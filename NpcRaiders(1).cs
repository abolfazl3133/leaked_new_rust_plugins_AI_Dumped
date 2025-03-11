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

namespace Oxide.Plugins
{
    [Info("NpcRaiders", "WhitePlugins.Ru", "1.4.6", ResourceId = 107)]
    [Description("Spawn Npc to raid your base.")]
    public class NpcRaiders : RustPlugin
    {
        #region Loading
        public static bool debug = false;

        [PluginReference]
        private Plugin Kits, TruePVE, ServerRewards, Economics, LifeSupport, ZoneManager;

        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;

        PlayerStats statsData;
        private DynamicConfigFile STATSDATA;

        public static NpcRaiders _;
        private Dictionary<BasePlayer, eventList> EventList = new Dictionary<BasePlayer, eventList>();
        private Dictionary<ulong, List<ulong>> corpsLock = new Dictionary<ulong, List<ulong>>();
        public static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        public static List<MLRS> mlrs = new List<MLRS>();

        private static int groundLayer;
        private static int terainLayer;
        private static int BuildingLayer;
        private static Vector3 Vector3Down;
        private Color backgroundColor;
        private Color PanelColor;
        private static string usePerm = "npcraiders.use";
        private static string coolPerm = "npcraiders.nocooldown";
        private static string adminPerm = "npcraiders.admin";
        private static string bypassPerm = "npcraiders.bypass";
        private static string bypassCost = "npcraiders.nocost";
        private float nextTime;
        private string chatComand = "raidme";
        public class eventList
        {
            public string type;
            public Vector3 location;
            public BuildingPrivlidge priv;
        }

        private void Init()
        {
            ColorExtensions.TryParseHexString("#2b2b2b", out backgroundColor);
            ColorExtensions.TryParseHexString("#404141F2", out PanelColor);
            cmd.AddConsoleCommand("npcraiders", this, "ConsolStartRaid");
            if (!string.IsNullOrEmpty(configData.settings.chatcommand))
            {
                chatComand = configData.settings.chatcommand;
                cmd.AddChatCommand(configData.settings.chatcommand, this, "cmdChatStartRaid");
            }
            else
            {
                cmd.AddChatCommand("raidme", this, "cmdChatStartRaid");
            }
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/PlayerCooldowns");
            STATSDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/PlayerStats");
            LoadData();
            _ = this;
            permission.RegisterPermission(usePerm, this);
            permission.RegisterPermission(coolPerm, this);
            permission.RegisterPermission(adminPerm, this);
            permission.RegisterPermission(bypassPerm, this);
            permission.RegisterPermission(bypassCost, this);
        }

        private void OnServerInitialized()
        {
			PrintWarning("\n-----------------------------\n" +
            " Author - WhitePlugins.Ru\n" +
            " VK - https://vk.com/rustnastroika/n" +
            " Forum - https://whiteplugins.ru/n" +
            " Discord - https://discord.gg/5DPTsRmd3G/n" +
            "-----------------------------");
            if (TruePVE != null)
            {
                Unsubscribe("OnEntityTakeDamage");
                Unsubscribe("CanBeTargeted");
            }
            groundLayer = LayerMask.GetMask("Construction", "Terrain", "World");
            terainLayer = LayerMask.GetMask("Terrain", "World", "Water");
            BuildingLayer = LayerMask.GetMask("Construction");
            Vector3Down = new Vector3(0f, -1f, 0f);
            if (configData.raidSettings.raidTypes.Count <= 0)
            {
                if (configData.raidBuyOptions.BuyOptions.Count <= 0)
                {
                    configData.raidBuyOptions.BuyOptions.Add("easy", new raidBuy("-932201673", 500));
                    configData.raidBuyOptions.BuyOptions.Add("medium", new raidBuy("-932201673", 1000));
                    configData.raidBuyOptions.BuyOptions.Add("hard", new raidBuy("-932201673", 1500));
                    configData.raidBuyOptions.BuyOptions.Add("expert", new raidBuy("-932201673", 1500));
                    configData.raidBuyOptions.BuyOptions.Add("nightmare", new raidBuy("-932201673", 1500));
                }

                if (configData.raidRewardOptions.RewardOptions.Count <= 0)
                {
                    configData.raidRewardOptions.RewardOptions.Add("easy", new raidReward("-932201673", 500));
                    configData.raidRewardOptions.RewardOptions.Add("medium", new raidReward("-932201673", 1000));
                    configData.raidRewardOptions.RewardOptions.Add("hard", new raidReward("-932201673", 1500));
                    configData.raidRewardOptions.RewardOptions.Add("expert", new raidReward("-932201673", 1500));
                    configData.raidRewardOptions.RewardOptions.Add("nightmare", new raidReward("-932201673", 1500));
                }

                configData.raidSettings.raidTypes.Add("easy", new raidConfig(10, true, 100, 640, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 2, "npcraiders.easy", 15000, 1.0));
                configData.raidSettings.raidTypes.Add("medium", new raidConfig(15, true, 200, 900, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 3, "npcraiders.medium", 30000, 1.0));
                configData.raidSettings.raidTypes.Add("hard", new raidConfig(20, true, 400, 1200, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 4, "npcraiders.hard", 50000, 1.0));
                configData.raidSettings.raidTypes.Add("expert", new raidConfig(30, true, 400, 1500, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 4, "npcraiders.expert", 65000, 1.0));
                configData.raidSettings.raidTypes.Add("nightmare", new raidConfig(30, true, 400, 2100, new List<string>(), new List<string>() { "explosive.timed", "explosive.satchel" }, 5, "npcraiders.nightmare", 100000, 1.0));
                SaveConfig();
            }

            if (configData.raidVipOptions == null)
            {
                configData.raidVipOptions = new ConfigData.RaidVipOptions();
                configData.raidVipOptions.VipOptions = new Dictionary<string, raidVip>();
                configData.raidVipOptions.VipOptions.Add("npcraiders.vip1", new raidVip(3600, 43200));
                configData.raidVipOptions.VipOptions.Add("npcraiders.vip2", new raidVip(3600, 43200));
                configData.raidVipOptions.VipOptions.Add("npcraiders.vip3", new raidVip(3600, 43200));
                SaveConfig();
            }

            timer.Every(10f, () =>
            {
                if (EventList.Count <= 0) return;

                if (EventRaidManager._AllRaids.Count < configData.settings.MaxRaids)
                {
                    foreach (var key in EventList.ToList())
                    {
                        if (key.Key == null)
                        {
                            EventList.Remove(key.Key);
                            continue;
                        }
                        if (key.Value.priv == null)
                        {
                            if (key.Key != null)
                                SendReply(key.Key, lang.GetMessage("TcWasAlreadyDestroyed", this, key.Key.UserIDString));
                            continue;
                        }
                        if (key.Key != null)
                        {
                            if (configData.settings.gtip)
                                GameTipsMessage(key.Key, lang.GetMessage("waitingToStart", this, key.Key.UserIDString));
                            if (configData.settings.ctip)
                                SendReply(key.Key, lang.GetMessage("waitingToStart", this, key.Key.UserIDString));
                        }
                        timer.Once(59, () => startRaidForPlayer(key.Key, key.Value.location, key.Value.priv, key.Value.type));
                        EventList.Remove(key.Key);
                        break;
                    }
                }
            });

            foreach (var perm in configData.raidSettings.raidTypes)
            {
                if (!String.IsNullOrEmpty(perm.Value.Permission) && !permission.PermissionExists(perm.Value.Permission, this))
                    permission.RegisterPermission(perm.Value.Permission, this);
            }

            if (configData.raidVipOptions != null && configData.raidVipOptions.VipOptions.Count > 0)
                foreach (var perms in configData.raidVipOptions.VipOptions)
                    if (!String.IsNullOrEmpty(perms.Key) && !permission.PermissionExists(perms.Key, this))
                        permission.RegisterPermission(perms.Key, this);
        }

        private void Unload()
        {
            foreach (var eventRunning in EventRaidManager._AllRaids)
                eventRunning.Destroy();

            EventRaidManager._AllRaids.Clear();
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                CuiHelper.DestroyUi(player, UIMain);
                player.SendConsoleCommand("gametip.hidegametip");
            }

            _ = null;
        }

        #endregion Loading

        #region Data
        private void LoadData()
        {
            try
            {
                pcdData = Interface.GetMod().DataFileSystem.ReadObject<PlayerEntity>(Name + "/PlayerCooldowns");
            }
            catch
            {
                PrintWarning("Couldn't load Player data, creating new Playerfile");
                pcdData = new PlayerEntity();
            }
            try
            {
                statsData = Interface.GetMod().DataFileSystem.ReadObject<PlayerStats>(Name + "/PlayerStats");
            }
            catch
            {
                PrintWarning("Couldn't load Player stats, creating new Player stats file");
                statsData = new PlayerStats();
            }
        }

        public class PlayerEntity
        {
            public Dictionary<ulong, PCDInfo> pEntity = new Dictionary<ulong, PCDInfo>();
        }

        public class PCDInfo
        {
            public int total;
            public long cooldown;
            public Dictionary<ulong, long> tcCooldown = new Dictionary<ulong, long>();
        }

        public void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        public class PlayerStats
        {
            public Dictionary<ulong, StatsInfo> pEntity = new Dictionary<ulong, StatsInfo>();
        }

        public class StatsInfo
        {
            public string name;
            public int played;
            public int lost;
            public int won;
            public int killed;
            public int deaths;
        }

        public void SaveStats()
        {
            STATSDATA.WriteObject(statsData);
        }

        public class LootEntity
        {
            public int MaxItems;
            public int MinItems;
            public List<itemInfo> items = new List<itemInfo>();
        }

        public class itemInfo
        {
            public string item;
            public ulong skin;
            public string name;
            public int amountMax;
            public int amountMin;
            public string location;
        }

        public static bool HasSaveFile(string id) =>
            Interface.Oxide.DataFileSystem.ExistsDatafile(_.Name + "/LootProfiles/" + id);

        private static void SaveLootData<T>(T data, string filename = null) =>
            Interface.Oxide.DataFileSystem.WriteObject(filename ?? _.Name, data);

        private static void LoadLootData<T>(out T data, string filename = null) =>
            data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? _.Name);

        #endregion Data

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Random settings")]
            public Settings settings { get; set; }

            [JsonProperty(PropertyName = "All Raid Types")]
            public RaidSettings raidSettings { get; set; }

            [JsonProperty(PropertyName = "Raid Buy Options")]
            public RaidBuyOptions raidBuyOptions { get; set; }

            [JsonProperty(PropertyName = "Raid Reward Options")]
            public RaidRewardOptions raidRewardOptions { get; set; }

            [JsonProperty(PropertyName = "Raid Vip Options")]
            public RaidVipOptions raidVipOptions { get; set; }

            [JsonProperty(PropertyName = "Block raid in colider")]
            public BlockedColliders blockedColliders { get; set; }

            public class Settings
            {
                [JsonProperty(PropertyName = "Chat command")]
                public string chatcommand { get; set; }
                [JsonProperty(PropertyName = "The Maxum amount of raid events that can go on at once")]
                public int MaxRaids { get; set; }
                [JsonProperty(PropertyName = "Cooldown before you can call in another raid")]
                public int cooldownSeconds { get; set; }
                [JsonProperty(PropertyName = "Cooldown after authorising on a tc be for you can start a raid")]
                public int TcAuthCooldown { get; set; }
                [JsonProperty(PropertyName = "The maxum amount of time a rocket will fly before exploding")]
                public float RocketExplodeTime { get; set; }
                [JsonProperty(PropertyName = "Limit the damage to players building")]
                public bool DamagePlayerBuilding { get; set; }
                [JsonProperty(PropertyName = "Npc spawn damage delay")]
                public float damageDelay { get; set; }            
                [JsonProperty(PropertyName = "Log raidme buy chat command")]
                public bool logToFile { get; set; }
                [JsonProperty(PropertyName = "Use GameTip messages")]
                public bool gtip { get; set; } = true;
                [JsonProperty(PropertyName = "Use chat messages")]
                public bool ctip { get; set; } = true;
            }

            public class RaidSettings
            {
                [JsonProperty(PropertyName = "Raid types must be in lowercase")]
                public Dictionary<string, raidConfig> raidTypes { get; set; }
            }

            public class RaidBuyOptions
            {
                public Dictionary<string, raidBuy> BuyOptions { get; set; }
            }

            public class RaidRewardOptions
            {
                public Dictionary<string, raidReward> RewardOptions { get; set; }
            }

            public class RaidVipOptions
            {
                public Dictionary<string, raidVip> VipOptions { get; set; }
            }

            public class BlockedColliders
            {
                public List<string> Blocked { get; set; }
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
                    chatcommand = "raidme",
                    MaxRaids = 6,
                    cooldownSeconds = 3600,
                    TcAuthCooldown = 86400,
                    RocketExplodeTime = 4.0f,
                    DamagePlayerBuilding = false,
                    damageDelay = 2f,
                    logToFile = false
                },

                raidSettings = new ConfigData.RaidSettings
                {
                    raidTypes = new Dictionary<string, raidConfig>()
                },

                raidBuyOptions = new ConfigData.RaidBuyOptions
                {
                    BuyOptions = new Dictionary<string, raidBuy>()
                },

                raidRewardOptions = new ConfigData.RaidRewardOptions
                {
                    RewardOptions = new Dictionary<string, raidReward>()
                },

                raidVipOptions = new ConfigData.RaidVipOptions
                {
                    VipOptions = new Dictionary<string, raidVip>()
                },

                blockedColliders = new ConfigData.BlockedColliders
                {
                    Blocked = new List<string>() { "iceberg", "ice_berg", "ice_sheet", "icesheet" }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Version update detected!");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 0, 1))
            {
                configData = baseConfig;
                PrintWarning("Config update was needed and now completed!");
            }
            configData.Version = Version;
            PrintWarning("Version update completed!");
        }

        public class raidConfig
        {
            [JsonProperty(PropertyName = "The Permission")]
            public string Permission;
            [JsonProperty(PropertyName = "Total npcs per wave")]
            public int TotalNpcs;
            [JsonProperty(PropertyName = "How many extra waves")]
            public int TotalNpcWaves;
            [JsonProperty(PropertyName = "Seconds untell next wave")]
            public int NextWaveSeconds;
            [JsonProperty(PropertyName = "Npc fires Mlrs at base")]
            public bool FireMlrs;
            [JsonProperty(PropertyName = "Total event time in seconds")]
            public int RunTimeSeconds;
            [JsonProperty(PropertyName = "Block event players from repairing")]
            public bool canNotRepair;
            [JsonProperty(PropertyName = "Npc drop loot on death")]
            public bool NpcDropLoot;
            [JsonProperty(PropertyName = "Npc drop loot config names")]
            public List<string> NpcDropLootConfig;
            [JsonProperty(PropertyName = "Spawn health of the npc")]
            public float Health;
            [JsonProperty(PropertyName = "Rocket damage scale")]
            public double DamageScale;
            [JsonProperty(PropertyName = "Player damage scale from npc")]
            public float playerDamage;
            [JsonProperty(PropertyName = "Auto turret damage scale to npc")]
            public float AutoTurretDamage;
            [JsonProperty(PropertyName = "External Tc Scan Range")]
            public float ExternalTcRange;
            [JsonProperty(PropertyName = "Explosive item shortnames")]
            public List<string> explosive;
            [JsonProperty(PropertyName = "Names to give the npcs")]
            public List<string> NpcNames;
            [JsonProperty(PropertyName = "Spawn kits for the npcs")]
            public List<string> kits;
            [JsonProperty(PropertyName = "NPC aimConeScale default 2.0")]
            public float aimConeScale;

            public raidConfig(int TotalNpcs, bool OnlyAuthedMembers, float Health, int RunTimeSeconds, List<string> kits, List<string> explosive, int waves, string perms, int reward, double damage)
            {
                this.TotalNpcs = TotalNpcs;
                this.NpcDropLoot = OnlyAuthedMembers;
                this.Health = Health;
                this.kits = kits;
                this.RunTimeSeconds = RunTimeSeconds;
                this.explosive = explosive;
                this.TotalNpcWaves = waves;
                this.NextWaveSeconds = 120;
                this.Permission = perms;
                this.NpcNames = new List<string>() { "Cobalt Scientist" };
                this.DamageScale = damage;
                this.AutoTurretDamage = 1.0f;
                this.NpcDropLootConfig = new List<string>();
                this.FireMlrs = false;
                this.canNotRepair = false;
                this.playerDamage = 1.0f;
                this.ExternalTcRange = 150f;
                this.aimConeScale = 2.0f;
            }
        }

        public class raidBuy
        {
            public string BuyType;
            public object BuyAmmount;

            public raidBuy(string thetipe, object needed)
            {
                this.BuyType = thetipe;
                this.BuyAmmount = needed;
            }
        }

        public class raidReward
        {
            public bool enabled;
            public bool rewardAll;
            public string RewardType;
            public object RewardAmmount;

            public raidReward(string thetipe, object needed)
            {
                this.enabled = false;
                this.rewardAll = false;
                this.RewardType = thetipe;
                this.RewardAmmount = needed;
            }
        }

        public class raidVip
        {
            public int cooldownSeconds;
            public int TcAuthCooldown;

            public raidVip(int cool, int tcCool)
            {
                this.cooldownSeconds = cool;
                this.TcAuthCooldown = tcCool;
            }
        }
        #endregion Config

        #region Event
        public class RaidThinkManager : MonoBehaviour
        {
            EventRaidManager RM { get; set; }

            public void Setup(EventRaidManager R)
            {
                RM = R;
            }

            public void Update()
            {
                if (RM != null && !RM.isDestroyed)
                    RM.update();
                else if (this.gameObject != null) Destroy(this);
            }
        }

        public class EventRaidManager
        {
            internal static List<EventRaidManager> _AllRaids = new List<EventRaidManager>();
            internal static List<ulong> _AllPlayers = new List<ulong>();
            internal static List<NpcRaider> _AllMembers = new List<NpcRaider>();
            internal static bool isFireingMlrs;
            
            internal List<NpcRaider> members;
            internal List<ulong> authorizedPlayers;
            internal bool isDestroyed = false;
            internal Vector3 location;
            internal raidConfig config;
            internal string waveType;
            internal BasePlayer player;
            internal ulong playerID;
            internal BuildingPrivlidge priv;
            internal bool newWave;
            internal Coroutine QueuedRoutine;
            internal BuildingPrivlidge building;
            internal BaseCombatEntity tc;
            internal RaidThinkManager timerObject;
            internal float EndEventTime;
            internal float nextWaveTime = 0;
            internal int totalWaves = 0;
            internal float MlrsEventTime;
            internal bool active = false;
            internal int totalCopters = 3;
            internal GameObject thetimerObject;

            internal static EventRaidManager Create(BasePlayer p, ulong id, Vector3 l, raidConfig c, string t, RaidThinkManager timer, BuildingPrivlidge pr, GameObject gobj)
            {
                EventRaidManager manager = new EventRaidManager
                {
                    members = Pool.GetList<NpcRaider>(),
                    authorizedPlayers = Pool.GetList<ulong>(),
                    newWave = true,
                    config = c,
                    waveType = t,
                    player = p,
                    playerID = id,
                    location = l,
                    timerObject = timer,
                    building = pr,
                    thetimerObject = gobj
                };

                _AllRaids.Add(manager);
                return manager;
            }

            internal void Start()
            {
                Interface.Oxide.CallHook("NpcRaidersEventStart", player, playerID, waveType, location, this);

                MlrsEventTime = Time.time + UnityEngine.Random.Range(15f, 30f);
                EndEventTime = Time.time + config.RunTimeSeconds;
                nextWaveTime = Time.time + 3800;
                if (building != null)
                    foreach (var user in building.authorizedPlayers.ToList())
                    { 
                        if (!authorizedPlayers.Contains(user.userid))
                            authorizedPlayers.Add(user.userid);
                        if (config.canNotRepair && !_AllPlayers.Contains(user.userid))
                            _AllPlayers.Add(user.userid);

                    }
                QueuedRoutine = ServerMgr.Instance.StartCoroutine(GenerateEventMembers());
            }
            //R Thanks to HellFire on UMOD
            internal static void setupMlrs(EventRaidManager man)
            {
                if (isFireingMlrs || man == null || man.building == null) return;

                _.timer.Repeat(0.5f, 6, () =>
                {
                    if (man == null || man.building == null) return;
                    float baseGravity;
                    Vector3 posUP = man.building.transform.position;
                    posUP.y = posUP.y + 240f;
                    Vector3 posFire = new Vector3(man.building.transform.position.x + UnityEngine.Random.Range(-10.0f, 10.0f), man.building.transform.position.y, man.building.transform.position.z + UnityEngine.Random.Range(-10.0f, 10.0f));

                    Vector3 aimToTarget = GetAimToTarget(posUP, man.building.transform.position, out baseGravity);

                    var startPoint = posUP;
                    startPoint.y += 15f;

                    ServerProjectile projectile;

                    if (CreateAndSpawnRocket(startPoint, aimToTarget, out projectile) == false)
                        return;
                    TimedExplosive component = projectile.GetComponent<TimedExplosive>();
                    component.creatorEntity = man.player;
                    projectile.gravityModifier = baseGravity / (0f - Physics.gravity.y);
                });

            }

            internal static Vector3 GetAimToTarget(Vector3 startPosition, Vector3 targetPos, out float baseGravity)
            {
                Vector3 vector = targetPos - startPosition;
                //vector = new Vector3(-107.3504f, 12.1489f, -107.7641f);
                float num = 90f;
                float num2 = vector.Magnitude2D();
                float y = vector.y;
                float num5 = 40f;

                baseGravity = ProjectileDistToGravity(Mathf.Max(num2, 50f), y, num5, num);
float ProjectileDistToGravity(float x, float y, float θ, float v)
{
    float num = θ * 0.017453292f;
    float num2 = (v * v * x * Mathf.Sin(2f * num) - 2f * v * v * y * Mathf.Cos(num) * Mathf.Cos(num)) / (x * x);
    if (float.IsNaN(num2) || num2 < 0.01f)
    {
        num2 = -Physics.gravity.y;
    }
    return num2;
}

                vector.Normalize();
                vector.y = 0f;

                Vector3 axis = Vector3.Cross(vector, Vector3.up);

                vector = Quaternion.AngleAxis(num5, axis) * vector;

                return vector;
            }

            internal static bool CreateAndSpawnRocket(Vector3 firingPos, Vector3 firingDir, out ServerProjectile mlrsRocketProjectile)
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


            internal void update()
            {
                ulong id = 0UL;
                if (isDestroyed || !active)
                    return;

                if (EndEventTime < Time.time)
                {
                    Destroy();
                    return;
                }

                if (config.FireMlrs && MlrsEventTime < Time.time && !EventRaidManager.isFireingMlrs && MlrsEventTime != 0)
                {
                    MlrsEventTime = 0;
                    setupMlrs(this);
                }

                if (config.TotalNpcWaves > 0 && totalWaves <= config.TotalNpcWaves && nextWaveTime < Time.time)
                {
                    nextWaveTime = Time.time + config.NextWaveSeconds;
                    if (QueuedRoutine != null)
                        stopSpawning();
                    QueuedRoutine = ServerMgr.Instance.StartCoroutine(GenerateEventMembers());
                    return;
                }

                if ((totalWaves - 1) == config.TotalNpcWaves && members.Count <= 0)
                {
                    Destroy(true);
                    return;
                }

                if ((totalWaves - 1) < config.TotalNpcWaves)
                {
                    if (!notify && (nextWaveTime - Time.time) <= 30)
                    {
                        notify = true;
                        notifyWaveTime = totalWaves;
                        notifyWave();
                    }
                    else if (!notify60 && (nextWaveTime - Time.time) <= 60 )
                    {
                        notify60 = true;
                        notifyWaveTime = totalWaves;
                        notifyWave();
                    }
                    else if (notifyWaveTime != totalWaves && members.Count <= 0)
                    {
                        notifyWaveTime = totalWaves;
                        notifyWave();
                    }
                }
                if (!notifyEnd && (EndEventTime - 60) <= Time.time)
                {
                    notifyEnd = true;
                    string EndTime = _.FormatTime((long)(EndEventTime - Time.time));
                    for (int i = authorizedPlayers.Count - 1; i >= 0; i--)
                    {
                        id = authorizedPlayers[i];
                        if (id != 0UL)
                        {
                            BasePlayer TCplayer = BasePlayer.FindByID(id);
                            if (TCplayer != null)
                            {
                                if (_.configData.settings.gtip)
                                    GameTipsMessage(TCplayer, string.Format(_.lang.GetMessage("EventEndINTime", _, player.UserIDString), EndTime));
                                if (_.configData.settings.ctip)
                                    _.SendReply(TCplayer, _.lang.GetMessage("EventEndINTime", _, player.UserIDString), EndTime);
                            }
                        }
                    }
                }
                else if (!notifyEnd1 && (EndEventTime - 30) <= Time.time)
                {
                    notifyEnd1 = true;
                    string EndTime = _.FormatTime((long)(EndEventTime - Time.time));
                    for (int i = authorizedPlayers.Count - 1; i >= 0; i--)
                    {
                        id = authorizedPlayers[i];
                        if (id != 0UL)
                        {
                            BasePlayer TCplayer = BasePlayer.FindByID(id);
                            if (TCplayer != null)
                            {
                                if (_.configData.settings.gtip)
                                    GameTipsMessage(TCplayer, string.Format(_.lang.GetMessage("EventEndINTime", _, player.UserIDString), EndTime));
                                if (_.configData.settings.ctip)
                                    _.SendReply(TCplayer, _.lang.GetMessage("EventEndINTime", _, player.UserIDString), EndTime);
                            }
                        }
                    }
                }
            }

            internal bool notify { get; set; }
            internal bool notify60 { get; set; }
            internal bool notifyEnd { get; set; }
            internal bool notifyEnd1 { get; set; }
            internal int notifyWaveTime = 0;

            internal void notifyWave()
            {
                ulong id = 0UL;
                string waveWaitTime = _.FormatTime((long)(nextWaveTime - Time.time));
                for (int i = authorizedPlayers.Count - 1; i >= 0; i--)
                {
                    id = authorizedPlayers[i];
                    if (id != 0UL)
                    {
                        BasePlayer TCplayer = BasePlayer.FindByID(id);
                        if (TCplayer != null)
                        {
                            if (_.configData.settings.gtip)
                                GameTipsMessage(TCplayer, string.Format(_.lang.GetMessage("ArivalTimeNext", _, player.UserIDString), config.TotalNpcWaves + 1, totalWaves + 1, waveWaitTime), 3f);
                            if (_.configData.settings.ctip)
                                _.SendReply(TCplayer, _.lang.GetMessage("ArivalTimeNext", _, player.UserIDString), config.TotalNpcWaves + 1, totalWaves + 1, waveWaitTime);
                        }
                    }
                }
            }

            internal void RemoveNpc(NpcRaider player)
            {
                if (members != null && members.Contains(player))
                    members.Remove(player);
                if (_AllMembers != null && _AllMembers.Contains(player))
                    _AllMembers.Remove(player);
            }

            internal void Destroy(bool win = false)
            {
                if (isDestroyed)
                    return;
                ulong id = 0UL; 
                active = false;
                isDestroyed = true;
                MlrsEventTime = 1;
                Interface.Oxide.CallHook("NpcRaidersEventEnd", player, playerID, waveType, location);
                stopSpawning();
                
                for (int i = members.Count - 1; i >= 0; i--)
                {
                    NpcRaider npc = members[i];
                    if (npc != null && !npc.IsDestroyed && !npc.isMounted)
                    {
                        npc.Kill();
                    }
                }

                for (int i = authorizedPlayers.Count - 1; i >= 0; i--)
                {
                    id = authorizedPlayers[i];
                    if (id != 0UL)
                    {
                        BasePlayer TCplayer = BasePlayer.FindByID(id);
                        if (TCplayer != null)
                        {
                            if (win)
                            {
                                if (_.configData.settings.ctip)
                                    _.SendReply(TCplayer, _.lang.GetMessage("eventOverWin", _, TCplayer.UserIDString));
                                if (_.configData.settings.gtip)
                                    GameTipsMessage(TCplayer, _.lang.GetMessage("eventOverWin", _, TCplayer.UserIDString), 3f);
                                if (!_.statsData.pEntity.ContainsKey(id))
                                    _.statsData.pEntity.Add(id, new StatsInfo() { name = TCplayer.displayName });
                                _.statsData.pEntity[id].played++;
                                _.statsData.pEntity[id].won++;
                                _.SaveStats();
                            }
                            else
                            {
                                if (_.configData.settings.ctip)
                                    _.SendReply(TCplayer, _.lang.GetMessage("eventOver", _, TCplayer.UserIDString));
                                if (_.configData.settings.gtip)
                                    GameTipsMessage(TCplayer, _.lang.GetMessage("eventOver", _, TCplayer.UserIDString), 3f);
                                if (!_.statsData.pEntity.ContainsKey(id))
                                    _.statsData.pEntity.Add(id, new StatsInfo() { name = TCplayer.displayName });
                                _.statsData.pEntity[id].played++;
                                _.statsData.pEntity[id].lost++;
                                _.SaveStats();
                            }
                        }
                    }
                }

                if (win && player != null)
                {
                    if (_.configData.raidRewardOptions.RewardOptions.ContainsKey(waveType))
                    {
                        rewardPlayers(_.configData.raidRewardOptions.RewardOptions[waveType]);
                    }
                }

                foreach (ulong privey in authorizedPlayers.ToList())
                {
                    if (_AllPlayers.Contains(privey))
                        _AllPlayers.Remove(privey);
                }
                if (thetimerObject != null)
                {
                    UnityEngine.GameObject.DestroyImmediate(thetimerObject, true);
                }

                _.NextTick(() =>
                {
                    members.Clear();
                    Pool.FreeList(ref members);
                    authorizedPlayers.Clear();
                    Pool.FreeList(ref authorizedPlayers);
                    _AllRaids.Remove(this);
                });
            }

            internal void rewardPlayers(raidReward reward)
            {
                ItemDefinition def = null;
                var ids = default(int);
                object totals = reward.RewardAmmount;
                List<string> kitList = new List<string>();
                if (int.TryParse(reward.RewardType, out ids))
                {
                    def = ItemManager.FindItemDefinition(ids);
                }
                if (def != null)
                {
                    if (reward.rewardAll)
                    {
                        foreach (var playerid in authorizedPlayers.ToList())
                        {
                            BasePlayer RewardPlayer = BasePlayer.FindAwakeOrSleeping(playerid.ToString());
                            if (RewardPlayer == null || !RewardPlayer.IsConnected)
                                continue;

                            giveItem(RewardPlayer, ids, Convert.ToInt32(totals));
                            _.SendReply(RewardPlayer, String.Format(_.lang.GetMessage("RewardedItem", _), Convert.ToInt32(totals), def.displayName.english));
                        }
                    }

                    else if (player != null)
                    {
                        giveItem(player, ids, Convert.ToInt32(totals));
                        _.SendReply(player, String.Format(_.lang.GetMessage("RewardedItem", _), Convert.ToInt32(totals), def.displayName.english));
                    }
                }
                else if (def == null)
                {
                    int totalPlayers = 0;
                    List<BasePlayer> onlinePlayer = new List<BasePlayer>();
                    object totalsSplit = 0;

                    foreach (var playerid in authorizedPlayers.ToList())
                    {
                        BasePlayer RewardPlayer = BasePlayer.FindAwakeOrSleeping(playerid.ToString());
                        if (RewardPlayer == null || !RewardPlayer.IsConnected)
                            continue;
                        totalPlayers++;
                        onlinePlayer.Add(RewardPlayer);
                    }

                    if (reward.rewardAll && totalPlayers > 1)
                    {
                        foreach (var RewardPlayer in onlinePlayer.ToList())
                        {
                            if (RewardPlayer == null || !RewardPlayer.IsConnected)
                                continue;

                            if (reward.RewardType.ToLower() == "serverrewards")
                            {
                                totalsSplit = ((Convert.ToInt32(totals)) / totalPlayers);
                                _.ServerRewards?.Call("AddPoints", RewardPlayer.userID, Convert.ToInt32(totalsSplit));
                                _.SendReply(RewardPlayer, String.Format(_.lang.GetMessage("MoneyIssued", _), Convert.ToInt32(totalsSplit), RewardPlayer.displayName));
                            }
                            else if (reward.RewardType.ToLower() == "economics")
                            {
                                totalsSplit = ((Convert.ToInt32(totals)) / totalPlayers);
                                _.Economics?.Call("Deposit", RewardPlayer.userID, Convert.ToDouble(totalsSplit));
                                _.SendReply(RewardPlayer, String.Format(_.lang.GetMessage("MoneyIssued", _), Convert.ToDouble(totalsSplit), RewardPlayer.displayName));
                            }
                            else if (reward.RewardType.ToLower() == "kits" || reward.RewardType.ToLower() == "kit")
                            {
                                kitList = getObjects(totals);
                                if (totals is string)
                                {
                                    if (!string.IsNullOrEmpty(Convert.ToString(totals)))
                                    {
                                        object success = _.Kits?.Call("GiveKit", RewardPlayer, Convert.ToString(totals));
                                        if (success == null)
                                            _.PrintWarning($"Unable to find a kit with the name {Convert.ToString(totals)}");
                                        else if (success is string)
                                        {
                                            _.SendReply(RewardPlayer, (string)success, _);
                                            _.PrintWarning($"Ops {(string)success}");
                                        }
                                    }
                                    else _.PrintWarning($"Invalid kitname in RewardAmmount");
                                }
                                else if (kitList != null)
                                {
                                    if (kitList.Count <= 0) continue;
                                    string rand = kitList.GetRandom();
                                    object success = _.Kits?.Call("GiveKit", RewardPlayer, rand);
                                    if (success == null)
                                        _.PrintWarning($"Unable to find a kit with the name {rand}");
                                    else if (success is string)
                                    {
                                        _.SendReply(RewardPlayer, (string)success, _);
                                        _.PrintWarning($"Ops {(string)success}");
                                    }
                                }
                            } 
                        }
                    }
                    else
                    {
                        if (player != null)
                        {
                            if (reward.RewardType.ToLower() == "serverrewards")
                            {
                                _.ServerRewards?.Call("AddPoints", player.userID, Convert.ToInt32(totals));
                                _.SendReply(player, String.Format(_.lang.GetMessage("MoneyIssued", _), Convert.ToInt32(totals), player.displayName));
                            }
                            else if (reward.RewardType.ToLower() == "economics")
                            {
                                _.Economics?.Call("Deposit", player.userID, Convert.ToDouble(totals));
                                _.SendReply(player, String.Format(_.lang.GetMessage("MoneyIssued", _), Convert.ToDouble(totals), player.displayName));
                            }
                            else if (reward.RewardType.ToLower() == "kits" || reward.RewardType.ToLower() == "kit")
                            {
                                kitList = getObjects(totals);
                                if (totals is string)
                                {
                                    if (!string.IsNullOrEmpty(Convert.ToString(totals)))
                                    {
                                        object success = _.Kits?.Call("GiveKit", player, Convert.ToString(totals));
                                        if (success == null)
                                            _.PrintWarning($"Unable to find a kit with the name {Convert.ToString(totals)}");
                                        else if (success is string)
                                        {
                                            _.SendReply(player, (string)success, _);
                                            _.PrintWarning($"Ops {(string)success}");
                                        }
                                    }
                                    else _.PrintWarning($"Invalid kitname in RewardAmmount");
                                }
                                else if (kitList != null && kitList.Count > 0)
                                {
                                    if (kitList.Count <= 0) return;
                                    string rand = kitList.GetRandom();

                                    object success = _.Kits?.Call("GiveKit", player, rand);
                                    if (success == null)
                                        _.PrintWarning($"Unable to find a kit with the name {rand}");
                                    else if (success is string)
                                    {
                                        _.SendReply(player, (string)success, _);
                                        _.PrintWarning($"Ops {(string)success}");
                                    }
                                }
                            }
                        }
                    }
                }
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
            internal static void giveItem(BasePlayer RPlayer, int itemID, int total)
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

            internal void stopSpawning()
            {
                if (QueuedRoutine != null)
                    ServerMgr.Instance.StopCoroutine(QueuedRoutine);
                QueuedRoutine = null;
                float newTime = config.NextWaveSeconds + (config.TotalNpcs * 0.1f) + 12f;
                nextWaveTime = Time.time + newTime;
                totalWaves++;
                notify = false;
                notify60 = false;
            }

            internal NpcRaider hookSpawnMember(Vector3 position, bool mounted = false, bool driver = false)
            {
                NpcRaider theNewGuy = SpawnNPC(position);
                if (theNewGuy != null)
                {
                    members.Add(theNewGuy);
                    return theNewGuy;
                }
                return null;
            }
            internal IEnumerator GenerateEventMembers()
            {
                Interface.Oxide.CallHook("NpcRaidersGenerateEventMembers", player, playerID, waveType, totalWaves, location, this);
                ulong id = 0UL;
                if (newWave)
                {
                    for (int i = authorizedPlayers.Count - 1; i >= 0; i--)
                    {
                        id = authorizedPlayers[i];
                        if (id != 0UL)
                        {
                            BasePlayer TCplayer = BasePlayer.FindByID(id);
                            if (TCplayer != null && TCplayer.IsConnected)
                            {
                                if (_.configData.settings.gtip)
                                    GameTipsMessage(TCplayer, _.lang.GetMessage("start", _, player.UserIDString));
                                if (_.configData.settings.ctip)
                                    _.SendReply(TCplayer, _.lang.GetMessage("start", _, player.UserIDString));
                            }
                        }
                    }
                    _.PrintWarning($"EventStarted for {player.displayName}");
                }
                else
                {
                    for (int i = authorizedPlayers.Count - 1; i >= 0; i--)
                    {
                        id = authorizedPlayers[i];
                        if (id != 0UL)
                        {
                            BasePlayer TCplayer = BasePlayer.FindByID(id);
                            if (TCplayer != null && TCplayer.IsConnected)
                            {
                                if (_.configData.settings.ctip)
                                    _.SendReply(TCplayer, _.lang.GetMessage("nextWave", _, TCplayer.UserIDString));
                                if (_.configData.settings.gtip)
                                    GameTipsMessage(TCplayer, _.lang.GetMessage("nextWave", _, TCplayer.UserIDString), 3f);
                                if (!_.statsData.pEntity.ContainsKey(id))
                                    _.statsData.pEntity.Add(id, new StatsInfo() { name = TCplayer.displayName });
                                _.statsData.pEntity[id].played++;
                            }
                        }
                    }
                    _.SaveStats();
                }
                newWave = false;
                int totalNpc = 0;
                float spawnDictance = 40;
                if (config == null)
                {
                    stopSpawning();
                }
                List<Vector3> positions = generateSpawnList(location, spawnDictance, 200 / config.TotalNpcs);
                yield return CoroutineEx.waitForSeconds(5f);
                while (positions.Count < config.TotalNpcs)
                {
                    positions = generateSpawnList(location, spawnDictance, 2f);
                    spawnDictance = spawnDictance + 20f;
                    _.PrintWarning($"Not enough spawn positions increasing radius for building to {spawnDictance}");
                    yield return CoroutineEx.waitForSeconds(5f);
                }
                yield return CoroutineEx.waitForSeconds(1f);
                while (totalNpc < config.TotalNpcs)
                {
                    Vector3 position = positions.GetRandom();
                    positions.Remove(position);
                    NpcRaider theNewGuy = SpawnNPC(position);
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
                /* float newTime = config.NextWaveSeconds + (config.TotalNpcs * 0.1f) + 12f;
                   nextWaveTime = Time.time + newTime;
                   totalWaves++; 
                   raidernewID = "";*/
                stopSpawning();
            }

            #region Spawning
            internal NpcRaider SpawnNPC(Vector3 pos)
            {
                pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                object position = FindPointOnNavmesh(pos, 10f);
                if (position is Vector3 && (Vector3)position != Vector3.zero)
                {
                    NpcRaider scientistNPC = InstantiateEntity((Vector3)position, Quaternion.Euler(0, 0, 0));

                    if (scientistNPC == null) return null;

                    scientistNPC.enableSaving = false;
                    scientistNPC.Spawn();
                    //  _allNPC.Add(scientistNPC);
                    scientistNPC.gameObject.SetActive(true);
                    scientistNPC.manager = this;
                    _.NextTick(() =>
                    {
                        scientistNPC.InitNewSettings(location, config.Health);
                        if (config.kits.Count > 0)
                            scientistNPC.setUpGear(config.kits.GetRandom());
                        else scientistNPC.setUpGear("");
                    });
                    if (config.NpcNames.Count > 0)
                        scientistNPC.displayName = config.NpcNames.GetRandom();
                    else scientistNPC.displayName = "A Raiding Npc";

                    if (scientistNPC != null)
                        return scientistNPC;
                }

                return null;
            }

            private static NpcRaider InstantiateEntity(Vector3 position, Quaternion rotation)
            {
                GameObject gameObject = Instantiate.GameObject(GameManager.server.FindPrefab("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab"), position, Quaternion.identity);
                gameObject.name = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab";
                gameObject.SetActive(false);
                ScientistNPC scientistNPC = gameObject.GetComponent<ScientistNPC>();
                ScientistBrain defaultBrain = gameObject.GetComponent<ScientistBrain>();

                defaultBrain._baseEntity = scientistNPC;

                NpcRaider component = gameObject.AddComponent<NpcRaider>();
                NpcRaiderBrain brains = gameObject.AddComponent<NpcRaiderBrain>();
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

            private static List<Vector3> GetCircumferencePositions(Vector3 center, float radius, float next, float y) // Thanks to ArenaWallGenerator
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

            internal static object FindPointOnNavmesh(Vector3 targetPosition, float maxDistance = 4f)
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

            internal static bool IsInRockPrefab(Vector3 position)
            {
                Physics.queriesHitBackfaces = true;

                bool isInRock = Physics.Raycast(position, Vector3.up, out raycastHit, 20f, WORLD_LAYER, QueryTriggerInteraction.Ignore) &&
                                blockedColliders.Any(s => raycastHit.collider?.gameObject?.name.Contains(s) ?? false);

                Physics.queriesHitBackfaces = false;

                return isInRock;
            }

            internal static bool IsInRockPrefab2(Vector3 position)
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
                            if (isRock) return true;
                        }
                    }
                }
                return false;
            }

            internal static bool IsNearWorldCollider(Vector3 position)
            {
                Physics.queriesHitBackfaces = true;

                int count = Physics.OverlapSphereNonAlloc(position, 2f, _buffer, WORLD_LAYER, QueryTriggerInteraction.Ignore);
                Physics.queriesHitBackfaces = false;

                int removed = 0;
                for (int i = 0; i < count; i++)
                {
                    if (acceptedColliders.Any(s => _buffer[i].gameObject.name.Contains(s)))
                        removed++;
                }


                return count - removed > 0;
            }

            private static readonly string[] acceptedColliders = new string[] { "road", "carpark", "rocket_factory", "range", "train_track", "runway", "_grounds", "concrete_slabs", "lighthouse", "cave", "office", "walkways", "sphere", "tunnel", "industrial", "junkyard" };

            private static readonly string[] blockedColliders = new string[] { "rock", "junk", "range", "invisible", "cliff", "prevent_movement", "formation_" };
            #endregion

            #region GetingRaidBuilding

            internal void findNewBuilding(NpcRaider raider)
            {
                if (player != null)
                {
                    building = GetBuildingPrivilege(config.ExternalTcRange);
                    if (building == null)
                    {
                        Destroy();
                    }
                    else
                    {
                        raider.currentTargetOverride = building;
                        foreach (var user in building.authorizedPlayers.ToList())
                        {
                            if (!authorizedPlayers.Contains(user.userid))
                                authorizedPlayers.Add(user.userid);
                            if (config.canNotRepair && !_AllPlayers.Contains(user.userid))
                                _AllPlayers.Add(user.userid);
                        }
                    }
                }
                else
                {
                    Destroy();
                }
            }

            public BuildingPrivlidge GetBuildingPrivilege(float radius = 0f)
            {
                if (player == null || radius <= 0) return null;
                BuildingBlock buildingBlock1 = (BuildingBlock)null;
                BuildingPrivlidge buildingPrivlidge = (BuildingPrivlidge)null;
                System.Collections.Generic.List<BuildingBlock> list = Facepunch.Pool.GetList<BuildingBlock>();
                Vis.Entities<BuildingBlock>(location, radius, list, 2097152);
                for (int index = 0; index < list.Count; ++index)
                {
                    BuildingBlock buildingBlock2 = list[index];
                    if (buildingBlock2.IsOlderThan((BaseEntity)buildingBlock1) && Vector3.Distance(location, buildingBlock2.transform.position) <= 102.0)
                    {
                        BuildingManager.Building building = buildingBlock2.GetBuilding();
                        if (building != null)
                        {

                            BuildingPrivlidge buildingPrivilege = building.GetDominatingBuildingPrivilege();
                            if (!((UnityEngine.Object)buildingPrivilege == (UnityEngine.Object)null))
                            {
                                if (buildingPrivilege.IsAuthed(player))
                                {
                                    buildingBlock1 = buildingBlock2;
                                    buildingPrivlidge = buildingPrivilege;
                                }
                            }
                        }
                    }
                }
                Facepunch.Pool.FreeList<BuildingBlock>(ref list);
                return buildingPrivlidge;
            }
            #endregion
        }
        #endregion

        #region NpcRaider
        public class NpcRaider : NPCPlayer, IAISenses, IAIAttack, IThinker
        {
            public int AdditionalLosBlockingLayer;
            public LootContainer.LootSpawnSlot[] LootSpawnSlots;
            public float aimConeScale = 2f;
            public float lastDismountTime;
            [NonSerialized]
            protected bool lightsOn;
            private float nextZoneSearchTime;
            private AIInformationZone cachedInfoZone;
            private float targetAimedDuration;
            private float lastAimSetTime;

            private Vector3 aimOverridePosition = Vector3.zero;

            public EventRaidManager manager { get; set; }

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

            public NpcRaiderBrain Brain { get; private set; }

            public override bool IsLoadBalanced() => true;

            public bool HasTarget() => (currentTarget != null);

            public bool HasTargetOverride() => (currentTargetOverride != null);

            public List<ulong> DontIgnorePlayers = new List<ulong>();

            public override void ServerInit()
            {
                if (NavAgent == null)
                    NavAgent = GetComponent<NavMeshAgent>();

                NavAgent.areaMask = 1;
                NavAgent.agentTypeID = -1372625422;

                GetComponent<BaseNavigator>().DefaultArea = "Walkable";

                base.ServerInit();
                Brain = this.GetComponent<NpcRaiderBrain>();

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

                this.startHealth = health;
                InitializeHealth(health, health);
                this.health = health;

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
                    aimConeScale = manager.config.aimConeScale;
                });
            }

            public void setUpGear(string kitname = "")
            {
				if (this.IsDestroyed) return;
                this.inventory.containerBelt.capacity = 8;
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
                        ItemManager.CreateByName("rocket.launcher").MoveToContainer(this.inventory.containerBelt, 6);
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

                        if (_attackEntity is BaseProjectile)
                            this.UpdateActiveItem(weapon.uid);

                        else this.UpdateActiveItem(weapon.uid);

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
                if (this.IsDestroyed) return null;
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
                    if (currentTargetOverride == null)
                    {
                        currentTargetOverride = GetBuilding();
                        if (currentTargetOverride != null)
                        {
                            DontIgnorePlayers.AddRange(manager.authorizedPlayers);
                            HomePosition = currentTargetOverride.transform.position;
                        }
                        if (debug) _.PrintWarning("set raid target");
                        return;
                    }
                }
            }

            private BaseCombatEntity GetBuilding()
            {
                if (manager == null || this.IsDestroyed) return null;
                if (manager.building == null)
                {
                    manager.findNewBuilding(this);
                    if (manager.building == null)
                        return null;
                }

                return manager.building;
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

                    if (player is NpcRaider)
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
                    return IsPlayerVisibleToUs(baseCombatEntity as BasePlayer, eyes.worldStandingPosition, 1218519041);
                else return IsVisible(baseCombatEntity.CenterPoint(), eyes.worldStandingPosition, float.PositiveInfinity);
            }

            private bool ShouldIgnorePlayer(BasePlayer player)
            {
                if (player == null || player.IsDead()) return true;
                if (manager != null && manager.authorizedPlayers != null && player.userID != null)
                {
                    if (manager.authorizedPlayers.Contains(player.userID))
                        return false;
                    return true;
                }
                return false;
            }

            public bool TargetInThrowableRange()
            {
                if (currentTargetOverride == null)
                    return false;
                return !hasWall();   //return Vector3.Distance(targetEntity.transform.position, Entity.transform.position) <= 7.5f;
            }

            public bool hasWall()
            {
                // var ray = new Ray(Entity.eyes.position, Entity.eyes.HeadForward());
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

            public override void EquipWeapon(bool skipDeployDelay = false) => base.EquipWeapon(skipDeployDelay);

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

            internal void Internal()
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
                    if (initiator is BasePlayer && !manager.authorizedPlayers.Contains((initiator as BasePlayer).userID))
                    {
                        GameTips((initiator as BasePlayer));
                        return;
                    }
                    if (_.configData.settings.damageDelay > 0 && Brain != null && (Brain.spawnAge + _.configData.settings.damageDelay) > Time.time)
                        return;
                }
                base.Hurt(info);
                if (initiator == null || initiator.EqualNetID((BaseNetworkable)this))
                    return;
                this.Brain.Senses.Memory.SetKnown(initiator, (BaseEntity)this, (AIBrainSenses)null);
                lastTargetTime = Time.time + Brain.Senses.MemoryDuration;
                lastTargetPosition = initiator.transform.position;
                if (initiator is BasePlayer && manager.authorizedPlayers.Contains((initiator as BasePlayer).userID))
                {
                    lastDamagePlayer = initiator as BasePlayer;
                }
            }

            public static void GameTips(BasePlayer player)
            {
                if (player != null && player.userID.IsSteamId())
                {
                    player?.SendConsoleCommand("gametip.hidegametip");
                    player?.SendConsoleCommand("gametip.showgametip", "You can not hurt this Npc.");
                    _.timer.Once(2f, () => player?.SendConsoleCommand("gametip.hidegametip"));
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
                if (lastDamagePlayer != null && lastDamagePlayer.userID.IsSteamId())
                {
                    if (!_.statsData.pEntity.ContainsKey(lastDamagePlayer.userID))
                        _.statsData.pEntity.Add(lastDamagePlayer.userID, new StatsInfo() { name = lastDamagePlayer.displayName });
                    _.statsData.pEntity[lastDamagePlayer.userID].killed++;
                }


                NPCPlayerCorpse npcPlayerCorpse = this.DropCorpse("assets/prefabs/npc/scientist/scientist_corpse.prefab") as NPCPlayerCorpse;
                if (npcPlayerCorpse != null)
                {
                    npcPlayerCorpse.transform.position = npcPlayerCorpse.transform.position + Vector3.down * .05f;
                    npcPlayerCorpse.SetLootableIn(2f);
                    npcPlayerCorpse.SetFlag(BaseEntity.Flags.Reserved5, this.HasPlayerFlag(BasePlayer.PlayerFlags.DisplaySash));
                    npcPlayerCorpse.SetFlag(BaseEntity.Flags.Reserved2, true);
                    npcPlayerCorpse.TakeFrom(this, this.inventory.containerMain, this.inventory.containerWear, this.inventory.containerBelt);
                    npcPlayerCorpse.playerName = this.OverrideCorpseName();
                    npcPlayerCorpse.playerSteamID = this.userID;
                    npcPlayerCorpse.Spawn();
                    npcPlayerCorpse.TakeChildren((BaseEntity)this);
                    foreach (ItemContainer container in npcPlayerCorpse.containers)
                        container.Clear();

                    global::HumanNPC theNPC = GameManager.server.FindPrefab("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roamtethered.prefab").GetComponent<global::HumanNPC>();
                    LootSpawnSlots = theNPC.LootSpawnSlots;

                    if (manager != null && manager.config.NpcDropLoot)
                    {
                        List<ulong> copyList = new List<ulong>();
                        copyList.AddRange(manager.authorizedPlayers);
                        _.corpsLock.Add(npcPlayerCorpse.playerSteamID, copyList);
                        if (manager.config.NpcDropLootConfig.Count > 0)
                        {
                            string rand = manager.config.NpcDropLootConfig.GetRandom();
                            if (HasSaveFile(rand))
                            {
                                LootEntity newFile = null;
                                LoadLootData(out newFile, _.Name + "/LootProfiles/" + rand);

                                if (npcPlayerCorpse != null && newFile != null)
                                {
                                    int total = UnityEngine.Random.Range(newFile.MinItems, newFile.MaxItems);
                                    for (int j = 0; j < total; j++)
                                    {
                                        itemInfo itemNew = newFile.items.GetRandom();
                                        string item = itemNew.item;
                                        int amount = UnityEngine.Random.Range(itemNew.amountMin, itemNew.amountMax);
                                        ulong skin = itemNew.skin;
                                        string location = itemNew.location;
                                        if (amount == 0) amount = 1;

                                        Item x = ItemManager.CreateByName(item, amount, skin);
                                        if (x != null)
                                        {
                                            if (location == "containerMain")
                                                x.MoveToContainer(npcPlayerCorpse.containers[0], -1);
                                            else if (location == "containerBelt")
                                                x.MoveToContainer(npcPlayerCorpse.containers[1], -1);
                                            else if (location == "containerWear")
                                                x.MoveToContainer(npcPlayerCorpse.containers[2], -1);
                                            if (!String.IsNullOrEmpty(itemNew.name))
                                                x.name = itemNew.name;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (this.LootSpawnSlots != null && this.LootSpawnSlots.Length != 0)
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
                            }
                        }
                        else
                        {
                            if (this.LootSpawnSlots != null && this.LootSpawnSlots.Length != 0)
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
                        }
                    }
                    else removeCorpse(npcPlayerCorpse);
                }

                return (BaseCorpse)npcPlayerCorpse;
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
                return this.AdditionalLosBlockingLayer == 0 ? this.IsPlayerVisibleToUs(otherPlayer, eyes.worldStandingPosition, 1218519041) : this.IsPlayerVisibleToUs(otherPlayer, eyes.worldStandingPosition, 1218519041 | 1 << this.AdditionalLosBlockingLayer);
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

            private bool ShouldIgnoreDamage(BasePlayer player)
            {
                if (player == null)
                    return true;

                if (manager.playerID == player.userID)
                    return false;

                if (manager.building != null && manager.authorizedPlayers.Contains(player.userID))
                    return false;

                return true;
            }

            internal bool ShouldIgnoreBuildingDamage(BaseCombatEntity entity)
            {
                if (entity == null)
                    return true;

                BuildingPrivlidge Bprivilege = entity.GetBuildingPrivilege();
                if (Bprivilege == null)
                {
                    return false;
                }
                if (manager.building != null && manager.authorizedPlayers.Count > 0)
                    foreach (var userID in manager.authorizedPlayers.ToList())
                        if (Bprivilege.IsAuthed(userID) || entity.OwnerID == userID)
                            return false;
                return true;
            }
        }
        #endregion

        #region Brain
        public class NpcRaiderBrain : BaseAIBrain
        {
            private NpcRaider entityAI;
            public float spawnAge { get; private set; }
            NpcRaider humanNpc = null;

            private void Awake()
            {
                entityAI = GetComponent<NpcRaider>();
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
                humanNpc = GetBaseEntity().GetComponent<NpcRaider>();

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
                        SwitchToState(stateKey, -1);  //fix
                }
            }

            public class IdleState : BasicAIState
            {
                private readonly NpcRaider entityAI;
                private float nextStrafeTime;

                public IdleState(NpcRaider entityAI) : base(AIState.Idle)
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
                private readonly NpcRaider entityAI;
                private IAIAttack attack;
                private float nextStrafeTime;

                public CombatState(NpcRaider entityAI) : base(AIState.Cooldown)
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
                }


                private void StopAttacking() => this.attack.StopAttacking();

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    this.brain.Navigator.ClearFacingDirectionOverride();
                    this.brain.Navigator.Stop();
                    this.StopAttacking();
                    //	entityAI.SetDucked(false);
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
                private readonly NpcRaider entityAI;
                private float nextStrafeTime;
                private float nextFireTime;
                private Vector3 position;
                private Vector3 position2;
                private bool isFireing;
                public BaseLauncherState(NpcRaider entityAI) : base(AIState.Attack)
                {
                    this.entityAI = entityAI;
                }

                public AIState GetState() => AIState.Attack;

                public override float GetWeight()
                {
                    if (entityAI != null && !entityAI.HasTarget() && entityAI.currentTargetOverride != null)
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
                    entityAI.EquipNewWeapon(6);
                    base.StateEnter(brain, entity);
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
                    if (nextStrafeTime < Time.time)
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
                            isFireing = true;
                            nextFireTime = Time.time + UnityEngine.Random.Range(5f, 10f);
                            entityAI.Brain.Navigator.Stop();
                            entityAI.StartCoroutine(WeaponFireTest(position));
                            nextStrafeTime = Time.time + 1.2f;
                            isFireing = false;
                        }
                    }

                    return StateStatus.Running;
                }

                public IEnumerator WeaponFireTest(Vector3 locations, bool rocket = true)
                {
                    entityAI.Brain.Navigator.SetFacingDirectionOverride((locations - entityAI.transform.position).normalized);
                    yield return CoroutineEx.waitForSeconds(0.1f);
                    if (entityAI == null || entityAI.IsDestroyed)
                        ServerMgr.Instance.StopCoroutine(WeaponFireTest(locations, rocket));
                    rocket = entityAI.TargetInThrowableRange();
                    if (rocket)
                    {
                        HeldEntity heldEntity = entityAI.inventory.containerBelt.GetSlot(6)?.GetHeldEntity() as HeldEntity;
                        entityAI.EquipNewWeapon(6);
                        entityAI.UpdateActiveItem(entityAI.inventory.containerBelt.GetSlot(6).uid);
                        entityAI.inventory.UpdatedVisibleHolsteredItems();
                        if (heldEntity != null && heldEntity is BaseLauncher)
                        {
                            entityAI.Brain.Navigator.SetFacingDirectionOverride((locations - entityAI.transform.position).normalized);
                            entityAI.SetAimDirection((locations - entityAI.transform.position).normalized);
                            yield return CoroutineEx.waitForSeconds(0.9f);
                            if (entityAI == null || entityAI.IsDestroyed)
                                ServerMgr.Instance.StopCoroutine(WeaponFireTest(locations, rocket));
                            fireRocket((heldEntity as BaseProjectile), locations);
                        }
                        yield return CoroutineEx.waitForSeconds(0.5f);
                        if (entityAI == null || entityAI.IsDestroyed)
                            ServerMgr.Instance.StopCoroutine(WeaponFireTest(locations, rocket));
                        if (entityAI._ProjectilesSlot.Count > 0)
                            entityAI.EquipNewWeapon(entityAI._ProjectilesSlot.GetRandom());
                        else if (entityAI._MeleWeaponSlot.Count > 0)
                            entityAI.EquipNewWeapon(entityAI._MeleWeaponSlot.GetRandom());
                        entityAI.Brain.Navigator.ClearFacingDirectionOverride();
                        ServerMgr.Instance.StopCoroutine(WeaponFireTest(locations, rocket));
                    }
                    else
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
                                    ServerMgr.Instance.StopCoroutine(WeaponFireTest(locations, rocket));
                                }
                                ServerThrow(locations, _throwableWeapon);

                                yield return CoroutineEx.waitForSeconds(0.5f);
                            }
                        }
                        if (entityAI == null || entityAI.IsDestroyed)
                            ServerMgr.Instance.StopCoroutine(WeaponFireTest(locations, rocket));
                        if (entityAI._ProjectilesSlot.Count > 0)
                            entityAI.EquipNewWeapon(entityAI._ProjectilesSlot.GetRandom());
                        else if (entityAI._MeleWeaponSlot.Count > 0)
                            entityAI.EquipNewWeapon(entityAI._MeleWeaponSlot.GetRandom());
                        entityAI.Brain.Navigator.ClearFacingDirectionOverride();
                        ServerMgr.Instance.StopCoroutine(WeaponFireTest(locations, rocket));
                    }

                }

                private void fireRocket(BaseProjectile _ProectileWeapon, Vector3 locations)
                {
                    Vector3 vector3 = _ProectileWeapon.MuzzlePoint.transform.forward;
                    Vector3 position = _ProectileWeapon.MuzzlePoint.transform.position + (Vector3.up * 1.6f);
                    BaseEntity rocket = null;
                    rocket = GameManager.server.CreateEntity($"assets/prefabs/ammo/rocket/rocket_basic.prefab", position, entityAI.eyes.GetLookRotation());
                    if (rocket == null) return;
                    var proj = rocket.GetComponent<ServerProjectile>();
                    if (proj == null) return;
                    rocket.creatorEntity = entityAI;
                    proj.InitializeVelocity(Quaternion.Euler(vector3) * rocket.transform.forward * 35f);
                    TimedExplosive rocketExplosion = rocket.GetComponent<TimedExplosive>();
                    rocketExplosion.timerAmountMin = _.configData.settings.RocketExplodeTime;
                    rocketExplosion.timerAmountMax = _.configData.settings.RocketExplodeTime;
                    if (entityAI.manager.config.DamageScale > 0)
                        rocket.SendMessage("SetDamageScale", (object)(float)((double)entityAI.manager.config.DamageScale));
                    rocket.Spawn();
                }

                public void ServerThrow(Vector3 targetPosition, ThrownWeapon wep)
                {
                    if (wep == null) return;
                    Vector3 position = entityAI.eyes.position;
                    Vector3 vector3 = entityAI.eyes.BodyForward();
                    float num1 = 1f;
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
                        dud.dudChance = 0f;
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

        #region Helpers & Commands
        private object IsEventTc(BuildingPrivlidge building)
        {
            if (building != null && EventRaidManager._AllRaids.Count > 0)
            {
                foreach (EventRaidManager e in EventRaidManager._AllRaids)
                {
                    if (e.building != null && e.building == building)
                        return true;
                }
            }

            return null;
        }

        private void ConsolStartRaid(ConsoleSystem.Arg args)
        {
            BasePlayer player;
            if (args == null || args.Args == null || args.Args.Length == 1 || !args.IsServerside)
            {
                SendReply(args, "Usage: npcraiders buy <type> <optional playerID>");
                return;
            }

            player = args.Player();
            string type = args.Args[1];

            if (player == null && args.Args.Length == 3)
            {
                var ids = default(ulong);
                if (ulong.TryParse(args.Args[2], out ids))
                    player = BasePlayer.FindByID(ids);
            }
            
            if (player != null && args.Args[0].ToLower() == "buy" && !string.IsNullOrEmpty(type))
            {
                cmdChatStartRaidBuy(player, type);
            }
            else SendReply(args, "Usage: npcraiders buy <type> <optional playerID>");

        }

        public static void removeCorpse(NPCPlayerCorpse npcPlayerCorpse)
        {
            _.timer.Once(4f, () => { if (npcPlayerCorpse != null && !npcPlayerCorpse.IsDestroyed && npcPlayerCorpse.CanRemove()) npcPlayerCorpse.Kill(); });
        }

        private void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            double timeStamp = GrabCurrentTime();
            if (!pcdData.pEntity.ContainsKey(player.userID))
                pcdData.pEntity.Add(player.userID, new PCDInfo());
            if (!pcdData.pEntity[player.userID].tcCooldown.ContainsKey(privilege.net.ID.Value))
                pcdData.pEntity[player.userID].tcCooldown.Add(privilege.net.ID.Value, 0);
            long coolTime = (long)timeStamp + configData.settings.TcAuthCooldown;
            if (configData.raidVipOptions != null && configData.raidVipOptions.VipOptions.Count > 0)
            {
                foreach (var perm in configData.raidVipOptions.VipOptions)
                    if (permission.UserHasPermission(player.UserIDString, perm.Key))
                        coolTime = (long)timeStamp + perm.Value.TcAuthCooldown;
            }
            pcdData.pEntity[player.userID].tcCooldown[privilege.net.ID.Value] = coolTime;
            SaveData();
        }

        private bool isBlockedTerain(Vector3 position)
        {
            Vector3 p1 = position + new Vector3(0, 200f, 0);
            Vector3 p2 = position + new Vector3(0, -200f, 0);
            Vector3 diff = p2 - p1;
            RaycastHit[] hits;
            hits = Physics.RaycastAll(p1, diff, diff.magnitude, terainLayer);
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

        private string FormatTime(long seconds)
        {
            var timespan = TimeSpan.FromSeconds(seconds);
            return string.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds, System.Math.Floor(timespan.TotalHours));
        }

        private void cmdChatRaidClearDatsa(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, adminPerm))
            {
                PrintWarning($"{player.displayName} is resetting all player data & cooldowns");
                pcdData = new PlayerEntity();
                SendReply(player, "Resetting all player data & cooldowns");
                return;
            }
            else return;
        }

        private void cmdChatRaidReload(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, adminPerm))
            {
                SendReply(player, lang.GetMessage("noPermUse", this, player.UserIDString));
                return;
            }

            switch (args[1].ToLower())
            {
                case "all":
                    foreach (var eventRunning in EventRaidManager._AllRaids)
                        eventRunning.Destroy();

                    EventRaidManager._AllRaids.Clear();
                    SendReply(player, "Killing events. All events have been cancelled.");
                    return;

                case "player":
                    if (args.Length < 3)
                    {
                        SendReply(player, lang.GetMessage("UsageAdmin", this, player.UserIDString));
                        return;
                    }
                    else if (args.Length >= 3)
                    {
                        List<BasePlayer> players = FindPlayers(args[1], true);
                        if (players.Count <= 0)
                        {
                            SendReply(player, lang.GetMessage("PlayerNotFound", this, player.UserIDString));
                            return;
                        }
                        if (players.Count > 1)
                        {
                            SendReply(player, lang.GetMessage("MultiplePlayers", this, player.UserIDString), GetMultiplePlayers(players));
                            return;
                        }
                        if (players.Count == 1)
                        {
                            if (EventList.ContainsKey(players[0]))
                            {
                                SendReply(players[0], lang.GetMessage("EventCanceledByAdmin", this, players[0].UserIDString));
                                SendReply(player, lang.GetMessage("destroyEvent", this, player.UserIDString), players[0].displayName);
                                return;
                            }
                            foreach (var eventRunning in EventRaidManager._AllRaids)
                            {
                                if (eventRunning.player == players[0])
                                {
                                    eventRunning.Destroy();
                                    SendReply(player, lang.GetMessage("destroyEvent", this, player.UserIDString), players[0].displayName);
                                    SendReply(players[0], lang.GetMessage("EventCanceledByAdmin", this, players[0].UserIDString));
                                    return;
                                }
                            }
                            SendReply(player, lang.GetMessage("destroyEventNotFound", this, player.UserIDString), players[0].displayName);
                        }
                    }
                    return;
                default:
                    break;
            }
        }

        private void cmdChatStartRaid(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, usePerm))
            {
                SendReply(player, lang.GetMessage("noPermUse", this, player.UserIDString));
                return;
            }
            int permCount = 0;
            string type = "unknown";
            string types = "";
            string level = "";
            int count = 0;
            int NPCs;
            object reward;
            int waves;
            int cooldownTime = configData.settings.cooldownSeconds;

            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "stats":
                        UIStatsMainWindow(player);
                        return;

                    case "buy":
                        if (args.Length < 2)
                            cmdChatStartRaidBuy(player, null);
                        else
                            cmdChatStartRaidBuy(player, args[1]);
                        return;

                    case "clear":
                        cmdChatRaidClearDatsa(player);
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

                    case "reload":
                        if (args.Length < 2)
                        {
                            SendReply(player, $"<color=orange>Admin Command Usage</color>:\n" +
                              $"<color=orange>/{chatComand} reload all</color> - Will cancel all events.\n" +
                              $"<color=orange>/{chatComand} reload player <player></color> - Will cancel their raid event.");
                            return;
                        }

                        switch (args[1].ToLower())
                        {
                            case "all":
                                cmdChatRaidReload(player, command, args);
                                return;

                            case "player":
                                cmdChatRaidReload(player, command, args);
                                return;

                            default:
                                break;
                        }
                        return;

                    case "loot":
                        if (!permission.UserHasPermission(player.UserIDString, adminPerm))
                        {
                            SendReply(player, lang.GetMessage("noPermUse", this, player.UserIDString));
                            return;
                        }
                        if (args.Length < 3)
                        {
                            SendReply(player, $"<color=orange>Admin Command Usage</color>:\n" +
                              $"<color=orange>/{chatComand} loot add <name></color> - Will add all inventory to loot file.\n" +
                              $"<color=orange>/{chatComand} loot remove <name> <player></color> - Will remove loot file.");
                            return;
                        }

                        switch (args[1].ToLower())
                        {
                            case "add":
                                cmdChatLoot(player, args[2].ToLower());
                                return;

                            case "clear":
                                cmdChatLoot(player, args[2].ToLower(), true);
                                return;

                            default:
                                break;
                        }
                        return;

                    default:
                        break;
                }
            }

            foreach (var key in configData.raidSettings.raidTypes)
            {
                string rewards = "";
                if (permission.UserHasPermission(player.UserIDString, configData.raidSettings.raidTypes[key.Key].Permission))
                {
                    permCount++;
                    waves = configData.raidSettings.raidTypes[key.Key].TotalNpcWaves + 1;
                    NPCs = configData.raidSettings.raidTypes[key.Key].TotalNpcs;

                    if (configData.raidRewardOptions.RewardOptions.ContainsKey(key.Key))
                    {
                        if (configData.raidRewardOptions.RewardOptions[key.Key].enabled)
                        {
                            reward = configData.raidRewardOptions.RewardOptions[key.Key].RewardAmmount;
                            string typesRew = configData.raidRewardOptions.RewardOptions[key.Key].RewardType.ToLower();
                            if (typesRew == "serverrewards" || typesRew == "economics")
                                rewards = ", " + lang.GetMessage("Reward", this, player.UserIDString) + $"{Convert.ToInt32(reward)}</color>";
                            else if (typesRew == "kit" || typesRew == "kits")
                            {
                                if (reward is string)
                                {
                                    rewards = $", Reward <color=orange>{Convert.ToString(reward)}</color>";
                                }
                                else
                                {
                                    rewards = $", Reward <color=orange>Random kit</color>";
                                }
                            }
                            else
                            {
                                var ids = default(int);
                                if (int.TryParse(configData.raidRewardOptions.RewardOptions[key.Key].RewardType, out ids))
                                {
                                    ItemDefinition def = ItemManager.FindItemDefinition(ids);
                                    if (def != null)
                                    {
                                        string rName = def.displayName.english;
                                        if (rName.Length > 12)
                                            rName = rName.Substring(0, rName.Length - (rName.Length - 12));
                                        rewards = $", Reward <color=orange>{rName}</color>";
                                    }
                                }
                            }
                        }

                    }
                    if (permCount == 1)
                        types += ($"<color=yellow>{key.Key}</color>");
                    else types += ($" | <color=yellow>{key.Key}</color> ");

                    if (rewards != "")
                        level += $"<color=green>Mode</color>: <color=yellow>{key.Key}</color> - NPCs <color=orange>{NPCs}</color>, Waves <color=orange>{waves}</color>{rewards}\n";
                    else level += $"<color=green>Mode</color>: <color=yellow>{key.Key}</color> - NPCs <color=orange>{NPCs}</color>, Waves <color=orange>{waves}</color>\n";
                }
            }

            if (permCount > 0)
            {
                SendReply(player, lang.GetMessage("ChatHeader", this, player.UserIDString) + $"\n" +
                $"<color=orange>/{chatComand} buy</color> - Shows the costs of each buyable raiding type.\n" +
                $"<color=orange>/{chatComand} buy {types}</color> - Start an NPC Raiding event on your base that you have TC Auth on.\n\n" +
                $"{level}");
            }
            else
            {
                SendReply(player, lang.GetMessage("ChatHeader", this, player.UserIDString) + $"\n" +
               $"<color=orange>/{chatComand} buy</color> - Shows the costs of each buyable raiding type.\n" +
               $"<color=orange>/{chatComand} buy {types}</color> - Start an NPC Raiding event on your base that you have TC Auth on.\n\n" +
               $"You do not have any buy permisions!");
            }
            if (permission.UserHasPermission(player.UserIDString, adminPerm))
                SendReply(player, $"<color=red>Admin Commands</color>:\n" +
                    $"<color=orange>/{chatComand} reload all</color> - Will cancel all events.\n" +
                    $"<color=orange>/{chatComand} reload player <player></color> - Will cancel their raid event.\n" +
                    $"<color=orange>/{chatComand} clear</color> - Reset all player data and cooldowns.");
            return;
        }

        private void cmdChatLoot(BasePlayer player, string lootfile, bool remove = false)
        {
            if (remove)
            {
                if (HasSaveFile(lootfile))
                {
                    LootEntity newFile = new LootEntity();
                    if (newFile != null)
                    {
                        SaveLootData(newFile, Name + "/LootProfiles/" + lootfile);
                        SendReply(player, lang.GetMessage("lootFileCleared", this, player.UserIDString));
                    }
                }
                else SendReply(player, lang.GetMessage("lootFileNotExists", this, player.UserIDString));

            }
            else
            {
                if (!HasSaveFile(lootfile))
                {
                    LootEntity newFile = new LootEntity();
                    newFile.items = new List<itemInfo>();

                    if (player.inventory.containerMain != null)
                        foreach (Item itemUse in player.inventory.containerMain.itemList.ToList())
                        {
                            if (itemUse != null)
                                newFile.items.Add(new itemInfo() { item = itemUse.info.shortname, location = "containerMain", amountMax = itemUse.amount, amountMin = 1, skin = itemUse.skin, name = itemUse.name });
                        }
                    if (player.inventory.containerBelt != null)
                        foreach (Item itemUse in player.inventory.containerBelt.itemList.ToList())
                        {
                            if (itemUse != null)
                                newFile.items.Add(new itemInfo() { item = itemUse.info.shortname, location = "containerBelt", amountMax = itemUse.amount, amountMin = 1, skin = itemUse.skin, name = itemUse.name });
                        }
                    if (player.inventory.containerWear != null)
                        foreach (Item itemUse in player.inventory.containerWear.itemList.ToList())
                        {
                            if (itemUse != null)
                                newFile.items.Add(new itemInfo() { item = itemUse.info.shortname, location = "containerWear", amountMax = itemUse.amount, amountMin = 1, skin = itemUse.skin, name = itemUse.name });
                        }
                    // SaveLootData();
                    SaveLootData(newFile, Name + "/LootProfiles/" + lootfile);
                    SendReply(player, lang.GetMessage("lootFileAdded", this, player.UserIDString));

                }
                else if (HasSaveFile(lootfile))
                {
                    LootEntity newFile = null;
                    LoadLootData(out newFile, Name + "/LootProfiles/" + lootfile);
                    if (newFile != null)
                    {
                        if (player.inventory.containerMain != null)
                            foreach (Item itemUse in player.inventory.containerMain.itemList.ToList())
                            {
                                if (itemUse != null)
                                    newFile.items.Add(new itemInfo() { item = itemUse.info.shortname, location = "containerMain", amountMax = itemUse.amount, amountMin = 1, skin = itemUse.skin, name = itemUse.name });
                            }
                        if (player.inventory.containerBelt != null)
                            foreach (Item itemUse in player.inventory.containerBelt.itemList.ToList())
                            {
                                if (itemUse != null)
                                    newFile.items.Add(new itemInfo() { item = itemUse.info.shortname, location = "containerBelt", amountMax = itemUse.amount, amountMin = 1, skin = itemUse.skin, name = itemUse.name });
                            }
                        if (player.inventory.containerWear != null)
                            foreach (Item itemUse in player.inventory.containerWear.itemList.ToList())
                            {
                                if (itemUse != null)
                                    newFile.items.Add(new itemInfo() { item = itemUse.info.shortname, location = "containerWear", amountMax = itemUse.amount, amountMin = 1, skin = itemUse.skin, name = itemUse.name });
                            }
                        //   SaveLootData();
                        SaveLootData(newFile, Name + "/LootProfiles/" + lootfile);
                        SendReply(player, lang.GetMessage("LootFileAddMore", this, player.UserIDString));
                    }

                }
            }
        }

        private void cmdChatStartRaidBuy(BasePlayer player, string command)
        {
            string type = "unknown";
            if (command != null)
                type = command.ToLower();

            string types = "";
            string typesTotal = "";
            int count = 0;
            int cooldownTime = configData.settings.cooldownSeconds;
            string buyname = "";

            if (command == null)
            {
                foreach (var key in configData.raidBuyOptions.BuyOptions)
                {
                    count++;
                    if (configData.raidSettings.raidTypes.ContainsKey(key.Key) && permission.UserHasPermission(player.UserIDString, configData.raidSettings.raidTypes[key.Key].Permission))
                    {
                        if (count <= 1)
                            types += ($"<color=yellow>{key.Key}</color> ");
                        else types += ($"| <color=yellow>{key.Key}</color> ");

                        var ids = default(int);
                        if (int.TryParse(key.Value.BuyType, out ids))
                        {
                            ItemDefinition def = ItemManager.FindItemDefinition(ids);
                            if (def != null)
                                buyname = def.displayName.english;
                        }
                        else buyname = key.Value.BuyType.ToString();

                        if (buyname == "serverrewards" || buyname == "economics")
                            buyname = "$";

                        if (buyname == "$")
                            typesTotal += ($"\n<color=green>Raid Type</color>: <color=yellow>{key.Key}</color> - Cost: <color=green>{buyname}</color><color=yellow>{key.Value.BuyAmmount}</color>");
                        else
                            typesTotal += ($"\n<color=green>Raid Type</color>: <color=yellow>{key.Key}</color>  - Cost: <color=yellow>{key.Value.BuyAmmount}</color> <color=green>{buyname}</color>");
                    }
                }
                if (command == null && types.Length > 0)
                {
                    SendReply(player, lang.GetMessage("ChatHeader", this, player.UserIDString) + $"\n" +
                        $"<color=orange>/{chatComand} buy {types} </color>- Start a NPC Raiding event on your base that you have TC Auth on. \n{typesTotal}");
                    return;
                }
                else if (types.Length <= 0)
                {
                    SendReply(player, lang.GetMessage("ChatHeader", this, player.UserIDString) + $"\n" +
                       $"<color=orange>Nothing you can buy here!</color>");
                    return;
                }
            }
            else
            {
                if (canBuyRaid(player, type))
                {
                    Vector3 pos = player.transform.position;
                    float y = TerrainMeta.HeightMap.GetHeight(new Vector3(player.transform.position.x, 0, player.transform.position.z)) + 2f;
                    if (y != null)
                    {
                        pos.y = y;
                    }

                    if (EventRaidManager._AllRaids.Count >= configData.settings.MaxRaids)
                    {
                        int totalH = configData.settings.MaxRaids - EventRaidManager._AllRaids.Count;
                        if (totalH <= 0)
                            SendReply(player, lang.GetMessage("waitingNext", this, player.UserIDString), EventRaidManager._AllRaids.Count);
                        else
                            SendReply(player, lang.GetMessage("waitingLine", this, player.UserIDString), EventRaidManager._AllRaids.Count, EventList.Count);
                    }
                    else
                        SendReply(player, lang.GetMessage("waiting", this, player.UserIDString));

                    BuildingPrivlidge priv = player.GetBuildingPrivilege();
                    EventList.Add(player, new eventList() { type = type, location = pos, priv = priv });
                    if (configData.raidVipOptions != null && configData.raidVipOptions.VipOptions.Count > 0)
                    {
                        foreach (var perm in configData.raidVipOptions.VipOptions)
                            if (permission.UserHasPermission(player.UserIDString, perm.Key))
                                cooldownTime = perm.Value.cooldownSeconds;
                    }
                    hasCooldown(player, player.userID, cooldownTime);
                    foreach (var user in priv.authorizedPlayers.ToList())
                    {
                        if (user.userid == player.userID)
                            continue;
                        if (configData.raidVipOptions != null && configData.raidVipOptions.VipOptions.Count > 0)
                        {
                            foreach (var perm in configData.raidVipOptions.VipOptions)
                                if (permission.UserHasPermission(user.userid.ToString(), perm.Key))
                                    cooldownTime = perm.Value.cooldownSeconds;
                        }
                        BasePlayer TCplayer = BasePlayer.FindByID(user.userid);
                        hasCooldown(TCplayer, player.userID, cooldownTime, true);
                    }
                    if (configData.settings.logToFile)
                        LogToFile("NpcRaiders", $"[{DateTime.Now}] {player.displayName}({player.userID}) is calling raid {type} at {pos}", this);
                }
            }
        }

        private bool canBuyRaid(BasePlayer player, string type)
        {
            double timeStamp = GrabCurrentTime();
            ItemDefinition def = null;

            if (!configData.raidBuyOptions.BuyOptions.ContainsKey(type) || !configData.raidSettings.raidTypes.ContainsKey(type))
                return false;

            BuildingPrivlidge privCheck = player.GetBuildingPrivilege();
            if (privCheck != null && isBlockedTerain(privCheck.transform.position))
            {
                SendReply(player, lang.GetMessage("Blockedhere", this, player.UserIDString));
                return false;
            }

            if (privCheck != null && privCheck.transform.position.y <= -0.1f)
            {
                SendReply(player, lang.GetMessage("BlockedhereWater", this, player.UserIDString));
                return false;
            }

            if (EventList.ContainsKey(player))
            {
                SendReply(player, lang.GetMessage("ThereAlready", this, player.UserIDString));
                return false;
            }

            if (player.IsBuildingBlocked() || !player.IsBuildingAuthed())
            {
                SendReply(player, lang.GetMessage("blocked", this, player.UserIDString));
                return false;
            }

            if (pcdData.pEntity.ContainsKey(player.userID) && !permission.UserHasPermission(player.UserIDString, coolPerm))
            {
                if (pcdData.pEntity.ContainsKey(player.userID))
                {
                    BuildingPrivlidge priv = player.GetBuildingPrivilege();
                    if (priv != null)
                    {
                        if (pcdData.pEntity[player.userID].tcCooldown.ContainsKey(priv.net.ID.Value))
                        {
                            var cdTime = pcdData.pEntity[player.userID].tcCooldown[priv.net.ID.Value];
                            if (cdTime > timeStamp)
                            {
                                string time = FormatTime((long)(cdTime - timeStamp));
                                SendReply(player, lang.GetMessage("cooldownAuth", this, player.UserIDString), time);
                                return false;
                            }
                        }
                    }
                }
            }

            string buyType = configData.raidBuyOptions.BuyOptions[type].BuyType;

            if (pcdData.pEntity.ContainsKey(player.userID) && !permission.UserHasPermission(player.UserIDString, coolPerm))
            {
                var cdTime = pcdData.pEntity[player.userID].cooldown;
                if (cdTime > timeStamp)
                {
                    string timeNow1 = FormatTime((long)(cdTime - timeStamp));
                    SendReply(player, lang.GetMessage("cooldown", this, player.UserIDString), timeNow1);
                    return false;
                }
            }

            if (!permission.UserHasPermission(player.UserIDString, configData.raidSettings.raidTypes[type].Permission))
            {
                SendReply(player, lang.GetMessage("noPerm", this, player.UserIDString));
                return false;
            }

            if (!permission.UserHasPermission(player.UserIDString, bypassCost))
            {
                var ids = default(int);
                if (int.TryParse(buyType, out ids))
                {
                    def = ItemManager.FindItemDefinition(ids);
                }
                if (def != null)
                {
                    int ammount = Convert.ToInt32(configData.raidBuyOptions.BuyOptions[type].BuyAmmount);
                    if (player.inventory.GetAmount(ids) < ammount)
                    {
                        SendReply(player, lang.GetMessage("noMoney", this, player.UserIDString), ammount, def.displayName.english);
                        return false;
                    }
                    player.inventory.Take(null, ids, ammount);
                    return true;
                }
                else if (def == null)
                {
                    object totals = configData.raidBuyOptions.BuyOptions[type].BuyAmmount;
                    if (buyType.ToLower() == "serverrewards")
                    {
                        int totalHave = (int)ServerRewards.Call("CheckPoints", player.userID);
                        if (totalHave < Convert.ToInt32(totals))
                        {
                            SendReply(player, lang.GetMessage("noMoney", this, player.UserIDString), totals, "Reward Points");
                            return false;
                        }
                        ServerRewards.Call("TakePoints", player.userID, Convert.ToInt32(totals));
                        return true;
                    }
                    else if (buyType.ToLower() == "economics")
                    {
                        double totalHave = (double)Economics.Call("Balance", player.userID);
                        if (totalHave < Convert.ToDouble(totals))
                        {
                            SendReply(player, lang.GetMessage("noMoney", this, player.UserIDString), totals, "Eco Points");
                            return false;
                        }
                        Economics.Call("Withdraw", player.userID, Convert.ToDouble(totals));
                        return true;
                    }
                }
            }
            else return true;

            return false;
        }
        private bool hasCooldown(BasePlayer player, ulong userID, int time, bool notify = false)
        {
            double timeStamp = GrabCurrentTime();
            if (!pcdData.pEntity.ContainsKey(userID))
                pcdData.pEntity.Add(userID, new PCDInfo());

            if (!notify)
            {
                if (pcdData.pEntity.ContainsKey(userID) && !permission.UserHasPermission(userID.ToString(), coolPerm)) // Check if the player already has a cooldown for this
                {
                    pcdData.pEntity[userID].cooldown = (long)timeStamp + time;
                    SaveData();
                }

            }
            else if (!permission.UserHasPermission(userID.ToString(), coolPerm))
            {
                pcdData.pEntity[userID].cooldown = (long)timeStamp + time;
                SaveData();
                if (player != null)
                    SendReply(player, lang.GetMessage("InEvent", this, player.UserIDString));
            }
            return false;
        }

        private static void StripInventory(BasePlayer player, bool skipWear = false)
        {
            List<Item> list = Pool.GetList<Item>();

            player.inventory.GetAllItems(list);

            for (int i = list.Count - 1; i >= 0; i--)
            {
                Item item = list[i];

                if (skipWear && item?.parent == player.inventory.containerWear)
                    continue;

                item.RemoveFromContainer();
                item.Remove();
            }

            Pool.FreeList(ref list);
        }

        private static void StripCorpsInventory(NPCPlayerCorpse player)
        {
            foreach (var cont in player.containers)
            {
                cont?.Clear();
                ItemManager.DoRemoves();
            }
        }

        public static void startRaidForPlayer(BasePlayer player, Vector3 loc, BuildingPrivlidge priv, string type)
        {
            if (priv == null && player != null)
                _.SendReply(player, _.lang.GetMessage("TcNotFound", _, player.UserIDString));

            if (priv != null && _.configData.raidSettings.raidTypes.ContainsKey(type))
            {
                GameObject theObject = new GameObject();
                RaidThinkManager newThinker = theObject.AddComponent<RaidThinkManager>();
                EventRaidManager managers = EventRaidManager.Create(player, player.userID, loc, _.configData.raidSettings.raidTypes[type], type, newThinker, priv, theObject);
                newThinker.Setup(managers);
                managers.Start();
            }
            _.EventList.Remove(player);
            if (priv != null)
            {
                foreach (var user in priv.authorizedPlayers.ToList())
                {
                    if (user.userid == player.userID)
                    {
                        if (!_.statsData.pEntity.ContainsKey(user.userid))
                            _.statsData.pEntity.Add(user.userid, new StatsInfo() { name = player.displayName });
                        _.statsData.pEntity[user.userid].played++;
                        continue;
                    }
                    BasePlayer TCplayer = BasePlayer.FindByID(user.userid);
                    if (TCplayer != null)
                    {
                        if (_.configData.settings.gtip)
                            GameTipsMessage(TCplayer, _.lang.GetMessage("start", _, TCplayer.UserIDString));
                        if (_.configData.settings.ctip)
                            _.SendReply(TCplayer, _.lang.GetMessage("start", _, TCplayer.UserIDString));
                        if (!_.statsData.pEntity.ContainsKey(user.userid))
                            _.statsData.pEntity.Add(user.userid, new StatsInfo() { name = TCplayer.displayName });
                        _.statsData.pEntity[user.userid].played++;
                    }
                }
                _.SaveStats();
            }
        }

        private static BuildingPrivlidge HasCupboard(Vector3 position)
        {
            BuildingPrivlidge f = null;
            var list = Pool.GetList<BuildingPrivlidge>();
            Vis.Entities(position, 5f, list);
            foreach (var i in list)
            {
                if (i.IsValid() && i is BuildingPrivlidge)
                {
                    f = i;
                    break;
                }
            }
            Pool.FreeList(ref list);
            return f;
        }

        private object CanBeTargeted(NpcRaider scientistNPC, MonoBehaviour behaviour)
        {
            if (EventRaidManager._AllRaids.Count <= 0) return null;
            if (EventRaidManager._AllMembers.Contains(scientistNPC))
            {
                if ((behaviour is AutoTurret))
                {
                    if (scientistNPC != null && scientistNPC.manager != null && scientistNPC.manager.config.AutoTurretDamage <= 0)
                        return false;
                }
            }

            return null;
        }

        private object CanEntityBeTargeted(NpcRaider scientistNPC, AutoTurret turret)
        {
            if (EventRaidManager._AllRaids.Count <= 0) return null;
            if (turret is AutoTurret && EventRaidManager._AllMembers.Contains(scientistNPC))
            {
                if (scientistNPC != null && scientistNPC.manager != null && scientistNPC.manager.config.AutoTurretDamage <= 0)
                    return false;
                return true;
            }

            return null;
        }

        private object OnEntityTakeDamage(NpcRaider entity, HitInfo hitinfo)
        {
            if (EventRaidManager._AllRaids.Count <= 0) return null;
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
                else if (hitinfo != null && hitinfo.WeaponPrefab != null && hitinfo.Initiator != null && hitinfo.Initiator is NpcRaider)
                {
                    if (hitinfo.WeaponPrefab.ShortPrefabName.Contains("rocket"))
                        return true;
                    else if (hitinfo.WeaponPrefab.ShortPrefabName.Contains("timed"))
                        return true;
                }
            }
            return null;
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (EventRaidManager._AllRaids.Count <= 0) return;
            if (player == null || info == null || info.Initiator == null) return;

            if (info.Initiator.GetComponent<NpcRaider>() != null && player.userID.IsSteamId())
            {
                if (!statsData.pEntity.ContainsKey(player.userID))
                    statsData.pEntity.Add(player.userID, new StatsInfo() { name = player.displayName });
                statsData.pEntity[player.userID].deaths++;
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (EventRaidManager._AllRaids.Count <= 0) return null;

            if (hitinfo != null && hitinfo.WeaponPrefab != null && hitinfo.WeaponPrefab.name.Contains("rocket_mlrs"))
            {
                if (hitinfo.Weapon == null)
                {
                    if (entity.GetComponent<NpcRaider>() != null)
                        return false;
                    if (entity is BuildingBlock)
                        return true;
                }
            }
            else if (entity != null && entity is BuildingPrivlidge || entity is BuildingBlock || entity.name.Contains("wall.external") || entity.name.Contains("gates.external"))
            {
                if (!configData.settings.DamagePlayerBuilding) return null;
                if (entity.name.Contains("wall.external") || entity.name.Contains("gates.external"))
                    return null;
                if (hitinfo.Initiator != null)
                {
                    NpcRaider raider = hitinfo.Initiator.GetComponent<NpcRaider>();
                    if (raider != null)
                    {
                        if (raider.ShouldIgnoreBuildingDamage(entity))
                            return true;
                    }
                }
            }
            else if (entity != null && entity is BasePlayer && hitinfo != null && hitinfo.Initiator is NpcRaider && (hitinfo.Initiator as NpcRaider).manager != null)
            {
                if ((hitinfo.Initiator as NpcRaider).manager.config.playerDamage != 1.0f) hitinfo?.damageTypes?.ScaleAll((hitinfo.Initiator as NpcRaider).manager.config.playerDamage);
            }
            return null;
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (EventRaidManager._AllRaids.Count <= 0) return null;

            if (hitinfo != null && hitinfo.WeaponPrefab != null && hitinfo.WeaponPrefab.name.Contains("rocket_mlrs"))
            {
                if (hitinfo.Weapon == null)
                {
                    if (entity.GetComponent<NpcRaider>() != null)
                        return false;
                    if (entity is BuildingBlock)
                        return true;
                }
            }
            else if (entity != null && entity is NpcRaider)
            {
                if (hitinfo != null && hitinfo.Initiator is NpcRaider)
                {
                    return false;
                }
                else if (entity != null && hitinfo.Initiator != null && hitinfo.Initiator is AutoTurret)
                {
                    NpcRaider raider = entity.GetComponent<NpcRaider>();
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
            else if (entity != null && hitinfo != null && hitinfo.Initiator != null && hitinfo.Initiator is NpcRaider)
            {
                if (entity is BuildingPrivlidge || entity is BuildingBlock || entity.name.Contains("wall.external") || entity.name.Contains("gates.external"))
                {
                    if (entity.name.Contains("wall.external") || entity.name.Contains("gates.external"))
                        return true;

                    if (configData.settings.DamagePlayerBuilding)
                    {
                        if ((hitinfo.Initiator as NpcRaider).ShouldIgnoreBuildingDamage(entity))
                            return false;
                        else return true;
                    }
                    else return true;
                }
                else if (entity is BasePlayer && hitinfo != null && hitinfo.Initiator is NpcRaider && (hitinfo.Initiator as NpcRaider).manager != null)
                {
                    if ((hitinfo.Initiator as NpcRaider).manager.config.playerDamage != 1.0f)
                        hitinfo?.damageTypes?.ScaleAll((hitinfo.Initiator as NpcRaider).manager.config.playerDamage);
                    return true;
                }
            }
            return null;
        }

        private object CanLootEntity(BasePlayer player, NPCPlayerCorpse corpse)
        {
            if (corpse == null || player == null) return null;
            if (corpsLock.ContainsKey(corpse.playerSteamID))
            {
                if (corpsLock[corpse.playerSteamID].Contains(player.userID))
                {
                    corpsLock.Remove(corpse.playerSteamID);
                    return null;
                }
                GameTipsMessage(player, lang.GetMessage("NoLoot", this));
                return false;
            }
            return null;
        }

        private object CanLootEntity(BasePlayer player, DroppedItemContainer corpse)
        {
            if (corpse == null || player == null) return null;
            if (corpsLock.ContainsKey(corpse.playerSteamID))
            {
                if (corpsLock[corpse.playerSteamID].Contains(player.userID))
                {
                    corpsLock.Remove(corpse.playerSteamID);
                    return null;
                }
                GameTipsMessage(player, lang.GetMessage("NoLoot", this));
                return false;
            }
            return null;

        }

        //From Nteleportation
        private List<BasePlayer> FindPlayers(string arg, bool all = false)
        {
            var players = Pool.GetList<BasePlayer>();

            if (string.IsNullOrEmpty(arg))
            {
                return players;
            }

            foreach (var p in all ? BasePlayer.allPlayerList : BasePlayer.activePlayerList)
            {
                if (p == null || string.IsNullOrEmpty(p.displayName) || players.Contains(p))
                {
                    continue;
                }

                if (p.UserIDString == arg || p.displayName.Contains(arg, CompareOptions.OrdinalIgnoreCase))
                {
                    players.Add(p);
                }
            }

            return players;
        }

        private object OnLifeSupportSavingLife(BasePlayer player)
        {
            foreach (EventRaidManager raids in EventRaidManager._AllRaids)
                if (raids.authorizedPlayers.Contains(player.userID))
                    return true;
            return null;
        }

        //From Nteleportation
        private string GetMultiplePlayers(List<BasePlayer> players)
        {
            var list = Pool.GetList<string>();

            foreach (var player in players)
            {
                list.Add(player.displayName);
            }

            return string.Join(", ", list.ToArray());
        }

        private object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (EventRaidManager._AllRaids.Count <= 0 || entity == null) return null;

            if (player != null)
            {
                if (EventRaidManager._AllPlayers.Contains(player.userID))
                {
                    if (entity.health > entity.MaxHealth())
                    {
                        return null;
                    }

                    GameTipsMessage(player, lang.GetMessage("NoRepair", this));
                    return true;
                }
            }
            return null;
        }

        private object OnStructureUpgrade(BuildingBlock entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (EventRaidManager._AllRaids.Count <= 0 || entity == null) return null;

            if (player != null)
            {
                if (EventRaidManager._AllPlayers.Contains(player.userID))
                {
                    if (entity.health > entity.MaxHealth())
                    {
                        return null;
                    }
                    
                    GameTipsMessage(player, lang.GetMessage("NoRepair", this));
                    return true;
                }
            }
            return null;
        }

        public static void GameTipsMessage(BasePlayer player, string message, float howlong = 2f)
        {
            if (player != null && player.userID.IsSteamId())
            {
                player.SendConsoleCommand("gametip.hidegametip");
                player.SendConsoleCommand("gametip.showgametip", message);
                _.timer.Once(howlong, () => player?.SendConsoleCommand("gametip.hidegametip"));
            }
        }
        #endregion

        #region UiStats
        //Thanks to PlayerChalanges

        private IEnumerable<KeyValuePair<ulong, StatsInfo>> generateTopPlayerWins()
        {
            IEnumerable<KeyValuePair<ulong, StatsInfo>> leaders = statsData.pEntity.OrderByDescending(a => a.Value.won).Take(25);
            return leaders;
        }

        private IEnumerable<KeyValuePair<ulong, StatsInfo>> generateTopPlayerKills()
        {
            IEnumerable<KeyValuePair<ulong, StatsInfo>> leaders = statsData.pEntity.OrderByDescending(a => a.Value.killed).Take(25);
            return leaders;
        }

        private IEnumerable<KeyValuePair<ulong, StatsInfo>> generateTopPlayerDeaths()
        {
            IEnumerable<KeyValuePair<ulong, StatsInfo>> leaders = statsData.pEntity.OrderByDescending(a => a.Value.deaths).Take(25);
            return leaders;
        }

        private IEnumerable<KeyValuePair<ulong, StatsInfo>> generateTopPlayerLoses()
        {
            IEnumerable<KeyValuePair<ulong, StatsInfo>> leaders = statsData.pEntity.OrderByDescending(a => a.Value.lost).Take(25);
            return leaders;
        }

        private IEnumerable<KeyValuePair<ulong, StatsInfo>> generateTopPlayerWaves()
        {
            IEnumerable<KeyValuePair<ulong, StatsInfo>> leaders = statsData.pEntity.OrderByDescending(a => a.Value.played).Take(25);
            return leaders;
        }

        public static class UI
        {
            public static CuiElementContainer Container(string panelName, string aMin, string aMax, bool cursor = false)
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = ColorExtensions.ToRustFormatString(_.backgroundColor)},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent = "Overlay",
                        panelName
                    }
                };
                return container;
            }

            public static void Panel(ref CuiElementContainer container, string panel, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = ColorExtensions.ToRustFormatString(_.PanelColor) },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }

            public static void Label(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 0f)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }

            public static void Button(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 0f)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = ColorExtensions.ToRustFormatString(_.backgroundColor), Command = command, FadeIn = fadein },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
        }

        [ConsoleCommand("UI_DestroyAllRaiders")]
        private void cmdDestroyAll(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CuiHelper.DestroyUi(player, UIMain);
        }

        private string UIMain = "PCUI_Main";

        private void UIStatsMainWindow(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UIMain);
            CreateMenuContents(player, 0);
        }

		private static List<string> elements = new List<string>() { "WINS", "LOSES", "KILLS", "DEATHS", "WAVES" };
        private void CreateMenuContents(BasePlayer player, int page = 0)
        {
            CuiElementContainer container = UI.Container(UIMain, "0 0", "1 1", true);
            UI.Panel(ref container, UIMain, "0.005 0.93", "0.995 0.99");

            int count = page * 5;
            int number = 0;
            float dimension = 0.19f;
            for (int i = count; i < count + 5; i++)
            {
                if (elements.Count < i + 1) continue;
                float leftPos = 0.005f + (number * (dimension + 0.01f));
                AddMenuStats(ref container, UIMain, elements[i], leftPos, 0.01f, leftPos + dimension, 0.92f);
                number++;
            }

            UI.Button(ref container, UIMain, "Close", 16, "0.85 0.94", "0.95 0.98", "UI_DestroyAllRaiders");
            UI.Button(ref container, UIMain, "My Stats", 16, "0.05 0.94", "0.15 0.98", "UI_MyStatsRaiders");
            UI.Label(ref container, UIMain, $"<color=yellow>Npc Raider Top Stats</color>", 19, "0.25 0.94", "0.75 0.98", TextAnchor.MiddleCenter);
            CuiHelper.AddUi(player, container);
        }

        private void AddMenuStats(ref CuiElementContainer MenuElement, string panel, string type, float left, float bottom, float right, float top)
        {
            IEnumerable<KeyValuePair<ulong, StatsInfo>> stat = null;
            switch (type.ToLower())
            {
                case "wins":
                    {
                        stat = generateTopPlayerWins();
                        break;
                    }
                case "kills":
                    {
                        stat = generateTopPlayerKills();
                        break;
                    }
                case "deaths":
                    {
                        stat = generateTopPlayerDeaths();
                        break;
                    }
                case "loses":
                    {
                        stat = generateTopPlayerLoses();
                        break;
                    }
                case "waves":
                    {
                        stat = generateTopPlayerWaves();
                        break;
                    }
                default:
                    break;
            }
            UI.Panel(ref MenuElement, UIMain, $"{left} {bottom}", $"{right} {top}");
            UI.Label(ref MenuElement, UIMain, $"<color=red>TOP {type}</color>", 16, $"{left + 0.005f} {bottom + 0.01f}", $"{right - 0.005f} {top - 0.01f}", TextAnchor.UpperCenter);
            if (stat != null)
            {
                bottom = bottom + 0.05f;
                top = top - 0.05f;
                left = left + 0.025f;
                right = right - 0.025f;
                string inputText = "Unknown";
                foreach (var key in stat)
                {
                    switch (type.ToLower())
                    {
                        case "wins":
                            {
                                inputText = key.Value.won.ToString();
                                break;
                            }
                        case "kills":
                            {
                                inputText = key.Value.killed.ToString();
                                break;
                            }
                        case "deaths":
                            {
                                inputText = key.Value.deaths.ToString();
                                break;
                            }
                        case "loses":
                            {
                                inputText = key.Value.lost.ToString();
                                break;
                            }
                        case "waves":
                            {
                                inputText = key.Value.played.ToString();
                                break;
                            }
                        default:
                            break;
                    }
                    bottom = bottom + 0.025f;
                    top = top - 0.025f;
                    UI.Label(ref MenuElement, UIMain, $"<color=orange>{key.Value.name}</color>: {inputText}", 13, $"{left} {bottom}", $"{right} {top}", TextAnchor.UpperLeft);
                }
            }
        }

        #region UiColorHelpers
        private static string HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        public static bool TryParseHexString(string hexString, out Color color)
        {
            try
            {
                color = FromHexString(hexString);
                return true;
            }
            catch
            {
                color = Color.white;
                return false;
            }
        }
        private static Color FromHexString(string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
            {
                throw new InvalidOperationException("Cannot convert an empty/null string.");
            }
            var trimChars = new[] { '#' };
            var str = hexString.Trim(trimChars);
            switch (str.Length)
            {
                case 3:
                    {
                        var chArray2 = new[] { str[0], str[0], str[1], str[1], str[2], str[2], 'F', 'F' };
                        str = new string(chArray2);
                        break;
                    }
                case 4:
                    {
                        var chArray3 = new[] { str[0], str[0], str[1], str[1], str[2], str[2], str[3], str[3] };
                        str = new string(chArray3);
                        break;
                    }
                default:
                    if (str.Length < 6)
                    {
                        str = str.PadRight(6, '0');
                    }
                    if (str.Length < 8)
                    {
                        str = str.PadRight(8, 'F');
                    }
                    break;
            }
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            return new Color32(r, g, b, a);
        }
        public static class ColorExtensions
        {
            public static string ToRustFormatString(Color color)
            {
                return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
            }

            public static string ToHexStringRGB(Color col)
            {
                Color32 color = col;
                return string.Format("{0}{1}{2}", color.r, color.g, color.b);
            }

            public static string ToHexStringRGBA(Color col)
            {
                Color32 color = col;
                return string.Format("{0}{1}{2}{3}", color.r, color.g, color.b, color.a);
            }

            public static bool TryParseHexString(string hexString, out Color color)
            {
                try
                {
                    color = FromHexString(hexString);
                    return true;
                }
                catch
                {
                    color = Color.white;
                    return false;
                }
            }
        }
        #endregion UiColorHelpers
        #endregion UiStats

        #region Localization
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["blocked"] = "<color=orange>You need to be in a building area!</color>",
                ["start"] = "<color=orange>Your event is starting!</color>",
                ["nextWave"] = "<color=orange>The next wave of raiders are coming.</color>",
                ["eventOver"] = "<color=orange>Your event is now over!</color>",
                ["eventOverWin"] = "<color=orange>Your just won the event!</color>",
                ["waiting"] = "<color=orange>Your event will start shortly!</color>",
                ["noHurt"] = "You can not hurt the event npc!   ",
                ["max"] = "<color=orange>The maximum amount of event's are running. There is {0} ahead of you.</color>",
                ["noTypeRaid"] = "<color=orange>There is no raid type named {0}</color>",
                ["cooldown"] = "<color=orange>You are in cooldown for {0} seconds!</color>",
                ["cooldownAuth"] = "<color=orange>You are in cooldown for Cupboard Authorize {0} seconds!</color>",
                ["ThereAlready"] = "<color=orange>You are already wating on the event.</color>",
                ["InEvent"] = "<color=orange>Someone authed on the TC just started a raid event.</color>",
                ["noPerm"] = "<color=orange>You do not have permission to use this raid type!</color>",
                ["noPermUse"] = "<color=orange>You do not have permission to execute this command!</color>",
                ["noMoney"] = "<color=orange>You need {0} {1} to start this event!</color>",
                ["MultiplePlayers"] = "<color=orange>Found to many matches {0}!</color>",
                ["destroyEvent"] = "<color=orange>Event for {0} destroyed!</color>",
                ["destroyEventNotFound"] = "<color=orange>Event for {0} was not found!</color>",
                ["MoneyIssued"] = "<color=orange>{1} you just recived ${0} for winning the event.</color>",
                ["Blockedhere"] = "<color=orange>The Admin has blocked raids on this terrain type.</color>",
                ["BlockedhereWater"] = "<color=orange>You can not start the event underground.</color>",
                ["RewardedItem"] = "<color=orange>You just won the event and got {0} {1}</color>",
                ["waitingNext"] = "<color=orange>There is currently {0} events running you are next in line!</color>",
                ["waitingLine"] = "<color=orange>There is currently {0} events running and {1} ahead of you. You will be notified before your event starts.</color>",
                ["waitingToStart"] = "<color=orange>Your event will start in 60 seconds.</color>",
                ["TcNotFound"] = "<color=orange>Your event tc was not found, Event canceled!</color>",
                ["NoLoot"] = "<color=orange>You can not loot this event corpse!</color>",
                ["UIHeader"] = "<color=orange>NPC RAIDERS STATS</color>",
                ["UI1"] = "<color=orange>Total events played</color><color=white>:</color>",
                ["UI2"] = "<color=orange>Total events won</color><color=white>:</color>",
                ["UI3"] = "<color=orange>Total events lost</color><color=white>:</color>",
                ["UI4"] = "<color=orange>Total npc's killed</color><color=white>:</color>",
                ["ArivalTimeNext"] = "<color=orange>Wave {1}/{0} will start in</color><color=white>:</color><color=yellow>{2}</color>",
                ["NoRepair"] = "You can not repair while in this event!",
                ["EventEndINTime"] = "<color=orange>You're event will end in {0}</color>",
                ["ChatHeader"] = "<size=22><color=green>NpcRaiders</color></size> <color=#ffffff>by Razor</color>",
                ["Reward"] = "Reward <color=orange>$"
            }, this);
        }
        #endregion

    }
}

/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */