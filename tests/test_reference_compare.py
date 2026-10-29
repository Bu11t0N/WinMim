import importlib.util
from pathlib import Path
import tempfile,unittest,json

spec=importlib.util.spec_from_file_location('compare_reference',Path(__file__).parents[1]/'tools/compare_reference.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

class ReferenceCompareTests(unittest.TestCase):
    def test_registry_continuations_types_default_and_escaped_names(self):
        with tempfile.TemporaryDirectory() as temp:
            file=Path(temp)/'sample.reg'
            file.write_text('Windows Registry Editor Version 5.00\n\n[HKEY_CURRENT_USER\\Software\\Test]\n@="default"\n"Name\\\"Quoted"=hex(2):41,00,\\\n  42,00\n"Number"=dword:00000001\n',encoding='utf-16')
            parsed=module.registry(file)
            self.assertEqual(parsed[('hkey_current_user\\software\\test','@')],'"default"')
            self.assertEqual(parsed[('hkey_current_user\\software\\test','"name\\\"quoted"')],'hex(2):41,00,42,00')
    def test_metadata_noise_does_not_hide_content_or_acl_changes(self):
        old={'a':{'sha256':'one','sddl':'acl1','length':10,'lastWriteUtc':'first'}}
        new={'a':{'sha256':'one','sddl':'acl1','length':10,'lastWriteUtc':'second'}}
        self.assertEqual(module.delta(old,new,['sha256','sddl','length']),[])
        new['a']['sddl']='acl2'
        self.assertEqual(module.delta(old,new,['sha256','sddl','length'])[0]['kind'],'changed')
    def test_added_removed_and_incomplete_capture(self):
        self.assertEqual([x['kind'] for x in module.delta({'a':1},{'b':2})],['removed','added'])
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaisesRegex(ValueError,'Incomplete capture'):
                module.compare(Path(temp),Path(temp))
    def test_proven_absence_differs_from_failed_registry_export(self):
        with tempfile.TemporaryDirectory() as temp:
            before=Path(temp)/'before';after=Path(temp)/'after'
            for folder in [before,after]:
                folder.mkdir()
                for name in ['complete','system']:(folder/(name+'.json')).write_text('{}')
                for name in ['issues','files','services','tasks','links']:(folder/(name+'.json')).write_text('[]')
            key='HKCU\\Software\\Test'
            (before/'registry-index.json').write_text(json.dumps([{'key':key,'exitCode':0,'exists':False,'file':None}]))
            (after/'registry-index.json').write_text(json.dumps([{'key':key,'exitCode':0,'exists':True,'file':'value.reg'}]))
            (after/'value.reg').write_text('[HKEY_CURRENT_USER\\Software\\Test]\n"Enabled"=dword:00000001\n')
            report=module.compare(before,after)
            self.assertEqual(report['counts']['registry'],2)
            (before/'registry-index.json').write_text(json.dumps([{'key':key,'exitCode':1,'exists':None,'file':None}]))
            report=module.compare(before,after)
            self.assertEqual(report['counts']['registry'],0)
            self.assertEqual(report['counts']['unavailableRegistryScopes'],1)
            (before/'files.json').write_text(json.dumps([{'path':'C:\\Windows\\example.dll','sha256':'hash','length':1,'sddl':'acl'}]))
            (after/'issues.json').write_text(json.dumps([{'kind':'file','path':'C:\\Windows\\example.dll','error':'Access denied'}]))
            report=module.compare(before,after)
            self.assertEqual(report['files'],[])
            self.assertEqual(report['unavailableFiles'],['c:\\windows\\example.dll'])

if __name__=='__main__':unittest.main()
