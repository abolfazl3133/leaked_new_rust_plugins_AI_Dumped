using System;
using System.Collections.Generic;
using System.Diagnostics;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using UnityEngine;
using System.Linq;
using ConVar;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust.Modular;
using Debug = UnityEngine.Debug;
using Physics = UnityEngine.Physics;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("Purchase Vehicles from the Shop", "M&B-Studios", "1.1.7")]
    class VehicleBuy : RustPlugin
    {
        private class LockedVehicleTracker
        {
            public Dictionary<VehicleInfo, HashSet<BaseEntity>> VehiclesWithLocksByType { get; } = new Dictionary<VehicleInfo, HashSet<BaseEntity>>();

            private readonly VehicleInfoManager _vehicleInfoManager;

            public LockedVehicleTracker(VehicleInfoManager vehicleInfoManager)
            {
                _vehicleInfoManager = vehicleInfoManager;
            }

            public void OnServerInitialized()
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    var baseEntity = entity as BaseEntity;
                    if (baseEntity == null)
                        continue;

                    var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(baseEntity);
                    if (vehicleInfo == null || GetVehicleLock(baseEntity) == null)
                        continue;

                    OnLockAdded(baseEntity);
                }
            }

            public void OnLockAdded(BaseEntity vehicle)
            {
                GetEntityListForVehicle(vehicle)?.Add(vehicle);
            }

            public void OnLockRemoved(BaseEntity vehicle)
            {
                GetEntityListForVehicle(vehicle)?.Remove(vehicle);
            }

            private HashSet<BaseEntity> EnsureEntityList(VehicleInfo vehicleInfo)
            {
                HashSet<BaseEntity> vehicleList;
                if (!VehiclesWithLocksByType.TryGetValue(vehicleInfo, out vehicleList))
                {
                    vehicleList = new HashSet<BaseEntity>();
                    VehiclesWithLocksByType[vehicleInfo] = vehicleList;
                }
                return vehicleList;
            }

            private HashSet<BaseEntity> GetEntityListForVehicle(BaseEntity entity)
            {
                var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(entity);
                if (vehicleInfo == null)
                    return null;

                return EnsureEntityList(vehicleInfo);
            }
        }
        private const float MaxDeployDistance = 3;
        private class VehicleInfo
        {
            public string VehicleType;
            public string[] PrefabPaths;
            public Vector3 LockPosition;
            public Quaternion LockRotation;
            public string ParentBone;

            public string CodeLockPermission { get; private set; }
            public string KeyLockPermission { get; private set; }
            public uint[] PrefabIds { get; private set; }

            public Func<BaseEntity, BaseEntity> DetermineLockParent = (entity) => entity;
            public Func<BaseEntity, float> TimeSinceLastUsed = (entity) => 0;

            public void OnServerInitialized(VehicleBuy pluginInstance)
            {

                if (!pluginInstance.permission.PermissionExists(CodeLockPermission, pluginInstance))
                    pluginInstance.permission.RegisterPermission(CodeLockPermission, pluginInstance);

                if (!pluginInstance.permission.PermissionExists(KeyLockPermission, pluginInstance))
                    pluginInstance.permission.RegisterPermission(KeyLockPermission, pluginInstance);

                // Custom vehicles aren't currently allowed to specify prefabs since they reuse existing prefabs.
                if (PrefabPaths != null)
                {
                    var prefabIds = new List<uint>();
                    foreach (var prefabName in PrefabPaths)
                    {
                        var prefabId = StringPool.Get(prefabName);
                        if (prefabId != 0)
                        {
                            prefabIds.Add(prefabId);
                        }
                        else
                        {
                        }
                    }

                    PrefabIds = prefabIds.ToArray();
                }
            }

            // In the future, custom vehicles may be able to pass in a method to override this.
            public bool IsMounted(BaseEntity entity)
            {
                var vehicle = entity as BaseVehicle;
                if (vehicle != null)
                    return vehicle.AnyMounted();

                var mountable = entity as BaseMountable;
                if (mountable != null)
                    return mountable.AnyMounted();

                return false;
            }
        }
        private class VehicleInfoManager
        {
            private readonly VehicleBuy _plugin;
            private readonly Dictionary<uint, VehicleInfo> _prefabIdToVehicleInfo = new Dictionary<uint, VehicleInfo>();
            private readonly Dictionary<string, VehicleInfo> _customVehicleTypes = new Dictionary<string, VehicleInfo>();

            public VehicleInfoManager(VehicleBuy plugin)
            {
                _plugin = plugin;
            }

            public void OnServerInitialized()
            {
                var allVehicles = new[]
                {
                    new VehicleInfo
                    {
                        VehicleType = "attackhelicopter",
                        PrefabPaths = new[] { "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab" },
                        LockPosition = new Vector3(-0.6f, 1.08f, 1.01f),
                        TimeSinceLastUsed = (vehicle) => Time.time - (vehicle as AttackHelicopter)?.lastEngineOnTime ?? Time.time,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "chinook",
                        PrefabPaths = new[] { "assets/prefabs/npc/ch47/ch47.entity.prefab" },
                        LockPosition = new Vector3(-1.175f, 2, 6.5f),
                        TimeSinceLastUsed = (vehicle) => Time.time - (vehicle as CH47Helicopter)?.lastPlayerInputTime ?? Time.time,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "duosub",
                        PrefabPaths = new[] { "assets/content/vehicles/submarine/submarineduo.entity.prefab" },
                        LockPosition = new Vector3(-0.455f, 1.29f, 0.75f),
                        LockRotation = Quaternion.Euler(0, 180, 10),
                        TimeSinceLastUsed = (vehicle) => (vehicle as SubmarineDuo)?.timeSinceLastUsed ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "hotairballoon",
                        PrefabPaths = new[] { "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab" },
                        LockPosition = new Vector3(1.45f, 0.9f, 0),
                        TimeSinceLastUsed = (vehicle) => Time.time - (vehicle as HotAirBalloon)?.sinceLastBlast ?? Time.time,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "kayak",
                        PrefabPaths = new[] { "assets/content/vehicles/boats/kayak/kayak.prefab" },
                        LockPosition = new Vector3(-0.43f, 0.2f, 0.2f),
                        LockRotation = Quaternion.Euler(0, 90, 90),
                        TimeSinceLastUsed = (vehicle) => (vehicle as Kayak)?.timeSinceLastUsed ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "locomotive",
                        PrefabPaths = new[] { "assets/content/vehicles/trains/locomotive/locomotive.entity.prefab" },
                        LockPosition = new Vector3(-0.11f, 2.89f, 4.95f),
                        TimeSinceLastUsed = (vehicle) => (vehicle as TrainEngine)?.decayingFor ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "magnetcrane",
                        PrefabPaths = new[] { "assets/content/vehicles/crane_magnet/magnetcrane.entity.prefab" },
                        LockPosition = new Vector3(-1.735f, -1.445f, 0.79f),
                        LockRotation = Quaternion.Euler(0, 0, 90),
                        ParentBone = "Top",
                        TimeSinceLastUsed = (vehicle) => Time.realtimeSinceStartup - (vehicle as MagnetCrane)?.lastDrivenTime ?? Time.realtimeSinceStartup,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "minicopter",
                        PrefabPaths = new[] { "assets/content/vehicles/minicopter/minicopter.entity.prefab" },
                        LockPosition = new Vector3(-0.15f, 0.7f, -0.1f),
                        TimeSinceLastUsed = (vehicle) => Time.time - (vehicle as Minicopter)?.lastEngineOnTime ?? Time.time,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "modularcar",
                        // There are at least 37 valid Modular Car prefabs.
                        PrefabPaths = FindPrefabsOfType<ModularCar>(),
                        LockPosition = new Vector3(-0.9f, 0.35f, -0.5f),
                        DetermineLockParent = (vehicle) => FindFirstDriverModule((ModularCar)vehicle),
                        TimeSinceLastUsed = (vehicle) => Time.time - (vehicle as ModularCar)?.lastEngineOnTime ?? Time.time,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "rhib",
                        PrefabPaths = new[] { "assets/content/vehicles/boats/rhib/rhib.prefab" },
                        LockPosition = new Vector3(-0.68f, 2.00f, 0.7f),
                        TimeSinceLastUsed = (vehicle) => (vehicle as RHIB)?.timeSinceLastUsedFuel ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "ridablehorse",
                        PrefabPaths = new[] { "assets/rust.ai/nextai/testridablehorse.prefab" },
                        LockPosition = new Vector3(-0.6f, 0.25f, -0.1f),
                        LockRotation = Quaternion.Euler(0, 95, 90),
                        ParentBone = "Horse_RootBone",
                        TimeSinceLastUsed = (vehicle) => Time.time - (vehicle as RidableHorse)?.lastInputTime ?? Time.time,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "rowboat",
                        PrefabPaths = new[] { "assets/content/vehicles/boats/rowboat/rowboat.prefab" },
                        LockPosition = new Vector3(-0.83f, 0.51f, -0.57f),
                        TimeSinceLastUsed = (vehicle) => (vehicle as MotorRowboat)?.timeSinceLastUsedFuel ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "scraptransport",
                        PrefabPaths = new[] { "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab" },
                        LockPosition = new Vector3(-1.25f, 1.22f, 1.99f),
                        TimeSinceLastUsed = (vehicle) => Time.time - (vehicle as ScrapTransportHelicopter)?.lastEngineOnTime ?? Time.time,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "sedan",
                        PrefabPaths = new[] { "assets/content/vehicles/sedan_a/sedantest.entity.prefab" },
                        LockPosition = new Vector3(-1.09f, 0.79f, 0.5f),
                    },
                    new VehicleInfo
                    {
                        VehicleType = "sedanrail",
                        PrefabPaths = new[] { "assets/content/vehicles/sedan_a/sedanrail.entity.prefab" },
                        LockPosition = new Vector3(-1.09f, 1.025f, -0.26f),
                        TimeSinceLastUsed = (vehicle) => (vehicle as TrainEngine)?.decayingFor ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "snowmobile",
                        PrefabPaths = new[] { "assets/content/vehicles/snowmobiles/snowmobile.prefab" },
                        LockPosition = new Vector3(-0.205f, 0.59f, 0.4f),
                        TimeSinceLastUsed = (vehicle) => (vehicle as Snowmobile)?.timeSinceLastUsed ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "solosub",
                        PrefabPaths = new[] { "assets/content/vehicles/submarine/submarinesolo.entity.prefab" },
                        LockPosition = new Vector3(0f, 1.85f, 0f),
                        LockRotation = Quaternion.Euler(0, 90, 90),
                        TimeSinceLastUsed = (vehicle) => (vehicle as BaseSubmarine)?.timeSinceLastUsed ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "tomaha",
                        PrefabPaths = new[] { "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab" },
                        LockPosition = new Vector3(-0.37f, 0.4f, 0.125f),
                        TimeSinceLastUsed = (vehicle) => (vehicle as Snowmobile)?.timeSinceLastUsed ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "tugboat",
                        PrefabPaths = new[] { "assets/content/vehicles/boats/tugboat/tugboat.prefab" },
                        LockPosition = new Vector3(0.065f, 6.8f, 4.12f),
                        LockRotation = Quaternion.Euler(0, 90, 60),
                        TimeSinceLastUsed = (vehicle) => (vehicle as Tugboat)?.timeSinceLastUsedFuel ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "workcart",
                        PrefabPaths = new[] { "assets/content/vehicles/trains/workcart/workcart.entity.prefab" },
                        LockPosition = new Vector3(-0.2f, 2.35f, 2.7f),
                        TimeSinceLastUsed = (vehicle) => (vehicle as TrainEngine)?.decayingFor ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "workcartaboveground",
                        PrefabPaths = new[] { "assets/content/vehicles/trains/workcart/workcart_aboveground.entity.prefab" },
                        LockPosition = new Vector3(-0.2f, 2.35f, 2.7f),
                        TimeSinceLastUsed = (vehicle) => (vehicle as TrainEngine)?.decayingFor ?? 0,
                    },
                    new VehicleInfo
                    {
                        VehicleType = "workcartcovered",
                        PrefabPaths = new[] { "assets/content/vehicles/trains/workcart/workcart_aboveground2.entity.prefab" },
                        LockPosition = new Vector3(-0.2f, 2.35f, 2.7f),
                        TimeSinceLastUsed = (vehicle) => (vehicle as TrainEngine)?.decayingFor ?? 0,
                    },
                };

                foreach (var vehicleInfo in allVehicles)
                {
                    vehicleInfo.OnServerInitialized(_plugin);
                    foreach (var prefabId in vehicleInfo.PrefabIds)
                    {
                        _prefabIdToVehicleInfo[prefabId] = vehicleInfo;
                    }
                }
            }

            public void RegisterCustomVehicleType(VehicleBuy pluginInstance, VehicleInfo vehicleInfo)
            {
                vehicleInfo.OnServerInitialized(pluginInstance);
                _customVehicleTypes[vehicleInfo.VehicleType] = vehicleInfo;
            }

            public VehicleInfo GetVehicleInfo(BaseEntity entity)
            {
                VehicleInfo vehicleInfo;
                if (_prefabIdToVehicleInfo.TryGetValue(entity.prefabID, out vehicleInfo))
                    return vehicleInfo;

                foreach (var customVehicleInfo in _customVehicleTypes.Values)
                {
                    if (customVehicleInfo.DetermineLockParent(entity) != null)
                        return customVehicleInfo;
                }

                return null;
            }

            public BaseEntity GetCustomVehicleParent(BaseEntity entity)
            {
                foreach (var vehicleInfo in _customVehicleTypes.Values)
                {
                    var lockParent = vehicleInfo.DetermineLockParent(entity);
                    if (lockParent != null)
                        return lockParent;
                }

                return null;
            }
        }
        public class PlaceMonuments : FacepunchBehaviour
        {
            private BasePlayer Player { get; set; }

            private float NextTime { get; set; }

            private bool d = false;

            private void Awake()
            {
                Player = GetComponent<BasePlayer>();

            }

            private void Update()
            {
                if (!Player)
                {
                    return;
                }

                if (Player.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    if (NextTime > Time.time)
                    {
                        return;
                    }

                    NextTime = Time.time + 1f;

                    Item activeItem = Player.GetActiveItem();
                    if (activeItem == null)
                        return;

                    if (!Instance.cfg.Vehicles.Any(x => x.Value.Skin == activeItem.skin))
                        return;

                    var vehicle = Instance.cfg.Vehicles.FirstOrDefault(x => x.Value.Skin == activeItem.skin);



                    var depl = activeItem?.GetHeldEntity().GetComponent<Planner>();

                    if (depl == null)
                        return;


                    var deployable = depl.GetModDeployable();
                    if (deployable == null)
                        return;

                    var ent = GameManager.server.CreateEntity(deployable.entityPrefab.resourcePath);

                    if (!ent)
                        return;

                    ent.Spawn();
                    ent.OwnerID = Player.userID;
                    ent.skinID = activeItem.skin;

                    ent.SendNetworkUpdateImmediate();
                    Interface.CallHook("OnEntityBuilt", Player.GetHeldEntity(), ent.transform.gameObject);
                    activeItem.UseItem(1);
                }
            }

            public void DoDestroy()
            {
                DestroyImmediate(this);
            }
        }


        private Dictionary<ulong, ulong> PRM = new();
        public static VehicleBuy Instance;

        internal class PlayerData
        {
            public Dictionary<string, int> Cooldowns = new();
        }

        internal struct VendingMachinePosition
        {
            public Vector3 Offset;
            public Transform transform;
            public Vector3 Rotation;

            public VendingMachinePosition(Transform position, Vector3 rotation, Vector3 offset)
            {
                this.transform = position;
                this.Rotation = rotation;
                this.Offset = offset;
            }
        }

        private Dictionary<ulong, PlayerData> _players = new();
        [PluginReference] private Plugin ImageLibrary, Economics, ServerRewards;
        private const string Prefab_CodeLock_DeployedEffect = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";
        private const string Prefab_CodeLock_DeniedEffect = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
        private const string Prefab_CodeLock_UnlockEffect = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
        private const string PERM_USE = "vehiclebuy.use";
        private const string PERM_PICKUP = "vehiclebuy.pickup";
        private const string PERM_RECALL = "vehiclebuy.recall";
        private const string PURCHASE_EFFECT = "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab";
        private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0);
        private static int CurrentTime() => (int)DateTime.UtcNow.Subtract(Epoch).TotalSeconds;
        private readonly VehicleInfoManager _vehicleInfoManager;
        private readonly LockedVehicleTracker _lockedVehicleTracker;
        private List<BaseEntity> VendingMachines = new List<BaseEntity>();

        private VendingMachinePosition BANDITCAMP_POSITION;
        private VendingMachinePosition OUTPOST_POSITION;
        private VendingMachinePosition fvA_POSITION;
        private VendingMachinePosition fvB_POSITION;
        private VendingMachinePosition fvC_POSITION;

        private MonumentInfo Outpost;
        private MonumentInfo BanditCamp;
        private List<MonumentInfo> FishingVillagesA = new();
        private List<MonumentInfo> FishingVillagesB = new();
        private List<MonumentInfo> FishingVillagesC = new();

        private const string VENDINGMACHINE_PREFAB = @"assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab";

        #region Base Hooks
        private bool CanPlayerBypassLock(BasePlayer player, BaseLock baseLock, bool provideFeedback)
        {
            var hookResult = Interface.CallHook("CanUseLockedEntity", player, baseLock);
            if (hookResult is bool)
                return (bool)hookResult;

            var canAccessLock = IsPlayerAuthorizedToLock(player, baseLock);

            if (canAccessLock)
            {
                if (provideFeedback && !(baseLock is KeyLock))
                {
                    Effect.server.Run(Prefab_CodeLock_UnlockEffect, baseLock, 0, Vector3.zero, Vector3.forward);
                }

                return true;
            }

            if (provideFeedback)
            {
                Effect.server.Run(Prefab_CodeLock_DeniedEffect, baseLock, 0, Vector3.zero, Vector3.forward);
            }

            return false;
        }
        private static VehicleModuleSeating FindFirstDriverModule(ModularCar car)
        {
            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                BaseVehicleModule module;
                if (car.TryGetModuleAt(socketIndex, out module))
                {
                    var seatingModule = module as VehicleModuleSeating;
                    if (seatingModule != null && seatingModule.HasADriverSeat())
                        return seatingModule;
                }
            }
            return null;
        }
        public VehicleBuy()
        {
            _vehicleInfoManager = new VehicleInfoManager(this);
            _lockedVehicleTracker = new LockedVehicleTracker(_vehicleInfoManager);
        }
        private void OnServerInitialized()
        {
            Outpost = TerrainMeta.Path.Monuments.FirstOrDefault(p => p.name.ToLower().Contains("monument/medium/compound"));
            BanditCamp = TerrainMeta.Path.Monuments.FirstOrDefault(p => p.name.ToLower().Contains("monument/medium/bandit_town"));

            BANDITCAMP_POSITION = new VendingMachinePosition
            {
                Offset = new Vector3(-53.28f, 2f, 28.72f),
                transform = BanditCamp?.transform ?? null,
                Rotation = new Vector3(0, 51.4f, 0)
            };

            OUTPOST_POSITION = new VendingMachinePosition
            {
                Offset = new Vector3(32f, 1.51f, -15.34f),
                transform = Outpost?.transform ?? null,
                Rotation = new Vector3(0, 270f, 0)
            };

            fvA_POSITION = new()
            {
                Offset = new Vector3(1.2f, 2f, 4.48f),
                Rotation = new Vector3(0, 180f, 0)
            };
            fvB_POSITION = new()
            {
                Offset = new Vector3(-10.04f, 2f, 20.58f),
                Rotation = new Vector3(0, 270f, 0)
            };
            fvC_POSITION = new()
            {
                Offset = new Vector3(-8.72f, 2f, 12.23f),
                Rotation = new Vector3(0, 90f, 0)
            };


            var fvA = TerrainMeta.Path.Monuments.Where(p =>
                p.name.ToLower().Contains("monument/fishing_village/fishing_village_a"));
            var fvB = TerrainMeta.Path.Monuments.Where(p =>
                p.name.ToLower().Contains("monument/fishing_village/fishing_village_b"));
            var fvC = TerrainMeta.Path.Monuments.Where(p =>
                p.name.ToLower().Contains("monument/fishing_village/fishing_village_c"));


            if (fvA.Any())
                FishingVillagesA.AddRange(fvA);
            else
            {
                PrintError("Fishing villages A not found at map");
            }
            if (fvB.Any())
                FishingVillagesB.AddRange(fvB);
            else
            {
                PrintError("Fishing villages B not found at map");
            }
            if (fvC.Any())
                FishingVillagesC.AddRange(fvC);
            else
            {
                PrintError("Fishing villages C not found at map");
            }


            if (Outpost == null)
            {
                PrintError("Outpost not found at the map!");
            }
            if (BanditCamp == null)
            {
                PrintError("Bandit Camp not found at the map!");
            }

            permission.RegisterPermission(PERM_USE, this);
            permission.RegisterPermission(PERM_PICKUP, this);
            permission.RegisterPermission(PERM_RECALL, this);
            foreach (var x in cfg.Commands)
                cmd.AddChatCommand(x, this, cmdVehicleBuy);

            RegisterCommands();

            foreach (var x in cfg.Vehicles)
            {
                ImageLibrary.Call("AddImage", x.Value.Image, x.Key + ".image");
            }

            foreach (var x in cfg.Vehicles.Where(x => !string.IsNullOrEmpty(x.Value.Permission))
                         .Select(x => x.Value.Permission))
            {
                permission.RegisterPermission(x, this);
            }

            foreach (var x in BasePlayer.activePlayerList)
                x.gameObject.AddComponent<PlaceMonuments>();

            Instance = this;


            _vehicleInfoManager.OnServerInitialized();
            _lockedVehicleTracker.OnServerInitialized();

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var entity = networkable as BaseEntity;
                if ((object)entity == null)
                    continue;

                var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(entity);
                if (vehicleInfo == null)
                    continue;

                var lockEntity = GetVehicleLock(entity);
                if (lockEntity == null)
                    continue;

                var transform = lockEntity.transform;
                transform.localPosition = vehicleInfo.LockPosition;
                transform.localRotation = vehicleInfo.LockRotation;
                lockEntity.SendNetworkUpdate_Position();
            }

            Subscribe(nameof(OnEntityKill));

            // timer.Once(10f, () =>
            // {
            SpawnVendingMachines();
            // });
        }

        private void Unload()
        {
            DestroyVendingMachines();
            foreach (var x in BasePlayer.activePlayerList)
                x.gameObject.GetComponent<PlaceMonuments>()?.DoDestroy();

            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(basePlayer, Layer);
            }

            Instance = null;
        }

        #endregion

        #region Functions

        [ChatCommand("getposfva")]
        private void cmdGetPosfva(BasePlayer player)
        {
            if (!player.IsAdmin)
                return;

            var pos = player.GetNetworkPosition();
            var fva = FishingVillagesA.FirstOrDefault();
            // player.Teleport(fva.transform.position);

            player.ChatMessage($"Monument pos = {fva.transform.position}");
            player.ChatMessage($"Local to mon = {fva.transform.InverseTransformPoint(player.GetNetworkPosition())}");
            player.ChatMessage($"Global = X - {pos.x}, Y - {pos.y}, Z - {pos.z}");
            player.ChatMessage($"qu - {player.GetNetworkRotation()}");
        }
        [ChatCommand("getposfvb")]
        private void cmdGetPosfvb(BasePlayer player)
        {
            if (!player.IsAdmin)
                return;

            var pos = player.GetNetworkPosition();
            var fva = FishingVillagesB.FirstOrDefault();
            // player.Teleport(fva.transform.position);

            player.ChatMessage($"Monument pos = {fva.transform.position}");
            player.ChatMessage($"Local to mon = {fva.transform.InverseTransformPoint(player.GetNetworkPosition())}");
            player.ChatMessage($"Global = X - {pos.x}, Y - {pos.y}, Z - {pos.z}");
            player.ChatMessage($"qu - {player.GetNetworkRotation()}");
        }
        [ChatCommand("getposfvc")]
        private void cmdGetPosfvc(BasePlayer player)
        {
            if (!player.IsAdmin)
                return;

            var pos = player.GetNetworkPosition();
            var fva = FishingVillagesC.FirstOrDefault();
            // player.Teleport(fva.transform.position);

            player.ChatMessage($"Monument pos = {fva.transform.position}");
            player.ChatMessage($"Local to mon = {fva.transform.InverseTransformPoint(player.GetNetworkPosition())}");
            player.ChatMessage($"Global = X - {pos.x}, Y - {pos.y}, Z - {pos.z}");
            player.ChatMessage($"qu - {player.GetNetworkRotation()}");
        }
        [ChatCommand("getposoutpost")]
        private void cmdGetPosOP(BasePlayer player)
        {
            if (!player.IsAdmin)
                return;

            var pos = player.GetNetworkPosition();

            player.ChatMessage($"Monument pos = {Outpost.transform.position}");
            player.ChatMessage($"Local to mon = {Outpost.transform.InverseTransformPoint(player.GetNetworkPosition())}");
            player.ChatMessage($"Global = X - {pos.x}, Y - {pos.y}, Z - {pos.z}");
            player.ChatMessage($"qu - {player.GetNetworkRotation()}");
        }
        [ChatCommand("getposbandit")]
        private void cmdGetPosBC(BasePlayer player)
        {
            if (!player.IsAdmin)
                return;
            var pos = player.GetNetworkPosition();
            player.ChatMessage($"Monument pos = {BanditCamp.transform.position}");
            player.ChatMessage($"Local to mon = {BanditCamp.transform.InverseTransformPoint(player.GetNetworkPosition())}");
            player.ChatMessage($"Global = X - {pos.x}, Y - {pos.y}, Z - {pos.z}");
            player.ChatMessage($"qu - {player.GetNetworkRotation()}");
        }
        object OnGiveSoldItem(VendingMachine vending, Item soldItem, BasePlayer buyer)
        {
            if (!VMProducts.ContainsKey(vending.net.ID))
                return null;

            var vmopt = VMProducts[vending.net.ID];

            if (!vmopt.Any(x => x.RandomShortname == soldItem.info.shortname))
                return null;

            var item = vmopt.FirstOrDefault(x => x.RandomShortname == soldItem.info.shortname);

            var itemToGive = ItemManager.CreateByItemID(item.Get().DeployableItemId, soldItem.amount, item.Get().Skin);

            if (!string.IsNullOrEmpty(item.Get().Name))
                itemToGive.name = item.Get().Name;

            buyer.GiveItem(itemToGive, BaseEntity.GiveItemReason.Generic);

            return false;
        }
        object CanAdministerVending(BasePlayer player, VendingMachine machine)
        {
            if (VMProducts.ContainsKey(machine.net.ID))
                return false;
            return null;
        }
        private void SpawnVendingMachines()
        {
            if (cfg.VendingMachines.BanditCampSpawnMachine && BanditCamp)
            {
                var machine = SpawnVendingMachine(BANDITCAMP_POSITION);
                if (!machine.TryGetComponent<VendingMachine>(out var comp))
                {
                    GameObject.Destroy(machine.gameObject);
                    return;
                }

                SetupVendingMachine(machine, comp, cfg.VendingMachines.BanditCampOrders);
            }

            if (cfg.VendingMachines.OutpostSpawnMachine && Outpost)
            {
                var machine = SpawnVendingMachine(OUTPOST_POSITION);
                // BasePlayer.activePlayerList.First().Teleport(machine.transform.position);
                if (!machine.TryGetComponent<VendingMachine>(out var comp))
                {
                    GameObject.Destroy(machine.gameObject);
                    return;
                }

                SetupVendingMachine(machine, comp, cfg.VendingMachines.OutpostOrders);
            }





            if (cfg.VendingMachines.FishingVillageASpawnMachine && FishingVillagesA.Any())
            {
                foreach (var x in FishingVillagesA)
                {
                    var machine = SpawnVendingMachine(fvA_POSITION, x.transform);

                    if (!machine.TryGetComponent<VendingMachine>(out var comp))
                    {

                        GameObject.Destroy(machine.gameObject);
                        return;
                    }

                    SetupVendingMachine(machine, comp, cfg.VendingMachines.FishingVillageAOrders);
                }
            }
            if (cfg.VendingMachines.FishingVillageBSpawnMachine && FishingVillagesB.Any())
            {
                foreach (var x in FishingVillagesB)
                {
                    var machine = SpawnVendingMachine(fvB_POSITION, x.transform);

                    if (!machine.TryGetComponent<VendingMachine>(out var comp))
                    {

                        GameObject.Destroy(machine.gameObject);
                        return;
                    }

                    SetupVendingMachine(machine, comp, cfg.VendingMachines.FishingVillageBOrders);
                }
            }
            if (cfg.VendingMachines.FishingVillageBSpawnMachine && FishingVillagesB.Any())
            {
                foreach (var x in FishingVillagesC)
                {
                    var machine = SpawnVendingMachine(fvC_POSITION, x.transform);

                    if (!machine.TryGetComponent<VendingMachine>(out var comp))
                    {

                        GameObject.Destroy(machine.gameObject);
                        return;
                    }

                    SetupVendingMachine(machine, comp, cfg.VendingMachines.FishingVillageCOrders);
                }
            }
        }
        private Effect reusableSoundEffectInstance = new Effect();

        private void SendEffect(BasePlayer player, string effect)
        {
            reusableSoundEffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.zero, Vector3.forward, player.limitNetworking ? player.Connection : null);
            reusableSoundEffectInstance.pooledString = effect;
            if (string.IsNullOrEmpty(reusableSoundEffectInstance.pooledString))
            {
                return;
            }
            if (player.limitNetworking)
            {
                EffectNetwork.Send(reusableSoundEffectInstance, player.Connection);
            }
            else EffectNetwork.Send(reusableSoundEffectInstance);
        }
        private void SetupVendingMachine(BaseEntity machine, VendingMachine comp, List<VMOrder> items)
        {
            comp.shopName = "VEHICLE SHOP";
            Dictionary<string, string> usedShortnames = new Dictionary<string, string>();

            foreach (var item in items)
            {
                if (!cfg.Vehicles.ContainsKey(item.VehicleKey))
                    continue;
                var vehicledef = ItemManager.FindItemDefinition(cfg.Vehicles[item.VehicleKey].Shortname);
                var pricedef = ItemManager.FindItemDefinition(item.Shortname);

                var randDef =
                    ItemManager.itemList.FirstOrDefault(x => !usedShortnames.ContainsKey(x.shortname));

                usedShortnames.Add(randDef.shortname, item.VehicleKey);
                comp.sellOrders.sellOrders.Add(new ProtoBuf.VendingMachine.SellOrder
                {
                    itemToSellID = randDef.itemid,
                    itemToSellAmount = 1,
                    currencyID = pricedef.itemid,
                    currencyAmountPerItem = item.Price,
                    inStock = 10000,
                    currencyIsBP = false,
                    itemToSellIsBP = false,
                    itemCondition = 100,
                    itemConditionMax = 100
                });
            }

            foreach (var x in usedShortnames)
            {
                var item = ItemManager.CreateByName(x.Key, 10000, cfg.Vehicles[x.Value].Skin);

                item.MoveToContainer(comp.inventory);
            }

            VMProducts.Add(machine.net.ID, usedShortnames.Select(x => new VendingMachineCache
            {
                RandomShortname = x.Key,
                VehicleKey = x.Value
            }));
            VendingMachines.Add(machine);
        }
        internal class VendingMachineCache
        {
            public string RandomShortname;
            public string VehicleKey;

            public VehicleInfoConfig Get() => Instance.cfg.Vehicles[VehicleKey];
        }

        private Dictionary<NetworkableId, IEnumerable<VendingMachineCache>> VMProducts = new();

        private void DestroyVendingMachines()
        {
            VendingMachines.ForEach(x => x.Kill());
        }

        private BaseEntity SpawnVendingMachine(VendingMachinePosition position) => SpawnVendingMachine(position, position.transform);
        private BaseEntity SpawnVendingMachine(VendingMachinePosition position, Transform transform)
        {
            BaseEntity entity = (BaseEntity)GameManager.server.CreateEntity(VENDINGMACHINE_PREFAB, transform.TransformPoint(position.Offset));
            if (entity.TryGetComponent<StabilityEntity>(out var comp))
                comp.grounded = true;

            entity.OwnerID = 92929294944;
            entity.Spawn();
            entity.transform.rotation = transform.rotation * Quaternion.Euler(position.Rotation);

            return entity;
        }
        private static string[] FindPrefabsOfType<T>() where T : BaseEntity
        {
            var prefabList = new List<string>();

            foreach (var assetPath in GameManifest.Current.entities)
            {
                var entity = GameManager.server.FindPrefab(assetPath)?.GetComponent<T>();
                if (entity == null)
                    continue;

                prefabList.Add(entity.PrefabName);
            }

            return prefabList.ToArray();
        }

        private void SetCooldown(BasePlayer player, string key, int cooldownTime)
        {
            _players.TryAdd(player.userID, new PlayerData());

            _players[player.userID].Cooldowns.TryAdd(key, 0);

            _players[player.userID].Cooldowns[key] = CurrentTime() + cooldownTime;
        }

        private int GetCooldown(BasePlayer player, string key)
        {
            _players.TryAdd(player.userID, new PlayerData());

            if (_players[player.userID].Cooldowns.TryGetValue(key, out var cooldown))
                return cooldown - CurrentTime();

            return -1;
        }

        private bool IsCooldown(BasePlayer player, string key) => GetCooldown(player, key) > 0;

        private int ItemsCount(BasePlayer player, string shortname)
        {
            return player.inventory.GetAmount(ItemManager.FindItemDefinition(shortname).itemid);
        }

        private bool CanBuy(BasePlayer player, VehicleInfoConfig product)
        {
            if (!player.IPlayer.HasPermission(PERM_USE))
                return false;

            return product.SellCurrency switch
            {
                0 => ItemsCount(player, product.Shortname) >= product.Price,
                1 => GetBalance(player, product) >= product.Price,
                2 => GetBalance(player, product) >= product.Price,
                _ => false
            };
        }

        private bool Collect(BasePlayer player, VehicleInfoConfig product)
        {
            if (!CanBuy(player, product))
                return false;

            switch (product.SellCurrency)
            {
                case 0:
                    player.inventory.Take(null, ItemManager.FindItemDefinition(product.Shortname).itemid, product.Price);
                    return true;
                case 1:
                    Withdraw(player, product.Price, 1, product.Shortname);
                    return true;
                case 2:
                    Withdraw(player, product.Price, product.SellCurrency, product.Shortname);
                    return true;
                default:
                    return false;
            }
        }

        private string GetRemainingCost(BasePlayer player, VehicleInfoConfig product)
        {
            return product.SellCurrency switch
            {
                0 => product.Price - ItemsCount(player, product.Shortname) + " " +
                     ItemManager.FindItemDefinition(product.Shortname).displayName.english.ToUpper(),
                1 => product.Price - GetBalance(player, product) + " " + cfg.CurrencyName,
                2 => product.Price - GetBalance(player, product) + " " + cfg.CurrencyNameSR,
                _ => ""
            };
        }

        private int GetBalance(BasePlayer player, VehicleInfoConfig product)
        {
            if (product.SellCurrency == 1 && !Economics)
            {
                PrintError("Economics plugin is not available!");
                return -1;
            }
            if (product.SellCurrency == 2 && !ServerRewards)
            {
                PrintError("ServerRewards plugin is not available!");
                return -1;
            }

            return product.SellCurrency switch
            {
                0 => ItemsCount(player, product.Shortname),
                1 => Convert.ToInt32(Economics?.Call<double>("Balance", player.userID.Get())),
                2 => (int)ServerRewards?.Call("CheckPoints", player.userID.Get()),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void Withdraw(BasePlayer player, int price, int type, string shortname)
        {
            switch (type)
            {
                case 0:
                    if (string.IsNullOrEmpty(shortname))
                    {
                        PrintError("Shortname in 'Withdraw' is null!");
                        return;
                    }
                    player.inventory.Take(null, ItemManager.FindItemDefinition(shortname).itemid, price);
                    break;
                case 1:
                    Economics?.Call("Withdraw", player.userID.Get(), (double)price);
                    break;
                case 2:
                    ServerRewards?.Call("TakePoints", player.userID.Get(), price);
                    break;
            }
        }
        private string GetCost(string key)
        {
            if (!cfg.Vehicles.ContainsKey(key))
                return "";

            var product = cfg.Vehicles.FirstOrDefault(x => x.Key == key);

            switch (product.Value.SellCurrency)
            {
                case 0:
                    {
                        var itemDef = ItemManager.FindItemDefinition(product.Value.Shortname);

                        return itemDef == null ? "UNKNOWN ITEM" : $"{product.Value.Price} {itemDef.displayName.english.ToUpper()}";
                    }
                case 1:
                    return $"{product.Value.Price} {cfg.CurrencyName}";
                case 2:
                    return $"{product.Value.Price} {cfg.CurrencyNameSR}";
                default:
                    return "ERROR CHECK CFG";
            }
        }

        public void RegisterCommands()
        {
            foreach (var vehicle in cfg.Vehicles)
            {
                AddCovalenceCommand(vehicle.Value.Command, nameof(CmdAddVehicle));
            }
        }

        bool GiveVehicle(ItemContainer container, VehicleInfoConfig vehicleSettings, bool needFuel = true)
        {
            var item = ItemManager.CreateByItemID(vehicleSettings.DeployableItemId, 1, vehicleSettings.Skin);
            item.name = vehicleSettings.Name;
            return item.MoveToContainer(container, -1, false);
        }

        private void GetOutParts(ModularCar car)
        {
            foreach (var child in car.children)
            {
                var engineModule = child as VehicleModuleEngine;
                if (engineModule == null)
                    continue;

                var engineStorage = engineModule.GetContainer() as EngineStorage;
                if (engineStorage == null)
                    continue;

                engineStorage.inventory.Clear();
                engineStorage.SendNetworkUpdate();
            }
        }
        private void AddEngineParts(ModularCar car, List<string> shortnames)
        {
            foreach (var child in car.children)
            {
                var engineModule = child as VehicleModuleEngine;
                if (engineModule == null)
                    continue;

                var engineStorage = engineModule.GetContainer() as EngineStorage;
                if (engineStorage == null || !engineStorage.inventory.IsEmpty())
                    continue;

                foreach (var x in shortnames)
                {
                    AddPartsToEngineStorage(engineStorage, x);
                }
                engineModule.RefreshPerformanceStats(engineStorage);
            }
        }

        private void AddPartsToEngineStorage(EngineStorage engineStorage, string shortname)
        {
            if (engineStorage.inventory == null)
                return;

            var inventory = engineStorage.inventory;
            for (var i = 0; i < inventory.capacity; i++)
            {
                var item = inventory.GetSlot(i);
                if (item != null)
                    continue;

                // if (tier > 0)
                // {
                TryAddEngineItem(engineStorage, -1, shortname);
                // }
            }
        }

        private bool TryAddEngineItem(EngineStorage engineStorage, int slot, string shortname)
        {

            var component = ItemManager.FindItemDefinition(shortname);
            var item = ItemManager.Create(component);
            if (item == null)
                return false;

            item.MoveToContainer(engineStorage.inventory, slot, allowStack: false);
            return true;
        }
        bool GiveVehicle(BasePlayer player, VehicleInfoConfig vehicleSettings, bool needfuel = true)
        {
            var item = ItemManager.CreateByItemID(vehicleSettings.DeployableItemId, 1, vehicleSettings.Skin);
            item.name = vehicleSettings.Name + (needfuel ? "" : "   ");
            if (!player.inventory.GiveItem(item))
            {
                item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());
                return false;
            }
            return true;
        }

        private BaseEntity GetMinDistance(Vector3 position, IEnumerable<BaseEntity> entities)
        {
            BaseEntity result = null;
            float min = float.PositiveInfinity;

            BaseEntity[] ents = entities.ToArray();

            foreach (var t in ents)
            {
                var dist = Vector3.Distance(position, t.transform.position);

                if (dist < min)
                {
                    result = t;
                    min = dist;
                }
            }

            return result;
        }

        private bool HasInConfig(BaseEntity entity, string key)
        {
            return cfg.Vehicles.Any(x => x.Key == key && x.Value.Skin == entity.skinID && x.Value.Prefab == entity.PrefabName && x.Value.CanCallback);
        }

        #endregion

        #region UI

        private const string Layer = "ui.VehicleBuy.bg";

        private void UI_DrawMain(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.18 0.19 0.29 1.00", Material = "assets/icons/greyout.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-421.59 -233.87", OffsetMax = "421.59 233.43" }
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                Button = { Color = "0.50 0.50 1.00 1.00", Sprite = "assets/icons/close.png", Close = Layer },
                Text = { Text = "" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "389.186 202.986", OffsetMax = "414 227.8" }
            }, Layer, Layer + ".close");

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);

            UI_DrawVehicles(player);
            UI_DrawPages(player);
        }

        private int GetPages() => Mathf.CeilToInt(cfg.Vehicles.Count(x => x.Value.Show) / 8f);

        private void UI_DrawPages(BasePlayer player, int page = 0)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = Layer + ".page.number",
                Parent = Layer,
                Components = {
                    new CuiTextComponent { Text = $"{page + 1} von {GetPages()}", Font = "robotocondensed-bold.ttf", FontSize = 27, Align = TextAnchor.MiddleCenter, Color = "0.8078431 0.7803922 0.7411765 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-206.107 175.994", OffsetMax = "206.107 227.8" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.50 0.50 0.75 0.95", Command = page - 1 >= 0 ? $"vb_page {page - 1}" : "" },
                Text =
                {
                    Text = "<", Font = "robotocondensed-bold.ttf", FontSize = 43, Align = TextAnchor.MiddleCenter,
                    Color = page - 1 >= 0 ? "0.22 0.22 0.29 1.00" : "0.22 0.22 0.29 1.00"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-421.585 -29.046",
                    OffsetMax = "-371.875 29.046"
                }
            }, Layer, Layer + ".page.previous");

            container.Add(new CuiButton
            {
                Button = { Color = "0.50 0.50 0.75 0.95", Command = cfg.Vehicles.Where(x => x.Value.Show && x.Value.CanPlayerBuy(player)).Skip((page + 1) * 8).Take(8).Any() ? $"vb_page {page + 1}" : "" },
                Text =
                {
                    Text = ">", Font = "robotocondensed-bold.ttf", FontSize = 43, Align = TextAnchor.MiddleCenter,
                    Color = cfg.Vehicles.Where(x => x.Value.Show && x.Value.CanPlayerBuy(player)).Skip((page + 1) * 8).Take(8).Any() ? "0.22 0.22 0.29 0.92" : "0.67 0.57 0.93 1.00"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "371.875 -29.046",
                    OffsetMax = "421.585 29.046"
                }
            }, Layer, Layer + ".page.next");

            CuiHelper.DestroyUi(player, Layer + ".page.number");
            CuiHelper.DestroyUi(player, Layer + ".page.previous");
            CuiHelper.DestroyUi(player, Layer + ".page.next");
            CuiHelper.AddUi(player, container);
        }



        private void UI_DrawVehicles(BasePlayer player, int page = 0)
        {
            var container = new CuiElementContainer();

            float minx = -325.5007f;
            float maxx = -179.3994f;
            float miny = 17.5618f;
            float maxy = 163.6622f;
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-353.502 -163.662", OffsetMax = "353.502 163.662" }
            }, Layer, Layer + ".products.div");
            var vehicles = cfg.Vehicles.OrderBy(x => x.Value.Order).Where(x => x.Value.Show && x.Value.CanPlayerBuy(player)).Skip(page * 8).Take(8).ToArray();


            for (int i = 0; i < vehicles.Length; i++)
            {
                if (i % 4 == 0 && i > 0)
                {
                    minx = -325.5007f;
                    maxx = -179.3994f;
                    miny -= 163.662f;
                    maxy -= 163.662f;
                }
                var product = vehicles[i];

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.29 0.31 0.49 0.95" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}", OffsetMax = $"{maxx} {maxy}" }
                }, Layer + ".products.div", Layer + ".products.div" + $".product.{i}");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.40 0.37 0.71 1.00", Command = $"vb_buy {product.Key}", Material = "assets/icons/greyout.mat" },
                    Text = { Text = "BUY", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1.00 1.00 1.00 1.00" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-73.05 -73.05", OffsetMax = "73.05 -37.766" }
                }, Layer + ".products.div" + $".product.{i}", Layer + ".products.div" + $".product.{i}" + ".buy");

                container.Add(new CuiElement
                {
                    Name = Layer + ".products.div" + $".product.{i}" + ".image",
                    Parent = Layer + ".products.div" + $".product.{i}",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary.Call("GetImage", product.Key + ".image") },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-42.099 -15.997", OffsetMax = "42.098 68.2" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = Layer + ".products.div" + $".product.{i}" + ".cost",
                    Parent = Layer + ".products.div" + $".product.{i}",
                    Components = {
                        new CuiTextComponent { Text = GetCost(product.Key), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.8078431 0.7803922 0.7411765 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.014 -37.766", OffsetMax = "64.014 -15.996" }
                    }
                });


                minx += 163.05f;
                maxx += 163.05f;
            }

            CuiHelper.DestroyUi(player, Layer + ".products.div");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Commands

        [ChatCommand("callback")]
        private void cmdCallback(BasePlayer player, string command, string[] args)
        {
            if (!player.IPlayer.HasPermission(PERM_RECALL))
                return;

            if (args.IsNullOrEmpty())
            {
                player.ChatMessage("Usage: /callback vehicleName");
                return;
            }

            string name = args[0];

            var playerEntities = BaseNetworkable.serverEntities.OfType<BaseEntity>().Where(x => x.OwnerID == player.userID && HasInConfig(x, name));
            if (playerEntities == null || !playerEntities.Any())
            {
                player.ChatMessage("No vehicle to callback!");
                return;
            }
            var entity = GetMinDistance(player.transform.position, playerEntities);
            if (entity == null)
            {
                player.ChatMessage("No vehicle to callback!");
                return;
            }
            var product = cfg.Vehicles
                .FirstOrDefault(x => x.Value.Prefab == entity.PrefabName && x.Value.Skin == entity.skinID).Value;

            var balance = GetBalance(player, product);

            if (product.RecallCostNeed)
            {
                if (balance < product.RecallCost)
                {
                    player.ChatMessage($"Not enough balance! Need - {product.RecallCost - GetBalance(player, product)}");
                    return;
                }

                Withdraw(player, product.RecallCost, product.SellCurrency, product.Shortname);
            }

            Vector3 newCarPosition = new Vector3(player.transform.position.x + UnityEngine.Random.Range(3f, 5f), 0, player.transform.position.z + UnityEngine.Random.Range(3f, 5f));
            newCarPosition.y = TerrainMeta.HeightMap.GetHeight(newCarPosition) + 1f;

            entity.transform.position = newCarPosition;
            entity.SendNetworkUpdate();
            player.ChatMessage($"Vehicle {product.Name} was recalled");
        }

        [ChatCommand("pickup")]
        private void cmdPickup(BasePlayer player)
        {
            if (!player.IPlayer.HasPermission(PERM_PICKUP))
                return;

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, cfg.PickupRadius, -1))
            {
                var rhEntity = hit.GetEntity();
                if (rhEntity != null)
                {
                    if (rhEntity.name.Contains("module_entities"))
                        rhEntity = rhEntity.parentEntity.Get(true);

                    if (rhEntity.OwnerID != player.userID)
                    {
                        player.ChatMessage("Vehicle not found");
                        return;
                    }

                    var product = cfg.Vehicles.FirstOrDefault(z => z.Value.Prefab == rhEntity.PrefabName && z.Value.Skin == rhEntity.skinID);
                    if (product.Equals(default(KeyValuePair<string, VehicleInfoConfig>)) || !product.Value.CanPickup)
                    {
                        player.ChatMessage("Vehicle not found or cannot be picked up");
                        return;
                    }

                    if (product.Value.PickupPrice > 0)
                    {
                        if (product.Value.PickupPrice > GetBalance(player, product.Value))
                        {
                            player.ChatMessage($"Not enough balance! Need - {product.Value.PickupPrice - GetBalance(player, product.Value)}");
                            return;
                        }
                        Withdraw(player, product.Value.PickupPrice, product.Value.SellCurrency, product.Value.Shortname);
                    }

                    if (!GiveVehicle(player, product.Value, false))
                    {
                        player.ChatMessage("Unable to give vehicle to player.");
                        return;
                    }

                    if (rhEntity.TryGetComponent<ModularCar>(out var component))
                        GetOutParts(component);

                    var baseVehicle = rhEntity.GetComponent<BaseVehicle>();
                    if (baseVehicle != null)
                    {
                        var fuelSystem = baseVehicle.GetFuelSystem();
                        if (fuelSystem != null)
                        {
                            var fuelAmount = fuelSystem.GetFuelAmount();
                            if (fuelAmount > 0)
                            {
                                var fuelItem = ItemManager.CreateByName("lowgradefuel", fuelAmount);
                                if (fuelItem != null && fuelItem.amount > 0)
                                {
                                    if (!player.inventory.GiveItem(fuelItem))
                                    {
                                        player.ChatMessage($"{fuelItem.amount} fuel was dropped at your feet");
                                        fuelItem.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                                    }
                                }
                            }
                        }
                    }

                    rhEntity.Kill();
                    player.ChatMessage($"Vehicle {product.Value.Name} was picked up");
                }
                else
                {
                    player.ChatMessage("Vehicle not found");
                }
            }
            else
            {
                player.ChatMessage("Vehicle not found");
            }
        }

        [ConsoleCommand("vb_buy")]
        private void cmdBuy(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || arg.Args.IsNullOrEmpty())
                return;

            if (!arg.Player().IPlayer.HasPermission(PERM_USE))
                return;

            var player = arg.Player();
            var vehicle = arg.Args[0];

            if (!cfg.Vehicles.ContainsKey(vehicle))
                return;


            var product = cfg.Vehicles[vehicle];

            if (!product.Show)
                return;

            if (!product.CanPlayerBuy(player))
                return;

            if (IsCooldown(arg.Player(), vehicle))
            {
                var timespawn = TimeSpan.FromSeconds(GetCooldown(arg.Player(), vehicle));

                arg.Player().ChatMessage($"Vehicle is on cooldown! Remaining - {timespawn:hh\\:mm\\:ss}");
                return;
            }

            if (Collect(arg.Player(), product))
            {
                SetCooldown(player, vehicle, product.Cooldown);
                if (product.UseSoundOnPurchase)
                    SendEffect(arg.Player(), PURCHASE_EFFECT);
                GiveVehicle(player, product);
            }
            else
            {
                arg.Player().ChatMessage($"Not enough money for buy vehicle! Need - {GetRemainingCost(arg.Player(), product)}");
            }
        }
        [ConsoleCommand("vb_page")]
        private void cmdChangePage(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || arg.Args.IsNullOrEmpty())
                return;

            if (!arg.Player().IPlayer.HasPermission(PERM_USE))
                return;

            if (!int.TryParse(arg.Args[0], out var page))
                return;

            UI_DrawVehicles(arg.Player(), page);
            UI_DrawPages(arg.Player(), page);
        }

        private void cmdVehicleBuy(BasePlayer player, string command, string[] args)
        {
            if (!player.IPlayer.HasPermission(PERM_USE))
            {
                player.ChatMessage("[Error] You do not have access to this command!");
                return;
            }

            UI_DrawMain(player);
        }

        private void SendMessage(IPlayer player, string message)
        {
            if (player.IsServer)
            {
                PrintWarning("\n\n" + message);
                return;
            }
            player.Message(message);
        }
        private void CmdAddVehicle(IPlayer user, string command, string[] args)
        {
            if (!user.IsServer && !user.IsAdmin)
            {
                SendMessage(user, "[Error] You do not have access to this command!");
                return;
            }

            if (args.Length < 1)
            {
                SendMessage(user, $"[Error] Enter {command} steamid/nickname\n[Example] {command} Jjj\n[Example] {command} 76561198311233564");
                // PrintError(
                // );
                return;
            }

            var player = BasePlayer.Find(args[0]);
            if (player == null)
            {
                SendMessage(user, $"[Error] Unable to find player {args[0]}");
                return;
            }

            var vehicleSettings = cfg.Vehicles.FirstOrDefault(s => s.Value.Command == command);

            if (vehicleSettings.Value == null)
            {
                SendMessage(user, "Undefined vehicle!");
                return;
            }

            GiveVehicle(player, vehicleSettings.Value);
        }

        #endregion

        #region Hooks

        #region LockHelpers
        private static BaseLock GetVehicleLock(BaseEntity vehicle)
        {
            return vehicle.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
        }
        private bool IsPlayerAuthorizedToCodeLock(ulong userID, CodeLock codeLock)
        {
            return codeLock.whitelistPlayers.Contains(userID)
                || codeLock.guestPlayers.Contains(userID);
        }

        private bool IsPlayerAuthorizedToLock(BasePlayer player, BaseLock baseLock)
        {
            return (baseLock as KeyLock)?.HasLockPermission(player)
                ?? IsPlayerAuthorizedToCodeLock(player.userID, baseLock as CodeLock);
        }

        private object CanPlayerInteractWithVehicle(BasePlayer player, BaseEntity vehicle, bool provideFeedback = true)
        {
            if (player == null || vehicle == null)
                return null;

            var baseLock = GetVehicleLock(vehicle);
            if (baseLock == null || !baseLock.IsLocked())
                return null;

            if (CanPlayerBypassLock(player, baseLock, provideFeedback))
                return null;

            return false;
        }

        private BaseEntity GetParentVehicle(BaseEntity entity)
        {
            var parent = entity.GetParentEntity();
            if (parent == null)
                return null;

            // Check for a vehicle module first since they are considered vehicles.
            var parentModule = parent as BaseVehicleModule;
            if (parentModule != null)
                return parentModule.Vehicle;

            if (parent is HotAirBalloon || parent is BaseVehicle)
                return parent;

            return _vehicleInfoManager.GetCustomVehicleParent(entity);
        }

        private object CanPlayerInteractWithParentVehicle(BasePlayer player, BaseEntity entity, bool provideFeedback = true)
        {
            return CanPlayerInteractWithVehicle(player, GetParentVehicle(entity), provideFeedback);
        }
        #endregion

        #region Lock Info

        private class LockInfo
        {
            public int ItemId;
            public string Prefab;
            public string PreHookName;

            public ItemDefinition ItemDefinition =>
                ItemManager.FindItemDefinition(ItemId);

            public ItemBlueprint Blueprint =>
                ItemManager.FindBlueprint(ItemDefinition);
        }

        private readonly LockInfo LockInfo_CodeLock = new LockInfo
        {
            ItemId = 1159991980,
            Prefab = "assets/prefabs/locks/keypad/lock.code.prefab",
            PreHookName = "CanDeployVehicleCodeLock",
        };

        private readonly LockInfo LockInfo_KeyLock = new LockInfo
        {
            ItemId = -850982208,
            Prefab = "assets/prefabs/locks/keylock/lock.key.prefab",
            PreHookName = "CanDeployVehicleKeyLock",
        };

        #endregion

        private object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            // Don't lock taxi modules
            if (!(entity as ModularCarSeat)?.associatedSeatingModule?.DoorsAreLockable ?? false)
                return null;

            return CanPlayerInteractWithParentVehicle(player, entity);
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            // Don't lock taxi module shop fronts
            if (container is ModularVehicleShopFront)
                return null;

            return CanPlayerInteractWithParentVehicle(player, container);
        }

        private object CanLootEntity(BasePlayer player, ContainerIOEntity container) =>
            CanPlayerInteractWithParentVehicle(player, container);

        private object CanLootEntity(BasePlayer player, RidableHorse horse) =>
            CanPlayerInteractWithVehicle(player, horse);

        private object CanLootEntity(BasePlayer player, ModularCarGarage carLift)
        {
            if (carLift == null
                || !carLift.PlatformIsOccupied)
                return null;

            return CanPlayerInteractWithVehicle(player, carLift.carOccupant);
        }

        private object OnHorseLead(RidableHorse horse, BasePlayer player) =>
            CanPlayerInteractWithVehicle(player, horse);

        private object OnHotAirBalloonToggle(HotAirBalloon hab, BasePlayer player) =>
            CanPlayerInteractWithVehicle(player, hab);

        private object OnSwitchToggle(ElectricSwitch electricSwitch, BasePlayer player)
        {
            if (electricSwitch == null)
                return null;

            var autoTurret = electricSwitch.GetParentEntity() as AutoTurret;
            if (autoTurret != null)
                return CanPlayerInteractWithParentVehicle(player, autoTurret);

            return null;
        }

        private object OnTurretAuthorize(AutoTurret entity, BasePlayer player)
        {
            return CanPlayerInteractWithParentVehicle(player, entity);
        }

        private object OnTurretTarget(AutoTurret autoTurret, BasePlayer player)
        {
            if (autoTurret == null || player == null || player.UserIDString == null)
                return null;

            var turretParent = autoTurret.GetParentEntity();
            var vehicle = turretParent as BaseVehicle ?? (turretParent as BaseVehicleModule)?.Vehicle;
            if (vehicle == null)
                return null;

            var baseLock = GetVehicleLock(vehicle);
            if (baseLock == null)
                return null;

            if (CanPlayerBypassLock(player, baseLock, provideFeedback: false))
                return false;

            return null;
        }

        private object CanSwapToSeat(BasePlayer player, ModularCarSeat carSeat)
        {
            // Don't lock taxi modules
            if (!carSeat.associatedSeatingModule.DoorsAreLockable)
                return null;

            return CanPlayerInteractWithParentVehicle(player, carSeat, provideFeedback: false);
        }

        private object OnVehiclePush(BaseVehicle vehicle, BasePlayer player)
        {
            return CanPlayerInteractWithVehicle(player, vehicle);
        }

        private void OnEntityKill(BaseLock baseLock)
        {
            var vehicle = GetParentVehicle(baseLock);
            if (vehicle == null)
                return;

            _lockedVehicleTracker.OnLockRemoved(vehicle);
        }

        private void OnEntityTakeDamage(BaseEntity ent, HitInfo info)
        {
            if (ent == null || info == null) return;
            if (ent is Recycler && ent.OwnerID != 0 && info.InitiatorPlayer.userID.Get() != 0)
            {
                var vehicleSettings = cfg.Vehicles.FirstOrDefault(x => x.Value.Skin == ent.skinID && ent.name.Contains(x.Value.Name, StringComparison.OrdinalIgnoreCase)).Value;

                if (vehicleSettings == null)
                    return;
                GiveVehicle(info.InitiatorPlayer, vehicleSettings);
                ent.Kill();
            }
        }
        // Handle the case where a cockpit is removed but the car remains
        // If a lock is present, either move the lock to another cockpit or destroy it
        private void OnEntityKill(VehicleModuleSeating seatingModule)
        {
            if (seatingModule == null || !seatingModule.HasADriverSeat())
                return;

            var car = seatingModule.Vehicle as ModularCar;
            if (car == null)
                return;

            var baseLock = seatingModule.GetComponentInChildren<BaseLock>();
            if (baseLock == null)
                return;

            baseLock.SetParent(null);

            var car2 = car;
            var baseLock2 = baseLock;

            NextTick(() =>
            {
                if (car2 == null)
                {
                    _lockedVehicleTracker.OnLockRemoved(car2);
                    baseLock2.Kill();
                }
                else
                {
                    var driverModule = FindFirstDriverModule(car2);
                    if (driverModule == null)
                    {
                        _lockedVehicleTracker.OnLockRemoved(car2);
                        baseLock2.Kill();
                    }
                    else
                    {
                        baseLock2.SetParent(driverModule);
                    }
                }
            });
        }

        // Allow players to deploy locks directly without any commands.
        private object CanDeployItem(BasePlayer basePlayer, Deployer deployer, NetworkableId entityId)
        {
            if (basePlayer == null || deployer == null)
                return null;

            var deployable = deployer.GetDeployable();
            if (deployable == null)
                return null;

            var activeItem = basePlayer.GetActiveItem();
            if (activeItem == null)
                return null;

            var itemid = activeItem.info.itemid;

            LockInfo lockInfo;
            if (itemid == LockInfo_CodeLock.ItemId)
            {
                lockInfo = LockInfo_CodeLock;
            }
            else if (itemid == LockInfo_KeyLock.ItemId)
            {
                lockInfo = LockInfo_KeyLock;
            }
            else
            {
                return null;
            }

            var vehicle = GetVehicleFromEntity(BaseNetworkable.serverEntities.Find(entityId) as BaseEntity, basePlayer);
            if (vehicle == null)
                return null;

            var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(vehicle);
            if (vehicleInfo == null)
                return null;

            var player = basePlayer.IPlayer;

            // Trick to make sure the replies are in chat instead of console.
            player.LastCommand = CommandType.Chat;

            if (!VerifyCanDeploy(player, vehicle, vehicleInfo, lockInfo)
                || !VerifyDeployDistance(player, vehicle))
                return false;

            activeItem.UseItem();
            DeployLockForPlayer(vehicle, vehicleInfo, lockInfo, basePlayer);
            return false;
        }
        private BaseLock DeployLock(BaseEntity vehicle, VehicleInfo vehicleInfo, LockInfo lockInfo, ulong ownerId = 0)
        {
            var parentToEntity = vehicleInfo.DetermineLockParent(vehicle);
            if (parentToEntity == null)
                return null;

            var baseLock = GameManager.server.CreateEntity(lockInfo.Prefab, vehicleInfo.LockPosition, vehicleInfo.LockRotation) as BaseLock;
            if (baseLock == null)
                return null;

            var keyLock = baseLock as KeyLock;
            if (keyLock != null)
            {
                keyLock.keyCode = UnityEngine.Random.Range(1, 100000);
            }

            // Assign lock ownership when the lock is being deployed by/for a player.
            if (ownerId != 0)
            {
                baseLock.OwnerID = ownerId;
            }

            baseLock.SetParent(parentToEntity, vehicleInfo.ParentBone);
            baseLock.Spawn();
            vehicle.SetSlot(BaseEntity.Slot.Lock, baseLock);

            // Auto lock key locks to be consistent with vanilla.
            if (ownerId != 0 && keyLock != null)
            {
                keyLock.SetFlag(BaseEntity.Flags.Locked, true);
            }

            Effect.server.Run(Prefab_CodeLock_DeployedEffect, baseLock.transform.position);
            Interface.CallHook("OnVehicleLockDeployed", vehicle, baseLock);
            _lockedVehicleTracker.OnLockAdded(vehicle);

            return baseLock;
        }
        private BaseLock DeployLockForPlayer(BaseEntity vehicle, VehicleInfo vehicleInfo, LockInfo lockInfo, BasePlayer player)
        {
            var originalVehicleOwnerId = vehicle.OwnerID;

            // Temporarily set the player as the owner of the vehicle, for compatibility with AutoCodeLock (OnItemDeployed).
            vehicle.OwnerID = player.userID;

            var baseLock = DeployLock(vehicle, vehicleInfo, lockInfo, player.userID);
            if (baseLock == null)
            {
                vehicle.OwnerID = originalVehicleOwnerId;
                return null;
            }

            // Allow other plugins to detect the code lock being deployed (e.g., to auto lock).
            var lockItem = GetPlayerLockItem(player, lockInfo);
            if (lockItem != null)
            {
                Interface.CallHook("OnItemDeployed", lockItem.GetHeldEntity(), vehicle, baseLock);
            }
            else
            {
                // Temporarily increase the player inventory capacity to ensure there is enough space.
                player.inventory.containerMain.capacity++;
                var temporaryLockItem = ItemManager.CreateByItemID(lockInfo.ItemId);
                if (player.inventory.GiveItem(temporaryLockItem))
                {
                    Interface.CallHook("OnItemDeployed", temporaryLockItem.GetHeldEntity(), vehicle, baseLock);
                    temporaryLockItem.RemoveFromContainer();
                }
                temporaryLockItem.Remove();
                player.inventory.containerMain.capacity--;
            }

            // Revert the vehicle owner to the original, after OnItemDeployed is called.
            vehicle.OwnerID = originalVehicleOwnerId;


            // Potentially assign vehicle ownership when the lock is being deployed by/for a player.
            ClaimVehicle(vehicle, player.userID);

            return baseLock;
        }
        private static void ClaimVehicle(BaseEntity vehicle, ulong ownerId)
        {
            vehicle.OwnerID = ownerId;
            Interface.CallHook("OnVehicleOwnershipChanged", vehicle);
        }
        private static Item GetPlayerLockItem(BasePlayer player, LockInfo lockInfo)
        {
            return player.inventory.FindItemByItemID(lockInfo.ItemId);
        }
        private bool VerifyDeployDistance(IPlayer player, BaseEntity vehicle)
        {
            if (vehicle.Distance(player.Object as BasePlayer) <= MaxDeployDistance)
                return true;

            return false;
        }
        private static bool IsDead(BaseEntity entity)
        {
            return (entity as BaseCombatEntity)?.IsDead() ?? false;
        }
        private bool VerifyVehicleIsNotDead(IPlayer player, BaseEntity vehicle)
        {
            if (!IsDead(vehicle))
                return true;

            return false;
        }

        private bool VerifyNotForSale(IPlayer player, BaseEntity vehicle)
        {
            var rideableAnimal = vehicle as BaseRidableAnimal;
            if (rideableAnimal == null || !rideableAnimal.IsForSale())
                return true;

            return false;
        }
        private bool AllowNoOwner(BaseEntity vehicle)
        {
            return vehicle.OwnerID != 0;
        }

        private bool AllowDifferentOwner(IPlayer player, BaseEntity vehicle)
        {
            return vehicle.OwnerID == 0
                   || vehicle.OwnerID.ToString() == player.Id;
        }
        private bool VerifyNoOwnershipRestriction(IPlayer player, BaseEntity vehicle)
        {
            if (!AllowNoOwner(vehicle))
            {
                return false;
            }

            if (!AllowDifferentOwner(player, vehicle))
            {
                return false;
            }

            return true;
        }

        private bool VerifyCanBuild(IPlayer player, BaseEntity vehicle)
        {
            var basePlayer = player.Object as BasePlayer;

            if (vehicle.OwnerID == 0)
            {
                if (!basePlayer.IsBuildingAuthed() || !basePlayer.IsBuildingAuthed(vehicle.WorldSpaceBounds()))
                {
                    return false;
                }
            }
            else if (basePlayer.IsBuildingBlocked() || basePlayer.IsBuildingBlocked(vehicle.WorldSpaceBounds()))
            {
                return false;
            }

            return true;
        }

        private bool VerifyVehicleHasNoLock(IPlayer player, BaseEntity vehicle)
        {
            if (GetVehicleLock(vehicle) == null)
                return true;

            return false;
        }

        private bool VerifyVehicleCanHaveALock(IPlayer player, BaseEntity vehicle)
        {
            if (CanVehicleHaveALock(vehicle))
                return true;

            return false;
        }
        private static bool CanCarHaveLock(ModularCar car)
        {
            return FindFirstDriverModule(car) != null;
        }

        private static bool CanVehicleHaveALock(BaseEntity vehicle)
        {
            // Only modular cars have restrictions
            var car = vehicle as ModularCar;
            return car == null || CanCarHaveLock(car);
        }

        private bool VerifyNotMounted(IPlayer player, BaseEntity vehicle, VehicleInfo vehicleInfo)
        {
            if (!vehicleInfo.IsMounted(vehicle))
                return true;

            return false;
        }

        private bool VerifyCanDeploy(IPlayer player, BaseEntity vehicle, VehicleInfo vehicleInfo, LockInfo lockInfo)
        {
            var basePlayer = player.Object as BasePlayer;

            return
                VerifyVehicleIsNotDead(player, vehicle)
                && VerifyNotForSale(player, vehicle)
                && VerifyNoOwnershipRestriction(player, vehicle)
                && VerifyCanBuild(player, vehicle)
                && VerifyVehicleHasNoLock(player, vehicle)
                && VerifyVehicleCanHaveALock(player, vehicle)
                && VerifyNotMounted(player, vehicle, vehicleInfo)
                && !DeployWasBlocked(vehicle, basePlayer, lockInfo);
        }
        private static bool DeployWasBlocked(BaseEntity vehicle, BasePlayer player, LockInfo lockInfo)
        {
            var hookResult = Interface.CallHook(lockInfo.PreHookName, vehicle, player);
            return hookResult is bool && (bool)hookResult == false;
        }
        private static RidableHorse GetClosestHorse(HitchTrough hitchTrough, BasePlayer player)
        {
            var closestDistance = 1000f;
            RidableHorse closestHorse = null;

            for (var i = 0; i < hitchTrough.hitchSpots.Length; i++)
            {
                var hitchSpot = hitchTrough.hitchSpots[i];
                if (!hitchSpot.IsOccupied())
                    continue;

                var distance = Vector3.Distance(player.transform.position, hitchSpot.spot.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHorse = hitchSpot.horse.Get(serverside: true) as RidableHorse;
                }
            }

            return closestHorse;
        }
        private static BaseEntity GetVehicleFromEntity(BaseEntity entity, BasePlayer basePlayer)
        {
            if (entity == null)
                return null;

            var module = entity as BaseVehicleModule;
            if (module != null)
                return module.Vehicle;

            var carLift = entity as ModularCarGarage;
            if ((object)carLift != null)
                return carLift.carOccupant;

            var hitchTrough = entity as HitchTrough;
            if ((object)hitchTrough != null)
                return GetClosestHorse(hitchTrough, basePlayer);

            return entity;
        }
        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (info == null || info.HitEntity == null || player == null)
                return null;

            var rhEntity = info.HitEntity;
            if (rhEntity.name.Contains("module_entities"))
                rhEntity = rhEntity.parentEntity.Get(true);
            if (rhEntity.OwnerID != player.userID)
            {
                return null;
            }

            var product = cfg.Vehicles.FirstOrDefault(z => z.Value.Prefab == rhEntity.PrefabName && z.Value.Skin == rhEntity.skinID);
            if (product.Equals(default(KeyValuePair<string, VehicleInfoConfig>)) || !product.Value.CanPickup)
                return null;

            if (product.Value.PickupType == 0)
            {
                player.ChatMessage("Use '/pickup' to pickup vehicle.");
                return null;
            }

            if (!GiveVehicle(player, product.Value, false))
            {
                player.ChatMessage("Unable to give vehicle to player.");
                return null;
            }

            if (rhEntity.TryGetComponent<ModularCar>(out var component))
                GetOutParts(component);

            var baseVehicle = rhEntity.GetComponent<BaseVehicle>();
            if (baseVehicle != null)
            {
                var fuelSystem = baseVehicle.GetFuelSystem();
                if (fuelSystem != null)
                {
                    var fuelAmount = fuelSystem.GetFuelAmount();
                    if (fuelAmount > 0)
                    {
                        var fuelItem = ItemManager.CreateByName("lowgradefuel", fuelAmount);
                        if (fuelItem != null && fuelItem.amount > 0)
                        {
                            if (!player.inventory.GiveItem(fuelItem))
                            {
                                player.ChatMessage($"{fuelItem.amount} fuel was dropped at your feet");
                                fuelItem.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                            }
                        }
                    }
                }
            }

            rhEntity.Kill();
            player.ChatMessage($"Vehicle {product.Value.Name} was picked up");

            return false;
        }

        private void SpawnRecycler(BaseEntity entity, BasePlayer player, Vector3 position, Quaternion rotation, string prefab, ulong skin)
        {
            Recycler recycler = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", position, rotation) as Recycler;
            recycler.OwnerID = player.userID.Get();
            recycler.skinID = skin;
            recycler.Spawn();
            recycler._maxHealth = 1000;
            recycler.health = recycler.MaxHealth();
            NextFrame(() => { entity?.Kill(); });
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            BaseEntity entity = gameObject?.ToBaseEntity();
            if (entity == null || entity.skinID <= 0) return;

            var item = planner.GetItem();
            if (item == null)
                return;

            var vehicleSettings = cfg.Vehicles.FirstOrDefault(x => x.Value.Skin == entity.skinID && item.name.Contains(x.Value.Name, StringComparison.OrdinalIgnoreCase)).Value;

            if (vehicleSettings == null)
                return;

            Quaternion rot = entity.transform.rotation;
            Vector3 pos = entity.transform.position;
            string prefab = vehicleSettings.Prefab;

            if (prefab.Contains("recycler_static"))
            {
                if (CanPlaceRecycler(entity, planner.GetOwnerPlayer()))
                {
                    SpawnRecycler(entity, planner.GetOwnerPlayer(), pos, rot, prefab, vehicleSettings.Skin);
                    return;
                }
                else
                {
                    GiveVehicle(planner.GetOwnerPlayer(), vehicleSettings, false);
                    entity.Kill();
                    planner.GetOwnerPlayer().ChatMessage("Recycler cannot be placed here, it has been returned to your inventory.");
                    return;
                }
            }

            NextFrame(() => { entity?.Kill(); });

            pos = GetPositionFromPlayer(planner.GetOwnerPlayer(), vehicleSettings.SpawnDistance);
            var newEntity = GameManager.server.CreateEntity(prefab, pos, rot, true);

            if (newEntity == null)
            {
                GiveVehicle(planner.GetOwnerPlayer(), vehicleSettings);
                return;
            }

            newEntity.OwnerID = planner.GetOwnerPlayer().userID;
            newEntity.skinID = vehicleSettings.Skin;
            newEntity.Spawn();

            if (planner.GetItem() != null && !planner.GetItem().name.EndsWith("   "))
            {
                var baseVehicle = newEntity.GetComponent<BaseVehicle>();
                if (baseVehicle != null)
                {
                    var fuelSystem = baseVehicle.GetFuelSystem();
                    if (fuelSystem != null)
                    {
                        fuelSystem.AddFuel(vehicleSettings.Fuel);
                    }
                }
            }

            if (vehicleSettings.NeedCarParts)
            {
                if (newEntity.TryGetComponent<ModularCar>(out var carComponent))
                {
                    AddEngineParts(carComponent, vehicleSettings.EngineParts);
                }
            }

            if (!vehicleSettings.EnableDecay)
            {
                if (newEntity.TryGetComponent<DecayEntity>(out var decayEntity))
                    UnityEngine.Object.Destroy(decayEntity);
            }
        }

        private bool CanPlaceRecycler(BaseEntity entity, BasePlayer player)
        {
            // Check if the recycler can be placed at the current location
            // For demonstration purposes, we assume that placing a recycler within a monument is not allowed
            return !IsInMonument(entity.transform.position);
        }

        private bool IsInMonument(Vector3 position)
        {
            // This is a simple placeholder for checking if a position is within a monument.
            // You would need to replace this with actual game logic to check for monuments.
            // For example, you might need to use TerrainMeta.Path.Monuments or similar.
            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (monument.Bounds.Contains(position))
                {
                    return true;
                }
            }
            return false;
        }

        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (cfg.Vehicles.Any(x => x.Value.Skin == item.GetItem().skin)) return false;

            return null;
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (cfg.Vehicles.Any(x => x.Value.Skin == item.skin)) return false;

            return null;
        }

        #endregion

        #region Helpers

        private Vector3 GetPositionFromPlayer(BasePlayer player, float distance = 1f)
        {
            Quaternion rotation = player.GetNetworkRotation();

            Vector3 forward = rotation * Vector3.forward;
            Vector3 straight = Vector3.Cross(Vector3.Cross(Vector3.up, forward), Vector3.up).normalized;

            Vector3 buff = new Vector3(player.transform.position.x + (straight.x * distance),
                TerrainMeta.HeightMap.GetHeight(player.transform.position + (straight * distance)),
                player.transform.position.z + (straight.z * distance));

            return buff;
        }

        #endregion

        #region Config

        private ConfigData cfg;

        internal class VehicleInfoConfig
        {
            [JsonProperty("Sound on purchase", Order = 0)]
            public bool UseSoundOnPurchase;
            [JsonProperty("Order", Order = -1)] public int Order;
            [JsonProperty("Show", Order = 0)]
            public bool Show;
            [JsonProperty("Name", Order = 1)]
            public string Name;
            [JsonProperty("Prefab", Order = 2)]
            public string Prefab;
            [JsonProperty("Image link", Order = 3)]
            public string Image;
            [JsonProperty("Spawn distance", Order = 4)]
            public float SpawnDistance;
            [JsonProperty("Fuel", Order = 5)]
            public int Fuel;
            [JsonProperty("Currency: 0 - item, 1 - Economics, 2 - Server Rewards", Order = 6)]
            public byte SellCurrency;
            [JsonProperty("If vehicle selling for item type him shortname", Order = 7)]
            public string Shortname;
            [JsonProperty("Price", Order = 8)]
            public int Price;
            [JsonProperty("Skin", Order = 9)]
            public ulong Skin;
            [JsonProperty("Command", Order = 10)]
            public string Command;
            [JsonProperty("DeployableItemId", Order = 11)]
            public int DeployableItemId;
            [JsonProperty("Need add engine parts if it possible?", Order = 12)]
            public bool NeedCarParts = true;
            [JsonProperty("Engine parts", Order = 13)] public List<string> EngineParts;
            [JsonProperty("Cooldown to buy (in seconds)", Order = -2)]
            public int Cooldown;

            [JsonProperty("Pickup type (0 - command, 1 - hammer)", Order = -3)]
            public int PickupType;
            [JsonProperty("Can pickup?", Order = -4)]
            public bool CanPickup;
            [JsonProperty("Can recall?", Order = -5)]
            public bool CanCallback;

            [JsonProperty("Recall price", Order = -6)]
            public int RecallCost;
            [JsonProperty("Recall cost need?", Order = -7)]
            public bool RecallCostNeed;
            [JsonProperty("Pickup price", Order = -8)]
            public int PickupPrice;
            [JsonProperty("Enable decay?", Order = -9)]
            public bool EnableDecay;

            [JsonProperty("Permission (still empty if not need) ex. vehiclebuy.YOURPERMISSIONNAME", Order = -10)]
            public string Permission;

            public bool CanPlayerBuy(BasePlayer player)
            {
                if (string.IsNullOrEmpty(Permission))
                    return true;

                return player.IPlayer.HasPermission(Permission);
            }
        }

        internal class VendingMachinesConfig
        {
            [JsonProperty("Bandit Camp vending machine")]
            public bool BanditCampSpawnMachine;
            [JsonProperty("Outpost vending machine")]
            public bool OutpostSpawnMachine;

            [JsonProperty("Bandit Camp products")]
            public List<VMOrder> BanditCampOrders;
            [JsonProperty("Outpost products")]
            public List<VMOrder> OutpostOrders;

            [JsonProperty("Fishing village A vending machine")]
            public bool FishingVillageASpawnMachine;
            [JsonProperty("Fishing village B vending machine")]
            public bool FishingVillageBSpawnMachine;
            [JsonProperty("Fishing village C vending machine")]
            public bool FishingVillageCSpawnMachine;

            [JsonProperty("Fishing Village C products")]
            public List<VMOrder> FishingVillageCOrders;
            [JsonProperty("Fishing Village A products")]
            public List<VMOrder> FishingVillageAOrders;
            [JsonProperty("Fishing Village B products")]
            public List<VMOrder> FishingVillageBOrders;
        }

        internal class VMOrder
        {
            [JsonProperty("Vehicle key from config")]
            public string VehicleKey;

            [JsonProperty("Item (shortname)")] public string Shortname;
            [JsonProperty("Price")] public int Price;

            public VehicleInfoConfig GetVehicle() => Instance.cfg.Vehicles[VehicleKey] ?? throw new ArgumentException($"Key {VehicleKey} not found in config");
        }
        public class ConfigData
        {
            [JsonProperty("Commands", Order = -1)] public List<string> Commands;
            [JsonProperty("Currency name economics", Order = 0)]
            public string CurrencyName;
            [JsonProperty("Currency name Server Rewards", Order = 1)]
            public string CurrencyNameSR;

            [JsonProperty("Pickup distance", Order = 2)] public float PickupRadius;

            [JsonProperty("Vending machines", Order = 2)] public VendingMachinesConfig VendingMachines;
            [JsonProperty("Vehicles", Order = 3)]
            public Dictionary<string, VehicleInfoConfig> Vehicles;

            [JsonProperty(Order = 200)]
            public VersionNumber Version;
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData()
            {
                VendingMachines = new VendingMachinesConfig()
                {
                    FishingVillageASpawnMachine = true,
                    FishingVillageBSpawnMachine = true,
                    FishingVillageCSpawnMachine = true,
                    FishingVillageAOrders = new()
                    {
                        new()
                        {
                            VehicleKey = "copter",
                            Shortname = "scrap",
                            Price = 1000
                        },
                        new()
                        {
                            VehicleKey = "scrapheli",
                            Shortname = "scrap",
                            Price = 999
                        },
                        new()
                        {
                            VehicleKey = "attackheli",
                            Shortname = "sulfur.ore",
                            Price = 200
                        }
                    },
                    FishingVillageBOrders = new()
                    {
                        new()
                        {
                            VehicleKey = "copter",
                            Shortname = "scrap",
                            Price = 1000
                        },
                        new()
                        {
                            VehicleKey = "scrapheli",
                            Shortname = "scrap",
                            Price = 999
                        },
                        new()
                        {
                            VehicleKey = "attackheli",
                            Shortname = "sulfur.ore",
                            Price = 200
                        }
                    },
                    FishingVillageCOrders = new()
                    {
                        new()
                        {
                            VehicleKey = "copter",
                            Shortname = "scrap",
                            Price = 1000
                        },
                        new()
                        {
                            VehicleKey = "scrapheli",
                            Shortname = "scrap",
                            Price = 999
                        },
                        new()
                        {
                            VehicleKey = "attackheli",
                            Shortname = "sulfur.ore",
                            Price = 200
                        }
                    },
                    BanditCampSpawnMachine = true,
                    BanditCampOrders = new()
                    {
                        new()
                        {
                            VehicleKey = "copter",
                            Shortname = "scrap",
                            Price = 1000
                        },
                        new()
                        {
                            VehicleKey = "scrapheli",
                            Shortname = "scrap",
                            Price = 999
                        },
                        new()
                        {
                            VehicleKey = "attackheli",
                            Shortname = "sulfur.ore",
                            Price = 200
                        }
                    },
                    OutpostSpawnMachine = true,
                    OutpostOrders = new()
                    {
                        new()
                        {
                            VehicleKey = "car2",
                            Shortname = "scrap",
                            Price = 1000
                        },
                        new()
                        {
                            VehicleKey = "car1",
                            Shortname = "scrap",
                            Price = 999
                        },
                        new()
                        {
                            VehicleKey = "tugboat",
                            Shortname = "sulfur.ore",
                            Price = 200
                        }
                    }
                },
                Commands = new()
                {
                    "vehiclebuy",
                    "vb",
                    "vehicle"
                },
                PickupRadius = 5f,
                CurrencyNameSR = "SRTEST",
                CurrencyName = "ECOTEST",
                Vehicles = new Dictionary<string, VehicleInfoConfig>()
                {
                    ["copter"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 1,
                        Show = true,
                        Name = "Minicopter",
                        Prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                        Image = "https://rustlabs.com/img/screenshots/minicopter.png",
                        SpawnDistance = 5f,
                        Fuel = 53,
                        SellCurrency = 2,
                        Shortname = "scrap",
                        Price = 550,
                        Skin = 3036041060,
                        Command = "copter.add",
                        DeployableItemId = 833533164,
                        Permission = ""
                    },
                    ["scrapheli"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 2,
                        Show = true,
                        Name = "Scrap Transport Helicopter",
                        Prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                        Image = "https://rustlabs.com/img/screenshots/scrap-heli.png",
                        SpawnDistance = 10f,
                        Fuel = 522,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 850,
                        Skin = 3033922797,
                        Command = "scrapi.add",
                        DeployableItemId = 833533164,
                        Permission = ""
                    },
                    ["attackheli"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 3,
                        Show = true,
                        Name = "Attack Helicopter",
                        Prefab = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",
                        Image = "https://rustlabs.com/img/screenshots/attack-helicopter.png",
                        SpawnDistance = 10f,
                        Fuel = 522,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 1200,
                        Skin = 3036032642,
                        Command = "attack.add",
                        DeployableItemId = 833533164,
                        Permission = ""
                    },
                    ["car2"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 4,
                        Show = true,
                        Name = "Car 2",
                        Prefab = "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab",
                        Image = "https://rustlabs.com/img/screenshots/modular-vehicle-2.png",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 300,
                        Skin = 3051397208,
                        Command = "car2.add",
                        DeployableItemId = 833533164,
                        NeedCarParts = true,
                        EngineParts = new List<string>()
                        {
                            "carburetor3",
                            "crankshaft3",
                            "piston3",
                            "valve3",
                            "sparkplug3"
                        },
                        Permission = ""
                    },
                    ["car3"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 5,
                        Show = true,
                        Name = "Car 3",
                        Prefab = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab",
                        Image = "https://rustlabs.com/img/screenshots/modular-vehicle-3.png",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 600,
                        Skin = 3051397420,
                        Command = "car3.add",
                        DeployableItemId = 833533164,
                        NeedCarParts = true,
                        EngineParts = new List<string>()
                        {
                            "carburetor3",
                            "crankshaft3",
                            "piston3",
                            "valve3",
                            "sparkplug3"
                        },
                        Permission = ""
                    },
                    ["car4"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 6,
                        Show = true,
                        Name = "Car 4",
                        Prefab = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab",
                        Image = "https://rustlabs.com/img/screenshots/modular-vehicle-4.png",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 900,
                        Skin = 3051397599,
                        Command = "car4.add",
                        DeployableItemId = 833533164,
                        NeedCarParts = true,
                        EngineParts = new List<string>()
                        {
                            "carburetor3",
                            "crankshaft3",
                            "piston3",
                            "valve3",
                            "sparkplug3"
                        },
                        Permission = ""
                    },
                    ["tugboat"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 7,
                        Show = true,
                        Name = "TugBoat",
                        Prefab = "assets/content/vehicles/boats/tugboat/tugboat.prefab",
                        Image = "https://rustlabs.com/img/screenshots/tugboat.png",
                        SpawnDistance = 15f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 1500,
                        Skin = 3036456691,
                        Command = "tugboat.add",
                        DeployableItemId = -697981032,
                        Permission = ""
                    },
                    ["rowboat"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 8,
                        Show = true,
                        Name = "RowBoat",
                        Prefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                        Image = "https://rustlabs.com/img/screenshots/rowboat.png",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 2,
                        Shortname = "scrap",
                        Price = 450,
                        Skin = 3036112261,
                        Command = "rowboat.add",
                        DeployableItemId = -697981032,
                        Permission = ""
                    },
                    ["rhib"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 9,
                        Show = true,
                        Name = "RHIB",
                        Prefab = "assets/content/vehicles/boats/rhib/rhib.prefab",
                        Image = "https://rustlabs.com/img/screenshots/rhib.png",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 585,
                        Skin = 3036112776,
                        Command = "rhib.add",
                        DeployableItemId = -697981032,
                        Permission = ""
                    },
                    ["solosub"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 10,
                        Show = true,
                        Name = "SoloSub",
                        Prefab = "assets/content/vehicles/submarine/submarinesolo.entity.prefab",
                        Image = "https://rustlabs.com/img/screenshots/submarine-solo.png",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 555,
                        Skin = 3036453289,
                        Command = "solosub.add",
                        DeployableItemId = -697981032,
                        Permission = ""
                    },
                    ["duosub"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 11,
                        Show = true,
                        Name = "DuoSub",
                        Prefab = "assets/content/vehicles/submarine/submarineduo.entity.prefab",
                        Image = "https://rustlabs.com/img/screenshots/submarine-duo.png",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 750,
                        Skin = 3036453387,
                        Command = "duosub.add",
                        DeployableItemId = -697981032,
                        Permission = ""
                    },
                    ["horse"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 12,
                        Show = true,
                        Name = "Horse",
                        Prefab = "assets/rust.ai/nextai/testridablehorse.prefab",
                        Image = "https://rustlabs.com/img/screenshots/ridable-horse.png",
                        SpawnDistance = 5f,
                        Fuel = 0,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 150,
                        Skin = 3036456786,
                        Command = "horse.add",
                        DeployableItemId = 833533164,
                        Permission = ""
                    },
                    ["snowmobile"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 13,
                        Show = true,
                        Name = "SnowMobile",
                        Prefab = "assets/content/vehicles/snowmobiles/snowmobile.prefab",
                        Image = "https://rustlabs.com/img/screenshots/snowmobile.png",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 600,
                        Skin = 3036453555,
                        Command = "snowmobile.add",
                        DeployableItemId = 833533164,
                        Permission = ""
                    },
                    ["tomaha"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 14,
                        Show = true,
                        Name = "Tomaha",
                        Prefab = "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab",
                        Image = "https://i.postimg.cc/YC9FFpkf/Download-1.jpg",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 100,
                        Skin = 3036453663,
                        Command = "tomaha.add",
                        DeployableItemId = 833533164,
                        Permission = ""
                    },
                    ["hotairballoon"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 15,
                        Show = true,
                        Name = "HotairBalloon",
                        Prefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab",
                        Image = "https://rustlabs.com/img/screenshots/balloon.png",
                        SpawnDistance = 5f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 300,
                        Skin = 3036454299,
                        Command = "hotairballoon.add",
                        DeployableItemId = 833533164,
                        Permission = ""
                    },
                    ["recycler"] = new VehicleInfoConfig
                    {
                        UseSoundOnPurchase = true,
                        Order = 16,
                        Show = true,
                        Name = "Recycler",
                        Prefab = "assets/bundled/prefabs/static/recycler_static.prefab",
                        Image = "https://rustlabs.com/img/screenshots/recycler.png",
                        SpawnDistance = 2f,
                        Fuel = 52,
                        SellCurrency = 0,
                        Shortname = "scrap",
                        Price = 1000,
                        Skin = 3036111302,
                        Command = "recycler.add",
                        DeployableItemId = 833533164,
                        Permission = ""
                    }
                }
            };
            SaveConfig(config);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<ConfigData>();

            if (cfg.Version == null)
                cfg.Version = new(1, 0, 0);

            if (cfg.Version < new VersionNumber(1, 1, 4))
            {
                cfg.Vehicles.Add("pedalbike", new()
                {
                    UseSoundOnPurchase = true,
                    Order = 17,
                    Show = true,
                    Name = "pedalbike",
                    Prefab = "assets/content/vehicles/bikes/pedalbike.prefab",
                    Image = "https://i.postimg.cc/PJMWqW0Q/Pedal.jpg",
                    SpawnDistance = 5,
                    Fuel = 100,
                    SellCurrency = 0,
                    Shortname = "scrap",
                    Price = 500,
                    Skin = 3281191605,
                    Command = "pedalbike.add",
                    DeployableItemId = 833533164,
                    NeedCarParts = false,
                    EngineParts = null,
                    Cooldown = 0,
                    PickupType = 0,
                    CanPickup = false,
                    CanCallback = false,
                    RecallCost = 0,
                    RecallCostNeed = false,
                    PickupPrice = 0,
                    EnableDecay = false,
                    Permission = null
                });

                cfg.Vehicles.Add("motorbike", new()
                {
                    UseSoundOnPurchase = true,
                    Order = 18,
                    Show = true,
                    Name = "motorbike",
                    Prefab = "assets/content/vehicles/bikes/motorbike.prefab",
                    Image = "https://i.postimg.cc/HxX0qD2K/Motorbike.jpg",
                    SpawnDistance = 5,
                    Fuel = 100,
                    SellCurrency = 0,
                    Shortname = "scrap",
                    Price = 1000,
                    Skin = 3281191090,
                    Command = "motorbike.add",
                    DeployableItemId = 833533164,
                    NeedCarParts = false,
                    EngineParts = null,
                    Cooldown = 0,
                    PickupType = 0,
                    CanPickup = false,
                    CanCallback = false,
                    RecallCost = 0,
                    RecallCostNeed = false,
                    PickupPrice = 0,
                    EnableDecay = false,
                    Permission = null
                });

                cfg.Vehicles.Add("motorbike_sidecar", new()
                {
                    UseSoundOnPurchase = true,
                    Order = 19,
                    Show = true,
                    Name = "motorbike_sidecar",
                    Prefab = "assets/content/vehicles/bikes/motorbike_sidecar.prefab",
                    Image = "https://i.postimg.cc/qMLwBrTH/Sidecar.jpg",
                    SpawnDistance = 5,
                    Fuel = 100,
                    SellCurrency = 0,
                    Shortname = "scrap",
                    Price = 1500,
                    Skin = 3281192470,
                    Command = "motorbike_sidecar",
                    DeployableItemId = 833533164,
                    NeedCarParts = false,
                    EngineParts = null,
                    Cooldown = 0,
                    PickupType = 0,
                    CanPickup = false,
                    CanCallback = false,
                    RecallCost = 0,
                    RecallCostNeed = false,
                    PickupPrice = 0,
                    EnableDecay = false,
                    Permission = null
                });
                cfg.Version = new(1, 1, 4);
                PrintWarning("Config was updated");
            }
            SaveConfig(cfg);
        }

        private void SaveConfig(object config)
        {
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/cooldowns", _players);
        }

        void LoadData()
        {
            _players = Interface.Oxide?.DataFileSystem?.ReadObject<Dictionary<ulong, PlayerData>>($"{Title}/cooldowns")
                       ?? new Dictionary<ulong, PlayerData>();
        }
        #endregion
    }
}