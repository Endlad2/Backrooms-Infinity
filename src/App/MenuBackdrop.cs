using Godot;
using Backrooms.Engine.Platform;

namespace Backrooms.Infinity.App;

public sealed partial class MenuBackdrop : Control
{
    private readonly string[] _images = { "level4.png", "level8.png", "level10.png" };
    private readonly string[] _audio = { "Backrooms - Level 4.mp3", "Backrooms - Level 8.mp3", "Backrooms - Level 10.mp3" };
    private TextureRect _picture = null!;
    private AudioStreamPlayer _player = null!;
    private int _index;
    private double _phase;
    private double _hold;
    private bool _forward = true;
    private GameSettings _settings;

    public MenuBackdrop(GameSettings settings)
    {
        _settings = settings;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        GodotTheme.FullRect(this);
        _picture = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            PivotOffset = new Vector2(640, 360)
        };
        GodotTheme.FullRect(_picture);
        AddChild(_picture);

        var tint = new ColorRect { Color = new Color(0.02f, 0.02f, 0.015f, 0.40f), MouseFilter = MouseFilterEnum.Ignore };
        GodotTheme.FullRect(tint);
        AddChild(tint);

        _player = new AudioStreamPlayer { Bus = "Master" };
        AddChild(_player);
        SwitchMedia();
    }

    public void ApplySettings(GameSettings settings)
    {
        _settings = settings;
        _player.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, settings.AmbientVolume * settings.MasterVolume));
    }

    public override void _Process(double delta)
    {
        _phase += delta * 0.055 * (_forward ? 1 : -1);
        _phase = Mathf.Clamp((float)_phase, 0f, 1f);
        var x = Mathf.Lerp(-52f, 52f, (float)_phase);
        var zoom = 1.08f + Mathf.Sin((float)(_hold * 0.35)) * 0.018f;
        _picture.Position = new Vector2(x, 0);
        _picture.Scale = new Vector2(zoom, zoom);
        _hold += delta;

        if ((_forward && _phase >= 1) || (!_forward && _phase <= 0))
        {
            if (_hold >= 13.0)
            {
                _hold = 0;
                if (_forward)
                {
                    _forward = false;
                }
                else
                {
                    _forward = true;
                    _index = (_index + 1) % _images.Length;
                    SwitchMedia();
                }
            }
        }
    }

    private void SwitchMedia()
    {
        _picture.Texture = GD.Load<Texture2D>($"res://assets/menu/{_images[_index]}");
        _player.Stop();
        _player.Stream = GD.Load<AudioStream>($"res://assets/menu/{_audio[_index]}");
        _player.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, _settings.AmbientVolume * _settings.MasterVolume));
        if (_player.Stream is not null)
            _player.Play();
    }
}
