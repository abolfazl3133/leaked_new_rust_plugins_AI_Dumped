using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Ext.Chaos;
using Oxide.Ext.Chaos.Data;
using Oxide.Ext.Chaos.Json;
using Oxide.Ext.Chaos.UIFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Bounds = UnityEngine.Bounds;
using Chaos = Oxide.Ext.Chaos;
using Color = Oxide.Ext.Chaos.UIFramework.Color;
using Font = Oxide.Ext.Chaos.UIFramework.Font;
using GridLayoutGroup = Oxide.Ext.Chaos.UIFramework.GridLayoutGroup;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("TeleportGUI", "k1lly0u", "2.0.33")]
    class TeleportGUI : ChaosPlugin
    {
        #region Fields

        #region Permissions
        [Chaos.Permission] private const string PERMISSION_TP_USE = "teleportgui.tp.use";
        [Chaos.Permission] private const string PERMISSION_TP_CANCEL = "teleportgui.tp.tpcancel";
        [Chaos.Permission] private const string PERMISSION_TP_BACK = "teleportgui.tp.tpback";
        [Chaos.Permission] private const string PERMISSION_TP_BACK_ADMIN = "teleportgui.tp.tpback.admin";

        [Chaos.Permission] private const string PERMISSION_TP_HERE = "teleportgui.tp.tphere";
        [Chaos.Permission] private const string PERMISSION_TP_SLEEPERS = "teleportgui.tp.sleepers";
        [Chaos.Permission] private const string PERMISSION_TP_AUTOACCEPT = "teleportgui.tp.autoaccept";
        [Chaos.Permission] private const string PERMISSION_TP_LOCATION = "teleportgui.tp.location"; 
        
        [Chaos.Permission] private const string PERMISSION_HOME_USE = "teleportgui.homes.use";
        [Chaos.Permission] private const string PERMISSION_HOME_BACK = "teleportgui.homes.back";
        [Chaos.Permission] private const string PERMISSION_HOME_BACK_ADMIN = "teleportgui.homes.back.admin";
        [Chaos.Permission] private const string PERMISSION_HOME_BYPASS = "teleportgui.homes.back.bypass";
        [Chaos.Permission] private const string PERMISSION_HOME_VIEW_OTHER_HOMES = "teleportgui.homes.viewothershomes";
        [Chaos.Permission] private const string PERMISSION_HOME_DELETE_OTHER_HOMES = "teleportgui.homes.deleteothershomes";
        
        [Chaos.Permission] private const string PERMISSION_WARP_USE = "teleportgui.warps.use";
        [Chaos.Permission] private const string PERMISSION_WARP_BACK = "teleportgui.warps.back";
        [Chaos.Permission] private const string PERMISSION_WARP_BACK_ADMIN = "teleportgui.warps.back.admin";
        [Chaos.Permission] private const string PERMISSION_WARP_BYPASS = "teleportgui.warps.back.bypass";
        [Chaos.Permission] private const string PERMISSION_WARP_ADMIN = "teleportgui.warps.admin";

        [Chaos.Permission] private const string PERMISSION_HIDE_UI = "teleportgui.hideingui";

        [Chaos.Permission] private const string PERMISSION_SEE_ALL = "teleportgui.seeall";
        
        [Chaos.Permission] private const string PERMISSION_COMMAND_ADMIN = "teleportgui.admin";
        #endregion
        
        private static Datafile<TeleportData> m_TeleportData;
        private static Datafile<Hash<string, WarpPoint>> m_WarpData;
        private static Hash<string, MonumentWarpPoint> m_MonumentWarps = new Hash<string, MonumentWarpPoint>();
        
        private static Hash<ulong, Vector3> m_LastTeleport = new Hash<ulong, Vector3>();
        private static Hash<ulong, Vector3> m_LastHome = new Hash<ulong, Vector3>();
        private static Hash<ulong, Vector3> m_LastWarp = new Hash<ulong, Vector3>();
        private readonly Hash<ulong, Timer> m_PopupTimers = new Hash<ulong, Timer>();

        private static Func<float, Action, Timer> m_CreateTimer;
        
        private static ItemDefinition m_ScrapItemDefinition;

        private static RaycastHit[] m_RayBuffer = new RaycastHit[64];
        
        private static readonly List<Monument> m_Monuments = new List<Monument>();
        private static readonly List<Monument> m_OilRigs = new List<Monument>();
        
        private readonly Hash<string, Bounds> m_BoundsOverrides = new Hash<string, Bounds>
        {
            ["fishing_village_a"] = new Bounds(Vector3.up * 5f, Vector3.one * 85),
            ["fishing_village_b"] = new Bounds(Vector3.up * 5f, Vector3.one * 75),
            ["fishing_village_c"] = new Bounds(Vector3.up * 5f, Vector3.one * 50),
            ["lighthouse"] = new Bounds(Vector3.up * 20f, Vector3.one * 80),
            ["swamp_a"] = new Bounds(),
            ["swamp_b"] = new Bounds(),
            ["swamp_c"] = new Bounds(),
            ["supermarket_1"] = new Bounds(Vector3.zero, Vector3.one * 75),
            ["powerplant_1"] = new Bounds(Vector3.zero, new Vector3(250, 200, 300)),
            ["launch_site_1"] = new Bounds(Vector3.forward * -25, new Vector3(600, 200, 350)),
            ["trainyard_1"] = new Bounds(Vector3.zero, Vector3.one * 250),
            ["water_treatment_plant_1"] = new Bounds(Vector3.forward * -50, new Vector3(300, 200, 300)),
            ["radtown_small_3"] = new Bounds(Vector3.forward * -25, Vector3.one * 175),
            ["harbor_2"] = new Bounds(Vector3.zero, new Vector3(250, 200, 300)),
            ["train_tunnel_double_entrance"] = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(100, 50, 100))
        };
        
        private const string FOUNDATION_SHORTNAME = "foundation";
        private const string FOUNDATION_TRIANGLE_SHORTNAME = "foundation.triangle";
        private const string FLOOR_SHORTNAME = "floor";
        private const string FLOOR_TRIANGLE_SHORTNAME = "floor.triangle";

        private bool m_IsNewSave;
        
        private enum Mode { Teleport, Home, Warp }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            m_TeleportData = new Datafile<TeleportData>($"{Title}/userdata", new VectorConverter());
            m_WarpData = new Datafile<Hash<string, WarpPoint>>($"{Title}/warpdata", new VectorConverter());
            
            Configuration.RegisterCustomPermissions(permission, this);

            foreach (WarpPoint warpData in m_WarpData.Data.Values)
            {
                if (!string.IsNullOrEmpty(warpData.Permission))
                {
                    string perm = warpData.Permission.StartsWith("teleportgui.") ? warpData.Permission : $"teleportgui.{warpData.Permission}";
                    if (!permission.PermissionExists(perm))
                        permission.RegisterPermission(perm, this);
                }
            }

            m_GetString = GetString;
            m_SendMessage = BroadcastToPlayer;
            m_CreateTimer = timer.Every;

            TeleportRequest.m_PopupAction = PlayerTeleporter.m_PopupAction = CreateTeleportRequestPopup;
            PositionTeleporter.m_PopupAction = CreateTeleportRequestPopup;
            
            foreach (string cmdAlias in Configuration.TP.CommandAliases)
                cmd.AddChatCommand(cmdAlias, this, TPCommand);

            foreach (string cmdAlias in Configuration.Home.CommandAliases)
                cmd.AddChatCommand(cmdAlias, this, HomeCommand);
            
            foreach (string cmdAlias in Configuration.Warp.CommandAliases)
                cmd.AddChatCommand(cmdAlias, this, WarpCommand);
            
            if (m_TeleportData.Data.ShouldResetUses)
                ResetDailyUses();
            else timer.Once(TimeUntilMidnight(), ResetDailyUses);

            if (!Configuration.Home.SleepingBags.CreateHomeOnBagPlacement &&
                !Configuration.Home.SleepingBags.CreateHomeOnBedPlacement &&
                !Configuration.Home.SleepingBags.CreateHomeOnBeachTowelPlacement)
            {
                Unsubscribe(nameof(CanRenameBed));
                Unsubscribe(nameof(OnEntitySpawned));
            }
        }

        private void OnServerInitialized()
        {
            PrepareLocalization();
            
            PurgeOldUsers();

            if (m_IsNewSave && Configuration.Home.WipeHomesOnNewServerSave)
                m_TeleportData.Data.WipeHomesData();

            SetupUIComponents();
            
            m_ScrapItemDefinition = ItemManager.FindItemDefinition("scrap");

            bool saveConfig = false;
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                string shortname = Path.GetFileNameWithoutExtension(monument.name);
                PreventBuildingMonumentTag[] monumentTags = monument.GetComponentsInChildren<PreventBuildingMonumentTag>(true);

                Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                Vector3 position = default;
                float radius = 0f;
                
                if (monumentTags?.Length > 0)
                {
                    foreach (PreventBuildingMonumentTag monumentTag in monumentTags)
                    {
                        Collider collider = monumentTag ? monumentTag.GetComponent<Collider>() : null;

                        if (!monumentTag || collider.gameObject.layer != 29 || (!collider || collider is MeshCollider))
                            continue;

                        if (collider is SphereCollider sphereCollider)
                        {
                            if (sphereCollider.radius > radius)
                            {
                                position = sphereCollider.transform.position;
                                radius = sphereCollider.radius;
                            }
                        }
                        else if (collider is BoxCollider boxCollider)
                        {
                            Vector3 localCenter = monument.transform.InverseTransformPoint(boxCollider.transform.TransformPoint(boxCollider.center));
                            Vector3 localSize = Vector3.Scale(boxCollider.size, boxCollider.transform.lossyScale);
                            bounds.Encapsulate(new Bounds(localCenter, localSize));
                        }
                    }
                }

                if (radius == 0f && bounds == default && m_BoundsOverrides.TryGetValue(shortname, out Bounds @override))
                    bounds = @override;
                
                if (shortname.Contains("oilrig", CompareOptions.IgnoreCase))
                    m_OilRigs.Add(new Monument(shortname, monument.IsSafeZone, monument.transform, position, radius, bounds));
                else if (shortname.Contains("underwater_lab", CompareOptions.IgnoreCase))
                    continue;
                else
                {
                    if (!Configuration.Warp.MonumentWarps.TryGetValue(shortname, out ConfigData.WarpOptions.MonumentWarp monumentWarp))
                    {
                        Configuration.Warp.MonumentWarps[shortname] = monumentWarp = new ConfigData.WarpOptions.MonumentWarp();
                        saveConfig = true;
                    }
                    
                    m_Monuments.Add(new Monument(shortname, monument.IsSafeZone, monument.transform, position, radius, bounds));
                    
                    if (monumentWarp.Enabled)
                    {
                        string uniqueName = textInfo.ToTitleCase(Regex.Replace(shortname.Replace("_", " "), @"[\d-]", string.Empty).Trim());
                        string gridCoord = MapHelper.PositionToString(monument.transform.position);
                        
                        m_Messages[shortname] = uniqueName;
                        uniqueName += $" ({gridCoord})";
                        
                        MonumentWarpPoint monumentWarpPoint = new MonumentWarpPoint(shortname, gridCoord, uniqueName, monument.transform, bounds, position, radius, monument.IsSafeZone && monumentWarp.SafeZoneOnly);
                        if (monumentWarpPoint.Count > 0)
                        {
                            m_MonumentWarps[uniqueName] = monumentWarpPoint;

                            if (!string.IsNullOrEmpty(monumentWarp.Permission))
                            {
                                string perm = monumentWarp.Permission.StartsWith("teleportgui.") ? monumentWarp.Permission : $"teleportgui.{monumentWarp.Permission}";
                                if (!permission.PermissionExists(perm))
                                    permission.RegisterPermission(perm, this);

                                monumentWarpPoint.Permission = perm;
                            }

                            if (!string.IsNullOrEmpty(monumentWarp.Command))
                                cmd.AddChatCommand(monumentWarp.Command, this, (player, command, args) =>
                                {
                                    if (!player.HasPermission(PERMISSION_WARP_USE) || !monumentWarpPoint.HasPermission(player))
                                    {
                                        SendReply(player, "General.NoPermission");
                                        return;
                                    }
                                    
                                    WarpTo(player, uniqueName);
                                });
                        }
                    }
                }
            }

            lang.RegisterMessages(m_Messages, this);
            
            if (saveConfig)
                SaveConfiguration();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (m_TeleportData.Data.Users.TryGetValue(player.userID, out TeleportData.User userData))
                userData.LastOnlineTime = CurrentTime();
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (PlayerTeleporter.HasIncomingPending(player, out PlayerTeleporter teleporter) || PlayerTeleporter.HasOutgoingPending(player, out teleporter))            
                teleporter.CancelTeleport(true, "Reason.Disconnected");

            if (TeleportRequest.HasIncomingRequest(player, out TeleportRequest teleporterRequest) || TeleportRequest.HasOutgoingRequest(player, out teleporterRequest))            
                teleporterRequest.RequestCancelled();

            if (PositionTeleporter.IsWaiting(player, out PositionTeleporter positionTeleporter))
                positionTeleporter.CancelTeleport(true, "Reason.Disconnected");
        }
        
        private void OnEntitySpawned(SleepingBag sleepingBag)
        {
            if (!sleepingBag)           
                return;            

            BasePlayer player = BasePlayer.FindByID(sleepingBag.OwnerID);
            if (!player)
                return;

            if ((sleepingBag.ShortPrefabName == "sleepingbag_leather_deployed" && Configuration.Home.SleepingBags.CreateHomeOnBagPlacement) ||
                (sleepingBag.ShortPrefabName == "bed_deployed" && Configuration.Home.SleepingBags.CreateHomeOnBedPlacement) ||
                (sleepingBag.ShortPrefabName == "beachtowel.deployed" && Configuration.Home.SleepingBags.CreateHomeOnBeachTowelPlacement))
            {
                if (Configuration.Home.SleepingBags.OnlyCreateInBuilding && sleepingBag.IsOutside())
                    return;

                if (ZoneManager.IsLoaded && ZoneManager.PlayerHasFlag(player, "notp"))
                {
                    SendReply(player, "Homes can not be set in NoTP zones");
                    return;
                }
                
                TeleportData.User userData = GetOrCreateUserSettings(player);
                if (HasMaximumHomes(player, userData))
                {
                    SendReply(player, "Home.Error.LimitReached");
                    return;
                }
                
                string newName = userData.Homes.ContainsKey(sleepingBag.niceName) ? GetUniqueBagName(userData, sleepingBag.niceName) : sleepingBag.niceName;

                userData.Homes[newName] = new TeleportData.User.HomePoint
                {
                    Position = sleepingBag.transform.position,
                    EntityID = sleepingBag.net.ID.Value
                };
                
                int maxHomes = GetMaxHomesForPlayer(player);
                if (maxHomes == 0)
                    SendReply(player, "Home.Success.Created.Bed", newName);
                else SendReply(player, "Home.Success.Created.Bed.Remaining", newName, maxHomes - userData.Homes.Count);
            }
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo hitInfo)
        {
            if (!player) 
                return;

            if (Configuration.TP.CancelOnDamage)
            {
                if (PlayerTeleporter.HasIncomingPending(player, out PlayerTeleporter teleporter) || PlayerTeleporter.HasOutgoingPending(player, out teleporter))
                    teleporter.CancelTeleport(true, "Reason.TookDamage");
            }

            if (PositionTeleporter.IsWaiting(player, out PositionTeleporter positionTeleporter))
            {
                if ((positionTeleporter.IsHomeTeleport && Configuration.Home.CancelOnDamage) || (!positionTeleporter.IsHomeTeleport && Configuration.Warp.CancelOnDamage))
                    positionTeleporter.CancelTeleport(true, "Reason.TookDamage");
            }
        }

        private void OnEntityDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (!player) 
                return;

            if (Configuration.TP.CancelOnDeath)
            {
                if (PlayerTeleporter.HasIncomingPending(player, out PlayerTeleporter teleporter) || PlayerTeleporter.HasOutgoingPending(player, out teleporter))
                    teleporter.CancelTeleport(true, "Reason.Death");
            }

            if (PositionTeleporter.IsWaiting(player, out PositionTeleporter positionTeleporter))
            {
                if ((positionTeleporter.IsHomeTeleport && Configuration.Home.CancelOnDeath) || (!positionTeleporter.IsHomeTeleport && Configuration.Warp.CancelOnDeath))
                    positionTeleporter.CancelTeleport(true, "Reason.Death");
            }
        }

        private void OnEntityDeath(SleepingBag sleepingBag, HitInfo hitInfo) => HandleSleepingBagDestroyed(sleepingBag);

        private void OnEntityKill(SleepingBag sleepingBag) => HandleSleepingBagDestroyed(sleepingBag);
        
        private void CanRenameBed(BasePlayer player, SleepingBag sleepingBag, string bedName)
        {
            if (m_TeleportData.Data.Users.TryGetValue(sleepingBag.OwnerID, out TeleportData.User userData))
            {
                string homeName = null;
                TeleportData.User.HomePoint homePoint = null;
                foreach (KeyValuePair<string, TeleportData.User.HomePoint> kvp in userData.Homes)
                {
                    if (kvp.Value.EntityID == sleepingBag.net.ID.Value)
                    {
                        homeName = kvp.Key;
                        homePoint = kvp.Value;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(homeName) || homePoint == null)
                    return;

                NextTick(() =>
                {
                    if (!sleepingBag || sleepingBag.IsDestroyed)
                        return;

                    string newName = userData.Homes.ContainsKey(sleepingBag.niceName) ? GetUniqueBagName(userData, sleepingBag.niceName) : sleepingBag.niceName;
                    userData.Homes.Remove(homeName);
                    userData.Homes.Add(newName, homePoint);
                });
            }
        }

        private string GetUniqueBagName(TeleportData.User userData, string bedName)
        {
            int random = Random.Range(1000, 9999);
            if (userData.Homes.ContainsKey(bedName + " " + random))
                return GetUniqueBagName(userData, bedName);
            
            return bedName  + " " + random;
        }

        private void OnServerSave() => m_TeleportData.Save();

        private void OnNewSave(string str) => m_IsNewSave = true;

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                ChaosUI.Destroy(player, TPUI);
                ChaosUI.Destroy(player, TPR_POPUP);
                ChaosUI.Destroy(player, TPP_POPUP);
            }
            
            TeleportRequest.Clear();
            PlayerTeleporter.Clear();
            PositionTeleporter.Clear();
            
            if (!Interface.Oxide.IsShuttingDown)
                m_TeleportData.Save();

            m_Monuments.Clear();
            m_OilRigs.Clear();
            
            m_MonumentWarps.Clear();
            
            m_TeleportData = null;
            m_WarpData = null;
            m_SendMessage = null;
            m_CreateTimer = null;
            Configuration = null;
        }
        #endregion
        
        #region Functions
        private void PurgeOldUsers()
        {
            List<ulong> purgeUsers = Pool.Get<List<ulong>>();
            double currentTime = CurrentTime();
            foreach (KeyValuePair<ulong, TeleportData.User> kvp in m_TeleportData.Data.Users)
            {
                if (kvp.Value.LastOnlineTime + (Configuration.PurgeDays * 86400) < currentTime)
                    purgeUsers.Add(kvp.Key);
            }

            foreach (ulong playerId in purgeUsers)
                m_TeleportData.Data.Users.Remove(playerId);

            Pool.FreeUnmanaged(ref purgeUsers);
        }

        private void HandleSleepingBagDestroyed(SleepingBag sleepingBag)
        {
            if (!sleepingBag || sleepingBag.net == null || 
                (!Configuration.Home.SleepingBags.CreateHomeOnBagPlacement && !Configuration.Home.SleepingBags.CreateHomeOnBedPlacement))
                return;

            NetworkableId sleepingBagId = sleepingBag.net.ID;
            
            BasePlayer player = BasePlayer.FindByID(sleepingBag.OwnerID);

            if (m_TeleportData.Data.Users.TryGetValue(sleepingBag.OwnerID, out TeleportData.User userData))
            {
                foreach (KeyValuePair<string, TeleportData.User.HomePoint> kvp in userData.Homes)
                {
                    if (kvp.Value.EntityID == sleepingBagId.Value)
                    {
                        if (player && player.IsConnected)
                            SendReply(player, "Notification.BedHomeDestroyed", kvp.Key);

                        userData.Homes.Remove(kvp);
                        break;
                    }
                }
            }
        }
        #endregion

        #region Messaging
        private static Action<BasePlayer, string, object[]> m_SendMessage;

        private static Func<string, BasePlayer, string> m_GetString;

        private static string GetTranslatedString(string key, BasePlayer player) => m_GetString(key, player);
        
        private static void SendReply(BasePlayer target, string key, params object[] args) => m_SendMessage(target, key, args);
        
        private void BroadcastToPlayer(BasePlayer player, string key, params object[] args)
        {            
            string message = lang.GetMessage(key, this, player.UserIDString);
            if (args?.Length > 0)
                message = string.Format(message, args);

            if (Configuration.Chat.UsePrefix)
                message = Configuration.Chat.Prefix + message;
            
            if (Configuration.Chat.Icon != 0UL)
                player.SendConsoleCommand("chat.add", 2, Configuration.Chat.Icon, message);
            else player.ChatMessage(message);
        }
        #endregion
        
        #region Helpers
        private static TeleportData.User GetOrCreateUserSettings(BasePlayer player)
        {
            if (!m_TeleportData.Data.Users.TryGetValue(player.userID, out TeleportData.User userData))
            {
                userData = m_TeleportData.Data.Users[player.userID] = new TeleportData.User{ LastOnlineTime = CurrentTime() };
            }

            return userData;
        }
        
        private static bool IsNearOilRig(Vector3 position)
        {
            foreach (Monument monument in m_OilRigs)
            {
                if (monument.IsInMonument(position))
                    return true;
            }

            return false;
        }

        private static bool IsInMonument(BasePlayer player, bool ignoreSafeZone = false) => IsInMonument(player.transform.position, ignoreSafeZone);
        
        private static bool IsInMonument(Vector3 position, bool ignoreSafeZone = false) 
        {
            foreach (Monument monument in m_Monuments)
            {
                if (ignoreSafeZone && monument.IsSafeZone)
                    continue;
                
                if (monument.IsInMonument(position))
                    return true;
            }

            return false;
        }

        private static bool IsInUnderwaterLab(Vector3 position)
        {
            int hits = Physics.OverlapSphereNonAlloc(position, 1f, Vis.colBuffer, 1 << 18);

            for (int i = 0; i < hits; i++)
            {
                Collider col = Vis.colBuffer[i];
                EnvironmentVolume environmentVolume = col.gameObject.GetComponent<EnvironmentVolume>();
                if (environmentVolume != null && (environmentVolume.Type & EnvironmentType.UnderwaterLab) == EnvironmentType.UnderwaterLab)
                    return true;
            }
            return false;
        }

        private static bool IsInFoundation(Vector3 position) => IsUnderFoundation(position) || IsInsideFoundation(position);

        private static bool IsInsideFoundation(Vector3 pos)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Ray(pos + (Vector3.up * 2f), Vector3.down), 3f, 1 << 21);

            for (int i = 0; i < hits.Length; i++)
            {
                BuildingBlock block = hits[i].GetEntity() as BuildingBlock;
                if (block != null && block.ShortPrefabName is FOUNDATION_SHORTNAME or FOUNDATION_TRIANGLE_SHORTNAME)
                {
                    if (block.transform.position.y > pos.y)
                        return true;
                }
            }
            return false;
        }
        
        private static bool IsUnderFoundation(Vector3 pos)
        {
            bool hitBackFaces = Physics.queriesHitBackfaces;

            try
            {
                Physics.queriesHitBackfaces = true;
                if (Physics.Raycast(pos + (Vector3.up * 0.25f), Vector3.up, out RaycastHit raycastHit, 50f, 1 << 21, QueryTriggerInteraction.Collide))
                {
                    BuildingBlock buildingBlock = raycastHit.GetEntity() as BuildingBlock;
                    if (buildingBlock && buildingBlock.ShortPrefabName is FOUNDATION_SHORTNAME or FOUNDATION_TRIANGLE_SHORTNAME)
                        return true;
                }
                return false;
            }
            finally
            {
                Physics.queriesHitBackfaces = hitBackFaces;
            }
        }
        
        private static bool CheckFoundation(Vector3 position) => 
            FindBuildingBlock(position, FOUNDATION_SHORTNAME) || FindBuildingBlock(position, FOUNDATION_TRIANGLE_SHORTNAME);
        
        private static bool CheckFloor(Vector3 position) => 
            FindBuildingBlock(position, FLOOR_SHORTNAME) || FindBuildingBlock(position, FLOOR_TRIANGLE_SHORTNAME);
        
        private static bool FindBuildingBlock(Vector3 position, string shortname)
        {
            int num = Physics.RaycastNonAlloc(new Ray(position + (Vector3.up * 0.1f), Vector3.down), m_RayBuffer, 0.2f);

            if (num == 0)
                return false;

            for (int i = 0; i < num; i++)
            {
                RaycastHit raycastHit = m_RayBuffer[i];
           
                BuildingBlock buildingBlock = raycastHit.GetEntity() as BuildingBlock;

                if (buildingBlock && buildingBlock.ShortPrefabName == shortname)
                    return true;
            }
            return false;
        }
        
        private static bool IsInWater(BasePlayer player)
        {
            ModelState modelState = player.modelState;
            return modelState != null && modelState.waterLevel > 0f;
        }

        private static bool HasReachedDailyLimit(BasePlayer player, TeleportData.User userData, Mode mode)
        {
            switch (mode)
            {
                case Mode.Teleport:
                {
                    if (Configuration.TP.Limits.Default == 0)
                        return false;

                    int limit = Configuration.TP.Limits.GetHighestOption(player);
                    if (limit == 0)
                        return false;

                    return userData.TPUsage.UsesToday >= limit;
                }
                case Mode.Home:
                {
                    if (Configuration.Home.Limits.Default == 0)
                        return false;

                    int limit = Configuration.Home.Limits.GetHighestOption(player);
                    if (limit == 0)
                        return false;
                    
                    return userData.HomeUsage.UsesToday >= limit;
                }
                case Mode.Warp:
                {
                    if (Configuration.Warp.Limits.Default == 0)
                        return false;

                    int limit = Configuration.Warp.Limits.GetHighestOption(player);
                    if (limit == 0)
                        return false;
                    
                    return userData.WarpUsage.UsesToday >= limit;
                }
            }

            return false;
        }
        
        private static int TimeUntilMidnight() => ((59 - DateTime.Now.Second) + ((59 - DateTime.Now.Minute) * 60) + ((23 - DateTime.Now.Hour) * 3600));
        
        private void ResetDailyUses()
        {
            foreach (TeleportData.User userSettings in m_TeleportData.Data.Users.Values)
            {
                userSettings.TPUsage.UsesToday = 0;
                userSettings.HomeUsage.UsesToday = 0;
                userSettings.WarpUsage.UsesToday = 0;
            }
            
            m_TeleportData.Data.LastResetTime = CurrentTime();
            
            m_TeleportData.Save();
            
            timer.Once(TimeUntilMidnight(), ResetDailyUses);
        }

        private static bool IsAdmin(BasePlayer player) => ServerUsers.Is(player.userID, ServerUsers.UserGroup.Moderator) || ServerUsers.Is(player.userID, ServerUsers.UserGroup.Owner);
        #endregion
        
        #region Teleport
        private static void Teleport(BasePlayer player, BasePlayer target) => Teleport(player, target.transform.position);

        private static void Teleport(BasePlayer player, Vector3 position)
        {
            m_LastTeleport[player.userID] = player.transform.position;

            try
            {
                if (player.isMounted)
                    player.GetMounted().DismountPlayer(player, true);

                player.SetParent(null, true, true);

                player.StartSleeping();
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);

                player.EnablePlayerCollider();
                player.SetServerFall(true);

                player.MovePosition(position);
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);

                if (player.IsConnected)
                {
                    player.UpdateNetworkGroup();
                    player.SendNetworkUpdateImmediate();
                    player.ClearEntityQueue();
                    player.SendFullSnapshot();
                }
            }
            finally
            {                
                player.EnablePlayerCollider();
                player.SetServerFall(false);
            }
        }
        #endregion

        #region Player Search
        private static Action<List<BasePlayer>, string, ListHashSet<BasePlayer>> m_FindPlayerAction = ((results, search, input) =>
        {
            foreach (BasePlayer player in input)
            {
                if (player.UserIDString == search)
                {
                    results.Clear();
                    results.Add(player);
                    return;
                }

                if (player.displayName.Contains(search, CompareOptions.OrdinalIgnoreCase))
                    results.Add(player);
            }
        });

        private static List<BasePlayer> m_PlayerSearchList = new List<BasePlayer>();
        
        private static List<BasePlayer> FindPlayer(string nameOrUserId, bool includeSleepers = false)
        {
            m_PlayerSearchList.Clear();
            
            m_FindPlayerAction(m_PlayerSearchList, nameOrUserId, BasePlayer.activePlayerList);

            if (includeSleepers)
                m_FindPlayerAction(m_PlayerSearchList, nameOrUserId, BasePlayer.sleepingPlayerList);

            return m_PlayerSearchList;
        }
        #endregion

        #region Payment
        private static bool PayForTeleport(BasePlayer player, Mode mode)
        {
            ConfigData.PurchaseOptions purchaseOptions = mode == Mode.Warp ? Configuration.Warp.Purchase :
                                                                 mode == Mode.Home ? Configuration.Home.Purchase :
                                                                 Configuration.TP.Purchase;
            
            int cost = purchaseOptions.GetLowestOption(player);
            
            switch (purchaseOptions.Mode)
            {
                case PurchaseMode.ServerRewards:
                    if (!ServerRewards.IsLoaded || ServerRewards.CheckPoints(player.userID) < cost)
                    {
                        SendReply(player, "Purchase.Error.SR", cost);
                        return false;
                    }
                    break;

                case PurchaseMode.Economics:
                    if (!Economics.IsLoaded || Convert.ToInt32(Economics.Balance(player.userID)) < cost)
                    {
                        SendReply(player, "Purchase.Error.Economics", cost);
                        return false;
                    }
                    break;

                case PurchaseMode.Scrap:
                    if (player.inventory.GetAmount(m_ScrapItemDefinition.itemid) < cost)
                    {
                        SendReply(player, "Purchase.Error.Scrap", cost);
                        return false;
                    }
                    break;

                default:
                    Debug.Log("[TeleportGUI] Invalid currency type set in config!");
                    return false;
            }

            switch (purchaseOptions.Mode)
            {
                case PurchaseMode.ServerRewards:
                    ServerRewards.TakePoints(player.userID, cost);
                    SendReply(player, "Purchase.Success.SR", cost);
                    break;
                    
                case PurchaseMode.Economics:
                    Economics.Withdraw(player.userID, cost);
                    SendReply(player, "Purchase.Success.Economics", cost);
                    break;
                    
                case PurchaseMode.Scrap:
                    player.inventory.Take(null, m_ScrapItemDefinition.itemid, cost);
                    SendReply(player, "Purchase.Success.Scrap", cost);
                    break;
            }

            return true;
        }

        private static void RefundPayment(BasePlayer player, Mode mode)
        {
            ConfigData.PurchaseOptions purchaseOptions = mode == Mode.Warp ? Configuration.Warp.Purchase :
                                                         mode == Mode.Home ? Configuration.Home.Purchase :
                                                         Configuration.TP.Purchase;
            
            int cost = purchaseOptions.GetLowestOption(player);

            switch (purchaseOptions.Mode)
            {
                case PurchaseMode.ServerRewards:
                    ServerRewards.AddPoints(player.userID, cost);
                    SendReply(player, "Purchase.Refund.SR", cost);
                    break;
                    
                case PurchaseMode.Economics:
                    Economics.Deposit(player.userID, cost);
                    SendReply(player, "Purchase.Refund.Economics", cost);
                    break;
                    
                case PurchaseMode.Scrap:
                    player.GiveItem(ItemManager.Create(m_ScrapItemDefinition, cost), BaseEntity.GiveItemReason.PickedUp);
                    SendReply(player, "Purchase.Refund.Scrap", cost);
                    break;
            }
        }
        #endregion
        
        #region Classes
        private interface ITeleport
        {
            bool IsValid { get; set; }
            
            int TimeRemaining { get; }
            
            bool CanAccept { get; }

            void Accept();
            
            void Decline();
            
            void Cancel();

            bool CanCancel(BasePlayer player);
        }

        private class TeleportRequest : Pool.IPooled, ITeleport
        {
            private BasePlayer m_From;
            private BasePlayer m_To;

            private ulong m_FromID;
            private ulong m_ToID;
            
            private bool m_TpHere;
            private int m_Time;
            private bool m_IsPaying;
            private bool m_IsInstant;

            private Timer m_Timer;
            
            public int TimeRemaining => m_Time;

            public bool CanAccept => true;

            public bool IsValid { get; set; }

            private static Hash<ulong, TeleportRequest> m_OutgoingRequests = new Hash<ulong, TeleportRequest>();
            private static Hash<ulong, TeleportRequest> m_IncomingRequests = new Hash<ulong, TeleportRequest>();

            public static Action<BasePlayer, string, ITeleport, string, string, bool> m_PopupAction;
            
            public static void Create(BasePlayer from, BasePlayer to, int timeoutTime, bool tpHere, bool playerIsPaying)
            {
                TeleportRequest teleportRequest = Pool.Get<TeleportRequest>();
                
                teleportRequest.m_From = from;
                teleportRequest.m_FromID = from.userID;
                teleportRequest.m_To = to;
                teleportRequest.m_ToID = to.userID;
                teleportRequest.m_TpHere = tpHere;
                teleportRequest.m_Time = timeoutTime;
                teleportRequest.m_IsPaying = playerIsPaying;
                teleportRequest.IsValid = true;
                
                if (tpHere)
                {
                    SendReply(from, "TPRequest.Here.Sent", to.displayName);
                    SendReply(to, "TPRequest.Here.Received", from.displayName);
                }
                else
                {
                    SendReply(from, "TPRequest.Sent", to.displayName);
                    SendReply(to, "TPRequest.Received", from.displayName);
                }

                if (m_TeleportData.Data.Users.TryGetValue(to.userID, out TeleportData.User userData))
                {
                    if (((userData.AutoAccept & TeleportData.User.AutoAcceptEnum.All) != 0) ||
                        ((userData.AutoAccept & TeleportData.User.AutoAcceptEnum.Teams) != 0 && from.currentTeam != 0UL && from.currentTeam == to.currentTeam) ||
                        ((userData.AutoAccept & TeleportData.User.AutoAcceptEnum.Clans) != 0 && Clans.IsLoaded && Clans.IsClanMember(from.userID, to.userID)) ||
                        ((userData.AutoAccept & TeleportData.User.AutoAcceptEnum.Friends) != 0 && Friends.IsLoaded && Friends.AreFriends(from.userID, to.userID)))
                    {
                        teleportRequest.m_IsInstant = true;
                        teleportRequest.RequestAccepted();
                        return;
                    }
                }
                
                m_OutgoingRequests[from.userID] = teleportRequest;
                m_IncomingRequests[to.userID] = teleportRequest;

                teleportRequest.m_Timer = m_CreateTimer(1f, teleportRequest.TimerTick);

                m_PopupAction(to, TPR_POPUP, teleportRequest, tpHere ? "Popup.Incoming.TPHere" : "Popup.Incoming.TPR", from.displayName.StripTags(), true);
                m_PopupAction(from, TPR_POPUP, teleportRequest, tpHere ? "Popup.Outgoing.TPHere" : "Popup.Outgoing.TPR", to.displayName.StripTags(), false);
            }

            public static void Clear()
            {
                List<TeleportRequest> list = Pool.Get<List<TeleportRequest>>();
                list.AddRange(m_OutgoingRequests.Values);
                
                foreach (TeleportRequest teleportRequest in list)
                    teleportRequest.RequestCancelled();
                
                Pool.FreeUnmanaged(ref list);
                
                m_OutgoingRequests.Clear();
                m_IncomingRequests.Clear();

                m_PopupAction = null;
            }

            public static bool HasIncomingRequest(BasePlayer target, out TeleportRequest teleportRequest) => m_IncomingRequests.TryGetValue(target.userID, out teleportRequest);

            public static bool HasOutgoingRequest(BasePlayer player, out TeleportRequest teleportRequest) => m_OutgoingRequests.TryGetValue(player.userID, out teleportRequest);

            public static bool HasPendingRequest(BasePlayer player) => m_IncomingRequests.ContainsKey(player.userID) || m_OutgoingRequests.ContainsKey(player.userID);
            
            private void TimerTick()
            {
                if (m_Time == 0)
                {
                    RequestTimeOut();
                    return;
                }

                if (!m_To || !m_To.IsConnected || !m_From || !m_From.IsConnected)
                {
                    RequestCancelled();
                    return;
                }
                m_Time--;
            }

            public void Accept() => RequestAccepted();

            public void Decline() => RequestDeclined();

            public void Cancel() => RequestCancelled();
            
            public bool CanCancel(BasePlayer player) => true;

            public void RequestAccepted()
            {
                int delay = Configuration.TP.Delay.GetLowestOption(m_From);

                if (m_IsInstant)
                {
                    SendReply(m_From, "TPRequest.Instant.Sent", m_To.displayName, delay);
                    SendReply(m_To, "TPRequest.Instant.Received", m_From.displayName, delay);
                }
                else
                {
                    SendReply(m_From, "TPRequest.To.Accepted", m_To.displayName, delay);
                    SendReply(m_To, "TPRequest.From.Accepted", m_From.displayName, delay);
                }

                PlayerTeleporter.Create(m_From, m_To, delay, m_IsPaying, m_TpHere);
                
                Destroy();
            }

            public void RequestDeclined()
            {
                if (m_From)
                {
                    if (m_TpHere)
                        SendReply(m_From, "TPRequest.Here.To.Denied", m_To.displayName);
                    else SendReply(m_From, "TPRequest.To.Denied", m_To.displayName);

                    if (m_IsPaying)
                        RefundPayment(m_From, Mode.Teleport);
                }

                if (m_To)
                {
                    if (m_TpHere)
                        SendReply(m_To, "TPRequest.Here.From.Denied", m_From.displayName);
                    else SendReply(m_To, "TPRequest.From.Denied", m_From.displayName);
                }

                Destroy();
            }

            public void RequestCancelled()
            {
                if (m_From != null)
                {
                    SendReply(m_From, "TPRequest.To.Cancelled", m_To.displayName);

                    if (m_IsPaying)
                        RefundPayment(m_From, Mode.Teleport);
                }

                if (m_To != null)
                {
                    SendReply(m_To, "TPRequest.From.Cancelled", m_From.displayName);
                }

                Destroy();
            }

            private void RequestTimeOut()
            {                
                if (m_From)
                {
                    if (m_To)
                    {
                        if (m_TpHere)
                        {
                            SendReply(m_From, "TPRequest.Here.To.TimeOut", m_To.displayName);
                            SendReply(m_To, "TPRequest.Here.From.TimeOut", m_From.displayName);
                        }
                        else
                        {
                            SendReply(m_From, "TPRequest.To.TimeOut", m_To.displayName);
                            SendReply(m_To, "TPRequest.From.TimeOut", m_From.displayName);
                        }
                    }

                    if (m_IsPaying)
                        RefundPayment(m_From, Mode.Teleport);
                }

                Destroy();
            }

            private void Destroy()
            {
                m_Timer?.Destroy();
                
                IsValid = false;
                
                m_OutgoingRequests.Remove(m_FromID);
                m_IncomingRequests.Remove(m_ToID);

                if (m_From)
                    ChaosUI.Destroy(m_From, TPR_POPUP);

                if (m_To)
                    ChaosUI.Destroy(m_To, TPR_POPUP);

                TeleportRequest teleportRequest = this;
                Pool.Free(ref teleportRequest);
            }
            
            public void EnterPool()
            {
                m_From = null;
                m_To = null;
                m_FromID = 0UL;
                m_ToID = 0UL;
                m_IsInstant = false;
            }

            public void LeavePool(){}
        }
        
        private class PlayerTeleporter : Pool.IPooled, ITeleport
        {
            private BasePlayer m_From;
            private BasePlayer m_To;

            private ulong m_FromID;
            private ulong m_ToID;
            
            private int m_TimeUntilTP;
            private bool m_IsPaying;
            private bool m_TPHere;
            private Timer m_Timer;

            public int TimeRemaining => m_TimeUntilTP;

            public bool CanAccept => false;

            public bool IsValid { get; set; }

            private static Hash<ulong, PlayerTeleporter> m_Outgoing = new Hash<ulong, PlayerTeleporter>();
            private static Hash<ulong, PlayerTeleporter> m_Incoming = new Hash<ulong, PlayerTeleporter>();
            
            public static Action<BasePlayer, string, ITeleport, string, string, bool> m_PopupAction;
            
            public static void Create(BasePlayer from, BasePlayer to, int delay, bool isPaying, bool tpHere)
            {
                PlayerTeleporter teleporter = Pool.Get<PlayerTeleporter>();
                teleporter.m_From = from;
                teleporter.m_FromID = from.userID;
                teleporter.m_To = to;
                teleporter.m_ToID = to.userID;
                teleporter.m_TimeUntilTP = delay;
                teleporter.m_IsPaying = isPaying;
                teleporter.m_TPHere = tpHere;
                teleporter.IsValid = true;
                
                m_Outgoing[from.userID] = teleporter;
                m_Incoming[to.userID] = teleporter;
                
                teleporter.m_Timer = m_CreateTimer(1f, teleporter.TimerTick);

                if (delay > 0)
                {
                    m_PopupAction(to, TPP_POPUP, teleporter, "Popup.Incoming.TP", from.displayName.StripTags(), true);
                    m_PopupAction(from, TPP_POPUP, teleporter, "Popup.Outgoing.TP", to.displayName.StripTags(), false);
                }
            }
            
            public static void Clear()
            {
                List<PlayerTeleporter> list = Pool.Get<List<PlayerTeleporter>>();
                list.AddRange(m_Outgoing.Values);
                
                foreach (PlayerTeleporter teleporter in list)
                    teleporter.CancelTeleport(true);
                
                Pool.FreeUnmanaged(ref list);
                
                m_Outgoing.Clear();
                m_Incoming.Clear();
                
                m_PopupAction = null;
            }

            public static bool HasIncomingPending(BasePlayer target, out PlayerTeleporter teleporter) => m_Incoming.TryGetValue(target.userID, out teleporter);

            public static bool HasOutgoingPending(BasePlayer player, out PlayerTeleporter teleporter) => m_Outgoing.TryGetValue(player.userID, out teleporter);

            public static bool IsWaiting(BasePlayer player) => m_Incoming.ContainsKey(player.userID) || m_Outgoing.ContainsKey(player.userID);
            
            private void TimerTick()
            {
                if (!m_To || !m_To.IsConnected || !m_From || !m_From.IsConnected)
                {
                    CancelTeleport(true, "Reason.Disconnected");
                    return;
                }
                
                if (m_TimeUntilTP == 0)
                {
                    Teleport();
                    return;
                }

                m_TimeUntilTP--;
            }

            private void Teleport()
            {
                if (m_To.IsDead())
                {
                    SendReply(m_From, "TP.Error.TargetDead");
                    CancelTeleport(false);
                    return;
                }

                if (!Configuration.Conditions.MeetsConditions(m_From, m_To))
                {
                    CancelTeleport(false);
                    return;
                }
                
                if (m_TPHere)
                    TeleportGUI.Teleport(m_To, m_From);
                else TeleportGUI.Teleport(m_From, m_To);

                TeleportData.User userData = GetOrCreateUserSettings(m_From);

                int cooldown = Configuration.TP.Cooldown.GetLowestOption(m_From);

                userData.TPUsage.Cooldown = CurrentTime() + cooldown;

                if (Configuration.TP.Limits.Default > 0 && !Configuration.TP.Purchase.PayAlways)
                {
                    int dailyLimit = Configuration.TP.Limits.GetHighestOption(m_From);

                    if (dailyLimit > 0)
                    {
                        if (userData.TPUsage.UsesToday < dailyLimit)
                            SendReply(m_From, "Notification.TP.Remaining", dailyLimit - userData.TPUsage.UsesToday - 1);

                        userData.TPUsage.UsesToday++;
                    }
                }
                
                Destroy();
            }
            
            public void Accept(){}

            public void Decline() => CancelTeleport(true, "Reason.Declined");

            public void Cancel() => CancelTeleport(true);
            
            public bool CanCancel(BasePlayer player) => player == m_From && player.HasPermission(PERMISSION_TP_CANCEL);

            public void CancelTeleport(bool notify, string reason = "")
            {
                if (notify)
                {
                    if (!string.IsNullOrEmpty(reason))
                    {
                        SendReply(m_From, "TP.To.Cancelled.Reason", m_To.displayName, GetTranslatedString(reason, m_From));
                        SendReply(m_To, "TP.From.Cancelled.Reason", m_From.displayName, GetTranslatedString(reason, m_To));
                    }
                    else
                    {
                        SendReply(m_From, "TP.To.Cancelled", m_To.displayName);
                        SendReply(m_To, "TP.From.Cancelled", m_From.displayName);
                    }
                }
            

                if (m_IsPaying)
                    RefundPayment(m_From, Mode.Teleport);
                
                Destroy();
            }
            
            private void Destroy()
            {
                if (m_From)
                    ChaosUI.Destroy(m_From, TPP_POPUP);
                
                if (m_To)
                    ChaosUI.Destroy(m_To, TPP_POPUP);
                
                m_Timer?.Destroy();

                IsValid = false;
                
                m_Outgoing.Remove(m_FromID);
                m_Incoming.Remove(m_ToID);
                
                PlayerTeleporter teleporter = this;
                Pool.Free(ref teleporter);
            }
            
            public void EnterPool()
            {
                m_From = null;
                m_To = null;
                m_FromID = 0UL;
                m_ToID = 0UL;
            }

            public void LeavePool(){}
        }
        
        private class PositionTeleporter : Pool.IPooled, ITeleport
        {
            private BasePlayer m_From;
            private ulong m_FromID;
            private Vector3 m_To;
            private string m_ToName;
            private int m_TimeUntilTP;
            private bool m_IsPaying;
            private Timer m_Timer;

            public int TimeRemaining => m_TimeUntilTP;

            public bool CanAccept => false;

            public bool IsValid { get; set; }
            
            public bool IsHomeTeleport { get; private set; }
            
            public bool IsTPBack;
            
            private static Hash<ulong, PositionTeleporter> m_Pending = new Hash<ulong, PositionTeleporter>();
            
            public static Action<BasePlayer, string, ITeleport, string, string, bool> m_PopupAction;
            
            public static void Create(BasePlayer from, Vector3 to, string homeName, int delay, bool isPaying, bool isHome)
            {
                PositionTeleporter teleporter = Pool.Get<PositionTeleporter>();
                teleporter.m_From = from;
                teleporter.m_FromID = from.userID;
                teleporter.m_To = to;
                teleporter.m_ToName = homeName;
                teleporter.m_TimeUntilTP = delay;
                teleporter.m_IsPaying = isPaying;
                teleporter.IsValid = true;
                teleporter.IsHomeTeleport = isHome;
                
                m_Pending[from.userID] = teleporter;
                
                teleporter.m_Timer = m_CreateTimer(1f, teleporter.TimerTick);
                
                if (delay > 0)
                    m_PopupAction(from, TPP_POPUP, teleporter, isHome ? "Popup.Outgoing.TP.Home" : "Popup.Outgoing.TP.Warp", homeName, false);
            }
            
            public static void Clear()
            {
                List<PositionTeleporter> list = Pool.Get<List<PositionTeleporter>>();
                list.AddRange(m_Pending.Values);
                
                foreach (PositionTeleporter teleporter in list)
                    teleporter.CancelTeleport(true);
                
                Pool.FreeUnmanaged(ref list);
                
                m_Pending.Clear();
                
                m_PopupAction = null;
            }

            public static bool IsWaiting(BasePlayer player, out PositionTeleporter teleporter) => m_Pending.TryGetValue(player.userID, out teleporter);

            public static bool IsWaiting(BasePlayer player, out bool isHomeTeleport)
            {
                if (m_Pending.TryGetValue(player.userID, out PositionTeleporter positionTeleporter))
                {
                    isHomeTeleport = positionTeleporter.IsHomeTeleport;
                    return true;
                }

                isHomeTeleport = false;
                return false;
            }
            
            private void TimerTick()
            {
                if (!m_From || !m_From.IsConnected)
                {
                    CancelTeleport(true, "Reason.Disconnected");
                    return;
                }
                
                if (m_TimeUntilTP == 0)
                {
                    Teleport();
                    return;
                }

                m_TimeUntilTP--;
            }

            private void Teleport()
            {
                if ((IsHomeTeleport && !Configuration.Conditions.MeetsConditions(m_From, m_To)) || (!IsHomeTeleport && !Configuration.Conditions.MeetsWarpConditions(m_From, m_To)))
                {
                    CancelTeleport(false);
                    return;
                }

                if (IsHomeTeleport)
                    m_LastHome[m_From.userID] = m_From.transform.position;
                else m_LastWarp[m_From.userID] = m_From.transform.position;
                
                TeleportGUI.Teleport(m_From, m_To);

                TeleportData.User userData = GetOrCreateUserSettings(m_From);

                int cooldown = IsHomeTeleport ? Configuration.Home.Cooldown.GetLowestOption(m_From) : 
                                                Configuration.Warp.Cooldown.GetLowestOption(m_From);

                TeleportData.User.Usage usage = IsHomeTeleport ? userData.HomeUsage : userData.WarpUsage;
                ConfigData.LimitOptions limits = IsHomeTeleport ? Configuration.Home.Limits : Configuration.Warp.Limits;
                
                usage.Cooldown = CurrentTime() + cooldown;

                if (limits.Default > 0 && !(IsHomeTeleport ? Configuration.Home.Purchase.PayAlways : Configuration.Warp.Purchase.PayAlways))
                {
                    int dailyLimit = limits.GetHighestOption(m_From);

                    if (dailyLimit > 0)
                    {
                        if (usage.UsesToday < dailyLimit)
                            SendReply(m_From, IsHomeTeleport ? "Notification.Home.Remaining" : "Notification.Warp.Remaining", dailyLimit - usage.UsesToday - 1);

                        usage.UsesToday++;
                    }
                }
                
                Destroy();
            }
            
            public void Accept(){}

            public void Decline(){}

            public void Cancel() => CancelTeleport(true);
            
            public bool CanCancel(BasePlayer player) => player == m_From;

            public void CancelTeleport(bool notify, string reason = "")
            {
                if (notify)
                {
                    if (!string.IsNullOrEmpty(reason))
                        SendReply(m_From, "TP.To.Cancelled", m_ToName, GetTranslatedString(reason, m_From));
                    else SendReply(m_From, "TP.To.Cancelled", m_ToName);
                }

                if (m_IsPaying)
                    RefundPayment(m_From, IsHomeTeleport ? Mode.Home : Mode.Warp);
                
                Destroy();
            }
            
            private void Destroy()
            {
                if (m_From)
                    ChaosUI.Destroy(m_From, TPP_POPUP);
                
                m_Timer?.Destroy();

                IsValid = false;
                
                m_Pending.Remove(m_FromID);
                
                PositionTeleporter teleporter = this;
                Pool.Free(ref teleporter);
            }
            
            public void EnterPool()
            {
                m_From = null;
                m_To = Vector3.zero;
                m_FromID = 0UL;
                m_ToName = string.Empty;
            }

            public void LeavePool(){}
        }
        #endregion
        
        #region TP Functions
        private void TPR(BasePlayer player, BasePlayer targetPlayer, bool tpHere)
        {
            if (IsAdmin(player) && Configuration.Admin.Instant)
            {
                if (!Configuration.Admin.Silent)
                    SendReply(targetPlayer, tpHere ? "TPR.Admin.YouWereTPTo" : "TPR.Admin.TPTo", player.displayName);

                SendReply(player, tpHere ? "TPR.Admin.YouTPToYou" : "TPR.YouTPTo", targetPlayer.displayName);

                if (tpHere)
                    Teleport(targetPlayer, player);
                else Teleport(player, targetPlayer);
                return;
            }

            if (TeleportRequest.HasPendingRequest(player) || PlayerTeleporter.IsWaiting(player))
            {
                SendReply(player, "TP.SelfHasPendingRequest");
                return;
            }
            
            if (TeleportRequest.HasPendingRequest(targetPlayer) || PlayerTeleporter.IsWaiting(targetPlayer))
            {
                SendReply(player, "TP.TargetPendingRequest", targetPlayer.displayName);
                return;
            }

            if (PositionTeleporter.IsWaiting(player, out bool isHomeTeleport))
            {
                SendReply(player, isHomeTeleport ? "Home.SelfHasPendingRequest" : "Warp.SelfHasPendingRequest");
                return;
            }
            
            TeleportData.User userData = GetOrCreateUserSettings(player);

            if (userData.TPUsage.IsOnCooldown())
            {
                SendReply(player, "TP.CooldownFormat", FormatTime(userData.TPUsage.Cooldown - CurrentTime()));
                return;
            }

            if (!Configuration.Conditions.MeetsConditions(player, targetPlayer))
                return;
            
            bool playerIsPaying = false;
            if (HasReachedDailyLimit(player, userData, Mode.Teleport) || Configuration.TP.Purchase.PayAlways)
            {
                if (!Configuration.TP.Purchase.PayAfterUsingDailyLimits && !Configuration.TP.Purchase.PayAlways)
                {
                    SendReply(player, "TP.MaxTeleportsReached");
                    return;
                }

                if (!PayForTeleport(player, Mode.Teleport))
                    return;

                playerIsPaying = true;
            }

            TeleportRequest.Create(player, targetPlayer, Configuration.TP.RequestTimeout, tpHere, playerIsPaying);
        }

        private void TPC(BasePlayer player)
        {
            if (PlayerTeleporter.HasOutgoingPending(player, out PlayerTeleporter teleporter))
            {
                teleporter.CancelTeleport(true);
                return;
            }

            object call = Interface.Oxide.CallHook("CancelAllTeleports", player);
            if (call is string)
            {
                SendReply(player, call as string);
                return;
            }

            SendReply(player, "TP.Error.NoPending");
        }

        private void TPB(BasePlayer player)
        {
            if (!m_LastTeleport.TryGetValue(player.userID, out Vector3 position))
            {
                SendReply(player, "TPB.NoBackLocation");
                return;
            }
            
            if (!IsAdmin(player))
            {
                if (TeleportRequest.HasPendingRequest(player) || PlayerTeleporter.IsWaiting(player))
                {
                    SendReply(player, "TP.SelfHasPendingRequest");
                    return;
                }

                if (PositionTeleporter.IsWaiting(player, out bool isHomeTeleport))
                {
                    SendReply(player, isHomeTeleport ? "Home.SelfHasPendingRequest" : "Warp.SelfHasPendingRequest");
                    return;
                }
                
                if (IsInFoundation(position))
                {
                    SendReply(player, "General.InsideFoundation");
                    return;
                }
                
                if (!Configuration.Conditions.MeetsConditions(player, position))
                    return;
            }

            Teleport(player, position);
            SendReply(player, "TPB.Success");
        }

        private void TPHere(BasePlayer player, BasePlayer target)
        {
            if (!player.HasPermission(PERMISSION_TP_HERE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (player == target)
            {
                SendReply(player, "TPH.CantTeleportSelf");
                return;
            }

            if (IsAdmin(player) && Configuration.Admin.Instant)
            {
                if (!Configuration.Admin.Silent)
                    SendReply(player, "TPR.Admin.YouWereTPTo", player.displayName);
                
                SendReply(player, "TPR.Admin.TPTo", target.displayName);

                Teleport(target, player);
                return;
            }

            TPR(player, target, true);
        }
        #endregion
        
        #region Home Functions
        private int GetMaxHomesForPlayer(BasePlayer player)
        {
            if (Configuration.Home.MaxHomes.Default > 0)
            {
                int maxHomes = Configuration.Home.MaxHomes.GetHighestOption(player);
                return maxHomes;
            }

            return Configuration.Home.MaxHomes.Default;
        }

        private bool HasMaximumHomes(BasePlayer player, TeleportData.User userData)
        {
            int maxHomes = GetMaxHomesForPlayer(player);
            if (maxHomes == 0)
                return false;

            int homesCount = userData != null ? userData.Homes.Count : 0;
            return homesCount >= maxHomes;
        }
        
        private bool CanSetHome(BasePlayer player)
        {
            if (!Configuration.Home.AllowSetHomeInBuildBlocked && !player.CanBuild())
            {
                SendReply(player, "Home.Error.IsBuildingBlocked");
                return false;
            }

            m_TeleportData.Data.Users.TryGetValue(player.userID, out TeleportData.User userData);

            if (HasMaximumHomes(player, userData))
            {
                SendReply(player, "Home.Error.LimitReached");
                return false;
            }

            if (Configuration.Home.MinimumHomeRadiusDistance > 0)
            {
                if (userData != null)
                {
                    foreach (TeleportData.User.HomePoint home in userData.Homes.Values)
                    {
                        float distance = Vector3.Distance(player.transform.position, home.Position);

                        if (distance <= Configuration.Home.MinimumHomeRadiusDistance)
                        {
                            SendReply(player, "Home.Error.NearbyRadius",
                                distance.ToString("N1"),
                                Configuration.Home.MinimumHomeRadiusDistance.ToString("N1"));
                            return false;
                        }
                    }
                }
            }

            if (Configuration.Home.MustSetHomeOnBuilding)
            {
                if (!Configuration.Home.CanSetHomeOnFloor)
                {
                    if (!CheckFoundation(player.transform.position))
                    {
                        SendReply(player, "Home.Error.NotOnFoundation");
                        return false;
                    }
                }
                else
                {
                    if (!CheckFoundation(player.transform.position) && !CheckFloor(player.transform.position))
                    {
                        SendReply(player, "Home.Error.NotOnFoundationFloor");
                        return false;
                    }
                }
            }

            if (ZoneManager.IsLoaded)
            {
                if (ZoneManager.PlayerHasFlag(player, "notp"))
                {
                    SendReply(player, "Home.Error.NoTPZone");
                    return false;
                }
            }

            return true;
        }

        private bool IsHomePointValid(TeleportData.User.HomePoint homePoint)
        {
            if (homePoint.EntityID != 0U)
                return true;

            if (Configuration.Home.MustSetHomeOnBuilding)
            {
                if (!CheckFoundation(homePoint.Position) && !CheckFloor(homePoint.Position))
                    return false;
            }

            return true;
        }
        
        private bool CanTeleportHome(BasePlayer player, Vector3 position, out bool playerIsPaying)
        {
            playerIsPaying = false;
            
            if (TeleportRequest.HasPendingRequest(player) || PlayerTeleporter.IsWaiting(player))
            {
                SendReply(player, "TP.SelfHasPendingRequest");
                return false;
            }

            if (PositionTeleporter.IsWaiting(player, out bool isHomeTeleport))
            {
                SendReply(player, isHomeTeleport ? "Home.SelfHasPendingRequest" : "Warp.SelfHasPendingRequest");
                return false;
            }
            
            TeleportData.User userData = GetOrCreateUserSettings(player);

            if (userData.HomeUsage.IsOnCooldown())
            {
                SendReply(player, "Home.CooldownFormat", FormatTime(userData.HomeUsage.Cooldown - CurrentTime()));
                return false;
            }

            if (!Configuration.Conditions.MeetsConditions(player, position))
                return false;
            
            if (HasReachedDailyLimit(player, userData, Mode.Home) || Configuration.Home.Purchase.PayAlways)
            {
                if (!Configuration.Home.Purchase.PayAfterUsingDailyLimits && !Configuration.Home.Purchase.PayAlways)
                {
                    SendReply(player, "Home.MaxTeleportsReached");
                    return false;
                }

                if (!PayForTeleport(player, Mode.Home))
                    return false;

                playerIsPaying = true;
            }
            
            return true;
        }
        
        private void HomeBack(BasePlayer player)
        {
            if (!m_LastHome.TryGetValue(player.userID, out Vector3 position))
            {
                SendReply(player, "Home.Error.NoBackLocation");
                return;
            }

            Teleport(player, position);
            SendReply(player, "TPB.Success");
        }

        private void HomeBackLimited(BasePlayer player)
        {
            if (!m_LastHome.TryGetValue(player.userID, out Vector3 position))
            {
                SendReply(player, "Home.Error.NoBackLocation");
                return;
            }

            if (!CanTeleportHome(player, position, out bool playerIsPaying))
                return;

            PositionTeleporter.Create(player, position, "Previous location", Configuration.Home.Delay.GetLowestOption(player), playerIsPaying, true);
        }
        #endregion
        
        #region Warp Functions
        private void WarpTo(BasePlayer player, string warpName)
        {
            MonumentWarpPoint monumentWarpPoint = null;
            
            if (!m_WarpData.Data.TryGetValue(warpName, out WarpPoint warpPoint) && !m_MonumentWarps.TryGetValue(warpName, out monumentWarpPoint))
            {
                SendReply(player, "Warp.Error.DoesntExist", warpName);
                return;
            }

            IWarpPoint iWarpPoint = (IWarpPoint)warpPoint ?? monumentWarpPoint;

            if (!iWarpPoint.HasPermission(player))
            {
                SendReply(player, "Warp.Error.NoPermission");
                return;
            }

            Vector3 position = iWarpPoint.GetPosition();
            
            if (Configuration.Admin.Instant && player.IsAdmin)
            {
                Teleport(player, position);
                ChaosUI.Destroy(player, TPUI);
                return;
            }

            if (!CanWarpTo(player, position, out bool playerIsPaying))
                return;
            
            ChaosUI.Destroy(player, TPUI);

            PositionTeleporter.Create(player, position, warpName, Configuration.Warp.Delay.GetLowestOption(player), playerIsPaying, false);
        }
        
        private bool CanWarpTo(BasePlayer player, Vector3 position, out bool playerIsPaying)
        {
            playerIsPaying = false;
            
            if (TeleportRequest.HasPendingRequest(player) || PlayerTeleporter.IsWaiting(player))
            {
                SendReply(player, "TP.SelfHasPendingRequest");
                return false;
            }

            if (PositionTeleporter.IsWaiting(player, out bool isHomeTeleport))
            {
                SendReply(player, isHomeTeleport ? "Home.SelfHasPendingRequest" : "Warp.SelfHasPendingRequest");
                return false;
            }
            
            TeleportData.User userData = GetOrCreateUserSettings(player);

            if (userData.WarpUsage.IsOnCooldown())
            {
                SendReply(player, "Warp.CooldownFormat", FormatTime(userData.WarpUsage.Cooldown - CurrentTime()));
                return false;
            }

            if (!Configuration.Conditions.MeetsWarpConditions(player, position))
                return false;
            
            if (HasReachedDailyLimit(player, userData, Mode.Warp) || Configuration.Warp.Purchase.PayAlways)
            {
                if (!Configuration.Warp.Purchase.PayAfterUsingDailyLimits && !Configuration.Warp.Purchase.PayAlways)
                {
                    SendReply(player, "Warp.MaxTeleportsReached");
                    return false;
                }

                if (!PayForTeleport(player, Mode.Warp))
                    return false;

                playerIsPaying = true;
            }
            
            return true;
        }
        #endregion
        
        #region Chat Commands
        
        #region Locations
        [ChatCommand("tpsave")]
        void TPSaveCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_TP_LOCATION))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "TPL.NoNameSpecified");
                return;
            }

            TeleportData.User userData = GetOrCreateUserSettings(player);
            
            userData.Locations[args[0]] = player.transform.position;

            SendReply(player, "TPL.Success", args[0]);       
        }

        [ChatCommand("tpl")]
        void TPLCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_TP_LOCATION))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "TPL.NoNameSpecified");
                return;
            }

            TeleportData.User userData = GetOrCreateUserSettings(player);

            if (userData.Locations.Count == 0)
            {
                SendReply(player, "TPL.Error.NoLocations");
                return;
            }

            if (!userData.Locations.TryGetValue(args[0], out Vector3 position))
            {
                SendReply(player, "TPL.Error.NoLocation.Name", args[0]);
                return;
            }

            Teleport(player, position);
        }

        [ChatCommand("tpllist")]
        void TPLListCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_TP_LOCATION))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            TeleportData.User userData = GetOrCreateUserSettings(player);

            if (userData.Locations.Count == 0)
            {
                SendReply(player, "TPL.Error.NoLocations");
                return;
            }

            SendReply(player, "TPL.List", userData.Locations.Keys.ToSentence());
        }
        #endregion
        
        #region TP
        [ChatCommand("tp")]
        private void TPCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_TP_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (IsAdmin(player) && args.Length > 0)
            {
                if (args.Length == 1)
                {
                    List<BasePlayer> matches = FindPlayer(args[0], true);
                    if (matches.Count == 0)
                    {
                        SendReply(player, "General.PlayerNotFound", args[0]);
                        return;
                    }
                    
                    if (matches.Count > 1)
                    {
                        SendReply(player, "General.MultiplePlayersFound", args[0], matches.Select(p => p.displayName).ToSentence());
                        return;
                    }
                    
                    BasePlayer targetPlayer = matches[0];
                    Teleport(player, targetPlayer.transform.position);
                    SendReply(player, "TPR.YouTPTo", targetPlayer.displayName);
                    return;
                }
                
                if (args.Length == 2)
                {
                    List<BasePlayer> matches = FindPlayer(args[0], true);
                    if (matches.Count == 0)
                    {
                        SendReply(player, "General.PlayerNotFound", args[0]);
                        return;
                    }
                    
                    if (matches.Count > 1)
                    {
                        SendReply(player, "General.MultiplePlayersFound", args[0], matches.Select(p => p.displayName).ToSentence());
                        return;
                    }

                    BasePlayer sender = matches[0];
                    
                    matches = FindPlayer(args[1], true);
                    if (matches.Count == 0)
                    {
                        SendReply(player, "General.PlayerNotFound", args[1]);
                        return;
                    }
                    
                    if (matches.Count > 1)
                    {
                        SendReply(player, "General.MultiplePlayersFound", args[1], matches.Select(p => p.displayName).ToSentence());
                        return;
                    }

                    BasePlayer targetPlayer = matches[0];
                    
                    Teleport(sender, targetPlayer.transform.position);
                    
                    SendReply(sender, "TPR.YouTPTo", targetPlayer.displayName);
                    SendReply(targetPlayer, "TPR.Admin.TPTo", sender.displayName);
                    return;
                }

                if (args.Length < 3)
                {
                    SendReply(player, "TP.Error.PositionSyntax");
                    return;
                }

                if (!float.TryParse(args[0], out float x) || !float.TryParse(args[1], out float y) || !float.TryParse(args[2], out float z))
                {
                    SendReply(player, "TP.Error.PositionSyntax");
                    return;
                }

                Teleport(player, new Vector3(x, y, z));
                SendReply(player, "TP.Success.Position", x.ToString("N1"), y.ToString("N1"), z.ToString("N1"));
                return;
            }

            if (!Configuration.UI.DisableUI)
                ShowTeleportUI(player, Mode.Teleport);
            else
            {
                if (!player.HasPermission(PERMISSION_TP_LOCATION))
                {
                    SendReply(player, "Help.TP.1"); // /tpsave <name> - Save your current position as a TP location
                    SendReply(player, "Help.TP.2"); // /tpl <name> - Teleport to the specified TP location
                    SendReply(player, "Help.TP.3"); // /tplist - List all of your TP locations
                    SendReply(player, "Help.TP.4"); // /tpr <playername> - Request a teleport to the specified player
                    SendReply(player, "Help.TP.5"); // /tpa - Accept an incoming TP request
                    SendReply(player, "Help.TP.6"); // /tpd - Decline an incoming TP request
                    SendReply(player, "Help.TP.7"); // /tpc - Cancel a pending TP request
                    if (player.HasPermission(PERMISSION_TP_BACK))
                        SendReply(player, "Help.TP.8"); // /tpb - Teleport back to your previous location
                    
                    if (player.HasPermission(PERMISSION_TP_HERE))
                        SendReply(player, "Help.TP.9"); // /tprhere <playername> - Request a teleport that brings the specified player to you
                }
            }
        }

        [ChatCommand("tpr")]
        private void TPRCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_TP_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (args == null || args.Length < 1)
            {
                SendReply(player, "TPR.InvalidSynax");
                return;
            }

            string name = string.Join(" ", args);

            List<BasePlayer> matches = FindPlayer(name);
            if (matches.Count == 0)
            {
                SendReply(player, "General.PlayerNotFound", name);
                return;
            }
            
            if (matches.Count > 1)
            {
                SendReply(player, "General.MultiplePlayersFound", name, matches.Select(x => x.displayName).ToSentence());
                return;
            }
            
            BasePlayer targetPlayer = matches[0];

            if (targetPlayer == player)
            {
                SendReply(player, "TP.CantTeleportSelf");
                return;
            }

            TPR(player, targetPlayer, false);
        }

        [ChatCommand("tpa")]
        private void TPACommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_TP_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (!TeleportRequest.HasIncomingRequest(player, out TeleportRequest teleportRequest))
            {
                SendReply(player, "TPA.NonePending");
                return;
            }

            teleportRequest.RequestAccepted();
        }

        [ChatCommand("tpd")]
        private void TPDCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_TP_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (!TeleportRequest.HasIncomingRequest(player, out TeleportRequest teleportRequest))
            {
                SendReply(player, "TPA.NonePending");
                return;
            }

            teleportRequest.RequestDeclined();
        }

        [ChatCommand("tpc")]
        private void TPCCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_TP_CANCEL))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            TPC(player);
        }

        [ChatCommand("tpb")]
        private void TPBCommand(BasePlayer player, string command, string[] args)
        {
            if (player.HasPermission(PERMISSION_TP_BACK_ADMIN) && args != null && args.Length > 0)
            {
                string name = args[0];
                
                List<BasePlayer> matches = FindPlayer(name);
                if (matches.Count == 0)
                {
                    SendReply(player, "General.PlayerNotFound", name);
                    return;
                }
            
                if (matches.Count > 1)
                {
                    SendReply(player, "General.MultiplePlayersFound", name, matches.Select(x => x.displayName).ToSentence());
                    return;
                }
            
                BasePlayer targetPlayer = matches[0];
                if (!m_LastTeleport.TryGetValue(targetPlayer.userID, out Vector3 position))
                {
                    SendReply(player, "TPB.Admin.NoBackLocation", targetPlayer.displayName);
                    return;
                }
                
                Teleport(targetPlayer, position);
                
                SendReply(targetPlayer, "TPB.Admin.Success.Target");
                SendReply(player, "TPB.Admin.Success", targetPlayer.displayName);

                return;
            }
            
            if (!player.HasPermission(PERMISSION_TP_BACK))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            TPB(player);
        }

        [ChatCommand("tprhere")]
        private void TPRHereCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_TP_HERE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "TPR.Here.InvalidSynax");
                return;
            }

            string targetName = string.Join(" ", args);

            List<BasePlayer> matches = FindPlayer(targetName);
            if (matches.Count == 0)
            {
                SendReply(player, "General.PlayerNotFound", targetName);
                return;
            }
            
            if (matches.Count > 1)
            {
                SendReply(player, "General.MultiplePlayersFound", targetName, matches.Select(x => x.displayName).ToSentence());
                return;
            }

            TPHere(player, matches[0]);
        }
        #endregion
        
        #region Homes
        [ChatCommand("home")]
        private void HomeCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_HOME_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (args.Length > 0)
            {
                string option = args[0].ToLower();

                /* Support for NTeleportation format: /home add/remove name */
                if (option == "add")
                {
                    SetHomeCommand(player, "sethome", args.Skip(1).ToArray());
                    return;
                }

                if (option == "remove")
                {
                    DeleteHomeCommand(player, "delhome", args.Skip(1).ToArray());
                    return;
                }

                TeleportData.User userData = GetOrCreateUserSettings(player);

                string homeName = string.Join(" ", args);
                
                if (!userData.Homes.TryGetValue(homeName, out TeleportData.User.HomePoint homePoint))
                {
                    SendReply(player, "Home.Error.DoesntExist", homeName);
                    return;
                }

                if (!IsHomePointValid(homePoint))
                {
                    userData.Homes.Remove(homeName);
                    SendReply(player, "Home.Error.Invalid", homeName);
                    return;
                }

                if (!CanTeleportHome(player, homePoint.Position, out bool playerIsPaying))
                    return;
                
                PositionTeleporter.Create(player, homePoint.Position, homeName, Configuration.Home.Delay.GetLowestOption(player), playerIsPaying, true);
                return;
            }

            if (!Configuration.UI.DisableUI)
                ShowTeleportUI(player, Mode.Home);
            else
            {
                SendReply(player, "Help.Homes.1"); // /home <homename> - Teleport to the specified home
                SendReply(player, "Help.Homes.2"); // /sethome <homename> - Create a home on your current position
                SendReply(player, "Help.Homes.3"); // /delhome <homename> - Delete the home with the specified name
                SendReply(player, "Help.Homes.4"); // /listhomes - List all of your home names
                if (player.HasPermission(PERMISSION_HOME_BYPASS) || player.HasPermission(PERMISSION_HOME_BACK))
                    SendReply(player, "Help.Homes.5"); // /homec - Cancel a pending home teleport
            }
        }
        
        [ChatCommand("sethome")]
        private void SetHomeCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_HOME_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (Configuration.Home.SleepingBags.DisableSetHomeCommand)
            {
                SendReply(player, "Home.Error.BagDisableCommand");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "Home.Error.SetHomeSyntax");
                return;
            }

            TeleportData.User userData = GetOrCreateUserSettings(player);

            string homeName = string.Join(" ", args);

            if (userData.Homes.ContainsKey(homeName))
            {
                SendReply(player, "Home.Error.AlreadyExists", homeName);
                return;
            }

            if (!CanSetHome(player))
                return;

            userData.Homes.Add(homeName, new TeleportData.User.HomePoint{ Position = player.transform.position });

            int maxHomes = GetMaxHomesForPlayer(player);
            if (maxHomes == 0)
                SendReply(player, "Home.Success.Created", homeName);
            else SendReply(player, "Home.Success.Created.Remaining", homeName, maxHomes - userData.Homes.Count);
        }

        [ChatCommand("delhome")]
        private void DeleteHomeCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_HOME_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "Home.Error.DelHomeSyntax");
                return;
            }

            TeleportData.User userData;
            if (args.Length > 1 && player.HasPermission(PERMISSION_HOME_DELETE_OTHER_HOMES))
            {
                List<BasePlayer> targets = FindPlayer(args[0], true);

                if (targets.Count == 0)
                {
                    SendReply(player, "General.PlayerNotFound", args[0]);
                    return;
                }

                if (targets.Count > 1)
                {
                    SendReply(player, "General.MultiplePlayersFound", args[0], targets.Select(x => x.displayName).ToSentence());
                    return;
                }

                string targetHomeName = string.Join(" ", args.Skip(1));
                BasePlayer target = targets[0];
                if (!m_TeleportData.Data.Users.TryGetValue(target.userID, out userData) || userData.Homes.Count == 0)
                {
                    SendReply(player, "Home.Error.NoHomes.Target", target.displayName);
                    return;
                }

                if (!userData.Homes.ContainsKey(targetHomeName))
                {
                    SendReply(player, "Home.Error.DoesntExist.Target", target.displayName, targetHomeName);
                    return;
                }

                userData.Homes.Remove(targetHomeName);
                SendReply(player, "Home.Success.Deleted", targetHomeName);
                return;
            }

            if (!m_TeleportData.Data.Users.TryGetValue(player.userID, out userData) || userData.Homes.Count == 0)
            {
                SendReply(player, "Home.Error.NoHomes");
                return;
            }

            string homeName = string.Join(" ", args);

            if (!userData.Homes.ContainsKey(homeName))
            {
                SendReply(player, "Home.Error.DoesntExist", homeName);
                return;
            }

            userData.Homes.Remove(homeName);
            SendReply(player, "Home.Success.Deleted", homeName);
        }

        [ChatCommand("listhomes")]
        private void ListHomesCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_HOME_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            TeleportData.User userData;
            if (args.Length > 0)
            {
                if (!player.HasPermission(PERMISSION_HOME_VIEW_OTHER_HOMES))
                {
                    SendReply(player, "General.NoPermission");
                    return;
                }

                string playerName = string.Join(" ", args);

                List<BasePlayer> targets = FindPlayer(playerName, true);

                if (targets.Count == 0)
                {
                    SendReply(player, "General.PlayerNotFound", playerName);
                    return;
                }

                if (targets.Count > 1)
                {
                    SendReply(player, "General.MultiplePlayersFound", playerName, targets.Select(x => x.displayName).ToSentence());
                    return;
                }

                BasePlayer target = targets[0];
                if (!m_TeleportData.Data.Users.TryGetValue(target.userID, out userData) || userData.Homes.Count == 0)
                {
                    SendReply(player, "Home.Error.NoHomes.Target", target.displayName);
                    return;
                }

                SendReply(player, "Home.List.Target", target.displayName, string.Join(", ", userData.Homes.Keys));
                return;
            }

            if (!m_TeleportData.Data.Users.TryGetValue(player.userID, out userData) || userData.Homes.Count == 0)
            {
                SendReply(player, "Home.Error.NoHomes");
                return;
            }

            SendReply(player, "Home.List", string.Join(", ", userData.Homes.Keys));
        }

        [ChatCommand("homec")]
        private void HomeCancelCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_HOME_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (!PositionTeleporter.IsWaiting(player, out PositionTeleporter positionTeleporter))
            {
                SendReply(player, "Home.Error.NoPending");
                return;
            }

            positionTeleporter.CancelTeleport(true);
        }

        [ChatCommand("homeback")]
        private void HomeBackCommand(BasePlayer player, string command, string[] args)
        {
            if (player.HasPermission(PERMISSION_HOME_BACK_ADMIN) && args != null && args.Length > 0)
            {
                string name = args[0];
                
                List<BasePlayer> matches = FindPlayer(name);
                if (matches.Count == 0)
                {
                    SendReply(player, "General.PlayerNotFound", name);
                    return;
                }
            
                if (matches.Count > 1)
                {
                    SendReply(player, "General.MultiplePlayersFound", name, matches.Select(x => x.displayName).ToSentence());
                    return;
                }
            
                BasePlayer targetPlayer = matches[0];
                if (!m_LastHome.TryGetValue(targetPlayer.userID, out Vector3 position))
                {
                    SendReply(player, "TPB.Admin.NoBackLocation", targetPlayer.displayName);
                    return;
                }
                
                Teleport(targetPlayer, position);
                
                SendReply(targetPlayer, "TPB.Admin.Success.Target");
                SendReply(player, "TPB.Admin.Success", targetPlayer.displayName);

                return;
            }
            
            if (player.HasPermission(PERMISSION_HOME_BYPASS))
            {
                HomeBack(player);
                return;
            }

            if (player.HasPermission(PERMISSION_HOME_BACK))
            {
                HomeBackLimited(player);                
                return;
            }

            SendReply(player, "General.NoPermission");
        }
        #endregion
        
        #region Warp
        [ChatCommand("warp")]
        private void WarpCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_WARP_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (args.Length == 0)
            {
                if (!Configuration.UI.DisableUI)
                    ShowTeleportUI(player, Mode.Warp);
                else
                {
                    SendReply(player, "Help.Warp.1"); // /warp to <warpname> - Teleport to the specified warp point
                    SendReply(player, "Help.Warp.2"); // /warp list - Show available warp points
                    SendReply(player, "Help.Warp.3"); // /warpadd <warpname> - Add a new warp point on your current position
                    SendReply(player, "Help.Warp.4"); // /warpremove <warpname> - Delete the specified warp point
                }
                return;
            }

            if (args[0] == "to")
            {
                string warpName = string.Join(" ", args.Skip(1).ToArray());
                WarpTo(player, warpName);
                return;
            }
            
            if (args[0] == "list")
            {
                List<string> allowedWarps = Pool.Get<List<string>>();
                
                foreach (KeyValuePair<string, WarpPoint> kvp in m_WarpData.Data)
                {
                    if (!string.IsNullOrEmpty(kvp.Value.Permission) && !player.HasPermission(kvp.Value.Permission))
                        continue;
                    
                    allowedWarps.Add(kvp.Key);
                }
                
                SendReply(player, "WarpTo.List", allowedWarps.ToSentence());
                Pool.FreeUnmanaged(ref allowedWarps);
                return;
            }

            SendReply(player, "WarpTo.Error.Syntax");
        }
        
        [ChatCommand("warpback")]
        private void WarpBackCommand(BasePlayer player, string command, string[] args)
        {
            if (player.HasPermission(PERMISSION_WARP_BACK_ADMIN) && args != null && args.Length > 0)
            {
                string name = args[0];
                
                List<BasePlayer> matches = FindPlayer(name);
                if (matches.Count == 0)
                {
                    SendReply(player, "General.PlayerNotFound", name);
                    return;
                }
            
                if (matches.Count > 1)
                {
                    SendReply(player, "General.MultiplePlayersFound", name, matches.Select(x => x.displayName).ToSentence());
                    return;
                }
            
                BasePlayer targetPlayer = matches[0];
                if (!m_LastWarp.TryGetValue(targetPlayer.userID, out Vector3 position))
                {
                    SendReply(player, "TPB.Admin.NoBackLocation", targetPlayer.displayName);
                    return;
                }
                
                Teleport(targetPlayer, position);
                
                SendReply(targetPlayer, "TPB.Admin.Success.Target");
                SendReply(player, "TPB.Admin.Success", targetPlayer.displayName);

                return;
            }
            
            if (player.HasPermission(PERMISSION_WARP_BYPASS))
            {
                WarpBack(player);
                return;
            }

            if (player.HasPermission(PERMISSION_WARP_BACK))
            {
                WarpBackLimited(player);                
                return;
            }

            SendReply(player, "General.NoPermission");
        }
        
        private void WarpBack(BasePlayer player)
        {
            if (!m_LastWarp.TryGetValue(player.userID, out Vector3 position))
            {
                SendReply(player, "Warp.Error.NoBackLocation");
                return;
            }

            Teleport(player, position);
            SendReply(player, "TPB.Success");
        }

        private void WarpBackLimited(BasePlayer player)
        {
            if (!m_LastWarp.TryGetValue(player.userID, out Vector3 position))
            {
                SendReply(player, "Warp.Error.NoBackLocation");
                return;
            }

            if (!CanWarpTo(player, position, out bool playerIsPaying))
                return;

            PositionTeleporter.Create(player, position, "Previous location", Configuration.Warp.Delay.GetLowestOption(player), playerIsPaying, true);
        }

        [ChatCommand("warpadd")]
        private void WarpAddCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_WARP_ADMIN))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "WarpAdd.Error.Syntax");
                return;
            }

            string warpName = args[0];

            if (m_WarpData.Data.ContainsKey(warpName))
            {
                SendReply(player, "WarpAdd.Error.Exists", args[0]);
                return;
            }

            string permissionString = args.Length > 1 ?  args[1].ToLower() : string.Empty;
            if (!string.IsNullOrEmpty(permissionString))
            {
                if (!permissionString.StartsWith("teleportgui."))
                    permissionString = $"teleportgui.{permissionString}";
                
                permission.RegisterPermission(permissionString, this);
            }
            
            m_WarpData.Data.Add(warpName, new WarpPoint
            {
                Position = player.transform.position,
                Permission = permissionString
            });
            
            m_WarpData.Save();
            
            if (string.IsNullOrEmpty(permissionString))
                SendReply(player, "WarpAdd.Success", warpName);
            else SendReply(player, "WarpAdd.Success.Permission", warpName, permissionString);
        }

        [ChatCommand("warpremove")]
        private void WarpRemoveCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PERMISSION_WARP_ADMIN))
            {
                SendReply(player, "General.NoPermission");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "WarpRemove.Error.Syntax");
                return;
            }

            if (!m_WarpData.Data.ContainsKey(args[0]))
            {
                SendReply(player, "Warp.Error.DoesntExist", args[0]);
                return;
            }

            m_WarpData.Data.Remove(args[0]);
            m_WarpData.Save();
            
            SendReply(player, "WarpRemove.Success", args[0]);
        }
        #endregion
        #endregion

        #region Console Commands
        [ConsoleCommand("tpgui")]
        private void TPGuiCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (!player)
                return;

            if (!player.HasPermission(PERMISSION_TP_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }
            
            if (!Configuration.UI.DisableUI)
                ShowTeleportUI(player, Mode.Teleport);
        }
        
        [ConsoleCommand("homegui")]
        private void HomeGuiCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (!player)
                return;

            if (!player.HasPermission(PERMISSION_HOME_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }
            
            if (!Configuration.UI.DisableUI)
                ShowTeleportUI(player, Mode.Home);
        }
        
        [ConsoleCommand("warpgui")]
        private void WarpGuiCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (!player)
                return;

            if (!player.HasPermission(PERMISSION_WARP_USE))
            {
                SendReply(player, "General.NoPermission");
                return;
            }
            
            if (!Configuration.UI.DisableUI)
                ShowTeleportUI(player, Mode.Warp);
        }

        [ConsoleCommand("tpadmin")]
        private void AdminTPCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection?.player as BasePlayer;
            if (player && !player.HasPermission(PERMISSION_COMMAND_ADMIN))
            {
                SendReply(arg, "You do not have permission to use this command");
                return;
            }

            if (arg.Args == null || arg.Args.Length != 2)
            {
                SendReply(arg, "tpadmin wipehomes <user_id> - Wipe the homes for the specified player");
                SendReply(arg, "tpadmin wipelocations <user_id> - Wipe the locations for the specified player");
                SendReply(arg, "tpadmin wipetpusage <user_id> - Wipe the TP usage record for the specified player");
                SendReply(arg, "tpadmin wipehomeusage <user_id> - Wipe the home usage record for the specified player");
                SendReply(arg, "tpadmin wipewarpusage <user_id> - Wipe the warp usage record for the specified player");
                
                SendReply(arg, "All commands accept '*' as an argument in place of the user ID to indicate all players.\nex. 'tpadmin wipe homes *' will wipe all player homes");
                return;
            }

            string targetPlayer = arg.Args[1];
            bool applyToEveryone = targetPlayer.Equals("*", StringComparison.OrdinalIgnoreCase);
            TeleportData.User specifiedUser = null;
            
            if (!applyToEveryone)
            {
                if (!ulong.TryParse(arg.Args[1], out ulong userId))
                {
                    SendReply(arg, "Invalid user ID entered");
                    return;
                }

                if (!m_TeleportData.Data.Users.TryGetValue(userId, out specifiedUser))
                {
                    SendReply(arg, "No user data found with the specified user ID");
                    return;
                }
            }

            switch (arg.Args[0].ToLower())
            {
                case "wipehomes":
                    if (applyToEveryone)
                    {
                        foreach (TeleportData.User user in m_TeleportData.Data.Users.Values)
                        {
                            user.Homes.Clear();
                            user.HomeUsage.Reset();
                        }
                        
                        SendReply(arg, "You have wiped all home data");
                    }
                    else
                    {
                        specifiedUser.Homes.Clear();
                        specifiedUser.HomeUsage.Reset();
                        
                        SendReply(arg, $"You have wiped the home data for {arg.Args[1]}");
                    }
                    return;
                case "wipelocations":
                    if (applyToEveryone)
                    {
                        foreach (TeleportData.User user in m_TeleportData.Data.Users.Values)
                        {
                            user.Locations.Clear();
                        }
                        
                        SendReply(arg, "You have wiped all location data");
                    }
                    else
                    {
                        specifiedUser.Locations.Clear();
                        
                        SendReply(arg, $"You have wiped the location data for {arg.Args[1]}");
                    }
                    return;
                case "wipetpusage":
                    if (applyToEveryone)
                    {
                        foreach (TeleportData.User user in m_TeleportData.Data.Users.Values)
                        {
                            user.TPUsage.Reset();
                        }
                        
                        SendReply(arg, "You have wiped all TP usage data");
                    }
                    else
                    {
                        specifiedUser.TPUsage.Reset();
                        
                        SendReply(arg, $"You have wiped the TP usage data for {arg.Args[1]}");
                    }
                    return;
                case "wipehomeusage":
                    if (applyToEveryone)
                    {
                        foreach (TeleportData.User user in m_TeleportData.Data.Users.Values)
                        {
                            user.HomeUsage.Reset();
                        }
                        
                        SendReply(arg, "You have wiped all home usage data");
                    }
                    else
                    {
                        specifiedUser.HomeUsage.Reset();
                        
                        SendReply(arg, $"You have wiped the home usage data for {arg.Args[1]}");
                    }
                    return;
                case "wipewarpusage":
                    if (applyToEveryone)
                    {
                        foreach (TeleportData.User user in m_TeleportData.Data.Users.Values)
                        {
                            user.WarpUsage.Reset();
                        }
                        
                        SendReply(arg, "You have wiped all warp usage data");
                    }
                    else
                    {
                        specifiedUser.WarpUsage.Reset();
                        
                        SendReply(arg, $"You have wiped the warp usage data for {arg.Args[1]}");
                    }
                    return;
                default:
                    SendReply(arg, "Incorrect syntax");
                    return;
            }
        }

        #endregion
        
        #region UI
        private const string TPUI = "teleport.ui";
        private const string TPR_POPUP = "teleportrequest.ui.popup";
        private const string TPP_POPUP = "teleportpending.ui.popup";

        private string m_MagnifyImage;

        /*private Style m_BackgroundStyle;
        private Style m_PanelStyle;
        private Style m_HeaderStyle;
        private Style m_ButtonStyle;
        private Style m_ButtonDisabledStyle;
        private Style m_CloseStyle;
        private Style m_ToggleStyle;*/

        private StylePreset m_StylePreset;
        
        private OutlineComponent m_OutlineClose;
        private OutlineComponent m_OutlineHighlight;
        private OutlineComponent m_OutlineDark = new OutlineComponent(new Color(0.1647059f, 0.1803922f, 0.1921569f));

        private readonly GridLayoutGroup m_GridLayout = new GridLayoutGroup(2, 16, Axis.Horizontal)
        {
            Area = new Area(-220f, -215f, 220f, 215f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
        };
        
        private CommandCallbackHandler m_CallbackHandler;

        private void SetupUIComponents()
        {
            m_CallbackHandler = new CommandCallbackHandler(this);

            m_StylePreset = new StylePreset
            {
                Background = new Style(ChaosStyle.Background)
                {
                    ImageColor = new Color(Configuration.UI.Colors.Background.Hex, Configuration.UI.Colors.Background.Alpha)
                },
                Panel = new Style(ChaosStyle.Panel)
                {
                    ImageColor = new Color(Configuration.UI.Colors.Panel.Hex, Configuration.UI.Colors.Panel.Alpha)
                },
                Header = new Style(ChaosStyle.Header)
                {
                    ImageColor = new Color(Configuration.UI.Colors.Header.Hex, Configuration.UI.Colors.Header.Alpha),
                    Sprite = Sprites.Background_Rounded_top,
                    FontSize = 14,
                    Font = Font.PermanentMarker,
                    Alignment = TextAnchor.MiddleLeft
                },
                Button = new Style(ChaosStyle.Button)
                {
                    ImageColor = new Color(Configuration.UI.Colors.Button.Hex, Configuration.UI.Colors.Button.Alpha)
                },
                DisabledButton = new Style(ChaosStyle.DisabledButton)
                {
                    ImageColor = new Color(Configuration.UI.Colors.Button.Hex, Mathf.Min(Configuration.UI.Colors.Button.Alpha, 0.8f)),
                    FontColor = new Color(1f, 1f, 1f, 0.2f),
                },
                Close = new Style(ChaosStyle.Close)
                {
                    FontSize = 16
                },
                Toggle = new Style(ChaosStyle.Toggle)
                {
                    ImageColor = new Color(Configuration.UI.Colors.Highlight.Hex, Configuration.UI.Colors.Highlight.Alpha)
                }
            };

            m_OutlineClose = new OutlineComponent(new Color(Configuration.UI.Colors.Close.Hex, Configuration.UI.Colors.Close.Alpha));
            m_OutlineHighlight = new OutlineComponent(new Color(Configuration.UI.Colors.Highlight.Hex, Configuration.UI.Colors.Highlight.Alpha));
            
            if (ImageLibrary.IsLoaded)
            {
                ImageLibrary.AddImage("https://chaoscode.io/oxide/Images/magnifyingglass.png", "teleportgui.search", 0UL, () =>
                {
                    m_MagnifyImage = ImageLibrary.GetImage("teleportgui.search");
                });
            }
        }
        
        #region Teleport
        private void ShowTeleportUI(BasePlayer player, Mode mode)
        {
            TeleportData.User userData = GetOrCreateUserSettings(player);

            BaseContainer root = ChaosPrefab.Background(TPUI, Layer.Hud, Anchor.Center, new Offset(-225f, -265f, 225f, 265f), m_StylePreset)
                .WithChildren(parent =>
                {
                    CreateModeSelector(player, parent, mode);
                    
                    CreateTitleBar(player, userData, parent);

                    if (mode == Mode.Teleport)
                    {
                        List<BasePlayer> list = BuildPlayerList(player, userData);
                        
                        CreateHeaderBar(player, userData, parent, m_GridLayout.HasNextPage(userData.Page, list.Count), mode);
                        CreateGridLayout(player, userData, parent, list, mode, 
                            (layout, anchor, offset, t) => CreatePlayerEntry(player, userData, layout, anchor, offset, t));
                        
                        Pool.FreeUnmanaged(ref list);
                    }
                    
                    if (mode == Mode.Home)
                    {
                        List<KeyValuePair<string, TeleportData.User.HomePoint>> list = BuildHomeList(userData);
                        
                        CreateHeaderBar(player, userData, parent, m_GridLayout.HasNextPage(userData.Page, list.Count), mode);
                        CreateGridLayout(player, userData, parent, list, mode, 
                            (layout, anchor, offset, t) => CreateHomeEntry(player, userData, layout, anchor, offset, t));
                        
                        Pool.FreeUnmanaged(ref list);
                    }
                    
                    if (mode == Mode.Warp)
                    {
                        List<KeyValuePair<string, IWarpPoint>> list = BuildWarpList(player, userData);
                        
                        CreateHeaderBar(player, userData, parent, m_GridLayout.HasNextPage(userData.Page, list.Count), mode);
                        CreateGridLayout(player, userData, parent, list, mode, 
                            (layout, anchor, offset, t) => CreateWarpEntry(player, userData, layout, anchor, offset, t));
                        
                        Pool.FreeUnmanaged(ref list);
                    }
                })
                .NeedsCursor()
                .NeedsKeyboard()
                .DestroyExisting();

            ChaosUI.Show(player, root);
        }

        private void CreateTitleBar(BasePlayer player, TeleportData.User userData, BaseContainer parent)
        {
            ChaosPrefab.Panel(parent, Anchor.TopStretch, new Offset(5f, -35f, -5f, -5f))
                .WithChildren(titleBar =>
                {
                    ChaosPrefab.Title(titleBar, Anchor.CenterLeft, new Offset(5f, -15f, 205f, 15f), GetString("UI.Title", player))
                        .WithOutline(ChaosStyle.BlackOutline);

                    ChaosPrefab.CloseButton(titleBar, Anchor.CenterRight, new Offset(-25f, -10f, -5f, 10f), m_OutlineClose)
                        .WithCallback(m_CallbackHandler, arg =>
                        {
                            userData.OnClose();
                            ChaosUI.Destroy(player, TPUI);
                        }, $"{player.UserIDString}.menu.exit");
                });
        }

        private void CreateModeSelector(BasePlayer player, BaseContainer parent, Mode mode)
        {
            ImageContainer.Create(parent, Anchor.TopCenter, new Offset(-150f, 0f, 150f, 25f))
                .WithStyle(m_StylePreset.Background)
                .WithSprite(Sprites.Background_Rounded_top)
                .WithChildren(modeSelector =>
                {
                    bool canTeleport = player.HasPermission(PERMISSION_TP_USE);
                    bool canHome = player.HasPermission(PERMISSION_HOME_USE);
                    bool canWarp = player.HasPermission(PERMISSION_WARP_USE);
                    
                    ImageContainer.Create(modeSelector, Anchor.Center, new Offset(-145f, -12.5f, -51.66666f, 7.5f))
                        .WithStyle(mode == Mode.Teleport ? m_StylePreset.Header : (canTeleport ? m_StylePreset.Button : m_StylePreset.DisabledButton))
                        .WithSprite(Sprites.Background_Rounded)
                        .WithChildren(button =>
                        {
                            TextContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("UI.Teleport", player))
                                .WithStyle(canTeleport ? m_StylePreset.Button : m_StylePreset.DisabledButton);

                            if (canTeleport)
                            {
                                ButtonContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg => ShowTeleportUI(player, Mode.Teleport), $"{player.UserIDString}.mode.teleport");
                            }
                        });

                    ImageContainer.Create(modeSelector, Anchor.Center, new Offset(-46.66666f, -12.5f, 46.66667f, 7.5f))
                        .WithStyle(mode == Mode.Home ? m_StylePreset.Header : (canHome ? m_StylePreset.Button : m_StylePreset.DisabledButton))
                        .WithSprite(Sprites.Background_Rounded)
                        .WithChildren(button =>
                        {
                            TextContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("UI.Homes", player))
                                .WithStyle(canHome ? m_StylePreset.Button : m_StylePreset.DisabledButton);

                            if (canHome)
                            {
                                ButtonContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg => ShowTeleportUI(player, Mode.Home), $"{player.UserIDString}.mode.home");
                            }
                        });

                    ImageContainer.Create(modeSelector, Anchor.Center, new Offset(51.66667f, -12.5f, 145f, 7.5f))
                        .WithStyle(mode == Mode.Warp ? m_StylePreset.Header : (canWarp ? m_StylePreset.Button : m_StylePreset.DisabledButton))
                        .WithSprite(Sprites.Background_Rounded)
                        .WithChildren(button =>
                        {
                            TextContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("UI.Warps", player))
                                .WithStyle(canWarp ? m_StylePreset.Button : m_StylePreset.DisabledButton);

                            if (canWarp)
                            {
                                ButtonContainer.Create(button, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg => ShowTeleportUI(player, Mode.Warp), $"{player.UserIDString}.mode.warp");
                            }
                        });
                });
        }

        private void CreateHeaderBar(BasePlayer player, TeleportData.User userData, BaseContainer parent, bool hasNextPage, Mode mode)
        {
            ChaosPrefab.Panel(parent, Anchor.TopStretch, new Offset(5f, -70f, -5f, -40f))
                .WithChildren(header =>
                {
                    // Pagination
                    ChaosPrefab.PreviousPage(header, Anchor.CenterLeft, new Offset(5f, -10f, 35f, 10f), userData.Page > 0)?
                        .WithCallback(m_CallbackHandler, arg =>
                        {
                            userData.Page -= 1;
                            ShowTeleportUI(player, mode);
                        }, $"{player.UserIDString}.back");

                    ChaosPrefab.NextPage(header, Anchor.CenterRight, new Offset(-35f, -10f, -5f, 10f), hasNextPage)?
                        .WithCallback(m_CallbackHandler, arg =>
                        {
                            userData.Page += 1;
                            ShowTeleportUI(player, mode);
                        }, $"{player.UserIDString}.next");

                    // Search Input
                    ChaosPrefab.Input(header, Anchor.CenterRight, new Offset(-240f, -10f, -40f, 10f), userData.SearchString)
                        .WithCallback(m_CallbackHandler, arg =>
                            {
                                userData.SearchString = arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty;
                                ShowTeleportUI(player, mode);
                            }, $"{player.UserIDString}.search");

                    if (!string.IsNullOrEmpty(m_MagnifyImage))
                    {
                        RawImageContainer.Create(header, Anchor.CenterRight, new Offset(-265f, -10f, -245f, 10f))
                            .WithPNG(m_MagnifyImage);
                    }

                    if (mode == Mode.Teleport)
                    {
                        if (player.HasPermission(PERMISSION_TP_AUTOACCEPT) || player.HasPermission(PERMISSION_TP_SLEEPERS))
                        {
                            ChaosPrefab.SpriteButton(header, Anchor.CenterLeft, new Offset(40f, -10f, 60f, 10f),
                                    Icon.Gear, Anchor.FullStretch, new Offset(2f, 2f, -2f, -2f))
                                .WithCallback(m_CallbackHandler, arg => ShowTeleportSettingsUI(player, userData, mode), $"{player.UserIDString}.settings");
                            /*ImageContainer.Create(header, Anchor.CenterLeft, new Offset(40f, -10f, 60f, 10f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(settings =>
                                {
                                    ImageContainer.Create(settings, Anchor.FullStretch, new Offset(2f, 2f, -2f, -2f))
                                        .WithSprite(Icon.Gear);

                                    ButtonContainer.Create(settings, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg => ShowTeleportSettingsUI(player, userData, mode), $"{player.UserIDString}.settings");
                                });*/
                        }
                    }

                    if (mode == Mode.Home)
                    {
                        int maxHomes = GetMaxHomesForPlayer(player);
                        bool canSetHome = player.HasPermission(PERMISSION_HOME_USE) && (maxHomes == 0 || userData.Homes.Count < maxHomes);
                        
                        ImageContainer.Create(header, Anchor.CenterLeft, new Offset(40f, -10f, 60f, 10f))
                            .WithStyle(canSetHome ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                            .WithChildren(settings =>
                            {
                                ImageContainer.Create(settings, Anchor.FullStretch, new Offset(2f, 2f, -2f, -2f))
                                    .WithSprite(Icon.Add)
                                    .WithColor(canSetHome ? Color.White : m_StylePreset.DisabledButton.FontColor);

                                if (canSetHome)
                                {
                                    ButtonContainer.Create(settings, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg => SaveHomeUI(player), $"{player.UserIDString}.addhome");
                                }
                            });
                    }
                    
                    if (mode == Mode.Warp && player.HasPermission(PERMISSION_WARP_ADMIN))
                    {
                        ImageContainer.Create(header, Anchor.CenterLeft, new Offset(40f, -10f, 60f, 10f))
                            .WithStyle(m_StylePreset.Button)
                            .WithChildren(settings =>
                            {
                                ImageContainer.Create(settings, Anchor.FullStretch, new Offset(2f, 2f, -2f, -2f))
                                    .WithSprite(Icon.Add);

                                ButtonContainer.Create(settings, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg => SaveWarpUI(player), $"{player.UserIDString}.addwarp");
                            });
                    }
                });
        }

        private void CreateGridLayout<T>(BasePlayer player, TeleportData.User userData, BaseContainer parent, List<T> list, Mode mode, Action<BaseContainer, Anchor, Offset, T> createAction)
        {
            ChaosPrefab.Panel(parent, Anchor.FullStretch, new Offset(5f, 5f, -5f, -75f))
                .WithChildren(grid =>
                {
                    ConfigData.LimitOptions limits = mode == Mode.Warp ? Configuration.Warp.Limits :
                                                     mode == Mode.Home ? Configuration.Home.Limits :
                                                     Configuration.TP.Limits;
                    
                    ConfigData.PurchaseOptions purchase = mode == Mode.Warp ? Configuration.Warp.Purchase :
                                                          mode == Mode.Home ? Configuration.Home.Purchase :
                                                          Configuration.TP.Purchase;
                    
                    TeleportData.User.Usage usage = mode == Mode.Warp ? userData.WarpUsage :
                                                    mode == Mode.Home ? userData.HomeUsage :
                                                    userData.TPUsage;
                    
                    bool isOnCooldown = usage.IsOnCooldown();
                    bool notRestrictedByLimit = true;
                    bool _;
                    bool hasPendingRequest = TeleportRequest.HasPendingRequest(player) || PlayerTeleporter.IsWaiting(player) || PositionTeleporter.IsWaiting(player, out _);
                    bool canTeleport = !isOnCooldown && !hasPendingRequest && notRestrictedByLimit;
                    
                    ImageContainer.Create(grid, Anchor.TopStretch, new Offset(0f, -20f, 0f, 0f))
                        .WithStyle(m_StylePreset.Header)
                        .WithChildren(header =>
                        {
                            if (limits.Default > 0 || purchase.PayAlways)
                            {
                                if (HasReachedDailyLimit(player, userData, mode) || purchase.PayAlways)
                                {
                                    if (purchase.PayAfterUsingDailyLimits || purchase.PayAlways)
                                    {
                                        TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                            .WithText(FormatString("UI.CostToTP", player, purchase.GetLowestOption(player), GetString($"PurchaseMode.{purchase.Mode}", player)))
                                            .WithAlignment(TextAnchor.MiddleLeft);
                                    }
                                    else
                                    {
                                        notRestrictedByLimit = false;
                                        TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                            .WithText(GetString("UI.TPLimit", player))
                                            .WithAlignment(TextAnchor.MiddleLeft);
                                    }
                                }
                                else
                                {
                                    int limit = limits.GetHighestOption(player);
                                    
                                    TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                        .WithText(FormatString("UI.DailyLimitRemain", player, limit == 0 ? GetString("UI.Unlimited2", player) : limit - usage.UsesToday))
                                        .WithAlignment(TextAnchor.MiddleLeft);
                                    
                                }
                            }

                            if (isOnCooldown)
                            {
                                TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                    .WithText(GetString("UI.CooldownRemain", player))
                                    .WithAlignment(TextAnchor.MiddleRight)
                                    .WithCountdown(new CountdownComponent((int)(usage.Cooldown - CurrentTime())));
                                
                                // Temp solution for broken countdown component
                                /*string guid = CuiHelper.GetGuid();
                                int time = (int)(usage.Cooldown - CurrentTime());
                                
                                TextContainer.Create(header, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                    .WithText(GetString("UI.CooldownRemain", player).Replace("%TIME_LEFT%", time.ToString()))
                                    .WithAlignment(TextAnchor.MiddleRight)
                                    .WithName(guid);
                                    
                                ServerCountdown.Add(player, guid, "UI.CooldownRemain", time);*/
                            }
                        });

                    if (list.Count == 0)
                    {
                        TextContainer.Create(grid, Anchor.FullStretch, Offset.zero)
                            .WithText(GetString(mode == Mode.Home ? "UI.NoHomes" : mode == Mode.Warp ? "UI.NoWarps" : "UI.NoPlayers", player))
                            .WithAlignment(TextAnchor.MiddleCenter);
                    }
                    BaseContainer.Create(grid, Anchor.FullStretch, new Offset(0f, 0f, 0f, -20f))
                        .WithLayoutGroup(m_GridLayout, list, userData.Page, (i, t, layout, anchor, offset) =>
                        {
                            createAction(layout, anchor, offset, t);
                        });
                });
        }

        private void CreatePlayerEntry(BasePlayer player, TeleportData.User userData, BaseContainer layout, Anchor anchor, Offset offset, BasePlayer t)
        {
            bool isOnCooldown = userData.TPUsage.IsOnCooldown();
            bool hasPendingRequest = TeleportRequest.HasPendingRequest(player) || PlayerTeleporter.IsWaiting(player) || PositionTeleporter.IsWaiting(player, out bool _);
            bool canTeleport = !isOnCooldown && !hasPendingRequest;

            bool canTeleportToPlayer = canTeleport && (t.IsConnected || (IsAdmin(player) && Configuration.Admin.Instant));

            ChaosPrefab.Panel(layout, anchor, offset)
                .WithChildren(template =>
                {
                    TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, -85f, 0f))
                        .WithText(t.displayName.StripTags())
                        .WithAlignment(TextAnchor.MiddleLeft);

                    ImageContainer.Create(template, Anchor.RightStretch, new Offset(-41f, 1f, -1f, -1f))
                        .WithStyle(canTeleportToPlayer ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                        .WithChildren(tpto =>
                        {
                            TextContainer.Create(tpto, Anchor.FullStretch, Offset.zero)
                                .WithStyle(canTeleportToPlayer ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                .WithSize(12)
                                .WithText(GetString("UI.TPR", player))
                                .WithAlignment(TextAnchor.MiddleCenter);

                            if (canTeleportToPlayer)
                            {
                                ButtonContainer.Create(tpto, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        userData.OnClose();
                                        ChaosUI.Destroy(player, TPUI);
                                        TPR(player, t, false);
                                    }, $"{player.UserIDString}.tpr.{t.UserIDString}");
                            }

                        });

                    if (player.HasPermission(PERMISSION_TP_HERE))
                    {
                        ImageContainer.Create(template, Anchor.RightStretch, new Offset(-86f, 1f, -46f, -1f))
                            .WithStyle(canTeleportToPlayer ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                            .WithChildren(tphere =>
                            {
                                TextContainer.Create(tphere, Anchor.FullStretch, Offset.zero)
                                    .WithStyle(canTeleportToPlayer ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                    .WithSize(12)
                                    .WithText(GetString("UI.TPHere", player))
                                    .WithAlignment(TextAnchor.MiddleCenter);

                                if (canTeleportToPlayer)
                                {
                                    ButtonContainer.Create(tphere, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            userData.OnClose();
                                            ChaosUI.Destroy(player, TPUI);
                                            TPR(player, t, true);
                                        }, $"{player.UserIDString}.tphere.{t.UserIDString}");
                                }
                            });
                    }
                });
        }
        
        private void CreateHomeEntry(BasePlayer player, TeleportData.User userData, BaseContainer layout, Anchor anchor, Offset offset, KeyValuePair<string, TeleportData.User.HomePoint> t)
        {
            ButtonContainer.Create(layout, anchor, offset)
                .WithCallback(m_CallbackHandler, arg =>
                {
                    bool isOnCooldown = userData.HomeUsage.IsOnCooldown();
                    bool hasPendingRequest = TeleportRequest.HasPendingRequest(player) || PlayerTeleporter.IsWaiting(player) || PositionTeleporter.IsWaiting(player, out bool _);
                    
                    if (!isOnCooldown && !hasPendingRequest)
                    {
                        if (!IsHomePointValid(t.Value))
                        {
                            SendReply(player, "Home.Error.Invalid", t.Key);

                            userData.Homes.Remove(t.Key);
                            ShowTeleportUI(player, Mode.Home);
                            return;
                        }
                        
                        if (CanTeleportHome(player, t.Value.Position, out bool playerIsPaying))
                        {
                            PositionTeleporter.Create(player, t.Value.Position, t.Key, Configuration.Home.Delay.GetLowestOption(player), playerIsPaying, true);
                            ChaosUI.Destroy(player, TPUI);
                        }
                    }
                }, $"{player.UserIDString}.home.{t.Key}")
                .WithStyle(m_StylePreset.Panel)
                .WithChildren(template =>
                {
                    TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                        .WithText(t.Key)
                        .WithAlignment(TextAnchor.MiddleCenter);

                    ImageContainer.Create(template, Anchor.RightStretch, new Offset(-20f, 2f, -2f, -2f))
                        .WithStyle(m_StylePreset.Button)
                        .WithOutline(m_OutlineClose)
                        .WithChildren(delete =>
                        {
                            ImageContainer.Create(delete, Anchor.FullStretch, new Offset(2f, 2f, -2f, -2f))
                                .WithSprite(Icon.Clear);
                            
                            ButtonContainer.Create(delete, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    userData.Homes.Remove(t.Key);
                                    SendReply(player, "Home.Success.Deleted", t.Key);
                                    ShowTeleportUI(player, Mode.Home);
                                }, $"{player.UserIDString}.deletehome.{t.Key}");
                        });
                });
        }
        
        private void CreateWarpEntry(BasePlayer player, TeleportData.User userData, BaseContainer layout, Anchor anchor, Offset offset, KeyValuePair<string, IWarpPoint> t)
        {
            bool canWarp = t.Value.HasPermission(player);
            
            ButtonContainer.Create(layout, anchor, offset)
                .WithCallback(m_CallbackHandler, arg =>
                {
                    if (!canWarp)
                    {
                        SendReply(player, "Warp.Error.NoPermission");
                        return;
                    }
                    
                    bool isOnCooldown = userData.WarpUsage.IsOnCooldown();
                    bool hasPendingRequest = TeleportRequest.HasPendingRequest(player) || PlayerTeleporter.IsWaiting(player) || PositionTeleporter.IsWaiting(player, out bool _);
                    bool canTeleport = !isOnCooldown && !hasPendingRequest;
                    
                    if (canTeleport)
                    {
                        Vector3 position = t.Value.GetPosition();
                        
                        if (CanWarpTo(player, position, out bool playerIsPaying))
                        {
                            PositionTeleporter.Create(player, position, t.Key, Configuration.Warp.Delay.GetLowestOption(player), playerIsPaying, false);
                            ChaosUI.Destroy(player, TPUI);
                        }
                    }
                }, $"{player.UserIDString}.warp.{t.Key}")
                .WithStyle(m_StylePreset.Panel)
                .WithChildren(template =>
                {
                    TextContainer.Create(template, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                        .WithText(t.Key)
                        .WithAlignment(TextAnchor.MiddleCenter)
                        .WithColor(canWarp ? Color.White : m_StylePreset.DisabledButton.FontColor);

                    if (player.HasPermission(PERMISSION_WARP_ADMIN) && t.Value is WarpPoint)
                    {
                        ImageContainer.Create(template, Anchor.RightStretch, new Offset(-20f, 2f, -2f, -2f))
                            .WithStyle(m_StylePreset.Button)
                            .WithOutline(m_OutlineClose)
                            .WithChildren(delete =>
                            {
                                ImageContainer.Create(delete, Anchor.FullStretch, new Offset(2f, 2f, -2f, -2f))
                                    .WithSprite(Icon.Clear);
                            
                                ButtonContainer.Create(delete, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        m_WarpData.Data.Remove(t.Key);
                                        m_WarpData.Save();
                                        SendReply(player, "WarpRemove.Success", t.Key);
                                        ShowTeleportUI(player, Mode.Warp);
                                    }, $"{player.UserIDString}.deletewarp.{t.Key}");
                            });
                    }
                });
        }

        private List<BasePlayer> BuildPlayerList(BasePlayer player, TeleportData.User userData)
        {
            List<BasePlayer> list = Pool.Get<List<BasePlayer>>();
            list.AddRange(BasePlayer.activePlayerList);
                 
            if (userData.ShowSleepers)
                list.AddRange(BasePlayer.sleepingPlayerList);

            if (Configuration.UI.HideAdminsInUI && !IsAdmin(player))
                list.RemoveAll(IsAdmin);

            list.RemoveAll(x => x.HasPermission(PERMISSION_HIDE_UI));

            list.Remove(player);
            
            if (!player.HasPermission(PERMISSION_SEE_ALL) && Configuration.TP.FriendliesOnly)
            {
                List<ulong> friendlies = Pool.Get<List<ulong>>();
                if (Clans.IsLoaded)
                {
                    List<string> clanMembers = Clans.GetClanMembers(player.userID);
                    if (clanMembers != null)
                    {
                        foreach (string s in clanMembers)
                        {
                            if (ulong.TryParse(s, out ulong id))
                                friendlies.Add(id);
                        }
                    }
                }

                if (Friends.IsLoaded)
                {
                    if (Configuration.TP.MutualFriendsOnly)
                    {
                        ulong[] friends = Friends.GetFriends(player.userID);
                        if (friends != null)
                        {
                            foreach (ulong id in friends)
                            {
                                if (Friends.HasFriend(id, player.userID))
                                    friendlies.Add(id);
                            }
                        }
                    }
                    else friendlies.AddRange(Friends.GetFriends(player.userID));
                }
                
                if (player.Team != null)
                    friendlies.AddRange(player.Team.members);

                list.RemoveAll(x => !friendlies.Contains(x.userID));
            }

            if (!string.IsNullOrEmpty(userData.SearchString))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    BasePlayer p = list[i];
                    if (!p.displayName.Contains(userData.SearchString, CompareOptions.OrdinalIgnoreCase))
                        list.RemoveAt(i);
                }
            }

            list.Sort((a,b) => a.displayName.CompareTo(b.displayName));
            return list;
        }
        
        private List<KeyValuePair<string, TeleportData.User.HomePoint>> BuildHomeList(TeleportData.User userData)
        {
            List<KeyValuePair<string, TeleportData.User.HomePoint>> list = Pool.Get<List<KeyValuePair<string, TeleportData.User.HomePoint>>>();
            list.AddRange(userData.Homes);
            
            if (!string.IsNullOrEmpty(userData.SearchString))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    KeyValuePair<string, TeleportData.User.HomePoint> p = list[i];
                    if (!p.Key.Contains(userData.SearchString, CompareOptions.OrdinalIgnoreCase))
                        list.RemoveAt(i);
                }
            }
            
            list.Sort((a,b) => a.Key.CompareTo(b.Key));
            return list;
        }

        private List<KeyValuePair<string, IWarpPoint>> BuildWarpList(BasePlayer player, TeleportData.User userData)
        {
            List<KeyValuePair<string, IWarpPoint>> list = Pool.Get<List<KeyValuePair<string, IWarpPoint>>>();

            foreach (KeyValuePair<string, MonumentWarpPoint> kvp in m_MonumentWarps)
                list.Add(new KeyValuePair<string, IWarpPoint>(m_GetString(kvp.Value.Shortname, player) + $" ({kvp.Value.MapGrid})", kvp.Value));
            
            foreach (KeyValuePair<string, WarpPoint> kvp in m_WarpData.Data)
                list.Add(new KeyValuePair<string, IWarpPoint>(kvp.Key, kvp.Value));
            
            if (Configuration.UI.HideWarpsNoPermission)
                list.RemoveAll(x => !x.Value.HasPermission(player));

            if (!string.IsNullOrEmpty(userData.SearchString))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    KeyValuePair<string, IWarpPoint> p = list[i];
                    if (!p.Key.Contains(userData.SearchString, CompareOptions.OrdinalIgnoreCase))
                        list.RemoveAt(i);
                }
            }
            
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            return list;
        }

        private void SaveHomeUI(BasePlayer player, string homeName = "")
        {
            BaseContainer root = ChaosPrefab.Background(TPUI, Layer.Overall, Anchor.FullStretch, Offset.zero, m_StylePreset)
                .WithChildren(parent =>
                {
                    ChaosPrefab.Panel(parent, Anchor.Center, new Offset(-150f, -27.5f, 150f, 27.5f))
                        .WithChildren(titleBar =>
                        {
                            TextContainer.Create(titleBar, Anchor.TopStretch, new Offset(5f, -25f, -5f, -5f))
                                .WithText(GetString("UI.HomeName", player))
                                .WithAlignment(TextAnchor.MiddleLeft);
                            
                            ChaosPrefab.Input(titleBar, Anchor.TopStretch, new Offset(80f, -25f, -5.000015f, -5f), homeName)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    SaveHomeUI(player, arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty);
                                }, $"{player.UserIDString}.homenameinput");

                            ChaosPrefab.TextButton(titleBar, Anchor.BottomStretch, new Offset(5f, 5f, -155f, 25f), GetString("UI.Save", player), null, m_OutlineHighlight)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    SetHomeCommand(player, "sethome", new[]{ homeName });
                                    ShowTeleportUI(player, Mode.Home);
                                }, $"{player.UserIDString}.addhome.save");

                            ChaosPrefab.TextButton(titleBar, Anchor.BottomStretch, new Offset(155f, 5f, -5f, 25f), GetString("UI.Cancel", player), null, m_OutlineClose)
                                .WithCallback(m_CallbackHandler, arg => { ShowTeleportUI(player, Mode.Home); }, $"{player.UserIDString}.addhome.cancel");
                        });

                    ChaosPrefab.Panel(parent, Anchor.Center, new Offset(-150f, 32.5f, 150f, 52.5f))
                        .WithChildren(infoBar =>
                        {
                            TextContainer.Create(infoBar, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("UI.CreateNewHome", player))
                                .WithAlignment(TextAnchor.MiddleCenter);
                        });
                })
                .DestroyExisting()
                .NeedsCursor()
                .NeedsKeyboard();

            ChaosUI.Show(player, root);
        }

        private void SaveWarpUI(BasePlayer player, string warpName = "", string perm = "")
        {
            BaseContainer root = ChaosPrefab.Background(TPUI, Layer.Overall, Anchor.FullStretch, Offset.zero, m_StylePreset)
                .WithChildren(parent =>
                {
                    ChaosPrefab.Panel(parent, Anchor.Center, new Offset(-150f, -40f, 150f, 40f))
                        .WithChildren(titleBar =>
                        {
                            TextContainer.Create(titleBar, Anchor.TopStretch, new Offset(5f, -25f, -5f, -5f))
                                .WithText(GetString("UI.WarpName", player))
                                .WithAlignment(TextAnchor.MiddleLeft);
                            
                            ChaosPrefab.Input(titleBar, Anchor.TopStretch, new Offset(80f, -25f, -5.000015f, -5f), warpName)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    SaveWarpUI(player, arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty, perm);
                                }, $"{player.UserIDString}.warpnameinput");

                            TextContainer.Create(titleBar, Anchor.TopStretch, new Offset(5f, -50f, -5f, -30f))
                                .WithText(GetString("UI.Permission", player))
                                .WithAlignment(TextAnchor.MiddleLeft);
                            
                            ImageContainer.Create(titleBar, Anchor.TopStretch, new Offset(80f, -50f, -5.000015f, -30f))
                                .WithStyle(m_StylePreset.Button)
                                .WithChildren(permissionInput =>
                                {
                                    TextContainer.Create(permissionInput, Anchor.FullStretch, new Offset(5f, 0f, 0f, 0f))
                                        .WithText("teleportgui.")
                                        .WithAlignment(TextAnchor.MiddleLeft);

                                    InputFieldContainer.Create(permissionInput, Anchor.FullStretch, new Offset(73f, 0f, 0f, 0f))
                                        .WithText(perm)
                                        .WithAlignment(TextAnchor.MiddleLeft)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            SaveWarpUI(player, warpName, arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty);
                                        }, $"{player.UserIDString}.warppermissioninput");
                                });

                            ChaosPrefab.TextButton(titleBar, Anchor.BottomStretch, new Offset(5f, 5f, -155f, 25f), GetString("UI.Save", player), null, m_OutlineHighlight)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    WarpAddCommand(player, "warpadd", new[] {warpName, perm});
                                    ShowTeleportUI(player, Mode.Warp);
                                }, $"{player.UserIDString}.addwarp.save");

                            ChaosPrefab.TextButton(titleBar, Anchor.BottomStretch, new Offset(155f, 5f, -5f, 25f), GetString("UI.Cancel", player), null, m_OutlineClose)
                                .WithCallback(m_CallbackHandler, arg => ShowTeleportUI(player, Mode.Warp), $"{player.UserIDString}.addwarp.cancel");
                        });

                    ChaosPrefab.Panel(parent, Anchor.Center, new Offset(-150f, 45f, 150f, 65f))
                        .WithChildren(infoBar =>
                        {
                            TextContainer.Create(infoBar, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("UI.CreateNewWarp", player))
                                .WithAlignment(TextAnchor.MiddleCenter);
                        });
                })
                .DestroyExisting()
                .NeedsCursor()
                .NeedsKeyboard();

            ChaosUI.Show(player, root);
        }
        
        private void ShowTeleportSettingsUI(BasePlayer player, TeleportData.User userData, Mode mode)
        {
            BaseContainer root = ChaosPrefab.Background(TPUI, Layer.Overall, Anchor.FullStretch, Offset.zero, m_StylePreset)
                .WithChildren(parent =>
                {
                    ChaosPrefab.Panel(parent, Anchor.Center, new Offset(-100f, -77.5f, 100f, 77.5f))
                        .WithChildren(titleBar =>
                        {
                            ChaosPrefab.TextButton(titleBar, Anchor.BottomStretch, new Offset(5f, 5f, -5f, 25f), GetString("UI.Close", player), null, m_OutlineClose)
                                .WithCallback(m_CallbackHandler, arg => ShowTeleportUI(player, mode), $"{player.UserIDString}.settings.close");

                            bool canToggleAutoAccept = player.HasPermission(PERMISSION_TP_AUTOACCEPT);
                            
                            ImageContainer.Create(titleBar, Anchor.TopLeft, new Offset(5f, -25f, 25f, -5f))
                                .WithStyle(m_StylePreset.Button)
                                .WithChildren(autoaccept =>
                                {
                                    bool isActive = (userData.AutoAccept & TeleportData.User.AutoAcceptEnum.Clans) != 0;
                                    if (isActive)
                                    {
                                        ImageContainer.Create(autoaccept, Anchor.FullStretch, new Offset(5f, 5f, -5f, -5f))
                                            .WithStyle(m_StylePreset.Toggle);
                                    }

                                    if (canToggleAutoAccept)
                                    {
                                        ButtonContainer.Create(autoaccept, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (isActive)
                                                    userData.AutoAccept &= ~TeleportData.User.AutoAcceptEnum.Clans;
                                                else userData.AutoAccept |= TeleportData.User.AutoAcceptEnum.Clans;

                                                ShowTeleportSettingsUI(player, userData, mode);
                                            }, $"{player.UserIDString}.settings.aaclan");
                                    }

                                    TextContainer.Create(autoaccept, Anchor.HoriztonalCenterStretch, new Offset(25f, -10f, 170f, 10f))
                                        .WithText(GetString("UI.AA.Clan", player))
                                        .WithStyle(canToggleAutoAccept ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                        .WithAlignment(TextAnchor.MiddleLeft);
                                });

                            ImageContainer.Create(titleBar, Anchor.TopLeft, new Offset(5f, -50f, 25f, -30f))
                                .WithStyle(canToggleAutoAccept ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                .WithChildren(autoaccept =>
                                {
                                    bool isActive = (userData.AutoAccept & TeleportData.User.AutoAcceptEnum.Friends) != 0;
                                    if (isActive)
                                    {
                                        ImageContainer.Create(autoaccept, Anchor.FullStretch, new Offset(5f, 5f, -5f, -5f))
                                            .WithStyle(m_StylePreset.Toggle);
                                    }

                                    if (canToggleAutoAccept)
                                    {
                                        ButtonContainer.Create(autoaccept, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (isActive)
                                                    userData.AutoAccept &= ~TeleportData.User.AutoAcceptEnum.Friends;
                                                else userData.AutoAccept |= TeleportData.User.AutoAcceptEnum.Friends;

                                                ShowTeleportSettingsUI(player, userData, mode);
                                            }, $"{player.UserIDString}.settings.aafriend");
                                    }

                                    TextContainer.Create(autoaccept, Anchor.HoriztonalCenterStretch, new Offset(25f, -10f, 170f, 10f))
                                        .WithText(GetString("UI.AA.Friend", player))
                                        .WithStyle(canToggleAutoAccept ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                        .WithAlignment(TextAnchor.MiddleLeft);
                                });

                            ImageContainer.Create(titleBar, Anchor.TopLeft, new Offset(5f, -75f, 25f, -55f))
                                .WithStyle(canToggleAutoAccept ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                .WithChildren(autoaccept =>
                                {
                                    bool isActive = (userData.AutoAccept & TeleportData.User.AutoAcceptEnum.Teams) != 0;
                                    if (isActive)
                                    {
                                        ImageContainer.Create(autoaccept, Anchor.FullStretch, new Offset(5f, 5f, -5f, -5f))
                                            .WithStyle(m_StylePreset.Toggle);
                                    }

                                    if (canToggleAutoAccept)
                                    {
                                        ButtonContainer.Create(autoaccept, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (isActive)
                                                    userData.AutoAccept &= ~TeleportData.User.AutoAcceptEnum.Teams;
                                                else userData.AutoAccept |= TeleportData.User.AutoAcceptEnum.Teams;

                                                ShowTeleportSettingsUI(player, userData, mode);
                                            }, $"{player.UserIDString}.settings.aateam");
                                    }

                                    TextContainer.Create(autoaccept, Anchor.HoriztonalCenterStretch, new Offset(25f, -10f, 170f, 10f))
                                        .WithText(GetString("UI.AA.Team", player))
                                        .WithStyle(canToggleAutoAccept ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                        .WithAlignment(TextAnchor.MiddleLeft);
                                });

                            ImageContainer.Create(titleBar, Anchor.TopLeft, new Offset(5f, -100f, 25f, -80f))
                                .WithStyle(canToggleAutoAccept ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                .WithChildren(autoaccept =>
                                {
                                    bool isActive = (userData.AutoAccept & TeleportData.User.AutoAcceptEnum.All) != 0;
                                    if (isActive)
                                    {
                                        ImageContainer.Create(autoaccept, Anchor.FullStretch, new Offset(5f, 5f, -5f, -5f))
                                            .WithStyle(m_StylePreset.Toggle);
                                    }

                                    if (canToggleAutoAccept)
                                    {
                                        ButtonContainer.Create(autoaccept, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (isActive)
                                                    userData.AutoAccept &= ~TeleportData.User.AutoAcceptEnum.All;
                                                else userData.AutoAccept |= TeleportData.User.AutoAcceptEnum.All;

                                                ShowTeleportSettingsUI(player, userData, mode);
                                            }, $"{player.UserIDString}.settings.aaall");
                                    }

                                    TextContainer.Create(autoaccept, Anchor.HoriztonalCenterStretch, new Offset(25f, -10f, 170f, 10f))
                                        .WithText(GetString("UI.AA.All", player))
                                        .WithStyle(canToggleAutoAccept ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                        .WithAlignment(TextAnchor.MiddleLeft);
                                });

                            bool canToggleSleepers = player.HasPermission(PERMISSION_TP_SLEEPERS);
                            
                            ImageContainer.Create(titleBar, Anchor.TopLeft, new Offset(5f, -125f, 25f, -105f))
                                .WithStyle(canToggleSleepers ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                .WithChildren(showSleepers =>
                                {
                                    if (userData.ShowSleepers)
                                    {
                                        ImageContainer.Create(showSleepers, Anchor.FullStretch, new Offset(5f, 5f, -5f, -5f))
                                            .WithStyle(m_StylePreset.Toggle);
                                    }

                                    if (canToggleSleepers)
                                    {
                                        ButtonContainer.Create(showSleepers, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                userData.ShowSleepers = !userData.ShowSleepers;
                                                ShowTeleportSettingsUI(player, userData, mode);
                                            }, $"{player.UserIDString}.settings.sleepers");
                                    }

                                    TextContainer.Create(showSleepers, Anchor.HoriztonalCenterStretch, new Offset(25f, -10f, 170f, 10f))
                                        .WithText(GetString("UI.ShowSleepers", player))
                                        .WithStyle(canToggleSleepers ? m_StylePreset.Button : m_StylePreset.DisabledButton)
                                        .WithAlignment(TextAnchor.MiddleLeft);
                                });
                        });

                    ChaosPrefab.Panel(parent, Anchor.Center, new Offset(-100f, 82.5f, 100f, 102.5f))
                        .WithChildren(infoBar =>
                        {
                            TextContainer.Create(infoBar, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("UI.TeleportSettings", player))
                                .WithAlignment(TextAnchor.MiddleCenter);
                        });
                })
                .DestroyExisting()
                .NeedsCursor()
                .NeedsKeyboard();


            ChaosUI.Show(player, root);
        }
        #endregion

        #region Popups
        private void CreateTeleportRequestPopup(BasePlayer player, string panel, ITeleport teleport, string key, string displayName, bool isReceiver)
        {
            ConfigData.UIOptions.RequestPopup.HorizontalPadding padding = Configuration.UI.Request.Padding;
            
            BaseContainer root = ImageContainer.Create(panel, Layer.Hud, Configuration.UI.Request.Anchor, Configuration.UI.Request.Offset)
                .WithStyle(m_StylePreset.Background)
                .WithFadeIn(0.25f)
                .WithFadeOut(0.25f)
                .WithChildren(parent =>
                {
                    ImageContainer.Create(parent, Anchor.FullStretch, new Offset(5f, 5f, -5f, -5f))
                        .WithStyle(m_StylePreset.Panel)
                        .WithChildren(contents =>
                        {
                            ImageContainer.Create(contents, Anchor.TopStretch, new Offset(0f, -15f, 0f, 0f))
                                .WithStyle(m_StylePreset.Header)
                                .WithChildren(header =>
                                {
                                    TextContainer.Create(header, Anchor.FullStretch, new Offset(padding.Left, 0f, -padding.Right, 0f))
                                        .WithSize(12)
                                        .WithText(GetString(key, player))
                                        .WithAlignment(TextAnchor.MiddleCenter)
                                        .WithCountdown(new CountdownComponent(teleport.TimeRemaining));
                                });

                            TextContainer.Create(contents, Anchor.FullStretch, new Offset(5f + padding.Left, 0f, -40f - padding.Right, -15f))
                                .WithText(displayName)
                                .WithSize(12)
                                .WithAlignment(TextAnchor.MiddleLeft);

                            if (isReceiver && teleport.CanAccept)
                            {
                                ImageContainer.Create(contents, Anchor.CenterRight, new Offset(-37.5f - padding.Right, -15f, -22.5f - padding.Right, 0f))
                                    .WithStyle(m_StylePreset.Button)
                                    .WithOutline(m_OutlineHighlight)
                                    .WithChildren(accept =>
                                    {
                                        TextContainer.Create(accept, Anchor.FullStretch, Offset.zero)
                                            .WithSize(10)
                                            .WithText("✔")
                                            .WithAlignment(TextAnchor.MiddleCenter)
                                            .WithWrapMode(VerticalWrapMode.Overflow);

                                        ButtonContainer.Create(accept, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (m_PopupTimers.TryGetValue(player.userID, out Timer t))
                                                    t?.Destroy();
                                                
                                                ChaosUI.Destroy(player, panel);
                                                
                                                if (teleport.IsValid)
                                                    teleport.Accept();
                                            }, $"{player.UserIDString}.tprpopup.accept");
                                    });
                            }

                            if (teleport.CanCancel(player))
                            {
                                ImageContainer.Create(contents, Anchor.CenterRight, new Offset(-17.5f - padding.Right, -15f, -2.5f - padding.Right, 0f))
                                    .WithStyle(m_StylePreset.Button)
                                    .WithOutline(m_OutlineClose)
                                    .WithChildren(decline =>
                                    {
                                        TextContainer.Create(decline, Anchor.FullStretch, Offset.zero)
                                            .WithSize(12)
                                            .WithText("✘")
                                            .WithAlignment(TextAnchor.MiddleCenter)
                                            .WithWrapMode(VerticalWrapMode.Overflow);

                                        ButtonContainer.Create(decline, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                if (m_PopupTimers.TryGetValue(player.userID, out Timer t))
                                                    t?.Destroy();

                                                ChaosUI.Destroy(player, panel);
                                                
                                                if (teleport.IsValid)
                                                {
                                                    if (isReceiver)
                                                        teleport.Decline();
                                                    else teleport.Cancel();
                                                }
                                            }, $"{player.UserIDString}.tprpopup.decline");
                                    });
                            }
                        });
                })
                .DestroyExisting();
            
            if (m_PopupTimers.TryGetValue(player.userID, out Timer _t))
                _t?.Destroy();

            m_PopupTimers[player.userID] = timer.Once(teleport.TimeRemaining, () => ChaosUI.Destroy(player, panel));

            ChaosUI.Show(player, root);
        }
        #endregion
        #endregion
        
        #region Config        
        private static ConfigData Configuration;
        
        [JsonConverter(typeof(StringEnumConverter))]
        public enum PurchaseMode {Economics, ServerRewards, Scrap}
        
        protected class ConfigData : BaseConfigData
        {
            [JsonProperty("Chat options")]
            public ChatOptions Chat { get; set; }

            [JsonProperty("Teleport options")]
            public TeleportOptions TP { get; set; }
            
            [JsonProperty("Home options")]
            public HomeOptions Home { get; set; }
            
            [JsonProperty("Warp options")]
            public WarpOptions Warp { get; set; }
            
            [JsonProperty("Teleport conditions")]
            public TeleportConditions Conditions { get; set; }
            
            [JsonProperty("Purge user data after x amount of days of no activity")]
            public int PurgeDays { get; set; }

            [JsonProperty("Admin options")]
            public AdminOptions Admin { get; set; }
            
            [JsonProperty("UI options")]
            public UIOptions UI { get; set; }

            public class BaseOptions
            {
                [JsonProperty("Cancel pending teleport if hurt")]
                public bool CancelOnDamage { get; set; }
                
                [JsonProperty("Cancel pending teleport if either player dies")]
                public bool CancelOnDeath { get; set; }
                
                [JsonProperty("Teleport delay options")]
                public TeleportDelayOptions Delay { get; set; }
            
                [JsonProperty("Teleport cooldown options")]
                public CooldownOptions Cooldown { get; set; }
            
                [JsonProperty("Teleport daily limit options")]
                public LimitOptions Limits { get; set; }
            
                [JsonProperty("Purchase options")]
                public PurchaseOptions Purchase { get; set; }

                [JsonProperty("Command aliases")]
                public List<string> CommandAliases { get; set; }

            }
            
            public class TeleportOptions : BaseOptions
            {
                [JsonProperty("Teleport request timeout (seconds)")]
                public int RequestTimeout { get; set; }
                
                [JsonProperty("Only shows friends, clan members and team mates in player list")]
                public bool FriendliesOnly { get; set; }
                
                [JsonProperty("[Friends Plugin] Only shows friends that are mutual friends in player list (slow)")]
                public bool MutualFriendsOnly { get; set; }
            }
            
            public class HomeOptions : BaseOptions
            {
                [JsonProperty("Max home options")]
                public HomeLimits MaxHomes { get; set; }
                
                [JsonProperty("Sleeping bag homes")]
                public SleepingBagOptions SleepingBags { get; set; }

                [JsonProperty("Allow creating home in building blocked area")]
                public bool AllowSetHomeInBuildBlocked { get; set; }
            
                [JsonProperty("Homes can only be set on building blocks")]
                public bool MustSetHomeOnBuilding { get; set; }
            
                [JsonProperty("Allow homes to be set on floors")]
                public bool CanSetHomeOnFloor { get; set; }
            
                [JsonProperty("Don't allow homes to be set within X distance of another home")]
                public float MinimumHomeRadiusDistance { get; set; }

                [JsonProperty("Wipe home data when the server is wiped")]
                public bool WipeHomesOnNewServerSave { get; set; }
                
                public class SleepingBagOptions
                {
                    [JsonProperty("Create home on bag placement")]
                    public bool CreateHomeOnBagPlacement;

                    [JsonProperty("Create home on bed placement")]
                    public bool CreateHomeOnBedPlacement;

                    [JsonProperty("Create home on beach towel placement")]
                    public bool CreateHomeOnBeachTowelPlacement;

                    [JsonProperty("Only create a home on placement if it is inside a building")]
                    public bool OnlyCreateInBuilding = true;

                    //[JsonProperty("Remove home on bed/bag removal")]
                    //public bool RemoveHomeOnRemoval = false;

                    [JsonProperty("Disable set home command")]
                    public bool DisableSetHomeCommand;
                }

                public class HomeLimits : VipOption
                {
                    [JsonProperty("Default home limit (0 disables limits entirely)")]
                    public override int Default { get; set; }
                
                    [JsonProperty("VIP home limit (permission | limit)")]
                    public override Dictionary<string, int> VIP { get; set; }
                
                    public void RegisterPermissions(Permission permission, Plugin plugin)
                    {
                        foreach (string perm in VIP.Keys)
                        {
                            if (!permission.PermissionExists(perm))
                                permission.RegisterPermission(perm, plugin);
                        }
                    }
                }
            }

            public class WarpOptions : BaseOptions
            {
                [JsonProperty("Teleport to random point in X vicinity (0 to disable)")]
                public float VicinityTeleportRadius { get; set; }
                
                [JsonProperty("Radius to check for NPC's when teleporting to a monument warp point")]
                public float MonumentWarpNPCRadius { get; set; }
                
                [JsonProperty("Monument warp points")]
                public Hash<string, MonumentWarp> MonumentWarps { get; set; }

                public class MonumentWarp
                {
                    [JsonProperty("Generate warp for this monument")]
                    public bool Enabled { get; set; }
                    
                    [JsonProperty("Only generate warp points in the monuments safe zone (if applicable)")]
                    public bool SafeZoneOnly { get; set; }

                    [JsonProperty("Custom chat command")] 
                    public string Command { get; set; } = string.Empty;

                    [JsonProperty("Required permission (prefix with teleportgui.)")]
                    public string Permission { get; set; } = string.Empty;
                }
            }
            
            public class AdminOptions
            {
                [JsonProperty("Don't notify user's when a admin teleports to them")]
                public bool Silent { get; set; }
                
                [JsonProperty("Allow instant teleportation for admins")]
                public bool Instant { get; set; }
            }

            public class UIOptions
            {
                [JsonProperty("Disable UI")]
                public bool DisableUI { get; set; }
                
                [JsonProperty("Hide admins from player search list")]
                public bool HideAdminsInUI { get; set; }
                
                [JsonProperty("Hide warp points if the player doesn't have permission")]
                public bool HideWarpsNoPermission { get; set; }
                
                [JsonProperty("UI Colors")]
                public UIColors Colors { get; set; }

                [JsonProperty("Request Popup")]
                public RequestPopup Request { get; set; } = new RequestPopup();

                public class RequestPopup
                {
                    [JsonProperty("Anchor (TopLeft, TopCenter, TopRight, CenterLeft, Center, CenterRight, BottomLeft, BottomCenter, BottomRight, FullStretch, TopStretch, HorizontalCenterStretch, BottomStretch, LeftStretch, VerticalCenterStretch, RightStretch)"), JsonConverter(typeof(StringEnumConverter))]
                    public AnchorEnum _Anchor { get; set; } = AnchorEnum.CenterRight;
                    
                    [JsonProperty("Offset")]
                    public UIOffset _Offset { get; set; } = new UIOffset(-137.5f, -22.5f, 12.5f, 22.5f);
                    
                    [JsonProperty("Horizontal Padding")]
                    public HorizontalPadding Padding { get; set; } = new HorizontalPadding { Left = 0f, Right = 10f };
                    
                    [JsonIgnore]
                    public Anchor Anchor => FromEnum(_Anchor);
                    
                    [JsonIgnore]
                    public Offset Offset => new Offset(_Offset.XMin, _Offset.YMin, _Offset.XMax, _Offset.YMax);

                    public class UIOffset
                    {
                        public float XMin { get; set; }
                        public float XMax { get; set; }
                        public float YMin { get; set; }
                        public float YMax { get; set; }
                        
                        public UIOffset(){}
                        public UIOffset(float xMin, float yMin, float xMax, float yMax)
                        {
                            XMin = xMin;
                            YMin = yMin;
                            XMax = xMax;
                            YMax = yMax;
                        }
                    }

                    public class HorizontalPadding
                    {
                        public float Left { get; set; }
                        public float Right { get; set; }
                    }
                    
                    public enum AnchorEnum
                    {
                        TopLeft,
                        TopCenter,
                        TopRight,
                        CenterLeft,
                        Center,
                        CenterRight,
                        BottomLeft,
                        BottomCenter,
                        BottomRight,
                        FullStretch,
                        TopStretch,
                        HorizontalCenterStretch,
                        BottomStretch,
                        LeftStretch,
                        VerticalCenterStretch,
                        RightStretch,
                    }

                    private Anchor FromEnum(AnchorEnum anchor)
                    {
                        return anchor switch
                        {
                            AnchorEnum.TopLeft => Anchor.TopLeft,
                            AnchorEnum.TopCenter => Anchor.TopCenter,
                            AnchorEnum.TopRight => Anchor.TopRight,
                            AnchorEnum.CenterLeft => Anchor.CenterLeft,
                            AnchorEnum.CenterRight => Anchor.CenterRight,
                            AnchorEnum.BottomLeft => Anchor.BottomLeft,
                            AnchorEnum.BottomCenter => Anchor.BottomCenter,
                            AnchorEnum.BottomRight => Anchor.BottomRight,
                            AnchorEnum.FullStretch => Anchor.FullStretch,
                            AnchorEnum.TopStretch => Anchor.TopStretch,
                            AnchorEnum.HorizontalCenterStretch => Anchor.HoriztonalCenterStretch,
                            AnchorEnum.BottomStretch => Anchor.BottomStretch,
                            AnchorEnum.LeftStretch => Anchor.LeftStretch,
                            AnchorEnum.VerticalCenterStretch => Anchor.VerticalCenterStretch,
                            AnchorEnum.RightStretch => Anchor.RightStretch,
                            _ => Anchor.Center
                        };
                    }
                }
            }

            public class ChatOptions
            {
                [JsonProperty("Use chat prefix")]
                public bool UsePrefix { get; set; }
                
                [JsonProperty("Chat prefix")]
                public string Prefix { get; set; }
                
                [JsonProperty("Chat icon (steam ID)")]
                public ulong Icon { get; set; }
            }
            
            public class TeleportDelayOptions : VipOption
            {
                [JsonProperty("Default time until teleport (seconds)")]
                public override int Default { get; set; }
                
                [JsonProperty("VIP time until teleport (permission | seconds)")]
                public override Dictionary<string, int> VIP { get; set; } 

                public void RegisterPermissions(Permission permission, Plugin plugin)
                {
                    foreach (string perm in VIP.Keys)
                    {
                        if (!permission.PermissionExists(perm))
                            permission.RegisterPermission(perm, plugin);
                    }
                }
            }

            public class CooldownOptions : VipOption
            {
                [JsonProperty("Default cooldown time (seconds)")]
                public override int Default { get; set; }
                
                [JsonProperty("VIP cooldown times (permission | seconds)")]
                public override Dictionary<string, int> VIP { get; set; }
                
                public void RegisterPermissions(Permission permission, Plugin plugin)
                {
                    foreach (string perm in VIP.Keys)
                    {
                        if (!permission.PermissionExists(perm))
                            permission.RegisterPermission(perm, plugin);
                    }
                }
            }

            public class LimitOptions : VipOption
            {
                [JsonProperty("Default daily limit (0 disables limits entirely)")]
                public override int Default { get; set; }
                
                [JsonProperty("VIP daily limit (permission | limit)")]
                public override Dictionary<string, int> VIP { get; set; }
                
                public void RegisterPermissions(Permission permission, Plugin plugin)
                {
                    foreach (string perm in VIP.Keys)
                    {
                        if (!permission.PermissionExists(perm))
                            permission.RegisterPermission(perm, plugin);
                    }
                }
            }

            public class PurchaseOptions : VipOption
            {
                [JsonProperty("Require payment to teleport after daily limit has been reached")]
                public bool PayAfterUsingDailyLimits { get; set; }

                [JsonProperty("Always require payment to teleport, no freebies")]
                public bool PayAlways { get; set; }
                
                [JsonProperty("Payment mode (ServerRewards, Economics, Scrap)")]
                public PurchaseMode Mode { get; set; }
                
                [JsonProperty("Default payment cost")]
                public override int Default { get; set; }
                
                [JsonProperty("VIP payment cost (permission | cost)")]
                public override Dictionary<string, int> VIP { get; set; }
            }
            
            public abstract class VipOption
            {
                public abstract int Default { get; set; }
                
                public abstract Dictionary<string, int> VIP { get; set; }

                
                public int GetLowestOption(BasePlayer player)
                {
                    int t = int.MaxValue;

                    foreach (KeyValuePair<string, int> kvp in VIP)
                    {
                        if (player.HasPermission(kvp.Key) && kvp.Value < t)
                            t = kvp.Value;
                    }

                    if (t == int.MaxValue)
                        t = Default;

                    return t;
                }
                
                public int GetHighestOption(BasePlayer player)
                {
                    int t = 0;

                    foreach (KeyValuePair<string, int> kvp in VIP)
                    {
                        if (player.HasPermission(kvp.Key))
                        {
                            if (kvp.Value == 0)
                                return 0;
                            
                            if (kvp.Value > t)
                                t = kvp.Value;
                        }
                    }

                    if (t == 0)
                        t = Default;

                    return t;
                }
            }
            
            public class UIColors
            {                
                public Color Background { get; set; }

                public Color Panel { get; set; }
                
                public Color Header { get; set; }
                
                public Color Button { get; set; }

                public Color Close { get; set; }
                
                public Color Highlight { get; set; }
                
                public class Color
                {
                    public string Hex { get; set; }

                    public float Alpha { get; set; }
                }
            }

            #region Teleport Conditions
            public class TeleportConditions
            {
                [JsonProperty("Can teleport whilst bleeding")]
                public WhilstBleedingCondition WhilstBleeding { get; set; }
                
                [JsonProperty("Can teleport whilst crafting")]
                public WhenCraftingCondition WhenCrafting { get; set; }
                
                [JsonProperty("Can teleport if mounted")]
                public MountedCondition Mounted { get; set; }
                
                [JsonProperty("Can teleport if building blocked")]
                public BuildingBlockedCondition BuildingBlocked { get; set; }
                
                [JsonProperty("Can teleport if raid blocked")]
                public RaidBlockedCondition RaidBlocked { get; set; }
                
                [JsonProperty("Can teleport if on cargo ship")]
                public CargoShipCondition CargoShip { get; set; }
                
                [JsonProperty("Can teleport if on hot air balloon")]
                public HotAirBalloonCondition HotAirBalloon { get; set; }
                
                [JsonProperty("Can teleport if near oil rig")]
                public OilRigCondition OilRig { get; set; }
                
                [JsonProperty("Can teleport if in underwater labs")]
                public UnderwaterLabsCondition UnderwaterLabs { get; set; }
                
                [JsonProperty("Can teleport if in water")]
                public InWaterCondition InWater { get; set; }
                
                [JsonProperty("Can teleport if in notp zone")]
                public NoTPZoneCondition NoTpZone { get; set; }
                
                [JsonProperty("Can teleport if in safe zone")]
                public SafeZoneCondition SafeZone { get; set; }
                
                [JsonProperty("Can teleport if hostile")]
                public HostileCondition Hostile { get; set; }
                
                [JsonProperty("Can teleport if in monument")]
                public InMonumentCondition InMonument { get; set; }
                
                [JsonProperty("Can teleport if in any topologies (advanced)")]
                public CustomTopologyCondition Topology { get; set; }

                public bool MeetsConditions(BasePlayer player, BasePlayer target)
                {
                    if (player.IsWounded())
                    {
                        SendReply(player, "Condition.Wounded");
                        return false;
                    }
            
                    string canTeleport = Interface.Oxide.CallHook("CanTeleport", player) as string;
                    if (canTeleport != null)
                    {
                        SendReply(player, canTeleport);
                        return false;
                    }
                    
                    if (!WhilstBleeding.CanTeleportTo(player, target))
                        return false;

                    if (!WhenCrafting.CanTeleportTo(player, target))
                        return false;

                    if (!Mounted.CanTeleportTo(player, target))
                        return false;
                    
                    if (!BuildingBlocked.CanTeleportTo(player, target))
                        return false;
                    
                    if (!RaidBlocked.CanTeleportTo(player, target))
                        return false;
                    
                    if (!CargoShip.CanTeleportTo(player, target))
                        return false;
                    
                    if (!HotAirBalloon.CanTeleportTo(player, target))
                        return false;
                    
                    if (!OilRig.CanTeleportTo(player, target))
                        return false;
                    
                    if (!UnderwaterLabs.CanTeleportTo(player, target))
                        return false;
                    
                    if (!InWater.CanTeleportTo(player, target))
                        return false;
                    
                    if (!NoTpZone.CanTeleportTo(player, target))
                        return false;
                    
                    if (!SafeZone.CanTeleportTo(player, target))
                        return false;
                    
                    if (!Hostile.OnlyWarps && !Hostile.CanTeleportTo(player, target))
                        return false;
                    
                    if (!InMonument.CanTeleportTo(player, target))
                        return false;

                    if (!Topology.CanTeleportTo(player, target))
                        return false;

                    return true;
                }
                
                public bool MeetsConditions(BasePlayer player, Vector3 target)
                {
                    if (player.IsWounded())
                    {
                        SendReply(player, "Condition.Wounded");
                        return false;
                    }

                    string canTeleport = Interface.Oxide.CallHook("CanTeleport", player) as string;
                    if (canTeleport != null)
                    {
                        SendReply(player, canTeleport);
                        return false;
                    }
                    
                    if (!WhilstBleeding.CanTeleportTo(player))
                        return false;

                    if (!WhenCrafting.CanTeleportTo(player))
                        return false;

                    if (!Mounted.CanTeleportTo(player))
                        return false;
                    
                    if (!BuildingBlocked.CanTeleportTo(player, target))
                        return false;
                    
                    if (!RaidBlocked.CanTeleportTo(player))
                        return false;
                    
                    if (!CargoShip.CanTeleportTo(player))
                        return false;
                    
                    if (!HotAirBalloon.CanTeleportTo(player))
                        return false;
                    
                    if (!OilRig.CanTeleportTo(player, target))
                        return false;
                    
                    if (!UnderwaterLabs.CanTeleportTo(player, target))
                        return false;
                    
                    if (!InWater.CanTeleportTo(player))
                        return false;
                    
                    if (!NoTpZone.CanTeleportTo(player))
                        return false;
                    
                    if (!SafeZone.CanTeleportTo(player))
                        return false;
                    
                    if (!Hostile.OnlyWarps && !Hostile.CanTeleportTo(player))
                        return false;
                    
                    if (!InMonument.CanTeleportTo(player, target))
                        return false;
                    
                    if (!Topology.CanTeleportTo(player, target))
                        return false;

                    return true;
                }
                
                public bool MeetsWarpConditions(BasePlayer player, Vector3 target)
                {
                    if (player.IsWounded())
                    {
                        SendReply(player, "Condition.Wounded");
                        return false;
                    }

                    string canTeleport = Interface.Oxide.CallHook("CanTeleport", player) as string;
                    if (canTeleport != null)
                    {
                        SendReply(player, canTeleport);
                        return false;
                    }
                    
                    if (!WhilstBleeding.CanTeleportTo(player))
                        return false;

                    if (!WhenCrafting.CanTeleportTo(player))
                        return false;

                    if (!Mounted.CanTeleportTo(player))
                        return false;
                    
                    if (!BuildingBlocked.CanTeleportTo(player))
                        return false;
                    
                    if (!RaidBlocked.CanTeleportTo(player))
                        return false;
                    
                    if (!CargoShip.CanTeleportTo(player))
                        return false;
                    
                    if (!HotAirBalloon.CanTeleportTo(player))
                        return false;
                    
                    if (!OilRig.CanTeleportTo(player))
                        return false;
                    
                    if (!UnderwaterLabs.CanTeleportTo(player))
                        return false;
                    
                    if (!InWater.CanTeleportTo(player))
                        return false;
                    
                    if (!SafeZone.CanTeleportTo(player))
                        return false;
                    
                    if (!NoTpZone.CanTeleportTo(player))
                        return false;
                    
                    if (!Topology.CanTeleportTo(player))
                        return false;
                    
                    if (!Hostile.CanTeleportTo(player))
                        return false;
                    
                    if (!InMonument.CanTeleportTo(player))
                        return false;
                    
                    if (!Topology.CanTeleportTo(player))
                        return false;
                    
                    return true;
                }
            }
            
            public class WhilstBleedingCondition : TargetTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && target.metabolism.bleeding.value > 0f)
                    {
                        SendReply(player, "Condition.Bleeding.Target");
                        return false;
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && player.metabolism.bleeding.value > 0f)
                    {
                        SendReply(player, "Condition.Bleeding.Self");
                        return false;
                    }

                    return true;
                }
            }

            public class WhenCraftingCondition : TargetTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && target.metabolism.bleeding.value > 0f)
                    {
                        SendReply(player, "Condition.Crafting.Target");
                        return false;
                    }

                    return true;
                }
                
                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && player.inventory.crafting.queue.Count > 0)
                    {
                        SendReply(player, "Condition.Crafting.Self");
                        return false;
                    }

                    return true;
                }
            }
            
            public class BuildingBlockedCondition : AllTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && !target.CanBuild())
                    {
                        SendReply(player, "Condition.BuildingBlocked.Target");
                        return false;
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && !player.CanBuild())
                    {
                        SendReply(player, "Condition.BuildingBlocked.Self");
                        return false;
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player, Vector3 position)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPosition && player.IsBuildingBlocked(position, player.transform.rotation, player.bounds))
                    {
                        SendReply(player, "Condition.BuildingBlocked.Position");
                        return false;
                    }

                    return true;
                }
            }
            
            public class RaidBlockedCondition : TargetTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer)
                    {
                        if (NoEscape.IsLoaded && (NoEscape.IsCombatBlocked(target) || NoEscape.IsRaidBlocked(target)))
                        {
                            SendReply(player, "Condition.RaidBlocked.Target");
                            return false;
                        }
                    }

                    return true;
                }
                
                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport)
                    {
                        if (NoEscape.IsLoaded && (NoEscape.IsCombatBlocked(player) || NoEscape.IsRaidBlocked(player)))
                        {
                            SendReply(player, "Condition.RaidBlocked.Self");
                            return false;
                        }
                    }

                    return true;
                }
            }
            
            public class CargoShipCondition : TargetTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && target.GetParentEntity() is CargoShip)
                    {
                        SendReply(player, "Condition.CargoShip.Target");
                        return false;
                    }

                    return true;
                }
                
                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && player.GetParentEntity() is CargoShip)
                    {
                        SendReply(player, "Condition.CargoShip.Self");
                        return false;
                    }

                    return true;
                }
            }
            
            public class HotAirBalloonCondition : TargetTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && target.GetParentEntity() is HotAirBalloon)
                    {
                        SendReply(player, "Condition.HAB.Target");
                        return false;
                    }

                    return true;
                }
                
                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && player.GetParentEntity() is HotAirBalloon)
                    {
                        SendReply(player, "Condition.HAB.Self");
                        return false;
                    }

                    return true;
                }
            }

            public class OilRigCondition : AllTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && IsNearOilRig(target.transform.position))
                    {
                        SendReply(player, "Condition.OilRig.Target");
                        return false;
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && IsNearOilRig(player.transform.position))
                    {
                        SendReply(player, "Condition.OilRig.Self");
                        return false;
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player, Vector3 position)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPosition && IsNearOilRig(position))
                    {
                        SendReply(player, "Condition.OilRig.Position");
                        return false;
                    }

                    return true;
                }
            }
            
            public class UnderwaterLabsCondition : AllTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && IsInUnderwaterLab(target.transform.position))
                    {
                        SendReply(player, "Condition.UnderwaterLabs.Target");
                        return false;
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && IsInUnderwaterLab(player.transform.position))
                    {
                        SendReply(player, "Condition.UnderwaterLabs.Self");
                        return false;
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player, Vector3 position)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPosition && IsInUnderwaterLab(position))
                    {
                        SendReply(player, "Condition.UnderwaterLabs.Position");
                        return false;
                    }

                    return true;
                }
            }
            
            public class MountedCondition : TargetTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && target.isMounted)
                    {
                        SendReply(player, "Condition.Mounted.Target");
                        return false;
                    }

                    return true;
                }
                
                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && player.isMounted)
                    {
                        SendReply(player, "Condition.Mounted.Self");
                        return false;
                    }

                    return true;
                }
            }
            
            public class InWaterCondition : TargetTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && IsInWater(target))
                    {
                        SendReply(player, "Condition.InWater.Target");
                        return false;
                    }

                    return true;
                }
                
                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && IsInWater(player))
                    {
                        SendReply(player, "Condition.InWater.Self");
                        return false;
                    }

                    return true;
                }
            }
            
            public class NoTPZoneCondition : TargetTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer)
                    {
                        if (ZoneManager.IsLoaded && ZoneManager.PlayerHasFlag(target, "notp"))
                        {
                            SendReply(player, "Condition.NoTPZone.Target");
                            return false;
                        }
                    }

                    return true;
                }
                
                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport)
                    {
                        if (ZoneManager.IsLoaded && ZoneManager.PlayerHasFlag(player, "notp"))
                        {
                            SendReply(player, "Condition.NoTPZone.Self");
                            return false;
                        }
                    }

                    return true;
                }
            }
            
            public class SafeZoneCondition : TargetTeleportCondition
            {
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && target.InSafeZone())
                    {
                        SendReply(player, "Condition.SafeZone.Target");
                        return false;
                    }

                    return true;
                }
                
                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && player.InSafeZone())
                    {
                        SendReply(player, "Condition.SafeZone.Self");
                        return false;
                    }

                    return true;
                }
            }
            
            public class HostileCondition : TargetTeleportCondition
            {
                [JsonProperty("Only check when warping")]
                public bool OnlyWarps { get; set; }
                
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && target.IsHostile())
                    {
                        SendReply(player, "Condition.Hostile.Target");
                        return false;
                    }

                    return true;
                }
                
                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && player.IsHostile())
                    {
                        SendReply(player, "Condition.Hostile.Self");
                        return false;
                    }

                    return true;
                }
            }
            
            public class InMonumentCondition : AllTeleportCondition
            {
                [JsonProperty("Ignore safe zones when checking condition")]
                public bool IgnoreSafeZones { get; set; }
                
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (!CanTeleportTo(player))
                        return false;
                    
                    if (!CanTeleportTargetPlayer && IsInMonument(target.transform.position, IgnoreSafeZones))
                    {
                        SendReply(player, "Condition.Monument.Target");
                        return false;
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (!CanTeleport && IsInMonument(player.transform.position, IgnoreSafeZones))
                    {
                        SendReply(player, "Condition.Monument.Self");
                        return false;
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player, Vector3 position)
                {
                    if (!CanTeleportTo(player))
                        return false;

                    if (!CanTeleportTargetPosition && IsInMonument(position, IgnoreSafeZones))
                    {
                        SendReply(player, "Condition.Monument.Position");
                        return false;
                    }

                    return true;
                }
            }
            
            public class CustomTopologyCondition : AllTeleportCondition
            {
                [JsonProperty("Topology names (ex. ['Road', 'Roadside', 'Cliff'], replacing single quotation marks with double quotation marks)")]
                public string[] Topologies { get; set; }

                private int m_Topology = int.MaxValue;
                
                [JsonIgnore]
                public int Topology
                {
                    get
                    {
                        if (m_Topology == int.MaxValue)
                        {
                            if (Topologies?.Length == 0)
                                m_Topology = 0;
                            else
                            {
                                m_Topology = 0;
                                
                                string[] topologyNames = Enum.GetNames(typeof(TerrainTopology.Enum));
                                
                                for (int i = 0; i < topologyNames.Length; i++)
                                {
                                    if (Topologies.Contains(topologyNames[i], StringComparer.OrdinalIgnoreCase))
                                        m_Topology |= 1 << i;
                                }
                            }
                        }

                        return m_Topology;
                    }
                }
                
                public override bool CanTeleportTo(BasePlayer player, BasePlayer target)
                {
                    if (Topology != 0)
                    {
                        if (!CanTeleportTo(player))
                            return false;

                        if (!CanTeleportTargetPlayer && ContainsTopologyAtPoint(target.transform.position))
                        {
                            SendReply(player, "Condition.Topology.Target");
                            return false;
                        }
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player)
                {
                    if (Topology != 0)
                    {
                        if (!CanTeleport && ContainsTopologyAtPoint(player.transform.position))
                        {
                            SendReply(player, "Condition.Topology.Self");
                            return false;
                        }
                    }

                    return true;
                }

                public override bool CanTeleportTo(BasePlayer player, Vector3 position)
                {
                    if (Topology != 0)
                    {
                        if (!CanTeleportTo(player))
                            return false;

                        if (!CanTeleportTargetPosition && ContainsTopologyAtPoint(position))
                        {
                            SendReply(player, "Condition.Topology.Position");
                            return false;
                        }
                    }

                    return true;
                }

                private bool ContainsTopologyAtPoint(Vector3 position) => (TerrainMeta.TopologyMap.GetTopology(position) & Topology) != 0;
            }
            
            public abstract class AllTeleportCondition : TargetTeleportCondition
            {
                [JsonProperty("Can teleport if target position has condition")]
                public bool CanTeleportTargetPosition { get; set; }

                public abstract bool CanTeleportTo(BasePlayer player, Vector3 position);
            }
            
            public abstract class TargetTeleportCondition : BasicTeleportCondition
            {
                [JsonProperty("Can teleport if target player has condition")]
                public bool CanTeleportTargetPlayer { get; set; }
                
                public abstract bool CanTeleportTo(BasePlayer player, BasePlayer target);
            }
            
            public abstract class BasicTeleportCondition
            {
                [JsonProperty("Can teleport if player has condition")]
                public bool CanTeleport { get; set; }

                public abstract bool CanTeleportTo(BasePlayer player);
            }
            #endregion
            
            public void RegisterCustomPermissions(Permission permission, Plugin plugin)
            {
                TP.Delay.RegisterPermissions(permission, plugin);
                TP.Cooldown.RegisterPermissions(permission, plugin);
                TP.Limits.RegisterPermissions(permission, plugin);
                
                Home.Delay.RegisterPermissions(permission, plugin);
                Home.Cooldown.RegisterPermissions(permission, plugin);
                Home.Limits.RegisterPermissions(permission, plugin);
                Home.MaxHomes.RegisterPermissions(permission, plugin);
                
                Warp.Delay.RegisterPermissions(permission, plugin);
                Warp.Cooldown.RegisterPermissions(permission, plugin);
                Warp.Limits.RegisterPermissions(permission, plugin);
            }
        }  

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = ConfigurationData as ConfigData;
        }
        
        protected override ConfigurationFile OnLoadConfig(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);

        protected override void OnConfigurationUpdated(VersionNumber oldVersion)
        {
            ConfigData baseConfigData = GenerateDefaultConfiguration<ConfigData>();

            if (oldVersion < new VersionNumber(2, 0, 0))
                ConfigurationData = baseConfigData;
            
            if (oldVersion < new VersionNumber(2, 0, 3))
                (ConfigurationData as ConfigData).Warp.MonumentWarps = new Hash<string, ConfigData.WarpOptions.MonumentWarp>();

            if (oldVersion < new VersionNumber(2, 0, 4))
                (ConfigurationData as ConfigData).Conditions.Topology = baseConfigData.Conditions.Topology;

            if (oldVersion < new VersionNumber(2, 0, 8))
                (ConfigurationData as ConfigData).Warp.MonumentWarpNPCRadius = 25f;

            if (oldVersion < new VersionNumber(2, 0, 13))
                (ConfigurationData as ConfigData).Conditions.Hostile = baseConfigData.Conditions.Hostile;
            
            
            Configuration = ConfigurationData as ConfigData;
        }

        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                Chat = new ConfigData.ChatOptions
                {
                    UsePrefix = true,
                    Prefix = "<color=#C4FF00>TP: </color>",
                    Icon = 0UL
                },
                TP = new ConfigData.TeleportOptions
                {
                    RequestTimeout = 30,
                    CancelOnDamage = true,
                    CancelOnDeath = false,
                    Delay = new ConfigData.TeleportDelayOptions
                    {
                        Default = 15,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 10,
                            ["teleportgui.elite"] = 5,
                            ["teleportgui.god"] = 3,
                        }
                    },
                    Cooldown = new ConfigData.CooldownOptions
                    {
                        Default = 10,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 60,
                            ["teleportgui.elite"] = 30,
                            ["teleportgui.god"] = 15,
                        }
                    },
                    Limits = new ConfigData.LimitOptions
                    {
                        Default = 3,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 5,
                            ["teleportgui.elite"] = 8,
                            ["teleportgui.god"] = 15,
                        }
                    },
                    Purchase = new ConfigData.PurchaseOptions
                    {
                        Mode = PurchaseMode.Scrap,
                        PayAfterUsingDailyLimits = false,
                        PayAlways = false,
                        Default = 10,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 8,
                            ["teleportgui.elite"] = 6,
                            ["teleportgui.god"] = 5,
                        }
                    },
                    CommandAliases = new List<string>(),
                },
                Home = new ConfigData.HomeOptions
                {
                    CancelOnDamage = true,
                    CancelOnDeath = false,
                    AllowSetHomeInBuildBlocked = false,
                    MustSetHomeOnBuilding = true,
                    CanSetHomeOnFloor = false,
                    MinimumHomeRadiusDistance = 20f,
                    WipeHomesOnNewServerSave = true,
                    MaxHomes = new ConfigData.HomeOptions.HomeLimits
                    {
                        Default = 3,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 5,
                            ["teleportgui.elite"] = 8,
                            ["teleportgui.god"] = 10,
                        }
                    },
                    Delay = new ConfigData.TeleportDelayOptions
                    {
                        Default = 15,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 10,
                            ["teleportgui.elite"] = 5,
                            ["teleportgui.god"] = 3,
                        }
                    },
                    Cooldown = new ConfigData.CooldownOptions
                    {
                        Default = 10,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 60,
                            ["teleportgui.elite"] = 30,
                            ["teleportgui.god"] = 15,
                        }
                    },
                    Limits = new ConfigData.LimitOptions
                    {
                        Default = 3,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 5,
                            ["teleportgui.elite"] = 8,
                            ["teleportgui.god"] = 15,
                        }
                    },
                    Purchase = new ConfigData.PurchaseOptions
                    {
                        Mode = PurchaseMode.Scrap,
                        PayAfterUsingDailyLimits = false,
                        PayAlways = false,
                        Default = 10,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 8,
                            ["teleportgui.elite"] = 6,
                            ["teleportgui.god"] = 5,
                        }
                    },
                    CommandAliases = new List<string>(),
                    SleepingBags = new ConfigData.HomeOptions.SleepingBagOptions
                    {
                        CreateHomeOnBagPlacement = false,
                        CreateHomeOnBedPlacement = false,
                        CreateHomeOnBeachTowelPlacement = false,
                        OnlyCreateInBuilding = true,
                       // RemoveHomeOnRemoval = false,
                        DisableSetHomeCommand = false
                    },
                },
                Warp = new ConfigData.WarpOptions
                {
                    CancelOnDamage = true,
                    CancelOnDeath = false,
                    VicinityTeleportRadius = 0,
                    MonumentWarpNPCRadius = 25f,
                    Delay = new ConfigData.TeleportDelayOptions
                    {
                        Default = 15,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 10,
                            ["teleportgui.elite"] = 5,
                            ["teleportgui.god"] = 3,
                        }
                    },
                    Cooldown = new ConfigData.CooldownOptions
                    {
                        Default = 10,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 60,
                            ["teleportgui.elite"] = 30,
                            ["teleportgui.god"] = 15,
                        }
                    },
                    Limits = new ConfigData.LimitOptions
                    {
                        Default = 3,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 5,
                            ["teleportgui.elite"] = 8,
                            ["teleportgui.god"] = 15,
                        }
                    },
                    Purchase = new ConfigData.PurchaseOptions
                    {
                        Mode = PurchaseMode.Scrap,
                        PayAfterUsingDailyLimits = false,
                        PayAlways = false,
                        Default = 10,
                        VIP = new Dictionary<string, int>
                        {
                            ["teleportgui.vip"] = 8,
                            ["teleportgui.elite"] = 6,
                            ["teleportgui.god"] = 5,
                        }
                    },
                    CommandAliases = new List<string>(),
                },
                Conditions = new ConfigData.TeleportConditions
                {
                    WhilstBleeding = new ConfigData.WhilstBleedingCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = true
                    },
                    WhenCrafting = new ConfigData.WhenCraftingCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = true
                    },
                    Mounted = new ConfigData.MountedCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false
                    },
                    BuildingBlocked = new ConfigData.BuildingBlockedCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                        CanTeleportTargetPosition = false
                    },
                    RaidBlocked = new ConfigData.RaidBlockedCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                    },
                    CargoShip = new ConfigData.CargoShipCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                    },
                    HotAirBalloon = new ConfigData.HotAirBalloonCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                    },
                    OilRig = new ConfigData.OilRigCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                        CanTeleportTargetPosition = false
                    },
                    UnderwaterLabs = new ConfigData.UnderwaterLabsCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                        CanTeleportTargetPosition = false
                    },
                    InWater = new ConfigData.InWaterCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                    },
                    NoTpZone = new ConfigData.NoTPZoneCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                    },
                    SafeZone = new ConfigData.SafeZoneCondition
                    {
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                    },
                    Hostile = new ConfigData.HostileCondition
                    {
                        OnlyWarps = true,
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                    },
                    InMonument = new ConfigData.InMonumentCondition
                    {
                        IgnoreSafeZones = false,
                        CanTeleport = false,
                        CanTeleportTargetPlayer = false,
                        CanTeleportTargetPosition = false
                    },
                    Topology = new ConfigData.CustomTopologyCondition
                    {
                        CanTeleport = true,
                        CanTeleportTargetPlayer = true,
                        CanTeleportTargetPosition = true,
                        Topologies = Array.Empty<string>()
                    }
                },
                PurgeDays = 7,
                Admin = new ConfigData.AdminOptions
                {
                    Instant = false,
                    Silent = false,
                },
                UI = new ConfigData.UIOptions
                {
                    HideAdminsInUI = false,
                    Colors = new ConfigData.UIColors
                    {
                        Background = new ConfigData.UIColors.Color
                        {
                            Hex = "151515",
                            Alpha = 0.94f
                        },
                        Panel = new ConfigData.UIColors.Color
                        {
                            Hex = "FFFFFF",
                            Alpha = 0.165f
                        },
                        Header = new ConfigData.UIColors.Color
                        {
                            Hex = "C4FF00",
                            Alpha = 0.314f
                        },
                        Button = new ConfigData.UIColors.Color
                        {
                            Hex = "2A2E32",
                            Alpha = 1f
                        },
                        Close = new ConfigData.UIColors.Color
                        {
                            Hex = "CE422B",
                            Alpha = 1f
                        },
                        Highlight = new ConfigData.UIColors.Color
                        {
                            Hex = "C4FF00",
                            Alpha = 1f
                        },
                    }
                }

            } as T;
        }

        #endregion

        #region Data

        private interface IWarpPoint
        {
            bool IsEnabled();
            bool HasPermission(BasePlayer player);
            Vector3 GetPosition();
        }

        private class MonumentWarpPoint : IWarpPoint
        {
            internal LocalSpawnGenerator m_Spawns;

            public string Permission;

            public string Shortname { get; }

            public string MapGrid { get; }

            public int Count => m_Spawns?.Count ?? 0;

            public MonumentWarpPoint(string shortname, string grid, string uniqueName, Transform transform, Bounds bounds, Vector3 position, float radius, bool safeZoneOnly)
            {
                Shortname = shortname;
                MapGrid = grid;
                
                m_Spawns = new LocalSpawnGenerator(shortname, transform, bounds, position, radius, safeZoneOnly);
                
                if (m_Spawns.Count == 0)
                {
                    Debug.Log($"[TeleportGUI] Failed to generate spawn points in monument {uniqueName}");
                    m_Spawns = null;
                }
            }

            public bool IsEnabled() => m_Spawns?.Count > 0;
                    
            public bool HasPermission(BasePlayer player) => string.IsNullOrEmpty(Permission) || player.HasPermission(Permission);

            public Vector3 GetPosition() => m_Spawns.GetRandom();
        }
        
        private class WarpPoint : IWarpPoint
        {
            public Vector3 Position;
            public string Permission;

            public bool HasPermission(BasePlayer player) => string.IsNullOrEmpty(Permission) || player.HasPermission(Permission);

            public bool IsEnabled() => true;
            
            public Vector3 GetPosition()
            {
                Vector3 warpPosition = Position;
                
                if (Configuration.Warp.VicinityTeleportRadius > 0f)
                {
                    Vector3 random = Random.insideUnitCircle * Configuration.Warp.VicinityTeleportRadius;

                    warpPosition.x += random.x;
                    warpPosition.z += random.y;
                    warpPosition.y = TerrainMeta.HeightMap.GetHeight(warpPosition);
                }

                return warpPosition;
            }
        }
        
        private class TeleportData
        {
            public Hash<ulong, User> Users = new Hash<ulong, User>();
            public double LastResetTime;

            [JsonIgnore]
            public bool ShouldResetUses
            {
                get
                {
                    DateTime now = DateTime.UtcNow;
                    DateTime lastTime = DateTimeOffset.FromUnixTimeSeconds((long)LastResetTime).DateTime;
                    
                    return now.Day != lastTime.Day || now.Month != lastTime.Month || now.Year != lastTime.Year;
                }
            }
            
            public void WipeHomesData()
            {
                foreach (User user in Users.Values)
                {
                    user.Homes.Clear();
                    user.HomeUsage.Reset();
                }
                
                m_TeleportData.Save();
            }
            
            public class User
            {
                public Hash<string, Vector3> Locations = new Hash<string, Vector3>();
                public Hash<string, HomePoint> Homes = new Hash<string, HomePoint>();

                public Usage TPUsage = new Usage();
                public Usage HomeUsage = new Usage();
                public Usage WarpUsage = new Usage();
                
                public double LastOnlineTime;

                public bool ShowSleepers;
                public AutoAcceptEnum AutoAccept;

                [JsonIgnore] public string SearchString = string.Empty;
                [JsonIgnore] public int Page;

                public void OnClose()
                {
                    SearchString = string.Empty;
                    Page = 0;
                }

                [Flags]
                public enum AutoAcceptEnum { None = 0, Clans = 1 << 0, Teams = 1 << 1, Friends = 1 << 2, All = 1 << 3 }

                public class Usage
                {
                    public int UsesToday;
                    public double Cooldown;
                    
                    public bool IsOnCooldown() => Cooldown > CurrentTime();

                    public void Reset()
                    {
                        UsesToday = 0;
                        Cooldown = 0;
                    }
                }

                public class HomePoint
                {
                    public Vector3 Position;
                    public ulong EntityID;
                }
            }
        }
        #endregion
        
        #region Localization
        protected override void PopulatePhrases(){}
        
        private void PrepareLocalization()
        {
            m_Messages = new Dictionary<string, string>
            {
                ["Purchase.Error.SR"] = "You do not have enough RP to purchase this teleport ({0} RP)",
                ["Purchase.Error.Economics"] = "You do not have enough coins to purchase this teleport ({0} coins)",
                ["Purchase.Error.Scrap"] = "You do not have enough scrap to purchase this teleport ({0} scrap)",
                ["Purchase.Success.SR"] = "You have purchased this teleport for {0} RP",
                ["Purchase.Success.Economics"] = "You have purchased this teleport for {0} coinds",
                ["Purchase.Success.Scrap"] = "You have purchased this teleport for {0} scrap",
                ["Purchase.Refund.SR"] = "You were refunded {0} RP",
                ["Purchase.Refund.Economics"] = "You were refunded {0} coinds",
                ["Purchase.Refund.Scrap"] = "You were refunded {0} scrap",
                
                ["TPRequest.Here.Sent"] = "You sent a teleport here request to {0}",
                ["TPRequest.Here.Received"] = "You have received a teleport here request from {0}",
                ["TPRequest.Sent"] = "You sent a teleport request to {0}",
                ["TPRequest.Received"] = "You have received a teleport request from {0}",
                
                ["TPRequest.Instant.Sent"] = "Your teleport request to {0} was accepted automatically. Teleporting in {1} seconds",
                ["TPRequest.Instant.Received"] = "The teleport request from {0} was accepted as you have auto-accept enabled. Teleporting in {1} seconds.",
                ["TPRequest.To.Accepted"] = "Your teleport request to {0} was accepted. Teleporting in {1} seconds.",
                ["TPRequest.From.Accepted"] = "Teleport request from {0} accepted. Telporting in {1} seconds.",
                
                ["TPRequest.Here.To.Denied"] = "Your teleport here request to {0} was denied",
                ["TPRequest.Here.From.Denied"] = "Teleport here request from {0} denied",
                ["TPRequest.To.Denied"] = "Your teleport request to {0} was denied",
                ["TPRequest.From.Denied"] = "Teleport request from {0} denied",
                
                ["TPRequest.To.Cancelled"] = "Teleport request to {0} cancelled",
                ["TPRequest.From.Cancelled"] = "Teleport request from {0} cancelled",
                
                ["TPRequest.Here.To.TimeOut"] = "Your teleport here request to {0} timed out",
                ["TPRequest.Here.From.TimeOut"] = "The teleport here request from {0} timed out",
                ["TPRequest.To.TimeOut"] = "Your teleport request to {0} timed out",
                ["TPRequest.From.TimeOut"] = "The teleport request from {0} timed out",
                
                ["TPR.Admin.YouWereTPTo"] = "You were teleported to {0}",
                ["TPR.Admin.TPTo"] = "{0} teleported to you",
                ["TPR.Admin.YouTPToYou"] = "You teleported {0} to your position",
                ["TPR.YouTPTo"] = "You teleported to {0}",
                ["TPR.InvalidSynax"] = "Invalid TPR syntax! /tpr {player name}",
                ["TPR.Here.InvalidSynax"] = "Invalid TPR syntax! /tprhere {player name}",
                
                ["TP.TargetPendingRequest"] = "{0} has a pending teleport request",
                ["TP.SelfHasPendingRequest"] = "You have a pending teleport request",
                
                ["TP.To.Cancelled"] = "Teleport to {0} was cancelled",
                ["TP.From.Cancelled"] = "Teleport from {0} was cancelled",
                ["TP.To.Cancelled.Reason"] = "Teleport to {0} was cancelled : {1}",
                ["TP.From.Cancelled.Reason"] = "Teleport from {0} was cancelled : {1}",
                ["TP.Error.TargetDead"] = "Teleport cancelled. The target player has died",
                ["TP.Error.NoPending"] = "You have no teleports to cancel",
                ["TP.Error.PositionSyntax"] = "Invalid position sytanx! /tp {x} {y} {z}",
                ["TP.Success.Position"] = "You teleported to {0} {1} {2}",
                
                ["TPB.NoBackLocation"] = "You do not have a location to TP back to",
                ["TPB.Success"] = "You teleported back to your previous location",
                
                ["TPB.Admin.NoBackLocation"] = "{0} does not have a location to TP back to",
                ["TPB.Admin.Success"] = "You teleported {0} back to their previous location",
                ["TPB.Admin.Success.Target"] = "You were teleported back to your previous location by an admin",
                
                ["TPA.NonePending"] = "You do not have any pending teleport requests",

                ["TP.CantTeleportSelf"] = "You can not teleport to yourself",
                
                ["TPL.NoNameSpecified"] = "You must specify a location name",
                ["TPL.Success"] = "You have saved your position as teleport location {0}",
                ["TPL.Error.NoLocations"] = "You do not have any saved locations",
                ["TPL.Error.NoLocation.Name"] = "You do not have a saved location with the name {0}",
                ["TPL.List"] = "Your saved locations: {0}",
                
                ["Home.SelfHasPendingRequest"] = "You have a pending home teleport",
                ["Home.Error.IsBuildingBlocked"] = "You can not set home in a building blocked area",
                ["Home.Error.LimitReached"] = "You already have the maximum number of homes allowed",
                ["Home.Error.NearbyRadius"] = "You already have a home set {0}m away. The minimum distance for homes is {1}m",
                ["Home.Error.NotOnFoundation"] = "Homes can only be set on foundations",
                ["Home.Error.NotOnFoundationFloor"] = "Homes can only be set on foundations and floors",
                ["Home.Error.NoTPZone"] = "Homes can not be set in NoTP zones",
                ["Home.Error.NoBackLocation"] = "You do not have a location to TP back to",
                ["Home.Error.NoPending"] = "You have no pending home teleports to cancel",
                ["Home.Error.Invalid"] = "The home {0} has been destroyed as the block it was placed on has been destroyed",
                ["Home.Error.DoesntExist"] = "The home {0} doesnt exist",
                ["Home.Error.NoHomes"] = "You have not created any homes",
                ["Home.Error.AlreadyExists"] = "A home with the name {0} already exists",
                ["Home.Error.BagDisableCommand"] = "The /sethome is disabled on this server. Homes are created on bags/beds",
                ["Home.Error.SetHomeSyntax"] = "Invalid sethome syntax! /sethome {homename}",
                ["Home.Error.DelHomeSyntax"] = "Invalid delhome syntax! /delhome {homename}",
                ["Home.Error.NoHomes.Target"] = "{0} has not created any homes",
                ["Home.Error.DoesntExist.Target"] = "{0} does not have a home with the name {1}",
                
                ["Home.Cancelled"] = "You have cancelled your pending home teleport",
                ["Home.List"] = "Your homes: {0}",
                ["Home.List.Target"] = "Homes for {0}: {1}",
                ["Home.Success.Created"] = "You have created a new home with the name {0}",
                ["Home.Success.Created.Bed"] = "A home point has been created on this bag/bed with the name {0}",
                ["Home.Success.Created.Remaining"] = "You have created a new home with the name {0} ({1} homes remaining)",
                ["Home.Success.Created.Bed.Remaining"] = "A home point has been created on this bag/bed with the name {0}  ({1} homes remaining)",
                ["Home.Success.Deleted"] = "You have deleted the home {0}",
                ["Notification.BedHomeDestroyed"] = "Your home {0} was destroyed",
                
                ["Warp.SelfHasPendingRequest"] = "You have a pending warp teleport",
                ["Warp.Error.DoesntExist"] = "The warp {0} doesnt exist",
                ["Warp.Error.NoPermission"] = "You do not have permission to use this warp point",
                ["Warp.Error.NoBackLocation"] = "You do not have a location to TP back to",

                ["WarpTo.Error.Syntax"] = "Invalid warp syntax! /warp {to/list} {warpname}",
                ["WarpTo.List"] = "Available warps: {0}",
                
                ["WarpAdd.Error.Syntax"] = "Invalid warp add syntax! /warpadd {warpname} {opt:permission}",
                ["WarpAdd.Error.Exists"] = "A warp with the name {0} already exists",
                ["WarpAdd.Success"] = "You have created a warp point on your position with the name {0}",
                ["WarpAdd.Success.Permission"] = "You have created a warp point on your position with the name {0} and permission {1}",
                
                ["WarpRemove.Error.Syntax"] = "Invalid warp remove syntax! /warpremove {warpname}",
                ["WarpRemove.Success"] = "You have removed the warp point {0}",
                
                ["TP.CooldownFormat"] = "Your teleport is on cooldown for another {0}",
                ["Home.CooldownFormat"] = "Your home teleport is on cooldown for another {0}",
                ["Warp.CooldownFormat"] = "Your warp teleport is on cooldown for another {0}",
                
                ["TP.MaxTeleportsReached"] = "You have reached your max teleports for today.",
                ["Home.MaxTeleportsReached"] = "You have reached your max home teleports for today.",
                ["Warp.MaxTeleportsReached"] = "You have reached your max warp teleports for today.",
                
                ["General.InsideFoundation"] = "The target position is inside a foundation",
                ["General.NoPermission"] = "You do not have permission to use this command",
                ["General.PlayerNotFound"] = "Unable to find a player with the name {0}",
                ["General.MultiplePlayersFound"] = "Multiple players found with the name {0}\n{1}",
                
                ["Notification.TP.Remaining"] = "{0} teleports remaining today.",
                ["Notification.Home.Remaining"] = "{0} home teleports remaining today.",
                ["Notification.Warp.Remaining"] = "{0} warp teleports remaining today.",

                ["UI.Title"] = "Teleport GUI",
                ["UI.Teleport"] = "Teleport",
                ["UI.Homes"] = "Homes",
                ["UI.Warps"] = "Warps",
                ["UI.CostToTP"] = "Cost to TP: {0} {1}",
                ["UI.TPLimit"] = "You have used all your teleports for today",
                ["UI.DailyLimitRemain"] = "Daily Limit Remaining: {0}",
                ["UI.CooldownRemain"] = "Cooldown Remaining : %TIME_LEFT%s",
                ["UI.NoPlayers"] = "No players available at the moment",
                ["UI.NoHomes"] = "You have not created any homes",
                ["UI.NoWarps"] = "No warp points have been created",
                ["UI.TPR"] = "TPR",
                ["UI.TPHere"] = "HERE",
                ["UI.HomeName"] = "Home Name",
                ["UI.WarpName"] = "Warp Name",
                ["UI.Permission"] = "Permission",
                ["UI.CreateNewWarp"] = "Create Warp Point",
                ["UI.CreateNewHome"] = "Create New Home",
                ["UI.Save"] = "Save",
                ["UI.Cancel"] = "Cancel",
                ["UI.Close"] = "Close",
                ["UI.Unlimited2"] = "~",
                ["UI.AA.Clan"] = "Auto accept clan",
                ["UI.AA.Team"] = "Auto accept team",
                ["UI.AA.Friend"] = "Auto accept friends",
                ["UI.AA.All"] = "Auto accept all",
                ["UI.ShowSleepers"] = "Show sleepers",
                ["UI.TeleportSettings"] = "Teleport Settings",
                ["PurchaseMode.Scrap"] = "Scrap",
                ["PurchaseMode.ServerRewards"] = "RP",
                ["PurchaseMode.Economics"] = "coins",
                
                ["Popup.Incoming.TPHere"] = "INCOMING TPHERE (%TIME_LEFT%s)",
                ["Popup.Incoming.TPR"] = "INCOMING TPR (%TIME_LEFT%s)",
                ["Popup.Outgoing.TPHere"] = "OUTGOING TPHERE (%TIME_LEFT%s)",
                ["Popup.Outgoing.TPR"] = "OUTGOING TPR (%TIME_LEFT%s)",
                ["Popup.Incoming.TP"] = "INCOMING TP IN %TIME_LEFT%s",
                ["Popup.Outgoing.TP"] = "TELEPORTING IN %TIME_LEFT%s",
                ["Popup.Outgoing.TP.Home"] = "TELEPORTING IN %TIME_LEFT%s",
                ["Popup.Outgoing.TP.Warp"] = "TELEPORTING IN %TIME_LEFT%s",
                
                ["Condition.Wounded"] = "You can not teleport whilst wounded",
                ["Condition.Bleeding.Self"] = "You can not teleport whilst bleeding",
                ["Condition.Bleeding.Target"] = "You can not teleport when the target is bleeding",
                ["Condition.Mounted.Self"] = "You can not teleport whilst mounted",
                ["Condition.Mounted.Target"] = "You can not teleport when the target is mounted",
                ["Condition.InWater.Self"] = "You can not teleport whilst in water",
                ["Condition.InWater.Target"] = "You can not teleport when the target is in water",
                ["Condition.NoTPZone.Self"] = "You can not teleport whilst in a NoTP zone",
                ["Condition.NoTPZone.Target"] = "You can not teleport when the target is in a NoTP zone",
                ["Condition.SafeZone.Self"] = "You can not teleport whilst in a safe zone",
                ["Condition.SafeZone.Target"] = "You can not teleport when the target is in a safe zone",
                ["Condition.Hostile.Self"] = "You can not teleport whilst deemed hostile",
                ["Condition.Hostile.Target"] = "You can not teleport when the target is deemed hostile",
                ["Condition.Crafting.Self"] = "You can not teleport whilst crafting",
                ["Condition.Crafting.Target"] = "You can not teleport when the target is crafting",
                ["Condition.BuildingBlocked.Self"] = "You can not teleport whilst building blocked",
                ["Condition.BuildingBlocked.Target"] = "You can not teleport when the target is building blocked",
                ["Condition.BuildingBlocked.Position"] = "You can not teleport as you are building blocked in the desired location",
                ["Condition.RaidBlocked.Self"] = "You can not teleport whilst raid/combat blocked",
                ["Condition.RaidBlocked.Target"] = "You can not teleport when the target is raid/combat blocked",
                ["Condition.CargoShip.Self"] = "You can not teleport whilst on the cargo ship",
                ["Condition.CargoShip.Target"] = "You can not teleport when the target is on the cargo ship",
                ["Condition.HAB.Self"] = "You can not teleport whilst in a hot air balloon",
                ["Condition.HAB.Target"] = "You can not teleport when the target is in a hot air balloon",
                ["Condition.OilRig.Self"] = "You can not teleport whilst you are near the oil rig",
                ["Condition.OilRig.Target"] = "You can not teleport when the target is near the oil rig",
                ["Condition.OilRig.Position"] = "You can not teleport as the desired location is too close to the oil rig",
                ["Condition.UnderwaterLabs.Self"] = "You can not teleport whilst you are in underwater labs",
                ["Condition.UnderwaterLabs.Target"] = "You can not teleport when the target is in underwater labs",
                ["Condition.UnderwaterLabs.Position"] = "You can not teleport as the desired location is too close to underwater labs",
                ["Condition.Monument.Self"] = "You can not teleport whilst you are in a monument",
                ["Condition.Monument.Target"] = "You can not teleport when the target is in a monument",
                ["Condition.Monument.Position"] = "You can not teleport as the desired location is too close to a monument",
                ["Condition.Topology.Self"] = "You can not teleport from your current position",
                ["Condition.Topology.Target"] = "You can not teleport to the targets current position",
                ["Condition.Topology.Position"] = "You can not teleport to the target position",
                
                ["Reason.Disconnected"] = "Disconnected",
                ["Reason.TookDamage"] = "Hurt",
                ["Reason.Death"] = "Died",
                ["Reason.Declined"] = "Declined",
                
                ["Help.Warp.1"] = "/warp to <warpname> - Teleport to the specified warp point",
                ["Help.Warp.2"] = "/warp list - Show available warp points",
                ["Help.Warp.3"] = "/warpadd <warpname> - Add a new warp point on your current position",
                ["Help.Warp.4"] = "/warpremove <warpname> - Delete the specified warp point",
                
                ["Help.Homes.1"] = "/home <homename> - Teleport to the specified home",
                ["Help.Homes.2"] = "/sethome <homename> - Create a home on your current position",
                ["Help.Homes.3"] = "/delhome <homename> - Delete the home with the specified name",
                ["Help.Homes.4"] = "/listhomes - List all of your home names",
                ["Help.Homes.5"] = "/homec - Cancel a pending home teleport",
                
                ["Help.TP.1"] = "/tpsave <name> - Save your current position as a TP location",
                ["Help.TP.2"] = "/tpl <name> - Teleport to the specified TP location",
                ["Help.TP.3"] = "/tplist - List all of your TP locations",
                ["Help.TP.4"] = "/tpr <playername> - Request a teleport to the specified player",
                ["Help.TP.5"] = "/tpa - Accept an incoming TP request",
                ["Help.TP.6"] = "/tpd - Decline an incoming TP request",
                ["Help.TP.7"] = "/tpc - Cancel a pending TP request",
                ["Help.TP.8"] = "/tpb - Teleport back to your previous location",
                ["Help.TP.9"] = "/tprhere <playername> - Request a teleport that brings the specified player to you",
                
                /*
                ["Monument.trainyard_1"] = "Trainyard",
                ["Monument.harbor_2"] = "Harbor",
                ["Monument.harbor_1"] = "Large Harbor",
                ["Monument.fishing_village_c"] = "Fishing Village",
                ["Monument.fishing_village_a"] = "Fishing Village",
                ["Monument.fishing_village_b"] = "Fishing Village",
                ["Monument.desert_military_base_a"] = "Military Base",
                ["Monument.desert_military_base_b"] = "Military Base",
                ["Monument.desert_military_base_c"] = "Military Base",
                ["Monument.desert_military_base_d"] = "Military Base",
                ["Monument.arctic_research_base_a"] = "Arctic Research Base",
                ["Monument.arctic_research_base_b"] = "Arctic Research Base",
                ["Monument.arctic_research_base_c"] = "Arctic Research Base",
                ["Monument.launch_site_1"] = "Launch Site",
                ["Monument.compound"] = "Compound",
                ["Monument.bandit_town"] = "Bandit Town",
                ["Monument.excavator_1"] = "Excavator",
                ["Monument.junkyard_1"] = "Junkyard",
                ["Monument.stables_a"] = "Stables",
                ["Monument.stables_b"] = "Stables",
                ["Monument.stables_c"] = "Stables",
                ["Monument.powerplant_1"] = "Powerplant",
                ["Monument.military_tunnel_1"] = "Military Tunnel",
                ["Monument.water_treatment_plant_1"] = "Water Treatment",
                ["Monument.airfield_1"] = "Airfield",
                ["Monument.radtown_small_3"] = "Radtown",
                ["Monument.mining_quarry_a"] = "Sulfur Quarry",
                ["Monument.mining_quarry_b"] = "Stone Quarry",
                ["Monument.mining_quarry_c"] = "HQM Quarry",
                ["Monument.sphere_tank"] = "Dome",
                ["Monument.satellite_dish"] = "Satellite Dish",
                ["Monument.entrance_bunker_a"] = "Entrance Bunker",
                ["Monument.entrance_bunker_b"] = "Entrance Bunker",
                ["Monument.entrance_bunker_c"] = "Entrance Bunker",
                ["Monument.entrance_bunker_d"] = "Entrance Bunker",
                ["Monument.warehouse"] = "Warehouse",
                ["Monument.supermarket_1"] = "Supermarket",
                ["Monument.gas_station_1"] = "Gas Station",
                ["Monument.lighthouse"] = "Lighthouse",
                ["Monument.nuclear_missile_silo"] = "Missle Silo",
                ["Monument.water_well_a"] = "Water Well",
                ["Monument.water_well_b"] = "Water Well",
                ["Monument.water_well_c"] = "Water Well",*/
            };
        }

        #endregion
        
        #region API
        private Dictionary<string, Vector3> GetPlayerHomes(ulong userID)
        {
            if (!m_TeleportData.Data.Users.TryGetValue(userID, out TeleportData.User user))
                return null;

            return user.Homes.ToDictionary(k => k.Key, v => v.Value.Position);
        }
        
        private Dictionary<string, Vector3> GetPlayerLocations(ulong userID)
        {
            if (!m_TeleportData.Data.Users.TryGetValue(userID, out TeleportData.User user))
                return null;

            return user.Locations.ToDictionary(k => k.Key, v => v.Value);
        }
        
        private Dictionary<string, Vector3> GetWarpPoints()
        {
            return m_WarpData.Data.ToDictionary(k => k.Key, v => v.Value.Position);
        }

        private double GetPlayerHomeCooldown(ulong userID)
        {
            if (!m_TeleportData.Data.Users.TryGetValue(userID, out TeleportData.User user))
                return 0;

            return user.HomeUsage.Cooldown;
        }

        private int GetPlayerHomeUses(ulong userID)
        {
            if (!m_TeleportData.Data.Users.TryGetValue(userID, out TeleportData.User user))
                return 0;

            return user.HomeUsage.UsesToday;
        }
        
        private double GetPlayerTPCooldown(ulong userID)
        {
            if (!m_TeleportData.Data.Users.TryGetValue(userID, out TeleportData.User user))
                return 0;

            return user.TPUsage.Cooldown;
        }

        private int GetPlayerTPUses(ulong userID)
        {
            if (!m_TeleportData.Data.Users.TryGetValue(userID, out TeleportData.User user))
                return 0;

            return user.TPUsage.UsesToday;
        }
        
        private double GetPlayerWarpCooldown(ulong userID)
        {
            if (!m_TeleportData.Data.Users.TryGetValue(userID, out TeleportData.User user))
                return 0;

            return user.WarpUsage.Cooldown;
        }

        private int GetPlayerWarpUses(ulong userID)
        {
            if (!m_TeleportData.Data.Users.TryGetValue(userID, out TeleportData.User user))
                return 0;

            return user.WarpUsage.UsesToday;
        }

        #endregion
        
        private class Monument
        {
            public string Shortname { get; private set; }
            private Transform m_Transform;
            private Vector3 m_Position;
            private Bounds m_Bounds;
            private float m_Radius;
            
            public bool IsSafeZone { get; }
            public Bounds Bounds => m_Bounds;
            public Transform Transform => m_Transform;
            public Vector3 Position => m_Position;
            public float Radius => m_Radius;

            public Monument(string shortname, bool isSafeZone, Transform transform, Vector3 position = default, float radius = 0f, Bounds bounds = default)
            {
                Shortname = shortname;
                m_Transform = transform;
                m_Position = position;
                m_Radius = radius;
                m_Bounds = bounds;

                IsSafeZone = isSafeZone;//shortname.Equals("compound") || shortname.Equals("bandit_town");
            }
            
            public bool IsInMonument(Vector3 position)
            {
                if (m_Bounds == default || m_Radius > 0f)
                    return Vector3.Distance(m_Position, position) < m_Radius;
                
                Vector3 local = m_Transform.InverseTransformPoint(position);
                return m_Bounds.Contains(local);
            }
        }
        
        #region Monument Warps
        
        [ChatCommand("showmonumentbounds")]
        private void CommandShowBounds(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            float time = 30f;
            if (args.Length > 0)
                float.TryParse(args[0], out time);
            
            foreach (Monument monument in m_Monuments)
            {
                Bounds bounds = monument.Bounds;
                float radius = monument.Radius;
                
                player.SendConsoleCommand("ddraw.text", time, UnityEngine.Color.white, monument.Transform.position, monument.Shortname);
                
                if (radius > 0)
                    player.SendConsoleCommand("ddraw.sphere", time, UnityEngine.Color.blue, monument.Position, radius);

                else if (bounds != default)
                {
                    Transform transform = monument.Transform;
                    
                    // Calculate the 8 corners of the bounds in local space
                    Vector3 worldCorner1 = transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z)); // Bottom-front-left
                    Vector3 worldCorner2 = transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z)); // Bottom-back-left
                    Vector3 worldCorner3 = transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z)); // Top-front-left
                    Vector3 worldCorner4 = transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z)); // Top-back-left
                    Vector3 worldCorner5 = transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z)); // Bottom-front-right
                    Vector3 worldCorner6 = transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z)); // Bottom-back-right
                    Vector3 worldCorner7 = transform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z)); // Top-front-right
                    Vector3 worldCorner8 = transform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)); // Top-back-right

                    // Draw the lines to form the bounding box
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner1, worldCorner2); // Bottom left edge
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner1, worldCorner3); // Front left vertical
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner2, worldCorner4); // Back left vertical
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner3, worldCorner4); // Top left edge

                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner5, worldCorner6); // Bottom right edge
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner5, worldCorner7); // Front right vertical
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner6, worldCorner8); // Back right vertical
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner7, worldCorner8); // Top right edge

                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner1, worldCorner5); // Bottom front edge
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner2, worldCorner6); // Bottom back edge
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner3, worldCorner7); // Top front edge
                    player.SendConsoleCommand("ddraw.line", time, UnityEngine.Color.blue, worldCorner4, worldCorner8); // Top back edge
                }
            }
        }
        
        [ChatCommand("showgeneratedwarps")]
        private void CommandShowWarps(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;
            
            foreach (KeyValuePair<string, MonumentWarpPoint> kvp in m_MonumentWarps)
            {
                foreach (Vector3 v in kvp.Value.m_Spawns.m_SpawnPoints)
                {
                    if (Vector3.Distance(player.transform.position, v) < 300)
                    {
                        player.SendConsoleCommand("ddraw.sphere", 30f, UnityEngine.Color.blue, v, 0.5f);
                        player.SendConsoleCommand("ddraw.line", 30f, UnityEngine.Color.blue, v, v + (Vector3.up * 100f));
                    }
                }
            }
        }
        
        public class LocalSpawnGenerator
        {
            internal List<Vector3> m_SpawnPoints;
            private List<Vector3> m_AvailablePoints;

            public int Count => m_SpawnPoints.Count;

            private const int TARGET_LAYERS = ~(1 << 2 | 1 << 10 | 1 << 11 | 1 << 18 | 1 << 28 | 1 << 29);

            private static readonly string[] m_TargetNames = { "road", "carpark", "pavement", "walkway", "cliff", "Cliff", "a_lighthouse_ext" };

            private static RaycastHit m_RaycastHit;
            
            public LocalSpawnGenerator(string shortname, Transform transform, Bounds bounds, Vector3 position, float radius, bool safeZoneOnly)
            {
                m_SpawnPoints = new List<Vector3>();

                float maxDistance = radius > 0f ? radius * 2f : bounds.size.y;
                
                for (int i = 0; i < (safeZoneOnly ? 250 : 150); i++)
                {
                    Vector3 p = radius > 0f ? GetRandomPointInRadius(position, radius) : GetRandomPointInBounds(transform, bounds);
                    
                    if (Physics.SphereCast(new Ray(p, Vector3.down), 0.5f, out m_RaycastHit, maxDistance, TARGET_LAYERS, QueryTriggerInteraction.Ignore))
                    {
                        if (m_RaycastHit.collider.GetComponent<ProceduralObject>())
                            continue;

                        if (Mathf.Abs(Vector3.Dot(m_RaycastHit.normal, Vector3.up)) < 0.9f)
                            continue;

                        if (WaterLevel.GetWaterSurface(m_RaycastHit.point, true, true) > m_RaycastHit.point.y)
                            continue;

                        if (safeZoneOnly && !IsInSafeZone(m_RaycastHit.point))
                            continue;

                        if (m_RaycastHit.collider is TerrainCollider)
                        {
                            m_SpawnPoints.Add(m_RaycastHit.point);
                            continue;
                        }

                        if (m_TargetNames.Any(m_RaycastHit.collider.name.Contains))
                        {
                            m_SpawnPoints.Add(m_RaycastHit.point);
                        }
                    }

                    if (Count >= 30)
                        break;
                }

                m_AvailablePoints = new List<Vector3>(m_SpawnPoints);
            }
            
            private bool IsInSafeZone(Vector3 position)
            {
                List<Collider> list = Pool.Get<List<Collider>>();
                Vis.Colliders(position, 0f, list, -1, QueryTriggerInteraction.Collide);
                foreach (Collider collider in list)
                {
                    if (collider.GetComponent<TriggerSafeZone>())
                    {
                        Pool.FreeUnmanaged(ref list);
                        return true;
                    }
                }
                
                Pool.FreeUnmanaged(ref list);
                return false;
            }

            private Vector3 GetRandomPointInRadius(Vector3 position, float radius)
            {
                Vector2 random = Random.insideUnitCircle * radius;
                return position + new Vector3(random.x, position.y + radius, random.y);
            }
            
            private Vector3 GetRandomPointInBounds(Transform transform, Bounds bounds)
            {
                Vector3 target = new Vector3(Random.Range(bounds.min.x, bounds.max.x), bounds.center.y + bounds.max.y, Random.Range(bounds.min.z, bounds.max.z));

                return transform.TransformPoint(bounds.ClosestPoint(target));
            }

            public Vector3 GetRandom(int attempt = 0)
            {
                Vector3 point = m_AvailablePoints.GetRandom();
                m_AvailablePoints.Remove(point);

                if (attempt < 5)
                {
                    List<BasePlayer> list = Pool.Get<List<BasePlayer>>();
                    Vis.Entities(point, Configuration.Warp.MonumentWarpNPCRadius, list, 1 << (int) Rust.Layer.Player_Server);

                    bool hasNpcsNear = list.Any(x => x.IsNpc);

                    Pool.FreeUnmanaged(ref list);

                    if (m_AvailablePoints.Count == 0)
                        m_AvailablePoints.AddRange(m_SpawnPoints);

                    if (hasNpcsNear)
                        return GetRandom(attempt + 1);
                }

                return point;
            }
        }
        #endregion
        
        #region NTeleportation Data Converter
        [ConsoleCommand("teleportgui.convertntp")]
        private void CommandConvertNTeleportation(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                SendReply(arg, "This command can only be run via rcon console");
                return;
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "The command usage is 'teleportgui.convertntp <Data File Directory>'\n" +
                               "If you do not have \"Data File Directory (Blank = Default)\" set in your NTeleportation config then enter the word 'null' as the argument");
                return;
            }

            string folder = arg.GetString(0);
            if (folder.Equals("null", StringComparison.OrdinalIgnoreCase))
                folder = string.Empty;

            NTeleportationLoader.LoadNTeleportationData(folder, out Dictionary<ulong, NTeleportationLoader.AdminData> adminData, out Dictionary<ulong, NTeleportationLoader.HomeData> homeData);

            int adminLocations = 0;
            int homeLocations = 0;
            
            int adminUsers = 0;
            int homeUsers = 0;

            if (adminData?.Count > 0)
            {
                foreach (KeyValuePair<ulong, NTeleportationLoader.AdminData> kvp in adminData)
                {
                    if (!m_TeleportData.Data.Users.TryGetValue(kvp.Key, out TeleportData.User userData))
                        userData = m_TeleportData.Data.Users[kvp.Key] = new TeleportData.User { LastOnlineTime = CurrentTime() };

                    userData.Locations.Clear();
                    
                    foreach (KeyValuePair<string, Vector3> location in kvp.Value.Locations)
                    {
                        userData.Locations[location.Key] = location.Value;
                        adminLocations++;
                    }

                    adminUsers++;
                }
                
                SendReply(arg, $"[TeleportGUI] Converted {adminLocations} admin locations for {adminUsers} admin users");
            }

            if (homeData?.Count > 0)
            {
                foreach (KeyValuePair<ulong, NTeleportationLoader.HomeData> kvp in homeData)
                {
                    if (!m_TeleportData.Data.Users.TryGetValue(kvp.Key, out TeleportData.User userData))
                        userData = m_TeleportData.Data.Users[kvp.Key] = new TeleportData.User { LastOnlineTime = CurrentTime() };

                    userData.Homes.Clear();
                    
                    foreach (KeyValuePair<string, Vector3> home in kvp.Value.Locations)
                    {
                        userData.Homes[home.Key] = new TeleportData.User.HomePoint { Position = home.Value };
                        homeLocations++;
                    }

                    homeUsers++;
                }
                
                SendReply(arg, $"[TeleportGUI] Converted {homeLocations} home locations for {homeUsers} home users");
            }
            
            if (adminUsers > 0 || adminLocations > 0 || homeUsers > 0 || homeLocations > 0)
                m_TeleportData.Save();
        }
        
        public static class NTeleportationLoader
        {
            private static DynamicConfigFile GetFile(string name, string folder = "")
            {
                string fileName = string.IsNullOrEmpty(folder) ? $"NTeleportation{name}" : $"{folder}{Path.DirectorySeparatorChar}{name}";

                if (!Interface.Oxide.DataFileSystem.ExistsDatafile(fileName))
                {
                    Debug.LogError($"[TeleportGUI] Datafile {name} does not exist in folder {Path.Combine(Interface.Oxide.DataDirectory, folder)}");
                    return null;
                }
                
                DynamicConfigFile file = Interface.Oxide.DataFileSystem.GetFile(fileName);
                file.Settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                file.Settings.Converters = new JsonConverter[] { new Vector3Converter(), new CustomComparerDictionaryCreationConverter<string>(StringComparer.OrdinalIgnoreCase) };
                return file;
            }

            public static void LoadNTeleportationData(string folder, out Dictionary<ulong, AdminData> admin, out Dictionary<ulong, HomeData> home)
            {
                DynamicConfigFile dataAdmin = GetFile("Admin", folder);
                admin = dataAdmin?.ReadObject<Dictionary<ulong, AdminData>>();

                DynamicConfigFile dataHome = GetFile("Home", folder);
                home = dataHome?.ReadObject<Dictionary<ulong, HomeData>>();
            }
            
            public class AdminData
            {
                [JsonProperty("pl")] 
                public Vector3 PreviousLocation { get; set; }

                [JsonProperty("l")] 
                public Dictionary<string, Vector3> Locations { get; set; } = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            }

            public class HomeData
            {
                [JsonProperty("l")] 
                public Dictionary<string, Vector3> Locations { get; set; } = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);

                [JsonProperty("t")] 
                public TeleportData Teleports { get; set; } = new TeleportData();
            }

            public class TeleportData
            {
                [JsonProperty("a")] 
                public int Amount { get; set; }

                [JsonProperty("d")] 
                public string Date { get; set; }

                [JsonProperty("t")] 
                public int Timestamp { get; set; }
            }

            public class CustomComparerDictionaryCreationConverter<T> : CustomCreationConverter<IDictionary>
            {
                private readonly IEqualityComparer<T> comparer;

                public CustomComparerDictionaryCreationConverter(IEqualityComparer<T> comparer)
                {
                    if (comparer == null)
                        throw new ArgumentNullException(nameof(comparer));
                    this.comparer = comparer;
                }

                public override bool CanConvert(Type objectType)
                {
                    return HasCompatibleInterface(objectType) && HasCompatibleConstructor(objectType);
                }

                private static bool HasCompatibleInterface(Type objectType)
                {
                    return objectType.GetInterfaces().Where(i => HasGenericTypeDefinition(i, typeof(IDictionary<,>))).Any(i => typeof(T).IsAssignableFrom(i.GetGenericArguments().First()));
                }

                private static bool HasGenericTypeDefinition(Type objectType, Type typeDefinition)
                {
                    return objectType.GetTypeInfo().IsGenericType && objectType.GetGenericTypeDefinition() == typeDefinition;
                }

                private static bool HasCompatibleConstructor(Type objectType)
                {
                    return objectType.GetConstructor(new[] { typeof(IEqualityComparer<T>) }) != null;
                }

                public override IDictionary Create(Type objectType)
                {
                    return Activator.CreateInstance(objectType, comparer) as IDictionary;
                }
            }
        }
        #endregion
    }
}
