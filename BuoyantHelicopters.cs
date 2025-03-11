using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Buoyant Helicopters", "BlackLightning", "1.3.3")]
    [Description("Allows helicopters to float in water.")]
    internal class BuoyantHelicopters : CovalencePlugin
    {
        #region Fields

        private const string InnerTubePrefab = "assets/prefabs/misc/summer_dlc/inner_tube/innertube.deployed.prefab";

        private static readonly object True = true;
        private static readonly object False = false;

        private Configuration _config;
        private SavedData _data;
        private ProtectionProperties _immortalProtection;
        private readonly BuoyancyComponentManager _componentManager;
        private readonly VehicleInfoManager _vehicleInfoManager;
        private readonly TrackedCoroutine _trackedCoroutine;
        private readonly HookCollection _delayedHooks;
        private readonly HookCollection _permissionRelatedHooks;

        private List<BaseEntity> _reusableVehicleList = new List<BaseEntity>();
        private Coroutine _activeCoroutine;

        public BuoyantHelicopters()
        {
            _componentManager = new BuoyancyComponentManager(this);
            _vehicleInfoManager = new VehicleInfoManager(this);
            _trackedCoroutine = new TrackedCoroutine(this);

            _delayedHooks = new HookCollection(this, new[]
            {
                nameof(OnEntitySpawned),
                nameof(OnVehicleOwnershipChanged)
            });

            _permissionRelatedHooks = new HookCollection(this, new[]
            {
                nameof(OnServerSave),
                nameof(OnGroupPermissionGranted),
                nameof(OnGroupPermissionRevoked),
                nameof(OnUserPermissionGranted),
                nameof(OnUserPermissionRevoked),
                nameof(OnEntityMounted),
            }, () => _vehicleInfoManager.RequiresPermission);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _delayedHooks.Unsubscribe();
            _permissionRelatedHooks.Unsubscribe();
        }

        private void OnServerInitialized()
        {
            _vehicleInfoManager.OnServerInitialized();
            _immortalProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
            _immortalProtection.name = $"{nameof(BuoyantHelicopters)}Protection";
            _immortalProtection.Add(1);

            if (_vehicleInfoManager.RequiresPermission)
            {
                _data = SavedData.Load();
                _data.CleanEntitiesNotFound();
            }

            RefreshBuoyancyForAllVehicles();

            _delayedHooks.Refresh();
            _permissionRelatedHooks.Refresh();
        }

        private void Unload()
        {
            _data?.SaveIfChanged();
            UnityEngine.Object.Destroy(_immortalProtection);
            CancelActiveCoroutine();
            ServerMgr.Instance.StartCoroutine(_componentManager.UnloadRoutine());
        }

        // Only subscribed while permissions are required.
        private void OnServerSave()
        {
            _data?.SaveIfChanged();
        }

        private void OnEntitySpawned(BaseHelicopter vehicle)
        {
            var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(vehicle);
            if (vehicleInfo == null || !vehicleInfo.Config.Enabled)
                return;

            // Copy variables after conditions to delay closure creation.
            var vehicle2 = vehicle;
            var vehicleInfo2 = vehicleInfo;

            // Delay since plugins may set vehicle ownership on a delay.
            NextTick(() =>
            {
                if (vehicle2 == null || vehicle2.IsDestroyed)
                    return;

                if (!VehicleHasPermission(vehicle2, vehicleInfo2))
                    return;

                _componentManager.EnsureBuoyancy(vehicle2, vehicleInfo2);
            });
        }

        // Only subscribed while permissions are required.
        private void OnEntityMounted(BaseVehicleSeat seat)
        {
            var vehicle = seat.GetParentEntity() as BaseVehicle;
            if ((object)vehicle == null)
                return;

            var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(vehicle);
            if (vehicleInfo == null || !vehicleInfo.Config.Enabled)
                return;

            RefreshBuoyancy(vehicle);
        }

        private void OnGroupPermissionGranted(string groupName, string perm) => HandlePermissionChange(perm);
        private void OnGroupPermissionRevoked(string groupName, string perm) => HandlePermissionChange(perm);
        private void OnUserPermissionGranted(string userId, string perm) => HandlePermissionChange(perm);
        private void OnUserPermissionRevoked(string userId, string perm) => HandlePermissionChange(perm);

        // Hook from ClaimVehicle plugin.
        private void OnVehicleOwnershipChanged(BaseCombatEntity vehicle) => RefreshBuoyancy(vehicle);

        #endregion

        #region API

        [HookMethod(nameof(API_IsBuoyant))]
        public object API_IsBuoyant(BaseCombatEntity vehicle)
        {
            return BooleanNoAlloc(_componentManager.IsBuoyant(vehicle));
        }

        [HookMethod(nameof(API_AddBuoyancy))]
        public void API_AddBuoyancy(BaseCombatEntity vehicle)
        {
            var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(vehicle);
            if (vehicleInfo == null)
                throw new InvalidOperationException($"{nameof(API_AddBuoyancy)} does not support vehicle: {vehicle.PrefabName}");

            if (vehicleInfo.Config.Enabled)
                throw new InvalidOperationException($"{nameof(API_AddBuoyancy)} cannot add buoyancy to vehicles that are disabled in the config.");

            _componentManager.EnsureBuoyancy(vehicle, vehicleInfo, force: true);
        }

        [HookMethod(nameof(API_RemoveBuoyancy))]
        public void API_RemoveBuoyancy(BaseCombatEntity vehicle)
        {
            _componentManager.RemoveBuoyancy(vehicle);
        }

        #endregion

        #region Exposed Hooks

        private static class ExposedHooks
        {
            public static object OnVehicleBuoyancyAdd(BaseEntity vehicle)
            {
                return Interface.CallHook("OnVehicleBuoyancyAdd", vehicle);
            }

            public static void OnVehicleBuoyancyAdded(BaseEntity vehicle)
            {
                Interface.CallHook("OnVehicleBuoyancyAdded", vehicle);
            }

            public static void OnVehicleBuoyancyRemoved(BaseEntity vehicle)
            {
                Interface.CallHook("OnVehicleBuoyancyRemoved", vehicle);
            }
        }

        #endregion

        #region Helpers

        private static object BooleanNoAlloc(bool value) => value ? True : False;

        private bool OwnerHasPermission(BaseEntity entity, IVehicleInfo vehicleInfo)
        {
            return entity.OwnerID != 0
                && permission.UserHasPermission(entity.OwnerID.ToString(), vehicleInfo.OwnerPermission);
        }

        private bool PilotHasPermission(BaseEntity entity, IVehicleInfo vehicleInfo)
        {
            var vehicle = entity as BaseVehicle;
            if ((object)vehicle == null)
                return false;

            var pilotUserId = vehicle.GetDriver()?.UserIDString;
            if (pilotUserId == null && !_data.TryGetLastPilot(entity, out pilotUserId))
                return false;

            return permission.UserHasPermission(pilotUserId, vehicleInfo.PilotPermission);
        }

        private bool VehicleHasPermission(BaseEntity vehicle, IVehicleInfo vehicleInfo)
        {
            if (vehicleInfo.Config.RequirePermission)
            {
                return OwnerHasPermission(vehicle, vehicleInfo)
                    || PilotHasPermission(vehicle, vehicleInfo);
            }

            return true;
        }

        private bool RefreshBuoyancy(BaseEntity vehicle)
        {
            var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(vehicle);
            if (vehicleInfo == null || !vehicleInfo.Config.Enabled)
                return false;

            if (VehicleHasPermission(vehicle, vehicleInfo))
            {
                if (_vehicleInfoManager.RequiresPermission)
                {
                    var pilot = (vehicle as BaseVehicle)?.GetDriver();
                    if (pilot != null && pilot.userID.IsSteamId())
                    {
                        _data.SetLastPilot(vehicle, pilot.UserIDString);
                    }
                }

                return _componentManager.EnsureBuoyancy(vehicle, vehicleInfo);
            }

            return !_componentManager.IsForciblyBuoyant(vehicle) && _componentManager.RemoveBuoyancy(vehicle);
        }

        private IEnumerator RefreshBuoyancyRoutine()
        {
            _reusableVehicleList.Clear();

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var entity = networkable as BaseEntity;
                if ((object)entity == null)
                    continue;

                var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(entity);
                if (vehicleInfo == null || !vehicleInfo.Config.Enabled)
                    continue;

                _reusableVehicleList.Add(entity);
            }

            foreach (var entity in _reusableVehicleList)
            {
                if (entity == null || entity.IsDestroyed)
                    continue;

                if (RefreshBuoyancy(entity))
                    yield return null;
            }

            _reusableVehicleList.Clear();
        }

        private void CancelActiveCoroutine()
        {
            if (_activeCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(_activeCoroutine);
            }
        }

        private void RefreshBuoyancyForAllVehicles()
        {
            CancelActiveCoroutine();
            _activeCoroutine = ServerMgr.Instance.StartCoroutine(_trackedCoroutine.WithEnumerator(RefreshBuoyancyRoutine()));
        }

        private void HandlePermissionChange(string perm)
        {
            if (!perm.StartsWith(nameof(BuoyantHelicopters), StringComparison.OrdinalIgnoreCase))
                return;

            RefreshBuoyancyForAllVehicles();
        }

        #endregion

        #region Utilities

        private class HookCollection
        {
            private BuoyantHelicopters _plugin;
            private string[] _hookNames;
            private Func<bool> _shouldSubscribe;
            private bool _isSubscribed;

            public HookCollection(BuoyantHelicopters plugin, string[] hookNames, Func<bool> shouldSubscribe = null)
            {
                _plugin = plugin;
                _hookNames = hookNames;
                _shouldSubscribe = shouldSubscribe ?? (() => true);
            }

            public void Refresh()
            {
                if (_shouldSubscribe())
                {
                    if (!_isSubscribed)
                    {
                        Subscribe();
                    }
                }
                else if (_isSubscribed)
                {
                    Unsubscribe();
                }
            }

            public void Subscribe()
            {
                foreach (var hookName in _hookNames)
                {
                    _plugin.Subscribe(hookName);
                }

                _isSubscribed = true;
            }

            public void Unsubscribe()
            {
                foreach (var hookName in _hookNames)
                {
                    _plugin.Unsubscribe(hookName);
                }

                _isSubscribed = false;
            }
        }

        private class TrackedCoroutine : IEnumerator
        {
            private Plugin _plugin;
            private IEnumerator _inner;

            public TrackedCoroutine(Plugin plugin, IEnumerator inner = null)
            {
                _plugin = plugin;
                _inner = inner;
            }

            public object Current => _inner.Current;

            public bool MoveNext()
            {
                bool result;
                _plugin.TrackStart();

                try
                {
                    result = _inner.MoveNext();
                }
                finally
                {
                    _plugin.TrackEnd();
                }

                return result;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public TrackedCoroutine WithEnumerator(IEnumerator inner)
            {
                _inner = inner;
                return this;
            }
        }

        #endregion

        #region Vehicles

        private interface IVehicleInfo
        {
            string PrefabPath { get; }
            BuoyancyConfig Config { get; }
            uint PrefabId { get; }
            string OwnerPermission { get; }
            string PilotPermission { get; }
            float DefaultDrag { get; }
            float DefaultAngularDrag { get; }
            void OnServerInitialized(BuoyantHelicopters plugin);
            bool IsCorrectType(BaseEntity entity);
            Rigidbody GetRigidBody(BaseEntity entity);
        }

        private class VehicleInfo<T> : IVehicleInfo where T : BaseEntity
        {
            public string PermissionSuffix { private get; set; }
            public string PrefabPath { get; set; }
            public BuoyancyConfig Config { get; set; }
            public Func<T, Rigidbody> FindRigidBody { private get; set; }
            public uint PrefabId { get; private set; }
            public string OwnerPermission { get; private set; }
            public string PilotPermission { get; private set; }
            public float DefaultDrag { get; private set; }
            public float DefaultAngularDrag { get; private set; }

            private GameObject _prefab;

            public void OnServerInitialized(BuoyantHelicopters plugin)
            {
                OwnerPermission = $"{nameof(BuoyantHelicopters)}.owner.{PermissionSuffix}".ToLower();
                plugin.permission.RegisterPermission(OwnerPermission, plugin);

                PilotPermission = $"{nameof(BuoyantHelicopters)}.pilot.{PermissionSuffix}".ToLower();
                plugin.permission.RegisterPermission(PilotPermission, plugin);

                _prefab = GameManager.server.FindPrefab(PrefabPath);
                var defaultRigidBody = _prefab.GetComponent<Rigidbody>();
                DefaultDrag = defaultRigidBody.drag;
                DefaultAngularDrag = defaultRigidBody.angularDrag;

                var entity = _prefab.GetComponent<BaseEntity>();
                if (entity != null)
                {
                    PrefabId = entity.prefabID;
                }
            }

            public bool IsCorrectType(BaseEntity entity)
            {
                return entity is T;
            }

            public Rigidbody GetRigidBody(BaseEntity entity)
            {
                var entityOfType = entity as T;
                if ((object)entityOfType == null)
                    return null;

                return FindRigidBody(entityOfType);
            }
        }

        private class VehicleInfoManager
        {
            public bool RequiresPermission
            {
                get
                {
                    if (_allVehicles == null)
                        return false;

                    foreach (var vehicleInfo in _allVehicles)
                    {
                        if (vehicleInfo.Config.RequirePermission)
                            return true;
                    }

                    return false;
                }
            }

            private readonly BuoyantHelicopters _plugin;
            private readonly Dictionary<uint, IVehicleInfo> _prefabIdToVehicleInfo = new Dictionary<uint, IVehicleInfo>();
            private IVehicleInfo[] _allVehicles;

            private Configuration _config => _plugin._config;

            public VehicleInfoManager(BuoyantHelicopters plugin)
            {
                _plugin = plugin;
            }

            public void OnServerInitialized()
            {
                _allVehicles = new IVehicleInfo[]
                {
                    new VehicleInfo<Minicopter>
                    {
                        PermissionSuffix = "minicopter",
                        PrefabPath = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                        Config = _config.Minicopter,
                        FindRigidBody = mini => mini.rigidBody
                    },
                    new VehicleInfo<ScrapTransportHelicopter>
                    {
                        PermissionSuffix = "scraptransport",
                        PrefabPath = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                        Config = _config.ScrapTransportHelicopter,
                        FindRigidBody = scrapHeli => scrapHeli.rigidBody,
                    },
                    new VehicleInfo<AttackHelicopter>
                    {
                        PermissionSuffix = "attackhelicopter",
                        PrefabPath = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",
                        Config = _config.AttackHelicopter,
                        FindRigidBody = heli => heli.rigidBody,
                    },
                    #if FEATURE_CH47
                    new VehicleInfo<CH47Helicopter>
                    {
                        PermissionSuffix = "chinook",
                        PrefabPath = "assets/prefabs/npc/ch47/ch47.entity.prefab",
                        Config = _config.CH47,
                        FindRigidBody = ch47 => ch47.rigidBody,
                    },
                    #endif
                };

                foreach (var vehicleInfo in _allVehicles)
                {
                    vehicleInfo.OnServerInitialized(_plugin);

                    if (vehicleInfo.PrefabId != 0)
                    {
                        _prefabIdToVehicleInfo[vehicleInfo.PrefabId] = vehicleInfo;
                    }
                    else
                    {
                        _plugin.LogError($"Unable to determine Prefab ID for prefab: {vehicleInfo.PrefabPath}");
                    }
                }
            }

            public IVehicleInfo GetVehicleInfo(BaseEntity entity)
            {
                IVehicleInfo vehicleInfo;
                return _prefabIdToVehicleInfo.TryGetValue(entity.prefabID, out vehicleInfo) && vehicleInfo.IsCorrectType(entity)
                    ? vehicleInfo
                    : null;
            }
        }

        #endregion

        #region Buoyancy

        private class BuoyancyComponent : FacepunchBehaviour
        {
            private const float DecorationPersistSeconds = 5;

            public static bool TryAddToEntity(BuoyancyComponentManager manager, BaseEntity vehicle, IVehicleInfo vehicleInfo, out BuoyancyComponent component)
            {
                component = null;

                var config = vehicleInfo.Config;
                if (config.Points == null)
                    return false;

                var gameObject = vehicle.gameObject;
                var rigidBody = vehicleInfo.GetRigidBody(vehicle);
                if (rigidBody == null)
                    return false;

                component = gameObject.AddComponent<BuoyancyComponent>();
                component._manager = manager;
                component._entity = vehicle;
                component._netId = vehicle.net.ID.Value;
                component._vehicleInfo = vehicleInfo;

                var buoyancy = gameObject.AddComponent<Buoyancy>();
                component.Buoyancy = buoyancy;
                buoyancy.rigidBody = rigidBody;
                buoyancy.buoyancyScale = 1;

                if (config.UnderwaterDrag > 0)
                {
                    buoyancy.useUnderwaterDrag = true;
                    buoyancy.underwaterDrag = config.UnderwaterDrag;
                }

                buoyancy.points = new BuoyancyPoint[config.Points.Length];

                var numDecorations = 0;
                var numDynamicDecorations = 0;
                var lowestBuoyancyPointY = float.MaxValue;

                for (var i = 0; i < config.Points.Length; i++)
                {
                    var pointConfig = config.Points[i];
                    buoyancy.points[i] = CreateBuoyancyPoint(buoyancy, pointConfig);

                    lowestBuoyancyPointY = Mathf.Min(lowestBuoyancyPointY, pointConfig.Position.y);

                    if (!pointConfig.Decoration.Enabled)
                        continue;

                    numDecorations++;

                    if (pointConfig.Decoration.Dynamic)
                    {
                        numDynamicDecorations++;
                    }
                }

                buoyancy.SavePointData(false);

                if (lowestBuoyancyPointY != float.MaxValue)
                {
                    var centerOfMass = rigidBody.centerOfMass;
                    var desiredCenterOfMassY = lowestBuoyancyPointY - 0.3f;
                    if (desiredCenterOfMassY < centerOfMass.y)
                    {
                        component._originalCenterOfMassY = centerOfMass.y;
                        component._customCenterOfMassY = desiredCenterOfMassY;
                        if (buoyancy.submergedFraction > 0)
                        {
                            component.SetCenterOfMassY(desiredCenterOfMassY);
                        }
                    }
                }

                buoyancy.SubmergedChanged = component.SubmergedChanged;

                if (numDecorations > 0)
                {
                    component._decorations = new BaseEntity[numDecorations];

                    for (var i = 0; i < config.Points.Length; i++)
                    {
                        var pointConfig = config.Points[i];
                        if (!pointConfig.Decoration.Enabled)
                            continue;

                        component._decorations[i] = CreateDecoration(manager, vehicle, pointConfig.Decoration);
                    }
                }

                if (numDynamicDecorations > 0)
                {
                    component._checkDecorations = component.CheckDecorations;
                }

                return true;
            }

            private static BuoyancyPoint CreateBuoyancyPoint(Buoyancy buoyancy, PointConfig pointConfig)
            {
                var childObject = buoyancy.gameObject.CreateChild();
                childObject.name = "Buoyancy";
                childObject.transform.localPosition = pointConfig.Position;

                var buoyancyPoint = childObject.AddComponent<BuoyancyPoint>();
                buoyancyPoint.buoyancyForce = pointConfig.Force;
                buoyancyPoint.size = pointConfig.Size;

                return buoyancyPoint;
            }

            private static BaseEntity CreateDecoration(BuoyancyComponentManager manager, BaseEntity vehicle, DecorationConfig decorationConfig)
            {
                var decorationEntity = GameManager.server.CreateEntity(decorationConfig.PrefabPath, decorationConfig.Position, decorationConfig.Rotation);
                if (decorationEntity == null)
                    return null;

                var combatEntity = decorationEntity as BaseCombatEntity;
                if (combatEntity == null)
                {
                    manager.Plugin.LogError($"Decoration entity with prefab {decorationConfig.PrefabPath} does not derive from BaseCombatEntity.");
                    Destroy(combatEntity);
                    return null;
                }

                var mountable = decorationEntity as BaseMountable;
                if ((object)mountable != null)
                {
                    mountable.mountAnchor = null;
                    mountable.isMobile = false;
                }

                DestroyImmediate(combatEntity.gameObject.GetComponent<GroundWatch>());
                DestroyImmediate(combatEntity.gameObject.GetComponent<Buoyancy>());
                DestroyImmediate(combatEntity.gameObject.GetComponent<EntityCollisionMessage>());

                var rigidBody = combatEntity.gameObject.GetComponent<Rigidbody>();
                if (rigidBody != null)
                {
                    rigidBody.useGravity = false;
                    rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rigidBody.isKinematic = true;
                }

                foreach (var collider in combatEntity.gameObject.GetComponentsInChildren<Collider>())
                {
                    DestroyImmediate(collider);
                }

                foreach (var trigger in combatEntity.gameObject.GetComponentsInChildren<TriggerBase>())
                {
                    DestroyImmediate(trigger);
                }

                combatEntity.baseProtection = manager.Plugin._immortalProtection;
                combatEntity.pickup.enabled = false;
                combatEntity.EnableSaving(false);
                combatEntity.SetFlag(BaseEntity.Flags.Busy, true);
                combatEntity.SetFlag(BaseEntity.Flags.Disabled, decorationConfig.Dynamic);
                combatEntity.SetParent(vehicle);
                combatEntity.Spawn();
                return combatEntity;
            }

            public Buoyancy Buoyancy;
            private BuoyancyComponentManager _manager;
            private BaseEntity _entity;
            private ulong _netId;
            private IVehicleInfo _vehicleInfo;
            private BaseEntity[] _decorations;
            private bool _decorationsVisible;
            private Action _checkDecorations;
            private float _originalCenterOfMassY = float.MaxValue;
            private float _customCenterOfMassY = float.MaxValue;

            public void DestroyImmediate() => DestroyImmediate(this);

            private void CheckDecorations()
            {
                var shouldShowDecorations = Buoyancy.submergedFraction > 0;
                if (_decorationsVisible == shouldShowDecorations)
                    return;

                var pointConfigList = _vehicleInfo.Config.Points;
                var decorationIndex = 0;

                foreach (var pointConfig in pointConfigList)
                {
                    var decorationConfig = pointConfig.Decoration;
                    if (!decorationConfig.Enabled)
                        continue;

                    if (decorationConfig.Dynamic)
                    {
                        var decoration = _decorations[decorationIndex];
                        if (decoration != null && !decoration.IsDestroyed)
                        {
                            decoration.SetFlag(BaseEntity.Flags.Disabled, !shouldShowDecorations);
                        }
                    }

                    decorationIndex++;
                }

                _decorationsVisible = shouldShowDecorations;
            }

            private void RestoreDefaultDrag()
            {
                var rigidbody = _vehicleInfo.GetRigidBody(_entity);
                if (rigidbody == null)
                    return;

                rigidbody.drag = _vehicleInfo.DefaultDrag;
                rigidbody.angularDrag = _vehicleInfo.DefaultAngularDrag;
            }

            private void SetCenterOfMassY(float value)
            {
                // If the value is float.MaxValue, that means
                if (value == float.MaxValue)
                    return;

                var rigidbody = _vehicleInfo.GetRigidBody(_entity);
                if (rigidbody == null)
                    return;

                rigidbody.centerOfMass = rigidbody.centerOfMass.WithY(value);
            }

            private void SubmergedChanged(bool nowSubmerged)
            {
                if (nowSubmerged)
                {
                    SetCenterOfMassY(_customCenterOfMassY);

                    if (_checkDecorations != null)
                    {
                        _checkDecorations.Invoke();
                        CancelInvoke(_checkDecorations);
                    }
                }
                else
                {
                    RestoreDefaultDrag();
                    SetCenterOfMassY(_originalCenterOfMassY);

                    if (_checkDecorations != null)
                    {
                        Invoke(_checkDecorations, DecorationPersistSeconds);
                    }
                }
            }

            private void OnDestroy()
            {
                _manager.HandleComponentDestroyed(_entity);

                if (_entity == null || _entity.IsDestroyed)
                {
                    _manager.Plugin._data?.RemoveLastPilot(_netId);
                    return;
                }

                DestroyImmediate(Buoyancy);

                foreach (var point in Buoyancy.points)
                {
                    if (point == null)
                        continue;

                    DestroyImmediate(point.gameObject);
                }

                // Restore initial drag, but only if there aren't other buoyancy components attached.
                if (Buoyancy.submergedFraction > 0 && _entity.GetComponent<Buoyancy>() == null)
                {
                    RestoreDefaultDrag();
                }

                if (_decorations != null)
                {
                    foreach (var decorationEntity in _decorations)
                    {
                        if (decorationEntity != null && !decorationEntity.IsDestroyed)
                        {
                            decorationEntity.Kill();
                        }
                    }
                }

                ExposedHooks.OnVehicleBuoyancyRemoved(_entity);
            }
        }

        private class BuoyancyComponentManager
        {
            private const float DebugDrawDistance = 30;
            private const float DebugDrawDuration = 30;

            public readonly BuoyantHelicopters Plugin;
            private readonly Dictionary<BaseEntity, BuoyancyComponent> _buoyancyComponents = new Dictionary<BaseEntity, BuoyancyComponent>();
            private readonly HashSet<BaseEntity> _forciblyBuoyantHelicopters = new HashSet<BaseEntity>();

            public BuoyancyComponentManager(BuoyantHelicopters plugin)
            {
                Plugin = plugin;
            }

            public bool EnsureBuoyancy(BaseEntity vehicle, IVehicleInfo vehicleInfo, bool force = false)
            {
                if (_buoyancyComponents.ContainsKey(vehicle))
                {
                    if (force)
                    {
                        _forciblyBuoyantHelicopters.Add(vehicle);
                    }

                    return false;
                }

                var hookResult = ExposedHooks.OnVehicleBuoyancyAdd(vehicle);
                if (hookResult is bool && !(bool)hookResult)
                    return false;

                BuoyancyComponent component;
                if (!BuoyancyComponent.TryAddToEntity(this, vehicle, vehicleInfo, out component))
                    return false;

                _buoyancyComponents[vehicle] = component;

                if (force)
                {
                    _forciblyBuoyantHelicopters.Add(vehicle);
                }

                ExposedHooks.OnVehicleBuoyancyAdded(vehicle);

                if (Plugin._config.AdminDebug)
                {
                    ShowAdminDebug(vehicle, component);
                }

                return true;
            }

            public bool RemoveBuoyancy(BaseEntity vehicle)
            {
                var component = GetComponent(vehicle);
                if ((object)component == null)
                    return false;

                component.DestroyImmediate();
                return true;
            }

            public bool IsBuoyant(BaseEntity vehicle)
            {
                return _buoyancyComponents.ContainsKey(vehicle);
            }

            public bool IsForciblyBuoyant(BaseEntity vehicle)
            {
                return _forciblyBuoyantHelicopters.Contains(vehicle);
            }

            public BuoyancyComponent GetComponent(BaseEntity vehicle)
            {
                BuoyancyComponent component;
                return _buoyancyComponents.TryGetValue(vehicle, out component)
                    ? component
                    : null;
            }

            public void HandleComponentDestroyed(BaseEntity vehicle)
            {
                _buoyancyComponents.Remove(vehicle);
                _forciblyBuoyantHelicopters.Remove(vehicle);
            }

            public IEnumerator UnloadRoutine()
            {
                foreach (var component in _buoyancyComponents.Values.ToArray())
                {
                    component.DestroyImmediate();
                    yield return null;
                }
            }

            private void ShowAdminDebug(BaseEntity vehicle, BuoyancyComponent component)
            {
                var entityPosition = vehicle.transform.position;

                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.IsAdmin && Vector3.Distance(entityPosition, player.transform.position) <= DebugDrawDistance)
                    {
                        for (var i = 0; i < component.Buoyancy.points.Length; i++)
                        {
                            var point = component.Buoyancy.points[i];
                            var pointPosition = point.transform.position;
                            player.SendConsoleCommand("ddraw.sphere", DebugDrawDuration, Color.green, pointPosition, point.size);
                            player.SendConsoleCommand("ddraw.text", DebugDrawDuration, Color.green, pointPosition, $"{i+1}: {point.buoyancyForce}");
                        }
                    }
                }
            }
        }

        #endregion

        #region Data

        [JsonObject(MemberSerialization.OptIn)]
        private class SavedData
        {
            public static SavedData Load()
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<SavedData>(nameof(BuoyantHelicopters)) ?? new SavedData { _dirty = true };
                data.SaveIfChanged();
                return data;
            }

            private bool _dirty;

            [JsonProperty("LastPilot")]
            private Dictionary<ulong, string> LastPilotByVehicle = new Dictionary<ulong, string>();

            public bool TryGetLastPilot(BaseEntity vehicle, out string lastPilotId)
            {
                return LastPilotByVehicle.TryGetValue(vehicle.net.ID.Value, out lastPilotId);
            }

            public void SetLastPilot(BaseEntity vehicle, string userId)
            {
                var netId = vehicle.net.ID.Value;

                string lastPilotId;
                if (LastPilotByVehicle.TryGetValue(netId, out lastPilotId) && lastPilotId == userId)
                    return;

                LastPilotByVehicle[netId] = userId;
                _dirty = true;
            }

            public void RemoveLastPilot(ulong netId)
            {
                _dirty |= LastPilotByVehicle.Remove(netId);
            }

            public void SaveIfChanged()
            {
                if (!_dirty)
                    return;

                Interface.Oxide.DataFileSystem.WriteObject(nameof(BuoyantHelicopters), this);
                _dirty = false;
            }

            public void CleanEntitiesNotFound()
            {
                var netIdsToClean = new List<ulong>();

                foreach (var netId in LastPilotByVehicle.Keys)
                {
                    var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId));
                    if (entity == null || entity.IsDestroyed)
                    {
                        netIdsToClean.Add(netId);
                    }
                }

                _dirty |= netIdsToClean.Count > 0;

                foreach (var netId in netIdsToClean)
                {
                    LastPilotByVehicle.Remove(netId);
                }
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class DecorationConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled;

            [JsonProperty("Dynamic")]
            public bool Dynamic;

            [JsonProperty("Position")]
            public Vector3 Position;

            [JsonProperty("Prefab")]
            public string PrefabPath;

            [JsonProperty("Rotation angles")]
            public Vector3 RotationAngles;

            public Quaternion Rotation => Quaternion.Euler(RotationAngles);
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class PointConfig
        {
            [JsonProperty("Position")]
            public Vector3 Position;

            [JsonProperty("Size")]
            public float Size;

            [JsonProperty("Force")]
            public float Force;

            [JsonProperty("Decoration")]
            public DecorationConfig Decoration = new DecorationConfig();
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BuoyancyConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled;

            [JsonProperty("Require permission")]
            public bool RequirePermission;

            [JsonProperty("Require owner permission")]
            private bool DeprecatedRequireOwnerPermission { set { RequirePermission = value; } }

            [JsonProperty("Underwater drag")]
            public float UnderwaterDrag;

            [JsonProperty("Buoyancy points")]
            public PointConfig[] Points;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : BaseConfiguration
        {
            [JsonProperty("Admin debug")]
            public bool AdminDebug;

            [JsonProperty("Minicopter")]
            public BuoyancyConfig Minicopter = new BuoyancyConfig
            {
                Enabled = true,
                UnderwaterDrag = 2,
                Points = new[]
                {
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 650,
                        Position = new Vector3(0, -0.1f, 2.1f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(0, 0.25f, 1.75f),
                        },
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 950,
                        Position = new Vector3(1.18f, -0.1f, -0.73f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(1.18f, 0.3f, -0.73f),
                            RotationAngles = new Vector3(285, 270, 0),
                        }
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 950,
                        Position = new Vector3(-1.18f, -0.1f, -0.73f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(-1.18f, 0.3f, -0.73f),
                            RotationAngles = new Vector3(285, 90, 0),
                        }
                    },
                },
            };

            [JsonProperty("Scrap Transport Helicopter")]
            public BuoyancyConfig ScrapTransportHelicopter = new BuoyancyConfig
            {
                Enabled = true,
                UnderwaterDrag = 2,
                Points = new[]
                {
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 10200,
                        Position = new Vector3(0, 0.4f, 4.2f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(0f, 1, 4.6f),
                            RotationAngles = new Vector3(270, 180, 0),
                        }
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 5100,
                        Position = new Vector3(1.83f, 0.4f, -2.58f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(1.48f, 1.1f, 0.15f),
                            RotationAngles = new Vector3(270, 270, 0),
                        }
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 5100,
                        Position = new Vector3(-1.83f, 0.4f, -2.58f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(-1.48f, 1.1f, 0.15f),
                            RotationAngles = new Vector3(270, 90, 0),
                        }
                    },
                },
            };

            [JsonProperty("Attack Helicopter")]
            public BuoyancyConfig AttackHelicopter = new BuoyancyConfig
            {
                Enabled = true,
                UnderwaterDrag = 2,
                Points = new[]
                {
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 1790,
                        Position = new Vector3(1, 0.4f, 1.725f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(0.725f, 1, 0.02f),
                            RotationAngles = new Vector3(270, 270, 0),
                        }
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 1790,
                        Position = new Vector3(-1, 0.4f, 1.725f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(-0.725f, 1, 0.02f),
                            RotationAngles = new Vector3(270, 90, 0),
                        }
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 1790,
                        Position = new Vector3(1, 0.4f, -2.275f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = false,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                        }
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 1790,
                        Position = new Vector3(-1,0.4f, -2.275f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = false,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                        }
                    },
                }
            };

            #if FEATURE_CH47
            [JsonProperty("CH47 Helicopter")]
            public BuoyancyConfig CH47 = new BuoyancyConfig
            {
                Enabled = true,
                UnderwaterDrag = 2,
                Points = new[]
                {
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 1000,
                        Position = new Vector3(-1.5f, 0.85f, 3.4f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(-2.05f, 1.5f, 3.5f),
                            RotationAngles = new Vector3(270, 0, 90),
                        },
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 1000,
                        Position = new Vector3(1.5f, 0.85f, 3.5f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(2.0f, 1.5f, 3.4f),
                            RotationAngles = new Vector3(270, 0, 270),
                        },
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 1000,
                        Position = new Vector3(1.5f, 0.85f, -3.5f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(2.05f, 1.5f, -3.5f),
                            RotationAngles = new Vector3(270, 0, 270),
                        },
                    },
                    new PointConfig
                    {
                        Size = 0.2f,
                        Force = 1000,
                        Position = new Vector3(-1.5f, 0.85f, -3.5f),
                        Decoration = new DecorationConfig
                        {
                            Enabled = true,
                            Dynamic = true,
                            PrefabPath = InnerTubePrefab,
                            Position = new Vector3(-2.05f, 1.5f, -3.5f),
                            RotationAngles = new Vector3(270, 0, 90),
                        },
                    },
                },
            };
            #endif
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #region Configuration Helpers

        [JsonObject(MemberSerialization.OptIn)]
        private class BaseConfiguration
        {
            private string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(BaseConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigSection(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigSection(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigSection(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                PrintError(e.Message);
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion
    }
}
