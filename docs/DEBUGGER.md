# Debugger

The debugger is based on a shared `DiagnosticBus`.

## Sources

Diagnostics may come from:

- XML parser/validator
- Lua init/event scripts
- renderer/shader compilation
- model import
- physics
- networking
- level streaming
- launcher/client platform code

## Severity

- Info
- Warning
- Error

An Error automatically opens `DebugOverlayState`.

F3 toggles debugger state manually in the SDL client.

## Entry shape

Each entry stores:

- timestamp
- severity
- source
- message
- optional detail/exception text

The bus retains the newest 2000 entries.

## ImGui

`ImGuiDebugPanel` renders the newest entries and shows detail text on hover.

The panel itself does not own SDL/WebGPU backend initialization. That stays in the platform renderer layer.
