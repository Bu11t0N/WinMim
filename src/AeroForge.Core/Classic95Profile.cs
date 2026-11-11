using System.Globalization;
using System.Runtime.Versioning;
namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public static class Classic95Profile
{
    public const string PrivateIcons=@"C:\AeroForgeLab\PrivateAssets\Windows95Icons";
    public const string PrivateWindows2000Icons=@"C:\AeroForgeLab\PrivateAssets\Windows2000";
    public static bool HasOriginalIcons=>Directory.Exists(PrivateIcons);
    public const string ColorsKey=@"HKCU\Control Panel\Colors",MetricsKey=@"HKCU\Control Panel\Desktop\WindowMetrics";
    private static readonly string[] MetricNames=["CaptionFont","SmCaptionFont","MenuFont","StatusFont","MessageFont","IconFont","BorderWidth","PaddedBorderWidth","ScrollWidth","ScrollHeight","CaptionWidth","CaptionHeight","SmCaptionWidth","SmCaptionHeight","MenuWidth","MenuHeight"];
    private static readonly string[] ShellNames=["MenuCaption","SkinOptionsC1","MenuItems1","RecentPrograms","PinnedPrograms","PinnedItemsPath","SmallIconSize","SkinC1","SkinVariationC1","SearchBox","TaskbarTexture","MenuStyle","EnableStartButton","MouseClick","WinKey","CustomTaskbar","TaskbarLook","TaskbarOpacity","TaskbarColor","TaskbarTextColor","StartButtonText","StartButtonIcon","StartButtonIconSize","StartButtonType","StartButtonPath","StartButtonSize","SkipMetro","StartScreenShortcut","RecentMetroApps","GlassOverride","EnableGlass","SkinC2","SkinOptionsC2","SkinVariationC2","MenuItems2"];
    public static bool Allows(Change change)=>change.Kind==ChangeKind.Registry &&
        (change.Target==ColorsKey && ClassicAppearance.ColorNames.Contains(change.ValueName)
        ||change.Target==MetricsKey && MetricNames.Contains(change.ValueName)
        ||change.Target==DesktopRegistryPolicy.Shell && ShellNames.Contains(change.ValueName)
        ||change.Target==DesktopRegistryPolicy.Advanced && new[]{"TaskbarGlomLevel","MMTaskbarGlomLevel","TaskbarSmallIcons","ListviewShadow","ShowTaskViewButton","TaskbarSizeMove","DisablePreviewDesktop"}.Contains(change.ValueName)
        ||change.Target==DesktopRegistryPolicy.Computer && change.ValueName=="LocalizedString");
    public static Plan Build(string content,string root,ClassicAppearanceState original,string? privateIcons=null,string target="windows-95")
    {
        var theme=ClassicThemes.Get(target);
        var desired=ClassicAppearance.Desired(original,target);
        var plan=new Plan{ProfileId=target=="windows-95"?"desktop-windows95":"desktop-"+target,ProfileName=theme.Name+" — экспериментальный профиль",Exclusive=true};
        void Set(string key,string name,string value,string kind="String")=>plan.Changes.Add(new(){Kind=ChangeKind.Registry,Target=key,ValueName=name,ValueKind=kind,Value=value,Description=theme.Name+": "+name});
        for(int i=0;i<ClassicAppearance.ColorNames.Length;i++)Set(ColorsKey,ClassicAppearance.ColorNames[i],ClassicAppearance.RgbText(desired.Colors[i]));
        plan.Changes.AddRange(ClassicAppearance.PersistentMetrics(desired));
        foreach(var (name,value) in new[]{("MenuStyle","Classic1"),("SkinC1","Classic Skin"),("SkinVariationC1",""),("SearchBox","Hide"),("TaskbarLook","Opaque"),("TaskbarTexture",""),("StartButtonType","ClasicButton"),("StartButtonText","Пуск"),("StartButtonIcon",@"%SystemRoot%\System32\slui.exe, 2"),("StartButtonPath",""),("MouseClick","ClassicMenu"),("WinKey","ClassicMenu")})Set(DesktopRegistryPolicy.Shell,name,value);
        Set(DesktopRegistryPolicy.Shell,"MenuCaption",theme.Name);
        if(target=="windows-2000"){
            plan.Changes.Single(c=>c.ValueName=="MenuCaption"&&c.Target==DesktopRegistryPolicy.Shell).Value="Windows 2000 Professional";
            string help="Windows 2000 в WinMim\r\n\r\nПроводник: Назад, Вперёд, Вверх, Папки, Вырезать, Копировать, Вставить, Удалить, Вид и Свойства работают с текущим окном и выделением. В адресной строке введите путь и нажмите Enter.\r\n\r\nОснова системы — Windows 10 LTSC 2021. Справка оригинальной Windows 2000 и поиск людей/сетевых принтеров из старого меню не устанавливаются. Выход и выключение выполняются штатными механизмами Windows.\r\n\r\nДля возврата откройте WinMim и выберите восстановление классического профиля. Чужие изменения сохраняются и отмечаются в журнале.\r\n";
            plan.Changes.Add(new(){Kind=ChangeKind.File,Target="Windows2000-help.txt",Content=System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(help)).ToArray(),Description="Локальная справка WinMim"});
        }
        Set(DesktopRegistryPolicy.Shell,"RecentPrograms","0","DWord");
        Set(DesktopRegistryPolicy.Shell,"PinnedPrograms","PinnedItems");
        Set(DesktopRegistryPolicy.Shell,"PinnedItemsPath",SafePath.Under(root,"EmptyPinned"));
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
        if(target=="windows-2000")menu=menu.Select(line=>line=="HelpItem.Command=help"?"HelpItem.Command=notepad.exe \""+SafePath.Under(root,"Windows2000-help.txt")+"\"":line=="HelpItem.Label=Справка"?"HelpItem.Label=Справка WinMim":line).ToArray();
        if(theme.Xp)
            menu=menu.Where(line=>!line.StartsWith("Items=")).Concat(new[]{
                "Items=COLUMN_PADDING, ProgramsMenu, SEPARATOR, RunItem, COLUMN_BREAK, UserDocumentsItem, UserPicturesItem, UserMusicItem, ComputerItem, SettingsMenu, FindMenu, HelpItem, SEPARATOR, LogOffItem, ShutdownItem",
                "UserDocumentsItem.Command=user_documents","UserDocumentsItem.Label=Мои документы",
                "UserPicturesItem.Command=user_pictures","UserPicturesItem.Label=Мои рисунки",
                "UserMusicItem.Command=user_music","UserMusicItem.Label=Моя музыка",
                "ComputerItem.Command=computer","ComputerItem.Label=Мой компьютер",
                "LogOffItem.Command=logoff","LogOffItem.Label=Выход из системы…"}).ToArray();
        string iconDirectory=privateIcons??(target=="windows-2000"?PrivateWindows2000Icons:PrivateIcons);
        bool originals=Directory.Exists(iconDirectory);
        var manifest=target=="windows-2000"&&originals?JsonFile.Read<AssetManifest>(SafePath.Under(iconDirectory,"manifest.json")):
            JsonFile.Read<AssetManifest>(SafePath.Under(content,originals?"classic95/original-reference.json":"classic95/sources.json"));
        if(target=="windows-2000"&&!originals)plan.Blockers.Add("Для Windows 2000 нужен отдельный проверенный личный набор ресурсов. Подготовьте его tools/import_windows2000_assets.py; ресурсы Windows 95 не являются заменой.");
        if(target=="windows-2000"&&originals&&(manifest.SchemaVersion!=1||manifest.Files.Length is <1 or >256||manifest.Files.Select(f=>f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=manifest.Files.Length||manifest.Files.Any(f=>f.Size is <1 or >4194304)||manifest.Files.Sum(f=>f.Size)>67108864))throw new InvalidDataException("Invalid Windows 2000 asset manifest");
        string assetPrefix=target=="windows-2000"?"Assets2000/":"Assets/";
        var icons=originals?manifest.Icons:new Dictionary<string,string>{{"computer","mycomputer_1_G3M_icon.ico"},{"documents","mydocuments_vlJ_icon.ico"},{"network","network_1_jsM_icon.ico"},{"recycle-empty","bin1_1_9fQ_icon.ico"},{"recycle-full","bin2_1_3l0_icon.ico"}};
        foreach(string role in new[]{"computer","documents","network","recycle-empty","recycle-full"})
            if(!icons.TryGetValue(role,out string? name)||!manifest.Files.Any(f=>f.Name==name))throw new InvalidDataException("Неполный набор значков Windows 95: "+role);
        if(originals)
        {
            var menuIcons=new Dictionary<string,string>{{"ProgramsMenu","programs"},{"DocumentsMenu","documents"},{"SettingsMenu","settings"},{"FindMenu","search"},{"HelpItem","help"},{"RunItem","run"},{"ShutdownItem","shutdown"}};
            menu=menu.Select(line=>{int at=line.IndexOf(".Icon=",StringComparison.Ordinal);return at>0&&menuIcons.TryGetValue(line[..at],out string? role)?line[..(at+6)]+SafePath.Under(root,assetPrefix+icons[role])+",0":line;}).ToArray();
            menu=menu.Concat(new[]{"PrintersItem.Icon="+SafePath.Under(root,assetPrefix+icons["printers"])+",0","ControlPanelItem.Icon="+SafePath.Under(root,assetPrefix+icons["settings"])+",0"}).ToArray();
            plan.Changes.Single(c=>c.Kind==ChangeKind.Registry&&c.Target==DesktopRegistryPolicy.Shell&&c.ValueName=="StartButtonIcon").Value=SafePath.Under(root,assetPrefix+icons["start"])+",0";
            foreach(string clsid in new[]{"{26EE0668-A00A-44D7-9371-BEB064C98683}","{21EC2020-3AEA-1069-A2DD-08002B30309D}","{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}"})
                foreach(string prefix in new[]{@"HKCU\Software\Classes\CLSID\",@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\"})Set(prefix+clsid+@"\DefaultIcon","",SafePath.Under(root,assetPrefix+icons["settings"])+",0");
        }
        Set(DesktopRegistryPolicy.Shell,theme.Xp?"MenuItems2":"MenuItems1",System.Text.Json.JsonSerializer.Serialize(menu),"MultiString");
        if(theme.Xp){
            plan.Changes.Single(c=>c.Target==DesktopRegistryPolicy.Shell&&c.ValueName=="MenuStyle").Value="Classic2";
            Set(DesktopRegistryPolicy.Shell,"SkinC2","Windows XP Luna");Set(DesktopRegistryPolicy.Shell,"SkinVariationC2","");
        }
        foreach(var (name,value) in new[]{("EnableStartButton",target=="windows-95"?1:0),("CustomTaskbar",target=="windows-95"?1:0),("TaskbarOpacity",100),("TaskbarColor",theme.Face),("TaskbarTextColor",0),("StartButtonSize",0),("StartButtonIconSize",16),("SkipMetro",1),("StartScreenShortcut",0),("RecentMetroApps",0),("GlassOverride",1),("EnableGlass",0)})Set(DesktopRegistryPolicy.Shell,name,value.ToString(CultureInfo.InvariantCulture),"DWord");
        foreach(var (name,value) in new[]{("TaskbarGlomLevel",2),("MMTaskbarGlomLevel",2),("TaskbarSmallIcons",1),("ListviewShadow",0),("ShowTaskViewButton",0),("TaskbarSizeMove",0),("DisablePreviewDesktop",1)})Set(DesktopRegistryPolicy.Advanced,name,value.ToString(CultureInfo.InvariantCulture),"DWord");
        Set(DesktopRegistryPolicy.Computer,"LocalizedString","Мой компьютер");
        foreach(var asset in manifest.Files)
        {
            if(!asset.Name.EndsWith(".ico",StringComparison.OrdinalIgnoreCase))continue;
            string file=originals?SafePath.Under(iconDirectory,asset.Name):SafePath.Under(content,"classic95/"+asset.Name);byte[] bytes=File.ReadAllBytes(file);
            if(Hashing.Bytes(bytes)!=asset.Sha256 || target=="windows-2000"&&originals&&bytes.LongLength!=asset.Size)throw new InvalidDataException("Классический ресурс изменён: "+asset.Name);
            if(bytes.Length<22||bytes[0]!=0||bytes[1]!=0||bytes[2]!=1||bytes[3]!=0)throw new InvalidDataException("Неверный ICO: "+asset.Name);
            plan.Changes.Add(new(){Kind=ChangeKind.File,Target=assetPrefix+asset.Name,Content=bytes,Description="Классический ресурс: "+asset.Name});
        }
        foreach(var (clsid,file,name) in new[]{("{20D04FE0-3AEA-1069-A2D8-08002B30309D}",icons["computer"],""),("{59031A47-3F72-44A7-89C5-5595FE6B30EE}",icons["documents"],""),("{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}",icons["network"],""),("{645FF040-5081-101B-9F08-00AA002F954E}",icons["recycle-empty"],"empty"),("{645FF040-5081-101B-9F08-00AA002F954E}",icons["recycle-full"],"full")})
            foreach(var prefix in new[]{@"HKCU\Software\Classes\CLSID\",@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\"})Set(prefix+clsid+@"\DefaultIcon",name,SafePath.Under(root,assetPrefix+file)+",0");
        plan.Changes.Add(new(){Kind=ChangeKind.Wallpaper,Target="Windows.Wallpaper",ValueKind="Wallpaper",Value="",Description="Фон рабочего стола "+theme.Name});
        plan.Changes.Add(new(){Kind=ChangeKind.ClassicAppearance,Target="Windows.ClassicAppearance",ValueKind="ClassicAppearance",Value=ClassicAppearance.Serialize(desired),Description="Палитра, системные шрифты и метрики "+theme.Name});
        plan.Changes.Add(new(){Kind=ChangeKind.File,Target="classic-appearance.json",Content=System.Text.Encoding.UTF8.GetBytes(ClassicAppearance.Serialize(desired)),Description="Состояние Classic для возобновления"});
        if(target!="windows-95"){
            RetroBarHost.AddFiles(plan,content);
            string system=Environment.GetFolderPath(Environment.SpecialFolder.System);
            foreach(var (label,file) in new[]{("Проводник","explorer.exe"),("Блокнот","notepad.exe")}){
                string executable=Path.Combine(file=="explorer.exe"?Environment.GetFolderPath(Environment.SpecialFolder.Windows):system,file);
                if(File.Exists(executable))plan.Changes.Add(new(){Kind=ChangeKind.File,Target="QuickLaunch/"+label+".lnk",Content=DesktopShortcuts.Create(executable),Description="Быстрый запуск: "+label});
            }
        }
        if(target=="windows-2000"){
            if(originals)Windows2000Resources.Add(plan,iconDirectory,root);
            if(originals)foreach(var (role,cls) in new[]{("folder","Folder"),("folder","Directory"),("drive","Drive")})
                Set(@"HKCU\Software\Classes\"+cls+@"\DefaultIcon","",SafePath.Under(root,assetPrefix+icons[role])+",0");
            Windows2000Explorer.Add(plan,root,originals?icons:null);
        }
        return plan;
    }
    private sealed class AssetManifest{public int SchemaVersion{get;set;}public Asset[] Files{get;set;}=[];public Dictionary<string,string> Icons{get;set;}=[];}
    private sealed class Asset{public string Name{get;set;}="";public string Sha256{get;set;}="";public long Size{get;set;}}
}
