using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("BetterMetabolism", "ThePitereq", "1.1.6")]
    public class BetterMetabolism : RustPlugin
    {
        private readonly Dictionary<BasePlayer, float> currentGearBonus = new Dictionary<BasePlayer, float>();

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);
            LoadData();
            foreach (var perm in config.permissions)
                permission.RegisterPermission(perm.Key, this);
            if (!config.enableMaxHealth)
                Unsubscribe(nameof(OnPlayerAddModifiers));
            else
            {
                foreach (var player in BasePlayer.activePlayerList)
                    ModifyHealth(player);
                timer.Every(3000, () => {
                    foreach (var player in BasePlayer.activePlayerList)
                        ModifyHealth(player);
                });
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                data.playerMetabolism.TryAdd(player.userID, new MetabolismData());
                data.playerMetabolism[player.userID] = new MetabolismData() { health = player.health, calories = player.metabolism.calories.value, hydration = player.metabolism.hydration.value };
            }
            SaveData();
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            string highestPerm = GetHighestPerm(player);
            if (highestPerm == string.Empty) return;
            ModifyHealth(player);
            player.health = config.permissions[highestPerm].health;
            player.metabolism.calories.max = config.permissions[highestPerm].caloriesMax;
            player.metabolism.calories.value = config.permissions[highestPerm].calories;
            player.metabolism.hydration.max = config.permissions[highestPerm].hydrationMax;
            player.metabolism.hydration.value = config.permissions[highestPerm].hydration;
            NextTick(() => {
                player.SetHealth(config.permissions[highestPerm].health);
                player.SendNetworkUpdate();
            });
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            string highestPerm = GetHighestPerm(player);
            if (highestPerm == string.Empty) return;
            ModifyHealth(player);
            player.metabolism.calories.max = config.permissions[highestPerm].caloriesMax;
            player.metabolism.hydration.max = config.permissions[highestPerm].hydrationMax;
            if (data.playerMetabolism.ContainsKey(player.userID))
            {
                player.health = data.playerMetabolism[player.userID].health;
                player.metabolism.calories.value = data.playerMetabolism[player.userID].calories;
                player.metabolism.hydration.value = data.playerMetabolism[player.userID].hydration;
            }
            player.SendNetworkUpdate();
        }

        private void OnPlayerDeath(BasePlayer player) => data.playerMetabolism.Remove(player.userID);

        private void OnPlayerDisconnected(BasePlayer player)
        {
            data.playerMetabolism.TryAdd(player.userID, new MetabolismData());
            data.playerMetabolism[player.userID] = new MetabolismData() { health = player.health, calories = player.metabolism.calories.value, hydration = player.metabolism.hydration.value };
        }

        private void OnPlayerAddModifiers(BasePlayer player, Item item, ItemModConsumable consumable)
        {
            if (!item.info.shortname.Contains("maxhealthtea")) return;
            string highestPerm = GetHighestPerm(player);
            if (highestPerm == string.Empty) return;
            ulong playerId = player.userID;
            ModifyHealth(player, consumable.modifiers[0].value, consumable.modifiers[0].duration);
            timer.Once(consumable.modifiers[0].duration, () => {
                if (player != null)
                    ModifyHealth(player);
            });
        }

        private string GetHighestPerm(BasePlayer player)
        {
            string highestPerm = string.Empty;
            foreach (var perm in config.permissions)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Key))
                    highestPerm = perm.Key;
            }
            return highestPerm;
        }

        private object ELOnModifyHealth(BasePlayer player, float modifier)
        {
            currentGearBonus[player] = modifier;
            ModifyHealth(player);
            return false;
        }

        private void ModifyHealth(BasePlayer player, float teaBonus = 0, float teaTime = 0)
        {
            if (!config.enableMaxHealth) return;
            string highestPerm = GetHighestPerm(player);
            if (highestPerm == string.Empty) return;
            if (Interface.CallHook("OnModifyHealth", player) != null) return;
            if (config.permissions[highestPerm].healthMax == 100 || config.permissions[highestPerm].healthMax <= 0) return;
            float healthValue = (config.permissions[highestPerm].healthMax - 100) / 100;
            float duration = 3600;
            if (currentGearBonus.ContainsKey(player))
                teaBonus += currentGearBonus[player];
            if (teaBonus != 0 && config.increaseHealthType && healthValue < teaBonus)
            {
                healthValue = teaBonus;
                duration = teaTime;
            }
            else if (teaBonus != 0 && !config.increaseHealthType)
            {
                healthValue += teaBonus;
                duration = teaTime;
            }
            NextTick(() => {
                player.modifiers.Add(new List<ModifierDefintion>
                {
                    new ModifierDefintion
                    {
                        duration = duration,
                        source = Modifier.ModifierSource.Tea,
                        type = Modifier.ModifierType.Max_Health,
                        value = healthValue
                    }
                });
                player.SendNetworkUpdate();
            });
        }

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
            NextTick(() => {
                config = Config.ReadObject<PluginConfig>();
                config.permissions = new Dictionary<string, MetabolismConfig>()
                {
                    { "bettermetabolism.default", new MetabolismConfig() },
                    { "bettermetabolism.vip", new MetabolismConfig() { health = 100, caloriesMax = 1000, calories = 600 } }
                };
                Config.WriteObject(config);
            });
        }

        private class PluginConfig
        {
            [JsonProperty("Enable Max Health Option")]
            public bool enableMaxHealth = true;

            [JsonProperty("Increase health to tea level (true) or increase health by tea value (false)")]
            public bool increaseHealthType = false;

            [JsonProperty("Metabolism Permissions")]
            public Dictionary<string, MetabolismConfig> permissions = new Dictionary<string, MetabolismConfig>();
        }

        private class MetabolismConfig
        {
            [JsonProperty("Health")]
            public float health = 60;

            [JsonProperty("Max Health")]
            public float healthMax = 100;

            [JsonProperty("Max Calories")]
            public float caloriesMax = 500;

            [JsonProperty("Calories")]
            public float calories = 250;

            [JsonProperty("Max Hydration")]
            public float hydrationMax = 250;

            [JsonProperty("Hydration")]
            public float hydration = 100;
        }

        private static PluginData data = new PluginData();

        private class PluginData
        {
            [JsonProperty("Player Metabolism")]
            public Dictionary<ulong, MetabolismData> playerMetabolism = new Dictionary<ulong, MetabolismData>();
        }

        private class MetabolismData
        {
            [JsonProperty("Health")]
            public float health = 60;

            [JsonProperty("Calories")]
            public float calories = 250;

            [JsonProperty("Hydration")]
            public float hydration = 100;
        }

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(this.Name);
            timer.Every(Core.Random.Range(500, 700), SaveData);
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, data);
    }
}