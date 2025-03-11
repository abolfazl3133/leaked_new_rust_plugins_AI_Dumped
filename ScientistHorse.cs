using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Collections;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("ScientistHorse", "https://discord.gg/TrJ7jnS233", "1.0.5")]
    [Description("This plugin was fixed by k to order [Rust Plugin Sliv]: f")]
    internal class ScientistHorse : RustPlugin
    {
        #region Static

        private Configuration _config;
        private List<RiderInWorld> Riders = new List<RiderInWorld>();
        private Timer _timer;
        private List<Vector3> positions = new List<Vector3>();
        private const int scanHeight = 100;
        private static int getBlockMask => LayerMask.GetMask("Construction", "Prevent Building", "Water");
        private static bool MaskIsBlocked(int mask) => getBlockMask == (getBlockMask | (1 << mask));

        #region Classes

        private class RiderInWorld
        {
            public RidableHorse horse;
            public BasePlayer npc;
        }
        
        private class Rider
        {
            [JsonProperty(PropertyName = "Prefab of NPC")]
            public string NPCPrefab;

            [JsonProperty(PropertyName = "List of armor for NPC", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> armor;

            [JsonProperty(PropertyName = "Prefab of Horse")]
            public string horsePrefab;

            [JsonProperty(PropertyName = "Horse armor")]
            public string horseArmor;

            [JsonProperty(PropertyName = "Scan radius for Rider(Find player)")]
            public int scanRadius;
        }
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Bonus damage to NPCs on horseback")]
            public float bonusDamage = 6;

            [JsonProperty(PropertyName = "Maximum Riders population")]
            public int maxPopulation = 10;

            [JsonProperty(PropertyName = "Kill horse after NPC death")]
            public bool DeathTogether = true;

            [JsonProperty(PropertyName = "Rider types", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Rider> Riders = new List<Rider>
            {
                new Rider
                {
                    NPCPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_lr300.prefab",
                    horsePrefab = "assets/rust.ai/nextai/testridablehorse.prefab",
                    horseArmor = "horse.armor.roadsign",
                    scanRadius = 100,
                    armor = new List<string>{"hazmatsuit"}
                }
            };
        }

        #endregion

        #endregion

        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region OxideHooks
        
        private void OnServerInitialized()
        {
            GeneratePositions();
            _timer = timer.Every(60, () =>
            {
                while (Riders.Count < _config.maxPopulation) SpawnRider(_config.Riders.GetRandom(), positions.GetRandom());
            });
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || player.userID.IsSteamId() || !player.isMounted) return;
            info.damageTypes.ScaleAll(_config.bonusDamage);
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || player.userID.IsSteamId()) return;
            foreach (var check in Riders.ToArray())
            {
                if (check.npc.userID != player.userID) continue; 
                if (_config.DeathTogether) check.horse?.Kill();
                else check.horse?.GetComponent<RiderAI>().Kill();
                Riders.Remove(check);
            }
        }

        private void Unload()
        {
            _timer?.Destroy();
            foreach (var check in Riders)
            {
                if (check.npc != null) check.npc.Kill();
                if (check.horse != null) check.horse.Kill();
            }
        }

        #endregion

        #region Functions

        [ChatCommand("spawnrider")]
        private void cmdChatspawnrider(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            SpawnRider(_config.Riders.GetRandom(), player.transform.position);
        }
        private void GeneratePositions()
        {
            positions.Clear();
            var generationSuccess = 0;
            var islandSize = ConVar.Server.worldsize / 2;
            for (var i = 0; i < 500; i++)
            {
                if (generationSuccess >= 500) break;
                var x = Core.Random.Range(-islandSize, islandSize);
                var z = Core.Random.Range(-islandSize, islandSize);
                var original = new Vector3(x, scanHeight, z);
                var position = GetClosestValidPosition(original);
                if (position == new Vector3()) continue;
                positions.Add(position);
                generationSuccess++;
            }
        }

        private Vector3 GetClosestValidPosition(Vector3 original)
        {
            var target = original - new Vector3(0, 200, 0);
            RaycastHit hitInfo;
            if (Physics.Linecast(original, target, out hitInfo) == false) return new Vector3();
            var position = hitInfo.point;
            var collider = hitInfo.collider;
            var colliderLayer = collider?.gameObject.layer ?? 4;
            if (collider == null) return new Vector3();
            if (MaskIsBlocked(colliderLayer) || colliderLayer != 23) return new Vector3();
            return IsValidPosition(position) == false ? new Vector3() : position;
        }

        private Vector3 GetValidSpawnPoint()
        {
            for (var i = 0; i < 25; i++)
            {
                var number = Core.Random.Range(0, positions.Count);
                var position = positions.ElementAt(number);
                if (IsValidPosition(position)) return position;
                positions.Remove(position);
            }

            return new Vector3();
        }

        private static bool IsValidPosition(Vector3 position)
        {
            var entities = new List<BuildingBlock>();
            Vis.Entities(position, 25, entities);
            return entities.Count == 0;
        }

private void SpawnRider(Rider rider, Vector3 position)
{
    var npc = (ScientistNPC)GameManager.server.CreateEntity(rider.NPCPrefab, position);
    npc.Spawn();
    npc.GetComponent<BaseNavigator>().CanUseNavMesh = false;
    npc.inventory.containerWear.Clear();
    foreach (var check in rider.armor)
    {
        ItemManager.CreateByName(check).MoveToContainer(npc.inventory.containerWear);
    }

    var horse = (RidableHorse)GameManager.server.CreateEntity(rider.horsePrefab, position);
    horse.Spawn();

    horse.EquipmentUpdate();
    (horse.children.FirstOrDefault(x => x.PrefabName.Contains("saddle")) as BaseMountable).MountPlayer(npc);

    var comp = horse.gameObject.AddComponent<RiderAI>();
    comp._rider = rider;
    comp.npc = npc;
    comp.load = true;
    Riders.Add(new RiderInWorld { npc = npc, horse = horse });
}
        
        private class RiderAI : FacepunchBehaviour
        {
            private BasePlayer _target;
            private Coroutine _coroutine;
            private BaseRidableAnimal _horse;
            public Rider _rider;
            public bool load = false;
            public BasePlayer npc;
            
            private void Start()
            {
                _horse = GetComponent<BaseRidableAnimal>();
                StartCoroutine(FindPath());
            }

            private IEnumerator FindPath()
            {
                while (load) yield return new WaitForSeconds(Think());
            }

            private float Think()
            {
                if (_horse == null || _horse.IsDead() || npc == null || npc.IsDead())
                {
                    load = false;
                    Destroy(this);
                    return 0;
                }
                if (_target == null)
                {
                    FindTarget();
                    if (_target != null) return 0.5f;
                    StopMove();
                    StopRotate();
                    return 5;
                }
                if (_target == null || _target.IsSleeping() || !_target.IsConnected || _target.IsDead() || _target.IsSwimming() || _target.InSafeZone())
                {
                    _target = null;
                    return 5;
                }
                _horse.lastInputTime = Time.time;
                _horse.staminaSeconds = _horse.maxStaminaSeconds;
                RotateToTarget();
                MoveToTarget();
                return 0.5f;
            }

            private void MoveToTarget()
            {
                var distance = GetDistance();
                if (distance > _rider.scanRadius)
                {
                    _target = null;
                    return;
                }
                
                if (distance > distance * 0.85)
                {
                    StartRun();
                    return;
                }
                StartWalk();
            }

            private void RotateToTarget()
            {
                var angle = GetAngle();
                if (angle > 0)
                {
                    if (angle < 35)
                    {
                        StopRotate();
                        return;
                    }
                    RotateRight();
                    return;
                }

                if (angle > -35)
                {
                    StopRotate();
                    return;
                }
                
                RotateLeft();
            }

            private float GetDistance() => Vector3.Distance(_target.transform.position, transform.position);
            private float GetAngle() => Vector3.SignedAngle(_target.transform.position - transform.position, transform.forward, Vector3.up);
            private void StartRun() => _horse.ModifyRunState(2);
            private void StartWalk() => _horse.ModifyRunState(1);
            private void StopMove() => _horse.ModifyRunState(0);
            private void RotateLeft() => _horse.desiredRotation = 1;
            private void RotateRight() =>_horse.desiredRotation = -1;
            private void StopRotate() =>_horse.desiredRotation = 0;
            
            private void FindTarget()
            {
                var players = Facepunch.Pool.GetList<BasePlayer>();
                Vis.Entities(_horse.transform.position, _rider.scanRadius, players);
                foreach (var check in players.ToArray()) if (check.IsSleeping() || !check.userID.IsSteamId() || !check.IsConnected || check.IsDead() || check.IsSwimming() || check.InSafeZone()) players.Remove(check);
                if (players.Count > 0) _target = players[0];
                Facepunch.Pool.FreeList(ref players);
            }

            public void Kill() => Destroy(this);
            private void OnDestroy()
            {
                load = false;
                StopCoroutine(FindPath());
            }
        }
        
        #endregion
    }
}