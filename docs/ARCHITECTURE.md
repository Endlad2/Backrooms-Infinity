# Architecture — single Godot .NET process

```text
MainController (one executable)
  ├─ Main menu / animated background / ambient
  ├─ LevelCatalogService -> levels.db.zip -> downloaded level ZIP
  ├─ Loading screen
  ├─ LevelParser + LevelPackage
  ├─ LuaHost -> GeneratorPlan / world / UI / shader / events
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

Core XML/Lua/gameplay code remains engine-independent under `src/Core`. Godot-specific nodes stay in `src/App`.
