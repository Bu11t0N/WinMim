using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace AeroForge.Core;

public static class DesktopRegistryPolicy
{
    public const string Shell = @"HKCU\Software\OpenShell\StartMenu\Settings";
    public const string Advanced = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    public const string Dwm = @"HKCU\Software\Microsoft\Windows\DWM";
    public const string Libraries = @"HKCU\Software\Classes\CLSID\{031E4825-7B94-4DC3-B131-E946B44C8DD5}";
    public const string Computer = @"HKCU\Software\Classes\CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    public const string Favorites = @"HKCU\Software\Classes\CLSID\{323CA680-C24D-4099-B94D-446DD2D7249E}";
    public const string QuickAccess = @"HKCU\Software\Classes\CLSID\{679f85cb-0220-4080-b29b-5540cc05aab6}";
    public const string FavoritesNamespace = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{323CA680-C24D-4099-B94D-446DD2D7249E}";
    public const string HiddenDesktopIcons = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
    private static readonly Dictionary<string, HashSet<string>> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [Shell] = Names("MenuStyle EnableStartButton MouseClick WinKey CustomTaskbar TaskbarLook TaskbarOpacity TaskbarColor TaskbarTextColor TaskbarTileH TaskbarTileV SkipMetro ShowNextToTaskbar SkinW7 TaskbarTexture TaskbarBordersV StartButtonType StartButtonPath StartButtonSize ProgramsWidth MinMainHeight GlassOverride EnableGlass GlassColor GlassOpacity StartScreenShortcut PinnedPrograms PinnedItemsPath MenuItems7 RecentMetroApps"),
        [@"HKCU\Software\OpenShell\StartMenu"] = Names("ShowedStyle2"),
        [Advanced] = Names("TaskbarSmallIcons ShowTaskViewButton TaskbarGlomLevel MMTaskbarGlomLevel ShowStatusBar TaskbarSizeMove DisablePreviewDesktop"),
        [Libraries] = Names("System.IsPinnedToNameSpaceTree SortOrderIndex"),
        [FavoritesNamespace] = new(StringComparer.OrdinalIgnoreCase){""},
        [HiddenDesktopIcons] = Names("{323CA680-C24D-4099-B94D-446DD2D7249E}"),
        [Computer] = Names("LocalizedString"),
        [Favorites] = Names("System.IsPinnedToNameSpaceTree SortOrderIndex"),
        [QuickAccess] = Names("System.IsPinnedToNameSpaceTree"),
        [@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search"] = Names("SearchboxTaskbarMode"),
        [Advanced + @"\People"] = Names("PeopleBand"),
        [@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"] = Names("EnableTransparency AppsUseLightTheme"),
        [@"HKCU\Software\Microsoft\Windows\CurrentVersion\Run"] = Names("AeroForge"),
        [Dwm] = Names("GlassType CaptionButtons RoundRectRadius TextGlowMode BlurDeviation ColorizationColorBalanceOverride ColorizationAfterglowBalanceOverride ColorizationBlurBalanceOverride ColorizationOpaqueBlend ColorPrevalence EnableWindowColorization DisableModernBorders DisableGlassOnBattery ColorizationColorCaption")
    };
    private static HashSet<string> Names(string text) => new(text.Split(' '), StringComparer.OrdinalIgnoreCase);
    public static bool Allows(Change change) => change.Kind == ChangeKind.Registry
        && (Allowed.TryGetValue(change.Target, out var names) && names.Contains(change.ValueName) || DesktopIconOverrides.Allows(change) || SevenSounds.Allows(change) || Classic95Profile.Allows(change));
}

public sealed record VisualStyleState(string Path, string Color = "NormalColor", string Size = "NormalSize")
{
    public string Encode() => System.Text.Json.JsonSerializer.Serialize(new VisualStyleState(
        System.IO.Path.GetFullPath(Path).ToUpperInvariant(), Color.ToUpperInvariant(), Size.ToUpperInvariant()));
    public static VisualStyleState Decode(string value) => System.Text.Json.JsonSerializer.Deserialize<VisualStyleState>(value)
        ?? throw new InvalidDataException("Empty visual style state");
}

public static class DesktopProfile
{
    public static Plan BuildSeven(string root, string stylePath, string wallpaperPath, string? buttonPath = null, string? hostExecutable = null,TaskbarPreferences? taskbarPreferences=null,string? privateIconPack=null)
    {
        var plan = new Plan { ProfileId = "desktop-seven", ProfileName = "Оформление Seven", Exclusive = true };
        plan.Changes.Add(new(){Kind=ChangeKind.RegistryKey,Target=DesktopRegistryPolicy.FavoritesNamespace,ValueKind="RegistryKey",Value="present",Description="Регистрация настоящего Избранного Windows"});
        plan.Changes.Add(new(){Kind=ChangeKind.Registry,Target=DesktopRegistryPolicy.FavoritesNamespace,ValueName="",ValueKind="String",Value="Favorites",Description="Пространство имён Избранного"});
        if(privateIconPack!=null)plan.Changes.AddRange(DesktopIconOverrides.Build(privateIconPack));
        plan.AdoptUserPreferences=TaskbarPreferences.UserSettingIdentities.ToHashSet();
        string style = Path.GetFullPath(stylePath);
        if (!style.EndsWith(".msstyles", StringComparison.OrdinalIgnoreCase) || !File.Exists(style))
            throw new FileNotFoundException("Не найден внешний визуальный стиль Seven", style);
        void FileAsset(string target, byte[] content) => plan.Changes.Add(new() { Kind = ChangeKind.File, Target = target, Content = content, Description = "Ресурс оформления: " + target });
        void Setting(string key, string name, string value, string kind = "DWord") => plan.Changes.Add(new()
        { Kind = ChangeKind.Registry, Target = key, ValueName = name, Value = value, ValueKind = kind, Description = name });
        void Number(string key, string name, int value) => Setting(key, name, value.ToString(CultureInfo.InvariantCulture));
        byte[] taskbar=TaskbarTexture();
        string taskbarTarget="Assets/taskbar-"+Hashing.Bytes(taskbar)[..12]+".png";
        FileAsset(taskbarTarget,taskbar);
        string wallpaperExtension = Path.GetExtension(wallpaperPath).ToLowerInvariant();
        if (wallpaperExtension is not (".png" or ".jpg" or ".jpeg" or ".bmp")) throw new InvalidDataException("Unsupported wallpaper format");
        string wallpaperTarget = "Assets/wallpaper" + wallpaperExtension;
        byte[] wallpaper = File.ReadAllBytes(wallpaperPath);
        if (wallpaper.Length > 32 * 1024 * 1024) throw new InvalidDataException("Wallpaper exceeds 32 MiB");
        FileAsset(wallpaperTarget, wallpaper);
        if (buttonPath != null)
        {
            byte[] button = File.ReadAllBytes(buttonPath);
            if (button.Length > 4 * 1024 * 1024) throw new InvalidDataException("Start button exceeds 4 MiB");
            FileAsset("Assets/start.png", button);
            Setting(DesktopRegistryPolicy.Shell, "StartButtonType", "CustomButton", "String");
            Setting(DesktopRegistryPolicy.Shell, "StartButtonPath", SafePath.Under(root, "Assets/start.png"), "String");
            Number(DesktopRegistryPolicy.Shell, "StartButtonSize", 54);
        }
        else Setting(DesktopRegistryPolicy.Shell, "StartButtonType", "AeroButton", "String");
        Number(@"HKCU\Software\OpenShell\StartMenu", "ShowedStyle2", 1);
        foreach(var (name,value) in new[]{("MenuStyle","Win7"),("MouseClick","ClassicMenu"),("WinKey","ClassicMenu"),("TaskbarLook","Glass"),("TaskbarTileH","Tile"),("TaskbarTileV","Stretch")})
            Setting(DesktopRegistryPolicy.Shell,name,value,"String");
        foreach (var (name, value) in new (string, int)[] { ("EnableStartButton",1),("CustomTaskbar",1),("TaskbarOpacity",38),("TaskbarColor",0x996F46),("TaskbarTextColor",0xFFFFFF),("SkipMetro",1),("ShowNextToTaskbar",1) })
            Number(DesktopRegistryPolicy.Shell, name, value);
        Setting(DesktopRegistryPolicy.Shell, "SkinW7", "Windows Aero", "String");
        Setting(DesktopRegistryPolicy.Shell, "TaskbarTexture", SafePath.Under(root,taskbarTarget), "String");
        Setting(DesktopRegistryPolicy.Shell, "TaskbarBordersV", "1,0", "String");
        Number(DesktopRegistryPolicy.Shell,"ProgramsWidth",50);
        Number(DesktopRegistryPolicy.Shell,"MinMainHeight",21);
        Number(DesktopRegistryPolicy.Shell,"GlassOverride",1);
        Number(DesktopRegistryPolicy.Shell,"EnableGlass",1);
        Number(DesktopRegistryPolicy.Shell,"GlassColor",0x93652F);
        Number(DesktopRegistryPolicy.Shell,"GlassOpacity",25);
        Number(DesktopRegistryPolicy.Shell,"StartScreenShortcut",0);
        Number(DesktopRegistryPolicy.Shell,"RecentMetroApps",0);
        Setting(DesktopRegistryPolicy.Shell,"MenuItems7",System.Text.Json.JsonSerializer.Serialize(SevenMenuItems()),"MultiString");
        if(OperatingSystem.IsWindows())
        {
            string system=Environment.GetFolderPath(Environment.SpecialFolder.System);
            foreach(var (label,file) in new[]{("Блокнот","notepad.exe"),("Paint","mspaint.exe"),("Ножницы","SnippingTool.exe"),("Таблица символов","charmap.exe"),("Экранная лупа","Magnify.exe")})
            {
                string executable=Path.Combine(system,file);
                if(File.Exists(executable))FileAsset("Pinned/"+label+".lnk",DesktopShortcuts.Create(executable));
            }
            Setting(DesktopRegistryPolicy.Shell,"PinnedPrograms","PinnedItems","String");
            Setting(DesktopRegistryPolicy.Shell,"PinnedItemsPath",SafePath.Under(root,"Pinned"),"String");
        }
        Number(DesktopRegistryPolicy.Advanced,"ShowTaskViewButton",0);
        plan.Changes.AddRange((taskbarPreferences??TaskbarPreferences.LoadSaved(root)).Changes());
        Number(DesktopRegistryPolicy.Advanced,"ShowStatusBar",0);
        Number(DesktopRegistryPolicy.Libraries,"System.IsPinnedToNameSpaceTree",1);
        Number(DesktopRegistryPolicy.Libraries,"SortOrderIndex",66);
        Number(DesktopRegistryPolicy.HiddenDesktopIcons,"{323CA680-C24D-4099-B94D-446DD2D7249E}",1);
        Number(DesktopRegistryPolicy.Favorites,"System.IsPinnedToNameSpaceTree",1);
        Number(DesktopRegistryPolicy.Favorites,"SortOrderIndex",4);
        Number(DesktopRegistryPolicy.QuickAccess,"System.IsPinnedToNameSpaceTree",0);
        Setting(DesktopRegistryPolicy.Computer,"LocalizedString","Компьютер","String");
        Number(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0);
        Number(DesktopRegistryPolicy.Advanced + @"\People", "PeopleBand", 0);
        Number(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1);
        Number(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
        foreach (var (name, value) in new (string, int)[] { ("GlassType",1),("CaptionButtons",2),("RoundRectRadius",6),("TextGlowMode",1),("BlurDeviation",30),("ColorizationColorBalanceOverride",20),("ColorizationAfterglowBalanceOverride",10),("ColorizationBlurBalanceOverride",70),("ColorizationOpaqueBlend",0),("ColorPrevalence",0),("DisableModernBorders",1),("DisableGlassOnBattery",0),("ColorizationColorCaption",0) })
            Number(DesktopRegistryPolicy.Dwm, name, value);
        if (hostExecutable != null)
        {
            if (!Path.IsPathFullyQualified(hostExecutable) || hostExecutable.Contains('"')) throw new InvalidDataException("Invalid host executable path");
            Setting(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", "AeroForge", "\"" + hostExecutable + "\" -tray-only", "String");
        }
        // Native appearance mutations are part of the same write-ahead journal.
        plan.Changes.Add(new() { Kind = ChangeKind.VisualStyle, Target = "Windows.VisualStyle", ValueKind = "VisualStyle", Value = new VisualStyleState(style).Encode(), Description = "Визуальный стиль Seven" });
        plan.Changes.Add(new() { Kind = ChangeKind.Wallpaper, Target = "Windows.Wallpaper", ValueKind = "Wallpaper", Value = SafePath.Under(root, wallpaperTarget).ToUpperInvariant(), Description = "Обои рабочего стола" });
        return plan;
    }

    // Reproducible original RGBA artwork, without external image libraries or assets.
    public static byte[] TaskbarTexture()
    {
        using var png = new MemoryStream();
        png.Write([137,80,78,71,13,10,26,10]);
        void Chunk(string name, byte[] data)
        {
            byte[] type = Encoding.ASCII.GetBytes(name); Span<byte> number = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(number, data.Length); png.Write(number); png.Write(type); png.Write(data);
            uint crc = 0xFFFFFFFF;
            foreach (byte b in type.Concat(data)) { crc ^= b; for (int bit=0;bit<8;bit++) crc=(crc&1)!=0 ? (crc>>1)^0xEDB88320 : crc>>1; }
            BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); png.Write(number);
        }
        byte[] header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),16); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),40); header[8]=8;header[9]=6;Chunk("IHDR",header);
        using var compressed = new MemoryStream();
        using (var zip = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            for (int y=0;y<40;y++)
            {
                zip.WriteByte(0);
                for (int x=0;x<16;x++)
                {
                    if (y==0) zip.Write([224,245,255,220]);
                    else zip.Write([(byte)(92-40*y/39),(byte)(160-47*y/39),(byte)(205-39*y/39),(byte)(130+20*y/39)]);
                }
            }
        Chunk("IDAT",compressed.ToArray());Chunk("IEND",[]);return png.ToArray();
    }

    private static string[] SevenMenuItems()
    {
        var entries=new List<string>();
        var commands=new[]{("user_files",""),("lib_documents",""),("lib_pictures",""),("lib_music",""),
            ("separator",""),("games","Игры"),("computer","Компьютер"),("separator",""),
            ("control_panel","Панель управления"),("devices","Устройства и принтеры"),
            ("defaults","Программы по умолчанию"),("help","Справка и поддержка")};
        for(int i=0;i<commands.Length;i++)
        {
            string item="Item"+(i+1).ToString(CultureInfo.InvariantCulture);
            entries.Add(item+".Command="+commands[i].Item1);
            if(commands[i].Item2.Length!=0)entries.Add(item+".Label="+commands[i].Item2);
            if(commands[i].Item1!="separator")entries.Add(item+".Settings=NOEXPAND");
        }
        return entries.ToArray();
    }
}
