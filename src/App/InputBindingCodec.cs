using Godot;

namespace Backrooms.Infinity.App;

public static class InputBindingCodec
{
    public static Key ToKey(string value) => value switch
    {
        "LeftCtrl" => Key.Ctrl,
        "LeftShift" => Key.Shift,
        "LeftAlt" => Key.Alt,
        "Escape" => Key.Escape,
        "Space" => Key.Space,
        _ => Enum.TryParse<Key>(value, true, out var key) ? key : Key.None
    };

    public static string FromKey(Key key) => key switch
    {
        Key.Ctrl => "LeftCtrl",
        Key.Shift => "LeftShift",
        Key.Alt => "LeftAlt",
        Key.Escape => "Escape",
        Key.Space => "Space",
        _ => key.ToString()
    };

    public static bool Pressed(string value)
    {
        if (string.Equals(value, "MouseLeft", StringComparison.OrdinalIgnoreCase))
            return Input.IsMouseButtonPressed(MouseButton.Left);
        var key = ToKey(value);
        return key != Key.None && Input.IsKeyPressed(key);
    }
}
