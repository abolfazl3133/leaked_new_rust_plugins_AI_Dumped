using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PVPZoneStatus", "0xF", "1.0.1")]
    public class PVPZoneStatus : RustPlugin
    {
        #region Plugin References
        [PluginReference] private Plugin ImageLibrary, CustomStatusFramework, ZoneManager, DynamicPVP, RaidableBases, AbandonedBases;
        #endregion

        #region Hooks

        bool Initialled = false;
        private void OnServerInitialized()
        {
            
            if (ImageLibrary == null || CustomStatusFramework == null || ZoneManager == null)
            {
                PrintError("\nYou do not have one or more required plugins installed!\n" +
                    "ImageLibrary: https://umod.org/plugins/image-library \n" +
                    "ZoneManager: https://umod.org/plugins/zone-manager \n" +
                    "CustomStatusFramework: https://codefling.com/plugins/custom-status-framework");
                return;
            }

            LoadConfig();
            SaveConfig();

            foreach (var url in new string[] { config.PVPStatus.IconUrl, config.PVPDelayStatus.IconUrl, config.PVEStatus.IconUrl })
                if (!ImageLibrary.Call<bool>("HasImage", new object[] { url, (ulong)0 }))
                    ImageLibrary.Call<bool>("AddImage", new object[] { url, url, (ulong)0 });

            timer.Once(1f, () =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    UpdateStatus(player);
            });

        }
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CustomStatusFramework?.Call("ClearStatus", new object[] { player, $"PVPZoneStatus" });
            }

        }
        #endregion
        enum ForceMode
        {
            None,
            PVP,
            PVPDelay,
            PVE
        }


        #region RaidableBases

        private void OnPlayerEnteredRaidableBase(BasePlayer player, Vector3 location, bool allowPVP)
        {
            if (allowPVP)
                UpdateStatus(player, ForceMode.PVP);
        }

        private void OnPlayerExitedRaidableBase(BasePlayer player, Vector3 location, bool allowPVP) => UpdateStatus(player);

        private void OnRaidableBaseEnded(Vector3 raidPos, int mode, float loadingTime)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                UpdateStatus(player);
            }
        }

        #endregion RaidableBases

        #region AbandonedBases

        private void OnPlayerEnteredAbandonedBase(BasePlayer player, Vector3 location, bool allowPVP)
        {
            if (allowPVP)
                UpdateStatus(player, ForceMode.PVP);
        }

        private void OnPlayerExitAbandonedBase(BasePlayer player, Vector3 location, bool allowPVP)
        {
            UpdateStatus(player);
        }

        private void OnAbandonedBaseEnded(Vector3 location)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                UpdateStatus(player);
            }
        }

        #endregion AbandonedBases

        #region CargoTrainTunnel

        private void OnPlayerEnterPVPBubble(TrainEngine trainEngine, BasePlayer player)
        {
            UpdateStatus(player, ForceMode.PVP);
        }

        private void OnPlayerExitPVPBubble(TrainEngine trainEngine, BasePlayer player)
        {
            UpdateStatus(player);
        }

        private void OnTrainEventEnded(TrainEngine trainEngine)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                UpdateStatus(player);
            }
        }

        #endregion CargoTrainTunnel

        #region PVPDelay

        private void OnPlayerRemovedFromPVPDelay(ulong playerId, string zoneId) // DynamicPVP
        {
            var player = BasePlayer.FindByID(playerId);
            if (player == null) return;
            if (IsPlayerInPVPDelay(playerId))
                UpdateStatus(player);
        }

        private void OnPlayerPvpDelayExpired(BasePlayer player) // RaidableBases
        {
            if (player == null) return;
            UpdateStatus(player);
        }

        private void OnPlayerPvpDelayExpiredII(BasePlayer player) // AbandonedBases
        {
            if (player == null) return;
            UpdateStatus(player);
        }

        private bool IsPlayerInPVPDelay(ulong playerID)
        {
            if (DynamicPVP != null && Convert.ToBoolean(DynamicPVP.Call("IsPlayerInPVPDelay", playerID)))
            {
                return true;
            }

            if (RaidableBases != null && Convert.ToBoolean(RaidableBases.Call("HasPVPDelay", playerID)))
            {
                return true;
            }

            if (AbandonedBases != null && Convert.ToBoolean(AbandonedBases.Call("HasPVPDelay", playerID)))
            {
                return true;
            }

            return false;
        }

        #endregion PVPDelay

        #region Methods
        void UpdatePVEDelayStatus(BasePlayer player, ForceMode mode = ForceMode.None)
        {
            NextTick(() =>
            {
                CustomStatusFramework?.Call("UpdateStatus", new object[] { player, "PVPZoneStatus", config.PVPDelayStatus.Colors.Color, GetMessage(LangKeys.PVPDelay, player.UserIDString), config.PVPDelayStatus.Colors.TextColor, null, null, config.PVEStatus, config.PVPDelayStatus.Colors.IconColor });
            });
        }

        bool InPVPZone(BasePlayer player)
        {
            if (player.InSafeZone() || player.HasPlayerFlag(BasePlayer.PlayerFlags.SafeZone))
                return false;
            if (ZoneManager?.Call<bool>("PlayerHasFlag", new object[] { player, "PvpGod" }) == true)
                return false;
            return GetPlayerZoneIDs(player).Length > 0;
        }
        void UpdateStatus(BasePlayer player, ForceMode mode = ForceMode.None)
        {
            NextTick(() =>
            {
                if ((mode == ForceMode.PVP || (mode == ForceMode.None && InPVPZone(player))))
                {
                    CustomStatusFramework?.Call("UpdateStatus", new object[] { player, "PVPZoneStatus", config.PVPStatus.Colors.Color, GetMessage(LangKeys.PVP, player.UserIDString), config.PVPStatus.Colors.TextColor, null, null, config.PVPStatus.IconUrl, config.PVPStatus.Colors.IconColor });
                    return;
                }
                else
                {
                    if (mode == ForceMode.PVE)
                    {
                        CustomStatusFramework?.Call("UpdateStatus", new object[] { player, "PVPZoneStatus", config.PVEStatus.Colors.Color, GetMessage(LangKeys.PVE, player.UserIDString), config.PVEStatus.Colors.TextColor, null, null, config.PVEStatus.IconUrl, config.PVEStatus.Colors.IconColor });
                        return;
                    }
                    else
                    {
                        CustomStatusFramework?.Call("ClearStatus", new object[] { player, "PVPZoneStatus" });
                    }
                }

            });

        }

        void OnEnterZone(string ZoneID, BasePlayer player) => UpdateStatus(player);
        void OnExitZone(string ZoneID, BasePlayer player) => UpdateStatus(player);
        private string GetZoneName(string zoneId)
        {
            return (string)ZoneManager.Call("GetZoneName", zoneId);
        }
        private float GetZoneRadius(string zoneId)
        {
            var obj = ZoneManager.Call("GetZoneRadius", zoneId); ;
            if (obj is float)
            {
                return (float)obj;
            }
            return 0f;
        }

        private Vector3 GetZoneSize(string zoneId)
        {
            var obj = ZoneManager.Call("GetZoneSize", zoneId); ;
            if (obj is Vector3)
            {
                return (Vector3)obj;
            }
            return Vector3.zero;
        }
        private string[] GetPlayerZoneIDs(BasePlayer player)
        {
            return (string[])ZoneManager?.Call("GetPlayerZoneIDs", player);
        }
        private string GetSmallestZoneId(BasePlayer player)
        {
            float radius = float.MaxValue;
            string id = null;
            var zoneIDs = GetPlayerZoneIDs(player);
            foreach (var zoneId in zoneIDs)
            {
                float zoneRadius;
                var zoneSize = GetZoneSize(zoneId);
                if (zoneSize != Vector3.zero)
                {
                    zoneRadius = (zoneSize.x + zoneSize.z) / 2;
                }
                else
                {
                    zoneRadius = GetZoneRadius(zoneId);
                }
                if (zoneRadius <= 0f)
                {
                    continue;
                }
                if (radius >= zoneRadius)
                {
                    radius = zoneRadius;
                    id = zoneId;
                }
            }
            return id;
        }
        #endregion

        #region Config

        private static ConfigData config;

        private class ConfigData
        {
            public class Status
            {
                public class ColorsClass
                {
                    [JsonProperty(PropertyName = "Color")]
                    public string Color { get; set; }
                    [JsonProperty(PropertyName = "Icon Color")]
                    public string IconColor { get; set; }
                    [JsonProperty(PropertyName = "Text Color")]
                    public string TextColor { get; set; }

                }
                [JsonProperty(PropertyName = "Icon Url")]
                public string IconUrl { get; set; }
                public ColorsClass Colors { get; set; } = new ColorsClass();
            }
            [JsonProperty(PropertyName = "PVP Status Settings")]
            public Status PVPStatus { get; set; } = new Status()
            {
                IconUrl = "https://i.imgur.com/aNQeldI.png",
                Colors =
                {
                    Color = "0.65 0 0 1",
                    IconColor = "1 1 1 1",
                    TextColor = "1 1 1 1",
                }
            };
            [JsonProperty(PropertyName = "PVP Delay Status Settings")]
            public Status PVPDelayStatus { get; set; } = new Status()
            {
                IconUrl = "https://i.imgur.com/aNQeldI.png",
                Colors =
                {
                    Color = "0.65 0 0 1",
                    IconColor = "1 1 1 1",
                    TextColor = "1 1 1 1",
                }
            };
            [JsonProperty(PropertyName = "PVE Status Settings")]
            public Status PVEStatus { get; set; } = new Status()
            {
                IconUrl = "https://cdn-icons-png.flaticon.com/512/5103/5103350.png",
                Colors =
                {
                    Color = "0 0.65 0 1",
                    IconColor = "1 1 1 1",
                    TextColor = "1 1 1 1"
                }
            };
        }
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                    LoadDefaultConfig();
            }
            catch (Exception ex)
            {
                if (ex is JsonSerializationException || ex is NullReferenceException || ex is JsonReaderException)
                {
                    Puts($"Exception: {ex}");
                    LoadDefaultConfig();
                    return;
                }
                throw;
            }
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Configuration file missing or corrupt, creating default config file.");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Lang
        public class LangKeys
        {
            public const string PVP = nameof(PVP);
            public const string PVPDelay = nameof(PVPDelay);
            public const string PVE = nameof(PVE);
        }
        protected override void LoadDefaultMessages()
        {
            foreach (var l in lang.GetLanguages())
                lang.RegisterMessages(new Dictionary<string, string>
                {
                    [LangKeys.PVP] = "You're in the PVP zone",
                    [LangKeys.PVPDelay] = "You have PVP delay",
                    [LangKeys.PVE] = "You're in the PVE zone",
                }, this, l);
        }

        private string GetMessage(string langKey, string userID) => lang.GetMessage(langKey, this, userID);

        private string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang
    }
}