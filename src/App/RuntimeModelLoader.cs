using Godot;
using System.Globalization;
using Backrooms.Engine.Diagnostics;

namespace Backrooms.Infinity.App;

public sealed class RuntimeModelLoader
{
    private readonly DiagnosticBus _diagnostics;
    public RuntimeModelLoader(DiagnosticBus diagnostics) => _diagnostics = diagnostics;

    public Node3D Load(string path)
    {
        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".glb" or ".gltf" => LoadGltf(path),
                ".fbx" => LoadFbx(path),
                ".obj" => LoadObj(path),
                _ => Placeholder(Path.GetFileName(path), "unsupported model format")
            };
        }
        catch (Exception ex)
        {
            _diagnostics.Error("ModelLoader", $"Failed to load '{path}'.", ex.ToString());
            return Placeholder(Path.GetFileName(path), ex.Message);
        }
    }

    private Node3D LoadGltf(string path)
    {
        var doc = new GltfDocument();
        var state = new GltfState();
        var error = doc.AppendFromFile(path, state);
        if (error != Error.Ok) throw new InvalidDataException($"Godot glTF loader returned {error}.");
        var generated = doc.GenerateScene(state);
        if (generated is Node3D node) return node;
        var root = new Node3D(); root.AddChild(generated); return root;
    }

    private Node3D LoadFbx(string path)
    {
        var doc = new FbxDocument();
        var state = new FbxState();
        var error = doc.AppendFromFile(path, state);
        if (error != Error.Ok) throw new InvalidDataException($"Godot FBX loader returned {error}.");
        var generated = doc.GenerateScene(state);
        if (generated is Node3D node) return node;
        var root = new Node3D(); root.AddChild(generated); return root;
    }

    private Node3D LoadObj(string path)
    {
        var positions = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var faces = new List<string[]>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                var p = SplitFloats(line[2..], 3); positions.Add(new Vector3(p[0], p[1], p[2]));
            }
            else if (line.StartsWith("vt ", StringComparison.Ordinal))
            {
                var p = SplitFloats(line[3..], 2); uvs.Add(new Vector2(p[0], 1f - p[1]));
            }
            else if (line.StartsWith("vn ", StringComparison.Ordinal))
            {
                var p = SplitFloats(line[3..], 3); normals.Add(new Vector3(p[0], p[1], p[2]));
            }
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                var parts = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3) faces.Add(parts);
            }
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var face in faces)
        {
            for (var tri = 1; tri < face.Length - 1; tri++)
            {
                AddObjVertex(st, face[0], positions, uvs, normals);
                AddObjVertex(st, face[tri], positions, uvs, normals);
                AddObjVertex(st, face[tri + 1], positions, uvs, normals);
            }
        }
        if (normals.Count == 0) st.GenerateNormals();
        var mesh = st.Commit();
        var mi = new MeshInstance3D { Mesh = mesh, Name = Path.GetFileNameWithoutExtension(path) };
        var root = new Node3D(); root.AddChild(mi); return root;
    }

    private static void AddObjVertex(SurfaceTool st, string token, List<Vector3> p, List<Vector2> uv, List<Vector3> n)
    {
        var bits = token.Split('/');
        var pi = ObjIndex(bits.ElementAtOrDefault(0), p.Count);
        var ti = ObjIndex(bits.ElementAtOrDefault(1), uv.Count);
        var ni = ObjIndex(bits.ElementAtOrDefault(2), n.Count);
        if (ti >= 0 && ti < uv.Count) st.SetUV(uv[ti]);
        if (ni >= 0 && ni < n.Count) st.SetNormal(n[ni]);
        if (pi >= 0 && pi < p.Count) st.AddVertex(p[pi]);
    }

    private static int ObjIndex(string? raw, int count)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) || i == 0) return -1;
        return i > 0 ? i - 1 : count + i;
    }

    private static float[] SplitFloats(string s, int count)
    {
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new float[count];
        for (var i = 0; i < count; i++)
            result[i] = i < parts.Length && float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
        return result;
    }

    private static Node3D Placeholder(string name, string reason)
    {
        var root = new Node3D { Name = $"MissingModel_{name}" };
        var mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One } };
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.1f, 0.55f), EmissionEnabled = true, Emission = new Color(0.25f, 0, 0.25f) };
        mesh.MaterialOverride = material;
        root.AddChild(mesh);
        return root;
    }
}
