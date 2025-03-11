using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;

/* WARNING! */
/* Публикация данного плагина запрещена на сторонних форумах без указания её владельца*/


namespace Oxide.Plugins
{
    [Info("MBed Cooldowns", "vk.com/nnetadon", "2.0.0")]
    [Description("КД спальников по пермишену")]
    public class MBedCooldowns : RustPlugin
    {
        #region Oxide Hooks

        private void Init()
        {
            foreach (var value in config.list)
            {
                permission.RegisterPermission(value.perm, this);
            }
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                OnPlayerConnected(player);
            }
        }

        private void OnEntitySpawned(SleepingBag entity)
        {
            var settings = GetSettings(entity.OwnerID.ToString());
            SetCooldown(entity, settings);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            CheckPlayer(player);
        }

        #endregion

        #region Core

        private void CheckPlayer(BasePlayer player)
        {
            var settings = GetSettings(player.UserIDString);
            if (settings == null) {return;}
            ServerMgr.Instance.StartCoroutine(CheckBags(player.userID, settings));
        }
        
        private void SetCooldown(SleepingBag entity, SettingsEntry info)
        {
            if (info == null) {return;}

            if (entity.ShortPrefabName.Contains("bed"))
            {
                entity.secondsBetweenReuses = info.bed;
                entity.unlockTime = info.unlockTimeBed + UnityEngine.Time.realtimeSinceStartup;
            }
            else
            {
                entity.secondsBetweenReuses = info.bag;
                entity.unlockTime = info.unlockTimeBag + UnityEngine.Time.realtimeSinceStartup;
            }
            
            entity.SendNetworkUpdate();
        }

        private SettingsEntry GetSettings(string playerID)
        {
            var num = -1;
            var info = (SettingsEntry) null;

            foreach (var value in config.list)
            {
                if (permission.UserHasPermission(playerID, value.perm))
                {
                    var priority = value.priority;
                    if (priority > num)
                    {
                        num = priority;
                        info = value;
                    }
                }
            }

            return info;
        }

        private IEnumerator CheckBags(ulong playerID, SettingsEntry settings)
        {
            foreach (var entity in SleepingBag.sleepingBags)
            {
                if (entity.OwnerID == playerID)
                {
                    SetCooldown(entity, settings);
                }
            }
            
            yield break;
        }

        #endregion
        
        #region Configuration 1.1.0

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "List")]
            public List<SettingsEntry> list = new List<SettingsEntry>();
        }
        
        private class SettingsEntry
        {
            [JsonProperty(PropertyName = "Разрешение")]
            public string perm;
            
            [JsonProperty(PropertyName = "Приоритет")]
            public int priority;
                
            [JsonProperty(PropertyName = "Перезарядка Спального мешка")]
            public float bag;
                
            [JsonProperty(PropertyName = "Перезарядка кровати")]
            public float bed;

            [JsonProperty(PropertyName = "Время разблокировки спального мешка")]
            public float unlockTimeBag;

            [JsonProperty(PropertyName = "Время разблокировки кровати")]
            public float unlockTimeBed;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                list = new List<SettingsEntry>
                {
                    new SettingsEntry
                    {
                        perm = "mbedscooldowns.default",
                        priority = 1,
                        bag = 100,
                        bed = 100,
                        unlockTimeBag = 50,
                        unlockTimeBed = 50,
                    },
                    new SettingsEntry
                    {
                        perm = "mbedscooldowns.vip",
                        priority = 2,
                        bag = 75,
                        bed = 75,
                        unlockTimeBag = 50,
                        unlockTimeBed = 50,
                    }
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Файл конфигурации поврежден. Выгрузка плагина...");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
    }
}