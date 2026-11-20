using System.Globalization;
using System.Runtime.Versioning;
namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public static class Classic95Profile
{
    public const string PrivateIcons=@"C:\AeroForgeLab\PrivateAssets\Windows95Icons";
    public static bool HasOriginalIcons=>Directory.Exists(PrivateIcons);
    public const string ColorsKey=@"HKCU\Control Panel\Colors",MetricsKey=@"HKCU\Control Panel\Desktop\WindowMetrics";
    private static readonly string[] MetricNames=["CaptionFont","SmCaptionFont","MenuFont","StatusFont","MessageFont","IconFont","BorderWidth","PaddedBorderWidth","ScrollWidth","ScrollHeight","CaptionWidth","CaptionHeight","SmCaptionWidth","SmCaptionHeight","MenuWidth","MenuHeight"];
    private static readonly string[] ShellNames=["MenuCaption","SkinOptionsC1","MenuItems1","RecentPrograms","PinnedPrograms","PinnedItemsPath","SmallIconSize","SkinC1","SkinVariationC1","SearchBox","TaskbarTexture","MenuStyle","EnableStartButton","MouseClick","WinKey","CustomTaskbar","TaskbarLook","TaskbarOpacity","TaskbarColor","TaskbarTextColor","StartButtonText","StartButtonIcon","StartButtonIconSize","StartButtonType","StartButtonPath","StartButtonSize","SkipMetro","StartScreenShortcut","RecentMetroApps","GlassOverride","EnableGlass","SkinC2","SkinOptionsC2","SkinVariationC2","MenuItems2","UserNameCommand","UserPictureCommand"];
    public static bool Allows(Change change)=>change.Kind==ChangeKind.Registry &&
        (change.Target==ColorsKey && ClassicAppearance.ColorNames.Contains(change.ValueName)
        ||change.Target==MetricsKey && MetricNames.Contains(change.ValueName)
        ||change.Target==DesktopRegistryPolicy.Shell && ShellNames.Contains(change.ValueName)
        ||change.Target==DesktopRegistryPolicy.Advanced && new[]{"TaskbarGlomLevel","MMTaskbarGlomLevel","TaskbarSmallIcons","ListviewShadow","LaunchTo","AlwaysShowMenus","AutoCheckSelect","ShowTaskViewButton","TaskbarSizeMove","DisablePreviewDesktop"}.Contains(change.ValueName)
        ||change.Target==DesktopRegistryPolicy.Computer && change.ValueName=="LocalizedString");
    public static Plan Build(string content,string root,ClassicAppearanceState original,string? privateIcons=null,string target="windows-95",XpThemePack? xpPack=null)
    {
        var theme=ClassicThemes.Get(target);
        var xp=theme.Xp?xpPack??XpThemePack.ReadDefault(content):null;
        var desired=ClassicAppearance.Desired(original,target);
        var plan=new Plan{ProfileId=target=="windows-95"?"desktop-windows95":"desktop-"+target,ProfileName=theme.Name+" — экспериментальный профиль",Exclusive=true};
        if(xp!=null)
        {
            plan.Changes.AddRange(xp.Stage());
            plan.Changes.AddRange(XpDesktopResources.Build(xp.Manifest,root));
            plan.Changes.Add(new(){Kind=ChangeKind.VisualStyle,Target="Windows.VisualStyle",ValueKind="VisualStyle",Value=xp.Style.Encode(),Description="Визуальный стиль Luna Blue (личный пакет)"});
        }
        void Set(string key,string name,string value,string kind="String")=>plan.Changes.Add(new(){Kind=ChangeKind.Registry,Target=key,ValueName=name,ValueKind=kind,Value=value,Description=theme.Name+": "+name});
        for(int i=0;i<ClassicAppearance.ColorNames.Length;i++)Set(ColorsKey,ClassicAppearance.ColorNames[i],ClassicAppearance.RgbText(desired.Colors[i]));
        plan.Changes.AddRange(theme.Xp?XpAppearance.PersistentMetrics(desired,XpAppearance.SystemDpi):ClassicAppearance.PersistentMetrics(desired));
        foreach(var (name,value) in new[]{("MenuStyle","Classic1"),("SkinC1","Classic Skin"),("SkinVariationC1",""),("SearchBox","Hide"),("TaskbarLook","Opaque"),("TaskbarTexture",""),("StartButtonType","ClasicButton"),("StartButtonText","Пуск"),("StartButtonIcon",@"%SystemRoot%\System32\slui.exe, 2"),("StartButtonPath",""),("MouseClick","ClassicMenu"),("WinKey","ClassicMenu")})Set(DesktopRegistryPolicy.Shell,name,value);
        Set(DesktopRegistryPolicy.Shell,"MenuCaption",theme.Name);
        Set(DesktopRegistryPolicy.Shell,"RecentPrograms","0","DWord");
        Set(DesktopRegistryPolicy.Shell,"PinnedPrograms","PinnedItems");
        Set(DesktopRegistryPolicy.Shell,"PinnedItemsPath",SafePath.Under(root,theme.Xp?"Pinned":"EmptyPinned"));
        Set(DesktopRegistryPolicy.Shell,"SmallIconSize","16","DWord");
        Set(DesktopRegistryPolicy.Shell,"SkinOptionsC1",System.Text.Json.JsonSerializer.Serialize(new[]{"CAPTION=1","USER_IMAGE=0","USER_NAME=0","SMALL_ICONS=1","THICK_BORDER=1","SOLID_SELECTION=1"}),"MultiString");
        string[] menu=["Items=ProgramsMenu, DocumentsMenu, SettingsMenu, FindMenu, HelpItem, RunItem, SEPARATOR, ShutdownItem",
            "ProgramsMenu.Command=programs","ProgramsMenu.Label=Программы","ProgramsMenu.Icon=shell32.dll,326",
            "DocumentsMenu.Command=recent_documents","DocumentsMenu.Label=Документы","DocumentsMenu.Icon=shell32.dll,327",
            "SettingsMenu.Command=settings","SettingsMenu.Label=Настройка","SettingsMenu.Items=ControlPanelItem, PrintersItem, TaskbarItem","SettingsMenu.Icon=shell32.dll,330",
            "ControlPanelItem.Command=control_panel","ControlPanelItem.Label=Панель управления",
            "PrintersItem.Command=printers","PrintersItem.Label=Принтеры",
            "TaskbarItem.Command=taskbar_settings","TaskbarItem.Label=Панель задач",
            "FindMenu.Command=search","FindMenu.Label=Найти","FindMenu.Icon=shell32.dll,323",
            "HelpItem.Command=help","HelpItem.Label=Справка","HelpItem.Icon=imageres.dll,99",
            "RunItem.Command=run","RunItem.Label=Выполнить…","RunItem.Icon=shell32.dll,328",
            "ShutdownItem.Command=shutdown_box","ShutdownItem.Label=Завершение работы…","ShutdownItem.Icon=shell32.dll,329"];
        if(theme.QuickLaunch)
            menu=menu.Select(line=>line.StartsWith("Items=")?line.Replace("DocumentsMenu, SettingsMenu","DocumentsMenu, FavoritesItem, SettingsMenu"):line)
                .Concat(new[]{"FavoritesItem.Command=favorites","FavoritesItem.Label=Избранное"}).ToArray();
        if(theme.Xp)
            menu=menu.Where(line=>!line.StartsWith("Items=")).Concat(new[]{
                "Items=COLUMN_PADDING, ProgramsMenu, SEPARATOR, RunItem, COLUMN_BREAK, UserDocumentsItem, UserPicturesItem, UserMusicItem, ComputerItem, SettingsMenu, FindMenu, HelpItem, SEPARATOR, LogOffItem, ShutdownItem",
                "UserDocumentsItem.Command=user_documents","UserDocumentsItem.Label=Мои документы",
                "UserPicturesItem.Command=user_pictures","UserPicturesItem.Label=Мои рисунки",
                "UserMusicItem.Command=user_music","UserMusicItem.Label=Моя музыка",
                "ComputerItem.Command=computer","ComputerItem.Label=Мой компьютер",
                "LogOffItem.Command=logoff","LogOffItem.Label=Выход из системы…"}).ToArray();
        string iconDirectory=privateIcons??(theme.Xp?XpThemePack.PrivateAssets:PrivateIcons);
        bool originals=theme.Xp||Directory.Exists(iconDirectory);
        var manifest=xp!=null?new AssetManifest{Icons=xp.Manifest.Icons,Files=xp.Manifest.Files.Select(f=>new Asset{Name=f.Name,Sha256=f.Sha256}).ToArray()}:JsonFile.Read<AssetManifest>(SafePath.Under(content,originals?"classic95/original-reference.json":"classic95/sources.json"));
        var icons=originals?manifest.Icons:new Dictionary<string,string>{{"computer","mycomputer_1_G3M_icon.ico"},{"documents","mydocuments_vlJ_icon.ico"},{"network","network_1_jsM_icon.ico"},{"recycle-empty","bin1_1_9fQ_icon.ico"},{"recycle-full","bin2_1_3l0_icon.ico"}};
        foreach(string role in new[]{"computer","documents","network","recycle-empty","recycle-full"})
            if(!icons.TryGetValue(role,out string? name)||!manifest.Files.Any(f=>f.Name==name))throw new InvalidDataException("Неполный набор значков "+theme.Name+": "+role);
        if(originals)
        {
            var menuIcons=new Dictionary<string,string>{{"ProgramsMenu","programs"},{"DocumentsMenu","documents"},{"SettingsMenu","settings"},{"FindMenu","search"},{"HelpItem","help"},{"RunItem","run"},{"ShutdownItem","shutdown"}};
            menu=menu.Select(line=>{int at=line.IndexOf(".Icon=",StringComparison.Ordinal);return at>0&&menuIcons.TryGetValue(line[..at],out string? role)?line[..(at+6)]+SafePath.Under(root,"Assets/"+icons[role])+",0":line;}).ToArray();
            menu=menu.Concat(new[]{"PrintersItem.Icon="+SafePath.Under(root,"Assets/"+icons["printers"])+",0","ControlPanelItem.Icon="+SafePath.Under(root,"Assets/"+icons["settings"])+",0"}).ToArray();
            plan.Changes.Single(c=>c.Kind==ChangeKind.Registry&&c.Target==DesktopRegistryPolicy.Shell&&c.ValueName=="StartButtonIcon").Value=SafePath.Under(root,"Assets/"+icons["start"])+",0";
            foreach(string clsid in new[]{"{26EE0668-A00A-44D7-9371-BEB064C98683}","{21EC2020-3AEA-1069-A2DD-08002B30309D}","{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}"})
                foreach(string prefix in new[]{@"HKCU\Software\Classes\CLSID\",@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\"})Set(prefix+clsid+@"\DefaultIcon","",SafePath.Under(root,"Assets/"+icons["settings"])+",0");
        }
        Set(DesktopRegistryPolicy.Shell,theme.Xp?"MenuItems2":"MenuItems1",System.Text.Json.JsonSerializer.Serialize(menu),"MultiString");
        if(theme.Xp){
            Set(DesktopRegistryPolicy.Advanced,"LaunchTo","1","DWord");
            Set(DesktopRegistryPolicy.Advanced,"AlwaysShowMenus","1","DWord");
            Set(DesktopRegistryPolicy.Advanced,"ShowStatusBar","1","DWord");
            Set(DesktopRegistryPolicy.Advanced,"AutoCheckSelect","0","DWord");
            plan.Changes.Single(c=>c.Target==DesktopRegistryPolicy.Shell&&c.ValueName=="MenuStyle").Value="Classic2";
            Set(DesktopRegistryPolicy.Shell,"SkinC2","Windows XP Luna");Set(DesktopRegistryPolicy.Shell,"SkinVariationC2","");
            if(xp?.Manifest.MenuSkin is string menuSkin)
            {
                string skins=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Open-Shell","Skins");
                string path=SafePath.Under(root,"XP/"+menuSkin);
                plan.Changes.Single(c=>c.Target==DesktopRegistryPolicy.Shell&&c.ValueName=="SkinC2").Value=Path.GetRelativePath(skins,Path.ChangeExtension(path,null));
            }
            Set(DesktopRegistryPolicy.Shell,"SkinOptionsC2",System.Text.Json.JsonSerializer.Serialize(new[]{"USER_IMAGE=1","USER_NAME=1","SMALL_ICONS=0","NO_ICONS=0","LARGE_FONT=0"}),"MultiString");
            Set(DesktopRegistryPolicy.Shell,"UserNameCommand","control.exe /name Microsoft.UserAccounts");
            Set(DesktopRegistryPolicy.Shell,"UserPictureCommand","control.exe /name Microsoft.UserAccounts");
        }
        foreach(var (name,value) in new[]{("EnableStartButton",target=="windows-95"?1:0),("CustomTaskbar",target=="windows-95"?1:0),("TaskbarOpacity",100),("TaskbarColor",theme.Face),("TaskbarTextColor",0),("StartButtonSize",0),("StartButtonIconSize",16),("SkipMetro",1),("StartScreenShortcut",0),("RecentMetroApps",0),("GlassOverride",1),("EnableGlass",0)})Set(DesktopRegistryPolicy.Shell,name,value.ToString(CultureInfo.InvariantCulture),"DWord");
        foreach(var (name,value) in new[]{("TaskbarGlomLevel",2),("MMTaskbarGlomLevel",2),("TaskbarSmallIcons",1),("ListviewShadow",0),("ShowTaskViewButton",0),("TaskbarSizeMove",0),("DisablePreviewDesktop",1)})Set(DesktopRegistryPolicy.Advanced,name,value.ToString(CultureInfo.InvariantCulture),"DWord");
        Set(DesktopRegistryPolicy.Computer,"LocalizedString","Мой компьютер");
        foreach(var asset in manifest.Files)
        {
            if(!asset.Name.EndsWith(".ico",StringComparison.OrdinalIgnoreCase))continue;
            string file=originals?SafePath.Under(iconDirectory,asset.Name):SafePath.Under(content,"classic95/"+asset.Name);byte[] bytes=File.ReadAllBytes(file);
            if(Hashing.Bytes(bytes)!=asset.Sha256)throw new InvalidDataException("Ресурс "+theme.Name+" изменён: "+asset.Name);
            if(bytes.Length<22||bytes[0]!=0||bytes[1]!=0||bytes[2]!=1||bytes[3]!=0)throw new InvalidDataException("Неверный ICO: "+asset.Name);
            plan.Changes.Add(new(){Kind=ChangeKind.File,Target="Assets/"+asset.Name,Content=bytes,Description="Классический ресурс: "+asset.Name});
        }
        foreach(var (clsid,file,name) in new[]{("{20D04FE0-3AEA-1069-A2D8-08002B30309D}",icons["computer"],""),("{59031A47-3F72-44A7-89C5-5595FE6B30EE}",icons["documents"],""),("{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}",icons["network"],""),("{645FF040-5081-101B-9F08-00AA002F954E}",icons["recycle-empty"],"empty"),("{645FF040-5081-101B-9F08-00AA002F954E}",icons["recycle-full"],"full")})
            foreach(var prefix in new[]{@"HKCU\Software\Classes\CLSID\",@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\"})Set(prefix+clsid+@"\DefaultIcon",name,SafePath.Under(root,"Assets/"+file)+",0");
        plan.Changes.Add(new(){Kind=ChangeKind.Wallpaper,Target="Windows.Wallpaper",ValueKind="Wallpaper",Value=xp?.Manifest.Wallpaper is string wallpaper?SafePath.Under(root,"XP/"+wallpaper).ToUpperInvariant():"",Description="Фон рабочего стола "+theme.Name});
        plan.Changes.Add(new(){Kind=theme.Xp?ChangeKind.XpAppearance:ChangeKind.ClassicAppearance,Target="Windows.ClassicAppearance",ValueKind="ClassicAppearance",Value=ClassicAppearance.Serialize(desired),Description="Палитра, системные шрифты и метрики "+theme.Name});
        plan.Changes.Add(new(){Kind=ChangeKind.File,Target="classic-appearance.json",Content=System.Text.Encoding.UTF8.GetBytes(ClassicAppearance.Serialize(desired)),Description="Состояние Classic для возобновления"});
        if(target!="windows-95"){
            RetroBarHost.AddFiles(plan,content);
            string system=Environment.GetFolderPath(Environment.SpecialFolder.System);
            foreach(var (label,file) in new[]{("Проводник","explorer.exe"),("Блокнот","notepad.exe")}){
                string executable=Path.Combine(file=="explorer.exe"?Environment.GetFolderPath(Environment.SpecialFolder.Windows):system,file);
                if(File.Exists(executable))
                {
                    string? icon=theme.Xp?SafePath.Under(root,"Assets/"+icons.GetValueOrDefault(file=="explorer.exe"?"folder-closed":"notepad",icons["programs"])):null;
                    string? browser = theme.Xp && file == "explorer.exe" ? ProfilePlanner.FindController() : null;
                    byte[] shortcut = DesktopShortcuts.Create(browser ?? executable,
                        arguments: browser is null ? "" : "--retro-files", iconLocation: icon);
                    plan.Changes.Add(new(){Kind=ChangeKind.File,Target="QuickLaunch/"+label+".lnk",Content=shortcut,Description="Быстрый запуск: "+label});
                    if(theme.Xp)plan.Changes.Add(new(){Kind=ChangeKind.File,Target="Pinned/"+label+".lnk",Content=shortcut,Description="Закреплённая рабочая команда XP: "+label});
                }
            }
        }
        return plan;
    }
    private sealed class AssetManifest{public Asset[] Files{get;set;}=[];public Dictionary<string,string> Icons{get;set;}=[];}
    private sealed class Asset{public string Name{get;set;}="";public string Sha256{get;set;}="";}
}
