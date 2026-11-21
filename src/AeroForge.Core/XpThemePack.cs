using System.Buffers.Binary;
using System.Runtime.Versioning;

namespace AeroForge.Core;

// Only metadata is distributed. The style and Microsoft resources stay in a
// private pack; nothing is loaded as executable code by the preparation tool.
public sealed class XpPackManifest
{
    public int SchemaVersion { get; set; } = 1;
    public int Build { get; set; } = 19044;
    public string Source { get; set; } = "";
    public string License { get; set; } = "";
    public string Style { get; set; } = "";
    public string Color { get; set; } = "NormalColor";
    public string Size { get; set; } = "NormalSize";
    public string? Wallpaper { get; set; }
    public string? MenuSkin { get; set; }
    public Dictionary<string,string> Icons { get; set; } = [];
    public Dictionary<string,string> Sounds { get; set; } = [];
    public Dictionary<string,string> Cursors { get; set; } = [];
    public List<XpPackFile> Files { get; set; } = [];
}
public sealed record XpPackFile(string Name, string Sha256);

public sealed class XpThemePack
{
    public const string PrivateAssets = @"C:\AeroForgeLab\PrivateAssets\WindowsXP";
    public static string UserAssets => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinMim", "Packs", "WindowsXP");

    public static XpThemePack ReadDefault(string content)
    {
        string directory = UserAssets;
        if (!Directory.Exists(directory) && Environment.MachineName == "AEROFORGE-LAB" && Directory.Exists(PrivateAssets))
            directory = PrivateAssets;
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(UiText.Get("XpThemePack.APersonalLunaBluePackIsRequired"));
        return Read(content, directory);
    }

    public XpPackManifest Manifest { get; }
    private readonly Dictionary<string,byte[]> files;
    private readonly string directory;
    public string? IconPackDirectory=>files.ContainsKey("Resources/manifest.json")?SafePath.Under(directory,"Resources"):null;
    private XpThemePack(XpPackManifest manifest,Dictionary<string,byte[]> files,string directory)
    { Manifest=manifest;this.files=files;this.directory=directory; }
    public static XpThemePack Read(string content,string directory)
    {
        string reference=SafePath.Under(content,"xp/pack-reference.json");
        if(!File.Exists(reference))throw new FileNotFoundException(UiText.Get("XpThemePack.Build19044RequiresAVerifiedPersonalLuna"),reference);
        return Read(JsonFile.Read<XpPackManifest>(reference),directory);
    }
    public static XpThemePack Read(XpPackManifest manifest,string directory)
    {
        if(manifest.SchemaVersion!=1||manifest.Build!=19044||manifest.Files.Count is <1 or >256
            ||manifest.Files.Select(f=>f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=manifest.Files.Count
            ||string.IsNullOrWhiteSpace(manifest.Source)||string.IsNullOrWhiteSpace(manifest.License))
            throw new InvalidDataException(UiText.Get("XpThemePack.InvalidPersonalXPPackManifest"));
        var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);long total=0;
        foreach(var file in manifest.Files)
        {
            string path=SafePath.Under(directory,file.Name);
            if(!File.Exists(path))throw new FileNotFoundException(UiText.Get("XpThemePack.MissingPersonalXPResource")+file.Name,path);
            long length=new FileInfo(path).Length;total+=length;
            if(length is <1 or >64*1024*1024||total>256*1024*1024)throw new InvalidDataException(UiText.Get("XpThemePack.TheXPPackIsTooLarge"));
            byte[] bytes=File.ReadAllBytes(path);
            if(!Hashing.Bytes(bytes).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(UiText.Get("XpThemePack.XPResourceSHA256Mismatch")+file.Name);
            string extension=Path.GetExtension(file.Name).ToLowerInvariant();
            if(extension is ".msstyles" or ".dll" or ".mui" or ".skin")ValidateResourcePe(bytes);
            else if(extension==".ico")ValidateIcon(bytes);
            else if(extension==".cur")
            {
                var icon=(byte[])bytes.Clone();if(icon.Length<4||icon[2]!=2||icon[3]!=0)throw new InvalidDataException(UiText.Get("XpThemePack.InvalidXPCursor"));
                icon[2]=1;ValidateIcon(icon);
            }
            else if(extension==".wav" && (bytes.Length<12||!bytes.AsSpan(0,4).SequenceEqual("RIFF"u8)||!bytes.AsSpan(8,4).SequenceEqual("WAVE"u8)||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))!=bytes.Length-8))
                throw new InvalidDataException(UiText.Get("XpThemePack.InvalidXPWAVFile"));
            else if(extension==".json" && file.Name=="Resources/manifest.json")using(System.Text.Json.JsonDocument.Parse(bytes)){}
            else if(extension is not (".jpg" or ".png" or ".bmp" or ".cur" or ".ani" or ".wav"))
                throw new InvalidDataException(UiText.Get("XpThemePack.TheXPResourceTypeIsUnsupported")+file.Name);
            files.Add(file.Name,bytes);
        }
        if(!manifest.Style.EndsWith(".msstyles",StringComparison.OrdinalIgnoreCase)||!files.ContainsKey(manifest.Style)
            ||manifest.Wallpaper!=null&&!files.ContainsKey(manifest.Wallpaper)||manifest.MenuSkin!=null&&(!manifest.MenuSkin.EndsWith(".skin",StringComparison.OrdinalIgnoreCase)||!files.ContainsKey(manifest.MenuSkin)))throw new InvalidDataException(UiText.Get("XpThemePack.IncompleteXPVisualStyle"));
        foreach(string role in new[]{"computer","documents","network","recycle-empty","recycle-full","programs","settings","search","help","run","shutdown","start","printers"})
            if(!manifest.Icons.TryGetValue(role,out var icon)||!files.ContainsKey(icon)||!icon.EndsWith(".ico",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(UiText.Get("XpThemePack.IncompleteXPIconSet")+role);
        foreach(var pair in manifest.Sounds)
            if(!XpDesktopResources.SoundEvents.Contains(pair.Key)||!files.ContainsKey(pair.Value)||!pair.Value.EndsWith(".wav",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(UiText.Get("XpThemePack.UnknownXPSound")+pair.Key);
        foreach(var pair in manifest.Cursors)
            if(!XpDesktopResources.CursorNames.Contains(pair.Key)||!files.ContainsKey(pair.Value)||!pair.Value.EndsWith(".cur",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(UiText.Get("XpThemePack.UnknownXPPointer")+pair.Key);
        var pack=new XpThemePack(manifest,files,Path.GetFullPath(directory));
        if(pack.IconPackDirectory is string resources)
        {
            var libraries=JsonFile.Read<PrivateIconPack>(SafePath.Under(resources,"manifest.json"));
            if(libraries.Files.Any(f=>!files.ContainsKey("Resources/"+f.FileName)))throw new InvalidDataException(UiText.Get("XpThemePack.TheLibraryIsMissingFromTheXP"));
            _=PrivateIconPack.Settings(resources);
        }
        return pack;
    }
    public static void ValidateResourcePe(byte[] data)
    {
        if(data.Length<64||data[0]!='M'||data[1]!='Z')throw new InvalidDataException(UiText.Get("XpThemePack.TheXPResourceIsNotAPE"));
        int pe=BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(60,4));
        if(pe<64||pe>data.Length-44||BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pe,4))!=0x4550
            ||BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pe+40,4))!=0)
            throw new InvalidDataException(UiText.Get("XpThemePack.XPPacksSupportResourceOnlyPEFiles"));
    }
    public static void ValidateIcon(byte[] data)
    {
        if(data.Length<22||BinaryPrimitives.ReadUInt16LittleEndian(data)!=0||BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2))!=1)
            throw new InvalidDataException(UiText.Get("XpThemePack.InvalidXPIconGroup"));
        int count=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));
        if(count<1||count>256||6+16*count>data.Length)throw new InvalidDataException(UiText.Get("XpThemePack.InvalidXPICOTable"));
        for(int i=0;i<count;i++)
        {
            uint length=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(6+16*i+8));
            uint offset=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(6+16*i+12));
            if(length==0||offset<6+16*count||offset+(ulong)length>(ulong)data.Length)throw new InvalidDataException(UiText.Get("XpThemePack.InvalidXPICOSize"));
        }
    }
    // DWM and the Themes service must be able to read the style. Keep it in the
    // verified machine-readable private pack, like the existing Seven adapter,
    // rather than an ACL-restricted LocalAppData staging directory.
    public VisualStyleState Style=>new(SafePath.Under(directory,Manifest.Style),Manifest.Color,Manifest.Size);
    public IEnumerable<Change> Stage()=>files.Select(f=>new Change{Kind=ChangeKind.File,Target="XP/"+f.Key,Content=f.Value,Description=UiText.Get("XpThemePack.PersonalXPResource")+f.Key});
}

[SupportedOSPlatform("windows")]
public static class XpRuntime
{
    // Extracted from the hash-pinned v4.0.0 MSI; generated metadata is checked
    // against the installed DLL, rather than accepting an arbitrary patcher.
    public static void Check(string content)
    {
        if (!ThemePreparation.SecureUxReady(content))
            throw new InvalidOperationException(UiText.Get("XpThemePack.LunaRequiresThePinnedSecureUxTheme40"));
    }

    internal static void VerifyInstalled(string content)
    {
        var pins=JsonFile.Read<Dictionary<string,string>>(SafePath.Under(content,"xp/secureux-reference.json"));
        string system=Environment.GetFolderPath(Environment.SpecialFolder.System);
        foreach(var pin in pins)
        {
            if(pin.Key is not ("SecureUxTheme.dll" or "SecureUxTheme4.dll"))throw new InvalidDataException(UiText.Get("XpThemePack.InvalidSecureUxThemeChecksumReference"));
            string path=Path.Combine(system,pin.Key);
            if(File.Exists(path)&&Hashing.Bytes(File.ReadAllBytes(path))==pin.Value)return;
        }
        throw new InvalidOperationException(UiText.Get("XpThemePack.LunaRequiresThePinnedSecureUxTheme402"));
    }
}
