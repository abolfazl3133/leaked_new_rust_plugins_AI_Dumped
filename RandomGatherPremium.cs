using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Random Gather Premium", "Kaysharp", "1.0.12")]
    [Description("Get random items on gathering resources, quarry gather, Excavator gather and loot containers")]
    class RandomGatherPremium : CovalencePlugin
    {
        private readonly Random _random = new Random();
        public static ConfigData config { get; set; }

        public List<string> ContainterBlacklist = new List<string>
        {
            "crate_tools",
            "crate_mine",
            "crate_normal_2_food",
            "crate_normal_2_medical",
            "crate_ammunition",
            "crate_food_1",
            "crate_food_2",
            "crate_fuel",
            "crate_medical",
            "heli_crate",
            "survey_crater",
            "survey_crater_oil"
        };

        private void Init()
        {
            if (!config.OnCollectiblePickup)
                Unsubscribe(nameof(OnCollectiblePickup));

            if (!config.isOnDispenserBonus)
                Unsubscribe(nameof(OnDispenserBonus));

            if (!config.isOnDispenserGather)
                Unsubscribe(nameof(OnDispenserGather));

            if (!config.quarryLootTables.isOnQuarryGather)
                Unsubscribe(nameof(OnQuarryGather));

            if (!config.containersLootTables.isOnLootEntity)
                Unsubscribe(nameof(OnLootSpawn));

            if (!config.ExcavatorLootTables.isOnExcavatorGather)
                Unsubscribe(nameof(OnExcavatorGather));

            if (!config.isOnEntityDeath)
                Unsubscribe(nameof(OnEntityDeath));

            if (!config.airDropLootTable.isOnSupplyDropDropped)
                Unsubscribe(nameof(OnSupplyDropDropped));
            if (!config.NPCLootTable.isOnCorpsePopulate)
                Unsubscribe(nameof(OnCorpsePopulate));
        }

        private void OnServerInitialized()
        {
            if (config.containersLootTables.isOnLootEntity)
                RepopulateContainers();
        }

        private ItemDefinition FindItem(string itemNameOrId)
        {
            ItemDefinition itemDef = ItemManager.FindItemDefinition(itemNameOrId.ToLower());
            if (itemDef == null)
            {
                int itemId;
                if (int.TryParse(itemNameOrId, out itemId))
                {
                    itemDef = ItemManager.FindItemDefinition(itemId);
                }
            }

            return itemDef;
        }

        #region Oxide Hooks

        private void Unload()
        {
            config = null;
        }

        [ChatCommand("tt")]
        private void cmdTest(BasePlayer player, string command, string[] args)
        {
            var item = ItemManager.Create(FindItem("ammo.pistol.fire"));
            player.GiveItem(item);
        }

        private void RepopulateContainers()
        {
            int count = 0;
            foreach (var container in BaseNetworkable.serverEntities.OfType<LootContainer>())
            {
                if (container == null || !container.ShortPrefabName.Contains("crate") ||
                    ContainterBlacklist.Contains(container.ShortPrefabName)) continue;
                int Slots = 0;
                foreach (var LootSpawnSlot in container.LootSpawnSlots)
                {
                    Slots += LootSpawnSlot.numberToSpawn;
                }

                container.inventory.itemList.Clear();
                for (int i = 0; i < Slots; i++)
                {
                    var index = _random.Next(0, config.containersLootTables.LootTable.Count());
                    if (ItemManager.itemDictionaryByName.ContainsKey(config.containersLootTables.LootTable
                            .ElementAt(index).Key))
                    {
                        var randomItem =
                            ItemManager.itemDictionaryByName[
                                config.containersLootTables.LootTable.ElementAt(index).Key];
                        if (randomItem != null)
                        {
                            container.inventory.AddItem(randomItem,
                                config.containersLootTables.LootTable.ElementAt(index).Value);
                        }
                    }
                }

                count++;
            }

            Debug.Log("Repopulated " + count + " loot containers.");
        }

        BaseCorpse OnCorpsePopulate(BasePlayer npcPlayer, BaseCorpse corpse)
        {
            var npcplayerCorpse = corpse.GetComponent<NPCPlayerCorpse>();
            var humanNPC = npcPlayer as HumanNPC;
            if (humanNPC != null)
                foreach (global::LootContainer.LootSpawnSlot lootSpawnSlot in humanNPC.LootSpawnSlots)
                {
                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                    {
                        var index = _random.Next(0, config.NPCLootTable.LootTable.Count());
                        if (ItemManager.itemDictionaryByName.ContainsKey(config.NPCLootTable.LootTable.ElementAt(index)
                                .Key))
                        {
                            var randomItem =
                                ItemManager.itemDictionaryByName[config.NPCLootTable.LootTable.ElementAt(index).Key];
                            if (randomItem != null)
                            {
                                npcplayerCorpse.containers[0].AddItem(randomItem,
                                    config.NPCLootTable.LootTable.ElementAt(index).Value);
                            }
                        }
                    }
                }
            else
            {
                var _ScarecrowNPC = npcPlayer as ScarecrowNPC;
                foreach (global::LootContainer.LootSpawnSlot lootSpawnSlot in _ScarecrowNPC.LootSpawnSlots)
                {
                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                    {
                        var index = _random.Next(0, config.NPCLootTable.LootTable.Count());
                        if (ItemManager.itemDictionaryByName.ContainsKey(config.NPCLootTable.LootTable.ElementAt(index)
                                .Key))
                        {
                            var randomItem =
                                ItemManager.itemDictionaryByName[config.NPCLootTable.LootTable.ElementAt(index).Key];
                            if (randomItem != null)
                            {
                                npcplayerCorpse.containers[0].AddItem(randomItem,
                                    config.NPCLootTable.LootTable.ElementAt(index).Value);
                            }
                        }
                    }
                }
            }


            return corpse;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var container = entity as LootContainer;
            if (container == null || info == null || !container.ShortPrefabName.Contains("barrel")) return;

            var items = Facepunch.Pool.GetList<Item>();
            for (int i = 0; i < config.NumbOfItemsInBarrels; i++)
            {
                var index = _random.Next(0, config.Items.Count());
                var itemName = config.Items[index];
                var item = ItemManager.Create(FindItem(itemName));
                if (item != null)
                {
                    int amount = 1;
                    if (config.Amount.TryGetValue(itemName, out amount))
                    {
                        item.amount = amount;
                    }

                    item.Drop(entity.GetDropPosition() + UnityEngine.Vector3.up, entity.GetDropVelocity());
                }
            }

            container.inventory.itemList.Clear();
            Facepunch.Pool.FreeList(ref items);
        }

        object OnExcavatorGather(ExcavatorArm excavator, Item item)
        {
            var index = _random.Next(0, config.ExcavatorLootTables.LootTable.Count());
            var itemName = config.ExcavatorLootTables.LootTable.ElementAt(index).Key;

            foreach (ExcavatorOutputPile excavatorOutputPile in excavator.outputPiles)
            {
                item = ItemManager.Create(FindItem(itemName));
                if (item != null)
                {
                    item.amount = config.ExcavatorLootTables.LootTable.ElementAt(index).Value;
                    if (!item.MoveToContainer(excavatorOutputPile.inventory, -1, true, false))
                    {
                        item.Drop(excavatorOutputPile.GetDropPosition(), excavatorOutputPile.GetDropVelocity(),
                            default(Quaternion));
                    }
                }
            }

            return true;
        }

        object OnQuarryGather(MiningQuarry quarry, Item item)
        {
            var index = _random.Next(0, config.quarryLootTables.LootTable.Count());
            var itemName = config.quarryLootTables.LootTable.ElementAt(index).Key;
            item = ItemManager.Create(FindItem(itemName));
            if (item != null)
            {
                item.amount = config.NPCLootTable.LootTable.ElementAt(index).Value;
            }

            if (!item.MoveToContainer(quarry.hopperPrefab.instance.GetComponent<global::StorageContainer>().inventory,
                    -1, true, false))
            {
                item.Remove(0f);
                quarry.SetOn(false);
            }

            return true;
        }

        private object OnLootSpawn(LootContainer container)
        {
            if (container == null || !container.ShortPrefabName.Contains("crate") ||
                ContainterBlacklist.Contains(container.ShortPrefabName)) return null;
            int Slots = 0;
            foreach (var LootSpawnSlot in container.LootSpawnSlots)
            {
                Slots += LootSpawnSlot.numberToSpawn;
            }

            container.inventory.itemList.Clear();
            for (int i = 0; i < Slots; i++)
            {
                var index = _random.Next(0, config.containersLootTables.LootTable.Count());
                if (ItemManager.itemDictionaryByName.ContainsKey(config.containersLootTables.LootTable.ElementAt(index)
                        .Key))
                {
                    var randomItem =
                        ItemManager.itemDictionaryByName[config.containersLootTables.LootTable.ElementAt(index).Key];
                    if (randomItem != null)
                    {
                        container.inventory.AddItem(randomItem,
                            config.containersLootTables.LootTable.ElementAt(index).Value);
                    }
                }
            }

            return container;
        }

        void OnSupplyDropDropped(SupplyDrop SupplyDrop, CargoPlane plane)
        {
            var container = SupplyDrop as LootContainer;
            if (container != null)
            {
                int Slots = 0;
                foreach (var LootSpawnSlot in container.LootSpawnSlots)
                {
                    Slots += LootSpawnSlot.numberToSpawn;
                }

                container.inventory.itemList.Clear();
                for (int i = 0; i < Slots; i++)
                {
                    var index = _random.Next(0, config.airDropLootTable.LootTable.Count());
                    if (ItemManager.itemDictionaryByName.ContainsKey(config.airDropLootTable.LootTable.ElementAt(index)
                            .Key))
                    {
                        var randomItem =
                            ItemManager.itemDictionaryByName[config.airDropLootTable.LootTable.ElementAt(index).Key];
                        if (randomItem != null)
                        {
                            container.inventory.AddItem(randomItem,
                                config.airDropLootTable.LootTable.ElementAt(index).Value);
                        }
                    }
                }
            }
        }

        object OnCollectiblePickup(CollectibleEntity entity, BasePlayer player)
        {
            foreach (var itemPicked in entity.itemList)
            {
                var item = ItemManager.Create(itemPicked.itemDef, (int)itemPicked.amount, 0UL);
                if (item.info.displayName.english.ToLower().Contains("seed"))
                    return null;
                var InitItem = item;
                var index = _random.Next(0, config.Items.Count());
                var itemName = config.Items[index];
                item = ItemManager.Create(FindItem(itemName));
                if (item != null)
                {
                    int amount;
                    if (config.Amount.TryGetValue(itemName, out amount))
                    {
                        item.amount = amount;
                    }

                    try
                    {
                        player.GiveItem(item);
                    }
                    catch (Exception E)
                    {
                        item.Drop(player.GetDropPosition(), player.GetDropVelocity());
                    }

                    try
                    {
                        player.GiveItem(InitItem, global::BaseEntity.GiveItemReason.ResourceHarvested);
                    }
                    catch
                    {
                        InitItem.Drop(player.GetDropPosition(), player.GetDropVelocity());
                    }

                    if (entity.pickupEffect.isValid)
                    {
                        Effect.server.Run(entity.pickupEffect.resourcePath, entity.transform.position,
                            entity.transform.up, null, false);
                    }

                    RandomItemDispenser randomItemDispenser =
                        PrefabAttribute.server.Find<RandomItemDispenser>(entity.prefabID);
                    if (randomItemDispenser != null)
                    {
                        randomItemDispenser.DistributeItems(player, entity.transform.position);
                    }

                    entity.Kill(global::BaseNetworkable.DestroyMode.None);
                    return true;
                }
            }

            return null;
        }

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            var index = _random.Next(0, config.Items.Count());
            var itemName = config.Items[index];
            item = ItemManager.Create(FindItem(itemName));
            if (item != null)
            {
                int amount;
                if (config.Amount.TryGetValue(itemName, out amount))
                {
                    item.amount = amount;
                }

                return item;
            }

            return null;
        }

        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            var index = _random.Next(0, config.Items.Count());
            var itemName = config.Items[index];
            item = ItemManager.Create(FindItem(itemName));

            bool hasActiveModifier = false;
            float Value = 0f;
            string GatherType = dispenser.gatherType.ToString();
            if (player.modifiers.ActiveModifierCoount > 0 && config.isBoost)
            {
                var Modifiers = Facepunch.Pool.GetList<Modifier>();
                Modifiers = player.modifiers.All;
                if (GatherType.Contains("Tree"))
                    GatherType = "Wood";
                foreach (var Modifier in Modifiers)
                {
                    if (Modifier.Type.ToString().Contains(GatherType))
                    {
                        hasActiveModifier = true;
                        Value = 1 + Modifier.Value;
                        break;
                    }
                }
            }

            if (item != null)
            {
                int amount;
                if (config.Amount.TryGetValue(itemName, out amount))
                {
                    if (Value > 0 && hasActiveModifier && config.isBoost)
                        item.amount = (int)(amount * Value);
                    else
                        item.amount = amount;
                }
                else
                {
                    if (hasActiveModifier && config.isBoost)
                        item.amount = (int)(item.amount * Value);
                }

                try
                {
                    player.GiveItem(item);
                }
                catch
                {
                    item.Drop(player.GetDropPosition(), player.GetDropVelocity());
                }

                return true;
            }

            return null;
        }

        #endregion

        #region Config

        public class ConfigData
        {
            [JsonProperty(PropertyName = "Give bonuses if player have active boost")]
            public bool isBoost;

            [JsonProperty(PropertyName = "How many items should spawn when barrel is destroyed?")]
            public int NumbOfItemsInBarrels;

            [JsonProperty(PropertyName = "Give random items when barrel is destroyed")]
            public bool isOnEntityDeath { get; set; }

            [JsonProperty(PropertyName = "Give random items on gathering resources")]
            public bool isOnDispenserGather { get; set; }

            [JsonProperty(PropertyName = "Give random items only on full gather")]
            public bool isOnDispenserBonus { get; set; }

            [JsonProperty(PropertyName = "Give random item when collecting resources")]
            public bool OnCollectiblePickup { get; set; }

            [JsonProperty(PropertyName = "Give random items on quarry gather")]
            public QuarryLootTables quarryLootTables = new QuarryLootTables();

            [JsonProperty(PropertyName = "Give random items on Excavator gather")]
            public ExcavatorLootTables ExcavatorLootTables = new ExcavatorLootTables();

            [JsonProperty(PropertyName = "Fill loot containers with random items")]
            public ContainersLootTables containersLootTables = new ContainersLootTables();

            [JsonProperty(PropertyName = "Fill airdrops with random items")]
            public AirDropLootTable airDropLootTable = new AirDropLootTable();

            [JsonProperty(PropertyName = "Populate NPC corps with random loot")]
            public NPCLootTable NPCLootTable = new NPCLootTable();

            [JsonProperty(PropertyName = "Items")] public List<string> Items = new List<string>();

            [JsonProperty(PropertyName = "Amount")]
            public Dictionary<string, int> Amount = new Dictionary<string, int>();
        }

        public class QuarryLootTables
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool isOnQuarryGather { get; set; }

            [JsonProperty(PropertyName = "Loot table (Item name , amount)")]
            public Dictionary<string, int> LootTable = new Dictionary<string, int>();
        }

        public class ExcavatorLootTables
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool isOnExcavatorGather { get; set; }

            [JsonProperty(PropertyName = "Loot table (Item name , amount)")]
            public Dictionary<string, int> LootTable = new Dictionary<string, int>();
        }

        public class ContainersLootTables
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool isOnLootEntity { get; set; }

            [JsonProperty(PropertyName = "Loot table (Item name , amount)")]
            public Dictionary<string, int> LootTable = new Dictionary<string, int>();
        }

        public class AirDropLootTable
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool isOnSupplyDropDropped { get; set; }

            [JsonProperty(PropertyName = "Loot table (Item name , amount)")]
            public Dictionary<string, int> LootTable = new Dictionary<string, int>();
        }

        public class NPCLootTable
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool isOnCorpsePopulate { get; set; }

            [JsonProperty(PropertyName = "Loot table (Item name , amount)")]
            public Dictionary<string, int> LootTable = new Dictionary<string, int>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) throw new Exception();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = GetDefaultSettings();

        private ConfigData GetDefaultSettings()
        {
            return new ConfigData
            {
                isBoost = true,
                NumbOfItemsInBarrels = 2,
                airDropLootTable = new AirDropLootTable
                {
                    isOnSupplyDropDropped = true,
                    LootTable = new Dictionary<string, int>
                    {
                        { "ammo.grenadelauncher.buckshot", 10 },
                        { "ammo.grenadelauncher.he", 1 },
                        { "ammo.grenadelauncher.smoke", 1 },
                        { "ammo.handmade.shell", 15 },
                        { "ammo.nailgun.nails", 30 },
                        { "ammo.pistol", 30 },
                        { "ammo.pistol.fire", 10 },
                        { "ammo.pistol.hv", 10 },
                        { "ammo.rifle", 30 },
                        { "ammo.rifle.explosive", 10 },
                        { "ammo.rifle.hv", 15 },
                        { "ammo.rifle.incendiary", 15 },
                        { "ammo.rocket.basic", 1 },
                        { "ammo.rocket.fire", 1 },
                        { "ammo.rocket.hv", 1 },
                        { "ammo.rocket.sam", 5 },
                        { "ammo.rocket.smoke", 1 },
                        { "ammo.shotgun", 10 },
                        { "ammo.shotgun.fire", 10 },
                        { "ammo.shotgun.slug", 10 }
                    }
                },
                isOnEntityDeath = true,
                ExcavatorLootTables = new ExcavatorLootTables
                {
                    isOnExcavatorGather = true,
                    LootTable = new Dictionary<string, int>
                    {
                        { "ammo.grenadelauncher.buckshot", 10 },
                        { "ammo.grenadelauncher.he", 1 },
                        { "ammo.grenadelauncher.smoke", 1 },
                        { "ammo.handmade.shell", 15 },
                        { "ammo.nailgun.nails", 30 },
                        { "ammo.pistol", 30 },
                        { "ammo.pistol.fire", 10 },
                        { "ammo.pistol.hv", 10 },
                        { "ammo.rifle", 30 },
                        { "ammo.rifle.explosive", 10 },
                        { "ammo.rifle.hv", 15 },
                        { "ammo.rifle.incendiary", 15 },
                        { "ammo.rocket.basic", 1 },
                        { "ammo.rocket.fire", 1 },
                        { "ammo.rocket.hv", 1 },
                        { "ammo.rocket.sam", 5 },
                        { "ammo.rocket.smoke", 1 },
                        { "ammo.shotgun", 10 },
                        { "ammo.shotgun.fire", 10 },
                        { "ammo.shotgun.slug", 10 }
                    }
                },
                containersLootTables = new ContainersLootTables
                {
                    isOnLootEntity = true,
                    LootTable = new Dictionary<string, int>
                    {
                        { "ammo.grenadelauncher.buckshot", 10 },
                        { "ammo.grenadelauncher.he", 1 },
                        { "ammo.grenadelauncher.smoke", 1 },
                        { "ammo.handmade.shell", 15 },
                        { "ammo.nailgun.nails", 30 },
                        { "ammo.pistol", 30 },
                        { "ammo.pistol.fire", 10 },
                        { "ammo.pistol.hv", 10 },
                        { "ammo.rifle", 30 },
                        { "ammo.rifle.explosive", 10 },
                        { "ammo.rifle.hv", 15 },
                        { "ammo.rifle.incendiary", 15 },
                        { "ammo.rocket.basic", 1 },
                        { "ammo.rocket.fire", 1 },
                        { "ammo.rocket.hv", 1 },
                        { "ammo.rocket.sam", 5 },
                        { "ammo.rocket.smoke", 1 },
                        { "ammo.shotgun", 10 },
                        { "ammo.shotgun.fire", 10 },
                        { "ammo.shotgun.slug", 10 }
                    }
                },
                quarryLootTables = new QuarryLootTables
                {
                    isOnQuarryGather = true,
                    LootTable = new Dictionary<string, int>
                    {
                        { "ammo.grenadelauncher.buckshot", 10 },
                        { "ammo.grenadelauncher.he", 1 },
                        { "ammo.grenadelauncher.smoke", 1 },
                        { "ammo.handmade.shell", 15 },
                        { "ammo.nailgun.nails", 30 },
                        { "ammo.pistol", 30 },
                        { "ammo.pistol.fire", 10 },
                        { "ammo.pistol.hv", 10 },
                        { "ammo.rifle", 30 },
                        { "ammo.rifle.explosive", 10 },
                        { "ammo.rifle.hv", 15 },
                        { "ammo.rifle.incendiary", 15 },
                        { "ammo.rocket.basic", 1 },
                        { "ammo.rocket.fire", 1 },
                        { "ammo.rocket.hv", 1 },
                        { "ammo.rocket.sam", 5 },
                        { "ammo.rocket.smoke", 1 },
                        { "ammo.shotgun", 10 },
                        { "ammo.shotgun.fire", 10 },
                        { "ammo.shotgun.slug", 10 }
                    }
                },
                OnCollectiblePickup = true,
                isOnDispenserGather = true,
                isOnDispenserBonus = false,
                NPCLootTable = new NPCLootTable
                {
                    isOnCorpsePopulate = true,
                    LootTable = new Dictionary<string, int>
                    {
                        { "ammo.grenadelauncher.buckshot", 10 },
                        { "ammo.grenadelauncher.he", 1 },
                        { "ammo.grenadelauncher.smoke", 1 },
                        { "ammo.handmade.shell", 15 },
                        { "ammo.nailgun.nails", 30 },
                        { "ammo.pistol", 30 },
                        { "ammo.pistol.fire", 10 },
                        { "ammo.pistol.hv", 10 },
                        { "ammo.rifle", 30 },
                        { "ammo.rifle.explosive", 10 },
                        { "ammo.rifle.hv", 15 },
                        { "ammo.rifle.incendiary", 15 },
                        { "ammo.rocket.basic", 1 },
                        { "ammo.rocket.fire", 1 },
                        { "ammo.rocket.hv", 1 },
                        { "ammo.rocket.sam", 5 },
                        { "ammo.rocket.smoke", 1 },
                        { "ammo.shotgun", 10 },
                        { "ammo.shotgun.fire", 10 },
                        { "ammo.shotgun.slug", 10 }
                    }
                },
                Items = new List<string>
                {
                    "ammo.grenadelauncher.buckshot",
                    "ammo.grenadelauncher.he",
                    "ammo.grenadelauncher.smoke",
                    "ammo.handmade.shell",
                    "ammo.nailgun.nails",
                    "ammo.pistol",
                    "ammo.pistol.fire",
                    "ammo.pistol.hv",
                    "ammo.rifle",
                    "ammo.rifle.explosive",
                    "ammo.rifle.hv",
                    "ammo.rifle.incendiary",
                    "ammo.rocket.basic",
                    "ammo.rocket.fire",
                    "ammo.rocket.hv",
                    "ammo.rocket.sam",
                    "ammo.rocket.smoke",
                    "ammo.shotgun",
                    "ammo.shotgun.fire",
                    "ammo.shotgun.slug",
                    "apple",
                    "arcade.machine.chippy",
                    "arrow.bone",
                    "arrow.fire",
                    "arrow.hv",
                    "arrow.wooden",
                    "attire.bunnyears",
                    "attire.hide.boots",
                    "attire.hide.helterneck",
                    "attire.hide.pants",
                    "attire.hide.poncho",
                    "attire.hide.skirt",
                    "attire.hide.vest",
                    "attire.ninja.suit",
                    "autoturret",
                    "axe.salvaged",
                    "barricade.concrete",
                    "barricade.metal",
                    "barricade.sandbags",
                    "barricade.stone",
                    "barricade.wood.cover",
                    "battery.small",
                    "bbq",
                    "bed",
                    "black.berry",
                    "black.raspberries",
                    "blue.berry",
                    "blueberries",
                    "bone.armor.suit",
                    "boots.frog",
                    "bow.compound",
                    "bow.hunting",
                    "box.repair.bench",
                    "box.wooden",
                    "box.wooden.large",
                    "bucket.helmet",
                    "building.planner",
                    "burlap.gloves",
                    "burlap.gloves.new",
                    "burlap.headwrap",
                    "burlap.shirt",
                    "burlap.shoes",
                    "burlap.trousers",
                    "can.beans",
                    "can.tuna",
                    "cctv.camera",
                    "ceilinglight",
                    "chainsaw",
                    "chocholate",
                    "clatter.helmet",
                    "coffeecan.helmet",
                    "coffin.storage",
                    "computerstation",
                    "corn",
                    "crossbow",
                    "crude.oil",
                    "cupboard.tool",
                    "cursedcauldron",
                    "diving.fins",
                    "diving.mask",
                    "diving.tank",
                    "diving.wetsuit",
                    "door.double.hinged.metal",
                    "door.double.hinged.toptier",
                    "door.double.hinged.wood",
                    "door.hinged.industrial.a",
                    "door.hinged.metal",
                    "door.hinged.toptier",
                    "door.hinged.wood",
                    "dropbox",
                    "explosive.satchel",
                    "explosive.timed",
                    "explosives",
                    "flamethrower",
                    "flameturret",
                    "flashlight.held",
                    "floor.grill",
                    "floor.ladder.hatch",
                    "floor.triangle.grill",
                    "floor.triangle.ladder.hatch",
                    "fridge",
                    "furnace",
                    "furnace.large",
                    "fuse",
                    "gates.external.high.stone",
                    "gates.external.high.wood",
                    "gears",
                    "generator.wind.scrap",
                    "grenade.beancan",
                    "grenade.f1",
                    "grenade.smoke",
                    "guntrap",
                    "habrepair",
                    "hammer.salvaged",
                    "hat.beenie",
                    "hat.boonie",
                    "hat.cap",
                    "hat.ratmask",
                    "hat.wolf",
                    "hatchet",
                    "hazmatsuit",
                    "hazmatsuit.spacesuit",
                    "healingtea",
                    "healingtea.advanced",
                    "healingtea.pure",
                    "heavy.plate.helmet",
                    "heavy.plate.jacket",
                    "heavy.plate.pants",
                    "hitchtroughcombo",
                    "hobobarrel",
                    "hoodie",
                    "horse.armor.roadsign",
                    "horse.armor.wood",
                    "horse.saddle",
                    "horse.saddlebag",
                    "horse.shoes.advanced",
                    "horse.shoes.basic",
                    "icepick.salvaged",
                    "innertube",
                    "innertube.horse",
                    "innertube.unicorn",
                    "jacket",
                    "jacket.snow",
                    "jackhammer",
                    "jackolantern.angry",
                    "jackolantern.happy",
                    "jar.pickle",
                    "ammo.grenadelauncher.buckshot",
                    "ammo.grenadelauncher.he",
                    "ammo.grenadelauncher.smoke",
                    "ammo.handmade.shell",
                    "ammo.nailgun.nails",
                    "ammo.pistol",
                    "ammo.pistol.fire",
                    "ammo.pistol.hv",
                    "ammo.rifle",
                    "ammo.rifle.explosive",
                    "ammo.rifle.hv",
                    "ammo.rifle.incendiary",
                    "ammo.rocket.basic",
                    "ammo.rocket.fire",
                    "ammo.rocket.hv",
                    "ammo.rocket.sam",
                    "ammo.rocket.smoke",
                    "ammo.shotgun",
                    "ammo.shotgun.fire",
                    "ammo.shotgun.slug",
                    "jumpsuit.suit",
                    "jumpsuit.suit.blue",
                    "kayak",
                    "keycard_blue",
                    "keycard_green",
                    "keycard_red",
                    "knife.bone",
                    "knife.butcher",
                    "knife.combat",
                    "ladder.wooden.wall",
                    "lantern",
                    "largecandles",
                    "largemedkit",
                    "laserlight",
                    "lmg.m249",
                    "locker",
                    "longsword",
                    "lowgradefuel",
                    "mace",
                    "machete",
                    "mask.balaclava",
                    "mask.bandana",
                    "maxhealthtea",
                    "maxhealthtea.advanced",
                    "maxhealthtea.pure",
                    "metal.facemask",
                    "metal.fragments",
                    "metal.plate.torso",
                    "metal.refined",
                    "metalblade",
                    "metalpipe",
                    "metalspring",
                    "microphonestand",
                    "minihelicopter.repair",
                    "mining.quarry",
                    "mixingtable",
                    "multiplegrenadelauncher",
                    "mushroom",
                    "nightvisiongoggles",
                    "oretea",
                    "oretea.advanced",
                    "oretea.pure",
                    "paddle",
                    "paddlingpool",
                    "pants",
                    "pants.shorts",
                    "paper",
                    "partyhat",
                    "pickaxe",
                    "pistol.eoka",
                    "pistol.m92",
                    "pistol.nailgun",
                    "pistol.python",
                    "pistol.revolver",
                    "pistol.semiauto",
                    "pitchfork",
                    "planter.large",
                    "planter.small",
                    "plantfiber",
                    "pookie.bear",
                    "potato",
                    "powered.water.purifier",
                    "propanetank",
                    "pumpkin",
                    "pumpkinbasket",
                    "radiationremovetea",
                    "radiationremovetea.advanced",
                    "radiationremovetea.pure",
                    "radiationresisttea",
                    "radiationresisttea.advanced",
                    "radiationresisttea.pure",
                    "red.berry",
                    "research.table",
                    "rf.detonator",
                    "rf_pager",
                    "rifle.ak",
                    "rifle.bolt",
                    "rifle.l96",
                    "rifle.lr300",
                    "rifle.m39",
                    "rifle.semiauto",
                    "riflebody",
                    "riot.helmet",
                    "roadsign.gloves",
                    "roadsign.jacket",
                    "roadsign.kilt",
                    "roadsigns",
                    "rocket.launcher",
                    "rope",
                    "rug",
                    "rug.bear",
                    "salvaged.cleaver",
                    "salvaged.sword",
                    "samsite",
                    "scraptea",
                    "scraptea.advanced",
                    "scraptea.pure",
                    "scraptransportheli.repair",
                    "searchlight",
                    "secretlabchair",
                    "semibody",
                    "sewingkit",
                    "sheetmetal",
                    "shelves",
                    "ammo.grenadelauncher.buckshot",
                    "ammo.grenadelauncher.he",
                    "ammo.grenadelauncher.smoke",
                    "ammo.handmade.shell",
                    "ammo.nailgun.nails",
                    "ammo.pistol",
                    "ammo.pistol.fire",
                    "ammo.pistol.hv",
                    "ammo.rifle",
                    "ammo.rifle.explosive",
                    "ammo.rifle.hv",
                    "ammo.rifle.incendiary",
                    "ammo.rocket.basic",
                    "ammo.rocket.fire",
                    "ammo.rocket.hv",
                    "ammo.rocket.sam",
                    "ammo.rocket.smoke",
                    "ammo.shotgun",
                    "ammo.shotgun.fire",
                    "ammo.shotgun.slug",
                    "shirt.collared",
                    "shirt.tanktop",
                    "shoes.boots",
                    "shotgun.double",
                    "shotgun.pump",
                    "shotgun.spas12",
                    "shotgun.waterpipe",
                    "shutter.metal.embrasure.a",
                    "shutter.metal.embrasure.b",
                    "shutter.wood.a",
                    "sickle",
                    "sleepingbag",
                    "small.oil.refinery",
                    "smg.2",
                    "smg.mp5",
                    "smg.thompson",
                    "smgbody",
                    "spear.stone",
                    "spear.wooden",
                    "speargun",
                    "speargun.spear",
                    "stone.pickaxe",
                    "stonehatchet",
                    "storage.monitor",
                    "strobelight",
                    "submarine.torpedo.rising",
                    "submarine.torpedo.straight",
                    "supply.signal",
                    "surveycharge",
                    "syringe.medical",
                    "table",
                    "tactical.gloves",
                    "targeting.computer",
                    "tarp",
                    "techparts",
                    "tool.instant_camera",
                    "vending.machine",
                    "wall.external.high",
                    "wall.external.high.ice",
                    "wall.external.high.stone",
                    "waterpump",
                    "weapon.mod.8x.scope",
                    "weapon.mod.flashlight",
                    "weapon.mod.holosight",
                    "weapon.mod.lasersight",
                    "weapon.mod.muzzleboost",
                    "weapon.mod.muzzlebrake",
                    "weapon.mod.silencer",
                    "weapon.mod.simplesight",
                    "weapon.mod.small.scope",
                    "white.berry",
                    "wood.armor.helmet",
                    "wood.armor.jacket",
                    "wood.armor.pants",
                    "woodtea",
                    "woodtea.advanced",
                    "workbench1",
                    "workbench2",
                    "workbench3"
                },
                Amount = new Dictionary<string, int>
                {
                    { "ammo.grenadelauncher.buckshot", 10 },
                    { "ammo.grenadelauncher.he", 1 },
                    { "ammo.grenadelauncher.smoke", 1 },
                    { "ammo.handmade.shell", 15 },
                    { "ammo.nailgun.nails", 30 },
                    { "ammo.pistol", 30 },
                    { "ammo.pistol.fire", 10 },
                    { "ammo.pistol.hv", 10 },
                    { "ammo.rifle", 30 },
                    { "ammo.rifle.explosive", 10 },
                    { "ammo.rifle.hv", 15 },
                    { "ammo.rifle.incendiary", 15 },
                    { "ammo.rocket.basic", 1 },
                    { "ammo.rocket.fire", 1 },
                    { "ammo.rocket.hv", 1 },
                    { "ammo.rocket.sam", 5 },
                    { "ammo.rocket.smoke", 1 },
                    { "ammo.shotgun", 10 },
                    { "ammo.shotgun.fire", 10 },
                    { "ammo.shotgun.slug", 10 }
                }
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
    }
}