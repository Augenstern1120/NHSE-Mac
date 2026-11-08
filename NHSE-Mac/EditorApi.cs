using NHSE.Core;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

public static class EditorApi
{
    public static void MapEditorApi(this WebApplication app, string outputRoot)
    {
        app.MapPost("/api/editor", (EditorReadRequest request) =>
        {
            try
            {
                var source = EditorPath(request.Path);
                var save = HorizonSave.FromFolder(source);
                if (!save.ValidateSizes()) return Results.BadRequest(new { error = "存档版本或文件大小不受当前 NHSE 支持。" });
                if (request.Player < 0 || request.Player >= save.Players.Count) return Results.BadRequest(new { error = "玩家编号无效。" });
                return Results.Json(new {
                    path = source,
                    fields = GetFields(save, request.Player),
                    inventories = GetInventories(save, request.Player),
                    recipes = GetRecipes(save, request.Player),
                    invalidHashes = save.GetInvalidHashes().Count()
                });
            }
            catch (Exception e) { return Results.BadRequest(new { error = e.Message }); }
        });
        app.MapGet("/api/itemname/{id:int}", (int id) =>
        {
            if ((uint)id > ushort.MaxValue) return Results.BadRequest(new { error = "物品编号无效。" });
            return Results.Json(ItemMetadata(new Item((ushort)id)));
        });
        app.MapPost("/api/editor/save", (EditorWriteRequest request) =>
        {
            string? destination = null;
            try
            {
                var source = EditorPath(request.Path);
                var original = HorizonSave.FromFolder(source);
                if (!original.ValidateSizes() || original.GetInvalidHashes().Any()) return Results.BadRequest(new { error = "源存档校验未通过，已停止保存。" });
                if (request.Player < 0 || request.Player >= original.Players.Count) return Results.BadRequest(new { error = "玩家编号无效。" });
                if (request.Fields.Count + request.Items.Count + request.Recipes.Count == 0) return Results.BadRequest(new { error = "没有需要保存的修改。" });
                // Reject every invalid edit before creating an output folder.
                foreach (var edit in request.Fields) ValidateField(original, request.Player, edit);
                foreach (var edit in request.Items) ValidateItem(original, request.Player, edit);
                foreach (var edit in request.Recipes) if (edit.Index < 0 || edit.Index >= RecipeBook.RecipeCount) throw new ArgumentException("配方编号无效。");
                Directory.CreateDirectory(outputRoot);
                destination = Path.Combine(outputRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
                CopyFolder(source, destination);
                var save = HorizonSave.FromFolder(destination);
                foreach (var edit in request.Fields) ApplyField(save, request.Player, edit);
                foreach (var edit in request.Items) ApplyItem(save, request.Player, edit);
                if (request.Recipes.Count != 0)
                {
                    var personal = save.Players[request.Player].Personal;
                    var book = personal.GetRecipeBook();
                    foreach (var edit in request.Recipes)
                    {
                        book.SetIsKnown(edit.Index, edit.Known);
                        book.SetIsMade(edit.Index, edit.Made);
                        book.SetIsNew(edit.Index, edit.New);
                        book.SetIsFavorite(edit.Index, edit.Favorite);
                    }
                    personal.SetRecipeBook(book);
                }
                save.Save((uint)RandomNumberGenerator.GetInt32(int.MaxValue));
                var check = HorizonSave.FromFolder(destination);
                if (!check.ValidateSizes() || check.GetInvalidHashes().Any()) throw new InvalidDataException("保存后的存档校验失败。");
                return Results.Json(new { destination });
            }
            catch (Exception e)
            {
                if (destination is not null && Directory.Exists(destination)) Directory.Delete(destination, true);
                return Results.BadRequest(new { error = e.InnerException?.Message ?? e.Message });
            }
        });
    }

    private static string EditorPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("请输入存档文件夹路径。");
        var input = value.Trim();
        if (input.Length >= 2 && ((input[0] == '"' && input[^1] == '"') || (input[0] == '\'' && input[^1] == '\''))) input = input[1..^1].Trim();
        var path = Path.GetFullPath(input);
        if (!Directory.Exists(path) || !File.Exists(Path.Combine(path, "main.dat")) || !File.Exists(Path.Combine(path, "mainHeader.dat")))
            throw new DirectoryNotFoundException("请选择包含 main.dat 和 mainHeader.dat 的存档文件夹。");
        return path;
    }
    private static void CopyFolder(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(source)) CopyFolder(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
    private static Dictionary<string, object> Targets(HorizonSave save, int player)
    {
        var personal = save.Players[player].Personal;
        var targets = new Dictionary<string, object> { ["Personal"] = personal, ["Main"] = save.Main };
        if (personal.Data30 is { IsInitialized30: true } addition) targets["Data30"] = addition;
        if (save.Players[player].WhereAreN is { } where) targets["WhereAreN"] = where;
        for (var i = 0; i < MainSaveOffsets.VillagerCount; i++)
            targets["Villager" + i] = save.Main.GetVillager(i);
        return targets;
    }
    private static bool IsEditableType(Type type) => type == typeof(string) || type == typeof(bool) || type.IsEnum ||
        type == typeof(byte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int) ||
        type == typeof(uint) || type == typeof(long) || type == typeof(ulong) || type == typeof(EncryptedInt32);
    private static bool IsBasicField(string group, string name) => group switch
    {
        "Personal" => name is "PlayerName" or "TownName" or "Wallet" or "Bank" or "NookMiles" or "TotalNookMiles" or "PocketCount" or "BagCount" or "ItemChestCount" or "ProfileFruit",
        "Main" => name is "Hemisphere" or "TourHemisphere" or "AirportThemeColor" or "SpecialtyFruit" or "SisterFruit" or "SpecialtyFlower" or "SisterFlower",
        "Data30" => name is "HotelTickets",
        "WhereAreN" => name is "Poki",
        _ when group.StartsWith("Villager", StringComparison.Ordinal) => name is "Species" or "Personality" or "CatchPhrase" or "MovingOut",
        _ => false,
    };
    private static PropertyInfo Property(object target, string name)
    {
        var p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p is null || !p.CanRead || !p.CanWrite || p.GetIndexParameters().Length != 0 || !IsEditableType(p.PropertyType))
            throw new ArgumentException($"字段 {name} 不支持编辑。");
        return p;
    }
    private static object[] GetFields(HorizonSave save, int player) => Targets(save, player).SelectMany(group =>
        group.Value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && IsEditableType(p.PropertyType))
            .Where(p => p.Name is not ("TownID" or "PlayerID"))
            .Where(p => IsBasicField(group.Key, p.Name))
            .Select(p => {
                try {
                    var value = p.GetValue(group.Value);
                    if (value is EncryptedInt32 encrypted) value = encrypted.Value;
                    var key = group.Key + "." + p.Name;
                    var speciesOptions = key.EndsWith(".Species", StringComparison.Ordinal) && group.Value is IVillager v
                        ? Enum.GetValues<VillagerSpecies>().Select(x => {
                            var strings = GameInfo.GetStrings("zhs");
                            var variantName = VillagerUtil.GetInternalVillagerName(x, v.Variant);
                            var label = strings.GetVillager(variantName);
                            if (label == variantName) {
                                var defaultName = VillagerUtil.GetInternalVillagerName(x, 0);
                                label = strings.GetVillager(defaultName);
                            }
                            return new { value = (int)x, label };
                        }).ToArray()
                        : null;
                    var fruitOptions = key is "Personal.ProfileFruit" or "Main.SpecialtyFruit" or "Main.SisterFruit"
                        ? GameLists.Fruits.ToArray().Select(x => new { value = x, label = GameInfo.GetStrings("zhs").GetItemName(x) }).ToArray()
                        : null;
                    var kind = speciesOptions is not null ? "villagerSpecies" : fruitOptions is not null ? "fruit" : p.PropertyType.IsEnum ? "enum" : p.PropertyType == typeof(bool) ? "bool" : p.PropertyType == typeof(string) ? "text" : "number";
                    return (object?)new { key, value = Convert.ToString(value, CultureInfo.InvariantCulture), display = DisplayValue(key, value, group.Value), kind, options = p.PropertyType.IsEnum ? Enum.GetNames(p.PropertyType) : null, speciesOptions, fruitOptions };
                } catch { return null; }
            }).Where(x => x is not null).Select(x => x!)).ToArray();
    private static (object target, PropertyInfo property) ResolveField(HorizonSave save, int player, string key)
    {
        var parts = key.Split('.', 2);
        if (parts.Length != 2 || !Targets(save, player).TryGetValue(parts[0], out var target)) throw new ArgumentException($"未知字段 {key}。");
        if (!IsBasicField(parts[0], parts[1])) throw new ArgumentException($"字段 {key} 不在当前编辑范围内。");
        return (target, Property(target, parts[1]));
    }
    private static object ConvertValue(Type type, string value)
    {
        if (type == typeof(string)) return value.Length <= 40 ? value : throw new ArgumentException("文字过长。");
        if (type == typeof(bool)) return bool.Parse(value);
        if (type.IsEnum) return Enum.Parse(type, value, false);
        if (type == typeof(EncryptedInt32)) throw new InvalidOperationException();
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
    private static void ValidateField(HorizonSave save, int player, FieldEdit edit)
    {
        var (_, property) = ResolveField(save, player, edit.Key);
        if (property.PropertyType == typeof(EncryptedInt32)) _ = uint.Parse(edit.Value, CultureInfo.InvariantCulture);
        else _ = ConvertValue(property.PropertyType, edit.Value);
        if ((edit.Key is "Personal.PlayerName" or "Personal.TownName") && edit.Value.Length > 10) throw new ArgumentException("名字最多 10 个字符。");
    }
    private static void ApplyField(HorizonSave save, int player, FieldEdit edit)
    {
        var (target, property) = ResolveField(save, player, edit.Key);
        if (property.PropertyType == typeof(EncryptedInt32))
        {
            var original = (EncryptedInt32)property.GetValue(target)!;
            property.SetValue(target, original with { Value = uint.Parse(edit.Value, CultureInfo.InvariantCulture) });
            return;
        }
        if (edit.Key is "Personal.PlayerName" or "Personal.TownName")
        {
            var personal = save.Players[player].Personal;
            var old = edit.Key.EndsWith("PlayerName") ? personal.GetPlayerIdentity().ToArray() : personal.GetTownIdentity().ToArray();
            property.SetValue(target, edit.Value);
            var updated = edit.Key.EndsWith("PlayerName") ? personal.GetPlayerIdentity().ToArray() : personal.GetTownIdentity().ToArray();
            save.ChangeIdentity(old, updated);
            return;
        }
        property.SetValue(target, ConvertValue(property.PropertyType, edit.Value));
        if (edit.Key.StartsWith("Villager", StringComparison.Ordinal))
        {
            var index = int.Parse(edit.Key[8..edit.Key.IndexOf('.')], CultureInfo.InvariantCulture);
            save.Main.SetVillager((IVillager)target, index);
        }
    }
    private static Dictionary<string, IReadOnlyList<Item>> Inventories(HorizonSave save, int player)
    {
        var p = save.Players[player].Personal;
        return new() { ["Pocket"] = p.Pocket, ["Bag"] = p.Bag, ["ItemChest"] = p.ItemChest, ["RecycleBin"] = save.Main.RecycleBin };
    }
    private static object GetInventories(HorizonSave save, int player) => Inventories(save, player).ToDictionary(
        pair => pair.Key,
        pair => pair.Value.Select((item, index) => {
            var meta = ItemMetadata(item);
            return new { index, name = meta.Name, isFlower = meta.IsFlower, hasCount = meta.HasCount, maxCount = meta.MaxCount, hasUses = meta.HasUses, itemId = item.ItemId, count = item.Count, useCount = item.UseCount };
        }).ToArray());
    private static ItemMeta ItemMetadata(Item item)
    {
        var kind = ItemInfo.GetItemKind(item);
        var hasUses = kind is ItemKind.Kind_Axe or ItemKind.Kind_Net or ItemKind.Kind_Shovel or ItemKind.Kind_FishingRod or ItemKind.Kind_Watering or ItemKind.Kind_Slingshot or ItemKind.Kind_ChangeStick or ItemKind.Kind_WoodenStickTool or ItemKind.Kind_Ladder or ItemKind.Kind_GroundMaker or ItemKind.Kind_RiverMaker or ItemKind.Kind_CliffMaker;
        var hasCount = ItemInfo.TryGetMaxStackCount(item, out var maxCount) && maxCount > 1;
        return new ItemMeta(GameInfo.GetStrings("zhs").GetItemName(item), kind.IsFlowerGene(item.DisplayItemId), hasCount, hasCount ? maxCount : (ushort)0, hasUses);
    }
    private static string DisplayValue(string key, object? value, object target)
    {
        if (value is null) return "";
        if (value.GetType().IsEnum)
        {
            var enumName = value.ToString();
            return enumName switch {
                "Northern" => "北半球", "Southern" => "南半球",
                "Blue" => "蓝色", "Yellow" => "黄色", "Orange" => "橙色", "Green" => "绿色",
                "Windflower" => "银莲花", "Hyacinth" => "风信子", "Tulip" => "郁金香", "Rose" => "玫瑰",
                "Cosmos" => "波斯菊", "Lily" => "百合", "Mums" => "菊花", "Pansy" => "三色堇",
                "Lazy" => "悠闲", "Jock" => "运动", "Cranky" => "暴躁", "Smug" => "自恋",
                "Normal" => "普通", "Peppy" => "元气", "Snooty" => "成熟", "Uchi" => "大姐姐", _ => enumName ?? ""
            };
        }
        if (key.EndsWith(".Species", StringComparison.Ordinal) && target is IVillager villager)
        {
            var internalName = villager.InternalName;
            return GameInfo.GetStrings("zhs").GetVillager(internalName);
        }
        if ((key is "Personal.ProfileFruit" or "Main.SpecialtyFruit" or "Main.SisterFruit") && value is IConvertible)
        {
            try { return GameInfo.GetStrings("zhs").GetItemName(Convert.ToUInt16(value, CultureInfo.InvariantCulture)); }
            catch { }
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
    private static object GetRecipes(HorizonSave save, int player)
    {
        var book = save.Players[player].Personal.GetRecipeBook();
        return RecipeList.Recipes.OrderBy(pair => pair.Key).Select(pair => new {
            index = (int)pair.Key, name = GameInfo.GetStrings("zhs").GetItemName(pair.Value),
            known = book.GetIsKnown(pair.Key), made = book.GetIsMade(pair.Key), @new = book.GetIsNew(pair.Key), favorite = book.GetIsFavorite(pair.Key)
        }).ToArray();
    }
    private static void ValidateItem(HorizonSave save, int player, ItemEdit edit)
    {
        if (!Inventories(save, player).TryGetValue(edit.Inventory, out var items) || edit.Index < 0 || edit.Index >= items.Count)
            throw new ArgumentException("物品格位置无效。");
    }
    private static void ApplyItem(HorizonSave save, int player, ItemEdit edit)
    {
        var p = save.Players[player].Personal;
        var current = Inventories(save, player)[edit.Inventory].ToArray();
        var updated = current[edit.Index];
        updated.ItemId = edit.ItemId;
        if (edit.Count is { } count) updated.Count = count;
        if (edit.UseCount is { } useCount) updated.UseCount = useCount;
        current[edit.Index] = updated;
        switch (edit.Inventory)
        {
            case "Pocket": p.Pocket = current; break;
            case "Bag": p.Bag = current; break;
            case "ItemChest": p.ItemChest = current; break;
            case "RecycleBin": save.Main.RecycleBin = current; break;
        }
    }
}
public record EditorReadRequest(string Path, int Player);
public record FieldEdit(string Key, string Value);
public record ItemEdit(string Inventory, int Index, ushort ItemId, ushort? Count, ushort? UseCount);
public record RecipeEdit(int Index, bool Known, bool Made, bool New, bool Favorite);
public record ItemMeta(string Name, bool IsFlower, bool HasCount, ushort MaxCount, bool HasUses);
public record EditorWriteRequest(string Path, int Player, List<FieldEdit> Fields, List<ItemEdit> Items, List<RecipeEdit> Recipes);
