"""Prepare the pinned private XP pack from separately obtained resource files.

No installer or DLL is executed. The checked-in reference is metadata only.
Binary output contains third-party/Microsoft resources and must stay private.
"""
from pathlib import Path
import argparse,hashlib,json,shutil,struct
from import_classic95_icons import extract,read_resources

def extract_cursors(data):
    resources=read_resources(data);result={}
    for key,languages in resources.get(12,{}).items():
        language=next(iter(languages));group=languages[language];count=struct.unpack_from('<H',group,4)[0]
        entries=[];images=[];offset=6+16*count
        for i in range(count):
            width,height,planes,bits,length,image_id=struct.unpack_from('<HHHHIH',group,6+14*i)
            image=resources[1][image_id].get(language,next(iter(resources[1][image_id].values())))
            x,y=struct.unpack_from('<HH',image);bitmap=image[4:]
            width=struct.unpack_from('<i',bitmap,4)[0];height=abs(struct.unpack_from('<i',bitmap,8)[0])//2
            entries.append(struct.pack('<BBBBHHII',width%256,height%256,0,0,x,y,len(bitmap),offset))
            images.append(bitmap);offset+=len(bitmap)
        result[key]=struct.pack('<HHH',0,2,count)+b''.join(entries)+b''.join(images)
    return result

def prepare(luna,shell32,output,reference,resources=None,user32=None,media=None,menu_skin=None,notepad=None):
    data=shell32.read_bytes()
    if hashlib.sha256(data).hexdigest()!=reference['sourceBinarySha256']:
        raise ValueError('Unsupported XP shell32.dll; expected the pinned source binary')
    icons=extract(data)
    cursors={}
    if reference.get('cursors'):
        if user32 is None or hashlib.sha256(user32.read_bytes()).hexdigest()!=reference['sourceCursorBinarySha256']:
            raise ValueError('Supply the pinned XP user32.dll with --user32')
        cursors=extract_cursors(user32.read_bytes())
    roles={'computer':16,'documents':235,'network':18,'recycle-empty':32,'recycle-full':33,
        'programs':20,'settings':22,'search':23,'help':24,'run':25,'shutdown':26,'start':40,
        'printers':17,'folder-closed':4,'folder-open':5,'drive-removable':8,'drive-fixed':9,
        'drive-network':10,'drive-disconnected':11,'drive-cd':12,'drive-floppy':7}
    payload={}
    for entry in reference['files']:
        name=entry['name'];relative=Path(name)
        if relative.is_absolute() or '..' in relative.parts or ':' in name or '\\' in name:
            raise ValueError('Unsafe resource path')
        if name.startswith('Icons/'):
            role=relative.stem
            if role=='notepad':
                if notepad is None or hashlib.sha256(notepad.read_bytes()).hexdigest()!=reference['sourceNotepadSha256']:raise ValueError('Supply the pinned source with --notepad')
                payload[name]=next(iter(extract(notepad.read_bytes()).values()))
            else:payload[name]=icons[roles[role]]
        elif name.startswith('Luna/'):
            path=luna/Path(*relative.parts[1:])
            if path.is_symlink():raise ValueError('Resource symlinks are not accepted')
            payload[name]=path.read_bytes()
        elif name.startswith('Resources/') and resources is not None:
            payload[name]=(resources/relative.name).read_bytes()
        elif name.startswith('Cursors/'):
            payload[name]=cursors[reference['cursorResourceIds'][relative.stem]]
        elif name.startswith('Sounds/') and media is not None:
            payload[name]=(media/relative.name).read_bytes()
        elif name.startswith('Menu/') and menu_skin is not None:
            payload[name]=menu_skin.read_bytes()
        else:raise ValueError('Supply --resources built with tools/build_xp_icon_libraries.py')
        if hashlib.sha256(payload[name]).hexdigest()!=entry['sha256']:
            raise ValueError('Resource SHA-256 mismatch: '+name)
    # Validate everything before creating output or copying a single resource.
    output.mkdir(parents=True,exist_ok=False)
    for name,data in payload.items():
        path=output/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    (output/'manifest.json').write_text(json.dumps(reference,indent=2)+'\n',encoding='utf8')
    (output/'NOTICE.txt').write_text(reference['license']+'\n'+reference['source']+'\n',encoding='utf8')
    return len(payload)

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--luna',type=Path,required=True,help='Extracted Luna directory (not an installer)')
    p.add_argument('--shell32',type=Path,required=True,help='Pinned XP SP3 RU source binary')
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--resources',type=Path,help='Prepared partial resource libraries and their pinned manifest')
    p.add_argument('--user32',type=Path,help='Pinned XP source of standard pointer groups')
    p.add_argument('--media',type=Path,help='Directory containing the separately obtained pinned XP WAV files')
    p.add_argument('--menu-skin',type=Path,help='Separate skin generated by adapt_xp_menu_skin.py')
    p.add_argument('--notepad',type=Path,help='Pinned source of the original XP Notepad icon')
    a=p.parse_args()
    reference=json.loads((Path(__file__).resolve().parents[1]/'content/xp/pack-reference.json').read_text('utf8'))
    print('Prepared',prepare(a.luna,a.shell32,a.output,reference,a.resources,a.user32,a.media,a.menu_skin,a.notepad),'private resources; do not redistribute.')

if __name__=='__main__':main()
