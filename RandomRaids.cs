using System.Collections.Generic;
using System.Collections;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using Oxide.Game.Rust.Cui;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using Color = UnityEngine.Color;
using UnityEngine.AI;
using Facepunch;
using VLB;
using System.Globalization;
using ProtoBuf;
using Newtonsoft.Json.Linq;
using System.IO;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("RandomRaids", "Razor", "1.9.4")]
    [Description("Npc's that randomly raid bases")]
    public class RandomRaids : RustPlugin
    {
        #region Loading
        public static bool debug = false;
        public const string BlockName = "RandomRaidsTimer";
        private Coroutine QueuedRoutine;
        private Coroutine QueuedRoutinePlayer;

        private Dictionary<string, string> itemNameToConfig = new Dictionary<string, string>();
        private Dictionary<ulong, string> entitySkinToConfig = new Dictionary<ulong, string>();
        private List<string> itemLootSpawn = new List<string>();
        private static readonly string _directory = Path.Combine(Interface.Oxide.LogDirectory, "RandomRaids");
        private static Dictionary<ulong, List<ulong>> supplyDrops = new Dictionary<ulong, List<ulong>>();

        private static string respondok = "assets/prefabs/npc/scientist/sound/respondok.prefab";
        private static string chatter = "assets/prefabs/npc/scientist/sound/chatter.prefab";
        private static string responddeath = "assets/prefabs/npc/scientist/sound/responddeath.prefab";
        private static string takecover = "assets/prefabs/npc/scientist/sound/takecover.prefab";
        private static string agro = "assets/prefabs/npc/scientist/sound/aggro.prefab";
        private static string reload = "assets/prefabs/npc/scientist/sound/reload.prefab";
        private List<string> ItemConfig = new List<string>();

        private const string adminPerm = "randomraids.admin";
        private const string usePerm = "randomraids.use";

        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;

        RaidData raidData;
        private DynamicConfigFile RAIDDATA;

        LogData logData;
        private DynamicConfigFile LOGDATA;

        LogDataRB logDataRB;
        private DynamicConfigFile LOGDATARB;

        [PluginReference]
        private Plugin Kits, TruePVE, RaidableBases, CustomLoot, BotReSpawn, AlphaLoot, ServerRewards, Economics;

        public static RandomRaids _;

        private Dictionary<Vector3, DateTime> raidLocations = new Dictionary<Vector3, DateTime>();

        public static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        public static List<MLRS> mlrs = new List<MLRS>();
        static int obstructionMask = LayerMask.GetMask(new[] { "World", "Terrain" });

        private static int groundLayer;
        private static int terrainLayer;
        private static int BuildingLayer;
        private static Vector3 Vector3Down;

        public class eventList
        {
            public string type;
            public Vector3 location;
            public DateTime lastraidedTime;
            public DateTime lastraidedTimeNpcKills;

        }

        private object CanModifyCustomNPC(BaseEntity entity)
        {
            if (entity.GetType().ToString() == "RandomRaider")
                return false;
            return null;
        }

        private void Init()
        {
            _ = this;
            permission.RegisterPermission(adminPerm, this);
            permission.RegisterPermission(usePerm, this);

            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/PlayersTC");
            RAIDDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/RaidCooldownData");
            LOGDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/PlayerLog");
            LOGDATARB = Interface.Oxide.DataFileSystem.GetFile(Name + "/PlayerLogRaidableBases");
            LoadData();

            if (configData.raidSettings.raidTypes.Count <= 0)
            {
                configData.raidSettings.raidTypes.Add("easy", new raidConfig(12, 1, 10, true, 100, 640, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 1, 1.0, false, new List<string>() { "easy" }, 200));
                configData.raidSettings.raidTypes.Add("medium", new raidConfig(24, 2, 15, true, 200, 900, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 1, 1.0, true, new List<string>() { "easy" }, 400));
                configData.raidSettings.raidTypes.Add("hard", new raidConfig(32, 3, 20, true, 400, 1200, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 1, 1.0, true, new List<string>() { "easy" }, 600));
                configData.raidSettings.raidTypes.Add("expert", new raidConfig(42, 4, 30, true, 400, 1500, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 1, 1.0, true, new List<string>() { "medium" }, 800));
                configData.raidSettings.raidTypes.Add("nightmare", new raidConfig(60, 5, 30, true, 400, 2100, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 1, 1.0, true, new List<string>() { "medium" }, 1000));
                SaveConfig();
            }

            foreach (var jconfigs in configData.raidSettings.raidTypes.Values)
            {
                if (jconfigs.juggernaut == null)
                {
                    jconfigs.juggernaut = new List<string>() { "easy" };
                    SaveConfig();
                }
            }

            if (configData.copterSettings.copterProfile.Count <= 0)
            {
                configData.copterSettings.copterProfile.Add("easy", new copterConfig(1000, 2));
                SaveConfig();
            }

            if (configData.settingsRB.raidSettings.Count <= 0)
            {
                configData.settingsRB.raidSettings.Add(0, new RBConfig(3, true, 10, 320, new List<string>() { "easy" }));
                configData.settingsRB.raidSettings.Add(1, new RBConfig(3, true, 10, 320, new List<string>() { "easy", "medium" }));
                configData.settingsRB.raidSettings.Add(2, new RBConfig(3, true, 10, 320, new List<string>() { "easy", "medium" }));
                configData.settingsRB.raidSettings.Add(3, new RBConfig(3, true, 10, 320, new List<string>() { "easy", "medium", "hard" }));
                configData.settingsRB.raidSettings.Add(4, new RBConfig(3, true, 10, 320, new List<string>() { "hard", "expert", "nightmare" }));
                SaveConfig();
            }

            if (configData.settingsNPCKILLS == null)
                configData.settingsNPCKILLS = new ConfigData.SettingsNPCKILLS();
            if (configData.settingsNPCKILLS.npcSettings == null)
                configData.settingsNPCKILLS.npcSettings = new Dictionary<string, NPCConfig>();

            if (configData.settingsNPCKILLS.npcSettings.Count <= 0)
            {
                configData.settingsNPCKILLS.npcSettings.Add("scientistnpc_heavy", new NPCConfig(30, false, 10, 320, new List<string>() { "medium" }));
                configData.settingsNPCKILLS.npcSettings.Add("scientistnpc_oilrig", new NPCConfig(60, false, 10, 320, new List<string>() { "easy" }));
                configData.settingsNPCKILLS.npcSettings.Add("scientistnpc_patrol", new NPCConfig(60, false, 10, 320, new List<string>() { "easy" }));
                configData.settingsNPCKILLS.npcSettings.Add("scientistnpc_junkpile", new NPCConfig(60, false, 10, 320, new List<string>() { "easy" }));
                SaveConfig();
            }

            if (configData.juggernautProfile.Count <= 0)
            {
                configData.juggernautProfile.Add("easy", new juggernautConfig(new Dictionary<int, int>() { { 1, 1 } }, 500f, new List<string>()));
                configData.juggernautProfile.Add("medium", new juggernautConfig(new Dictionary<int, int>() { { 1, 2 } }, 700f, new List<string>()));
                SaveConfig();
            }
            if (configData.itemProfile.Count <= 0)
            {
                configData.itemProfile.Add("easy", new itemConfig(2893480896, "Npc Raid Level 1", 5, new List<string>() { "crate_normal", "crate_normal_2" }));
                configData.itemProfile.Add("medium", new itemConfig(2893480635, "Npc Raid Level 2", 5, new List<string>() { "crate_normal", "crate_normal_2", "heli_crate" }));
                configData.itemProfile.Add("hard", new itemConfig(2893481009, "Npc Raid Level 3", 5, new List<string>() { "bradley_crate", "crate_elite" }));
                configData.itemProfile.Add("expert", new itemConfig(2893481137, "Npc Raid Level 4", 5, new List<string>() { "bradley_crate", "crate_elite" }));
                configData.itemProfile.Add("nightmare", new itemConfig(2893482048, "Npc Raid Level 5", 5, new List<string>() { "bradley_crate", "crate_elite" }));
                SaveConfig();
            }

            if (configData.settings.totalRaids <= 0)
            {
                configData.settings.totalRaids = 10;
                SaveConfig();
            }

            if (configData.settingsNPCKILLS.CombineProfile == null)
            {
                configData.settingsNPCKILLS.CombineNpcList = new List<string>()
                {
                    "scientistnpc_patrol",
                    "scientistnpc_oilrig",
                    "scientistnpc_heavy",
                    "sentry.scientist.barge",
                    "sentry.scientist.barge.static",
                    "sentry.scientist.static",
                    "heavyscientist_youtooz.deployed",
                    "ch47scientists.entity",
                    "scientist_corpse",
                    "scientistnpc_arena",
                    "scientistnpc_cargo",
                    "scientistnpc_cargo_turret_any",
                    "scientistnpc_cargo_turret_lr300",
                    "scientistnpc_ch47_gunner",
                    "scientistnpc_excavator",
                    "scientistnpc_full_any",
                    "scientistnpc_full_lr300",
                    "scientistnpc_full_mp5",
                    "scientistnpc_full_pistol",
                    "scientistnpc_full_shotgun",
                    "scientistnpc_junkpile",
                    "sientistnpc_peacekeeper",
                    "scientistnpc_roam",
                    "scientistnpc_roam_nvg_variant",
                    "scientistnpc_roamtethered",
                    "npc_tunneldweller",
                    "npc_tunneldwellerspawned",
                    "npc_underwaterdweller",
                    "scarecrow"
                };
                configData.settingsNPCKILLS.CombineProfile = new NPCConfig(70, false, 10, 320, new List<string>() { "easy" });
                SaveConfig();
            }

            if (configData.settings.WebHookUrl == null)
            {

                configData.settings.WebHookUrl = "";
                configData.settings.AvatarURL = "http://images.myvector.xyz/discordimage.png";
                SaveConfig();
            }

            if (configData.itemProfile != null && configData.itemProfile.Count > 0)
            {
                foreach (var itemName in configData.itemProfile)
                {
                    if (configData.raidSettings.raidTypes.ContainsKey(itemName.Key) && !entitySkinToConfig.ContainsKey(itemName.Value.itemSkin))
                        entitySkinToConfig.Add(itemName.Value.itemSkin, itemName.Key);
                    if (configData.raidSettings.raidTypes.ContainsKey(itemName.Key) && !itemNameToConfig.ContainsKey(itemName.Value.itemName))
                        itemNameToConfig.Add(itemName.Value.itemName, itemName.Key);

                    if (itemName.Value.SpawnEnabled && configData.raidSettings.raidTypes.ContainsKey(itemName.Key))
                        itemLootSpawn.Add(itemName.Key);
                }
            }
        }

        private void Unload()
        {
            stopSearch();

            StopAllRaids();
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(current, "RtimerS" + BlockName);
                CuiHelper.DestroyUi(current, "RsurrenderS" + BlockName);
                current.SendConsoleCommand("gametip.hidegametip");
            }
            _ = null;
        }

        private void StopAllRaids()
        {
            List<EventRandomManager> list = Facepunch.Pool.Get<List<EventRandomManager>>();
            list.AddRange(EventRandomManager._AllRaids);

            foreach (var eventRunning in list)
            {
                eventRunning.Destroy();
            }
            EventRandomManager._AllRaids.Clear();
            Facepunch.Pool.FreeUnmanaged<EventRandomManager>(ref list);
        }

        private void OnServerInitialized()
        {
            groundLayer = LayerMask.GetMask("Construction", "Terrain", "World");
            terrainLayer = LayerMask.GetMask("Terrain", "World", "Water");
            BuildingLayer = LayerMask.GetMask("Construction");
            Vector3Down = new Vector3(0f, -1f, 0f);

            pcdData.tcData.Clear();

            foreach (BuildingPrivlidge find in BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>())
            {
                if (Convert.ToBoolean(RaidableBases?.CallHook("EventTerritory", find.transform.position)))
                    continue;

                foreach (var playerNameID in find.authorizedPlayers)
                {
                    if (!pcdData.tcData.ContainsKey(playerNameID.userid))
                        pcdData.tcData.Add(playerNameID.userid, new List<ulong>() { find.net.ID.Value });
                    else if (!pcdData.tcData[playerNameID.userid].Contains(find.net.ID.Value))
                        pcdData.tcData[playerNameID.userid].Add(find.net.ID.Value);
                }
            }

            SaveData();
            if (configData.settings.randomTimer)
            {
                timer.Every(configData.settings.cooldownSeconds, () => startRestartSearch());
            }

            foreach (var items in configData.itemProfile.Values)
                ItemConfig.Add(items.itemName);

            if (!configData.settings.lockAirdrop)
            {
                Unsubscribe(nameof(CanLootEntity));
                Unsubscribe(nameof(OnEntityKill));
            }
        }
        #endregion

        #region Data
        private void LoadData()
        {
            try
            {
                pcdData = Interface.GetMod().DataFileSystem.ReadObject<PlayerEntity>(Name + "/PlayersTC");
            }
            catch
            {
                PrintWarning("Couldn't load Player data, creating new Playerfile");
                pcdData = new PlayerEntity();
            }
            try
            {
                raidData = Interface.GetMod().DataFileSystem.ReadObject<RaidData>(Name + "/RaidCooldownData");
            }
            catch
            {
                PrintWarning("Couldn't load Raid data, creating new RaidCooldownData file");
                raidData = new RaidData();
            }
            try
            {
                logData = Interface.GetMod().DataFileSystem.ReadObject<LogData>(Name + "/PlayerLog");
            }
            catch
            {
                PrintWarning("Couldn't load log data, creating new log file");
                logData = new LogData();
            }
            try
            {
                logDataRB = Interface.GetMod().DataFileSystem.ReadObject<LogDataRB>(Name + "/PlayerLogRaidableBases");
            }
            catch
            {
                PrintWarning("Couldn't load log data, creating new log file RB");
                logDataRB = new LogDataRB();
            }
        }

        public class PlayerEntity
        {
            public Dictionary<ulong, List<ulong>> tcData = new Dictionary<ulong, List<ulong>>();
        }

        public class RaidData
        {
            public Dictionary<ulong, rData> data = new Dictionary<ulong, rData>();
        }

        public class LogData
        {
            public Dictionary<ulong, lData> pLogs = new Dictionary<ulong, lData>();
        }

        public class lData
        {
            public Dictionary<string, int> NPCKILLS = new Dictionary<string, int>();
            public int _totalNpcKills;
        }

        public class LogDataRB
        {
            public Dictionary<ulong, lDataRB> pLogsRB = new Dictionary<ulong, lDataRB>();
        }

        public class lDataRB
        {
            public Dictionary<int, int> mode = new Dictionary<int, int>();
        }

        public class rData
        {
            public DateTime LastRaided;
            public DateTime LastRaidedNpcKills;
            public DateTime LastRaidedRB;
            public Dictionary<Vector3, DateTime> raidLocations = new Dictionary<Vector3, DateTime>();
            public List<string> info = new List<string>();
            public List<ulong> info1 = new List<ulong>();
            public List<ulong> info2 = new List<ulong>();
        }

        public void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        public void SaveRaidData()
        {
            RAIDDATA.WriteObject(raidData);
        }

        public void SaveLogData()
        {
            LOGDATA.WriteObject(logData);
        }

        public void SaveRBLogData()
        {
            LOGDATARB.WriteObject(logDataRB);
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Random settings")]
            public Settings settings { get; set; }

            [JsonProperty(PropertyName = "All Random Raid Types")]
            public RaidSettings raidSettings { get; set; }

            [JsonProperty(PropertyName = "AttackCopter profiles")]
            public CopterSettings copterSettings { get; set; }

            [JsonProperty(PropertyName = "Block Random raid in colider")]
            public BlockedColliders blockedColliders { get; set; }

            [JsonProperty(PropertyName = "Raidable Base plugin settings")]
            public SettingsRB settingsRB { get; set; }

            [JsonProperty(PropertyName = "Trigger by npc kills")]
            public SettingsNPCKILLS settingsNPCKILLS { get; set; }

            [JsonProperty(PropertyName = "Juggernaut profiles")]
            public Dictionary<string, juggernautConfig> juggernautProfile { get; set; } = new Dictionary<string, juggernautConfig>();

            [JsonProperty(PropertyName = "Item profiles")]
            public Dictionary<string, itemConfig> itemProfile { get; set; } = new Dictionary<string, itemConfig>();

            public class Settings
            {
                [JsonProperty(PropertyName = "Total raids allowed at once")]
                public int totalRaids { get; set; }
                [JsonProperty(PropertyName = "A player on the tc must be online")]
                public bool mustBeOnlinePlayer { get; set; }
                [JsonProperty(PropertyName = "Use random raid timer")]
                public bool randomTimer { get; set; }
                [JsonProperty(PropertyName = "Random raid timer time")]
                public int cooldownSeconds { get; set; }
                [JsonProperty(PropertyName = "Chance of random raid at time (1-100)")]
                public int chanceTime { get; set; }
                [JsonProperty(PropertyName = "Authorized players Cooldown minutes before random raided again by chance")]
                public int raidAgainCooldown { get; set; }
                [JsonProperty(PropertyName = "The maximum amount of time a rocket will fly before exploding")]
                public float RocketExplodeTime { get; set; }
                [JsonProperty(PropertyName = "Npc spawn damage delay")]
                public float damageDelay { get; set; }
                [JsonProperty(PropertyName = "Display global chat message on raid start")]
                public bool useChat { get; set; }
                [JsonProperty(PropertyName = "Display global map marker on raid start")]
                public bool useMarker { get; set; }
                [JsonProperty(PropertyName = "Display Gui to base owners")]
                public bool useGUI { get; set; }
                [JsonProperty(PropertyName = "GUI AnchorMin")]
                public string AnchorMin = "0.807 0.96";
                [JsonProperty(PropertyName = "GUI AnchorMax")]
                public string AnchorMax = "0.996 0.99";
                [JsonProperty(PropertyName = "GUI2 AnchorMin")]
                public string AnchorMin2 = "0.807 0.92";
                [JsonProperty(PropertyName = "GUI2 AnchorMax")]
                public string AnchorMax2 = "0.996 0.95";
                [JsonProperty(PropertyName = "Use GameTip announcement to player")]
                public bool gameTip { get; set; }
                [JsonProperty(PropertyName = "GameTip display time in seconds")]
                public float gameTipTime = 2f;
                [JsonProperty(PropertyName = "Disable radio chatter")]
                public bool disableChatter { get; set; }
                [JsonProperty(PropertyName = "Wait until all NPC are dead before sending in next wave")]
                public bool pauseWave { get; set; }
                [JsonProperty(PropertyName = "Player must have randomraids.use inorder to get raided")]
                public bool musthavePerm { get; set; }
                [JsonProperty(PropertyName = "Use world text to show base location")]
                public bool worldMarker { get; set; }
                [JsonProperty(PropertyName = "World text display time")]
                public float worldMarkerTime { get; set; }
                [JsonProperty(PropertyName = "Lock airdrop to event players")]
                public bool lockAirdrop { get; set; }
                [JsonProperty(PropertyName = "Use discord Webhook")]
                public bool DiscordWebHook { get; set; }
                [JsonProperty(PropertyName = "Discord Webhook URL")]
                public string WebHookUrl { get; set; }
                [JsonProperty(PropertyName = "Discord Avatar URL")]
                public string AvatarURL { get; set; }
            }

            public class SettingsRB
            {
                [JsonProperty(PropertyName = "Raid player on Raidable Base Completed")]
                public bool raidableBaseComplete { get; set; }
                [JsonProperty(PropertyName = "Warn player in chat of upcoming revenge on them")]
                public bool warnPlayer { get; set; }
                [JsonProperty(PropertyName = "Raidable Base Settings")]
                public Dictionary<int, RBConfig> raidSettings { get; set; }
            }

            public class RaidSettings
            {
                [JsonProperty(PropertyName = "Random raid types")]
                public Dictionary<string, raidConfig> raidTypes { get; set; }
            }

            public class CopterSettings
            {
                [JsonProperty(PropertyName = "Profile Names")]
                public Dictionary<string, copterConfig> copterProfile { get; set; }

            }

            public class BlockedColliders
            {
                public List<string> Blocked { get; set; }
            }

            public class SettingsNPCKILLS
            {
                [JsonProperty(PropertyName = "Enable log kills and random raid on kills")]
                public bool enableKills { get; set; }
                [JsonProperty(PropertyName = "Reset npc kills on player raided.")]
                public bool resetKills { get; set; }
                [JsonProperty(PropertyName = "Warn player in chat of upcoming revenge on them")]
                public bool warnPlayer { get; set; }
                [JsonProperty(PropertyName = "Use total players on tc check on type")]
                public bool enforceAuth { get; set; }
                [JsonProperty(PropertyName = "Use min Base size check on type")]
                public bool BlockCheck { get; set; }
                [JsonProperty(PropertyName = "Enable combine npc kills list")]
                public bool enableKillsCombine { get; set; }
                [JsonProperty(PropertyName = "Combine npc kills list")]
                public List<string> CombineNpcList { get; set; }
                [JsonProperty(PropertyName = "Combine Profile to use")]
                public NPCConfig CombineProfile { get; set; }     
                [JsonProperty(PropertyName = "Npc kills settings")]
                public Dictionary<string, NPCConfig> npcSettings { get; set; }
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
                settings = new ConfigData.Settings
                {
                    totalRaids = 2,
                    mustBeOnlinePlayer = true,
                    randomTimer = false,
                    cooldownSeconds = 3600,
                    chanceTime = 10,
                    raidAgainCooldown = 1440,
                    RocketExplodeTime = 4.0f,
                    damageDelay = 2f,
                    useChat = true,
                    useMarker = true,
                    disableChatter = false,
                    pauseWave = false,
                    musthavePerm = false,
                    worldMarker = false,
                    worldMarkerTime = 60f,
                    lockAirdrop = false,
                    DiscordWebHook = false,
                    WebHookUrl = "",
                    AvatarURL = "http://images.myvector.xyz/discordimage.png"
                },

                settingsRB = new ConfigData.SettingsRB
                {
                    raidSettings = new Dictionary<int, RBConfig>()
                },

                raidSettings = new ConfigData.RaidSettings
                {
                    raidTypes = new Dictionary<string, raidConfig>()
                },

                copterSettings = new ConfigData.CopterSettings
                {
                    copterProfile = new Dictionary<string, copterConfig>()
                },

                blockedColliders = new ConfigData.BlockedColliders
                {
                    Blocked = new List<string>() { "iceberg", "ice_berg", "ice_sheet", "icesheet", "cliff", "cave" }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Version update detected!");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 4, 6))
            {
                configData = baseConfig;
                PrintWarning("Config update was needed and now completed!");
            }
            configData.Version = Version;
            PrintWarning("Version update completed!");
        }

        public class raidConfig
        {
            [JsonProperty(PropertyName = "Total auth players on tc needed")]
            public int TotalAuth;
            [JsonProperty(PropertyName = "Total npcs per wave")]
            public int TotalNpcs;
            [JsonProperty(PropertyName = "How many extra waves")]
            public int TotalNpcWaves;
            [JsonProperty(PropertyName = "Seconds untell next wave")]
            public int NextWaveSeconds;
            [JsonProperty(PropertyName = "Npc fires Mlrs at base")]
            public bool FireMlrs;
            [JsonProperty(PropertyName = "Send in Attack Copter support")]
            public bool attackCopter;
            [JsonProperty(PropertyName = "Total event time in seconds")]
            public int RunTimeSeconds;
            [JsonProperty(PropertyName = "Npc drop loot on death")]
            public bool NpcDropLoot;
            [JsonProperty(PropertyName = "Spawn health of the npc")]
            public float Health;
            [JsonProperty(PropertyName = "Total rockets npc can fire")]
            public int rockets;
            [JsonProperty(PropertyName = "Total explosives npc can toss")]
            public int explosives;
            [JsonProperty(PropertyName = "Total AirDrops on event win")]
            public int WinDrop;
            [JsonProperty(PropertyName = "Rocket damage scale")]
            public double DamageScale;
            [JsonProperty(PropertyName = "Player damage scale from npc")]
            public float playerDamage;
            [JsonProperty(PropertyName = "Auto turret damage scale to npc")]
            public float AutoTurretDamage;
            [JsonProperty(PropertyName = "Raiders aimConeScale")]
            public float aimConeScale;
            [JsonProperty(PropertyName = "Throw Explosive item shortnames")]
            public List<string> explosive;
            [JsonProperty(PropertyName = "Spawn Attack Heli profile")]
            public List<string> spawnCopter;
            [JsonProperty(PropertyName = "Spawn kits for the npcs")]
            public List<string> kits;
            [JsonProperty(PropertyName = "All npc receive the same kit")]
            public bool kitsSame;
            [JsonProperty(PropertyName = "Names to give the npcs")]
            public List<string> NpcNames;
            [JsonProperty(PropertyName = "CustomLoot config profile name")]
            public List<string> lootProfile;
            [JsonProperty(PropertyName = "CustomLoot config profile names for AirDrop")]
            public List<string> lootProfileAirDrop;
            [JsonProperty(PropertyName = "AlphaLoot config profile names")]
            public List<string> lootProfileA;
            [JsonProperty(PropertyName = "AlphaLoot config profile names for AirDrop")]
            public List<string> lootProfileAirDropA;
            [JsonProperty(PropertyName = "Juggernaut config")]
            public List<string> juggernaut;
            [JsonProperty(PropertyName = "Surrender Item")]
            public string surrenderItem;
            [JsonProperty(PropertyName = "Surrender Item Name")]
            public string surrenderName;
            [JsonProperty(PropertyName = "Surrender costs")]
            public int surrenderCosts;
            [JsonProperty(PropertyName = "Notify owners of incoming Random Raid Triggered By Timer")]
            public bool notifyOwners;
            [JsonProperty(PropertyName = "Min Base Blocks Size")]
            public int blockMIN;

            public raidConfig(int minblocks, int TotalAuth, int TotalNpcs, bool OnlyAuthedMembers, float Health, int RunTimeSeconds, List<string> kits, List<string> explosive, int waves, double damage, bool attackCopter, List<string> jconfig, int surrenderCosts1)
            {
                this.blockMIN = minblocks;
                this.TotalAuth = TotalAuth;
                this.TotalNpcs = TotalNpcs;
                this.NpcDropLoot = OnlyAuthedMembers;
                this.Health = Health;
                this.rockets = 20;
                this.explosives = 10;
                this.attackCopter = attackCopter;
                this.kits = kits;
                this.RunTimeSeconds = RunTimeSeconds;
                this.explosive = explosive;
                this.TotalNpcWaves = waves;
                this.NextWaveSeconds = 120;
                this.NpcNames = new List<string>() { "Cobalt Scientist" };
                this.DamageScale = damage;
                this.AutoTurretDamage = 1.0f;
                this.FireMlrs = false;
                this.playerDamage = 1.0f;
                this.aimConeScale = 2f;
                this.spawnCopter = new List<string>() { "easy" };
                this.juggernaut = jconfig;
                this.lootProfile = new List<string>();
                this.lootProfileAirDrop = new List<string>();
                this.lootProfileA = new List<string>();
                this.lootProfileAirDropA = new List<string>();
                this.WinDrop = 1;
                this.surrenderCosts = surrenderCosts1;
                this.kitsSame = false;
                this.surrenderItem = "scrap";
                this.surrenderName = "Scrap";
            }
        }

        public class copterConfig
        {
            [JsonProperty(PropertyName = "Heli float health")]
            public float startHealth;
            [JsonProperty(PropertyName = "Total crates to drop")]
            public int maxCratesToSpawn;
            [JsonProperty(PropertyName = "Heli strafe cooldown")]
            public float strafeTime;
            [JsonProperty(PropertyName = "Heli can strafe x times")]
            public int totalStrafe;

            public copterConfig(float startHealth1, int maxCratesToSpawn1)
            {
                this.startHealth = startHealth1;
                this.maxCratesToSpawn = maxCratesToSpawn1;
                this.totalStrafe = 3;
                this.strafeTime = 50;
            }
        }

        public class RBConfig
        {
            [JsonProperty(PropertyName = "Total amount of mode complete before ever has chance")]
            public int totalRaids;
            [JsonProperty(PropertyName = "Reset kill count after chance happened")]
            public bool resetKillCount;
            [JsonProperty(PropertyName = "Raidable Base Completed Delay Before Raid chance happens")]
            public float raidableBaseCompleteDelay;
            [JsonProperty(PropertyName = "raid chance (1-100)")]
            public int raidChance;
            [JsonProperty(PropertyName = "Cooldown minutes after raid chance happenes")]
            public int cooldownRB;
            [JsonProperty(PropertyName = "Random raid to send them if chance")]
            public List<string> sendRaids;


            public RBConfig(int totalRaids1, bool resetKillCount1, int raidChance1, float raidableBaseCompleteDelay1, List<string> sendRaids1)
            {
                this.totalRaids = totalRaids1;
                this.resetKillCount = resetKillCount1;
                this.raidableBaseCompleteDelay = raidableBaseCompleteDelay1;
                this.raidChance = raidChance1;
                this.cooldownRB = 1440;
                this.sendRaids = sendRaids1;
            }
        }

        public class NPCConfig
        {
            [JsonProperty(PropertyName = "Total amount of Npc killed before chance can happen")]
            public int totalRaids;
            [JsonProperty(PropertyName = "Reset kill count after chance happened")]
            public bool resetKillCount;
            [JsonProperty(PropertyName = "Delay before chance happens")]
            public float raidDelay;
            [JsonProperty(PropertyName = "Raid chance (1-100)")]
            public int raidChance;
            [JsonProperty(PropertyName = "Cooldown minutes before chance can happen again")]
            public int chanceCooldown;
            [JsonProperty(PropertyName = "Random raid to send them if chance")]
            public List<string> sendRaids;

            public NPCConfig(int totalRaids1, bool resetKillCount1, int raidChance1, float raidDelay1, List<string> sendRaids1)
            {
                this.totalRaids = totalRaids1;
                this.resetKillCount = resetKillCount1;
                this.raidDelay = raidDelay1;
                this.chanceCooldown = 1440;
                this.raidChance = raidChance1;
                this.sendRaids = sendRaids1;
            }
        }

        public class juggernautConfig
        {
            [JsonProperty(PropertyName = "Juggernaut spawns on witch Wave number/Total to spawn")]
            public Dictionary<int, int> juggernautOnWaves;
            [JsonProperty(PropertyName = "Spawn health of the juggernaut")]
            public float Health;
            [JsonProperty(PropertyName = "Player damage scale from the juggernaut")]
            public float playerDamage;
            [JsonProperty(PropertyName = "juggernaut aimConeScale")]
            public float aimConeScale;
            [JsonProperty(PropertyName = "Names to give the juggernaut")]
            public List<string> NpcNames;
            [JsonProperty(PropertyName = "Spawn kits for the juggernaut")]
            public List<string> kits;

            public juggernautConfig(Dictionary<int, int> juggernautOnWaves, float Health, List<string> kits)
            {
                this.juggernautOnWaves = juggernautOnWaves;
                this.Health = Health;
                this.kits = kits;
                this.NpcNames = new List<string>() { "Juggernaut Raider" };
                this.playerDamage = 1.0f;
                this.aimConeScale = 2f;
            }
        }

        public class itemConfig
        {
            [JsonProperty(PropertyName = "Raid call item skin")]
            public ulong itemSkin;
            [JsonProperty(PropertyName = "Raid call item name")]
            public string itemName;
            [JsonProperty(PropertyName = "LootContainer Spawn enabled")]
            public bool SpawnEnabled;
            [JsonProperty(PropertyName = "Can Spawn In LootContainer types")]
            public List<string> LootContainers;
            [JsonProperty(PropertyName = "LootContainer Spawn Chance 1-100")]
            public float SpawnChance;

            public itemConfig(ulong itemSkin, string itemName, float SpawnChance, List<string> lootContainers)
            {
                this.itemSkin = itemSkin;
                this.itemName = itemName;
                this.SpawnEnabled = false;
                this.SpawnChance = SpawnChance;
                this.LootContainers = lootContainers;
            }
        }
        #endregion Config

        #region EventRandomManager
        public static float ProjectileDistToGravity(float x, float y, float θ, float v)
        {
            float f1 = θ * ((float)Math.PI / 180f);
            float f2 = (float)(((double)v * (double)v * (double)x * (double)Mathf.Sin(2f * f1) - 2.0 * (double)v * (double)v * (double)y * (double)Mathf.Cos(f1) * (double)Mathf.Cos(f1)) / ((double)x * (double)x));
            if (float.IsNaN(f2) || (double)f2 < 0.00999999977648258)
                f2 = -Physics.gravity.y;
            return f2;
        }

        public class EventRandomManager : FacepunchBehaviour
        {
            public static List<EventRandomManager> _AllRaids = new List<EventRandomManager>();
            public static List<RandomRaider> _AllMembers = new List<RandomRaider>();
            public List<juggernautConfig> jconfig = new List<juggernautConfig>();
            public static bool isFireingMlrs;
            public List<BuildingBlock> _AllFoundations = new List<BuildingBlock>();
            public List<ulong> _authedPlayers = new List<ulong>();

            public List<RandomRaider> members;
            public bool isDestroyed = false;
            public Vector3 location;
            public raidConfig config;
            public string waveType;
            public bool newWave;
            public Coroutine QueuedRoutine;
            public BuildingPrivlidge building;
            public BaseCombatEntity tc;
            public float EndEventTime;
            public float nextWaveTime = 0;
            public int totalWaves = 0;
            public float MlrsEventTime;
            public bool active = false;
            public int totalCopters = 3;
            public PatrolHelicopter heli;
            public MapMarkerExplosion marker;
            public DeployableBoomBox boombox;
            public List<Vector3> savedPosition = new List<Vector3>();
            public List<BasePlayer> _onlinePlayers = new List<BasePlayer>();
            public DateTime activeTime = DateTime.Now;
            public int Duration;
            public DateTime time = DateTime.Now;
            public TimeSpan timeOffset;
            public DateTime lastBlock = DateTime.Now;
            public DateTime lastNotification = DateTime.MinValue;
            public DateTime lastUINotification = DateTime.MinValue;
            public int currentWave = 0;
            public bool sendGUI = false;
            public bool showFinle = false;
            public bool curentlySawning = false;
            public bool showInfinity = false;
            public float useDelay = 0.1f;
            public string sameKit = "";
            public string surrenderItem;
            public int surrenderCost;
            public string surrenderName = "";
            public GameObject gobj;

            public static EventRandomManager Create(Vector3 l, raidConfig c, string t, List<ulong> authedPlayers, GameObject gobj, List<BuildingBlock> blocks, float useDelay = 0.1f)
            {
                GameObject gobjBew = new GameObject();
                EventRandomManager manager = gobjBew.AddComponent<EventRandomManager>();
                manager.members = Facepunch.Pool.Get<List<RandomRaider>>();
                manager.newWave = true;
                manager.config = c;
                manager.waveType = t;
                manager.location = l;
                manager._authedPlayers = authedPlayers;
                manager._AllFoundations = blocks;
                manager.useDelay = useDelay;
                manager.surrenderItem = c.surrenderItem;
                manager.surrenderCost = c.surrenderCosts;
                manager.surrenderName = c.surrenderName;

                _AllRaids.Add(manager);

                if (c != null && c.juggernaut != null && c.juggernaut.Count > 0)
                {
                    foreach (var jconfigs in c.juggernaut)
                    {
                        if (_.configData.juggernautProfile.ContainsKey(jconfigs))
                        {
                            manager.jconfig.Add(_.configData.juggernautProfile[jconfigs]);
                        }
                    }
                }
                manager.sendGUI = _.configData.settings.useGUI;
                return manager;
            }

            public void StartEvent()
            {
                if (config.kitsSame && config.kits != null && config.kits.Count > 0)
                {
                    sameKit = config.kits.GetRandom();
                }
                _.timer.Once(useDelay, () =>
                {
                    Interface.Oxide.CallHook("OnRandomRaidStart", waveType, location);
                    MlrsEventTime = Time.time + UnityEngine.Random.Range(15f, 30f);
                    EndEventTime = Time.time + config.RunTimeSeconds;
                    nextWaveTime = Time.time + 3800;

                    QueuedRoutine = StartCoroutine(GenerateEventMembers());

                    if (config.attackCopter && config.spawnCopter.Count > 0)
                    {
                        string copterProfile = config.spawnCopter.GetRandom();
                        if (_.configData.copterSettings.copterProfile.ContainsKey(copterProfile))
                            heli = spawnHeli(this, location, _.configData.copterSettings.copterProfile[copterProfile]);
                    }
                    if (_.configData.settings.useMarker)
                        marker = SpawnRaidMarker(location, EndEventTime);
                    if (_.configData.settings.useChat)
                        _.notifyChat(location);

                    if (config.TotalNpcWaves <= 0)
                        Duration = config.RunTimeSeconds;
                    else if (_.configData.settings.pauseWave)
                    {
                        Duration = 10;
                    }
                    else
                        Duration = config.NextWaveSeconds;
                    nextWaveTime = Time.time + config.NextWaveSeconds;
                });
            }

            private static MapMarkerExplosion SpawnRaidMarker(Vector3 vector, float timeLimit)
            {
                MapMarkerExplosion explosionMarker = null;

                explosionMarker = GameManager.server.CreateEntity(StringPool.Get(4060989661), vector) as MapMarkerExplosion;
                if (explosionMarker != null)
                {
                    explosionMarker.enableSaving = false;
                    explosionMarker.Spawn();
                    explosionMarker.SetDuration(timeLimit);
                }

                return explosionMarker;
            }

            public static void SendEffect(string effect, Vector3 position, BaseEntity entity)
            {
                if (_ == null || _.configData.settings.disableChatter || entity == null)
                    return;

                Effect infoEffect = new Effect(effect, entity, 0, new Vector3(), new Vector3());
                infoEffect.broadcast = true;
                EffectNetwork.Send(infoEffect);
            }

            public static void setupMlrs(EventRandomManager man)
            {
                if (isFireingMlrs || man == null) return;

                _.timer.Repeat(0.5f, 6, () =>
                {
                    if (man == null || man.location == Vector3.zero) return;
                    float baseGravity;
                    Vector3 posUP = man.location;
                    posUP.y = posUP.y + 240f;
                    Vector3 posFire = new Vector3(man.location.x + UnityEngine.Random.Range(-2.0f, 2.0f), man.location.y, man.location.z + UnityEngine.Random.Range(-2.0f, 2.0f));

                    Vector3 aimToTarget = GetAimToTarget(posUP, posFire, out baseGravity);

                    var startPoint = posUP;
                    startPoint.y += 30f;

                    ServerProjectile projectile;

                    if (CreateAndSpawnRocket(startPoint, aimToTarget, out projectile) == false)
                        return;
                    TimedExplosive component = projectile.GetComponent<TimedExplosive>();
                    if (_AllMembers.Count > 0)
                        component.creatorEntity = _AllMembers.GetRandom();

                    projectile.gravityModifier = 1.5f;
                });
            }

            public static Vector3 GetAimToTarget(Vector3 startPosition, Vector3 targetPos, out float baseGravity)
            {
                Vector3 vector = targetPos - startPosition;
                float num = 90f;
                float num2 = vector.Magnitude2D();
                float y = vector.y;
                float num5 = 40f;

                baseGravity = ProjectileDistToGravity(Mathf.Max(num2, 50f), y, num5, num);

                vector.Normalize();
                vector.y = 0f;

                Vector3 axis = Vector3.Cross(vector, Vector3.up);

                vector = Quaternion.AngleAxis(num5, axis) * vector;

                return vector;
            }

            public static bool CreateAndSpawnRocket(Vector3 firingPos, Vector3 firingDir, out ServerProjectile mlrsRocketProjectile)
            {
                RaycastHit hitInfo;

                float launchOffset = 0f;

                if (Physics.Raycast(firingPos, firingDir, out hitInfo, launchOffset, 1236478737))
                    launchOffset = hitInfo.distance - 0.1f;

                var mlrsRocketEntity = GameManager.server.CreateEntity("assets/content/vehicles/mlrs/rocket_mlrs.prefab", firingPos + firingDir * launchOffset);

                if (mlrsRocketEntity == null)
                {
                    mlrsRocketProjectile = null;
                    return false;
                }

                mlrsRocketProjectile = mlrsRocketEntity.GetComponent<ServerProjectile>();

                var velocityVector = mlrsRocketProjectile.initialVelocity + firingDir * mlrsRocketProjectile.speed;

                mlrsRocketProjectile.InitializeVelocity(velocityVector);

                var mlrsRocket = mlrsRocketEntity as MLRSRocket;

                if (mlrsRocket == null)
                    return false;

                mlrsRocket.OwnerID = 0;

                mlrsRocket.Spawn();

                return true;
            }

            public void Update()
            {
                ulong id = 0UL;
                if (_ == null)
                {
                    UnityEngine.GameObject.Destroy(this.gameObject);
                    return;
                }

                if (isDestroyed || !active)
                    return;

                if (EndEventTime < Time.time)
                {
                    _.PrintWarning($"Ending event at {location}");
                    Destroy();
                    return;
                }

                if (sendGUI)
                {
                    bool send = false;
                    if (lastUINotification == DateTime.MinValue)
                    {
                        lastUINotification = DateTime.Now;
                        send = true;
                    }
                    else
                    {
                        TimeSpan ts = DateTime.Now - lastUINotification;
                        if (ts.TotalSeconds > 1)
                        {
                            send = true;
                        }
                        else
                        {
                            send = false;
                        }
                    }

                    if (send)
                    {
                        lastUINotification = DateTime.Now;
                        if (_onlinePlayers.Count <= 0)
                        {
                            updatePlayers();
                        }
                        else
                        {
                            foreach (BasePlayer onPlayers in _onlinePlayers)
                                if (onPlayers != null && onPlayers.IsConnected)
                                    SendClockGUI(onPlayers);
                        }
                    }
                }

                if (config.FireMlrs && MlrsEventTime < Time.time && !EventRandomManager.isFireingMlrs && MlrsEventTime != 0)
                {
                    MlrsEventTime = 0;
                    setupMlrs(this);
                }

                if (!_.configData.settings.pauseWave)
                {
                    if (config.TotalNpcWaves > 0 && totalWaves <= config.TotalNpcWaves && nextWaveTime < Time.time)
                    {
                        nextWaveTime = Time.time + config.NextWaveSeconds;

                        if (currentWave >= config.TotalNpcWaves)
                        {
                            time = DateTime.Now;

                            int totalMinus = config.NextWaveSeconds * currentWave;
                            Duration = config.RunTimeSeconds - totalMinus;
                            TimeSpan timeOffset = new TimeSpan();
                            lastBlock = DateTime.Now;
                            lastNotification = DateTime.MinValue;
                            lastUINotification = DateTime.MinValue;
                        }
                        else
                        {
                            Duration = config.NextWaveSeconds;
                            TimeSpan timeOffset = new TimeSpan();
                            lastBlock = DateTime.Now;
                            lastNotification = DateTime.MinValue;
                            lastUINotification = DateTime.MinValue;
                        }
                        if (QueuedRoutine != null)
                            stopSpawning();
                        QueuedRoutine = StartCoroutine(GenerateEventMembers());
                        return;
                    }
                }
                else
                {
                    if (!newWave && config.TotalNpcWaves > 0 && totalWaves <= config.TotalNpcWaves && members.Count <= 0 && !curentlySawning)
                    {
                        curentlySawning = true;
                        nextWaveTime = Time.time + config.NextWaveSeconds;

                        if (currentWave >= config.TotalNpcWaves)
                        {
                            time = DateTime.Now;

                            Duration = config.NextWaveSeconds;
                            TimeSpan timeOffset = new TimeSpan();
                            lastBlock = DateTime.Now;
                            lastNotification = DateTime.MinValue;
                            lastUINotification = DateTime.MinValue;
                        }
                        else
                        {
                            Duration = config.NextWaveSeconds;
                            TimeSpan timeOffset = new TimeSpan();
                            lastBlock = DateTime.Now;
                            lastNotification = DateTime.MinValue;
                            lastUINotification = DateTime.MinValue;
                        }

                        return;
                    }
                    else if (curentlySawning && members.Count <= 0 && lastBlock.AddSeconds(Duration) - DateTime.Now <= TimeSpan.Zero)
                    {
                        Duration = 30;
                        TimeSpan timeOffset = new TimeSpan();
                        lastBlock = DateTime.Now;
                        lastNotification = DateTime.MinValue;
                        lastUINotification = DateTime.MinValue;

                        if (QueuedRoutine != null)
                            stopSpawning();
                        QueuedRoutine = StartCoroutine(GenerateEventMembers());
                        showInfinity = true;
                    }
                }

                if ((totalWaves - 1) == config.TotalNpcWaves && members.Count <= 0)
                {
                    Destroy(true);
                    return;
                }
            }


            public void updatePlayers()
            {
                foreach (var user in _authedPlayers)
                {
                    BasePlayer TCplayer = BasePlayer.FindByID(user);
                    if (TCplayer != null && !_onlinePlayers.Contains(TCplayer))
                        _onlinePlayers.Add(TCplayer);
                }
            }

            public void SendClockGUI(BasePlayer current)
            {
                if (current == null)
                    return;

                CuiHelper.DestroyUi(current, "RtimerS" + BlockName);
                CuiHelper.DestroyUi(current, "RsurrenderS" + BlockName);

                TimeSpan ts = lastBlock.AddSeconds(Duration) - DateTime.Now;

                int totalWavesNext = config.TotalNpcWaves + 1;

                string countDownM = FormatTime(ts);
                string countDownS = FormatTimeS(ts);
                string waveMessage = _.lang.GetMessage("nextWave", _);
                if (currentWave >= totalWavesNext)
                    waveMessage = _.lang.GetMessage("endEvent", _);

                string message = string.Format(_.lang.GetMessage("guiMessage", _), currentWave, totalWavesNext, members.Count, waveMessage, countDownM, countDownS);
                string message2 = string.Format(_.lang.GetMessage("guiMessageSurrenderNew", _), surrenderCost, surrenderName);

                if (_.configData.settings.pauseWave)

                {
                    if (members.Count > 0 || showInfinity)
                        message = string.Format(_.lang.GetMessage("guiMessage", _), currentWave, totalWavesNext, members.Count, waveMessage, "∞", "∞");
                }

                var elements = new CuiElementContainer();

                var BlockMsg = elements.Add(new CuiPanel
                {
                    Image = { Color = "0.55 0.55 0.55 0.99" },
                    RectTransform = { AnchorMin = _.configData.settings.AnchorMin, AnchorMax = _.configData.settings.AnchorMax } }, "Hud", "RtimerS" + BlockName);

                elements.Add(new CuiElement
                {
                    Parent = BlockMsg,
                    Components = { new CuiRawImageComponent { Sprite = "assets/icons/explosion.png", Color = "0.95 0.4 0.02 0.99"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.13 1" } }
                });

                elements.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.15 0", AnchorMax = "1 1" },
                    Text = { Text = message, FontSize = 11, Align = TextAnchor.MiddleLeft, }
                }, BlockMsg);

                var BlockSurrender = elements.Add(new CuiPanel
                {
                    Image = { Color = "0.55 0.55 0.55 0.99" },
                    RectTransform = { AnchorMin = _.configData.settings.AnchorMin2, AnchorMax = _.configData.settings.AnchorMax2 } }, "Hud", "RsurrenderS" + BlockName);


                elements.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.05 0", AnchorMax = "1 1" },
                    Text = { Text = message2, FontSize = 11, Align = TextAnchor.MiddleLeft, }
                }, BlockSurrender);

                CuiHelper.AddUi(current, elements);
            }

            public void RemoveNpc(RandomRaider player)
            {
                if (members != null && members.Contains(player))
                    members.Remove(player);
                if (_AllMembers != null && _AllMembers.Contains(player))
                    _AllMembers.Remove(player);
            }

            public void Destroy(bool win = false)
            {
                if (isDestroyed)
                    return;

                isDestroyed = true;

                if (boombox != null)
                    boombox?.Kill();

                if (win && config.WinDrop > 0)
                {
                    if (config.lootProfileAirDrop.Count > 0)
                        dropWin(location, config.WinDrop, config.lootProfileAirDrop, _authedPlayers);
                    else dropWin(location, config.WinDrop, config.lootProfileAirDropA, _authedPlayers, true);

                }
                ulong id = 0UL;
                active = false;
                MlrsEventTime = 1;
                Interface.Oxide.CallHook("RandomRaidEventEnd", location);
                stopSpawning(true);

                if (marker != null)
                {
                    marker?.CancelInvoke(marker.DelayedDestroy);
                    marker?.Kill(BaseNetworkable.DestroyMode.None);
                }

                for (int i = members.Count - 1; i >= 0; i--)
                {
                    RandomRaider npc = members[i];
                    if (npc != null && !npc.IsDestroyed && !npc.isMounted)
                    {
                        npc.Kill();
                    }
                }

                foreach (var user in _authedPlayers)
                {
                    BasePlayer TCplayer = BasePlayer.FindByID(user);
                    if (TCplayer != null && TCplayer.IsConnected)
                    {
                        _.SendReply(TCplayer, _.lang.GetMessage("RaidersLeaving", _, TCplayer.UserIDString));
                        CuiHelper.DestroyUi(TCplayer, "RtimerS" + BlockName);
                        CuiHelper.DestroyUi(TCplayer, "RsurrenderS" + BlockName);
                    }
                }

                _.NextTick(() =>
                {
                    members.Clear();
                    Facepunch.Pool.FreeUnmanaged(ref members);
                    _authedPlayers.Clear();
                    _AllFoundations.Clear();
                    if (_AllRaids.Contains(this))
                        _AllRaids.Remove(this);
                    if (heli != null && !heli.IsDestroyed)
                        heli.GetComponent<PatrolHelicopterAI>()?.Retire();
                    jconfig = null;
                    savedPosition = null;
                    _onlinePlayers = null;
                    location = Vector3.zero;
                });
                UnityEngine.GameObject.Destroy(this.gameObject);
            }

            private List<string> getObjects(object name)
            {
                List<string> kitObject = new List<string>();
                if (name is string || name is int || name is float)
                    return kitObject;
                var json = JsonConvert.SerializeObject(name);
                if (json != null)
                    kitObject = JsonConvert.DeserializeObject<List<string>>(json) as List<string>;
                return kitObject;
            }
            public static void giveItem(BasePlayer RPlayer, int itemID, int total)
            {
                var item = ItemManager.CreateByItemID(itemID, total, 0);
                if (item == null) return;

                if (item.MoveToContainer(RPlayer.inventory.containerBelt, -1, true))
                    return;

                else if (item.MoveToContainer(RPlayer.inventory.containerMain, -1, true))
                    return;

                Vector3 velocity = Vector3.zero;
                item.Drop(RPlayer.transform.position + new Vector3(0.5f, 1f, 0), velocity);
                _.SendReply(RPlayer, _.lang.GetMessage("RewardDropedOnGround", _));
            }

            public void stopSpawning(bool destroying = false)
            {
                if (QueuedRoutine != null)
                    StopCoroutine(QueuedRoutine);
                QueuedRoutine = null;
                float newTime = config.NextWaveSeconds + (config.TotalNpcs * 0.1f) + 12f;
                totalWaves++;
                if (!destroying && jconfig != null && jconfig.Count > 0)
                {
                    foreach (var key in jconfig)
                    {
                        if (key.juggernautOnWaves.ContainsKey(totalWaves))
                        {
                            if (savedPosition != null && savedPosition.Count > 0)
                            {
                                for (int spawnTotals = 0; spawnTotals < key.juggernautOnWaves[totalWaves]; spawnTotals++)
                                {
                                    Vector3 position = savedPosition.GetRandom();
                                    RandomRaider theNewGuy = SpawnNPC(position, key);
                                    if (theNewGuy != null)
                                    {
                                        members.Add(theNewGuy);
                                        if (theNewGuy != null)
                                            _AllMembers.Add(theNewGuy);
                                        if (debug) _.PrintWarning($"Spawning juggernaut at: {position} for Wave: {totalWaves}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public IEnumerator GenerateEventMembers()
            {
                currentWave++;
                ulong id = 0UL;
                showInfinity = true;
                if (newWave)
                {
                    _.PrintWarning($"Random Raid started for {location}");
                }
                else
                {
                    if (debug) _.PrintWarning($"Random Raid next wave started for {location}");
                }
                newWave = false;
                int totalNpc = 0;
                float spawnDictance = 40;
                float radius = 200;
                if (config == null)
                {
                    stopSpawning();
                }

                List<Vector3> positions = generateSpawnList(location, spawnDictance, radius / config.TotalNpcs);

                if (savedPosition.Count <= 0)
                    savedPosition = new List<Vector3>(positions);

                yield return CoroutineEx.waitForSeconds(5f);
                while (positions.Count < config.TotalNpcs)
                {
                    if (radius < 50 || (radius / config.TotalNpcs) <= 0)
                    {
                        _.PrintWarning($"Not enough spawn positions stopping raid attempt {location}");
                        Destroy();
                        stopSpawning();
                        yield return CoroutineEx.waitForSeconds(10f);
                    }
                    radius = radius - 10f;
                    spawnDictance = spawnDictance + 2f;

                    positions = generateSpawnList(location, spawnDictance, radius / config.TotalNpcs);

                    _.PrintWarning($"Not enough spawn positions increasing radius for building to {spawnDictance}");
                    yield return CoroutineEx.waitForSeconds(5f);
                }
                yield return CoroutineEx.waitForSeconds(1f);
                while (totalNpc < config.TotalNpcs)
                {
                    Vector3 position = positions.GetRandom();
                    positions.Remove(position);
                    RandomRaider theNewGuy = SpawnNPC(position);
                    if (theNewGuy != null)
                    {
                        members.Add(theNewGuy);
                        yield return CoroutineEx.waitForSeconds(0.1f);
                        if (theNewGuy != null && theNewGuy != null)
                            _AllMembers.Add(theNewGuy);
                        if (!active)
                            active = true;
                    }

                    totalNpc++;
                    yield return CoroutineEx.waitForSeconds(1.0f);
                }
                curentlySawning = false;
                showInfinity = false;
                /* float newTime = config.NextWaveSeconds + (config.TotalNpcs * 0.1f) + 12f;
                   nextWaveTime = Time.time + newTime;
                   totalWaves++; 
                   raidernewID = "";*/
                stopSpawning();
            }

            #region Spawning
            public RandomRaider SpawnNPC(Vector3 pos, juggernautConfig isJug = null)
            {
                pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                object position = FindPointOnNavmesh(pos, 10f);
                if (position is Vector3 && (Vector3)position != Vector3.zero)
                {
                    RandomRaider scientistNPC = InstantiateEntity((Vector3)position, Quaternion.Euler(0, 0, 0));

                    if (scientistNPC == null) return null;

                    scientistNPC.enableSaving = false;
                    scientistNPC.isJug = isJug;
                    scientistNPC.skinID = 8675309;
                    scientistNPC.Spawn();
                    scientistNPC.gameObject.SetActive(true);
                    scientistNPC.manager = this;
                    Interface.Oxide.CallHook("OnRandomRaidRaiderSpawned", location, scientistNPC, isJug);
                    _.NextTick(() =>
                    {
                        if (scientistNPC != null)
                        {
                            scientistNPC.InitNewSettings(location, config.Health);
                            if (config.kits.Count > 0)
                            {
                                if (!string.IsNullOrEmpty(sameKit))
                                    scientistNPC.setUpGear(sameKit);
                                else
                                    scientistNPC.setUpGear(config.kits.GetRandom());
                            }
                            else scientistNPC.setUpGear("");
                        }
                    });
                    if (scientistNPC != null)
                    {
                        if (config.NpcNames.Count > 0)
                        {
                            string name = config.NpcNames.GetRandom();
                            scientistNPC.displayName = name;
                            scientistNPC.npcName = name;
                        }
                        else
                        {
                            scientistNPC.displayName = "A Raiding Npc";
                            scientistNPC.npcName = "A Raiding Npc";
                        }

                        return scientistNPC;
                    }
                }

                return null;
            }
            
            private static List<Vector3> generateSpawnList(Vector3 position, float radius = 100, float next = 2f)
            {
                List<Vector3> placement = new List<Vector3>();

                placement = GetCircumferencePositions(position, radius, next, position.y);
                foreach (Vector3 locat in placement.ToList())
                {
                    if (HasCupboard(locat) != null)
                        placement.Remove(locat);

                    if (IsInRockPrefab(locat))
                        placement.Remove(locat);

                    if (IsNearWorldCollider(locat))
                        placement.Remove(locat);
                }
                return placement;
            }

            private static BuildingPrivlidge HasCupboard(Vector3 position)
            {
                BuildingPrivlidge f = null;
                var list = Pool.Get<List<BuildingPrivlidge>>();
                Vis.Entities(position, 5f, list);
                foreach (var i in list)
                {
                    if (i.IsValid() && i is BuildingPrivlidge)
                    {
                        f = i;
                        break;
                    }
                }
                Pool.FreeUnmanaged(ref list);
                return f;
            }

            private static List<Vector3> GetCircumferencePositions(Vector3 center, float radius, float next, float y)
            {
                var positions = new List<Vector3>();
                float degree = 0f;

                while (degree < 360)
                {
                    float angle = (float)(2 * Math.PI / 360) * degree;
                    float x = center.x + radius * (float)Math.Cos(angle);
                    float z = center.z + radius * (float)Math.Sin(angle);
                    var position = new Vector3(x, center.y, z);
                    object success = FindPointOnNavmesh(position, 10f);
                    if (success != null)
                        positions.Add((Vector3)success);

                    degree += next;
                }

                return positions;
            }

            private static NavMeshHit navmeshHit;

            private static RaycastHit raycastHit;

            private static Collider[] _buffer = new Collider[256];

            private const int WORLD_LAYER = 65536;

            public static object FindPointOnNavmesh(Vector3 targetPosition, float maxDistance = 4f)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 position = i == 0 ? targetPosition : targetPosition + (UnityEngine.Random.onUnitSphere * maxDistance);
                    if (NavMesh.SamplePosition(position, out navmeshHit, maxDistance, 1))
                    {
                        if (IsInRockPrefab2(navmeshHit.position))
                            continue;

                        if (IsInRockPrefab(navmeshHit.position))
                            continue;

                        if (IsNearWorldCollider(navmeshHit.position))
                            continue;

                        return navmeshHit.position;
                    }
                }
                return null;
            }

            public static bool IsInRockPrefab(Vector3 position)
            {
                Physics.queriesHitBackfaces = true;

                bool isInRock = Physics.Raycast(position, Vector3.up, out raycastHit, 20f, WORLD_LAYER, QueryTriggerInteraction.Ignore) &&
                                blockedColliders.Any(s => raycastHit.collider?.gameObject?.name.Contains(s) ?? false);

                Physics.queriesHitBackfaces = false;

                return isInRock;
            }

            public static bool IsInRockPrefab2(Vector3 position)
            {
                Vector3 p1 = position + new Vector3(0, 20f, 0);
                Vector3 p2 = position + new Vector3(0, -2f, 0);
                Vector3 diff = p2 - p1;
                RaycastHit[] hits;
                hits = Physics.RaycastAll(p1, diff, diff.magnitude);
                if (hits.Length > 0)
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        RaycastHit hit = hits[i];
                        if (hit.collider != null)
                        {
                            bool isRock = blockedColliders.Any(s => hit.collider?.gameObject?.name.ToLower().Contains(s) ?? false);
                            if (isRock)
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            public static bool IsNearWorldCollider(Vector3 position)
            {
                Physics.queriesHitBackfaces = true;

                int count = Physics.OverlapSphereNonAlloc(position, 2f, _buffer, WORLD_LAYER, QueryTriggerInteraction.Ignore);
                Physics.queriesHitBackfaces = false;

                if (count == 0)
                    return false;

                int removed = 0;
                for (int i = 0; i < count; i++)
                {
                    if (acceptedColliders.Any(s => _buffer[i].gameObject.name.Contains(s)))
                    {
                        removed++;
                    }
                }


                return count - removed > 0;
            }

            private static readonly string[] acceptedColliders = new string[] { "ice_lake", "road", "carpark", "rocket_factory", "range", "train_track", "runway", "_grounds", "concrete_slabs", "lighthouse", "cave", "office", "walkways", "sphere", "tunnel", "industrial", "junkyard" };

            private static readonly string[] blockedColliders = new string[] { "rock", "junk", "range", "invisible", "cliff", "prevent_movement", "formation_" };
            #endregion
        }
        #endregion

        #region RandomRaider Spawn Method
        private static RandomRaider InstantiateEntity(Vector3 position, Quaternion rotation)
        {
            GameObject gameObject = Instantiate.GameObject(GameManager.server.FindPrefab("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab"), position, Quaternion.identity);
            gameObject.name = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab";
            gameObject.SetActive(false);
            ScientistNPC scientistNPC = gameObject.GetComponent<ScientistNPC>();
            ScientistBrain defaultBrain = gameObject.GetComponent<ScientistBrain>();

            defaultBrain._baseEntity = scientistNPC;

            RandomRaider component = gameObject.AddComponent<RandomRaider>();
            RandomRaiderBrain brains = gameObject.AddComponent<RandomRaiderBrain>();
            brains.Pet = false;
            brains.AllowedToSleep = false;
            CopyFields<NPCPlayer>(scientistNPC, component);

            brains._baseEntity = component;

            UnityEngine.Object.DestroyImmediate(defaultBrain, true);
            UnityEngine.Object.DestroyImmediate(scientistNPC, true);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

            return component;
        }

        private static void CopyFields<T>(T src, T dst)
        {
            var fields = typeof(T).GetFields();

            foreach (var field in fields)
            {
                if (field.IsStatic) continue;
                object value = field.GetValue(src);
                field.SetValue(dst, value);
            }
        }
        #endregion

        #region RandomRaider
        public class RandomRaider : NPCPlayer, IAISenses, IAIAttack, IThinker
        {
            public int AdditionalLosBlockingLayer;
            public LootContainer.LootSpawnSlot[] LootSpawnSlots;
            public float aimConeScale = 20f;
            public float lastDismountTime;
            [NonSerialized]
            protected bool lightsOn;
            private float nextZoneSearchTime;
            private AIInformationZone cachedInfoZone;
            private float targetAimedDuration;
            private float lastAimSetTime;
            public float playerDamage = 1.0f;

            private Vector3 aimOverridePosition = Vector3.zero;

            public EventRandomManager manager { get; set; }

            private bool isInit { get; set; }

            public BaseCombatEntity currentTarget { get; private set; }

            public BaseCombatEntity currentTargetOverride { get; set; }

            public List<int> _ProjectilesSlot { get; private set; }

            public List<int> _BaseLauncherSlot { get; private set; }

            public List<int> _MeleWeaponSlot { get; private set; }

            public Vector3 HomePosition { get; set; }

            private float lastTargetTime { get; set; }

            private Vector3 lastTargetPosition { get; set; }

            public Vector3 lostPosition() => lastTargetPosition;

            public bool GoodLastTime() => lastTargetTime > Time.time;

            public override float StartHealth() => this.startHealth;

            public override float StartMaxHealth() => this.startHealth;

            public override float MaxHealth() => this.startHealth;

            public RandomRaiderBrain Brain { get; private set; }

            public override bool IsLoadBalanced() => true;

            public bool HasTarget() => (currentTarget != null);

            public bool HasTargetOverride() => (currentTargetOverride != null);

            public List<ulong> DontIgnorePlayers = new List<ulong>();

            public int totalShotRockets { get; set; }

            public int totalTossedExplosives { get; set; }

            public juggernautConfig isJug { get; set; }

            public bool isJugernaut { get; private set; }

            public float isTalking { get; set; }

            public float isTalkingIdle { get; set; }

            public string npcName { get; set; }

            public override string displayName => npcName;

            public override void ServerInit()
            {
                if (NavAgent == null)
                    NavAgent = GetComponent<NavMeshAgent>();

                NavAgent.areaMask = 1;
                NavAgent.agentTypeID = -1372625422;

                GetComponent<BaseNavigator>().DefaultArea = "Walkable";

                base.ServerInit();
                Brain = this.GetComponent<RandomRaiderBrain>();

                AIThinkManager.Add((IThinker)this);

                InitNewSettings(this.transform.position);
                InitMovementSettings();
                setupweapons();
                isInit = true;
            }

            public void InitNewSettings(Vector3 home, float health = 100f, float Memory = 20f, float SenseRange = 30f, float LostRange = 250f, float attackRange = 2f, bool isMounted = false)
            {
                if (!this.isServer && !isInit)
                {
                    _.NextFrame(() => { if (this != null) InitNewSettings(home, health, Memory, SenseRange, LostRange, attackRange, isMounted); });
                    return;
                }
                if (home == Vector3.zero) HomePosition = this.transform.position;
                else HomePosition = home;

                if (isJug == null)
                {
                    this.startHealth = health;
                    InitializeHealth(health, health);
                    this.health = health;
                    if (manager != null)
                        playerDamage = manager.config.playerDamage;
                }
                else
                {
                    isJugernaut = true;
                    health = isJug.Health;
                    this.startHealth = isJug.Health;
                    InitializeHealth(isJug.Health, isJug.Health);
                    this.health = isJug.Health;
                    if (manager != null)
                        this.playerDamage = isJug.playerDamage;
                }

                Brain.Senses.MemoryDuration = Memory;
                Brain.AttackRangeMultiplier = attackRange;
                Brain.SenseRange = SenseRange;
                Brain.TargetLostRange = LostRange;
            }

            public void InitMovementSettings(float Acceleration = 12f, float Speed = 6.0f, float TurnSpeed = 120f, float NormalSpeedFraction = 0.5f, float FastSpeedFraction = 1f, float LowHealthMaxSpeedFraction = 0.9f, float SlowestSpeedFraction = 0.16f, float SlowSpeedFraction = 0.3f, float BestMovementPointMaxDistance = 20f, float MaxRoamDistanceFromHome = 30f, float BestRoamPointMaxDistance = 25f, float BestCoverPointMaxDistance = 5f)
            {
                if (!this.isServer && !isInit)
                {
                    _.NextFrame(() => { if (this != null) InitMovementSettings(Acceleration, Speed, TurnSpeed, NormalSpeedFraction, FastSpeedFraction, LowHealthMaxSpeedFraction, SlowestSpeedFraction, SlowSpeedFraction, BestMovementPointMaxDistance, MaxRoamDistanceFromHome, BestRoamPointMaxDistance, BestCoverPointMaxDistance); });
                    return;
                }
                _.NextFrame(() =>
                {
                    Brain.Navigator.Acceleration = Acceleration;
                    Brain.Navigator.Speed = Speed;
                    Brain.Navigator.TurnSpeed = TurnSpeed;
                    Brain.Navigator.NormalSpeedFraction = NormalSpeedFraction;
                    Brain.Navigator.FastSpeedFraction = FastSpeedFraction;
                    Brain.Navigator.LowHealthMaxSpeedFraction = LowHealthMaxSpeedFraction;
                    Brain.Navigator.SlowestSpeedFraction = SlowestSpeedFraction;
                    Brain.Navigator.SlowSpeedFraction = SlowSpeedFraction;
                    Brain.Navigator.BestMovementPointMaxDistance = BestMovementPointMaxDistance;
                    Brain.Navigator.MaxRoamDistanceFromHome = MaxRoamDistanceFromHome;
                    Brain.Navigator.BestRoamPointMaxDistance = BestRoamPointMaxDistance;
                    Brain.Navigator.BestCoverPointMaxDistance = BestCoverPointMaxDistance;
                    if (manager.config != null && manager.config.aimConeScale > 0 && isJug == null)
                        aimConeScale = manager.config.aimConeScale;
                    else if (isJug != null)
                        aimConeScale = isJug.aimConeScale;
                });
            }

            public void setUpGear(string kitname = "")
            {
                if (this.IsDestroyed) return;

                if (isJug != null)
                {
                    if (isJug.kits != null && isJug.kits.Count > 0)
                    {
                        string kitJug = isJug.kits.GetRandom();
                        if (!string.IsNullOrEmpty(kitJug))
                        {
                            this.inventory.Strip();
                            object success = _.Kits?.Call("GetKitInfo", kitJug);
                            if (success == null)
                            {
                                addJugGear();
                                _.PrintWarning($"Unable to find a kit with name {kitJug}?");
                            }
                            else
                            {
                                JObject obj = success as JObject;
                                JArray items = obj["items"] as JArray;

                                for (int i = 0; i < items.Count; i++)
                                {
                                    JObject itemObject = items[i] as JObject;
                                    string container = (string)itemObject["container"];
                                    if (container == "wear")
                                        ItemManager.CreateByItemID((int)itemObject["itemid"], (int)itemObject["amount"], (ulong)itemObject["skinid"]).MoveToContainer(this.inventory.containerWear, -1);
                                    else if (container == "belt")
                                    {
                                        Item item = ItemManager.CreateByItemID((int)itemObject["itemid"], (int)itemObject["amount"], (ulong)itemObject["skinid"]);
                                        item.MoveToContainer(this.inventory.containerBelt, -1, true);
                                        this.inventory.containerBelt.OnChanged();
                                    }
                                }

                                setupweapons();

                                _.timer.Once(1f, () =>
                                {
                                    if (this == null) return;
                                    if (_ProjectilesSlot.Count > 0)
                                        EquipNewWeapon(_ProjectilesSlot[0]);
                                    else if (_MeleWeaponSlot.Count > 0)
                                        EquipNewWeapon(_MeleWeaponSlot[0]);
                                });
                            }
                        }
                    }
                    else
                    {
                        addJugGear();
                        EquipWeapon();
                        setupweapons();
                    }
                    return;
                }
                else
                {
                    totalShotRockets = manager.config.rockets;
                    totalTossedExplosives = manager.config.explosives;

                    this.inventory.containerBelt.capacity = 7;
                    if (!string.IsNullOrEmpty(kitname))
                    {
                        this.inventory.Strip();
                        ItemManager.CreateByName("rocket.launcher").MoveToContainer(this.inventory.containerBelt, 6);
                        object success = _.Kits?.Call("GetKitInfo", kitname);
                        if (success == null)
                        {
                            addDefaultGear();
                            _.PrintWarning($"Unable to find a kit with name {kitname}?");
                        }
                        else
                        {
                            JObject obj = success as JObject;
                            JArray items = obj["items"] as JArray;

                            for (int i = 0; i < items.Count; i++)
                            {
                                JObject itemObject = items[i] as JObject;
                                string container = (string)itemObject["container"];
                                if (container == "wear")
                                    ItemManager.CreateByItemID((int)itemObject["itemid"], (int)itemObject["amount"], (ulong)itemObject["skinid"]).MoveToContainer(this.inventory.containerWear, -1);
                                else if (container == "belt")
                                {
                                    Item item = ItemManager.CreateByItemID((int)itemObject["itemid"], (int)itemObject["amount"], (ulong)itemObject["skinid"]);
                                    item.MoveToContainer(this.inventory.containerBelt, -1, true);
                                    this.inventory.containerBelt.OnChanged();
                                }
                            }

                            setupweapons();

                            _.timer.Once(1f, () =>
                            {
                                if (this == null) return;
                                if (_ProjectilesSlot.Count > 0)
                                    EquipNewWeapon(_ProjectilesSlot[0]);
                                else if (_MeleWeaponSlot.Count > 0)
                                    EquipNewWeapon(_MeleWeaponSlot[0]);
                            });
                        }
                    }
                    else
                    {
                        addDefaultGear();
                        EquipWeapon();
                        setupweapons();
                    }
                }
            }

            private void addDefaultGear()
            {
                switch (UnityEngine.Random.Range(0, 7))
                {
                    case 0:
                        ItemManager.CreateByName("scientistsuit_heavy").MoveToContainer(this.inventory.containerWear);
                        break;
                    case 1:
                        ItemManager.CreateByName("hazmatsuit_scientist_peacekeeper").MoveToContainer(this.inventory.containerWear);
                        break;
                    case 2:
                        ItemManager.CreateByName("hazmatsuit_scientist").MoveToContainer(this.inventory.containerWear);
                        break;
                    case 3:
                        ItemManager.CreateByName("hazmatsuit").MoveToContainer(this.inventory.containerWear);
                        break;
                    case 4:
                        ItemManager.CreateByName("scientistsuit_heavy").MoveToContainer(this.inventory.containerWear);
                        break;
                    case 5:
                        ItemManager.CreateByName("hazmatsuit_scientist_peacekeeper").MoveToContainer(this.inventory.containerWear);
                        break;
                    case 6:
                        ItemManager.CreateByName("hazmatsuit_scientist").MoveToContainer(this.inventory.containerWear);
                        break;
                    case 7:
                        ItemManager.CreateByName("hazmatsuit").MoveToContainer(this.inventory.containerWear);
                        break;
                    default:
                        ItemManager.CreateByName("hazmatsuit_scientist").MoveToContainer(this.inventory.containerWear);
                        break;
                }

                switch (UnityEngine.Random.Range(0, 9))
                {
                    case 0:
                        ItemManager.CreateByName("smg.mp5").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 1:
                        ItemManager.CreateByName("shotgun.spas12").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 2:
                        ItemManager.CreateByName("rifle.lr300").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 3:
                        ItemManager.CreateByName("lmg.m249").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 4:
                        ItemManager.CreateByName("pistol.m92").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 5:
                        ItemManager.CreateByName("smg.mp5").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 6:
                        ItemManager.CreateByName("shotgun.spas12").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 7:
                        ItemManager.CreateByName("rifle.lr300").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 8:
                        ItemManager.CreateByName("lmg.m249").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 9:
                        ItemManager.CreateByName("pistol.m92").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    default:
                        ItemManager.CreateByName("pistol.m92").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                }
                ItemManager.CreateByName("rocket.launcher").MoveToContainer(this.inventory.containerBelt, 6);
                ItemManager.CreateByName("multiplegrenadelauncher").MoveToContainer(this.inventory.containerBelt, 8);
                this.inventory.containerBelt.OnChanged();
            }

            private void addJugGear()
            {
                this.inventory.Strip();

                ItemManager.CreateByName("frankensteins.monster.03.head").MoveToContainer(this.inventory.containerWear);
                ItemManager.CreateByName("frankensteins.monster.03.legs").MoveToContainer(this.inventory.containerWear);
                ItemManager.CreateByName("frankensteins.monster.03.torso").MoveToContainer(this.inventory.containerWear);

                switch (UnityEngine.Random.Range(0, 2))
                {
                    case 0:
                        ItemManager.CreateByName("smg.mp5").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 1:
                        ItemManager.CreateByName("lmg.m249").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    case 2:
                        ItemManager.CreateByName("smg.mp5").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                    default:
                        ItemManager.CreateByName("lmg.m249").MoveToContainer(this.inventory.containerBelt, 0);
                        break;
                }
                this.inventory.containerBelt.OnChanged();
            }

            private void setupweapons()
            {
                _ProjectilesSlot = new List<int>();
                _MeleWeaponSlot = new List<int>();
                _BaseLauncherSlot = new List<int>();
                foreach (Item slot in inventory.containerBelt.itemList)
                {
                    if (slot != null)
                    {
                        BaseEntity heldEntity = slot.GetHeldEntity();
                        if (heldEntity != null)
                        {
                            AttackEntity _attackEntity = heldEntity.GetComponent<AttackEntity>();
                            if (_attackEntity != null)
                            {
                                _attackEntity.deployDelay = 0.5f;

                                if (_attackEntity is BaseLauncher)
                                {
                                    _BaseLauncherSlot.Add(slot.position);
                                    _attackEntity.aiOnlyInRange = false;
                                }
                                else if (_attackEntity is BaseProjectile)
                                {
                                    _ProjectilesSlot.Add(slot.position);
                                    _attackEntity.aiOnlyInRange = false;
                                    (_attackEntity as BaseProjectile).reloadTime = 0.1f;

                                    if (_attackEntity.effectiveRange <= 5)
                                        _attackEntity.effectiveRange = 15f;
                                }
                                else _MeleWeaponSlot.Add(slot.position);
                            }
                        }
                    }
                }
                if (debug)
                {
                    _.PrintWarning($"DEBUG: Current Projectiles: {_ProjectilesSlot.Count}, Current BaseLaunchers: {_BaseLauncherSlot.Count}, Current Outers: {_MeleWeaponSlot.Count}");
                }
            }

            public HeldEntity EquipNewWeapon(int slot = 0)
            {
                if (this.IsDestroyed) return null;
                Item weapon = this.inventory.containerBelt.GetSlot(slot);
                if (weapon != null)
                {
                    HeldEntity heldEntity = weapon.GetHeldEntity() as HeldEntity;
                    if (heldEntity != null)
                    {
                        AttackEntity _attackEntity = heldEntity.GetComponent<AttackEntity>();
                        if (_attackEntity == null) return null;

                        this.UpdateActiveItem(weapon.uid);

                        _attackEntity.SetHeld(true);

                        if (_attackEntity is Chainsaw)
                            (_attackEntity as Chainsaw).ServerNPCStart();
                        heldEntity.SetHeld(true);
                        return heldEntity;
                    }
                }
                return null;
            }

            private BaseEntity UpdateTargets()
            {
                if (manager == null || _ == null)
                {
                    this.Kill();
                    return null;
                }
                if (this.IsDestroyed)
                    return null;

                BaseEntity target = null;
                float delta = -1f;
                foreach (BaseEntity baseEntity in Brain.Senses.Memory.Targets)
                {
                    if (baseEntity == null || baseEntity.Health() <= 0f)
                        continue;

                    if (baseEntity is BasePlayer && ShouldIgnorePlayer(baseEntity as BasePlayer))
                        continue;

                    if (!CanSeeTarget(baseEntity))
                        continue;

                    float distanceToTarget = Vector3.Distance(baseEntity.transform.position, transform.position);
                    if (distanceToTarget > Brain.TargetLostRange)
                        continue;

                    float rangeDelta = 1f - Mathf.InverseLerp(1f, Brain.SenseRange, distanceToTarget);

                    float dot = Vector3.Dot((baseEntity.transform.position - eyes.position).normalized, eyes.BodyForward());

                    rangeDelta += Mathf.InverseLerp(Brain.VisionCone, 1f, dot) / 2f;
                    rangeDelta += (Brain.Senses.Memory.IsLOS(baseEntity) ? 2f : 0f);

                    if (rangeDelta <= delta)
                        continue;

                    target = baseEntity;
                    delta = rangeDelta;
                }

                currentTarget = target as BaseCombatEntity;
                if (target != null)
                {
                    Brain.Senses.Memory.Threats.Add(target);
                    Brain.Senses.Memory.SetKnown(target, this, null);
                    lastTargetTime = Time.time + Brain.Senses.MemoryDuration;
                    lastTargetPosition = target.transform.position;
                }
                UpdateMemory();
                GetRaidReady();
                return target;
            }

            private float nextRaidTime { get; set; }
            private void GetRaidReady()
            {
                if (this.IsDestroyed) return;
                if (nextRaidTime < Time.time)
                {
                    nextRaidTime = Time.time + 20;
                    if (this == null || this.IsDestroyed || this.IsDead())
                        return;
                    if (currentTargetOverride == null && manager._AllFoundations.Count > 0)
                    {
                        currentTargetOverride = manager._AllFoundations.GetRandom();
                        if (currentTargetOverride != null)
                        {
                            HomePosition = currentTargetOverride.transform.position;
                        }
                        if (debug) _.PrintWarning("set random raid target");
                        return;
                    }
                }
            }

            private BasePlayer[] playerQueryResults = new BasePlayer[64];

            private void UpdateMemory()
            {
                int inSphere = BaseEntity.Query.Server.GetPlayersInSphere(this.transform.position, Brain.SenseRange, playerQueryResults, new Func<BaseEntity, bool>(AiCaresAbout));
                for (int i = 0; i < inSphere; i++)
                {
                    BaseCombatEntity baseCombatEntity = playerQueryResults[i] as BaseCombatEntity;
                    if (baseCombatEntity != null && !baseCombatEntity.EqualNetID(this) && IsVisibleToUs(baseCombatEntity))
                    {
                        Brain.Senses.Memory.SetKnown(baseCombatEntity, this, null);
                    }
                }
            }

            private bool AiCaresAbout(BaseEntity entity)
            {
                BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;

                if (baseCombatEntity == null || baseCombatEntity.IsDestroyed || baseCombatEntity.transform == null)
                    return false;

                if (baseCombatEntity.Health() <= 0f)
                    return false;

                if (baseCombatEntity is BasePlayer)
                {
                    BasePlayer player = baseCombatEntity as BasePlayer;

                    if (this.userID == player.userID)
                        return false;

                    if (player is global::HumanNPC)
                        return false;

                    if (player is RandomRaider)
                        return false;

                    if (!player.userID.IsSteamId() && !player.IsNpc)
                        return false;

                    return true;
                }

                if (baseCombatEntity is BaseNpc)
                    return false;

                return false;
            }
            public bool IsVisibleToUs(BaseCombatEntity baseCombatEntity)
            {
                if (baseCombatEntity is BasePlayer)
                    return IsPlayerVisibleToUs(baseCombatEntity as BasePlayer, Vector3.zero, 1218519041);
                else return IsVisible(baseCombatEntity.CenterPoint(), eyes.worldStandingPosition, float.PositiveInfinity);
            }

            private bool ShouldIgnorePlayer(BasePlayer player)
            {
                if (player == null || player.IsDead() || player.IsSleeping()) return true;
                return false;
            }

            public bool TargetInThrowableRange()
            {
                if (currentTargetOverride == null)
                    return false;
                return !hasWall();
            }

            public bool hasWall()
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, 5.2f, BuildingLayer))
                {
                    return true;
                }

                return false;
            }

            public void SetDestination(Vector3 destination)
            {
                if (Brain.Navigator == null) return;
                destination = new Vector3(destination.x, TerrainMeta.HeightMap.GetHeight(destination) + 0.5f, destination.z);
                Brain.Navigator.SetDestination(destination, BaseNavigator.NavigationSpeed.Fast);
            }

            public override void DestroyShared()
            {
                if (manager != null)
                    manager.RemoveNpc(this);
                base.DestroyShared();
                AIThinkManager.Remove(this);
            }

            public void LightCheck()
            {
                if ((!TOD_Sky.Instance.IsNight || this.lightsOn) && (!TOD_Sky.Instance.IsDay || !this.lightsOn))
                    return;
                this.LightToggle();
                this.lightsOn = !this.lightsOn;
            }

            public override float GetAimConeScale() => this.aimConeScale;

            public override void EquipWeapon(bool skipDeployDelay = false) => base.EquipWeapon();

            public override void DismountObject()
            {
                base.DismountObject();
                this.lastDismountTime = Time.time;
            }

            public bool RecentlyDismounted() => (double)Time.time < (double)this.lastDismountTime + 10.0;

            public virtual float GetIdealDistanceFromTarget() => Mathf.Max(5f, this.EngagementRange() * 0.75f);

            public AIInformationZone GetInformationZone(Vector3 pos)
            {
                if ((UnityEngine.Object)this.VirtualInfoZone != (UnityEngine.Object)null)
                    return this.VirtualInfoZone;
                if ((UnityEngine.Object)this.cachedInfoZone == (UnityEngine.Object)null || (double)Time.time > (double)this.nextZoneSearchTime)
                {
                    this.cachedInfoZone = AIInformationZone.GetForPoint(pos);
                    this.nextZoneSearchTime = Time.time + 5f;
                }
                return this.cachedInfoZone;
            }

            public bool InEngagementRange()
            {
                AttackEntity attackEntity = this.GetAttackEntity();
                if (currentTarget == null || attackEntity == null) return false;

                if (attackEntity is BaseProjectile && attackEntity.effectiveRange <= 1)
                    attackEntity.effectiveRange = 15f;

                return (double)Vector3Ex.Distance2D(this.transform.position, currentTarget.transform.position) <= (attackEntity.effectiveRange * this.Brain.AttackRangeMultiplier);
            }

            public float EngagementRange()
            {
                AttackEntity attackEntity = this.GetAttackEntity();
                return (bool)(UnityEngine.Object)attackEntity ? attackEntity.effectiveRange * (attackEntity.aiOnlyInRange ? 1f : 2f) * this.Brain.AttackRangeMultiplier : 300f;
            }

            public void SetDucked(bool flag)
            {
                this.modelState.ducked = flag;
                this.SendNetworkUpdate();
            }

            public float lastThinkTime { get; set; }

            public void Internal()
            {
                this.ServerThink(Time.time - this.lastThinkTime);
                this.lastThinkTime = Time.time;
            }

            public virtual void TryThink() => Internal();

            public override void ServerThink(float delta)
            {
                base.ServerThink(delta);
                if (!this.Brain.ShouldServerThink())
                    return;
                UpdateTargets();
                this.Brain.DoThink();
            }

            public void TickAttack(float delta, BaseCombatEntity target, bool targetIsLOS)
            {
                if ((UnityEngine.Object)target == (UnityEngine.Object)null)
                    return;
                float num = Vector3.Dot(this.eyes.BodyForward(), (target.CenterPoint() - this.eyes.position).normalized);
                if (targetIsLOS)
                {
                    if ((double)num > 0.200000002980232)
                        this.targetAimedDuration += delta;
                }
                else
                {
                    if ((double)num < 0.5)
                        this.targetAimedDuration = 0.0f;
                    this.CancelBurst();
                }
                if ((double)this.targetAimedDuration >= 0.200000002980232 & targetIsLOS)
                {
                    bool flag = false;
                    IAIAttack aiAttack = (IAIAttack)this;
                    float dist = 0.0f;
                    if (aiAttack != null)
                    {
                        flag = aiAttack.IsTargetInRange((BaseEntity)target, out dist);
                    }
                    else
                    {
                        AttackEntity attackEntity = this.GetAttackEntity();
                        if ((bool)(UnityEngine.Object)attackEntity)
                        {
                            dist = (UnityEngine.Object)target != (UnityEngine.Object)null ? Vector3.Distance(this.transform.position, target.transform.position) : -1f;
                            flag = (double)dist < (double)attackEntity.effectiveRange * (attackEntity.aiOnlyInRange ? 1.0 : 2.0);
                        }
                    }
                    if (!flag)
                        return;
                    this.ShotTest(dist);
                }
                else
                    this.CancelBurst();
            }

            private BasePlayer lastDamagePlayer { get; set; }

            public override void Hurt(HitInfo info)
            {
                BaseEntity initiator = info.Initiator;
                if (initiator != null && initiator.EqualNetID((BaseNetworkable)this))
                    return;
                if (manager != null && initiator != null)
                {
                    if (_.configData.settings.damageDelay > 0 && Brain != null && (Brain.spawnAge + _.configData.settings.damageDelay) > Time.time)
                        return;
                }
                base.Hurt(info);
                if (initiator == null || initiator.EqualNetID((BaseNetworkable)this))
                    return;
                this.Brain.Senses.Memory.SetKnown(initiator, (BaseEntity)this, (AIBrainSenses)null);
                lastTargetTime = Time.time + Brain.Senses.MemoryDuration;
                lastTargetPosition = initiator.transform.position;
                if (initiator is BasePlayer)
                {
                    lastDamagePlayer = initiator as BasePlayer;
                }
            }

            public float GetAimSwayScalar() => 1f - Mathf.InverseLerp(1f, 3f, Time.time - this.lastGunShotTime);

            public override Vector3 GetAimDirection() => (UnityEngine.Object)this.Brain != (UnityEngine.Object)null && (UnityEngine.Object)this.Brain.Navigator != (UnityEngine.Object)null && this.Brain.Navigator.IsOverridingFacingDirection ? this.Brain.Navigator.FacingDirectionOverride : base.GetAimDirection();

            public override void SetAimDirection(Vector3 newAim)
            {
                if (newAim == Vector3.zero)
                    return;
                float num = Time.time - this.lastAimSetTime;
                this.lastAimSetTime = Time.time;
                AttackEntity attackEntity = this.GetAttackEntity();
                if ((bool)(UnityEngine.Object)attackEntity)
                    newAim = attackEntity.ModifyAIAim(newAim, this.GetAimSwayScalar());
                if (this.isMounted)
                {
                    BaseMountable mounted = this.GetMounted();
                    Vector3 eulerAngles = mounted.transform.eulerAngles;
                    Vector3 vector3 = BaseMountable.ConvertVector(Quaternion.LookRotation(this.transform.InverseTransformDirection(Quaternion.Euler(Quaternion.LookRotation(newAim, mounted.transform.up).eulerAngles) * Vector3.forward), this.transform.up).eulerAngles);
                    newAim = BaseMountable.ConvertVector(Quaternion.LookRotation(this.transform.TransformDirection(Quaternion.Euler(Mathf.Clamp(vector3.x, mounted.pitchClamp.x, mounted.pitchClamp.y), Mathf.Clamp(vector3.y, mounted.yawClamp.x, mounted.yawClamp.y), eulerAngles.z) * Vector3.forward), this.transform.up).eulerAngles);
                }
                else
                {
                    BaseEntity parentEntity = this.GetParentEntity();
                    if ((bool)(UnityEngine.Object)parentEntity)
                    {
                        Vector3 vector3 = parentEntity.transform.InverseTransformDirection(newAim);
                        this.eyes.rotation = Quaternion.Lerp(this.eyes.rotation, Quaternion.LookRotation(new Vector3(newAim.x, vector3.y, newAim.z), parentEntity.transform.up), num * 25f);
                        this.viewAngles = this.eyes.bodyRotation.eulerAngles;
                        this.ServerRotation = this.eyes.bodyRotation;
                        return;
                    }
                }
                this.eyes.rotation = this.isMounted ? Quaternion.Slerp(this.eyes.rotation, Quaternion.Euler(newAim), num * 70f) : Quaternion.Lerp(this.eyes.rotation, Quaternion.LookRotation(newAim, this.transform.up), num * 25f);
                this.viewAngles = this.eyes.rotation.eulerAngles;
                this.ServerRotation = this.eyes.rotation;
            }

            public void SetStationaryAimPoint(Vector3 aimAt) => this.aimOverridePosition = aimAt;

            public void ClearStationaryAimPoint() => this.aimOverridePosition = Vector3.zero;

            public override bool ShouldDropActiveItem() => false;

            public override BaseCorpse CreateCorpse(PlayerFlags flagsOnDeath, Vector3 posOnDeath, Quaternion rotOnDeath, List<TriggerBase> triggersOnDeath, bool forceServerSide = false)
            {
                if (manager.members.Contains(this))
                    manager.members.Remove(this);


                NPCPlayerCorpse npcPlayerCorpse = this.DropCorpse("assets/prefabs/npc/scientist/scientist_corpse.prefab") as NPCPlayerCorpse;
                if (npcPlayerCorpse != null)
                {
                    npcPlayerCorpse.transform.position = npcPlayerCorpse.transform.position + Vector3.down * .05f;
                    npcPlayerCorpse.SetLootableIn(2f);
                    npcPlayerCorpse.SetFlag(BaseEntity.Flags.Reserved5, this.HasPlayerFlag(BasePlayer.PlayerFlags.DisplaySash));
                    npcPlayerCorpse.SetFlag(BaseEntity.Flags.Reserved2, true);
                    npcPlayerCorpse.TakeFrom(npcPlayerCorpse, this.inventory.containerMain, this.inventory.containerWear, this.inventory.containerBelt);
                    npcPlayerCorpse.playerName = this.OverrideCorpseName();
                    npcPlayerCorpse.playerSteamID = this.userID;
                    npcPlayerCorpse.name = "RandomRaider";
                    npcPlayerCorpse.Spawn();
                    npcPlayerCorpse.TakeChildren((BaseEntity)this);
                    foreach (ItemContainer container in npcPlayerCorpse.containers)
                        container.Clear();


                    if (_ != null && manager != null && manager.config != null && manager.config.NpcDropLoot && manager.config.lootProfile != null && manager.config.lootProfile.Count > 0)
                    {
                        _.NextTick(() =>
                        {
                            if (npcPlayerCorpse != null)
                            {
                                var lootcall = _.CustomLoot?.Call("MakeLoot", manager.config.lootProfile.GetRandom());

                                if (lootcall is string)
                                    _.PrintWarning((string)lootcall);
                                else if (lootcall != null && lootcall is List<Item>)
                                {
                                    foreach (var item in lootcall as List<Item>)
                                    {
                                        if (!item.MoveToContainer(npcPlayerCorpse.containers[0], -1, true))
                                            item.Remove();
                                    }
                                }
                            }
                        });
                    }
                    else if (_ != null && manager != null && manager.config != null && manager.config.NpcDropLoot && manager.config.lootProfileA != null && manager.config.lootProfileA.Count > 0)
                    {
                        _.NextTick(() =>
                        {
                            if (npcPlayerCorpse != null)
                            {
                                _.AlphaLoot?.Call("PopulateLoot", npcPlayerCorpse.containers[0], manager.config.lootProfileA.GetRandom());
                            }
                        });
                    }
                    else
                    {
                        global::HumanNPC theNPC = GameManager.server.FindPrefab("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roamtethered.prefab").GetComponent<global::HumanNPC>();
                        LootSpawnSlots = theNPC.LootSpawnSlots;

                        if (manager != null && manager.config.NpcDropLoot && this.LootSpawnSlots != null && this.LootSpawnSlots.Length != 0)
                        {
                            foreach (LootContainer.LootSpawnSlot lootSpawnSlot in this.LootSpawnSlots)
                            {
                                for (int index = 0; index < lootSpawnSlot.numberToSpawn; ++index)
                                {
                                    if ((double)UnityEngine.Random.Range(0.0f, 1f) <= (double)lootSpawnSlot.probability)
                                        lootSpawnSlot.definition.SpawnIntoContainer(npcPlayerCorpse.containers[0]);
                                }
                            }
                        }
                        else removeCorpse(npcPlayerCorpse);
                    }
                }
                return (BaseCorpse)npcPlayerCorpse;
            }

            public static void removeCorpse(NPCPlayerCorpse npcPlayerCorpse)
            {
                _.timer.Once(4f, () => { if (npcPlayerCorpse != null && !npcPlayerCorpse.IsDestroyed && npcPlayerCorpse.CanRemove()) npcPlayerCorpse.Kill(); });
            }

            protected virtual string OverrideCorpseName() => this.displayName;

            public override void AttackerInfo(PlayerLifeStory.DeathInfo info)
            {
                base.AttackerInfo(info);
                info.inflictorName = this.inventory.containerBelt.GetSlot(0).info.shortname;
                info.attackerName = "scientist";
            }

            public bool IsThreat(BaseEntity entity) => this.IsTarget(entity);

            public bool IsTarget(BaseEntity entity) => entity is BasePlayer && !entity.IsNpc || entity is BasePet;

            public bool IsFriendly(BaseEntity entity) => !((UnityEngine.Object)entity == (UnityEngine.Object)null) && (int)entity.prefabID == (int)this.prefabID;

            public bool CanAttack(BaseEntity entity) => true;

            public bool IsTargetInRange(BaseEntity entity, out float dist)
            {
                dist = Vector3.Distance(entity.transform.position, this.transform.position);
                return (double)dist <= (double)this.EngagementRange();
            }

            public bool CanSeeTarget(BaseEntity entity)
            {
                BasePlayer otherPlayer = entity as BasePlayer;
                if ((UnityEngine.Object)otherPlayer == (UnityEngine.Object)null)
                    return true;
                return this.AdditionalLosBlockingLayer == 0 ? this.IsPlayerVisibleToUs(otherPlayer, Vector3.zero, 1218519041) : this.IsPlayerVisibleToUs(otherPlayer, Vector3.zero, 1218519041 | 1 << this.AdditionalLosBlockingLayer);
            }

            public bool NeedsToReload() => false;

            public bool Reload() => true;

            public float CooldownDuration() => 5f;

            public bool IsOnCooldown() => false;

            public bool StartAttacking(BaseEntity entity) => true;

            public void StopAttacking()
            {
            }

            public float GetAmmoFraction() => this.AmmoFractionRemaining();

            public BaseEntity GetBestTarget()
            {
                BaseEntity baseEntity = (BaseEntity)null;
                float num1 = -1f;
                foreach (BaseEntity player in this.Brain.Senses.Players)
                {
                    if (!((UnityEngine.Object)player == (UnityEngine.Object)null) && (double)player.Health() > 0.0)
                    {
                        float num2 = 1f - Mathf.InverseLerp(1f, this.Brain.SenseRange, Vector3.Distance(player.transform.position, this.transform.position)) + Mathf.InverseLerp(this.Brain.VisionCone, 1f, Vector3.Dot((player.transform.position - this.eyes.position).normalized, this.eyes.BodyForward())) / 2f + (this.Brain.Senses.Memory.IsLOS(player) ? 2f : 0.0f);
                        if ((double)num2 > (double)num1)
                        {
                            baseEntity = player;
                            num1 = num2;
                        }
                    }
                }
                return baseEntity;
            }

            public void AttackTick(float delta, BaseEntity target, bool targetIsLOS)
            {
                BaseCombatEntity target1 = target as BaseCombatEntity;
                this.TickAttack(delta, target1, targetIsLOS);
            }

            public override bool IsOnGround() => true;
        }
        #endregion

        #region Brain
        public class RandomRaiderBrain : BaseAIBrain
        {
            private RandomRaider entityAI;
            public float spawnAge { get; private set; }
            RandomRaider humanNpc = null;

            private void Awake()
            {
                entityAI = GetComponent<RandomRaider>();
                spawnAge = Time.time;
            }
            public override void AddStates()
            {
                base.AddStates();
                AddState(new IdleState(entityAI));
                AddState(new CombatState(entityAI));
                AddState(new BaseLauncherState(entityAI));
            }

            public override void InitializeAI()
            {
                humanNpc = GetBaseEntity().GetComponent<RandomRaider>();

                UseAIDesign = false;

                base.InitializeAI();

                ThinkMode = AIThinkMode.Interval;
                thinkRate = 0.25f;
                PathFinder = new HumanPathFinder();
                ((HumanPathFinder)PathFinder).Init(humanNpc);
            }

            public override void Think(float delta)
            {
                if (!ConVar.AI.think || states == null)
                    return;

                this.ForceSetAge(this.Age + delta);
                lastThinkTime = Time.time;

                if (CurrentState != null)
                    CurrentState.StateThink(delta, this, humanNpc.currentTarget);

                if (CurrentState == null || CurrentState.CanLeave())
                {
                    float highest = 0f;
                    BasicAIState state = null;
                    AIState stateKey = new AIState();

                    foreach (var value in states)
                    {
                        if (value.Key == null || !value.Value.CanEnter())
                            continue;

                        float weight = value.Value.GetWeight();

                        if (weight <= highest)
                            continue;

                        highest = weight;
                        state = value.Value;
                        stateKey = value.Key;
                    }

                    if (state != CurrentState)
                        SwitchToState(stateKey, -1);
                }
            }

            public class IdleState : BasicAIState
            {
                private readonly RandomRaider entityAI;
                private float nextStrafeTime;

                public IdleState(RandomRaider entityAI) : base(AIState.Idle)
                {
                    this.entityAI = entityAI;
                }

                public AIState GetState() => AIState.Idle;

                public override float GetWeight()
                {
                    if ((entityAI != null && entityAI.Brain.spawnAge < Time.time + 10) || (entityAI != null && !entityAI.HasTarget() && !entityAI.GoodLastTime() && entityAI.currentTargetOverride == null))
                        return 20f;

                    return 0f;
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    if (debug) _.PrintWarning("Enter idle state");
                    entityAI.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                    base.StateEnter(brain, entity);
                    entityAI.isTalkingIdle = UnityEngine.Time.realtimeSinceStartup;
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);

                    if (entityAI == null || entityAI.IsDestroyed || entityAI.transform == null)
                        return StateStatus.Error;

                    if (Time.time > nextStrafeTime)
                    {
                        nextStrafeTime = Time.time + UnityEngine.Random.Range(5f, 8f);
                        if (entityAI != null)
                        {
                            Vector3 position = GetRandomPositionAround(entityAI.HomePosition, 1f, brain.Navigator.MaxRoamDistanceFromHome);
                            if (position != null && position is Vector3)
                                entityAI.SetDestination(position);
                        }
                    }

                    if (entityAI.isTalkingIdle < UnityEngine.Time.realtimeSinceStartup)
                    {
                        EventRandomManager.SendEffect(chatter, entityAI.transform.position, entityAI);
                        entityAI.isTalkingIdle = UnityEngine.Time.realtimeSinceStartup + UnityEngine.Random.Range(10.0f, 20.0f);
                    }

                    return StateStatus.Running;
                }

                public virtual Vector3 GetRandomPositionAround(Vector3 position, float minDistFrom = 0.0f, float maxDistFrom = 2f)
                {
                    if ((double)maxDistFrom < 0.0)
                        maxDistFrom = 0.0f;
                    Vector2 vector2 = UnityEngine.Random.insideUnitCircle * maxDistFrom;
                    float x = Mathf.Clamp(Mathf.Max(Mathf.Abs(vector2.x), minDistFrom), minDistFrom, maxDistFrom) * Mathf.Sign(vector2.x);
                    float z = Mathf.Clamp(Mathf.Max(Mathf.Abs(vector2.y), minDistFrom), minDistFrom, maxDistFrom) * Mathf.Sign(vector2.y);
                    return position + new Vector3(x, 0.0f, z);
                }
            }

            public class CombatState : BasicAIState
            {
                private readonly RandomRaider entityAI;
                private IAIAttack attack;
                private float nextStrafeTime;

                public CombatState(RandomRaider entityAI) : base(AIState.Cooldown)
                {
                    this.entityAI = entityAI;
                }

                public AIState GetState() => AIState.Cooldown;

                public override float GetWeight()
                {
                    if (entityAI.HasTarget())
                        return 30f;

                    return 0f;
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    if (debug) _.PrintWarning("Enter combat state");
                    entityAI.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                    base.StateEnter(brain, entity);
                    this.attack = entityAI as IAIAttack;
                    if (!entityAI.HasTarget())
                    {
                        this.brain.Navigator.SetFacingDirectionOverride(GetAimDirection(this.brain.Navigator.transform.position, entityAI.lostPosition()));
                    }
                    else
                    {
                        this.brain.Navigator.SetFacingDirectionOverride(GetAimDirection(this.brain.Navigator.transform.position, entityAI.currentTarget.transform.position));
                        if (this.attack.CanAttack(entityAI.currentTarget))
                            this.StartAttacking(entityAI.currentTarget);

                        entityAI.SetDestination(entityAI.currentTarget.transform.position);

                        entityAI.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                        if (entityAI.GetAttackEntity() is BaseProjectile)
                            this.brain.Navigator.StoppingDistance = 10f;
                        else this.brain.Navigator.StoppingDistance = 0.5f;

                    }
                    if (entityAI.isTalking < UnityEngine.Time.realtimeSinceStartup)
                    {
                        EventRandomManager.SendEffect(takecover, entityAI.transform.position, entityAI);
                        entityAI.isTalking = UnityEngine.Time.realtimeSinceStartup + UnityEngine.Random.Range(7.0f, 15.0f);
                    }
                }


                private void StopAttacking() => this.attack.StopAttacking();

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    this.brain.Navigator.ClearFacingDirectionOverride();
                    this.brain.Navigator.Stop();
                    this.StopAttacking();
                }


                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);

                    if (this.attack == null) return StateStatus.Error;
                    if (!entityAI.HasTarget())
                    {
                        if (nextStrafeTime < Time.time)
                        {
                            this.brain.Navigator.ClearFacingDirectionOverride();
                            nextStrafeTime = Time.time + 5;
                            Vector3 position = GetRandomPositionAround(entityAI.lostPosition(), 1f, 10f);
                            if (position != null && position is Vector3)
                            {
                                this.brain.Navigator.SetDestination((Vector3)position, BaseNavigator.NavigationSpeed.Fast);
                                return StateStatus.Running;
                            }
                        }
                        return StateStatus.Running;
                    }
                    else if (entityAI.currentTarget != null)
                    {
                        if (entityAI == null || entityAI.IsDestroyed || entityAI.currentTarget == null || entityAI.currentTarget.IsDead() || entityAI.currentTarget.IsDestroyed)
                            return StateStatus.Error;


                        if (entityAI.currentTarget == null)
                        {
                            this.brain.Navigator.ClearFacingDirectionOverride();
                            this.StopAttacking();
                            return StateStatus.Finished;
                        }
                        if (this.brain.Senses.ignoreSafeZonePlayers)
                        {
                            BasePlayer basePlayer = entityAI.currentTarget as BasePlayer;
                            if ((UnityEngine.Object)basePlayer != (UnityEngine.Object)null && basePlayer.InSafeZone())
                                return StateStatus.Error;
                        }
                        if (entityAI.InEngagementRange())
                            entityAI.TickAttack(delta, entityAI.currentTarget, true);

                        if (!this.brain.Navigator.SetDestination(entityAI.currentTarget.transform.position, BaseNavigator.NavigationSpeed.Fast))
                        {
                            this.brain.Navigator.SetFacingDirectionOverride((entityAI.currentTarget.transform.position - entityAI.transform.position).normalized);
                            if (entityAI.InEngagementRange())
                                entityAI.TickAttack(delta, entityAI.currentTarget, true);
                            return StateStatus.Error;
                        }

                        this.brain.Navigator.SetFacingDirectionOverride((entityAI.currentTarget.transform.position - entityAI.transform.position).normalized);
                        if (this.attack.CanAttack(entityAI.currentTarget))
                        {
                            this.StartAttacking(entityAI.currentTarget);
                            if (entityAI.InEngagementRange())
                                entityAI.TickAttack(delta, entityAI.currentTarget, true);
                        }
                        else
                            this.StopAttacking();
                    }
                    return StateStatus.Running;
                }

                private Vector3 GetAimDirection(Vector3 from, Vector3 target) => Vector3Ex.Direction2D(target, from);

                private void StartAttacking(BaseEntity entity) => this.attack.StartAttacking(entity);

                public virtual Vector3 GetRandomPositionAround(Vector3 position, float minDistFrom = 0.0f, float maxDistFrom = 2f)
                {
                    if ((double)maxDistFrom < 0.0)
                        maxDistFrom = 0.0f;
                    Vector2 vector2 = UnityEngine.Random.insideUnitCircle * maxDistFrom;
                    float x = Mathf.Clamp(Mathf.Max(Mathf.Abs(vector2.x), minDistFrom), minDistFrom, maxDistFrom) * Mathf.Sign(vector2.x);
                    float z = Mathf.Clamp(Mathf.Max(Mathf.Abs(vector2.y), minDistFrom), minDistFrom, maxDistFrom) * Mathf.Sign(vector2.y);
                    return position + new Vector3(x, TerrainMeta.HeightMap.GetHeight(position), z);
                }
            }

            public class BaseLauncherState : BasicAIState
            {
                private readonly RandomRaider entityAI;
                private float nextStrafeTime;
                private float nextFireTime;
                private Vector3 position;
                private Vector3 position2;
                private bool isFireing;
                private bool isTwig = false;
                private float nextFireGunTime;
                private IAIAttack attack;

                public BaseLauncherState(RandomRaider entityAI) : base(AIState.Attack)
                {
                    this.entityAI = entityAI;
                }

                public AIState GetState() => AIState.Attack;

                public override float GetWeight()
                {
                    if (entityAI != null && !entityAI.isJugernaut && !entityAI.HasTarget() && entityAI.currentTargetOverride != null && entityAI.totalTossedExplosives > 0 || entityAI != null && !entityAI.isJugernaut && !entityAI.HasTarget() && entityAI.currentTargetOverride != null && entityAI.totalShotRockets > 0)
                        return 25f;

                    return 0f;
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    if (debug) _.PrintWarning("Enter raid state");
                    position = entityAI.currentTargetOverride.transform.position;
                    position2 = Vector3.zero;
                    nextFireTime = Time.time + 0.5f;
                    isFireing = false;
                    entityAI.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                    this.attack = entityAI as IAIAttack;

                    BuildingBlock block = entityAI.currentTargetOverride.GetComponent<BuildingBlock>();

                    base.StateEnter(brain, entity);

                    if (entityAI.isTalking < UnityEngine.Time.realtimeSinceStartup)
                    {
                        EventRandomManager.SendEffect(agro, entityAI.transform.position, entityAI);
                        entityAI.isTalking = UnityEngine.Time.realtimeSinceStartup + UnityEngine.Random.Range(5.0f, 10.0f);
                    }
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    this.brain.Navigator.ClearFacingDirectionOverride();
                    position = Vector3.zero;
                    if (entityAI._ProjectilesSlot != null && entityAI._ProjectilesSlot.Count > 0)
                        entityAI.EquipNewWeapon(entityAI._ProjectilesSlot.GetRandom());
                    else if (entityAI._MeleWeaponSlot != null && entityAI._MeleWeaponSlot.Count > 0)
                        entityAI.EquipNewWeapon(entityAI._MeleWeaponSlot.GetRandom());
                    base.StateLeave(brain, entity);
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);

                    if (entityAI == null || entityAI.IsDestroyed || entityAI.transform == null || entityAI.HasTarget() || entityAI.currentTargetOverride == null || position == Vector3.zero)
                        return StateStatus.Error;

                    if (isTwig && nextFireGunTime > Time.time)
                    {
                        entityAI.Brain.Navigator.SetFacingDirectionOverride((position - entityAI.transform.position).normalized);
                        this.StartAttacking(entityAI.currentTargetOverride);
                        entityAI.TickAttack(delta, entityAI.currentTargetOverride, true);
                    }
                    else if (nextStrafeTime < Time.time)
                    {
                        entityAI.Brain.Navigator.SetNavMeshEnabled(true);
                        entityAI.Brain.Navigator.ClearFacingDirectionOverride();
                        if (entityAI != null)
                        {
                            Vector3 position = GetRandomPositionAround(entityAI.HomePosition, 1f, 20f);
                            if (position != null && position is Vector3)
                                entityAI.SetDestination(position);
                        }
                        nextStrafeTime = Time.time + UnityEngine.Random.Range(6f, 10f);

                        if (!isFireing && nextFireTime < Time.time && entityAI.Brain.spawnAge + 10 < Time.time)
                        {
                            if (IsTwigInWay())
                            {
                                nextFireGunTime = Time.time + 5f;
                                isTwig = true;
                                nextFireTime = Time.time + UnityEngine.Random.Range(2f, 5f);

                                if (entityAI._ProjectilesSlot != null && entityAI._ProjectilesSlot.Count > 0)
                                    entityAI.EquipNewWeapon(entityAI._ProjectilesSlot.GetRandom());
                                else if (entityAI._MeleWeaponSlot != null && entityAI._MeleWeaponSlot.Count > 0)
                                    entityAI.EquipNewWeapon(entityAI._MeleWeaponSlot.GetRandom());
                                return StateStatus.Running;
                            }

                            if (Physics.Linecast(entityAI.transform.position, position, obstructionMask))
                            {
                                nextFireTime = Time.time + 0.1f;
                            }
                            else
                            {
                                isFireing = true;
                                nextFireTime = Time.time + UnityEngine.Random.Range(5f, 10f);
                                entityAI.Brain.Navigator.Stop();
                                entityAI.StartCoroutine(WeaponFireTest(position, 6, true));
                                nextStrafeTime = Time.time + 1.2f;
                                isFireing = false;
                            }
                        }
                    }

                    return StateStatus.Running;
                }

                private bool IsTwigInWay()
                {
                    entityAI.Brain.Navigator.SetFacingDirectionOverride((position - entityAI.transform.position).normalized);

                    var ray = new Ray(entityAI.eyes.position, ((position + new Vector3(0, 0.5f, 0)) - entityAI.transform.position).normalized);

                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 80f))
                    {
                        BuildingBlock block = hit.GetEntity()?.GetComponent<BuildingBlock>();
                        if (block != null && block.grade == BuildingGrade.Enum.Twigs)
                            return true;
                    }
                    return false;
                }

                private bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
                {
                    viewAngle = new Quaternion(0f, 0f, 0f, 0f);
                    if (player.serverInput?.current == null) return false;
                    viewAngle = Quaternion.Euler(player.serverInput.current.aimAngles);
                    return true;

                }

                private Vector3 GetAimDirection(Vector3 from, Vector3 target) => Vector3Ex.Direction2D(target, from);

                private void StartAttacking(BaseEntity entity) => this.attack.StartAttacking(entity);
                private void StopAttacking() => this.attack.StopAttacking();

                public IEnumerator WeaponFireTest(Vector3 locations, int slot, bool rocket = true)
                {
                    entityAI.Brain.Navigator.SetFacingDirectionOverride((locations - entityAI.transform.position).normalized);
                    yield return CoroutineEx.waitForSeconds(0.1f);
                    if (entityAI == null || entityAI.IsDestroyed)
                        entityAI?.StopCoroutine(WeaponFireTest(locations, slot, rocket));
                    rocket = entityAI.TargetInThrowableRange();
                    if (entityAI.totalShotRockets > 0 && rocket)
                    {
                        HeldEntity heldEntity = entityAI.inventory.containerBelt.GetSlot(slot)?.GetHeldEntity() as HeldEntity;
                        entityAI.EquipNewWeapon(slot);
                        entityAI.UpdateActiveItem(entityAI.inventory.containerBelt.GetSlot(slot).uid);
                        entityAI.inventory.UpdatedVisibleHolsteredItems();
                        heldEntity.UpdateVisibility_Hand();
                        entityAI.finalDestination = locations;

                        if (heldEntity != null && heldEntity is BaseLauncher && slot == 6)
                        {
                            entityAI.Brain.Navigator.SetFacingDirectionOverride((locations - entityAI.transform.position).normalized);
                            entityAI.SetAimDirection((locations - entityAI.transform.position).normalized);
                            yield return CoroutineEx.waitForSeconds(0.9f);
                            if (entityAI == null || entityAI.IsDestroyed)
                                entityAI?.StopCoroutine(WeaponFireTest(locations, slot, rocket));
                            if (fireRocket((heldEntity as BaseProjectile), locations))
                                entityAI.totalShotRockets--;
                        }
                        if (slot == 6)
                        {
                            yield return CoroutineEx.waitForSeconds(0.5f);
                            if (entityAI == null || entityAI.IsDestroyed)
                                entityAI?.StopCoroutine(WeaponFireTest(locations, slot, rocket));
                            if (entityAI._ProjectilesSlot.Count > 0)
                                entityAI.EquipNewWeapon(entityAI._ProjectilesSlot.GetRandom());
                            else if (entityAI._MeleWeaponSlot.Count > 0)
                                entityAI.EquipNewWeapon(entityAI._MeleWeaponSlot.GetRandom());
                            entityAI.Brain.Navigator.ClearFacingDirectionOverride();
                            entityAI?.StopCoroutine(WeaponFireTest(locations, slot, rocket));
                        }
                    }
                    else if (entityAI.totalTossedExplosives > 0)
                    {
                        Item itemC4 = ItemManager.CreateByName(entityAI.manager.config.explosive.GetRandom(), 1);
                        if (itemC4 != null)
                        {
                            itemC4.MoveToContainer(entityAI.inventory.containerBelt, 5);
                            ThrownWeapon _throwableWeapon = itemC4.GetHeldEntity() as ThrownWeapon;
                            entityAI.UpdateActiveItem(entityAI.inventory.containerBelt.GetSlot(5).uid);
                            if (_throwableWeapon != null)
                            {
                                _throwableWeapon.SetHeld(true);
                                entityAI.inventory.UpdatedVisibleHolsteredItems();
                                entityAI.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                                yield return CoroutineEx.waitForSeconds(0.5f);
                                if (entityAI == null || entityAI.IsDestroyed)
                                {
                                    itemC4?.Remove();
                                    entityAI?.StopCoroutine(WeaponFireTest(locations, slot, rocket));
                                }
                                ServerThrow(locations, _throwableWeapon);
                                entityAI.totalTossedExplosives--;
                                yield return CoroutineEx.waitForSeconds(0.5f);
                            }
                        }
                        if (entityAI == null || entityAI.IsDestroyed)
                            entityAI?.StopCoroutine(WeaponFireTest(locations, slot, rocket));
                        if (entityAI._ProjectilesSlot.Count > 0)
                            entityAI.EquipNewWeapon(entityAI._ProjectilesSlot.GetRandom());
                        else if (entityAI._MeleWeaponSlot.Count > 0)
                            entityAI.EquipNewWeapon(entityAI._MeleWeaponSlot.GetRandom());
                        entityAI.Brain.Navigator.ClearFacingDirectionOverride();
                        entityAI?.StopCoroutine(WeaponFireTest(locations, slot, rocket));
                    }
                }

                private bool fireRocket(BaseProjectile _ProectileWeapon, Vector3 locations)
                {
                    if (entityAI == null || _ProectileWeapon == null || locations == null || _ProectileWeapon.MuzzlePoint?.transform == null)
                        return false;

                    Vector3 vector3 = _ProectileWeapon.MuzzlePoint.transform.forward;
                    Vector3 position = _ProectileWeapon.MuzzlePoint.transform.position + (Vector3.up * 1.6f);
                    BaseEntity rocket = null;
                    rocket = GameManager.server.CreateEntity($"assets/prefabs/ammo/rocket/rocket_basic.prefab", position, entityAI.eyes.GetLookRotation());
                    if (rocket == null) return false;
                    var proj = rocket.GetComponent<ServerProjectile>();
                    if (proj == null) return false;
                    rocket.creatorEntity = entityAI;
                    proj.InitializeVelocity(Quaternion.Euler(vector3) * rocket.transform.forward * 35f);
                    TimedExplosive rocketExplosion = rocket.GetComponent<TimedExplosive>();
                    rocketExplosion.timerAmountMin = _.configData.settings.RocketExplodeTime;
                    rocketExplosion.timerAmountMax = _.configData.settings.RocketExplodeTime;
                    if (entityAI.manager.config.DamageScale > 0)
                        rocket.SendMessage("SetDamageScale", (object)(float)((double)entityAI.manager.config.DamageScale));
                    rocket.Spawn();
                    return true;
                }

                public void ServerThrow(Vector3 targetPosition, ThrownWeapon wep)
                {
                    if (wep == null) return;
                    Vector3 position = entityAI.eyes.position;
                    Vector3 vector3 = entityAI.eyes.BodyForward();
                    float num1 = 1.1f;
                    wep.SignalBroadcast(BaseEntity.Signal.Throw, string.Empty);
                    BaseEntity entity = GameManager.server.CreateEntity(wep.prefabToThrow.resourcePath, position, Quaternion.LookRotation(wep.overrideAngle == Vector3.zero ? -vector3 : wep.overrideAngle));
                    if ((UnityEngine.Object)entity == (UnityEngine.Object)null)
                        return;
                    entity.creatorEntity = (BaseEntity)entityAI;
                    Vector3 aimDir = vector3 + Quaternion.AngleAxis(10f, Vector3.right) * Vector3.up;
                    float f = 6f;
                    if (float.IsNaN(f))
                    {
                        aimDir = vector3 + Quaternion.AngleAxis(20f, Vector3.right) * Vector3.up;
                        f = 6f;
                        if (float.IsNaN(f))
                            f = 5f;
                    }
                    entity.SetVelocity(aimDir * f * num1);
                    if ((double)wep.tumbleVelocity > 0.0)
                        entity.SetAngularVelocity(new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * wep.tumbleVelocity);
                    DudTimedExplosive dud = entity.GetComponent<DudTimedExplosive>();
                    if (dud != null)
                    {
                        dud.dudChance = 0f;
                        dud.becomeDudInWater = false;
                    }

                    entity.Spawn();
                    wep.StartAttackCooldown(wep.repeatDelay);
                    UseItemAmount(wep, 1);
                }

                private bool UseItemAmount(ThrownWeapon wep, int iAmount)
                {
                    if (iAmount <= 0)
                        return true;
                    Item ownerItem = wep.GetItem();
                    if (ownerItem == null)
                    {
                        wep.DestroyThis();
                        return true;
                    }
                    ownerItem.amount -= iAmount;
                    ownerItem.MarkDirty();
                    if (ownerItem.amount > 0)
                        return false;
                    wep.DestroyThis();
                    return true;
                }

                public bool ReachedPosition(Vector3 position) => Vector2.Distance(position, this.entityAI.transform.position) <= 0.5f;

                public virtual Vector3 GetRandomPositionAround(Vector3 position, float minDistFrom = 0.0f, float maxDistFrom = 2f)
                {
                    if ((double)maxDistFrom < 0.0)
                        maxDistFrom = 0.0f;
                    Vector2 vector2 = UnityEngine.Random.insideUnitCircle * maxDistFrom;
                    float x = Mathf.Clamp(Mathf.Max(Mathf.Abs(vector2.x), minDistFrom), minDistFrom, maxDistFrom) * Mathf.Sign(vector2.x);
                    float z = Mathf.Clamp(Mathf.Max(Mathf.Abs(vector2.y), minDistFrom), minDistFrom, maxDistFrom) * Mathf.Sign(vector2.y);
                    return position + new Vector3(x, 0.0f, z);
                }
            }
        }
        #endregion

        #region Hooks
        private void CanWaterBallSplash(ItemDefinition liquidDef, Vector3 position, float radius, int amount)
        {
            List<DudTimedExplosive> list1 = Facepunch.Pool.Get<List<DudTimedExplosive>>();
            Vis.Entities<DudTimedExplosive>(position, radius, list1, 1220225811);

            foreach (DudTimedExplosive baseEntity in list1)
            {
                if (baseEntity != null && !baseEntity.IsDestroyed && baseEntity.creatorEntity != null && baseEntity.creatorEntity is RandomRaider)
                {
                    baseEntity.Explode();
                }
            }
        }

        private object CanLootEntity(BasePlayer player, SupplyDrop container)
        {
            if (container.net != null && supplyDrops.ContainsKey(container.net.ID.Value))
            {
                if (!supplyDrops[container.net.ID.Value].Contains(player.userID.Get()))
                {
                    GameTipMessage(player, lang.GetMessage("LockedLoot", this, player.UserIDString));
                    return true;
                }
                else
                {
                    return null;
                }
            }
            return null;
        }

        object OnEntityKill(SupplyDrop container)
        {
            if (container.net != null && supplyDrops.ContainsKey(container.net.ID.Value))
            {
                supplyDrops.Remove(container.net.ID.Value);
            }
            return null;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            if (item == null || item.name == null || !ItemConfig.Contains(item.name)) return null;
            if (ItemConfig.Contains(item.name))
            {
                Item x = ItemManager.CreateByItemID(item.info.itemid, amount, item.skin);
                x.name = item.name;
                x.skin = item.skin;
                x.amount = amount;

                item.amount -= amount;
                return x;
            }

            return null;
        }

        public static string FormatTime(TimeSpan ts)
        {
            return string.Format("{0}", ts.Minutes);
        }

        public static string FormatTimeS(TimeSpan ts)
        {
            return string.Format("{0}", ts.Seconds);
        }

        void OnLootSpawn(LootContainer container)
        {
            if (itemLootSpawn.Count <= 0 || container.ShortPrefabName == "stocking_large_deployed" ||
                container.ShortPrefabName == "stocking_small_deployed") return;
            foreach (var ItemsConfig in itemLootSpawn)
            {
                if (!configData.itemProfile.ContainsKey(ItemsConfig))
                    continue;

                bool ItemAdded = false;
                foreach (var LootContainers in configData.itemProfile[ItemsConfig].LootContainers)
                {
                    if (LootContainers == null || configData.itemProfile[ItemsConfig].SpawnChance <= 0)
                        continue;

                    if (LootContainers.Contains(container.ShortPrefabName))
                    {
                        if (UnityEngine.Random.Range(0, 100) < configData.itemProfile[ItemsConfig].SpawnChance)
                        {
                            NextTick(() =>
                            {
                                if (container != null)
                                {
                                    if (container.inventory.itemList.Count == container.inventory.capacity)
                                    {
                                        container.inventory.capacity++;
                                    }
                                    string name = configData.itemProfile[ItemsConfig].itemName;
                                    var FlareItem = ItemManager.CreateByItemID(304481038, 1, configData.itemProfile[ItemsConfig].itemSkin);
                                    FlareItem.MoveToContainer(container.inventory);
                                    FlareItem.name = name;
                                    if (debug) PrintWarning($"{name} Spawned in container {LootContainers} At: {container.transform.position}");
                                }
                            });
                            ItemAdded = true;
                            break;
                        }
                    }
                }
                if (ItemAdded)
                    break;
            }
        }

        void startPlayerSearch(BasePlayer player)
        {
            if (QueuedRoutinePlayer != null)
                ServerMgr.Instance.StopCoroutine(QueuedRoutinePlayer);
            QueuedRoutinePlayer = null;
            QueuedRoutinePlayer = ServerMgr.Instance.StartCoroutine(FindRandomBaseToRaid(configData.settings.mustBeOnlinePlayer, false, player));
        }

        void startRestartSearch()
        {
            if (QueuedRoutine != null)
                ServerMgr.Instance.StopCoroutine(QueuedRoutine);
            QueuedRoutine = null;
            QueuedRoutine = ServerMgr.Instance.StartCoroutine(FindRandomBaseToRaid(configData.settings.mustBeOnlinePlayer, true));
        }

        void stopSearch()
        {
            if (QueuedRoutine != null)
                ServerMgr.Instance.StopCoroutine(QueuedRoutine);
            if (QueuedRoutinePlayer != null)
                ServerMgr.Instance.StopCoroutine(QueuedRoutinePlayer);
        }

        [ChatCommand("surrender")]
        private void surrender(BasePlayer player, string command, string[] args)
        {
            EventRandomManager nearestEvent = null;
            float helperDistance = 9999;

            foreach (EventRandomManager eventRunning in EventRandomManager._AllRaids)
            {
                float distance = Vector3.Distance(eventRunning.location, player.transform.position);
                if (distance < helperDistance && eventRunning._authedPlayers.Contains(player.userID.Get()))
                    nearestEvent = eventRunning;
            }

            if (nearestEvent != null)
            {
                string itemName = nearestEvent.surrenderItem;
                int totalCosts = nearestEvent.surrenderCost;
                var itemDef = ItemManager.FindItemDefinition(itemName);

                if (itemDef == null)
                {
                    if (itemName.ToLower() == "serverrewards")
                    {
                        var totalHave = (int)ServerRewards?.Call("CheckPoints", player.userID.Get());
                        if (totalHave != null && totalHave < totalCosts)
                        {
                            SendReply(player, lang.GetMessage("negotiateMessageNew", this, player.UserIDString), totalCosts, nearestEvent.surrenderName);
                            return;
                        }
                        ServerRewards?.Call("TakePoints", player.userID.Get(), totalCosts);
                        nearestEvent.Destroy();
                        SendReply(player, lang.GetMessage("payedAndSurrenderedNew", this, player.UserIDString), player.displayName, totalCosts, nearestEvent.surrenderName);
                        return;
                    }
                    else if (itemName.ToLower() == "economics")
                    {
                        var totalHave = (double)Economics?.Call("Balance", player.userID.Get());
                        if (totalHave != null && totalHave < Convert.ToDouble(totalCosts))
                        {
                            SendReply(player, lang.GetMessage("negotiateMessageNew", this, player.UserIDString), totalCosts, nearestEvent.surrenderName);
                            return;
                        }
                        Economics?.Call("Withdraw", player.userID.Get(), Convert.ToDouble(totalCosts));
                        nearestEvent.Destroy();
                        SendReply(player, lang.GetMessage("payedAndSurrenderedNew", this, player.UserIDString), player.displayName, totalCosts, nearestEvent.surrenderName);
                        return;
                    }
                }
                else
                {
                    if (itemDef != null)
                    {
                        if (player.inventory.GetAmount(itemDef.itemid) >= totalCosts)
                        {
                            player.inventory.Take(null, itemDef.itemid, totalCosts);
                            nearestEvent.Destroy();
                        }
                        else
                        {
                            SendReply(player, lang.GetMessage("negotiateMessageNew", this, player.UserIDString), totalCosts, nearestEvent.surrenderName);
                        }
                    }
                }
            }
        }

        [ChatCommand("randomraid")]
        private void randomRaidChat(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, adminPerm))
            {
                SendReply(player, $"You are missing {adminPerm}");
                return;
            }

            BuildingPrivlidge priv = null;

            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "here":
                        if (args.Length <= 1)
                        {
                            SendReply(player, lang.GetMessage("usageHere", this, player.UserIDString));
                            return;
                        }

                        priv = player.GetBuildingPrivilege();
                        if (priv == null)
                        {
                            SendReply(player, "There needs to be a tc here to start or your not in tc range!");
                        }
                        else CallRaidHere(priv, args[1], player);
                        return;

                    case "random":
                        SendReply(player, "Looking for random base to raid!");
                        startPlayerSearch(player);
                        return;

                    case "reload":
                        SendReply(player, "Stoping all raids!");
                        StopAllRaids();
                        return;

                        case "terrain":
                        string names = "Found Coliders In the Scan Up 200 down 200 Are:\n";
                        Vector3 p1 = player.transform.position + new Vector3(0, 200f, 0);
                        Vector3 p2 = player.transform.position + new Vector3(0, -200f, 0);
                        Vector3 diff = p2 - p1;
                        RaycastHit[] hits;
                        hits = Physics.RaycastAll(p1, diff, diff.magnitude);
                        if (hits.Length > 0)
                        {
                            for (int i = 0; i < hits.Length; i++)
                            {
                                RaycastHit hit = hits[i];
                                if (hit.collider != null)
                                    names += $"{hit.collider.name},\n";
                            }
                            SendReply(player, $"{names}");
                        }
                        else
                        {
                            names += $"No Detected Coliders,\n";
                            SendReply(player, $"{names}");
                        }
                        return;

                    case "item":
                        if (args.Length <= 1)
                        {
                            SendReply(player, lang.GetMessage("usageItem", this, player.UserIDString));
                            return;
                        }

                        string[] theItemConfig = args.Skip(1).ToArray();
                        GetTheMRaidItem(player, "", theItemConfig);
                        return;
                }
            }
            SendReply(player, $"<color=red>Admin Command Usage</color>:\n\n" +
                              $"<color=orange>/randomraid here <type></color> - Will start a random raid at your location.\n\n" +
                              $"<color=orange>/randomraid random</color> - Will start a searching for a random raid location.\n\n" +
                              $"<color=orange>/randomraid item <type></color> - Will give you raid item.\n\n" +
                              $"<color=orange>/randomraid reload</color> - Will cancel all raid events.");
        }

        void OnLootEntityEnd(BasePlayer player, NPCPlayerCorpse npcPlayerCorpse)
        {
            NextTick(() =>
            {
                if (npcPlayerCorpse != null && npcPlayerCorpse.name == "RandomRaider")
                {
                    foreach (ItemContainer container in npcPlayerCorpse.containers)
                    {
                        if (!container.IsEmpty())
                            continue;
                        npcPlayerCorpse?.ResetRemovalTime(3);
                    }
                }
            });
        }

        public static void dropWin(Vector3 location, int amount, List<string> custloot, List<ulong> playerID, bool alphaloot = false)
        {
            for (int i = 0; i < amount; i++)
            {
                SupplyDrop entity = GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", location + new Vector3(UnityEngine.Random.Range(-15.0f, 15.0f), 200, UnityEngine.Random.Range(-15.0f, 15.0f))) as SupplyDrop;
                if (entity != null)
                {
                    entity.globalBroadcast = true;
                    entity.Spawn();
                    if (_.configData.settings.lockAirdrop)
                        supplyDrops.Add(entity.net.ID.Value, new List<ulong>(playerID));
                    entity.inventory.capacity = 36;
                    entity.onlyAcceptCategory = ItemCategory.All;
                    entity.SendNetworkUpdateImmediate();
                    Interface.Oxide.CallHook("OnRandomRaidWin", entity, playerID);
                    _.timer.Once(2f, () =>
                    {
                        if (!alphaloot && entity != null && custloot != null && custloot.Count > 0)
                        {
                            var lootcall = _.CustomLoot?.Call("MakeLoot", custloot.GetRandom());

                            if (lootcall is string)
                                _.PrintWarning((string)lootcall);
                            else if (lootcall != null && lootcall is List<Item> && (lootcall as List<Item>).Count > 0)
                            {
                                entity.inventory.Clear();
                                foreach (var item in lootcall as List<Item>)
                                {
                                    if (!item.MoveToContainer(entity.inventory, -1, true))
                                        item.Remove();
                                }
                            }
                        }
                        else if (alphaloot && entity != null && custloot != null && custloot.Count > 0)
                        {
                            List<Item> itemList = Pool.Get<List<Item>>();
                            itemList.AddRange(entity.inventory.itemList);
                            foreach (var items in itemList)
                                items.Remove(0f);
                            _.AlphaLoot?.Call("PopulateLoot", entity.inventory, custloot.GetRandom());
                            Pool.FreeUnmanaged(ref itemList);
                        }
                    });
                }
            }
        }

        private void OnRaidableBaseCompleted(Vector3 raidPos, int mode, bool allowPVP, string id, float spawnTime, float despawnTime, float loadTime, ulong ownerId, BasePlayer owner, List<BasePlayer> raiders, List<BasePlayer> intruders, List<BaseEntity> entities)
        {
            List<ulong> tcDataTemp = Pool.Get<List<ulong>>();
            List<ProtoBuf.PlayerNameID> authedTemp = Pool.Get<List<ProtoBuf.PlayerNameID>>();

            if (mode == 512)
                mode = 0;

            timer.Once(10f, () =>
            {
                if (!configData.settingsRB.raidableBaseComplete || raiders == null)
                {
                    if (debug) PrintWarning($"raidableBaseComplete status: {configData.settingsRB.raidableBaseComplete}");
                    return;
                }

                if (debug)
                {
                    if (owner != null)
                        PrintWarning($"Making the call Raidable Base Check Call 1234 Mode: {mode} Raiders Count: {raiders.Count} owner: {owner.displayName}");
                }

                if (raiders.Count > 0 && configData.settingsRB.raidSettings.ContainsKey(mode))
                {
                    foreach (var pInfo in raiders)
                    {
                        if (pInfo != null && !logDataRB.pLogsRB.ContainsKey(pInfo.userID.Get()))
                            logDataRB.pLogsRB.Add(pInfo.userID.Get(), new lDataRB());

                        if (!logDataRB.pLogsRB[pInfo.userID.Get()].mode.ContainsKey(mode))
                            logDataRB.pLogsRB[pInfo.userID.Get()].mode.Add(mode, 1);
                        else logDataRB.pLogsRB[pInfo.userID.Get()].mode[mode]++;
                    }

                    SaveRBLogData();

                    if (debug) PrintWarning($"Running scan on raiders to find a base");
                    RBConfig config = configData.settingsRB.raidSettings[mode];
                    if (config == null) return;

                    int rando = UnityEngine.Random.Range(0, 100);
                    if (debug) PrintWarning($"Rolling chance {rando}");

                    if (rando <= config.raidChance && pcdData.tcData.Count > 0)
                    {
                        if (debug) PrintWarning($"Chance Hit Scanning Raiders");
                        string type = "";
                        BasePlayer okPlayer = null;
                        BuildingPrivlidge priv = null;
                        Vector3 location = Vector3.zero;
                        List<ulong> authPlayers = new List<ulong>();

                        List<BuildingBlock> block = new List<BuildingBlock>();
                        foreach (var p in raiders)
                        {
                            tcDataTemp.Clear();
                            authedTemp.Clear();

                            if (priv != null)
                                break;

                            if (p == null)
                            {
                                if (debug) PrintWarning($"Could not find raid player he is null");
                                continue;
                            }

                            if (debug) PrintWarning($"Testing Players Bases in the raid Player: {p.displayName}");

                            if (configData.settings.mustBeOnlinePlayer && !p.IsConnected)
                            {
                                if (debug) PrintWarning($"Player: {p.displayName} is not online");
                                continue;
                            }

                            if (configData.settings.musthavePerm && !permission.UserHasPermission(p.UserIDString, usePerm))
                            {
                                if (debug) PrintWarning($"Testing Players Permisions in the raid failed Player: {p.displayName} does not have use permision.");
                                continue;
                            }

                            if (debug) PrintWarning($"Checking if Player: {p.displayName} Has TC Data");

                            if (pcdData.tcData.ContainsKey(p.userID.Get()))
                            {
                                if (debug) PrintWarning($"Player: {p.displayName} Does Have Tc Data");

                                int totalRaids = logDataRB.pLogsRB[p.userID.Get()].mode[mode];

                                if (totalRaids == 0 || totalRaids < config.totalRaids)
                                {
                                    if (debug) PrintWarning($"Made it to this Section TotalRaids and failed not enuf total to be raided for this player {p.displayName}");
                                    continue;
                                }
                                else
                                {
                                    if (debug) PrintWarning($"Player: {p.displayName} Has A Total Of: {totalRaids} Completed Raids");
                                }

                                if (debug) PrintWarning($"Testing Total Bases Raids for mode Player: {p.displayName} Total: {totalRaids}");

                                tcDataTemp.AddRange(pcdData.tcData[p.userID.Get()]);

                                foreach (ulong key in tcDataTemp)
                                {
                                    if (debug) PrintWarning($"Finding Players Base");

                                    BuildingPrivlidge privFind = FindEntity(key) as BuildingPrivlidge;
                                    if (privFind == null)
                                    {
                                        if (debug) PrintWarning($"Cound Not Find TC NedId; {key}");
                                        pcdData.tcData[p.userID.Get()].Remove(key);
                                        SaveData();
                                    }
                                    else if (priv == null)
                                    {
                                        if (!CanBeRaided(privFind))
                                        {
                                            if (debug) PrintWarning($"Player can not be raided currenty on cooldown");
                                            continue;
                                        }
                                        if (isBlockedTerain(privFind.transform.position))
                                        {
                                            if (debug) PrintWarning($"Player base is on blocked terrain.");
                                            continue;
                                        }

                                        BuildingManager.Building build = privFind.GetBuilding();
                                        if (build == null || !build.HasBuildingBlocks() || build.buildingBlocks.Count < 5)
                                        {
                                            if (debug) PrintWarning($"Player base is to small to be raided!");
                                            continue;
                                        }
                                        else
                                        {
                                            if (debug) PrintWarning($"Players Base Found");

                                            block = GenerateTargets(privFind);
                                            if (block.Count < 5)
                                            {
                                                if (debug) PrintWarning($"Player base is to small to be raided!");
                                                continue;
                                            }
                                            priv = privFind;
                                            okPlayer = p;
                                            location = priv.transform.position;
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (debug) PrintWarning($"Player: {p.displayName} DOES NOT HAVE TC DATA");
                            }

                            if (priv != null)
                            {
                                type = config.sendRaids.GetRandom();
                            }

                            if (!string.IsNullOrEmpty(type))
                            {
                                if (configData.settingsRB.warnPlayer)
                                {
                                    authedTemp.AddRange(priv.authorizedPlayers);

                                    foreach (var user in authedTemp)
                                    {
                                        if (!authPlayers.Contains(user.userid))
                                            authPlayers.Add(user.userid);
                                        BasePlayer TCplayer = BasePlayer.FindByID(user.userid);
                                        if (TCplayer != null && TCplayer.IsConnected)
                                        {
                                            TimeSpan newTime = TimeSpan.FromSeconds(config.raidableBaseCompleteDelay);
                                            SendReply(TCplayer, lang.GetMessage("warningRevengeGrid", this, TCplayer.UserIDString), GetGrid(raidPos), GetGrid(priv.transform.position), FormatTime(newTime), FormatTimeS(newTime));
                                            if (configData.settings.gameTip)
                                                GameTipMessage(TCplayer, string.Format(lang.GetMessage("warningRevengeGrid", this, TCplayer.UserIDString), GetGrid(raidPos), GetGrid(priv.transform.position), FormatTime(newTime), FormatTimeS(newTime)));
                                            if (configData.settings.worldMarker)
                                                LocateBaseWithMarker(TCplayer, priv.transform.position, configData.settings.worldMarkerTime);

                                            if (!raidData.data.ContainsKey(user.userid))
                                                raidData.data.Add(user.userid, new rData() { LastRaidedRB = DateTime.Now.AddMinutes(config.cooldownRB) });
                                            else raidData.data[user.userid].LastRaidedRB = DateTime.Now.AddMinutes(config.cooldownRB);
                                        }
                                    }
                                }
                                timer.Once(config.raidableBaseCompleteDelay, () =>
                                {
                                    startRandomRaid(location, authPlayers, type, block);
                                    LogToFile("RaidableBasesRaids", $"[{DateTime.Now}] Started raid at {priv.transform.position}, For player {p.displayName}", this);
                                    if (debug) PrintWarning($"Random Raid timer for RaidableBase started for location {location}");
                                    if (configData.settings.DiscordWebHook)
                                        ServerMgr.Instance.StartCoroutine(SendToDiscord(ConVar.Server.hostname, okPlayer.displayName, location.ToString(), $"Radable Base Complete {type}"));
                                });

                                if (config.resetKillCount)
                                {
                                    foreach (var pInfo2 in raiders)
                                    {
                                        if (pInfo2 != null && !logDataRB.pLogsRB.ContainsKey(pInfo2.userID.Get()))
                                            logDataRB.pLogsRB.Add(pInfo2.userID.Get(), new lDataRB());

                                        logDataRB.pLogsRB[pInfo2.userID.Get()].mode[mode] = 0;
                                    }
                                    SaveRBLogData();
                                }
                                break;
                            }
                        }
                    }
                }
                Pool.FreeUnmanaged(ref tcDataTemp);
                Pool.FreeUnmanaged(ref authedTemp);
                if (debug) PrintWarning($"Finished the raid Scan!");

            });
        }

        private string GetGrid(Vector3 position)
        {
            string[] chars = new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AI", "AJ", "AK", "AL", "AM", "AN", "AO", "AP", "AQ", "AR", "AS", "AT", "AU", "AV", "AW", "AX", "AY", "AZ" };

            const float block = 146;

            float size = ConVar.Server.worldsize;
            float offset = size / 2;

            float xpos = position.x + offset;
            float zpos = position.z + offset;

            int maxgrid = (int)(size / block);

            float xcoord = Mathf.Clamp(xpos / block, 0, maxgrid - 1);
            float zcoord = Mathf.Clamp(maxgrid - (zpos / block), 0, maxgrid - 1);

            string pos = string.Concat(chars[(int)xcoord], (int)zcoord);

            return (pos);
        }

        private string GetDirectionAngle(float angle, string UserIDString)
        {
            if (angle > 337.5 || angle < 22.5)
                return lang.GetMessage("msgNorth", this, UserIDString);
            else if (angle > 22.5 && angle < 67.5)
                return lang.GetMessage("msgNorthEast", this, UserIDString);
            else if (angle > 67.5 && angle < 112.5)
                return lang.GetMessage("msgEast", this, UserIDString);
            else if (angle > 112.5 && angle < 157.5)
                return lang.GetMessage("msgSouthEast", this, UserIDString);
            else if (angle > 157.5 && angle < 202.5)
                return lang.GetMessage("msgSouth", this, UserIDString);
            else if (angle > 202.5 && angle < 247.5)
                return lang.GetMessage("msgSouthWest", this, UserIDString);
            else if (angle > 247.5 && angle < 292.5)
                return lang.GetMessage("msgWest", this, UserIDString);
            else if (angle > 292.5 && angle < 337.5)
                return lang.GetMessage("msgNorthWest", this, UserIDString);
            return "";
        }

        private void notifyChat(Vector3 vector)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendReply(player, lang.GetMessage("gridMessageDirection", this, player.UserIDString), GetGrid(vector), GetDirectionAngle(Quaternion.LookRotation((vector - player.eyes.position).normalized).eulerAngles.y, player.UserIDString), (int)Math.Round(Vector3.Distance(player.transform.position, vector)));
            }
        }

        public IEnumerator FindRandomBaseToRaid(bool offline = false, bool useChance = true, BasePlayer player = null)
        {
            if (pcdData.tcData.Count <= 0)
                yield return CoroutineEx.waitForSeconds(configData.settings.cooldownSeconds);

            if (useChance)
            {
                int rando = UnityEngine.Random.Range(0, 100);
                if (rando > configData.settings.chanceTime)
                {
                    stopSearch();
                    yield break;
                }
            }

            BuildingPrivlidge priv = null;
            string type = "";
            List<BuildingBlock> block = new List<BuildingBlock>();
            List<ulong> authPlayers = new List<ulong>();

            if (player == null)
                PrintWarning("Scanning to see if there is a random raid ready.");

            foreach (var p in !offline ? BasePlayer.allPlayerList : BasePlayer.activePlayerList)
            {
                if (configData.settings.musthavePerm && !permission.UserHasPermission(p.UserIDString, usePerm))
                    continue;

                if (pcdData.tcData.ContainsKey(p.userID.Get()))
                {
                    foreach (ulong key in pcdData.tcData[p.userID.Get()].ToList())
                    {
                        type = "";
                        block = new List<BuildingBlock>();
                        authPlayers = new List<ulong>();

                        BuildingPrivlidge privFind = FindEntity(key) as BuildingPrivlidge;

                        if (privFind == null)
                        {
                            pcdData.tcData[p.userID.Get()].Remove(key);
                            SaveData();
                        }
                        else if (priv == null)
                        {
                            if (isBlockedTerain(privFind.transform.position))
                                continue;

                            if (!CanBeRaided(privFind))
                                continue;

                            BuildingManager.Building build = privFind.GetBuilding();
                            if (build == null || !build.HasBuildingBlocks() || build.buildingBlocks.Count < 5)
                                continue;

                            block = GenerateTargets(privFind);

                            if (block.Count <= 6)
                                continue;

                            int total = privFind.authorizedPlayers.Count;

                            List<string> availableTypes = new List<string>();

                            foreach (var keyNext in configData.raidSettings.raidTypes)
                            {
                                if (total >= keyNext.Value.TotalAuth && block.Count >= keyNext.Value.blockMIN && !availableTypes.Contains(keyNext.Key))
                                    availableTypes.Add(keyNext.Key);
                            }

                            if (availableTypes.Count > 0)
                            {
                                type = availableTypes.GetRandom();

                                foreach (var user in privFind.authorizedPlayers)
                                {
                                    if (!raidData.data.ContainsKey(user.userid))
                                        raidData.data.Add(user.userid, new rData() { LastRaided = DateTime.Now });
                                    else raidData.data[user.userid].LastRaided = DateTime.Now;

                                    if (!authPlayers.Contains(user.userid))
                                        authPlayers.Add(user.userid);
                                }

                                priv = privFind;

                                if (!raidLocations.ContainsKey(privFind.transform.position))
                                    raidLocations.Add(privFind.transform.position, DateTime.Now.AddMinutes(configData.settings.raidAgainCooldown));
                                else raidLocations[privFind.transform.position] = DateTime.Now.AddMinutes(configData.settings.raidAgainCooldown);

                                SaveRaidData();
                                startRandomRaid(privFind.transform.position, authPlayers, type, block);
                                string combinedString = string.Join(",", authPlayers);
                                LogToFile("Random", $"[{DateTime.Now}] Started random raid at {priv.transform.position} for players {combinedString}", this);

                                if (configData.settings.DiscordWebHook)
                                    ServerMgr.Instance.StartCoroutine(SendToDiscord(ConVar.Server.hostname, p.displayName, priv.transform.position.ToString(), $"Random Raid {type}"));

                                foreach (var target in authPlayers)
                                {
                                    BasePlayer p2 = BasePlayer.FindByID(target);

                                    if (p2 != null && p2.IsConnected)
                                    {
                                        if (configData.raidSettings.raidTypes[type].notifyOwners)
                                        {
                                            if (configData.settings.gameTip)
                                            {
                                                GameTipMessage(p2, string.Format(lang.GetMessage("IncomingRandom", this, p2.UserIDString), GetGrid(priv.transform.position)));
                                            }
                                            else
                                            {
                                                SendReply(p2, lang.GetMessage("IncomingRandom", this, p2.UserIDString), GetGrid(priv.transform.position));
                                            }
                                        }
                                    }
                                }

                                if (player != null)
                                    SendReply(player, $"Random raid found in grid: {GetGrid(priv.transform.position)}");
                                break;
                            }

                        }
                        yield return CoroutineEx.waitForSeconds(2f);
                    }
                }
                yield return CoroutineEx.waitForSeconds(2);
            }


            if (priv == null)
            {
                if (player != null)
                    SendReply(player, "No good random raid locations at this time.");
                PrintWarning("No good random raid locations at this time.");
                LogToFile("Random", $"[{DateTime.Now}] No good random raid locations at this time.", this);
            }

            stopSearch();
        }

        private bool CanBeRaided(BuildingPrivlidge priv)
        {
            if (EventRandomManager._AllRaids.Count >= configData.settings.totalRaids)
            {
                return false;
            }

            object hookCall = Interface.CallHook("BaseTCCanBeRaided", priv);

            if (hookCall is bool)
                return (bool)hookCall;

            if (priv != null)
            {
                Vector3 privLoc = priv.transform.position;

                if (Convert.ToBoolean(RaidableBases?.CallHook("EventTerritory", privLoc)))
                {
                    return false;
                }

                foreach (var user in priv.authorizedPlayers)
                {
                    if (raidData.data.ContainsKey(user.userid))
                    {
                        if (raidData.data[user.userid].LastRaided.AddMinutes(configData.settings.raidAgainCooldown) > DateTime.Now)
                        {
                            return false;
                        }

                        if (raidData.data[user.userid].LastRaidedNpcKills > DateTime.Now)
                        {
                            return false;
                        }

                        if (raidData.data[user.userid].LastRaidedRB > DateTime.Now)
                        {
                            return false;
                        }
                    }
                }

                foreach (var eventRunning in EventRandomManager._AllRaids)
                {
                    float distance = Vector3.Distance(eventRunning.location, priv.transform.position);
                    if (distance < 150f)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool CanBeRaidedFlair(BasePlayer player, BuildingPrivlidge priv)
        {
            if (priv != null)
            {
                foreach (var eventRunning in EventRandomManager._AllRaids)
                {
                    float distance = Vector3.Distance(eventRunning.location, priv.transform.position);
                    if (distance < 150f)
                    {
                        SendReply(player, lang.GetMessage("BaseToCose", this, player.UserIDString));
                        return false;
                    }
                    if (eventRunning._authedPlayers.Contains(player.userID))
                    {
                        SendReply(player, lang.GetMessage("inEvent", this, player.UserIDString));
                        return false;
                    }
                }
            }
            return true;
        }

        private bool isBlockedTerain(Vector3 position)
        {
            Vector3 p1 = position + new Vector3(0, 200f, 0);
            Vector3 p2 = position + new Vector3(0, -200f, 0);
            Vector3 diff = p2 - p1;
            RaycastHit[] hits;
            hits = Physics.RaycastAll(p1, diff, diff.magnitude, terrainLayer);
            if (hits.Length > 0)
            {
                for (int j = 0; j < hits.Length; j++)
                {
                    RaycastHit hit = hits[j];
                    if (hit.collider != null)
                    {
                        for (int i = configData.blockedColliders.Blocked.Count - 1; i >= 0; i--)
                        {
                            string g = configData.blockedColliders.Blocked[i];
                            if (hit.collider != null && hit.collider.name.Contains(g))
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        public static void startRandomRaid(Vector3 loc, List<ulong> authed, string type, List<BuildingBlock> blocks, float delayTIme = 0.1f)
        {
            if (_.configData.raidSettings.raidTypes.ContainsKey(type))
            {
                EventRandomManager managers = EventRandomManager.Create(loc, _.configData.raidSettings.raidTypes[type], type, authed, null, blocks, delayTIme);
                managers.StartEvent();
            }
        }

        private static List<BuildingBlock> GenerateTargets(BuildingPrivlidge priv)
        {
            BuildingManager.Building build = priv?.GetBuilding();
            if (build == null || !build.HasBuildingBlocks())
                return new List<BuildingBlock>();
            else
            {
                List<BuildingBlock> _AllFoundations = new List<BuildingBlock>(build.buildingBlocks);
                foreach (BuildingBlock found in build.buildingBlocks)
                {
                    if (found != null && !found.blockDefinition.canRotateAfterPlacement && _AllFoundations.Contains(found))
                        _AllFoundations.Remove(found);
                }
                return _AllFoundations;
            }
            return new List<BuildingBlock>();
        }

        private object OnEntityTakeDamage(BasePlayer player, HitInfo hitinfo)
        {
            if (EventRandomManager._AllRaids.Count <= 0) return null;
            if (player != null && hitinfo != null && hitinfo.Initiator is RandomRaider && (hitinfo.Initiator as RandomRaider).manager != null)
            {
                if ((hitinfo.Initiator as RandomRaider).playerDamage != 1.0f)
                    hitinfo?.damageTypes?.ScaleAll((hitinfo.Initiator as RandomRaider).playerDamage);
            }
            return null;
        }

        private object OnEntityTakeDamage(RandomRaider entity, HitInfo hitinfo)
        {
            if (EventRandomManager._AllRaids.Count <= 0) return null;
            if (entity != null)
            {
                if (hitinfo.Initiator != null && hitinfo.Initiator is AutoTurret)
                {
                    if (entity != null && entity.manager != null)
                    {
                        float damageTotal = entity.manager.config.AutoTurretDamage;
                        if (damageTotal != 0)
                            hitinfo.damageTypes.ScaleAll(damageTotal);
                        return null;
                    }
                }
                else if (hitinfo != null && hitinfo.WeaponPrefab != null && hitinfo.Initiator != null && hitinfo.Initiator is RandomRaider)
                {
                    if (hitinfo.WeaponPrefab.ShortPrefabName.Contains("rocket") || hitinfo.WeaponPrefab.ShortPrefabName.Contains("timed") || hitinfo.WeaponPrefab.ShortPrefabName.Contains("explosive"))
                        return true;
                }
                else if (hitinfo != null && hitinfo.WeaponPrefab != null && hitinfo.WeaponPrefab.ShortPrefabName.Contains("rocket_heli"))
                    return true;
                else if (hitinfo != null && hitinfo.WeaponPrefab != null && hitinfo.WeaponPrefab.name.Contains("rocket_mlrs"))
                    return true;
            }
            return null;
        }

        private void OnEntityTakeDamage(BuildingBlock block, HitInfo hitinfo)
        {
            if (EventRandomManager._AllRaids.Count <= 0) return;
            if (block != null && hitinfo.Initiator is RandomRaider && block.grade == BuildingGrade.Enum.Twigs)
                hitinfo.damageTypes.ScaleAll(500);
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (EventRandomManager._AllRaids.Count <= 0) return null;

            if (hitinfo != null && hitinfo.WeaponPrefab != null && hitinfo.WeaponPrefab.name.Contains("rocket_mlrs"))
            {
                if (hitinfo.Weapon == null)
                {
                    if (entity.GetComponent<RandomRaider>() != null)
                        return false;
                    if (entity is BuildingBlock)
                        return true;
                }
            }
            else if (entity != null && entity is RandomRaider)
            {
                if (hitinfo != null && hitinfo.Initiator is RandomRaider)
                {
                    return false;
                }
                else if (entity != null && hitinfo.Initiator != null && hitinfo.Initiator is AutoTurret)
                {
                    RandomRaider raider = entity.GetComponent<RandomRaider>();
                    if (raider != null && raider.manager != null)
                    {
                        float damageTotal = raider.manager.config.AutoTurretDamage;
                        if (damageTotal != 0)
                        {
                            hitinfo.damageTypes.ScaleAll(damageTotal);
                            return true;
                        }
                    }
                }
                if (hitinfo != null && hitinfo.Initiator is BasePlayer)
                    return true;
            }
            else if (entity != null && hitinfo != null && hitinfo.Initiator != null && hitinfo.Initiator is RandomRaider)
            {
                if (entity is BuildingPrivlidge || entity is BuildingBlock || entity.name.Contains("wall.external") || entity.name.Contains("gates.external"))
                {
                    BuildingBlock block = entity.GetComponent<BuildingBlock>();
                    if (block != null && block.grade == BuildingGrade.Enum.Twigs)
                        hitinfo.damageTypes.ScaleAll(500);

                    return true;
                }
                else if (entity is BasePlayer && hitinfo != null && hitinfo.Initiator is RandomRaider && (hitinfo.Initiator as RandomRaider).manager != null)
                {
                    if ((hitinfo.Initiator as RandomRaider).playerDamage != 1.0f)
                        hitinfo?.damageTypes?.ScaleAll((hitinfo.Initiator as RandomRaider).playerDamage);
                    return true;
                }
            }
            return null;
        }

        private void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (Convert.ToBoolean(RaidableBases?.CallHook("EventTerritory", privilege.transform.position)))
                return;

            NextTick(() =>
            {
                if (privilege != null && player != null && privilege.IsAuthed(player))
                {
                    if (!pcdData.tcData.ContainsKey(player.userID.Get()))
                        pcdData.tcData.Add(player.userID.Get(), new List<ulong>() { privilege.net.ID.Value });
                    else if (!pcdData.tcData[player.userID.Get()].Contains(privilege.net.ID.Value))
                        pcdData.tcData[player.userID.Get()].Add(privilege.net.ID.Value);
                    SaveData();
                }
            });
        }

        private void OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (pcdData.tcData.ContainsKey(player.userID.Get()))
                if (pcdData.tcData[player.userID.Get()].Contains(privilege.net.ID.Value))
                    pcdData.tcData[player.userID.Get()].Remove(privilege.net.ID.Value);
                SaveData();
        }

        private object OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (pcdData.tcData.ContainsKey(player.userID.Get()) && pcdData.tcData[player.userID.Get()].Contains(privilege.net.ID.Value))
                pcdData.tcData[player.userID.Get()].Remove(privilege.net.ID.Value);

            foreach (var playerNameID in privilege.authorizedPlayers)
            {
                 if (pcdData.tcData.ContainsKey(playerNameID.userid) && pcdData.tcData[playerNameID.userid].Contains(privilege.net.ID.Value))
                    pcdData.tcData[playerNameID.userid].Remove(privilege.net.ID.Value);
            }
            SaveData();
            return null;
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.InitiatorPlayer == null || !info.InitiatorPlayer.userID.IsSteamId() || player is RandomRaider || player.skinID == 14922524) return;           
            string name = player.ShortPrefabName;
            if (!string.IsNullOrEmpty(name))
            {  
                if (name.Contains("scientistnpc_junkpile"))
                    name = "scientistnpc_junkpile";

                if (Convert.ToBoolean(BotReSpawn?.CallHook("IsBotReSpawn", player.userID.Get())))
                    return;

                if (!logData.pLogs.ContainsKey(info.InitiatorPlayer.userID.Get()))
                    logData.pLogs.Add(info.InitiatorPlayer.userID.Get(), new lData());

                if (configData.settingsNPCKILLS.enableKillsCombine && configData.settingsNPCKILLS.CombineNpcList.Contains(name))
                {
                    logData.pLogs[info.InitiatorPlayer.userID.Get()]._totalNpcKills++;
                    SaveLogData();
                    if (logData.pLogs[info.InitiatorPlayer.userID.Get()]._totalNpcKills >= configData.settingsNPCKILLS.CombineProfile.totalRaids)
                    {
                        CallRaidOnNpcKills(info.InitiatorPlayer, name, configData.settingsNPCKILLS.CombineProfile, true);
                        if (configData.settingsNPCKILLS.CombineProfile.resetKillCount)
                            logData.pLogs[info.InitiatorPlayer.userID.Get()]._totalNpcKills = 0;
                    }
                    return;
                }

                if (configData.settingsNPCKILLS.enableKills && configData.settingsNPCKILLS.npcSettings.ContainsKey(name))
                { 
                    if (!logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS.ContainsKey(name))
                        logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS.Add(name, 1);
                    else
                        logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS[name]++;
                    SaveLogData();
                    if (logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS[name] >= configData.settingsNPCKILLS.npcSettings[name].totalRaids)
                    { 
                        CallRaidOnNpcKills(info.InitiatorPlayer, name, configData.settingsNPCKILLS.npcSettings[name]);
                        if(configData.settingsNPCKILLS.npcSettings[name].resetKillCount)
                            logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS[name] = 0;
                    }
                }
            }           
        }

        void OnBotReSpawnNPCKilled(ScientistNPC player, string profilename, string group, HitInfo info)
        {
            if (player == null || info == null || info.InitiatorPlayer == null || !info.InitiatorPlayer.userID.IsSteamId()) return;
            if (!string.IsNullOrEmpty(profilename))
            {
                if (configData.settingsNPCKILLS.enableKillsCombine && configData.settingsNPCKILLS.CombineNpcList.Contains(profilename))
                {
                    logData.pLogs[info.InitiatorPlayer.userID.Get()]._totalNpcKills++;
                    SaveLogData();
                    if (logData.pLogs[info.InitiatorPlayer.userID.Get()]._totalNpcKills >= configData.settingsNPCKILLS.CombineProfile.totalRaids)
                    {
                        CallRaidOnNpcKills(info.InitiatorPlayer, profilename, configData.settingsNPCKILLS.CombineProfile);
                        if (configData.settingsNPCKILLS.CombineProfile.resetKillCount)
                            logData.pLogs[info.InitiatorPlayer.userID.Get()]._totalNpcKills = 0;
                        return;
                    }
                }

                if (configData.settingsNPCKILLS.enableKills && configData.settingsNPCKILLS.npcSettings.ContainsKey(profilename))
                {
                    if (!logData.pLogs.ContainsKey(info.InitiatorPlayer.userID.Get()))
                        logData.pLogs.Add(info.InitiatorPlayer.userID.Get(), new lData());
                    if (!logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS.ContainsKey(profilename))
                        logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS.Add(profilename, 1);
                    else
                        logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS[profilename]++;
                    SaveLogData();
                    if (logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS[profilename] >= configData.settingsNPCKILLS.npcSettings[profilename].totalRaids)
                    {
                        CallRaidOnNpcKills(info.InitiatorPlayer, profilename, configData.settingsNPCKILLS.npcSettings[profilename]);
                        if(configData.settingsNPCKILLS.npcSettings[profilename].resetKillCount)
                                logData.pLogs[info.InitiatorPlayer.userID.Get()].NPCKILLS[profilename] = 0;
                    }
                }
            }
        }

        private void LocateBaseWithMarker(BasePlayer player, Vector3 drawpos, float duration = 60f)
        {
            if (drawpos != Vector3.zero)
            {
                bool tempAdmin = false;
                if (!player.IsAdmin)
                {
                    tempAdmin = true;
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendNetworkUpdateImmediate();
                }
                player.SendConsoleCommand("ddraw.text", duration, Color.yellow, drawpos + new Vector3(0, 0.5f, 0), string.Format(lang.GetMessage("locateBaseText", this, player.UserIDString)));
                if (tempAdmin)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
            }
        }

        public BaseNetworkable FindEntity(ulong netID)
        {
            return BaseNetworkable.serverEntities.Find(new NetworkableId(netID));
        }

        private void CallRaidOnNpcKills(BasePlayer p, string Rtype, NPCConfig config, bool combined = false)
        {
            List<ulong> tcDataTemp = Pool.Get<List<ulong>>();

            if (p != null)
            {	
                if (configData.settings.musthavePerm && !permission.UserHasPermission(p.UserIDString, usePerm))
                    return;

                string type = "";
                BuildingPrivlidge priv = null;
                Vector3 location = Vector3.zero;
                List<ulong> authPlayers = new List<ulong>();
                List<BuildingBlock> block = new List<BuildingBlock>();

                if (config == null) return;

                int rando = UnityEngine.Random.Range(0, 100);
                
                if (rando <= config.raidChance && pcdData.tcData.Count > 0)
                {
                    if (pcdData.tcData.ContainsKey(p.userID.Get()))
                    {
                        tcDataTemp.AddRange(pcdData.tcData[p.userID.Get()]);

                        foreach (ulong key in tcDataTemp)
                        {
                            BuildingPrivlidge privFind = FindEntity(key) as BuildingPrivlidge;
                            if (privFind == null)
                                pcdData.tcData[p.userID.Get()].Remove(key);
                            else if (priv == null)
                            {
                                if (!CanBeRaided(privFind))
								{
									continue;
								}
                                if (isBlockedTerain(privFind.transform.position))
                                    continue;
                                BuildingManager.Building build = privFind.GetBuilding();
                                if (build == null || !build.HasBuildingBlocks() || build.buildingBlocks.Count <= 6)
                                    continue;

                                 block = GenerateTargets(privFind);
                                     if (block.Count <= 6)
                                        continue;
                                 priv = privFind;
                                 location = priv.transform.position;
                                break;            
                            }
                        }
                        Pool.FreeUnmanaged(ref tcDataTemp);
                    }

                    if (priv == null)
                        return;

                    if (configData.settingsNPCKILLS.enforceAuth || configData.settingsNPCKILLS.BlockCheck)
                    {
                        int totalAuth = priv.authorizedPlayers.Count();
                        List<string> raidsToUse = Pool.Get<List<string>>();
                        raidsToUse.AddRange(config.sendRaids);
                        if (totalAuth > 0)
                        {
                            foreach (var keyNext in raidsToUse)
                            {
                                if (configData.settingsNPCKILLS.enforceAuth && configData.raidSettings.raidTypes.ContainsKey(keyNext) && totalAuth < configData.raidSettings.raidTypes[keyNext].TotalAuth && raidsToUse.Contains(keyNext))
                                        raidsToUse.Remove(keyNext);

                                if (configData.settingsNPCKILLS.BlockCheck && raidsToUse.Contains(keyNext) && configData.raidSettings.raidTypes.ContainsKey(keyNext) && block.Count < configData.raidSettings.raidTypes[keyNext].blockMIN)
                                    raidsToUse.Remove(keyNext);
                            }
                        }

                        type = raidsToUse.GetRandom();
                        Pool.FreeUnmanaged(ref raidsToUse);
                    }
                    else
                        type = config.sendRaids.GetRandom();

                    if (!string.IsNullOrEmpty(type))
                    {
                        foreach (var user in priv.authorizedPlayers)
                        {
                            if (raidData.data.ContainsKey(user.userid))
                            {
                                if (raidData.data[user.userid].LastRaidedNpcKills >= DateTime.Now)
                                {
                                    return;
                                }
                            }

                            if (!authPlayers.Contains(user.userid))
                                authPlayers.Add(user.userid);
                            
                            if (configData.settingsNPCKILLS.resetKills)
                            {
                                if (logData.pLogs.ContainsKey(user.userid) && logData.pLogs[user.userid].NPCKILLS != null)
                                    logData.pLogs[user.userid].NPCKILLS.Clear();  
                                SaveLogData();

                            }

                            if (configData.settingsNPCKILLS.warnPlayer)
                            {
                                BasePlayer p2 = BasePlayer.FindByID(user.userid);
                                if (p2 != null)
                                {
                                    TimeSpan newTime = TimeSpan.FromSeconds(config.raidDelay);

                                    if (configData.settings.gameTip)
                                        GameTipMessage(p2, string.Format(lang.GetMessage("warningRevengeGridNpc", this, p2.UserIDString), GetGrid(priv.transform.position), FormatTime(newTime), FormatTimeS(newTime)));
                                    else
                                        SendReply(p2, lang.GetMessage("warningRevengeGridNpc", this, p2.UserIDString), GetGrid(priv.transform.position), FormatTime(newTime), FormatTimeS(newTime));

                                    if (configData.settings.worldMarker)
                                        LocateBaseWithMarker(p2, priv.transform.position, configData.settings.worldMarkerTime);
                                }
                            }
                        }

                        foreach (var userid in authPlayers)
                        {
                            if (!raidData.data.ContainsKey(userid))
                                raidData.data.Add(userid, new rData() { LastRaidedNpcKills = DateTime.Now.AddMinutes(config.chanceCooldown) });
                            else raidData.data[userid].LastRaidedNpcKills = DateTime.Now.AddMinutes(config.chanceCooldown);
                        }
                        SaveRaidData();
                        startRandomRaid(location, authPlayers, type, block, config.raidDelay);
                        LogToFile("NpcKillsRaids", $"[{DateTime.Now}] Started raid triggered at {priv.transform.position}, For player {p.displayName}", this);
                        if (configData.settings.DiscordWebHook)
                            ServerMgr.Instance.StartCoroutine(SendToDiscord(ConVar.Server.hostname, p.displayName, priv.transform.position.ToString(), $"Npc Kills {type}"));
                    }
                }
            }
        }

        private void CallRaidHere(BuildingPrivlidge privFind, string Rtype, BasePlayer p)
        {
            string type = "";
            Vector3 location = Vector3.zero;
            List<ulong> authPlayers = new List<ulong>();
            List<BuildingBlock> block = new List<BuildingBlock>();

            if (!configData.raidSettings.raidTypes.ContainsKey(Rtype))
            {
                SendReply(p, lang.GetMessage("NoConfigType", this, p.UserIDString), Rtype);
                return;
            }

            raidConfig config = configData.raidSettings.raidTypes[Rtype];

            if (config == null)
            {
                SendReply(p, lang.GetMessage("NoConfigType", this, p.UserIDString), Rtype);
                return;
            }

            if (isBlockedTerain(privFind.transform.position))
            {
                SendReply(p, lang.GetMessage("terrainBlocked", this, p.UserIDString));
                return;
            }

            BuildingManager.Building build = privFind.GetBuilding();
            if (build == null || !build.HasBuildingBlocks() || build.buildingBlocks.Count < 5)
            {
                SendReply(p, lang.GetMessage("BaseToSmallYet", this, p.UserIDString));
                return;
            }

            block = GenerateTargets(privFind);
            if (block.Count < 5)
            {
                SendReply(p, lang.GetMessage("BaseToSmallYet", this, p.UserIDString));
                return;
            }

            location = privFind.transform.position;

            foreach (var user in privFind.authorizedPlayers)
                if (!authPlayers.Contains(user.userid))
                    authPlayers.Add(user.userid);

            startRandomRaid(location, authPlayers, Rtype, block);
            SendReply(p, lang.GetMessage("AdminRaidStarted", this, p.UserIDString));
        }
        #endregion

        #region Explode Handler
        private void OnExplosiveDropped(BasePlayer player, TimedExplosive entity, ThrownWeapon item)
        {
            OnExplosiveThrown(player, entity, item);
        }

        private void OnExplosiveThrown(BasePlayer player, TimedExplosive entity, ThrownWeapon item)
        {
            if (entity != null && entity.skinID != 0 && entitySkinToConfig.ContainsKey(entity.skinID))
            {
                if (!CanstartFlareRaid(player, entitySkinToConfig[entity.skinID]))
                {
                    entity.Kill();
                }
            }
            else if (item != null)
            {
                Item raidItem = item.GetItem();
                if (raidItem != null && raidItem.name != null && itemNameToConfig.ContainsKey(raidItem.name))
                {
                    if (!CanstartFlareRaid(player, itemNameToConfig[raidItem.name]))
                    {
                        entity.Kill();
                    }
                }
            }
        }

        private bool CanstartFlareRaid(BasePlayer player, string configName)
        {
            List<BuildingBlock> block = new List<BuildingBlock>();
            List<ulong> authPlayers = new List<ulong>();

            LogToFile("FlareItem", $"[{DateTime.Now}] {player.displayName} attempt to start raid at {player.transform.position}", this);
            BuildingPrivlidge privCheck = player.GetBuildingPrivilege();
            if (privCheck == null || !player.CanBuild() || !privCheck.IsAuthed(player))
            {
                SendReply(player, lang.GetMessage("NeedBuildingPrivToss", this, player.UserIDString));
                GetFlareItem(player, configName, 1, false);
                return false;
            }

            if (isBlockedTerain(privCheck.transform.position))
            {
                SendReply(player, lang.GetMessage("Blockedhere", this, player.UserIDString));
                GetFlareItem(player, configName, 1, false);
                return false;
            }

            if (privCheck.transform.position.y <= -0.1f)
            {
                SendReply(player, lang.GetMessage("BlockedhereWater", this, player.UserIDString));
                GetFlareItem(player, configName, 1, false);
                return false;
            }

            Vector3 checkLocation = privCheck.transform.position;
            checkLocation.y = TerrainMeta.HeightMap.GetHeight(checkLocation);
            float waterLevel = WaterLevel.GetWaterDepth(checkLocation, true, false);
            if (waterLevel > 0.44f)
            {
                SendReply(player, lang.GetMessage("BlockedhereWaterDeep", this, player.UserIDString));
                GetFlareItem(player, configName, 1, false);
                return false;
            }

            if (EventRandomManager._AllRaids.Count >= configData.settings.totalRaids)
            {
                SendReply(player, lang.GetMessage("MaxEvents", this, player.UserIDString));
                GetFlareItem(player, configName, 1, false);
                return false;
            }

            if (!CanBeRaidedFlair(player, privCheck))
            {
                GetFlareItem(player, configName, 1, false);
                return false;
            }

            BuildingManager.Building build = privCheck.GetBuilding();
            if (build == null || !build.HasBuildingBlocks() || build.buildingBlocks.Count < 5)
            {
                SendReply(player, lang.GetMessage("BaseToSmallYet", this, player.UserIDString));
                GetFlareItem(player, configName, 1, false);
                return false;
            }

            block = GenerateTargets(privCheck);
            if (block.Count < 5)
            {
                SendReply(player, lang.GetMessage("BaseToSmallYet", this, player.UserIDString));
                GetFlareItem(player, configName, 1, false);
                return false;
            }

            foreach (var user in privCheck.authorizedPlayers)
            {
                if (!authPlayers.Contains(user.userid))
                    authPlayers.Add(user.userid);
            }

            startRandomRaid(privCheck.transform.position, authPlayers, configName, block);
            LogToFile("FlareItem", $"[{DateTime.Now}] {player.displayName} started raid at {player.transform.position}", this);
            if (player != null)
            {
                SendReply(player, $"You are now going to get Npc raided in grid: {GetGrid(privCheck.transform.position)}");
                if (configData.settings.DiscordWebHook)
                    ServerMgr.Instance.StartCoroutine(SendToDiscord(ConVar.Server.hostname, player.displayName, privCheck.transform.position.ToString(), $"Flare item {configName}"));
            }
            return true;
        }
        #endregion

        #region FlairItem
        [ConsoleCommand("randomraid")]
        private void CmdConsolePage(ConsoleSystem.Arg args)
        {
            if (args == null || args.Args == null || args.Args.Length < 2)
            {
                SendReply(args, "Usage: randomraid <playerID> <itemType>");
                return;
            }
            string userID = args.Args[0];
            string portal = args.Args[1];

            BasePlayer player = null;
            var ids = default(ulong);
            if (ulong.TryParse(userID, out ids))
            {
                player = BasePlayer.FindByID(ids);
            }

            if (player != null)
            {
                string[] theItemConfig = args.Args.Skip(1).ToArray();
                GetTheMRaidItem(player, "", theItemConfig);
            }
        }

        private void GetTheMRaidItem(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length <= 0)
            {
                messagePlayer(player);
                return;
            }

            if (args[0].ToLower() == "list")
            {
                messagePlayer(player);
                return;
            }

            var theItemConfig = args[0];
            if (!configData.itemProfile.ContainsKey(theItemConfig))
            {
                messagePlayer(player);
                return;
            }

            int total = 1;

            if (args.Length > 1)
            {
                if (!int.TryParse(args[1], out total))
                    total = 1;
            }

            if (configData.itemProfile.ContainsKey(theItemConfig))
            {
                GetFlareItem(player, theItemConfig, total, true);
                return;
            }

            SendReply(player, lang.GetMessage("NoValidItem", this, player.UserIDString), theItemConfig);
        }

        private void messagePlayer(BasePlayer player)
        {
            if (player == null)
                return;
            string configitems = "<color=#ce422b>Raid Item List Usage /randomraidsitem <config name></color>\n\n";
            foreach (var key in configData.itemProfile)
            {
                configitems += $"<color=#FFFF00>Config Name</color>: {key.Key} <color=#FFFF00>Item Name:</color> {key.Value.itemName}\n";
            }
            SendReply(player, configitems);
        }

        private void GetFlareItem(BasePlayer player, string name, int total, bool message)
        {
            itemConfig itemconfig = configData.itemProfile[name];
            var raidItem = ItemManager.CreateByItemID(304481038, total, itemconfig.itemSkin);
            if (raidItem == null) return;
            raidItem.name = itemconfig.itemName;
            BaseEntity held = raidItem.GetHeldEntity();
            if (held != null)
            {
                held.name = itemconfig.itemName;
                held.skinID = itemconfig.itemSkin;
            }
            if (raidItem.MoveToContainer(player.inventory.containerBelt, -1, true))
            {
                if (message) SendReply(player, lang.GetMessage("gaveProtector", this), itemconfig.itemName);
                return;
            }
            else if (raidItem.MoveToContainer(player.inventory.containerMain, -1, true))
            {
                if (message) SendReply(player, lang.GetMessage("gaveProtector", this), itemconfig.itemName);
                return;
            }
            Vector3 velocity = new Vector3(4.8654f, 2.6241f, 1.3869f);
            velocity = Vector3.zero;
            raidItem.Drop(player.transform.position + new Vector3(0.5f, 1f, 0), velocity);
            if (message) SendReply(player, lang.GetMessage("droped", this), itemconfig.itemName);
        }
        #endregion

        #region Patrol Helicopter
        public static PatrolHelicopter spawnHeli(EventRandomManager newManager, Vector3 location, copterConfig theConfig)
        {
            Vector3 spawnLocation = location + new Vector3(150f, 100f, -150f);
            PatrolHelicopter heli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", spawnLocation, new Quaternion(), true) as PatrolHelicopter;
            if (heli == null)
                return null;

            PatrolHelicopterAI ai = heli.GetComponent<PatrolHelicopterAI>();
            attackHelicopterAI newAI = heli.gameObject.AddComponent<attackHelicopterAI>();
            if (ai == null || newAI == null) return null;

            newAI.setupManager(newManager);

            heli.startHealth = theConfig.startHealth;
            heli.InitializeHealth(theConfig.startHealth, theConfig.startHealth);
            heli.health = theConfig.startHealth;
            heli.maxCratesToSpawn = theConfig.maxCratesToSpawn;
            newAI.totalStrafe = theConfig.totalStrafe;
            newAI.strafeTime = theConfig.strafeTime;

            location.y = location.y + 50;
            ai.SetInitialDestination(location, 0.25f);
            heli.skinID = 8675309;
            heli.Spawn();
            heli.transform.position = spawnLocation;
            location.y = location.y + 50;
            Interface.Oxide.CallHook("OnRandomRaidHeliSpawned", location, heli);
            return heli;
        }

        public class attackHelicopterAI : MonoBehaviour
        {
            private PatrolHelicopter heli { get; set; }
            private PatrolHelicopterAI heliAI { get; set; }
            private EventRandomManager manager { get; set; }
            private bool isRetire { get; set; }
            private bool Active { get; set; }
            private DateTime strafe { get; set; }
            public int totalStrafe { get; set; }
            public float strafeTime { get; set; }
            public Vector3 location { get; set; }

            private void Awake()
            {
                heli = GetComponent<PatrolHelicopter>();
                heliAI = GetComponent<PatrolHelicopterAI>();
            }

            public void setupManager(EventRandomManager newManager)
            {
                manager = newManager;
                location = manager.location;
                strafe = DateTime.Now.AddSeconds(strafeTime);
                Active = true;
                UpdateMono();
                InvokeRepeating("UpdateMono", 6.0f, 6.0f);
            }

            public bool AtDestination() => (double)Vector3.Distance(this.transform.position, location) < 50;

            private void Update()
            {
                if (isRetire || !Active || heli == null || heliAI == null || heliAI._currentState == PatrolHelicopterAI.aiState.DEATH) return;
                if (!isRetire && manager != null && manager.isDestroyed)
                {
                    isRetire = true;
                    heliAI.Retire();
                }
                if (heliAI.CanStrafe())
                    heliAI.lastStrafeTime = UnityEngine.Time.realtimeSinceStartup + 2000;

                if (heliAI.dangerZones.Count > 0)
                    heliAI.dangerZones.Clear();
                if (heliAI.noGoZones.Count > 0)
                    heliAI.noGoZones.Clear();
        }

            private void UpdateMono()
            {
                if (isRetire || !Active || heli == null || heliAI == null || heliAI._currentState == PatrolHelicopterAI.aiState.DEATH) return;

                if (!AtDestination() && heliAI._currentState != PatrolHelicopterAI.aiState.STRAFE && heliAI.interestZoneOrigin != location)
                {
                    heliAI.spawnTime = UnityEngine.Time.realtimeSinceStartup;
                    heliAI.hasInterestZone = true;
                    heliAI.interestZoneOrigin = location;
                    heliAI.ExitCurrentState();
                    heliAI.State_Move_Enter(location);
                }
                else if (heliAI._currentState == PatrolHelicopterAI.aiState.MOVE && AtDestination())
                {
                    heliAI.spawnTime = UnityEngine.Time.realtimeSinceStartup;
                    heliAI.interestZoneOrigin = location;
                    heliAI.State_Orbit_Enter(30.0f);
                }
                else if (totalStrafe > 0 && strafe < DateTime.Now && heliAI._currentState != PatrolHelicopterAI.aiState.STRAFE)
                {
                    if (heliAI._targetList.Count > 0)
                    {
                        BasePlayer stPlayer = heliAI._targetList[0].ply;
                        if (stPlayer != null)
                        {
                            heliAI.State_Strafe_Enter(stPlayer, false);
                            strafe = DateTime.Now.AddSeconds(strafeTime);
                            totalStrafe--;
                        }
                    }
                }
                if (manager == null || manager.isDestroyed && heliAI != null && !heli.IsDestroyed)
                {
                    isRetire = true;
                    heliAI.Retire();
                }
            }

            private void OnDestroy()
            {
                if (heli != null && !heli.IsDestroyed)
                    heli.Kill();
                CancelInvoke("UpdateMono");
            }
        }
        #endregion

        #region Localization
        public static void GameTipMessage(BasePlayer player, string message)
        {
            if (player != null && player.userID.IsSteamId())
            {
                player?.SendConsoleCommand("gametip.hidegametip");
                player?.SendConsoleCommand("gametip.showgametip", message);
                _.timer.Once(_.configData.settings.gameTipTime, () => player?.SendConsoleCommand("gametip.hidegametip"));
            }
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["warningRevengeGrid"] = "<color=orange>[</color><color=red>Cobalt Scientist</color><color=orange>]</color> So you raided our base at {0}, now we are coming for your base at {1} in {2}m {3}s if you don't <color=#ce422b>/surrender</color>.",
                ["warningRevengeGridNpc"] = "<color=orange>[</color><color=red>Cobalt Scientist</color><color=orange>]</color> So you think you can keep killing us? Now we are coming for your base at {0} in {1}m {2}s if you don't <color=#ce422b>/surrender</color>.",
                ["gridMessageDirection"] = "<color=orange>[</color><color=red>RANDOM RAID DETECTION</color><color=orange>]</color> in grid {0}, {1} of you {2} yards!",
                ["RaidersLeaving"] = "<color=orange>[</color><color=red>Cobalt Scientist</color><color=orange>]</color> Do not make us come back here!",
				["negotiateMessageNew"] = "<color=orange>[</color><color=red>Cobalt Scientist</color><color=orange>]</color> {0} {1} and we forget about this little mistake you made.",
				["payedAndSurrenderedNew"] = "<color=orange>[</color><color=red>{0}</color><color=orange>]</color> surrendered by paying {1} {2}.",
                ["msgNorth"] = "North",
                ["msgNorthEast"] = "NorthEast",
                ["msgEast"] = "East",
                ["msgSouthEast"] = "SouthEast",
                ["msgSouth"] = "South",
                ["msgSouthWest"] = "SouthWest",
                ["msgWest"] = "West",
                ["msgNorthWest"] = "NorthWest",
                ["usageHere"] = "/randomraid here <raid type>",
                ["usageItem"] = "/randomraid item <raid type>",
                ["NoConfigType"] = "You do not have a config type of {0}",
                ["BaseToSmallYet"] = "This base is to small yet!",
                ["AdminRaidStarted"] = "You just started a raid here!",
                ["terrainBlocked"] = "This base in on unradable terrain!",
                ["usageChatAdmin"] = "/randomraid <here/random/reload>",
                ["BaseToCose"] = "Your can not start a Npc raid when you are close to another ongoing one!",
                ["gaveProtector"] = "<color=#ce422b>You have just got a {0}!</color>",
                ["droped"] = "<color=#ce422b>You'r inventory was full so i dropped your {0} on the ground!</color>",
                ["blocked"] = "<color=#ce422b>You are building blocked!</color>",
                ["NoValidItem"] = "That is not a valid config item {0}!",
                ["NoPlayer"] = "Player not found!",
                ["ammountNot"] = "Amount not set correctly",
                ["NeedBuildingPrivToss"] = "You must be in building priv to toss this.",
                ["Blockedhere"] = "<color=orange>The Admin has blocked raids on this terrain type.</color>",
                ["BlockedhereWater"] = "<color=orange>You can not start the event underground.</color>",
                ["BlockedhereWaterDeep"] = "<color=orange>You can not start the event in this water depth.</color>",
                ["guiMessage"] = "<color=#ce422b>Wave</color>: <color=#FFFF00>{0}</color>/<color=#FFFF00>{1}</color>  <color=#ce422b>Raiders</color>:<color=#FFFF00> {2}</color>  <color=#ce422b>{3}</color>: <color=#FFFF00>{4}</color>m <color=#FFFF00>{5}</color>s",
				["guiMessageSurrenderNew"] = "<color=#ce422b>Surrender with</color> /surrender <color=#ce422b>for</color> <color=#FFFF00>{0}</color> {1}.",
                ["nextWave"] = "Next Wave",
                ["endEvent"] = "Ends In",
                ["locateBaseText"] = "Raid Incoming Here",
                ["MaxEvents"] = "The Server Is At Max Raid Events, Please Try Again Later.",
                ["IncomingRandom"] = "A Group of raiders are headed to your base in {0}.",
                ["LockedLoot"] = "This loot is locked to event players.",
                ["inEvent"] = "You are already in a event!"
            }, this);
        }
        #endregion

        #region Discord Class
        public class Message
        {
            public string username { get; set; }
            public string avatar_url { get; set; }
            public List<Embeds> embeds { get; set; }

            public class Fields
            {
                public string name { get; set; }
                public string value { get; set; }
                public bool inline { get; set; }
                public Fields(string name, string value, bool inline)
                {
                    this.name = name;
                    this.value = value;
                    this.inline = inline;
                }
            }

            public class Footer
            {
                public string text { get; set; }
                public Footer(string text)
                {
                    this.text = text;
                }
            }

            public class Embeds
            {
                public string title { get; set; }
                public string description { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Embeds(string title, string description, List<Fields> fields, Footer footer)
                {
                    this.title = title;
                    this.description = description;
                    this.fields = fields;
                    this.footer = footer;
                }
            }

            public Message(string username, string avatar_url, List<Embeds> embeds)
            {
                this.username = username;
                this.avatar_url = avatar_url;
                this.embeds = embeds;
            }
        }

        private Message DiscordMessage(string servername, string displayName, string location, string typeRaid)
        {
            if (configData.settings.AvatarURL == null)
                configData.settings.AvatarURL = "";

            var fields = new List<Message.Fields>()
            {
                new Message.Fields("Player", displayName, false),
                new Message.Fields("Location", location, false),
                new Message.Fields("Type ", typeRaid, false),
            };
            var footer = new Message.Footer($"Logged @{DateTime.UtcNow:dd/MM/yy HH:mm:ss}");
            var embeds = new List<Message.Embeds>()
            {
                new Message.Embeds(servername, "Random raid started", fields, footer)
            };
            Message msg = new Message("Random Raid Detected", configData.settings.AvatarURL, embeds);
            return msg;
        }

        private IEnumerator SendToDiscord(string servername, string displayName, string location, string typeRaid)
        {
            if (!string.IsNullOrEmpty(configData.settings.WebHookUrl))
            {
                if (displayName == null)
                    displayName = "UnKnown player";

                var msg = DiscordMessage(ConVar.Server.hostname, displayName, location, typeRaid);
                string jsonmsg = JsonConvert.SerializeObject(msg);
                UnityWebRequest wwwpost = new UnityWebRequest(configData.settings.WebHookUrl, "POST");
                byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonmsg.ToString());
                wwwpost.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
                wwwpost.SetRequestHeader("Content-Type", "application/json");
                yield return wwwpost.SendWebRequest();

                if (wwwpost.isNetworkError || wwwpost.isHttpError)
                {
                    PrintError(wwwpost.error);
                }
                wwwpost.Dispose();
            }

        }
        #endregion
    }
}
    
