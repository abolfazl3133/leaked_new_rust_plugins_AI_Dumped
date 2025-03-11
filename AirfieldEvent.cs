using System;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using HarmonyLib;


namespace Oxide.Plugins
{
    [Info("AirfieldEvent", "Fruster", "1.4.9")]
    [Description("AirfieldEvent")]
    class AirfieldEvent : CovalencePlugin
    {
        public static List<Vector3> points = new List<Vector3>();
        private BaseEntity planeEntity;
        private BaseEntity chinookDelivery;
        private Vector3 airfieldPosition;
        private Quaternion airfieldRotation;
        private List<Vector3> planeWayPoints = new List<Vector3>();
        private int stage;
        private float rotationSpeed = 1.3f;
        private float speed;
        private float maxSpeed = 5;
        private Vector3 dir;
        private Quaternion oldRot;
        private bool fix;
        private string npcPrefab;
        private const string supplyPrefab = "assets/prefabs/misc/supply drop/supply_drop.prefab";
        private const string cratePrefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        private int lifeTimeCrates;
        private int lifeTimeSupply;
        private int lifeTimeNpcs;
        private int remainToEvent;
        private int remain;
        private int typeNpcs;
        private int npcHealth;
        private bool eventFlag;
        private bool callHeli;
        private int patrolTime;
        private bool eventMarker;
        private bool afterLanding;
        private int markerLifetime;
        private float markerAlpha;
        private float markerRadius;
        private string markerName;
        private bool npcForceFreeze;
        private Color markerColor = Color.red;
        private MapMarkerGenericRadius marker;
        private VendingMachineMapMarker vending;
        private float npcDamageMultiplier;
        private const string npcName = "afeguardnpc";
        private const string heliName = "afeHelicopter";
        private ConfigData Configuration;
        private bool randomEventTime;
        private int minimumRemainToEvent;
        private int maximumRemainToEvent;
        private float heliDamageMultiplier;
        private float heliHealth;
        [PluginReference] Plugin Kits, SimpleLootTable, ZoneManager, Notify, BetterNPC;
        private List<BaseEntity> entityList = new List<BaseEntity>();
        private int xSum;
        private List<string> kitsListNPC;
        private float npcAttackRange;
        private const int layerS = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private const int TARGET_LAYERS = ~(1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private List<List<Vector3>> wayPointsList = new List<List<Vector3>>();
        private List<List<BaseEntity>> botList = new List<List<BaseEntity>>();
        private int coolDownTime = 10;
        private int homeRadius = 20;
        private Timer eventTimer;
        private Timer fastTimer;
        private BaseEntity patrolHelicopter;
        private PatrolHelicopter patrolHelicopterH;
        private PatrolHelicopterAI patrolHelicopterAI;
        private int patrolHelicopterTime;
        private int patrolHelicopterReplaceTime;
        public GameObject mon = new GameObject();
        private class NpcBrain : FacepunchBehaviour
        {
            public Vector3 homePosition;
            public Vector3 targetPosition;
            public BaseEntity target;
            public int stateTimer;
            public int moveChance;
        }

        private class CH47Bradley : FacepunchBehaviour
        {
            public Vector3 targetPosition;
            public Timer mainTimer;
            public Timer secondTimer;
        }

        private class OwnerTimer : FacepunchBehaviour
        {
            public Timer timer;
        }

        private class TimerForCrate : FacepunchBehaviour
        {
            public Timer timer;
        }
        private float serverHackSeconds = HackableLockedCrate.requiredHackSeconds;
        public static BasePlayer eventOwner;
        public static SphereEntity spherePVE;
        public static string enterMessage;
        public static string ejectMessage;
        public static string ownerLeaveMessage;
        public static int typeEnterMessage;
        public static int typeEjectMessage;
        public static int typeOwnerLeaveMessage;
        public static bool ejectPlayers;
        public static bool useNotify;
        public static int eventRadius;
        public static int ownerLeftTime;
        public static bool teamOwners;
        public static bool adminDomeAllow;
        class ConfigData
        {
            [JsonProperty("PVE mode (crates can only be looted by the player who first dealt damage to the NPC)")]
            public bool pveMode = false;
            [JsonProperty("Time after which the owner of the event will be deleted if he left the dome or left the server (for PVE mode)")]
            public int ownerLeftTime = 300;
            [JsonProperty("Give event ownership to the owner's teammates if he is no longer the owner. Only if teammates are within the event radius (for PVE mode)")]
            public bool teamOwners = true;
            [JsonProperty("Radius for event(for PVE mode)")]
            public int eventRadius = 380;
            [JsonProperty("Create a dome for PVE mode")]
            public bool createSphere = false;
            [JsonProperty("Dome transparency (the higher the value, the darker the dome, recommended 4)")]
            public int transparencySphere = 4;
            [JsonProperty("Dome offset")]
            public Vector3 sphereOffset = new Vector3(0, 0, 30);
            [JsonProperty("Message when a player enters the event dome(only for PVE mode if there is a dome)")]
            public string enterMessage = "You have entered the Airfield Event";
            [JsonProperty("Message when the event owner leaves the event dome (only for PVE mode if there is a dome)")]
            public string ownerLeaveMessage = "Return to the event dome, otherwise after 300 seconds you will no longer be the owner of this event";

            [JsonProperty("Do not allow other players into the event(only for PVE mode if there is a dome)")]
            public bool ejectPlayers = false;
            [JsonProperty("Message when a player is ejected from the event dome(only for PVE mode if there is a dome)")]
            public string ejectMessage = "You cannot be here, you are not the owner of this event";
            [JsonProperty("Allow admin to be in event dome (only for PVE mode if there is a dome)")]
            public bool adminDomeAllow = true;

            [JsonProperty("Triggering an event by timer (disable if you want to trigger the event only manually)")]
            public bool timerStart = true;
            [JsonProperty("Time to event start(in seconds, minimum 900)")]
            public int remainToEvent = 3900;
            [JsonProperty("Random time to event start")]
            public bool randomEventTime = false;
            [JsonProperty("Minimum time to event start(in seconds, minimum 900)")]
            public int minimumRemainToEvent = 900;
            [JsonProperty("Maximum time to event start(in seconds)")]
            public int maximumRemainToEvent = 10800;
            [JsonProperty("Minimum number of online players to trigger an event")]
            public int minOnline = 1;
            [JsonProperty("Minimum drops amount(minimum number of cargo spawns after plane landing, should not be less than 1)")]
            public int dropsAmount = 2;
            [JsonProperty("Maximum drops amount(maximum number of cargo spawns after plane landing, should not be less than 1, maximum 10)")]
            public int maxDropsAmount = 4;
            [JsonProperty("Minimum crates amount(spawn every cargo drop)")]
            public int cratesAmount = 1;
            [JsonProperty("Maximum crates amount(spawn every cargo drop)")]
            public int maxCratesAmount = 1;
            [JsonProperty("Crate simple loot table name(plugin SimpleLootTable is required)")]
            public string crateTableName = "";
            [JsonProperty("Minimum number of items in a crate(plugin SimpleLootTable is required)")]
            public int crateMinItems = 0;
            [JsonProperty("Maximum number of items in a crate(plugin SimpleLootTable is required)")]
            public int crateMaxItems = 0;
            [JsonProperty("Remove crates after being looted by a player(in seconds)")]
            public int removeCratesTime = 300;
            [JsonProperty("Extend the duration of the event if the NPCs were attacked(if there is less time left, then increase to the set time(in seconds))")]
            public int eventExtDuration = 600;
            [JsonProperty("Crates lifetime(in seconds). The crate will not be destroyed if it has been activated")]
            public int lifeTimeCrates = 3600;
            [JsonProperty("Crates timer(in seconds)")]
            public int timerCrates = 900;
            [JsonProperty("Minimum airdrops amount(spawn every cargo drop)")]
            public int supplyAmount = 1;
            [JsonProperty("Maximum airdrops amount(spawn every cargo drop)")]
            public int maxSupplyAmount = 1;
            [JsonProperty("Airdrop simple loot table name(plugin SimpleLootTable is required)")]
            public string airdropTableName = "";
            [JsonProperty("Minimum number of items in an airdrop(plugin SimpleLootTable is required)")]
            public int airdropMinItems = 0;
            [JsonProperty("Maximum number of items in an airdrop(plugin SimpleLootTable is required)")]
            public int airdropMaxItems = 0;
            [JsonProperty("Airdrops lifetime(in seconds)")]
            public int lifeTimeSupply = 3600;
            [JsonProperty("Minimum NPCs amount(spawn every cargo drop)")]
            public int npcsAmount = 1;
            [JsonProperty("Maximum NPCs amount(spawn every cargo drop)")]
            public int maxNpcsAmount = 2;
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
            [JsonProperty("Forcibly immobilize an NPC")]
            public bool npcForceFreeze = false;
            [JsonProperty("Method of distribution of kits for NPCs(1 - sequentially, 2 - repeating, 3 - randomly)")]
            public int kitsDistribution = 1;
            [JsonProperty("List of kits for NPC(requires Kits plugin)")]
            public List<string> kitsListNPC = new List<string> { "kit1", "kit2", "kit3" };
            [JsonProperty("Default displayName for NPC(for SimpleKillFeed/DeathNotes plugin)")]
            public string displayNameNPC = "Airfield NPC";
            [JsonProperty("List of displayNames for each NPC(for SimpleKillFeed/DeathNotes plugin)")]
            public List<string> displayNamesListNPC = new List<string> { "Airfield NPC1", "Airfield NPC2", "Airfield NPC3" };
            [JsonProperty("Event message(if empty, no message will be displayed)")]
            public string message = "Airfield event started";
            [JsonProperty("Event end message(if empty, no message will be displayed)")]
            public string endMessage = "Airfield event ended";
            [JsonProperty("Landing message(displayed when the cargo plane has landed)")]
            public string landingMessage = "Cargoplane landed at Airfield";
            [JsonProperty("Patrol helicopter spawn chance (0 - 100%)")]
            public int heliChance = 50;
            [JsonProperty("Call the helicopter only after activating the hackable crate")]
            public bool heliAfterHack = false;
            [JsonProperty("How long the helicopter will patrol the airfield (in minutes)")]
            public int patrolTime = 5;
            [JsonProperty("Helicopter damage multiplier")]
            public float heliDamageMultiplier = 1;
            [JsonProperty("Helicopter health")]
            public float heliHealth = 10000;
            [JsonProperty("Helicopter main rotor health")]
            public float heliMainRotorHealth = 900;
            [JsonProperty("Helicopter tail rotor health")]
            public float heliTailRotorHealth = 500;
            [JsonProperty("The patrol helicopter will not patrol the airfield if it has found a target")]
            public bool heliChangeBehavior = true;
            [JsonProperty("Make helicopter fly away after end of patrol time")]
            public bool heliAway = false;
            [JsonProperty("Spawns a helicopter right on the airfield(if false, then the helicopter will arrive from afar in a few seconds)")]
            public bool heliSpawnNear = false;
            [JsonProperty("Helicopter patrol range")]
            public int heliPatrolRange = 150;
            [JsonProperty("Event marker on the map(will spawn a marker immediately after the start of the event)")]
            public bool eventMarker = false;
            [JsonProperty("If true, spawn the marker only after the plane lands")]
            public bool afterLanding = false;
            [JsonProperty("Event marker name")]
            public string markerName = "Airfield event";
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
            [JsonProperty("Use a custom place to land a cargo plane")]
            public bool customLanding = false;
            [JsonProperty("Custom place position")]
            public Vector3 customPosition = Vector3.zero;
            [JsonProperty("Custom place rotation")]
            public Vector3 customRotation = Vector3.zero;
            [JsonProperty("Use custom navmesh (enable if using custom airfield and getting NPC navmesh error)")]
            public bool customNavmesh = false;
            [JsonProperty("BradleyAPC spawn chance (0 - 100%) If you are not using a custom map, correct operation of BradleyAPC is not guaranteed. Use the default airfield or a landing spot that is similar to the default airfield")]
            public int bradleyAPCspawnChance = 0;
            [JsonProperty("BradleyAPC lifetime(in seconds)")]
            public int BradleyAPClifetime = 3600;
            [JsonProperty("BradleyAPC bullet damage")]
            public float BradleyAPCbulletDamage = 7f;
            [JsonProperty("BradleyAPC health")]
            public float BradleyAPChealth = 1000f;
            [JsonProperty("Use Notify plugin for messages")]
            public bool useNotify = false;
            [JsonProperty("Type notify for 'Message when a player enters the event dome'(only for Notify plugin)")]
            public int typeEnterMessage = 0;
            [JsonProperty("Type notify for 'Message when the event owner leaves the event dome'(only for Notify plugin)")]
            public int typeLeaveMessage = 0;
            [JsonProperty("Type notify for 'Message when a player is ejected from the event dome'(only for Notify plugin)")]
            public int typeEjectMessage = 0;
            [JsonProperty("Type notify for 'Message if the player attacks far away NPCs'(only for Notify plugin)")]
            public int typeFarAwayMessage = 0;
            [JsonProperty("Type notify for 'Event message'(only for Notify plugin)")]
            public int typeEventMessage = 0;
            [JsonProperty("Type notify for 'Event end message'(only for Notify plugin)")]
            public int typeEndMessage = 0;
            [JsonProperty("Type notify for 'Landing message'(only for Notify plugin)")]
            public int typeLandingMessage = 0;
        }

        [HarmonyPatch(typeof(ScientistNPC))]
        [HarmonyPatch("displayName", MethodType.Getter)]
        public static class DisplayNameScientistAFE
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
        public static class DisplayNameTunnelDwellerAFE
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
        public static class DisplayNameUnderwaterDwellerAFE
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

        private Harmony harmony = new Harmony("AirfieldEvent");

        private void OnServerInitialized()
        {
            harmony.UnpatchAll("AirfieldEvent");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            UnsubscribeAll();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                AddOwnerTimerForPlayer(player);

            LoadConfig();
            eventFlag = false;
            remainToEvent = Configuration.remainToEvent;
            if (remainToEvent < 900) remainToEvent = 900;
            remain = remainToEvent;
            lifeTimeCrates = Configuration.lifeTimeCrates;
            lifeTimeSupply = Configuration.lifeTimeSupply;
            lifeTimeNpcs = Configuration.lifeTimeNpcs;
            typeNpcs = Configuration.typeNpcs;
            patrolTime = Configuration.patrolTime;
            npcHealth = Configuration.npcHealth;
            npcForceFreeze = Configuration.npcForceFreeze;
            eventMarker = Configuration.eventMarker;
            afterLanding = Configuration.afterLanding;
            markerName = Configuration.markerName;
            markerLifetime = Configuration.markerLifetime;
            markerAlpha = Configuration.markerAlpha;
            markerRadius = Configuration.markerRadius;
            markerColor.r = Configuration.markerColorR;
            markerColor.g = Configuration.markerColorG;
            markerColor.b = Configuration.markerColorB;
            npcDamageMultiplier = Configuration.npcDamageMultiplier;
            randomEventTime = Configuration.randomEventTime;
            minimumRemainToEvent = Configuration.minimumRemainToEvent;
            maximumRemainToEvent = Configuration.maximumRemainToEvent;
            heliHealth = Configuration.heliHealth;
            heliDamageMultiplier = Configuration.heliDamageMultiplier;
            kitsListNPC = Configuration.kitsListNPC;
            npcAttackRange = Configuration.npcAttackRange;
            ejectMessage = Configuration.ejectMessage;
            enterMessage = Configuration.enterMessage;
            ownerLeaveMessage = Configuration.ownerLeaveMessage;
            useNotify = Configuration.useNotify;
            typeEjectMessage = Configuration.typeEjectMessage;
            typeEnterMessage = Configuration.typeEnterMessage;
            typeOwnerLeaveMessage = Configuration.typeLeaveMessage;
            ejectPlayers = Configuration.ejectPlayers;
            eventRadius = Configuration.eventRadius;
            ownerLeftTime = Configuration.ownerLeftTime;
            teamOwners = Configuration.teamOwners;
            adminDomeAllow = Configuration.adminDomeAllow;

            GameObject monTemp = new GameObject();

            if (Configuration.customLanding)
            {
                monTemp.transform.position = Configuration.customPosition;
                monTemp.transform.rotation = Quaternion.Euler(Configuration.customRotation.x, Configuration.customRotation.y, Configuration.customRotation.z);
                eventFlag = true;
            }
            else
                foreach (var monument in TerrainMeta.Path.Monuments)
                    if (monument.name == "assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab")
                    {
                        monTemp.transform.position = monument.transform.position;
                        monTemp.transform.rotation = monument.transform.rotation;
                        eventFlag = true;
                        break;
                    }

            CreateWaypoints(monTemp);

            mon = monTemp;

            if (randomEventTime)
                remainToEvent = UnityEngine.Random.Range(minimumRemainToEvent, maximumRemainToEvent);

            if (!eventFlag)
                Puts("Airfield not found on map, event will not start! You can set up a custom landing spot for your cargo plane");
            else
            {
                if (Configuration.timerStart)
                {
                    if (!Configuration.customLanding)
                        Puts("Airfield found on map, event will start in " + remainToEvent.ToString() + " seconds");
                    else
                        Puts("Cargoplane will land at a custom airfield, event will start in " + remainToEvent.ToString() + " seconds");
                }
                else
                    Puts("The event will be triggered only in manual mode");
            }


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

            if (eventFlag)
            {
                if (Configuration.timerStart)
                    timer.Every(1f, () =>
                        {
                            remain--;
                            if (remain < 0)
                            {
                                if (planeEntity == null)
                                    if (BasePlayer.activePlayerList.Count > Configuration.minOnline - 1)
                                        EventStart();
                                    else
                                        Puts("Not enough online players on the server, event will not start!");
                                else
                                    Puts("The event is already running");

                                if (randomEventTime)
                                    remainToEvent = UnityEngine.Random.Range(minimumRemainToEvent, maximumRemainToEvent);

                                remain = remainToEvent;

                                Puts("Next event will start in " + remainToEvent.ToString() + " seconds");
                            }
                        });

                timer.Every(30f, () =>
                    {
                        if (spherePVE != null && entityList.Count == 1)
                            spherePVE.Kill();

                        for (int i = entityList.Count - 1; i > -1; i--)
                            if (entityList[i] == null)
                            {
                                entityList.Remove(entityList[i]);

                                if (entityList.Count == 0)
                                {
                                    Interface.CallHook("AirfieldEventEnded");

                                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                                        if (!useNotify)
                                            player.ChatMessage(Configuration.endMessage);
                                        else
                                            Notify?.Call("SendNotify", player, Configuration.typeEndMessage, Configuration.endMessage);

                                    Puts("Event ended");
                                    UnsubscribeAll();

                                    if (vending) vending.Kill();
                                }
                            }
                    });

                timer.Every(10f, () =>
                 {
                     patrolHelicopterTime--;
                     patrolHelicopterReplaceTime--;

                     if (vending)
                         VendingUpdate(eventOwner);

                     if (patrolHelicopter)
                     {

                         if (patrolHelicopterTime > 0)
                         {
                             int range = Configuration.heliPatrolRange;
                             if (patrolHelicopterAI._targetList.Count == 0 || !Configuration.heliChangeBehavior)
                             {
                                 patrolHelicopterAI.SetTargetDestination(airfieldPosition + new Vector3(UnityEngine.Random.Range(-range, range), 25f, UnityEngine.Random.Range(-range, range)), 5f, 5f);
                             }

                             if (Vector3.Distance(airfieldPosition, patrolHelicopter.transform.position) > 300)
                             {
                                 patrolHelicopterAI._targetList.Clear();
                                 patrolHelicopterAI.SetTargetDestination(airfieldPosition + new Vector3(UnityEngine.Random.Range(-range, range), 25f, UnityEngine.Random.Range(-range, range)), 5f, 5f);
                             }

                             if (patrolHelicopterReplaceTime == 0 && patrolHelicopterAI._currentState.ToString() != "DEATH")
                             {
                                 Vector3 pos = patrolHelicopter.transform.position;

                                 timer.Repeat(1f, 3, () =>
                                    {
                                        patrolHelicopterAI.SetTargetDestination(pos, 1f, 1f);
                                    });

                                 timer.Once(3.1f, () =>
                                     {
                                         var heli = (PatrolHelicopter)GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", patrolHelicopter.transform.position, patrolHelicopter.transform.rotation, true);
                                         var heliAI = heli.GetComponent<PatrolHelicopterAI>();
                                         heliAI.SetInitialDestination(patrolHelicopter.transform.position, 0f);
                                         heli.transform.position = patrolHelicopter.transform.position;
                                         heli.transform.rotation = patrolHelicopter.transform.rotation;
                                         heli.Spawn();
                                         heliAI._targetList = patrolHelicopterAI._targetList;
                                         PatrolHelicopter patrolHelicopterTemp = patrolHelicopter as PatrolHelicopter;

                                         heli.weakspots[0].maxHealth = Configuration.heliMainRotorHealth;
                                         heli.weakspots[0].health = patrolHelicopterTemp.weakspots[0].health;
                                         heli.weakspots[1].maxHealth = Configuration.heliTailRotorHealth;
                                         heli.weakspots[1].health = patrolHelicopterTemp.weakspots[1].health;
                                         heli.InitializeHealth(patrolHelicopterTemp.health, Configuration.heliHealth);
                                         heli.name = heliName;
                                         patrolHelicopter = heli;
                                         patrolHelicopterH = heli;
                                         patrolHelicopterAI = heliAI;
                                         entityList.Add(heli);
                                         entityList.Remove(patrolHelicopterTemp);
                                         patrolHelicopterTemp.Kill();
                                         patrolHelicopterReplaceTime = 90;
                                     });
                             }
                         }



                         if (Configuration.heliAway && patrolHelicopterTime == 0)
                         {
                             patrolHelicopterAI.SetTargetDestination(airfieldPosition + new Vector3(-99999, 50f, 0), 5f, 5f);
                             timer.Once(180, () =>
                               {
                                   if (patrolHelicopter)
                                       patrolHelicopter.Kill();
                               });
                         }
                     }
                 });

            }

            foreach (var item in BaseNetworkable.serverEntities)
            {
                if (item == null) continue;
                if (item.name == "AirfieldEventCH47")
                    RemoveChinook(item as BaseEntity);
            }

        }

        void SaveConfig(object config) => Config.WriteObject(config, true);

        void LoadConfig()
        {
            base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            Configuration = Config.ReadObject<ConfigData>();
            SaveConfig(Configuration);
        }

        private void CreateWaypoints(GameObject monument)
        {
            dir = monument.transform.position - monument.transform.right * 1000f - monument.transform.up * -2f - monument.transform.forward * 43.5f;
            airfieldPosition = monument.transform.position;
            airfieldRotation = monument.transform.rotation;
            planeWayPoints.Add(monument.transform.position - monument.transform.right - monument.transform.up * -1200f - monument.transform.forward * 1500f);
            planeWayPoints.Add(monument.transform.position - monument.transform.right * -4500 - monument.transform.up * -1200f - monument.transform.forward * 1300f);
            planeWayPoints.Add(monument.transform.position - monument.transform.right * -3700 - monument.transform.up * -1200f - monument.transform.forward * 550f);
            planeWayPoints.Add(monument.transform.position - monument.transform.right * -3000 - monument.transform.up * -900f - monument.transform.forward * 43.5f);

            for (float i = -2100; i < -150f; i += 78.64f)
                planeWayPoints.Add(monument.transform.position - monument.transform.right * i - monument.transform.up * i / 2.6f - monument.transform.forward * 43.5f - Vector3.up * 45f);

            planeWayPoints.Add(monument.transform.position - monument.transform.right * -100f - monument.transform.up * -4f - monument.transform.forward * 43.5f);
            planeWayPoints.Add(monument.transform.position - monument.transform.right * 80f - monument.transform.up * -2f - monument.transform.forward * 43.5f);

            for (float i = 160f; i < 2100; i += 78.64f)
                planeWayPoints.Add(monument.transform.position - monument.transform.right * i + monument.transform.up * i / 2.6f - monument.transform.forward * 43.5f - Vector3.up * 45f);

            planeWayPoints.Add(monument.transform.position - monument.transform.right * 12000f - monument.transform.up * -700f - monument.transform.forward * 43.5f);
            planeWayPoints.Add(monument.transform.position - monument.transform.right * 13000f - monument.transform.up * -700f - monument.transform.forward * 43.5f);

        }

        private void EventCalc()
        {
            if (planeEntity)
            {
                if (Vector3.Distance(planeEntity.transform.position, planeWayPoints[stage]) < speed)
                { stage++; ChangeSpeed(stage); }

                planeEntity.transform.rotation = oldRot;
                Vector3 lTargetDir = planeWayPoints[stage] - planeEntity.transform.position;
                planeEntity.transform.rotation = Quaternion.RotateTowards(planeEntity.transform.rotation, Quaternion.LookRotation(lTargetDir), rotationSpeed);
                var direction = planeEntity.transform.forward;
                planeEntity.transform.position += direction * speed;
                oldRot = planeEntity.transform.rotation;
                if (fix)
                    planeEntity.transform.LookAt(dir);
            }
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container)
                if (container.name != null)
                    if (container.name == "airfieldcrate")
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
                                player.ChatMessage("You can't loot this one");
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

        void DrawMarker()
        {
            string markerPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
            marker = GameManager.server.CreateEntity(markerPrefab, airfieldPosition).GetComponent<MapMarkerGenericRadius>();
            markerPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
            vending = GameManager.server.CreateEntity(markerPrefab, airfieldPosition).GetComponent<VendingMachineMapMarker>();
            vending.markerShopName = markerName;
            vending.enableSaving = false;
            vending.Spawn();
            marker.radius = markerRadius;
            marker.alpha = markerAlpha;
            marker.color1 = markerColor;
            marker.enableSaving = false;
            marker.Spawn();
            marker.SetParent(vending);
            marker.transform.localPosition = new Vector3(0, 0, 0);
            marker.SendUpdate();
            vending.SendNetworkUpdate();
            timer.Once(markerLifetime, () => { if (vending) vending.Kill(); });

        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        }

        private PatrolHelicopter CallHeli(Vector3 destination)
        {
            float x = 0.25f;

            if (Configuration.heliSpawnNear)
            {
                destination = airfieldPosition;
                x = 0;
            }

            var heli = (PatrolHelicopter)GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", new Vector3(), new Quaternion(), true);
            PatrolHelicopterAI heliAI = heli.GetComponent<PatrolHelicopterAI>();
            heliAI.SetInitialDestination(destination, x);
            heli.Spawn();
            patrolHelicopter = heli;
            patrolHelicopterH = heli;
            patrolHelicopterAI = heliAI;
            patrolHelicopterTime = Configuration.patrolTime * 6;
            patrolHelicopterReplaceTime = 90;
            entityList.Add(heli);

            heli.name = heliName;

            heli.weakspots[0].maxHealth = Configuration.heliMainRotorHealth;
            heli.weakspots[0].health = Configuration.heliMainRotorHealth;
            heli.weakspots[1].maxHealth = Configuration.heliTailRotorHealth;
            heli.weakspots[1].health = Configuration.heliTailRotorHealth;
            heli.InitializeHealth(heliHealth, heliHealth);

            return heli;
        }

        void ChangeSpeed(int stage)
        {
            switch (stage)
            {
                case 1:
                    AddBradleyAPC(2000);
                    break;
                case 5:
                    fix = true;
                    break;
                case 22:
                    speed = maxSpeed / 1.2f;
                    break;
                case 26:
                    speed = maxSpeed / 1.4f;
                    break;
                case 30:
                    serverHackSeconds = HackableLockedCrate.requiredHackSeconds;
                    int dropsAmount = UnityEngine.Random.Range(Configuration.dropsAmount, Configuration.maxDropsAmount + 1);

                    if (dropsAmount < 1)
                    {
                        PrintWarning("Drops amount should not be less than 1, the value of the number of drops is set to 1");
                        dropsAmount = 1;
                    }
                    if (dropsAmount > 10) dropsAmount = 10;
                    if (!Configuration.heliAfterHack)
                        TryCallHeli();

                    if (eventMarker && afterLanding)
                        DrawMarker();

                    if (Configuration.landingMessage != "")
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                            if (!useNotify)
                                player.ChatMessage(Configuration.landingMessage);
                            else
                                Notify?.Call("SendNotify", player, Configuration.typeLandingMessage, Configuration.landingMessage);

                    speed = maxSpeed / 1.6f;
                    timer.Repeat(6.5f / (dropsAmount + 1), dropsAmount, () => { if (planeEntity) SpawnDrop(); });
                    break;
                case 31:
                    fix = false;
                    break;
                case 32:
                    speed = maxSpeed / 1.4f;
                    break;
                case 34:
                    speed = maxSpeed / 1.2f;
                    break;
                case 36:
                    speed = maxSpeed;
                    break;
                case 38:
                    speed = maxSpeed * 1.1f;
                    break;
                case 40:
                    speed = maxSpeed * 1.2f;
                    break;
                case 42:
                    speed = maxSpeed * 1.4f;
                    break;
                case 57:
                    eventTimer?.Destroy();
                    planeEntity.Kill();
                    Puts("Cargo plane flew off the map");
                    break;
            }

        }

        private void TryCallHeli()
        {
            int random = UnityEngine.Random.Range(0, 100);

            if (random < Configuration.heliChance)
                CallHeli(airfieldPosition + new Vector3(UnityEngine.Random.Range(-150, 150), 25f, UnityEngine.Random.Range(-150, 150)));
        }

        private void SpawnDrop()
        {
            Vector3 pos;
            xSum++;
            int supplyAmount = UnityEngine.Random.Range(Configuration.supplyAmount, Configuration.maxSupplyAmount + 1);
            for (int i = 0; i < supplyAmount; i++)
            {
                float offset = 2f;
                pos = planeEntity.transform.position - planeEntity.transform.forward * 12f + Vector3.up * 2f - planeEntity.transform.right * supplyAmount + planeEntity.transform.right * i * offset;
                var drop = GameManager.server.CreateEntity(supplyPrefab, pos, airfieldRotation);
                drop.Spawn();
                drop.name = "airfieldcrate";
                entityList.Add(drop);

                if (Configuration.airdropTableName != "")
                    SimpleLootTable?.Call("GetSetItems", drop, Configuration.airdropTableName, Configuration.airdropMinItems, Configuration.airdropMaxItems, 1f);

                if (drop is SupplyDrop)
                {
                    SupplyDrop supplyDrop = drop as SupplyDrop;
                    var box = supplyDrop.GetComponent<Rigidbody>();
                    supplyDrop.RemoveParachute();
                    box.drag = 1f;
                    timer.Once(lifeTimeSupply, () => { if (drop) drop.Kill(); });
                }
            }


            List<BaseEntity> tempBotsList = new List<BaseEntity>();
            List<Vector3> tempWayPointsList = new List<Vector3>();
            int repeatCount = 0;
            int npcsAmount = UnityEngine.Random.Range(Configuration.npcsAmount, Configuration.maxNpcsAmount + 1);

            for (int i = 0; i < npcsAmount; i++)
            {
                float x = i;
                int offset = 0;


                if (x / 2 == (int)(x / 2))
                    offset = 9;

                pos = planeEntity.transform.position + Vector3.up - planeEntity.transform.forward * offset - planeEntity.transform.forward * UnityEngine.Random.Range(5, 9) - planeEntity.transform.right * npcsAmount / 2f + planeEntity.transform.right * i;

                pos = GroundPos(pos);

                var npc = GameManager.server.CreateEntity(npcPrefab, pos, new Quaternion(), true);
                npc.Spawn();

                entityList.Add(npc);
                BasePlayer player = npc as BasePlayer;
                player.name = npcName;
                player._name = Configuration.displayNameNPC;
                player._lastSetName = "#Scientist1818";


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




                brain.AttackRangeMultiplier = 10f;

                timer.Once(1f, () =>
                                {
                                    HeldEntity heldEntity = player.GetHeldEntity();
                                    if (heldEntity != null)
                                        if (heldEntity.name == "assets/prefabs/weapons/l96/l96.entity.prefab" || heldEntity.name == "assets/prefabs/weapons/bolt rifle/bolt_rifle.entity.prefab")
                                            brain.AttackRangeMultiplier = 100f;
                                });

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
                navigator.CanUseNavMesh = false;
                brainNPC.homePosition = pos;
                brainNPC.stateTimer = 5;
                brainNPC.moveChance = 22;

                if (Configuration.customNavmesh)
                {
                    navigator.MaxRoamDistanceFromHome = navigator.BestMovementPointMaxDistance = navigator.BestRoamPointMaxDistance = 5f;
                    navigator.DefaultArea = "Walkable";
                    navigator.topologyPreference = ((TerrainTopology.Enum)TerrainTopology.EVERYTHING);
                    navigator.Agent.agentTypeID = -1372625422;
                    if (navigator.CanUseNavMesh) navigator.Init(player, navigator.Agent);
                }

                timer.Once(1f, () =>
                {
                    if (npc)
                        MoveToPosition(brain, navigator, npc.transform.position + Vector3.forward * 0.1f, 0);
                });

                if (!npcForceFreeze)
                    timer.Every(3f, () => { if (npc) NpcAI(npc); });

                int y = npcsAmount * (xSum - 1) + i;

                if (y < Configuration.displayNamesListNPC.Count)
                    if (Configuration.displayNamesListNPC[y] != null)
                        player._name = Configuration.displayNamesListNPC[y];


                switch (Configuration.kitsDistribution)
                {
                    case 1:
                        if (y < kitsListNPC.Count)
                            GiveKitNPC(player, kitsListNPC[y]);
                        break;
                    case 2:
                        if (repeatCount >= kitsListNPC.Count)
                            repeatCount = 0;
                        GiveKitNPC(player, kitsListNPC[repeatCount]);
                        repeatCount++;
                        break;
                    case 3:
                        GiveKitNPC(player, kitsListNPC[UnityEngine.Random.Range(0, kitsListNPC.Count)]);
                        break;
                }

                if (npcHealth > 0)
                { player.startHealth = npcHealth; player.health = npcHealth; }
                timer.Once(lifeTimeNpcs, () => { if (npc) npc.Kill(); });

                tempBotsList.Add(npc);

            }

            botList.Add(tempBotsList);
            timer.Once(0.1f, () => AddWayPoints(planeEntity.transform.position - planeEntity.transform.forward * 15, 20));

            int cratesAmount = UnityEngine.Random.Range(Configuration.cratesAmount, Configuration.maxCratesAmount + 1);

            for (int i = 0; i < cratesAmount; i++)
            {
                float offset = 2f;
                pos = planeEntity.transform.position - planeEntity.transform.forward * 10f + Vector3.up * 2f - planeEntity.transform.right * cratesAmount + planeEntity.transform.right * i * offset;
                var crate = GameManager.server.CreateEntity(cratePrefab, pos, airfieldRotation);
                crate.Spawn();
                crate.name = "airfieldcrate";
                entityList.Add(crate);

                if (Configuration.crateTableName != "")
                    SimpleLootTable?.Call("GetSetItems", crate, Configuration.crateTableName, Configuration.crateMinItems, Configuration.crateMaxItems, 1f);

                HackableLockedCrate ent = crate.GetComponent<HackableLockedCrate>();
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
                    if (crate)
                    {
                        ent = crate.GetComponent<HackableLockedCrate>();
                        if (ent.hackSeconds == serverHackSeconds - Configuration.timerCrates)
                            crate.Kill();
                    }
                });
            }
        }

        private void GiveKitNPC(BasePlayer player, string kit)
        {
            if (kit != null)
            {
                if (Kits?.Call("IsKit", kit).ToString() == "True")
                {
                    player.inventory.Strip();
                    Kits?.Call("GiveKit", player, kit);
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

        public void AddWayPoints(Vector3 center, float radius)
        {
            List<Vector3> tempWayPointsList = new List<Vector3>();
            float off = 1;
            for (float i = -radius / 2 + off; i < radius / 2 + off; i += 1)
                for (float ii = -radius / 2 + off; ii < radius / 2 + off; ii += 1)
                {
                    RaycastHit check;
                    Vector3 offset = new Vector3(i, 0, ii);
                    if (Physics.Raycast(center + offset + Vector3.up * 100, Vector3.down, out check, 200, layerS))
                    {
                        BaseEntity entity = check.GetEntity();
                        if (!entity)
                            tempWayPointsList.Add(check.point);

                    }
                }
            wayPointsList.Add(tempWayPointsList);

        }


        public void NpcAI(BaseEntity npc)
        {
            BaseNavigator navigator = npc.GetComponent<BaseNavigator>();
            ScientistBrain brain = npc.GetComponent<ScientistBrain>();
            NpcBrain x = npc.gameObject.GetComponent<NpcBrain>();
            List<Vector3> wayPoints = new List<Vector3>();
            int speed = 0;
            int s = 0;

            foreach (var item in botList)
            {
                for (int i = 0; i < item.Count; i++)
                    if (npc == item[i])
                        wayPoints = wayPointsList[s];

                s++;
            }


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
                        //if (Vector3.Distance(npc.transform.position, x.homePosition) > 10) return;

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

        public void MoveToPosition(ScientistBrain brain, BaseNavigator navigator, Vector3 pos, int moveSpeed)
        {
            var speed = BaseNavigator.NavigationSpeed.Slow;
            if (moveSpeed == 1)
                speed = BaseNavigator.NavigationSpeed.Fast;

            navigator.CanUseNavMesh = true;
            brain.Navigator.SetDestination(pos, speed, 0f, 0f);
            navigator.CanUseNavMesh = false;

        }

        void EventStart()
        {
            if (eventFlag)
            {
                eventTimer?.Destroy();
                fastTimer?.Destroy();
                RemoveEvent();
                entityList.Clear();
                botList.Clear();
                wayPointsList.Clear();
                xSum = 0;


                if (eventMarker && !afterLanding)
                    DrawMarker();

                Puts("Event started");
                Interface.CallHook("AirfieldEventStarted");
                SubscribeAll();

                string prefabstr = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
                planeEntity = GameManager.server.CreateEntity(prefabstr, new Vector3(0, 1000, 0), new Quaternion(), false);
                planeEntity.Spawn();
                stage = 0;
                speed = maxSpeed;
                oldRot = planeEntity.transform.rotation;
                fix = false;

                eventTimer = timer.Every(0.1f, () => EventCalc());

                if (Configuration.message != null)
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                        if (!useNotify)
                            player.ChatMessage(Configuration.message);
                        else
                            Notify?.Call("SendNotify", player, Configuration.typeEventMessage, Configuration.message);




            }
        }

        private void AddBradleyAPC(int radius)
        {
            Debug.LogWarning("If you are not using a custom map, correct operation of BradleyAPC is not guaranteed. Use the default airfield or a landing spot that is similar to the default airfield");

            int random = UnityEngine.Random.Range(0, 100);
            if (random >= Configuration.bradleyAPCspawnChance) return;


            float i = UnityEngine.Random.Range(0, 6.283185307f);

            double x = airfieldPosition.x + radius * Math.Cos(i);
            double z = airfieldPosition.z + radius * Math.Sin(i);

            Vector3 spawnPosition = GroundPos(new Vector3(Convert.ToSingle(x), 500, Convert.ToSingle(z))) + Vector3.up * 10;

            Vector3 point = mon.transform.position - mon.transform.forward * 30 + mon.transform.right * 94;
            string prefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";
            var entity = GameManager.server.CreateEntity(prefab, spawnPosition);
            entity.Spawn();
            entity.name = "AirfieldEventCH47";
            chinookDelivery = entity;

            prefab = "assets/prefabs/deployable/elevator/elevator.prefab";
            var elevator = GameManager.server.CreateEntity(prefab, entity.transform.position, new Quaternion(), false);
            elevator.Spawn();

            elevator.SetParent(entity);
            elevator.transform.localPosition = new Vector3(0, -1.5f, -0.5f);

            prefab = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
            var entity1 = GameManager.server.CreateEntity(prefab, entity.transform.position, new Quaternion(), false);
            entity1.Spawn();

            entity1.SetParent(entity);
            entity1.transform.localPosition = new Vector3(0, -2.5f, -0.5f);

            CH47HelicopterAIController chinook = entity as CH47HelicopterAIController;
            CH47AIBrain brain = entity.GetComponent<CH47AIBrain>();
            CH47Bradley ch47Bradley = entity.gameObject.AddComponent<CH47Bradley>();
            ch47Bradley.targetPosition = point + Vector3.up * 3;

            ch47Bradley.mainTimer = timer.Every(1f, () =>
                            {
                                if (!entity) return;
                                Vector3 pos = new Vector3((ch47Bradley.targetPosition.x - entity.transform.position.x) * 1000f, entity.transform.position.y, (ch47Bradley.targetPosition.z - entity.transform.position.z) * 1000f);

                                SetDestination(chinook, pos);

                                if (Vector3.Distance(entity.transform.position, new Vector3(ch47Bradley.targetPosition.x, entity.transform.position.y, ch47Bradley.targetPosition.z)) < 30)
                                {
                                    ch47Bradley.mainTimer.Destroy();

                                    ch47Bradley.mainTimer = timer.Every(0.2f, () =>
                                    {
                                        if (chinook != null && !chinook.IsDestroyed)
                                        {
                                            chinook.rigidBody.velocity = Vector3.Normalize(new Vector3(ch47Bradley.targetPosition.x - entity.transform.position.x, ch47Bradley.targetPosition.y - entity.transform.position.y, ch47Bradley.targetPosition.z - entity.transform.position.z)) * 12;
                                            SetDestination(chinook, ch47Bradley.targetPosition);
                                            if (Vector3.Distance(entity.transform.position, ch47Bradley.targetPosition) < 3)
                                            {
                                                chinook.rigidBody.velocity = Vector3.up;
                                                ch47Bradley.mainTimer.Destroy();
                                                Vector3 pos = entity1.transform.position;
                                                Quaternion rot = entity1.transform.rotation;

                                                entity1.transform.position = pos;
                                                entity1.transform.rotation = rot;

                                                elevator.Kill();
                                                entity1.Kill();

                                                entity1 = GameManager.server.CreateEntity(prefab, pos, rot, true);
                                                entity1.Spawn();
                                                entityList.Add(entity1);
                                                entity1.name = npcName;

                                                BradleyAPC bapc = entity1.GetComponent<BradleyAPC>();
                                                bapc.ClearPath();

                                                bapc.bulletDamage = Configuration.BradleyAPCbulletDamage;
                                                bapc.health = Configuration.BradleyAPChealth;

                                                bapc.currentPath.Add(mon.transform.position - mon.transform.forward * 25 + mon.transform.right * 80);
                                                bapc.currentPath.Add(mon.transform.position - mon.transform.forward * 25 - mon.transform.right * 45);

                                                SetDestination(chinook, new Vector3(0, chinook.transform.position.y, 9999));

                                                timer.Once(Configuration.BradleyAPClifetime, () =>
                                                    {
                                                        if (entity1)
                                                            entity1.Kill();
                                                    });

                                                timer.Once(300, () => RemoveChinook(chinookDelivery));

                                                timer.Repeat(5, 60, () =>
                                                    {
                                                        if (entity)
                                                            SetDestination(chinook, new Vector3(0, chinook.transform.position.y, 9999));
                                                    });
                                            }
                                        }

                                    });

                                }

                            });
        }

        private void SetDestination(CH47HelicopterAIController heli, Vector3 target)
        {
            if (heli == null || heli.IsDestroyed || heli.gameObject == null || heli.IsDead() || target == Vector3.zero) return;
            heli.SetMoveTarget(target);

            var brain = heli?.GetComponent<CH47AIBrain>() ?? null;
            if (brain != null) brain.mainInterestPoint = target;
        }

        [Command("afe_addcustom")]
        private void afe_addcustom(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (player)
                if (player.IsAdmin)
                {
                    PlayerEyes capsule = player.GetComponentInChildren<PlayerEyes>();
                    GameObject monument = new GameObject();
                    monument.transform.rotation = Quaternion.Euler(capsule.rotation.eulerAngles.x, capsule.rotation.eulerAngles.y + 90, capsule.rotation.eulerAngles.z);
                    monument.transform.position = capsule.transform.position - monument.transform.right * 100f - monument.transform.up * 0.3f - monument.transform.forward * -43.5f;
                    planeWayPoints.Clear();
                    CreateWaypoints(monument);
                    player.SendConsoleCommand("ddraw.text", 30f, Color.green, planeWayPoints[29], "<size=24>start</size>");
                    player.SendConsoleCommand("ddraw.text", 30f, Color.green, planeWayPoints[30], "<size=24>end</size>");
                    player.SendConsoleCommand("ddraw.arrow", 30, Color.green, planeWayPoints[29], planeWayPoints[30], 2f);
                    player.ChatMessage("Cargo plane landing site has been successfully added and saved");

                    Configuration.customRotation = new Vector3(monument.transform.rotation.eulerAngles.x, monument.transform.rotation.eulerAngles.y, monument.transform.rotation.eulerAngles.z);
                    Configuration.customPosition = monument.transform.position;
                    SaveConfig(Configuration);

                }
        }

        [Command("afestart")]
        private void afestart(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player || player.IsAdmin)
                EventStart();
        }

        [Command("afestop")]
        private void afestop(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player || player.IsAdmin)
            {
                RemoveEvent();
                Interface.CallHook("AirfieldEventEnded");

                foreach (BasePlayer item in BasePlayer.activePlayerList)
                    if (!useNotify)
                        item.ChatMessage(Configuration.endMessage);
                    else
                        Notify?.Call("SendNotify", item, Configuration.typeEndMessage, Configuration.endMessage);

                Puts("Event ended");
                UnsubscribeAll();
            }
        }

        [Command("afefast")]
        private void afefast(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (!player || player.IsAdmin)
            {
                EventStart();
                timer.Once(2f, () =>
                    {
                        if (planeEntity)
                        {
                            planeEntity.transform.position = planeWayPoints[20];
                            planeEntity.transform.LookAt(dir);
                            oldRot = planeEntity.transform.rotation;
                            stage = 21;
                            fix = true;
                            AddBradleyAPC(200);
                        }
                    });
                fastTimer = timer.Once(30f, () =>
                    {
                        if (planeEntity)
                        {
                            planeEntity.transform.position = planeWayPoints[56];
                            stage = 56;
                        }
                    });



            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (vending != null)
            {
                marker.SendUpdate();
                vending.SendNetworkUpdate();
            }
        }

        private void AddOwnerTimerForPlayer(BasePlayer player)
        {
            OwnerTimer ownerTimer = player.gameObject.GetComponent<OwnerTimer>();
            if (!ownerTimer)
                player.gameObject.AddComponent<OwnerTimer>();
        }

        void OnServerShutdown()
        {
            RemoveEvent();
        }
        void Unload()
        {
            harmony.UnpatchAll("AirfieldEvent");
            RemoveEvent();
        }

        void RemoveEvent()
        {
            eventOwner = null;
            RemoveChinook(chinookDelivery);

            if (planeEntity) planeEntity.Kill();
            if (vending) vending.Kill();

            for (int i = 0; i < entityList.Count; i++)
                if (entityList[i])
                    entityList[i].Kill();

            entityList.Clear();
        }

        private void RemoveChinook(BaseEntity _chinookDelivery)
        {
            if (!_chinookDelivery) return;

            BaseMountable[] seats = _chinookDelivery.GetComponentsInChildren<BaseMountable>();
            foreach (BaseMountable seat in seats)
            {
                BasePlayer mountedPlayer = seat.GetMounted();
                if (mountedPlayer)
                    mountedPlayer.Kill();

            }

            timer.Once(0.1f, () => { if (_chinookDelivery) _chinookDelivery.Kill(); });

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
                        if (Vector3.Distance(player.transform.position, spherePVE.transform.position) > Configuration.eventRadius / 2f)
                            StartOwnerTimer(player);
                    }
                }

        }

        private void StartOwnerTimer(BasePlayer player)
        {
            if (player != eventOwner)
                return;

            if (!useNotify)
                player.ChatMessage(ownerLeaveMessage);
            else
                NotifyMessage(player, typeOwnerLeaveMessage, ownerLeaveMessage);

            OwnerTimer ownerTimer = player.GetComponent<OwnerTimer>();
            ownerTimer.timer?.Destroy();
            ownerTimer.timer = timer.Once(ownerLeftTime, () =>
                {
                    SelectChangeOwner();
                });
        }

        private void StopOwnerTimer(BasePlayer player)
        {
            if (player != eventOwner)
                return;

            if (!useNotify)
                player.ChatMessage(enterMessage);
            else
                NotifyMessage(player, typeEnterMessage, enterMessage);

            OwnerTimer ownerTimer = player.GetComponent<OwnerTimer>();
            ownerTimer.timer?.Destroy();
        }

        private void SelectChangeOwner()
        {
            if (teamOwners)
                ChangeOwner();
            else
            {
                eventOwner = null;
                if (spherePVE) spherePVE.Kill();
            }
        }

        private void NotifyMessage(BasePlayer player, int type, string message)
        {
            string com = "notify.player " + player.userID + " " + type.ToString() + " " + message;
            server.Command(com);
        }

        private void AddEventSphere()
        {
            Vector3 pos = airfieldPosition - mon.transform.right * Configuration.sphereOffset.x - mon.transform.up * Configuration.sphereOffset.y - mon.transform.forward * Configuration.sphereOffset.z;
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
            entityList.Add(sphereEntity);

            if (spherePVE != null) spherePVE.Kill();
            spherePVE = sphereEntity;

            AddTrigger.AddToEntity(sphereEntity);
        }

        private class AddTrigger : MonoBehaviour
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
            [PluginReference] Plugin Notify;
            AirfieldEvent airfieldEvent = new AirfieldEvent();
            private void OnTriggerEnter(Collider collider)
            {
                BasePlayer player = collider?.ToBaseEntity()?.ToPlayer();

                if (player == null || !player.IsConnected)
                    return;

                airfieldEvent.StopOwnerTimer(player);
            }

            private void OnTriggerExit(Collider collider)
            {
                BasePlayer player = collider?.ToBaseEntity()?.ToPlayer();

                if (player == null || !player.IsConnected)
                    return;

                airfieldEvent.StartOwnerTimer(player);
            }

            private void OnTriggerStay(Collider collider)
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


                if (!useNotify)
                    player.ChatMessage(ejectMessage);
                else
                    airfieldEvent.NotifyMessage(player, typeEjectMessage, ejectMessage);


                Vector3 ejectPoint = AirfieldEvent.points[0];

                foreach (Vector3 point in AirfieldEvent.points)
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

                vending.SendNetworkUpdate();
            }

        }

        private void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            var killer = info.Initiator;

            if (killer)
            {
                switch (killer.name)
                {
                    case heliName:
                        info.damageTypes.ScaleAll(heliDamageMultiplier);
                        break;
                }

                switch (entity.name)
                {
                    case npcName:
                        AddEventOwner(killer);
                        if (remain < Configuration.eventExtDuration)
                            remain = Configuration.eventExtDuration;
                        if (Vector3.Distance(entity.transform.position, killer.transform.position) > Configuration.npcMinDistToDmg)
                        {
                            info.damageTypes.ScaleAll(0);
                            BasePlayer player = killer as BasePlayer;

                            if (player && player.IsConnected)
                                if (!useNotify)
                                    player.ChatMessage(Configuration.npcFarAwayMessage);
                                else
                                    Notify?.Call("SendNotify", player, Configuration.typeFarAwayMessage, Configuration.npcFarAwayMessage);

                        }
                        break;
                    case heliName:
                        AddEventOwner(killer);
                        break;
                }

            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            AddOwnerTimerForPlayer(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (!Configuration.pveMode)
                return;

            if (player == eventOwner)
            {
                OwnerTimer ownerTimer = player.GetComponent<OwnerTimer>();
                ownerTimer.timer?.Destroy();
                timer.Once(Configuration.ownerLeftTime, () =>
                {
                    if (!player.IsConnected)
                        SelectChangeOwner();
                });
            }
        }

        private object OnPlayerSleep(BasePlayer player)
        {
            if (player != eventOwner) return null;

            timer.Once(0.1f, () =>
                {
                    if (Vector3.Distance(airfieldPosition + Configuration.sphereOffset, player.transform.position) > Configuration.eventRadius / 2f)
                        StartOwnerTimer(player);
                });

            return null;
        }

        private void SubscribeAll()
        {
            Subscribe("OnEntityTakeDamage");
            Subscribe("CanHackCrate");
            Subscribe("CanLootEntity");
            Subscribe("OnPlayerSleep");
        }

        private void UnsubscribeAll()
        {
            Unsubscribe("OnEntityTakeDamage");
            Unsubscribe("CanHackCrate");
            Unsubscribe("CanLootEntity");
            Unsubscribe("OnPlayerSleep");
        }

        private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate.name != "airfieldcrate") return;

            if (Configuration.heliAfterHack)
                TryCallHeli();

            Unsubscribe("CanHackCrate");
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
                            if (spherePVE)
                            {
                                if (Vector3.Distance(findPlayer.transform.position, spherePVE.transform.position) < eventRadius / 2f)
                                {
                                    eventOwner = findPlayer;
                                    break;
                                }
                            }
                        }
            if (eventOwner == null)
                if (spherePVE) spherePVE.Kill();

            VendingUpdate(eventOwner);
        }

    }
}