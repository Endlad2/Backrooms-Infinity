using Backrooms.Engine.Math;

namespace Backrooms.Engine.Generation;

public enum GeneratorMode
{
    Grid,
    Finite,
    Axis,
    Custom
}

public sealed record ChunkPlacement(string TemplateId, int X, int Y, int Z)
{
    public Vec3 WorldCenter(float chunkSize) => new(X * chunkSize, Y * chunkSize, Z * chunkSize);
}

public sealed class GeneratorPlan
{
    public GeneratorMode Mode { get; set; } = GeneratorMode.Grid;
    public int Radius { get; set; } = 2;
    public char Axis { get; set; } = 'y';
    public int AxisRadius { get; set; } = 8;
    public IReadOnlyList<ChunkPlacement> FixedPlacements { get; set; } = Array.Empty<ChunkPlacement>();
    public Func<Vec3, IReadOnlyList<ChunkPlacement>>? CustomPlanner { get; set; }
}

public sealed class ChunkPlanner
{
    public IReadOnlyList<ChunkPlacement> Plan(
        GeneratorPlan plan,
        IReadOnlyList<string> templateIds,
        Vec3 playerWorldPosition,
        float chunkSize)
    {
        if (templateIds.Count == 0)
            return Array.Empty<ChunkPlacement>();

        return plan.Mode switch
        {
            GeneratorMode.Finite => plan.FixedPlacements,
            GeneratorMode.Axis => PlanAxis(plan, templateIds, playerWorldPosition, chunkSize),
            GeneratorMode.Custom when plan.CustomPlanner is not null => plan.CustomPlanner(playerWorldPosition),
            _ => PlanGrid(plan, templateIds, playerWorldPosition, chunkSize)
        };
    }

    private static IReadOnlyList<ChunkPlacement> PlanGrid(
        GeneratorPlan plan,
        IReadOnlyList<string> templates,
        Vec3 player,
        float chunkSize)
    {
        var cx = (int)MathF.Floor((player.X + chunkSize * 0.5f) / chunkSize);
        var cz = (int)MathF.Floor((player.Z + chunkSize * 0.5f) / chunkSize);
        var result = new List<ChunkPlacement>();

        for (var z = cz - plan.Radius; z <= cz + plan.Radius; z++)
        for (var x = cx - plan.Radius; x <= cx + plan.Radius; x++)
        {
            var index = StableIndex(x, 0, z, templates.Count);
            result.Add(new ChunkPlacement(templates[index], x, 0, z));
        }

        return result;
    }

    private static IReadOnlyList<ChunkPlacement> PlanAxis(
        GeneratorPlan plan,
        IReadOnlyList<string> templates,
        Vec3 player,
        float chunkSize)
    {
        var coordinate = plan.Axis switch
        {
            'x' => player.X,
            'z' => player.Z,
            _ => player.Y
        };
        var center = (int)MathF.Floor((coordinate + chunkSize * 0.5f) / chunkSize);

        var result = new List<ChunkPlacement>();
        for (var i = center - plan.AxisRadius; i <= center + plan.AxisRadius; i++)
        {
            var index = StableIndex(
                plan.Axis == 'x' ? i : 0,
                plan.Axis == 'y' ? i : 0,
                plan.Axis == 'z' ? i : 0,
                templates.Count);

            result.Add(plan.Axis switch
            {
                'x' => new ChunkPlacement(templates[index], i, 0, 0),
                'z' => new ChunkPlacement(templates[index], 0, 0, i),
                _ => new ChunkPlacement(templates[index], 0, i, 0)
            });
        }

        return result;
    }

    private static int StableIndex(int x, int y, int z, int count)
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + x;
            hash = hash * 31 + y;
            hash = hash * 31 + z;
            return (hash & int.MaxValue) % count;
        }
    }
}
