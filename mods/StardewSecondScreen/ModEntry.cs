using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Quests;
using StardewValley.Locations;
using StardewValley.Characters;
using StardewValley.Buildings;
using StardewValley.WorldMaps;
using StardewValley.Objects;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Microsoft.Xna.Framework.Graphics;

namespace StardewSecondScreen
{

    public sealed class ModEntry : Mod
    {
        private const int Port = 7786;

        private Bridge? _bridge;
        private Beacon? _beacon;

        private int _lastGold = -1;
        private int _lastInventoryHash;
        private int _lastSelected = -1;
        private int _lastWeaponCooldownBucket = -1;
        private int _lastMapX = -1;
        private int _lastMapY = -1;

        private string _lastMapOutcome = "";

        private int _mapWidth;
        private int _mapHeight;

        private bool _regionMeasured;
        private float _regionWidth;
        private float _regionHeight;

        [MemberNotNullWhen(true, nameof(_bridge))]
        private bool Live => Context.IsWorldReady && _bridge is { HasClients: true };

        private ModConfig _config = new();

        private readonly Sprites _sprites = new();
        private readonly CommandQueue _commands = new();

        private bool _hudHidden;

        private bool _portraitWanted;

        private bool _artSent;

        private string _groundSeason = "";

        private bool _portraitFailed;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<ModConfig>();

            _bridge = new Bridge(
                Port,
                message => Monitor.Log(message, LogLevel.Info),
                _config.AllowRemote);
            _bridge.MessageReceived += _commands.Accept;
            _bridge.Start();

            if (_config.AllowRemote)
            {
                _beacon = new Beacon(
                    Port,
                    Describe,
                    message => Monitor.Log(message, LogLevel.Info));
                _beacon.Start();
            }

            helper.Events.GameLoop.UpdateTicked += OnTick;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnSecond;
            helper.Events.Player.InventoryChanged += (_, _) =>
            {
                Attempt("inventory", SendInventory);
                Attempt("bundles", SendBundles);
                Attempt("crafting", SendCrafting);
                Attempt("cooking", SendCooking);
                Attempt("shipping", SendShipping);
                Attempt("collections", SendCollections);
                Attempt("gifts", SendGifts);
                Attempt("trees", SendTrees);
            };
            helper.Events.Player.Warped += (_, _) => SendDay();
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            helper.Events.Display.Rendered += OnRendered;
        }

        public override object? GetApi() => null;

        private string Describe() => Json.Message(
            "wemu_beacon",
            Json.Str("game", "stardew"),
            Json.Num("port", Port),
            Json.Str("save", Context.IsWorldReady ? Game1.player?.farmName?.Value ?? "" : ""));

        protected override void Dispose(bool disposing)
        {
            _beacon?.Dispose();
            _bridge?.Dispose();
            base.Dispose(disposing);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            Announce();
            SendEverything();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e) => SendEverything();

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            Attempt("day", SendDay);
            Attempt("machines", SendMachines);
            Attempt("animals", SendAnimals);
            Attempt("pet", SendPets);
            Attempt("mines", SendMines);
        }

        private void OnSecond(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || _bridge is not { HasClients: true }) return;

            if (Game1.player.Money != _lastGold)
            {
                _lastGold = Game1.player.Money;
                SendDay();
            }

            if (_config.SendMap)
            {
                var map = PlayerMapPixel();
                if (map.x != _lastMapX || map.y != _lastMapY) SendDay();
            }

            var hash = InventoryHash();
            if (hash != _lastInventoryHash)
            {
                _lastInventoryHash = hash;
                SendInventory();
            }
        }

        private void OnTick(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            var connected = _bridge is { HasClients: true };
            var hide = connected && _config.HideGameHud;
            if (hide != _hudHidden)
            {
                _hudHidden = hide;
                Game1.displayHUD = !hide;
                Monitor.Log(
                    hide
                        ? "The game's HUD is off the top screen"
                        : "The game's HUD is back",
                    LogLevel.Info);

                if (hide) {  }

                if (connected)
                {

                    _artSent = false;

                    Announce();
                    _sprites.Reset();
                    SendEverything();
                }
            }

            if (Game1.player.CurrentToolIndex != _lastSelected)
            {
                _lastSelected = Game1.player.CurrentToolIndex;
                SendInventory();
            }

            if (connected && e.IsMultipleOf(6))
            {
                var cooldown = WeaponCooldownBucket();
                if (cooldown != _lastWeaponCooldownBucket)
                {
                    _lastWeaponCooldownBucket = cooldown;
                    SendInventory();
                }
            }

            _commands.Drain(Apply);
        }

        private void Apply(Command command)
        {
            var player = Game1.player;
            switch (command.Type)
            {

                case "select_slot":
                    if (!_config.AllowInventoryEdits) break;
                    if (command.A >= 0 && command.A < 12) player.CurrentToolIndex = command.A;
                    break;

                case "move_item":
                    if (!_config.AllowInventoryEdits) break;
                    var from = command.A;
                    var to = command.B;
                    if (from < 0 || to < 0) break;
                    if (from >= player.Items.Count || to >= player.Items.Count) break;
                    (player.Items[from], player.Items[to]) = (player.Items[to], player.Items[from]);
                    SendInventory();
                    break;

                case "shift_toolbar":
                    if (!_config.AllowInventoryEdits) break;
                    player.shiftToolbar(command.A > 0);
                    _lastInventoryHash = InventoryHash();
                    SendInventory();
                    break;

                case "cancel_quest":
                    if (!_config.AllowQuestCancel) break;
                    var at = command.A;
                    if (at < 0 || at >= player.questLog.Count) break;
                    var quest = player.questLog[at];
                    if (quest == null || !quest.canBeCancelled.Value) break;
                    player.questLog.RemoveAt(at);
                    SendQuests();
                    break;

                case "set_option":
                    if (!ApplyOption(command.Key, command.A != 0)) break;
                    Helper.WriteConfig(_config);
                    SendEverything();
                    break;

                case "eat_slot":
                    if (!_config.AllowInventoryEdits) break;
                    var slot = command.A;
                    if (slot < 0 || slot >= player.Items.Count) break;
                    if (player.Items[slot] is not StardewValley.Object food) break;
                    if (food.Edibility <= InedibleThreshold) break;
                    Game1.player.eatObject(food);
                    food.Stack--;
                    if (food.Stack <= 0) player.Items[slot] = null;
                    SendInventory();
                    break;

                case "sort_bag":
                    if (!_config.AllowInventoryEdits) break;
                    ItemGrabMenu.organizeItemsInList(player.Items);
                    SendInventory();
                    break;

                case "refresh":
                    SendEverything();
                    break;
            }
        }

        private bool ApplyOption(string key, bool value)
        {
            switch (key)
            {
                case "hideGameHud": _config.HideGameHud = value; return true;
                case "allowInventoryEdits": _config.AllowInventoryEdits = value; return true;
                case "allowQuestCancel": _config.AllowQuestCancel = value; return true;
                case "farmerMarker": _config.FarmerMarker = value; return true;
                case "sendCrops": _config.SendCrops = value; return true;
                case "sendMachines": _config.SendMachines = value; return true;
                case "sendAnimals": _config.SendAnimals = value; return true;
                case "sendBundles": _config.SendBundles = value; return true;
                case "sendVillagers": _config.SendVillagers = value; return true;
                case "sendMap": _config.SendMap = value; return true;
                case "sendCrafting": _config.SendCrafting = value; return true;
                default:
                    Monitor.Log($"The console asked for an unknown setting \"{key}\".",
                                LogLevel.Trace);
                    return false;
            }
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            _lastGold = -1;
            _lastInventoryHash = 0;
            _lastSelected = -1;
            _lastMapX = -1;
            _lastMapY = -1;
            _lastMapOutcome = "";
            _regionMeasured = false;
            _portraitWanted = false;
            _artSent = false;
            _groundSeason = "";
            _sprites.Reset();
            if (_hudHidden)
            {
                _hudHidden = false;
                Game1.displayHUD = true;
            }
            _bridge?.Broadcast("game_info", Json.Message(
                "game_info", Json.Str("gameId", "stardew"), Json.Str("saveName", "")));
        }

        private void Announce()
        {
            _bridge?.Broadcast("game_info", Json.Message(
                "game_info",
                Json.Str("gameId", "stardew"),
                Json.Str("saveName", Game1.player?.farmName?.Value ?? "")));
        }

        private void SendEverything()
        {
            Attempt("config", SendConfig);
            Attempt("interface art", SendUi);
            Attempt("day", SendDay);
            Attempt("inventory", SendInventory);
            Attempt("quests", SendQuests);
            Attempt("villagers", SendVillagers);
            Attempt("crops", SendCrops);
            Attempt("skills", SendSkills);
            Attempt("machines", SendMachines);
            Attempt("bundles", SendBundles);
            Attempt("animals", SendAnimals);
            Attempt("gifts", SendGifts);
            Attempt("crafting", SendCrafting);
            Attempt("calendar", SendCalendar);
            Attempt("cart", SendCart);
            Attempt("orders", SendOrders);
            Attempt("cooking", SendCooking);
            Attempt("shipping", SendShipping);
            Attempt("trees", SendTrees);
            Attempt("pet", SendPets);
            Attempt("mines", SendMines);
            Attempt("collections", SendCollections);
        }

        private void Attempt(string what, Action send)
        {
            try
            {
                send();
            }
            catch (Exception failure)
            {
                Monitor.Log($"Could not send the {what}: {failure}", LogLevel.Error);
            }
        }

        private void SendSprite(string qualifiedId)
        {
            if (_bridge == null || string.IsNullOrEmpty(qualifiedId)) return;
            if (_sprites.AlreadySent(qualifiedId)) return;
            var png = _sprites.EncodeId(qualifiedId);
            if (png == null) return;
            _bridge.Send(Json.Message(
                "sdv_sprite",
                Json.Str("id", qualifiedId),
                Json.Str("png", Convert.ToBase64String(png))));
        }

        private void SendMachines()
        {
            if (!Live) return;
            if (!_config.SendMachines) { Clear("SendMachines"); return; }

            var machines = new List<string>();
            Utility.ForEachLocation(location =>
            {
                foreach (var obj in location.objects.Values.ToList())
                {
                    var held = obj?.heldObject.Value;
                    if (obj == null || held == null) continue;

                    if (obj is Chest || obj.QualifiedItemId == "(BC)165") continue;

                    SendSprite(obj.QualifiedItemId);
                    SendSprite(held.QualifiedItemId);

                    machines.Add(Json.Object(
                        Json.Str("id", obj.QualifiedItemId),
                        Json.Str("name", obj.DisplayName ?? obj.Name ?? ""),
                        Json.Str("outputId", held.QualifiedItemId),
                        Json.Str("output", held.DisplayName ?? held.Name ?? ""),
                        Json.Num("count", held.Stack),
                        Json.Str("location", LocationName(location)),
                        Json.Num("minutes", Math.Max(0, obj.MinutesUntilReady)),
                        Json.Flag("ready", obj.readyForHarvest.Value)));
                }
                return true;
            });

            _bridge.Broadcast("sdv_machines",
                Json.Message("sdv_machines", Json.Array("machines", machines)));
        }

        private void SendBundles()
        {
            if (!Live) return;
            if (!_config.SendBundles) { Clear("SendBundles"); return; }

            var bundles = new List<string>();
            try
            {
                if (Game1.getLocationFromName("CommunityCenter") is CommunityCenter centre)
                {

                    var carried = new HashSet<string>();
                    foreach (var item in Game1.player.Items)
                        if (item != null) carried.Add(item.QualifiedItemId);

                    foreach (var pair in Game1.netWorldState.Value.BundleData)
                    {
                        try
                        {
                            var key = pair.Key.Split('/');
                            if (key.Length < 2 || !int.TryParse(key[key.Length - 1], out var index))
                                continue;
                            if (!centre.bundles.ContainsKey(index)) continue;
                            var done = centre.bundles[index];

                            var fields = pair.Value.Split('/');
                            if (fields.Length < 3) continue;

                            var title = fields.Length > 6 && !string.IsNullOrWhiteSpace(fields[6])
                                ? fields[6]
                                : fields[0];

                            var parts = fields[2].Split(' ');
                            var missing = new List<string>();
                            var have = 0;
                            var slots = 0;

                            for (var i = 0; i + 2 < parts.Length; i += 3)
                            {
                                var slot = i / 3;
                                slots++;
                                if (slot < done.Length && done[slot]) { have++; continue; }

                                if (!int.TryParse(parts[i + 1], out var count)) count = 1;
                                int.TryParse(parts[i + 2], out var quality);

                                if (parts[i] == "-1")
                                {
                                    missing.Add(Json.Object(
                                        Json.Str("id", ""),
                                        Json.Str("name", count.ToString("N0") + "g"),
                                        Json.Num("count", 1),
                                        Json.Num("quality", 0),
                                        Json.Flag("carried", Game1.player.Money >= count)));
                                    continue;
                                }

                                var qid = ItemRegistry.QualifyItemId(parts[i]) ?? "(O)" + parts[i];
                                var data = ItemRegistry.GetData(qid);
                                if (data != null) SendSprite(qid);

                                missing.Add(Json.Object(
                                    Json.Str("id", qid),
                                    Json.Str("name", data?.DisplayName ?? parts[i]),
                                    Json.Num("count", count),
                                    Json.Num("quality", quality),
                                    Json.Flag("carried", carried.Contains(qid))));
                            }

                            var required = slots;
                            if (fields.Length > 4 && int.TryParse(fields[4], out var stated)
                                && stated > 0 && stated < slots)
                                required = stated;

                            if (have >= required || missing.Count == 0) continue;

                            bundles.Add(Json.Object(
                                Json.Str("room", RoomName(key[0])),
                                Json.Str("name", title),
                                Json.Num("have", have),
                                Json.Num("required", required),
                                Json.Array("missing", missing)));
                        }
                        catch
                        {

                        }
                    }
                }
            }
            catch
            {

            }

            _bridge.Broadcast("sdv_bundles",
                Json.Message("sdv_bundles", Json.Array("bundles", bundles)));
        }

        private void SendAnimals()
        {
            if (!Live) return;
            if (!_config.SendAnimals) { Clear("SendAnimals"); return; }

            var animals = new List<string>();
            try
            {
                foreach (var animal in Game1.getFarm().getAllFarmAnimals())
                {
                    if (animal == null) continue;

                    var produceId = animal.currentProduce.Value;
                    var produceQid = "";
                    var produceName = "";
                    // Stardew uses "-1" as the no-produce sentinel. Qualifying
                    // it creates an invalid object ID whose error sprite looks
                    // like weeds, making idle chickens appear to produce weeds.
                    if (!string.IsNullOrWhiteSpace(produceId) && produceId != "-1")
                    {
                        produceQid = ItemRegistry.QualifyItemId(produceId) ?? "(O)" + produceId;
                        var data = ItemRegistry.GetData(produceQid);
                        if (data != null)
                        {
                            produceName = data.DisplayName;
                            SendSprite(produceQid);
                        }
                        else
                        {
                            produceQid = "";
                        }
                    }

                    animals.Add(Json.Object(
                        Json.Str("name", animal.displayName ?? animal.Name ?? ""),
                        Json.Str("type", animal.displayType ?? animal.type.Value ?? ""),

                        Json.Num("hearts", animal.friendshipTowardFarmer.Value / 200),
                        Json.Flag("petted", animal.wasPet.Value),
                        Json.Flag("baby", animal.isBaby()),
                        Json.Str("produceId", produceQid),
                        Json.Str("produce", produceName)));
                }
            }
            catch
            {

            }

            _bridge.Broadcast("sdv_animals",
                Json.Message("sdv_animals", Json.Array("animals", animals)));
        }

        private void SendDay()
        {
            if (!Live) return;

            var map = _config.SendMap ? PlayerMapPixel() : (x: -1, y: -1);
            _lastMapX = map.x;
            _lastMapY = map.y;

            _bridge.Broadcast("sdv_day", Json.Message(
                "sdv_day",
                Json.Str("season", Capitalise(Game1.currentSeason)),
                Json.Num("day", Game1.dayOfMonth),
                Json.Num("year", Game1.year),
                Json.Str("weekday", WorldDate.GetDayOfWeekFor(Game1.dayOfMonth).ToString()),
                Json.Num("timeOfDay", Game1.timeOfDay),
                Json.Str("weatherToday", WeatherToday()),
                Json.Str("weatherTomorrow", WeatherTomorrow()),
                Json.Num("gold", Game1.player.Money),
                Json.Num("energy", (int)Game1.player.Stamina),
                Json.Num("maxEnergy", Game1.player.MaxStamina),
                Json.Num("health", Game1.player.health),
                Json.Num("maxHealth", Game1.player.maxHealth),
                Json.Str("location", PlaceName()),

                Json.Num("luck", (int)Math.Round(Game1.player.team.sharedDailyLuck.Value * 1000)),
                Json.Num("mapX", map.x),
                Json.Num("mapY", map.y)));
        }

        private static string PlaceName()
        {
            try
            {
                var position = WorldMapManager.GetPositionData(
                    Game1.currentLocation, Game1.player.TilePoint);
                var named = position?.GetScrollText();
                if (!string.IsNullOrWhiteSpace(named)) return named!;
            }
            catch
            {
            }
            return Game1.currentLocation?.Name ?? "";
        }

        private (int x, int y) PlayerMapPixel()
        {
            var where = Game1.currentLocation?.Name ?? "nowhere";
            try
            {
                var position = WorldMapManager.GetPositionData(
                    Game1.currentLocation, Game1.player.TilePoint);
                if (position == null)
                {
                    Explain($"no map area covers \"{where}\" at tile "
                            + $"{Game1.player.TilePoint.X},{Game1.player.TilePoint.Y}");
                    return (-1, -1);
                }

                var pixel = position.Value.GetMapPixelPosition();
                MeasureRegion(position.Value);

                var x = (int)Math.Round(pixel.X / _regionWidth * Thousandths);
                var y = (int)Math.Round(pixel.Y / _regionHeight * Thousandths);

                Explain($"on the map at {(int)pixel.X},{(int)pixel.Y} of "
                        + $"{(int)_regionWidth}x{(int)_regionHeight} from \"{where}\" "
                        + $"({x}/1000, {y}/1000)");
                return (x, y);
            }
            catch (Exception failure)
            {
                Explain($"the map lookup threw in \"{where}\": {failure.Message}");
                return (-1, -1);
            }
        }

        private void MeasureRegion(MapAreaPositionWithContext position)
        {
            if (_regionMeasured) return;
            _regionMeasured = true;

            _regionWidth = Math.Max(1, _mapWidth) * AssumedMapZoom;
            _regionHeight = Math.Max(1, _mapHeight) * AssumedMapZoom;
            var how = $"assumed {AssumedMapZoom}x the picture";

            try
            {
                var names = new List<string>();
                foreach (var member in position.GetType()
                             .GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    names.Add(member.Name);
                }
                Monitor.Log("Map position members: " + string.Join(", ", names), LogLevel.Trace);

                foreach (var found in RectanglesOn(position))
                {
                    if (found.Width <= 0 || found.Height <= 0) continue;

                    if (found.Width < _mapWidth || found.Height < _mapHeight) continue;
                    _regionWidth = found.Width;
                    _regionHeight = found.Height;
                    how = $"read from the game as {found.Width}x{found.Height}";
                    break;
                }
            }
            catch
            {

            }

            Monitor.Log(
                $"Map region is {(int)_regionWidth}x{(int)_regionHeight} ({how}); "
                + $"the picture is {_mapWidth}x{_mapHeight}.",
                LogLevel.Info);
        }

        private static IEnumerable<Microsoft.Xna.Framework.Rectangle> RectanglesOn(object root)
        {
            var seen = new List<object> { root };

            for (var depth = 0; depth < 2; depth++)
            {
                var next = new List<object>();
                foreach (var node in seen)
                {
                    if (node == null) continue;
                    foreach (var property in node.GetType()
                                 .GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (property.GetIndexParameters().Length > 0) continue;
                        object? value;
                        try { value = property.GetValue(node); } catch { continue; }
                        if (value is Microsoft.Xna.Framework.Rectangle rectangle
                            && (property.Name.Contains("Bounds")
                                || property.Name.Contains("Pixel")
                                || property.Name.Contains("Area")))
                        {
                            yield return rectangle;
                        }
                        else if (value != null && value.GetType().Namespace?.StartsWith("StardewValley") == true)
                        {
                            next.Add(value);
                        }
                    }

                    foreach (var field in node.GetType()
                                 .GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        object? value;
                        try { value = field.GetValue(node); } catch { continue; }
                        if (value is Microsoft.Xna.Framework.Rectangle rectangle
                            && (field.Name.Contains("Bounds")
                                || field.Name.Contains("Pixel")
                                || field.Name.Contains("Area")))
                        {
                            yield return rectangle;
                        }
                        else if (value != null && value.GetType().Namespace?.StartsWith("StardewValley") == true)
                        {
                            next.Add(value);
                        }
                    }
                }
                seen = next;
            }
        }

        private const int Thousandths = 1000;

        private const float AssumedMapZoom = 4f;

        private void Explain(string outcome)
        {
            var shape = outcome.Split(' ')[0] + (outcome.Contains("on the map") ? "+" : "-");
            if (shape == _lastMapOutcome) return;
            _lastMapOutcome = shape;
            Monitor.Log("Map position: " + outcome, LogLevel.Info);
        }

        private void SendInventory()
        {
            if (!Live) return;

            var player = Game1.player;
            var sent = 0;
            var failed = new List<string>();
            for (var slot = 0; slot < player.Items.Count; slot++)
            {
                var item = player.Items[slot];
                if (item == null || _sprites.AlreadySent(item.QualifiedItemId)) continue;
                var png = _sprites.Encode(item);
                if (png == null)
                {

                    failed.Add(item.QualifiedItemId);
                    continue;
                }
                sent++;
                _bridge.Send(Json.Message(
                    "sdv_sprite",
                    Json.Str("id", item.QualifiedItemId),
                    Json.Str("png", Convert.ToBase64String(png))));
            }
            if (sent > 0 || failed.Count > 0)
            {
                Monitor.Log(
                    $"Sprites: {sent} sent"
                    + (failed.Count > 0
                        ? $", {failed.Count} could not be read: {string.Join(", ", failed)}"
                        : ""),
                    failed.Count > 0 ? LogLevel.Warn : LogLevel.Debug);
            }

            var slots = new List<string>();
            for (var slot = 0; slot < player.Items.Count; slot++)
            {
                var item = player.Items[slot];
                if (item == null)
                {
                    slots.Add(Json.Object(Json.Num("slot", slot), Json.Str("id", "")));
                    continue;
                }
                var wateringCan = item as WateringCan;
                var weapon = item as MeleeWeapon;
                var cooldown = WeaponCooldown(weapon);
                slots.Add(Json.Object(
                    Json.Num("slot", slot),
                    Json.Str("id", item.QualifiedItemId),
                    Json.Str("name", item.DisplayName),
                    Json.Num("count", item.Stack),
                    Json.Num("quality", item.Quality),
                    Json.Num("water", wateringCan?.WaterLeft ?? -1),
                    Json.Num("waterMax", wateringCan?.waterCanMax ?? 0),
                    Json.Flag("bottomless", wateringCan?.IsBottomless ?? false),
                    Json.Num("cooldownMs", cooldown.remaining),
                    Json.Num("cooldownMax", cooldown.maximum),
                    Json.Str("category", item.getCategoryName())));
            }

            _bridge.Broadcast("sdv_inventory", Json.Message(
                "sdv_inventory",
                Json.Num("selected", player.CurrentToolIndex),
                Json.Num("capacity", player.MaxItems),
                Json.Array("items", slots)));

            _lastWeaponCooldownBucket = WeaponCooldownBucket();
        }

        private static (int remaining, int maximum) WeaponCooldown(MeleeWeapon? weapon)
        {
            if (weapon == null) return (-1, 0);
            return weapon.type.Value switch
            {
                MeleeWeapon.dagger => (Math.Max(0, MeleeWeapon.daggerCooldown),
                    MeleeWeapon.daggerCooldownTime),
                MeleeWeapon.club => (Math.Max(0, MeleeWeapon.clubCooldown),
                    MeleeWeapon.clubCooldownTime),
                MeleeWeapon.stabbingSword => (Math.Max(0, MeleeWeapon.attackSwordCooldown),
                    MeleeWeapon.defenseCooldownTime),
                _ => (Math.Max(0, MeleeWeapon.defenseCooldown),
                    MeleeWeapon.defenseCooldownTime),
            };
        }

        private static int WeaponCooldownBucket()
        {
            var remaining = Math.Max(
                Math.Max(MeleeWeapon.defenseCooldown, MeleeWeapon.attackSwordCooldown),
                Math.Max(MeleeWeapon.daggerCooldown, MeleeWeapon.clubCooldown));
            return remaining <= 0 ? 0 : (remaining + 99) / 100;
        }

        private void SendQuests()
        {
            if (!Live) return;

            var quests = new List<string>();
            for (var index = 0; index < Game1.player.questLog.Count; index++)
            {
                var quest = Game1.player.questLog[index];
                if (quest == null) continue;

                var objective = "";
                try
                {
                    quest.reloadObjective();
                    objective = StripMarkup(quest.currentObjective);
                }
                catch
                {

                }

                quests.Add(Json.Object(

                    Json.Num("index", index),
                    Json.Str("title", quest.questTitle),
                    Json.Str("detail", StripMarkup(quest.questDescription)),
                    Json.Str("objective", objective),

                    Json.Num("daysLeft", quest.daysLeft.Value > 0 ? quest.daysLeft.Value : -1),
                    Json.Num("reward", quest.moneyReward.Value),
                    Json.Flag("daily", quest.dailyQuest.Value),
                    Json.Flag("cancellable", quest.canBeCancelled.Value),
                    Json.Flag("complete", quest.completed.Value)));
            }

            _bridge.Broadcast("sdv_quests",
                Json.Message("sdv_quests", Json.Array("quests", quests)));
        }

        private void SendVillagers()
        {
            if (!Live) return;
            if (!_config.SendVillagers) { Clear("SendVillagers"); return; }

            var seasons = new[] { "spring", "summer", "fall", "winter" };
            var seasonNow = Array.IndexOf(seasons, Game1.currentSeason);
            var villagers = new List<string>();

            foreach (var name in Game1.player.friendshipData.Keys.ToList())
            {
                NPC? npc = Game1.getCharacterFromName(name);
                if (npc == null) continue;
                var friendship = Game1.player.friendshipData[name];

                var days = -1;
                var birthSeason = Array.IndexOf(seasons, npc.Birthday_Season ?? "");
                if (birthSeason >= 0 && npc.Birthday_Day > 0 && seasonNow >= 0)
                {

                    var today = seasonNow * 28 + Game1.dayOfMonth;
                    var birthday = birthSeason * 28 + npc.Birthday_Day;
                    days = birthday - today;
                    if (days < 0) days += 112;
                }

                if (days >= 0 && days <= PortraitDays) SendPortrait(npc);

                villagers.Add(Json.Object(
                    Json.Str("name", npc.displayName ?? name),
                    Json.Str("npc", npc.Name ?? name),
                    Json.Num("hearts", friendship.Points / 250),
                    Json.Num("maxHearts", friendship.IsMarried() ? 14 : 10),
                    Json.Str("birthdaySeason", Capitalise(npc.Birthday_Season ?? "")),
                    Json.Num("birthdayDay", npc.Birthday_Day),
                    Json.Num("birthdayIn", days),
                    Json.Num("giftsThisWeek", friendship.GiftsThisWeek),
                    Json.Flag("talkedToday", friendship.TalkedToToday)));
            }

            _bridge.Broadcast("sdv_villagers",
                Json.Message("sdv_villagers", Json.Array("villagers", villagers)));
        }

        private void SendCrops()
        {
            if (!Live) return;
            if (!_config.SendCrops) { Clear("SendCrops"); return; }

            var crops = new List<string>();
            foreach (var location in Game1.locations)
            {
                if (location == null) continue;
                foreach (var feature in location.terrainFeatures.Values.ToList())
                {
                    if (feature is not HoeDirt dirt || dirt.crop == null) continue;
                    var crop = dirt.crop;
                    if (crop.dead.Value) continue;

                    var ready = crop.currentPhase.Value >= crop.phaseDays.Count - 1
                                && (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value <= 0);

                    var remaining = 0;
                    for (var phase = crop.currentPhase.Value; phase < crop.phaseDays.Count - 1; phase++)
                        remaining += crop.phaseDays[phase];
                    remaining -= crop.dayOfCurrentPhase.Value;

                    crops.Add(Json.Object(
                        Json.Str("name", CropName(crop)),
                        Json.Str("location", location.Name),
                        Json.Num("daysLeft", Math.Max(0, remaining)),
                        Json.Flag("needsWater", dirt.state.Value == HoeDirt.dry),
                        Json.Flag("ready", ready)));
                }
            }

            _bridge.Broadcast("sdv_crops",
                Json.Message("sdv_crops", Json.Array("crops", crops)));
        }

        private void SendUi()
        {
            if (!Live) return;

            var seasonChanged = _groundSeason != Game1.currentSeason;
            if (_artSent && !seasonChanged)
            {
                RequestPortrait();
                return;
            }

            var menu = Game1.menuTexture;
            var cursors = Game1.mouseCursors;
            var missing = new List<string>();

            void Piece(string id, Texture2D? sheet, Microsoft.Xna.Framework.Rectangle rect,
                       int inset = 0, int insetY = -1)
            {
                var png = _sprites.EncodeRegion(sheet, rect);
                if (png == null) { missing.Add(id); return; }
                _bridge!.Send(Json.Message(
                    "sdv_sprite",
                    Json.Str("id", id),
                    Json.Num("inset", inset),
                    Json.Num("insetX", inset),
                    Json.Num("insetY", insetY < 0 ? inset : insetY),
                    Json.Str("png", Convert.ToBase64String(png))));
            }

            Piece("ui:panel", menu, new Microsoft.Xna.Framework.Rectangle(0, 256, 60, 60), 20);

            Piece("ui:slot", menu, Game1.getSourceRectForStandardTileSheet(menu, 10));
            Piece("ui:slot_selected", menu, Game1.getSourceRectForStandardTileSheet(menu, 56));
            Piece("ui:heart", cursors, new Microsoft.Xna.Framework.Rectangle(211, 428, 7, 6));
            Piece("ui:heart_empty", cursors, new Microsoft.Xna.Framework.Rectangle(218, 428, 7, 6));
            Piece("ui:coin", cursors, new Microsoft.Xna.Framework.Rectangle(280, 412, 20, 16));

            Piece("ui:arrow_left", cursors, new Microsoft.Xna.Framework.Rectangle(352, 495, 12, 11));
            Piece("ui:arrow_right", cursors, new Microsoft.Xna.Framework.Rectangle(365, 495, 12, 11));

            Piece("ui:scroll", cursors, new Microsoft.Xna.Framework.Rectangle(325, 318, 25, 18),
                  inset: 12, insetY: 0);

            Piece("ui:check_off", cursors, new Microsoft.Xna.Framework.Rectangle(227, 425, 9, 9));
            Piece("ui:check_on", cursors, new Microsoft.Xna.Framework.Rectangle(236, 425, 9, 9));
            Piece("ui:button", cursors, new Microsoft.Xna.Framework.Rectangle(432, 439, 9, 9),
                  inset: 3);
            Piece("ui:water_gauge", cursors,
                  new Microsoft.Xna.Framework.Rectangle(297, 420, 14, 5));

            if (!SendGround()) missing.Add("ui:ground");
            SendGroundChoices();
            if (_config.SendMap && !SendMap()) missing.Add("ui:map");
            RequestPortrait();

            _groundSeason = Game1.currentSeason;
            _artSent = true;

            if (missing.Count > 0)
            {
                Monitor.Log(
                    "Interface art this game version did not yield: " + string.Join(", ", missing)
                    + " — the console draws those in its own theme.",
                    LogLevel.Warn);
            }
        }

        private void RequestPortrait()
        {
            _portraitWanted = _config.SendMap && _config.FarmerMarker && !_portraitFailed;
        }

        private bool SendGround()
        {
            if (_bridge == null) return false;

            foreach (var name in new[]
                     {
                         $"Maps/{Game1.currentSeason}_outdoorsTileSheet",
                         "Maps/spring_outdoorsTileSheet",
                     })
            {
                try
                {
                    var sheet = Game1.content.Load<Texture2D>(name);
                    if (sheet == null || sheet.Width < Tile || sheet.Height < Tile) continue;

                    var rows = Math.Min(Tile * 3, sheet.Height);
                    var columns = sheet.Width;
                    var pixels = new Microsoft.Xna.Framework.Color[columns * rows];
                    sheet.GetData(
                        0,
                        new Microsoft.Xna.Framework.Rectangle(0, 0, columns, rows),
                        pixels, 0, pixels.Length);

                    var bestX = -1;
                    var bestY = 0;
                    var bestSpread = int.MaxValue;

                    for (var ty = 0; ty + Tile <= rows; ty += Tile)
                    {
                        for (var tx = 0; tx + Tile <= columns; tx += Tile)
                        {
                            if (!Ground(pixels, columns, tx, ty, out _, out var spread)) continue;
                            if (spread < bestSpread)
                            {
                                bestSpread = spread;
                                bestX = tx;
                                bestY = ty;
                            }
                        }
                    }

                    if (bestX < 0) continue;

                    var png = _sprites.EncodeRegion(
                        sheet, new Microsoft.Xna.Framework.Rectangle(bestX, bestY, Tile, Tile));
                    if (png == null) continue;

                    _bridge.Send(Json.Message(
                        "sdv_sprite",
                        Json.Str("id", "ui:ground"),
                        Json.Num("inset", 0),
                        Json.Str("png", Convert.ToBase64String(png))));
                    Monitor.Log(
                        $"Backdrop is the tile at {bestX},{bestY} of \"{name}\".", LogLevel.Debug);
                    return true;
                }
                catch
                {

                }
            }

            return false;
        }

        private void SendGroundChoices()
        {
            if (_bridge == null) return;

            var chosen = new List<(int Sum, string Sheet, int X, int Y)>();

            foreach (var name in new[]
                     {
                         "Maps/walls_and_floors",
                         "Maps/spring_outdoorsTileSheet",
                         "Maps/summer_outdoorsTileSheet",
                         "Maps/fall_outdoorsTileSheet",
                         "Maps/winter_outdoorsTileSheet",
                     })
            {
                try
                {
                    var sheet = Game1.content.Load<Texture2D>(name);
                    if (sheet == null) continue;

                    var rows = Math.Min(ScanHeight, sheet.Height);
                    var columns = Math.Min(ScanWidth, sheet.Width);
                    var pixels = new Microsoft.Xna.Framework.Color[columns * rows];
                    sheet.GetData(
                        0,
                        new Microsoft.Xna.Framework.Rectangle(0, 0, columns, rows),
                        pixels, 0, pixels.Length);

                    for (var ty = 0; ty + Tile <= rows; ty += Tile)
                    {
                        for (var tx = 0; tx + Tile <= columns; tx += Tile)
                        {
                            if (!Ground(pixels, columns, tx, ty, out var mean, out var spread))
                                continue;

                            if (spread > MaximumTexture) continue;

                            var tooClose = false;
                            foreach (var already in chosen)
                            {
                                if (Math.Abs(already.Sum - mean) < ColourGap) { tooClose = true; break; }
                            }
                            if (tooClose) continue;

                            chosen.Add((mean, name, tx, ty));
                            if (chosen.Count >= MaximumChoices) break;
                        }
                        if (chosen.Count >= MaximumChoices) break;
                    }
                }
                catch
                {

                }

                if (chosen.Count >= MaximumChoices) break;
            }

            var ids = new List<string>();
            for (var index = 0; index < chosen.Count; index++)
            {
                var pick = chosen[index];
                try
                {
                    var sheet = Game1.content.Load<Texture2D>(pick.Sheet);
                    var png = _sprites.EncodeRegion(
                        sheet,
                        new Microsoft.Xna.Framework.Rectangle(pick.X, pick.Y, Tile, Tile));
                    if (png == null) continue;

                    var id = GroundId(pick.Sheet, pick.X, pick.Y);
                    _bridge.Send(Json.Message(
                        "sdv_sprite",
                        Json.Str("id", id),
                        Json.Num("inset", 0),
                        Json.Str("png", Convert.ToBase64String(png))));
                    ids.Add(Json.Object(Json.Str("id", id)));
                }
                catch
                {

                }
            }

            _bridge.Broadcast("sdv_grounds",
                Json.Message("sdv_grounds", Json.Array("grounds", ids)));
            Monitor.Log($"Offered {ids.Count} backdrop tiles.", LogLevel.Debug);
        }

        private static string GroundId(string sheet, int x, int y)
        {
            var name = sheet.Replace("Maps/", "").Replace("_outdoorsTileSheet", "");
            return $"ui:ground:{name}:{x}:{y}";
        }

        private static bool Ground(
            Microsoft.Xna.Framework.Color[] pixels,
            int stride,
            int tx,
            int ty,
            out int mean,
            out int spread)
        {
            mean = 0;
            spread = 0;

            var total = 0;
            for (var y = 0; y < Tile; y++)
            {
                for (var x = 0; x < Tile; x++)
                {
                    var pixel = pixels[(ty + y) * stride + tx + x];
                    if (pixel.A < 255) return false;
                    total += pixel.R + pixel.G + pixel.B;
                }
            }

            mean = total / (Tile * Tile);
            for (var y = 0; y < Tile; y++)
            {
                for (var x = 0; x < Tile; x++)
                {
                    var pixel = pixels[(ty + y) * stride + tx + x];
                    spread += Math.Abs(pixel.R + pixel.G + pixel.B - mean);
                }
            }

            return spread >= MinimumTexture;
        }

        private const int ScanWidth = 256;
        private const int ScanHeight = 512;

        private const int MaximumTexture = 20000;

        private const int ColourGap = 22;

        private const int MaximumChoices = 28;

        private const int Tile = 16;

        private const int MinimumTexture = 256;

        private bool SendMap()
        {
            if (_bridge == null) return false;

            foreach (var name in MapAssetNames())
            {
                try
                {
                    var texture = Game1.content.Load<Texture2D>(name);
                    if (texture == null) continue;

                    var rect = new Microsoft.Xna.Framework.Rectangle(
                        0, 0,
                        Math.Min(300, texture.Width),
                        Math.Min(180, texture.Height));

                    var png = _sprites.EncodeRegion(texture, rect);
                    if (png == null)
                    {
                        Monitor.Log($"Map \"{name}\" loaded at {texture.Width}x{texture.Height} "
                                    + "but its pixels could not be read.", LogLevel.Warn);
                        continue;
                    }

                    _bridge.Send(Json.Message(
                        "sdv_sprite",
                        Json.Str("id", "ui:map"),
                        Json.Num("inset", 0),
                        Json.Str("png", Convert.ToBase64String(png))));

                    _mapWidth = rect.Width;
                    _mapHeight = rect.Height;
                    Monitor.Log($"Map sent from \"{name}\", {rect.Width}x{rect.Height}, "
                                + $"{png.Length / 1024}KB.", LogLevel.Info);
                    return true;
                }
                catch
                {

                }
            }

            return false;
        }

        private void OnRendered(object? sender, RenderedEventArgs e)
        {
            if (!_portraitWanted) return;
            _portraitWanted = false;
            if (!Live) return;

            if (!SendPlayerHead()) _portraitFailed = true;
        }

        private bool SendPlayerHead()
        {
            if (_bridge == null || Game1.player == null) return false;

            RenderTarget2D? target = null;
            SpriteBatch? batch = null;
            try
            {
                var device = Game1.graphics?.GraphicsDevice;
                if (device == null) return false;

                const int Size = 64;
                const float Scale = 4f;

                var previous = device.GetRenderTargets();
                target = new RenderTarget2D(device, Size, Size);
                device.SetRenderTarget(target);
                device.Clear(Microsoft.Xna.Framework.Color.Transparent);

                batch = new SpriteBatch(device);
                batch.Begin(

                    SpriteSortMode.Immediate,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    null,
                    null);
                Game1.player.FarmerRenderer.drawMiniPortrat(
                    batch,
                    Microsoft.Xna.Framework.Vector2.Zero,
                    0.0001f,
                    Scale,

                    2,
                    Game1.player);
                batch.End();

                if (previous != null && previous.Length > 0) device.SetRenderTargets(previous);
                else device.SetRenderTarget(null);

                var pixels = new Microsoft.Xna.Framework.Color[Size * Size];
                target.GetData(pixels);

                var visible = 0;
                foreach (var pixel in pixels) if (pixel.A > 8) visible++;
                if (visible < Size)
                {
                    Monitor.Log(
                        $"The farmer's portrait rendered {visible} visible pixels, which is "
                        + "not a face — the console will mark the map with its own dot instead.",
                        LogLevel.Warn);
                    return false;
                }

                using var cut = new Texture2D(device, Size, Size);
                cut.SetData(pixels);
                using var stream = new System.IO.MemoryStream();
                cut.SaveAsPng(stream, Size, Size);
                var png = stream.ToArray();

                _bridge.Send(Json.Message(
                    "sdv_sprite",
                    Json.Str("id", "ui:player"),
                    Json.Num("inset", 0),
                    Json.Str("png", Convert.ToBase64String(png))));
                Monitor.Log($"Farmer portrait sent, {visible} visible pixels, {png.Length} bytes.",
                            LogLevel.Info);
                return true;
            }
            catch (Exception failure)
            {
                Monitor.Log("Could not render the farmer's portrait: " + failure.Message,
                            LogLevel.Warn);
                return false;
            }
            finally
            {
                batch?.Dispose();
                target?.Dispose();
            }
        }

        private static IEnumerable<string> MapAssetNames()
        {
            yield return "LooseSprites/map";
            yield return "LooseSprites\\map";
        }

        private void Clear(string sender)
        {
            var kind = sender switch
            {
                "SendCrops" => "sdv_crops",
                "SendMachines" => "sdv_machines",
                "SendAnimals" => "sdv_animals",
                "SendBundles" => "sdv_bundles",
                "SendVillagers" => "sdv_villagers",
                "SendGifts" => "sdv_gifts",
                "SendCrafting" => "sdv_crafting",
                "SendCooking" => "sdv_cooking",
                "SendTrees" => "sdv_trees",
                _ => "",
            };
            var field = sender switch
            {
                "SendCrops" => "crops",
                "SendMachines" => "machines",
                "SendAnimals" => "animals",
                "SendBundles" => "bundles",
                "SendVillagers" => "villagers",
                "SendGifts" => "villagers",
                "SendCrafting" => "recipes",
                "SendCooking" => "recipes",
                "SendTrees" => "trees",
                _ => "",
            };
            if (kind.Length == 0) return;
            _bridge?.Broadcast(kind, Json.Message(kind, Json.Array(field, new List<string>())));
        }

        private void SendConfig()
        {
            _bridge?.Broadcast("sdv_config", Json.Message(
                "sdv_config",
                Json.Flag("hideGameHud", _config.HideGameHud),
                Json.Flag("allowInventoryEdits", _config.AllowInventoryEdits),
                Json.Flag("allowQuestCancel", _config.AllowQuestCancel),
                Json.Flag("farmerMarker", _config.FarmerMarker),
                Json.Flag("sendCrops", _config.SendCrops),
                Json.Flag("sendMachines", _config.SendMachines),
                Json.Flag("sendAnimals", _config.SendAnimals),
                Json.Flag("sendBundles", _config.SendBundles),
                Json.Flag("sendVillagers", _config.SendVillagers),
                Json.Flag("sendMap", _config.SendMap),
                Json.Flag("sendCrafting", _config.SendCrafting)));
        }

        private void SendSkills()
        {
            if (!Live) return;
            _bridge.Broadcast("sdv_skills", Json.Message(
                "sdv_skills",
                Json.Num("farming", Game1.player.FarmingLevel),
                Json.Num("mining", Game1.player.MiningLevel),
                Json.Num("foraging", Game1.player.ForagingLevel),
                Json.Num("fishing", Game1.player.FishingLevel),
                Json.Num("combat", Game1.player.CombatLevel),
                Json.Num("farmingNext", ToNextLevel(0)),
                Json.Num("miningNext", ToNextLevel(3)),
                Json.Num("foragingNext", ToNextLevel(2)),
                Json.Num("fishingNext", ToNextLevel(1)),
                Json.Num("combatNext", ToNextLevel(4))));
        }

        private static int ToNextLevel(int skill)
        {
            try
            {
                var earned = Game1.player.experiencePoints[skill];
                foreach (var threshold in LevelThresholds)
                {
                    if (earned < threshold) return threshold - earned;
                }
                return 0;
            }
            catch
            {
                return -1;
            }
        }

        private static readonly int[] LevelThresholds =
        {
            100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000,
        };

        private void SendGifts()
        {
            if (!Live) return;
            if (!_config.SendVillagers) { Clear("SendGifts"); return; }

            var carried = new HashSet<string>();
            foreach (var item in Game1.player.Items)
                if (item != null) carried.Add(item.QualifiedItemId);

            var entries = new List<string>();
            foreach (var name in Game1.player.friendshipData.Keys.ToList())
            {
                try
                {
                    if (!Game1.NPCGiftTastes.TryGetValue(name, out var tastes)) continue;
                    var fields = tastes.Split('/');
                    if (fields.Length < 2) continue;

                    var loved = new List<string>();
                    var have = false;
                    foreach (var raw in fields[1].Split(' '))
                    {
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        var qid = ItemRegistry.QualifyItemId(raw) ?? "(O)" + raw;
                        var data = ItemRegistry.GetData(qid);
                        if (data == null) continue;
                        if (carried.Contains(qid)) have = true;
                        SendSprite(qid);
                        loved.Add(Json.Object(
                            Json.Str("id", qid),
                            Json.Str("name", data.DisplayName),
                            Json.Flag("carried", carried.Contains(qid))));
                        if (loved.Count >= MaximumGifts) break;
                    }
                    if (loved.Count == 0) continue;

                    var npc = Game1.getCharacterFromName(name);
                    entries.Add(Json.Object(
                        Json.Str("name", npc?.displayName ?? name),
                        Json.Flag("carrying", have),
                        Json.Array("loves", loved)));
                }
                catch
                {
                }
            }

            _bridge.Broadcast("sdv_gifts",
                Json.Message("sdv_gifts", Json.Array("villagers", entries)));
        }

        private const int MaximumGifts = 8;

        private void SendCrafting()
        {
            if (!Live) return;
            if (!_config.SendCrafting) { Clear("SendCrafting"); return; }

            var ready = new List<string>();
            try
            {
                foreach (var known in Game1.player.craftingRecipes.Keys.ToList())
                {
                    try
                    {
                        var recipe = new CraftingRecipe(known, false);
                        if (!recipe.doesFarmerHaveIngredientsInInventory()) continue;

                        var made = recipe.getIndexOfMenuView();
                        var qid = ItemRegistry.QualifyItemId(made) ?? made;
                        if (qid != null) SendSprite(qid);

                        ready.Add(Json.Object(
                            Json.Str("id", qid ?? ""),
                            Json.Str("name", recipe.DisplayName)));
                        if (ready.Count >= MaximumRecipes) break;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            _bridge.Broadcast("sdv_crafting",
                Json.Message("sdv_crafting", Json.Array("recipes", ready)));
        }

        private const int MaximumRecipes = 24;

        private void SendCalendar()
        {
            if (!Live) return;

            var days = new List<string>();
            try
            {
                var season = Game1.currentSeason;
                var birthdays = new Dictionary<int, List<string>>();
                var ids = new Dictionary<int, string>();

                foreach (var name in Game1.player.friendshipData.Keys.ToList())
                {
                    var npc = Game1.getCharacterFromName(name);
                    if (npc == null) continue;
                    if (!string.Equals(npc.Birthday_Season, season, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (npc.Birthday_Day <= 0) continue;

                    if (!birthdays.TryGetValue(npc.Birthday_Day, out var onThatDay))
                    {
                        onThatDay = new List<string>();
                        birthdays[npc.Birthday_Day] = onThatDay;
                    }
                    onThatDay.Add(npc.displayName ?? name);
                    if (!ids.ContainsKey(npc.Birthday_Day)) ids[npc.Birthday_Day] = npc.Name ?? name;
                    SendPortrait(npc);
                }

                for (var day = 1; day <= DaysInSeason; day++)
                {
                    var festival = "";
                    try
                    {
                        if (Utility.isFestivalDay(day, Game1.season)) festival = FestivalName(day);
                    }
                    catch
                    {
                    }

                    birthdays.TryGetValue(day, out var names);
                    if (festival.Length == 0 && (names == null || names.Count == 0)) continue;

                    days.Add(Json.Object(
                        Json.Num("day", day),
                        Json.Str("festival", festival),
                        Json.Str("festivalWhen", festival.Length == 0 ? "" : FestivalWhen(day)),
                        Json.Str("festivalWhere", festival.Length == 0 ? "" : FestivalWhere(day)),
                        Json.Str("names", names == null ? "" : string.Join(", ", names)),
                        Json.Str("npc", ids.TryGetValue(day, out var who) ? who : "")));
                }
            }
            catch
            {
            }

            _bridge.Broadcast("sdv_calendar", Json.Message(
                "sdv_calendar",
                Json.Str("season", Capitalise(Game1.currentSeason)),
                Json.Num("today", Game1.dayOfMonth),
                Json.Array("days", days)));
        }

        private const int DaysInSeason = 28;

        private const int PortraitDays = 7;

        private const int InedibleThreshold = -300;

        private static string FestivalName(int day)
        {
            try
            {
                var key = Game1.currentSeason + day;
                if (Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + key)
                        is { } data && data.TryGetValue("name", out var name))
                {
                    return name;
                }
            }
            catch
            {
            }
            return "Festival";
        }

        private static string FestivalField(int day, int index)
        {
            try
            {
                var key = Game1.currentSeason + day;
                if (Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + key)
                        is { } data && data.TryGetValue("conditions", out var conditions))
                {
                    var parts = conditions.Split('/');
                    if (index < parts.Length) return parts[index];
                }
            }
            catch
            {
            }
            return "";
        }

        private static string FestivalWhere(int day) => FestivalField(day, 0);

        private static string FestivalWhen(int day)
        {
            var window = FestivalField(day, 1).Split(' ');
            if (window.Length < 2) return "";
            return Clock(window[0]) + " to " + Clock(window[1]);
        }

        private static string Clock(string raw)
        {
            if (!int.TryParse(raw, out var value)) return "";
            var hours = value / 100 % 24;
            var minutes = value % 100;
            var suffix = hours < 12 ? "am" : "pm";
            var shown = hours % 12 == 0 ? 12 : hours % 12;
            return minutes == 0 ? $"{shown}{suffix}" : $"{shown}:{minutes:00}{suffix}";
        }

        private void SendOrders()
        {
            if (!Live) return;

            var orders = new List<string>();
            try
            {
                foreach (var order in Game1.player.team.specialOrders)
                {
                    if (order == null) continue;
                    var objective = "";
                    try
                    {
                        foreach (var step in order.objectives)
                        {
                            var text = step?.GetDescription();
                            if (string.IsNullOrWhiteSpace(text)) continue;
                            objective = StripMarkup(text);
                            break;
                        }
                    }
                    catch
                    {
                    }

                    orders.Add(Json.Object(
                        Json.Str("title", StripMarkup(order.GetName())),
                        Json.Str("objective", objective),
                        Json.Num("daysLeft", order.GetDaysLeft())));
                }
            }
            catch
            {
            }

            _bridge.Broadcast("sdv_orders",
                Json.Message("sdv_orders", Json.Array("orders", orders)));
        }

        private void SendPortrait(NPC npc)
        {
            var id = "npc:" + (npc.Name ?? "");
            if (_bridge == null || _sprites.AlreadySent(id)) return;
            try
            {
                var portrait = npc.Portrait;
                if (portrait == null) return;
                var png = _sprites.EncodeRegion(
                    portrait, new Microsoft.Xna.Framework.Rectangle(0, 0, 64, 64));
                if (png == null) return;
                _sprites.Remember(id);
                _bridge.Send(Json.Message(
                    "sdv_sprite",
                    Json.Str("id", id),
                    Json.Num("inset", 0),
                    Json.Str("png", Convert.ToBase64String(png))));
            }
            catch
            {
            }
        }

        private void SendCart()
        {
            if (!Live) return;

            var open = false;
            try
            {
                if (Game1.getLocationFromName("Forest") is Forest forest)
                {
                    open = forest.travelingMerchantDay;
                }
            }
            catch
            {
            }

            _bridge.Broadcast("sdv_cart", Json.Message("sdv_cart", Json.Flag("open", open)));
        }

        private void SendCooking()
        {
            if (!Live) return;
            if (!_config.SendCrafting) { Clear("SendCooking"); return; }

            var ready = new List<string>();
            try
            {
                foreach (var known in Game1.player.cookingRecipes.Keys.ToList())
                {
                    try
                    {
                        var recipe = new CraftingRecipe(known, true);
                        if (!recipe.doesFarmerHaveIngredientsInInventory()) continue;

                        var made = recipe.getIndexOfMenuView();
                        var qid = ItemRegistry.QualifyItemId(made) ?? made;
                        if (qid != null) SendSprite(qid);

                        ready.Add(Json.Object(
                            Json.Str("id", qid ?? ""),
                            Json.Str("name", recipe.DisplayName)));
                        if (ready.Count >= MaximumRecipes) break;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            _bridge.Broadcast("sdv_cooking",
                Json.Message("sdv_cooking", Json.Array("recipes", ready)));
        }

        private void SendShipping()
        {
            if (!Live) return;

            var items = new List<string>();
            var total = 0;
            try
            {
                var bin = Game1.getFarm()?.getShippingBin(Game1.player);
                if (bin != null)
                {
                    foreach (var item in bin)
                    {
                        if (item == null) continue;
                        var worth = 0;
                        try
                        {
                            if (item is StardewValley.Object sold)
                                worth = sold.sellToStorePrice() * item.Stack;
                        }
                        catch
                        {
                        }
                        total += worth;
                        SendSprite(item.QualifiedItemId);
                        if (items.Count >= MaximumShipped) continue;
                        items.Add(Json.Object(
                            Json.Str("id", item.QualifiedItemId),
                            Json.Str("name", item.DisplayName),
                            Json.Num("count", item.Stack),
                            Json.Num("worth", worth)));
                    }
                }
            }
            catch
            {
            }

            _bridge.Broadcast("sdv_shipping", Json.Message(
                "sdv_shipping",
                Json.Num("total", total),
                Json.Array("items", items)));
        }

        private const int MaximumShipped = 12;

        private void SendTrees()
        {
            if (!Live) return;
            if (!_config.SendCrops) { Clear("SendTrees"); return; }

            var trees = new List<string>();
            try
            {
                Utility.ForEachLocation(location =>
                {
                    foreach (var feature in location.terrainFeatures.Values.ToList())
                    {
                        if (feature is not FruitTree tree) continue;
                        var count = 0;
                        try
                        {
                            count = tree.fruit.Count;
                        }
                        catch
                        {
                        }
                        if (count <= 0) continue;

                        var qid = "";
                        try
                        {
                            var first = tree.fruit[0];
                            if (first != null)
                            {
                                qid = first.QualifiedItemId;
                                SendSprite(qid);
                            }
                        }
                        catch
                        {
                        }

                        trees.Add(Json.Object(
                            Json.Str("id", qid),
                            Json.Str("location", LocationName(location)),
                            Json.Num("count", count)));
                    }
                    return true;
                });
            }
            catch
            {
            }

            _bridge.Broadcast("sdv_trees",
                Json.Message("sdv_trees", Json.Array("trees", trees)));
        }

        private void SendPets()
        {
            if (!Live) return;

            var name = "";
            var petted = false;
            var bowl = false;
            try
            {
                var pet = Game1.player.getPet();
                if (pet != null)
                {
                    name = pet.displayName ?? pet.Name ?? "";
                    petted = pet.lastPetDay.ContainsKey(Game1.player.UniqueMultiplayerID)
                             && pet.lastPetDay[Game1.player.UniqueMultiplayerID]
                             == Game1.Date.TotalDays;
                }
                if (Game1.getFarm() is { } farm)
                {
                    foreach (var building in farm.buildings)
                    {
                        if (building is PetBowl dish && dish.watered.Value)
                        {
                            bowl = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
            }

            _bridge.Broadcast("sdv_pet", Json.Message(
                "sdv_pet",
                Json.Str("name", name),
                Json.Flag("petted", petted),
                Json.Flag("bowl", bowl)));
        }

        private void SendMines()
        {
            if (!Live) return;

            var deepest = 0;
            var skull = 0;
            try
            {
                deepest = Game1.player.deepestMineLevel;
                if (deepest > MineFloors) skull = deepest - MineFloors;
            }
            catch
            {
            }

            _bridge.Broadcast("sdv_mines", Json.Message(
                "sdv_mines",
                Json.Num("deepest", Math.Min(deepest, MineFloors)),
                Json.Num("skull", skull)));
        }

        private const int MineFloors = 120;

        private void SendCollections()
        {
            if (!Live) return;

            var donated = 0;
            var carried = new List<string>();
            try
            {
                if (Game1.getLocationFromName("ArchaeologyHouse") is LibraryMuseum museum)
                {
                    donated = museum.museumPieces.Count();
                    if (_lastDonated != donated)
                    {
                        _lastDonated = donated;
                        Monitor.Log($"Museum: {donated} pieces donated.", LogLevel.Debug);
                    }
                    foreach (var item in Game1.player.Items)
                    {
                        if (item == null) continue;
                        if (!museum.isItemSuitableForDonation(item)) continue;
                        SendSprite(item.QualifiedItemId);
                        carried.Add(Json.Object(
                            Json.Str("id", item.QualifiedItemId),
                            Json.Str("name", item.DisplayName)));
                    }
                }
            }
            catch (Exception failure)
            {
                Monitor.Log("Could not read the museum: " + failure.Message, LogLevel.Warn);
            }

            _bridge.Broadcast("sdv_collections", Json.Message(
                "sdv_collections",
                Json.Num("donated", donated),
                Json.Num("total", MuseumPieces),
                Json.Array("carried", carried)));
        }

        private const int MuseumPieces = 95;

        private int _lastDonated = -1;

        private static string WeatherToday()
        {
            if (Game1.isLightning) return "Storm";
            if (Game1.isSnowing) return "Snow";
            if (Game1.isRaining) return "Rain";
            if (Game1.isDebrisWeather) return "Windy";
            return "Sunny";
        }

        private static string WeatherTomorrow()
        {
            try
            {

                var type = typeof(Game1);
                var raw = (type.GetProperty("weatherForTomorrow")?.GetValue(null)
                           ?? type.GetField("weatherForTomorrow")?.GetValue(null))
                    ?.ToString();
                if (string.IsNullOrEmpty(raw)) return "";
                return raw switch
                {
                    "Rain" => "Rain",
                    "Storm" => "Storm",
                    "Snow" => "Snow",
                    "Wind" => "Windy",
                    "Festival" => "Festival",
                    "Sun" => "Sunny",
                    _ => Capitalise(raw!),
                };
            }
            catch
            {
                return "";
            }
        }

        private static string LocationName(GameLocation location)
        {
            try
            {
                var shown = location.DisplayName;
                if (!string.IsNullOrWhiteSpace(shown)) return shown;
            }
            catch
            {

            }
            return location.Name ?? "";
        }

        private static string RoomName(string key) => key switch
        {
            "Pantry" => "Pantry",
            "Crafts Room" => "Crafts Room",
            "Fish Tank" => "Fish Tank",
            "Boiler Room" => "Boiler Room",
            "Vault" => "Vault",
            "Bulletin Board" => "Bulletin Board",
            "Abandoned Joja Mart" => "Joja Mart",
            _ => key,
        };

        private static string CropName(Crop crop)
        {
            try
            {
                var harvest = ItemRegistry.Create($"(O){crop.indexOfHarvest.Value}");
                return harvest?.DisplayName ?? "Crop";
            }
            catch
            {
                return "Crop";
            }
        }

        private int InventoryHash()
        {
            var hash = 17;
            foreach (var item in Game1.player.Items)
            {
                if (item == null) { hash = hash * 31; continue; }
                hash = hash * 31 + item.QualifiedItemId.GetHashCode();
                hash = hash * 31 + item.Stack;
                if (item is WateringCan wateringCan)
                {
                    hash = hash * 31 + wateringCan.WaterLeft;
                    hash = hash * 31 + wateringCan.waterCanMax;
                    hash = hash * 31 + (wateringCan.IsBottomless ? 1 : 0);
                }
            }
            return hash;
        }

        private static string StripMarkup(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var cleaned = text!.Replace("^", " ").Trim();
            return cleaned.Length > 240 ? cleaned.Substring(0, 240) : cleaned;
        }

        private static string Capitalise(string value) =>
            string.IsNullOrEmpty(value)
                ? ""
                : char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
}
