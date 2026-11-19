# Gameplay

Defaults: WASD movement, Left Ctrl sprint, Left Shift crouch, Space jump, E inventory, Q dismantle hovered inventory item, 1–4 hotbar, LMB interact/place, ESC pause, F3 debugger. Bindings are stored in `settings.json` and can be changed from both menu Settings and ESC -> Settings.

The hotbar is always visible. The default leather backpack has 16 slots. One LMB click picks up a stack onto the cursor; clicking an occupied slot while carrying ejects the previous stack beside the player. Hovering an item shows decomposition materials; Q dismantles one item.

Player crafting is 2x2. Four wood items make a workbench. A placed workbench opens the 3x3 recipe UI on interaction. The existing crafting book includes tools, weapons, furniture, backpacks, generators, extractors and four outpost flags.

Only one outpost can exist per level. Higher flag tiers increase territory radius; extractors require tier II+. Hostile monster entry produces a warning notification. Outpost persistence remains in `outposts.json`.
