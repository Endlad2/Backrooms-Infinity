namespace Backrooms.Launcher;

public sealed record LauncherMediaItem(string ImagePath, string AudioPath, string Label);

public sealed class LauncherMediaDeck
{
    private readonly LauncherMediaItem[] _items;
    private readonly double _itemSeconds;
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;

    public LauncherMediaDeck(string assetRoot, double itemSeconds = 18)
    {
        _itemSeconds = itemSeconds;
        _items =
        [
            new(Path.Combine(assetRoot, "level4.png"), Path.Combine(assetRoot, "Backrooms - Level 4.mp3"), "Level 4"),
            new(Path.Combine(assetRoot, "level8.png"), Path.Combine(assetRoot, "Backrooms - Level 8.mp3"), "Level 8"),
            new(Path.Combine(assetRoot, "level10.png"), Path.Combine(assetRoot, "Backrooms - Level 10.mp3"), "Level 10")
        ];
    }

    public LauncherMediaItem Current
    {
        get
        {
            var elapsed = (DateTimeOffset.UtcNow - _started).TotalSeconds;
            var index = (int)(elapsed / _itemSeconds) % _items.Length;
            return _items[index];
        }
    }

    public (double PanX, double Zoom) Motion
    {
        get
        {
            var elapsed = (DateTimeOffset.UtcNow - _started).TotalSeconds % _itemSeconds;
            var phase = elapsed / _itemSeconds;
            var triangle = phase < 0.5 ? phase * 2.0 : (1.0 - phase) * 2.0;
            var pan = -0.045 + triangle * 0.09; // right, then left
            var zoom = 1.08 + Math.Sin(phase * Math.PI) * 0.035;
            return (pan, zoom);
        }
    }
}
