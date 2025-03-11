/*
                                   /\  /\  /\
     TTTTT  H   H  EEEE     K  K  |  \/  \/  |  NN   N   GGG
       T    H   H  E        K K   *----------*  N N  N  G
       T    HHHHH  EEE      KK     I  I  I  I   N  N N  G  GG
       T    H   H  E        K K    I  I  I  I   N   NN  G   G
       T    H   H  EEEE     K  K   I  I  I  I   N    N   GGG


This plugin (the software) is © copyright the_kiiiing.

You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without explicit consent from the_kiiiing.

DISCLAIMER:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Facepunch;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using PluginComponents.FlyingCargoShip;
using PluginComponents.FlyingCargoShip.Core;
using PluginComponents.FlyingCargoShip.Extensions.BaseNetworkable;
using PluginComponents.FlyingCargoShip.Extensions.DDraw;
using PluginComponents.FlyingCargoShip.External.NpcSpawn;
using PluginComponents.FlyingCargoShip.Loot;
using PluginComponents.FlyingCargoShip.Tools.Entity;
using PluginComponents.FlyingCargoShip.Tools.Space;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PluginComponents.FlyingCargoShip.LoottableApi;
using PluginComponents.FlyingCargoShip.MapMarker;
using PluginComponents.FlyingCargoShip.SpawnPoint;
using PluginComponents.FlyingCargoShip.Extensions.Lang;
using System.Diagnostics.CodeAnalysis;

/*
 * VERSION HISTORY
 * 
 * V 1.0.1
 * - fix NRE in CanEntityTakeDamage
 * 
 * V 1.0.2
 *  update map marker
 *  add config option to disable air card requirement
 *  add custom name to loot item
 *  flying cargo can now spawn without control center if no valid spawn point was found
 *  
 *  V 1.0.3
 *  fix conflict with Space plugin
 *  add map marker color option
 *  removed redundant loot table config option
 *  
 *  V 1.0.4
 *  loottable api update
 *  add config options for map marker text and air card name
 *  
 *  V 1.0.5
 *  revert map marker changes
 *  update harbor spawn positions
 *  
 *  V 1.0.6
 *  skipped idk
 *  
 *  V 1.0.7
 *  remove IsExternalInit as it is included in Oxide now
 *  marker size now adjusts with map size
 *  fix path height issues
 *  fix ship spawn at map center when no position was provided
 *  add config option to disable control center
 *  add config option to spawn control center outside monuments
 *  
 *  V 1.0.8
 *  fix issues with helicopter service ceiling
 *
 *  V 1.0.9
 *  fix balloons disappearing
 *  fix control center appearing after server reboot
 *  add npc attack range multiplier to config
 *  fix no loot on ship when air card is disabled
 *
 *  V 1.0.10
 *  remove anti air turrets from save list
 *  fix control center npc loot config not applied
 *  replace deprecated pool methods
 *
 *  V 1.0.11
 *  add configurable chat prefix
 *  misc fixes
 *
 * V 1.0.12
 * fix NRE when one of the balloons is destroyed
 * fix control center spawn points
 * 
 * V 1.0.13
 * fix for December update
 *
 */

namespace Oxide.Plugins
{
    [Info(nameof(FlyingCargoShip), "The_Kiiiing", "1.0.13")]
    internal class FlyingCargoShip : BasePlugin<FlyingCargoShip, FlyingCargoShip.Configuration>
    {
        private const string PERM_ADMIN = "flyingcargoship.admin";

        private const float SERVICE_CEILING_OFFSET = 80f;
        private const float FLY_HEIGHT = 200f;
        private const ulong ANTI_AIR_IGNORE_SKIN = 3151401879ul;

        private const int CARD_ITEM_ID = -484206264;
        private const ulong CARD_SKIN = 3151859094ul;

        protected override Color ChatColor => new(51f / 255f, 236f / 255f, 251f / 255f, 1);
        protected override string ChatPrefix => Config.overrideChatPrefix ?? base.ChatPrefix;

        private readonly CircularPathFinder pathFinder;
        private readonly LoottableApi loottableApi;
        private readonly SpawnPointManager controlCenterSpawnPointManager;

        private Timer eventTimer;
        private AirCargoController controller;

        [PluginReference, UsedImplicitly]
        private readonly Plugin NpcSpawn;

        public FlyingCargoShip()
        {
            loottableApi = new LoottableApi(this);
            pathFinder = new CircularPathFinder();

            controlCenterSpawnPointManager = new SpawnPointManager(5);
            controlCenterSpawnPointManager.Configure(c =>
            {
                
                // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
                c.BlockTopology(SpawnPointManager.TP_DEFAULT | SpawnPointManager.TP_ROAD);
                // ReSharper restore BitwiseOperatorOnEnumWithoutFlags
                c.MinDistanceToBuildings = 80f;
                c.TopologyRadius = 10f;
                c.ValidHeight = new MinMaxB<float>{ Min = 5f };
            });
        }

        #region Hooks

        protected override void Init()
        {
            base.Init();

            permission.RegisterPermission(PERM_ADMIN, this);

            Unsubscribe();
        }

        protected override void Unload()
        {
            controller?.Destroy();
            eventTimer?.Destroy();

            base.Unload();
        }

        protected override void OnServerInitialized()
        {
            base.OnServerInitialized();
            
            pathFinder.GeneratePath(FLY_HEIGHT, () =>
            {
                var ceiling = Mathf.Max(HotAirBalloon.serviceCeiling, pathFinder.MaxHeight + SERVICE_CEILING_OFFSET);
                HotAirBalloon.serviceCeiling = ceiling;
                Log($"Set helicopter service ceiling to {ceiling:N1}m (default 200m)");

                ScheduleEvent();
            });

            InvokeHandler.Instance.StartCoroutine(controlCenterSpawnPointManager.CacheSpawnPoints());
        }

        protected override void OnServerInitializedDelayed()
        {
            base.OnServerInitializedDelayed();

            if (NpcSpawn == null)
            {
                LogError("NpcSpawn is required to spawn NPCs - you can download it here: https://codefling.com/extensions/npc-spawn");
            }

            loottableApi.AddCustomItem(CARD_ITEM_ID, CARD_SKIN, Config.airCardDisplayName);

            loottableApi.ClearPresets();

            loottableApi.CreatePresetCategory("Crates");
            loottableApi.CreatePreset("c_locked", "Locked Crate", "crate_hackable");
            loottableApi.CreatePreset("c_elite", "Elite Crate", "crate_elite");
            loottableApi.CreatePreset("c_military", "Military Crate", "crate_military");
            loottableApi.CreatePreset("c_normal", "Normal Crate", "crate_normal");

            loottableApi.CreatePresetCategory("NPCs");
            loottableApi.CreatePreset(true, "npc_cargo", "Cargo Ship NPC", "npc_militunnel");
            loottableApi.CreatePreset(true, "npc_control", "Control Center NPC", "npc_militunnel");
        }

        void OnShipDestroyed()
        {
            controller = null;
            ScheduleEvent();

            Unsubscribe();

            Interface.CallHook("OnFlyingCargoEnd");
        }

        void Subscribe()
        {
            Subscribe(nameof(OnCardSwipe));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnButtonPress));
            Subscribe(nameof(OnLootEntityEnd));
            Subscribe(nameof(OnPlayerDeath));
            Subscribe(nameof(CanEntityTakeDamage));

            Subscribe(nameof(OnCorpsePopulate));
            Subscribe(nameof(OnCustomLootNPC));
            Subscribe(nameof(CanPopulateLoot));
        }

        void Unsubscribe()
        {
            Unsubscribe(nameof(OnCardSwipe));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnButtonPress));
            Unsubscribe(nameof(OnLootEntityEnd));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(CanEntityTakeDamage));

            Unsubscribe(nameof(OnCorpsePopulate));
            Unsubscribe(nameof(OnCustomLootNPC));
            Unsubscribe(nameof(CanPopulateLoot));
        }

        #region Entity

        object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (controller != null && controller?.CardReader == cardReader)
            {
                if (card.skinID == CARD_SKIN)
                {
                    controller!.OpenSecurityDoors(player);
                    card.GetItem()?.Remove();
                }
                else
                {
                    lang.SendMessage("air_card_required", player);
                    return true;
                }
            }

            return null;
        }

        void OnEntitySpawned(ScientistNPC npc)
        {
            if (!npc.IsNullOrDestroyed() && controller != null && (npc.prefabID == 881071619 || npc.prefabID == 1639447304 || npc.prefabID == 3623670799))
            {
                controller.OnCargoNpcSpawn(npc);
            }
        }

        object OnButtonPress(PressButton button, BasePlayer player)
        {
            if (controller != null && controller?.ControlCenter?.TurretButton == button)
            {
                controller!.ControlCenterDisableTurrets(player);
            }

            return null;
        }

        void OnLootEntityEnd(BasePlayer player, HackableLockedCrate crate)
        {
            if (controller != null)
            {
                controller.OnCrateLooted(crate);
            }
        }

        object OnPlayerDeath(ScientistNPC npc, HitInfo info)
        {
            if (npc.IsNpc && controller != null && npc.net != null && controller.ContainsNpc(npc.net.ID.Value))
            {
                controller.OnNpcDeath(npc);
            }

            return null;
        }

        // TruePVE
        object CanEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (info?.Initiator?.skinID == ANTI_AIR_IGNORE_SKIN)
            {
                return true;
            }

            return null;
        }

        #endregion

        #region Loot

        void OnCorpsePopulate(ScientistNPC npc, NPCPlayerCorpse corpse)
        {
            if (!npc.IsValid() || !corpse.IsValid())
            {
                return;
            }

            if (controller != null && controller.NpcHasCustomLoot(npc.net.ID.Value))
            {
                NextFrame(() =>
                {
                    if (!corpse.IsValid())
                    {
                        LogError($"Failed to populate NPC corpse ({npc.displayName}) - invalid corpse");
                        return;
                    }

                    var lootTable = controller.IsControlCenterNpc(npc) ? Config.controlCenterNpcLootTable : Config.shipLootConfig.npcLootTable;
                    LootManager.FillWithLoot(corpse.containers[0], lootTable);

                    // Drop bag
                    if (npc.clanId > 0 && !corpse.IsDestroyed)
                    {
                        corpse.Kill();
                    }
                });
            }
        }

        // Loottable
        object OnCorpsePopulate(LootableCorpse corpse)
        {
            if (controller != null && corpse?.parentEnt?.net != null && controller.NpcHasCustomLoot(corpse.parentEnt.net.ID.Value))
            {
                return true;
            }

            return null;
        }

        // CustomLoot
        object OnCustomLootNPC(ulong netId)
        {
            if (controller != null && controller.NpcHasCustomLoot(netId))
            {
                return true;
            }

            return null;
        }

        // Alpha loot
        object CanPopulateLoot(LootContainer container)
        {
            if (controller != null && controller.IsEventLootContainer(container))
            {
                return false;
            }

            return null;
        }

        #endregion

        #endregion

        #region Spawn

        [ConsoleCommand("fcargo"), UsedImplicitly]
        void CmdAc(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                arg.ReplyWith("No permission");
                return;
            }

            var arg1 = arg.GetString(0);

            if (arg1 == "start")
            {
                if (controller != null)
                {
                    arg.ReplyWith("Failed to start event, it is already running");
                }
                else
                {
                    SpawnCargo(player?.transform.position ?? default);
                }
            }
            else if (arg1 == "stop")
            {
                Stop();
            }
            else
            {
                arg.ReplyWith("Invalid args \\-(`o`)-/ Usage: fcargo start, fcargo stop");
            }
        }

        private void ScheduleEvent()
        {
            if (!Config.scheduleEvent)
            {
                return;
            }

            if (eventTimer != null && !eventTimer.Destroyed)
            {
                eventTimer.DestroyToPool();
            }

            Log($"Schedule event in {Config.eventDelayMinutes}min");
            eventTimer = timer.In(Config.eventDelayMinutes * 60, Start);
        }

        void Start()
        {
            if (controller != null)
            {
                Log("Cancel scheduled event, the event is already running");
                ScheduleEvent();
                return;
            }

            SpawnCargo();
        }

        void Stop()
        {
            if (controller != null)
            {
                controller.Destroy();
                controller = null;
            }
        }

        void SpawnCargo(Vector3 position = default)
        {
            if (position == default)
            {
                position = TerrainMeta.RandomPointOffshore();
            }

            position = position.WithY(FLY_HEIGHT);

            // IMPORTANT: call before spawn
            Subscribe();

            var ship = EntityTools.CreateEntity<CargoShip>("assets/content/vehicles/boats/cargoship/cargoshiptest.prefab", position, Quaternion.LookRotation(position.WithY(0) * -1f));
            ship.Spawn();
            ship.transform.position = position;

            controller = ship.gameObject.AddComponent<AirCargoController>();
            controller.PathFinder = pathFinder;
            controller.PathFinder.NodeSwitchDistance = 80f;

            Interface.CallHook("OnFlyingCargoStart");
        }

        #if DEBUG
        [ConsoleCommand("at"), UsedImplicitly]
        void CmdAt(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            var turret = EntityTools.CreateCustomEntity<NPCAutoTurret, AntiAirAutoTurret>("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", player.transform.position);
            turret.Spawn();
        }
        #endif

        #endregion

        //[ConsoleCommand("mon")]
        //void CmdMon(ConsoleSystem.Arg arg)
        //{
        //    var player = arg.Player();

        //    var monument = TerrainMeta.Path.Monuments.Where(x => x.Type == MonumentType.Radtown).OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position)).First();

        //    Log($"{monument.name} {monument.Type} {monument.transform.InverseTransformPoint(player.transform.position)}");
        //}

        #region Controllers

        class ControlCenterController : FacepunchBehaviour
        {
            public AirCargoController Controller { get; set; }

            public readonly HashSet<ulong> scientistIds = new();
            public readonly List<BaseEntity> entities = new();

            private BaseEntity Entity => GetComponent<BaseEntity>();

            public PressButton TurretButton { get; private set; }

            private CustomMapMarker mapMarker;

            private float endTime;
            private bool despawning;

            void Awake()
            {
                SpawnNpcs(8f, Config.controlCenterNpcCount);
                SpawnEntities();
                CreateMapMarker();
            }

            public void SetEndTime(float endTime)
            {
                this.endTime = endTime;
            }

            public void ScheduleDespawn()
            {
                if (!despawning)
                {
                    despawning = true;
                    LogDebug("Schedule control center despawn");          
                    endTime = Time.time + Config.controlCenterDestroyTime;
                    Invoke(Destroy, Config.controlCenterDestroyTime);
                }
            }

            private void SpawnNpcs(float distance, int count)
            {
                var delta = 360 / count;
                for (int i = 0; i < count; i++)
                {
                    var position = transform.position + Quaternion.Euler(0, i * delta, 0) * Vector3.forward * distance;
                    if (GetGroundHeight(ref position, Rust.Layers.Solid))
                    {
                        CreateNpc(Config.controlCenterNpcConfig, position);
                    }
                    else
                    {
                        LogError($"Failed to spawn NPC - failed to get ground height");
                    }
                }
            }

            private void SpawnEntities()
            {
                if (Config.enableAirCard)
                {
                    var cardBox = EntityTools.CreateEntity<LootContainer>("assets/bundled/prefabs/radtown/dmloot/dm c4.prefab", transform.position + transform.rotation * new Vector3(1f, 0, 1.7f), transform.rotation);
                    cardBox.minSecondsBetweenRefresh = 0;
                    cardBox.maxSecondsBetweenRefresh = 0;
                    cardBox.Spawn();

                    cardBox.inventory.Clear();
                    ItemManager.DoRemoves();

                    var card = ItemManager.CreateByItemID(CARD_ITEM_ID, 1, CARD_SKIN);
                    card.name = Config.airCardDisplayName;
                    if (!card.MoveToContainer(cardBox.inventory))
                    {
                        LogError("Failed to move card to loot box");
                    }

                    entities.Add(cardBox);
                }

                TurretButton = EntityTools.CreateEntity<PressButton>("assets/prefabs/io/electric/switches/pressbutton/pressbutton.prefab", transform.position + transform.rotation * new Vector3(1.9f, 0.2f, -0.3f), transform.rotation);
                TurretButton.Spawn();
                entities.Add(TurretButton);
            }

            private void CreateMapMarker()
            {
                if (!ColorUtility.TryParseHtmlString(Config.controlCenterMarkerColor, out var markerColor))
                {
                    LogWarning("Invalid color for Control Center map marker");
                    markerColor = Instance.ChatColor;
                }

                mapMarker = CustomMapMarker.Create(Entity.transform.position, 0.2f, markerColor, 2);
                mapMarker.SetText(0, Config.controlCenterMarkerText);

                InvokeRepeating(UpdateMapMarker, 1, 5);
            }

            private void UpdateMapMarker()
            {
                var seconds = Mathf.FloorToInt((endTime - Time.time) % 60);
                var minutes = Mathf.FloorToInt((endTime - Time.time) / 60);

                mapMarker.SetText(1, $"{minutes:N0}m {seconds:N0}s");
            }

            public void AddFoundations(float elevation)
            {
                const int ext = 2;
                var layers = Mathf.CeilToInt(elevation / 3f);
                
                for (int x = -ext; x <= ext; x++)
                {
                    for (int z = -ext; z <= ext; z++)
                    {
                        var foundation = EntityTools.CreateEntity<BuildingBlock>("assets/prefabs/building core/foundation/foundation.prefab", transform.position + new Vector3(x * 3, -0.1f, z * 3), transform.rotation);
                        foundation.grounded = true;
                        foundation.Spawn();
                        foundation.ChangeGradeAndSkin(BuildingGrade.Enum.Stone, 10225);

                        entities.Add(foundation);
                    }
                }

                for (int layer = 1; layer < layers; layer++)
                {
                    for (int rot = 0; rot < 360; rot += 90)
                    {
                        entities.AddRange(WallRow(transform, layer, rot));
                    }
                }

                static IEnumerable<BuildingBlock> WallRow(Transform transform, int layer, float rotY)
                {
                    for (int i = -ext; i <= ext; i++)
                    {
                        var rot = Quaternion.Euler(0, rotY, 0);
                        var wall = EntityTools.CreateEntity<BuildingBlock>("assets/prefabs/building core/wall/wall.prefab", transform.position + rot * new Vector3(ext * 3 + 1.5f, (layer + 1) * -3 - 0.1f, i * 3), transform.rotation * rot);
                        wall.grounded = true;
                        wall.Spawn();
                        wall.ChangeGradeAndSkin(BuildingGrade.Enum.Stone, 10225);

                        yield return wall;
                    }

                }
            }

            void OnDestroy()
            {
                mapMarker?.Destroy();

                foreach (var ent in entities)
                {
                    if (!ent.IsNullOrDestroyed())
                    {
                        ent.Kill();
                    }
                }

                Controller?.OnControlCenterDestroyed();
            }

            public void Destroy()
            {
                if (!Entity.IsNullOrDestroyed())
                {
                    Entity.Kill();
                }
                else
                {
                    Destroy(this);
                }
            }

            #region Npc

            private void CreateNpc(NpcConfig config, Vector3 position)
            {
                if (Instance.NpcSpawn == null)
                {
                    LogError("Failed to spawn npc - NpcSpawn is not loaded");
                    return;
                }

                NpcSpawnConfig npcConfig = new NpcSpawnConfig
                {
                    Name = config.name,
                    WearItems = config.clothing.Select(x => new NpcSpawnNpcWear { ShortName = x.shortName, SkinID = x.skinId }),
                    BeltItems = config.belt.Select(x => new NpcSpawnNpcBelt { ShortName = x.shortName, Amount = x.amount, SkinID = x.skinId, Ammo = string.Empty, Mods = Array.Empty<string>() }),
                    Kit = config.kit,
                    Health = config.health,
                    RoamRange = config.roamRange,
                    ChaseRange = config.chaseRange,
                    SenseRange = config.senseRange,
                    ListenRange = config.senseRange / 2f,
                    AttackRangeMultiplier = config.attackRangeMultiplier,
                    VisionCone = config.visionCone,
                    DamageScale = config.damageScale,
                    TurretDamageScale = 1f,
                    AimConeScale = 1f,
                    DisableRadio = !config.enableRadio,
                    CanRunAwayWater = true,
                    CanSleep = false,
                    Speed = 6f,
                    AreaMask = 1,
                    AgentTypeID = -1372625422,
                    HomePosition = position.ToString(),
                    MemoryDuration = config.memoryDuration,
                    States = new HashSet<string> { NpcSpawnStates.ROAM, NpcSpawnStates.CHASE, NpcSpawnStates.COMBAT, }
                };

                var scientist = Instance.NpcSpawn?.Call("SpawnNpc", position, JObject.FromObject(npcConfig)) as ScientistNPC;
                if (scientist == null)
                {
                    LogError("Failed to spawn npc - scientist is null");
                    return;
                }

                if (config.removeCorpse)
                {
                    scientist.clanId = 1;
                }

                entities.Add(scientist);
                scientistIds.Add(scientist.net.ID.Value);

                Instance?.loottableApi.AssignPreset(scientist, "npc_control");
            }

            private bool GetGroundHeight(ref Vector3 position, int layerMask = -1, float rayHeight = 400f)
            {
                if (Physics.Raycast(position.WithY(rayHeight), Vector3.down, out var hit, rayHeight, layerMask, QueryTriggerInteraction.Ignore))
                {
                    position.y = hit.point.y;
                    return true;
                }

                return false;
            }

            #endregion

            #region Spawn points

            private static readonly Dictionary<string, (Vector3, float)> spawnPositions = new Dictionary<string, (Vector3, float)>
            {
                { "train_yard_display_name", (new Vector3(41.26f, 0.20f, -78.74f), 0) },
                { "launchsite", (new Vector3(69.38f, 2.97f, 25.66f), 0f) },
                { "airfield_display_name", (new Vector3(-131.78f, 0.01f, -62.98f), 0f) },
                { "water_treatment_plant_display_name", (new Vector3(96.73f, 0.26f, -85.93f), 0f) },
                { "satellite_dish_display_name", (new Vector3(-38.01f, 0.10f, 27.22f), 0f) },
                { "excavator", (new Vector3(-35.48f, 0.02f, -105.39f), 0f) },
                { "military_tunnels_display_name", (new Vector3(-36.65f, 30f, -26.86f), -90f) },
                { "junkyard_display_name", (new Vector3(54.5f, 0.10f, 28.08f), 10f) },
                { "ferryterminal", (new Vector3(22.33f, 5.08f, 15.15f), 0f) },
                { "harbor_2_display_name", (new Vector3(-43.66f, 4.00f, -97.62f), 0f) },
                { "harbor_display_name", (new Vector3(60.27f, 4.08f, -52.40f), 18.4f) },
                { "dome_monument_name", (new Vector3(-41.46f, 5.75f, -6.36f), 258.2f) },
            };

            public static bool GetSpawnPoint(out Vector3 position, out Quaternion rotation, out bool isMonument, out float elevation)
            {
                rotation = Quaternion.identity;
                elevation = 0f;

                if (!Config.controlCenterAtMonuments)
                {
                    isMonument = false;
                    if (Instance.controlCenterSpawnPointManager.TryGetRandomSpawnPoint(out position))
                    {
                        var maxHeight = 0f;
                        var minHeight = 10_000f;
                        const int a = 8;
                        for (int x = -a; x <= a; x++)
                        {
                            for (int z = -a; z <= a; z++)
                            {
                                var height = TerrainMeta.HeightMap.GetHeight(position + new Vector3(x, 0, z));
                                if (height > maxHeight)
                                {
                                    maxHeight = height;
                                }
                                if (height < minHeight)
                                {
                                    minHeight = height;
                                }
                            }
                        }

                        elevation = maxHeight - minHeight;
                        position.y = maxHeight;
                        return true;
                    }

                    return false;
                }

                isMonument = true;
                position = default;

                var monuments = TerrainMeta.Path.Monuments
                    .Where(x => spawnPositions.ContainsKey(x.displayPhrase.token))
                    .ToList();

                if (monuments.Count < 1)
                {
                    return false;
                }
                else
                {
                    var monument = monuments.GetRandom();
                    var (localPos, localY) = spawnPositions[monument.displayPhrase.token];

                    (position, rotation) = SpaceTools.LocalToWorld(monument.transform.position, monument.transform.forward, localPos, new Vector3(0, localY, 0));

                    LogDebug($"Control center spawned at {monument.displayPhrase.english}");

                    return true;
                }
            }

            #endregion
        }

        class AirCargoController : FacepunchBehaviour
        {
            private const float SPEED = 8f; // default is 8
            private const float TURN_SPEED = 2.5f; // default is 2.5
            private const float ELEVATOR_SPEED = 1f;

            private readonly float maxCoords;

            public bool IsDestroyed => Ship?.IsDestroyed ?? true;

            public CircularPathFinder PathFinder { get; set; }

            public CargoShip Ship { get; private set; }
            public CardReader CardReader { get; private set; }
            public ControlCenterController ControlCenter { get; private set; }

            private readonly HashSet<BaseEntity> entities = new();
            private readonly HashSet<HotAirBalloon> balloons = new();

            private readonly HashSet<ulong> scientistIds = new();

            private CustomMapMarker mapMarker;

            private int crateCount;
            private bool doorsOpen;
            private bool egressing;
            private float endTime;

            private int targetNodeIndex = -1;

            private Vector3 nodePosition;
            private BasePlayer debugPlayer;

            private bool _lootSpawned;

            #region Mono

            AirCargoController()
            {
                maxCoords = World.Size / 2f + 1500;
            }

            void Awake()
            {
                Ship = GetComponent<CargoShip>();

                Ship.CancelInvoke(Ship.FindInitialNode);
                Ship.CancelInvoke(Ship.BuildingCheck);
                Ship.CancelInvoke(Ship.StartEgress);
                Ship.CancelInvoke(Ship.RespawnLoot);

                KillRhib();

                endTime = Time.time + Config.eventDurationMinutes * 60;

                CreateMapMarker();
                SpawnEntities();
                SpawnControlCenter();

                if (ControlCenter != null)
                {
                    Instance?.lang.BroadcastMessage("ship_incoming", MapHelper.PositionToString(ControlCenter.transform.position));
                }
                else
                {
                    Instance?.lang.BroadcastMessage("ship_incoming_2");
                }
                

                Invoke(Egress, Config.eventDurationMinutes * 60);

#if DEBUG
                debugPlayer = BasePlayer.activePlayerList.FirstOrDefault();
                InvokeRepeating(() => debugPlayer?.DrawSphere(nodePosition, 20f, Color.red, 2f), 1, 2f);
#endif
            }

            void FixedUpdate()
            {
                foreach (var balloon in balloons)
                {
                    if (!balloon.IsNullOrDestroyed())
                    {
                        balloon.myRigidbody.isKinematic = true;
                        balloon.inflationLevel = 10000;
                    }
                }

                if (PathFinder == null)
                {
                    return;
                }

                if (targetNodeIndex < 0)
                {
                    targetNodeIndex = PathFinder.GetNearestNodeIndex(transform.position);
                }
                
                if (egressing)
                {
                    if (Mathf.Abs(transform.position.x) > maxCoords || Mathf.Abs(transform.position.z) > maxCoords)
                    {
                        Destroy();
                        return;
                    }

                    nodePosition = transform.position + transform.position.WithY(0).normalized * 1000f;
                }
                else
                {
                    nodePosition = PathFinder.GetNextNode(transform.position, ref targetNodeIndex);
                }

                var normalized = (nodePosition - transform.position).normalized;

                float up = transform.position.y < nodePosition.y ? 1f : -0.5f;
                float multi = egressing ? 2 : 1;

                float dot = Vector3.Dot(transform.forward, normalized);
                float num = Mathf.InverseLerp(0f, 1f, dot);
                float num2 = Vector3.Dot(transform.right, normalized);
                float num3 = TURN_SPEED;
                float b = Mathf.InverseLerp(0.05f, 0.5f, Mathf.Abs(num2));
                Ship.turnScale = Mathf.Lerp(Ship.turnScale, b, Time.deltaTime * 0.2f);
                float num4 = ((!(num2 < 0f)) ? 1 : (-1));
                Ship.currentTurnSpeed = num3 * Ship.turnScale * num4;
#pragma warning disable IDE0002
                // ReSharper disable once RedundantNameQualifier
                transform.Rotate(Vector3.up, Time.deltaTime * Ship.currentTurnSpeed, UnityEngine.Space.World);
#pragma warning restore
                Ship.currentThrottle = Mathf.Lerp(Ship.currentThrottle, num, Time.deltaTime * 0.2f);
                Ship.currentVelocity = (transform.forward * (SPEED * Ship.currentThrottle) + Vector3.up * (ELEVATOR_SPEED * up)) * multi;
                transform.position += Ship.currentVelocity * Time.deltaTime;
            }

            void OnDestroy()
            {
                foreach (var entity in entities)
                {
                    if (!entity.IsNullOrDestroyed())
                    {
                        entity.Kill();
                    }
                }

                mapMarker?.Destroy();

                ControlCenter?.Destroy();

                Instance?.OnShipDestroyed();
            }

            #endregion

            #region Api

            public bool IsEventLootContainer(LootContainer container)
            {
                return entities.Contains(container);
            }

            public void OnCargoNpcSpawn(ScientistNPC npc)
            {
                Invoke(() => OnCargoNpcSpawnIntl(npc), 0.5f);
            }

            public void OpenSecurityDoors(BasePlayer player)
            {
                if (player != null)
                {
                    Instance?.lang.BroadcastMessage("ship_door_opened", player.displayName);
                }

                if (!doorsOpen)
                {
                    doorsOpen = true;

                    foreach (var ent in entities)
                    {
                        if (ent is AntiAirAutoTurret turret)
                        {
                            turret.SetOffline();
                        }
                        else if (ent is Door door)
                        {
                            door.SetOpen(true, true);
                        }
                    }

                    SpawnLoot();
                    PlayHorn();
                }
            }

            private void DisableTurrets()
            {
                foreach (var ent in entities)
                {
                    if (ent is AntiAirAutoTurret turret)
                    {
                        turret.SetOffline();
                    }
                }
            }

            public void ControlCenterDisableTurrets(BasePlayer player)
            {
                Instance?.lang.BroadcastMessage("ship_turrets_disabled", player.displayName);

                DisableTurrets();

                PlayHorn();
                if (!Config.enableAirCard)
                {
                    SpawnLoot();
                }

                ControlCenter?.ScheduleDespawn();
            }

            public void OnCrateLooted(HackableLockedCrate crate)
            {
                if (entities.Remove(crate) && crateCount > 0)
                {
                    LogDebug($"crate count is {crateCount - 1}");
                    if (--crateCount <= 0)
                    {
                        endTime = Time.time + Config.shipLeaveDelayAfterLooted;
                        TimeRemaining(out var min, out var sec);
                        Instance?.lang.BroadcastMessage("ship_looted", min, sec);
                        Invoke(Egress, Config.shipLeaveDelayAfterLooted);
                    }
                }
            }

            #endregion

            #region Spawn

            private void SpawnParachute()
            {
                SpawnItem("parachute", new Vector3(-1.9f, 11f, -54f), Quaternion.Euler(-90, 0, 0));
                SpawnItem("parachute", new Vector3(-2.5f, 11f, -54f), Quaternion.Euler(-90, 0, 0));

                void SpawnItem(string shortname, Vector3 position, Quaternion rotation)
                {
                    var item = ItemManager.CreateByName(shortname);

                    var droppedItem = (DroppedItem)item.CreateWorldObject(position, rotation, Ship);
                    droppedItem.EnableSaving(true);

                    Destroy(droppedItem.GetComponent<PhysicsEffects>());
                    Destroy(droppedItem.GetComponent<EntityCollisionMessage>());

                    var rigidbody = droppedItem.GetComponent<Rigidbody>();
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    rigidbody.isKinematic = true;

                    droppedItem.StickIn();
                    droppedItem.CancelInvoke(droppedItem.IdleDestroy);
                }
            }

            private void SpawnControlCenter()
            {
                if (!Config.controlCenterEnabled || !ControlCenterController.GetSpawnPoint(out var position, out var rotation, out var isAtMonument, out var elevation))
                {
                    if (Config.controlCenterEnabled)
                    {
                        LogWarning($"Failed to get spawn point for control center");
                    }

                    DisableTurrets();
                    return;
                }

                var ent = EntityTools.CreateEntity<BaseEntity>("assets/prefabs/misc/desertbasedwelling/desert_dwelling_single_k.prefab", position, rotation);
                ent.Spawn();

                ControlCenter = ent.gameObject.AddComponent<ControlCenterController>();
                ControlCenter.Controller = this;
                ControlCenter.SetEndTime(endTime);
                if (!isAtMonument)
                {
                    ControlCenter.AddFoundations(elevation);
                }
            }

            private void SpawnEntities()
            {
                if (!Config.enableAirCard)
                {
                    SpawnLoot();
                }
                
                foreach (var prefab in Prefabs)
                {
                    if (!Config.enableAirCard && (prefab.PrefabName == "assets/bundled/prefabs/static/door.hinged.security.blue.prefab" || prefab.PrefabName == "assets/prefabs/io/electric/switches/cardreader.prefab"))
                    {
                        continue;
                    }

                    BaseEntity entity;
                    if (prefab.PrefabName == "assets/content/props/sentry_scientists/sentry.scientist.static.prefab")
                    {
                        entity = EntityTools.CreateCustomEntity<NPCAutoTurret, AntiAirAutoTurret>(prefab.PrefabName, prefab.Position, Quaternion.Euler(prefab.EulerAngles));
                    }
                    else
                    {
                        entity = EntityTools.CreateEntity<BaseEntity>(prefab.PrefabName, prefab.Position, Quaternion.Euler(prefab.EulerAngles));
                    }

                    entity.SetParent(Ship);
                    entity.Spawn();

                    if (entity is HotAirBalloon balloon)
                    {
                        balloon.skinID = ANTI_AIR_IGNORE_SKIN;
                        balloon.myRigidbody.isKinematic = true;

                        var armor = EntityTools.CreateEntity<BaseEntity>("assets/prefabs/deployable/hot air balloon/hotairballoon_armor_t1.prefab");
                        armor.SetParent(balloon);
                        armor.Spawn();

                        balloons.Add(balloon);
                    }
                    else if (entity is CardReader cardReader)
                    {
                        cardReader.accessLevel = 2;
                        cardReader.SetFlag(cardReader.AccessLevel1, cardReader.accessLevel == 1);
                        cardReader.SetFlag(cardReader.AccessLevel2, cardReader.accessLevel == 2);
                        cardReader.SetFlag(cardReader.AccessLevel3, cardReader.accessLevel == 3);

                        cardReader.SetFlag(IOEntity.Flag_HasPower, true);

                        CardReader = cardReader;
                    }

                    entities.Add(entity);
                }
            }

            private void SpawnLoot()
            {
                if (_lootSpawned)
                {
                    return;
                }
                _lootSpawned = true;
                
                var spawnPoints = new Queue<Transform>(Ship.crateSpawns.Where(x => x.localPosition.y < 5f));

                SpawnCrates(Ship.lockedCratePrefab.resourcePath, Config.shipLootConfig.lockedCrateCount, Config.shipLootConfig.lockedCrateLootTable, "c_locked");
                SpawnCrates(Ship.eliteCratePrefab.resourcePath, Config.shipLootConfig.eliteCrateCount, Config.shipLootConfig.eliteCrateLootTable, "c_elite");
                SpawnCrates(Ship.militaryCratePrefab.resourcePath, Config.shipLootConfig.militaryCrateCount, Config.shipLootConfig.militaryCrateLootTable, "c_military");
                SpawnCrates(Ship.junkCratePrefab.resourcePath, Config.shipLootConfig.normalCrateCount, Config.shipLootConfig.normalCrateLootTable, "c_normal");

                crateCount = Config.shipLootConfig.lockedCrateCount;

                void SpawnCrates(string prefabName, int count, LootManager.LootTable lootTable, string loottableKey)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (!spawnPoints.TryDequeue(out var spawnPoint))
                        {
                            break;
                        }

                        var container = GameManager.server.CreateEntity(prefabName, spawnPoint.localPosition, spawnPoint.localRotation) as LootContainer;
                        if (container)
                        {
                            container.enableSaving = false;
                            container.SendMessage("SetWasDropped", SendMessageOptions.DontRequireReceiver);
                            container.SetParent(Ship);
                            container.Spawn();

                            var rigidbody = container.GetComponent<Rigidbody>();
                            if (rigidbody != null)
                            {
                                rigidbody.isKinematic = true;
                            }

                            entities.Add(container);

                            if (Instance?.loottableApi.AssignPreset(container, loottableKey) == true)
                            {
                                continue;
                            }

                            if (lootTable != null && lootTable.Enabled)
                            {
                                LootManager.FillWithLoot(container, lootTable);
                            }
                        }
                    }
                }
            }

            #endregion

            #region NPC

            public bool IsControlCenterNpc(ScientistNPC npc)
            {
                if (ControlCenter != null && npc.net?.ID != null)
                {
                    return ControlCenter.scientistIds.Contains(npc.net.ID.Value);
                }

                return false;
            }

            public bool ContainsNpc(ulong netId)
            {
                return scientistIds.Contains(netId) || (ControlCenter?.scientistIds.Contains(netId) ?? false);
            }

            public bool NpcHasCustomLoot(ulong netId)
            {
                if (scientistIds.Contains(netId))
                {
                    return Config.shipLootConfig.npcLootTable.Enabled;
                }
                if (ControlCenter?.scientistIds.Contains(netId) ?? false)
                {
                    return Config.controlCenterNpcLootTable.Enabled;
                }

                return false;
            }

            public void OnNpcDeath(ScientistNPC npc)
            {
                entities.Remove(npc);
                ControlCenter?.entities.Remove(npc);
                // Don't remove net id, its needed for loot check
            }

            private void OnCargoNpcSpawnIntl(ScientistNPC npc)
            {
                if (npc.GetParentEntity() != Ship)
                {
                    LogDebug("npc is not parented");
                    return;
                }

                var config = npc.prefabID switch
                {
                    3623670799 => Config.cargoNpcNormalConfig,  // cargo
                    1639447304 => Config.cargoNpcTowerConfig,   // turret_any
                    881071619 => Config.cargoNpcTurretLrConfig, // turret_lr300
                    _ => null
                };

                if (config != null)
                {
                    var shouldDuplicate = npc.prefabID == 3623670799 && Config.shipSpawnDuplicateNpcs;
                    ReplaceNpc(npc, config, !shouldDuplicate);
                    if (shouldDuplicate)
                    {
                        Invoke(() => ReplaceNpc(npc, config, true), 1);
                    }
                }
            }

            private void ReplaceNpc(ScientistNPC npc, NpcConfig config, bool kill)
            {
                var isTurret = npc.prefabID != 3623670799;
                var newNpc = SpawnNpc(npc.transform.position, config, isTurret);
                Instance?.NpcSpawn?.Call("SetParentEntity", newNpc, Ship, Ship.transform.InverseTransformPoint(npc.transform.position));
                if (newNpc != null)
                {
                    Invoke(() => SetupNpc(newNpc, isTurret), 0.1f);
                }

                void SetupNpc(ScientistNPC newNpc, bool isTurret)
                {
                    newNpc.Brain.Navigator.CanUseNavMesh = false;
                    if (!isTurret)
                    {
                        newNpc.Brain.Navigator.AStarGraph = npc.Brain.Navigator.AStarGraph;
                        newNpc.Brain.Navigator.CanUseAStar = true;
                    }

                    if (kill && !npc.IsDestroyed)
                    {
                        npc.Kill();
                    }
                }
            }

            private ScientistNPC SpawnNpc(Vector3 position, NpcConfig config, bool isTurret)
            {
                if (Instance.NpcSpawn == null)
                {
                    LogError("Failed to spawn npc - NpcSpawn is not loaded");
                    return null;
                }

                NpcSpawnConfig npcConfig = new NpcSpawnConfig
                {
                    Name = config.name,
                    WearItems = config.clothing.Select(x => new NpcSpawnNpcWear { ShortName = x.shortName, SkinID = x.skinId }),
                    BeltItems = config.belt.Select(x => new NpcSpawnNpcBelt { ShortName = x.shortName, Amount = x.amount, SkinID = x.skinId, Ammo = string.Empty, Mods = new string[0] }),
                    Kit = config.kit,
                    Health = config.health,
                    RoamRange = config.roamRange,
                    ChaseRange = config.chaseRange,
                    SenseRange = config.senseRange,
                    ListenRange = config.senseRange / 2f,
                    AttackRangeMultiplier = config.attackRangeMultiplier,
                    VisionCone = config.visionCone,
                    DamageScale = config.damageScale,
                    TurretDamageScale = 1f,
                    AimConeScale = 1f,
                    DisableRadio = !config.enableRadio,
                    CanRunAwayWater = false,
                    CanSleep = false,
                    Speed = 6f,
                    AreaMask = 25,
                    AgentTypeID = 0,
                    HomePosition = string.Empty,
                    MemoryDuration = config.memoryDuration,
                    States = isTurret ? new HashSet<string> { NpcSpawnStates.IDLE, NpcSpawnStates.COMBAT_STATIONARY } : new HashSet<string> { NpcSpawnStates.ROAM, NpcSpawnStates.CHASE, NpcSpawnStates.COMBAT }
                };

                var scientist = Instance.NpcSpawn?.Call("SpawnNpc", position, JObject.FromObject(npcConfig)) as ScientistNPC;
                if (scientist == null)
                {
                    LogError("Failed to spawn npc - scientist is null");
                    return null;
                }

                if (config.removeCorpse)
                {
                    scientist.clanId = 1;
                }

                entities.Add(scientist);
                scientistIds.Add(scientist.net.ID.Value);

                Instance?.loottableApi.AssignPreset(scientist, "npc_cargo");

                return scientist;
            }

            #endregion

            #region Map marker

            public void TimeRemaining(out int minutes, out int seconds)
            {
                seconds = Mathf.FloorToInt((endTime - Time.time) % 60);
                minutes = Mathf.FloorToInt((endTime - Time.time) / 60);
            }

            public void CreateMapMarker()
            {
                if (Ship.mapMarkerInstance)
                {
                    Ship.mapMarkerInstance.Kill();
                }

                if (!ColorUtility.TryParseHtmlString(Config.shipMarkerColor, out var markerColor))
                {
                    LogWarning("Invalid color for Ship map marker");
                    markerColor = Instance.ChatColor;
                }

                mapMarker = CustomMapMarker.Create(Ship, new Vector3(0, 0, 10), 0.4f, markerColor, 2);
                mapMarker.SetText(Config.shipMarkerText);

                InvokeRepeating(UpdateMapMarker, 1, 2);
            }

            void UpdateMapMarker()
            {
                TimeRemaining(out var minutes, out var seconds);

                if (minutes >= 0 && seconds >= 0)
                {
                    mapMarker.SetText(1, $"{minutes}m {seconds}s");
                }
                else
                {
                    mapMarker.SetText(1, $"leaving");
                }

                mapMarker.SendNetworkUpdate();
            }

            #endregion

            private void Egress()
            {
                CancelInvoke(Egress);
                if (!egressing)
                {
                    egressing = true;
                    Ship.radiation.SetActive(value: true);
                    Ship.SetFlag(BaseEntity.Flags.Reserved8, b: true);
                    Ship.InvokeRepeating(Ship.UpdateRadiation, Config.shipRadiationDelay, 1f);
                    ControlCenter?.Destroy();
                }
            }

            private void PlayHorn()
            {
                Ship.InvokeRepeating(Ship.PlayHorn, 0f, 8f);
            }

            public void OnControlCenterDestroyed()
            {
                LogDebug("control center destroyed");
                ControlCenter = null;
            }

            public void Destroy()
            {
                Ship?.Kill();
            }

            private void KillRhib()
            {
                foreach (var child in Ship.children)
                {
                    if (child.prefabID == 2226588638)
                    {
                        child.Kill();
                        break;
                    }
                }
            }

            #region Prefabs

            private readonly IReadOnlyCollection<Prefab> Prefabs = new Prefab[]
            {
                //new Prefab("assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", new Vector3(-5f, 28f, -40.5f), new Vector3(0, 0, 30)),
                //new Prefab("assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", new Vector3(5f, 28f, -40.5f), new Vector3(0, 0, -30)),
                new("assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", new Vector3(0, 27f, 69f)),
                new("assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", new Vector3(0, 36f, -42f)),

                new("assets/bundled/prefabs/static/door.hinged.security.blue.prefab", new Vector3(-7.5f, 6.67f, -33.25f), new Vector3(0, -90, 0)),
                new("assets/bundled/prefabs/static/door.hinged.security.blue.prefab", new Vector3(7.5f, 6.67f, -33.25f), new Vector3(0, -90, 0)),
                new("assets/bundled/prefabs/static/door.hinged.security.blue.prefab", new Vector3(-7.5f, 6.67f, 57.25f), new Vector3(0, 90, 0)),
                new("assets/bundled/prefabs/static/door.hinged.security.blue.prefab", new Vector3(7.5f, 6.67f, 57.25f), new Vector3(0, 90, 0)),

                new("assets/prefabs/io/electric/switches/cardreader.prefab", new Vector3(3.25f, 24.5f, -42.75f), new Vector3(0, 90, 0)),

                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(-10f, 27.5f, -41f)),
                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(10f, 27.5f, -41f)),
                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(-6f, 9.5f, 67f)),
                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(6f, 9.5f, 67f)),
                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(-11.1f, 6.5f, 24f), new Vector3(0, -90, 0)),
                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(11.1f, 6.5f, 24f), new Vector3(0, 90, 0)),
                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(-11.1f, 6.5f, -3), new Vector3(0, -90, 0)),
                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(11.1f, 6.5f, -3), new Vector3(0, 90, 0)),

                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(10.6f, 12.5f, -48.5f), new Vector3(0, 90, 0)),
                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(-10.6f, 12.5f, -48.5f), new Vector3(0, -90, 0)),

                new("assets/content/props/sentry_scientists/sentry.scientist.static.prefab", new Vector3(1.85f, 11f, -55.9f), new Vector3(0, 180, 0)),
            };

            record Prefab(string PrefabName, Vector3 Position, Vector3 EulerAngles = default);

            #endregion
        }

        #endregion

        #region Anti Air Sentry

        class AntiAirAutoTurret : NPCAutoTurret
        {
            private const string MUZZLE_FLASH_PREFAB = "assets/prefabs/npc/sam_site_turret/effects/tube_launch.prefab";
            private const string PROJECTILE_PREFAB = "assets/prefabs/npc/sam_site_turret/rocket_sam.prefab";

            private const float PROJECTILE_SPEED = 65 + 10;

            const float SCAN_RADIUS = 100f;
            const float BURST_DELAY = 2f;
            const int BURST_SIZE = 6;

            SamSite.ISamSiteTarget currentTarget;
            float nextBurstTime;
            Vector3 currentAimDir = Vector3.forward;
            int firedCount;
            float lockOnTime;

            public override void ServerInit()
            {
                base.ServerInit();

                skinID = ANTI_AIR_IGNORE_SKIN;

                InvokeRandomized(AirTargetScan, 1f, 3f, 1f);
            }

            public override bool IsEntityHostile(BaseCombatEntity ent)
            {
                if (currentTarget != null)
                {
                    return false;
                }

                var player = ent as BasePlayer;
                if (player != null)
                {
                    return !player.IsNpc;
                }

                return false;
            }

            public void SetOffline()
            {
                SetFlag(Flags.Busy, true);
                SetIsOnline(false);
            }

            private bool HasValidTarget()
            {
                return !ObjectEx.IsUnityNull(currentTarget);
            }

            private void AirTargetScan()
            {
                if (!IsOn())
                {
                    return;
                }

                

                var targetList = Pool.Get<List<SamSite.ISamSiteTarget>>();
                if (Interface.CallHook("OnSamSiteTargetScan", this, targetList) == null)
                {
                    Vis.Entities(transform.position, SCAN_RADIUS, targetList, 32768, QueryTriggerInteraction.Ignore);
                }

                //LogDebug($"scan targets {targetList.Count}");
                SamSite.ISamSiteTarget samSiteTarget = null;
                foreach (var target in targetList)
                {
                    //LogDebug($"r {target.SAMTargetType?.scanRadius ?? -1}");
                    if (!target.isClient
                        //&& target.SAMTargetType != null
                        //&& target.IsVisible(muzzleLeft.transform.position, target.SAMTargetType.scanRadius * 2f) 
                        && target.IsVisible(muzzleLeft.transform.position, SCAN_RADIUS)
                        && target.IsValidSAMTarget(true) && Interface.CallHook("OnSamSiteTarget", this, target) == null 
                        && (target as BaseEntity)?.skinID != ANTI_AIR_IGNORE_SKIN 
                       )
                    {
                        samSiteTarget = target;
                        break;
                    }
                }

                Pool.FreeUnmanaged(ref targetList);

                if (HasValidTarget() && currentTarget != samSiteTarget)
                {
                    lockOnTime = Time.time + 0.5f;
                }

                currentTarget = samSiteTarget;
                
                if (ObjectEx.IsUnityNull(currentTarget))
                {
                    CancelInvoke(WeaponTick);
                }
                else
                {
                    InvokeRandomized(WeaponTick, 0f, 0.4f, 0.2f);
                }
            }

            private void FixedUpdate()
            {
                if (HasValidTarget() && IsOn())
                {
                    float num = PROJECTILE_SPEED;// * currentTarget.SAMTargetType.speedMultiplier;
                    Vector3 vector2 = currentTarget.CenterPoint();
                    float num2 = Vector3.Distance(vector2, centerMuzzle.transform.position);
                    float num3 = num2 / num;
                    Vector3 a = vector2 + currentTarget.GetWorldVelocity() * num3;
                    num3 = Vector3.Distance(a, centerMuzzle.transform.position) / num;
                    a = vector2 + currentTarget.GetWorldVelocity() * num3;
                    if (currentTarget.GetWorldVelocity().magnitude > 0.1f)
                    {
                        float num4 = Mathf.Sin(Time.time * 3f) * (1f + num3 * 0.5f);
                        a += currentTarget.GetWorldVelocity().normalized * num4;
                    }

                    aimDir = currentAimDir = (a - centerMuzzle.transform.position).normalized;
                }
            }

            public void WeaponTick()
            {
                if (!IsOn())
                {
                    CancelInvoke(WeaponTick);
                    return;
                }

                if (Time.time < lockOnTime || Time.time < nextBurstTime)
                {
                    return;
                }

                if (firedCount >= BURST_SIZE)
                {
                    nextBurstTime = Time.time + BURST_DELAY;
                    firedCount = 0;
                    return;
                }

                firedCount++;
                FireProjectile(muzzleLeft.position + currentAimDir * 1f, currentAimDir);
            }

            public void FireProjectile(Vector3 origin, Vector3 direction)
            {
                var projectile = GameManager.server.CreateEntity(PROJECTILE_PREFAB, origin, Quaternion.LookRotation(direction, Vector3.up));
                if (projectile != null)
                {
                    projectile.creatorEntity = this;
                    var serverProjectile = projectile.GetComponent<ServerProjectile>();
                    if (serverProjectile)
                    {
                        serverProjectile.InitializeVelocity(GetInheritedProjectileVelocity(direction) + direction * PROJECTILE_SPEED);
                        var oldRadius = serverProjectile.radius;
                        serverProjectile.radius = 0.2f;
                        projectile.Invoke(() => serverProjectile.radius = oldRadius, 0.2f);
                    }

                    projectile.Spawn();
                }

                Effect.server.Run(MUZZLE_FLASH_PREFAB, this, StringPool.Get(muzzleLeft.gameObject.name), Vector3.zero, Vector3.zero);
            }
        }

        #endregion

        #region Path

        class CircularPathFinder
        {
            public bool IsReady { get; private set; }
            public float MaxHeight { get; private set; }

            public float NodeSwitchDistance { get => Mathf.Sqrt(minSqrNodeDistance); set => minSqrNodeDistance = value * value; }
            private float minSqrNodeDistance;

            private List<Vector3> nodes;

            public void GeneratePath(float heightOffset, Action callback = null)
            {
                InvokeHandler.Instance.StartCoroutine(GeneratePathCoro(heightOffset, callback));
            }

            private IEnumerator GeneratePathCoro(float heightOffset, Action callback)
            {
                MaxHeight = GetMaxTerrainHeight();

                var radius = World.Size / 4f;
                var nodeDistance = 50f;

                var length = 2f * Mathf.PI * radius;
                var steps = Mathf.CeilToInt(length / nodeDistance);
                var step = 360f / steps;

                yield return CoroutineEx.waitForEndOfFrame;

                nodes = new List<Vector3>(steps);
                for (int i = 0; i < steps; i++)
                {
                    var point = Quaternion.Euler(0, step * i, 0) * Vector3.forward * radius;
                    var height = Mathf.Max(0, TerrainMeta.HeightMap.GetHeight(point)) + heightOffset;

                    if (height > MaxHeight)
                    {
                        MaxHeight = height;
                    }

                    nodes.Add(point.WithY(height));

                    if (i % 100 == 0 || i + 1 == steps)
                    {
                        yield return CoroutineEx.waitForEndOfFrame;
                        Log($"Generating path {((float)(i + 1) / steps * 100):N0}%");
                    }
                }

                IsReady = true;

                callback?.Invoke();
            }

            private static float GetMaxTerrainHeight()
            {
                return TerrainMeta.Position.y + TerrainMeta.HeightMap.ToEnumerable().Select(x => BitUtility.Short2Float(x)).Max() * TerrainMeta.Size.y;
            }

            public Vector3 GetNextNode(Vector3 position, ref int index)
            {
                if (Vector3.SqrMagnitude(position - nodes[index]) < minSqrNodeDistance)
                {
                    index++;
                    if (index >= nodes.Count)
                    {
                        index = 0;
                    }
                }

                return nodes[index];
            }

            public int GetNearestNodeIndex(Vector3 position)
            {
                var dist = float.PositiveInfinity;
                var index = -1;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var currentDist = Vector3.SqrMagnitude(position - nodes[i]);
                    if (currentDist < dist)
                    {
                        index = i;
                        dist = currentDist;
                    }
                }

                return index;
            }
        }

        #endregion

        #region Config
        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
        [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
        public class Configuration
        {
            [JsonProperty("Schedule event")]
            public bool scheduleEvent = true;

            [JsonProperty("Time between events (minutes)")]
            public int eventDelayMinutes = 60;

            [JsonProperty("Event duration (minutes)")]
            public int eventDurationMinutes = 40;

            [JsonProperty("Time before ship leaves after all crates have been looted (seconds)")]
            public int shipLeaveDelayAfterLooted = 300;

            [JsonProperty("Time before radiation when ship is leaving (seconds)")]
            public int shipRadiationDelay = 30;

            [JsonProperty("Require Air Card to access loot on the ship")]
            public bool enableAirCard = true;

            [JsonProperty("Air card display name")]
            public string airCardDisplayName = "Air Card";

            [JsonProperty("Double NPC count on ship")]
            public bool shipSpawnDuplicateNpcs = false;

            [JsonProperty("Flying cargo ship map marker text (max. 20 characters)")]
            public string shipMarkerText = "Flying Cargo Ship";

            [JsonProperty("Flying cargo ship map marker color")]
            public string shipMarkerColor = "#33ECFB";

            [JsonProperty("Enable control center")]
            public bool controlCenterEnabled = true;

            [JsonProperty("Control center destroy time (seconds)")]
            public int controlCenterDestroyTime = 300;

            [JsonProperty("Control center NPC count")]
            public int controlCenterNpcCount = 8;

            [JsonProperty("Control center map marker text (max. 20 characters)")]
            public string controlCenterMarkerText = "Cargo Control Center";

            [JsonProperty("Control center map marker color")]
            public string controlCenterMarkerColor = "#33ECFB";

            [JsonProperty("Spawn control center at monuments")]
            public bool controlCenterAtMonuments = false;

            [JsonProperty("Custom chat prefix")]
            public string overrideChatPrefix = null;

            [JsonProperty("Ship loot configuration")]
            public LootConfig shipLootConfig = new LootConfig
            {
                lockedCrateCount = 4,
                eliteCrateCount = 4,
                militaryCrateCount = 6,
                normalCrateCount = 7,
                lockedCrateLootTable = new LootManager.LootTable
                {
                    enabled = true,
                    items = new List<LootManager.LootItem>
                    {
                        new LootManager.LootItem("scrap", 10, 100, 1f),
                        new LootManager.LootItem("metal.refined", 10, 25, 0.6f),

                        new LootManager.LootItem("lmg.m249", 1, 1, 0.05f),
                        new LootManager.LootItem("rifle.l96", 1, 1, 0.1f),
                        new LootManager.LootItem("rifle.ak.ice", 1, 1, 0.2f),
                        new LootManager.LootItem("rifle.bolt", 1, 1, 0.2f),
                        new LootManager.LootItem("smg.mp5", 1, 1, 0.3f),
                        new LootManager.LootItem("smg.thompson", 1, 1, 0.3f),
                        new LootManager.LootItem("pistol.prototype17", 1, 1, 0.4f),

                        new LootManager.LootItem("metal.facemask.icemask", 1, 1, 0.15f),
                        new LootManager.LootItem("metal.plate.torso.icevest", 1, 1, 0.15f),

                        new LootManager.LootItem("explosives", 10, 20, 0.2f),
                        new LootManager.LootItem("explosive.timed", 1, 2, 0.1f),
                        new LootManager.LootItem("ammo.rocket.basic", 1, 3, 0.1f),
                        new LootManager.LootItem("ammo.rocket.seeker", 1, 3, 0.1f),
                        new LootManager.LootItem("ammo.rocket.mlrs", 1, 2, 0.1f),
                        new LootManager.LootItem("aiming.module.mlrs", 1, 2, 0.6f),

                        new LootManager.LootItem("rocket.launcher", 1, 1, 0.15f),
                        new LootManager.LootItem("homingmissile.launcher", 1, 1, 0.15f),
                    },
                },
                npcLootTable = new LootManager.LootTable
                {
                    enabled = true,
                    items = new List<LootManager.LootItem>
                    {
                        new LootManager.LootItem("parachute", 1, 1, 0.3f),
                        new LootManager.LootItem("ammo.shotgun", 4, 8, 0.2f),
                        new LootManager.LootItem("ammo.shotgun.fire", 4, 8, 0.2f),
                        new LootManager.LootItem("ammo.shotgun.slug", 4, 8, 0.2f),
                        new LootManager.LootItem("ammo.pistol", 15, 30, 0.2f),
                        new LootManager.LootItem("ammo.pistol.hv", 15, 30, 0.2f),
                        new LootManager.LootItem("ammo.pistol.fire", 15, 30, 0.2f),
                        new LootManager.LootItem("ammo.rifle", 12, 24, 0.2f),
                        new LootManager.LootItem("ammo.rifle.hv", 12, 24, 0.2f),
                        new LootManager.LootItem("ammo.rifle.incendiary", 12, 24, 0.2f),
                        new LootManager.LootItem("syringe.medical", 1, 2, 0.2f),
                        new LootManager.LootItem("bandage", 1, 3, 0.3f),
                        new LootManager.LootItem("largemedkit", 2, 6, 0.1f),
                        new LootManager.LootItem("riflebody", 1, 1, 0.1f),
                        new LootManager.LootItem("smgbody", 1, 2, 0.1f),
                        new LootManager.LootItem("metalspring", 1, 3, 0.1f),
                        new LootManager.LootItem("sewingkit", 1, 2, 0.1f),
                    }
                }
            };

            [JsonProperty("Control center NPC loot table")]
            public LootManager.LootTable controlCenterNpcLootTable = new LootManager.LootTable
            {
                enabled = true,
                items = new List<LootManager.LootItem>
                {
                    new LootManager.LootItem("parachute", 1, 1, 0.3f),
                    new LootManager.LootItem("ammo.shotgun", 4, 8, 0.2f),
                    new LootManager.LootItem("ammo.shotgun.fire", 4, 8, 0.2f),
                    new LootManager.LootItem("ammo.shotgun.slug", 4, 8, 0.2f),
                    new LootManager.LootItem("ammo.pistol", 15, 30, 0.2f),
                    new LootManager.LootItem("ammo.pistol.hv", 15, 30, 0.2f),
                    new LootManager.LootItem("ammo.pistol.fire", 15, 30, 0.2f),
                    new LootManager.LootItem("ammo.rifle", 12, 24, 0.2f),
                    new LootManager.LootItem("ammo.rifle.hv", 12, 24, 0.2f),
                    new LootManager.LootItem("ammo.rifle.incendiary", 12, 24, 0.2f),
                    new LootManager.LootItem("syringe.medical", 1, 2, 0.2f),
                    new LootManager.LootItem("bandage", 1, 3, 0.3f),
                    new LootManager.LootItem("largemedkit", 2, 6, 0.1f),
                    new LootManager.LootItem("riflebody", 1, 1, 0.1f),
                    new LootManager.LootItem("smgbody", 1, 2, 0.1f),
                    new LootManager.LootItem("metalspring", 1, 3, 0.1f),
                    new LootManager.LootItem("sewingkit", 1, 2, 0.1f),
                }
            };

            [JsonProperty("Control center NPC configuration")]
            public NpcConfig controlCenterNpcConfig = new NpcConfig
            {
                name = "Air Scientist",
                health = 200f,
                enableRadio = true,
                senseRange = 50f,
                visionCone = 135f,
                damageScale = 1f,
                memoryDuration = 30f,
                roamRange = 20,
                chaseRange = 40,
                removeCorpse = true,
                clothing = new List<NpcConfig.Item>()
                {
                    new(){ shortName = "hazmatsuittwitch" }
                },
                belt = new List<NpcConfig.Item>
                {
                    new(){ shortName = "rifle.lr300" },
                    new(){ shortName = "grenade.f1", amount = 10 }
                }
            };

            [JsonProperty("Cargo ship NPC configuration (Top)")]
            public NpcConfig cargoNpcTurretLrConfig = new NpcConfig
            {
                name = "Air Scientist",
                health = 250f,
                enableRadio = true,
                senseRange = 100f,
                visionCone = 135f,
                damageScale = 1f,
                memoryDuration = 30f,
                roamRange = 1,
                chaseRange = 1,
                removeCorpse = true,
                clothing = new List<NpcConfig.Item>()
                {
                    new(){ shortName = "hazmatsuittwitch" }
                },
                belt = new List<NpcConfig.Item>
                {
                    new(){ shortName = "rifle.lr300" }
                }
            };

            [JsonProperty("Cargo ship NPC configuration (Normal)")]
            public NpcConfig cargoNpcNormalConfig = new NpcConfig
            {
                name = "Air Scientist",
                health = 200f,
                enableRadio = true,
                senseRange = 40f,
                visionCone = 135f,
                damageScale = 1f,
                memoryDuration = 30f,
                roamRange = 20,
                chaseRange = 40,
                removeCorpse = true,
                clothing = new List<NpcConfig.Item>()
                {
                    new(){ shortName = "hazmatsuittwitch" }
                },
                belt = new List<NpcConfig.Item>
                {
                    new(){ shortName = "smg.mp5" },
                    new(){ shortName = "grenade.f1", amount = 10 }
                }
            };

            [JsonProperty("Cargo ship NPC configuration (Inside)")]
            public NpcConfig cargoNpcTowerConfig = new NpcConfig
            {
                name = "Air Scientist",
                health = 250f,
                enableRadio = true,
                senseRange = 20f,
                visionCone = 135f,
                damageScale = 1f,
                memoryDuration = 30f,
                roamRange = 10,
                chaseRange = 20,
                removeCorpse = true,
                clothing = new List<NpcConfig.Item>()
                {
                    new(){ shortName = "hazmatsuittwitch" }
                },
                belt = new List<NpcConfig.Item>
                {
                    new(){ shortName = "shotgun.spas12" },
                    new(){ shortName = "grenade.f1", amount = 10 }
                }
            };
        }

        public class LootConfig
        {
            [JsonProperty("IMPORTANT NOTICE")]
            private string notice = "The maximum total crate count is 21. If the crate count in the config is higher, excess crates will be ignored, starting at the lowest tier";

            [JsonProperty("Locked crate count (total crate count shold be less than or equal to 21)")]
            public int lockedCrateCount;
            [JsonProperty("Elite crate count (total crate count shold be less than or equal to 21)")]
            public int eliteCrateCount;
            [JsonProperty("Military crate count (total crate count shold be less than or equal to 21)")]
            public int militaryCrateCount;
            [JsonProperty("Normal crate count (total crate count shold be less than or equal to 21)")]
            public int normalCrateCount;

            [JsonProperty("Locked crate loot table")]
            public LootManager.LootTable lockedCrateLootTable = new();
            [JsonProperty("Elite crate loot table")]
            public LootManager.LootTable eliteCrateLootTable = new();
            [JsonProperty("Military crate loot table")]
            public LootManager.LootTable militaryCrateLootTable = new();
            [JsonProperty("Normal crate loot table")]
            public LootManager.LootTable normalCrateLootTable = new();
            [JsonProperty("NPC loot table")]
            public LootManager.LootTable npcLootTable = new();
        }

        public class NpcConfig
        {
            [JsonProperty("Npc name")]
            public string name = "Scientist";
            [JsonProperty("Health")]
            public float health = 150f;
            [JsonProperty("Enable radio chatter")]
            public bool enableRadio = true;

            [JsonProperty("Attack range multiplier")]
            public float attackRangeMultiplier = 1;
            [JsonProperty("Sense range (m)")]
            public float senseRange = 50f;
            [JsonProperty("Vision cone (degrees)")]
            public float visionCone = 135f;
            [JsonProperty("Damage scale (1 = 100%)")]
            public float damageScale = 1f;
            [JsonProperty("Memory duration (seconds)")]
            public float memoryDuration = 60f;
            [JsonProperty("Roam range (m)")]
            public float roamRange = 30f;
            [JsonProperty("Chase range (m)")]
            public float chaseRange = 50f;

            [JsonProperty("Remove corpse on death and drop bag")]
            public bool removeCorpse = false;

            [JsonProperty("Kit (requires Kits plugin)")]
            public string kit = string.Empty;

            [JsonProperty("Clothing items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Item> clothing = new();
            [JsonProperty("Belt items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Item> belt = new();

            public class Item
            {
                public string shortName;
                public int amount = 1;
                public ulong skinId;
            }
        }

        #endregion

        #region Lang
       
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new()
            {
                ["ship_incoming"] = "A flying cargo ship is approaching the island. To enter the ship, you have to disable the turrets first. This can be done at the control center at {0}",
                ["ship_incoming_2"] = "A flying cargo ship is approaching the island",
                ["ship_turrets_disabled"] = "{0} took over the control center and disabled the turrets on the ship",
                ["ship_door_opened"] = "The doors of the ship have been opened by {0}",
                ["ship_looted"] = "The flying cargo ship has been looted and will leave in {0}m {1}s",
                ["air_card_required"] = "You need an Air Card to open the doors",
            }, this);
        }

        #endregion
    }
}
namespace PluginComponents.FlyingCargoShip{using JetBrains.Annotations;using Oxide.Plugins;using System;[AttributeUsage(AttributeTargets.Field,AllowMultiple=false),MeansImplicitUse]public sealed class PermAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,AllowMultiple=false),MeansImplicitUse]public sealed class UniversalCommandAttribute:Attribute{public UniversalCommandAttribute(string name){Name=name;}public string Name{get;set;}public string Permission{get;set;}}[AttributeUsage(AttributeTargets.Method),MeansImplicitUse]public sealed class HookAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,Inherited=false)]public sealed class DebugAttribute:Attribute{}public class MinMaxInt{public int min;public int max;public MinMaxInt(){}public MinMaxInt(int value):this(value,value){}public MinMaxInt(int min,int max){this.min=min;this.max=max;}public int Random(){return UnityEngine.Random.Range(min,max+1);}}}namespace PluginComponents.FlyingCargoShip.Core{using Oxide.Core.Plugins;using Oxide.Core;using Oxide.Plugins;using Newtonsoft.Json;using System.IO;using UnityEngine;using System;using System.Diagnostics;using System.Collections.Generic;using System.Linq;using Facepunch.Extend;using System.Reflection;using PluginComponents.FlyingCargoShip;public abstract class BasePlugin<TPlugin,TConfig>:BasePlugin<TPlugin>where TConfig:class,new()where TPlugin:RustPlugin{protected new static TConfig Config{get;private set;}private string ConfigPath=>Path.Combine(Interface.Oxide.ConfigDirectory,$"{Name}.json");protected override void LoadConfig()=>ReadConfig();protected override void SaveConfig()=>WriteConfig();protected override void LoadDefaultConfig()=>Config=new TConfig();private void ReadConfig(){if(File.Exists(ConfigPath)){Config=JsonConvert.DeserializeObject<TConfig>(File.ReadAllText(ConfigPath));if(Config==null){LogError("[CONFIG] Your configuration file contains an error. Using default configuration values.");LoadDefaultConfig();}}else{LoadDefaultConfig();}WriteConfig();}private void WriteConfig(){var directoryName=Utility.GetDirectoryName(ConfigPath);if(directoryName!=null&&!Directory.Exists(directoryName)){Directory.CreateDirectory(directoryName);}if(Config!=null){string text=JsonConvert.SerializeObject(Config,Formatting.Indented);File.WriteAllText(ConfigPath,text);}else{LogError("[CONFIG] Saving failed - config is null");}}}public abstract class BasePlugin<TPlugin>:BasePlugin where TPlugin:RustPlugin{public new static TPlugin Instance{get;private set;}protected static string DataFolder=>Path.Combine(Interface.Oxide.DataDirectory,nameof(TPlugin));protected override void Init(){base.Init();Instance=this as TPlugin;}protected override void Unload(){Instance=null;base.Unload();}}public abstract class BasePlugin:RustPlugin{public const int OSI_DELAY=5;public const bool CARBONARA=
#if CARBON
true;
#else
false;
#endif
public const bool DEBUG=
#if DEBUG
true;
#else
false;
#endif
public static BasePlayer DebugPlayer=>DEBUG?BasePlayer.activePlayerList.FirstOrDefault(x=>!x.IsNpc):null;public static string PluginName=>Instance?.Name??"NULL";public static BasePlugin Instance{get;private set;}protected virtual UnityEngine.Color ChatColor=>default;protected virtual string ChatPrefix=>ChatColor!=default?$"<color=#{ColorUtility.ToHtmlStringRGB(ChatColor)}>[{Title}]</color>":$"[{Title}]";[HookMethod("Init")]protected virtual void Init(){Instance=this;foreach(var field in GetType().GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)){if(field.IsLiteral&&!field.IsInitOnly&&field.FieldType==typeof(string)&&field.HasAttribute(typeof(PermAttribute))){if(field.GetValue(null)is string perm){LogDebug($"Auto-registered permission '{perm}'");permission.RegisterPermission(perm,this);}}}foreach(var method in GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)){if(method.GetCustomAttributes(typeof(UniversalCommandAttribute),true).FirstOrDefault()is UniversalCommandAttribute attribute){var commandName=attribute.Name??method.Name.ToLower().Replace("cmd",string.Empty);if(attribute.Permission!=null){LogDebug($"Auto-registered command '{commandName}' with permission '{attribute.Permission??"<null>"}'");}else{LogDebug($"Auto-registered command '{commandName}'");}AddUniversalCommand(commandName,method.Name,attribute.Permission);}}}[HookMethod("Unload")]protected virtual void Unload(){Instance=null;}[HookMethod("OnServerInitialized")]protected virtual void OnServerInitialized(bool initial){if(!CARBONARA){OnServerInitialized();}timer.In(OSI_DELAY,OnServerInitializedDelayed);}
#if CARBON
[HookMethod("OnServerInitialized")]
#endif
protected virtual void OnServerInitialized(){}protected virtual void OnServerInitializedDelayed(){}public static void Log(string s){if(Instance!=null){Interface.Oxide.LogInfo($"[{Instance.Title}] {s}");}}[Conditional("DEBUG")]public static void LogDebug(string s){if(DEBUG&&Instance!=null){if(CARBONARA){LogWarning("[DEBUG] "+s);}else{Interface.Oxide.LogDebug($"[{Instance.Title}] {s}");}}}public static void LogWarning(string s){if(Instance!=null){Interface.Oxide.LogWarning($"[{Instance.Title}] {s}");}}public static void LogError(string s){if(Instance!=null){Interface.Oxide.LogError($"[{Instance.Title}] {s}");}}private Dictionary<string,CommandCallback>uiCallbacks;private string uiCommandBase;private void PrepareCommandHandler(){if(uiCallbacks==null){uiCallbacks=new();uiCommandBase=$"{Title.ToLower()}.uicmd";cmd.AddConsoleCommand(uiCommandBase,this,HandleCommand);}}private bool HandleCommand(ConsoleSystem.Arg arg){var cmd=arg.GetString(0);if(uiCallbacks.TryGetValue(cmd,out var callback)){var player=arg.Player();try{callback.ButtonCallback?.Invoke(player);callback.InputCallback?.Invoke(player,string.Join(' ',arg.Args?.Skip(1)??Enumerable.Empty<string>()));}catch(Exception ex){PrintError($"Failed to run UI command {cmd}: {ex}");}}return false;}public string CreateUiCommand(string guid,Action<BasePlayer>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}public string CreateUiCommand(string guid,Action<BasePlayer,string>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}private readonly struct CommandCallback{public readonly bool SingleUse;public readonly Action<BasePlayer>ButtonCallback;public readonly Action<BasePlayer,string>InputCallback;public CommandCallback(Action<BasePlayer>buttonCallback,bool singleUse){ButtonCallback=buttonCallback;InputCallback=null;SingleUse=singleUse;}public CommandCallback(Action<BasePlayer,string>inputCallback,bool singleUse){ButtonCallback=null;InputCallback=inputCallback;SingleUse=singleUse;}}public void ChatMessage(BasePlayer player,string message){if(player){player.SendConsoleCommand("chat.add",2,0,$"{ChatPrefix} {message}");}}}}namespace PluginComponents.FlyingCargoShip.Extensions.BaseNetworkable{using PluginComponents.FlyingCargoShip;using PluginComponents.FlyingCargoShip.Extensions;public static class BaseNetworkableEx{public static bool IsNullOrDestroyed(this global::BaseNetworkable baseNetworkable){return baseNetworkable==null||baseNetworkable.IsDestroyed;}}}namespace PluginComponents.FlyingCargoShip.Extensions.DDraw{using UnityEngine;using PluginComponents.FlyingCargoShip;using PluginComponents.FlyingCargoShip.Extensions;public static class DdrawExtensions{public static void DrawLine(BasePlayer player,Vector3 from,Vector3 to,Color color,float duration){player.SendConsoleCommand("ddraw.line",duration,color,from,to);}public static void DrawArrow(this BasePlayer player,Vector3 from,Vector3 to,float headSize,Color color,float duration){player.SendConsoleCommand("ddraw.arrow",duration,color,from,to,headSize);}public static void DrawSphere(this BasePlayer player,Vector3 pos,float radius,Color color,float duration){player.SendConsoleCommand("ddraw.sphere",duration,color,pos,radius);}public static void DrawText(this BasePlayer player,Vector3 pos,string text,Color color,float duration){player.SendConsoleCommand("ddraw.text",duration,color,pos,text);}public static void DrawBox(this BasePlayer player,Vector3 pos,float size,Color color,float duration){player.SendConsoleCommand("ddraw.box",duration,color,pos,size);}}}namespace PluginComponents.FlyingCargoShip.External.NpcSpawn{using System.Collections.Generic;using PluginComponents.FlyingCargoShip;using PluginComponents.FlyingCargoShip.External;public static class NpcSpawnStates{public const string ROAM="RoamState";public const string CHASE="ChaseState";public const string COMBAT="CombatState";public const string IDLE="IdleState";public const string COMBAT_STATIONARY="CombatStationaryState";public const string RAID="RaidState";public const string RAID_MELEE="RaidStateMelee";public const string SLEDGE="SledgeState";public const string BLAZER="BlazerState";}public class NpcSpawnNpcBelt{public string ShortName;public int Amount;public ulong SkinID;public IEnumerable<string>Mods;public string Ammo;}public class NpcSpawnNpcWear{public string ShortName;public ulong SkinID;}public class NpcSpawnConfig{public string Name{get;set;}public IEnumerable<NpcSpawnNpcWear>WearItems{get;set;}public IEnumerable<NpcSpawnNpcBelt>BeltItems{get;set;}public string Kit{get;set;}public float Health{get;set;}public float RoamRange{get;set;}public float ChaseRange{get;set;}public float SenseRange{get;set;}public float ListenRange{get;set;}public float AttackRangeMultiplier{get;set;}public bool CheckVisionCone{get;set;}public float VisionCone{get;set;}public float DamageScale{get;set;}public float TurretDamageScale{get;set;}public float AimConeScale{get;set;}public bool DisableRadio{get;set;}public bool CanRunAwayWater{get;set;}public bool CanSleep{get;set;}public float Speed{get;set;}public int AreaMask{get;set;}public int AgentTypeID{get;set;}public string HomePosition{get;set;}public float MemoryDuration{get;set;}public HashSet<string>States{get;set;}}}namespace PluginComponents.FlyingCargoShip.Loot{using Newtonsoft.Json;using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using PluginComponents.FlyingCargoShip;public static class LootManager{public static void FillWithLoot(StorageContainer container,IEnumerable<LootItem>lootTable)=>FillWithLoot(container.inventory,lootTable);public static void FillWithLoot(ItemContainer container,IEnumerable<LootItem>lootTable){ClearContainer(container);int amt=0;foreach(var itm in lootTable){if(UnityEngine.Random.Range(0f,1f)<=itm.chance){var item=itm.CreateItem();if(item==null){continue;}if(!item.MoveToContainer(container)){item.Remove();}amt++;}if(amt>=container.capacity){break;}}}public static void FillWithLoot(StorageContainer container,LootTable lootTable)=>FillWithLoot(container.inventory,lootTable);public static void FillWithLoot(ItemContainer container,LootTable lootTable){if(!lootTable.Enabled){return;}if(lootTable.minItems<=0||lootTable.maxItems<=0){FillWithLoot(container,lootTable.items);return;}ClearContainer(container);const int max_retries=50;int itemAmount=0;var targetItemAmount=UnityEngine.Random.Range(lootTable.minItems,lootTable.maxItems+1);targetItemAmount=Mathf.Min(targetItemAmount,lootTable.items.Count);var included=new HashSet<string>();container.capacity=targetItemAmount;for(int i=0;(i<max_retries&&itemAmount<targetItemAmount);i++){foreach(var itm in lootTable.items){if(!included.Contains(itm.shortname)&&UnityEngine.Random.Range(0f,1f)<=itm.chance){var item=itm.CreateItem();if(item==null){continue;}if(!item.MoveToContainer(container)){item.Remove();}included.Add(itm.shortname);itemAmount++;}}}}private static void ClearContainer(ItemContainer container){container.Clear();ItemManager.DoRemoves();}public class LootTable{[JsonIgnore]public bool Enabled=>enabled&&items.Count>0;[JsonProperty("Enabled")]public bool enabled;[JsonProperty("Minimum items",DefaultValueHandling=DefaultValueHandling.Ignore)]public int minItems;[JsonProperty("Maximum items",DefaultValueHandling=DefaultValueHandling.Ignore)]public int maxItems;[JsonProperty("Item list",ObjectCreationHandling=ObjectCreationHandling.Replace)]public List<LootItem>items=new();public LootTable Copy(){return new LootTable{enabled=enabled,minItems=minItems,maxItems=maxItems,items=items.ToList(),};}}public class LootItem{[JsonProperty("Short name")]public string shortname;[JsonProperty("Min amount")]public int min;[JsonProperty("Max amount")]public int max;[JsonProperty("Chance (1 = 100%)")]public float chance;[JsonProperty("Skin id")]public ulong skin=0;[JsonProperty("Custom name")]public string customName=string.Empty;[JsonProperty("Text",DefaultValueHandling=DefaultValueHandling.Ignore)]public string text;[JsonIgnore]public ItemDefinition ItemDefinition=>ItemManager.FindItemDefinition(shortname);public LootItem(){shortname="scrap";min=5;max=10;chance=1f;skin=0;}public LootItem(string shortname,int min,int max,float chance){this.shortname=shortname;this.min=min;this.max=max;this.chance=chance;}public LootItem(string shortname,int min,int max,float chance,ulong skin){this.shortname=shortname;this.min=min;this.max=max;this.chance=chance;this.skin=skin;}public Item CreateItem(){if(ItemDefinition==null||ItemDefinition.itemid==-996920608){return null;}var itm=ItemManager.Create(ItemDefinition,UnityEngine.Random.Range(min,max+1),skin);itm?.OnVirginSpawn();if(customName!=null&&customName.Length>0){itm.name=customName;}if(text!=null&&text.Length>0){itm.text=text;}return itm;}public override int GetHashCode(){return HashCode.Combine(shortname,skin);}}}}namespace PluginComponents.FlyingCargoShip.Tools.Entity{using Facepunch;using JetBrains.Annotations;using PluginComponents.FlyingCargoShip.Extensions.BaseNetworkable;using System;using System.Collections.Generic;using System.Reflection;using UnityEngine;using PluginComponents.FlyingCargoShip;using PluginComponents.FlyingCargoShip.Tools;public static class EntityTools{public static TCustom CreateCustomEntity<TEnt,TCustom>(string prefab,Vector3 position=default,Quaternion rotation=default)where TEnt:BaseEntity where TCustom:TEnt{var entity=CreateEntity<TEnt>(prefab,position,rotation,false,false);var customEntity=entity.gameObject.AddComponent<TCustom>();var fields=typeof(TEnt).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);foreach(var field in fields){field.SetValue(customEntity,field.GetValue(entity));}UnityEngine.Object.DestroyImmediate(entity,true);customEntity.gameObject.AwakeFromInstantiate();return customEntity;}public static T CreateEntity<T>(string prefab,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,default,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Vector3 rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.Euler(rotation),save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,rotation,save,true);private static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save,bool active)where T:BaseEntity{var ent=GameManager.server.CreateEntity(prefab,position,rotation,active);if(ent is not T entity){UnityEngine.Object.Destroy(ent);throw new InvalidCastException($"Failed to create entity of type '{typeof(T).Name}' from '{prefab}'");}ent.enableSaving=save;return entity;}public static void KillSafe<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{var list=Pool.Get<List<T>>();list.AddRange(entities);Kill(list);Pool.FreeUnmanaged(ref list);}public static void Kill<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{foreach(var entity in entities){Kill(entity);}}public static void Kill([CanBeNull]BaseEntity entity){if(entity is not null&&!entity.IsDestroyed){entity.Kill();}}}}namespace PluginComponents.FlyingCargoShip.Tools.Space{using System;using UnityEngine;using PluginComponents.FlyingCargoShip;using PluginComponents.FlyingCargoShip.Tools;public static class SpaceTools{public static(Vector3 position,Quaternion rotation)LocalToWorld(Vector3 rootPos,Vector3 rootFwd,Vector3 localPos,Vector3 localEulers){var worldPos=rootPos+(Quaternion.LookRotation(rootFwd)*localPos);var worldRot=Quaternion.LookRotation(rootFwd)*Quaternion.Euler(localEulers);return new ValueTuple<Vector3,Quaternion>(worldPos,worldRot);}public static(Vector3 position,Vector3 eulers)WorldToLocal(Vector3 worldPos,Quaternion worldRot,Vector3 rootPos,Quaternion rootRot){var inverseRotation=Quaternion.Inverse(rootRot);var localPos=inverseRotation*(worldPos-rootPos);var localEulers=(inverseRotation*worldRot).eulerAngles;return new ValueTuple<Vector3,Vector3>(localPos,localEulers);}}}namespace PluginComponents.FlyingCargoShip.LoottableApi{using Oxide.Core.Plugins;using PluginComponents.FlyingCargoShip;public class LoottableApi{private static Plugin Loottable=>Oxide.Core.Interface.Oxide.RootPluginManager.GetPlugin("Loottable");private readonly Plugin plugin;public LoottableApi(Plugin plugin){this.plugin=plugin;}public void ClearPresets(){Loottable?.Call("ClearPresets",plugin);}public void CreatePresetCategory(string displayName){Loottable?.Call("AddCategory",plugin,displayName);}public void CreatePreset(string displayName,string iconOrUrl){CreatePreset(false,displayName,displayName,iconOrUrl);}public void CreatePreset(bool isNpc,string displayName,string iconOrUrl){CreatePreset(isNpc,displayName,displayName,iconOrUrl);}public void CreatePreset(string key,string displayName,string iconOrUrl){CreatePreset(false,key,displayName,iconOrUrl);}public void CreatePreset(bool isNpc,string key,string displayName,string iconOrUrl){Loottable?.Call("AddPreset",plugin,isNpc,key,displayName,iconOrUrl);}public bool AssignPreset(ScientistNPC npc,string key){return Loottable?.Call("AssignPreset",plugin,key,npc)!=null;}public bool AssignPreset(StorageContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public bool AssignPreset(ItemContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public void ClearCustomItems(){Loottable?.Call("ClearCustomItems",plugin);}public void AddCustomItem(int itemId,ulong skinId){Loottable?.Call("AddCustomItem",plugin,itemId,skinId);}public void AddCustomItem(int itemId,ulong skinId,string customName){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName);}public void AddCustomItem(int itemId,ulong skinId,string customName,bool persistent){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName,persistent);}}}namespace PluginComponents.FlyingCargoShip.MapMarker{using Oxide.Core;using PluginComponents.FlyingCargoShip.Core;using PluginComponents.FlyingCargoShip.Extensions.BaseNetworkable;using System;using System.Linq;using System.Text;using UnityEngine;using PluginComponents.FlyingCargoShip;public class CustomMapMarker{private VendingMachineMapMarker vendingMarker;private MapMarkerGenericRadius colorMarker;private readonly string[]lines;private bool isParented;private CustomMapMarker(int lines){if(lines<1){throw new ArgumentException("Marker line count must be positive",nameof(lines));}this.lines=new string[lines];}private void Spawn(Vector3 position,Color color,float radius,BaseEntity parent){isParented=parent!=null;vendingMarker=GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab",position).GetComponent<VendingMachineMapMarker>();vendingMarker.enableSaving=false;vendingMarker.Spawn();if(parent!=null){vendingMarker.SetParent(parent);}vendingMarker.SendNetworkUpdate();colorMarker=GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab").GetComponent<MapMarkerGenericRadius>();colorMarker.color1=color;colorMarker.color2=colorMarker.color1;colorMarker.radius=radius*(4000f/World.Size);colorMarker.alpha=color.a;colorMarker.enableSaving=false;colorMarker.SetParent(vendingMarker);colorMarker.Spawn();colorMarker.SendNetworkUpdate();colorMarker.SendUpdate();}public void SetText(string text,bool networkUpdate=true)=>SetText(0,text,networkUpdate);public void SetText(int line,string text,bool networkUpdate=true){lines[line]=text.Trim();vendingMarker.markerShopName=String.Join('\n',lines);if(networkUpdate){SendNetworkUpdate();}}public void SendNetworkUpdate(bool fullUpdate=false){vendingMarker.SendNetworkUpdate();if(isParented||fullUpdate){colorMarker.SendNetworkUpdate();colorMarker.SendUpdate();}}public void Destroy(){if(!colorMarker.IsNullOrDestroyed()){colorMarker.Kill();}if(!vendingMarker.IsNullOrDestroyed()){vendingMarker.Kill();}}public static CustomMapMarker Create(Vector3 position,float radius,Color color,int lines=1)=>Create(null,position,radius,color,lines);public static CustomMapMarker Create(BaseEntity parent,Vector3 localPosition,float radius,Color color,int lines=1){var marker=new CustomMapMarker(lines);marker.Spawn(localPosition,color,radius,parent);marker.SendNetworkUpdate();return marker;}}}namespace PluginComponents.FlyingCargoShip.SpawnPoint{using System;using System.Collections.Generic;using UnityEngine;using PluginComponents.FlyingCargoShip.Core;using Facepunch;using Oxide.Core;using Rust;using System.Collections;using System.Diagnostics.CodeAnalysis;using PluginComponents.FlyingCargoShip;[SuppressMessage("ReSharper","BitwiseOperatorOnEnumWithoutFlags")]public class SpawnPointManager{public const int DEFAULT_BATCH_SIZE=100_000;public const TerrainTopology.Enum TP_WATER=TerrainTopology.Enum.Ocean|TerrainTopology.Enum.Oceanside|TerrainTopology.Enum.Lake|TerrainTopology.Enum.Lakeside|TerrainTopology.Enum.River|TerrainTopology.Enum.Riverside|TerrainTopology.Enum.Swamp|TerrainTopology.Enum.Offshore;public const TerrainTopology.Enum TP_ROAD=TerrainTopology.Enum.Road|TerrainTopology.Enum.Roadside|TerrainTopology.Enum.Rail|TerrainTopology.Enum.Railside;public const TerrainTopology.Enum TP_BUILDING=TerrainTopology.Enum.Building|TerrainTopology.Enum.Monument;public const TerrainTopology.Enum TP_ROCK=TerrainTopology.Enum.Hilltop|TerrainTopology.Enum.Mountain|TerrainTopology.Enum.Cliff|TerrainTopology.Enum.Cliffside|TerrainTopology.Enum.Clutter|TerrainTopology.Enum.Decor;public const TerrainTopology.Enum TP_DEFAULT=TP_ROCK|TP_WATER|TP_BUILDING;public bool Initialized{get;private set;}public int GridSize{get;set;}public readonly SpawnPointConstraints Constraints=new();private readonly List<Vector3>spawnPoints=new();public SpawnPointManager(int gridSize,float overrideTopologyRadius=-1){GridSize=gridSize;Constraints.TopologyRadius=overrideTopologyRadius>0?overrideTopologyRadius:gridSize;}public void Configure(Action<SpawnPointConstraints>configure){configure.Invoke(Constraints);}public bool TryGetRandomSpawnPoint(out Vector3 point,int attempts=20){point=default;if(!Initialized){BasePlugin.LogError($"Failed to get spawn point - not initialized");return false;}for(int attempt=0;attempt<attempts;attempt++){point=spawnPoints.GetRandom();if(IsValidSpawnPoint(point)){return true;}}BasePlugin.LogWarning($"Failed to get spawn point - attempt limit exceeded");return false;}public IEnumerator CacheSpawnPoints(int batchSize=DEFAULT_BATCH_SIZE){var ws=Mathf.RoundToInt(World.Size/2f);int count=0,vcount=0,tcount=(ws*2/GridSize)*(ws*2/GridSize);for(int x=-ws;x<ws;x+=GridSize){for(int z=-ws;z<ws;z+=GridSize){if(batchSize>0&&count%batchSize==0){yield return CoroutineEx.waitForEndOfFrame;}if(batchSize>0&&count%(batchSize*2)==0){BasePlugin.Log($"Finding spawn points {((float)count/tcount*100f):N0}% ({count} / {tcount})");}var point=new Vector3(x,0,z);var height=TerrainMeta.HeightMap.GetHeight(point);point.y=height;if(IsValidSpawnPoint(point)){vcount++;spawnPoints.Add(point);}count++;}}BasePlugin.LogDebug($"Found {vcount:N0} valid spawn points of {tcount:N0}");BasePlugin.Log("done");Initialized=true;}private bool IsValidSpawnPoint(Vector3 point){float height=TerrainMeta.HeightMap.GetHeight(point);if(!Constraints.ValidHeight.IsInRange(height)){return false;}var topology=(TerrainTopology.Enum)TerrainMeta.TopologyMap.GetTopology(point,Constraints.TopologyRadius);if((topology&Constraints.BlockedTopology)>0){return false;}if(Constraints.MinDistanceToBuildings>0){var list=Pool.Get<List<BaseEntity>>();Vis.Entities(point,Constraints.MinDistanceToBuildings,list,Layers.Mask.Construction|Layers.Mask.Deployed);list.RemoveAll(x=>!x||x.IsDestroyed);list.RemoveAll(x=>x.IsNpc||!x.OwnerID.IsSteamId());var fail=list.Count>0;Pool.FreeUnmanaged(ref list);if(fail){return false;}}if(Constraints.CustomValidator!=null&&!Constraints.CustomValidator.Invoke(point)){return false;}return true;}private static BuildingPrivlidge GetBuildingPrivilege(Vector3 position,float radius){var obb=new OBB(position,Quaternion.identity,new Bounds(Vector3.zero,new Vector3(radius*2f,8f,radius*2f)));BuildingBlock other=null;BuildingPrivlidge result=null;List<BuildingBlock>buildingBlocks=Pool.Get<List<BuildingBlock>>();Vis.Entities(obb.position,16f+obb.extents.magnitude,buildingBlocks,2097152);for(int i=0;i<buildingBlocks.Count;i++){BuildingBlock buildingBlock=buildingBlocks[i];if(!buildingBlock.IsOlderThan(other)||obb.Distance(buildingBlock.WorldSpaceBounds())>16f){continue;}BuildingManager.Building building=buildingBlock.GetBuilding();if(building!=null){BuildingPrivlidge dominatingBuildingPrivilege=building.GetDominatingBuildingPrivilege();if(!(dominatingBuildingPrivilege==null)){other=buildingBlock;result=dominatingBuildingPrivilege;}}}Pool.FreeUnmanaged(ref buildingBlocks);return result;}public class SpawnPointConstraints{public TerrainTopology.Enum BlockedTopology{get;set;}public float TopologyRadius{get;set;}public float MinDistanceToBuildings{get;set;}public MinMaxB<float>ValidHeight{get;set;}public Func<Vector3,bool>CustomValidator;public void BlockTopology(params TerrainTopology.Enum[]topology){foreach(var layer in topology){BlockedTopology|=layer;}}}}public struct MinMaxB<T>where T:IComparable<T>{private T min;public T Min{get=>MinIsSet?min:default;set{min=value;MinIsSet=true;}}public bool MinIsSet{get;private set;}private T max;public T Max{get=>MaxIsSet?max:default;set{max=value;MaxIsSet=true;}}public bool MaxIsSet{get;private set;}public bool IsInRange(T value){if(MinIsSet&&value.CompareTo(min)<0){return false;}if(MaxIsSet&&value.CompareTo(max)>0){return false;}return true;}}}namespace PluginComponents.FlyingCargoShip.Extensions.Lang{using PluginComponents.FlyingCargoShip.Core;using System.Collections.Generic;using PluginComponents.FlyingCargoShip;using PluginComponents.FlyingCargoShip.Extensions;public static class LangEx{public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player)=>GetMessage(lang,key,player.userID.Get());public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,ulong playerId){return lang.GetMessage(key,BasePlugin.Instance,playerId.ToString());}public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player,params object[]args)=>GetMessage(lang,key,player.userID,args);public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,ulong playerId,params object[]args){var msg=lang.GetMessage(key,BasePlugin.Instance,playerId.ToString());return string.Format(msg,args);}public static void SendMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player){var msg=GetMessage(lang,key,player.userID);BasePlugin.Instance.ChatMessage(player,msg);}public static void SendMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player,params object[]args){var msg=GetMessage(lang,key,player.userID,args);BasePlugin.Instance.ChatMessage(player,msg);}public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key)=>BroadcastMessage(lang,key,BasePlayer.activePlayerList);public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,IEnumerable<BasePlayer>players){foreach(var player in players){var msg=GetMessage(lang,key,player.userID);BasePlugin.Instance.ChatMessage(player,msg);}}public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,params object[]args)=>BroadcastMessage(lang,key,BasePlayer.activePlayerList,args);public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,IEnumerable<BasePlayer>players,params object[]args){foreach(var player in players){var msg=GetMessage(lang,key,player.userID,args);BasePlugin.Instance.ChatMessage(player,msg);}}public static string GetLanguage(this Oxide.Core.Libraries.Lang lang,BasePlayer player)=>GetLanguage(lang,player.userID);public static string GetLanguage(this Oxide.Core.Libraries.Lang lang,ulong userId){return lang.GetLanguage(userId.ToString());}}}