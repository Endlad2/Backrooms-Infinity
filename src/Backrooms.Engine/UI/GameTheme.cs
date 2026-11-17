using Backrooms.Engine.Platform;

namespace Backrooms.Engine.UI;

/// <summary>Single visual text policy shared by launcher, client UI and debug UI backends.</summary>
public static class GameTheme
{
    public const string VhsFontFileName = "vcrosdmonorus_vhsicons.ttf";
    public static string VhsFontPath => AppPaths.ResolveAsset(VhsFontFileName);
}
