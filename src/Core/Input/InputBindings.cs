namespace Backrooms.Engine.Input;

public enum GameInputAction
{
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    Sprint,
    Crouch,
    Inventory,
    Dismantle,
    Interact,
    Pause,
    Debugger,
    Jump
}

/// <summary>
/// Canonical, platform-neutral key names persisted in settings.json.
/// Godot-specific input code translates physical keys to these canonical names.
/// </summary>
public static class InputBindingNames
{
    public const string W = "W";
    public const string A = "A";
    public const string S = "S";
    public const string D = "D";
    public const string E = "E";
    public const string Q = "Q";
    public const string Space = "Space";
    public const string Escape = "Escape";
    public const string F3 = "F3";
    public const string LeftCtrl = "LeftCtrl";
    public const string LeftShift = "LeftShift";
    public const string LeftAlt = "LeftAlt";
    public const string MouseLeft = "MouseLeft";

    public static string Normalize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        return value.Trim();
    }
}
