# Architecture — single Godot .NET process

```text
MainController (one executable)
  ├─ Main menu / animated background / ambient
  ├─ LevelCatalogService -> levels.db.zip (SQLite) with JSON runtime fallback -> downloaded level ZIP
  ├─ Loading screen
  ├─ LevelParser + LevelPackage
  ├─ LevelScriptRuntime -> LuaHost (primary) / GdScriptHost (runtime special cases)
  └─ GameWorld
      ├─ CharacterBody3D + Camera3D
      ├─ ChunkPlanner + ChunkSceneBuilder
      ├─ StandardMaterial3D PBR
      ├─ GltfDocument / FbxDocument / OBJ runtime loader
      ├─ InventoryUi / workbench / pause / settings
      ├─ VHS CanvasItem post-process
      └─ F3 DebuggerOverlay (global)
```

There is no launcher/client process boundary and no game-specific headless mode.

Core XML/Lua/gameplay code remains engine-independent under `src/Core`. Runtime GDScript execution and other Godot-specific bridges stay in `src/App`.
