using System;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Configuration;
using System.Reflection;
using Oxide.Core;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Random = UnityEngine.Random;
using System.Globalization;
using ProtoBuf;
using Oxide.Game.Rust;

namespace Oxide.Plugins
{
    [Info("Base Call", "Razor", "1.0.12", ResourceId = 35)]
    [Description("Plane drops copy paste")]
    public class BaseCall : RustPlugin
    {
        [PluginReference] private Plugin CopyPaste;
        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;
        public List<MonumentInfo> monuments = new List<MonumentInfo>();
        private const int LAND_LAYERS = 1 << 4 | 1 << 8 | 1 << 16 | 1 << 21 | 1 << 23;
        private const int WORLD_LAYER = (1 << 0 | 1 << 16) | ~(1 << 18 | 1 << 28 | 1 << 29);
        private const string CargoPlanePrefab = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        private string baseFile;
        private static BaseCall ins;
        public long coolTimer;
        public int limit;
        public bool Changed;
        static System.Random random = new System.Random();
        public Dictionary<Vector3, ulong> PlayerBase = new Dictionary<Vector3, ulong>();
        public Dictionary<ulong, List<BaseEntity>> PlayerEntitys = new Dictionary<ulong, List<BaseEntity>>();
        public Dictionary<ulong, dropInfo> PlayerDrop = new Dictionary<ulong, dropInfo>();

        public class dropInfo
        {
            public string name;
            public ulong skinid;
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BlockedBuild"] = "<color=#ce422b>You can not do this here!</color>",
                ["NoPerm"] = "<color=#ce422b>You lack the permishions to use this command!</color>",
                ["baseIncoming"] = "<color=#ce422b>Your base is on the way.</color>",
                ["Cooldown"] = "<color=#ce422b>You have another {0} seconds remaining to do that again!</color>",
                ["gave"] = "<color=#ce422b>You have just got one base drop!</color>",
                ["drop"] = "<color=#ce422b>You have no room so your basedrop got droped on the ground!</color>",
                ["usagecommand"] = "<color=#ce422b>/getsrop <ItemName></color>",
                ["noDrop"] = "<color=#ce422b>Could not find ItemName {0}</color>",
                ["NoUndo"] = "<color=#ce422b>You can no longer undo this drop!</color>",
                ["Undo"] = "<color=#ce422b>You can undo this drop with /redo there may be a cooldown to toss a new drop.</color>",
                ["redo"] = "<color=#ce422b>This drop will now be removed.</color>",
                ["noredo"] = "<color=#ce422b>No drop or redo time has expired.</color>",
            }, this);
        }

        #region Config 

        private ConfigData configData;
        class ConfigData
        {

            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; }

            public class Settings
            {
                public string PermissionUse { get; set; }
                public float planeSpeed { get; set; }
                public float dropSpeed { get; set; }
                public int cooldownToss { get; set; }
                public int cooldownGet { get; set; }
                public Dictionary<ulong, dropSettings> DropItems { get; set; }
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
                    PermissionUse = "basecall.use",
                    planeSpeed = 6f,
                    dropSpeed = 60f,
                    cooldownToss = 86400,
                    cooldownGet = 86400,
                    DropItems = new Dictionary<ulong, dropSettings>()
                },

                Version = Version
            };
        }

        public class dropSettings
        {
            public string ItemName = "BaseDrop";
            public string pasteFile;
            public List<string> CopyPasteParameters;
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 0, 1))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        void Init()
        {
            RegisterPermissions();
            ins = this;
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/Player");
            LoadData();
        }

        private void OnServerInitialized()
        {
            if (configData.settings.DropItems.Count <= 0)
            {
                configData.settings.DropItems.Add(2131827661, new dropSettings());
                configData.settings.DropItems[2131827661].pasteFile = "None";
                configData.settings.DropItems[2131827661].CopyPasteParameters = new List<string>() { "stability", "false" };
                SaveConfig();
            }

            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (!monuments.Contains(monument))
                    monuments.Add(monument);
            }
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(configData.settings.PermissionUse, this);
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.GetMod().DataFileSystem.ReadObject<PlayerEntity>(Name + "/Player");
            }
            catch
            {
                PrintWarning("Couldn't load Player data, creating new Playerfile");
                pcdData = new PlayerEntity();
            }

        }
        class PlayerEntity
        {
            public Dictionary<ulong, PCDInfo> pEntity = new Dictionary<ulong, PCDInfo>();


            public PlayerEntity() { }
        }
        class PCDInfo
        {
            public int limit;
            public long cooldown;
            public long cooldownGet;
        }
        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        #region Classes

        //private void OnExplosiveDropped(BasePlayer player, SurveyCharge entity, ThrownWeapon item) => OnExplosiveThrown(player, entity, item);
        private void OnExplosiveThrown(BasePlayer player, SurveyCharge entity, ThrownWeapon item)
        {
            if (entity == null || player == null || item == null) return;

            if (entity.name.Contains("surveycharge") && configData.settings.DropItems.ContainsKey(item.skinID))
            {
                ulong itemSkin = item.skinID;
                string name = item.name;
                double time = GrabCurrentTime();
                if (!pcdData.pEntity.ContainsKey(player.userID))
                {
                    pcdData.pEntity.Add(player.userID, new PCDInfo());
                    pcdData.pEntity[player.userID].cooldown = (long)time;
                }

                else if (pcdData.pEntity[player.userID].cooldown + configData.settings.cooldownToss >= (long)time)
                {
                    int left = Convert.ToInt32(pcdData.pEntity[player.userID].cooldown + configData.settings.cooldownToss - time);
                    SendReply(player, lang.GetMessage("Cooldown", this, player.UserIDString), left);
                    GetBaseItem(player, false, itemSkin, name);
                    entity.Kill();
                    return;
                }

                if (!player.CanBuild() || isOnRT(player.transform.position))
                {
                    SendReply(player, lang.GetMessage("BlockedBuild", this));
                    GetBaseItem(player, false, itemSkin, name);
                    entity.Kill();
                    return;
                }

                if (!player.IsOutside())
                {
                    SendReply(player, lang.GetMessage("BlockedBuild", this));
                    GetBaseItem(player, false, itemSkin, name);
                    entity.Kill();
                    return;
                }
                timer.Once(1.0f, () =>
                {
                    if (CheckRoad(entity.transform.position) || IsplayerAtMonument(player))
                    {
                        SendReply(player, lang.GetMessage("BlockedBuild", this));
                        GetBaseItem(player, false, itemSkin, name);
						pcdData.pEntity[player.userID].cooldown = 0;
                        entity.Kill();
                        return;
                    }

                    if (WaterLevel.Factor(entity.WorldSpaceBounds().ToBounds(), false, false) > 0.7f)
                    {
                        SendReply(player, lang.GetMessage("BlockedBuild", this));
                        GetBaseItem(player, false, itemSkin, name);
						pcdData.pEntity[player.userID].cooldown = 0;
                        entity.Kill();
                        return;
                    }
                    baseFile = configData.settings.DropItems[item.skinID].pasteFile;
                    SendReply(player, lang.GetMessage("baseIncoming", this, player.UserIDString));
                    CallDrop(entity.transform.position, baseFile, player.userID, item.skinID);
                    if (!PlayerBase.ContainsKey(entity.transform.position))
                        PlayerBase.Add(entity.transform.position, player.userID);
                    else PlayerBase[entity.transform.position] = player.userID;
                    pcdData.pEntity[player.userID].cooldown = (long)time;
                    SaveData();
                    if (PlayerDrop.ContainsKey(player.userID))
                        PlayerDrop.Remove(player.userID);
                    PlayerDrop.Add(player.userID, new dropInfo() { name = name, skinid = itemSkin });
                });
            }
        }


        private bool isOnRT(Vector3 position)
        {
            var allColliders = Physics.OverlapSphere(position, 10);
            foreach (Collider collider in allColliders)
            {
                if (collider.name.Contains("prevent_building") || collider.name.Contains("rock_"))
                    return true;
            }
            return false;
        }

        private bool CheckRoad(Vector3 Position)
        {
            RaycastHit[] hitInfo = Physics.SphereCastAll(Position, 20f, Vector3.down, 66f, LayerMask.GetMask("Terrain", "World", "Construction", "Water"), QueryTriggerInteraction.Ignore);
            if (hitInfo == null) return false;

            foreach (var info in hitInfo)
                if (info.collider.name.ToLower().Contains("road") || info.collider.name.ToLower().Contains("rail") || info.collider.name.ToLower().Contains("powerline")) return true;
            return false;
        }

        private bool IsplayerAtMonument(BasePlayer player)
        {
            List<BuildingBlock> nearby = new List<BuildingBlock>();
            Vis.Entities<BuildingBlock>(player.transform.position, 30, nearby);
            if (nearby.Count > 0) return true;

            foreach (var monument in monuments.ToList())
            {
                if (monument.IsInBounds(player.transform.position))
                {
                    return true;
                }
                if (Vector3.Distance(monument.transform.position, player.transform.position) <= monument.Bounds.size.x || Vector3.Distance(monument.transform.position, player.transform.position) <= 40f)
                {
                    return true;
                }
            }

            return false;
        }

        public bool scanBeforBuild(Vector3 location, BasePlayer player = null)
        {
            List<SleepingBag> nearby = new List<SleepingBag>();
            Vis.Entities<SleepingBag>(location, 15, nearby);
            if (nearby.Count > 0)
            {
                foreach (SleepingBag bag in nearby)
                {
                    if (bag != null) bag.Kill();
                }
                return true;
            }
            return false;
        }

        private bool SpawnBaseBuilding(ulong playerIDs, Vector3 vect, ulong skinID)
        {
            if (skinID == 0)
            {
                PrintWarning($"Building failed skin id in config not found {skinID.ToString()}?");
                return false;
            }
            scanBeforBuild(vect);

            object success = CopyPaste.Call("TryPasteFromVector3", (Vector3)vect, (float)0, configData.settings.DropItems[skinID].pasteFile, configData.settings.DropItems[skinID].CopyPasteParameters.ToArray());

            if (success is string)
            {
                PrintWarning("Building failed copy/paste file not found?");

                return false;
            }
            return true;
        }

        class PlaneBehavior : BaseHookBehavior
        {
            private CargoPlane Plane { get; set; }
            public Vector3 dropLocation { get; set; }
            public ulong skinID { get; set; }


            private void Awake()
            {
                enabled = false;
                Plane = GetComponent<CargoPlane>();

                ins.timer.Once(3, () =>
                {
                    Plane.secondsToTake = Plane.secondsToTake / ins.configData.settings.planeSpeed;
                });
            }
        }


        class BaseDropBehavior : MonoBehaviour
        {
            private BaseEntity thePackage { get; set; }
            public ulong player { get; set; }
            private bool landed;
            public ulong skinID;
            public Vector3 dropLocation { get; set; }

            private void Awake()
            {
                thePackage = GetComponent<BaseEntity>();
                if (thePackage == null)
                {
                    Destroy(this);
                    return;
                }
            }

            private void OnCollisionEnter(Collision col)
            {
                if (landed) return;
                landed = true;
                RunEffect("assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab", null, dropLocation);
                RunEffect("assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab", null, dropLocation);
                thePackage?.Kill();
                ins.SpawnBaseBuilding(player, dropLocation, skinID);
                Destroy(this);
            }

            private void Update()
            {
                if (thePackage.transform.position.y <= dropLocation.y + 1f)
                {
                    if (landed) return;
                    landed = true;
                    RunEffect("assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab", null, dropLocation);
                    RunEffect("assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab", null, dropLocation);
                    thePackage?.Kill();
                    ins.SpawnBaseBuilding(player, dropLocation, skinID);
                    Destroy(this);
                }

            }

            private static void RunEffect(string name, BaseEntity entity = null, Vector3 position = new Vector3(), Vector3 offset = new Vector3())
            {
                if (entity != null)
                    Effect.server.Run(name, entity, 0, offset, position, null, true);
                else Effect.server.Run(name, position, Vector3.up, null, true);
            }
        }

        class DropBehavior : BaseHookBehavior
        {
            private SupplyDrop Drop { get; set; }
            private bool Landed { get; set; }

            private void Awake()
            {
                enabled = false;
                Drop = GetComponent<SupplyDrop>();
            }
        }

        class BaseHookBehavior : FacepunchBehaviour
        {
            public ulong SupplySignalPlayer { get; set; }
            public string baseFilestring { get; set; }
            public ulong skinID { get; set; }
            public bool WasSupplySignaled => SupplySignalPlayer != 0;
        }
        #endregion

        private ulong GetSupplySignalPlayer(SupplyDrop drop)
        {
            return drop?.GetComponent<DropBehavior>()?.SupplySignalPlayer ?? 0u;
        }

        public List<SupplyDrop> drops = new List<SupplyDrop>();

        private void OnEntitySpawned(SupplyDrop drop)
        {
            CargoPlane dropPlane = BaseNetworkable.serverEntities
                .OfType<CargoPlane>()
                .OrderBy(cp => Vector3.Distance(drop.transform.position, cp.transform.position))
                .FirstOrDefault();

            if (dropPlane == null)
            {
                return;
            }

            PlaneBehavior planeBehavior = dropPlane.GetComponent<PlaneBehavior>();
            if (planeBehavior == null)
            {
                return;
            }
            if (planeBehavior.SupplySignalPlayer == 0)
            {
                return;
            }
            if (planeBehavior.baseFilestring == null)
            {
                return;
            }

            var baseFilestring = planeBehavior.baseFilestring;
            ulong skinID = planeBehavior.skinID;
            DropBehavior dropBehavior = drop.gameObject.AddComponent<DropBehavior>();
            if (dropBehavior == null)
            {
                return;
            }

            if (!drops.Contains(drop))
                drops.Add(drop);
			drop.name = "Base_Drop";
            dropBehavior.SupplySignalPlayer = planeBehavior.SupplySignalPlayer;

            float wind = random.Next(0, 25) / 10f;
            float fall = random.Next(40, 80) / 10f;
            var rb = drop.GetComponent<Rigidbody>();
            //rb.isKinematic = false;
            var fwd = new Vector3(1.7363f, 5.4749f, 1.2958f);
            rb.useGravity = false;
            rb.drag = 0f;
            fwd = rb.transform.forward;
            rb.velocity = new Vector3(fwd.x * 0, 0, fwd.z * 0) - new Vector3(0, configData.settings.dropSpeed, 0);
            var col = drop.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(10, 10, 10);
            BaseDropBehavior BaseDropBehavior = drop.gameObject.AddComponent<BaseDropBehavior>();
            BaseDropBehavior.dropLocation = planeBehavior.dropLocation;
            BaseDropBehavior.player = planeBehavior.SupplySignalPlayer;
            BaseDropBehavior.skinID = skinID;
            timer.Once(5f, () => { if (drop != null && drops.Contains(drop)) drops.Remove(drop); });
        }

        object OnBotReSpawnAirdrop(SupplyDrop drop)
        {
            if (drops.Contains(drop))
			{
                return true;
			}

            return null;
        }

        private void CallDrop(Vector3 dropLocation, string baseFile, ulong playerId = 0, ulong skinID = 2131827661)
        {
            CargoPlane plane = GameManager.server.CreateEntity(CargoPlanePrefab) as CargoPlane;
            if (!plane)
            {
                return;
            }

            PlaneBehavior planeBehavior = plane.gameObject.AddComponent<PlaneBehavior>();
            planeBehavior.SupplySignalPlayer = playerId;
            planeBehavior.baseFilestring = baseFile;
            planeBehavior.dropLocation = dropLocation;
            planeBehavior.skinID = skinID;
            plane.InitDropPosition(dropLocation);
            plane.name = "BaseCallPlane";
            plane.Spawn();
        }

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        [ConsoleCommand("givedrop")]
        private void CmdConsolePage(ConsoleSystem.Arg args)
        {

            if (args == null || args.Args.Length < 2) { SendReply(args, lang.GetMessage("givedrop <ItemName> <steamID>", this)); return; }

            string userID = args.Args[1];
            string name = "DropBase";
            ulong config = 0;
            BasePlayer player = null;

            foreach (var key in configData.settings.DropItems.ToList())
            {
                if (key.Value.ItemName.ToLower() == args.Args[0].ToLower())
                {
                    config = key.Key;
                    name = key.Value.ItemName;
                    break;
                }
            }
            if (config == 0)
            {
                SendReply(args, lang.GetMessage("Drop itemName not found in config", this));
                return;
            }

            var ids = default(ulong);
            if (ulong.TryParse(userID, out ids))
            {
                player = BasePlayer.FindByID(ids);
            }

            if (player != null)
            {
                GetBaseItem(player, false, config, name);
                SendReply(args, lang.GetMessage($"Gave drop to {player.displayName}", this));
                return;
            }
            PrintWarning($"Player not found with id: {args.Args[0]}");
        }
        [ChatCommand("getdrop")]
        void GetTheDropItem(BasePlayer player, string command, string[] args)
        {
            ulong config = 0;
            string name = "BaseDrop";
            if (player.net?.connection != null && !permission.UserHasPermission(player.UserIDString, configData.settings.PermissionUse))
            {
                SendReply(player, string.Format(lang.GetMessage("NoPerm", this, player.UserIDString)));
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, string.Format(lang.GetMessage("usagecommand", this, player.UserIDString)));
                return;
            }

            foreach (var key in configData.settings.DropItems.ToList())
            {
                if (key.Value.ItemName.ToLower() == args[0].ToLower())
                {
                    config = key.Key;
                    name = key.Value.ItemName;
                    break;
                }
            }
            if (config == 0)
            {
                SendReply(player, lang.GetMessage("noDrop", this, player.UserIDString), args[0]);
                return;
            }

            if (!pcdData.pEntity.ContainsKey(player.userID))
            {
                pcdData.pEntity.Add(player.userID, new PCDInfo());
            }
            double timeStamp = GrabCurrentTime();
            if (pcdData.pEntity.ContainsKey(player.userID)) // Check if the player already has a cooldown for this
            {
                var cdTime = pcdData.pEntity[player.userID].cooldownGet; // Get the cooldown time of the NPC
                if (cdTime > timeStamp)
                {
                    SendReply(player, lang.GetMessage("cooldown", this, player.UserIDString), (int)(cdTime - timeStamp));
                    return;
                }
            }

            GetBaseItem(player, true, config, name);
            int time = configData.settings.cooldownGet;

            pcdData.pEntity[player.userID].cooldownGet = (long)timeStamp + time;
            SaveData();
        }

        public void GetBaseItem(BasePlayer player, bool message, ulong skinid = 2131827661, string theName = "BaseDrop")
        {
            if (skinid == null || theName == null) return;
            var card = ItemManager.CreateByItemID(1975934948, 1, skinid);
            if (card == null) return;
            card.name = theName;

            if (card.MoveToContainer(player.inventory.containerBelt, -1, true))
            {
                if (message) SendReply(player, lang.GetMessage("gave", this));
                return;
            }
            else if (card.MoveToContainer(player.inventory.containerMain, -1, true))
            {
                if (message) SendReply(player, lang.GetMessage("gave", this));
                return;
            }
            Vector3 velocity = Vector3.zero;
            card.Drop(player.transform.position + new Vector3(0.5f, 1f, 0), velocity);
            if (message) SendReply(player, lang.GetMessage("droped", this));
        }


        void OnPasteFinished(List<BaseEntity> pastedEntities, string filename)
        {
            if (pastedEntities.Count <= 0) return;
            ulong playerID = 0UL;
            Vector3 locationKey = Vector3.zero;
            BasePlayer player = null;
            bool movedPlayer = false;

            foreach (var key in pastedEntities)
            {
                if (key == null) continue;
                foreach (var location in PlayerBase.ToList())
                {
                    float distance = Vector3.Distance(location.Key, key.transform.position);
                    if (distance <= 20) { playerID = location.Value; locationKey = location.Key; break; }

                }
            }
            if (playerID == 0UL) { return; }
            if (PlayerEntitys.ContainsKey(playerID))
                PlayerEntitys.Remove(playerID);
            PlayerEntitys.Add(playerID, pastedEntities);
            timer.Once(60.0f, () => { noMoreRemove(playerID); });
            foreach (var key in pastedEntities.ToList())
            {
                if (key != null)
                {
                    key.OwnerID = playerID;
                    var auth = new PlayerNameID { userid = playerID, username = RustCore.FindPlayerById(playerID)?.displayName ?? string.Empty, ShouldPool = true };

                    if (key is BuildingPrivlidge)
                    {
                        BuildingPrivlidge buildingPrivlidge = key as BuildingPrivlidge;
                        if (auth == null || buildingPrivlidge == null) continue;
                        buildingPrivlidge.authorizedPlayers.Add(auth);
                    }
                    else if (key is AutoTurret)
                    {
                        AutoTurret autoTurret = key as AutoTurret;
                        if (autoTurret == null) continue;
                        var isOnline = autoTurret.IsOnline();
                        if (isOnline) { autoTurret.SetIsOnline(false); timer.Once(2, () => { if (autoTurret != null) autoTurret.SetIsOnline(true); }); }
                        autoTurret.authorizedPlayers.Clear();
                        if (auth == null) continue;
                        autoTurret.authorizedPlayers.Add(auth);
                    }
                    else if (key is CodeLock)
                    {
                        CodeLock codeLock = key as CodeLock;
                        if (codeLock != null) codeLock.whitelistPlayers.Add(playerID);
                    }
                    else if (key is SleepingBag)
                    {
                        SleepingBag sleepingBag = key as SleepingBag;
                        if (sleepingBag == null) continue;
                        sleepingBag.deployerUserID = playerID;
                        sleepingBag.niceName = "dropBase";
                        player = BasePlayer.FindByID(playerID);
                        if (!movedPlayer && player != null)
                        {
                            movedPlayer = true;
                            Vector3 vector3 = sleepingBag.transform.position + sleepingBag.spawnOffset;
                            Quaternion rotation = Quaternion.Euler(0.0f, sleepingBag.transform.rotation.eulerAngles.y, 0.0f);
                            player.EnsureDismounted();
                            timer.Once(0.5f, () =>
                            {
                                TeleportPlayerPosition(player, vector3, rotation);
                            });
                        }
                    }
                }
            }
            if (PlayerBase.ContainsKey(locationKey)) PlayerBase.Remove(locationKey);
            player = BasePlayer.FindByID(playerID);
            if (player != null)
                SendReply(player, lang.GetMessage("Undo", this, player.UserIDString));
        }

        private void TeleportPlayerPosition(BasePlayer player, Vector3 destination, Quaternion rotation)
        {
            player.GetMounted()?.DismountPlayer(player, true);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            player.inventory.crafting.CancelAll(true);
            StartSleeping(player);
            player.MovePosition(destination);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
            player.transform.rotation = rotation;
        }

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
        }

        private void noMoreRemove(ulong playerID)
        {
            if (PlayerEntitys.ContainsKey(playerID))
            {
                PlayerEntitys.Remove(playerID);
                BasePlayer player = BasePlayer.FindByID(playerID);
                if (player != null)
                    SendReply(player, lang.GetMessage("NoUndo", this, player.UserIDString));
            }
        }

        private void removeDropBase(BasePlayer player)
        {
            ulong playerID = player.userID;
            if (!PlayerDrop.ContainsKey(playerID) || !PlayerEntitys.ContainsKey(playerID)) return;
            if (player != null)
            {
                GetBaseItem(player, false, PlayerDrop[playerID].skinid, PlayerDrop[playerID].name);
				if (pcdData.pEntity.ContainsKey(player.userID))
                {
                    pcdData.pEntity[player.userID].cooldown = 0;
					SaveData();
                }

                SendReply(player, lang.GetMessage("redo", this, player.UserIDString));
            }
            foreach (BaseEntity entity in PlayerEntitys[playerID].ToList())
            {
                if (entity == null) continue;
                if (entity is StorageContainer)
                {
                    StorageContainer container = entity as StorageContainer;
                    if (container != null)
                    {
                        foreach (Item item in container.inventory.itemList.ToList())
                        {
                            if (item == null) continue;
                            container.inventory.Take(null, item.info.itemid, item.amount);
                        }
                        container.inventory.Clear();
                    }
                }
                entity.Kill();
            }
        }
        [ChatCommand("redo")]
        void redoTheDrop(BasePlayer player, string command, string[] args)
        {
            if (!PlayerDrop.ContainsKey(player.userID) || !PlayerEntitys.ContainsKey(player.userID))
            {
                SendReply(player, lang.GetMessage("noredo", this, player.UserIDString));
                return;
            }
            removeDropBase(player);
        }

        private void OnLootEntity(BasePlayer player, StorageContainer container)
        {
            if (PlayerEntitys.ContainsKey(container.OwnerID))
            {
                noMoreRemove(container.OwnerID);
            }
        }
    }
}

