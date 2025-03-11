using System.Collections.Generic;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using VLB;
using Rust;
using Facepunch;
using static Facepunch.Pool;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("Auto Farm", "https://discord.gg/dNGbxafuJn", "2.0.3")]
    [Description("Auto Farm The PlanterBoxes")]
    public class AutoFarm : RustPlugin
    {
        #region Init
        [PluginReference]
        private Plugin Ganja;

        FarmEntity pcdData;
        private DynamicConfigFile PCDDATA;
        static System.Random random = new System.Random();
        private static AutoFarm _;
        public Dictionary<ulong, int> playerTotals = new Dictionary<ulong, int>();
        public Dictionary<ulong, bool> disabledFarmPlayer = new Dictionary<ulong, bool>();
        public List<ulong> GametipShown = new List<ulong>(); 

        void Init()
        {
            _ = this;
            if (configData.settings.Permission == null || configData.settings.Permission.Count < 0)
            {
                configData.settings.Permission = new Dictionary<string, int>() { { "autofarm.allow", 200 }, { "autofarm.vip", 4 } };
                Config.WriteObject(configData, true);
            }

            RegisterPermissions();

            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/Farm_Data");
            LoadData();

            foreach (var i in pcdData.Planters.ToList())
            {
                var networkable = FindEntity(i.Key);
                if (networkable != null)
                {
                    planterBoxBehavior mono = networkable.GetOrAddComponent<planterBoxBehavior>();
                    mono.autoFill();
                }
            }
        }

        private void OnServerInitialized()
        {
            if (configData.settings.seedsAllowedAndMultiplier == null || configData.settings.seedsAllowedAndMultiplier.Count <= 0)
            {
                configData.settings.seedsAllowedAndMultiplier = new Dictionary<int, int>() { {803954639, 1 }, {998894949, 1}, {1911552868, 1}, {-1776128552, 1}, {-237809779, 1}, {-2084071424, 1}, {-1511285251, 1}, {830839496, 1}, {-992286106, 1}, {-520133715, 1}, {838831151, 1}, {-778875547, 1}, {-1305326964, 1}, {-886280491, 1}, {1512054436, 1}, {1898094925, 1}, {2133269020, 1}, {1533551194, 1}, {390728933, 1} };
                SaveConfig();
            }

            if (configData.settings.SprinklerRadius <= 0.0f)
            {
                configData.settings.SprinklerRadius = 1;
                SaveConfig();
            }

            if (configData.settings.LargePlanterSprinklerMinSoilSaturation <= 0)
            {
                configData.settings.LargePlanterSprinklerMinSoilSaturation = 5100;
                configData.settings.LargePlanterSprinklerMaxSoilSaturation = 6000;
                configData.settings.SmallPlanterSprinklerMinSoilSaturation = 1650;
                configData.settings.SmallPlanterSprinklerMaxSoilSaturation = 1750;
                SaveConfig();
            }

            if (configData.settingsClone == null)
            {
                configData.settingsClone = new ConfigData.SettingsClone();
                configData.settingsClone.cloneList = new Dictionary<string, string>()
                {
                    { "blue_berry", "Sapling" },
                    { "white_berry", "Sapling" },
                    { "red_berry", "Sapling" },
                    { "green_berry", "Sapling" },
                    { "black_berry", "Sapling" },
                    { "yellow_berry", "Sapling" },
                    { "pumpkin", "Sapling" },
                    { "potato", "Sapling" },
                    { "hemp", "Sapling" },
                    { "corn", "Sapling" }
                };
                SaveConfig();
            }
            if (configData.settingsClone.cloneList.ContainsKey("blue_berry.entity"))
            {
                Dictionary<string, string> newList = new Dictionary<string, string>();
                foreach (var oldList in configData.settingsClone.cloneList)
                {
                    newList.Add(oldList.Key.Replace(".entity", "", StringComparison.OrdinalIgnoreCase).Trim(), oldList.Value);
                }
                newList.Add("corn", "Sapling");
                configData.settingsClone.cloneList = newList;
                SaveConfig();
                PrintWarning("Updating config clone list.");
            }

            NextTick(() =>
            {
                int removeCount = 0;
                int totalPlanters = 0;
                if (pcdData.Planters.Count <= 0) return;

                Dictionary<ulong, PCDInfo> plantersTemp = Pool.Get<Dictionary<ulong, PCDInfo>>();
                plantersTemp = new Dictionary<ulong, PCDInfo>(pcdData.Planters);

                foreach (var i in plantersTemp)
                {
                    var networkable = FindEntity(i.Key);
                    if (networkable == null) { pcdData.Planters.Remove(i.Key); removeCount++; }
                    else
                    {
                        totalPlanters++;
                        planterBoxBehavior mono = networkable.GetOrAddComponent<planterBoxBehavior>();
                        timer.Once(5, () => { if (_ != null && mono != null) mono.autoFill(); });
                    }
                }
                SaveData();
                PrintWarning($"Removed {removeCount} planters not found from datafile, and reactivated {totalPlanters}");
                Pool.Free(ref plantersTemp);
            });
        }

        private void Unload()
        {
            foreach (var Controler in UnityEngine.Object.FindObjectsOfType<planterBoxBehavior>())
            {
                UnityEngine.Object.DestroyImmediate(Controler);
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                player.SendConsoleCommand("gametip.hidegametip");
            }

            _ = null;
        }

        private void RegisterPermissions()
        {
            if (configData.settings.Permission != null && configData.settings.Permission.Count > 0)
                foreach (var perm in configData.settings.Permission)
                    permission.RegisterPermission(perm.Key, this);
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["enabled"] = "<color=#ce422b>AutoFarm placment enabled!</color>",
                ["disabled"] = "<color=#ce422b>AutoFarm placment disabled!</color>",
                ["max"] = "<color=#ce422b>You reached your max AutoFarm placments of {0}!</color>",
                ["rotatePlanter"] = "You can rotate the planter by hitting it with a hammer!",
                ["noPickup"] = "Storage contains items still!"
            }, this);
        }
        #endregion

        #region Config

        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; }

            [JsonProperty(PropertyName = "Exposure Settings")]
            public SettingsExposure settingsExposure { get; set; }

            [JsonProperty(PropertyName = "Auto Clone Settings")]
            public SettingsClone settingsClone { get; set; }

            public class Settings
            {
                [JsonProperty(PropertyName = "Disable autofarm placement by default /autofarm")]
                public bool DisablePlacementByDefault { get; set; }
                [JsonProperty(PropertyName = "How many slots in seed container")]
                public int seedStorageSlots { get; set; }
                [JsonProperty(PropertyName = "How many slots in output container")]
                public int BoxStorageSlots { get; set; }
                [JsonProperty(PropertyName = "Add sprinkler to planter")]
                public bool AddSprinkler { get; set; }
                [JsonProperty(PropertyName = "How far can sprinkler water")]
                public float SprinklerRadius { get; set; }
                [JsonProperty(PropertyName = "Add storage adapters")]
                public bool AddStorageAdapter { get; set; }
                [JsonProperty(PropertyName = "Sprinkler needs water hookup to work")]
                public bool SprinklerNeedsWater { get; set; }
                [JsonProperty(PropertyName = "Large Box Sprinkler On Soil Saturation Level")]
                public int LargePlanterSprinklerMinSoilSaturation { get; set; }
                [JsonProperty(PropertyName = "Large Box Sprinkler OFF Soil Saturation Level")]
                public int LargePlanterSprinklerMaxSoilSaturation { get; set; }
                [JsonProperty(PropertyName = "Small Box Sprinkler On Soil Saturation Level")]
                public int SmallPlanterSprinklerMinSoilSaturation { get; set; }
                [JsonProperty(PropertyName = "Small Box Sprinkler OFF Soil Saturation Level")]
                public int SmallPlanterSprinklerMaxSoilSaturation { get; set; }
                [JsonProperty(PropertyName = "Enable for use in plugins that require CallHookOnCollectiblePickup")]
                public bool CallHookOnCollectiblePickup { get; set; }
                [JsonProperty(PropertyName = "Enable weed pick from Ganja plugin")]
                public bool GanjaPluginEnable { get; set; }
                [JsonProperty(PropertyName = "Allowed seed itemID's and multiplier amount to get on auto gather")]
                public Dictionary<int, int> seedsAllowedAndMultiplier { get; set; }
                [JsonProperty(PropertyName = "Permission needed to place autofarms")]
                public Dictionary<string, int> Permission { get; set; }
            }

            public class SettingsExposure
            {
                [JsonProperty(PropertyName = "Always Light Exposure")]
                public bool lightExposure { get; set; }
                [JsonProperty(PropertyName = "Always Heat Exposure")]
                public bool heatExposure { get; set; }
            }

            public class SettingsClone
            {
                [JsonProperty(PropertyName = "Disable Auto Clone")]
                public bool enableCloning { get; set; }

                [JsonProperty(PropertyName = "Allowed To Clone And Stage ")]
                public Dictionary<string, string> cloneList { get; set; }
                [JsonProperty(PropertyName = "Available tools And Clone Multiplier")]
                public Dictionary<int, int> knifeList { get; set; }
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
                    DisablePlacementByDefault = false,
                    seedStorageSlots = 6,
                    BoxStorageSlots = 24,
                    AddSprinkler = false,
                    SprinklerRadius = 1,
                    AddStorageAdapter = false,
                    SprinklerNeedsWater = false,
                    LargePlanterSprinklerMinSoilSaturation = 5100,
                    LargePlanterSprinklerMaxSoilSaturation = 6000,
                    SmallPlanterSprinklerMinSoilSaturation = 1650,
                    SmallPlanterSprinklerMaxSoilSaturation = 1750,
                    CallHookOnCollectiblePickup = false,
                    GanjaPluginEnable = false,
                    seedsAllowedAndMultiplier = new Dictionary<int, int>() { { 803954639, 1 }, { 998894949, 1 }, { 1911552868, 1 }, { -1776128552, 1 }, { -237809779, 1 }, { -2084071424, 1 }, { -1511285251, 1 }, { 830839496, 1 }, { -992286106, 1 }, { -520133715, 1 }, { 838831151, 1 }, { -778875547, 1 }, { -1305326964, 1 }, { -886280491, 1 }, { 1512054436, 1 }, { 1898094925, 1 }, { 2133269020, 1 }, { 1533551194, 1 }, { 390728933, 1 } },
                    Permission = new Dictionary<string, int>() { { "autofarm.allow", 2 }, { "autofarm.vip", 4 } }
                },

                settingsExposure = new ConfigData.SettingsExposure
                {
                    lightExposure = false,
                    heatExposure = false
                },

                settingsClone = new ConfigData.SettingsClone
                {
                    enableCloning = true,
                    cloneList = new Dictionary<string, string>() { { "blue_berry", "Sapling" }, { "white_berry", "Sapling" }, { "red_berry", "Sapling" }, { "green_berry", "Sapling" }, { "black_berry", "Sapling" }, { "yellow_berry", "Sapling" }, { "pumpkin", "Sapling" }, { "potato", "Sapling" }, { "hemp", "Sapling" }, { "corn", "Sapling" } },
                    knifeList = new Dictionary<int, int>() { { 1814288539, 0 }, { -194509282, 0 }, { 2040726127, 0 }, { -2073432256, 0 } }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(2, 0, 2))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion Config

        #region Data

        void LoadData()
        {
            try
            {
                pcdData = Interface.GetMod().DataFileSystem.ReadObject<FarmEntity>(Name + "/Farm_Data") ?? new FarmEntity();
            }
            catch
            {
                PrintWarning("Couldn't load Farm_Data, creating new FarmData file");
                pcdData = new FarmEntity();
            }
        }
        class FarmEntity
        {
            public Dictionary<ulong, PCDInfo> Planters = new Dictionary<ulong, PCDInfo>();

            public FarmEntity() { }
        }
        class PCDInfo : IPooled
        {
            public int lifeLimit;
            public int pickedCount;
            public ulong ownerid;

            // Метод вызывается, когда объект возвращается в пул
            public void EnterPool()
            {
                lifeLimit = 0;
                pickedCount = 0;
                ownerid = 0;
            }

            // Метод вызывается, когда объект извлекается из пула
            public void LeavePool()
            {
                // Если требуется особая инициализация, её можно добавить здесь
                // Например, сбросить данные или установить дефолтные значения
            }
        }

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        public BaseNetworkable FindEntity(ulong netID)
        {
         //   KeyValuePair<NetworkableId, BaseNetworkable> searchResult = BaseNetworkable.serverEntities.entityList.FirstOrDefault(s => s.Value.net.ID.Value == netID);
            return BaseNetworkable.serverEntities.Find(new NetworkableId(netID));
        }
        #endregion Data

        #region Hooks
        [ChatCommand("autofarm")]
        private void thePath(BasePlayer player, string cmd, string[] args)
        {
            foreach (var key in configData.settings.Permission)
            {
                if (!configData.settings.DisablePlacementByDefault)
                {
                    if (permission.UserHasPermission(player.UserIDString, key.Key))
                    {
                        if (disabledFarmPlayer.ContainsKey(player.userID))
                        {
                            disabledFarmPlayer.Remove(player.userID);
                            SendReply(player, lang.GetMessage("enabled", this, player.UserIDString));
                            return;
                        }
                        else
                        {
                            disabledFarmPlayer.Add(player.userID, true);
                            SendReply(player, lang.GetMessage("disabled", this, player.UserIDString));
                            return;
                        }

                    }
                }
                else if (configData.settings.DisablePlacementByDefault)
                {
                    if (permission.UserHasPermission(player.UserIDString, key.Key))
                    {
                        if (disabledFarmPlayer.ContainsKey(player.userID))
                        {
                            disabledFarmPlayer.Remove(player.userID);
                            SendReply(player, lang.GetMessage("disabled", this, player.UserIDString));
                            return;
                        }
                        else
                        {
                            disabledFarmPlayer.Add(player.userID, true);
                            SendReply(player, lang.GetMessage("enabled", this, player.UserIDString));
                            return;
                        }
                    }
                }

            }
            SendReply(player, lang.GetMessage("NoPerms", this, player.UserIDString));
        }

        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            var player = plan?.GetOwnerPlayer();
            PlanterBox planterBox = gameObject.GetComponent<PlanterBox>();
            if (planterBox == null || player == null)
                return;

            if (!configData.settings.DisablePlacementByDefault && disabledFarmPlayer.ContainsKey(player.userID))
                return;
            else if (configData.settings.DisablePlacementByDefault && !disabledFarmPlayer.ContainsKey(player.userID))
                return;

            int totals = 0;
            int playertotals = 0;
            foreach (var key in configData.settings.Permission)
            {
                if (permission.UserHasPermission(player.UserIDString, key.Key))
                {
                    totals = key.Value;
                }
            }

            if (playerTotals.ContainsKey(planterBox.OwnerID))
                playertotals = playerTotals[player.userID];
            if (playertotals < totals)
            {
                planterBoxBehavior cropStorage = planterBox.gameObject.AddComponent<planterBoxBehavior>();
                pcdData.Planters.Add(planterBox.net.ID.Value, new PCDInfo());
                SaveData();
                if (!GametipShown.Contains(player.userID))
                {
                    GameTips(player, lang.GetMessage("rotatePlanter", this, player.UserIDString));
                    GametipShown.Add(player.userID);
                }
            }
            else if (player != null && totals > 0) SendReply(player, lang.GetMessage("max", this, player.UserIDString), totals);
        }

        void OnLootEntityEnd(BasePlayer player, StorageContainer stash)
        {
            planterBoxBehavior planterBox = stash.GetComponentInParent<PlanterBox>()?.GetComponentInParent<planterBoxBehavior>();
            if (planterBox != null)
            {
                planterBox.autoFill();
                if (!_.configData.settingsClone.enableCloning)
                    planterBox.findKnife();
            }
        }

        private object OnEntityTakeDamage(StorageContainer stash, HitInfo hitInfo)
        {
            var planterBox = stash.GetParentEntity()?.GetComponentInParent<PlanterBox>();
            if (planterBox != null && planterBox.GetComponent<planterBoxBehavior>() != null)
            {
                hitInfo.damageTypes.ScaleAll(0);
                return true;
            }

            return null;
        }

        void CanTakeCutting(BasePlayer player, GrowableEntity entity)
        {
            if (entity.GetPlanter() != null)
            {
                PlanterBox planter = entity.GetPlanter();
                NextTick(() => { planter?.GetComponent<planterBoxBehavior>()?.autoFill(); });
            }
        }

        private void OnGrowableGather(GrowableEntity entity, BasePlayer player, bool param3)
        {
            if (entity.GetPlanter() != null)
            {
                PlanterBox planter = entity.GetPlanter();
                NextTick(() => { planter?.GetComponent<planterBoxBehavior>()?.autoFill(); });
            }
        }

        private void OnCollectiblePickup(CollectibleEntity entity, BasePlayer player)
        {
            if (entity.GetComponentInParent<PlanterBox>() != null)
            {
                PlanterBox planter = entity.GetComponentInParent<PlanterBox>();
                NextTick(() => { planter?.GetComponent<planterBoxBehavior>()?.autoFill(); });
            }
        }

        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (container.entityOwner == null) return null;
            if (container.entityOwner is StorageContainer)
            {
                if (container.entityOwner.name != null && container.entityOwner.name != "seedsBox") return null;
                planterBoxBehavior planterBox = container.entityOwner?.GetComponentInParent<PlanterBox>()?.GetComponentInParent<planterBoxBehavior>();
                if (planterBox != null && container.entityOwner.name == "seedsBox")
                {
                    if (!configData.settingsClone.enableCloning && configData.settingsClone.knifeList.ContainsKey(item.info.itemid))
                    {
                        if (planterBox.findKnife())
                            return ItemContainer.CanAcceptResult.CannotAccept;
   
                        return null;
                    }

                    if (!configData.settings.seedsAllowedAndMultiplier.ContainsKey(item.info.itemid))
                        return ItemContainer.CanAcceptResult.CannotAccept;
                }
            }
            return null;
        }

        private object CanPickupEntity(BasePlayer player, Sprinkler entity)
        {
            if (entity == null) return null;
            if (entity != null && entity.name != null && entity.name.Contains("NoPickUp"))
            {
                string stringID = entity.name.Replace("NoPickUp", "");
                if (!string.IsNullOrEmpty(stringID))
                {
                    BaseNetworkable planter = FindEntity(Convert.ToUInt64(stringID));
                    planter?.GetComponent<planterBoxBehavior>()?.pickUpPlanter(player);
                }
                return false;
            }
            return null;
        }

        private void CanPickupEntity(BasePlayer player, PlanterBox entity)
        {
            if (entity != null)
                entity.GetComponent<planterBoxBehavior>()?.RemoveEntitys();
        }

        private object CanPickupEntity(BasePlayer player, Splitter entity)
        {
            if (entity == null) return null;
            if (entity != null && entity.name != null && entity.name.Contains("NoPickUp"))
            {
                string stringID = entity.name.Replace("NoPickUp", "");
                if (!string.IsNullOrEmpty(stringID))
                {
                    BaseNetworkable planter = FindEntity(Convert.ToUInt64(stringID));
                    planter?.GetComponent<planterBoxBehavior>()?.pickUpPlanter(player);
                }
                return false;
            }
            return null;
        }

        private object CanPickupEntity(BasePlayer player, IndustrialStorageAdaptor entity)
        {
            if (entity == null) return null;
            if (entity != null && entity.name != null && entity.name.Contains("NoPickUp"))
            {
                entity.GetComponentInParent<planterBoxBehavior>()?.pickUpPlanter(player);
                return false;
            }
            return null;
        }

        private object OnWireClear(BasePlayer player, IOEntity entity1, int connected, IOEntity entity2, bool flag)
        {
            if (entity1 != null && entity1 is Splitter && entity1.name != null && entity1.name.Contains("NoPickUp") && entity2 != null && entity2 is Sprinkler && entity2.name != null && entity2.name.Contains("NoPickUp"))
            {
                return false;
            }
            return null;
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            entity?.GetComponent<PlanterBox>()?.GetComponent<planterBoxBehavior>()?.RemoveEntitys();
        }

        private void OnGrowableStateChange(GrowableEntity growableEntity, PlantProperties.State state)
        {
            _.NextTick(() =>
            {
                if (growableEntity != null && growableEntity.planter != null)
                {
                    planterBoxBehavior behavior = growableEntity.planter.GetComponent<planterBoxBehavior>();
                    if (behavior != null)
                    {
                        behavior.seeIfCanPick(growableEntity);
                    }
                }
            });
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            PlanterBox planterBox = info?.HitEntity as PlanterBox;
            if (planterBox != null)
            {
                planterBoxBehavior behavior = planterBox.GetComponent<planterBoxBehavior>();
                if (behavior == null) return;

                if (behavior.ownerplayer == player.userID && planterBox.health == planterBox.MaxHealth())
                    behavior.Rotate();
            }
        }

        public void GameTips(BasePlayer player, string tipsShow)
        {
            if (player != null && player.userID.IsSteamId())
            {
                player?.SendConsoleCommand("gametip.hidegametip");
                player?.SendConsoleCommand("gametip.showgametip", tipsShow);
                _.timer.Once(4f, () => player?.SendConsoleCommand("gametip.hidegametip"));
            }
        }

        public static IndustrialStorageAdaptor AddStorageAdaptor(PlanterBox planterBox, BaseEntity parent, Vector3 pos, Quaternion rot)
        {
            IndustrialStorageAdaptor adaptor = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/industrialadaptors/storageadaptor.deployed.prefab", planterBox.transform.TransformPoint(pos), planterBox.transform.rotation * rot) as IndustrialStorageAdaptor;
            adaptor.OwnerID = parent.OwnerID;
            adaptor.Spawn();

            SpawnRefresh(adaptor);
            adaptor.SetParent(parent, true, true);
            adaptor.SendNetworkUpdateImmediate();
            return adaptor;
        }

        public static void SpawnRefresh(BaseEntity entity1)
        {
            if (entity1 != null)
            {
                if (entity1.GetComponentsInChildren<MeshCollider>() != null)
                foreach (var mesh in entity1.GetComponentsInChildren<MeshCollider>())
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }

                if (entity1.GetComponent<Collider>() != null)
                    UnityEngine.Object.DestroyImmediate(entity1.GetComponent<Collider>());
                if (entity1.GetComponent<GroundWatch>() != null)
                    UnityEngine.Object.DestroyImmediate(entity1.GetComponent<GroundWatch>());
                if (entity1.GetComponent<DestroyOnGroundMissing>() != null)
                    UnityEngine.Object.DestroyImmediate(entity1.GetComponent<DestroyOnGroundMissing>());
            }
        }

        private static void UpdateLocalPositionAndRotation(Transform parentTransform, BaseEntity entity, Vector3 localPosition, Quaternion localRotation)
        {
            var transform = entity.transform;
            var intendedPosition = parentTransform.TransformPoint(localPosition);
            var intendedRotation = parentTransform.rotation * localRotation;

            List<List<Vector3>> outputPointLists = null;

            var ioEntity = entity as IOEntity;
            if (ioEntity != null)
            {
                outputPointLists = new List<List<Vector3>>();

                // Save IO outputs using world position, to be restored later to new local positions.
                foreach (var slot in ioEntity.outputs)
                {
                    if (slot.connectedTo.Get() == null)
                        continue;

                    var pointList = new List<Vector3>();
                    outputPointLists.Add(pointList);

                    // Skip the last (closest) line point, since it needs to maintain relative position.
                    for (var i = 0; i < slot.linePoints.Length - 1; i++)
                    {
                        var localPointPosition = slot.linePoints[i];
                        pointList.Add(transform.TransformPoint(localPointPosition));
                    }
                }
            }

            var wantsPositionChange = transform.position != intendedPosition;
            var wantsRotationChange = transform.rotation != intendedRotation;
            var wantsChange = wantsPositionChange || wantsRotationChange;

            BaseEntity[] children = null;
            if (wantsChange && entity.children.Count > 0)
            {
                children = entity.children.ToArray();

                // Temporarily unparent industrial adapters, so their pipes aren't messed up.
                // Pipes will be saved and restored when this method is called for the adapters.
                foreach (var child in children)
                {
                    child.SetParent(null, worldPositionStays: true, sendImmediate: false);
                }
            }

            if (wantsPositionChange)
            {
                transform.position = intendedPosition;
            }

            if (wantsRotationChange)
            {
                transform.rotation = intendedRotation;
            }

            // Re-parent industrial adapters, without changing their position.
            if (children != null)
            {
                foreach (var child in children)
                {
                    child.SetParent(entity, worldPositionStays: true);
                }
            }

            if (wantsChange && outputPointLists != null)
            {
                // Restore IO outputs from world position to new local positions.
                for (int i = 0, j = 0; i < ioEntity.outputs.Length; i++)
                {
                    var slot = ioEntity.outputs[i];
                    if (slot.connectedTo.Get() == null)
                        continue;

                    var pointList = outputPointLists[j++];

                    // Skip the last (closest) line point, since it needs to maintain relative position.
                    for (var k = 0; k < slot.linePoints.Length - 1; k++)
                    {
                        slot.linePoints[k] = transform.InverseTransformPoint(pointList[k]);
                    }
                }

                foreach (var inputSlot in ioEntity.inputs)
                {
                    var inputEntity = inputSlot.connectedTo.Get();
                    if (inputEntity == null)
                        continue;

                    var linePoints = inputEntity.outputs.ElementAtOrDefault(inputSlot.connectedToSlot)?.linePoints;
                    if (linePoints == null || linePoints.Length == 0)
                        continue;

                    var worldHandlePosition = transform.TransformPoint(inputSlot.handlePosition);
                    linePoints[0] = inputEntity.transform.InverseTransformPoint(worldHandlePosition);
                    inputEntity.TerminateOnClient(BaseNetworkable.DestroyMode.None);
                    inputEntity.SendNetworkUpdateImmediate();
                }
            }

            if (wantsChange)
            {
                if (ioEntity != null)
                {
                    // Line points may have changed, which needs a full network snapshot.
                    entity.TerminateOnClient(BaseNetworkable.DestroyMode.None);
                    entity.SendNetworkUpdateImmediate();
                }
                else
                {
                    entity.InvalidateNetworkCache();
                    entity.SendNetworkUpdate_Position();
                }
            }
        }

        private static T GetNearbyEntity<T>(Vector3 position, float radius) where T : BaseEntity
        {
            List<T> nearby = new List<T>();
            Vis.Entities(position, radius, nearby);
            return nearby.FirstOrDefault();
        }

        private static T GetChildOfType<T>(BaseEntity entity) where T : class
        {
            foreach (var child in entity.children)
            {
                var childOfType = child as T;
                if (childOfType != null)
                    return childOfType;
            }

            return null;
        }

        private static IndustrialStorageAdaptor GetAdapter(StorageContainer entity)
        {
            return GetChildOfType<IndustrialStorageAdaptor>(entity);
        }

        #endregion

        #region planerBox

        private struct PlanterConfig
        {
            public static PlanterConfig Small = new PlanterConfig
            {
                SprinklerPos = new Vector3(0f, 0.11f, 0f),
                SplitterPos = new Vector3(0, 0.1f, 0.5f),
                FertilizerAdapterPos = new Vector3(0.5f, 0.30f, 0.0f),
                InputStoragePos = new Vector3(0.5f, 0.20f, 0.50f),
                InputStorageRot = Quaternion.Euler(0, 0, 90),
                InputAdapterPos = new Vector3(1.1f, 0.20f, 0.50f),
                InputAdapterRot = Quaternion.Euler(90, 0, 0),
                OutputStoragePos = new Vector3(-0.5f, 0.20f, 0.50f),
                OutputStorageRot = Quaternion.Euler(0, 0, 270),
                OutputAdapterPos = new Vector3(-1.1f, .20f, 0.50f),
                OutputAdapterRot = Quaternion.Euler(90, 0, 0),
            };

            public static PlanterConfig Minecart = new PlanterConfig
            {
                SprinklerPos = new Vector3(0f, 0.55f, 0f),
                SplitterPos = new Vector3(0, 0.36f, 0.75f),
                FertilizerAdapterPos = new Vector3(0.55f, 0.75f, 0.3f),
                InputStoragePos = new Vector3(0.183f, 0.60f, 0.50f),
                InputStorageRot = Quaternion.Euler(0, 0, 0),
                InputAdapterPos = new Vector3(0.58f, 0.60f, 0.50f),
                InputAdapterRot = Quaternion.Euler(90, 0, 0),
                OutputStoragePos = new Vector3(-0.183f, 0.60f, 0.50f),
                OutputStorageRot = Quaternion.Euler(0, 0, 0),
                OutputAdapterPos = new Vector3(-0.58f, 0.60f, 0.50f),
                OutputAdapterRot = Quaternion.Euler(90, 0, 0),
            };

            public static PlanterConfig Bathtub = new PlanterConfig
            {
                SprinklerPos = new Vector3(0f, 0.35f, 0f),
                SplitterPos = new Vector3(0, 0.12f, 0.75f),
                FertilizerAdapterPos = new Vector3(0.58f, 0.55f, 0.3f),
                InputStoragePos = new Vector3(0.183f, 0.32f, 0.50f),
                InputStorageRot = Quaternion.Euler(0, 0, 0),
                InputAdapterPos = new Vector3(0.58f, 0.40f, 0.50f),
                InputAdapterRot = Quaternion.Euler(90, 0, 0),
                OutputStoragePos = new Vector3(-0.183f, 0.32f, 0.50f),
                OutputStorageRot = Quaternion.Euler(0, 0, 0),
                OutputAdapterPos = new Vector3(-0.58f, 0.40f, 0.50f),
                OutputAdapterRot = Quaternion.Euler(90, 0, 0),
            };

            public static PlanterConfig Railroad = new PlanterConfig
            {
                SprinklerPos = new Vector3(0f, 0.13f, 0f),
                SplitterPos = new Vector3(0, 0.1f, 1.5f),
                FertilizerAdapterPos = new Vector3(0.5f, 0.30f, 1.0f),
                InputStoragePos = new Vector3(0.5f, 0.20f, 1.40f),
                InputStorageRot = Quaternion.Euler(0, 0, 90),
                InputAdapterPos = new Vector3(1.1f, 0.20f, 1.42f),
                InputAdapterRot = Quaternion.Euler(90, 0, 0),
                OutputStoragePos = new Vector3(-0.5f, 0.20f, 1.40f),
                OutputStorageRot = Quaternion.Euler(0, 0, 270),
                OutputAdapterPos = new Vector3(-1.1f, 0.20f, 1.42f),
                OutputAdapterRot = Quaternion.Euler(90, 0, 0),
            };

            public static PlanterConfig Large = new PlanterConfig
            {
                SprinklerPos = new Vector3(0f, 0.11f, 0f),
                SplitterPos = new Vector3(0, 0.1f, 1.425f),
                FertilizerAdapterPos = new Vector3(0.5f, 0.30f, 1.0f),
                InputStoragePos = new Vector3(0.5f, 0.20f, 1.40f),
                InputStorageRot = Quaternion.Euler(0, 0, 90),
                InputAdapterPos = new Vector3(1.1f, 0.20f, 1.40f),
                InputAdapterRot = Quaternion.Euler(90, 0, 0),
                OutputStoragePos = new Vector3(-0.5f, 0.20f, 1.40f),
                OutputStorageRot = Quaternion.Euler(0, 0, 270),
                OutputAdapterPos = new Vector3(-1.1f, 0.20f, 1.40f),
                OutputAdapterRot = Quaternion.Euler(90, 0, 0),
            };

            public Vector3 SprinklerPos;
            public Vector3 SplitterPos;
            public Vector3 FertilizerAdapterPos;

            public Vector3 InputStoragePos;
            public Quaternion InputStorageRot;
            public Vector3 InputAdapterPos;
            public Quaternion InputAdapterRot;

            public Vector3 OutputStoragePos;
            public Quaternion OutputStorageRot;
            public Vector3 OutputAdapterPos;
            public Quaternion OutputAdapterRot;
        }

        private static PlanterConfig GetPlanterConfig(PlanterBox planterBox)
        {
            switch (planterBox.ShortPrefabName)
            {
                case "planter.small.deployed":
                    return PlanterConfig.Small;
                case "minecart.planter.deployed":
                    return PlanterConfig.Minecart;
                case "bathtub.planter.deployed":
                    return PlanterConfig.Bathtub;
                case "railroadplanter.deployed":
                    return PlanterConfig.Railroad;
                default:
                    return PlanterConfig.Large;
            }
        }

        class planterBoxBehavior : FacepunchBehaviour
        {
            public PlanterBox planterBox { get; set; }
            public ulong ownerplayer { get; set; }
            private StorageContainer container { get; set; }
            private StorageContainer containerSeeds { get; set; }
            private Sprinkler sprinkler { get; set; }
            private Splitter waterSource { get; set; }
            private ItemDefinition itemDefinition { get; set; }
            private Dictionary<int, int> seeds = _.configData.settings.seedsAllowedAndMultiplier;
            private DateTime lastRotate { get; set; }
            private int totalslotsAvailable = 11;
            private float splashRadius = 6f;
            private int soilSaturationON { get; set; }
            private int soilSaturationOFF { get; set; }
            private float nextAutofiltime { get; set; }
            public Item cloningItem { get; set; }

            private void Awake()
            {
                planterBox = GetComponent<PlanterBox>();
                if (!_.playerTotals.ContainsKey(planterBox.OwnerID))
                    _.playerTotals.Add(planterBox.OwnerID, 1);
                else _.playerTotals[planterBox.OwnerID]++;
                ownerplayer = planterBox.OwnerID;
                splashRadius = _.configData.settings.SprinklerRadius;


                _.timer.Once(1, () =>
                {
                    generateStorage();
                    float delay = random.Next(300, 600);
                    InvokeRepeating("isPlanterFull", delay, 601);
                });

                switch (planterBox.ShortPrefabName)
                {
                    case "minecart.planter.deployed":
                        totalslotsAvailable = 4;
                        soilSaturationON = _.configData.settings.SmallPlanterSprinklerMinSoilSaturation;
                        soilSaturationOFF = _.configData.settings.SmallPlanterSprinklerMaxSoilSaturation;
                        break;

                    case "planter.small.deployed":
                    case "bathtub.planter.deployed":
                        totalslotsAvailable = 5;
                        soilSaturationON = _.configData.settings.SmallPlanterSprinklerMinSoilSaturation;
                        soilSaturationOFF = _.configData.settings.SmallPlanterSprinklerMaxSoilSaturation;
                        break;

                    default:
                        soilSaturationON = _.configData.settings.LargePlanterSprinklerMinSoilSaturation;
                        soilSaturationOFF = _.configData.settings.LargePlanterSprinklerMaxSoilSaturation;
                        break;
                }
                ForceLightUpdate();
                ForceArtificialTemperature();
            }
            public void Rotate()
            {
                if (lastRotate < DateTime.Now)
                {
                    var degrees = 180;

                    lastRotate = DateTime.Now.AddSeconds(2);
                    transform.Rotate(0, degrees, 0);
                    planterBox.SendNetworkUpdateImmediate(true);

                    if (waterSource != null)
                    {
                        waterSource.transform.RotateAround(transform.position, transform.up, degrees);
                        waterSource.SendNetworkUpdateImmediate();
                    }
                }
            }

            private float CalculateExposure() => 1f;
            private float CalculateTemperature() => 10f;

            public void ForceLightUpdate()
            {
                if (_.configData.settingsExposure.lightExposure)
                {
                    planterBox.artificialLightExposure = new TimeCachedValue<float>()
                    {
                        refreshCooldown = 60f,
                        refreshRandomRange = 5f,
                        updateValue = new Func<float>(CalculateExposure)
                    };
                }
            }

            public void ForceArtificialTemperature()
            {
                if (_.configData.settingsExposure.heatExposure)
                {
                    planterBox.plantArtificalTemperature = new TimeCachedValue<float>()
                    {
                        refreshCooldown = 60f,
                        refreshRandomRange = 5f,
                        updateValue = new Func<float>(CalculateTemperature)
                    };
                }
            }
          
            public void ResetArtificialLightExposure()
            {
                planterBox.artificialLightExposure = null;
               /* planterBox.plantArtificalTemperature = new TimeCachedValue<float>()
                {
                    refreshCooldown = 60f,
                    refreshRandomRange = 5f,
                    updateValue = new Func<float>(planterBox.CalculateArtificialLightExposure)
                };*/
            }
            
            public void ResetArtificialTemperature()
            {
                planterBox.plantArtificalTemperature = null;
                /*
                planterBox.plantArtificalTemperature = new TimeCachedValue<float>()
                {
                    refreshCooldown = 60f,
                    refreshRandomRange = 5f,
                    updateValue = new Func<float>(planterBox.CalculateArtificialTemperature)
                };*/
            }

            public bool pickUpPlanter(BasePlayer player)
            {
                if (containerSeeds != null)
                {
                    if (containerSeeds.inventory.itemList.Count > 0)
                    {
                        _.GameTips(player, _.lang.GetMessage("noPickup", _, player.UserIDString));
                        return false;
                    }
                    else if (container.inventory.itemList.Count > 0)
                    {
                        _.GameTips(player, _.lang.GetMessage("noPickup", _, player.UserIDString));
                        return false;
                    }
                    else if (planterBox.inventory.itemList.Count > 0)
                    {
                        _.GameTips(player, _.lang.GetMessage("noPickup", _, player.UserIDString));
                        return false;
                    }

                    int itemIdToGet;

                    switch (planterBox.ShortPrefabName)
                    {
                        case "minecart.planter.deployed":
                            itemIdToGet = 1361520181;
                            break;
                        case "planter.small.deployed":
                            itemIdToGet = 1903654061;
                            break;
                        case "bathtub.planter.deployed":
                            itemIdToGet = -1274093662;
                            break;
                        case "railroadplanter.deployed":
                            itemIdToGet = 615112838;
                            break;
                        default:
                            itemIdToGet = 1581210395;
                            break;
                    }

                    var planterItem = ItemManager.CreateByItemID(itemIdToGet, 1, 0);
                    if (planterItem != null)
                    {
                        player.GiveItem(planterItem, BaseEntity.GiveItemReason.PickedUp);
                        planterBox.Kill();
                    }
                }
                return true;
            }

            private void generateStorage()
            {
                var planterConfig = GetPlanterConfig(planterBox);

                // Search radius, for entities that are not parented to the planter.
                var entitySearchRadius = 0.1f;

                if (_.configData.settings.AddSprinkler)
                {
                    sprinkler = GetNearbyEntity<Sprinkler>(planterBox.transform.TransformPoint(planterConfig.SprinklerPos), entitySearchRadius)
                                ?? GetNearbyEntity<Sprinkler>(planterBox.transform.TransformPoint(PlanterConfig.Large.SprinklerPos), entitySearchRadius);

                    if (sprinkler != null)
                    {
                        UpdateLocalPositionAndRotation(transform, sprinkler, planterConfig.SprinklerPos, Quaternion.identity);
                    }
                }

                if (_.configData.settings.SprinklerNeedsWater && _.configData.settings.AddSprinkler)
                {
                    waterSource = GetNearbyEntity<Splitter>(planterBox.transform.TransformPoint(planterConfig.SplitterPos), entitySearchRadius)
                                ?? GetNearbyEntity<Splitter>(planterBox.transform.TransformPoint(PlanterConfig.Large.SplitterPos), entitySearchRadius);

                    if (waterSource != null)
                    {
                        UpdateLocalPositionAndRotation(transform, waterSource, planterConfig.SplitterPos, Quaternion.identity);
                    }
                }

                /*if (!_.configData.settings.SprinklerNeedsWater)
                {
                    Vector3 pos = new Vector3(planterBox.transform.position.x, planterBox.transform.position.y + 0.1f, planterBox.transform.position.z) + planterBox.transform.forward * 1.425f;
                    if (planterBox.ShortPrefabName == "planter.small.deployed")
                        pos = new Vector3(planterBox.transform.position.x, planterBox.transform.position.y + 0.1f, planterBox.transform.position.z) + planterBox.transform.forward * 0.50f;
                    waterSorce = isThereSplitter(pos, 0.5f);
                    if (waterSorce != null)
                        waterSorce.Kill();
                }*/

                bool fertilizerBoxAdapter = false;

                foreach (BaseEntity child in planterBox.children.ToList())
                {
                    if (child == null)
                        continue;

                    if (child is IndustrialStorageAdaptor)
                    {
                        UpdateLocalPositionAndRotation(transform, child, planterConfig.FertilizerAdapterPos, Quaternion.identity);

                        if (child.name != "NoPickUp")
                        {
                            child.name = "NoPickUp";
                        }

                        fertilizerBoxAdapter = true;
                        totalslotsAvailable++;
                    }

                    switch (child.GetType().ToString())
                    {
                        case "StorageContainer":
                            {
                                StorageContainer theStash = child as StorageContainer;
                                if (theStash != null && theStash.name != null)
                                {
                                    if (theStash.name == "seedItem" || theStash.transform.localPosition == planterConfig.OutputStoragePos || theStash.transform.localPosition == PlanterConfig.Large.OutputStoragePos)
                                    {
                                        container = theStash;
                                        container.name = "seedItem";
                                        SpawnRefresh(theStash);

                                        if (_.configData.settings.BoxStorageSlots > 0)
                                            container.inventory.capacity = _.configData.settings.BoxStorageSlots;

                                        UpdateLocalPositionAndRotation(transform, theStash, planterConfig.OutputStoragePos, planterConfig.OutputStorageRot);

                                        if (_.configData.settings.AddStorageAdapter)
                                        {
                                            IndustrialStorageAdaptor adapter = GetAdapter(container);
                                            if (adapter != null)
                                            {
                                                UpdateLocalPositionAndRotation(transform, adapter, planterConfig.OutputAdapterPos, planterConfig.OutputAdapterRot);
                                            }
                                            else if (container.isSpawned)
                                            {
                                                adapter = AddStorageAdaptor(planterBox, container, planterConfig.OutputAdapterPos, planterConfig.OutputAdapterRot);
                                                adapter.name = "NoPickUp";
                                            }
                                        }
                                    }
                                    else if (theStash.name == "seedsBox" || theStash.transform.localPosition == planterConfig.InputStoragePos || theStash.transform.localPosition == PlanterConfig.Large.InputStoragePos)
                                    {
                                        containerSeeds = theStash;
                                        containerSeeds.name = "seedsBox";
                                        SpawnRefresh(theStash);

                                        if (_.configData.settings.seedStorageSlots > 0)
                                            containerSeeds.inventory.capacity = _.configData.settings.seedStorageSlots;

                                        UpdateLocalPositionAndRotation(transform, theStash, planterConfig.InputStoragePos, planterConfig.InputStorageRot);

                                        if (_.configData.settings.AddStorageAdapter)
                                        {
                                            IndustrialStorageAdaptor adapter = GetAdapter(containerSeeds);
                                            if (adapter != null)
                                            {
                                                UpdateLocalPositionAndRotation(transform, adapter, planterConfig.InputAdapterPos, planterConfig.InputAdapterRot);
                                            }
                                            else if (containerSeeds.isSpawned)
                                            {
                                                adapter = AddStorageAdaptor(planterBox, containerSeeds, planterConfig.InputAdapterPos, planterConfig.InputAdapterRot);
                                                adapter.name = "NoPickUp";
                                            }
                                        }
                                        findKnife();
                                    }
                                }
                                break;
                            }
                    }
                }

                if (container == null)
                {
                    container = GameManager.server.CreateEntity("assets/prefabs/deployable/hot air balloon/subents/hab_storage.prefab", planterConfig.OutputStoragePos, planterConfig.OutputStorageRot) as StorageContainer;
                    if (container != null)
                    {
                        container.SetParent(planterBox);
                        container.panelTitle = new Translate.Phrase("seeds", "Seeds");

                        container.name = "seedItem";
                        container.OwnerID = planterBox.OwnerID;
                        container.Spawn();
                        SpawnRefresh(container);
                        container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);

                        if (_.configData.settings.BoxStorageSlots > 0)
                            container.inventory.capacity = _.configData.settings.BoxStorageSlots;

                        if (_.configData.settings.AddStorageAdapter)
                        {
                            IndustrialStorageAdaptor adapter = AddStorageAdaptor(planterBox, container, planterConfig.OutputAdapterPos, planterConfig.OutputAdapterRot);
                            if (adapter != null)
                            {
                                adapter.name = "NoPickUp";
                            }
                        }
                    }
                }

                if (containerSeeds == null)
                {
                    containerSeeds = GameManager.server.CreateEntity("assets/prefabs/deployable/hot air balloon/subents/hab_storage.prefab", planterConfig.InputStoragePos, planterConfig.InputStorageRot) as StorageContainer;
                    if (containerSeeds != null)
                    {
                        containerSeeds.SetParent(planterBox);

                        containerSeeds.name = "seedsBox";
                        containerSeeds.OwnerID = planterBox.OwnerID;
                        containerSeeds.Spawn();
                        SpawnRefresh(containerSeeds);

                        if (_.configData.settings.seedStorageSlots > 0)
                            containerSeeds.inventory.capacity = _.configData.settings.seedStorageSlots;

                        if (_.configData.settings.AddStorageAdapter)
                        {
                            IndustrialStorageAdaptor adapter = AddStorageAdaptor(planterBox, containerSeeds, planterConfig.InputAdapterPos, planterConfig.InputAdapterRot);
                            if (adapter != null)
                            {
                                adapter.name = "NoPickUp";
                            }
                        }
                    }
                }

                if (_.configData.settings.SprinklerNeedsWater && waterSource == null)
                {
                    Vector3 pos = planterBox.transform.TransformPoint(planterConfig.SplitterPos);
                    waterSource = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/fluidsplitter/fluidsplitter.prefab", pos, planterBox.transform.rotation) as Splitter;
                    if (waterSource != null)
                    {
                        SpawnRefresh(waterSource);
                        waterSource.OwnerID = planterBox.OwnerID;
                        waterSource.Spawn();
                    }
                }

                if (waterSource != null)
                {
                    waterSource.name = $"NoPickUp{planterBox.net.ID.Value}";
                    SpawnRefresh(waterSource);
                }

                if (sprinkler == null && _.configData.settings.AddSprinkler)
                {
                    sprinkler = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/sprinkler/electric.sprinkler.deployed.prefab", transform.TransformPoint(planterConfig.SprinklerPos), transform.rotation) as Sprinkler;
                    if (sprinkler != null)
                    {
                        sprinkler.OwnerID = planterBox.OwnerID;
                        sprinkler.Spawn();
                        sprinkler.DecayPerSplash = 0f;
                        //sprinkler.ConsumptionAmount();
                        SpawnRefresh(sprinkler);
                    }
                }

                if (sprinkler != null)
                {
                    sprinkler.DecayPerSplash = 0f;
                    SpawnRefresh(sprinkler);
                    if (!_.configData.settings.SprinklerNeedsWater)
                        InvokeRepeating("WaterPlants", 10, 30);
                    else if (waterSource != null && !sprinkler.IsConnectedToAnySlot(waterSource, 0, 3))
                    {
                        _.NextTick(() => connectWater(waterSource, sprinkler));
                    }
                    sprinkler.name = $"NoPickUp{planterBox.net.ID.Value}";
                }

                if (_.configData.settings.AddStorageAdapter && !fertilizerBoxAdapter)
                {
                    BaseEntity adaptersPoo = AddStorageAdaptor(planterBox, planterBox, planterConfig.FertilizerAdapterPos, Quaternion.identity);
                    adaptersPoo.name = "NoPickUp";
                    totalslotsAvailable++;
                }
                itemDefinition = ItemManager.FindItemDefinition("water");
                cropPlants();
            }

            private void connectWater(IOEntity entity, IOEntity entity1)
            {
                entity1.ClearConnections();
                _.NextTick(() =>
                {
                    if (entity == null || entity1 == null) return;
                    IOEntity.IOSlot ioOutput = entity.outputs[0];
                    if (ioOutput != null)
                    {
                        ioOutput.connectedTo = new IOEntity.IORef();
                        ioOutput.connectedTo.Set(entity1);
                        ioOutput.connectedToSlot = 0;
                        ioOutput.connectedTo.Init();

                        entity1.inputs[0].connectedTo = new IOEntity.IORef();
                        entity1.inputs[0].connectedTo.Set(entity);
                        entity1.inputs[0].connectedToSlot = 0;
                        entity1.inputs[0].connectedTo.Init();
                        entity.SendNetworkUpdateImmediate(true);
                        entity1.SendNetworkUpdateImmediate(true);
                    }
                });
            }

            private void WaterPlants()
            {
                if (sprinkler == null) return;

                if (planterBox.soilSaturation <= soilSaturationON && !sprinkler.IsOn())
                {
                    sprinkler.SetFuelType(itemDefinition, null);
                    //sprinkler.TurnOn();
                    sprinkler.SetFlag(BaseEntity.Flags.On, true);
                    sprinkler.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    sprinkler.SendNetworkUpdateImmediate(true);

                    this.forceUpdateSplashables = true;

                    this.InvokeRandomized(new Action(DoSplash), sprinkler.SplashFrequency * 0.5f, sprinkler.SplashFrequency, sprinkler.SplashFrequency * 0.2f);
                }
                else if (sprinkler.IsOn() && planterBox.soilSaturation >= soilSaturationOFF)
                {
                    sprinkler.TurnOff();
                    if (this.IsInvoking(new Action(DoSplash)))
                        this.CancelInvoke(new Action(DoSplash));
                }
            }

            private void cropPlants()
            {
                foreach (BaseEntity child in planterBox.children.ToList())
                {
                    if (child != null && child is GrowableEntity)
                        seeIfCanPick((child as GrowableEntity));
                }
            }

            public void seeIfCanPick(GrowableEntity growableEntity)
            {
                if (growableEntity == null) return;

                string sName = growableEntity.ShortPrefabName.Replace(".entity", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (!_.configData.settingsClone.enableCloning && cloningItem != null && _.configData.settingsClone.cloneList.ContainsKey(sName))
                {
                    if (growableEntity.skinID != 0UL)
                    {

                    }
                    else if (growableEntity.CanClone() && growableEntity.State.ToString().ToLower().Contains(_.configData.settingsClone.cloneList[sName].ToLower()))
                    {
                        CloneCrop(growableEntity);
                        return;
                    }
                }
                if (growableEntity.State == PlantProperties.State.Ripe || growableEntity.State == PlantProperties.State.Dying)
                {
                    pickCrop(growableEntity);
                }
                else autoFill();

                  //growableEntity.ChangeState(PlantProperties.State.Sapling, true, false); //test add
            }

            public bool findKnife()
            {
                foreach (var item in containerSeeds.inventory.itemList)
                {
                    if (_.configData.settingsClone.knifeList.ContainsKey(item.info.itemid))
                    {
                        cloningItem = item;
                        return true;
                    }                  
                }

                cloningItem = null;
                return false;
            }

            private void CloneCrop(GrowableEntity growableEntity)
            {
                int iAmount = growableEntity.Properties.BaseCloneCount + growableEntity.Genes.GetGeneTypeCount(GrowableGenetics.GeneType.Yield) / 2;

                if (cloningItem != null && _.configData.settingsClone.knifeList[cloningItem.info.itemid] > 1)
                {
                    iAmount = iAmount * _.configData.settingsClone.knifeList[cloningItem.info.itemid];
                }

                if (iAmount <= 0)
                    return;
                Item targetItem = ItemManager.Create(growableEntity.Properties.CloneItem, iAmount);
                GrowableGeneEncoding.EncodeGenesToItem(growableEntity, targetItem);

                Item itemEntity = growableEntity.GetItem();
                if (itemEntity != null && itemEntity.skin != 0UL)
                {
                    BasePlayer player = BasePlayer.Find(planterBox.OwnerID.ToString());
                    if (growableEntity is CollectibleEntity)
                    {
                        Interface.CallHook("OnCollectiblePickup", targetItem, player, growableEntity);
                    }
                    else
                    {
                        Interface.CallHook("OnGrowableGathered", growableEntity, targetItem, player);
                    }
                }

                if (!targetItem.MoveToContainer(container.inventory))
                {
                    Vector3 velocity = Vector3.zero;
                    targetItem.Drop(growableEntity.transform.position + new Vector3(0f, 2f, 1.5f), velocity);
                }
                if (growableEntity.Properties.pickEffect.isValid)
                    Effect.server.Run(growableEntity.Properties.pickEffect.resourcePath, growableEntity.transform.position, Vector3.up);

                if (growableEntity != null && !growableEntity.IsDestroyed) { growableEntity.Kill(); }
                _.NextTick(() => { autoFill(); });
            }

            private void pickCrop(GrowableEntity growableEntity)
            {
                if (containerSeeds == null || container == null) generateStorage();
                int amount = growableEntity.CurrentPickAmount;
                if (seeds.ContainsKey(growableEntity.Properties.SeedItem.itemid))
                    amount = growableEntity.CurrentPickAmount * seeds[growableEntity.Properties.SeedItem.itemid];
                if (amount <= 0) return;

                Item obj = ItemManager.Create(growableEntity.Properties.pickupItem, amount, 0UL);

                if (obj != null)
                {
                    Item itemEntity = growableEntity.GetItem();
                    if (_ != null && _.Ganja != null && _.configData.settings.GanjaPluginEnable)
                    {
                        if (growableEntity.prefabID == 3006540952)
                            Interface.CallHook("OnAutoFarmGather", planterBox.OwnerID.ToString(), container, growableEntity.transform.position, true);
                        else if (growableEntity.prefabID == 3587624038)
                            Interface.CallHook("OnAutoFarmGather", planterBox.OwnerID.ToString(), container, growableEntity.transform.position, false, growableEntity.Genes.GetGeneTypeCount(GrowableGenetics.GeneType.Hardiness));
                    }

                    if (itemEntity != null && itemEntity.skin != 0UL)
                    {
                        BasePlayer player = BasePlayer.Find(planterBox.OwnerID.ToString());
                        if (growableEntity is CollectibleEntity)
                        {
                            Interface.CallHook("OnCollectiblePickup", obj, player, growableEntity);
                        }
                        else
                        {
                            Interface.CallHook("OnGrowableGathered", growableEntity, obj, player);
                        }
                    }
                    else if (_.configData.settings.CallHookOnCollectiblePickup && planterBox != null && planterBox.OwnerID != 0UL)
                    {
                        BasePlayer player = BasePlayer.Find(planterBox.OwnerID.ToString());
                        if (player != null)
                        {
                            if (growableEntity is CollectibleEntity)
                            {
                                Interface.CallHook("OnCollectiblePickup", obj, player, growableEntity);
                            }
                            else
                            {
                                Interface.CallHook("OnGrowableGathered", growableEntity, obj, player);
                            }
                        }
                    }
                    if (!obj.MoveToContainer(container.inventory))
                    {
                        Vector3 velocity = Vector3.zero;

                        BasePlayer player = BasePlayer.Find(planterBox.OwnerID.ToString());
                        obj.Drop(growableEntity.transform.position + new Vector3(0f, 2f, 1.5f), velocity);
                    }
                    if (growableEntity.Properties.pickEffect.isValid)
                        Effect.server.Run(growableEntity.Properties.pickEffect.resourcePath, growableEntity.transform.position, Vector3.up);

                    if (growableEntity != null && !growableEntity.IsDestroyed) { growableEntity.Kill(); }

                    _.NextTick(() => { autoFill(); });
                }
            }

            public void autoFill()
            {
                int totalOpen = totalslotsAvailable - planterBox.children.Count;
                if (totalOpen > 0) isPlanterFull();
            }

            private bool checkSpawnPoint(Vector3 position, float size)
            {
                if (position == null)
                    position = new Vector3(4.8654f, 2.6241f, 1.3869f);
                List<GrowableEntity> nearby = new List<GrowableEntity>();
                Vis.Entities<GrowableEntity>(position, size, nearby);
                if (nearby.Distinct().ToList().Count > 0)
                    return true;
                return false;
            }

            private void isPlanterFull()
            {
                if (nextAutofiltime > UnityEngine.Time.realtimeSinceStartup)
                    return;

                if (containerSeeds == null || container == null)
                    generateStorage();

                if (planterBox == null || planterBox.IsDestroyed)
                    return;

                int freePlacement = totalslotsAvailable - planterBox.children.Count;

                if (freePlacement > 0)
                {
                    for (int slot1 = 0; slot1 < containerSeeds.inventory.capacity; ++slot1)
                    {
                        int totalPlacement = 0;
                        Item slot2 = containerSeeds.inventory.GetSlot(slot1);
                        if (slot2 != null && seeds.ContainsKey(slot2.info.itemid))
                        {
                            int amountToConsume = slot2.amount;
                            if (amountToConsume > 0)
                            {
                                if (freePlacement < amountToConsume)
                                    totalPlacement = freePlacement;
                                else totalPlacement = amountToConsume;
                                if (totalPlacement > 0)
                                    fillPlanter(slot2.info.itemid, totalPlacement, slot2);
                            }
                        }
                    }
                }

                nextAutofiltime = UnityEngine.Time.realtimeSinceStartup + 1f;
            }

            private void fillPlanter(int theID, int amount, Item item = null)
            {
                if (planterBox.links != null)
                {
                    var deployablePrefab = item.info.GetComponent<ItemModDeployable>()?.entityPrefab?.resourcePath;
                    if (string.IsNullOrEmpty(deployablePrefab)) return;

                    foreach (EntityLink socketBase in planterBox.links)
                    {
                        Socket_Base baseSocket = socketBase.socket;
                        if (baseSocket != null)
                        {
                            if (!baseSocket.female || planterBox.IsOccupied(baseSocket.socketName) || !IsFree(planterBox.transform.TransformPoint(baseSocket.worldPosition)))
                                continue;

                            GrowableEntity growable = GameManager.server.CreateEntity(deployablePrefab, planterBox.transform.position, Quaternion.identity) as GrowableEntity;
                            if (growable != null)
                            {
                                Item itemEntity = growable.GetItem();
                                if (itemEntity != null && item.skin != 0UL)
                                {
                                    itemEntity.skin = item.skin;
                                }

                                var idata = item?.instanceData;

                                growable.SetParent(planterBox, true);
                                growable.Spawn();

                                if (idata != null)
                                    growable.ReceiveInstanceData(idata);

                                growable.transform.localPosition = baseSocket.worldPosition;

                                planterBox.SendNetworkUpdateImmediate();
                                planterBox.SendChildrenNetworkUpdateImmediate();
                                amount--;
                                item?.UseItem(1);

                                Effect.server.Run("assets/prefabs/plants/plantseed.effect.prefab", planterBox.transform.TransformPoint(baseSocket.worldPosition), planterBox.transform.up);

                                if (itemEntity != null)
                                {
                                    Planner planer = itemEntity.GetHeldEntity() as Planner;
                                    if (planer != null)
                                    {
                                        Interface.CallHook("OnEntityBuilt", planer, planer.gameObject);
                                    }
                                }

                                if (amount <= 0)
                                    break;
                            }
                        }
                    }
                }
            }

            public bool IsFree(Vector3 position)
            {
                float distance = 0.1f;
                List<GrowableEntity> list = new List<GrowableEntity>();
                Vis.Entities<GrowableEntity>(position, distance, list);
                return list.Count <= 0;
            }

            private HashSet<ISplashable> cachedSplashables = new HashSet<ISplashable>();
            private TimeSince updateSplashableCache;
            private bool forceUpdateSplashables;

            private void DoSplash()
            {
                if (sprinkler == null)
                    return;

                using (TimeWarning.New("SprinklerSplash"))
                {
                    int waterPerSplash = sprinkler.WaterPerSplash;
                    if ((double)(float)this.updateSplashableCache > (double)sprinkler.SplashFrequency * 4.0 || this.forceUpdateSplashables)
                    {
                        this.cachedSplashables.Clear();
                        this.forceUpdateSplashables = false;
                        this.updateSplashableCache = (TimeSince)0.0f;
                        Vector3 position = sprinkler.Eyes.position;
                        Vector3 up = sprinkler.transform.up;
                        float num = ConVar.Server.sprinklerEyeHeightOffset * Mathf.Clamp(Vector3.Angle(up, Vector3.up) / 180f, 0.2f, 1f);
                        Vector3 startPosition = position + up * (splashRadius * 0.5f);
                        Vector3 vector3 = position + up * num;
                        List<BaseEntity> list1 = Facepunch.Pool.GetList<BaseEntity>();
                        Vector3 endPosition = vector3;
                        double sprinklerRadius = (double)splashRadius;
                        List<BaseEntity> list2 = list1;
                        Vis.Entities<BaseEntity>(startPosition, endPosition, (float)sprinklerRadius, list2, 1236478737);
                        if (list1.Count > 0)
                        {
                            foreach (BaseEntity baseEntity in list1)
                            {
                                ISplashable splashable6 = null;
                                IOEntity entity6 = null;

                                if (baseEntity is IOEntity)
                                    entity6 = baseEntity as IOEntity;

                              if (!baseEntity.isClient && baseEntity is ISplashable)
                                    splashable6 = baseEntity as ISplashable;

                                if (splashable6 != null && (!this.cachedSplashables.Contains(splashable6) && splashable6.WantsSplash(sprinkler.currentFuelType, waterPerSplash)) && (baseEntity.IsVisible(position) && (!(baseEntity is IOEntity) || entity6 != null && !sprinkler.IsConnectedTo(entity6, IOEntity.backtracking)))) this.cachedSplashables.Add(splashable6);
                            }
                        }
                        Facepunch.Pool.FreeList<BaseEntity>(ref list1);
                    }
                    if (this.cachedSplashables.Count > 0)
                    {
                        int amount = waterPerSplash / this.cachedSplashables.Count;
                        foreach (ISplashable cachedSplashable in this.cachedSplashables)
                        {
                            if (!cachedSplashable.IsUnityNull<ISplashable>() && cachedSplashable.WantsSplash(sprinkler.currentFuelType, amount))
                            {
                                int num = cachedSplashable.DoSplash(sprinkler.currentFuelType, amount);
                                waterPerSplash -= num;
                                if (waterPerSplash <= 0)
                                    break;
                            }
                        }
                    }
                }
                Interface.CallHook("OnSprinklerSplashed", (object)sprinkler);
            }

            public void OnDestroy()
            {
                if (_.playerTotals.ContainsKey(ownerplayer))
                    _.playerTotals[ownerplayer] = _.playerTotals[ownerplayer] - 1;

                CancelInvoke("WaterPlants");
                CancelInvoke("isPlanterFull");
                if (sprinkler != null && !_.configData.settings.SprinklerNeedsWater) sprinkler.TurnOff();
                if (planterBox == null || planterBox.IsDestroyed)
                {
                    if (sprinkler != null) sprinkler?.Kill();
                    if (waterSource != null) waterSource?.Kill();
                }
                ResetArtificialLightExposure();
                ResetArtificialTemperature();
            }

            public void RemoveEntitys()
            {
                CancelInvoke("WaterPlants");
                CancelInvoke("isPlanterFull");
                if (sprinkler != null) sprinkler?.Kill();
                if (waterSource != null) waterSource?.Kill();
            }
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