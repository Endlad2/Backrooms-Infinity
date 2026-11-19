using Godot;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Generation;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Mathematics;
using Backrooms.Engine.Runtime;

namespace Backrooms.Infinity.App;

public sealed class ChunkSceneBuilder
{
    private readonly DiagnosticBus _diagnostics;
    private readonly LevelPackage _package;
    private readonly LevelExtraction _extracted;
    private readonly RuntimeModelLoader _models;
    private readonly GodotRendererBridge _renderer;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);

    public ChunkSceneBuilder(DiagnosticBus diagnostics, LevelPackage package, LevelExtraction extracted, GodotRendererBridge renderer)
    {
        _diagnostics = diagnostics;
        _package = package;
        _extracted = extracted;
        _renderer = renderer;
        _models = new RuntimeModelLoader(diagnostics);
    }

    public Node3D Build(ChunkDefinition template, ChunkPlacement placement, RuntimeWorld runtimeWorld, IDictionary<string, Node3D>? runtimeNodes = null)
    {
        var chunkRoot = new Node3D { Name = $"Chunk_{template.Id}_{placement.X}_{placement.Y}_{placement.Z}" };
        var center = placement.WorldCenter(_package.Definition.ChunkSize);
        chunkRoot.Position = ToGodot(center);

        var textureTargets = template.TextureTargets.GroupBy(x => x.TargetId).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var shaderTargets = template.ShaderTargets.GroupBy(x => x.TargetId).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var instancePrefix = $"{template.Id}@{placement.X},{placement.Y},{placement.Z}";

        foreach (var entity in template.Entities)
        {
            var entityNode = entity.Kind == EntityKind.Model ? BuildModel(entity) : BuildPrimitive(entity);
            entityNode.Name = entity.Id;
            entityNode.Position = ToGodot(entity.Transform.Position);
            entityNode.RotationDegrees = ToGodot(entity.Transform.Rotation);
            if (entity.Kind == EntityKind.Model)
                entityNode.Scale = ToGodot(entity.Transform.Scale);
            entityNode.SetMeta("entity_id", entity.Id);
            entityNode.SetMeta("runtime_id", $"{instancePrefix}/{entity.Id}");

            var material = BuildPbrMaterial(entity, textureTargets.TryGetValue(entity.Id, out var extraTextures) ? extraTextures : null);
            ApplyMaterial(entityNode, material);
            ApplyCustomShader(entityNode, entity, shaderTargets.TryGetValue(entity.Id, out var extraShaders) ? extraShaders : null);
            chunkRoot.AddChild(entityNode);

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
            foreach (var tex in entity.Textures) runtime.Textures[tex.Slot] = tex.Source;
            foreach (var shader in entity.Shaders.Where(s => s.Enabled)) runtime.EnabledShaders.Add(shader.Id ?? shader.Source);
            runtimeWorld.Add(runtime);
            if (runtimeNodes is not null) runtimeNodes[runtime.Id] = entityNode;
        }

        return chunkRoot;
    }

    private Node3D BuildPrimitive(LevelEntity entity)
    {
        var root = new Node3D();
        Mesh mesh;
        Shape3D? shape;
        var s = entity.Transform.Scale;
        var abs = new Vector3(Mathf.Abs(s.X), Mathf.Abs(s.Y), Mathf.Abs(s.Z));
        switch (entity.PrimitiveType?.ToLowerInvariant())
        {
            case "sphere":
                mesh = new SphereMesh { Radius = MathF.Max(0.05f, abs.X * 0.5f), Height = MathF.Max(0.1f, abs.Y) };
                shape = new SphereShape3D { Radius = MathF.Max(0.05f, abs.X * 0.5f) };
                break;
            case "capsule":
                mesh = new CapsuleMesh { Radius = MathF.Max(0.05f, abs.X * 0.5f), Height = MathF.Max(abs.Y, abs.X) };
                shape = new CapsuleShape3D { Radius = MathF.Max(0.05f, abs.X * 0.5f), Height = MathF.Max(abs.Y, abs.X) };
                break;
            case "cylinder":
                mesh = new CylinderMesh { TopRadius = MathF.Max(0.05f, abs.X * 0.5f), BottomRadius = MathF.Max(0.05f, abs.X * 0.5f), Height = MathF.Max(0.05f, abs.Y) };
                shape = new CylinderShape3D { Radius = MathF.Max(0.05f, abs.X * 0.5f), Height = MathF.Max(0.05f, abs.Y) };
                break;
            case "cone":
                mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = MathF.Max(0.05f, abs.X * 0.5f), Height = MathF.Max(0.05f, abs.Y) };
                shape = new CylinderShape3D { Radius = MathF.Max(0.05f, abs.X * 0.5f), Height = MathF.Max(0.05f, abs.Y) };
                break;
            case "plane":
            case "quad":
                mesh = new PlaneMesh { Size = new Vector2(MathF.Max(0.05f, abs.X), MathF.Max(0.05f, abs.Z)) };
                shape = new BoxShape3D { Size = new Vector3(MathF.Max(0.05f, abs.X), 0.08f, MathF.Max(0.05f, abs.Z)) };
                break;
            default:
                mesh = new BoxMesh { Size = new Vector3(MathF.Max(0.05f, abs.X), MathF.Max(0.05f, abs.Y), MathF.Max(0.05f, abs.Z)) };
                shape = new BoxShape3D { Size = new Vector3(MathF.Max(0.05f, abs.X), MathF.Max(0.05f, abs.Y), MathF.Max(0.05f, abs.Z)) };
                break;
        }

        var mi = new MeshInstance3D { Mesh = mesh };
        root.AddChild(mi);
        if (shape is not null)
        {
            var body = new StaticBody3D();
            body.SetMeta("entity_id", entity.Id);
            body.AddChild(new CollisionShape3D { Shape = shape });
            root.AddChild(body);
        }
        return root;
    }

    private Node3D BuildModel(LevelEntity entity)
    {
        if (string.IsNullOrWhiteSpace(entity.ModelSource)) return new Node3D();
        var path = _extracted.PathFor(entity.ModelSource!);
        var root = _models.Load(path);
        TryCreateModelCollisions(root, entity.Id);
        return root;
    }

    private void TryCreateModelCollisions(Node node, string entityId)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D mi && mi.Mesh is not null)
            {
                try
                {
                    var shape = mi.Mesh.CreateTrimeshShape();
                    if (shape is not null)
                    {
                        var body = new StaticBody3D();
                        body.SetMeta("entity_id", entityId);
                        body.AddChild(new CollisionShape3D { Shape = shape });
                        mi.AddChild(body);
                    }
                }
                catch (Exception ex) { _diagnostics.Warn("Physics", $"Could not build trimesh collision for {entityId}.", ex.Message); }
            }
            TryCreateModelCollisions(child, entityId);
        }
    }

    private StandardMaterial3D BuildPbrMaterial(LevelEntity entity, List<TextureTargetBinding>? extra)
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.72f, 0.69f, 0.52f),
            Roughness = 0.82f,
            Metallic = 0.0f
        };
        var bindings = entity.Textures.Select(t => (t.Slot, t.Source)).ToList();
        if (extra is not null) bindings.AddRange(extra.Select(t => (t.Slot, t.Source)));
        foreach (var (slotRaw, src) in bindings)
        {
            var slot = slotRaw.ToLowerInvariant();
            var tex = LoadTexture(src);
            if (tex is null) continue;
            switch (slot)
            {
                case "albedo": case "basecolor": case "base_color": mat.AlbedoTexture = tex; break;
                case "normal": mat.NormalEnabled = true; mat.NormalTexture = tex; break;
                case "roughness": mat.RoughnessTexture = tex; break;
                case "metallic": mat.MetallicTexture = tex; break;
                case "ao": case "occlusion": mat.AOEnabled = true; mat.AOTexture = tex; break;
                case "emission": mat.EmissionEnabled = true; mat.EmissionTexture = tex; mat.EmissionEnergyMultiplier = 1.8f; break;
            }
            if (src.Contains("fluorescent", StringComparison.OrdinalIgnoreCase))
            {
                mat.EmissionEnabled = true;
                mat.Emission = new Color(0.96f, 0.94f, 0.73f);
                mat.EmissionEnergyMultiplier = 2.2f;
            }
        }
        return mat;
    }

    private Texture2D? LoadTexture(string source)
    {
        if (_textures.TryGetValue(source, out var cached)) return cached;
        try
        {
            using var stream = _package.OpenAsset(source);
            using var ms = new MemoryStream(); stream.CopyTo(ms);
            var image = new Image();
            var ext = Path.GetExtension(source).ToLowerInvariant();
            var err = ext switch
            {
                ".jpg" or ".jpeg" => image.LoadJpgFromBuffer(ms.ToArray()),
                ".webp" => image.LoadWebpFromBuffer(ms.ToArray()),
                _ => image.LoadPngFromBuffer(ms.ToArray())
            };
            if (err != Error.Ok) throw new InvalidDataException($"Godot image loader returned {err}");
            var tex = ImageTexture.CreateFromImage(image);
            _textures[source] = tex;
            return tex;
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Texture", $"Failed to load '{source}'.", ex.Message);
            return null;
        }
    }

    private void ApplyMaterial(Node node, Material material)
    {
        if (node is MeshInstance3D mi) mi.MaterialOverride = material;
        foreach (var child in node.GetChildren()) ApplyMaterial(child, material);
    }

    private void ApplyCustomShader(Node3D node, LevelEntity entity, List<ShaderTargetBinding>? extra)
    {
        var shaders = entity.Shaders.Where(x => x.Enabled).Select(x => (x.Id ?? x.Source, x.Source, x.Parameters)).ToList();
        if (extra is not null) shaders.AddRange(extra.Where(x => x.Enabled).Select(x => (x.ShaderId ?? x.Source, x.Source, x.Parameters)));
        foreach (var (id, source, parameters) in shaders)
        {
            if (!source.EndsWith(".gdshader", StringComparison.OrdinalIgnoreCase))
            {
                _diagnostics.Warn("Shader", $"'{source}' is not a Godot .gdshader and was ignored after the engine migration.");
                continue;
            }
            try
            {
                var shader = new Shader { Code = _package.ReadText(source) };
                var material = new ShaderMaterial { Shader = shader };
                foreach (var p in parameters) material.SetShaderParameter(p.Key, p.Value);
                ApplyMaterial(node, material);
                _renderer.Register(id, material);
            }
            catch (Exception ex) { _diagnostics.Error("Shader", $"Failed to load '{source}'.", ex.ToString()); }
        }
    }

    private static Vector3 ToGodot(Vec3 v) => new(v.X, v.Y, v.Z);
}
