# F3 debugger

`DebuggerOverlay` is a Godot `CanvasLayer` shared by menu/loading/game.

It opens when the configured debugger key is pressed (F3 by default), and automatically opens when a `DiagnosticSeverity.Error` is emitted. Parser errors, Lua/GDScript exceptions, model/texture/shader failures and engine startup errors all flow through the same `DiagnosticBus` and log file.

The overlay supports ALL/INFO/WARN/ERR filters and shows FPS, level name, loaded chunk count, player position and monster count while in-game.

Logs are written immediately on application startup to `.backrooms_infinity/logs/godot-*.log`.
