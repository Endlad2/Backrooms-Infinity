namespace Backrooms.Launcher.Catalog;

public sealed record LevelCatalogEntry(string DownloadLink, string Preview, string Name)
{
    public string LocalZipPath => Backrooms.Engine.Platform.AppPaths.LevelZipPath(Name);
    public bool IsInstalled => File.Exists(LocalZipPath);
}
