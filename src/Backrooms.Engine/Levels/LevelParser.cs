using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Math;

namespace Backrooms.Engine.Levels;

public sealed class LevelParser
{
    private readonly DiagnosticBus _diagnostics;
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    private static readonly HashSet<string> SupportedModelExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".obj", ".fbx", ".gltf", ".glb" };

    public LevelParser(DiagnosticBus diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public LevelDefinition Parse(ZipArchive archive)
    {
        _ids.Clear();

        try
        {
            var xmlEntry = archive.GetEntry("level.xml")
                           ?? archive.Entries.FirstOrDefault(e =>
                               string.Equals(Path.GetFileName(e.FullName), "level.xml", StringComparison.OrdinalIgnoreCase))
                           ?? throw new InvalidDataException("Level ZIP must contain level.xml.");

            XDocument doc;
            using (var stream = xmlEntry.Open())
                doc = XDocument.Load(stream, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);

            var root = doc.Root ?? throw new InvalidDataException("level.xml has no root element.");
            if (!string.Equals(root.Name.LocalName, "level", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Root element must be <level>.");

            var name = Required(root, "name");
            var version = Optional(root, "version", "1");
            var init = ParseInitScript(root, archive);

            var chunksElement = root.Element("chunks")
                                ?? throw new InvalidDataException("level.xml must contain <chunks>.");

            var chunkSize = FloatAttr(chunksElement, "size", required: true);
            if (chunkSize <= 0)
                throw new InvalidDataException("<chunks size> must be greater than zero.");

            var chunks = chunksElement.Elements("chunk").Select(c => ParseChunk(c, archive)).ToArray();
            if (chunks.Length == 0)
                _diagnostics.Warn("LevelParser", "The level defines no chunks.");

            var metadata = root.Element("meta")?
                .Elements("entry")
                .Where(e => e.Attribute("key") is not null)
                .ToDictionary(e => Required(e, "key"), e => Optional(e, "value", e.Value), StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);

            _diagnostics.Info("LevelParser", $"Loaded '{name}', chunks={chunks.Length}, chunkSize={chunkSize}.");

            return new LevelDefinition(name, version, chunkSize, init, chunks, metadata);
        }
        catch (Exception ex)
        {
            _diagnostics.Error("LevelParser", ex.Message, ex.ToString());
            throw;
        }
    }

    private InitScriptDefinition? ParseInitScript(XElement root, ZipArchive archive)
    {
        var node = root.Elements("script")
            .FirstOrDefault(e => string.Equals(Optional(e, "class", ""), "init", StringComparison.OrdinalIgnoreCase));

        if (node is null)
            return null;

        var src = node.Attribute("src")?.Value?.Trim();
        var inline = string.IsNullOrWhiteSpace(node.Value) ? null : node.Value;

        if (!string.IsNullOrWhiteSpace(src))
            EnsureZipAsset(archive, src, "init script");

        if (src is null && inline is null)
            _diagnostics.Warn("LevelParser", "<script class=\"init\"> is empty.");

        return new InitScriptDefinition(src, inline);
    }

    private ChunkDefinition ParseChunk(XElement node, ZipArchive archive)
    {
        var id = Required(node, "id");
        var entities = new List<LevelEntity>();
        var textures = new List<TextureTargetBinding>();
        var shaders = new List<ShaderTargetBinding>();
        var scripts = new List<ScriptBinding>();

        foreach (var child in node.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "object":
                    entities.Add(ParseObject(child, archive));
                    break;
                case "model":
                    entities.Add(ParseModel(child, archive));
                    break;
                case "texture":
                    textures.Add(ParseTextureTarget(child, archive));
                    break;
                case "shader":
                    shaders.Add(ParseShaderTarget(child, archive));
                    break;
                case "script":
                    scripts.Add(ParseScript(child, archive, defaultEvent: "chunk_load"));
                    break;
                default:
                    _diagnostics.Warn("LevelParser", $"Unknown element <{child.Name.LocalName}> in chunk '{id}'.");
                    break;
            }
        }

        foreach (var texture in textures)
            EnsureEntityExists(texture.TargetId, $"texture target in chunk '{id}'");
        foreach (var shader in shaders)
            EnsureEntityExists(shader.TargetId, $"shader target in chunk '{id}'");

        return new ChunkDefinition(id, entities, textures, shaders, scripts);
    }

    private LevelEntity ParseObject(XElement node, ZipArchive archive)
    {
        var id = RegisterId(node);
        var type = Optional(node, "type", "cube").ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cube", "sphere", "plane", "capsule", "cylinder", "cone", "quad"
        };
        if (!allowed.Contains(type))
            throw new InvalidDataException($"Object '{id}' uses unsupported primitive type '{type}'.");

        return ParseEntityCommon(node, archive, id, EntityKind.Primitive, type, null);
    }

    private LevelEntity ParseModel(XElement node, ZipArchive archive)
    {
        var id = RegisterId(node);
        var src = Required(node, "src");
        var ext = Path.GetExtension(src);
        if (!SupportedModelExtensions.Contains(ext))
            throw new InvalidDataException($"Model '{id}' has unsupported extension '{ext}'. Expected OBJ, FBX, glTF or GLB.");

        EnsureZipAsset(archive, src, $"model '{id}'");
        return ParseEntityCommon(node, archive, id, EntityKind.Model, null, src);
    }

    private LevelEntity ParseEntityCommon(
        XElement node,
        ZipArchive archive,
        string id,
        EntityKind kind,
        string? primitiveType,
        string? modelSource)
    {
        var transformNode = node.Element("transform");
        var transform = new TransformSpec(
            ParseVec3(transformNode?.Attribute("position")?.Value ?? Optional(node, "position", "0 0 0"), Vec3.Zero),
            ParseVec3(transformNode?.Attribute("rotation")?.Value ?? Optional(node, "rotation", "0 0 0"), Vec3.Zero),
            ParseVec3(transformNode?.Attribute("scale")?.Value ?? Optional(node, "scale", "1 1 1"), Vec3.One));

        var textures = node.Elements("texture")
            .Select(t =>
            {
                var source = Required(t, "src");
                EnsureZipAsset(archive, source, $"texture on '{id}'");
                return new TextureBinding(Optional(t, "slot", "albedo"), source);
            })
            .ToArray();

        var shaders = node.Elements("shader")
            .Select(s =>
            {
                var source = Required(s, "src");
                EnsureZipAsset(archive, source, $"shader on '{id}'");
                return new ShaderBinding(
                    s.Attribute("id")?.Value,
                    source,
                    BoolAttr(s, "enabled", true),
                    ParseParams(s));
            })
            .ToArray();

        var scripts = node.Elements("script")
            .Select(s => ParseScript(s, archive, "load"))
            .ToArray();

        var properties = node.Element("properties")?.Elements("property")
            .ToDictionary(p => Required(p, "name"), p => Optional(p, "value", p.Value), StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        return new LevelEntity(id, kind, primitiveType, modelSource, transform, textures, shaders, scripts, properties);
    }

    private TextureTargetBinding ParseTextureTarget(XElement node, ZipArchive archive)
    {
        var target = Required(node, "target");
        var src = Required(node, "src");
        EnsureZipAsset(archive, src, $"texture targeting '{target}'");
        return new TextureTargetBinding(target, Optional(node, "slot", "albedo"), src);
    }

    private ShaderTargetBinding ParseShaderTarget(XElement node, ZipArchive archive)
    {
        var target = Required(node, "target");
        var src = Required(node, "src");
        EnsureZipAsset(archive, src, $"shader targeting '{target}'");
        return new ShaderTargetBinding(
            target,
            node.Attribute("id")?.Value,
            src,
            BoolAttr(node, "enabled", true),
            ParseParams(node));
    }

    private ScriptBinding ParseScript(XElement node, ZipArchive archive, string defaultEvent)
    {
        var eventName = Optional(node, "event", defaultEvent);
        var src = node.Attribute("src")?.Value?.Trim();
        var inline = string.IsNullOrWhiteSpace(node.Value) ? null : node.Value;

        if (!string.IsNullOrWhiteSpace(src))
            EnsureZipAsset(archive, src, $"script for event '{eventName}'");

        if (src is null && inline is null)
            _diagnostics.Warn("LevelParser", $"Empty script for event '{eventName}'.");

        return new ScriptBinding(eventName, src, inline);
    }

    private static IReadOnlyDictionary<string, string> ParseParams(XElement node) =>
        node.Elements("param")
            .Where(p => p.Attribute("name") is not null)
            .ToDictionary(p => Required(p, "name"), p => Optional(p, "value", p.Value), StringComparer.Ordinal);

    private string RegisterId(XElement node)
    {
        var id = Required(node, "id");
        if (!_ids.Add(id))
            throw new InvalidDataException(
                $"Duplicate entity id '{id}'. IDs are global across both <object> and <model>.");
        return id;
    }

    private void EnsureEntityExists(string id, string context)
    {
        if (!_ids.Contains(id))
            throw new InvalidDataException($"Unknown entity id '{id}' used by {context}.");
    }

    private static void EnsureZipAsset(ZipArchive archive, string path, string context)
    {
        var normalized = LevelPackage.Normalize(path);
        if (archive.GetEntry(normalized) is null)
            throw new FileNotFoundException($"Missing {context}: '{normalized}'.");
    }

    private static string Required(XElement node, string name)
    {
        var value = node.Attribute(name)?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"<{node.Name.LocalName}> requires attribute '{name}'.");
        return value;
    }

    private static string Optional(XElement node, string name, string fallback) =>
        node.Attribute(name)?.Value?.Trim() is { Length: > 0 } value ? value : fallback;

    private static float FloatAttr(XElement node, string name, bool required)
    {
        var raw = node.Attribute(name)?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (required)
                throw new InvalidDataException($"<{node.Name.LocalName}> requires numeric attribute '{name}'.");
            return 0;
        }

        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException($"Invalid float '{raw}' in attribute '{name}'.");

        return value;
    }

    private static bool BoolAttr(XElement node, string name, bool fallback)
    {
        var raw = node.Attribute(name)?.Value;
        return raw is null ? fallback : bool.TryParse(raw, out var value) ? value : fallback;
    }

    private static Vec3 ParseVec3(string raw, Vec3 fallback)
    {
        var parts = raw.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return fallback;

        if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            return new Vec3(x, y, z);

        return fallback;
    }
}
