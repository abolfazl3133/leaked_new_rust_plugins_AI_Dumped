using Facepunch;
using Network;
using Network.Visibility;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HeliCommander", "k1lly0u", "0.3.25")]
    [Description("Custom controllers and options for all types of helicopters")]
    class HeliCommander : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin Clans, Friends, RandomSpawns, Spawns;

        private RestoreData restoreData;
        private Dictionary<ulong, double> userCooldowns = new Dictionary<ulong, double>();
        private DynamicConfigFile restorationdata, cooldowns;

        private List<BaseController> spawnedHelis = new List<BaseController>();
        private List<BaseController> saveableHelis = new List<BaseController>();

        private static Controls Control;

        private static RaycastHit raycastHit;

        private const string UH1Y_PREFAB = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private const string CH47_PREFAB = "assets/prefabs/npc/ch47/ch47.entity.prefab";
        private const string MINI_PREFAB = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private const string TRANS_PREFAB = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";
        private const string CHAIR_PREFAB = "assets/prefabs/vehicle/seats/passenger.prefab";

        private const string FUEL_STORAGE = "assets/content/vehicles/minicopter/subents/fuel_storage.prefab";

        private const string ROCKET_NORMAL_PREFAB = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
        private const string ROCKET_NAPALM_PREFAB = "assets/prefabs/ammo/rocket/rocket_fire.prefab";
        private const string ROCKET_HV_PREFAB = "assets/prefabs/ammo/rocket/rocket_hv.prefab";

        private const string UI_HEALTH = "HCUI_Health";
        private const string UI_AMMO_MG = "HCUI_Ammo_MG";
        private const string UI_AMMO_ROCKET = "HCUI_Ammo_Rocket";
        private const string UI_FUEL = "HCUI_Fuel";
        private const string UI_AIM = "HCUI_Aim";

        private const string USE_PERMISSION = "helicommander.use.";
        private const string BUILD_PERMISSION = "helicommander.canbuild.";
        private const string SPAWN_PERMISSION = "helicommander.canspawn.";
        private const string COOLDOWN_PERMISSION = "helicommander.ignorecooldown.";
        private const string PASSENGER_PERMISSION = "helicommander.passenger.";

        private static GameObjectRef FUEL_STORAGE_REF; 
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Unsubscribe(nameof(OnEntitySpawned));

            foreach (HelicopterType type in Enum.GetValues(typeof(HelicopterType)) as HelicopterType[])
            {
                if (type == HelicopterType.Invalid)
                    continue;

                permission.RegisterPermission(string.Concat(BUILD_PERMISSION, type), this);
                permission.RegisterPermission(string.Concat(SPAWN_PERMISSION, type), this);
                permission.RegisterPermission(string.Concat(USE_PERMISSION, type), this);
                permission.RegisterPermission(string.Concat(PASSENGER_PERMISSION, type), this);
                permission.RegisterPermission(string.Concat(COOLDOWN_PERMISSION, type), this);
            }

            lang.RegisterMessages(Messages, this);

            GetMessage = Message;

            Control = new Controls();
        }

        private void OnServerInitialized()
        {
            FUEL_STORAGE_REF = GameManager.server.FindPrefab("assets/content/vehicles/minicopter/minicopter.entity.prefab").GetComponent<Minicopter>().fuelStoragePrefab;
            LoadData();

            if (Configuration.Mini.Convert || Configuration.Transport.Convert)
                Subscribe(nameof(OnEntitySpawned));

            restoreData.RestoreVehicles(SpawnAtLocation);

            timer.In(5, () => CheckForSpawns(HelicopterType.CH47));
            timer.In(10, () => CheckForSpawns(HelicopterType.UH1Y));
            timer.In(15, () => CheckForSpawns(HelicopterType.Mini));
            timer.In(20, () => CheckForSpawns(HelicopterType.Transport));
        }

        private void OnEntitySpawned(Minicopter miniCopter)
        {
            if (miniCopter == null)
                return;

            if ((miniCopter is ScrapTransportHelicopter && !Configuration.Transport.Convert) || !Configuration.Mini.Convert)
                return;

            if (!miniCopter.GetComponent<BaseController>() && !restoreData.HasRestoreData(miniCopter.net.ID))
            {
                NextTick(() =>
                {
                    if (miniCopter == null || miniCopter.GetComponent<BaseController>())
                        return;

                    MiniController controller = miniCopter is ScrapTransportHelicopter ? miniCopter.gameObject.AddComponent<TransportController>() :
                                                                                         miniCopter.gameObject.AddComponent<MiniController>();

                    saveableHelis.Add(controller);
                });
            }
        }

        private void OnSamSiteTargetScan(SamSite samsite, List<SamSite.ISamSiteTarget> list)
        {
            if (spawnedHelis.Count == 0 && saveableHelis.Count == 0)            
                return;

            for (int i = 0; i < spawnedHelis.Count; i++)
            {
                BaseController baseController = spawnedHelis[i];
                if (!(baseController is CH47Controller) && !(baseController is UH1YController))
                    continue;

                if (Vector3.Distance(baseController.Transform.position, samsite.transform.position) < SamSite.targetTypeVehicle.scanRadius)
                    list.Add(baseController as SamSite.ISamSiteTarget);
            }

            for (int i = 0; i < saveableHelis.Count; i++)
            {
                BaseController baseController = saveableHelis[i];
                if (!(baseController is CH47Controller) && !(baseController is UH1YController))
                    continue;

                if (Vector3.Distance(baseController.Transform.position, samsite.transform.position) < SamSite.targetTypeVehicle.scanRadius)
                    list.Add(baseController as SamSite.ISamSiteTarget);
            }
        }

        private object CanConvertToPilotEject(PatrolHelicopter baseHelicopter) => baseHelicopter != null && baseHelicopter.GetComponent<UH1YController>() != null ? (object)false : null;

        private void OnEntityKill(BaseNetworkable baseNetworkable)
        {
            BaseController baseController = baseNetworkable.GetComponent<BaseController>();
            if (baseController != null)
            {
                baseController.EjectAllPlayers();

                spawnedHelis.Remove(baseController);
                saveableHelis.Remove(baseController);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity baseCombatEntity, HitInfo hitInfo)
        {            
            if (baseCombatEntity == null || hitInfo == null) 
                return;

            BaseController baseController = baseCombatEntity.GetComponent<BaseController>();
            if (baseController != null)
            {
                baseController.ManageDamage(hitInfo);
            }
        }
        
        private void OnHammerHit(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || hitInfo == null || hitInfo.HitEntity == null)
                return;

            BaseController baseController = hitInfo.HitEntity.GetComponent<BaseController>();
            if (baseController != null)
                baseController.TryRepair(player);
        }        

        private void OnPlayerInput(BasePlayer player, InputState inputState)
        {
            if (player == null) 
                return;

            if (Control.JustPressed(player, BUTTON.USE))
            {
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out raycastHit, 3f))
                {
                    BaseController baseController = raycastHit.collider.GetComponentInParent<BaseController>();                    
                    if (baseController != null && baseController.Type == HelicopterType.UH1Y)
                    {
                        if (!baseController.IsOccupant(player))
                        {
                            BaseVehicle.MountPointInfo mountPointInfo = baseController.GetIdealMountPoint(player);
                            if (mountPointInfo != null && CanMountHelicopter(baseController, mountPointInfo, player) == null)
                            {
                                ForceMountPlayerTo(player, mountPointInfo.mountable, baseController);
                            }
                        }
                    }                       
                }
                return;
            }

            if (Control.JustPressed(player, Controls.Type.Inventory))
            {
                if (Physics.SphereCast(player.eyes.position, 0.1f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out raycastHit, 2f))
                {
                    BaseController baseController = raycastHit.collider.GetComponentInParent<BaseController>();
                    if (baseController != null && baseController.Config.Inventory.Enabled && baseController.Commander == null && !baseController.IsOccupant(player))
                    {
                        baseController.OpenInventory(player);
                    }
                }
                return;
            }

            if (Control.JustPressed(player, Controls.Type.FuelTank))
            {
                if (Physics.SphereCast(player.eyes.position, 0.1f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out raycastHit, 2f))
                {
                    BaseController baseController = raycastHit.collider.GetComponentInParent<BaseController>();
                    if (baseController != null && baseController.ConsumeFuel && baseController.Commander == null && !baseController.IsOccupant(player))
                    {
                        if ((int)baseController.Type < 3)
                        {
                            baseController.OpenFuel(player);
                        }
                    }
                }
                return;
            }
        }

        private void OnServerSave() 
        {
            restoreData.Clear();

            for (int i = saveableHelis.Count - 1; i >= 0; i--)
            {
                BaseController controller = saveableHelis[i];
                BaseCombatEntity baseCombatEntity = controller.GetEntity();

                if (controller == null || baseCombatEntity == null || !baseCombatEntity.IsValid() || baseCombatEntity.IsDestroyed)
                {
                    saveableHelis.RemoveAt(i);
                    continue;
                }
                restoreData.AddData(controller);
            }
            SaveData();
        }

        private void Unload()
        {            
            BaseController[] baseControllers = UnityEngine.Object.FindObjectsOfType<BaseController>();
            if (baseControllers != null)
            {
                foreach (BaseController baseController in baseControllers)
                {
                    BaseCombatEntity baseCombatEntity = baseController.GetEntity();

                    UnityEngine.Object.DestroyImmediate(baseController);

                    if (baseCombatEntity != null && !baseCombatEntity.IsDestroyed)
                        baseCombatEntity.Kill(BaseNetworkable.DestroyMode.None);
                }
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                DestroyAllUI(player);

            Configuration = null;
        }
        
        private object CanMountHelicopter(BaseController baseController, BaseVehicle.MountPointInfo mountPointInfo, BasePlayer player)
        {
            if (baseController != null)
            {
                if (player.isMounted)
                    return false;

                if (baseController.MountLocked)
                    return false;

                if (baseController.IsDying)
                    return false;

                bool canPilot = permission.UserHasPermission(player.UserIDString, string.Concat(USE_PERMISSION, baseController.Type));
                bool canPassenger = permission.UserHasPermission(player.UserIDString, string.Concat(PASSENGER_PERMISSION, baseController.Type));

                if (!canPilot && !canPassenger)
                {
                    SendReply(player, Message("nopermission", player.userID));
                    return false;
                }

                if (mountPointInfo != null)
                {
                    if (mountPointInfo.isDriver)
                    {
                        if (!canPilot)
                        {
                            SendReply(player, Message("noflypermission", player.userID));
                            return false;
                        }

                        return null;
                    }

                    if (baseController.Config.Passengers.Enabled)
                    {
                        if (baseController.Commander == null)                            
                            return null;        
                        
                        if (!baseController.Config.Passengers.UseFriends && !baseController.Config.Passengers.UseClans)                                
                            return null;
                                
                        if (IsFriendlyPlayer(baseController.Commander.userID, player.userID, baseController.Type))                                
                            return null;
                                
                        SendReply(player, Message("not_friend", player.userID));
                        return false;
                    }

                    SendReply(player, Message("passenger_not_enabled", player.userID));
                    return false;
                }
            }
            return null;           
        }
       
        private object CanMountEntity(BasePlayer player, BaseMountable baseMountable)
        {
            BaseController baseController = baseMountable.GetParentEntity()?.GetComponent<BaseController>();
            if (baseController != null && !baseController.IsOccupant(player))
            {
                BaseVehicle.MountPointInfo mountPointInfo = baseController.FindMountPointOf(baseMountable);
                if (mountPointInfo != null)                
                    return CanMountHelicopter(baseController, mountPointInfo, player);                
            }

            return null;
        }

        private void OnPlayerDismountFailed(BasePlayer player, BaseMountable baseMountable)
        {
            BaseEntity parentEntity = baseMountable.GetParentEntity();
            if (parentEntity != null && !parentEntity.IsDestroyed)
            {
                BaseController baseController = parentEntity.GetComponentInParent<BaseController>();
                if (baseController != null)
                {
                    if (baseController.MountLocked)                    
                        return;
                    
                    if (baseController.Type == HelicopterType.UH1Y)                    
                        ForceDismountPlayerFrom(player, baseMountable, baseController);                      
                }
            }
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable baseMountable)
        {
            BaseEntity parentEntity = baseMountable.GetParentEntity();
            if (parentEntity != null && !parentEntity.IsDestroyed)
            {
                BaseController baseController = parentEntity.GetComponentInParent<BaseController>();
                if (baseController != null)
                {
                    if (baseController.MountLocked)                    
                        return false;

                    if (baseController.Type == HelicopterType.UH1Y)
                    {
                        ForceDismountPlayerFrom(player, baseMountable, baseController);
                        return false;
                    }
                }
            }

            return null;
        }

        private void OnEntityMounted(BaseMountable baseMountable, BasePlayer player)
        {
            BaseEntity parentEntity = baseMountable.GetParentEntity();
            if (parentEntity != null && !parentEntity.IsDestroyed)
            {
                BaseController baseController = parentEntity.GetComponentInParent<BaseController>();
                if (baseController != null)
                {
                    baseController.OnPlayerMounted(baseMountable, player);
                }
            }
        }

        private void OnEntityDismounted(BaseMountable baseMountable, BasePlayer player)
        {
            BaseEntity parentEntity = baseMountable.GetParentEntity();
            if (parentEntity != null && !parentEntity.IsDestroyed)
            {
                BaseController baseController = parentEntity.GetComponentInParent<BaseController>();
                if (baseController != null)
                {
                    baseController.OnPlayerDismounted(baseMountable, player);
                    DestroyAllUI(player);
                }
            }
        }
        #endregion

        #region Functions
        private static T ParseType<T>(string type)
        {
            try
            {
                T value = (T)Enum.Parse(typeof(T), type, true);
                if (Enum.IsDefined(typeof(T), value))
                    return value;
            }
            catch
            {
                if (typeof(T) == typeof(BUTTON))
                    Debug.LogError($"INVALID CONFIG OPTION DETECTED! The value \"{type}\" is an incorrect selection.\nAvailable options are: {Enum.GetNames(typeof(T)).ToSentence()}");
                return default(T);
            }
            return default(T);
        }
        
        private class Controls
        {
            private Hash<Type, BUTTON> _controls = new Hash<Type, BUTTON>();

            public Controls()
            {
                _controls[Type.Lights] = ParseType<BUTTON>(Configuration.Buttons.Lights);
                _controls[Type.Inventory] = ParseType<BUTTON>(Configuration.Buttons.Inventory);
                _controls[Type.MG] = ParseType<BUTTON>(Configuration.Buttons.MG);
                _controls[Type.Rockets] = ParseType<BUTTON>(Configuration.Buttons.Rockets);
                _controls[Type.PitchF] = ParseType<BUTTON>(Configuration.Buttons.PitchForward);
                _controls[Type.PitchB] = ParseType<BUTTON>(Configuration.Buttons.PitchBackward);
                _controls[Type.RollL] = ParseType<BUTTON>(Configuration.Buttons.RollLeft);
                _controls[Type.RollR] = ParseType<BUTTON>(Configuration.Buttons.RollRight);
                _controls[Type.ThrottleUp] = ParseType<BUTTON>(Configuration.Buttons.ThrottleUp);
                _controls[Type.ThrottleDown] = ParseType<BUTTON>(Configuration.Buttons.ThrottleDown);
                _controls[Type.FuelTank] = ParseType<BUTTON>(Configuration.Buttons.FuelTank);
                _controls[Type.LockYaw] = ParseType<BUTTON>(Configuration.Buttons.LockYaw);
            }

            public bool IsDown(BasePlayer player, Type commandType)
            {
                BUTTON button = _controls[commandType];
                return player.serverInput.WasJustPressed(button) || player.serverInput.IsDown(button);
            }

            public bool IsDown(BasePlayer player, BUTTON button)
            {
                return player.serverInput.WasJustPressed(button) || player.serverInput.IsDown(button);
            }

            public bool JustPressed(BasePlayer player, Type commandType)
            {
                return player.serverInput.WasJustPressed(_controls[commandType]);
            }

            public bool JustPressed(BasePlayer player, BUTTON button)
            {
                return player.serverInput.WasJustPressed(button);
            }

            public BUTTON GetButtonOf(Type type) => _controls[type];

            public enum Type { Lights, Inventory, MG, Rockets, RollL, RollR, PitchF, PitchB, ThrottleUp, ThrottleDown, LockYaw, FuelTank }
        }
          
        private void CheckForSpawns(HelicopterType type)
        {
            ConfigData.PatrolHelicopter config = type == HelicopterType.CH47 ? Configuration.CH47 :
                                               type == HelicopterType.Transport ? Configuration.Transport as ConfigData.PatrolHelicopter :
                                               type == HelicopterType.Mini ? Configuration.Mini as ConfigData.PatrolHelicopter : 
                                               Configuration.UH1Y;

            if (config.Spawnable.Enabled)
            {
                if ((spawnedHelis.Count + saveableHelis.Count) < config.Spawnable.Max)
                {
                    object position = null;
                    if (config.Spawnable.RandomSpawns)
                    {
                        if (!RandomSpawns)
                        {
                            PrintError("RandomSpawns can not be found! Unable to autospawn helicopters");
                            return;
                        }

                        object success = RandomSpawns.Call("GetSpawnPoint");
                        if (success != null)
                            position = (Vector3)success;
                        else PrintError("Unable to find a valid spawnpoint from RandomSpawns");
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(config.Spawnable.Spawnfile))
                            return;

                        if (!Spawns)
                        {
                            PrintError("Spawns Database can not be found! Unable to autospawn helicopters");
                            return;
                        }
                        
                        object success = Spawns.Call("GetSpawnsCount", config.Spawnable.Spawnfile);
                        if (success is string)
                        {
                            PrintError($"An invalid {type} spawnfile has been set in the config. Unable to autospawn helicopters : " + (string)success);
                            return;
                        }

                        success = Spawns.Call("GetRandomSpawn", config.Spawnable.Spawnfile);
                        if (success is string)
                        {
                            PrintError((string)success);
                            return;
                        }
                        else position = (Vector3)success;
                    }

                    if (position != null)
                    {
                        int count = 0;
                        switch (type)
                        {                            
                            case HelicopterType.UH1Y:
                                {
                                    List<PatrolHelicopter> entities = Facepunch.Pool.Get<List<PatrolHelicopter>>();
                                    Vis.Entities((Vector3)position, 5f, entities);
                                    count = entities.Count;
                                    Facepunch.Pool.FreeUnmanaged(ref entities);
                                    if (count > 0)
                                    {
                                        timer.In(10, () => CheckForSpawns(type));
                                        return;
                                    }
                                    else SpawnAtLocation((Vector3)position, Quaternion.identity, type, config.Spawnable.Save);                                    
                                }
                                break;
                            case HelicopterType.CH47:
                                {
                                    List<CH47Helicopter> entities = Facepunch.Pool.Get<List<CH47Helicopter>>();
                                    Vis.Entities((Vector3)position, 5f, entities);
                                    count = entities.Count;
                                    Facepunch.Pool.FreeUnmanaged(ref entities);
                                    if (count > 0)
                                    {
                                        timer.In(10, () => CheckForSpawns(type));
                                        return;
                                    }
                                    else SpawnAtLocation((Vector3)position, Quaternion.identity, type, config.Spawnable.Save);
                                }
                                break;
                            case HelicopterType.Transport:
                            case HelicopterType.Mini:
                                {
                                    List<Minicopter> entities = Facepunch.Pool.Get<List<Minicopter>>();
                                    Vis.Entities((Vector3)position, 5f, entities);
                                    count = entities.Count;
                                    Facepunch.Pool.FreeUnmanaged(ref entities);
                                    if (count > 0)
                                    {
                                        timer.In(10, () => CheckForSpawns(type));
                                        return;
                                    }
                                    else SpawnAtLocation((Vector3)position, Quaternion.identity, type, config.Spawnable.Save);
                                }
                                break;
                        }                        
                    }
                }
                timer.In(config.Spawnable.Time, () => CheckForSpawns(type));
            }
        }

        private double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            int days = dateDifference.Days;
            int hours = dateDifference.Hours;
            hours += (days * 24);
            int mins = dateDifference.Minutes;
            int secs = dateDifference.Seconds;
            return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
        }

             
        private string GetPrefabPath(HelicopterType type)
        {
            switch (type)
            {               
                case HelicopterType.UH1Y:
                    return UH1Y_PREFAB;
                case HelicopterType.CH47:
                    return CH47_PREFAB;
                case HelicopterType.Mini:
                    return MINI_PREFAB;
                case HelicopterType.Transport:
                    return TRANS_PREFAB;
                case HelicopterType.Invalid:
                default:
                    return string.Empty;
            }
        }

        private int GetCooldown(HelicopterType type)
        {
            switch (type)
            {
                case HelicopterType.UH1Y:
                    return Configuration.UH1Y.Spawnable.Cooldown;
                case HelicopterType.CH47:
                    return Configuration.CH47.Spawnable.Cooldown;
                case HelicopterType.Mini:
                    return Configuration.Mini.Spawnable.Cooldown;
                case HelicopterType.Transport:
                    return Configuration.Transport.Spawnable.Cooldown;
                case HelicopterType.Invalid:
                default:
                    return 0;
            }
        }

        private List<ConfigData.PatrolHelicopter.BuildOptions.BuildOption> GetBuildCosts(HelicopterType type)
        {
            switch (type)
            {
                case HelicopterType.UH1Y:
                    return Configuration.UH1Y.Build.Costs;
                case HelicopterType.CH47:
                    return Configuration.CH47.Build.Costs;
                case HelicopterType.Mini:
                    return Configuration.Mini.Build.Costs;
                case HelicopterType.Transport:
                    return Configuration.Transport.Build.Costs;
                case HelicopterType.Invalid:
                default:
                    return new List<ConfigData.PatrolHelicopter.BuildOptions.BuildOption>();
            }
        }

        private bool HasBuildCooldown(HelicopterType type)
        {
            switch (type)
            {
                case HelicopterType.UH1Y:
                    return Configuration.UH1Y.Build.Cooldown;
                case HelicopterType.CH47:
                    return Configuration.CH47.Build.Cooldown;
                case HelicopterType.Mini:
                    return Configuration.Mini.Build.Cooldown;
                case HelicopterType.Transport:
                    return Configuration.Transport.Build.Cooldown;
                case HelicopterType.Invalid:
                default:
                    return true;
            }
        }

        private static BaseMountable SpawnMountPoint(BaseVehicle.MountPointInfo mountToSpawn, Model model, BaseCombatEntity parent)
        {
            Vector3 localForward = Quaternion.Euler(mountToSpawn.rot) * Vector3.forward;
            Vector3 position = mountToSpawn.pos;
            Vector3 localUp = Vector3.up;

            if (!string.IsNullOrEmpty(mountToSpawn.bone))
            {
                position = model.FindBone(mountToSpawn.bone).transform.position + parent.transform.TransformDirection(mountToSpawn.pos);
                localForward = parent.transform.TransformDirection(localForward);
                localUp = parent.transform.up;
            }

            BaseMountable baseMountable = GameManager.server.CreateEntity(CHAIR_PREFAB, position, Quaternion.LookRotation(localForward, localUp), true) as BaseMountable;
            if (baseMountable != null)
            {               
                if (string.IsNullOrEmpty(mountToSpawn.bone))                
                    baseMountable.SetParent(parent, false, false);                
                else baseMountable.SetParent(parent, mountToSpawn.bone, true, true);
                
                baseMountable.Spawn();
                mountToSpawn.mountable = baseMountable;
            }
            return baseMountable;
        }
        #endregion

        #region Component
        public enum HelicopterType { Invalid, UH1Y, CH47, Mini, Transport }

        private abstract class BaseController : MonoBehaviour
        {
            internal virtual HelicopterType Type => HelicopterType.Invalid;

            internal virtual ConfigData.PatrolHelicopter Config { get; }

            protected ConfigData.PatrolHelicopter.MovementSettings Movement { get; set; }

            internal EntityFuelSystem FuelSystem { get; set; }

            internal ItemContainer Inventory { get; private set; }

            protected Rigidbody Rigidbody { get; set; }

            internal Transform Transform { get; set; }

            internal BasePlayer Commander 
            {
                get 
                { 
                    return _commander; 
                } 
                set 
                { 
                    _commander = value;
                    OnCommanderSet(value);
                } 
            }
           

            internal abstract bool UseCustomControls { get; }

            internal bool MountLocked { get; set; }


            private BasePlayer _commander;

            protected List<BaseVehicle.MountPointInfo> mountPoints = Pool.Get<List<BaseVehicle.MountPointInfo>>();

            protected List<BasePlayer> occupants = Pool.Get<List<BasePlayer>>();

            internal int OccupantCount => occupants.Count;

            internal bool IsDying { get; private set; }


            protected virtual void OnDestroy()
            {
                EjectAllPlayers();

                DestroyAllMounts();

                Pool.FreeUnmanaged(ref mountPoints);
                Pool.FreeUnmanaged(ref occupants);
            }

            internal abstract BaseCombatEntity GetEntity();

            internal abstract bool IsGrounded();

            internal bool IsNearGround()
            {
                const int GROUND_LAYER = 1 << 16 | 1 << 21 | 1 << 23;
                return Physics.Raycast(new Ray(Transform.position, Vector3.down), 1f, GROUND_LAYER);
            }

            protected virtual void UpdateAimTarget() { }

            protected virtual void ToggleLights() { }

            #region Mounting
            internal bool IsOccupant(BasePlayer player) => occupants.Contains(player);

            internal List<BasePlayer> GetOccupants() => occupants;

            internal virtual void CreateMountPoints() { }


            internal BaseVehicle.MountPointInfo FindMountPointOf(BaseMountable baseMountable)
            {
                for (int i = 0; i < mountPoints.Count; i++)
                {
                    BaseVehicle.MountPointInfo mountPointInfo = mountPoints[i];
                    if (mountPointInfo.mountable == baseMountable)
                        return mountPointInfo;
                }
                return null;
            }

            internal BaseVehicle.MountPointInfo FindMountPoint(Func<BaseVehicle.MountPointInfo, bool> func)
            {
                for (int i = 0; i < mountPoints.Count; i++)
                {
                    BaseVehicle.MountPointInfo mountPointInfo = mountPoints[i];

                    if (func(mountPointInfo))
                        return mountPointInfo;
                }
                return null;
            }
                        
            internal virtual void OnPlayerMounted(BaseMountable baseMountable, BasePlayer player)
            {
                bool isDriver = FindMountPointOf(baseMountable).isDriver;

                if (isDriver)
                {
                    string message = UseCustomControls ? string.Format(GetMessage((int)Type > 2 ? "controls_new_mini" : "controls_new", player.userID),
                                                                                                Control.GetButtonOf(Controls.Type.PitchF),
                                                                                                Control.GetButtonOf(Controls.Type.PitchB),
                                                                                                Control.GetButtonOf(Controls.Type.RollL),
                                                                                                Control.GetButtonOf(Controls.Type.RollR),
                                                                                                Control.GetButtonOf(Controls.Type.ThrottleUp),
                                                                                                Control.GetButtonOf(Controls.Type.ThrottleDown),
                                                                                                Control.GetButtonOf(Controls.Type.LockYaw)) : string.Empty;

                    player.ChatMessage(message);
                    message = Type is HelicopterType.CH47 or HelicopterType.Transport ? $"{string.Format(GetMessage("toggle_lights", player.userID), Configuration.Buttons.Lights)}\n" : string.Empty;

                    if (this is UH1YController)
                    {
                        if ((this as UH1YController).GetWeapons()?.Rocket.Enabled ?? false)
                            message += $"{string.Format(GetMessage("fire_rocket", player.userID), Configuration.Buttons.Rockets)}\n";

                        if ((this as UH1YController).GetWeapons()?.MG.Enabled ?? false)
                            message += $"{string.Format(GetMessage("fire_mg", player.userID), Configuration.Buttons.MG)}\n";
                    }

                    if (Inventory != null)
                        message += $"{string.Format(GetMessage("access_inventory", player.userID), Configuration.Buttons.Inventory)}\n";

                    if (ConsumeFuel && GetFuelItem() != null)
                    {
                        if ((int)Type < 3)
                            message += $"{string.Format(GetMessage("access_fuel", player.userID), Control.GetButtonOf(Controls.Type.FuelTank))}\n";
                        message += $"{string.Format(GetMessage("fuel_type", player.userID), GetFuelItem().displayName.english)}";
                    }

                    if (!string.IsNullOrEmpty(message))
                        player.ChatMessage(message);

                    if (Config.Repair.Enabled && GetRepairItem() != null)
                        player.ChatMessage(string.Format(GetMessage("repairhelp", player.userID), Config.Repair.Amount, GetRepairItem().displayName.english));
                }
                else
                {
                    string message = string.Empty;

                    if (Inventory != null)
                        message += $"{string.Format(GetMessage("access_inventory", player.userID), Control.GetButtonOf(Controls.Type.Inventory))}\n";

                    if (ConsumeFuel && GetFuelItem() != null)
                    {
                        if ((int)Type < 3)
                            message += $"{string.Format(GetMessage("access_fuel", player.userID), Control.GetButtonOf(Controls.Type.FuelTank))}\n";
                        message += $"{string.Format(GetMessage("fuel_type", player.userID), GetFuelItem().displayName.english)}";
                    }

                    if (!string.IsNullOrEmpty(message))
                        player.ChatMessage(message);
                }

                OnEntityMounted(player, isDriver);
            }

            internal virtual void OnEntityMounted(BasePlayer player, bool isDriver)
            {
                occupants.Add(player);

                if (isDriver)
                {
                    Commander = player;

                    if ((int)Type < 3)
                    {
                        CreateHealthUI(player, this);

                        if (ConsumeFuel)
                            UpdateFuelUI();
                    }
                }
            }

            internal virtual void OnPlayerDismounted(BaseMountable mountable, BasePlayer player)
            {
                if (player == Commander)
                    Commander = null;

                occupants.Remove(player);
                DestroyAllUI(player);
            }

            protected virtual void OnCommanderSet(BasePlayer player) { }

            internal virtual void EjectAllPlayers()
            {
                if (mountPoints != null)
                {
                    for (int i = 0; i < mountPoints.Count; i++)
                        mountPoints[i].mountable.DismountAllPlayers();
                }
            }

            protected abstract void DestroyAllMounts();

            internal BaseVehicle.MountPointInfo GetIdealMountPoint(BasePlayer player)
            {
                BaseVehicle.MountPointInfo mountPointInfo = null;
                float closestDistance = float.PositiveInfinity;

                foreach (BaseVehicle.MountPointInfo mountPoint in mountPoints)
                {
                    if (mountPoint.mountable.AnyMounted())                    
                        continue;
                    
                    float distance = Vector3.Distance(mountPoint.mountable.mountAnchor.position, player.transform.position);
                    if (distance > closestDistance)                    
                        continue;

                    mountPointInfo = mountPoint;
                    closestDistance = distance;
                }
                return mountPointInfo;
            }
                              
            internal class DriverMount : MonoBehaviour
            {
                private BaseMountable baseMountable;

                private void Awake()
                {
                    baseMountable = GetComponent<BaseMountable>();

                    InvokeHandler.InvokeRepeating(baseMountable, UpdateHeldItems, 0.5f, 0.5f);
                }
                public void UpdateHeldItems()
                {
                    if (baseMountable._mounted == null)
                        return;

                    Item item = baseMountable._mounted.GetActiveItem();
                    if (item == null || item.GetHeldEntity() == null)
                        return;

                    int slot = item.position;
                    item.SetParent(null);
                    item.MarkDirty();

                    InvokeHandler.Invoke(baseMountable, () =>
                    {
                        if (item == null)
                            return;

                        item.SetParent(baseMountable._mounted.inventory.containerBelt);
                        item.position = slot;
                        item.MarkDirty();
                    }, 0.15f);
                }
            }
            #endregion

            #region Inventory
            protected void InitializeInventory()
            {
                if (Config.Inventory.Enabled)
                {
                    Inventory = new ItemContainer();
                    Inventory.ServerInitialize(null, Config.Inventory.Size);

                    if (Inventory.uid == default(ItemContainerId))
                        Inventory.GiveUID();
                }
            }

            internal virtual void OpenInventory(BasePlayer player) { }

            internal void DropInventory()
            {
                if (Inventory == null)
                    return;

                Inventory.Drop("assets/prefabs/misc/item drop/item_drop.prefab", 
                    (Transform.position + new Vector3(0f, 1.5f, 0f)) + (UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 3f)), Quaternion.identity, 0f);
            }
            #endregion

            #region Fuel
            private float lastFuelAmount;

            private bool fuelDisabled = false;

            internal bool ConsumeFuel => Config.Fuel.Enabled && !fuelDisabled;

            protected abstract ItemDefinition GetFuelItem();

            protected virtual void InitializeFuel() { }

            protected void CreateFuelSystem()
            {
                StorageContainer fuelStorage = GameManager.server.CreateEntity(FUEL_STORAGE, Transform.position, Quaternion.identity, true) as StorageContainer;
                fuelStorage.SetParent(GetEntity(), false, true);
                fuelStorage.transform.localPosition = Vector3.zero;
                fuelStorage.GetComponent<Collider>().enabled = false;
                fuelStorage.Spawn();

                FuelSystem = new EntityFuelSystem(true, FUEL_STORAGE_REF, new List<BaseEntity>());
                FuelSystem.fuelStorageInstance.Set(fuelStorage);

                ItemContainer itemContainer = FuelSystem.GetFuelContainer().inventory;
                itemContainer.onlyAllowedItems = new ItemDefinition[] { GetFuelItem() };

                if (!Config.Fuel.Enabled)
                    itemContainer.SetFlag(ItemContainer.Flag.IsLocked, true);
                else if (Config.Fuel.GiveFuel)
                    ItemManager.Create(GetFuelItem(), UnityEngine.Random.Range(Config.Fuel.FuelAmountMin, Config.Fuel.FuelAmountMax)).MoveToContainer(itemContainer);
            }

            internal virtual void OpenFuel(BasePlayer player) { }

            internal void DropFuel()
            {
                StorageContainer storageContainer = FuelSystem.GetFuelContainer();
                if (storageContainer == null)
                    return;

                storageContainer.inventory.Drop("assets/prefabs/misc/item drop/item_drop.prefab", 
                    (Transform.position + new Vector3(0f, 1.5f, 0f)) + (UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 3f)), Quaternion.identity, 0f);
            }

            protected void UpdateFuelUI()
            {
                if (Commander != null)
                {
                    int currentFuelAmount = FuelSystem.GetFuelAmount();
                    if (currentFuelAmount != lastFuelAmount)
                    {
                        CreateFuelUI(Commander, this, currentFuelAmount);
                        lastFuelAmount = currentFuelAmount;
                    }

                    InvokeHandler.Invoke(this, UpdateFuelUI, 1f);
                }                
            }

            internal void SetFuelRequirement(bool fuelEnabled)
            {
                fuelDisabled = !fuelEnabled;

                if (Commander != null)
                    DestroyUI(Commander, UI_FUEL);
            }
            #endregion

            #region Repair
            protected abstract ItemDefinition GetRepairItem();

            internal void TryRepair(BasePlayer player)
            {
                BaseCombatEntity baseCombatEntity = GetEntity();

                if (baseCombatEntity != null && Config.Repair.Enabled)
                {
                    if (baseCombatEntity.health < baseCombatEntity.MaxHealth())
                    {
                        ItemDefinition repairItem = GetRepairItem();

                        if (repairItem == null)
                        {
                            Debug.Log($"[HeliCommander] Attempted repair for type {GetType()} but invalid repair item set in config");
                            return;
                        }

                        if (player.inventory.GetAmount(repairItem.itemid) < Config.Repair.Amount)
                        {
                            player.ChatMessage(string.Format(GetMessage("noresources", player.userID), Config.Repair.Amount, repairItem.displayName.english));
                            return;
                        }

                        player.inventory.Take(null, repairItem.itemid, Config.Repair.Amount);
                        baseCombatEntity.Heal(Config.Repair.Damage);

                        player.Command("note.inv", new object[] { repairItem.itemid, Config.Repair.Amount * -1 });
                    }
                    else player.ChatMessage(GetMessage("fullhealth", player.userID));
                }
            }

            #endregion

            #region Death
            internal void ManageDamage(HitInfo info)
            {
                if (IsDying)
                    return;

                if (IsDead())
                {
                    OnDeath();
                    return;
                }

                foreach (KeyValuePair<DamageType, float> damageType in Config.Damage.Modifiers)
                {
                    if (info.damageTypes.Has(damageType.Key))
                    {
                        info.damageTypes.Scale(damageType.Key, damageType.Value);
                    }
                }

                if (info.damageTypes.Total() > GetEntity().health)
                {
                    OnDeath();
                    return;
                }

                if (Type is HelicopterType.Mini or HelicopterType.Transport)
                    return;

                foreach (BasePlayer occupant in occupants)
                    CreateHealthUI(occupant, this);
            }

            protected void StopToDie()
            {
                enabled = false;
                IsDying = true;

                Rigidbody.velocity = Vector3.zero;

                EjectAllPlayers();
                GetEntity().Invoke(OnDeath, 5f);
            }

            protected virtual bool IsDead() => false;

            protected virtual void OnDeath() 
            {
                enabled = false;

                IsDying = true;

                EjectAllPlayers();

                if (Config.Inventory.DropInv && Inventory != null)
                    DropInventory();
            }
            #endregion
        }

        private abstract class BaseController<T> : BaseController where T : BaseCombatEntity
        {
            public T Component { get; private set; }

            private bool isInvincible;

            protected bool isGrounded = false;

            protected HelicopterInput input = new HelicopterInput();

            #region Initialization
            protected virtual void Awake()
            {
                Component = GetComponent<T>();
                Transform = Component.transform;

                isInvincible = true;

                InvokeHandler.Invoke(this, () => { isInvincible = false; }, 3f);

                InitializeSettings();
                InitializeInventory();
                InitializeFuel();

                CreateMountPoints();
            }

            protected virtual void InitializeSettings()
            {
                Movement = Config.Movement;
                Component.InitializeHealth(Config.Damage.Health, Config.Damage.Health);
                Component.SendNetworkUpdate();
            }

            internal override BaseCombatEntity GetEntity() => Component;
            #endregion

            #region Inventory
            internal override void OpenInventory(BasePlayer player)
            {
                if (Config.Inventory.Enabled && Inventory != null)
                    OpenContainer(player, Inventory);
            }

            private void OpenContainer(BasePlayer player, ItemContainer container)
            {
                player.inventory.loot.Clear();
                player.inventory.loot.entitySource = Component;
                player.inventory.loot.itemSource = null;
                player.inventory.loot.AddContainer(container);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic");
                player.SendNetworkUpdate();
            }
            #endregion

            #region Fuel
            internal override void OpenFuel(BasePlayer player)
            {
                if (ConsumeFuel)
                    OpenContainer(player, FuelSystem.GetFuelContainer().inventory);
            }
            #endregion

            #region Movement
            private void FixedUpdate()
            {
                if (WaterLevel.Factor(Component.WorldSpaceBounds().ToBounds(), true, true, Component) > 0.7f)
                {
                    StopToDie();
                    return;
                }

                if (!UseCustomControls)
                    return;

                OnFixedUpdate();

                if (Vector3.Dot(Component.transform.up, Vector3.up) < 0)
                {
                    StopToDie();
                    return;
                }

                MovementUpdate();

                if (IsEngineOn() && (!Commander || (ConsumeFuel && !FuelSystem.HasFuel())))
                    EngineOff();

                if (IsEngineOn() && ConsumeFuel)
                    FuelSystem.TryUseFuel(Time.fixedDeltaTime, IsGrounded() ? Config.Fuel.Consumption * 0.1f : Config.Fuel.Consumption);
            }

            private void MovementUpdate()
            {
                if (input.lights)
                    ToggleLights();

                if (ConsumeFuel && !FuelSystem.HasFuel())
                    return;

                if (IsGrounded())
                    ApplyForceAtWheels();

                if (IsEngineOn() && !(IsGrounded() && input.throttle < 0f))
                {
                    LiftForce();
                    TiltForce();
                    YawForce();
                }
            }

            protected virtual void OnFixedUpdate() { }

            private void LateUpdate()
            {
                if (!UseCustomControls)
                    return;

                float levelPitch = pitchRoll.y > 0f ? -Time.deltaTime : pitchRoll.y < 0f ? Time.deltaTime : 0f;
                float levelRoll = pitchRoll.x > 0f ? -Time.deltaTime : pitchRoll.x < 0f ? Time.deltaTime : 0f;

                if (Commander == null)
                {
                    input.Clear();

                    pitchRoll.x = Mathf.Clamp(pitchRoll.x + levelPitch, -1f, 1f);
                    pitchRoll.y = Mathf.Clamp(pitchRoll.y + levelRoll, -1f, 1f);

                    tilt.x = Mathf.Lerp(tilt.x, pitchRoll.x * Movement.RollTilt, Time.deltaTime);
                    tilt.y = Mathf.Lerp(tilt.y, pitchRoll.y * Movement.PitchTilt, Time.deltaTime);
                    return;
                }

                input.lights = Control.JustPressed(Commander, Controls.Type.Lights);
                input.throttle = Control.IsDown(Commander, Controls.Type.ThrottleUp) ? 1f : Control.IsDown(Commander, Controls.Type.ThrottleDown) ? -1f : 0f;
                input.roll = Control.IsDown(Commander, Controls.Type.RollR) ? 1f : Control.IsDown(Commander, Controls.Type.RollL) ? -1f : 0f;
                input.pitch = Control.IsDown(Commander, Controls.Type.PitchF) ? 1f : Control.IsDown(Commander, Controls.Type.PitchB) ? -1f : 0f;

                input.lockYaw = Control.IsDown(Commander, Controls.Type.LockYaw);
                input.yaw = LookAngle();

                UpdateAimTarget();

                if (!IsEngineOn() && input.throttle > 0f)
                    EngineStartup();

                float pitchAmount = input.pitch > 0f ? Time.deltaTime : input.pitch < 0f ? -Time.deltaTime : levelPitch;
                float rollAmount = input.roll > 0f ? Time.deltaTime : input.roll < 0f ? -Time.deltaTime : levelRoll;

                pitchRoll.x = Mathf.Clamp(pitchRoll.x + rollAmount, -1f, 1f);
                pitchRoll.y = Mathf.Clamp(pitchRoll.y + pitchAmount, -1f, 1f);

                tilt.x = Mathf.Lerp(tilt.x, pitchRoll.x * Movement.RollTilt, Time.deltaTime);
                tilt.y = Mathf.Lerp(tilt.y, pitchRoll.y * Movement.PitchTilt, Time.deltaTime);
            }

            private const int GROUND_LAYERS = 1 << 0 | 1 << 16 | 1 << 21 | 1 << 23;
            
            private const int DESTROY_LAYERS = 1 << 26 | 1 << 30;

            private const int DAMAGE_LAYERS = 1 << 0 | 1 << 8 | 1 << 15 | 1 << 16 | 1 << 21 | 1 << 23 | 1 << 27 | 1 << 30;

            private float _nextDamageTime;

            protected void OnCollisionEnter(Collision collision)
            {
                if ((int)Type > 2)
                    return;

                int collisionMask = 1 << collision.gameObject.layer;

                if ((DESTROY_LAYERS | collisionMask) == DESTROY_LAYERS)
                {
                    collision.gameObject.ToBaseEntity().Kill(BaseNetworkable.DestroyMode.None);
                    return;
                }

                if ((GROUND_LAYERS | collisionMask) == GROUND_LAYERS)                
                    isGrounded = true;

                if (IsDying || isInvincible || Time.time < _nextDamageTime)
                    return;

                if ((DAMAGE_LAYERS | collisionMask) == DAMAGE_LAYERS)
                {
                    float force = collision.relativeVelocity.magnitude;
                    if (force > 5)
                    {
                        Component.Hurt(new HitInfo(Commander, Component, DamageType.Blunt, force * 150));

                        foreach (BasePlayer occupant in occupants)
                            CreateHealthUI(occupant, this);

                        _nextDamageTime = Time.time + 0.333f;
                    }
                }
            }

            private void OnCollisionExit(Collision collision)
            {
                if ((GROUND_LAYERS | (1 << collision.gameObject.layer)) == GROUND_LAYERS)
                    isGrounded = false;
            }
              
            protected virtual void EngineStartup() { }

            internal virtual void EngineOff() { }

            internal abstract bool IsEngineOn();

            protected virtual void ApplyForceAtWheels() { }            
            #endregion

            #region Flight Physics            
            private float currentThrottle;
            private float avgTerrainHeight;

            private Vector2 pitchRoll;
            private Vector2 tilt;
            private float turnRoll; 
           
            private void LiftForce()
            {
                if (!IsEngineOn())                
                    return;

                currentThrottle = Mathf.Lerp(currentThrottle, input.throttle, 2f * Time.fixedDeltaTime);
                currentThrottle = Mathf.Clamp(currentThrottle, -1f, 1f);

                float dotUp = Mathf.Clamp01(Vector3.Dot(Transform.up, Vector3.up));
                const float serviceCeiling = 250f;

                avgTerrainHeight = Mathf.Lerp(avgTerrainHeight, TerrainMeta.HeightMap.GetHeight(Transform.position), Time.fixedDeltaTime);
                float altitudeMultiplier = 1f - Mathf.InverseLerp(avgTerrainHeight + serviceCeiling - 20f, avgTerrainHeight + serviceCeiling, Transform.position.y);

                float dotMax = Mathf.InverseLerp(Movement.LiftDotMax, 1f, dotUp) * altitudeMultiplier;

                float altDotUp = 1f - Mathf.InverseLerp(Movement.ThrustDotMin, 1f, dotUp);
                float thrustForce = altDotUp / (Movement.PitchTilt / 100f);
                
                Vector3 lift = Vector3.up * (Movement.EngineLift * Movement.LiftFraction * currentThrottle * dotMax);
                Vector3 thrust = (Transform.up - Vector3.up).normalized * (Movement.EngineThrust * thrustForce);

                float hoverForce = Rigidbody.mass * -Physics.gravity.y;
               
                Rigidbody.AddForce(Transform.up * (hoverForce * dotMax * Movement.HoverForceScale), ForceMode.Force);

                Rigidbody.AddForce(lift, ForceMode.Force);

                Rigidbody.AddForce(thrust, ForceMode.Force);
            }

            private void TiltForce()
            {
                if (IsGrounded())
                    return;

                Rigidbody.transform.localRotation = Quaternion.Euler(tilt.y, Rigidbody.transform.localEulerAngles.y, -tilt.x);
            }

            private void YawForce()
            {
                if (IsGrounded())
                    return;

                if (!input.lockYaw && Mathf.Abs(input.yaw) > 0.2f)
                {
                    float force = Movement.Turn * input.yaw;
                    Rigidbody.AddRelativeTorque(0f, force * Rigidbody.mass, 0);
                }

                float rollForce = Movement.TurnTilt * Mathf.Lerp(pitchRoll.x, pitchRoll.x * Mathf.Abs(pitchRoll.y), Mathf.Max(0f, pitchRoll.y));
                turnRoll = Mathf.Lerp(turnRoll, rollForce, Time.fixedDeltaTime);
                Rigidbody.AddRelativeTorque(0f, turnRoll * Rigidbody.mass, 0);                
            }

            protected float LookAngle()
            {
                Vector3 eyeDirection = Vector3.ProjectOnPlane(Commander.eyes.HeadForward(), Transform.up);

                float angle = Mathf.Atan2(Vector3.Dot(Transform.up, Vector3.Cross(Transform.forward, eyeDirection)), Vector3.Dot(Transform.forward, eyeDirection)) * Mathf.Rad2Deg;

                return (Mathf.InverseLerp(-120f, 120f, angle) - 0.5f) * 2f;
            }            
            #endregion

            protected class HelicopterInput
            {
                public float throttle;
                public float roll;
                public float pitch;
                public float yaw;

                public bool lights;
                public bool lockYaw;

                public void Clear()
                {
                    throttle = 0;
                    roll = 0;
                    pitch = 0;
                    yaw = 0;

                    lights = false;
                    lockYaw = false;
                }
            }
        }

        private class MiniController : BaseController<Minicopter>
        {
            internal override HelicopterType Type => HelicopterType.Mini;

            internal override ConfigData.PatrolHelicopter Config => Configuration.Mini;

            internal override bool UseCustomControls => (Config as ConfigData.DefaultHelicopter).Custom;

            internal static readonly ItemDefinition FuelItem;
            internal static readonly ItemDefinition RepairItem;

            #region Initialization
            static MiniController()
            {
                FuelItem = ItemManager.itemList.Find(x => x.shortname == Configuration.Mini.Fuel.FuelType);
                RepairItem = ItemManager.itemList.Find(x => x.shortname == Configuration.Mini.Repair.Shortname);
            }

            protected override void Awake()
            {
                base.Awake();

                foreach (PlayerHelicopter.Wheel wheel in Component.wheels)
                {
                    Component.ApplyWheelForce(wheel.wheelCollider, 0f, 50, 0f);
                }
            }
           
            protected override void InitializeSettings()
            {
                base.InitializeSettings();

                Rigidbody = Component.rigidBody;

                if (UseCustomControls)
                    BaseMountable.AllMountables.Remove(Component);
            }
            #endregion

            #region Fuel
            protected override ItemDefinition GetFuelItem() => FuelItem;

            protected override void InitializeFuel()
            {                
                FuelSystem = Component.engineController.FuelSystem as EntityFuelSystem;

                ItemContainer itemContainer = FuelSystem.GetFuelContainer().inventory;
                itemContainer.onlyAllowedItems = new ItemDefinition[] { GetFuelItem() };

                if (!Config.Fuel.Enabled)
                {
                    Component.fuelPerSec = 0;
                    ItemManager.Create(GetFuelItem(), 2).MoveToContainer(itemContainer);
                    itemContainer.SetFlag(ItemContainer.Flag.IsLocked, true);
                }
                else
                {
                    if (Config.Fuel.GiveFuel)
                        ItemManager.Create(GetFuelItem(), UnityEngine.Random.Range(Config.Fuel.FuelAmountMin, Config.Fuel.FuelAmountMax)).MoveToContainer(itemContainer);

                    Component.fuelPerSec = Config.Fuel.Consumption;
                }
            }
            #endregion

            #region Movement
            protected override void ApplyForceAtWheels()
            {
                if (!Rigidbody)
                    return;

                float braking = (input.pitch == 0f ? 50f : 0f);
                float acceleration = input.pitch;
                float turning = input.roll;

                acceleration = acceleration * (Component.IsOn() ? 1f : 0f);
                foreach (PlayerHelicopter.Wheel wheel in Component.wheels)
                {
                    Component.ApplyWheelForce(wheel.wheelCollider, acceleration, braking, wheel.steering ? turning : 0f);
                }
            }

            protected override void OnFixedUpdate()
            {
                if (Component._mounted)
                {
                    Component._mounted.transform.rotation = Component.mountAnchor.transform.rotation;
                    Component._mounted.ServerRotation = Component.mountAnchor.transform.rotation;
                    Component._mounted.MovePosition(Component.mountAnchor.transform.position);
                }

                if (Component.clippingChecks != BaseVehicle.ClippingCheckMode.OnMountOnly && Component.AnyMounted())
                {
                    Vector3 center = Transform.TransformPoint(Component.bounds.center);
                    
                    int mask = (Component.IsFlipped() ? 1218511105 : 1210122497);
                    if (Component.checkVehicleClipping)
                        mask |= 8192;
                    
                    if (Physics.OverlapBox(center, Component.bounds.extents, Component.transform.rotation, mask).Length != 0)
                        Component.CheckSeatsForClipping();
                }
                
                if (Rigidbody)
                    Component.SetFlag(BaseEntity.Flags.Reserved7, (!Rigidbody.IsSleeping() ? false : !Component.AnyMounted()), false, true);

                if (Component.OnlyOwnerAccessible() && Component.safeAreaRadius != -1f && Vector3.Distance(Transform.position, Component.safeAreaOrigin) > Component.safeAreaRadius)
                    Component.ClearOwnerEntry();

                Component.EnableGlobalBroadcast(Component.IsEngineOn());

                GameObject[] killTriggers = Component.killTriggers;
                for (int i = 0; i < killTriggers.Length; i++)
                    killTriggers[i].SetActive(Rigidbody.velocity.y < 0f);

                Component.lastPlayerInputTime = Time.time;
            }
            #endregion

            #region Mounting
            internal override void CreateMountPoints()
            {
                mountPoints = Component.mountPoints;

                if (UseCustomControls)
                {
                    for (int i = 0; i < mountPoints.Count; i++)
                    {
                        BaseVehicle.MountPointInfo mountPointInfo = mountPoints[i];
                        if (mountPointInfo.isDriver)
                        {
                            if (mountPointInfo.mountable != null && !mountPointInfo.mountable.IsDestroyed)
                                mountPointInfo.mountable.Kill(BaseNetworkable.DestroyMode.None);

                            SpawnMountPoint(mountPointInfo, Component.model, Component).gameObject.AddComponent<DriverMount>();
                        }
                    }
                }
            }

            protected override void DestroyAllMounts()
            {
                if (UseCustomControls)
                {
                    for (int i = 0; i < mountPoints.Count; i++)
                    {
                        BaseVehicle.MountPointInfo mountPointInfo = mountPoints[i];
                        if (mountPointInfo.isDriver)
                        {
                            if (mountPointInfo.mountable != null && !mountPointInfo.mountable.IsDestroyed)
                                mountPointInfo.mountable.Kill(BaseNetworkable.DestroyMode.None);

                            if (!Component.IsDestroyed)
                                Component.SpawnMountPoint(mountPointInfo, Component.model);
                        }
                    }
                }
            }

            protected override void OnCommanderSet(BasePlayer player)
            {
                Component._mounted = player;
            }
            #endregion

            #region Functions
            internal override bool IsEngineOn() => Component.IsOn();

            internal override bool IsGrounded() => Component.IsGrounded();

            protected override void EngineStartup()
            {
                if (!Component.IsInvoking(Component.engineController.FinishStartingEngine))
                {
                    Component.SetFlag(Component.engineController.engineStartingFlag, true, false, true);
                    Component.SetFlag(BaseEntity.Flags.On, false, false, true);
                    Component.Invoke(Component.engineController.FinishStartingEngine, Component.engineController.engineStartupTime);
                }
            }

            internal override void EngineOff()
            {
                if (!Component.IsOn())
                    return;

                Component.engineController.CancelEngineStart();
                Component.SetFlag(BaseEntity.Flags.On, false, false, true);
                Component.SetFlag(Component.engineController.engineStartingFlag, false, false, true);
            }

            protected override void ToggleLights()
            {
                Component.SetFlag(BaseEntity.Flags.Reserved6, !Component.HasFlag(BaseEntity.Flags.Reserved6), false, true);
            }
            #endregion

            #region Damage and Destruction 
            protected override bool IsDead() => Component.health <= 0f;

            protected override void OnDeath()
            {
                base.OnDeath();
                Component.Die();
                //Destroy(this);
            }

            protected override void OnDestroy()
            {
                base.OnDestroy();

                if (Component != null && !Component.IsDestroyed)
                    Component.Kill(BaseNetworkable.DestroyMode.None);
            }
            #endregion

            #region Repair
            protected override ItemDefinition GetRepairItem() => RepairItem;
            #endregion
        }

        private class TransportController : MiniController
        {
            internal override HelicopterType Type => HelicopterType.Transport;

            internal override ConfigData.PatrolHelicopter Config => Configuration.Transport;

            internal static readonly new ItemDefinition FuelItem;
            internal static readonly new ItemDefinition RepairItem;

            static TransportController()
            {
                FuelItem = ItemManager.itemList.Find(x => x.shortname == Configuration.Transport.Fuel.FuelType);
                RepairItem = ItemManager.itemList.Find(x => x.shortname == Configuration.Transport.Repair.Shortname);
            }

            protected override void InitializeSettings()
            {
                base.InitializeSettings();

                if (UseCustomControls)
                    Rigidbody.mass = Rigidbody.mass * 0.25f;
            }

            #region Repair
            protected override ItemDefinition GetRepairItem() => RepairItem;
            #endregion

            #region Fuel
            protected override ItemDefinition GetFuelItem() => FuelItem;
            #endregion
        }


        private class CH47Controller : BaseController<CH47Helicopter>, SamSite.ISamSiteTarget
        {
            internal override HelicopterType Type => HelicopterType.CH47;

            internal override ConfigData.PatrolHelicopter Config => Configuration.CH47;

            internal override bool UseCustomControls => (Config as ConfigData.DefaultHelicopter).Custom;

            internal static readonly ItemDefinition FuelItem;
            internal static readonly ItemDefinition RepairItem;

            #region Initialization
            static CH47Controller()
            {
                FuelItem = ItemManager.itemList.Find(x => x.shortname == Configuration.CH47.Fuel.FuelType);
                RepairItem = ItemManager.itemList.Find(x => x.shortname == Configuration.CH47.Repair.Shortname);
            }

            protected override void InitializeSettings()
            {
                base.InitializeSettings();

                Component.mapMarkerInstance.Kill();

                Rigidbody = Component.rigidBody;

                if (UseCustomControls)
                {
                    Rigidbody.centerOfMass = Rigidbody.centerOfMass + (-Vector3.up * 1.5f);

                    Component.nextDamageTime = float.MaxValue;

                    BaseMountable.AllMountables.Remove(Component);
                }
            }
            #endregion

            #region Mounting
            internal override void CreateMountPoints()
            {
                mountPoints = Component.mountPoints;

                if (UseCustomControls)
                {
                    for (int i = 0; i < mountPoints.Count; i++)
                    {
                        BaseVehicle.MountPointInfo mountPointInfo = mountPoints[i];
                        if (mountPointInfo.isDriver)
                        {
                            if (mountPointInfo.mountable != null && !mountPointInfo.mountable.IsDestroyed)
                                mountPointInfo.mountable.Kill(BaseNetworkable.DestroyMode.None);

                            SpawnMountPoint(mountPointInfo, Component.model, Component).gameObject.AddComponent<DriverMount>();
                        }
                    }
                }
            }

            protected override void DestroyAllMounts()
            {
                if (UseCustomControls)
                {
                    for (int i = 0; i < mountPoints.Count; i++)
                    {
                        BaseVehicle.MountPointInfo mountPointInfo = mountPoints[i];
                        if (mountPointInfo.isDriver)
                        {
                            if (mountPointInfo.mountable != null && !mountPointInfo.mountable.IsDestroyed)
                                mountPointInfo.mountable.Kill(BaseNetworkable.DestroyMode.None);

                            if (!Component.IsDestroyed)
                                Component.SpawnMountPoint(mountPointInfo, Component.model);
                        }
                    }
                }
            }

            protected override void OnCommanderSet(BasePlayer player)
            {
                Component._mounted = player;
            }
            #endregion

            #region Functions 
            internal override bool IsEngineOn() => true;

            internal override bool IsGrounded() => isGrounded || IsNearGround();

            protected override void ToggleLights()
            {
                Component.SetFlag(BaseEntity.Flags.Reserved5, !Component.HasFlag(BaseEntity.Flags.Reserved5), false);
                Component.SetFlag(BaseEntity.Flags.Reserved6, !Component.HasFlag(BaseEntity.Flags.Reserved6), false);
            }

            protected override void OnFixedUpdate()
            {
                if (Component._mounted)
                {
                    Component._mounted.transform.rotation = Component.mountAnchor.transform.rotation;
                    Component._mounted.ServerRotation = Component.mountAnchor.transform.rotation;
                    Component._mounted.MovePosition(Component.mountAnchor.transform.position);
                }

                if (Component.clippingChecks != BaseVehicle.ClippingCheckMode.OnMountOnly && Component.AnyMounted())
                {
                    Vector3 center = Transform.TransformPoint(Component.bounds.center);
                    
                    int mask = (Component.IsFlipped() ? 1218511105 : 1210122497);
                    if (Component.checkVehicleClipping)
                        mask |= 8192;
                    
                    if (Physics.OverlapBox(center, Component.bounds.extents, Component.transform.rotation, mask).Length != 0)
                        Component.CheckSeatsForClipping();
                }
                
                if (Rigidbody)
                    Component.SetFlag(BaseEntity.Flags.Reserved7, (!Rigidbody.IsSleeping() ? false : !Component.AnyMounted()), false, true);

                if (Component.OnlyOwnerAccessible() && Component.safeAreaRadius != -1f && Vector3.Distance(Transform.position, Component.safeAreaOrigin) > Component.safeAreaRadius)
                    Component.ClearOwnerEntry();

                Component.EnableGlobalBroadcast(Component.IsEngineOn());

                GameObject[] killTriggers = Component.killTriggers;
                for (int i = 0; i < killTriggers.Length; i++)
                    killTriggers[i].SetActive(Rigidbody.velocity.y < 0f);

                Component.lastPlayerInputTime = Time.time;
            }
            #endregion

            #region Damage and Destruction 
            protected override bool IsDead() => Component.health <= 0f;

            protected override void OnDeath()
            {
                base.OnDeath();

                if (Config.Inventory.DropLoot)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        Vector3 randomOnSphere = UnityEngine.Random.onUnitSphere;

                        BaseEntity lootCrate = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/heli_crate.prefab", (Transform.position + new Vector3(0f, 1.5f, 0f)) + (randomOnSphere * UnityEngine.Random.Range(2f, 3f)), Quaternion.LookRotation(randomOnSphere), true);
                        lootCrate.Spawn();

                        LootContainer lootContainer = lootCrate as LootContainer;
                        if (lootContainer)
                            lootContainer.Invoke(lootContainer.RemoveMe, 1800f);

                        Collider collider = lootCrate.GetComponent<Collider>();
                        Rigidbody rigidbody = lootCrate.gameObject.AddComponent<Rigidbody>();
                        rigidbody.useGravity = true;
                        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rigidbody.mass = 2f;
                        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                        rigidbody.velocity = Vector3.zero + (randomOnSphere * UnityEngine.Random.Range(1f, 3f));
                        rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                        rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                        rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);

                        FireBall fireBall = GameManager.server.CreateEntity(Component.fireBall.resourcePath, lootCrate.transform.position, Quaternion.identity, true) as FireBall;
                        if (fireBall)
                        {
                            fireBall.transform.position = lootCrate.transform.position;
                            fireBall.Spawn();
                            fireBall.GetComponent<Rigidbody>().isKinematic = true;
                            fireBall.GetComponent<Collider>().enabled = false;
                            fireBall.transform.parent = lootCrate.transform;
                        }
                    }
                }

                Component.Die();
            }

            protected override void OnDestroy()
            {
                base.OnDestroy();

                if (Component != null && !Component.IsDestroyed)
                    Component.Kill(BaseNetworkable.DestroyMode.None);
            }
            #endregion

            #region Repair
            protected override ItemDefinition GetRepairItem() => RepairItem;
            #endregion

            #region Fuel
            protected override ItemDefinition GetFuelItem() => FuelItem;

            protected override void InitializeFuel() => CreateFuelSystem();
            #endregion

            #region Samsite targeting
            public SamSite.SamTargetType SAMTargetType => SamSite.targetTypeVehicle;

            public bool isClient => Component.isClient;

            public bool IsValidSAMTarget(bool staticRespawn) => !Component.InSafeZone();
            
            public Vector3 CenterPoint() => Component.CenterPoint();

            public Vector3 GetWorldVelocity() => Component.GetWorldVelocity();

            public bool IsVisible(Vector3 position, float maxDistance = float.PositiveInfinity) => Component.IsVisible(position, maxDistance);
            #endregion
        }

        private class UH1YController : BaseController<PatrolHelicopter>, SamSite.ISamSiteTarget
        {
            #region Variables       
            private PatrolHelicopterAI heliAi;

            private float lastFireRocket;
            private float lastFireMG;
            private float lastNoAmmoRocket;
            private float lastNoAmmoMG;
            private bool lastRocketLeft;

            private uint groupId = 0;

            internal override HelicopterType Type => HelicopterType.UH1Y;

            internal override ConfigData.PatrolHelicopter Config => Configuration.UH1Y;

            internal override bool UseCustomControls => true;

            internal static readonly ItemDefinition FuelItem;
            internal static readonly ItemDefinition RepairItem;

            private static readonly ItemDefinition RocketItem;
            private static readonly ItemDefinition MGItem;

            private const int MG_FIRE_LAYERS = 1 << 0 | 1 << 4 | 1 << 8 | 1 << 16 | 1 << 17 | 1 << 21 | 1 << 23 | 1 << 27 | 1 << 30;
            #endregion

            #region Initialization
            static UH1YController()
            {
                FuelItem = ItemManager.itemList.Find(x => x.shortname == Configuration.UH1Y.Fuel.FuelType);
                RepairItem = ItemManager.itemList.Find(x => x.shortname == Configuration.UH1Y.Repair.Shortname);
                RocketItem = ItemManager.itemList.Find(x => x.shortname == Configuration.UH1Y.Weapons.Rocket.Type);
                MGItem = ItemManager.itemList.Find(x => x.shortname == Configuration.UH1Y.Weapons.MG.Type);
            }

            protected override void Awake()
            {
                base.Awake();

                Component.enabled = false;

                heliAi = Component.GetComponent<PatrolHelicopterAI>();
                heliAi.enabled = false;
                heliAi.CancelInvoke(heliAi.UpdateWind);

                Component.GetComponentInChildren<MeshCollider>().convex = true;

                Component.gameObject.layer = (int)Layer.Vehicle_World;// (int)Layer.Reserved3;

                Component.transform.GetChild(0).GetChild(0).gameObject.layer = (int)Layer.Vehicle_World;//27;

                InvokeHandler.InvokeRepeating(Component, UpdateNetworkGroup, 0f, 2f);
            }

            protected override void OnDestroy()
            {
                base.OnDestroy();

                if (Component != null && !Component.IsDestroyed)
                {
                    if (Component.IsInvoking(UpdateNetworkGroup))
                        Component.CancelInvoke(UpdateNetworkGroup);

                    Component.health = 0;
                    Component.lifestate = BaseCombatEntity.LifeState.Dead;

                    NetWrite netWrite = Net.sv.StartWrite();
                    netWrite.PacketID(Network.Message.Type.EntityDestroy);
                    netWrite.EntityID(Component.net.ID);
                    netWrite.UInt8((byte)BaseNetworkable.DestroyMode.None);
                    netWrite.Send(new SendInfo(BasePlayer.activePlayerList.Select(x => x.Connection).ToList()));
                    
                    Component.Kill(BaseNetworkable.DestroyMode.None);
                }
            }

            protected override void InitializeSettings()
            {
                base.InitializeSettings();

                Rigidbody = Component.GetComponent<Rigidbody>();
                Rigidbody.isKinematic = false;
                Rigidbody.useGravity = true;
                Rigidbody.mass = 400;
                Rigidbody.drag = 0.375f;
                Rigidbody.angularDrag = 1.125f;
                Rigidbody.centerOfMass = new Vector3(0f, 1f, 0.5f);
                Rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
            #endregion

            #region Fuel
            protected override void InitializeFuel() => CreateFuelSystem();

            protected override ItemDefinition GetFuelItem() => FuelItem;
            #endregion

            #region Mounting
            internal override void CreateMountPoints()
            {
                mountPoints = new List<BaseVehicle.MountPointInfo>();

                CreateMountPoint(new Vector3(-0.6f, 0.65f, 3.1f), new Vector3(-2f, 0.1f, 0f), Vector3.zero, true);
                
                if (Config.Passengers.Enabled)
                {
                    CreateMountPoint(new Vector3(0.6f, 0.65f, 3.1f), new Vector3(2f, 0.1f, 0f));
                    CreateMountPoint(new Vector3(-1.2f, 0.68f, -0.4f), new Vector3(0f, 0.1f, 2f), new Vector3(0, -90f, 0f));
                    CreateMountPoint(new Vector3(1.2f, 0.68f, -0.4f), new Vector3(0f, 0.1f, 2f), new Vector3(0, 90f, 0f));
                    CreateMountPoint(new Vector3(-0.3f, 0.68f, 1f), new Vector3(-2.5f, 0.1f, 0f));
                    CreateMountPoint(new Vector3(0.3f, 0.68f, 1f), new Vector3(2.5f, 0.1f, 0f));
                }
            }

            private void CreateMountPoint(Vector3 localPosition, Vector3 dismountPosition, Vector3 localRotation = default(Vector3), bool isDriver = false)
            {
                BaseVehicle.MountPointInfo mountPointInfo = new BaseVehicle.MountPointInfo() { pos = localPosition, rot = localRotation, isDriver = isDriver };

                BaseMountable baseMountable = SpawnMountPoint(mountPointInfo, Component.model, Component);

                if (isDriver)
                    baseMountable.gameObject.AddComponent<DriverMount>();

                Transform t = new GameObject("DismountPosition").transform;
                t.SetParent(baseMountable.transform, false);
                t.localPosition = dismountPosition;
                
                baseMountable.dismountPositions = new Transform[] { t }; 

                mountPoints.Add(mountPointInfo);
            }

            protected override void DestroyAllMounts()
            {
                for (int i = 0; i < mountPoints.Count; i++)
                {
                    BaseVehicle.MountPointInfo mountPointInfo = mountPoints[i];
                    if (mountPointInfo != null && mountPointInfo.mountable != null && !mountPointInfo.mountable.IsDestroyed)
                        mountPointInfo.mountable.Kill(BaseNetworkable.DestroyMode.None);
                }
            }

            internal override void EjectAllPlayers()
            {
                if (mountPoints != null)
                {
                    for (int i = 0; i < mountPoints.Count; i++)
                    {
                        BaseVehicle.MountPointInfo mountPointInfo = mountPoints[i];
                        if (mountPointInfo != null && mountPointInfo.mountable != null && mountPointInfo.mountable._mounted != null)
                        {
                            ForceDismountPlayerFrom(mountPointInfo.mountable._mounted, mountPointInfo.mountable, this);
                        }
                    }
                }
            }
            #endregion

            #region Movement
            private void UpdateNetworkGroup()
            {
                Group group = Net.sv.visibility.GetGroup(Component.transform.position);
                if (group.ID != groupId)
                {
                    groupId = group.ID;
                    Component.net.SwitchGroup(group);
                }
            }
                        
            #endregion

            #region Weapons
            internal ConfigData.AttackHelicopter.WeaponOptions GetWeapons() => (Config as ConfigData.AttackHelicopter).Weapons;

            protected override void UpdateAimTarget()
            {
                if (!Commander)
                    return;

                if (GetWeapons().Rocket.Enabled && Control.JustPressed(Commander, Controls.Type.Rockets))
                    FireRockets();

                if (GetWeapons().MG.Enabled && Control.IsDown(Commander, Controls.Type.MG))
                {
                    Vector3 position;

                    if (Physics.Raycast(new Ray(Commander.transform.position, Quaternion.Euler(Commander.serverInput.current.aimAngles) * Vector3.forward), out raycastHit, 2000f, MG_FIRE_LAYERS))
                        position = raycastHit.point;
                    else position = Commander.transform.position + (Quaternion.Euler(Commander.serverInput.current.aimAngles) * (Vector3.forward * 15));

                    FireMG(LookAngle() < 0f ? false : true, position);

                    Component.spotlightTarget = position;
                }
            }

            private void FireRockets()
            {
                int inventoryCount = Inventory.GetAmount(RocketItem.itemid, false);

                if (GetWeapons().Rocket.RequireAmmo && inventoryCount == 0)
                {
                    if (Time.realtimeSinceStartup >= lastNoAmmoRocket)
                    {
                        if (RocketItem)
                            Commander.ChatMessage(string.Format(GetMessage("no_ammo_rocket", Commander.userID), RocketItem.displayName.english));
                        else Debug.Log($"[HeliCommander] Invalid ammo type for the rocket launchers set in config: {GetWeapons().Rocket.Type}");

                        lastNoAmmoRocket = Time.realtimeSinceStartup + 3;
                    }
                    return;
                }

                if (Time.realtimeSinceStartup >= lastFireRocket)
                {
                    lastRocketLeft = !lastRocketLeft;

                    Vector3 launchPos = (lastRocketLeft ? Component.rocket_tube_right.transform : Component.rocket_tube_left.transform).position + (Transform.forward * 2f);

                    const string ROCKET_TUBE_RIGHT = "rocket_tube_right";
                    const string ROCKET_TUBE_LEFT = "rocket_tube_left";
                    const string ROCKET_BASIC = "ammo.rocket.basic";
                    const string ROCKET_FIRE = "ammo.rocket.fire";

                    Effect.server.Run(Component.rocket_fire_effect.resourcePath, Component, StringPool.Get((lastRocketLeft ? ROCKET_TUBE_RIGHT : ROCKET_TUBE_LEFT)), Vector3.zero, Vector3.forward, null, true);
                    
                    BaseEntity rocketEnt = GameManager.server.CreateEntity(RocketItem.shortname == ROCKET_BASIC ? ROCKET_NORMAL_PREFAB : 
                                                                           RocketItem.shortname == ROCKET_FIRE ? ROCKET_NAPALM_PREFAB : 
                                                                           ROCKET_HV_PREFAB, launchPos, Quaternion.identity, true);

                    TimedExplosive timedExplosive = rocketEnt.GetComponent<TimedExplosive>();
                    ServerProjectile serverProjectile = rocketEnt.GetComponent<ServerProjectile>();

                    serverProjectile.gravityModifier = 0;                    
                    timedExplosive.timerAmountMin = 300;
                    timedExplosive.timerAmountMax = 300;

                    serverProjectile.InitializeVelocity(Transform.forward.normalized * serverProjectile.speed);
                    rocketEnt.Spawn();

                    TimedExplosive projectile = rocketEnt.GetComponent<TimedExplosive>();
                    if (projectile)
                        projectile.damageTypes.Add(new DamageTypeEntry { amount = GetWeapons().Rocket.Damage, type = DamageType.Explosion });

                    lastFireRocket = Time.realtimeSinceStartup + GetWeapons().Rocket.Interval;

                    if (GetWeapons().Rocket.RequireAmmo)
                    {
                        Inventory.itemList.Find(x => x.info == RocketItem).UseItem(1);

                        CreateRocketAmmoUI(Commander, this, inventoryCount - 1);
                    }
                }
            }

            private void FireMG(bool isLeft, Vector3 targetPos)
            {
                int inventoryCount = Inventory.GetAmount(MGItem.itemid, false);

                if (GetWeapons().MG.RequireAmmo && inventoryCount == 0)
                {
                    if (Time.realtimeSinceStartup >= lastNoAmmoMG)
                    {
                        if (MGItem)
                            Commander.ChatMessage(string.Format(GetMessage("no_ammo_mg", Commander.userID), MGItem.displayName.english));
                        else Debug.Log($"[HeliCommander] Invalid ammo type for the machine gun set in config: {GetWeapons().MG.Type}");

                        lastNoAmmoMG = Time.realtimeSinceStartup + 3;
                    }
                    return;
                }

                if (Time.realtimeSinceStartup >= lastFireMG)
                {                   
                    Vector3 muzzlePos = !isLeft ? (Component.right_gun_muzzle.transform.position + (Component.right_gun_muzzle.transform.forward * 0.75f)) : (Component.left_gun_muzzle.transform.position + (Component.left_gun_muzzle.transform.forward * 0.75f));
                    Vector3 normDir = (targetPos - muzzlePos).normalized;

                    Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(GetWeapons().MG.Accuracy, normDir, true);

                    if (!Physics.Raycast(muzzlePos, modifiedAimConeDirection, out raycastHit, 300f, 1084427009))                    
                        targetPos = muzzlePos + (modifiedAimConeDirection * 300f);                    
                    else
                    {
                        targetPos = raycastHit.point;
                        if (raycastHit.collider)
                        {
                            BaseEntity baseEntity = raycastHit.collider.gameObject.ToBaseEntity();
                            if (baseEntity && baseEntity != Component)
                            {
                                if (baseEntity is BasePlayer && occupants.Contains(baseEntity as BasePlayer))
                                    return;

                                BaseCombatEntity baseCombatEntity = baseEntity as BaseCombatEntity;
                                HitInfo hitInfo = new HitInfo(Commander, baseEntity, DamageType.Bullet, GetWeapons().MG.Damage * UnityEngine.Random.Range(0.9f, 1.1f), raycastHit.point);
                                if (!baseCombatEntity)
                                    baseEntity.OnAttacked(hitInfo);
                                else
                                {
                                    baseCombatEntity.OnAttacked(hitInfo);
                                    if (baseCombatEntity is BasePlayer or BaseNpc)
                                    {
                                        HitInfo hitInfo1 = new HitInfo()
                                        {
                                            HitPositionWorld = raycastHit.point,
                                            HitNormalWorld = -modifiedAimConeDirection,
                                            HitMaterial = StringPool.Get("Flesh")
                                        };
                                        Effect.server.ImpactEffect(hitInfo1);
                                    }
                                }
                            }
                        }                        
                    }
                    Component.ClientRPC(null, "FireGun", isLeft, targetPos);                   

                    lastFireMG = Time.realtimeSinceStartup + GetWeapons().MG.Interval;

                    if (GetWeapons().MG.RequireAmmo)
                    {
                        Inventory.itemList.Find(x => x.info == MGItem).UseItem(1);  
                        
                        CreateMGAmmoUI(Commander, this, inventoryCount - 1);
                    }
                }                
            }
            #endregion

            #region Functions     
            internal override bool IsEngineOn() => true;

            internal override bool IsGrounded() => isGrounded || IsNearGround();
                       
            internal override void OnEntityMounted(BasePlayer player, bool isDriver)
            {
                base.OnEntityMounted(player, isDriver);

                if (isDriver)
                {
                    CreateAimHelper(player);
                    CreateMGAmmoUI(player, this, Inventory.GetAmount(MGItem.itemid, false));
                    CreateRocketAmmoUI(player, this, Inventory.GetAmount(RocketItem.itemid, false));
                }
            }
            #endregion

            #region Damage and Destruction            
            protected override bool IsDead() => heliAi.isDead;

            protected override void OnDeath()
            {
                base.OnDeath();
               
                if (Component.IsInvoking(UpdateNetworkGroup))
                {
                    Component.CancelInvoke(UpdateNetworkGroup);
                    Component.net.SwitchGroup(Net.sv.visibility.Get(0));
                }

                Effect.server.Run(Component.explosionEffect.resourcePath, Transform.position, Vector3.up, null, true);
                GameObject gibSource = Component.servergibs.Get().GetComponent<ServerGib>()._gibSource;
                List<ServerGib> serverGibs = ServerGib.CreateGibs(Component.servergibs.resourcePath, Component.gameObject, gibSource, Vector3.zero, 3f);
                
                for (int i = 0; i < 12 - Component.maxCratesToSpawn; i++)
                {
                    BaseEntity fireBall = GameManager.server.CreateEntity(Component.fireBall.resourcePath, Transform.position, Transform.rotation, true);
                    if (fireBall)
                    {
                        Vector3 randomOnSphere = UnityEngine.Random.onUnitSphere;
                        fireBall.transform.position = (Transform.position + new Vector3(0f, 1.5f, 0f)) + (randomOnSphere * UnityEngine.Random.Range(-4f, 4f));
                        Collider collider = fireBall.GetComponent<Collider>();
                        fireBall.Spawn();
                        fireBall.SetVelocity(Vector3.zero + (randomOnSphere * UnityEngine.Random.Range(3, 10)));

                        foreach (ServerGib serverGib in serverGibs)
                            Physics.IgnoreCollision(collider, serverGib.GetCollider(), true);
                    }
                }
                               
                if (Config.Inventory.DropLoot)
                {
                    for (int j = 0; j < Component.maxCratesToSpawn; j++)
                    {
                        Vector3 randomOnSphere = UnityEngine.Random.onUnitSphere;
                        BaseEntity lootCrate = GameManager.server.CreateEntity(Component.crateToDrop.resourcePath, (Transform.position + new Vector3(0f, 1.5f, 0f)) + (randomOnSphere * UnityEngine.Random.Range(2f, 3f)), Quaternion.LookRotation(randomOnSphere), true);
                        lootCrate.Spawn();

                        LootContainer lootContainer = lootCrate as LootContainer;
                        if (lootContainer)
                            lootContainer.Invoke(new Action(lootContainer.RemoveMe), 1800f);

                        Collider collider = lootCrate.GetComponent<Collider>();
                        Rigidbody rigidbody = lootCrate.gameObject.AddComponent<Rigidbody>();
                        rigidbody.useGravity = true;
                        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rigidbody.mass = 2f;
                        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                        rigidbody.velocity = Vector3.zero + (randomOnSphere * UnityEngine.Random.Range(1f, 3f));
                        rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                        rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                        rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);

                        FireBall fireBall = GameManager.server.CreateEntity(Component.fireBall.resourcePath, lootCrate.transform.position, Quaternion.identity, true) as FireBall;
                        if (fireBall)
                        {
                            fireBall.transform.position = lootCrate.transform.position;
                            fireBall.Spawn();
                            fireBall.GetComponent<Rigidbody>().isKinematic = true;
                            fireBall.GetComponent<Collider>().enabled = false;
                            fireBall.transform.parent = lootCrate.transform;
                        }
                        lootCrate.SendMessage("SetLockingEnt", fireBall.gameObject, SendMessageOptions.DontRequireReceiver);

                        foreach (ServerGib serverGib in serverGibs)
                            Physics.IgnoreCollision(collider, serverGib.GetCollider(), true);
                    }
                }

                Destroy(this);
            }
            #endregion

            #region Repair
            protected override ItemDefinition GetRepairItem() => RepairItem;
            #endregion

            #region Samsite targeting
            public SamSite.SamTargetType SAMTargetType => SamSite.targetTypeVehicle;

            public bool isClient => Component.isClient;

            public bool IsValidSAMTarget(bool staticRespawn) => !Component.InSafeZone();

            public Vector3 CenterPoint() => Component.CenterPoint();

            public Vector3 GetWorldVelocity() => Component.GetWorldVelocity();

            public bool IsVisible(Vector3 position, float maxDistance = float.PositiveInfinity) => Component.IsVisible(position, maxDistance);
            #endregion
        }

        //public class EntityFuelSystem
        //{
        //    public readonly bool isServer;

        //    public readonly BaseEntity owner;

        //    public EntityRef fuelStorageInstance;

        //    public float nextFuelCheckTime;

        //    public bool cachedHasFuel;

        //    public float pendingFuel;

        //    public EntityFuelSystem(BaseEntity owner, bool isServer)
        //    {
        //        this.isServer = isServer;
        //        this.owner = owner;
        //    }

        //    public void AddStartingFuel(float amount = -1f)
        //    {
        //        amount = (amount == -1f ? (float)GetFuelContainer().allowedItem.stackable * 0.2f : amount);
        //        GetFuelContainer().inventory.AddItem(GetFuelContainer().allowedItem, Mathf.FloorToInt(amount), 0UL);
        //    }

        //    public void AdminFillFuel()
        //    {
        //        GetFuelContainer().inventory.AddItem(GetFuelContainer().allowedItem, GetFuelContainer().allowedItem.stackable, 0UL);
        //    }

        //    public int GetFuelAmount()
        //    {
        //        Item fuelItem = GetFuelItem();                
        //        if (fuelItem == null || fuelItem.amount < 1)                
        //            return 0;

        //        return fuelItem.amount;
        //    }

        //    public StorageContainer GetFuelContainer()
        //    {
        //        BaseEntity baseEntity = fuelStorageInstance.Get(isServer);
        //        if (!(baseEntity != null) || !baseEntity.IsValid())                
        //            return null;

        //        return baseEntity as StorageContainer;
        //    }

        //    public float GetFuelFraction()
        //    {
        //        Item fuelItem = GetFuelItem();
        //        if (fuelItem == null || fuelItem.amount < 1)                
        //            return 0f;

        //        return Mathf.Clamp01((float)fuelItem.amount / (float)fuelItem.MaxStackable());
        //    }

        //    public Item GetFuelItem()
        //    {
        //        StorageContainer fuelContainer = GetFuelContainer();                
        //        if (fuelContainer == null)                
        //            return null;

        //        return fuelContainer.inventory.GetSlot(0);
        //    }

        //    public bool HasFuel(bool forceCheck = false)
        //    {
        //        if (Time.time > nextFuelCheckTime | forceCheck)
        //        {
        //           cachedHasFuel = (float)this.GetFuelAmount() > 0f;
        //            nextFuelCheckTime = Time.time + UnityEngine.Random.Range(1f, 2f);
        //        }
        //        return cachedHasFuel;
        //    }

        //    public bool IsInFuelInteractionRange(BasePlayer player)
        //    {
        //        StorageContainer fuelContainer = GetFuelContainer();               
        //        if (fuelContainer == null)                
        //            return false;

        //        float single = isServer ? 3f : 0f;

        //        return fuelContainer.Distance(player.eyes.position) <= single;
        //    }

        //    public void LootFuel(BasePlayer player)
        //    {
        //        if (IsInFuelInteractionRange(player))                
        //            GetFuelContainer().PlayerOpenLoot(player, "", true);                
        //    }

        //    public void SpawnFuelStorage(GameObjectRef fuelStoragePrefab, Transform fuelStoragePoint)
        //    {
        //        if (fuelStoragePrefab == null || fuelStoragePoint == null)                
        //            return;

        //        if (!Rust.Application.isLoadingSave)
        //        {
        //            Vector3 vector3 = owner.transform.InverseTransformPoint(fuelStoragePoint.position);
        //            Quaternion quaternion = Quaternion.Inverse(owner.transform.rotation) * fuelStoragePoint.rotation;
        //            BaseEntity baseEntity = GameManager.server.CreateEntity(fuelStoragePrefab.resourcePath, vector3, quaternion, true);
        //            baseEntity.SetParent(owner, false, false);
        //            baseEntity.Spawn();
        //            fuelStorageInstance.Set(baseEntity);
        //        }
        //    }

        //    public int TryUseFuel(float seconds, float fuelUsedPerSecond)
        //    {
        //        StorageContainer fuelContainer = GetFuelContainer();

        //        if (fuelContainer == null)                
        //            return 0;

        //        Item slot = fuelContainer.inventory.GetSlot(0);
        //        if (slot == null || slot.amount < 1)                
        //            return 0;

        //        pendingFuel = pendingFuel + seconds * fuelUsedPerSecond;
        //        if (pendingFuel < 1f)                
        //            return 0;

        //        int num = Mathf.FloorToInt(pendingFuel);
        //        slot.UseItem(num);
        //        pendingFuel -= (float)num;
        //        return num;
        //    }
        //}
        #endregion

        #region UI
        #region UI Elements
        public static class UI
        {
            public static CuiElementContainer Container(string panelName, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return container;
            }

            public static void Panel(ref CuiElementContainer container, string panel, string color, UI4 dimensions, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = cursor
                },
                panel);
            }

            public static void Label(ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = "droidsansmono.ttf" },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);

            }    
            
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        public class UI4
        {
            public float xMin, yMin, xMax, yMax;
            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }
            public string GetMin() => $"{xMin} {yMin}";
            public string GetMax() => $"{xMax} {yMax}";
        }
        #endregion

        #region UI Creation    
        private static void CreateAimHelper(BasePlayer player)
        {
            ConfigData.UIOptions.UICrosshair opt = Configuration.UI.Aim;
            if (!opt.Enabled)
                return;

            CuiElementContainer container = UI.Container(UI_AIM, "0 0 0 0", new UI4(0.4f, 0.4f, 0.6f, 0.6f));
            UI.Label(ref container, UI_AIM, $"<color={opt.Color}>X</color>", opt.Size, new UI4(0, 0, 1, 1), TextAnchor.MiddleCenter);            
            DestroyUI(player, UI_AIM);
            CuiHelper.AddUi(player, container);
        }    
        private static void CreateHealthUI(BasePlayer player, BaseController baseController)
        {
            ConfigData.UIOptions.UICounter opt = Configuration.UI.Health;
            if (!opt.Enabled)
                return;

            CuiElementContainer container = UI.Container(UI_HEALTH, UI.Color(opt.Color1, opt.Color1A), new UI4(opt.Xmin, opt.YMin, opt.XMax, opt.YMax));
            UI.Label(ref container, UI_HEALTH, GetMessage("health", player.userID), 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);

            double percentHealth = System.Convert.ToDouble((float)baseController.GetEntity().health / (float)baseController.GetEntity().MaxHealth());
            float yMaxHealth = 0.25f + (0.73f * (float)percentHealth);
            UI.Panel(ref container, UI_HEALTH, UI.Color(opt.Color2, opt.Color2A), new UI4(0.25f, 0.1f, yMaxHealth, 0.9f));
            
            DestroyUI(player, UI_HEALTH);
            CuiHelper.AddUi(player, container);
        }
        private static void CreateMGAmmoUI(BasePlayer player, UH1YController uh1yController, int amountRemaining)
        {            
            if (uh1yController.GetWeapons().MG.Enabled && uh1yController.GetWeapons().MG.RequireAmmo)
            {
                ConfigData.UIOptions.UICounter opt = Configuration.UI.MG;
                if (!opt.Enabled)
                    return;

                CuiElementContainer container = UI.Container(UI_AMMO_MG, UI.Color(opt.Color1, opt.Color1A), new UI4(opt.Xmin, opt.YMin, opt.XMax, opt.YMax));
                UI.Label(ref container, UI_AMMO_MG, string.Format(GetMessage("mgun", player.userID), $"<color={opt.Color2}>{amountRemaining}</color>"), 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
                
                DestroyUI(player, UI_AMMO_MG);
                CuiHelper.AddUi(player, container);
            }
        }
        private static void CreateRocketAmmoUI(BasePlayer player, UH1YController uh1yController, int amountRemaining)
        {           
            if (uh1yController.GetWeapons().Rocket.Enabled && uh1yController.GetWeapons().Rocket.RequireAmmo)
            {
                ConfigData.UIOptions.UICounter opt = Configuration.UI.Rocket;
                if (!opt.Enabled)
                    return;

                CuiElementContainer container = UI.Container(UI_AMMO_ROCKET, UI.Color(opt.Color1, opt.Color1A), new UI4(opt.Xmin, opt.YMin, opt.XMax, opt.YMax));
                UI.Label(ref container, UI_AMMO_ROCKET, string.Format(GetMessage("rocket", player.userID), $"<color={opt.Color2}>{amountRemaining}</color>"), 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
                DestroyUI(player, UI_AMMO_ROCKET);
                CuiHelper.AddUi(player, container);
            }            
        }

        private static void CreateFuelUI(BasePlayer player, BaseController baseController, float fuelAmount)
        {
            if (baseController.ConsumeFuel)
            {
                ConfigData.UIOptions.UICounter opt = Configuration.UI.Fuel;
                if (!opt.Enabled)
                    return;

                CuiElementContainer container = UI.Container(UI_FUEL, UI.Color(opt.Color1, opt.Color1A), new UI4(opt.Xmin, opt.YMin, opt.XMax, opt.YMax));
                UI.Label(ref container, UI_FUEL, string.Format(GetMessage("fuel", player.userID), $"<color={opt.Color2}>{fuelAmount}</color>"), 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft);
                
                DestroyUI(player, UI_FUEL);
                CuiHelper.AddUi(player, container);
            }
        }

        private static void DestroyUI(BasePlayer player, string panel) => CuiHelper.DestroyUi(player, panel);

        private static void DestroyAllUI(BasePlayer player)
        {
            DestroyUI(player, UI_HEALTH);
            DestroyUI(player, UI_AMMO_MG);
            DestroyUI(player, UI_AMMO_ROCKET);
            DestroyUI(player, UI_FUEL);
            DestroyUI(player, UI_AIM);
        }
        #endregion
        #endregion

        private BaseEntity SpawnAtLocation(Vector3 position, Quaternion rotation, string typeStr, bool enableSaving = false, ulong ownerId = 0UL, bool preventMounting = false)
        {
            HelicopterType type = ParseType<HelicopterType>(typeStr);            
            return SpawnAtLocation(position, rotation, type, enableSaving, ownerId, preventMounting);
        }

        private BaseEntity SpawnAtLocation(Vector3 position, Quaternion rotation, HelicopterType type, bool enableSaving = false, ulong ownerId = 0UL, bool preventMounting = false)
        {
            BaseEntity entity = GameManager.server.CreateEntity(GetPrefabPath(type), position + Vector3.up, rotation);
            entity.enableSaving = false;
            entity.Spawn();
            entity.OwnerID = ownerId;

            entity.transform.position = position + Vector3.up;

            BaseController controller;

            if (type == HelicopterType.CH47)
                controller = entity.gameObject.AddComponent<CH47Controller>();
            else if (type == HelicopterType.Mini)
                controller = entity.gameObject.AddComponent<MiniController>();
            else if (type == HelicopterType.Transport)
                controller = entity.gameObject.AddComponent<TransportController>();
            else controller = entity.gameObject.AddComponent<UH1YController>();

            controller.MountLocked = preventMounting;

            if (enableSaving)            
                saveableHelis.Add(controller); 
            else spawnedHelis.Add(controller);

            return entity;
        }
        
        private void ToggleController(BaseEntity baseEntity, bool enabled)
        {
            if (baseEntity == null)
                return;

            BaseController controller = baseEntity.GetComponent<BaseController>();
            if (controller != null)
            {
                controller.enabled = enabled;                
            }
        }

        private void MountPlayerTo(BasePlayer player, BaseEntity baseEntity, bool fuelEnabled = true)
        {
            if (player == null)
                return;

            BaseController baseController = baseEntity.GetComponent<BaseController>();
            if (baseController != null)
            {
                BaseVehicle.MountPointInfo mountPointInfo = baseController.FindMountPoint((BaseVehicle.MountPointInfo m) => m.isDriver);

                if (mountPointInfo != null && mountPointInfo.mountable != null)
                {
                    BaseMountable baseMountable = mountPointInfo.mountable;

                    ForceMountPlayerTo(player, baseMountable, baseController);

                    baseController.SetFuelRequirement(fuelEnabled);
                }
            }
        }

        private void ForceMountPlayerTo(BasePlayer player, BaseMountable baseMountable, BaseController baseController)
        {
            player.EnsureDismounted();
            baseMountable._mounted = player;

            TriggerParent triggerParent = player.FindTrigger<TriggerParent>();
            if (triggerParent)
                triggerParent.OnTriggerExit(player.GetComponent<Collider>());

            player.SetMounted(baseMountable);
            player.MovePosition(baseMountable.mountAnchor.transform.position);
            player.transform.rotation = baseMountable.mountAnchor.transform.rotation;
            player.ServerRotation = baseMountable.mountAnchor.transform.rotation;
            player.OverrideViewAngles(baseMountable.mountAnchor.transform.rotation.eulerAngles);
            baseMountable._mounted.eyes.NetworkUpdate(baseMountable.mountAnchor.transform.rotation);
            player.ClientRPCPlayer<Vector3>(null, player, "ForcePositionTo", player.transform.position);
            baseMountable.SetFlag(BaseEntity.Flags.Busy, true, false, true);
            baseMountable.OnPlayerMounted();

            baseController.OnPlayerMounted(baseMountable, player);
        }

        private static void ForceDismountPlayerFrom(BasePlayer player, BaseMountable baseMountable, BaseController baseController)
        {
            Vector3 dismountPosition = baseMountable.dismountPositions?.Length > 0 ? baseMountable.dismountPositions[0].position : baseMountable.transform.position + (baseMountable.transform.right * 3f);
            baseMountable._mounted.DismountObject();
            baseMountable._mounted.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            baseMountable._mounted.MovePosition(dismountPosition);
            baseMountable._mounted.SendNetworkUpdateImmediate(false);
            baseMountable._mounted = null;
            baseMountable.SetFlag(BaseEntity.Flags.Busy, false, false, true);

            player.ForceUpdateTriggers(true, true, true);
            player.ClientRPCPlayer<Vector3>(null, player, "ForcePositionTo", dismountPosition);

            baseController.OnPlayerDismounted(baseMountable, player);
        }

        private void EjectAllPlayers(BaseEntity baseEntity)
        {
            BaseController controller = baseEntity.GetComponent<BaseController>();
            if (controller != null)
                controller.EjectAllPlayers();
        }

        #region Commands
        [ChatCommand("spawnhc")]
        private void cmdSpawnHeli(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.uh1y") && !permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.ch47") 
                && !permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.mini") && !permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.transport"))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            if (args.Length == 0)
            {        
                if (permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.uh1y"))                
                    SendReply(player, "/spawnhc UH1Y - Spawn an attack helicopter");
                
                if (permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.ch47"))                
                    SendReply(player, "/spawnhc CH47 - Spawn a chinook");

                if (permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.mini"))
                    SendReply(player, "/spawnhc mini - Spawn a Minicopter");

                if (permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.transport"))
                    SendReply(player, "/spawnhc transport - Spawn a Scrap Transport Helicopter");

                return;
            }

            HelicopterType type = ParseType<HelicopterType>(args[0]);
            if (type == HelicopterType.Invalid)
            {
                SendReply(player, "Invalid helicopter type selected!");
                return;
            }

            if ((type == HelicopterType.CH47 && !permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.ch47")) || 
                (type == HelicopterType.UH1Y && !permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.uh1y")) ||
                (type == HelicopterType.Transport && !permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.transport")) ||
                (type == HelicopterType.Mini && !permission.UserHasPermission(player.UserIDString, "helicommander.canspawn.mini")))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

                if ((type == HelicopterType.CH47 && !permission.UserHasPermission(player.UserIDString, "helicommander.ignorecooldown.ch47")) || 
                (type == HelicopterType.UH1Y && !permission.UserHasPermission(player.UserIDString, "helicommander.ignorecooldown.uh1y")) ||
                (type == HelicopterType.Transport && !permission.UserHasPermission(player.UserIDString, "helicommander.ignorecooldown.transport")) ||
                (type == HelicopterType.Mini && !permission.UserHasPermission(player.UserIDString, "helicommander.ignorecooldown.mini")))
            {
                double time = GrabCurrentTime();
                if (!userCooldowns.ContainsKey(player.userID))
                    userCooldowns.Add(player.userID, time + GetCooldown(type));
                else
                {
                    double nextUseTime = userCooldowns[player.userID];
                    if (nextUseTime > time)
                    {
                        SendReply(player, string.Format(Message("onCooldown", player.userID), FormatTime(nextUseTime - time)));
                        return;
                    }
                    else userCooldowns[player.userID] = time + GetCooldown(type);
                }
            }

            Vector3 position = player.transform.position + (player.eyes.MovementForward() * 5f);
            position.y = Mathf.Max(position.y, TerrainMeta.HeightMap.GetHeight(position) + 0.25f);
           
            SpawnAtLocation(position, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y - 90f, 0), type, (args.Length == 2 && args[1].ToLower() == "save"), player.userID);
        }

        [ChatCommand("storehc")]
        private void cmdStoreHeli(BasePlayer player, string command, string[] args)
        {
            BaseMountable mountable = player.GetMounted();
            BaseController baseController = mountable?.GetComponentInParent<BaseController>();

            if (mountable == null || baseController == null || player != baseController.Commander)
            {
                PrintError($"{mountable == null} {baseController == null} {player != baseController?.Commander}");
                SendReply(player, "You need to be mounted in the drivers seat of a helicopter to use this command");
                return;
            }

            if (!baseController.IsGrounded() && !baseController.IsNearGround())
            {
                SendReply(player, "The helicopter must be parked on the ground to use this command");
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, string.Concat(USE_PERMISSION, baseController.Type)))
            {
                baseController.EjectAllPlayers();
                baseController.DropInventory();
                baseController.DropFuel();

                UnityEngine.Object.Destroy(baseController);
                
                if (userCooldowns.ContainsKey(player.userID))                    
                    userCooldowns[player.userID] = 0f;

                SendReply(player, "The helicopter has been despawned! Anything left in the inventory or fuel tank has been dropped.");
            }
            else SendReply(player, "You do not have permission to use this command");
        }

        [ChatCommand("buildhc")]
        private void cmdBuildHeli(BasePlayer player, string command, string[] args)
        {
            bool hasPermissionUH1Y = permission.UserHasPermission(player.UserIDString, string.Concat(BUILD_PERMISSION, HelicopterType.UH1Y));
            bool hasPermissionCH47 = permission.UserHasPermission(player.UserIDString, string.Concat(BUILD_PERMISSION, HelicopterType.CH47));
            bool hasPermissionMini = permission.UserHasPermission(player.UserIDString, string.Concat(BUILD_PERMISSION, HelicopterType.Mini));
            bool hasPermissionTransport = permission.UserHasPermission(player.UserIDString, string.Concat(BUILD_PERMISSION, HelicopterType.Transport));

            if (!hasPermissionUH1Y && !hasPermissionCH47 && !hasPermissionMini && !hasPermissionTransport)
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            if (!Configuration.UH1Y.Build.Enabled && !Configuration.CH47.Build.Enabled && !Configuration.Mini.Build.Enabled)
                return;

            if (args.Length == 0)
            {
                SendReply(player, "/buildhc costs - Display the costs to build");

                if (hasPermissionUH1Y && Configuration.UH1Y.Build.Enabled)
                    SendReply(player, "/buildhc UH1Y - Build an attack helicopter");

                if (hasPermissionCH47 && Configuration.CH47.Build.Enabled)
                    SendReply(player, "/buildhc CH47 - Build a chinook");

                if (hasPermissionMini && Configuration.Mini.Build.Enabled)
                    SendReply(player, "/buildhc mini - Build a minicopter");

                if (hasPermissionTransport && Configuration.Transport.Build.Enabled)
                    SendReply(player, "/buildhc transport - Build a Scrap Transport Helicopter");

                return;
            }

            if (args[0].ToLower() == "costs")
            {
                string str = Message("ch47cost", player.userID);
                foreach (ConfigData.PatrolHelicopter.BuildOptions.BuildOption cost in Configuration.CH47.Build.Costs)
                {
                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition(cost.Shortname);
                    if (itemDefinition == null)
                        continue;

                    str += $"\n- {cost.Amount} x {itemDefinition.displayName.translated}";
                }
                SendReply(player, str);

                str = Message("uh1ycost", player.userID);
                foreach (ConfigData.PatrolHelicopter.BuildOptions.BuildOption cost in Configuration.UH1Y.Build.Costs)
                {
                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition(cost.Shortname);
                    if (itemDefinition == null)
                        continue;

                    str += $"\n- {cost.Amount} x {itemDefinition.displayName.translated}";
                }
                SendReply(player, str);

                str = Message("minicost", player.userID);
                foreach (ConfigData.PatrolHelicopter.BuildOptions.BuildOption cost in Configuration.Mini.Build.Costs)
                {
                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition(cost.Shortname);
                    if (itemDefinition == null)
                        continue;

                    str += $"\n- {cost.Amount} x {itemDefinition.displayName.translated}";
                }
                SendReply(player, str);

                str = Message("transportcost", player.userID);
                foreach (ConfigData.PatrolHelicopter.BuildOptions.BuildOption cost in Configuration.Transport.Build.Costs)
                {
                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition(cost.Shortname);
                    if (itemDefinition == null)
                        continue;

                    str += $"\n- {cost.Amount} x {itemDefinition.displayName.translated}";
                }
                SendReply(player, str);
                return;
            }

            HelicopterType type = ParseType<HelicopterType>(args[0]);
            if (type == HelicopterType.Invalid)
            {
                SendReply(player, "Invalid helicopter type selected!");
                return;
            }

            if ((type == HelicopterType.CH47 && (!hasPermissionCH47 || !Configuration.CH47.Build.Enabled)) || 
                (type == HelicopterType.UH1Y && (!hasPermissionUH1Y || !Configuration.UH1Y.Build.Enabled)) ||
                (type == HelicopterType.Transport && (!hasPermissionTransport || !Configuration.Transport.Build.Enabled)) ||
                (type == HelicopterType.Mini && (!hasPermissionMini || !Configuration.Mini.Build.Enabled)))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            List<ItemCost> requiredItems = new List<ItemCost>();
            List<ConfigData.PatrolHelicopter.BuildOptions.BuildOption> costs = GetBuildCosts(type);

            foreach (ConfigData.PatrolHelicopter.BuildOptions.BuildOption cost in costs)
            {
                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(cost.Shortname);
                if (itemDefinition == null)
                    continue;                

                if (player.inventory.GetAmount(itemDefinition.itemid) < cost.Amount)
                {
                    SendReply(player, string.Format(Message("notenoughres", player.userID), cost.Amount, itemDefinition.displayName.translated));
                    return;
                }

                requiredItems.Add(new ItemCost(itemDefinition.itemid, cost.Amount));
            }

            if (HasBuildCooldown(type))
            {
                if ((type == HelicopterType.CH47 && !permission.UserHasPermission(player.UserIDString, "helicommander.ignorecooldown.ch47")) || 
                    (type == HelicopterType.UH1Y && !permission.UserHasPermission(player.UserIDString, "helicommander.ignorecooldown.uh1y")) ||
                    (type == HelicopterType.Transport && !permission.UserHasPermission(player.UserIDString, "helicommander.ignorecooldown.transport")) ||
                    (type == HelicopterType.Mini && !permission.UserHasPermission(player.UserIDString, "helicommander.ignorecooldown.mini")))
                {
                    double time = GrabCurrentTime();
                    if (!userCooldowns.ContainsKey(player.userID))
                        userCooldowns.Add(player.userID, time + GetCooldown(type));
                    else
                    {
                        double nextUseTime = userCooldowns[player.userID];
                        if (nextUseTime > time)
                        {
                            SendReply(player, string.Format(Message("onCooldown", player.userID), FormatTime(nextUseTime - time)));
                            return;
                        }
                        else userCooldowns[player.userID] = time + GetCooldown(type);
                    }
                }
            }

            foreach (ItemCost cost in requiredItems)
                player.inventory.Take(null, cost.itemId, cost.amount);

            Vector3 position = player.eyes.position + (player.eyes.MovementForward() * 5f);

            float y = TerrainMeta.HeightMap.GetHeight(position);
            if (y > position.y)
                position.y = y;

            SpawnAtLocation(position, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y - 90f, 0), type, true, player.userID);
        }

        [ChatCommand("removehc")]
        void cmdRemoveHelicopter(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "helicommander.admin")) return;

            if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out raycastHit, 3f))
            {
                BaseController controller = raycastHit.GetEntity()?.GetComponent<BaseController>();
                if (controller != null)
                {
                    controller.EjectAllPlayers();
                    UnityEngine.Object.Destroy(controller);
                }
            }
        }

        [ConsoleCommand("spawnhc")]
        private void ccmdSpawnHeli(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null || arg.Args == null)
                return;

            HelicopterType type = ParseType<HelicopterType>(arg.Args[0]);
            if (type == HelicopterType.Invalid)
            {
                PrintError("Invalid helicopter type selected in console command. You need to select either CH47, UH1Y, Mini or Transport\n\"spawnhc <ch47 / uh1y / mini / transport> <playerID> | spawnhc <ch47 / uh1y / mini / transport> <x> <y> <z>\"");
                return;
            }

            if (arg.Args.Length >= 2 && arg.Args.Length < 4)
            {
                BasePlayer player = covalence.Players.Connected.FirstOrDefault(x => x.Id == arg.GetString(1))?.Object as BasePlayer;
                if (player != null)
                {
                    Vector3 position = player.eyes.position + (player.eyes.MovementForward() * 5f);

                    float y = TerrainMeta.HeightMap.GetHeight(position);
                    if (y > position.y)
                        position.y = y;

                    SpawnAtLocation(position, Quaternion.Euler(0, player.eyes.rotation.eulerAngles.y - 90f, 0), type, (arg.Args.Length == 3 && arg.Args[2].ToLower() == "save"));
                }
                return;
            }
            if (arg.Args.Length >= 4)
            {
                if (float.TryParse(arg.GetString(1), out float x))
                {
                    if (float.TryParse(arg.GetString(2), out float y))
                    {
                        if (float.TryParse(arg.GetString(3), out float z))
                        {
                            SpawnAtLocation(new Vector3(x, y, z) + Vector3.up, Quaternion.identity, type, (arg.Args.Length == 5 && arg.Args[4].ToLower() == "save"));                           
                            return;
                        }
                    }
                }
                PrintError($"Invalid arguments supplied to spawn a helicopter at position : (x = {arg.GetString(1)}, y = {arg.GetString(2)}, z = {arg.GetString(3)})");
            }
        }

        [ChatCommand("clearhelis")]
        void cmdClearHelicopters(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "helicommander.admin")) 
                return;

            ClearHelicopters();
        }

        [ConsoleCommand("clearhelis")]
        void ccmdClearHelicopters(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null || arg.Args == null)
                return;

            ClearHelicopters();
        }

        private void ClearHelicopters()
        {
            for (int i = spawnedHelis.Count - 1; i >= 0; i--)
            {
                BaseController baseController = spawnedHelis[i];
                if (baseController != null)
                {
                    BaseCombatEntity baseCombatEntity = baseController.GetEntity();

                    if (baseCombatEntity != null && !baseCombatEntity.IsDestroyed)
                    {
                        baseController.EjectAllPlayers();
                        UnityEngine.Object.Destroy(baseController);
                    }
                }
            }

            for (int i = saveableHelis.Count - 1; i >= 0; i--)
            {
                BaseController baseController = saveableHelis[i];
                if (baseController != null)
                {
                    BaseCombatEntity baseCombatEntity = baseController.GetEntity();

                    if (baseCombatEntity != null && !baseCombatEntity.IsDestroyed)
                    {
                        baseController.EjectAllPlayers();
                        UnityEngine.Object.Destroy(baseController);
                    }
                }
            }
        }

        #endregion

        #region Friends
        public bool IsFriendlyPlayer(ulong playerId, ulong friendId, HelicopterType type)
        {
            ConfigData.PatrolHelicopter.PassengerOptions passengers = type == HelicopterType.UH1Y ? Configuration.UH1Y.Passengers : type == HelicopterType.Mini ? Configuration.Mini.Passengers : type == HelicopterType.Transport ? Configuration.Transport.Passengers : Configuration.CH47.Passengers;
            if (playerId == friendId || (!passengers.UseFriends && !passengers.UseClans) || (passengers.UseFriends && IsFriend(playerId, friendId)) || (passengers.UseClans && IsClanmate(playerId, friendId)))
                return true;
            return false;
        }

        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (!Clans) return false;
            object playerTag = Clans?.Call("GetClanOf", playerId);
            object friendTag = Clans?.Call("GetClanOf", friendId);
            if ((playerTag is string && !string.IsNullOrEmpty((string)playerTag)) && (friendTag is string && !string.IsNullOrEmpty((string)friendTag)))
                if (playerTag == friendTag) return true;
            return false;
        }

        private bool IsFriend(ulong playerID, ulong friendID)
        {
            if (!Friends) return false;
            return (bool)Friends?.Call("AreFriends", playerID, friendID);
        }        
        #endregion

        #region API
        private bool IsHeliController(BaseEntity baseEntity) => baseEntity.GetComponent<BaseController>();

        private ulong[] GetOccupantIDs(BaseController helicopter)
        {
            BaseController baseController = helicopter.GetComponent<BaseController>();
            if (baseController == null)
                return new ulong[0];

            return baseController.GetOccupants().Select(x => x.userID.Get()).ToArray();
        }

        private object CanTeleport(BasePlayer player)
        {
            foreach(BaseController controller in spawnedHelis)
            {
                if (controller.IsOccupant(player))                
                    return "You can not teleport whilst in a helicopter or to a player in a helicopter";                
            }
            return null;
        }
        #endregion

        #region Config        
        private static ConfigData Configuration;

        public class ConfigData
        {
            [JsonProperty(PropertyName = "CH47 Options")]
            public DefaultHelicopter CH47 { get; set; }

            [JsonProperty(PropertyName = "Minicopter Options")]
            public MiniHelicopter Mini { get; set; }

            [JsonProperty(PropertyName = "Scrap Transport Options")]
            public MiniHelicopter Transport { get; set; }

            [JsonProperty(PropertyName = "UH1Y Options")]
            public AttackHelicopter UH1Y { get; set; }   
            
            [JsonProperty(PropertyName = "Button Configuration")]
            public ButtonConfiguration Buttons { get; set; }  
            
            [JsonProperty(PropertyName = "UI Options")]
            public UIOptions UI { get; set; }           

            public class ButtonConfiguration
            {                
                [JsonProperty(PropertyName = "Toggle light")]
                public string Lights { get; set; }

                [JsonProperty(PropertyName = "Open inventory")]
                public string Inventory { get; set; }

                [JsonProperty(PropertyName = "Pitch Forward")]
                public string PitchForward { get; set; }

                [JsonProperty(PropertyName = "Pitch Backward")]
                public string PitchBackward { get; set; }

                [JsonProperty(PropertyName = "Roll Left")]
                public string RollLeft { get; set; }

                [JsonProperty(PropertyName = "Roll Right")]
                public string RollRight { get; set; }  
                
                [JsonProperty(PropertyName = "Throttle Up")]
                public string ThrottleUp { get; set; }

                [JsonProperty(PropertyName = "Throttle Down")]
                public string ThrottleDown { get; set; }

                [JsonProperty(PropertyName = "Lock Yaw")]
                public string LockYaw { get; set; }

                [JsonProperty(PropertyName = "Fire Rockets")]
                public string Rockets { get; set; }  
                
                [JsonProperty(PropertyName = "Fire MG")]
                public string MG { get; set; }

                [JsonProperty(PropertyName = "Open fuel tank")]
                public string FuelTank { get; set; }
            }
            public class MiniHelicopter : DefaultHelicopter
            {
                [JsonProperty(PropertyName = "Convert game spawned minicopters to HeliCommander minicopters")]
                public bool Convert { get; set; }
            }
            public class DefaultHelicopter : PatrolHelicopter
            {
                [JsonProperty(PropertyName = "Use Custom Controls")]
                public bool Custom { get; set; }                
            }
            public class AttackHelicopter : PatrolHelicopter
            {
                [JsonProperty(PropertyName = "Weapon Options")]
                public WeaponOptions Weapons { get; set; }

                public class WeaponOptions
                {
                    [JsonProperty(PropertyName = "Rockets")]
                    public WeaponSystem Rocket { get; set; }

                    [JsonProperty(PropertyName = "Machine Gun")]
                    public WeaponSystem MG { get; set; }

                    public class WeaponSystem
                    {
                        [JsonProperty(PropertyName = "Enable weapon system")]
                        public bool Enabled { get; set; }

                        [JsonProperty(PropertyName = "Require ammunition in inventory")]
                        public bool RequireAmmo { get; set; }

                        [JsonProperty(PropertyName = "Ammunition type (item shortname)")]
                        public string Type { get; set; }

                        [JsonProperty(PropertyName = "Fire rate (seconds)")]
                        public float Interval { get; set; }

                        [JsonProperty(PropertyName = "Aim cone (smaller number is more accurate)")]
                        public float Accuracy { get; set; }

                        [JsonProperty(PropertyName = "Damage")]
                        public float Damage { get; set; }
                    }
                }
            }
            public class PatrolHelicopter
            {
                [JsonProperty(PropertyName = "Movement Settings")]
                public MovementSettings Movement { get; set; }

                [JsonProperty(PropertyName = "Passenger Options")]
                public PassengerOptions Passengers { get; set; }

                [JsonProperty(PropertyName = "Inventory Options")]
                public InventoryOptions Inventory { get; set; }

                [JsonProperty(PropertyName = "Health and Damage Options")]
                public DamageOptions Damage { get; set; }

                [JsonProperty(PropertyName = "Fuel Options")]
                public FuelOptions Fuel { get; set; }

                [JsonProperty(PropertyName = "Spawnable Options")]
                public SpawnableOptions Spawnable { get; set; }

                [JsonProperty(PropertyName = "Repair Options")]
                public RepairSettings Repair { get; set; }

                [JsonProperty(PropertyName = "Build Options")]
                public BuildOptions Build { get; set; }

                public class BuildOptions
                {
                    [JsonProperty(PropertyName = "Allow users to build this helicopter")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Use cooldown timers")]
                    public bool Cooldown { get; set; }

                    [JsonProperty(PropertyName = "Build Costs")]
                    public List<BuildOption> Costs { get; set; }

                    public class BuildOption
                    {
                        [JsonProperty(PropertyName = "Item shortname")]
                        public string Shortname { get; set; }

                        [JsonProperty(PropertyName = "Amount")]
                        public int Amount { get; set; }
                    }
                }
                public class DamageOptions
                {
                    [JsonProperty(PropertyName = "Initial health")]
                    public int Health { get; set; }

                    [JsonProperty(PropertyName = "Damage modifiers")]
                    public Dictionary<DamageType, float> Modifiers { get; set; }
                }
                public class FuelOptions
                {
                    [JsonProperty(PropertyName = "Requires fuel")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Fuel type (item shortname)")]
                    public string FuelType { get; set; }

                    [JsonProperty(PropertyName = "Fuel consumption rate (litres per second)")]
                    public float Consumption { get; set; }

                    [JsonProperty(PropertyName = "Spawn vehicles with fuel")]
                    public bool GiveFuel { get; set; }

                    [JsonProperty(PropertyName = "Amount of fuel to give spawned vehicles (minimum)")]
                    public int FuelAmountMin { get; set; }

                    [JsonProperty(PropertyName = "Amount of fuel to give spawned vehicles (maximum)")]
                    public int FuelAmountMax { get; set; }
                }
                public class MovementSettings
                {                    
                    [JsonProperty(PropertyName = "Pitch Tilt Force")]
                    public float PitchTilt { get; set; }

                    [JsonProperty(PropertyName = "Roll Tilt Force")]
                    public float RollTilt { get; set; }

                    [JsonProperty(PropertyName = "Turn Force (Yaw via controls)")]
                    public float Turn { get; set; }

                    [JsonProperty(PropertyName = "Roll Turn Force (Yaw via roll)")]
                    public float TurnTilt { get; set; }

                    [JsonProperty(PropertyName = "Engine Lift")]
                    public float EngineLift { get; set; }

                    [JsonProperty(PropertyName = "Engine Thrust")]
                    public float EngineThrust { get; set; }

                    [JsonProperty(PropertyName = "Thrust Lerp Speed")]
                    public float ThrustLerpSpeed { get; set; }

                    [JsonProperty(PropertyName = "Lift Angle Max (0.0 - 1.0)")]
                    public float LiftDotMax { get; set; }

                    [JsonProperty(PropertyName = "Thrust Angle Min (0.0 - 1.0)")]
                    public float ThrustDotMin { get; set; }

                    [JsonProperty(PropertyName = "Lift Fraction (0.0 - 1.0)")]
                    public float LiftFraction { get; set; }

                    [JsonProperty(PropertyName = "Hover Force Scale (0.0 - 1.0)")]
                    public float HoverForceScale { get; set; }
                }
                public class RepairSettings
                {
                    [JsonProperty(PropertyName = "Allow users to repair helicopters")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Shortname of item required to repair")]
                    public string Shortname { get; set; }

                    [JsonProperty(PropertyName = "Amount of item required to repair")]
                    public int Amount { get; set; }

                    [JsonProperty(PropertyName = "Amount of damage repaired per hit")]
                    public int Damage { get; set; }                    
                }
                public class PassengerOptions
                {
                    [JsonProperty(PropertyName = "Allow passengers")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Require passenger to be a friend (FriendsAPI)")]
                    public bool UseFriends { get; set; }

                    [JsonProperty(PropertyName = "Require passenger to be a clan mate (Clans)")]
                    public bool UseClans { get; set; }

                    [JsonProperty(PropertyName = "Eject non-friendly players when a player enters the pilot seat")]
                    public bool EjectNonFriendlies { get; set; }
                }
                public class SpawnableOptions
                {
                    [JsonProperty(PropertyName = "Enable automatic vehicle spawning")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Use RandomSpawns for spawn locations")]
                    public bool RandomSpawns { get; set; }

                    [JsonProperty(PropertyName = "Save helicopters spawned through the spawn system through reloads/restarts")]
                    public bool Save { get; set; }

                    [JsonProperty(PropertyName = "Spawnfile name")]
                    public string Spawnfile { get; set; }

                    [JsonProperty(PropertyName = "Maximum spawned vehicles at any time")]
                    public int Max { get; set; }

                    [JsonProperty(PropertyName = "Time between autospawns (seconds)")]
                    public int Time { get; set; }

                    [JsonProperty(PropertyName = "Cooldown time for player spawned vehicles via chat command (seconds)")]
                    public int Cooldown { get; set; }
                }
                public class InventoryOptions
                {
                    [JsonProperty(PropertyName = "Enable inventory system")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Drop inventory on death")]
                    public bool DropInv { get; set; }

                    [JsonProperty(PropertyName = "Drop loot on death")]
                    public bool DropLoot { get; set; }

                    [JsonProperty(PropertyName = "Inventory size (max 36)")]
                    public int Size { get; set; }
                }
            }           
            public class UIOptions
            {
                [JsonProperty(PropertyName = "Health settings")]
                public UICounter Health { get; set; }

                [JsonProperty(PropertyName = "Rocket settings")]
                public UICounter Rocket { get; set; }

                [JsonProperty(PropertyName = "MG settings")]
                public UICounter MG { get; set; }

                [JsonProperty(PropertyName = "Fuel settings")]
                public UICounter Fuel { get; set; }

                [JsonProperty(PropertyName = "Crosshair settings")]
                public UICrosshair Aim { get; set; }

                public class UICounter
                {
                    [JsonProperty(PropertyName = "Display to player")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Position - X minimum")]
                    public float Xmin { get; set; }

                    [JsonProperty(PropertyName = "Position - X maximum")]
                    public float XMax { get; set; }

                    [JsonProperty(PropertyName = "Position - Y minimum")]
                    public float YMin { get; set; }

                    [JsonProperty(PropertyName = "Position - Y maximum")]
                    public float YMax { get; set; }

                    [JsonProperty(PropertyName = "Background color (hex)")]
                    public string Color1 { get; set; }

                    [JsonProperty(PropertyName = "Background alpha")]
                    public float Color1A { get; set; }

                    [JsonProperty(PropertyName = "Status color (hex)")]
                    public string Color2 { get; set; }

                    [JsonProperty(PropertyName = "Status alpha")]
                    public float Color2A { get; set; }
                }
                public class UICrosshair
                {
                    [JsonProperty(PropertyName = "Display to player")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Size")]
                    public int Size { get; set; }    
                    
                    [JsonProperty(PropertyName = "Color (hex)")]
                    public string Color { get; set; }                    
                }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigData>();

            if (Configuration.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(Configuration, true);
        }

        protected override void LoadDefaultConfig() => Configuration = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Buttons = new ConfigData.ButtonConfiguration
                {
                    Inventory = "RELOAD",
                    Lights = "RELOAD",
                    MG = "FIRE_PRIMARY",
                    Rockets = "FIRE_SECONDARY",
                    PitchForward = "FORWARD",
                    PitchBackward = "BACKWARD",
                    RollLeft = "LEFT",
                    RollRight = "RIGHT",
                    ThrottleUp = "SPRINT",
                    ThrottleDown = "DUCK",
                    LockYaw = "USE",
                    FuelTank = "FIRE_THIRD"
                },
                CH47 = new ConfigData.DefaultHelicopter
                {
                    Build = new ConfigData.PatrolHelicopter.BuildOptions
                    {
                        Enabled = false,
                        Cooldown = true,
                        Costs = new List<ConfigData.PatrolHelicopter.BuildOptions.BuildOption>
                        {
                            new ConfigData.PatrolHelicopter.BuildOptions.BuildOption
                            {
                                Amount = 500,
                                Shortname = "metal.refined"
                            },
                            new ConfigData.PatrolHelicopter.BuildOptions.BuildOption
                            {
                                Amount = 100,
                                Shortname = "techparts"
                            }
                        }
                    },
                    Damage = new ConfigData.PatrolHelicopter.DamageOptions
                    {
                        Health = 7500,
                        Modifiers = new Dictionary<DamageType, float>
                        {
                            [DamageType.Bullet] = 1.0f,
                            [DamageType.Explosion] = 1.0f
                        }
                    },
                    Inventory = new ConfigData.PatrolHelicopter.InventoryOptions
                    {
                        DropInv = true,
                        DropLoot = false,
                        Enabled = true,
                        Size = 36
                    },
                    Movement = new ConfigData.PatrolHelicopter.MovementSettings
                    {       
                        PitchTilt = 30f,
                        Turn = 24f,
                        TurnTilt = 6f,
                        RollTilt = 9f,
                        EngineLift = 15000f,
                        EngineThrust = 8000f,
                        ThrustDotMin = 0.85f,
                        HoverForceScale = 0.75f,
                        LiftDotMax = 0.5f,
                        LiftFraction = 0.75f,
                        ThrustLerpSpeed = 6f,
                    },
                    Passengers = new ConfigData.PatrolHelicopter.PassengerOptions
                    {
                        Enabled = true,
                        UseClans = true,
                        UseFriends = true,
                        EjectNonFriendlies = true
                    },
                    Fuel = new ConfigData.PatrolHelicopter.FuelOptions
                    {
                        Enabled = true,
                        Consumption = 0.25f,
                        FuelType = "lowgradefuel",
                        FuelAmountMin = 10,
                        FuelAmountMax = 50,
                        GiveFuel = true
                    },
                    Repair = new ConfigData.PatrolHelicopter.RepairSettings
                    {
                        Amount = 10,
                        Damage = 30,
                        Enabled = true,
                        Shortname = "scrap"
                    },
                    Spawnable = new ConfigData.PatrolHelicopter.SpawnableOptions
                    {
                        Enabled = true,
                        Max = 2,
                        Time = 1800,
                        Spawnfile = "",
                        RandomSpawns = false,
                        Cooldown = 86400,
                        Save = false
                    },
                    Custom = false
                },
                Mini = new ConfigData.MiniHelicopter
                {
                    Build = new ConfigData.PatrolHelicopter.BuildOptions
                    {
                        Enabled = false,
                        Cooldown = true,
                        Costs = new List<ConfigData.PatrolHelicopter.BuildOptions.BuildOption>
                        {
                            new ConfigData.PatrolHelicopter.BuildOptions.BuildOption
                            {
                                Amount = 500,
                                Shortname = "metal.refined"
                            },
                            new ConfigData.PatrolHelicopter.BuildOptions.BuildOption
                            {
                                Amount = 100,
                                Shortname = "techparts"
                            }
                        }
                    },
                    Damage = new ConfigData.PatrolHelicopter.DamageOptions
                    {
                        Health = 1000,
                        Modifiers = new Dictionary<DamageType, float>
                        {
                            [DamageType.Bullet] = 1.0f,
                            [DamageType.Explosion] = 1.0f
                        }
                    },
                    Inventory = new ConfigData.PatrolHelicopter.InventoryOptions
                    {
                        DropInv = true,
                        DropLoot = false,
                        Enabled = true,
                        Size = 36
                    },
                    Movement = new ConfigData.PatrolHelicopter.MovementSettings
                    {
                        PitchTilt = 30f,
                        Turn = 3f,
                        TurnTilt = 1.5f,
                        RollTilt = 40f,
                        EngineLift = 3500f,
                        EngineThrust = 2500f,
                        LiftDotMax = 0.25f,
                        ThrustDotMin = 0.5f,
                        HoverForceScale = 0.75f,
                        LiftFraction = 0.75f,
                        ThrustLerpSpeed = 8f,
                    },
                    Passengers = new ConfigData.PatrolHelicopter.PassengerOptions
                    {
                        Enabled = true,
                        UseClans = true,
                        UseFriends = true,
                        EjectNonFriendlies = true
                    },
                    Fuel = new ConfigData.PatrolHelicopter.FuelOptions
                    {
                        Enabled = true,
                        Consumption = 0.25f,
                        FuelType = "lowgradefuel",
                        FuelAmountMin = 10,
                        FuelAmountMax = 50,
                        GiveFuel = true
                    },
                    Repair = new ConfigData.PatrolHelicopter.RepairSettings
                    {
                        Amount = 10,
                        Damage = 30,
                        Enabled = true,
                        Shortname = "scrap"
                    },
                    Spawnable = new ConfigData.PatrolHelicopter.SpawnableOptions
                    {
                        Enabled = true,
                        Max = 2,
                        Time = 1800,
                        Spawnfile = "",
                        RandomSpawns = false,
                        Cooldown = 86400,
                        Save = false
                    },
                    Custom = true,
                    Convert = false
                },
                Transport = new ConfigData.MiniHelicopter
                {
                    Build = new ConfigData.PatrolHelicopter.BuildOptions
                    {
                        Enabled = false,
                        Cooldown = true,
                        Costs = new List<ConfigData.PatrolHelicopter.BuildOptions.BuildOption>
                        {
                            new ConfigData.PatrolHelicopter.BuildOptions.BuildOption
                            {
                                Amount = 500,
                                Shortname = "metal.refined"
                            },
                            new ConfigData.PatrolHelicopter.BuildOptions.BuildOption
                            {
                                Amount = 100,
                                Shortname = "techparts"
                            }
                        }
                    },
                    Damage = new ConfigData.PatrolHelicopter.DamageOptions
                    {
                        Health = 1000,
                        Modifiers = new Dictionary<DamageType, float>
                        {
                            [DamageType.Bullet] = 1.0f,
                            [DamageType.Explosion] = 1.0f
                        }
                    },
                    Inventory = new ConfigData.PatrolHelicopter.InventoryOptions
                    {
                        DropInv = true,
                        DropLoot = false,
                        Enabled = true,
                        Size = 36
                    },
                    Movement = new ConfigData.PatrolHelicopter.MovementSettings
                    {
                        PitchTilt = 30f,
                        Turn = 10f,
                        TurnTilt = 6f,
                        RollTilt = 40f,
                        EngineLift = 4500f,
                        EngineThrust = 3000f,
                        ThrustDotMin = 0.5f,
                        HoverForceScale = 0.9f,
                        LiftDotMax = 0.5f,
                        LiftFraction = 0.45f,
                        ThrustLerpSpeed = 8f,
                    },
                    Passengers = new ConfigData.PatrolHelicopter.PassengerOptions
                    {
                        Enabled = true,
                        UseClans = true,
                        UseFriends = true,
                        EjectNonFriendlies = true
                    },
                    Fuel = new ConfigData.PatrolHelicopter.FuelOptions
                    {
                        Enabled = true,
                        Consumption = 0.25f,
                        FuelType = "lowgradefuel",
                        FuelAmountMin = 10,
                        FuelAmountMax = 50,
                        GiveFuel = true
                    },
                    Repair = new ConfigData.PatrolHelicopter.RepairSettings
                    {
                        Amount = 10,
                        Damage = 30,
                        Enabled = true,
                        Shortname = "scrap"
                    },
                    Spawnable = new ConfigData.PatrolHelicopter.SpawnableOptions
                    {
                        Enabled = true,
                        Max = 2,
                        Time = 1800,
                        Spawnfile = "",
                        RandomSpawns = false,
                        Cooldown = 86400,
                        Save = false
                    },
                    Custom = true,
                    Convert = false
                },
                UH1Y = new ConfigData.AttackHelicopter
                {
                    Build = new ConfigData.PatrolHelicopter.BuildOptions
                    {
                        Enabled = false,
                        Cooldown = true,
                        Costs = new List<ConfigData.PatrolHelicopter.BuildOptions.BuildOption>
                        {
                            new ConfigData.PatrolHelicopter.BuildOptions.BuildOption
                            {
                                Amount = 500,
                                Shortname = "metal.refined"
                            },
                            new ConfigData.PatrolHelicopter.BuildOptions.BuildOption
                            {
                                Amount = 100,
                                Shortname = "techparts"
                            }
                        }
                    },
                    Damage = new ConfigData.PatrolHelicopter.DamageOptions
                    {
                        Health = 7500,
                        Modifiers = new Dictionary<DamageType, float>
                        {
                            [DamageType.Bullet] = 1.0f,
                            [DamageType.Explosion] = 1.0f
                        }
                    },
                    Inventory = new ConfigData.PatrolHelicopter.InventoryOptions
                    {
                        DropInv = true,
                        DropLoot = false,
                        Enabled = true,
                        Size = 36
                    },
                    Movement = new ConfigData.PatrolHelicopter.MovementSettings
                    {
                        PitchTilt = 30f,
                        Turn = 30f,
                        TurnTilt = 20f,
                        RollTilt = 40f,
                        EngineLift = 6000f,
                        EngineThrust = 3500f,
                        ThrustDotMin = 0.85f,
                        HoverForceScale = 0.75f,
                        LiftDotMax = 0.5f,
                        LiftFraction = 0.85f,
                        ThrustLerpSpeed = 12f,
                    },
                    Passengers = new ConfigData.PatrolHelicopter.PassengerOptions
                    {
                        Enabled = true,
                        UseClans = true,
                        UseFriends = true,
                        EjectNonFriendlies = true
                    },
                    Fuel = new ConfigData.PatrolHelicopter.FuelOptions
                    {
                        Enabled = true,
                        Consumption = 0.5f,
                        FuelType = "lowgradefuel",
                        FuelAmountMin = 10,
                        FuelAmountMax = 50,
                        GiveFuel = true
                    },
                    Repair = new ConfigData.PatrolHelicopter.RepairSettings
                    {
                        Amount = 10,
                        Damage = 30,
                        Enabled = true,
                        Shortname = "scrap"
                    },
                    Spawnable = new ConfigData.PatrolHelicopter.SpawnableOptions
                    {
                        Enabled = true,
                        Max = 2,
                        Time = 1800,
                        Spawnfile = "",
                        RandomSpawns = false,
                        Cooldown = 86400,
                        Save = false
                    },
                    Weapons = new ConfigData.AttackHelicopter.WeaponOptions
                    {
                        MG = new ConfigData.AttackHelicopter.WeaponOptions.WeaponSystem
                        {
                            Accuracy = 1.25f,
                            Damage = 10f,
                            Enabled = true,
                            Interval = 0.1f,
                            RequireAmmo = false,
                            Type = "ammo.rifle.hv"
                        },
                        Rocket = new ConfigData.AttackHelicopter.WeaponOptions.WeaponSystem
                        {
                            Accuracy = 0.025f,
                            Damage = 90f,
                            Enabled = true,
                            Interval = 1.75f,
                            RequireAmmo = false,
                            Type = "ammo.rocket.hv"
                        }
                    },
                },
                UI = new ConfigData.UIOptions
                {
                    Fuel = new ConfigData.UIOptions.UICounter
                    {
                        Color1 = "#F2F2F2",
                        Color1A = 0.05f,
                        Color2 = "#ce422b",
                        Color2A = 1,
                        Enabled = true,
                        Xmin = 0.69f,
                        XMax = 0.83f,
                        YMin = 0.139f,
                        YMax = 0.174f
                    },
                    Health = new ConfigData.UIOptions.UICounter
                    {
                        Color1 = "#F2F2F2",
                        Color1A = 0.05f,
                        Color2 = "#ce422b",
                        Color2A = 0.6f,
                        Enabled = true,
                        Xmin = 0.69f,
                        XMax = 0.83f,
                        YMin = 0.1f,
                        YMax = 0.135f
                    },
                    Rocket = new ConfigData.UIOptions.UICounter
                    {
                        Color1 = "#F2F2F2",
                        Color1A = 0.05f,
                        Color2 = "#ce422b",
                        Color2A = 0.6f,
                        Enabled = true,
                        Xmin = 0.69f,
                        XMax = 0.83f,
                        YMin = 0.06f,
                        YMax = 0.096f
                    },
                    MG = new ConfigData.UIOptions.UICounter
                    {
                        Color1 = "#F2F2F2",
                        Color1A = 0.05f,
                        Color2 = "#ce422b",
                        Color2A = 0.6f,
                        Enabled = true,
                        Xmin = 0.69f,
                        XMax = 0.83f,
                        YMin = 0.021f,
                        YMax = 0.056f
                    },
                    Aim = new ConfigData.UIOptions.UICrosshair
                    {
                        Color = "#ce422b",
                        Enabled = true,
                        Size = 25
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();
            if (Configuration.Version < new VersionNumber(0, 2, 0))
            {
                Configuration.CH47 = baseConfig.CH47;
                Configuration.UH1Y = baseConfig.UH1Y;
            }

            if (Configuration.Version < new VersionNumber(0, 2, 02))
            {
                Configuration.CH47.Repair = baseConfig.CH47.Repair;
                Configuration.UH1Y.Repair = baseConfig.UH1Y.Repair;
            }

            if (Configuration.Version < new VersionNumber(0, 2, 05))
            {
                Configuration.CH47.Fuel.Consumption = baseConfig.CH47.Fuel.Consumption;
                Configuration.UH1Y.Fuel.Consumption = baseConfig.UH1Y.Fuel.Consumption;
            }

            if (Configuration.Version < new VersionNumber(0, 2, 12))
            {
                Configuration.CH47.Build = baseConfig.CH47.Build;
                Configuration.UH1Y.Build = baseConfig.UH1Y.Build;
            }

            if (Configuration.Version < new VersionNumber(0, 2, 13))
            {
                Configuration.Mini = baseConfig.Mini;
            }

            if (Configuration.Version < new VersionNumber(0, 2, 17))
                Configuration.Mini.Custom = baseConfig.Mini.Custom;

            if (Configuration.Version < new VersionNumber(0, 2, 20))
                Configuration.Mini.Convert = false;

            if (Configuration.Version < new VersionNumber(0, 2, 28))
                Configuration.Transport = baseConfig.Transport;

            if (Configuration.Version < new VersionNumber(0, 3, 0))
            {
                Configuration.Mini.Movement = baseConfig.Mini.Movement;
                Configuration.Transport.Movement = baseConfig.Transport.Movement;
                Configuration.CH47.Movement = baseConfig.CH47.Movement;
                Configuration.UH1Y.Movement = baseConfig.UH1Y.Movement;
            }
                        
            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Data Management
        private void SaveData()
        {
            restorationdata.WriteObject(restoreData);
            cooldowns.WriteObject(userCooldowns);
        }

        private void LoadData()
        {
            restorationdata = Interface.Oxide.DataFileSystem.GetFile("helicommander_data");
            cooldowns = Interface.Oxide.DataFileSystem.GetFile("helicommander_cooldowns");

            try
            {
                restoreData = restorationdata.ReadObject<RestoreData>();
            }
            catch
            {
                restoreData = new RestoreData();
            }
            try
            {
                userCooldowns = cooldowns.ReadObject<Dictionary<ulong, double>>();
            }
            catch
            {
                userCooldowns = new Dictionary<ulong, double>();
            }
        }

        private class RestoreData
        {
            [JsonProperty(PropertyName = "restoreData")]
            private Hash<ulong, HelicopterData> restoreData = new Hash<ulong, HelicopterData>();

            internal void AddData(BaseController controller)
            {
                restoreData[controller.GetEntity().net.ID.Value] = new HelicopterData(controller);
            }

            internal void RemoveData(NetworkableId netId)
            {
                if (HasRestoreData(netId))
                    restoreData.Remove(netId.Value);                
            }

            internal bool HasRestoreData(NetworkableId netId) => restoreData.ContainsKey(netId.Value);

            internal void RestoreVehicles(Func<Vector3, Quaternion, HelicopterType, bool, ulong, bool, BaseEntity> spawnAtLocation)
            {                
                if (restoreData.Count > 0)
                {
                    for (int i = restoreData.Count - 1; i >= 0; i--)
                    {
                        KeyValuePair<ulong, HelicopterData> helicopterData = restoreData.ElementAt(i);
                        BaseController controller = spawnAtLocation(helicopterData.Value.GetPosition(), helicopterData.Value.GetRotation(), helicopterData.Value.type, true, 0UL, false).GetComponent<BaseController>();
                        helicopterData.Value.RestoreVehicle(controller);
                        restoreData.Remove(helicopterData.Key);
                    }
                }
            }

            internal void Clear() => restoreData.Clear();

            private class HelicopterData
            {
                public HelicopterType type;
                public float x, y, z, rx, ry, rz, health;
                public InventoryData inventoryData;

                internal HelicopterData() { }

                internal HelicopterData(BaseController baseController)
                {
                    type = baseController is UH1YController ? HelicopterType.UH1Y : 
                           baseController is TransportController ? HelicopterType.Transport : 
                           baseController is MiniController ? HelicopterType.Mini : 
                           HelicopterType.CH47;

                    SetPosition(baseController.Transform.position);
                    SetRotation(baseController.Transform.rotation);

                    health = baseController.GetEntity().health;
                    inventoryData = new InventoryData(baseController);
                }

                private void SetPosition(Vector3 position)
                {
                    x = position.x;
                    y = position.y;
                    z = position.z;
                }

                private void SetRotation(Quaternion rotation)
                {
                    rx = rotation.eulerAngles.x;
                    ry = rotation.eulerAngles.y;
                    rz = rotation.eulerAngles.z;
                }

                internal Vector3 GetPosition() => new Vector3(x, y, z);

                internal Quaternion GetRotation() => Quaternion.Euler(rx, ry, rz);

                internal void RestoreVehicle(BaseController baseController)
                {
                    baseController.GetEntity().health = health;
                    if (baseController.Inventory != null)
                        RestoreAllItems(baseController, inventoryData);
                }

                private void RestoreAllItems(BaseController baseController, InventoryData inventoryData)
                {
                    if (baseController == null)
                        return;

                    RestoreItems(baseController, inventoryData.vehicleContainer, true);
                    RestoreItems(baseController, inventoryData.fuelContainer, false);
                }

                private bool RestoreItems(BaseController baseController, ItemData[] itemData, bool isInventory)
                {
                    ConfigData.PatrolHelicopter config = baseController is UH1YController ? Configuration.UH1Y : 
                                                       baseController is TransportController ? Configuration.Transport : 
                                                       baseController is MiniController ? Configuration.Mini : 
                                                       Configuration.CH47 as ConfigData.PatrolHelicopter;

                    if ((!isInventory && !config.Fuel.Enabled) || (isInventory && !config.Inventory.Enabled) || itemData == null || itemData.Length == 0)
                        return true;

                    for (int i = 0; i < itemData.Length; i++)
                    {
                        Item item = CreateItem(itemData[i]);
                        item.MoveToContainer(isInventory ? baseController.Inventory : baseController.FuelSystem.GetFuelContainer().inventory, itemData[i].position, true);
                    }
                    return true;
                }

                private Item CreateItem(ItemData itemData)
                {
                    Item item = ItemManager.CreateByItemID(itemData.itemid, itemData.amount, itemData.skin);
                    item.condition = itemData.condition;
                    if (itemData.instanceData != null)
                        itemData.instanceData.Restore(item);

                    item.blueprintTarget = itemData.blueprintTarget;

                    BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                    if (weapon != null)
                    {
                        if (!string.IsNullOrEmpty(itemData.ammotype))
                            weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                        weapon.primaryMagazine.contents = itemData.ammo;
                    }
                    if (itemData.contents != null)
                    {
                        foreach (ItemData contentData in itemData.contents)
                        {
                            Item newContent = ItemManager.CreateByItemID(contentData.itemid, contentData.amount);
                            if (newContent != null)
                            {
                                newContent.condition = contentData.condition;
                                newContent.MoveToContainer(item.contents);
                            }
                        }
                    }
                    return item;
                }

                internal class InventoryData
                {
                    public ItemData[] vehicleContainer = new ItemData[0];
                    public ItemData[] fuelContainer = new ItemData[0];

                    public InventoryData() { }

                    internal InventoryData(BaseController baseController)
                    {
                        ConfigData.PatrolHelicopter config = baseController is UH1YController ? Configuration.UH1Y : 
                                                           baseController is TransportController ? Configuration.Transport : 
                                                           baseController is MiniController ? Configuration.Mini : 
                                                           Configuration.CH47 as ConfigData.PatrolHelicopter;

                        if (config.Inventory.Enabled && baseController.Inventory != null)
                            vehicleContainer = GetItems(baseController.Inventory).ToArray();

                        if (config.Fuel.Enabled && baseController.FuelSystem.GetFuelContainer() != null)
                            fuelContainer = GetItems(baseController.FuelSystem.GetFuelContainer().inventory).ToArray();
                    }

                    private IEnumerable<ItemData> GetItems(ItemContainer container)
                    {
                        return container.itemList.Select(item => new ItemData
                        {
                            itemid = item.info.itemid,
                            amount = item.amount,
                            ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                            ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                            position = item.position,
                            skin = item.skin,
                            condition = item.condition,
                            instanceData = new ItemData.InstanceData(item),
                            blueprintTarget = item.blueprintTarget,
                            contents = item.contents?.itemList.Select(item1 => new ItemData
                            {
                                itemid = item1.info.itemid,
                                amount = item1.amount,
                                condition = item1.condition
                            }).ToArray()
                        });
                    }
                }

                public class ItemData
                {
                    public int itemid;
                    public ulong skin;
                    public int amount;
                    public float condition;
                    public int ammo;
                    public string ammotype;
                    public int position;
                    public int blueprintTarget;
                    public InstanceData instanceData;
                    public ItemData[] contents;

                    public class InstanceData
                    {
                        public int dataInt;
                        public int blueprintTarget;
                        public int blueprintAmount;

                        public InstanceData() { }
                        public InstanceData(Item item)
                        {
                            if (item.instanceData == null)
                                return;

                            dataInt = item.instanceData.dataInt;
                            blueprintAmount = item.instanceData.blueprintAmount;
                            blueprintTarget = item.instanceData.blueprintTarget;
                        }

                        public void Restore(Item item)
                        {
                            item.instanceData = new ProtoBuf.Item.InstanceData();
                            item.instanceData.blueprintAmount = blueprintAmount;
                            item.instanceData.blueprintTarget = blueprintTarget;
                            item.instanceData.dataInt = dataInt;
                        }
                    }
                }
            }                       
        }

        public struct ItemCost
        {
            public int itemId;
            public int amount;

            public ItemCost(int itemId, int amount)
            {
                this.itemId = itemId;
                this.amount = amount;
            }
        }
        #endregion

        #region Localization
        private string Message(string key, ulong playerId = 0UL) => lang.GetMessage(key, this, playerId.ToString());

        private static Func<string, ulong, string> GetMessage;

        private readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["is_flying"] = "<color=#D3D3D3>You can not enter the helicopter when you are flying</color>",
            ["in_use"] = "<color=#D3D3D3>This helicopter is already in use</color>",
            ["not_friend"] = "<color=#D3D3D3>You must be a friend or clanmate with the operator</color>",
            ["passenger_not_enabled"] = "<color=#D3D3D3>Passengers are not allowed in this helicopter</color>",
            ["passenger_enter"] = "<color=#D3D3D3>You have entered the helicopter as a passenger</color>",
            ["controls_new"] = "<color=#ce422b>Helicopter Controls:</color>\n<color=#D3D3D3>Pitch Forward:</color> <color=#ce422b>{0}</color>\n<color=#D3D3D3>Pitch Backward:</color> <color=#ce422b>{1}</color>\n<color=#D3D3D3>Roll Left:</color> <color=#ce422b>{2}</color>\n<color=#D3D3D3>Roll Right:</color> <color=#ce422b>{3}</color>\n<color=#D3D3D3>Throttle Up:</color> <color=#ce422b>{4}</color>\n<color=#D3D3D3>Throttle Down:</color> <color=#ce422b>{5}</color>\n<color=#D3D3D3>Turn Left/Right:</color> <color=#ce422b>MOUSE LOOK</color>\n<color=#D3D3D3>Lock Left/Right Turn:</color> <color=#ce422b>HOLD {6}</color>",
            ["controls_new_mini"] = "<color=#ce422b>Helicopter Controls:</color>\n<color=#D3D3D3>Pitch Forward:</color> <color=#ce422b>{0}</color>\n<color=#D3D3D3>Pitch Backward:</color> <color=#ce422b>{1}</color>\n<color=#D3D3D3>Roll Left:</color> <color=#ce422b>{2}</color>\n<color=#D3D3D3>Roll Right:</color> <color=#ce422b>{3}</color>\n<color=#D3D3D3>Throttle Up:</color> <color=#ce422b>{4}</color>\n<color=#D3D3D3>Throttle Down:</color> <color=#ce422b>{5}</color>",
            ["fire_rocket"] = "<color=#D3D3D3>Fire Rockets </color><color=#ce422b>{0}</color>",            
            ["fire_mg"] = "<color=#D3D3D3>Fire MG </color><color=#ce422b>{0}</color>",
            ["enter_exit"] = "<color=#D3D3D3>Enter/Exit Vehicle </color><color=#ce422b>{0}</color>",
            ["toggle_lights"] = "<color=#D3D3D3>Toggle Lights </color><color=#ce422b>{0}</color>",
            ["access_inventory"] = "<color=#D3D3D3>Access Inventory (from outside of the vehicle) </color><color=#ce422b>{0}</color>",
            ["access_fuel"] = "<color=#D3D3D3>Access Fuel Tank (from outside of the vehicle) </color><color=#ce422b>{0}</color>",
            ["fuel_type"] = "<color=#D3D3D3>This vehicle requires </color><color=#ce422b>{0}</color> <color=#D3D3D3>to run!</color>",
            ["no_ammo_rocket"] = "<color=#D3D3D3>You do not have ammunition to fire the rocket launchers. It requires </color><color=#ce422b>{0}</color>",
            ["no_ammo_mg"] = "<color=#D3D3D3>You do not have ammunition to fire the machine guns. It requires </color><color=#ce422b>{0}</color>",
            ["health"] = "HLTH: ",
            ["fuel"] = "FUEL: {0} L",
            ["rocket"] = "RCKT: {0}",
            ["mgun"] = "MGUN: {0}",
            ["nopermission"] = "<color=#D3D3D3>You do not have permission to enter the helicopter</color>",
            ["noflypermission"] = "<color=#D3D3D3>You do not have permission to fly the helicopter</color>",
            ["onCooldown"] = "<color=#D3D3D3>You can not use this command for another:</color> <color=#ce422b>{0}</color>",
            ["repairhelp"] = "<color=#D3D3D3>You can make repairs to this vehicle using a hammer which costs </color><color=#ce422b>{0}x {1}</color> <color=#D3D3D3>per hit</color>",
            ["pilotKick"] = "<color=#D3D3D3>You have been ejected because you are not friends with the pilot</color>",
            ["fullhealth"] = "<color=#D3D3D3>This vehicle is already at full health</color>",
            ["noresources"] = "<color=#D3D3D3>You need atleast </color><color=#ce422b>{0}x {1}</color> <color=#D3D3D3>to make repairs</color>",
            ["ch47cost"] = "<color=#ce422b>CH47 Costs:</color>",
            ["uh1ycost"] = "<color=#ce422b>UH1Y Costs:</color>",
            ["minicost"] = "<color=#ce422b>Minicopter Costs:</color>",
            ["transportcost"] = "<color=#ce422b>Scrap Transport Helicopter Costs:</color>",
            ["notenoughres"] = "<color=#D3D3D3>You need <color=#ce422b>{0} {1}</color> to build</color>"
        };
        #endregion
    }
}
