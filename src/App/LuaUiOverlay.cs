using Godot;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Scripting;
using Backrooms.Engine.UI;

namespace Backrooms.Infinity.App;

public sealed partial class LuaUiOverlay : CanvasLayer
{
    private readonly UiState _state;
    private readonly LevelPackage _package;
    private readonly LevelExtraction _extraction;
    private readonly LevelEventRuntime _events;
    private readonly Dictionary<string, Control> _controls = new(StringComparer.Ordinal);
    private double _timer;

    public LuaUiOverlay(UiState state, LevelPackage package, LevelExtraction extraction, LevelEventRuntime events)
    {
        _state = state;
        _package = package;
        _extraction = extraction;
        _events = events;
        Layer = 45;
    }

    public override void _Process(double delta)
    {
        _timer += delta;
        if (_timer < 0.15) return;
        _timer = 0;
        Sync();
    }

    private void Sync()
    {
        var alive = new HashSet<string>(StringComparer.Ordinal);
        var y = 90f;
        foreach (var node in _state.Nodes)
        {
            alive.Add(node.Id);
            if (!_controls.TryGetValue(node.Id, out var control))
            {
                control = Create(node);
                _controls[node.Id] = control;
                AddChild(control);
            }
            control.Visible = node.Visible;
            control.Position = new Vector2(24, y);
            y += 42;
            switch (node)
            {
                case UiText text when control is Label label: label.Text = text.Text; break;
                case UiButton button when control is Button b: b.Text = button.Label; break;
                case UiImage image when control is TextureRect tr: tr.Texture ??= LoadImage(image.Source); break;
                case UiProgress progress when control is ProgressBar pb: pb.MaxValue = Math.Max(1, progress.Max); pb.Value = progress.Value; break;
            }
        }
        foreach (var stale in _controls.Keys.Where(k => !alive.Contains(k)).ToArray())
        {
            _controls[stale].QueueFree();
            _controls.Remove(stale);
        }
    }

    private Control Create(UiNode node)
    {
        Control control = node switch
        {
            UiButton b => MakeButton(b.Id, b.Label),
            UiImage => new TextureRect { CustomMinimumSize = new Vector2(240, 140), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspect },
            UiProgress p => new ProgressBar { CustomMinimumSize = new Vector2(320, 26), MaxValue = Math.Max(1, p.Max), Value = p.Value },
            UiText t => GodotTheme.Label(t.Text, 18),
            _ => GodotTheme.Label(node.Id, 18)
        };
        return control;
    }

    private Button MakeButton(string id, string label)
    {
        var b = GodotTheme.Button(label, 300);
        b.CustomMinimumSize = new Vector2(300, 38);
        b.Pressed += () => _events.Emit("click", id);
        return b;
    }

    private Texture2D? LoadImage(string relative)
    {
        try
        {
            var path = _extraction.PathFor(relative);
            var image = Image.LoadFromFile(path);
            return image is null || image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
        }
        catch { return null; }
    }
}
