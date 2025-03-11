using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System.IO;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using System.Collections;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("StatsController", "Amino", "1.0.11")]
    [Description("A sleek stats system for Rust")]
    public class StatsController : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary, PlaytimeTracker, ServerRewards, Economics;

        #region Config
        public static Configuration _config;
        public static UIElements _uiColors;
        public class Configuration
        {
            [JsonProperty("Commands")]
            public List<string> Commands { get; set; } = new List<string>();
            [JsonProperty(PropertyName = "Clear data on wipe")]
            public bool ClearDataOnWipe { get; set; } = true;
            [JsonProperty(PropertyName = "Main Statistic PVP")]
            public string MainStatisticPVP { get; set; } = "PlayerKills";
            [JsonProperty(PropertyName = "Main Statistic PVE")]
            public string MainStatisticPVE { get; set; } = "NPCKills";
            [JsonProperty(PropertyName = "Using Money? Options (Economics, ServerRewards)")]
            public string Money { get; set; } = "ServerRewards";
            [JsonProperty(PropertyName = "Main Statistic RAID")]
            public string MainStatisticRAID { get; set; } = "rocket_basic_fired";
            [JsonProperty(PropertyName = "Default Category (PVP, PVE, RAID)")]
            public string DefaultCategory { get; set; } = "PVP";
            [JsonProperty(PropertyName = "Max Players Shown")]
            public int MaxPlayers { get; set; } = 100;
            [JsonProperty(PropertyName = "Stats to log")]
            public Dictionary<string, StatsOptions> LogStats { get; set; } = new Dictionary<string, StatsOptions>();
            [JsonProperty(PropertyName = "Update Static Data Every X Minutes (5+ Recommended)")]
            public double DataFreq { get; set; } = 5;
            [JsonProperty(PropertyName = "UI Colors (0, 1)")]
            public int UIColors { get; set; } = 0;
            [JsonProperty(PropertyName = "UI Colors 0")]
            public UIElements UIColorsZero { get; set; } = new UIElements();
            [JsonProperty(PropertyName = "UI Colors 1")]
            public UIElements UIColorsOne { get; set; } = new UIElements();
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Commands = new List<string> { "stats", "stat" },
                    LogStats = new Dictionary<string, StatsOptions>()
                    {
                        { "PlayerKills", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVP", IsMainPersonalStat = true } },
                        { "PlayerDeaths", new StatsOptions { Enabled = true, PointsChange = -1, Category = "PVP", IsMainPersonalStat = true } },
                        { "PlayerKDR", new StatsOptions { Enabled = true, PointsChange = 0, Category = "PVP", IsMainPersonalStat = true } },
                        { "Money", new StatsOptions { Enabled = true, PointsChange = 0, Category = "PVE" } },
                        { "NPCKills", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVE" } },
                        { "ChickenKills", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVE" } },
                        { "BoarKills", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVE" } },
                        { "StagKills", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVE" } },
                        { "WolfKills", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVE" } },
                        { "PolarbearKills", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVE" } },
                        { "RidableHorseKills", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVE" } },
                        { "Sulfur_Ore_Farmed", new StatsOptions { Enabled = true, PointsChange = 0, Category = "PVE" } },
                        { "Stones_Farmed", new StatsOptions { Enabled = true, PointsChange = 0, Category = "PVE" } },
                        { "Metal_Ore_Farmed", new StatsOptions { Enabled = true, PointsChange = 0, Category = "PVE" } },
                        { "Suicides", new StatsOptions { Enabled = true, PointsChange = -1, Category = "PVP" } },
                        { "LootContainerKills", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVE" } },
                        { "BulletsFired", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVP" } },
                        { "Headshots", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVP" } },
                        { "BulletsHit", new StatsOptions { Enabled = true, PointsChange = 1, Category = "PVP" } },
                        { "explosive.satchel.deployed.thrown", new StatsOptions { Enabled = true, PointsChange = -1, Category = "RAID" } },
                        { "explosive.timed.deployed.thrown", new StatsOptions { Enabled = true, PointsChange = 1, Category = "RAID" } },
                        { "rocket_basic_fired", new StatsOptions { Enabled = true, PointsChange = 1, Category = "RAID" } },
                        { "rocket_fire_fired", new StatsOptions { Enabled = true, PointsChange = 1, Category = "RAID" } },
                        { "rocket_hv_fired", new StatsOptions { Enabled = true, PointsChange = 1, Category = "RAID" } }

                    },
                    UIColorsOne = new UIElements()
                    {
                        MainPanelColor = ".17 .17 .17 1",
                        PersonalStatsTitlePanelColor = "1 1 1 .1",
                        PersonalStatsStatsPanelColor = "1 1 1 .1",
                        PersonalStatsStatPanelColor = "1 1 1 .1",
                        PersonalStatsStat2PanelColor = "1 1 1 .1",
                        PersonalStatsStat3PanelColor = "1 1 1 .1",
                        PageButtonColor = "1 1 1 .1",
                        SelectedButtonColor = "1 1 1 .2",
                        SelectionButtonColor = "1 1 1 .1",
                        StatRowEvenColor = "1 1 1 .1",
                        FilterPanelColor = "1 1 1 .12",
                        StatRowOddColor = "1 1 1 .2",
                        StatRowPersonColor = ".38 .63 1 .4",
                        HandleColor = "1 1 1 .2",
                        HighlightColor = "1 1 1 .15",
                        PressedColor = "1 1 1 .2",
                        TrackColor = "1 1 1 .05"
                    }
                };
            }
        }

        public class StatsOptions
        {
            public bool Enabled { get; set; } = true;
            public bool DisplayOnMainUI { get; set; } = true;
            public bool DisplayOnPersonalUI { get; set; } = true;
            public bool IsMainPersonalStat { get; set; } = false;
            public double PointsChange { get; set; } = 0;
            [JsonProperty(PropertyName = "ItemID for stat image")]
            public int ItemID { get; set; } = 0;
            [JsonProperty(PropertyName = "IMG Link for stat image (takes priority over ItemID)")]
            public string IMGLink { get; set; } = string.Empty;
            [JsonProperty(PropertyName = "Category (PVP, PVE, RAID)")]
            public string Category { get; set; }
        }

        public class UIElements
        {
            public string BlurBackgroundColor { get; set; } = "0 0 0 .4";
            public string BlurPanelColorOverlay { get; set; } = "1 1 1 .05";
            public string MainPanelColor { get; set; } = "0 0 0 0";
            public string SecondaryPanelColor { get; set; } = "0 0 0 0";
            public string PersonalStatsTitlePanelColor { get; set; } = "0 0 0 .5";
            public string PersonalStatsBGColor { get; set; } = "0 0 0 .4";
            public string PersonalStatsStatsPanelColor { get; set; } = "0 0 0 .4";
            public string PersonalStatsStatPanelColor { get; set; } = "0 0 0 .3";
            public string PersonalStatsStat2PanelColor { get; set; } = "0 0 0 .5";
            public string PersonalStatsStat3PanelColor { get; set; } = "0 0 0 .4";
            public string PageButtonColor { get; set; } = "0 0 0 .4";
            public string ThirdPanelColor { get; set; } = "0 0 0 0";
            public string FilterPanelColor { get; set; } = "0 0 0 .5";
            public string SelectionButtonColor { get; set; } = "0 0 0 .6";
            public string SelectedButtonColor { get; set; } = ".28 .56 1 .6";
            public string StatRowOddColor { get; set; } = "0 0 0 .6";
            public string StatRowEvenColor { get; set; } = "0 0 0 .5";
            public string StatRowPersonColor { get; set; } = ".38 .63 1 .4";
            public string HandleColor { get; set; } = "0.15 0.15 0.15 .5";
            public string HighlightColor { get; set; } = "0.17 0.17 0.17 .5";
            public string PressedColor { get; set; } = ".17 .17 .17 .7";
            public string TrackColor { get; set; } = ".09 .09 .09 .4";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();

                _uiColors = _config.UIColors == 1 ? _config.UIColorsOne : _config.UIColorsZero;
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Data & Constructors
        public List<KillTypes> _currentData = new List<KillTypes>();
        public List<KillTypes> _staticData = new List<KillTypes>();
        private bool usingWC = false;
        private bool isWipe = false;

        public class KillTypes
        {
            public ulong SteamID;
            public string SteamName;
            public string Selection { get; set; } = null;
            public string Search { get; set; } = null;
            public string Filter { get; set; } = null;
            public double Points;
            public Dictionary<string, double> KillSaves = new Dictionary<string, double>()
            {
                { "PlayerKills", 0 },
                { "PlayerDeaths", 0 },
                { "NPCKills", 0 },
                { "ChickenKills", 0 },
                { "BoarKills", 0 },
                { "StagKills", 0 },
                { "WolfKills", 0 },
                { "PolarbearKills", 0 },
                { "RidableHorseKills", 0 },
                { "LootContainerKills", 0 },
                { "BulletsFired", 0 },
                { "Headshots", 0 },
                { "BulletsHit", 0 },
                { "Suicides", 0 },
                { "Sulfur_Ore_Farmed", 0 },
                { "Stones_Farmed", 0 },
                { "Metal_Ore_Farmed", 0 },
                { "explosive.satchel.deployed.thrown", 0 },
                { "explosive.timed.deployed.thrown", 0 },
                { "rocket_basic_fired", 0 },
                { "rocket_fire_fired", 0 },
                { "rocket_hv_fired", 0 },
                { "Money", 0 }
            };

            public KillTypes Clone()
            {
                KillTypes clone = new KillTypes();

                clone.SteamID = this.SteamID;
                clone.SteamName = this.SteamName;
                clone.Selection = this.Selection;
                clone.Points = this.Points;
                clone.KillSaves = new Dictionary<string, double>(this.KillSaves);

                return clone;
            }

            public Dictionary<string, double> CloneKillSaves()
            {
                return new Dictionary<string, double>(this.KillSaves);
            }

            public void AddKillSaves(Dictionary<string, double> otherKillSaves)
            {
                foreach (var entry in otherKillSaves)
                {
                    if (this.KillSaves.ContainsKey(entry.Key))
                    {
                        if (entry.Key == "Money") this.KillSaves["Money"] = entry.Value;
                        else this.KillSaves[entry.Key] += entry.Value;
                    }
                    else
                    {
                        this.KillSaves.Add(entry.Key, entry.Value);
                    }
                }
            }
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UpdateMsg"] = "Stats update every 5 minutes!",
                ["PVPButtonName"] = "PVP",
                ["PVEButtonName"] = "PVE",
                ["RAIDButtonName"] = "RAID",
                ["PlayerKills"] = "KILLS",
                ["PlayerDeaths"] = "DEATHS",
                ["PlayerKDR"] = "KDR",
                ["Suicides"] = "SUICIDES",
                ["NamePlaceholder"] = "NAME",
                ["PositionPlaceholder"] = "#",
                ["BulletsFired"] = "BULLETS FIRED",
                ["Headshots"] = "HEADSHOTS",
                ["BulletsHit"] = "BULLETS HIT",
                ["NPCKills"] = "NPCS",
                ["ChickenKills"] = "CHICKENS",
                ["BoarKills"] = "BOARS",
                ["StagKills"] = "STAGS",
                ["WolfKills"] = "WOLVES",
                ["PolarbearKills"] = "POLAR B",
                ["RidableHorseKills"] = "HORSES",
                ["LootContainerKills"] = "BARRELS",
                ["PersonalNamePlaceholder"] = "{0}",
                ["PlaytimePlaceholder"] = "{0}",
                ["Sulfur_Ore_Farmed"] = "SULFUR",
                ["Stones_Farmed"] = "STONE",
                ["Metal_Ore_Farmed"] = "METAL",
                ["explosive.satchel.deployed.thrown"] = "SATCHELS USED",
                ["explosive.timed.deployed.thrown"] = "C4 USED",
                ["rocket_basic_fired"] = "ROCKETS USED",
                ["rocket_fire_fired"] = "INCEN ROCKETS USED",
                ["rocket_hv_fired"] = "HV ROCKETS USED",
                ["ServerRewards"] = "RP: {0}",
                ["Economics"] = "Economics {0}",
                ["Money"] = "MONEY",
                ["NoStats"] = "No stats logged yet!"
            }, this, "en");
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Hooks
        void OnServerInitialized(bool initial)
        {
            var isUsingWC = Interface.Call("IsUsingPlugin", "StatsController");
            if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
            else usingWC = false;

            RegisterCommandsAndPermissions();
            LoadData();
            RegisterImages();
            UnsubFromHooks();
            timer.Every((float)_config.DataFreq * 60, UpdateStaticData);
        }

        void OnNewSave(string filename)
        {
            if (_config.ClearDataOnWipe) isWipe = true;
        }

        void OnWCRequestedUIPanel(BasePlayer player, string panelName, string neededPlugin)
        {
            if (!neededPlugin.Equals("StatsController", StringComparison.OrdinalIgnoreCase)) return;
            usingWC = true;
            CMDOpenStatsUI(player, null, null);
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                var isUsingWC = Interface.Call("IsUsingPlugin", "StatsController");
                if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
                else usingWC = false;
            }
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                usingWC = false;
                RegisterCommandsAndPermissions();
            }

        }

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
            {
                UpdateStaticData();

                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "STATSMainPanel");
                }
            }

            _config = null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if(entity == null || info == null || info.Initiator == null || info.InitiatorPlayer == null) return;

            if(!(entity is BasePlayer))
            {
                var attacker = info.Initiator as BasePlayer;
                if (attacker == null || !attacker.userID.IsSteamId()) return;
                var attackerInfo = GetOrLoadActiveData(attacker);
                if (attackerInfo == null || entity == attacker) return;

                var entityName = entity?.GetType().Name + "Kills";
                if (_config.LogStats.TryGetValue(entityName, out var points))
                {
                    if (points.Enabled) AddData(ref attackerInfo, entityName, points.PointsChange);
                }
            }
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            var playerInfo = GetOrLoadActiveData(player);

            var entName = item.info.displayName.english.Replace(" ", "_") + "_Farmed";
            if (_config.LogStats.TryGetValue(entName, out var points))
            {
                if (points.Enabled) AddData(ref playerInfo, entName, points.PointsChange);
            }
            return;
        }

        object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (info.InitiatorPlayer == null || player == null) return null;
            if (info.InitiatorPlayer.IsNpc || player.IsNpc) return null;

            var attackerInfo = GetOrLoadActiveData(info.InitiatorPlayer);
            if (attackerInfo == null) return null;

            if (info.isHeadshot && _config.LogStats.TryGetValue("Headshots", out var headshots))
                if (headshots.Enabled) AddData(ref attackerInfo, "Headshots", headshots.PointsChange);

            if (_config.LogStats.TryGetValue("BulletsHit", out var bulletsHit))
                if (bulletsHit.Enabled) AddData(ref attackerInfo, "BulletsHit", bulletsHit.PointsChange);

            return null;
        }

        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (player == null || player.IsNpc) return;

            var attackerInfo = GetOrLoadActiveData(player);
            if (attackerInfo == null) return;

            if (_config.LogStats.TryGetValue("BulletsFired", out var bulletsFired))
                if (bulletsFired.Enabled) AddData(ref attackerInfo, "BulletsFired", bulletsFired.PointsChange);
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            var attackerInfo = GetOrLoadActiveData(player);
            if (attackerInfo == null) return;

            if (_config.LogStats.TryGetValue($"{entity.ShortPrefabName}.thrown", out var bulletsFired))
                if (bulletsFired.Enabled) AddData(ref attackerInfo, $"{entity.ShortPrefabName}.thrown", bulletsFired.PointsChange);
        }

        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            var attackerInfo = GetOrLoadActiveData(player);
            if (attackerInfo == null) return;

            if (_config.LogStats.TryGetValue($"{entity.ShortPrefabName}_fired", out var bulletsFired))
                if (bulletsFired.Enabled) AddData(ref attackerInfo, $"{entity.ShortPrefabName}_fired", bulletsFired.PointsChange);
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if(player == null || info == null || info.Initiator == null || info.InitiatorPlayer == null) return;

            var attackerInfo = GetOrLoadActiveData(info.InitiatorPlayer);
            var receiverInfo = GetOrLoadActiveData(player);

            if(attackerInfo == null && receiverInfo == null) return;
            if (info.InitiatorPlayer.Team != null && info.InitiatorPlayer.Team.members.Contains(player.userID)) return;

            if (player == info.Initiator && attackerInfo != null && _config.LogStats.TryGetValue("Suicides", out var suicides))
            {
                if (suicides.Enabled) AddData(ref attackerInfo, "Suicides", suicides.PointsChange);
                return;
            }

            if (!info.InitiatorPlayer.IsNpc && player.IsNpc && attackerInfo != null && _config.LogStats.TryGetValue("NPCKills", out var npckills))
            {
                if (npckills.Enabled) AddData(ref attackerInfo, "NPCKills", npckills.PointsChange);
                return;
            }

            if (!player.IsNpc && receiverInfo != null && _config.LogStats.TryGetValue("PlayerDeaths", out var deaths))
            {
                if (deaths.Enabled) AddData(ref receiverInfo, "PlayerDeaths", deaths.PointsChange);
            }

            if (!info.Initiator.IsNpc && attackerInfo != null && _config.LogStats.TryGetValue("PlayerKills", out var kills))
            {
                if (kills.Enabled) AddData(ref attackerInfo, "PlayerKills", kills.PointsChange);
            }

            return;
        }
        #endregion

        #region Commands
        [ConsoleCommand("stats_main")]
        private void CMDStats(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var currentData = GetOrLoadActiveData(player);
            switch (arg.Args[0])
            {
                case "close":
                    CuiHelper.DestroyUi(player, "STATSMainPanel");
                    break;
                case "selection":
                    currentData.Selection = arg.Args[1];
                    UpdateFilter(currentData);
                    UIOpenStatsSelection(player, currentData);
                    UIOpenStatsPanel(player, currentData);
                    break;
                case "player":
                    UIOpenPersonalStats(player, ulong.Parse(arg.Args[1]), currentData);
                    UIOpenStatsSelection(player, currentData, false);
                    CuiHelper.DestroyUi(player, "STATSPersonalPanelOverlay");
                    break;
                case "filter":
                    currentData.Filter = String.Join(" ", arg.Args.Skip(1));
                    UIOpenStatsPanel(player, currentData);
                    break;
                case "back":
                    UIOpenStatsSelection(player, currentData);
                    UIOpenStatsPanel(player, currentData);
                    break;
                default:
                    CuiHelper.DestroyUi(player, "STATSMainPanel");
                    break;
            }
        }
        #endregion

        #region Methods
        void UnsubFromHooks()
        {
            var p = _config.LogStats;
            if (!p["ChickenKills"].Enabled && !p["BoarKills"].Enabled && !p["StagKills"].Enabled
                && !p["WolfKills"].Enabled && !p["PolarbearKills"].Enabled && !p["RidableHorseKills"].Enabled && !p["LootContainerKills"].Enabled)
                Unsubscribe(nameof(OnEntityDeath));

            if (!p["Sulfur_Ore_Farmed"].Enabled && !p["Stones_Farmed"].Enabled && !p["Metal_Ore_Farmed"].Enabled)
                Unsubscribe(nameof(OnDispenserBonus));

            if (!p["BulletsFired"].Enabled)
                Unsubscribe(nameof(OnWeaponFired));

            if (!p["Headshots"].Enabled && !p["BulletsHit"].Enabled)
                Unsubscribe(nameof(OnEntityTakeDamage));

            if (!p["explosive.satchel.deployed.thrown"].Enabled && !p["explosive.timed.deployed.thrown"].Enabled)
                Unsubscribe(nameof(OnExplosiveThrown));

            if (!p["rocket_basic_fired"].Enabled && !p["rocket_fire_fired"].Enabled && !p["rocket_hv_fired"].Enabled)
                Unsubscribe(nameof(OnRocketLaunched));

            if (!p["PlayerDeaths"].Enabled && !p["NPCKills"].Enabled && !p["Suicides"].Enabled)
                Unsubscribe(nameof(OnPlayerDeath));
        }

        private string GetTextColor(int i = 0, int page = 0)
        {
            var rankColor = "1 1 1 1";
            if (page == 0)
            {
                switch (i)
                {
                    case 0:
                        rankColor = "1 .92 .19 .8";
                        break;
                    case 1:
                        rankColor = "1 .84 .48 .8";
                        break;
                    case 2:
                        rankColor = "1 .72 .38 .8";
                        break;
                }
            }
            return rankColor;
        }

        double GetKDR(double kills, double deaths)
        {
            if (kills == 0) return 0;
            if (deaths == 0) return kills;
            return Math.Round(kills / deaths, 2);
        }

        double GetPlayTime(string userID)
        {
            var playTime = PlaytimeTracker?.Call<object>("GetPlayTime", userID);
            if (playTime == null) return 0;
            return (double)playTime;
        }

        private string GetImage(string imageName)
        {
            if (ImageLibrary == null)
            {
                PrintError("Could not load images due to no Image Library");
                return null;
            }

            return ImageLibrary.Call<string>("GetImage", imageName, 0UL, false);
        }

        void UpdateStaticData()
        {
            foreach (var player in _currentData)
            {
                KillTypes foundPlayer = null;

                for (int i = 0; i < _staticData.Count; i++)
                    if (_staticData[i].SteamID == player.SteamID)
                    {
                        foundPlayer = _staticData[i];
                        break;
                    }

                if (foundPlayer == null)
                {
                    foundPlayer = new KillTypes
                    {
                        SteamID = player.SteamID,
                        SteamName = player.SteamName
                    };
                    _staticData.Add(foundPlayer);
                }

                foundPlayer.Points += player.Points;
                foundPlayer.SteamName = player.SteamName;
                foundPlayer.AddKillSaves(player.KillSaves);
            }

            SaveData();
            _currentData = new List<KillTypes>();
        }

        private void AddData(ref KillTypes currentData, string theKey, double points, double addData = -1)
        {
            if (currentData == null) return;
            currentData.KillSaves[theKey]++;
            currentData.Points += points;
        }

        private KillTypes GetOrLoadActiveData(BasePlayer player)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return null;

            var playerData = _currentData.FirstOrDefault(x => x.SteamID == player.userID);
            if (playerData == null)
            {
                playerData = new KillTypes { SteamID = player.userID, SteamName = player.displayName };

                if (_config.LogStats["Money"].Enabled)
                {
                    if (_config.Money == "ServerRewards")
                    {
                        var point = ServerRewards?.Call("CheckPoints", player.userID);
                        playerData.KillSaves["Money"] = point == null ? 0 : double.Parse($"{point}");
                    }
                    else if (_config.Money == "Economics")
                    {
                        var point = Economics?.Call("Balance", player.userID);
                        playerData.KillSaves["Money"] = point == null ? 0 : double.Parse($"{point}");
                    }
                    else playerData.KillSaves["Money"] = 0;
                }

                var defaultCat = _config.DefaultCategory.ToLower();
                switch (defaultCat)
                {
                    case "pvp":
                        playerData.Filter = _config.MainStatisticPVP;
                        break;
                    case "pve":
                        playerData.Filter = _config.MainStatisticPVE;
                        break;
                    case "raid":
                        playerData.Filter = _config.MainStatisticRAID;
                        break;
                }
                _currentData.Add(playerData);
            }

            return playerData;
        }

        void RegisterCommandsAndPermissions()
        {
            if(!usingWC) foreach (var command in _config.Commands)
                cmd.AddChatCommand(command, this, CMDOpenStatsUI);
        }

        private void RegisterImages()
        {
            Dictionary<string, string> imageList = new Dictionary<string, string>();
            foreach (var stat in _config.LogStats.Where(x => !string.IsNullOrEmpty(x.Value.IMGLink)))
            {
                imageList.Add($"Stat{stat.Key}", stat.Value.IMGLink);
            }

            ImageLibrary?.Call("ImportImageList", "StatsController", imageList, 0UL, true);
        }

        private void LoadData()
        {
            var stats = Interface.GetMod().DataFileSystem.ReadObject<List<KillTypes>>("statscontroller");
            if (stats == null || isWipe) stats = new List<KillTypes>();

            _staticData = stats;
        }

        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("statscontroller", _staticData);
        #endregion

        #region UI
        private void CMDOpenStatsUI(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            var currentData = GetOrLoadActiveData(player);
            if (string.IsNullOrEmpty(currentData.Selection)) currentData.Selection = _config.DefaultCategory;

            var container = new CuiElementContainer();

            CreatePanel(ref container, "0 0", "1 1", usingWC ? "0 0 0 0" : _uiColors.BlurBackgroundColor, usingWC ? "WCSourcePanel" : "Overlay", "STATSMainPanel", true, true);
            CreatePanel(ref container, "0 0", "1 1", usingWC ? "0 0 0 0" : _uiColors.BlurPanelColorOverlay, "STATSMainPanel");

            CreatePanel(ref container, usingWC ? "0 0" : ".15 .1", usingWC ? ".995 1" : ".85 .9", _uiColors.MainPanelColor, "STATSMainPanel", "STATSInfoPanel");
            CreatePanel(ref container, "0 .92", "1 1", _uiColors.SecondaryPanelColor, "STATSInfoPanel", "STATSSelectionPanel");
            CreatePanel(ref container, "0 .09", "1 .91", _uiColors.ThirdPanelColor, "STATSInfoPanel", "STATSStatsPanel");
            CreatePanel(ref container, "0 0", "1 .08", _uiColors.ThirdPanelColor, "STATSInfoPanel", "STATSPersonalPanel");
            CreateLabel(ref container, "0 -.07", "1 -.02", "0 0 0 0", "1 1 1 .6", Lang("UpdateMsg"), 20, TextAnchor.UpperCenter, "STATSMainPanel");

            CuiHelper.DestroyUi(player, "STATSMainPanel");
            CuiHelper.AddUi(player, container);

            UIOpenStatsSelection(player, currentData);
            UIOpenStatsPanel(player, currentData);
        }

        void UIOpenStatsPanel(BasePlayer player, KillTypes currentData)
        {
            var container = new CuiElementContainer();
            var statsPanel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "STATSStatsPanel", "STATSStatsPanelOverlay");
            var filterPanel = CreatePanel(ref container, "0 .91", ".996 .99", _uiColors.FilterPanelColor, statsPanel);
            AddScrollView(ref container, "0 0", ".995 .9", $"{(_staticData.Count <= 8 ? 0 : (1 + (-.125 * _staticData.Take(_config.MaxPlayers).Count())))}", "0 0 0 0", statsPanel, "StatsScrollPanel");

            var enabledStats = _config.LogStats.Where(statType => statType.Value.Enabled && statType.Value.DisplayOnMainUI && statType.Value.Category.Equals(currentData.Selection, StringComparison.OrdinalIgnoreCase)).Select(stat => stat.Key).ToList();

            CreateLabel(ref container, "0 0", ".05 1", "0 0 0 0", "1 1 1 1", Lang("PositionPlaceholder"), 15, TextAnchor.MiddleCenter, filterPanel);
            CreateLabel(ref container, ".05 0", ".3 1", "0 0 0 0", "1 1 1 1", Lang("NamePlaceholder"), 15, TextAnchor.MiddleLeft, filterPanel);

            double partLength = .7 / enabledStats.Count;
            for (int i = 0; i < enabledStats.Count; i++)
            {
                CreateButton(ref container, $"{.3 + (i * partLength)} 0", $"{.3 + partLength + (i * partLength)} 1", "0 0 0 0", "1 1 1 1", Lang(enabledStats[i]), 15, $"stats_main filter {enabledStats[i]}", filterPanel);
            }

            var dataSet = _staticData.Select(x => x.Clone()).ToList();
            dataSet.Sort((a, b) =>
            {
                var aValue = currentData.Filter == "PlayerKDR" ? GetKDR(a.KillSaves["PlayerKills"], a.KillSaves["PlayerDeaths"]) : a.KillSaves[currentData.Filter];
                var bValue = currentData.Filter == "PlayerKDR" ? GetKDR(b.KillSaves["PlayerKills"], b.KillSaves["PlayerDeaths"]) : b.KillSaves[currentData.Filter];
                return bValue.CompareTo(aValue);
            });

            int playerIndex = dataSet.FindIndex(x => x.SteamID == player.userID);
            UIPerStats(player, playerIndex, dataSet, enabledStats);

            CuiHelper.DestroyUi(player, "STATSStatsPanelOverlay");
            CuiHelper.AddUi(player, container);

            for (int i = 0; i < Math.Min(dataSet.Count, _config.MaxPlayers); i++)
            {
                ServerMgr.Instance.StartCoroutine(AddStatRow(player, dataSet[i], i, enabledStats, partLength, "StatsScrollPanel"));
            }
        }

        IEnumerator AddStatRow(BasePlayer player, KillTypes stat, int index, List<string> enabledStats, double partLength, string parent)
        {
            var container = new CuiElementContainer();

            var panelDepth = 0 - (-.125 * _staticData.Take(_config.MaxPlayers).Count());
            var space = _staticData.Count <= 8 ? .01 : .01 / panelDepth;

            var rowDepth = _staticData.Count <= 8 ? .125 : (.125) / panelDepth;
            var topHeight = 1 + (rowDepth - ((index + 1) * rowDepth));

            var statRow = CreatePanel(ref container, $"0 {topHeight - rowDepth + space}", $".99 {topHeight}", index % 2 == 0 ? _uiColors.StatRowEvenColor : _uiColors.StatRowOddColor, parent, fadeIn: true);
            CreateLabel(ref container, "0 0", ".05 1", "0 0 0 0", GetTextColor(index), $"#{index + 1}", 15, TextAnchor.MiddleCenter, statRow);
            CreateImagePanel(ref container, ".045 .1", ".091 .9", GetImage($"{stat.SteamID}"), statRow);
            CreateLabel(ref container, ".101 0", ".3 1", "0 0 0 0", "1 1 1 1", stat.SteamName, 17, TextAnchor.MiddleLeft, statRow);

            yield return null;

            for (int i = 0; i < enabledStats.Count; i++)
            {
                double neededStat = stat.KillSaves.ContainsKey(enabledStats[i]) ? stat.KillSaves[enabledStats[i]] : 0;
                if (enabledStats[i].Equals("PlayerKDR", StringComparison.OrdinalIgnoreCase))
                    neededStat = GetKDR(stat.KillSaves["PlayerKills"], stat.KillSaves["PlayerDeaths"]);

                CreateLabel(ref container, $"{.3 + (i * partLength)} 0", $"{.3 + partLength + (i * partLength)} 1", "0 0 0 0", "1 1 1 1", $"{neededStat}", 15, TextAnchor.MiddleCenter, statRow);
            }

            CreateButton(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", " ", 15, $"stats_main player {stat.SteamID}", statRow);

            CuiHelper.AddUi(player, container);

            yield return null;
        }

        void UIPerStats(BasePlayer player, int statIndex, List<KillTypes> staticStats, List<string> enabledStats)
        {
            var container = new CuiElementContainer();
            var statRow = CreatePanel(ref container, "0 0", ".999 1", _uiColors.StatRowPersonColor, "STATSPersonalPanel", "STATSPersonalPanelOverlay");

            if(statIndex == -1) CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", Lang("NoStats"), 20, TextAnchor.MiddleCenter, statRow);
            else
            {
                var stat = staticStats[statIndex];

                var i = 0;
                var partLength = .7 / enabledStats.Count;
                CreateLabel(ref container, "0 0", ".045 1", "0 0 0 0", GetTextColor(i), $"#{statIndex + 1}", 15, TextAnchor.MiddleCenter, statRow);
                CreateImagePanel(ref container, ".046 .1", ".091 .9", GetImage($"{stat.SteamID}"), statRow);
                CreateLabel(ref container, ".101 0", ".3 1", "0 0 0 0", "1 1 1 1", stat.SteamName, 17, TextAnchor.MiddleLeft, statRow);

                var ii = 0;
                foreach (var statType in enabledStats)
                {
                    double neededStat = 0;
                    if (statType.Equals("PlayerKDR", StringComparison.OrdinalIgnoreCase))
                    {
                        neededStat = GetKDR(stat.KillSaves["PlayerKills"], stat.KillSaves["PlayerDeaths"]);
                    }
                    else neededStat = stat.KillSaves[statType];

                    CreateLabel(ref container, $"{.3 + (ii * partLength)} 0", $"{.3 + partLength + (ii * partLength)} 1", "0 0 0 0", "1 1 1 1", $"{neededStat}", 15, TextAnchor.MiddleCenter, statRow);
                    ii++;
                }

                CreateButton(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", " ", 15, $"stats_main player {stat.SteamID}", statRow);
            }

            CuiHelper.DestroyUi(player, "STATSPersonalPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIOpenPersonalStats(BasePlayer player, ulong steamID, KillTypes currentData)
        {
            var container = new CuiElementContainer();
            var playerStats = _staticData.FirstOrDefault(x => x.SteamID == steamID);
            
            var timeSpan = TimeSpan.FromSeconds(GetPlayTime($"{steamID}"));
            string playTime = $"Days: {timeSpan.Days:00} Hours: {timeSpan.Hours:00} Mins: {timeSpan.Minutes:00}";

            var statsPanel = CreatePanel(ref container, "0 0", "1 1", _uiColors.PersonalStatsBGColor, "STATSStatsPanel", "STATSStatsPanelOverlay");
            var titlePanel = CreatePanel(ref container, ".009 .8", ".99 .985", _uiColors.PersonalStatsTitlePanelColor, statsPanel);
            CreateLabel(ref container, ".1 .54", ".4 .9", "0 0 0 0", "1 1 1 .8", Lang("PersonalNamePlaceholder", null, playerStats.SteamName), 20, TextAnchor.LowerLeft, titlePanel);
            CreateLabel(ref container, ".1 .1", ".5 .46", "0 0 0 0", "1 1 1 .8", Lang("PlaytimePlaceholder", null, playTime), 20, TextAnchor.UpperLeft, titlePanel);
            if (_config.LogStats["Money"].Enabled) CreateLabel(ref container, ".5 .1", "1 .46", "0 0 0 0", "1 1 1 .8", Lang(_config.Money, null, playerStats.KillSaves["Money"]), 20, TextAnchor.UpperLeft, titlePanel);

            CreateImagePanel(ref container, ".01 .1", ".09 .9", GetImage($"{steamID}"), titlePanel);

            var mainStats = _config.LogStats.Where(x => x.Value.IsMainPersonalStat).ToList();
            var buttonLength = .96 / mainStats.Count;
            var mainStatsPanel = CreatePanel(ref container, ".009 .65", ".99 .79", _uiColors.PersonalStatsTitlePanelColor, statsPanel);
            var i = 0;
            foreach (var mainStat in mainStats)
            {
                var mainStatPanel = CreatePanel(ref container, $"{buttonLength * i} 0", $"{buttonLength + (i * buttonLength)} 1", "0 0 0 0", mainStatsPanel);
                CreateLabel(ref container, "0 .52", "1 1", "0 0 0 0", "1 1 1 .7", Lang(mainStat.Key), 15, TextAnchor.LowerCenter, mainStatPanel);

                string statInfo = null;
                if (!playerStats.KillSaves.TryGetValue(mainStat.Key, out double amt))
                {
                    switch (mainStat.Key)
                    {
                        case "PlayerKDR":
                            statInfo = $"{GetKDR(playerStats.KillSaves["PlayerKills"], playerStats.KillSaves["PlayerDeaths"])}";
                            break;
                        default:
                            statInfo = "0";
                            break;
                    }
                }
                else statInfo = $"{amt}";
                CreateLabel(ref container, "0 0", "1 .48", "0 0 0 0", "1 1 1 .7", statInfo, 20, TextAnchor.UpperCenter, mainStatPanel);
                i++;
            }

            var pvpStats = new List<string>();
            var pveStats = new List<string>();
            var raidStats = new List<string>();

            foreach (var statType in _config.LogStats)
                if (statType.Value.Enabled)
                {
                    var lowerCat = statType.Value.Category.ToLower();
                    switch (lowerCat)
                    {
                        case "pvp":
                            pvpStats.Add(statType.Key);
                            break;
                        case "pve":
                            pveStats.Add(statType.Key);
                            break;
                        case "raid":
                            raidStats.Add(statType.Key);
                            break;
                    }
                }

            AddScrollView(ref container, ".009 .01", ".327 .64", $"{(pvpStats.Count < 6 ? 0 : 1 + (-.20 * pvpStats.Count))}", _uiColors.PersonalStatsStatsPanelColor, statsPanel, "StatsPVPScroll");
            AddScrollView(ref container, ".333 .01", ".657 .64", $"{(pveStats.Count < 6 ? 0 : 1 + (-.20 * pveStats.Count))}", _uiColors.PersonalStatsStatsPanelColor, statsPanel, "StatsPVEScroll");
            AddScrollView(ref container, ".663 .01", ".99 .64", $"{(raidStats.Count < 6 ? 0 : 1 + (-.20 * raidStats.Count))}", _uiColors.PersonalStatsStatsPanelColor, statsPanel, "StatsRAIDScroll");

            UICreateStatPanel(container, "StatsPVPScroll", playerStats, pvpStats);
            UICreateStatPanel(container, "StatsPVEScroll", playerStats, pveStats);
            UICreateStatPanel(container, "StatsRAIDScroll", playerStats, raidStats);

            CuiHelper.DestroyUi(player, "STATSStatsPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UICreateStatPanel(CuiElementContainer container, string panel, KillTypes playerStats, List<string> stats)
        {
            var i = 0;
            var panelDepth = 0 - (-.20 * stats.Count);
            var space = stats.Count < 6 ? .01 : .01 / panelDepth;
            var rowDepth = stats.Count < 6 ? .20 : .20 / panelDepth;

            foreach (var stat in stats)
            {
                string statInfo = null;
                if (!playerStats.KillSaves.TryGetValue(stat, out double amt))
                {
                    switch (stat)
                    {
                        case "PlayerKDR":
                            statInfo = $"{GetKDR(playerStats.KillSaves["PlayerKills"], playerStats.KillSaves["PlayerDeaths"])}";
                            break;
                        default:
                            statInfo = "0";
                            break;
                    }
                }
                else statInfo = $"{amt}";

                var topHeight = 1 + (rowDepth - ((i + 1) * rowDepth));

                var statPanel = CreatePanel(ref container, $"0 {topHeight - rowDepth + space}", $".99 {topHeight}", i % 2 == 0 ? _uiColors.StatRowEvenColor : _uiColors.StatRowOddColor, panel);
                CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", $"{Lang($"{stat}")}: {statInfo}", 17, TextAnchor.MiddleCenter, statPanel);
                i++;
            }
            
        }

        void UIOpenStatsSelection(BasePlayer player, KillTypes currentData, bool isMain = true)
        {
            var container = new CuiElementContainer();

            var selectionPanel = CreatePanel(ref container, "0 0", "1 .99", isMain ? "0 0 0 0" : "0 0 0 .5", "STATSSelectionPanel", "STATSSelectionPanelOverlay");
            if (!usingWC) CreateButton(ref container, ".93 0", ".997 .95", isMain ? _uiColors.SelectionButtonColor : "0 0 0 0", "1 1 1 1", isMain ? "X" : "<", 30, isMain ? "stats_main close" : "stats_main back", selectionPanel);
            else CreateButton(ref container, ".93 0", ".997 .95", isMain ? _uiColors.SelectionButtonColor : "0 0 0 0", "1 1 1 1", isMain ? " " : "<", 30, isMain ? " " : "stats_main back", selectionPanel);

            if (isMain)
            {
                CreateButton(ref container, "0 0", ".3 .95", GetUIButtonColor(currentData, "pvp"), "1 1 1 1", Lang("PVPButtonName"), 20, "stats_main selection pvp", selectionPanel);
                CreateButton(ref container, ".31 0", ".61 .95", GetUIButtonColor(currentData, "pve"), "1 1 1 1", Lang("PVEButtonName"), 20, "stats_main selection pve", selectionPanel);
                CreateButton(ref container, ".62 0", ".92 .95", GetUIButtonColor(currentData, "raid"), "1 1 1 1", Lang("RAIDButtonName"), 20, "stats_main selection raid", selectionPanel);
            }

            CuiHelper.DestroyUi(player, "STATSSelectionPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        string GetUIButtonColor(KillTypes currentData, string currentButton)
        {
            if (currentButton.Equals(currentData.Selection, StringComparison.OrdinalIgnoreCase)) return _uiColors.SelectedButtonColor;

            return _uiColors.SelectionButtonColor;
        }

        void UpdateFilter(KillTypes currentData)
        {
            var currentCat = currentData.Selection.ToLower();
            switch (currentCat)
            {
                case "pvp":
                    currentData.Filter = _config.MainStatisticPVP;
                    break;
                case "pve":
                    currentData.Filter = _config.MainStatisticPVE;
                    break;
                case "raid":
                    currentData.Filter = _config.MainStatisticRAID;
                    break;
            }
        }
        #endregion

        #region UI Methods
        static public void AddScrollView(ref CuiElementContainer container, string anchorMin, string anchorMax, string offSetMin, string color, string parent = "Overlay", string panelName = null)
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parent,
                Components = {
                        new CuiImageComponent {
                            FadeIn = 0.2f,
                            Color = color
                        },
                        new CuiScrollViewComponent {
                            Horizontal = false,
                            Vertical = true,
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                            Elasticity = 0.25f,
                            Inertia = true,
                            DecelerationRate = 0.3f,
                            ContentTransform = new CuiRectTransform()
                            {
                                AnchorMin = $"0 {offSetMin}",
                                AnchorMax = "1 1",
                            },
                            ScrollSensitivity = 13.0f,
                            VerticalScrollbar = new CuiScrollbar {
                                Invert = false,
                                AutoHide = false,
                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                HandleColor = _uiColors.HandleColor,
                                HighlightColor = _uiColors.HighlightColor,
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",
                                TrackColor = _uiColors.TrackColor,
                                Size = 6,
                                PressedColor = _uiColors.PressedColor
                            }
                        },
                        new CuiRectTransformComponent {AnchorMin = anchorMin, AnchorMax = anchorMax}
                    }
            });
        }

        private static string CreateItemPanel(ref CuiElementContainer container, string anchorMin, string anchorMax, float padding, string color, int itemId, string parent = "Overlay", string panelName = null, ulong skinId = 0L)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Image = { Color = color }
            }, parent, panelName);

            container.Add(new CuiElement
            {
                Parent = panel,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{padding} {padding + .004f}",
                        AnchorMax = $"{1 - padding - .004f} {1 - padding - .02f}"
                    },
                    new CuiImageComponent {ItemId = itemId, SkinId = skinId}
                }
            });

            return panel;
        }

        private static string CreateLabel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, panelColor, parent, panelName);
            container.Add(new CuiLabel
            {
                Text =
                {
                    Color = textColor,
                    Text = labelText,
                    Align = alignment,
                    FontSize = fontSize,
                    Font = "robotocondensed-bold.ttf"
                }
            }, panel);
            return panel;
        }

        private static string CreatePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string parent = "Overlay", string panelName = null, bool blur = false, bool isMainPanel = false, string offsetMin = null, string offsetMax = null, bool fadeIn = false)
        {
            CuiPanel panel = new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                Image = { Color = panelColor }
            };

            if(offsetMax != null) panel.RectTransform.OffsetMax = offsetMax;
            if (offsetMax != null) panel.RectTransform.OffsetMin = offsetMin;
            if (fadeIn) panel.Image.FadeIn = 0f;

            if (blur) panel.Image.Material = "assets/content/ui/uibackgroundblur.mat";
            if (isMainPanel) panel.CursorEnabled = true;
            return container.Add(panel, parent, panelName);
        }

        private static void CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelImage, string parent = "Overlay", string panelName = null, bool isUrl = false)
        {
            var panel = new CuiElement
            {
                Parent = parent,
                Name = panelName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    }
                }
            };

            if (isUrl) panel.Components.Add(new CuiRawImageComponent { Url = panelImage });
            else panel.Components.Add(new CuiRawImageComponent { Png = panelImage });

            container.Add(panel);
        }

        private static void CreateImageButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string buttonCommand, string panelImage, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, buttonColor, parent, panelName);
            CreateImagePanel(ref container, "0 0", "1 1", panelImage, panel);

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"{buttonCommand}" }
            }, panel);
        }

        private static string CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText, int fontSize, string buttonCommand, string parent = "Overlay", TextAnchor labelAnchor = TextAnchor.MiddleCenter)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, "0 0 0 0", parent);

            container.Add(new CuiButton
            {
                Button = { Color = buttonColor, Command = $"{buttonCommand}" },
                Text = { Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText },
            }, panel);
            return panel;
        }

        private static string CreateInput(ref CuiElementContainer container, string anchorMin, string anchorMax, string command, string backgroundColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string labelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, backgroundColor, parent, labelName);

            container.Add(new CuiElement
            {
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = textColor,
                        Text = labelText,
                        Align = alignment,
                        FontSize = fontSize,
                        Font = "robotocondensed-bold.ttf",
                        NeedsKeyboard = true,
                        Command = command
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                Parent = panel
            });

            return panel;
        }
        #endregion
    }
}
