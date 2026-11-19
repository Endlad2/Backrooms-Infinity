# Backrooms Infinity — Godot .NET

Единый проект игры на **Godot 4.7.2 .NET / C#**. Отдельных Launcher/Client бинарников больше нет: главное меню, каталог уровней, загрузка ZIP, игра, настройки и debugger работают в одном процессе и одном `Backrooms.Infinity.csproj`.

## Что внутри

- единый `Main.tscn` и `MainController.cs`;
- меню с VHS-шрифтом, панорамированием/зумом фоновых изображений и соответствующим ambient;
- «Одиночная игра» загружает `levels.db.zip` из `raw.githubusercontent.com/Endlad2/Backrooms-Infinity/main/levels.db.zip`, показывает карточки/preview и зелёный `ГОТОВ` для скачанных ZIP;
- ZIP/XML scripting API with explicit `type="lua"` / `type="gdscript"`, `chunks size`, позиции относительно центра чанка, общий namespace ID для object/model;
- генераторы `grid`, `finite`, `axis`, `custom` через init Lua; GDScript runtime доступен для специальных случаев;
- runtime **glTF/GLB, FBX и OBJ**: glTF/FBX через Godot runtime document API, OBJ через встроенный C# runtime loader;
- PBR на `StandardMaterial3D` (albedo/normal/roughness/metallic/AO/emission);
- `.gdshader` shaders уровня + Lua/GDScript shader bridge;
- F3 debugger с Info/Warning/Error, автопоказом при engine/parser/Lua/GDScript error и runtime-статистикой;
- VHS post-process + timestamp, head bob;
- Ctrl sprint, Shift crouch, E inventory, Q dismantle, 1–4 hotbar, ESC pause;
- настройки доступны и из главного меню, и из ESC → Настройки; клавиши переназначаются;
- 4-slot hotbar, 16-slot leather backpack, 2x2 crafting, 3x3 workbench, decomposition, placeables, outposts and monsters;
- 50 level ZIP (`levels/Level-0.zip` … `Level-49.zip`) и `levels.db.zip` из предыдущей ветки сохранены.

## Важно про headless

У игры **нет собственного `--headless` режима и консольного headless-клиента**. Это сознательно удалено вместе с разделением Launcher/Client. GitHub Actions может использовать служебный `godot --headless --editor` только для CI/import проверки движком — это не игровой режим и не пользовательский CLI.

## Структура

```text
Backrooms-Infinity/
├─ project.godot
├─ Main.tscn
├─ Backrooms.Infinity.csproj
├─ src/
│  ├─ App/                 Godot UI / 3D / loader / debugger
│  └─ Core/                XML, Lua, generation, gameplay
├─ shaders/vhs.gdshader
├─ assets/                 VHS font + menu media
├─ levels/                 Level-0..49.zip + previews
├─ levels.db.zip
├─ docs/
├─ installer.ps1 / .sh
└─ .github/workflows/
```

## Запуск

Нужны Godot **4.7.2 .NET** и .NET 8 SDK.

```bash
dotnet restore Backrooms.Infinity.csproj
dotnet build Backrooms.Infinity.csproj -c Debug
```

Затем открыть `project.godot` в Godot .NET и запустить `Main.tscn`.

## Пользовательские данные

Windows: `%APPDATA%/.backrooms_infinity/`

Linux/macOS: `~/.backrooms_infinity/`

Там находятся `levels/`, `settings/`, `assets/`, `logs/`, `cache/`, `temp/` и сохранение аванпостов.

## Level shaders после миграции

Старые WGSL-файлы были частью WebGPU-ветки. В Godot-версии кастомный runtime shader уровня должен иметь расширение `.gdshader`. XML/Lua shader API сохранён, но backend теперь Godot Shader/ShaderMaterial.

## Лицензия

Код репозитория — PolyForm Noncommercial 1.0.0. У сторонних ассетов могут быть собственные условия. Attribution VHS font сохранён в `assets/common/FONT-COPYRIGHT.txt`.
