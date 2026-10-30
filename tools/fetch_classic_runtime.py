"""Fetch the pinned unmodified RetroBar runtime; never execute it."""
from pathlib import Path
import hashlib, urllib.request, zipfile
ROOT=Path(__file__).resolve().parents[1]
VERSION='v1.22.122'
COMMIT='cff7a787c35c5f3e1db67a22a93721b911313fae'
ARCHIVE_SHA='6ab0d0972535e5efedc13c83403b52883edb06881d2d6aa18bf8af379bc7a128'
SOURCE_SHA='78277995c2562e82b536e0af35252955b342559b39c8c0d03cf91e246a8dfbaf'
FILES={'RetroBar.exe':'bf3fda3e31a1839482bd3ec355cd9fffebf27d3ce378a8c0f890670e24af7d1a','License.txt':'3413530d8a5a30ad46de082ea70ff0e2195565499393d5156635a4787f80d215'}
LOCK={'version':VERSION,'commit':COMMIT,'url':f'https://github.com/dremin/RetroBar/releases/download/{VERSION}/RetroBar.Portable.zip','archiveSha256':ARCHIVE_SHA,'sourceArchiveSha256':SOURCE_SHA,'files':FILES,'license':'Apache-2.0; bundled BSD-3-Clause notices retained'}
def fetch(url,path,expected):
    if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest()!=expected:
        with urllib.request.urlopen(url,timeout=60) as r:data=r.read(64*1024*1024+1)
        if len(data)>64*1024*1024 or hashlib.sha256(data).hexdigest()!=expected:raise ValueError('Package hash mismatch')
        path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
def main():
    archive=ROOT/'.cache/RetroBar.Portable.zip'
    fetch(f'https://github.com/dremin/RetroBar/releases/download/{VERSION}/RetroBar.Portable.zip',archive,ARCHIVE_SHA)
    fetch(f'https://github.com/dremin/RetroBar/archive/{COMMIT}.zip',ROOT/'vendor/sources/retrobar-source.zip',SOURCE_SHA)
    with zipfile.ZipFile(ROOT/'vendor/sources/retrobar-source.zip') as source:
        license_data=source.read(f'RetroBar-{COMMIT}/LICENSE')
    licenses=ROOT/'content/licenses';licenses.mkdir(parents=True,exist_ok=True)
    (licenses/'RetroBar-Apache-2.0.txt').write_bytes(license_data)
    destination=ROOT/'content/classic-runtime/retrobar';destination.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(archive) as z:
        for name,expected in FILES.items():
            data=z.read('64-bit/'+name)
            if hashlib.sha256(data).hexdigest()!=expected:raise ValueError(name)
            (destination/name).write_bytes(data)
    print('Verified RetroBar '+VERSION+' (x64, .NET 10); not executed')
if __name__=='__main__':main()
