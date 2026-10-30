"""Build small, resource-only private icon libraries from exact imported XP ICOs.

Missing groups fall back to Windows through icon-resource-redirect. This does
not replace system DLLs or copy dialogs/strings from another Windows version.
"""
from pathlib import Path
import argparse,subprocess,hashlib,json

MAPPINGS={
    'shell32.dll':{4:'folder-closed',5:'folder-open',7:'drive-floppy',8:'drive-removable',9:'drive-fixed',10:'drive-network',11:'drive-disconnected',12:'drive-cd',16:'computer',17:'printers',18:'network',20:'programs',22:'settings',23:'search',24:'help',25:'run',26:'shutdown',32:'recycle-empty',33:'recycle-full',235:'documents'},
    'imageres.dll':{3:'folder-closed',4:'folder-open',25:'network',54:'recycle-full',55:'recycle-empty',109:'computer',123:'documents'}
}
def build(icons,output,rc,cvtres,lld):
    output.mkdir(parents=True,exist_ok=True)
    for name,groups in MAPPINGS.items():
        script=output/(name+'.rc');res=output/(name+'.res');obj=output/(name+'.obj')
        lines=['LANGUAGE 0, 0']
        for group,role in groups.items():
            path=(icons/(role+'.ico')).resolve()
            if not path.is_file():raise ValueError('Missing original icon: '+role)
            lines.append(f'{group} ICON "{path.as_posix()}"')
        script.write_text('\n'.join(lines)+'\n',encoding='utf8')
        subprocess.run([str(rc),'/nologo','/fo',str(res),str(script)],check=True)
        subprocess.run([str(cvtres),'/nologo','/machine:x64','/out:'+str(obj),str(res)],check=True)
        subprocess.run([str(lld),'-flavor','link','/dll','/noentry','/nodefaultlib','/machine:x64','/timestamp:0','/out:'+str(output/name),str(obj)],check=True)
    manifest={'source':'Exact original XP icons remapped to selected Windows 10 resource groups; partial libraries only',
        'files':[{'fileName':name,'sha256':hashlib.sha256((output/name).read_bytes()).hexdigest()} for name in MAPPINGS]}
    (output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf8')

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['icons','output','rc','cvtres','lld']:p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();build(a.icons,a.output,a.rc,a.cvtres,a.lld)
    print('Built two private resource-only libraries; keep Microsoft artwork out of public archives.')
if __name__=='__main__':main()
