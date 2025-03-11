// Requires: ZombieHorde
using System.Collections.Generic;
using System.Collections;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using Oxide.Game.Rust.Cui;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using Color = UnityEngine.Color;
using UnityEngine.AI;
using Facepunch;
using Facepunch.Utility;
using VLB;
using Rust;
using ProtoBuf;
using Newtonsoft.Json.Linq;
using Rust.Ai;
using System.Globalization;
using UnityEngine;
using Random = UnityEngine.Random;
using Oxide.Ext.ChaosNPC;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("Raiding Zombies", "https://discord.gg/TrJ7jnS233", "3.2.0", ResourceId = 99)]
    [Description("Make zombies toss C4")]
    public class RaidingZombies : RustPlugin
    {
        [PluginReference]
        private Plugin TruePVE;

        public static RaidingZombies _instance;
        public static bool debug = false;
        private bool isInit;
        private int totalZombies;
        private int totalC4z;
        private int totalRz;
        public bool theSwitch = false;
        public List<ulong> raidingZombie = new List<ulong>();
        private static Collider[] colBuffer;
        private static int targetLayer;
        private static Vector3 Vector3Down;
        static Dictionary<string, string> rocketTypes = new Dictionary<string, string>();

        #region Config

        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; }

            public class Settings
            {
                [JsonProperty(PropertyName = "Chance of making group raiders")]
                public int chance { get; set; }

                [JsonProperty(PropertyName = "Total raiders in the group")]
                public int TotalPerHorde { get; set; }

                [JsonProperty(PropertyName = "Total Explosives each raider has")]
                public int totalExplosivesToUse { get; set; } 

                [JsonProperty(PropertyName = "Target only bases of players he has seen")]
                public bool targetPlayerOnly { get; set; }

                [JsonProperty(PropertyName = "How long to forget a target he has seen")]
                public float ForgetTargetTime { get; set; }

                [JsonProperty(PropertyName = "How far the Leader will scan for a base to raid")]
                public float BaseScanDistance { get; set; }

                [JsonProperty(PropertyName = "How mutch scale damage explosives will do")]
                public float DamageScale { get; set; }

                [JsonProperty(PropertyName = "Item shortname of Throwable item he can use")]
                public List<string> ThrowExplosiveItemTypes { get; set; }

                [JsonProperty(PropertyName = "Rocket Prefab shortnames of rockets he can use")]
                public List<string> RocketPrefabTypes { get; set; }
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
                    chance = 50,
                    TotalPerHorde = 3,
                    totalExplosivesToUse = 5,
                    ThrowExplosiveItemTypes = new List<string>() { "explosive.timed", "explosive.satchel" },
                    RocketPrefabTypes = new List<string>() { "rocket_basic" },
                    targetPlayerOnly = true,
                    ForgetTargetTime = 640.0f,
                    BaseScanDistance = 40.0f,
                    DamageScale = 10f,
                },

                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(3, 0, 6))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion Config

        void OnEntitySpawned(ZombieHorde.ZombieNPC npc)
        {
            if (!isInit) return;
            timer.Once(5f, () =>
            {
                if (npc != null && !npc.IsDestroyed)
                {
                    if (npc.GetComponent<ZombieHorde.ZombieNPC>() != null)
                    {
                        if (npc.IsGroupLeader)
                        {
                            int rando = UnityEngine.Random.Range(0, 100);
                            if (rando <= configData.settings.chance)
                            {
                                npc?.gameObject.GetOrAddComponent<raidTrigger>();
                                if (debug) PrintWarning("Added Raid Leader OnEntitySpawned");
                            }
                        }
                    }
                }
            });
        }
        
        private void OnServerInitialized()
        {
            _instance = this;
            if (TruePVE != null) Unsubscribe("OnEntityTakeDamage");
            Vector3Down = new Vector3(0f, -1f, 0f);
            colBuffer = Vis.colBuffer;
            targetLayer = LayerMask.GetMask("Deployed", "Construction");
           
            timer.Once(60, () =>
            {
                for (int i = ZombieHorde.Horde.AllHordes.Count - 1; i >= 0; i--)
                {
                    ZombieHorde.Horde g = ZombieHorde.Horde.AllHordes[i];

                    if (g != null && g.Leader != null)
                    {
                        int rando = UnityEngine.Random.Range(0, 100);
                        if (rando <= configData.settings.chance)
                        {
                            g?.Leader?.gameObject.GetOrAddComponent<raidTrigger>();
                            totalZombies++;
                            if (debug) PrintWarning("Added Raid Leader");
                        }
                    }
                }

                rocketTypes.Add("rocket_basic", "assets/prefabs/ammo/rocket/rocket_basic.prefab");
                rocketTypes.Add("rocket_fire", "assets/prefabs/ammo/rocket/rocket_fire.prefab");
                rocketTypes.Add("rocket_heli", "assets/prefabs/npc/patrol helicopter/rocket_heli.prefab");
                rocketTypes.Add("rocket_heli_airburst", "assets/prefabs/npc/patrol helicopter/rocket_heli_airburst.prefab");
                rocketTypes.Add("rocket_heli_napalm", "assets/prefabs/npc/patrol helicopter/rocket_heli_napalm.prefab");
                rocketTypes.Add("rocket_hv", "assets/prefabs/ammo/rocket/rocket_hv.prefab");
                rocketTypes.Add("rocket_sam", "assets/prefabs/npc/sam_site_turret/rocket_sam.prefab");
                rocketTypes.Add("rocket_smoke", "assets/prefabs/ammo/rocket/rocket_smoke.prefab");
                rocketTypes.Add("rocket_mlrs", "assets/content/vehicles/mlrs/rocket_mlrs.prefab");

                foreach (string rockets in configData.settings.RocketPrefabTypes)
                {
                    if (!rocketTypes.ContainsKey(rockets))
                       configData.settings.RocketPrefabTypes.Remove("rockets");
                }
                SaveConfig();

                isInit = true;

                PrintWarning($"Added a total of {totalZombies} Groups of zombie raiders");
            });
        }

        private class mynewclass : BaseAIBrain.BasicAIState//CustomScientistNPC
        {
            public ZombieHorde.ZombieNPC zombieNpc;
            private float nextThrowTime = Time.time;
            private float nextTargetTime = Time.time;
            private float nextPositionUpdateTime;
            private float forgetTargetTime = Time.time;
            private bool isThrowingWeapon = false;
            public int totalTossed;
            public int totalBoom;
            public bool isC4 = true;
            public ThrownWeapon _throwableWeapon = null;
            public BaseProjectile _ProectileWeapon = null;
            private Vector3 nextPosition = Vector3.zero;
            public BaseCombatEntity targetEntity;
            private Transform tr;
            public BasePlayer targetPlayer;
            private bool DoNotMove;
            private bool notActive;
            private Vector3 destination = Vector3.zero;
            private bool isSetup { get; set; }
            private bool unreachableLastUpdate { get; set; }
            private float nextRaidFire { get; set; }
            public bool canLeave { get; set; }
            
            internal void TryThrowBoom(bool rocket = false)
            {
                if (isThrowingWeapon)
                    return;

                nextRaidFire = Time.time + UnityEngine.Random.Range(10f, 15f);
                isThrowingWeapon = true;
                zombieNpc.StartCoroutine(ThrowWeaponBoom(rocket));
            }

            private IEnumerator ThrowWeaponBoom(bool rocket)
            {
                EquipThrowable(rocket);

                yield return CoroutineEx.waitForSeconds(1.5f);

                if (targetEntity != null && !targetEntity.IsDestroyed)
                {
                    zombieNpc.SetAimDirection((targetEntity.transform.position - zombieNpc.transform.position).normalized);

                    DoNotMove = true;
                    yield return CoroutineEx.waitForSeconds(0.1f);
                    if (!rocket && _throwableWeapon != null)
                    {
                        if (targetEntity != null && !targetEntity.IsDestroyed)
                            ServerThrow(targetEntity.transform.position);
                    }
                    else if (rocket)
                    {
                        yield return CoroutineEx.waitForSeconds(0.6f);
                        if (targetEntity != null && !targetEntity.IsDestroyed)
                            zombieNpc.SetAimDirection((targetEntity.transform.position - zombieNpc.transform.position).normalized);
                        yield return CoroutineEx.waitForSeconds(0.5f);
                        fireRocket(true);
                        yield return CoroutineEx.waitForSeconds(2.0f);
                        DoNotMove = false;
                    }
                    totalBoom--;
                    nextThrowTime = Time.time + 15f;
                }

                yield return CoroutineEx.waitForSeconds(1f);
                if (zombieNpc != null && !zombieNpc.IsDestroyed)
                {
                    zombieNpc.EquipWeapon();
                }
                removeweapons();
                yield return CoroutineEx.waitForSeconds(0.1f);

                totalTossed++;
                isThrowingWeapon = false;
                ServerMgr.Instance.StopCoroutine(ThrowWeaponBoom(false));
            }


            private void EquipThrowable(bool rocket = false)
            {
                if (targetEntity == null || targetEntity.IsDestroyed || targetEntity.IsDead())
                    return;
                Item itemC4 = null;
                zombieNpc.inventory.containerBelt.capacity = 6;
                Item items = zombieNpc.inventory.containerBelt.GetSlot(5);
                if (items != null) zombieNpc.inventory.Take(null, items.info.itemid, items.amount);
                if (!rocket)
                {
                    itemC4 = ItemManager.CreateByName(_instance.configData.settings.ThrowExplosiveItemTypes.GetRandom(), 1);
                    if (itemC4 == null) return;
                    itemC4.MoveToContainer(zombieNpc.inventory.containerBelt, 5);
                    _throwableWeapon = itemC4.GetHeldEntity() as ThrownWeapon;
                    zombieNpc.UpdateActiveItem(zombieNpc.inventory.containerBelt.GetSlot(5).uid);
                    if (_throwableWeapon == null) return;
                    _throwableWeapon.SetHeld(true);
                    zombieNpc.inventory.UpdatedVisibleHolsteredItems();
                    zombieNpc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
                else
                {
                    Item launcher = null;
                    launcher = ItemManager.CreateByName("rocket.launcher", 1);
                    launcher.MoveToContainer(zombieNpc.inventory.containerBelt, 5);
                    zombieNpc.UpdateActiveItem(zombieNpc.inventory.containerBelt.GetSlot(5).uid);
                    _ProectileWeapon = launcher.GetHeldEntity() as BaseProjectile;
                    if (_ProectileWeapon == null) { return; }
                    _ProectileWeapon.SetHeld(true);
                    zombieNpc.inventory.UpdatedVisibleHolsteredItems();
                    zombieNpc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
            }

            public void ServerThrow(Vector3 targetPosition)
            {
                ThrownWeapon wep = _throwableWeapon as ThrownWeapon;
                if (wep == null) return;
                BasePlayer ownerPlayer = wep.GetOwnerPlayer();
                if ((UnityEngine.Object)ownerPlayer == (UnityEngine.Object)null)
                    return;
                Vector3 position = ownerPlayer.eyes.position;
                Vector3 vector3 = ownerPlayer.eyes.BodyForward();
                float num1 = 1f;
                wep.SignalBroadcast(BaseEntity.Signal.Throw, string.Empty);
                BaseEntity entity = GameManager.server.CreateEntity(wep.prefabToThrow.resourcePath, position, Quaternion.LookRotation(wep.overrideAngle == Vector3.zero ? -vector3 : wep.overrideAngle));
                if ((UnityEngine.Object)entity == (UnityEngine.Object)null)
                    return;
                entity.creatorEntity = (BaseEntity)ownerPlayer;
                Vector3 aimDir = vector3 + Quaternion.AngleAxis(10f, Vector3.right) * Vector3.up;
                float f = 5f;
                if (float.IsNaN(f))
                {
                    aimDir = vector3 + Quaternion.AngleAxis(20f, Vector3.right) * Vector3.up;
                    f = 6f;
                    if (float.IsNaN(f))
                        f = 5f;
                }
                entity.SetVelocity(aimDir * f * num1);
                if ((double)wep.tumbleVelocity > 0.0)
                    entity.SetAngularVelocity(new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * wep.tumbleVelocity);
                DudTimedExplosive dud = entity.GetComponent<DudTimedExplosive>();
                if (dud != null)
                    dud.dudChance = 0f;
                entity.Spawn();
                wep.StartAttackCooldown(wep.repeatDelay);
            }

            private void fireRocket(bool ducked = false)
            {
                ducked = false;
                Vector3 loc = zombieNpc.eyes.position + zombieNpc.eyes.HeadForward();
                if (ducked)
                    loc = zombieNpc.eyes.position + zombieNpc.eyes.HeadForward() + Vector3Down * 0.65f;
                string type = rocketTypes[_instance.configData.settings.RocketPrefabTypes.GetRandom()];
                var aim = zombieNpc.serverInput.current.aimAngles;
                var rocket = GameManager.server.CreateEntity(type, loc, zombieNpc.transform.rotation);
                if (rocket == null) return;
                var proj = rocket.GetComponent<ServerProjectile>();
                if (proj == null) return;
                rocket.creatorEntity = zombieNpc;
                proj.InitializeVelocity(Quaternion.Euler(aim) * rocket.transform.forward * 25f);
                rocket.Spawn();
            }

            private void removeweapons()
            {
                if (zombieNpc.IsDestroyed) return;
                Item itemC4 = null;
                zombieNpc.inventory.containerBelt.capacity = 6;
                Item items = zombieNpc.inventory.containerBelt.GetSlot(5);
                if (items != null) zombieNpc.inventory.Take(null, items.info.itemid, items.amount);
            }

            internal bool TargetInThrowableRange()
            {
                if (targetEntity == null)
                    return false;
                return hasWall();
            }

            internal bool hasWall()
            {
                // try this if not switch back
                // var ray = new Ray(Entity.eyes.position, new Vector3(-107.3504f, 12.1489f, -107.7641f));
                RaycastHit hit;
                if (Physics.Raycast(zombieNpc.transform.position, zombieNpc.transform.TransformDirection(Vector3.forward), out hit, 4.5f, targetLayer))
                {
                    return true;
                }

                return false;
            }

            public mynewclass(ZombieHorde.ZombieNPC zombieNpc) : base(AIState.Cooldown)
            {
                if (debug) _instance.PrintWarning("Added cooldown State");
                this.zombieNpc = zombieNpc;
                if (!isSetup) setup();
            }

            public bool setTarget(BaseCombatEntity newTarget)
            {
                targetEntity = newTarget;
                return true;
            }

            public void setup()
            {
                isSetup = true;
                if (_instance.configData.settings.totalExplosivesToUse <= 0)
                    totalBoom = 5;
                else totalBoom = _instance.configData.settings.totalExplosivesToUse;
            }
  
            public override bool CanInterrupt()
            {
                if (canLeave)
                    return true;

                if (targetEntity == null)
                    return true;

                return false;
            }

            public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
            {
                zombieNpc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                nextRaidFire = Time.time + 10;
                if (!_instance.raidingZombie.Contains(zombieNpc.net.ID.Value))
                    _instance.raidingZombie.Add(zombieNpc.net.ID.Value);
                base.StateEnter(brain, entity);
            }

            public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
            {
                if (_instance.raidingZombie.Contains(zombieNpc.net.ID.Value))
                    _instance.raidingZombie.Remove(zombieNpc.net.ID.Value);
                base.StateLeave(brain, entity);
            }

            public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
            {       
                base.StateThink(delta, brain, entity);

                if (zombieNpc.IsDormant)
                    zombieNpc.IsDormant = false;

                if (targetEntity == null)
                {
                    return StateStatus.Error;
                }

                if (totalBoom <= 0 && nextPositionUpdateTime < Time.time)
                {
                    targetEntity = null;
                    canLeave = true;
                    zombieNpc.ResetRoamState();
                    zombieNpc.Brain.states.Remove(AIState.Cooldown);
                    return StateStatus.Error;
                }
                else if (totalBoom > 0 && !isThrowingWeapon && nextRaidFire < Time.time)
                {
                    nextPositionUpdateTime = Time.time + 5f;
                    brain.Navigator.SetDestination(zombieNpc.transform.position, BaseNavigator.NavigationSpeed.Slow, 0.0f, 0f);
                    brain.Navigator.Stop();
                    zombieNpc.SetAimDirection((targetEntity.transform.position - zombieNpc.transform.position).normalized);
                    if (_instance.configData.settings.RocketPrefabTypes.Count <= 0)
                        if (!TargetInThrowableRange())
                            TryThrowBoom(false);
                    else if (_instance.configData.settings.ThrowExplosiveItemTypes.Count <= 0)
                            TryThrowBoom(true);
                    else _instance.PrintWarning("You do not have any explosive types set");

                    TryThrowBoom(!TargetInThrowableRange());
                }
                else if (Time.time > nextPositionUpdateTime)
                {
                    Vector3 position = brain.PathFinder.GetRandomPositionAround(targetEntity.transform.position, 2f, 20f);
                    brain.Navigator.SetDestination(position, BaseNavigator.NavigationSpeed.Fast, 0.1f, 0f);

                    nextPositionUpdateTime = Time.time + 3f;
                    unreachableLastUpdate = false;

                    return StateStatus.Running;
                }

                return StateStatus.Running;
            }
        }

        public class raidTrigger : MonoBehaviour
        {
            private ZombieHorde.ZombieNPC zombieNpc;

            private Dictionary<ZombieHorde.ZombieNPC, mynewclass> classFind = new Dictionary<ZombieHorde.ZombieNPC, mynewclass>();
            private readonly HashSet<BuildingPrivlidge> triggerEntitys = new HashSet<BuildingPrivlidge>();
            private Dictionary<BasePlayer, float> targetPlayers = new Dictionary<BasePlayer, float>();
            private bool Active { get; set; }

            public float collisionRadius;

            private void Awake()
            {
                zombieNpc = GetComponent<ZombieHorde.ZombieNPC>();
                collisionRadius = _instance.configData.settings.BaseScanDistance;
                InvokeRepeating("UpdateTriggerArea", UnityEngine.Random.Range(5, 9), UnityEngine.Random.Range(10, 15));
            }

            private void FindNewLeader(bool newLeaderEntity = true)
            {
                Active = true;
                if (newLeaderEntity)
                {
                    if (debug) _instance.PrintWarning("Setting leader of Zombies");
                    int total = _instance.configData.settings.TotalPerHorde;
                    mynewclass theClass = new mynewclass(zombieNpc.GetComponent<ZombieHorde.ZombieNPC>());
                    zombieNpc.gameObject.GetComponent<CustomScientistBrain>()?.AddState(theClass);
                    classFind.Add(zombieNpc, theClass);

                    foreach (ZombieHorde.ZombieNPC member in zombieNpc.Horde.members.ToList())
                    {
                        if (member != null && !member.IsDestroyed && !classFind.ContainsKey(member) && !member.IsGroupLeader)
                        {
                            theClass = new mynewclass(member.GetComponent<ZombieHorde.ZombieNPC>());
                            member.gameObject.GetComponent<CustomScientistBrain>()?.AddState(theClass);
                            classFind.Add(member, theClass);
                            total--;
                        }
                        if (total <= 1) break;
                    }
                }
                else
                {
                    if (debug) _instance.PrintWarning("Setting new leader of Zombies already spawned");
                    foreach (var member in classFind.ToList())
                    {
                        if (member.Key != null && !member.Key.IsDestroyed && member.Key != zombieNpc)
                        {
                            raidTrigger newLeader = member.Key.gameObject.AddComponent<raidTrigger>();
                            classFind.Remove(zombieNpc);
                            newLeader.classFind = classFind;
                            break;
                        }
                    }
                }
            }

            private void OnDestroy()
            {
                CancelInvoke("UpdateTriggerArea");
                FindNewLeader(false);
            }

            private void UpdateTriggerArea()
            {
                if (classFind.Count <= 0)
                {
                    FindNewLeader(true);
                    return;
                }
                if (zombieNpc == null)
                {
                    Destroy(this);
                    return;
                }
                if (zombieNpc.CurrentTarget != null && zombieNpc.CurrentTarget is BasePlayer)
                {
                    if (targetPlayers.ContainsKey((zombieNpc.CurrentTarget as BasePlayer)))
                    {
                        if (Time.time > targetPlayers[(zombieNpc.CurrentTarget as BasePlayer)] || (zombieNpc.CurrentTarget as BasePlayer).IsSleeping())
                        {
                            targetPlayers.Remove((zombieNpc.CurrentTarget as BasePlayer));
                        }
                    }
                    else if (!(zombieNpc.CurrentTarget as BasePlayer).IsSleeping()) targetPlayers.Add((zombieNpc.CurrentTarget as BasePlayer), Time.time + _instance.configData.settings.ForgetTargetTime);
                }

                if (_instance.configData.settings.targetPlayerOnly && targetPlayers.Count <= 0) return;
               
                var count = Physics.OverlapSphereNonAlloc(transform.position, collisionRadius, colBuffer, targetLayer);
                var collidePriv = new HashSet<BuildingPrivlidge>();
                for (int i = 0; i < count; i++)
                {
                    var collider = colBuffer[i];
                    colBuffer[i] = null;
                    var priv = collider.GetComponentInParent<BuildingPrivlidge>();
                    if (priv != null)
                    {
                        collidePriv.Add(priv);
                        if (triggerEntitys.Add(priv)) OnEnterCollision(priv);
                        continue;
                    }
                }

                var removePriv = new HashSet<BuildingPrivlidge>();
                foreach (BuildingPrivlidge player in triggerEntitys)
                    if (!collidePriv.Contains(player)) removePriv.Add(player);
                foreach (BuildingPrivlidge player in removePriv)
                {
                    triggerEntitys.Remove(player);
                }
            }

            private void OnEnterCollision(BuildingPrivlidge priv)
            {
                if (_instance.configData.settings.targetPlayerOnly)
                {
                    foreach (var player in targetPlayers)
                    {
                        if (player.Key == null || priv == null || !priv.IsAuthed(player.Key))
                        {
                            continue;
                        }
                        else
                        {
                            foreach (var raider in classFind.ToList())
                            {
                                if (raider.Key != null && !raider.Key.IsDestroyed && raider.Value != null)
                                {
                                    raider.Value.setTarget(priv);
                                    raider.Key.Brain.SwitchToState(AIState.Cooldown, 0);                                  
                                }
                                else
                                {
                                    classFind.Remove(raider.Key);
                                }
                            }
                            break;
                        }
                    }
                }
                else
                {
                    foreach (var raider in classFind.ToList())
                    {
                        if (raider.Key != null && !raider.Key.IsDestroyed && raider.Value != null)
                        {
                            raider.Value.setTarget(priv);
                            raider.Key.Brain.SwitchToState(AIState.Cooldown, 0);
                        }
                        else
                        {
                            classFind.Remove(raider.Key);
                        }
                    }
                }
                
            }
        }

        private object CanEntityTakeDamage(BuildingBlock entity, HitInfo hitInfo)
        {
            if (hitInfo?.Initiator is ZombieHorde.ZombieNPC)
            {
                if (configData.settings.DamageScale > 0 && hitInfo.WeaponPrefab != null && !hitInfo.WeaponPrefab.ShortPrefabName.Contains("beancan"))
                {
                    if (hitInfo.damageTypes.Get(DamageType.Explosion) > 0)
                        hitInfo.damageTypes.ScaleAll(configData.settings.DamageScale);
                }
                return true;
            }
            return null;
        }

        private void OnEntityTakeDamage(BaseCombatEntity baseCombatEntity, HitInfo hitInfo)
        {
            if (hitInfo != null && baseCombatEntity != null && configData.settings.DamageScale > 0f)
            {
                ZombieHorde.ZombieNPC hordeMember;

                if (hitInfo.InitiatorPlayer != null)
                {
                    hordeMember = hitInfo.InitiatorPlayer.GetComponent<ZombieHorde.ZombieNPC>();
                    if (hordeMember != null)
                    {
                        if (hitInfo.damageTypes.Get(DamageType.Explosion) > 0)
                        {
                            hitInfo.damageTypes.ScaleAll(configData.settings.DamageScale);
                            return;
                        }
                    }
                }
            }     
        }

    }
}