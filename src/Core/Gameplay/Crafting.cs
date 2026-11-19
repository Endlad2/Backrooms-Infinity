namespace Backrooms.Engine.Gameplay;

public sealed record CraftingRecipe(
    string Id,
    string Name,
    int Width,
    int Height,
    IReadOnlyList<string?> Pattern,
    ItemStack Result,
    bool WorkbenchOnly = true,
    string Category = "Предметы");

public sealed class CraftingBook
{
    private readonly List<CraftingRecipe> _recipes = new();
    public IReadOnlyList<CraftingRecipe> Recipes => _recipes;

    public CraftingBook(IEnumerable<CraftingRecipe>? recipes = null)
    {
        _recipes.AddRange(recipes ?? DefaultRecipes());
    }

    public CraftingRecipe? FindFor2x2(IReadOnlyList<ItemStack?> slots, ItemRegistry registry) =>
        _recipes.FirstOrDefault(r => !r.WorkbenchOnly && r.Width == 2 && r.Height == 2 && Matches(r, slots, 2, registry));

    public bool CanCraft(CraftingRecipe recipe, IReadOnlyList<ItemStack?> slots, int gridWidth, ItemRegistry registry) =>
        recipe.Width <= gridWidth && Matches(recipe, slots, gridWidth, registry);

    public bool Consume(CraftingRecipe recipe, ItemStack?[] slots, int gridWidth, ItemRegistry registry)
    {
        if (!Matches(recipe, slots, gridWidth, registry))
            return false;

        for (var y = 0; y < recipe.Height; y++)
        for (var x = 0; x < recipe.Width; x++)
        {
            var required = recipe.Pattern[y * recipe.Width + x];
            if (required is null) continue;
            var idx = y * gridWidth + x;
            var stack = slots[idx]!;
            slots[idx] = stack.Quantity > 1 ? stack.WithQuantity(stack.Quantity - 1) : null;
        }
        return true;
    }

    private static bool Matches(CraftingRecipe recipe, IReadOnlyList<ItemStack?> slots, int gridWidth, ItemRegistry registry)
    {
        if (slots.Count < gridWidth * recipe.Height)
            return false;

        for (var y = 0; y < recipe.Height; y++)
        for (var x = 0; x < recipe.Width; x++)
        {
            var required = recipe.Pattern[y * recipe.Width + x];
            var stack = slots[y * gridWidth + x];
            if (required is null)
            {
                if (stack is not null) return false;
                continue;
            }

            if (stack is null || !CanSupply(stack.ItemId, required, registry))
                return false;
        }
        return true;
    }

    private static bool CanSupply(string itemId, string requiredMaterialOrItem, ItemRegistry registry)
    {
        if (string.Equals(itemId, requiredMaterialOrItem, StringComparison.OrdinalIgnoreCase))
            return true;
        var item = registry.Get(itemId);
        return item.Materials.ContainsKey(requiredMaterialOrItem);
    }

    private static IEnumerable<CraftingRecipe> DefaultRecipes()
    {
        yield return new CraftingRecipe("workbench", "Верстак", 2, 2,
            new string?[] { "wood", "wood", "wood", "wood" }, new ItemStack("workbench", 1), false, "Мебель");

        yield return R("club", "Дубинка", "Оружие", new[] { "wood", null, "wood", null, null, null, null, null, null });
        yield return R("spear", "Копьё", "Оружие", new[] { "metal", null, null, "wood", null, null, "wood", null, null });
        yield return R("crowbar", "Ломик", "Инструменты", new[] { "metal", null, null, "metal", null, null, "metal", null, null });
        yield return R("axe", "Топор", "Инструменты", new[] { "metal", "metal", null, "wood", null, null, "wood", null, null });
        yield return R("chair", "Стул", "Мебель", new[] { "wood", null, "wood", "wood", null, "wood", null, null, null });
        yield return R("shelf", "Стеллаж", "Мебель", new[] { "metal", null, "metal", "metal", null, "metal", "metal", null, "metal" });
        yield return R("lamp", "Лампа", "Мебель", new[] { "glass", null, null, "wire", null, null, "metal", null, null });
        yield return R("storage_box", "Ящик", "Мебель", new[] { "wood", "wood", "wood", "wood", null, "wood", "wood", "wood", "wood" });
        yield return R("bed", "Кровать", "Мебель", new[] { "cloth", "cloth", "cloth", "wood", "wood", "wood", null, null, null });
        yield return R("generator", "Генератор", "Техника", new[] { "metal", "wire", "metal", "metal", "scrap", "metal", null, "wire", null });
        yield return R("extractor", "Добывалка", "Техника", new[] { "metal", "metal", "metal", "wire", "generator", "wire", "metal", "scrap", "metal" });
        yield return R("field_backpack", "Полевой рюкзак", "Снаряжение", new[] { "cloth", "cloth", "cloth", "plastic", null, "plastic", "cloth", "cloth", "cloth" });
        yield return R("expedition_backpack", "Экспедиционный рюкзак", "Снаряжение", new[] { "cloth", "metal", "cloth", "cloth", "field_backpack", "cloth", "plastic", "metal", "plastic" });
        yield return R("outpost_flag_1", "Флаг аванпоста I", "Аванпост", new[] { "cloth", "cloth", null, "wood", "wood", null, "wood", null, null });
        yield return R("outpost_flag_2", "Флаг аванпоста II", "Аванпост", new[] { "cloth", "cloth", "metal", "wood", "outpost_flag_1", "metal", "wood", "wire", null });
        yield return R("outpost_flag_3", "Флаг аванпоста III", "Аванпост", new[] { "cloth", "metal", "cloth", "metal", "outpost_flag_2", "metal", "wire", "generator", "wire" });
        yield return R("outpost_flag_4", "Флаг аванпоста IV", "Аванпост", new[] { "metal", "cloth", "metal", "wire", "outpost_flag_3", "wire", "metal", "extractor", "metal" });
    }

    private static CraftingRecipe R(string id, string name, string category, string?[] pattern) =>
        new(id, name, 3, 3, pattern, new ItemStack(id, 1), true, category);
}
