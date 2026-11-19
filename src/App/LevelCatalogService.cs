using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Backrooms.Engine.Platform;

namespace Backrooms.Infinity.App;

public sealed record LevelCatalogEntry(string DownloadLink, string Preview, string Name)
{
    public string LocalZipPath => AppPaths.LevelZipPath(Name);
    public bool IsInstalled => File.Exists(LocalZipPath) && new FileInfo(LocalZipPath).Length > 0;
}

public sealed class LevelCatalogService : IDisposable
{
    public static readonly Uri CatalogUri = new("https://raw.githubusercontent.com/Endlad2/Backrooms-Infinity/main/levels.db.zip");
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public LevelCatalogService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Backrooms-Infinity-Godot", "1.0"));
    }

    public async Task<IReadOnlyList<LevelCatalogEntry>> LoadAsync(CancellationToken ct = default)
    {
        AppPaths.EnsureCreated();
        byte[] bytes;
        try
        {
            bytes = await _http.GetByteArrayAsync(CatalogUri, ct);
            await File.WriteAllBytesAsync(Path.Combine(AppPaths.Cache, "levels.db.zip"), bytes, ct);
        }
        catch
        {
            var cached = Path.Combine(AppPaths.Cache, "levels.db.zip");
            if (File.Exists(cached))
                bytes = await File.ReadAllBytesAsync(cached, ct);
            else
                return await LoadBundledJsonAsync(ct);
        }

        return await ParseDatabaseZipAsync(bytes, ct);
    }

    private static async Task<IReadOnlyList<LevelCatalogEntry>> ParseDatabaseZipAsync(byte[] bytes, CancellationToken ct)
    {
        var dbPath = Path.Combine(AppPaths.Temp, $"levels-{Guid.NewGuid():N}.db");
        try
        {
            using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            {
                var entry = zip.GetEntry("levels.db") ?? throw new InvalidDataException("levels.db.zip does not contain levels.db");
                await using var input = entry.Open();
                await using var output = File.Create(dbPath);
                await input.CopyToAsync(output, ct);
            }

            var result = new List<LevelCatalogEntry>();
            await using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT download_link, preview, name FROM levels ORDER BY name COLLATE NOCASE";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result.Add(new LevelCatalogEntry(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            return result;
        }
        finally
        {
            try { File.Delete(dbPath); } catch { }
        }
    }

    private static async Task<IReadOnlyList<LevelCatalogEntry>> LoadBundledJsonAsync(CancellationToken ct)
    {
        var path = Godot.ProjectSettings.GlobalizePath("res://data/catalog.json");
        if (!File.Exists(path)) return Array.Empty<LevelCatalogEntry>();
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<LevelCatalogEntry>>(stream, cancellationToken: ct) ?? new List<LevelCatalogEntry>();
    }

    public async Task<string> EnsurePreviewAsync(LevelCatalogEntry level, CancellationToken ct = default)
    {
        AppPaths.EnsureCreated();
        var ext = GuessImageExtension(level.Preview);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(level.Preview))).ToLowerInvariant();
        var path = Path.Combine(AppPaths.PreviewCache, hash + ext);
        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
        var data = await _http.GetByteArrayAsync(level.Preview, ct);
        await File.WriteAllBytesAsync(path, data, ct);
        return path;
    }

    public async Task<string> EnsureLevelAsync(LevelCatalogEntry level, IProgress<float>? progress = null, CancellationToken ct = default)
    {
        AppPaths.EnsureCreated();
        var finalPath = level.LocalZipPath;
        if (File.Exists(finalPath) && new FileInfo(finalPath).Length > 0) return finalPath;

        var temp = finalPath + ".part";
        using var response = await _http.GetAsync(level.DownloadLink, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true);
        var buffer = new byte[64 * 1024];
        long readTotal = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), ct);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;
            if (total is > 0) progress?.Report((float)readTotal / total.Value);
        }
        File.Move(temp, finalPath, true);
        progress?.Report(1f);
        return finalPath;
    }

    private static string GuessImageExtension(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (ext is ".jpg" or ".jpeg" or ".png" or ".webp") return ext;
        }
        return ".img";
    }

    public void Dispose() => _http.Dispose();
}
