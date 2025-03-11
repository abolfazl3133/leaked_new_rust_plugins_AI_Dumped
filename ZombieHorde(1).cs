using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Oxide.Ext.ChaosNPC;
using Rust;
using Rust.Ai;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;
using UnityEngine.AI;

using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("ZombieHorde", "https://discord.gg/TrJ7jnS233", "0.6.18")]
    class ZombieHorde : RustPlugin, IChaosNPCPlugin
    {
        [PluginReference]
        private Plugin Kits, Spawns;

        private static ZombieHorde Instance { get; set; }


        public enum SpawnSystem { None, Random, SpawnsDatabase }

        public enum SpawnState { Spawn, Despawn }


        private static BaseNavigator.NavigationSpeed DefaultRoamSpeed;

        private const string ADMIN_PERMISSION = "zombiehorde.admin";

        private const string IGNORE_PERMISSION = "zombiehorde.ignore";

        #region Oxide Hooks       
        private void OnServerInitialized()
        {
            Instance = this;

            permission.RegisterPermission(ADMIN_PERMISSION, this);
            permission.RegisterPermission(IGNORE_PERMISSION, this);

            if (!Configuration.Member.TargetedByPeaceKeeperTurrets)
                Unsubscribe(nameof(CanEntityBeHostile));

            if (Configuration.Member.TargetedByAPC)
                Unsubscribe(nameof(CanBradleyApcTarget));
            
            if (!Configuration.Member.DespawnDudExplosives && !Configuration.Member.ExplodeDudExplosives)
                Unsubscribe(nameof(OnExplosiveDud));

            DefaultRoamSpeed = ParseType<BaseNavigator.NavigationSpeed>(Configuration.Horde.DefaultRoamSpeed);

            ValidateLoadoutProfiles();

            ValidateSpawnSystem();

            Horde.SpawnOrder.InitializeSpawnOrders();
            
            CreateMonumentHordeOrders();
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            ["Notification.BeginSpawn"] = "<color=#ce422b>CAUTION!</color> Zombies have been spotted in the area",
            ["Notification.BeginDespawn"] = "<color=#ce422b>CAUTION!</color> The zombies appear to be dispersing"
        }, this);

        private void OnEntityTakeDamage(BaseCombatEntity baseCombatEntity, HitInfo hitInfo)
        {
            ZombieNPC zombieNpc = hitInfo?.InitiatorPlayer as ZombieNPC;
            
            if (!zombieNpc || !baseCombatEntity || hitInfo == null) 
                return;

            bool hasExplosionDamage = hitInfo.damageTypes.Get(DamageType.Explosion) > 0;

            if (baseCombatEntity is BuildingBlock || baseCombatEntity is SimpleBuildingBlock || baseCombatEntity is Door)
            {
                if (hasExplosionDamage)
                { 
                    if (Configuration.Member.IgnoreBuildingMultiplierNotOwner || Configuration.Member.DisableBuildingMultiplierNotOwner)
                    {
                        BasePlayer target = zombieNpc.CurrentTarget as BasePlayer;
                        if (!target)
                            goto SCALE_DAMAGE;
                        
                        if (baseCombatEntity.OwnerID == target.userID)
                            goto SCALE_DAMAGE;
                        
                        BuildingPrivlidge buildingPrivilege = (baseCombatEntity as DecayEntity).GetBuildingPrivilege();
                        if (buildingPrivilege && buildingPrivilege.IsAuthed(target.userID))
                            goto SCALE_DAMAGE;
                        
                        if (Configuration.Member.DisableBuildingMultiplierNotOwner)
                            hitInfo.damageTypes.Scale(DamageType.Explosion, 0);

                        return;
                    }

                    SCALE_DAMAGE:
                    hitInfo.damageTypes.Scale(DamageType.Explosion, Configuration.Member.ExplosiveBuildingDamageMultiplier);
                    return;
                }

                AttackEntity attackEntity = zombieNpc.GetAttackEntity();

                if (attackEntity is BaseMelee)
                {
                    hitInfo.damageTypes.ScaleAll(Configuration.Member.MeleeBuildingDamageMultiplier);
                    return;
                }
            }

            if (hasExplosionDamage)
            { 
                if (baseCombatEntity is BasePlayer)
                {
                    if ((baseCombatEntity as BasePlayer).IsNpc)
                        goto APPLY_MULTIPLIER;
                    
                    hitInfo.damageTypes.ScaleAll(ConVar.Halloween.scarecrow_beancan_vs_player_dmg_modifier);
                    return;
                }
                
                hitInfo.damageTypes.ScaleAll(0);
                return;
            }

            APPLY_MULTIPLIER:
            
            float damageMultiplier = zombieNpc.Loadout.DamageMultiplier;
            
            if (!Mathf.Approximately(damageMultiplier, 1f))
                hitInfo.damageTypes.ScaleAll(damageMultiplier);
        }

        private object OnExplosiveDud(DudTimedExplosive dudTimedExplosive)
        {
            if (dudTimedExplosive.creatorEntity is ZombieNPC)
            {
                if (Configuration.Member.ExplodeDudExplosives)
                    return false;

                NextFrame(() =>
                {
                    if (dudTimedExplosive && !dudTimedExplosive.IsDestroyed)
                        dudTimedExplosive.KillMessage();
                });
            }

            return null;
        }
        
        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || hitInfo == null)
                return;

            if (player is ZombieNPC zombieNpc)
            {
                zombieNpc.Horde.OnMemberKilled(zombieNpc, hitInfo.Initiator);
                return;
            }

            if (Configuration.Horde.CreateOnDeath && hitInfo.InitiatorPlayer is ZombieNPC initiatorNpc)
                initiatorNpc.Horde.OnPlayerKilled(player);
        }

        private void OnEntityKill(ZombieNPC zombieNpc)
        {
            if (zombieNpc != null && zombieNpc.Horde is { isDespawning: false })
                zombieNpc.Horde.OnMemberKilled(zombieNpc, null);
        }

        private object CanBeTargeted(ZombieNPC zombieNpc, GunTrap gunTrap) => Configuration.Member.TargetedByTurrets ? null : (object)false;

        private object CanBeTargeted(ZombieNPC zombieNpc, FlameTurret flameTurret) => Configuration.Member.TargetedByTurrets ? null : (object)false;

        private object CanBeTargeted(ZombieNPC zombieNpc, AutoTurret autoTurret)
        {
            if (Configuration.Member.TargetedByTurrets && !(autoTurret is NPCAutoTurret))
                return null;

            if ((Configuration.Member.TargetedByPeaceKeeperTurrets || Configuration.Member.TargetedByTurrets) && autoTurret is NPCAutoTurret)
            {
                if (autoTurret.target == null)
                    autoTurret.SetTarget(zombieNpc);
                return null;
            }

            if (autoTurret.PeacekeeperMode() && Configuration.Member.TargetedByPeaceKeeperTurrets)
                return null;

            return false;
        }

        private object CanEntityBeHostile(ZombieNPC zombieNpc) => true;
        
        private object CanBradleyApcTarget(BradleyAPC bradleyApc, ZombieNPC zombieNpc) => false;
        
        private object OnNpcTarget(NPCPlayer npcPlayer, ZombieNPC zombieNpc) => Configuration.Member.TargetedByNPCs ? null : (object)true;

        private object OnNpcTarget(BaseNpc baseNpc, ZombieNPC zombieNpc) => Configuration.Member.TargetedByAnimals ? null : (object)true;


        private object OnEntityEnter(TriggerSafeZone triggerSafeZone, ZombieNPC zombieNpc)
        {
            if (triggerSafeZone && zombieNpc)
            {
                if (triggerSafeZone.contents != null)
                    triggerSafeZone.contents.Remove(zombieNpc.gameObject);

                zombieNpc.LeaveTrigger(triggerSafeZone);
                return true;
            }
            return null;
        }
        private void Unload()
        {
            Horde.SpawnOrder.OnUnload();

            for (int i = Horde.AllHordes.Count - 1; i >= 0; i--)
                Horde.AllHordes[i].Destroy(true, true);

            Horde.AllHordes.Clear();

            ZombieNPC[] zombies = UnityEngine.Object.FindObjectsOfType<ZombieNPC>();
            for (int i = 0; i < zombies?.Length; i++)
                zombies[i].Kill(BaseNetworkable.DestroyMode.None);

            Configuration = null;
            Instance = null;
        }
        #endregion

        #region Sensations  
        private void OnEntityKill(TimedExplosive timedExplosive)
        {
            if (!Configuration.Horde.UseSenses)
                return;

            Sense.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = timedExplosive.transform.position,
                Radius = 80f,
            });
        }

        private void OnEntityKill(TreeEntity treeEntity)
        {
            if (!Configuration.Horde.UseSenses)
                return;

            Sense.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = treeEntity.transform.position,
                Radius = 30f,
            });
        }

        private void OnEntityKill(OreResourceEntity oreResourceEntity)
        {
            if (!Configuration.Horde.UseSenses)
                return;

            Sense.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = oreResourceEntity.transform.position,
                Radius = 30f,
            });
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!Configuration.Horde.UseSenses)
                return;

            Sense.Stimulate(new Sensation()
            {
                Type = SensationType.Gunshot,
                Position = dispenser.transform.position,
                Radius = 20f
            });
        }
        #endregion

        #region Functions
        private T ParseType<T>(string type)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), type, true);
            }
            catch
            {
                return default(T);
            }
        }
        
        private static bool IsInOrOnBuilding(BaseEntity baseEntity)
        {
            if (!baseEntity || baseEntity.IsDestroyed)
                return false;
            
            const int CONSTRUCTION_LAYER = 1 << 21;
            Vector3 position = baseEntity.transform.position;

            if (Physics.Raycast(position, Vector3.up, out RaycastHit raycastHit, 40f, CONSTRUCTION_LAYER) || 
                Physics.Raycast(position, Vector3.down, out raycastHit, 20f, CONSTRUCTION_LAYER))
            {
                BaseEntity hitEntity = raycastHit.collider.ToBaseEntity();
                return hitEntity is BuildingBlock || hitEntity is SimpleBuildingBlock;
            }

            return false;
        }


        #region Horde Spawning
        private List<Vector3> _spawnPoints;

        private SpawnSystem _spawnSystem = SpawnSystem.None;

        private const int SPAWN_RAYCAST_MASK = 1 << 0 | 1 << 8 | 1 << 15 | 1 << 17 | 1 << 21 | 1 << 29;

        private const TerrainTopology.Enum SPAWN_TOPOLOGY_MASK = (TerrainTopology.Enum.Ocean | TerrainTopology.Enum.River | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Offshore | TerrainTopology.Enum.Summit | TerrainTopology.Enum.Decor | TerrainTopology.Enum.Monument);

        private static bool ContainsTopologyAtPoint(TerrainTopology.Enum mask, Vector3 position) => (TerrainMeta.TopologyMap.GetTopology(position, 1f) & (int)mask) != 0;

        private bool ValidateSpawnSystem()
        {
            _spawnSystem = ParseType<SpawnSystem>(Configuration.Horde.SpawnType);

            if (_spawnSystem == SpawnSystem.None)
            {
                PrintError("You have set an invalid value in the config entry \"Spawn Type\". Unable to spawn hordes!");
                return false;
            }
            
            if (_spawnSystem == SpawnSystem.SpawnsDatabase)
            {
                if (Spawns != null)
                {
                    if (string.IsNullOrEmpty(Configuration.Horde.SpawnFile))
                    {
                        PrintError("You have selected SpawnsDatabase as your method of spawning hordes, however you have not specified a spawn file. Unable to spawn hordes!");
                        return false;
                    }

                    object success = Spawns?.Call("LoadSpawnFile", Configuration.Horde.SpawnFile);
                    if (success is List<Vector3>)
                    {
                        _spawnPoints = success as List<Vector3>;
                        if (_spawnPoints.Count > 0)
                            return true;
                    }
                    PrintError("You have selected SpawnsDatabase as your method of spawning hordes, however the spawn file you have chosen is either invalid, or has no spawn points. Unable to spawn hordes!");
                    return false;
                }

                PrintError("You have selected SpawnsDatabase as your method of spawning hordes, however SpawnsDatabase is not loaded on your server. Unable to spawn hordes!");
                return false;
            }

            return true;
        }

        private Vector3 GetSpawnPoint()
        {
            switch (_spawnSystem)
            {
                case SpawnSystem.None:
                    break;

                case SpawnSystem.SpawnsDatabase:
                    {
                        if (Spawns == null)
                        {
                            PrintError("Tried getting a spawn point but SpawnsDatabase is null. Make sure SpawnsDatabase is still loaded to continue using custom spawn points");
                            break;
                        }

                        if (_spawnPoints == null || _spawnPoints.Count == 0)
                        {
                            PrintError("No spawnpoints have been loaded from the designated spawnfile. Defaulting to Rust spawns");
                            break;
                        }

                        Vector3 spawnPoint = _spawnPoints.GetRandom();
                        _spawnPoints.Remove(spawnPoint);
                        if (_spawnPoints.Count == 0)
                            _spawnPoints = (List<Vector3>)Spawns.Call("LoadSpawnFile", Configuration.Horde.SpawnFile);

                        return spawnPoint;
                    }
            }

            float size = (World.Size / 2f) * 0.75f;

            for (int i = 0; i < 10; i++)
            {
                Vector2 randomInCircle = Random.insideUnitCircle * size;

                Vector3 position = new Vector3(randomInCircle.x, 0, randomInCircle.y);
                position.y = TerrainMeta.HeightMap.GetHeight(position);

                if (NavmeshSpawnPoint.Find(position, 25f, out position))
                {
                    if (Physics.SphereCast(new Ray(position + (Vector3.up * 5f), Vector3.down), 10f, 10f, SPAWN_RAYCAST_MASK))
                        continue;

                    if (ContainsTopologyAtPoint(SPAWN_TOPOLOGY_MASK, position))
                        continue;

                    if (WaterLevel.GetWaterDepth(position, true, false, null) <= 0.01f)
                        return position;
                }
            }

            return ServerMgr.FindSpawnPoint().pos;
        }

        private void CreateMonumentHordeOrders()
        {
            int count = 0;
            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (GameObject gobject in allObjects)
            {
                if (count >= Configuration.Horde.MaximumHordes)
                    break;

                if (gobject.name.Contains("autospawn/monument"))
                {
                    Transform tr = gobject.transform;
                    Vector3 position = tr.position;

                    if (position == Vector3.zero)
                        continue;

                    if (gobject.name.Contains("powerplant_1") && Configuration.Monument.Powerplant.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-30.8f, 0.2f, -15.8f)), Configuration.Monument.Powerplant);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("military_tunnel_1") && Configuration.Monument.Tunnels.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-7.4f, 13.4f, 53.8f)), Configuration.Monument.Tunnels);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("harbor_1") && Configuration.Monument.LargeHarbor.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(54.7f, 5.1f, -39.6f)), Configuration.Monument.LargeHarbor);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("harbor_2") && Configuration.Monument.SmallHarbor.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-66.6f, 4.9f, 16.2f)), Configuration.Monument.SmallHarbor);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("airfield_1") && Configuration.Monument.Airfield.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-12.4f, 0.2f, -28.9f)), Configuration.Monument.Airfield);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("trainyard_1") && Configuration.Monument.Trainyard.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(35.8f, 0.2f, -0.8f)), Configuration.Monument.Trainyard);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1") && Configuration.Monument.WaterTreatment.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(11.1f, 0.3f, -80.2f)), Configuration.Monument.WaterTreatment);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("warehouse") && Configuration.Monument.Warehouse.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(16.6f, 0.1f, -7.5f)), Configuration.Monument.Warehouse);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("satellite_dish") && Configuration.Monument.Satellite.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(18.6f, 6.0f, -7.5f)), Configuration.Monument.Satellite);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("sphere_tank") && Configuration.Monument.Dome.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-44.6f, 5.8f, -3.0f)), Configuration.Monument.Dome);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("radtown_small_3") && Configuration.Monument.Radtown.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-16.3f, -2.1f, -3.3f)), Configuration.Monument.Radtown);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("launch_site_1") && Configuration.Monument.LaunchSite.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(222.1f, 3.3f, 0.0f)), Configuration.Monument.LaunchSite);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("gas_station_1") && Configuration.Monument.GasStation.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-9.8f, 3.0f, 7.2f)), Configuration.Monument.GasStation);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("supermarket_1") && Configuration.Monument.Supermarket.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(5.5f, 0.0f, -20.5f)), Configuration.Monument.Supermarket);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_c") && Configuration.Monument.HQMQuarry.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(15.8f, 4.5f, -1.5f)), Configuration.Monument.HQMQuarry);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_a") && Configuration.Monument.SulfurQuarry.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-0.8f, 0.6f, 11.4f)), Configuration.Monument.SulfurQuarry);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("mining_quarry_b") && Configuration.Monument.StoneQuarry.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-7.6f, 0.2f, 12.3f)), Configuration.Monument.StoneQuarry);
                        count++;
                        continue;
                    }

                    if (gobject.name.Contains("junkyard_1") && Configuration.Monument.Junkyard.Enabled)
                    {
                        Horde.SpawnOrder.Create(tr.TransformPoint(new Vector3(-16.7f, 0.2f, 1.4f)), Configuration.Monument.Junkyard);
                        count++;
                        continue;
                    }
                }
            }

            foreach (ConfigData.MonumentSpawn.CustomSpawnPoints customSpawnPoint in Configuration.Monument.Custom)
            {
                if (customSpawnPoint.Enabled && customSpawnPoint.Location.IsValid)
                {
                    Horde.SpawnOrder.Create(customSpawnPoint.Location, Configuration.Horde.InitialMemberCount, customSpawnPoint.HordeSize, customSpawnPoint.RoamDistance, customSpawnPoint.Profile);
                    count++;
                }
            }

            if (count < Configuration.Horde.MaximumHordes)
                CreateRandomHordes();
        }

        private void CreateRandomHordes()
        {
            int amountToCreate = Configuration.Horde.MaximumHordes - Horde.AllHordes.Count;
            for (int i = 0; i < amountToCreate; i++)
            {
                float roamDistance = Configuration.Horde.LocalRoam ? Configuration.Horde.RoamDistance : -1;
                string profile = Configuration.Horde.UseProfiles ? Configuration.HordeProfiles.Keys.ToArray().GetRandom() : string.Empty;

                Horde.SpawnOrder.Create(GetSpawnPoint(), Configuration.Horde.InitialMemberCount, Configuration.Horde.MaximumMemberCount, roamDistance, profile);
            }
        }
        #endregion

        #region Chaos NPC
        public bool InitializeStates(BaseAIBrain customNpcBrain)
        {
            customNpcBrain.AddState(new BaseAIBrain.BaseIdleState());
            customNpcBrain.AddState(new ZombieNPCBrain.ZombieChaseState());
            customNpcBrain.AddState(new ZombieNPCBrain.ZombieAttackState());
            customNpcBrain.AddState(new ZombieNPCBrain.ZombieRoamState());
            customNpcBrain.AddState(new ZombieNPCBrain.ZombieMountedState());
            
            return true;
        }

        public bool WantsToPopulateLoot(CustomScientistNPC customNpc, NPCPlayerCorpse npcplayerCorpse)
        {
            if (Configuration.Loot.DropDefault)
                return false;
            
            int count = Random.Range(Configuration.Loot.Random.Minimum, Configuration.Loot.Random.Maximum);

            int spawnedCount = 0;
            int loopCount = 0;

            while (true)
            {
                loopCount++;

                if (loopCount > 3)
                    goto EndLootSpawn;

                float probability = Random.Range(0f, 1f);

                List<ConfigData.LootTable.RandomLoot.LootDefinition> definitions = new List<ConfigData.LootTable.RandomLoot.LootDefinition>(Configuration.Loot.Random.List);

                for (int i = 0; i < Configuration.Loot.Random.List.Count; i++)
                {
                    ConfigData.LootTable.RandomLoot.LootDefinition lootDefinition = definitions.GetRandom();

                    definitions.Remove(lootDefinition);

                    if (lootDefinition.Probability >= probability)
                    {
                        lootDefinition.Create(npcplayerCorpse.containers[0]);

                        spawnedCount++;

                        if (spawnedCount >= count)
                            goto EndLootSpawn;
                    }
                }
            }

            EndLootSpawn:
            return true;
        }

        private bool ChaosNPC_PreSpawn(ZombieNPC zombieNpc) => false;

        public byte[] GetCustomDesign() => new byte[] {  8, 1, 8, 3, 8, 15, 8, 2, 8, 6, 18, 75, 8, 0, 16, 1, 26, 12, 8, 17, 16, 4, 24, 0, 32, 4, 40, 3, 48, 0, 26, 12, 8, 14, 16, 1, 24, 0, 32, 0, 40, 0, 48, 1, 26, 25, 8, 0, 16, 3, 24, 0, 32, 0, 40, 0, 48, 2, 162, 6, 10, 13, 0, 0, 0, 0, 21, 0, 0, 0, 0, 26, 12, 8, 3, 16, 1, 24, 0, 32, 4, 40, 0, 48, 3, 32, 0, 18, 158, 1, 8, 1, 16, 3, 26, 12, 8, 17, 16, 4, 24, 0, 32, 4, 40, 3, 48, 0, 26, 12, 8, 2, 16, 0, 24, 0, 32, 0, 40, 0, 48, 1, 26, 12, 8, 4, 16, 0, 24, 0, 32, 0, 40, 0, 48, 2, 26, 12, 8, 5, 16, 2, 24, 0, 32, 0, 40, 0, 48, 3, 26, 12, 8, 18, 16, 0, 24, 0, 32, 0, 40, 0, 48, 4, 26, 12, 8, 15, 16, 2, 24, 0, 32, 0, 40, 0, 48, 5, 26, 29, 8, 12, 16, 255, 255, 255, 255, 255, 255, 255, 255, 255, 1, 24, 0, 32, 0, 40, 0, 48, 6, 218, 6, 5, 13, 0, 0, 160, 65, 26, 21, 8, 14, 16, 255, 255, 255, 255, 255, 255, 255, 255, 255, 1, 24, 0, 32, 0, 40, 0, 48, 7, 26, 12, 8, 20, 16, 0, 24, 0, 32, 0, 40, 0, 48, 8, 32, 0, 18, 113, 8, 2, 16, 15, 26, 12, 8, 17, 16, 4, 24, 0, 32, 4, 40, 3, 48, 0, 26, 12, 8, 4, 16, 0, 24, 0, 32, 0, 40, 0, 48, 1, 26, 12, 8, 2, 16, 0, 24, 0, 32, 0, 40, 0, 48, 2, 26, 12, 8, 20, 16, 0, 24, 0, 32, 0, 40, 0, 48, 3, 26, 12, 8, 5, 16, 1, 24, 1, 32, 0, 40, 0, 48, 4, 26, 12, 8, 15, 16, 1, 24, 1, 32, 0, 40, 0, 48, 5, 26, 21, 8, 14, 16, 255, 255, 255, 255, 255, 255, 255, 255, 255, 1, 24, 0, 32, 0, 40, 0, 48, 6, 32, 0, 18, 62, 8, 3, 16, 2, 26, 12, 8, 4, 16, 0, 24, 0, 32, 0, 40, 0, 48, 0, 26, 12, 8, 2, 16, 0, 24, 0, 32, 0, 40, 0, 48, 1, 26, 12, 8, 14, 16, 1, 24, 0, 32, 0, 40, 0, 48, 2, 26, 12, 8, 3, 16, 1, 24, 0, 32, 4, 40, 0, 48, 3, 32, 0, 18, 48, 8, 4, 16, 6, 26, 12, 8, 17, 16, 0, 24, 1, 32, 4, 40, 3, 48, 0, 26, 12, 8, 2, 16, 0, 24, 0, 32, 0, 40, 0, 48, 1, 26, 12, 8, 4, 16, 0, 24, 0, 32, 0, 40, 0, 48, 2, 32, 0, 24, 0, 34, 13, 90, 111, 109, 98, 105, 101, 32, 68, 101, 115, 105, 103, 110, 40, 0, 48, 0 };
        #endregion

        #region Loadouts  
        private static Definitions _zombieDefinition;

        private static Definitions ZombieDefinition
        {
            get
            {
                if (_zombieDefinition == null)
                {
                    ScarecrowNPC scarecrowNpc = GameManager.server.FindPrefab("assets/prefabs/npc/scarecrow/scarecrow.prefab").GetComponent<ScarecrowNPC>();

                    _zombieDefinition = new Definitions
                    {
                        Loadouts = scarecrowNpc.loadouts,
                        LootSpawns = scarecrowNpc.LootSpawnSlots
                    };
                }

                return _zombieDefinition;
            }
        }

        private void ValidateLoadoutProfiles()
        {
            Puts("Validating horde profiles...");

            bool hasChanged = false;

            for (int i = Configuration.HordeProfiles.Count - 1; i >= 0; i--)
            {
                string key = Configuration.HordeProfiles.ElementAt(i).Key;

                for (int y = Configuration.HordeProfiles[key].Count - 1; y >= 0; y--)
                {
                    string loadoutId = Configuration.HordeProfiles[key][y];

                    if (Configuration.Member.Loadouts.All(x => x.LoadoutID != loadoutId))
                    {
                        Puts($"Loadout profile {loadoutId} does not exist. Removing from config");
                        Configuration.HordeProfiles[key].Remove(loadoutId);
                        hasChanged = true;
                    }
                }

                if (Configuration.HordeProfiles[key].Count <= 0)
                {
                    Puts($"Horde profile {key} does not have any valid loadouts. Removing from config");
                    Configuration.HordeProfiles.Remove(key);
                    hasChanged = true;
                }
            }

            if (hasChanged)
                SaveConfig();
        }
        #endregion

        #endregion

        public class Horde
        {
            public static List<Horde> AllHordes = new List<Horde>();

            private static readonly Spatial.Grid<Horde> HordeGrid = new Spatial.Grid<Horde>(32, 8096f);

            private static readonly Horde[] HordeGridQueryResults = new Horde[4];

            private static readonly BasePlayer[] PlayerVicinityQueryResults = new BasePlayer[32];

            public List<ZombieNPC> members;

            public readonly Vector3 InitialPosition;
            public readonly bool IsLocalHorde;
            public readonly float MaximumRoamDistance;

            private readonly int initialMemberCount;
            private readonly int maximumMemberCount;

            private readonly string hordeProfile;

            private float nextUpdateTime;
            private float nextSeperationCheckTime;
            private float nextGrowthTime;
            private float nextMergeTime;
            private float nextSleepTime;

            internal bool isDespawning;

            private const float HORDE_UPDATE_RATE = 1f;
            private const float SEPERATION_CHECK_RATE = 10f;
            private const float MERGE_CHECK_RATE = 10f;
            private const float SLEEP_CHECK_RATE = 5f;

            public ZombieNPC Leader { get; private set; }

            public bool IsSleeping { get; private set; }

            public Vector3 CentralLocation { get; private set; }

            public bool HordeOnAlert { get; private set; }
            
            public float NextThrownWeaponTime { get; set; }

            public int MemberCount => members.Count;

            public static bool Create(SpawnOrder spawnOrder)
            {
                Horde horde = new Horde(spawnOrder);

                for (int i = 0; i < spawnOrder.InitialMemberCount; i++)
                    horde.SpawnMember(spawnOrder.Position);

                if (horde.members.Count == 0)
                {
                    horde.Destroy();
                    return false;
                }

                AllHordes.Add(horde);

                horde.CentralLocation = horde.CalculateCentralLocation();

                HordeGrid.Add(horde, horde.CentralLocation.x, horde.CentralLocation.z);

                return true;
            }

            public Horde(SpawnOrder spawnOrder)
            {
                members = Pool.GetList<ZombieNPC>();

                InitialPosition = CentralLocation = spawnOrder.Position;
                IsLocalHorde = spawnOrder.MaximumRoamDistance > 0;
                MaximumRoamDistance = spawnOrder.MaximumRoamDistance;
                initialMemberCount = spawnOrder.InitialMemberCount;
                maximumMemberCount = spawnOrder.MaximumMemberCount;
                hordeProfile = spawnOrder.HordeProfile;

                nextSeperationCheckTime = Time.time + SEPERATION_CHECK_RATE;
                nextGrowthTime = Time.time + Configuration.Horde.GrowthRate;
                nextMergeTime = Time.time + MERGE_CHECK_RATE;
                nextSleepTime = Time.time + SLEEP_CHECK_RATE + Random.Range(1f, 5f);
            }

            public void Update()
            {
                if (members == null || members.Count == 0)
                    return;

                if (Time.time < nextUpdateTime)
                    return;

                nextUpdateTime = Time.time + HORDE_UPDATE_RATE;

                CentralLocation = CalculateCentralLocation();

                if (Configuration.Member.EnableDormantSystem)
                    DoSleepChecks();

                if (IsSleeping)
                    return;

                HordeGrid.Move(this, CentralLocation.x, CentralLocation.z);

                TryMergeHordes();

                TryGrowHorde();

                TryCongregateHorde();

                MoveRoamersTowardsTarget();
            }

            private void MoveRoamersTowardsTarget()
            {
                HordeOnAlert = AnyHasTarget(out BaseEntity target);

                if (Leader && !Leader.IsDestroyed && target && !target.IsDestroyed)
                    SetLeaderRoamTarget(target.transform.position);
            }

            private void TryCongregateHorde()
            {
                if (Time.time > nextSeperationCheckTime)
                {
                    nextSeperationCheckTime = Time.time + SEPERATION_CHECK_RATE;

                    if (GetLargestSeperation() > 30f)
                    {
                        if (Leader.CurrentState <= AIState.Roam)
                            SetLeaderRoamTarget(CentralLocation);
                    }
                }
            }

            public void RegisterInterestInTarget(ZombieNPC interestedMember, BaseEntity baseEntity, bool force)
            {
                if (baseEntity == null || members == null)
                    return;

                for (int i = 0; i < members.Count; i++)
                {
                    ZombieNPC hordeMember = members[i];
                    if (!hordeMember || hordeMember.IsDestroyed || interestedMember == hordeMember)
                        continue;

                    hordeMember.Brain.Senses.Memory.SetKnown(baseEntity, hordeMember, null);
                    
                    if (force)
                    {
                        hordeMember.Brain.Events.Memory.Entity.Set(baseEntity, hordeMember.Brain.Events.CurrentInputMemorySlot);
                        hordeMember.Brain.SwitchToState(AIState.Chase, 1);
                    }
                }

                if (!force && Leader && !Leader.IsDestroyed && !Leader.HasTarget)
                    SetLeaderRoamTarget(baseEntity.transform.position);
            }

            public bool HasTarget()
            {
                if (members != null)
                {
                    for (int i = 0; i < members.Count; i++)
                    {
                        ZombieNPC hordeMember = members[i];
                        if (hordeMember.HasTarget)
                            return true;
                    }
                }
                return false;
            }

            public bool HasHumanTarget()
            {
                if (members != null)
                {
                    for (int i = 0; i < members.Count; i++)
                    {
                        ZombieNPC hordeMember = members[i];
                        if (hordeMember.HasHumanTargetInRange)
                            return true;
                    }
                }
                return false;
            }

            public bool AnyHasTarget(out BaseEntity target)
            {
                if (members != null)
                {
                    for (int i = 0; i < members.Count; i++)
                    {
                        ZombieNPC hordeMember = members[i];
                        if (hordeMember.HasTarget)
                        {
                            target = hordeMember.CurrentTarget;
                            return true;
                        }
                    }
                }
                target = null;
                return false;
            }

            public void ResetRoamTarget()
            {
                if (members == null)
                    return;

                for (int i = 0; i < members.Count; i++)
                {
                    ZombieNPC hordeMember = members[i];
                    if (hordeMember.IsGroupLeader)
                        continue;

                    hordeMember.ResetRoamState();
                }
            }

            public void SetLeaderRoamTarget(Vector3 position)
            {
                if (Leader != null && !Leader.IsDestroyed && !Leader.RecentlySetDestination)
                {
                    Leader.SetRoamTargetOverride(position);
                    ResetRoamTarget();
                }
            }

            public Vector3 GetLeaderDestination()
            {
                if (Leader == null || Leader.IsDestroyed || Leader.Brain == null || Leader.Brain.Events == null)
                    return CentralLocation;

                return Leader.Brain.Navigator.Destination;
            }

            public void OnMemberKilled(ZombieNPC zombieNpc, BaseEntity initiator)
            {
                if (zombieNpc == null)
                    return;

                if (members == null || !members.Contains(zombieNpc))
                    return;

                members.Remove(zombieNpc);

                if (members.Count == 0)
                    Destroy();
                else
                {
                    if (zombieNpc.IsGroupLeader)
                    {
                        Leader = members.GetRandom();
                        Leader.IsGroupLeader = true;
                    }

                    if ((initiator is BasePlayer && Leader.CanTargetBasePlayer(initiator as BasePlayer)) || (initiator is BaseNpc && Leader.CanTargetEntity(initiator)))
                        RegisterInterestInTarget(null, initiator, zombieNpc.isHidingInside || zombieNpc.unreachableLastFrame);
                }
            }

            public void OnPlayerKilled(BasePlayer player)
            {
                if (Configuration.Horde.CreateOnDeath && MemberCount < maximumMemberCount)
                {
                    if (NavmeshSpawnPoint.Find(player.transform.position, 10f, out Vector3 position))
                        SpawnMember(position);
                }
            }

            public void SpawnMember(Vector3 position)
            {
                ConfigData.MemberOptions.Loadout loadout = null;

                if (!string.IsNullOrEmpty(hordeProfile) && Configuration.HordeProfiles.ContainsKey(hordeProfile))
                {
                    string loadoutId = Configuration.HordeProfiles[hordeProfile].GetRandom();
                    loadout = Configuration.Member.Loadouts.FirstOrDefault(x => x.LoadoutID == loadoutId);
                }

                if (loadout == null)
                    loadout = Configuration.Member.Loadouts.GetRandom();

                ZombieNPC zombieNpc = ChaosNPC.SpawnNPC<ZombieNPC, ZombieNPCBrain>(Instance, position, loadout.NPCSettings);
                if (!zombieNpc)
                    return;

                zombieNpc.Loadout = loadout;
                zombieNpc.Horde = this;

                zombieNpc.gameObject.AwakeFromInstantiate();
                zombieNpc.Spawn();

                members.Add(zombieNpc);

                if (members.Count == 1)
                {
                    Leader = zombieNpc;
                    Leader.IsGroupLeader = true;
                }
                else zombieNpc.Invoke(zombieNpc.OnInitialSpawn, 1f);
            }

            public void Destroy(bool permanent = false, bool killNpcs = true)
            {
                isDespawning = true;

                if (killNpcs)
                {
                    for (int i = members.Count - 1; i >= 0; i--)
                    {
                        ZombieNPC zombieNpc = members[i];
                        if (zombieNpc && !zombieNpc.IsDestroyed)
                            zombieNpc.Kill();
                    }
                }

                members.Clear();
                Pool.FreeList(ref members);

                HordeGrid.Remove(this);

                AllHordes.Remove(this);

                if (!permanent && AllHordes.Count <= Configuration.Horde.MaximumHordes)
                    Instance.timer.In(Configuration.Horde.RespawnTime, () => SpawnOrder.Create(this));
            }

            private Vector3 CalculateCentralLocation()
            {
                Vector3 location = Vector3.zero;

                if (members == null || members.Count == 0)
                    return location;

                int count = 0;
                for (int i = 0; i < members.Count; i++)
                {
                    ZombieNPC zombieNpc = members[i];

                    if (zombieNpc == null || zombieNpc.IsDestroyed)
                        continue;

                    location += zombieNpc.Transform.position;
                    count++;
                }

                return location /= count;
            }

            private float GetLargestSeperation()
            {
                float distance = 0;

                if (members != null)
                {
                    for (int i = 0; i < members.Count; i++)
                    {
                        ZombieNPC zombieNpc = members[i];
                        if (zombieNpc != null && !zombieNpc.IsDestroyed)
                        {
                            float d = Vector3.Distance(zombieNpc.Transform.position, CentralLocation);
                            if (d > distance)
                                distance = d;
                        }
                    }
                }

                return distance;
            }

            private void TryGrowHorde()
            {
                if (Configuration.Horde.GrowthRate <= 0 || nextGrowthTime < Time.time)
                {
                    if (MemberCount < maximumMemberCount)
                        SpawnMember(members.GetRandom().Transform.position);

                    nextGrowthTime = Time.time + Configuration.Horde.GrowthRate;
                }
            }

            #region Horde Merging
            private static bool HordeMergeQuery(Horde horde) => horde.MemberCount < horde.maximumMemberCount;

            private void TryMergeHordes()
            {
                if (!Configuration.Horde.MergeHordes || nextMergeTime > Time.time)
                    return;

                nextMergeTime = Time.time + MERGE_CHECK_RATE;

                if (members == null || MemberCount >= maximumMemberCount)
                    return;

                int results = HordeGrid.Query(CentralLocation.x, CentralLocation.z, 30f, HordeGridQueryResults, HordeMergeQuery);

                if (results > 1)
                {
                    int amountToMerge = maximumMemberCount - members.Count;

                    for (int i = 0; i < results; i++)
                    {
                        Horde otherHorde = HordeGridQueryResults[i];

                        if (otherHorde == this)
                            continue;

                        if (MemberCount >= maximumMemberCount || otherHorde.members == null)
                            break;

                        if (amountToMerge >= otherHorde.members.Count)
                        {
                            for (int y = 0; y < otherHorde.members.Count; y++)
                            {
                                ZombieNPC zombieNpc = otherHorde.members[y];
                                members.Add(zombieNpc);
                                zombieNpc.Horde = this;
                                zombieNpc.IsGroupLeader = false;
                                zombieNpc.OnInitialSpawn();
                            }

                            otherHorde.members.Clear();
                            otherHorde.Destroy();
                        }
                        else
                        {
                            for (int y = 0; y < amountToMerge; y++)
                            {
                                if (otherHorde.members.Count > 0)
                                {
                                    ZombieNPC zombieNpc = otherHorde.members[otherHorde.MemberCount - 1];

                                    members.Add(zombieNpc);

                                    zombieNpc.Horde = this;
                                    zombieNpc.IsGroupLeader = false;
                                    zombieNpc.OnInitialSpawn();

                                    otherHorde.members.Remove(zombieNpc);
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Sleeping
            private void DoSleepChecks()
            {
                if (Time.time >= nextSleepTime)
                {
                    nextSleepTime = Time.time + SLEEP_CHECK_RATE + Random.Range(1f, 5f);

                    int count = BaseEntity.Query.Server.GetPlayersInSphere(CentralLocation, AiManager.ai_to_player_distance_wakeup_range, PlayerVicinityQueryResults, HordeSleepPlayerFilter);

                    if (count > 0)
                    {
                        if (IsSleeping)
                            SetSleeping(false);
                    }
                    else
                    {
                        if (!IsSleeping)
                            SetSleeping(true);
                    }
                }
            }

            private void SetSleeping(bool sleep)
            {
                if (members == null)
                    return;

                for (int i = 0; i < members.Count; i++)
                {
                    ZombieNPC zombieNpc = members[i];
                    if (!zombieNpc || !zombieNpc.Brain)
                        continue;

                    if (zombieNpc.Brain.sleeping == sleep)
                        continue;

                    ((ZombieNPCBrain)zombieNpc.Brain).SetSleeping(sleep);
                }

                IsSleeping = sleep;
            }

            public void ForceWakeFromSleep()
            {
                if (!IsSleeping)
                    return;

                SetSleeping(false);
            }

            private static bool HordeSleepPlayerFilter(BaseEntity entity)
            {
                BasePlayer basePlayer = entity as BasePlayer;
                if (!basePlayer || !basePlayer.IsConnected)
                    return false;

                if (basePlayer is ZombieNPC)
                    return false;

                if (Configuration.Member.IgnoreSleepers && basePlayer.IsSleeping())
                    return false;

                return true;
            }
            #endregion

            public int GetMemberIndex(ZombieNPC zombieNpc) => members.IndexOf(zombieNpc);

            public class SpawnOrder
            {
                public Vector3 Position { get; private set; }

                public int InitialMemberCount { get; private set; }

                public int MaximumMemberCount { get; private set; }

                public float MaximumRoamDistance { get; private set; }

                public string HordeProfile { get; private set; }

                public SpawnOrder(Vector3 position, int initialMemberCount, int maximumMemberCount, float maximumRoamDistance, string hordeProfile)
                {
                    this.Position = position;
                    this.InitialMemberCount = initialMemberCount;
                    this.MaximumMemberCount = maximumMemberCount;
                    this.MaximumRoamDistance = maximumRoamDistance;
                    this.HordeProfile = hordeProfile;
                }

                #region Static
                private static Queue<SpawnOrder> _spawnOrders = new Queue<SpawnOrder>();

                private static Coroutine _spawnRoutine;

                private static Coroutine _despawnRoutine;

                private static bool _isSpawning;

                private static bool _isDespawning;

                public static SpawnState State;

                public static void InitializeSpawnOrders()
                {
                    State = Configuration.TimedSpawns.Enabled ? (ShouldSpawn() ? SpawnState.Spawn : SpawnState.Despawn) : SpawnState.Spawn;

                    if (Configuration.TimedSpawns.Enabled)
                        StartTimer();
                }
                
                internal static void Create(Vector3 position, int initialMemberCount, int maximumMemberCount, float maximumRoamDistance, string hordeProfile)
                {
                    if (NavmeshSpawnPoint.Find(position, 10f, out position))
                    {
                        _spawnOrders.Enqueue(new SpawnOrder(position, initialMemberCount, maximumMemberCount, maximumRoamDistance, hordeProfile));

                        if (!_isSpawning && State == SpawnState.Spawn)
                            DequeueAndSpawn();
                    }
                }

                internal static void Create(Horde horde)
                {
                    if (NavmeshSpawnPoint.Find(horde.IsLocalHorde ? horde.InitialPosition : Instance.GetSpawnPoint(), 10f, out Vector3 position))
                    {
                        _spawnOrders.Enqueue(new SpawnOrder(position, horde.initialMemberCount, horde.maximumMemberCount, horde.IsLocalHorde ? horde.MaximumRoamDistance : -1, horde.hordeProfile));

                        if (!_isSpawning && State == SpawnState.Spawn)
                            DequeueAndSpawn();
                    }
                }

                internal static void Create(Vector3 position, ConfigData.MonumentSpawn.MonumentSettings settings)
                {
                    if (NavmeshSpawnPoint.Find(position, 10f, out position))
                    {
                        _spawnOrders.Enqueue(new SpawnOrder(position, Configuration.Horde.InitialMemberCount, settings.HordeSize, settings.RoamDistance, settings.Profile));

                        if (!_isSpawning && State == SpawnState.Spawn)
                            DequeueAndSpawn();
                    }
                }

                private static void DequeueAndSpawn()
                {
                    if (_spawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_spawnRoutine);
                    
                    _spawnRoutine = ServerMgr.Instance.StartCoroutine(ProcessSpawnOrders());
                }

                private static void QueueAndDespawn()
                {
                    if (_despawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_despawnRoutine);
                    
                    _despawnRoutine = ServerMgr.Instance.StartCoroutine(ProcessDespawn());
                }

                private static void StopSpawning()
                {
                    if (_spawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_spawnRoutine);

                    _isSpawning = false;
                }

                private static void StopDespawning()
                {
                    if (_despawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_despawnRoutine);

                    _isDespawning = false;
                }

                private static IEnumerator ProcessSpawnOrders()
                {
                    if (_spawnOrders.Count == 0)
                        yield break;

                    _isSpawning = true;

                    RESTART:
                    if (_isDespawning)
                        StopDespawning();

                    while (AllHordes.Count > Configuration.Horde.MaximumHordes)
                        yield return CoroutineEx.waitForSeconds(10f);

                    SpawnOrder spawnOrder = _spawnOrders.Dequeue();

                    if (spawnOrder != null)
                        Horde.Create(spawnOrder);

                    if (_spawnOrders.Count > 0)
                    {
                        yield return CoroutineEx.waitForSeconds(3f);
                        goto RESTART;
                    }

                    _spawnRoutine = null;
                    _isSpawning = false;
                }

                private static IEnumerator ProcessDespawn()
                {
                    _isDespawning = true;

                    if (_isSpawning)
                        StopSpawning();

                    while (AllHordes.Count > 0)
                    {
                        Horde horde = AllHordes.GetRandom();
                        if (!horde.HasHumanTarget())
                        {
                            Create(horde);
                            horde.Destroy(true, true);
                        }

                        yield return CoroutineEx.waitForSeconds(3f);
                    }

                    _despawnRoutine = null;
                    _isDespawning = false;
                }

                internal static void OnUnload()
                {
                    if (_spawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_spawnRoutine);
                    
                    if (_despawnRoutine != null)
                        ServerMgr.Instance.StopCoroutine(_despawnRoutine);

                    _isDespawning = false;
                    _isSpawning = false;

                    State = SpawnState.Spawn;

                    _spawnOrders.Clear();
                }

                private static void StartTimer() => Instance.timer.In(1f, CheckTime);

                private static bool ShouldSpawn()
                {
                    float currentTime = TOD_Sky.Instance.Cycle.Hour;

                    if (Configuration.TimedSpawns.Start > Configuration.TimedSpawns.End)
                        return currentTime > Configuration.TimedSpawns.Start || currentTime < Configuration.TimedSpawns.End;
                    return currentTime > Configuration.TimedSpawns.Start && currentTime < Configuration.TimedSpawns.End;
                }

                private static void CheckTime()
                {
                    if (ShouldSpawn())
                    {
                        if (State == SpawnState.Despawn)
                        {
                            if (Configuration.TimedSpawns.BroadcastStart)
                                SendNotification("Notification.BeginSpawn");
                            
                            State = SpawnState.Spawn;
                            StopDespawning();
                            DequeueAndSpawn();
                        }
                    }
                    else
                    {
                        if (State == SpawnState.Spawn)
                        {
                            if (Configuration.TimedSpawns.BroadcastEnd)
                                SendNotification("Notification.BeginDespawn");
                            
                            State = SpawnState.Despawn;

                            if (Configuration.TimedSpawns.Despawn)
                            {
                                StopSpawning();
                                QueueAndDespawn();
                            }
                        }
                    }

                    StartTimer();
                }

                private static void SendNotification(string key)
                {
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                        player.ChatMessage(Instance.lang.GetMessage(key, Instance, player.UserIDString));
                }
                #endregion
            }
        }

        public class ZombieNPC : CustomScientistNPC, IAIAttack, IAISenses, IThinker
        {
            private float lastAimSetTime;

            public Horde Horde { get; internal set; }

            public bool IsGroupLeader { get; internal set; }

            public bool HasTarget { get { return CurrentTarget; } }

            public bool HasHumanTargetInRange
            {
                get
                {
                    if (!Transform || !CurrentTarget || !CurrentTarget.transform)
                        return false;
                    
                    return CurrentTarget is BasePlayer && Vector3.Distance(Transform.position, CurrentTarget.transform.position) < Loadout.Sensory.TargetLostRange;
                }
            }

            private float lastSetDestinationOverride;
            
            public bool RecentlySetDestination { get { return Time.time - lastSetDestinationOverride < 5f; } }
            
            public bool unreachableLastFrame;

            public bool isHidingInside;
            
            public Item ThrowableExplosive { get; private set; }

            public ConfigData.MemberOptions.Loadout Loadout { get; set; }

            private SphereEntity noiseEmitter;

            private BasePlayer recentAttacker;


            private const int LOS_BLOCKING_LAYER = 1218519041;

            #region Horde            
            public void SetRoamTargetOverride(Vector3 position)
            {
                if (IsGroupLeader)
                {
                    lastSetDestinationOverride = Time.time;
                    DestinationOverride = position;
                    ResetRoamState();
                }
            }

            public void ResetRoamState()
            {
                if (Brain != null && CurrentState == AIState.Roam)
                {
                    Brain.states[AIState.Roam].StateEnter(Brain, this);
                }
            }

            public void OnInitialSpawn()
            {
                if (IsGroupLeader)
                    return;

                if (Horde.Leader.HasTarget)
                    Brain.Senses.Memory.SetKnown(Horde.Leader.CurrentTarget, this, null);
            }
            #endregion

            #region BaseNetworkable            
            public override void ServerInit()
            {
                faction = Faction.Horror;
                
                base.ServerInit();

                inventory.containerWear.canAcceptItem = null;

                Loadout.GiveToPlayer(this);

                FindThrowableExplosive();

                InvokeRepeating(LightCheck, 1f, 30f);

                DeathEffects = new GameObjectRef[]
                {
                    new GameObjectRef
                    {
                        guid = GameManifest.pathToGuid["assets/prefabs/npc/murderer/sound/death.prefab"]
                    }
                };

                if (Configuration.Member.EnableZombieNoises)
                    Invoke(SetupNoiseObject, 1f);
            }
            #endregion

            #region Noises
            private void SetupNoiseObject()
            {
                noiseEmitter = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", transform.position) as SphereEntity;

                noiseEmitter.SetParent(this, StringPool.Get("head"));
                noiseEmitter.transform.localPosition = Vector3.zero;
                noiseEmitter.transform.localScale = Vector3.zero;

                noiseEmitter.currentRadius = noiseEmitter.lerpRadius = 0f;
                noiseEmitter.lerpSpeed = 1000;
                noiseEmitter.enabled = false;

                noiseEmitter.enableSaving = false;
                noiseEmitter.Spawn();

                noiseEmitter.InvokeRandomized(() => noiseEmitter.SendNetworkUpdate(NetworkQueue.Update), 10f, 10f, 2f);
                MakeZombieNoises();
            }

            private void MakeZombieNoises()
            {
                if (!IsAlive() || IsDestroyed || !noiseEmitter || noiseEmitter.IsDestroyed)
                    return;

                if (!Horde.IsSleeping)
                {
                    const string BREATHING_EFFECT = "assets/prefabs/npc/murderer/sound/breathing.prefab";

                    Effect.server.Run(BREATHING_EFFECT, noiseEmitter, 0, Vector3.zero, Vector3.zero, null, false);
                }

                Invoke(MakeZombieNoises, Random.Range(8, 15));
            }
            #endregion

            #region BaseEntity
            public override float BoundsPadding() => (0.1f * Brain.Navigator.Agent.speed) + 0.1f;

            public override float StartHealth() => Loadout.Vitals.Health;

            public override float StartMaxHealth() => Loadout.Vitals.Health;

            public override float MaxHealth() => Loadout.Vitals.Health;

            public override void OnSensation(Sensation sensation)
            {
                if (!Configuration.Horde.UseSenses || sensation.Type == SensationType.Explosion)
                    return;

                if (sensation.UsedEntity is TimedExplosive && sensation.Type == SensationType.ThrownWeapon)
                    return;

                if (sensation.Initiator)
                {
                    if (sensation.Initiator is ZombieNPC)
                        return;

                    Brain.Senses.Memory.SetKnown(sensation.Initiator, this, null);
                }

                if (IsGroupLeader && CurrentState <= AIState.Roam && !HasTarget)
                    Horde.SetLeaderRoamTarget(sensation.Position);
            }

            public override void OnAttacked(HitInfo info)
            {
                base.OnAttacked(info);
                Horde.ForceWakeFromSleep();
            }
            #endregion

            #region BasePlayer
            public override bool IsNpc => _isNpc;

            private bool _isNpc = false;

            public override BaseNpc.AiStatistics.FamilyEnum Family => BaseNpc.AiStatistics.FamilyEnum.Zombie;

            public override string Categorize() => "Zombie";

            public override bool ShouldDropActiveItem() => false;
            #endregion

            #region BaseCombatEntity
            public override bool IsHostile() => true;
            #endregion

            #region NPCPlayer
            public override bool IsLoadBalanced() => true;

            public override void EquipWeapon(bool skipDeployDelay = false)
            {
                if (!isEquippingWeapon)
                    StartCoroutine(EquipItem());
            }

            private void FindThrowableExplosive()
            {
                for (int i = 0; i < inventory.containerBelt.itemList.Count; i++)
                {
                    Item item = inventory.containerBelt.GetSlot(i);
                    if (item != null && item.GetHeldEntity() is ThrownWeapon)
                    {
                        ThrowableExplosive = item;
                        break;
                    }
                }
            }

            private IEnumerator EquipItem(Item slot = null)
            {
                if (inventory && inventory.containerBelt != null)
                {
                    isEquippingWeapon = true;

                    if (slot == null)
                    {
                        for (int i = 0; i < inventory.containerBelt.itemList.Count; i++)
                        {
                            Item item = inventory.containerBelt.GetSlot(i);
                            if (item != null && item.GetHeldEntity() is AttackEntity)
                            {
                                slot = item;
                                break;
                            }
                        }
                    }

                    if (slot != null)
                    {
                        if (CurrentWeapon)
                        {
                            CurrentWeapon.SetHeld(false);
                            CurrentWeapon = null;

                            SendNetworkUpdate(NetworkQueue.Update);
                            inventory.UpdatedVisibleHolsteredItems();
                        }

                        yield return CoroutineEx.waitForSeconds(0.5f);

                        UpdateActiveItem(slot.uid);
                        
                        HeldEntity heldEntity = slot.GetHeldEntity() as HeldEntity;
                        if (heldEntity)
                        {
                            if (heldEntity is AttackEntity)
                                (heldEntity as AttackEntity).TopUpAmmo();

                            if (heldEntity is Chainsaw)
                                ServerStartChainsaw(heldEntity as Chainsaw);
                        }

                        CurrentWeapon = heldEntity as AttackEntity;
                    }

                    isEquippingWeapon = false;
                }
            }

            public override float GetAimConeScale() => Loadout.AimConeScale;

            public override Vector3 GetAimDirection()
            {
                if (Brain && Brain.Navigator && Brain.Navigator.IsOverridingFacingDirection)
                    return Brain.Navigator.FacingDirectionOverride;
                return base.GetAimDirection();
            }

            public override void SetAimDirection(Vector3 newAim)
            {
                if (newAim == Vector3.zero)
                    return;

                float num = Time.time - lastAimSetTime;
                lastAimSetTime = Time.time;

                AttackEntity attackEntity = GetAttackEntity();
                if (attackEntity)
                    newAim = attackEntity.ModifyAIAim(newAim, GetAimSwayScalar());

                if (isMounted)
                {
                    BaseMountable baseMountable = GetMounted();
                    Vector3 eulerAngles = baseMountable.transform.eulerAngles;
                    Quaternion aimRotation = Quaternion.Euler(Quaternion.LookRotation(newAim, baseMountable.transform.up).eulerAngles);

                    Vector3 lookRotation = Quaternion.LookRotation(transform.InverseTransformDirection(aimRotation * Vector3.forward), transform.up).eulerAngles;
                    lookRotation = BaseMountable.ConvertVector(lookRotation);

                    Quaternion clampedRotation = Quaternion.Euler(Mathf.Clamp(lookRotation.x, baseMountable.pitchClamp.x, baseMountable.pitchClamp.y),
                                                                  Mathf.Clamp(lookRotation.y, baseMountable.yawClamp.x, baseMountable.yawClamp.y),
                                                                  eulerAngles.z);

                    newAim = BaseMountable.ConvertVector(Quaternion.LookRotation(transform.TransformDirection(clampedRotation * Vector3.forward), transform.up).eulerAngles);
                }
                else
                {
                    BaseEntity parentEntity = GetParentEntity();
                    if (parentEntity)
                    {
                        Vector3 aimDirection = parentEntity.transform.InverseTransformDirection(newAim);
                        Vector3 forward = new Vector3(newAim.x, aimDirection.y, newAim.z);

                        eyes.rotation = Quaternion.Lerp(eyes.rotation, Quaternion.LookRotation(forward, parentEntity.transform.up), num * 25f);
                        viewAngles = eyes.bodyRotation.eulerAngles;
                        ServerRotation = eyes.bodyRotation;
                        return;
                    }
                }

                eyes.rotation = (isMounted ? Quaternion.Slerp(eyes.rotation, Quaternion.Euler(newAim), num * 70f) : Quaternion.Lerp(eyes.rotation, Quaternion.LookRotation(newAim, transform.up), num * 25f));
                viewAngles = eyes.rotation.eulerAngles;
                ServerRotation = eyes.rotation;
            }

            public override void Hurt(HitInfo info)
            {
                if (info.InitiatorPlayer is ZombieNPC)
                    return;

                if (info.Initiator is ResourceEntity)
                {
                    info.damageTypes.ScaleAll(0);
                    return;
                }

                if (Configuration.Member.HeadshotKills && info.isHeadshot)
                {
                    if (info.damageTypes.Total() >= Configuration.Member.MinimumHeadshotDamage)
                        info.damageTypes.ScaleAll(1000);
                }

                base.Hurt(info);

                BaseEntity initiator = info.Initiator;

                if (initiator != null && !initiator.EqualNetID(this))
                {
                    if (initiator == recentAttacker)
                        return;
                    
                    recentAttacker = initiator as BasePlayer;

                    if ((initiator is BasePlayer && CanTargetBasePlayer(initiator as BasePlayer)) || (initiator is BaseNpc && CanTargetEntity(initiator)))
                    {
                        Horde.RegisterInterestInTarget(this, initiator, isHidingInside || unreachableLastFrame);
                    }
                }
            }

            public override void OnKilled(HitInfo info)
            {
                _isNpc = true;
                base.OnKilled(info);
            }
            
            private string GetCorpsePrefab()
            {
                if (npcType == NPCType.GingerBreadMan)
                {
                    Random.State state = Random.state;
                    Random.InitState((int)(4332 + userID));
                    float range = Random.Range(0f, 1f);
                    Random.state = state;

                    if (range > 0.5f)
                        return "assets/prefabs/npc/gingerbread/gingerbread_corpse_female.prefab";
                    return "assets/prefabs/npc/gingerbread/gingerbread_corpse_male.prefab";
                }

                return "assets/prefabs/npc/murderer/murderer_corpse.prefab";
            }

            public override BaseCorpse CreateCorpse(PlayerFlags flagsOnDeath, Vector3 posOnDeath, Quaternion rotOnDeath, List<TriggerBase> triggersOnDeath)
            {
                if (noiseEmitter != null && !noiseEmitter.IsDestroyed)
                    noiseEmitter.Kill(DestroyMode.None);

                RemoveItemsOnDeath();
                BaseCorpse corpse = base.CreateCorpse(flagsOnDeath, posOnDeath, rotOnDeath, triggersOnDeath);

                if (Configuration.Member.CorpseDespawnTime > 0f)
                    corpse.ResetRemovalTime(Configuration.Member.CorpseDespawnTime);
                
                return corpse;
            }

            private void RemoveItemsOnDeath()
            {
                if (Configuration.Member.GiveGlowEyes)
                {
                    for (int i = inventory.containerWear.itemList.Count - 1; i >= 0; i--)
                    {
                        Item item = inventory.containerWear.itemList[i];
                        if (item.info == ConfigData.MemberOptions.Loadout.GlowEyes)
                        {
                            item.RemoveFromContainer();
                            item.Remove(0f);
                        }
                    }
                }

                if (Configuration.Loot.DropInventory && Configuration.Loot.DroppedBlacklist.Length > 0f)
                {
                    Action<ItemContainer> removeBlacklistedItems = new Action<ItemContainer>((ItemContainer itemContainer) =>
                    {
                        for (int i = itemContainer.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = itemContainer.itemList[i];
                            if (Configuration.Loot.DroppedBlacklist.Contains(item.info.shortname))
                            {
                                item.RemoveFromContainer();
                                item.Remove(0f);
                            }
                        }
                    });

                    removeBlacklistedItems(inventory.containerBelt);
                    removeBlacklistedItems(inventory.containerMain);
                }
            }

            private void ResetModifiedWeaponRange()
            {
                float effectiveRange;

                for (int i = 0; i < inventory.containerBelt.itemList.Count; i++)
                {
                    Item item = inventory.containerBelt.itemList[i];

                    if (item != null)
                    {
                        HeldEntity heldEntity = item.GetHeldEntity() as HeldEntity;
                        if (heldEntity != null)
                        {
                            if (heldEntity is BaseProjectile)
                            {
                                if (ConfigData.MemberOptions.Loadout.GetDefaultEffectiveRange(item.info.shortname, out effectiveRange))
                                    (heldEntity as BaseProjectile).effectiveRange = effectiveRange;
                            }

                            if (heldEntity is BaseMelee)
                            {
                                if (ConfigData.MemberOptions.Loadout.GetDefaultEffectiveRange(item.info.shortname, out effectiveRange))
                                    (heldEntity as BaseMelee).effectiveRange = effectiveRange;
                            }
                        }
                    }
                }
            }

            public override bool IsOnGround() => true;
            #endregion

            #region Thrown Weapons
            private float lastThrowTime;

            public bool TryThrownWeapon(BasePlayer target)
            {
                if (HasThrownCooldown())
                    return false;

                if (ThrowableExplosive == null)
                {
                    lastThrowTime = Time.time;
                    return false;
                }

                return TryThrownWeapon(ThrowableExplosive, target);
            }

            private bool TryThrownWeapon(Item item, BasePlayer target)
            {
                Vector3 targetPosition = target.transform.position;
                
                float distanceToTarget = Vector3.Distance(targetPosition, Transform.position);
                if (distanceToTarget <= 2f || distanceToTarget > Configuration.Member.MaxExplosiveThrowRange)
                    return false;

                if (TerrainMeta.HeightMap.GetHeight(targetPosition) > targetPosition.y)
                {
                    if (!IsPlayerVisibleToUs(target, eyes.position, LOS_BLOCKING_LAYER))
                        return false;
                }

                if (!IsVisible(CenterPoint(), targetPosition, float.MaxValue))
                    return false;

                if (!UseThrownWeapon(item, target))
                    return false;

                lastThrowTime = Time.time;
                return true;
            }
            
            private bool HasThrownCooldown() => Time.time - lastThrowTime < 5f;
            
            private new bool UseThrownWeapon(Item item, BaseEntity target)
            {
                if (item == null || item.amount == 0)
                    return false;
                
                UpdateActiveItem(item.uid);
                
                ThrownWeapon thrownWeapon = GetActiveItem()?.GetHeldEntity() as ThrownWeapon;
                if (!thrownWeapon)
                    return false;
                
                StartCoroutine(DoThrow(thrownWeapon, target));
                return true;
            }

            private IEnumerator DoThrow(ThrownWeapon thrownWeapon, BaseEntity target)
            {
                modelState.aiming = true;
                
                yield return new WaitForSeconds(1.5f);

                if (!target || !thrownWeapon)
                { 
                    modelState.aiming = false;
                    yield break;
                }
                
                Vector3 targetPosition = target.transform.position;
                Brain.Navigator.SetFacingDirectionOverride(Vector3Ex.Direction(targetPosition, Transform.position));
                
                if (!Configuration.Member.ConsumeThrowables)
                    thrownWeapon.GetItem().amount++;
                
                thrownWeapon.ResetAttackCooldown();
                thrownWeapon.ServerThrow(targetPosition);
                
                modelState.aiming = false;
                Invoke(EquipTest, 0.5f);
            }
            #endregion
            
            #region IAIAttack
            public bool StartAttacking(BaseEntity target)
            {
                BaseCombatEntity baseCombatEntity = target as BaseCombatEntity;
                if (!baseCombatEntity)
                    return false;

                return Attack(baseCombatEntity);
            }

            private bool Attack(BaseCombatEntity target)
            {
                if (!target)
                    return false;
                
                Vector3 vector = target.ServerPosition - ServerPosition;
                if (vector.magnitude > 0.001f && !isMounted)
                    ServerRotation = Quaternion.LookRotation(vector.normalized);
                
                AttackEntity attackEntity = GetAttackEntity();
                if (attackEntity)
                {
                    if (!isMounted)
                    {
                        if (!(attackEntity is BaseMelee) && !(attackEntity is Chainsaw))
                            return true;

                        if (Brain.Navigator.IsSwimming() && !(attackEntity is BaseMelee))
                            return false;
                    }

                    ServerUseCurrentWeapon(attackEntity);

                    return true;
                }

                return false;
            }

            public void StopAttacking()
            {
            }
            
            public new void AttackTick(float delta, BaseEntity target, bool targetIsLOS)
            {
                if (!target)
                    return;

                AttackEntity attackEntity = GetAttackEntity();
                if (attackEntity && !(attackEntity is BaseProjectile))
                    return;
                
                if (Brain.Navigator.IsSwimming())
                    return;

                Vector3 forward = eyes.BodyForward();
                Vector3 direction = target.CenterPoint() - eyes.position;
                float dot = Vector3.Dot(forward, direction.normalized);

                if (!targetIsLOS)
                {
                    if (dot < 0.5f)
                        targetAimedDuration = 0f;

                    CancelBurst();
                }
                else
                {
                    if (dot > 0.2f)
                        targetAimedDuration += delta;
                }

                if (targetAimedDuration >= 0.2f && targetIsLOS)
                {
                    float distanceToTarget;
                    if (IsTargetInAttackRange(target, out distanceToTarget))
                        ServerUseCurrentWeapon(attackEntity);
                }
                else CancelBurst();
            }

            private void ServerUseCurrentWeapon(AttackEntity attackEntity)
            {
                BaseProjectile baseProjectile = attackEntity as BaseProjectile;
                if (baseProjectile)
                {
                    if (baseProjectile.primaryMagazine.contents <= 0)
                    {
                        baseProjectile.ServerReload();
                        return;
                    }

                    if (baseProjectile.NextAttackTime > Time.time)
                        return;
                }
                
                if (!(attackEntity is LiquidWeapon) && Mathf.Approximately(attackEntity.attackLengthMin, -1f))
                {
                    attackEntity.ServerUse(damageScale, null);
                    lastGunShotTime = Time.time;
                    return;
                }
                
              
                if (IsInvoking(TriggerDown))
                    return;

                if (Time.time < nextTriggerTime)
                    return;

                InvokeRepeating(TriggerDown, 0f, 0.01f);

                triggerEndTime = Time.time + Random.Range(attackEntity.attackLengthMin, attackEntity.attackLengthMax);
                TriggerDown();
            }

            public override void TriggerDown()
            {
                AttackEntity heldEntity = GetHeldEntity() as AttackEntity;
                if (heldEntity != null)
                {
                    if (heldEntity is LiquidWeapon)
                        LiquidWeaponStartFiring(heldEntity as LiquidWeapon);
                    else heldEntity.ServerUse(this.damageScale, null);
                }
                
                lastGunShotTime = Time.time;
                
                if (Time.time > triggerEndTime)
                {
                    if (heldEntity != null && heldEntity is LiquidWeapon)
                        LiquidWeaponStopFiring(heldEntity as LiquidWeapon);
                    
                    CancelInvoke(TriggerDown);
                    nextTriggerTime = Time.time + (heldEntity != null ? heldEntity.attackSpacing : 1f);
                }
            }
            
            #region Liquid Weapons

            private static ItemDefinition m_WaterDefinition;
            
            private void LiquidWeaponFireTick()
            {
                LiquidWeapon liquidWeapon = GetHeldEntity() as LiquidWeapon;
                
                int num = Mathf.Min(liquidWeapon.FireAmountML, liquidWeapon.AmountHeld());
                if (num == 0)
                {
                    LiquidWeaponStopFiring(liquidWeapon);
                    return;
                }
                
                liquidWeapon.LoseWater(num);
                
                float currentRange = liquidWeapon.CurrentRange;
                liquidWeapon.pressure -= liquidWeapon.PressureLossPerTick;
                
                if (liquidWeapon.pressure <= 0)
                    LiquidWeaponStopFiring(liquidWeapon);
                
                Ray ray = eyes.BodyRay();

                if (Physics.Raycast(ray, out RaycastHit raycastHit, currentRange, 1218652417))
                {
                    if (m_WaterDefinition == null)
                        m_WaterDefinition = ItemManager.FindItemDefinition("water.salt");
                    
                    WaterBall.DoSplash(raycastHit.point, liquidWeapon.SplashRadius, m_WaterDefinition, num);
                    DamageUtil.RadiusDamage(this, liquidWeapon.LookupPrefab(), raycastHit.point, liquidWeapon.MinDmgRadius, liquidWeapon.MaxDmgRadius, liquidWeapon.Damage, 131072, true);
                }
                base.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
            
            private void LiquidWeaponStartFiring(LiquidWeapon liquidWeapon)
            {
                if (Time.realtimeSinceStartup < liquidWeapon.cooldownTime)
                    return;

                liquidWeapon.pressure = liquidWeapon.MaxPressure;
                
                CancelInvoke(LiquidWeaponFireTick);
                InvokeRepeating(LiquidWeaponFireTick, 0f, liquidWeapon.FireRate);
                
                liquidWeapon.SetFlag(BaseEntity.Flags.On, true, false, true);
                
                if (Time.realtimeSinceStartup + liquidWeapon.FireRate > liquidWeapon.cooldownTime)
                    liquidWeapon.cooldownTime = Time.realtimeSinceStartup + liquidWeapon.FireRate;
                
                liquidWeapon.SendNetworkUpdateImmediate(false);
            }
            
            private void LiquidWeaponStopFiring(LiquidWeapon liquidWeapon)
            {
                CancelInvoke(LiquidWeaponFireTick);
                liquidWeapon.pressure = liquidWeapon.MaxPressure;
                
                liquidWeapon.SetFlag(BaseEntity.Flags.On, false, false, true);
                liquidWeapon.SendNetworkUpdateImmediate(false);
            }
            #endregion

            public new bool CanSeeTarget(BaseEntity entity, Vector3 fromOffset)
            {
                if (!(entity is BaseCombatEntity))
                    return false;

                if (entity is BasePlayer)
                    return IsPlayerVisibleToUs((entity as BasePlayer), fromOffset, LOS_BLOCKING_LAYER);
                
                return (IsVisible(entity.CenterPoint(), eyes.worldStandingPosition, float.PositiveInfinity) ||
                        IsVisible(entity.transform.position, eyes.worldStandingPosition, float.PositiveInfinity));
            }

            public new float EngagementRange()
            {
                AttackEntity attackEntity = GetAttackEntity();
                if (!attackEntity)
                    return Brain.SenseRange;

                return attackEntity is ThrownWeapon ? -1f : attackEntity.effectiveRange;
            }

            public new float GetAmmoFraction()
            {
                AttackEntity attackEntity = GetAttackEntity();
                if (attackEntity is BaseProjectile)
                    return (attackEntity as BaseProjectile).AmmoFraction();
                
                return 1f;
            }

            public new BaseEntity GetBestTarget()
            {
                BaseEntity target = null;
                float delta = -1f;

                foreach (BaseEntity baseEntity in Brain.Senses.Memory.Targets)
                {
                    if (!baseEntity || baseEntity.Health() <= 0f)
                        continue;

                    if (baseEntity is BasePlayer player && !CanTargetBasePlayer(player))
                        continue;

                    if (!CanTargetEntity(baseEntity))
                        continue;

                    float distanceToTarget = Vector3.Distance(baseEntity.transform.position, Transform.position);
                    if (distanceToTarget > Brain.TargetLostRange)
                        continue;

                    float rangeDelta = 1f - Mathf.InverseLerp(1f, Brain.SenseRange, distanceToTarget);

                    float dot = Vector3.Dot((baseEntity.transform.position - eyes.position).normalized, eyes.BodyForward());

                    if (Loadout.Sensory.IgnoreNonVisionSneakers && dot < Brain.VisionCone)
                        continue;

                    rangeDelta += Mathf.InverseLerp(Brain.VisionCone, 1f, dot) / 2f;
                    rangeDelta += (Brain.Senses.Memory.IsLOS(baseEntity) ? 2f : 0f);
                    
                    if (rangeDelta <= delta)
                        continue;

                    target = baseEntity;
                    delta = rangeDelta;
                }

                if (target != null)
                    Horde.RegisterInterestInTarget(this, target, false);
                
                CurrentTarget = target;

                return target;
            }

            public override bool CanTargetBasePlayer(BasePlayer player)
            {
                if (!player.IsValid() || player.IsFlying || player is NPCShopKeeper)
                    return false;

                if (Configuration.Member.IgnoreSleepers && player.IsSleeping())
                    return false;

                if (!Configuration.Member.TargetHumanNPCs && !player.IsNpc && !player.userID.IsSteamId())
                    return false;

                if (!Configuration.Member.TargetNPCs && player.IsNpc)
                {
                    if (Configuration.Member.TargetNPCsThatAttack && recentAttacker == player)
                        return true;

                    return false;
                }

                if (player.userID.IsSteamId() && Instance.permission.UserHasPermission(player.UserIDString, IGNORE_PERMISSION))
                    return false;

                if (Loadout.Sensory.IgnoreSafeZonePlayers && player.InSafeZone())
                    return false;
                
                if (!Configuration.Member.TargetInBuildings && IsInOrOnBuilding(player))
                    return false;

                return true;
            }

            public override bool CanTargetEntity(BaseEntity baseEntity)
            {
                if (!(baseEntity is BasePlayer) && !(baseEntity is BaseNpc))
                    return false;
                
                if (Configuration.Horde.RestrictLocalChaseDistance && Horde.IsLocalHorde)
                {
                    if (Vector3.Distance(baseEntity.transform.position, Horde.InitialPosition) > Horde.MaximumRoamDistance * 1.5f)
                        return false;
                }

                if (!Configuration.Member.TargetAnimals && baseEntity is BaseNpc)
                    return false;

                if (!Settings.Movement.CanSwim && baseEntity.WaterFactor() >= 0.95f)
                    return false;

                if (Vector3.Distance(baseEntity.transform.position, Transform.position) > Brain.TargetLostRange)
                    return false;
                
                return true;
            }

            public bool IsTargetInAttackRange(BaseEntity entity, out float distance)
            {
                distance = Vector3.Distance(entity.transform.position, Transform.position);
                return distance <= EngagementRange();
            }
            
            public bool IsTargetInAttackRange(BaseEntity entity)
            {
                return Vector3.Distance(entity.transform.position, Transform.position) <= EngagementRange();
            }
            #endregion

            #region IAISenses
            public new bool IsFriendly(BaseEntity entity) => entity is ZombieNPC;

            public new bool IsTarget(BaseEntity entity) => !IsFriendly(entity);

            public new bool IsThreat(BaseEntity entity) => IsTarget(entity);
            #endregion

            #region IThinker

            private float lastlevelnote = 0;
            public override void ServerThink(float delta)
            {
                if (!Configuration.Member.TargetInBuildings && isHidingInside)
                {
                    Brain.SwitchToState(AIState.Idle, 0);
                }

                base.ServerThink(delta);
                /*this.TickAi(delta);
                if (this.Brain.ShouldServerThink())
                {
                    this.Brain.DoThink();
                }
                
                if (Settings.Movement.CanSwim)
                {
                    float waterLevel = WaterFactor();
                    if (modelState.waterLevel != waterLevel)
                    {
                        modelState.waterLevel = waterLevel;

                        if (CurrentWeapon is Chainsaw)
                        {
                            if (Brain.Navigator.IsSwimming())
                                ServerStopChainsaw((CurrentWeapon as Chainsaw));
                            else ServerStartChainsaw((CurrentWeapon as Chainsaw));
                        }
                        
                        SendNetworkUpdate(NetworkQueue.Update);
                    }

                    bool isSwimming = Brain.Navigator.IsSwimming();

                    Brain.Navigator.CanUseNavMesh = !isSwimming;
                    Brain.Navigator.CanUseCustomNav = isSwimming;
                }*/
                //
            }
            #endregion

            #region Lights
            private new void LightCheck()
            {
                if ((TOD_Sky.Instance.Cycle.Hour > 18 || TOD_Sky.Instance.Cycle.Hour < 6) && !lightsOn)
                    ToggleLights(true);

                if ((TOD_Sky.Instance.Cycle.Hour < 18 && TOD_Sky.Instance.Cycle.Hour > 6) && lightsOn)
                    ToggleLights(false);
            }

            private void ToggleLights(bool lightsOn)
            {
                Item activeItem = GetActiveItem();
                if (activeItem != null)
                {
                    HeldEntity heldEntity = activeItem.GetHeldEntity() as HeldEntity;
                    if (heldEntity != null)
                        heldEntity.SendMessage("SetLightsOn", lightsOn, SendMessageOptions.DontRequireReceiver);
                }

                foreach (Item item in inventory.containerWear.itemList)
                {
                    ItemModWearable itemModWearble = item.info.GetComponent<ItemModWearable>();
                    if (itemModWearble && itemModWearble.emissive)
                    {
                        item.SetFlag(global::Item.Flag.IsOn, lightsOn);
                        item.MarkDirty();
                    }
                }

                if (isMounted)
                    GetMounted().LightToggle(this);

                this.lightsOn = lightsOn;
            }
            #endregion

            #region Chainsaw Hackery
            private void ServerStartChainsaw(Chainsaw chainsaw)
            {
                if (chainsaw.HasFlag(Flags.On))
                    return;

                chainsaw.DoReload(default(BaseEntity.RPCMessage));
                chainsaw.SetEngineStatus(true);
                chainsaw.SendNetworkUpdateImmediate(false);

                Invoke(ChainsawRefuel, 1f);
            }

            private void ServerStopChainsaw(Chainsaw chainsaw)
            {
                if (!chainsaw.HasFlag(Flags.On))
                    return;

                chainsaw.SetEngineStatus(false);
                chainsaw.SendNetworkUpdateImmediate(false);

                CancelInvoke(ChainsawRefuel);
            }

            private void ChainsawRefuel()
            {
                Chainsaw chainsaw = GetAttackEntity() as Chainsaw;
                
                if (!chainsaw)
                    return;

                chainsaw.ammo = chainsaw.maxAmmo;

                Invoke(ChainsawRefuel, 1f);
            }
            #endregion

            #region Loadouts
            protected override LootContainer.LootSpawnSlot[] GetLootSpawnSlotsForType(NPCType type) => ZombieDefinition.LootSpawns;

            protected override PlayerInventoryProperties[] GetLoadoutForType(NPCType type) => null;
            #endregion
            
            #region Mounting

            public void TryDismount()
            {
                if (isMounted)
                {
                    BaseMountable baseMountable = GetMounted();
                    if (baseMountable)
                    {
                        baseMountable.DismountPlayer(this, true);
                    }
                }
            }

            public void TryMountTargetsVehicle(BaseEntity baseEntity)
            {
                if (Configuration.Member.CanMountVehicles && baseEntity is BasePlayer && (baseEntity as BasePlayer).isMounted)
                {
                    BaseVehicle baseVehicle = (baseEntity as BasePlayer).GetMountedVehicle();
                    if (baseVehicle)
                    {
                        AttemptMount(this, baseVehicle);
                    }
                }
            }
            
            private void AttemptMount(BasePlayer player, BaseVehicle baseVehicle)
            {
                if (baseVehicle._mounted != null)
                    return;
                
                if (!baseVehicle.MountEligable(player))
                    return;

                if (GetIdealMountPoint(baseVehicle, player, out BaseMountable idealMountPoint))
                {
                    if (idealMountPoint != baseVehicle)
                        idealMountPoint.AttemptMount(player, false);
                    else baseVehicle.AttemptMount(player, false);

                    if (player.GetMountedVehicle() == baseVehicle)
                        baseVehicle.PlayerMounted(player, idealMountPoint);
                }
            }

            private bool GetIdealMountPoint(BaseVehicle baseVehicle, BasePlayer player, out BaseMountable baseMountable)
            {
                baseMountable = null;
                
                if (!player)
                    return false;

                if (!baseVehicle.HasMountPoints())
                    return false;
                
                Vector3 position = player.transform.position;
                float shortestDistance = Single.PositiveInfinity;
                
                foreach (BaseVehicle.MountPointInfo allMountPoint in baseVehicle.allMountPoints)
                {
                    if (allMountPoint.mountable.AnyMounted())
                        continue;

                    float distance = Vector3.Distance(allMountPoint.mountable.mountAnchor.position, position);
                    if (distance > shortestDistance)
                        continue;

                    if (baseVehicle.IsSeatClipping(allMountPoint.mountable))
                        continue;
                    
                    baseMountable = allMountPoint.mountable;
                    shortestDistance = distance;
                }

                return baseMountable && shortestDistance < 3f;
            }

            #endregion
        }

        public class ZombieNPCBrain : CustomScientistBrain
        {
            public override void InitializeAI()
            {
                base.InitializeAI();
                MaxGroupSize = 0;
            }

            public override EntityType GetSenseTypes() => Configuration.Member.GetSenseTypes();

            public override float GetMaxRoamDistance(CustomScientistNPC customNpc) => (customNpc as ZombieNPC).Horde.IsLocalHorde ? (customNpc as ZombieNPC).Horde.MaximumRoamDistance : -1f;

            public override void Think(float delta)
            {
                ZombieNPC zombieNpc = GetBaseEntity() as ZombieNPC;

                if (zombieNpc.IsGroupLeader)
                    zombieNpc.Horde.Update();

                base.Think(delta);
            }

            /*protected override void OnStateChanged()
            {
                base.OnStateChanged();
                
                Debug.Log($"OnStateChanged {CurrentState.StateType}");
            }*/

            public void SetSleeping(bool sleep)
            {
                if (sleep)
                {
                    sleeping = true;

                    if (Navigator != null)
                        Navigator.Pause();

                    CancelInvoke(TickMovement);
                }
                else
                {
                    sleeping = false;

                    if (Navigator != null)
                        Navigator.Resume();

                    CancelInvoke(TickMovement);
                    InvokeRandomized(TickMovement, 1f, 0.1f, 0.010000001f);
                }
            }

            public class ZombieRoamState : BasicAIState
            {
                private StateStatus status = StateStatus.Error;

                private Vector3 bestRoamPosition;

                private Vector3 lastSwimPosition;
                private float stuckTime = 0;

                public bool IsStuckSwimming() => stuckTime > 3f;

                public ZombieRoamState() : base(AIState.Roam)
                {
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateEnter(brain, entity);
                    status = StateStatus.Error;
                    
                    if (brain.PathFinder == null)
                        return;
                    
                    ZombieNPC zombieNpc = entity as ZombieNPC;
                    if (!zombieNpc)
                        return;
                    
                    zombieNpc.TryDismount();

                    bestRoamPosition = brain.Events.Memory.Position.Get(4);

                    bool isHeadingToDestination = false;
                    if (!zombieNpc.IsGroupLeader)
                    {
                        bestRoamPosition = BasePathFinder.GetPointOnCircle(zombieNpc.Horde.GetLeaderDestination(), Random.Range(2f, 7f), Random.Range(0f, 359f));
                        isHeadingToDestination = zombieNpc.Horde.Leader.DestinationOverride != Vector3.zero;
                    }
                    else
                    {
                        if (brain.Navigator.IsSwimming())
                        {
                            bestRoamPosition = brain.PathFinder.GetBestRoamPositionFromAnchor(brain.Navigator, zombieNpc.Transform.position, bestRoamPosition, 0f, 250f);
                        }
                        else
                        {
                            if (zombieNpc.DestinationOverride != Vector3.zero)
                            {
                                bestRoamPosition = brain.PathFinder.GetBestRoamPositionFromAnchor(brain.Navigator, zombieNpc.DestinationOverride, bestRoamPosition, 0f, 15f);

                                isHeadingToDestination = true;

                                if (Vector3.Distance(zombieNpc.Transform.position, zombieNpc.DestinationOverride) < 10f)
                                    zombieNpc.DestinationOverride = Vector3.zero;
                            }
                            else
                            {
                                bestRoamPosition = (zombieNpc.Horde.IsLocalHorde ? 
                                    brain.PathFinder.GetBestRoamPositionFromAnchor(brain.Navigator, zombieNpc.Horde.InitialPosition, bestRoamPosition, 20f, zombieNpc.Horde.MaximumRoamDistance) : 
                                    brain.PathFinder.GetBestRoamPosition(brain.Navigator, zombieNpc.transform.position, bestRoamPosition, 20f, 100f));
                            }
                        }
                    }

                    bool isAlert = zombieNpc.Horde.HordeOnAlert || isHeadingToDestination;
                    if (brain.Navigator.SetDestination(bestRoamPosition, isAlert ? BaseNavigator.NavigationSpeed.Fast : DefaultRoamSpeed, 0f, 0f))
                    {
                        brain.Navigator.ClearFacingDirectionOverride();
                        
                        if (zombieNpc.IsGroupLeader)
                            zombieNpc.Horde.ResetRoamTarget();
                        
                        status = StateStatus.Running;
                        return;
                    }
                    
                    if (brain.Navigator.IsSwimming() && !brain.Navigator.Agent.isOnNavMesh && !zombieNpc.isMounted)
                    {
                        brain.Navigator.Agent.enabled = false;
                        
                        Vector3 aimDirection = Vector3Ex.Direction(bestRoamPosition, brain.Navigator.transform.position);
                        brain.Navigator.SetFacingDirectionOverride(aimDirection);
                        
                        status = StateStatus.Running;
                        return;
                    }

                    status = StateStatus.Error;
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    brain.Navigator.Stop();
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    if (status == StateStatus.Error)
                        return status;

                    ZombieNPC zombieNpc = entity as ZombieNPC;
                    if (!zombieNpc)
                        return StateStatus.Error;

                    if (zombieNpc.transform.position == lastSwimPosition)
                        stuckTime += delta;
                    else
                    {
                        lastSwimPosition = zombieNpc.transform.position;
                        stuckTime = 0;
                    }

                    if (brain.Navigator.IsSwimming())
                        return IsStuckSwimming() ? StateStatus.Finished : StateStatus.Running;
                         
                    if (brain.Navigator.Moving)
                        return StateStatus.Running;
                    
                    return StateStatus.Finished;
                }
            }

            public class ZombieAttackState : BasicAIState
            {
                private IAIAttack attack;

                private float originalStoppingDistance;

                public ZombieAttackState() : base(AIState.Attack)
                {
                    AgrresiveState = true;
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    entity.SetFlag(BaseEntity.Flags.Reserved3, true, false, true);
                    
                    originalStoppingDistance = brain.Navigator.StoppingDistance;
                    brain.Navigator.Agent.stoppingDistance = 1f;
                    brain.Navigator.StoppingDistance = 1f;
                    base.StateEnter(brain, entity);
                    
                    attack = (entity as IAIAttack);
                    
                    BaseEntity baseEntity = brain.Events.Memory.Entity.Get(brain.Events.CurrentInputMemorySlot);
                    if (baseEntity != null)
                    {
                        Vector3 aimDirection = Vector3Ex.Direction(baseEntity.CenterPoint(), (entity as ZombieNPC).eyes.position);
                        brain.Navigator.SetFacingDirectionOverride(aimDirection);

                       if (!((entity as ZombieNPC).GetAttackEntity() is BaseProjectile) && attack.CanAttack(baseEntity))
                            attack.StartAttacking(entity);

                        brain.Navigator.SetDestination(baseEntity.transform.position, BaseNavigator.NavigationSpeed.Fast, 0f, 0f);
                    }
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);

                    entity.SetFlag(BaseEntity.Flags.Reserved3, false, false, true);
                    brain.Navigator.Agent.stoppingDistance = originalStoppingDistance;
                    brain.Navigator.StoppingDistance = originalStoppingDistance;
                    brain.Navigator.ClearFacingDirectionOverride();
                    brain.Navigator.Stop();

                    attack.StopAttacking();
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    
                    if (attack == null)
                        return StateStatus.Error;

                    ZombieNPC zombieNpc = entity as ZombieNPC;
                    if (!zombieNpc)
                        return StateStatus.Error;
                    
                    BaseEntity baseEntity = brain.Events.Memory.Entity.Get(brain.Events.CurrentInputMemorySlot);
                    if (!baseEntity || !zombieNpc.CanTargetEntity(baseEntity) || (baseEntity is BasePlayer player && !zombieNpc.CanTargetBasePlayer(player)))
                    {
                        brain.Navigator.ClearFacingDirectionOverride();
                        attack.StopAttacking();
                        return StateStatus.Finished;
                    }
                    
                    if (zombieNpc.isMounted)
                    {
                        if (baseEntity is BasePlayer && !(baseEntity as BasePlayer).isMounted)
                            zombieNpc.TryDismount();
                        
                        return StateStatus.Running;
                    }

                    Vector3 targetPosition = zombieNpc.GetAttackEntity() is BaseProjectile ? brain.PathFinder.GetRandomPositionAround(baseEntity.transform.position, 2f, 10f) : baseEntity.transform.position;
                    
                    if (!brain.Navigator.SetDestination(targetPosition, BaseNavigator.NavigationSpeed.Fast, 0.2f, 0f))
                        return StateStatus.Error;

                    Vector3 aimDirection = Vector3Ex.Direction(baseEntity.CenterPoint(), zombieNpc.eyes.position);
                    brain.Navigator.SetFacingDirectionOverride(aimDirection);

                    if (zombieNpc.GetAttackEntity() is BaseProjectile)
                        zombieNpc.AttackTick(delta, baseEntity, brain.Senses.Memory.IsLOS(baseEntity));
                    else if (attack.CanAttack(baseEntity))
                        attack.StartAttacking(entity);

                    zombieNpc.TryMountTargetsVehicle(baseEntity);

                    return StateStatus.Running;
                }
            }

            public class ZombieChaseState : BasicAIState
            {
                private float throwDelayTime;

                private bool useBeanCan;

                public ZombieChaseState() : base(AIState.Chase)
                {
                    AgrresiveState = true;
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateEnter(brain, entity);
                    entity.SetFlag(BaseEntity.Flags.Reserved3, true, false, true);
                    
                    throwDelayTime = Time.time + Random.Range(0.2f, 0.5f);
                    useBeanCan = (Random.Range(0f, 100f) <= 20f);
                    
                    BaseEntity baseEntity = brain.Events.Memory.Entity.Get(brain.Events.CurrentInputMemorySlot);
                    if (baseEntity != null)
                        brain.Navigator.SetDestination(baseEntity.transform.position, BaseNavigator.NavigationSpeed.Fast, 0f, 0f);
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    
                    entity.SetFlag(BaseEntity.Flags.Reserved3, false, false, true);

                    (entity as ZombieNPC).isHidingInside = false;
                    (entity as ZombieNPC).unreachableLastFrame = false;
                    
                    brain.Navigator.Stop();
                    brain.Navigator.ClearFacingDirectionOverride();
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    
                    ZombieNPC zombieNpc = (brain.GetBrainBaseEntity() as ZombieNPC);
                    if (!zombieNpc)
                        return StateStatus.Error;
                    
                    BaseEntity baseEntity = brain.Events.Memory.Entity.Get(brain.Events.CurrentInputMemorySlot);
                    if (!baseEntity || !zombieNpc.CanTargetEntity(baseEntity) || (baseEntity is BasePlayer player && !zombieNpc.CanTargetBasePlayer(player)))
                    {
                        brain.Navigator.Stop();
                        return StateStatus.Error;
                    }

                    if (zombieNpc.isMounted)
                    {
                        if (baseEntity is BasePlayer && !(baseEntity as BasePlayer).isMounted)
                            zombieNpc.TryDismount();
                        
                        return StateStatus.Running;
                    }
                   
                    AttackEntity attackEntity = zombieNpc.GetAttackEntity();

                    if ((attackEntity is BaseMelee || attackEntity is BaseProjectile) && baseEntity is BasePlayer)
                    {
                        if ((zombieNpc.unreachableLastFrame || useBeanCan) && CanThrow && Time.time >= zombieNpc.Horde.NextThrownWeaponTime && zombieNpc.TryThrownWeapon(baseEntity as BasePlayer))
                        {
                            if (brain.Navigator.IsSwimming() && !(baseEntity as BasePlayer).isMounted)
                                goto ContinueEvent;

                            zombieNpc.Horde.NextThrownWeaponTime = Time.time + (zombieNpc.unreachableLastFrame ? 0.5f : ConVar.Halloween.scarecrow_throw_beancan_global_delay);

                            brain.Navigator.SetFacingDirectionOverride(Vector3Ex.Direction(baseEntity.transform.position, zombieNpc.Transform.position));

                            brain.Navigator.Stop();
                            return StateStatus.Running;
                        }

                        ContinueEvent:
                        if (!(attackEntity is BaseProjectile) && zombieNpc.modelState.aiming)
                        {
                            return StateStatus.Running;
                        }
                    }

                    brain.Navigator.SetFacingDirectionOverride(Vector3Ex.Direction(baseEntity.CenterPoint(), zombieNpc.eyes.position));
                    
                    Vector3 targetPosition = attackEntity is BaseProjectile && brain.Senses.Memory.IsLOS(baseEntity) ? 
                        brain.PathFinder.GetRandomPositionAround(baseEntity.transform.position, 2f, 10f) : 
                        baseEntity.transform.position;

                    brain.Navigator.SetDestination(targetPosition, BaseNavigator.NavigationSpeed.Fast, 0.25f, 0f);

                    zombieNpc.isHidingInside = baseEntity is BasePlayer && IsInOrOnBuilding(baseEntity);
                    
                    if ((zombieNpc.isHidingInside || brain.Navigator.Agent.path.status > NavMeshPathStatus.PathComplete)/* && attackEntity is BaseMelee*/) 
                    {
                        zombieNpc.unreachableLastFrame = true;
                        return StateStatus.Running;
                    }
                    
                    zombieNpc.unreachableLastFrame = false;

                    zombieNpc.TryMountTargetsVehicle(baseEntity);
                    
                    return !brain.Navigator.Moving ? StateStatus.Finished : StateStatus.Running;
                }

                private bool CanThrow => Time.time >= throwDelayTime && ConVar.AI.npc_use_thrown_weapons && ConVar.Halloween.scarecrows_throw_beancans;
            }

            public class ZombieMountedState : BasicAIState
            {
                private IAIAttack attack;
                
                public ZombieMountedState() : base(AIState.Mounted){}

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateEnter(brain, entity);
                    
                    attack = entity as IAIAttack;
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    
                    if (((ZombieNPC)entity).isMounted)
                        ((ZombieNPC)entity).TryDismount();
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    
                    ZombieNPC zombieNpc = (brain.GetBrainBaseEntity() as ZombieNPC);
                    if (!zombieNpc)
                        return StateStatus.Error;

                    BaseEntity baseEntity = brain.Events.Memory.Entity.Get(brain.Events.CurrentInputMemorySlot);
                    if (!baseEntity || !zombieNpc.CanTargetEntity(baseEntity) || (baseEntity is BasePlayer && !zombieNpc.CanTargetBasePlayer(baseEntity as BasePlayer)))
                    {
                        brain.Navigator.Stop();
                        return StateStatus.Error;
                    }

                    if (baseEntity is BasePlayer && !(baseEntity as BasePlayer).isMounted)
                    {
                        zombieNpc.TryDismount();

                        return StateStatus.Running;
                    }
                    
                    /*brain.Navigator.SetFacingDirectionOverride(Vector3Ex.Direction(baseEntity.transform.position, zombieNpc.Transform.position));*/
                    brain.Navigator.SetFacingDirectionOverride(Vector3Ex.Direction(baseEntity.CenterPoint(), zombieNpc.eyes.position));
                    
                    if (zombieNpc.GetAttackEntity() is BaseProjectile)
                        zombieNpc.AttackTick(delta, baseEntity, brain.Senses.Memory.IsLOS(baseEntity));
                    else if (attack.CanAttack(baseEntity))
                        attack.StartAttacking(entity);

                    return StateStatus.Running;
                }
            }
        }

        #region Commands         
        [ChatCommand("horde")]
        private void cmdHorde(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "/horde info - Show position and information about active zombie hordes");
                SendReply(player, "/horde tpto <number> - Teleport to the specified zombie horde");
                SendReply(player, "/horde destroy <number> - Destroy the specified zombie horde");
                SendReply(player, "/horde create <opt:distance> <opt:profile> - Create a new zombie horde on your position, optionally specifying distance they can roam and the horde profile you want to use");
                SendReply(player, "/horde createspawn <opt:membercount> <opt:distance> <opt:profile> - Save your current position as a custom horde spawn point");
                SendReply(player, "/horde createloadout - Copy your current inventory to a new zombie loadout");
                SendReply(player, "/horde hordecount <number> - Set the maximum number of hordes allowed");
                SendReply(player, "/horde membercount <number> - Set the maximum number of members allowed per horde");
                return;
            }

            switch (args[0].ToLower())
            {
                case "info":
                    int memberCount = 0;
                    int hordeNumber = 0;
                    foreach (Horde horde in Horde.AllHordes)
                    {
                        player.SendConsoleCommand("ddraw.text", 30, Color.green, horde.CentralLocation + new Vector3(0, 1.5f, 0), $"<size=20>Zombie Horde {hordeNumber}</size>");
                        memberCount += horde.MemberCount;
                        hordeNumber++;
                    }

                    SendReply(player, $"There are {Horde.AllHordes.Count} active zombie hordes with a total of {memberCount} zombies");
                    return;
                case "destroy":
                    {
                        if (args.Length != 2 || !int.TryParse(args[1], out int number))
                        {
                            SendReply(player, "You must specify a horde number");
                            return;
                        }

                        if (number < 0 || number >= Horde.AllHordes.Count)
                        {
                            SendReply(player, "An invalid horde number has been specified");
                            return;
                        }

                        Horde.AllHordes[number].Destroy(true, true);
                        SendReply(player, $"You have destroyed zombie horde {number}");
                        return;
                    }
                case "tpto":
                    {
                        if (args.Length != 2 || !int.TryParse(args[1], out int number))
                        {
                            SendReply(player, "You must specify a horde number");
                            return;
                        }

                        if (number < 0 || number >= Horde.AllHordes.Count)
                        {
                            SendReply(player, "An invalid horde number has been specified");
                            return;
                        }

                        player.Teleport(Horde.AllHordes[number].CentralLocation);
                        SendReply(player, $"You have teleported to zombie horde {number}");
                        return;
                    }
                case "create":
                    {
                        float distance = -1;
                        if (args.Length >= 2)
                        {
                            if (!float.TryParse(args[1], out distance))
                            {
                                SendReply(player, "Invalid Syntax!");
                                return;
                            }
                        }

                        string profile = string.Empty;
                        if (args.Length >= 3 && Configuration.HordeProfiles.ContainsKey(args[2]))
                            profile = args[2];

                        if (NavmeshSpawnPoint.Find(player.transform.position, 5f, out Vector3 position))
                        {
                            if (Horde.Create(new Horde.SpawnOrder(position, Configuration.Horde.InitialMemberCount, Configuration.Horde.MaximumMemberCount, distance, profile)))
                            {
                                if (distance > 0)
                                    SendReply(player, $"You have created a zombie horde with a roam distance of {distance}");
                                else SendReply(player, "You have created a zombie horde");

                                return;
                            }
                        }

                        SendReply(player, "Invalid spawn position, move to another more open position. Unable to spawn horde");
                        return;
                    }

                case "createspawn":
                    {
                        int members = Configuration.Horde.InitialMemberCount;
                        if (args.Length >= 2)
                        {
                            if (!int.TryParse(args[1], out members))
                            {
                                SendReply(player, "Invalid Syntax!");
                                return;
                            }
                        }

                        float distance = -1;
                        if (args.Length >= 3)
                        {
                            if (!float.TryParse(args[2], out distance))
                            {
                                SendReply(player, "Invalid Syntax!");
                                return;
                            }
                        }

                        string profile = string.Empty;
                        if (args.Length >= 4 && Configuration.HordeProfiles.ContainsKey(args[3]))
                            profile = args[3];

                        Configuration.Monument.Custom.Add(new ConfigData.MonumentSpawn.CustomSpawnPoints
                        {
                            Enabled = true,
                            HordeSize = members,
                            Location = player.transform.position,
                            Profile = profile,
                            RoamDistance = distance
                        });

                        SaveConfig();

                        if (NavmeshSpawnPoint.Find(player.transform.position, 5f, out Vector3 position))
                        {
                            if (Horde.Create(new Horde.SpawnOrder(position, Configuration.Horde.InitialMemberCount, Configuration.Horde.MaximumMemberCount, distance, profile)))
                            {
                                if (distance > 0)
                                    SendReply(player, $"You have created a custom horde spawn point with a roam distance of {distance}");
                                else SendReply(player, "You have created a custom horde spawn point");

                                return;
                            }
                        }

                        SendReply(player, "Invalid spawn position, move to another more open position");
                        return;
                    }

                case "createloadout":
                    {
                        ConfigData.MemberOptions.Loadout loadout = new ConfigData.MemberOptions.Loadout($"loadout-{Configuration.Member.Loadouts.Count}");

                        for (int i = 0; i < player.inventory.containerBelt.itemList.Count; i++)
                        {
                            Item item = player.inventory.containerBelt.itemList[i];
                            if (item == null || item.amount == 0)
                                continue;

                            loadout.BeltItems.Add(new ConfigData.LootTable.InventoryItem()
                            {
                                Amount = item.amount,
                                Shortname = item.info.shortname,
                                SkinID = item.skin
                            });
                        }

                        for (int i = 0; i < player.inventory.containerMain.itemList.Count; i++)
                        {
                            Item item = player.inventory.containerMain.itemList[i];
                            if (item == null || item.amount == 0)
                                continue;

                            loadout.MainItems.Add(new ConfigData.LootTable.InventoryItem()
                            {
                                Amount = item.amount,
                                Shortname = item.info.shortname,
                                SkinID = item.skin
                            });
                        }

                        for (int i = 0; i < player.inventory.containerWear.itemList.Count; i++)
                        {
                            Item item = player.inventory.containerWear.itemList[i];
                            if (item == null || item.amount == 0)
                                continue;

                            loadout.WearItems.Add(new ConfigData.LootTable.InventoryItem()
                            {
                                Amount = item.amount,
                                Shortname = item.info.shortname,
                                SkinID = item.skin
                            });
                        }

                        Configuration.Member.Loadouts.Add(loadout);
                        SaveConfig();

                        SendReply(player, "Saved your current inventory as a zombie loadout");
                        return;
                    }

                case "hordecount":
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int hordes))
                        {
                            SendReply(player, "You must enter a number");
                            return;
                        }

                        Configuration.Horde.MaximumHordes = hordes;

                        if (Horde.AllHordes.Count < hordes)
                            CreateRandomHordes();
                        SaveConfig();
                        SendReply(player, $"Set maximum hordes to {hordes}");
                        return;
                    }

                case "membercount":
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int members))
                        {
                            SendReply(player, "You must enter a number");
                            return;
                        }

                        Configuration.Horde.MaximumMemberCount = members;
                        SaveConfig();
                        SendReply(player, $"Set maximum horde members to {members}");
                        return;
                    }
                default:
                    SendReply(player, "Invalid Syntax!");
                    break;
            }
        }

        [ConsoleCommand("horde")]
        private void ccmdHorde(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                if (!permission.UserHasPermission(arg.Connection.userid.ToString(), ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "horde info - Show position and information about active zombie hordes");
                SendReply(arg, "horde destroy <number> - Destroy the specified zombie horde");
                SendReply(arg, "horde create <opt:distance> <opt:profile> - Create a new zombie horde at a random position, optionally specifying distance they can roam from the initial spawn point");
                SendReply(arg, "horde addloadout <kitname> <opt:otherkitname> <opt:otherkitname> - Convert the specified kit(s) into loadout(s) (add as many as you want)");
                SendReply(arg, "horde hordecount <number> - Set the maximum number of hordes allowed");
                SendReply(arg, "horde membercount <number> - Set the maximum number of members allowed per horde");
                return;
            }

            switch (arg.Args[0].ToLower())
            {
                case "info":
                    int memberCount = 0;
                    int hordeNumber = 0;
                    foreach (Horde horde in Horde.AllHordes)
                    {
                        memberCount += horde.MemberCount;
                        hordeNumber++;
                    }

                    SendReply(arg, $"There are {Horde.AllHordes.Count} active zombie hordes with a total of {memberCount} zombies");
                    return;
                case "destroy":
                    if (arg.Args.Length != 2 || !int.TryParse(arg.Args[1], out int number))
                    {
                        SendReply(arg, "You must specify a horde number");
                        return;
                    }

                    if (number < 1 || number > Horde.AllHordes.Count)
                    {
                        SendReply(arg, "An invalid horde number has been specified");
                        return;
                    }

                    Horde.AllHordes[number - 1].Destroy(true, true);
                    SendReply(arg, $"You have destroyed zombie horde {number}");
                    return;
                case "create":
                    float distance = -1;
                    if (arg.Args.Length >= 2)
                    {
                        if (!float.TryParse(arg.Args[1], out distance))
                        {
                            SendReply(arg, "Invalid Syntax!");
                            return;
                        }
                    }

                    string profile = string.Empty;
                    if (arg.Args.Length >= 3 && Configuration.HordeProfiles.ContainsKey(arg.Args[2]))
                        profile = arg.Args[2];

                    if (NavmeshSpawnPoint.Find(GetSpawnPoint(), 20f, out Vector3 position) &&
                        Horde.Create(new Horde.SpawnOrder(position, Configuration.Horde.InitialMemberCount, Configuration.Horde.MaximumMemberCount, distance, profile)))
                    {
                        if (distance > 0)
                            SendReply(arg, $"You have created a zombie horde with a roam distance of {distance}");
                        else SendReply(arg, "You have created a zombie horde");
                    }
                    else SendReply(arg, "Invalid spawn position. Unable to spawn horde. Try again for a new random position");

                    return;
                case "addloadout":
                    if (!Kits)
                    {
                        SendReply(arg, "Unable to find the kits plugin");
                        return;
                    }

                    if (arg.Args.Length < 2)
                    {
                        SendReply(arg, "horde addloadout <kitname> <opt:otherkitname> <opt:otherkitname> - Convert the specified kit(s) into loadout(s) (add as many as you want)");
                        return;
                    }

                    for (int i = 1; i < arg.Args.Length; i++)
                    {
                        string kitname = arg.Args[i];
                        object success = Kits.Call("GetKitInfo", kitname);
                        if (success == null)
                        {
                            SendReply(arg, $"Unable to find a kit with the name {kitname}");
                            continue;
                        }

                        JObject obj = success as JObject;
                        JArray items = obj["items"] as JArray;

                        ConfigData.MemberOptions.Loadout loadout = new ConfigData.MemberOptions.Loadout(kitname);

                        for (int y = 0; y < items.Count; y++)
                        {
                            JObject item = items[y] as JObject;
                            string container = (string)item["container"];

                            List<ConfigData.LootTable.InventoryItem> list = container == "belt" ? loadout.BeltItems : container == "main" ? loadout.MainItems : loadout.WearItems;
                            list.Add(new ConfigData.LootTable.InventoryItem
                            {
                                Amount = (int)item["amount"],
                                Shortname = ItemManager.FindItemDefinition((int)item["itemid"])?.shortname,
                                SkinID = (ulong)item["skinid"]
                            });
                        }

                        Configuration.Member.Loadouts.Add(loadout);

                        SendReply(arg, $"Successfully converted the kit {kitname} to a zombie loadout");
                    }

                    SaveConfig();
                    return;

                case "hordecount":
                    if (arg.Args.Length < 2 || !int.TryParse(arg.Args[1], out int hordes))
                    {
                        SendReply(arg, "You must enter a number");
                        return;
                    }

                    Configuration.Horde.MaximumHordes = hordes;

                    if (Horde.AllHordes.Count < hordes)
                        CreateRandomHordes();
                    SaveConfig();
                    SendReply(arg, $"Set maximum hordes to {hordes}");
                    return;

                case "membercount":
                    if (arg.Args.Length < 2 || !int.TryParse(arg.Args[1], out int members))
                    {
                        SendReply(arg, "You must enter a number");
                        return;
                    }

                    Configuration.Horde.MaximumMemberCount = members;
                    SaveConfig();
                    SendReply(arg, $"Set maximum horde members to {members}");
                    return;
                default:
                    SendReply(arg, "Invalid Syntax!");
                    break;
            }
        }

        private float nextCountTime;
        private string cachedString = string.Empty;

        private string GetInfoString()
        {
            if (nextCountTime < Time.time || string.IsNullOrEmpty(cachedString))
            {
                int memberCount = 0;
                Horde.AllHordes.ForEach(x => memberCount += x.MemberCount);
                cachedString = $"There are currently <color=#ce422b>{Horde.AllHordes.Count}</color> hordes with a total of <color=#ce422b>{memberCount}</color> zombies";
                nextCountTime = Time.time + 30f;
            }

            return cachedString;
        }

        [ChatCommand("hordeinfo")]
        private void cmdHordeInfo(BasePlayer player, string command, string[] args) => player.ChatMessage(GetInfoString());

        [ConsoleCommand("hordeinfo")]
        private void ccmdHordeInfo(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                PrintToChat(GetInfoString());
        }

        #endregion

        #region Config      

        public static ConfigData Configuration;

        internal class ConfigData
        {
            [JsonProperty(PropertyName = "Horde Options")]
            public HordeOptions Horde { get; set; }

            [JsonProperty(PropertyName = "Horde Member Options")]
            public MemberOptions Member { get; set; }

            [JsonProperty(PropertyName = "Loot Table")]
            public LootTable Loot { get; set; }

            [JsonProperty(PropertyName = "Monument Spawn Options")]
            public MonumentSpawn Monument { get; set; }

            [JsonProperty(PropertyName = "Timed Spawn Options")]
            public TimedSpawnOptions TimedSpawns { get; set; }

            [JsonProperty(PropertyName = "Horde Profiles (profile name, list of applicable loadouts)")]
            public Dictionary<string, List<string>> HordeProfiles { get; set; }

            public class TimedSpawnOptions
            {
                [JsonProperty(PropertyName = "Only allows spawns during the set time period")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Despawn hordes outside of the set time period")]
                public bool Despawn { get; set; }

                [JsonProperty(PropertyName = "Start time (0.0 - 24.0)")]
                public float Start { get; set; }

                [JsonProperty(PropertyName = "End time (0.0 - 24.0)")]
                public float End { get; set; }
                
                [JsonProperty(PropertyName = "Broadcast notification when hordes start spawning")]
                public bool BroadcastStart { get; set; }
                
                [JsonProperty(PropertyName = "Broadcast notification when hordes start despawning")]
                public bool BroadcastEnd { get; set; }
            }

            public class HordeOptions
            {
                [JsonProperty(PropertyName = "Amount of zombies to spawn when a new horde is created")]
                public int InitialMemberCount { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of spawned zombies per horde")]
                public int MaximumMemberCount { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of hordes at any given time")]
                public int MaximumHordes { get; set; }

                [JsonProperty(PropertyName = "Amount of time from when a horde is destroyed until a new horde is created (seconds)")]
                public int RespawnTime { get; set; }

                [JsonProperty(PropertyName = "Amount of time before a horde grows in size")]
                public int GrowthRate { get; set; }

                [JsonProperty(PropertyName = "Add a zombie to the horde when a horde member kills a player")]
                public bool CreateOnDeath { get; set; }

                [JsonProperty(PropertyName = "Merge hordes together if they collide")]
                public bool MergeHordes { get; set; }

                [JsonProperty(PropertyName = "Spawn system (SpawnsDatabase, Random)")]
                public string SpawnType { get; set; }

                [JsonProperty(PropertyName = "Spawn file (only required when using SpawnsDatabase)")]
                public string SpawnFile { get; set; }

                [JsonProperty(PropertyName = "Amount of time a player needs to be outside of a zombies vision before it forgets about them")]
                public float ForgetTime { get; set; }

                [JsonProperty(PropertyName = "Default roam speed (Slowest, Slow, Normal, Fast)")]
                public string DefaultRoamSpeed { get; set; }

                [JsonProperty(PropertyName = "Force all hordes to roam locally")]
                public bool LocalRoam { get; set; }

                [JsonProperty(PropertyName = "Local roam distance")]
                public float RoamDistance { get; set; }

                [JsonProperty(PropertyName = "Restrict chase distance for local hordes (1.5x the maximum roam distance for that horde)")]
                public bool RestrictLocalChaseDistance { get; set; }

                [JsonProperty(PropertyName = "Use horde profiles for randomly spawned hordes")]
                public bool UseProfiles { get; set; }

                [JsonProperty(PropertyName = "Sense nearby gunshots and explosions")]
                public bool UseSenses { get; set; }
            }

            public class MemberOptions
            {
                [JsonProperty(PropertyName = "Can target animals")]
                public bool TargetAnimals { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by turrets")]
                public bool TargetedByTurrets { get; set; }
                
                [JsonProperty(PropertyName = "Can be targeted by NPC turrets")]
                public bool TargetedByNPCTurrets { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by peacekeeper turrets and NPC turrets")]
                public bool TargetedByPeaceKeeperTurrets { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by Bradley APC")]
                public bool TargetedByAPC { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by other NPCs")]
                public bool TargetedByNPCs { get; set; }

                [JsonProperty(PropertyName = "Can be targeted by animals")]
                public bool TargetedByAnimals { get; set; }

                [JsonProperty(PropertyName = "Can target other NPCs")]
                public bool TargetNPCs { get; set; }

                [JsonProperty(PropertyName = "Can target other NPCs that attack zombies")]
                public bool TargetNPCsThatAttack { get; set; }

                [JsonProperty(PropertyName = "Can target NPCs from HumanNPC")]
                public bool TargetHumanNPCs { get; set; }

                [JsonProperty(PropertyName = "Ignore sleeping players")]
                public bool IgnoreSleepers { get; set; }

                [JsonProperty(PropertyName = "Give all zombies glowing eyes")]
                public bool GiveGlowEyes { get; set; }

                [JsonProperty(PropertyName = "Headshots instantly kill zombie")]
                public bool HeadshotKills { get; set; }
                
                [JsonProperty(PropertyName = "Minimum damage required for a headshot kill")]
                public float MinimumHeadshotDamage { get; set; }

                [JsonProperty(PropertyName = "Kill NPCs that are under water")]
                public bool KillUnderWater { get; set; }

                [JsonProperty(PropertyName = "Can zombies swim across water")]
                public bool CanSwim { get; set; }

                [JsonProperty(PropertyName = "Enable NPC dormant system. This will put NPCs to sleep when no players are nearby to improve performance")]
                public bool EnableDormantSystem { get; set; }

                [JsonProperty(PropertyName = "Zombies make zombie sounds")]
                public bool EnableZombieNoises { get; set; }
                
                [JsonProperty(PropertyName = "Continue to target players who hide in buildings")]
                public bool TargetInBuildings { get; set; }
                
                [JsonProperty(PropertyName = "Throwable explosive building damage multiplier")]
                public float ExplosiveBuildingDamageMultiplier { get; set; }
                
                [JsonProperty(PropertyName = "Maximum explosive throw range")]
                public float MaxExplosiveThrowRange { get; set; }
                
                [JsonProperty(PropertyName = "Despawn dud explosives thrown by Zombies")]
                public bool DespawnDudExplosives { get; set; }
                
                [JsonProperty(PropertyName = "Make dud explosives thrown by Zombies explode anyway")]
                public bool ExplodeDudExplosives { get; set; }

                [JsonProperty(PropertyName = "Don't apply the building damage multiplier if the target is not the owner or authed on the TC")]
                public bool IgnoreBuildingMultiplierNotOwner { get; set; }
                
                [JsonProperty(PropertyName = "Don't apply building damage if the target is not the owner or authed on the TC")]
                public bool DisableBuildingMultiplierNotOwner { get; set; }
                
                [JsonProperty(PropertyName = "Melee weapon building damage multiplier")]
                public float MeleeBuildingDamageMultiplier { get; set; }
                
                [JsonProperty(PropertyName = "Zombies can mount vehicles if target player mounts it")]
                public bool CanMountVehicles { get; set; }
                
                [JsonProperty(PropertyName = "Consume throwable items when using")]
                public bool ConsumeThrowables { get; set; }
                
                [JsonProperty(PropertyName = "Make zombies gingerbread men")]
                public bool GingerBreadZombies { get; set; }
                
                [JsonProperty(PropertyName = "Corpse despawn time (0 is default behavior)")]
                public float CorpseDespawnTime { get; set; }
                
                public List<Loadout> Loadouts { get; set; }

                [JsonIgnore]
                private EntityType _senseTypes = 0;

                public EntityType GetSenseTypes()
                {
                    if (_senseTypes == 0)
                    {
                        _senseTypes |= EntityType.Player;

                        if (TargetNPCs)
                            _senseTypes |= EntityType.BasePlayerNPC;

                        if (TargetAnimals)
                            _senseTypes |= EntityType.NPC;
                    }
                    return _senseTypes;
                }

                public class Loadout
                {
                    public string LoadoutID { get; set; }

                    [JsonProperty(PropertyName = "Potential names for zombies using this loadout (chosen at random)")]
                    public string[] Names { get; set; }

                    [JsonProperty(PropertyName = "Damage multiplier")]
                    public float DamageMultiplier { get; set; }

                    [JsonProperty(PropertyName = "Aim cone scale (for projectile weapons)")]
                    public float AimConeScale { get; set; }

                    public NPCSettings.VitalStats Vitals { get; set; }

                    public ZombieMovementStats Movement { get; set; }

                    public NPCSettings.SensoryStats Sensory { get; set; }

                    public List<LootTable.InventoryItem> BeltItems { get; set; }

                    public List<LootTable.InventoryItem> MainItems { get; set; }

                    public List<LootTable.InventoryItem> WearItems { get; set; }

                    public class ZombieMovementStats : NPCSettings.MovementStats
                    {
                        public override void ApplySettingsToNavigator(BaseNavigator baseNavigator)
                        {
                            base.ApplySettingsToNavigator(baseNavigator);

                            baseNavigator.topologyPreference = (TerrainTopology.Enum)1673010749;
                            
                            if (Configuration.Member.CanSwim)
                            {
                                baseNavigator.SwimmingSpeedMultiplier = 0.4f;
                            }
                        }
                    }

                    [JsonIgnore]
                    private NPCSettings _npcSettings;

                    [JsonIgnore]
                    public NPCSettings NPCSettings
                    {
                        get
                        {
                            if (_npcSettings == null)
                            {
                                _npcSettings = new NPCSettings
                                {
                                    Types = new NPCType[] { Configuration.Member.GingerBreadZombies ? NPCType.GingerBreadMan : NPCType.Scarecrow },
                                    AimConeScale = AimConeScale,
                                    DisplayNames = Names,
                                    Vitals = Vitals,
                                    Movement = Movement,
                                    Sensory = Sensory,
                                    KillUnderWater = !Configuration.Member.CanSwim,
                                    StripCorpseLoot = Configuration.Loot.DropInventory,
                                    DropInventoryOnDeath = Configuration.Loot.DropInventory,
                                    EnableNavMesh = false,
                                    TargetedByNPCTurrets = Configuration.Member.TargetedByNPCTurrets,
                                    DropAlphaLootProfiles = Configuration.Loot.DropAlphaLootProfiles
                                };

                                _npcSettings.Movement.CanSwim = Configuration.Member.CanSwim;
                            }

                            return _npcSettings;
                        }
                    }

                    [JsonIgnore]
                    private static Hash<string, float> _effectiveRangeDefaults = new Hash<string, float>();

                    [JsonIgnore]
                    private static ItemDefinition _glowEyes;

                    [JsonIgnore]
                    public static ItemDefinition GlowEyes
                    {
                        get
                        {
                            if (_glowEyes == null)
                                _glowEyes = ItemManager.FindItemDefinition("gloweyes");
                            return _glowEyes;
                        }
                    }

                    public Loadout()
                    {
                        Names = new string[] { "Zombie" };

                        DamageMultiplier = 1f;

                        AimConeScale = 2f;

                        Vitals = new NPCSettings.VitalStats();

                        Movement = new ZombieMovementStats();

                        Sensory = new NPCSettings.SensoryStats();

                        BeltItems = new List<LootTable.InventoryItem>();
                        MainItems = new List<LootTable.InventoryItem>();
                        WearItems = new List<LootTable.InventoryItem>();
                    }

                    public Loadout(string loadoutID) : this()
                    {
                        LoadoutID = loadoutID;
                    }

                    internal void GiveToPlayer(ZombieNPC zombieNpc)
                    {
                        if (zombieNpc == null)
                            return;

                        zombieNpc.inventory.Strip();

                        foreach (LootTable.InventoryItem inventoryItem in BeltItems)
                        {
                            Item item = inventoryItem.Give(zombieNpc.inventory.containerBelt);

                            if (item != null)
                            {
                                HeldEntity heldEntity = item.GetHeldEntity() as HeldEntity;
                                if (heldEntity != null)
                                {
                                    if (heldEntity is BaseProjectile)
                                    {
                                        if (!_effectiveRangeDefaults.ContainsKey(item.info.shortname))
                                            _effectiveRangeDefaults[item.info.shortname] = (heldEntity as BaseProjectile).effectiveRange;

                                        if (ProjectileEffectiveRange.TryGetValue(item.info.shortname, out float effectiveRange))
                                            (heldEntity as BaseProjectile).effectiveRange = effectiveRange;
                                        else (heldEntity as BaseProjectile).effectiveRange *= 1.25f;
                                    }

                                    if (heldEntity is BaseMelee)
                                    {
                                        if (!_effectiveRangeDefaults.ContainsKey(item.info.shortname))
                                            _effectiveRangeDefaults[item.info.shortname] = (heldEntity as BaseMelee).effectiveRange;

                                        (heldEntity as BaseMelee).effectiveRange *= 1.5f;
                                    }
                                }
                            }
                        }

                       
                        foreach (LootTable.InventoryItem inventoryItem in MainItems)
                            inventoryItem.Give(zombieNpc.inventory.containerMain);

                        if (Configuration.Member.GingerBreadZombies)
                        {
                            Item item = ItemManager.CreateByName("gingerbreadsuit");
                            if (item != null)
                                item.MoveToContainer(zombieNpc.inventory.containerWear);
                        }
                        else
                        {
                            foreach (LootTable.InventoryItem inventoryItem in WearItems)
                                inventoryItem.Give(zombieNpc.inventory.containerWear);

                            if (Configuration.Member.GiveGlowEyes)
                            {
                                Item item = ItemManager.Create(GlowEyes);
                                if (!item.MoveToContainer(zombieNpc.inventory.containerWear))
                                    item.Remove(0f);
                            }
                        }
                    }

                    private static readonly Hash<string, float> ProjectileEffectiveRange = new Hash<string, float>
                    {
                        ["bow.compound"] = 20,
                        ["bow.hunting"] = 20,
                        ["crossbow"] = 20,
                        ["flamethrower"] = 8,
                        ["gun.water"] = 10,
                        ["lmg.m249"] = 150,
                        ["multiplegrenadelauncher"] = 20,
                        ["pistol.eoka"] = 5,
                        ["pistol.m92"] = 15,
                        ["pistol.nailgun"] = 10,
                        ["pistol.python"] = 15,
                        ["pistol.revolver"] = 15,
                        ["pistol.semiauto"] = 15,
                        ["pistol.water"] = 10,
                        ["rifle.ak"] = 30,
                        ["rifle.bolt"] = 80,
                        ["rifle.l96"] = 100,
                        ["rifle.lr300"] = 40,
                        ["rifle.m39"] = 30,
                        ["rifle.semiauto"] = 20,
                        ["rocket.launcher"] = 20,
                        ["shotgun.double"] = 15,
                        ["shotgun.pump"] = 15,
                        ["shotgun.spas12"] = 15,
                        ["shotgun.waterpipe"] = 10,
                        ["smg.2"] = 20,
                        ["smg.mp5"] = 20,
                        ["smg.thompson"] = 20,
                        ["snowballgun"] = 10,
                        ["speargun"] = 10,
                    };

                    public static bool GetDefaultEffectiveRange(string shortname, out float value) => _effectiveRangeDefaults.TryGetValue(shortname, out value);
                }
            }

            public class LootTable
            {
                [JsonProperty(PropertyName = "Drop inventory on death instead of random loot")]
                public bool DropInventory { get; set; }
                
                [JsonProperty(PropertyName = "Drop default murderer loot on death instead of random loot")]
                public bool DropDefault { get; set; }
                
                [JsonProperty(PropertyName = "Drop one of the specified AlphaLoot profiles as loot")]
                public string[] DropAlphaLootProfiles { get; set; }

                [JsonProperty(PropertyName = "Random loot table")]
                public RandomLoot Random { get; set; }

                [JsonProperty(PropertyName = "Dropped inventory item blacklist (shortnames)")]
                public string[] DroppedBlacklist { get; set; }

                public class InventoryItem
                {
                    public string Shortname { get; set; }
                    public ulong SkinID { get; set; }
                    public int Amount { get; set; }

                    [JsonProperty(PropertyName = "Attachments", NullValueHandling = NullValueHandling.Ignore)]
                    public InventoryItem[] SubSpawn { get; set; }

                    public Item Give(ItemContainer itemContainer)
                    {
                        Item item = ItemManager.CreateByName(Shortname, Amount, SkinID);
                        if (item == null)
                            return null;

                        if (!item.MoveToContainer(itemContainer))
                        {
                            item.Remove(0f);
                            return null;
                        }

                        if (item.contents != null && SubSpawn?.Length > 0)
                        {
                            for (int i = 0; i < SubSpawn.Length; i++)
                                SubSpawn[i].Give(item.contents);
                        }

                        return item;
                    }
                }

                public class RandomLoot
                {
                    [JsonProperty(PropertyName = "Minimum amount of items to spawn")]
                    public int Minimum { get; set; }

                    [JsonProperty(PropertyName = "Maximum amount of items to spawn")]
                    public int Maximum { get; set; }

                    public List<LootDefinition> List { get; set; }

                    public class LootDefinition
                    {
                        public string Shortname { get; set; }

                        public string ItemName { get; set; } = string.Empty;
                        
                        public int Minimum { get; set; }

                        public int Maximum { get; set; }

                        public ulong SkinID { get; set; }

                        [JsonProperty(PropertyName = "Spawn as blueprint")]
                        public bool IsBlueprint { get; set; }

                        [JsonProperty(PropertyName = "Probability (0.0 - 1.0)")]
                        public float Probability { get; set; }

                        [JsonProperty(PropertyName = "Minimum condition (0.0 - 1.0)")]
                        public float MinCondition { get; set; } = 1f;

                        [JsonProperty(PropertyName = "Maximum condition (0.0 - 1.0)")]
                        public float MaxCondition { get; set; } = 1f;

                        [JsonProperty(PropertyName = "Spawn with")]
                        public LootDefinition Required { get; set; }

                        [JsonIgnore]
                        private ItemDefinition _blueprintDefinition;

                        [JsonIgnore]
                        private ItemDefinition BlueprintDefinition
                        {
                            get
                            {
                                if (_blueprintDefinition == null)
                                    _blueprintDefinition = ItemManager.FindItemDefinition("blueprintbase");
                                return _blueprintDefinition;
                            }
                        }

                        private int GetAmount()
                        {
                            if (Maximum <= 0f || Maximum <= Minimum)
                                return Minimum;

                            return UnityEngine.Random.Range(Minimum, Maximum);
                        }

                        public void Create(ItemContainer container)
                        {
                            Item item;

                            if (!IsBlueprint)
                                item = ItemManager.CreateByName(Shortname, GetAmount(), SkinID);
                            else
                            {
                                item = ItemManager.Create(BlueprintDefinition);
                                item.blueprintTarget = ItemManager.FindItemDefinition(Shortname).itemid;
                            }

                            if (item != null)
                            {
                                if (!string.IsNullOrEmpty(ItemName))
                                    item.name = ItemName;
                                
                                if (!IsBlueprint)
                                    item.conditionNormalized = UnityEngine.Random.Range(Mathf.Clamp01(MinCondition), Mathf.Clamp01(MaxCondition));

                                item.OnVirginSpawn();
                                if (!item.MoveToContainer(container, -1, true))
                                    item.Remove(0f);
                            }

                            if (Required != null)
                                Required.Create(container);
                        }
                    }
                }
            }

            public class MonumentSpawn
            {
                public MonumentSettings Airfield { get; set; }
                public MonumentSettings Dome { get; set; }
                public MonumentSettings Junkyard { get; set; }
                public MonumentSettings LargeHarbor { get; set; }
                public MonumentSettings GasStation { get; set; }
                public MonumentSettings Powerplant { get; set; }
                public MonumentSettings StoneQuarry { get; set; }
                public MonumentSettings SulfurQuarry { get; set; }
                public MonumentSettings HQMQuarry { get; set; }
                public MonumentSettings Radtown { get; set; }
                public MonumentSettings LaunchSite { get; set; }
                public MonumentSettings Satellite { get; set; }
                public MonumentSettings SmallHarbor { get; set; }
                public MonumentSettings Supermarket { get; set; }
                public MonumentSettings Trainyard { get; set; }
                public MonumentSettings Tunnels { get; set; }
                public MonumentSettings Warehouse { get; set; }
                public MonumentSettings WaterTreatment { get; set; }

                public List<CustomSpawnPoints> Custom { get; set; }

                public class MonumentSettings : SpawnSettings
                {
                    [JsonProperty(PropertyName = "Enable spawns at this monument")]
                    public bool Enabled { get; set; }
                }

                public class CustomSpawnPoints : MonumentSettings
                {
                    public SerializedVector Location { get; set; }

                    public class SerializedVector
                    {
                        public float X { get; set; }
                        public float Y { get; set; }
                        public float Z { get; set; }

                        public bool IsValid => !Mathf.Approximately(X, 0f) || !Mathf.Approximately(Y, 0f) || !Mathf.Approximately(Z, 0f);
                    
                        public SerializedVector() { }

                        public SerializedVector(float x, float y, float z)
                        {
                            this.X = x;
                            this.Y = y;
                            this.Z = z;
                        }

                        public static implicit operator Vector3(SerializedVector v)
                        {
                            return new Vector3(v.X, v.Y, v.Z);
                        }

                        public static implicit operator SerializedVector(Vector3 v)
                        {
                            return new SerializedVector(v.x, v.y, v.z);
                        }
                    }
                }
            }

            public class SpawnSettings
            {
                [JsonProperty(PropertyName = "Distance that this horde can roam from their initial spawn point")]
                public float RoamDistance { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of members in this horde")]
                public int HordeSize { get; set; }

                [JsonProperty(PropertyName = "Horde profile")]
                public string Profile { get; set; }
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
                Horde = new ConfigData.HordeOptions
                {
                    InitialMemberCount = 3,
                    MaximumHordes = 5,
                    MaximumMemberCount = 10,
                    GrowthRate = 300,
                    CreateOnDeath = true,
                    ForgetTime = 10f,
                    MergeHordes = true,
                    RespawnTime = 900,
                    SpawnType = "Random",
                    SpawnFile = "",
                    DefaultRoamSpeed = BaseNavigator.NavigationSpeed.Slow.ToString(),
                    LocalRoam = false,
                    RoamDistance = 150,
                    UseProfiles = false,
                    UseSenses = true
                },
                Member = new ConfigData.MemberOptions
                {
                    IgnoreSleepers = false,
                    TargetAnimals = true,
                    TargetedByAnimals = true,
                    TargetedByNPCs = true,
                    TargetedByTurrets = false,
                    TargetedByNPCTurrets = true,
                    TargetedByAPC = false,
                    TargetNPCs = true,
                    TargetNPCsThatAttack = true,
                    TargetHumanNPCs = false,
                    GiveGlowEyes = true,
                    HeadshotKills = true,
                    MinimumHeadshotDamage = 25f,
                    Loadouts = BuildDefaultLoadouts(),
                    KillUnderWater = true,
                    TargetedByPeaceKeeperTurrets = true,
                    EnableDormantSystem = true,
                    EnableZombieNoises = true,
                    CanSwim = true,
                    CanMountVehicles = true,
                    TargetInBuildings = true,
                    IgnoreBuildingMultiplierNotOwner = true,
                    ExplosiveBuildingDamageMultiplier = 1f,
                    MaxExplosiveThrowRange = 20f,
                    MeleeBuildingDamageMultiplier = 1f,
                    DespawnDudExplosives = true,
                    ExplodeDudExplosives = false
                },
                Loot = new ConfigData.LootTable
                {
                    DropInventory = false,
                    Random = BuildDefaultLootTable(),
                    DropAlphaLootProfiles = Array.Empty<string>(),
                    DroppedBlacklist = new string[] { "exampleitem.shortname1", "exampleitem.shortname2" }
                },
                TimedSpawns = new ConfigData.TimedSpawnOptions
                {
                    Enabled = false,
                    Despawn = true,
                    Start = 18f,
                    End = 6f
                },
                HordeProfiles = new Dictionary<string, List<string>>
                {
                    ["Profile1"] = new List<string> { "loadout-1", "loadout-2", "loadout-3" },
                    ["Profile2"] = new List<string> { "loadout-2", "loadout-3", "loadout-4" },
                },
                Monument = new ConfigData.MonumentSpawn
                {
                    Airfield = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 85,
                        HordeSize = 10,
                        Profile = "",
                    },
                    Dome = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 50,
                        HordeSize = 10,
                    },
                    Junkyard = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 100,
                        HordeSize = 10,
                        Profile = ""
                    },
                    GasStation = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    LargeHarbor = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 120,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Powerplant = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 120,
                        HordeSize = 10,
                        Profile = ""
                    },
                    HQMQuarry = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    StoneQuarry = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    SulfurQuarry = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Radtown = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 85,
                        HordeSize = 10,
                        Profile = ""
                    },
                    LaunchSite = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 140,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Satellite = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 60,
                        HordeSize = 10,
                        Profile = ""
                    },
                    SmallHarbor = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 85,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Supermarket = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 20,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Trainyard = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 100,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Tunnels = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 90,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Warehouse = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 40,
                        HordeSize = 10,
                        Profile = ""
                    },
                    WaterTreatment = new ConfigData.MonumentSpawn.MonumentSettings
                    {
                        Enabled = false,
                        RoamDistance = 120,
                        HordeSize = 10,
                        Profile = ""
                    },
                    Custom = new List<ConfigData.MonumentSpawn.CustomSpawnPoints>()
                    {
                        new ConfigData.MonumentSpawn.CustomSpawnPoints
                        {
                            Enabled = false,
                            HordeSize = 3,
                            Location = new ConfigData.MonumentSpawn.CustomSpawnPoints.SerializedVector
                            {
                                X = 0f,
                                Y = 0f,
                                Z = 0f
                            },
                            Profile = string.Empty,
                            RoamDistance = -1
                        },
                        new ConfigData.MonumentSpawn.CustomSpawnPoints
                        {
                            Enabled = false,
                            HordeSize = 3,
                            Location = new ConfigData.MonumentSpawn.CustomSpawnPoints.SerializedVector
                            {
                                X = 0f,
                                Y = 0f,
                                Z = 0f
                            },
                            Profile = string.Empty,
                            RoamDistance = -1
                        }
                    }
                },
                Version = Version
            };
        }

        private List<ConfigData.MemberOptions.Loadout> BuildDefaultLoadouts()
        {
            List<ConfigData.MemberOptions.Loadout> list = new List<ConfigData.MemberOptions.Loadout>();

            PlayerInventoryProperties[] loadouts = ZombieDefinition.Loadouts;
            if (loadouts != null)
            {
                for (int i = 0; i < loadouts.Length; i++)
                {
                    PlayerInventoryProperties inventoryProperties = loadouts[i];

                    ConfigData.MemberOptions.Loadout loadout = new ConfigData.MemberOptions.Loadout($"loadout-{list.Count}");

                    for (int belt = 0; belt < inventoryProperties.belt.Count; belt++)
                    {
                        PlayerInventoryProperties.ItemAmountSkinned item = inventoryProperties.belt[belt];

                        loadout.BeltItems.Add(new ConfigData.LootTable.InventoryItem() { Shortname = item.itemDef.shortname, SkinID = item.skinOverride, Amount = (int)item.amount });
                    }

                    for (int main = 0; main < inventoryProperties.main.Count; main++)
                    {
                        PlayerInventoryProperties.ItemAmountSkinned item = inventoryProperties.main[main];

                        loadout.MainItems.Add(new ConfigData.LootTable.InventoryItem() { Shortname = item.itemDef.shortname, SkinID = item.skinOverride, Amount = (int)item.amount });
                    }

                    for (int wear = 0; wear < inventoryProperties.wear.Count; wear++)
                    {
                        PlayerInventoryProperties.ItemAmountSkinned item = inventoryProperties.wear[wear];

                        loadout.WearItems.Add(new ConfigData.LootTable.InventoryItem() { Shortname = item.itemDef.shortname, SkinID = item.skinOverride, Amount = (int)item.amount });
                    }

                    list.Add(loadout);
                }
            }
            return list;
        }

        private ConfigData.LootTable.RandomLoot BuildDefaultLootTable()
        {
            ConfigData.LootTable.RandomLoot randomLoot = new ConfigData.LootTable.RandomLoot();

            randomLoot.Minimum = 3;
            randomLoot.Maximum = 9;
            randomLoot.List = new List<ConfigData.LootTable.RandomLoot.LootDefinition>();

            LootContainer.LootSpawnSlot[] loot = ZombieDefinition.LootSpawns;
            if (loot != null)
            {
                for (int i = 0; i < loot.Length; i++)
                {
                    LootContainer.LootSpawnSlot lootSpawn = loot[i];

                    for (int y = 0; y < lootSpawn.definition.subSpawn.Length; y++)
                    {
                        LootSpawn.Entry entry = lootSpawn.definition.subSpawn[y];

                        for (int c = 0; c < entry.category.items.Length; c++)
                        {
                            ItemAmountRanged itemAmountRanged = entry.category.items[c];

                            ConfigData.LootTable.RandomLoot.LootDefinition lootDefinition = new ConfigData.LootTable.RandomLoot.LootDefinition();
                            lootDefinition.Probability = lootSpawn.probability;
                            lootDefinition.Shortname = itemAmountRanged.itemDef.shortname;
                            lootDefinition.Minimum = (int)itemAmountRanged.amount;
                            lootDefinition.Maximum = (int)itemAmountRanged.maxAmount;
                            lootDefinition.SkinID = 0;
                            lootDefinition.IsBlueprint = itemAmountRanged.itemDef.spawnAsBlueprint;
                            lootDefinition.Required = null;

                            randomLoot.List.Add(lootDefinition);
                        }
                    }
                }
            }
            return randomLoot;
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (Configuration.Version < new Core.VersionNumber(0, 2, 0))
                Configuration = baseConfig;

            if (Configuration.Version < new Core.VersionNumber(0, 2, 1))
                Configuration.Loot.Random = baseConfig.Loot.Random;

            if (Configuration.Version < new Core.VersionNumber(0, 2, 2))
            {
                for (int i = 0; i < Configuration.Member.Loadouts.Count; i++)
                    Configuration.Member.Loadouts[i].LoadoutID = $"loadout-{i}";

                Configuration.Horde.LocalRoam = false;
                Configuration.Horde.RoamDistance = 150;
                Configuration.Horde.UseProfiles = false;

                Configuration.HordeProfiles = baseConfig.HordeProfiles;

                Configuration.Monument.Airfield.Profile = string.Empty;
                Configuration.Monument.Dome.Profile = string.Empty;
                Configuration.Monument.GasStation.Profile = string.Empty;
                Configuration.Monument.HQMQuarry.Profile = string.Empty;
                Configuration.Monument.Junkyard.Profile = string.Empty;
                Configuration.Monument.LargeHarbor.Profile = string.Empty;
                Configuration.Monument.LaunchSite.Profile = string.Empty;
                Configuration.Monument.Powerplant.Profile = string.Empty;
                Configuration.Monument.Radtown.Profile = string.Empty;
                Configuration.Monument.Satellite.Profile = string.Empty;
                Configuration.Monument.SmallHarbor.Profile = string.Empty;
                Configuration.Monument.StoneQuarry.Profile = string.Empty;
                Configuration.Monument.SulfurQuarry.Profile = string.Empty;
                Configuration.Monument.Supermarket.Profile = string.Empty;
                Configuration.Monument.Trainyard.Profile = string.Empty;
                Configuration.Monument.Tunnels.Profile = string.Empty;
                Configuration.Monument.Warehouse.Profile = string.Empty;
                Configuration.Monument.WaterTreatment.Profile = string.Empty;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 2, 13))
                Configuration.TimedSpawns = baseConfig.TimedSpawns;

            if (Configuration.Version < new Core.VersionNumber(0, 2, 18))
                Configuration.Member.TargetedByPeaceKeeperTurrets = Configuration.Member.TargetedByTurrets;

            if (Configuration.Version < new Core.VersionNumber(0, 2, 30))
            {
                if (Configuration.Horde.SpawnType == "RandomSpawns" || Configuration.Horde.SpawnType == "Default")
                    Configuration.Horde.SpawnType = "Random";
            }

            if (Configuration.Version < new Core.VersionNumber(0, 2, 31))
            {
                if (string.IsNullOrEmpty(Configuration.Horde.SpawnType))
                    Configuration.Horde.SpawnType = "Random";
            }

            if (Configuration.Version < new Core.VersionNumber(0, 3, 0))
            {
                Configuration.Horde.UseSenses = true;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 3, 5))
            {
                Configuration.Loot.DroppedBlacklist = baseConfig.Loot.DroppedBlacklist;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 4, 0))
            {
                foreach (ConfigData.MemberOptions.Loadout loadout in Configuration.Member.Loadouts)
                {
                    loadout.AimConeScale = 2f;

                    loadout.Movement = new ConfigData.MemberOptions.Loadout.ZombieMovementStats
                    {
                        Speed = 6.2f,
                        Acceleration = 12f,
                        TurnSpeed = 120f,
                        FastSpeedFraction = 1f,
                        NormalSpeedFraction = 0.5f,
                        SlowSpeedFraction = 0.3f,
                        SlowestSpeedFraction = 0.16f,
                        LowHealthMaxSpeedFraction = 0.5f
                    };

                    loadout.Sensory = new NPCSettings.SensoryStats
                    {
                        AttackRangeMultiplier = 1.5f,
                        IgnoreNonVisionSneakers = true,
                        IgnoreSafeZonePlayers = true,
                        ListenRange = 20f,
                        SenseRange = 30f,
                        TargetLostRange = 40f,
                        VisionCone = 135f
                    };
                }

                if (Configuration.Loot.DroppedBlacklist == null)
                    Configuration.Loot.DroppedBlacklist = baseConfig.Loot.DroppedBlacklist;

                Configuration.Horde.DefaultRoamSpeed = BaseNavigator.NavigationSpeed.Slow.ToString();
                Configuration.Member.EnableDormantSystem = true;
                Configuration.Member.TargetAnimals = true;
                Configuration.Member.TargetedByNPCs = true;
                Configuration.Member.TargetedByPeaceKeeperTurrets = true;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 4, 2))
                Configuration.Member.TargetedByAnimals = true;

            if (Configuration.Version < new Core.VersionNumber(0, 4, 8))
            {
                if (Configuration.Monument.Custom == null)
                    Configuration.Monument.Custom = baseConfig.Monument.Custom;

                Configuration.Member.CanSwim = true;
                Configuration.Member.KillUnderWater = false;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 5, 5))
                Configuration.Member.EnableZombieNoises = true;

            if (Configuration.Version < new VersionNumber(0, 6, 0))
            {
                Configuration.Member.CanMountVehicles = true;
                Configuration.Member.ExplosiveBuildingDamageMultiplier = 1f;
                Configuration.Member.MeleeBuildingDamageMultiplier = 1f;
                Configuration.Member.TargetInBuildings = true;
                Configuration.Member.IgnoreBuildingMultiplierNotOwner = false;
            }

            if (Configuration.Version < new VersionNumber(0, 6, 2))
            {
                Configuration.Member.DespawnDudExplosives = true;
                Configuration.Member.ExplodeDudExplosives = false;
            }

            if (Configuration.Version < new VersionNumber(0, 6, 5))
                Configuration.Member.TargetedByNPCTurrets = true;

            if (Configuration.Version < new VersionNumber(0, 6, 9))
                Configuration.Member.MaxExplosiveThrowRange = 20;

            if (Configuration.Version < new VersionNumber(0, 6, 11))
                Configuration.Loot.DropAlphaLootProfiles = Array.Empty<string>();

            if (Configuration.Version < new VersionNumber(0, 6, 13))
                Configuration.Member.MinimumHeadshotDamage = 25f;
            
            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion
    }
}