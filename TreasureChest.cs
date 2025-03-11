using Oxide.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json.Converters;

namespace Oxide.Plugins
{
    [Info("Treasure Chest", "Copek", "2.1.5")]
    [Description("Give custom loot when interacting with a specific item.")]
    class TreasureChest : RustPlugin
    {
        private Configuration config;
        private const string GiveChestPermission = "treasurechest.itemloot.givechest";
        private static System.Random rng = new System.Random();

        [PluginReference]
        private Plugin ImageLibrary = null, Economics, ServerRewards;

        private Dictionary<ulong, DateTime> lastChestOpenTime = new Dictionary<ulong, DateTime>();
        #region Config
        class Configuration
        {
            [JsonProperty("BackgroundImageUrl")]
            public string ChestImageUrl { get; set; } = "https://www.dropbox.com/scl/fi/n04axnx5q2wm921pofvi7/combine_images__3_-removebg-preview.png?rlkey=tmfjk662psemyzerhi514x84x&dl=1";

            [JsonProperty("ChestHelp")]
            public List<string> ChestHelp { get; set; }

            [JsonProperty("Opening effect")]
            public string OpeningChestEffect = "assets/prefabs/misc/halloween/lootbag/effects/gold_open.prefab";

            [JsonProperty("Chests")]
            public List<ChestConfiguration> Chests { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        class ChestConfiguration
        {
            [JsonProperty("Enabled")]
            public bool Enabled { get; set; }

            [JsonProperty("CustomStackSize")]
            public int CustomStackSize { get; set; } = 1;

            [JsonProperty("ItemDisplayName")]
            public string ItemDisplayName { get; set; }

            [JsonProperty("ItemShortname")]
            public string ItemShortname { get; set; }

            [JsonProperty("LootBoxSkinID")]
            public ulong LootBoxSkinID { get; set; }

            [JsonProperty("MinChestAmount")]
            public int MinChestAmount { get; set; }

            [JsonProperty("MaxChestAmount")]
            public int MaxChestAmount { get; set; }

            [JsonProperty("CooldownSeconds")]
            public int CooldownSeconds { get; set; } = 60; // Default cooldown of 60 seconds

            [JsonProperty("LootTable")]
            public string LootTable { get; set; }

            [JsonProperty("LootTables")]
            public Dictionary<string, List<LootTableItem>> LootTables { get; set; }

            [JsonProperty("Spawns")]
            public List<CrateSpawn> Spawns { get; set; }
        }
        class CrateSpawn
        {
            [JsonProperty("PrefabPath")]
            public string PrefabPath { get; set; }

            [JsonProperty("SpawnChance")]
            public float SpawnChance { get; set; }
        }
        private enum EconomyType
        {
            Plugin,
            Item
        }

        class LootTableItem
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public EconomyType Economy { get; set; } = EconomyType.Item;
            public string PluginName { get; set; }
            public string Shortname { get; set; }
            public int MaxAmount { get; set; }
            public int MinAmount { get; set; }
            public float Probability { get; set; }
            public ulong SkinID { get; set; }
            public string DisplayName { get; set; }
            public bool IsBlueprint { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();

            if (config.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }
        protected override void LoadDefaultConfig() => config = GetDefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            Configuration defaultConfig = GetDefaultConfig();

            if (config.Version < new VersionNumber(2, 1, 0))
                config = defaultConfig;

            config.Version = Version;
            PrintWarning("Config update completed!");
        }

        private void Init()
        {
            Puts("Treasure Chest plugin loaded.");
            LoadConfig();
            permission.RegisterPermission(GiveChestPermission, this);
        }
        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.allPlayerList)
            {
                CuiHelper.DestroyUi(player, "TreasurePanel");
            }
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                ChestHelp = new List<string>
                {
                "Enabled - true/false , if false chest will not spawn in PrefabPaths",
                "CustomStackSize - now you can change stack size of each chest",
                "You can change skin and display name of each chest",
                "MinChestAmount/MaxCHestAmount - how much items player will get from chest ",
                "CooldownSeconds - how much seconds will player need to wait to open another chest",
                "You can add more items to loottable",
                "Economy: here you put item or plugin",
                "PluginName: economics or serverrewards",
                "If you use plugin ,shortname and skinId will be showed in Ui as item for economy,you will not get that item you will get balance of plugin",
                "IsBlueprint - if true you will get bp of that item",
                "Min/MaxAmount - quantity of that item (you can put min/max - to same number (1),so its min and max 1 quantity of that item)",
                "Probability - from 0.0 (0%) to 1.0 (100%) chance to get that item",
                "command : /givechest display name quantity; example /givechest green chest 5",
                "Spawns - PrefabPath (chose where will chest spawn),SpawnChance (0-100 ,chance to spawn chest in that prefab)",
                "Dont put same prefabpath for more type of chests,for each chest use different prefabpath",
                "Opening effect : if empty its disabled,you can change opening effect to something else"
                },

                Chests = new List<ChestConfiguration>
        {
            new ChestConfiguration
            {
                Enabled = true,
                ItemDisplayName = "Green Chest",
                ItemShortname = "xmas.present.large",
                LootBoxSkinID = 3038475567,
                MinChestAmount = 1,
                MaxChestAmount = 2,
                LootTable = "greenchest",
                LootTables = new Dictionary<string, List<LootTableItem>>
                {
                    ["greenchest"] = new List<LootTableItem>
                    {
                        new LootTableItem
                        {
                            Shortname = "rock",
                            MinAmount = 1,
                            MaxAmount = 5,
                            Probability = 1.0F,
                            SkinID = 2108583966,
                            DisplayName = "Best Rock In Game"
                        },
                        new LootTableItem
                        {
                            Shortname = "wood",
                            MinAmount = 100,
                            MaxAmount = 300,
                            Probability = 1.0F,
                            SkinID = 0,
                            DisplayName = ""
                        },
                        new LootTableItem
                        {
                            Shortname = "stones",
                            MinAmount = 100,
                            MaxAmount = 300,
                            Probability = 1.0F,
                            SkinID = 0,
                            DisplayName = ""
                        }
                    }
                },
                Spawns = new List<CrateSpawn>
                {
                    new CrateSpawn
                    {
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                        SpawnChance = 50f
                    }
                }
            },
            new ChestConfiguration
            {
                Enabled = true,
                ItemDisplayName = "Blue Chest",
                ItemShortname = "xmas.present.large",
                LootBoxSkinID = 3038475897,
                MinChestAmount = 1,
                MaxChestAmount = 2,
                LootTable = "bluechest",
                LootTables = new Dictionary<string, List<LootTableItem>>
                {
                    ["bluechest"] = new List<LootTableItem>
                    {
                        new LootTableItem
                        {
                            Shortname = "metal.refined",
                            MinAmount = 1,
                            MaxAmount = 5,
                            Probability = 1.0F,
                            SkinID = 0,
                            DisplayName = ""
                        },
                        new LootTableItem
                        {
                            Shortname = "hq.metal.ore",
                            MinAmount = 10,
                            MaxAmount = 30,
                            Probability = 1.0F,
                            SkinID = 0,
                            DisplayName = ""
                        },
                        new LootTableItem
                        {
                            Shortname = "lowgradefuel",
                            MinAmount = 10,
                            MaxAmount = 30,
                            Probability = 1.0F,
                            SkinID = 0,
                            DisplayName = ""
                        }
                    }
                },
                Spawns = new List<CrateSpawn>
                {
                    new CrateSpawn
                    {
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                        SpawnChance = 50f
                    }
                }
            },
            new ChestConfiguration
            {
                Enabled = true,
                ItemDisplayName = "Red Chest",
                ItemShortname = "xmas.present.large",
                LootBoxSkinID = 3038476006,
                MinChestAmount = 1,
                MaxChestAmount = 2,
                LootTable = "redchest",
                LootTables = new Dictionary<string, List<LootTableItem>>
                {
                    ["redchest"] = new List<LootTableItem>
                    {
                        new LootTableItem
                        {
                            Shortname = "rifle.ak",
                            MinAmount = 1,
                            MaxAmount = 5,
                            Probability = 1.0F,
                            SkinID = 0,
                            DisplayName = ""
                        },
                        new LootTableItem
                        {
                            Shortname = "rifle.ak",
                            MinAmount = 1,
                            MaxAmount = 5,
                            Probability = 1.0F,
                            SkinID = 2585539626,
                            DisplayName = ""
                        },
                        new LootTableItem
                        {
                            Shortname = "lmg.m249",
                            MinAmount = 1,
                            MaxAmount = 2,
                            Probability = 1.0F,
                            SkinID = 0,
                            DisplayName = "Best Gun"
                        }
                    }
                },
                Spawns = new List<CrateSpawn>
                {
                    new CrateSpawn
                    {
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                        SpawnChance = 50f
                    }
                }
            },
            new ChestConfiguration
            {
                Enabled = true,
                ItemDisplayName = "Gold Chest",
                ItemShortname = "xmas.present.large",
                LootBoxSkinID = 3041592792,
                MinChestAmount = 1,
                MaxChestAmount = 2,
                LootTable = "goldchest",
                LootTables = new Dictionary<string, List<LootTableItem>>
                {
                    ["goldchest"] = new List<LootTableItem>
                    {
                        new LootTableItem
                        {
                            Shortname = "xmas.present.large",
                            MinAmount = 1,
                            MaxAmount = 10,
                            Probability = 1.0F,
                            SkinID = 3038475567,
                            DisplayName = "Green Chest"
                        },
                        new LootTableItem
                        {
                            Shortname = "xmas.present.large",
                            MinAmount = 1,
                            MaxAmount = 5,
                            Probability = 1.0F,
                            SkinID = 3038475897,
                            DisplayName = "Blue Chest"
                        },
                        new LootTableItem
                        {
                            Shortname = "xmas.present.large",
                            MinAmount = 1,
                            MaxAmount = 3,
                            Probability = 1.0F,
                            SkinID = 3038476006,
                            DisplayName = "Red Chest"
                        }
                    }
                },
                Spawns = new List<CrateSpawn>
                {
                    new CrateSpawn
                    {
                        PrefabPath = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab",
                        SpawnChance = 50f
                    },
                    new CrateSpawn
                    {
                        PrefabPath = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate_oilrig.prefab",
                        SpawnChance = 50f
                    }
                }
            }
        }
            };
        }
        #endregion

        #region Logic
        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("The plugin is not installed on the server [ImageLibrary]");
            }
            else
            {
                ImageLibrary.Call("AddImage", config.ChestImageUrl, "Chest");
            }
            if (config.Chests.Sum(x => x.Spawns.Count) == 0) Unsubscribe("OnLootSpawn");
        }
        private string GetImg(string name)
        {
            return (string)ImageLibrary?.Call("GetImage", name) ?? "";
        }

        private static void SoundEffect(BasePlayer player, string effect)
        {
            if (player == null || string.IsNullOrEmpty(effect)) return;

            EffectNetwork.Send(new Effect(effect, player, 0, new Vector3(), new Vector3()), player.Connection);
        }

        private void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null) return;
            ChestConfiguration chest = config.Chests.FirstOrDefault(x => x.Spawns.Any(y => y.PrefabPath == container.PrefabName));
            if (chest == null || !chest.Enabled) return;
            CrateSpawn crate = chest.Spawns.FirstOrDefault(x => x.PrefabPath == container.PrefabName);
            if (crate == null) return;
            if (UnityEngine.Random.Range(0f, 100f) <= crate.SpawnChance)
            {
                if (container.inventory.itemList.Count == container.inventory.capacity) container.inventory.capacity++;
                Item item = GetChest(chest);
                if (!item.MoveToContainer(container.inventory)) item.Remove();
            }
        }

        private static Item GetChest(ChestConfiguration config)
        {
            Item item = ItemManager.CreateByName("xmas.present.large", 1, config.LootBoxSkinID);
            if (!string.IsNullOrEmpty(config.ItemDisplayName)) item.name = config.ItemDisplayName;
            return item;
        }

        private void GiveRandomItem(BasePlayer player, string lootTable)
        {
            var chestConfig = config.Chests.Find(c => c.LootTable == lootTable);
            if (chestConfig == null)
            {
                PrintError($"Chest config not found or not enabled for loot table '{lootTable}'.");
                return;
            }

            var lootTableItems = chestConfig.LootTables[lootTable];
            if (lootTableItems == null)
            {
                PrintError($"Loot table '{lootTable}' not found!");
                return;
            }

            int numItemsToSpawn = UnityEngine.Random.Range(chestConfig.MinChestAmount, chestConfig.MaxChestAmount + 1);

            var guaranteedItems = lootTableItems
                .Where(lootItem => lootItem.Probability >= 1.0f)
                .ToList();

            var randomItems = lootTableItems
                .Where(lootItem => lootItem.Probability < 1.0f)
                .ToList();

            List<LootTableItem> selectedItems = new List<LootTableItem>();

            selectedItems.AddRange(guaranteedItems);

            if (selectedItems.Count > numItemsToSpawn)
            {
                selectedItems = selectedItems.Take(numItemsToSpawn).ToList();
            }

            while (selectedItems.Count < numItemsToSpawn)
            {
                var weightedRandomItem = GetWeightedRandomItem(randomItems);
                if (weightedRandomItem == null) break;
                selectedItems.Add(weightedRandomItem);
                randomItems.Remove(weightedRandomItem);
            }

            foreach (var lootItem in selectedItems)
            {
                var randomAmountForItem = UnityEngine.Random.Range(lootItem.MinAmount, lootItem.MaxAmount + 1);

                if (lootItem.Economy == EconomyType.Plugin)
                {
                    if (AddEconomyBalance(player.UserIDString, randomAmountForItem, lootItem.PluginName))
                    {
                        SendReply(player, $"You have received ${randomAmountForItem} in your account.");
                    }
                }
                else
                {
                    Item item;
                    if (lootItem.IsBlueprint)
                    {
                        var itemDefinition = ItemManager.FindItemDefinition(lootItem.Shortname);
                        var blueprint = ItemManager.CreateByItemID(ItemManager.FindItemDefinition("blueprintbase").itemid, randomAmountForItem);
                        blueprint.blueprintTarget = itemDefinition.itemid;
                        item = blueprint;
                    }
                    else
                    {
                        item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(lootItem.Shortname).itemid, randomAmountForItem, lootItem.SkinID);
                    }

                    if (item != null)
                    {
                        if (!string.IsNullOrEmpty(lootItem.DisplayName))
                        {
                            item.name = lootItem.DisplayName;
                        }

                        if (!player.inventory.GiveItem(item, player.inventory.containerMain))
                        {
                            item.Drop(player.transform.position, player.transform.forward);
                        }
                    }
                }
            }
        }

        private LootTableItem GetWeightedRandomItem(List<LootTableItem> items)
        {
            if (items == null || items.Count == 0) return null;

            float totalWeight = items.Sum(item => item.Probability);
            float randomWeight = UnityEngine.Random.Range(0f, totalWeight);

            foreach (var item in items)
            {
                if (randomWeight < item.Probability)
                {
                    return item;
                }
                randomWeight -= item.Probability;
            }

            return null;
        }
        private string GetDefaultItemName(string itemShortname)
        {
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemShortname);

            if (itemDefinition != null && !string.IsNullOrEmpty(itemDefinition.displayName.english))
            {
                return itemDefinition.displayName.english;
            }

            return itemShortname;
        }
        private void GivePlayerItem(BasePlayer player, string shortname, int amount, ulong skinID, string displayName)
        {
            var itemDefinition = ItemManager.FindItemDefinition(shortname);
            if (itemDefinition == null)
            {
                PrintError($"Item shortname '{shortname}' not found!");
                return;
            }

            var chestConfig = config.Chests.FirstOrDefault(c => c.ItemShortname == shortname && c.LootBoxSkinID == skinID);
            int customStackSize = (chestConfig != null) ? chestConfig.CustomStackSize : itemDefinition.stackable;

            if (amount <= customStackSize)
            {
                var item = ItemManager.Create(itemDefinition, amount, skinID);
                if (!string.IsNullOrEmpty(displayName))
                {
                    item.name = displayName;
                }

                if (!player.inventory.GiveItem(item, player.inventory.containerMain))
                {
                    item.Drop(player.transform.position, player.transform.forward);
                }
            }
            else
            {
                while (amount > 0)
                {
                    var stackSize = Mathf.Min(amount, customStackSize);
                    var item = ItemManager.Create(itemDefinition, stackSize, skinID);

                    if (!string.IsNullOrEmpty(displayName))
                    {
                        item.name = displayName;
                    }

                    if (!player.inventory.GiveItem(item, player.inventory.containerMain))
                    {
                        item.Drop(player.transform.position, player.transform.forward);
                    }

                    amount -= stackSize;
                }
            }
        }
        private bool AddEconomyBalance(string playerId, int amount, string pluginName)
        {
            ulong playerID = Convert.ToUInt64(playerId);

            if (pluginName.Equals("Economics", StringComparison.OrdinalIgnoreCase))
            {
                if (Economics)
                {
                    Economics?.Call("Deposit", playerId, (double)amount);
                    return true;
                }
                else
                {
                    PrintError("Economics plugin is not loaded.");
                    return false;
                }
            }
            else if (pluginName.Equals("ServerRewards", StringComparison.OrdinalIgnoreCase))
            {
                if (ServerRewards)
                {
                    ServerRewards?.Call("AddPoints", playerID, amount);
                    return true;
                }
                else
                {
                    PrintError("ServerRewards plugin is not loaded.");
                    return false;
                }
            }
            else
            {
                PrintError("Unknown plugin specified for economy balance.");
                return false;
            }
        }
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || string.IsNullOrEmpty(action) || player == null)
                return null;

            var chestConfig = config.Chests.Find(c => c.ItemShortname == item.info.shortname && c.LootBoxSkinID == item.skin);
            if (chestConfig == null)
                return null;

            switch (action)
            {
                case "unwrap":

                    if (IsPlayerOnCooldown(player, chestConfig))
                    {
                        SendReply(player, $"You need to wait {GetCooldownTimeLeft(player, chestConfig)} seconds before opening another {chestConfig.ItemDisplayName}.");
                        return true;
                    }

                    ItemByPlayer[player] = item;
                    RustUI(player, chestConfig.LootTable);
                    return true;

                case "upgrade_item":
                    return true;
                default:
                    return null;
            }
        }
        private bool IsPlayerOnCooldown(BasePlayer player, ChestConfiguration chestConfig)
        {
            if (lastChestOpenTime.TryGetValue(player.userID, out var lastOpenTime))
            {
                double secondsSinceLastOpen = (DateTime.UtcNow - lastOpenTime).TotalSeconds;
                return secondsSinceLastOpen < chestConfig.CooldownSeconds;
            }
            return false;
        }

        private int GetCooldownTimeLeft(BasePlayer player, ChestConfiguration chestConfig)
        {
            if (lastChestOpenTime.TryGetValue(player.userID, out var lastOpenTime))
            {
                double secondsSinceLastOpen = (DateTime.UtcNow - lastOpenTime).TotalSeconds;
                return Math.Max(0, chestConfig.CooldownSeconds - (int)secondsSinceLastOpen);
            }
            return chestConfig.CooldownSeconds;
        }
        [ChatCommand("givechest")]
        private void GiveChestCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                SendReply(player, "Syntax: /givechest \"<display name>\" <quantity>");
                return;
            }

            string displayName = string.Join(" ", args.Take(args.Length - 1));
            int numLootBoxesToGive;

            if (!int.TryParse(args[args.Length - 1], out numLootBoxesToGive) || numLootBoxesToGive <= 0)
            {
                SendReply(player, "Invalid quantity. Please specify a positive number.");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, GiveChestPermission))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            var chestConfig = config.Chests.FirstOrDefault(c => c.ItemDisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            if (chestConfig == null)
            {
                SendReply(player, $"Invalid display name or configuration not found for '{displayName}'.");
                return;
            }

            for (int i = 0; i < numLootBoxesToGive;)
            {
                int stackSize = Mathf.Min(numLootBoxesToGive - i, chestConfig.CustomStackSize);
                GivePlayerItem(player, chestConfig.ItemShortname, stackSize, chestConfig.LootBoxSkinID, chestConfig.ItemDisplayName);
                i += stackSize;
            }

            SendReply(player, $"You've received {numLootBoxesToGive} <color=green>{chestConfig.ItemDisplayName}</color> chest!");
        }
        [ChatCommand("givechestto")]
        private void GiveChestToCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 3)
            {
                SendReply(player, "Syntax: /givechestto <player ID/name> \"<display name>\" <quantity>");
                return;
            }

            string targetPlayerIdentifier = args[0];
            string displayName = string.Join(" ", args.Skip(1).Take(args.Length - 2));
            int numLootBoxesToGive;

            if (!int.TryParse(args[args.Length - 1], out numLootBoxesToGive) || numLootBoxesToGive <= 0)
            {
                SendReply(player, "Invalid quantity. Please specify a positive number.");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, GiveChestPermission))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            BasePlayer targetPlayer = FindPlayerByIDOrName(targetPlayerIdentifier);
            if (targetPlayer == null)
            {
                SendReply(player, $"Player '{targetPlayerIdentifier}' not found.");
                return;
            }

            var chestConfig = config.Chests.FirstOrDefault(c => c.ItemDisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            if (chestConfig == null)
            {
                SendReply(player, $"Invalid display name or configuration not found for '{displayName}'.");
                return;
            }

            for (int i = 0; i < numLootBoxesToGive;)
            {
                int stackSize = Mathf.Min(numLootBoxesToGive - i, chestConfig.CustomStackSize);
                GivePlayerItem(targetPlayer, chestConfig.ItemShortname, stackSize, chestConfig.LootBoxSkinID, chestConfig.ItemDisplayName);
                i += stackSize;
            }

            SendReply(targetPlayer, $"You've received {numLootBoxesToGive} <color=green>{chestConfig.ItemDisplayName}</color> chest!");
            SendReply(player, $"You've given {numLootBoxesToGive} <color=green>{chestConfig.ItemDisplayName}</color> chest to {targetPlayer.displayName}!");
        }

        [ConsoleCommand("givechestto")]
        private void GiveChestConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 3)
            {
                arg.ReplyWith("Syntax: givechestto <player ID/name> \"<display name>\" <quantity>");
                return;
            }

            string targetPlayerIdentifier = arg.Args[0];
            string displayName = string.Join(" ", arg.Args.Skip(1).Take(arg.Args.Length - 2));
            int numLootBoxesToGive;

            if (!int.TryParse(arg.Args[arg.Args.Length - 1], out numLootBoxesToGive) || numLootBoxesToGive <= 0)
            {
                arg.ReplyWith("Invalid quantity. Please specify a positive number.");
                return;
            }

            BasePlayer targetPlayer = FindPlayerByIDOrName(targetPlayerIdentifier);
            if (targetPlayer == null)
            {
                arg.ReplyWith($"Player '{targetPlayerIdentifier}' not found.");
                return;
            }

            var chestConfig = config.Chests.FirstOrDefault(c => c.ItemDisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            if (chestConfig == null)
            {
                arg.ReplyWith($"Invalid display name or configuration not found for '{displayName}'.");
                return;
            }

            for (int i = 0; i < numLootBoxesToGive;)
            {
                int stackSize = Mathf.Min(numLootBoxesToGive - i, chestConfig.CustomStackSize);
                GivePlayerItem(targetPlayer, chestConfig.ItemShortname, stackSize, chestConfig.LootBoxSkinID, chestConfig.ItemDisplayName);
                i += stackSize;
            }

            SendReply(targetPlayer, $"You've received {numLootBoxesToGive} <color=green>{chestConfig.ItemDisplayName}</color> chest!");
            arg.ReplyWith($"You've given {numLootBoxesToGive} {chestConfig.ItemDisplayName} chest to {targetPlayer.displayName}!");
        }

        private BasePlayer FindPlayerByIDOrName(string identifier)
        {
            BasePlayer targetPlayer = null;

            if (ulong.TryParse(identifier, out ulong playerID))
            {
                targetPlayer = BasePlayer.FindAwakeOrSleeping(playerID.ToString());
            }
            else
            {
                targetPlayer = BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.Equals(identifier, StringComparison.OrdinalIgnoreCase));
                if (targetPlayer == null)
                {
                    targetPlayer = BasePlayer.sleepingPlayerList.FirstOrDefault(p => p.displayName.Equals(identifier, StringComparison.OrdinalIgnoreCase));
                }
            }

            return targetPlayer;
        }
        object OnMaxStackable(Item item)
        {
            if (item == null) return null;

            ChestConfiguration chestConfig = config.Chests.FirstOrDefault(c =>
                c.ItemShortname == item.info.shortname &&
                c.LootBoxSkinID == item.skin
            );

            if (chestConfig != null)
            {
                return chestConfig.CustomStackSize;
            }

            return null; 
        }
        private object CanStackItem(Item item, Item targetItem)
        {
            if (item == null || targetItem == null) return null;

            var chestConfig = config.Chests.FirstOrDefault(c => c.ItemShortname == item.info.shortname && c.LootBoxSkinID == item.skin);
            if (chestConfig != null)
            {
                return null;
            }

            return null;
        }

        private object CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem targetItem)
        {
            if (droppedItem == null || targetItem == null) return null;

            var item = droppedItem.GetItem();
            if (item != null && config.Chests.Any(c => c.ItemShortname == item.info.shortname && c.LootBoxSkinID == item.skin))
                return false;

            item = targetItem.GetItem();
            if (item != null && config.Chests.Any(c => c.ItemShortname == item.info.shortname && c.LootBoxSkinID == item.skin))
                return false;

            return null;
        }
        private readonly Dictionary<BasePlayer, Item> ItemByPlayer = new Dictionary<BasePlayer, Item>();
        #endregion

        #region VirutalOpening
        [ConsoleCommand("chestsim")]
        private void SimulateChests(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Syntax: chestsim <lootTable> <number of chests>");
                return;
            }

            string lootTable = arg.Args[0];
            int numChestsToSimulate;

            if (!int.TryParse(arg.Args[1], out numChestsToSimulate) || numChestsToSimulate <= 0)
            {
                arg.ReplyWith("Invalid number of chests. Please specify a positive number.");
                return;
            }

            SimulateLootGeneration(arg.Player(), lootTable, numChestsToSimulate, false);
        }

        [ConsoleCommand("chestsimr")]
        private void RconSimulateChests(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Syntax: chestsimr <lootTable> <number of chests>");
                return;
            }

            string lootTable = arg.Args[0];
            int numChestsToSimulate;

            if (!int.TryParse(arg.Args[1], out numChestsToSimulate) || numChestsToSimulate <= 0)
            {
                arg.ReplyWith("Invalid number of chests. Please specify a positive number.");
                return;
            }

            SimulateLootGeneration(null, lootTable, numChestsToSimulate, true);
        }
        private void SimulateLootGeneration(BasePlayer player, string lootTable, int numChestsToSimulate, bool isRcon)
        {
            var chestConfig = config.Chests.Find(c => c.LootTable == lootTable);
            if (chestConfig == null)
            {
                if (isRcon)
                    PrintError($"Chest config not found or not enabled for loot table '{lootTable}'.");
                else
                    player.ConsoleMessage($"Chest config not found or not enabled for loot table '{lootTable}'.");
                return;
            }

            var lootTableItems = chestConfig.LootTables[lootTable];
            if (lootTableItems == null)
            {
                if (isRcon)
                    PrintError($"Loot table '{lootTable}' not found!");
                else
                    player.ConsoleMessage($"Loot table '{lootTable}' not found!");
                return;
            }

            var lootResults = new Dictionary<(string shortname, ulong skinID), (int count, int totalAmount)>();

            for (int i = 0; i < numChestsToSimulate; i++)
            {
                var itemsInChest = GenerateLootForChest(chestConfig);

                foreach (var item in itemsInChest)
                {
                    var key = (item.shortname, item.skinID);
                    if (lootResults.ContainsKey(key))
                    {
                        lootResults[key] = (lootResults[key].count + 1, lootResults[key].totalAmount + item.amount);
                    }
                    else
                    {
                        lootResults[key] = (1, item.amount);
                    }
                }
            }

            if (isRcon)
                LogLootResultsToRcon(lootResults, numChestsToSimulate, lootTable);
            else
                LogLootResultsToPlayer(player, lootResults, numChestsToSimulate, lootTable);
        }
        private List<(string shortname, ulong skinID, int amount)> GenerateLootForChest(ChestConfiguration chestConfig)
        {
            var lootTableItems = chestConfig.LootTables[chestConfig.LootTable];

            var guaranteedItems = lootTableItems
                .Where(lootItem => lootItem.Probability >= 1.0f)
                .ToList();

            var randomItems = lootTableItems
                .Where(lootItem => lootItem.Probability < 1.0f)
                .ToList();

            int numItemsToSpawn = UnityEngine.Random.Range(chestConfig.MinChestAmount, chestConfig.MaxChestAmount + 1);
            List<LootTableItem> selectedItems = new List<LootTableItem>();

            selectedItems.AddRange(guaranteedItems);

            if (selectedItems.Count > numItemsToSpawn)
            {
                selectedItems = selectedItems.Take(numItemsToSpawn).ToList();
            }

            while (selectedItems.Count < numItemsToSpawn)
            {
                var weightedRandomItem = GetWeightedRandomItem(randomItems);
                if (weightedRandomItem == null) break;
                selectedItems.Add(weightedRandomItem);
                randomItems.Remove(weightedRandomItem);
            }

            var items = new List<(string shortname, ulong skinID, int amount)>();
            foreach (var lootItem in selectedItems)
            {
                var amount = UnityEngine.Random.Range(lootItem.MinAmount, lootItem.MaxAmount + 1);
                items.Add((lootItem.Shortname, lootItem.SkinID, amount));
            }

            return items;
        }
        private void LogLootResultsToPlayer(BasePlayer player, Dictionary<(string shortname, ulong skinID), (int count, int totalAmount)> lootResults, int numChestsToSimulate, string lootTable)
        {
            var result = new System.Text.StringBuilder();
            result.AppendLine($"[TreasureChest]");
            result.AppendLine($"Results of simulating {numChestsToSimulate} chests for loot table '{lootTable}':");
            result.AppendLine($"| {"Item",-41} | {"Count",-8} | {"Percent",-10} | {"Average",-10} | {"Total",-10} |");
            result.AppendLine($"|{"-".PadRight(43, '-')}|{"-".PadRight(10, '-')}|{"-".PadRight(12, '-')}|{"-".PadRight(12, '-')}|{"-".PadRight(12, '-')}|");

            foreach (var lootResult in lootResults.OrderByDescending(c => c.Value.count))
            {
                double percent = (double)lootResult.Value.count / numChestsToSimulate * 100;
                int average = lootResult.Value.totalAmount / lootResult.Value.count;

                var displayName = config.Chests
                    .SelectMany(chest => chest.LootTables.Values.SelectMany(items => items))
                    .FirstOrDefault(item => item.Shortname == lootResult.Key.shortname && item.SkinID == lootResult.Key.skinID)?.DisplayName;

                string itemName = lootResult.Key.shortname;
                string displayNameText = !string.IsNullOrEmpty(displayName) ? $"# {displayName}" : string.Empty;

                result.AppendLine($"| {displayNameText,-20} {itemName,-20} | {lootResult.Value.count,-8} | {percent,-10:F2} | {average,-10} | {lootResult.Value.totalAmount,-10} |");
            }

            player.ConsoleMessage(result.ToString());
        }

        private void LogLootResultsToRcon(Dictionary<(string shortname, ulong skinID), (int count, int totalAmount)> lootResults, int numChestsToSimulate, string lootTable)
        {
            var result = new System.Text.StringBuilder();
            result.AppendLine($"Results of simulating {numChestsToSimulate} chests for loot table '{lootTable}':");
            result.AppendLine($"| {"Item",-41} | {"Count",-8} | {"Percent",-10} | {"Average",-10} | {"Total",-10} |");
            result.AppendLine($"|{"".PadRight(43, '-')}|{"".PadRight(10, '-')}|{"".PadRight(12, '-')}|{"".PadRight(12, '-')}|{"".PadRight(12, '-')}|");

            foreach (var lootResult in lootResults.OrderByDescending(c => c.Value.count))
            {
                double percent = (double)lootResult.Value.count / numChestsToSimulate * 100;
                int average = lootResult.Value.totalAmount / lootResult.Value.count;

                var displayName = config.Chests
                    .SelectMany(chest => chest.LootTables.Values.SelectMany(items => items))
                    .FirstOrDefault(item => item.Shortname == lootResult.Key.shortname && item.SkinID == lootResult.Key.skinID)?.DisplayName;

                string itemName = lootResult.Key.shortname;
                string displayNameText = !string.IsNullOrEmpty(displayName) ? $"# {displayName}" : string.Empty;

                result.AppendLine($"| {displayNameText,-20} {itemName,-20} | {lootResult.Value.count,-8} | {percent,-10:F2} | {average,-10} | {lootResult.Value.totalAmount,-10} |");
            }

            Puts(result.ToString());
        }
        #endregion

        #region UI
        private void RustUI(BasePlayer player, string lootTable)
        {
            if (!ItemByPlayer.ContainsKey(player)) return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.1792453 0.1792453 0.1792453 0.4745098" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-233.928 -211.335", OffsetMax = "232.741 211.335" }
            }, "Overlay", "TreasurePanel");

            container.Add(new CuiElement
            {
                Name = "Image_1276",
                Parent = "TreasurePanel",
                Components = {
            new CuiRawImageComponent { Color = "1 1 1 0.4", Png = GetImg("Chest") },
            new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1 -1" },
            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-213.332 -159.998", OffsetMax = "213.328 159.992" }
        }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-202.993 -203.733", OffsetMax = "-71.607 -151.067" },
                Text = { Text = "OPEN CHEST", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 0.843 0 1" },
                Button = {
            Color = "0 0 0 0.85",
            Close = "TreasurePanel",
            Command = $"Ui_Test open {lootTable}"
        }
            }, "TreasurePanel", "OpenButton");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "71.607 -203.733", OffsetMax = "202.993 -151.067" },
                Text = { Text = "EXIT", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0 0.9811321 0.03699166 1" },
                Button = {
            Color = "0 0 0 0.85",
            Close = "TreasurePanel",
            Command = "Ui_Test close"
        }
            }, "TreasurePanel", "CloseButton");

            int rows = 3;
            int columns = 4;

            float slotWidth = 100;
            float slotHeight = 100;
            float horizontalSpacing = 10;
            float verticalSpacing = 10;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.6", AnchorMax = "0.5 0.6", OffsetMin = $"{-(columns * (slotWidth + horizontalSpacing)) / 2} {-(rows * (slotHeight + verticalSpacing)) / 2}", OffsetMax = $"{(columns * (slotWidth + horizontalSpacing)) / 2} {(rows * (slotHeight + verticalSpacing)) / 2}" },
                Image = { Color = "0.2 0.2 0.2 0.7" },
            }, "TreasurePanel", "SlotPanel");

            var chestConfig = config.Chests.Find(c => c.LootTable == lootTable);
            if (chestConfig != null && chestConfig.LootTables.TryGetValue(lootTable, out var lootTableItems))
            {
                int totalItems = lootTableItems.Count;
                int itemsPerPage = rows * columns;
                int totalPages = (int)Math.Ceiling((float)totalItems / itemsPerPage);

                int currentPage = 0;

                if (playerPage.ContainsKey(player) && playerPage[player] >= 1 && playerPage[player] <= totalPages)
                {
                    currentPage = playerPage[player] - 1;
                }
                else
                {
                    playerPage[player] = 1;
                }

                if (currentPage < totalPages - 1)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "17.308 -165", OffsetMax = "62.308 -138" },
                        Text = { Text = ">>>>", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                        Button = {
                    Color = "1 1 1 0.5",
                    Command = $"Ui_Test next_page {lootTable}",
                    Close = "TreasurePanel"
                }
                    }, "TreasurePanel", "NextButton");
                }

                if (currentPage > 0)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.308 -165", OffsetMax = "-17.308 -138" },
                        Text = { Text = "<<<<", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                        Button = {
                    Color = "1 1 1 0.5",
                    Command = $"Ui_Test prev_page {lootTable}",
                    Close = "TreasurePanel"
                }
                    }, "TreasurePanel", "PrevButton");
                }

                int startIndex = currentPage * itemsPerPage;
                int endIndex = startIndex + itemsPerPage;

                for (int i = startIndex; i < endIndex; i++)
                {
                    int rowIndex = (i - startIndex) / columns;
                    int columnIndex = (i - startIndex) % columns;

                    float xOffset = (columnIndex * (slotWidth + horizontalSpacing)) + horizontalSpacing / 2;
                    float yOffset = (rows - 1 - rowIndex) * (slotHeight + verticalSpacing);

                    if (i < lootTableItems.Count)
                    {
                        var lootItem = lootTableItems[i];
                        int itemId = lootItem.IsBlueprint ? ItemManager.FindItemDefinition("blueprintbase").itemid : ItemManager.FindItemDefinition(lootItem.Shortname).itemid;

                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{xOffset} {yOffset}", OffsetMax = $"{xOffset + slotWidth} {yOffset + slotHeight}" },
                            Button = { Command = $"Ui_Test slot_click {i}", Color = "0.8 0.8 0.8 0.6" },
                            Text = { Text = GetSlotText(lootTableItems[i]), FontSize = 12, Align = TextAnchor.UpperCenter, Color = "0 0 0 1" }
                        }, "SlotPanel", $"Slot_{i}");

                        if (lootItem.IsBlueprint)
                        {
                            container.Add(new CuiElement
                            {
                                Components = {
                            new CuiImageComponent {
                                Color = "1 1 1 0.9",
                                ItemId = itemId,
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/icons/iconmaterial.mat"
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "10 10",
                                OffsetMax = "-10 -10"
                            },
                        },
                                Parent = $"Slot_{i}"
                            });
                        }

                        container.Add(new CuiElement
                        {
                            Components = {
                        new CuiImageComponent {
                            Color = "1 1 1 0.9",
                            ItemId = ItemManager.FindItemDefinition(lootItem.Shortname).itemid,
                            SkinId = lootItem.SkinID,
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/icons/iconmaterial.mat"
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = lootItem.IsBlueprint ? "20 20" : "10 10", 
                            OffsetMax = lootItem.IsBlueprint ? "-20 -20" : "-10 -10" 
                        },
                    },
                            Parent = $"Slot_{i}"
                        });


                    }
                    else
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{xOffset} {yOffset}", OffsetMax = $"{xOffset + slotWidth} {yOffset + slotHeight}" },
                            Image = { Color = "0 0 0 0" },
                        }, "SlotPanel", $"Slot_{i}");

                    }
                }
            }

            container.Add(new CuiPanel
            {
                RectTransform = {
            AnchorMin = "0.5 0.5", 
            AnchorMax = "0.5 0.5", 
            OffsetMin = "-233.417 214", 
            OffsetMax = "233.252 254"
        },
                Image = {
            Color = "0 0 0 0.85"
        }
            }, "TreasurePanel", "TreasureTextBackground");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-45.922 217.5", OffsetMax = "45.922 244.5" },
                Text = { Text = "TREASURE:", FontSize = 19, Align = TextAnchor.UpperCenter, Color = "1 0.843 0 1" }
            }, "TreasurePanel");

            CuiHelper.DestroyUi(player, "RustUI");
            CuiHelper.AddUi(player, container);
        }
        int itemNameFontSize = 12;
        int probabilityFontSize = 6;
        int minMaxFontSize = 12;

        private string GetSlotText(LootTableItem lootItem)
        {
            string itemName;
            string probabilityText = $"{lootItem.Probability:P}";
            string minMaxAmountText = $"x{lootItem.MinAmount}-{lootItem.MaxAmount}";


            if (!string.IsNullOrEmpty(lootItem.DisplayName))
            {
                itemName = lootItem.DisplayName;
            }
            else
            {

                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(lootItem.Shortname);
                if (itemDefinition != null)
                {
                    itemName = itemDefinition.displayName.english;
                }
                else
                {
                    itemName = lootItem.Shortname;
                }
            }

            return $"<size={itemNameFontSize}>{itemName}</size>\n\n\n\n\n\n<size={minMaxFontSize}>({minMaxAmountText})</size><size={probabilityFontSize}>{probabilityText}</size>";
        }
        private Dictionary<BasePlayer, int> playerPage = new Dictionary<BasePlayer, int>();

        private void NextPage(BasePlayer player, string lootTable)
        {
            if (playerPage.ContainsKey(player))
            {
                playerPage[player]++;
            }
            else
            {
                playerPage[player] = 1;
            }

            RustUI(player, lootTable);
        }

        private void PreviousPage(BasePlayer player, string lootTable)
        {
            if (playerPage.ContainsKey(player) && playerPage[player] > 1)
            {
                playerPage[player]--;
            }
            else
            {
                playerPage[player] = 1;
            }

            RustUI(player, lootTable);
        }

        [ConsoleCommand("Ui_Test")]
        private void CmdConsoleTest(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.player != null)
            {
                var player = arg.Connection.player as BasePlayer;

                if (arg.Args == null || arg.Args.Length == 0)
                {
                    player.ChatMessage("Usage: Ui_Test <command>");
                    return;
                }

                string subCommand = arg.Args[0].ToLower();

                switch (subCommand)
                {
                    case "close":

                        CuiHelper.DestroyUi(player, "TreasurePanel");
                        break;

                    case "open":
                        string lootTable = arg.Args.Length > 1 ? arg.Args[1] : "";

                        if (ItemByPlayer.ContainsKey(player))
                        {
                            var chestConfig = config.Chests.FirstOrDefault(c =>
                                c.ItemShortname == ItemByPlayer[player].info.shortname &&
                                c.LootBoxSkinID == ItemByPlayer[player].skin);

                            if (chestConfig == null)
                            {
                                player.ChatMessage("Error: Chest configuration not found.");
                                return;
                            }

                            if (IsPlayerOnCooldown(player, chestConfig))
                            {
                                player.ChatMessage($"You need to wait {GetCooldownTimeLeft(player, chestConfig)} seconds before opening another {chestConfig.ItemDisplayName}.");
                                return;
                            }

                            GiveRandomItem(player, lootTable);

                            ItemByPlayer[player].amount--;

                            if (ItemByPlayer[player].amount <= 0)
                            {
                                ItemByPlayer[player].Remove();
                                ItemByPlayer.Remove(player);
                            }
                            else
                            {
                                ItemByPlayer[player].MarkDirty();
                            }

                            SoundEffect(player, config.OpeningChestEffect);
                            CuiHelper.DestroyUi(player, "TreasurePanel");

                            lastChestOpenTime[player.userID] = DateTime.UtcNow;
                        }
                        break;

                    case "slot_click":

                        if (arg.Args.Length > 1)
                        {
                            int slotIndex;
                            if (int.TryParse(arg.Args[1], out slotIndex))
                            {
                                // Handle the slot click for slot at index 'slotIndex'
                                // Future Update
                            }
                        }
                        break;
                    case "next_page":

                        string lootTableForNext = arg.Args.Length > 1 ? arg.Args[1] : "";
                        NextPage(player, lootTableForNext);
                        break;

                    case "prev_page":

                        string lootTableForPrev = arg.Args.Length > 1 ? arg.Args[1] : "";
                        PreviousPage(player, lootTableForPrev);
                        break;

                    default:
                        player.ChatMessage("Unknown command: " + subCommand);
                        break;
                }
            }
        }
        #endregion
    }
}