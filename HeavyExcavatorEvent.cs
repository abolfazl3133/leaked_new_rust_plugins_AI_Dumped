using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HeavyExcavatorEvent", "Cahnu", "1.0.4")]
    class HeavyExcavatorEvent : CovalencePlugin
    {
        private const string _scientistPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_mp5.prefab";
        private const string _BradleyApcPrefab = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
        private const string _mapMarkerPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        private const string _autoTurretPlayer = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private const string _HackableCrateLoot = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";

        private PluginConfiguration _pluginConfiguration;

        private DieselEngine _engine;
        private ExcavatorArm _arm;
        private ExcavatorOutputPile _output1;
        private ExcavatorOutputPile _output2;

        private HashSet<BaseEntity> _spawnedNpcs = new HashSet<BaseEntity>();
        private HashSet<BaseEntity> _spawnedBradleys = new HashSet<BaseEntity>();
        private HashSet<BaseEntity> _spawnedTurrets = new HashSet<BaseEntity>();
        private HashSet<BaseEntity> _spawnedHackableCrates = new HashSet<BaseEntity>();
        private VendingMachineMapMarker _mapMarker;

        private bool _isEventRunning;

        private string _randomCode = string.Empty;

        private bool _isExcavatorFound => _engine && _arm && _output1 && _output2;

        private Coroutine _eventTimerCoroutine;
        private Coroutine _runEventCoroutine;
        private Coroutine _hooksEnabledCoroutine;

        private int eventSecondsTime;

        public enum LootTableType
        {
            Default = 0,
            AlphaLoot = 1,
            Other = 2,
        }

        public enum ResourceType
        {
            HQM = 0,
            Sulfur = 1,
            Stone = 2,
            MetalFrags = 3, 
            Random = 4,
        }

        #region Commands
        [Command("HeavyExcavatorStart")]
        private void cmdHeavyExcavatorStart(IPlayer player, string command)
        {
            if (!player.IsAdmin)
                return;

            if (!_isExcavatorFound)
            {
                ServerConsole.print(lang.GetMessage("UnableToFindExcavator", this));
                return;
            }

            ServerMgr.Instance.StartCoroutine(RunHeavyExcavatorEvent(true));
        }

        [Command("HeavyExcavatorStop")]
        private void cmdHeavyExcavatorStop(IPlayer player, string command)
        {
            if (!player.IsAdmin || !_isEventRunning)
                return;

            if (!_isExcavatorFound)
            {
                ServerConsole.print(lang.GetMessage("UnableToFindExcavator", this));
                return;
            }

            StopExcavatorEvent();
        }

        [Command("HePOS")]
        private void CmdGetLocalExcavatorPosition(IPlayer player)
        {
            if (!player.IsAdmin)
                return;

            if (!_engine)
            {
                player.Reply(lang.GetMessage("NoExcavatorFound", this));
                return;
            }

            var position = (player.Object as BasePlayer).transform.position;
            var locoPosition = _engine.transform.InverseTransformPoint(position);
            player.Reply("position is: " + locoPosition);
        }
        #endregion

        #region Core
        private IEnumerator RunHeavyExcavatorEvent(bool isManual = false)
        {
            if (!isManual)
            {
                while (true)
                {
                    var maxWait = _pluginConfiguration.Maximum < 0 ? 180 : _pluginConfiguration.Maximum;
                    var minWait = _pluginConfiguration.Minimum < 1 ? 1 : _pluginConfiguration.Minimum;
                    var waitTime = UnityEngine.Random.Range(minWait * 60, (maxWait * 60) + 1);

                    yield return CoroutineEx.waitForSeconds(waitTime);

                    if ((_pluginConfiguration.SkipEventIfPlayersAtExcavatorAlready && isPlayerAtExcavator())
                        || (_pluginConfiguration.SkipEventIfPlayersAtExcavatorAlready && isExcavatorAlreadyRunning())
                        || (_pluginConfiguration.SkipEventIfLootInExcavatorAlready && IsLootInExcavator()))
                        continue;

                    SetupHeavyExcavator(isManual);
                }
            }
            else
            {
                SetupHeavyExcavator(isManual);
            }
        }

        private void SetupHeavyExcavator(bool isManual)
        {
            if (!_engine)
                return;

            if (_isEventRunning)
            {
                ServerConsole.print(lang.GetMessage("EventAlreadyInProgress", this));
                return;
            }

            if (ConnectedPlayersCount() < _pluginConfiguration.MinimumNumberOfPlayers && !isManual)
            {
                ServerConsole.print(lang.GetMessage("MinimumPlayersNotMet", this));
                return;
            }

            if (_pluginConfiguration.BroadcastStartMessage)
                foreach (var player in BasePlayer.activePlayerList)
                    player.ChatMessage(lang.GetMessage("HeavyExcavatorStartedBroadcast", this, player.UserIDString));

            _isEventRunning = true;

            var item = ItemManager.CreateByItemID(1568388703, _pluginConfiguration.DieselAmount);

            if (!item.MoveToContainer(_engine.inventory))
                item.Remove();

            _engine.EngineOn();

            _arm.resourceMiningIndex = GetResourceIndex();
            _arm.BeginMining();

            SubscribeHooks(true);
            DeployNpcs();
            DeployBradleys();
            DeployAutoTurrets();
            DeployHackableCrates();
            DeployMapMarker();

            if (isManual)
                ServerConsole.print(lang.GetMessage("HeavyExcavatorStartedManual", this));
            else
                ServerConsole.print(lang.GetMessage("HeavyExcavatorStarted", this));

            _eventTimerCoroutine = ServerMgr.Instance.StartCoroutine(UpdateEventTime());

            Interface.Oxide.CallHook("HeavyExcavatorEventStarted");
        }

        private int GetResourceIndex()
        {
            return _pluginConfiguration.ResourceType == ResourceType.Random
                ? (int)UnityEngine.Random.Range(0f, 3f)
                : (int)_pluginConfiguration.ResourceType;
        }

        private int ConnectedPlayersCount()
        {
            var count = 0;

            foreach (var item in players.Connected)
            {
                count++;
            }

            return count;
        }

        private IEnumerator UpdateEventTime()
        {
            eventSecondsTime = 0;

            while (_isEventRunning)
            {
                if (eventSecondsTime > (_pluginConfiguration.EventTimeSpan * 60))
                {
                    StopExcavatorEvent();
                    break;
                }

                yield return CoroutineEx.waitForSeconds(1);

                eventSecondsTime++;
            }
        }

        private bool isPlayerAtExcavator()
        {
            LayerMask mask = 1 << LayerMask.NameToLayer("Player (Server)");

            var colliders = Physics.OverlapBox(
                _engine.transform.position,
                new Vector3(100, 100, 100),
                Quaternion.identity,
                mask);

            foreach (var item in colliders)
            {
                var player = item.gameObject.GetComponent<BasePlayer>();
                if (player && !player.IsNpc && player.userID.IsSteamId())
                {
                    return true;
                }
            }

            return false;
        }

        private bool isExcavatorAlreadyRunning()
        {
            if (_engine == null)
                return false;

            return _engine.IsOn();
        }

        private bool IsLootInExcavator()
        {
            return (_output1 != null && _output1.inventory.itemList.Count > 0)
                 || (_output2 != null && _output2.inventory.itemList.Count > 0)
                 || (_engine != null && _engine.inventory.itemList.Count > 0);
        }

        void StopExcavatorEvent()
        {
            SubscribeHooks(false);

            foreach (var entity in _spawnedNpcs)
            {
                KillEntity(entity);
            }

            foreach (var entity in _spawnedBradleys)
            {
                KillEntity(entity);
            }

            foreach (var entity in _spawnedHackableCrates)
            {
                KillEntity(entity);
            }

            foreach (var entity in _spawnedTurrets)
            {
                KillEntity(entity);
            }

            foreach (var item in _engine.inventory.itemList)
            {
                item.Remove();
            }

            foreach (var item in _output1.inventory.itemList)
            {
                item.Remove();
            }

            foreach (var item in _output2.inventory.itemList)
            {
                item.Remove();
            }

            KillEntity(_mapMarker);

            _mapMarker = null;
            _spawnedBradleys.Clear();
            _spawnedNpcs.Clear();
            _spawnedHackableCrates.Clear();
            _spawnedTurrets.Clear();

            _engine.EngineOff();
            _arm.StopMining();
            _isEventRunning = false;

            Interface.Oxide.CallHook("HeavyExcavatorEventStopped");
        }

        private void DeployMapMarker()
        {
            _mapMarker = GameManager.server.CreateEntity(_mapMarkerPrefab) as VendingMachineMapMarker;
            _mapMarker.transform.position = _engine.transform.position;
            _mapMarker.Spawn();

            _mapMarker.StartCoroutine(UpdateMapMarkerTime());
        }

        private IEnumerator UpdateMapMarkerTime()
        {
            while (_isEventRunning)
            {
                yield return CoroutineEx.waitForSeconds(1f);

                var timeLeft = (_pluginConfiguration.EventTimeSpan * 60) - eventSecondsTime;

                _mapMarker.markerShopName = lang.GetMessage("ExcavatorMarkerName", this)
                    + " - " + FormatTime(timeLeft) + " Remaining";

                _mapMarker.SendNetworkUpdate();
            }
        }

        private string FormatTime(int timeLeft)
        {
            return timeLeft < 60
                ? timeLeft.ToString() + " Seconds"
                : (timeLeft / 60).ToString() + " Minutes";
        }

        private void KillEntity(BaseEntity entity)
        {
            if (entity != null && !entity.IsDestroyed)
                entity.Kill();
        }

        private void DeployBradleys()
        {
            foreach (var location in _pluginConfiguration.BradleyLocations)
            {
                var bradley = DeployEntity(_BradleyApcPrefab, location);

                foreach (var item in bradley.GetComponentsInChildren<Transform>())
                {
                    if (item.gameObject.name.Contains("Wheel"))
                    {
                        item.gameObject.SetActive(false);
                    }
                }

                _spawnedBradleys.Add(bradley);
            }
        }

        private void DeployAutoTurrets()
        {
            foreach (var location in _pluginConfiguration.AutoTurretLocations)
            {
                var entity = DeployEntity(_autoTurretPlayer, location, true);
                var turret = entity as AutoTurret;
                var gunToLoad = ItemManager.FindItemDefinition("rifle.ak");
                var weaponItem = ItemManager.Create(gunToLoad);
                weaponItem.MoveToContainer(turret.inventory, 0);

                var heldWeapon = weaponItem.GetHeldEntity() as BaseProjectile;
                heldWeapon.primaryMagazine.contents = 0;

                turret.UpdateAttachedWeapon();
                turret.CancelInvoke(turret.UpdateAttachedWeapon);

                var weapon = turret.GetAttachedWeapon();
                weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;

                var ammoToLoad = gunToLoad.GetComponent<ItemModEntity>().entityPrefab.Get().GetComponent<BaseProjectile>().primaryMagazine.ammoType;

                for (var i = 0; i < 6; i++)
                {
                    ItemManager.Create(ammoToLoad, 128).MoveToContainer(turret.inventory, i + 1);
                }

                turret.SetFlag(BaseEntity.Flags.Reserved8, true);
                turret.InitiateStartup();
                turret.UpdateFromInput(11, 0);

                _spawnedTurrets.Add(turret);
            }
        }

        private void DeployHackableCrates()
        {
            foreach (var location in _pluginConfiguration.HackableCrateLocations)
            {
                var crate = DeployEntity(_HackableCrateLoot, location);
                _spawnedHackableCrates.Add(crate);

                foreach (var lootItem in _pluginConfiguration.SpecialLootItems)
                {
                    var itemData = lootItem.itemData;
                    //sample loot item
                    if (itemData.ItemId == 0)
                        continue;

                    var randomRoll = UnityEngine.Random.Range(0f, 100f);
                    var shouldSpawn = randomRoll < lootItem.ChanceOfDrop;

                    if (!shouldSpawn)
                        continue;

                    var item = ItemManager.CreateByItemID(itemData.ItemId,
                            UnityEngine.Random.Range(itemData.MinimumStackSize, itemData.MaximumStackSize + 1), itemData.SkinId);

                    var container = (crate as LootContainer).inventory;

                    if (container.capacity == container.itemList.Count)
                        container.capacity += 1;

                    if (!item.MoveToContainer(container))
                        item.Remove();
                }
            }
        }

        private void DeployNpcs()
        {
            foreach (var location in _pluginConfiguration.ScientistLocations)
            {
                _spawnedNpcs.Add(DeployEntity(_scientistPrefab, location));
            }
        }

        private BaseEntity DeployEntity(string prefabPath, Vector3 localPos, bool destroyGroundCheck = false, bool setParent = false)
        {
            var entity = GameManager.server.CreateEntity(prefabPath);
            entity.transform.position = _engine.transform.TransformPoint(localPos);

            if (setParent)
            {
                entity.SetParent(_engine, true);
            }

            if (destroyGroundCheck)
            {
                UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
            }

            entity.Spawn();

            return entity;
        }

        private void StopCoroutine(Coroutine coroutine)
        {
            if (coroutine != null)
                ServerMgr.Instance.StopCoroutine(coroutine);
        }

        private IEnumerator SpawnNoteDelay(PlayerCorpse corpse)
        {
            yield return CoroutineEx.waitForSeconds(1);

            var item = ItemManager.CreateByItemID(1414245162, 1);
            item.text = String.Format(lang.GetMessage("TurnOffAutoTurretsMsg", this), _randomCode);

            var container = corpse.containers[0];

            if (container.capacity == container.itemList.Count)
                corpse.containers[0].capacity += 1;

            if (!item.MoveToContainer(corpse.containers[0]))
                item.Remove();
        }

        public void SubscribeHooks(bool shouldSubscribe)
        {
            if (shouldSubscribe)
            {
                Subscribe(nameof(OnEntityDeath));
                Subscribe(nameof(OnLootEntity));
                Subscribe(nameof(OnTurretTarget));
                Subscribe(nameof(OnTurretAuthorize));
                Subscribe(nameof(CanEntityBeTargeted));
                Subscribe(nameof(CanEntityTakeDamage));
                Subscribe(nameof(CanEntityBeTargeted));
                Subscribe(nameof(OnCustomLootContainer));
                Subscribe(nameof(CanPopulateLoot));
                Subscribe(nameof(OnEntityDeath));
            }
            else
            {
                Unsubscribe(nameof(OnEntityDeath));
                Unsubscribe(nameof(OnLootEntity));
                Unsubscribe(nameof(OnTurretTarget));
                Unsubscribe(nameof(OnTurretAuthorize));
                Unsubscribe(nameof(CanEntityBeTargeted));
                Unsubscribe(nameof(CanEntityTakeDamage));
                Unsubscribe(nameof(CanEntityBeTargeted));
                Unsubscribe(nameof(OnCustomLootContainer));
                Unsubscribe(nameof(CanPopulateLoot));
                Unsubscribe(nameof(OnEntityDeath));
            }
        }
        #endregion

        #region Oxide 
        void OnServerInitialized()
        {
            _pluginConfiguration = Config.ReadObject<PluginConfiguration>();

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is DieselEngine)
                    _engine = (DieselEngine)entity;
                else if (entity is ExcavatorArm)
                    _arm = (ExcavatorArm)entity;
                else if (entity is ExcavatorOutputPile && _output1 == null)
                    _output1 = (ExcavatorOutputPile)entity;
                else if (entity is ExcavatorOutputPile && _output1.net.ID != entity.net.ID)
                    _output2 = (ExcavatorOutputPile)entity;

                if (_isExcavatorFound)
                    break;
            }

            if (!_isExcavatorFound)
                ServerConsole.print(lang.GetMessage("UnableToFindExcavator", this));

            if (_isExcavatorFound && _pluginConfiguration.AutoStart)
                _runEventCoroutine = ServerMgr.Instance.StartCoroutine(RunHeavyExcavatorEvent());
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!_isEventRunning 
                || _pluginConfiguration.AllowLootingEngine 
                || !player.IsValid()
                || !entity.IsValid())
                return;

            if (_engine.net.ID == entity.net.ID)
            {
                player.ChatMessage(lang.GetMessage("CantLootEngine", this, player.UserIDString));
                NextTick(player.EndLooting);
            }
        }

        void Unload()
        {
            StopExcavatorEvent();

            StopCoroutine(_runEventCoroutine);
            StopCoroutine(_eventTimerCoroutine);
            StopCoroutine(_hooksEnabledCoroutine);
        }

        protected override void LoadDefaultConfig()
        {
            base.Config.WriteObject(PluginConfiguration.GetDefault(), true);
        }

        private bool DoesEntityExist(BaseEntity entity, IEnumerable<BaseEntity> entitesToSearch)
        {
            if (entity == null || !entity.IsValid())
                return false;

            foreach (var spawnedTurret in entitesToSearch)
            {
                if (spawnedTurret.IsValid()
                    && spawnedTurret.net.ID == entity.net.ID)
                    return true;
            }

            return false;
        }
        #endregion

        #region Hooks

        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity)
        {
            if (!_isEventRunning
                || !entity.IsValid()
                || !turret.IsValid()
                || _pluginConfiguration.AutoTurretsCanAttackNPCs
                || !entity.IsNpc)
                return null;

            if (DoesEntityExist(turret, _spawnedTurrets))
                return false;

            return null;
        }

        private object OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            if (!_isEventRunning || turret == null || player == null)
                return null;

            if (DoesEntityExist(turret, _spawnedTurrets))
            {
                player.ChatMessage(lang.GetMessage("CantAuthAutoTurret", this));
                return false;
            }

            return null;
        }

        private object CanEntityBeTargeted(BasePlayer player, BaseEntity entity)
        {
            if (!_isEventRunning || player == null || (entity as AutoTurret) == null)
                return null;

            if (DoesEntityExist(entity, _spawnedTurrets))
                return true;

            return null;
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!_isEventRunning || hitInfo == null || entity == null)
                return null;

            var turret = entity as AutoTurret ?? hitInfo.Initiator as AutoTurret;

            if (turret && DoesEntityExist(turret, _spawnedTurrets))
                return true;

            return null;
        }
        object OnCustomLootContainer(uint netID)
        {
            if (!_isEventRunning)
                return null;

            foreach (var crate in _spawnedHackableCrates)
            {
                if (crate.net.ID.Value == netID)
                    return CanPopulateLoot((crate as LootContainer));
            }

            return null;
        }

        object CanPopulateLoot(LootContainer container)
        {
            if (!_isEventRunning || !container || !container.IsValid())
                return null;

            foreach (var crate in _spawnedHackableCrates)
            {
                if (crate.IsValid()
                    && crate.net.ID == container.net.ID
                    && _pluginConfiguration.LootTableType != LootTableType.AlphaLoot)
                {
                    return false;
                }
            }

            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!entity || !_isEventRunning || _pluginConfiguration.AllowTurretLootingOnDestroy)
                return;


            foreach (var item in _spawnedTurrets)
            {
                if (item.IsValid() && entity.net.ID == item.net.ID)
                    (item as AutoTurret).inventory.Clear();
            }
        }
        #endregion

        #region Configuration 
        private sealed class PluginConfiguration
        {

            [JsonProperty(PropertyName = "_Which loot table should this plugin use? 0 - Default, 1 - Alpha Loot, 2 - Other(Magic Loot, Better Loot)")]
            public LootTableType LootTableType = LootTableType.Default;

            [JsonProperty(PropertyName = "Which type of resource should the excavator mine during the event?  0 - HQM, 1 - Sulfur, 2 - Stone, 3 - Metal Frags, 4 - Random each event")]
            public ResourceType ResourceType = ResourceType.MetalFrags;

            [JsonProperty(PropertyName = "Should the event start automatically(false if you only want manually start with HeavyExcavatorStart command")]
            public bool AutoStart = true;

            [JsonProperty(PropertyName = "Minimum time between events(minutes):")]
            public int Minimum { get; set; } = 60;

            [JsonProperty(PropertyName = "Maximum time between events(minutes):")]
            public int Maximum { get; set; } = 180;

            [JsonProperty(PropertyName = "Minimum number of players before event will start")]
            public int MinimumNumberOfPlayers = 4;

            [JsonProperty(PropertyName = "Broadcast start message to players?")]
            public bool BroadcastStartMessage = true;

            [JsonProperty(PropertyName = "Skip event if players are already at the excavator?")]
            public bool SkipEventIfPlayersAtExcavatorAlready = true;

            [JsonProperty(PropertyName = "Skip event if there is loot sitting in the excavator(checks diesel and output piles)")]
            public bool SkipEventIfLootInExcavatorAlready = false;

            [JsonProperty(PropertyName = "Skip event if the excavator is already running?")]
            public bool SkipEventIfExcavatorsAlreadyRunning = true;

            [JsonProperty(PropertyName = "Allow turrets to be looted if they are destroyed")]
            public bool AllowTurretLootingOnDestroy = false;

            [JsonProperty(PropertyName = "If this is true auto turrets will target and kill NPCs as well as players in the excavator event area.")]
            public bool AutoTurretsCanAttackNPCs = false;

            [JsonProperty(PropertyName = "Should players be allowed to loot the diesel from the engine during the event?")]
            public bool AllowLootingEngine = false;

            [JsonProperty(PropertyName = "How long should the event go on for?(minutes)")]
            public int EventTimeSpan = 50;

            [JsonProperty(PropertyName = "How many diesel should spawn in the engine on event start? (each diesel takes 2 min to burn. keep in mind total event time.)")]
            public int DieselAmount = 20;

            [JsonProperty(PropertyName = "Controls how many NPCs will have the note on death (max 4).")]
            public int NumberOfNPCsWithCode = 1;

            [JsonProperty(PropertyName = "Start locations for scientists")]
            public HashSet<Vector3> ScientistLocations = new HashSet<Vector3>();

            [JsonProperty(PropertyName = "Start locations for Bradleys")]
            public HashSet<Vector3> BradleyLocations = new HashSet<Vector3>();

            [JsonProperty(PropertyName = "Start locations for AutoTurrets")]
            public HashSet<Vector3> AutoTurretLocations = new HashSet<Vector3>();

            [JsonProperty(PropertyName = "Start locations for hackable Crates")]
            public HashSet<Vector3> HackableCrateLocations = new HashSet<Vector3>();

            [JsonProperty(PropertyName = "This is for special items you want added to the hackable crates outside of loot tables")]
            public List<LootItem> SpecialLootItems = new List<LootItem>();

            public class LootItem
            {
                [JsonProperty(PropertyName = "percentage chance for it to appear in a hackable crate (IE 30 = 30% chance)")]
                public int ChanceOfDrop = 30;
                public ItemSpawnData itemData;
            }

            public class ItemSpawnData
            {
                [JsonProperty(PropertyName = "The Item Id:")]
                public int ItemId;
                [JsonProperty(PropertyName = "Random amount minimum:")]
                public int MinimumStackSize;
                [JsonProperty(PropertyName = "Random amount maximum:")]
                public int MaximumStackSize;
                [JsonProperty(PropertyName = "skin id for the item")]
                public ulong SkinId;
            }

            public static PluginConfiguration GetDefault()
            {
                return new PluginConfiguration()
                {
                    ScientistLocations = GetDefaultScientistLocations(),
                    BradleyLocations = GetDefaultBradleyLocations(),
                    AutoTurretLocations = GetDefaultAutoTurretLocations(),
                    HackableCrateLocations = GetDefaultHackableCrateLocations(),
                };
            }
        }

        private static HashSet<Vector3> GetDefaultScientistLocations()
        {
            return new HashSet<Vector3>()
            {
                //loot Pile 1
                 new Vector3() { x = -2.2f, y = -1.6f,z = 91.5f},
                 new Vector3() { x = -7.2f, y = -1.6f,z = 90.5f},
                 new Vector3() { x = 7.1f, y = -1.5f,z = 90.9f},
                 new Vector3() { x = 10.9f, y = -1.4f,z = 83.4f},
                 new Vector3() { x = -22.6f, y = 2.4f,z = 82.2f},
                 new Vector3() { x = -25.5f, y = 2.4f,z = 81.1f},

                 //loot Pile 2
                 new Vector3() { x = -128.2f, y = -1.8f,z = 54.4f},
                 new Vector3() { x = -125.3f, y = -1.8f,z = 43.1f},
                 new Vector3() { x = -116.8f, y = 2.3f,z = 60.4f},
                 new Vector3() { x = -119.4f, y = 2.0f,z = 63.5f},

                 //middle
                 new Vector3() { x = -50.8f, y = 4.6f,z = 100.7f},
                 new Vector3() { x = -52.1f, y = 4.6f,z = 97.4f},

                 //inner core
                 new Vector3() { x = -4.0f, y = 3.9f, z = 5.0f},
                 new Vector3() { x = -5.9f, y = 3.9f, z = 2.7f},
                 new Vector3() { x = -4.7f, y = 3.9f, z = -2.4f},
                 new Vector3() { x = -3.4f, y = 3.9f, z = -4.8f},
                 new Vector3() { x = 2.4f, y = 2.4f, z = -6.0f},
                 new Vector3() { x = 5.7f, y = 2.4f, z = 0.2f},
            };
        }

        private static HashSet<Vector3> GetDefaultAutoTurretLocations()
        {
            return new HashSet<Vector3>()
            {
                //loot Pile 1
                new Vector3() { x = -2.5f, y = 3.1f,z = 99.7f},
                new Vector3() { x = 2.8f, y = 8.3f,z = 91.5f},
                new Vector3() { x = 6.6f, y = 8.3f,z = 82.1f},

                //loot Pile 2 
                new Vector3() { x = -130.5f, y = 7.9f,z = 39.6f},
                new Vector3() { x = -121.1f, y = 7.9f,z = 43.2f},
                new Vector3() { x = -125.7f, y = 2.0f,z = 41.1f},

                //inner core
                new Vector3() { x = 2.6f, y = 8.0f,z = 2.6f},
                new Vector3() { x = .9f, y = 8.0f,z = -2.6f},
            };
        }

        private static HashSet<Vector3> GetDefaultBradleyLocations()
        {
            return new HashSet<Vector3>()
            {
                //loot Pile 1
                 new Vector3() { x = -1.9f, y = -0.3f,z = 72.3f},

                //loot Pile 2
                 new Vector3() { x = -135.6f, y = -1.9f,z = 54.5f},
            };
        }

        private static HashSet<Vector3> GetDefaultHackableCrateLocations()
        {
            return new HashSet<Vector3>()
            {
                new Vector3() { x = -5.8f, y = 3.9f,z = -.1f},
                new Vector3() { x = -5.1f, y = 3.9f,z = -4.5f},
            };
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ExcavatorMarkerName"] = "Heavy Excavator",
                ["HeavyExcavatorStarted"] = "Started Heavy Excavator Event.",
                ["HeavyExcavatorStartedManual"] = "Heavy Excavator Event was manually started.",
                ["ErrorLocatingExcavator"] = "error locating excavator on map. Events will not start.",
                ["MinimumPlayersNotMet"] = "Skipping Heavy Excavator Event - Minimum player count not met.",
                ["HeavyExcavatorStartedBroadcast"] = "<color=#b5440b>A heavy excavator is being formed.</color>",
                ["EventAlreadyInProgress"] = "Heavy Excavator Event is already in progress. " +
                "The current event must end before a new one can start.",
                ["CantLootEngine"] = "The Engine can't be looted while the Heavy Excavator event is running.",
                ["UnableToFindExcavator"] = "Heavy Excavator Event was unable to find an excavator on the map. Event will not run.",
                ["CantAuthAutoTurret"] = "You can't authorize on a heavy excavator event turret.",
                ["NoExcavatorFound"] = "No excavator found on map.",
            }, this);
        }
        #endregion
    }
}
