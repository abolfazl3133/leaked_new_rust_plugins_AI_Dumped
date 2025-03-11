using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SubmersiblePump", "ThePitereq", "1.0.11")]
    public class SubmersiblePump : RustPlugin
    {

        private static readonly Dictionary<ulong, int> placedPumps = new Dictionary<ulong, int>();

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);
            cmd.AddChatCommand(config.command, this, nameof(CraftPumpCommand));
            cmd.AddConsoleCommand("givepump", this, nameof(GivePumpCommand));
            if (config.requirePerm) permission.RegisterPermission("submersiblepump.use", this);
            permission.RegisterPermission("submersiblepump.give", this);
            foreach (var perm in config.permissionLimit)
                permission.RegisterPermission(perm.Key, this);
            if (!config.hammerHitPickup)
                Unsubscribe(nameof(OnHammerHit));
        }

        private void OnServerInitialized()
        {
            LoadData();
            foreach (var pump in data.pumps)
            {
                WaterPump pumpEntity = BaseNetworkable.serverEntities.Find(new NetworkableId(pump)) as WaterPump;
                if (pumpEntity != null)
                {
                    TerrainMeta.TopologyMap.AddTopology(pumpEntity.transform.position, 65536);
                    placedPumps.TryAdd(pumpEntity.OwnerID, 0);
                    placedPumps[pumpEntity.OwnerID]++;
                }
            }
        }

        private void Unload()
        {
            SaveData();
            placedPumps.Clear();
        }

        private void OnPasteFinished(List<BaseEntity> pastedEntities)
        {
            foreach (var ent in pastedEntities)
            {
                if (ent.skinID != 2593673595) continue;
                TerrainMeta.TopologyMap.AddTopology(ent.transform.position, 65536);
            }
        }

        private void OnEntityKill(WaterPump entity)
        {
            if (entity.OwnerID == 0) return;
            if (data.pumps.Contains(entity.net.ID.Value))
            {
                Item item = ItemManager.CreateByName("electric.fuelgenerator.small", 1, 2593673595);
                item.name = config.pumpName;
                item.Drop(entity.transform.position, Vector3.zero, new Quaternion());
                if (placedPumps.ContainsKey(entity.OwnerID))
                    placedPumps[entity.OwnerID]--;
            }
        }

        private bool CanPickupEntity(BasePlayer player, WaterPump entity)
        {
            if (entity.skinID == 2593673595)
            {
                if (entity.GetBuildingPrivilege() == null)
                {
                    entity.Kill();
                    return false;
                }
                if (!entity.GetBuildingPrivilege().IsAuthed(player)) return false;
                entity.Kill();
                return false;
            }
            return true;
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan.GetOwnerPlayer() == null) return;
            BasePlayer player = plan.GetOwnerPlayer();
            FuelGenerator generator = go.ToBaseEntity() as FuelGenerator;
            if (generator == null || generator.skinID != 2593673595) return;
            if (config.requirePerm && !permission.UserHasPermission(player.UserIDString, "submersiblepump.use"))
            {
                Item item = ItemManager.CreateByName("electric.fuelgenerator.small", 1, 2593673595);
                item.name = config.pumpName;
                player.inventory.GiveItem(item);
                NextTick(() => generator.Kill());
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            if (config.pumpLimit > 0)
            {
                int permLimit = config.pumpLimit;
                foreach (var perm in config.permissionLimit)
                    if (permission.UserHasPermission(player.UserIDString, perm.Key))
                        permLimit = perm.Value;
                if (placedPumps.ContainsKey(player.userID) && placedPumps[player.userID] >= permLimit)
                {
                    Item item = ItemManager.CreateByName("electric.fuelgenerator.small", 1, 2593673595);
                    item.name = config.pumpName;
                    player.inventory.GiveItem(item);
                    NextTick(() => generator.Kill());
                    SendReply(player, Lang("PumpLimit", player.UserIDString));
                    return;
                }
            }
            Vector3 heightCheck = new Vector3(generator.transform.position.x, TerrainMeta.HeightMap.GetHeight(generator.transform.position), generator.transform.position.z);
            if ((config.checkGround || config.allowWater) && Vector3.Distance(heightCheck, generator.transform.position) > 1.4f)
            {
                if (!config.allowWater || (config.allowWater && (heightCheck.y > 0 || generator.transform.position.y > 1.4f)))
                {
                    Item item = ItemManager.CreateByName("electric.fuelgenerator.small", 1, 2593673595);
                    item.name = config.pumpName;
                    player.inventory.GiveItem(item);
                    NextTick(() => generator.Kill());
                    SendReply(player, Lang("TooHigh", player.UserIDString));
                    return;
                }
            }
            WaterPump pump = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/waterpump/water.pump.deployed.prefab", generator.transform.position, generator.transform.rotation) as WaterPump;
            pump.skinID = generator.skinID;
            pump.OwnerID = player.userID;
            NextTick(() => generator.Kill());
            pump.Spawn();
            placedPumps.TryAdd(player.userID, 0);
            placedPumps[player.userID]++;
            data.pumps.Add(pump.net.ID.Value);
            TerrainMeta.TopologyMap.AddTopology(pump.transform.position, 65536);
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            WaterPump pump = info.HitEntity as WaterPump;
            if (pump == null) return;
            if (pump.skinID != 2593673595) return;
            BuildingPrivlidge tc = pump.GetBuildingPrivilege();
            if (tc != null && !tc.IsAuthed(player)) return;
            pump.Kill();
        }

        private object canRemove(BasePlayer player, WaterPump pump)
        {
            if (pump.skinID == 2593673595) return false;
            else return null;
        }

        private void CraftPumpCommand(BasePlayer player, string command, string[] args)
        {
            if (config.requirePerm && !permission.UserHasPermission(player.UserIDString, "submersiblepump.use"))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }
            if (!config.enablePumpCraft)
            {
                SendReply(player, Lang("NotEnabled", player.UserIDString));
                return;
            }
            if (args.Length == 0)
                ShowHelp(player);
            else if (args.Length == 1 && args[0].ToLower() == "craft")
            {
                if (config.requireBlueprint && !player.blueprints.HasUnlocked(ItemManager.FindItemDefinition("waterpump")))
                {
                    SendReply(player, Lang("NoBlueprint", player.UserIDString));
                    return;
                }
                if (config.requiredWorkbench > player.currentCraftLevel)
                {
                    SendReply(player, Lang("NoWorkbench", player.UserIDString, player.currentCraftLevel, config.requiredWorkbench));
                    return;
                }
                if (!TakeResources(player))
                {
                    SendReply(player, Lang("NoItems", player.UserIDString));
                    return;
                }
                SendReply(player, Lang("ItemCrafted", player.UserIDString));
                Item item = ItemManager.CreateByName("electric.fuelgenerator.small", 1, 2593673595);
                item.name = config.pumpName;
                if (player.inventory.GiveItem(item))
                    player.SendConsoleCommand($"note.inv -1284169891 1 \"{config.pumpName}\"");
                else
                    item.Drop(player.transform.position + new Vector3(0, 1, 0), Vector3.zero);
            }
            else
                ShowHelp(player);
        }

        private void GivePumpCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "submersiblepump.give"))
            {
                SendReply(arg, Lang("NoPermission", player.UserIDString));
                return;
            }
            if (player == null && arg.Args == null)
            {
                SendReply(arg, Lang("PlayerNotFound"));
                return;
            }
            if (arg.Args != null && arg.Args.Length >= 1)
            {
                player = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[0]));
                if (player == null)
                {
                    SendReply(arg, Lang("PlayerNotFound"));
                    return;
                }
            }
            Item item = ItemManager.CreateByName("electric.fuelgenerator.small", 1, 2593673595);
            item.name = config.pumpName;
            player.inventory.GiveItem(item);
            player.SendConsoleCommand($"note.inv -1284169891 1 \"{config.pumpName}\"");
            SendReply(arg, Lang("ItemCrafted"));
        }

        private bool TakeResources(BasePlayer player)
        {
            foreach (var requiredItem in config.pumpCraftCost)
            {
                bool haveRequired = false;
                int inventoryAmount = 0;
                foreach (var item in player.inventory.AllItems())
                {
                    if (item.skin == requiredItem.skin && item.info.shortname == requiredItem.shortname)
                    {
                        inventoryAmount += item.amount;
                        if (inventoryAmount >= requiredItem.amount)
                        {
                            haveRequired = true;
                            break;
                        }
                    }
                }
                if (!haveRequired)
                    return false;
            }
            foreach (var requiredItem in config.pumpCraftCost)
            {
                ItemDefinition itemDef = ItemManager.FindItemDefinition(requiredItem.shortname);
                player.SendConsoleCommand($"note.inv {itemDef.itemid} -{requiredItem.amount}");
                int takenItems = 0;
                foreach (var item in player.inventory.AllItems())
                {
                    if (item.skin == requiredItem.skin && item.info.shortname == requiredItem.shortname)
                    {
                        if (takenItems < requiredItem.amount)
                        {
                            if (item.amount > requiredItem.amount - takenItems)
                            {
                                item.amount -= requiredItem.amount - takenItems;
                                item.MarkDirty();
                                break;
                            }
                            if (item.amount <= requiredItem.amount - takenItems)
                            {
                                takenItems += item.amount;
                                item.GetHeldEntity()?.Kill();
                                item.Remove();
                            }
                        }
                        else break;
                    }
                }
            }
            return true;
        }

        private void ShowHelp(BasePlayer player)
        {
            string items = string.Empty;
            if (config.requireBlueprint)
                items += Lang("BlueprintRequired", player.UserIDString);
            foreach (var item in config.pumpCraftCost)
                items += Lang("ItemFormat", player.UserIDString, item.amount, ItemManager.FindItemDefinition(item.shortname).displayName.english);
            SendReply(player, Lang("Help", player.UserIDString, config.command, config.requiredWorkbench, items));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TooHigh"] = "You are <color=#5c81ed>too far from ground</color> to place submersible pump!",
                ["NotEnabled"] = "Submersible Pump crafting is <color=#5c81ed>not enabled</color>!",
                ["NoItems"] = "You don't have <color=#5c81ed>required items</color> to craft Submersible Pump!",
                ["ItemCrafted"] = "You've succesfully crafted your <color=#5c81ed>Submersible Pump</color>!",
                ["NoBlueprint"] = "You need to learn Water Pump <color=#5c81ed>blueprint</color> first to craft Submersible Pump!",
                ["NoWorkbench"] = "Submersible Pump require higher <color=#5c81ed>workbench level</color>!\n(Current: {0}, Required: {1})",
                ["Help"] = "<color=#5c81ed>/{0} craft</color> - Craft Submersible Pump\n\nRequired Workbench Level: <color=#5c81ed>{1}</color>\nRequired Items:\n{2}",
                ["BlueprintRequired"] = "  - Water Pump Blueprint\n",
                ["ItemFormat"] = "  - <color=#5c81ed>x{0}</color> {1}\n",
                ["NoPermission"] = "You don't have permission to craft and place <color=#5c81ed>Submersible Pumps</color>!",
                ["PlayerNotFound"] = "We couldn't find player with this ID!",
                ["PumpLimit"] = "You've reached your submersible pump limit!"
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(config = new PluginConfig()
            {
                pumpCraftCost = new List<ItemConfig>()
                {
                    new ItemConfig() { shortname = "metal.fragments", amount = 1000, skin = 0 },
                    new ItemConfig() { shortname = "gears", amount = 10, skin = 0 },
                    new ItemConfig() { shortname = "metalpipe", amount = 20, skin = 0 }
                },
                permissionLimit = new Dictionary<string, int>()
                {
                    {"submersiblepump.vip", 5},
                    {"submersiblepump.admin", 100}
                }
            }, true);
        }

        private class PluginConfig
        {
            [JsonProperty("Misc - Require Permission")]
            public bool requirePerm = false;

            [JsonProperty("Misc - Pump Item Name")]
            public string pumpName = "Submersible Pump";

            [JsonProperty("Misc - Pump Ground Check")]
            public bool checkGround = true;

            [JsonProperty("Misc - Allow Placing On Water Level")]
            public bool allowWater = false;

            [JsonProperty("Misc - Allow Hammer Hit Pickup")]
            public bool hammerHitPickup = true;

            [JsonProperty("Limit - Default Pump Limit (0, to disable)")]
            public int pumpLimit = 0;

            [JsonProperty("Limit - Permission Limits")]
            public Dictionary<string, int> permissionLimit = new Dictionary<string, int>();

            [JsonProperty("Craft - Enable Pump Craft")]
            public bool enablePumpCraft = true;

            [JsonProperty("Craft - Chat Command")]
            public string command = "pump";

            [JsonProperty("Craft - Require Blueprint For Pump")]
            public bool requireBlueprint = true;

            [JsonProperty("Craft - Required Workbench Level (0-3)")]
            public int requiredWorkbench = 2;

            [JsonProperty("Craft - Pump Craft Cost")]
            public List<ItemConfig> pumpCraftCost = new List<ItemConfig>();
        }

        private class ItemConfig
        {
            [JsonProperty("Item Shortname")]
            public string shortname;

            [JsonProperty("Item Amount")]
            public int amount;

            [JsonProperty("Item Skin")]
            public ulong skin;
        }

        private static PluginData data = new PluginData();

        private class PluginData
        {
            [JsonProperty("Placed Pumps")]
            public List<ulong> pumps = new List<ulong>();
        }

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(this.Name);
            timer.Every(Core.Random.Range(500, 700), SaveData);
            if (data == null)
            {
                PrintError("Data file is corrupted! Generating new data file...");
                Interface.Oxide.DataFileSystem.WriteObject(this.Name, new PluginData());
                data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(this.Name);
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, data);
    }
}
