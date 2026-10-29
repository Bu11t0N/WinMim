using System.Text.RegularExpressions;

namespace AeroForge.Core;

public static class SafePath
{
    public static string Under(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')
            || relative.Split('/', '\\').Any(s => s is ".." or "." or "" || s.EndsWith(' ') || s.EndsWith('.')))
            throw new InvalidDataException("Unsafe relative path: " + relative);
        foreach (string part in relative.Split('/', '\\'))
        {
            string stem = part.Split('.')[0].ToUpperInvariant();
            if (part.IndexOfAny(['<', '>', '"', '|', '?', '*', '\0']) >= 0 || stem is "CON" or "PRN" or "AUX" or "NUL"
                || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && "123456789¹²³".Contains(stem[3])))
                throw new InvalidDataException("Reserved Windows path: " + relative);
        }
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Path escapes root: " + relative);
        string? ancestor = fullRoot.TrimEnd(Path.DirectorySeparatorChar);
        while (ancestor != null) { CheckNoLinks(ancestor); ancestor = Path.GetDirectoryName(ancestor); }
        string? current = result;
        while (current != null && current.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        { CheckNoLinks(current); current = Path.GetDirectoryName(current); }
        return result;
    }
    private static void CheckNoLinks(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Symbolic links are not allowed in managed paths: " + path);
    }
}
public sealed partial class ContentStore(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    public Catalog Load()
    {
        var catalog = JsonFile.Read<Catalog>(Path.Combine(Root, "catalog.json"));
        if (catalog.SchemaVersion != 1) throw new InvalidDataException("Unsupported catalog schema");
        if (catalog.Modules.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != catalog.Modules.Count)
            throw new InvalidDataException("Duplicate module IDs");
        foreach (var m in catalog.Modules)
            if (!IdRegex().IsMatch(m.Id)) throw new InvalidDataException("Invalid module ID: " + m.Id);
        if (catalog.Profiles.Select(p => p.Id).Distinct().Count() != catalog.Profiles.Count)
            throw new InvalidDataException("Duplicate profile IDs");
        return catalog;
    }
    public byte[] ReadVerified(ContentFile file)
    {
        if (!HashRegex().IsMatch(file.Sha256)) throw new InvalidDataException("Missing SHA-256: " + file.Path);
        string path = SafePath.Under(Root, file.Path);
        byte[] bytes = File.ReadAllBytes(path);
        if (!string.Equals(Hashing.Bytes(bytes), file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SHA-256 mismatch: " + file.Path);
        return bytes;
    }
    public string[] VerifyAll(Catalog catalog)
    {
        List<string> errors = [];
        foreach (var f in catalog.RuntimeFiles.Concat(catalog.Modules.SelectMany(m => m.Binaries)))
            try { ReadVerified(f); } catch (Exception ex) { errors.Add(ex.Message); }
        return errors.ToArray();
    }
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,80}$")] private static partial Regex IdRegex();
    [GeneratedRegex("^[a-fA-F0-9]{64}$")] private static partial Regex HashRegex();
}
