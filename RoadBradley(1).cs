//Reference: 0Harmony
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Facepunch;
using ProtoBuf;
using System.Text;
using Network;
using Oxide.Core.Libraries.Covalence;
using HarmonyLib;


#if CARBON
using Carbon.Components;
using Carbon.Plugins;
using Carbon;
using Carbon.Base;
using Carbon.Modules;
#else
using Oxide.Ext.CarbonAliases;
using System.Reflection;
#endif

#pragma warning disable IDE0090

namespace Oxide.Plugins
{
    [Info("RoadBradley", "ThePitereq", "1.8.1")]
#if CARBON
    public class RoadBradley : CarbonPlugin
#else
    public class RoadBradley : RustPlugin
#endif
    {

        [PluginReference] private readonly Plugin Friends, Clans, Economics, ServerRewards, ShoppyStock, PopUpAPI;

        private static readonly ImageDatabaseModule imageDb = BaseModule.GetModule<ImageDatabaseModule>();

        private static HarmonyLib.Harmony harmony;
#if !CARBON
        private static readonly CUI.Handler CuiHandler = new CUI.Handler();

        private static FieldInfo seekerTargets;
        private static FieldInfo targetUpdateTime;
        private static MethodInfo killScientist;
#endif
        private static RoadBradley plugin;

        //ROAD LIST
        private static readonly Dictionary<string, RoadInfo> roads = new Dictionary<string, RoadInfo>();

        //ROAD BRADLEYS
        private static readonly HashSet<BradleyAPC> roadBradleys = new HashSet<BradleyAPC>();

        //PURCHASE COOLDOWNS - NOT LIMITS
        private static readonly Dictionary<ulong, Dictionary<string, int>> purchaseCooldowns = new Dictionary<ulong, Dictionary<string, int>>();
        private static Timer purchaseTimer;

        private static readonly Dictionary<string, DateTime> lastKillTime = new Dictionary<string, DateTime>();

        //UI CACHE
        private static readonly Dictionary<ulong, string> lastSelectedTankUi = new Dictionary<ulong, string>();

        //ITEM ID CACHE FOR BETTER UI PERFORMANCE
        private static readonly Dictionary<string, int> cachedItemIds = new Dictionary<string, int>();

        //CACHED TANK ID FOR CORRECT HEALTH DISPLAY
        private static readonly Dictionary<ulong, ulong> currentTankHealthId = new Dictionary<ulong, ulong>();

        private static readonly Dictionary<ulong, DateTime> timeToOpenCrate = new Dictionary<ulong, DateTime>();

        private static readonly Dictionary<ulong, EditorCache> activeEditors = new Dictionary<ulong, EditorCache>();

        private static readonly Dictionary<string, HashSet<ulong>> tanksWaitingToSpawn = new Dictionary<string, HashSet<ulong>>();

        private class EditorCache
        {
            public bool allowEdit = false;
            public string currentMode = string.Empty;
            public int currentIndex = 0;
            public string roadName = string.Empty;
            public List<Vector3> cachedCheckpoints;
        }

        private static string mapName;

        private static int mergedTracksCounter = 0;

        private static string coreJson = string.Empty;
        private static string healthJson = string.Empty;
        private static string editorJson = string.Empty;

        private class RoadInfo
        {
            public RoadType roadType = RoadType.Default;
            public HashSet<string> assignedRandomEvents = new HashSet<string>();
            public bool isPurchasable = false;
            public bool isNowOccupied = false;
            public bool hasBeenMerged = false;
            public string routeName = string.Empty;
            public List<Vector3> checkpoints = new List<Vector3>();
        }

        private static class TankDeathInfo
        {
            public static Vector3 tankDeathLoc = Vector3.zero;
            public static string tankDeathProfile = string.Empty;
            public static ulong tankDeathOwner = 0;
            public static ulong topDmgUser = 0;
            public static Dictionary<string, int> crateCount = new Dictionary<string, int>();
            public static bool debrisSet = false;
            public static int cratesToSet = 0;

            public static void ResetValues()
            {
                tankDeathProfile = string.Empty;
                crateCount.Clear();
                debrisSet = false;
                cratesToSet = 0;
            }
        }

        private enum RoadType
        {
            Default,
            Edited,
            Merged,
            Custom
        }

        private class BradleyCache
        {
            public int bradleyTimer = -1;
            public ulong bradleyOwner = 0;
        }

        private static readonly Dictionary<string, string> markerPrefabs = new Dictionary<string, string>()
        {
            { "crate", "assets/prefabs/tools/map/cratemarker.prefab" },
            { "chinook", "assets/prefabs/tools/map/ch47marker.prefab" },
            { "cargoship", "assets/prefabs/tools/map/cargomarker.prefab" },
            { "generic", "assets/prefabs/tools/map/genericradiusmarker.prefab" },
            { "vending", "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab" }
        };

        private void Init()
        {
            LoadConfig();
            LoadData();

            harmony = new HarmonyLib.Harmony("com.ThePitereq.RoadBradley");
#if !CARBON
            seekerTargets = typeof(SeekerTarget).GetField("seekerTargets", BindingFlags.NonPublic | BindingFlags.Static);
            targetUpdateTime = typeof(SeekingServerProjectile).GetField("nextTargetUpdateTime", BindingFlags.NonPublic);
            killScientist = typeof(BradleyAPC).GetMethod("KillSpawnedScientists", BindingFlags.NonPublic | BindingFlags.Instance);
#endif
            harmony.Patch(original: AccessTools.Method(typeof(BradleyAPC), nameof(BradleyAPC.DoSimpleAI)), prefix: new HarmonyMethod(typeof(BradleyHarmonyUpdate), nameof(BradleyHarmonyUpdate.AiLock)));
            harmony.Patch(original: AccessTools.Method(typeof(BradleyAPC), nameof(BradleyAPC.DoPhysicsMove)), prefix: new HarmonyMethod(typeof(BradleyHarmonyUpdate), nameof(BradleyHarmonyUpdate.MoveLock)));
            harmony.Patch(original: AccessTools.Method(typeof(BradleyAPC), "TrySpawnScientists"), prefix: new HarmonyMethod(typeof(BradleyHarmonyUpdate), nameof(BradleyHarmonyUpdate.ScientistSpawnLock)));
            if (config.ignoreOwnerships && !config.customTankDeath.fireballLock)
                Unsubscribe(nameof(CanLootEntity));
            if (!config.enabledBags)
                Unsubscribe(nameof(CanBeWounded));
            Unsubscribe(nameof(OnWeaponFired));
            Unsubscribe(nameof(OnPlayerAttack));
            Unsubscribe(nameof(OnLoseCondition));
        }

        private void OnServerInitialized()
        {
            ClearCache();
            plugin = this;
            FindRegularRoads();
            FindMapName();
            TryGenerateDefaultTracks();
            TryGeneratePurchasableTracks();
            PrintRoadInfo();
            LoadMessages();
            AddImages();
            GenerateBaseUi();
            GenerateHealthUi();
            GenerateEditorUi();
            RegisterCommands();
            RegisterPermissions();
            FindRequiredItemIcons();
            CheckForMissingTimedTanks();
        }

        private void Unload()
        {
            SaveData();
            harmony.UnpatchAll("com.ThePitereq.RoadBradley");
            using CUI cui = new CUI(CuiHandler);
            foreach (var player in BasePlayer.activePlayerList)
            {
                cui.Destroy("RoadBradleyUI_RoadBradley", player);
                cui.Destroy("RoadBradleyHealthUI_RoadBradleyHealth", player);
                cui.Destroy("RoadBradleyEditorUI_RoadBradleyEditor", player);
            }
            foreach (var bradley in roadBradleys.ToList())
                SafeKillBradley(bradley);
        }

        private static void ClearCache()
        {
            roads.Clear();
            roadBradleys.Clear();
            purchaseCooldowns.Clear();
            lastSelectedTankUi.Clear();
            cachedItemIds.Clear();
            currentTankHealthId.Clear();
            timeToOpenCrate.Clear();
            activeEditors.Clear();
            tanksWaitingToSpawn.Clear();
            TankDeathInfo.ResetValues();
        }
        private struct CachedResource
        {
            public string shortname;
            public int itemId;
            public ulong skin;
            public string displayName;
            public int minDrops;
            public int maxDrops;
            public Dictionary<string, CachedItem> itemList;
        }

        private struct CachedItem
        {
            public string shortname;
            public int itemId;
            public ulong skin;
            public string displayName;
            public int amountMin;
            public int amountMax;
            public float chance;
        }

        private string GetDropRates()
        {
            Dictionary<string, CachedResource> dropRates = new Dictionary<string, CachedResource>();
            StringBuilder sb = Pool.Get<StringBuilder>();
            foreach (var bradDrop in config.lootPresets)
            {
                int itemId = ItemManager.itemDictionaryByName["box.wooden.large"].itemid;
                string key = sb.Clear().Append("box.wooden.large_").Append(bradDrop.Key).ToString();
                dropRates.Add(key, new CachedResource()
                {
                    shortname = "box.wooden.large",
                    skin = 0,
                    itemId = itemId,
                    displayName = "Bradley Drop",
                    minDrops = bradDrop.Value.minItems,
                    maxDrops = bradDrop.Value.maxItems,
                    itemList = new Dictionary<string, CachedItem>()
                });
                int summedChance = 0;
                foreach (var drop in bradDrop.Value.lootTable)
                    summedChance += drop.chance;
                foreach (var drop in bradDrop.Value.lootTable)
                {
                    ItemDefinition def = ItemManager.FindItemDefinition(drop.shortname);
                    itemId = def == null ? 0 : def.itemid;
                    float chance = 100f / summedChance * drop.chance;
                    if (drop.alwaysInclude > 0 && drop.chance == 0)
                        chance = drop.alwaysInclude / bradDrop.Value.maxItems;
                    dropRates[key].itemList.Add(sb.Clear().Append(drop.shortname).Append('_').Append(drop.skin).ToString(), new CachedItem()
                    {
                        amountMin = drop.amount - drop.amountRandomize,
                        amountMax = drop.amount + drop.amountRandomize,
                        chance = chance,
                        displayName = drop.displayName,
                        itemId = itemId,
                        shortname = drop.shortname,
                        skin = drop.skin
                    });
                }
            }
            Pool.FreeUnmanaged(ref sb);
            return JsonConvert.SerializeObject(dropRates);
        }

        private static void AddImages()
        {
            List<string> urls = Pool.Get<List<string>>();
            foreach (var purchase in config.purchases.Values)
                foreach (var req in purchase.requiredItems)
                    if (!string.IsNullOrEmpty(req.iconUrl))
                        urls.Add(req.iconUrl);
            imageDb.QueueBatch(false, urls);
            Pool.FreeUnmanaged(ref urls);
        }

        private void OnNewSave()
        {
            bagData.bags.Clear();
            limitData.limits.Clear();
            SaveData();
        }

        private static void FindMapName()
        {
            mapName = string.IsNullOrEmpty(ConVar.Server.levelurl) ? "default" : ConVar.Server.levelurl.Split('/').Last();
            if (ConVar.Server.level == "HapisIsland")
                mapName = ConVar.Server.level;
            else if (!config.mapTracks.ContainsKey(mapName))
                mapName = "default";
        }

        private static void FindRequiredItemIcons()
        {
            foreach (var purchase in config.purchases.Values)
                foreach (var item in purchase.requiredItems)
                {
                    ItemDefinition def = ItemManager.FindItemDefinition(item.shortname);
                    if (def == null) continue;
                    cachedItemIds.TryAdd(item.shortname, def.itemid);
                }
        }

        private static void FindRegularRoads()
        {
            roads.Clear();
            int counter = -1;
            bool foundFirstRing = false;
            Dictionary<Vector3, string> obstacles = config.checkObstacles || config.splitRadTownRoad ? FindObstaclePrefabs() : null;
            foreach (var road in TerrainMeta.Path.Roads)
            {
                counter++;
                string counterString = counter.ToString();
                roads.TryAdd(counterString, new RoadInfo());
                roads[counterString].checkpoints = new List<Vector3>(road.Path.Points);
                if (config.asphaltOnly && road.Width <= 5) continue;
                int maxRoadCount = 1;
                if (config.roadSplitSize > 0)
                {
                    float roadLength = 0;
                    Vector3 oldPos = road.Path.Points[0];
                    foreach (var point in road.Path.Points.Skip(1))
                    {
                        roadLength += Vector3.Distance(oldPos, point);
                        oldPos = point;
                    }
                    if (roadLength / 2f > config.roadSplitSize)
                        maxRoadCount = Mathf.RoundToInt((roadLength - (roadLength % config.roadSplitSize)) / config.roadSplitSize);
                }
                int checkpointSplitStart = 0;
                for (int i = 0; i < maxRoadCount; i++)
                {
                    string editedKey = $"{counter}_Edited_{i}";
                    roads.Add(editedKey, new RoadInfo()
                    {
                        roadType = RoadType.Edited,
                        isPurchasable = true
                    });
                    if (!foundFirstRing && config.removeRingRoad && road.Path.GetStartPoint() == road.Path.GetEndPoint())
                    {
                        roads.Remove(editedKey);
                        Debug.LogWarning($"[RoadBradley WARNING] Route '{editedKey}' has been removed from the pool as it's ring road for Traveling Vendor!");
                        foundFirstRing = true;
                        continue;
                    }
                    int checkpointCount = road.Path.Points.Length;
                    int oldStart = checkpointSplitStart;
                    if (maxRoadCount > 1)
                    {
                        float roadLength = 0;
                        checkpointCount = 0;
                        Vector3 oldPos = road.Path.Points[checkpointSplitStart];
                        foreach (var point in road.Path.Points.Skip(checkpointSplitStart))
                        {
                            roadLength += Vector3.Distance(oldPos, point);
                            oldPos = point;
                            checkpointCount++;
                            checkpointSplitStart++;
                            if (roadLength >= config.roadSplitSize)
                                break;
                        }
                    }
                    if (maxRoadCount == 1 && config.splitRadTownRoad)
                    {
                        bool isRadTownRoad = false;
                        Vector3 radTownObstacle = Vector3.zero;
                        if (obstacles.ContainsValue("assets/bundled/prefabs/autospawn/monument/roadside/radtown_1.prefab"))
                            radTownObstacle = obstacles.FirstOrDefault(x => x.Value == "assets/bundled/prefabs/autospawn/monument/roadside/radtown_1.prefab").Key;
                        float checkWidth = road.Width / 2f;
                        bool firstHalf = true;
                        int noRadTownCount = 0;
                        if (radTownObstacle != Vector3.zero)
                        {
                            foreach (var checkpoint in road.Path.Points.Skip(oldStart).Take(checkpointCount))
                            {
                                noRadTownCount++;
                                if (Vector3.Distance(checkpoint, radTownObstacle) < checkWidth)
                                {
                                    isRadTownRoad = true;
                                    firstHalf = noRadTownCount >= checkpointCount / 2;
                                    break;
                                }
                            }
                        }
                        if (isRadTownRoad)
                        {
                            if (firstHalf)
                                checkpointCount = noRadTownCount - 8;
                            else
                            {
                                oldStart = noRadTownCount + 8;
                                checkpointCount = checkpointCount - oldStart;
                            }
                            foundFirstRing = true;
                        }
                    }
                    bool isLooped = (checkpointCount > 0 && Vector3.Distance(road.Path.Points[oldStart], road.Path.Points[checkpointCount - 1]) < 20f);
                    int checkpointCounter = 0;
                    bool failedCheck = false;
                    foreach (var checkpoint in road.Path.Points.Skip(oldStart).Take(checkpointCount))
                    {
                        checkpointCounter++;
                        if (!isLooped && (checkpointCounter <= config.removeCheckpointAmount || checkpointCounter >= checkpointCount - config.removeCheckpointAmount)) continue;
                        if (config.checkObstacles)
                        {
                            float checkWidth = road.Width / 2f;
                            foreach (var obstacle in obstacles)
                            {
                                if (Vector3.Distance(checkpoint, obstacle.Key) < checkWidth)
                                {
                                    if (config.removeObstacleRoads)
                                    {
                                        Debug.LogWarning($"[RoadBradley WARNING] There is an obstacle '{obstacle.Value}' on the road '{counter}' at '{obstacle.Key}'. Removing road from the road pool!");
                                        roads.Remove(editedKey);
                                        failedCheck = true;
                                        break;
                                    }
                                    else
                                        Debug.LogWarning($"[RoadBradley WARNING] There is an obstacle '{obstacle.Value}' on the road '{counter}' at '{obstacle.Key}'.");
                                }
                            }
                            if (failedCheck) break;
                        }
                        Vector3 checkpointFixed = checkpoint;
                        checkpointFixed.y += 2f;
                        RaycastHit hit;
                        if (Physics.Raycast(checkpointFixed, Vector3.down, out hit, 4f, Layers.World))
                            checkpointFixed = hit.point;
                        else
                            checkpointFixed.y = TerrainMeta.HeightMap.GetHeight(checkpointFixed) + 0.25f;
                        roads[editedKey].checkpoints.Add(checkpointFixed);
                    }
                    if (failedCheck) continue;
                    checkpointCount = roads[editedKey].checkpoints.Count;
                    if (checkpointCount < config.minRouteLength)
                    {
                        roads.Remove(editedKey);
                        Debug.LogWarning($"[RoadBradley WARNING] Route '{editedKey}' is too small for a bradley! It have less than {config.minRouteLength} checkpoints. Cannot be used.");
                        continue;
                    }
                    if (!isLooped)
                    {
                        checkpointCount -= 2;
                        while (checkpointCount > 0)
                        {
                            roads[editedKey].checkpoints.Add(roads[editedKey].checkpoints[checkpointCount]);
                            checkpointCount--;
                        }
                    }
                }
            }
            foreach (var road in roadData.roads)
            {
                roads.Add(road.Key, new RoadInfo()
                {
                    roadType = RoadType.Custom,
                    checkpoints = road.Value
                });
            }
        }

        private static Dictionary<Vector3, string> FindObstaclePrefabs()
        {
            Dictionary<Vector3, string> obstacles = new Dictionary<Vector3, string>();
            foreach (PrefabData entry in World.Serialization.world.prefabs)
            {
                if (TerrainMeta.TopologyMap.GetTopology(entry.position, TerrainTopology.ROAD) && !obstacles.ContainsKey(entry.position))
                {
                    string prefabName = StringPool.Get(entry.id).ToLower();
                    bool shouldAdd = true;
                    foreach (var whitelist in config.obstacleWhitelist)
                    {
                        if (prefabName.Contains(whitelist.ToLower()))
                        {
                            shouldAdd = false;
                            break;
                        }
                    }
                    if (shouldAdd)
                        obstacles.Add(entry.position, prefabName);
                }
            }
            return obstacles;
        }

        private void RegisterCommands()
        {
#if CARBON
            cmd.AddConsoleCommand(Community.Protect("RoadBradleyUI"), this, nameof(RoadBradleyConsoleCommand), @protected: true);
            cmd.AddConsoleCommand(Community.Protect("RoadBradleyEditorUI"), this, nameof(RoadBradleyEditorConsoleCommand), @protected: true);
            foreach (var command in config.commands)
                cmd.AddCovalenceCommand(command, this, nameof(RoadBradleyCommand));
            cmd.AddCovalenceCommand("tankroute", this, nameof(RoadBradleyRouteCommand));
#else
            cmd.AddConsoleCommand("RoadBradleyUI", this, nameof(RoadBradleyConsoleCommand));
            cmd.AddConsoleCommand("RoadBradleyEditorUI", this, nameof(RoadBradleyEditorConsoleCommand));
            foreach (var command in config.commands)
                cmd.AddChatCommand(command, this, nameof(RoadBradleyCommand));
            cmd.AddChatCommand("tankroute", this, nameof(RoadBradleyRouteCommand));
#endif
            AddCovalenceCommand("spawnrbtank", nameof(SpawnTankCommand));
            AddCovalenceCommand("tankcount", nameof(TankCountCommand));
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission("roadbradley.admin", this);
            permission.RegisterPermission("roadbradley.bypass", this);
            foreach (var purchase in config.purchases.Values)
            {
                foreach (var perm in purchase.purchasePermissionLimits.Keys)
                    if (!permission.PermissionExists(perm, this))
                        permission.RegisterPermission(perm, this);
                if (purchase.permission != "" && !permission.PermissionExists(purchase.permission, this))
                    permission.RegisterPermission(purchase.permission, this);
            }
        }

        private void RoadBradleyConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            using CUI cui = new CUI(CuiHandler);
            switch (arg.Args[0])
            {
                case "close":
                    cui.Destroy("RoadBradleyUI_RoadBradley", player);
                    break;
                case "profile":
                    CUI.Handler.UpdatePool elements = cui.UpdatePool();
                    StringBuilder sb = Pool.Get<StringBuilder>();
                    string oldProfile = lastSelectedTankUi[player.userID];
                    lastSelectedTankUi[player.userID] = arg.Args[1];
                    UpdateDisplayedTankUi(player, sb, cui, elements);
                    cui.Destroy("RoadBradleyUI_ItemSection", player);
                    UpdateLeftButton(player, oldProfile, lastSelectedTankUi[player.userID], sb, cui, elements);
                    elements.Send(player);
                    Pool.FreeUnmanaged(ref sb);
                    break;
                case "spawn":
                    cui.Destroy("RoadBradleyUI_RoadBradley", player);
                    PurchaseTank(player, lastSelectedTankUi[player.userID]);
                    break;
                case "hint":
                    PopUpAPI?.Call("ShowPopUp", player, config.popUpProfile, Lang(arg.Args[1], player.UserIDString));
                    break;
                case "reqItem":
                    string profile = arg.Args[1];
                    string slotIdentifier = arg.Args[2];
                    PopUpAPI?.Call("ShowPopUp", player, config.popUpProfile, Lang($"ReqItemHint_{profile}_{slotIdentifier}", player.UserIDString));
                    break;
            }
        }

        private void RoadBradleyEditorConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, "roadbradley.admin")) return;
            using CUI cui = new CUI(CuiHandler);
            switch (arg.Args[0])
            {
                case "close":
                    SendReply(player, Lang("EditorClosedNotSaved", player.UserIDString));
                    cui.Destroy("RoadBradleyEditorUI_RoadBradleyEditor", player);
                    activeEditors.Remove(player.userID);
                    if (activeEditors.Count == 0)
                    {
                        Unsubscribe(nameof(OnWeaponFired));
                        Unsubscribe(nameof(OnPlayerAttack));
                        Unsubscribe(nameof(OnLoseCondition));
                    }
                    break;
                case "showCheckpoints":
                    DisplayRoute(player);
                    break;
                case "showHelp":
                    SendReply(player, Lang("EditorHint", player.UserIDString));
                    break;
                case "saveExit":
                    SaveRouteAndExit(player);
                    break;
            }
        }

        private void RoadBradleyCommand(BasePlayer player) => OpenRoadBradleyUI(player);

        private void RoadBradleyRouteCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "roadbradley.admin"))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }
            if (args.Length == 0)
            {
                SendReply(player, Lang("RouteHelpV4", player.UserIDString));
                return;
            }
            string type = args[0].ToLower();
            switch (type)
            {
                case "new":
                    if (args.Length == 1)
                    {
                        SendReply(player, Lang("InvalidNewRoadSyntax", player.UserIDString));
                        return;
                    }
                    TryCreateRoute(player, args[1]);
                    break;
                case "default":
                case "edited":
                case "merged":
                case "custom":
                    ListRoutes(player, type);
                    break;
                case "clone":
                    if (args.Length == 1)
                    {
                        SendReply(player, Lang("InvalidRouteNameSyntax", player.UserIDString));
                        return;
                    }
                    string routeName = args[1];
                    CloneRoute(player, routeName);
                    break;
                default:
                    TryEditRoute(player, args[0]);
                    break;
            }
        }

        private void SpawnTankCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin && !permission.UserHasPermission(user.Id, "roadbradley.admin"))
            {
                user.Reply(Lang("NoPermission", user.Id));
                return;
            }
            if (args.Length != 2)
            {
                user.Reply(Lang("SpawnHelp", user.Id));
                return;
            }
            string profile = args[0];
            string routeOrPlayer = args[1];
            if (!config.profiles.ContainsKey(profile))
            {
                user.Reply(Lang("ProfileNotFound", user.Id, profile));
                return;
            }
            ulong userId;
            if (ulong.TryParse(routeOrPlayer, out userId) && userId > 1000000000)
            {
                BasePlayer spawnUser = BasePlayer.FindByID(userId);
                if (spawnUser == null)
                {
                    user.Reply(Lang("PlayerOffline", user.Id, userId));
                    return;
                }
                SpawnRoadBradley(profile, spawnUser);
                user.Reply(Lang("TankSpawnedUser", user.Id, userId));
                Puts($"Spawned tank with profile {profile} for {spawnUser.displayName} ({spawnUser.UserIDString}).");
            }
            else
            {
                if (!config.spawns.ContainsKey(routeOrPlayer))
                {
                    user.Reply(Lang("RouteNotFound", user.Id, routeOrPlayer));
                    return;
                }
                if (!config.mapTracks[mapName].Contains(routeOrPlayer))
                {
                    user.Reply(Lang("RouteNotAddedToConfig", user.Id, routeOrPlayer, mapName));
                    return;
                }
                SpawnRoadBradley(profile, null, routeOrPlayer);
                user.Reply(Lang("TankSpawned", user.Id));
                Puts($"Spawned tank with profile {profile} on route {routeOrPlayer}.");
            }
        }


        private void TankCountCommand(IPlayer user)
        {
            if (!user.IsAdmin && !permission.UserHasPermission(user.Id, "roadbradley.admin"))
            {
                user.Reply(Lang("NoPermission", user.Id));
                return;
            }
            StringBuilder sb = Pool.Get<StringBuilder>();
            sb.Clear().Append("Road Bradleys Active: ").Append(roadBradleys.Count).AppendLine()
                .Append("Random Spawned: ").Append(roadBradleys.Count(x => x.GetComponent<RoadBradleyController>().ownerId == 0)).AppendLine()
                .Append("Player Owned: ").Append(roadBradleys.Count(x => x.GetComponent<RoadBradleyController>().ownerId != 0));
            user.Reply(sb.ToString());
            Pool.FreeUnmanaged(ref sb);
        }

        private static readonly Dictionary<string, RoadType> roadTypeConvert = new Dictionary<string, RoadType>()
        {
            { "default", RoadType.Default },
            { "edited", RoadType.Edited },
            { "merged", RoadType.Merged },
            { "custom", RoadType.Custom }
        };

        private void ListRoutes(BasePlayer player, string type)
        {
            StringBuilder sb = Pool.Get<StringBuilder>();
            sb.Clear().AppendLine(Lang("AvailableRoads", player.UserIDString, type));
            int counter = 0;
            foreach (var road in roads)
                if (road.Value.roadType == roadTypeConvert[type])
                {
                    counter++;
                    sb.AppendLine(Lang("RoadListed", player.UserIDString, road.Key));
                }
            if (counter == 0)
                sb.AppendLine(Lang("NoRoadFound", player.UserIDString));
            SendReply(player, sb.ToString());
            Pool.FreeUnmanaged(ref sb);
        }

        private void TryEditRoute(BasePlayer player, string roadKey)
        {
            if (!roads.ContainsKey(roadKey))
            {
                SendReply(player, Lang("NoRoadNamedFound", player.UserIDString, roadKey));
                return;
            }
            StartRouteEdit(player, roadKey);
        }

        private void TryCreateRoute(BasePlayer player, string roadName)
        {
            if (roads.ContainsKey(roadName))
            {
                SendReply(player, Lang("RoadAlreadyCreated", player.UserIDString, roadName));
                return;
            }
            roads.Add(roadName, new RoadInfo()
            {
                roadType = RoadType.Custom
            });
            StartRouteEdit(player, roadName);
        }

        private void CloneRoute(BasePlayer player, string roadName)
        {
            if (!roads.ContainsKey(roadName))
            {
                SendReply(player, Lang("RoadNotExist", player.UserIDString, roadName));
                return;
            }
            string fixedName = $"{roadName}_Clone";
            if (roads.ContainsKey(fixedName))
            {
                SendReply(player, Lang("AlreadyCloned", player.UserIDString));
                return;
            }
            roads.Add(fixedName, new RoadInfo()
            {
                roadType = RoadType.Custom,
                checkpoints = roads[roadName].checkpoints
            });
            StartRouteEdit(player, fixedName);
        }

        private void StartRouteEdit(BasePlayer player, string roadName)
        {
            CuiHelper.DestroyUi(player, "RoadBradleyEditorUI_RoadBradleyEditor");
            SendJson(player, editorJson);
            GiveEditorTool(player);
            if (activeEditors.Count == 0)
            {
                Subscribe(nameof(OnWeaponFired));
                Subscribe(nameof(OnPlayerAttack));
                Subscribe(nameof(OnLoseCondition));
            }
            activeEditors[player.userID] = new EditorCache()
            {
                allowEdit = roads[roadName].roadType == RoadType.Custom,
                cachedCheckpoints = new List<Vector3>(roads[roadName].checkpoints),
                roadName = roadName,
            };
            UpdateRouteEditor(player);
            DisplayRoute(player);
            if (roads[roadName].roadType != RoadType.Custom)
                SendReply(player, Lang("RoadPreviewOnly", player.UserIDString));
            SendReply(player, Lang("RouteLoaded", player.UserIDString, roadName));
            SendReply(player, Lang("EditorHint", player.UserIDString));
        }

        private void SaveRouteAndExit(BasePlayer player)
        {
            if (roads[activeEditors[player.userID].roadName].roadType == RoadType.Custom)
            {
                roadData.roads.TryAdd(activeEditors[player.userID].roadName, new List<Vector3>(activeEditors[player.userID].cachedCheckpoints));
                SendReply(player, Lang("RouteSaved", player.UserIDString, activeEditors[player.userID].roadName));
                SaveData();
            }
            else
                SendReply(player, Lang("EditorClosedNotSaved", player.UserIDString));
            using CUI cui = new CUI(CuiHandler);
            cui.Destroy("RoadBradleyEditorUI_RoadBradleyEditor", player);
            activeEditors.Remove(player.userID);
            if (activeEditors.Count == 0)
            {
                Unsubscribe(nameof(OnWeaponFired));
                Unsubscribe(nameof(OnPlayerAttack));
                Unsubscribe(nameof(OnLoseCondition));
            }
        }

        private void OnPlayerConnected(BasePlayer player) => CheckForMissingTimedTanks();

        private static void GiveEditorTool(BasePlayer player)
        {
            Item weapon = ItemManager.CreateByName("rifle.semiauto", 1, 0);
            weapon.name = "Road Bradley Editor Tool";
            if (weapon.MoveToContainer(player.inventory.containerBelt))
            {
                BaseProjectile weaponProj = weapon.GetHeldEntity() as BaseProjectile;
                if (weaponProj != null)
                {
                    weaponProj.primaryMagazine.contents = weaponProj.primaryMagazine.capacity;
                    weaponProj.SendNetworkUpdateImmediate();
                }
                Item silencer = ItemManager.CreateByName("weapon.mod.silencer");
                silencer.MoveToContainer(weapon.contents);
            }
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            if (!activeEditors.ContainsKey(player.userID)) return;
            projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
            projectile.SendNetworkUpdateImmediate();
        }

        private void OnLoseCondition(Item item, ref float amount)
        {
            BasePlayer player = item.GetOwnerPlayer();
            if (player == null || !activeEditors.ContainsKey(player.userID)) return;
            amount = 0;
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info == null || !activeEditors.ContainsKey(attacker.userID)) return;
            EditorCache controller = activeEditors[attacker.userID];
            if (roads[controller.roadName].roadType != RoadType.Custom) return;
            Vector3 hitPosition = info.HitPositionWorld + new Vector3(0, config.editorHeightOffset, 0);
            if (controller.currentMode == "")
            {
                if (attacker.serverInput.IsDown(BUTTON.SPRINT))
                {
                    int counter = 0;
                    foreach (var checkpoint in controller.cachedCheckpoints)
                    {
                        if (Vector3.Distance(hitPosition, checkpoint) < 2f)
                        {
                            controller.currentMode = "Insert";
                            controller.currentIndex = counter;
                            SendReply(attacker, Lang("InsertInfo", attacker.UserIDString, counter));
                            return;
                        }
                        counter++;
                    }
                }
                else if (attacker.serverInput.IsDown(BUTTON.DUCK))
                {
                    int counter = 0;
                    foreach (var checkpoint in controller.cachedCheckpoints)
                    {
                        if (Vector3.Distance(hitPosition, checkpoint) < 2f)
                        {
                            controller.cachedCheckpoints.RemoveAt(counter);
                            UpdateCheckpointCount(attacker);
                            SendReply(attacker, Lang("CheckpointRemove", attacker.UserIDString, counter));
                            DisplayRoute(attacker, 2);
                            return;
                        }
                        counter++;
                    }
                }
                else
                {
                    int counter = 0;
                    foreach (var checkpoint in controller.cachedCheckpoints)
                    {
                        if (Vector3.Distance(hitPosition, checkpoint) < 2f)
                        {
                            controller.currentMode = "Update";
                            controller.currentIndex = counter;
                            SendReply(attacker, Lang("UpdateInfo", attacker.UserIDString, counter));
                            return;
                        }
                        counter++;
                    }
                    controller.cachedCheckpoints.Add(hitPosition);
                    UpdateCheckpointCount(attacker);
                    SendReply(attacker, Lang("CheckpointAdd", attacker.UserIDString));
                    DisplayRoute(attacker, 2);
                }
            }
            else if (controller.currentMode == "Insert")
            {
                controller.cachedCheckpoints.Insert(controller.currentIndex + 1, hitPosition);
                controller.currentMode = "";
                UpdateCheckpointCount(attacker);
                SendReply(attacker, Lang("CheckpointInsert", attacker.UserIDString));
                DisplayRoute(attacker, 2);
            }
            else if (controller.currentMode == "Update")
            {
                controller.cachedCheckpoints[controller.currentIndex] = hitPosition;
                controller.currentMode = "";
                UpdateCheckpointCount(attacker);
                SendReply(attacker, Lang("CheckpointUpdate", attacker.UserIDString));
                DisplayRoute(attacker, 2);
            }
        }

        private static void DisplayRoute(BasePlayer player, float time = 15f)
        {
            if (!activeEditors.ContainsKey(player.userID) || activeEditors[player.userID].cachedCheckpoints.Count == 0) return;
            if (player.net.connection.authLevel == 0)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
            }
            Vector3 firstCheckPoint = activeEditors[player.userID].cachedCheckpoints[0];
            int counter = 0;
            foreach (var checkpoint in activeEditors[player.userID].cachedCheckpoints)
            {
                if (firstCheckPoint != checkpoint)
                {
                    player.SendConsoleCommand("ddraw.line", time, Color.blue, firstCheckPoint, checkpoint);
                    firstCheckPoint = checkpoint;
                }
                player.SendConsoleCommand("ddraw.text", time, Color.green, checkpoint, counter);
                counter++;
            }
            if (player.net.connection.authLevel == 0)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }

        private object CanBradleyApcTarget(BradleyAPC apc, BasePlayer player)
        {
            if (!roadBradleys.Contains(apc)) return null;
            if (player.IsNpc) return config.targetNpc ? true : null;
            RoadBradleyController controller = apc.GetComponent<RoadBradleyController>();
            if (config.enableTargetHook)
            {
                object call = Interface.Call("CanRoadBradleyTarget", apc, player);
                if (call is bool) return call;
            }
            if (controller.targets.Contains(player))
            {
                currentTankHealthId[player.userID] = apc.net.ID.Value;
                return null;
            }
            else
            {
                ProfileConfig configValue = config.profiles[controller.profile];
                if (!config.targetSleepers && player.IsSleeping()) return false;
                Item activeItem;
                bool validTarget = Vector3.Distance(player.transform.position, apc.transform.position) <= configValue.maxTargetDistance;
                if (validTarget)
                {
                    if (controller.ownerId != 0 && config.purchases[controller.profile].ownerTarget)
                    {
                        if (ValidatePerm(controller.ownerId, player))
                        {
                            controller.AddTarget(player);
                            return null;
                        }
                        else return false;
                    }
                    else
                    {
                        controller.AddTarget(player);
                        return null;
                    }
                }
                activeItem = player.GetActiveItem();
                validTarget = activeItem != null && configValue.targetItems.Contains(activeItem.info.shortname);
                if (validTarget)
                {
                    if (controller.ownerId != 0 && config.purchases[controller.profile].ownerTarget)
                    {
                        if (ValidatePerm(controller.ownerId, player))
                        {
                            controller.AddTarget(player);
                            return null;
                        }
                        else return false;
                    }
                    else
                    {
                        controller.AddTarget(player);
                        return null;
                    }
                }
                else return false;
            }
        }

        private static readonly List<string> validNullDamageEntities = new List<string>()
        {
            "cupboard.tool.deployed",
            "bed_deployed",
            "sleepingbag_leather_deployed",
            "beachtowel.deployed"
        };

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.ShortPrefabName == "bradleyapc")
            {
                BradleyAPC apc = entity as BradleyAPC;
                if (!roadBradleys.Contains(apc)) return null;
                if (info.Initiator != null && info.Initiator == apc) return config.hookReturnValue;
                BasePlayer player = info?.InitiatorPlayer;
                if (player == null) return null;
                RoadBradleyController controller = apc.GetComponent<RoadBradleyController>();
                if (controller == null) return null;
                if (controller.targets.Contains(player))
                {
                    controller.damageDealt.TryAdd(player.userID, 0);
                    controller.damageDealt[player.userID] += info.damageTypes.Total();
                    currentTankHealthId[player.userID] = apc.net.ID.Value;
                    controller.CheckMLRSSpawn();
                    return null;
                }
                if (controller.ownerId != 0 && config.purchases[controller.profile].ownerDamage)
                {
                    if (ValidatePerm(controller.ownerId, player))
                    {
                        controller.AddTarget(player);
                        controller.damageDealt.TryAdd(player.userID, 0);
                        controller.damageDealt[player.userID] += info.damageTypes.Total();
                        controller.CheckMLRSSpawn();
                        return null;
                    }
                    else
                    {
                        SendReply(player, Lang("CannotDamage", player.UserIDString));
                        return config.hookReturnValue;
                    }
                }
                else
                {
                    controller.AddTarget(player);
                    controller.damageDealt.TryAdd(player.userID, 0);
                    controller.damageDealt[player.userID] += info.damageTypes.Total();
                    controller.CheckMLRSSpawn();
                    return null;
                }
            }
            if (entity.OwnerID == 0) return null;
            if (info.Initiator == null || info.Initiator.ShortPrefabName != "bradleyapc") return null;
            BradleyAPC apc2 = info.Initiator as BradleyAPC;
            if (config.disableUnownedDamage && apc2.OwnerID == 0 && entity is DecayEntity) return config.hookReturnValue;
            if (config.disableTcBagDamage && validNullDamageEntities.Contains(entity.ShortPrefabName)) return config.hookReturnValue;
            if (info.WeaponPrefab == null) return null;
            if (config.callerDamage && info.WeaponPrefab.ShortPrefabName == "maincannonshell")
            {
                RoadBradleyController controller = apc2.GetComponent<RoadBradleyController>();
                if (controller == null || controller.ownerId == 0 || controller.ownerId == entity.OwnerID) return null;
                if (config.callerTeamCheck)
                {
                    RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindPlayersTeam(entity.OwnerID);
                    if (team != null && team.members.Contains(controller.ownerId)) return null;
                }
                if (controller.targets.Any(x => x.userID == entity.OwnerID)) return null;
                controller.damageAuths++;
                if (controller.damageAuths == config.damageAuths)
                    controller.tankRemainingTime = 1;
                if (controller.owner != null)
                {
                    if (controller.damageAuths % config.damageAuthsRemind == 0 && controller.damageAuths < config.damageAuths)
                        SendReply(controller.owner, Lang("FightUnauthorizedInfo", controller.owner.UserIDString, controller.damageAuths, config.damageAuths));
                    if (controller.damageAuths == config.damageAuths)
                        SendReply(controller.owner, Lang("FightLostUnauthorized", controller.owner.UserIDString, config.damageAuths));
                }
                return config.hookReturnValue;
            }
            else if (info.WeaponPrefab.ShortPrefabName == "rocket_mlrs")
            {
                //TODO MAYBE ADD ANOTHER DICTIONARY FOR MLRS AND CHECK FOR THEIR NET.ID IF THEY BELONG TO TANK IF TANK IS PRESENT JUST CONTINUE AND IF NOT JUST CANCEL BEHAVIOUR
                if (apc2.IsDestroyed) return config.hookReturnValue;
                RoadBradleyController controller = apc2.GetComponent<RoadBradleyController>();
                if (controller == null) return null;
                bool isTankOwned = controller.ownerId != 0;
                if (!isTankOwned) return null;
                DecayEntity hitEntity = entity as DecayEntity;
                if (hitEntity == null)
                {
                    BasePlayer hitPlayer = entity as BasePlayer;
                    if (hitPlayer != null)
                    {
                        if (!isTankOwned) return null;
                        if (!controller.targets.Contains(hitPlayer)) return config.hookReturnValue;
                        return null;
                    }
                    else return null;
                }
                if (hitEntity.OwnerID == controller.owner.userID) return null;
                BuildingPrivlidge tc = hitEntity.GetBuildingPrivilege();
                if (tc == null) return null;
                if (!tc.IsAuthed(controller.ownerId)) return config.hookReturnValue;
            }
            return null;
        }

        private object CanBeWounded(BasePlayer player, HitInfo info)
        {
            if (info == null) return null;
            return OnKilledByBradley(player, info, true);
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player is NPCPlayer) return null;
            foreach (var tank in roadBradleys)
            {
                RoadBradleyController controller = tank?.GetComponent<RoadBradleyController>();
                controller.targets.Remove(player);
            }
            if (config.enabledBags && info != null)
                return OnKilledByBradley(player, info);
            else
                return null;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            foreach (var tank in roadBradleys)
            {
                RoadBradleyController controller = tank?.GetComponent<RoadBradleyController>();
                controller.RemoveTarget(player);
            }
        }

        private object CanDropActiveItem(BasePlayer player)
        {
            if (player is NPCPlayer) return null;
            if (player.lastAttacker != null && player.lastAttacker is BradleyAPC) return false;
            return null;
        }

        private object CanLootEntity(BasePlayer player, LockedByEntCrate crate)
        {
            if (!config.ignoreOwnerships)
            {
                if (crate.OwnerID != 0 && crate.HasFlag(BaseEntity.Flags.Unused23))
                {
                    if (!ValidatePerm(crate.OwnerID, player))
                    {
                        SendReply(player, Lang("CannotLoot", player.UserIDString));
                        return false;
                    }
                }
            }
            if (!config.customTankDeath.fireballLock && timeToOpenCrate.ContainsKey(crate.net.ID.Value))
            {
                if (DateTime.Now < timeToOpenCrate[crate.net.ID.Value])
                {
                    int seconds = (int)Math.Ceiling((timeToOpenCrate[crate.net.ID.Value] - DateTime.Now).TotalSeconds);
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", crate.transform.position);
                    SendReply(player, Lang("CannotOpenYet", player.UserIDString, seconds));
                    return false;
                }
                timeToOpenCrate.Remove(crate.net.ID.Value);
            }
            return null;
        }

        private void SetupCrate(LockedByEntCrate crate)
        {
            if (TankDeathInfo.cratesToSet <= 0) return;
            if (Vector3.Distance(crate.transform.position, TankDeathInfo.tankDeathLoc) > 10) return;
            crate.SetFlag(BaseEntity.Flags.Unused23, true);
            crate.inventory.capacity = 48;
            crate.panelName = "generic_resizable";
            if (TankDeathInfo.tankDeathOwner != 0 && config.purchases[TankDeathInfo.tankDeathProfile].ownerLoot)
                crate.OwnerID = TankDeathInfo.tankDeathOwner;
            if (config.limitToTopDamage && crate.OwnerID == 0 && TankDeathInfo.topDmgUser != 0)
                crate.OwnerID = TankDeathInfo.topDmgUser;
            if (config.profiles[TankDeathInfo.tankDeathProfile].lootPreset != "")
            {
                foreach (var item in crate.inventory.itemList.ToList())
                {
                    item.GetHeldEntity()?.Kill();
                    item.RemoveFromContainer();
                    item.Remove();
                }
                ItemManager.DoRemoves();
                string preset = config.profiles[TankDeathInfo.tankDeathProfile].lootPreset;
                if (!config.lootPresets.ContainsKey(preset))
                {
                    Puts($"PRESET {preset} IS MISSING FROM CONFIGURATION! CRATES WILL BE BROKEN!");
                    return;
                }
                foreach (var item in config.lootPresets[preset].lootTable)
                    if (item.alwaysInclude > 0)
                    {
                        string key = $"{item.shortname}_{item.skin}";
                        if (item.maxPerLoot > 0 && TankDeathInfo.crateCount.ContainsKey(key) && TankDeathInfo.crateCount[key] >= item.maxPerLoot) continue;
                        if (item.alwaysInclude > Core.Random.Range(0f, 100f))
                        {
                            CreateItem(item, crate.inventory);
                            TankDeathInfo.crateCount.TryAdd(key, 0);
                            TankDeathInfo.crateCount[key]++;
                        }
                    }
                int itemAmount = Core.Random.Range(config.lootPresets[preset].minItems, config.lootPresets[preset].maxItems + 1);
                int sumChance = 0;
                foreach (var item in config.lootPresets[preset].lootTable)
                    sumChance += item.chance;
                for (int i = 0; i < itemAmount; i++)
                {
                    int chance = Core.Random.Range(0, sumChance + 1);
                    int currentChance = 0;
                    foreach (var item in config.lootPresets[preset].lootTable)
                    {
                        currentChance += item.chance;
                        if (currentChance >= chance)
                        {
                            if (!CreateItem(item, crate.inventory)) continue;
                            break;
                        }
                    }
                }
            }
            crate.inventory.capacity = crate.inventory.itemList.Count;
            if (config.customTankDeath.fireballLock)
            {
                timer.Once(config.profiles[TankDeathInfo.tankDeathProfile].lockTime, () =>
                {
                    if (crate == null) return;
                    FireBall lockingEnt = crate.lockingEnt?.GetComponent<FireBall>();
                    if (lockingEnt != null && !lockingEnt.IsDestroyed)
                    {
                        lockingEnt.CancelInvoke(lockingEnt.Extinguish);
                        lockingEnt.Extinguish();
                    }
                    crate.CancelInvoke(crate.Think);
                    crate?.SetLocked(false);
                });
            }
        }

        private void OnEntitySpawned(TimedExplosive entity)
        {
            if (entity.ShortPrefabName != "maincannonshell") return;
            List<BradleyAPC> bradleys = Pool.Get<List<BradleyAPC>>();
            bradleys.Clear();
            Vis.Entities(entity.transform.position, 3f, bradleys);
            if (bradleys.Count == 0) return;
            entity.creatorEntity = bradleys[0];
            RoadBradleyController controller = bradleys[0].GetComponent<RoadBradleyController>();
            Pool.FreeUnmanaged(ref bradleys);
            if (controller == null) return;
            entity.explosionRadius = config.profiles[controller.profile].cannonExplosionRadius;
            for (int i = 0; i < entity.damageTypes.Count; i++)
            {
                if (entity.damageTypes[i].type == DamageType.Blunt)
                    entity.damageTypes[i].amount = config.profiles[controller.profile].cannonBluntDamage;
            }
            if (config.profiles[controller.profile].cannonExplosionDamage != 0)
            {
                DamageTypeEntry explosionDamage = new DamageTypeEntry() { type = DamageType.Explosion, amount = config.profiles[controller.profile].cannonExplosionDamage };
                entity.damageTypes.Add(explosionDamage);
            }
        }

        private void OnGibSpawned(HelicopterDebris entity, string profile)
        {
            if (config.profiles[profile].gibsScale && config.profiles[profile].tankScale != 1)
            {
                SphereEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", entity.transform.position) as SphereEntity;
                sphere.Spawn();
                entity.SetParent(sphere, true);
                SphereInstantLerp(sphere, config.profiles[profile].tankScale);
                if (config.customTankDeath.hideGibSpheres)
                {
                    timer.Once(1f, () => {
                        if (sphere != null)
                            sphere.transform.position += new Vector3(0, -5, 0);
                        if (entity != null)
                            entity.transform.position += new Vector3(0, 5, 0);
                    });
                }
            }
            entity.InitializeHealth(config.profiles[profile].gibsHealth, config.profiles[profile].gibsHealth);
            entity.tooHotUntil = Time.realtimeSinceStartup + config.profiles[profile].gibsTime;
            entity.SendNetworkUpdate();
        }

        private object CanRenameBed(BasePlayer player, SleepingBag bed, string bedName)
        {
            string newToLower = bedName.ToLower();
            if (bed.niceName.ToLower().Contains("bradley") && !newToLower.Contains("bradley"))
            {
                if (bed.OwnerID != player.userID)
                {
                    SendReply(player, Lang("NotBedOwner", player.UserIDString));
                    return false;
                }
                bagData.bags[player.userID].Remove(bed.net.ID.Value);
                SendReply(player, Lang("RespawnPointRemoved", player.UserIDString));
                return null;
            }
            else if (newToLower.Contains("bradley"))
            {
                if (bed.OwnerID != player.userID)
                {
                    SendReply(player, Lang("NotBedOwner", player.UserIDString));
                    return false;
                }
                bagData.bags.TryAdd(player.userID, new List<ulong>());
                if (bagData.bags[player.userID].Count > config.maxBags)
                {
                    SendReply(player, Lang("TooManyBags", player.UserIDString));
                    return false;
                }
                bagData.bags[player.userID].Add(bed.net.ID.Value);
                SendReply(player, Lang("RespawnPointSet", player.UserIDString));
                return null;
            }
            return null;
        }

        private bool CreateItem(ItemConfig item, ItemContainer storage)
        {
            int amount = item.amount;
            if (item.amountRandomize != 0)
                amount = Core.Random.Range(item.amount - item.amountRandomize, item.amount + item.amountRandomize + 1);
            Item itemOutput = ItemManager.CreateByName(item.shortname, amount, item.skin);
            if (itemOutput == null)
            {
                PrintWarning($"Couldn't create item with shortname {item.shortname}. This item doesn't exist!");
                return false;
            }
            if (!string.IsNullOrEmpty(item.displayName))
                itemOutput.name = item.displayName;
            itemOutput.MoveToContainer(storage);
            if (item.additionalItems.Count == 0) return true;
            foreach (var additionalItem in item.additionalItems)
            {
                Item additionalItemOutput = ItemManager.CreateByName(additionalItem.shortname, additionalItem.amount, additionalItem.skin);
                if (!string.IsNullOrEmpty(additionalItem.displayName))
                    additionalItemOutput.name = additionalItem.displayName;
                additionalItemOutput.MoveToContainer(storage);
            }
            return true;
        }

        private void OnEntityKill(BradleyAPC apc)
        {
            if (apc?.net == null) return;
            RoadBradleyController controller = apc.GetComponent<RoadBradleyController>();
            if (controller == null) return;
            using CUI cui = new CUI(CuiHandler);
            foreach (var player in controller.targets)
                cui.Destroy("RoadBradleyHealthUI_RoadBradleyHealth", player);
            if (controller.ownerId == 0)
            {
                int waitTime = Core.Random.Range(config.spawns[controller.spawnProfile].respawnTime - config.spawns[controller.spawnProfile].respawnTimeRandomize, config.spawns[controller.spawnProfile].respawnTime + config.spawns[controller.spawnProfile].respawnTimeRandomize);
                ulong netId = apc.net.ID.Value;
                string profile = controller.spawnProfile;
                tanksWaitingToSpawn.TryAdd(profile, new HashSet<ulong>());
                tanksWaitingToSpawn[profile].Add(netId);
                timer.Once(waitTime, () => {
                    tanksWaitingToSpawn[profile].Remove(netId);
                    CheckForMissingTimedTanks();
                });
            }
            foreach (var user in currentTankHealthId.ToList())
                if (user.Value == apc.net.ID.Value)
                    currentTankHealthId.Remove(user.Key);
            roads[controller.roadName].isNowOccupied = false;
            SphereEntity sphere = apc.GetParentEntity() as SphereEntity;
            if (sphere != null && !sphere.IsDestroyed)
                sphere.Kill();
            roadBradleys.Remove(apc);
        }

        private void OnEntityKill(HelicopterDebris debris)
        {
            SphereEntity sphere = debris.GetParentEntity() as SphereEntity;
            if (sphere != null && !sphere.IsDestroyed)
                sphere.Kill();
        }

        private object OnEntityDestroy(BradleyAPC apc)
        {
            RoadBradleyController controller = apc.GetComponent<RoadBradleyController>();
            if (controller == null) return null;
            bool isOwnedTank = controller.ownerId != 0;
            if (!isOwnedTank)
            {
                Puts($"Bradley '{controller.profile}' from route '{controller.roadName}' has been destroyed at {apc.transform.position}!");
                TankDeathInfo.tankDeathOwner = 0;
            }
            else
            {
                Puts($"Bradley purchased by '{controller.ownerId}' ({controller.profile}) has been destroyed at {apc.transform.position}!");
                Interface.Call("OnRoadBradleyKilled", apc, controller.ownerId, controller.profile);
                lastKillTime[controller.roadName] = DateTime.Now;

                TankDeathInfo.tankDeathOwner = controller.ownerId;
                if (!config.countLimitPerCalls)
                {
                    string date = DateTime.Now.ToShortDateString();
                    limitData.limits.TryAdd(date, new Dictionary<ulong, Dictionary<string, int>>());
                    limitData.limits[date].TryAdd(controller.ownerId, new Dictionary<string, int>());
                    limitData.limits[date][controller.ownerId].TryAdd(controller.profile, 0);
                    limitData.limits[date][controller.ownerId][controller.profile]++;
                }
            }
            TankDeathInfo.tankDeathLoc = apc.transform.position;
            TankDeathInfo.tankDeathProfile = controller.profile;
            TankDeathInfo.cratesToSet = apc.maxCratesToSpawn;
            TankDeathInfo.crateCount.Clear();
            foreach (var damageDealt in controller.damageDealt.OrderByDescending(x => x.Value))
            {
                TankDeathInfo.topDmgUser = damageDealt.Key;
                break;
            }
            if (!isOwnedTank)
                Interface.Call("OnRoadBradleyKilled", apc, 0, controller.profile, TankDeathInfo.topDmgUser);
            if ((!isOwnedTank && config.spawns[controller.spawnProfile].announceKill) || (isOwnedTank && config.purchases[controller.profile].announceKill))
            {
                Dictionary<string, int> damageScoreboard = new Dictionary<string, int>();
                foreach (var damageDealt in controller.damageDealt.OrderByDescending(x => x.Value))
                {
                    BasePlayer player = BasePlayer.FindByID(damageDealt.Key);
                    if (player == null) continue;
                    damageScoreboard.TryAdd(player.displayName, Convert.ToInt32(damageDealt.Value));
                }
                List<BasePlayer> broadcasters = Pool.Get<List<BasePlayer>>();
                broadcasters.Clear();
                if (isOwnedTank)
                {
                    foreach (var damageDealt in controller.damageDealt.Keys)
                    {
                        BasePlayer player = BasePlayer.FindByID(damageDealt);
                        if (player == null) continue;
                        broadcasters.Add(player);
                    }
                }
                else
                    broadcasters.AddRange(BasePlayer.activePlayerList);
                StringBuilder sb = Pool.Get<StringBuilder>();
                foreach (var player in broadcasters)
                {
                    sb.Clear().Append(Lang("TankDeathAnnouncement", player.UserIDString, controller.profile.ToUpper()));
                    int counter = 0;
                    foreach (var dealt in damageScoreboard)
                    {
                        counter++;
                        sb.Append(Lang("TankDeathAnnouncementPlayerFormat", player.UserIDString, counter, dealt.Key, dealt.Value));
                    }
                    SendReply(player, sb.ToString());
                }
                Pool.FreeUnmanaged(ref sb);
                Pool.FreeUnmanaged(ref broadcasters);
            }
            if (config.profiles[controller.profile].damageReward.Count > 0 && controller.damageDealt.Count > 0)
            {
                float damageSum = 0;
                foreach (var damage in controller.damageDealt)
                    damageSum += damage.Value;
                StringBuilder sb = Pool.Get<StringBuilder>();
                Dictionary<int, int> redeemedAmounts = Pool.Get<Dictionary<int, int>>();
                foreach (var playerId in controller.damageDealt.OrderByDescending(x => x.Value))
                {
                    BasePlayer damagePlayer = BasePlayer.FindByID(playerId.Key);
                    if (damagePlayer == null) continue;
                    float percentage = playerId.Value / damageSum;
                    sb.Clear().Append(Lang("KillReward", damagePlayer.UserIDString, percentage * 100f));
                    int itemCount = -1;
                    foreach (var item in config.profiles[controller.profile].damageReward)
                    {
                        itemCount++;
                        redeemedAmounts.TryAdd(itemCount, 0);
                        int amount = config.profiles[controller.profile].damageRewardFloor ? Mathf.FloorToInt(item.amount * percentage) : Mathf.RoundToInt(item.amount * percentage);
                        if (amount == 0 && redeemedAmounts[itemCount] < item.amount)
                            amount = item.amount;
                        if (amount == 0) continue;
                        redeemedAmounts[itemCount] += amount;
                        if (item.command != "")
                        {
                            Server.Command(item.command.Replace("{amount}", amount.ToString()).Replace("{userId}", damagePlayer.UserIDString).Replace("{userName}", damagePlayer.displayName).Replace("{userPosX}", damagePlayer.transform.position.x.ToString()).Replace("{userPosY}", damagePlayer.transform.position.y.ToString()).Replace("{userPosZ}", damagePlayer.transform.position.z.ToString()));
                        }
                        else
                        {
                            if (item.shortname == "currency")
                            {
                                ulong userId = damagePlayer.userID.Get();
                                switch (config.moneyPlugin)
                                {
                                    case 1:
                                        Economics?.Call("Deposit", userId, (double)amount);
                                        break;
                                    case 2:
                                        ServerRewards?.Call("AddPoints", userId, amount);
                                        break;
                                    case 3:
                                        ShoppyStock?.Call("GiveCurrency", config.currency, userId, amount);
                                        break;
                                    default:
                                        break;
                                }
                                sb.Append(Lang("KillRewardCurrency", damagePlayer.UserIDString, amount));
                            }
                            else
                            {
                                Item rewardItem = ItemManager.CreateByName(item.shortname, amount, item.skin);
                                if (!string.IsNullOrEmpty(item.displayName))
                                    rewardItem.name = item.displayName;
                                if (!rewardItem.MoveToContainer(damagePlayer.inventory.containerMain))
                                    rewardItem.Drop(damagePlayer.eyes.position, Vector3.zero);
                                string displayName = string.IsNullOrEmpty(item.displayName) ? rewardItem.info.displayName.english : item.displayName;
                                sb.Append(Lang("KillRewardItem", damagePlayer.UserIDString, displayName, amount));
                            }
                        }
                    }
                    SendReply(damagePlayer, sb.ToString());
                }
                Pool.FreeUnmanaged(ref redeemedAmounts);
                Pool.FreeUnmanaged(ref sb);
            }
            InitializeCustomTankDeath(apc, controller);
            return true;
        }

        private void InitializeCustomTankDeath(BradleyAPC apc, RoadBradleyController controller)
        {
            if (config.customTankDeath.showOnMap)
                apc.CreateExplosionMarker(10f);
#if CARBON
            apc.KillSpawnedScientists();
#else
            killScientist.Invoke(apc, null);
#endif
            Effect.server.Run(apc.explosionEffect.resourcePath, apc.mainTurretEyePos.transform.position, Vector3.up, null, true);
            Vector3 zero = Vector3.zero;
            GameObject gibSource = apc.servergibs.Get().GetComponent<ServerGib>()._gibSource;
            List<ServerGib> list = ServerGib.CreateGibs(apc.servergibs.resourcePath, apc.gameObject, gibSource, zero, 3f);
            foreach (var gib in list)
                OnGibSpawned(gib as HelicopterDebris, controller.profile);
            if (config.customTankDeath.fireballCount > 0)
            {
                for (int i = 0; i < config.customTankDeath.fireballCount; i++)
                {
                    global::BaseEntity baseEntity = GameManager.server.CreateEntity(apc.fireBall.resourcePath, apc.transform.position, apc.transform.rotation, true);
                    if (baseEntity)
                    {
                        float minInclusive = 3f;
                        float maxInclusive = 10f;
                        Vector3 onUnitSphere = UnityEngine.Random.onUnitSphere;
                        baseEntity.transform.position = apc.transform.position + new Vector3(0f, 1.5f, 0f) + onUnitSphere * UnityEngine.Random.Range(-4f, 4f);
                        Collider component = baseEntity.GetComponent<Collider>();
                        baseEntity.Spawn();
                        baseEntity.SetVelocity(zero + onUnitSphere * UnityEngine.Random.Range(minInclusive, maxInclusive));
                        foreach (ServerGib serverGib in list)
                            Physics.IgnoreCollision(component, serverGib.GetCollider(), true);
                    }
                }
            }
            for (int j = 0; j < apc.maxCratesToSpawn; j++)
            {
                Vector3 onUnitSphere2 = UnityEngine.Random.onUnitSphere;
                onUnitSphere2.y = 0f;
                onUnitSphere2.Normalize();
                Vector3 pos = apc.transform.position + new Vector3(0f, 1.5f, 0f) + onUnitSphere2 * UnityEngine.Random.Range(2f, 3f);
                LockedByEntCrate lootContainer = GameManager.server.CreateEntity(apc.crateToDrop.resourcePath, pos, Quaternion.LookRotation(onUnitSphere2), true) as LockedByEntCrate;
                lootContainer.Spawn();
                timer.Once(0.1f, () => SetupCrate(lootContainer));
                if (lootContainer)
                    lootContainer.Invoke(new Action(lootContainer.RemoveMe), 1800f);
                Collider component2 = lootContainer.GetComponent<Collider>();
                Rigidbody rigidbody = lootContainer.gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.mass = 2f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.velocity = zero + onUnitSphere2 * UnityEngine.Random.Range(1f, 3f);
                rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);
                if (config.customTankDeath.fireballLock)
                {
                    FireBall fireBall = GameManager.server.CreateEntity(apc.fireBall.resourcePath, default, default, true) as FireBall;
                    if (fireBall)
                    {
                        fireBall.SetParent(lootContainer, false, false);
                        fireBall.Spawn();
                        fireBall.GetComponent<Rigidbody>().isKinematic = true;
                        fireBall.GetComponent<Collider>().enabled = false;
                    }
                    lootContainer.SendMessage("SetLockingEnt", fireBall.gameObject, SendMessageOptions.DontRequireReceiver);
                }
                else
                {
                    CodeLock codelock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", new Vector3(0, 0.6f, 0), Quaternion.Euler(0, 90, 90), true) as CodeLock;
                    codelock.SetParent(lootContainer);
                    codelock.Spawn();
                    codelock.code = "tryThis";
                    codelock.hasCode = true;
                    codelock.CanRemove = false;
                    codelock.SetFlag(BaseEntity.Flags.Locked, true);
                    codelock.SendNetworkUpdate();
                    timeToOpenCrate.Add(lootContainer.net.ID.Value, DateTime.Now.AddSeconds(config.profiles[controller.profile].lockTime));
                    timer.Once(config.profiles[controller.profile].lockTime, () => {
                        if (codelock != null)
                            Effect.server.Run(codelock.effectUnlocked.resourcePath, codelock, 0U, Vector3.zero, Vector3.forward);
                    });
                }
                foreach (ServerGib serverGib2 in list)
                    Physics.IgnoreCollision(component2, serverGib2.GetCollider(), true);
            }
            SafeKillBradley(apc, true);
        }

        private object OnKilledByBradley(BasePlayer victim, HitInfo info, bool wasWounded = false)
        {
            if (!bagData.bags.ContainsKey(victim.userID) || bagData.bags[victim.userID].Count == 0) return null;
            if ((info.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName == "maincannonshell") || (info.Initiator != null && info.Initiator is BradleyAPC))
            {
                bool found = false;
                SleepingBag bed = null;
                foreach (var bag in bagData.bags[victim.userID].ToList())
                {
                    bed = BaseNetworkable.serverEntities.Find(new NetworkableId(bag)) as SleepingBag;
                    if (bed == null)
                    {
                        bagData.bags[victim.userID].Remove(bag);
                        continue;
                    }
                    if (Vector3.Distance(victim.transform.position, bed.transform.position) > config.maxBedDistance) continue;
                    found = true;
                    break;
                }
                if (!found)
                {
                    if (!wasWounded)
                        SendReply(victim, Lang("RespawnNotFound", victim.UserIDString));
                    return null;
                }
                if (victim.isMounted)
                {
                    BaseVehicle baseVehicle = victim.GetMountedVehicle();
                    if (baseVehicle != null)
                    {
                        foreach (var mountPointInfo in baseVehicle.allMountPoints)
                        {
                            if (mountPointInfo != null && mountPointInfo.mountable != null)
                            {
                                BasePlayer mounted = mountPointInfo.mountable.GetMounted();
                                if (mounted != null && mounted.userID == victim.userID)
                                    mountPointInfo.mountable.DismountPlayer(mounted);
                            }
                        }
                        BasePlayer[] players = baseVehicle.GetComponentsInChildren<BasePlayer>();
                        foreach (var player in players)
                            if (player.userID == victim.userID)
                                player.SetParent(null, true, true);
                    }
                }
                victim.DismountObject();
                victim.Heal(5);
                victim.ClientRPC(RpcTarget.Player("StartLoading", victim));
                if (!victim.IsSleeping())
                {
                    victim.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
                    BasePlayer.sleepingPlayerList.Add(victim);
                    victim.CancelInvoke("InventoryUpdate");
                }
                victim.Teleport(bed.transform.position + new Vector3(0, 0.5f, 0));
                victim.SendNetworkUpdate();
                victim.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                victim.UpdateNetworkGroup();
                victim.SendNetworkUpdateImmediate(false);
                victim.SendFullSnapshot();
                NextTick(() => victim.metabolism.bleeding.value = 0);
                if (!wasWounded)
                    PrintToChat(victim, Lang("Respawned", victim.UserIDString));
                return false;
            }
            return null;
        }

        private bool ValidatePerm(ulong ownerId, BasePlayer player)
        {
            if (ownerId == player.userID) return true;
            if (config.useTeams && player.Team != null && player.Team.teamID != 0UL && player.Team.members.Contains(ownerId)) return true;
            if (config.useClans && Clans != null && Clans.Call<bool>("IsClanMember", ownerId.ToString(), player.UserIDString)) return true;
            if (config.useFriends && Friends != null && Friends.Call<bool>("HasFriend", ownerId, player.userID.Get())) return true;
            return false;
        }

        private static void TryGenerateDefaultTracks()
        {
            if (!config.mapTracks.ContainsKey(mapName)) return;
            foreach (var spawnConfig in config.mapTracks[mapName])
            {
                if (!config.spawns.ContainsKey(spawnConfig))
                {
                    Debug.LogWarning($"[RoadBradley WARNING] Can't find '{spawnConfig}' in spawn configuration! Skipping...");
                    continue;
                }
                SpawnConfig configValue = config.spawns[spawnConfig];
                string usedRoad = string.Empty;
                if (configValue.useLongestPath)
                {
                    int longest = 0;
                    foreach (var road in roads)
                    {
                        if (road.Value.roadType == RoadType.Default) continue;
                        int checkpointCount = road.Value.checkpoints.Count;
                        if (checkpointCount > longest)
                        {
                            usedRoad = road.Key;
                            longest = checkpointCount;
                        }
                    }
                    if (usedRoad != string.Empty && !roads[usedRoad].assignedRandomEvents.Contains(spawnConfig))
                        roads[usedRoad].assignedRandomEvents.Add(spawnConfig);
                }
                else if (configValue.useAllDefault)
                {
                    foreach (var road in roads)
                        if (road.Value.roadType == RoadType.Edited && !road.Value.assignedRandomEvents.Contains(spawnConfig))
                            road.Value.assignedRandomEvents.Add(spawnConfig);
                }
                else
                {
                    foreach (var track in configValue.trackList)
                    {
                        int trackCount = track.Count;
                        if (trackCount == 1)
                        {
                            if (!roads.ContainsKey(track[0]))
                            {
                                Debug.LogWarning($"[RoadBradley WARNING] There is no generated route with name '{track[0]}'! Change it in configuration or tanks may not appear!");
                                continue;
                            }
                            if (!roads[track[0]].assignedRandomEvents.Contains(spawnConfig))
                                roads[track[0]].assignedRandomEvents.Add(spawnConfig);
                        }
                        else if (trackCount > 1)
                            MergeTracks(track, spawnConfig);
                    }
                }
            }
        }

        private static void MergeTracks(List<string> trackKeys, string spawnConfig)
        {
            foreach (var key in trackKeys)
            {
                if (!roads.ContainsKey(key))
                {
                    Debug.LogWarning($"[RoadBradley WARNING] There is no generated route with name '{key}'! Change it in configuration or tanks may not appear!");
                    return;
                }
            }
            List<Vector3> mergedRoads = new List<Vector3>();
            foreach (var key in trackKeys)
                mergedRoads.AddRange(roads[key].checkpoints);
            foreach (var road in roads.Values)
            {
                if (road.roadType != RoadType.Merged) continue;
                if (road.checkpoints[0] == mergedRoads[0] && road.checkpoints.Last() == mergedRoads.Last())
                {
                    road.assignedRandomEvents.Add(spawnConfig);
                    return;
                }
            }
            foreach (var key in trackKeys)
                roads[key].hasBeenMerged = true;
            roads.Add($"{mergedTracksCounter}_Merged", new RoadInfo()
            {
                roadType = RoadType.Merged,
                assignedRandomEvents = new HashSet<string> { spawnConfig },
                checkpoints = mergedRoads
            });
            mergedTracksCounter++;
        }

        private static void TryGeneratePurchasableTracks()
        {
            bool hasMapName1 = config.roadBlacklist.ContainsKey(mapName);
            bool hasMapName2 = config.customRoads.ContainsKey(mapName);
            foreach (var road in roads)
            {
                RoadInfo roadValue = road.Value;
                if (roadValue.roadType == RoadType.Default) continue;
                if (config.removeTimedRoads && roadValue.assignedRandomEvents.Count > 0) continue;
                if (hasMapName1 && config.roadBlacklist[mapName].Contains(road.Key)) continue;
                if (hasMapName2 && roadValue.roadType == RoadType.Custom && !config.customRoads[mapName].Contains(road.Key)) continue;
                roadValue.isPurchasable = true;
            }
        }

        private static void PrintRoadInfo()
        {
            int defaultRoads = 0, editedRoads = 0, mergedRoads = 0, canceledRoads = 0, customRoads = 0, randonEventRoads = 0, purchasableRoads = 0;
            foreach (var road in roads)
            {
                RoadInfo roadValue = road.Value;
                if (roadValue.roadType == RoadType.Default)
                    defaultRoads++;
                if (roadValue.roadType == RoadType.Edited)
                    editedRoads++;
                if (roadValue.roadType == RoadType.Merged)
                    mergedRoads++;
                if (roadValue.roadType == RoadType.Custom)
                    customRoads++;
                if (roadValue.hasBeenMerged)
                    canceledRoads++;
                if (roadValue.assignedRandomEvents.Count > 0)
                    randonEventRoads++;
                if (roadValue.isPurchasable)
                    purchasableRoads++;
            }
            StringBuilder sb = Pool.Get<StringBuilder>();
            sb.Clear().AppendLine("##############################").AppendLine("Generated roads for RoadBradleys:")
                .Append("Default Roads: ").Append(defaultRoads).AppendLine()
                .Append("Edited Roads: ").Append(editedRoads).AppendLine()
                .Append("Merged Roads: ").Append(mergedRoads).AppendLine()
                .Append("Custom Roads: ").Append(customRoads).AppendLine()
                .Append("Merged Roads Parts: ").Append(canceledRoads).Append(" (they won't be used)").AppendLine()
                .Append("Random Event Roads: ").Append(randonEventRoads).AppendLine()
                .Append("Purchasable Event Roads: ").Append(purchasableRoads).AppendLine().AppendLine("##############################");
            Debug.Log(sb.ToString());
            Pool.FreeUnmanaged(ref sb);
        }

        private void CheckForMissingTimedTanks()
        {
            if (!config.mapTracks.ContainsKey(mapName)) return;
            int playerCount = BasePlayer.activePlayerList.Count;
            foreach (var mapProfile in config.mapTracks[mapName])
            {
                int tanksWaiting = tanksWaitingToSpawn.ContainsKey(mapProfile) ?
                    tanksWaitingToSpawn[mapProfile].Count : 0;
                int targetTanks = 0;
                foreach (var tankAm in config.spawns[mapProfile].onlinePlayers)
                    if (playerCount >= tankAm.Value)
                    {
                        targetTanks = tankAm.Key;
                    }
                int alreadySpawnedTanks = 0;
                foreach (var apc in roadBradleys)
                {
                    if (apc.GetComponent<RoadBradleyController>().spawnProfile == mapProfile)
                        alreadySpawnedTanks++;
                }
                int tanksToSpawn = targetTanks - alreadySpawnedTanks - tanksWaiting;
                for (int i = 0; i < tanksToSpawn; i++)
                {
                    SpawnTimedTank(mapProfile);
                }
            }
        }

        private void SpawnTimedTank(string confKey)
        {
            int sumWeight = 0;
            foreach (var profileKey in config.spawns[confKey].chances.Values)
                sumWeight += profileKey;
            int randSpawn = Core.Random.Range(0, sumWeight + 1);
            sumWeight = 0;
            string profile = string.Empty;
            foreach (var profileKey in config.spawns[confKey].chances)
            {
                sumWeight += profileKey.Value;
                if (sumWeight >= randSpawn)
                {
                    profile = profileKey.Key;
                    break;
                }
            }
            if (!config.profiles.ContainsKey(profile))
            {
                Puts($"THERE IS NO TANK WITH PROFILE {profile}. SKIPPING SPAWN...");
                return;
            }
            SpawnRoadBradley(profile, null, confKey);
        }

        private void PurchaseTank(BasePlayer player, string profile)
        {
            if (!config.purchases.ContainsKey(profile)) return;
            if (!IsFreeRoad())
            {
                SendReply(player, Lang("NoFreeRoad", player.UserIDString));
                return;
            }
            if (permission.UserHasPermission(player.UserIDString, "roadbradley.bypass"))
            {
                SpawnRoadBradley(profile, player);
                return;
            }
            else
            {
                if (config.maxBradleys > 0)
                {
                    int purchasedTanks = 0;
                    foreach (var roadBradley in roadBradleys)
                    {
                        RoadBradleyController controller = roadBradley.GetComponent<RoadBradleyController>();
                        if (controller.ownerId != 0)
                            purchasedTanks++;
                    }
                    if (purchasedTanks > config.maxBradleys)
                    {
                        SendReply(player, Lang("TooManyBradleys", player.UserIDString));
                        return;
                    }
                }
                PurchaseConfig configValue = config.purchases[profile];
                bool haveRequiredItems = true;
                foreach (var req in configValue.requiredItems)
                {
                    if (!HaveRequiredItem(player, req.shortname, req.skin, req.amount))
                    {
                        haveRequiredItems = false;
                        return;
                    }
                }
                foreach (var brad in roadBradleys)
                    if (brad.OwnerID == player.userID)
                        return; //SHOULDN'T BE POSSIBLE SO NO MESSAGE. IT'S JUST SECURITY CHECK.
                if (!haveRequiredItems) return; //SHOULDN'T BE POSSIBLE SO NO MESSAGE. IT'S JUST SECURITY CHECK.
                if (purchaseCooldowns.ContainsKey(player.userID) && purchaseCooldowns[player.userID].ContainsKey(profile)) return;
                TakeRequiredItems(player, profile);
                if (configValue.cooldown > 0)
                {
                    purchaseCooldowns.TryAdd(player.userID, new Dictionary<string, int>());
                    purchaseCooldowns[player.userID][profile] = configValue.cooldown;
                    TryRunCooldownTimer();
                }
                if (config.countLimitPerCalls && configValue.maxDaily > 0)
                {
                    string date = DateTime.Now.ToShortDateString();
                    limitData.limits.TryAdd(date, new Dictionary<ulong, Dictionary<string, int>>());
                    limitData.limits[date].TryAdd(player.userID, new Dictionary<string, int>());
                    limitData.limits[date][player.userID].TryAdd(profile, 0);

                    int playerTanks = limitData.limits[date][player.userID][profile];
                    int playerLimit = configValue.maxDaily;
                    foreach (var perm in configValue.purchasePermissionLimits)
                    {
                        if (permission.UserHasPermission(player.UserIDString, perm.Key))
                        {
                            playerLimit = perm.Value;
                            break;
                        }
                    }
                    if (playerTanks >= playerLimit) return; //SHOULDN'T BE POSSIBLE SO NO MESSAGE. IT'S JUST SECURITY CHECK.

                    limitData.limits[date][player.userID][profile]++;
                }
                SpawnRoadBradley(profile, player);
            }
        }

        private void TryRunCooldownTimer()
        {
            if (purchaseCooldowns.Count > 0 && (purchaseTimer == null || purchaseTimer.Destroyed))
            {
                purchaseTimer = timer.Every(1, () =>
                {
                    foreach (var user in purchaseCooldowns.ToList())
                    {
                        foreach (var tank in user.Value.ToList())
                        {
                            purchaseCooldowns[user.Key][tank.Key]--;
                            if (purchaseCooldowns[user.Key][tank.Key] == 0)
                                purchaseCooldowns[user.Key].Remove(tank.Key);
                        }
                        if (purchaseCooldowns[user.Key].Count == 0)
                        {
                            purchaseCooldowns.Remove(user.Key);
                            if (purchaseCooldowns.Count == 0)
                                purchaseTimer.Destroy();
                        }
                    }
                });
            }
        }

        private void SpawnRoadBradley(string profile, BasePlayer owner = null, string spawnProfile = "")
        {
            string spawnRoad = GetBradleyRoad(owner, spawnProfile);
            if (spawnRoad == string.Empty) return;
            int spawnPoint = 0;
            int checkpointCount = roads[spawnRoad].checkpoints.Count;
            if (config.randomizeSpawn)
                spawnPoint = Core.Random.Range(0, checkpointCount - 1);
            Vector3 startPosition = roads[spawnRoad].checkpoints[spawnPoint];
            BradleyAPC apc = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", startPosition) as BradleyAPC;
            apc.Spawn();
            if (spawnProfile != "" && config.spawns[spawnProfile].announceSpawn)
            {
                Puts($"Random event Road Bradley of profile '{profile}' has spawned at {startPosition}!");
                foreach (var player in BasePlayer.activePlayerList)
                    SendReply(player, Lang("BradleySpawned", player.UserIDString, profile, MapHelper.PositionToString(startPosition)));
            }
            else if (owner != null)
            {
                Puts($"Road Bradley purchased by {owner.displayName} ({owner.UserIDString}) of profile '{profile}' has spawned at {startPosition}!");
                StringBuilder sb = Pool.Get<StringBuilder>();
                SendReply(owner, Lang("BradleyPurchased", owner.UserIDString, profile, MapHelper.PositionToString(startPosition), FormatTime(config.purchases[profile].killTime, sb), FormatTime(config.purchases[profile].comingTime, sb)));
                Pool.FreeUnmanaged(ref sb);
            }
            roadBradleys.Add(apc);
            RoadBradleyController controller = apc.gameObject.AddComponent<RoadBradleyController>();
            controller.roadName = spawnRoad;
            controller.spawnProfile = spawnProfile;
            controller.profile = profile;
            controller.owner = owner;
            controller.ownerId = owner == null ? 0 : owner.userID;
            controller.InitTank(spawnPoint);
            ProfileConfig configValue = config.profiles[profile];
            apc.OwnerID = owner == null ? 0 : owner.userID;
            apc.moveForceMax = configValue.moveForce;
            apc.throttle = configValue.moveSpeed;
            apc.leftThrottle = configValue.moveSpeed;
            apc.rightThrottle = configValue.moveSpeed;
            apc.searchRange = configValue.viewRange;
            apc.viewDistance = configValue.viewRange;
            apc.ScientistSpawnCount = configValue.scientistsToSpawn;
            apc.ScientistSpawnRadius = configValue.scientistsRadius;
            if (configValue.lootPreset == "")
                apc.maxCratesToSpawn = configValue.crateAmount;
            else
            {
                int crateAmount = Core.Random.Range(config.lootPresets[configValue.lootPreset].minCrates, config.lootPresets[configValue.lootPreset].maxCrates + 1);
                apc.maxCratesToSpawn = crateAmount;
            }
            apc.coaxBurstLength = configValue.coaxBurstLength;
            apc.coaxFireRate = configValue.coaxFireRate;
            apc.coaxAimCone = configValue.coaxAimCone;
            apc.bulletDamage = configValue.coaxBulletDamage;
            apc._health = configValue.health;
            apc._maxHealth = configValue.health;
            apc.globalBroadcast = true;
            TryScaleTank(apc, configValue);
            TryCreateMarkers(apc, controller, configValue);
            TryAddDisarmZone(apc, controller, configValue);
            Interface.Call("OnRoadBradleySpawned", owner, apc, profile, spawnRoad);
            roads[spawnRoad].isNowOccupied = true;
        }

        private static void TryScaleTank(BradleyAPC apc, ProfileConfig configValue)
        {
            if (configValue.tankScale == 1) return;
            SphereEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", apc.transform.position) as SphereEntity;
            sphere.Spawn();
            apc.SetParent(sphere, true);
            SphereInstantLerp(sphere, configValue.tankScale);
        }

        private static void SphereInstantLerp(SphereEntity sphere, float scale)
        {
            sphere.lerpRadius = scale;
            sphere.currentRadius = scale;
            sphere.UpdateScale();
            sphere.SendNetworkUpdate();
        }

        private static void TryCreateMarkers(BradleyAPC apc, RoadBradleyController controller, ProfileConfig configValue)
        {
            string markerToLower = configValue.markerType.ToLower();
            if (markerToLower != "none" && markerPrefabs.ContainsKey(markerToLower))
            {
                BaseEntity marker = GameManager.server.CreateEntity(markerPrefabs[markerToLower], apc.transform.position);
                marker.Spawn();
                marker.SetParent(apc, true);
            }
            bool purchased = controller.ownerId != 0;
            if (purchased && config.enablePurchasedTankInfo)
            {
                VendingMachineMapMarker marker2 = GameManager.server.CreateEntity(markerPrefabs["vending"], apc.transform.position) as VendingMachineMapMarker;
                marker2.Spawn();
                marker2.SetParent(apc, true);
                marker2.markerShopName = string.Format(config.purchasedTankInfo, controller.profile.ToUpper(), controller.owner.displayName, apc.health);
            }
            else if (!purchased && config.enableTankInfo)
            {
                VendingMachineMapMarker marker2 = GameManager.server.CreateEntity(markerPrefabs["vending"], apc.transform.position) as VendingMachineMapMarker;
                marker2.Spawn();
                marker2.SetParent(apc, true);
                marker2.markerShopName = string.Format(config.tankInfo, controller.profile.ToUpper(), apc.health);
            }
        }

        private static void TryAddDisarmZone(BradleyAPC apc, RoadBradleyController controller, ProfileConfig configValue)
        {
            if (!configValue.disarmEnabled) return;
            GameObject go = new GameObject();
            BradleyDisarmZone bradleyDisarm = go.AddComponent<BradleyDisarmZone>();
            bradleyDisarm.SetupZone(apc, controller);
        }

        private void KillTank(BradleyAPC apc, RoadBradleyController controller, string killReason)
        {
            if (killReason == "timeElapsed")
            {
                foreach (var target in controller.targets)
                    if (target != null)
                        SendReply(target, Lang("FightLost", target.UserIDString));
            }
            Effect.server.Run("assets/prefabs/weapons/beancan grenade/effects/beancan_grenade_explosion.prefab", apc.transform.position);
            SafeKillBradley(apc);
        }

        private static void SafeKillBradley(BradleyAPC apc, bool gib = false)
        {
            SphereEntity sphere = apc.GetParentEntity() as SphereEntity;
            if (sphere != null && !sphere.IsDestroyed)
                sphere.Kill();
            else if (apc != null && !apc.IsDestroyed)
                apc.Kill(gib ? BaseNetworkable.DestroyMode.Gib : BaseNetworkable.DestroyMode.None);
        }

        private void OpenRoadBradleyUI(BasePlayer player)
        {
            if (config.purchases.Count == 0)
            {
                PrintToChat(player, Lang("NoPurchases", player.UserIDString));
                return;
            }
            SendJson(player, coreJson);
            using CUI cui = new CUI(CuiHandler);
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            StringBuilder sb = Pool.Get<StringBuilder>();
            int startY = 352;
            if (!lastSelectedTankUi.ContainsKey(player.userID))
                lastSelectedTankUi.Add(player.userID, config.purchases.ElementAt(0).Key);
            foreach (var profile in config.purchases)
            {
                if (profile.Value.permission != "" && !permission.UserHasPermission(player.UserIDString, profile.Value.permission)) continue;
                string buttonKey = sb.Clear().Append("RoadBradleyUI_TierButton_").Append(profile.Key).ToString();
                string textKey = sb.Append("_Text").ToString();
                string command = sb.Clear().Append("RoadBradleyUI profile ").Append(profile.Key).ToString();
                string color = lastSelectedTankUi[player.userID] == profile.Key ? BetterColors.GreenBackgroundTransparent : BetterColors.Transparent;
                //Tier Button 1
                cui.CreateProtectedButton(elements, "RoadBradleyUI_LeftPanel", color, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 220, startY, startY + 30, command, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, buttonKey, null, false);
                string langKey = sb.Clear().Append("Tank_").Append(profile.Key).ToString();
                string displayedText = sb.Clear().Append("•  ").Append(Lang(langKey, player.UserIDString)).ToString();
                color = lastSelectedTankUi[player.userID] == profile.Key ? BetterColors.GreenText : BetterColors.LightGrayTransparent73;
                //Tier Button 1 Text
                cui.CreateText(elements, buttonKey, color, displayedText, 17, 0f, 0f, 0f, 0f, 12, 220, 0, 30, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, textKey, null, false);
                startY -= 30;
            }
            elements.Add(cui.UpdateText("RoadBradleyUI_Title", BetterColors.LightGray, Lang("BradleyFights", player.UserIDString), 21, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Add(cui.UpdateText("RoadBradleyUI_SelectTierText", BetterColors.LightGray, Lang("Levels", player.UserIDString), 18, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            UpdateDisplayedTankUi(player, sb, cui, elements);
            cui.Destroy("RoadBradleyUI_ItemSection", player);
            elements.Send(player);
            elements.Dispose();
            Pool.FreeUnmanaged(ref sb);
        }

        private void UpdateDisplayedTankUi(BasePlayer player, StringBuilder sb, CUI cui, CUI.Handler.UpdatePool elements)
        {
            ProfileConfig tankProfile = config.profiles[lastSelectedTankUi[player.userID]];
            PurchaseConfig purchaseConfig = config.purchases[lastSelectedTankUi[player.userID]];
            string langText = Lang(sb.Clear().Append("Tank_").Append(lastSelectedTankUi[player.userID]).ToString(), player.UserIDString);
            elements.Add(cui.UpdateText("RoadBradleyUI_BradleyInfoTitleText", BetterColors.LightGray, langText, 22, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Add(cui.UpdateText("RoadBradleyUI_DescriptionTitle", BetterColors.LightGrayTransparent73, Lang("Description", player.UserIDString), 15, align: TextAnchor.UpperLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            langText = Lang(sb.Append("_Description").ToString(), player.UserIDString);
            elements.Add(cui.UpdateText("RoadBradleyUI_Description", BetterColors.LightGrayTransparent58, langText, 11, align: TextAnchor.UpperLeft));
            elements.Add(cui.UpdateText("RoadBradleyUI_TankStatsTitle", BetterColors.LightGrayTransparent73, Lang("TankStats", player.UserIDString), 13, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Add(cui.UpdateText("RoadBradleyUI_TankStatsNames", BetterColors.LightGray, Lang("TankStatKeys", player.UserIDString), 9, align: TextAnchor.UpperLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            sb.Clear().Append(tankProfile.health).AppendLine().Append(tankProfile.coaxBulletDamage).AppendLine().Append(tankProfile.coaxBurstLength).AppendLine();
            if (tankProfile.mlrsEnable)
                sb.AppendLine(Lang("Yes", player.UserIDString));
            else
                sb.AppendLine(Lang("No", player.UserIDString));
            if (tankProfile.doCustomAttacks)
                sb.AppendLine(Lang("Yes", player.UserIDString));
            else
                sb.AppendLine(Lang("No", player.UserIDString));
            elements.Add(cui.UpdateText("RoadBradleyUI_TankStatsStats", BetterColors.LightGray, sb.ToString(), 9, align: TextAnchor.UpperCenter, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Add(cui.UpdateText("RoadBradleyUI_TankKillTimeTime", BetterColors.LightGray, FormatTime(purchaseConfig.killTime, sb), 13, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Add(cui.UpdateText("RoadBradleyUI_TankCooldownTime", BetterColors.LightGray, FormatTime(purchaseConfig.cooldown, sb), 13, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            string date = DateTime.Now.ToShortDateString();
            int playerTanks = limitData.limits.ContainsKey(date) && limitData.limits[date].ContainsKey(player.userID) && limitData.limits[date][player.userID].ContainsKey(lastSelectedTankUi[player.userID]) ? limitData.limits[date][player.userID][lastSelectedTankUi[player.userID]] : 0;
            int playerLimit = purchaseConfig.maxDaily;
            foreach (var perm in purchaseConfig.purchasePermissionLimits)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Key))
                {
                    playerLimit = perm.Value;
                    break;
                }
            }
            sb.Clear().Append(playerTanks).Append('/').Append(playerLimit);
            elements.Add(cui.UpdateText("RoadBradleyUI_TankLimitText", BetterColors.LightGray, sb.ToString(), 13, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Add(cui.UpdateText("RoadBradleyUI_TankRequirementsTitle", BetterColors.LightGrayTransparent73, Lang("Requirements", player.UserIDString), 13, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));

            cui.CreatePanel(elements, "RoadBradleyUI_RightPanel", BetterColors.Transparent, null, 0f, 0f, 0f, 0f, 0, 480, 42, 112, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_ItemSection", null, false);

            int startX = 8;
            int counter = 0;
            bool haveRequiredItems = true;
            int yOffset = 0;
            int iconSize = 54;
            int textSize = 15;
            if (purchaseConfig.requiredItems.Count > 7)
            {
                yOffset = 4;
                iconSize = 46;
                textSize = 14;
            }
            foreach (var req in purchaseConfig.requiredItems)
            {
                string command = sb.Clear().Append("RoadBradleyUI reqItem ").Append(lastSelectedTankUi[player.userID]).Append(' ').Append(counter).ToString();
                //Item 1
                cui.CreateProtectedButton(elements, "RoadBradleyUI_ItemSection", BetterColors.BlackTransparent20, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, startX, startX + iconSize, 8 + yOffset, 8 + iconSize + yOffset, command, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_Item", null, false);
                if (req.iconUrl != "" || req.shortname == "currency")
                {
                    //Item 1 URL Icon
                    cui.CreateImage(elements, "RoadBradleyUI_Item", imageDb.GetImage(req.iconUrl), BetterColors.WhiteTransparent80, null, 0f, 0f, 0f, 0f, 4, iconSize - 4, 4, iconSize - 4, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage10", null, false);
                }
                else
                {
                    //Item 1 Icon
                    cui.CreateItemImage(elements, "RoadBradleyUI_Item", cachedItemIds[req.shortname], req.skin, BetterColors.WhiteTransparent80, null, 0f, 0f, 0f, 0f, 4, iconSize - 4, 4, iconSize - 4, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage10", null, false);
                }
                bool hasItem = HaveRequiredItem(player, req.shortname, req.skin, req.amount);
                string textColor = hasItem ? BetterColors.GreenText : BetterColors.RedText;
                if (!hasItem)
                    haveRequiredItems = false;
                //Item 1 Amount
                string amountString = req.shortname == "currency" ? FormatCurrency(req.amount, sb) : FormatAmount(req.amount, sb);
                cui.CreateText(elements, "RoadBradleyUI_Item", textColor, amountString, textSize, 0f, 0f, 0f, 0f, 0, iconSize - 1, 0, 22, TextAnchor.LowerRight, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_ItemAmount", null, false);
                startX += iconSize + 8;
                counter++;
            }
            int cooldown = 0;
            if (purchaseCooldowns.ContainsKey(player.userID) && purchaseCooldowns[player.userID].ContainsKey(lastSelectedTankUi[player.userID]))
                cooldown = purchaseCooldowns[player.userID][lastSelectedTankUi[player.userID]];
            bool isAlreadySpawned = false;
            foreach (var brad in roadBradleys)
                if (brad.OwnerID == player.userID)
                {
                    isAlreadySpawned = true;
                    break;
                }
            string bottomText = string.Empty;
            if (!haveRequiredItems)
                bottomText = Lang("NoRequiredItems", player.UserIDString);
            else if (purchaseConfig.maxDaily > 0 && playerTanks >= playerLimit)
                bottomText = Lang("DailyLimitReached", player.UserIDString);
            else if (cooldown != 0)
                bottomText = Lang("TankOnCooldown", player.UserIDString, FormatTime(cooldown, sb));
            else if (isAlreadySpawned)
                bottomText = Lang("TankAlreadySpawned", player.UserIDString);
            if (bottomText == string.Empty)
            {
                elements.Add(cui.UpdateProtectedButton("RoadBradleyUI_SpawnButton", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", command: "RoadBradleyUI spawn"));
                elements.Add(cui.UpdateText("RoadBradleyUI_SpawnButtonText", BetterColors.GreenText, Lang("SpawnButton", player.UserIDString), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                bottomText = Lang("CanSpawnTank", player.UserIDString);
            }
            else
            {
                string command = permission.UserHasPermission(player.UserIDString, "roadbradley.bypass") ? "RoadBradleyUI spawn" : "";
                elements.Add(cui.UpdateProtectedButton("RoadBradleyUI_SpawnButton", BetterColors.RedBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", command: command));
                elements.Add(cui.UpdateText("RoadBradleyUI_SpawnButtonText", BetterColors.RedText, Lang("LockedButton", player.UserIDString), 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            }
            elements.Add(cui.UpdateText("RoadBradleyUI_BottomInfoText", BetterColors.LightGrayTransparent73, bottomText, 11, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
        }

        private void UpdateLeftButton(BasePlayer player, string oldProfile, string newProfile, StringBuilder sb, CUI cui, CUI.Handler.UpdatePool elements)
        {
            string buttonKey = sb.Clear().Append("RoadBradleyUI_TierButton_").Append(oldProfile).ToString();
            string textKey = sb.Append("_Text").ToString();
            string command = sb.Clear().Append("RoadBradleyUI profile ").Append(oldProfile).ToString();
            elements.Add(cui.UpdateProtectedButton(buttonKey, BetterColors.Transparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", command: command));

            string langKey = sb.Clear().Append("Tank_").Append(oldProfile).ToString();
            string displayedText = sb.Clear().Append("•  ").Append(Lang(langKey, player.UserIDString)).ToString();
            elements.Add(cui.UpdateText(textKey, BetterColors.LightGrayTransparent73, displayedText, 17, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));

            buttonKey = sb.Clear().Append("RoadBradleyUI_TierButton_").Append(newProfile).ToString();
            textKey = sb.Append("_Text").ToString();
            elements.Add(cui.UpdateProtectedButton(buttonKey, BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", command: ""));

            langKey = sb.Clear().Append("Tank_").Append(newProfile).ToString();
            displayedText = sb.Clear().Append("•  ").Append(Lang(langKey, player.UserIDString)).ToString();
            elements.Add(cui.UpdateText(textKey, BetterColors.GreenText, displayedText, 17, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
        }

        private void DrawInitialTankHealth(BasePlayer player, RoadBradleyController controller)
        {
            using CUI cui = new CUI(CuiHandler);
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            StringBuilder sb = Pool.Get<StringBuilder>();
            elements.Add(cui.UpdateText("RoadBradleyHealthUI_BradleyName", BetterColors.LightGrayTransparent73, Lang(sb.Clear().Append("Tank_").Append(controller.profile).ToString(), player.UserIDString), 12, align: TextAnchor.LowerLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            if (controller.tankRemainingTime != -1)
                elements.Add(cui.UpdateText("RoadBradleyHealthUI_BradleyTime", BetterColors.LightGrayTransparent73, FormatShortTime(controller.tankRemainingTime, sb), 11, align: TextAnchor.LowerRight, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            else
                elements.Add(cui.UpdateSimpleImage("RoadBradleyHealthUI_BradleyTimeIcon", "", "assets/icons/stopwatch.png", BetterColors.Transparent));
            cui.Destroy("RoadBradleyHealthUI_RoadBradleyHealth", player);
            SendJson(player, healthJson);
            elements.Send(player);
            elements.Dispose();
            Pool.FreeUnmanaged(ref sb);
        }

        private void UpdateTankHealth(BasePlayer player, BradleyAPC apc, RoadBradleyController controller)
        {
            if (player == null || !player.IsConnected) return;
            if (!currentTankHealthId.ContainsKey(player.userID) || currentTankHealthId[player.userID] != apc.net.ID.Value) return;
            using CUI cui = new CUI(CuiHandler);
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            if (controller.tankRemainingTime != -1)
            {
                StringBuilder sb = Pool.Get<StringBuilder>();
                elements.Add(cui.UpdateText("RoadBradleyHealthUI_BradleyTime", BetterColors.LightGrayTransparent73, FormatShortTime(controller.tankRemainingTime, sb), 11, align: TextAnchor.LowerRight, font: CUI.Handler.FontTypes.RobotoCondensedBold));
                Pool.FreeUnmanaged(ref sb);
            }
            float healthPercentage = apc.health / config.profiles[controller.profile].health;
            elements.Add(cui.UpdatePanel("RoadBradleyHealthUI_HealthBar", BetterColors.RedBackground, "assets/content/ui/namefontmaterial.mat", 0f, healthPercentage, 0f, 1f));
            elements.Send(player);
            elements.Dispose();
        }

        private void ClearTankHealth(BradleyAPC apc, RoadBradleyController controller)
        {
            using CUI cui = new CUI(CuiHandler);
            foreach (var target in controller.targets)
                if (currentTankHealthId.ContainsKey(target.userID) && currentTankHealthId[target.userID] == apc.net.ID.Value)
                {
                    currentTankHealthId.Remove(target.userID);
                    cui.Destroy("RoadBradleyHealthUI_RoadBradleyHealth", target);
                }
        }

        private void UpdateRouteEditor(BasePlayer player)
        {
            using CUI cui = new CUI(CuiHandler);
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            //Title
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_Title", BetterColors.LightGray, Lang("RouteEditorTitle", player.UserIDString), 15, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            //Route Name
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_RouteName", BetterColors.GreenText, activeEditors[player.userID].roadName, 15, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            //Current Route Text
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_CurrentRouteText", BetterColors.LightGray, Lang("CurrentRouteTitle", player.UserIDString), 12, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            //Show Checkpoints Button Text
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_ShowCheckpointsButtonText", BetterColors.GreenText, Lang("ShowCheckpoints", player.UserIDString), 14, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            //Show Help Button Text
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_ShowHelpButtonText", BetterColors.GreenText, Lang("ShowHelp", player.UserIDString), 14, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            //Save And Exit Button Text
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_SaveAndExitButtonText", BetterColors.GreenText, Lang("SaveAndExit", player.UserIDString), 14, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            RoadInfo roadInfo = roads[activeEditors[player.userID].roadName];
            string color = roadInfo.roadType == RoadType.Default ? BetterColors.GreenText : BetterColors.RedText;
            //Default Route
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_DefaultRoute", color, Lang("DefaultRoute", player.UserIDString), 13, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            color = roadInfo.assignedRandomEvents.Count > 0 ? BetterColors.GreenText : BetterColors.RedText;
            //Random Route
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_RandomRoute", color, Lang("RandomRoute", player.UserIDString), 13, align: TextAnchor.MiddleLeft, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            color = roadInfo.isPurchasable ? BetterColors.GreenText : BetterColors.RedText;
            //Purchasable Route
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_PurchasableRoute", color, Lang("PurchasableRoute", player.UserIDString), 13, align: TextAnchor.MiddleRight, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            color = roadInfo.hasBeenMerged ? BetterColors.GreenText : BetterColors.RedText;
            //Merged Route
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_OccupiedRoute", color, Lang("MergedRoute", player.UserIDString), 13, align: TextAnchor.MiddleRight, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            //Checkpoint Count
            elements.Add(cui.UpdateText("RoadBradleyEditorUI_CheckpointCount", BetterColors.LightGrayTransparent73, Lang("CheckpointCount", player.UserIDString, activeEditors[player.userID].cachedCheckpoints.Count), 10, align: TextAnchor.LowerCenter, font: CUI.Handler.FontTypes.RobotoCondensedBold));
            elements.Send(player);
            elements.Dispose();
        }

        private void UpdateCheckpointCount(BasePlayer player)
        {
            using CUI cui = new CUI(CuiHandler);
            CUI.Handler.UpdatePool elements = cui.UpdatePool();
            //Checkpoint Count
            cui.UpdateText("RoadBradleyEditorUI_CheckpointCount", BetterColors.LightGrayTransparent73, Lang("CheckpointCount", player.UserIDString, activeEditors[player.userID].cachedCheckpoints.Count), 10, align: TextAnchor.LowerCenter, font: CUI.Handler.FontTypes.RobotoCondensedBold);
            elements.Send(player);
            elements.Dispose();
        }

        private bool HaveRequiredItem(BasePlayer player, string shortname, ulong skin, int amount)
        {
            string shortToLower = shortname.ToLower();
            if (shortToLower == "currency")
            {
                ulong userId = player.userID.Get();
                switch (config.moneyPlugin)
                {
                    case 1:
                        if (Economics != null && Economics.Call<double>("Balance", userId) >= amount)
                            return true;
                        else
                            return false;
                    case 2:
                        if (ServerRewards != null && ServerRewards.Call<int>("CheckPoints", userId) >= amount)
                            return true;
                        else
                            return false;
                    case 3:
                        if (ShoppyStock != null && ShoppyStock.Call<int>("GetCurrencyAmount", config.currency, userId) >= amount)
                            return true;
                        else
                            return false;
                    default:
                        return false;
                }
            }
            else
            {
                int foundAmount = 0;
                foreach (var item in player.inventory.containerMain.itemList)
                {
                    if (item.info.shortname == shortname && item.skin == skin)
                    {
                        foundAmount += item.amount;
                        if (foundAmount >= amount)
                            return true;
                    }
                }
                foreach (var item in player.inventory.containerBelt.itemList)
                {
                    if (item.info.shortname == shortname && item.skin == skin)
                    {
                        foundAmount += item.amount;
                        if (foundAmount >= amount)
                            return true;
                    }
                }
                return false;
            }
        }

        private void TakeRequiredItems(BasePlayer player, string profile)
        {
            foreach (var item in config.purchases[profile].requiredItems)
            {
                string shortToLower = item.shortname.ToLower();
                if (shortToLower == "currency")
                {
                    ulong userId = player.userID.Get();
                    switch (config.moneyPlugin)
                    {
                        case 1:
                            Economics?.Call("Withdraw", userId, (double)item.amount);
                            break;
                        case 2:
                            ServerRewards?.Call("TakePoints", userId, item.amount);
                            break;
                        case 3:
                            ShoppyStock?.Call("TakeCurrency", config.currency, userId, item.amount);
                            break;
                    }
                }
                else
                {
                    int foundAmount = 0;
                    List<Item> invItems = Pool.Get<List<Item>>();
                    invItems.AddRange(player.inventory.containerMain.itemList);
                    invItems.AddRange(player.inventory.containerBelt.itemList);
                    foreach (var invItem in invItems)
                    {
                        if (invItem.info.shortname == item.shortname && invItem.skin == item.skin)
                        {
                            int amountToTake = invItem.amount > item.amount - foundAmount ? item.amount - foundAmount : invItem.amount;
                            foundAmount += amountToTake;
                            if (amountToTake < invItem.amount)
                            {
                                invItem.amount -= amountToTake;
                                invItem.MarkDirty();
                            }
                            else
                            {
                                invItem.GetHeldEntity()?.Kill();
                                invItem.RemoveFromContainer();
                                invItem.Remove();
                            }
                            if (foundAmount >= item.amount) break;
                        }
                    }
                    Pool.Free(ref invItems);
                }
            }
            ItemManager.DoRemoves();
        }

        private static string FormatTime(int seconds, StringBuilder sb)
        {
            if (seconds >= 60)
            {
                int remainingSeconds = seconds % 60;
                int minutes = (seconds - remainingSeconds) / 60;
                if (minutes >= 60)
                {
                    int remainingMinutes = minutes % 60;
                    int hours = (minutes - remainingMinutes) / 60;
                    return sb.Clear().Append(hours).Append('h').ToString();
                }
                else return sb.Clear().Append(minutes).Append('m').ToString();
            }
            else return sb.Clear().Append(seconds).Append('s').ToString();
        }

        private static string FormatShortTime(int seconds, StringBuilder sb)
        {
            if (seconds >= 60)
            {
                int remainingSeconds = seconds % 60;
                int minutes = (seconds - remainingSeconds) / 60;
                return sb.Clear().Append(minutes).Append('m').Append(' ').Append(remainingSeconds).Append('s').ToString();
            }
            else return sb.Clear().Append(seconds).Append('s').ToString();
        }

        private static string FormatAmount(int amount, StringBuilder sb)
        {
            if (amount >= 1000)
            {
                float thousands = amount / 1000f;
                if (thousands >= 1000)
                {
                    float millions = thousands / 1000f;
                    string amountString;
                    if (millions < 10)
                        amountString = thousands.ToString("0.##");
                    else if (millions < 100)
                        amountString = thousands.ToString("0.#");
                    else
                        amountString = Mathf.RoundToInt(millions).ToString();
                    return sb.Clear().Append('x').Append(amountString).Append('m').ToString();
                }
                else
                {
                    string amountString;
                    if (thousands < 10)
                        amountString = thousands.ToString("0.##");
                    else if (thousands < 100)
                        amountString = thousands.ToString("0.#");
                    else
                        amountString = Mathf.RoundToInt(thousands).ToString();
                    return sb.Clear().Append('x').Append(amountString).Append('k').ToString();
                }

            }
            else return sb.Clear().Append('x').Append(amount).ToString();
        }

        private static string FormatCurrency(int amount, StringBuilder sb)
        {
            if (amount >= 1000)
            {
                int thousands = Mathf.FloorToInt(amount / 1000f);
                if (thousands >= 1000)
                {
                    int millions = Mathf.FloorToInt(thousands / 1000f);
                    return sb.Clear().Append(millions).Append("m ").Append(config.currencySymbol).ToString();
                }
                else return sb.Clear().Append(thousands).Append("k ").Append(config.currencySymbol).ToString();
            }
            else return sb.Clear().Append(amount).Append(' ').Append(config.currencySymbol).ToString();
        }


        private static string GetBradleyRoad(BasePlayer owner = null, string spawnProfile = "")
        {
            if (owner != null)
            {
                Vector3 playerPos = owner.transform.position;
                float nearest = float.MaxValue;
                string nearestRoad = string.Empty;
                foreach (var road in roads)
                {
                    RoadInfo roadValue = road.Value;
                    if (!roadValue.isPurchasable || roadValue.isNowOccupied) continue;
                    if (config.timeCooldownRoads > 0 && lastKillTime.ContainsKey(road.Key) && (DateTime.Now - lastKillTime[road.Key]).TotalSeconds < config.timeCooldownRoads) continue;
                    int counter = -1;
                    foreach (var checkpoint in roadValue.checkpoints)
                    {
                        counter++;
                        if (counter % 3 != 0) continue;
                        float distance = Vector3.Distance(playerPos, checkpoint);
                        if (distance < nearest)
                        {
                            nearest = distance;
                            nearestRoad = road.Key;
                        }
                    }
                }
                return nearestRoad;
            }
            else if (spawnProfile != "")
            {
                List<string> keys = Pool.Get<List<string>>();
                keys.Clear();
                foreach (var road in roads)
                {
                    RoadInfo roadValue = road.Value;
                    if (roadValue.isNowOccupied) continue;
                    if (!roadValue.assignedRandomEvents.Contains(spawnProfile)) continue;
                    keys.Add(road.Key);
                }
                if (keys.Count > 0)
                {
                    string targetRoad = keys[Core.Random.Range(0, keys.Count)];
                    Pool.FreeUnmanaged(ref keys);
                    return targetRoad;
                }
                Pool.FreeUnmanaged(ref keys);
            }
            return string.Empty;
        }

        private static bool IsFreeRoad(string spawnProfile = null)
        {
            bool checkForTime = config.timeCooldownRoads > 0;
            if (string.IsNullOrEmpty(spawnProfile))
                return roads.Any(x => !x.Value.isNowOccupied && x.Value.isPurchasable &&
                (!checkForTime || (!lastKillTime.ContainsKey(x.Key) || (lastKillTime.ContainsKey(x.Key) && (DateTime.Now - lastKillTime[x.Key]).TotalSeconds > config.timeCooldownRoads))));
            else
                return roads.Any(x => !x.Value.isNowOccupied && x.Value.assignedRandomEvents.Contains(spawnProfile));
        }

        private void DestroyHealthUi(BasePlayer player)
        {
            using CUI cui = new CUI(CuiHandler);
            cui.Destroy("RoadBradleyHealthUI_RoadBradleyHealth", player);
        }

        internal static class BradleyHarmonyUpdate
        {
            internal static bool AiLock(BradleyAPC __instance) => !__instance.HasFlag(BaseEntity.Flags.Reserved6);

            internal static bool MoveLock(BradleyAPC __instance) => !__instance.HasFlag(BaseEntity.Flags.Reserved6);

            internal static bool ScientistSpawnLock(BradleyAPC __instance)
            {
                if (!__instance.HasFlag(BaseEntity.Flags.Reserved6))
                    return true;
                RoadBradleyController controller = __instance.GetComponent<RoadBradleyController>();
                if (controller == null) return true;
                return !config.profiles[controller.profile].lockNpcSpawn;
            }
        }

        private class BradleyDisarmZone : FacepunchBehaviour
        {
            private Rigidbody rigidbody;
            private SphereCollider sphereCollider;
            private BradleyAPC apc;
            private RoadBradleyController controller;

            private void Awake()
            {
                gameObject.name = "BradleyDisarmZone";
                transform.position = Vector3.zero;
                rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                gameObject.AddComponent<SphereCollider>();
                sphereCollider = gameObject.GetComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
            }

            public void SetupZone(BradleyAPC bradley, RoadBradleyController con)
            {
                apc = bradley;
                controller = con;
                transform.position = apc.transform.position;
                transform.SetParent(apc.transform, true);
                sphereCollider.transform.position = apc.transform.position;
                sphereCollider.transform.SetParent(apc.transform, true);
                sphereCollider.radius = config.profiles[controller.profile].disarmRadius;
            }

            private void OnTriggerEnter(Collider other)
            {
                if (!other || !other.gameObject) return;
                RFTimedExplosive explosive = other.GetComponent<RFTimedExplosive>();
                if (explosive == null || explosive.GetFrequency() == -1) return;
                if (config.profiles[controller.profile].disarmType)
                    explosive.Explode();
                else
                    explosive.ChangeFrequency(config.disarmFrequency);
            }
        }

        private class RoadBradleyController : FacepunchBehaviour
        {
            public string roadName;
            public string spawnProfile;
            public string profile;
            public ulong ownerId = 0;

            //OWNER GETS NULL WHEN KILLED AND DISCONNECTED
            public BasePlayer owner;

            private BasePlayer currentTarget = null;
            public readonly HashSet<BasePlayer> targets = new HashSet<BasePlayer>();
            public readonly Dictionary<ulong, float> damageDealt = new Dictionary<ulong, float>();
            public int damageAuths = 0;

            private readonly HashSet<ulong> disabledUiUpdates = new HashSet<ulong>();

            private readonly HashSet<BasePlayer> uiUsers = new HashSet<BasePlayer>();

            private BradleyAPC apc;
            private bool initialized = false;
            private int checkpointCount = 0;
            private bool isLooped = false;


            private float lastTarget = 0;
            private bool brakes = false;

            private int stuckCount = 0;
            private Vector3 lastPos = Vector3.zero;

            private readonly HashSet<float> ranMlrs = new HashSet<float>();

            private readonly HashSet<ulong> alreadyDrawnUis = new HashSet<ulong>();

            public int tankRemainingTime = -1;
            private bool removedComeTime = false;

            public void InitTank(int startIndex)
            {
                apc.currentPath = roads[roadName].checkpoints;
                apc.currentPathIndex = startIndex;
                checkpointCount = apc.currentPath.Count;
                initialized = true;
                isLooped = (checkpointCount > 0 && Vector3.Distance(apc.currentPath[0], apc.currentPath[checkpointCount - 1]) < 20f);
                if (ownerId != 0)
                {
                    tankRemainingTime = config.purchases[profile].killTime + config.purchases[profile].comingTime;
                    InvokeRepeating(RunTankTimer, 1, 1);
                    lastPos = apc.transform.position;
                    InvokeRepeating(CheckForStuck, 3, 3);
                }
                else
                    InvokeRepeating(UpdateHealth, 1, 1);
                if (config.enablePurchasedTankInfo || config.enableTankInfo)
                    InvokeRepeating(UpdateHealthOnMarker, 5, 5);
                if (config.profiles[profile].doCustomAttacks)
                    InvokeRandomized(TryCustomAttackMethods, config.profiles[profile].customAttackInterval, config.profiles[profile].customAttackInterval, config.profiles[profile].customAttackIntervalRandomization);
                CancelInvoke(nameof(apc.UpdateTargetList));
                CancelInvoke(nameof(apc.UpdateTargetVisibilities));
                InvokeRepeating(nameof(UpdateTargetList), 1, 1);
            }

            public void UpdateTargetList()
            {
                float nearest = float.MaxValue;
                BasePlayer bestTarget = null;
                foreach (var target in targets)
                {
                    if (target.IsValid() && !target.IsDead() && (!target.InSafeZone() || target.IsHostile()) && apc.VisibilityTest(target))
                    {
                        float distance = Vector3.Distance(target.transform.position, apc.transform.position);
                        if (distance > config.profiles[profile].viewRange * 2.5f)
                        {
                            if (!disabledUiUpdates.Contains(target.userID))
                            {
                                disabledUiUpdates.Add(target.userID);
                                plugin.DestroyHealthUi(target);
                            }
                        }
                        else if (disabledUiUpdates.Contains(target.userID))
                        {
                            plugin.DrawInitialTankHealth(target, this);
                            disabledUiUpdates.Remove(target.userID);
                        }
                        if (distance > config.profiles[profile].viewRange) continue;
                        if (distance < nearest)
                        {
                            nearest = distance;
                            bestTarget = target;
                        }
                    }
                }
                currentTarget = bestTarget;
            }

            public void AddTarget(BasePlayer player)
            {
                TryDrawInitialTankHealth(player);
                if (uiUsers.Count == 0)
                    TryRemoveComeTime();
                targets.Add(player);
                uiUsers.Add(player);
                currentTankHealthId[player.userID] = apc.net.ID.Value;
            }

            public void RemoveTarget(BasePlayer player)
            {
                targets.Remove(player);
                uiUsers.Remove(player);
                alreadyDrawnUis.Remove(player.userID);
                currentTankHealthId.Remove(player.userID);
            }

            private void UpdateHealthOnMarker()
            {
                if (config.enablePurchasedTankInfo && owner != null)
                {
                    foreach (var child in apc.children)
                    {
                        if (child.PrefabName != "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab") continue;
                        VendingMachineMapMarker marker = child as VendingMachineMapMarker;
                        if (marker == null) return;
                        marker.markerShopName = string.Format(config.purchasedTankInfo, profile.ToUpper(), owner.displayName, apc.health);
                        marker.SendNetworkUpdate();
                    }
                }
                else if (ownerId == 0 && config.enableTankInfo)
                {
                    foreach (var child in apc.children)
                    {
                        if (child.PrefabName != "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab") continue;
                        VendingMachineMapMarker marker = child as VendingMachineMapMarker;
                        if (marker == null) return;
                        marker.markerShopName = string.Format(config.tankInfo, profile.ToUpper(), apc.health);
                        marker.SendNetworkUpdate();
                    }
                }
            }

            private void CheckForStuck()
            {
                if (brakes) return;
                if (Vector3.Distance(lastPos, apc.transform.position) > 0.4f)
                {
                    stuckCount = 0;
                    lastPos = apc.transform.position;
                    return;
                }
                int count = 0;
                List<BaseEntity> roadEntities = Pool.Get<List<BaseEntity>>();
                roadEntities.Clear();
                Vis.Entities(apc.transform.position, 4, roadEntities);
                foreach (var entity in roadEntities)
                {
                    if (entity.IsDestroyed) continue;
                    if (entity.net.ID == apc.net.ID) continue;
                    if (config.killedEntities.Contains(entity.ShortPrefabName))
                    {
                        Effect.server.Run("assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab", entity.transform.position);
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        count++;
                    }
                }
                if (count == 0)
                    stuckCount++;
                else
                {
                    stuckCount = 0;
                    return;
                }
                StringBuilder sb = Pool.Get<StringBuilder>();
                if (config.printStuckInfo && stuckCount == 20)
                {
                    int entityCount = 0;
                    sb.Clear().AppendLine().Append("Tank has been stuck for over a minute! Current location: ").Append(apc.transform.position).Append(". Sending list of entities nearby:");
                    foreach (var entity in roadEntities)
                    {
                        if (entity.net.ID == apc.net.ID) continue;
                        sb.Append("\n - ").Append(entity.ShortPrefabName);
                        entityCount++;
                    }
                    if (entityCount == 0)
                        sb.Append("\nNO ENTITIES FOUND!");
                    else
                        sb.Append("\nIts recommended to check these entities and add them to filter, because players could abuse and block bradley!");
                    sb.Append("\nIt can be also a road that bradley cound't reach!");
                    plugin.PrintWarning(sb.ToString());
                }
                Pool.FreeUnmanaged(ref roadEntities);
                if (config.stuckErrorKill > 0 && stuckCount >= config.stuckErrorKill)
                {
                    if (config.broadcastStuck)
                    {
                        foreach (var player in targets)
                        {
                            if (player == null) continue;
                            plugin.SendReply(player, plugin.Lang("TankStuckTeleport", player.UserIDString));
                        }
                    }
                    int randPos = Core.Random.Range(0, checkpointCount);
                    apc.transform.position = roads[roadName].checkpoints[randPos];
                    apc.currentPathIndex = randPos;
                    apc.SendNetworkUpdate();
                    plugin.PrintWarning(sb.Clear().Append("Tank has been stuck for over ").Append(config.stuckErrorKill).Append(" checks and has been teleported to random position of the road at ").Append(apc.transform.position).Append('.').ToString());
                }
                Pool.FreeUnmanaged(ref sb);

            }

            private void RunTankTimer()
            {
                tankRemainingTime--;
                if (tankRemainingTime == 0)
                {
                    plugin.ClearTankHealth(apc, this);
                    plugin.KillTank(apc, this, "timeElapsed");
                    CancelInvoke();
                }
                else
                {
                    foreach (var target in uiUsers)
                        plugin.UpdateTankHealth(target, apc, this);
                }
            }

            private void UpdateHealth()
            {
                foreach (var target in uiUsers)
                {
                    if (target == null) continue;
                    if (!disabledUiUpdates.Contains(target.userID))
                        plugin.UpdateTankHealth(target, apc, this);
                }
            }

            public void CheckMLRSSpawn()
            {
                if (!config.profiles[profile].mlrsEnable) return;
                foreach (var health in config.profiles[profile].mlrsHealthSpawns)
                {
                    if (apc._health > health) continue;
                    if (ranMlrs.Contains(health)) continue;
                    ranMlrs.Add(health);
                    foreach (var attackerPlayer in targets)
                    {
                        if (Vector3.Distance(attackerPlayer.transform.position, apc.transform.position) > 125f) continue;
                        if (config.profiles[profile].mlrsSound != "")
                            EffectNetwork.Send(new Effect(config.profiles[profile].mlrsSound, attackerPlayer, 0, new Vector3(0, 1.7f, 0), Vector3.forward), attackerPlayer.net.connection);
                        if (config.profiles[profile].mlrsMessage)
                            plugin.SendReply(attackerPlayer, plugin.Lang("MlrsIncoming", attackerPlayer.UserIDString));
                        for (int i = 0; i < config.profiles[profile].mlrsAmount; i++)
                        {
                            Vector3 dropPos = attackerPlayer.transform.position;
                            float randomization = config.profiles[profile].mlrsRandom;
                            dropPos.x += Core.Random.Range(-randomization, randomization);
                            dropPos.z += Core.Random.Range(-randomization, randomization);
                            TimedExplosive rocket = GameManager.server.CreateEntity("assets/content/vehicles/mlrs/rocket_mlrs.prefab", new Vector3(dropPos.x, config.profiles[profile].mlrsHeight, dropPos.z)) as TimedExplosive;
                            rocket.timerAmountMin = 150;
                            rocket.timerAmountMax = 150;
                            rocket.explosionRadius = config.profiles[profile].mlrsExplosionRadius;
                            rocket.creatorEntity = apc;
                            ServerProjectile projectile = rocket.GetComponent<ServerProjectile>();
                            projectile.gravityModifier = Core.Random.Range(0.1f, 0.2f);
                            projectile.InitializeVelocity(new Vector3(0, -18, 0));
                            foreach (var damageType in rocket.damageTypes)
                            {
                                if (damageType.type == DamageType.Blunt) damageType.amount = config.profiles[profile].mlrsBluntDamage;
                                else if (damageType.type == DamageType.Explosion) damageType.amount = config.profiles[profile].mlrsExplosionDamage;
                                else if (damageType.type == DamageType.AntiVehicle) damageType.amount = 0;
                            }
                            rocket.Spawn();
                        }
                    }
                }
            }

            private void TryCustomAttackMethods()
            {
                bool foundValidTarget = false;
                foreach (var userTarget in targets)
                {
                    if (userTarget == null) continue;
                    if (!userTarget.isMounted) continue;
                    if (Vector3.Distance(userTarget.transform.position, apc.transform.position) > config.profiles[profile].viewRange) continue;
                    foundValidTarget = true;
                }
                if (!foundValidTarget) return;
                SeekerTarget targetTarget = null;
#if CARBON
                foreach (var rocketTarget in SeekerTarget.seekerTargets.Values)
#else
                foreach (var rocketTarget in (seekerTargets.GetValue(null) as Dictionary<SeekerTarget.ISeekerTargetOwner, SeekerTarget>).Values)
#endif
                {
                    Vector3 vector;
                    if (!rocketTarget.TryGetPosition(out vector)) continue;
                    if (Vector3.Distance(vector, apc.transform.position) > config.profiles[profile].viewRange) continue;
                    targetTarget = rocketTarget;
                    break;
                }
                if (targetTarget == null) return;
                TimedExplosive rocket = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_heatseeker.prefab", apc.topTurretYaw.position) as TimedExplosive;
                SeekingServerProjectile target = rocket.GetComponent<SeekingServerProjectile>();
                target.launchingDuration = 0;
#if CARBON
                target.nextTargetUpdateTime = float.MaxValue;
#else
                targetUpdateTime.SetValue(target, float.MaxValue);
#endif
                target.lockedTarget = targetTarget;
                rocket.Spawn();
            }

            private void TryDrawInitialTankHealth(BasePlayer player)
            {
                if (alreadyDrawnUis.Contains(player.userID)) return;
                plugin.DrawInitialTankHealth(player, this);
                alreadyDrawnUis.Add(player.userID);

            }

            public void TryRemoveComeTime()
            {
                if (tankRemainingTime == -1) return;
                if (removedComeTime) return;
                removedComeTime = true;
                if (tankRemainingTime > config.purchases[profile].killTime)
                    tankRemainingTime = config.purchases[profile].killTime;
                //UpdateTimeToPlayers();
            }

            private void Awake()
            {
                apc = GetComponent<BradleyAPC>();
                apc.SetFlag(BaseEntity.Flags.Reserved6, true);
            }

            private void FixedUpdate()
            {
                if (!initialized) return;
                DoCustomAI();
                DoCustomMove();
            }

            private void DoCustomAI()
            {
                apc.SetFlag(BaseEntity.Flags.Reserved5, TOD_Sky.Instance.IsNight);
                if (Interface.CallHook("OnBradleyApcThink", this) != null) return;
                apc.mainGunTarget = currentTarget;
                CheckBrake(apc.mainGunTarget != null);
                if (apc.mainGunTarget != null)
                    lastTarget = Time.realtimeSinceStartup;
                float distanceToEnd = Vector3.Distance(apc.transform.position, apc.currentPath[apc.currentPathIndex]);
                float slowerMovement = isLooped ? 20f : Vector3.Distance(apc.transform.position, apc.currentPath[checkpointCount - 1]);
                if (distanceToEnd > apc.stoppingDist)
                {
                    Vector3 lhs = BradleyAPC.Direction2D(apc.currentPath[apc.currentPathIndex], apc.transform.position);
                    float num2 = Vector3.Dot(lhs, apc.transform.right);
                    float num3 = Vector3.Dot(lhs, apc.transform.right);
                    float num4 = Vector3.Dot(lhs, -apc.transform.right);
                    if (Vector3.Dot(lhs, -apc.transform.forward) > num2)
                    {
                        if (num3 >= num4)
                            apc.turning = 1f;
                        else
                            apc.turning = -1f;
                    }
                    else
                        apc.turning = Mathf.Clamp(num2 * 3f, -1f, 1f);
                    float num5 = 1f - Mathf.InverseLerp(0f, 0.3f, Mathf.Abs(apc.turning));
                    float num7 = Mathf.InverseLerp(0.1f, 0.4f, Vector3.Dot(apc.transform.forward, Vector3.up));
                    apc.throttle = (0.1f + Mathf.InverseLerp(0f, 20f, slowerMovement) * 1f) * num5 + num7;
                }
                apc.DoWeaponAiming();
                apc.SendNetworkUpdate();
            }

            private void CheckBrake(bool hasTarget)
            {
                if (hasTarget && !brakes)
                {
                    apc.ApplyBrakeTorque(1f, true);
                    apc.ApplyBrakeTorque(1f, false);
                    brakes = true;
                }
                else if (!hasTarget && brakes && Time.realtimeSinceStartup - lastTarget > config.profiles[profile].loseIntrestTime)
                {
                    apc.ApplyBrakeTorque(0f, true);
                    apc.ApplyBrakeTorque(0f, false);
                    brakes = false;
                }
            }

            private void DoCustomMove()
            {
                Vector3 velocity = apc.myRigidBody.velocity;
                apc.throttle = Mathf.Clamp(apc.throttle, -1f, 1f);
                apc.leftThrottle = apc.throttle;
                apc.rightThrottle = apc.throttle;
                if (apc.turning > 0f)
                {
                    apc.rightThrottle = -apc.turning;
                    apc.leftThrottle = apc.turning;
                }
                else if (apc.turning < 0f)
                {
                    apc.leftThrottle = apc.turning;
                    apc.rightThrottle = apc.turning * -1f;
                }
                float num = Vector3.Distance(apc.transform.position, apc.currentPath[apc.currentPathIndex]);
                if (num < 5f)
                {
                    if (apc.currentPathIndex == checkpointCount - 1)
                        apc.currentPathIndex = 0;
                    else
                        apc.currentPathIndex++;
                }
                float num6 = apc.throttle;
                apc.leftThrottle = Mathf.Clamp(apc.leftThrottle + num6, -1f, 1f);
                apc.rightThrottle = Mathf.Clamp(apc.rightThrottle + num6, -1f, 1f);
                float t = Mathf.InverseLerp(2f, 1f, velocity.magnitude * Mathf.Abs(Vector3.Dot(velocity.normalized, apc.transform.forward)));
                float torqueAmount = Mathf.Lerp(apc.moveForceMax, apc.turnForce, t);
                float num7 = Mathf.InverseLerp(5f, 1.5f, velocity.magnitude * Mathf.Abs(Vector3.Dot(velocity.normalized, apc.transform.forward)));
                apc.ScaleSidewaysFriction(1f - num7);
                apc.SetMotorTorque(apc.leftThrottle, false, torqueAmount);
                apc.SetMotorTorque(apc.rightThrottle, true, torqueAmount);
                apc.impactDamager.damageEnabled = (apc.myRigidBody.velocity.magnitude > 2f);
            }

        }

        private static PluginConfig config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(config = new PluginConfig()
            {
                commands = new List<string>()
                {
                    "buytank",
                    "bt"
                },
                obstacleWhitelist = new HashSet<string>()
                {
                    "roadsign",
                    "train_track",
                    "road_tunnel",
                    "doorcloser"
                },
                killedEntities = new HashSet<string>()
                {
                    "servergibs_bradley",
                    "scraptransporthelicopter",
                    "magnetcrane.entity",
                    "minicopter.entity",
                    "supply_drop"
                },
                mapTracks = new Dictionary<string, List<string>>()
                {
                    { "default", new List<string> {
                        "default"
                    }},
                    { "HapisIsland", new List<string> {
                        "hapis",
                    }},
                    { "Detroit", new List<string> {
                        "default",
                        "custom"
                    }},
                    { "ParadiseIsland", new List<string> {
                        "custom"
                    }}
                },
                roadBlacklist = new Dictionary<string, List<string>>()
                {
                    { "default", new List<string> {
                        "Edited_756",
                        "Edited_33",
                    }},
                    { "Detroit", new List<string> {
                        "Edited_4",
                    }}
                },
                customRoads = new Dictionary<string, List<string>>()
                {
                    { "default", new List<string> {
                        "CustomRoad1",
                        "CustomRoad2"
                    }},
                    { "Detroit", new List<string> {
                        "DetroitRoad1",
                        "DetroitRoad2",
                        "DetroitRoad3"
                    }},
                    { "HapisIsland", new List<string> {
                        "HapisRoad2",
                        "HapisRoad3",
                        "HapisRoad4"
                    }}
                },
                spawns = new Dictionary<string, SpawnConfig>()
                {
                    { "default", new SpawnConfig() {
                        onlinePlayers = new Dictionary<int, int>() {
                            { 1, 4 },
                            { 2, 8 }
                        },
                        chances = new Dictionary<string, int>()
                        {
                            { "Normal", 5 },
                            { "Hard", 2 }
                        }
                    }},
                    { "custom", new SpawnConfig() {
                        respawnTime = 1200,
                        onlinePlayers = new Dictionary<int, int>() {
                            { 1, 4 },
                            { 2, 12 },
                            { 3, 20 }
                        },
                        trackList = new List<List<string>>() { new List<string>() { "0", "1" }, new List<string>() { "2", "3" } },
                        useLongestPath = false,
                        chances = new Dictionary<string, int>()
                        {
                            { "Hard", 3 },
                            { "Extreme", 1 }
                        }
                    }},
                    { "hapis", new SpawnConfig() {
                        onlinePlayers = new Dictionary<int, int>() {
                            { 1, 4 },
                            { 2, 8 }
                        },
                        trackList = new List<List<string>>() { new List<string>() { "HapisRoad1" } },
                        useLongestPath = false,
                        chances = new Dictionary<string, int>()
                        {
                            { "Normal", 5 },
                            { "Hard", 2 }
                        }
                    }}
                },
                purchases = new Dictionary<string, PurchaseConfig>()
                {
                    { "Normal", new PurchaseConfig() {
                        cooldown = 3600,
                        requiredItems = new List<ReqItemConfig>()
                        {
                            new ReqItemConfig()
                            {
                                shortname = "scrap",
                                skin = 0,
                                amount = 100,
                            },
                            new ReqItemConfig()
                            {
                                shortname = "metal.fragments",
                                skin = 0,
                                amount = 1500,
                            }
                        }
                    }},
                    { "Hard", new PurchaseConfig() {
                        cooldown = 7200,
                        maxDaily = 1,
                        requiredItems = new List<ReqItemConfig>()
                        {
                            new ReqItemConfig()
                            {
                                shortname = "scrap",
                                skin = 0,
                                amount = 1000,
                            },
                            new ReqItemConfig()
                            {
                                shortname = "currency",
                                skin = 0,
                                amount = 1500,
                            }
                        }
                    }},
                    { "Extreme", new PurchaseConfig() {
                        permission = "roadbradley.extreme",
                        cooldown = 7200,
                        maxDaily = 1,
                        purchasePermissionLimits = new Dictionary<string, int>()
                        {
                            { "roadbradley.limit.premium", 3 },
                            { "roadbradley.limit.vip", 2 },
                        },
                        requiredItems = new List<ReqItemConfig>()
                        {
                            new ReqItemConfig()
                            {
                                shortname = "scrap",
                                skin = 0,
                                amount = 1000,
                            },
                            new ReqItemConfig()
                            {
                                shortname = "currency",
                                skin = 0,
                                amount = 5000,
                            },
                            new ReqItemConfig()
                            {
                                shortname = "metal.fragments",
                                skin = 0,
                                amount = 10000,
                            }
                        }
                    }}
                },
                profiles = new Dictionary<string, ProfileConfig>()
                {
                    { "Normal", new ProfileConfig() {
                        targetItems = new HashSet<string>()
                        {
                            "rocket.launcher",
                            "explosive.timed",
                            "explosive.satchel"
                        },
                        tankScale = 0.6f,
                        viewRange = 50,
                    }},
                    { "Hard", new ProfileConfig() {
                        targetItems = new HashSet<string>()
                        {
                            "rocket.launcher",
                            "explosive.timed",
                            "explosive.satchel"
                        },
                        tankScale = 0.6f,
                        viewRange = 50,
                        health = 2500,
                        coaxBurstLength = 15,
                        coaxBulletDamage = 20,
                        mlrsEnable = true,
                        mlrsHealthSpawns = new List<float>()
                        {
                            2300,
                            1100
                        },
                        damageReward = new List<DamageRewardConfig>()
                        {
                            new DamageRewardConfig()
                            {
                                shortname = "currency",
                                amount = 2000,
                                skin = 0,
                                displayName = ""
                            }
                        }
                    }},
                    { "Extreme", new ProfileConfig() {
                        targetItems = new HashSet<string>()
                        {
                            "rocket.launcher",
                            "explosive.timed",
                            "explosive.satchel"
                        },
                        tankScale = 0.7f,
                        viewRange = 75,
                        health = 5000,
                        coaxBurstLength = 15,
                        coaxBulletDamage = 25,
                        mlrsEnable = true,
                        mlrsAmount = 4,
                        mlrsHealthSpawns = new List<float>()
                        {
                            4500,
                            3000,
                            1000,
                            100,
                        },
                        lootPreset = "custom",
                        damageReward = new List<DamageRewardConfig>()
                        {
                            new DamageRewardConfig()
                            {
                                shortname = "currency",
                                amount = 2000,
                                skin = 0,
                                displayName = ""
                            },
                            new DamageRewardConfig()
                            {
                                shortname = "scrap",
                                amount = 500,
                                skin = 0,
                                displayName = ""
                            }
                        }
                    }}
                },
                lootPresets = new Dictionary<string, LootConfig>()
                {
                    { "custom", new LootConfig() {
                        lootTable = new List<ItemConfig>()
                        {
                            new ItemConfig()
                            {
                                shortname = "explosive.timed",
                                amount = 1,
                                chance = 1,
                                skin = 0,
                                displayName = "",
                                additionalItems = new List<SubItemConfig>()
                            },
                            new ItemConfig()
                            {
                                shortname = "metal.refined",
                                amount = 1000,
                                chance = 2,
                                skin = 0,
                                displayName = "",
                                additionalItems = new List<SubItemConfig>()
                            },
                            new ItemConfig()
                            {
                                shortname = "rifle.ak",
                                amount = 1,
                                chance = 1,
                                skin = 0,
                                displayName = "",
                                additionalItems = new List<SubItemConfig>()
                                {
                                    new SubItemConfig()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 20,
                                        skin = 0,
                                        displayName = ""
                                    }
                                }
                            }
                        }

                    }}
                }
            }, true);
        }

        private class PluginConfig
        {
            [JsonProperty("Tank Purchase - Commands")]
            public List<string> commands = new List<string>();

            [JsonProperty("Tank Route - Use Asphalt Roads Only")]
            public bool asphaltOnly = false;

            [JsonProperty("Tank Route - Split roads into smaller (input distance in meters, 0 to disable)")]
            public int roadSplitSize = 0;

            [JsonProperty("Tank Route - Check for Map Obstacles (recommended for custom maps with prefabs on roads")]
            public bool checkObstacles = false;

            [JsonProperty("Tank Route - Remove Roads With Obstacles From Pool")]
            public bool removeObstacleRoads = false;

            [JsonProperty("Tank Route - Teleport Bradley To Random Road Position after X Stuck Errors (0, to disable)")]
            public int stuckErrorKill = 0;

            [JsonProperty("Tank Route - Broadcast Stuck To Fighters")]
            public bool broadcastStuck = true;

            [JsonProperty("Tank Route - Obstacle Keyword Whitelist")]
            public HashSet<string> obstacleWhitelist = new HashSet<string>();

            [JsonProperty("Tank Route - Remove first and last checkpoints from all routes")]
            public int removeCheckpointAmount = 1;

            [JsonProperty("Tank Route - Minimal Route Checkpoints To Use")]
            public int minRouteLength = 5;

            [JsonProperty("Tank Route - Remove Ring Road From Pool (Traveling Vendor Route)")]
            public bool removeRingRoad = false;

            [JsonProperty("Tank Route - Split Radtown Road To Bigger Part")]
            public bool splitRadTownRoad = false;

            [JsonProperty("Tank Route - Remove Timed-Event Roads From Purchasable Pool")]
            public bool removeTimedRoads = false;

            [JsonProperty("Tank Route - Randomize Spawn On Road")]
            public bool randomizeSpawn = true;

            [JsonProperty("Tank Route - Cooldown Between Same Road Used On Purchased Events (in seconds)")]
            public float timeCooldownRoads = 0f;

            [JsonProperty("Tank Route Editor - Height Offset")]
            public float editorHeightOffset = 0.1f;

            [JsonProperty("Targeting - Target NPC")]
            public bool targetNpc = false;

            [JsonProperty("Targeting - Target Sleepers")]
            public bool targetSleepers = false;

            [JsonProperty("Loot & Damage Share - Check For Caller Building Damage")]
            public bool callerDamage = true;

            [JsonProperty("Loot & Damage Share - Allow Damage For Caller Team Buildings")]
            public bool callerTeamCheck = true;

            [JsonProperty("Loot & Damage Share - Unowned Damage Entity Reminder")]
            public int damageAuthsRemind = 50;

            [JsonProperty("Loot & Damage Share - Unowned Damage Entity Bradley Kill")]
            public int damageAuths = 200;

            [JsonProperty("Loot & Damage Share - Ignore Crate Ownership Checks")]
            public bool ignoreOwnerships = false;

            [JsonProperty("Loot & Damage Share - Use Friends")]
            public bool useFriends = false;

            [JsonProperty("Loot & Damage Share - Use Clans")]
            public bool useClans = false;

            [JsonProperty("Loot & Damage Share - Use RUST Teams")]
            public bool useTeams = true;

            [JsonProperty("Loot & Damage Share - Limit Server Spawned Bradleys Loot To Top Damage Player")]
            public bool limitToTopDamage = false;

            [JsonProperty("Loot & Damage Share - Disable Unowned Tank Damage To Buildings")]
            public bool disableUnownedDamage = false;

            [JsonProperty("Loot & Damage Share - Disable Tank Damage To TC And Sleeping Bags")]
            public bool disableTcBagDamage = false;

            [JsonProperty("Purchases - Used Purchase System (0 - None, 1 - Economics, 2 - ServerRewards, 3 - ShoppyStock)")]
            public int moneyPlugin = 3;

            [JsonProperty("Purchases - Used Currency (If ShoppyStock Is Used)")]
            public string currency = "myCurrencyKey";

            [JsonProperty("Purchases - Currency Symbol (used in display)")]
            public string currencySymbol = "$";

            [JsonProperty("Purchases - Max Purchased Bradleys (0, to disable)")]
            public int maxBradleys = 3;

            [JsonProperty("Limits - Daily Limit Type (true - calls, false - won fights)")]
            public bool countLimitPerCalls = true;

            [JsonProperty("Tank Info - Display Timed Bradley Info")]
            public bool enableTankInfo = true;

            [JsonProperty("Tank Info - Timed Bradley Shop Name Format")]
            public string tankInfo = "{0} BRADLEY\n[Health: {1}]";

            [JsonProperty("Tank Info - Display Purchased Bradley Info")]
            public bool enablePurchasedTankInfo = true;

            [JsonProperty("Tank Info - Purchased Bradley Shop Name Format")]
            public string purchasedTankInfo = "{1}'s {0} BRADLEY\n[Health: {2}]";

            [JsonProperty("Tank Health UI - X Anchor (0-1)")]
            public float healthBarAnchorX = 0.5f;

            [JsonProperty("Tank Health UI - Y Anchor (0-1)")]
            public float healthBarAnchorY = 0f;

            [JsonProperty("Bag Respawns - Enabled")]
            public bool enabledBags = true;

            [JsonProperty("Bag Respawns - Max Bags Per Player")]
            public int maxBags = 4;

            [JsonProperty("Bag Respawns - Max Bed Distance From Player")]
            public float maxBedDistance = 100;

            [JsonProperty("OnEntityTakeDamage Return Value")]
            public bool hookReturnValue = false;

            [JsonProperty("PopUpAPI - PopUp Profile Name")]
            public string popUpProfile = "Legacy";

            [JsonProperty("Tank Disarm Frequency")]
            public int disarmFrequency = 1470;

            [JsonProperty("Enable CanRoadBradleyTarget Hook")]
            public bool enableTargetHook = false;

            [JsonProperty("Custom Tank Death Properties")]
            public TankDeathConfig customTankDeath = new TankDeathConfig();

            [JsonProperty("Tank Stuck - Print Info To Console")]
            public bool printStuckInfo = true;

            [JsonProperty("Tank Unstuck - Killed Entity Names")]
            public HashSet<string> killedEntities = new HashSet<string>();

            [JsonProperty("Tank Routes - Spawns Per Maps")]
            public Dictionary<string, List<string>> mapTracks = new Dictionary<string, List<string>>();

            [JsonProperty("Tank Routes - Purchasable Road ID Blacklist Per Map")]
            public Dictionary<string, List<string>> roadBlacklist = new Dictionary<string, List<string>>();

            [JsonProperty("Tank Routes - Custom Purchasable Roads Per Map")]
            public Dictionary<string, List<string>> customRoads = new Dictionary<string, List<string>>();

            [JsonProperty("Tank Routes - Spawn Configuration")]
            public Dictionary<string, SpawnConfig> spawns = new Dictionary<string, SpawnConfig>();

            [JsonProperty("Tank Routes - Purchasable")]
            public Dictionary<string, PurchaseConfig> purchases = new Dictionary<string, PurchaseConfig>();

            [JsonProperty("Tank Routes - Configuration")]
            public Dictionary<string, ProfileConfig> profiles = new Dictionary<string, ProfileConfig>();

            [JsonProperty("Loot - Presets")]
            public Dictionary<string, LootConfig> lootPresets = new Dictionary<string, LootConfig>();
        }

        private class TankDeathConfig
        {
            [JsonProperty("Show Road Bradley Death Locations On Map")]
            public bool showOnMap = true;

            [JsonProperty("Fireball Count (0, to disable)")]
            public int fireballCount = 0;

            [JsonProperty("Use Fireballs As Chest Lock (not recommended, high performance impact, if false, uses custom method)")]
            public bool fireballLock = false;

            [JsonProperty("Hide Gib Flying Spheres (used for their scaling, will make gibs 'jump' once)")]
            public bool hideGibSpheres = true;
        }

        private class SpawnConfig
        {
            [JsonProperty("Tank Route - Road IDs (Random, if more than one)")]
            public List<List<string>> trackList = new List<List<string>>();

            [JsonProperty("Tank Route - Use all default roads")]
            public bool useAllDefault = false;

            [JsonProperty("Tank Route - Use longest path")]
            public bool useLongestPath = true;

            [JsonProperty("Tank Route - Remove first and last checkpoints")]
            public int removeCheckpointAmount = 3;

            [JsonProperty("Spawns - Announce Spawn")]
            public bool announceSpawn = true;

            [JsonProperty("Spawns - Announce Kill")]
            public bool announceKill = true;

            [JsonProperty("Spawns - Respawn Time (in seconds)")]
            public int respawnTime = 900;

            [JsonProperty("Spawns - Respawn Time Randomize Value (goes +value and -value from option above)")]
            public int respawnTimeRandomize = 120;

            [JsonProperty("Spawns - Min. Online Players For Each Tank Amount (Amount Of Tanks: Amount Of Players)")]
            public Dictionary<int, int> onlinePlayers = new Dictionary<int, int>();

            [JsonProperty("Spawns - Chance")]
            public Dictionary<string, int> chances = new Dictionary<string, int>();
        }

        private class ProfileConfig
        {
            [JsonProperty("Tank Target - Max distance to target")]
            public float maxTargetDistance = 10;

            [JsonProperty("Tank Target - Targeted items")]
            public HashSet<string> targetItems = new HashSet<string>();

            [JsonProperty("Tank Target - Lose Target Intrest After X Seconds")]
            public int loseIntrestTime = 10;

            [JsonProperty("Tank Options - Model Scale")]
            public float tankScale = 1;

            [JsonProperty("Tank Options - Map Marker Type (None/Crate/Chinook/Cargoship)")]
            public string markerType = "Crate";

            [JsonProperty("Tank Options - Move Speed (0-1)")]
            public float moveSpeed = 1;

            [JsonProperty("Tank Options - Move Force")]
            public float moveForce = 2000;

            [JsonProperty("Tank Options - Health")]
            public float health = 1000;

            [JsonProperty("Tank Options - View Range")]
            public float viewRange = 100;

            [JsonProperty("Turret Options - Fire Rate")]
            public float coaxFireRate = 0.06f;

            [JsonProperty("Turret Options - Burst Length")]
            public int coaxBurstLength = 10;

            [JsonProperty("Turret Options - Aim Cone")]
            public float coaxAimCone = 3;

            [JsonProperty("Turret Options - Bullet Damage")]
            public float coaxBulletDamage = 15;

            [JsonProperty("Cannon Options - Explosion Radius")]
            public float cannonExplosionRadius = 8;

            [JsonProperty("Cannon Options - Blunt Damage")]
            public float cannonBluntDamage = 40;

            [JsonProperty("Cannon Options - Explosion Damage")]
            public float cannonExplosionDamage = 0;

            [JsonProperty("Custom Attack - Do Custom Attacks")]
            public bool doCustomAttacks = true;

            [JsonProperty("Custom Attack - Try Interval (in seconds)")]
            public float customAttackInterval = 15;

            [JsonProperty("Custom Attack - Try Interval Randomization (in seconds)")]
            public float customAttackIntervalRandomization = 5;

            [JsonProperty("MLRS Options - Enabled")]
            public bool mlrsEnable = false;

            [JsonProperty("MLRS Options - Amount Per Fighter")]
            public int mlrsAmount = 2;

            [JsonProperty("MLRS Options - Height Spawn")]
            public float mlrsHeight = 450;

            [JsonProperty("MLRS Options - Sound Alert Prefab Name")]
            public string mlrsSound = "assets/prefabs/tools/pager/effects/beep.prefab";

            [JsonProperty("MLRS Options - Chat Message Alert")]
            public bool mlrsMessage = true;

            [JsonProperty("MLRS Options - Position Randomization")]
            public float mlrsRandom = 25;

            [JsonProperty("MLRS Options - Health Level Spawns")]
            public List<float> mlrsHealthSpawns = new List<float>();

            [JsonProperty("MLRS Options - Explosion Radius")]
            public float mlrsExplosionRadius = 15;

            [JsonProperty("MLRS Options - Blunt Damage")]
            public float mlrsBluntDamage = 75;

            [JsonProperty("MLRS Options - Explosion Damage")]
            public float mlrsExplosionDamage = 350;

            [JsonProperty("Scientist Options - Disable Scientist Spawn")]
            public bool lockNpcSpawn = false;

            [JsonProperty("Scientist Options - Amount Of Scientists To Spawn (might not spawn all of them)")]
            public int scientistsToSpawn = 4;

            [JsonProperty("Scientist Options - Scientist Spawn Radius")]
            public float scientistsRadius = 3f;

            [JsonProperty("RF Disarm - Enabled")]
            public bool disarmEnabled = false;

            [JsonProperty("RF Disarm - Type (false - Change Frequency, true - Explode)")]
            public bool disarmType = true;

            [JsonProperty("RF Disarm - Radius From Tank")]
            public float disarmRadius = 35;

            [JsonProperty("Bradley Debris - Scale With Tank")]
            public bool gibsScale = true;

            [JsonProperty("Bradley Debris - Health")]
            public float gibsHealth = 500;

            [JsonProperty("Bradley Debris - Time To Cool Down")]
            public float gibsTime = 60;

            [JsonProperty("Loot - Crate Amount (If default loot preset)")]
            public int crateAmount = 4;

            [JsonProperty("Loot - Used Loot Preset (leave blank for default)")]
            public string lootPreset = "";

            [JsonProperty("Loot - Floor Rewards For Damage Dealt")]
            public bool damageRewardFloor = true;

            [JsonProperty("Loot - Rewards For Damage Dealt")]
            public List<DamageRewardConfig> damageReward = new List<DamageRewardConfig>();

            [JsonProperty("Loot - Fire Lock Time (in seconds)")]
            public int lockTime = 60;
        }

        private class DamageRewardConfig
        {
            [JsonProperty("Command (if not empty, item is ignored)")]
            public string command = "";

            [JsonProperty("Shortname")]
            public string shortname = "";

            [JsonProperty("Amount")]
            public int amount = 1;

            [JsonProperty("Skin")]
            public ulong skin = 0;

            [JsonProperty("Display Name")]
            public string displayName = "";
        }

        private class PurchaseConfig
        {
            [JsonProperty("Purchases - Required Permission (leave blank, to disable)")]
            public string permission = "";

            [JsonProperty("Purchases - Cooldown (in seconds, 0 to disable)")]
            public int cooldown = 0;

            [JsonProperty("Purchases - Max Daily (in seconds, 0 to disable)")]
            public int maxDaily = 3;

            [JsonProperty("Purchases - Max Purchased Bradley Permissions (from best to worse)")]
            public Dictionary<string, int> purchasePermissionLimits = new Dictionary<string, int>();

            [JsonProperty("Purchases - Time To Kill (in seconds, 0 to disable)")]
            public int killTime = 600;

            [JsonProperty("Purchases - Time To Come To Bradley (Kill Time + Coming Time)")]
            public int comingTime = 600;

            [JsonProperty("Purchases - Limit Loot To Team")]
            public bool ownerLoot = true;

            [JsonProperty("Purchases - Limit Target To Team")]
            public bool ownerTarget = true;

            [JsonProperty("Purchases - Limit Damage To Team")]
            public bool ownerDamage = true;

            [JsonProperty("Purchases - Kill Announce Enabled")]
            public bool announceKill = true;

            [JsonProperty("Purchases - Required Items")]
            public List<ReqItemConfig> requiredItems = new List<ReqItemConfig>();
        }

        private class ReqItemConfig
        {
            [JsonProperty("Shortname")]
            public string shortname = "";

            [JsonProperty("Amount")]
            public int amount = 1;

            [JsonProperty("Skin")]
            public ulong skin = 0;

            [JsonProperty("Custom Icon URL")]
            public string iconUrl = "";
        }

        private class LootConfig
        {
            [JsonProperty("Loot - Min. Crates")]
            public int minCrates = 2;

            [JsonProperty("Loot - Max. Crates")]
            public int maxCrates = 4;

            [JsonProperty("Loot - Min. Items Per Crate")]
            public int minItems = 4;

            [JsonProperty("Loot - Max. Items Per Crate")]
            public int maxItems = 5;

            [JsonProperty("Loot - Loot Table")]
            public List<ItemConfig> lootTable = new List<ItemConfig>();
        }

        private class ItemConfig
        {
            [JsonProperty("Shortname")]
            public string shortname = "";

            [JsonProperty("Amount")]
            public int amount = 1;

            [JsonProperty("Amount Randomizer Amount (+ and -)")]
            public int amountRandomize = 0;

            [JsonProperty("Always Include Chance (0-100)")]
            public float alwaysInclude = 0;

            [JsonProperty("Max Always Includes Per Loot (0 to disable)")]
            public int maxPerLoot = 0;

            [JsonProperty("Skin")]
            public ulong skin = 0;

            [JsonProperty("Display Name")]
            public string displayName = "";

            [JsonProperty("Chance")]
            public int chance = 1;

            [JsonProperty("Additional Items")]
            public List<SubItemConfig> additionalItems = new List<SubItemConfig>();
        }

        private class SubItemConfig
        {
            [JsonProperty("Shortname")]
            public string shortname = "";

            [JsonProperty("Amount")]
            public int amount = 1;

            [JsonProperty("Skin")]
            public ulong skin = 0;

            [JsonProperty("Display Name")]
            public string displayName = "";
        }

        private static LimitData limitData = new LimitData();
        private static RoadData roadData = new RoadData();
        private static BagData bagData = new BagData();

        private class LimitData
        {
            [JsonProperty("Player Limits")]
            public Dictionary<string, Dictionary<ulong, Dictionary<string, int>>> limits = new Dictionary<string, Dictionary<ulong, Dictionary<string, int>>>();
        }

        private class RoadData
        {
            [JsonProperty("Saved Roads")]
            public Dictionary<string, List<Vector3>> roads = new Dictionary<string, List<Vector3>>();
        }

        private class BagData
        {
            [JsonProperty("Sleeping Bags")]
            public Dictionary<ulong, List<ulong>> bags = new Dictionary<ulong, List<ulong>>();
        }

        private void LoadData()
        {
            limitData = Interface.Oxide.DataFileSystem.ReadObject<LimitData>($"{Name}/LimitData");
            roadData = Interface.Oxide.DataFileSystem.ReadObject<RoadData>($"{Name}/RoadData");
            bagData = Interface.Oxide.DataFileSystem.ReadObject<BagData>($"{Name}/BagData");
            timer.Every(Core.Random.Range(500, 700), SaveData);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/LimitData", limitData);
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/RoadData", roadData);
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/BagData", bagData);
        }

        private void LoadMessages()
        {
            Dictionary<string, string> langFile = new Dictionary<string, string>()
            {
                ["KillTime"] = "The time limit that you have for killing this Bradley.",
                ["CooldownTime"] = "The cooldown that you will get to call another Bradley of the same type.",
                ["DailyLimit"] = "The daily limit of the Bradley to be called.",
                ["EditorClosedNotSaved"] = "Route editor has been closed. No changes has been saved.",
                ["EditorHint"] = "To click on UI - open chat to get the cursor.\nIn order to add/edit/remove use Semi Automatic Rifle. You should get one.\n\n<color=#5c81ed>Shoot</color> - Create checkpoint on hit position.\n<color=#5c81ed>Shoot near checkpoint</color> - update its position.\n<color=#5c81ed>Shoot near checkpoint holding SPRINT</color> - Create checkpoint one index after shoot checkpoint.\n<color=#5c81ed>Shoot near checkpoint holding DUCK</color> - Remove Checkpoint.",
                ["RouteHelpV4"] = "Command usage: \n<color=#5c81ed>/tankroute <routeName></color> - Opens route editor with previously created/generated route.\nIf it is server-generated road you won't be able to edit the road.\n<color=#5c81ed>/tankroute clone <name></color> - Clones already existing road and starts editing it.\n<color=#5c81ed>/tankroute new</color> - Opens route editor without any road loaded.\n<color=#5c81ed>/tankroute default</color> - Displays list of available default roads. (Raw road paths from RUST)\n<color=#5c81ed>/tankroute edited</color> - Displays list of available roads edited automatically by RoadBradley plugin for usage.\n<color=#5c81ed>/tankroute custom</color> - Displays list of all user-made routes.",
                ["InvalidNewRoadSyntax"] = "Invalid syntax!\nCorrect usage: <color=#5c81ed>/tankroute new <routeName></color>",
                ["AvailableRoads"] = "List of available roads of <color=#5c81ed>{0}</color> type:",
                ["RoadListed"] = "<color=#5c81ed>•</color>  {0}",
                ["NoRoadFound"] = "<color=#5c81ed>There is no road of this type created yet!</color>",
                ["NoRoadNamedFound"] = "Can't find road with name <color=#5c81ed>{0}</color>.",
                ["RoadAlreadyCreated"] = "Road with this name is already created.\nTo edit or view this road, write <color=#5c81ed>/tankroute {0}</color>.",
                ["RoadPreviewOnly"] = "Roads different than custom made are PREVIEW-ONLY. To create new roads, use <color=#5c81ed>/tankroute new <routeName></color>.",
                ["RouteLoaded"] = "Route <color=#5c81ed>{0}</color> has been loaded to the editor.",
                ["RouteSaved"] = "Route <color=#5c81ed>{0}</color> has been saved in data file.",
                ["InsertInfo"] = "Shoot, where you want to insert new - <color=#5c81ed>{0}</color> checkpoint!",
                ["CheckpointRemove"] = "Checkpoint <color=#5c81ed>{0}</color> has been removed!",
                ["UpdateInfo"] = "Shoot, where you want to move - <color=#5c81ed>{0}</color> checkpoint!",
                ["CheckpointAdd"] = "New checkpoint has been added!",
                ["CheckpointInsert"] = "You've inserted new checkpoint!",
                ["CheckpointUpdate"] = "You've updated checkpoint!",
                ["CannotDamage"] = "Only the owner, their friends and/or team can damage this bradley!",
                ["FightUnauthorizedInfo"] = "Your tank is hitting buildings that are not owned by anyone, who fights right now.\nIf tank will keep shooting, it will despawn and fight will be lost.\nCurrent warning: <color=#5c81ed>{0}</color>/<color=#5c81ed>{1}</color>",
                ["FightLostUnauthorized"] = "You've lost your fight because tank tried to damage too many entities not owned by anyone, who fought the tank.",
                ["CannotLoot"] = "Only the owner, their friends or team can collect this bradley loot!",
                ["CannotOpenYet"] = "Loot is still being hacked...\nYou can open it in <color=#5c81ed>{0} seconds</color>.",
                ["NotBedOwner"] = "This bed is bradley death respawn, and You are not owner of this bed!\nYou cannot change its name!",
                ["RespawnPointRemoved"] = "Bradley death respawn point removed!",
                ["TooManyBags"] = "You've set too many respawn bags.\nRemove previously created first, in order to create new one!",
                ["RespawnPointSet"] = "New bradley respawn point set!",
                ["TankDeathAnnouncement"] = "The <color=#5c81ed>{0}</color> Bradley has been killed!\n\nDamage dealt to Bradley:",
                ["TankDeathAnnouncementPlayerFormat"] = "\n{0}. <color=#5c81ed>{1}</color> - {2}",
                ["KillReward"] = "You've got an reward for damaging <color=#5c81ed>{0}%</color> of Bradley's health:",
                ["KillRewardCurrency"] = "\n <color=#5c81ed>-</color> {0}$",
                ["KillRewardItem"] = "\n <color=#5c81ed>-</color> x{1} {0}",
                ["RespawnNotFound"] = "Unfortunatelly we've not found respawn point for your bradley fight.\nYou can add <color=#5c81ed>bradley</color> to sleeping bag name to make it spawn for bradley fight!",
                ["Respawned"] = "You've spawned in your bradley fight sleeping bag!",
                ["NoFreeRoad"] = "There is no free road on the map!\nWait a moment!",
                ["TooManyBradleys"] = "There is too many purchased bradleys on the map!\nWait a moment!",
                ["NoPurchases"] = "There is no bradleys available to purchase!",
                ["BradleyFights"] = "BRADLEY FIGHTS",
                ["Levels"] = "SELECT TIER",
                ["Description"] = "DESCRIPTION",
                ["TankStats"] = "TANK STATS",
                ["TankStatKeys"] = "Health\nBullet Damage\nBurst Length\nMLRS Strikes\nSAM Missiles",
                ["No"] = "NO",
                ["Yes"] = "YES",
                ["Requirements"] = "REQUIREMENTS",
                ["NoRequiredItems"] = "You don't have all required items! You can't call this Bradley right now.",
                ["DailyLimitReached"] = "You've reached your daily Bradley call limit! You can't call this Bradley right now.",
                ["TankOnCooldown"] = "The tank is on cooldown! You can call Bradley again in {0}.",
                ["TankAlreadySpawned"] = "There is already one of your Bradleys already driving. Kill it first in order to spawn another one!",
                ["SpawnButton"] = "SPAWN",
                ["LockedButton"] = "LOCKED",
                ["CanSpawnTank"] = "You can spawn tank now. Tank will be spawned on your nearby free road. Prepare for a fight!",
                ["RouteEditorTitle"] = "ROUTE EDITOR",
                ["CurrentRouteTitle"] = "CURRENT ROUTE",
                ["ShowCheckpoints"] = "SHOW CHECKPOINTS",
                ["ShowHelp"] = "SHOW USAGE HINT",
                ["SaveAndExit"] = "SAVE AND EXIT",
                ["DefaultRoute"] = "DEFAULT",
                ["RandomRoute"] = "RANDOM",
                ["PurchasableRoute"] = "PURCHASABLE",
                ["MergedRoute"] = "MERGED",
                ["CheckpointCount"] = "CHECKPOINTS: {0}",
                ["TankStuckTeleport"] = "Your Bradley has been stuck for too long and has been <color=#5c81ed>teleported</color>!",
                ["MlrsIncoming"] = "Bradley called MLRS strike! Look up!",
                ["NoPermission"] = "You don't have permission to use this command!",
                ["FightLost"] = "You've lost fight with the bradley!\nYou can try again by <color=#5c81ed>purchasing another one</color>!",
                ["BradleySpawned"] = "An <color=#5c81ed>{0}</color> Bradley has spawned at <color=#5c81ed>{1}</color>!",
                ["BradleyPurchased"] = "Your <color=#5c81ed>{0}</color> Bradley has spawned at <color=#5c81ed>{1}</color>!\nYou have <color=#5c81ed>{2}</color> to destroy it, and additional <color=#5c81ed>{3}</color> to came!",
                ["SpawnHelp"] = "Usage:\n/spawnrbtank <profile> <route> - Spawns road bradley with specified profile on specified route.\n/spawnrbtank <profile> <userId> - Spawns road bradley with specified profile on specified route as purchased by user.",
                ["ProfileNotFound"] = "Profile '{0}' not found.",
                ["RouteNotFound"] = "Route '{0}' not found.",
                ["RouteNotAddedToConfig"] = "Route '{0}' is not added to config in '{1}' tab in 'Tank Routes - Spawns Per Maps' section. Add it and reload the plugin to continue.",
                ["TankSpawned"] = "If there is no limits on the map, your tank should be succesfully spawned.",
                ["UserIdIncorrect"] = "Id '{0}' is not correct userId.",
                ["PlayerOffline"] = "Player with id '{0}' is offline.",
                ["TankSpawnedUser"] = "If there is no limits on the map, your tank for user '{0}' should be succesfully spawned.",
                ["InvalidRouteNameSyntax"] = "Invalid syntax!\nUsage: <color=#5c81ed>/tankroute clone <name></color>",
                ["RoadNotExist"] = "There is no route with this name. Try other route.",
                ["AlreadyCloned"] = "This route is already cloned! Try cloning the clone of the clonned road lol.",
            };
            foreach (var tank in config.profiles.Keys)
            {
                langFile.Add($"Tank_{tank}", $"{tank.ToUpper()} BRADLEY");
                langFile.Add($"Tank_{tank}_Description", $"{tank} Bradley Description. Can be changed in lang/en/RoadBradley.json!");
            }
            foreach (var purchase in config.purchases)
            {
                int counter = 0;
                foreach (var reqItem in purchase.Value.requiredItems)
                {
                    langFile.TryAdd($"ReqItemHint_{purchase.Key}_{counter}", $"This is item description of item No. {counter} in your {purchase.Key} profile. Can be changed in lang/en/RoadBradley.json!");
                    counter++;
                }
            }
            lang.RegisterMessages(langFile, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private class BetterColors
        {
            public static readonly string GreenBackgroundTransparent = "0.4509804 0.5529412 0.2705882 0.5450981";
            public static readonly string GreenText = "0.6078432 0.7058824 0.4313726 1";
            public static readonly string RedBackgroundTransparent = "0.6980392 0.2039216 0.003921569 0.5450981";
            public static readonly string RedBackground = "0.6980392 0.2039216 0.003921569 1";
            public static readonly string RedText = "0.9411765 0.4862745 0.3058824 1";
            public static readonly string Transparent = "1 1 1 0";
            public static readonly string White = "1 1 1 1";
            public static readonly string WhiteTransparent10 = "1 1 1 0.1019608";
            public static readonly string WhiteTransparent40 = "1 1 1 0.3921569";
            public static readonly string WhiteTransparent60 = "1 1 1 0.6";
            public static readonly string WhiteTransparent80 = "1 1 1 0.8";
            public static readonly string BlackTransparent10 = "0 0 0 0.1019608";
            public static readonly string BlackTransparent20 = "0 0 0 0.2";
            public static readonly string BlackTransparent50 = "0 0 0 0.5019608";
            public static readonly string LightGray = "0.9686275 0.9215686 0.8823529 1";
            public static readonly string LightGrayTransparent3_5 = "0.9686275 0.9215686 0.8823529 0.00829412";
            public static readonly string LightGrayTransparent3_9 = "0.9686275 0.9215686 0.8823529 0.0157451";
            public static readonly string LightGrayTransparent8 = "0.9686275 0.9215686 0.8823529 0.02043138";
            public static readonly string LightGrayTransparent12 = "0.9686275 0.9215686 0.8823529 0.0306471";
            public static readonly string LightGrayTransparent20 = "0.9686275 0.9215686 0.8823529 0.05";
            public static readonly string LightGrayTransparent30 = "0.9686275 0.9215686 0.8823529 0.3019608";
            public static readonly string LightGrayTransparent58 = "0.9686275 0.9215686 0.8823529 0.403922";
            public static readonly string LightGrayTransparent73 = "0.9686275 0.9215686 0.8823529 0.594118";
            public static readonly string RadioUiBackground = "0.1529412 0.1411765 0.1137255 1";
            public static readonly string CraftingRadialBackground = "0.1686275 0.1607843 0.1411765 0.7529412";
        }

        private static void SendJson(BasePlayer player, string json) => CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(player.net.connection), null, "AddUI", json);

        private void GenerateBaseUi()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer roadBradley = cui.CreateContainer("RoadBradleyUI_RoadBradley", BetterColors.Transparent, 0, 1, 0, 1, 0, 0, 0, 0, 0f, 0f, true, false, CUI.ClientPanels.HudMenu, null);
            //Background Blur
            cui.CreatePanel(roadBradley, "RoadBradleyUI_RoadBradley", BetterColors.BlackTransparent10, "assets/content/ui/uibackgroundblur.mat", 0, 1, 0, 1, 0, 0, 0, 0, true, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiBlur0", null, false);
            //Background Darker
            cui.CreatePanel(roadBradley, "RoadBradleyUI_RoadBradley", BetterColors.CraftingRadialBackground, "assets/content/ui/namefontmaterial.mat", 0, 1, 0, 1, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiPanel1", null, false);
            //Middle Anchor
            cui.CreatePanel(roadBradley, "RoadBradleyUI_RoadBradley", BetterColors.Transparent, null, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_MiddleAnchor", null, false);
            //Main Background
            cui.CreatePanel(roadBradley, "RoadBradleyUI_MiddleAnchor", BetterColors.RadioUiBackground, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, -350, 350, -225, 225, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_MainBackground", null, false);
            //Top Panel
            cui.CreatePanel(roadBradley, "RoadBradleyUI_MainBackground", BetterColors.LightGrayTransparent3_9, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 700, 418, 450, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TopPanel", null, false);
            //Title Icon
            cui.CreateSimpleImage(roadBradley, "RoadBradleyUI_TopPanel", "", "assets/icons/target.png", BetterColors.LightGray, null, 0f, 0f, 0f, 0f, 4, 28, 4, 28, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage2", null, false);
            //Title
            cui.CreateText(roadBradley, "RoadBradleyUI_TopPanel", BetterColors.LightGray, "", 21, 0f, 0f, 0f, 0f, 34, 512, 0, 32, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_Title", null, false);
            //Close Button
            cui.CreateProtectedButton(roadBradley, "RoadBradleyUI_TopPanel", BetterColors.RedBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 673, 695, 5, 27, "RoadBradleyUI close", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_CloseButton", null, false);
            //X Text
            cui.CreateText(roadBradley, "RoadBradleyUI_CloseButton", BetterColors.RedText, "✖", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiText3", null, false);
            //Left Panel
            cui.CreatePanel(roadBradley, "RoadBradleyUI_MainBackground", BetterColors.BlackTransparent20, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 220, 0, 418, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_LeftPanel", null, false);
            //Select Tier Background
            cui.CreatePanel(roadBradley, "RoadBradleyUI_LeftPanel", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 4, 216, 386, 414, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_SelectTierBackground", null, false);
            //Select Tier Text
            cui.CreateText(roadBradley, "RoadBradleyUI_SelectTierBackground", BetterColors.LightGrayTransparent73, "", 18, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_SelectTierText", null, false);
            //Right Panel
            cui.CreatePanel(roadBradley, "RoadBradleyUI_MainBackground", BetterColors.Transparent, null, 0f, 0f, 0f, 0f, 220, 700, 0, 418, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_RightPanel", null, false);
            //Bradley Info Title Background
            cui.CreatePanel(roadBradley, "RoadBradleyUI_RightPanel", BetterColors.LightGrayTransparent12, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 480, 382, 418, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_BradleyInfoTitleBackground", null, false);
            //Bradley Info Title Text
            cui.CreateText(roadBradley, "RoadBradleyUI_BradleyInfoTitleBackground", BetterColors.LightGray, "", 22, 0f, 0f, 0f, 0f, 38, 480, 0, 36, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_BradleyInfoTitleText", null, false);
            //Bradley Info Title Icon
            cui.CreateSimpleImage(roadBradley, "RoadBradleyUI_BradleyInfoTitleBackground", "", "assets/icons/examine.png", BetterColors.LightGray, null, 0f, 0f, 0f, 0f, 8, 32, 6, 30, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage4", null, false);
            //Tank Info Section
            cui.CreatePanel(roadBradley, "RoadBradleyUI_RightPanel", BetterColors.Transparent, null, 0f, 0f, 0f, 0f, 0, 480, 136, 382, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankInfoSection", null, false);
            //Description Title
            cui.CreateText(roadBradley, "RoadBradleyUI_TankInfoSection", BetterColors.LightGrayTransparent73, "", 15, 0f, 0f, 0f, 0f, 8, 200, 220, 240, TextAnchor.UpperLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_DescriptionTitle", null, false);
            //Description
            cui.CreateText(roadBradley, "RoadBradleyUI_TankInfoSection", BetterColors.LightGrayTransparent58, "", 11, 0f, 0f, 0f, 0f, 8, 378, 110, 220, TextAnchor.UpperLeft, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_Description", null, false);
            //Tank Stats Background
            cui.CreatePanel(roadBradley, "RoadBradleyUI_TankInfoSection", BetterColors.LightGrayTransparent3_5, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 8, 178, 8, 108, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankStatsBackground", null, false);
            //Tank Stats Title Background
            cui.CreatePanel(roadBradley, "RoadBradleyUI_TankStatsBackground", BetterColors.LightGrayTransparent8, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 170, 80, 100, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankStatsTitleBackground", null, false);
            //Tank Stats Title
            cui.CreateText(roadBradley, "RoadBradleyUI_TankStatsTitleBackground", BetterColors.LightGrayTransparent73, "", 13, 0f, 0f, 0f, 0f, 4, 170, 0, 20, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankStatsTitle", null, false);
            //Tank Stats Names
            cui.CreateText(roadBradley, "RoadBradleyUI_TankStatsBackground", BetterColors.LightGray, "", 9, 0f, 0f, 0f, 0f, 8, 130, 0, 76, TextAnchor.UpperLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankStatsNames", null, false);
            //Tank Stats Stats Background
            cui.CreatePanel(roadBradley, "RoadBradleyUI_TankStatsBackground", BetterColors.LightGrayTransparent20, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 130, 170, 0, 80, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankStatsStatsBackground", null, false);
            //Tank Stats Stats
            cui.CreateText(roadBradley, "RoadBradleyUI_TankStatsStatsBackground", BetterColors.LightGray, "", 9, 0f, 0f, 0f, 0f, 0, 40, 0, 76, TextAnchor.UpperCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankStatsStats", null, false);
            //Tank Kill Time Background
            cui.CreateProtectedButton(roadBradley, "RoadBradleyUI_TankInfoSection", BetterColors.LightGrayTransparent12, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 418, 472, 216, 238, "RoadBradleyUI hint KillTime", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankKillTimeBackground", null, false);
            //Tank Kill Time Skull
            cui.CreateSimpleImage(roadBradley, "RoadBradleyUI_TankKillTimeBackground", "", "assets/icons/skull.png", BetterColors.LightGrayTransparent58, null, 0f, 0f, 0f, 0f, 5, 20, 2, 17, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage5", null, false);
            //Tank Kill Time Stopwatch
            cui.CreateSimpleImage(roadBradley, "RoadBradleyUI_TankKillTimeBackground", "", "assets/icons/stopwatch.png", BetterColors.LightGray, null, 0f, 0f, 0f, 0f, 2, 14, 8, 20, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage6", null, false);
            //Tank Kill Time Time
            cui.CreateText(roadBradley, "RoadBradleyUI_TankKillTimeBackground", BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 22, 54, 0, 22, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankKillTimeTime", null, false);
            //Tank Cooldown Background
            cui.CreateProtectedButton(roadBradley, "RoadBradleyUI_TankInfoSection", BetterColors.LightGrayTransparent12, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 418, 472, 190, 212, "RoadBradleyUI hint CooldownTime", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankCooldownBackground", null, false);
            //Tank Cooldown Icon
            cui.CreateSimpleImage(roadBradley, "RoadBradleyUI_TankCooldownBackground", "", "assets/icons/farming/icon_age.png", BetterColors.LightGrayTransparent73, null, 0f, 0f, 0f, 0f, 4, 20, 3, 19, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage7", null, false);
            //Tank Cooldown Time
            cui.CreateText(roadBradley, "RoadBradleyUI_TankCooldownBackground", BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 22, 54, 0, 22, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankCooldownTime", null, false);
            //Tank Limit Background
            cui.CreateProtectedButton(roadBradley, "RoadBradleyUI_TankInfoSection", BetterColors.LightGrayTransparent12, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 418, 472, 164, 186, "RoadBradleyUI hint DailyLimit", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankLimitBackground", null, false);
            //Tank Limit Icon
            cui.CreateSimpleImage(roadBradley, "RoadBradleyUI_TankLimitBackground", "", "assets/icons/skull.png", BetterColors.LightGrayTransparent73, null, 0f, 0f, 0f, 0f, 4, 20, 3, 19, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage8", null, false);
            //Tank Limit Text
            cui.CreateText(roadBradley, "RoadBradleyUI_TankLimitBackground", BetterColors.LightGray, "", 13, 0f, 0f, 0f, 0f, 22, 54, 0, 22, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankLimitText", null, false);
            //Tank Requirements Background
            cui.CreatePanel(roadBradley, "RoadBradleyUI_RightPanel", BetterColors.LightGrayTransparent12, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 480, 112, 136, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankRequirementsBackground", null, false);
            //Tank Requirements Icon
            cui.CreateSimpleImage(roadBradley, "RoadBradleyUI_TankRequirementsBackground", "", "assets/icons/open.png", BetterColors.LightGrayTransparent73, null, 0f, 0f, 0f, 0f, 8, 26, 3, 21, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage9", null, false);
            //Tank Requirements Title
            cui.CreateText(roadBradley, "RoadBradleyUI_TankRequirementsBackground", BetterColors.LightGrayTransparent73, "", 15, 0f, 0f, 0f, 0f, 32, 480, 0, 24, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_TankRequirementsTitle", null, false);
            //Item Section
            cui.CreatePanel(roadBradley, "RoadBradleyUI_RightPanel", BetterColors.Transparent, null, 0f, 0f, 0f, 0f, 0, 480, 42, 112, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_ItemSection", null, false);
            //Bottom Panel Background
            cui.CreatePanel(roadBradley, "RoadBradleyUI_RightPanel", BetterColors.LightGrayTransparent3_9, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 480, 0, 42, false, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_BottomPanelBackground", null, false);
            //Spawn Button
            cui.CreateProtectedButton(roadBradley, "RoadBradleyUI_BottomPanelBackground", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 364, 474, 6, 36, "", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_SpawnButton", null, false);
            //Spawn Button Text
            cui.CreateText(roadBradley, "RoadBradleyUI_SpawnButton", BetterColors.GreenText, "", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_SpawnButtonText", null, false);
            //Bottom Info Icon
            cui.CreateSimpleImage(roadBradley, "RoadBradleyUI_BottomPanelBackground", "", "assets/icons/info.png", BetterColors.LightGrayTransparent73, null, 0f, 0f, 0f, 0f, 8, 32, 9, 33, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_UiImage12", null, false);
            //Bottom Info Text
            cui.CreateText(roadBradley, "RoadBradleyUI_BottomPanelBackground", BetterColors.LightGrayTransparent73, "", 11, 0f, 0f, 0f, 0f, 38, 356, 0, 42, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedRegular, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyUI_BottomInfoText", null, false);
            coreJson = CuiHelper.ToJson(roadBradley);
        }

        private void GenerateHealthUi()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer roadBradleyHealth = cui.CreateContainer("RoadBradleyHealthUI_RoadBradleyHealth", BetterColors.Transparent, config.healthBarAnchorX, config.healthBarAnchorX, config.healthBarAnchorY, config.healthBarAnchorY, 0, 0, 0, 0, 0f, 0f, false, false, CUI.ClientPanels.Hud, null);
            //Background Darker
            var element = cui.CreateSimpleImage(roadBradleyHealth, "RoadBradleyHealthUI_RoadBradleyHealth", "", "assets/icons/shadow.png", BetterColors.BlackTransparent50, null, 0f, 0f, 0f, 0f, -208, 188, 76, 108, 0f, 0f, false, false, null, null, false, "RoadBradleyHealthUI_UiImage0", null, false);
            foreach (var comp in element.Element.Components)
                if (comp is CuiImageComponent)
                    (comp as CuiImageComponent).ImageType = UnityEngine.UI.Image.Type.Tiled;
            //Health Bar Background
            cui.CreatePanel(roadBradleyHealth, "RoadBradleyHealthUI_RoadBradleyHealth", BetterColors.LightGrayTransparent12, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, -192, 180, 87, 92, false, 0f, 0f, false, false, null, null, false, "RoadBradleyHealthUI_HealthBarBackground", null, false);
            //Health Bar
            cui.CreatePanel(roadBradleyHealth, "RoadBradleyHealthUI_HealthBarBackground", BetterColors.RedBackground, "assets/content/ui/namefontmaterial.mat", 0f, 1f, 0f, 1f, 0, 0, 0, 0, false, 0f, 0f, false, false, null, null, false, "RoadBradleyHealthUI_HealthBar", null, false);
            //Explosion Icon
            cui.CreateSprite(roadBradleyHealth, "RoadBradleyHealthUI_RoadBradleyHealth", "assets/icons/explosion_sprite.png", BetterColors.WhiteTransparent80, null, 0f, 0f, 0f, 0f, -200, -184, 82, 98, 0f, 0f, false, false, null, null, false, "RoadBradleyHealthUI_UiImage1", null, false);
            //Bradley Name
            cui.CreateText(roadBradleyHealth, "RoadBradleyHealthUI_RoadBradleyHealth", BetterColors.LightGrayTransparent73, "", 12, 0f, 0f, 0f, 0f, -184, 16, 92, 108, TextAnchor.LowerLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyHealthUI_BradleyName", null, false);
            //Bradley Time
            cui.CreateText(roadBradleyHealth, "RoadBradleyHealthUI_RoadBradleyHealth", BetterColors.LightGrayTransparent73, "", 11, 0f, 0f, 0f, 0f, 82, 165, 93, 108, TextAnchor.LowerRight, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyHealthUI_BradleyTime", null, false);
            //Bradley Time Icon
            cui.CreateSimpleImage(roadBradleyHealth, "RoadBradleyHealthUI_RoadBradleyHealth", "", "assets/icons/stopwatch.png", BetterColors.LightGrayTransparent73, null, 0f, 0f, 0f, 0f, 166, 179, 93, 106, 0f, 0f, false, false, null, null, false, "RoadBradleyHealthUI_BradleyTimeIcon", null, false);
            healthJson = CuiHelper.ToJson(roadBradleyHealth);
        }

        private void GenerateEditorUi()
        {
            using CUI cui = new CUI(CuiHandler);
            CuiElementContainer roadBradleyEditor = cui.CreateContainer("RoadBradleyEditorUI_RoadBradleyEditor", BetterColors.Transparent, 1, 1, 0.5f, 0.5f, 0, 0, 0, 0, 0f, 0f, false, false, CUI.ClientPanels.HudMenu, null);
            //Main Background
            cui.CreatePanel(roadBradleyEditor, "RoadBradleyEditorUI_RoadBradleyEditor", BetterColors.RadioUiBackground, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, -176, -26, -96, 104, false, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_MainBackground", null, false);
            //Top Panel
            cui.CreatePanel(roadBradleyEditor, "RoadBradleyEditorUI_MainBackground", BetterColors.LightGrayTransparent3_9, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 0, 150, 176, 200, false, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_TopPanel", null, false);
            //Title
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_TopPanel", BetterColors.LightGray, "", 15, 0f, 0f, 0f, 0f, 6, 118, 0, 24, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_Title", null, false);
            //Close Button
            cui.CreateProtectedButton(roadBradleyEditor, "RoadBradleyEditorUI_TopPanel", BetterColors.RedBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 129, 147, 3, 21, "RoadBradleyEditorUI close", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_CloseButton", null, false);
            //X Text
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_CloseButton", BetterColors.RedText, "✖", 15, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_UiText0", null, false);
            //Bottom Panel
            cui.CreatePanel(roadBradleyEditor, "RoadBradleyEditorUI_MainBackground", BetterColors.Transparent, null, 0f, 0f, 0f, 0f, 0, 150, 0, 176, false, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_BottomPanel", null, false);
            //Route Name
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.GreenText, "", 15, 0f, 0f, 0f, 0f, 0, 150, 152, 172, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_RouteName", null, false);
            //Current Route Background
            cui.CreatePanel(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.LightGrayTransparent12, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 12, 138, 134, 152, false, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_CurrentRouteBackground", null, false);
            //Current Route Text
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_CurrentRouteBackground", BetterColors.LightGray, "", 12, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_CurrentRouteText", null, false);
            //Show Checkpoints Button
            cui.CreateProtectedButton(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 6, 144, 106, 128, "RoadBradleyEditorUI showCheckpoints", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_ShowCheckpointsButton", null, false);
            //Show Checkpoints Button Text
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_ShowCheckpointsButton", BetterColors.GreenText, "", 14, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_ShowCheckpointsButtonText", null, false);
            //Show Help Button
            cui.CreateProtectedButton(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 6, 144, 80, 102, "RoadBradleyEditorUI showHelp", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_ShowHelpButton", null, false);
            //Show Help Button Text
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_ShowHelpButton", BetterColors.GreenText, "", 14, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_ShowHelpButtonText", null, false);
            //Save And Exit Button
            cui.CreateProtectedButton(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.GreenBackgroundTransparent, BetterColors.Transparent, "", 1, "assets/content/ui/namefontmaterial.mat", 0f, 0f, 0f, 0f, 6, 144, 54, 76, "RoadBradleyEditorUI saveExit", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_SaveAndExitButton", null, false);
            //Save And Exit Button Text
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_SaveAndExitButton", BetterColors.GreenText, "", 14, 0, 1, 0, 1, 0, 0, 0, 0, TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_SaveAndExitButtonText", null, false);
            //Default Route
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.RedText, "", 13, 0f, 0f, 0f, 0f, 6, 61, 33, 51, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_DefaultRoute", null, false);
            //Random Route
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.RedText, "", 13, 0f, 0f, 0f, 0f, 6, 75, 15, 33, TextAnchor.MiddleLeft, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_RandomRoute", null, false);
            //Purchasable Route
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.RedText, "", 13, 0f, 0f, 0f, 0f, 61, 144, 33, 51, TextAnchor.MiddleRight, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_PurchasableRoute", null, false);
            //Occupied Route
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.GreenText, "", 13, 0f, 0f, 0f, 0f, 75, 144, 15, 33, TextAnchor.MiddleRight, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_OccupiedRoute", null, false);
            //Checkpoint Count
            cui.CreateText(roadBradleyEditor, "RoadBradleyEditorUI_BottomPanel", BetterColors.LightGrayTransparent73, "", 10, 0f, 0f, 0f, 0f, 0, 150, 2, 15, TextAnchor.LowerCenter, CUI.Handler.FontTypes.RobotoCondensedBold, VerticalWrapMode.Overflow, 0f, 0f, false, false, null, null, false, "RoadBradleyEditorUI_CheckpointCount", null, false);
            editorJson = CuiHelper.ToJson(roadBradleyEditor);
        }
    }
}