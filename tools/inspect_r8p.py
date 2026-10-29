"""Read-only inventory of an already extracted installer. Does not run its code."""
from pathlib import Path
import argparse, hashlib, json, re
from collections import Counter

def inspect(root):
    source = root / 'installer.ahk'
    raw = source.read_bytes()
    text = raw.decode('utf-8-sig', errors='replace')
    commands = []
    installation = False
    for number, line in enumerate(text.splitlines(), 1):
        if line.startswith('PerformInstallation() {'):
            installation = True
        if line.startswith('HelpPaneLanguageCallback('):
            installation = False
        if not installation:
            continue
        m = re.match(r'(RegWrite|RegDelete|FileCopy|FileMove|FileMoveDir|FileCreateDir|FileDelete|FileRemoveDir|RunWait|Run|SetSecurityFile|SetSecurityRegistry|DllCall)\b', line)
        if m:
            commands.append({'line': number, 'operation': m[1], 'expression': line})
    registry = []
    for file in sorted((root / 'text' / 'Registry').glob('*.reg')):
        raw_reg = file.read_bytes()
        contents = raw_reg.decode('utf-16' if raw_reg.startswith(b'\xff\xfe') else 'utf-8-sig', errors='replace')
        keys = re.findall(r'^\[([^\]]+)\]', contents, re.M)
        values = re.findall(r'^(?:"[^"\r\n]*"|@)=', contents, re.M)
        registry.append({'file': file.name, 'sha256': hashlib.sha256(raw_reg).hexdigest(), 'keys': keys, 'valueCount': len(values)})
    modules = []
    for file in sorted((root / 'text' / 'ModsSource').glob('*.cpp')):
        raw_module = file.read_bytes()
        module_text = raw_module.decode('utf-8-sig', errors='replace')
        metadata = {key:value.strip() for key,value in re.findall(r'^//\s+@(id|name|author|version|license)\s+(.+)$', module_text, re.M)}
        modules.append({'file': file.name, 'sha256': hashlib.sha256(raw_module).hexdigest(), **metadata,
                        'licenseDisposition': 'review explicit terms' if 'license' in metadata else 'no explicit license in module header; do not import by default'})
    return {'schema': 1, 'kind': 'static-analysis-not-observed-installation',
            'installerScriptSha256': hashlib.sha256(raw).hexdigest(),
            'counts': dict(Counter(c['operation'] for c in commands)),
            'installCommands': commands, 'registryFiles': registry, 'modules': modules}

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('extracted', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    report = inspect(args.extracted)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'counts': report['counts'], 'registryFiles': len(report['registryFiles']), 'modules': len(report['modules'])}))
