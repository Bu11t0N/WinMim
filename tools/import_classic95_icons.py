"""Extract exact ICO resources from a user-supplied Windows 95 shell32.dll.

Reads PE data only; never loads or executes the DLL. The private output must not
be distributed as GPL project artwork. Extract shell32.dll from your own media
with 7-Zip first. Only the pinned Russian retail binary is currently supported.
"""
from pathlib import Path
import argparse,hashlib,json,struct

def extract(data):
    pe=struct.unpack_from('<I',data,0x3c)[0];opt=pe+24
    if data[pe:pe+4]!=b'PE\0\0':raise ValueError('Not a PE image')
    directories=opt+(112 if struct.unpack_from('<H',data,opt)[0]==0x20b else 96)
    resource_rva=struct.unpack_from('<I',data,directories+16)[0];sections=[]
    for i in range(struct.unpack_from('<H',data,pe+6)[0]):
        at=opt+struct.unpack_from('<H',data,pe+20)[0]+40*i
        sections.append((struct.unpack_from('<I',data,at+12)[0],struct.unpack_from('<I',data,at+8)[0],struct.unpack_from('<I',data,at+20)[0]))
    def offset(rva):
        for address,size,raw in sections:
            if address<=rva<address+size:return raw+rva-address
        raise ValueError('Resource outside image')
    base=offset(resource_rva)
    def tree(at):
        result={}
        for i in range(sum(struct.unpack_from('<HH',data,at+12))):
            key,value=struct.unpack_from('<II',data,at+16+8*i)
            if key&0x80000000:continue
            if value&0x80000000:result[key]=tree(base+(value&0x7fffffff))
            else:
                rva,size=struct.unpack_from('<II',data,base+value);result[key]=data[offset(rva):offset(rva)+size]
        return result
    resources=tree(base);icons={}
    for group_id,languages in resources[14].items():
        language=next(iter(languages));group=languages[language];count=struct.unpack_from('<H',group,4)[0]
        entries=[];images=[];position=6+16*count
        for i in range(count):
            entry=group[6+14*i:20+14*i];image_id=struct.unpack_from('<H',entry,12)[0]
            image=resources[3][image_id].get(language,next(iter(resources[3][image_id].values())))
            entries.append(entry[:8]+struct.pack('<II',len(image),position));images.append(image);position+=len(image)
        icons[group_id]=group[:6]+b''.join(entries)+b''.join(images)
    return icons

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--shell32',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    reference=json.loads((Path(__file__).resolve().parents[1]/'content/classic95/original-reference.json').read_text())
    data=args.shell32.read_bytes()
    if hashlib.sha256(data).hexdigest()!=reference['sourceBinarySha256']:raise ValueError('Unsupported shell32.dll; source hash differs from the verified reference')
    icons=extract(data);files={}
    for item in reference['files']:
        icon=icons[item['resourceId']]
        if hashlib.sha256(icon).hexdigest()!=item['sha256']:raise ValueError('Icon extraction hash mismatch: '+item['name'])
        files[item['name']]=icon
    args.output.mkdir(parents=True,exist_ok=False)
    for name,icon in files.items():(args.output/name).write_bytes(icon)
    (args.output/'manifest.json').write_text(json.dumps(reference,indent=2)+'\n')
    (args.output/'NOTICE.txt').write_text(reference['license']+'\n'+reference['source']+'\n')
    print(f'Extracted {len(files)} exact original icons; kept original sizes and palettes. Private output: {args.output}')

if __name__=='__main__':main()
