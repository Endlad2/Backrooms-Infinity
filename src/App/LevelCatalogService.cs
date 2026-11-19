using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Backrooms.Engine.Diagnostics;
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
    public static readonly Uri CatalogJsonUri = new("https://raw.githubusercontent.com/Endlad2/Backrooms-Infinity/main/data/catalog.json");

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly DiagnosticBus? _diagnostics;

    public LevelCatalogService(DiagnosticBus? diagnostics = null)
    {
        _diagnostics = diagnostics;
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var cached = Path.Combine(AppPaths.Cache, "levels.db.zip");
            if (File.Exists(cached))
                bytes = await File.ReadAllBytesAsync(cached, ct);
            else
            {
                _diagnostics?.Warn("Catalog", "levels.db.zip unavailable; falling back to JSON catalog.", ex.Message);
                return await LoadJsonWithFallbackAsync(ct);
            }
        }

        try
        {
            return await SqliteCatalogReader.ParseDatabaseZipAsync(bytes, ct);
        }
        catch (Exception ex) when (IsSqliteRuntimeFailure(ex))
        {
            _diagnostics?.Warn(
                "Catalog",
                "SQLite runtime dependency is unavailable. Using the mirrored JSON catalog instead.",
                ex.ToString());
            return await LoadJsonWithFallbackAsync(ct);
        }
    }

    private async Task<IReadOnlyList<LevelCatalogEntry>> LoadJsonWithFallbackAsync(CancellationToken ct)
    {
        var cachedJson = Path.Combine(AppPaths.Cache, "catalog.json");
        try
        {
            var bytes = await _http.GetByteArrayAsync(CatalogJsonUri, ct);
            await File.WriteAllBytesAsync(cachedJson, bytes, ct);
            return DeserializeCatalog(bytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (File.Exists(cachedJson))
            {
                try
                {
                    return DeserializeCatalog(await File.ReadAllBytesAsync(cachedJson, ct));
                }
                catch (Exception cachedEx)
                {
                    _diagnostics?.Warn("Catalog", "Cached JSON catalog is invalid; trying bundled catalog.", cachedEx.Message);
                }
            }

            _diagnostics?.Warn("Catalog", "Remote JSON catalog unavailable; using bundled catalog.", ex.Message);
            return await LoadBundledJsonAsync(ct);
        }
    }

    private static IReadOnlyList<LevelCatalogEntry> DeserializeCatalog(byte[] bytes) =>
        JsonSerializer.Deserialize<List<LevelCatalogEntry>>(bytes) ?? new List<LevelCatalogEntry>();

    private static bool IsSqliteRuntimeFailure(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is FileNotFoundException or FileLoadException or DllNotFoundException or BadImageFormatException or TypeInitializationException)
                return true;

            var text = current.Message;
            if (text.Contains("Microsoft.Data.Sqlite", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("SQLitePCLRaw", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("e_sqlite3", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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
