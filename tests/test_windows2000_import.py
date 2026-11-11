import hashlib,json,struct,sys,tempfile,unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tools'))
from import_windows2000_assets import prepare

class Windows2000ImportTests(unittest.TestCase):
    def fixture(self,root,data):
        icons=root/'icons';icons.mkdir();(icons/'test.ico').write_bytes(data)
        ref=root/'reference.json'
        ref.write_text(json.dumps({'files':[{'name':'test.ico','input':'icons/test.ico','size':len(data),'sha256':hashlib.sha256(data).hexdigest()}],'license':'test','source':'test'}))
        return icons,ref
    def test_hash_change_does_not_create_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);icons,ref=self.fixture(root,b'original');(icons/'test.ico').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError,'hash/size'):prepare(icons,root,root/'output',ref)
            self.assertFalse((root/'output').exists())
    def test_out_of_bounds_image_does_not_create_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);data=b'\0\0\1\0\1\0'+b'\x10\x10\0\0\1\0\x20\0'+struct.pack('<II',40,999999)
            icons,ref=self.fixture(root,data)
            with self.assertRaisesRegex(ValueError,'bounds'):prepare(icons,root,root/'output',ref)
            self.assertFalse((root/'output').exists())
    def test_source_traversal_is_rejected_before_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);icons,ref=self.fixture(root,b'anything');m=json.loads(ref.read_text());m['files'][0]['input']='icons/../outside.ico';ref.write_text(json.dumps(m))
            with self.assertRaisesRegex(ValueError,'escapes'):prepare(icons,root,root/'output',ref)
            self.assertFalse((root/'output').exists())

if __name__=='__main__':unittest.main()
