using System;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using HarmonyLib;
using System.Linq;


namespace Oxide.Plugins
{
    [Info("CargoPlaneCrash", "https://discord.gg/TrJ7jnS233", "1.4.7")]
    [Description("CargoPlaneCrash")]
    class CargoPlaneCrash : CovalencePlugin
    {
        private List<Vector3> spawnItems = new List<Vector3>();
        private float remainToEvent;
        private float remain;
        private string prefabstr = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        private BaseEntity entity;
        private Vector3 target = new Vector3(0, 0, 0);
        private float mapSize;
        private float speed;
        private Vector3 direction;
        private float crashRadius = 10f;
        private bool expMarker;
        private bool signalSmoke;
        private int fireballsAmount;
        private int passengersAmount;
        private RaycastHit check;
        private LayerMask layerS = LayerMask.GetMask("Construction", "Terrain", "Water", "World");
        private string message;
        private string kitName;
        public static string passengerName = "Passenger";
        private Vector3 crashZoneCenter;
        private Vector3 crashPosition;
        [PluginReference] Plugin Kits, SimpleLootTable;
        private float altitude;
        private int lifeTimeExpMarker;
        private int lifeTimeCrates;
        private int lifeTimeAirdrops;
        private bool smokeFlag = false;
        private int lifeTimeSignalSmoke;
        private string botPrefab = "assets/prefabs/player/player.prefab";
        private int typeNpcs;
        private string npcPrefab;
        private int lifeTimeNpcs;
        private float npcDamageMultiplier;
        private int npcHealth;
        private const string npcName = "cpcguardnpc";
        private bool randomEventTime;
        private int minimumRemainToEvent;
        private int maximumRemainToEvent;
        private int minOnline;
        private bool buildingBlock;
        private int buildingBlockTime;
        private int buildingBlockTimer;
        private string npcKit;
        private float npcAttackRange;
        private ConfigData Configuration;
        private List<Vector3> wayPoints = new List<Vector3>();
        private List<BaseEntity> botList = new List<BaseEntity>();
        private List<BaseEntity> entityList = new List<BaseEntity>();
        private int coolDownTime = 7;
        private int homeRadius = 20;
        private int attackRadius = 75;
        private int eventDuration;
        private MapMarkerGenericRadius marker;
        private VendingMachineMapMarker vending;
        private BaseEntity explosionMarker;
        public static SphereEntity dome;
        private class NpcBrain : FacepunchBehaviour
        {
            public Vector3 homePosition;
            public Vector3 targetPosition;
            public BaseEntity target;
            public int stateTimer;
            public int moveChance;
            public Timer moveTimer;
        }

        private class MultipleZones
        {
            public string name { get; set; }
            public int radius { get; set; }
            public Vector3 center { get; set; }
        }
        private int zoneSequence = 0;

        private class TimerForCrate : FacepunchBehaviour
        {
            public Timer timer;
        }

        private float serverHackSeconds = HackableLockedCrate.requiredHackSeconds;
        private List<Vector3> monumentsList = new List<Vector3>();
        private List<Vector3> tcList = new List<Vector3>();
        private int monumentsRadius;
        public static BasePlayer eventOwner;
        public static string enterMessage;
        public static string ejectMessage;
        public static string ownerLeaveMessage;
        public static bool ejectPlayers;
        public static int eventRadius;
        public static int ownerLeftTime;
        public static bool teamOwners;
        public static bool adminDomeAllow;
        public static ulong iconID;
        public static List<Vector3> points = new List<Vector3>();
        private string markerName;
        private Timer eventTimer;
        public static int ownerTimerRemain = -1;


        class ConfigData
        {
            [JsonProperty("PVE mode (crates can only be looted by the player who first dealt damage to the NPC)")]
            public bool pveMode = false;
            [JsonProperty("Give event ownership to the owner's teammates if he is no longer the owner. Only if teammates are within the event radius (for PVE mode)")]
            public bool teamOwners = true;
            [JsonProperty("Radius for event(for PVE mode)")]
            public int eventRadius = 380;
            [JsonProperty("Create a dome for PVE mode")]
            public bool createSphere = false;
            [JsonProperty("Dome transparency (the higher the value, the darker the dome, recommended 4)")]
            public int transparencySphere = 4;
            [JsonProperty("Time after which the owner of the event will be deleted if he left the dome or left the server (for PVE mode)")]
            public int ownerLeftTime = 300;
            [JsonProperty("Message when a player enters the event dome(only for PVE mode if there is a dome)")]
            public string enterMessage = "You have entered the Cargo Plane Crash Event";
            [JsonProperty("Message when the event owner leaves the event dome (only for PVE mode if there is a dome)")]
            public string ownerLeaveMessage = "Return to the event dome, otherwise after 300 seconds you will no longer be the owner of this event";
            [JsonProperty("Do not allow other players into the event(only for PVE mode if there is a dome) Be careful, if the crash site is near the player's base and the player is not the owner of the event, he will be ejected from his base outside the dome")]
            public bool ejectPlayers = false;
            [JsonProperty("Message when a player is ejected from the event dome(only for PVE mode if there is a dome)")]
            public string ejectMessage = "You cannot be here, you are not the owner of this event";
            [JsonProperty("Allow admin to be in event dome (only for PVE mode if there is a dome)")]
            public bool adminDomeAllow = true;
            [JsonProperty("Triggering an event by timer (disable if you want to trigger the event only manually)")]
            public bool timerStart = true;
            [JsonProperty("Time to event start(in seconds)")]
            public float remainToEvent = 3600f;
            [JsonProperty("Random time to event start")]
            public bool randomEventTime = false;
            [JsonProperty("Minimum time to event start(in seconds)")]
            public int minimumRemainToEvent = 600;
            [JsonProperty("Maximum time to event start(in seconds)")]
            public int maximumRemainToEvent = 10800;
            [JsonProperty("CargoPlane speed(recommended 4 to 6)")]
            public float speed = 5f;
            [JsonProperty("Minimum amount of online players to trigger the event")]
            public int minOnline = 1;
            [JsonProperty("Minimum crates amount(spawn after crash)")]
            public int cratesAmount = 1;
            [JsonProperty("Maximum crates amount(spawn after crash)")]
            public int cratesAmountMax = 1;
            [JsonProperty("Crate simple loot table name(plugin SimpleLootTable is required)")]
            public string crateTableName = "";
            [JsonProperty("Minimum number of items in a crate(plugin SimpleLootTable is required)")]
            public int crateMinItems = 0;
            [JsonProperty("Maximum number of items in a crate(plugin SimpleLootTable is required)")]
            public int crateMaxItems = 0;
            [JsonProperty("Crates lifetime(in seconds). The crate will not be destroyed if it has been activated")]
            public int lifeTimeCrates = 3600;
            [JsonProperty("Crates timer(in seconds)")]
            public int timerCrates = 900;
            [JsonProperty("Remove crates after being looted by a player(in seconds)")]
            public int removeCratesTime = 300;
            [JsonProperty("Minimum airdrops amount(spawn after crash)")]
            public int airdropsAmount = 1;
            [JsonProperty("Maximum airdrops amount(spawn after crash)")]
            public int airdropsAmountMax = 1;
            [JsonProperty("Airdrop simple loot table name(plugin SimpleLootTable is required)")]
            public string airdropTableName = "";
            [JsonProperty("Minimum number of items in an airdrop(plugin SimpleLootTable is required)")]
            public int airdropMinItems = 0;
            [JsonProperty("Maximum number of items in an airdrop(plugin SimpleLootTable is required)")]
            public int airdropMaxItems = 0;
            [JsonProperty("Airdrops lifetime(in seconds)")]
            public int lifeTimeAirdrops = 3600;
            [JsonProperty("Fireballs amount(spawn after crash)")]
            public int fireballsAmount = 5;
            [JsonProperty("Passengers amount(spawn after crash)")]
            public int passengersAmount = 2;
            [JsonProperty("Explosion marker on the map(spawn after crash)")]
            public bool expMarker = true;
            [JsonProperty("Explosion marker lifetime(in seconds)")]
            public int lifeTimeExpMarker = 300;
            [JsonProperty("Enable signal smoke(spawn after crash)")]
            public bool signalSmoke = true;
            [JsonProperty("Signal smoke lifetime(in seconds, max 214)")]
            public int lifeTimeSignalSmoke = 214;
            [JsonProperty("Map size(crash zone size), you can see the zone, use the chat command /showcrashzone")]
            public float mapSize = 4500;
            [JsonProperty("Starting altitude, defaults to map size, can be increased if cargo plane hits high ground(no more than 10000 recommended)")]
            public float altitude = 4500;
            [JsonProperty("Crash zone center. Use chat command /setcrashzonecenter to set to player position. You can check crash zone center, use the chat command /showcrashzone")]
            public Vector3 crashZoneCenter = new Vector3(0, 0, 0);
            [JsonProperty("Use multiple zones")]
            public bool useMultipleZones = false;
            [JsonProperty("Select a zone from the list sequentially(if false, then the crash zone will be chosen randomly)")]
            public bool selectZoneSequentially = false;
            [JsonProperty("Zones list")]
            public List<MultipleZones> zonesList = new List<MultipleZones>()
            {
                new MultipleZones{ name = "0", radius = 300, center = new Vector3(-300, 0, 0) },
                new MultipleZones{ name = "1", radius = 200, center = new Vector3(100, 0, 0) }
            };
            [JsonProperty("Event message(if empty, no message will be displayed)")]
            public string message = "CargoPlane event started";
            [JsonProperty("Crash message(if empty, no message will be displayed)")]
            public string crashMessage = "Cargo plane crashed";
            [JsonProperty("Event end message(if empty, no message will be displayed)")]
            public string endMessage = "CargoPlaneCrash event ended";
            [JsonProperty("Message about coordinates(will display the coordinates of the crash site. If empty, no message will be displayed)")]
            public string coordMessage = "";
            [JsonProperty("Kit name(you can use kits for passengers if you have Kits plugin)")]
            public string kitName = "";
            [JsonProperty("Passenger name")]
            public string passengerName = "Mister bot";
            [JsonProperty("Use NPC prefab for passengers")]
            public bool passengerNPC = false;
            [JsonProperty("Minimum NPCs amount(spawn after crash)")]
            public int npcsAmount = 2;
            [JsonProperty("Maximum NPCs amount(spawn after crash)")]
            public int npcsAmountMax = 2;
            [JsonProperty("NPCs lifetime(in seconds)")]
            public int lifeTimeNpcs = 3600;
            [JsonProperty("NPCs type(NPCs prefab, experimental setting, it is not known how the NPCs will behave) 0 - tunneldweller; 1 - underwaterdweller; 2 - excavator; 3 - full_any; 4 - lr300; 5 - mp5; 6 - pistol; 7 - shotgun; 8 - heavy; 9 - junkpile_pistol; 10 - oilrig; 11 - patrol; 12 - peacekeeper; 13 - roam; 14 - roamtethered; 15 - bandit_guard; 16 - cargo; 17 - cargo_turret_any; 18 - cargo_turret_lr300; 19 - ch47_gunner")]
            public int typeNpcs = 8;
            [JsonProperty("NPCs health(0 - default)")]
            public int npcHealth = 0;
            [JsonProperty("NPCs damage multiplier")]
            public float npcDamageMultiplier = 1f;
            [JsonProperty("NPCs accuracy(the lower the value, the more accurate, 0 - maximum accuracy)")]
            public float npcAccuracy = 2f;
            [JsonProperty("NPCs attack range")]
            public float npcAttackRange = 75f;
            [JsonProperty("Radius of chasing the player(NPCs will chase the player as soon as he comes closer than the specified radius, must be no greater than the attack range)")]
            public float chaseRadius = 60;
            [JsonProperty("Minimum distance to NPC damage")]
            public float npcMinDistToDmg = 75f;
            [JsonProperty("Message if the player attacks far away NPCs")]
            public string npcFarAwayMessage = "NPC is too far away, he doesn't take damage";
            [JsonProperty("Kit for NPCs. The NPC will use the weapon that is in the first slot of the belt(requires Kits plugin)")]
            public string npcKit = "";
            [JsonProperty("Default displayName for NPC(for SimpleKillFeed/DeathNotes plugin)")]
            public string displayNameNPC = "Crashsite NPC";
            [JsonProperty("List of displayNames for each NPC(for SimpleKillFeed/DeathNotes plugin)")]
            public List<string> displayNamesListNPC = new List<string> { "Crashsite NPC1", "Crashsite NPC2", "Crashsite NPC3" };
            [JsonProperty("Prohibit building near the crash site")]
            public bool buildingBlock = false;
            [JsonProperty("Building prohibition radius")]
            public float buildingBlockRadius = 20;
            [JsonProperty("How long construction is prohibited near the crash site(in seconds)")]
            public int buildingBlockTime = 1800;
            [JsonProperty("Event marker on the map(spawn an event marker at the crash site)")]
            public bool eventMarker = false;
            [JsonProperty("Display approximate end time of event on marker")]
            public bool displayDuration = false;
            [JsonProperty("Event marker name")]
            public string markerName = "Cargo plane crash site";
            [JsonProperty("Event marker lifetime(in seconds)")]
            public int markerLifetime = 3600;
            [JsonProperty("Event marker transparency(0-1)")]
            public float markerAlpha = 0.75f;
            [JsonProperty("Event marker radius")]
            public float markerRadius = 0.5f;
            [JsonProperty("Event marker color.R(0-1)")]
            public float markerColorR = 1.0f;
            [JsonProperty("Event marker color.G(0-1)")]
            public float markerColorG = 0f;
            [JsonProperty("Event marker color.B(0-1)")]
            public float markerColorB = 0f;
            [JsonProperty("Do not spawn crates and NPCs when a cargo plane falls under water (if the water depth is greater than)")]
            public float spawnDepth = 0.5f;
            [JsonProperty("Do not choose a crash site near monuments")]
            public bool monumentsProhibition = false;
            [JsonProperty("If possible, the crash site will not be chosen near player bases")]
            public bool tcProhibition = true;
            [JsonProperty("SteamID for chat message icon")]
            public ulong iconID = 0;
        }

        [HarmonyPatch(typeof(BasePlayer))]
        [HarmonyPatch("displayName", MethodType.Getter)]
        public static class DisplayNameScientistCPC1
        {
            static bool Prefix(ref string __result, BasePlayer __instance)
            {
                if (__instance != null)
                {
                    BasePlayer bot = __instance.gameObject.GetComponent<BasePlayer>();
                    string npcname = "Scientist";

                    if (bot)
                    {
                        if (bot._name != "#Passenger1818")
                            return true;
                        npcname = passengerName;
                    }

                    __result = npcname;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ScientistNPC))]
        [HarmonyPatch("displayName", MethodType.Getter)]
        public static class DisplayNameScientistCPC
        {

            static bool Prefix(ref string __result, ScientistNPC __instance)
            {
                if (__instance != null)
                {
                    BasePlayer bot = __instance.gameObject.GetComponent<BasePlayer>();
                    string npcname = "Scientist";

                    if (bot)
                    {
                        if (bot._lastSetName != "#Scientist1818")
                            return true;
                        npcname = bot._name;
                    }

                    __result = npcname;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(TunnelDweller))]
        [HarmonyPatch("displayName", MethodType.Getter)]
        public static class DisplayNameTunnelDwellerCPC
        {
            static bool Prefix(ref string __result, ScientistNPC __instance)
            {
                if (__instance != null)
                {
                    BasePlayer bot = __instance.gameObject.GetComponent<BasePlayer>();
                    string npcname = "Scientist";

                    if (bot)
                    {
                        if (bot._lastSetName != "#Scientist1818")
                            return true;
                        npcname = bot._name;
                    }

                    __result = npcname;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(UnderwaterDweller))]
        [HarmonyPatch("displayName", MethodType.Getter)]
        public static class DisplayNameUnderwaterDwellerCPC
        {
            static bool Prefix(ref string __result, ScientistNPC __instance)
            {
                if (__instance != null)
                {
                    BasePlayer bot = __instance.gameObject.GetComponent<BasePlayer>();
                    string npcname = "Scientist";

                    if (bot)
                    {
                        if (bot._lastSetName != "#Scientist1818")
                            return true;
                        npcname = bot._name;
                    }

                    __result = npcname;
                    return false;
                }
                return true;
            }
        }

        private Harmony harmony = new Harmony("CargoPlaneCrash");

        private void OnServerInitialized()
        {
            harmony.UnpatchAll("CargoPlaneCrash");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            LoadConfig();
            markerName = Configuration.markerName;
            remainToEvent = Configuration.remainToEvent;
            remain = remainToEvent;
            speed = Configuration.speed;
            fireballsAmount = Configuration.fireballsAmount;
            passengersAmount = Configuration.passengersAmount;
            expMarker = Configuration.expMarker;
            mapSize = Configuration.mapSize / 4f;
            altitude = Configuration.altitude;
            message = Configuration.message;
            kitName = Configuration.kitName;
            passengerName = Configuration.passengerName;
            crashZoneCenter = Configuration.crashZoneCenter;
            lifeTimeExpMarker = Configuration.lifeTimeExpMarker;
            lifeTimeCrates = Configuration.lifeTimeCrates;
            lifeTimeAirdrops = Configuration.lifeTimeAirdrops;
            signalSmoke = Configuration.signalSmoke;
            lifeTimeSignalSmoke = Configuration.lifeTimeSignalSmoke;
            typeNpcs = Configuration.typeNpcs;
            npcHealth = Configuration.npcHealth;
            npcDamageMultiplier = Configuration.npcDamageMultiplier;
            lifeTimeNpcs = Configuration.lifeTimeNpcs;
            randomEventTime = Configuration.randomEventTime;
            minimumRemainToEvent = Configuration.minimumRemainToEvent;
            maximumRemainToEvent = Configuration.maximumRemainToEvent;
            minOnline = Configuration.minOnline;
            buildingBlock = Configuration.buildingBlock;
            buildingBlockTime = Configuration.buildingBlockTime;
            npcKit = Configuration.npcKit;
            npcAttackRange = Configuration.npcAttackRange;
            if (lifeTimeSignalSmoke > 214) lifeTimeSignalSmoke = 214;
            ejectMessage = Configuration.ejectMessage;
            enterMessage = Configuration.enterMessage;
            ownerLeaveMessage = Configuration.ownerLeaveMessage;
            ejectPlayers = Configuration.ejectPlayers;
            eventRadius = Configuration.eventRadius;
            ownerLeftTime = Configuration.ownerLeftTime;
            teamOwners = Configuration.teamOwners;
            adminDomeAllow = Configuration.adminDomeAllow;
            iconID = Configuration.iconID;

            Unsubscribe("OnTick");

            foreach (var item in TerrainMeta.Path.Monuments)
                monumentsList.Add(item.transform.position);

            foreach (var item in BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>())
                tcList.Add(item.transform.position);

            switch (typeNpcs)
            {
                case 0:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/tunneldweller/npc_tunneldweller.prefab";
                    break;
                case 1:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/underwaterdweller/npc_underwaterdweller.prefab";
                    break;
                case 2:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_excavator.prefab";
                    break;
                case 3:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab";
                    break;
                case 4:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_lr300.prefab";
                    break;
                case 5:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_mp5.prefab";
                    break;
                case 6:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_pistol.prefab";
                    break;
                case 7:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_shotgun.prefab";
                    break;
                case 8:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab";
                    break;
                case 9:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_junkpile_pistol.prefab";
                    break;
                case 10:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab";
                    break;
                case 11:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_patrol.prefab";
                    break;
                case 12:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_peacekeeper.prefab";
                    break;
                case 13:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roam.prefab";
                    break;
                case 14:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roamtethered.prefab";
                    break;
                case 15:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/banditguard/npc_bandit_guard.prefab";
                    break;
                case 16:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_cargo.prefab";
                    break;
                case 17:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_cargo_turret_any.prefab";
                    break;
                case 18:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_cargo_turret_lr300.prefab";
                    break;
                case 19:
                    npcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_ch47_gunner.prefab";
                    break;
            }

            if (randomEventTime)
                remainToEvent = UnityEngine.Random.Range(minimumRemainToEvent, maximumRemainToEvent);
            remain = remainToEvent;


            if (Configuration.timerStart)
            {
                Puts("Event will start in " + remainToEvent.ToString() + " seconds");

                timer.Every(1f, () =>
                    {
                        remain--;
                        ownerTimerRemain--;
                        buildingBlockTimer--;

                        if (remain < 0)
                        {
                            int s = 0;
                            if (entity)
                                s = -9999;
                            foreach (BasePlayer player in BasePlayer.activePlayerList)
                                s++;
                            if (s >= minOnline)
                                startEvent();
                            else
                            {
                                remain = remainToEvent;

                                if (entity)
                                    Puts("The event is already running");
                                else
                                    Puts("Not enough online players on the server, event will not start!");
                            }

                        }

                        if (ownerTimerRemain == 0)
                        {
                            SelectChangeOwner();
                        }

                    });
            }
            else
                Puts("The event will be triggered only in manual mode");

        }
        void SaveConfig(object config) => Config.WriteObject(config, true);
        void LoadConfig()
        {
            base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            Configuration = Config.ReadObject<ConfigData>();
            SaveConfig(Configuration);
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        }

        void DurationCalc(int airdropsAmount, int cratesAmount, int npcsAmount)
        {
            eventDuration = 0;
            if (airdropsAmount > 0) eventDuration = Configuration.lifeTimeAirdrops;
            if (cratesAmount > 0 && Configuration.lifeTimeCrates > eventDuration) eventDuration = Configuration.lifeTimeCrates;
            if (npcsAmount > 0 && Configuration.lifeTimeNpcs > eventDuration) eventDuration = Configuration.lifeTimeNpcs;
        }


        private void SendMessage(BasePlayer player, string message, ulong iconID)
        {
            player.SendConsoleCommand("chat.add", 2, iconID, message);
        }


        private void startEvent()
        {
            Interface.CallHook("CargoPlaneCrashStarted");

            if (entityList.Count == 0)
                KillAll();
            else
                RemoveEvent();

            Puts("Event started");

            findTarget();

            entity = GameManager.server.CreateEntity(prefabstr, new Vector3(0, 1000, 0), new Quaternion(), false);
            entity.Spawn();
            var str = "assets/bundled/prefabs/oilfireballsmall.prefab";
            BaseEntity fire = GameManager.server.CreateEntity(str, entity.transform.position);
            fire.Spawn();
            fire.SetParent(entity);
            fire.transform.localPosition = new Vector3(-6.8f, 6.8f, 3f);

            Subscribe("OnTick");

            if (message != null)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    SendMessage(player, message, Configuration.iconID);


            entity.transform.position += Vector3.up * altitude;

            if (randomEventTime)
                remainToEvent = UnityEngine.Random.Range(minimumRemainToEvent, maximumRemainToEvent);
            remain = remainToEvent;

            if (Configuration.timerStart)
                Puts("Next event will start in " + remainToEvent.ToString() + " seconds");
        }

        void OnTick()
        {
            if (entity)
            {
                entity.transform.LookAt(target);
                direction = entity.transform.forward;
                entity.transform.position += direction * speed;

                if (Vector3.Distance(entity.transform.position, target) < speed)
                { crashSite(); return; }

                if (Physics.Raycast(entity.transform.position + Vector3.up * 100, Vector3.down, out check, 200, layerS))
                    if (Vector3.Distance(check.point, entity.transform.position) < 3)
                        crashSite();
            }
        }

        private void OnAirdrop(CargoPlane plane, Vector3 dropPosition)
        {
            BaseEntity ent = plane as BaseEntity;

            if (smokeFlag)
            { ent.Kill(); smokeFlag = false; }
        }

        private void crashSite()
        {
            timer.Once(1, () =>
            {
                eventTimer = timer.Every(10, () =>
                {
                    eventDuration -= 10;

                    if (vending)
                        VendingUpdate(eventOwner);

                    for (int i = entityList.Count - 1; i > -1; i--)
                        if (entityList[i] == null)
                        {
                            entityList.Remove(entityList[i]);

                        }

                    if (entityList.Count == 0 && entity == null)
                    {
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                            SendMessage(player, Configuration.endMessage, Configuration.iconID);

                        RemoveEvent();
                    }
                });

            });

            serverHackSeconds = HackableLockedCrate.requiredHackSeconds;

            if (Configuration.crashMessage != null)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    SendMessage(player, Configuration.crashMessage, Configuration.iconID);

            if (Configuration.coordMessage != "")
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    SendMessage(player, Configuration.coordMessage + MapHelper.PositionToString(entity.transform.position), Configuration.iconID);

            Interface.CallHook("CargoPlaneCrashCrashed");
            entityList.Clear();
            Vector3 pos = Vector3.zero;
            crashPosition = entity.transform.position;
            spawnItems.Add(Vector3.zero);
            spawnItems.Clear();

            int airdropsAmount = UnityEngine.Random.Range(Configuration.airdropsAmount, Configuration.airdropsAmountMax + 1);
            int cratesAmount = UnityEngine.Random.Range(Configuration.cratesAmount, Configuration.cratesAmountMax + 1);
            int npcsAmount = UnityEngine.Random.Range(Configuration.npcsAmount, Configuration.npcsAmountMax + 1);

            DurationCalc(airdropsAmount, cratesAmount, npcsAmount);

            spawnItem("assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab", 1, true);
            spawnItem("assets/prefabs/misc/supply drop/supply_drop.prefab", airdropsAmount, false);
            spawnItem("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", cratesAmount, false);
            spawnItem("assets/bundled/prefabs/oilfireballsmall.prefab", fireballsAmount, false);

            SpawnPassenger(passengersAmount);


            if (signalSmoke)
                spawnItem("assets/prefabs/tools/supply signal/grenade.supplysignal.deployed.prefab", 1, false);

            if (expMarker)
            {
                var str = "assets/prefabs/tools/map/explosionmarker.prefab";
                var marker = GameManager.server.CreateEntity(str, entity.transform.position);
                marker.Spawn();
                explosionMarker = marker;

                timer.Once(lifeTimeExpMarker, () => { if (marker) marker.Kill(); });
            }

            if (Configuration.eventMarker)
                DrawMarker();

            Unsubscribe("OnTick");
            entity.Kill();
            buildingBlockTimer = buildingBlockTime;

            RaycastHit hit = new RaycastHit();

            for (int i = 0; i < npcsAmount; i++)
            {
                int s = 0;
                for (int x = 0; x < 22; x++)
                {
                    s = 0;
                    pos = new Vector3(crashPosition.x + UnityEngine.Random.Range(-crashRadius, crashRadius), crashPosition.y, crashPosition.z + UnityEngine.Random.Range(-crashRadius, crashRadius));
                    hit = new RaycastHit();
                    if (Physics.Raycast(pos + Vector3.up * 100f, Vector3.down, out hit, 500f, layerS))
                        pos = hit.point;
                    for (int f = 0; f < spawnItems.Count; f++)
                    {
                        if (Vector3.Distance(pos, spawnItems[f]) < 2)
                            s++;
                    }
                    if (s == 0)
                    {
                        spawnItems.Add(pos);
                        break;
                    }
                }

                if (WaterLevel.GetWaterDepth(hit.point, false, false) < Configuration.spawnDepth)
                {

                    if (s > 0)
                        spawnItems.Add(pos);

                    var npc = GameManager.server.CreateEntity(npcPrefab, pos, new Quaternion(), true);
                    npc.Spawn();


                    entityList.Add(npc);
                    BasePlayer player = npc as BasePlayer;
                    player.name = npcName;
                    player._name = Configuration.displayNameNPC;
                    player._lastSetName = "#Scientist1818";

                    if (i < Configuration.displayNamesListNPC.Count)
                        if (Configuration.displayNamesListNPC[i] != null)
                            player._name = Configuration.displayNamesListNPC[i];


                    ScientistBrain brain = npc.GetComponent<ScientistBrain>();
                    BaseNavigator navigator = npc.GetComponent<BaseNavigator>();
                    NpcBrain brainNPC = npc.gameObject.AddComponent<NpcBrain>();

                    ScientistNPC scientistNPC = npc.gameObject.GetComponent<ScientistNPC>();

                    if (scientistNPC)
                    {
                        scientistNPC.damageScale = npcDamageMultiplier;
                        scientistNPC.aimConeScale = Configuration.npcAccuracy;
                    }

                    TunnelDweller tunnelDweller = npc.gameObject.GetComponent<TunnelDweller>();
                    if (tunnelDweller)
                    {
                        tunnelDweller.damageScale = npcDamageMultiplier;
                        tunnelDweller.aimConeScale = Configuration.npcAccuracy;
                    }

                    UnderwaterDweller underwaterDweller = npc.gameObject.GetComponent<UnderwaterDweller>();
                    if (underwaterDweller)
                    {
                        underwaterDweller.damageScale = npcDamageMultiplier;
                        underwaterDweller.aimConeScale = Configuration.npcAccuracy;
                    }

                    navigator.CanUseNavMesh = false;
                    brain.AttackRangeMultiplier = 10f;
                    brain.VisionCone = -1;
                    brain.SenseRange = npcAttackRange;
                    brain.ListenRange = 15f;
                    brain.SenseTypes = EntityType.Player;
                    brain.MemoryDuration = 5f;
                    brain.TargetLostRange = npcAttackRange;
                    brain.CheckVisionCone = false;
                    brain.CheckLOS = true;
                    brain.IgnoreNonVisionSneakers = true;
                    brain.HostileTargetsOnly = false;
                    brain.IgnoreSafeZonePlayers = false;
                    brain.RefreshKnownLOS = true;
                    navigator.MaxRoamDistanceFromHome = navigator.BestMovementPointMaxDistance = navigator.BestRoamPointMaxDistance = 5f;
                    navigator.DefaultArea = "Walkable";
                    navigator.topologyPreference = ((TerrainTopology.Enum)TerrainTopology.EVERYTHING);
                    navigator.Agent.agentTypeID = -1372625422;
                    if (navigator.CanUseNavMesh) navigator.Init(player, navigator.Agent);

                    brainNPC.homePosition = pos;
                    brainNPC.stateTimer = 5;
                    brainNPC.moveChance = 22;
                    botList.Add(npc);


                    timer.Once(1f, () =>
                                    {
                                        if (npc)
                                            MoveToPosition(brain, navigator, npc.transform.position + Vector3.forward * 0.1f, 0);
                                    });

                    float delay = UnityEngine.Random.Range(0, 3f);
                    timer.Once(delay, () => { if (npc) brainNPC.moveTimer = timer.Every(3f, () => { if (npc) NpcAI(npc); }); });

                    if (npcKit != "")
                    {
                        player.inventory.Strip();
                        Kits?.Call("GiveKit", player, npcKit);
                    }

                    if (npcHealth > 0)
                    { player.startHealth = npcHealth; player.health = npcHealth; }
                    timer.Once(lifeTimeNpcs, () => { if (npc) npc.Kill(); });
                }
                else
                {
                    PrintWarning("NPC position is under water, so NPC will not spawn");
                    continue;
                }

            }
            AddWayPoints(crashPosition, crashRadius);

        }

        public void SpawnPassenger(int amount)
        {
            RaycastHit hit = new();
            for (int i = 0; i < amount; i++)
            {
                Vector3 pos = new Vector3(entity.transform.position.x + UnityEngine.Random.Range(-crashRadius, crashRadius), entity.transform.position.y, entity.transform.position.z + UnityEngine.Random.Range(-crashRadius, crashRadius));
                if (Physics.Raycast(pos + Vector3.up * 150f, Vector3.down, out hit, 500f, layerS))
                    if (hit.point.y > pos.y)
                        pos += Vector3.up * (hit.point.y - pos.y);
                string prefab = botPrefab;

                if (Configuration.passengerNPC)
                    prefab = "assets/rust.ai/agents/npcplayer/npcplayertest.prefab";


                var entityCrash = GameManager.server.CreateEntity(prefab, pos);
                BasePlayer pl = entityCrash as BasePlayer;

                if (!Configuration.passengerNPC)
                    pl._name = "#Passenger1818";

                entityCrash.Spawn();
                entityList.Add(entityCrash);
                pl.inventory.Strip();
                if (kitName != "")
                    Kits?.Call("GiveKit", pl, kitName);

                if (Configuration.passengerNPC)
                {
                    pl._lastSetName = "#Scientist1818";
                    pl._name = passengerName;
                    pl.displayName = passengerName;
                }

                pl.Hurt(9999);

            }
        }

        public void MoveToPosition(ScientistBrain brain, BaseNavigator navigator, Vector3 pos, int moveSpeed)
        {
            var speed = BaseNavigator.NavigationSpeed.Slow;
            if (moveSpeed == 1)
                speed = BaseNavigator.NavigationSpeed.Fast;

            navigator.CanUseNavMesh = true;
            brain.Navigator.SetDestination(pos, speed, 0f, 0f);
            navigator.CanUseNavMesh = false;

        }

        public void NpcAI(BaseEntity npc)
        {
            BaseNavigator navigator = npc.GetComponent<BaseNavigator>();
            ScientistBrain brain = npc.GetComponent<ScientistBrain>();
            NpcBrain x = npc.gameObject.GetComponent<NpcBrain>();
            int speed = 0;

            if (brain.Senses.Players.Count == 0)
            {
                if (x.stateTimer == 0)
                {
                    brain.SwitchToState(AIState.Idle, 0);
                    x.moveChance = 22;
                }

                x.stateTimer--;
            }

            else

            {
                x.stateTimer = coolDownTime;
                speed = 1;
                x.moveChance = 100;

                if (brain.Senses.Players[0])
                    if (Vector3.Distance(brain.Senses.Players[0].transform.position, npc.transform.position) < Configuration.chaseRadius)
                    {
                        BasePlayer player = brain.Senses.Players[0] as BasePlayer;
                        if (!player.IsAlive())
                        {
                            brain.Senses.Players.Remove(brain.Senses.Players[0]);
                            return;
                        }

                        float xx = UnityEngine.Random.Range(-2, 2);
                        float zz = UnityEngine.Random.Range(-2, 2);
                        Vector3 offset = new Vector3(xx, 0, zz);
                        x.targetPosition = brain.Senses.Players[0].transform.position + offset;
                        MoveToPosition(brain, navigator, x.targetPosition, 1);
                        return;
                    }
            }

            if (Vector3.Distance(npc.transform.position, x.homePosition) < homeRadius)
            {
                if (!navigator.Moving)
                {
                    var chanсe = UnityEngine.Random.Range(0, 100);
                    if (chanсe < x.moveChance)
                    {
                        var y = UnityEngine.Random.Range(0, wayPoints.Count);
                        Vector3 point = wayPoints[y] + Vector3.up * 0.5f;
                        RaycastHit check;
                        Vector3 vec = new Vector3(point.x - npc.transform.position.x, point.y - npc.transform.position.y, point.z - npc.transform.position.z);

                        if (!Physics.Raycast(npc.transform.position + Vector3.up * 0.5f, vec, out check, Vector3.Distance(npc.transform.position, point), layerS))
                            x.targetPosition = wayPoints[y];
                        else
                            x.targetPosition = check.point - Vector3.Normalize(vec) * 0.5f;

                        MoveToPosition(brain, navigator, x.targetPosition, speed);
                    }
                }
            }
            else
            if (x.stateTimer < 0)
                MoveToPosition(brain, navigator, x.homePosition, 1);
        }

        private void AddWayPoints(Vector3 center, float radius)
        {
            wayPoints.Clear();
            float off = 1;
            for (float i = -radius / 2 + off; i < radius / 2 + off; i += 1)
                for (float ii = -radius / 2 + off; ii < radius / 2 + off; ii += 1)
                {
                    RaycastHit check;
                    Vector3 offset = new Vector3(i, 0, ii);
                    if (Physics.Raycast(center + offset + Vector3.up * 100, Vector3.down, out check, 200, layerS))
                        if (WaterLevel.GetWaterDepth(check.point, false, false) < Configuration.spawnDepth)
                        {
                            BaseEntity entity = check.GetEntity();
                            if (!entity)
                                wayPoints.Add(check.point);

                        }
                }

        }

        private void spawnItem(string str, int amount, bool effect)
        {
            RaycastHit hit = new RaycastHit();
            for (int i = 0; i < amount; i++)
            {
                Vector3 pos = new Vector3(entity.transform.position.x + UnityEngine.Random.Range(-crashRadius, crashRadius), entity.transform.position.y, entity.transform.position.z + UnityEngine.Random.Range(-crashRadius, crashRadius));
                hit = new RaycastHit();
                if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out hit, 500f, layerS))
                    if (hit.point.y > pos.y)
                        pos += Vector3.up * (hit.point.y - pos.y);
                if (effect)
                    Effect.server.Run(str, entity.transform.position);
                else
                {
                    var entityCrash = GameManager.server.CreateEntity(str, pos);
                    entityCrash.Spawn();
                    entityList.Add(entityCrash);

                    if (str == "assets/prefabs/tools/supply signal/grenade.supplysignal.deployed.prefab")
                    {
                        smokeFlag = true;
                        timer.Once(lifeTimeSignalSmoke, () => { if (entityCrash) entityCrash.Kill(); });
                    }

                    if (str == "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab")
                    {
                        entityCrash.name = "cargoplanecrashcrate";

                        if (Configuration.crateTableName != "")
                            SimpleLootTable?.Call("GetSetItems", entityCrash, Configuration.crateTableName, Configuration.crateMinItems, Configuration.crateMaxItems, 1f);

                        spawnItems.Add(entityCrash.transform.position);
                        HackableLockedCrate ent = entityCrash.GetComponent<HackableLockedCrate>();

                        ent.hackSeconds = serverHackSeconds - Configuration.timerCrates;
                        float oldTime = ent.hackSeconds;
                        TimerForCrate timerForCrate = ent.gameObject.AddComponent<TimerForCrate>();

                        timerForCrate.timer = timer.Every(1, () =>
                            {
                                if (Math.Abs(oldTime - ent.hackSeconds) > 5)
                                    if (ent.hackSeconds == 0 || ent.hackSeconds == 1)
                                    {
                                        ent.hackSeconds = oldTime - 5;
                                        if (ent.hackSeconds < serverHackSeconds - Configuration.timerCrates)
                                            ent.hackSeconds = serverHackSeconds - Configuration.timerCrates;

                                    }
                                    else
                                    {
                                        if (ent.hackSeconds == 0 && Math.Abs(oldTime - ent.hackSeconds) == 0)
                                            ent.hackSeconds -= 5;

                                    }

                                oldTime = ent.hackSeconds;
                                if (!ent)
                                    timerForCrate.timer.Destroy();
                            });

                        timer.Once(lifeTimeCrates, () =>
                            {
                                if (entityCrash)
                                {
                                    ent = entityCrash.GetComponent<HackableLockedCrate>();
                                    if (ent.hackSeconds == serverHackSeconds - Configuration.timerCrates)
                                        ent.Kill();
                                }

                            });
                    }

                    if (entityCrash is SupplyDrop)
                    {
                        entityCrash.name = "cargoplanecrashcrate";

                        if (Configuration.airdropTableName != "")
                            SimpleLootTable?.Call("GetSetItems", entityCrash, Configuration.airdropTableName, Configuration.airdropMinItems, Configuration.airdropMaxItems, 1f);

                        spawnItems.Add(entityCrash.transform.position);
                        SupplyDrop supplyDrop = entityCrash as SupplyDrop;
                        var drop = supplyDrop.GetComponent<Rigidbody>();
                        supplyDrop.RemoveParachute();
                        drop.drag = 1f;
                        timer.Once(lifeTimeAirdrops, () => { if (entityCrash) entityCrash.Kill(); });
                    }
                }

            }
        }

        object OnConstructionPlace(BaseEntity entity, Construction component, Construction.Target constructionTarget, BasePlayer player)
        {
            if (buildingBlock)
                if (buildingBlockTimer > 0)
                    if (Vector3.Distance(player.transform.position, crashPosition) < Configuration.buildingBlockRadius)
                    {
                        NextFrame(() => { if (entity) entity.AdminKill(); });
                        SendMessage(player, "You can't build near the crash site", Configuration.iconID);
                    }

            return null;
        }

        private void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            var killer = info.Initiator as BasePlayer;
            if (killer)
            {
                if (entity.name == npcName)
                    if (Vector3.Distance(entity.transform.position, killer.transform.position) > Configuration.npcMinDistToDmg)
                    {
                        info.damageTypes.ScaleAll(0);
                        BasePlayer player = killer as BasePlayer;
                        if (player && player.IsConnected)
                            SendMessage(player, Configuration.npcFarAwayMessage, Configuration.iconID);
                    }
                    else
                        AddEventOwner(killer);
            }
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container)
                if (container.name != null)
                    if (container.name == "cargoplanecrashcrate")
                    {
                        bool flag = false;
                        if (eventOwner)
                        {
                            if (eventOwner.Team != null)
                                if (eventOwner.Team.members.Contains(player.userID))
                                    flag = true;
                            if (player == eventOwner)
                                flag = true;

                            if (!flag)
                            {
                                SendMessage(player, "You can't loot this one", Configuration.iconID);
                                return false;
                            }

                        }

                        timer.Once(Configuration.removeCratesTime, () =>
                        {
                            if (container)
                                container.Kill();
                        });
                    }

            return null;
        }

        void Unload()
        {
            harmony.UnpatchAll("CargoPlaneCrash");
            RemoveEvent();
        }

        void RemoveEvent()
        {
            Puts("Event ended");
            Interface.CallHook("CargoPlaneCrashEnded");
            KillAll();
        }

        private void KillAll()
        {
            eventOwner = null;

            if (eventTimer != null) eventTimer.Destroy();
            if (dome) dome.Kill();
            if (entity) entity.Kill();
            if (vending) vending.Kill();
            if (explosionMarker) explosionMarker.Kill();

            for (int i = 0; i < entityList.Count; i++)
                if (entityList[i])
                    entityList[i].Kill();
        }

        private void findTarget()
        {
            target = Vector3.zero;
            int index = zoneSequence;

            if (Configuration.selectZoneSequentially)
            {
                zoneSequence++;
                if (zoneSequence > Configuration.zonesList.Count - 1)
                    zoneSequence = 0;
            }
            else
                index = UnityEngine.Random.Range(0, Configuration.zonesList.Count);

            if (Configuration.monumentsProhibition)
                monumentsRadius = 150;
            else
                monumentsRadius = -1;

            for (int i = 0; i < 999; i++)
            {
                calculateTarget(index);

                if (target != Vector3.zero)
                    break;
            }


        }

        private void calculateTarget(int index)
        {
            if (!Configuration.useMultipleZones)
                target = new Vector3(crashZoneCenter.x + UnityEngine.Random.Range(-mapSize, mapSize), 300, crashZoneCenter.z + UnityEngine.Random.Range(-mapSize, mapSize));
            else
            {
                crashZoneCenter = Configuration.zonesList[index].center;
                mapSize = Configuration.zonesList[index].radius / 4;
                target = new Vector3(crashZoneCenter.x + UnityEngine.Random.Range(-mapSize, mapSize), 300, crashZoneCenter.z + UnityEngine.Random.Range(-mapSize, mapSize));
            }

            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(target, Vector3.down, out hit, 500, layerS))
                target = hit.point;

            if (WaterLevel.GetWaterDepth(hit.point, false, false) > 2)
            {
                target = Vector3.zero;
                return;
            }

            foreach (Vector3 item in monumentsList)
                if (Vector3.Distance(target, item) < monumentsRadius)
                {
                    target = Vector3.zero;
                    return;
                }

            if (Configuration.tcProhibition)
                foreach (Vector3 item in tcList)
                    if (Vector3.Distance(target, item) < 100)
                    {
                        target = Vector3.zero;
                        return;
                    }

        }

        private void drawDot(BasePlayer player, Vector3 targetPoint, string str)
        {
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(targetPoint, Vector3.down, out hit, 2000, layerS))
                targetPoint = hit.point;
            player.SendConsoleCommand("ddraw.text", 10f, Color.green, targetPoint, "<size=30>" + str + "</size>");
        }

        private void DrawMarker()
        {
            string markerPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
            marker = GameManager.server.CreateEntity(markerPrefab, crashPosition).GetComponent<MapMarkerGenericRadius>();
            markerPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
            vending = GameManager.server.CreateEntity(markerPrefab, crashPosition).GetComponent<VendingMachineMapMarker>();
            vending.markerShopName = Configuration.markerName;
            vending.enableSaving = false;
            vending.Spawn();
            marker.radius = Configuration.markerRadius;
            marker.alpha = Configuration.markerAlpha;
            marker.color1.r = Configuration.markerColorR;
            marker.color1.g = Configuration.markerColorG;
            marker.color1.b = Configuration.markerColorB;
            marker.enableSaving = false;
            marker.Spawn();
            marker.SetParent(vending);
            marker.transform.localPosition = new Vector3(0, 0, 0);
            marker.SendUpdate();
            vending.SendNetworkUpdate();
            timer.Once(Configuration.markerLifetime, () => { if (vending) vending.Kill(); });

        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (vending != null)
            {
                marker.SendUpdate();
                vending.SendNetworkUpdate();
            }
        }

        private void OnServerShutdown()
        {
            RemoveEvent();
        }

        [Command("cpc_stop")]
        private void cpc_stop(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player || player.IsAdmin)
            {
                RemoveEvent();
            }
        }

        [Command("crashcargoplane")]
        private void crashcargoplane(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player || player.IsAdmin)
                if (entity)
                    crashSite();

        }

        [Command("callcargoplane")]
        private void callcargoplane(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player || player.IsAdmin)
                if (!entity)
                {
                    startEvent();
                }
        }

        [Command("setcrashzonecenter")]
        private void setcrashzonecenter(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (player && player.IsAdmin)
            {
                crashZoneCenter = player.transform.position;
                Configuration = Config.ReadObject<ConfigData>();
                Configuration.crashZoneCenter = crashZoneCenter;
                SaveConfig(Configuration);
                SendMessage(player, "Сenter successfully saved " + crashZoneCenter, Configuration.iconID);
                showcrashzone(player.IPlayer);
            }
        }

        [Command("cpc_add_crashzone")]
        private void cpc_add_crashzone(IPlayer iplayer, string command, string[] args)
        {
            var player = (BasePlayer)iplayer.Object;
            if (player && player.IsAdmin)
            {

                string zoneName = Configuration.zonesList.Count.ToString();
                int zoneRadius = 1000;

                if (args.Length > 0)
                    zoneName = args[0];
                if (args.Length > 1)
                    zoneRadius = int.Parse(args[1]);

                Configuration.zonesList.Add(new MultipleZones { name = zoneName, radius = zoneRadius, center = player.transform.position });
                SaveConfig(Configuration);
                SendMessage(player, "Zone was added successfully " + zoneName, Configuration.iconID);
                if (Configuration.useMultipleZones)
                    showcrashzone(player.IPlayer);
                else
                    PrintWarning("In order to see the zones, please set the setting 'Use multiple zones': true");
            }
        }

        [Command("cpdebug")]
        private void cpdebug(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (player && player.IsAdmin)
            {

                string str = "+";
                if (entity)
                    player.SendConsoleCommand("ddraw.text", 10f, Color.blue, entity.transform.position, "<size=30>" + str + "</size>");
                player.SendConsoleCommand("ddraw.text", 10f, Color.red, target, "<size=30>" + str + "</size>");
                showcrashzone(player.IPlayer);
            }
        }

        [Command("changetarget")]
        private void changetarget(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (player && player.IsAdmin)
                target = player.transform.position;
        }

        [Command("showcrashzone")]
        private void showcrashzone(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            int poitsAmount;
            float pos = mapSize;
            Vector3 tar = target;
            Vector3 center = target;


            if (player && player.IsAdmin)
            {
                if (!Configuration.useMultipleZones)
                {
                    poitsAmount = (int)mapSize / 8;
                    for (int i = 0; i < poitsAmount + 1; i++)
                    {
                        drawDot(player, crashZoneCenter, "+");
                        tar = new Vector3(crashZoneCenter.x - pos + i * pos / poitsAmount * 2, 1000, crashZoneCenter.z - pos);
                        drawDot(player, tar, "o");
                        tar = new Vector3(crashZoneCenter.x - pos + i * pos / poitsAmount * 2, 1000, crashZoneCenter.z + pos);
                        drawDot(player, tar, "o");
                        tar = new Vector3(crashZoneCenter.x - pos, 1000, crashZoneCenter.z - pos + i * pos / poitsAmount * 2);
                        drawDot(player, tar, "o");
                        tar = new Vector3(crashZoneCenter.x + pos, 1000, crashZoneCenter.z - pos + i * pos / poitsAmount * 2);
                        drawDot(player, tar, "o");
                    }
                }
                else
                {
                    for (int f = 0; f < Configuration.zonesList.Count; f++)
                    {
                        center = Configuration.zonesList[f].center;
                        pos = Configuration.zonesList[f].radius / 4;
                        poitsAmount = (int)pos / 6;

                        for (int i = 0; i < poitsAmount + 1; i++)
                        {
                            drawDot(player, center, Configuration.zonesList[f].name);
                            tar = new Vector3(center.x - pos + i * pos / poitsAmount * 2, 1000, center.z - pos);
                            drawDot(player, tar, "o");
                            tar = new Vector3(center.x - pos + i * pos / poitsAmount * 2, 1000, center.z + pos);
                            drawDot(player, tar, "o");
                            tar = new Vector3(center.x - pos, 1000, center.z - pos + i * pos / poitsAmount * 2);
                            drawDot(player, tar, "o");
                            tar = new Vector3(center.x + pos, 1000, center.z - pos + i * pos / poitsAmount * 2);
                            drawDot(player, tar, "o");
                        }
                    }
                }
            }
        }

        private Vector3 GroundPos(Vector3 pos)
        {
            RaycastHit check;
            float y = pos.y;

            if (Physics.Raycast(pos + Vector3.up * 500, Vector3.down, out check, 999, layerS))
                y = check.point.y;

            return new Vector3(pos.x, y, pos.z);
        }

        private void AddEventOwner(BaseEntity entity)
        {
            if (eventOwner || !Configuration.pveMode)
                return;

            BasePlayer player = entity as BasePlayer;

            if (player)
                if (player.IsConnected)
                {
                    eventOwner = player;
                    VendingUpdate(player);
                    if (Configuration.createSphere)
                    {
                        AddEventSphere();
                        if (Vector3.Distance(player.transform.position, dome.transform.position) > Configuration.eventRadius / 2f)
                            StartOwnerTimer(player);
                    }
                }

        }

        public void StartOwnerTimer(BasePlayer player)
        {
            if (player != eventOwner)
                return;

            SendMessage(player, ownerLeaveMessage, iconID);

            if (ownerTimerRemain < 0)
                ownerTimerRemain = ownerLeftTime;
        }

        private void StopOwnerTimer(BasePlayer player)
        {
            if (player != eventOwner)
                return;

            SendMessage(player, enterMessage, iconID);

            ownerTimerRemain = -1;
        }

        private void SelectChangeOwner()
        {
            if (teamOwners)
                ChangeOwner();
            else
            {
                eventOwner = null;
                if (dome) dome.Kill();
            }
        }

        private void AddEventSphere()
        {
            Vector3 pos = crashPosition;
            float radius = Configuration.eventRadius;
            double x;
            double z;
            points.Clear();

            for (float i = 0; i < 6.283185307f; i += 12f / radius)
            {
                x = pos.x + radius / 1.9f * Math.Cos(i);
                z = pos.z + radius / 1.9f * Math.Sin(i);

                Vector3 point = GroundPos(new Vector3(Convert.ToSingle(x), pos.y, Convert.ToSingle(z))) + Vector3.up;
                points.Add(point);
            }

            SphereEntity sphereEntity = GameManager.server.CreateEntity(StringPool.Get(3211242734), pos, Quaternion.identity) as SphereEntity;
            sphereEntity.EnableSaving(false);
            sphereEntity.EnableGlobalBroadcast(false);
            sphereEntity.Spawn();

            for (int i = 0; i < Configuration.transparencySphere; i++)
            {
                var sphere = GameManager.server.CreateEntity(StringPool.Get(3211242734), pos) as SphereEntity;
                sphere.Spawn();
                sphere.SetParent(sphereEntity);
                sphere.transform.localPosition = Vector3.zero;
                sphere.currentRadius = 1;
                sphere.lerpRadius = 1;
                sphere.UpdateScale();
                sphere.EnableSaving(false);
                sphere.EnableGlobalBroadcast(false);
                sphere.SendNetworkUpdateImmediate();
            }

            sphereEntity.currentRadius = radius;
            sphereEntity.lerpRadius = radius;
            sphereEntity.UpdateScale();
            sphereEntity.SendNetworkUpdateImmediate();


            if (dome != null) dome.Kill();
            dome = sphereEntity;

            AddTrigger.AddToEntity(sphereEntity);
        }

        public class AddTrigger : MonoBehaviour
        {
            public static void AddToEntity(BaseEntity entity)
            {
                SphereCollider collider = entity.gameObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                entity.gameObject.layer = (int)Rust.Layer.Reserved1;
                entity.gameObject.AddComponent<CollisionListener>();
            }
        }

        public class CollisionListener : MonoBehaviour
        {
            CargoPlaneCrash cargoPlaneCrash = new();

            public void OnTriggerEnter(Collider collider)
            {
                BasePlayer player = collider?.ToBaseEntity()?.ToPlayer();

                if (player == null || !player.IsConnected)
                    return;


                cargoPlaneCrash.StopOwnerTimer(player);
            }

            public void OnTriggerExit(Collider collider)
            {
                try
                {
                    BasePlayer player = collider?.ToBaseEntity()?.ToPlayer();

                    if (player == null || !player.IsConnected)
                        return;

                    cargoPlaneCrash.StartOwnerTimer(player);
                }
                catch { }
            }

            public void OnTriggerStay(Collider collider)
            {
                if (!ejectPlayers)
                    return;

                BasePlayer player = collider?.ToBaseEntity()?.ToPlayer();

                if (player == null)
                    return;

                if (!eventOwner)
                    return;

                if (player.IsAdmin && adminDomeAllow)
                    return;

                if (player == eventOwner || !player.IsConnected)
                    return;

                if (eventOwner.Team != null)
                    if (eventOwner.Team.members.Contains(player.userID))
                        return;

                cargoPlaneCrash.SendMessage(player, ejectMessage, iconID);

                Vector3 ejectPoint = CargoPlaneCrash.points[0];

                foreach (Vector3 point in CargoPlaneCrash.points)
                    if (Vector3.Distance(player.transform.position, point) < Vector3.Distance(player.transform.position, ejectPoint))
                        ejectPoint = point;

                player.EnsureDismounted();
                player.MovePosition(ejectPoint);
                player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
                player.SendNetworkUpdateImmediate();
            }
        }

        private void VendingUpdate(BasePlayer player)
        {
            if (vending)
            {
                vending.markerShopName = markerName;

                if (player != null)
                    vending.markerShopName = markerName + "(" + eventOwner.displayName + ")";

                if (Configuration.displayDuration && eventDuration > 0)
                    vending.markerShopName += "(" + (int)eventDuration / 60 + "m" + eventDuration % 60 + "s)";

                vending.SendNetworkUpdate();
            }

        }

        private void OnEntitySpawned(BuildingPrivlidge entity)
        {
            tcList.Add(entity.transform.position);
        }

        private void OnEntityKill(BuildingPrivlidge entity)
        {
            tcList.Remove(entity.transform.position);
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity._name == "#Passenger1818") return true;

            return null;
        }




        private void OnPlayerDisconnected(BasePlayer player)
        {
            try
            {
                if (player == null || eventOwner == null || player != eventOwner || !Configuration.pveMode)
                    return;

                StartOwnerTimer(player);
            }
            finally { }
        }

        private void ChangeOwner()
        {
            if (eventOwner == null)
                return;

            BasePlayer owner = eventOwner;
            eventOwner = null;

            if (owner.Team != null)
                if (owner.Team.members.Count > 1)
                    foreach (BasePlayer findPlayer in BasePlayer.activePlayerList)
                        if (owner.Team.members.Contains(findPlayer.userID) && findPlayer.IsConnected && findPlayer != owner)
                        {
                            if (dome)
                            {
                                if (Vector3.Distance(findPlayer.transform.position, dome.transform.position) < eventRadius / 2f)
                                {
                                    eventOwner = findPlayer;
                                    break;
                                }
                            }
                        }

            VendingUpdate(eventOwner);

            if (eventOwner == null)
                if (dome) dome.Kill();
        }
    }
} 