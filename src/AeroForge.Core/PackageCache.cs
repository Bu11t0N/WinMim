using System.Security.Cryptography;

namespace AeroForge.Core;

public sealed class PackageCache(string directory)
{
    private readonly string root = Path.GetFullPath(directory);
    public async Task<string> DownloadAsync(Dependency dependency, IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(dependency.DownloadUrl);
        if (uri.Scheme != "https" || uri.Host != "github.com" || dependency.Sha256.Length != 64)
            throw new InvalidDataException("A pinned HTTPS GitHub release with SHA-256 is required");
        string path = SafePath.Under(root, dependency.FileName);
        Directory.CreateDirectory(root);
        if (File.Exists(path) && Hashing.File(path).Equals(dependency.Sha256, StringComparison.OrdinalIgnoreCase)) return path;
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AeroForge/0.1.0");
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            const long limit = 512L * 1024 * 1024;
            if (response.Content.Headers.ContentLength > limit) throw new IOException("Package exceeds 512 MiB");
            await using var src = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var dst = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                byte[] buffer = new byte[65536]; long done = 0; int read;
                while ((read = await src.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    done += read; if (done > limit) throw new IOException("Package exceeds 512 MiB");
                    hash.AppendData(buffer, 0, read); await dst.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    progress?.Report($"{dependency.Name}: {done / 1048576d:F1} МБ");
                }
                dst.Flush(true);
            }
            string actual = Convert.ToHexStringLower(hash.GetHashAndReset());
            if (!actual.Equals(dependency.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 mismatch");
            File.Move(temp, path, true); return path;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
