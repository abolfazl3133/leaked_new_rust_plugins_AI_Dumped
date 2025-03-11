using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Oxide.Core.Configuration;
using UnityEngine;
using UnityEngine.Networking;


namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("Server Info GUI", "https://discord.gg/dNGbxafuJn", "1.3.3")]
    [Description("Shows various server information on modern looking Guis.")]
    public class ServerInfoGui : CovalencePlugin
    {
        private PluginConfig _config;
        [PluginReference] private Plugin ServerRewards;
        [PluginReference] private Plugin Economics;
        private static ServerInfoGui _plugin;
        public const string PermissionUse = "serverinfogui.use";
        public const string PlayerCountImagesPrefix = "PlayerLocationImage_State_";
        public const string BalanceImagesPrefix = "PlayerBalance_State_";
        private DynamicConfigFile _dataManager;
        private PluginData _pluginData;
        private ImageManagerBehaviour _imageManager;
        private readonly Dictionary<ulong, BaseEntity> _chinookCrates = new Dictionary<ulong, BaseEntity>();
        private readonly Dictionary<ulong, BaseEntity> _oilRigCrates = new Dictionary<ulong, BaseEntity>();
        private readonly Dictionary<ulong, BaseEntity> _hackingOilRigCrates = new Dictionary<ulong, BaseEntity>();
        private readonly Dictionary<ulong, BaseEntity> _hackingChinookCrates = new Dictionary<ulong, BaseEntity>();
        private readonly Dictionary<ulong, BaseEntity> _patrolHelicopters = new Dictionary<ulong, BaseEntity>();
        private readonly Dictionary<ulong, BaseEntity> _cargoShips = new Dictionary<ulong, BaseEntity>();
        private readonly Dictionary<ulong, BaseEntity> _bradleyHostiles = new Dictionary<ulong, BaseEntity>();
        private int _bradleyDestroyedCount = 0;
        private readonly Dictionary<ulong, BaseEntity> _airDrops = new Dictionary<ulong, BaseEntity>();
        private readonly Dictionary<ulong, BaseEntity> _supplyDrops = new Dictionary<ulong, BaseEntity>();
        public delegate void UpdateUiEventHandler();
        public event UpdateUiEventHandler OnUiEventUpdated;
        private List<MonumentInfo> _oilRigs = new List<MonumentInfo>();
        private bool _serverInitialized = false;
        private int _lastServerMaxPlayersCount = 0;

        private Dictionary<string, GuiConfig> _playerGuiConfigsCache { get; set; } = new Dictionary<string, GuiConfig>();
        public static class ConsoleCommands
        {
            public const string ToggleLocationEnabled = "serverinfogui.toggle.location";
            public const string ToggleWeatherEnabled = "serverinfogui.toggle.weather";
            public const string ToggleEventsEnabled = "serverinfogui.toggle.events";
            public const string TogglePlayersCountEnabled = "serverinfogui.toggle.players";
            public const string ToggleBalanceEnabled = "serverinfogui.toggle.balance";
            public const string SaveChanges = "serverinfogui.config.save";
            public const string ResetChanges = "serverinfogui.config.reset";
        }
        public class WeatherData
        {
            public string Name { get; set; }
            public float Value { get; set; }
            public string ImageUrl { get; set; }
            public string Text { get; set; }
            public override bool Equals(object obj)
            {
                var weatherData = obj as WeatherData;
                if (weatherData == null)
                    return false;
                return weatherData.Name?.ToLower() == Name?.ToLower();
            }
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
        public WeatherData GetWeatherData(BasePlayer player)
        {
            if (TOD_Sky.Instance.IsNight)
            {
                return new WeatherData { Text = "NIGHT", Name = nameof(GuiImages.WeatherNight), ImageUrl = _config.GuiImages.WeatherNight };
            }
            else
            {
                var position = player.transform.position;
                var conditions = new List<WeatherData>
                {
                    new WeatherData { Text = "RAINY", Name = nameof(GuiImages.WeatherRainy), Value = Climate.GetRain(position), ImageUrl = _config.GuiImages.WeatherRainy },
                    new WeatherData { Text = "FOGGY", Name = nameof(GuiImages.WeatherFoggy), Value = Climate.GetFog(position), ImageUrl = _config.GuiImages.WeatherFoggy },
                    new WeatherData { Text = "WINDY", Name = nameof(GuiImages.WeatherWindy), Value = Climate.GetWind(position), ImageUrl = _config.GuiImages.WeatherWindy },
                    new WeatherData { Text = "THUNDER", Name = nameof(GuiImages.WeatherThunder), Value = Climate.GetThunder(position), ImageUrl = _config.GuiImages.WeatherThunder },
                    new WeatherData { Text = "RAINBOW", Name = nameof(GuiImages.WeatherRainbow), Value = Climate.GetRainbow(position), ImageUrl = _config.GuiImages.WeatherRainbow }
                };
                if (conditions.All(c => c.Value <= 0))
                    return new WeatherData { Text = "SUNNY", Name = nameof(GuiImages.WeatherSunny), ImageUrl = _config.GuiImages.WeatherSunny };
                var weather = conditions.Aggregate((c1, c2) => c1.Value > c2.Value ? c1 : c2);
                return new WeatherData { Name = weather.Name, ImageUrl = weather.ImageUrl, Text = weather.Text };
            }
        }

        #region Config
        protected override void SaveConfig() => Config.WriteObject(_config, true);
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();

            if (_config == null)
            {
                _config = new PluginConfig();
            }
            SaveConfig();
        }
        private void ApplyDefaultConfigForPlayer(IPlayer player)
        {
            GuiConfig guiConfig;
            if (!_pluginData.PlayerGuiConfigs.TryGetValue(player.Id, out guiConfig) || guiConfig == null)
            {
                guiConfig = new GuiConfig();
                _playerGuiConfigsCache[player.Id] = guiConfig;
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
            }
            guiConfig.BalanceEnabled = true;
            guiConfig.EventsEnabled = _config.EventAlertAirdropSpawn || _config.EventAlertAirdropSupplySignal
                || _config.EventAlertBradleyHostile || _config.EventAlertBradleyKill
                || _config.EventAlertCargoShipSpawn || _config.EventAlertChinookCrateDrop
                || _config.EventAlertChinookCrateHack || _config.EventAlertOilRigCrateHack
                || _config.EventAlertPatrolHelicopterSpawn;
            guiConfig.LocationEnabled = _config.LocationOfPlayerEnabled;
            guiConfig.PlayersEnabled = true;
            guiConfig.WeatherEnabled = _config.WeatherInformationEnabled;
            _playerGuiConfigsCache[player.Id] = guiConfig;
        }
        private void ApplyDefaultConfig()
        {
            _config = new PluginConfig
            {
                PlayerBalanceLocation = GetPlayerBalanceMargins(true),
                PlayersCountLocation = GetPlayersCountMargins(true),
                ChinookLocation = GetChinookMargins(true),
                OilRigsLocation = GetOilRigMargins(true),
                CargoShipLocation = GetCargoShipMargins(true),
                HelicopterLocation = GetHelicopterMargins(true),
                BradleyLocation = GetBradleyMargins(true),
                AirDropLocation = GetAirDropMargins(true)
            };

            if (_config.GuiImages.PlayersCountImages == null || !_config.GuiImages.PlayersCountImages.Any())
                _config.GuiImages.PlayersCountImages = new List<ProgressImage>
                {
                    new ProgressImage{Value = 0,Url = "https://i.ibb.co/f4z0ydB/players-01.png"},
                    new ProgressImage{Value = 6,Url = "https://i.ibb.co/nz8pR8X/players-02.png"},
                    new ProgressImage{Value = 12,Url = "https://i.ibb.co/6ZKkRXc/players-03.png"},
                    new ProgressImage{Value = 18,Url = "https://i.ibb.co/2Mdg2XD/players-04.png"},
                    new ProgressImage{Value = 24,Url = "https://i.ibb.co/cwdpKbV/players-05.png"},
                    new ProgressImage{Value = 30,Url = "https://i.ibb.co/zSxv0Nz/players-06.png"},
                    new ProgressImage{Value = 36,Url = "https://i.ibb.co/8zvht6L/players-07.png"},
                    new ProgressImage{Value = 42,Url = "https://i.ibb.co/xDjZwKx/players-08.png"},
                    new ProgressImage{Value = 48,Url = "https://i.ibb.co/5kgP0dJ/players-09.png"},
                    new ProgressImage{Value = 54,Url = "https://i.ibb.co/LRGzmFP/players-10.png"},
                    new ProgressImage{Value = 60,Url = "https://i.ibb.co/K5DYN24/players-11.png"},
                    new ProgressImage{Value = 66,Url = "https://i.ibb.co/CMLbwJQ/players-12.png"},
                    new ProgressImage{Value = 72,Url = "https://i.ibb.co/Nry8yhM/players-13.png"},
                    new ProgressImage{Value = 78,Url = "https://i.ibb.co/gTHstdG/players-14.png"},
                    new ProgressImage{Value = 90,Url = "https://i.ibb.co/9NmmFZ0/players-15.png"},
                    new ProgressImage{Value = 100,Url = "https://i.ibb.co/jkp8Rtp/players-16.png"},
                };

            if (_config.GuiImages.BalanceImages == null || !_config.GuiImages.BalanceImages.Any())
                _config.GuiImages.BalanceImages = new List<ProgressImage>
                {
                    new ProgressImage{Value = 0,Url = "https://i.ibb.co/VmPZ504/balance-01.png"},
                    new ProgressImage{Value = 10,Url = "https://i.ibb.co/PcPRCyk/balance-02.png"},
                    new ProgressImage{Value = 20,Url = "https://i.ibb.co/GMh25P8/balance-03.png"},
                    new ProgressImage{Value = 30,Url = "https://i.ibb.co/4fN77G0/balance-04.png"},
                    new ProgressImage{Value = 40,Url = "https://i.ibb.co/PZ5xhTc/balance-05.png"},
                    new ProgressImage{Value = 50,Url = "https://i.ibb.co/wYsgBT7/balance-06.png"},
                    new ProgressImage{Value = 100,Url = "https://i.ibb.co/Jd9Ytyp/balance-07.png"},
                    new ProgressImage{Value = 500,Url = "https://i.ibb.co/HhmhKJD/balance-08.png"},
                    new ProgressImage{Value = 1000,Url = "https://i.ibb.co/JtX5qrx/balance-09.png"},
                    new ProgressImage{Value = 10000,Url = "https://i.ibb.co/xSWdjWn/balance-10.png"},
                    new ProgressImage{Value = 20000,Url = "https://i.ibb.co/F8bW96f/balance-11.png"},
                    new ProgressImage{Value = 50000,Url = "https://i.ibb.co/8ghZSFz/balance-12.png"},
                    new ProgressImage{Value = 100000,Url = "https://i.ibb.co/qpwDJWW/balance-13.png"},
                    new ProgressImage{Value = 200000,Url = "https://i.ibb.co/K94rb1C/balance-14.png"},
                    new ProgressImage{Value = 500000,Url = "https://i.ibb.co/j8kL6ds/balance-15.png"},
                    new ProgressImage{Value = 1000000,Url = "https://i.ibb.co/v3MmkYw/balance-16.png"}
                };
        }
        protected override void LoadDefaultConfig()
        {
            if (_config == null)
            {
                ApplyDefaultConfig();
                SaveConfig();
            }
        }
        public class PluginConfig
        {
            [JsonProperty(PropertyName = "Config Menu Command")]
            public string MenuCommand { get; set; } = "config";

            [JsonProperty(PropertyName = "Game Time - Enabled")]
            public bool GameTimeEnabled { get; set; } = true;

            [JsonProperty(PropertyName = "Players Count - Online - Enabled")]
            public bool ShowOnlineCount { get; set; } = true;

            [JsonProperty(PropertyName = "Balance - Economics (Server Rewards = false)")]
            public bool BalanceEconomicsEnabled { get; set; } = true;

            [JsonProperty(PropertyName = "Server Rewards Symbol")]
            public string ServerRewardsSymbol { get; set; } = "RP";

            [JsonProperty(PropertyName = "Economics Symbol")]
            public string EconomicsSymbol { get; set; } = "Money";

            [JsonProperty(PropertyName = "Weather - Enabled")]
            public bool WeatherInformationEnabled { get; set; } = true;

            [JsonProperty(PropertyName = "Player Location - Grid - Enabled")]
            public bool LocationOfPlayerEnabled { get; set; } = true;

            [JsonProperty(PropertyName = "Event Alert - Bradley - Hostile")]
            public bool EventAlertBradleyHostile { get; set; } = true;

            [JsonProperty(PropertyName = "Event Alert - Bradley - Kill")]
            public bool EventAlertBradleyKill { get; set; } = false;

            [JsonProperty(PropertyName = "Event Alert - Patrol Helicopter - Spawn")]
            public bool EventAlertPatrolHelicopterSpawn { get; set; } = true;

            [JsonProperty(PropertyName = "Event Alert - Airdrop - Spawn")]
            public bool EventAlertAirdropSpawn { get; set; } = true;

            [JsonProperty(PropertyName = "Event Alert - Airdrop - Supply Signal")]
            public bool EventAlertAirdropSupplySignal { get; set; } = true;

            [JsonProperty(PropertyName = "Event Alert - Oil Rig - Crate Hack")]
            public bool EventAlertOilRigCrateHack { get; set; } = true;

            [JsonProperty(PropertyName = "Event Alert - Chinook - Crate Drop")]
            public bool EventAlertChinookCrateDrop { get; set; } = true;

            [JsonProperty(PropertyName = "Event Alert - Chinook - Crate Hack")]
            public bool EventAlertChinookCrateHack { get; set; } = false;

            [JsonProperty(PropertyName = "Event Alert - Cargo Ship - Spawn")]
            public bool EventAlertCargoShipSpawn { get; set; } = true;

            /*[JsonProperty(PropertyName = "Event Trigger Visibility Duration In Seconds")]
            public float EventTriggerVisibilityDuration { get; set; } = 3f;*/

            [JsonProperty(PropertyName = "Weather Info UI Update Interval In Seconds")]
            public float WeatherInfoUiUpdateInterval { get; set; } = 3f;

            [JsonProperty(PropertyName = "Player Location UI Update Interval In Seconds")]
            public float PlayerLocationUiUpdateInterval { get; set; } = 3f;

            [JsonProperty(PropertyName = "Player Location Show Coordinates")]
            public bool PlayerLocationShowCoordinates { get; set; }

            [JsonProperty(PropertyName = "Player Balance UI Update Interval In Seconds")]
            public float PlayerBalanceUiUpdateInterval { get; set; } = 3f;

            [JsonProperty(PropertyName = "Players Statistics UI Update Interval In Seconds")]
            public float PlayersStatisticsUiUpdateInterval { get; set; } = 3f;

            [JsonProperty(PropertyName = "Player Balance GUI Location")]
            public Margin PlayerBalanceLocation { get; set; }

            [JsonProperty(PropertyName = "Players Count GUI Location")]
            public Margin PlayersCountLocation { get; set; }

            [JsonProperty(PropertyName = "Chinook GUI Location")]
            public Margin ChinookLocation { get; set; }

            [JsonProperty(PropertyName = "Oil Rigs GUI Location")]
            public Margin OilRigsLocation { get; set; }

            [JsonProperty(PropertyName = "Cargo Ship GUI Location")]
            public Margin CargoShipLocation { get; set; }

            [JsonProperty(PropertyName = "Patrol Helicopter GUI Location")]
            public Margin HelicopterLocation { get; set; }

            [JsonProperty(PropertyName = "Bradley GUI Location")]
            public Margin BradleyLocation { get; set; }

            [JsonProperty(PropertyName = "Air Drop GUI Location")]
            public Margin AirDropLocation { get; set; }

            [JsonProperty(PropertyName = "GUI Images")]
            public GuiImages GuiImages { get; set; } = new GuiImages();
        }

        public class Margin
        {
            public Margin(int top, int right, int bottom, int left)
            {
                Top = top;
                Right = right;
                Bottom = bottom;
                Left = left;
            }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
            public int Left { get; set; }

            public override string ToString() => $"Top: {Top}, Right: {Right}, Bottom: {Bottom}, Left: {Left}";
        }
        public class GuiImages
        {
            [JsonProperty(PropertyName = "Check Mark Icon")]
            public string Checked { get; set; } = "https://i.ibb.co/CPCK3cs/check.png";

            [JsonProperty(PropertyName = "Player Section Icon")]
            public string PlayerSection { get; set; } = "https://i.ibb.co/Rps0JgG/player-location-3x.png";

            [JsonProperty(PropertyName = "Player Coordinates Icon")]
            public string PlayerCoordinates { get; set; } = "https://i.ibb.co/dbYZfv6/player-coordinates-3x.png";

            #region Weather Images
            [JsonProperty(PropertyName = "Weather - Sunny")]
            public string WeatherSunny { get; set; } = "https://i.ibb.co/89QCpPX/weather-sunny-3x.png";

            [JsonProperty(PropertyName = "Weather - Night")]
            public string WeatherNight { get; set; } = "https://i.ibb.co/k84xm00/weather-night-3x.png";

            [JsonProperty(PropertyName = "Weather - Rainy")]
            public string WeatherRainy { get; set; } = "https://i.ibb.co/3kTKGmv/weather-rainy-3x.png";

            [JsonProperty(PropertyName = "Weather - Foggy")]
            public string WeatherFoggy { get; set; } = "https://i.ibb.co/s6PDXQB/weather-foggy-3x.png";

            [JsonProperty(PropertyName = "Weather - Thunder")]
            public string WeatherThunder { get; set; } = "https://i.ibb.co/4gxrvCF/weather-thunder-3x.png";

            [JsonProperty(PropertyName = "Weather - Windy")]
            public string WeatherWindy { get; set; } = "https://i.ibb.co/CMdNkkZ/weather-windy-3x.png";

            [JsonProperty(PropertyName = "Weather - Rainbow")]
            public string WeatherRainbow { get; set; } = "https://i.ibb.co/k3dNj3b/weather-rainbow-3x.png";
            #endregion Weather Images

            #region Events Images
            [JsonProperty(PropertyName = "Events - Chinook Crate Drop - Active")]
            public string EventsChinookDropActive { get; set; } = "https://i.imgur.com/rIPW6L9.png";

            [JsonProperty(PropertyName = "Events - Chinook Crate Drop - Inactive")]
            public string EventsChinookDropInactive { get; set; } = "https://i.imgur.com/IFbIr5l.png";

            [JsonProperty(PropertyName = "Events - Locked Crate On Oil Rig - Active")]
            public string EventsOilRigCrateActive { get; set; } = "https://i.imgur.com/TnOIJz5.png";

            [JsonProperty(PropertyName = "Events - Locked Crate On Oil Rig - Inactive")]
            public string EventsOilRigCrateInactive { get; set; } = "https://i.imgur.com/dv8uva9.png";

            [JsonProperty(PropertyName = "Events - Spawn Cargo Ship - Active")]
            public string EventsSpawnCargoShipActive { get; set; } = "https://i.imgur.com/2vCIes5.png";

            [JsonProperty(PropertyName = "Events - Spawn Cargo Ship - Inactive")]
            public string EventsSpawnCargoShipInactive { get; set; } = "https://i.imgur.com/KaQ1EO0.png";

            [JsonProperty(PropertyName = "Events - Spawn Patrol Helicopter - Active")]
            public string EventsSpawnPatrolHelicopterActive { get; set; } = "https://i.imgur.com/4CcJYBm.png";

            [JsonProperty(PropertyName = "Events - Spawn Patrol Helicopter - Inactive")]
            public string EventsSpawnPatrolHelicopterInactive { get; set; } = "https://i.imgur.com/hNixysj.png";

            [JsonProperty(PropertyName = "Events - Destroy of Bradley on Launch Site - Active")]
            public string EventsDestroyBradleyOnLaunchsiteActive { get; set; } = "https://i.imgur.com/Xvdobts.png";

            [JsonProperty(PropertyName = "Events - Destroy of Bradley on Launch Site - Inactive")]
            public string EventsDestroyBradleyOnLaunchsiteInactive { get; set; } = "https://i.imgur.com/xjWhr1J.png";

            [JsonProperty(PropertyName = "Events - Server Airdrop Spawn - Active")]
            public string EventsAirdropActive { get; set; } = "https://i.imgur.com/VrldJO7.png";

            [JsonProperty(PropertyName = "Events - Server Airdrop Spawn - Inactive")]
            public string EventsAirdropInactive { get; set; } = "https://i.imgur.com/UFY232k.pngg";
            #endregion Events Images

            #region Progress Images

            [JsonProperty(PropertyName = "Players Count Progress Images")]
            public List<ProgressImage> PlayersCountImages { get; set; } = new List<ProgressImage>();

            [JsonProperty(PropertyName = "RP/Money Progress Images")]
            public List<ProgressImage> BalanceImages { get; set; } = new List<ProgressImage>();
            #endregion Players Count Images
        }
        public class ProgressImage
        {
            public double Value { get; set; }

            public string Url { get; set; }
        }
        #endregion Config

        #region Data
        private void SaveData()
        {
            _dataManager.WriteObject(_pluginData);
        }
        private void LoadData()
        {
            try
            {
                _pluginData = _dataManager.ReadObject<PluginData>();
                foreach (var data in _pluginData.PlayerGuiConfigs)
                {
                    _playerGuiConfigsCache[data.Key] = data.Value;
                }
            }
            catch (Exception exception)
            {
                PrintError("Data file is corrupt, error message:");
                PrintError(exception.Message);
                if (exception.InnerException != null)
                {
                    PrintError($"Inner exception message: {exception.InnerException.Message}");
                }
                PrintWarning("New data file created");
                _pluginData = new PluginData();
                SaveData();
            }
        }
        public class PluginData
        {
            [JsonProperty(PropertyName = "Players Gui Config")]
            public Dictionary<string, GuiConfig> PlayerGuiConfigs { get; set; } = new Dictionary<string, GuiConfig>();
        }
        public class GuiConfig
        {
            public bool TimeEnabled { get; set; } = true;
            public bool LocationEnabled { get; set; } = true;
            public bool WeatherEnabled { get; set; } = true;
            public bool EventsEnabled { get; set; } = true;
            public bool PlayersEnabled { get; set; } = true;
            public bool BalanceEnabled { get; set; } = true;
        }
        #endregion Data

        #region Image Helpers
        public class ImageData
        {
            public ImageData()
            {
                IsUrl = true;
                Image = string.Empty;
            }
            public string Image { get; set; }
            public bool IsUrl { get; set; }
        }
        public ImageData GetImage(string name, string url)
        {
            if (_imageManager == null)
            {
                _imageManager = new GameObject("ImageManagerGameObject").AddComponent<ImageManagerBehaviour>();
                return new ImageData
                {
                    Image = url,
                    IsUrl = true
                };
            }

            var image = _imageManager.GetImage(name);
            if (string.IsNullOrWhiteSpace(image))
            {
                ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage(name, url));
                return new ImageData
                {
                    Image = url,
                    IsUrl = true
                };
            }
            return new ImageData
            {
                Image = image,
                IsUrl = false
            };
        }
        public ImageData GetWeatherImage(BasePlayer player)
        {
            var weather = GetWeatherData(player);
            return GetImage(weather.Name, weather.ImageUrl);
        }

        public ImageData GetPlayerLocationImage()
        {
            return _config.PlayerLocationShowCoordinates ?
                GetImage(nameof(GuiImages.PlayerCoordinates), _config.GuiImages.PlayerCoordinates) :
                GetImage(nameof(GuiImages.PlayerSection), _config.GuiImages.PlayerSection);
        }
        public ImageData GetChinookImage()
        {
            if ((_config.EventAlertChinookCrateDrop && _chinookCrates.Count > 0)
                || (_config.EventAlertChinookCrateHack && _hackingChinookCrates.Count > 0))
                return GetImage(nameof(GuiImages.EventsChinookDropActive), _config.GuiImages.EventsChinookDropActive);
            return GetImage(nameof(GuiImages.EventsChinookDropInactive), _config.GuiImages.EventsChinookDropInactive);
        }
        public ImageData GetOilRigImage()
        {
            return _hackingOilRigCrates.Count > 0
                ? GetImage(nameof(GuiImages.EventsOilRigCrateActive), _config.GuiImages.EventsOilRigCrateActive)
                : GetImage(nameof(GuiImages.EventsOilRigCrateInactive), _config.GuiImages.EventsOilRigCrateInactive);
        }
        public ImageData GetCargoShipImage()
        {
            return _cargoShips.Count > 0
                ? GetImage(nameof(GuiImages.EventsSpawnCargoShipActive), _config.GuiImages.EventsSpawnCargoShipActive)
                : GetImage(nameof(GuiImages.EventsSpawnCargoShipInactive), _config.GuiImages.EventsSpawnCargoShipInactive);
        }
        public ImageData GetHelicopterImage()
        {
            return _patrolHelicopters.Count > 0
                ? GetImage(nameof(GuiImages.EventsSpawnPatrolHelicopterActive), _config.GuiImages.EventsSpawnPatrolHelicopterActive)
                : GetImage(nameof(GuiImages.EventsSpawnPatrolHelicopterInactive), _config.GuiImages.EventsSpawnPatrolHelicopterInactive);
        }
        public ImageData GetBradleyImage()
        {
            if ((_config.EventAlertBradleyHostile && _bradleyHostiles.Count > 0)
                || (_config.EventAlertBradleyKill && _bradleyDestroyedCount > 0))
                return GetImage(nameof(GuiImages.EventsDestroyBradleyOnLaunchsiteActive), _config.GuiImages.EventsDestroyBradleyOnLaunchsiteActive);
            return GetImage(nameof(GuiImages.EventsDestroyBradleyOnLaunchsiteInactive), _config.GuiImages.EventsDestroyBradleyOnLaunchsiteInactive);
        }
        public ImageData GetAirdropImage()
        {
            if ((_config.EventAlertAirdropSpawn && _airDrops.Count > 0)
                 || (_config.EventAlertAirdropSupplySignal && _supplyDrops.Count > 0))
                return GetImage(nameof(GuiImages.EventsAirdropActive), _config.GuiImages.EventsAirdropActive);
            return GetImage(nameof(GuiImages.EventsAirdropInactive), _config.GuiImages.EventsAirdropInactive);
        }
        public ImageData GetPlayersCountImage()
        {
            var activePlayersCount = BasePlayer.activePlayerList.Count;
            var maxServerPlayers = server.MaxPlayers; ;
            var percentage = (double)activePlayersCount * 100 / maxServerPlayers;
            var image = _config.GuiImages.PlayersCountImages
                .OrderByDescending(x => x.Value)
                .FirstOrDefault(x => x.Value <= percentage);
            if (image == null)
            {
                return new ImageData();
            }
            return GetImage($"{PlayerCountImagesPrefix}{image.Value}", image.Url);

        }
        public ImageData GetBalanceImage(double balance)
        {
            var image = _config.GuiImages.BalanceImages
                .OrderByDescending(x => x.Value)
                .FirstOrDefault(x => x.Value <= balance);
            if (image == null)
            {
                return new ImageData();
            }
            return GetImage($"{BalanceImagesPrefix}{image.Value}", image.Url);
        }
        #endregion Image Helpers

        #region Behaviors
        public class GuiManager : MonoBehaviour
        {
            private BasePlayer _player;
            private double _playerBalance = 0;
            private float _deltaTimeWeather = 0;
            private float _deltaTimePlayerLocation = 0;
            private float _deltaTimeGameTime = 0;
            private float _deltaTimeDownloadBootstraper = 0;
            private float _deltaTimePlayerBalance = 0;
            private float _deltaTimePlayersCount = 0;
            private float _downloadBootstraperTime = 20f;
            private WeatherData _lastWeatherData = new WeatherData();
            private string _lastLocation = "";
            public void ShowAll()
            {
                ShowBalance();
                ShowPlayerLocationImage();
                ShowPlayerLocation();
                ShowGameTime();
                ShowWeatherInfo();
                ShowPlayersCount();
                ShowEventsToolbar();
            }
            public void OnUiEventUpdated()
            {
                if (_plugin == null)
                    return;
                ShowEventsToolbar();
            }
            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                if (_player == null)
                {
                    enabled = false;
                    return;
                }
                _plugin.OnUiEventUpdated += OnUiEventUpdated;
                var balance = 0d;
                if (!_plugin._config.BalanceEconomicsEnabled)
                {
                    balance = _plugin.GetServerRewardPoints(_player.userID);
                }
                if (_plugin._config.BalanceEconomicsEnabled)
                {
                    balance = _plugin.GetEconomicsBalance(_player.userID);
                }
                SetPlayerBalance(balance);
                ShowAll();
            }
            private void Update()
            {
                if (_plugin == null)
                    return;
                _deltaTimeWeather += Time.deltaTime;
                _deltaTimePlayerBalance += Time.deltaTime;
                _deltaTimePlayerLocation += Time.deltaTime;
                _deltaTimeGameTime += Time.deltaTime;
                _deltaTimeDownloadBootstraper += Time.deltaTime;
                _deltaTimePlayersCount += Time.deltaTime;
                if (_deltaTimeWeather >= _plugin._config.WeatherInfoUiUpdateInterval)
                {
                    _deltaTimeWeather = 0;
                    var currentWeatherInfo = _plugin.GetWeatherData(_player);
                    if (!currentWeatherInfo.Equals(_lastWeatherData))
                    {
                        ShowWeatherInfo();
                        _lastWeatherData = currentWeatherInfo;
                    }

                }
                if (_deltaTimePlayerBalance >= _plugin._config.PlayerBalanceUiUpdateInterval)
                {
                    _deltaTimePlayerBalance = 0;
                    var points = 0d;
                    if (_plugin._config.BalanceEconomicsEnabled)
                        points = _plugin.GetEconomicsBalance(_player.userID);
                    else
                        points = _plugin.GetServerRewardPoints(_player.userID);
                    SetPlayerBalance(points);
                }
                if (_deltaTimePlayerLocation >= _plugin._config.PlayerLocationUiUpdateInterval)
                {
                    var currentLocation = _plugin.GetGrid(_player.ServerPosition);
                    _deltaTimePlayerLocation = 0;
                    if (_lastLocation != currentLocation)
                    {
                        _lastLocation = currentLocation;
                        ShowPlayerLocation();
                        ShowPlayerLocationImage();
                    }
                }
                if (_deltaTimeGameTime >= 1)
                {
                    _deltaTimeGameTime = 0;
                    ShowGameTime();
                }
                if (_deltaTimeDownloadBootstraper >= 3 && _downloadBootstraperTime > 0)
                {
                    _deltaTimeDownloadBootstraper = 0;
                    _downloadBootstraperTime -= 3;
                    ShowPlayersCount();
                    ShowEventsToolbar();
                    //ShowPlayerLocationImage();
                }
                if (_deltaTimePlayersCount >= _plugin._config.PlayersStatisticsUiUpdateInterval
                    && _plugin._lastServerMaxPlayersCount != _plugin.server.MaxPlayers)
                {
                    _plugin._lastServerMaxPlayersCount = _plugin.server.MaxPlayers;
                    _deltaTimePlayersCount = 0;
                    ShowPlayersCount();
                }
            }
            public void ShowEventsToolbar()
            {
                if (!_plugin._config.EventAlertAirdropSpawn &&
                    !_plugin._config.EventAlertAirdropSupplySignal &&
                    !_plugin._config.EventAlertBradleyHostile &&
                    !_plugin._config.EventAlertBradleyKill &&
                    !_plugin._config.EventAlertCargoShipSpawn &&
                    !_plugin._config.EventAlertChinookCrateDrop &&
                    !_plugin._config.EventAlertChinookCrateHack &&
                    !_plugin._config.EventAlertOilRigCrateHack &&
                    !_plugin._config.EventAlertPatrolHelicopterSpawn)
                {
                    CuiHelper.DestroyUi(_player, Ui.Panels.EventsToolbar.ToString());
                    return;
                }
                GuiConfig guiConfig;
                if (_plugin._playerGuiConfigsCache.TryGetValue(_player.UserIDString, out guiConfig) && guiConfig != null)
                {
                    if (!guiConfig.EventsEnabled)
                    {
                        CuiHelper.DestroyUi(_player, Ui.Panels.EventsToolbar.ToString());
                        return;
                    }
                }
                var mainContainer = Ui.Container(Ui.Panels.EventsToolbar, Ui.Color(Ui.ColorCode.Black, 0f), Ui.GetMin(0, 0), Ui.GetMax(0, 0), parent: Ui.Panels.Under);
                //chinook image position
                var chinookCrateImagePosition = new
                {
                    Min = Ui.GetMin(_plugin._config.ChinookLocation.Left, _plugin._config.ChinookLocation.Bottom),
                    Max = Ui.GetMax(_plugin._config.ChinookLocation.Right, _plugin._config.ChinookLocation.Top)
                };

                //oil rig image position
                var oilRigImagePosition = new
                {
                    Min = Ui.GetMin(_plugin._config.OilRigsLocation.Left, _plugin._config.OilRigsLocation.Bottom),
                    Max = Ui.GetMax(_plugin._config.OilRigsLocation.Right, _plugin._config.OilRigsLocation.Top)
                };

                //cargo ship image position
                var cargoShipImagePosition = new
                {
                    Min = Ui.GetMin(_plugin._config.CargoShipLocation.Left, _plugin._config.CargoShipLocation.Bottom),
                    Max = Ui.GetMax(_plugin._config.CargoShipLocation.Right, _plugin._config.CargoShipLocation.Top)
                };

                //helicopter image position
                var helicopterImagePosition = new
                {
                    Min = Ui.GetMin(_plugin._config.HelicopterLocation.Left, _plugin._config.HelicopterLocation.Bottom),
                    Max = Ui.GetMax(_plugin._config.HelicopterLocation.Right, _plugin._config.HelicopterLocation.Top)
                };

                //Bradley image position
                var bradleyImagePosition = new
                {
                    Min = Ui.GetMin(_plugin._config.BradleyLocation.Left, _plugin._config.BradleyLocation.Bottom),
                    Max = Ui.GetMax(_plugin._config.BradleyLocation.Right, _plugin._config.BradleyLocation.Top)
                };

                //airdrop image position
                var airDropImagePosition = new
                {
                    Min = Ui.GetMin(_plugin._config.AirDropLocation.Left, _plugin._config.AirDropLocation.Bottom),
                    Max = Ui.GetMax(_plugin._config.AirDropLocation.Right, _plugin._config.AirDropLocation.Top)
                };


                //chinook crate
                var chinookImage = _plugin.GetChinookImage();
                Ui.Image(ref mainContainer,
                    Ui.Panels.EventsToolbar,
                    Ui.Color(Ui.ColorCode.White),
                    chinookImage.Image,
                    chinookImage.IsUrl,
                    chinookCrateImagePosition.Min,
                    chinookCrateImagePosition.Max);

                //oil rig crate
                var oilRigImage = _plugin.GetOilRigImage();
                Ui.Image(ref mainContainer,
                    Ui.Panels.EventsToolbar,
                    Ui.Color(Ui.ColorCode.White),
                    oilRigImage.Image,
                    oilRigImage.IsUrl,
                    oilRigImagePosition.Min,
                    oilRigImagePosition.Max);

                //cargo ship
                var cargoShipImage = _plugin.GetCargoShipImage();
                Ui.Image(ref mainContainer,
                    Ui.Panels.EventsToolbar,
                    Ui.Color(Ui.ColorCode.White),
                    cargoShipImage.Image,
                    cargoShipImage.IsUrl,
                    cargoShipImagePosition.Min,
                    cargoShipImagePosition.Max);

                //helicopter
                var helicopterImage = _plugin.GetHelicopterImage();
                Ui.Image(ref mainContainer,
                    Ui.Panels.EventsToolbar,
                    Ui.Color(Ui.ColorCode.White),
                    helicopterImage.Image,
                    helicopterImage.IsUrl,
                    helicopterImagePosition.Min,
                    helicopterImagePosition.Max);

                //Bradley
                var bradleyImage = _plugin.GetBradleyImage();
                Ui.Image(ref mainContainer,
                    Ui.Panels.EventsToolbar,
                    Ui.Color(Ui.ColorCode.White),
                    bradleyImage.Image,
                    bradleyImage.IsUrl,
                    bradleyImagePosition.Min,
                    bradleyImagePosition.Max);

                //airdrop
                var airdropImage = _plugin.GetAirdropImage();
                Ui.Image(ref mainContainer,
                    Ui.Panels.EventsToolbar,
                    Ui.Color(Ui.ColorCode.White),
                    airdropImage.Image,
                    airdropImage.IsUrl,
                    airDropImagePosition.Min,
                    airDropImagePosition.Max);
                CuiHelper.DestroyUi(_player, Ui.Panels.EventsToolbar.ToString());
                CuiHelper.AddUi(_player, mainContainer);
            }
            public void ShowPlayersCount()
            {
                if (!_plugin._config.ShowOnlineCount)
                    return;

                GuiConfig guiConfig;
                if (_plugin._playerGuiConfigsCache.TryGetValue(_player.UserIDString, out guiConfig) && guiConfig != null)
                {
                    if (!guiConfig.PlayersEnabled)
                    {
                        CuiHelper.DestroyUi(_player, Ui.Panels.PlayersStatistics.ToString());
                        return;
                    }
                }
                //players count image position
                var playersCountImagePosition = new
                {
                    Min = Ui.GetMin(_plugin._config.PlayersCountLocation.Left, _plugin._config.PlayersCountLocation.Bottom),
                    Max = Ui.GetMax(_plugin._config.PlayersCountLocation.Right, _plugin._config.PlayersCountLocation.Top)
                };

                var mainContainer = Ui.Container(Ui.Panels.PlayersStatistics, Ui.Color(Ui.ColorCode.Black, 0f),
                    playersCountImagePosition.Min,
                    playersCountImagePosition.Max,
                    parent: Ui.Panels.Under);

                //players count
                var playersCountImage = _plugin.GetPlayersCountImage();
                Ui.Image(ref mainContainer,
                    Ui.Panels.PlayersStatistics,
                    Ui.Color(Ui.ColorCode.White),
                    playersCountImage.Image,
                    playersCountImage.IsUrl,
                    Ui.GetMin(0, 0),
                    Ui.GetMax(0, 0));

                Ui.Label(ref mainContainer,
                    Ui.Panels.PlayersStatistics,
                    BasePlayer.activePlayerList.Count.ToString(),
                    24,
                    Ui.GetMin(8, 18, 120, 120),
                    Ui.GetMax(65, 74, 120, 120),
                    textColor: Ui.Color(Ui.ColorCode.AndroidGreen),
                    align: TextAnchor.LowerRight);

                Ui.Label(ref mainContainer,
                    Ui.Panels.PlayersStatistics,
                    BasePlayer.sleepingPlayerList.Count.ToString(),
                    16,
                    Ui.GetMin(65, 20, 120, 120),
                    Ui.GetMax(23, 74, 120, 120),
                    textColor: Ui.Color(Ui.ColorCode.PaleSilver),
                    align: TextAnchor.LowerLeft);

                CuiHelper.DestroyUi(_player, Ui.Panels.PlayersStatistics.ToString());
                CuiHelper.AddUi(_player, mainContainer);
            }
            public void ShowBalance()
            {
                if (!_plugin._config.BalanceEconomicsEnabled && !_plugin.plugins.Exists(nameof(ServerRewards)))
                {
                    CuiHelper.DestroyUi(_player, Ui.Panels.Balance.ToString());
                    return;
                }
                if (_plugin._config.BalanceEconomicsEnabled && !_plugin.plugins.Exists(nameof(Economics)))
                {
                    CuiHelper.DestroyUi(_player, Ui.Panels.Balance.ToString());
                    return;
                }
                GuiConfig guiConfig;
                if (_plugin._playerGuiConfigsCache.TryGetValue(_player.UserIDString, out guiConfig) && guiConfig != null)
                {
                    if (!guiConfig.BalanceEnabled)
                    {
                        CuiHelper.DestroyUi(_player, Ui.Panels.Balance.ToString());
                        return;
                    }
                }
                //balance image position
                var balanceImagePosition = new
                {
                    Min = Ui.GetMin(_plugin._config.PlayerBalanceLocation.Left, _plugin._config.PlayerBalanceLocation.Bottom),
                    Max = Ui.GetMax(_plugin._config.PlayerBalanceLocation.Right, _plugin._config.PlayerBalanceLocation.Top)
                };

                var mainContainer = Ui.Container(Ui.Panels.Balance, Ui.Color(Ui.ColorCode.Black, 0f),
                    balanceImagePosition.Min,
                    balanceImagePosition.Max,
                    parent: Ui.Panels.Under);

                //balance
                var balanceImage = _plugin.GetBalanceImage(_playerBalance);
                Ui.Image(ref mainContainer,
                    Ui.Panels.Balance,
                    Ui.Color(Ui.ColorCode.White),
                    balanceImage.Image,
                    balanceImage.IsUrl,
                    Ui.GetMin(0, 0),
                    Ui.GetMax(0, 0));

                Ui.Label(ref mainContainer,
                   Ui.Panels.Balance,
                   _playerBalance.ToString("N0"),
                   14,
                   Ui.GetMin(18, 28, 120, 120),
                   Ui.GetMax(18, 70, 120, 120),
                   textColor: Ui.Color(Ui.ColorCode.PaleSilver));

                Ui.Label(ref mainContainer,
                  Ui.Panels.Balance,
                  !_plugin._config.BalanceEconomicsEnabled ? _plugin._config.ServerRewardsSymbol : _plugin._config.EconomicsSymbol,
                  14,
                  Ui.GetMin(30, 15, 120, 120),
                  Ui.GetMax(30, 87, 120, 120),
                  textColor: Ui.Color(Ui.ColorCode.ChocolateWeb));
                CuiHelper.DestroyUi(_player, Ui.Panels.Balance.ToString());
                CuiHelper.AddUi(_player, mainContainer);
            }
            public void ShowPlayerLocationImage()
            {
                if (!_plugin._config.LocationOfPlayerEnabled)
                {
                    CuiHelper.DestroyUi(_player, Ui.Panels.PlayerLocationImage.ToString());
                    return;
                }
                GuiConfig guiConfig;
                if (_plugin._playerGuiConfigsCache.TryGetValue(_player.UserIDString, out guiConfig) && guiConfig != null)
                {
                    if (!guiConfig.LocationEnabled)
                    {
                        CuiHelper.DestroyUi(_player, Ui.Panels.PlayerLocationImage.ToString());
                        return;
                    }
                }
                var mainContainer = Ui.Container(Ui.Panels.PlayerLocationImage, Ui.Color(Ui.ColorCode.Black, 0f), Ui.GetMin(0, 0), Ui.GetMax(0, 0), parent: Ui.Panels.Under);

                //location
                var locationImage = _plugin.GetPlayerLocationImage();

                var playerLocationImagePosition = new
                {
                    Min = Ui.GetMin(23, 915),
                    Max = Ui.GetMax(1798, 105)
                };
                if (_plugin._config.PlayerLocationShowCoordinates)
                {
                    playerLocationImagePosition = new
                    {
                        Min = Ui.GetMin(23, 915),
                        Max = Ui.GetMax(1698, 105)
                    };
                }

                Ui.Image(ref mainContainer,
                    Ui.Panels.PlayerLocationImage,
                    Ui.Color(Ui.ColorCode.White),
                    locationImage?.Image,
                    locationImage != null ? locationImage.IsUrl : false,
                    playerLocationImagePosition.Min,
                    playerLocationImagePosition.Max);
                CuiHelper.DestroyUi(_player, Ui.Panels.PlayerLocationImage.ToString());
                CuiHelper.AddUi(_player, mainContainer);
            }
            public void ShowPlayerLocation()
            {
                if (!_plugin._config.LocationOfPlayerEnabled)
                {
                    CuiHelper.DestroyUi(_player, Ui.Panels.PlayerLocationText.ToString());
                    return;
                }
                GuiConfig guiConfig;
                if (_plugin._playerGuiConfigsCache.TryGetValue(_player.UserIDString, out guiConfig) && guiConfig != null)
                {
                    if (!guiConfig.LocationEnabled)
                    {
                        CuiHelper.DestroyUi(_player, Ui.Panels.PlayerLocationText.ToString());
                        return;
                    }
                }
                var mainContainer = Ui.Container(Ui.Panels.PlayerLocationText, Ui.Color(Ui.ColorCode.Black, 0f), Ui.GetMin(0, 0), Ui.GetMax(0, 0), parent: Ui.Panels.Under);
                //location
                var playerLocationTextPosition = new
                {
                    Min = Ui.GetMin(87, 936),
                    Max = Ui.GetMax(1798, 126)
                };

                if (_plugin._config.PlayerLocationShowCoordinates)
                {
                    playerLocationTextPosition = new
                    {
                        Min = Ui.GetMin(81, 936),
                        Max = Ui.GetMax(1700, 126)
                    };
                }

                Ui.Label(ref mainContainer,
                    Ui.Panels.PlayerLocationText,
                    _plugin._config.PlayerLocationShowCoordinates ?
                        $"{_player.ServerPosition.x:F1}, {_player.ServerPosition.y:F1}, {_player.ServerPosition.z:F1}" :
                        _plugin.GetGrid(_player.ServerPosition),
                    15,
                    playerLocationTextPosition.Min,
                    playerLocationTextPosition.Max,
                    textColor: Ui.Color(Ui.ColorCode.PaleSilver),
                    align: _plugin._config.PlayerLocationShowCoordinates ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft,
                    font: Ui.Fonts.RobotoCondensedBold);
                CuiHelper.DestroyUi(_player, Ui.Panels.PlayerLocationText.ToString());
                CuiHelper.AddUi(_player, mainContainer);
            }
            public void ShowGameTime()
            {
                if (!_plugin._config.GameTimeEnabled)
                {
                    CuiHelper.DestroyUi(_player, Ui.Panels.GameTime.ToString());
                    return;
                }
                GuiConfig guiConfig;
                if (_plugin._playerGuiConfigsCache.TryGetValue(_player.UserIDString, out guiConfig) && guiConfig != null)
                {
                    if (!guiConfig.TimeEnabled)
                    {
                        CuiHelper.DestroyUi(_player, Ui.Panels.GameTime.ToString());
                        return;
                    }
                }
                var mainContainer = Ui.Container(Ui.Panels.GameTime, Ui.Color(Ui.ColorCode.Black, 0f), Ui.GetMin(0, 0), Ui.GetMax(0, 0), parent: Ui.Panels.Under);
                //location
                Ui.OutlineLabel(ref mainContainer,
                    Ui.Panels.GameTime,
                    $"{TOD_Sky.Instance.Cycle.DateTime:HH:mm}",
                    30,
                    Ui.GetMin(0, 1005),
                    Ui.GetMax(0, 40),
                    textColor: Ui.Color(Ui.ColorCode.White),
                    align: TextAnchor.MiddleCenter,
                    font: Ui.Fonts.RobotoCondensedBold);
                CuiHelper.DestroyUi(_player, Ui.Panels.GameTime.ToString());
                CuiHelper.AddUi(_player, mainContainer);
            }
            public void ShowWeatherInfo()
            {
                if (!_plugin._config.WeatherInformationEnabled)
                {
                    CuiHelper.DestroyUi(_player, Ui.Panels.WeatherInfo.ToString());
                    return;
                }
                GuiConfig guiConfig;
                if (_plugin._playerGuiConfigsCache.TryGetValue(_player.UserIDString, out guiConfig) && guiConfig != null)
                {
                    if (!guiConfig.WeatherEnabled)
                    {
                        CuiHelper.DestroyUi(_player, Ui.Panels.WeatherInfo.ToString());
                        return;
                    }
                }
                var mainContainer = Ui.Container(Ui.Panels.WeatherInfo, Ui.Color(Ui.ColorCode.Black, 0f), Ui.GetMin(0, 0), Ui.GetMax(0, 0), parent: Ui.Panels.Under);
                //weather
                var weatherText = _plugin.GetWeatherData(_player)?.Text;
                var weatherImage = _plugin.GetWeatherImage(_player);
                Ui.Image(ref mainContainer,
                    Ui.Panels.WeatherInfo,
                    Ui.Color(Ui.ColorCode.White),
                    weatherImage.Image,
                    weatherImage.IsUrl,
                    Ui.GetMin(23, 985),
                    Ui.GetMax(1771, 35));
                if (!string.IsNullOrEmpty(weatherText))
                    Ui.Label(ref mainContainer,
                        Ui.Panels.WeatherInfo,
                        weatherText,
                        15,
                        Ui.GetMin(81, 1006),
                        Ui.GetMax(1772, 56),
                        textColor: Ui.Color(Ui.ColorCode.PaleSilver),
                        align: TextAnchor.MiddleCenter,
                        font: Ui.Fonts.RobotoCondensedBold);
                CuiHelper.DestroyUi(_player, Ui.Panels.WeatherInfo.ToString());
                CuiHelper.AddUi(_player, mainContainer);
            }
            public void SetPlayerBalance(double balance)
            {
                _playerBalance = balance;
                ShowBalance();
            }

            private void OnDestroy()
            {
                Ui.ClearAllMenus(_player);
                if (_plugin == null)
                    return;
                _plugin.OnUiEventUpdated -= OnUiEventUpdated;
            }
            public void DoDestroy()
            {
                Destroy(this);
            }
        }
        public class ImageManagerBehaviour : MonoBehaviour
        {
            string dataDirectory = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}";
            private readonly Dictionary<string, string> _images = new Dictionary<string, string>();
            public string GetImage(string key)
            {
                var imageId = string.Empty;
                if (_images.TryGetValue(key, out imageId))
                {
                    return imageId;
                }
                return null;
            }
            public IEnumerator DownloadImage(string key, string url)
            {
                if (!url.StartsWith("http"))
                    url = $"{dataDirectory}{url}";
                var www = UnityWebRequest.Get(url);

                yield return www.SendWebRequest();
                if (www.isNetworkError || www.isHttpError)
                {
                    _plugin.PrintError($"Image failed to download! Error: {www.error} - Image URL: {url}");
                    www.Dispose();
                    yield break;
                }

                var texture = new Texture2D(2, 2);
                texture.LoadImage(www.downloadHandler.data);
                if (texture != null)
                {
                    byte[] bytes = texture.EncodeToPNG();
                    DestroyImmediate(texture);
                    StoreByteArray(key, bytes);
                }
                www.Dispose();
            }

            private void StoreByteArray(string key, byte[] bytes)
            {
                if (bytes != null)
                {
                    _images[key] = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                }
            }
        }
        #endregion Behaviours

        #region GUI

        private Margin GetChinookMargins(bool isFirstMode)
        {
            return isFirstMode ?
                new Margin(996, 1528, 32, 340) :
                new Margin(996, 661, 32, 1207);
        }
        private Margin GetOilRigMargins(bool isFirstMode)
        {
            return isFirstMode ?
                new Margin(996, 1470, 32, 398) :
                new Margin(996, 603, 32, 1265);
        }
        private Margin GetCargoShipMargins(bool isFirstMode)
        {
            return isFirstMode ?
                new Margin(996, 1412, 32, 456) :
                new Margin(996, 543, 32, 1324);
        }
        private Margin GetHelicopterMargins(bool isFirstMode)
        {
            return isFirstMode ?
                new Margin(996, 1355, 32, 514) :
                new Margin(996, 484, 32, 1384);
        }
        private Margin GetBradleyMargins(bool isFirstMode)
        {
            return isFirstMode ?
                new Margin(996, 1296, 32, 571) :
                new Margin(996, 424, 32, 1443);
        }
        private Margin GetAirDropMargins(bool isFirstMode)
        {
            return isFirstMode ?
                new Margin(996, 1238, 32, 630) :
                new Margin(996, 365, 32, 1503);


        }
        private Margin GetPlayersCountMargins(bool isFirstMode)
        {
            return isFirstMode ?
                new Margin(944, 1777, 16, 23) :
                new Margin(34, 19, 926, 1781);
        }
        private Margin GetPlayerBalanceMargins(bool isFirstMode)
        {
            return isFirstMode ?
                new Margin(944, 1645, 16, 155) :
                new Margin(166, 19, 794, 1781);
        }

        private void CloseUiConfigMenu(IPlayer player)
        {
            CuiHelper.DestroyUi(player.Object as BasePlayer, Ui.Panels.QuickMenu.ToString());
        }
        private void ShowUiConfigMenu(IPlayer player)
        {
            var checkImage = _imageManager.GetImage(nameof(GuiImages.Checked));
            var checkImageIsUrl = false;
            if (string.IsNullOrWhiteSpace(checkImage))
            {
                ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage(nameof(GuiImages.Checked), _config.GuiImages.Checked));
                checkImage = _config.GuiImages.Checked;
                checkImageIsUrl = true;
            }
            var basePlayer = player.Object as BasePlayer; // BasePlayer.allPlayerList.FirstOrDefault(p => p.UserIDString == player.Id);
            if (basePlayer == null)
                return;
            GuiConfig guiConfig;
            if (!_pluginData.PlayerGuiConfigs.TryGetValue(player.Id, out guiConfig) || guiConfig == null)
            {
                guiConfig = new GuiConfig();
                _playerGuiConfigsCache[player.Id] = guiConfig;
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
            }

            var mainContainer = Ui.Container(Ui.Panels.QuickMenu, Ui.Color(Ui.ColorCode.Black, 0f), Ui.GetMin(0, 0), Ui.GetMax(0, 0), true);
            //main panel background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ShadowBlack, 0.5f),
                Ui.GetMin(757, 267),
                Ui.GetMax(757, 267));
            //title background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(830, 757),
                Ui.GetMax(830, 279));
            //title white footer
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.White, 1f),
                Ui.GetMin(830, 753),
                Ui.GetMax(830, 323));
            //title text
            Ui.Label(ref mainContainer,
               Ui.Panels.QuickMenu,
               "UI CONFIG",
               22,
               Ui.GetMin(914, 766),
               Ui.GetMax(914, 288));

            //location item background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(769, 683),
                Ui.GetMax(769, 349));
            //location item text
            Ui.Label(ref mainContainer,
                Ui.Panels.QuickMenu,
                "LOCATION",
                22,
                Ui.GetMin(791, 694),
                Ui.GetMax(1038, 360),
                align: TextAnchor.MiddleLeft);

            //location checkbox border
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.Blue, 1f),
                Ui.GetMin(1111, 691),
                Ui.GetMax(777, 357));
            //location checkbox background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(1113, 693),
                Ui.GetMax(779, 359));
            //location checkbox
            Ui.ImageButton(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                _config.LocationOfPlayerEnabled && guiConfig.LocationEnabled ? checkImage : string.Empty,
                checkImageIsUrl,
                Ui.GetMin(1113, 693),
                Ui.GetMax(779, 359),
                ConsoleCommands.ToggleLocationEnabled);

            //weather item background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(769, 623),
                Ui.GetMax(769, 409));
            //weather item text
            Ui.Label(ref mainContainer,
                Ui.Panels.QuickMenu,
                "WEATHER",
                22,
                Ui.GetMin(791, 634),
                Ui.GetMax(1040, 420),
                align: TextAnchor.MiddleLeft);
            //weather checkbox border
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.Blue, 1f),
                Ui.GetMin(1111, 631),
                Ui.GetMax(777, 417));
            //weather checkbox background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(1113, 633),
                Ui.GetMax(779, 419));
            //weather checkbox
            Ui.ImageButton(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
               _config.WeatherInformationEnabled && guiConfig.WeatherEnabled ? checkImage : string.Empty,
                //checkImage,
                checkImageIsUrl,
                Ui.GetMin(1113, 633),
                Ui.GetMax(779, 419),
                ConsoleCommands.ToggleWeatherEnabled);

            //events item background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(769, 563),
                Ui.GetMax(769, 469));
            //events item text
            Ui.Label(ref mainContainer,
                Ui.Panels.QuickMenu,
                "EVENTS",
                22,
                Ui.GetMin(791, 574),
                Ui.GetMax(1058, 480),
                align: TextAnchor.MiddleLeft);
            //events checkbox border
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.Blue, 1f),
                Ui.GetMin(1111, 571),
                Ui.GetMax(777, 477));
            //events checkbox background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(1113, 573),
                Ui.GetMax(779, 479));
            //events checkbox
            Ui.ImageButton(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                (_config.EventAlertAirdropSpawn || _config.EventAlertAirdropSupplySignal
                || _config.EventAlertBradleyHostile || _config.EventAlertBradleyKill
                || _config.EventAlertCargoShipSpawn || _config.EventAlertChinookCrateDrop
                || _config.EventAlertChinookCrateHack || _config.EventAlertOilRigCrateHack
                || _config.EventAlertPatrolHelicopterSpawn) && guiConfig.EventsEnabled ? checkImage : string.Empty,
                checkImageIsUrl,
                Ui.GetMin(1113, 573),
                Ui.GetMax(779, 479),
                ConsoleCommands.ToggleEventsEnabled);

            //players item background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(769, 503),
                Ui.GetMax(769, 529));
            //players item text
            Ui.Label(ref mainContainer,
                Ui.Panels.QuickMenu,
                "PLAYERS",
                22,
                Ui.GetMin(791, 514),
                Ui.GetMax(1048, 540),
                align: TextAnchor.MiddleLeft);
            //players checkbox border
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.Blue, 1f),
                Ui.GetMin(1111, 511),
                Ui.GetMax(777, 537));
            //players checkbox background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(1113, 513),
                Ui.GetMax(779, 539));
            //players checkbox
            Ui.ImageButton(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                guiConfig.PlayersEnabled ? checkImage : string.Empty,
                checkImageIsUrl,
                Ui.GetMin(1113, 513),
                Ui.GetMax(779, 539),
                ConsoleCommands.TogglePlayersCountEnabled);

            //balance item background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(769, 443),
                Ui.GetMax(769, 589));
            //balance item text
            Ui.Label(ref mainContainer,
                Ui.Panels.QuickMenu,
                "BALANCE",
                22,
                Ui.GetMin(791, 454),
                Ui.GetMax(1043, 600),
                align: TextAnchor.MiddleLeft);
            //balance checkbox border
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.Blue, 1f),
                Ui.GetMin(1111, 451),
                Ui.GetMax(777, 597));
            //balance checkbox background
            Ui.Panel(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                Ui.GetMin(1113, 453),
                Ui.GetMax(779, 599));
            //balance checkbox
            Ui.ImageButton(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
                guiConfig.BalanceEnabled ? checkImage : string.Empty,
                checkImageIsUrl,
                Ui.GetMin(1113, 453),
                Ui.GetMax(779, 599),
                ConsoleCommands.ToggleBalanceEnabled);

            //save change panel
            Ui.Panel(ref mainContainer,
               Ui.Panels.QuickMenu,
               Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
               Ui.GetMin(769, 339),
               Ui.GetMax(769, 693));
            //save change title
            Ui.Label(ref mainContainer,
                Ui.Panels.QuickMenu,
                "SAVE CHANGES",
                22,
                Ui.GetMin(791, 350),
                Ui.GetMax(989, 704),
                align: TextAnchor.MiddleLeft);
            //save change apply button
            Ui.Button(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.Green, 1f),
                "APPLY",
                19,
                Ui.GetMin(1014, 347),
                Ui.GetMax(777, 701),
                ConsoleCommands.SaveChanges);

            //reset default change panel
            Ui.Panel(ref mainContainer,
               Ui.Panels.QuickMenu,
               Ui.Color(Ui.ColorCode.ButtonBlack, 1f),
               Ui.GetMin(769, 279),
               Ui.GetMax(769, 753));
            //reset default change title
            Ui.Label(ref mainContainer,
                Ui.Panels.QuickMenu,
                "RESET DEFAULT",
                22,
                Ui.GetMin(791, 290),
                Ui.GetMax(991, 764),
                align: TextAnchor.MiddleLeft);
            //reset button
            Ui.Button(ref mainContainer,
                Ui.Panels.QuickMenu,
                Ui.Color(Ui.ColorCode.Red, 1f),
                "RESET",
                19,
                Ui.GetMin(1014, 287),
                Ui.GetMax(777, 761),
                ConsoleCommands.ResetChanges);
            CuiHelper.DestroyUi(basePlayer, Ui.Panels.QuickMenu.ToString());
            CuiHelper.AddUi(basePlayer, mainContainer);
        }
        #endregion GUI

        #region Hooks
        private void OnServerInitialized()
        {
            LoadData();
            if (!_config.BalanceEconomicsEnabled && !plugins.Exists(nameof(ServerRewards)))
            {
                PrintWarning("No ServerRewards plugin found");
            }
            if (_config.BalanceEconomicsEnabled && !plugins.Exists(nameof(Economics)))
            {
                PrintWarning("No Economics plugin found");
            }
            covalence.RegisterCommand(_config.MenuCommand, this, ServerInfoGuiCommand);
            if (_imageManager == null)
                _imageManager = new GameObject("ImageManagerGameObject").AddComponent<ImageManagerBehaviour>();
            if (TerrainMeta.Path != null)
            {
                _oilRigs = TerrainMeta.Path.Monuments?.Where(x => x.shouldDisplayOnMap &&
                    x.displayPhrase?.english != null && x.displayPhrase.english.Contains("Oil Rig")).ToList() ?? new List<MonumentInfo>();
            }
            foreach (var monumentInfo in _oilRigs)
            {
                foreach (var baseNetworkable in BaseNetworkable.serverEntities.Where(e => e is HackableLockedCrate))
                {
                    var crate = (HackableLockedCrate)baseNetworkable;
                    var position = monumentInfo.transform.position;
                    var distance = Vector3Ex.Distance2D(position, crate.ServerPosition);
                    if ((monumentInfo.displayPhrase.english.Equals("Oil Rig", StringComparison.OrdinalIgnoreCase) && distance < 50) ||
                        (monumentInfo.displayPhrase.english.Equals("Large Oil Rig", StringComparison.OrdinalIgnoreCase) && distance < 100))
                    {
                        _oilRigCrates[crate.net.ID.Value] = crate;
                    }
                }
            }

            /*var imageProperties = _config.GuiImages.GetType().GetProperties();
            foreach (var imageProperty in imageProperties)
            {
                if (imageProperty.PropertyType == typeof(string))
                    ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage(imageProperty.Name, imageProperty.GetValue(_config.GuiImages).ToString()));
            }*/

            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("Checked", _config.GuiImages.Checked));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("PlayerSection", _config.GuiImages.PlayerSection));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("PlayerCoordinates", _config.GuiImages.PlayerCoordinates));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("WeatherSunny", _config.GuiImages.WeatherSunny));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("WeatherNight", _config.GuiImages.WeatherNight));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("WeatherRainy", _config.GuiImages.WeatherRainy));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("WeatherFoggy", _config.GuiImages.WeatherFoggy));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("WeatherThunder", _config.GuiImages.WeatherThunder));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("WeatherWindy", _config.GuiImages.WeatherWindy));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("WeatherRainbow", _config.GuiImages.WeatherRainbow));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsChinookDropActive", _config.GuiImages.EventsChinookDropActive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsChinookDropInactive", _config.GuiImages.EventsChinookDropInactive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsOilRigCrateActive", _config.GuiImages.EventsOilRigCrateActive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsOilRigCrateInactive", _config.GuiImages.EventsOilRigCrateInactive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsSpawnCargoShipActive", _config.GuiImages.EventsSpawnCargoShipActive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsSpawnCargoShipInactive", _config.GuiImages.EventsSpawnCargoShipInactive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsSpawnPatrolHelicopterActive", _config.GuiImages.EventsSpawnPatrolHelicopterActive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsSpawnPatrolHelicopterInactive", _config.GuiImages.EventsSpawnPatrolHelicopterInactive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsDestroyBradleyOnLaunchsiteActive", _config.GuiImages.EventsDestroyBradleyOnLaunchsiteActive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsDestroyBradleyOnLaunchsiteInactive", _config.GuiImages.EventsDestroyBradleyOnLaunchsiteInactive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsAirdropActive", _config.GuiImages.EventsAirdropActive));
            ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage("EventsAirdropInactive", _config.GuiImages.EventsAirdropInactive));

            foreach (var playersCountImage in _config.GuiImages.PlayersCountImages)
            {
                ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage($"{PlayerCountImagesPrefix}{playersCountImage.Value}", playersCountImage.Url));
            }

            foreach (var balanceImage in _config.GuiImages.BalanceImages)
            {
                ServerMgr.Instance.StartCoroutine(_imageManager.DownloadImage($"{BalanceImagesPrefix}{balanceImage.Value}", balanceImage.Url));
            }

            timer.Once(5, () =>
            {
                foreach (var p in players.Connected)
                {

                    var basePlayer = p.Object as BasePlayer;
                    if (basePlayer == null || basePlayer.IsNpc)
                        continue;
                    var currentBehaviour = basePlayer.GetComponent<GuiManager>();
                    if (currentBehaviour != null)
                    {
                        currentBehaviour.DoDestroy();
                    }
                    if (!p.HasPermission(PermissionUse))
                        continue;
                    basePlayer.gameObject.AddComponent<GuiManager>();
                }
            });
            _serverInitialized = true;
        }
        private void Loaded()
        {
            _plugin = this;
            if (!permission.PermissionExists(PermissionUse, this))
                permission.RegisterPermission(PermissionUse, this);
            _dataManager = Interface.Oxide.DataFileSystem.GetFile(nameof(ServerInfoGui));
        }
        private void Unload()
        {
            _plugin = null;
            if (_imageManager != null)
                UnityEngine.Object.Destroy(_imageManager);

            foreach (var guiManager in UnityEngine.Object.FindObjectsOfType<GuiManager>())
            {
                if (guiManager != null)
                {
                    guiManager.DoDestroy();
                }
            }

            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                if (basePlayer == null || basePlayer.gameObject == null)
                    continue;
                var guiManager = basePlayer.gameObject.GetComponent<GuiManager>();
                if (guiManager != null)
                    guiManager.DoDestroy();
            }
            _playerGuiConfigsCache.Clear();
            _serverInitialized = false;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsNpc)
                return;
            var guiManager = player.gameObject.GetComponent<GuiManager>();
            if (guiManager != null)
                guiManager.DoDestroy();

            if (permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                player.gameObject.AddComponent<GuiManager>();
            }
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                guiManager = basePlayer.GetComponent<GuiManager>();
                if (guiManager != null)
                {
                    guiManager.ShowPlayersCount();
                }
            }
        }
        void OnPlayerSleepEnded(BasePlayer player)
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                var guiManager = basePlayer.GetComponent<GuiManager>();
                if (guiManager != null)
                {
                    guiManager.ShowPlayersCount();
                }
            }
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            timer.Once(1f, () =>
            {
                foreach (var basePlayer in BasePlayer.activePlayerList)
                {
                    var guiManager = basePlayer.GetComponent<GuiManager>();
                    if (guiManager != null)
                    {
                        guiManager.ShowPlayersCount();
                    }
                }
            });
        }

        #region HackableLockedCrate (Chinook & Oil Rig)

        void OnEntitySpawned(HackableLockedCrate crate)
        {
            if (!_serverInitialized)
                return;
            foreach (var monumentInfo in _oilRigs)
            {
                var position = monumentInfo.transform.position;
                var distance = Vector3Ex.Distance2D(position, crate.ServerPosition);
                if ((monumentInfo.displayPhrase.english.Equals("Oil Rig", StringComparison.OrdinalIgnoreCase) && distance < 50) ||
                    (monumentInfo.displayPhrase.english.Equals("Large Oil Rig", StringComparison.OrdinalIgnoreCase) && distance < 100))
                {
                    _oilRigCrates[crate.net.ID.Value] = crate;
                    return;
                }

            }
        }
        void OnCrateDropped(HackableLockedCrate crate)
        {
            if (!_serverInitialized)
                return;
            if (crate.net == null)
            {
                timer.Once(2f, () =>
                {
                    _chinookCrates[crate.net.ID.Value] = crate;
                    OnUiEventUpdated?.Invoke();
                });
            }

        }
        void OnCrateLanded(HackableLockedCrate crate)
        {
            if (!_serverInitialized)
                return;
            if (_chinookCrates.ContainsKey(crate.net.ID.Value))
            {
                _chinookCrates.Remove(crate.net.ID.Value);
            }
            OnUiEventUpdated?.Invoke();
        }
        void OnCrateHack(HackableLockedCrate crate)
        {
            if (!_serverInitialized)
                return;
            if (_oilRigCrates.ContainsKey(crate.net.ID.Value))
            {
                _hackingOilRigCrates[crate.net.ID.Value] = crate;
                _oilRigCrates.Remove(crate.net.ID.Value);
            }
            else if (_chinookCrates.ContainsKey(crate.net.ID.Value))
            {
                _hackingChinookCrates[crate.net.ID.Value] = crate;
                _chinookCrates.Remove(crate.net.ID.Value);
            }
            OnUiEventUpdated?.Invoke();
        }
        void OnCrateHackEnd(HackableLockedCrate crate)
        {
            if (!_serverInitialized)
                return;
            _hackingOilRigCrates.Remove(crate.net.ID.Value);
            _hackingChinookCrates.Remove(crate.net.ID.Value);
            OnUiEventUpdated?.Invoke();
        }
        void OnEntityKill(HackableLockedCrate crate)
        {
            if (!_serverInitialized)
                return;
            _hackingOilRigCrates.Remove(crate.net.ID.Value);
            _hackingChinookCrates.Remove(crate.net.ID.Value);
            _oilRigCrates.Remove(crate.net.ID.Value);
            _chinookCrates.Remove(crate.net.ID.Value);
            OnUiEventUpdated?.Invoke();
        }

        #endregion HackableLockedCrate (Chinook & Oil Rig)

        #region CargoShip
        void OnEntitySpawned(CargoShip baseBoat)
        {
            if (!_serverInitialized)
                return;
            _cargoShips[baseBoat.net.ID.Value] = baseBoat;
            OnUiEventUpdated?.Invoke();
        }
        void OnEntityKill(CargoShip baseBoat)
        {
            if (!_serverInitialized)
                return;
            _cargoShips.Remove(baseBoat.net.ID.Value);
            OnUiEventUpdated?.Invoke();
        }

        #endregion CargoShip

        #region Patrol Helicopter

        void OnEntitySpawned(PatrolHelicopter baseHelicopter)
        {
            if (!_serverInitialized)
                return;
            _patrolHelicopters[baseHelicopter.net.ID.Value] = baseHelicopter;
            OnUiEventUpdated?.Invoke();
        }
        object OnHelicopterRetire(PatrolHelicopterAI helicopterAi)
        {
            if (!_serverInitialized)
                return null;
            if (helicopterAi != null && helicopterAi.helicopterBase != null)
            {
                _patrolHelicopters.Remove(helicopterAi.helicopterBase.net.ID.Value);
                OnUiEventUpdated?.Invoke();
            }
            return null;
        }
        void OnEntityKill(PatrolHelicopter baseHelicopter)
        {
            if (!_serverInitialized)
                return;
            _patrolHelicopters.Remove(baseHelicopter.net.ID.Value);
            OnUiEventUpdated?.Invoke();
        }

        #endregion Patrol Helicopter

        #region Bradley

        object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            if (!_serverInitialized)
                return null;
            if (_bradleyHostiles.ContainsKey(apc.net.ID.Value))
                return null;
            _bradleyHostiles[apc.net.ID.Value] = apc;
            OnUiEventUpdated?.Invoke();
            return null;
        }
        object OnBradleyApcPatrol(BradleyAPC apc)
        {
            if (!_serverInitialized)
                return null;
            _bradleyHostiles.Remove(apc.net.ID.Value);
            OnUiEventUpdated?.Invoke();
            return null;
        }
        void OnEntityKill(BradleyAPC bradleyApc)
        {
            if (!_serverInitialized)
                return;
            _bradleyDestroyedCount++;
            timer.Once(10 * 60, () =>
            {
                _bradleyDestroyedCount--;
                OnUiEventUpdated?.Invoke();
            });
            _bradleyHostiles.Remove(bradleyApc.net.ID.Value);
            OnUiEventUpdated?.Invoke();
        }

        #endregion Bradley

        #region Air Drop (CargoPlane)

        void OnCargoPlaneSignaled(CargoPlane cargoPlane, SupplySignal supplySignal)
        {
            if (!_serverInitialized)
                return;

            if (cargoPlane == null)
                return;

            _supplyDrops[cargoPlane.net.ID.Value] = cargoPlane;
            OnUiEventUpdated?.Invoke();
        }
        void OnEntitySpawned(CargoPlane cargoPlane)
        {
            if (!_serverInitialized)
                return;
            timer.Once(1, () =>
            {
                if (!_supplyDrops.ContainsKey(cargoPlane.net.ID.Value))
                {
                    _airDrops.Add(cargoPlane.net.ID.Value, cargoPlane);
                    OnUiEventUpdated?.Invoke();
                }
            });
        }
        void OnSupplyDropDropped(SupplyDrop supplyDrop, CargoPlane cargoPlane)
        {
            if (!_serverInitialized)
                return;
            _supplyDrops.Remove(cargoPlane.net.ID.Value);
            _airDrops.Remove(cargoPlane.net.ID.Value);
            OnUiEventUpdated?.Invoke();
        }

        #endregion Air Drop (CargoPlane)

        void OnUserPermissionGranted(string id, string permName)
        {
            if (permName == PermissionUse)
            {
                var player = BasePlayer.FindAwakeOrSleeping(id);
                if (player != null)
                {
                    var guiManager = player.GetComponent<GuiManager>();
                    if (guiManager != null)
                        guiManager.DoDestroy();
                    player.gameObject.AddComponent<GuiManager>();
                }
            }
        }
        void OnUserPermissionRevoked(string id, string permName)
        {
            if (permName == PermissionUse)
            {
                var player = BasePlayer.FindAwakeOrSleeping(id);
                if (player != null)
                {
                    var guiManager = player.GetComponent<GuiManager>();
                    if (guiManager != null)
                        guiManager.DoDestroy();
                }
            }
        }
        #endregion Hooks

        #region Lang
        private static class Messages
        {
            public const string NoPermission = "No Permission";
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Messages.NoPermission] = "You don't have permission to use this command",
            }, this);
        }
        #endregion

        #region Commands
        [Command(ConsoleCommands.ToggleLocationEnabled)]
        void ToggleLocationEnabled(IPlayer player, string cmd, string[] args)
        {
            GuiConfig guiConfig;
            if (!_pluginData.PlayerGuiConfigs.TryGetValue(player.Id, out guiConfig) || guiConfig == null)
            {
                guiConfig = new GuiConfig();
                _playerGuiConfigsCache[player.Id] = guiConfig;
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
            }
            if (_config.LocationOfPlayerEnabled)
            {
                guiConfig.LocationEnabled = !guiConfig.LocationEnabled;
                _playerGuiConfigsCache[player.Id] = guiConfig;
                var basePlayer = player.Object as BasePlayer;
                if (basePlayer != null)
                {
                    var guiManager = basePlayer.GetComponent<GuiManager>();
                    if (guiManager != null)
                    {
                        guiManager.ShowPlayerLocation();
                        guiManager.ShowPlayerLocationImage();
                    }
                }
            }
            ShowUiConfigMenu(player);
        }

        [Command(ConsoleCommands.ToggleBalanceEnabled)]
        void ToggleBalanceEnabled(IPlayer player, string cmd, string[] args)
        {
            GuiConfig guiConfig;
            if (!_pluginData.PlayerGuiConfigs.TryGetValue(player.Id, out guiConfig) || guiConfig == null)
            {
                guiConfig = new GuiConfig();
                _playerGuiConfigsCache[player.Id] = guiConfig;
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
            }
            guiConfig.BalanceEnabled = !guiConfig.BalanceEnabled;
            _playerGuiConfigsCache[player.Id] = guiConfig;
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                var guiManager = basePlayer.GetComponent<GuiManager>();
                if (guiManager != null)
                {
                    guiManager.ShowBalance();
                }
            }
            ShowUiConfigMenu(player);
        }

        [Command(ConsoleCommands.ToggleEventsEnabled)]
        void ToggleEventsEnabled(IPlayer player, string cmd, string[] args)
        {
            GuiConfig guiConfig;
            if (!_pluginData.PlayerGuiConfigs.TryGetValue(player.Id, out guiConfig) || guiConfig == null)
            {
                guiConfig = new GuiConfig();
                _playerGuiConfigsCache[player.Id] = guiConfig;
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
            }
            if (_config.EventAlertAirdropSpawn || _config.EventAlertAirdropSupplySignal
                || _config.EventAlertBradleyHostile || _config.EventAlertBradleyKill
                || _config.EventAlertCargoShipSpawn || _config.EventAlertChinookCrateDrop
                || _config.EventAlertChinookCrateHack || _config.EventAlertOilRigCrateHack
                || _config.EventAlertPatrolHelicopterSpawn)
            {

                guiConfig.EventsEnabled = !guiConfig.EventsEnabled;
                _playerGuiConfigsCache[player.Id] = guiConfig;
                var basePlayer = player.Object as BasePlayer;
                if (basePlayer != null)
                {
                    var guiManager = basePlayer.GetComponent<GuiManager>();
                    if (guiManager != null)
                    {
                        guiManager.ShowEventsToolbar();
                    }
                }
            }
            ShowUiConfigMenu(player);
        }

        [Command(ConsoleCommands.TogglePlayersCountEnabled)]
        void TogglePlayersCountEnabled(IPlayer player, string cmd, string[] args)
        {
            GuiConfig guiConfig;
            if (!_pluginData.PlayerGuiConfigs.TryGetValue(player.Id, out guiConfig) || guiConfig == null)
            {
                guiConfig = new GuiConfig();
                _playerGuiConfigsCache[player.Id] = guiConfig;
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
            }
            guiConfig.PlayersEnabled = !guiConfig.PlayersEnabled;
            _playerGuiConfigsCache[player.Id] = guiConfig;
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                var guiManager = basePlayer.GetComponent<GuiManager>();
                if (guiManager != null)
                {
                    guiManager.ShowPlayersCount();
                }
            }

            ShowUiConfigMenu(player);
        }

        [Command(ConsoleCommands.ToggleWeatherEnabled)]
        void ToggleWeatherEnabled(IPlayer player, string cmd, string[] args)
        {
            GuiConfig guiConfig;
            if (!_pluginData.PlayerGuiConfigs.TryGetValue(player.Id, out guiConfig) || guiConfig == null)
            {
                guiConfig = new GuiConfig();
                _playerGuiConfigsCache[player.Id] = guiConfig;
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
            }
            if (_config.WeatherInformationEnabled)
            {
                guiConfig.WeatherEnabled = !guiConfig.WeatherEnabled;
                _playerGuiConfigsCache[player.Id] = guiConfig;
                var basePlayer = player.Object as BasePlayer;
                if (basePlayer != null)
                {
                    var guiManager = basePlayer.GetComponent<GuiManager>();
                    if (guiManager != null)
                    {
                        guiManager.ShowWeatherInfo();
                    }
                }
            }
            ShowUiConfigMenu(player);
        }


        [Command(ConsoleCommands.SaveChanges)]
        void SaveChanges(IPlayer player, string cmd, string[] args)
        {
            GuiConfig guiConfig;
            if (!_pluginData.PlayerGuiConfigs.TryGetValue(player.Id, out guiConfig) || guiConfig == null)
            {
                guiConfig = new GuiConfig();
                _playerGuiConfigsCache[player.Id] = guiConfig;
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
            }
            if (_playerGuiConfigsCache.TryGetValue(player.Id, out guiConfig))
            {
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
                SaveData();
            }

            CloseUiConfigMenu(player);
        }

        [Command(ConsoleCommands.ResetChanges)]
        void ResetChanges(IPlayer player, string cmd, string[] args)
        {
            GuiConfig guiConfig;
            if (!_pluginData.PlayerGuiConfigs.TryGetValue(player.Id, out guiConfig) || guiConfig == null)
            {
                guiConfig = new GuiConfig();
                _playerGuiConfigsCache[player.Id] = guiConfig;
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
            }
            ApplyDefaultConfigForPlayer(player);
            if (_playerGuiConfigsCache.TryGetValue(player.Id, out guiConfig))
            {
                _pluginData.PlayerGuiConfigs[player.Id] = guiConfig;
                SaveData();
            }
            CloseUiConfigMenu(player);
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                var guiManager = basePlayer.GetComponent<GuiManager>();
                if (guiManager != null)
                {
                    guiManager.ShowAll();
                }
            }
        }

        bool ServerInfoGuiCommand(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(PermissionUse))
            {
                player.Message(lang.GetMessage(Messages.NoPermission, this));
                CloseUiConfigMenu(player);
                return false;
            }
            ShowUiConfigMenu(player);
            return true;
        }

        [Command("sig.help")]
        bool ServerInfoGuiLocationsCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Players Count:\n1) {GetPlayersCountMargins(true)}\n2) {GetPlayersCountMargins(false)}\n");
                sb.AppendLine($"Player Balance:\n1) {GetPlayerBalanceMargins(true)}\n2) {GetPlayerBalanceMargins(false)}\n");
                sb.AppendLine($"Chinook:\n1) {GetChinookMargins(true)}\n2) {GetChinookMargins(false)}\n");
                sb.AppendLine($"Oil Rigs:\n1) {GetOilRigMargins(true)}\n2) {GetOilRigMargins(false)}\n");
                sb.AppendLine($"Cargo Ships:\n1) {GetCargoShipMargins(true)}\n2) {GetCargoShipMargins(false)}\n");
                sb.AppendLine($"Patrol Helicopter:\n1) {GetHelicopterMargins(true)}\n2) {GetHelicopterMargins(false)}\n");
                sb.AppendLine($"Bradley:\n1) {GetBradleyMargins(true)}\n2) {GetBradleyMargins(false)}\n");
                sb.AppendLine($"Air Drop:\n1) {GetAirDropMargins(true)}\n2) {GetAirDropMargins(false)}\n");
                player.Message(sb.ToString());
                return true;
            }
            return false;
        }

        #endregion Commands

        #region GUI Helper
        public static class Ui
        {
            public static float GetX(float amount, int width = 1920) => amount / width;
            public static float GetY(float amount, int height = 1080) => amount / height;
            public static float GetMinX(float left, int width = 1920) => left / width;
            public static float GetMaxX(float right, int width = 1920) => 1 - right / width;
            public static float GetMinY(float bottom, int height = 1080) => bottom / height;
            public static float GetMaxY(float top, int height = 1080) => 1 - top / height;
            public static string GetMin(float left, float bottom, int width = 1920, int height = 1080) => $"{GetMinX(left, width)} {GetMinY(bottom, height)}";
            public static string GetMax(float right, float top, int width = 1920, int height = 1080) => $"{GetMaxX(right, width)} {GetMaxY(top, height)}";
            public static string GetVerticalMin(float left, float bottom, int order, float amount, int width = 1920, int height = 1080) =>
                $"{GetMinX(left, width)} {GetMinY(bottom, height) - GetY(amount, height) * order}";
            public static string GetVerticalMax(float right, float top, int order, float amount, int width = 1920, int height = 1080) =>
                $"{GetMaxX(right, width)} {GetMaxY(top, height) - GetY(amount, height) * order}";
            public static string GetHorizontalMin(float left, float bottom, int order, float amount, int width = 1920, int height = 1080) =>
                $"{GetMinX(left, width) + GetX(amount, width) * order} {GetMinY(bottom, height)}";
            public static string GetHorizontalMax(float right, float top, int order, float amount, int width = 1920, int height = 1080) =>
                $"{GetMaxX(right, width) + GetX(amount, width) * order} {GetMaxY(top, height)}";
            public static string GetGridMin(float left, float bottom, int order, float xAmount, float yAmount, int columnCount, int rowCount, int width = 1920, int height = 1080)
            {
                if (columnCount > 0)
                {
                    return $"{GetMinX(left, width) + GetX(xAmount, width) * (order % columnCount)} {GetMinY(bottom, height) - GetY(yAmount, height) * (order / columnCount)}";
                }
                if (rowCount > 0)
                {
                    return $"{GetMinX(left, width) + GetX(xAmount, width) * (order % rowCount)} {GetMinY(bottom, height) - GetY(yAmount, height) * (order / rowCount)}";
                }
                return $"{GetMinX(left, width) + GetX(xAmount, width) * order} {GetMinY(bottom, height) - GetY(yAmount, height) * order}";
            }
            public static string GetGridMax(float right, float top, int order, float xAmount, float yAmount, int columnCount, int rowCount, int width = 1920, int height = 1080)
            {
                if (columnCount > 0)
                {
                    return $"{GetMaxX(right, width) + GetX(xAmount, width) * (order % columnCount)} {GetMaxY(top, height) - GetY(yAmount, height) * (order / columnCount)}";
                }
                if (rowCount > 0)
                {
                    return $"{GetMaxX(right, width) + GetX(xAmount, width) * (order % rowCount)} {GetMaxY(top, height) - GetY(yAmount, height) * (order / rowCount)}";
                }
                return $"{GetMaxX(right, width) + GetX(xAmount, width) * order} {GetMaxY(top, height) - GetY(yAmount, height) * order}";
            }

            public static CuiElementContainer Container(Panels panel, string color, string min, string max, bool useCursor = false, bool useBlur = false, Panels parent = Panels.Overlay)
            {
                var container = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color, Material = useBlur ? "assets/content/ui/uibackgroundblur.mat" : "Assets/Icons/IconMaterial.mat"},
                            RectTransform = {AnchorMin = min, AnchorMax = max},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent == Panels.HudMenu ? "Hud.Menu" : parent.ToString(),
                        panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString()
                    }
                };
                return container;
            }
            public static void Panel(ref CuiElementContainer container, Panels panel, string color, string min, string max, bool useCursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    CursorEnabled = useCursor
                },
                panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString());
            }
            public static void PanelWithText(ref CuiElementContainer container, Panels panel, string color, string text, int size, string min, string max,
                bool useCursor = false, string font = Fonts.RobotoCondensedRegular, string textColor = "1.0 1.0 1.0 1.0",
                TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0, string textMin = null, string textMax = null)
            {
                var name = CuiHelper.GetGuid();
                var cuiElement = new CuiElement
                {
                    Name = name,
                    Parent = panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString(),
                    FadeOut = 0
                };
                cuiElement.Components.Add(new CuiImageComponent { Color = color, FadeIn = fadeIn });
                cuiElement.Components.Add(new CuiRectTransformComponent { AnchorMin = min, AnchorMax = max });
                if (useCursor)
                    cuiElement.Components.Add(new CuiNeedsCursorComponent());
                container.Add(cuiElement);
                var textName = CuiHelper.GetGuid();
                container.Add(new CuiElement
                {
                    Name = textName,
                    Parent = panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString(),
                    FadeOut = 0,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = textColor,
                            FontSize = (int)(size * 0.7f),
                            Align = align,
                            Text = text,
                            Font = font,
                            FadeIn = fadeIn
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = textMin ?? min,
                            AnchorMax = textMax ?? max
                        }
                    }
                });
            }
            public static void Label(ref CuiElementContainer container, Panels panel, string text, int size, string min, string max, string font = Fonts.RobotoCondensedRegular,
                string textColor = "1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0)
            {
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Color = textColor,
                        FadeIn = fadeIn,
                        FontSize = (int)(size * 0.7f),
                        Align = align,
                        Text = text,
                        Font = font
                    },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString());
            }
            public static void OutlineLabel(ref CuiElementContainer container, Panels panel, string text, int size, string min, string max, string font = Fonts.RobotoCondensedRegular,
                string textColor = "1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0, string outlineDistance = "1 1", string outlineColor = "0 0 0 1.0")
            {
                CuiElement textElement = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString(),
                    FadeOut = fadeIn,
                    Components =
                {
                    new CuiTextComponent
                    {
                        Text = text,
                        FontSize = (int)(size * 0.7f),
                        Align = align,
                        FadeIn = fadeIn,
                        Font = font,
                        Color = textColor
                    },
                    new CuiOutlineComponent
                    {
                        Distance = outlineDistance,
                        Color = outlineColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = min,
                        AnchorMax = max
                    }
                }
                };
                container.Add(textElement);
            }
            public static void Input(ref CuiElementContainer container, Panels panel, string text, int size, string min, string max, string command = null,
                string font = Fonts.RobotoCondensedRegular, string textColor = "1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleLeft, int charsLimit = 100, bool isPassword = false)
            {
                var name = CuiHelper.GetGuid();
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString(),
                    FadeOut = 0,
                    Components = {
                        new CuiInputFieldComponent
                        {
                            Color = textColor,
                            FontSize = (int)(size * 0.7f),
                            Align = align,
                            Text = text,
                            Font = font,
                            CharsLimit = charsLimit,
                            Command = command,
                            IsPassword = isPassword
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = min,
                            AnchorMax = max
                        }
                    }
                });
            }
            public static void Button(ref CuiElementContainer container, Panels panel, string color, string text, int size, string min, string max, string command, string font = Fonts.RobotoCondensedBold,
                string textColor = "1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0, string textMin = null, string textMax = null)
            {
                var parentName = CuiHelper.GetGuid();
                container.Add(new CuiElement
                {
                    Name = parentName,
                    Parent = panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString(),
                    FadeOut = 0,
                    Components = {
                    new CuiButtonComponent
                    {
                        Color = color,
                        Command = command,
                        FadeIn = fadeIn
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = min,
                        AnchorMax = max
                    }
                }
                });
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = parentName,
                    FadeOut = 0,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = textColor,
                            FontSize = (int)(size * 0.7f),
                            Align = align,
                            Text = text,
                            Font = font,
                            FadeIn = fadeIn
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = textMin ?? "0.0 0.0",
                            AnchorMax = textMax ?? "1.0 1.0"
                        }
                    }
                });
            }
            public static void ImageButton(ref CuiElementContainer container, Panels panel, string color, string image, bool isUrl, string min, string max, string command, string imageMin = null, string imageMax = null)
            {
                var name = CuiHelper.GetGuid();
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString(),
                    FadeOut = 0,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = color,
                            Command = command,
                            FadeIn = 0f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = min,
                            AnchorMax = max
                        }
                    }
                });
                if (!string.IsNullOrWhiteSpace(image))
                {
                    var rawImage = new CuiRawImageComponent
                    {
                        Color = "1.0 1.0 1.0 1.0",
                        FadeIn = 0f
                    };
                    if (isUrl)
                    {
                        rawImage.Url = image;
                    }
                    else
                    {
                        rawImage.Png = image;
                    }
                    container.Add(new CuiElement
                    {
                        Parent = name,
                        FadeOut = 0,
                        Components =
                        {
                            rawImage,
                            new CuiRectTransformComponent
                            {
                                AnchorMin = string.IsNullOrWhiteSpace(imageMin) ? "0.0 0.0" : imageMin,
                                AnchorMax = string.IsNullOrWhiteSpace(imageMax) ? "1.0 1.0" : imageMax
                            }
                        }
                    });
                }
            }
            public static void Image(ref CuiElementContainer container, Panels panel, string color, string image, bool isUrl, string min, string max)
            {
                var rawImage = new CuiRawImageComponent
                {
                    Color = color,
                    FadeIn = 0f
                };
                if (isUrl)
                {
                    rawImage.Url = image;
                }
                else
                {
                    rawImage.Png = image;
                }
                container.Add(new CuiElement
                {
                    Components =
                    {
                        rawImage,
                        new CuiRectTransformComponent{ AnchorMin = min, AnchorMax = max },
                    },
                    FadeOut = 0f,
                    Parent = panel == Panels.HudMenu ? "Hud.Menu" : panel.ToString()
                });
            }
            public static string Color(string hexColor, float alpha = 1)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                var red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                var green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                var blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
            public static void ClearAllMenus(BasePlayer player)
            {
                foreach (var name in Enum.GetNames(typeof(Panels)))
                {
                    CuiHelper.DestroyUi(player, name.Equals("HudMenu") ? "Hud.Menu" : name);
                }
            }

            public enum Panels
            {
                Overall,
                Overlay,
                Hud,
                HudMenu,
                Under,
                FullScreen,
                QuickMenu,
                EventsToolbar,
                WeatherInfo,
                PlayerLocationText,
                PlayerLocationImage,
                PlayersStatistics,
                Balance,
                GameTime
            }
            public class ColorCode
            {
                public const string Black = "#000000", White = "#FFFFFF";
                public const string Green = "#018447", Red = "#B1231E", Blue = "#3C7DB3", ButtonBlack = "#141414", EerieBlack = "#282726",
                    Gray = "#424242", ShadowBlack = "#0F0F14", TextGray = "#B3B3B3", DarkBlue = "#0A325A", QueenBlue = "#33689A",
                    MayaBlue = "#81C7F8", Gainsboro = "#DCDCDC", Xanthic = "#EEEE04", PaleSilver = "#CDC3BB",
                    AndroidGreen = "#9BBB45", ChocolateWeb = "#C9753D";
            }
            public class Fonts
            {
                public const string DroidSansMono = "DroidSansMono.ttf";
                public const string PermanentMarker = "PermanentMarker.ttf";
                public const string RobotoCondensedBold = "RobotoCondensed-Bold.ttf";
                public const string RobotoCondensedRegular = "RobotoCondensed-Regular.ttf";
            }
            public class Anchor
            {
                public Anchor(float top, float right, float bottom, float left)
                {
                    Width = 1920;
                    Height = 1080;
                    Top = top;
                    Right = right;
                    Bottom = bottom;
                    Left = left;
                }

                public Anchor(int width, int height, float top, float right, float bottom, float left) : this(top, right, bottom, left)
                {
                    Width = width;
                    Height = height;
                }

                public int Width { get; set; }
                public int Height { get; set; }
                public float Top { get; set; }
                public float Right { get; set; }
                public float Bottom { get; set; }
                public float Left { get; set; }
            }
        }

        #endregion GUI Helper

        #region Location Helpers
        private string GetGrid(Vector3 pos)
        {
            return MapHelper.PositionToString(pos);
            /*char letter = 'A';
            var x = Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) % 26;
            var count = Mathf.Floor(Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) / 26);
            var z = (Mathf.Floor(ConVar.Server.worldsize / 146.3f)) - Mathf.Floor((pos.z + (ConVar.Server.worldsize / 2)) / 146.3f);
            letter = (char)(letter + x);
            var secondLetter = count <= 0 ? string.Empty : ((char)('A' + (count - 1))).ToString();
            return $"{secondLetter}{letter}{z - 1}";*/
        }
        #endregion Location Helpers

        [HookMethod("OnBalanceChanged")]
        private void OnBalanceChanged(string playerId, double amount)
        {
            if (!plugins.Exists(nameof(Economics)))
                return;
            var player = BasePlayer.activePlayerList.FirstOrDefault(p => p.UserIDString.Equals(playerId));
            if (player != null)
            {
                var guiBehaviour = player.GetComponent<GuiManager>();
                if (guiBehaviour != null)
                {
                    guiBehaviour.SetPlayerBalance(amount);
                }
            }
        }
        public int GetServerRewardPoints(ulong playerId)
        {
            if (!plugins.Exists(nameof(ServerRewards)))
                return 0;
            var points = ServerRewards?.Call("CheckPoints", playerId);
            if (points == null)
                return 0;
            int pointsValue;
            int.TryParse(points.ToString(), out pointsValue);
            return pointsValue;
        }
        public double GetEconomicsBalance(ulong playerId)
        {
            if (!plugins.Exists(nameof(Economics)))
                return 0;
            var points = Economics?.Call("Balance", playerId);
            if (points == null)
                return 0;
            double pointsValue;
            double.TryParse(points.ToString(), out pointsValue);
            return pointsValue;
        }
    }
}
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */