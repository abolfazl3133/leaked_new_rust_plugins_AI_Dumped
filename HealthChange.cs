using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("HealthChange", "CA$HR(discord: CASHR#6906)", "1.0.0")]
    internal class HealthChange : RustPlugin
    {
        #region Static

        private static HealthChange _;
        private Configuration _config;

        #endregion

        #region Config

        private class Configuration
        {
            [JsonProperty(PropertyName = "Список настроек хп по привилегиям", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> HPList = new Dictionary<string, int>()
            {
                ["heathchange.default"] = 100,
                ["heathchange.vip"] = 150,
                ["heathchange.elite"] = 200, 
            };
            
            [JsonProperty(PropertyName = "Список настроек урона по привилегиям", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> DamageList = new Dictionary<string, float>()
            {
                ["heathchange.default"] = 1,
                ["heathchange.vip"] = 1.2f,
                ["heathchange.elite"] = 1.5f,
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region OxideHooks

        private void OnServerInitialized()
        {
            _ = this;
            foreach (var check in _config.HPList)
            {
                permission.RegisterPermission(check.Key, this);
            }
            foreach (var check in _config.DamageList)
            {
                permission.RegisterPermission(check.Key, this);
            }
            PrintError("|-----------------------------------|");
            PrintWarning($"|  Plugin {Title} v{Version} is loaded  |");
            PrintWarning("|          Discord: CASHR#6906      |");
            PrintError("|-----------------------------------|");

        }

        private object OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            var attacker = info.InitiatorPlayer;
            if (attacker == null) return null;
            var damage = GetDamage(attacker);
            info.damageTypes.ScaleAll(damage);
            return null;
        }
        private void OnPlayerRespawned(BasePlayer player)
        {
            UpdateHealth(player, GetMaxHP(player));
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            UpdateHealth(player, GetMaxHP(player));
        }
        private void UpdateHealth(BasePlayer player,float amount)
        {
            var modifiers = new List<ModifierDefintion>();
            modifiers.Clear();

            float HealthPercent = 0;
            amount = amount < 0 ? 0 : amount;
            if (amount < 100)
                HealthPercent = (100f - amount) / 100f * -1;
            else
                HealthPercent = amount / 100f - 1;
            var modifier = new ModifierDefintion()
            {
                type = Modifier.ModifierType.Max_Health, duration = 86400, source = Modifier.ModifierSource.Tea,
                value = HealthPercent,
            };
            modifiers.Add(modifier);
            player.modifiers.Add(modifiers); 
        }
        private int GetMaxHP(BasePlayer player)
        {
            int damage = 100;
            foreach (var check in _config.HPList)
            {
                if(permission.UserHasPermission(player.UserIDString, check.Key))
                    damage = Math.Max(check.Value, damage);
            }
            return damage;
        }
        private float GetDamage(BasePlayer player)
        {
            float damage = 1f;
            foreach (var check in _config.DamageList)
            {
                if(permission.UserHasPermission(player.UserIDString, check.Key))
                    damage = Math.Max(check.Value, damage);
            }
            return damage;
        }
        private void Unload()
        {
            _ = null;
        }

        #endregion


        #region Function

        private void DestroyAll<T>()
        {
            var objects = UnityEngine.Object.FindObjectsOfType(typeof(T));
            objects?.ToList().ForEach(UnityEngine.Object.Destroy);
        }

        #endregion
    }
}