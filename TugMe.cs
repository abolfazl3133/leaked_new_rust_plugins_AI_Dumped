/*▄▄▄    ███▄ ▄███▓  ▄████  ▄▄▄██▀▀▀▓█████▄▄▄█████▓
▓█████▄ ▓██▒▀█▀ ██▒ ██▒ ▀█▒   ▒██   ▓█   ▀▓  ██▒ ▓▒
▒██▒ ▄██▓██    ▓██░▒██░▄▄▄░   ░██   ▒███  ▒ ▓██░ ▒░
▒██░█▀  ▒██    ▒██ ░▓█  ██▓▓██▄██▓  ▒▓█  ▄░ ▓██▓ ░ 
░▓█  ▀█▓▒██▒   ░██▒░▒▓███▀▒ ▓███▒   ░▒████▒ ▒██▒ ░ 
░▒▓███▀▒░ ▒░   ░  ░ ░▒   ▒  ▒▓▒▒░   ░░ ▒░ ░ ▒ ░░   
▒░▒   ░ ░  ░      ░  ░   ░  ▒ ░▒░    ░ ░  ░   ░    
 ░    ░ ░      ░   ░ ░   ░  ░ ░ ░      ░    ░      
 ░             ░         ░  ░   ░      ░  ░*/
using Facepunch;
using Facepunch.Utility;
using HarmonyLib;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using ProtoBuf.Nexus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Oxide.Plugins
{
    [Info("TugMe", "bmgjet", "1.2.2")]
    [Description("Mod TugBoat")]
    class TugMe : RustPlugin
    {
        //External Plugins
        [PluginReference]
        private readonly Plugin ServerRewards, Economics, Kits, NpcSpawn;
        private static TugMe _plugin; //Self Reference

        //Variables
        private Dictionary<BaseNetworkable, Vector3> TugDealers = new Dictionary<BaseNetworkable, Vector3>(); //Store Tug sellers
        private List<BaseNetworkable> EventBoats = new List<BaseNetworkable>(); //Current event tugboats
        private List<ulong> EventBoatsDead = new List<ulong>(); //Current event tugboats
        private List<ulong> ActiveNPCs = new List<ulong>(); //List of active NPCs on server from this plugin
        private Dictionary<ulong, ulong> LootBoxes = new Dictionary<ulong, ulong>(); //List Of lootboxes
        private List<ulong> PlayingEffect = new List<ulong>(); //List of Tugboats playing Effect
        private List<ulong> Shooting = new List<ulong>(); //List for cooldown of shooting
        private List<ulong> VoicePlayers = new List<ulong>(); //List of NPCs used for VoiceOutput
        private List<BaseNetworkable> TugMapMarkers = new List<BaseNetworkable>(); //List Of tugs mapmarkers
        private List<BaseNetworkable> mapmarkersList = new List<BaseNetworkable>(); //List of map markers
        private List<BaseNetworkable> VenderMapMarkers = new List<BaseNetworkable>(); //List Of vendor mapmarkers
        private List<BaseNetworkable> WaterSystem = new List<BaseNetworkable>();
        private Dictionary<ulong, ulong> UidMapping = new Dictionary<ulong, ulong>();
        private Dictionary<ulong, float> DamageTimer = new Dictionary<ulong, float>();
        private List<int?> AllowedItems = null;
        private bool CUICached = false;
        public class TugProtoData { public string OwnerID; public string TugData; public List<SignDataJson> SignData; }
        public class SignDataJson { public ulong NetId; public int Frame; public string SignData; }
        #region DataFile
        private StoredData storedData;
        private class StoredData
        {
            public List<ulong> AutoNav = new List<ulong>(); //Users who have enabled auto nav
            public List<ulong> RunningTugs = new List<ulong>(); //networkID and OwnerID
            public Dictionary<ulong, ulong> OwnedTugs = new Dictionary<ulong, ulong>(); //networkID and OwnerID
            public Dictionary<ulong, ulong> LastMiniDriver = new Dictionary<ulong, ulong>(); //networkID and OwnerID
            public List<ulong> EventTugs = new List<ulong>(); //Users who have enabled auto nav
        }
        #endregion

        #region CustomVendors
        //Setting for custom placed tugboat vendors
        public class CustomVendorPos
        {
            public Vector3 Position;
            public Vector3 Rotation;
            public Vector3 SpawnPoint;
            public CustomVendorPos init(Vector3 p, Vector3 r, Vector3 s)
            {
                Position = p;
                Rotation = r;
                SpawnPoint = s;
                return this;
            }
        }
        #endregion

        #region VendorsConfig
        //Setting for vendor positions
        public class VendorPos
        {
            public string PrefabPath;
            public Vector3 OffsetPosition;
            public Vector3 OffsetRotation;
            public Vector3 OffsetSpawnPoint;
            public VendorPos init(string m, Vector3 p, Vector3 r, Vector3 s)
            {
                PrefabPath = m;
                OffsetPosition = p;
                OffsetRotation = r;
                OffsetSpawnPoint = s;
                return this;
            }
        }
        #endregion

        #region TurretPositions
        //Setting for Turret Positions/Rotations
        public class TurretPos
        {
            public Vector3 Position;
            public Vector3 Rotation;
            public TurretPos init(Vector3 p, Vector3 r)
            {
                Position = p;
                Rotation = r;
                return this;
            }
        }
        #endregion

        #region BetterNPC Config Layout
        public class NpcBelt
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty("Amount")] public int Amount { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
            [JsonProperty("Mods")] public List<string> Mods { get; set; }
            [JsonProperty("Ammo")] public string Ammo { get; set; }
        }

        public class NpcWear
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
        }
        #endregion

        #region ResourcesData
        //Setting for Turret Positions/Rotations
        public class ResourcesData
        {
            public string Shortname;
            public int Amount;
            public ulong SkinID;
            public string Name;
            public ResourcesData init(string s, int a, ulong i, string n)
            {
                Shortname = s;
                Name = n;
                Amount = a;
                SkinID = i;
                return this;
            }
        }
        #endregion

        #region Language
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
            {"Help", "Tugboat Help Info:"},
            {"Buy1", "You Can Buy Tug Boats @ "},
            {"H1", "Harbors "},
            {"FV", "Fishing Villages "},
            {"FT", "Ferry Terminals"},
            {"M1", "You can enable AutoNav with command /autonav"},
            {"M2", "It will take over driving if you dismount while full throttle."},
            {"M3", "You can spawn tug boat with command /tugboat"},
            {"M4", "You can remove tugs with command /tugremove ID"},
            {"M5", "You can place some things even while they show red"},
            {"M6", "You can mark owned tugs on the map with command /tugfind"},
            {"M7", "You can use horn while driving with mouse1"},
            {"M8", "There is a button while driving a tug that will let you toggle autonav"},
            {"M9", "Cannot spawn because you're <color=orange>building blocked!</color>"},
            {"M0", "Cannot spawn because you're <color=orange>below sea level</color>"},
            {"E1", "<color=red>Trying to spawn outside of terrain!</color>"},
            {"E2", "<color=red>You're trying to spawn it too far away!</color>"},
            {"E3", "Spawning this close will <color=red>KILL YOU!</color>"},
            {"E4", "<color=orange>Not enough open ocean area to spawn.</color>"},
            {"E5", "<color=orange>To close to a blocked prefab.</color>"},
            {"E6", "<color=orange>Tugboat already present within {0}m</color>"},
            {"E7", "<color=red>To close to a player</color>"},
            {"E8", "Spawned Tug Boat"},
            {"E9", "<color=orange>At Spawn Limit ({0})</color>"},
            {"E0", "Spawning Event @ {0}"},
            {"A0", "Killed TugID: {0} @ {1}"},
            {"A1", "You Don't Own This Tug Boat."},
            {"A2", "Not A Tug Boat"},
            {"A3", "Enter ID of tug to remove /tugremove ID"},
            {"A4", "TugID: {0} @ {1}"},
            {"A5", "You don't have TugMe.Remove Permission."},
            {"A6", "Found {0} tug boats."},
            {"A7", "You don't have TugMe.Find Permission."},
            {"A8", "Enabling AutoNav"},
            {"A9", "Disabling AutoNav"},
            {"B0", "You Don't Have AutoNav Permission"},
            {"B1", "AutoNav Requires The Engine To Be Running"},
            {"B2", "AutoNav Enabled"},
            {"B3", "AutoNav Disabled"},
            {"B4", "You need more resources:\n{0}"},
            {"B5", "ServerRewards not installed"},
            {"B6", "You can not afford to buy this"},
            {"B7", "Economics not installed"},
            {"B8", "Currency Type Not supported on this server"},
            {"B9", "Paid {0} for tug boat"},
            {"C0", "Buy Tug Boat\n"},
            {"C1", "Spawn New Tug Boat"},
            {"C2", "To close to {0}({1})"},
            {"C3", "You don't have TugMe.Buy Permission"},
            {"C4", "At Spawn Limit ({0})"},
            {"C5", "Bought Tug Boat."},
            {"C6", "Tug Event Started Near Grid {0}"},
            {"C7", "Teleported Tug: {0} To {1}"},
            {"C8", "You can teleport your tug to you with /tugtome"},
            {"C9", "You can rotate some deployables, hit with hammer + reload key"},
            {"D1", "Enter ID of tug to teleport /tugtome ID"},
            {"D2", "You don't have TugMe.Tome Permission."},
            {"D3", "Out of torpedos!"},
            {"D4", "Torpedo fired!"},
            {"D5", "Out of inner tubes for water mine!"},
            {"D6", "Out of C4 for water mine!"},
            {"D7", "Water mine dropped!"},
            {"D8", "Minicopter latched!"},
            {"D9", "Minicopter unlatched!"},
            {"F0", "You can shoot torpedos key (R), Ammo taken from inventory"},
            {"F1", "You can drop mines with (ctrl), InnerTube + C4 taken from inventory"},
            {"F2", "Minicopter will latch onto tugboats"},
            {"F3", "Tug auth required to latch/unLatch!"},
            {"G0", "Invalid Args /tugcam name"},
            {"G1", "Not Authed On This Tug"},
            {"G2", "Set Camera ID {0}"},
            {"G3", "Set the ID of CCTV with /tugcam ID"},
            {"G4", "At placement limit for this entity ({0}/{1})"},
            {"G6","Doors On Running Tugboats Take Damage In Safe Zones!" },
            }, this);
        }

        public string message(string key, string text)
        {
            char[] array = lang.GetMessage(key, this).ToCharArray();
            Array.Reverse(array);
            return new string(array);
        }

        private string message(BasePlayer player, string key, params object[] args)
        {
            if (player == null) { return string.Format(lang.GetMessage(key, this), args); }
            return string.Format(lang.GetMessage(key, this, player.UserIDString), args);
        }
        #endregion

        #region Configuration
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("OrderID: ")]
            public string OrderID = "";

            [JsonProperty("Force Carbon Fallback")]
            public bool ForceCarbon = false;

            [JsonProperty("[Server] Block Native Tugboat Spawns")]
            public bool BlockNative = false;

            [JsonProperty("[Server] Move Blocked Tugboats To GC Instead Of Kill")]
            public bool BlockMove = false;

            [JsonProperty("[Server] Allow Native Spawned Tugs Already Authed On (For Option Block Native Tugboat Spawns)")]
            public bool NativeAllowAlreadyAuthed = false;

            [JsonProperty("[Server] Enable PVE Support")]
            public bool PVESupport = false;

            [JsonProperty("[Server] Near Tug Radius")]
            public int NearTug = 45;

            [JsonProperty("[Chat] Message Icon (SteamID)")]
            public string AnnouncementIcon = "76561199381312678";

            [JsonProperty("[Chat] Message Name")]
            public string TugName = "<color=orange>TugBoat</color>";

            [JsonProperty("[MAP] Draw On Map TugBoats")]
            public bool MapFunction = false;

            [JsonProperty("[MAP] Refresh Rate")]
            public int MapRefresh = 60;

            [JsonProperty("[PirateTug] Number Of Tug Boats")]
            public int EventTug = 0;

            [JsonProperty("[PirateTug] Min Server FPS To Allow Events")]
            public int MinFPS = 20;

            [JsonProperty("[PirateTug] Apply Code Lock To Pirate Loot Box")]
            public bool ApplyCodeLock = true;

            [JsonProperty("[PirateTug] Prevent Pirate Tugboat From Sinking")]
            public bool PirateSink = false;

            [JsonProperty("[PirateTug] When No Sink Trigger Fire After (Sec)")]
            public int PirateSinkFire = 180;

            [JsonProperty("[PirateTug] Try Prevent Beaching With Repel Force")]
            public bool NavPushBack = false;

            [JsonProperty("[PirateTug] Enable Event Oxide Hooks")]
            public bool EventHooks = false;

            [JsonProperty("[PirateTug] End Event Timeout (sec)")]
            public int EventTimeout = 1800;

            [JsonProperty("[PirateTug] Refresh Tickrate (sec)")]
            public int EventTick = 300;

            [JsonProperty("[PirateTug] Announce Event Starts")]
            public bool SpawnAnnounce = true;

            [JsonProperty("[PirateTug] Announce To Player Chat")]
            public bool SpawnAnnounceChat = false;

            [JsonProperty("[PirateTug] Announce Style")]
            public int AnnounceStyle = 1;

            [JsonProperty("[PirateTug] Mark Events On Map")]
            public bool MarkEvents = false;

            [JsonProperty("[PirateTug] Marker Alpha")]
            public float MarkerAlpha = 0.2f;

            [JsonProperty("[PirateTug] Marker Radius")]
            public float MarkerRadius = 0.5f;

            [JsonProperty("[PirateTug] Marker Color 1")]
            public string MarkerC1 = "#0098FF";

            [JsonProperty("[PirateTug] Marker Color 2")]
            public string MarkerC2 = "#FF0000";

            [JsonProperty("[PirateTug] Speed Multiplyer (Use Less Then 1 To Make NPC Tugs Slower)")]
            public float EventSpeed = 0.8f;

            [JsonProperty("[PirateTug] Over-ride Buoyant Sleep When No Near By Players (Will Increase Server Load)")]
            public bool DisableBuoyantSleep = true;

            [JsonProperty("[PirateTug] Loot Prefab (Used as loot profile)")]
            public string LootPrefab = "assets/prefabs/npc/patrol helicopter/heli_crate.prefab";

            [JsonProperty("[PirateTug] Loot Multiplyer")]
            public int LootMulti = 2;

            [JsonProperty("[PirateTug] Pirate Tugboat Health (-1 = default)")]
            public float PirateBoatHealth = -1;

            [JsonProperty("[PirateTug] Door Health")]
            public int DoorHealth = 35;

            [JsonProperty("[PirateTug] Door SkinID")]
            public ulong DoorSkinID = 0;

            [JsonProperty("[PirateTug] Start Fires After Loot Box Break (seconds)")]
            public int FireDelay = 10;

            [JsonProperty("[PirateTug] Add Extra Fire")]
            public bool ExtraFire = false;

            [JsonProperty("[PirateTug] Use Alternative NPC Type (Stops Better NPC from removing them)")]
            public bool AlternativeNPC = false;

            [JsonProperty("[PirateTug] Use Better NPC for Bots")]
            public bool UseBetterNPC = false;

            [JsonProperty("[PirateTug] NPCs Health")]
            public int NPCHealth = 350;

            [JsonProperty("[PirateTug] NPCs AimScaler")]
            public int NPCAimScaler = 1;

            [JsonProperty("[PirateTug] NPCs DamageScaler")]
            public int NPCDamageScaler = 1;

            [JsonProperty("[PirateTug] NPCs Attack Range Multiplyer")]
            public int NPCAttackRange = 3;

            [JsonProperty("[PirateTug] NPCs Name Prefix (Added to start of pirates name)")]
            public string NPCPrefix = "";

            [JsonProperty("[PirateTug] NPCs Gun")]
            public string NPCGun = "rifle.semiauto";

            [JsonProperty("[PirateTug] NPCs Gun Attachment")]
            public string NPCGunAttachment = "weapon.mod.flashlight";

            [JsonProperty("[PirateTug] NPCs Attire")]
            public string NPCAttire = "hazmatsuit.diver";

            [JsonProperty("[PirateTug] Use Kits Plugin For NPCs Attire(Change NPCs Attire to kits name)")]
            public bool UseKitsPlugin = false;

            [JsonProperty("[PirateTug] NPCs Chatter Remote Asset")]
            public string ChatterURL = "";

            [JsonProperty("[PirateTug] NPCs Kill Remote Asset")]
            public string KillURL = "";

            [JsonProperty("[PirateTug] NPCs Death Remote Asset")]
            public string DeathURL = "";

            [JsonProperty("[PirateTug] NPC Spawn Positions")]
            public Vector3[] NPCSpawnPoints = new Vector3[] { new Vector3(2, 2.8f, -8), new Vector3(-2, 2.8f, -8), new Vector3(0, 6, 2.5f), new Vector3(0, 2.1f, 9), new Vector3(0, 8.7f, 2), new Vector3(0, 2.1f, 3.5f), };

            [JsonProperty("[PirateTug] AutoTurret Gun")]
            public string AutoTurretGun = "pistol.eoka";

            [JsonProperty("[PirateTug] AutoTurret Ammo Amount")]
            public int AutoTurretAmmo = 100;

            [JsonProperty("[PirateTug] AutoTurret Health")]
            public int AutoTurretHealth = 150;

            [JsonProperty("[PirateTug] AutoTurret Spawn Positions")]
            public TurretPos[] NPCAutoTurretPoints = new TurretPos[]
            {
                new TurretPos().init(new Vector3(0,2,-1.49f), new Vector3(0,180,0)),
                new TurretPos().init(new Vector3(1.47f,7.2f,-1.1f), new Vector3(0,56,0)),
                new TurretPos().init(new Vector3(-1.47f,7.2f,-1.1f), new Vector3(0,304,0)),
                new TurretPos().init(new Vector3(0,4.5f,5.3f), new Vector3(0,0,0)),
                new TurretPos().init(new Vector3(0,3f,11f), new Vector3(0,0,0)),
                new TurretPos().init(new Vector3(0,4.5f,-4.8f), new Vector3(0,180,0)),
            };

            [JsonProperty("[PirateTug] Samsite Spawn Positions")]
            public TurretPos[] NPCSamsitePoints = new TurretPos[]
            {
                new TurretPos().init(new Vector3(-0.57f,10.7f,2.1f), new Vector3(0,0,0)),
            };

            [JsonProperty("[PirateTug] Samsite Ammo")]
            public int SamsiteAmmo = 25;

            [JsonProperty("[PirateTug] Samsite Health")]
            public int SamsiteHealth = 500;

            [JsonProperty("[PirateTug] Guntrap Spawn Positions")]
            public TurretPos[] NPCGuntrapPoints = new TurretPos[]
            {
                new TurretPos().init(new Vector3(1.6f,5.8f,3f), new Vector3(0,180,0)),
                new TurretPos().init(new Vector3(-1.6f,5.8f,3f), new Vector3(0,180,0)),
                new TurretPos().init(new Vector3(2f,2f,-4.7f), new Vector3(0,270,0)),
                new TurretPos().init(new Vector3(-2f,2f,-4.7f), new Vector3(0,90,0)),
            };

            [JsonProperty("[PirateTug] Guntrap Ammo")]
            public int GuntrapAmmo = 25;

            [JsonProperty("[PirateTug] Guntrap Health")]
            public int GuntrapHealth = 150;

            [JsonProperty("[Boat Setting] Only Apply Settings To Player Bought/Spawned Tug Boats")]
            public bool LimitPlayerSpawned = false;

            [JsonProperty("[Boat Setting] Lock Minicopters To Tugboats On Driver Dismount")]
            public bool TugBoatHeliLock = false;

            [JsonProperty("[Boat Setting] Move Locked Helis To Back Of Tugboat")]
            public bool MoveBackHeli = false;

            [JsonProperty("[Boat Setting] Disable Decay On Locked Helis")]
            public bool LockNoDecay = false;

            [JsonProperty("[Boat Setting] Unlock Helis On Plugin Unload")]
            public bool UnlockUnload = false;

            [JsonProperty("[Boat Setting] Minicopter Unlock Delay")]
            public int HeliUnLock = 8;

            [JsonProperty("[Boat Setting] Minicopter Lock Requires Tug Auth")]
            public bool LatchAuth = true;

            [JsonProperty("[Boat Setting] AutoFlip Back The Right Way Up")]
            public bool AutoFlipper = false;

            [JsonProperty("[Boat Setting] Tug Water Mines Mode (Pressing Duck (ctrl) to drop mines using InnerTube+C4 from players inventory)")]
            public bool Tugboatmines = false;

            [JsonProperty("[Boat Setting] Mines Health (They decay fast when in deep water)")]
            public int TugboatminesHealth = 500;

            [JsonProperty("[Boat Setting] Water Mines Cool Down (Sec per drop)")]
            public float TugboatminesCD = 2f;

            [JsonProperty("[Boat Setting] Tug Torpedo Mode (Pressing Reload (R) to fire torpedo from players inventory)")]
            public bool Tugboattorpedo = false;

            [JsonProperty("[Boat Setting] Torpedo Cool Down (Sec per shoot)")]
            public float TugboattorpedoCD = 3f;

            [JsonProperty("[Boat Setting] Horn Remote Asset")]
            public string HornURL = "";

            [JsonProperty("[Boat Setting] Horn Use Nexus Horn Effect")]
            public bool Horn = true;

            [JsonProperty("[Boat Setting] Disable Tugboat Driver Check")]
            public bool DisableDriverCheck = false;

            [JsonProperty("[Boat Setting] No Fuel Requirement")]
            public bool DisableFuelRequirement = false;

            [JsonProperty("[Boat Setting] Spawn With Fuel (-1 = default)")]
            public int FuelAmount = -1;

            [JsonProperty("[Boat Setting] Enable Global Broadcast")]
            public bool SpawnGlobal = true;

            [JsonProperty("[Boat Setting] Fuel Per Sec (-1 = default)")]
            public float FuelPerSec = -1;

            [JsonProperty("[Boat Setting] Engine Thrust (-1 = default)")]
            public float engineThrust = -1;

            [JsonProperty("[Boat Setting] Tugboat Health (-1 = default)")]
            public float BoatHealth = -1;

            [JsonProperty("[Boat Setting] Damage Doors On Tugboats Idling In Safe Zones (Damage Per 5min)(0 = disabled)")]
            public int SafeZoneDamage = 0;

            [JsonProperty("[AutoNav] Add AutoNav Button")]
            public bool AutoNavButton = false;

            [JsonProperty("[AutoNav] Node Distance To Switch Target")]
            public int NodeDistance = 80;

            [JsonProperty("[AutoNav] ButtonCUI")]
            public string ButtonCUIJson = @"[{""name"":""AutoNavButton"",""parent"":""Overlay"",""destroyUi"":""AutoNavButton"",""components"":[{""type"":""UnityEngine.UI.RawImage"",""color"":""1 1 1 1""},{""type"":""RectTransform"",""anchormin"":""0.92 0"",""anchormax"":""0.986 0.02"",""offsetmin"":""0 0"",""offsetmax"":""0 0""}]},{""name"":""Button"",""parent"":""AutoNavButton"",""components"":[{""type"":""UnityEngine.UI.Button"",""command"":""CMDAutoNav"",""color"":""0.26 0.39 0.53 1""},{""type"":""RectTransform"",""anchormin"":""0.0 0.0"",""anchormax"":""1 1""}]},{""parent"":""Button"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""AutoNav"",""fontSize"":12,""align"":""MiddleCenter"",""color"":""0.905 0.882 0.807 1""},{""type"":""RectTransform""}]}]";

            [JsonProperty("[AutoNav] Enable AutoNav On Dismount")]
            public bool AllowAutoNav = false;

            [JsonProperty("[AutoNav] AutoNav Take Over Delay")]
            public int AutoNavDelay = 8;

            [JsonProperty("[AutoNav] AutoNav TickRate")]
            public int AutoNavTick = 2;

            [JsonProperty("[AutoNav] Remove Water Junkpiles On Collision (AutoNav Only)")]
            public bool JunkPileKill = true;

            [JsonProperty("[AutoNav] Water Junkpiles Scan Distance")]
            public float JunkPileScan = 8;

            [JsonProperty("[Spawners] Buy Window CUI")]
            public string BuyCUIJson = @"[{""name"":""BuyTugPannel"",""parent"":""Overlay"",""destroyUi"":""BuyTugPannel"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0.179 0.179 0.179 0.9""},{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1"",""offsetmin"":""380 220"",""offsetmax"":""-380 -220""},{""type"":""NeedsCursor""}]},{""name"":""BuyTextMsg"",""parent"":""BuyTugPannel"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{BUYTEXT}"",""fontSize"":40,""align"":""MiddleCenter"",""color"":""1 1 1 1""},{""type"":""UnityEngine.UI.Outline"",""color"":""0 0 0 1"",""distance"":""1 1""},{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1"",""offsetmin"":""0 -120"",""offsetmax"":""0 180""}]},{""name"":""YesButton"",""parent"":""BuyTugPannel"",""components"":[{""type"":""UnityEngine.UI.Button"",""command"":""BuyTugBoat"",""color"":""1 1 1 1""},{""type"":""RectTransform"",""anchormin"":""0.5 0.5"",""anchormax"":""0.5 0.5"",""offsetmin"":""-243 -127"",""offsetmax"":""-1 -100""}]},{""parent"":""YesButton"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""Yes"",""fontSize"":15,""align"":""MiddleCenter"",""color"":""0 0 0 1""},{""type"":""RectTransform""}]},{""name"":""NoButton"",""parent"":""BuyTugPannel"",""components"":[{""type"":""UnityEngine.UI.Button"",""command"":""CloseTug"",""color"":""1 1 1 1""},{""type"":""RectTransform"",""anchormin"":""0.5 0.5"",""anchormax"":""0.5 0.5"",""offsetmin"":""1 -127"",""offsetmax"":""243 -100""}]},{""parent"":""NoButton"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""No"",""fontSize"":15,""align"":""MiddleCenter"",""color"":""0 0 0 1""},{""type"":""RectTransform""}]}]";

            [JsonProperty("[Spawners] Max Alive Tugs Per Player")]
            public int MaxPerPlayer = 5;

            [JsonProperty("[Spawners] Adds NPC Spawners At Harbors")]
            public bool EnableTugSpawners = true;

            [JsonProperty("[Spawners] Adds NPCs Spawners At Fishing Villages")]
            public bool EnableTugSpawners2 = true;

            [JsonProperty("[Spawners] Adds NPC Spawner At Ferry Terminal")]
            public bool EnableTugSpawners3 = true;

            [JsonProperty("[Spawners] Mark NPC Spawner On Map")]
            public bool MarkVendorsOnMap = true;

            [JsonProperty("[Spawners] Map Marker Label")]
            public string VendorsOnMap = "TUGBOAT VENDOR";

            [JsonProperty("[Spawners] NPC Spawners Attire")]
            public string NPCVendorAttire = "hazmatsuit.diver";

            [JsonProperty("[Spawners] Use Kits Plugin For NPCs Spawners Attire(Change NPC Spawners Attire to kits name)")]
            public bool UseKitsPluginVendor = false;

            [JsonProperty("[Spawners] Spawn Block Radius")]
            public float SpawnBlockRadius = 20;

            [JsonProperty("[Spawners] Spawn Cost Type (serverrewards,economics,resources,free)")]
            public string Costtype = "free";

            [JsonProperty("[Spawners] Spawn Cost For (serverrewards,economics)")]
            public float Spawncost = 2000;

            [JsonProperty("[Spawners] Currency Symbol For (serverrewards,economics)")]
            public string CurrencySymbol = "$";

            [JsonProperty("[Spawners] Spawn Cost When Using Resources V2")]
            public ResourcesData[] SpawnCostResources = new ResourcesData[] { new ResourcesData().init("scrap", 1300, 0, ""), new ResourcesData().init("metal.refined", 1300, 0, ""), };

            [JsonProperty("[Spawners] Vendor Positions (Offset from Center Of Monument)")]
            public VendorPos[] VendorPositions = new VendorPos[]
            {
                new VendorPos().init("assets/bundled/prefabs/autospawn/monument/harbor/ferry_terminal_1.prefab", new Vector3(3.896f, 0.217f, 43.764f), new Vector3(0, 80, 0), new Vector3(3, 0, 56)),
                new VendorPos().init("assets/bundled/prefabs/autospawn/monument/harbor/harbor_1.prefab", new Vector3(-65.4f, 4.3f, 24.8f), new Vector3(0, 180, 0), new Vector3(-74.5f, 0f, 35f)),
                new VendorPos().init("assets/bundled/prefabs/autospawn/monument/harbor/harbor_2.prefab", new Vector3(40.25f, 4.0f, 109f), new Vector3(0, 0, 0), new Vector3(50, 0, 120)),
                new VendorPos().init("assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_a.prefab", new Vector3(0.48f, 2, 21.33f), new Vector3(0, 270, 0), new Vector3(-7, 0, 32.5f)),
                new VendorPos().init("assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_b.prefab", new Vector3(-8.73f, 0.68f, 30.68f), new Vector3(0, 270, 0), new Vector3(-19, 0, 41.5f)),
                new VendorPos().init("assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_c.prefab", new Vector3(-1.47f, 2.1f, 22.62f), new Vector3(0, 270, 0), new Vector3(-14, 0, 31.5f)),
            };

            [JsonProperty("[Spawners] Custom Vendor Positions (Position On The Map)")]
            public CustomVendorPos[] CustomPositions = new CustomVendorPos[] { new CustomVendorPos().init(new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0)), };

            [JsonProperty("[Building] Allow Override Of Placement Check (White Listed Items Only)")]
            public bool OverrideAllow = false;

            [JsonProperty("[Building] Use Creative Mode As The Placement Overrider (Don't use yet still testing)")]
            public bool AdvancedOverrideAllow = false;

            [JsonProperty("[Building] Placement Distance Checking")]
            public float DistanceCheck = 1.5f;

            [JsonProperty("[Building] Power Electrical When Engine Running")]
            public bool PowerFromEngine = true;

            [JsonProperty("[Building] Run Sprinkler/Electrice Water Purifier system")]
            public bool WaterSystem = true;

            [JsonProperty("[Building] Water System Tick Rate")]
            public int WaterSystemTick = 3;

            [JsonProperty("[Building] Allow Horses On Tugboats")]
            public bool HorseTug = false;

            [JsonProperty("[Building] Add Double Door Frame To Tug Back")]
            public bool DoubleDoorFrame = false;

            [JsonProperty("[Building] Boat Auth = Trap Auth (needed for gun and flame traps)")]
            public bool AuthFromBoat = true;

            [JsonProperty("[Building] Boat Auth = Samsite Auth")]
            public bool SamAuthFromBoat = true;

            [JsonProperty("[Building] Max Samsite Per Tug (-1 = no limit)")]
            public int MaxSam = -1;

            [JsonProperty("[Building] Max AutoTurret Per Tug (-1 = no limit)")]
            public int MaxAT = -1;

            [JsonProperty("[Building] White List Shortnames")]
            public string[] ItemOverride = new string[]
            {
                "autoturret",
                "samsite" ,
                "water.catcher.small" ,
                "barricade.concrete" ,
                "composter" ,
                "sign.pole.banner.large",
                "small.oil.refinery" ,
                "trap.landmine",
                "trap.bear" ,
                "searchlight" ,
                "guntrap",
                "flameturret",
                "planter.small",
                "woodcross",
                "rustige_egg_a",
                "rustige_egg_b",
                "rustige_egg_c",
                "rustige_egg_d",
                "rustige_egg_e",
                "rustige_egg_f",
                "electric.flasherlight",
                "electric.furnace",
                "electric.sprinkler",
                "smallcandles",
                "strobelight",
                "discofloor",
                "fogmachine",
                "cursedcauldron",
                "coffin.storage",
                "largecandles",
                "snowmachine",
                "spookyspeaker",
                "boombox",
                "electric.heater",
                "electric.sirenlight",
                "electric.simplelight",
                "target.reactive",
                "industrial.wall.light.green",
                "industrial.wall.light.red",
                "industrial.wall.light",
                "planter.large",
                "scarecrow",
                "mailbox",
                "wall.graveyard.fence",
                "fireplace.stone",
                "powered.water.purifier",
                "ceilinglight",
                "cctv.camera",
                "ptz.cctv.camera",
                "hitchtroughcombo",
            };

            [JsonProperty("[Building] Rotation Amount (Per Hammer Hit)")]
            public int ItemAngle = 8;

            [JsonProperty("[Building] Allow Rotation On")]
            public string[] ItemRotation = new string[]
            {
                "autoturret_deployed",
                "barricade.concrete" ,
                "composter" ,
                "sign.pole.banner.large",
                "refinery_small_deployed" ,
                "searchlight.deployed" ,
                "guntrap.deployed",
                "flameturret.deployed",
                "snowmachine",
                "largecandleset",
                "fogmachine",
                "coffinstorage",
                "gravestone.wood.deployed",
                "boombox.deployed",
                "electricfurnace.deployed",
                "rustigeegg_b.deployed",
                "smallcandleset",
                "electrical.heater",
                "spookyspeaker",
                "strobelight",
                "electric.flasherlight.deployed",
                "reactivetarget_deployed",
                "planter.small.deployed",
                "electric.sirenlight.deployed",
                "cursedcauldron.deployed",
                "discofloor.deployed",
                "planter.large.deployed",
                "industrial.wall.lamp.deployed",
                "industrial.wall.lamp.red.deployed",
                "industrial.wall.lamp.green.deployed",
                "simplelight",
                "graveyardfence",
                "mailbox.deployed",
                "scarecrow.deployed",
                "fireplace.deployed",
                "poweredwaterpurifier.deployed",
                "hitchtrough.deployed",
            };

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() { config = new Configuration(); }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) { throw new JsonException(); }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            PrintWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }
        #endregion Configuration

        #region Commands
        [ChatCommand("tughelp")]
        private void tughelp(BasePlayer player) //Help chat command
        {
            MessageIcon(player, "Help");
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Buy"))
            {
                string buymsg = message(player, "Buy1");
                if (config.EnableTugSpawners) { buymsg += message(player, "H1"); }
                if (config.EnableTugSpawners) { buymsg += message(player, "FV"); }
                if (config.EnableTugSpawners3) { buymsg += message(player, "FT"); }
                rust.SendChatMessage(player, config.TugName, buymsg, config.AnnouncementIcon);
            }
            if (config.AllowAutoNav && permission.UserHasPermission(player.UserIDString, "TugMe.AutoNav"))
            {
                MessageIcon(player, "M1");
                MessageIcon(player, "M2");
            }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Spawn")) { MessageIcon(player, "M3"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Remove")) { MessageIcon(player, "M4"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Place")) { MessageIcon(player, "M5"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Find")) { MessageIcon(player, "M6"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Horn")) { MessageIcon(player, "M7"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Button")) { MessageIcon(player, "M8"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Tome")) { MessageIcon(player, "C8"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Rotate")) { MessageIcon(player, "C9"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Torpedo")) { MessageIcon(player, "F0"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Mine")) { MessageIcon(player, "F1"); }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Latch")) { MessageIcon(player, "F2"); }
            MessageIcon(player, "G3");
        }

        [ChatCommand("tugcopy")]
        private void TugCopy(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            if (args.Length != 1)
            {
                player.ChatMessage("Enter A Name To Save As /tugcopy name");
                return;
            }
            Tugboat tugboat = player.GetParentEntity() as Tugboat;
            if (tugboat != null)
            {
                File.WriteAllText(Path.Combine("tugmetugs", args[0] + ".json"), CopyTugBoat(tugboat));
                player.ChatMessage("Tug Copied");
            }
        }

        [ChatCommand("tugpaste")]
        private void TugPaste(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            if (args.Length != 1)
            {
                player.ChatMessage("Enter A Name Paste /tugpaste name");
                return;
            }
            if (!Directory.Exists("tugmetugs")) { Directory.CreateDirectory("tugmetugs"); }
            string path = Path.Combine("tugmetugs", args[0] + ".json");
            if (!File.Exists(path))
            {
                player.ChatMessage("File Not Found");
                return;
            }
            ProcessTugMeTugsJson(player.eyes.position + (player.eyes.BodyForward() * 16), player.eyes.rotation, File.ReadAllText(path));
            player.ChatMessage("Pasted Tugboat");
        }

        [ChatCommand("tugcam")]
        private void TugCam(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                player.ChatMessage(message(player, "G0"));
                return;
            }
            string camname = Sanitize(args[0]);
            if (player != null && player?.GetParentEntity() is Tugboat)
            {
                Tugboat tug = player.GetParentEntity() as Tugboat;
                if (tug == null) { return; }
                if (!tug.IsAuthed(player)) { player.ChatMessage(message(player, "G1")); return; }
                int index = 0;
                foreach (var child in tug.children)
                {
                    if (child is CCTV_RC)
                    {
                        CCTV_RC cam = child as CCTV_RC;
                        if (cam != null)
                        {
                            cam.UpdateIdentifier(camname + index++);
                            player.ChatMessage(message(player, "G2", cam.GetIdentifier()));
                        }
                    }
                }
            }
        }

        [ChatCommand("tugtoken")]
        private void Spawntugtoken(BasePlayer player, string command, string[] args) //Spawn command
        {
            if (player != null && player.IsAdmin)
            {
                int amount = 1;
                if (args.Length == 1) { int.TryParse(args[0], out amount); }
                Item i = ItemManager.CreateByName("paper", amount, 2513472788);
                player.GiveItem(i);
            }
        }

        [ChatCommand("tugboat")]
        private void SpawnTugBoat(BasePlayer player, string command, string[] args) //Spawn command
        {
            if (player != null && permission.UserHasPermission(player.UserIDString, "TugMe.Spawn"))
            {
                BasePlayer p = null;
                if (player.IsAdmin && args.Length == 1)
                {
                    ulong id;
                    if (ulong.TryParse(args[0], out id))
                    {
                        p = BasePlayer.FindByID(id);
                        if (p != null) { player = p; }
                    }
                }
                Vector3 position = GetPosition(player);
                if (position == Vector3.zero) return;
                if (SpawnManager(player)) //Check not over spawn limit
                {
                    MessageIcon(player, "E8");
                    if (p) { MessageIcon(p, "E8"); }
                    Tugboat boat = GameManager.server.CreateEntity("assets/content/vehicles/boats/tugboat/tugboat.prefab", position, player.transform.rotation, true) as Tugboat;
                    boat.OwnerID = player.userID;
                    boat.Spawn();
                    storedData.OwnedTugs.Add(boat.net.ID.Value, player.userID);
                    return;
                }
                MessageIcon(player, "E9", config.MaxPerPlayer.ToString());
                if (p) { MessageIcon(p, "E9", config.MaxPerPlayer.ToString()); }
            }
        }

        [ChatCommand("tugevent")]
        private void TugBoatEvent(BasePlayer player) //Spawn event nearest cargo node to player
        {
            if (player != null && permission.UserHasPermission(player.UserIDString, "TugMe.Event"))
            {
                player.Invoke(() =>
                {
                    if (player.IsBuildingBlocked())
                    {
                        MessageIcon(player, "M9");
                        return;
                    }
                    if (player.transform.position.y < -5) //Prevent spawning in train tunnels, caves ect
                    {
                        MessageIcon(player, "M0");
                        return;
                    }
                    Vector3 vector = TerrainMeta.Path.OceanPatrolFar[GetClosestNodeToUs(player.transform.position)];
                    MessageIcon(player, "E0", vector.ToString());
                    Tugboat TB = NewTugBoat(vector, Quaternion.Euler(new Vector3(0, 0, 0)), 1234);
                    if (config.DisableBuoyantSleep)
                    {
                        var TugBuoyancy = TB.GetComponent<Buoyancy>();
                        if (TugBuoyancy != null)
                        {
                            TugBuoyancy.CancelInvoke("CheckSleepState");
                            TugBuoyancy.Invoke(new Action(TugBuoyancy.Wake), 0f); //Force Awake
                        }
                    }
                    timer.Once(2, () => { if (TB != null) { SetupEvent(TB); } });
                }, 0.1f);
            }
        }

        [ChatCommand("tugtome")]
        private void TugToMe(BasePlayer player, string command, string[] args)
        {
            if (player != null && permission.UserHasPermission(player.UserIDString, "TugMe.Tome"))
            {
                if (args.Length == 1)
                {
                    ulong ID;
                    if (ulong.TryParse(args[0], out ID))
                    {
                        if (ID < 1000) { ID = ListOwnedTugs(player, (int)ID); }
                        if (storedData.OwnedTugs.ContainsKey(ID))
                        {
                            if (storedData.OwnedTugs[ID] == player.userID || player.IsAdmin)
                            {
                                TeleportTug(ID, player);
                            }
                            else { MessageIcon(player, "A1"); }
                        }
                        else { MessageIcon(player, "A2"); }
                    }
                    return;
                }
                MessageIcon(player, "D1");
                ListOwnedTugs(player);
                return;
            }
            MessageIcon(player, "D2");
        }

        [ChatCommand("tugremove")]
        private void TugRemove(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Remove"))
            {
                if (args.Length == 1)
                {
                    ulong ID;
                    if (ulong.TryParse(args[0], out ID))
                    {
                        if (ID < 1000) { ID = ListOwnedTugs(player, (int)ID); }
                        if (storedData.OwnedTugs.ContainsKey(ID))
                        {
                            if (storedData.OwnedTugs[ID] == player.userID)
                            {
                                BaseNetworkable bn = BaseNetworkable.serverEntities.Find(new NetworkableId(ID));
                                bn.Kill();
                                MessageIcon(player, "A0", ID, PositionToGridCoord(bn.transform.position));
                            }
                            else { MessageIcon(player, "A1"); }
                        }
                        else { MessageIcon(player, "A2"); }
                    }
                    return;
                }
                MessageIcon(player, "A3");
                ListOwnedTugs(player);
                return;
            }
            MessageIcon(player, "A5");
        }

        [ChatCommand("tugfind")]
        private void TugFind(BasePlayer player) //Mark tugboats owned on map
        {
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Find"))
            {
                MessageIcon(player, "A6", UpdatePlayersTugIcon(player, true).ToString());
                return;
            }
            MessageIcon(player, "A7");
        }

        [ChatCommand("autonav")]
        private void TugBoatAutoNav(BasePlayer player) //Toggle auto nav mode on/off
        {
            if (permission.UserHasPermission(player.UserIDString, "TugMe.AutoNav"))
            {
                if (!storedData.AutoNav.Contains(player.userID))
                {
                    MessageIcon(player, "A8");
                    storedData.AutoNav.Add(player.userID);
                    return;
                }
                MessageIcon(player, "A9");
                storedData.AutoNav.Remove(player.userID);
                return;
            }
            MessageIcon(player, "B0");
        }

        [ConsoleCommand("starttugevent")]
        private void ConsoleTugEvent(ConsoleSystem.Arg arg) //Create tug event at position from console/other plugin
        {
            if ((arg.IsAdmin || arg.IsRcon) && arg.Args?.Length >= 3) //Check there are 3 or more args
            {
                float x; float y; float z;
                if (float.TryParse(arg.Args[0], out x) && float.TryParse(arg.Args[1], out y) && float.TryParse(arg.Args[2], out z)) //Try convert to vector3
                {
                    Vector3 pos = new Vector3(x, y, z);
                    PrintToConsole("TugEvent @ " + pos);
                    Tugboat TB = NewTugBoat(pos, Quaternion.Euler(new Vector3(0, 0, 0)), 1234);
                    if (config.DisableBuoyantSleep)
                    {
                        var TugBuoyancy = TB.GetComponent<Buoyancy>();
                        if (TugBuoyancy != null)
                        {
                            TugBuoyancy.CancelInvoke("CheckSleepState");
                            TugBuoyancy.Invoke(new Action(TugBuoyancy.Wake), 0f); //Force Awake
                        }
                    }
                    timer.Once(2, () => { if (TB != null) { SetupEvent(TB); } });
                }
            }
        }

        //CUI Close button
        [ConsoleCommand("CloseTug")] private void CloseCUI(ConsoleSystem.Arg arg) { if (arg.Player() != null) { CuiHelper.DestroyUi(arg.Player(), "BuyTugPannel"); } }
        //CUI Buy button
        [ConsoleCommand("BuyTugBoat")]
        private void BuyCUI(ConsoleSystem.Arg arg)
        {
            if (arg != null)
            {
                BasePlayer player = arg?.Player();
                if (player != null)
                {
                    CuiHelper.DestroyUi(player, "BuyTugPannel"); //Close CUI Window
                    //Make sure NPC is nearby to stop console command exploiting
                    foreach (NPCTalking npc in TugDealers.Keys)
                    {
                        if (npc != null && Vector3.Distance(npc.transform.position, player.transform.position) <= 5)
                        {
                            if (ChargePlayer(player))
                            {
                                Tugboat tug = _plugin.NewTugBoat(_plugin.TugDealers[npc], npc.transform.rotation, player.userID);
                                if (tug != null) { storedData.OwnedTugs.Add(tug.net.ID.Value, player.userID); }
                            }
                            break;
                        }
                    }
                }
            }
        }
        //CUI AutoNav button
        [ConsoleCommand("CMDAutoNav")]
        private void AutoNavCUI(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                BasePlayer player = arg.Player();
                if (player.GetParentEntity() is Tugboat)
                {
                    Tugboat tugboat = player.GetParentEntity() as Tugboat;
                    if (tugboat != null && tugboat.GetDriver() == player)
                    {
                        if (!tugboat.HasFlag(Tugboat.Flags.On))
                        {
                            MessageIcon(player, "B1");
                            return;
                        }
                        AutoNavigation an = tugboat.gameObject.GetComponent<AutoNavigation>();
                        if (an == null) //Add auto nav if missing
                        {
                            an = tugboat.gameObject.AddComponent<AutoNavigation>();
                            an.captin = player;
                            MessageIcon(player, "B2");
                        }
                        else { an.RemoveMe(); }
                    }
                }
            }
        }
        #endregion

        #region TruePVEHook
        object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (config.PVESupport)
            {
                //Allow Players To Damage Pirate Tugs And Pirate NPCs
                if (EventBoats.Contains(entity) || ActiveNPCs.Contains(entity.net.ID.Value))
                {
                    return true;
                }
                //Allow Players To Damage Things On Pirate Tugs
                if (EventBoats.Contains(entity?.GetParentEntity()))
                {
                    return true;
                }
                //Allow Player On Player Damage When One On Pirate Tugboat
                BasePlayer player = entity.ToPlayer();
                if (player != null && hitinfo?.Initiator?.ToPlayer() != null)
                {
                    Tugboat tugboat = player.GetParentEntity() as Tugboat;
                    Tugboat tugboat2 = hitinfo?.Initiator?.ToPlayer().GetParentEntity() as Tugboat;
                    if (tugboat != null && tugboat2 != null)
                    {
                        if (EventBoats.Contains(tugboat) || EventBoats.Contains(tugboat2))
                        {
                            return true;
                        }
                    }
                }
            }
            return null;
        }
        object CanEntityBeTargeted(BasePlayer player, BaseEntity turret)
        {
            if (config.PVESupport) { if (EventBoats.Contains(turret?.GetParentEntity())) { return true; } }
            return null;
        }
        object CanEntityTrapTrigger(BaseTrap trap, BasePlayer player)
        {
            if (config.PVESupport) { if (EventBoats.Contains(trap?.GetParentEntity())) { return true; } }
            return null;
        }
        #endregion

        #region Hooks

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnNpcRadioChatter));
            Unsubscribe(nameof(OnSamSiteTarget));
            Unsubscribe(nameof(OnHammerHit));
            Unsubscribe(nameof(OnEntityKill));
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("TugMe");
            _plugin = this; //Set self reference
            //Permissions
            permission.RegisterPermission("TugMe.Spawn", this);
            permission.RegisterPermission("TugMe.Remove", this);
            permission.RegisterPermission("TugMe.Event", this);
            permission.RegisterPermission("TugMe.Buy", this);
            permission.RegisterPermission("TugMe.AutoNav", this);
            permission.RegisterPermission("TugMe.Place", this);
            permission.RegisterPermission("TugMe.Find", this);
            permission.RegisterPermission("TugMe.Horn", this);
            permission.RegisterPermission("TugMe.Button", this);
            permission.RegisterPermission("TugMe.Announce", this);
            permission.RegisterPermission("TugMe.Tome", this);
            permission.RegisterPermission("TugMe.Rotate", this);
            permission.RegisterPermission("TugMe.VendorMap", this);
            permission.RegisterPermission("TugMe.Torpedo", this);
            permission.RegisterPermission("TugMe.Mine", this);
            permission.RegisterPermission("TugMe.Latch", this);
            permission.RegisterPermission("TugMe.Tomeanywhere", this);
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityKill));
            if (!string.IsNullOrEmpty(config.ChatterURL)) { Subscribe(nameof(OnNpcRadioChatter)); }
            if (config.SamAuthFromBoat) { Subscribe(nameof(OnSamSiteTarget)); }
            if (config.ItemAngle != 0) { Subscribe(nameof(OnHammerHit)); }
            timer.Once(2, () =>
            { //Some reason some servers dont have ServerMgr.Instance ready when hook called so wait 2 sec)
            ServerMgr.Instance.StartCoroutine(TugMeStartUp());
            });
        }

        private void Unload()
        {
            foreach (var t in TugDealers.Keys) { if (t != null && !t.IsDestroyed) { t.Kill(); } } //Remove Tug NPCs
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p == null) { continue; }
                CuiHelper.DestroyUi(p, "AutoNavButton");
                CuiHelper.DestroyUi(p, "BuyTugPannel");
            }
            MarkerDisplayingDelete();
            foreach (var d in EventBoatsDead)
            {
                Tugboat tug = BaseNetworkable.serverEntities.Find(new NetworkableId(d)) as Tugboat;
                if (tug != null && !tug.IsDestroyed) { tug.Kill(); }
            }
            foreach (var t in EventBoats) { if (t != null && !t.IsDestroyed) { t.Kill(); } }
            foreach (var n in ActiveNPCs)
            {
                ScientistNPC npc = BaseNetworkable.serverEntities.Find(new NetworkableId(n)) as ScientistNPC;
                if (npc != null && !npc.IsDestroyed) { npc.Kill(); }
            }
            foreach (var v in VoicePlayers)
            {
                BasePlayer player = BasePlayer.FindByID(v);
                if (player != null && !player.IsDestroyed) { player.Kill(); }
            }
            //Kill VoiceNPCs
            foreach (var vm in VenderMapMarkers) { if (vm != null && !vm.IsDestroyed) { vm.Kill(); } }
            foreach (var vm in TugMapMarkers) { if (vm != null && !vm.IsDestroyed) { vm.Kill(); } }
            if (config.TugBoatHeliLock)
            {
                foreach (var miniCopter in BaseNetworkable.serverEntities)
                {
                    if (miniCopter != null && miniCopter is BaseHelicopter)
                    {
                        MiniLatch ml = miniCopter.gameObject.GetComponent<MiniLatch>();
                        if (ml != null) { ml.RemoveMe(); }
                    }
                }
            }
            for (int i = storedData.RunningTugs.Count - 1; i >= 0; i--)
            {
                BaseNetworkable b = BaseNetworkable.serverEntities.Find(new NetworkableId(storedData.RunningTugs[i]));
                if (b == null)
                {
                    storedData.RunningTugs.RemoveAt(i);
                    continue;
                }
                AutoNavigation an = b.gameObject.GetComponent<AutoNavigation>();
                if (an != null) { an.RemoveMe(); }
                Tugboat_TogglePower(b.children, false, storedData.RunningTugs[i]);
            }
            storedData.EventTugs.Clear();//Should all be removed if code made it this far.
            SaveData(); //Save Datafile
            _plugin = null;
        }

        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (entity?.prefabID == 1955582400)// "tugboatdriver") //Mounted tugboat driver seat
            {
                Tugboat tugboat = player?.GetParentEntity() as Tugboat;
                if (tugboat == null) { return; }
                if (EventBoatsDead.Contains(tugboat.net.ID.Value)) { NextFrame(() => { entity?.DismountAllPlayers(); }); return; }
                AutoNavigation an = tugboat.gameObject.GetComponent<AutoNavigation>();
                if (an != null) { an.captin = player; }
                if (config.AutoNavButton && permission.UserHasPermission(player.UserIDString, "TugMe.Button")) { CuiHelper.AddUi(player, config.ButtonCUIJson); }
                return;
            }
            //Keep track of last mini driver
            if (config.TugBoatHeliLock && (entity?.prefabID == 3742994540 /* == "miniheliseat"*/ || entity?.prefabID == 70742988))// == "attackhelidriver"))
            {

                try //try catch since will throw error on nexus transferes
                {
                    BaseVehicle bv = player?.GetParentEntity() as BaseVehicle;
                    if (bv == null) { return; }
                    if (storedData.LastMiniDriver.ContainsKey(bv.net.ID.Value)) { storedData.LastMiniDriver[bv.net.ID.Value] = player.userID; }
                    else { storedData.LastMiniDriver.Add(bv.net.ID.Value, player.userID); }
                    MiniLatch ml = entity?.VehicleParent()?.gameObject?.GetComponent<MiniLatch>();
                    if (ml != null) { ml.UnLatch(config.HeliUnLock); }
                }
                catch { }
                return;
            }
            if (config.Tugboatmines && entity?.prefabID == 1252195950)//"innertube.deployed") //Mounted Mine
            {
                foreach (var c4 in entity.children) { if (c4 != null && c4 is TimedExplosive) { entity?.DismountAllPlayers(); return; } }
            }
        }

        void CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            if (entity?.prefabID == 1955582400)// "tugboatdriver") //Dismounted tugboat driver seat
            {
                if (config.AutoNavButton) { CuiHelper.DestroyUi(player, "AutoNavButton"); }
                if (config.AllowAutoNav && storedData.AutoNav.Contains(player.userID) && permission.UserHasPermission(player.UserIDString, "TugMe.AutoNav"))
                {
                    Tugboat tugboat = player?.GetParentEntity() as Tugboat;
                    if (tugboat != null && tugboat.HasFlag(Tugboat.Flags.On)) //Check if engine on
                    {
                        AutoNavigation an = tugboat.gameObject.GetComponent<AutoNavigation>();
                        if (an == null) //Add auto nav if missing
                        {
                            an = tugboat.gameObject.AddComponent<AutoNavigation>();
                            an.captin = player;
                            MessageIcon(player, "B2");
                        }
                    }
                }
            }
            //Keep track of last mini driver
            if (config.TugBoatHeliLock && (entity?.prefabID == 3742994540 || entity?.prefabID == 70742988) && player == entity?.VehicleParent()?.GetDriver())
            {
                BaseVehicle bv = entity?.VehicleParent();
                if (bv == null) { return; }
                if (storedData.LastMiniDriver.ContainsKey(bv.net.ID.Value)) { storedData.LastMiniDriver[bv.net.ID.Value] = bv.GetDriver().userID; }
                else { storedData.LastMiniDriver.Add(bv.net.ID.Value, bv.GetDriver().userID); }
            }
        }

        //Sam Site Control check boat auth
        object OnSamSiteTarget(SamSite samSite, BaseCombatEntity target)
        {
            if (samSite.staticRespawn) { return null; }
            var mountPoints = (target as BaseVehicle)?.mountPoints;
            var tugboat = samSite?.GetParentEntity() as Tugboat;
            if (mountPoints != null && tugboat != null) //Checks players mounted
            {
                foreach (var mountPoint in mountPoints)
                {
                    var player = mountPoint.mountable.GetMounted();
                    if ((object)player != null && tugboat.IsAuthed(player)) { return true; }
                }
            }
            else if (tugboat != null) //Checks players standing in scrappy
            {
                foreach (var child in target.children)
                {
                    var player = child as BasePlayer;
                    if ((object)player != null)
                    {
                        if (tugboat.IsAuthed(player)) { return true; }
                    }
                }
            }
            return null;
        }

        private void OnServerSave() { SaveData(); }

        //Catch Hammer hitting to enable rotate mode
        object OnHammerHit(BasePlayer ownerPlayer, HitInfo info)
        {
            if (info?.Initiator?.ToPlayer() == null || !(info?.Initiator?.GetParentEntity() is Tugboat)) { return null; }
            //Check has reload down and permission
            if (info.Initiator.ToPlayer().serverInput.IsDown(BUTTON.RELOAD) && permission.UserHasPermission(info.Initiator.ToPlayer().UserIDString, "TugMe.Rotate"))
            {
                Tugboat tb = info.Initiator.GetParentEntity() as Tugboat;
                if (EventBoats.Contains(tb)) { return null; } //Check isnt event tug
                if (tb.IsAuthed(info.Initiator.ToPlayer()) && config.ItemRotation.Contains(info?.HitEntity?.ShortPrefabName) && info.HitEntity?.GetParentEntity() == tb)
                {
                    info.HitEntity.transform.localRotation *= Quaternion.Euler(0, config.ItemAngle, 0);
                    info.HitEntity.SendNetworkUpdateImmediate();
                    return true;
                }
            }
            return null;
        }

        void OnEntityKill(Tugboat entity)
        {
            if (entity?.net?.ID != null)
            {
                storedData.OwnedTugs.Remove(entity.net.ID.Value);
                storedData.RunningTugs.Remove(entity.net.ID.Value);
            }
        }
        void OnEntityKill(BaseHelicopter entity)
        {
            if (entity?.net?.ID != null)
            {
                storedData.LastMiniDriver.Remove(entity.net.ID.Value);
                try
                {
                    MiniLatch ml = entity?.gameObject?.GetComponent<MiniLatch>();
                    if (ml != null) { ml.mini.rigidBody.ResetInertiaTensor(); }
                }
                catch { }
            }
        }

        void OnEntityKill(StorageContainer entity)
        {
            if (entity?.net?.ID != null)
            {
                Tugboat boat = entity.GetParentEntity() as Tugboat;
                if (EventBoats.Contains(boat))
                {
                    LootBoxes.Remove(entity.net.ID.Value);
                    if (boat == null || boat.IsDestroyed) { return; }
                    if (config.EventHooks) 
                    { 
                        List<BasePlayer> list = Pool.Get<List<BasePlayer>>();
                        foreach(var child in boat.children)
                        {
                            if(child is BasePlayer)
                            {
                                BasePlayer player = child as BasePlayer;
                                if(player.UserIDString.IsSteamId())
                                {
                                    list.Add(player);
                                }
                            }
                        }
                        Interface.Oxide.CallHook("EventTugLooted", boat, list); 
                        Pool.FreeUnmanaged(ref list);
                    }
                    //Delay start fire
                    timer.Once(config.FireDelay, () => { if (boat != null) { BoatFire(boat); } });
                }
            }
        }

        void OnEntityKill(WaterInflatable entity)
        {
            for (int i = entity.children.Count - 1; i >= 0; i--)
            {
                if (entity.children[i] is TimedExplosive)
                {
                    TimedExplosive te = (entity.children[i] as TimedExplosive);
                    te.SetParent(null);
                    te.transform.position = entity.transform.position + new Vector3(0, 2, 0); //Raise out of water
                    List<Tugboat> obj = Pool.Get<List<Tugboat>>();
                    Vis.Entities(entity.transform.position, 4, obj, -1); //Check around area
                    foreach (Tugboat obj2 in obj)
                    {
                        obj2.Hurt(10000); //Manaually Apply Damage
                        break;
                    }
                    te.Explode(); //Trigger
                    Pool.FreeUnmanaged(ref obj);
                }
            }
        }

        //Custom kill and death sound effects
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            //NPC Killed a player
            if (info?.InitiatorPlayer?.net?.ID != null && !string.IsNullOrEmpty(config.KillURL)) //Check if remote asset is set
            {
                if (ActiveNPCs.Contains(info.InitiatorPlayer.net.ID.Value))
                {
                    WebPlayback(config.KillURL, info?.InitiatorPlayer?.GetParentEntity() as Tugboat);
                    return;
                }
            }
            //NPC Death from a player
            if (entity != null && entity?.net?.ID != null)
            {
                try
                {
                    if (ActiveNPCs.Contains(entity.net.ID.Value))
                    {
                        ActiveNPCs.Remove(entity.net.ID.Value);
                        //NPC Death
                        if (!string.IsNullOrEmpty(config.DeathURL)) { WebPlayback(config.DeathURL, entity?.GetParentEntity() as Tugboat); }
                        return;
                    }

                    //Filter out deaths of event tugs
                    if (EventBoats.Contains(entity))
                    {

                        Tugboat tug = entity as Tugboat;
                        if (tug != null && tug?.net?.ID != null)
                        {
                            EventBoatsDead.Add(tug.net.ID.Value);
                            AutoNavigation an = tug.gameObject.GetComponent<AutoNavigation>();
                            if (an != null) { an.RemoveMe(); } //Stops them auto nav
                            tug.gasPedal = 0; //Stop throttle
                            if (tug?.transform != null)
                            {
                                Effect.server.Run("assets/prefabs/tools/c4/effects/c4_explosion.prefab", tug.transform.position + tug.transform.rotation * new Vector3(0, 6.5f, 4), Vector3.up, null, true);
                                Effect.server.Run("assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab", tug.transform.position + tug.transform.rotation * new Vector3(0, 6.5f, 4), Vector3.up, null, true);
                            }
                            //Kill Scientists
                            for (int i = tug.children.Count - 1; i >= 0; i--)
                            {
                                try
                                {
                                    if (tug.children[i] is ScientistNPC)
                                    {
                                        ActiveNPCs.Remove(tug.children[i].net.ID.Value);
                                        if (!tug.children[i].IsDestroyed) { tug.children[i].Kill(); }
                                    }
                                    else if (tug.children[i] is AutoTurret || tug.children[i] is SamSite)
                                    {
                                        if (!tug.children[i].IsDestroyed) { tug.children[i].Kill(); }
                                    }
                                }
                                catch { }
                            }
                            if (config.EventHooks) { Interface.Oxide.CallHook("EventTugStopped", tug); }
                            //Remove data
                            try { EventBoats.Remove(tug); } catch { }
                            try { storedData.EventTugs.Remove(tug.net.ID.Value); } catch { }
                            //Stop From Sinking
                            if (_plugin.config.PirateSink)
                            {
                                tug.SetFlag(BaseEntity.Flags.Broken, false);
                                tug.Invoke(() =>
                                {
                                    if (tug != null)
                                    {
                                        tug.SetFlag(BaseEntity.Flags.Broken, false);
                                        tug.Invoke(() => { if (tug != null) { BoatFire(tug); } }, config.PirateSinkFire);
                                    }
                                }, 5);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        //Custom chatter
        private object OnNpcRadioChatter(ScientistNPC bot)
        {
            if (bot.GetParentEntity() is Tugboat) //Check that npc is on tugboat
            {
                Tugboat tb = bot.GetParentEntity() as Tugboat;
                if (tb == null || tb.IsDestroyed) { return null; }
                WebPlayback(config.ChatterURL, tb); //Play effect
                return false; //block orignal npc
            }
            return null;
        }

        private void OnNewSave()
        {
            storedData.EventTugs.Clear();
            storedData.OwnedTugs.Clear();
            storedData.AutoNav.Clear();
            storedData.LastMiniDriver.Clear();
            storedData.RunningTugs.Clear();
            SaveData();
        }

        object CanNetworkTo(VendingMachineMapMarker marker, BasePlayer player)
        {
            if (marker.OwnerID != player.userID && TugMapMarkers.Contains(marker)) { return false; }
            return null;
        }

        private void OnEntitySpawned(BaseHelicopter baseEntity)
        {
            NextFrame(() =>
            {
                LatchFunction(baseEntity, null);
            });
        }

        private void OnEntitySpawned(Tugboat baseEntity)
        {
            NextFrame(() =>
            {
                if (baseEntity != null)
                {
                    SetupTugSettings(baseEntity);
                }
            });
        }

        #endregion

        #region Methods
        public bool IsBase64String(string s) { s = s.Trim(); return (s.Length % 4 == 0) && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", System.Text.RegularExpressions.RegexOptions.None); }

        //Sends chat message with custom name and icon
        public void MessageIcon(BasePlayer player, string txt, params object[] args) { rust.SendChatMessage(player, config.TugName, message(player, txt, args), config.AnnouncementIcon); }

        public IEnumerator TugMeStartUp()
        {
            AllowedItems = Tugboat_AllowedItems();
            bool dupes = false;
            CustomVendorPos[] c = config.CustomPositions.Distinct().ToArray();
            if (c.Length != config.CustomPositions.Length)
            {
                config.CustomPositions = c;
                dupes = true;
            }
            VendorPos[] v = config.VendorPositions.Distinct().ToArray();
            if (v.Length != config.VendorPositions.Length)
            {
                config.VendorPositions = v;
                dupes = true;
            }
            if (dupes)
            {
                PrintWarning("Configuration appears to have duplicates; fixed and saving");
                SaveConfig();
            }
            yield return CoroutineEx.waitForEndOfFrame;
            if (config.EnableTugSpawners || config.EnableTugSpawners2 || config.EnableTugSpawners3)
            { //Create NPC Tug boat spawners
                CreateNPCs();
                Puts("Spawned " + TugDealers.Count + " Tugboat Dealers.");
            }
            yield return CoroutineEx.waitForEndOfFrame;
            //Clear up if server missed Unload hook (Crash or dirty shutdown)
            if (storedData.EventTugs != null && storedData.EventTugs.Count > 0)
            {
                int dirty = 0;
                foreach (var tugid in storedData.EventTugs)
                {
                    BaseNetworkable bn = BaseNetworkable.serverEntities.Find(new NetworkableId(tugid));
                    if (bn != null && !bn.IsDestroyed) { bn.Kill(); dirty++; }
                }
                if (dirty > 0) { Puts("Detected Dirty Server Shut Down, Removed " + dirty + " Orphaned Entitys!"); }
                storedData.EventTugs.Clear();
            }
            yield return CoroutineEx.waitForEndOfFrame;
            //Reapply mod to exsisting tugboats
            int tugs = 0;
            int minis = 0;
            foreach (var t in BaseNetworkable.serverEntities.entityList.Get().Values)
            {
                if (t is BaseHelicopter) { minis++; LatchFunction(t, null); continue; }
                if (t is Tugboat)
                {
                    if ((t as Tugboat).OwnerID == 1234)
                    {
                        Puts("Killing Old Event Tug");
                        t.Kill();
                    }
                    else
                    {
                        tugs++;
                        if (t != null)
                        {
                            SetupTugSettings(t as Tugboat);
                            //Reapply heli latching
                            for (int i = t.children.Count - 1; i >= 0; i--)
                            {

                                if (t.children[i] is Minicopter || t.children[i] is AttackHelicopter) { LatchFunction(t.children[i], t as Tugboat); }
                            }
                        }
                    }
                    continue;
                }
                if ((t is FlameTurret || t is GunTrap) && (t as BaseEntity).HasFlag(BaseEntity.Flags.Reserved5)) { (t as BaseEntity).SetFlag(BaseEntity.Flags.Reserved5, false); } //Reset Traps
            }
            Puts("Modded " + tugs + " Tugboats.");
            EventTick();
            timer.Every(config.EventTick, () => { EventTick(); });
            if (config.MapFunction) { timer.Every(config.MapRefresh, () => { MapTick(); }); }
            if (config.MarkEvents) { timer.Every(config.MapRefresh, () => { DrawMapfunction(); }); }
            if (config.WaterSystem) { timer.Every(config.WaterSystemTick, () => { AddWater(); }); }
        }

        public void SaveData() { Interface.Oxide.DataFileSystem.WriteObject("TugMe", storedData); } //Save Datafile

        public string Sanitize(string String) { return new string(String.Where(char.IsLetterOrDigit).ToArray()); }

        public string PositionToGridCoord(Vector3 position)
        {
            Vector2 vector = new Vector2(TerrainMeta.NormalizeX(position.x), TerrainMeta.NormalizeZ(position.z));
            float num = TerrainMeta.Size.x / 1024f;
            int num2 = 7;
            Vector2 vector2 = vector * num * num2;
            float num3 = Mathf.Floor(vector2.x) + 1f;
            float num4 = Mathf.Floor(num * (float)num2 - vector2.y);
            string text = string.Empty;
            float num5 = num3 / 26f;
            float num6 = num3 % 26f;
            if (num6 < 0) { num6 = 1; } //Limit to no less than A grid
            if (num4 < 0) { num4 = 0; } //Limit to no less then 0 Grid
            else if (num6 == 0f) { num6 = 26f; }
            if (num5 > 1f) { text += Convert.ToChar(64 + (int)num5); }
            text += Convert.ToChar(64 + (int)num6);
            return $"{text}{num4}";
        }

        private ulong ListOwnedTugs(BasePlayer player, int index = -1)
        {
            List<ulong> list = new List<ulong>();
            int i = 0;
            foreach (KeyValuePair<ulong, ulong> kvp in storedData.OwnedTugs)
            {
                if (kvp.Value == player.userID)
                {
                    BaseNetworkable bn = BaseNetworkable.serverEntities.Find(new NetworkableId(kvp.Key));
                    if (bn != null && !bn.IsDestroyed)
                    {
                        MessageIcon(player, "A4", + i++ + " Or " + kvp.Key.ToString(), PositionToGridCoord(bn.transform.position));
                        list.Add(kvp.Key);
                    }
                }
            }
            if (index == -1 || index > list.Count - 1) { return 0; }
            return list[index];
        }

        public void RepositionEntities(TransferRequest Request, Vector3 offset, Quaternion rot)
        {
            ProtoBuf.Entity entity = Request.entities[0];
            Vector3 pos = entity.baseEntity.pos;
            Quaternion rotation = Quaternion.Euler(entity.baseEntity.rot);
            Quaternion rhs = rot * Quaternion.Inverse(rotation);
            Vector3 b = offset - pos;
            foreach (ProtoBuf.Entity entity2 in Request.entities)
            {
                if (entity2.baseEntity != null && (entity2.parent == null || !entity2.parent.uid.IsValid))
                {
                    entity2.baseEntity.pos += b;
                    entity2.baseEntity.rot = (Quaternion.Euler(entity2.baseEntity.rot) * rhs).eulerAngles;
                }
            }
        }

        private List<SignDataJson> GetSignData(List<BaseNetworkable> networkables)
        {
            List<SignDataJson> signData = new List<SignDataJson>();
            foreach (var bn in networkables)
            {
                try
                {
                    if (bn is Signage)
                    {
                        int i = 0;
                        foreach (uint uid in (bn as Signage).textureIDs)
                        {
                            if (uid == 0) { continue; } //Blank image
                            byte[] Image = FileStorage.server.Get(uid, FileStorage.Type.png, bn.net.ID);
                            SignDataJson signDataJson = new SignDataJson();
                            signDataJson.NetId = bn.net.ID.Value;
                            signDataJson.Frame = i++;
                            signDataJson.SignData = Convert.ToBase64String(Compression.Compress(Image));
                            signData.Add(signDataJson);
                        }
                    }
                    else if (bn is PhotoFrame)
                    {
                        if ((bn as PhotoFrame)._overlayTextureCrc == 0) { continue; } //Blank image
                        byte[] Image = FileStorage.server.Get((bn as PhotoFrame)._overlayTextureCrc, FileStorage.Type.png, bn.net.ID);
                        SignDataJson signDataJson = new SignDataJson();
                        signDataJson.NetId = bn.net.ID.Value;
                        signDataJson.Frame = 0;
                        signDataJson.SignData = Convert.ToBase64String(Compression.Compress(Image));
                        signData.Add(signDataJson);
                    }
                }
                catch { Puts("GetSignData Fault"); }
            }
            return signData;
        }

        private string CopyTugBoat(Tugboat tugboat)
        {
            if (!Directory.Exists("tugmetugs")) { Directory.CreateDirectory("tugmetugs"); }
            Request request = null;
            List<BaseNetworkable> networkables = null;
            List<BasePlayer> players = null;
            List<string> playerIds = null;
            tugboat.DismountAllPlayers();
            NexusServer.BuildTransferRequest(tugboat, "", "", "", out request, out networkables, out players, out playerIds);
            for (int i = request.transfer.entities.Count - 1; i >= 0; i--) { if (request.transfer.entities[i].basePlayer != null) { request.transfer.entities.RemoveAt(i); } }
            TugProtoData tugProtoData = new TugProtoData();
            tugProtoData.OwnerID = tugboat.OwnerID.ToString();
            tugProtoData.TugData = Convert.ToBase64String(Compression.Compress(TransferRequest.SerializeToBytes(request.transfer)));
            tugProtoData.SignData = GetSignData(networkables);
            return JsonConvert.SerializeObject(tugProtoData);
        }

        private bool ProcessTugMeTugsJson(Vector3 positon, Quaternion rotation, string data)
        {
            TugProtoData tugProtoData = JsonConvert.DeserializeObject<TugProtoData>(data);
            if (tugProtoData == null)
            {
                Puts("DeserializeObject Fault");
                return false;
            }
            TransferRequest Request = TransferRequest.Deserialize(Compression.Uncompress(Convert.FromBase64String(tugProtoData.TugData))); //Convert byte array into Transfere data
            if (Request != null)
            {
                //Remap UIDs
                UidMapping.Clear();
                Request.InspectUids(new UidInspector<ulong>(UpdateWithNewUid));
                RepositionEntities(Request, positon, rotation);
                SpawnEntities(Request, tugProtoData.SignData); //Spawn entitys
            }
            return true;
        }

        private void SpawnEntities(TransferRequest Request, List<SignDataJson> Signs)
        {
            Tugboat tug = null;
            Rust.Application.isLoadingSave = true; //Stops auto init on entitys being created
            try
            {
                Dictionary<BaseEntity, ProtoBuf.Entity> EntityToSpawn = new Dictionary<BaseEntity, ProtoBuf.Entity>();
                List<BaseEntity> images = new List<BaseEntity>();
                foreach (ProtoBuf.Entity entity in Request.entities)
                {
                    //Create entitys
                    BaseEntity baseEntity = GameManager.server.CreateEntity(StringPool.Get(entity.baseNetworkable.prefabID), entity.baseEntity.pos, Quaternion.Euler(entity.baseEntity.rot), true);
                    if (baseEntity != null)
                    {
                        baseEntity.InitLoad(entity.baseNetworkable.uid);
                        EntityToSpawn.Add(baseEntity, entity);
                    }
                }
                foreach (KeyValuePair<BaseEntity, ProtoBuf.Entity> keyValuePair in EntityToSpawn)
                {
                    //Spawn and load data
                    BaseEntity key = keyValuePair.Key;
                    if (!(key == null))
                    {

                        key.Spawn();
                        //Remove old sign crc's
                        if (key is PhotoFrame)
                        {
                            keyValuePair.Value.photoFrame.overlayImageCrc = 0;
                            images.Add(key);
                        }
                        else if (key is Signage)
                        {
                            for (int i = keyValuePair.Value.sign.imageIds.Count - 1; i >= 0; i--) { keyValuePair.Value.sign.imageIds[i] = 0; }
                            images.Add(key);
                        }
                        else if (key is Tugboat)
                        {
                            tug = key as Tugboat;
                        }
                        key.Load(new BaseNetworkable.LoadInfo
                        {
                            fromDisk = true,
                            fromTransfer = true,
                            msg = keyValuePair.Value
                        });
                    }
                }
                //Fix Signs
                foreach (BaseEntity be in images)
                {
                    if (UidMapping.ContainsValue(be.net.ID.Value))
                    {
                        var NewUID = UidMapping.FirstOrDefault(x => x.Value == be.net.ID.Value).Key;
                        if (be is Signage)
                        {
                            int i = 0;
                            foreach (uint frame in (be as Signage).textureIDs) { ReadImage(NewUID, i++, be, Signs); }
                        }
                        else { ReadImage(NewUID, 0, be, Signs); }
                    }
                }
                foreach (KeyValuePair<BaseEntity, ProtoBuf.Entity> keyValuePair2 in EntityToSpawn)
                {
                    //Update data
                    BaseEntity key2 = keyValuePair2.Key;
                    if (!(key2 == null))
                    {
                        key2.UpdateNetworkGroup();
                        key2.PostServerLoad();
                    }
                }
            }
            catch { }
            Rust.Application.isLoadingSave = false; //Return to normal
            NextFrame(() =>
            {
                if (tug != null)
                {
                    SetupTugSettings(tug);
                    //Reapply heli latching
                    for (int i = tug.children.Count - 1; i >= 0; i--)
                    {
                        if (tug.children[i] is Minicopter || tug.children[i] is AttackHelicopter)
                        {
                            if (!storedData.LastMiniDriver.ContainsKey(tug.children[i].net.ID.Value))
                            {
                                storedData.LastMiniDriver.Add(tug.children[i].net.ID.Value, tug.OwnerID);
                            }
                            LatchFunction(tug.children[i], tug);
                            break; //Can only latch 1 heli
                        }
                    }
                }
            });
        }

        private void ReadImage(ulong uid, int frame, BaseEntity be, List<SignDataJson> Signs)
        {
            uint crc = 0;
            if (Signs == null || Signs.Count == 0) { return; }
            foreach (var sd in Signs)
            {
                if (sd.NetId == uid && frame == sd.Frame)
                {
                    Puts("Loaded Image " + String.Format("{0:0.##}", (double)(sd.SignData.Length / 1024f)) + " Kb");
                    crc = FileStorage.server.Store(Compression.Uncompress(Convert.FromBase64String(sd.SignData)), FileStorage.Type.png, be.net.ID);
                    if (be is Signage) { (be as Signage).textureIDs[frame] = crc; }
                    else if (be is PhotoFrame) { (be as PhotoFrame)._overlayTextureCrc = crc; }
                }
            }
        }

        private void UpdateWithNewUid(UidType type, ref ulong prevUid)
        {
            if (type == UidType.Clear) { prevUid = 0uL; }
            else if (prevUid != 0L)
            {
                if (!UidMapping.TryGetValue(prevUid, out ulong value))
                {
                    value = Network.Net.sv.TakeUID();
                    UidMapping.Add(prevUid, value);
                }
                prevUid = value;
            }
        }

        private void TeleportTug(ulong ID, BasePlayer player)
        {
            BaseNetworkable bn = BaseNetworkable.serverEntities.Find(new NetworkableId(ID));
            Vector3 vector = GetPosition(player);
            if (vector == Vector3.zero) { return; }
            bn.transform.position = vector;
            bn.SendNetworkUpdateImmediate();
            MessageIcon(player, "C7", ID, PositionToGridCoord(bn.transform.position));
        }

        private Vector3 GetPosition(BasePlayer player)
        {
            if (player == null) return Vector3.zero;
            if (player.IsBuildingBlocked())
            {
                MessageIcon(player, "M9");
                return Vector3.zero;
            }
            if (player.transform.position.y < -5) //Prevent spawning in train tunnels, caves ect
            {
                MessageIcon(player, "M0");
                return Vector3.zero;
            }
            Vector3 position;
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World")))
            {
                MessageIcon(player, "E1");
                return Vector3.zero;
            }
            if (permission.UserHasPermission(player.UserIDString, "TugMe.Tomeanywhere"))
            {
                return hit.point + new Vector3(0, 4, 0);
            }
            if (hit.distance > 35) //Limit max spawn range
            {
                MessageIcon(player, "E2");
                return Vector3.zero;
            }
            else if (hit.distance < 12) //Limit min spawn range to stop player glitching inside it
            {
                MessageIcon(player, "E3");
                return Vector3.zero;
            }
            position = hit.point;
            position.y = 0.1f; //Force to water level
            bool buildingblocked = false;
            List<BuildingPrivlidge> bb = Pool.Get<List<BuildingPrivlidge>>();
            Vis.Entities<BuildingPrivlidge>(position, _plugin.config.SpawnBlockRadius, bb);
            foreach (BuildingPrivlidge bp in bb)
            {
                if (!bp.IsAuthed(player))
                {
                    buildingblocked = true;
                    break;
                }
            }
            Pool.FreeUnmanaged(ref bb);
            if (buildingblocked)
            {
                MessageIcon(player, "M9");
                return Vector3.zero;
            }
            //Check 8 points around radius are all in water
            for (int i = 0; i < 8; i++)
            {
                float num5 = (float)i / (float)8 * 360f;
                Vector3 p = new Vector3(Mathf.Sin(num5 * 0.0174532924f) * 11f, 0, Mathf.Cos(num5 * 0.0174532924f) * 11f) + position;
                if (TerrainMeta.HeightMap.GetHeight(p) > 0)
                {
                    MessageIcon(player, "E4");
                    return Vector3.zero;
                }
            }
            //Check for prefab collision, Prevent spawn inside things
            if (CheckColliders(position, 12))
            {
                MessageIcon(player, "E5");
                return Vector3.zero;
            }
            //Check no close by tugs already
            List<Tugboat> list = Pool.Get<List<Tugboat>>(); //Pooling to save list memory
            Vis.Entities<Tugboat>(position, _plugin.config.SpawnBlockRadius, list);
            if (list.Count != 0)
            {
                MessageIcon(player, "E6", _plugin.config.SpawnBlockRadius.ToString());
                Pool.FreeUnmanaged(ref list); //Free pooling
                return Vector3.zero;
            }
            Pool.FreeUnmanaged(ref list); //Free pooling
            //Check not spawning ontop of a player
            List<BasePlayer> list2 = Pool.Get<List<BasePlayer>>();
            Vis.Entities<BasePlayer>(position, 12, list2);
            if (list2.Count != 0)
            {
                MessageIcon(player, "E7");
                Pool.FreeUnmanaged(ref list2);
                return Vector3.zero;
            }
            Pool.FreeUnmanaged(ref list2);
            return position;
        }

        private void MarkerDisplayingDelete()
        {
            foreach (var m in mapmarkersList)
            {
                if (m != null && !m.IsDestroyed)
                {
                    (m as MapMarkerGenericRadius).Kill();
                    (m as MapMarkerGenericRadius).SendUpdate();
                }
            }
            mapmarkersList.Clear();
        }

        private void DrawMapfunction()
        {
            MarkerDisplayingDelete();
            if (EventBoats.Count == 0) { return; }
            Color one;
            Color two;
            ColorUtility.TryParseHtmlString(config.MarkerC1, out one);
            ColorUtility.TryParseHtmlString(config.MarkerC2, out two);
            foreach (var tug in EventBoats)
            {
                MapMarkerGenericRadius MapMarkerCustom;
                MapMarkerCustom = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab") as MapMarkerGenericRadius;
                MapMarkerCustom.alpha = config.MarkerAlpha;
                MapMarkerCustom.radius = config.MarkerRadius;
                MapMarkerCustom.color1 = one;
                MapMarkerCustom.color2 = two;
                MapMarkerCustom.SetParent(tug as Tugboat, false, true);
                if (MapMarkerCustom.transform.position != new Vector3(0, 0, 0)) { mapmarkersList.Add(MapMarkerCustom); }
            }
            foreach (var m in mapmarkersList)
            {
                m.Spawn();
                MapMarker.serverMapMarkers.Remove((m as MapMarkerGenericRadius));
                (m as MapMarkerGenericRadius).SendUpdate();
            }
        }

        private void UpdateVenderTugIcon()
        {
            foreach (var kvp in TugDealers)
            {
                VendingMachineMapMarker vendingMarkerIcon = GameManager.server.CreateEntity(StringPool.Get(3459945130), kvp.Key.transform.position) as VendingMachineMapMarker;
                vendingMarkerIcon.markerShopName = config.VendorsOnMap;
                vendingMarkerIcon.enabled = false;
                vendingMarkerIcon.Spawn();
                VenderMapMarkers.Add(vendingMarkerIcon);
            }
        }

        private int UpdatePlayersTugIcon(BasePlayer player, bool forcerefresh = false)
        {
            if (player == null || player.IsNpc || player.IsSleeping() || !player.IsAlive()) { return 0; }
            int Found = 0;
            foreach (KeyValuePair<ulong, ulong> kvp in storedData.OwnedTugs)
            {
                if (kvp.Value == player.userID)
                {
                    BaseNetworkable bn = BaseNetworkable.serverEntities.Find(new NetworkableId(kvp.Key));
                    if (bn != null && !bn.IsDestroyed && bn is Tugboat)
                    {
                        Found++;
                        VendingMachineMapMarker vm = null;
                        foreach (var mm in TugMapMarkers)
                        {
                            if ((mm as VendingMachineMapMarker).markerShopName == "TUGBOAT[" + kvp.Key + "]")
                            {
                                vm = mm as VendingMachineMapMarker;
                                break;
                            }
                        }
                        if (vm == null)
                        {
                            VendingMachineMapMarker vendingMarkerIcon = GameManager.server.CreateEntity(StringPool.Get(3459945130), bn.transform.position) as VendingMachineMapMarker;
                            vendingMarkerIcon.markerShopName = "TUGBOAT[" + kvp.Key + "]";
                            vendingMarkerIcon.enabled = false;
                            vendingMarkerIcon.OwnerID = player.userID;
                            vendingMarkerIcon.Spawn();
                            vendingMarkerIcon.SetParent(bn as Tugboat, true, true);
                            TugMapMarkers.Add(vendingMarkerIcon);
                        }
                    }
                }
            }
            return Found; //Return number found
        }

        private bool HaveResources(BasePlayer player)
        {
            Dictionary<string, int> Needed = new Dictionary<string, int>();
            //Check has needed meterials
            foreach (var component in config.SpawnCostResources)
            {
                string name = component.Shortname;
                if (component.SkinID != 0)
                {
                    int found = 0;
                    List<Item> items = Facepunch.Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(items);
                    foreach (Item i in items) { if (i.skin == component.SkinID) { found += i.amount; } }
                    Pool.FreeUnmanaged(ref items);
                    if (found < component.Amount)
                    {
                        if (!Needed.ContainsKey(component.Name)) { Needed.Add(component.Name, 0); }
                        Needed[component.Name] += component.Amount;
                    }
                }
                else
                {
                    if (player.inventory.GetAmount(ItemManager.FindItemDefinition(component.Shortname).itemid) < component.Amount)
                    {
                        if (!Needed.ContainsKey(name)) { Needed.Add(name, 0); }
                        Needed[name] += component.Amount;
                    }
                }
            }
            //Has everything needed so remove from player and allow
            if (Needed.Count == 0)
            {
                foreach (var item in config.SpawnCostResources)
                {
                    if (item.SkinID == 0) { Take(player, ItemManager.FindItemDefinition(item.Shortname).itemid, item.Amount); }
                    else
                    {
                        int take = 0;
                        List<Item> list = Pool.Get<List<Item>>();
                        player.inventory.GetAllItems(list);
                        foreach (Item i in list)
                        {
                            if (i.skin == item.SkinID)
                            {
                                if (i.amount + take == item.Amount)
                                {
                                    take += i.amount;
                                    i.Remove();
                                    i.MarkDirty();
                                }
                                else if (i.amount + take < item.Amount)
                                {
                                    take += i.amount;
                                    i.Remove();
                                    i.MarkDirty();
                                }
                                else if (i.amount + take > item.Amount)
                                {
                                    int amountremove = (item.Amount - take);
                                    i.amount -= amountremove;
                                    take += amountremove;
                                    i.MarkDirty();
                                }
                                if (take == item.Amount) { break; }
                            }
                        }
                        Pool.FreeUnmanaged(ref list);
                    }
                }
                MessageIcon(player, "C5");
                return true;
            }
            //Doesnt have everything needed to build list and message.
            else
            {
                string text = "";
                foreach (var item in Needed) { text += $" * {item.Key} x{item.Value}\n"; }
                rust.SendChatMessage(player, config.TugName, message(player, "B4", text), config.AnnouncementIcon);
                return false;
            }
        }

        //Check type of tugboat buying and use external plugin if set
        bool ChargePlayer(BasePlayer player)
        {
            object result = null;
            if (config.Costtype == "serverrewards")
            {
                if (ServerRewards == null)
                {
                    MessageIcon(player, "B5");
                    return false;
                }
                result = ServerRewards.Call("CheckPoints", player.UserIDString);
                if (result is int && (int)result <= (int)config.Spawncost)
                {
                    MessageIcon(player, "B6");
                    return false;
                }
                result = ServerRewards.Call("TakePoints", player.UserIDString, (int)config.Spawncost);
            }
            else if (config.Costtype == "economics")
            {
                if (Economics == null)
                {
                    MessageIcon(player, "B7");
                    return false;
                }
                result = Economics.Call("Withdraw", player.UserIDString, (double)config.Spawncost);
            }
            else if (config.Costtype == "resources") { return HaveResources(player); }
            else if (config.Costtype == "free")
            {
                MessageIcon(player, "E8");
                return true;
            }
            else
            {
                MessageIcon(player, "B8");
                return false;
            }
            if (result == null || (result is bool && (bool)result == false))
            {
                MessageIcon(player, "B6");
                return false;
            }
            MessageIcon(player, "B9", config.CurrencySymbol + config.Spawncost.ToString());
            return true;
        }

        //Create CUI window to buy tug
        private void RustUI(BasePlayer player)
        {
            if (CUICached)
            {
                CuiHelper.AddUi(player, config.BuyCUIJson);
                return;
            }
            //Create CUI buy message
            string buytext = message(player, "C0");
            switch (config.Costtype)
            {
                case "serverrewards":
                    buytext += config.CurrencySymbol + config.Spawncost;
                    break;
                case "economics":
                    buytext += config.CurrencySymbol + config.Spawncost;
                    break;
                case "resources":
                    foreach (var item in config.SpawnCostResources)
                    {
                        if (item.Name == "") { buytext += item.Shortname + "(" + item.Amount + ")\n"; }
                        else { buytext += item.Name + "(" + item.Amount + ")\n"; }
                    }
                    break;
                default:
                    buytext = message(player, "C1");
                    break;
            }
            //Create CUI
            config.BuyCUIJson = config.BuyCUIJson.Replace("{BUYTEXT}", buytext);
            CUICached = true;
            CuiHelper.AddUi(player, config.BuyCUIJson);
        }

        private void LatchFunction(BaseNetworkable bv, Tugboat tb)
        {
            if (config.TugBoatHeliLock && bv != null)
            {
                MiniLatch latch = bv.gameObject.GetComponent<MiniLatch>();
                if (latch == null)
                {
                    latch = bv.gameObject.AddComponent<MiniLatch>();
                }
                if (latch != null && tb != null)
                {
                    latch.boat = tb;
                    latch.Latched = true;
                    latch.landed = true;
                    latch.mini = bv as BaseVehicle;
                    latch.Lock();
                }
            }
        }

        //Check prefabs by colliders
        private bool CheckColliders(Vector3 position, float distance)
        {
            foreach (Collider col in Physics.OverlapSphere(position, distance, LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World")))
            {
                string thisobject = col.gameObject.ToString();
                if (thisobject.Contains("modding") || thisobject.Contains("props") || thisobject.Contains("structures") || thisobject.Contains("building core") || thisobject.Contains("iceberg") || thisobject.Contains("tugboat") || thisobject.Contains("barge_base") || thisobject.Contains("cargo") || thisobject.Contains("dock") || thisobject.Contains("level0") || thisobject.Contains("structure_bottom")) { return true; }
            }
            return false;
        }

        //Find closest cargoship path point
        public int GetClosestNodeToUs(Vector3 position)
        {
            int result = 0;
            float num = float.PositiveInfinity;
            for (int i = 0; i < TerrainMeta.Path.OceanPatrolFar.Count; i++)
            {
                Vector3 b = TerrainMeta.Path.OceanPatrolFar[i];
                float num2 = Vector3.Distance(position, b);
                if (num2 < num)
                {
                    result = i;
                    num = num2;
                }
            }
            return result;
        }

        //Check and maintain spawned owner list.
        private bool SpawnManager(BasePlayer player)
        {
            int OwnedTugs = 0;
            for (int i = storedData.OwnedTugs.Count - 1; i >= 0; i--)
            {
                KeyValuePair<ulong, ulong> KVP = storedData.OwnedTugs.ElementAt(i);
                BaseNetworkable BN = BaseNetworkable.serverEntities.Find(new NetworkableId(KVP.Key));
                if (BN != null && !BN.IsDestroyed && BN is Tugboat)
                {
                    if (player.userID == KVP.Value) { OwnedTugs++; }
                    continue;
                }
                storedData.OwnedTugs.Remove(KVP.Key);
            }
            if (OwnedTugs >= config.MaxPerPlayer)
            {
                return false;
            }
            return true;
        }

        private void MapTick()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, "TugMe.Find")) { UpdatePlayersTugIcon(player); }
            }
        }

        private void EventTick()
        {
            //Clean Boat List
            for (int i = EventBoats.Count - 1; i >= 0; i--) { if (EventBoats[i].IsDestroyed || (EventBoats[i] as Tugboat).IsDead()) { EventBoats.Remove(EventBoats[i]); } }
            //Check if needs spawn
            if (EventBoats.Count >= config.EventTug) { return; }
            Vector3 SpawnPos = TerrainMeta.Path.OceanPatrolFar.GetRandom();
            if (!PlayersNearby(SpawnPos, 50))
            {
                Tugboat TB = NewTugBoat(SpawnPos, Quaternion.Euler(new Vector3(0, 0, 0)), 1234);
                if (config.DisableBuoyantSleep)
                {
                    var TugBuoyancy = TB.GetComponent<Buoyancy>();
                    if (TugBuoyancy != null)
                    {
                        TugBuoyancy.CancelInvoke("CheckSleepState");
                        TugBuoyancy.Invoke(new Action(TugBuoyancy.Wake), 0f); //Force Awake
                    }
                }
                TB.Invoke(() => { if (TB != null) { SetupEvent(TB); } }, 2);
            }
        }

        //Create fire on the boat
        private void BoatFire(Tugboat boat)
        {
            try
            {
                AutoNavigation an = boat.gameObject.GetComponent<AutoNavigation>();
                if (an != null) { an.RemoveMe(); }
                boat.gasPedal = 0; //Stop throttle
                Effect.server.Run("assets/prefabs/tools/c4/effects/c4_explosion.prefab", boat.transform.position + boat.transform.rotation * new Vector3(0, 6.5f, 4), Vector3.up, null, true);
                Effect.server.Run("assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab", boat.transform.position + boat.transform.rotation * new Vector3(0, 6.5f, 4), Vector3.up, null, true);
                CreateFire(300, new Vector3(0, 6f, 4.5f), 5, boat);
                CreateFire(300, new Vector3(0, 2.1f, 3.8f), 60, boat);
                CreateFire(300, new Vector3(0, 2.1f, -1f), 70, boat);
                CreateFire(300, new Vector3(0, 2.1f, -5f), 80, boat);
                if (config.ExtraFire)
                {
                    CreateFire(300, new Vector3(-2.3f, 5.67f, 1.5f), 80, boat);
                    CreateFire(300, new Vector3(-2.66f, 2f, -7.3f), 80, boat);
                    CreateFire(300, new Vector3(2.7f, 2f, -6.8f), 80, boat);
                    CreateFire(300, new Vector3(1.5f, 4.5f, -3.6f), 80, boat);
                    CreateFire(300, new Vector3(0.1f, 7.2f, -0.8f), 80, boat);
                    CreateFire(300, new Vector3(0.2f, 8.6f, 2.3f), 80, boat);
                    CreateFire(300, new Vector3(0f, 2f, 9f), 80, boat);
                }
            }
            catch { }
            //Trigger boat death loop
            timer.Once(80, () => { if (boat != null) { TryDie(boat); } });
        }

        //Delayed kill function
        private void TryDie(Tugboat tugboat)
        {
            if (tugboat == null || tugboat.IsDestroyed) { return; }
            if (PlayersNearby(tugboat.transform.position))
            {
                timer.Once(60, () => { TryDie(tugboat); });
                return;
            }
            EventBoatsDead.Add(tugboat.net.ID.Value);
            tugboat.DismountAllPlayers();
            //Kill NPCs and loot
            for (int i = tugboat.children.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (tugboat.children[i] != null && tugboat.children[i] is ScientistNPC && !tugboat.children[i].IsDestroyed)
                    {
                        ActiveNPCs.Remove((tugboat.children[i] as ScientistNPC).net.ID.Value);
                        tugboat.children[i].Kill();
                    }
                }
                catch { }
            }
            if (config.EventHooks) { Interface.Oxide.CallHook("EventTugStopped", tugboat); }
            //Remove data
            try { EventBoats.Remove(tugboat); } catch { }
            try { storedData.EventTugs.Remove(tugboat.net.ID.Value); } catch { }
            //Update save file
            SaveData();
            EventBoatsDead.Remove(tugboat.net.ID.Value);
            tugboat.Kill();
        }

        public bool PlayersNearby(Vector3 position, int ExtraDistance = 0)
        {
            List<BasePlayer> list = Pool.Get<List<BasePlayer>>(); //Pooling to save list memory
            Vis.Entities<BasePlayer>(position, config.NearTug + ExtraDistance, list, 131072, QueryTriggerInteraction.Collide); //Limit layers to players only and collide to ignore vanished players.
            bool result = false;
            foreach (BasePlayer basePlayer in list) //Check vis list
            {
                if (basePlayer.UserIDString.IsSteamId() && !basePlayer.IsSleeping() && basePlayer.IsAlive()) //Make sure not NPC and not asleep and that the player is alive
                {
                    result = true; //Found player
                    break; //End loop
                }
            }
            Pool.FreeUnmanaged(ref list); //Free pooling
            return result; //Return result
        }

        private void SetupEvent(Tugboat tugboat)
        {
            tugboat.EnableSaving(false);
            if (Performance.report.frameRate < config.MinFPS)
            {
                Puts("Warning SERVER Running Below 20FPS, Pirate Event Canceled.");
                tugboat.Kill();
                return;
            }
            //Force to face next node in path
            int targetNodeIndex = GetClosestNodeToUs(tugboat.transform.position) - 1;
            if (targetNodeIndex < 0) { targetNodeIndex = TerrainMeta.Path.OceanPatrolFar.Count - 1; }
            tugboat.transform.LookAt(TerrainMeta.Path.OceanPatrolFar[targetNodeIndex]);
            //Slightly slow down so players can catch.
            tugboat.engineThrust = tugboat.engineThrust * config.EventSpeed;
            //Give unlimited fuel
            tugboat.fuelPerSec = 0f;
            if (config.PirateBoatHealth != -1) { tugboat.InitializeHealth(config.PirateBoatHealth, config.PirateBoatHealth); }
            //Setup unlimited fuel
            StorageContainer fuelContainer = tugboat.fuelSystem.GetFuelContainer();
            fuelContainer.inventory.AddItem(fuelContainer.allowedItem, 1);
            fuelContainer.SetFlag(BaseEntity.Flags.Locked, true);
            //Create Suiside timer
            timer.Once(config.EventTimeout, () => { TryDie(tugboat); });
            //Spawn NPCs
            var TugNPCs = CreateNPCScientist(tugboat);
            //Create AutoTurrets
            CreateAutoTurrets(tugboat, TugNPCs);
            //Create Samsites
            CreateSamTurrets(tugboat);
            //Create GunTraps
            CreateGunTraps(tugboat);
            //Spawn Doors
            CreateDoors(tugboat, new Vector3(0, 2, -5.3f), new Vector3(0, 90, 0));
            CreateDoors(tugboat, new Vector3(-2, 5.75f, 1.4f), new Vector3(0, 0, 0));
            CreateDoors(tugboat, new Vector3(2, 5.75f, 1.4f), new Vector3(0, 180, 0));
            NextFrame(() =>
            {
                if (tugboat == null) { return; }
                //Spawn Loot
                StorageContainer loot = CreateStoragefunction(new Vector3(0, 2, 0), tugboat);
                LootBoxes.Add(loot.net.ID.Value, tugboat.net.ID.Value);
                if (loot != null)
                {
                    for (int i = 0; i <= config.LootMulti; i++)
                    {
                        StorageContainer baseEntity = GameManager.server.CreateEntity(config.LootPrefab) as StorageContainer;
                        if (baseEntity != null && loot != null)
                        {
                            baseEntity.SendMessage("SetWasDropped", SendMessageOptions.DontRequireReceiver);
                            baseEntity.Spawn();
                            baseEntity.EnableSaving(false);
                            foreach (Item item in baseEntity.inventory.itemList.ToList()) { if (item != null) { item.MoveToContainer(loot.inventory); } }
                            NextFrame(() => { if (baseEntity != null && !baseEntity.IsDestroyed) { baseEntity.Kill(); } });
                        }
                    }
                }
                EventBoats.Add(tugboat);
                storedData.EventTugs.Add(tugboat.net.ID.Value);
                if (config.EventHooks)
                {
                    Interface.Oxide.CallHook("EventTugStarted", tugboat);
                }
                SaveData();
                tugboat.SetFlag(Tugboat.Flags.On, true);
                tugboat.gasPedal = 1;
                AutoNavigation AN = tugboat.gameObject.AddComponent<AutoNavigation>();
                if (config.DisableBuoyantSleep)
                {
                    var TugBuoyancy = tugboat.GetComponent<Buoyancy>();
                    if (TugBuoyancy != null)
                    {
                        TugBuoyancy.CancelInvoke("CheckSleepState");
                        TugBuoyancy.Invoke(new Action(TugBuoyancy.Wake), 0f); //Force Awake
                    }
                }
                if (config.SpawnAnnounce || config.SpawnAnnounceChat)
                {
                    timer.Once(5, () =>
                    {
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                        {
                            if (!player.IsSleeping() && permission.UserHasPermission(player.UserIDString, "TugMe.Announce"))
                            {
                                if (config.SpawnAnnounceChat) { MessageIcon(player, message(player, "C6", PositionToGridCoord(tugboat.transform.position))); }
                                if (config.SpawnAnnounce) { player.SendConsoleCommand("gametip.ShowToast", config.AnnounceStyle, message(player, "C6", PositionToGridCoord(tugboat.transform.position))); }
                            }
                        }
                    });
                }
            });
        }

        //Create a woodbox for the loot
        private StorageContainer CreateStoragefunction(Vector3 offset, BaseEntity parent)
        {
            StorageContainer itembox = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab") as StorageContainer;
            itembox.Spawn();
            itembox.EnableSaving(false);
            itembox.SetParent(parent, true, false);
            itembox.skinID = 931816387;
            itembox.transform.localPosition = offset;
            itembox.transform.localRotation = Quaternion.Euler(0,0,0);
            itembox.inventory.capacity = 30;
            if (config.ApplyCodeLock)
            {
                CodeLock alock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab") as CodeLock;
                alock.Spawn();
                alock.EnableSaving(false);
                alock.OwnerID = 0;
                alock.code = UnityEngine.Random.Range(1111, 9999).ToString();
                alock.SetParent(itembox, itembox.GetSlotAnchorName(BaseEntity.Slot.Lock));
                itembox.SetSlot(BaseEntity.Slot.Lock, alock);
                alock.SetFlag(BaseEntity.Flags.Locked, true);
            }
            itembox.SendNetworkUpdateImmediate();
            return itembox;
        }

        //Create doors on event tug
        private void CreateDoors(Tugboat boat, Vector3 position, Vector3 rotation)
        {
            StabilityEntity door = GameManager.server.CreateEntity("assets/prefabs/building/door.hinged/door.hinged.wood.prefab", boat.transform.position + boat.transform.rotation * position, boat.transform.rotation * Quaternion.Euler(rotation)) as StabilityEntity;
            door.Spawn();
            door.EnableSaving(false);
            door.SetParent(boat, true, false);
            door.InitializeHealth(config.DoorHealth, config.DoorHealth);
            door.SetFlag(BaseEntity.Flags.Locked, true);
            (door as Door).canTakeLock = false;
            door.grounded = true;
            door.skinID = config.DoorSkinID;
            door.SendNetworkUpdateImmediate(true);
        }

        //Create gun for event npc
        private void CreateGun(ScientistNPC npc, string ItemName, string Attachment)
        {
            try //Try catch since some times half way though NPC might die and its quicker then adding null check each line
            {
                if (string.IsNullOrEmpty(ItemName)) { return; }
                Item item = ItemManager.CreateByName(ItemName, 1, 0);
                if (item == null) { return; }
                BaseEntity be = item.GetHeldEntity();
                BaseEntity we = item.GetWorldEntity();
                if (be != null) { be.isSpawned = false; }
                if (we != null) { we.isSpawned = false; }
                timer.Once(3f, () =>
                {
                    if (be != null) { be.isSpawned = true; }
                    if (we != null) { we.isSpawned = true; }
                });
                if (!item.MoveToContainer(npc.inventory.containerBelt, -1, false)) { item.Remove(); return; }
                if (be != null && be is BaseProjectile)
                {
                    if (!string.IsNullOrEmpty(Attachment))
                    {
                        Item moditem = ItemManager.CreateByName(Attachment, 1, 0);
                        if (moditem != null && item.contents != null)
                        {
                            BaseEntity bemi = moditem.GetHeldEntity();
                            BaseEntity wemi = moditem.GetWorldEntity();
                            if (bemi != null) { bemi.isSpawned = false; }
                            if (wemi != null) { wemi.isSpawned = false; }
                            timer.Once(3f, () =>
                            {
                                if (bemi != null) { bemi.isSpawned = true; }
                                if (wemi != null) { wemi.isSpawned = true; }
                            });
                            if (!moditem.MoveToContainer(item.contents)) { item.contents.Insert(moditem); }
                        }
                    }
                    timer.Once(5f, () => { if (npc != null && item != null) { npc.UpdateActiveItem(item.uid); } });
                }
            }
            catch { }
        }

        private void DestroyNonConvex(BaseEntity ent)
        {
            foreach (var mesh in ent.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private void AttachToTug(BaseEntity ent, BaseEntity parent, bool canpickup = false)
        {
            DestroyNonConvex(ent);
            ent.Spawn();
            ent.EnableSaving(false);
            ent.SetParent(parent, true, false);
            if (ent as BaseCombatEntity != null) { (ent as BaseCombatEntity).pickup.enabled = canpickup; }
            ent.SendNetworkUpdateImmediate();
        }

        //Create event turrets
        private void CreateAutoTurrets(Tugboat tugboat, Dictionary<ScientistNPC, Vector3> NPCs)
        {
            foreach (TurretPos position in _plugin.config.NPCAutoTurretPoints)
            {
                AutoTurret aturret = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", tugboat.transform.position + tugboat.transform.rotation * position.Position, tugboat.transform.rotation * Quaternion.Euler(position.Rotation)) as AutoTurret;
                AttachToTug(aturret, tugboat);
                NextFrame(() =>
                {
                    if (aturret != null && aturret.AttachedWeapon == null)
                    {
                        Item item = ItemManager.CreateByName(config.AutoTurretGun, 1, 0);
                        if (!item.MoveToContainer(aturret.inventory, 0, false)) { item.Remove(); }
                        else { aturret.UpdateAttachedWeapon(); }
                    }
                    aturret.InitializeHealth(config.AutoTurretHealth, config.AutoTurretHealth);
                    FillAmmoTurret(aturret);
                    foreach (var npc in NPCs.Keys)
                    {
                        PlayerNameID playerNameID = new PlayerNameID();
                        playerNameID.userid = npc.userID;
                        playerNameID.username = npc.displayName;
                        aturret.authorizedPlayers.Add(playerNameID);
                    }
                    if (!aturret.IsPowered()) { aturret.InitiateStartup(); aturret.authDirty = true; }
                });
            }
        }

        //Create event sams
        private void CreateSamTurrets(Tugboat tugboat)
        {
            foreach (TurretPos position in _plugin.config.NPCSamsitePoints)
            {
                SamSite sam = GameManager.server.CreateEntity("assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab", tugboat.transform.position + tugboat.transform.rotation * position.Position, tugboat.transform.rotation * Quaternion.Euler(position.Rotation)) as SamSite;
                AttachToTug(sam, tugboat);
                sam.SetFlag(BaseEntity.Flags.Locked, true);
                sam.InitializeHealth(config.SamsiteHealth, config.SamsiteHealth);
                NextFrame(() =>
                {
                    if (sam.ammoType == null) { sam.ammoType = ItemManager.FindItemDefinition("ammo.rocket.sam"); }
                    var ammo = sam.inventory.GetSlot(0);
                    if (ammo == null) { sam.inventory.AddItem(sam.ammoType, config.SamsiteAmmo); }
                });
            }
        }

        //Create event guntraps
        private void CreateGunTraps(Tugboat tugboat)
        {
            foreach (TurretPos position in _plugin.config.NPCGuntrapPoints)
            {
                GunTrap guntrap = GameManager.server.CreateEntity("assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab", tugboat.transform.position + tugboat.transform.rotation * position.Position, tugboat.transform.rotation * Quaternion.Euler(position.Rotation)) as GunTrap;
                AttachToTug(guntrap, tugboat);
                guntrap.SetFlag(BaseEntity.Flags.Locked, true);
                guntrap.InitializeHealth(config.GuntrapHealth, config.GuntrapHealth);
                NextFrame(() =>
                {
                    if (guntrap.ammoType == null) { guntrap.ammoType = ItemManager.FindItemDefinition("ammo.handmade.shell"); }
                    var ammo = guntrap.inventory.GetSlot(0);
                    if (ammo == null) { guntrap.inventory.AddItem(guntrap.ammoType, config.GuntrapAmmo); }
                });
            }
        }

        //Create event NPC
        private Dictionary<ScientistNPC, Vector3> CreateNPCScientist(Tugboat baseEntity)
        {
            if (baseEntity == null) { return null; }
            Dictionary<ScientistNPC, Vector3> NPCList = new Dictionary<ScientistNPC, Vector3>();
            if (config.UseBetterNPC && NpcSpawn != null)
            {
                HashSet<string> states = new HashSet<string> { "IdleState", "CombatStationaryState" };
                var WearItems = new List<NpcWear> { new NpcWear { ShortName = config.NPCAttire, SkinID = 0 } };
                var BeltItems = new List<NpcBelt>
                {
                                new NpcBelt { ShortName = config.NPCGun, Amount = 1, SkinID = 0, Mods = new List<string> { "weapon.mod.holosight", "weapon.mod.flashlight" }, Ammo = string.Empty },
                                new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new List<string>(), Ammo = string.Empty },
                };
                JObject objectConfig = new JObject
                {
                    ["Name"] = config.NPCPrefix + RandomUsernames.Get((ulong)UnityEngine.Random.Range(0, 10000000)),
                    ["WearItems"] = new JArray { WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = config.UseKitsPlugin ? config.NPCAttire : "",
                    ["Health"] = config.NPCHealth,
                    ["RoamRange"] = 0,
                    ["ChaseRange"] = 0,
                    ["SenseRange"] = 100,
                    ["ListenRange"] = 50,
                    ["AttackRangeMultiplier"] = config.NPCAttackRange,
                    ["CheckVisionCone"] = 1,
                    ["VisionCone"] = 1,
                    ["HostileTargetsOnly"] = false,
                    ["DamageScale"] = config.NPCDamageScaler,
                    ["TurretDamageScale"] = 1f,
                    ["AimConeScale"] = 1,
                    ["DisableRadio"] = true,
                    ["CanRunAwayWater"] = false,
                    ["CanSleep"] = false,
                    ["SleepDistance"] = 0,
                    ["Speed"] = 3,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = 60,
                    ["States"] = new JArray { states }
                };
                foreach (Vector3 position in _plugin.config.NPCSpawnPoints)
                {
                    ScientistNPC npc = (ScientistNPC)NpcSpawn.Call("SpawnNpc", baseEntity.transform.position, objectConfig);
                    if (npc != null)
                    {
                        ActiveNPCs.Add(npc.net.ID.Value);
                        NPCList.Add(npc, position);
                        timer.Once(1, () =>
                        {
                            if (npc != null)
                            {
                                NpcSpawn.Call("SetParentEntity", npc, baseEntity, baseEntity.transform.InverseTransformPoint(position));
                                npc.SetParent(baseEntity);
                                npc.transform.localPosition = position;
                                npc.SendNetworkUpdateImmediate();
                            }
                        });
                    }
                }
                return NPCList;
            }

            var prefabName = StringPool.Get(config.AlternativeNPC ? 3763080634 : 1536035819);
            foreach (Vector3 position in _plugin.config.NPCSpawnPoints)
            {
                if (position == null) { continue; }
                try
                {
                    var go = Facepunch.Instantiate.GameObject(GameManager.server.FindPrefab(prefabName));
                    go.SetActive(false);
                    go.name = prefabName;
                    go.transform.position = new Vector3(0, 3, 0);
                    ScientistNPC npc = go.GetComponent<ScientistNPC>();
                    SceneManager.MoveGameObjectToScene(go, Rust.Server.EntityScene);
                    npc.userID = (ulong)UnityEngine.Random.Range(0, 10000000);
                    npc.UserIDString = npc.userID.ToString();
                    npc.displayName = config.NPCPrefix + RandomUsernames.Get(npc.userID);
                    ScientistBrain brain = npc.GetComponent<ScientistBrain>();
                    if (brain != null) { brain.SenseRange = 100; brain.CheckVisionCone = false; brain.CheckLOS = false; }
                    go.SetActive(true);
                    npc.Spawn();
                    if (npc == null) { continue; }
                    ActiveNPCs.Add(npc.net.ID.Value);
                    NPCList.Add(npc, position);
                    npc.EnableSaving(false);
                    npc.isSpawned = false;
                    npc.InitializeHealth(config.NPCHealth, config.NPCHealth);
                    npc.aimConeScale *= config.NPCAimScaler;
                    npc.damageScale *= config.NPCDamageScaler;
                    npc.transform.position = baseEntity.transform.position;
                    BaseNavigator baseNavigator = npc.GetComponent<BaseNavigator>();
                    if (baseNavigator != null) { baseNavigator.CanUseNavMesh = false; }
                    timer.Once(Core.Random.Range(3, 4), () =>
                    {
                        baseEntity.gasPedal = 1;
                        if (npc != null)
                        {
                            npc.inventory.Strip();
                            if (config.UseKitsPlugin)
                            {
                                if (Kits == null) { Puts("Kits Plugin Not Loaded!"); }
                                else
                                {
                                    timer.Once(5, () =>
                                    {
                                        if (npc != null && npc.net != null && npc.net.group != null && npc?.inventory?.containerWear != null)
                                        {
                                            object success = Kits?.Call("GiveKit", npc, config.NPCAttire);
                                            if (success == null || !(success is bool)) { Puts("Failed to give NPC Kit"); }
                                            timer.Once(5, () =>
                                            {
                                                if (npc == null) { return; }
                                                Item projectileItem = null;
                                                foreach (var item in npc.inventory.containerBelt.itemList.ToList())
                                                {
                                                    if (item.GetHeldEntity() is BaseProjectile)
                                                    {
                                                        projectileItem = item;
                                                        break;
                                                    }
                                                    if (item.GetHeldEntity() is MedicalTool)
                                                    {
                                                        item.MoveToContainer(npc.inventory.containerMain);
                                                        continue;
                                                    }
                                                }
                                                if (projectileItem == null)
                                                {
                                                    foreach (var item in npc.inventory.containerBelt.itemList.ToList())
                                                    {
                                                        if (item.GetHeldEntity() is BaseMelee)
                                                        {
                                                            projectileItem = item;
                                                            break;
                                                        }
                                                    }
                                                }
                                                if (projectileItem != null)
                                                {
                                                    npc.UpdateActiveItem(projectileItem.uid);
                                                    npc.inventory.UpdatedVisibleHolsteredItems();
                                                    timer.Once(1f, () => { npc.AttemptReload(); });
                                                }
                                                npc.SendNetworkUpdateImmediate();
                                                npc.inventory.ServerUpdate(0f);
                                            });
                                        }
                                    });
                                }
                            }
                            else
                            {
                                Item item = ItemManager.CreateByName(config.NPCAttire, 1, 0);
                                timer.Once(4, () => //Wait 4 secs for super shit slow servers to have the NPC ready.
                                {
                                    if (item != null && npc != null && npc.net != null && npc.net.group != null && npc?.inventory?.containerWear != null)
                                    {
                                        npc.inventory.containerWear.onItemAddedRemoved = null;
                                        npc.inventory.containerWear.Insert(item);
                                        CreateGun(npc, config.NPCGun, config.NPCGunAttachment);
                                        npc?.SendNetworkUpdateImmediate();
                                        npc.inventory.ServerUpdate(0f);
                                        item.MarkDirty();
                                    }
                                    else
                                    {
                                        timer.Once(10, () => //Wait 14 secs Final try to kit
                                        {
                                            if (item != null && npc != null && npc.net != null && npc.net.group != null && npc?.inventory?.containerWear != null)
                                            {
                                                npc.inventory.containerWear.onItemAddedRemoved = null;
                                                npc.inventory.containerWear.Insert(item);
                                                CreateGun(npc, config.NPCGun, config.NPCGunAttachment);
                                                npc?.SendNetworkUpdateImmediate();
                                                npc.inventory.ServerUpdate(0f);
                                                item.MarkDirty();
                                            }
                                        });
                                    }
                                });
                            }
                            npc.SetParent(baseEntity);
                            npc.transform.localPosition = position;
                            npc.isSpawned = true;
                            npc.Brain.AttackRangeMultiplier = config.NPCAttackRange;
                            npc.Invoke(() =>
                            {
                                if (npc?.net?.ID != null)
                                {
                                    ActiveNPCs.Remove(npc.net.ID.Value);
                                    if (!npc.IsDestroyed) { npc.Kill(); }
                                }


                            }, config.EventTimeout + 120);
                        }
                    });
                }
                catch { Puts("Failed To Spawn NPC @ " + position); }
            }
            //Check NPCs parented
            timer.Once(60, () =>
            {
                if (baseEntity != null && NPCList.Count > 0)
                {
                    foreach (var reparent in NPCList)
                    {
                        if (reparent.Key != null)
                        {
                            if (!reparent.Key.HasParent())
                            {
                                reparent.Key.SetParent(baseEntity);
                                reparent.Key.transform.localPosition = reparent.Value;
                                reparent.Key.SendNetworkUpdateImmediate();
                            }
                        }
                    }
                }
            });
            return NPCList;
        }

        //Spawn NPCs to buy tug from
        private void CreateNPCs()
        {
            foreach (MonumentInfo info in TerrainMeta.Path.Monuments)
            {
                foreach (VendorPos vp in config.VendorPositions)
                {
                    //Check config block outs
                    if (info.name.Contains("harbor_") && !config.EnableTugSpawners) { break; }
                    if (info.name.Contains("fishing_village") && !config.EnableTugSpawners2) { break; }
                    if (info.name.Contains("ferry_terminal") && !config.EnableTugSpawners3) { break; }
                    //Get positions from config file
                    if (vp.PrefabPath == info.name) { TugDealers.Add(NewTugger(info.transform.position + info.transform.rotation * vp.OffsetPosition, info.transform.rotation * Quaternion.Euler(vp.OffsetRotation)), info.transform.position + info.transform.rotation * vp.OffsetSpawnPoint); }
                }
            }
            //Custom Vendors
            foreach (var t in config.CustomPositions)
            {
                if (t.Position.x == 0 && t.Position.y == 0) { continue; } //Ignore the example
                TugDealers.Add(NewTugger(t.Position, Quaternion.Euler(t.Rotation)), t.SpawnPoint);
            }
            if (config.MarkVendorsOnMap) { UpdateVenderTugIcon(); }
        }

        private void FillAmmoTurret(AutoTurret turret)
        {
            var attachedWeapon = turret.GetAttachedWeapon();
            if (attachedWeapon == null)
            {
                turret.Invoke(() => FillAmmoTurret(turret), 0.2f);
                return;
            }
            turret.inventory.AddItem(attachedWeapon.primaryMagazine.ammoType, config.AutoTurretAmmo, 0uL);
            attachedWeapon.primaryMagazine.contents = attachedWeapon.primaryMagazine.capacity;
            attachedWeapon.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            turret.Invoke(turret.UpdateTotalAmmo, 0.25f);
            turret.dropFloats = false;
            turret.dropsLoot = false;
            turret.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
        }

        private void CreateFire(float lifetime, Vector3 FireOffset, float FireDelay, BaseEntity parant)
        {
            timer.Once(FireDelay, () =>
            {
                if (parant == null || parant.IsDestroyed) { return; }
                Effect.server.Run("assets/prefabs/tools/c4/effects/c4_explosion.prefab", parant.transform.position + parant.transform.rotation * FireOffset, Vector3.up, null, true);
                Effect.server.Run("assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab", parant.transform.position + parant.transform.rotation * FireOffset, Vector3.up, null, true);
                FireBall fireBallSPAWN = GameManager.server.CreateEntity("assets/bundled/prefabs/oilfireballsmall.prefab", parant.transform.position) as FireBall;
                fireBallSPAWN.Spawn();
                fireBallSPAWN.EnableSaving(false);
                fireBallSPAWN.SetParent(parant, true, true);
                fireBallSPAWN.transform.localPosition = FireOffset;
                Rigidbody rb = fireBallSPAWN.GetComponent<Rigidbody>();
                rb.isKinematic = false;
                fireBallSPAWN.CancelInvoke(fireBallSPAWN.Extinguish);
                fireBallSPAWN.Invoke(fireBallSPAWN.Extinguish, lifetime);
            });
        }

        private List<byte[]> ArrayToByteArrayList(byte[] bytes, int offset = 0)
        {
            List<byte[]> packetbytes = new List<byte[]>();
            List<int> packetchecksum = new List<int>();
            for (int checksum = 0; checksum < bytes.Length - offset;)
            {
                packetchecksum.Add(BitConverter.ToInt32(bytes, offset));
                offset += 4;
                checksum = packetchecksum.Sum();
                if (checksum > bytes.Length - offset)
                {
                    Puts("Unpacking Stream Failed!");
                    return null;
                }
            }
            foreach (int size in packetchecksum) { packetbytes.Add(bytes.Skip(offset).Take(size).ToArray()); offset += size; }
            return packetbytes;
        }

        public void VoicePacket(string response, Tugboat Target, bool loud = false)
        {
            List<byte[]> VD = ArrayToByteArrayList(Convert.FromBase64String(response));
            BasePlayer newPlayer = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab").ToPlayer();
            newPlayer.Spawn();
            newPlayer.EnableSaving(false);
            newPlayer.modelState.ducked = true;
            newPlayer.SetParent(Target);
            newPlayer.transform.localScale = Vector3.zero;
            newPlayer.transform.localPosition = new Vector3(0f, -0.5f, 4.6f);
            BaseEntity.Query.Server.RemovePlayer(newPlayer);
            if (loud)
            {
                //Create megaphone and give it to target
                ItemDefinition itemDefinition = ItemManager.FindItemDefinition("megaphone");
                Item megaphone = ItemManager.Create(itemDefinition, 1, 0);
                newPlayer.GiveItem(megaphone);
                newPlayer.UpdateActiveItem(megaphone.uid);
                Megaphone MP = newPlayer.GetHeldEntity() as Megaphone;
                //switch it on
                MP.SetFlag(Megaphone.Flags.On, true);
            }
            newPlayer.SendNetworkUpdateImmediate();
            VoicePlayers.Add(newPlayer.userID);
            timer.Once((VD.Count / 10) + 4f, () => { VoicePlayers.Remove(newPlayer.userID); newPlayer.Kill(); });
            NextFrame(() => { InvokeHandler.Instance.StartCoroutine(PlayVoicefunction(VD, newPlayer)); });
        }

        public void WebPlayback(string URL, Tugboat Target, bool loud = false)
        {
            if (Target == null || string.IsNullOrEmpty(URL) || PlayingEffect.Contains(Target.net.ID.Value)) { return; }
            bool flag = false;
            foreach (BasePlayer bp in BasePlayer.activePlayerList) //Check players nearby
            {
                if (Target.Distance(bp.transform.position) < 100 && bp.IsConnected)
                {
                    flag = true;
                    break;
                }
            }
            if (!flag) { return; }
            PlayingEffect.Add(Target.net.ID.Value);
            timer.Once(10, () => { PlayingEffect.Remove(Target.net.ID.Value); });
            if (!URL.Contains("http"))
            {
                if (Directory.Exists(URL))
                {
                    string[] filePaths = Directory.GetFiles(URL, "*.voice", SearchOption.TopDirectoryOnly);
                    if (filePaths.Length > 0)
                    {
                        string vp = File.ReadAllText(filePaths.GetRandom());
                        if (IsBase64String(vp)) { VoicePacket(vp, Target, loud); }
                    }
                }
                return;
            }
            webrequest.Enqueue(URL, null, (code, response) =>
            {
                if (code != 200 || response == null) { Puts($"Error: {code}"); return; }
                if (IsBase64String(response)) { VoicePacket(response, Target, loud); }
                else { Puts("WebVoice URL Fault!"); }
            }, this, RequestMethod.GET);
        }

        //Play Voice Data
        private IEnumerator PlayVoicefunction(List<byte[]> VD, BasePlayer bot)
        {
            if (VD == null || bot == null) yield break;
            foreach (byte[] data in VD)
            {
                if (Network.Net.sv.IsConnected())
                {
                    NetWrite netWrite = Network.Net.sv.StartWrite();
                    netWrite.PacketID(Network.Message.Type.VoiceData);
                    netWrite.UInt64(bot.net.ID.Value);
                    netWrite.BytesWithSize(data);
                    netWrite.Send(new SendInfo(BaseNetworkable.GetConnectionsWithin(bot.transform.position, 100)) { priority = Network.Priority.Immediate });
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        private bool WeaponsCoolDown(Tugboat tug)
        {
            if (Shooting.Contains(tug.net.ID.Value)) { return true; }
            Shooting.Add(tug.net.ID.Value);
            timer.Once(config.TugboatminesCD, () => { if (tug != null) { Shooting.Remove(tug.net.ID.Value); } });
            return false;
        }

        private void Take(BasePlayer player, int itemid, int amount)
        {
            var collect = Facepunch.Pool.Get<List<Item>>();
            player.inventory.Take(collect, itemid, amount); // Take only calls RemoveFromContainer
            foreach (Item item in collect)
            {
                item.Remove();
            }
            Facepunch.Pool.FreeUnmanaged(ref collect);
        }

        //FireTorpedo
        private void Tugboat_TorpedoMod(BasePlayer player, Tugboat tug)
        {
            if (tug == null || player == null || !tug.IsDriver(player) || WeaponsCoolDown(tug) || !permission.UserHasPermission(player.UserIDString, "TugMe.Torpedo")) { return; }
            int torpedoid = ItemManager.FindItemDefinition("submarine.torpedo.straight").itemid;
            if (player.inventory.GetAmount(torpedoid) != 0)
            {
                BaseEntity baseEntity = GameManager.server.CreateEntity("assets/prefabs/ammo/torpedo/torpedostraight.prefab", tug.transform.position + (tug.transform.forward * 12.75f) + tug.transform.up * 1f, default(Quaternion), true);
                ServerProjectile projectile = baseEntity.GetComponent<ServerProjectile>();
                Vector3 vector = projectile.initialVelocity + tug.transform.forward * projectile.speed;
                Take(player, torpedoid, 1);
                projectile.InitializeVelocity(vector);
                if (player.IsValid())
                {
                    baseEntity.creatorEntity = player;
                    baseEntity.OwnerID = player.userID;
                }
                baseEntity.Spawn();
                MessageIcon(player, "D4");
                return;
            }
            MessageIcon(player, "D3");
        }

        //Drop Water Mine
        private void Tugboat_WaterMineMod(BasePlayer player, Tugboat tug)
        {
            if (tug == null || player == null || !tug.IsDriver(player) || WeaponsCoolDown(tug) || !permission.UserHasPermission(player.UserIDString, "TugMe.Mine")) { return; }
            int InnerTube = ItemManager.FindItemDefinition("innertube").itemid;
            int C4 = ItemManager.FindItemDefinition("explosive.timed").itemid;
            if (player.inventory.GetAmount(InnerTube) == 0)
            {
                MessageIcon(player, "D5");
                return;
            }
            if (player.inventory.GetAmount(C4) == 0)
            {
                MessageIcon(player, "D6");
                return;
            }
            BaseCombatEntity innertube = GameManager.server.CreateEntity("assets/prefabs/misc/summer_dlc/inner_tube/innertube.deployed.prefab", tug.transform.position + (tug.transform.forward * -12.75f) + tug.transform.up * 1f) as BaseCombatEntity;
            RFTimedExplosive explosive = GameManager.server.CreateEntity("assets/prefabs/tools/c4/explosive.timed.deployed.prefab", tug.transform.position + (tug.transform.forward * -12.75f) + tug.transform.up * 1f) as RFTimedExplosive;
            innertube.Spawn();
            explosive.Spawn();
            Take(player, InnerTube, 1);
            Take(player, C4, 1);
            explosive.SetParent(innertube, true, true);
            innertube.pickup.enabled = false;
            innertube.InitializeHealth(config.TugboatminesHealth, config.TugboatminesHealth);
            explosive.SetMotionEnabled(false);
            explosive.SetCollisionEnabled(false);
            explosive.SetFrequency(UnityEngine.Random.Range(1111, 9999)); //Set to given frequency
            explosive.ArmRF(); //trigger Arm SFX and conditions
            explosive.CancelInvoke(new Action(explosive.Explode)); //Block auto exploding
            explosive.SetFlag(BaseEntity.Flags.On, false);
            if (player.IsValid())
            {
                explosive.creatorEntity = player;
                explosive.OwnerID = player.userID;
            }
            NextFrame(() => { innertube.SetFlag(BaseEntity.Flags.On, false); });
            MessageIcon(player, "D7");
            return;
        }

        //Horn Code
        private void Tugboat_HornMod(BasePlayer player, Tugboat tug)
        {
            if (player == null || tug == null) { return; }
            if (!permission.UserHasPermission(player.UserIDString, "TugMe.Horn")) { return; }
            if (tug.IsDriver(player))
            {
                if (config.Horn)
                {
                    if (!PlayingEffect.Contains(tug.net.ID.Value))
                    {
                        PlayingEffect.Add(tug.net.ID.Value);
                        timer.Once(3, () => { PlayingEffect.Remove(tug.net.ID.Value); });
                        Effect.server.Run("assets/content/nexus/ferry/effects/nexus-ferry-departure-horn.prefab", tug.transform.position);
                    }
                }
                else if (_plugin.config.HornURL.Length > 0) { WebPlayback(_plugin.config.HornURL, tug, true); }
            }
        }

        //Override placement limitation
        private void Tugboat_PlacementMod(BasePlayer player, Tugboat tug, Item heldEntity, BaseEntity entity, Vector3 pos)
        {
            if (!permission.UserHasPermission(player.UserIDString, "TugMe.Place")) { return; }
            //Apply Amount Limits
            if ((config.MaxSam != -1 || config.MaxAT != -1) && (heldEntity?.info?.itemid == -1009359066 || heldEntity?.info?.itemid == -2139580305)) //samsite || autoturret
            {
                int Fsam = 0;
                int Fat = 0;
                foreach (var child in entity.children)
                {
                    if (child is SamSite) { Fsam++; }
                    else if (child is AutoTurret) { Fat++; }
                }
                if (config.MaxSam != -1 && Fsam >= config.MaxSam && heldEntity?.info?.itemid == -1009359066) //samsite
                {
                    MessageIcon(player, "G4", Fsam, config.MaxSam);
                    return;
                }
                if (config.MaxAT != -1 && Fat >= config.MaxAT && heldEntity?.info?.itemid == -2139580305) //autoturret
                {
                    MessageIcon(player, "G4", Fat, config.MaxAT);
                    return;
                }
            }
            foreach (var c in entity.children)
            {
                if (c == null || c is BasePlayer) { continue; }
                float distance = Vector3.Distance(c.transform.position, pos);
                if (distance <= config.DistanceCheck)
                {
                    MessageIcon(player, "C2", c.ShortPrefabName, String.Format("{0:0.0}", distance));
                    return;
                }
            }
            BaseEntity ent = GameManager.server.CreateEntity(heldEntity?.info?.GetComponent<ItemModDeployable>().entityPrefab.resourcePath, pos, entity.transform.rotation, true);
            if (ent == null) { return; }
            ent.transform.position = pos;
            DestroyNonConvex(ent);
            ent.Spawn();
            ent.SetParent(entity, true, false);
            ent.OwnerID = player.userID;
            if (AllowKinematic(ent))
            {
                Rigidbody component = ent.GetComponent<Rigidbody>();
                if (component != null) { component.isKinematic = true; }
            }
            Take(player, heldEntity.info.itemid, 1);
            if (storedData.RunningTugs.Contains(player.GetParentEntity().net.ID.Value)) { Tugboat_TogglePower(new List<BaseEntity>() { ent }, true, tug.net.ID.Value); }
            return;
        }

        private bool AllowKinematic(BaseEntity entity)
        {
            if (entity is Drone) { return false; }

            return true;
        }

        //Apply custom tugboat settings
        private void SetupTugSettings(Tugboat tugboat)
        {
            if (config.BlockNative && tugboat.OwnerID == 0)
            {
                if (config.NativeAllowAlreadyAuthed)
                {
                    foreach (BaseEntity child in tugboat.children)
                    {
                        VehiclePrivilege vehiclePrivilege = child as VehiclePrivilege;
                        if (vehiclePrivilege != null && !vehiclePrivilege.AnyAuthed())
                        {
                            try { tugboat.Kill(); } catch { } // Kill Native
                            return;
                        }
                    }
                }
                else
                {
                    if (config.BlockMove)
                    {
                        try
                        {
                            tugboat.transform.position = new Vector3(0, -11, 0);
                            tugboat.rigidBody.Sleep();
                            tugboat.rigidBody.isKinematic = true;
                            tugboat.rigidBody.detectCollisions = false;
                            tugboat.rigidBody.constraints = RigidbodyConstraints.FreezeAll;
                            for (int i = tugboat.children.Count - 1; i >= 0; i--)
                            {
                                if (tugboat.children[i] != null && !tugboat.children[i].IsDestroyed)
                                {
                                    tugboat.children[i].Kill();
                                }
                            }
                        }
                        catch { }
                        tugboat.SendNetworkUpdateImmediate();
                    }
                    else
                    {
                        try { tugboat.Kill(); return; } catch { } // Kill Native
                    }
                }
            }
            if (config.LimitPlayerSpawned && tugboat.OwnerID == 0) { return; }

            StorageContainer fuelContainer = tugboat.fuelSystem.GetFuelContainer();
            if (config.FuelAmount > 0)
            { //Give Spawn Fuel
                if (fuelContainer != null && fuelContainer.inventory.itemList.Count == 0)
                {
                    fuelContainer.inventory.AddItem(fuelContainer.allowedItem, config.FuelAmount);
                }
            }
            if (config.DisableFuelRequirement)
            {
                tugboat.fuelPerSec = 0f;
                if (fuelContainer != null)
                {
                    fuelContainer.inventory.AddItem(fuelContainer.allowedItem, 1);
                    fuelContainer.SetFlag(BaseEntity.Flags.Locked, true);
                }
            }
            else if (config.FuelPerSec >= 0) { tugboat.fuelPerSec = config.FuelPerSec; }
            if (config.engineThrust != -1) { tugboat.engineThrust = config.engineThrust; }
            if (config.BoatHealth != -1) { tugboat.InitializeHealth(config.BoatHealth, config.BoatHealth); }
            tugboat.globalBroadcast = config.SpawnGlobal;
            if (config.PowerFromEngine)
            {
                if (storedData.RunningTugs.Contains(tugboat.net.ID.Value))
                {
                    Tugboat_TogglePower(tugboat.children, true, tugboat.net.ID.Value);
                }
            }
            if (config.DoubleDoorFrame && tugboat.OwnerID != 0)
            {
                foreach (BaseEntity child in tugboat.children) { if (child != null && child.prefabID == 919059809) { return; } } //Check if already has door frame
                //Spawn a door frame
                var entity = GameManager.server.CreateEntity("assets/prefabs/building core/wall.frame/wall.frame.prefab", new Vector3(0, -499, 0), tugboat.transform.rotation * Quaternion.Euler(new Vector3(0, 90, 0)));
                if (entity == null) { return; }
                entity.Spawn();
                Rigidbody component = entity.GetComponent<Rigidbody>();
                if (component != null) { component.isKinematic = true; }
                entity.OwnerID = tugboat.OwnerID;
                entity.SetParent(tugboat, true, true);
                entity.transform.localPosition = new Vector3(0, 1.53f, -5.1f);
                var buildingBlock = entity as BuildingBlock;
                if (buildingBlock != null)
                {
                    buildingBlock.SetGrade((BuildingGrade.Enum)4);
                    buildingBlock.grounded = true;
                    buildingBlock.AttachToBuilding(BuildingManager.server.NewBuildingID());
                    buildingBlock.health = buildingBlock.MaxHealth();
                }
            }
        }

        private NPCTalking NewTugger(Vector3 position, Quaternion rotation)
        {
            List<NPCTalking> list = Pool.Get<List<NPCTalking>>(); //Pooling to save list memory
            Vis.Entities<NPCTalking>(position, 3, list); //Limit layers to players only and collide to ignore vanished players.
            foreach (NPCTalking t in list) { if (t != null && !t.IsDestroyed) { t.Kill(); } }
            Pool.FreeUnmanaged(ref list); //Free pooling
            NPCTalking player = GameManager.server.CreateEntity("assets/prefabs/npc/bandit/shopkeepers/missionprovider_test.prefab", position, rotation, true) as NPCTalking;
            player.Spawn();
            player.inventory.Strip();
            player.EnableSaving(false);
            if (config.UseKitsPluginVendor)
            {
                if (Kits == null)
                {
                    Puts("Kits Plugin Not Loaded!");
                    return player;
                }
                else
                {
                    timer.Once(3, () =>
                    {
                        object success = Kits?.Call("GiveKit", player, config.NPCVendorAttire);
                        if (success == null || !(success is bool)) { Puts("Failed to give NPC Kit"); }
                        else { timer.Once(3, () => {
                            List<Item> list = Pool.Get<List<Item>>();
                            player.inventory.GetAllItems(list);
                            foreach (Item i in list) { i.condition = 999999999999; }
                            Pool.FreeUnmanaged(ref list);
                        }); }
                    });
                }
            }
            else
            {
                Item item = ItemManager.CreateByName(config.NPCVendorAttire, 1, 0);
                item.condition = 99999999999999;
                timer.Once(4, () =>
                {
                    {
                        if (item != null)
                        {
                            player.inventory.containerWear.Insert(item);
                            player.SendNetworkUpdateImmediate();
                        }
                    }
                });
            }
            return player;
        }

        private Tugboat NewTugBoat(Vector3 position, Quaternion rotation, ulong OwnerID)
        {
            position.y = 0.1f; //Stops splashing if cargo node below water
            Tugboat boat = GameManager.server.CreateEntity("assets/content/vehicles/boats/tugboat/tugboat.prefab", position, rotation, true) as Tugboat;
            boat.OwnerID = OwnerID;
            boat.Spawn();
            return boat;
        }

        void AddWater()
        {
            if (WaterSystem?.Count == 0) { return; }
            foreach (var baseNetworkable in WaterSystem)
            {
                WaterPurifier purifier = baseNetworkable as WaterPurifier;
                if (purifier == null || !purifier.IsPowered()) { return; }

                purifier.inventory.AddItem(ItemManager.FindItemDefinition("water.salt"), 20);
                foreach (var c in purifier.GetParentEntity().children)
                {
                    if (c != null && c is Sprinkler)
                    {
                        Sprinkler sprinkler = (Sprinkler)c;
                        List<BaseEntity> list = Facepunch.Pool.Get<List<BaseEntity>>();
                        Vector3 position = sprinkler.transform.position;
                        Vector3 up = sprinkler.transform.up;
                        float num2 = ConVar.Server.sprinklerEyeHeightOffset;
                        float num3 = Vector3.Angle(up, Vector3.up) / 180f;
                        num2 *= Mathf.Clamp(num3, 0.2f, 1f);
                        Vector3 startPosition = position + up * (ConVar.Server.sprinklerRadius * 0.5f);
                        Vector3 endPosition = position + up * num2;
                        Vis.Entities<BaseEntity>(startPosition, endPosition, ConVar.Server.sprinklerRadius, list, 1237003025, QueryTriggerInteraction.Collide);
                        if (list.Count > 0)
                        {
                            foreach (BaseEntity baseEntity in list)
                            {
                                if (baseEntity is PlanterBox)
                                {
                                    PlanterBox box = baseEntity as PlanterBox;
                                    if (box != null && purifier.waterStorage.inventory.GetSlot(0) != null && purifier.waterStorage.inventory.GetSlot(0).amount > sprinkler.WaterPerSplash)
                                    {
                                        var collect = Facepunch.Pool.Get<List<Item>>();
                                        purifier.waterStorage.inventory.Take(collect, ItemManager.FindItemDefinition("water").itemid, sprinkler.WaterPerSplash); // Take only calls RemoveFromContainer
                                        foreach (Item item in collect) { item.Remove(); }
                                        Facepunch.Pool.FreeUnmanaged(ref collect);
                                        box.soilSaturation += Mathf.Min(box.availableWaterCapacity, sprinkler.WaterPerSplash);
                                        if (box.children == null) { return; }
                                        foreach (BaseEntity plants in box.children)
                                        {
                                            if (!(plants == null))
                                            {
                                                GrowableEntity growableEntity = plants as GrowableEntity;
                                                if (growableEntity != null) { growableEntity.QueueForQualityUpdate(); }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        Facepunch.Pool.FreeUnmanaged(ref list);
                    }
                }
            }
        }

        private void OnEngineStarted(Tugboat tug)
        {
            if (tug != null)
            {
                foreach(BaseEntity entity in tug.children)
                {
                    if(entity != null && entity is BaseOven)
                    {
                        BaseOven oven = entity as BaseOven;
                        if (Interface.CallHook("OnOvenStart", oven) != null)
                        {
                            return;
                        }
                        oven.inventory.temperature = oven.cookingTemperature;
                        oven.UpdateAttachmentTemperature();
                        oven.InvokeRepeating(new Action(oven.Cook), 0.5f, 0.5f);
                        oven.SetFlag(BaseEntity.Flags.On, true, false, true);
                        Interface.CallHook("OnOvenStarted", oven);
                    }
                }
            }
        }

        //Manually give or remove power from IO entity
        private void Tugboat_TogglePower(List<BaseEntity> baseEntities, bool on, ulong tugid)
        {
            if (on) { if (!storedData.RunningTugs.Contains(tugid)) { storedData.RunningTugs.Add(tugid); } }
            else { storedData.RunningTugs.Remove(tugid); }
            foreach (var be in baseEntities)
            {
                IOEntity io = be as IOEntity;
                if (io != null)
                {
                    if (!on)
                    {
                        io.SetFlag(BaseEntity.Flags.On, false);
                        io.UpdateHasPower(0, 1);
                        io.SendNetworkUpdateImmediate();
                        if (be is AutoTurret) { (be as AutoTurret).InitiateShutdown(); }
                        else if (io is SamSite) { io.SetFlag(BaseEntity.Flags.Reserved8, false); }
                        else if (config.WaterSystem && be is PoweredWaterPurifier) { WaterSystem.Remove(be as PoweredWaterPurifier); }
                        continue;
                    }
                    io.SetFlag(BaseEntity.Flags.On, true);
                    io.UpdateHasPower(100, 1);
                    io.SendNetworkUpdateImmediate();
                    if (be is AutoTurret) { (be as AutoTurret).InitiateStartup(); (be as AutoTurret).authDirty = true; }
                    else if (io is SamSite){io.SetFlag(BaseEntity.Flags.Reserved8, true);}
                    else if (config.WaterSystem && be is PoweredWaterPurifier) { WaterSystem.Add(be as PoweredWaterPurifier); }
                }
            }
        }

        private List<int?> Tugboat_AllowedItems()
        {
            List<int?> list = new List<int?>();
            foreach (string item in config.ItemOverride)
            {
                ItemDefinition itemdef = ItemManager.FindItemDefinition(item);
                if (itemdef != null) { list.Add(itemdef.itemid); }
            }
            return list;
        }

        private void Tugboat_SafeZoneMessage(Tugboat tug, List<BaseEntity> players) { foreach (var p in players) { p.ToPlayer().SendConsoleCommand("gametip.ShowToast", 1, message(p.ToPlayer(), "G6")); } }
        #endregion

        #region Harmony
        [AutoPatch]
        [HarmonyPatch(typeof(Tugboat), "VehicleFixedUpdate")]
        internal class Tugboat_VehicleFixedUpdate
        {
            [HarmonyPostfix]
            static void Postfix(Tugboat __instance)
            {
            List<BaseEntity> players = Facepunch.Pool.Get<List<BaseEntity>>();
            List<BaseEntity> IO = Facepunch.Pool.Get<List<BaseEntity>>();
            List<BaseEntity> guntraps = Facepunch.Pool.Get<List<BaseEntity>>();
            List<BaseEntity> doors = Facepunch.Pool.Get<List<BaseEntity>>();
                try
                {
                    if (__instance != null && !__instance.IsBroken())
                    {
                        //Auto Flipper Function
                        if (_plugin.config.AutoFlipper && __instance.IsFlipped())
                        {
                            __instance.transform.up = __instance.transform.up * -1;
                            __instance.SendNetworkUpdateImmediate();
                        }

                        //Disable Driver Check
                        if (_plugin.config.DisableDriverCheck || __instance.OwnerID == 1234) { __instance.lastHadDriverTime = UnityEngine.Time.time; }

                        //Scan all children of tugboat
                        for (int i = __instance.children.Count - 1; i >= 0; i--)
                        {
                            if (__instance.children[i] is Door) { doors.Add(__instance.children[i]); }
                            else if (__instance.children[i] is GunTrap || __instance.children[i] is FlameTurret) { guntraps.Add(__instance.children[i]); } //Collect Traps
                            else if (__instance.children[i] is IOEntity) { IO.Add(__instance.children[i]); } //Collect IO Ents
                            else if (__instance.children[i] is BasePlayer && (__instance.children[i] as BasePlayer).UserIDString.IsSteamId()) //Check Players
                            {
                                BasePlayer player = __instance.children[i] as BasePlayer;
                                if (player == null || !player.UserIDString.IsSteamId()) { continue; }
                                players.Add(player);
                                if (player.serverInput.WasJustPressed(BUTTON.RELOAD))
                                {
                                    _plugin.Tugboat_TorpedoMod(player, __instance);
                                    continue;
                                }
                                else if (player.serverInput.WasJustPressed(BUTTON.DUCK))
                                {
                                    _plugin.Tugboat_WaterMineMod(player, __instance);
                                    continue;
                                }
                                RaycastHit rhit;
                                if (_plugin.config.OverrideAllow && _plugin.config.AdvancedOverrideAllow)
                                {
                                    var heldEntity = player?.GetActiveItem(); //Get held item
                                    if (_plugin.AllowedItems.Contains(heldEntity?.info?.itemid))
                                    {
                                        if (!Physics.Raycast(player.eyes.HeadRay(), out rhit, 2)) { player.SetPlayerFlag(BasePlayer.PlayerFlags.CreativeMode, false); }
                                        if (rhit.GetEntity() is Tugboat)
                                        {
                                            if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.CreativeMode))
                                            {
                                                if (!player.IsBuildingBlocked())
                                                {
                                                    player.SetPlayerFlag(BasePlayer.PlayerFlags.CreativeMode, true);
                                                }
                                            }
                                        }
                                        else if (player.HasPlayerFlag(BasePlayer.PlayerFlags.CreativeMode))
                                        {
                                            player.SetPlayerFlag(BasePlayer.PlayerFlags.CreativeMode, false);
                                        }
                                    }
                                    else if (player.HasPlayerFlag(BasePlayer.PlayerFlags.CreativeMode))
                                    {
                                        player.SetPlayerFlag(BasePlayer.PlayerFlags.CreativeMode, false);
                                    }
                                }
                                if (!player.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY)) { continue; } //Early Exit point
                                else if (player.isMounted)
                                {
                                    //Horn
                                    _plugin.Tugboat_HornMod(player, __instance);
                                    continue;
                                }
                                else if (_plugin.config.OverrideAllow && !_plugin.config.AdvancedOverrideAllow)
                                {
                                    //Building Override
                                    var heldEntity = player?.GetActiveItem(); //Get held item
                                    if (!_plugin.AllowedItems.Contains(heldEntity?.info?.itemid)) { continue; } //Check is allowed override           
                                    var HeldItem = heldEntity?.GetHeldEntity();
                                    if (HeldItem == null || HeldItem is BaseProjectile || HeldItem is BaseMelee || heldEntity?.info?.itemid == 1525520776 || !player.CanBuild()) { continue; } //Block hammer/guns/melee/No Build

                                    if (!Physics.Raycast(player.eyes.HeadRay(), out rhit, 3)) { continue; }
                                    var entity = rhit.GetEntity();
                                    if (entity == null || rhit.distance > 4f || rhit.distance < 1.2f || entity.prefabID != 268742921) { continue; } //268742921 = tugboat
                                    if (heldEntity?.info?.itemid == 634478325 || heldEntity?.info?.itemid == 140006625 || heldEntity?.info?.itemid == -1009359066) //cctv.camera || ptz.cctv.camera || Samsite
                                    {
                                        //Camera Allow Anywhere
                                    }
                                    else if (heldEntity?.info?.itemid == 1142993169) { if (rhit.point.y < player.eyes.position.y) { continue; } } //Ceiling light has to be above
                                    else if (rhit.point.y > player.eyes.position.y) { continue; }//Checks playing below players eyes
                                    Vector3 pos = rhit.point;
                                    if (pos.y > player.GetParentEntity().transform.position.y + 8.621) { pos.y = player.GetParentEntity().transform.position.y + 8.621f; }
                                    if (heldEntity?.info?.itemid == -1009359066 && (rhit.point.y - entity.transform.position.y) > 10) { pos.y += 2.2f; } //Samsite Top Anttenna Offset
                                    _plugin.Tugboat_PlacementMod(player, __instance, heldEntity, entity, pos);
                                }
                            }
                        }
                        //IO Power Function
                        if (_plugin.config.PowerFromEngine)
                        {
                            if (__instance.HasFlag(Tugboat.Flags.On) && !_plugin.storedData.RunningTugs.Contains(__instance.net.ID.Value))
                            {
                                _plugin.Tugboat_TogglePower(IO, true, __instance.net.ID.Value);
                                _plugin.storedData.RunningTugs.Add(__instance.net.ID.Value);
                            }
                            else if (!__instance.HasFlag(Tugboat.Flags.On) && _plugin.storedData.RunningTugs.Contains(__instance.net.ID.Value))
                            {
                                _plugin.Tugboat_TogglePower(IO, false, __instance.net.ID.Value);
                                _plugin.storedData.RunningTugs.Remove(__instance.net.ID.Value);
                            }
                        }
                       
                        //Trap Control
                        if (_plugin.config.AuthFromBoat)
                        {
                            foreach (var gt in guntraps)
                            {
                                if (gt.HasFlag(BaseEntity.Flags.Reserved5)) { continue; }
                                gt.Invoke(() => { gt.SetFlag(BaseEntity.Flags.Reserved5, false); }, 0.65f);
                                gt.SetFlag(BaseEntity.Flags.Reserved5, true);
                                foreach (var bp in players)
                                {
                                    BasePlayer p = bp as BasePlayer;
                                    if (!p.IsSleeping() && !p.IsDead() && !__instance.IsAuthed(p))
                                    {
                                        if (Vector3.Distance(p.transform.position, gt.transform.position + (gt.transform.forward * 3f)) < 2.1f)
                                        {
                                            GunTrap t = gt as GunTrap;
                                            if (t != null && t.UseAmmo())
                                            {
                                                Effect.server.Run(t.gun_fire_effect.resourcePath, t, StringPool.Get(t.muzzlePos.gameObject.name), Vector3.zero, Vector3.zero);
                                                for (int i = 0; i < t.numPellets; i++) { t.FireBullet(); };
                                                break;
                                            }
                                            FlameTurret f = gt as FlameTurret;
                                            if (f != null)
                                            {
                                                if (!f.IsTriggered() && f.HasFuel())
                                                {
                                                    f.SetTriggered(true);
                                                    Effect.server.Run(f.triggeredEffect.resourcePath, f.transform.position, Vector3.up);
                                                    f.DoFlame(0.5f);
                                                    f.SendNetworkUpdateImmediate(false);
                                                    f.Invoke(() => { f.SetTriggered(false); }, 5);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        //Safe Zone Idle Damage
                        if (_plugin.config.SafeZoneDamage > 0)
                        {
                            if (!_plugin.DamageTimer.ContainsKey(__instance.net.ID.Value)) { _plugin.DamageTimer.Add(__instance.net.ID.Value, 0); }
                            if (_plugin.DamageTimer[__instance.net.ID.Value] < UnityEngine.Time.realtimeSinceStartup)
                            {
                                _plugin.DamageTimer[__instance.net.ID.Value] = UnityEngine.Time.realtimeSinceStartup + 300;
                                bool Safezone = false;
                                foreach (TriggerSafeZone safezone in TriggerSafeZone.allSafeZones)
                                {
                                    if (Vector3.Distance(safezone.transform.position, __instance.transform.position) < 60)
                                    {
                                        Safezone = true;
                                        break;
                                    }
                                }
                                if (Safezone && __instance.HasFlag(Tugboat.Flags.On))
                                {
                                    for (int i = doors.Count - 1; i >= 0; i--)
                                    {
                                        (doors[i] as Door).health -= _plugin.config.SafeZoneDamage;
                                        doors[i].SendNetworkUpdate();
                                        if ((doors[i] as Door).health <= 0) { doors[i].Kill(); }
                                    }
                                    if (players != null) {
                                        _plugin.Tugboat_SafeZoneMessage(__instance, players); }
                                }
                            }
                        }
                    }
                }
                catch { }
                Facepunch.Pool.FreeUnmanaged(ref players);
                Facepunch.Pool.FreeUnmanaged(ref IO);
                Facepunch.Pool.FreeUnmanaged(ref guntraps);
                Facepunch.Pool.FreeUnmanaged(ref doors);
            }
        }

        //Stops infinate flame turrets on tugboats
        [AutoPatch]
        [HarmonyPatch(typeof(FlameTurret), "ServerThink")]
        internal class FlameTurret_ServerThink
        {
            [HarmonyPrefix]
            static bool Prefix(FlameTurret __instance, ref float ___lastServerThink)
            {
                try { if (__instance?.GetParentEntity() is Tugboat) { ___lastServerThink = UnityEngine.Time.realtimeSinceStartup; return false; } }
                catch { }
                return true;
            }
        }

        //Stops Guntraps on tugboats
        [AutoPatch]
        [HarmonyPatch(typeof(GunTrap), "FireWeapon")]
        internal class GunTrap_FireWeapon
        {
            [HarmonyPrefix]
            static bool Prefix(GunTrap __instance)
            {
                try { if (__instance?.GetParentEntity() is Tugboat) { return false; } }
                catch { }
                return true;
            }
        }

        //Shut Down Turrets When Tug Sinks
        [AutoPatch]
        [HarmonyPatch(typeof(Tugboat), "EnterCorpseState")]
        internal class Tugboat_EnterCorpseState
        {
            [HarmonyPostfix]
            static void Postfix(MotorRowboat __instance)
            {
                try
                {
                    for (int i = __instance.children.Count - 1; i >= 0; i--)
                    {
                        if (__instance.children[i] is AutoTurret)
                        {
                            AutoTurret at = __instance.children[i] as AutoTurret;
                            if (at != null)
                            {
                                at.SetFlag(BaseEntity.Flags.On, false);
                                at.SetFlag(BaseEntity.Flags.Reserved8, false);
                                at.UpdateHasPower(0, 1);
                                at.InitiateShutdown();
                                at.SetTarget(null);
                                at.SendNetworkUpdateImmediate();
                            }
                        }
                    }
                }
                catch { }
            }
        }

        //Stops kinematic spam
        [AutoPatch]
        [HarmonyPatch(typeof(BaseBoat), "CheckDriftToShore")]
        internal class BaseBoat_CheckDriftToShore
        {
            [HarmonyPrefix]
            static bool Prefix(BaseBoat __instance)
            {
                try { if (__instance?.rigidBody != null && __instance.rigidBody.isKinematic) { return false; } }
                catch { }
                return true;
            }
        }

        //Stops Null trigger spam on server restarts when latched heli
        [AutoPatch]
        [HarmonyPatch(typeof(TriggerBase), "OnObjectRemoved", typeof(GameObject))]
        internal class TriggerBase_OnObjectRemoved
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = instructions.ToList<CodeInstruction>();
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].opcode == OpCodes.Ldstr && list[i].operand as string == "Trigger ")
                    {
                        list[i].opcode = OpCodes.Nop;
                        list[i + 1].opcode = OpCodes.Nop;
                        list[i + 2].opcode = OpCodes.Nop;
                        list[i + 3].opcode = OpCodes.Nop;
                        list[i + 4].opcode = OpCodes.Nop;
                        list[i + 5].opcode = OpCodes.Nop;
                        list[i + 6].opcode = OpCodes.Nop;
                        break;
                    }
                }
                return list;
            }
        }

        //Vendor control
        [AutoPatch]
        [HarmonyPatch(typeof(NPCTalking), "Server_BeginTalking", typeof(BasePlayer))]
        internal class NPCTalking_Server_BeginTalking
        {
            [HarmonyPrefix]
            static bool Prefix(BasePlayer ply, NPCTalking __instance)
            {
                try
                {
                    if (_plugin.TugDealers.ContainsKey(__instance))
                    {
                        List<Tugboat> list = Pool.Get<List<Tugboat>>();
                        Vis.Entities<Tugboat>(__instance.transform.position, _plugin.config.SpawnBlockRadius, list);
                        bool TugBlocked = false;
                        if (list.Count > 0) { TugBlocked = true; }
                        if (!_plugin.permission.UserHasPermission(ply.UserIDString, "TugMe.Buy"))
                        {
                            _plugin.MessageIcon(ply, "C3");
                            Pool.FreeUnmanaged(ref list);
                            return false;
                        }
                        if (TugBlocked)
                        {
                            _plugin.MessageIcon(ply, "E6", _plugin.config.SpawnBlockRadius.ToString());
                            Pool.FreeUnmanaged(ref list);
                            return false;
                        }
                        if (!_plugin.SpawnManager(ply))
                        {
                            _plugin.MessageIcon(ply, "C4", _plugin.config.MaxPerPlayer.ToString());
                            Pool.FreeUnmanaged(ref list);
                            return false;
                        }
                        _plugin.RustUI(ply); //Create CUI
                        Pool.FreeUnmanaged(ref list);
                        return false;
                    }
                }
                catch { }
                return true;
            }
        }

        //Fix electric oven not turning on
        [AutoPatch]
        [HarmonyPatch(typeof(ElectricOven), "get_CanRunWithNoFuel")]
        internal class ElectricOven_CanRunWithNoFuel
        {
            [HarmonyPrefix]
            static bool Prefix(ElectricOven __instance, ref bool __result)
            {
                try
                {
                    if (__instance?.GetParentEntity() is Tugboat)
                    {
                        if (_plugin.storedData.RunningTugs.Contains(__instance.GetParentEntity().net.ID.Value))
                        {
                            __result = true;
                            return false;
                        }
                    }
                }
                catch { }
                return true;
            }
        }

        //Control Tugboat NPCs attacking back if sniped from distance
        [AutoPatch]
        [HarmonyPatch(typeof(ScientistNPC), "OnAttacked", typeof(HitInfo))]
        internal class BaseCombatEntity_Hurt
        {
            [HarmonyPostfix]
            static void Postfix(HitInfo info, ScientistNPC __instance)
            {
                try
                {
                    if (info?.InitiatorPlayer == null) { return; }
                    if (__instance?.net?.ID != null && __instance.GetParentEntity() is Tugboat)
                    {
                        ScientistNPC npc = __instance;
                        if (npc?.Brain != null)
                        {
                            if (npc.Brain.AttackRangeMultiplier != 100)
                            {
                                npc.Brain.Senses.Memory.Targets.Clear();
                                npc.Brain.Senses.Memory.Targets.Add(info.InitiatorPlayer);
                                float oldTLR = npc.Brain.TargetLostRange;
                                float oldARM = npc.Brain.AttackRangeMultiplier;
                                float oldAIM = npc.aimConeScale;
                                npc.aimConeScale = 0.2f;
                                npc.Brain.TargetLostRange = 800;
                                npc.Brain.AttackRangeMultiplier = 100;
                                npc.Invoke(() => //Return normal settings
                                {
                                    if (npc != null)
                                    {
                                        npc.aimConeScale = oldAIM;
                                        npc.Brain.TargetLostRange = oldTLR;
                                        npc.Brain.AttackRangeMultiplier = oldARM;
                                        npc.Brain.Senses.Memory.Targets.Clear();
                                    }
                                }, 4f);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        //allows horses on tugboats
        [AutoPatch]
        [HarmonyPatch(typeof(BaseRidableAnimal), "UpdateOnIdealTerrain")]
        internal class BaseRidableAnimal_UpdateOnIdealTerrain
        {
            public static Dictionary<ulong, float> HorseChecker = new Dictionary<ulong, float>();
            [HarmonyPostfix]
            static void Postfix(BaseRidableAnimal __instance)
            {
                try
                {

                    if (_plugin.config.HorseTug)
                    {
                        if (!HorseChecker.ContainsKey(__instance.net.ID.Value)) { HorseChecker.Add(__instance.net.ID.Value, 0); }
                        if (UnityEngine.Time.time > HorseChecker[__instance.net.ID.Value])
                        {
                            HorseChecker[__instance.net.ID.Value] = UnityEngine.Time.time + 3;
                            bool hastug = (__instance?.GetParentEntity() is Tugboat);
                            RaycastHit rhit;
                            if (!Physics.Raycast(__instance.transform.position, Vector3.down, out rhit, 3f)) { if (hastug) { __instance.SetParent(null, true, true); } return; }
                            var entity = rhit.GetEntity();
                            if (entity != null && entity is Tugboat)
                            {
                                __instance.SetParent(entity, true, true);
                                return;
                            }
                            if (hastug) { __instance.SetParent(null, true, true); }
                        }
                    }
                }
                catch { }
            }
        }
        #endregion

        #region Behaviour
        //Control latching mini to tugboat
        public class MiniLatch : MonoBehaviour
        {
            public BaseVehicle mini = null;
            public Tugboat boat = null;
            public bool Latched = false;
            public bool landed = false;

            void Awake() { mini = GetComponent<BaseVehicle>(); }

            public void UnLatch(float delay)
            {
                landed = false;
                _plugin.timer.Once(delay, () =>
                {
                    if (mini != null && mini.HasDriver() && Latched)
                    {
                        landed = false;
                        if (_plugin.storedData.LastMiniDriver.ContainsKey(mini.net.ID.Value) && _plugin.permission.UserHasPermission(mini.GetDriver().UserIDString, "TugMe.Latch"))
                        {
                            BasePlayer player = BasePlayer.FindByID(_plugin.storedData.LastMiniDriver[mini.net.ID.Value]);
                            if (_plugin.config.LatchAuth)
                            {
                                if (IsAuthed(_plugin.storedData.LastMiniDriver[mini.net.ID.Value]))
                                {
                                    Unlock();
                                    if (player != null) { _plugin.MessageIcon(player, "D9"); }
                                    return;
                                }
                                if (player != null) { _plugin.MessageIcon(player, "F3"); }
                                return;
                            }
                            Unlock();
                            _plugin.MessageIcon(player, "D9");
                        }
                    }
                });
            }

            void Unlock()
            {
                mini.SetParent(null, true, true);
                if (_plugin.config.MoveBackHeli) { mini.transform.position = mini.transform.position + new Vector3(0, 3f, 0); }
                else { mini.transform.position = mini.transform.position + new Vector3(0, 0.5f, 0); }
                mini.rigidBody.SetActive(true);
                if (_plugin.config.LockNoDecay) { mini.InvokeRandomized(new Action((mini as PlayerHelicopter).DecayTick), UnityEngine.Random.Range(30f, 60f), 60f, 6f); }
                Latched = false;
            }

            public void Lock()
            {
                Quaternion q = mini.transform.rotation;
                q.x = boat.transform.rotation.x;
                q.z = boat.transform.rotation.z;
                mini.rigidBody.SetActive(false);
                if (_plugin.config.MoveBackHeli)
                {
                    q.y = boat.transform.rotation.y;
                    mini.transform.rotation = q;
                    mini.SetParent(boat, false, false);
                    mini.transform.position = boat.transform.position + (boat.transform.up * 3) + (boat.transform.forward * -12);
                    mini.transform.LookAt(boat.transform.position, Vector3.up);
                }
                else
                {
                    mini.transform.rotation = q;
                    mini.SetParent(boat, true, true);

                }
                if (_plugin.config.LockNoDecay) { mini.CancelInvoke((mini as PlayerHelicopter).DecayTick); }
                mini.SendNetworkUpdateImmediate();
                Latched = true;
            }

            bool IsAuthed(ulong player)
            {
                foreach (BaseEntity baseEntity in boat.children)
                {
                    VehiclePrivilege vehiclePrivilege = baseEntity as VehiclePrivilege;
                    if (vehiclePrivilege != null) { return vehiclePrivilege.IsAuthed(player); }
                }
                return true;
            }

            void OnCollisionStay(Collision collision)
            {
                if (collision == null || Latched || landed) { return; }
                Tugboat tb = collision?.gameObject?.GetComponent<Tugboat>();
                if (tb != null && !mini.HasDriver() && !mini.HasParent())
                {
                    if (_plugin.storedData.LastMiniDriver.ContainsKey(mini.net.ID.Value) && _plugin.permission.UserHasPermission(_plugin.storedData.LastMiniDriver[mini.net.ID.Value].ToString(), "TugMe.Latch"))
                    {
                        boat = tb;
                        BasePlayer player = BasePlayer.FindByID(_plugin.storedData.LastMiniDriver[mini.net.ID.Value]);
                        if (_plugin.config.LatchAuth)
                        {
                            if (IsAuthed(_plugin.storedData.LastMiniDriver[mini.net.ID.Value]))
                            {
                                landed = true;
                                Lock();
                                if (player != null) { _plugin.MessageIcon(player, "D8"); }
                                return;
                            }
                            landed = true;
                            if (player != null) { _plugin.MessageIcon(player, "F3"); }
                            return;
                        }
                        landed = true;
                        Lock();
                        _plugin.MessageIcon(player, "D8");
                    }
                }
            }

            public void RemoveMe()
            {
                if (Latched && _plugin.config.UnlockUnload) { Unlock(); }
                Destroy(this);
            }
        }

        //Control tugboat to follow cargopath
        public class AutoNavigation : MonoBehaviour
        {
            private Tugboat boat;
            public BasePlayer captin = null;
            private float delay = 0;
            private int targetNodeIndex = -1;
            private bool HalfTick = false;
            void Awake()
            {
                boat = GetComponent<Tugboat>();
                if (boat == null) { Destroy(this); } //No player so remove
                delay = UnityEngine.Time.time + _plugin.config.AutoNavDelay; //start delay
            }

            public void RemoveMe() => Destroy(this); //Remove componant function

            void FixedUpdate()
            {
                if (delay > UnityEngine.Time.time) { return; } //Cancel since not waited long enough
                if (boat == null || boat.IsDestroyed || (!_plugin.storedData.RunningTugs.Contains(boat.net.ID.Value) && !_plugin.EventBoats.Contains(boat))) { Destroy(this); return; }
                delay = UnityEngine.Time.time + _plugin.config.AutoNavTick; //Set up next adjustment
                if (TerrainMeta.Path.OceanPatrolFar == null || TerrainMeta.Path.OceanPatrolFar.Count == 0 || targetNodeIndex == -2) { return; } //Do nothing
                else if (targetNodeIndex == -1) { targetNodeIndex = _plugin.GetClosestNodeToUs(base.transform.position); } //Get a node
                if (_plugin.config.JunkPileKill) //Kill junkpiles if colliding with them
                {
                    List<JunkPileWater> list = Pool.Get<List<JunkPileWater>>(); //Pooling to save list memory
                    Vis.Entities<JunkPileWater>(boat.transform.position, _plugin.config.JunkPileScan, list);
                    foreach (JunkPileWater pile in list)
                    {
                        pile.SpawnGroupsEmpty();
                        pile.SinkAndDestroy();
                    }
                    Pool.FreeUnmanaged(ref list); //Free pooling
                }
                if (!HalfTick && _plugin.EventBoats.Contains(boat)) //Event boat force throttle on.
                {
                    HalfTick = true;
                    boat.SetFlag(Tugboat.Flags.On, true);
                    if (_plugin.config.NavPushBack)
                    {
                        //Check Infront
                        if (TerrainMeta.HeightMap.GetHeight(boat.transform.position + boat.transform.forward * 30) > -3f) { boat.rigidBody.AddForce(boat.transform.forward * 20f * -400000, ForceMode.Force); }
                        //Check Right Side
                        if (TerrainMeta.HeightMap.GetHeight(boat.transform.position + boat.transform.right * 25) > -3f) { boat.rigidBody.AddForce(boat.transform.right * 20f * -400000, ForceMode.Force); }
                        //Check Left Side
                        if (TerrainMeta.HeightMap.GetHeight(boat.transform.position + boat.transform.right * -25) > -3f) { boat.rigidBody.AddForce(boat.transform.right * 20f * 400000, ForceMode.Force); }
                    }
                    if (captin == null)
                    {
                        bool throttle = false;
                        //Check for NPCs
                        List<ScientistNPC> list = Pool.Get<List<ScientistNPC>>();
                        Vis.Entities<ScientistNPC>(boat.transform.position, 15, list);
                        foreach (ScientistNPC npc in list) //Check vis list
                        {
                            if (_plugin.ActiveNPCs.Contains(npc.net.ID.Value))
                            {
                                throttle = true;
                                break;
                            }
                        }
                        Pool.FreeUnmanaged(ref list);
                        if (throttle) { boat.gasPedal = 1; }
                        else { boat.gasPedal = 0; }
                    }
                }
                else
                {
                    HalfTick = false;
                }
                Vector3 vector = TerrainMeta.Path.OceanPatrolFar[targetNodeIndex]; //Get position from cargopath
                if (vector.x > 3990) { vector.x = 3990; }
                if (vector.x < -3990) { vector.x = -3990; }
                if (vector.z > 3990) { vector.z = 3990; }
                if (vector.z < -3990) { vector.z = -3990; }
                Vector3 normalized = (vector - base.transform.position).normalized; //Get direction
                boat.steering = Vector3.Dot(base.transform.right, normalized) * -1; //Adjust to that direction
                if (Vector3.Distance(base.transform.position, vector) < _plugin.config.NodeDistance) //Goto next node
                {
                    targetNodeIndex--;
                    if (targetNodeIndex <= 0) { targetNodeIndex = TerrainMeta.Path.OceanPatrolFar.Count - 1; }
                }
            }
            void OnDestroy() { try { if (captin != null && !captin.IsSleeping() && captin.IsAlive()) { _plugin.MessageIcon(captin, "B3"); } } catch { } }
        }
        #endregion
    }
}