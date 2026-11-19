using Backrooms.Engine.Mathematics;

namespace Backrooms.Engine.Gameplay;

public sealed record MonsterDefinition(
    string Id,
    string DisplayName,
    bool Hostile = true,
    float WalkSpeed = 2.5f,
    float ChaseSpeed = 5.0f,
    float DetectionRadius = 18f);

public sealed class MonsterState
{
    public required string InstanceId { get; init; }
    public required MonsterDefinition Definition { get; init; }
    public Vec3 Position { get; set; }
    public bool Chasing { get; set; }
}

public sealed class MonsterRuntime
{
    private readonly List<MonsterState> _monsters = new();
    public IReadOnlyList<MonsterState> Monsters => _monsters;
    public event Action<MonsterState>? OutpostIntrusion;

    public void Spawn(MonsterDefinition definition, string instanceId, Vec3 position) =>
        _monsters.Add(new MonsterState { Definition = definition, InstanceId = instanceId, Position = position });

    public void Tick(double deltaSeconds, Vec3 playerPosition, string levelId, OutpostService outposts)
    {
        var dt = (float)Math.Min(deltaSeconds, 0.1);
        foreach (var monster in _monsters)
        {
            var before = monster.Position;
            var dx = playerPosition.X - monster.Position.X;
            var dz = playerPosition.Z - monster.Position.Z;
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            monster.Chasing = monster.Definition.Hostile && distance <= monster.Definition.DetectionRadius;
            if (monster.Chasing && distance > 0.01f)
            {
                var speed = monster.Definition.ChaseSpeed;
                monster.Position = monster.Position + new Vec3(dx / distance * speed * dt, 0, dz / distance * speed * dt);
            }

            if (outposts.MonsterEntered(levelId, before, monster.Position))
                OutpostIntrusion?.Invoke(monster);
        }
    }
}
