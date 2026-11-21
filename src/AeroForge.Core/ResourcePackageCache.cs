using System.IO.Compression;

namespace AeroForge.Core;

public sealed record ResourcePackage(string Id, string Version, string Source, string Sha256, bool Approved);

// Preparation copies data to a durable destination. Apply/recovery must never use the cache as its resource root.
public sealed class ResourcePackageCache(string cacheDirectory, string preparedDirectory, HttpMessageHandler? transport = null)
{
    private const long Limit = 64 * 1024 * 1024;
    private const string MetadataName = ".winmim-package.json";
    private readonly string cache = Path.GetFullPath(cacheDirectory);
    private readonly string prepared = Path.GetFullPath(preparedDirectory);

    public async Task<string> PrepareAsync(ResourcePackage package, string? localArchive = null, CancellationToken token = default)
    {
        string digest = Validate(package);
        if (Contains(cache, prepared) || Contains(prepared, cache))
            throw new InvalidOperationException("Кеш и подготовленные данные должны находиться в отдельных каталогах.");
        string destination = SafePath.Under(prepared, digest);
        string archive = SafePath.Under(cache, digest + ".zip");
        token.ThrowIfCancellationRequested();
        if (Directory.Exists(destination))
            throw new IOException("Такой пакет уже подготовлен. Используйте сохранённую копию.");

        if (localArchive == null && !File.Exists(archive))
        {
            if (!TryReleaseSource(package.Source, out var source))
                throw new FileNotFoundException("Кеш пуст. Укажите локальный файл этого пакета.");
            await new PackageCache(cache, transport).DownloadAsync(new Dependency
            {
                Name = package.Id, Version = package.Version, DownloadUrl = source.AbsoluteUri,
                FileName = Path.GetFileName(archive), Sha256 = digest
            }, cancellationToken: token);
        }
        Directory.CreateDirectory(prepared);
        Directory.CreateDirectory(cache);
        string pending = SafePath.Under(prepared, ".pending-" + Guid.NewGuid().ToString("N"));
        string incoming = SafePath.Under(cache, ".incoming-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pending);
        var written = new List<string>();
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (localArchive != null)
            {
                using var source = PackageCache.OpenVerified(Path.GetFullPath(localArchive), digest);
                if (source.Length > Limit) throw new InvalidDataException("Архив превышает 64 МиБ.");
                await using var copy = new FileStream(incoming, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(copy, token);
            }
            using (var source = PackageCache.OpenVerified(localArchive == null ? archive : incoming, digest))
            {
                if (source.Length > Limit) throw new InvalidDataException("Архив превышает 64 МиБ.");
                using var zip = new ZipArchive(source, ZipArchiveMode.Read);
                if (zip.Entries.Count is < 1 or > 256) throw new InvalidDataException("Неожиданное число файлов в архиве.");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long total = 0;
                foreach (var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    if (entry.FullName.EndsWith('/') && entry.Length == 0)
                    {
                        _ = SafePath.Under(pending, entry.FullName[..^1]);
                        continue;
                    }
                    string name = SafePath.Under(pending, entry.FullName);
                    if (!seen.Add(name) || entry.FullName.Equals(MetadataName, StringComparison.OrdinalIgnoreCase) ||
                        ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 ||
                        Path.GetExtension(name).ToLowerInvariant() is not (".txt" or ".json" or ".png" or ".jpg" or ".bmp" or ".ico" or ".wav") ||
                        entry.Length < 0 || entry.Length > Limit - total)
                        throw new InvalidDataException("Архив содержит неподдерживаемый, повторный или слишком большой файл.");
                    string parent = Path.GetDirectoryName(name)!;
                    for (string? folder = parent; folder != pending; folder = Path.GetDirectoryName(folder))
                        directories.Add(folder!);
                    Directory.CreateDirectory(parent);
                    using var reader = entry.Open();
                    await using var writer = new FileStream(name, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    written.Add(name);
                    byte[] buffer = new byte[65536];
                    long length = 0;
                    int count;
                    while ((count = await reader.ReadAsync(buffer, token)) > 0)
                    {
                        length += count;
                        if (length > entry.Length) throw new InvalidDataException("Размер файла не совпадает с записью ZIP.");
                        await writer.WriteAsync(buffer.AsMemory(0, count), token);
                    }
                    if (length != entry.Length) throw new InvalidDataException("Неполный файл в ZIP.");
                    total += length;
                }
                if (written.Count == 0) throw new InvalidDataException("Архив не содержит поддерживаемых файлов.");
            }
            token.ThrowIfCancellationRequested();
            string metadata = SafePath.Under(pending, MetadataName);
            written.Add(metadata);
            JsonFile.Write(metadata, package);
            if (localArchive != null) File.Move(incoming, archive, overwrite: true);
            Directory.Move(pending, destination);
            return destination;
        }
        finally
        {
            File.Delete(incoming);
            if (Directory.Exists(pending))
            {
                foreach (string name in written) File.Delete(name);
                foreach (string folder in directories.OrderByDescending(name => name.Length))
                    if (Directory.Exists(folder)) Directory.Delete(folder);
                Directory.Delete(pending);
            }
        }
    }

    public void ClearCachedArchive(ResourcePackage package)
    {
        // Clearing the cache leaves prepared assets and recovery state intact, even for withdrawn sources.
        if (!ValidHash(package.Sha256)) throw new InvalidDataException("Отсутствует проверенный SHA-256.");
        if (Directory.Exists(cache)) File.Delete(SafePath.Under(cache, package.Sha256.ToLowerInvariant() + ".zip"));
    }

    private static bool ValidHash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string Validate(ResourcePackage package)
    {
        if (!package.Approved || string.IsNullOrWhiteSpace(package.Id) || string.IsNullOrWhiteSpace(package.Version) ||
            string.IsNullOrWhiteSpace(package.Source) || !ValidHash(package.Sha256) ||
            (!package.Source.StartsWith("local:", StringComparison.Ordinal) && !TryReleaseSource(package.Source, out _)))
            throw new InvalidDataException("Источник или версия пакета не утверждены.");
        return package.Sha256.ToLowerInvariant();
    }

    private static bool TryReleaseSource(string source, out Uri uri)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var candidate) &&
            candidate is { Scheme: "https", Host: "github.com", IsDefaultPort: true, UserInfo.Length: 0, Query.Length: 0, Fragment.Length: 0 })
        {
            var parts = candidate.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 6 && parts[2] == "releases" && parts[3] == "download" &&
                parts[5].EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                uri = candidate;
                return true;
            }
        }
        uri = null!;
        return false;
    }

    private static bool Contains(string root, string path) =>
        Path.TrimEndingDirectorySeparator(root).Equals(Path.TrimEndingDirectorySeparator(path), StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
