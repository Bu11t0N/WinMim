using System.Globalization;
using System.Runtime.Versioning;
namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public static class Classic95Profile
{
    public const string ColorsKey=@"HKCU\Control Panel\Colors",MetricsKey=@"HKCU\Control Panel\Desktop\WindowMetrics";
    private static readonly string[] MetricNames=["CaptionFont","SmCaptionFont","MenuFont","StatusFont","MessageFont","IconFont","BorderWidth","PaddedBorderWidth","ScrollWidth","ScrollHeight","CaptionWidth","CaptionHeight","SmCaptionWidth","SmCaptionHeight","MenuWidth","MenuHeight"];
    private static readonly string[] ShellNames=["MenuCaption","SkinOptionsC1","MenuItems1","RecentPrograms","PinnedPrograms","PinnedItemsPath","SmallIconSize","SkinC1","SkinVariationC1","SearchBox","TaskbarTexture","MenuStyle","EnableStartButton","MouseClick","WinKey","CustomTaskbar","TaskbarLook","TaskbarOpacity","TaskbarColor","TaskbarTextColor","StartButtonText","StartButtonIcon","StartButtonIconSize","StartButtonType","StartButtonPath","StartButtonSize","SkipMetro","StartScreenShortcut","RecentMetroApps","GlassOverride","EnableGlass"];
    public static bool Allows(Change change)=>change.Kind==ChangeKind.Registry &&
        (change.Target==ColorsKey && ClassicAppearance.ColorNames.Contains(change.ValueName)
        ||change.Target==MetricsKey && MetricNames.Contains(change.ValueName)
        ||change.Target==DesktopRegistryPolicy.Shell && ShellNames.Contains(change.ValueName)
        ||change.Target==DesktopRegistryPolicy.Advanced && new[]{"TaskbarGlomLevel","MMTaskbarGlomLevel","TaskbarSmallIcons","ListviewShadow","ShowTaskViewButton","TaskbarSizeMove","DisablePreviewDesktop"}.Contains(change.ValueName)
        ||change.Target==DesktopRegistryPolicy.Computer && change.ValueName=="LocalizedString");
    public static Plan Build(string content,string root,ClassicAppearanceState original)
    {
        var desired=ClassicAppearance.Desired(original);
        var plan=new Plan{ProfileId="desktop-windows95",ProfileName="Windows 95 — экспериментальный профиль",Exclusive=true};
        void Set(string key,string name,string value,string kind="String")=>plan.Changes.Add(new(){Kind=ChangeKind.Registry,Target=key,ValueName=name,ValueKind=kind,Value=value,Description="Windows 95: "+name});
        for(int i=0;i<ClassicAppearance.ColorNames.Length;i++)Set(ColorsKey,ClassicAppearance.ColorNames[i],ClassicAppearance.RgbText(desired.Colors[i]));
        plan.Changes.AddRange(ClassicAppearance.PersistentMetrics(desired));
        foreach(var (name,value) in new[]{("MenuStyle","Classic1"),("SkinC1","Classic Skin"),("SkinVariationC1",""),("SearchBox","Hide"),("TaskbarLook","Opaque"),("TaskbarTexture",""),("StartButtonType","ClasicButton"),("StartButtonText","Пуск"),("StartButtonIcon",@"%SystemRoot%\System32\slui.exe, 2"),("StartButtonPath",""),("MouseClick","ClassicMenu"),("WinKey","ClassicMenu")})Set(DesktopRegistryPolicy.Shell,name,value);
        Set(DesktopRegistryPolicy.Shell,"MenuCaption","Windows 95");
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
        Set(DesktopRegistryPolicy.Shell,"MenuItems1",System.Text.Json.JsonSerializer.Serialize(menu),"MultiString");
        foreach(var (name,value) in new[]{("EnableStartButton",1),("CustomTaskbar",1),("TaskbarOpacity",100),("TaskbarColor",0xC0C0C0),("TaskbarTextColor",0),("StartButtonSize",0),("StartButtonIconSize",16),("SkipMetro",1),("StartScreenShortcut",0),("RecentMetroApps",0),("GlassOverride",1),("EnableGlass",0)})Set(DesktopRegistryPolicy.Shell,name,value.ToString(CultureInfo.InvariantCulture),"DWord");
        foreach(var (name,value) in new[]{("TaskbarGlomLevel",2),("MMTaskbarGlomLevel",2),("TaskbarSmallIcons",1),("ListviewShadow",0),("ShowTaskViewButton",0),("TaskbarSizeMove",0),("DisablePreviewDesktop",1)})Set(DesktopRegistryPolicy.Advanced,name,value.ToString(CultureInfo.InvariantCulture),"DWord");
        Set(DesktopRegistryPolicy.Computer,"LocalizedString","Мой компьютер");
        var manifest=JsonFile.Read<AssetManifest>(SafePath.Under(content,"classic95/sources.json"));
        foreach(var asset in manifest.Files)
        {
            if(!asset.Name.EndsWith(".ico",StringComparison.OrdinalIgnoreCase))continue;
            string file=SafePath.Under(content,"classic95/"+asset.Name);byte[] bytes=File.ReadAllBytes(file);
            if(Hashing.Bytes(bytes)!=asset.Sha256)throw new InvalidDataException("Ресурс Windows 95 изменён: "+asset.Name);
            plan.Changes.Add(new(){Kind=ChangeKind.File,Target="Assets/"+asset.Name,Content=bytes,Description="Классический ресурс: "+asset.Name});
        }
        foreach(var (clsid,file,name) in new[]{("{20D04FE0-3AEA-1069-A2D8-08002B30309D}","mycomputer_1_G3M_icon.ico",""),("{59031A47-3F72-44A7-89C5-5595FE6B30EE}","mydocuments_vlJ_icon.ico",""),("{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}","network_1_jsM_icon.ico",""),("{645FF040-5081-101B-9F08-00AA002F954E}","bin1_1_9fQ_icon.ico","empty"),("{645FF040-5081-101B-9F08-00AA002F954E}","bin2_1_3l0_icon.ico","full")})
            foreach(var prefix in new[]{@"HKCU\Software\Classes\CLSID\",@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\"})Set(prefix+clsid+@"\DefaultIcon",name,SafePath.Under(root,"Assets/"+file)+",0");
        plan.Changes.Add(new(){Kind=ChangeKind.Wallpaper,Target="Windows.Wallpaper",ValueKind="Wallpaper",Value="",Description="Однотонный бирюзовый рабочий стол"});
        plan.Changes.Add(new(){Kind=ChangeKind.ClassicAppearance,Target="Windows.ClassicAppearance",ValueKind="ClassicAppearance",Value=ClassicAppearance.Serialize(desired),Description="Палитра, системные шрифты и метрики Windows 95"});
        plan.Changes.Add(new(){Kind=ChangeKind.File,Target="classic-appearance.json",Content=System.Text.Encoding.UTF8.GetBytes(ClassicAppearance.Serialize(desired)),Description="Состояние Classic для возобновления"});
        return plan;
    }
    private sealed class AssetManifest{public Asset[] Files{get;set;}=[];}
    private sealed class Asset{public string Name{get;set;}="";public string Sha256{get;set;}="";}
}
