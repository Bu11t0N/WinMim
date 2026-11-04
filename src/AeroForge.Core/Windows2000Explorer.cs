using System.Text.Json;

namespace AeroForge.Core;

// Configuration for the pinned Open-Shell Explorer BHO, never an archive DLL.
public static class Windows2000Explorer
{
    public static void EnsureInstalled()
    {
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Open-Shell");
        string dll=Path.Combine(directory,"ClassicExplorer64.dll");
        if(!File.Exists(dll)||Hashing.File(dll)!="3f9d0c83723848a479ba9e783a089135beb97431211e94f934843cad1b5fc750")
            throw new InvalidOperationException("Нужен Classic Explorer из закреплённого Open-Shell 4.4.198. Выберите компонент Classic Explorer в официальном установщике.");
    }
    public const string Key = @"HKCU\Software\OpenShell\ClassicExplorer";
    public const string Settings = Key + @"\Settings";
    private static readonly HashSet<string> Names = new("TreeStyle NoFadeButtons TreeItemSpacing FullIndent DisableBreadcrumbs AddressBarHistory HideSearch UseBigButtons ToolbarListMode ShowStatusBar StatusBarFont HideScrollTip ShowUpButton NoInitialToolbar ToolbarItems".Split(' '),StringComparer.Ordinal);
    public static bool Allows(Change c) => c.Kind==ChangeKind.Registry &&
        (c.Target==Settings && Names.Contains(c.ValueName) || c.Target==Key && c.ValueName=="ShowedToolbar" || c.Target==DesktopRegistryPolicy.Advanced && c.ValueName=="AlwaysShowMenus" || c.Target==DesktopRegistryPolicy.Shell && c.ValueName=="HighlightNew");
    public static Change ToolbarRegistryChange() => new(){Kind=ChangeKind.Registry,Target=Key,ValueName="ShowedToolbar",ValueKind="DWord"};
    public static void ValidateToolbarRequest(Change c)
    {
        if(c.Kind!=ChangeKind.ExplorerToolbarRequest || c.Target!=Key || c.ValueName!="ShowedToolbar" || c.ValueKind!="DWord" || c.Value is not (null or "0"))
            throw new InvalidDataException("Invalid Open-Shell toolbar request");
    }
    public static bool IsAcknowledgedRequest(Change c,ValueState current,string expected)
    {
        if(c.Kind!=ChangeKind.ExplorerToolbarRequest)return false;
        ValidateToolbarRequest(c);
        // ExplorerBHO acknowledges the one-shot request by changing DWORD 0 to 1.
        // Only the expected state accepts both; the original snapshot is exact.
        return expected==new ValueState{Exists=true,Kind="DWord",Value="0"}.Fingerprint && current.Exists && current.Kind=="DWord" && current.Value=="1";
    }
    public static void Add(Plan plan,string root,IReadOnlyDictionary<string,string>? assets=null)
    {
        void Set(string key,string name,string value,string kind="DWord") => plan.Changes.Add(new(){Kind=ChangeKind.Registry,Target=key,ValueName=name,Value=value,ValueKind=kind,Description="Windows 2000 Explorer: "+name});
        foreach(var (name,value) in new[]{("TreeStyle","0"),("NoFadeButtons","1"),("TreeItemSpacing","-2"),("FullIndent","1"),("DisableBreadcrumbs","1"),("AddressBarHistory","1"),("HideSearch","1"),("UseBigButtons","0"),("ToolbarListMode","1"),("ShowStatusBar","1"),("HideScrollTip","0"),("ShowUpButton","0"),("NoInitialToolbar","0")})Set(Settings,name,value);
        Set(Settings,"StatusBarFont","Tahoma, normal, 8","String");
        Set(DesktopRegistryPolicy.Advanced,"AlwaysShowMenus","1");
        Set(DesktopRegistryPolicy.Advanced,"ShowStatusBar","0");
        Set(DesktopRegistryPolicy.QuickAccess,"System.IsPinnedToNameSpaceTree","0");
        Set(@"HKCU\Software\OpenShell\StartMenu","ShowedStyle2","1");
        Set(DesktopRegistryPolicy.Shell,"HighlightNew","0");
        Set(Settings,"ToolbarItems",JsonSerializer.Serialize(Toolbar(root,assets)),"MultiString");
        plan.Changes.Add(new(){Kind=ChangeKind.ExplorerToolbarRequest,Target=Key,ValueName="ShowedToolbar",ValueKind="DWord",Value="0",Description="Показать панель Open-Shell; сохранить исходное ShowedToolbar"});
    }
    public static string[] Toolbar(string root,IReadOnlyDictionary<string,string>? assets=null)
    {
        var lines=new List<string>{"Items=BackItem,ForwardItem,UpItem,SEPARATOR,FoldersItem,SEPARATOR,CutItem,CopyItem,PasteItem,DeleteItem,SEPARATOR,ViewsItem,PropertiesItem"};
        void Item(string item,string command,string label,string icon)
        {
            lines.Add(item+".Command="+command);lines.Add(item+".Tip="+label);
            if(item is "BackItem" or "FoldersItem")lines.Add(item+".Label="+label);
            string role="toolbar/"+command;
            lines.Add(item+".Icon="+(assets!=null&&assets.TryGetValue(role,out string? normal)?SafePath.Under(root,"Assets2000/"+normal):icon));
            // Omit a missing disabled icon: Open-Shell generates a disabled image.
            // Never reuse the normal Cut icon as the archive XML did.
            if(assets!=null&&assets.TryGetValue(role+"/disabled",out string? disabled))lines.Add(item+".IconDisabled="+SafePath.Under(root,"Assets2000/"+disabled));
        }
        Item("BackItem","back","Назад","shell32.dll,16770");
        Item("ForwardItem","forward","Вперёд","shell32.dll,16771");
        Item("UpItem","up","На один уровень вверх","shell32.dll,46");
        Item("FoldersItem","nav_pane","Папки","shell32.dll,4");
        Item("CutItem","cut","Вырезать","shell32.dll,16762");
        Item("CopyItem","copy","Копировать","shell32.dll,16761");
        Item("PasteItem","paste","Вставить","shell32.dll,16763");
        Item("DeleteItem","delete","Удалить","shell32.dll,240");
        lines.Add("ViewsItem.Items=LargeItem,SmallItem,ListItem,DetailsItem,SEPARATOR,OptionsItem");
        lines.Add("ViewsItem.Tip=Вид");lines.Add("ViewsItem.Icon="+(assets!=null&&assets.TryGetValue("toolbar/views",out string? view)?SafePath.Under(root,"Assets2000/"+view):"shell32.dll,16710"));
        Item("LargeItem","viewicons_large","Крупные значки","shell32.dll,63008");
        Item("SmallItem","viewicons_small","Мелкие значки","shell32.dll,63010");
        Item("ListItem","viewlist","Список","shell32.dll,63000");
        Item("DetailsItem","viewdetails","Таблица","shell32.dll,62998");
        Item("OptionsItem","folderoptions","Свойства папки","imageres.dll,166");
        Item("PropertiesItem","properties","Свойства","shell32.dll,253");
        return lines.ToArray();
    }
}
