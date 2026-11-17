using Backrooms.Engine.Generation;
using Backrooms.Engine.Levels;

namespace Backrooms.Engine.Runtime;

public sealed class ChunkRuntime
{
    private readonly RuntimeWorld _world;

    public ChunkRuntime(RuntimeWorld world)
    {
        _world = world;
    }

    public IReadOnlyList<RuntimeEntity> Instantiate(
        LevelDefinition level,
        ChunkDefinition template,
        ChunkPlacement placement)
    {
        var center = placement.WorldCenter(level.ChunkSize);
        var instancePrefix = $"{template.Id}@{placement.X},{placement.Y},{placement.Z}";
        var created = new List<RuntimeEntity>();

        foreach (var entity in template.Entities)
        {
            // XML positions are LOCAL to the chunk center by design.
            var runtime = new RuntimeEntity
            {
                Id = $"{instancePrefix}/{entity.Id}",
                Kind = entity.Kind,
                ChunkInstanceId = instancePrefix,
                LocalPosition = entity.Transform.Position,
                WorldPosition = center + entity.Transform.Position,
                Rotation = entity.Transform.Rotation,
                Scale = entity.Transform.Scale
            };

            foreach (var texture in entity.Textures)
                runtime.Textures[texture.Slot] = texture.Source;
            foreach (var shader in entity.Shaders.Where(s => s.Enabled))
                runtime.EnabledShaders.Add(shader.Id ?? shader.Source);

            _world.Add(runtime);
            created.Add(runtime);
        }

        return created;
    }
}
