"""Fetch only exact Microsoft PDB identities observed in the offline lab."""
from pathlib import Path
from http_retry import read_https
import json,urllib.request,urllib.error,struct,uuid,hashlib,zipfile,concurrent.futures,argparse,re
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--metadata',type=Path,required=True)
parser.add_argument('--store',type=Path,required=True)
parser.add_argument('--archive',type=Path)
args=parser.parse_args()
STORE=args.store.resolve();STORE.mkdir(parents=True,exist_ok=True)
items={}
for entry in json.loads(args.metadata.read_text(encoding='utf-8-sig')):
 for line in entry['Text'].splitlines():
  if not line.startswith('pdb_') or '=error:' not in line:continue
  key=line.split('=',1)[0][4:];module=line.rsplit('#',1)[-1].rsplit('-',2)[0]
  name=Path(module).stem+'.pdb'
  if not re.fullmatch(r'[A-Za-z0-9_.-]+[.]pdb',name) or not re.fullmatch(r'[0-9A-F]{33,40}',key):raise ValueError('Invalid symbol identity')
  items[(name,key)]=None
def identity(data):
 assert data.startswith(b'Microsoft C/C++ MSF 7.00'), 'Not an MSF PDB'
 bs,_,blocks,dirsize,_,blockmap=struct.unpack_from('<IIIIII',data,32)
 assert bs in (512,1024,2048,4096,8192) and blocks*bs<=len(data)
 nd=(dirsize+bs-1)//bs
 ids=struct.unpack_from('<'+'I'*nd,data,blockmap*bs)
 directory=b''.join(data[i*bs:(i+1)*bs] for i in ids)[:dirsize]
 count=struct.unpack_from('<I',directory)[0];sizes=struct.unpack_from('<'+'I'*count,directory,4);pos=4+count*4
 result={}
 for i,size in enumerate(sizes):
  if size==0xffffffff:continue
  n=(size+bs-1)//bs;ids=struct.unpack_from('<'+'I'*n,directory,pos);pos+=n*4
  if i in (1,3):
   info=b''.join(data[j*bs:(j+1)*bs] for j in ids)[:size]
   age=struct.unpack_from('<I',info,8)[0]
   if i==1:result.update(guid=uuid.UUID(bytes_le=info[12:28]).hex.upper(),pdbAge=age)
   else:result['dbiAge']=age
 assert len(result)==3,'PDB identity streams missing'
 return result
def fetch(item):
 name,key=item;path=STORE/name/key/name;path.parent.mkdir(parents=True,exist_ok=True)
 url=f'https://msdl.microsoft.com/download/symbols/{name}/{key}/{name}'
 if path.exists():data=path.read_bytes()
 else:
  try:
   data=read_https(url)
  except urllib.error.HTTPError as error:
   if error.code!=404:raise
   # Some WOW64 binaries use a w-prefixed PDB name. Still require exact GUID/DBI age.
   name='w'+name;path=STORE/name/key/name;path.parent.mkdir(parents=True,exist_ok=True)
   url=f'https://msdl.microsoft.com/download/symbols/{name}/{key}/{name}'
   data=read_https(url)
 found=identity(data);age=int(key[32:],16)
 # Microsoft PDB1::OpenValidate4: GUID exact, PDB age >= image age,
 # DBI age == image age. Public/stripped PDB info age can be higher.
 assert found['guid']==key[:32] and found['pdbAge']>=age and found['dbiAge']==age,(name,key,found)
 path.write_bytes(data)
 print('Verified',name,key,len(data),flush=True)
 return {'path':path.relative_to(STORE).as_posix(),'url':url,'identity':key,'streams':found,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
assert len(items)<=128,'Too many symbols in one request'
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:manifest=list(pool.map(fetch,sorted(items)))
(STORE/'manifest.json').write_text(json.dumps(manifest,indent=2))
if args.archive:
 args.archive.parent.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(args.archive,'w',zipfile.ZIP_DEFLATED) as z:
  for p in STORE.rglob('*'):
   if p.is_file():z.write(p,'Symbols/'+p.relative_to(STORE).as_posix())
print('Ready:',len(manifest),'PDBs; not included in AeroForge distribution',flush=True)


