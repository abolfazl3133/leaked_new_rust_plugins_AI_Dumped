using System.Collections.Generic;
using Network;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("DeathMarker", "k1lly0u", "2.0.8")]
    [Description("Show your death location on your map, and with in game draw")]
    class DeathMarker : RustPlugin
    {
        #region Fields
        internal static DeathMarker Instance { get; private set; }

        private static Hash<ulong, PlayerMarkers> playerMarkers;
        private static Hash<ulong, ulong> markerLookup;

        private const string PERMISSION_USE = "deathmarker.use";
        private const string RADIUSMARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string VENDINGMARKER_PREFAB = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            playerMarkers = new Hash<ulong, PlayerMarkers>();
            markerLookup = new Hash<ulong, ulong>();

            permission.RegisterPermission(PERMISSION_USE, this);

            Unsubscribe("CanNetworkTo");

            if (!configData.Draw.Enabled)
                Unsubscribe("OnPlayerRespawned");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Chat.Notification"] = "You can see your last death location on your map",
                ["Marker.Title"] = "You died here",
            },
            this);

            Instance = this;
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
                return;

            playerMarkers[player.userID]?.Destroy();
            playerMarkers[player.userID] = new PlayerMarkers(player);

            Subscribe("CanNetworkTo");
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
                return;

            PlayerMarkers playerMarker;
            if (playerMarkers.TryGetValue(player.userID, out playerMarker) && playerMarker != null)
                Draw(player, playerMarker.position);
        }

        private object CanNetworkTo(MapMarker marker, BasePlayer player)
        {
            if (marker == null || player == null)
                return null;

            ulong ownerId;
            if (markerLookup.TryGetValue(marker.net.ID.Value, out ownerId))
            {
                if (player.userID == ownerId)
                    return null;

                return false;
            }
            return null;
        }

        private void Unload()
        {
            for (int i = playerMarkers.Count - 1; i >= 0; i--)            
                playerMarkers.ElementAt(i).Value?.Destroy();
            
            playerMarkers.Clear();
            playerMarkers = null;

            markerLookup.Clear();
            markerLookup = null;

            configData = null;
            Instance = null;
        }
        #endregion

        #region Functions
        private void Draw(BasePlayer player, Vector3 position)
        {
            if (player.IsAdmin)
            {
                SendDrawCommands(player, position);
            }
            else
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
                SendDrawCommands(player, position);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
            }
        }

        private void SendDrawCommands(BasePlayer player, Vector3 position)
        {
            if (configData.Draw.RandomizeOffset)
                position += new Vector3(Random.Range(-20, 20), 0, Random.Range(-20, 20));

            if (configData.Draw.ShowArrow)
                player.SendConsoleCommand("ddraw.arrow", configData.Draw.Duration, ColorFromHex(configData.Draw.ArrowColor), position + (Vector3.up * (configData.Draw.ArrowOffset + configData.Draw.ArrowLength)), position + (Vector3.up * configData.Draw.ArrowOffset), 5f);

            if (configData.Draw.ShowSphere)
                player.SendConsoleCommand("ddraw.sphere", configData.Draw.Duration, ColorFromHex(configData.Draw.SphereColor), position, configData.Draw.SphereRadius);

            if (configData.Draw.ShowText)
                player.SendConsoleCommand("ddraw.text", configData.Draw.Duration, ColorFromHex(configData.Draw.TextColor), position + (Vector3.up * configData.Draw.TextOffset), Message("Marker.Title", player.UserIDString));
        }

        private static Color ColorFromHex(string hexColor)
        {
            if (hexColor.StartsWith("#"))
                hexColor = hexColor.Substring(1);
            int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);

            return new Color((float)red / 255f, (float)green / 255f, (float)blue / 255f, 1);
        }

        private static string Message(string key, string userId) => Instance.lang.GetMessage(key, Instance, userId);

        private class PlayerMarkers
        {
            public MapMarkerGenericRadius radialMarker;
            public VendingMachineMapMarker vendingMarker;

            public Vector3 position;

            private ulong userId;
            private Timer timer;

            public PlayerMarkers(BasePlayer player)
            {
                position = player.transform.position;
                userId = player.userID;

                if (configData.Map.EnableRadial)
                {
                    radialMarker = GameManager.server.CreateEntity(RADIUSMARKER_PREFAB, position) as MapMarkerGenericRadius;
                    radialMarker.radius = configData.Map.Radius;
                    radialMarker.color1 = ColorFromHex(configData.Map.Color);
                    radialMarker.alpha = 1f;

                    radialMarker.enableSaving = false;                   
                    radialMarker.Spawn();

                    markerLookup[radialMarker.net.ID.Value] = userId;

                    NetworkDestroy(radialMarker);
                }

                if (configData.Map.EnableVending)
                {
                    vendingMarker = GameManager.server.CreateEntity(VENDINGMARKER_PREFAB, position) as VendingMachineMapMarker;
                    vendingMarker.markerShopName = Message("Marker.Title", player.UserIDString);

                    vendingMarker.enableSaving = false;
                    vendingMarker.Spawn();

                    markerLookup[vendingMarker.net.ID.Value] = userId;

                    NetworkDestroy(vendingMarker);
                }

                timer = Instance.timer.In(configData.Map.Duration, Destroy);
            }

            private void NetworkDestroy(BaseEntity entity)
            {
                NetWrite netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Network.Message.Type.EntityDestroy);
                netWrite.EntityID(entity.net.ID);
                netWrite.UInt8((byte)0);
                netWrite.Send(new SendInfo(BasePlayer.activePlayerList.Select(x => x.Connection).ToList()));

                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Count);
            }

            public void Destroy()
            {
                timer?.Destroy();

                if (radialMarker != null && !radialMarker.IsDestroyed)
                {
                    markerLookup.Remove(radialMarker.net.ID.Value);
                    radialMarker.Kill();
                }

                if (vendingMarker != null && !vendingMarker.IsDestroyed)
                {
                    markerLookup.Remove(vendingMarker.net.ID.Value);
                    vendingMarker.Kill();
                }

                playerMarkers.Remove(userId);

                if (playerMarkers.Count == 0)
                    Instance.Unsubscribe("CanNetworkTo");
            }
        }
        #endregion

        #region Config        
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Map Marker Options")]
            public MapMarkers Map { get; set; }

            [JsonProperty(PropertyName = "Notification Options")]
            public Notifications Notification { get; set; }

            [JsonProperty(PropertyName = "Direct Draw Options")]
            public DDraw Draw { get; set; }

            public class MapMarkers
            {
                [JsonProperty(PropertyName = "Enable radial markers")]
                public bool EnableRadial { get; set; }

                [JsonProperty(PropertyName = "Enable vending markers")]
                public bool EnableVending { get; set; }

                [JsonProperty(PropertyName = "Duration markers are visible on map (seconds)")]
                public int Duration { get; set; }

                [JsonProperty(PropertyName = "Radius of map marker")]
                public float Radius { get; set; }

                [JsonProperty(PropertyName = "Color of map marker (hex)")]
                public string Color { get; set; }
            }

            public class Notifications
            {
                [JsonProperty(PropertyName = "Show notification on respawn")]
                public bool RespawnNotification { get; set; }

                [JsonProperty(PropertyName = "Notification delay (seconds)")]
                public int NotificationDelay { get; set; }

                [JsonProperty(PropertyName = "Debug deaths in console")]
                public bool Debug { get; set; }
            }

            public class DDraw
            {
                [JsonProperty(PropertyName = "Enable direct draw")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Color of the arrow (hex)")]
                public string ArrowColor { get; set; }

                [JsonProperty(PropertyName = "Color of the text (hex)")]
                public string TextColor { get; set; }

                [JsonProperty(PropertyName = "Color of the sphere (hex)")]
                public string SphereColor { get; set; }

                [JsonProperty(PropertyName = "Show sphere")]
                public bool ShowSphere { get; set; }

                [JsonProperty(PropertyName = "Show arrow")]
                public bool ShowArrow { get; set; }

                [JsonProperty(PropertyName = "Show text")]
                public bool ShowText { get; set; }

                [JsonProperty(PropertyName = "Sphere radius")]
                public float SphereRadius { get; set; }

                [JsonProperty(PropertyName = "Arrow length")]
                public float ArrowLength { get; set; }

                [JsonProperty(PropertyName = "Arrow vertical offset")]
                public float ArrowOffset { get; set; }

                [JsonProperty(PropertyName = "Text vertical offset")]
                public float TextOffset { get; set; }

                [JsonProperty(PropertyName = "Randomize XZ offset")]
                public bool RandomizeOffset { get; set; }

                [JsonProperty(PropertyName = "Direct draw duration")]
                public int Duration { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Draw = new ConfigData.DDraw
                {
                    ArrowLength = 150,
                    ArrowOffset = 1,
                    ArrowColor = "FCD12A",
                    SphereColor = "9E1A1A",
                    TextColor = "115DA8",
                    Duration = 60,
                    Enabled = true,
                    RandomizeOffset = false,
                    ShowArrow = true,
                    ShowSphere = false,
                    ShowText = true,
                    SphereRadius = 5f,
                    TextOffset = 150
                },
                Map = new ConfigData.MapMarkers
                {
                    Color = "FCD12A",
                    Duration = 300,
                    Radius = 0.28f,
                    EnableRadial = false,
                    EnableVending = true
                },
                Notification = new ConfigData.Notifications
                {
                    Debug = false,
                    NotificationDelay = 5,
                    RespawnNotification = true
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Oxide.Core.VersionNumber(2, 0, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion
    }
}
