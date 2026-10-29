"""Prepare reproducible native dependencies without installing or starting them.

Requires Python 3.10+, PyYAML 6.0.3 and an existing trusted 7-Zip.
Downloads are immutable releases; the downloaded installer is parsed only by 7-Zip.
"""
from pathlib import Path
import argparse,hashlib,json,re,shutil,subprocess,urllib.request,zipfile
import yaml

ROOT=Path(__file__).resolve().parents[1]
COMMIT='3f11069419fad9103a56cf78b28d293fee6c20cf'
WH_VERSION='1.7.3'
WH_SHA='4d93016570f982326eebdfc9068e924ff21f6448534ea32672bb4c1d52a8193b'
WH_URL='https://github.com/ramensoftware/windhawk/releases/download/v1.7.3/windhawk_setup_offline.exe'
WH_SOURCE_SHA='7dcce5a390081b8eb241a4661f306e8b3e56b85f26def5aac4b36385783d855f'
CONTENT=ROOT/'content'

DESCRIPTIONS={
'classic-taskbar-buttons-lite':('Панель задач','Объёмные кнопки классической панели задач.'),
'classic-theme-explorer-lite':('Оформление','Исправления классической панели задач Windows 10 и рамок Проводника.'),
'syslistview32-enabler':('Проводник','Классический список SysListView32 вместо современного представления.'),
'remove-command-bar':('Проводник','Убрать командную панель Vista/7 для исторического оформления.'),
'disable-navigation-bar':('Проводник','Убрать современную навигационную панель.'),
'classic-theme-transparency-fix':('Оформление','Исправления прозрачности окон в классическом режиме.'),

'settings-to-control-panel':('Поведение','Переходы к классическим страницам панели управления.'),
'classic-taskbar-properties':('Панель задач','Классические свойства панели и меню «Пуск» из upstream; самостоятельный дополнительный компонент.'),
'win7-network-flyout-recreation':('Трей','Сетевое окно в стиле Windows 7.'),
'win7-open-with-dialog':('Диалоги','Классическое окно «Открыть с помощью».'),
'win7-legacy-applet-restorer':('Панель управления','Восстановление доступных классических апплетов.'),
'win7-action-center-recreation':('Трей','Классический центр поддержки с информацией о состоянии системы.'),
'win7-display-control-panel-restorer':('Панель управления','Классические страницы экрана и разрешения.'),
'aerexplorer':('Проводник','Панели, адресная строка и другие элементы классического Проводника.'),
'icon-resource-redirect':('Ресурсы','Подмена иконок и ресурсов через отдельный пакет оформления.'),
'aero-flyout-fix':('Трей','Оформление всплывающих системных окон в стиле Aero.'),
'aero-tray':('Трей','Классическое оформление области уведомлений.'),
'win32-tray-clock-experience':('Трей','Классическое окно часов Windows.'),
'windows-7-clock-spacing':('Трей','Отступы часов и области уведомлений в стиле Windows 7.'),
'legacy-file-copy':('Диалоги','Классический интерфейс копирования файлов.'),
'old-this-pc-commands':('Проводник','Классические команды раздела «Этот компьютер».'),
'msg-box-font-fix':('Диалоги','Шрифты и оформление стандартных сообщений.'),
'eradicate-immersive-menus':('Меню','Классические контекстные меню Win32.'),
'transparent-screensaver-fix':('Система','Исправление прозрачности экранных заставок.'),
'win7-style-uac-dim':('Система','Затемнение окна UAC в стиле Windows 7.'),
'win7-alttab-loader':('Переключение окон','Загрузка старого Alt+Tab; нужен отдельно проверенный ресурс AltTab.dll.'),
'basic-themer':('Оформление','Оформление окон Basic через DWM. Требует испытаний в VM.'),
'classic-uwp-fix':('Оформление','Исправления окон UWP для классического стиля.'),
'uifile-override':('Ресурсы','Подмена UIFILE через пакет ресурсов.'),
'disable-office-hotkeys':('Поведение','Отключение зарезервированных сочетаний клавиш Office.'),
'classic-theme-enable':('Оформление','Включение классической темы через системный процесс. Требует VM.'),
'classic-menus':('Меню','Классическое оформление стандартных меню.'),
'taskbar-show-desktop-button-aero-peek':('Панель задач','Поведение кнопки «Показать рабочий стол» и Aero Peek.'),
'classic-explorer-statusbar':('Проводник','Классическая строка состояния Проводника.'),
'classic-explorer-treeview':('Проводник','Классическая панель дерева каталогов.'),
'control-panel-revival':('Панель управления','Возвращение доступных классических страниц панели управления.')}

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def dump(path,data):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def download(url,path,expected=None):
    path.parent.mkdir(parents=True,exist_ok=True)
    if path.exists() and (expected is None or sha(path)==expected):return
    tmp=path.with_name(path.name+'.download')
    try:
        with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'AeroForge/0.1.0'}),timeout=120) as src,tmp.open('wb') as dst:
            shutil.copyfileobj(src,dst,1024*1024)
        if expected and sha(tmp)!=expected:raise ValueError('SHA256 mismatch: '+url)
        tmp.replace(path)
    finally:
        if tmp.exists():tmp.unlink()

def flatten_settings(v,prefix=''):
    result={}
    if isinstance(v,dict):
        for k,x in v.items():
            if str(k).startswith('$'):continue
            result.update(flatten_settings(x,prefix+'.'+str(k) if prefix else str(k)))
    elif isinstance(v,list):
        for i,x in enumerate(v):result.update(flatten_settings(x,f'{prefix}[{i}]' if prefix else ''))
    elif prefix:
        result[prefix]=str(int(v)) if isinstance(v,bool) else str(v) if v is not None else ''
    return result

def metadata(p):
    text=p.read_text(encoding='utf-8-sig');fields={}
    header=re.search(r'// ==WindhawkMod==(.*?)// ==/WindhawkMod==',text,re.S)
    if not header:raise ValueError('Missing Windhawk metadata header: '+str(p))
    for k,v in re.findall(r'^\s*//\s*@(\w+)\s+([^\r\n]*)',header[1],re.M):fields.setdefault(k,[]).append(v.strip())
    id=fields['id'][0];category,description=DESCRIPTIONS[id]
    explicit=re.search(r'Source code is published under (?:The )?([^\r\n]+)',text,re.I)
    license=fields.get('license',[explicit[1].rstrip('.') if explicit else 'MIT (Windhawk mods contribution policy)'])[0]
    defaults={}
    match=re.search(r'// ==WindhawkModSettings==\s*/\*(.*?)\*/',text,re.S)
    if match:defaults=flatten_settings(yaml.safe_load(match[1]))
    includes=fields.get('include',[])
    return {'id':id,'name':fields.get('name',[id])[0],'description':description,'category':category,
        'author':fields.get('author',[''])[0],'version':fields['version'][0],'license':license,
        'sourceUrl':f'https://github.com/ramensoftware/windhawk-mods/blob/{COMMIT}/mods/{p.name}',
        'sourceSha256':sha(p),'includes':includes,'excludes':fields.get('exclude',[]),
        'architecture':fields.get('architecture',[]),'defaultSettings':defaults,
        'criticalProcess':any(x.lower() in ['dwm.exe','winlogon.exe','consent.exe','logonui.exe'] for x in includes),
        'requiresResources':id in ['icon-resource-redirect','uifile-override','win7-alttab-loader','win7-display-control-panel-restorer'],
        'conflicts':['classic-menus'] if id=='eradicate-immersive-menus' else ['eradicate-immersive-menus'] if id=='classic-menus' else [],
        'status':'source','binaries':[],'compilerOptions':fields.get('compilerOptions',[''])[0]}

def prepare(toolchain=None,installer=None,seven_zip=None):
    cache=ROOT/'.cache';cache.mkdir(exist_ok=True)
    toolchain=Path(toolchain) if toolchain else cache/'windhawk'
    if not (toolchain/'Compiler/bin/clang++.exe').exists():
        install=Path(installer) if installer else cache/'windhawk_setup_offline.exe'
        if not install.exists():download(WH_URL,install,WH_SHA)
        if sha(install)!=WH_SHA:raise ValueError('Windhawk installer checksum mismatch')
        seven_zip=seven_zip or shutil.which('7z') or r'C:\Program Files\7-Zip\7z.exe'
        subprocess.run([seven_zip,'x',str(install),'-o'+str(toolchain),'-i!Compiler\\*','-i!Engine\\*',
            '-i!windhawk.exe','-i!windhawk-x64-helper.exe','-i!LICENSE*','-r','-y','-bso0','-bsp0'],check=True)
    # Installer uses a literal $R1 placeholder. Copy without interpreting it as a shell variable.
    engine=toolchain/'Engine/$R1'
    if not engine.exists():engine=toolchain/'Engine'/WH_VERSION
    runtime=CONTENT/'runtime';runtime.mkdir(parents=True,exist_ok=True)
    files=[]
    def stage(src,target):
        dst=runtime/target;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst)
        files.append({'path':'runtime/'+target,'target':target,'sha256':sha(dst)})
    stage(toolchain/'windhawk.exe','windhawk.exe')
    helper=toolchain/'windhawk-x64-helper.exe'
    if helper.exists():stage(helper,'windhawk-x64-helper.exe')
    for arch,triple in [('32','i686-w64-mingw32'),('64','x86_64-w64-mingw32')]:
        for p in (engine/arch).iterdir():
            if p.suffix.lower() in ['.dll','.yes']:stage(p,f'Engine/{WH_VERSION}/{arch}/{p.name}')
        for original,new in [('libc++.dll','libc++.whl'),('libunwind.dll','libunwind.whl'),('windhawk-mod-shim.dll','windhawk-mod-shim.dll')]:
            stage(toolchain/f'Compiler/{triple}/bin/{original}',f'Data/Engine/Mods/{arch}/{new}')
    source_archive=ROOT/'vendor/sources/windhawk-v1.7.3.zip'
    download('https://codeload.github.com/ramensoftware/windhawk/zip/refs/tags/v1.7.3',source_archive,WH_SOURCE_SHA)
    with zipfile.ZipFile(source_archive) as z:
        name=next(n for n in z.namelist() if n.count('/')==1 and n.endswith('/LICENSE'))
        license_bytes=z.read(name)
    (ROOT/'LICENSE').write_bytes(license_bytes)
    licenses=CONTENT/'licenses';licenses.mkdir(exist_ok=True)
    (licenses/'Windhawk-GPL-3.0.txt').write_bytes(license_bytes)
    shutil.copyfile(toolchain/'Compiler/LICENSE.TXT',licenses/'LLVM-LICENSE.txt')
    download(f'https://raw.githubusercontent.com/ramensoftware/windhawk-mods/{COMMIT}/README.md',ROOT/'vendor/windhawk-mods/UPSTREAM-README.md')
    mods=[metadata(p) for p in sorted((ROOT/'vendor/windhawk-mods').glob('*.wh.cpp'))]
    existing_path=CONTENT/'catalog.json'
    if existing_path.exists():
        old={m['id']:m for m in json.loads(existing_path.read_text(encoding='utf-8'))['modules']}
        for m in mods:
            previous=old.get(m['id'],{})
            binaries=previous.get('binaries',[])
            if previous.get('sourceSha256')==m['sourceSha256'] and binaries and all((CONTENT/b['path']).is_file() and sha(CONTENT/b['path'])==b['sha256'] for b in binaries):
                m['status']=previous['status'];m['binaries']=binaries
    common=['aerexplorer','eradicate-immersive-menus','msg-box-font-fix','old-this-pc-commands',
        'win32-tray-clock-experience','windows-7-clock-spacing','aero-tray','aero-flyout-fix',
        'disable-office-hotkeys','legacy-file-copy','taskbar-show-desktop-button-aero-peek']
    profiles=[
        {'id':'seven','name':'Seven','accent':'#69BCEF','description':'Классическая оболочка Windows 7. Визуальный стиль и Aero Glass подключаются отдельно.','modules':common},
        {'id':'vista','name':'Vista','accent':'#7DC8B3','description':'Профиль оболочки Vista; точные темы и ресурсы ещё требуют открытого пакета.','modules':[x for x in common if x!='windows-7-clock-spacing']},
        {'id':'basic','name':'Basic','accent':'#7F9BD1','description':'Экспериментальный профиль Basic. Системный модуль DWM требует отдельной проверки.','modules':common+['basic-themer']},
        {'id':'classic','name':'Classic','accent':'#B8B9C7','description':'Экспериментальный Classic. Системный модуль winlogon требует отдельной проверки.','modules':[x for x in common if x not in ['aero-tray','aero-flyout-fix','eradicate-immersive-menus']]+['classic-theme-enable','classic-menus','classic-uwp-fix']}]
    deps=[
        {'id':'open-shell','name':'Open-Shell','version':'4.4.198','license':'MIT','description':'Классическое меню «Пуск». Устанавливается официальным установщиком отдельно; AeroForge не удаляет его при откате профиля.',
         'sourceUrl':'https://github.com/Open-Shell/Open-Shell-Menu','downloadUrl':'https://github.com/Open-Shell/Open-Shell-Menu/releases/download/v4.4.198/OpenShellSetup_4_4_198.exe',
         'sha256':'a4d2d4459de55b5e962ba2a14f7bb794170511649138173dfa72949837b48c3f','fileName':'OpenShellSetup_4_4_198.exe'},
        {'id':'secureuxtheme','name':'SecureUxTheme','version':'4.0.0','license':'LGPL-2.1','description':'Подключение сторонних визуальных стилей. Изменения установщика управляются средствами Windows Installer.',
         'sourceUrl':'https://github.com/namazso/SecureUxTheme','downloadUrl':'https://github.com/namazso/SecureUxTheme/releases/download/v4.0.0/SecureUxTheme_x64.msi',
         'sha256':'af6ab67e0a283a0138b827b6731d5bb1f4ffc59e6d905f1c41567eb3305a9537','fileName':'SecureUxTheme_x64.msi'},
        {'id':'openglass','name':'OpenGlass','version':'3.0.2.3749','license':'GPL-3.0','description':'Прозрачность и рамки Aero. Совместимость конкретной ревизии Windows проверяется отдельно.',
         'sourceUrl':'https://github.com/ALTaleX531/OpenGlass','downloadUrl':'https://github.com/ALTaleX531/OpenGlass/releases/download/v3.0.2.3749/OpenGlassSetup.Signed.exe',
         'sha256':'191553cf594af2459631a8d00a48ffe0375868210f5e84c60ff75a668529405f','fileName':'OpenGlassSetup.Signed.exe'}]
    catalog={'schemaVersion':1,'version':'0.1.0','sourceCommit':COMMIT,'modules':mods,'profiles':profiles,'runtimeFiles':files,'dependencies':deps}
    from own_modules import register
    register(catalog)
    dump(CONTENT/'catalog.json',catalog)
    dump(ROOT/'third-party.lock.json',{'windhawk':{'version':WH_VERSION,'url':WH_URL,'sha256':WH_SHA,'sourceArchiveSha256':sha(source_archive)},'mods':{'commit':COMMIT,'files':{m['id']:m['sourceSha256'] for m in mods}},'optionalDependencies':deps})
    dump(cache/'toolchain.json',{'root':str(toolchain.resolve()),'engine':str(engine.resolve())})
    print(f'Prepared {len(mods)} sources and {len(files)} runtime files. No program was installed or started.',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--toolchain');parser.add_argument('--installer');parser.add_argument('--seven-zip')
    a=parser.parse_args();prepare(a.toolchain,a.installer,a.seven_zip)
