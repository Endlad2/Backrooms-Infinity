using System.IO.Compression;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Platform;

namespace Backrooms.Infinity.App;

public sealed class LevelExtraction : IDisposable
{
    public string Root { get; }
    public LevelExtraction(LevelPackage package)
    {
        Root = Path.Combine(AppPaths.Temp, "godot-level-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        using var zip = ZipFile.OpenRead(package.ZipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var normalized = LevelPackage.Normalize(entry.FullName);
            var destination = Path.GetFullPath(Path.Combine(Root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(Path.GetFullPath(Root), StringComparison.Ordinal))
                throw new InvalidDataException("Unsafe ZIP path.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, true);
        }
    }
    public string PathFor(string relative) => Path.Combine(Root, LevelPackage.Normalize(relative).Replace('/', Path.DirectorySeparatorChar));
    public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
}
