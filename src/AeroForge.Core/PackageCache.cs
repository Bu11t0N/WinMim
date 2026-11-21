using System.Security.Cryptography;

namespace AeroForge.Core;

public sealed class PackageCache(string directory, HttpMessageHandler? transport = null)
{
    private readonly string root = Path.GetFullPath(directory);
    public async Task<string> DownloadAsync(Dependency dependency, IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(dependency.DownloadUrl);
        if (uri.Scheme != "https" || uri.Host != "github.com" || dependency.Sha256.Length != 64 ||
            dependency.Sha256.Any(c => !Uri.IsHexDigit(c)) || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            throw new InvalidDataException("A pinned HTTPS GitHub release with SHA-256 is required");
        string path = SafePath.Under(root, dependency.FileName);
        Directory.CreateDirectory(root);
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(path) && Hashing.File(path).Equals(dependency.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(UiText.Format("PackageCache.0VerifiedPackageFoundInTheCache", dependency.Name));
            return path;
        }
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using var client = new HttpClient(transport ?? new SocketsHttpHandler
                { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(20) }, disposeHandler: transport == null)
                { Timeout = TimeSpan.FromMinutes(2) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WinMim/" + typeof(PackageCache).Assembly.GetName().Version!.ToString(3));
            using var response = await GetReleaseAsync(client, uri, cancellationToken);
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
                    progress?.Report(UiText.Format("PackageCache.01F1MB", dependency.Name, done / 1048576d));
                }
                dst.Flush(true);
            }
            string actual = Convert.ToHexStringLower(hash.GetHashAndReset());
            if (!actual.Equals(dependency.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(UiText.Format("PackageCache.0SHA256MismatchThePackageWas", dependency.Name));
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temp, path, true); return path;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    internal static FileStream OpenVerified(string path, string hash)
    {
        path = SafePath.Under(Path.GetDirectoryName(path)!, Path.GetFileName(path));
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (!Convert.ToHexString(SHA256.HashData(file)).Equals(hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(UiText.Get("PackageCache.ThePackageWasChangedSHA256Mismatch"));
            file.Position = 0;
            return file;
        }
        catch { file.Dispose(); throw; }
    }

    private static async Task<HttpResponseMessage> GetReleaseAsync(HttpClient client, Uri uri, CancellationToken token)
    {
        for (int redirect = 0; redirect < 5; redirect++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                uri.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                throw new IOException(UiText.Get("PackageCache.TheOfficialSourceRedirectedToAnUnapproved"));
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) return response;
            using (response)
                uri = new Uri(uri, response.Headers.Location ?? throw new IOException(UiText.Get("PackageCache.TheRedirectAddressIsMissing")));
        }
        throw new IOException(UiText.Get("PackageCache.TooManyRedirectsWhileDownloadingTheComponent"));
    }
}
