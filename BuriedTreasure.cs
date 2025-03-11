using Rust;
using System;
using Facepunch;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using System.Collections;
using System.Collections.Generic;
   // https://modhub.to
  // This is a user submitted Rust Plugin checked and verified by ModHub
 // ModHub is the largest Server Owner Trading Platform Online
// ModHub Official Email: nulled-rust@protonmail.com
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⠿⠿⠿⠿⠿⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⡿⠛⠉⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠛⢿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠙⢿⣿⣿⣿⣿
//⣿⣿⣿⡿⠁⠀⠀⠀⠀⣠⣴⣶⣿⡛⢻⣿⣿⠻⣶⣤⣀⠀⠀⠀⠀⠀⢙⣿⣿⣿
//⣿⣿⣿⠃⠀⠀⠀⣠⣾⣿⣤⠈⢿⣇⣘⣿⣇⣰⣿⣿⣿⣷⣄⢀⣠⣴⣿⣿⣿
//⣿⣿⣿⠀⠀⠀⣼⣿⣿⣿⣿⣷⠾⠛⠋⠉⠉⠛⠛⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡀⠀⢸⣿⣿⣿⣿⣿⠃⠀⠀⠀⠀⠀⠀⠀⣀⣴⠿⠋⢿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣇⠀⠘⣿⣿⣿⣿⠇⠀⠀⣀⣀⣠⣤⠶⠟⠉⠀⠀⠀⠘⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⡄⠀⠹⣿⣿⣿⡀⠀⢸⡏⠉⠁⠀⢠⣄⡀⠀⠀⠀⠀⢿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣄⠀⠙⣿⣿⣇⠀⢸⣇⠀⠀⠀⢿⣿⣿⣿⣶⣶⡄⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣧⠀⢸⣿⣿⡄⠈⢿⡄⠀⠀⠀⠉⠛⠻⢿⣿⣇⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⡿⢀⣾⣿⣿⡇⠀⣨⣿⣶⣄⠀⠀⠀⠀⠀⠸⣿⣸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⢡⣾⣿⣿⡿⠁⠀⣿⣿⣿⣿⣇⠀⠀⠀⠀⠀⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡿⣋⣴⣿⣿⣿⣏⣤⣴⣾⣿⣿⣿⣿⣿⣷⣦⣄⡀⠀⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿https://modhub.to⣿⣿⣿⣿⣷⣿⣿
namespace Oxide.Plugins
{
    [Info("BuriedTreasure", "mods", "1.0.22")]
    [Description("This plugin was fixed by rust mods to order [Rust Plugin]: ")]
    class BuriedTreasure : RustPlugin
    {
        // Fix for basic Treasure maps using Uncommon loot tables
        // Fix for Custom Gold Skin ID not working for custom ID's
        // Fix for Marking maps when close to water. should work much better to find another random spot
        // Fix for Treasure box not accepting some items on Loot table, now accepts ALL items
        // Added Options to Check for Close Tool Cupboards for treasure location, with config option to enable check and radius to check.

        #region Load

        [PluginReference] Plugin ServerRewards;
        [PluginReference] Plugin Economics;

        private List<LootContainer> lootAddedList = new List<LootContainer>();

        private void Loaded()
        {
            LoadConfig();
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("buriedtreasure.admin", this);
        }

        #endregion

        #region Configuration

        private static PluginConfig config;

        private class PluginConfig
        {
            public GlobalSettings globalSettings { get; set; }
            public ChanceSettings chanceSettings { get; set; }
            public LootTableSettings lootTableSettings { get; set; }

            public class GlobalSettings
            {
                [JsonProperty(PropertyName = "Gold - Enable gold to be sold for Server Reward Points ? ")] public bool UseServerRewards { get; set; }
                [JsonProperty(PropertyName = "Gold - Enable gold to be sold for Economics Bucks ? ")] public bool UseEconomics { get; set; }
                [JsonProperty(PropertyName = "Gold - Player will get this many Server Reward Points when selling 1 gold : ")] public int ServerRewardsGoldExhcange { get; set; }
                [JsonProperty(PropertyName = "Gold - Player will get this many Economics Bucks when selling 1 gold : ")] public int EconomicsGoldExchange { get; set; }
                [JsonProperty(PropertyName = "Gold - Skin ID for Gold Coin Inventory Item : (default is 1376561963, custom gold coins) ")] public ulong goldSkinID { get; set; }
                [JsonProperty(PropertyName = "Gold - Item ID for Gold Coin Inventory Item : (default note) ")] public int goldItemID { get; set; }
                [JsonProperty(PropertyName = "Gold - Name Shown for Item (default is Gold) : ")] public string goldNameText { get; set; }
                [JsonProperty(PropertyName = "Gold - Text Shown for Item : ")] public string goldDescriptionText { get; set; }

                [JsonProperty(PropertyName = "AutoLoot - Automatically turn in gold coins for rewards when looting ? ")] public bool EnableAutoGoldRewardOnLoot { get; set; }
                [JsonProperty(PropertyName = "AutoLoot - Automatically mark treasure maps when they are looted ? ")] public bool EnableAutoReadMapOnLoot { get; set; }

                [JsonProperty(PropertyName = "Treasure - Spawn - Only spawn Treasure up to this far from players current postion : ")] public float LocalTreasureMaxDistance { get; set; }
                [JsonProperty(PropertyName = "Treasure - Spawn - Use whole map (instead of distance from player) to get random spawn point ? ")] public bool UseWholeMapSpawn { get; set; }
                [JsonProperty(PropertyName = "Treasure - Spawn - When whole map size is used, reduce spawn area by this much offset (closer to land) : ")] public float WholeMapOffset { get; set; }
                [JsonProperty(PropertyName = "Treasure - Despawn - Approx Seconds the Treasure Marker and Location will despawn if not found : ")] public float DespawnTime { get; set; }
                [JsonProperty(PropertyName = "Treasure - Despawn - Approx Seconds the Spawned Chest will despawn if not looted : ")] public float TreasureDespawnTime { get; set; }
                [JsonProperty(PropertyName = "Treasure - Location - When player gets within this distance, treasure will spawn nearby : ")] public float LootDetectionRadius { get; set; }
                [JsonProperty(PropertyName = "Treasure - Location - Allow treasure to spawn underwater ? ")] public bool SpawnUnderWater { get; set; }
                [JsonProperty(PropertyName = "Treasure - Location - Allow Treasure to spawn near Tool Cupboards ? ")] public bool SpawnNearTC { get; set; }
                [JsonProperty(PropertyName = "Treasure - Location - If Not Allowed, how far from TC's should treasure spawn : ")] public float RadiusTCCheck { get; set; }

                [JsonProperty(PropertyName = "Map Marker - Prefab - Treasure Chest Map marker prefab (default Hack Crate Marker) : ")] public string MapMarkerPrefab { get; set; }
                [JsonProperty(PropertyName = "Map Marker - Enable Color Circle Markers on Map as well as default ? ")] public bool EnableColorMarkers { get; set; }
                [JsonProperty(PropertyName = "Map Marker - Allow Teams Members to see each others Buried Treasure Map Markers ? ")] public bool AllowTeamVisibility { get; set; }

                [JsonProperty(PropertyName = "Treasure - Prefab - Basic Treasure Chest prefab : ")] public string BasicTreasurePrefab { get; set; }
                [JsonProperty(PropertyName = "Treasure - Prefab - UnCommon Treasure Chest prefab : ")] public string UnCommonTreasurePrefab { get; set; }
                [JsonProperty(PropertyName = "Treasure - Prefab - Rare Treasure Chest prefab : ")] public string RareTreasurePrefab { get; set; }
                [JsonProperty(PropertyName = "Treasure - Prefab - Elite Treasure Chest prefab : ")] public string EliteTreasurePrefab { get; set; }

                [JsonProperty(PropertyName = "Text - Basic Map name when inspecting map in inventory")] public string BasicMapTitle { get; set; }
                [JsonProperty(PropertyName = "Text - Uncommon Map name when inspecting map in inventory")] public string UncommonMapTitle { get; set; }
                [JsonProperty(PropertyName = "Text - Rare Map name when inspecting map in inventory")] public string RareMapTitle { get; set; }
                [JsonProperty(PropertyName = "Text - Elite Map name when inspecting map in inventory")] public string EliteMapTitle { get; set; }
                [JsonProperty(PropertyName = "Text - Info on Maps and Gold on how to use : ")] public string MapInfomation { get; set; }

                [JsonProperty(PropertyName = "Effect - Enable Effects when Buried Treasure if found ? ")] public bool EnableFX { get; set; }
                [JsonProperty(PropertyName = "Effect - When Basic Treasure Chest is found, play with Effect : ")] public string BasicFXPrefab { get; set; }
                [JsonProperty(PropertyName = "Effect - When UnCommon Treasure Chest is found, play with Effect : ")] public string UnCommonFXPrefab { get; set; }
                [JsonProperty(PropertyName = "Effect - When Rare Treasure Chest is found, play with Effect : ")] public string RareFXPrefab { get; set; }
                [JsonProperty(PropertyName = "Effect - When Elite Treasure Chest is found, play with Effect : ")] public string EliteFXPrefab { get; set; }
            }

            public class ChanceSettings
            {
                [JsonProperty(PropertyName = "Standard Loot - Enable chance for random Treasure Map in standard loot crates ? ")] public bool StandardLlootMapEnabled { get; set; }
                [JsonProperty(PropertyName = "Standard Loot - Chance - To spawn random Treasure Map in standard Loot (if Enabled) : ")] public int StandardLootAddMapChance { get; set; }
                [JsonProperty(PropertyName = "Standard Loot - Enable chance for Gold to spawn in standard loot crates ? ")] public bool StandardLootGoldEnabled { get; set; }
                [JsonProperty(PropertyName = "Standard Loot - Chance - To spawn Gold in standard Loot (if Enabled) : ")] public int StandardLootAddGoldChance { get; set; }

                [JsonProperty(PropertyName = "Treasure - Chance - To spawn Treasure Map in Buried Treasure crates : ")] public int AddMapChance { get; set; }
                [JsonProperty(PropertyName = "Treasure - Chance - To spawn Gold in Buried Treasure crates : ")] public int AddGoldChance { get; set; }
                [JsonProperty(PropertyName = "Treasure - Chance - When a random map is added to chest or spawned, chance it will be a Basic Map: ")] public int BasicMapChance { get; set; }
                [JsonProperty(PropertyName = "Treasure - Chance - When a random map is added to chest or spawned, chance it will be a UnCommon Map: ")] public int UnCommonMapChance { get; set; }
                [JsonProperty(PropertyName = "Treasure - Chance - When a random map is added to chest or spawned, chance it will be a Rare Map: ")] public int RareMapChance { get; set; }
                [JsonProperty(PropertyName = "Treasure - Chance - When a random map is added to chest or spawned, chance it will be a Elite Map: ")] public int EliteMapChance { get; set; }
            }

            public class LootTableSettings
            {
                [JsonProperty(PropertyName = "Loot Table - Delete Standard Loot and only add items from Loot Tables ? ")] public bool UseOnlyLootTable { get; set; }
                [JsonProperty(PropertyName = "Loot Table - Basic Treasure Chest (Shortname, Chance, MinQty, MaxQty, SkinID) ")] public List<LootTable> BasicLootTable { get; set; }
                [JsonProperty(PropertyName = "Loot Table - UnCommon Treasure Chest (Shortname, Chance, MinQty, MaxQty, SkinID) ")] public List<LootTable> UnCommonLootTable { get; set; }
                [JsonProperty(PropertyName = "Loot Table - Rare Treasure Chest (Shortname, Chance, MinQty, MaxQty, SkinID) ")] public List<LootTable> RareLootTable { get; set; }
                [JsonProperty(PropertyName = "Loot Table - Elite Treasure Chest (Shortname, Chance, MinQty, MaxQty, SkinID) ")] public List<LootTable> EliteLootTable { get; set; }
            }

            public static PluginConfig DefaultConfig() => new PluginConfig()
            {
                globalSettings = new PluginConfig.GlobalSettings
                {
                    UseServerRewards = true,
                    UseEconomics = true,
                    ServerRewardsGoldExhcange = 100,
                    EconomicsGoldExchange = 100,
                    goldSkinID = 1376561963,
                    goldItemID = 1414245162,
                    goldNameText = "Gold",
                    goldDescriptionText = "Place in Quick Slot, then right click on it with mouse cursor to sell gold (if available)",

                    EnableAutoGoldRewardOnLoot = false,
                    EnableAutoReadMapOnLoot = false,
                    LocalTreasureMaxDistance = 100,
                    UseWholeMapSpawn = false,
                    SpawnUnderWater = false,
                    SpawnNearTC = false,
                    RadiusTCCheck = 25f,
                    WholeMapOffset = 500f,
                    DespawnTime = 3600f,
                    TreasureDespawnTime = 3600f,
                    LootDetectionRadius = 8f,
                    MapMarkerPrefab = "assets/prefabs/tools/map/cratemarker.prefab",
                    EnableColorMarkers = true,
                    AllowTeamVisibility = true,

                    BasicTreasurePrefab = "assets/bundled/prefabs/radtown/crate_basic.prefab",
                    UnCommonTreasurePrefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                    RareTreasurePrefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                    EliteTreasurePrefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",

                    BasicMapTitle = "Basic Map",
                    UncommonMapTitle = "Uncommon Map",
                    RareMapTitle = "Rare Map",
                    EliteMapTitle = "Elite Map",

                    EnableFX = true,
                    BasicFXPrefab = "assets/bundled/prefabs/fx/missing.prefab",
                    UnCommonFXPrefab = "assets/bundled/prefabs/fx/missing.prefab",
                    RareFXPrefab = "assets/bundled/prefabs/fx/missing.prefab",
                    EliteFXPrefab = "assets/bundled/prefabs/fx/missing.prefab",

                    MapInfomation = "Place in Quick Slot, then right click on it with mouse cursor to Mark Map or sell gold (if available)",
                },
                chanceSettings = new PluginConfig.ChanceSettings
                {
                    StandardLlootMapEnabled = true,
                    StandardLootAddMapChance = 10,
                    StandardLootGoldEnabled = true,
                    StandardLootAddGoldChance = 5,
                    AddMapChance = 25,
                    AddGoldChance = 15,
                    BasicMapChance = 50,
                    UnCommonMapChance = 30,
                    RareMapChance = 15,
                    EliteMapChance = 5,

                },
                lootTableSettings = new PluginConfig.LootTableSettings
                {
                    UseOnlyLootTable = false,
                    BasicLootTable = new List<LootTable>() { { new LootTable() { itemName = "Scraps", itemText = "A piece of scrap", itemChance = 50, itemMinAmount = 1, itemMaxAmount = 3, itemSkin = 0 } }, { new LootTable() { itemName = "seed.hemp", itemText = "Some Hemp Seeds", itemChance = 50, itemMinAmount = 2, itemMaxAmount = 6, itemSkin = 0 } } },
                    UnCommonLootTable = new List<LootTable>() { { new LootTable() { itemName = "Scraps", itemText = "A piece of scrap", itemChance = 50, itemMinAmount = 3, itemMaxAmount = 5, itemSkin = 0 } }, { new LootTable() { itemName = "seed.hemp", itemText = "Some Hemp Seeds", itemChance = 50, itemMinAmount = 6, itemMaxAmount = 15, itemSkin = 0 } } },
                    RareLootTable = new List<LootTable>() { { new LootTable() { itemName = "Scraps", itemText = "A piece of scrap", itemChance = 50, itemMinAmount = 5, itemMaxAmount = 10, itemSkin = 0 } }, { new LootTable() { itemName = "seed.hemp", itemText = "Some Hemp Seeds", itemChance = 50, itemMinAmount = 15, itemMaxAmount = 25, itemSkin = 0 } } },
                    EliteLootTable = new List<LootTable>() { { new LootTable() { itemName = "Scraps", itemText = "A piece of scrap", itemChance = 50, itemMinAmount = 10, itemMaxAmount = 30, itemSkin = 0 } }, { new LootTable() { itemName = "seed.hemp", itemText = "Some Hemp Seeds", itemChance = 50, itemMinAmount = 25, itemMaxAmount = 35, itemSkin = 0 } } }
                }
            };
        }

        public class LootTable
        {
            public string itemName;
            public string itemText;
            public int itemChance;
            public int itemMinAmount;
            public int itemMaxAmount;
            public ulong itemSkin;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created!!");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization

        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["nearland"] = "You are too close to water or a TC to Mark Treasure Map. Placed Map back in your inventory.",
            ["helptext1"] = "Treasure Map Commands : ",
            ["helptext2"] = "/markmap - marks location on ingame map.",
            ["helptext3"] = "or Right click mouse while holding map will mark location.",
            ["helptext4"] = "/sellgold - sell for RP or Economics Bucks.",
            ["helptext5"] = "Note : Map or gold must be in active held item slot.",
            ["notholdingmap"] = "You are not holding a Treasure Map !!",
            ["marked"] = "Treasure is now marked on ingame map at approx grid : ",
            ["treasureclose"] = "The Treasure is very close !!",
            ["notallowed"] = "You are not authorized to use that command !!"
        };
        #endregion

        #region Commands

        [ConsoleCommand("buymap")]
        private void cmdConsoleBuyMap(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, "buriedtreasure.admin"))
                {
                    SendReply(player, msg("notallowed", player.UserIDString));
                    return;
                }
                GiveTreasureMap(player);
                return;
            }
            if (arg.Args.Length > 0)
            {
                ulong id = Convert.ToUInt64(arg.Args[0]);
                GiveTreasureMap(BasePlayer.FindByID(id));
            }
        }

        [ConsoleCommand("buyuncommonmap")]
        private void cmdConsoleBuyUnCommonMap(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, "buriedtreasure.admin"))
                {
                    SendReply(player, msg("notallowed", player.UserIDString));
                    return;
                }
                GiveUnCommonTreasureMap(player);
                return;
            }
            if (arg.Args.Length > 0)
            {
                ulong id = Convert.ToUInt64(arg.Args[0]);
                GiveUnCommonTreasureMap(BasePlayer.FindByID(id));
            }
        }

        [ConsoleCommand("buyraremap")]
        private void cmdConsoleBuyRareMap(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, "buriedtreasure.admin"))
                {
                    SendReply(player, msg("notallowed", player.UserIDString));
                    return;
                }
                GiveRareTreasureMap(player);
                return;
            }
            if (arg.Args.Length > 0)
            {
                ulong id = Convert.ToUInt64(arg.Args[0]);
                GiveRareTreasureMap(BasePlayer.FindByID(id));
            }
        }

        [ConsoleCommand("givegold")]
        private void cmdConsoleGiveGold(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, "buriedtreasure.admin"))
                {
                    SendReply(player, msg("notallowed", player.UserIDString));
                    return;
                }
                GiveGold(player);
                return;
            }
        }

        [ConsoleCommand("buyelitemap")]
        private void cmdConsoleBuyEliteMap(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, "buriedtreasure.admin"))
                {
                    SendReply(player, msg("notallowed", player.UserIDString));
                    return;
                }
                GiveEliteTreasureMap(player);
                return;
            }
            if (arg.Args.Length > 0)
            {
                ulong id = Convert.ToUInt64(arg.Args[0]);
                GiveEliteTreasureMap(BasePlayer.FindByID(id));
            }
        }

        [ConsoleCommand("buyrandommap")]
        private void cmdConsoleBuyRandomMap(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, "buriedtreasure.admin"))
                {
                    SendReply(player, msg("notallowed", player.UserIDString));
                    return;
                }
                GiveRandomTreasureMap(player);
                return;
            }
            if (arg.Args.Length > 0)
            {
                ulong id = Convert.ToUInt64(arg.Args[0]);
                GiveRandomTreasureMap(BasePlayer.FindByID(id));
            }
        }

        [ChatCommand("markmap")]
        private void cmdMarkMap(BasePlayer player, string command, string[] args)
        {
            if (player.GetActiveItem() == null || !IsHoldingMap(player, player.GetActiveItem()))
            {
                SendReply(player, msg("notholdingmap", player.UserIDString));
            }
        }

        [ChatCommand("treasurehelp")]
        private void cmdTreasureHelp(BasePlayer player, string command, string[] args)
        {
            SendReply(player, msg("helptext1", player.UserIDString) + "\n" + msg("helptext2", player.UserIDString) + "\n" + msg("helptext3", player.UserIDString) + "\n" + msg("helptext4", player.UserIDString) + "\n" + msg("helptext5", player.UserIDString));
        }

        [ChatCommand("sellgold")]
        private void cmdSellGold(BasePlayer player, string command, string[] args)
        {
            SellGold(player);
        }

        #endregion

        #region Hooks

        private bool IsHoldingMap(BasePlayer player, Item item)
        {
            if (item.skin == 0) return false;
            if (item.skin == 1389950043) return true;
            if (item.skin == 1390209788) return true;
            if (item.skin == 1390210901) return true;
            if (item.skin == 1390211736) return true;
            return false;
        }

        private bool IsHoldingGold(BasePlayer player, Item item)
        {
            if (item.skin == 0) return false;
            if (item.skin == config.globalSettings.goldSkinID) return true;
            return false;
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;

            if (input.WasJustPressed(BUTTON.FIRE_SECONDARY))
            {
                var activeItem = player.GetActiveItem();
                if (activeItem != null && activeItem.skin != 0)
                {
                    if (IsHoldingMap(player, activeItem)) { MarkMapProcess(player, activeItem); return; }
                    else if (IsHoldingGold(player, activeItem)) { SellGold(player, activeItem); return; }
                }
            }
        }

        private void CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot)
        {
            if (item == null || playerLoot == null || targetContainer == null || targetSlot == null) return;
            if (item.skin == 0) return;
            var thplayer = playerLoot.GetComponentInParent<BasePlayer>() as BasePlayer;
            if (thplayer == null) return;
            if (config.globalSettings.EnableAutoGoldRewardOnLoot && item.skin == config.globalSettings.goldSkinID) { SellGold(thplayer, item); return; }
            if (config.globalSettings.EnableAutoReadMapOnLoot && IsHoldingMap(thplayer, item)) { MarkMapProcess(thplayer, item); return; }

            if (targetSlot != -1) return;

            var container = playerLoot.FindContainer(targetContainer) ?? null;
            if (container == null || container != playerLoot.containerMain) return;

            if (IsHoldingMap(thplayer, item)) { MarkMapProcess(thplayer, item); return; }
            else if (IsHoldingGold(thplayer, item)) { SellGold(thplayer, item); return; }
        }

        private void MarkMapProcess(BasePlayer player, Item item)
        {
            var skinid = item.skin;
            if (skinid == 1389950043)
            {
                item.Remove(0f);
                ServerMgr.Instance.StartCoroutine(BuryTheTreasure(player, 1));
            }
            else if (skinid == 1390209788)
            {
                item.Remove(0f);
                ServerMgr.Instance.StartCoroutine(BuryTheTreasure(player, 2));
            }
            else if (skinid == 1390210901)
            {
                item.Remove(0f);
                ServerMgr.Instance.StartCoroutine(BuryTheTreasure(player, 3));
            }
            else if (skinid == 1390211736)
            {
                item.Remove(0f);
                ServerMgr.Instance.StartCoroutine(BuryTheTreasure(player, 4));
            }
        }

        private void GiveTreasureMap(BasePlayer player)
        {
            var item = ItemManager.CreateByItemID(1414245162, 1, 1389950043);
            item.name = config.globalSettings.BasicMapTitle;
            item.text = config.globalSettings.MapInfomation;
            player.inventory.GiveItem(item);
        }

        private void GiveUnCommonTreasureMap(BasePlayer player)
        {
            var item = ItemManager.CreateByItemID(1414245162, 1, 1390209788);
            item.name = config.globalSettings.UncommonMapTitle;
            item.text = config.globalSettings.MapInfomation;
            player.inventory.GiveItem(item);
        }

        private void GiveRareTreasureMap(BasePlayer player)
        {
            var item = ItemManager.CreateByItemID(1414245162, 1, 1390210901);
            item.name = config.globalSettings.RareMapTitle;
            item.text = config.globalSettings.MapInfomation;
            player.inventory.GiveItem(item);
        }

        private void GiveEliteTreasureMap(BasePlayer player)
        {
            var item = ItemManager.CreateByItemID(1414245162, 1, 1390211736);
            item.name = config.globalSettings.EliteMapTitle;
            item.text = config.globalSettings.MapInfomation;
            player.inventory.GiveItem(item);
        }

        private void GiveRandomTreasureMap(BasePlayer player)
        {
            ulong skinid = 1389950043;
            string mapTitle = config.globalSettings.BasicMapTitle;
            var randomroll = UnityEngine.Random.Range(0, (config.chanceSettings.BasicMapChance + config.chanceSettings.UnCommonMapChance + config.chanceSettings.RareMapChance + config.chanceSettings.EliteMapChance));
            if (randomroll >= 0 && randomroll <= config.chanceSettings.BasicMapChance) { skinid = 1389950043; mapTitle = config.globalSettings.BasicMapTitle; }
            if (randomroll >= (config.chanceSettings.BasicMapChance + 1) && randomroll <= (config.chanceSettings.BasicMapChance + config.chanceSettings.UnCommonMapChance)) { skinid = 1390209788; mapTitle = config.globalSettings.UncommonMapTitle; }
            if (randomroll >= (config.chanceSettings.UnCommonMapChance + 1) && randomroll <= (config.chanceSettings.UnCommonMapChance + config.chanceSettings.RareMapChance)) { skinid = 1390210901; mapTitle = config.globalSettings.RareMapTitle; }
            if (randomroll >= (config.chanceSettings.RareMapChance + 1) && randomroll <= (config.chanceSettings.RareMapChance + config.chanceSettings.EliteMapChance)) { skinid = 1390211736; mapTitle = config.globalSettings.EliteMapTitle; }

            var item = ItemManager.CreateByItemID(1414245162, 1, skinid);
            player.inventory.GiveItem(item);
        }

        private void GiveContainerRandomTreasureMap(LootContainer container)
        {
            ulong skinid = 1389950043;
            string mapTitle = config.globalSettings.BasicMapTitle;
            var randomroll = UnityEngine.Random.Range(0, (config.chanceSettings.BasicMapChance + config.chanceSettings.UnCommonMapChance + config.chanceSettings.RareMapChance + config.chanceSettings.EliteMapChance));
            if (randomroll >= 0 && randomroll <= config.chanceSettings.BasicMapChance) { skinid = 1389950043; mapTitle = config.globalSettings.BasicMapTitle; }
            if (randomroll >= (config.chanceSettings.BasicMapChance + 1) && randomroll <= (config.chanceSettings.BasicMapChance + config.chanceSettings.UnCommonMapChance)) { skinid = 1390209788; mapTitle = config.globalSettings.UncommonMapTitle; }
            if (randomroll >= (config.chanceSettings.UnCommonMapChance + 1) && randomroll <= (config.chanceSettings.UnCommonMapChance + config.chanceSettings.RareMapChance)) { skinid = 1390210901; mapTitle = config.globalSettings.RareMapTitle; }
            if (randomroll >= (config.chanceSettings.RareMapChance + 1) && randomroll <= (config.chanceSettings.RareMapChance + config.chanceSettings.EliteMapChance)) { skinid = 1390211736; mapTitle = config.globalSettings.EliteMapTitle; }

            ItemContainer component1 = container.GetComponent<StorageContainer>().inventory;
            Item item = ItemManager.CreateByItemID(1414245162, 1, skinid);
            component1.itemList.Add(item);
            component1.capacity++;
            item.name = mapTitle;
            item.text = config.globalSettings.MapInfomation;
            item.parent = component1;
            item.MarkDirty();
        }

        private void GiveGold(BasePlayer player)
        {
            var item = ItemManager.CreateByItemID(config.globalSettings.goldItemID, 1, config.globalSettings.goldSkinID);
            item.name = config.globalSettings.goldNameText;
            item.text = config.globalSettings.goldDescriptionText;
            player.inventory.GiveItem(item);
        }

        private void GiveContainerGold(LootContainer container)
        {
            ItemContainer component1 = container.GetComponent<StorageContainer>().inventory;
            Item item = ItemManager.CreateByItemID(config.globalSettings.goldItemID, 1, config.globalSettings.goldSkinID);
            item.name = config.globalSettings.goldNameText;
            item.text = config.globalSettings.goldDescriptionText;
            component1.itemList.Add(item);
            component1.capacity++;
            item.parent = component1;
            item.MarkDirty();
        }

        private void SellGold(BasePlayer player, Item item = null)
        {
            Item activeItem = new Item();
            if (item != null) activeItem = item;
            else activeItem = player.GetActiveItem();

            if (activeItem != null)
            {
                if (activeItem.skin == config.globalSettings.goldSkinID)
                {
                    if (config.globalSettings.UseServerRewards && ServerRewards != null)
                    {
                        ServerRewards?.Call("AddPoints", new object[] { player.userID, config.globalSettings.ServerRewardsGoldExhcange });
                        SendReply(player, "You Just sold your gold for " + config.globalSettings.ServerRewardsGoldExhcange.ToString() + " Rewards Points !!!");
                        activeItem.Remove(0f);
                    }
                    else if (config.globalSettings.UseEconomics && Economics != null)
                    {
                        Economics?.Call("Deposit", new object[] { player.userID, Convert.ToDouble(config.globalSettings.EconomicsGoldExchange) });
                        SendReply(player, "You Just sold your gold for " + config.globalSettings.EconomicsGoldExchange.ToString() + " Economic Bucks !!!");
                        activeItem.Remove(0f);
                    }
                    SendReply(player, "You Just sold your gold.");
                    activeItem.Remove(0f);
                    return;
                }
            }
        }

        private float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity, UnityEngine.LayerMask.GetMask("World", "Construction", "Default")))
                return Mathf.Max(hit.point.y, y);

            return y;
        }

        private Vector3 GetSpawnLocation(BasePlayer player)
        {
            Vector3 targetPos = new Vector3();
            RaycastHit hitInfo;
            Vector3 randomizer = new Vector3(UnityEngine.Random.Range(-config.globalSettings.LocalTreasureMaxDistance, config.globalSettings.LocalTreasureMaxDistance), 0f, UnityEngine.Random.Range(-config.globalSettings.LocalTreasureMaxDistance, config.globalSettings.LocalTreasureMaxDistance));
            Vector3 newp = (player.transform.position + randomizer);
            var groundy = GetGroundPosition(newp);
            targetPos = new Vector3(newp.x, groundy, newp.z);
            return targetPos;
        }

        private Vector3 FindGlobalSpawnPoint()
        {
            Vector3 spawnpoint = new Vector3();
            float mapoffset = config.globalSettings.WholeMapOffset;
            float mapsize = ((ConVar.Server.worldsize) / 2) - mapoffset;
            Vector3 randomizer = new Vector3(UnityEngine.Random.Range(-mapsize, mapsize), 0f, UnityEngine.Random.Range(-mapsize, mapsize));
            Vector3 newp = randomizer;
            var groundy = GetGroundPosition(newp);
            spawnpoint = new Vector3(randomizer.x, groundy, randomizer.z);
            return spawnpoint;
        }

        private bool IsUnderWater(Vector3 position)
        {
            if (!config.globalSettings.SpawnUnderWater && position.y < 0.1f) return true;
            return false;
        }

        private bool IsNearTC(Vector3 position)
        {
            if (config.globalSettings.SpawnNearTC) return false;
            List<BuildingPrivlidge> privList = Pool.GetList<BuildingPrivlidge>();
            Vis.Entities<BuildingPrivlidge>(position, config.globalSettings.RadiusTCCheck, privList);

            foreach (BuildingPrivlidge foundTC in privList)
            {
                return true;
            }
            Pool.FreeList<BuildingPrivlidge>(ref privList);
            return false;
        }

        public IEnumerator BuryTheTreasure(BasePlayer player, int maprarity = 1)
        {
            Vector3 position = GetSpawnLocation(player);
            if (config.globalSettings.UseWholeMapSpawn) position = FindGlobalSpawnPoint();

            float counter = 0f;
            do
            {
                position = GetSpawnLocation(player);
                if (config.globalSettings.UseWholeMapSpawn) position = FindGlobalSpawnPoint();

                if (counter >= 10f)
                {
                    SendReply(player, msg("nearland", player.UserIDString));
                    if (maprarity == 1) GiveTreasureMap(player);
                    if (maprarity == 2) GiveUnCommonTreasureMap(player);
                    if (maprarity == 3) GiveRareTreasureMap(player);
                    if (maprarity == 4) GiveEliteTreasureMap(player);
                    counter = 0f;
                    yield break;
                }
                counter++;
                yield return new WaitForEndOfFrame();
            } while (IsUnderWater(position) || IsNearTC(position));

            yield return new WaitForEndOfFrame();

            GameObject newTreasure = new GameObject();
            newTreasure.transform.position = position;
            var stash = newTreasure.gameObject.AddComponent<BaseEntity>();
            stash.OwnerID = player.userID;
            var addmarker = stash.gameObject.AddComponent<TreasureMarker>();
            addmarker.rarity = maprarity;
            if (config.globalSettings.EnableColorMarkers) addmarker.AddColorMarker(maprarity);

            SendReply(player, msg("marked", player.UserIDString) + GetGridLocation(position));
            yield break;
        }

        private void OnLootSpawn(LootContainer container)
        {
            if (container == null || lootAddedList.Contains(container)) return;
            //Add Gold/Maps to Buried Treasure Chests
            if (container.skinID == 111)
            {
                int ranMapRoll = UnityEngine.Random.Range(0, 100);
                if (ranMapRoll <= config.chanceSettings.AddMapChance) GiveContainerRandomTreasureMap(container);

                int ranGoldRoll = UnityEngine.Random.Range(0, 100);
                if (ranGoldRoll <= config.chanceSettings.AddGoldChance) GiveContainerGold(container);
                lootAddedList.Add(container);
            }
            //Add Gold/Maps to standard loot chests
            else
            {
                if (config.chanceSettings.StandardLlootMapEnabled)
                {
                    int ranMapRollStandard = UnityEngine.Random.Range(0, 100);
                    if (ranMapRollStandard <= config.chanceSettings.StandardLootAddMapChance) GiveContainerRandomTreasureMap(container);
                }
                if (config.chanceSettings.StandardLootGoldEnabled)
                {
                    int ranGoldRollStandard = UnityEngine.Random.Range(0, 100);
                    if (ranGoldRollStandard <= config.chanceSettings.StandardLootAddGoldChance) GiveContainerGold(container);
                }
                lootAddedList.Add(container);
            }
        }

        private void OnEntityDeath(LootContainer container, HitInfo info)
        {
            if (lootAddedList.Contains(container)) lootAddedList.Remove(container);
        }

        private string GetGridLocation(Vector3 position)
        {
            // left / right modifier
            int xmodifier = 15 - ((int)World.Size / 1000) * 3;
            // up / down modifier
            int ymodifier = 131 - ((int)World.Size / 1000) * 19;
            Vector2 offsetPos = new Vector2((World.Size / 2 - xmodifier) + position.x, (World.Size / 2 - ymodifier) - position.z);
            string gridstring = $"{Convert.ToChar(65 + (int)offsetPos.x / 146)}{(int)(offsetPos.y / 146)}";
            return gridstring;
        }

        private void Unload()
        {
            DestroyAll<TreasureMarker>();
        }

        private static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        #endregion

        #region TreasureMarker 

        private object CanNetworkTo(MapMarker mapentity, BasePlayer target)
        {
            if (mapentity.name == "Treasure Marker")
            {
                if (target.userID == mapentity.OwnerID) return null;
                if (config.globalSettings.AllowTeamVisibility && target.Team != null)
                {
                    if (target.Team.members.Contains(mapentity.OwnerID)) return null;
                }
                return false;
            }
            return null;
        }

        private class TreasureMarker : BaseEntity
        {
            private BaseEntity lootbox;
            private BaseEntity treasurechest;
            private MapMarker mapmarker;
            private MapMarkerGenericRadius colorMarker;
            private SphereCollider sphereCollider;
            public ulong playerid;
            private BuriedTreasure instance;
            public int rarity;
            private string prefabtreasure;
            private List<LootTable> loottable;
            private bool isvisible;
            private bool didspawnchest;
            private float despawncounter;
            private float detectionradius;


            private void Awake()
            {
                instance = new BuriedTreasure();
                lootbox = GetComponentInParent<BaseEntity>();
                playerid = lootbox.OwnerID;
                rarity = 1;
                despawncounter = 0f;
                isvisible = false;
                didspawnchest = false;
                detectionradius = config.globalSettings.LootDetectionRadius;
                string prefabmarker = config.globalSettings.MapMarkerPrefab;

                mapmarker = GameManager.server.CreateEntity(prefabmarker, lootbox.transform.position, Quaternion.identity, true) as MapMarker;
                mapmarker.OwnerID = lootbox.OwnerID;
                mapmarker.name = "Treasure Marker";
                mapmarker.Spawn();

                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Layer.Reserved1;
                sphereCollider.isTrigger = true;
                sphereCollider.radius = detectionradius;
            }

            public void AddColorMarker(int rarity)
            {
                Color newColor = new Color(1.0f, 0.9f, 0.3f, 1.0f);
                if (rarity == 1) newColor = new Color(1.0f, 0.9f, 0.3f, 1.0f);
                if (rarity == 2) newColor = new Color(0.0f, 1.0f, 0.0f, 1.0f);
                if (rarity == 3) newColor = new Color(0.0f, 0.5f, 1.0f, 1.0f);
                if (rarity == 4) newColor = new Color(0.5f, 0.0f, 1.0f, 1.0f);
                float markerRadius = 0.1f;
                var worldSize = (int)World.Size;
                if (worldSize <= 1500) markerRadius = 0.4f;
                else if (worldSize <= 3000) markerRadius = 0.2f;
                colorMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", lootbox.transform.position) as MapMarkerGenericRadius;
                colorMarker.alpha = 0.5f;
                colorMarker.color1 = newColor;
                colorMarker.name = "Treasure Marker";
                colorMarker.radius = markerRadius;
                colorMarker.OwnerID = lootbox.OwnerID;
                colorMarker.Spawn();
                colorMarker.SendUpdate();
            }

            private void OnTriggerEnter(Collider col)
            {
                if (didspawnchest) return;
                var target = col.GetComponentInParent<BasePlayer>();
                if (target != null)
                {
                    if (target.userID == lootbox.OwnerID)
                    {
                        didspawnchest = true;
                        SpawnTreasureChest();
                        instance.SendReply(target, instance.msg("treasureclose", target.UserIDString));
                    }
                }
            }

            private void SpawnTreasureChest()
            {
                string effectPrefab = config.globalSettings.BasicFXPrefab;
                if (rarity == 1)
                {
                    prefabtreasure = config.globalSettings.BasicTreasurePrefab;
                    effectPrefab = config.globalSettings.BasicFXPrefab;
                }
                if (rarity == 2)
                {
                    prefabtreasure = config.globalSettings.UnCommonTreasurePrefab;
                    effectPrefab = config.globalSettings.UnCommonFXPrefab;
                }
                if (rarity == 3)
                {
                    prefabtreasure = config.globalSettings.RareTreasurePrefab;
                    effectPrefab = config.globalSettings.RareFXPrefab;
                }
                if (rarity == 4)
                {
                    prefabtreasure = config.globalSettings.EliteTreasurePrefab;
                    effectPrefab = config.globalSettings.EliteFXPrefab;
                }

                treasurechest = GameManager.server.CreateEntity(prefabtreasure, lootbox.transform.position, Quaternion.identity, true);
                treasurechest.skinID = 111;
                treasurechest.OwnerID = lootbox.OwnerID;
                var getStorage = treasurechest.GetComponent<StorageContainer>();
                if (getStorage != null)
                {
                    getStorage.onlyAcceptCategory = ItemCategory.All;
                }
                treasurechest.Spawn();
                treasurechest.gameObject.AddComponent<TreasureDespawner>();

                ItemContainer storageCont = treasurechest.GetComponent<StorageContainer>().inventory;
                storageCont.capacity = 12;
                if (config.lootTableSettings.UseOnlyLootTable) storageCont.Clear();

                AddLootTableItems(treasurechest);
                CheckSpawnVisibility(treasurechest);
                if (effectPrefab != null) Effect.server.Run(effectPrefab, treasurechest.transform.position);
                lootbox.Invoke("KillMessage", 0.2f);
            }

            private void AddLootTableItems(BaseEntity treasurebox)
            {
                if (rarity == 1) loottable = config.lootTableSettings.BasicLootTable;
                if (rarity == 2) loottable = config.lootTableSettings.UnCommonLootTable;
                if (rarity == 3) loottable = config.lootTableSettings.RareLootTable;
                if (rarity == 4) loottable = config.lootTableSettings.EliteLootTable;

                foreach (var itemLoot in loottable)
                {
                    var spawnChance = UnityEngine.Random.Range(1, 101);
                    if (spawnChance <= itemLoot.itemChance)
                    {
                        int randomAmount = UnityEngine.Random.Range(itemLoot.itemMinAmount, itemLoot.itemMaxAmount);
                        Item foundItem = ItemManager.CreateByPartialName(itemLoot.itemName, randomAmount, itemLoot.itemSkin);
                        if (foundItem != null)
                        {
                            AddCustomLoot(treasurebox, randomAmount, foundItem.info.itemid, itemLoot.itemSkin);
                        }
                    }
                }
            }

            private void AddCustomLoot(BaseEntity treasurebox, int qauntity, int itemid, ulong skinID)
            {
                if (qauntity <= 0) qauntity = 1;
                ItemContainer component1 = treasurebox.GetComponent<StorageContainer>().inventory;
                Item item = ItemManager.CreateByItemID(itemid, UnityEngine.Random.Range(1, qauntity + 1), skinID);
                component1.itemList.Add(item);
                component1.capacity++;
                item.parent = component1;
                item.MarkDirty();
            }

            private void CheckSpawnVisibility(BaseEntity entitybox)
            {
                if (isvisible) return;
                if (entitybox.IsOutside()) { isvisible = true; return; }
                entitybox.transform.position = entitybox.transform.position + new Vector3(0f, 0.2f, 0f);
                entitybox.transform.hasChanged = true;
                entitybox.SendNetworkUpdateImmediate();
                CheckSpawnVisibility(entitybox);
            }

            private void FixedUpdate()
            {
                if (despawncounter >= (config.globalSettings.DespawnTime * 15) && lootbox != null) { lootbox.Invoke("KillMessage", 0.1f); return; }
                despawncounter = despawncounter + 1f;
            }

            private void OnDestroy()
            {
                if (colorMarker != null) colorMarker.Invoke("KillMessage", 0.1f);
                if (mapmarker != null) mapmarker.Invoke("KillMessage", 0.1f);
                if (lootbox != null) lootbox.Invoke("KillMessage", 0.1f);
            }
        }

        #endregion

        #region TreasureDespawner 

        private class TreasureDespawner : MonoBehaviour
        {
            private BaseEntity treasure;
            private float despawncounter;

            private void Awake()
            {
                treasure = GetComponentInParent<BaseEntity>();
                despawncounter = 0f;
            }

            private void FixedUpdate()
            {
                if (despawncounter >= (config.globalSettings.TreasureDespawnTime * 15) && treasure != null) { treasure.Invoke("KillMessage", 0.1f); return; }
                despawncounter = despawncounter + 1f;
            }

            private void OnDestroy()
            {
                if (treasure != null) treasure.Invoke("KillMessage", 0.1f);
            }
        }

        #endregion
    }
}