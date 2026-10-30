using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AeroForge.Core;

public static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public static T Read<T>(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        return JsonSerializer.Deserialize<T>(stream,Options) ?? throw new InvalidDataException("Empty JSON: "+path);
    }
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, value, Options); file.Flush(true); }
            for(int attempt=0;;attempt++)
            {
                try{File.Move(temp,path,true);break;}
                catch(IOException ex)when(attempt<40 && (ex.HResult&0xffff) is 5 or 32 or 33){Thread.Sleep(50);}
                catch(UnauthorizedAccessException)when(attempt<40 && File.Exists(path)){Thread.Sleep(50);}
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public sealed class Catalog
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "0.1.0";
    public string SourceCommit { get; set; } = "";
    public List<Module> Modules { get; set; } = [];
    public List<Profile> Profiles { get; set; } = [];
    public List<ContentFile> RuntimeFiles { get; set; } = [];
    public List<Dependency> Dependencies { get; set; } = [];
}
public sealed class Module
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string License { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Status { get; set; } = "source";
    public bool CriticalProcess { get; set; }
    public bool RequiresResources { get; set; }
    public string[] Conflicts { get; set; } = [];
    public string[] Includes { get; set; } = [];
    public string[] Excludes { get; set; } = [];
    public string[] Architecture { get; set; } = [];
    public Dictionary<string, string> DefaultSettings { get; set; } = [];
    public List<ContentFile> Binaries { get; set; } = [];
    public string ManagedId => "local@aeroforge-" + Id;
}
public sealed class Profile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Accent { get; set; } = "#65B9E8";
    public string[] Modules { get; set; } = [];
    public Dictionary<string, Dictionary<string, string>> Settings { get; set; } = [];
}
public sealed class ContentFile
{
    public string Path { get; set; } = "";
    public string Target { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
public sealed class Dependency
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string License { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string FileName { get; set; } = "";
}
public sealed class HostInfo
{
    public string Os { get; set; } = "";
    public int Build { get; set; }
    public int Revision { get; set; }
    public string Architecture { get; set; } = "";
    public bool IsAdministrator { get; set; }
    public bool IsWindows { get; set; }
    public bool HasOtherWindhawk { get; set; }
    public bool HasRevert8Plus { get; set; }
    public bool KnownBase => IsWindows && Architecture == "X64" && Build is >= 19041 and <= 19045;
    public string Summary => $"{Os} · {Build}.{Revision} · {Architecture}";
}
public enum ChangeKind { File, Registry, VisualStyle, Wallpaper, TaskbarState, RegistryKey, ThemeSection, ClassicAppearance }
public sealed class Change
{
    public ChangeKind Kind { get; set; }
    public string Target { get; set; } = "";
    public string ValueName { get; set; } = "";
    public string ValueKind { get; set; } = "String";
    public string? Value { get; set; }
    public byte[]? Content { get; set; }
    public string Description { get; set; } = "";
    public string Identity => Kind == ChangeKind.File ? "file:" + Target.ToUpperInvariant()
        : Kind == ChangeKind.RegistryKey ? "key:" + Target.ToUpperInvariant()
        : "reg:" + Target.ToUpperInvariant() + "|" + ValueName.ToUpperInvariant();
}
public sealed class Plan
{
    public HashSet<string> AdoptUserPreferences { get; set; } = [];
    public bool Exclusive { get; set; }
    public string ProfileId { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public List<Change> Changes { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Blockers { get; set; } = [];
    public string[] Modules { get; set; } = [];
    public long Bytes => Changes.Sum(c => (long)(c.Content?.Length ?? 0));
    public string Fingerprint => Hashing.Text(JsonSerializer.Serialize(Changes, JsonFile.Options));
}
public static class Hashing
{
    public static string Bytes(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    public static string Text(string s) => Bytes(Encoding.UTF8.GetBytes(s));
    public static string File(string path)
    { using var stream = System.IO.File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
}
