using Backrooms.Engine.Mathematics;
using Backrooms.Engine.Platform;

namespace Backrooms.Engine.Gameplay;

public sealed class PlayerState
{
    public Vec3 Position { get; set; } = new(0, 1.7f, 0);
    public float YawDegrees { get; set; }
    public float PitchDegrees { get; set; }
    public bool Sprinting { get; set; }
    public bool Crouching { get; set; }
    public bool InventoryOpen { get; set; }
    public bool Paused { get; set; }
    public float HeadBobOffsetY { get; set; }
    public float HeadBobOffsetX { get; set; }
    public int SelectedHotbarSlot { get; set; }
}

public readonly record struct PlayerInput(
    bool Forward,
    bool Backward,
    bool Left,
    bool Right,
    bool Sprint,
    bool Crouch,
    bool Jump,
    float LookX,
    float LookY);

/// <summary>
/// Lightweight deterministic controller retained for gameplay simulation and tests.
/// Physics integration can later replace the position update without changing UI/debug APIs.
/// </summary>
public sealed class PlayerController
{
    private double _stepPhase;

    public PlayerState State { get; }

    public PlayerController(PlayerState state)
    {
        State = state;
    }

    public void Update(double deltaSeconds, PlayerInput input, GameSettings settings)
    {
        if (State.Paused || State.InventoryOpen || deltaSeconds <= 0)
            return;

        State.Sprinting = input.Sprint && !input.Crouch;
        State.Crouching = input.Crouch;
        State.YawDegrees += input.LookX * settings.MouseSensitivity;
        State.PitchDegrees = Math.Clamp(State.PitchDegrees - input.LookY * settings.MouseSensitivity, -89f, 89f);

        var forwardAmount = (input.Forward ? 1f : 0f) - (input.Backward ? 1f : 0f);
        var sideAmount = (input.Right ? 1f : 0f) - (input.Left ? 1f : 0f);
        var len = MathF.Sqrt(forwardAmount * forwardAmount + sideAmount * sideAmount);
        if (len > 1f)
        {
            forwardAmount /= len;
            sideAmount /= len;
        }

        var yaw = State.YawDegrees * MathF.PI / 180f;
        var forwardX = MathF.Sin(yaw);
        var forwardZ = -MathF.Cos(yaw);
        var rightX = MathF.Cos(yaw);
        var rightZ = MathF.Sin(yaw);

        var speed = State.Crouching ? 2.0f : State.Sprinting ? 7.2f : 4.2f;
        var dt = (float)Math.Min(deltaSeconds, 0.1);
        var dx = (forwardX * forwardAmount + rightX * sideAmount) * speed * dt;
        var dz = (forwardZ * forwardAmount + rightZ * sideAmount) * speed * dt;
        State.Position = State.Position + new Vec3(dx, 0, dz);

        var moved = MathF.Abs(dx) + MathF.Abs(dz) > 0.0001f;
        if (settings.HeadBob && moved)
        {
            var cadence = State.Sprinting ? 13.5 : State.Crouching ? 6.0 : 9.0;
            _stepPhase += deltaSeconds * cadence;
            var strength = settings.HeadBobStrength * (State.Sprinting ? 1.35f : State.Crouching ? 0.55f : 1f);
            State.HeadBobOffsetY = (float)Math.Sin(_stepPhase * 2.0) * strength;
            State.HeadBobOffsetX = (float)Math.Sin(_stepPhase) * strength * 0.55f;
        }
        else
        {
            State.HeadBobOffsetY *= 0.82f;
            State.HeadBobOffsetX *= 0.82f;
        }
    }
}
