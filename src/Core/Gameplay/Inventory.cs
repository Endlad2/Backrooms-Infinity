namespace Backrooms.Engine.Gameplay;

public enum ItemKind
{
    Material,
    Tool,
    Weapon,
    Furniture,
    Consumable,
    Backpack,
    OutpostFlag,
    Misc
}

public sealed record ItemDefinition(
    string Id,
    string Name,
    ItemKind Kind,
    int MaxStack = 1,
    bool Placeable = false,
    int BackpackCapacity = 0,
    IReadOnlyDictionary<string, int>? DecomposesTo = null,
    IReadOnlyDictionary<string, string>? Properties = null)
{
    public IReadOnlyDictionary<string, int> Materials =>
        DecomposesTo ?? EmptyMaterials;

    private static readonly IReadOnlyDictionary<string, int> EmptyMaterials =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

public sealed record ItemStack(string ItemId, int Quantity)
{
    public ItemStack WithQuantity(int quantity) => this with { Quantity = quantity };
}

public enum InventoryArea
{
    Hotbar,
    Backpack,
    Crafting
}

public readonly record struct InventorySlotRef(InventoryArea Area, int Index);

public sealed class ItemRegistry
{
    private readonly Dictionary<string, ItemDefinition> _items = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<ItemDefinition> All => _items.Values;

    public ItemRegistry(IEnumerable<ItemDefinition>? initial = null)
    {
        foreach (var item in initial ?? DefaultItems())
            AddOrReplace(item);
    }

    public void AddOrReplace(ItemDefinition item) => _items[item.Id] = item;

    public bool TryGet(string id, out ItemDefinition item) => _items.TryGetValue(id, out item!);

    public ItemDefinition Get(string id) =>
        TryGet(id, out var item) ? item : new ItemDefinition(id, id, ItemKind.Misc);

    public static IEnumerable<ItemDefinition> DefaultItems()
    {
        yield return new ItemDefinition("wood", "Доска", ItemKind.Material, 64);
        yield return new ItemDefinition("metal", "Металл", ItemKind.Material, 64);
        yield return new ItemDefinition("cloth", "Ткань", ItemKind.Material, 64);
        yield return new ItemDefinition("plastic", "Пластик", ItemKind.Material, 64);
        yield return new ItemDefinition("glass", "Стекло", ItemKind.Material, 64);
        yield return new ItemDefinition("wire", "Провод", ItemKind.Material, 64);
        yield return new ItemDefinition("scrap", "Лом", ItemKind.Material, 64);
        yield return new ItemDefinition("chair", "Стул", ItemKind.Furniture, 1, true, DecomposesTo: new Dictionary<string, int> { ["wood"] = 4, ["metal"] = 1 });
        yield return new ItemDefinition("desk", "Стол", ItemKind.Furniture, 1, true, DecomposesTo: new Dictionary<string, int> { ["wood"] = 6, ["metal"] = 2 });
        yield return new ItemDefinition("shelf", "Стеллаж", ItemKind.Furniture, 1, true, DecomposesTo: new Dictionary<string, int> { ["metal"] = 6 });
        yield return new ItemDefinition("lamp", "Лампа", ItemKind.Furniture, 1, true, DecomposesTo: new Dictionary<string, int> { ["metal"] = 1, ["glass"] = 1, ["wire"] = 2 });
        yield return new ItemDefinition("workbench", "Верстак", ItemKind.Furniture, 1, true, DecomposesTo: new Dictionary<string, int> { ["wood"] = 4 });
        yield return new ItemDefinition("club", "Дубинка", ItemKind.Weapon, 1, false, DecomposesTo: new Dictionary<string, int> { ["wood"] = 2 });
        yield return new ItemDefinition("spear", "Копьё", ItemKind.Weapon, 1, false, DecomposesTo: new Dictionary<string, int> { ["wood"] = 2, ["metal"] = 1 });
        yield return new ItemDefinition("crowbar", "Ломик", ItemKind.Tool, 1, false, DecomposesTo: new Dictionary<string, int> { ["metal"] = 3 });
        yield return new ItemDefinition("axe", "Топор", ItemKind.Tool, 1, false, DecomposesTo: new Dictionary<string, int> { ["wood"] = 1, ["metal"] = 2 });
        yield return new ItemDefinition("leather_backpack", "Кожаный рюкзак", ItemKind.Backpack, 1, false, 16, new Dictionary<string, int> { ["cloth"] = 6 });
        yield return new ItemDefinition("field_backpack", "Полевой рюкзак", ItemKind.Backpack, 1, false, 24, new Dictionary<string, int> { ["cloth"] = 8, ["plastic"] = 4 });
        yield return new ItemDefinition("expedition_backpack", "Экспедиционный рюкзак", ItemKind.Backpack, 1, false, 32, new Dictionary<string, int> { ["cloth"] = 12, ["metal"] = 4 });
        yield return new ItemDefinition("outpost_flag_1", "Флаг аванпоста I", ItemKind.OutpostFlag, 1, true);
        yield return new ItemDefinition("outpost_flag_2", "Флаг аванпоста II", ItemKind.OutpostFlag, 1, true);
        yield return new ItemDefinition("outpost_flag_3", "Флаг аванпоста III", ItemKind.OutpostFlag, 1, true);
        yield return new ItemDefinition("outpost_flag_4", "Флаг аванпоста IV", ItemKind.OutpostFlag, 1, true);
        yield return new ItemDefinition("extractor", "Добывалка", ItemKind.Furniture, 1, true);
        yield return new ItemDefinition("generator", "Генератор", ItemKind.Furniture, 1, true);
        yield return new ItemDefinition("storage_box", "Ящик", ItemKind.Furniture, 1, true);
        yield return new ItemDefinition("bed", "Кровать", ItemKind.Furniture, 1, true);
    }
}

public sealed class InventoryState
{
    public const int HotbarSize = 4;
    public const int CraftingSize = 4;

    private readonly ItemStack?[] _hotbar = new ItemStack?[HotbarSize];
    private ItemStack?[] _backpack;
    private readonly ItemStack?[] _crafting = new ItemStack?[CraftingSize];

    public IReadOnlyList<ItemStack?> Hotbar => _hotbar;
    public IReadOnlyList<ItemStack?> Backpack => _backpack;
    public IReadOnlyList<ItemStack?> Crafting => _crafting;
    public int BackpackCapacity => _backpack.Length;
    public string EquippedBackpackId { get; private set; } = "leather_backpack";
    public ItemStack? Cursor { get; private set; }

    public InventoryState(int backpackCapacity = 16)
    {
        _backpack = new ItemStack?[Math.Max(1, backpackCapacity)];
    }

    public ItemStack? Get(InventorySlotRef slot)
    {
        var array = ArrayFor(slot.Area);
        return slot.Index >= 0 && slot.Index < array.Length ? array[slot.Index] : null;
    }

    public void Click(InventorySlotRef slot, Action<ItemStack>? eject)
    {
        var array = ArrayFor(slot.Area);
        if (slot.Index < 0 || slot.Index >= array.Length)
            return;

        var existing = array[slot.Index];
        if (Cursor is null)
        {
            if (existing is null) return;
            Cursor = existing;
            array[slot.Index] = null;
            return;
        }

        if (existing is not null)
            eject?.Invoke(existing);

        array[slot.Index] = Cursor;
        Cursor = null;
    }

    public void CancelCursor(Action<ItemStack>? eject)
    {
        if (Cursor is null) return;
        eject?.Invoke(Cursor);
        Cursor = null;
    }

    public ItemStack? TakeCursor()
    {
        var value = Cursor;
        Cursor = null;
        return value;
    }

    public bool Add(ItemStack stack, ItemRegistry registry, Action<ItemStack>? overflow = null)
    {
        if (stack.Quantity <= 0) return true;
        var remaining = stack.Quantity;
        var definition = registry.Get(stack.ItemId);
        var max = Math.Max(1, definition.MaxStack);

        remaining = MergeInto(_hotbar, stack.ItemId, remaining, max);
        remaining = MergeInto(_backpack, stack.ItemId, remaining, max);
        remaining = FillEmpty(_hotbar, stack.ItemId, remaining, max);
        remaining = FillEmpty(_backpack, stack.ItemId, remaining, max);

        if (remaining <= 0) return true;
        overflow?.Invoke(new ItemStack(stack.ItemId, remaining));
        return false;
    }

    public IReadOnlyDictionary<string, int> HoverBreakdown(InventorySlotRef slot, ItemRegistry registry)
    {
        var stack = Get(slot);
        return stack is null ? EmptyBreakdown : registry.Get(stack.ItemId).Materials;
    }

    public ItemStack? TakeOne(InventorySlotRef slot)
    {
        var array = ArrayFor(slot.Area);
        if (slot.Index < 0 || slot.Index >= array.Length || array[slot.Index] is not { } stack)
            return null;
        array[slot.Index] = stack.Quantity > 1 ? stack.WithQuantity(stack.Quantity - 1) : null;
        return new ItemStack(stack.ItemId, 1);
    }

    public bool Dismantle(InventorySlotRef slot, ItemRegistry registry, Action<ItemStack>? overflow = null)
    {
        var array = ArrayFor(slot.Area);
        if (slot.Index < 0 || slot.Index >= array.Length || array[slot.Index] is not { } stack)
            return false;

        var definition = registry.Get(stack.ItemId);
        if (definition.Materials.Count == 0)
            return false;

        array[slot.Index] = stack.Quantity > 1 ? stack.WithQuantity(stack.Quantity - 1) : null;
        foreach (var pair in definition.Materials)
            Add(new ItemStack(pair.Key, pair.Value), registry, overflow);
        return true;
    }

    public void EquipBackpack(string itemId, int capacity, Action<ItemStack>? overflow = null)
    {
        capacity = Math.Max(1, capacity);
        if (capacity == _backpack.Length)
        {
            EquippedBackpackId = itemId;
            return;
        }

        var old = _backpack;
        _backpack = new ItemStack?[capacity];
        EquippedBackpackId = itemId;
        for (var i = 0; i < old.Length; i++)
        {
            if (old[i] is null) continue;
            if (i < _backpack.Length && _backpack[i] is null)
                _backpack[i] = old[i];
            else
                overflow?.Invoke(old[i]!);
        }
    }

    public ItemStack?[] CopyCrafting() => _crafting.ToArray();

    public void ReplaceCrafting(IReadOnlyList<ItemStack?> slots)
    {
        for (var i = 0; i < _crafting.Length; i++)
            _crafting[i] = i < slots.Count ? slots[i] : null;
    }

    public void ClearCrafting(Action<ItemStack>? eject)
    {
        for (var i = 0; i < _crafting.Length; i++)
        {
            if (_crafting[i] is { } stack)
                eject?.Invoke(stack);
            _crafting[i] = null;
        }
    }

    private ItemStack?[] ArrayFor(InventoryArea area) => area switch
    {
        InventoryArea.Hotbar => _hotbar,
        InventoryArea.Backpack => _backpack,
        InventoryArea.Crafting => _crafting,
        _ => _backpack
    };

    private static int MergeInto(ItemStack?[] slots, string itemId, int remaining, int max)
    {
        foreach (var index in Enumerable.Range(0, slots.Length))
        {
            if (remaining <= 0) break;
            var current = slots[index];
            if (current is null || !string.Equals(current.ItemId, itemId, StringComparison.OrdinalIgnoreCase) || current.Quantity >= max)
                continue;
            var amount = Math.Min(max - current.Quantity, remaining);
            slots[index] = current.WithQuantity(current.Quantity + amount);
            remaining -= amount;
        }
        return remaining;
    }

    private static int FillEmpty(ItemStack?[] slots, string itemId, int remaining, int max)
    {
        for (var i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i] is not null) continue;
            var amount = Math.Min(max, remaining);
            slots[i] = new ItemStack(itemId, amount);
            remaining -= amount;
        }
        return remaining;
    }

    private static readonly IReadOnlyDictionary<string, int> EmptyBreakdown =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
