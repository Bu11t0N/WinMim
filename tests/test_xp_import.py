import subprocess,sys,tempfile,unittest
from pathlib import Path

class XpImportTests(unittest.TestCase):
    def test_unknown_source_is_rejected_before_output_creation(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);source=root/'shell32.dll';source.write_bytes(b'MZ untrusted source')
            output=root/'pack'
            tool=Path(__file__).resolve().parents[1]/'tools/import_xp_assets.py'
            result=subprocess.run([sys.executable,str(tool),'--luna',str(root/'missing-theme'),'--shell32',str(source),'--output',str(output)],capture_output=True,text=True)
            self.assertNotEqual(result.returncode,0)
            self.assertIn('Unsupported XP shell32.dll',result.stderr)
            self.assertFalse(output.exists())

if __name__=='__main__':unittest.main()
