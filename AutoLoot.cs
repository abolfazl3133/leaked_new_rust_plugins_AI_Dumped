using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Auto Loot", "Gt403cyl2", "1.0.3")]
    [Description("Auto transfer loot from a destroyed barrel / Roadsigns as well as other optional containers to your inventory.")]
    internal class AutoLoot : RustPlugin
    {
        #region config

        private List<BasePlayer> playersLoot = new List<BasePlayer>();
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Distance to allow Auto Loot to be used (meters): ")]
            public float abdistance = 15f;

            [JsonProperty(PropertyName = "Auto Loot command for players: ")]
            public string alootCommand = "aloot";

            [JsonProperty("Enable Optional Containers")]
            public bool UseOptionalContainers = true;

            [JsonProperty(PropertyName = "Optional Containers")]
            public string[] OptionalContainers = new string[]
                {
                    "box.wooden.large",
                    "woodbox_deployed",
                    "cupboard.tool.deployed",
                    "coffinstorage",
                    "vendingmachine.deployed",
                    "dropbox.deployed",
                    "fridge.deployed",
                    "tunalight.deployed",
                    "furnace.prefab",
                    "furnace.large.prefab",
                    "bbq.deployed",
                    "mixingtable.deployed",
                    "composter",
                    "fireplace.deployed",
                    "researchtable_deployed",
                    "hitchtrough.deployed",
                    "locker.deployed",
                    "flameturret.deployed",
                };

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => configData = new ConfigData();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                configData = Config.ReadObject<ConfigData>();

                if (!configData.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning($"{Name} Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (FileNotFoundException)
            {
                PrintWarning($"No {Name} configuration file found, creating default");
                LoadDefaultConfig();
                SaveConfig();
            }
            catch (JsonReaderException)
            {
                PrintError($"{Name} Configuration file contains invalid JSON, creating default");
            }
        }

        protected override void SaveConfig()
        {
            PrintWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(configData, true);
        }

        #endregion config

        #region Hooks

        private void Init()
        {
            cmd.AddChatCommand(configData.alootCommand, this, CommandALoot);
            permission.RegisterPermission("autoloot.use", this);
        }


        private void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker == null || attacker.IsNpc || !permission.UserHasPermission(attacker.UserIDString, "autoloot.use")) return;
            if (!playersLoot.Contains(attacker)) return;
            //Puts($"Entity killed: {entity.ShortPrefabName}");
            if (!entity.name.Contains("barrel") && !entity.name.Contains("roadsign") && (!configData.OptionalContainers.Contains(entity.ShortPrefabName) || !configData.UseOptionalContainers)) return;
            if (UnityEngine.Vector3.Distance(attacker.transform.position, entity.transform.position) > configData.abdistance) return;

            if (entity is LootContainer || entity is StorageContainer)
            {
                ItemContainer itemContainer = (entity as StorageContainer)?.inventory;
                if (itemContainer != null)
                {
                    List<Item> items = itemContainer.itemList.ToList();
                    foreach (Item item in items)
                    {
                        if (!attacker.inventory.containerMain.IsFull())
                        {
                            item.MoveToContainer(attacker.inventory.containerMain);
                        }
                        else if (!attacker.inventory.containerBelt.IsFull())
                        {
                            {
                                item.MoveToContainer(attacker.inventory.containerBelt);
                            }
                        }
                        else
                        {
                            item.Drop(attacker.transform.position, attacker.transform.forward);
                        }
                        itemContainer.itemList.Remove(item); // modify the original collection outside of the foreach loop
                    }
                }
            }
        }


        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (playersLoot.Contains(player)) playersLoot.Remove(player);
            else return;
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.allPlayerList)
            {
                if (playersLoot.Contains(player))
                {
                    playersLoot.Remove(player);
                    player.ChatMessage(lang.GetMessage("UnloadDisable", this));
                }
            }
        }

        #endregion Hooks

        #region Commands

        private void CommandALoot(BasePlayer player, string commands, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "autoloot.use"))
            {
                player.ChatMessage($"{lang.GetMessage("NoPermission", this)}");
                return;
            }

            if (!playersLoot.Contains(player))
            {
                playersLoot.Add(player);
                player.ChatMessage($"{lang.GetMessage("ALootActive", this)}");
            }
            else
            {
                playersLoot.Remove(player);
                player.ChatMessage(lang.GetMessage("ALootDeactivated", this));
            }
        }

        #endregion Commands

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = $"You don't have permission to use this command.",
                ["ALootActive"] = $"AutoLoot is active, use /{configData.alootCommand} to deactivate.",
                ["ALootDeactivated"] = $"AutoLoot deactivated.",
                ["UnloadDisable"] = $"AutoLoot has been unloaded or reloaded, you will have to re-enable it with /{configData.alootCommand}"
            }, this);
        }

        #endregion Lang
    }
}