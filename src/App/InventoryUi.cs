using Godot;
using Backrooms.Engine.Gameplay;

namespace Backrooms.Infinity.App;

public sealed partial class InventoryUi : CanvasLayer
{
    private readonly GameSession _session;
    private PanelContainer _inventoryPanel = null!;
    private PanelContainer _workbenchPanel = null!;
    private HBoxContainer _hotbar = null!;
    private GridContainer _backpack = null!;
    private GridContainer _craft2 = null!;
    private GridContainer _craft3 = null!;
    private VBoxContainer _recipes = null!;
    private Label _hover = null!;
    private Label _cursor = null!;
    private ItemStack?[] _workbench = new ItemStack?[9];
    public InventorySlotRef? HoveredSlot { get; private set; }
    public bool InventoryOpen => _inventoryPanel.Visible;
    public bool WorkbenchOpen => _workbenchPanel.Visible;

    public InventoryUi(GameSession session)
    {
        _session = session;
        Layer = 50;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        BuildHotbar();
        BuildInventory();
        BuildWorkbench();
        _cursor = GodotTheme.Label("", 17);
        _cursor.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_cursor);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (_cursor is null) return;
        _cursor.Position = GetViewport().GetMousePosition() + new Vector2(18, 18);
        var cursor = _session.Inventory.Cursor;
        _cursor.Text = cursor is null ? "" : $"{_session.Items.Get(cursor.ItemId).Name} x{cursor.Quantity}";
        _cursor.Visible = cursor is not null;
    }

    public void ToggleInventory()
    {
        _workbenchPanel.Visible = false;
        _inventoryPanel.Visible = !_inventoryPanel.Visible;
        Refresh();
    }

    public void CloseAll()
    {
        _inventoryPanel.Visible = false;
        _workbenchPanel.Visible = false;
        _session.Inventory.CancelCursor(_session.DropNearPlayer);
        Refresh();
    }

    public void OpenWorkbench()
    {
        _inventoryPanel.Visible = false;
        _workbenchPanel.Visible = true;
        Refresh();
    }

    public void DismantleHovered()
    {
        if (HoveredSlot is not { } slot) return;
        _session.Dismantle(slot);
        Refresh();
    }

    public void Refresh()
    {
        RefreshGrid(_hotbar, _session.Inventory.Hotbar, InventoryArea.Hotbar);
        RefreshGrid(_backpack, _session.Inventory.Backpack, InventoryArea.Backpack);
        RefreshGrid(_craft2, _session.Inventory.Crafting, InventoryArea.Crafting);
        RefreshWorkbenchGrid();
        RefreshHoverText();
    }

    private void BuildHotbar()
    {
        var center = new CenterContainer { AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetTop = -92, OffsetBottom = -20 };
        AddChild(center);
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle(new Color(0.02f,0.02f,0.016f,0.84f), 4)); center.AddChild(panel);
        _hotbar = new HBoxContainer(); panel.AddChild(_hotbar);
        for (var i = 0; i < InventoryState.HotbarSize; i++) _hotbar.AddChild(NewSlot(InventoryArea.Hotbar, i));
    }

    private void BuildInventory()
    {
        _inventoryPanel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(760, 620) };
        _inventoryPanel.AnchorLeft = 0.5f; _inventoryPanel.AnchorTop = 0.5f; _inventoryPanel.AnchorRight = 0.5f; _inventoryPanel.AnchorBottom = 0.5f;
        _inventoryPanel.OffsetLeft = -380; _inventoryPanel.OffsetTop = -310; _inventoryPanel.OffsetRight = 380; _inventoryPanel.OffsetBottom = 310;
        _inventoryPanel.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle(new Color(0.025f,0.025f,0.02f,0.97f), 6));
        AddChild(_inventoryPanel);
        var root = new VBoxContainer(); _inventoryPanel.AddChild(root);
        root.AddChild(GodotTheme.Label("ИНВЕНТАРЬ", 28, HorizontalAlignment.Center));
        root.AddChild(GodotTheme.Label("Крафт 2x2", 18));
        _craft2 = new GridContainer { Columns = 2 }; root.AddChild(_craft2);
        for (var i = 0; i < 4; i++) _craft2.AddChild(NewSlot(InventoryArea.Crafting, i));
        var craft = GodotTheme.Button("СОЗДАТЬ", 220); craft.Pressed += () => { _session.CraftPersonal(); Refresh(); }; root.AddChild(craft);
        root.AddChild(GodotTheme.Label("Кожаный рюкзак / слоты", 18));
        _backpack = new GridContainer { Columns = 8 }; root.AddChild(_backpack);
        for (var i = 0; i < _session.Inventory.BackpackCapacity; i++) _backpack.AddChild(NewSlot(InventoryArea.Backpack, i));
        _hover = GodotTheme.Label("Наведи на предмет: здесь будет разбор. Q — разобрать.", 17); _hover.AutowrapMode = TextServer.AutowrapMode.WordSmart; root.AddChild(_hover);
    }

    private void BuildWorkbench()
    {
        _workbenchPanel = new PanelContainer { Visible = false };
        _workbenchPanel.AnchorLeft = 0.5f; _workbenchPanel.AnchorTop = 0.5f; _workbenchPanel.AnchorRight = 0.5f; _workbenchPanel.AnchorBottom = 0.5f;
        _workbenchPanel.OffsetLeft = -520; _workbenchPanel.OffsetTop = -330; _workbenchPanel.OffsetRight = 520; _workbenchPanel.OffsetBottom = 330;
        _workbenchPanel.AddThemeStyleboxOverride("panel", GodotTheme.PanelStyle(new Color(0.02f,0.02f,0.016f,0.98f), 6)); AddChild(_workbenchPanel);
        var root = new HBoxContainer(); _workbenchPanel.AddChild(root);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(420, 600) }; root.AddChild(left);
        left.AddChild(GodotTheme.Label("РЕЦЕПТЫ", 25, HorizontalAlignment.Center));
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; left.AddChild(scroll);
        _recipes = new VBoxContainer(); scroll.AddChild(_recipes);
        foreach (var recipe in _session.Crafting.Recipes.Where(r => r.WorkbenchOnly))
        {
            var r = recipe;
            var b = GodotTheme.Button($"{r.Category}: {r.Name}", 380); b.CustomMinimumSize = new Vector2(380, 40);
            b.Pressed += () => CraftWorkbench(r);
            _recipes.AddChild(b);
        }
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(440, 600) }; root.AddChild(right);
        right.AddChild(GodotTheme.Label("ВЕРСТАК 3x3", 25, HorizontalAlignment.Center));
        _craft3 = new GridContainer { Columns = 3 }; right.AddChild(_craft3);
        for (var i = 0; i < 9; i++)
        {
            var idx = i; var b = SlotButton();
            b.Pressed += () => ClickWorkbench(idx);
            _craft3.AddChild(b);
        }
        var close = GodotTheme.Button("ЗАКРЫТЬ", 220); close.Pressed += () => { ReturnWorkbenchItems(); _workbenchPanel.Visible = false; }; right.AddChild(close);
    }

    private Button NewSlot(InventoryArea area, int index)
    {
        var b = SlotButton();
        var slot = new InventorySlotRef(area, index);
        b.Pressed += () => { _session.Inventory.Click(slot, _session.DropNearPlayer); Refresh(); };
        b.MouseEntered += () => { HoveredSlot = slot; RefreshHoverText(); };
        b.MouseExited += () => { if (HoveredSlot == slot) HoveredSlot = null; RefreshHoverText(); };
        b.SetMeta("slot_area", (int)area); b.SetMeta("slot_index", index);
        return b;
    }

    private static Button SlotButton()
    {
        var b = new Button { CustomMinimumSize = new Vector2(74, 64) };
        GodotTheme.Apply(b, 15);
        b.AddThemeStyleboxOverride("normal", GodotTheme.PanelStyle(new Color(0.06f,0.06f,0.045f,0.95f), 3));
        b.AddThemeStyleboxOverride("hover", GodotTheme.PanelStyle(new Color(0.18f,0.16f,0.08f,0.98f), 3));
        return b;
    }

    private void RefreshGrid(Container grid, IReadOnlyList<ItemStack?> items, InventoryArea area)
    {
        var buttons = grid.GetChildren().OfType<Button>().ToArray();
        for (var i = 0; i < buttons.Length; i++)
        {
            var stack = i < items.Count ? items[i] : null;
            buttons[i].Text = stack is null ? (area == InventoryArea.Hotbar ? $"{i + 1}\n—" : "—") : $"{(area == InventoryArea.Hotbar ? (i + 1) + "\n" : "")}{_session.Items.Get(stack.ItemId).Name}\nx{stack.Quantity}";
            if (area == InventoryArea.Hotbar)
                buttons[i].Modulate = i == _session.Player.SelectedHotbarSlot ? new Color(1f,0.95f,0.65f) : Colors.White;
        }
    }

    private void RefreshHoverText()
    {
        if (_hover is null || HoveredSlot is not { } slot) return;
        var stack = _session.Inventory.Get(slot);
        if (stack is null) { _hover.Text = "Пустой слот"; return; }
        var def = _session.Items.Get(stack.ItemId);
        var breakdown = def.Materials.Count == 0 ? "не разбирается" : string.Join(", ", def.Materials.Select(x => $"{_session.Items.Get(x.Key).Name} x{x.Value}"));
        _hover.Text = $"{def.Name} — {breakdown}.  Q — разобрать.";
    }

    private void ClickWorkbench(int index)
    {
        if (_session.Inventory.Cursor is null)
        {
            if (_workbench[index] is null) return;
            _session.Inventory.Add(_workbench[index]!, _session.Items, _session.DropNearPlayer);
            _workbench[index] = null;
        }
        else
        {
            var cursor = _session.Inventory.TakeCursor()!;
            if (_workbench[index] is { } old) _session.Inventory.Add(old, _session.Items, _session.DropNearPlayer);
            _workbench[index] = cursor;
        }
        Refresh();
    }

    private void CraftWorkbench(CraftingRecipe recipe)
    {
        var result = _session.CraftAtWorkbench(recipe, _workbench);
        if (result is not null) Refresh();
    }

    private void ReturnWorkbenchItems()
    {
        for (var i = 0; i < _workbench.Length; i++)
        {
            if (_workbench[i] is { } stack) _session.Inventory.Add(stack, _session.Items, _session.DropNearPlayer);
            _workbench[i] = null;
        }
        Refresh();
    }

    private void RefreshWorkbenchGrid()
    {
        if (_craft3 is null) return;
        var buttons = _craft3.GetChildren().OfType<Button>().ToArray();
        for (var i = 0; i < buttons.Length; i++)
        {
            var stack = _workbench[i];
            buttons[i].Text = stack is null ? "—" : $"{_session.Items.Get(stack.ItemId).Name}\nx{stack.Quantity}";
        }
    }
}
