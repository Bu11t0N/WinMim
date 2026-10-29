"""Compile pinned upstream modules with the verified Windhawk LLVM toolchain.
Compilation never loads the resulting DLL into any process.
"""
from pathlib import Path
import argparse,json,shlex,subprocess,time
from prepare import ROOT,CONTENT,dump,sha,metadata
from own_modules import register

def build(only=None):
    toolchain=json.loads((ROOT/'.cache/toolchain.json').read_text())
    compiler=Path(toolchain['root'])/'Compiler';engine=Path(toolchain['engine'])
    catalog=json.loads((CONTENT/'catalog.json').read_text(encoding='utf-8'))
    known={m['id']:m for m in catalog['modules']}
    for source in sorted((ROOT/'vendor/windhawk-mods').glob('*.wh.cpp')):
        item=metadata(source);previous=known.get(item['id'])
        if previous:
            if previous['sourceSha256']!=item['sourceSha256']:raise ValueError('Source changed without lock update: '+item['id'])
            item['status']=previous['status'];item['binaries']=previous['binaries']
            previous.update(item)
        else:catalog['modules'].append(item)
    register(catalog)
    logdir=ROOT/'build/native-logs';logdir.mkdir(parents=True,exist_ok=True)
    result=[]
    for mod in catalog['modules']:
        if only and mod['id'] not in only:continue
        src=ROOT/mod['sourcePath'] if 'sourcePath' in mod else ROOT/'vendor/windhawk-mods'/(mod['id']+'.wh.cpp')
        if sha(src)!=mod['sourceSha256']:raise ValueError('Source modified without lock update: '+mod['id'])
        archs=mod['architecture'];targets=[]
        if not archs or 'x86' in archs:targets.append(('32','i686-w64-mingw32'))
        if not archs or any(x in archs for x in ['x86-64','amd64']):targets.append(('64','x86_64-w64-mingw32'))
        binaries=[];ok=True;start=time.monotonic()
        for arch,triple in targets:
            patch='-winmim1' if mod['id'] in ('icon-resource-redirect','classic-taskbar-properties','aerexplorer') else ''
            if 'sourcePath' in mod:patch='-'+mod['sourceSha256'][:10]
            filename=f"aeroforge-{mod['id']}_{mod['version']}{patch}.dll"
            target=CONTENT/f'modules/{arch}/{filename}';target.parent.mkdir(parents=True,exist_ok=True)
            args=[str(compiler/'bin/clang++.exe'),'-std=c++23','-O2','-shared','-DUNICODE','-D_UNICODE',
                '-DWINVER=0x0A00','-D_WIN32_WINNT=0x0A00','-D_WIN32_IE=0x0A00','-DNTDDI_VERSION=0x0A000008',
                '-D__USE_MINGW_ANSI_STDIO=0','-DWH_MOD',f'-DWH_MOD_ID=L"local@aeroforge-{mod["id"]}"',
                f'-DWH_MOD_VERSION=L"{mod["version"]}"',str(engine/arch/'windhawk.lib'),'-x','c++','-',
                '-include','windhawk_api.h','-include','atomic','-include','cmath','-include','vector',
                '-target',triple,'-Wl,--export-all-symbols','-o',str(target)]+shlex.split(mod['compilerOptions'])
            source=src.read_bytes()
            if mod['id']=='aerexplorer':
                source+=b'\n'+(ROOT/'src/WinMim.Native/aerexplorer-status.inc.cpp').read_bytes()
            if mod['id']=='classic-taskbar-properties':
                addition=(ROOT/'src/WinMim.Native/classic-taskbar-openshell.inc.cpp').read_bytes()
                replacements={b'ShowStartCustomDialog(hwnd);':b'if (!WinMimOpenShellSettings(hwnd)) ShowStartCustomDialog(hwnd);',
                    b'return RegGetDWordSafe(HKEY_CURRENT_USER, kAdvKey, L"Start_PowerButtonAction", 2);':b'return WinMimGetOpenShellPower(RegGetDWordSafe(HKEY_CURRENT_USER, kAdvKey, L"Start_PowerButtonAction", 2));',
                    b'RegSetDWordSafe(HKEY_CURRENT_USER, kAdvKey, L"Start_PowerButtonAction", v);':b'RegSetDWordSafe(HKEY_CURRENT_USER, kAdvKey, L"Start_PowerButtonAction", v); WinMimSetOpenShellPower(v);'}
                for original,replacement in replacements.items():
                    if source.count(original)!=1:raise ValueError('Classic properties integration point changed')
                    source=source.replace(original,replacement)
                source=addition+b'\n'+source
            if mod['id']=='icon-resource-redirect':
                # Refresh from the hooked Explorer process, never an unhooked controller.
                # Keep the pinned upstream source intact; this build-time addition is ours.
                source+=b'\nvoid Wh_ModAfterInit() { if (DoesCurrentProcessOwnTaskbar()) SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSHNOWAIT, nullptr, nullptr); }\n'
            p=subprocess.run(args,input=source,cwd=compiler,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=180)
            (logdir/f'{mod["id"]}-{arch}.log').write_bytes(p.stdout)
            if p.returncode:
                ok=False;print(f'FAILED {mod["id"]} {arch}: see build/native-logs',flush=True)
                break
            binaries.append({'path':target.relative_to(CONTENT).as_posix(),'target':f'Data/Engine/Mods/{arch}/{filename}','sha256':sha(target)})
        mod['binaries']=binaries if ok else []
        mod['status']='built' if ok else 'build-failed'
        result.append({'id':mod['id'],'status':mod['status'],'seconds':round(time.monotonic()-start,2)})
        dump(CONTENT/'catalog.json',catalog)
        print(mod['id'],mod['status'],f'{time.monotonic()-start:.1f}s',flush=True)
    dump(ROOT/'build/native-build-results.json',result)
    if any(m['status']!='built' for m in result):raise SystemExit(1)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--only',nargs='+');a=p.parse_args();build(a.only)
