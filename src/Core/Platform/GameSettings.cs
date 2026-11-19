using System.Text.Json;
using Backrooms.Engine.Input;

namespace Backrooms.Engine.Platform;

public sealed record GameSettings(
    string Nickname = "wanderer",
    int Width = 1280,
    int Height = 720,
    bool Fullscreen = false,
    float MasterVolume = 0.75f,
    float AmbientVolume = 0.65f,
    bool VhsEffect = true,
    float VhsIntensity = 0.55f,
    bool HeadBob = true,
    float HeadBobStrength = 0.045f,
    float MouseSensitivity = 0.18f,
    float FieldOfView = 78f,
    string MoveForwardKey = InputBindingNames.W,
    string MoveBackwardKey = InputBindingNames.S,
    string MoveLeftKey = InputBindingNames.A,
    string MoveRightKey = InputBindingNames.D,
    string SprintKey = InputBindingNames.LeftCtrl,
    string CrouchKey = InputBindingNames.LeftShift,
    string InventoryKey = InputBindingNames.E,
    string DismantleKey = InputBindingNames.Q,
    string InteractKey = InputBindingNames.MouseLeft,
    string PauseKey = InputBindingNames.Escape,
    string DebuggerKey = InputBindingNames.F3,
    string JumpKey = InputBindingNames.Space)
{
    public static GameSettings Load()
    {
        AppPaths.EnsureCreated();
        if (!File.Exists(AppPaths.SettingsFile))
        {
            var defaults = new GameSettings();
            defaults.Save();
            return defaults;
        }

        try
        {
            var value = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(AppPaths.SettingsFile));
            return (value ?? new GameSettings()).Sanitize();
        }
        catch
        {
            return new GameSettings();
        }
    }

    public void Save()
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(Sanitize(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.SettingsFile, json);
    }

    public string BindingFor(GameInputAction action) => action switch
    {
        GameInputAction.MoveForward => MoveForwardKey,
        GameInputAction.MoveBackward => MoveBackwardKey,
        GameInputAction.MoveLeft => MoveLeftKey,
        GameInputAction.MoveRight => MoveRightKey,
        GameInputAction.Sprint => SprintKey,
        GameInputAction.Crouch => CrouchKey,
        GameInputAction.Inventory => InventoryKey,
        GameInputAction.Dismantle => DismantleKey,
        GameInputAction.Interact => InteractKey,
        GameInputAction.Pause => PauseKey,
        GameInputAction.Debugger => DebuggerKey,
        GameInputAction.Jump => JumpKey,
        _ => string.Empty
    };

    public GameSettings WithBinding(GameInputAction action, string key)
    {
        key = InputBindingNames.Normalize(key, BindingFor(action));
        return action switch
        {
            GameInputAction.MoveForward => this with { MoveForwardKey = key },
            GameInputAction.MoveBackward => this with { MoveBackwardKey = key },
            GameInputAction.MoveLeft => this with { MoveLeftKey = key },
            GameInputAction.MoveRight => this with { MoveRightKey = key },
            GameInputAction.Sprint => this with { SprintKey = key },
            GameInputAction.Crouch => this with { CrouchKey = key },
            GameInputAction.Inventory => this with { InventoryKey = key },
            GameInputAction.Dismantle => this with { DismantleKey = key },
            GameInputAction.Interact => this with { InteractKey = key },
            GameInputAction.Pause => this with { PauseKey = key },
            GameInputAction.Debugger => this with { DebuggerKey = key },
            GameInputAction.Jump => this with { JumpKey = key },
            _ => this
        };
    }

    private GameSettings Sanitize() => this with
    {
        Nickname = string.IsNullOrWhiteSpace(Nickname) ? "wanderer" : Nickname.Trim(),
        Width = Math.Clamp(Width, 640, 7680),
        Height = Math.Clamp(Height, 360, 4320),
        MasterVolume = Math.Clamp(MasterVolume, 0f, 1f),
        AmbientVolume = Math.Clamp(AmbientVolume, 0f, 1f),
        VhsIntensity = Math.Clamp(VhsIntensity, 0f, 1f),
        HeadBobStrength = Math.Clamp(HeadBobStrength, 0f, 0.2f),
        MouseSensitivity = Math.Clamp(MouseSensitivity, 0.02f, 2f),
        FieldOfView = Math.Clamp(FieldOfView, 55f, 120f),
        MoveForwardKey = InputBindingNames.Normalize(MoveForwardKey, InputBindingNames.W),
        MoveBackwardKey = InputBindingNames.Normalize(MoveBackwardKey, InputBindingNames.S),
        MoveLeftKey = InputBindingNames.Normalize(MoveLeftKey, InputBindingNames.A),
        MoveRightKey = InputBindingNames.Normalize(MoveRightKey, InputBindingNames.D),
        SprintKey = InputBindingNames.Normalize(SprintKey, InputBindingNames.LeftCtrl),
        CrouchKey = InputBindingNames.Normalize(CrouchKey, InputBindingNames.LeftShift),
        InventoryKey = InputBindingNames.Normalize(InventoryKey, InputBindingNames.E),
        DismantleKey = InputBindingNames.Normalize(DismantleKey, InputBindingNames.Q),
        InteractKey = InputBindingNames.Normalize(InteractKey, InputBindingNames.MouseLeft),
        PauseKey = InputBindingNames.Normalize(PauseKey, InputBindingNames.Escape),
        DebuggerKey = InputBindingNames.Normalize(DebuggerKey, InputBindingNames.F3),
        JumpKey = InputBindingNames.Normalize(JumpKey, InputBindingNames.Space)
    };
}
