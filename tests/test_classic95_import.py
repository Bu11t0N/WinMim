import subprocess,sys,tempfile,unittest
from pathlib import Path

class Classic95ImportTests(unittest.TestCase):
    def test_unknown_executable_is_rejected_before_output_creation(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);source=root/'shell32.dll';source.write_bytes(b'MZ not the pinned Windows binary')
            destination=root/'icons'
            tool=Path(__file__).resolve().parents[1]/'tools/import_classic95_icons.py'
            result=subprocess.run([sys.executable,str(tool),'--shell32',str(source),'--output',str(destination)],capture_output=True,text=True)
            self.assertNotEqual(result.returncode,0)
            self.assertIn('Unsupported shell32.dll',result.stderr)
            self.assertFalse(destination.exists())

if __name__=='__main__':unittest.main()
