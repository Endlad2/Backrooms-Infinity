using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Backrooms.Engine.Platform;

namespace Backrooms.Launcher.Catalog;

public sealed class LevelCatalogService
{
    public static readonly Uri CatalogUri = new(
        "https://raw.githubusercontent.com/Endlad2/Backrooms-Infinity/main/levels.db.zip");

    private readonly HttpClient _http;

    public LevelCatalogService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Backrooms-Infinity", "1.0"));
    }

    public async Task<IReadOnlyList<LevelCatalogEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        var bytes = await _http.GetByteArrayAsync(CatalogUri, cancellationToken);
        var cachedZip = Path.Combine(AppPaths.Cache, "levels.db.zip");
        await File.WriteAllBytesAsync(cachedZip, bytes, cancellationToken);

        var dbPath = Path.Combine(AppPaths.Temp, $"levels-{Guid.NewGuid():N}.db");
        try
        {
            using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, leaveOpen: false))
            {
                var entry = zip.GetEntry("levels.db")
                    ?? throw new InvalidDataException("levels.db.zip does not contain levels.db.");
                await using var input = entry.Open();
                await using var output = File.Create(dbPath);
                await input.CopyToAsync(output, cancellationToken);
            }

            var result = new List<LevelCatalogEntry>();
            await using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT download_link, preview, name FROM levels ORDER BY name COLLATE NOCASE";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new LevelCatalogEntry(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2)));
            }

            return result;
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }

    public async Task<string> EnsurePreviewAsync(LevelCatalogEntry level, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        var extension = GuessImageExtension(level.Preview);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(level.Preview))).ToLowerInvariant();
        var path = Path.Combine(AppPaths.PreviewCache, hash + extension);
        if (File.Exists(path) && new FileInfo(path).Length > 0)
            return path;

        var tmp = path + ".part";
        var bytes = await _http.GetByteArrayAsync(level.Preview, cancellationToken);
        await File.WriteAllBytesAsync(tmp, bytes, cancellationToken);
        File.Move(tmp, path, overwrite: true);
        return path;
    }

    public async Task<string> EnsureLevelAsync(
        LevelCatalogEntry level,
        IProgress<(long Received, long? Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        var finalPath = level.LocalZipPath;
        if (File.Exists(finalPath) && new FileInfo(finalPath).Length > 0)
            return finalPath;

        var tempPath = finalPath + ".part";
        using var response = await _http.GetAsync(level.DownloadLink, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 64, useAsync: true);
        var buffer = new byte[1024 * 64];
        long received = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            progress?.Report((received, total));
        }

        File.Move(tempPath, finalPath, overwrite: true);
        return finalPath;
    }

    private static string GuessImageExtension(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var ext = Path.GetExtension(uri.AbsolutePath);
            if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".webp", StringComparison.OrdinalIgnoreCase))
                return ext.ToLowerInvariant();
        }
        return ".img";
    }
}
