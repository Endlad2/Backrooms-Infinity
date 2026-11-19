using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Backrooms.Engine.Platform;

namespace Backrooms.Infinity.App;

internal static class SqliteCatalogReader
{
    public static async Task<IReadOnlyList<LevelCatalogEntry>> ParseDatabaseZipAsync(byte[] bytes, CancellationToken ct)
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
}
