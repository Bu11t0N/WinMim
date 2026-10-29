"""Register original WinMim modules without modifying the pinned upstream collection."""
from prepare import ROOT,sha

def register(catalog):
    source=ROOT/'src/WinMim.Native/winmim-shell-behavior.wh.cpp'
    module={'id':'winmim-shell-behavior','name':'WinMim: классические переходы оболочки',
      'description':'Свойства панели задач и классические страницы вместо выбранных переходов в Параметры.',
      'category':'Поведение','author':'WinMim contributors','version':'0.1.0','license':'GPL-3.0',
      'sourceUrl':'src/WinMim.Native/winmim-shell-behavior.wh.cpp',
      'sourcePath':'src/WinMim.Native/winmim-shell-behavior.wh.cpp','sourceSha256':sha(source),
      'includes':['explorer.exe','StartMenu.exe','WinMim.ShellProbe.exe'],'excludes':[], 'architecture':['x86-64'],
      'defaultSettings':{'controller':'','useOwnTaskbarDialog':'1'},'criticalProcess':False,'requiresResources':False,
      'conflicts':[],'status':'source','binaries':[], 'compilerOptions':'-lshell32'}
    previous=next((m for m in catalog['modules'] if m['id']==module['id']),None)
    if previous and previous['sourceSha256']==module['sourceSha256']:
        module['status']=previous['status'];module['binaries']=previous['binaries']
    catalog['modules']=[m for m in catalog['modules'] if m['id']!=module['id']]+[module]
    for profile in catalog['profiles']:
        if profile['id']=='seven' and module['id'] not in profile['modules']:
            profile['modules'].append(module['id'])
        if profile['id']=='seven':
            available={m['id'] for m in catalog['modules']}
            extras=['settings-to-control-panel','classic-taskbar-properties','win7-network-flyout-recreation',
                'win7-open-with-dialog','win7-legacy-applet-restorer','win7-action-center-recreation','win7-display-control-panel-restorer']
            for extra in extras:
                if extra in available and extra not in profile['modules']:profile['modules'].append(extra)
            defaults={
                'settings-to-control-panel':{'RedirectSystemTray':'1','RedirectDisplayPages':'0','FallbackMode':'2'},
                'win7-action-center-recreation':{'enableNotificationSimulation':'0','enableHotkey':'0','theme':'light'},
                'win7-network-flyout-recreation':{'enableHotkey':'0','theme':'light'},
                'win7-open-with-dialog':{'defaultAssociationBehavior':'disabled'}}
            for name,settings in defaults.items():
                if name in available:
                    target=profile.setdefault('settings',{}).setdefault(name,{})
                    for key,value in settings.items():target.setdefault(key,value)

    classic95=['winmim-shell-behavior','classic-taskbar-buttons-lite','aerexplorer','classic-theme-explorer-lite','classic-theme-transparency-fix','syslistview32-enabler','remove-command-bar','disable-navigation-bar','classic-uwp-fix','eradicate-immersive-menus','legacy-file-copy','old-this-pc-commands','msg-box-font-fix']
    if all(any(m['id']==id for m in catalog['modules']) for id in classic95):
        catalog['profiles']=[p for p in catalog['profiles'] if p['id']!='windows95']+[{'id':'windows95','name':'Windows 95 (стенд)','accent':'#008080','description':'Классическая отрисовка, системные метрики и исправления Explorer. Требуется отдельно включённый ClassicSession.','modules':classic95,'settings':{'aerexplorer':{'noribbon':'1','hidetitle':'0','detailspane':'0'},'classic-theme-transparency-fix':{'removeDialogTransparency':'1'}}}]
