using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using UnityEngine.AI;
using VLB;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using Rust;
using Rust.Ai;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Paratroopers", "FIXbyLore", "4.0.1")]
    [Description("Spawns Paratroopers Event that deploys from the Cargo Plane to random area")]
    class Paratroopers : RustPlugin
    {
        [PluginReference] Plugin Kits, Titan;
        private static Paratroopers plugin;

        #region Vars
        private static Paratroopers ins { get; set; }

        private StoredData storedData;
        private DynamicConfigFile data;

        private const string ADMIN_PERM = "paratroopers.admin";
        private const string PLAYER_PERM = "paratroopers.cancall";

        private NavMeshHit navHit;
        private RaycastHit rayHit;

        public List<int> FltNum = new List<int>();

        private const uint CARGOPLANE_PREFABID = 2383782438;
        private const uint NPCPlayer_PREFABID = 4199494415; //
        private const uint CORPSE_PREFABID = 2604534927;
        private const uint CHUTE_PREFABID = 1268659691;
        private const uint SUPPLYDROP_PREFABID = 3632568684;
        private const uint HACKABLECRATE_PREFABID = 209286362;
        private const uint BRADLEY_PREFABID = 1456850188;
        private const uint SMOKE_EFFECTID = 2278592883;
        private const uint SIRENLIGHT_EFFECTID = 2436926577;
        private const uint SIRENALARM_EFFECTID = 1056621402;
        private const uint SUPPLYDROP_EFFECTID = 135533567;

        private LootContainer crate;
        const object Null = null;
        public bool IsNight;

        private List<MapMarkerGenericRadius> dropZoneMarkerList = new List<MapMarkerGenericRadius>();
        private Dictionary<ScientistNPC, MapMarkerGenericRadius> botMarkerList = new Dictionary<ScientistNPC, MapMarkerGenericRadius>();
        private Dictionary<ScientistNPC, NPCParatrooper> paratrooperList = new Dictionary<ScientistNPC, NPCParatrooper>();
        private List<ScientistNPC> jumperList = new List<ScientistNPC>();
        private List<ulong> paratrooperCorpseIDs = new List<ulong>();
        public List<ulong> DeadNPCPlayerIds = new List<ulong>(); //to tracebackpacks
        private Dictionary<CargoPlane, ParatrooperPlane> planes = new Dictionary<CargoPlane, ParatrooperPlane>();
        List<LootContainer> drops = new List<LootContainer>();
        List<StorageContainer> dropsCrate = new List<StorageContainer>();
        List<BradleyAPC> dropBradley = new List<BradleyAPC>();

        private const int LAND_LAYERS = 1 << 4 | 1 << 8 | 1 << 16 | 1 << 21 | 1 << 23;
        private const int WORLD_LAYER = (1 << 0 | 1 << 16) | ~(1 << 18 | 1 << 28 | 1 << 29);

        public const string ChatIcon = "76561198035100747";
        #endregion

        #region Get Misc Functions
        private Vector3 GetDropPosition()
        {
            return new Vector3(configData.Automation.JumpPositionX, configData.Automation.jumpHeight, configData.Automation.JumpPositionZ);
        }

        public int GetDefaultJumpSpread()
        {
            return configData.Paratroopers.defaultJumpSpread;
        }

        public float GetJumpHeight()
        {
            return configData.Automation.jumpHeight;
        }

        public string GetInboundMessage()
        {
            return msg("InboundJumpPositionMessageChat");
        }

        public float GetPlaneSpeed()
        {
            return configData.Automation.PlaneSpeed;
        }
        #endregion

        #region startup/close/clean-up

        private void OnServerInitialized()
        {
            //Start Loaded
            Unsubscribe();
            IsNight = TOD_Sky.Instance.IsNight;

            if (configData.Notifications.ConsoleOutput)
            {
                if (Kits == null)
                {
                    Puts("WARNING: Kits Plugin is required");
                }
                else Puts("Found dependency Kits");
            }

            ins = this;
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission(ADMIN_PERM, this);
            permission.RegisterPermission(PLAYER_PERM, this);

            foreach (var kvp in configData.Cooldowns)
            {
                if (!kvp.Key.StartsWith("paratroopers."))
                {
                    permission.RegisterPermission("paratroopers." + kvp.Key, this);
                }
                else permission.RegisterPermission(kvp.Key, this);
            }

            if (configData.MapSettings.CreateBotMarkers)
            {
                timer.Repeat(configData.MapSettings.BotMarkerRefreshInterval, 0, () => RefreshBotMarkers());
            }
            //End Loaded

            ins = this;
            LoadData();

            if (configData.Automation.AutoSpawn == true)
            {
                float nextJump = Oxide.Core.Random.Range(configData.Automation.RandomJumpIntervalMinSec, configData.Automation.RandomJumpIntervalMaxSec);

                timer.Once(nextJump, () => RepeatRandomJump());

                if (configData.Notifications.ConsoleOutput)
                {
                    Puts("Random Paratroopers Events will occur in {0}s", nextJump);
                }
            }
            else if (configData.Notifications.ConsoleOutput)
            {
                Puts("Random interval Paratroopers Events are turned off");
            }

            Subscribe();
        }

        private void Unload()
        {
            if (configData.Notifications.ConsoleOutput)
            {
                Puts("Save files, destroy timers, etc");
            }

            WipeTroopers();
            WipeMarkers(botMarkerList.Values.ToList());
            WipeMarkers(dropZoneMarkerList.ToList());
            ins = null;
            KillEvent();
            configData = null;
            SaveData();
        }

        public void GetFlightNumber()
        {
            FlightNumber = Core.Random.Range(3000, 9999);
            FltNum.Add(FlightNumber);
        }

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            var mins = dateDifference.Minutes;
            var secs = dateDifference.Seconds;
            return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
        }
        #endregion

        #region hooks
        private object OnDeathNotice(Dictionary<string, object> data, string message) // this shouldn't be required ever
        {
            //PrintWarning(data["VictimEntity"].ToString());

            var entity = data["VictimEntity"] as ScientistNPC;

            if (entity == null)
            {
                return null;
            }

            if (paratrooperList.ContainsKey(entity))
            {
                paratrooperList.Remove(entity);

                return false;
            }

            return null;
        }

        private object OnCustomLootNPC(uint ID)
        {
            foreach (var npc in paratrooperList.Keys)
            {
                if (npc?.net?.ID.Value == ID)
                {
                    if (configData.npc.LootType.ToLower() == "default")
                    {
                        return null;
                    }

                    return true;
                }
            }

            return null;
        }

        private object OnNpcResume(ScientistNPC npc)
        {
            if (npc == null)
            {
                return null;
            }

            NPCParatrooper paratrooper;
            if (!paratrooperList.TryGetValue(npc, out paratrooper) || paratrooper.hasLanded)
            {
                return null;
            }

            return true;
        }

        private void OnEntityKill(SupplyDrop drop) => drops.Remove(drop);

        private void OnEntityKill(HackableLockedCrate crate) => dropsCrate.Remove(crate);

        private void OnEntityKill(CargoPlane cp)
        {
            if (cp == null)
            {
                return;
            }

            ParatrooperPlane crashComponent;
            if (!planes.TryGetValue(cp, out crashComponent))
            {
                return;
            }

            UnityEngine.Object.Destroy(crashComponent);
            planes.Remove(cp);
        }

        private void OnEntityKill(ScientistNPC victim) => OnPlayerDeath(victim, null);

        private void OnPlayerDeath(ScientistNPC victim, HitInfo hitInfo)
        {
            if (victim == null)
            {
                return;
            }

            NPCParatrooper paratrooper;
            if (!paratrooperList.TryGetValue(victim, out paratrooper))
            {
                return;
            }

            victim.UpdateActiveItem(0);

            if (configData.npc.LootType.ToLower() == "inventory")
            {
                paratrooper.PrepareInventory();
            }

            if (hitInfo == null)
            {
                return;
            }

            var attacker = hitInfo.Initiator as BasePlayer;

            if (attacker == null)
            {
                return;
            }

            MapMarkerGenericRadius mapMarker;
            if (botMarkerList.TryGetValue(victim, out mapMarker))
            {
                mapMarker.Kill();
                mapMarker.SendUpdate();
                botMarkerList.Remove(victim);
            }

            paratrooperCorpseIDs.Add(victim.userID);

            if (victim == attacker)
            {
                if (configData.Notifications.NPCSuicide)
                {
                    string key = hitInfo.damageTypes.Has(Rust.DamageType.Drowned) ? "ParatrooperDrowned" : "ParatrooperKilledSelf";

                    foreach (var target in BasePlayer.activePlayerList)
                    {
                        target.SendConsoleCommand("chat.add", new object[] { 2, ChatIcon, string.Format(msg(key, target.UserIDString), victim.displayName) });
                    }
                }
            }
            else if (configData.Notifications.NPCDeath)
            {
                foreach (var target in BasePlayer.activePlayerList)
                {
                    target.SendConsoleCommand("chat.add", new object[] { 2, ChatIcon, string.Format(msg("ParatrooperKilled", target.UserIDString), attacker.displayName, victim.displayName) });
                }
            }
        }

        private void OnEntityDeath(BradleyAPC victim, HitInfo hitInfo)
        {
            if (victim == null || hitInfo == null || !dropBradley.Contains(victim))
            {
                return;
            }

            dropBradley.Remove(victim);

            victim.GetOrAddComponent<APCController>();

            if (!configData.Notifications.APCDeath)
            {
                return;
            }

            var attacker = hitInfo.Initiator as BasePlayer;

            if (attacker == null)
            {
                return;
            }

            foreach (var target in BasePlayer.activePlayerList)
            {
                target.SendConsoleCommand("chat.add", new object[] { 2, ChatIcon, string.Format(msg("BradleyKilled", target.UserIDString), attacker.displayName) });
            }
        }

        private object OnTurretTarget(NPCAutoTurret turret, ScientistNPC ScientistNPC) => HookHandler(ScientistNPC);

        private object CanHelicopterTarget(PatrolHelicopterAI heliAi, ScientistNPC ScientistNPC) => HookHandler(ScientistNPC);

        private object CanHelicopterStrafeTarget(PatrolHelicopterAI heliAi, ScientistNPC ScientistNPC) => HookHandler(ScientistNPC);

        private object OnNpcTarget(BaseNpc npc, ScientistNPC ScientistNPC) => HookHandler(ScientistNPC);

        private object HookHandler(ScientistNPC ScientistNPC) => ScientistNPC != null && paratrooperList.ContainsKey(ScientistNPC) ? true : (object)null;

        private object CanBradleyApcTarget(BradleyAPC apc, BasePlayer player)
        {
            if (player == null || apc == null || !dropBradley.Contains(apc))
            {
                return null;
            }

            if (player is ScientistNPC)
            {
                return HookHandler(player as ScientistNPC);
            }

            return player.IsNearEnemyBase() || CanIgnoreSleeper(player) ? false : (object)null;
        }

        private bool EventTerritory(Vector3 target)
        {
            foreach (var drop in drops)
            {
                if ((drop.transform.position - target).magnitude <= configData.Distance)
                {
                    return true;
                }
            }

            foreach (var drop in dropsCrate)
            {
                if ((drop.transform.position - target).magnitude <= configData.Distance)
                {
                    return true;
                }
            }

            foreach (var drop in dropBradley)
            {
                if ((drop.transform.position - target).magnitude <= configData.Distance)
                {
                    return true;
                }
            }

            return false;
        }

        private object CanEntityTakeDamage(BasePlayer victim, HitInfo hitInfo)
        {
            if (hitInfo == null || !victim.IsValid() || victim.IsNpc)
            {
                return null;
            }

            var attacker = hitInfo.Initiator as BasePlayer;

            if (!attacker.IsValid())
            {
                return null;
            }

            if (EventTerritory(victim.transform.position) && EventTerritory(attacker.transform.position))
            {
                return true;
            }

            return null;
        }

        private void OnEntityTakeDamage(BradleyAPC apc, HitInfo hitInfo)
        {
            if (apc == null || !dropBradley.Contains(apc) || hitInfo == null || !(hitInfo.Initiator is BasePlayer))
            {
                return;
            }

            var attacker = hitInfo.Initiator as BasePlayer;

            if (!attacker.IsNearEnemyBase())
            {
                return;
            }

            hitInfo.damageTypes = new DamageTypeList();
        }

        private void OnEntityTakeDamage(ScientistNPC ScientistNPC, HitInfo hitInfo)
        {
            if (ScientistNPC == null || hitInfo == null || !(hitInfo.Initiator is BasePlayer))
            {
                return;
            }

            NPCParatrooper paratrooper;
            if (!paratrooperList.TryGetValue(ScientistNPC, out paratrooper))
            {
                return;
            }

            paratrooper.OnReceivedDamage(hitInfo.Initiator as BasePlayer, hitInfo);
        }

        #region IgnoreSleeper
        private object OnNpcTarget(BaseEntity npc, BasePlayer player) => CanIgnoreSleeper(player) ? true : (object)null;

        private bool CanIgnoreSleeper(BasePlayer player)
        {
            if (player == null || player is TunnelDweller || !player.userID.IsSteamId() || !player.IsSleeping()) return false;
            return true;
        }
        #endregion IgnoreSleeper

        #region WeaponHandling
        private int GetRange(float distance)
        {
            if (distance < 2f) return 1;
            if (distance < 10f) return 2;
            if (distance < 40f) return 3;
            return 4;
        }

        #endregion

        private object CanPopulateLoot(ScientistNPC ScientistNPC, NPCPlayerCorpse corpse) => HookHandler(ScientistNPC);

        private object OnCorpsePopulate(HumanNPC ScientistNPC, NPCPlayerCorpse corpse)
        {
            if (corpse != null && paratrooperCorpseIDs.Contains(corpse.playerSteamID))
            {
                paratrooperCorpseIDs.Remove(corpse.playerSteamID);
                DeadNPCPlayerIds.Add(corpse.playerSteamID);

                if (corpse.containers != null)
                {
                    for (int i = 0; i < corpse.containers.Length; i++)
                    {
                        ClearContainer(corpse.containers[i]);
                    }
                }
            }

            if (ScientistNPC == null)
            {
                return null;
            }

            NPCParatrooper paratrooper;
            if (!paratrooperList.TryGetValue(ScientistNPC as ScientistNPC, out paratrooper))
                return null;

            paratrooperList.Remove(ScientistNPC as ScientistNPC);

            if (configData.npc.LootType.ToLower() == "inventory")
            {
                paratrooper.MoveInventoryTo(corpse);
                return corpse;
            }

            //if (configData.npc.RandomItems.Maximum > 0)
            //{
            //    PopulateLoot(corpse.containers[0], configData.npc.RandomItems);
            //}

            return corpse;
        }
        private object OnEntityDismounted(BaseMountable mount, BasePlayer player)
        {
            if (player.IsNpc && paratrooperList.ContainsKey(player.GetComponent<ScientistNPC>()) && mount.VehicleParent() is CH47Helicopter)
            {
                player.Invoke(() =>
                {
                    if (player == null)
                    {
                        return;
                    }
                    BaseNavigator BN = player.GetComponent<BaseNavigator>();
                    if (BN == null)
                    {
                        return;
                    }
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(player.transform.position, out hit, 30, -1))
                    {
                        player.gameObject.layer = 17;
                        player.ServerPosition = hit.position;
                        BN.Agent.Warp(player.ServerPosition);
                        player.Invoke(() => BN.CanUseNavMesh = true, 2f);
                    }
                }
                , 1f);
            }
            return null;
        }
        private void Subscribe() // removed all init checks
        {
            if (configData.Distance > 0 && configData.LootBoxes.LootEffects.dropBradley)
            {
                Subscribe(nameof(CanEntityTakeDamage));
            }

            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(OnPlayerDeath));
            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(CanLootPlayer));
            Subscribe(nameof(OnPlayerAssist));

            if (configData.npc.LootType.ToLower() != "default")
            {
                Subscribe(nameof(OnCorpsePopulate));
                Subscribe(nameof(CanPopulateLoot));
            }
        }

        private void Unsubscribe()
        {
            Unsubscribe(nameof(CanEntityTakeDamage));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(CanLootPlayer));
            Unsubscribe(nameof(OnPlayerAssist));
            Unsubscribe(nameof(OnCorpsePopulate));
            Unsubscribe(nameof(CanPopulateLoot));
            Unsubscribe(nameof(CanBradleyApcTarget));
        }

        private void OnEntitySpawned(DroppedItemContainer container)
        {
            NextTick(() =>
            {
                if (container == null || !DeadNPCPlayerIds.Contains(container.playerSteamID)) return;
                DeadNPCPlayerIds.Remove(container.playerSteamID);
                if (!container.IsDestroyed) container.Kill();
            });
        }

        private object CanLootPlayer(ScientistNPC ScientistNPC, BasePlayer player)
        {
            if (ScientistNPC != null && paratrooperList.ContainsKey(ScientistNPC) && ScientistNPC.IsWounded())
            {
                return false;
            }

            return null;
        }

        private object OnPlayerAssist(ScientistNPC ScientistNPC, BasePlayer player) => HookHandler(ScientistNPC);

        private static void RunEffect(string name, BaseEntity entity = null, Vector3 position = new Vector3(), Vector3 offset = new Vector3())
        {
            if (entity != null)
            {
                Effect.server.Run(name, entity, 0, offset, position, null, true);
            }
            else Effect.server.Run(name, position, Vector3.up, null, true);
        }

        private string GetGridString(Vector3 position)
        {
            var adjPosition = new Vector2((World.Size / 2) + position.x, (World.Size / 2) - position.z);
            return $"{NumberToString((int)(adjPosition.x / 145))}{((int)(adjPosition.y / 145))}";
        }

        private static string NumberToString(int number)
        {
            var a = number > 26;
            var c = (System.Char)(65 + (a ? number - 26 : number));
            return a ? "A" + c : c.ToString();
        }

        private static void StripInventory(BasePlayer player)
        {
            ClearContainer(player.inventory.containerBelt);
            ClearContainer(player.inventory.containerMain);
            ClearContainer(player.inventory.containerWear);
        }

        private static void ClearContainer(ItemContainer container)
        {
            while (container.itemList.Count > 0)
            {
                Item item = container.itemList[0];
                item.RemoveFromContainer();
                item.Remove();
            }
        }

        private static void PopulateLoot(ItemContainer container, ConfigData.LootContainer loot)
        {
            ClearContainer(container);

            var amount = UnityEngine.Random.Range(loot.Minimum, loot.Maximum);
            var list = new List<ConfigData.LootItem>(loot.Items);

            for (int i = 0; i < amount; i++)
            {
                if (list.Count == 0)
                {
                    list = new List<ConfigData.LootItem>(loot.Items);
                }

                var lootItem = list[UnityEngine.Random.Range(0, list.Count())];

                list.Remove(lootItem);

                Item item = ItemManager.CreateByName(lootItem.Name, UnityEngine.Random.Range(lootItem.Minimum, lootItem.Maximum), lootItem.Skin);
                if (!item.MoveToContainer(container)) item.Remove();
            }
        }

        private static object FindPointOnNavmesh(Vector3 targetPosition)
        {
            targetPosition.y = TerrainMeta.HeightMap.GetHeight(targetPosition);

            for (int i = 0; i < 10; i++)
            {
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(targetPosition, out navHit, 40, -1))
                {
                    if (IsInOrOnRock(navHit.position))
                    {
                        continue;
                    }

                    return navHit.position;
                }
            }
            return null;
        }

        private static bool IsInOrOnRock(Vector3 position, string meshName = "rock_", float radius = 2f)
        {
            bool flag = false;
            int hits = Physics.OverlapSphereNonAlloc(position, radius, Vis.colBuffer, Layers.Mask.World, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                if (Vis.colBuffer[i].name.StartsWith(meshName) && IsRockTooLarge(Vis.colBuffer[i].bounds))
                {
                    flag = true;
                }

                Vis.colBuffer[i] = null;
            }
            if (!flag)
            {
                float y = TerrainMeta.HighestPoint.y + 250f;
                RaycastHit hit;
                if (Physics.Raycast(position, Vector3.up, out hit, y, Layers.Mask.World, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.name.StartsWith(meshName) && IsRockTooLarge(hit.collider.bounds)) flag = true;
                }
                if (!flag && Physics.Raycast(position, Vector3.down, out hit, y, Layers.Mask.World, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.name.StartsWith(meshName) && IsRockTooLarge(hit.collider.bounds)) flag = true;
                }
                if (!flag && Physics.Raycast(position + new Vector3(0f, y, 0f), Vector3.down, out hit, y + 1f, Layers.Mask.World, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.name.StartsWith(meshName) && IsRockTooLarge(hit.collider.bounds)) flag = true;
                }
            }
            return flag;
        }

        private static bool IsRockTooLarge(Bounds bounds, float extents = 1.5f)
        {
            return bounds.extents.Max() > extents;
        }
        private bool IsKit(string kit)
        {
            //Call kit plugin to check if its valid kit
            var success = Kits?.Call("isKit", kit);
            if (success == null || !(success is bool)) { return false; }
            return (bool)success;
        }
        void GiveKit(NPCPlayer npc, string kit)
        {
            if (kit == "" || !IsKit(kit)) return;
            object success = Kits?.Call("GiveKit", npc, kit);
            if (success == null || !(success is bool))
            {
                Puts("Failed to give NPC Kit");
                return;
            }
            //Trys to equip stuff
            Item projectileItem = null;
            //Find first gun
            foreach (var item in npc.inventory.containerBelt.itemList.ToList())
            {
                if (item.GetHeldEntity() is BaseProjectile)
                {
                    projectileItem = item;
                    break;
                }
                //Move medial items out of hot bar
                if (item.GetHeldEntity() is MedicalTool)
                {
                    item.MoveToContainer(npc.inventory.containerMain);
                    continue;
                }
            }
            if (projectileItem == null)
            {
                //Find a melee weapon in the belt
                foreach (var item in npc.inventory.containerBelt.itemList.ToList())
                {
                    if (item.GetHeldEntity() is BaseMelee)
                    {
                        projectileItem = item;
                        break;
                    }
                }
            }
            if (projectileItem != null)
            {
                //pull out active item.
                npc.UpdateActiveItem(projectileItem.uid);
                npc.inventory.UpdatedVisibleHolsteredItems();
                timer.Once(1f, () => { npc.AttemptReload(); });
            }
        }
        #endregion

        #region Map Markers

        private void CreateBotMarker(ScientistNPC npc)
        {
            if (npc == null || npc.IsDestroyed)
            {
                return;
            }

            MapMarkerGenericRadius mapMarker;
            if (!botMarkerList.TryGetValue(npc, out mapMarker))
            {
                mapMarker = GameManager.server.CreateEntity(StringPool.Get(2849728229), npc.transform.position) as MapMarkerGenericRadius;
                mapMarker.alpha = configData.MapSettings.BotDropZoneAlpha;
                mapMarker.color1 = Color.yellow;
                mapMarker.radius = configData.MapSettings.BotDropZoneRadius;
                mapMarker.Spawn();
                botMarkerList[npc] = mapMarker;
            }

            mapMarker.SendUpdate();
        }

        private void CreateDropZoneMarker(Vector3 pos)
        {
            var mapMarker = GameManager.server.CreateEntity(StringPool.Get(2849728229), pos) as MapMarkerGenericRadius;

            mapMarker.alpha = configData.MapSettings.DropZoneAlpha;
            mapMarker.color1 = Color.red;
            mapMarker.radius = configData.MapSettings.DropZoneRadius;

            mapMarker.Spawn();
            mapMarker.SendUpdate();

            timer.Once(configData.npc.Lifetime + 90f, () =>
            {
                try
                {
                    dropZoneMarkerList.Remove(mapMarker);

                    mapMarker.Kill();
                    mapMarker.SendUpdate();
                }
                catch { }
            });  // add time to account for drop time

            dropZoneMarkerList.Add(mapMarker);
        }

        private void RefreshBotMarkers()
        {
            foreach (var npcPlayer in paratrooperList.Keys)
            {
                CreateBotMarker(npcPlayer);
            }

            foreach (var jumperPlayer in jumperList)
            {
                CreateBotMarker(jumperPlayer);
            }

            foreach (var mapMarker in dropZoneMarkerList)
            {
                mapMarker?.SendUpdate();
            }
        }

        private List<string> GetParatrooperPositions()
        {
            var returnPositionList = new List<string>();

            foreach (var jumper in jumperList)
            {
                if (jumper == null || jumper.IsDestroyed)
                {
                    continue;
                }

                var gridPos = GetGridString(jumper.transform.position);

                if (!returnPositionList.Contains(gridPos))
                {
                    returnPositionList.Add(gridPos);
                }
            }

            foreach (var ScientistNPC in paratrooperList.Keys)
            {
                if (ScientistNPC == null || ScientistNPC.IsDestroyed)
                {
                    continue;
                }

                var gridPos = GetGridString(ScientistNPC.transform.position);

                if (!returnPositionList.Contains(gridPos))
                {
                    returnPositionList.Add(gridPos);
                }
            }

            return returnPositionList;
        }

        private string GetParatrooperGridLocations()
        {
            var troopPositionList = GetParatrooperPositions();

            if (troopPositionList.Count == 0)
            {
                return msg("NoParatroopers");
            }

            string returnValue = msg("ParatroopersFound");

            foreach (string gridLoc in troopPositionList)
            {
                returnValue = returnValue + " <color=orange>" + gridLoc + "</color>";
            }

            return returnValue;
        }
        #endregion

        #region Wipe Elements

        private void WipeMarkers(List<MapMarkerGenericRadius> markers)
        {
            foreach (var mapMarker in markers)
            {
                if (mapMarker == null || mapMarker.IsDestroyed)
                {
                    continue;
                }

                mapMarker.Kill();
                mapMarker.SendUpdate();
            }

            markers.Clear();
        }

        private void WipeTroopers()
        {
            foreach (var x in jumperList.ToList())
            {
                if (x != null && !x.IsDestroyed)
                {
                    x.Kill();
                }
            }

            foreach (var entry in paratrooperList.ToList())
            {
                var x = entry.Key;

                if (x != null && !x.IsDestroyed)
                {
                    x.Kill();
                }
            }

            jumperList.Clear();
            paratrooperList.Clear();
        }

        private void KillEvent()
        {
            if (paratrooperList.Count > 0 || jumperList.Count > 0)
            {
                WipeTroopers();
            }

            if (botMarkerList.Count > 0)
            {
                WipeMarkers(botMarkerList.Values.ToList());
            }

            if (dropZoneMarkerList.Count > 0)
            {
                WipeMarkers(dropZoneMarkerList);
            }

            foreach (var x in drops)
            {
                if (x != null && !x.IsDestroyed)
                {
                    x.Kill();
                }
            }

            foreach (var x in dropsCrate)
            {
                if (x != null && !x.IsDestroyed)
                {
                    x.Kill();
                }
            }

            foreach (var x in dropBradley)
            {
                if (x != null && !x.IsDestroyed)
                {
                    x.Kill();
                }
            }

            FltNum.Clear();
            ParatrooperPlane[] planes = UnityEngine.Object.FindObjectsOfType<ParatrooperPlane>();

            for (int i = 0; i < planes?.Length; i++)
            {
                UnityEngine.Object.Destroy(planes[i]);
            }
        }
        #endregion

        #region Misc Functions
        private Vector3 SpreadPosition(Vector3 origPosition, int amtToSpread)
        {
            int spreadX = Oxide.Core.Random.Range(amtToSpread * -1, amtToSpread);
            int spreadZ = Oxide.Core.Random.Range(amtToSpread * -1, amtToSpread);

            return new Vector3(origPosition.x + spreadX, origPosition.y, origPosition.z + spreadZ);
        }

        private void RepeatRandomJump()
        {
            // Call random jump

            if (BasePlayer.activePlayerList.Count >= configData.Automation.RequiredPlayers)
            {
                TriggerRandomJump();
            }
            else if (configData.Notifications.ConsoleOutput)
            {
                Puts("Not enough players for event.");
            }

            // Queue up the next one..
            float nextJump = UnityEngine.Random.Range(configData.Automation.RandomJumpIntervalMinSec, configData.Automation.RandomJumpIntervalMaxSec);
            timer.Once(nextJump, () => RepeatRandomJump());

            if (configData.Notifications.ConsoleOutput)
            {
                Puts("A random Paratroopers Event will occur in {0}s", nextJump);
            }
        }

        private SpawnFilter filter = new SpawnFilter();

        public Vector3 RandomDropPosition()
        {
            Vector3 vector;
            if (configData.Airfield)
            {
                vector = GetAirfieldPosition();

                if (vector != Vector3.zero)
                {
                    return vector;
                }
            }
            float num = 100f;
            float x = TerrainMeta.Size.x;
            do
            {
                vector = Vector3Ex.Range(0f - x / 3f, x / 3f);
            }
            while (filter.GetFactor(vector) == 0f && (num -= 1f) > 0f);
            vector.y = 0f;
            return vector;
        }

        private void TriggerRandomJump()
        {
            var jumpPosition = RandomDropPosition();

            if (configData.Notifications.ConsoleOutput)
            {
                Puts("A random Paratroopers Event will occur near {0}", GetGridString(jumpPosition));
            }

            CallParatroopers(jumpPosition, configData.npc.RandomJumperCount, null, 0, true);
        }
        #endregion

        #region Grid Teleport
        public class SpawnPosition
        {
            private const float aboveGoundPosition = 2.5f;
            public Vector3 Position;
            public Vector3 GroundPosition;
            public string GridReference;

            public SpawnPosition(Vector3 position)
            {
                Position = position;
                GroundPosition = GetGroundPosition(new Vector3(position.x, 25, position.z));
            }

            public bool isPositionAboveWater()
            {
                if ((TerrainMeta.HeightMap.GetHeight(Position) - TerrainMeta.WaterMap.GetHeight(Position)) >= 0)
                    return false;
                return true;
            }

            public float WaterDepthAtPosition()
            {
                return (TerrainMeta.WaterMap.GetHeight(Position) - TerrainMeta.HeightMap.GetHeight(Position));
            }

            Vector3 GetGroundPosition(Vector3 sourcePos)
            {
                RaycastHit hitInfo;
                if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo))
                    sourcePos.y = hitInfo.point.y;

                sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos)) + aboveGoundPosition;

                return sourcePos;
            }
        }

        #endregion

        #region Drop Chutes
        public class CrateDropChute : MonoBehaviour
        {
            private SupplyDrop crate;
            private Rigidbody rigidbody;
            private BaseEntity parachute;

            private void Awake()
            {
                rigidbody = crate.GetComponent<Rigidbody>();
            }

            private void Start()
            {
                AddParachute();
            }

            private void OnDestroy()
            {
                RemoveParachute();
            }

            private void AddParachute()
            {
                if (rigidbody != null)
                {
                    rigidbody.useGravity = true;
                }

                parachute = GameManager.server.CreateEntity(StringPool.Get(CHUTE_PREFABID));

                if (parachute != null)
                {
                    parachute.SetParent(crate, false);
                    parachute.Spawn();
                    parachute.SendNetworkUpdateImmediate(true);
                }
            }

            private void RemoveParachute()
            {
                if (parachute.IsValid())
                {
                    if (rigidbody != null)
                    {
                        rigidbody.useGravity = false;
                    }

                    if (!parachute.IsDestroyed)
                    {
                        parachute.Kill();
                    }

                    parachute = null;
                }
            }

            private void OnCollisionEnter(Collision collision)
            {
                Effect.server.Run(StringPool.Get(SUPPLYDROP_EFFECTID), transform.position);
                Destroy(this);
            }

            private void FixedUpdate()
            {
                if (parachute.IsValid())
                {
                    rigidbody.useGravity = true;
                    rigidbody.mass = configData.LootBoxes.LootEffects.lootMass;
                    rigidbody.drag = configData.LootBoxes.LootEffects.lootDrag * (rigidbody.mass / 5f);
                    crate.transform.position -= new Vector3(0, 10f * configData.LootBoxes.LootEffects.lootMass * Time.deltaTime, 0);
                    //crate.transform.position -= new Vector3(0, 10f * Time.deltaTime, 0);
                }
            }
        }

        private class HackCrateDropChute : MonoBehaviour
        {
            private BaseEntity crate;
            private BaseEntity parachute;
            private Rigidbody rigidbody;

            private void Awake()
            {
                crate = GetComponent<BaseEntity>();
                rigidbody = crate.GetComponent<Rigidbody>();
                rigidbody.useGravity = true;

                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.mass = 5.25f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                rigidbody.drag = 2.0f * (rigidbody.mass / 5f);
                rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);
            }

            private void Start()
            {
                AddParachute();
            }

            private void OnDestroy()
            {
                RemoveParachute();
            }

            private void AddParachute()
            {
                if (rigidbody != null)
                {
                    rigidbody.useGravity = false;
                }

                parachute = GameManager.server.CreateEntity(StringPool.Get(CHUTE_PREFABID));

                if (parachute != null)
                {
                    parachute.SetParent(crate, false);
                    parachute.Spawn();
                    parachute.SendNetworkUpdateImmediate(true);
                }
            }

            private void RemoveParachute()
            {
                if (parachute.IsValid())
                {
                    if (rigidbody != null)
                    {
                        rigidbody.useGravity = true;
                    }

                    if (!parachute.IsDestroyed)
                    {
                        parachute.Kill();
                    }

                    parachute = null;
                }
            }

            private void OnCollisionEnter(Collision collision)
            {
                Destroy(this);
            }

            private void FixedUpdate()
            {
                if (parachute.IsValid())
                {
                    crate.transform.position -= new Vector3(0, 10f * Time.deltaTime, 0);
                }
            }
        }

        private class DroppingVehicle : MonoBehaviour
        {
            private BaseEntity vehicle;
            private BaseEntity parachute;
            private Rigidbody rb;

            private void Awake()
            {
                vehicle = GetComponent<BaseEntity>();
                rb = vehicle.gameObject.GetComponent<Rigidbody>();
            }

            private void Start()
            {
                AddParachute();
            }

            private void OnDestroy()
            {
                RemoveParachute();
            }

            private void AddParachute()
            {
                if (rb != null)
                {
                    rb.useGravity = false;
                }

                parachute = GameManager.server.CreateEntity(StringPool.Get(CHUTE_PREFABID));

                if (parachute != null)
                {
                    parachute.SetParent(vehicle, "parachute_attach");
                    parachute.Spawn();

                    if (configData.LootBoxes.LootEffects.enableSmoke)
                    {
                        Effect.server.Run(StringPool.Get(SMOKE_EFFECTID), parachute, 0, Vector3.zero, Vector3.zero, null, true);
                    }
                }
            }

            private void RemoveParachute()
            {
                if (vehicle.IsValid() && rb != null)
                {
                    rb.useGravity = true;
                }

                if (parachute.IsValid())
                {
                    if (!parachute.IsDestroyed)
                    {
                        parachute.Kill();
                    }

                    parachute = null;
                }
            }

            private void OnCollisionEnter(Collision collision)
            {
                Destroy(this);
            }

            private void FixedUpdate()
            {
                if (parachute.IsValid())
                {
                    vehicle.transform.position -= new Vector3(0, 10f * Time.deltaTime, 0);
                }
            }
        }

        #endregion

        #region Jump Event Manager
        private class JumpEventManager
        {
            public Vector3 DropPosition = new Vector3();
            public int ParatrooperCount = 0;
            public int JumpedCount = 0;
            public string ParatrooperKit = "";
            public bool ParatrooperRandomKit;
            public int ParatrooperHealth = 0;
            private BaseEntity parachute;
            private bool EnableLootDrop;
            internal ParatrooperPlane paratrooperPlane { get; private set; }
            internal APCController APCBradley { get; private set; }

            private bool CreatedDropZoneMarker = false;

            private void a_PositionReached(object sender, EventArgs e)
            {
                Vector3 deployPos = paratrooperPlane.transform.position;
                string jumpMessage = string.Format(ins.GetInboundMessage(), ins.GetGridString(deployPos));

                foreach (int FlightNumber in ins.FltNum)
                {
                    if (configData.Notifications.Inbound)
                    {
                        string key = configData.LootBoxes.LootEffects.dropBradley ? "InboundJumpPositionMessage2" : "InboundJumpPositionMessage";

                        SendChatMessage(key, FlightNumber, ParatrooperCount, ins.GetGridString(deployPos));
                    }
                }

                if (configData.Notifications.ConsoleOutput)
                {
                    ins.Puts(jumpMessage);
                }

                CreateJumpers();
            }

            private void CreateJumpers()
            {
                var jumpPosition = ins.SpreadPosition(paratrooperPlane.transform.position, ins.GetDefaultJumpSpread());

                ServerMgr.Instance.StartCoroutine(SpawnNPCs(jumpPosition, ParatrooperKit, ParatrooperHealth, ParatrooperRandomKit));
                string ent = configData.LootBoxes.typeOfBox.ToLower();

                if (++JumpedCount < ParatrooperCount)
                {
                    float nextJumperDelay = UnityEngine.Random.Range(0.01f, 0.5f);
                    ins.timer.Once(nextJumperDelay, () => CreateJumpers());
                }


                if (configData.MapSettings.CreateDropZoneMarkers && !CreatedDropZoneMarker && JumpedCount >= (ParatrooperCount / 2))
                {
                    ins.CreateDropZoneMarker(jumpPosition);
                    CreatedDropZoneMarker = true;

                    if (configData.LootBoxes.enableLootBox)
                    {
                        if (ent == "supplydrop")
                        {
                            SpawnLoot();
                        }
                        else if (ent == "hackablecrate")
                        {
                            SpawnLootCrate();
                        }
                    }

                    if (configData.LootBoxes.LootEffects.dropBradley)
                    {
                        ins.timer.Once(0.05f, () => SpawnBradley());
                    }
                }
            }

            private static void CreateSirenLights(BaseEntity entity)
            {
                var SirenLight = GameManager.server.CreateEntity(StringPool.Get(SIRENLIGHT_EFFECTID), default(Vector3), default(Quaternion), true);

                SirenLight.gameObject.Identity();
                SirenLight.SetParent(entity as LootContainer, "parachute_attach");
                SirenLight.Spawn();
                SirenLight.SetFlag(BaseEntity.Flags.Reserved8, true);
            }

            private static void CreateSirenAlarms(BaseEntity entity)
            {
                var SirenAlarm = GameManager.server.CreateEntity(StringPool.Get(SIRENALARM_EFFECTID), default(Vector3), default(Quaternion), true);

                SirenAlarm.gameObject.Identity();
                SirenAlarm.SetParent(entity as LootContainer, "parachute_attach");
                SirenAlarm.Spawn();
                SirenAlarm.SetFlag(BaseEntity.Flags.Reserved8, true);
            }

            private void CreateDropEffects(BaseEntity entity)
            {
                if (entity == null)
                {
                    return;

                }

                if (configData.LootBoxes.LootEffects.enableSmoke)
                {
                    Effect.server.Run(StringPool.Get(SMOKE_EFFECTID), entity, 0, Vector3.zero, Vector3.zero, null, true);
                }

                if (configData.LootBoxes.LootEffects.enableSirenAlarm && configData.LootBoxes.LootEffects.enableSirenAlarmNight && TOD_Sky.Instance.IsNight)
                {
                    CreateSirenAlarms(entity);
                }
                else if (configData.LootBoxes.LootEffects.enableSirenAlarmNight && TOD_Sky.Instance.IsNight)
                {
                    CreateSirenAlarms(entity);
                }
                else if (configData.LootBoxes.LootEffects.enableSirenAlarm)
                {
                    CreateSirenAlarms(entity);
                }
                else return;

                if (configData.LootBoxes.LootEffects.enableSirenLight && configData.LootBoxes.LootEffects.enableSirenLightNight && TOD_Sky.Instance.IsNight)
                {
                    CreateSirenLights(entity);
                }
                else if (configData.LootBoxes.LootEffects.enableSirenLightNight && TOD_Sky.Instance.IsNight)
                {
                    CreateSirenLights(entity);
                }
                else if (configData.LootBoxes.LootEffects.enableSirenLight)
                {
                    CreateSirenLights(entity);
                }
                else return;
            }

            private Vector3 GetRandomPoint(Vector3 position)
            {
                var vector = position + UnityEngine.Random.onUnitSphere * 40f;

                return vector;
            }

            private IEnumerator SpawnNPCs(Vector3 position, string jumperKit, int botHealth, bool randomKit)
            {
                position = ins.SpreadPosition(paratrooperPlane.transform.position + (paratrooperPlane.transform.up + (paratrooperPlane.transform.forward * 2f)), ins.GetDefaultJumpSpread());

                var prefab = GameManager.server.FindPrefab(StringPool.Get(NPCPlayer_PREFABID));
                var go = Instantiate.GameObject(prefab, GetRandomPoint(position), default(Quaternion));
                go.SetActive(false);
                go.name = StringPool.Get(NPCPlayer_PREFABID);

                var npc = go.GetComponent<ScientistNPC>();
                npc.enableSaving = false;

                SceneManager.MoveGameObjectToScene(go, Rust.Server.EntityScene);
                npc.enableSaving = false;


                //var spawnable = go.GetComponent<Spawnable>();
                //if (spawnable)
                //    UnityEngine.Object.Destroy(spawnable);

                go.SetActive(true);
                npc.IsDormant = true;
                npc.userID = (ulong)UnityEngine.Random.Range(0, 10000000);
                npc.UserIDString = npc.userID.ToString();
                npc.displayName = RandomUsernames.Get(npc.userID);
                npc.Spawn();
                //BaseNavigator baseNavigator = npc.GetComponent<BaseNavigator>();
                //if (baseNavigator != null) { baseNavigator.CanUseNavMesh = false; }

                //npc.PauseFlyHackDetection();
                //npc.AiContext.Human.NextToolSwitchTime = Time.realtimeSinceStartup * 10;
                //npc.AiContext.Human.NextWeaponSwitchTime = Time.realtimeSinceStartup * 10;
                var paratrooper = npc.gameObject.AddComponent<NPCParatrooper>();
                paratrooper.paratrooperPlane = paratrooperPlane;

                NPCParatrooper.ParatrooperHealth = botHealth;
                NPCParatrooper.ParatrooperKit = jumperKit;
                NPCParatrooper.ParatrooperRandomKit = randomKit;
                yield return CoroutineEx.waitForEndOfFrame;
            }

            private void SpawnLoot()
            {
                var randsphere = UnityEngine.Random.onUnitSphere;
                var entpos = (paratrooperPlane.transform.position + new Vector3(0f, 1.5f, 0f)) + (randsphere * UnityEngine.Random.Range(-2f, 3f));
                var supplyDrop = GameManager.server.CreateEntity(StringPool.Get(SUPPLYDROP_PREFABID), entpos, Quaternion.LookRotation(randsphere), true) as SupplyDrop;

                supplyDrop.Spawn();

                ins.drops.Add(supplyDrop);

                if (configData.LootBoxes.LootEffects.enableEffects)
                {
                    CreateDropEffects(supplyDrop);
                }

                if (!configData.LootBoxes.LootEffects.enableChute)
                {
                    supplyDrop.RemoveParachute();
                    Rigidbody rigidbody;
                    rigidbody = supplyDrop.GetComponent<Rigidbody>();
                    rigidbody.useGravity = true;
                    rigidbody.isKinematic = false;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rigidbody.mass = configData.LootBoxes.LootEffects.lootMass;
                    rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                    rigidbody.drag = configData.LootBoxes.LootEffects.lootDrag;
                }
                else supplyDrop.GetComponent<CrateDropChute>();

                if (configData.LootBoxes.RandomItems.Maximum > 0)
                {
                    PopulateLoot(supplyDrop.inventory, configData.LootBoxes.RandomItems);
                }

                supplyDrop.SendNetworkUpdateImmediate();

                if (configData.LootBoxes.Lifetime > 0)
                {
                    ins.timer.Once(configData.LootBoxes.Lifetime, () =>
                    {
                        if (supplyDrop != null && !supplyDrop.IsDestroyed)
                        {
                            ClearContainer(supplyDrop.inventory);
                            supplyDrop.Kill();
                        }
                    });
                }
            }

            private void SpawnLootCrate()
            {
                var randsphere = UnityEngine.Random.onUnitSphere;
                var entpos = (paratrooperPlane.transform.position + new Vector3(0f, 1.5f, 0f)) + (randsphere * UnityEngine.Random.Range(-2f, 3f));
                var crate = GameManager.server.CreateEntity(StringPool.Get(HACKABLECRATE_PREFABID), entpos, Quaternion.LookRotation(randsphere), true) as HackableLockedCrate;

                crate.Spawn();

                if (configData.LootBoxes.LootEffects.enableEffects)
                {
                    CreateDropEffects(crate);
                }

                crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - configData.LootBoxes.LootEffects.requiredHackSeconds;

                if (configData.LootBoxes.LootEffects.enableChute)
                {
                    crate.GetOrAddComponent<HackCrateDropChute>();
                }

                ins.dropsCrate.Add(crate);

                if (configData.LootBoxes.RandomItems.Maximum > 0)
                {
                    PopulateLoot(crate.inventory, configData.LootBoxes.RandomItems);
                }

                crate.SendNetworkUpdateImmediate();

                if (configData.LootBoxes.Lifetime > 0)
                {
                    ins.timer.Once(configData.LootBoxes.Lifetime, () =>
                    {
                        if (crate != null && !crate.IsDestroyed)
                        {
                            ClearContainer(crate.inventory);
                            crate.Kill();
                        }
                    });
                }
            }

            private void SpawnBradley()
            {
                var randsphere = UnityEngine.Random.onUnitSphere;
                var entpos = (paratrooperPlane.transform.position + new Vector3(0f, 1.5f, 0f)) + (randsphere * UnityEngine.Random.Range(-2f, 3f));
                var bradley = GameManager.server.CreateEntity(StringPool.Get(BRADLEY_PREFABID), entpos, Quaternion.LookRotation(randsphere), true) as BradleyAPC;

                bradley.Spawn();

                if (configData.LootBoxes.LootEffects.enableChuteBradley)
                {
                    bradley.GetOrAddComponent<DroppingVehicle>();
                }

                ins.dropBradley.Add(bradley);

                if (ins.dropBradley.Count == 1)
                {
                    ins.Subscribe(nameof(CanBradleyApcTarget));
                }

                bradley.GetOrAddComponent<APCController>();
                bradley.SendNetworkUpdateImmediate();

                if (configData.LootBoxes.LootEffects.BradleyLifetime > 0)
                {
                    ins.timer.Once(configData.LootBoxes.LootEffects.BradleyLifetime, () =>
                    {
                        if (bradley != null)
                        {
                            if (configData.LootBoxes.LootEffects.BradleyGibs)
                            {
                                if (!bradley.IsDestroyed)
                                {
                                    bradley.Die(new HitInfo(bradley, bradley, Rust.DamageType.Explosion, 10000f));
                                }

                                if (configData.Notifications.APCSuicide)
                                {
                                    foreach (var target in BasePlayer.activePlayerList)
                                    {
                                        target.SendConsoleCommand("chat.add", new object[] { 2, ChatIcon, string.Format(msg("BradleySelfDestruct", target.UserIDString)) });
                                    }
                                }

                                ins.dropBradley.Remove(bradley);
                            }
                            else
                            {
                                if (!bradley.IsDestroyed)
                                {
                                    bradley.Kill();
                                }

                                if (configData.Notifications.APCSuicide)
                                {
                                    foreach (var target in BasePlayer.activePlayerList)
                                    {
                                        target.SendConsoleCommand("chat.add", new object[] { 2, ChatIcon, string.Format(msg("BradleyRecalled", target.UserIDString)) });
                                    }
                                }

                                ins.dropBradley.Remove(bradley);
                            }

                            if (ins.dropBradley.Count == 0)
                            {
                                ins.Unsubscribe(nameof(CanBradleyApcTarget));
                            }
                        }
                    });
                }
            }

            public JumpEventManager(Vector3 jumperPosition, int jumperCount, string jumperKit, int botHealth, bool randomKit)
            {
                DropPosition = jumperPosition;
                ParatrooperCount = jumperCount;
                ParatrooperKit = jumperKit;
                ParatrooperHealth = botHealth;
                ParatrooperRandomKit = randomKit;
            }

            // actions
            public void DoJump()
            {
                var cp = GameManager.server.CreateEntity(StringPool.Get(CARGOPLANE_PREFABID)) as CargoPlane;

                cp.enableSaving = false;
                cp.Spawn();

                //UnityEngine.Object.Destroy(cp.GetComponent<SavePause>());
                paratrooperPlane = cp.gameObject.AddComponent<ParatrooperPlane>();
                paratrooperPlane.InitializeFlightPath(DropPosition);
                paratrooperPlane.PositionReached += a_PositionReached;
                ins.planes[cp] = paratrooperPlane;
            }
        }

        #endregion

        #region Component

        private class NPCParatrooper : FacepunchBehaviour
        {
            internal ScientistNPC npc { get; private set; }
            internal ParatrooperPlane paratrooperPlane { get; set; }
            private Rigidbody rb { get; set; }
            private Vector3 roamDestination { get; set; }
            private float nextSetDestinationTime { get; set; }
            private BaseEntity parachute { get; set; }
            private Vector3 currentWindVector { get; set; }
            public bool isFalling { get; set; }
            public bool hasLanded { get; set; }
            public bool isMelee { get; set; }
            private ItemContainer[] containers { get; set; }
            private float woundedDuration { get; set; }
            private float woundedStartTime { get; set; }
            private Vector3 _initialPosition { get; set; }
            private int attacks { get; set; }

            public static string ParatrooperKit { get; set; } = "";
            public static bool ParatrooperRandomKit { get; set; }
            public static int ParatrooperHealth { get; set; }

            public int enemyDistance { get; set; }

            internal Vector3 InitialPosition
            {
                get
                {
                    return _initialPosition;
                }
                set
                {
                    object success = FindPointOnNavmesh(value);
                    if (success is Vector3)
                        _initialPosition = (Vector3)success;
                    else _initialPosition = value;
                }
            }

            private float secondsSinceWoundedStarted
            {
                get
                {
                    return Time.realtimeSinceStartup - woundedStartTime;
                }
            }

            private void Awake()
            {
                enabled = false;
                npc = GetComponent<ScientistNPC>();
                if (npc == null) return;

                if (ins.paratrooperList.ContainsKey(npc))
                    ins.paratrooperList[npc] = this;
                else
                    ins.paratrooperList.Add(npc, this);
                ////  userID = npc.userID;
                InitializeNPC();
                InitializeVelocity();
                Invoke(() => enabled = true, 2);
            }

            private void InitializeNPC()
            {
                
                npc.CancelInvoke(npc.EquipTest);
                npc.NavAgent.areaMask = -1;
                npc.NavAgent.agentTypeID = -1372625422;
                npc.NavAgent.enabled = false;
                npc.CancelInvoke(npc.EquipTest);
                float health = ParatrooperHealth > 0 ? ParatrooperHealth : configData.npc.Health;
                npc.startHealth = health;
                npc.InitializeHealth(health, health);
                if (configData.npc.Names.Length > 0)
                {
                    npc.displayName = configData.npc.Names.GetRandom();
                }
                if (configData.npc.Kits.Length > 0)
                {
                    //StripInventory(npc);
                    //
                   // npc.Invoke(() => plugin.GiveKit(npc, ParatrooperRandomKit ? configData.npc.Kits.GetRandom() : ParatrooperKit), 1f);
                    npc.Invoke(() => isMelee = IsMelee(npc), 2f);
                }
                if (configData.npc.Lifetime > 0)
                {
                    npc.Invoke(() => npc.Die(new HitInfo(npc, npc, Rust.DamageType.Explosion, 1000f)), configData.npc.Lifetime + UnityEngine.Random.Range(0, 30));
                }
                ins.jumperList.Add(npc);
            }

            private void OnDestroy()
            {
                if (parachute != null && !parachute.IsDestroyed)
                {
                    parachute.Kill();
                }

                if (npc != null && !npc.IsDestroyed)
                {
                    npc.Kill();
                }
            }

            #region Parachuting
            private void Update()
            {
                if (isFalling && Physics.Raycast(npc.transform.position, Vector3.down, 0.5f, LAND_LAYERS))
                {
                    enabled = false;
                    isFalling = false;

                    rb.useGravity = false;
                    rb.isKinematic = true;
                    rb.drag = 0;

                    npc.modelState.onground = true;
                    npc.modelState.flying = false;

                    if (parachute != null && !parachute.IsDestroyed)
                    {
                        parachute.Kill();
                    }

                    npc.Invoke(() => WaterCheck(npc), 2f);

                    if (configData.npc.WoundedChance == 100 || UnityEngine.Random.Range(0, 100) <= configData.npc.WoundedChance)
                    {
                        StartWounded();
                    }
                    else OnParachuteLand();
                }
            }

            private void WaterCheck(ScientistNPC npc)
            {
                if (TerrainMeta.WaterMap.GetDepth(npc.transform.position) > 0.9f)
                {
                    npc.Die(new HitInfo(npc, npc, Rust.DamageType.Drowned, 1000f));
                }
            }

            private Vector3 DirectionTowardsCrate
            {
                get
                {
                    if (ins.crate != null && !ins.crate.IsDestroyed)
                    {
                        return (ins.crate.transform.position.XZ3D() - npc.transform.position.XZ3D()).normalized;
                    }

                    return (InitialPosition.XZ3D() - npc.transform.position.XZ3D()).normalized;
                }
            }

            private void FixedUpdate()
            {
                if (!isFalling && rb.velocity.y < 0)
                {
                    DeployParachute();
                }

                if (isFalling)
                {
                    var windAtPosition = Vector3.Lerp(InitialPosition, GetWindAtCurrentPos(), paratrooperPlane != null ? 0.25f : 0.75f);
                    var heightFromGround = Mathf.Max(TerrainMeta.HeightMap.GetHeight(npc.transform.position), TerrainMeta.WaterMap.GetHeight(npc.transform.position));
                    var force = Mathf.InverseLerp(heightFromGround + 20f, heightFromGround + 60f, npc.transform.position.y);
                    var normalizedDir = (windAtPosition.normalized * force) * configData.Paratroopers.Wind;

                    currentWindVector = Vector3.Lerp(currentWindVector, normalizedDir, Time.fixedDeltaTime * 0.25f);

                    rb.AddForceAtPosition(normalizedDir * 0.1f, npc.transform.position, ForceMode.Force);
                    rb.AddForce(normalizedDir * 0.9f, ForceMode.Force);

                    var rotation = Quaternion.LookRotation(rb.velocity);
                    npc.transform.rotation = npc.eyes.rotation = npc.ServerRotation = rotation;
                    npc.viewAngles = rotation.eulerAngles;

                    parachute.transform.localRotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
                    parachute.SendNetworkUpdate();
                }
            }

            private void InitializeVelocity()
            {
                rb = npc.GetComponent<Rigidbody>();
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.AddForce((Vector3.up * 15) + (UnityEngine.Random.onUnitSphere.XZ3D() * 5), ForceMode.Impulse);
            }

            private void DeployParachute()
            {
                parachute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab",  npc.transform.position);//StringPool.Get(CHUTE_PREFABID),
                parachute.enableSaving = false;
                parachute.Spawn();

                parachute.SetParent(npc, false);
                parachute.transform.localPosition = Vector3.up * 6f;
                parachute.transform.localRotation = Quaternion.Euler(0f, npc.viewAngles.y, 0f);

                rb.drag = configData.Paratroopers.Drag;

                if (configData.Paratroopers.deploySmoke)
                {
                    Effect.server.Run(StringPool.Get(SMOKE_EFFECTID), parachute, 0, Vector3.zero, Vector3.zero, null, true);
                }

                npc.modelState.onground = false;
                npc.modelState.flying = true;
                npc.SendNetworkUpdate();
                isFalling = true;
            }

            private Vector3 GetWindAtCurrentPos()
            {
                var single = npc.transform.position.y * 6f;
                var force = new Vector3(Mathf.Sin(single * 0.0174532924f), 0f, Mathf.Cos(single * 0.0174532924f));

                return force.normalized * 1f;
            }

            private void OnParachuteLand()
            {
                object success = FindPointOnNavmesh(npc.transform.position);

                if (success is Vector3)
                {
                    npc.NavAgent.Warp(npc.transform.position = npc.ServerPosition = (Vector3)success);
                }

                if (_initialPosition == Vector3.zero)
                {
                    InitialPosition = npc.ServerPosition;
                }

                ins.jumperList.Remove(npc);

                npc.EquipTest();
                roamDestination = InitialPosition;
                UpdateTargetPosition(InitialPosition);
                hasLanded = true;
                npc.IsDormant = false;
                //UnityEngine.Debug.Log("OnParacheteLand");
                //npc.Invoke(() =>
                //{
                //    BaseNavigator baseNavigator = npc.GetComponent<BaseNavigator>();
                //    //Enables navagent so NPCs move on rig.
                //    if (baseNavigator != null) { baseNavigator.CanUseNavMesh = true; }
                //}, 4f);
            }
            #endregion

            #region Think
            public void UpdateTargetPosition(Vector3 targetPosition)
            {
                if (npc == null || npc.IsDestroyed || npc.NavAgent == null || !npc.NavAgent.isOnNavMesh)
                {
                    return;
                }

                 npc.SetDestination(targetPosition);
            }

            public void OnReceivedDamage(BasePlayer attacker, HitInfo hitInfo)
            {
                if (npc == null || npc.IsDestroyed || npc.transform == null || npc.NavAgent == null || npc.isMounted)
                {
                    return;
                }

                float distance = Vector3.Distance(attacker.transform.position, npc.transform.position);

                if (!(hitInfo.Initiator is BaseMelee))
                {
                    float newAccuracy = configData.npc.TrooperAccuracy;
                    float newDamage = configData.npc.TrooperDamageScale / 100f;

                    if (distance > 100f)
                    {
                        newAccuracy = configData.npc.TrooperAccuracy / (distance / 100f);
                        newDamage /= distance / 100f;
                    }

                    if (UnityEngine.Random.Range(1, 101) < newAccuracy)
                    {
                        hitInfo.damageTypes.ScaleAll(newDamage);
                    }
                }

                BaseNpc baseNpc = npc.GetComponent<BaseNpc>();

                if (!baseNpc) return;

                baseNpc.AttackTarget = attacker;

                if (distance > baseNpc.Stats.AggressionRange && baseNpc.Stats.AggressionRange < 200)
                {
                    baseNpc.Stats.AggressionRange += 200;
                    baseNpc.Stats.DeaggroRange += 200;
                }

                //if (distance <= baseNpc.Stats.AggressionRange && distance > 15f && baseNpc.AttackTarget == attacker)
                //{
                //    npc.SetDestination(attacker.transform.position);
                //}

                if (!hasLanded && isMelee)
                {
                    return;
                }

                if (attacks > 0)
                {
                    attacks = 1;
                    return;
                }

                AttackTarget(npc, attacker);
            }

            private void AttackTarget(ScientistNPC npc, BasePlayer attacker)
            {
                if (npc.IsDestroyed || attacker == null)
                {
                    attacks = 0;
                    return;
                }

                if (hasLanded)
                {
                    npc.RandomMove();
                }

                BaseNpc baseNpc = npc.GetComponent<BaseNpc>();

                if (attacker.IsVisible(npc.eyes.position, attacker.eyes.position, baseNpc.Stats.AggressionRange))
                {
                    npc.SetAimDirection((attacker.transform.position - npc.GetPosition()).normalized);

                    if (isMelee)
                    {
                        npc.MeleeAttack();
                    }
                    else npc.ShotTest(10);
                }

                if (++attacks >= 20)
                {
                    baseNpc.Stats.AggressionRange = configData.npc.Aggro_Range;
                    baseNpc.Stats.DeaggroRange = configData.npc.Aggro_Range * 1.25f;
                    attacks = 0;
                }
                else npc.Invoke(() => AttackTarget(npc, attacker), 2f);
            }

            private bool IsMelee(BasePlayer player)
            {
                var attackEntity = player.GetHeldEntity() as AttackEntity;

                if (attackEntity == null)
                {
                    return false;
                }

                return attackEntity is BaseMelee;
            }
            #endregion

            #region Wounded
            private void StartWounded()
            {
                woundedDuration = UnityEngine.Random.Range(configData.npc.WoundedMin, configData.npc.WoundedMax);
                woundedStartTime = Time.realtimeSinceStartup;

                ins.jumperList.Remove(npc);

                npc.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);
                npc.SetServerFall(true);
                npc.SendNetworkUpdateImmediate(false);
                Invoke(WoundingTick, 1f);
            }

            private void WoundingTick()
            {
                if (!npc.IsDead())
                {
                    if (secondsSinceWoundedStarted < woundedDuration)
                    {
                        Invoke(WoundingTick, 1f);
                    }
                    else if (UnityEngine.Random.Range(0, 100) >= configData.npc.RecoveryChance)
                    {
                        npc.Die(new HitInfo(npc, npc, Rust.DamageType.Explosion, 1000f));
                    }
                    else
                    {
                        npc.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
                        OnParachuteLand();
                    }
                }
            }
            #endregion

            #region Loot
            internal void PrepareInventory()
            {
                ItemContainer[] source = new ItemContainer[] { npc.inventory.containerMain, npc.inventory.containerWear, npc.inventory.containerBelt };

                containers = new ItemContainer[3];

                for (int i = 0; i < containers.Length; i++)
                {
                    containers[i] = new ItemContainer();
                    containers[i].ServerInitialize(null, source[i].capacity);
                    containers[i].GiveUID();

                    Item[] array = source[i].itemList.ToArray();

                    for (int j = 0; j < array.Length; j++)
                    {
                        Item item = array[j];

                        if (!item.MoveToContainer(containers[i], -1, true))
                        {
                            item.Remove();
                        }
                    }
                }
            }

            internal void MoveInventoryTo(LootableCorpse corpse)
            {
                for (int i = 0; i < containers.Length; i++)
                {
                    Item[] array = containers[i].itemList.ToArray();

                    for (int j = 0; j < array.Length; j++)
                    {
                        Item item = array[j];

                        if (!item.MoveToContainer(corpse.containers[i], -1, true))
                        {
                            item.Remove(0f);
                        }
                    }
                }

                corpse.ResetRemovalTime();
            }
            #endregion
        }
        #endregion

        #region ParatrooperPlane

        private class ParatrooperPlane : FacepunchBehaviour
        {
            public event EventHandler PositionReached;
            protected virtual void OnPositionReached(EventArgs e)
            {
                PositionReached?.Invoke(this, e);
            }

            private CargoPlane entity;
            private Vector3 targetPos;

            private Vector3 startPos;
            private Vector3 endPos;
            private float secondsToTake;

            private float planeSpeed = ins.GetPlaneSpeed();
            private float dropDistance = 75f;

            private bool hasDropped = false;

            private void Awake()
            {
                entity = GetComponent<CargoPlane>();
                entity.dropped = true;
                enabled = false;
            }

            private void Update()
            {
                float xDistance = transform.position.x - targetPos.x;
                float zDistance = transform.position.z - targetPos.z;

                if (!hasDropped && Math.Abs(xDistance) <= dropDistance && Math.Abs(zDistance) <= dropDistance)
                {
                    hasDropped = true;
                    OnPositionReached(EventArgs.Empty);
                }
            }

            private void OnDestroy()
            {
                enabled = false;
                CancelInvoke();

                if (entity != null && !entity.IsDestroyed)
                {
                    entity.Kill();
                }

                ins?.FltNum?.Clear();
            }

            public void InitializeFlightPath(Vector3 targetPos)
            {
                this.targetPos = targetPos;
                targetPos += new Vector3(UnityEngine.Random.Range(-10, 10), 0, UnityEngine.Random.Range(-10, 10));

                float size = TerrainMeta.Size.x;
                float highestPoint = ins.GetJumpHeight();

                startPos = Vector3Ex.Range(-1f, 1f);
                startPos.y = 0f;
                startPos.Normalize();
                startPos *= size * 2f;
                startPos.y = highestPoint;

                endPos = startPos * -1f;
                endPos.y = startPos.y;
                startPos += targetPos;
                endPos += targetPos;

                secondsToTake = (Vector3.Distance(startPos, endPos) / planeSpeed); // * UnityEngine.Random.Range(0.95f, 1.05f);

                entity.transform.position = startPos;
                entity.transform.rotation = Quaternion.LookRotation(endPos - startPos);

                entity.startPos = startPos;
                entity.endPos = endPos;
                entity.dropPosition = targetPos;
                entity.secondsToTake = secondsToTake;

                enabled = true;
            }

            public void GetFlightData(out Vector3 startPos, out Vector3 endPos, out float secondsToTake)
            {
                startPos = this.startPos;
                endPos = this.endPos;
                secondsToTake = this.secondsToTake;
            }

            public void SetFlightData(Vector3 startPos, Vector3 endPos, Vector3 targetPos, float secondsToTake)
            {
                this.startPos = startPos;
                this.endPos = endPos;
                this.targetPos = targetPos;
                this.secondsToTake = secondsToTake;

                entity.transform.position = startPos;
                entity.transform.rotation = Quaternion.LookRotation(endPos - startPos);

                entity.startPos = startPos;
                entity.endPos = endPos;
                entity.dropPosition = targetPos;
                entity.secondsToTake = secondsToTake;

                enabled = true;
            }
        }

        #endregion

        #region Bradley ACP
        private class APCController : MonoBehaviour
        {
            protected internal BradleyAPC entity { get; private set; }
            private bool isDying = false;
            private void Awake()
            {
                entity = GetComponent<BradleyAPC>();
                entity.enabled = true;
                entity.ClearPath();
                entity.IsAtFinalDestination();
                entity.searchRange = 100f;
                entity.maxCratesToSpawn = configData.LootBoxes.LootEffects.BradleyCrates;
                entity.InitializeHealth(configData.LootBoxes.LootEffects.BradleyHealth, configData.LootBoxes.LootEffects.BradleyHealth);
            }

            private void OnDestroy()
            {
                //if (entity != null && !entity.IsDestroyed) entity.Kill();
            }

            public void ManageDamage(HitInfo info)
            {
                if (isDying)
                {
                    return;
                }

                if (info.damageTypes.Total() >= entity.health)
                {
                    info.damageTypes = new Rust.DamageTypeList();
                    info.HitEntity = null;
                    info.HitMaterial = 0;
                    info.PointStart = Vector3.zero;

                    OnDeath();
                }
            }

            private void RemoveCrate(LockedByEntCrate crate)
            {
                if (crate == null || crate.IsDestroyed)
                {
                    return;
                }

                crate.Kill();
            }

            private void OnDeath()
            {
                isDying = true;
                Effect.server.Run(entity.explosionEffect.resourcePath, entity.transform.position, Vector3.up, null, true);

                var serverGibs = ServerGib.CreateGibs(entity.servergibs.resourcePath, entity.gameObject, entity.servergibs.Get().GetComponent<ServerGib>()._gibSource, Vector3.zero, 3f);

                for (int i = 0; i < 12 - entity.maxCratesToSpawn; i++)
                {
                    BaseEntity fireBall = GameManager.server.CreateEntity(entity.fireBall.resourcePath, entity.transform.position, entity.transform.rotation, true);
                    if (fireBall)
                    {
                        Vector3 onSphere = UnityEngine.Random.onUnitSphere;
                        fireBall.transform.position = (entity.transform.position + new Vector3(0f, 1.5f, 0f)) + (onSphere * UnityEngine.Random.Range(-4f, 4f));
                        Collider collider = fireBall.GetComponent<Collider>();
                        fireBall.Spawn();
                        fireBall.SetVelocity(Vector3.zero + (onSphere * UnityEngine.Random.Range(3, 10)));
                        foreach (ServerGib serverGib in serverGibs)
                        {
                            Physics.IgnoreCollision(collider, serverGib.GetCollider(), true);
                        }
                    }
                }

                if (entity != null && !entity.IsDestroyed)
                {
                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
        }
        #endregion

        #region chat/console commands
        [ChatCommand("pt")]
        private void cmdChatParatroopers(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            if (args.Length == 0)
            {
                var helpmsg = new StringBuilder();
                helpmsg.Append("<size=22><color=green>Paratroopers</color></size> by: FastBurst\n");
                helpmsg.Append("<color=orange>/pt kill | cancel</color> - Cancels the Paratroopers Event\n");
                helpmsg.Append("<color=orange>/pt loc</color> - Display the location of current Paratroopers\n");
                helpmsg.Append("<color=orange>/pt count</color> - Display any active counts of Paratroopers\n");
                helpmsg.Append("<color=orange>/pt random</color> - Start Paratrooper Event at a Random Location\n");
                helpmsg.Append($"<color=orange>/pt static</color> - Start Paratrooper Event at coordinates <color=green>({configData.Automation.JumpPositionX}, {configData.Automation.JumpPositionZ})</color>\n");
                helpmsg.Append("<color=orange>/pt call x x</color> - Start Paratrooper Event at specific coordinates <color=green>(x, x)</color>\n");
                helpmsg.Append("<color=orange>/pt player playername</color> - Start Paratrooper Event at Players Location\n");
                helpmsg.Append("<color=orange>/pt player playername kitname count health(optional)</color> - Start Paratrooper Event at Players Location with a specific kit and set number of paratroopers");
                SendReply(player, helpmsg.ToString().TrimEnd());
                return;
            }

            switch (args[0].ToLower())
            {
                case "kill":
                case "cancel":
                    KillEvent();
                    PrintWarning("Cleaning up event. This may take a few seconds to cycle thru. You will see a few warnings. Nothing to worry about.");
                    SendReply(player, "All Paratroopers Events has been canceled");
                    return;
                case "loc":
                    player.ChatMessage(GetParatrooperGridLocations());
                    return;
                case "count":
                    SendReply(player, $"Paratroopers Count: {paratrooperList.Count} \nParatroopers Active Jumpers: {jumperList.Count}");
                    return;
                case "random":
                    TriggerRandomJump();
                    SendReply(player, "Paratroopers will be deployed to a random location");
                    return;
                case "static":
                    Vector3 jumpPosition = ins.SpreadPosition(GetDropPosition(), 0);
                    CallParatroopers(jumpPosition, configData.npc.RandomJumperCount, null, 0, true);
                    SendReply(player, $"ParaTroopers will be deployed to coordinates <color=green>({configData.Automation.JumpPositionX}, {configData.Automation.JumpPositionZ})</color>");
                    return;
                case "call":
                    Vector3 position = new Vector3();
                    float x, z;
                    if (!float.TryParse(args[1], out x) || !float.TryParse(args[2], out z))
                    {
                        SendReply(player, "Invalid Coordinates");
                        return;
                    }
                    else position = new Vector3(x, 0, z);
                    SendReply(player, $"ParaTroopers will be deployed to coordinates {x} - {z}");
                    CallParatroopers(position, configData.npc.RandomJumperCount, null, 0, true);
                    return;
                case "player":
                    Vector3 location = Vector3.zero;
                    IPlayer targetPlayer = covalence.Players.FindPlayer(args[1]);

                    if (targetPlayer != null && targetPlayer.IsConnected)
                    {
                        BasePlayer target = targetPlayer?.Object as BasePlayer;
                        if (target != null)
                        {
                            if (args.Length == 4 || args.Length == 5)
                            {
                                var cmdKits = args[2];
                                int cmdCount = Convert.ToInt32(args[3]);

                                int cmdHealth = 0;
                                if (args.Length == 5)
                                    int.TryParse(args[4], out cmdHealth);

                                if (cmdKits != null)
                                {
                                    location = target.transform.position;
                                    SendReply(player, $"<color=orange>{cmdCount} Paratroopers sent towards </color>{target.displayName}'s current position");
                                    if (configData.Notifications.ConsoleOutput)
                                        Puts($"{cmdCount} Paratroopers sent towards {target.displayName}'s current position");
                                    CallParatroopers(location, cmdCount, cmdKits, cmdHealth, false);
                                    return;
                                }
                                else
                                {
                                    SendReply(player, "Invalid syntax!");
                                    return;
                                }
                            }

                            location = target.transform.position;
                            SendReply(player, $"<color=orange>Paratroopers sent towards </color>{target.displayName}'s current position");
                            if (configData.Notifications.ConsoleOutput)
                                Puts($"Paratroopers sent towards {target.displayName}'s current position");
                            CallParatroopers(location, configData.npc.RandomJumperCount, null, 0, true);
                            return;
                        }
                    }
                    else
                    {
                        SendReply(player, "<color=orange>Could not locate the specified player</color>");
                        return;
                    }

                    return;
                default:
                    SendReply(player, "Invalid syntax!");
                    break;
            }
        }

        [ChatCommand("paratroopers")]
        private void cmdChatParatroopersLoc(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, GetParatrooperGridLocations());
                return;
            }

            int cooldown = -1;

            if (permission.UserHasPermission(player.UserIDString, PLAYER_PERM))
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

            if (player.IsAdmin)
                cooldown = 0;

            if (cooldown < 0)
                return;

            if (cooldown != 0 && storedData.IsOnCooldown(player))
            {
                SendReply(player, string.Format(msg("OnCooldown", player.UserIDString), FormatTime(storedData.GetTimeRemaining(player))));
                return;
            }

            if (args.Length > 0)
            {
                if (args.Length == 1)
                {
                    if (!permission.UserHasPermission(player.UserIDString, PLAYER_PERM))
                    {
                        SendReply(player, "You do not have permission to use this command");
                        return;
                    }

                    Vector3 location = Vector3.zero;
                    IPlayer targetPlayer = covalence.Players.FindPlayer(args[0]);
                    if (targetPlayer != null && targetPlayer.IsConnected)
                    {
                        BasePlayer target = targetPlayer?.Object as BasePlayer;
                        if (target != null)
                        {
                            location = target.transform.position;
                            SendReply(player, $"<color=orange>Paratroopers sent towards </color>{target.displayName}'s current position");
                            storedData.AddCooldown(player, cooldown);
                            CallParatroopers(location, configData.npc.RandomJumperCount, null, 0, true);
                        }
                    }
                    else
                    {
                        SendReply(player, "<color=orange>Could not locate the specified player</color>");
                        return;
                    }
                }
            }

            if (cooldown > 0)
                storedData.AddCooldown(player, cooldown);
        }

        [ConsoleCommand("pt")]
        private void ccmdGetTroopLocations(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            if (arg.Args == null || arg.Args.Length == 0)
            {
                Puts("pt kill | cancel - Cancels the Paratroopers Event");
                Puts("pt loc - Display the location of current Paratroopers");
                Puts("pt count - Display any active counts of Paratroopers");
                Puts("pt random - Start Paratrooper Event at a Random Location");
                Puts("pt static - Start Paratrooper Event at coordinates ({0}, {1})", configData.Automation.JumpPositionX, configData.Automation.JumpPositionZ);
                Puts("pt call x x - Start Paratrooper Event at specific coordinates (x, x)");
                Puts("pt player playername - Start Paratrooper Event at Players Location");
                Puts("pt player playername kitname count health(optional) - Start Paratrooper Event at Players Location with a specific kit and set number of paratroopers");
                return;
            }

            switch (arg.Args[0].ToLower())
            {
                case "kill":
                case "cancel":
                    KillEvent();
                    PrintWarning("Cleaning up event. This may take a few seconds to cycle thru. You will see a few warnings. Nothing to worry about.");
                    Puts("All Paratroopers Event has been canceled");
                    return;
                case "loc":
                    Puts(GetParatrooperGridLocations());
                    return;
                case "count":
                    Puts("paratrooperList={0}, jumperList={1}", paratrooperList.Count, jumperList.Count);
                    return;
                case "random":
                    TriggerRandomJump();
                    return;
                case "static":
                    Puts("ParaTroopers will be deployed to coordinates {0} - {1}", configData.Automation.JumpPositionX, configData.Automation.JumpPositionZ);
                    Vector3 jumpPosition = ins.SpreadPosition(GetDropPosition(), 0);
                    CallParatroopers(jumpPosition, configData.npc.RandomJumperCount, null, 0, true);
                    return;
                case "call":
                    Vector3 position = new Vector3();
                    float x, z;
                    if (!float.TryParse(arg.Args[1], out x) || !float.TryParse(arg.Args[2], out z))
                    {
                        Puts("Invalid Coordinates");
                        return;
                    }
                    else position = new Vector3(x, 0, z);
                    Puts("ParaTroopers will be deployed to coordinates {0} - {1}", x, z);
                    CallParatroopers(position, configData.npc.RandomJumperCount, null, 0, true);
                    return;
                case "player":
                    Vector3 location = Vector3.zero;
                    IPlayer targetPlayer = covalence.Players.FindPlayer(arg.Args[1]);
                    if (targetPlayer != null && targetPlayer.IsConnected)
                    {
                        BasePlayer target = targetPlayer?.Object as BasePlayer;
                        if (target != null)
                        {
                            if (arg.Args.Length == 4 || arg.Args.Length == 5)
                            {
                                var cmdKits = arg.Args[2];
                                int cmdCount = Convert.ToInt32(arg.Args[3]);

                                int cmdHealth = 0;

                                if (arg.Args.Length == 5)
                                    int.TryParse(arg.Args[4], out cmdHealth);

                                if (cmdKits != null)
                                {
                                    location = target.transform.position;
                                    Puts($"{cmdCount} Paratroopers sent towards {target.displayName}'s current position");
                                    CallParatroopers(location, cmdCount, cmdKits, cmdHealth, false);
                                    return;
                                }
                                else
                                {
                                    Puts("Invalid syntax!");
                                    return;
                                }
                            }

                            location = target.transform.position;
                            Puts($"Paratroopers sent towards {target.displayName}'s current position");
                            CallParatroopers(location, configData.npc.RandomJumperCount, null, 0, true);
                            return;
                        }
                    }
                    else
                    {
                        Puts("Could not locate the specified player");
                        return;
                    }

                    return;
                default:
                    break;
            }
        }
        #endregion

        private const int TARGET_LAYERS = ~(Layers.Mask.Invisible | Layers.Mask.Trigger | Layers.Mask.Prevent_Movement | Layers.Mask.Prevent_Building); // credits ZoneManager
        private List<Vector3> _airfieldPositions = new List<Vector3>();

        private Vector3 GetAirfieldPosition()
        {
            if (_airfieldPositions.Count == 0)
            {
                var airfield = TerrainMeta.Path.Monuments.FirstOrDefault(x => x.displayPhrase.english == "Airfield");

                if (airfield == null)
                {
                    return Vector3.zero;
                }

                var position = airfield.transform.position;
                RaycastHit hit;

                for (int i = 0; i < 100; i++)
                {
                    var vector = UnityEngine.Random.insideUnitSphere * 200f + position;

                    vector.y = 500f;

                    if (Physics.Raycast(vector, Vector3.down, out hit, vector.y, TARGET_LAYERS, QueryTriggerInteraction.Ignore) && hit.collider is TerrainCollider)
                    {
                        _airfieldPositions.Add(hit.point);
                    }
                }
            }

            return _airfieldPositions[UnityEngine.Random.Range(0, _airfieldPositions.Count())];
        }

        #region External calls
        private void CallParatroopers(Vector3 jumperPosition, int jumperCount, string jumperKit, int bothHealth, bool randomKit)
        {
            var jumpEvent = new JumpEventManager(jumperPosition, jumperCount, jumperKit, bothHealth, randomKit);

            jumpEvent.DoJump();
            GetFlightNumber();

            foreach (int FlightNumber in FltNum)
            {
                if (configData.Notifications.Inbound)
                {
                    SendChatMessage("IncomingMessage", FlightNumber);
                }
            }

            if (configData.Notifications.ConsoleOutput)
            {
                Puts("A Paratroopers Events has just started.");
            }
        }
        #endregion

        #region Config
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Event Automation")]
            public AutomationOptions Automation { get; set; }

            [JsonProperty(PropertyName = "Map Marker Settings")]
            public MapSettingOptions MapSettings { get; set; }

            [JsonProperty(PropertyName = "Notification Options")]
            public NotificationOptions Notifications { get; set; }

            [JsonProperty(PropertyName = "Paratrooper Chute Options")]
            public ParatroopersOptions Paratroopers { get; set; }

            [JsonProperty(PropertyName = "npc Paratrooper Options")]
            public NPCOptions npc { get; set; }

            [JsonProperty(PropertyName = "Loot Container Options")]
            public Loot LootBoxes { get; set; }

            [JsonProperty(PropertyName = "Command Cooldowns Timers (permission / time in minutes)")]
            public Dictionary<string, int> Cooldowns { get; set; }

            [JsonProperty(PropertyName = "Spawn At Airfield Only")]
            public bool Airfield { get; set; }

            public class AutomationOptions
            {
                [JsonProperty(PropertyName = "Automatically spawn Paratroopers Events on a timer")]
                public bool AutoSpawn { get; set; }
                [JsonProperty(PropertyName = "Auto-spawn time minimum (seconds)")]
                public float RandomJumpIntervalMinSec { get; set; }
                [JsonProperty(PropertyName = "Auto-spawn time maximum (seconds)")]
                public float RandomJumpIntervalMaxSec { get; set; }
                [JsonProperty(PropertyName = "Minimum amount of online players to trigger the event")]
                public int RequiredPlayers { get; set; }
                [JsonProperty(PropertyName = "Paratrooper Plane Jump Height")]
                public float jumpHeight { get; set; }
                [JsonProperty(PropertyName = "Paratrooper Plane incoming speed")]
                public float PlaneSpeed { get; set; }
                [JsonProperty(PropertyName = "Static Paratrooper Jump Point X Coordinate")]
                public float JumpPositionX { get; set; }
                [JsonProperty(PropertyName = "Static Paratrooper Jump Point Z Coordinate")]
                public float JumpPositionZ { get; set; }
            }

            public class MapSettingOptions
            {
                [JsonProperty(PropertyName = "Paratrooper Marker Refresh rate (seconds)")]
                public float BotMarkerRefreshInterval { get; set; }
                [JsonProperty(PropertyName = "Create Paratroopers Drop Zone Marker on map")]
                public bool CreateDropZoneMarkers { get; set; }
                [JsonProperty(PropertyName = "Paratroopers Drop Zone Radius")]
                public float DropZoneRadius { get; set; }
                [JsonProperty(PropertyName = "Paratroopers Drop Zone Alpha Shading")]
                public float DropZoneAlpha { get; set; }
                [JsonProperty(PropertyName = "Create Individual Paratrooper Landing Zone Markers on map")]
                public bool CreateBotMarkers { get; set; }
                [JsonProperty(PropertyName = "Paratroopers individual Landing Zone Radius")]
                public float BotDropZoneRadius { get; set; }
                [JsonProperty(PropertyName = "Paratroopers individual Landing Zone Alpha Shading")]
                public float BotDropZoneAlpha { get; set; }
            }

            public class NotificationOptions
            {
                [JsonProperty(PropertyName = "Show notification when Paratroopers are inbound")]
                public bool Inbound { get; set; }
                [JsonProperty(PropertyName = "Show notification when Paratroopers begin Jumping")]
                public bool JumpNotification { get; set; }
                [JsonProperty(PropertyName = "Show notification when a npc has been killed")]
                public bool NPCDeath { get; set; }
                [JsonProperty(PropertyName = "Show notification when a npc commited Suicide/Drowned/Despawned")]
                public bool NPCSuicide { get; set; }
                [JsonProperty(PropertyName = "Show notification when the Bradley APC has been destroyed")]
                public bool APCDeath { get; set; }
                [JsonProperty(PropertyName = "Show notification when the Bradley APC SelfDestruct/Recalled")]
                public bool APCSuicide { get; set; }
                [JsonProperty(PropertyName = "Show notifications in Console")]
                public bool ConsoleOutput { get; set; }
            }

            public class ParatroopersOptions
            {
                [JsonProperty(PropertyName = "Parachute drag force")]
                public float Drag { get; set; }
                [JsonProperty(PropertyName = "Wind force")]
                public float Wind { get; set; }
                [JsonProperty(PropertyName = "Paratrooper Spread Distance")]
                public int defaultJumpSpread { get; set; }
                [JsonProperty(PropertyName = "Deploy Paratroopers with Smoke Trail while parachuting down")]
                public bool deploySmoke { get; set; }
            }

            public class NPCOptions
            {
                [JsonProperty(PropertyName = "Amount of npc Paratroopers to spawn")]
                public int RandomJumperCount { get; set; }
                [JsonProperty(PropertyName = "npc Paratrooper display name (chosen at random)")]
                public string[] Names { get; set; }
                [JsonProperty(PropertyName = "npc Paratrooper Kit to be used (chosen at random)")]
                public string[] Kits { get; set; }
                [JsonProperty(PropertyName = "npc Health")]
                public int Health { get; set; }
                [JsonProperty(PropertyName = "npc Paratroopers attack Accuracy (0 - 100)")]
                public int TrooperAccuracy { get; set; }
                [JsonProperty(PropertyName = "npc Paratroopers attack Damage Scale (0 - 100)")]
                public int TrooperDamageScale { get; set; }
                [JsonProperty(PropertyName = "npc Paratroopers Aggrovation Range")]
                public int Aggro_Range { get; set; }
                [JsonProperty(PropertyName = "npc Paratroopers DeAggrovation Range")]
                public int DeAggro_Range { get; set; }
                [JsonProperty(PropertyName = "npc Paratroopers use Peace Keeper mode")]
                public bool Peace_Keeper { get; set; }
                [JsonProperty(PropertyName = "npc Paratroopers use Peace Keeper Cooldown")]
                public int Peace_Keeper_Cool_Down { get; set; }
                [JsonProperty(PropertyName = "Always use Light Sources if available")]
                public bool AlwaysUseLights { get; set; }
                [JsonProperty(PropertyName = "Chance of being wounded when landing (x / 100)")]
                public int WoundedChance { get; set; }
                [JsonProperty(PropertyName = "Wounded duration minimum time (seconds)")]
                public int WoundedMin { get; set; }
                [JsonProperty(PropertyName = "Wounded duration maximum time (seconds)")]
                public int WoundedMax { get; set; }
                [JsonProperty(PropertyName = "Chance of recovery from being wounded(x / 100)")]
                public int RecoveryChance { get; set; }
                [JsonProperty(PropertyName = "Amount of time the NPCs will be alive before suiciding (seconds, 0=disable)")]
                public int Lifetime { get; set; }
                [JsonProperty(PropertyName = "Roam distance from landing position")]
                public float RoamDistance { get; set; }
                [JsonProperty(PropertyName = "Loot type (Default, Inventory, Random)")]
                public string LootType { get; set; }
                [JsonProperty(PropertyName = "Random loot items")]
                public LootContainer RandomItems { get; set; }
            }

            public class Loot
            {
                [JsonProperty(PropertyName = "Enable Loot Box to drop with Paratroopers")]
                public bool enableLootBox { get; set; }
                [JsonProperty(PropertyName = "What type of Loot box to drop with Paratroopers (supplydrop|hackablecrate)")]
                public string typeOfBox { get; set; }
                [JsonProperty(PropertyName = "Amount of time before despawning Loot Box (seconds, 0=disable)")]
                public int Lifetime { get; set; }
                [JsonProperty(PropertyName = "Loot Box Effects Settings")]
                public LootEffectsOptions LootEffects { get; set; }
                [JsonProperty(PropertyName = "Loot container items")]
                public LootContainer RandomItems { get; set; }
            }

            public class LootEffectsOptions
            {
                [JsonProperty(PropertyName = "Enable Loot Effects")]
                public bool enableEffects { get; set; }
                [JsonProperty(PropertyName = "Enable Parachute Loot Box for either Supply Drop or HackableCrate")]
                public bool enableChute { get; set; }
                [JsonProperty(PropertyName = "Supply Drop Drag Force (without parachute)")]
                public float lootDrag { get; set; }
                [JsonProperty(PropertyName = "Supply Drop Mass Weigth (without parachute")]
                public float lootMass { get; set; }
                [JsonProperty(PropertyName = "Hackable Crate Drag Force")]
                public float lootDragCrate { get; set; }
                [JsonProperty(PropertyName = "Hackable Crate Weigth")]
                public float lootMassCrate { get; set; }
                [JsonProperty(PropertyName = "Hackable Crate time to unlock (seconds)")]
                public int requiredHackSeconds { get; set; }
                [JsonProperty(PropertyName = "Bradley ACP - Air drop a Bradley APC with Paratroopers")]
                public bool dropBradley { get; set; }
                [JsonProperty(PropertyName = "Bradley APC - Enable Parachute for Air Drop")]
                public bool enableChuteBradley { get; set; }
                [JsonProperty(PropertyName = "Bradley ACP - Amount of Crates to drop after death (0=disable)")]
                public int BradleyCrates { get; set; }
                [JsonProperty(PropertyName = "Bradley APC - Health Amount")]
                public float BradleyHealth { get; set; }
                [JsonProperty(PropertyName = "Bradley ACP - Amount of time before despawning (seconds, 0=disable)")]
                public int BradleyLifetime { get; set; }
                [JsonProperty(PropertyName = "Bradley ACP - Allow to explode instead of despawning")]
                public bool BradleyGibs { get; set; }
                [JsonProperty(PropertyName = "Enable Smoke trail Loot Box and Bradley APC on drop")]
                public bool enableSmoke { get; set; }
                [JsonProperty(PropertyName = "Enable Spinning Light trail Loot Box on drop")]
                public bool enableSirenLight { get; set; }
                [JsonProperty(PropertyName = "Enable Spinning Light trail Loot Box on drop at night only")]
                public bool enableSirenLightNight { get; set; }
                [JsonProperty(PropertyName = "Enable Siren Alarm trail Loot Box on drop")]
                public bool enableSirenAlarm { get; set; }
                [JsonProperty(PropertyName = "Enable Siren Alarm trail Loot Box on drop at night only")]
                public bool enableSirenAlarmNight { get; set; }
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
                [JsonProperty(PropertyName = "Item Display Name")]
                public string itemDisplayName { get; set; }
            }

            [JsonProperty(PropertyName = "PVP Distance")]
            public float Distance { get; set; } = 200f;

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {

            }

            if (configData == null)
            {
                LoadDefaultConfig();
            }

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig()
        {
            configData = GetBaseConfig();
        }

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Automation = new ConfigData.AutomationOptions
                {
                    AutoSpawn = true,
                    RandomJumpIntervalMinSec = 1800,
                    RandomJumpIntervalMaxSec = 5400,
                    RequiredPlayers = 1,
                    PlaneSpeed = 80f,
                    jumpHeight = 200f,
                    JumpPositionX = 0f,
                    JumpPositionZ = 0f
                },
                MapSettings = new ConfigData.MapSettingOptions
                {
                    BotMarkerRefreshInterval = 15f,
                    CreateDropZoneMarkers = true,
                    DropZoneRadius = 1f,
                    DropZoneAlpha = 0.4f,
                    CreateBotMarkers = true,
                    BotDropZoneRadius = 0.5f,
                    BotDropZoneAlpha = 0.35f
                },
                Notifications = new ConfigData.NotificationOptions
                {
                    Inbound = true,
                    JumpNotification = true,
                    NPCDeath = true,
                    NPCSuicide = true,
                    APCDeath = true,
                    APCSuicide = true,
                    ConsoleOutput = true
                },
                Paratroopers = new ConfigData.ParatroopersOptions
                {
                    Drag = 2f,
                    Wind = 10f,
                    defaultJumpSpread = 20,
                    deploySmoke = true
                },
                npc = new ConfigData.NPCOptions
                {
                    RandomJumperCount = 10,
                    Names = new string[] { "Col. Jones", "Maj. Smith", "Capt. Willams", "Pfc. Garcia", "Sgt. Morris", "Lt. Richards", "Pvt. Sossaman" },
                    Kits = new string[] { "Soldier1", "Soldier2", "Soldier3", "Soldier5", "Soldier6", "Soldier7" },
                    Health = 250,
                    TrooperAccuracy = 40,
                    TrooperDamageScale = 50,
                    Aggro_Range = 80,
                    DeAggro_Range = 50,
                    Peace_Keeper = false,
                    Peace_Keeper_Cool_Down = 5,
                    AlwaysUseLights = true,
                    WoundedChance = 20,
                    WoundedMin = 60,
                    WoundedMax = 180,
                    RecoveryChance = 50,
                    Lifetime = 900,
                    RoamDistance = 50,
                    LootType = "Random",
                    RandomItems = new ConfigData.LootContainer
                    {
                        Minimum = 5,
                        Maximum = 12,
                        Items = new ConfigData.LootItem[]
                        {
                            new ConfigData.LootItem {Name = "apple", Skin = 0, Maximum = 6, Minimum = 2, itemDisplayName = "Apple" },
                            new ConfigData.LootItem {Name = "bearmeat.cooked", Skin = 0, Maximum = 4, Minimum = 2, itemDisplayName = "Cooked Bear Meat" },
                            new ConfigData.LootItem {Name = "blueberries", Skin = 0, Maximum = 8, Minimum = 4, itemDisplayName = "Blueberries" },
                            new ConfigData.LootItem {Name = "corn", Skin = 0, Maximum = 8, Minimum = 4, itemDisplayName = "Corn" },
                            new ConfigData.LootItem {Name = "fish.raw", Skin = 0, Maximum = 4, Minimum = 2, itemDisplayName = "Raw Fish" },
                            new ConfigData.LootItem {Name = "granolabar", Skin = 0, Maximum = 4, Minimum = 1, itemDisplayName = "Granola Bar" },
                            new ConfigData.LootItem {Name = "meat.pork.cooked", Skin = 0, Maximum = 8, Minimum = 4, itemDisplayName = "Cooked Pork" },
                            new ConfigData.LootItem {Name = "syringe.medical", Skin = 0, Maximum = 6, Minimum = 2, itemDisplayName = "Medical Syringe" },
                            new ConfigData.LootItem {Name = "largemedkit", Skin = 0, Maximum = 2, Minimum = 1, itemDisplayName = "Large Medkit" },
                            new ConfigData.LootItem {Name = "bandage", Skin = 0, Maximum = 4, Minimum = 1, itemDisplayName = "Bandage" },
                            new ConfigData.LootItem {Name = "antiradpills", Skin = 0, Maximum = 3, Minimum = 1, itemDisplayName = "Anti-Radiation Pills" },
                            new ConfigData.LootItem {Name = "ammo.rifle", Skin = 0, Maximum = 100, Minimum = 10, itemDisplayName = "5.56 Rifle Ammo" },
                            new ConfigData.LootItem {Name = "ammo.pistol", Skin = 0, Maximum = 100, Minimum = 10, itemDisplayName = "Pistol Bullet" },
                            new ConfigData.LootItem {Name = "ammo.rocket.basic", Skin = 0, Maximum = 10, Minimum = 1, itemDisplayName = "Rocket" },
                            new ConfigData.LootItem {Name = "ammo.shotgun.slug", Skin = 0, Maximum = 20, Minimum = 10, itemDisplayName = "12 Gauge Slug" },
                            new ConfigData.LootItem {Name = "pistol.m92",Skin = 0,  Maximum = 1, Minimum = 1, itemDisplayName = "M92 Pistol" },
                            new ConfigData.LootItem {Name = "rifle.l96", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "L96 Rifle" },
                            new ConfigData.LootItem {Name = "rifle.lr300", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "LR-300 Assault Rifle" },
                            new ConfigData.LootItem {Name = "rifle.ak", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "Assault Rifle" },
                            new ConfigData.LootItem {Name = "rifle.bolt", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "Bolt Action Rifle" },
                            new ConfigData.LootItem {Name = "rocket.launcher", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "Rocket Launcher" },
                            new ConfigData.LootItem {Name = "pistol.revolver", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "Revolver" }
                        }
                    }
                },
                LootBoxes = new ConfigData.Loot
                {
                    enableLootBox = true,
                    typeOfBox = "supplydrop",
                    Lifetime = 900,
                    LootEffects = new ConfigData.LootEffectsOptions
                    {
                        enableEffects = true,
                        enableChute = true,
                        lootDrag = 1.0f,
                        lootMass = 10.25f,
                        lootDragCrate = 2.0f,
                        lootMassCrate = 5.25f,
                        requiredHackSeconds = 300,
                        dropBradley = true,
                        enableChuteBradley = true,
                        BradleyCrates = 2,
                        BradleyHealth = 1500f,
                        BradleyLifetime = 900,
                        BradleyGibs = true,
                        enableSmoke = true,
                        enableSirenLight = true,
                        enableSirenLightNight = true,
                        enableSirenAlarm = true,
                        enableSirenAlarmNight = true
                    },
                    RandomItems = new ConfigData.LootContainer
                    {
                        Minimum = 7,
                        Maximum = 15,
                        Items = new ConfigData.LootItem[]
                        {
                            new ConfigData.LootItem {Name = "apple", Skin = 0, Maximum = 6, Minimum = 2, itemDisplayName = "Apple" },
                            new ConfigData.LootItem {Name = "bearmeat.cooked", Skin = 0, Maximum = 4, Minimum = 2, itemDisplayName = "Cooked Bear Meat" },
                            new ConfigData.LootItem {Name = "blueberries", Skin = 0, Maximum = 8, Minimum = 4, itemDisplayName = "Blueberries" },
                            new ConfigData.LootItem {Name = "corn", Skin = 0, Maximum = 8, Minimum = 4, itemDisplayName = "Corn" },
                            new ConfigData.LootItem {Name = "fish.raw", Skin = 0, Maximum = 4, Minimum = 2, itemDisplayName = "Raw Fish" },
                            new ConfigData.LootItem {Name = "granolabar", Skin = 0, Maximum = 4, Minimum = 1, itemDisplayName = "Granola Bar" },
                            new ConfigData.LootItem {Name = "meat.pork.cooked", Skin = 0, Maximum = 8, Minimum = 4, itemDisplayName = "Cooked Pork" },
                            new ConfigData.LootItem {Name = "syringe.medical", Skin = 0, Maximum = 6, Minimum = 2, itemDisplayName = "Medical Syringe" },
                            new ConfigData.LootItem {Name = "largemedkit", Skin = 0, Maximum = 2, Minimum = 1, itemDisplayName = "Large Medkit" },
                            new ConfigData.LootItem {Name = "bandage", Skin = 0, Maximum = 4, Minimum = 1, itemDisplayName = "Bandage" },
                            new ConfigData.LootItem {Name = "antiradpills", Skin = 0, Maximum = 3, Minimum = 1, itemDisplayName = "Anti-Radiation Pills" },
                            new ConfigData.LootItem {Name = "ammo.rifle", Skin = 0, Maximum = 100, Minimum = 10, itemDisplayName = "5.56 Rifle Ammo" },
                            new ConfigData.LootItem {Name = "ammo.pistol", Skin = 0, Maximum = 100, Minimum = 10, itemDisplayName = "Pistol Bullet" },
                            new ConfigData.LootItem {Name = "ammo.rocket.basic", Skin = 0, Maximum = 10, Minimum = 1, itemDisplayName = "Rocket" },
                            new ConfigData.LootItem {Name = "ammo.shotgun.slug", Skin = 0, Maximum = 20, Minimum = 10, itemDisplayName = "12 Gauge Slug" },
                            new ConfigData.LootItem {Name = "pistol.m92",Skin = 0,  Maximum = 1, Minimum = 1, itemDisplayName = "M92 Pistol" },
                            new ConfigData.LootItem {Name = "rifle.l96", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "L96 Rifle" },
                            new ConfigData.LootItem {Name = "rifle.lr300", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "LR-300 Assault Rifle" },
                            new ConfigData.LootItem {Name = "rifle.ak", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "Assault Rifle" },
                            new ConfigData.LootItem {Name = "rifle.bolt", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "Bolt Action Rifle" },
                            new ConfigData.LootItem {Name = "rocket.launcher", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "Rocket Launcher" },
                            new ConfigData.LootItem {Name = "pistol.revolver", Skin = 0, Maximum = 1, Minimum = 1, itemDisplayName = "Revolver" }
                        }
                    }
                },
                Cooldowns = new Dictionary<string, int>
                {
                    ["paratroopers.cancall.default"] = 120,
                    ["paratroopers.cancall.vip"] = 60
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(2, 0, 0))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(2, 1, 2))
            {
                configData.npc.WoundedChance = 20;
                configData.npc.RecoveryChance = 100;
                configData.npc.RoamDistance = 50;
                configData.npc.LootType = "Random";
                configData.npc.Lifetime = 900;
                configData.Notifications.NPCSuicide = true;
            }

            if (configData.Version < new Core.VersionNumber(2, 1, 4))
            {
                configData.npc.RandomItems = baseConfig.npc.RandomItems;
                configData.LootBoxes = baseConfig.LootBoxes;
            }

            if (configData.Version < new Core.VersionNumber(2, 1, 5))
                configData.LootBoxes.LootEffects.requiredHackSeconds = 300;

            if (configData.Version < new Core.VersionNumber(2, 2, 3))
            {
                configData.npc.WoundedMin = 60;
                configData.npc.WoundedMax = 180;
            }

            if (configData.Version < new Core.VersionNumber(2, 2, 5))
            {
                configData.LootBoxes.LootEffects.lootDrag = configData.LootBoxes.LootEffects.lootDrag;
                configData.LootBoxes.LootEffects.lootMass = configData.LootBoxes.LootEffects.lootMass;
                configData.LootBoxes.LootEffects.enableChute = configData.LootBoxes.LootEffects.enableChute;
            }

            if (configData.Version < new Core.VersionNumber(2, 2, 6))
            {
                configData.npc.TrooperAccuracy = 80;
                configData.npc.TrooperDamageScale = 50;
                configData.npc.Aggro_Range = 80;
                configData.npc.DeAggro_Range = 80;
                configData.npc.Peace_Keeper = false;
                configData.npc.Peace_Keeper_Cool_Down = 5;
                configData.npc.AlwaysUseLights = true;
            }

            if (configData.Version < new Core.VersionNumber(2, 2, 8))
            {
                configData.LootBoxes.LootEffects.dropBradley = true;
                configData.LootBoxes.LootEffects.BradleyHealth = 1500f;
                configData.LootBoxes.LootEffects.BradleyLifetime = 900;
                configData.LootBoxes.LootEffects.BradleyGibs = true;
                configData.LootBoxes.LootEffects.enableChuteBradley = true;
                configData.Notifications.APCDeath = true;
                configData.Notifications.APCSuicide = true;
            }

            if (configData.Version < new Core.VersionNumber(2, 3, 0))
                configData.LootBoxes.LootEffects.BradleyCrates = 2;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management
        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("Paratroopers");

            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {

            }
            finally
            {
                if (data == null)
                {
                    storedData = new StoredData();
                }

                SaveData();
                timer.Every(Core.Random.Range(500, 700f), SaveData);
            }
        }

        private class StoredData
        {
            public Dictionary<ulong, double> cooldowns = new Dictionary<ulong, double>();

            private double CurrentTime() => Facepunch.Math.Epoch.Current; // DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

            public void AddCooldown(BasePlayer player, int time)
            {
                if (cooldowns.ContainsKey(player.userID))
                {
                    cooldowns[player.userID] = CurrentTime() + (time * 60);
                }
                else cooldowns.Add(player.userID, CurrentTime() + (time * 60));
            }

            public bool IsOnCooldown(BasePlayer player)
            {
                double time = GetTimeRemaining(player);

                if (time <= 0)
                {
                    cooldowns.Remove(player.userID);
                    return false;
                }

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
        private static void SendChatMessage(string key, params object[] args)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                player.ChatMessage(args != null ? string.Format(msg(key, player.UserIDString), args) : msg(key, player.UserIDString));
            }
        }

        private static string msg(string key, string playerId = null) => ins.lang.GetMessage(key, ins, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["IncomingMessage"] = "<color=green>[Paratroopers]</color> <color=red>ALERT WARNING</color>: Be advised, that <color=#C4FF00>ZTL Flight {0}</color> is currently inbound and will deploy a group of <color=#0099CC>Cobalt Paratroopers</color> to the island. Their mission is to find any surviors and eliminate them.</color>",
            ["InboundJumpPositionMessage"] = "<color=green>[Paratroopers]</color> <color=red>ALERT WARNING</color>: Be advised, <color=#C4FF00>ZTL Flight {0}</color> has begun deploying <color=#0099CC>{1} Cobalt Paratroopers</color> over the area near map grid marker <color=orange>{2}</color>. Use extreme caution, these soldiers are very dangerous and their intent is to kill you!",
            ["InboundJumpPositionMessage2"] = "<color=green>[Paratroopers]</color> <color=red>ALERT WARNING</color>: Be advised, <color=#C4FF00>ZTL Flight {0}</color> has begun deploying <color=#0099CC>{1} Cobalt Paratroopers</color> and a <color=#0099CC>Elite Class Bradley</color> over the area near map grid marker <color=orange>{2}</color>. Use extreme caution, these soldiers are very dangerous and their intent is to kill you!",
            ["InboundJumpPositionMessageChat"] = "Paratroopers have deployed near map grid marker {0}",
            ["NoParatroopers"] = "No paratroopers found",
            ["FoundParatroopers"] = "Paratroopers found in the following grid locations:",
            ["OnCooldown"] = "You must wait another <color=orange>{0}</color> before you can call for more Paratroopers",
            ["ParatrooperKilled"] = "<size=12><color=#0099CC>{0} finished off Paratrooper {1} at one of the Landing Zones.</color></size>",
            ["ParatrooperKilledSelf"] = "<size=12><color=#0099CC>Paratrooper {0} decided they had enough and took the easy way out at one of the Landing Zones.</color></size>",
            ["ParatrooperDrowned"] = "<size=12><color=#0099CC>Paratrooper {0} found out they couldn't swim with all the attack gear and is now lost to Davey Jones Locker.</color></size>",
            ["BradleyKilled"] = "<size=12><color=#0099CC>{0} just completely annihialted the Elite Class Bradley APC near one of the Paratrooper Drop Zones!</color></size>",
            ["BradleySelfDestruct"] = "<size=12><color=#0099CC>A massive explosion just went off in a Drop Zone! The Elite Class Bradley APC self-destructed!</color></size>",
            ["BradleyRecalled"] = "<size=12><color=#0099CC>The Elite Class Bradley APC was just recalled from one of the Drop Zones!</color></size>"
        };

        private int FlightNumber;
        #endregion

    }
}