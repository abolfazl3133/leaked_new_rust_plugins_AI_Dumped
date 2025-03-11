using System;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using CompanionServer.Handlers;
using System.Linq;
using Oxide.Core.Plugins;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("TreasureMaps", "Copek", "1.1.5")]
    class TreasureMaps : RustPlugin
    {
        # region Config
        private Configuration config;
        private static System.Random rng = new System.Random();

        private int _halfWorldSize;

        [PluginReference] private readonly Plugin NpcSpawn, Economics, ServerRewards;

        class Configuration
        {
            [JsonProperty("AutomaticEventEnabled")]
            public bool AutomaticEventEnabled { get; set; } = false;

            [JsonProperty("MaxEventsAtTime")]
            public int MaxConcurrentEvents { get; set; } = 5;

            [JsonProperty("MinEventTimer")]
            public float MinEventTimer { get; set; } = 1800f; // 30 minutes

            [JsonProperty("MaxEventTimer")]
            public float MaxEventTimer { get; set; } = 3600f;

            [JsonProperty("LocalTreasureMaxDistance")]
            public float LocalTreasureMaxDistance { get; set; } = 150;

            [JsonProperty("MaxOpenedMaps")]
            public int MaxOpenedMaps { get; set; } = 3; 

            [JsonProperty("MapOpenCooldown")]
            public int MapOpenCooldown { get; set; } = 60; // Default value, in seconds

            [JsonProperty("MaxOpenedCoins")]
            public int MaxOpenedCoins { get; set; } = 3;

            [JsonProperty("CoinOpenCooldown")]
            public int CoinOpenCooldown { get; set; } = 60; // Default value, in seconds

            [JsonProperty("Chest/NPC/Marker DestroyTimer")]
            public float ChestDestroyTimer { get; set; } = 1800f;

            [JsonProperty("TreasureMapsDescription")]
            public List<string> TreasureMapsDescription { get; set; }

            [JsonProperty("Maps")]
            public List<MapConfiguration> Maps { get; set; }

            [JsonProperty("Coins")]
            public List<CoinConfiguration> Coins { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }

        }

        class MapConfiguration
        {
            [JsonProperty("EventProbability")]
            public float EventProbability { get; set; } = 0.1f;

            [JsonProperty("MarkerRadius")]
            public float MarkerRadius { get; set; } = 0.2f;

            [JsonProperty("MarkerDisplayName")]
            public string MarkerDisplayName { get; set; } = "Treasure";

            [JsonProperty("MarkerColor")]
            public string MarkerColor { get; set; } = "00FFFF";

            [JsonProperty("MarkerOutlineColor")]
            public string MarkerOutlineColor { get; set; } = "00FFFFFF";

            [JsonProperty("CustomStackSize")]
            public int CustomStackSize { get; set; } = 1;

            [JsonProperty("ItemDisplayName")]
            public string ItemDisplayName { get; set; }

            [JsonProperty("ItemShortname")]
            public string ItemShortname { get; set; }

            [JsonProperty("SkinID")]
            public ulong SkinID { get; set; }

            [JsonProperty("SpawnedPrefabChest")]
            public string SpawnedPrefabChest { get; set; }

            [JsonProperty("SpawnedPrefabSkin")]
            public ulong SpawnedPrefabSkin { get; set; }

            [JsonProperty("MinItemPerChest")]
            public int MinItemPerChest { get; set; }

            [JsonProperty("MaxItemPerChest")]
            public int MaxItemPerChest { get; set; }

            [JsonProperty("LootTable")]
            public string LootTable { get; set; }

            [JsonProperty("LootTables")]
            public Dictionary<string, List<LootTableItem>> LootTables { get; set; }

            [JsonProperty("NPCSpawns")]
            public List<NPCSpawn> NPCSpawns { get; set; }

            [JsonProperty("Spawns")]
            public List<MapSpawn> Spawns { get; set; }

            [JsonProperty("SpawnBradley")]
            public bool SpawnBradley { get; set; } = false;

            [JsonProperty("BradleyHealth")]
            public int BradleyHealth { get; set; } = 1000; // Default Bradley health

            [JsonProperty("MinCratesToSpawn")]
            public int MinCratesToSpawn { get; set; } = 1; // Default min crates

            [JsonProperty("MaxCratesToSpawn")]
            public int MaxCratesToSpawn { get; set; } = 3; // Default max crates
        }
        private enum EconomyType
        {
            Plugin,
            Item
        }
        class CoinConfiguration
        {
            [JsonProperty("CustomStackSize")]
            public int CustomStackSize { get; set; } = 1;

            [JsonProperty("ItemDisplayName")]
            public string ItemDisplayName { get; set; }

            [JsonProperty("ItemShortname")]
            public string ItemShortname { get; set; }

            [JsonProperty("SkinID")]
            public ulong SkinID { get; set; }

            [JsonProperty("MinItemPerCoin")]
            public int MinItemPerCoin { get; set; }

            [JsonProperty("MaxItemPerCoin")]
            public int MaxItemPerCoin { get; set; }

            [JsonProperty("LootTable")]
            public string LootTable { get; set; }

            [JsonProperty("LootTables")]
            public Dictionary<string, List<CoinLootTableItem>> LootTables { get; set; }

            [JsonProperty("Spawns")]
            public List<MapSpawn> Spawns { get; set; }
        }
        
        class LootTableItem
        {
            public string DisplayName { get; set; }
            public string Shortname { get; set; }
            public ulong SkinID { get; set; }
            public int MaxItemAmount { get; set; }
            public int MinItemAmount { get; set; }
            public float Probability { get; set; }
            public bool IsBlueprint { get; set; }
        }
        class CoinLootTableItem
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public EconomyType Economy { get; set; } = EconomyType.Item;
            public string PluginName { get; set; }

            public string DisplayName { get; set; }
            public string Shortname { get; set; }
            public ulong SkinID { get; set; }
            public int MaxItemAmount { get; set; }
            public int MinItemAmount { get; set; }
            public float Probability { get; set; }
            public bool IsBlueprint { get; set; }
        }

        class MapSpawn
        {
            [JsonProperty("PrefabPath")]
            public string PrefabPath { get; set; }

            [JsonProperty("SpawnChance")]
            public float SpawnChance { get; set; }
        }
        class NPCSpawn
        {
            [JsonProperty("SpawnCount")]
            public int SpawnCount { get; set; } = 0;

            [JsonProperty("SpawnRadius")]
            public float SpawnRadius { get; set; } = 5f;

            [JsonProperty("EntityDisplayName")]
            public string EntityDisplayName { get; set; } = "Treasure Guard";

            [JsonProperty("Health")]
            public int Health { get; set; } = 100;

            [JsonProperty("RoamRange")]
            public float RoamRange { get; set; } = 5f;

            [JsonProperty("ChaseRange")]
            public float ChaseRange { get; set; } = 30f;

            [JsonProperty("SenseRange")]
            public float SenseRange { get; set; } = 20f;

            [JsonProperty("ListenRange")]
            public float ListenRange { get; set; } = 10f;

            [JsonProperty("AttackRangeMultiplier")]
            public float AttackRangeMultiplier { get; set; } = 1f;

            [JsonProperty("CheckVisionCone")]
            public bool CheckVisionCone { get; set; } = true;

            [JsonProperty("VisionCone")]
            public float VisionCone { get; set; } = 140f;

            [JsonProperty("HostileTargetsOnly")]
            public bool HostileTargetsOnly { get; set; } = false;

            [JsonProperty("DamageScale")]
            public float DamageScale { get; set; } = 1f;

            [JsonProperty("AimConeScale")]
            public float AimConeScale { get; set; } = 1f;

            [JsonProperty("Speed")]
            public float Speed { get; set; } = 6f;

            [JsonProperty("MemoryDuration")]
            public float MemoryDuration { get; set; } = 10f;

            [JsonProperty("Kit")]
            public string Kit { get; set; }

            [JsonProperty("WearItems")]
            public List<NPCWearItem> WearItems { get; set; } = new List<NPCWearItem>();

            [JsonProperty("BeltItems")]
            public List<NPCBeltItem> BeltItems { get; set; } = new List<NPCBeltItem>();

            [JsonProperty("CustomLoot")]
            public bool CustomLoot { get; set; } = false;

            [JsonProperty("LootTable")]
            public List<LootTableItem> LootTable { get; set; } = new List<LootTableItem>();
        }
        class NPCWearItem
        {
            [JsonProperty("ShortName")]
            public string ShortName { get; set; }

            [JsonProperty("SkinID")]
            public ulong SkinId { get; set; }
        }

        class NPCBeltItem
        {
            [JsonProperty("ShortName")]
            public string ShortName { get; set; }

            [JsonProperty("Amount")]
            public int Amount { get; set; }

            [JsonProperty("SkinID")]
            public ulong SkinId { get; set; }

            [JsonProperty("Mods")]
            public List<string> Mods { get; set; } = new List<string>();

            [JsonProperty("Ammo")]
            public string Ammo { get; set; }
        }
        class PlayerMapData
        {
            public int OpenedMapsCount { get; set; }
            public DateTime LastMapOpenTime { get; set; }

            public int OpenedCoinsCount { get; set; }
            public DateTime LastCoinOpenTime { get; set; }

            public PlayerMapData()
            {
                OpenedMapsCount = 0;
                LastMapOpenTime = DateTime.MinValue;
                OpenedCoinsCount = 0;
                LastCoinOpenTime = DateTime.MinValue;
            }
        }
        private JObject GetNpcConfig(NPCSpawn npcSpawn)
        {
            return new JObject
            {
                ["Name"] = npcSpawn.EntityDisplayName,
                ["Health"] = npcSpawn.Health,
                ["RoamRange"] = npcSpawn.RoamRange,
                ["ChaseRange"] = npcSpawn.ChaseRange,
                ["SenseRange"] = npcSpawn.SenseRange,
                ["ListenRange"] = npcSpawn.ListenRange,
                ["AttackRangeMultiplier"] = npcSpawn.AttackRangeMultiplier,
                ["CheckVisionCone"] = npcSpawn.CheckVisionCone,
                ["VisionCone"] = npcSpawn.VisionCone,
                ["HostileTargetsOnly"] = npcSpawn.HostileTargetsOnly,
                ["DamageScale"] = npcSpawn.DamageScale,
                ["TurretDamageScale"] = 0f,
                ["AimConeScale"] = npcSpawn.AimConeScale,
                ["DisableRadio"] = true,
                ["CanRunAwayWater"] = true,
                ["CanSleep"] = false,
                ["Speed"] = npcSpawn.Speed,
                ["AreaMask"] = 1,
                ["AgentTypeID"] = -1372625422,
                ["HomePosition"] = string.Empty,
                ["MemoryDuration"] = npcSpawn.MemoryDuration,
                ["Kit"] = npcSpawn.Kit,
                ["WearItems"] = new JArray(npcSpawn.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinId })),
                ["BeltItems"] = new JArray(npcSpawn.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinId, ["Mods"] = new JArray(x.Mods), ["Ammo"] = x.Ammo })),
                ["States"] = new JArray { "RoamState", "ChaseState", "CombatState" }
            };
        }
        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null)
            {
                Puts("OnCorpsePopulate: entity is null");
                return;
            }

            if (config?.Maps == null)
            {
                Puts("OnCorpsePopulate: config.Maps is null");
                return;
            }
            var matchingNpcSpawn = config.Maps
                .Where(m => m.NPCSpawns != null)
                .SelectMany(m => m.NPCSpawns)
                .FirstOrDefault(n => n.EntityDisplayName == entity.displayName);


            if (matchingNpcSpawn == null)
            {
                return;
            }

            NextTick(() =>
            {
                if (corpse == null)
                {
                    Puts("OnCorpsePopulate: corpse is null");
                    return;
                }


                if (matchingNpcSpawn.CustomLoot)
                {
                    PopulateCorpseWithCustomLoot(corpse, matchingNpcSpawn.LootTable);
                }
            });
        }

        private void PopulateCorpseWithCustomLoot(NPCPlayerCorpse corpse, List<LootTableItem> lootTable)
        {
            var container = corpse.containers[0];
            container.Clear();

            var selectedItems = lootTable
                .Where(item => UnityEngine.Random.value <= item.Probability)
                .ToList();

            foreach (var lootItem in selectedItems)
            {
                var amount = UnityEngine.Random.Range(lootItem.MinItemAmount, lootItem.MaxItemAmount + 1);

                Item item;
                if (lootItem.IsBlueprint)
                {
                    var itemDefinition = ItemManager.FindItemDefinition(lootItem.Shortname);
                    var blueprint = ItemManager.CreateByItemID(ItemManager.FindItemDefinition("blueprintbase").itemid, amount);
                    blueprint.blueprintTarget = itemDefinition.itemid;
                    item = blueprint;
                }
                else
                {
                    var itemDefinition = ItemManager.FindItemDefinition(lootItem.Shortname);
                    item = ItemManager.CreateByItemID(itemDefinition.itemid, amount, lootItem.SkinID);
                }

                if (item != null)
                {
                    if (!string.IsNullOrEmpty(lootItem.DisplayName))
                    {
                        item.name = lootItem.DisplayName;
                    }
                    container.Insert(item);
                }
            }
        }

        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null) return null;

            var npcSpawn = config.Maps
                .Where(m => m.NPCSpawns != null)
                .SelectMany(m => m.NPCSpawns)
                .FirstOrDefault(n => n.EntityDisplayName == entity.displayName);

            if (npcSpawn == null) return null;

            return npcSpawn.CustomLoot ? (object)true : null;
        }
        private void OnEntityDeath(ScientistNPC npc, HitInfo info)
        {
            if (npc == null || info == null) return;
        }
        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                AutomaticEventEnabled = false,
                MaxConcurrentEvents = 5,
                MinEventTimer = 1800,
                MaxEventTimer = 3600,
                LocalTreasureMaxDistance = 150,
                MaxOpenedMaps = 3,
                MapOpenCooldown = 60,
                MaxOpenedCoins = 3,
                CoinOpenCooldown = 60,
                TreasureMapsDescription = new List<string>
                {
                "LocalTreasureMaxDistance - max distance of spawning prefab/chest",
                "CustomStackSize - you can change stack size of each map,and for coins",
                "You can change skin and display name of each map,for each coin",
                "MinItemPerChest/MaxItemPerChest - how much items player will get from spawned chest ",
                "MinItemPerCoin/MaxItemPerCoin - how much items player will get from coin",
                "SpawnedPrefabChest - what chest will spawn when you unwarp map item,if you put default rust container it will be default rust loot table",
                "SpawnedPrefabSkin - skin of prefab",
                "You can add more items to loottable,works also with loottable of coins",
                "Min/MaxAmount - quantity of that item (you can put min/max - to same number (1),so its min and max 1 quantity of that item)",
                "Probability - from 0.0 (0%) to 1.0 (100%) chance to get that item",
                "IsBlueprint - if true you will give bp of that item",
                "command : /givemap <display name> <quantity>; example /givemap green map 5",
                "command : /givecoin <display name> <quantity>; example /givecoin gold coin 5",
                "command : /givemapto <player ID/name> <display name> <quantity>; example /givemapto copek green map 5",
                "command : /givecointo <player ID/name> <display name> <quantity>; example /givecointo copek gold coin 5",
                "console command : givemapto <player ID/name> <display name> <quantity>; example givemapto copek green map 5",
                "console command : givecointo <player ID/name> <display name> <quantity>; example givecointo copek gold coin 5",
                "Spawns - PrefabPath (chose where will map spawn),SpawnChance (0-100 ,chance to spawn map in that prefab),works also with coins,works also with coins",
                "SpawnCount - how much scientist will be defending chest",
                "SpawnRadius - distance where scientist will spawn",
                "NpcOptions - you can change stats of npc Health,Damage and more",
                "Kits - you can use kits or you can manualy add wear,belt items",
                "EntityDisplayName - name of npc, for each group change name ,if you are using custom loot for npc",
                "CustomLoot - true/false ,if true you can add custom loot table for npc body",
                "You can now add new custom item <coins>, coins can we crackopen/unwarp and you will get items/plugin balance",
                "In coins you have option to give economics and serverrewards",
                "Economy: here you put item or plugin",
                "PluginName: economics or serverrewards"
                },
                Maps = new List<MapConfiguration>
        {
            new MapConfiguration
            {
                EventProbability = 1.0f,
                MarkerRadius = 0.2f,
                MarkerDisplayName = "Green Treasure",
                MarkerColor = "00FF00",
                MarkerOutlineColor = "00FF00",
                CustomStackSize = 10,
                ItemDisplayName = "Green Map",
                ItemShortname = "xmas.present.large",
                SkinID = 3073615238,
                SpawnedPrefabChest = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab",
                SpawnedPrefabSkin = 1818868472,
                MinItemPerChest = 1,
                MaxItemPerChest = 2,
                LootTable = "greenmap",
                LootTables = new Dictionary<string, List<LootTableItem>>
                {
                    {
                        "greenmap", new List<LootTableItem>
                        {
                            new LootTableItem
                            {
                                DisplayName = "",
                                Shortname = "rock",
                                MinItemAmount = 1,
                                MaxItemAmount = 5,
                                Probability = 1.0F,
                                SkinID = 0,
                            }
                        }
                    }
                },
                Spawns = new List<MapSpawn>
                {
                    new MapSpawn
                    {
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                        SpawnChance = 0f
                    }
                },
                NPCSpawns = new List<NPCSpawn>
                {
                    new NPCSpawn
                    {
                        SpawnCount = 1,
                        SpawnRadius = 5f,
                        Health = 100,
                        RoamRange = 5f,
                        ChaseRange = 30f,
                        SenseRange = 20f,
                        ListenRange = 10f,
                        AttackRangeMultiplier = 1f,
                        CheckVisionCone = true,
                        VisionCone = 140f,
                        HostileTargetsOnly = false,
                        DamageScale = 1f,
                        AimConeScale = 1f,
                        Speed = 6f,
                        MemoryDuration = 10f,
                        Kit = "",
                        WearItems = new List<NPCWearItem>
                        {
                            new NPCWearItem
                            {
                                ShortName = "hazmatsuit",
                                SkinId = 0
                            }
                        },
                        BeltItems = new List<NPCBeltItem>
                        {
                            new NPCBeltItem
                            {
                                ShortName = "rifle.ak",
                                Amount = 1,
                                SkinId = 0,
                                Mods = new List<string> { "weapon.mod.flashlight" },
                                Ammo = "ammo.rifle"
                            }
                        },
                        CustomLoot = false,
                        LootTable = new List<LootTableItem>
                        {
                            new LootTableItem
                            {
                                DisplayName = "Custom Rock",
                                Shortname = "rock",
                                SkinID = 0,
                                MinItemAmount = 1,
                                MaxItemAmount = 5,
                                Probability = 1.0f,
                                IsBlueprint = false
                            }
                        }
                    }
                }
            },
            new MapConfiguration
            {
                EventProbability = 1.0f,
                MarkerRadius = 0.2f,
                MarkerDisplayName = "Blue Treasure",
                MarkerColor = "00BFFF",
                MarkerOutlineColor = "00BFFF",
                CustomStackSize = 10,
                ItemDisplayName = "Blue Map",
                ItemShortname = "xmas.present.large",
                SkinID = 3073615579,
                SpawnedPrefabChest = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",
                SpawnedPrefabSkin = 837107924,
                MinItemPerChest = 1,
                MaxItemPerChest = 2,
                LootTable = "bluemap",
                LootTables = new Dictionary<string, List<LootTableItem>>
                {
                    {
                        "bluemap", new List<LootTableItem>
                        {
                            new LootTableItem
                            {
                                DisplayName = "",
                                Shortname = "scrap",
                                MinItemAmount = 1,
                                MaxItemAmount = 5,
                                Probability = 1.0F,
                                SkinID = 0,
                            }
                        }
                    }
                },
                Spawns = new List<MapSpawn>
                {
                    new MapSpawn
                    {
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                        SpawnChance = 0f
                    }
                }
            },
            new MapConfiguration
            {
                EventProbability = 1.0f,
                MarkerRadius = 0.2f,
                MarkerDisplayName = "Red Treasure",
                MarkerColor = "FF0000",
                MarkerOutlineColor = "FF0000",
                CustomStackSize = 10,
                ItemDisplayName = "Red Map",
                ItemShortname = "xmas.present.large",
                SkinID = 3073615919,
                SpawnedPrefabChest = "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_a.prefab",
                SpawnedPrefabSkin = 0,
                MinItemPerChest = 1,
                MaxItemPerChest = 2,
                LootTable = "redmap",
                LootTables = new Dictionary<string, List<LootTableItem>>
                {
                    {
                        "redmap", new List<LootTableItem>
                        {
                            new LootTableItem
                            {
                                DisplayName = "Not Bad Weapon",
                                Shortname = "rifle.ak",
                                MinItemAmount = 1,
                                MaxItemAmount = 1,
                                Probability = 1.0F,
                                SkinID = 0,
                            }
                        }
                    }
                },
                Spawns = new List<MapSpawn>
                {
                    new MapSpawn
                    {
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                        SpawnChance = 0f
                    }
                }
            },
            new MapConfiguration
            {
                EventProbability = 1.0f,
                MarkerRadius = 0.2f,
                MarkerDisplayName = "Gold Treasure",
                MarkerColor = "FFFF00",
                MarkerOutlineColor = "FFFF00",
                CustomStackSize = 10,
                ItemDisplayName = "Gold Map",
                ItemShortname = "xmas.present.large",
                SkinID = 3073616325,
                SpawnedPrefabChest = "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_c.prefab",
                SpawnedPrefabSkin = 0,
                MinItemPerChest = 1,
                MaxItemPerChest = 2,
                LootTable = "goldmap",
                LootTables = new Dictionary<string, List<LootTableItem>>
                {
                    {
                        "goldmap", new List<LootTableItem>
                        {
                            new LootTableItem
                            {
                                DisplayName = "Fire!!",
                                Shortname = "lmg.m249",
                                MinItemAmount = 1,
                                MaxItemAmount = 1,
                                Probability = 1.0F,
                                SkinID = 0,
                            }
                        }
                    }
                },
                Spawns = new List<MapSpawn>
                {
                    new MapSpawn
                    {
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                        SpawnChance = 0f
                    }
                }
            }
        },
                Coins = new List<CoinConfiguration>
        {
            new CoinConfiguration
            {
                CustomStackSize = 10,
                ItemDisplayName = "Gold Coin",
                ItemShortname = "easter.goldegg",
                SkinID = 3151241149,
                MinItemPerCoin = 1,
                MaxItemPerCoin = 10,
                LootTable = "goldcoin",
                LootTables = new Dictionary<string, List<CoinLootTableItem>>
                {
                    {
                        "goldcoin", new List<CoinLootTableItem>
                        {
                            new CoinLootTableItem
                            {
                                Economy = EconomyType.Item,
                                PluginName = "",
                                DisplayName = "Gold Coin",
                                Shortname = "easter.goldegg",
                                MinItemAmount = 1,
                                MaxItemAmount = 10,
                                Probability = 1.0F,
                                SkinID = 3151241149,
                            },
                            new CoinLootTableItem
                            {
                                Economy = EconomyType.Plugin,
                                PluginName = "Economics",
                                DisplayName = "Gold Coin",
                                Shortname = "easter.goldegg",
                                MinItemAmount = 50,
                                MaxItemAmount = 100,
                                Probability = 1.0F,
                                SkinID = 3151241149,
                            },
                            new CoinLootTableItem
                            {
                                Economy = EconomyType.Plugin,
                                PluginName = "ServerRewards",
                                DisplayName = "Gold Coin",
                                Shortname = "easter.goldegg",
                                MinItemAmount = 150,
                                MaxItemAmount = 200,
                                Probability = 1.0F,
                                SkinID = 3151241149,
                            },
                        }
                    }
                },
                Spawns = new List<MapSpawn>
                {
                    new MapSpawn
                    {
                        PrefabPath = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                        SpawnChance = 100f
                    }
                }
            }
        }
            };
        }

        private void Init()
        {
            Puts("Treasure Maps plugin loaded.");
            LoadConfig();
            permission.RegisterPermission("treasuremaps.give", this);

            if (!plugins.Exists("NpcSpawn"))
            {
                PrintError("NpcSpawn plugin doesn't exist! Please install it from the appropriate source.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
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

            if (config.Version < new VersionNumber(1, 1, 5)) 
            {
                foreach (var mapConfig in config.Maps)
                {
                    if (mapConfig.NPCSpawns == null)
                    {
                        PrintWarning($"NPCSpawns was null in configuration for map '{mapConfig.MarkerDisplayName}', initializing to an empty list.");
                        mapConfig.NPCSpawns = new List<NPCSpawn>();
                    }

                    foreach (var npcSpawn in mapConfig.NPCSpawns)
                    {
                        foreach (var beltItem in npcSpawn.BeltItems)
                        {
                            if (int.TryParse(beltItem.Ammo, out int ammoCount))
                            {
                                beltItem.Ammo = "ammo.rifle";

                                PrintWarning($"Converted ammo count ({ammoCount}) to default ammo type 'ammo.rifle' for NPC '{npcSpawn.EntityDisplayName}'.");
                            }
                        }
                    }
                }
                config.Version = Version;
                PrintWarning("Config update for Ammo type and NPCSpawns initialization completed!");
            }

            Config.WriteObject(config, true);
        }

        void OnServerInitialized()
        {
            _halfWorldSize = ConVar.Server.worldsize / 2;

            FindMonuments();

            spawnedEntities.RemoveAll(entity => entity == null || entity.IsDestroyed);
            foreach (var entity in spawnedEntities)
            {
                entity.Kill();
            }
            spawnedEntities.Clear();

            StartEventTimer();
        }

        private List<string> spawnedMarkers = new List<string>();

        void Unload()
        {
            foreach (var entity in spawnedEntities.ToList())
            {
                if (entity != null && !entity.IsDestroyed)
                {
                    entity.Kill();
                }
            }
            spawnedEntities.Clear();

            foreach (var markerName in spawnedMarkers.ToList())
            {
                RemoveMarker(markerName);
            }
            spawnedMarkers.Clear();
        }
        void OnEntityKill(BaseNetworkable networkable)
        {
            BaseEntity entity = networkable as BaseEntity;
            if (entity != null)
            {
                spawnedEntities.Remove(entity);
            }
        }
        public void AddToSpawnedEntities(BaseEntity entity)
        {
            if (entity != null && !entity.IsDestroyed)
            {
                spawnedEntities.Add(entity);
            }
        }

        #endregion
        private static Item GetMap(MapConfiguration config)
        {
            Item item = ItemManager.CreateByName("xmas.present.large", 1, config.SkinID);
            if (!string.IsNullOrEmpty(config.ItemDisplayName)) item.name = config.ItemDisplayName;
            return item;
        }

        private void GivePlayerItem(BasePlayer player, string shortname, int amount, ulong skinID, string displayName)
        {
            var itemDefinition = ItemManager.FindItemDefinition(shortname);
            if (itemDefinition == null)
            {
                PrintError($"Item shortname '{shortname}' not found!");
                return;
            }

            var mapConfig = config.Maps.FirstOrDefault(c => c.ItemShortname == shortname && c.SkinID == skinID);
            var coinConfig = config.Coins.FirstOrDefault(c => c.ItemShortname == shortname && c.SkinID == skinID);

            int customStackSize = (mapConfig != null) ? mapConfig.CustomStackSize : (coinConfig != null) ? coinConfig.CustomStackSize : itemDefinition.stackable;

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
        private void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null) return;

            List<MapConfiguration> possibleChests = config.Maps
                .Where(x => x.Spawns.Any(y => y.PrefabPath == container.PrefabName))
                .ToList();

            foreach (var chest in possibleChests)
            {
                MapSpawn crate = chest.Spawns.FirstOrDefault(x => x.PrefabPath == container.PrefabName);
                if (crate != null && UnityEngine.Random.Range(0f, 100f) <= crate.SpawnChance)
                {
                    if (container.inventory.itemList.Count == container.inventory.capacity)
                    {
                        container.inventory.capacity++;
                    }

                    Item item = GetMap(chest);
                    if (!item.MoveToContainer(container.inventory))
                    {
                        item.Remove();
                    }
                }
            }
            List<CoinConfiguration> possibleCoins = config.Coins
                .Where(x => x.Spawns.Any(y => y.PrefabPath == container.PrefabName))
                .ToList();

            foreach (var coin in possibleCoins)
            {
                MapSpawn spawn = coin.Spawns.FirstOrDefault(x => x.PrefabPath == container.PrefabName);
                if (spawn != null && UnityEngine.Random.Range(0f, 100f) <= spawn.SpawnChance)
                {
                    if (container.inventory.itemList.Count == container.inventory.capacity)
                    {
                        container.inventory.capacity++;
                    }

                    Item item = GetCoin(coin);
                    if (!item.MoveToContainer(container.inventory))
                    {
                        item.Remove();
                    }
                }
            }
        }
        private static Item GetCoin(CoinConfiguration config)
        {
            Item item = ItemManager.CreateByName("easter.goldegg", 1, config.SkinID);
            if (!string.IsNullOrEmpty(config.ItemDisplayName)) item.name = config.ItemDisplayName;
            return item;
        }
        private Dictionary<string, PlayerMapData> playerMapData = new Dictionary<string, PlayerMapData>();
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || string.IsNullOrEmpty(action) || player == null)
                return null;

            var mapConfig = config.Maps.Find(c => c.ItemShortname == item.info.shortname && c.SkinID == item.skin);
            if (mapConfig != null)
            {
                if (action == "unwrap")
                {
                    if (!playerMapData.ContainsKey(player.UserIDString))
                        playerMapData[player.UserIDString] = new PlayerMapData();

                    var data = playerMapData[player.UserIDString];

                    double timeSinceLastOpen = (DateTime.UtcNow - data.LastMapOpenTime).TotalSeconds;
                    if (timeSinceLastOpen < config.MapOpenCooldown && data.OpenedMapsCount >= config.MaxOpenedMaps)
                    {
                        int remainingCooldown = (int)(config.MapOpenCooldown - timeSinceLastOpen);
                        SendReply(player, $"You must wait {remainingCooldown} seconds before opening another map.");
                        return false;
                    }

                    if (timeSinceLastOpen >= config.MapOpenCooldown)
                    {
                        data.OpenedMapsCount = 0;
                        data.LastMapOpenTime = DateTime.UtcNow;
                    }

                    data.OpenedMapsCount++;
                    if (data.OpenedMapsCount >= config.MaxOpenedMaps)
                    {
                        data.LastMapOpenTime = DateTime.UtcNow;
                    }

                    ItemByPlayer[player] = item;
                    item.UseItem(1);

                    SpawnChest(player, config);

                    return true;
                }
            }

            var coinConfig = config.Coins.Find(c => c.ItemShortname == item.info.shortname && c.SkinID == item.skin);
            if (coinConfig != null)
            {
                if (action == "unwrap")
                {
                    if (!playerMapData.ContainsKey(player.UserIDString))
                        playerMapData[player.UserIDString] = new PlayerMapData();

                    var data = playerMapData[player.UserIDString];

                    double timeSinceLastOpen = (DateTime.UtcNow - data.LastCoinOpenTime).TotalSeconds;
                    if (timeSinceLastOpen < config.CoinOpenCooldown && data.OpenedCoinsCount >= config.MaxOpenedCoins)
                    {
                        int remainingCooldown = (int)(config.CoinOpenCooldown - timeSinceLastOpen);
                        SendReply(player, $"You must wait {remainingCooldown} seconds before opening another coin.");
                        return false;
                    }

                    if (timeSinceLastOpen >= config.CoinOpenCooldown)
                    {
                        data.OpenedCoinsCount = 0;
                        data.LastCoinOpenTime = DateTime.UtcNow;
                    }

                    data.OpenedCoinsCount++;
                    if (data.OpenedCoinsCount >= config.MaxOpenedCoins)
                    {
                        data.LastCoinOpenTime = DateTime.UtcNow;
                    }

                    ItemByPlayer[player] = item;
                    item.UseItem(1);

                    GiveCoinLoot(player, coinConfig);

                    return true;
                }
            }

            return null;
        }
        [ChatCommand("givemap")]
        private void GiveMapCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                SendReply(player, "Syntax: /givemap \"<display name>\" <quantity>");
                return;
            }

            string displayName = string.Join(" ", args.Take(args.Length - 1));
            int numLootBoxesToGive;

            if (!int.TryParse(args[args.Length - 1], out numLootBoxesToGive) || numLootBoxesToGive <= 0)
            {
                SendReply(player, "Invalid quantity. Please specify a positive number.");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, "treasuremaps.give"))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            var mapConfig = config.Maps.FirstOrDefault(c => c.ItemDisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            if (mapConfig == null)
            {
                SendReply(player, $"Invalid display name or configuration not found for '{displayName}'.");
                return;
            }

            for (int i = 0; i < numLootBoxesToGive;)
            {
                int stackSize = Mathf.Min(numLootBoxesToGive - i, mapConfig.CustomStackSize);
                GivePlayerItem(player, mapConfig.ItemShortname, stackSize, mapConfig.SkinID, mapConfig.ItemDisplayName);
                i += stackSize;
            }

            SendReply(player, $"You've received {numLootBoxesToGive} <color=green>{mapConfig.ItemDisplayName}</color> map!");
        }
        [ChatCommand("givemapto")]
        private void GiveMapToCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 3)
            {
                SendReply(player, "Syntax: /givemapto <player ID/name> \"<display name>\" <quantity>");
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

            if (!permission.UserHasPermission(player.UserIDString, "treasuremaps.give"))
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

            var mapConfig = config.Maps.FirstOrDefault(c => c.ItemDisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            if (mapConfig == null)
            {
                SendReply(player, $"Invalid display name or configuration not found for '{displayName}'.");
                return;
            }

            for (int i = 0; i < numLootBoxesToGive;)
            {
                int stackSize = Mathf.Min(numLootBoxesToGive - i, mapConfig.CustomStackSize);
                GivePlayerItem(targetPlayer, mapConfig.ItemShortname, stackSize, mapConfig.SkinID, mapConfig.ItemDisplayName);
                i += stackSize;
            }

            SendReply(targetPlayer, $"You've received {numLootBoxesToGive} <color=green>{mapConfig.ItemDisplayName}</color> map!");
            SendReply(player, $"You've given {numLootBoxesToGive} <color=green>{mapConfig.ItemDisplayName}</color> map to {targetPlayer.displayName}!");
        }
        [ConsoleCommand("givemapto")]
        private void GiveMapConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 3)
            {
                arg.ReplyWith("Syntax: givemapto <player ID/name> \"<display name>\" <quantity>");
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

            var mapConfig = config.Maps.FirstOrDefault(c => c.ItemDisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            if (mapConfig == null)
            {
                arg.ReplyWith($"Invalid display name or configuration not found for '{displayName}'.");
                return;
            }

            for (int i = 0; i < numLootBoxesToGive;)
            {
                int stackSize = Mathf.Min(numLootBoxesToGive - i, mapConfig.CustomStackSize);
                GivePlayerItem(targetPlayer, mapConfig.ItemShortname, stackSize, mapConfig.SkinID, mapConfig.ItemDisplayName);
                i += stackSize;
            }

            SendReply(targetPlayer, $"You've received {numLootBoxesToGive} <color=green>{mapConfig.ItemDisplayName}</color> map!");
            arg.ReplyWith($"You've given {numLootBoxesToGive} {mapConfig.ItemDisplayName} map to {targetPlayer.displayName}!");
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
        [ChatCommand("givecoin")]
        private void GiveCoinCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                SendReply(player, "Syntax: /givecoin \"<display name>\" <quantity>");
                return;
            }

            string displayName = string.Join(" ", args.Take(args.Length - 1));
            int numCoinsToGive;

            if (!int.TryParse(args[args.Length - 1], out numCoinsToGive) || numCoinsToGive <= 0)
            {
                SendReply(player, "Invalid quantity. Please specify a positive number.");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, "treasuremaps.give"))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            var coinConfig = config.Coins.FirstOrDefault(c => c.ItemDisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            if (coinConfig == null)
            {
                SendReply(player, $"Invalid display name or configuration not found for '{displayName}'.");
                return;
            }

            for (int i = 0; i < numCoinsToGive;)
            {
                int stackSize = Mathf.Min(numCoinsToGive - i, coinConfig.CustomStackSize);
                GivePlayerItem(player, coinConfig.ItemShortname, stackSize, coinConfig.SkinID, coinConfig.ItemDisplayName);
                i += stackSize;
            }

            SendReply(player, $"You've received {numCoinsToGive} <color=green>{coinConfig.ItemDisplayName}</color> coins!");
        }
        [ChatCommand("givecointo")]
        private void GiveCoinToCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 3)
            {
                SendReply(player, "Syntax: /givecointo <player ID/name> \"<display name>\" <quantity>");
                return;
            }

            string targetPlayerIdentifier = args[0];
            string displayName = string.Join(" ", args.Skip(1).Take(args.Length - 2));
            int numCoinsToGive;

            if (!int.TryParse(args[args.Length - 1], out numCoinsToGive) || numCoinsToGive <= 0)
            {
                SendReply(player, "Invalid quantity. Please specify a positive number.");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, "treasuremaps.give"))
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

            var coinConfig = config.Coins.FirstOrDefault(c => c.ItemDisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            if (coinConfig == null)
            {
                SendReply(player, $"Invalid display name or configuration not found for '{displayName}'.");
                return;
            }

            for (int i = 0; i < numCoinsToGive;)
            {
                int stackSize = Mathf.Min(numCoinsToGive - i, coinConfig.CustomStackSize);
                GivePlayerItem(targetPlayer, coinConfig.ItemShortname, stackSize, coinConfig.SkinID, coinConfig.ItemDisplayName);
                i += stackSize;
            }

            SendReply(targetPlayer, $"You've received {numCoinsToGive} <color=green>{coinConfig.ItemDisplayName}</color> coins!");
            SendReply(player, $"You've given {numCoinsToGive} <color=green>{coinConfig.ItemDisplayName}</color> coins to {targetPlayer.displayName}!");
        }

        [ConsoleCommand("givecointo")]
        private void GiveCoinConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 3)
            {
                arg.ReplyWith("Syntax: givecointo <player ID/name> \"<display name>\" <quantity>");
                return;
            }

            string targetPlayerIdentifier = arg.Args[0];
            string displayName = string.Join(" ", arg.Args.Skip(1).Take(arg.Args.Length - 2));
            int numCoinsToGive;

            if (!int.TryParse(arg.Args[arg.Args.Length - 1], out numCoinsToGive) || numCoinsToGive <= 0)
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

            var coinConfig = config.Coins.FirstOrDefault(c => c.ItemDisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            if (coinConfig == null)
            {
                arg.ReplyWith($"Invalid display name or configuration not found for '{displayName}'.");
                return;
            }

            for (int i = 0; i < numCoinsToGive;)
            {
                int stackSize = Mathf.Min(numCoinsToGive - i, coinConfig.CustomStackSize);
                GivePlayerItem(targetPlayer, coinConfig.ItemShortname, stackSize, coinConfig.SkinID, coinConfig.ItemDisplayName);
                i += stackSize;
            }

            SendReply(targetPlayer, $"You've received {numCoinsToGive} <color=green>{coinConfig.ItemDisplayName}</color> coins!");
            arg.ReplyWith($"You've given {numCoinsToGive} {coinConfig.ItemDisplayName} coins to {targetPlayer.displayName}!");
        }
        object OnMaxStackable(Item item)
        {
            if (item == null) return null;

            MapConfiguration mapConfig = config.Maps.FirstOrDefault(c =>
                c.ItemShortname == item.info.shortname &&
                c.SkinID == item.skin
            );

            if (mapConfig != null)
            {
                return mapConfig.CustomStackSize;
            }

            CoinConfiguration coinConfig = config.Coins.FirstOrDefault(c =>
                c.ItemShortname == item.info.shortname &&
                c.SkinID == item.skin
            );

            if (coinConfig != null)
            {
                return coinConfig.CustomStackSize;
            }

            return null;
        }
        private object CanStackItem(Item item, Item targetItem)
        {
            if (item == null || targetItem == null) return null;

            var mapConfig = config.Maps.FirstOrDefault(c => c.ItemShortname == item.info.shortname && c.SkinID == item.skin);
            if (mapConfig != null)
            {
                return null;
            }

            var coinConfig = config.Coins.FirstOrDefault(c => c.ItemShortname == item.info.shortname && c.SkinID == item.skin);
            if (coinConfig != null)
            {
                return null;
            }

            return null;
        }

        private object CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem targetItem)
        {
            if (droppedItem == null || targetItem == null) return null;

            var item = droppedItem.GetItem();
            if (item != null && (config.Maps.Any(c => c.ItemShortname == item.info.shortname && c.SkinID == item.skin) ||
                                 config.Coins.Any(c => c.ItemShortname == item.info.shortname && c.SkinID == item.skin)))
            {
                return false;
            }

            item = targetItem.GetItem();
            if (item != null && (config.Maps.Any(c => c.ItemShortname == item.info.shortname && c.SkinID == item.skin) ||
                                 config.Coins.Any(c => c.ItemShortname == item.info.shortname && c.SkinID == item.skin)))
            {
                return false;
            }

            return null;
        }
        private readonly Dictionary<BasePlayer, Item> ItemByPlayer = new Dictionary<BasePlayer, Item>();

        private List<BaseEntity> spawnedEntities = new List<BaseEntity>();
        private void SpawnChest(BasePlayer player, Configuration config)
        {
            Vector3 targetPos = GetSpawnLocation(player, config.LocalTreasureMaxDistance, true);

            var item = ItemByPlayer[player];
            var mapConfig = config.Maps.FirstOrDefault(m => m.ItemShortname == item.info.shortname && m.SkinID == item.skin);
            if (mapConfig == null) return;
            if (mapConfig != null && mapConfig.NPCSpawns != null)
            {
                foreach (var npcSpawn in mapConfig.NPCSpawns)
                {
                    SpawnNPCsAroundChest(targetPos, npcSpawn);
                }
            }
            if (mapConfig.SpawnBradley)
            {
                SpawnBradleyAPC(targetPos, mapConfig);
            }

            var chestEntity = GameManager.server.CreateEntity(mapConfig.SpawnedPrefabChest, targetPos) as StorageContainer;
            if (chestEntity == null) return;

            chestEntity.skinID = mapConfig.SpawnedPrefabSkin;
            chestEntity.Spawn();
            var chestTag = chestEntity.gameObject.AddComponent<TreasureChestTag>();
            chestTag.IsEventChest = false;
            spawnedEntities.Add(chestEntity);
            chestEntity.OwnerID = player.userID;
            var codeLock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", chestEntity.transform.position) as CodeLock;
            if (codeLock != null)
            {
                codeLock.Spawn();
                spawnedEntities.Add(codeLock);
                codeLock.SetParent(chestEntity);

                codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                codeLock.OwnerID = player.userID;
                codeLock.whitelistPlayers.Add(player.userID);

                chestEntity.SetSlot(BaseEntity.Slot.Lock, codeLock);
            }
            string markerName = "treasure_marker_" + chestEntity.net.ID;

            CreatePrivateMarker(
                targetPos,
                markerName,
                player.userID,
                (int)config.ChestDestroyTimer,
                0f,
                mapConfig.MarkerRadius,
                mapConfig.MarkerDisplayName,
                mapConfig.MarkerColor,
                mapConfig.MarkerOutlineColor
            );

            PopulateChestWithLoot(chestEntity, mapConfig);
            SendReply(player, $"[<color=green> TREASURE EVENT </color>] : Your treasure [<color=#{mapConfig.MarkerColor}> {mapConfig.MarkerDisplayName} </color>] has appeared on the map at <color=yellow> Grid : </color> " + GetGridLocation(targetPos));

            float destroyAfterSeconds = config.ChestDestroyTimer;
            timer.Once(destroyAfterSeconds, () =>
            {
                if (chestEntity != null && !chestEntity.IsDestroyed)
                {
                    chestEntity.Kill();
                    spawnedEntities.Remove(chestEntity);
                }
                
                RemoveMarker(markerName);
            });
        }
        private void CreatePrivateMarker(Vector3 position, string uname, ulong ownerid, int duration = 0, float refreshRate = 0f, float radius = 0.2f, string displayName = "Marker", string colorMarker = "00FFFF", string colorOutline = "00FFFFFF")
        {
            Interface.CallHook("API_CreateMarkerPrivate", position, uname, ownerid, duration, refreshRate, radius, displayName, colorMarker, colorOutline);
            spawnedMarkers.Add(uname);
        }
        private void RemoveMarker(string name)
        {
            Interface.CallHook("API_RemoveCachedMarker", name);
            if (spawnedMarkers.Contains(name))
            {
                spawnedMarkers.Remove(name);
            }
            else
            {
            }
        }

        private Vector3 GetRandomPosition(Vector3 center, float radius)
        {
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * radius;
            randomDirection += center;
            RaycastHit hit;
            if (Physics.Raycast(randomDirection + Vector3.up * 50, -Vector3.up, out hit, 100f, LayerMask.GetMask("Terrain", "Default")))
            {
                return hit.point;
            }
            return center;
        }

        private void SpawnNPCsAroundChest(Vector3 chestPosition, NPCSpawn npcSpawn)
        {
            for (int i = 0; i < npcSpawn.SpawnCount; i++)
            {
                Vector3 spawnPosition = GetRandomPosition(chestPosition, npcSpawn.SpawnRadius);

                while (IsInRockPrefab(spawnPosition))
                {
                    spawnPosition = GetRandomPosition(chestPosition, npcSpawn.SpawnRadius);
                }

                JObject configJson = GetNpcConfig(npcSpawn);

                var npc = (ScientistNPC)NpcSpawn.Call("SpawnNpc", spawnPosition, configJson);
                if (npc != null)
                {
                    spawnedEntities.Add(npc);
                    NpcSpawn.Call("AddTargetGuard", npc, chestPosition);

                    float destroyAfterSeconds = config.ChestDestroyTimer;
                    timer.Once(destroyAfterSeconds, () =>
                    {
                        if (npc != null && !npc.IsDestroyed)
                        {
                            npc.Kill();
                            spawnedEntities.Remove(npc);
                        }
                    });
                }
            }
        }

        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);

            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity, UnityEngine.LayerMask.GetMask("World", "Construction", "Default")))
                return Mathf.Max(hit.point.y, y);

            return y;
        }

        private Vector3 GetSpawnLocation(BasePlayer player, float maxDistance, bool extendSearch)
        {
            Vector3 targetPos = Vector3.zero;
            float currentMaxDistance = maxDistance;
            bool locationFound = false;

            while (!locationFound)
            {
                Vector3 randomizer = new Vector3(UnityEngine.Random.Range(-currentMaxDistance, currentMaxDistance), 0f, UnityEngine.Random.Range(-currentMaxDistance, currentMaxDistance));
                Vector3 potentialLocation = player.transform.position + randomizer;
                var groundY = GetGroundPosition(potentialLocation);

                if (groundY > 0.2f &&!IsPositionInWater(potentialLocation) && !IsPositionInSafeZone(potentialLocation) && !IsInRockPrefab(potentialLocation))
                {
                    targetPos = new Vector3(potentialLocation.x, groundY, potentialLocation.z);
                    locationFound = true;
                }
                else if (extendSearch)
                {
                    currentMaxDistance += 50;
                }
                else
                {
                    targetPos = player.transform.position;
                    break;
                }
            }

            return targetPos;
        }
        private static bool IsInRockPrefab(Vector3 position)
        {
            Physics.queriesHitBackfaces = true;

            bool flag = IsInside(position);

            Physics.queriesHitBackfaces = false;

            return flag;
        }

        private static bool IsInside(Vector3 point)
        {
            RaycastHit _hit;
            return Physics.Raycast(point, Vector3.up, out _hit, 30f, LayerMask.GetMask("World", "Default"), QueryTriggerInteraction.Ignore) && IsRock(_hit.collider.name);
        }

        private static bool IsRock(string name) => _prefabs.Exists(value => name.Contains(value, CompareOptions.OrdinalIgnoreCase));

        private static List<string> _prefabs = new List<string> { "rock", "formation", "cliff" };
        private bool IsPositionInSafeZone(Vector3 position)
        {
            foreach (var safeZone in safeZones)
            {
                if (Vector3.Distance(position, safeZone.transform.position) <= safeZone.Bounds.extents.magnitude)
                {
                    return true;
                }
            }
            return false;
        }
        bool IsPositionInWater(Vector3 position)
        {
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(position);
            float waterHeight = TerrainMeta.WaterMap.GetHeight(position);

            return waterHeight > terrainHeight;
        }
        private List<MonumentInfo> safeZones;
        private void FindMonuments()
        {
            safeZones = new List<MonumentInfo>();

            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument.name.Contains("compound") || monument.name.Contains("bandit"))
                {
                    safeZones.Add(monument);
                }
            }
        }
        string GetGridLocation(Vector3 location)
        {
            string gridLocation = "";
            int numx = Convert.ToInt32(location.x);
            int numz = Convert.ToInt32(location.z);

            float offset = (ConVar.Server.worldsize) / 2;
            float step = (ConVar.Server.worldsize) / (0.0066666666666667f * (ConVar.Server.worldsize));
            string start = "";

            int diff = Convert.ToInt32(step);
            int absoluteDifference = diff;

            char letter = 'A';
            int number = 0;
            for (float xx = -offset; xx < offset; xx += step)
            {
                for (float zz = offset; zz > -offset; zz -= step)
                {
                    if (Math.Abs(numx - xx) <= diff && Math.Abs(numz - zz) <= diff)
                    {
                        gridLocation = $"{start}{letter}{number}";
                        break;
                    }
                    number++;
                }
                number = 0;
                if (letter.ToString().ToUpper() == "Z")
                {
                    start = "A";
                    letter = 'A';
                }
                else
                {
                    letter = (char)(((int)letter) + 1);
                }
                if (Math.Abs(numx - xx) <= diff)
                {
                    break;
                }
            }
            return gridLocation;
        }

        private void PopulateChestWithLoot(StorageContainer chest, MapConfiguration mapConfig)
        {
            var lootTableItems = mapConfig.LootTables[mapConfig.LootTable];

            var guaranteedItems = lootTableItems
                .Where(lootItem => lootItem.Probability >= 1.0f)
                .ToList();

            var randomItems = lootTableItems
                .Where(lootItem => lootItem.Probability < 1.0f)
                .ToList();

            int numItemsToSpawn = UnityEngine.Random.Range(mapConfig.MinItemPerChest, mapConfig.MaxItemPerChest + 1);
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
                var amount = UnityEngine.Random.Range(lootItem.MinItemAmount, lootItem.MaxItemAmount + 1);

                Item item;
                if (lootItem.IsBlueprint)
                {
                    var itemDefinition = ItemManager.FindItemDefinition(lootItem.Shortname);
                    var blueprint = ItemManager.CreateByItemID(ItemManager.FindItemDefinition("blueprintbase").itemid, amount);
                    blueprint.blueprintTarget = itemDefinition.itemid;
                    item = blueprint;
                }
                else
                {
                    item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(lootItem.Shortname).itemid, amount, lootItem.SkinID);
                }

                if (item != null)
                {
                    string itemName = !string.IsNullOrEmpty(lootItem.DisplayName)
                        ? lootItem.DisplayName
                        : GetDefaultItemName(lootItem.Shortname);

                    item.name = itemName;
                    chest.inventory.Insert(item);
                }
            }

            chest.SendNetworkUpdateImmediate(false);
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
        private CoinLootTableItem GetWeightedRandomCoinItem(List<CoinLootTableItem> items)
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
        private void GiveCoinLoot(BasePlayer player, CoinConfiguration coinConfig)
        {
            var lootTableItems = coinConfig.LootTables[coinConfig.LootTable];
            if (lootTableItems == null)
            {
                PrintError($"Loot table '{coinConfig.LootTable}' not found!");
                return;
            }

            var guaranteedItems = lootTableItems
                .Where(lootItem => lootItem.Probability >= 1.0f)
                .ToList();

            var randomItems = lootTableItems
                .Where(lootItem => lootItem.Probability < 1.0f)
                .ToList();

            int numItemsToSpawn = UnityEngine.Random.Range(coinConfig.MinItemPerCoin, coinConfig.MaxItemPerCoin + 1);
            List<CoinLootTableItem> selectedItems = new List<CoinLootTableItem>();

            selectedItems.AddRange(guaranteedItems);

            if (selectedItems.Count > numItemsToSpawn)
            {
                selectedItems = selectedItems.Take(numItemsToSpawn).ToList();
            }

            while (selectedItems.Count < numItemsToSpawn)
            {
                var weightedRandomItem = GetWeightedRandomCoinItem(randomItems);
                if (weightedRandomItem == null) break;
                selectedItems.Add(weightedRandomItem);
                randomItems.Remove(weightedRandomItem);
            }

            foreach (var lootItem in selectedItems)
            {
                var randomAmountForItem = UnityEngine.Random.Range(lootItem.MinItemAmount, lootItem.MaxItemAmount + 1);

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
                        string itemName = !string.IsNullOrEmpty(lootItem.DisplayName)
                            ? lootItem.DisplayName
                            : GetDefaultItemName(lootItem.Shortname);

                        item.name = itemName;
                        if (!player.inventory.GiveItem(item, player.inventory.containerMain))
                        {
                            item.Drop(player.transform.position, player.transform.forward);
                        }
                    }
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
        private string GetDefaultItemName(string itemShortname)
        {
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemShortname);

            if (itemDefinition != null && !string.IsNullOrEmpty(itemDefinition.displayName.english))
            {
                return itemDefinition.displayName.english;
            }

            return itemShortname;
        }

        void OnLootEntity(BasePlayer looter, BaseEntity entity)
        {
            if (entity is StorageContainer container)
            {
                var isTreasureChest = container.gameObject.GetComponent<TreasureChestTag>() != null;

                if (isTreasureChest && container.OwnerID != 0 && looter.userID != container.OwnerID)
                {
                    looter.ChatMessage("You do not have permission to access this chest.");
                    return;
                }
            }
        }
        void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (container == null) return;

            var treasureTag = container.GetComponent<TreasureChestTag>();
            if (treasureTag != null)
            {

                if (container.inventory == null || container.inventory.itemList.Count == 0)
                {
                    string markerName = treasureTag.IsEventChest
                        ? "treasure_event_marker_" + container.net.ID
                        : "treasure_marker_" + container.net.ID;


                    container.Die();

                    if (spawnedMarkers.Contains(markerName))
                    {

                        RemoveMarker(markerName);
                    }
                    else
                    {
                    }
                }
            }
        }

        private static readonly string BradleyPrefabPath = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
        private void SpawnBradleyAPC(Vector3 spawnLocation, MapConfiguration mapConfig)
        {
            if (!mapConfig.SpawnBradley) return;

            var bradley = GameManager.server.CreateEntity(BradleyPrefabPath, spawnLocation, Quaternion.identity) as BradleyAPC;
            if (bradley == null) return;

            bradley.EnableSaving(false);
            int cratesToSpawn = UnityEngine.Random.Range(mapConfig.MinCratesToSpawn, mapConfig.MaxCratesToSpawn + 1);
            bradley.maxCratesToSpawn = cratesToSpawn;
            bradley.Spawn();
            bradley._maxHealth = mapConfig.BradleyHealth;
            bradley.health = mapConfig.BradleyHealth;
            spawnedEntities.Add(bradley);

            bradley.ClearPath();
            bradley.currentPath.Clear();
            bradley.currentPathIndex = 0;

            bradley.DoAI = true;
            bradley.DoSimpleAI();
        }
        [ConsoleCommand("mapsim")]
        private void SimulateChests(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Syntax: mapsim <lootTable> <number of chests>");
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

        [ConsoleCommand("mapsimr")]
        private void RconSimulateChests(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Syntax: mapsimr <lootTable> <number of chests>");
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
            var mapConfig = config.Maps.Find(c => c.LootTable == lootTable);
            if (mapConfig == null)
            {
                if (isRcon)
                    PrintError($"Maps config not found or not enabled for loot table '{lootTable}'.");
                else
                    player.ConsoleMessage($"Maps config not found or not enabled for loot table '{lootTable}'.");
                return;
            }

            var lootTableItems = mapConfig.LootTables[lootTable];
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
                var itemsInChest = GenerateLootForChest(mapConfig);

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

        private List<(string shortname, ulong skinID, int amount)> GenerateLootForChest(MapConfiguration mapConfig)
        {
            var lootTableItems = mapConfig.LootTables[mapConfig.LootTable];

            var guaranteedItems = lootTableItems
                .Where(lootItem => lootItem.Probability >= 1.0f)
                .ToList();

            var randomItems = lootTableItems
                .Where(lootItem => lootItem.Probability < 1.0f)
                .ToList();

            int numItemsToSpawn = UnityEngine.Random.Range(mapConfig.MinItemPerChest, mapConfig.MaxItemPerChest + 1);
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
                var amount = UnityEngine.Random.Range(lootItem.MinItemAmount, lootItem.MaxItemAmount + 1);
                items.Add((lootItem.Shortname, lootItem.SkinID, amount));
            }

            return items;
        }

        private void LogLootResultsToPlayer(BasePlayer player, Dictionary<(string shortname, ulong skinID), (int count, int totalAmount)> lootResults, int numChestsToSimulate, string lootTable)
        {
            var result = new System.Text.StringBuilder();
            result.AppendLine($"[TreasureMaps]");
            result.AppendLine($"Results of simulating {numChestsToSimulate} chests for loot table '{lootTable}':");
            result.AppendLine($"| {"Item",-41} | {"Count",-8} | {"Percent",-10} | {"Average",-10} | {"Total",-10} |");
            result.AppendLine($"|{"-".PadRight(43, '-')}|{"-".PadRight(10, '-')}|{"-".PadRight(12, '-')}|{"-".PadRight(12, '-')}|{"-".PadRight(12, '-')}|");

            foreach (var lootResult in lootResults.OrderByDescending(c => c.Value.count))
            {
                double percent = (double)lootResult.Value.count / numChestsToSimulate * 100;
                int average = lootResult.Value.totalAmount / lootResult.Value.count;

                var displayName = config.Maps
                    .SelectMany(map => map.LootTables.Values.SelectMany(items => items))
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
            result.AppendLine($"[TreasureMaps]");
            result.AppendLine($"Results of simulating {numChestsToSimulate} chests for loot table '{lootTable}':");
            result.AppendLine($"| {"Item",-41} | {"Count",-8} | {"Percent",-10} | {"Average",-10} | {"Total",-10} |");
            result.AppendLine($"|{"".PadRight(43, '-')}|{"".PadRight(10, '-')}|{"".PadRight(12, '-')}|{"".PadRight(12, '-')}|{"".PadRight(12, '-')}|");

            foreach (var lootResult in lootResults.OrderByDescending(c => c.Value.count))
            {
                double percent = (double)lootResult.Value.count / numChestsToSimulate * 100;
                int average = lootResult.Value.totalAmount / lootResult.Value.count;

                var displayName = config.Maps
                    .SelectMany(map => map.LootTables.Values.SelectMany(items => items))
                    .FirstOrDefault(item => item.Shortname == lootResult.Key.shortname && item.SkinID == lootResult.Key.skinID)?.DisplayName;

                string itemName = lootResult.Key.shortname;
                string displayNameText = !string.IsNullOrEmpty(displayName) ? $"# {displayName}" : string.Empty;

                result.AppendLine($"| {displayNameText,-20} {itemName,-20} | {lootResult.Value.count,-8} | {percent,-10:F2} | {average,-10} | {lootResult.Value.totalAmount,-10} |");
            }

            Puts(result.ToString());
        }


        class TreasureChestTag : MonoBehaviour
        {
            public bool IsEventChest { get; set; } 
        }

        private bool maxEventsReachedNotified = false; 

        private void StartEventTimer()
        {
            if (config.AutomaticEventEnabled)
            {
                ScheduleNextEvent();
            }
        }

        private void ScheduleNextEvent()
        {
            if (GetActiveEventCount() >= config.MaxConcurrentEvents)
            {
                if (!maxEventsReachedNotified)
                {
                    PrintToChat("[<color=green> TREASURE EVENT </color>] : Maximum number of active events reached. One needs to be destroyed to spawn a new one!");
                    maxEventsReachedNotified = true;
                }

                timer.Once(config.MinEventTimer, () => ScheduleNextEvent());
                return;
            }

            maxEventsReachedNotified = false;

            float eventDelay = UnityEngine.Random.Range(config.MinEventTimer, config.MaxEventTimer);

            PrintToChat($"[<color=green> TREASURE EVENT </color>] : Next Treasure event will start in <color=yellow>{Mathf.RoundToInt(eventDelay)}</color> seconds!");

            timer.Once(eventDelay, () => TriggerEvent());
        }

        private int GetActiveEventCount()
        {
            return spawnedEntities.Count(entity => entity.GetComponent<TreasureChestTag>() != null);
        }

        private void TriggerEvent()
        {
            var selectedMap = GetRandomEventMap();
            if (selectedMap != null)
            {
                SpawnChestForEvent(selectedMap);
            }

            ScheduleNextEvent();
        }

        private MapConfiguration GetRandomEventMap()
        {
            var eligibleMaps = config.Maps.Where(m => m.EventProbability > 0).ToList();
            if (!eligibleMaps.Any()) return null;

            float totalProbability = eligibleMaps.Sum(m => m.EventProbability);
            float randomPoint = UnityEngine.Random.value * totalProbability;

            foreach (var map in eligibleMaps)
            {
                if (randomPoint < map.EventProbability)
                {
                    return map;
                }
                randomPoint -= map.EventProbability;
            }

            return null;
        }
        private void SpawnChestForEvent(MapConfiguration mapConfig)
        {
            Vector3? eventPosition = TryFindPosition();

            if (eventPosition == null)
            {
                Puts("Failed to find a valid position for the event!");
                return;
            }

            Vector3 targetPos = eventPosition.Value;

            if (mapConfig != null && mapConfig.NPCSpawns != null)
            {
                foreach (var npcSpawn in mapConfig.NPCSpawns)
                {
                    SpawnNPCsAroundChest(targetPos, npcSpawn);
                }
            }
            if (mapConfig.SpawnBradley)
            {
                SpawnBradleyAPC(targetPos, mapConfig);
            }

            var chestEntity = GameManager.server.CreateEntity(mapConfig.SpawnedPrefabChest, targetPos) as StorageContainer;
            if (chestEntity == null) return;

            chestEntity.skinID = mapConfig.SpawnedPrefabSkin;
            chestEntity.Spawn();
            var chestTag = chestEntity.gameObject.AddComponent<TreasureChestTag>();
            chestTag.IsEventChest = true;  
            spawnedEntities.Add(chestEntity);

            string markerName = "treasure_event_marker_" + chestEntity.net.ID;

            CreatePublicMarker(
                targetPos,
                markerName,
                (int)config.ChestDestroyTimer,
                0f,
                mapConfig.MarkerRadius,
                mapConfig.MarkerDisplayName,
                mapConfig.MarkerColor,
                mapConfig.MarkerOutlineColor
            );

            PopulateChestWithLoot(chestEntity, mapConfig);

            float destroyAfterSeconds = config.ChestDestroyTimer;
            timer.Once(destroyAfterSeconds, () =>
            {

                if (chestEntity != null && !chestEntity.IsDestroyed)
                {
                    chestEntity.Kill();
                    spawnedEntities.Remove(chestEntity);
                }

                RemoveMarker(markerName);
            });

            PrintToChat($"[<color=green> TREASURE EVENT </color>] : The treasure [<color=#{mapConfig.MarkerColor}> {mapConfig.MarkerDisplayName} </color>] has appeared on the map at <color=yellow> Grid : </color> " + GetGridLocation(targetPos));
        }
        private void CreatePublicMarker(Vector3 position, string uname, int duration = 0, float refreshRate = 0f, float radius = 0.2f, string displayName = "Marker", string colorMarker = "00FFFF", string colorOutline = "00FFFFFF")
        {
            Interface.CallHook("API_CreateMarkerPublic", position, uname, duration, refreshRate, radius, displayName, colorMarker, colorOutline);
            spawnedMarkers.Add(uname);
        }
        private Vector3? TryFindPosition()
        {
            for (int i = 0; i < 10; i++) 
            {
                Vector3 randomPosition = new Vector3(GetRandomPosition(), 0, GetRandomPosition());

                randomPosition.y = TerrainMeta.HeightMap.GetHeight(randomPosition);

                if (randomPosition.y < 0.2f || IsPositionInWater(randomPosition) || IsPositionInSafeZone(randomPosition) || IsInRockPrefab(randomPosition))
                {
                    continue;
                }

                return randomPosition;
            }

            return null; 
        }
        private int GetRandomPosition() => UnityEngine.Random.Range(-_halfWorldSize, _halfWorldSize);


    }
}
