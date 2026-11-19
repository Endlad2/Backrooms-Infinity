using Godot;
using System.Text.Json;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Logging;
using Backrooms.Engine.Platform;

namespace Backrooms.Infinity.App;

public sealed partial class MainController : Node
{
    private readonly DiagnosticBus _diagnostics = new();
    private readonly DebugOverlayState _debugState;
    private RuntimeLogWriter? _log;
    private GameSettings _settings = new();
    private Control? _screen;
    private MenuBackdrop? _backdrop;
    private LevelCatalogService? _catalog;
    private DebuggerOverlay? _debugger;
    private GameWorld? _game;
    private LevelPackage? _package;
    private LevelExtraction? _extraction;
    private CancellationTokenSource? _screenCts;

    public MainController() => _debugState = new DebugOverlayState(_diagnostics);

    public override void _Ready()
    {
        AppPaths.EnsureCreated();
        _settings = GameSettings.Load();
        _log = RuntimeLogWriter.Create("godot");
        _diagnostics.EntryAdded += OnDiagnostic;
        _diagnostics.Info("Startup", "Backrooms Infinity unified Godot .NET runtime starting.");
        _diagnostics.Info("Startup", $"User data: {AppPaths.Root}");
        ApplyGlobalSettings(_settings);
        _debugger = new DebuggerOverlay(_diagnostics, _debugState); AddChild(_debugger);
        ShowMainMenu();
    }

    public override void _ExitTree()
    {
        _screenCts?.Cancel();
        _catalog?.Dispose();
        CleanupGame();
        _diagnostics.EntryAdded -= OnDiagnostic;
        _log?.Dispose();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            var physical = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
            if (string.Equals(InputBindingCodec.FromKey(physical), _settings.DebuggerKey, StringComparison.OrdinalIgnoreCase))
            {
                _debugState.Toggle();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void ShowMainMenu()
    {
        CleanupGame();
        BeginScreen();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _backdrop = new MenuBackdrop(_settings); _screen!.AddChild(_backdrop);

        var title = GodotTheme.Label("BACKROOMS INFINITY", 46, HorizontalAlignment.Center);
        title.AnchorLeft = 0.5f; title.AnchorRight = 0.5f; title.AnchorTop = 0.14f; title.AnchorBottom = 0.14f; title.OffsetLeft = -500; title.OffsetRight = 500; title.OffsetTop = -40; title.OffsetBottom = 40;
        title.AddThemeColorOverride("font_color", new Color(0.93f,0.90f,0.69f)); _screen.AddChild(title);

        var center = new CenterContainer(); GodotTheme.FullRect(center); _screen.AddChild(center);
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle(new Color(0.02f,0.02f,0.016f,0.82f), 8)); center.AddChild(panel);
        var menu = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) }; panel.AddChild(menu);
        var single = GodotTheme.Button("ОДИНОЧНАЯ ИГРА", 420); single.Pressed += () => _ = ShowLevelBrowserAsync(); menu.AddChild(single);
        var multi = GodotTheme.Button("СЕТЕВАЯ ИГРА", 420); multi.Pressed += () => ShowMessage("СЕТЕВАЯ ИГРА", "Пока не реализовано."); menu.AddChild(multi);
        var settings = GodotTheme.Button("НАСТРОЙКИ", 420); settings.Pressed += ShowMainSettings; menu.AddChild(settings);
        var authors = GodotTheme.Button("АВТОРЫ", 420); authors.Pressed += ShowAuthors; menu.AddChild(authors);

        var hint = GodotTheme.Label("F3 — debugger   •   ZIP/XML/Lua levels   •   Godot 4.7.2 .NET", 16, HorizontalAlignment.Center);
        hint.AnchorLeft = 0; hint.AnchorRight = 1; hint.AnchorTop = 1; hint.AnchorBottom = 1; hint.OffsetTop = -44; hint.OffsetBottom = -12; _screen.AddChild(hint);
    }

    private async Task ShowLevelBrowserAsync()
    {
        BeginScreen();
        _backdrop = new MenuBackdrop(_settings); _screen!.AddChild(_backdrop);
        var root = new VBoxContainer { AnchorLeft = 0.05f, AnchorTop = 0.06f, AnchorRight = 0.95f, AnchorBottom = 0.94f };
        _screen.AddChild(root);
        var top = new HBoxContainer(); root.AddChild(top);
        var back = GodotTheme.Button("← НАЗАД", 180); back.CustomMinimumSize = new Vector2(180,44); back.Pressed += ShowMainMenu; top.AddChild(back);
        var heading = GodotTheme.Label("ОДИНОЧНАЯ ИГРА", 32, HorizontalAlignment.Center); heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(heading);
        var status = GodotTheme.Label("Загрузка levels.db.zip…", 18, HorizontalAlignment.Right); status.CustomMinimumSize = new Vector2(300,0); top.AddChild(status);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(scroll);
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(grid);

        try
        {
            _catalog ??= new LevelCatalogService();
            var levels = await _catalog.LoadAsync(_screenCts!.Token);
            if (_screenCts.IsCancellationRequested) return;
            status.Text = $"Уровней: {levels.Count}";
            foreach (var level in levels)
            {
                var card = BuildLevelCard(level, out var preview);
                grid.AddChild(card);
                _ = LoadPreviewAsync(level, preview, _screenCts.Token);
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Catalog", "Не удалось загрузить каталог уровней.", ex.ToString());
            status.Text = "Ошибка каталога — F3";
        }
    }

    private Control BuildLevelCard(LevelCatalogEntry level, out TextureRect preview)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(340, 210) };
        card.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle(new Color(0.02f,0.02f,0.016f,0.88f), 5));
        var root = new Control { CustomMinimumSize = new Vector2(320,190), ClipContents = true }; card.AddChild(root);
        preview = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore };
        GodotTheme.FullRect(preview); root.AddChild(preview);
        var shade = new ColorRect { Color = new Color(0,0,0,0.18f), MouseFilter = Control.MouseFilterEnum.Ignore }; GodotTheme.FullRect(shade); root.AddChild(shade);
        var name = GodotTheme.Label(level.Name, 20, HorizontalAlignment.Right); name.AnchorLeft = 0; name.AnchorRight = 1; name.AnchorTop = 1; name.AnchorBottom = 1; name.OffsetLeft = 12; name.OffsetRight = -12; name.OffsetTop = -44; name.OffsetBottom = -10; name.AddThemeColorOverride("font_color", Colors.White); root.AddChild(name);
        if (level.IsInstalled)
        {
            var ready = GodotTheme.Label("ГОТОВ", 17); ready.Position = new Vector2(12,10); ready.AddThemeColorOverride("font_color", GodotTheme.Good); root.AddChild(ready);
        }
        var click = new Button { Flat = true }; GodotTheme.FullRect(click); click.Pressed += () => _ = StartLevelAsync(level); root.AddChild(click);
        return card;
    }

    private async Task LoadPreviewAsync(LevelCatalogEntry level, TextureRect target, CancellationToken ct)
    {
        try
        {
            if (_catalog is null) return;
            var path = await _catalog.EnsurePreviewAsync(level, ct);
            if (ct.IsCancellationRequested || !IsInstanceValid(target)) return;
            var image = Image.LoadFromFile(path);
            if (image is not null && !image.IsEmpty()) target.Texture = ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex) { _diagnostics.Warn("Catalog", $"Preview failed: {level.Name}", ex.Message); }
    }

    private async Task StartLevelAsync(LevelCatalogEntry level)
    {
        ShowLoading(level.Name, "Подготовка загрузки…", 0.02f, out var stage, out var progress);
        try
        {
            _catalog ??= new LevelCatalogService();
            string path;
            try
            {
                stage.Text = level.IsInstalled ? "Проверяем локальный архив…" : "Скачиваем архив уровня…";
                path = await _catalog.EnsureLevelAsync(level, null, _screenCts!.Token);
                progress.Value = 40;
            }
            catch (Exception downloadError)
            {
                path = TryBundledLevel(level.Name) ?? throw new InvalidOperationException("Не удалось скачать уровень и локальной копии нет.", downloadError);
                _diagnostics.Warn("Level", "Использована bundled-копия уровня из репозитория.");
            }
            if (_screenCts!.IsCancellationRequested) return;
            stage.Text = "Парсим level.xml…"; progress.Value = 48;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var parser = new LevelParser(_diagnostics);
            _package = LevelPackage.Open(path, parser);
            stage.Text = "Распаковываем runtime-ресурсы…"; progress.Value = 62;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _extraction = new LevelExtraction(_package);
            stage.Text = "Lua init / стратегия генерации…"; progress.Value = 76;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _game = new GameWorld(_package, _extraction, _diagnostics, _settings);
            _game.ReturnToMenuRequested += ShowMainMenu;
            stage.Text = "Создаём чанки и PBR-сцену…"; progress.Value = 91;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ClearScreen();
            AddChild(_game);
            _debugger?.SetStatsProvider(() => _game?.DebugStats ?? "menu");
            progress.Value = 100;
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Level", $"Не удалось запустить '{level.Name}'.", ex.ToString());
            ShowMessage("ОШИБКА УРОВНЯ", $"{level.Name}\n{ex.Message}\n\nОткрыт debugger (F3).", ShowMainMenu);
        }
    }

    private void ShowLoading(string levelName, string initial, float value, out Label stage, out ProgressBar progress)
    {
        BeginScreen();
        _backdrop = new MenuBackdrop(_settings); _screen!.AddChild(_backdrop);
        var center = new CenterContainer(); GodotTheme.FullRect(center); _screen.AddChild(center);
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle(new Color(0.015f,0.015f,0.012f,0.92f), 4)); center.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(700, 180) }; panel.AddChild(box);
        box.AddChild(GodotTheme.Label(levelName.ToUpperInvariant(), 32, HorizontalAlignment.Center));
        stage = GodotTheme.Label(initial, 19, HorizontalAlignment.Center); box.AddChild(stage);
        progress = new ProgressBar { MinValue = 0, MaxValue = 100, Value = value * 100, CustomMinimumSize = new Vector2(650, 30), ShowPercentage = false }; box.AddChild(progress);
    }

    private void ShowMainSettings()
    {
        BeginScreen();
        _backdrop = new MenuBackdrop(_settings); _screen!.AddChild(_backdrop);
        var center = new CenterContainer(); GodotTheme.FullRect(center); _screen.AddChild(center);
        var outer = new VBoxContainer(); center.AddChild(outer);
        var panel = new SettingsPanel(_settings, s => { _settings = s; ApplyGlobalSettings(s); _backdrop?.ApplySettings(s); }); outer.AddChild(panel);
        var back = GodotTheme.Button("НАЗАД", 260); back.Pressed += ShowMainMenu; outer.AddChild(back);
    }

    private void ShowAuthors()
    {
        BeginScreen();
        _backdrop = new MenuBackdrop(_settings); _screen!.AddChild(_backdrop);
        var center = new CenterContainer(); GodotTheme.FullRect(center); _screen.AddChild(center);
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle()); center.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(650, 430) }; panel.AddChild(box);
        box.AddChild(GodotTheme.Label("АВТОРЫ", 32, HorizontalAlignment.Center));
        var text = GodotTheme.Label("Backrooms Infinity\nGitHub: Endlad2/Backrooms-Infinity\n\nЛицензия проекта: PolyForm Noncommercial 1.0.0\n\nVCR OSD Mono / RUS / VHS icons:\nMrManet • Daymarius • DENIRO_pro", 20, HorizontalAlignment.Center);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.AddChild(text);
        var back = GodotTheme.Button("НАЗАД", 260); back.Pressed += ShowMainMenu; box.AddChild(back);
    }

    private void ShowMessage(string title, string text, Action? close = null)
    {
        BeginScreen();
        _backdrop = new MenuBackdrop(_settings); _screen!.AddChild(_backdrop);
        var center = new CenterContainer(); GodotTheme.FullRect(center); _screen.AddChild(center);
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle()); center.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) }; panel.AddChild(box);
        box.AddChild(GodotTheme.Label(title, 30, HorizontalAlignment.Center));
        var body = GodotTheme.Label(text, 19, HorizontalAlignment.Center); body.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.AddChild(body);
        var b = GodotTheme.Button("OK", 240); b.Pressed += () => (close ?? ShowMainMenu).Invoke(); box.AddChild(b);
    }

    private void ClearScreen()
    {
        _screenCts?.Cancel(); _screenCts?.Dispose(); _screenCts = null;
        if (_screen is not null && IsInstanceValid(_screen)) _screen.QueueFree();
        _screen = null;
        _backdrop = null;
    }

    private void BeginScreen()
    {
        ClearScreen();
        _screenCts = new CancellationTokenSource();
        _screen = new Control(); GodotTheme.FullRect(_screen); AddChild(_screen);
    }

    private void CleanupGame()
    {
        if (_game is not null && IsInstanceValid(_game))
        {
            _game.ReturnToMenuRequested -= ShowMainMenu; _game.QueueFree(); _game = null;
        }
        _package?.Dispose(); _package = null;
        _extraction?.Dispose(); _extraction = null;
        _debugger?.SetStatsProvider(null);
    }

    private string? TryBundledLevel(string displayName)
    {
        var digits = new string(displayName.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits)) return null;
        var path = ProjectSettings.GlobalizePath($"res://levels/Level-{digits}.zip");
        return File.Exists(path) ? path : null;
    }

    private void ApplyGlobalSettings(GameSettings settings)
    {
        _settings = settings;
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(Mathf.Max(0.0001f, settings.MasterVolume)));
        DisplayServer.WindowSetMode(settings.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        if (!settings.Fullscreen) DisplayServer.WindowSetSize(new Vector2I(settings.Width, settings.Height));
    }

    private void OnDiagnostic(DiagnosticEntry e)
    {
        _log?.Write(e.Severity.ToString().ToUpperInvariant(), e.Source, e.Message + (string.IsNullOrWhiteSpace(e.Detail) ? "" : " | " + e.Detail));
    }
}
