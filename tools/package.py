"""Package source and a runtime-dependent application; no installer is executed."""
from pathlib import Path
import hashlib,json,zipfile,datetime
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT.parent

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def archive(destination,files):
    with zipfile.ZipFile(destination,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for file,name in sorted(files,key=lambda x:x[1]):
            entry=zipfile.ZipInfo(name,(2026,1,1,0,0,0));entry.compress_type=zipfile.ZIP_DEFLATED
            entry.external_attr=0o100644<<16
            z.writestr(entry,file.read_bytes())
    return {'name':destination.name,'bytes':destination.stat().st_size,'sha256':sha(destination)}

def package():
    catalog=json.loads((ROOT/'content/catalog.json').read_text(encoding='utf-8'))
    assert all(m['status']=='built' for m in catalog['modules'])
    for f in catalog['runtimeFiles']+[b for m in catalog['modules'] for b in m['binaries']]:
        assert sha(ROOT/'content'/f['path'])==f['sha256'],f['path']
    notices=['# Third-party notices','','Own WinMim code: GPL-3.0. Each component below retains its own license.','',
        '## Windhawk runtime','','Version 1.7.3, GPL-3.0. Unmodified official runtime; corresponding source archive: `vendor/sources/windhawk-v1.7.3.zip`.',
        'Upstream: https://github.com/ramensoftware/windhawk/tree/v1.7.3','',
        'LLVM runtime components retain their upstream licenses; see `content/licenses/LLVM-LICENSE.txt`. Official Windhawk debug helper libraries retain their respective upstream terms.',
        '', '## Modules','', 'Upstream sources are pinned at commit `'+catalog['sourceCommit']+'`. Original WinMim modules are under src/WinMim.Native and retain their stated GPL-3.0 license.',
        'For modules without an explicit license, the upstream contribution policy states MIT. See `vendor/windhawk-mods/UPSTREAM-README.md`.',
        '', 'The icon-resource-redirect binary includes a WinMim build-time Wh_ModAfterInit addition to refresh icons from the hooked Explorer process; see tools/build_modules.py. The pinned upstream file remains unchanged.',
        'The classic-taskbar-properties binary includes original GPL-3.0 WinMim integration with Open-Shell settings and its shutdown-button preference. See src/WinMim.Native/classic-taskbar-openshell.inc.cpp and tools/build_modules.py; the pinned upstream source retains its original license and remains unchanged.',
        'The Aerexplorer binary includes an original GPL-3.0 WinMim read-only diagnostic export; see src/WinMim.Native/aerexplorer-status.inc.cpp. The pinned upstream source remains unchanged.',
        'The optional legacy Microsoft display.dll provider is verified and supplied separately in the private lab. It is not included in either archive and is not relicensed.',
        '', '| Module | Author | Version | License | Source |','|---|---|---|---|---|']
    for m in catalog['modules']:notices.append(f'| {m["name"]} | {m["author"]} | {m["version"]} | {m["license"]} | [source]({m["sourceUrl"]}) |')
    notices+=['','## Optional installers','','These installers are downloaded directly from upstream on a user action; they are not bundled in this release.']
    for d in catalog['dependencies']:notices.append(f'- {d["name"]} {d["version"]}: {d["license"]}. {d["sourceUrl"]}')
    notices+=['','## Optional lab visual style','','Aero10 Seven by vaporvance is a separate CC BY-NC-SA 4.0 package; Microsoft assets retain their rights. The theme, wallpaper and start-button images are not bundled or relicensed as GPL. The explicit tools/fetch_desktop_packages.py --with-aero10 option obtains the external upstream package for personal lab use. See https://github.com/vaporvance/aero10 and docs/SEVEN-DESKTOP-LAB.md.',
        '', '## Private reference resources and extras', '', 'The optional private W7Aero-Redstone theme, 56 resource-only icon libraries, games, Desktop Gadgets, Media Center, Windows 7 WAV sounds and DVD Appx/license are supplied separately for the private lab. None is bundled or relicensed. The original WinMim coordinator validates pinned files and retains native uninstallers. See docs/SEVEN-PROGRESS-20261024.md.', '', '## Classic95 artwork', '', 'Five original homage icons by ke2083 are included under GPL-3.0, pinned at d1cb96c9841c9ce321d7c95c0ee266e0d483c702. See content/classic95/LICENSE, UPSTREAM-README.md and sources.json. They are not extracted Microsoft Windows 95 binaries. The Start button in the Classic95 profile uses the existing system icon and the native Open-Shell classic renderer.', '', '## Original tools and artwork','','The system-native Windows Forms interface, remaining WPF utilities, transaction engine, Mines game, gadgets, media player, Alt+Tab implementation and generated taskbar texture are original WinMim code. No Revert8Plus visual resource packs or proprietary StartIsBack binaries are included.']
    (ROOT/'THIRD-PARTY-NOTICES.md').write_text('\n'.join(notices)+'\n',encoding='utf-8')
    mit='''MIT License

Copyright: the respective module authors, identified in THIRD-PARTY-NOTICES.md and original source headers.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
'''
    (ROOT/'content/licenses/Modules-MIT.txt').write_text(mit,encoding='utf-8')
    sbom={'bomFormat':'CycloneDX','specVersion':'1.6','version':1,'metadata':{'component':{'type':'application','name':'WinMim','version':catalog['version']}},
        'components':[{'type':'library','name':m['id'],'version':m['version'],'author':m['author'],
            'licenses':[{'license':{'name':m['license']}}],'externalReferences':[{'type':'vcs','url':m['sourceUrl']}],
            'hashes':[{'alg':'SHA-256','content':m['sourceSha256']}]} for m in catalog['modules']]}
    (ROOT/'sbom.cdx.json').write_text(json.dumps(sbom,ensure_ascii=False,indent=2),encoding='utf-8')
    app=ROOT/'build/app';cli=ROOT/'build/cli'
    assert (app/'WinMim.exe').exists() and (cli/'AeroForge.Cli.exe').exists()
    portable=[]
    # Take content from the current verified source, not a potentially stale publish directory.
    for p in app.iterdir():
        if p.is_file():portable.append((p,'WinMim/'+p.name))
    for p in (app/'tools').rglob('*'):
        if p.is_file():portable.append((p,'WinMim/tools/'+p.relative_to(app/'tools').as_posix()))
    for name in ['Capture-R8PReference.ps1','compare_reference.py','inspect_r8p.py','reference-scopes.json']:
        portable.append((ROOT/'tools'/name,'WinMim/tools/'+name))
    for p in cli.iterdir():
        if p.is_file():portable.append((p,'WinMim/cli/'+p.name))
    for p in (ROOT/'content').rglob('*'):
        if p.is_file():portable.append((p,'WinMim/content/'+p.relative_to(ROOT/'content').as_posix()))
    for name in ['README.md','LICENSE','THIRD-PARTY-NOTICES.md','sbom.cdx.json']:
        portable.append((ROOT/name,'WinMim/'+name))
    for p in (ROOT/'docs').rglob('*'):
        if p.is_file():portable.append((p,'WinMim/docs/'+p.relative_to(ROOT/'docs').as_posix()))
    # The exact engine source is included to accompany its binary distribution.
    for p in (ROOT/'vendor').rglob('*'):
        if p.is_file():portable.append((p,'WinMim/vendor/'+p.relative_to(ROOT/'vendor').as_posix()))
    sources=[]
    for p in ROOT.rglob('*'):
        if not p.is_file():continue
        rel=p.relative_to(ROOT)
        if any(x in ['.git','.cache','bin','obj','build','work','__pycache__'] for x in rel.parts):continue
        sources.append((p,'WinMim/'+rel.as_posix()))
    packages=[archive(OUT/'WinMim-0.1.0-portable.zip',portable),archive(OUT/'WinMim-0.1.0-source.zip',sources)]
    (OUT/'WinMim-0.1.0-checksums.json').write_text(json.dumps(packages,indent=2),encoding='utf-8')
    print(json.dumps(packages,indent=2))

if __name__=='__main__':package()


