using Microsoft.Win32;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.Json;

namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public sealed class WindowsBackend(string hostRoot, bool allowDesktopSettings = false) : IChangeBackend
{
    public string HostRoot { get; } = Path.GetFullPath(hostRoot);
    // This build never elevates its transaction backend and cannot write to HKLM or system directories.
    private static readonly string[] RegistryRoots =
    ["HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run", "HKCU\\Software\\OpenShell\\StartMenu\\Settings"];
    public void Validate(Change change)
    {
        if (change.Kind == ChangeKind.File) { SafePath.Under(HostRoot, change.Target); return; }
        if (change.Kind != ChangeKind.Registry) throw new InvalidDataException("Unsupported change kind");
        if (!RegistryRoots.Contains(change.Target, StringComparer.OrdinalIgnoreCase)
            && !(allowDesktopSettings && DesktopRegistryPolicy.Allows(change)))
            throw new InvalidDataException("Registry target is not owned by this adapter: " + change.Target);
        if (change.Target.EndsWith("\\Run", StringComparison.OrdinalIgnoreCase) && change.ValueName != "AeroForge")
            throw new InvalidDataException("The adapter only manages the AeroForge startup value");
        if (change.ValueName.IndexOfAny(['\0', '\r', '\n']) >= 0) throw new InvalidDataException("Invalid registry value name");
    }
    public ValueState Read(Change change)
    {
        Validate(change);
        if (change.Kind == ChangeKind.File)
        {
            string path = SafePath.Under(HostRoot, change.Target);
            if (Directory.Exists(path)) throw new IOException("A directory occupies the file target: " + path);
            return new() { Exists = File.Exists(path), Kind = "File", Bytes = File.Exists(path) ? File.ReadAllBytes(path) : null };
        }
        using var key = Registry.CurrentUser.OpenSubKey(change.Target[5..]);
        if (key == null || !key.GetValueNames().Contains(change.ValueName, StringComparer.OrdinalIgnoreCase)) return new();
        var value = key.GetValue(change.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        var kind = key.GetValueKind(change.ValueName);
        string text = kind switch
        {
            RegistryValueKind.Binary => Convert.ToBase64String((byte[])value!),
            RegistryValueKind.MultiString => JsonSerializer.Serialize((string[])value!),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
        return new() { Exists = true, Kind = kind.ToString(), Value = text };
    }
    public void Write(Change change, ValueState state)
    {
        Validate(change);
        if (change.Kind == ChangeKind.File)
        {
            string path = SafePath.Under(HostRoot, change.Target);
            if (!state.Exists)
            {
                try { File.Delete(path); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException && IsModulePayload(change.Target))
                {
                    // Windows can rename a mapped DLL even while its image section
                    // prevents deletion. Remove it from the active module path now;
                    // delete the unchanged retired copy once the loader releases it.
                    string retired=SafePath.Under(HostRoot,"Data/Engine/Retired/"+Hashing.File(path)+"."+Guid.NewGuid().ToString("N")+".dll");
                    Directory.CreateDirectory(Path.GetDirectoryName(retired)!);
                    retired=SafePath.Under(HostRoot,Path.GetRelativePath(HostRoot,retired));
                    File.Move(path,retired);
                }
                RemoveEmptyParents(Path.GetDirectoryName(path));return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Resolve again after creation so a newly inserted reparse point fails closed.
            path = SafePath.Under(HostRoot, change.Target);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(state.Bytes ?? []); stream.Flush(true); }
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return;
        }
        if (!state.Exists)
        { using var existing = Registry.CurrentUser.OpenSubKey(change.Target[5..], true); existing?.DeleteValue(change.ValueName, false); return; }
        var kind = Enum.Parse<RegistryValueKind>(state.Kind);
        object value = kind switch
        {
            RegistryValueKind.DWord => int.Parse(state.Value!, CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => long.Parse(state.Value!, CultureInfo.InvariantCulture),
            RegistryValueKind.Binary => Convert.FromBase64String(state.Value!),
            RegistryValueKind.MultiString => JsonSerializer.Deserialize<string[]>(state.Value!)!,
            _ => state.Value ?? ""
        };
        using var key = Registry.CurrentUser.CreateSubKey(change.Target[5..], true);
        key.SetValue(change.ValueName, value, kind);
    }
    private static bool IsModulePayload(string target)=>System.Text.RegularExpressions.Regex.IsMatch(target.Replace('\\','/'),@"^Data/Engine/Mods/(32|64)/[^/]+\.dll$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    public void CleanupRetiredModules()
    {
        string directory=SafePath.Under(HostRoot,"Data/Engine/Retired");
        if(!Directory.Exists(directory))return;
        foreach(string item in Directory.EnumerateFiles(directory,"*.dll"))
        {
            string name=Path.GetFileName(item);
            if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"^[0-9a-f]{64}\.[0-9a-f]{32}\.dll$"))continue;
            try
            {
                string path=SafePath.Under(HostRoot,"Data/Engine/Retired/"+name);
                if(Hashing.File(path)!=name[..64])continue; // Preserve later edits.
                File.Delete(path);
            }
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or InvalidDataException){ }
        }
        RemoveEmptyParents(directory);
    }
    private void RemoveEmptyParents(string? directory)
    {
        string root = HostRoot.TrimEnd(Path.DirectorySeparatorChar);
        while (directory != null && !directory.Equals(root, StringComparison.OrdinalIgnoreCase)
            && directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            string relative = Path.GetRelativePath(root, directory);
            SafePath.Under(root, relative);
            if (!Directory.Exists(directory) || Directory.EnumerateFileSystemEntries(directory).Any()) return;
            Directory.Delete(directory); directory = Path.GetDirectoryName(directory);
        }
    }
    public static HostInfo Inspect()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        string product = key?.GetValue("ProductName") as string ?? "Windows";
        int.TryParse(key?.GetValue("CurrentBuildNumber")?.ToString(), out int build);
        int revision = key?.GetValue("UBR") as int? ?? 0;
        using var identity = WindowsIdentity.GetCurrent();
        using var wh = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Windhawk");
        using var r8p = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Revert8Plus");
        return new() { Os = product, Build = build, Revision = revision,
            IsWindows = true, Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            IsAdministrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator),
            HasOtherWindhawk = wh != null, HasRevert8Plus = r8p != null };
    }
}
