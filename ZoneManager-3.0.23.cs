using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Zone Manager", "k1lly0u", "3.0.23")]
    [Description("An advanced management system for creating in-game zones")]
    public class ZoneManager : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin Backpacks, PopupNotifications, Spawns;

        private StoredData storedData;

        private DynamicConfigFile data;

        private readonly Hash<string, Zone> zones = new Hash<string, Zone>();

        private readonly Hash<ulong, EntityZones> zonedPlayers = new Hash<ulong, EntityZones>();

        private readonly Hash<uint, EntityZones> zonedEntities = new Hash<uint, EntityZones>();

        private readonly Dictionary<ulong, string> lastPlayerZone = new Dictionary<ulong, string>();


        private ZoneFlags globalFlags;

        private bool zonesInitialized = false;


        private static ZoneManager Instance { get; set; }

        private const string PERMISSION_ZONE = "zonemanager.zone";

        private const string PERMISSION_IGNORE_FLAG = "zonemanager.ignoreflag.";

        private const int PLAYER_MASK = 131072;

        private const int TARGET_LAYERS = ~(1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            Instance = this;

            lang.RegisterMessages(Messages, this);
            
            permission.RegisterPermission(PERMISSION_ZONE, this);

            foreach (object flag in Enum.GetValues(typeof(ZoneFlags)))
                permission.RegisterPermission(PERMISSION_IGNORE_FLAG + flag.ToString().ToLower(), this);

            LoadData();
        }

        private void OnServerInitialized()
        {
            InitializeZones();
            InitializeUpdateBehaviour();
        }

        private void OnTerrainInitialized() => InitializeZones();

        private void OnPlayerConnected(BasePlayer player) => updateBehaviour.QueueUpdate(player);

        private void OnEntityKill(BaseEntity baseEntity)
        {
            if (!baseEntity || !baseEntity.IsValid() || baseEntity.IsDestroyed)
                return;

            EntityZones entityZones;
            if (zonedEntities.TryGetValue(baseEntity.net.ID, out entityZones))
            {
                for (int i = entityZones.Zones.Count - 1; i >= 0; i--)
                {
                    entityZones.Zones.ElementAt(i)?.OnEntityExitZone(baseEntity, false, true);
                }

                zonedEntities.Remove(baseEntity.net.ID);
            }
        }

        private void Unload()
        {
            DestroyUpdateBehaviour();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, ZMUI);

            foreach (KeyValuePair<string, Zone> kvp in zones)
                UnityEngine.Object.Destroy(kvp.Value.gameObject);

            zones.Clear();

            Instance = null;
        }
        #endregion

        #region UpdateQueue  
        private UpdateBehaviour updateBehaviour;

        private void InitializeUpdateBehaviour()
        {
            updateBehaviour = new GameObject("ZoneManager.UpdateBehaviour").AddComponent<UpdateBehaviour>();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                updateBehaviour.QueueUpdate(player);
        }

        private void DestroyUpdateBehaviour() => UnityEngine.Object.Destroy(updateBehaviour?.gameObject);

        // Queue and check players for new zones and that they are still in old zones. Previously any plugin that put a player to sleep and teleports them out of a zone
        // without calling the OnPlayerSleep hook would bypass a player zone update which would result in players being registered in zones they were no longer in.
        // Options are to either continually check and update players, or have every plugin that teleports players call the hook...
        private class UpdateBehaviour : MonoBehaviour
        {
            private System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

            private Queue<BasePlayer> playerUpdateQueue = new Queue<BasePlayer>();

            private const float MAX_MS = 0.25f;

            private void OnDestroy()
            {
                playerUpdateQueue.Clear();
            }

            internal void QueueUpdate(BasePlayer player)
            {
                if (!playerUpdateQueue.Contains(player))
                    playerUpdateQueue.Enqueue(player);
            }

            private void Update()
            {
                if (Time.frameCount % 10 != 0)
                    return;

                sw.Reset();
                sw.Start();

                while (playerUpdateQueue.Count > 0)
                {
                    if (sw.Elapsed.TotalMilliseconds >= MAX_MS)
                    {
                        sw.Stop();
                        return;
                    }

                    BasePlayer player = playerUpdateQueue.Dequeue();
                    if (!player || !player.IsConnected)
                        continue;

                    Instance.UpdatePlayerZones(player);

                    InvokeHandler.Invoke(this, () => QueueUpdate(player), 2f);
                }
            }
        }
        #endregion

        #region Flag Hooks
        private void OnEntityBuilt(Planner planner, GameObject gObject)
        {
            BasePlayer player = planner?.GetOwnerPlayer();
            if (!player)
                return;

            BaseEntity entity = gObject?.ToBaseEntity();
            if (!entity)
                return;

            if (entity is BuildingBlock || entity is SimpleBuildingBlock)
            {
                if (HasPlayerFlag(player, ZoneFlags.NoBuild, true))
                {
                    entity.Invoke(() => entity.Kill(BaseNetworkable.DestroyMode.Gib), 0.1f);
                    SendMessage(player, Message("noBuild", player.UserIDString));
                }
            }
            else
            {
                if (entity is BuildingPrivlidge)
                {
                    if (HasPlayerFlag(player, ZoneFlags.NoCup, false))
                    {
                        entity.Invoke(() => entity.Kill(BaseNetworkable.DestroyMode.Gib), 0.1f);
                        SendMessage(player, Message("noCup", player.UserIDString));
                    }
                }
                else
                {
                    if (HasPlayerFlag(player, ZoneFlags.NoDeploy, true))
                    {
                        entity.Invoke(() => entity.Kill(BaseNetworkable.DestroyMode.Gib), 0.1f);
                        SendMessage(player, Message("noDeploy", player.UserIDString));
                    }
                }
            }
        }

        private object OnStructureUpgrade(BuildingBlock buildingBlock, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoUpgrade, true))
            {
                SendMessage(player, Message("noUpgrade", player.UserIDString));
                return true;
            }
            return null;
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity deployedEntity)
        {
            BasePlayer player = deployer.GetOwnerPlayer();
            if (!player)
                return;

            if (HasPlayerFlag(player, ZoneFlags.NoDeploy, true))
            {
                deployedEntity.Invoke(() => deployedEntity.Kill(BaseNetworkable.DestroyMode.Gib), 0.1f);
                SendMessage(player, Message("noDeploy", player.UserIDString));
            }
        }

        private void OnItemUse(Item item, int amount)
        {
            BaseEntity entity = item?.parent?.entityOwner;
            if (!entity)
                return;

            if (entity is FlameTurret || entity is AutoTurret || entity is GunTrap)
            {
                if (HasEntityFlag(entity, ZoneFlags.InfiniteTrapAmmo))
                    item.amount += amount;
                return;
            }

            if (entity is SearchLight)
            {
                if (HasEntityFlag(entity, ZoneFlags.AlwaysLights))
                {
                    item.amount += amount;
                    return;
                }

                if (HasEntityFlag(entity, ZoneFlags.AutoLights))
                {
                    if (TOD_Sky.Instance.Cycle.Hour > Instance.configData.AutoLights.OnTime || TOD_Sky.Instance.Cycle.Hour < Instance.configData.AutoLights.OffTime)
                        item.amount += amount;
                }
            }
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity ownerEntity, float delta)
        {
            BasePlayer player = ownerEntity as BasePlayer;
            if (!player)
                return;

            if (metabolism.bleeding.value > 0 && HasPlayerFlag(player, ZoneFlags.NoBleed, false))
                metabolism.bleeding.value = 0f;
            if (metabolism.oxygen.value < 1 && HasPlayerFlag(player, ZoneFlags.NoDrown, false))
                metabolism.oxygen.value = 1f;
        }

        private object OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (!player)
                return null;

            if (HasPlayerFlag(player, ZoneFlags.NoChat, true))
            {
                SendMessage(player, Message("noChat", player.UserIDString));
                return true;
            }
            return null;
        }

        private object OnBetterChat(Oxide.Core.Libraries.Covalence.IPlayer iPlayer, string message)
        {
            BasePlayer player = iPlayer.Object as BasePlayer;
            return OnPlayerChat(player, message, ConVar.Chat.ChatChannel.Global);
        }

        private object OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoVoice, true))
            {
                SendMessage(player, Message("noVoice", player.UserIDString));
                return true;
            }
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (!player || string.IsNullOrEmpty(arg.cmd?.Name))
                return null;

            if (arg.cmd.Name == "kill" && HasPlayerFlag(player, ZoneFlags.NoSuicide, false))
            {
                SendMessage(player, Message("noSuicide", player.UserIDString));
                return true;
            }
            return null;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (!player)
                return;

            if (HasPlayerFlag(player, ZoneFlags.KillSleepers, true))
            {
                player.Die();
                return;
            }

            if (HasPlayerFlag(player, ZoneFlags.EjectSleepers, true))
            {
                EntityZones entityZones;
                if (!zonedPlayers.TryGetValue(player.userID, out entityZones) || entityZones.Count == 0)
                    return;

                for (int i = 0; i < entityZones.Count; i++)
                {
                    Zone zone = entityZones.Zones.ElementAt(i);
                    if (!zone)
                        continue;

                    if (HasFlag(zone, ZoneFlags.EjectSleepers))
                    {
                        EjectPlayer(player, zone);
                        return;
                    }
                }
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (!entity || entity.GetComponent<ResourceDispenser>() != null)
                return null;

            BasePlayer attacker = hitinfo.InitiatorPlayer;
            BasePlayer victim = entity as BasePlayer;

            if (victim != null)
            {
                if (hitinfo.damageTypes.GetMajorityDamageType() == DamageType.Fall)
                {
                    if (HasPlayerFlag(victim, ZoneFlags.NoFallDamage, false))
                        return true;
                }

                if (victim.IsSleeping() && HasPlayerFlag(victim, ZoneFlags.SleepGod, false))
                    return true;
                else if (attacker != null)
                {
                    if (IsNpc(victim))
                        return null;

                    if (HasPlayerFlag(victim, ZoneFlags.PvpGod, false))
                    {
                        if (attacker == victim && hitinfo.damageTypes.GetMajorityDamageType() == DamageType.Suicide)
                        {
                            if (HasPlayerFlag(victim, ZoneFlags.NoSuicide, false))
                                return true;
                            return null;
                        }
                        if (IsNpc(attacker) && configData.NPCHurtPvpGod)
                            return null;

                        return true;
                    }
                    else if (HasPlayerFlag(attacker, ZoneFlags.PvpGod, false) && !IsNpc(attacker))                    
                        return true;                    
                }
                else if (HasPlayerFlag(victim, ZoneFlags.PveGod, false) && !IsNpc(victim))
                    return true;
                else if (hitinfo.Initiator is FireBall && HasPlayerFlag(victim, ZoneFlags.PvpGod, false))
                    return true;
                return null;
            }

            BaseNpc baseNpc = entity as BaseNpc;
            if (baseNpc != null)
            {
                if (HasEntityFlag(baseNpc, ZoneFlags.NoPve))
                {
                    if (attacker != null && CanBypass(attacker, ZoneFlags.NoPve))
                        return null;
                    return true;
                }
                return null;
            }

            if (!(entity is LootContainer) && !(entity is BaseHelicopter))
            {
                if (HasEntityFlag(entity, ZoneFlags.UnDestr))
                {
                    if (hitinfo.InitiatorPlayer != null && CanBypass(hitinfo.InitiatorPlayer, ZoneFlags.UnDestr))
                        return null;

                    if (hitinfo.damageTypes.GetMajorityDamageType() == DamageType.Decay && configData.DecayDamageUndestr)
                        return null;

                    return true;
                }
            }

            return null;
        }

        private void OnEntitySpawned(BaseNetworkable baseNetworkable)
        {
            if (baseNetworkable is BaseEntity)
                timer.In(2, () => CanSpawn(baseNetworkable as BaseEntity));
        }

        private void CanSpawn(BaseEntity baseEntity)
        {
            if (!baseEntity.IsValid() || baseEntity.IsDestroyed)
                return;

            if (Interface.CallHook("CanSpawnInZone", baseEntity) != null)
                return;

            if (baseEntity is BaseCorpse)
            {
                if (HasEntityFlag(baseEntity, ZoneFlags.NoCorpse) && !CanBypass((baseEntity as BaseCorpse).OwnerID, ZoneFlags.NoCorpse))
                    baseEntity.Invoke(() => baseEntity.Kill(BaseNetworkable.DestroyMode.None), 0.1f);
            }
            if (baseEntity is LootContainer || baseEntity is JunkPile)
            {
                if (HasEntityFlag(baseEntity, ZoneFlags.NoLootSpawns))
                    baseEntity.Invoke(() => baseEntity.Kill(BaseNetworkable.DestroyMode.None), 0.1f);
            }
            else if (baseEntity is BaseNpc || baseEntity is NPCPlayer)
            {
                if (HasEntityFlag(baseEntity, ZoneFlags.NoNPCSpawns))
                    baseEntity.Invoke(() => baseEntity.Kill(BaseNetworkable.DestroyMode.None), 0.1f);
            }
            else if (baseEntity is DroppedItem || baseEntity is WorldItem)
            {
                if (HasEntityFlag(baseEntity, ZoneFlags.NoDrop))
                {
                    (baseEntity as WorldItem).item.Remove(0f);
                    baseEntity.Invoke(() => baseEntity.Kill(BaseNetworkable.DestroyMode.None), 0.1f);
                }
            }
            else if (baseEntity is DroppedItemContainer)
            {
                if (HasEntityFlag(baseEntity, ZoneFlags.NoDrop))
                    baseEntity.Invoke(() => baseEntity.Kill(BaseNetworkable.DestroyMode.None), 0.1f);
            }
        }

        private object CanBeWounded(BasePlayer player, HitInfo hitinfo) => HasPlayerFlag(player, ZoneFlags.NoWounded, false) ? (object)false : null;

        private object CanUpdateSign(BasePlayer player, Signage sign)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoSignUpdates, false))
            {
                SendMessage(player, Message("noSignUpdates", player.UserIDString));
                return false;
            }
            return null;
        }

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoOvenToggle, false))
            {
                SendMessage(player, Message("noOvenToggle", player.UserIDString));
                return true;
            }
            return null;
        }

        private object CanUseVending(BasePlayer player, VendingMachine machine)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoVending, false))
            {
                SendMessage(player, Message("noVending", player.UserIDString));
                return false;
            }
            return null;
        }

        private object CanHideStash(BasePlayer player, StashContainer stash)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoStash, false))
            {
                SendMessage(player, Message("noStash", player.UserIDString));
                return false;
            }
            return null;
        }

        private object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            BasePlayer player = itemCrafter.GetComponent<BasePlayer>();
            if (player != null)
            {
                if (HasPlayerFlag(player, ZoneFlags.NoCraft, false))
                {
                    SendMessage(player, Message("noCraft", player.UserIDString));
                    return false;
                }
            }
            return null;
        }

        private void OnDoorOpened(Door door, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoDoorAccess, false))
            {
                SendMessage(player, Message("noDoor", player.UserIDString));
                door.CloseRequest();
            }
        }

        #region Looting Hooks
        private object CanLootPlayer(BasePlayer target, BasePlayer looter) => OnLootPlayerInternal(looter, target);

        private void OnLootPlayer(BasePlayer looter, BasePlayer target) => OnLootPlayerInternal(looter, target);

        private object OnLootPlayerInternal(BasePlayer looter, BasePlayer target)
        {
            if (HasPlayerFlag(looter, ZoneFlags.NoPlayerLoot, false) || (target != null && HasPlayerFlag(target, ZoneFlags.NoPlayerLoot, false)))
            {
                if (looter == target && Backpacks != null)
                {
                    object hookResult = Backpacks.Call("CanLootPlayer", target, looter);
                    if (hookResult is bool && (bool)hookResult)
                        return true;
                }

                SendMessage(looter, Message("noLoot", looter.UserIDString));
                NextTick(looter.EndLooting);
                return false;
            }
            return null;
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity is LootableCorpse)
                OnLootCorpse(entity as LootableCorpse, player);
            if (entity is DroppedItemContainer)
                OnLootContainer(entity as DroppedItemContainer, player);
            if (entity is StorageContainer)
                OnLootInternal(player, ZoneFlags.NoBoxLoot);
        }

        private object CanLootEntity(BasePlayer player, LootableCorpse corpse)
        {
            if (corpse.playerSteamID == player.userID && HasPlayerFlag(player, ZoneFlags.LootSelf, false))
                return null;
            return CanLootInternal(player, ZoneFlags.NoPlayerLoot);
        }

        private void OnLootCorpse(LootableCorpse corpse, BasePlayer player)
        {
            if (corpse.playerSteamID == player.userID && HasPlayerFlag(player, ZoneFlags.LootSelf, false))
                return;

            OnLootInternal(player, ZoneFlags.NoPlayerLoot);
        }

        private void OnLootContainer(DroppedItemContainer container, BasePlayer player)
        {
            if (container.playerSteamID == player.userID && HasPlayerFlag(player, ZoneFlags.LootSelf, false))
                return;

            OnLootInternal(player, ZoneFlags.NoPlayerLoot);
        }

        private object CanLootEntity(BasePlayer player, DroppedItemContainer container)
        {
            if (container.playerSteamID == player.userID && HasPlayerFlag(player, ZoneFlags.LootSelf, false))
                return null;

            return CanLootInternal(player, ZoneFlags.NoPlayerLoot);
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container) => CanLootInternal(player, ZoneFlags.NoBoxLoot);

        private object CanLootInternal(BasePlayer player, ZoneFlags flag)
        {
            if (HasPlayerFlag(player, flag, false))
            {
                SendMessage(player, Message("noLoot", player.UserIDString));
                return false;
            }
            return null;
        }

        private void OnLootInternal(BasePlayer player, ZoneFlags flag)
        {
            if (HasPlayerFlag(player, flag, false))
            {
                SendMessage(player, Message("noLoot", player.UserIDString));
                NextTick(player.EndLooting);
            }
        }
        #endregion

        #region Pickup Hooks
        private object CanPickupEntity(BasePlayer player, BaseCombatEntity entity) => CanPickupInternal(player, ZoneFlags.NoEntityPickup);

        private object CanPickupLock(BasePlayer player, BaseLock baseLock) => CanPickupInternal(player, ZoneFlags.NoEntityPickup);

        private object OnItemPickup(Item item, BasePlayer player) => CanPickupInternal(player, ZoneFlags.NoPickup);

        private object CanPickupInternal(BasePlayer player, ZoneFlags flag)
        {
            if (HasPlayerFlag(player, flag, false))
            {
                SendMessage(player, Message("noPickup", player.UserIDString));
                return false;
            }
            return null;
        }
        #endregion

        #region Gather Hooks        
        private object CanLootEntity(ResourceContainer container, BasePlayer player) => OnGatherInternal(player);

        private object OnCollectiblePickup(Item item, BasePlayer player) => OnGatherInternal(player);

        private object OnGrowableGather(GrowableEntity plant, Item item, BasePlayer player) => OnGatherInternal(player);

        private object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item) => OnGatherInternal(entity?.ToPlayer());

        private object OnGatherInternal(BasePlayer player)
        {
            if (player != null)
            {
                if (HasPlayerFlag(player, ZoneFlags.NoGather, false))
                {
                    SendMessage(player, Message("noGather", player.UserIDString));
                    return true;
                }
            }
            return null;
        }
        #endregion

        #region Targeting Hooks
        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity) => OnTargetPlayerInternal(entity?.ToPlayer(), ZoneFlags.NoTurretTargeting);

        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            if (HasPlayerFlag(entity?.ToPlayer(), ZoneFlags.NoAPCTargeting, false))
                return false;
            return null;
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoHeliTargeting, false))
            {
                heli.interestZoneOrigin = heli.GetRandomPatrolDestination();
                return false;
            }
            return null;
        }

        private object CanHelicopterStrafeTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoHeliTargeting, false))
                return false;
            return null;
        }

        private object OnHelicopterTarget(HelicopterTurret turret, BaseCombatEntity entity) => OnTargetPlayerInternal(entity?.ToPlayer(), ZoneFlags.NoHeliTargeting);

        private object OnNpcTarget(BaseCombatEntity entity, BasePlayer player) => OnTargetPlayerInternal(player, ZoneFlags.NoNPCTargeting);

        private object OnTargetPlayerInternal(BasePlayer player, ZoneFlags flag)
        {
            if (player != null)
            {
                if (HasPlayerFlag(player, flag, false))
                    return true;
            }
            return null;
        }
        #endregion

        #region Additional KillSleeper Checks
        private void OnPlayerSleep(BasePlayer player)
        {
            if (!player)
                return;

            //player.Invoke(()=> UpdatePlayerZones(player), 1f); // Manually update the zones a player is in. Sleeping players don't trigger OnTriggerEnter or OnTriggerExit            

            timer.In(2f, () =>
            {
                if (!player || !player.IsSleeping())
                    return;

                if (!player.IsConnected)
                {
                    if (HasPlayerFlag(player, ZoneFlags.KillSleepers, true))
                    {
                        player.Invoke(() => KillSleepingPlayer(player), 3f);
                        return;
                    }

                    if (HasPlayerFlag(player, ZoneFlags.EjectSleepers, true))
                    {
                        player.Invoke(() =>
                        {
                            if (!player || !player.IsSleeping())
                                return;

                            EntityZones entityZones;
                            if (!zonedPlayers.TryGetValue(player.userID, out entityZones) || entityZones.Count == 0)
                                return;

                            for (int i = 0; i < entityZones.Count; i++)
                            {
                                Zone zone = entityZones.Zones.ElementAt(i);
                                if (!zone)
                                    return;

                                if (HasFlag(zone, ZoneFlags.EjectSleepers))
                                {
                                    EjectPlayer(player, zone);
                                }
                            }
                        }, 3f);
                    }
                }
            });
        }

        private void OnPlayerSleepEnd(BasePlayer player) => updateBehaviour.QueueUpdate(player);

        private void KillSleepingPlayer(BasePlayer player)
        {
            if (!player || !player.IsSleeping())
                return;

            if (HasPlayerFlag(player, ZoneFlags.KillSleepers, true))
            {
                if (player.IsConnected)
                    OnPlayerSleep(player);
                else player.Die();
            }
        }

        private void UpdatePlayerZones(BasePlayer player)
        {
            if (!player)
                return;

            EntityZones entityZones;
            if (zonedPlayers.TryGetValue(player.userID, out entityZones))
            {
                for (int i = entityZones.Count - 1; i >= 0; i--)
                {
                    Zone zone = entityZones.Zones.ElementAt(i);
                    if (!zone || !zone.definition.Enabled)
                        continue;

                    if (zone.definition.Size != Vector3.zero)
                    {
                        if (!IsInsideBounds(zone, player.transform.position))
                            OnPlayerExitZone(player, zone);
                    }
                    else
                    {
                        if (Vector3.Distance(player.transform.position, zone.transform.position) > zone.definition.Radius)
                            OnPlayerExitZone(player, zone);
                    }
                }
            }

            for (int i = 0; i < zones.Count; i++)
            {
                Zone zone = zones.ElementAt(i).Value;
                if (!zone)
                    continue;

                if (entityZones != null && entityZones.Zones.Contains(zone))
                    continue;

                if (zone.definition.Size != Vector3.zero)
                {
                    if (IsInsideBounds(zone, player.transform.position))
                        OnPlayerEnterZone(player, zone);
                }
                else
                {
                    if (Vector3.Distance(player.transform.position, zone.transform.position) <= zone.definition.Radius)
                        OnPlayerEnterZone(player, zone);
                }
            }

            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.SafeZone) && !player.InSafeZone())
                player.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, false);
        }

        private bool IsInsideBounds(Zone zone, Vector3 worldPos) => zone?.collider?.ClosestPoint(worldPos) == worldPos;
        #endregion
        #endregion

        #region Zone Functions
        private void InitializeZones()
        {
            if (zonesInitialized)
                return;

            foreach (Zone.Definition definition in storedData.definitions)
                CreateZone(definition);

            foreach (Zone zone in zones.Values)
                zone.FindZoneParent();

            zonesInitialized = true;

            UnsubscribeAll();
            UpdateHookSubscriptions();
        }

        private void CreateZone(Zone.Definition definition)
        {
            Zone zone = new GameObject().AddComponent<Zone>();
            zone.InitializeZone(definition);

            zones.Add(definition.Id, zone);
        }

        private bool ReverseVelocity(BasePlayer player)
        {
            BaseVehicle baseVehicle = player.GetMounted().VehicleParent();
            if (baseVehicle != null)
            {
                Vector3 euler = baseVehicle.transform.eulerAngles;
                baseVehicle.transform.rotation = Quaternion.Euler(euler.x, euler.y - 180f, euler.z);
                baseVehicle.rigidBody.velocity *= -1f;
                return true;
            }
            return false;
        }

        private void EjectPlayer(BasePlayer player, Zone zone)
        {
            if (zone.keepInList.Contains(player.userID) || zone.whitelist.Contains(player.userID))
                return;

            if (!string.IsNullOrEmpty(zone.definition.Permission))
            {
                if (HasPermission(player, zone.definition.Permission))
                    return;
            }

            if (player.isMounted && ReverseVelocity(player))
            {
                SendMessage(player, Message("eject", player.UserIDString));
                return;
            }

            Vector3 position = Vector3.zero;
            if (Spawns && !string.IsNullOrEmpty(zone.definition.EjectSpawns))
            {
                object success = Spawns.Call("GetRandomSpawn", zone.definition.EjectSpawns);
                if (success is Vector3)
                    position = (Vector3)success;
            }

            if (position == Vector3.zero)
            {
                float distance;
                if (zone.definition.Size != Vector3.zero)
                    distance = Mathf.Max(zone.definition.Size.x, zone.definition.Size.z);
                else distance = zone.definition.Radius;

                position = zone.transform.position + (((player.transform.position.XZ3D() - zone.transform.position.XZ3D()).normalized) * (distance + 10f));

                RaycastHit rayHit;
                if (Physics.Raycast(new Ray(new Vector3(position.x, position.y + 300, position.z), Vector3.down), out rayHit, 500, TARGET_LAYERS, QueryTriggerInteraction.Ignore))
                    position.y = rayHit.point.y;
                else position.y = TerrainMeta.HeightMap.GetHeight(position);
            }

            player.MovePosition(position);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
            player.SendNetworkUpdateImmediate();

            SendMessage(player, Message("eject", player.UserIDString));
        }

        private void AttractPlayer(BasePlayer player, Zone zone)
        {
            if (player.isMounted && ReverseVelocity(player))
            {
                SendMessage(player, Message("attract", player.UserIDString));
                return;
            }

            float distance;
            if (zone.definition.Size != Vector3.zero)
                distance = Mathf.Max(zone.definition.Size.x, zone.definition.Size.z);
            else distance = zone.definition.Radius;

            Vector3 position = zone.transform.position + (player.transform.position - zone.transform.position).normalized * (distance - 5f);
            position.y = TerrainMeta.HeightMap.GetHeight(position);

            player.MovePosition(position);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
            player.SendNetworkUpdateImmediate();

            SendMessage(player, Message("attract", player.UserIDString));
        }

        private void ShowZone(BasePlayer player, string zoneId, float time = 30)
        {
            Zone zone = GetZoneByID(zoneId);
            if (!zone)
                return;

            if (zone.definition.Size != Vector3.zero)
            {
                Vector3 center = zone.definition.Location;
                Quaternion rotation = Quaternion.Euler(zone.definition.Rotation);
                Vector3 size = zone.definition.Size / 2;
                Vector3 point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
                Vector3 point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
                Vector3 point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
                Vector3 point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
                Vector3 point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
                Vector3 point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
                Vector3 point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
                Vector3 point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

                player.SendConsoleCommand("ddraw.line", time, Color.blue, point1, point2);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point1, point3);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point1, point5);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point4, point2);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point4, point3);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point4, point8);

                player.SendConsoleCommand("ddraw.line", time, Color.blue, point5, point6);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point5, point7);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point6, point2);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point8, point6);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point8, point7);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point7, point3);
            }
            else player.SendConsoleCommand("ddraw.sphere", time, Color.blue, zone.definition.Location, zone.definition.Radius);
        }

        private Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation) => rotation * (point - pivot) + pivot;

        #endregion

        #region Component
        public class Zone : MonoBehaviour
        {
            internal Definition definition;

            internal ZoneFlags disabledFlags = ZoneFlags.None;

            internal Zone parent;

            internal List<BasePlayer> players = Pool.GetList<BasePlayer>();

            internal List<BaseEntity> entities = Pool.GetList<BaseEntity>();

            internal List<ulong> keepInList = Pool.GetList<ulong>();

            internal List<ulong> whitelist = Pool.GetList<ulong>();

            private Rigidbody rigidbody;

            internal Collider collider;

            internal Bounds colliderBounds;

            private ChildSphereTrigger<TriggerRadiation> radiation;

            private ChildSphereTrigger<TriggerComfort> comfort;

            private ChildSphereTrigger<TriggerTemperature> temperature;

            private TriggerSafeZone safeZone;

            private bool isTogglingLights = false;

            private void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "ZoneManager";
                enabled = false;
            }

            private void OnDestroy()
            {
                EmptyZone();

                Pool.FreeList(ref players);
                Pool.FreeList(ref entities);
                Pool.FreeList(ref keepInList);
                Pool.FreeList(ref whitelist);
            }

            private void EmptyZone()
            {
                RemoveAllPlayers();

                keepInList.Clear();

                for (int i = players.Count - 1; i >= 0; i--)
                    Instance?.OnPlayerExitZone(players[i], this);

                for (int i = entities.Count - 1; i >= 0; i--)
                    Instance?.OnEntityExitZone(entities[i], this);
            }

            #region Zone Initialization
            public void InitializeZone(Definition definition)
            {
                this.definition = definition;

                transform.position = definition.Location;

                transform.rotation = Quaternion.Euler(definition.Rotation);

                if (definition.Enabled)
                {
                    RegisterPermission();

                    InitializeCollider();

                    InitializeAutoLights();

                    InitializeRadiation();

                    InitializeSafeZone();

                    InitializeComfort();

                    InitializeTemperature();

                    RemoveAllPlayers();
                    AddAllPlayers();
                }
                else
                {
                    InvokeHandler.CancelInvoke(this, CheckAlwaysLights);
                    InvokeHandler.CancelInvoke(this, CheckLights);

                    if (isLightsOn)
                        ServerMgr.Instance.StartCoroutine(ToggleLights(false));

                    EmptyZone();

                    if (collider != null)
                        DestroyImmediate(collider);

                    if (rigidbody != null)
                        DestroyImmediate(rigidbody);
                }

                enabled = definition.Enabled;
            }

            public void FindZoneParent()
            {
                if (string.IsNullOrEmpty(definition.ParentID))
                    return;

                Instance.zones.TryGetValue(definition.ParentID, out parent);
            }

            public void Reset()
            {
                InvokeHandler.CancelInvoke(this, CheckAlwaysLights);
                InvokeHandler.CancelInvoke(this, CheckLights);

                if (isLightsOn)
                    ServerMgr.Instance.StartCoroutine(ToggleLights(false));

                EmptyZone();

                InitializeZone(definition);
            }

            private void RegisterPermission()
            {
                if (!string.IsNullOrEmpty(definition.Permission) && !Instance.permission.PermissionExists(definition.Permission))
                    Instance.permission.RegisterPermission(definition.Permission, Instance);
            }

            private void InitializeCollider()
            {
                if (collider != null)
                    DestroyImmediate(collider);

                if (rigidbody != null)
                    DestroyImmediate(rigidbody);

                rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                SphereCollider sphereCollider = gameObject.GetComponent<SphereCollider>();
                BoxCollider boxCollider = gameObject.GetComponent<BoxCollider>();

                if (definition.Size != Vector3.zero)
                {
                    if (sphereCollider != null)
                        Destroy(sphereCollider);

                    if (!boxCollider)
                    {
                        boxCollider = gameObject.AddComponent<BoxCollider>();
                        boxCollider.isTrigger = true;
                    }
                    boxCollider.size = definition.Size;
                    colliderBounds = boxCollider.bounds;
                    collider = boxCollider;
                }
                else
                {
                    if (boxCollider != null)
                        Destroy(boxCollider);

                    if (!sphereCollider)
                    {
                        sphereCollider = gameObject.AddComponent<SphereCollider>();
                        sphereCollider.isTrigger = true;
                    }
                    sphereCollider.radius = definition.Radius;
                    colliderBounds = sphereCollider.bounds;
                    collider = sphereCollider;
                }
            }
            #endregion

            #region Triggers
            private void InitializeRadiation()
            {                
                if (definition.Radiation > 0)
                {
                    if (radiation == null)
                        radiation = new ChildSphereTrigger<TriggerRadiation>(gameObject, "Radiation");

                    radiation.Trigger.RadiationAmountOverride = definition.Radiation;
                    radiation.Collider.radius = collider is SphereCollider ? definition.Radius : Mathf.Min(definition.Size.x, definition.Size.y, definition.Size.z) * 0.5f;
                    radiation.Trigger.enabled = this.enabled;
                }
                else radiation?.Destroy();
            }

            private void InitializeComfort()
            {
                if (definition.Comfort > 0)
                {
                    if (comfort == null)
                        comfort = new ChildSphereTrigger<TriggerComfort>(gameObject, "Comfort");

                    comfort.Trigger.baseComfort = definition.Comfort;
                    comfort.Trigger.triggerSize = comfort.Collider.radius = collider is SphereCollider ? definition.Radius : Mathf.Min(definition.Size.x, definition.Size.y, definition.Size.z) * 0.5f;
                    comfort.Trigger.enabled = this.enabled;
                }
                else comfort?.Destroy();
            }

            private void InitializeTemperature()
            {
                if (definition.Temperature != 0)
                {
                    if (temperature == null)
                        temperature = new ChildSphereTrigger<TriggerTemperature>(gameObject, "Temperature");

                    temperature.Trigger.Temperature = definition.Temperature;
                    temperature.Trigger.triggerSize = temperature.Collider.radius = collider is SphereCollider ? definition.Radius : Mathf.Min(definition.Size.x, definition.Size.y, definition.Size.z) * 0.5f;
                    temperature.Trigger.enabled = this.enabled;
                }
                else temperature?.Destroy();
            }

            private void InitializeSafeZone()
            {
                if (definition.SafeZone)
                {
                    if (safeZone == null)
                        safeZone = gameObject.AddComponent<TriggerSafeZone>();

                    safeZone.interestLayers = PLAYER_MASK;
                    safeZone.enabled = this.enabled;
                }
                else
                {
                    if (safeZone != null)
                        Destroy(safeZone);
                }
            }
                        
            private void AddToTrigger(TriggerBase triggerBase, BasePlayer player)
            {
                if (!triggerBase || !player)
                    return;

                if (triggerBase.entityContents == null)
                    triggerBase.entityContents = new HashSet<BaseEntity>();

                if (!triggerBase.entityContents.Contains(player))
                {
                    triggerBase.entityContents.Add(player);
                    player.EnterTrigger(triggerBase);

                    if (triggerBase is TriggerSafeZone)
                    {
                        if (player.IsItemHoldRestricted(player.inventory.containerBelt.FindItemByUID(player.svActiveItemID)))
                            player.UpdateActiveItem(0);

                        player.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, true);
                    }
                }
            }

            private void RemoveFromTrigger(TriggerBase triggerBase, BasePlayer player)
            {
                if (!triggerBase || !player)
                    return;

                if (triggerBase.entityContents != null && triggerBase.entityContents.Contains(player))
                {
                    triggerBase.entityContents.Remove(player);
                    player.LeaveTrigger(triggerBase);

                    if (triggerBase is TriggerSafeZone)
                    {
                        if (!player.InSafeZone())
                            player.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, false);
                    }
                }
            }

            private void AddAllPlayers()
            {
                for (int i = 0; i < players.Count; i++)
                {
                    BasePlayer player = players[i];

                    AddToTrigger(safeZone, player);

                    if (radiation != null)
                        AddToTrigger(radiation.Trigger, player);

                    if (comfort != null)
                        AddToTrigger(comfort.Trigger, player);

                    if (temperature != null)
                        AddToTrigger(temperature.Trigger, player);
                }
            }

            private void RemoveAllPlayers()
            {
                for (int i = 0; i < players.Count; i++)                    
                {
                    BasePlayer player = players[i];

                    RemoveFromTrigger(safeZone, player);

                    if (radiation != null)
                        RemoveFromTrigger(radiation.Trigger, player);

                    if (comfort != null)
                        RemoveFromTrigger(comfort.Trigger, player);

                    if (temperature != null)
                        RemoveFromTrigger(temperature.Trigger, player);
                }
            }

            private class ChildSphereTrigger<T> where T : TriggerBase
            {
                public GameObject Object { get; private set; }

                public SphereCollider Collider { get; private set; }

                public T Trigger { get; private set; }

                public ChildSphereTrigger(GameObject parent, string name)
                {
                    Object = parent.CreateChild();
                    Object.name = name;
                    Object.layer = (int)Layer.TransparentFX;

                    Collider = Object.AddComponent<SphereCollider>();
                    Collider.isTrigger = true;

                    Trigger = Object.AddComponent<T>();
                    Trigger.interestLayers = 0;
                }

                public void Destroy() => UnityEngine.Object.Destroy(Object);
            }
            #endregion

            #region Autolights
            private bool isLightsOn = false;

            private void InitializeAutoLights()
            {
                if (HasFlag(ZoneFlags.AlwaysLights))
                {
                    isLightsOn = true;

                    InvokeHandler.CancelInvoke(this, CheckAlwaysLights);
                    InvokeHandler.InvokeRandomized(this, CheckAlwaysLights, 5f, 60f, 10f);
                }
                else if (HasFlag(ZoneFlags.AutoLights))
                {
                    InvokeHandler.CancelInvoke(this, CheckLights);
                    InvokeHandler.InvokeRandomized(this, CheckLights, 5f, 20f, 10f);
                }
            }

            private void CheckAlwaysLights()
            {
                ServerMgr.Instance.StartCoroutine(ToggleLights(true));
            }

            private void CheckLights()
            {
                float currentTime = TOD_Sky.Instance.Cycle.Hour;

                bool shouldBeActive = currentTime > Instance.configData.AutoLights.OnTime || currentTime < Instance.configData.AutoLights.OffTime;

                if (shouldBeActive != isLightsOn)
                {
                    isLightsOn = shouldBeActive;
                    ServerMgr.Instance.StartCoroutine(ToggleLights(isLightsOn));
                }
            }

            private IEnumerator ToggleLights(bool active)
            {
                while (isTogglingLights)
                    yield return null;

                isTogglingLights = true;

                bool requiresFuel = Instance.configData.AutoLights.RequiresFuel;

                for (int i = 0; i < entities.Count; i++)
                {
                    if (ToggleLight(entities[i], active, requiresFuel))
                        yield return CoroutineEx.waitForEndOfFrame;
                }

                isTogglingLights = false;
            }

            private bool ToggleLight(BaseEntity baseEntity, bool active, bool requiresFuel)
            {
                BaseOven baseOven = baseEntity as BaseOven;
                if (baseOven != null)
                {
                    if (active)
                    {
                        if (!baseOven.IsOn())
                        {
                            if ((requiresFuel && baseOven.FindBurnable() != null) || !requiresFuel)
                                baseOven.SetFlag(BaseEntity.Flags.On, true);
                        }
                    }
                    else
                    {
                        if (baseOven.IsOn())
                            baseOven.StopCooking();
                    }

                    return true;
                }

                SearchLight searchLight = baseEntity as SearchLight;
                if (searchLight != null)
                {
                    if (active)
                    {
                        if (!searchLight.IsOn())
                            searchLight.SetFlag(BaseEntity.Flags.On, true);
                    }
                    else
                    {
                        if (searchLight.IsOn())
                            searchLight.SetFlag(BaseEntity.Flags.On, false);
                    }

                    return true;
                }

                return false;
            }
            #endregion

            #region Entity Detection            
            private void OnTriggerEnter(Collider col)
            {
                if (!definition.Enabled || !col || !col.gameObject)
                    return;

                BaseEntity baseEntity = col.gameObject.ToBaseEntity();
                if (!baseEntity || !baseEntity.IsValid())
                    return;

                if (baseEntity is BasePlayer)
                {
                    Instance.OnPlayerEnterZone(baseEntity as BasePlayer, this);

                    if (parent != null)
                        Instance.UpdateZoneFlags(this);

                    return;
                }

                Instance.OnEntityEnterZone(baseEntity, this);
            }

            private void OnTriggerExit(Collider col)
            {
                if (!definition.Enabled || !col || !col.gameObject)
                    return;

                BaseEntity baseEntity = col.gameObject.ToBaseEntity();
                if (!baseEntity || !baseEntity.IsValid())
                    return;

                if (baseEntity is BasePlayer)
                {
                    Instance.OnPlayerExitZone(baseEntity as BasePlayer, this);

                    return;
                }

                Instance.OnEntityExitZone(baseEntity, this);
            }

            public void OnPlayerEnterZone(BasePlayer player)
            {
                if (!players.Contains(player))
                    players.Add(player);

                AddToTrigger(safeZone, player);

                if (radiation != null)
                    AddToTrigger(radiation.Trigger, player);

                if (comfort != null)
                    AddToTrigger(comfort.Trigger, player);

                if (temperature != null)
                    AddToTrigger(temperature.Trigger, player);
            }

            public void OnPlayerExitZone(BasePlayer player)
            {
                players.Remove(player);

                RemoveFromTrigger(safeZone, player);

                if (radiation != null)
                    RemoveFromTrigger(radiation.Trigger, player);

                if (comfort != null)
                    RemoveFromTrigger(comfort.Trigger, player);

                if (temperature != null)
                    RemoveFromTrigger(temperature.Trigger, player);
            }

            public void OnEntityEnterZone(BaseEntity baseEntity)
            {
                entities.Add(baseEntity);

                if (HasFlag(ZoneFlags.NoDecay))
                {
                    DecayEntity decayEntity = baseEntity.GetComponentInParent<DecayEntity>();
                    if (decayEntity != null)
                    {
                        decayEntity.decay = null;
                    }
                }

                if (HasFlag(ZoneFlags.NoStability))
                {
                    if (baseEntity is StabilityEntity)
                    {
                        (baseEntity as StabilityEntity).grounded = true;
                    }
                }

                if (HasFlag(ZoneFlags.NpcFreeze))
                {
                    if (baseEntity is BaseNpc)
                    {
                        baseEntity.CancelInvoke((baseEntity as BaseNpc).TickAi);
                    }
                }

                if (HasFlag(ZoneFlags.AlwaysLights) || (HasFlag(ZoneFlags.AutoLights) && isLightsOn))
                {
                    if (baseEntity is BaseOven || baseEntity is SearchLight)
                    {
                        ToggleLight(baseEntity, true, Instance.configData.AutoLights.RequiresFuel);
                    }
                }
            }

            public void OnEntityExitZone(BaseEntity baseEntity, bool resetDecay, bool isDead = false)
            {
                entities.Remove(baseEntity);

                if (isDead)
                    return;

                if (resetDecay)
                {
                    if (HasFlag(ZoneFlags.NoDecay))
                    {
                        DecayEntity decayEntity = baseEntity.GetComponentInParent<DecayEntity>();
                        if (decayEntity != null)
                        {
                            decayEntity.decay = PrefabAttribute.server.Find<Decay>(decayEntity.prefabID);
                        }
                    }
                }

                if (HasFlag(ZoneFlags.NpcFreeze))
                {
                    if (baseEntity is BaseNpc)
                    {
                        baseEntity.InvokeRandomized((baseEntity as BaseNpc).TickAi, 0.1f, 0.1f, 0.00500000035f);
                    }
                }

                if (HasFlag(ZoneFlags.AlwaysLights) || (HasFlag(ZoneFlags.AutoLights) && isLightsOn))
                {
                    if (baseEntity is BaseOven || baseEntity is SearchLight)
                    {
                        ToggleLight(baseEntity, false, false);
                    }
                }
            }
            #endregion

            #region Helpers
            public bool HasPermission(BasePlayer player) => string.IsNullOrEmpty(definition.Permission) ? true : Instance.permission.UserHasPermission(player.UserIDString, definition.Permission);

            public bool CanLeaveZone(BasePlayer player) => !keepInList.Contains(player.userID);

            public bool CanEnterZone(BasePlayer player) => HasPermission(player) || !CanLeaveZone(player) || whitelist.Contains(player.userID);

            private bool HasFlag(ZoneFlags flags) => (definition.Flags & ~disabledFlags & flags) == flags;
            #endregion

            #region Zone Definition
            public class Definition
            {
                public string Name { get; set; }
                public float Radius { get; set; }
                public float Radiation { get; set; }
                public float Comfort { get; set; }
                public float Temperature { get; set; }
                public bool SafeZone { get; set; }
                public Vector3 Location { get; set; }
                public Vector3 Size { get; set; }
                public Vector3 Rotation { get; set; }
                public string Id { get; set; }
                public string ParentID { get; set; }
                public string EnterMessage { get; set; }
                public string LeaveMessage { get; set; }
                public string Permission { get; set; }
                public string EjectSpawns { get; set; }
                public bool Enabled { get; set; } = true;
                public ZoneFlags Flags { get; set; }

                public Definition() { }

                public Definition(Vector3 position)
                {
                    Radius = 20f;
                    Location = position;
                }
            }
            #endregion
        }
        #endregion

        #region Entity Management
        private void OnPlayerEnterZone(BasePlayer player, Zone zone)
        {
            if (!player || IsNpc(player))
                return;

            if (!zone.CanEnterZone(player))
            {
                EjectPlayer(player, zone);
                return;
            }

            if (HasFlag(zone, ZoneFlags.Eject))
            {
                if (!CanBypass(player, ZoneFlags.Eject) && !IsAdmin(player))
                {
                    EjectPlayer(player, zone);
                    return;
                }
            }

            if (HasFlag(zone, ZoneFlags.KeepVehiclesOut) && player.isMounted)
            {
                if (ReverseVelocity(player))
                {
                    SendMessage(player, Message("novehiclesenter", player.UserIDString));
                    return;
                }
            }

            if (player.IsSleeping() && !player.IsConnected)
            {
                if (HasFlag(zone, ZoneFlags.KillSleepers))
                {
                    if (!CanBypass(player, ZoneFlags.KillSleepers) && !IsAdmin(player))
                    {
                        player.Die();
                        return;
                    }
                }

                if (HasFlag(zone, ZoneFlags.EjectSleepers))
                {
                    if (!CanBypass(player, ZoneFlags.EjectSleepers) && !IsAdmin(player))
                    {
                        EjectPlayer(player, zone);
                        return;
                    }
                }
            }

            if (HasFlag(zone, ZoneFlags.Kill))
            {
                if (!CanBypass(player, ZoneFlags.Kill) && !IsAdmin(player))
                {
                    player.Die();
                    return;
                }
            }

            EntityZones entityZones;
            if (!zonedPlayers.TryGetValue(player.userID, out entityZones))
                zonedPlayers[player.userID] = entityZones = new EntityZones();

            if (!entityZones.EnterZone(zone))
                return;

            if (zone.parent != null)
                entityZones.UpdateFlags();
            else entityZones.AddFlags(zone.definition.Flags);

            zone.OnPlayerEnterZone(player);

            if (!string.IsNullOrEmpty(zone.definition.EnterMessage))
            {
                if (PopupNotifications != null && configData.Notifications.Popups)
                    PopupNotifications.Call("CreatePopupNotification", string.Format(zone.definition.EnterMessage, player.displayName), player);
                else SendMessage(player, zone.definition.EnterMessage, player.displayName);
            }

            Interface.CallHook("OnEnterZone", zone.definition.Id, player);
        }

        private void OnPlayerExitZone(BasePlayer player, Zone zone)
        {
            if (!player || IsNpc(player))
                return;

            if (HasFlag(zone, ZoneFlags.KeepVehiclesIn) && player.isMounted)
            {
                if (ReverseVelocity(player))
                {
                    SendMessage(player, Message("novehiclesleave", player.UserIDString));
                    return;
                }
            }

            if (!zone.CanLeaveZone(player))
            {
                AttractPlayer(player, zone);
                return;
            }

            EntityZones entityZones;
            if (!zonedPlayers.TryGetValue(player.userID, out entityZones))
                return;

            entityZones.LeaveZone(zone);

            if (entityZones.ShouldRemove())
                zonedPlayers.Remove(player.userID);
            else entityZones.UpdateFlags();

            zone.OnPlayerExitZone(player);

            if (!string.IsNullOrEmpty(zone.definition.LeaveMessage))
            {
                if (PopupNotifications != null && configData.Notifications.Popups)
                    PopupNotifications.Call("CreatePopupNotification", string.Format(zone.definition.LeaveMessage, player.displayName), player);
                else SendMessage(player, zone.definition.LeaveMessage, player.displayName);
            }

            Interface.CallHook("OnExitZone", zone.definition.Id, player);
        }

        private void OnEntityEnterZone(BaseEntity baseEntity, Zone zone)
        {
            if (!baseEntity.IsValid())
                return;

            EntityZones entityZones;
            if (!zonedEntities.TryGetValue(baseEntity.net.ID, out entityZones))
                zonedEntities[baseEntity.net.ID] = entityZones = new EntityZones();

            if (!entityZones.EnterZone(zone))
                return;

            if (zone.parent != null)
                entityZones.UpdateFlags();
            else entityZones.AddFlags(zone.definition.Flags);

            zone.OnEntityEnterZone(baseEntity);

            Interface.CallHook("OnEntityEnterZone", zone.definition.Id, baseEntity);
        }

        private void OnEntityExitZone(BaseEntity baseEntity, Zone zone)
        {
            if (!baseEntity.IsValid())
                return;

            EntityZones entityZones;
            if (!zonedEntities.TryGetValue(baseEntity.net.ID, out entityZones))
                return;

            entityZones.LeaveZone(zone);

            if (entityZones.ShouldRemove())
                zonedEntities.Remove(baseEntity.net.ID);
            else entityZones.UpdateFlags();

            zone.OnEntityExitZone(baseEntity, !entityZones.HasFlag(ZoneFlags.NoDecay));

            Interface.CallHook("OnEntityExitZone", zone.definition.Id, baseEntity);
        }
        #endregion

        #region Helpers
        private bool IsAdmin(BasePlayer player) => player?.net?.connection?.authLevel > 0;

        private bool IsNpc(BasePlayer player) => player.IsNpc || player is NPCPlayer;

        private bool HasPermission(BasePlayer player, string permname) => IsAdmin(player) || permission.UserHasPermission(player.UserIDString, permname);

        private bool HasPermission(ConsoleSystem.Arg arg, string permname) => (arg.Connection.player as BasePlayer) == null ? true : permission.UserHasPermission((arg.Connection.player as BasePlayer).UserIDString, permname);

        private bool CanBypass(object player, ZoneFlags flag) => permission.UserHasPermission(player is BasePlayer ? (player as BasePlayer).UserIDString : player.ToString(), PERMISSION_IGNORE_FLAG + flag);

        private void SendMessage(BasePlayer player, string message, params object[] args)
        {
            if (player != null)
            {
                if (args.Length > 0)
                    message = string.Format(message, args);
                SendReply(player, $"<color={configData.Notifications.Color}>{configData.Notifications.Prefix}</color> {message}");
            }
            else Puts(message);
        }

        private Zone GetZoneByID(string zoneId) => zones.ContainsKey(zoneId) ? zones[zoneId] : null;

        private void AddToKeepinlist(Zone zone, BasePlayer player)
        {
            zone.keepInList.Add(player.userID);

            EntityZones entityZones;
            if (!zonedPlayers.TryGetValue(player.userID, out entityZones) || !entityZones.Zones.Contains(zone))
                AttractPlayer(player, zone);
        }

        private void RemoveFromKeepinlist(Zone zone, BasePlayer player) => zone.keepInList.Remove(player.userID);

        private void AddToWhitelist(Zone zone, BasePlayer player)
        {
            if (!zone.whitelist.Contains(player.userID))
                zone.whitelist.Add(player.userID);
        }

        private void RemoveFromWhitelist(Zone zone, BasePlayer player) => zone.whitelist.Remove(player.userID);

        private bool HasPlayerFlag(BasePlayer player, ZoneFlags flag, bool canBypass)
        {
            if (!player)
                return false;

            if (canBypass && IsAdmin(player))
                return false;

            if (CanBypass(player.userID, flag))
                return false;

            EntityZones entityZones;
            if (!zonedPlayers.TryGetValue(player.userID, out entityZones))
                return false;

            return entityZones.HasFlag(flag);
        }

        private bool HasEntityFlag(BaseEntity baseEntity, ZoneFlags flag)
        {
            if (!baseEntity.IsValid())
                return false;

            EntityZones entityZones;
            if (!zonedEntities.TryGetValue(baseEntity.net.ID, out entityZones))
                return false;

            return entityZones.HasFlag(flag);
        }
        #endregion

        #region API 

        #region Zone Management       

        private void SetZoneStatus(string zoneId, bool active)
        {
            Zone zone = GetZoneByID(zoneId);
            if (zone != null)
            {
                zone.definition.Enabled = active;
                zone.InitializeZone(zone.definition);
            }
        }

        private Vector3 GetZoneLocation(string zoneId) => GetZoneByID(zoneId)?.definition.Location ?? Vector3.zero;

        private object GetZoneRadius(string zoneID) => GetZoneByID(zoneID)?.definition.Radius;

        private object GetZoneSize(string zoneID) => GetZoneByID(zoneID)?.definition.Size;

        private object GetZoneName(string zoneID) => GetZoneByID(zoneID)?.definition.Name;

        private object CheckZoneID(string zoneID) => GetZoneByID(zoneID)?.definition.Id;

        private object GetZoneIDs() => zones.Keys.ToArray();

        private bool IsPositionInZone(string zoneID, Vector3 position)
        {
            Zone zone = GetZoneByID(zoneID);
            if (!zone)
                return false;

            if (zone.definition.Size != Vector3.zero)
                return IsInsideBounds(zone, position); 
            else return Vector3.Distance(position, zone.transform.position) <= zone.definition.Radius;            
        }

        private List<BasePlayer> GetPlayersInZone(string zoneID)
        {
            Zone zone = GetZoneByID(zoneID);
            if (!zone)
                return new List<BasePlayer>();

            return new List<BasePlayer>(zone.players);
        }

        private List<BaseEntity> GetEntitiesInZone(string zoneId)
        {
            Zone zone = GetZoneByID(zoneId);
            if (!zone)
                return new List<BaseEntity>();

            return new List<BaseEntity>(zone.entities);
        }

        private bool isPlayerInZone(string zoneID, BasePlayer player) => IsPlayerInZone(zoneID, player);

        private bool IsPlayerInZone(string zoneID, BasePlayer player)
        {
            Zone zone = GetZoneByID(zoneID);
            if (!zone)
                return false;

            return zone.players.Contains(player);
        }

        private bool IsEntityInZone(string zoneID, BaseEntity entity)
        {
            Zone zone = GetZoneByID(zoneID);
            if (!zone)
                return false;

            return zone.entities.Contains(entity);
        }

        private string[] GetPlayerZoneIDs(BasePlayer player)
        {
            EntityZones entityZones;
            if (!zonedPlayers.TryGetValue(player.userID, out entityZones))
                return new string[0];

            return entityZones.Zones.Select(x => x.definition.Id).ToArray();
        }

        private string[] GetEntityZoneIDs(BaseEntity entity)
        {
            EntityZones entityZones;
            if (!zonedEntities.TryGetValue(entity.net.ID, out entityZones))
                return new string[0];

            return entityZones.Zones.Select(x => x.definition.Id).ToArray();
        }

        private bool HasFlag(string zoneId, string flagStr)
        {
            try
            {
                ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagStr, true);

                Zone zone = GetZoneByID(zoneId);

                return zone != null ? HasFlag(zone, flag) : false;
            }
            catch
            {
                PrintError(string.Format("A plugin called HasFlag with an invalid flag {0}", flagStr));
                return false;
            }
        }

        private void AddFlag(string zoneId, string flagStr)
        {
            try
            {
                ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagStr, true);

                Zone zone = GetZoneByID(zoneId);
                if (zone != null)
                    AddFlag(zone, flag);
            }
            catch
            {
                PrintError(string.Format("A plugin called AddFlag with an invalid flag {0}", flagStr));
            }
        }

        private void RemoveFlag(string zoneId, string flagStr)
        {
            try
            {
                ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagStr, true);

                Zone zone = GetZoneByID(zoneId);
                if (zone != null)
                    RemoveFlag(zone, flag);
            }
            catch
            {
                PrintError(string.Format("A plugin called RemoveFlag with an invalid flag {0}", flagStr));
            }
        }

        private bool HasDisabledFlag(string zoneId, string flagStr)
        {
            try
            {
                ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagStr, true);

                Zone zone = GetZoneByID(zoneId);

                return zone != null ? HasDisabledFlag(zone, flag) : false;
            }
            catch
            {
                PrintError(string.Format("A plugin called HasDisabledFlag with an invalid flag {0}", flagStr));
                return false;
            }
        }

        private void AddDisabledFlag(string zoneId, string flagStr)
        {
            try
            {
                ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagStr, true);

                Zone zone = GetZoneByID(zoneId);
                if (zone != null)
                    AddDisabledFlag(zone, flag);
            }
            catch
            {
                PrintError(string.Format("A plugin called AddDisabledFlag with an invalid flag {0}", flagStr));
            }
        }

        private void RemoveDisabledFlag(string zoneId, string flagStr)
        {
            try
            {
                ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagStr, true);

                Zone zone = GetZoneByID(zoneId);
                if (zone != null)
                    RemoveDisabledFlag(zone, flag);
            }
            catch
            {
                PrintError(string.Format("A plugin called RemoveDisabledFlag with an invalid flag {0}", flagStr));
            }
        }

        private bool CreateOrUpdateZone(string zoneId, string[] args, Vector3 position = default(Vector3))
        {
            Zone.Definition definition;

            Zone zone;
            if (!zones.TryGetValue(zoneId, out zone))
            {
                zone = new GameObject().AddComponent<Zone>();
                definition = new Zone.Definition { Id = zoneId, Radius = 20 };

                zones[zoneId] = zone;
                zone.InitializeZone(definition);
            }
            else definition = zone.definition;

            UpdateZoneDefinition(zone, args);

            if (position != default(Vector3))
                definition.Location = position;

            zone.definition = definition;
            zone.Reset();
            zone.FindZoneParent();
            SaveData();
            return true;
        }

        private bool EraseZone(string zoneId)
        {
            Zone zone;
            if (!zones.TryGetValue(zoneId, out zone))
                return false;

            zones.Remove(zoneId);

            UnityEngine.Object.Destroy(zone.gameObject);

            SaveData();
            return true;
        }


        private List<string> ZoneFieldListRaw()
        {
            List<string> list = new List<string> { "name", "ID", "radius", "rotation", "size", "Location", "enter_message", "leave_message", "radiation", "comfort", "temperature" };
            list.AddRange(Enum.GetNames(typeof(ZoneFlags)));
            return list;
        }

        private Dictionary<string, string> ZoneFieldList(string zoneId)
        {
            Zone zone = GetZoneByID(zoneId);
            if (!zone)
                return null;

            Dictionary<string, string> fields = new Dictionary<string, string>
            {
                { "name", zone.definition.Name },
                { "ID", zone.definition.Id },
                { "comfort", zone.definition.Comfort.ToString() },
                { "temperature", zone.definition.Temperature.ToString() },
                { "radiation", zone.definition.Radiation.ToString() },
                { "safezone", zone.definition.SafeZone.ToString() },
                { "radius", zone.definition.Radius.ToString() },
                { "rotation", zone.definition.Rotation.ToString() },
                { "size", zone.definition.Size.ToString() },
                { "Location", zone.definition.Location.ToString() },
                { "enter_message", zone.definition.EnterMessage },
                { "leave_message", zone.definition.LeaveMessage },
                { "permission", zone.definition.Permission },
                { "ejectspawns", zone.definition.EjectSpawns }
            };

            foreach (object value in Enum.GetValues(typeof(ZoneFlags)))
                fields[value.ToString()] = HasFlag(zone, (ZoneFlags)value).ToString();

            return fields;
        }
        #endregion

        #region Entity Management        
        private bool AddPlayerToZoneKeepinlist(string zoneId, BasePlayer player)
        {
            Zone zone = GetZoneByID(zoneId);
            if (!zone)
                return false;

            AddToKeepinlist(zone, player);
            return true;
        }

        private bool RemovePlayerFromZoneKeepinlist(string zoneId, BasePlayer player)
        {
            Zone zone = GetZoneByID(zoneId);
            if (!zone)
                return false;

            RemoveFromKeepinlist(zone, player);
            return true;
        }

        private bool AddPlayerToZoneWhitelist(string zoneId, BasePlayer player)
        {
            Zone zone = GetZoneByID(zoneId);
            if (!zone)
                return false;

            AddToWhitelist(zone, player);
            return true;
        }

        private bool RemovePlayerFromZoneWhitelist(string zoneId, BasePlayer player)
        {
            Zone zone = GetZoneByID(zoneId);
            if (!zone)
                return false;

            RemoveFromWhitelist(zone, player);
            return true;
        }

        private bool EntityHasFlag(BaseEntity baseEntity, string flagStr)
        {
            if (!baseEntity.IsValid())
                return false;

            try
            {
                ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagStr, true);
                return HasEntityFlag(baseEntity, flag);
            }
            catch
            {
                PrintError(string.Format("A plugin called EntityHasFlag with an invalid flag {0}", flagStr));
                return false;
            }
        }

        private bool PlayerHasFlag(BasePlayer player, string flagStr)
        {
            if (!player)
                return false;

            try
            {
                ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagStr, true);
                return HasPlayerFlag(player, flag, false);
            }
            catch
            {
                PrintError(string.Format("A plugin called HasPlayerFlag with an invalid flag {0}", flagStr));
                return false;
            }
        }
        #endregion

        #region Plugin Integration
        private object CanRedeemKit(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoKits, false) ? "You may not redeem a kit inside this area" : null;

        private object CanTeleport(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoTp, false) ? "You may not teleport in this area" : null;

        private object canRemove(BasePlayer player) => CanRemove(player);

        private object CanRemove(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoRemove, false) ? "You may not use the remover tool in this area" : null;

        private bool CanChat(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoChat, false) ? false : true;

        private object CanTrade(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoTrade, false) ? "You may not trade in this area" : null;

        private object canShop(BasePlayer player) => CanShop(player);

        private object CanShop(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoShop, false) ? "You may not use the store in this area" : null;
        #endregion

        #endregion

        #region Flags
        [Flags]
        public enum ZoneFlags : ulong
        {
            None = 0UL,
            AutoLights = 1UL,
            Eject = 1UL << 1,
            PvpGod = 1UL << 2,
            PveGod = 1UL << 3,
            SleepGod = 1UL << 4,
            UnDestr = 1UL << 5,
            NoBuild = 1UL << 6,
            NoTp = 1UL << 7,
            NoChat = 1UL << 8,
            NoGather = 1UL << 9,
            NoPve = 1UL << 10,
            NoWounded = 1UL << 11,
            NoDecay = 1UL << 12,
            NoDeploy = 1UL << 13,
            NoKits = 1UL << 14,
            NoBoxLoot = 1UL << 15,
            NoPlayerLoot = 1UL << 16,
            NoCorpse = 1UL << 17,
            NoSuicide = 1UL << 18,
            NoRemove = 1UL << 19,
            NoBleed = 1UL << 20,
            KillSleepers = 1UL << 21,
            NpcFreeze = 1UL << 22,
            NoDrown = 1UL << 23,
            NoStability = 1UL << 24,
            NoUpgrade = 1UL << 25,
            EjectSleepers = 1UL << 26,
            NoPickup = 1UL << 27,
            NoCollect = 1UL << 28,
            NoDrop = 1UL << 29,
            Kill = 1UL << 30,
            NoCup = 1UL << 31,
            AlwaysLights = 1UL << 32,
            NoTrade = 1UL << 33,
            NoShop = 1UL << 34,
            NoSignUpdates = 1UL << 35,
            NoOvenToggle = 1UL << 36,
            NoLootSpawns = 1UL << 37,
            NoNPCSpawns = 1UL << 38,
            NoVending = 1UL << 39,
            NoStash = 1UL << 40,
            NoCraft = 1UL << 41,
            NoHeliTargeting = 1UL << 42,
            NoTurretTargeting = 1UL << 43,
            NoAPCTargeting = 1UL << 44,
            NoNPCTargeting = 1UL << 45,
            NoEntityPickup = 1UL << 46,
            NoFallDamage = 1UL << 47,
            InfiniteTrapAmmo = 1UL << 48,
            LootSelf = 1UL << 49,
            NoDoorAccess = 1UL << 50,
            NoVoice = 1UL << 51,
            KeepVehiclesIn = 1UL << 52,
            KeepVehiclesOut = 1UL << 53,
            Custom1 = 1UL << 61,
            Custom2 = 1UL << 62,
            Custom3 = 1UL << 63,
        }

        private void AddFlag(Zone zone, ZoneFlags flag)
        {
            zone.definition.Flags |= flag;

            if (NeedsUpdateSubscriptions())
                UpdateHookSubscriptions();

            zone.Reset();
        }

        private void RemoveFlag(Zone zone, ZoneFlags flag)
        {
            zone.definition.Flags &= ~flag;

            if (NeedsUpdateSubscriptions())
            {
                UnsubscribeAll();
                UpdateHookSubscriptions();
            }

            zone.Reset();
        }

        private bool HasFlag(Zone zone, ZoneFlags flag) => (zone.definition.Flags & ~zone.disabledFlags & flag) == flag;

        private void AddDisabledFlag(Zone zone, ZoneFlags flag)
        {
            zone.disabledFlags |= flag;

            UpdateZoneFlags(zone);
        }

        private void RemoveDisabledFlag(Zone zone, ZoneFlags flag)
        {
            zone.disabledFlags &= ~flag;

            UpdateZoneFlags(zone);
        }

        private bool HasDisabledFlag(Zone zone, ZoneFlags flag) => (zone.disabledFlags & flag) == flag;

        private void UpdateZoneFlags(Zone zone)
        {
            for (int i = 0; i < zonedPlayers.Count; i++)
            {
                EntityZones entityZones = zonedPlayers.ElementAt(i).Value;

                if (entityZones.Zones.Contains(zone))
                {
                    entityZones.UpdateFlags();
                }
            }

            for (int i = 0; i < zonedEntities.Count; i++)
            {
                EntityZones entityZones = zonedEntities.ElementAt(i).Value;

                if (entityZones.Zones.Contains(zone))
                {
                    entityZones.UpdateFlags();
                }
            }
        }
        #endregion

        #region Hook Subscriptions
        private bool HasGlobalFlag(ZoneFlags flags) => (globalFlags & flags) != 0;

        private void UpdateGlobalFlags()
        {
            globalFlags = ZoneFlags.None;

            for (int i = 0; i < zones.Count; i++)
            {
                Zone zone = zones.ElementAt(i).Value;
                if (!zone)
                    continue;

                globalFlags |= zone.definition.Flags;
            }
        }

        private bool NeedsUpdateSubscriptions()
        {
            ZoneFlags tempFlags = ZoneFlags.None;

            for (int i = 0; i < zones.Count; i++)
            {
                Zone zone = zones.ElementAt(i).Value;
                if (!zone)
                    continue;

                tempFlags |= zone.definition.Flags;
            }

            return tempFlags != globalFlags;
        }

        private void UpdateHookSubscriptions()
        {
            UpdateGlobalFlags();

            if (HasGlobalFlag(ZoneFlags.NoBuild) || HasGlobalFlag(ZoneFlags.NoCup) || HasGlobalFlag(ZoneFlags.NoDeploy))
                Subscribe(nameof(OnEntityBuilt));

            if (HasGlobalFlag(ZoneFlags.NoUpgrade))
                Subscribe(nameof(OnStructureUpgrade));

            if (HasGlobalFlag(ZoneFlags.NoDeploy))
                Subscribe(nameof(OnItemDeployed));

            if (HasGlobalFlag(ZoneFlags.InfiniteTrapAmmo) || HasGlobalFlag(ZoneFlags.AlwaysLights) || HasGlobalFlag(ZoneFlags.AutoLights))
                Subscribe(nameof(OnItemUse));

            if (HasGlobalFlag(ZoneFlags.NoBleed) || HasGlobalFlag(ZoneFlags.NoDrown))
                Subscribe(nameof(OnRunPlayerMetabolism));

            if (HasGlobalFlag(ZoneFlags.NoChat))
                Subscribe(nameof(OnPlayerChat));

            if (HasGlobalFlag(ZoneFlags.NoSuicide))
                Subscribe(nameof(OnServerCommand));

            if (HasGlobalFlag(ZoneFlags.KillSleepers) || HasGlobalFlag(ZoneFlags.EjectSleepers))
                Subscribe(nameof(OnPlayerDisconnected));

            if (HasGlobalFlag(ZoneFlags.NoFallDamage) || HasGlobalFlag(ZoneFlags.SleepGod) || HasGlobalFlag(ZoneFlags.PvpGod) || HasGlobalFlag(ZoneFlags.PveGod) || HasGlobalFlag(ZoneFlags.NoPve) || HasGlobalFlag(ZoneFlags.UnDestr))
                Subscribe(nameof(OnEntityTakeDamage));

            if (HasGlobalFlag(ZoneFlags.NoWounded))
                Subscribe(nameof(CanBeWounded));

            if (HasGlobalFlag(ZoneFlags.NoSignUpdates))
                Subscribe(nameof(CanUpdateSign));

            if (HasGlobalFlag(ZoneFlags.NoOvenToggle))
                Subscribe(nameof(OnOvenToggle));

            if (HasGlobalFlag(ZoneFlags.NoVending))
                Subscribe(nameof(CanUseVending));

            if (HasGlobalFlag(ZoneFlags.NoStash))
                Subscribe(nameof(CanHideStash));

            if (HasGlobalFlag(ZoneFlags.NoCraft))
                Subscribe(nameof(CanCraft));

            if (HasGlobalFlag(ZoneFlags.NoDoorAccess))
                Subscribe(nameof(OnDoorOpened));

            if (HasGlobalFlag(ZoneFlags.NoVoice))
                Subscribe(nameof(OnPlayerVoice));

            if (HasGlobalFlag(ZoneFlags.NoPlayerLoot))
            {
                Subscribe(nameof(CanLootPlayer));
                Subscribe(nameof(OnLootPlayer));
            }

            if (HasGlobalFlag(ZoneFlags.LootSelf) || HasGlobalFlag(ZoneFlags.NoPlayerLoot))
                Subscribe(nameof(OnLootEntity));

            if (HasGlobalFlag(ZoneFlags.LootSelf) || HasGlobalFlag(ZoneFlags.NoPlayerLoot) || HasGlobalFlag(ZoneFlags.NoBoxLoot) || HasGlobalFlag(ZoneFlags.NoGather))
                Subscribe(nameof(CanLootEntity));

            if (HasGlobalFlag(ZoneFlags.NoEntityPickup))
            {
                Subscribe(nameof(CanPickupEntity));
                Subscribe(nameof(CanPickupLock));
                Subscribe(nameof(OnItemPickup));
            }

            if (HasGlobalFlag(ZoneFlags.NoGather))
            {
                Subscribe(nameof(OnCollectiblePickup));
                Subscribe(nameof(OnGrowableGather));
                Subscribe(nameof(OnDispenserGather));
            }

            if (HasGlobalFlag(ZoneFlags.NoTurretTargeting))
                Subscribe(nameof(OnTurretTarget));

            if (HasGlobalFlag(ZoneFlags.NoAPCTargeting))
                Subscribe(nameof(CanBradleyApcTarget));

            if (HasGlobalFlag(ZoneFlags.NoHeliTargeting))
            {
                Subscribe(nameof(CanHelicopterTarget));
                Subscribe(nameof(CanHelicopterStrafeTarget));
                Subscribe(nameof(OnHelicopterTarget));
            }

            if (HasGlobalFlag(ZoneFlags.NoNPCTargeting))
                Subscribe(nameof(OnNpcTarget));
        }

        private void UnsubscribeAll()
        {
            Unsubscribe(nameof(OnEntityBuilt));
            Unsubscribe(nameof(OnStructureUpgrade));
            Unsubscribe(nameof(OnItemDeployed));
            Unsubscribe(nameof(OnItemUse));
            Unsubscribe(nameof(OnRunPlayerMetabolism));
            Unsubscribe(nameof(OnPlayerChat));
            Unsubscribe(nameof(OnServerCommand));
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(CanBeWounded));
            Unsubscribe(nameof(CanUpdateSign));
            Unsubscribe(nameof(OnOvenToggle));
            Unsubscribe(nameof(CanUseVending));
            Unsubscribe(nameof(CanHideStash));
            Unsubscribe(nameof(CanCraft));
            Unsubscribe(nameof(OnDoorOpened));
            Unsubscribe(nameof(CanLootPlayer));
            Unsubscribe(nameof(OnLootPlayer));
            Unsubscribe(nameof(CanLootEntity));
            Unsubscribe(nameof(CanLootEntity));
            Unsubscribe(nameof(CanPickupEntity));
            Unsubscribe(nameof(CanPickupLock));
            Unsubscribe(nameof(OnItemPickup));
            Unsubscribe(nameof(CanLootEntity));
            Unsubscribe(nameof(OnCollectiblePickup));
            Unsubscribe(nameof(OnGrowableGather));
            Unsubscribe(nameof(OnDispenserGather));
            Unsubscribe(nameof(OnTurretTarget));
            Unsubscribe(nameof(CanBradleyApcTarget));
            Unsubscribe(nameof(CanHelicopterTarget));
            Unsubscribe(nameof(CanHelicopterStrafeTarget));
            Unsubscribe(nameof(OnHelicopterTarget));
            Unsubscribe(nameof(OnNpcTarget));
            Unsubscribe(nameof(OnPlayerVoice));
        }
        #endregion

        #region Zone Creation
        private void UpdateZoneDefinition(Zone zone, string[] args, BasePlayer player = null)
        {
            for (var i = 0; i < args.Length; i = i + 2)
            {
                object editvalue;
                switch (args[i].ToLower())
                {
                    case "name":
                        editvalue = zone.definition.Name = args[i + 1];
                        break;

                    case "id":
                        editvalue = zone.definition.Id = args[i + 1];
                        break;

                    case "comfort":
                        editvalue = zone.definition.Comfort = Convert.ToSingle(args[i + 1]);
                        break;

                    case "temperature":
                        editvalue = zone.definition.Temperature = Convert.ToSingle(args[i + 1]);
                        break;

                    case "radiation":
                        editvalue = zone.definition.Radiation = Convert.ToSingle(args[i + 1]);
                        break;

                    case "safezone":
                        editvalue = zone.definition.SafeZone = Convert.ToBoolean(args[i + 1]);
                        break;

                    case "radius":
                        editvalue = zone.definition.Radius = Convert.ToSingle(args[i + 1]);
                        zone.definition.Size = Vector3.zero;
                        break;

                    case "rotation":
                        float rotation;
                        if (float.TryParse(args[i + 1], out rotation))
                            zone.definition.Rotation = Quaternion.AngleAxis(rotation, Vector3.up).eulerAngles;
                        else zone.definition.Rotation = new Vector3(0, player?.GetNetworkRotation().eulerAngles.y ?? 0, 0);

                        editvalue = zone.definition.Rotation;
                        break;

                    case "location":
                        if (player != null && args[i + 1].Equals("here", StringComparison.OrdinalIgnoreCase))
                        {
                            editvalue = zone.definition.Location = player.transform.position;
                            break;
                        }

                        string[] location = args[i + 1].Trim().Split(' ');
                        if (location.Length == 3)
                            editvalue = zone.definition.Location = new Vector3(Convert.ToSingle(location[0]), Convert.ToSingle(location[1]), Convert.ToSingle(location[2]));
                        else
                        {
                            if (player != null)
                                SendMessage(player, "Invalid location format. Correct syntax is \"/zone location \"x y z\"\" - or - \"/zone location here\"");
                            continue;
                        }
                        break;

                    case "size":
                        string[] size = args[i + 1].Trim().Split(' ');
                        if (size.Length == 3)
                            editvalue = zone.definition.Size = new Vector3(Convert.ToSingle(size[0]), Convert.ToSingle(size[1]), Convert.ToSingle(size[2]));
                        else
                        {
                            if (player != null)
                                SendMessage(player, "Invalid size format, Correct syntax is \"/zone size \"x y z\"\"");
                            continue;
                        }
                        break;

                    case "enter_message":
                        editvalue = zone.definition.EnterMessage = args[i + 1];
                        break;

                    case "leave_message":
                        editvalue = zone.definition.LeaveMessage = args[i + 1];
                        break;

                    case "parentid":
                        editvalue = args[i + 1];
                        Zone parent;
                        if (zones.TryGetValue((string)editvalue, out parent))
                        {
                            zone.definition.ParentID = (string)editvalue;
                            zone.FindZoneParent();
                            UpdateZoneFlags(zone);
                        }
                        else
                        {
                            if (player != null)
                                SendMessage(player, $"Unable to find zone with ID {editvalue}");
                            continue;
                        }
                        break;

                    case "permission":
                        string permission = args[i + 1];

                        if (!permission.StartsWith("zonemanager."))
                            permission = $"zonemanager.{permission}";

                        editvalue = zone.definition.Permission = permission;
                        break;

                    case "ejectspawns":
                        editvalue = zone.definition.EjectSpawns = args[i + 1];
                        break;

                    case "enabled":
                    case "enable":
                        bool enabled;
                        if (!bool.TryParse(args[i + 1], out enabled))
                            enabled = false;

                        editvalue = zone.definition.Enabled = enabled;
                        break;

                    default:
                        try
                        {
                            ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), args[i], true);

                            bool active;
                            if (!bool.TryParse(args[i + 1], out active))
                                active = false;

                            editvalue = active;

                            if (active)
                                AddFlag(zone, flag);
                            else RemoveFlag(zone, flag);
                        }
                        catch
                        {
                            if (player != null)
                                SendMessage(player, $"Invalid zone flag: {args[i]}");
                            continue;
                        }
                        break;
                }
                if (player != null)
                    SendMessage(player, $"{args[i]} set to {editvalue}");
            }
        }
        #endregion

        #region Commands
        [ChatCommand("zone_add")]
        private void cmdChatZoneAdd(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ZONE))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            Zone.Definition definition = new Zone.Definition(player.transform.position) { Id = UnityEngine.Random.Range(1, 99999999).ToString() };

            CreateZone(definition);

            lastPlayerZone[player.userID] = definition.Id;

            SaveData();

            ShowZone(player, definition.Id);

            SendMessage(player, "You have successfully created a new zone with ID : {0}!\nYou can edit it using the /zone_edit command", definition.Id);
        }

        [ChatCommand("zone_wipe")]
        private void cmdChatZoneReset(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ZONE))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            storedData.definitions.Clear();
            SaveData();
            Unload();
            SendMessage(player, "Wiped zone data");
        }

        [ChatCommand("zone_remove")]
        private void cmdChatZoneRemove(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ZONE))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            if (args.Length == 0)
            {
                SendMessage(player, "Invalid syntax! /zone_remove <zone ID>");
                return;
            }

            Zone zone;
            if (!zones.TryGetValue(args[0], out zone))
            {
                SendMessage(player, "A zone with the specified ID does not exist");
                return;
            }

            zones.Remove(args[0]);
            UnityEngine.Object.Destroy(zone.gameObject);
            SaveData();

            SendMessage(player, "Successfully removed zone : {0}", args[0]);
        }

        [ChatCommand("zone_stats")]
        private void cmdChatZoneStats(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ZONE))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            SendMessage(player, "Zones : {0}", zones.Count);
            SendMessage(player, "Players in Zones: {0}", zonedPlayers.Count);
            SendMessage(player, "Entities in Zones: {0}", zonedEntities.Count);
        }

        [ChatCommand("zone_edit")]
        private void cmdChatZoneEdit(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ZONE))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            string zoneId;
            if (args.Length == 0)
            {
                EntityZones entityZones;
                if (!zonedPlayers.TryGetValue(player.userID, out entityZones) || entityZones.Count != 1)
                {
                    SendMessage(player, "You must enter a zone ID. /zone_edit <zone ID>");
                    return;
                }
                zoneId = entityZones.Zones.First().definition.Id;
            }
            else zoneId = args[0];

            if (!zones.ContainsKey(zoneId))
            {
                SendMessage(player, "The specified zone does not exist");
                return;
            }

            lastPlayerZone[player.userID] = zoneId;

            SendMessage(player, "You are now editing the zone with ID : {0}", zoneId);
            ShowZone(player, zoneId);
        }

        [ChatCommand("zone_list")]
        private void cmdChatZoneList(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ZONE))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            SendMessage(player, "--- Zone list ---");
            if (zones.Count == 0)
            {
                SendMessage(player, "None...");
                return;
            }

            foreach (KeyValuePair<string, Zone> zone in zones)
                SendMessage(player, $"ID: {zone.Key} - {zone.Value.definition.Name} - {zone.Value.definition.Location}");
        }

        [ChatCommand("zone")]
        private void cmdChatZone(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ZONE))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            string zoneId;
            if (!lastPlayerZone.TryGetValue(player.userID, out zoneId))
            {
                SendMessage(player, "You must start editing a zone first. /zone_edit <zone ID>");
                return;
            }

            Zone zone;
            if (!zones.TryGetValue(zoneId, out zone))
            {
                SendMessage(player, "Unable to find a zone with ID : {0}", zoneId);
                return;
            }

            if (args.Length == 0)
            {
                SendMessage(player, "/zone <option> <value>");
                string message = $"<color={configData.Notifications.Color}>Name:</color> {zone.definition.Name}";
                message += $"\n<color={configData.Notifications.Color}>Enabled:</color> {zone.definition.Enabled}";
                message += $"\n<color={configData.Notifications.Color}>ID:</color> {zone.definition.Id}";
                message += $"\n<color={configData.Notifications.Color}>Comfort:</color> {zone.definition.Comfort}";
                message += $"\n<color={configData.Notifications.Color}>Temperature:</color> {zone.definition.Temperature}";
                message += $"\n<color={configData.Notifications.Color}>Radiation:</color> {zone.definition.Radiation}";
                message += $"\n<color={configData.Notifications.Color}>Safe Zone?:</color> {zone.definition.SafeZone}";
                SendReply(player, message);

                message = $"<color={configData.Notifications.Color}>Radius:</color> {zone.definition.Radius}";
                message += $"\n<color={configData.Notifications.Color}>Location:</color> {zone.definition.Location}";
                message += $"\n<color={configData.Notifications.Color}>Size:</color> {zone.definition.Size}";
                message += $"\n<color={configData.Notifications.Color}>Rotation:</color> {zone.definition.Rotation}";
                SendReply(player, message);

                message = $"<color={configData.Notifications.Color}>Enter Message:</color> {zone.definition.EnterMessage}";
                message += $"\n<color={configData.Notifications.Color}>Leave Message:</color> {zone.definition.LeaveMessage}";
                SendReply(player, message);

                message = $"<color={configData.Notifications.Color}>Permission:</color> {zone.definition.Permission}";
                message += $"\n<color={configData.Notifications.Color}>Eject Spawnfile:</color> {zone.definition.EjectSpawns}";
                message += $"\n<color={configData.Notifications.Color}>Parent Zone ID:</color> {zone.definition.ParentID}";
                SendReply(player, message);

                SendReply(player, $"<color={configData.Notifications.Color}>Flags:</color> {zone.definition.Flags}");
                ShowZone(player, zoneId);
                return;
            }

            if (args[0].ToLower() == "flags")
            {
                OpenFlagEditor(player, zoneId);
                return;
            }

            if (args.Length % 2 != 0)
            {
                SendMessage(player, "Value missing. You must follow a option with a value");
                return;
            }
            UpdateZoneDefinition(zone, args, player);
            zone.Reset();
            SaveData();
            ShowZone(player, zoneId);
        }

        [ChatCommand("zone_flags")]
        private void cmdChatZoneFlags(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ZONE))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            string zoneId;
            if (!lastPlayerZone.TryGetValue(player.userID, out zoneId))
            {
                SendMessage(player, "You must start editing a zone first. /zone_edit <zone ID>");
                return;
            }

            OpenFlagEditor(player, zoneId);
        }

        [ChatCommand("zone_player")]
        private void cmdChatZonePlayer(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_ZONE))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            BasePlayer targetPlayer = player;
            if (args != null && args.Length > 0)
            {
                targetPlayer = BasePlayer.Find(args[0]);
                if (!targetPlayer)
                {
                    SendMessage(player, "Unable to find a player with the specified information");
                    return;
                }
            }

            EntityZones entityZones;
            if (!zonedPlayers.TryGetValue(targetPlayer.userID, out entityZones))
            {
                SendReply(player, "The specified player is not in any zone");
                return;
            }

            SendMessage(player, $"--- {targetPlayer.displayName} ---");
            SendMessage(player, $"Has Flags: {entityZones.Flags}");
            SendMessage(player, "Is in zones:");

            foreach (Zone zone in entityZones.Zones)
                SendMessage(player, $"{zone.definition.Id}: {zone.definition.Name} - {zone.definition.Location}");
        }

        [ConsoleCommand("zone")]
        private void ccmdZone(ConsoleSystem.Arg arg)
        {
            if (!HasPermission(arg, PERMISSION_ZONE))
            {
                SendReply(arg, "You don't have access to this command");
                return;
            }

            string zoneId = arg.GetString(0);
            Zone zone;
            if (!arg.HasArgs(3) || !zones.TryGetValue(zoneId, out zone))
            {
                SendReply(arg, "Zone ID not found or too few arguments supplied: zone <zoneid> <arg> <value>");
                return;
            }

            string[] args = new string[arg.Args.Length - 1];
            Array.Copy(arg.Args, 1, args, 0, args.Length);

            UpdateZoneDefinition(zone, args, arg.Player());
            zone.Reset();
            SaveData();
        }
        #endregion

        #region UI
        const string ZMUI = "zmui.editor";
        #region Helper
        public static class UI
        {
            static public CuiElementContainer Container(string panel, string color, string min, string max, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = min, AnchorMax = max},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
                return container;
            }
            static public void Panel(ref CuiElementContainer container, string panel, string color, string min, string max, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void Label(ref CuiElementContainer container, string panel, string text, int size, string min, string max, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel);

            }
            static public void Button(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion

        #region Creation
        private void OpenFlagEditor(BasePlayer player, string zoneId)
        {
            Zone zone = GetZoneByID(zoneId);
            if (!zone)
            {
                SendReply(player, $"Error getting zone object with ID: {zoneId}");
                CuiHelper.DestroyUi(player, ZMUI);
            }

            CuiElementContainer container = UI.Container(ZMUI, UI.Color("2b2b2b", 0.9f), "0 0", "1 1", true);
            UI.Label(ref container, ZMUI, $"Zone Flag Editor", 18, "0 0.92", "1 1");
            UI.Label(ref container, ZMUI, $"Zone ID: {zoneId}\nName: {zone.definition.Name}\n{(zone.definition.Size != Vector3.zero ? $"Box Size: {zone.definition.Size}\nRotation: {zone.definition.Rotation}" : $"Radius: {zone.definition.Radius}\nSafe Zone: {zone.definition.SafeZone}")}", 13, "0.05 0.8", "1 0.92", TextAnchor.UpperLeft);
            UI.Label(ref container, ZMUI, $"Comfort: {zone.definition.Comfort}\nRadiation: {zone.definition.Radiation}\nTemperature: {zone.definition.Temperature}\nZone Enabled: {zone.definition.Enabled}", 13, "0.25 0.8", "1 0.92", TextAnchor.UpperLeft);
            UI.Label(ref container, ZMUI, $"Permission: {zone.definition.Permission}\nEject Spawnfile: {zone.definition.EjectSpawns}\nEnter Message: {zone.definition.EnterMessage}\nExit Message: {zone.definition.LeaveMessage}", 13, "0.5 0.8", "1 0.92", TextAnchor.UpperLeft);
            UI.Button(ref container, ZMUI, UI.Color("#d85540", 1f), "Exit", 12, "0.95 0.96", "0.99 0.99", $"zmui.editflag {zoneId} exit");

            int count = 0;

            string[] flags = Enum.GetNames((typeof(ZoneFlags))).OrderBy(x => x).ToArray();

            for (int i = 0; i < flags.Length; i++)
            {
                string flagName = flags.ElementAt(i);
                if (flagName == "None")
                    continue;

                bool value = HasFlag(zoneId, flagName);

                float[] position = GetButtonPosition(count);

                UI.Label(ref container, ZMUI, flagName, 12, $"{position[0]} {position[1]}", $"{position[0] + ((position[2] - position[0]) / 2)} {position[3]}");
                UI.Button(ref container, ZMUI, value ? UI.Color("#72E572", 1f) : UI.Color("#d85540", 1f), value ? "Enabled" : "Disabled", 12, $"{position[0] + ((position[2] - position[0]) / 2)} {position[1]}", $"{position[2]} {position[3]}", $"zmui.editflag {zoneId} {flagName} {!value}");

                count++;
            }

            CuiHelper.DestroyUi(player, ZMUI);
            CuiHelper.AddUi(player, container);
        }

        private float[] GetButtonPosition(int i)
        {
            int column = i == 0 ? 0 : ColumnNumber(4, i);
            int row = i - (column * 4);

            float offsetX = 0.04f + ((0.01f + 0.21f) * row);
            float offsetY = (0.76f - (column * 0.04f));

            return new float[] { offsetX, offsetY, offsetX + 0.21f, offsetY + 0.03f };
        }

        private int ColumnNumber(int max, int count) => Mathf.FloorToInt(count / max);
        #endregion

        #region Commands
        [ConsoleCommand("zmui.editflag")]
        private void ccmdEditFlag(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (!player)
                return;

            string zoneId = arg.GetString(0);

            if (arg.GetString(1) == "exit")
                CuiHelper.DestroyUi(player, ZMUI);
            else
            {
                Zone zone = GetZoneByID(zoneId);
                if (!zone)
                {
                    SendReply(player, $"Error getting zone object with ID: {zoneId}");
                    CuiHelper.DestroyUi(player, ZMUI);
                }

                ZoneFlags flag = (ZoneFlags)Enum.Parse((typeof(ZoneFlags)), arg.GetString(1));

                if (arg.GetBool(2))
                    AddFlag(zone, flag);
                else RemoveFlag(zone, flag);

                SaveData();

                OpenFlagEditor(player, zoneId);
            }
        }
        #endregion
        #endregion

        #region Config        
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Autolight Options")]
            public AutoLightOptions AutoLights { get; set; }

            [JsonProperty(PropertyName = "Notification Options")]
            public NotificationOptions Notifications { get; set; }

            [JsonProperty(PropertyName = "NPC players can deal player damage in zones with PvpGod flag")]
            public bool NPCHurtPvpGod { get; set; }

            [JsonProperty(PropertyName = "Allow decay damage in zones with Undestr flag")]
            public bool DecayDamageUndestr { get; set; }

            public class AutoLightOptions
            {
                [JsonProperty(PropertyName = "Time to turn lights on")]
                public float OnTime { get; set; }

                [JsonProperty(PropertyName = "Time to turn lights off")]
                public float OffTime { get; set; }

                [JsonProperty(PropertyName = "Lights require fuel to activate automatically")]
                public bool RequiresFuel { get; set; }
            }

            public class NotificationOptions
            {
                [JsonProperty(PropertyName = "Display notifications via PopupNotifications")]
                public bool Popups { get; set; }

                [JsonProperty(PropertyName = "Chat prefix")]
                public string Prefix { get; set; }

                [JsonProperty(PropertyName = "Chat color (hex)")]
                public string Color { get; set; }
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
                AutoLights = new ConfigData.AutoLightOptions
                {
                    OnTime = 18f,
                    OffTime = 6f,
                    RequiresFuel = true
                },
                Notifications = new ConfigData.NotificationOptions
                {
                    Color = "#d85540",
                    Popups = false,
                    Prefix = "[Zone Manager] :"
                },
                NPCHurtPvpGod = false,
                DecayDamageUndestr = false,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(3, 0, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Data Management
        private void SaveData()
        {
            storedData.definitions = new HashSet<Zone.Definition>();

            foreach (KeyValuePair<string, Zone> zone in zones)
                storedData.definitions.Add(zone.Value.definition);

            data.WriteObject(storedData);
        }

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("ZoneManager/zone_data");
            data.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new Vector3Converter() };

            storedData = data.ReadObject<StoredData>();
            if (storedData == null)
                storedData = new StoredData();
        }

        private class StoredData
        {
            public HashSet<Zone.Definition> definitions = new HashSet<Zone.Definition>();
        }

        private class EntityZones
        {
            public ZoneFlags Flags { get; private set; }

            public HashSet<Zone> Zones { get; private set; }

            public EntityZones()
            {
                Zones = new HashSet<Zone>();
            }

            public void AddFlags(ZoneFlags flags)
            {
                Flags |= flags;
            }

            public void RemoveFlags(ZoneFlags flags)
            {
                Flags &= ~flags;
            }

            public bool HasFlag(ZoneFlags flags)
            {
                return (Flags & flags) == flags;
            }

            public void UpdateFlags()
            {
                Flags = ZoneFlags.None;

                for (int i = 0; i < Zones.Count; i++)
                {
                    Zone zone = Zones.ElementAt(i);
                    if (!zone)
                        continue;

                    AddFlags(zone.definition.Flags & ~zone.disabledFlags);
                }

                for (int i = 0; i < Zones.Count; i++)
                {
                    Zone zone = Zones.ElementAt(i);
                    if (!zone)
                        continue;

                    if (zone.parent != null && Zones.Contains(zone.parent))
                        RemoveFlags(zone.parent.definition.Flags);
                }
            }

            public bool EnterZone(Zone zone)
            {
                return Zones.Add(zone);
            }

            public bool LeaveZone(Zone zone)
            {
                return Zones.Remove(zone);
            }

            public bool IsInZone(Zone zone)
            {
                return Zones.Contains(zone);
            }

            public bool IsInZone(string zoneId)
            {
                return Zones.Select(x => x.definition.Id).Contains(zoneId);
            }

            public bool ShouldRemove() => Count == 0;

            public int Count => Zones.Count;
        }

        private class Vector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Vector3 vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    string[] values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                JObject o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }
        #endregion

        #region Localization
        private string Message(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["noBuild"] = "You are not allowed to build in this area!",
            ["noUpgrade"] = "You are not allowed to upgrade structures in this area!",
            ["noDeploy"] = "You are not allowed to deploy items in this area!",
            ["noCup"] = "You are not allowed to deploy cupboards in this area!",
            ["noChat"] = "You are not allowed to chat in this area!",
            ["noSuicide"] = "You are not allowed to suicide in this area!",
            ["noGather"] = "You are not allowed to gather in this area!",
            ["noLoot"] = "You are not allowed loot in this area!",
            ["noSignUpdates"] = "You can not update signs in this area!",
            ["noOvenToggle"] = "You can not toggle ovens and lights in this area!",
            ["noPickup"] = "You can not pick up objects in this area!",
            ["noVending"] = "You can not use vending machines in this area!",
            ["noStash"] = "You can not hide a stash in this area!",
            ["noCraft"] = "You can not craft in this area!",
            ["eject"] = "You are not allowed in this area!",
            ["attract"] = "You are not allowed to leave this area!",
            ["kill"] = "Access to this area is restricted!",
            ["noVoice"] = "You are not allowed to voice chat in this area!",
            ["novehiclesenter"] = "Vehicles are not allowed in this area!",
            ["novehiclesleave"] = "Vehicles are not allowed to leave this area!"
        };
        #endregion                
    }
}
