"""Compare two read-only VM captures. Produces evidence, never an import script."""
import argparse, json, re
from pathlib import Path

def read_json(path, empty=None):
    raw=path.read_bytes()
    text=raw.decode('utf-16' if raw.startswith(b'\xff\xfe') else 'utf-8-sig')
    return json.loads(text) if text.strip() else empty

def records(path):
    value=read_json(path, [])
    return value if isinstance(value,list) else [value]

def registry(path):
    raw=path.read_bytes()
    text=raw.decode('utf-16' if raw.startswith(b'\xff\xfe') else 'utf-8-sig')
    text=re.sub(r'\\\r?\n\s*','',text)
    key=None;result={}
    for line in text.splitlines():
        line=line.strip()
        if line.startswith('[') and line.endswith(']'):
            key=line[1:-1].casefold()
            result[(key,None)]='key'
        elif key:
            match=re.match(r'(@|"(?:\\.|[^"\\])*")=(.*)$',line)
            if match:
                name=match[1].casefold()
                value=match[2]
                if value.startswith(('hex:', 'hex(', 'dword:')):value=value.lower().replace(' ','')
                result[(key,name)]=value
    return result

def delta(before,after,fields=None):
    changes=[]
    for identity in sorted(before.keys() | after.keys(),key=str):
        old=before.get(identity);new=after.get(identity)
        old_content={k:old.get(k) for k in fields} if fields and old is not None else old
        new_content={k:new.get(k) for k in fields} if fields and new is not None else new
        if old_content!=new_content:
            changes.append({'identity':identity,'kind':'added' if old is None else 'removed' if new is None else 'changed','before':old,'after':new})
    return changes

def compare(before,after):
    for path in [before,after]:
        if not(path/'complete.json').exists():raise ValueError(f'Incomplete capture: {path}')
    report={'schema':1,'kind':'observed-state-difference-not-causal-proof',
            'before':read_json(before/'system.json'),'after':read_json(after/'system.json'),
            'coverageIssues':{'before':records(before/'issues.json'),'after':records(after/'issues.json')},
            'limitations':['Only recorded scopes are covered.','Background Windows changes may be unrelated; use a control capture.','A state diff does not explain hooks or resources inside binaries.']}
    for name,identity,fields in [('files','path',['sha256','length','sddl']),('services','Name',['PathName','StartMode','StartName']),('tasks',None,None),('links','path',None)]:
        capture_issues=report['coverageIssues']['before']+report['coverageIssues']['after']
        if any(issue.get('kind')==name for issue in capture_issues):
            report[name]=[]
            report.setdefault('unavailableCategories',[]).append(name)
            continue
        old={str(x[identity]).casefold() if identity else (x['path']+x['name']).casefold():x for x in records(before/(name+'.json'))}
        new={str(x[identity]).casefold() if identity else (x['path']+x['name']).casefold():x for x in records(after/(name+'.json'))}
        if name=='files':
            unknown_files={x['path'].casefold() for x in capture_issues if x.get('kind')=='file'}
            unknown_directories=[x['path'].casefold().rstrip('\\')+'\\' for x in capture_issues if x.get('kind')=='directory']
            unknown={key for key in old.keys()|new.keys() if key in unknown_files or any(key.startswith(directory) for directory in unknown_directories)}
            report['unavailableFiles']=sorted(unknown)
            old={key:value for key,value in old.items() if key not in unknown}
            new={key:value for key,value in new.items() if key not in unknown}
        report[name]=delta(old,new,fields)
    old_index={x['key'].casefold():x for x in records(before/'registry-index.json')}
    new_index={x['key'].casefold():x for x in records(after/'registry-index.json')}
    report['registry']=[];report['unavailableRegistryScopes']=[]
    for key in sorted(old_index.keys()|new_index.keys()):
        a=old_index.get(key);b=new_index.get(key)
        if not a or not b or a['exitCode']!=0 or b['exitCode']!=0:
            report['unavailableRegistryScopes'].append({'key':key,'before':a,'after':b})
            # Failed exports cannot prove absence. Preserve uncertainty explicitly.
            continue
        old_values={} if a.get('exists') is False else registry(before/a['file'])
        new_values={} if b.get('exists') is False else registry(after/b['file'])
        report['registry']+=delta(old_values,new_values)
    report['counts']={k:len(report[k]) for k in ['files','services','tasks','links','registry','unavailableRegistryScopes']}
    return report

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('before',type=Path);parser.add_argument('after',type=Path)
    parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    report=compare(args.before,args.after)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report['counts']))
