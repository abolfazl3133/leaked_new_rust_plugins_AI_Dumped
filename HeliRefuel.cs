using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Ext.ChaosNPC;
using Rust;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Facepunch.Extend;
using UnityEngine;
using UnityEngine.AI;

namespace Oxide.Plugins
{
    [Info("HeliRefuel", "k1lly0u", "2.1.21")]
    [Description("A mini-event where a helicopter populated with NPCs needs to land to refuel")]
    class HeliRefuel : RustPlugin, IChaosNPCPlugin
    {
        #region Fields
        [PluginReference] Plugin PilotEject;

        private static List<ConfigData.LocationOptions.Monument.CustomOptions> RefuelPoints;

        protected static HeliRefuel Instance { get; private set; }

        private static NavMeshHit navHit;
        private static RaycastHit raycastHit;

        private bool isSubscribed = true;

        private Hash<BasePlayer, Vector3> velocityInheritance = new Hash<BasePlayer, Vector3>();

        private List<CustomScientistNPC> customNpcs = new List<CustomScientistNPC>();

        private const string HELICOPTER_PREFAB = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private const string CHAIR_PREFAB = "assets/prefabs/deployable/chair/chair.deployed.prefab";

        private const string ELITE_CRATE_PREFAB = "assets/bundled/prefabs/radtown/crate_elite.prefab";
        private const string HELI_CRATE_PREFAB = "assets/prefabs/npc/patrol helicopter/heli_crate.prefab";

        private const string ADMIN_PERM = "helirefuel.admin";

        private const int VEHICLE_LAYER = 1 << 13 | 1 << 27;
        private const int TARGET_LAYERS = ~(1 << 10 | 1 << 12 | 1 << 13 | 1 << 17 | 1 << 18 | 1 << 27 | 1 << 28 | 1 << 29);
        
        private static FieldInfo SetReachedSpinoutLocation;
        private static FieldInfo SetLastMoveDir;
        private static MethodInfo StartSpinout;
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Instance = this;
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission(ADMIN_PERM, this);

            SetLastMoveDir = typeof(PatrolHelicopterAI).GetField("_lastMoveDir", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            SetReachedSpinoutLocation = typeof(PatrolHelicopterAI).GetField("reachedSpinoutLocation", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            StartSpinout = typeof(PatrolHelicopterAI).GetMethod("StartSpinout", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        private void OnServerInitialized()
        {
            PopulateRefuelLocations();

            ToggleSubscriptions(false);

            if (configData.Automation.Enabled)
                RunAutomatedEvent();
        }

        private void OnEntityTakeDamage(BaseNetworkable networkable, HitInfo info)
        {
            if (networkable == null || info == null)
                return;

            BaseMountable mountable = networkable as BaseMountable;
            if (mountable)
            {
                if (mountable.GetComponentInParent<RefuelHelicopter>())
                    NullifyDamage(info);
            }

            RefuelHelicopter refuelHelicopter = networkable is PatrolHelicopter ? networkable.GetComponent<RefuelHelicopter>() : null;
            if (refuelHelicopter)
                refuelHelicopter.OnDamage(info);
        }

        private void OnEntityKill(PatrolHelicopter helicopter)
        {
            RefuelHelicopter refuelHelicopter = helicopter?.GetComponent<RefuelHelicopter>();
            if (refuelHelicopter)
            {
                refuelHelicopter.destroyKill = false;
                UnityEngine.Object.Destroy(refuelHelicopter);
            }
        }

        private void OnEntityKill(CustomScientistNPC customNpc) => customNpcs.Remove(customNpc);

        private void OnHelicopterRetire(PatrolHelicopterAI helicopterAI)
        {
            RefuelHelicopter refuelHelicopter = helicopterAI?.helicopterBase?.GetComponent<RefuelHelicopter>();
            if (refuelHelicopter)
            {
                if (refuelHelicopter.hasValidLocation || refuelHelicopter.isLanding || refuelHelicopter.isRefueling)
                {
                    InvokeHandler.CancelInvoke(refuelHelicopter, refuelHelicopter.ResetStateToPatrol);
                    refuelHelicopter.ResetStateToPatrol();
                }

                refuelHelicopter.isRetiring = true;
                refuelHelicopter.destroyKill = false;
            }
        }

        private object OnNpcTarget(ScientistNPC attacker, CustomScientistNPC customNpc) => customNpcs.Contains(customNpc) ? (object)true : null;

        private object CanBradleyApcTarget(BradleyAPC bradleyApc, CustomScientistNPC customNpc) => customNpcs.Contains(customNpc) ? (object)false : null;

        private object OnTurretTarget(NPCAutoTurret turret, CustomScientistNPC customNpc) => customNpcs.Contains(customNpc) ? (object)true : null;

        private object CanBeTargeted(CustomScientistNPC customNpc, MonoBehaviour behaviour)
        {
            if (customNpcs.Contains(customNpc))
            {
                if ((behaviour is AutoTurret or GunTrap or FlameTurret) && configData.NPC.TargetedByTurrets)
                    return null;
                return false;
            }

            return null;
        }

        private void OnEntityDeath(CustomScientistNPC customNpc, HitInfo info)
        {            
            for (int i = 0; i < RefuelHelicopter.allHelicopters.Count; i++)
            {
                RefuelHelicopter refuelHelicopter = RefuelHelicopter.allHelicopters[i];
                if (refuelHelicopter.NPCs.Contains(customNpc))
                {
                    refuelHelicopter.OnNPCKilled(customNpc);
                    return;
                }
            }
        }

        private void OnPlayerCorpseSpawned(CustomScientistNPC customNpc, BaseCorpse corpse)
        {
            if (customNpc == null || corpse == null)
                return;

            if (velocityInheritance.TryGetValue(customNpc, out Vector3 velocity))
            {
                velocityInheritance.Remove(customNpc);

                NextTick(() => corpse?.GetComponent<Rigidbody>()?.AddForce(velocity, ForceMode.VelocityChange));
            }
        }

        private void Unload()
        {
            RefuelPoints.Clear();

            for (int i = RefuelHelicopter.allHelicopters.Count - 1; i >= 0 ; i--)
            {
                RefuelHelicopter refuelHelicopter = RefuelHelicopter.allHelicopters[i];
                refuelHelicopter.killSilent = true;
                refuelHelicopter.destroyKill = true;

                UnityEngine.Object.Destroy(refuelHelicopter);
            }

            configData = null;
            Instance = null;
        }
        #endregion

        #region Functions
        private void ToggleSubscriptions(bool subscribe)
        {
            if (subscribe == isSubscribed)
                return;

            if (subscribe)
            {
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnNpcTarget));
                Subscribe(nameof(CanBradleyApcTarget));
                Subscribe(nameof(OnTurretTarget));
                Subscribe(nameof(OnEntityKill));
            }
            else
            {
                Unsubscribe(nameof(OnEntityTakeDamage));
                Unsubscribe(nameof(OnNpcTarget));
                Unsubscribe(nameof(CanBradleyApcTarget));
                Unsubscribe(nameof(OnTurretTarget));
                Unsubscribe(nameof(OnEntityKill));
            }

            isSubscribed = subscribe;
        }

        private void RunAutomatedEvent()
        {
            timer.In(UnityEngine.Random.Range(configData.Automation.Minimum, configData.Automation.Maximum), () =>
            {
                if (BasePlayer.activePlayerList.Count >= configData.Automation.RequiredPlayers)
                    SpawnEntity();
                RunAutomatedEvent();
            });
        }

        private void SpawnEntity(Vector3 positionOverride = default(Vector3))
        {
            PatrolHelicopter helicopter = GameManager.server.CreateEntity(HELICOPTER_PREFAB) as PatrolHelicopter;
            helicopter.enableSaving = false;
            helicopter.Spawn();

            Broadcast("Notification.OnSpawned");

            NextTick(() =>
            {
                if (!helicopter)
                {
                    PrintError("[Plugin Conflict] Another plugin on your server has destroyed the helicopter when it spawned");
                    return;
                }

                helicopter.transform.position = positionOverride == default(Vector3) ? RandomPointOnWorldBounds() : positionOverride;
                helicopter.myAI.State_Patrol_Enter();                
                helicopter.gameObject.AddComponent<RefuelHelicopter>();
            });
        }

        private Vector3 RandomPointOnWorldBounds()
        {
            float size = TerrainMeta.Size.x / 2f;

            Vector3 position;

            if (UnityEngine.Random.value > 0.5f)
                position = new Vector3(UnityEngine.Random.value > 0.5f ? size : -size, 100, UnityEngine.Random.Range(-size, size));
            else position = new Vector3(UnityEngine.Random.Range(-size, size), 100, UnityEngine.Random.value > 0.5f ? size : -size);

            return position;
        }

        private void PopulateRefuelLocations()
        {
            RefuelPoints = new List<ConfigData.LocationOptions.Monument.CustomOptions>(configData.Location.Custom.Where(x => x.Enabled));

            ConfigData.LocationOptions.Monument config = configData.Location.Monuments;

            MonumentInfo[] monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();
            for (int i = 0; i < monuments.Length; i++)
            {
                MonumentInfo monument = monuments[i];

                if (monument.name.Contains("harbor_1", CompareOptions.IgnoreCase))
                {
                    if (config.HarbourLarge.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.LargeHarbour"),
                            Position = monument.transform.TransformPoint(config.HarbourLarge.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.HarbourLarge.Rotation)
                        });

                        config.HarbourLarge.Monument.Add(monument);
                    }
                    continue;
                }

                if (monument.name.Contains("harbor_2", CompareOptions.IgnoreCase))
                {
                    if (config.HarbourSmall.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.SmallHarbour"),
                            Position = monument.transform.TransformPoint(config.HarbourSmall.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.HarbourSmall.Rotation)
                        });

                        config.HarbourSmall.Monument.Add(monument);
                    }
                    continue;
                }

                if (monument.name.Contains("airfield_1", CompareOptions.IgnoreCase))
                {
                    if (config.Airfield.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.Airfield"),
                            Position = monument.transform.TransformPoint(config.Airfield.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.Airfield.Rotation)
                        });

                        config.Airfield.Monument.Add(monument);
                    }
                    continue;
                }

                if (monument.name.Contains("launch_site_1", CompareOptions.IgnoreCase))
                {
                    if (config.LaunchSite.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.LaunchSite"),
                            Position = monument.transform.TransformPoint(config.LaunchSite.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.LaunchSite.Rotation)
                        });

                        config.LaunchSite.Monument.Add(monument);
                    }
                    continue;
                }

                if (monument.name.Contains("oilrigai2", CompareOptions.IgnoreCase))
                {
                    if (config.LargeOilRig.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.LargeOilRig"),
                            Position = monument.transform.TransformPoint(config.LargeOilRig.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.LargeOilRig.Rotation),
                            IsOilRig = true
                        });

                        config.LargeOilRig.Monument.Add(monument);
                    }
                    continue;
                }

                if (monument.name.Contains("oilrigai", CompareOptions.IgnoreCase))
                {
                    if (config.SmallOilRig.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.SmallOilRig"),
                            Position = monument.transform.TransformPoint(config.SmallOilRig.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.SmallOilRig.Rotation),
                            IsOilRig = true
                        });

                        config.SmallOilRig.Monument.Add(monument);
                    }
                    continue;
                }
                
                if (monument.name.Contains("powerplant_1", CompareOptions.IgnoreCase))
                {
                    if (config.Powerplant.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.PowerPlant"),
                            Position = monument.transform.TransformPoint(config.Powerplant.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.Powerplant.Rotation)
                        });

                        config.Powerplant.Monument.Add(monument);
                    }                    
                }

                if (monument.name.Contains("military_tunnel_1", CompareOptions.IgnoreCase))
                {
                    if (config.MilitaryTunnels.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.MilitaryTunnel"),
                            Position = monument.transform.TransformPoint(config.MilitaryTunnels.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.MilitaryTunnels.Rotation)
                        });

                        config.MilitaryTunnels.Monument.Add(monument);
                    }
                }

                if (monument.name.Contains("junkyard_1", CompareOptions.IgnoreCase))
                {
                    if (config.Junkyard.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.Junkyard"),
                            Position = monument.transform.TransformPoint(config.Junkyard.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.Junkyard.Rotation)
                        });

                        config.Junkyard.Monument.Add(monument);
                    }
                }

                if (monument.name.Contains("water_treatment_plant_1", CompareOptions.IgnoreCase))
                {
                    if (config.WaterTreatment.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.WaterTreatment"),
                            Position = monument.transform.TransformPoint(config.WaterTreatment.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.WaterTreatment.Rotation)
                        });

                        config.WaterTreatment.Monument.Add(monument);
                    }
                }

                if (monument.name.Contains("trainyard_1", CompareOptions.IgnoreCase))
                {
                    if (config.TrainYard.Enabled)
                    {
                        RefuelPoints.Add(new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Name = Message("Monument.TrainYard"),
                            Position = monument.transform.TransformPoint(config.TrainYard.Position),
                            Rotation = monument.transform.rotation * Quaternion.Euler(config.TrainYard.Rotation)
                        });

                        config.TrainYard.Monument.Add(monument);
                    }
                }
            }

            Puts($"Found {RefuelPoints.Count} valid refuel points; {RefuelPoints.Select(x => Messages.ContainsKey(x.Name) ? Message(x.Name) : x.Name).ToSentence()}");
        }

        private static void Broadcast(string key, params object[] args)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                player.ChatMessage(args?.Length > 0 ? string.Format(Instance.Message(key, player.userID), args) : Instance.Message(key, player.userID));
        }

        private static void NullifyDamage(HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitEntity = null;
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
        }

        private static void ClearContainer(ItemContainer container)
        {
            if (container == null || container.itemList == null)
                return;

            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }

        private static void PopulateLoot(LootContainer container)
        {
            if (!container || container.inventory == null)
                return;

            ClearContainer(container.inventory);

            int amount = UnityEngine.Random.Range(configData.Loot.MinimumItems, configData.Loot.MaximumItems);

            List<ConfigData.LootOptions.LootItem> list = Pool.Get<List<ConfigData.LootOptions.LootItem>>();
            list.AddRange(configData.Loot.Items);

            for (int i = 0; i < amount; i++)
            {
                if (list.Count == 0)
                    list = new List<ConfigData.LootOptions.LootItem>(configData.Loot.Items);

                ConfigData.LootOptions.LootItem lootItem = list.GetRandom();
                list.Remove(lootItem);

                Item item = ItemManager.CreateByName(lootItem.Name, UnityEngine.Random.Range(lootItem.Minimum, lootItem.Maximum), lootItem.Skin);
                item.MoveToContainer(container.inventory);
            }

            Pool.FreeUnmanaged(ref list);
        }

        private bool IsRefuelHelicopter(PatrolHelicopter helicopter) => helicopter.GetComponent<RefuelHelicopter>();
        #endregion

        #region CustomNPC
        public bool InitializeStates(BaseAIBrain customNPCBrain) => false;

        public bool WantsToPopulateLoot(CustomScientistNPC customNpc, NPCPlayerCorpse npcplayerCorpse) => false;

        public byte[] GetCustomDesign() => null;
        #endregion

        #region Component
        public class RefuelHelicopter : MonoBehaviour
        {
            internal static List<RefuelHelicopter> allHelicopters = new List<RefuelHelicopter>();


            public PatrolHelicopter Helicopter { get; private set; }

            protected PatrolHelicopterAI AI { get; private set; }


            protected List<BaseMountable> Mounts = new List<BaseMountable>();

            internal List<CustomScientistNPC> NPCs = new List<CustomScientistNPC>();

            protected List<LootContainer> Containers = new List<LootContainer>();


            internal bool hasValidLocation = false;            

            internal bool isLanding = false;

            internal bool isRefueling = false;

            internal bool isRetiring = false;

            internal bool killSilent = false;

            private bool isOilRig = false;

            private bool enemiesInVicinity = false;

            private bool isDying = false;

            private bool hasDroppedOnDeath = false;

            internal bool destroyKill = true;

            private float actualHealth;


            private string locationName;

            private Vector3 targetLocation;

            private Quaternion targetRotation;

            private Vector3 hoverDistance = new Vector3(0f, 20f, 0f);


            private Vector3 landingLocation;

            private Quaternion landingRotation;


            private MapMarkerGenericRadius mapMarker;
            

            private float nextValidationTime;

            private const float TIME_TO_LAND = 5f;

            private float landTimeTaken = 0f;

            private const string MAP_MARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";

            private List<KeyValuePair<Vector3, float>> mountLocations = new List<KeyValuePair<Vector3, float>>
            {
                new KeyValuePair<Vector3, float>(new Vector3(-0.6f, 0.65f, 2.8f), 0f),
                new KeyValuePair<Vector3, float>(new Vector3(0.6f, 0.65f, 2.8f), 0f),
                new KeyValuePair<Vector3, float>(new Vector3(-0.8f, 0.68f, -0.5f), -90f),
                new KeyValuePair<Vector3, float>(new Vector3(0.8f, 0.68f, -0.5f), 90f),
                new KeyValuePair<Vector3, float>(new Vector3(-0.3f, 0.68f, 0.6f), 0f),
                new KeyValuePair<Vector3, float>(new Vector3(0.3f, 0.68f, 0.6f), 0f)
            };

            private void Awake()
            {
                Helicopter = GetComponent<PatrolHelicopter>();
                AI = GetComponent<PatrolHelicopterAI>();

                float maxHealth = Helicopter.MaxHealth();
                float targetHealth = maxHealth * Mathf.Max(0.1f, configData.Helicopter.StartHealthPercent);
                Helicopter._maxHealth = targetHealth;
                Helicopter.SetHealth(targetHealth);

                foreach (PatrolHelicopter.weakspot weakspot in Helicopter.weakspots)
                {
                    maxHealth = weakspot.maxHealth;
                    targetHealth = maxHealth * Mathf.Max(0.1f, configData.Helicopter.StartHealthPercent);
                    
                    weakspot.maxHealth = targetHealth;
                    weakspot.health = targetHealth;
                }
                
                Helicopter.maxCratesToSpawn = 0;

                actualHealth = Helicopter.health;

                InvokeHandler.Invoke(this, TriggerRefuel, UnityEngine.Random.Range(configData.Helicopter.MinRefuelTime, configData.Helicopter.MaxRefuelTime));

                CreateMountPoints();
                CreateNPCs();

                allHelicopters.Add(this);

                Instance.ToggleSubscriptions(true);
            }

            private void OnDestroy()
            {
                if (mapMarker && !mapMarker.IsDestroyed)
                    mapMarker.Kill();

                if (!isDying)
                {
                    for (int i = 0; i < NPCs?.Count; i++)
                    {
                        CustomScientistNPC customNpc = NPCs[i];

                        if (customNpc && !customNpc.IsDestroyed)
                            customNpc.Kill();
                    }
                }
                else
                {
                    if (configData.NPC.DespawnTime > 0f)
                    {
                        for (int i = 0; i < NPCs?.Count; i++)
                        {
                            CustomScientistNPC customNpc = NPCs[i];

                            if (customNpc && !customNpc.IsDestroyed)
                            {
                                customNpc.Invoke(()=>
                                {
                                    customNpc.Kill(BaseNetworkable.DestroyMode.None);
                                }, configData.NPC.DespawnTime);
                            }
                        }
                    }
                }

                for (int i = 0; i < Mounts?.Count; i++)
                {
                    BaseMountable mountable = Mounts[i];

                    if (mountable && !mountable.IsDestroyed)
                        mountable.Kill();
                }

                if (killSilent)
                {
                    for (int i = 0; i < Containers?.Count; i++)
                    {
                        LootContainer container = Containers[i];
                        if (container && !container.IsDestroyed)
                        {
                            ClearContainer(container.inventory);
                            container.Kill();
                        }
                    }
                }

                allHelicopters?.Remove(this);

                if (allHelicopters.Count == 0)
                    Instance?.ToggleSubscriptions(false);

                if (destroyKill && Helicopter && !Helicopter.IsDestroyed)
                    Helicopter.Kill();                
            }

            private void Update()
            {
                if (!hasValidLocation || (AI.enabled && isRetiring))
                    return;

                if (isLanding)
                {
                    landTimeTaken += Time.deltaTime;

                    float delta = Mathf.InverseLerp(0f, TIME_TO_LAND, landTimeTaken);

                    transform.position = Vector3.Lerp(landingLocation, targetLocation, delta);
                    transform.rotation = Quaternion.Slerp(landingRotation, targetRotation, delta);

                    if (delta >= 1f)
                    {
                        isLanding = false;
                        hasValidLocation = false;
                        isRefueling = true;

                        DismountAll();
                        CreateLoot();
                        CreateMapMarker();

                        if (configData.Notification.BeginRefuelling)
                            Broadcast("Notification.BeginRefuelling2", locationName);

                        InvokeHandler.Invoke(this, ResetStateToPatrol, configData.Helicopter.RefuelTime);
                    }
                    return;
                }

                float distanceFromTarget = Vector3Ex.Distance2D(transform.position, targetLocation);

                if (Time.realtimeSinceStartup > nextValidationTime)
                {                    
                    if (!IsClearToLand())
                    {
                        hasValidLocation = false;

                        if (configData.Notification.IntendedDestination)
                            Broadcast("Notification.LocationOccupied");   
                        
                        InvokeHandler.Invoke(this, FindTargetLocation, 5f);                                               
                        return;
                    }

                    FindEnemiesInVicinity();

                    nextValidationTime = Time.realtimeSinceStartup + 5f;
                }

                AIUpdate();                
                SetTargetDestination(targetLocation + hoverDistance);

                if (enemiesInVicinity)                
                    return;                
                
                if (distanceFromTarget < 5)
                {
                    isLanding = true;
                    landTimeTaken = 0f;

                    AI.enabled = false;
                    AI.ClearAimTarget();

                    landingLocation = transform.position;
                    landingRotation = transform.rotation;

                    for (int i = 0; i < NPCs.Count; i++)
                    {
                        CustomScientistNPC customNpc = NPCs[i];
                        if (isOilRig)
                        {
                            customNpc.NavAgent.areaMask = 25;
                            customNpc.NavAgent.agentTypeID = 0;
                        }
                        else
                        {
                            customNpc.NavAgent.areaMask = 1;
                            customNpc.NavAgent.agentTypeID = -1372625422;
                        }
                    }
                }
            }

            private void SetTargetDestination(Vector3 targetDest)
            {
                AI.destination = targetDest;
                float distance = Vector3.Distance(targetDest, transform.position);

                if (distance > 30)                
                    AI.SetIdealRotation(AI.GetYawRotationTo(targetDest), -1f); 
                
                AI.targetThrottleSpeed = 0.25f;
            }

            internal void ResetStateToPatrol()
            {
                if (isRetiring)
                    return;
                
                if (mapMarker && !mapMarker.IsDestroyed)
                    mapMarker.Kill();

                if (configData.NPC.Amount > 0 && NPCs.Count == 0)
                {
                    if (configData.Helicopter.SelfDestruct)
                    {
                        if (configData.Helicopter.SelfDestructTime == 0)
                            configData.Helicopter.SelfDestructTime = 300;

                        if (!isDying)
                        {
                            isDying = true;
                            Broadcast("Notification.SelfDestruct", configData.Helicopter.SelfDestructTime);
                            Helicopter.Invoke(()=> Helicopter.Hurt(Helicopter.health * 2f, DamageType.Generic, null, false), configData.Helicopter.SelfDestructTime);
                        }
                        return;
                    }
                    else
                    {
                        Broadcast("Notification.EventFinished");

                        destroyKill = false;

                        InvokeHandler.Invoke(this, ()=>
                        {
                            AI.enabled = true;
                            AI.ClearAimTarget();
                            AI.State_Patrol_Enter();

                            Destroy(this);
                        }, 5f);

                        return;
                    }
                }

                if (configData.Notification.FinishRefuelling)
                    Broadcast("Notification.FinishRefuellingPre3", locationName, configData.Helicopter.IdleTime);

                for (int i = 0; i < NPCs.Count; i++)
                {
                    CustomScientistNPC customNpc = NPCs[i];
                    ChaosNPC.SetDestination(customNpc, Helicopter.transform.position, () => MountAny(customNpc));
                }

                PackLoot();

                InvokeHandler.Invoke(this, ResetState, configData.Helicopter.IdleTime);
            }

            private void ResetState()
            {
                for (int i = 0; i < NPCs.Count; i++)
                {
                    CustomScientistNPC customNpc = NPCs[i];
                    if (customNpc && !customNpc.isMounted)
                    {
                        MountAny(customNpc);
                    }
                }

                isRefueling = false;
                enabled = true;

                AI.enabled = true;
                AI.ClearAimTarget();
                AI.State_Patrol_Enter();

                hasValidLocation = false;

                if (configData.Notification.FinishRefuelling)
                    Broadcast("Notification.FinishRefuelling2", locationName);

                InvokeHandler.Invoke(this, TriggerRefuel, UnityEngine.Random.Range(configData.Helicopter.MinRefuelTime, configData.Helicopter.MaxRefuelTime));
            }

            private void AIUpdate()
            {
                AI.MoveToDestination();
                AI.UpdateRotation();
                AI.UpdateSpotlight();
                AI.AIThink();
                AI.DoMachineGuns();
            }

            private bool IsClearToLand() => !Vis.AnyColliders(targetLocation, 10f, VEHICLE_LAYER);

            private void FindEnemiesInVicinity()
            {
                enemiesInVicinity = false;

                List<BasePlayer> players = Pool.Get<List<BasePlayer>>();

                Vis.Entities(targetLocation, 30f, players);

                players.RemoveAll(x => x.IsNpc || x.GetThreatLevel() <= 0.5f || !AI.PlayerVisible(x));

                if (players.Count > 0)
                {
                    enemiesInVicinity = true;
                    AI._targetList.Clear();
                    players.ForEach(x => AI._targetList.Add(new PatrolHelicopterAI.targetinfo(x, x)));
                }

                Pool.FreeUnmanaged(ref players);
            }

            internal void TriggerRefuel()
            {
                if (isRetiring)
                    return;

                FindTargetLocation();

                if (!hasValidLocation)
                    InvokeHandler.Invoke(this, TriggerRefuel, UnityEngine.Random.Range(configData.Helicopter.MinRefuelTime, configData.Helicopter.MaxRefuelTime));
                else
                {
                    if (configData.Notification.IntendedDestination)
                        Broadcast("Notification.TargetLocation", locationName);

                    nextValidationTime = Time.realtimeSinceStartup + 20f;
                    AI.ClearAimTarget();
                }
            }

            private void FindTargetLocation()
            {
                if (isRetiring)
                    return;
                
                targetLocation = Vector3.zero;
                targetRotation = Quaternion.identity;

                if (configData.Location.RefuelDetermination == "Closest")
                {
                    IOrderedEnumerable<ConfigData.LocationOptions.Monument.CustomOptions> points = RefuelPoints.OrderBy(x => Vector3.Distance(x.Position, transform.position));

                    for (int i = 0; i < points.Count(); i++)
                    {
                        ConfigData.LocationOptions.Monument.CustomOptions point = points.ElementAt(i);
                        
                        if (Vis.AnyColliders(point.Position, 10f, VEHICLE_LAYER))
                            continue;

                        locationName = point.Name;
                        targetLocation = point.Position;
                        targetRotation = Quaternion.Euler(-12.5f, point.Rotation.y, 0f);
                        hasValidLocation = true;
                        isOilRig = point.IsOilRig;
                        return;
                    }                    
                }
                else
                {
                    List<ConfigData.LocationOptions.Monument.CustomOptions> points = new List<ConfigData.LocationOptions.Monument.CustomOptions>(RefuelPoints);

                    for (int i = 0; i < points.Count; i++)
                    {
                        ConfigData.LocationOptions.Monument.CustomOptions point = points.GetRandom();

                        if (Vis.AnyColliders(point.Position, 10f, VEHICLE_LAYER))
                        {
                            points.Remove(point);
                            continue;
                        }

                        locationName = point.Name;
                        targetLocation = point.Position;
                        targetRotation = Quaternion.Euler(0f, point.Rotation.y, 0f);
                        hasValidLocation = true;
                        isOilRig = point.IsOilRig;
                        return;
                    }
                }

                hasValidLocation = false;
                print($"[HeliRefuel] - Unable to land for refuel. All refuel points are occupied by players or vehicles");
            }

            private void CreateMapMarker()
            {
                if (!configData.Helicopter.UseMapMarker)
                    return;

                mapMarker = GameManager.server.CreateEntity(MAP_MARKER_PREFAB, transform.position) as MapMarkerGenericRadius;               
                mapMarker.enableSaving = false;
                mapMarker.Spawn();

                mapMarker.color1 = mapMarker.color2 = ConvertToColor(configData.Helicopter.MarkerColor);
                mapMarker.alpha = configData.Helicopter.MarkerOpacity;
                mapMarker.radius = configData.Helicopter.MarkerSize;
                mapMarker.SendUpdate();
            }

            private Color ConvertToColor(string color)
            {
                if (color.StartsWith("#"))
                    color = color.Substring(1);
                int red = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);

                return new Color((float)red / 255, (float)green / 255, (float)blue / 255);
            }

            #region Death and Damage
            internal void OnDamage(HitInfo info)
            {
                actualHealth -= info.damageTypes.Total();

                if (actualHealth < 0f)
                {
                    if (isRefueling || isLanding)
                    { 
                        OnDeath(info);
                        Helicopter.Die(info);
                        //NullifyDamage(info);
                    }
                    else
                    {
                        if (isDying && info.damageTypes.GetMajorityDamageType() == DamageType.Generic)
                            DropOnDeath(info);

                        if (!isDying)
                            CrashToDeath();
                    }
                }
            }

            internal void OnDeath(HitInfo info)
            {
                if (isDying)
                    return;

                isDying = true;

                if (isRefueling || isLanding)
                {
                    AI.moveSpeed = 0;
                    SetLastMoveDir.SetValue(AI, Vector3.zero);
                }

                DropOnDeath(info);

                //Interface.CallHook("OnEntityDeath", Helicopter);
                //Helicopter.OnKilled(info);
                //Helicopter.DieInstantly();
            }
            
            internal void CrashToDeath()
            {
                isDying = true;   
                
                AI.CriticalDamage();

                SetReachedSpinoutLocation.SetValue(AI, true);
                StartSpinout.Invoke(AI, null);
            }

            private void DropOnDeath(HitInfo info)
            {
                if (hasDroppedOnDeath)
                    return;

                hasDroppedOnDeath = true;

                if (!isRefueling)
                {
                    if (configData.Helicopter.DropCratesOnDeath)
                        DropLoot();

                    if (configData.Helicopter.DropNPCsOnDeath)
                    {
                        Vector3 direction = (AI.GetLastMoveDir() * AI.GetMoveSpeed()) * 0.75f;

                        for (int i = NPCs.Count - 1; i >= 0; i--)
                        {
                            CustomScientistNPC customNpc = NPCs[i];
                            if (customNpc && !customNpc.IsDestroyed)
                            {
                                Instance.velocityInheritance[customNpc] = direction + (UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 5f));

                                DismountObject(customNpc, true);
                                customNpc.Die(new HitInfo(info != null ? info.Initiator : Helicopter, customNpc, DamageType.Explosion, 1000f));
                            }
                        }
                    }
                    else
                    {
                        for (int i = NPCs.Count - 1; i >= 0; i--)
                        {
                            CustomScientistNPC customNpc = NPCs[i];
                            if (customNpc && !customNpc.IsDestroyed)
                            {
                                DismountObject(customNpc, true);
                                customNpc.Kill();
                            }
                        }
                    }
                }
                else
                {
                    if (configData.Helicopter.DropCratesOnDeathLanded)
                        DropLoot();
                    
                    for (int i = NPCs.Count - 1; i >= 0; i--)
                    {
                        CustomScientistNPC customNpc = NPCs[i];
                        if (customNpc && !customNpc.IsDestroyed && customNpc.isMounted)
                        {
                            DismountObject(customNpc, true);
                            customNpc.Die(new HitInfo(info != null ? info.Initiator : Helicopter, customNpc, DamageType.Explosion, 1000f));
                        }
                    }
                }
            }

            #endregion

            #region NPCs and Mounting
            private void CreateMountPoints()
            {
                for (int i = 0; i < Mathf.Clamp(configData.NPC.Amount, 0, 6); i++)
                {
                    BaseMountable mountable = GameManager.server.CreateEntity(CHAIR_PREFAB, transform.position) as BaseMountable;
                    mountable.enableSaving = false;
                    mountable.isMobile = true;
                    mountable.skinID = (ulong)1169930802;
                    
                    Destroy(mountable.GetComponent<DestroyOnGroundMissing>());
                    Destroy(mountable.GetComponent<GroundWatch>());
                    
                    DestroyImmediate(mountable.transform.FindChildRecursive("Collision").gameObject);
                    
                    mountable.Spawn();
                    
                    //mountable.GetComponentInChildren<MeshCollider>().convex = true;

                    mountable.SetParent(Helicopter, false);

                    KeyValuePair<Vector3, float> mountOffset = mountLocations[i];

                    mountable.transform.localPosition = mountOffset.Key;
                    mountable.transform.localRotation = Quaternion.Euler(0, mountOffset.Value, 0);

                    Mounts.Add(mountable);
                }
            }

            private void CreateNPCs()
            {
                configData.NPC.CustomNPCSettings.EnableNavMesh = false;
                configData.NPC.CustomNPCSettings.CanUseWeaponMounted = false;
                configData.NPC.CustomNPCSettings.EquipWeapon = false;

                for (int i = 0; i < Mathf.Clamp(configData.NPC.Amount, 0, 6); i++)
                {                    
                    CustomScientistNPC customNpc = ChaosNPC.SpawnNPC(Instance, transform.position, configData.NPC.CustomNPCSettings);
                    if (customNpc)
                    {
                        Instance.customNpcs.Add(customNpc);
                        NPCs.Add(customNpc);
                           
                        MountAny(customNpc);
                    }
                }
            }

            internal void MountAny(CustomScientistNPC customNpc)
            {
                for (int i = 0; i < Mounts.Count; i++)
                {
                    BaseMountable mountable = Mounts[i];
                    if (!mountable.AnyMounted())
                    {
                        customNpc.SetPaused(true);
                        customNpc.HolsterWeapon();

                        MountObject(customNpc, mountable);
                        return;
                    }
                }
            }

            private void MountObject(CustomScientistNPC customNpc, BaseMountable mountable)
            {
                customNpc.EnsureDismounted();
                mountable._mounted = customNpc;
                customNpc.SetMounted(mountable);
                customNpc.MovePosition(mountable.mountAnchor.transform.position);
                customNpc.transform.rotation = mountable.mountAnchor.transform.rotation;
                customNpc.ServerRotation = mountable.mountAnchor.transform.rotation;
                customNpc.OverrideViewAngles(mountable.mountAnchor.transform.rotation.eulerAngles);
                customNpc.eyes.NetworkUpdate(mountable.mountAnchor.transform.rotation);
                customNpc.ClientRPCPlayer(null, customNpc, "ForcePositionTo", mountable.transform.position);
                mountable.SetFlag(BaseEntity.Flags.Busy, true, false);
            }   
            
            private void DismountAll()
            {
                for (int i = 0; i < NPCs.Count; i++)
                {
                    DismountObject(NPCs[i]);
                }
            }

            private void DismountObject(CustomScientistNPC customNpc, bool isDead = false)
            {
                if (!customNpc.isMounted)
                    return;

                BaseMountable mountable = customNpc.GetMounted();

                Vector3 dismountPosition = transform.position + (transform.forward * UnityEngine.Random.Range(-2f, 2f)) + ((UnityEngine.Random.value > 0.5f ? transform.right : -transform.right) * 3f);

                if (NavMesh.SamplePosition(dismountPosition, out navHit, 3f, -1))                
                    dismountPosition = navHit.position;

                customNpc.mounted.Set(null);
                customNpc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                mountable._mounted = null;
                mountable.SetFlag(BaseEntity.Flags.Busy, false, false);

                customNpc.transform.position = dismountPosition;                
                customNpc.transform.rotation = Quaternion.identity;
                customNpc.NavAgent.Warp(dismountPosition);

                customNpc.EquipWeapon();
                ChaosNPC.SetRoamHomePosition(customNpc, customNpc.transform.position);
                customNpc.SetPaused(false);
            }

            internal void OnNPCKilled(CustomScientistNPC customNpc)
            {
                if (killSilent)
                    return;

                NPCs.Remove(customNpc);

                if (!isDying && NPCs.Count == 0)
                {
                    if (!isRefueling)
                        CrashToDeath();
                    else
                    {
                        InvokeHandler.CancelInvoke(this, ResetStateToPatrol);
                        ResetStateToPatrol();
                    }
                }
            }
            #endregion

            #region Loot
            private void CreateLoot()
            {
                int amount = UnityEngine.Random.Range(configData.Loot.MinimumCrates, configData.Loot.MaximumCrates);

                for (int i = 0; i < amount; i++)
                {
                    LootContainer container = CreateContainer();
                    if (container)
                    {
                        Rigidbody rigidbody = container.gameObject.GetComponent<Rigidbody>();
                        if (rigidbody)
                            rigidbody.useGravity = false;

                        Containers.Add(container);
                    }
                }
            }

            private void DropLoot()
            {
                int amount = UnityEngine.Random.Range(configData.Loot.MinimumCrates, configData.Loot.MaximumCrates);
                
                Vector3 direction = (AI.GetLastMoveDir() * AI.GetMoveSpeed()) * 0.75f;
                for (int i = 0; i < amount; i++)
                {
                    LootContainer container = CreateContainer(true);
                    if (container)
                    {
                        container.Invoke(container.RemoveMe, 1800f);

                        Rigidbody rigidbody = container.gameObject.AddComponent<Rigidbody>();
                        rigidbody.useGravity = true;
                        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rigidbody.mass = 2f;
                        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                        rigidbody.velocity = direction + (UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1f, 3f));
                        rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                        rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                        rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);

                        FireBall fireBall = GameManager.server.CreateEntity(Helicopter.fireBall.resourcePath) as FireBall;
                        if (fireBall)
                        {
                            fireBall.SetParent(container, false, false);
                            fireBall.Spawn();
                            fireBall.GetComponent<Rigidbody>().isKinematic = true;
                            fireBall.GetComponent<Collider>().enabled = false;
                        }
                        container.SendMessage("SetLockingEnt", fireBall.gameObject, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }

            private LootContainer CreateContainer(bool ignoreRaycast = false)
            {
                string prefab = UnityEngine.Random.Range(0, 1) == 1 ? ELITE_CRATE_PREFAB : HELI_CRATE_PREFAB;

                Vector2 random = UnityEngine.Random.insideUnitCircle.normalized;

                Vector3 rayPosition = transform.position + (new Vector3(random.x, 0f, random.y) * 6f) + (Vector3.up * 5f);

                if (ignoreRaycast || Physics.Raycast(new Ray(rayPosition, Vector3.down), out raycastHit, 10f, TARGET_LAYERS, QueryTriggerInteraction.Ignore))
                {
                    Vector3 point = ignoreRaycast ? rayPosition : raycastHit.point;

                    LootContainer container = GameManager.server.CreateEntity(prefab, point, Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f)) as LootContainer;
                    container.enableSaving = false;
                    container.Spawn();

                    if (configData.Loot.CustomLoot)
                        PopulateLoot(container);

                    return container;
                }
                return null;
            }

            private void PackLoot()
            {
                for (int i = Containers.Count - 1; i >= 0; i--)
                {
                    LootContainer container = Containers[i];
                    if (container && !container.IsDestroyed)
                    {
                        Containers.Remove(container);
                        ClearContainer(container.inventory);
                        container.Kill();                        
                    }
                }
            }
            #endregion            
        }
        #endregion

        #region Commands
        [ConsoleCommand("hr")]
        private void ccmdHR(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            if (arg.Args == null || arg.Args.Length == 0)
            {                
                Puts("hr call - Call a refueling helicopter");
                Puts("hr killall - Kill all refueling helicopters");
                Puts("hr forcerf - Force all active helicopters to refuel");
                Puts("hr cancelrf - Force all active helicopters to finish refuelling");
                return;
            }

            switch (arg.Args[0].ToLower())
            {                
                case "call":
                    SpawnEntity();
                    return;

                case "killall":
                    Puts($"Killing {RefuelHelicopter.allHelicopters.Count} refuel helicopters");

                    for (int i = 0; i < RefuelHelicopter.allHelicopters.Count; i++)
                    {
                        RefuelHelicopter refuelHelicopter = RefuelHelicopter.allHelicopters[i];
                        refuelHelicopter.killSilent = true;

                        UnityEngine.Object.Destroy(refuelHelicopter);
                    }
                    return;

                case "forcerf":
                    for (int i = 0; i < RefuelHelicopter.allHelicopters.Count; i++)
                    {
                        RefuelHelicopter refuelHelicopter = RefuelHelicopter.allHelicopters[i];

                        if (!refuelHelicopter.hasValidLocation)
                        {
                            InvokeHandler.CancelInvoke(refuelHelicopter, refuelHelicopter.TriggerRefuel);
                            refuelHelicopter.TriggerRefuel();
                        }
                    }
                    Puts("All helicopters have been instructed to refuel!");
                    return;

                case "cancelrf":
                    for (int i = 0; i < RefuelHelicopter.allHelicopters.Count; i++)
                    {
                        RefuelHelicopter refuelHelicopter = RefuelHelicopter.allHelicopters[i];

                        if (refuelHelicopter.hasValidLocation || refuelHelicopter.isLanding || refuelHelicopter.isRefueling)
                        {
                            InvokeHandler.CancelInvoke(refuelHelicopter, refuelHelicopter.ResetStateToPatrol);
                            refuelHelicopter.ResetStateToPatrol();
                        }
                    }
                    Puts("All helicopters have been instructed to finish refuelling!");
                    return;
                default:
                    break;
            }
        }

        [ChatCommand("drawlz")]
        private void cmdDrawLZ(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                player.ChatMessage("You do not have permission to use this command");
                return;
            }

            foreach(ConfigData.LocationOptions.Monument.CustomOptions lz in RefuelPoints)
            {
                player.SendConsoleCommand("ddraw.sphere", 60, Color.blue, lz.Position, 1f);
                player.SendConsoleCommand("ddraw.arrow", 60, Color.blue, lz.Position, lz.Position + ((Quaternion)lz.Rotation * (Vector3.forward * 3f)), 1f);
            }
        }


        [ChatCommand("hr")]
        private void cmdHR(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                player.ChatMessage("You do not have permission to use this command");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("<color=#ce422b>/hr call</color> - Call a refueling helicopter");
                player.ChatMessage("<color=#ce422b>/hr killall</color> - Kill all refueling helicopters");
                player.ChatMessage("<color=#ce422b>/hr forcerf</color> - Force all active helicopters to refuel");
                player.ChatMessage("<color=#ce422b>/hr cancelrf</color> - Force all active helicopters to finish refuelling");

                player.ChatMessage("<color=#ce422b>/hr lz add <name></color> - Create a new landing zone on your position and rotation");
                player.ChatMessage("<color=#ce422b>/hr lz remove <name></color> - Remove the landing zone by name");
                player.ChatMessage("<color=#ce422b>/hr lz edit <name></color> - Edit the specified landing zone to use your position and rotation");

                player.ChatMessage("<color=#ce422b>/hr showlz</color> - Show all landing zone positions and rotations");
                return;
            }

            switch (args[0].ToLower())
            {
                case "callt":
                    SpawnEntity(player.transform.position);
                    return;
                
                case "calltrf":
                    SpawnEntity(player.transform.position);
                    
                    timer.In(1f, ()=>
                    {
                        for (int i = 0; i < RefuelHelicopter.allHelicopters.Count; i++)
                        {
                            RefuelHelicopter refuelHelicopter = RefuelHelicopter.allHelicopters[i];

                            if (!refuelHelicopter.hasValidLocation)
                            {
                                InvokeHandler.CancelInvoke(refuelHelicopter, refuelHelicopter.TriggerRefuel);
                                refuelHelicopter.TriggerRefuel();
                            }
                        }
                    });
                    return;

                case "call":
                    SpawnEntity();
                    return;

                case "killall":
                    player.ChatMessage($"Killing {RefuelHelicopter.allHelicopters.Count} refuel helicopters");
                    for (int i = 0; i < RefuelHelicopter.allHelicopters.Count; i++)
                    {
                        RefuelHelicopter refuelHelicopter = RefuelHelicopter.allHelicopters[i];
                        refuelHelicopter.killSilent = true;

                        UnityEngine.Object.Destroy(refuelHelicopter);
                    }
                    return;

                case "forcerf":
                    for (int i = 0; i < RefuelHelicopter.allHelicopters.Count; i++)
                    {
                        RefuelHelicopter refuelHelicopter = RefuelHelicopter.allHelicopters[i];

                        if (!refuelHelicopter.hasValidLocation && !refuelHelicopter.isRefueling)
                        {
                            InvokeHandler.CancelInvoke(refuelHelicopter, refuelHelicopter.TriggerRefuel);
                            refuelHelicopter.TriggerRefuel();
                        }
                    }
                    player.ChatMessage("All helicopters have been instructed to refuel!");
                    return;

                case "cancelrf":
                    for (int i = 0; i < RefuelHelicopter.allHelicopters.Count; i++)
                    {
                        RefuelHelicopter refuelHelicopter = RefuelHelicopter.allHelicopters[i];

                        if (refuelHelicopter.hasValidLocation || refuelHelicopter.isLanding || refuelHelicopter.isRefueling)
                        {
                            InvokeHandler.CancelInvoke(refuelHelicopter, refuelHelicopter.ResetStateToPatrol);
                            refuelHelicopter.ResetStateToPatrol();
                        }
                    }
                    player.ChatMessage("All helicopters have been instructed to finish refuelling!");
                    return;

                case "showlz":
                    for (int i = 0; i < RefuelPoints.Count; i++)
                    {
                        ConfigData.LocationOptions.Monument.CustomOptions lz = RefuelPoints[i];

                        player.SendConsoleCommand("ddraw.text", 30f, Color.green, (Vector3)lz.Position + Vector3.up, $"<size=20>{lz.Name}</size>");
                        player.SendConsoleCommand("ddraw.sphere", 30f, Color.blue, (Vector3)lz.Position, 0.5f);
                        player.SendConsoleCommand("ddraw.arrow", 30f, Color.blue, (Vector3)lz.Position + Vector3.up, (Vector3)lz.Position + ((Quaternion)lz.Rotation * (Vector3.forward + Vector3.up)), 0.5f);
                    }
                    return;

                case "lz":
                    {
                        if (args.Length < 3)
                        {
                            player.ChatMessage("Invalid syntax!");
                            return;
                        }

                        string name = args[2];

                        switch (args[1].ToLower())
                        {
                            case "add":
                                {
                                    if (configData.Location.Custom.Any(x => x.Name == name))
                                    {
                                        player.ChatMessage("An LZ with that name already exists!");
                                        return;
                                    }

                                    ConfigData.LocationOptions.Monument.CustomOptions lz = new ConfigData.LocationOptions.Monument.CustomOptions()
                                    {
                                        Enabled = true,
                                        Name = name,
                                        Position = player.transform.position,
                                        Rotation = new VectorData(0f, player.eyes.rotation.eulerAngles.y, 0f)
                                    };

                                    configData.Location.Custom.Add(lz);
                                    SaveConfig();

                                    PopulateRefuelLocations();

                                    player.ChatMessage("Created a new LZ on your position");
                                }
                                return;
                            case "remove":
                                {
                                    if (!configData.Location.Custom.Any(x => x.Name == name))
                                    {
                                        player.ChatMessage("There is no LZ with that name!");
                                        return;
                                    }

                                    ConfigData.LocationOptions.Monument.CustomOptions lz = configData.Location.Custom.Find(x => x.Name == name);
                                    configData.Location.Custom.Remove(lz);

                                    PopulateRefuelLocations();

                                    player.ChatMessage("Successfully removed the specified LZ");
                                }
                                return;
                            case "edit":
                                {                                    
                                    if (configData.Location.Custom.Any(x => x.Name == name))
                                    {
                                        ConfigData.LocationOptions.Monument.CustomOptions lz = configData.Location.Custom.Find(x => x.Name == name);
                                        lz.Position = player.transform.position;
                                        lz.Rotation = new VectorData(0f, player.eyes.rotation.eulerAngles.y, 0f);
                                    }
                                    else
                                    {
                                        MonumentInfo monument;
                                        switch (name.ToLower())
                                        {                                            
                                            case "airfield":
                                                monument = configData.Location.Monuments.Airfield.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.Airfield.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.Airfield.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "harbour1":
                                            case "large harbour":
                                                monument = configData.Location.Monuments.HarbourLarge.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.HarbourLarge.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.HarbourLarge.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "harbour2":
                                            case "small harbour":
                                                monument = configData.Location.Monuments.HarbourSmall.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.HarbourSmall.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.HarbourSmall.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "oilrig":
                                            case "large oilrig":
                                            case "large oil rig":
                                                monument = configData.Location.Monuments.SmallOilRig.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.SmallOilRig.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.SmallOilRig.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "oilrig2":
                                            case "small oilrig":
                                            case "small oil rig":
                                                monument = configData.Location.Monuments.LargeOilRig.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.LargeOilRig.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.LargeOilRig.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "powerplant":
                                            case "power plant":
                                                monument = configData.Location.Monuments.Powerplant.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.Powerplant.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.Powerplant.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "launchsite":
                                            case "launch site":
                                                monument = configData.Location.Monuments.LaunchSite.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.LaunchSite.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.LaunchSite.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "militarytunnels":
                                            case "military tunnels":
                                                monument = configData.Location.Monuments.MilitaryTunnels.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.MilitaryTunnels.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.MilitaryTunnels.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "junkyard":
                                            case "junk yard":
                                                monument = configData.Location.Monuments.Junkyard.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.Junkyard.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.Junkyard.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "watertreatmentplant":
                                            case "water treatment plant":
                                                monument = configData.Location.Monuments.WaterTreatment.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.WaterTreatment.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.WaterTreatment.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            case "trainyard":
                                            case "train yard":
                                                monument = configData.Location.Monuments.TrainYard.Monument?.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position))?.First();
                                                if (monument == null)
                                                    break;

                                                configData.Location.Monuments.TrainYard.Position = monument.transform.InverseTransformPoint(player.transform.position);
                                                configData.Location.Monuments.TrainYard.Rotation = (Quaternion.Inverse(monument.transform.rotation) * Quaternion.Euler(0f, player.eyes.rotation.eulerAngles.y, 0f)).eulerAngles;
                                                break;
                                            default:
                                                player.ChatMessage("Invalid syntax!");
                                                return;
                                        }
                                        if (monument == null)
                                            player.ChatMessage("This monument does not exist on your map");
                                        else
                                        {
                                            SaveConfig();
                                            PopulateRefuelLocations();
                                            player.ChatMessage("Successfully updated local position and rotation for monument");
                                        }
                                    }
                                }
                                return;                            
                        }
                    }
                    return;
                default:
                    player.ChatMessage("Invalid syntax!");
                    break;
            }
        }
        #endregion

        #region Config        
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Helicopter Options")]
            public HelicopterOptions Helicopter { get; set; }

            [JsonProperty(PropertyName = "Location Options")]
            public LocationOptions Location { get; set; }
           
            [JsonProperty(PropertyName = "Loot Options")]
            public LootOptions Loot { get; set; }

            [JsonProperty(PropertyName = "NPC Options")]
            public NPCOptions NPC { get; set; }

            [JsonProperty(PropertyName = "Automation Options")]
            public AutomationOptions Automation { get; set; }

            [JsonProperty(PropertyName = "Notification Options")]
            public NotificationOptions Notification { get; set; }

            public class HelicopterOptions
            {
                [JsonProperty(PropertyName = "Self destruct when all NPCs are killed")]
                public bool SelfDestruct { get; set; }

                [JsonProperty(PropertyName = "Amount of time until helicopter self destructs when all NPCs are killed (seconds)")]
                public int SelfDestructTime { get; set; }

                [JsonProperty(PropertyName = "The minimum amount of time the helicopter can fly before refuelling (seconds)")]
                public int MinRefuelTime { get; set; }

                [JsonProperty(PropertyName = "The maximum amount of time the helicopter can fly before refuelling (seconds)")]
                public int MaxRefuelTime { get; set; }

                [JsonProperty(PropertyName = "Amount of time the helicopter will wait to refuel (seconds)")]
                public int RefuelTime { get; set; }

                [JsonProperty(PropertyName = "Amount of time the helicopter will wait before taking off (seconds)")]
                public int IdleTime { get; set; }

                [JsonProperty(PropertyName = "Drop additional loot crates if killed whilst flying")]
                public bool DropCratesOnDeath { get; set; }

                [JsonProperty(PropertyName = "Drop additional loot crates if killed whilst landed")]
                public bool DropCratesOnDeathLanded { get; set; }

                [JsonProperty(PropertyName = "Drop NPC corpses if killed whilst flying")]
                public bool DropNPCsOnDeath { get; set; }

                [JsonProperty(PropertyName = "Display map when while helicopter is on the ground")]
                public bool UseMapMarker { get; set; }

                [JsonProperty(PropertyName = "Map marker size")]
                public float MarkerSize { get; set; }

                [JsonProperty(PropertyName = "Map marker color (hex)")]
                public string MarkerColor { get; set; }

                [JsonProperty(PropertyName = "Map marker opacity (0.0 - 1.0)")]
                public float MarkerOpacity { get; set; }

                [JsonProperty(PropertyName = "Helicopter health percentage when spawned (0.1 - X)")]
                public float StartHealthPercent { get; set; } = 1.0f;
            }

            public class LocationOptions
            {
                [JsonProperty(PropertyName = "How to determine refuel point (Random, Closest)")]
                public string RefuelDetermination { get; set; }

                [JsonProperty(PropertyName = "Monument Options")]
                public Monument Monuments { get; set; }

                [JsonProperty(PropertyName = "Custom Landing Points")]
                public List<Monument.CustomOptions> Custom { get; set; }

                public class Monument
                {
                    [JsonProperty(PropertyName = "Airfield")]
                    public Options Airfield { get; set; }

                    [JsonProperty(PropertyName = "Small Harbour")]
                    public Options HarbourSmall { get; set; }

                    [JsonProperty(PropertyName = "Large Harbour")]
                    public Options HarbourLarge { get; set; }

                    [JsonProperty(PropertyName = "Launch Site")]
                    public Options LaunchSite { get; set; }

                    [JsonProperty(PropertyName = "Power Plant")]
                    public Options Powerplant { get; set; }

                    [JsonProperty(PropertyName = "Small Oil Rig")]
                    public Options SmallOilRig { get; set; }

                    [JsonProperty(PropertyName = "Large Oil Rig")]
                    public Options LargeOilRig { get; set; }

                    [JsonProperty(PropertyName = "Junk Yard")]
                    public Options Junkyard { get; set; }

                    [JsonProperty(PropertyName = "Water Treatment Plant")]
                    public Options WaterTreatment { get; set; }

                    [JsonProperty(PropertyName = "Train Yard")]
                    public Options TrainYard { get; set; }

                    [JsonProperty(PropertyName = "Military Tunnels")]
                    public Options MilitaryTunnels { get; set; }

                    public class Options
                    {
                        public bool Enabled { get; set; }

                        [JsonProperty(PropertyName = "Local Position")]
                        public VectorData Position { get; set; }

                        [JsonProperty(PropertyName = "Local Rotation")]
                        public VectorData Rotation { get; set; }

                        [JsonIgnore]
                        public List<MonumentInfo> Monument { get; set; } = new List<MonumentInfo>();
                    }

                    public class CustomOptions : Options
                    {
                        public string Name { get; set; }

                        [JsonIgnore]
                        internal bool IsOilRig { get; set; } = false;
                    }
                }
            }

            public class NPCOptions
            {
                [JsonProperty(PropertyName = "Can be targeted by turrets")]
                public bool TargetedByTurrets { get; set; }

                [JsonProperty(PropertyName = "Amount of NPCs to spawn")]
                public int Amount { get; set; }
                
                [JsonProperty(PropertyName = "Despawn time for NPCs when helicopter is destroyed (seconds, 0 to disable)")]
                public int DespawnTime { get; set; }

                [JsonProperty(PropertyName = "Custom NPC Settings")]
                public NPCSettings CustomNPCSettings { get; set; }
            }

            public class LootOptions
            {
                [JsonProperty(PropertyName = "Minimum amount of crates to spawn")]
                public int MinimumCrates { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of crates to spawn")]
                public int MaximumCrates { get; set; }

                [JsonProperty(PropertyName = "Fill crates with custom loot")]
                public bool CustomLoot { get; set; }

                [JsonProperty(PropertyName = "Minimum amount of items to spawn")]
                public int MinimumItems { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of items to spawn")]
                public int MaximumItems { get; set; }
                
                [JsonProperty(PropertyName = "Custom Loot Contents")]
                public List<LootItem> Items { get; set; }

                public class LootItem
                {
                    [JsonProperty(PropertyName = "Shortname")]
                    public string Name { get; set; }

                    [JsonProperty(PropertyName = "Minimum amount of item")]
                    public int Minimum { get; set; }

                    [JsonProperty(PropertyName = "Maximum amount of item")]
                    public int Maximum { get; set; }

                    [JsonProperty(PropertyName = "Skin ID")]
                    public ulong Skin { get; set; }
                }
            }

            public class AutomationOptions
            {
                [JsonProperty(PropertyName = "Automatic events enabled")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Minimum amount of online players to trigger the event")]
                public int RequiredPlayers { get; set; }

                [JsonProperty(PropertyName = "Minimum amount of time between events (seconds)")]
                public int Minimum { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of time between events (seconds)")]
                public int Maximum { get; set; }
            }

            public class NotificationOptions
            {
                [JsonProperty(PropertyName = "Notify players of the helicopters intended destination")]
                public bool IntendedDestination { get; set; }

                [JsonProperty(PropertyName = "Notify players when the helicopter begins refuelling")]
                public bool BeginRefuelling { get; set; }

                [JsonProperty(PropertyName = "Notify players when the helicopter finishes refuelling")]
                public bool FinishRefuelling { get; set; }
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
                Helicopter = new ConfigData.HelicopterOptions
                {
                    SelfDestruct = true,
                    SelfDestructTime = 300,
                    MaxRefuelTime = 900,
                    MinRefuelTime = 450,
                    RefuelTime = 180,
                    IdleTime = 30,
                    DropCratesOnDeath = true,
                    DropCratesOnDeathLanded = true,
                    DropNPCsOnDeath = true,
                    UseMapMarker = false,
                    MarkerColor = "#ce422b",
                    MarkerOpacity = 0.6f,
                    MarkerSize = 0.25f
                },
                Location = new ConfigData.LocationOptions
                {
                    Custom = new List<ConfigData.LocationOptions.Monument.CustomOptions>()
                    {
                        new ConfigData.LocationOptions.Monument.CustomOptions()
                        {
                            Enabled = false,
                            Name = "Example Custom Point",
                            Position = Vector3.zero,
                            Rotation = Vector3.zero
                        }
                    },
                    RefuelDetermination = "Closest",
                    Monuments = new ConfigData.LocationOptions.Monument
                    {
                        Airfield = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(-70f, 0.3f, 34.5f),
                            Rotation = new VectorData(0f, 180f, 0f)
                        },
                        HarbourLarge = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(53.6f, 5.1f, -36.4f),
                            Rotation = new VectorData(0f, 0f, 0f)
                        },
                        HarbourSmall = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(-15.6f, 4.9f, -25f),
                            Rotation = new VectorData(0f, 270f, 0f)
                        },
                        LaunchSite = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(-20f, 3.2f, 0f),
                            Rotation = new VectorData(0f, 90f, 0f)
                        },
                        SmallOilRig = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(39f, 31.52f, -10f),
                            Rotation = new VectorData(0f, 270f, 0f)
                        },
                        LargeOilRig = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(29f, 45.12f, 5f),
                            Rotation = new VectorData(0f, 270f, 0f)
                        },
                        Powerplant = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(-23.7f, 0.1f, -46.5f),
                            Rotation = new VectorData(0f, 90f, 0f)
                        },
                        Junkyard = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(-20.4f, 20.9f, 38.7f),
                            Rotation = new VectorData(0f, 115f, 0f)
                        },
                        MilitaryTunnels = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(-24.2f, 20f, -75f),
                            Rotation = new VectorData(0f, 311f, 0f)
                        },
                        TrainYard = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(90f, 0.3f, -61.5f),
                            Rotation = new VectorData(0f, 270f, 0f)
                        },
                        WaterTreatment = new ConfigData.LocationOptions.Monument.Options
                        {
                            Enabled = true,
                            Position = new VectorData(39f, 0.25f, -62f),
                            Rotation = new VectorData(0f, 270f, 0f)
                        }
                    }
                },               
                Loot = new ConfigData.LootOptions
                {
                    CustomLoot = false,
                    Items = new List<ConfigData.LootOptions.LootItem>
                    {
                        new ConfigData.LootOptions.LootItem {Name = "syringe.medical", Maximum = 6, Minimum = 2 },
                        new ConfigData.LootOptions.LootItem {Name = "largemedkit", Maximum = 2, Minimum = 1 },
                        new ConfigData.LootOptions.LootItem {Name = "bandage", Maximum = 4, Minimum = 1 },
                        new ConfigData.LootOptions.LootItem {Name = "antiradpills", Maximum = 3, Minimum = 1 },
                        new ConfigData.LootOptions.LootItem {Name = "ammo.rifle", Maximum = 100, Minimum = 10 },
                        new ConfigData.LootOptions.LootItem {Name = "ammo.pistol", Maximum = 100, Minimum = 10 },
                        new ConfigData.LootOptions.LootItem {Name = "ammo.rocket.basic", Maximum = 3, Minimum = 1 },
                        new ConfigData.LootOptions.LootItem {Name = "ammo.shotgun.slug", Maximum = 20, Minimum = 10 },
                        new ConfigData.LootOptions.LootItem {Name = "pistol.m92", Maximum = 1, Minimum = 1 },
                        new ConfigData.LootOptions.LootItem {Name = "rifle.ak", Maximum = 1, Minimum = 1 },
                        new ConfigData.LootOptions.LootItem {Name = "rifle.bolt", Maximum = 1, Minimum = 1 },
                        new ConfigData.LootOptions.LootItem {Name = "rocket.launcher", Maximum = 1, Minimum = 1 },
                        new ConfigData.LootOptions.LootItem {Name = "pistol.revolver", Maximum = 1, Minimum = 1 }
                    },
                    MaximumCrates = 4,
                    MaximumItems = 6,
                    MinimumCrates = 2,
                    MinimumItems = 2
                },
                NPC = new ConfigData.NPCOptions
                {
                    CustomNPCSettings = new NPCSettings
                    {
                        Types = new NPCType[] { NPCType.Scientist, NPCType.HeavyScientist },
                        DisplayNames = new string[] { "Pilot", "Co-Pilot", "Gunner", "Guard" },
                        RoamRange = 30f,
                        ChaseRange = 90f
                    },
                    Amount = 4,
                    TargetedByTurrets = true
                },
                Automation = new ConfigData.AutomationOptions
                {
                    Enabled = true,
                    Minimum = 1800,
                    Maximum = 3600,
                    RequiredPlayers = 1
                },
                Notification = new ConfigData.NotificationOptions
                {
                    BeginRefuelling = true,
                    FinishRefuelling = true,
                    IntendedDestination = true
                }
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(2, 0, 0))
                configData = baseConfig;

            if (configData.Version < new VersionNumber(2, 0, 1))
            {
                configData.Location.Monuments.Junkyard = baseConfig.Location.Monuments.Junkyard;
                configData.Location.Monuments.MilitaryTunnels = baseConfig.Location.Monuments.MilitaryTunnels;
                configData.Location.Monuments.TrainYard = baseConfig.Location.Monuments.TrainYard;
                configData.Location.Monuments.WaterTreatment = baseConfig.Location.Monuments.WaterTreatment;
                configData.Location.Monuments.LargeOilRig = baseConfig.Location.Monuments.LargeOilRig;
                configData.Location.Monuments.SmallOilRig = baseConfig.Location.Monuments.SmallOilRig;
            }

            if (configData.Version < new VersionNumber(2, 0, 3))
            {
                configData.Location.Monuments.LargeOilRig = baseConfig.Location.Monuments.LargeOilRig;
                configData.Location.Monuments.SmallOilRig = baseConfig.Location.Monuments.SmallOilRig;
            }

            if (configData.Version < new VersionNumber(2, 0, 5))
                configData.Automation.RequiredPlayers = 1;

            if (configData.Version < new VersionNumber(2, 0, 7))
            {
                configData.Helicopter.SelfDestruct = true;
                configData.Helicopter.SelfDestructTime = 300;
            }

            if (configData.Version < new VersionNumber(2, 0, 9))
            {
                configData.Notification = baseConfig.Notification;
            }

            if (configData.Version < new VersionNumber(2, 0, 13))
            {
                configData.Helicopter.IdleTime = 10;
            }

            if (configData.Version < new VersionNumber(2, 0, 17))
            {
                configData.Helicopter.MarkerColor = baseConfig.Helicopter.MarkerColor;
                configData.Helicopter.MarkerOpacity = baseConfig.Helicopter.MarkerOpacity;
                configData.Helicopter.MarkerSize = baseConfig.Helicopter.MarkerSize;
                configData.Helicopter.UseMapMarker = baseConfig.Helicopter.UseMapMarker;
            }

            if (configData.Version < new VersionNumber(2, 1, 0))
            {
                configData.Location.Monuments.Powerplant.Position = baseConfig.Location.Monuments.Powerplant.Position;
                configData.Helicopter.IdleTime = 30;
            }

            if (configData.Version < new VersionNumber(2, 1, 3))
                configData.Location.Monuments.HarbourSmall.Position = baseConfig.Location.Monuments.HarbourSmall.Position;

            if (configData.Version < new VersionNumber(2, 1, 5))
            {
                configData.NPC = baseConfig.NPC;
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        internal class VectorData
        {
            public float x, y, z;

            public VectorData(){ }

            public VectorData(float x, float y, float z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public static implicit operator VectorData(Vector3 v)
            {
                return new VectorData(v.x, v.y, v.z);
            }

            public static implicit operator VectorData(Quaternion q)
            {
                return q.eulerAngles;
            }

            public static implicit operator Vector3(VectorData v)
            {
                return new Vector3(v.x, v.y, v.z);
            }

            public static implicit operator Quaternion(VectorData v)
            {
                return Quaternion.Euler(v);
            }
        }
        #endregion

        #region Localization
        private string Message(string key, ulong playerId = 0UL) => lang.GetMessage(key, this, playerId == 0UL ? null : playerId.ToString());

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Monument.Airfield"] = "Airfield",
            ["Monument.LargeHarbour"] = "Large Harbour",
            ["Monument.SmallHarbour"] = "Small Harbour",
            ["Monument.LaunchSite"] = "Launch Site",
            ["Monument.LargeOilRig"] = "Large Oil Rig",
            ["Monument.SmallOilRig"] = "Small Oil Rig",
            ["Monument.PowerPlant"] = "Power Plant",
            ["Monument.MilitaryTunnel"] = "Military Tunnels",
            ["Monument.Junkyard"] = "Junk Yard",
            ["Monument.WaterTreatment"] = "Water Treatment Plant",
            ["Monument.TrainYard"] = "Train Yard",
            ["Notification.OnSpawned"] = "<color=#ce422b>[Refuel Event]</color> A <color=#aaff55>Patrol Helicopter</color> is inbound but is low on fuel. Keep an eye out, this could be rewarding.",
            ["Notification.TargetLocation"] = "<color=#ce422b>[Refuel Event]</color> The <color=#aaff55>Patrol Helicopter</color> needs to refuel! Looks like it is heading towards <color=#ce422b>{0}</color> but be warned, if it spots you before it has landed it will attack!",
            ["Notification.LocationOccupied"] = "<color=#ce422b>[Refuel Event]</color> The <color=#aaff55>Patrol Helicopter</color> refuel location has been compromised! Scouting a new location...",
            ["Notification.SelfDestruct"] = "<color=#ce422b>[Refuel Event]</color> The <color=#aaff55>Patrol Helicopters</color> crew have been killed. Self destruct sequence initiated! <color=#ce422b>T-{0} seconds!</color>",
            ["Notification.BeginRefuelling2"] = "<color=#ce422b>[Refuel Event]</color> The <color=#aaff55>Patrol Helicopter</color> is refuelling at <color=#ce422b>{0}</color>. Now is the time to strike!",
            ["Notification.FinishRefuellingPre3"] = "<color=#ce422b>[Refuel Event]</color> The <color=#aaff55>Patrol Helicopter</color> will leave <color=#ce422b>{0}</color> in <color=#ce422b>{1} seconds</color>!",
            ["Notification.FinishRefuelling2"] = "<color=#ce422b>[Refuel Event]</color> The <color=#aaff55>Patrol Helicopter</color> has finished refuelling at <color=#ce422b>{0}</color> and is back to patrolling the skies!",
            ["Notification.EventFinished"] = "<color=#ce422b>[Refuel Event]</color> The <color=#aaff55>Patrol Helicopter</color> NPCs have been killed. The event is over.",
        };
        #endregion
    }
}
