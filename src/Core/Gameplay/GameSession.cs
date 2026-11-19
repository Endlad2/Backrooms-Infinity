using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Mathematics;
using Backrooms.Engine.Platform;

namespace Backrooms.Engine.Gameplay;

public sealed record WorldDrop(Guid Id, ItemStack Stack, Vec3 Position);
public sealed record PlacedWorldItem(Guid Id, string ItemId, Vec3 Position, float YawDegrees);

public sealed class GameSession
{
    private readonly DiagnosticBus _diagnostics;
    private readonly List<WorldDrop> _drops = new();
    private readonly List<PlacedWorldItem> _placed = new();
    private readonly Random _random;

    public LevelDefinition Level { get; }
    public string LevelId { get; }
    public GameSettings Settings { get; private set; }
    public PlayerState Player { get; } = new();
    public PlayerController Controller { get; }
    public ItemRegistry Items { get; }
    public InventoryState Inventory { get; } = new(16);
    public CraftingBook Crafting { get; } = new();
    public OutpostService Outposts { get; } = new();
    public MonsterRuntime Monsters { get; } = new();
    public IReadOnlyList<WorldDrop> Drops => _drops;
    public IReadOnlyList<PlacedWorldItem> PlacedItems => _placed;
    public event Action<string>? Notification;

    public GameSession(LevelDefinition level, GameSettings settings, DiagnosticBus diagnostics)
    {
        Level = level;
        LevelId = level.Metadata.TryGetValue("level_id", out var id) ? id : level.Name;
        Settings = settings;
        _diagnostics = diagnostics;
        _random = new Random(StringComparer.Ordinal.GetHashCode(LevelId));
        Controller = new PlayerController(Player);
        Items = new ItemRegistry();
        RegisterLevelItems(level);
        SpawnLevelMonsters(level);
        Monsters.OutpostIntrusion += monster =>
        {
            var message = $"ВНИМАНИЕ: {monster.Definition.DisplayName} вошёл на территорию аванпоста.";
            _diagnostics.Warn("Outpost", message);
            Notification?.Invoke(message);
        };
    }

    public void ApplySettings(GameSettings settings)
    {
        Settings = settings;
        Settings.Save();
    }

    public void Tick(double deltaSeconds, PlayerInput input)
    {
        Controller.Update(deltaSeconds, input, Settings);
        Monsters.Tick(deltaSeconds, Player.Position, LevelId, Outposts);
    }

    public void DropNearPlayer(ItemStack stack)
    {
        var angle = (float)(_random.NextDouble() * Math.PI * 2);
        var position = Player.Position + new Vec3(MathF.Cos(angle) * 0.8f, -1.4f, MathF.Sin(angle) * 0.8f);
        _drops.Add(new WorldDrop(Guid.NewGuid(), stack, position));
        _diagnostics.Info("Inventory", $"Dropped {stack.Quantity}x {Items.Get(stack.ItemId).Name} at {position}.");
    }

    public bool PickupDrop(Guid id)
    {
        var index = _drops.FindIndex(d => d.Id == id);
        if (index < 0) return false;
        var drop = _drops[index];
        _drops.RemoveAt(index);
        Inventory.Add(drop.Stack, Items, DropNearPlayer);
        return true;
    }

    public bool Dismantle(InventorySlotRef slot)
    {
        var ok = Inventory.Dismantle(slot, Items, DropNearPlayer);
        if (ok)
            Notification?.Invoke("Предмет разобран на материалы.");
        return ok;
    }

    public ItemStack? CraftPersonal()
    {
        var slots = Inventory.CopyCrafting();
        var recipe = Crafting.FindFor2x2(slots, Items);
        if (recipe is null) return null;
        if (!Crafting.Consume(recipe, slots, 2, Items)) return null;
        Inventory.ReplaceCrafting(slots);
        Inventory.Add(recipe.Result, Items, DropNearPlayer);
        Notification?.Invoke($"Создано: {Items.Get(recipe.Result.ItemId).Name}");
        return recipe.Result;
    }

    public ItemStack? CraftAtWorkbench(CraftingRecipe recipe, ItemStack?[] grid)
    {
        if (!recipe.WorkbenchOnly || grid.Length != 9 || !Crafting.Consume(recipe, grid, 3, Items))
            return null;
        Inventory.Add(recipe.Result, Items, DropNearPlayer);
        Notification?.Invoke($"Верстак: создано {Items.Get(recipe.Result.ItemId).Name}");
        return recipe.Result;
    }

    public PlacedWorldItem? PlaceFromHotbar(int slot)
    {
        var taken = Inventory.TakeOne(new InventorySlotRef(InventoryArea.Hotbar, slot));
        if (taken is null) return null;
        var definition = Items.Get(taken.ItemId);
        if (!definition.Placeable)
        {
            Inventory.Add(taken, Items, DropNearPlayer);
            return null;
        }

        if (definition.Kind == ItemKind.OutpostFlag)
        {
            var tier = taken.ItemId.EndsWith("_4", StringComparison.Ordinal) ? 4 :
                       taken.ItemId.EndsWith("_3", StringComparison.Ordinal) ? 3 :
                       taken.ItemId.EndsWith("_2", StringComparison.Ordinal) ? 2 : 1;
            if (!PlaceOutpostFlag(tier))
            {
                Inventory.Add(taken, Items, DropNearPlayer);
                return null;
            }
        }

        if (string.Equals(taken.ItemId, "extractor", StringComparison.OrdinalIgnoreCase))
        {
            if (!Outposts.TryGet(LevelId, out var outpost) ||
                !Outposts.IsInside(LevelId, Player.Position) ||
                !OutpostService.Tier(outpost.Tier).AllowsExtractor)
            {
                Notification?.Invoke("Добывалку можно ставить только внутри аванпоста II уровня или выше.");
                Inventory.Add(taken, Items, DropNearPlayer);
                return null;
            }
        }

        var yaw = Player.YawDegrees * MathF.PI / 180f;
        var pos = Player.Position + new Vec3(MathF.Sin(yaw) * 1.8f, -1.6f, -MathF.Cos(yaw) * 1.8f);
        var placed = new PlacedWorldItem(Guid.NewGuid(), taken.ItemId, pos, Player.YawDegrees);
        _placed.Add(placed);
        Notification?.Invoke($"Установлено: {definition.Name}");
        return placed;
    }

    public bool HasWorkbenchInReach(float maxDistance = 3f)
    {
        var maxSq = maxDistance * maxDistance;
        return _placed.Any(p => string.Equals(p.ItemId, "workbench", StringComparison.OrdinalIgnoreCase) &&
            DistanceSquared(p.Position, Player.Position) <= maxSq);
    }

    private static float DistanceSquared(Vec3 a, Vec3 b)
    {
        var d = a - b;
        return d.X * d.X + d.Y * d.Y + d.Z * d.Z;
    }

    public bool PlaceOutpostFlag(int tier)
    {
        if (!Outposts.TryPlace(LevelId, Level.Name, tier, Player.Position, out var message))
        {
            Notification?.Invoke(message);
            return false;
        }
        Notification?.Invoke(message);
        return true;
    }

    private void RegisterLevelItems(LevelDefinition level)
    {
        foreach (var item in level.Items)
        {
            var kind = Enum.TryParse<ItemKind>(item.Kind, true, out var parsed) ? parsed : ItemKind.Misc;
            Items.AddOrReplace(new ItemDefinition(
                item.Id,
                item.Name,
                kind,
                item.MaxStack,
                item.Placeable,
                item.BackpackCapacity,
                item.DecomposesTo,
                item.Properties));
        }
    }

    private void SpawnLevelMonsters(LevelDefinition level)
    {
        foreach (var spec in level.Monsters)
        {
            var definition = new MonsterDefinition(spec.Id, spec.Name, spec.Hostile, spec.WalkSpeed, spec.ChaseSpeed, spec.DetectionRadius);
            for (var i = 0; i < spec.MaxAlive; i++)
            {
                var angle = (float)(_random.NextDouble() * Math.PI * 2);
                var distance = 18f + (float)_random.NextDouble() * 42f;
                Monsters.Spawn(definition, $"{spec.Id}_{i + 1}", new Vec3(MathF.Cos(angle) * distance, 0, MathF.Sin(angle) * distance));
            }
        }
        if (level.Monsters.Count > 0)
            _diagnostics.Info("Monsters", $"Spawned {Monsters.Monsters.Count} monster instance(s) from level metadata.");
    }
}
