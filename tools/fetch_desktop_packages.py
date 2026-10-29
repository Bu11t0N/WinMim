"""Fetch pinned installers and an optional external Seven visual style; never run them."""
from pathlib import Path
import argparse, hashlib, json, urllib.request

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--destination',type=Path,required=True)
parser.add_argument('--with-aero10',action='store_true',help='Also fetch the separate CC BY-NC-SA theme for personal lab use')
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
catalog=json.loads((root/'content/catalog.json').read_text(encoding='utf-8'))
packages=[dict(d) for d in catalog['dependencies'] if d['id'] in ('open-shell','secureuxtheme','openglass')]
if args.with_aero10:
 packages.append(dict(id='aero10-seven',name='Aero10 Seven by vaporvance',version='a10-archive',license='CC-BY-NC-SA-4.0; Microsoft assets retain their rights',fileName='aero10-seven.zip',downloadUrl='https://github.com/vaporvance/aero10/releases/download/a10-archive/aero10-seven.zip',sha256='6e97f8860991b5903e27105be738433cfb4d38b1d26677371fe1a212ebc0d68a'))
args.destination.mkdir(parents=True,exist_ok=True)
for package in packages:
 path=args.destination/package['fileName']
 if not path.exists():
  pending=path.with_suffix(path.suffix+'.download')
  try:
   with urllib.request.urlopen(package['downloadUrl'],timeout=90) as response, pending.open('wb') as target:
    size=0
    while chunk:=response.read(1024*1024):
     size+=len(chunk)
     if size>128*1024*1024:raise ValueError('Package exceeds limit')
     target.write(chunk)
   if hashlib.sha256(pending.read_bytes()).hexdigest()!=package['sha256']:raise ValueError('Package SHA-256 mismatch: '+path.name)
   pending.replace(path)
  finally:pending.unlink(missing_ok=True)
 if hashlib.sha256(path.read_bytes()).hexdigest()!=package['sha256']:raise ValueError('Cached package SHA-256 mismatch: '+path.name)
 print('Verified:',path.name)
(args.destination/'desktop-packages.json').write_text(json.dumps(packages,ensure_ascii=False,indent=2),encoding='utf-8')
