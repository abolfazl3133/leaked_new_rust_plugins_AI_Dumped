using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SlidingDoors", "k1lly0u", "2.0.10")]
    class SlidingDoors : RustPlugin
    {
        #region Fields
        private StoredData storedData;
        private DynamicConfigFile data;

        private static SlidingDoors Instance { get; set; }

        private bool wipeData = false;

        private const string PERMISSION_WOOD = "slidingdoors.wood";
        private const string PERMISSION_METAL = "slidingdoors.metal";
        private const string PERMISSION_ARMOUR = "slidingdoors.armour";
        private const string PERMISSION_USE = "slidingdoors.use";
        private const string PERMISSION_ALL = "slidingdoors.all";
        private const string PERMISSION_AUTO_DOOR = "slidingdoors.autodoor";
        private const string PERMISSION_AUTO_DEPLOY = "slidingdoors.autodeploy";

        private const string DOOR_WOOD_SPN = "door.hinged.wood";
        private const string DOOR_METAL_SPN = "door.hinged.metal";
        private const string DOOR_ARMOUR_SPN = "door.hinged.toptier";

        private const string EFFECT_WOOD = "assets/prefabs/building/door.hinged/effects/door-wood-open-end.prefab";
        private const string EFFECT_METAL = "assets/prefabs/building/door.hinged/effects/door-metal-open-end.prefab";

        //private FieldInfo OwnerItemUID = typeof(Deployer).GetField("ownerItemUID", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("slidingdoors_data");

            permission.RegisterPermission(PERMISSION_ALL, this);
            permission.RegisterPermission(PERMISSION_ARMOUR, this);
            permission.RegisterPermission(PERMISSION_AUTO_DEPLOY, this);
            permission.RegisterPermission(PERMISSION_AUTO_DOOR, this);
            permission.RegisterPermission(PERMISSION_METAL, this);
            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_WOOD, this);

            lang.RegisterMessages(Messages, this);

            if (!configData.Door.ApplyToAll)
                Unsubscribe(nameof(OnEntitySpawned));

            Instance = this;
        }

        private void OnNewSave() => wipeData = true;

        private void OnServerSave()
        {
            if (!Interface.Oxide.IsShuttingDown)
            {
                for (int i = SlidingDoor._allDoors.Count - 1; i >= 0; i--)                
                    SlidingDoor._allDoors[i]?.CloseForGameSave();

                SaveRestore.Instance.StartCoroutine(RestoreOnSaveFinished());
            }
        }        
        
        private void OnServerInitialized()
        {
            LoadData();

            if (wipeData)
            {
                storedData.ids.Clear();
                SaveData();
            }

            ServerMgr.Instance.StartCoroutine(LoadSlidingDoors());
        }

        private void OnDoorOpened(Door door, BasePlayer player)
        {
            SlidingDoor slidingDoor = door.GetComponent<SlidingDoor>();

            if (slidingDoor == null)
                return;

            door.SetOpen(false);

            slidingDoor.OnDoorToggled();                        
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            SlidingDoor slidingDoor = entity.GetComponent<SlidingDoor>();

            if (slidingDoor == null)
                return;

            if (slidingDoor.IsOpen)
            {
                Deployable deployable = deployer.GetDeployable();
                if (deployable != null)
                {
                    entity.GetSlot(deployable.slot)?.Kill();

                    BasePlayer ownerPlayer = deployer.GetOwnerPlayer();
                    if (ownerPlayer == null || ownerPlayer.inventory == null)
                        return;

                    ownerPlayer.ChatMessage(msg("Error.DoorOpen", ownerPlayer.userID));

                    int itemId = GetOwnerItemID(deployer);
                    if (itemId != 0)
                        ownerPlayer.GiveItem(ItemManager.CreateByItemID(itemId, 1, 0));
                }                    
            }
        }

        private int GetOwnerItemID(Deployer deployer)
        {
            BasePlayer ownerPlayer = deployer.GetOwnerPlayer();
            if (ownerPlayer == null || ownerPlayer.inventory == null)            
                return 0;

            //ItemId itemId = (ItemId)OwnerItemUID.GetValue(deployer);
            return ownerPlayer.inventory.FindItemByUID(deployer.ownerItemUID)?.info.itemid ?? 0;
        }

        private void OnEntitySpawned(BaseNetworkable baseNetworkable)
        {            
            Door door = baseNetworkable as Door;
            if (door == null || !door.IsValid() || door.OwnerID == 0UL)
                return;

            if (!HasPermission(door.OwnerID, PERMISSION_AUTO_DEPLOY))
                return;

            SlidingDoor.DoorType doorType = ToDoorType(door.ShortPrefabName);
            if (doorType == SlidingDoor.DoorType.Invalid)
                return;

            door.gameObject.AddComponent<SlidingDoor>();
            storedData.ids.Add(door.net.ID.Value);
        }

        private void OnEntityKill(BaseNetworkable baseNetworkable)
        {
            if (baseNetworkable == null || baseNetworkable.net == null)
                return;

            if (storedData?.ids?.Contains(baseNetworkable.net.ID.Value) ?? false)
            {
                storedData.ids.Remove(baseNetworkable.net.ID.Value);
                SaveData();
            }
        }

        private void Unload()
        {
            for (int i = SlidingDoor._allDoors.Count - 1; i >= 0; i--)
            {
                SlidingDoor slidingDoor = SlidingDoor._allDoors[i];
                slidingDoor?.ResetDoorState();
                UnityEngine.Object.Destroy(slidingDoor);
            }

            configData = null;
            Instance = null;
        }
        #endregion

        #region Functions
        private IEnumerator LoadSlidingDoors()
        {
            if (storedData.ids.Count > 0)
            {                
                Door[] doors = BaseNetworkable.serverEntities.Where(x => x is Door).Cast<Door>().ToArray();
                
                Stopwatch sw = Stopwatch.StartNew();

                for (int i = 0; i < doors.Length; i++)
                {
                    if (sw.Elapsed.TotalMilliseconds > 0.1)
                    {
                        yield return CoroutineEx.waitForEndOfFrame;
                        yield return CoroutineEx.waitForEndOfFrame;
                        sw.Reset();
                    }

                    Door door = doors[i];
                    if (door == null || !door.IsValid())
                        continue;

                    if (storedData.ids.Contains(door.net.ID.Value))                    
                        door.gameObject.AddComponent<SlidingDoor>();                    
                }

                Puts(string.Format("Loaded {0} sliding doors from data", SlidingDoor._allDoors.Count));
            }
        }

        private IEnumerator RestoreOnSaveFinished()
        {
            while (SaveRestore.IsSaving)            
                yield return null;            

            yield return CoroutineEx.waitForEndOfFrame;
            yield return CoroutineEx.waitForEndOfFrame;

            for (int i = SlidingDoor._allDoors.Count - 1; i >= 0; i--)            
                SlidingDoor._allDoors[i]?.RestoreAfterGameSave();            
        }

        private static T FindEntityFromRay<T>(BasePlayer player) where T : Component
        {
            Ray ray = new Ray(player.eyes.position, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward);

            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 1.5f))
                return null;

            return hit.collider.GetComponentInParent<T>();            
        }

        private static bool HasPermission(ulong ownerId, string perm) => Instance.permission.UserHasPermission(ownerId.ToString(), PERMISSION_ALL) || Instance.permission.UserHasPermission(ownerId.ToString(), perm);

        private static SlidingDoor.DoorType ToDoorType(string shortPrefabName)
        {
            return shortPrefabName.Equals(DOOR_WOOD_SPN) ? SlidingDoor.DoorType.Wood :
                shortPrefabName.Equals(DOOR_METAL_SPN) ? SlidingDoor.DoorType.Metal :
                shortPrefabName.Equals(DOOR_ARMOUR_SPN) ? SlidingDoor.DoorType.Armoured : SlidingDoor.DoorType.Invalid;
        }
        #endregion

        #region Component
        private class SlidingDoor : MonoBehaviour
        {
            public static List<SlidingDoor> _allDoors = new List<SlidingDoor>();

            protected Door Entity { get; private set; }

            private List<DoorEntity> Children = new List<DoorEntity>();

            private Transform tr;

            private Vector3 originalPosition;

            private Vector3 openPosition;

            private bool isOpening = false;

            private bool isPaused = false;

            private DoorType doorType;

            private float timeTaken = 0f;

            private float timeToTake;


            public bool IsOpen { get { return tr.position != originalPosition; } }


            private void Awake()
            {                
                Entity = GetComponent<Door>();

                doorType = ToDoorType(Entity.ShortPrefabName);
                if (doorType == DoorType.Invalid)
                {
                    Destroy(this);
                    return;
                }

                Entity.CloseRequest();

                Entity.syncPosition = true;  
                
                tr = Entity.transform;

                originalPosition = tr.position;
                openPosition = originalPosition + (-tr.forward * 1.15f);                

                timeTaken = timeToTake = doorType == DoorType.Wood ? configData.Door.WoodSpeed : doorType == DoorType.Metal ? configData.Door.MetalSpeed : configData.Door.ArmourSpeed;

                UpdateCurrentChildren();

                SendNetworkUpdate(true);
                
                enabled = false;

                _allDoors.Add(this);
            }
            
            private void Update()
            {
                if (isPaused)
                    return;

                timeTaken += UnityEngine.Time.deltaTime;

                float delta = Mathf.InverseLerp(0f, timeToTake, timeTaken);

                tr.position = isOpening ? Vector3.Lerp(originalPosition, openPosition, delta) : Vector3.Lerp(openPosition, originalPosition, delta);
                tr.hasChanged = true;

                for (int i = 0; i < Children.Count; i++)                
                    Children[i].Move(isOpening, delta);

                SendNetworkUpdate(false);

                if (timeTaken >= timeToTake)
                {
                    timeTaken = timeToTake;

                    enabled = false;

                    SendNetworkUpdate(true);

                    if (configData.AutoDoor.Enabled && HasPermission(Entity.OwnerID, PERMISSION_AUTO_DOOR) && isOpening)
                        InvokeHandler.Invoke(this, OnDoorToggled, configData.AutoDoor.OpenTime);
                    return;
                }
            }

            private void OnDestroy()
            {
                Children.Clear();
                Children = null;

                _allDoors.Remove(this);
            }

            #region Hackery
            private bool onSave_enabled;
            private float onSave_timeTaken;
            private Vector3 onSave_currentPosition;

            internal void CloseForGameSave()
            {
                if (Entity != null && !Entity.IsDestroyed)
                {
                    onSave_enabled = enabled;
                    onSave_timeTaken = timeTaken;
                    onSave_currentPosition = tr.position;

                    enabled = false;

                    tr.position = originalPosition;
                    tr.hasChanged = true;

                    for (int i = 0; i < Children?.Count; i++)
                        Children[i].ResetForGameSave();
                }
            }

            internal void RestoreAfterGameSave()
            {
                if (Entity != null && !Entity.IsDestroyed)
                {
                    tr.position = onSave_currentPosition;
                    tr.hasChanged = true;

                    for (int i = 0; i < Children?.Count; i++)
                        Children[i].Restore();

                    timeTaken = onSave_timeTaken;
                    enabled = onSave_enabled;
                }
            }
            #endregion

            internal void ResetDoorState()
            {
                enabled = false;

                InvokeHandler.CancelInvoke(this, OnDoorToggled);

                if (Entity != null && !Entity.IsDestroyed)
                {
                    tr.position = originalPosition;
                    tr.hasChanged = true;

                    for (int i = 0; i < Children?.Count; i++)
                        Children[i].Reset();

                    SendNetworkUpdate(false);                    
                }
            }

            internal void OnDoorToggled()
            {
                if (enabled && !isPaused)
                {
                    enabled = false;
                    isPaused = true;
                    SendNetworkUpdate(true);
                    return;
                }

                InvokeHandler.CancelInvoke(this, OnDoorToggled);

                UpdateCurrentChildren();

                isOpening = !isOpening;
                timeTaken = Mathf.Abs(timeToTake - timeTaken);

                isPaused = false;
                enabled = true;

                Effect.server.Run((int)doorType == 0 ? EFFECT_WOOD : EFFECT_METAL, tr.position);
            }

            private void UpdateCurrentChildren()
            {
                for (int i = Children.Count - 1; i >= 0; i--)
                {
                    DoorEntity doorEntity = Children[i];

                    if (!doorEntity.Entity || doorEntity.Entity.IsDestroyed)
                        Children.RemoveAt(i);
                }

                Entity.children.ForEach(x =>
                {
                    if (!Children.Any(y => y.Entity == x))
                        Children.Add(new DoorEntity(x));
                });
            }

            private void SendNetworkUpdate(bool isFinal)
            {               
                if (!Entity.IsValid())
                    return;

                if (doorType != DoorType.Armoured || isFinal)
                {
                    NetWrite netWrite = Net.sv.StartWrite();
                    netWrite.PacketID(Message.Type.EntityDestroy);
                    netWrite.EntityID(Entity.net.ID);
                    netWrite.UInt8((byte)0);
                    netWrite.Send(new SendInfo(Entity.net.@group.subscribers));
                }
                else
                {
                    NetWrite netWrite = Net.sv.StartWrite();
                    netWrite.PacketID(Message.Type.EntityPosition);
                    netWrite.EntityID(Entity.net.ID);
                    Vector3 v = tr.position;
                    netWrite.Vector3(in v);
                    netWrite.Send(new SendInfo(Entity.net.@group.subscribers));
                }

                List<Connection> connections = Entity.net.@group.subscribers;
                for (int i = 0; i < connections.Count; i++)
                {
                    BasePlayer player = connections[i].player as BasePlayer;
                    if (player != null)
                    {
                        player.SendEntitySnapshot(Entity);
                        Entity.children.ForEach(x => player.SendEntitySnapshot(x));
                    }
                }

                Entity.SendNetworkUpdateImmediate();
            }

            //private void SendFinalUpdate()
            //{
            //    if (!Entity.IsValid())
            //        return;

            //    if (netWrite.Start())
            //    {
            //        netWrite.PacketID(Message.Type.EntityDestroy);
            //        netWrite.EntityID(Entity.net.ID);
            //        netWrite.UInt8((byte)0);
            //        netWrite.Send(new SendInfo(Entity.net.@group.subscribers));
            //    }

            //    List<Connection> connections = Entity.net.@group.subscribers;
            //    for (int i = 0; i < connections.Count; i++)
            //    {
            //        BasePlayer player = connections[i].player as BasePlayer;
            //        if (player != null)
            //        {
            //            player.SendEntitySnapshot(Entity);
            //            Entity.children.ForEach(x => player.SendEntitySnapshot(x));
            //        }
            //    }

            //    Entity.SendNetworkUpdateImmediate();
            //}

            private struct DoorEntity
            {
                public BaseEntity Entity { get; private set; }

                private Transform tr;

                private Vector3 originalPosition;

                private Vector3 openPosition;

                private Vector3 onSave_position;

                public DoorEntity(BaseEntity entity)
                {
                    Entity = entity;

                    tr = entity.transform;

                    onSave_position = originalPosition = tr.position;

                    openPosition = originalPosition + (-tr.forward * 1.15f);
                }

                internal void Move(bool isOpening, float delta)
                {
                    if (tr == null)
                        return;

                    tr.position = isOpening ? Vector3.Lerp(originalPosition, openPosition, delta) : Vector3.Lerp(openPosition, originalPosition, delta);
                    tr.hasChanged = true;
                }

                internal void Reset()
                {
                    if (tr == null)
                        return;

                    tr.position = originalPosition;
                    tr.hasChanged = true;
                }

                internal void ResetForGameSave()
                {
                    if (tr == null)
                        return;

                    onSave_position = tr.position;

                    tr.position = originalPosition;
                    tr.hasChanged = true;
                }

                internal void Restore()
                {
                    if (tr == null)
                        return;

                    tr.position = onSave_position;
                    tr.hasChanged = true;
                }
            }

            internal enum DoorType { Wood, Metal, Armoured, Invalid }
        }
        #endregion

        #region Commands
        [ChatCommand("sdoor")]
        private void cmdSDoor(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.userID, PERMISSION_USE))
            {
                player.ChatMessage("You do not have permission to use this command");
                return;
            }
            
            Door door = FindEntityFromRay<Door>(player);
            if (door == null || !door.IsValid())
            {
                player.ChatMessage(msg("Error.NoDoor", player.userID));
                return;
            }

            if (configData.Door.BuildingAuth && !player.IsBuildingAuthed())
            {
                player.ChatMessage(msg("Error.NoBuildingPriv", player.userID));
                return;
            }

            BaseEntity doorLock = door.GetSlot(BaseEntity.Slot.Lock);
            if (doorLock is CodeLock && configData.Door.CodeLockAuth)
            {
                if (!(doorLock as CodeLock).whitelistPlayers.Contains(player.userID))
                {
                    player.ChatMessage(msg("Error.NoCodeLock", player.userID));
                    return;
                }
            }

            SlidingDoor.DoorType doorType = ToDoorType(door.ShortPrefabName);
            if (doorType == SlidingDoor.DoorType.Invalid)
            {
                player.ChatMessage(msg("Error.InvalidDoorType", player.userID));
                return;
            }

            if (!HasPermission(player.userID, doorType == SlidingDoor.DoorType.Wood ? PERMISSION_WOOD : doorType == SlidingDoor.DoorType.Metal ? PERMISSION_METAL : PERMISSION_ARMOUR))
            {
                player.ChatMessage(msg("Error.NoPermission.Other", player.userID));
                return;
            }

            SlidingDoor slidingDoor = door.GetComponent<SlidingDoor>();
            if (slidingDoor != null)
            {
                slidingDoor.ResetDoorState();
                UnityEngine.Object.Destroy(slidingDoor);
                storedData.ids.Remove(door.net.ID.Value);
                SaveData();

                player.ChatMessage(msg("Notification.Success.Add", player.userID));
            }
            else
            {
                door.gameObject.AddComponent<SlidingDoor>();
                storedData.ids.Add(door.net.ID.Value);
                SaveData();

                player.ChatMessage(msg("Notification.Success.Remove", player.userID));
            }
        }
        #endregion

        #region Config        
        private static ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Auto-Close Settings")]
            public AutoDoorSettings AutoDoor { get; set; }

            [JsonProperty(PropertyName = "Door Settings")]
            public DoorSettings Door { get; set; }

            public class AutoDoorSettings
            {
                [JsonProperty(PropertyName = "Amount of time before auto-closing a door (seconds)")]
                public float OpenTime { get; set; }

                [JsonProperty(PropertyName = "Enable automatic door closing")]
                public bool Enabled { get; set; }
            }

            public class DoorSettings
            {
                [JsonProperty(PropertyName = "Door Speed (Wood)")]
                public float WoodSpeed { get; set; }

                [JsonProperty(PropertyName = "Door Speed (Metal)")]
                public float MetalSpeed { get; set; }

                [JsonProperty(PropertyName = "Door Speed (Armour)")]
                public float ArmourSpeed { get; set; }

                [JsonProperty(PropertyName = "Require code lock authentication")]
                public bool CodeLockAuth { get; set; }

                [JsonProperty(PropertyName = "Require building privilege")]
                public bool BuildingAuth { get; set; }

                [JsonProperty(PropertyName = "Automatically apply to all doors")]
                public bool ApplyToAll { get; set; }
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
                AutoDoor = new ConfigData.AutoDoorSettings
                {
                    Enabled = false,
                    OpenTime = 3f
                },
                Door = new ConfigData.DoorSettings
                {
                    ApplyToAll = false,
                    ArmourSpeed = 1f,
                    BuildingAuth = true,
                    CodeLockAuth = false,
                    MetalSpeed = 1f,
                    WoodSpeed = 1f
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management
        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }

        private class StoredData
        {
            public List<ulong> ids = new List<ulong>();
        }
        #endregion

        #region Localization
        private string msg(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId == 0U ? null : playerId.ToString());

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Error.NoPermission.Simple"] = "You do not have <color=#ce422b>permission</color> to use this command",
            ["Error.NoDoor"] = "<color=#ce422b>No door found...</color>",
            ["Error.NoBuildingPriv"] = "You must have <color=#ce422b>Building Privilege</color> to use this command",
            ["Error.NoCodeLock"] = "You must be authed on the <color=#ce422b>Code Lock</color>",
            ["Error.InvalidDoorType"] = "<color=#ce422b>Invalid door type</color>. This plugin only works with player deployable single doors",
            ["Error.NoPermission.Other"] = "You do not have <color=#ce422b>permission</color> apply/remove a sliding door to this door type",
            ["Error.DoorOpen"] = "You can not place a deployable on this door whilst it is open",
            ["Notification.Success.Add"] = "You have <color=#ce422b>removed</color> the sliding door component from this door",
            ["Notification.Success.Remove"] = "You have <color=#ce422b>installed</color> a sliding door component on this door",
        };
        #endregion
    }
}
