using System.Numerics;
using ImGuiNET;

namespace Backrooms.Engine.Diagnostics;

public sealed class ImGuiDebugPanel
{
    private readonly DiagnosticBus _diagnostics;
    private readonly DebugOverlayState _state;

    public ImGuiDebugPanel(DiagnosticBus diagnostics, DebugOverlayState state)
    {
        _diagnostics = diagnostics;
        _state = state;
    }

    // Call from the platform-specific ImGui frame after an ImGui context/backend is initialized.
    public void Draw()
    {
        if (!_state.IsOpen)
            return;

        var open = true;
        if (!ImGui.Begin("Backrooms Debugger (F3)", ref open))
        {
            ImGui.End();
            if (!open) _state.Close();
            return;
        }

        foreach (var entry in _diagnostics.Snapshot().TakeLast(500))
        {
            var color = entry.Severity switch
            {
                DiagnosticSeverity.Error => new Vector4(1f, 0.35f, 0.35f, 1f),
                DiagnosticSeverity.Warning => new Vector4(1f, 0.8f, 0.25f, 1f),
                _ => new Vector4(0.75f, 0.85f, 1f, 1f)
            };

            ImGui.TextColored(color,
                $"[{entry.Timestamp:HH:mm:ss}] {entry.Severity,-7} {entry.Source}: {entry.Message}");

            if (!string.IsNullOrWhiteSpace(entry.Detail) && ImGui.IsItemHovered())
                ImGui.SetTooltip(entry.Detail);
        }

        ImGui.End();
        if (!open) _state.Close();
    }
}
