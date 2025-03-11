using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Ext.ChaosNPC;
using Rust;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PilotEject", "k1lly0u", "3.1.13")]
    [Description("A mini event where a helicopter malfunctions and the pilot has to eject")]
    class PilotEject : RustPlugin, IChaosNPCPlugin
    {
        #region Fields
        [PluginReference] Plugin HeliRefuel;

        private readonly List<CustomScientistNPC> customNpcs = new List<CustomScientistNPC>();

        public static PilotEject Instance { get; private set; }

        private const string ADMIN_PERM = "piloteject.admin";

        private const string HELICOPTER_PREFAB = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private const string SMOKE_EFFECT = "assets/bundled/prefabs/fx/smoke_signal_full.prefab";

        private const int LAND_LAYERS = 1 << 4 | 1 << 8 | 1 << 16 | 1 << 21 | 1 << 23;
        
        private static FieldInfo SetReachedSpinoutLocation;
        private static MethodInfo StartSpinout;
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Instance = this;

            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission(ADMIN_PERM, this);
            
            SetReachedSpinoutLocation = typeof(PatrolHelicopterAI).GetField("reachedSpinoutLocation", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            StartSpinout = typeof(PatrolHelicopterAI).GetMethod("StartSpinout", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        private void OnServerInitialized()
        {            
            if (configData.Automation.AutoSpawn)
                RunAutomatedEvent();
        }

        private void OnEntitySpawned(PatrolHelicopter baseHelicopter)
        {
            timer.In(1f, () =>
            {
                if (baseHelicopter == null || baseHelicopter.GetComponent<EjectionComponent>())
                    return;

                if (HeliRefuel && (bool)HeliRefuel.Call("IsRefuelHelicopter", baseHelicopter))
                    return;

                if (Core.Interface.Call("CanConvertToPilotEject", baseHelicopter) != null)
                    return;

                if (Mathf.Approximately(configData.Automation.Chance, 100) || Random.Range(0, 100) < configData.Automation.Chance)
                    baseHelicopter.gameObject.AddComponent<EjectionComponent>();
            });
        }

        private void OnEntityTakeDamage(PatrolHelicopter baseHelicopter, HitInfo hitInfo)
        {
            if (!baseHelicopter || hitInfo == null)
                return;

            EjectionComponent refuelHelicopter = baseHelicopter.GetComponent<EjectionComponent>();
            if (refuelHelicopter != null)
                refuelHelicopter.OnTakeDamage(hitInfo); 
        }
        
        private void OnEntityKill(PatrolHelicopter baseHelicopter)
        {
            if (baseHelicopter == null)
                return;

            EjectionComponent ejectionHelictoper = baseHelicopter.GetComponent<EjectionComponent>();
            if (ejectionHelictoper != null)
            {
                if (!ejectionHelictoper.hasEjected)
                {
                    if (configData.Ejection.EjectOnDeath)
                        ejectionHelictoper.EjectPilot();
                    return;
                }
                
                UnityEngine.Object.Destroy(ejectionHelictoper);
            }

            for (int i = 0; i < ParachuteControl._allParachutes.Count; i++)
            {
                ParachuteControl parachutePhysics = ParachuteControl._allParachutes[i];
                if (parachutePhysics != null && parachutePhysics.helicopter == baseHelicopter)                
                    parachutePhysics.crashSite = baseHelicopter.transform.position;                
            }
        }

        private void OnEntityKill(CustomScientistNPC customNpc) => customNpcs.Remove(customNpc);


        private void OnHelicopterRetire(PatrolHelicopterAI patrolHelicopterAI) => UnityEngine.Object.Destroy(patrolHelicopterAI?.helicopterBase?.GetComponent<EjectionComponent>());

        private object CanBeTargeted(BaseCombatEntity player, MonoBehaviour behaviour)
        {
            ParachuteControl npcParachute = player?.GetComponent<ParachuteControl>();
            if (npcParachute != null)
            {
                if ((behaviour is AutoTurret or GunTrap or FlameTurret) && configData.NPC.TargetedByTurrets)
                    return null;
                return false;
            }

            return null;
        }

        private void OnEntityDeath(CustomScientistNPC customNpc, HitInfo hitInfo)
        {
            if (customNpc == null || !customNpcs.Contains(customNpc))
                return;

            if (configData.Notifications.NPCDeath)
            {
                if (hitInfo != null && hitInfo.InitiatorPlayer != null)
                    Broadcast("Notification.PilotKilled", hitInfo.InitiatorPlayer.displayName);
                else Broadcast("Notification.PilotKilled2");
            }
        }
         
        private void Unload()
        {
            for (int i = EjectionComponent.allHelicopters.Count - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(EjectionComponent.allHelicopters[i]);

            for (int i = ParachuteControl._allParachutes.Count - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(ParachuteControl._allParachutes[i]);

            configData = null;
            Instance = null;
        }
        #endregion

        #region Functions
        private void RunAutomatedEvent()
        {
            timer.In(Random.Range(configData.Automation.Min, configData.Automation.Max), () =>
            {
                if (BasePlayer.activePlayerList.Count >= configData.Automation.RequiredPlayers)
                    SpawnEntity();

                RunAutomatedEvent();
            });
        }

        private EjectionComponent SpawnEntity()
        {
            PatrolHelicopter baseHelicopter = GameManager.server.CreateEntity(HELICOPTER_PREFAB) as PatrolHelicopter;
            baseHelicopter.enableSaving = false;
            baseHelicopter.Spawn();

            return baseHelicopter.gameObject.AddComponent<EjectionComponent>(); 
        }
                                
        private static void ClearContainer(ItemContainer container)
        {
            if (container == null || container.itemList == null)
                return;

            while (container.itemList.Count > 0)
            {
                Item item = container.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }

        private static void PopulateLoot(ItemContainer container, ConfigData.LootContainer loot)
        {
            if (container == null || loot == null)
                return;

            ClearContainer(container);

            int amount = Random.Range(loot.Minimum, loot.Maximum);

            List<ConfigData.LootItem> list = Pool.Get<List<ConfigData.LootItem>>();
            list.AddRange(loot.Items);

            int itemCount = 0;
            while (itemCount < amount)
            {
                int totalWeight = list.Sum((ConfigData.LootItem x) => Mathf.Max(1, x.Weight));
                int random = Random.Range(0, totalWeight);

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    ConfigData.LootItem lootItem = list[i];

                    totalWeight -= Mathf.Max(1, lootItem.Weight);

                    if (random >= totalWeight)
                    {
                        list.Remove(lootItem);

                        Item item = ItemManager.CreateByName(lootItem.Name, Random.Range(lootItem.Minimum, lootItem.Maximum), lootItem.Skin);
                        item?.MoveToContainer(container);

                        itemCount++;
                        break;
                    }
                }

                if (list.Count == 0)
                    list.AddRange(loot.Items);
            }

            Pool.FreeUnmanaged(ref list);            
        }
        #endregion

        #region CustomNPC
        public bool InitializeStates(BaseAIBrain customNPCBrain) => false;

        public bool WantsToPopulateLoot(CustomScientistNPC customNpc, NPCPlayerCorpse npcplayerCorpse)
        {
            if (configData.NPC.UseCustomLootTable)
            {
                PopulateLoot(npcplayerCorpse.containers[0], configData.NPC.RandomItems);
                return true;
            }

            return false;
        }

        public byte[] GetCustomDesign() => null;
        #endregion

        #region Component        
        private class EjectionComponent : MonoBehaviour
        {
            internal static List<EjectionComponent> allHelicopters = new List<EjectionComponent>();

            internal PatrolHelicopter Helicopter { get; private set; }

            internal PatrolHelicopterAI AI { get; private set; }
            
            private float actualHealth;

            internal bool ejectOverride = false;

            internal bool hasEjected = false;

            private void Awake()
            {
                allHelicopters.Add(this);

                Helicopter = GetComponent<PatrolHelicopter>();
                AI = Helicopter.myAI;

                if (configData.Helicopter.DamageEffects)
                {
                    PatrolHelicopter.weakspot weakspot = Helicopter.weakspots[1];
                    weakspot.healthFractionOnDestroyed = 0f;
                    weakspot.Hurt(weakspot.health, null);
                }

                actualHealth = Helicopter.health = configData.Helicopter.Health;
                Helicopter.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                if (configData.Ejection.EjectRandom)
                    InvokeHandler.Invoke(this, TryAutoEjection, Random.Range(configData.Ejection.Min, configData.Ejection.Max));
            }

            private void OnDestroy() => allHelicopters.Remove(this);

            internal void OnTakeDamage(HitInfo hitInfo)
            {
                if (!configData.Ejection.EjectOnKilled || hasEjected)
                    return;

                actualHealth -= hitInfo.damageTypes.Total();

                if (actualHealth < 0f)
                    EjectPilot();                
            }

            private void TryAutoEjection()
            {
                if (TerrainMeta.HeightMap.GetHeight(transform.position) < 0 || BasePlayer.activePlayerList.Count < configData.Automation.RequiredPlayers)
                {
                    InvokeHandler.Invoke(this, TryAutoEjection, Random.Range(30, 60));
                    return;
                }

                EjectPilot();
            }

            internal void EjectPilot()
            {
                if ((BasePlayer.activePlayerList.Count < configData.Automation.RequiredPlayers && !ejectOverride) || hasEjected)
                {
                    Destroy(this);
                    return;
                }

                InvokeHandler.CancelInvoke(this, EjectPilot);

                ServerMgr.Instance.StartCoroutine(SpawnNPCs(Helicopter));

                DropLoot();

                hasEjected = true;

                if (actualHealth < 0f)
                {
                    if (configData.Notifications.Death)
                        Broadcast("Notification.OnDeath", MapHelper.PositionToString(transform.position));
                }
                else
                {
                    if (configData.Notifications.Malfunction)
                        Broadcast("Notification.Malfunction", MapHelper.PositionToString(transform.position));
                }

                /*bool previous = PatrolHelicopterAI.monument_crash;
                PatrolHelicopterAI.monument_crash = false;
                
                ServerMgr.Instance.Invoke(() =>
                {
                    PatrolHelicopterAI.monument_crash = previous;
                }, 0.1f);*/

                if (!AI.isDead)
                {
                    AI.CriticalDamage();
                    
                    SetReachedSpinoutLocation.SetValue(AI, true);
                    StartSpinout.Invoke(AI, null);
                }

                Destroy(this);
            }
                        
            private IEnumerator SpawnNPCs(PatrolHelicopter baseHelicopter)
            {                
                Vector3 position = transform.position + (transform.up + (transform.forward * 2f));
                Quaternion rotation = transform.rotation;

                configData.NPC.CustomNPCSettings.EquipWeapon = false;
                configData.NPC.CustomNPCSettings.EnableNavMesh = false;
                configData.NPC.CustomNPCSettings.CanUseWeaponMounted = false;

                for (int i = 0; i < configData.NPC.Amount; i++)
                {     
                    CustomScientistNPC customNpc = ChaosNPC.SpawnNPC(Instance, position, configData.NPC.CustomNPCSettings);
                    if (customNpc)
                    {
                        Item slot = customNpc.inventory.containerWear.GetSlot(7);
                        if (slot != null)
                        {
                            slot.RemoveFromContainer();
                            slot.Remove();
                        }

                        Item item = ItemManager.CreateByName("parachute");
                        item.position = 7;
                        item.SetParent(customNpc.inventory.containerWear);
                       
                        customNpc.ServerRotation = rotation;
                        customNpc.modelState.flying = true;

                        Instance.customNpcs.Add(customNpc);
                        customNpc.gameObject.AddComponent<ParachuteControl>().helicopter = baseHelicopter;
                    }
                    
                    yield return CoroutineEx.waitForSeconds(0.5f);
                }
            }

            private void DropLoot()
            {
                if (configData.LootBoxes.Amount <= 0)
                    return;

                for (int i = 0; i < configData.LootBoxes.Amount; i++)
                {
                    LootContainer container = GameManager.server.CreateEntity(Helicopter.crateToDrop.resourcePath, transform.position) as LootContainer;
                    container.enableSaving = false;
                    container.Spawn();

                    Vector3 velocity = Random.onUnitSphere;
                    velocity.y = 1;

                    Rigidbody rb = container.gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.mass = 2f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.angularVelocity = UnityEngine.Vector3Ex.Range(-1.75f, 1.75f);
                    rb.drag = 0.5f * (rb.mass / 5f);
                    rb.angularDrag = 0.2f * (rb.mass / 5f);

                    rb.AddForce(velocity * 5f, ForceMode.Impulse);

                    ClearContainer(container.inventory);
                    PopulateLoot(container.inventory, configData.LootBoxes.RandomItems);
                }
            }
        }

        internal class ParachuteControl : MonoBehaviour
        {
            internal static List<ParachuteControl> _allParachutes = new List<ParachuteControl>();
            
            private CustomScientistNPC m_Entity;
            private Parachute m_Parachute;
            
            private Transform m_Transform;
            private Rigidbody m_Rigidbody;


            public PatrolHelicopter helicopter;
            public Vector3 crashSite;

            private bool isFalling;
            private bool wasWounded;
            
            private Vector3 DirectionTowardsCrash2D
            {
                get
                {
                    if (helicopter && !helicopter.IsDestroyed)
                        return (helicopter.transform.position.XZ3D() - m_Transform.position.XZ3D()).normalized;
                    return (crashSite.XZ3D() - m_Transform.position.XZ3D()).normalized;
                }
            }

            private void Awake()
            {
                m_Entity = GetComponent<CustomScientistNPC>();
                m_Entity.SetPaused(true);

                m_Transform = m_Entity.Transform;

                InitializeVelocity();

                _allParachutes.Add(this);

                wasWounded = m_Entity.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded);
                m_Entity.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
            }

            private void OnDestroy()
            {
                _allParachutes.Remove(this);

                if (m_Parachute && !m_Parachute.IsDestroyed)
                {
                    m_Parachute.ParachuteCollider.enabled = false;
                    m_Parachute.enabled = false;
                }
            }

            private void Update()
            {
                m_Entity.modelState.onground = false;
                m_Entity.modelState.flying = true;

                if (!isFalling && m_Rigidbody.velocity.y < 0)
                {
                    DeployParachute();
                    isFalling = true;
                }

                if (m_Parachute)
                {
                    if (Physics.Raycast(m_Parachute.transform.position, Vector3.down, out RaycastHit raycastHit, 2f, LAND_LAYERS))
                    {
                        enabled = false;
                        isFalling = false;

                        m_Rigidbody.useGravity = false;
                        m_Rigidbody.isKinematic = true;
                        
                        m_Entity.modelState.onground = true;
                        m_Entity.modelState.flying = false;
                        
                        m_Entity.mounted.Set(null);
                        m_Entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                        
                        m_Parachute._mounted = null;
                        m_Parachute.SetFlag(BaseEntity.Flags.Busy, false, false, true);
                        m_Parachute.Kill(BaseNetworkable.DestroyMode.None);
                        
                        if (!NavmeshSpawnPoint.Find(raycastHit.point, 15f, out Vector3 position))
                        {
                            m_Entity.Die(new HitInfo(null, m_Entity, DamageType.Fall, 1000f));
                            return;
                        }
                        
                        m_Entity.transform.position = position;                
                        m_Entity.transform.rotation = Quaternion.identity;
                        m_Entity.NavAgent.Warp(position);
                        
                        if (m_Entity.WaterFactor() > 0.85f)
                        {
                            m_Entity.Die(new HitInfo(null, m_Entity, DamageType.Drowned, 1000f));
                            return;
                        }
                        
                        OnParachuteLand();
                        return;
                    }

                    if (isFalling)
                    {
                        Vector3 windAtPosition = Vector3.Lerp(DirectionTowardsCrash2D, GetWindAtCurrentPos(), helicopter && !helicopter.IsDestroyed ? 0.25f : 0.75f);

                        float heightFromGround = Mathf.Max(TerrainMeta.HeightMap.GetHeight(m_Transform.position), TerrainMeta.WaterMap.GetHeight(m_Transform.position));
                        float force = Mathf.InverseLerp(heightFromGround + 20f, heightFromGround + 60f, m_Transform.position.y);

                        Vector3 normalizedDir = windAtPosition.normalized * (force * configData.Ejection.Wind);

                        Vector3 direction = m_Transform.InverseTransformDirection(DirectionTowardsCrash2D + normalizedDir);

                        UpdatePlayerInput((direction.x < 0.5f ? (int)BUTTON.LEFT : direction.x > 0.5f ? (int)BUTTON.RIGHT : 0) | (int)BUTTON.FORWARD);
                        m_Parachute.PlayerServerInput(m_Entity.serverInput, m_Entity);
                    }
                }
            }

            private void UpdatePlayerInput(int buttons)
            {
                m_Entity.serverInput.previous.aimAngles = m_Entity.serverInput.current.aimAngles;
                m_Entity.serverInput.previous.buttons = m_Entity.serverInput.current.buttons;
                m_Entity.serverInput.previous.mouseDelta = m_Entity.serverInput.current.mouseDelta;
                m_Entity.serverInput.current.aimAngles = Vector3.zero;
                m_Entity.serverInput.current.buttons = buttons;
                m_Entity.serverInput.current.mouseDelta = Vector3.zero;
            }
            
            private void InitializeVelocity()
            {
                m_Rigidbody = m_Entity.GetComponent<Rigidbody>();
                m_Rigidbody.useGravity = true;
                m_Rigidbody.isKinematic = false;
                m_Rigidbody.AddForce((Vector3.up * Random.Range(10, 20)) + (Random.onUnitSphere.XZ3D() * 10), ForceMode.Impulse);
            }
            
            private void DeployParachute()
            {
                Item slot = m_Entity.inventory.containerWear.GetSlot(7);
                if (slot != null && slot.info.TryGetComponent<ItemModParachute>(out ItemModParachute itemModParachute))
                {
                    m_Parachute = GameManager.server.CreateEntity(itemModParachute.ParachuteVehiclePrefab.resourcePath, m_Entity.transform.position, m_Entity.eyes.rotation, true) as Parachute;
                    if (m_Parachute)
                    {
                        Destroy(m_Parachute.GetComponent<EntityCollisionMessage>());
                        m_Parachute.skinID = slot.skin;
                        m_Parachute.Spawn();

                        m_Parachute.HurtAmount = 0;
                        
                        m_Parachute.AttemptMount(m_Entity, false);
                        if (m_Entity.isMounted)
                        {
                            if (configData.Ejection.ShowSmoke)
                                Effect.server.Run(SMOKE_EFFECT, m_Parachute, 0, Vector3.zero, Vector3.zero, null, true);

                            m_Parachute.ParachuteCollider.enabled = false;
                            m_Parachute.enabled = false;

                            m_Parachute.ConditionLossPerUse = 1f;
                            //m_Parachute._health = m_Parachute.ConditionLossPerUse * m_Parachute.MaxHealth();
                            
                            slot.Remove(0f);
                            ItemManager.DoRemoves();
                            m_Entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            return;
                        }
                        
                        m_Parachute.Kill(BaseNetworkable.DestroyMode.None);
                    }
                }
            }

            private Vector3 GetWindAtCurrentPos()
            {
                float single = m_Transform.position.y * 6f;
                Vector3 force = new Vector3(Mathf.Sin(single * 0.0174532924f), 0f, Mathf.Cos(single * 0.0174532924f));
                return force.normalized * 1f;
            }

            private void OnParachuteLand()
            {
                /*if (NavmeshSpawnPoint.Find(m_Entity.transform.position, 10f, out Vector3 position))
                    m_Entity.transform.position = position;*/
                
                ChaosNPC.SetRoamHomePosition(m_Entity, helicopter && !helicopter.IsDestroyed ? helicopter.transform.position : crashSite);

                if (wasWounded)
                    m_Entity.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);

                m_Entity.SetPaused(false);
                m_Entity.EquipWeapon();

                Destroy(this);
            }
        }
        
        /*internal class ParachutePhysics : MonoBehaviour
        {
            internal static List<ParachutePhysics> _allParachutes = new List<ParachutePhysics>();

            internal CustomScientistNPC CustomNpc { get; private set; }

            internal PatrolHelicopter Helicopter { get; set; }

            private Transform tr;

            private Rigidbody rb;

            //private BaseEntity parachute;

            private Vector3 currentWindVector = Vector3.zero;

            internal Vector3 crashSite;

            private bool isFalling;

            private bool wasWounded = false;

            private Vector3 DirectionTowardsCrash2D
            {
                get
                {
                    if (Helicopter && !Helicopter.IsDestroyed)
                        return (Helicopter.transform.position.XZ3D() - tr.position.XZ3D()).normalized;
                    return (crashSite.XZ3D() - tr.position.XZ3D()).normalized;
                }
            }

            private void Awake()
            {
                CustomNpc = GetComponent<CustomScientistNPC>();
                CustomNpc.SetPaused(true);

                tr = CustomNpc.Transform;

                InitializeVelocity();

                _allParachutes.Add(this);

                wasWounded = CustomNpc.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded);
                CustomNpc.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
            }

            private void OnDestroy()
            {
                _allParachutes.Remove(this);

                //if (parachute != null && !parachute.IsDestroyed)
                //    parachute.Kill();
            }

            private void Update()
            {
                CustomNpc.modelState.onground = false;
                CustomNpc.modelState.flying = true;

                if (!isFalling)
                    return;
                else
                {
                    if (Physics.Raycast(tr.position, Vector3.down, 0.5f, LAND_LAYERS))
                    {
                        enabled = false;
                        isFalling = false;

                        rb.useGravity = false;
                        rb.isKinematic = true;
                        rb.drag = 0;

                        CustomNpc.modelState.onground = true;
                        CustomNpc.modelState.flying = false;

                        //if (parachute != null && !parachute.IsDestroyed)
                        //    parachute.Kill();

                        if (TerrainMeta.WaterMap.GetDepth(tr.position) > 0.6f)
                        {
                            CustomNpc.Die(new HitInfo(null, CustomNpc, DamageType.Drowned, 1000f));
                            return;
                        }

                        OnParachuteLand();
                    }                    
                }
            }

            private void FixedUpdate()
            {
                if (!isFalling && rb.velocity.y < 0)
                    DeployParachute();

                if (isFalling)
                {
                    Vector3 windAtPosition = Vector3.Lerp(DirectionTowardsCrash2D, GetWindAtCurrentPos(), Helicopter != null && !Helicopter.IsDestroyed ? 0.25f : 0.75f);

                    float heightFromGround = Mathf.Max(TerrainMeta.HeightMap.GetHeight(tr.position), TerrainMeta.WaterMap.GetHeight(tr.position));
                    float force = Mathf.InverseLerp(heightFromGround + 20f, heightFromGround + 60f, tr.position.y);

                    Vector3 normalizedDir = (windAtPosition.normalized * force) * configData.Ejection.Wind;

                    currentWindVector = Vector3.Lerp(currentWindVector, normalizedDir, Time.fixedDeltaTime * 0.25f);

                    rb.AddForceAtPosition(normalizedDir * 0.1f, tr.position, ForceMode.Force);
                    rb.AddForce(normalizedDir * 0.9f, ForceMode.Force);

                    Quaternion rotation = Quaternion.LookRotation(rb.velocity);
                    tr.rotation = CustomNpc.eyes.rotation = CustomNpc.ServerRotation = rotation;
                    CustomNpc.viewAngles = rotation.eulerAngles;

                    /*parachute.transform.localRotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
                    parachute.SendNetworkUpdate();#1#
                }
            }

            private void InitializeVelocity()
            {
                rb = CustomNpc.GetComponent<Rigidbody>();
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.AddForce((Vector3.up * 15) + (Random.onUnitSphere.XZ3D() * 5), ForceMode.Impulse);
            }

            private void DeployParachute()
            {
                ItemModParachute itemModParachute;
                Item slot = CustomNpc.inventory.containerWear.GetSlot(7);
                if (slot != null && slot.info.TryGetComponent<ItemModParachute>(out itemModParachute))
                {
                    Parachute parachute = GameManager.server.CreateEntity(itemModParachute.ParachuteVehiclePrefab.resourcePath, base.transform.position, CustomNpc.eyes.rotation, true) as Parachute;
                    if (parachute)
                    {
                        parachute.skinID = slot.skin;
                        parachute.Spawn();

                        parachute.AttemptMount(CustomNpc, false);
                        if (CustomNpc.isMounted)
                        {
                            if (configData.Ejection.ShowSmoke)
                                Effect.server.Run(SMOKE_EFFECT, parachute, 0, Vector3.zero, Vector3.zero, null, true);

                            parachute.ParachuteCollider.enabled = false;
                            
                            slot.Remove(0f);
                            ItemManager.DoRemoves();
                            CustomNpc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            return;
                        }
                        parachute.Kill(BaseNetworkable.DestroyMode.None);
                    }
                }
                /*parachute = GameManager.server.CreateEntity(PARACHUTE_PREFAB, tr.position);
                parachute.enableSaving = false;
                parachute.skinID = 0;
                parachute.Spawn();

                parachute.SetParent(CustomNpc, false);
                parachute.transform.localPosition = Vector3.up * 2f;
                parachute.transform.localRotation = Quaternion.Euler(0f, CustomNpc.viewAngles.y, 0f);

                foreach (BaseEntity.Flags f in (BaseEntity.Flags[])System.Enum.GetValues(typeof(BaseEntity.Flags)))
                {
                    parachute.SetFlag(f, true, true);
                }#1#
                
                //parachute.SetFlag(BaseEntity.Flags.Reserved11, true, true);
                
                rb.drag = configData.Ejection.Drag;

                

                /*CustomNpc.Invoke(() =>
                {
                    CustomNpc.modelState.onground = false;
                    CustomNpc.modelState.flying = true;
                    CustomNpc.SendNetworkUpdate();
                }, 1f);#1#

                isFalling = true;
            }

            private Vector3 GetWindAtCurrentPos()
            {
                float single = tr.position.y * 6f;
                Vector3 force = new Vector3(Mathf.Sin(single * 0.0174532924f), 0f, Mathf.Cos(single * 0.0174532924f));
                return force.normalized * 1f;
            }

            private void OnParachuteLand()
            {
                ChaosNPC.SetRoamHomePosition(CustomNpc, Helicopter != null && !Helicopter.IsDestroyed ? Helicopter.transform.position : crashSite);

                if (wasWounded)
                    CustomNpc.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);

                CustomNpc.SetPaused(false);
                CustomNpc.EquipWeapon();

                Destroy(this);
            }
        }        */
        #endregion

        #region Commands
        [ChatCommand("pe")]
        private void cmdPilotEject(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM) && !player.IsAdmin)
                return;

            if (args.Length == 0)
            {
                SendReply(player, "/pe call - Call a PilotEject helicopter");
                SendReply(player, "/pe eject - Force eject any active pilots");
                return;
            }

            switch (args[0].ToLower())
            {
                case "call":
                    SpawnEntity();
                    SendReply(player, "Spawned PilotEject helicopter");
                    return;
                case "c":
                    if (player.net.connection.authLevel == 2)
                    {
                        EjectionComponent ejectionComponent = SpawnEntity();
                        timer.In(1f, () => ejectionComponent.Helicopter.transform.position = player.transform.position);
                    }
                    return;
                case "t":
                    if (player.net.connection.authLevel == 2)
                    {
                        EjectionComponent ejectionComponent = SpawnEntity();
                        timer.In(0.75f, () => ejectionComponent.Helicopter.transform.position = player.transform.position);
                        timer.In(1.5f, () =>
                        {
                            ejectionComponent.ejectOverride = true;
                            ejectionComponent.EjectPilot();
                        });
                    }
                    return;
                case "s":
                    if (player.net.connection.authLevel == 2)
                    {
                        for (int i = 0; i < EjectionComponent.allHelicopters.Count; i++)
                        {
                            EjectionComponent ejectionComponent = EjectionComponent.allHelicopters[i];
                            ejectionComponent.Helicopter.Die(new HitInfo(ejectionComponent.Helicopter, ejectionComponent.Helicopter, Rust.DamageType.Explosion, 100000));
                        }
                    }
                    return;
                case "eject":
                    int count = 0;
                    for (int i = 0; i < EjectionComponent.allHelicopters.Count; i++)
                    {
                        EjectionComponent ejectionComponent = EjectionComponent.allHelicopters[i];
                        if (!ejectionComponent.hasEjected)
                        {
                            ejectionComponent.ejectOverride = true;
                            ejectionComponent.EjectPilot();
                            count++;
                        }
                    }
                    SendReply(player, $"Ejected {count * configData.NPC.Amount} pilots from {count} helicopters");
                    return;

                default:
                    SendReply(player, "Invalid syntax!");
                    break;
            }
        }

        [ConsoleCommand("pe")]
        private void ccmdPilotEject(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                if (!permission.UserHasPermission(arg.Connection.userid.ToString(), ADMIN_PERM))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args.Length == 0)
            {
                SendReply(arg, "pe call - Call a PilotEject helicopter");
                SendReply(arg, "pe eject - Force eject any active pilots");
                return;
            }

            switch (arg.Args[0].ToLower())
            {
                case "call":
                    SpawnEntity();
                    return;                
                case "eject":
                    int count = 0;
                    for (int i = 0; i < EjectionComponent.allHelicopters.Count; i++)
                    {
                        EjectionComponent ejectionComponent = EjectionComponent.allHelicopters[i];
                        if (!ejectionComponent.hasEjected)
                        {
                            ejectionComponent.ejectOverride = true;
                            ejectionComponent.EjectPilot();
                            count++;
                        }
                    }
                    SendReply(arg, $"Ejected {count * configData.NPC.Amount} pilots from {count} helicopters");
                    return;

                default:
                    SendReply(arg, "Invalid syntax!");
                    break;
            }
        }
        #endregion

        #region Config        
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Event Automation")]
            public AutomationOptions Automation { get; set; }

            [JsonProperty(PropertyName = "Ejection Options")]
            public EjectionOptions Ejection { get; set; }

            [JsonProperty(PropertyName = "Helicopter Options")]
            public HelicopterOptions Helicopter { get; set; }

            [JsonProperty(PropertyName = "Notification Options")]
            public NotificationOptions Notifications { get; set; }

            [JsonProperty(PropertyName = "NPC Options")]
            public NPCOptions NPC { get; set; }

            [JsonProperty(PropertyName = "Loot Container Options")]
            public Loot LootBoxes { get; set; }

            public class AutomationOptions
            {
                [JsonProperty(PropertyName = "Automatically spawn helicopters on a timer")]
                public bool AutoSpawn { get; set; }

                [JsonProperty(PropertyName = "Auto-spawn time minimum (seconds)")]
                public float Min { get; set; }

                [JsonProperty(PropertyName = "Auto-spawn time maximum (seconds)")]
                public float Max { get; set; }

                [JsonProperty(PropertyName = "Minimum amount of online players to trigger the event")]
                public int RequiredPlayers { get; set; }

                [JsonProperty(PropertyName = "Chance of game spawned helicopter becoming a PilotEject helicopter (x / 100)")]
                public float Chance { get; set; }
            }

            public class EjectionOptions
            {
                [JsonProperty(PropertyName = "Eject the pilot when the helicopter has been shot down")]
                public bool EjectOnKilled { get; set; }

                [JsonProperty(PropertyName = "Eject the pilot when the helicopter has been destroyed mid-air")]
                public bool EjectOnDeath { get; set; }

                [JsonProperty(PropertyName = "Eject the pilot randomly")]
                public bool EjectRandom { get; set; }

                [JsonProperty(PropertyName = "Show smoke when parachuting")]
                public bool ShowSmoke { get; set; }

                [JsonProperty(PropertyName = "Random ejection time minimum (seconds)")]
                public float Min { get; set; }

                [JsonProperty(PropertyName = "Random ejection time maximum (seconds)")]
                public float Max { get; set; }

                [JsonProperty(PropertyName = "Parachute drag force")]
                public float Drag { get; set; }

                [JsonProperty(PropertyName = "Wind force")]
                public float Wind { get; set; }
            }

            public class HelicopterOptions
            {
                [JsonProperty(PropertyName = "Helicopter spawns with tail rotor on fire")]
                public bool DamageEffects { get; set; }

                [JsonProperty(PropertyName = "Start health")]
                public float Health { get; set; }
            }

            public class NotificationOptions
            {
                [JsonProperty(PropertyName = "Show notification when helicopter has been shot down")]
                public bool Death { get; set; }

                [JsonProperty(PropertyName = "Show notification when helicopter malfunctions")]
                public bool Malfunction { get; set; }

                [JsonProperty(PropertyName = "Show notification when a NPC has been killed")]
                public bool NPCDeath { get; set; }
            }

            public class NPCOptions
            {
                [JsonProperty(PropertyName = "Amount of NPCs to spawn")]
                public int Amount { get; set; }

                [JsonProperty(PropertyName = "Custom NPC Settings")]
                public NPCSettings CustomNPCSettings { get; set; }
               
                [JsonProperty(PropertyName = "Use custom loot table")]
                public bool UseCustomLootTable { get; set; }

                [JsonProperty(PropertyName = "Random loot items")]
                public LootContainer RandomItems { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by turrets")]
                public bool TargetedByTurrets { get; set; }
            }

            public class Loot
            {
                [JsonProperty(PropertyName = "Amount of loot boxes to drop when pilot ejects")]
                public int Amount { get; set; }

                [JsonProperty(PropertyName = "Loot container items")]
                public LootContainer RandomItems { get; set; }
            }

            public class LootContainer
            {
                [JsonProperty(PropertyName = "Minimum amount of items")]
                public int Minimum { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of items")]
                public int Maximum { get; set; }

                [JsonProperty(PropertyName = "Items")]
                public LootItem[] Items { get; set; }
            }

            public class LootItem
            {
                [JsonProperty(PropertyName = "Item shortname")]
                public string Name { get; set; }

                [JsonProperty(PropertyName = "Item skin ID")]
                public ulong Skin { get; set; }

                [JsonProperty(PropertyName = "Minimum amount of item")]
                public int Minimum { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of item")]
                public int Maximum { get; set; }

                [JsonProperty(PropertyName = "Item weight (a larger number has more chance of being selected)")]
                public int Weight { get; set; } = 1;
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
                Automation = new ConfigData.AutomationOptions
                {
                    AutoSpawn = false,
                    Chance = 100,
                    Min = 3600,
                    Max = 5400,
                    RequiredPlayers = 1,
                },
                Ejection = new ConfigData.EjectionOptions
                {
                    Drag = 2f,
                    EjectOnDeath = false,
                    EjectOnKilled = true,
                    EjectRandom = false,
                    ShowSmoke = false,
                    Min = 300,
                    Max = 600,
                    Wind = 10f
                },
                Helicopter = new ConfigData.HelicopterOptions
                {
                    DamageEffects = false,
                    Health = 10000
                },
                Notifications = new ConfigData.NotificationOptions
                {
                    Death = true,
                    Malfunction = true,
                    NPCDeath = true
                },
                NPC = new ConfigData.NPCOptions
                {
                    Amount = 2,                   
                    RandomItems = new ConfigData.LootContainer
                    {
                        Minimum = 3,
                        Maximum = 5,
                        Items = new ConfigData.LootItem[]
                        {
                            new ConfigData.LootItem {Name = "apple", Skin = 0, Maximum = 6, Minimum = 2 },
                            new ConfigData.LootItem {Name = "bearmeat.cooked", Skin = 0, Maximum = 4, Minimum = 2 },
                            new ConfigData.LootItem {Name = "blueberries", Skin = 0, Maximum = 8, Minimum = 4 },
                            new ConfigData.LootItem {Name = "corn", Skin = 0, Maximum = 8, Minimum = 4 },
                            new ConfigData.LootItem {Name = "fish.raw", Skin = 0, Maximum = 4, Minimum = 2 },
                            new ConfigData.LootItem {Name = "granolabar", Skin = 0, Maximum = 4, Minimum = 1 },
                            new ConfigData.LootItem {Name = "meat.pork.cooked", Skin = 0, Maximum = 8, Minimum = 4 },
                            new ConfigData.LootItem {Name = "candycane", Skin = 0, Maximum = 2, Minimum = 1 }
                        }
                    },                   
                    CustomNPCSettings = new NPCSettings
                    {
                        WoundedRecoveryChance = 80,
                        WoundedChance = 15,
                        RoamRange = 30,
                        ChaseRange = 90
                    }
                },
                LootBoxes = new ConfigData.Loot
                {
                    Amount = 2,
                    RandomItems = new ConfigData.LootContainer
                    {
                        Minimum = 3,
                        Maximum = 5,
                        Items = new ConfigData.LootItem[]
                        {
                            new ConfigData.LootItem {Name = "apple", Skin = 0, Maximum = 6, Minimum = 2 },
                            new ConfigData.LootItem {Name = "bearmeat.cooked", Skin = 0, Maximum = 4, Minimum = 2 },
                            new ConfigData.LootItem {Name = "blueberries", Skin = 0, Maximum = 8, Minimum = 4 },
                            new ConfigData.LootItem {Name = "corn", Skin = 0, Maximum = 8, Minimum = 4 },
                            new ConfigData.LootItem {Name = "fish.raw", Skin = 0, Maximum = 4, Minimum = 2 },
                            new ConfigData.LootItem {Name = "granolabar", Skin = 0, Maximum = 4, Minimum = 1 },
                            new ConfigData.LootItem {Name = "meat.pork.cooked", Skin = 0, Maximum = 8, Minimum = 4 },
                            new ConfigData.LootItem {Name = "candycane", Skin = 0, Maximum = 2, Minimum = 1 }
                        }
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

            if (configData.Version < new Core.VersionNumber(3, 0, 0))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(3, 0, 2))
                configData.Helicopter = baseConfig.Helicopter;

            if (configData.Version < new Core.VersionNumber(3, 0, 3))
                configData.Notifications = baseConfig.Notifications;

            if (configData.Version < new Core.VersionNumber(3, 0, 4))
            {
                configData.Ejection.ShowSmoke = false;
            }

            if (configData.Version < new Core.VersionNumber(3, 1, 2))
            {
                for (int i = 0; i < configData.LootBoxes.RandomItems.Items.Length; i++)                
                    configData.LootBoxes.RandomItems.Items[i].Weight = 1;
            }

            if (configData.Version < new Core.VersionNumber(3, 1, 4))
                configData.NPC.CustomNPCSettings = baseConfig.NPC.CustomNPCSettings;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Localization
        private static void Broadcast(string key, params object[] args)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                player.ChatMessage(args?.Length > 0 ? string.Format(Instance.Message(key, player.userID), args) : Instance.Message(key, player.userID));
        }

        private string Message(string key, ulong playerId = 0UL) => lang.GetMessage(key, this, playerId == 0UL ? null : playerId.ToString());

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {           
            ["Notification.OnDeath"] = "<color=#ce422b>[Pilot Eject]</color> A <color=#aaff55>Patrol Helicopter</color> has been shot down and the pilot had to eject. He was last spotted near <color=#aaff55>{0}</color>",
            ["Notification.Malfunction"] = "<color=#ce422b>[Pilot Eject]</color> A <color=#aaff55>Patrol Helicopter</color> has malfunctioned and the pilot had to eject. He was last spotted near <color=#aaff55>{0}</color>",
            ["Notification.PilotKilled"] = "<color=#ce422b>[Pilot Eject]</color> A <color=#aaff55>Patrol Helicopter</color> pilot has been killed by <color=#aaff55>{0}</color>",
            ["Notification.PilotKilled2"] = "<color=#ce422b>[Pilot Eject]</color> A <color=#aaff55>Patrol Helicopter</color> pilot has been killed",
        };
        #endregion
    }
}
