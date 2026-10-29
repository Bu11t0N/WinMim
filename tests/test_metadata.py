import sys,tempfile,unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tools'))
from prepare import metadata

class HeaderTests(unittest.TestCase):
    def test_comment_examples_cannot_expand_process_inclusion(self):
        with tempfile.TemporaryDirectory() as directory:
            file=Path(directory)/'settings-to-control-panel.wh.cpp'
            file.write_text('''// ==WindhawkMod==
// @id settings-to-control-panel
// @version 1
// @include explorer.exe
// @architecture x86-64
// @license GPL-3.0
// ==/WindhawkMod==
// Documentation examples below are not metadata:
// @include winlogon.exe
// @include rundll32.exe matches EVERY invocation
// @compilerOptions -lnotreal
''',encoding='utf-8')
            item=metadata(file)
            self.assertEqual(item['includes'],['explorer.exe'])
            self.assertEqual(item['architecture'],['x86-64'])
            self.assertEqual(item['compilerOptions'],'')
            self.assertEqual(item['license'],'GPL-3.0')

if __name__=='__main__':unittest.main()
