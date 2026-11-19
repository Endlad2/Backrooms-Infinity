using Godot;
using Backrooms.Engine.Diagnostics;

namespace Backrooms.Infinity.App;

public sealed partial class DebuggerOverlay : CanvasLayer
{
    private readonly DiagnosticBus _bus;
    private readonly DebugOverlayState _state;
    private PanelContainer _panel = null!;
    private RichTextLabel _log = null!;
    private Label _stats = null!;
    private Func<string>? _statsProvider;
    private DiagnosticSeverity? _filter;

    public DebuggerOverlay(DiagnosticBus bus, DebugOverlayState state)
    {
        _bus = bus;
        _state = state;
        Layer = 200;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        _panel = new PanelContainer
        {
            Position = new Vector2(22, 22),
            Size = new Vector2(820, 520),
            Visible = _state.IsOpen
        };
        _panel.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle(new Color(0.015f,0.015f,0.012f,0.96f), 4));
        AddChild(_panel);

        var root = new VBoxContainer();
        _panel.AddChild(root);
        var titleRow = new HBoxContainer();
        root.AddChild(titleRow);
        var title = GodotTheme.Label("BACKROOMS DEBUGGER  [F3]", 24);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        foreach (var (text, filter) in new (string, DiagnosticSeverity? )[] { ("ALL", null), ("INFO", DiagnosticSeverity.Info), ("WARN", DiagnosticSeverity.Warning), ("ERR", DiagnosticSeverity.Error) })
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(74, 32) };
            GodotTheme.Apply(b, 16);
            b.Pressed += () => { _filter = filter; Refresh(); };
            titleRow.AddChild(b);
        }

        _stats = GodotTheme.Label("", 16);
        root.AddChild(_stats);
        _log = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(780, 420),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        GodotTheme.Apply(_log, 16);
        root.AddChild(_log);

        _bus.EntryAdded += OnEntry;
        Refresh();
    }

    public override void _ExitTree() => _bus.EntryAdded -= OnEntry;

    public override void _Process(double delta)
    {
        _panel.Visible = _state.IsOpen;
        if (_panel.Visible && _statsProvider is not null)
            _stats.Text = _statsProvider();
    }

    public void SetStatsProvider(Func<string>? provider) => _statsProvider = provider;

    private void OnEntry(DiagnosticEntry entry)
    {
        if (entry.Severity == DiagnosticSeverity.Error)
            _state.Open();
        Refresh();
    }


    private void Refresh()
    {
        if (_log is null) return;
        _log.Clear();
        foreach (var e in _bus.Snapshot().Where(e => _filter is null || e.Severity == _filter).TakeLast(400))
        {
            var color = e.Severity switch
            {
                DiagnosticSeverity.Error => "ef7373",
                DiagnosticSeverity.Warning => "e6bd62",
                _ => "bdb99c"
            };
            _log.AppendText($"[color=#{color}]{e.Timestamp:HH:mm:ss} [{e.Severity}] {Escape(e.Source)}: {Escape(e.Message)}[/color]\n");
            if (!string.IsNullOrWhiteSpace(e.Detail))
                _log.AppendText($"[color=#777466]{Escape(e.Detail!)}[/color]\n");
        }
        _log.ScrollToLine(Math.Max(0, _log.GetLineCount() - 1));
    }

    private static string Escape(string s) => s.Replace("[", "[lb]");
}
