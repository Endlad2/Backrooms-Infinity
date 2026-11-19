using Godot;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Gameplay;
using Backrooms.Engine.Generation;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Mathematics;
using Backrooms.Engine.Platform;
using Backrooms.Engine.Runtime;
using Backrooms.Engine.Scripting;
using Backrooms.Engine.UI;

namespace Backrooms.Infinity.App;

public sealed partial class GameWorld : Node3D
{
    private readonly LevelPackage _package;
    private readonly LevelExtraction _extraction;
    private readonly DiagnosticBus _diagnostics;
    private GameSettings _settings;
    private readonly RuntimeWorld _runtimeWorld;
    private readonly GeneratorPlan _generator = new();
    private readonly ChunkPlanner _planner = new();
    private readonly GodotRendererBridge _renderer;
    private readonly UiState _luaUi = new();
    private readonly LuaHost _lua;
    private readonly LevelEventRuntime _events;
    private readonly GameSession _session;
    private readonly Dictionary<string, Node3D> _chunks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Node3D> _runtimeNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Node3D> _placedVisuals = new();
    private readonly Dictionary<string, Node3D> _monsterVisuals = new(StringComparer.Ordinal);

    private Node3D _chunkRoot = null!;
    private CharacterBody3D _player = null!;
    private Camera3D _camera = null!;
    private RayCast3D _ray = null!;
    private InventoryUi _inventoryUi = null!;
    private CanvasLayer _hud = null!;
    private CanvasLayer _vhsLayer = null!;
    private ShaderMaterial _vhsMaterial = null!;
    private Label _timestamp = null!;
    private Label _notification = null!;
    private Control _pauseRoot = null!;
    private SettingsPanel _settingsPanel = null!;
    private bool _paused;
    private float _yaw;
    private float _pitch;
    private double _streamTimer;
    private double _headBobPhase;
    private string? _hoverEntity;
    private double _notificationUntil;

    public event Action? ReturnToMenuRequested;
    public GameSession Session => _session;
    public int LoadedChunkCount => _chunks.Count;
    public string DebugStats => $"FPS: {Godot.Engine.GetFramesPerSecond()}   Level: {_package.Definition.Name}   Chunks: {_chunks.Count}   Pos: {_player?.GlobalPosition.ToString() ?? "-"}   Monsters: {_session.Monsters.Monsters.Count}";

    public GameWorld(LevelPackage package, LevelExtraction extraction, DiagnosticBus diagnostics, GameSettings settings)
    {
        _package = package;
        _extraction = extraction;
        _diagnostics = diagnostics;
        _settings = settings;
        _runtimeWorld = new RuntimeWorld(diagnostics);
        _renderer = new GodotRendererBridge(diagnostics);
        _lua = new LuaHost(diagnostics, _runtimeWorld, _luaUi, _renderer, _generator);
        _lua.RunInit(package);
        _events = new LevelEventRuntime(_lua, package);
        _session = new GameSession(package.Definition, settings, diagnostics);
        _session.Notification += Notify;
    }

    public override void _Ready()
    {
        Name = "GameWorld";
        BuildEnvironment();
        BuildPlayer();
        _chunkRoot = new Node3D { Name = "Chunks" }; AddChild(_chunkRoot);
        BuildHud();
        BuildPauseMenu();
        AddChild(new LuaUiOverlay(_luaUi, _package, _extraction, _events));
        _inventoryUi = new InventoryUi(_session); AddChild(_inventoryUi);
        UpdateChunks(force: true);
        BuildMonsterVisuals();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _diagnostics.Info("Godot", $"Game world ready. Renderer={_renderer.BackendName}; generator={_generator.Mode}.");
    }

    public override void _ExitTree()
    {
        _session.Notification -= Notify;
        _renderer.Dispose();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_paused || _inventoryUi.InventoryOpen || _inventoryUi.WorkbenchOpen) return;

        var forward = InputBindingCodec.Pressed(_settings.MoveForwardKey);
        var backward = InputBindingCodec.Pressed(_settings.MoveBackwardKey);
        var left = InputBindingCodec.Pressed(_settings.MoveLeftKey);
        var right = InputBindingCodec.Pressed(_settings.MoveRightKey);
        var sprint = InputBindingCodec.Pressed(_settings.SprintKey);
        var crouch = InputBindingCodec.Pressed(_settings.CrouchKey);
        var jump = InputBindingCodec.Pressed(_settings.JumpKey);

        var input = Vector2.Zero;
        if (forward) input.Y -= 1;
        if (backward) input.Y += 1;
        if (left) input.X -= 1;
        if (right) input.X += 1;
        input = input.LengthSquared() > 1 ? input.Normalized() : input;

        var speed = crouch ? 2.1f : sprint ? 7.4f : 4.3f;
        var basis = _player.GlobalTransform.Basis;
        var dir = (basis.X * input.X + basis.Z * input.Y); dir.Y = 0;
        if (dir.LengthSquared() > 0.001f) dir = dir.Normalized();
        var velocity = _player.Velocity;
        velocity.X = dir.X * speed; velocity.Z = dir.Z * speed;
        if (!_player.IsOnFloor()) velocity.Y -= 18f * (float)delta;
        else if (jump) velocity.Y = 5.2f;
        _player.Velocity = velocity;
        _player.MoveAndSlide();

        var moving = dir.LengthSquared() > 0.01f;
        var targetHeight = crouch ? 1.10f : 1.65f;
        var camPos = _camera.Position;
        camPos.Y = Mathf.Lerp(camPos.Y, targetHeight, (float)delta * 10f);
        if (_settings.HeadBob && moving && _player.IsOnFloor())
        {
            _headBobPhase += delta * (sprint ? 13.5 : crouch ? 6.0 : 9.0);
            camPos.Y += Mathf.Sin((float)(_headBobPhase * 2.0)) * _settings.HeadBobStrength;
            camPos.X = Mathf.Sin((float)_headBobPhase) * _settings.HeadBobStrength * 0.55f;
        }
        else camPos.X = Mathf.Lerp(camPos.X, 0, (float)delta * 8f);
        _camera.Position = camPos;

        _session.Player.Position = ToVec3(_player.GlobalPosition);
        _session.Player.YawDegrees = Mathf.RadToDeg(_yaw);
        _session.Player.PitchDegrees = Mathf.RadToDeg(_pitch);
        _session.Player.Sprinting = sprint && !crouch;
        _session.Player.Crouching = crouch;
        _session.Monsters.Tick(delta, _session.Player.Position, _session.LevelId, _session.Outposts);
        SyncMonsters();

        _streamTimer += delta;
        if (_streamTimer >= 0.45) { _streamTimer = 0; UpdateChunks(force: false); }
        UpdateHover();
        SyncRuntimeWorld();
    }

    public override void _Process(double delta)
    {
        if (_timestamp is not null)
        {
            _timestamp.Text = $"PM {DateTime.Now:hh:mm:ss}\n{DateTime.Now:MMM.dd yyyy}".ToUpperInvariant();
            _timestamp.Visible = _settings.VhsEffect;
        }
        if (_notification is not null && Time.GetTicksMsec() / 1000.0 > _notificationUntil)
            _notification.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && !_paused && !_inventoryUi.InventoryOpen && !_inventoryUi.WorkbenchOpen && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            var sens = _settings.MouseSensitivity * 0.005f;
            _yaw -= motion.Relative.X * sens;
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * sens, Mathf.DegToRad(-89), Mathf.DegToRad(89));
            _player.Rotation = new Vector3(0, _yaw, 0);
            _camera.Rotation = new Vector3(_pitch, 0, 0);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            var name = InputBindingCodec.FromKey(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
            if (string.Equals(name, _settings.PauseKey, StringComparison.OrdinalIgnoreCase))
            {
                if (_inventoryUi.InventoryOpen || _inventoryUi.WorkbenchOpen) { _inventoryUi.CloseAll(); Input.MouseMode = Input.MouseModeEnum.Captured; }
                else TogglePause();
                GetViewport().SetInputAsHandled(); return;
            }
            if (!_paused && string.Equals(name, _settings.InventoryKey, StringComparison.OrdinalIgnoreCase))
            {
                _inventoryUi.ToggleInventory(); Input.MouseMode = _inventoryUi.InventoryOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
                GetViewport().SetInputAsHandled(); return;
            }
            if (_inventoryUi.InventoryOpen && string.Equals(name, _settings.DismantleKey, StringComparison.OrdinalIgnoreCase))
            {
                _inventoryUi.DismantleHovered(); GetViewport().SetInputAsHandled(); return;
            }
            if (!_paused && key.Keycode is >= Key.Key1 and <= Key.Key4)
            {
                _session.Player.SelectedHotbarSlot = ((int)key.Keycode - (int)Key.Key1);
                _inventoryUi.Refresh();
            }
        }

        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && !_paused && !_inventoryUi.InventoryOpen && !_inventoryUi.WorkbenchOpen)
        {
            if (!TryInteract())
            {
                var placed = _session.PlaceFromHotbar(_session.Player.SelectedHotbarSlot);
                if (placed is not null) AddPlacedVisual(placed);
                _inventoryUi.Refresh();
            }
        }
    }

    public void ApplySettings(GameSettings settings)
    {
        _settings = settings;
        _session.ApplySettings(settings);
        _camera.Fov = settings.FieldOfView;
        if (_vhsMaterial is not null) _vhsMaterial.SetShaderParameter("intensity", settings.VhsEffect ? settings.VhsIntensity : 0f);
        if (_vhsLayer is not null) _vhsLayer.Visible = settings.VhsEffect;
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(Mathf.Max(0.0001f, settings.MasterVolume)));
        DisplayServer.WindowSetMode(settings.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }

    private void BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.018f, 0.018f, 0.012f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.72f, 0.68f, 0.42f),
            AmbientLightEnergy = 0.62f,
            FogEnabled = true,
            FogLightColor = new Color(0.32f, 0.30f, 0.18f),
            FogDensity = 0.006f
        };
        AddChild(new WorldEnvironment { Environment = env });
    }

    private void BuildPlayer()
    {
        _player = new CharacterBody3D { Name = "Player", Position = new Vector3(0, 2.2f, 0) };
        _player.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.34f, Height = 1.75f }, Position = new Vector3(0, 0.9f, 0) });
        _camera = new Camera3D { Name = "Camera", Position = new Vector3(0, 1.65f, 0), Fov = _settings.FieldOfView, Current = true };
        _player.AddChild(_camera);
        _ray = new RayCast3D { Enabled = true, TargetPosition = new Vector3(0, 0, -4.2f), CollideWithBodies = true, CollideWithAreas = true };
        _camera.AddChild(_ray);
        var fill = new OmniLight3D { OmniRange = 10f, LightEnergy = 0.22f, LightColor = new Color(1f, 0.92f, 0.66f), ShadowEnabled = false };
        _camera.AddChild(fill);
        AddChild(_player);
    }

    private void BuildHud()
    {
        _hud = new CanvasLayer { Layer = 40 }; AddChild(_hud);
        var cross = GodotTheme.Label("+", 24, HorizontalAlignment.Center); cross.AnchorLeft = cross.AnchorRight = 0.5f; cross.AnchorTop = cross.AnchorBottom = 0.5f; cross.OffsetLeft = -16; cross.OffsetRight = 16; cross.OffsetTop = -18; cross.OffsetBottom = 18; cross.MouseFilter = Control.MouseFilterEnum.Ignore; _hud.AddChild(cross);
        _notification = GodotTheme.Label("", 20, HorizontalAlignment.Center); _notification.AnchorLeft = 0.5f; _notification.AnchorRight = 0.5f; _notification.AnchorTop = 0.12f; _notification.AnchorBottom = 0.12f; _notification.OffsetLeft = -420; _notification.OffsetRight = 420; _notification.OffsetTop = -24; _notification.OffsetBottom = 24; _notification.Visible = false; _hud.AddChild(_notification);

        _vhsLayer = new CanvasLayer { Layer = 90 }; AddChild(_vhsLayer);
        var shader = GD.Load<Shader>("res://shaders/vhs.gdshader");
        _vhsMaterial = new ShaderMaterial { Shader = shader }; _vhsMaterial.SetShaderParameter("intensity", _settings.VhsEffect ? _settings.VhsIntensity : 0f);
        var overlay = new ColorRect { Color = Colors.White, Material = _vhsMaterial, MouseFilter = Control.MouseFilterEnum.Ignore }; GodotTheme.FullRect(overlay); _vhsLayer.AddChild(overlay);
        _timestamp = GodotTheme.Label("", 20, HorizontalAlignment.Right); _timestamp.AnchorLeft = 1; _timestamp.AnchorRight = 1; _timestamp.AnchorTop = 1; _timestamp.AnchorBottom = 1; _timestamp.OffsetLeft = -250; _timestamp.OffsetRight = -24; _timestamp.OffsetTop = -88; _timestamp.OffsetBottom = -22; _timestamp.MouseFilter = Control.MouseFilterEnum.Ignore; _vhsLayer.AddChild(_timestamp);
        _vhsLayer.Visible = _settings.VhsEffect;
    }

    private void BuildPauseMenu()
    {
        var layer = new CanvasLayer { Layer = 80, ProcessMode = ProcessModeEnum.Always }; AddChild(layer);
        _pauseRoot = new Control { Visible = false, ProcessMode = ProcessModeEnum.Always }; GodotTheme.FullRect(_pauseRoot); layer.AddChild(_pauseRoot);
        var shade = new ColorRect { Color = new Color(0,0,0,0.72f) }; GodotTheme.FullRect(shade); _pauseRoot.AddChild(shade);
        var center = new CenterContainer(); GodotTheme.FullRect(center); _pauseRoot.AddChild(center);
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle(new Color(0.025f,0.025f,0.02f,0.96f), 8)); center.AddChild(panel);
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) }; panel.AddChild(root);
        root.AddChild(GodotTheme.Label("ПАУЗА", 34, HorizontalAlignment.Center));
        var cont = GodotTheme.Button("ПРОДОЛЖИТЬ"); cont.Pressed += () => TogglePause(false); root.AddChild(cont);
        var settings = GodotTheme.Button("НАСТРОЙКИ"); root.AddChild(settings);
        var menu = GodotTheme.Button("ГЛАВНОЕ МЕНЮ"); menu.Pressed += () => ReturnToMenuRequested?.Invoke(); root.AddChild(menu);
        _settingsPanel = new SettingsPanel(_settings, ApplySettings) { Visible = false }; root.AddChild(_settingsPanel);
        settings.Pressed += () => _settingsPanel.Visible = !_settingsPanel.Visible;
    }

    private void TogglePause(bool? force = null)
    {
        _paused = force ?? !_paused;
        _session.Player.Paused = _paused;
        _pauseRoot.Visible = _paused;
        if (!_paused) _settingsPanel.Visible = false;
        Input.MouseMode = _paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    private void UpdateChunks(bool force)
    {
        if (_player is null) return;
        var p = ToVec3(_player.GlobalPosition);
        var ids = _package.Definition.Chunks.Select(c => c.Id).ToArray();
        var placements = _planner.Plan(_generator, ids, p, _package.Definition.ChunkSize);
        var wanted = new HashSet<string>(placements.Select(ChunkKey), StringComparer.Ordinal);
        foreach (var placement in placements)
        {
            var key = ChunkKey(placement); if (_chunks.ContainsKey(key)) continue;
            var template = _package.Definition.GetChunk(placement.TemplateId);
            var builder = new ChunkSceneBuilder(_diagnostics, _package, _extraction, _renderer);
            var node = builder.Build(template, placement, _runtimeWorld, _runtimeNodes);
            _chunkRoot.AddChild(node); _chunks[key] = node;
            foreach (var binding in template.Scripts) _lua.ExecuteBinding(_package, binding, template.Id);
            foreach (var entity in template.Entities) _events.Emit("load", entity.Id);
        }
        foreach (var stale in _chunks.Keys.Where(k => !wanted.Contains(k)).ToArray())
        {
            _chunks[stale].QueueFree(); _chunks.Remove(stale);
            var prefix = stale + "/";
            foreach (var id in _runtimeWorld.Entities.Where(e => e.ChunkInstanceId == stale).Select(e => e.Id).ToArray()) { _runtimeWorld.Remove(id); _runtimeNodes.Remove(id); }
        }
    }


    private void SyncRuntimeWorld()
    {
        foreach (var pair in _runtimeNodes.ToArray())
        {
            var runtime = _runtimeWorld.TryGet(pair.Key);
            if (runtime is null)
            {
                if (IsInstanceValid(pair.Value)) pair.Value.QueueFree();
                _runtimeNodes.Remove(pair.Key);
                continue;
            }
            if (!IsInstanceValid(pair.Value)) { _runtimeNodes.Remove(pair.Key); continue; }
            pair.Value.Visible = runtime.Visible;
            pair.Value.GlobalPosition = new Vector3(runtime.WorldPosition.X, runtime.WorldPosition.Y, runtime.WorldPosition.Z);
            pair.Value.RotationDegrees = new Vector3(runtime.Rotation.X, runtime.Rotation.Y, runtime.Rotation.Z);
        }
    }

    private void BuildMonsterVisuals()
    {
        foreach (var monster in _session.Monsters.Monsters)
        {
            var root = new Node3D { Name = monster.InstanceId };
            var mi = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.38f, Height = 1.9f } };
            mi.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.11f,0.10f,0.08f), Roughness = 0.95f };
            root.AddChild(mi); AddChild(root); _monsterVisuals[monster.InstanceId] = root;
        }
        SyncMonsters();
    }

    private void SyncMonsters()
    {
        foreach (var monster in _session.Monsters.Monsters)
            if (_monsterVisuals.TryGetValue(monster.InstanceId, out var node)) node.Position = new Vector3(monster.Position.X, monster.Position.Y + 0.95f, monster.Position.Z);
    }

    private void UpdateHover()
    {
        if (!_ray.IsColliding()) { _hoverEntity = null; return; }
        var node = _ray.GetCollider() as Node;
        var id = FindMeta(node, "entity_id");
        if (id is not null && id != _hoverEntity)
        {
            _hoverEntity = id; _events.Emit("hover", id);
        }
    }

    private bool TryInteract()
    {
        if (!_ray.IsColliding()) return false;
        var node = _ray.GetCollider() as Node;
        var entity = FindMeta(node, "entity_id");
        if (entity is not null) { _events.Emit("click", entity); return true; }
        var item = FindMeta(node, "item_id");
        if (string.Equals(item, "workbench", StringComparison.OrdinalIgnoreCase))
        {
            _inventoryUi.OpenWorkbench(); Input.MouseMode = Input.MouseModeEnum.Visible; return true;
        }
        return false;
    }

    private static string? FindMeta(Node? node, string key)
    {
        var current = node;
        while (current is not null)
        {
            if (current.HasMeta(key)) return current.GetMeta(key).AsString();
            current = current.GetParent();
        }
        return null;
    }

    private void AddPlacedVisual(PlacedWorldItem placed)
    {
        var root = new StaticBody3D { Name = $"Placed_{placed.ItemId}_{placed.Id:N}", Position = new Vector3(placed.Position.X, placed.Position.Y + 0.5f, placed.Position.Z), RotationDegrees = new Vector3(0, placed.YawDegrees, 0) };
        root.SetMeta("item_id", placed.ItemId);
        var mesh = new MeshInstance3D { Mesh = placed.ItemId == "outpost_flag_1" || placed.ItemId.StartsWith("outpost_flag_", StringComparison.Ordinal) ? new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 2.2f } : new BoxMesh { Size = new Vector3(1.0f, 1.0f, 1.0f) } };
        mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = placed.ItemId.StartsWith("outpost_flag_", StringComparison.Ordinal) ? new Color(0.78f,0.16f,0.12f) : new Color(0.38f,0.31f,0.20f), Roughness = 0.85f };
        root.AddChild(mesh); root.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1,1,1) } }); AddChild(root); _placedVisuals[placed.Id] = root;
    }

    private void Notify(string message)
    {
        if (_notification is null) return;
        _notification.Text = message; _notification.Visible = true; _notificationUntil = Time.GetTicksMsec() / 1000.0 + 4.5;
    }

    private static string ChunkKey(ChunkPlacement p) => $"{p.TemplateId}@{p.X},{p.Y},{p.Z}";
    private static Vec3 ToVec3(Vector3 v) => new(v.X, v.Y, v.Z);
}
