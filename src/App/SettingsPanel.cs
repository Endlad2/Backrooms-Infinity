using Godot;
using Backrooms.Engine.Input;
using Backrooms.Engine.Platform;

namespace Backrooms.Infinity.App;

public sealed partial class SettingsPanel : PanelContainer
{
    private GameSettings _settings;
    private readonly Action<GameSettings> _apply;
    private VBoxContainer _list = null!;
    private GameInputAction? _waiting;
    private readonly Dictionary<GameInputAction, Button> _bindingButtons = new();

    public SettingsPanel(GameSettings settings, Action<GameSettings> apply)
    {
        _settings = settings;
        _apply = apply;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(720, 470);
        AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle(new Color(0.025f,0.025f,0.02f,0.97f), 8));
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(680, 430),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_list);

        var title = GodotTheme.Label("НАСТРОЙКИ", 30, HorizontalAlignment.Center);
        _list.AddChild(title);
        AddCheck("VHS эффект", _settings.VhsEffect, v => Update(_settings with { VhsEffect = v }));
        AddSlider("Интенсивность VHS", 0, 1, 0.05, _settings.VhsIntensity, v => Update(_settings with { VhsIntensity = (float)v }));
        AddCheck("Покачивание камеры", _settings.HeadBob, v => Update(_settings with { HeadBob = v }));
        AddSlider("Сила покачивания", 0, 0.2, 0.005, _settings.HeadBobStrength, v => Update(_settings with { HeadBobStrength = (float)v }));
        AddSlider("Чувствительность мыши", 0.02, 1.0, 0.01, _settings.MouseSensitivity, v => Update(_settings with { MouseSensitivity = (float)v }));
        AddSlider("FOV", 55, 120, 1, _settings.FieldOfView, v => Update(_settings with { FieldOfView = (float)v }));
        AddSlider("Громкость", 0, 1, 0.05, _settings.MasterVolume, v => Update(_settings with { MasterVolume = (float)v }));
        AddSlider("Эмбиент", 0, 1, 0.05, _settings.AmbientVolume, v => Update(_settings with { AmbientVolume = (float)v }));
        AddCheck("Полный экран", _settings.Fullscreen, v => Update(_settings with { Fullscreen = v }));

        var controls = GodotTheme.Label("УПРАВЛЕНИЕ — нажми кнопку, затем новую клавишу", 20);
        controls.AddThemeColorOverride("font_color", GodotTheme.Accent);
        _list.AddChild(controls);
        AddBinding("Вперёд", GameInputAction.MoveForward);
        AddBinding("Назад", GameInputAction.MoveBackward);
        AddBinding("Влево", GameInputAction.MoveLeft);
        AddBinding("Вправо", GameInputAction.MoveRight);
        AddBinding("Спринт", GameInputAction.Sprint);
        AddBinding("Присесть", GameInputAction.Crouch);
        AddBinding("Инвентарь", GameInputAction.Inventory);
        AddBinding("Разобрать предмет", GameInputAction.Dismantle);
        AddBinding("Прыжок", GameInputAction.Jump);
        AddBinding("Дебаггер", GameInputAction.Debugger);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || _waiting is null || @event is not InputEventKey key || !key.Pressed || key.Echo)
            return;
        var physical = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (physical == Key.Escape)
        {
            _waiting = null;
            RefreshBindings();
            GetViewport().SetInputAsHandled();
            return;
        }
        _settings = _settings.WithBinding(_waiting.Value, InputBindingCodec.FromKey(physical));
        _settings.Save();
        _apply(_settings);
        _waiting = null;
        RefreshBindings();
        GetViewport().SetInputAsHandled();
    }

    private void AddCheck(string text, bool value, Action<bool> changed)
    {
        var box = new CheckBox { Text = text, ButtonPressed = value };
        GodotTheme.Apply(box, 19);
        box.Toggled += toggledOn => changed(toggledOn);
        _list.AddChild(box);
    }

    private void AddSlider(string text, double min, double max, double step, double value, Action<double> changed)
    {
        var row = new HBoxContainer();
        var label = GodotTheme.Label(text, 18); label.CustomMinimumSize = new Vector2(300, 0);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(260, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var valueLabel = GodotTheme.Label(value.ToString("0.##"), 16); valueLabel.CustomMinimumSize = new Vector2(70, 0);
        slider.ValueChanged += v => { valueLabel.Text = v.ToString("0.##"); changed(v); };
        row.AddChild(label); row.AddChild(slider); row.AddChild(valueLabel); _list.AddChild(row);
    }

    private void AddBinding(string labelText, GameInputAction action)
    {
        var row = new HBoxContainer();
        var label = GodotTheme.Label(labelText, 18); label.CustomMinimumSize = new Vector2(300, 0);
        var button = GodotTheme.Button(_settings.BindingFor(action), 240); button.CustomMinimumSize = new Vector2(240, 38);
        button.Pressed += () => { _waiting = action; button.Text = "НАЖМИ КЛАВИШУ..."; };
        _bindingButtons[action] = button;
        row.AddChild(label); row.AddChild(button); _list.AddChild(row);
    }

    private void RefreshBindings()
    {
        foreach (var pair in _bindingButtons) pair.Value.Text = _settings.BindingFor(pair.Key);
    }

    private void Update(GameSettings settings)
    {
        _settings = settings;
        _settings.Save();
        _apply(_settings);
    }
}
