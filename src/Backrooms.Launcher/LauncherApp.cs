using System.Collections.Concurrent;
using System.Text.Json;
using Backrooms.Engine.Logging;
using Backrooms.Engine.Platform;
using Backrooms.Engine.UI;
using Backrooms.Launcher.Catalog;
using Backrooms.Launcher.Generated;
using Backrooms.Launcher.Runtime;
using Backrooms.Launcher.UI;
using SDL3;

namespace Backrooms.Launcher;

public sealed class LauncherApp : IDisposable
{
    private enum Screen
    {
        Main,
        Catalog,
        Settings,
        Authors,
        Message,
        LoadingWorld
    }

    private sealed record Contributor(string Name, int Commits);

    private readonly LauncherOptions _options;
    private readonly RuntimeLogWriter _log;
    private readonly LevelCatalogService _catalog = new();
    private readonly ConcurrentQueue<string> _clientLogs = new();
    private readonly Dictionary<string, Task<string>> _previewTasks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _previewPaths = new(StringComparer.Ordinal);

    private IntPtr _window;
    private IntPtr _renderer;
    private NativeSdlUi? _ui;
    private AmbientPlayer? _ambient;
    private LauncherMediaDeck? _deck;
    private GameSettings _settings;
    private Screen _screen = Screen.Main;
    private string _message = string.Empty;
    private string _loadingLevelName = string.Empty;
    private string _loadingStage = string.Empty;
    private double? _downloadProgress;
    private Task<IReadOnlyList<LevelCatalogEntry>>? _catalogTask;
    private IReadOnlyList<LevelCatalogEntry> _levels = Array.Empty<LevelCatalogEntry>();
    private Task<string>? _levelDownloadTask;
    private LevelCatalogEntry? _pendingLevel;
    private ClientProcessSession? _clientSession;
    private Task<int>? _clientWaitTask;
    private bool _clientReady;
    private bool _windowHiddenForGame;
    private int _catalogPage;
    private int _authorsPage;
    private bool _running = true;
    private bool _previousMouseDown;
    private bool _sdlDisposed;
    private readonly IReadOnlyList<Contributor> _contributors;

    public LauncherApp(LauncherOptions options)
    {
        _options = options;
        AppPaths.EnsureCreated();
        _log = RuntimeLogWriter.Create("launcher");
        _settings = GameSettings.Load();
        if (!string.IsNullOrWhiteSpace(options.Nick))
            _settings = _settings with { Nickname = options.Nick };
        _contributors = ParseContributors();
        _log.Write("INFO", "Launcher", $"Application root: {AppPaths.Root}");
    }

    public int Run()
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio))
        {
            _log.Write("ERROR", "SDL3", $"SDL init failed: {SDL.GetError()}");
            return 3;
        }

        if (!SDL.CreateWindowAndRenderer("Backrooms Infinity", 1280, 720, SDL.WindowFlags.Resizable, out _window, out _renderer))
        {
            _log.Write("ERROR", "SDL3", $"Window creation failed: {SDL.GetError()}");
            SDL.Quit();
            return 4;
        }

        try
        {
            var font = GameTheme.VhsFontPath;
            _ui = new NativeSdlUi(_renderer, font);
            _ambient = new AmbientPlayer();
            var mediaRoot = Path.GetDirectoryName(AppPaths.ResolveAsset("launcher/level4.png"))!;
            _deck = new LauncherMediaDeck(mediaRoot);

            if (!string.IsNullOrWhiteSpace(_options.LevelZip) && File.Exists(_options.LevelZip))
            {
                _loadingLevelName = Path.GetFileNameWithoutExtension(_options.LevelZip);
                _loadingStage = "Запуск уровня из аргументов";
                _screen = Screen.LoadingWorld;
                StartClient(_options.LevelZip, _loadingLevelName);
            }

            while (_running)
            {
                PumpEvents();
                TickAsyncState();
                Render();
                Thread.Sleep(8);
            }

            return 0;
        }
        finally
        {
            DisposeSdlObjects();
        }
    }

    private void PumpEvents()
    {
        while (SDL.PollEvent(out var e))
        {
            var type = (SDL.EventType)e.Type;
            if (type == SDL.EventType.Quit)
            {
                _running = false;
                continue;
            }

            if (type == SDL.EventType.KeyDown && !e.Key.Repeat)
            {
                var scancode = (int)e.Key.Scancode;
                if (scancode == 41) // ESC
                {
                    if (_screen == Screen.Main) _running = false;
                    else if (_screen is not Screen.LoadingWorld) _screen = Screen.Main;
                }
            }
        }
    }

    private void TickAsyncState()
    {
        if (_catalogTask is { IsCompleted: true })
        {
            var task = _catalogTask;
            _catalogTask = null;
            if (task.IsCompletedSuccessfully)
            {
                _levels = task.Result;
                _catalogPage = 0;
                _log.Write("INFO", "Catalog", $"Loaded {_levels.Count} levels.");
                foreach (var level in _levels)
                    EnsurePreviewTask(level);
            }
            else
            {
                ShowMessage("Не удалось загрузить levels.db.zip\n" + (task.Exception?.GetBaseException().Message ?? "Неизвестная ошибка"));
            }
        }

        foreach (var pair in _previewTasks.ToArray())
        {
            if (!pair.Value.IsCompleted) continue;
            _previewTasks.Remove(pair.Key);
            if (pair.Value.IsCompletedSuccessfully)
                _previewPaths[pair.Key] = pair.Value.Result;
        }

        if (_levelDownloadTask is { IsCompleted: true })
        {
            var task = _levelDownloadTask;
            _levelDownloadTask = null;
            if (task.IsCompletedSuccessfully && _pendingLevel is not null)
            {
                _loadingStage = "Запуск клиента";
                StartClient(task.Result, _pendingLevel.Name);
            }
            else
            {
                ShowMessage("Не удалось скачать уровень\n" + (task.Exception?.GetBaseException().Message ?? "Неизвестная ошибка"));
            }
        }

        if (_clientReady && !_windowHiddenForGame && _clientSession is not null)
        {
            _clientReady = false;
            _windowHiddenForGame = true;
            SDL.HideWindow(_window);
        }

        if (_clientWaitTask is { IsCompleted: true })
        {
            var exit = _clientWaitTask.IsCompletedSuccessfully ? _clientWaitTask.Result : -1;
            _clientWaitTask = null;
            _clientSession?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _clientSession = null;
            if (_windowHiddenForGame)
            {
                SDL.ShowWindow(_window);
                _windowHiddenForGame = false;
            }

            if (exit == 0)
                _screen = Screen.Main;
            else
                ShowMessage($"Клиент завершился с кодом {exit}.\nСмотри логи в {AppPaths.Logs}");
        }
    }

    private void Render()
    {
        if (_ui is null || _deck is null || _ambient is null || _windowHiddenForGame)
            return;

        SDL.GetWindowSize(_window, out var width, out var height);
        width = Math.Max(width, 640);
        height = Math.Max(height, 480);

        var media = _deck.Current;
        var motion = _deck.Motion;
        var bg = _ui.LoadTexture(media.ImagePath);
        SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL.RenderClear(_renderer);
        _ui.DrawCover(bg, width, height, motion.PanX, motion.Zoom);
        _ambient.Play(media.AudioPath);

        var mouseButtons = SDL.GetMouseState(out var mouseX, out var mouseY);
        var mouseDown = (((uint)mouseButtons) & 1u) != 0;
        var clicked = mouseDown && !_previousMouseDown;
        _previousMouseDown = mouseDown;

        switch (_screen)
        {
            case Screen.Main:
                DrawMain(width, height, mouseX, mouseY, clicked);
                break;
            case Screen.Catalog:
                DrawCatalog(width, height, mouseX, mouseY, clicked);
                break;
            case Screen.Settings:
                DrawSettings(width, height, mouseX, mouseY, clicked);
                break;
            case Screen.Authors:
                DrawAuthors(width, height, mouseX, mouseY, clicked);
                break;
            case Screen.Message:
                DrawMessage(width, height, mouseX, mouseY, clicked);
                break;
            case Screen.LoadingWorld:
                DrawLoadingWorld(width, height);
                break;
        }

        SDL.RenderPresent(_renderer);
    }

    private void DrawMain(int width, int height, float mx, float my, bool clicked)
    {
        if (_ui is null) return;
        var buttonW = Math.Min(430f, width * 0.52f);
        const float buttonH = 58f;
        const float gap = 14f;
        var x = (width - buttonW) * 0.5f;
        var total = buttonH * 4 + gap * 3;
        var y = (height - total) * 0.5f;

        if (_ui.Button("Одиночная игра", 28, x, y, buttonW, buttonH, mx, my, clicked))
            OpenCatalog();
        y += buttonH + gap;
        if (_ui.Button("Сетевая игра", 28, x, y, buttonW, buttonH, mx, my, clicked))
            ShowMessage("Сетевая игра ещё не реализована.");
        y += buttonH + gap;
        if (_ui.Button("Настройки", 28, x, y, buttonW, buttonH, mx, my, clicked))
            _screen = Screen.Settings;
        y += buttonH + gap;
        if (_ui.Button("Авторы", 28, x, y, buttonW, buttonH, mx, my, clicked))
        {
            _authorsPage = 0;
            _screen = Screen.Authors;
        }
    }

    private void OpenCatalog()
    {
        _screen = Screen.Catalog;
        _levels = Array.Empty<LevelCatalogEntry>();
        _previewTasks.Clear();
        _previewPaths.Clear();
        _catalogTask = _catalog.LoadAsync();
        _log.Write("INFO", "Catalog", $"Downloading {LevelCatalogService.CatalogUri}");
    }

    private void DrawCatalog(int width, int height, float mx, float my, bool clicked)
    {
        if (_ui is null) return;
        _ui.Fill(28, 22, width - 56, height - 44, 10, 10, 10, 238);
        _ui.DrawText("Одиночная игра", 30, 52, 38);

        if (_catalogTask is not null)
        {
            DrawSpinner(width, height, "Загрузка списка уровней");
            return;
        }

        if (_levels.Count == 0)
        {
            var m = _ui.MeasureText("Список уровней пуст", 28);
            _ui.DrawText("Список уровней пуст", 28, (width - m.Width) / 2, height / 2f);
            return;
        }

        const int perPage = 6;
        var pageCount = Math.Max(1, (int)Math.Ceiling(_levels.Count / (double)perPage));
        _catalogPage = Math.Clamp(_catalogPage, 0, pageCount - 1);
        var start = _catalogPage * perPage;
        var end = Math.Min(start + perPage, _levels.Count);

        var margin = 52f;
        var gapX = 22f;
        var gapY = 22f;
        var cardW = (width - margin * 2 - gapX * 2) / 3f;
        var cardH = Math.Min(210f, (height - 150f - gapY) / 2f);
        var y0 = 90f;

        for (var i = start; i < end; i++)
        {
            var slot = i - start;
            var col = slot % 3;
            var row = slot / 3;
            var x = margin + col * (cardW + gapX);
            var y = y0 + row * (cardH + gapY);
            DrawLevelCard(_levels[i], x, y, cardW, cardH, mx, my, clicked);
        }

        var navY = height - 54f;
        if (_catalogPage > 0 && _ui.Button("<", 25, 52, navY, 60, 36, mx, my, clicked))
            _catalogPage--;
        var pageText = $"{_catalogPage + 1}/{pageCount}";
        var pm = _ui.MeasureText(pageText, 20);
        _ui.DrawText(pageText, 20, (width - pm.Width) / 2f, navY + 7);
        if (_catalogPage + 1 < pageCount && _ui.Button(">", 25, width - 112, navY, 60, 36, mx, my, clicked))
            _catalogPage++;
    }

    private void DrawLevelCard(LevelCatalogEntry level, float x, float y, float w, float h, float mx, float my, bool clicked)
    {
        if (_ui is null) return;
        var hovered = mx >= x && mx <= x + w && my >= y && my <= y + h;
        _ui.Fill(x, y, w, h, 22, 22, 22, 255);
        if (_previewPaths.TryGetValue(level.Name, out var preview) && File.Exists(preview))
            _ui.DrawTexture(_ui.LoadTexture(preview), x, y, w, h);
        else
            _ui.DrawText("...", 28, x + w / 2 - 18, y + h / 2 - 16);

        _ui.Fill(x, y + h - 46, w, 46, 0, 0, 0, 230);
        var nm = _ui.MeasureText(level.Name, 20);
        _ui.DrawText(level.Name, 20, x + w - nm.Width - 10, y + h - nm.Height - 9);
        if (level.IsInstalled)
            _ui.DrawText("Готов", 20, x + 10, y + 8, 90, 255, 100);
        if (hovered)
            _ui.Outline(x, y, w, h, 255, 255, 255);

        if (hovered && clicked)
            BeginLevel(level);
    }

    private void EnsurePreviewTask(LevelCatalogEntry level)
    {
        if (_previewTasks.ContainsKey(level.Name) || _previewPaths.ContainsKey(level.Name)) return;
        _previewTasks[level.Name] = _catalog.EnsurePreviewAsync(level);
    }

    private void BeginLevel(LevelCatalogEntry level)
    {
        _pendingLevel = level;
        _loadingLevelName = level.Name;
        _downloadProgress = null;
        _screen = Screen.LoadingWorld;
        if (level.IsInstalled)
        {
            _loadingStage = "Подготовка клиента";
            StartClient(level.LocalZipPath, level.Name);
            return;
        }

        _loadingStage = "Скачивание уровня";
        var progress = new Progress<(long Received, long? Total)>(p =>
        {
            _downloadProgress = p.Total is > 0 ? p.Received / (double)p.Total.Value : null;
        });
        _levelDownloadTask = _catalog.EnsureLevelAsync(level, progress);
    }

    private void StartClient(string levelZip, string levelName)
    {
        try
        {
            _loadingLevelName = levelName;
            _loadingStage = "Запуск клиента";
            _clientSession = ClientProcessSession.Start(
                levelZip,
                _settings.Nickname,
                _options.Skin,
                _settings.Width,
                _settings.Height,
                _settings.Fullscreen);
            _clientSession.StageChanged += stage => _loadingStage = stage;
            _clientSession.LogLine += line =>
            {
                _clientLogs.Enqueue(line);
                while (_clientLogs.Count > 8 && _clientLogs.TryDequeue(out _)) { }
                _log.Write("INFO", "Client", line);
            };
            _clientSession.Ready += () => _clientReady = true;
            _clientWaitTask = _clientSession.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            ShowMessage("Не удалось запустить клиент\n" + ex.Message);
        }
    }

    private void DrawLoadingWorld(int width, int height)
    {
        if (_ui is null) return;
        var panelW = Math.Min(width - 44f, 820f);
        var panelH = 250f;
        var x = (width - panelW) / 2f;
        var y = (height - panelH) / 2f;

        // Bedrock-style loading card: light frame, dark content and a thin green progress pulse.
        _ui.Fill(x, y, panelW, panelH, 196, 196, 196, 255);
        _ui.Outline(x, y, panelW, panelH, 92, 92, 92);

        var name = string.IsNullOrWhiteSpace(_loadingLevelName) ? "Backrooms Infinity" : _loadingLevelName;
        var nm = _ui.MeasureText(name, 29);
        _ui.DrawText(name, 29, x + (panelW - nm.Width) / 2f, y + 15, 80, 80, 80);

        var innerX = x + 16;
        var innerY = y + 55;
        var innerW = panelW - 32;
        var innerH = panelH - 71;
        _ui.Fill(innerX, innerY, innerW, innerH, 8, 8, 8, 255);
        _ui.Outline(innerX, innerY, innerW, innerH, 40, 40, 40);

        var stage = string.IsNullOrWhiteSpace(_loadingStage) ? "Подготовка мира" : _loadingStage;
        if (_downloadProgress.HasValue)
            stage += $"  {Math.Clamp(_downloadProgress.Value, 0, 1):P0}";
        var sm = _ui.MeasureText(stage, 22);
        _ui.DrawText(stage, 22, innerX + (innerW - sm.Width) / 2f, innerY + 38);

        var barX = innerX + 36;
        var barY = innerY + innerH - 44;
        var barW = innerW - 72;
        _ui.Fill(barX, barY, barW, 12, 24, 30, 24);
        if (_downloadProgress.HasValue)
        {
            _ui.Fill(barX, barY, barW * (float)Math.Clamp(_downloadProgress.Value, 0, 1), 12, 88, 118, 80);
        }
        else
        {
            var phase = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2200) / 2200f;
            var pulseW = barW * 0.24f;
            var px = barX + (barW - pulseW) * (phase < 0.5f ? phase * 2 : (1 - phase) * 2);
            _ui.Fill(px, barY, pulseW, 12, 88, 118, 80);
        }
    }

    private void DrawSpinner(int width, int height, string text)
    {
        if (_ui is null) return;
        var cx = width / 2f;
        var cy = height / 2f - 28f;
        var active = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 110 % 8);
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4.0;
            var x = cx + (float)Math.Cos(angle) * 24f - 4f;
            var y = cy + (float)Math.Sin(angle) * 24f - 4f;
            var distance = (i - active + 8) % 8;
            var shade = (byte)Math.Clamp(245 - distance * 22, 70, 245);
            _ui.Fill(x, y, 8, 8, shade, shade, shade);
        }

        var m = _ui.MeasureText(text, 26);
        _ui.DrawText(text, 26, (width - m.Width) / 2f, cy + 48f);
    }

    private void DrawSettings(int width, int height, float mx, float my, bool clicked)
    {
        if (_ui is null) return;
        var x = Math.Max(40f, (width - 700f) / 2f);
        var y = 70f;
        _ui.Fill(x - 30, 40, Math.Min(760, width - 40), height - 80, 12, 12, 12, 245);
        _ui.DrawText("Настройки", 32, x, y);
        y += 70;
        _ui.DrawText($"Ник: {_settings.Nickname}", 22, x, y);
        y += 42;
        _ui.DrawText($"Окно: {_settings.Width} x {_settings.Height}", 22, x, y);
        y += 42;
        _ui.DrawText($"Полный экран: {(_settings.Fullscreen ? "да" : "нет")}", 22, x, y);
        y += 55;
        if (_ui.Button("Переключить полный экран", 20, x, y, 360, 46, mx, my, clicked))
        {
            _settings = _settings with { Fullscreen = !_settings.Fullscreen };
            _settings.Save();
        }
        y += 72;
        _ui.DrawText("Папка данных:", 18, x, y, 185, 185, 185);
        _ui.DrawText(AppPaths.Root, 17, x, y + 28, 185, 185, 185);
    }

    private void DrawAuthors(int width, int height, float mx, float my, bool clicked)
    {
        if (_ui is null) return;
        var panelW = Math.Min(760f, width - 60f);
        var x = (width - panelW) / 2f;
        _ui.Fill(x, 40, panelW, height - 80, 12, 12, 12, 245);
        _ui.DrawText("Авторы", 34, x + 28, 66);
        if (_contributors.Count == 0)
        {
            _ui.DrawText("Нет данных о коммитах", 22, x + 28, 125);
            return;
        }

        const int perPage = 14;
        var pageCount = Math.Max(1, (int)Math.Ceiling(_contributors.Count / (double)perPage));
        _authorsPage = Math.Clamp(_authorsPage, 0, pageCount - 1);
        var start = _authorsPage * perPage;
        var end = Math.Min(start + perPage, _contributors.Count);
        var y = 125f;
        for (var i = start; i < end; i++)
        {
            var author = _contributors[i];
            _ui.DrawText(author.Name, 21, x + 32, y);
            var count = author.Commits.ToString();
            var cm = _ui.MeasureText(count, 21);
            _ui.DrawText(count, 21, x + panelW - cm.Width - 32, y, 180, 220, 180);
            y += 31;
        }

        if (pageCount > 1)
        {
            var navY = height - 84f;
            if (_authorsPage > 0 && _ui.Button("<", 22, x + 28, navY, 58, 34, mx, my, clicked))
                _authorsPage--;
            var page = $"{_authorsPage + 1}/{pageCount}";
            var pm = _ui.MeasureText(page, 18);
            _ui.DrawText(page, 18, x + (panelW - pm.Width) / 2f, navY + 7);
            if (_authorsPage + 1 < pageCount && _ui.Button(">", 22, x + panelW - 86, navY, 58, 34, mx, my, clicked))
                _authorsPage++;
        }
    }

    private void ShowMessage(string message)
    {
        _message = message;
        _screen = Screen.Message;
    }

    private void DrawMessage(int width, int height, float mx, float my, bool clicked)
    {
        if (_ui is null) return;
        var panelW = Math.Min(720f, width - 50f);
        var panelH = 240f;
        var x = (width - panelW) / 2f;
        var y = (height - panelH) / 2f;
        _ui.Fill(x, y, panelW, panelH, 12, 12, 12, 248);
        _ui.Outline(x, y, panelW, panelH, 210, 210, 210);
        var lines = _message.Replace("\r", "").Split('\n');
        var ty = y + 34;
        foreach (var line in lines.Take(5))
        {
            var m = _ui.MeasureText(line, 20);
            _ui.DrawText(line, 20, x + (panelW - m.Width) / 2f, ty);
            ty += 31;
        }
        if (_ui.Button("OK", 22, x + panelW / 2f - 65, y + panelH - 64, 130, 42, mx, my, clicked))
            _screen = Screen.Main;
    }

    private static IReadOnlyList<Contributor> ParseContributors()
    {
        try
        {
            return JsonSerializer.Deserialize<List<Contributor>>(GeneratedContributors.Json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<Contributor>();
        }
        catch
        {
            return Array.Empty<Contributor>();
        }
    }

    private void DisposeSdlObjects()
    {
        if (_sdlDisposed) return;
        _sdlDisposed = true;
        if (_clientSession is not null)
        {
            try { _clientSession.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
            _clientSession = null;
        }
        _ambient?.Dispose();
        _ambient = null;
        _ui?.Dispose();
        _ui = null;
        if (_renderer != IntPtr.Zero) SDL.DestroyRenderer(_renderer);
        if (_window != IntPtr.Zero) SDL.DestroyWindow(_window);
        _renderer = IntPtr.Zero;
        _window = IntPtr.Zero;
        SDL.Quit();
    }

    public void Dispose()
    {
        DisposeSdlObjects();
        _log.Dispose();
    }
}
