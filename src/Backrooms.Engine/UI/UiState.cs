namespace Backrooms.Engine.UI;

public abstract record UiNode(string Id, bool Visible);
public sealed record UiText(string Id, string Text, bool Visible = true) : UiNode(Id, Visible);
public sealed record UiButton(string Id, string Label, bool Visible = true) : UiNode(Id, Visible);
public sealed record UiImage(string Id, string Source, bool Visible = true) : UiNode(Id, Visible);
public sealed record UiProgress(string Id, double Value, double Max, bool Visible = true) : UiNode(Id, Visible);

public sealed class UiState
{
    private readonly Dictionary<string, UiNode> _nodes = new(StringComparer.Ordinal);

    public IEnumerable<UiNode> Nodes => _nodes.Values;

    public void Set(UiNode node) => _nodes[node.Id] = node;
    public bool Remove(string id) => _nodes.Remove(id);
    public UiNode? Get(string id) => _nodes.TryGetValue(id, out var node) ? node : null;

    public void SetVisible(string id, bool visible)
    {
        if (!_nodes.TryGetValue(id, out var node))
            return;

        _nodes[id] = node switch
        {
            UiText x => x with { Visible = visible },
            UiButton x => x with { Visible = visible },
            UiImage x => x with { Visible = visible },
            UiProgress x => x with { Visible = visible },
            _ => node
        };
    }
}
