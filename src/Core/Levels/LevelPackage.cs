using System.IO.Compression;
using System.Text;

namespace Backrooms.Engine.Levels;

public sealed class LevelPackage : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly FileStream _stream;

    public string ZipPath { get; }
    public LevelDefinition Definition { get; }

    private LevelPackage(string zipPath, FileStream stream, ZipArchive archive, LevelDefinition definition)
    {
        ZipPath = zipPath;
        _stream = stream;
        _archive = archive;
        Definition = definition;
    }

    public static LevelPackage Open(string zipPath, LevelParser parser)
    {
        var full = Path.GetFullPath(zipPath);
        if (!File.Exists(full))
            throw new FileNotFoundException("Level ZIP was not found.", full);

        var stream = File.Open(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        try
        {
            var definition = parser.Parse(archive);
            return new LevelPackage(full, stream, archive, definition);
        }
        catch
        {
            archive.Dispose();
            stream.Dispose();
            throw;
        }
    }

    public bool Exists(string relativePath) =>
        _archive.GetEntry(Normalize(relativePath)) is not null;

    public Stream OpenAsset(string relativePath)
    {
        var normalized = Normalize(relativePath);
        var entry = _archive.GetEntry(normalized)
                    ?? throw new FileNotFoundException($"Asset '{normalized}' not found in level ZIP.");
        return entry.Open();
    }

    public string ReadText(string relativePath)
    {
        using var stream = OpenAsset(relativePath);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    public string MaterializeAsset(string relativePath, string tempRoot)
    {
        var normalized = Normalize(relativePath);
        var destination = Path.Combine(tempRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var source = OpenAsset(normalized);
        using var target = File.Create(destination);
        source.CopyTo(target);
        return destination;
    }

    public void Dispose()
    {
        _archive.Dispose();
        _stream.Dispose();
    }

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        var replaced = path.Replace('\\', '/').TrimStart('/');
        var parts = replaced.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Any(p => p is "." or ".."))
            throw new InvalidDataException($"Unsafe ZIP path: '{path}'.");

        return string.Join('/', parts);
    }
}
