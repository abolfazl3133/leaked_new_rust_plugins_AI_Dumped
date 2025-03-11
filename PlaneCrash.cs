using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.ChaosNPC;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("PlaneCrash", "k1lly0u", "0.3.12")]
    [Description("Call cargo planes that can be shot down by players to score loot")]
    class PlaneCrash : RustPlugin, IChaosNPCPlugin
    {
        #region Fields
        [PluginReference] Plugin Clans, Friends, LustyMap;
       
        private static PlaneCrash Instance { get; set; }

        private Timer callTimer;
        private bool initialized;

        private readonly SpawnFilter Filter = new SpawnFilter
        {
            BiomeType = (TerrainBiome.Enum)15,
            TopologyAll = (TerrainTopology.Enum)0,
            TopologyAny = ~(TerrainTopology.Enum)0,
            SplatType = (TerrainSplat.Enum)255,
            TopologyNot = (TerrainTopology.Enum)2705030
        };

        // Effects
        private const string C4EXPLOSION_EFFECT = "assets/prefabs/tools/c4/effects/c4_explosion.prefab";
        private const string HELIEXPLOSION_EFFECT = "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab";
        private const string DEBRIS_EFFECT = "assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab";
        private const string FIREBALL_EFFECT = "assets/bundled/prefabs/oilfireballsmall.prefab";
        private const string SMOKE_EFFECT = "assets/bundled/prefabs/fx/smoke_signal_full.prefab";

        // Prefabs
        private const string CARGOPLANE_PREFAB = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        private const string GIBS_PREFAB = "assets/prefabs/npc/patrol helicopter/servergibs_patrolhelicopter.prefab";

        private const string CRATE_PREFAB = "assets/prefabs/npc/patrol helicopter/heli_crate.prefab";
        private const string SUPPLYDROP_PREFAB = "assets/prefabs/misc/supply drop/supply_drop.prefab";
               
        private const string DEBRISMARKER_PREFAB = "assets/prefabs/tools/map/explosionmarker.prefab";
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            LoadData();

            foreach (KeyValuePair<string, int> kvp in configData.Cooldowns)
            {
                if (!kvp.Key.StartsWith("planecrash."))
                    permission.RegisterPermission("planecrash." + kvp.Key, this);
                else permission.RegisterPermission(kvp.Key, this);
            }
            permission.RegisterPermission("planecrash.cancall", this);
            
            if (!configData.Plane.ApplyOnSpawn)
                Unsubscribe(nameof(OnEntitySpawned));

            lang.RegisterMessages(Messages, this);
        }

        private void OnServerInitialized()
        {
            Instance = this;
            initialized = true;

            if (configData.EventTimers.Random)
                StartCrashTimer();
        }

        private void OnServerSave() => SaveData();

        private void OnEntitySpawned(CargoPlane cargoPlane)
        {
            NextFrame(() =>
            {
                if (cargoPlane)
                {
                    SpawnCrashPlane(cargoPlane);

                    Debug.Log("[PlaneCrash] - Replaced game spawned plane");

                    if (configData.Messages.DisplayIncoming)
                        SendChatMessage("IncomingMessage.Supply");
                }
            });
        }

        private object CanLootEntity(BasePlayer player, LootContainer container)
        {
            LootLock lootLock = container.GetComponent<LootLock>();
            if (lootLock && !lootLock.IsUnlocked())
            {
                if (player.userID == lootLock.ownerID)                
                    return null;
                
                if (configData.Loot.LockSettings.LockToPlayerTeam)
                {
                    RelationshipManager.PlayerTeam ownerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(lootLock.ownerID);
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(player.userID);
                    if (ownerTeam != null && playerTeam != null && ownerTeam == playerTeam)                    
                        return null;                    
                }

                if (configData.Loot.LockSettings.LockToPlayerClans && IsClanmate(lootLock.ownerID, player.userID))                                   
                    return null;
                
                if (configData.Loot.LockSettings.LockToPlayerFriends && AreFriends(lootLock.ownerID, player.userID))                
                    return null;
                
                player.ChatMessage(msg("LootLocked", player.UserIDString));
                return false;
            }
            return null;
        }
                
        private void Unload()
        {
            CrashPlane[] planes = UnityEngine.Object.FindObjectsOfType<CrashPlane>();
            for (int i = 0; i < planes?.Length; i++)            
                planes[i].Kill(BaseNetworkable.DestroyMode.None);
            
            configData = null;
            Instance = null;
        }
        #endregion

        #region CustomNPC
        public bool InitializeStates(BaseAIBrain customNPCBrain) => false;

        public bool WantsToPopulateLoot(CustomScientistNPC customNpc, NPCPlayerCorpse npcplayerCorpse) => false;

        public byte[] GetCustomDesign() => null;
        #endregion

        #region Components
        public enum FlightStatus { Flying, Crashing }

        public class CrashPlane : BaseEntity
        {
            public Transform Transform { get; private set; }

            public BoxCollider Collider { get; private set; }

            public FlightStatus status;

            private BasePlayer lastAttacker;

            public Vector3 startPosition;

            public Vector3 endPosition;

            public float secondsToTake;

            public float secondsTaken;

            private FireballHandler engineFireballLeft;
            private FireballHandler engineFireballRight;

            private DestructionTrigger destructionTrigger;

            private int rocketHitsTaken;

            private float speed;
            private float currentSpeed;

            private float crashTimeTaken = 0;
            private readonly float crashTimeToTake = 20;           

            private bool isDying;
            private bool isSmoking;

            public bool shouldDropCrate;
            private bool hasDropped;

            private bool willAutoCrash;
            private float autoCrashAt;
            private float randomCrashDirection = -1;
            private float crashDelay = 0;

            private Vector3 crashRotation;

            public bool isPaused = false;

            private void Awake()
            {
                Transform = transform;

                gameObject.layer = 0;
                gameObject.name = "CrashPlane";

                CreateColliders();

                speed = currentSpeed = configData.Plane.Speed;

                status = FlightStatus.Flying;

                willAutoCrash = Random.Range(0, 100) < configData.Plane.AutoCrashChance;
                autoCrashAt = Random.Range(configData.Plane.CrashMinimumRange, configData.Plane.CrashMaximumRange);

                if (configData.Plane.RandomizeCrashDirection)
                    randomCrashDirection = Random.value;
            }

            private void CreateColliders()
            {
                Collider = gameObject.AddComponent<BoxCollider>();
                Collider.size = new Vector3(25, 15, 55);
                Collider.center = Vector3.up * 10f;

                destructionTrigger = gameObject.CreateChild().AddComponent<DestructionTrigger>();
                destructionTrigger.Setup(this);
            }

            public override void OnAttacked(HitInfo info)
            {               
                base.OnAttacked(info);

                if (info == null)
                    return;

                TimedExplosive timedExplosive = info.WeaponPrefab as TimedExplosive;
                if (timedExplosive)
                {
                    SmallExplosion();
                                        
                    rocketHitsTaken++;

                    BasePlayer attacker = info.InitiatorPlayer;

                    if (configData.Loot.CrateHit > 0)
                        ServerMgr.Instance.StartCoroutine(SpawnLoot(configData.Loot.CrateHit, false));

                    if (configData.Loot.SupplyHit > 0)
                        ServerMgr.Instance.StartCoroutine(SpawnLoot(configData.Loot.SupplyHit, true));

                    if (rocketHitsTaken == 1 && engineFireballLeft == null)
                        engineFireballLeft = new FireballHandler(new Vector3(-14.5f, 7.5f, 9f), Transform);

                    if (rocketHitsTaken >= configData.Plane.DestroyHits)
                    {
                        if (attacker)
                            lastAttacker = attacker;

                        if (configData.Messages.DisplayAttacker && lastAttacker)
                            SendChatMessage("AttackerMessage2", lastAttacker.displayName);

                        Die();
                        return;
                    }

                    if (rocketHitsTaken >= configData.Plane.DownHits && status == FlightStatus.Flying)
                    {
                        if (attacker)
                            lastAttacker = attacker;

                        if (lastAttacker && configData.Messages.DisplayAttacker)
                            SendChatMessage("AttackerMessage1", lastAttacker.displayName);

                        BeginCrash();
                        return;
                    }
                }
            }

            public override void PostInitShared()
            {
                base.PostInitShared();

                if (configData.Plane.SmokeTrail)
                    Invoke(()=> RunEffect(SMOKE_EFFECT, this, Vector3.zero, Vector3.up * 3), 5f);
            }

            public override void DestroyShared()
            {
                Destroy(destructionTrigger);
                base.DestroyShared();
            }
                       

            private void Update()
            {
                if (isPaused)
                    return;

                if (Transform.position.y <= 0)
                {
                    if (!isDying)
                        Die();
                    return;
                }

                if (status == FlightStatus.Crashing)
                {
                    crashTimeTaken += Time.deltaTime;

                    float delta = Mathf.InverseLerp(0, crashTimeToTake, crashTimeTaken);
                    if (delta < 1)
                    {
                        currentSpeed = speed + Mathf.Lerp(0, 10, delta);

                        Vector3 direction = crashRotation;

                        direction.x = Mathf.Lerp(0, 25f, delta);

                        if (randomCrashDirection != -1)
                        {
                            float yaw = Mathf.Lerp(0f, 25f, delta * 1.75f);
                            float roll = Mathf.Lerp(0f, 35f, delta * 2.25f);

                            if (randomCrashDirection <= 0.5f)
                            {
                                direction.y -= yaw;
                                direction.z += roll;
                            }
                            else
                            {
                                direction.y += yaw;
                                direction.z -= roll;
                            }
                        }
                        Transform.eulerAngles = direction;
                    }
                   
                    Transform.position = Vector3.MoveTowards(Transform.position, Transform.position + (Transform.forward * 10), currentSpeed * Time.deltaTime);                    
                }
                else
                {
                    Transform.position = Vector3.MoveTowards(Transform.position, endPosition, currentSpeed * Time.deltaTime);

                    float delta = InverseLerp(startPosition, endPosition, Transform.position);

                    secondsTaken = secondsToTake * delta;

                    if (shouldDropCrate && !hasDropped && delta >= 0.5f)
                    {
                        hasDropped = true;

                        SupplyDrop supplyDrop = GameManager.server.CreateEntity(SUPPLYDROP_PREFAB, Transform.position + (Transform.forward * -10f) + (Vector3.down * 3f)) as SupplyDrop;
                        supplyDrop.globalBroadcast = true;   
                        supplyDrop.Spawn();

                        supplyDrop.Invoke(() => VerifySupplyDropParachute(supplyDrop), 2f);

                        Interface.CallHook("OnSupplyDropDropped", supplyDrop, this);
                    }

                    if (willAutoCrash)
                    {
                        if (delta >= autoCrashAt && crashDelay < Time.time)
                        {
                            if (TerrainMeta.HeightMap.GetHeight(Transform.position) < 0)
                            {
                                crashDelay = Time.time + 3f;
                                return;
                            }

                            SendChatMessage("AutoCrashMessage");
                            BeginCrash();
                        }
                    }
                }

                engineFireballLeft?.Update();
                engineFireballRight?.Update();             

                Transform.hasChanged = true;
            }

            private void VerifySupplyDropParachute(SupplyDrop supplyDrop)
            {
                supplyDrop.SetFlag(Flags.Reserved2, true);
                /*if (!supplyDrop.parachute || supplyDrop.parachute.IsDestroyed)
                {
                    supplyDrop.parachute = GameManager.server.CreateEntity(supplyDrop.parachutePrefab.resourcePath);
                    supplyDrop.parachute.SetParent(supplyDrop, "parachute_attach", false, false);
                    supplyDrop.parachute.Spawn();

                    supplyDrop.isLootable = false;

                    supplyDrop.Invoke(()=> VerifySupplyDropParachute(supplyDrop), 1f);
                }*/
            }

            private void OnDestroy()
            {
                enabled = false;
                CancelInvoke();

                if (engineFireballLeft != null)
                    engineFireballLeft.Destroy();

                if (engineFireballRight != null)
                    engineFireballRight.Destroy();

                if (!IsDestroyed)
                    Kill();
            }

            #region Initialization
            public void SetFlightPath(Vector3 dropPosition)
            {
                if (configData.Plane.RandomDistance != 0)
                {
                    Vector2 random = Random.insideUnitCircle * configData.Plane.RandomDistance;
                    dropPosition.x += random.x;
                    dropPosition.z += random.y;
                }

                dropPosition = dropPosition.XZ3D();

                startPosition = Vector3Ex.Range(-1f, 1f);
                startPosition.y = 0f;
                startPosition.Normalize();
                startPosition *= (TerrainMeta.Size.x * 2f);
                startPosition.y = 150f + configData.Plane.Height;
                endPosition = startPosition * -1f;
                endPosition.y = startPosition.y;
                startPosition += dropPosition;
                endPosition += dropPosition;

                secondsToTake = Vector3.Distance(startPosition, endPosition) / speed;

                transform.position = startPosition;
                transform.rotation = Quaternion.LookRotation(endPosition - startPosition);

                if (configData.Plane.RandomizeCrashDirection)
                {
                    Vector3 perp = Vector3.Cross(Transform.forward, Vector3.zero - Transform.position);
                    randomCrashDirection = Vector3.Dot(perp, Vector3.up) >= 0 ? 1 : 0f;
                }

                Destroy(this, secondsToTake);
            }
            
            public void SetFlightPath(CargoPlane cargoPlane)
            {
                startPosition = cargoPlane.startPos;
                startPosition.y = 150f + configData.Plane.Height;
                
                endPosition = cargoPlane.endPos;
                endPosition.y = startPosition.y;

                secondsToTake = Vector3.Distance(startPosition, endPosition) / speed;
                secondsToTake = cargoPlane.secondsToTake;

                willAutoCrash = false;

                Vector3 position = cargoPlane.transform.position;
                transform.position = new Vector3(position.x, startPosition.y, position.z);
                transform.rotation =  cargoPlane.transform.rotation;

                if (configData.Plane.RandomizeCrashDirection)
                {
                    Vector3 perp = Vector3.Cross(Transform.forward, Vector3.zero - Transform.position);
                    randomCrashDirection = Vector3.Dot(perp, Vector3.up) >= 0 ? 1 : 0f;
                }

                Destroy(this, secondsToTake);
            }
            
            public void BeginCrash()
            {
                endPosition = new Vector3(endPosition.x, 0, endPosition.z);
                status = FlightStatus.Crashing;

                crashRotation = Transform.eulerAngles;

                SmallExplosion();

                if (engineFireballLeft == null)
                    engineFireballLeft = new FireballHandler(new Vector3(-14.5f, 7.5f, 9f), Transform);

                if (engineFireballRight == null)
                    engineFireballRight = new FireballHandler(new Vector3(14.5f, 7.5f, 9f), Transform);
            }
            #endregion

            #region Effects
            private void BigExplosion()
            {
                RunEffect(HELIEXPLOSION_EFFECT, null, Transform.position);
                RunEffect(DEBRIS_EFFECT, null, Transform.position);
            }

            private void SmallExplosion()
            {
                RunEffect(C4EXPLOSION_EFFECT, null, Transform.position);
                RunEffect(DEBRIS_EFFECT, null, Transform.position);
            }            
            #endregion

            private float InverseLerp(Vector3 a, Vector3 b, Vector3 value)
            {
                Vector3 ab = b - a;
                Vector3 av = value - a;
                return Vector3.Dot(av, ab) / Vector3.Dot(ab, ab);
            }

            private void Die()
            {
                if (isDying)
                    return;

                isDying = true;
                BigExplosion();
                FinalLootNPCSpawn();
                
                if (configData.LustyOptions.CrashIcon)
                    InvokeHandler.Invoke(this, UpdateCrashMarker, 1.5f);

                if (configData.MapOptions.CrashIcon)
                    InvokeHandler.Invoke(this, UpdateMapMarker, 1.5f);

                InvokeHandler.Invoke(this, SmallExplosion, 0.25f);
                InvokeHandler.Invoke(this, SmallExplosion, 0.5f);
                InvokeHandler.Invoke(this, BigExplosion, 1.25f);
                InvokeHandler.Invoke(this, SmallExplosion, 1.75f);
                InvokeHandler.Invoke(this, BigExplosion, 2.25f);

                Destroy(this, 2.5f);
            }

            #region Loot
            private void FinalLootNPCSpawn()
            {
                Vector3 velocity = Transform.forward * currentSpeed;

                if (configData.Loot.FireLife > 0)
                {
                    List<ServerGib> serverGibs = ServerGib.CreateGibs(GIBS_PREFAB, gameObject, gameObject, velocity, 5f);
                    for (int i = 0; i < 12; i++)
                    {
                        FireBall fireBall = GameManager.server.CreateEntity(FIREBALL_EFFECT, Transform.position, Transform.rotation, true) as FireBall;
                        if (fireBall)
                        {
                            Vector3 randsphere = Random.onUnitSphere;
                            fireBall.transform.position = (Transform.position + new Vector3(0f, 1.5f, 0f)) + (randsphere * Random.Range(-4f, 4f));

                            Collider collider = fireBall.GetComponent<Collider>();
                            fireBall.lifeTimeMin = fireBall.lifeTimeMax = configData.Loot.FireLife;
                            fireBall.Spawn();

                            fireBall.SetVelocity(velocity + (randsphere * Random.Range(3f, 10f)));

                            foreach (ServerGib serverGib in serverGibs)
                                Physics.IgnoreCollision(collider, serverGib.GetCollider(), true);
                        }
                    }
                }

                List<BaseEntity> list = Facepunch.Pool.Get<List<BaseEntity>>();

                if (configData.Loot.CrateCrash > 0)
                    ServerMgr.Instance.StartCoroutine(SpawnLoot(configData.Loot.CrateCrash, false, true, list));

                if (configData.Loot.SupplyCrash > 0)
                    ServerMgr.Instance.StartCoroutine(SpawnLoot(configData.Loot.SupplyCrash, true, true, list));

                if (configData.NPCOptions.Enabled)
                    Instance.timer.In(5f, () => SpawnNPCs(list, lastAttacker));
                else Facepunch.Pool.FreeUnmanaged(ref list);

                if (configData.Messages.DisplayDestroy)
                {
                    if (configData.Messages.UseGrid)
                        SendChatMessage("DestroyMessage.Grid", MapHelper.PositionToString(Transform.position));
                    else SendChatMessage("DestroyMessage", Transform.position.x, Transform.position.z);
                }
            }

            private IEnumerator SpawnLoot(int amount, bool isDrop, bool isCrashing = false, List<BaseEntity> lootList = null)
            {
                Vector3 lastMoveDir = Transform.forward * currentSpeed;

                for (int j = 0; j < amount; j++)
                {
                    Vector3 position = (Transform.position + new Vector3(0f, 1.5f, 0f)) + (Random.onUnitSphere * Random.Range(-2f, 3f));

                    position.y = Mathf.Max(TerrainMeta.HeightMap.GetHeight(position), position.y) + 3f;
                    
                    string ent = isDrop ? SUPPLYDROP_PREFAB : CRATE_PREFAB;

                    LootContainer container = GameManager.server.CreateEntity(ent, position, Quaternion.LookRotation(Random.onUnitSphere), true) as LootContainer;
                    container.enableSaving = false;
                    container.Spawn();

                    if (lootList != null)
                        lootList.Add(container);

                    if (configData.Loot.LockSettings.LockToPlayer && lastAttacker)                    
                        LootLock.Initialize(container, lastAttacker.userID);
                    
                    if (j == 0 && configData.Plane.Smoke && isCrashing && !isSmoking)
                    {
                        RunEffect(SMOKE_EFFECT, container);
                        isSmoking = true;
                    }

                    Rigidbody rigidbody;
                    if (!isDrop)
                    {
                        rigidbody = container.gameObject.GetComponent<Rigidbody>();
                        if (!rigidbody)
                            rigidbody = container.gameObject.AddComponent<Rigidbody>();
                    }
                    else
                    {
                        container.GetComponent<SupplyDrop>().RemoveParachute();
                        rigidbody = container.GetComponent<Rigidbody>();
                    }

                    if (rigidbody)
                    {
                        rigidbody.isKinematic = false;
                        rigidbody.useGravity = true;
                        rigidbody.mass = 2f;
                        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                        rigidbody.velocity = Vector3.ClampMagnitude(lastMoveDir, 35) + (Random.onUnitSphere * Random.Range(1f, configData.Loot.LootSpread));
                        rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                        rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                        rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);                        
                    }

                    if (configData.Loot.FireLife > 0f)
                        FireballHandler.Create(container);
                    
                    InvokeHandler.Invoke(container, () => FillLootContainer(container, isDrop), 2f);

                    if (configData.Loot.LootDespawnTime > 0)
                    {
                        container.Invoke(() =>
                        {
                            if (!container || container.HasFlag(BaseEntity.Flags.Open))
                                return;

                            container.Kill();
                        }, configData.Loot.LootDespawnTime);
                    }

                    yield return CoroutineEx.waitForEndOfFrame;
                }
            }
            #endregion            

            #region Map Markers
            private void UpdateCrashMarker()
            {
                if (!Instance.LustyMap)
                    return;

                Instance.LustyMap.Call("AddTemporaryMarker", Transform.position.x, Transform.position.z, "Crashed Plane", configData.LustyOptions.IconURL, 0);
                Instance.timer.In(configData.LustyOptions.CrashIconTime, () => Instance.LustyMap.Call("RemoveTemporaryMarker", "Crashed Plane"));
            }

            private void UpdateMapMarker()
            {                
                BaseEntity baseEntity = GameManager.server.CreateEntity(DEBRISMARKER_PREFAB, Transform.position, Quaternion.identity, true);
                baseEntity.Spawn();
                baseEntity.SendMessage("SetDuration", configData.MapOptions.CrashIconTime, SendMessageOptions.DontRequireReceiver);
            }
            #endregion

            private class DestructionTrigger : MonoBehaviour
            {
                private CrashPlane crashPlane;

                private BoxCollider collider;

                public void Setup(CrashPlane crashPlane)
                {
                    this.crashPlane = crashPlane;

                    gameObject.layer = (int)Layer.Reserved2;

                    Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
                    rigidbody.useGravity = false;
                    rigidbody.isKinematic = true;
                    rigidbody.detectCollisions = true;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                    collider = gameObject.AddComponent<BoxCollider>();
                    collider.isTrigger = true;
                    collider.size = new Vector3(60, 15, 55);
                    collider.center = Vector3.up * 10f;

                    Physics.IgnoreCollision(crashPlane.Collider, collider, true);
                }

                private void OnTriggerEnter(Collider col)
                {
                    if (col is TerrainCollider)
                    {
                        crashPlane.Die();
                        return;
                    }

                    BaseEntity baseEntity = col.gameObject.ToBaseEntity();

                    if (baseEntity is BasePlayer)
                    {
                        if ((baseEntity as BasePlayer).IsNpc)
                            return;

                        (baseEntity as BasePlayer).Die();
                        return;
                    }

                    if (configData.Plane.Destruction)
                    {
                        if (baseEntity is TreeEntity or ResourceEntity or BuildingBlock or SimpleBuildingBlock or PatrolHelicopter)
                        {
                            BaseCombatEntity baseCombatEntity = baseEntity as BaseCombatEntity;
                            if (baseCombatEntity)
                            {
                                baseCombatEntity.Die();
                                return;
                            }

                            if (baseEntity)
                            {
                                baseEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                                return;
                            }
                        }
                    }
                }
            }

            private class FireballHandler
            {
                private readonly Vector3 offset;
                private readonly Transform parent;
                private FireBall fireBall;

                public FireballHandler(Vector3 offset, Transform parent)
                {
                    this.offset = offset;
                    this.parent = parent;

                    fireBall = Create(parent.TransformPoint(offset), 600f);
                }

                public void Update()
                {
                    if (fireBall)
                        fireBall.transform.position = parent.TransformPoint(offset);
                }

                public void Destroy()
                {
                    if (fireBall && !fireBall.IsDestroyed)
                    {
                        fireBall.Extinguish();
                        fireBall = null;
                    }
                }

                public static FireBall Create(Vector3 position, float lifetime)
                {
                    FireBall fireBall = GameManager.server.CreateEntity(FIREBALL_EFFECT, position) as FireBall;
                    fireBall.enableSaving = false;
                    fireBall.Spawn();

                    Rigidbody rb = fireBall.GetComponent<Rigidbody>();
                    rb.isKinematic = true;

                    fireBall.CancelInvoke(fireBall.Extinguish);
                    fireBall.CancelInvoke(fireBall.TryToSpread);

                    fireBall.Invoke(fireBall.Extinguish, lifetime);
                    return fireBall;
                }

                public static FireBall Create(BaseEntity parent)
                {
                    FireBall fireBall = GameManager.server.CreateEntity(FIREBALL_EFFECT) as FireBall;
                    fireBall.SetParent(parent, false, false);
                    fireBall.transform.localPosition = Vector3.zero;

                    fireBall.lifeTimeMin = fireBall.lifeTimeMax = configData.Loot.FireLife;                   
                    fireBall.Spawn();

                    fireBall.GetComponent<Rigidbody>().isKinematic = true;
                    fireBall.GetComponent<Collider>().enabled = false;

                    parent.SetFlag(Flags.OnFire, true, false, true);
                    parent.SetFlag(Flags.Locked, true, false, true);

                    parent.Invoke(() =>
                    {
                        if (fireBall && !fireBall.IsDestroyed)
                            fireBall.Kill(DestroyMode.None);

                        if (parent && !parent.IsDestroyed)
                        {
                            parent.SetFlag(Flags.OnFire, false, false, true);
                            parent.SetFlag(Flags.Locked, false, false, true);
                        }
                    }, configData.Loot.FireLife);
                    
                    return fireBall;
                }
            }
        }

        public class LootLock : MonoBehaviour
        {
            internal LootContainer Container { get; private set; }

            internal ulong ownerID;

            private float lockExpiretime;

            private void Awake()
            {
                Container = GetComponent<LootContainer>();
                lockExpiretime = Time.time + configData.Loot.LockSettings.LockTime;
                Destroy(this, configData.Loot.LockSettings.LockTime);
            }

            internal bool IsUnlocked() => Time.time >= lockExpiretime;

            internal static void Initialize(LootContainer container, ulong ownerId)
            {
                LootLock lootLock = container.gameObject.AddComponent<LootLock>();
                lootLock.ownerID = ownerId;                
            }
        }

        #region NPCs
        private static void SpawnNPCs(List<BaseEntity> lootContainers, BasePlayer lastAttacker)
        {            
            Vector3 position = Vector3.zero;

            int count = 0;
            lootContainers.ForEach((BaseEntity baseEntity) =>
            {
                if (baseEntity)
                {
                    position += baseEntity.transform.position;
                    count++;
                }
            });

            position /= count;

            Facepunch.Pool.FreeUnmanaged(ref lootContainers);

            ServerMgr.Instance.StartCoroutine(CreateNPCs(position, lastAttacker));
        }

        private static IEnumerator CreateNPCs(Vector3 position, BasePlayer lastAttacker)
        {            
            int amount = configData.NPCOptions.Amount + (configData.NPCOptions.CorpseEnabled ? configData.NPCOptions.CorpseAmount : 0);

            for (int i = 0; i < amount; i++)
            {
                Vector3 newPosition = position + (Random.onUnitSphere * 20);

                bool spawnDead = i >= configData.NPCOptions.Amount;               

                NPCSettings settings = configData.NPCOptions.CustomNPCSettings;
                settings.StartDead = spawnDead;

                ChaosNPC.SpawnNPC(Instance, newPosition, settings);

                yield return CoroutineEx.waitForSeconds(0.5f);
            }
        }                
        #endregion
        #endregion

        #region Functions
        private Vector3 GetRandomDropPosition()
        {
            Vector3 dropPosition;
            int attempts = 100;
            float mapSize = TerrainMeta.Size.x;
            do
            {
                dropPosition = Vector3Ex.Range(-(mapSize / 6f), mapSize / 6f);
            }
            while (Filter.GetFactor(dropPosition, true) == 0f && (attempts -= 1) > 0f);
            
            dropPosition.y = 0f;
            return dropPosition;
        }

        private void StartCrashTimer()
        {
            callTimer = timer.In(Random.Range(configData.EventTimers.Min, configData.EventTimers.Max) * 60, () =>
            {
                Vector3 position = GetRandomDropPosition();
                SpawnCrashPlane(position, configData.Loot.DropOnAutoSpawn);

                Debug.Log($"[PlaneCrash] - Auto-spawned plane with a drop target at {position}");

                if (configData.Messages.DisplayIncoming)
                    SendChatMessage("IncomingMessage");

                StartCrashTimer();
            });
        }

        private CrashPlane SpawnCrashPlane(Vector3 dropPosition, bool dropCrate)
        {
            CargoPlane cargoPlane = (CargoPlane)GameManager.server.CreateEntity(CARGOPLANE_PREFAB, Vector3.zero, Quaternion.identity, true);

            CrashPlane crashPlane = cargoPlane.gameObject.AddComponent<CrashPlane>();

            crashPlane.prefabID = cargoPlane.prefabID;
            crashPlane.bounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(60f, 15f, 55f));
            crashPlane.syncPosition = true;
            crashPlane.globalBroadcast = true;
            crashPlane.shouldDropCrate = dropCrate;

            crashPlane.SetFlightPath(dropPosition);

            UnityEngine.Object.DestroyImmediate(cargoPlane);

            crashPlane.Spawn();

            return crashPlane;
        }
        
        private CrashPlane SpawnCrashPlane(CargoPlane replaceCargoPlane)
        {
            CargoPlane cargoPlane = (CargoPlane)GameManager.server.CreateEntity(CARGOPLANE_PREFAB, replaceCargoPlane.transform.position, replaceCargoPlane.transform.rotation, true);

            CrashPlane crashPlane = cargoPlane.gameObject.AddComponent<CrashPlane>();

            crashPlane.prefabID = cargoPlane.prefabID;
            crashPlane.bounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(60f, 15f, 55f));
            crashPlane.syncPosition = true;
            crashPlane.globalBroadcast = true;
            crashPlane.shouldDropCrate = true;

            crashPlane.SetFlightPath(replaceCargoPlane);

            UnityEngine.Object.DestroyImmediate(cargoPlane);
            replaceCargoPlane.Kill();

            crashPlane.Spawn();

            return crashPlane;
        }

        private static void RunEffect(string name, BaseEntity entity = null, Vector3 position = new Vector3(), Vector3 offset = new Vector3())
        {
            if (entity)
                Effect.server.Run(name, entity, 0, offset, position, null, true);
            else Effect.server.Run(name, position, Vector3.up, null, true);
        }

        private static void FillLootContainer(BaseEntity entity, bool isDrop)
        {
            if (!entity)
                return;
            
            ItemContainer container = isDrop && entity is SupplyDrop ? (entity as SupplyDrop).inventory : entity is LootContainer ? (entity as LootContainer).inventory : null;

            if (configData.Loot.DespawnSafezone && IsInSafeZone(entity.transform.position))
            {
                container.Clear();
                entity.Kill();
                return;
            }

            ConfigData.LootSettings.LootTables lootTable = isDrop ? configData.Loot.SupplyLoot : configData.Loot.CrateLoot;
           
            if (container == null || lootTable == null)
                return;

            if (lootTable.Enabled)
            {
                while (container.itemList.Count > 0)
                {
                    Item item = container.itemList[0];
                    item.RemoveFromContainer();
                    item.Remove(0f);
                }

                List<ConfigData.LootSettings.LootTables.LootItem> items = new List<ConfigData.LootSettings.LootTables.LootItem>(lootTable.Items);

                int count = Random.Range(lootTable.Minimum, lootTable.Maximum);
                for (int i = 0; i < count; i++)
                {
                    ConfigData.LootSettings.LootTables.LootItem lootItem = items.GetRandom();
                    if (lootItem == null)
                        continue;

                    items.Remove(lootItem);
                    if (items.Count == 0)
                        items.AddRange(lootTable.Items);

                    Item item = ItemManager.CreateByName(lootItem.Shortname, Random.Range(lootItem.Min, lootItem.Max));
                    if (item != null)
                        item.MoveToContainer(container);
                }
            }

            if (configData.Loot.LockSettings.LockCrates)
            {
                entity.SetFlag(BaseEntity.Flags.Locked, true, false);
                InvokeHandler.Invoke(entity, () => { entity.SetFlag(BaseEntity.Flags.Locked, false, false); }, configData.Loot.LockSettings.LockTimer);                
            }
        }

        private static bool IsInSafeZone(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, 1f, Vis.colBuffer, 1 << 18, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider collider = Vis.colBuffer[i];
                if (collider.GetComponent<TriggerSafeZone>())
                    return true;
            }

            return false;
        }

        private static void StripInventory(BasePlayer player, bool skipWear = false)
        {
            List<Item> allItems = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(allItems);

            for (int i = allItems.Count - 1; i >= 0; i--)
            {
                Item item = allItems[i];
                if (skipWear && item?.parent == player.inventory.containerWear)
                    continue;

                item.RemoveFromContainer();
                item.Remove();
            }
            
            Pool.FreeUnmanaged(ref allItems);
        }        
               
        private bool IsCrashPlane(BaseEntity baseEntity) => baseEntity is CrashPlane;        

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

        private static void SendChatMessage(string key, params object[] args)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];           
                player.ChatMessage(args != null ? string.Format(msg(key, player.UserIDString), args) : msg(key, player.UserIDString));
            }
        }
        #endregion

        #region Friends
        private bool AreFriends(ulong playerId, ulong friendId)
        {
            if (Friends)
                return (bool)Friends?.Call("AreFriends", playerId.ToString(), friendId.ToString());
            return false;
        }
        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (Clans)
            {
                object playerTag = Clans?.Call("GetClanOf", playerId);
                object friendTag = Clans?.Call("GetClanOf", friendId);
                if (playerTag is string && friendTag is string)
                {
                    if (!string.IsNullOrEmpty((string)playerTag) && !string.IsNullOrEmpty((string)friendTag) && (playerTag == friendTag))
                        return true;
                }
            }
            return false;
        }
        #endregion

        #region Commands
        [ConsoleCommand("callcrash")]
        void ccmdSendCrash(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            Vector3 location = Vector3.zero;
            if (arg.Args != null)
            {
                if (arg.Args.Length == 1 && arg.Args[0].ToLower() == "help")
                {
                    SendReply(arg, $"{Title}  v{Version} - k1lly0u @ chaoscode.io");
                    SendReply(arg, "callcrash - Send a random crash plane");
                    SendReply(arg, "callcrash \"X\" \"Z\" - Send a crash plane towards the specified X and Z co-ordinates");
                    SendReply(arg, "callcrash \"playername\" - Send a crash plane towards the specified player's position");
                    SendReply(arg, "callcrash crashall - Force crash any active planes");
                    return;
                }

                if (arg.Args.Length > 0)
                {
                    if (arg.Args.Length == 2)
                    {
                        if (float.TryParse(arg.Args[0], out float x) && float.TryParse(arg.Args[1], out float z))
                        {
                            location = new Vector3(x, 0, z);
                            SendReply(arg, $"Crash plane sent to X: {x}, Z: {z}");
                        }
                    }
                    if (arg.Args.Length == 1)
                    {
                        if (arg.Args[0].ToLower() == "crashall")
                        {
                            CrashPlane[] crashPlanes = UnityEngine.Object.FindObjectsOfType<CrashPlane>();
                            if (crashPlanes == null || crashPlanes.Length == 0)
                            {
                                SendReply(arg, "There are no planes currently active");
                                return;
                            }

                            for (int i = 0; i < crashPlanes.Length; i++)
                            {
                                CrashPlane crashPlane = crashPlanes[i];
                                if (crashPlane.status == FlightStatus.Flying)
                                    crashPlane.BeginCrash();
                            }
                            
                            SendReply(arg, $"Force crashing {crashPlanes.Length} planes!");
                            return;
                        }
                        else
                        {
                            IPlayer targetPlayer = covalence.Players.FindPlayer(arg.Args[0]);
                            if (targetPlayer != null && targetPlayer.IsConnected)
                            {
                                BasePlayer target = targetPlayer?.Object as BasePlayer;
                                if (target)
                                {
                                    location = target.transform.position;
                                    SendReply(arg, $"Crash plane sent towards {target.displayName}'s current position");
                                }
                            }
                            else
                            {
                                SendReply(arg, "Could not locate the specified player");
                                return;
                            }
                        }
                    }
                    else
                    {
                        location = GetRandomDropPosition();
                        SendReply(arg, "Crash plane sent to random location");
                    }
                }
            }
            else
            {
                location = GetRandomDropPosition();
                SendReply(arg, "Crash plane sent to random location");
            }

            SpawnCrashPlane(location, false);
        }
               
        [ChatCommand("callcrash")]
        void cmdSendCrash(BasePlayer player, string command, string[] args)
        {
            int cooldown = -1;

            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "planecrash.cancall"))
                cooldown = 0;
            else
            {
                foreach (KeyValuePair<string, int> kvp in configData.Cooldowns.OrderBy(x => x.Value))
                {
                    if (permission.UserHasPermission(player.UserIDString, kvp.Key))
                    {
                        cooldown = kvp.Value;
                        break;
                    }
                }
            }

            if (cooldown < 0)
                return;

            if (args.Length == 1 && args[0].ToLower() == "help")
            {
                SendReply(player, $"<color=#ce422b>{Title}</color>  <color=#939393>v</color><color=#ce422b>{Version}</color> <color=#939393>-</color> <color=#ce422b>k1lly0u</color><color=#939393> @</color> <color=#ce422b>chaoscode.io</color>");

                if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "planecrash.cancall"))
                {
                    SendReply(player, "<color=#ce422b>/callcrash</color><color=#939393> - Send a random crash plane</color>");
                    SendReply(player, "<color=#ce422b>/callcrash \"X\" \"Z\" </color><color=#939393>- Send a crash plane towards the specified X and Z co-ordinates</color>");
                    SendReply(player, "<color=#ce422b>/callcrash \"playername\" </color><color=#939393>- Send a crash plane towards the specified player's position</color>");
                    SendReply(player, "<color=#ce422b>/callcrash crashall</color><color=#939393> - Force all active planes to crash</color>");
                }
                else SendReply(player, "<color=#ce422b>/callcrash</color><color=#939393> - Send a crash plane towards your position</color>");

                return;
            }

            if (cooldown != 0 && storedData.IsOnCooldown(player))
            {
                SendReply(player, string.Format(msg("OnCooldown", player.UserIDString), FormatTime(storedData.GetTimeRemaining(player))));
                return;
            }

            Vector3 location = Vector3.zero;
            if (args.Length > 0 && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "planecrash.cancall")))
            {
                if (args.Length == 2)
                {
                    if (float.TryParse(args[0], out float x) && float.TryParse(args[1], out float z))
                    {
                        location = new Vector3(x, 0, z);
                        SendReply(player, $"<color=#939393>Crash plane sent to</color> <color=#ce422b>X: {x}, Z: {z}</color>");
                    }
                }

                if (args.Length == 1)
                {
                    if (args[0].ToLower() == "crashall")
                    {
                        CrashPlane[] crashPlanes = UnityEngine.Object.FindObjectsOfType<CrashPlane>();
                        if (crashPlanes == null || crashPlanes.Length == 0)
                        {
                            SendReply(player, "There are no planes currently active");
                            return;
                        }

                        for (int i = 0; i < crashPlanes.Length; i++)
                        {
                            CrashPlane crashPlane = crashPlanes[i];
                            if (crashPlane.status == FlightStatus.Flying)
                                crashPlane.BeginCrash();
                        }

                        SendReply(player, $"Force crashing {crashPlanes.Length} planes!");

                        return;
                    }
                    else
                    {
                        IPlayer targetPlayer = covalence.Players.FindPlayer(args[0]);
                        if (targetPlayer != null && targetPlayer.IsConnected)
                        {
                            BasePlayer target = targetPlayer?.Object as BasePlayer;
                            if (target)
                            {
                                location = target.transform.position;
                                SendReply(player, $"<color=#939393>Crash plane sent towards </color><color=#ce422b>{target.displayName}'s</color><color=#939393> current position</color>");
                            }
                        }
                        else
                        {
                            SendReply(player, "<color=#ce422b>Could not locate the specified player</color>");
                            return;
                        }
                    }
                }
                else
                {
                    location = GetRandomDropPosition();
                    SendReply(player, "<color=#ce422b>Crash plane sent to random location</color>");
                }
            }
            else
            {
                location = player.transform.position;
                SendReply(player, "<color=#ce422b>Crash plane sent towards your location</color>");
            }

            SpawnCrashPlane(location, false);

            if (cooldown > 0)
                storedData.AddCooldown(player, cooldown);
        }
        #endregion

        #region Config        
        private static ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Plane Settings")]
            public PlaneSettings Plane { get; set; }

            [JsonProperty(PropertyName = "Loot Settings")]
            public LootSettings Loot { get; set; }

            [JsonProperty(PropertyName = "Message Options")]
            public Messaging Messages { get; set; }

            [JsonProperty(PropertyName = "Timer Settings")]
            public Timers EventTimers { get; set; }

            [JsonProperty(PropertyName = "LustyMap Integration")]
            public Lusty LustyOptions { get; set; }

            [JsonProperty(PropertyName = "Ingame Map Integration")]
            public Map MapOptions { get; set; }

            [JsonProperty(PropertyName = "NPC Options")]
            public Bots NPCOptions { get; set; }

            [JsonProperty(PropertyName = "Command Cooldowns (permission / time in minutes)")]
            public Dictionary<string, int> Cooldowns { get; set; }
           
            public class PlaneSettings
            {                
                [JsonProperty(PropertyName = "Flight speed")]
                public float Speed { get; set; }

                [JsonProperty(PropertyName = "Show smoke on crash site")]
                public bool Smoke { get; set; }

                [JsonProperty(PropertyName = "Height modifier to default flight height")]
                public float Height { get; set; }

                [JsonProperty(PropertyName = "Amount of rocket hits to destroy mid-flight")]
                public int DestroyHits { get; set; }

                [JsonProperty(PropertyName = "Amount of rocket hits to make the plane crash")]
                public int DownHits { get; set; }

                [JsonProperty(PropertyName = "Show smoke trail behind plane")]
                public bool SmokeTrail { get; set; }

                [JsonProperty(PropertyName = "Destroy objects that get in the way of a crashing plane")]
                public bool Destruction { get; set; }   
                
                [JsonProperty(PropertyName = "Destination randomization distance")]
                public float RandomDistance { get; set; }

                [JsonProperty(PropertyName = "Chance of crashing without player interaction (x out of 100)")]
                public int AutoCrashChance { get; set; }

                [JsonProperty(PropertyName = "Randomize crash direction (may lean left or right when crashing)")]
                public bool RandomizeCrashDirection { get; set; }

                [JsonProperty(PropertyName = "Automatic crash travel range minimum")]
                public float CrashMinimumRange { get; set; }

                [JsonProperty(PropertyName = "Automatic crash travel range maximum")]
                public float CrashMaximumRange { get; set; }
                
                [JsonProperty(PropertyName = "Apply crash mechanics to game spawned planes (these will not auto-crash)")]
                public bool ApplyOnSpawn { get; set; }
                
                [JsonProperty(PropertyName = "Only apply crash mechanics to game spawned planes if they are called using a supply signal")]
                public bool ApplyOnSpawnSignal { get; set; }
            }

            public class LootSettings
            {                
                [JsonProperty(PropertyName = "Fireball lifetime (seconds)")]
                public int FireLife { get; set; }

                [JsonProperty(PropertyName = "Crate amount (Crash)")]
                public int CrateCrash { get; set; }

                [JsonProperty(PropertyName = "Supply drop amount (Crash)")]
                public int SupplyCrash { get; set; }

                [JsonProperty(PropertyName = "Crate amount (Rocket hit)")]
                public int CrateHit { get; set; }

                [JsonProperty(PropertyName = "Supply drop amount (Rocket hit)")]
                public int SupplyHit { get; set; }

                [JsonProperty(PropertyName = "Supply drop loot table")]
                public LootTables SupplyLoot { get; set; }

                [JsonProperty(PropertyName = "Loot despawn time (seconds)")]
                public int LootDespawnTime { get; set; }

                [JsonProperty(PropertyName = "Maximum loot spread velocity")]
                public float LootSpread { get; set; }

                [JsonProperty(PropertyName = "Despawn loot if it falls in a safezone")]
                public bool DespawnSafezone { get; set; }

                [JsonProperty(PropertyName = "Drop standard supply drop on auto-spawned planes")]
                public bool DropOnAutoSpawn { get; set; }

                [JsonProperty(PropertyName = "Crate loot table")]
                public LootTables CrateLoot { get; set; }

                [JsonProperty(PropertyName = "Loot Locking Settings")]
                public LootLock LockSettings { get; set; }
                
                public class LootTables
                {
                    [JsonProperty(PropertyName = "Use this loot table")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Minimum amount of items to drop")]
                    public int Minimum { get; set; }

                    [JsonProperty(PropertyName = "Maximum amount of items to drop")]
                    public int Maximum { get; set; }

                    [JsonProperty(PropertyName = "Item list")]
                    public List<LootItem> Items { get; set; }

                    public class LootItem
                    {
                        [JsonProperty(PropertyName = "Item shortname")]
                        public string Shortname { get; set; }

                        [JsonProperty(PropertyName = "Minimum amount")]
                        public int Min { get; set; }

                        [JsonProperty(PropertyName = "Maximum amount")]
                        public int Max { get; set; }
                    }
                }

                public class LootLock
                {
                    [JsonProperty(PropertyName = "Lock dropped crates and supply drops")]
                    public bool LockCrates { get; set; }

                    [JsonProperty(PropertyName = "Locked crates and supply drop timer (seconds)")]
                    public int LockTimer { get; set; }

                    [JsonProperty(PropertyName = "Lock loot to player who shot down plane")]
                    public bool LockToPlayer { get; set; }

                    [JsonProperty(PropertyName = "Allow friends to loot")]
                    public bool LockToPlayerFriends { get; set; }

                    [JsonProperty(PropertyName = "Allow clan mates to loot")]
                    public bool LockToPlayerClans { get; set; }

                    [JsonProperty(PropertyName = "Allow team mates to loot")]
                    public bool LockToPlayerTeam { get; set; }

                    [JsonProperty(PropertyName = "Amount of time containers will be locked to the player who shot down the plane (seconds)")]
                    public int LockTime { get; set; }
                }
            }   

            public class Lusty
            {
                [JsonProperty(PropertyName = "Show icon on crash site")]
                public bool CrashIcon { get; set; }

                [JsonProperty(PropertyName = "Amount of time the crash icon will be displayed on LustyMap (seconds)")]
                public int CrashIconTime { get; set; }

                [JsonProperty(PropertyName = "Crash icon URL")]
                public string IconURL { get; set; }               
            }

            public class Map
            {
                [JsonProperty(PropertyName = "Show ingame map marker on crash site")]
                public bool CrashIcon { get; set; }

                [JsonProperty(PropertyName = "Amount of time the crash icon will be displayed on the ingame map (minutes)")]
                public int CrashIconTime { get; set; }               
            }

            public class Messaging
            {
                [JsonProperty(PropertyName = "Display incoming crash plane message")]
                public bool DisplayIncoming { get; set; }

                [JsonProperty(PropertyName = "Display destroyed crash plane message")]
                public bool DisplayDestroy { get; set; }

                [JsonProperty(PropertyName = "Display message stating who shot down the plane")]
                public bool DisplayAttacker { get; set; }

                [JsonProperty(PropertyName = "Use grid coordinates instead of world coordinates")]
                public bool UseGrid { get; set; }
            }

            public class Timers
            {
                [JsonProperty(PropertyName = "Autospawn crash planes with a random spawn timer")]
                public bool Random { get; set; }

                [JsonProperty(PropertyName = "Minimum time between autospawned planes (minutes)")]
                public int Min { get; set; }

                [JsonProperty(PropertyName = "Maximum time between autospawned planes (minutes)")]
                public int Max { get; set; }
            }

            public class Bots
            {
                [JsonProperty(PropertyName = "Spawn NPCs at the crash site")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Amount of NPCs to spawn")]
                public int Amount { get; set; }

                [JsonProperty(PropertyName = "Spawn corpses at the crash site")]
                public bool CorpseEnabled { get; set; }

                [JsonProperty(PropertyName = "Amount of corpses to spawn")]
                public int CorpseAmount { get; set; }

                [JsonProperty(PropertyName = "NPC's that spawn dead, or die from despawn time use the plane's attacker as the killer")]
                public bool SetAttackerKiller { get; set; }
                
                [JsonProperty(PropertyName = "Custom NPC Settings")]
                public NPCSettings CustomNPCSettings { get; set; }
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
                Cooldowns = new Dictionary<string, int>
                {
                    ["planecrash.cancall.vip1"] = 120,
                    ["planecrash.cancall.vip2"] = 60,
                    ["planecrash.cancall.vip3"] = 30,
                },
                EventTimers = new ConfigData.Timers
                {
                    Max = 60,
                    Min = 45,
                    Random = true
                },
                Plane = new ConfigData.PlaneSettings
                {
                    AutoCrashChance = 20,
                    Smoke = true,
                    Speed = 35f,
                    Height = 0f,
                    DestroyHits = 3,
                    Destruction = false,
                    DownHits = 1,
                    SmokeTrail = true,
                    RandomDistance = 20f,
                    RandomizeCrashDirection = true,
                    CrashMinimumRange = 0.3f,
                    CrashMaximumRange = 0.5f,
                },
                Loot = new ConfigData.LootSettings
                {
                    CrateCrash = 3,
                    SupplyCrash = 3,
                    FireLife = 300,
                    CrateHit = 1,
                    SupplyHit = 1,
                    LootDespawnTime = 0,
                    LootSpread = 3f,
                    DespawnSafezone = false,
                    DropOnAutoSpawn = true,
                    CrateLoot = new ConfigData.LootSettings.LootTables
                    {
                        Maximum = 4,
                        Minimum = 1,
                        Items = new List<ConfigData.LootSettings.LootTables.LootItem>
                        {
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "metal.refined", Max = 100, Min = 10 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "explosive.timed", Max = 2, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "grenade.f1", Max = 3, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "supply.signal", Max = 1, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "cctv.camera", Max = 1, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "targeting.computer", Max = 1, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "ammo.rifle", Max = 60, Min = 20 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "ammo.pistol", Max = 60, Min = 20 }
                        },
                        Enabled = false
                    },
                    SupplyLoot = new ConfigData.LootSettings.LootTables
                    {
                        Maximum = 6,
                        Minimum = 2,
                        Items = new List<ConfigData.LootSettings.LootTables.LootItem>
                        {
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "rifle.ak", Max = 1, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "pistol.m92", Max = 1, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "pistol.semiauto", Max = 1, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "shotgun.double", Max = 1, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "smg.thompson", Max = 1, Min = 1 },
                            new ConfigData.LootSettings.LootTables.LootItem { Shortname = "rifle.bolt", Max = 1, Min = 1 }
                        },
                        Enabled = false
                    },
                    LockSettings = new ConfigData.LootSettings.LootLock
                    {
                        LockCrates = true,
                        LockTimer = 120,
                        LockToPlayer = false,
                        LockToPlayerClans = false,
                        LockToPlayerFriends = false,
                        LockToPlayerTeam = false,
                        LockTime = 600
                    }
                },
                LustyOptions = new ConfigData.Lusty
                {
                    CrashIcon = true,
                    CrashIconTime = 300,
                    IconURL = "http://www.rustedit.io/images/crashicon.png"
                },
                MapOptions = new ConfigData.Map
                {
                    CrashIcon = true,
                    CrashIconTime = 5
                },
                Messages = new ConfigData.Messaging
                {
                    DisplayDestroy = true,
                    DisplayIncoming = true,
                    DisplayAttacker = true,
                    UseGrid = false
                },
                NPCOptions = new ConfigData.Bots
                {
                    Amount = 5,
                    Enabled = true,
                    CorpseAmount = 5,
                    CorpseEnabled = false,                  
                    SetAttackerKiller = true,
                    CustomNPCSettings = new NPCSettings()
                    {
                        Types = new NPCType[] { NPCType.TunnelDweller, NPCType.Scientist },
                        RoamRange = 30f, 
                        ChaseRange = 90f,
                        WoundedDurationMin = 300,
                        WoundedDurationMax = 600
                    }                    
                },
                Version = Version
            };            
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new VersionNumber(0, 1, 9))
            {
                configData.Messages.DisplayAttacker = baseConfig.Messages.DisplayAttacker;                
                configData.NPCOptions = baseConfig.NPCOptions;
            }

            if (configData.Version < new VersionNumber(0, 1, 94))            
                configData.Plane.Destruction = baseConfig.Plane.Destruction;

            if (configData.Version < new VersionNumber(0, 1, 97))
                configData.MapOptions = baseConfig.MapOptions;

           

            if (configData.Version < new VersionNumber(0, 1, 103))
            {
                configData.Plane.RandomDistance = 20f;
                configData.Cooldowns = baseConfig.Cooldowns;
            }

            if (configData.Version < new VersionNumber(0, 1, 104))
            {
                configData.Loot.LockSettings = baseConfig.Loot.LockSettings;
            }

            if (configData.Version < new VersionNumber(0, 1, 107))
            {
                configData.Plane.AutoCrashChance = 20;
                configData.Plane.RandomizeCrashDirection = true;
            }

            if (configData.Version < new VersionNumber(0, 1, 108))
            {
                configData.Plane.CrashMinimumRange = 0.3f;
                configData.Plane.CrashMaximumRange = 0.5f;
            }

            if (configData.Version < new VersionNumber(0, 1, 115))
            {
                configData.Loot.LootSpread = 3f;
                configData.NPCOptions.SetAttackerKiller = true;
            }

            if (configData.Version < new VersionNumber(0, 1, 116))
            {
                configData.Loot.DespawnSafezone = false;
            }

            if (configData.Version < new VersionNumber(0, 3, 3))
                configData.NPCOptions.CustomNPCSettings = baseConfig.NPCOptions.CustomNPCSettings;

            if (configData.Version < new VersionNumber(0, 3, 4))
                configData.Loot.DropOnAutoSpawn = true;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Corpse Inventory Population
        public class InventoryData
        {
            public List<InventoryItem> items = new List<InventoryItem>();

            public InventoryData(NPCPlayer player)
            {
                List<Item> allItems = Pool.Get<List<Item>>();
                player.inventory.GetAllItems(allItems);
                
                items = allItems.Select(item => new InventoryItem
                {
                    itemid = item.info.itemid,
                    amount = item.amount > 1 ? Random.Range(1, item.amount) : item.amount,
                    ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                    ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                    skin = item.skin,
                    condition = Random.Range(1, item.condition),
                    instanceData = new InventoryItem.InstanceData(item),
                    contents = item.contents?.itemList.Select(item1 => new InventoryItem
                    {
                        itemid = item1.info.itemid,
                        amount = item1.amount,
                        condition = Random.Range(1, item1.condition)
                    }).ToArray()
                }).ToList();
                
                Pool.FreeUnmanaged(ref allItems);
            }

            public void RestoreItemsTo(LootableCorpse corpse)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    Item item = CreateItem(items[i]);
                    item.MoveToContainer(corpse.containers[0]);
                }
            }

            private Item CreateItem(InventoryItem itemData)
            {
                Item item = ItemManager.CreateByItemID(itemData.itemid, itemData.amount, itemData.skin);
                item.condition = itemData.condition;

                if (itemData.instanceData != null)
                    itemData.instanceData.Restore(item);

                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon)
                {
                    if (!string.IsNullOrEmpty(itemData.ammotype))
                        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                    weapon.primaryMagazine.contents = itemData.ammo;
                }
                if (itemData.contents != null)
                {
                    foreach (InventoryItem contentData in itemData.contents)
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

            public class InventoryItem
            {
                public int itemid;
                public ulong skin;
                public int amount;
                public float condition;
                public int ammo;
                public string ammotype;
                public InstanceData instanceData;
                public InventoryItem[] contents;

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
        #endregion

        #region Data Management
        private StoredData storedData;
        private DynamicConfigFile data;

        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("planecrash_cooldowns");

            storedData = data.ReadObject<StoredData>();
            
            if (storedData == null || storedData.cooldowns == null)
                storedData = new StoredData();
        }

        private class StoredData
        {
            public Dictionary<ulong, double> cooldowns = new Dictionary<ulong, double>();

            private double CurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

            public void AddCooldown(BasePlayer player, int time)
            {
                if (cooldowns.ContainsKey(player.userID))
                    cooldowns[player.userID] = CurrentTime() + (time * 60);
                else cooldowns.Add(player.userID, CurrentTime() + (time * 60));
            }

            public bool IsOnCooldown(BasePlayer player)
            {
                if (!cooldowns.ContainsKey(player.userID))
                    return false;

                if (cooldowns[player.userID] < CurrentTime())
                    return false;

                return true;
            }

            public double GetTimeRemaining(BasePlayer player)
            {
                if (!cooldowns.ContainsKey(player.userID))
                    return 0;

                return cooldowns[player.userID] - CurrentTime();
            }
        }
        #endregion

        #region Localization
        private static string msg(string key, string playerId = null) => Instance.lang.GetMessage(key, Instance, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["DestroyMessage"] = "<color=#939393>A plane carrying cargo has just crashed at co-ordinates </color><color=#ce422b>X: {0}, Z: {1}</color>",
            ["DestroyMessage.Grid"] = "<color=#939393>A plane carrying cargo has just crashed around </color><color=#ce422b>{0}</color>",
            ["IncomingMessage"] = "<color=#ce422b>A low flying plane carrying cargo is about to fly over!</color><color=#939393>\nIf you are skilled enough you can shoot it down with a rocket launcher!</color>",
            ["IncomingMessage.Supply"] = "<color=#ce422b>A low flying plane carrying cargo is about to fly over!</color><color=#939393>\nIf you are skilled enough you can shoot it down with a rocket launcher!</color>",
            ["AttackerMessage1"] = "<color=#ce422b>{0}</color><color=#939393> has shot down the plane!</color>",
            ["AttackerMessage2"] = "<color=#ce422b>{0}</color><color=#939393> has shot the plane out of the sky!</color>",
            ["OnCooldown"] = "<color=#939393>You must wait another </color><color=#ce422b>{0}</color><color=#939393> before you can call another crash plane</color>",
            ["LootLocked"] = "<color=#939393>This container is locked to the player who shot down the plane</color>",
            ["AutoCrashMessage"] = "<color=#939393>A Cargo Planes <color=#ce422b>engines have malfunctioned</color> and it is making a crash landing!</color>"
        };
        #endregion
    }
}
