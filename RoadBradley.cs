//Reference: 0Harmony
using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Game.Rust.Libraries;
using Rust;
using UnityEngine;
using HarmonyLib;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("RoadBradley", "https://discord.gg/dNGbxafuJn", "1.5.3")]
    public class RoadBradley : RustPlugin
    {
        private static RoadBradley _plugin;
        [PluginReference] private readonly Plugin Economics, ServerRewards, Friends, Clans, ShoppyStock;
        private static HarmonyLib.Harmony harmony;

        private readonly List<BradleyAPC> roadBradleys = new List<BradleyAPC>();
        private readonly Dictionary<string, RoadInfo> roads = new Dictionary<string, RoadInfo>();
        private readonly Dictionary<ulong, Dictionary<string, int>> purchaseCooldowns = new Dictionary<ulong, Dictionary<string, int>>();
        private readonly Dictionary<BradleyAPC, int> bradleyTimers = new Dictionary<BradleyAPC, int>();
        private readonly Dictionary<Vector3, string> obstacleChecks = new Dictionary<Vector3, string>();
        private readonly Dictionary<string, SpawnConfig> mapRoutes = new Dictionary<string, SpawnConfig>();
        private readonly Dictionary<BradleyAPC, ulong> currentBradleyOwners = new Dictionary<BradleyAPC, ulong>();
        private readonly List<ulong> routeEditors = new List<ulong>();
        private readonly TankDeathInfo tankDeathInfo = new TankDeathInfo();
        private string mapName;
        private Timer killTimer;
        private static readonly Dictionary<string, string> markerPrefabs = new Dictionary<string, string>()
        {
            { "crate", "assets/prefabs/tools/map/cratemarker.prefab" },
            { "chinook", "assets/prefabs/tools/map/ch47marker.prefab" },
            { "cargoship", "assets/prefabs/tools/map/cargomarker.prefab" },
            { "generic", "assets/prefabs/tools/map/genericradiusmarker.prefab" },
            { "vending", "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab" }
        };

        private class RoadInfo
        {
            public bool isDefault = true;
            public bool isRandomEvent = false;
            public bool isPurchasable = false;
            public bool isNowOccupied = false;
            public string routeName = string.Empty;
            public List<Vector3> checkpoints = new List<Vector3>();
        }

        private class TankDeathInfo
        {
            public Vector3 tankDeathLoc = Vector3.zero;
            public string tankDeathProfile = "";
            public ulong tankDeathOwner = 0;
            public ulong topDmgUser = 0;
            public Dictionary<string, int> crateCount = new Dictionary<string, int>();
        }

        private void Init()
        {
            Unsubscribe(nameof(OnWeaponFired));
            Unsubscribe(nameof(OnPlayerAttack));
            Unsubscribe(nameof(OnLoseCondition));
        }

        private void OnServerInitialized()
        {
            _plugin = this;
            cmd.AddChatCommand("tankroute", this, nameof(TankRouteCommand));
            permission.RegisterPermission("roadbradley.admin", this);
            permission.RegisterPermission("roadbradley.bypass", this);
            config = Config.ReadObject<PluginConfig>();
            if (config.commands.Count == 0)
                config.commands.Add("buytank");
            AddCovalenceCommand("spawnrbtank", nameof(SpawnTankCommand));
            AddCovalenceCommand("tankcount", nameof(TankCountCommand));
            Config.WriteObject(config);
            LoadMessages();
            LoadData();
            harmony = new HarmonyLib.Harmony("com.ThePitereq.RoadBradley");
            harmony.Patch(original: AccessTools.Method(typeof(BradleyAPC), nameof(BradleyAPC.DoPhysicsMove)), prefix: new HarmonyMethod(typeof(BradleyLimitMovement), nameof(BradleyLimitMovement.Prefix)));
            /*foreach (var road in roadData.roads.ToList())
            {
                Vector3 prev = Vector3.zero;
                bool startDeleting = false;
                foreach (var checkpoint in road.Value.ToList())
                {
                    if (checkpoint == prev)
                        startDeleting = true;
                    prev = checkpoint;
                    if (startDeleting)
                        roadData.roads[road.Key].Remove(checkpoint);
                }
            }
            SaveData();*/
            if (config.checkObstacles)
                foreach (ProtoBuf.PrefabData entry in World.Serialization.world.prefabs)
                {
                    if (TerrainMeta.TopologyMap.GetTopology(entry.position, 2048) && !obstacleChecks.ContainsKey(entry.position))
                    {
                        string prefabName = StringPool.Get(entry.id).Split('/').Last().Replace(".prefab", "");
                        bool shouldAdd = true;
                        foreach (var whitelist in config.obstacleWhitelist)
                        {
                            if (prefabName.ToLower().Contains(whitelist.ToLower()))
                            {
                                shouldAdd = false;
                                break;
                            }
                        }
                        if (shouldAdd)
                            obstacleChecks.Add(entry.position, prefabName);
                    }
                }
            FindRoads();
            cmd.AddChatCommand("showroute", this, nameof(DebugShowRouteCommand)); //DEBUG
            foreach (var command in config.commands)
                cmd.AddChatCommand(command, this, nameof(BuyTankCommand));
            if (!roads.Any())
            {
                PrintWarning("Roads on map not found! Disabling the plugin...");
                Server.Command($"oxide.unload {this.Name}");
                return;
            }
            if (!config.mapTracks.ContainsKey("default"))
            {
                PrintWarning("Default road config not found! Disabling the plugin...");
                Server.Command($"oxide.unload {this.Name}");
                return;
            }
            mapName = ConVar.Server.levelurl.Split('/').Last();
            foreach (var configValue in config.mapTracks)
                if (mapName.Contains(configValue.Key))
                {
                    mapName = configValue.Key;
                    break;
                }
            foreach (var configValue in config.purchases.Values)
                if (configValue.permission != "")
                    permission.RegisterPermission(configValue.permission, this);
            if (config.mapTracks.ContainsKey(mapName))
                foreach (var spawnConfig in config.mapTracks[mapName])
                    mapRoutes.Add(spawnConfig, config.spawns[spawnConfig]);
            else
            {
                mapName = "default";
                if (ConVar.Server.level == "HapisIsland")
                    mapName = "HapisIsland";
                if (config.mapTracks.ContainsKey(mapName))
                    foreach (var spawnConfig in config.mapTracks[mapName])
                        mapRoutes.Add(spawnConfig, config.spawns[spawnConfig]);
            }
            if (config.ignoreOwnerships)
                Unsubscribe(nameof(CanLootEntity));
            if (!config.enabledBags)
                Unsubscribe(nameof(CanBeWounded));
            Puts($"Setting routes for '{mapName}' map profile...");
            GenerateDefaultTracks();
            if (config.purchases.Any())
                GeneratePurchasableTracks();
            foreach (var route in mapRoutes)
                SpawnDefaultRouteTank(route.Key);
            SaveData();
        }

        private void Unload()
        {
            SaveData();
            foreach (var bradley in roadBradleys)
                bradley?.Kill();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "RoadBradleyUI_Main");
                CuiHelper.DestroyUi(player, "RoadBradleyUI_Timer");
                CuiHelper.DestroyUi(player, "RoadBradleyUI_Edit");
            }
            harmony.UnpatchAll("com.ThePitereq.RoadBradley");
        }

        private void FindRoads()
        {
            int counter = 0;
            foreach (var road in TerrainMeta.Path.Roads)
            {
                if (config.asphaltOnly && road.Width <= 5) continue;
                roads.TryAdd($"{counter}", new RoadInfo()
                {
                    isDefault = true
                });
                roads[$"{counter}"].checkpoints = new List<Vector3>(road.Path.Points.ToList());
                roads.Add($"{counter}_Edited", new RoadInfo()
                {
                    isDefault = false,
                    isPurchasable = true
                });
                roads[$"{counter}_Edited"].checkpoints = new List<Vector3>();
                List<Vector3> alreadyListed = new List<Vector3>();
                bool failedCheck = false;
                foreach (var checkpoint in road.Path.Points.ToList())
                {
                    if (config.checkObstacles)
                    {
                        foreach (var obstacle in obstacleChecks)
                        {
                            if (!alreadyListed.Contains(obstacle.Key) && Vector3.Distance(checkpoint, obstacle.Key) < road.Width / 2)
                            {
                                alreadyListed.Add(obstacle.Key);
                                if (config.removeObstacleRoads)
                                {
                                    Puts($"There is an obstacle '{obstacle.Value}' on the road '{counter}' at '{obstacle.Key}'. Removing road from the road pool!");
                                    roads.Remove($"{counter}_Edited");
                                    failedCheck = true;
                                    break;
                                }
                                else
                                    Puts($"There is an obstacle '{obstacle.Value}' on the road '{counter}' at '{obstacle.Key}'.");
                            }
                        }
                        if (failedCheck) break;
                    }
                    Vector3 checkpointFixed = checkpoint;
                    checkpointFixed.y = TerrainMeta.HeightMap.GetHeight(checkpointFixed);
                    if (config.heightOffset != 0)
                        checkpointFixed.y += config.heightOffset;
                    roads[$"{counter}_Edited"].checkpoints.Add(checkpointFixed);
                }
                counter++;
                if (failedCheck)
                    continue;
            }
            foreach (var road in roadData.roads)
            {
                roads.Add(road.Key, new RoadInfo()
                {
                    isDefault = false,
                    checkpoints = road.Value
                });
            }
            SaveData();
        }

        private void GenerateDefaultTracks()
        {
            bool longestSet = false;
            foreach (var route in mapRoutes)
            {
                if (!config.spawns.ContainsKey(route.Key))
                {
                    PrintWarning($"Route '{route.Key}' not found in configuration! Skipping...");
                    continue;
                }
                if (route.Value.useLongestPath)
                {
                    if (longestSet)
                        PrintWarning($"You've already set one route to be on longest route! Bradleys can collide!");
                    longestSet = true;
                    int longest = 0;
                    string longestName = "";
                    foreach (var road in roads)
                    {
                        if (road.Value.isDefault) continue;
                        int count = road.Value.checkpoints.Count;
                        if (count > longest)
                        {
                            longest = count;
                            longestName = road.Key;
                        }
                    }
                    if (roads[longestName].checkpoints.Count < 5)
                    {
                        PrintWarning($"Route '{longestName}' is too small for a bradley! Cannot be used.");
                        continue;
                    }
                    if (config.removeTimedRoads)
                        roads[longestName].isPurchasable = false;
                    roads[longestName].isRandomEvent = true;
                    roads[longestName].routeName = route.Key;
                    bool isLooped = false;
                    if (Vector3.Distance(roads[longestName].checkpoints.First(), roads[longestName].checkpoints.Last()) < 5f)
                        isLooped = true;
                    if (!isLooped)
                    {
                        if (route.Value.removeCheckpointAmount * 2 >= roads[longestName].checkpoints.Count)
                            PrintWarning($"You are removing more start/end checkpoints that is available on road '{longestName}'!");
                        List<Vector3> removeCheckpoints = new List<Vector3>();
                        removeCheckpoints.AddRange(roads[longestName].checkpoints.Take(config.removePurchasableCheckpointAmount));
                        removeCheckpoints.AddRange(roads[longestName].checkpoints.TakeLast(config.removePurchasableCheckpointAmount));
                        foreach (var point in removeCheckpoints)
                            roads[longestName].checkpoints.Remove(point);
                        int pointCount = roads[longestName].checkpoints.Count - 1;
                        foreach (var point in roads[longestName].checkpoints.ToList())
                        {
                            roads[longestName].checkpoints.Add(roads[longestName].checkpoints[pointCount]);
                            pointCount--;
                        }
                    }
                    Puts($"Route '{longestName}' set and added as longest bradley route!");
                }
                else
                {
                    List<List<string>> routes = new List<List<string>>(route.Value.trackList);
                    if (route.Value.useAllDefault)
                    {
                        routes = new List<List<string>>();
                        foreach (var road in roads)
                            if (road.Value.isDefault)
                                routes.Add(new List<string>() { road.Key });
                    }
                    int routeCount = 0;
                    foreach (var road1 in routes)
                    {
                        string fixedName1 = $"{road1[0]}_Edited";
                        int i = int.TryParse(road1[0], out i) ? i : -1;
                        if (i == -1)
                            fixedName1 = road1[0];
                        if (!roads.ContainsKey(fixedName1))
                        {
                            PrintWarning($"No data found about road '{fixedName1}' on map! Skipping whole route!");
                            continue;
                        }
                        string roadDisplay = fixedName1;
                        if (route.Value.trackList.Any())
                            roadDisplay = string.Join(",", route.Value.trackList[routeCount]);
                        foreach (var road2 in road1.Skip(1))
                        {
                            string fixedName2 = $"{road2}_Edited";
                            int x;
                            if (!int.TryParse(road2, out x))
                                fixedName2 = road2;
                            if (!roads.ContainsKey(fixedName2))
                            {
                                PrintWarning($"No data found about road '{road2}' on map! Skipping this road in route!");
                                continue;
                            }
                            roads[fixedName1].checkpoints.AddRange(roads[fixedName2].checkpoints);
                            if (config.removeTimedRoads)
                            {
                                roads[fixedName2].isPurchasable = false;
                                roads[fixedName2].isRandomEvent = false;
                            }
                        }
                        if (roads[fixedName1].checkpoints.Count < 5)
                        {
                            PrintWarning($"Route made with roads '{roadDisplay}' is too small! Cannot be used.");
                            continue;
                        }
                        if (config.removeTimedRoads)
                            roads[fixedName1].isPurchasable = false;
                        roads[fixedName1].isRandomEvent = true;
                        roads[fixedName1].routeName = route.Key;
                        bool isLooped = false;
                        if (Vector3.Distance(roads[fixedName1].checkpoints.First(), roads[fixedName1].checkpoints.Last()) < 5f)
                            isLooped = true;
                        if (!isLooped)
                        {
                            if (i != -1)
                            {
                                if (route.Value.removeCheckpointAmount * 2 >= roads[fixedName1].checkpoints.Count)
                                    PrintWarning($"You are removing more start/end checkpoints that is available on road '{fixedName1}'!");
                                List<Vector3> removeCheckpoints = new List<Vector3>();
                                removeCheckpoints.AddRange(roads[fixedName1].checkpoints.Take(config.removePurchasableCheckpointAmount));
                                removeCheckpoints.AddRange(roads[fixedName1].checkpoints.TakeLast(config.removePurchasableCheckpointAmount));
                                foreach (var point in removeCheckpoints)
                                    roads[fixedName1].checkpoints.Remove(point);
                            }
                            int pointCount = roads[fixedName1].checkpoints.Count - 1;
                            foreach (var point in roads[fixedName1].checkpoints.ToList())
                            {
                                roads[fixedName1].checkpoints.Add(roads[fixedName1].checkpoints[pointCount]);
                                pointCount--;
                            }
                        }
                        Puts($"Route made with roads '{roadDisplay}' set and added as bradley route!");
                        routeCount++;
                    }
                }
            }
        }

        private void GeneratePurchasableTracks()
        {
            int counter = 0;
            foreach (var road in roads)
            {
                if (road.Value.isDefault) continue;
                if (config.removeTimedRoads && road.Value.isRandomEvent) continue;
                if (!road.Value.checkpoints.Any()) continue;
                int i = int.TryParse(road.Key.Split('_')[0], out i) ? i : -1;
                if (i == -1 && !(config.customRoads.ContainsKey(mapName) && config.customRoads[mapName].Contains(road.Key))) continue;
                if (i != -1 && config.roadBlacklist.ContainsKey(mapName) && config.roadBlacklist[mapName].Contains(i)) continue;
                road.Value.isPurchasable = true;
                bool isLooped = false;
                if (Vector3.Distance(road.Value.checkpoints.First(), road.Value.checkpoints.Last()) < 5f)
                    isLooped = true;
                if (!isLooped)
                {
                    if (i != -1)
                    {
                        if (config.removePurchasableCheckpointAmount * 2 >= road.Value.checkpoints.Count)
                            PrintWarning($"You are removing more start/end checkpoints that is available on road {road.Key}!");
                        List<Vector3> removeCheckpoints = new List<Vector3>();
                        removeCheckpoints.AddRange(road.Value.checkpoints.Take(config.removePurchasableCheckpointAmount));
                        removeCheckpoints.AddRange(road.Value.checkpoints.TakeLast(config.removePurchasableCheckpointAmount));
                        foreach (var point in removeCheckpoints)
                            road.Value.checkpoints.Remove(point);
                    }
                    int pointCount = road.Value.checkpoints.Count - 1;
                    foreach (var point in road.Value.checkpoints.ToList())
                    {
                        road.Value.checkpoints.Add(road.Value.checkpoints[pointCount]);
                        pointCount--;
                    }
                }
                counter++;
                Puts($"Route '{road.Key}' set and added as purchasable bradley route!");
            }
            if (counter == 0)
                PrintWarning("You have too less roads to make purchasable bradleys! Decrease amount of timed bradleys or disable purchases!");
            else
                Puts("Tracks for purchasable bradleys set successfully!");
        }

        private void SpawnDefaultRouteTank(string route)
        {
            int thisRouteCount = 0;
            foreach (var tank in roadBradleys.ToList())
            {
                if (tank == null)
                {
                    roadBradleys.Remove(tank);
                    continue;
                }
                if (tank.GetComponent<RoadBradleyController>().routeName == route)
                    thisRouteCount++;
            }
            int playerCount = BasePlayer.activePlayerList.Count;
            int bradleys = 0;
            foreach (var bradleyAmount in config.spawns[route].onlinePlayers)
            {
                if (playerCount >= bradleyAmount.Value)
                    bradleys = bradleyAmount.Key;
            }
            int bradleysToSpawn = bradleys - thisRouteCount;
            if (bradleysToSpawn <= 0)
            {
                if (thisRouteCount == 0)
                    timer.Once(900, () => SpawnDefaultRouteTank(route));
                return;
            }
            for (int i = 0; i < bradleysToSpawn; i++)
            {
                int chance = 0;
                foreach (var tankType in config.spawns[route].chances)
                    chance += tankType.Value;
                int rolledTank = Core.Random.Range(1, chance + 1);
                chance = 0;
                foreach (var tankType in config.spawns[route].chances)
                {
                    chance += tankType.Value;
                    if (chance >= rolledTank)
                    {
                        SpawnTank(tankType.Key, route);
                        break;
                    }
                }
            }
        }

        private void SpawnTank(string profileName, string routeName)
        {
            ProfileConfig configValue = config.profiles[profileName];
            List<string> possibleRoads = new List<string>();
            foreach (var road in roads)
            {
                if (road.Value.routeName == routeName && !road.Value.isNowOccupied)
                    possibleRoads.Add(road.Key);
            }
            if (!possibleRoads.Any())
            {
                PrintWarning($"Couldn't find suitable road for bradley with profile '{profileName}' on route '{routeName}'.");
                return;
            }
            string rolledRoad = possibleRoads[Core.Random.Range(0, possibleRoads.Count - 1)];
            int spawnPoint = 0;
            if (config.randomizeSpawn)
                spawnPoint = Core.Random.Range(0, roads[rolledRoad].checkpoints.Count - 1);
            Vector3 startPosition = roads[rolledRoad].checkpoints[spawnPoint] + new Vector3(0, 0.1f, 0);

            RuntimePath runtimePath = new RuntimePath();
            RuntimePath runtimePath2 = runtimePath;
            IAIPathNode[] nodes = new RuntimePathNode[roads[rolledRoad].checkpoints.Count];
            runtimePath2.Nodes = nodes;
            IAIPathNode iaipathNode = null;
            int num5 = 0;
            int num6 = roads[rolledRoad].checkpoints.Count - 1;
            for (int j = 0; j <= num6; j++)
            {
                IAIPathNode iaipathNode2 = new RuntimePathNode(roads[rolledRoad].checkpoints[j] + Vector3.up * 1f);
                if (iaipathNode != null)
                {
                    iaipathNode2.AddLink(iaipathNode);
                    iaipathNode.AddLink(iaipathNode2);
                }
                runtimePath.Nodes[num5] = iaipathNode2;
                iaipathNode = iaipathNode2;
                num5++;
            }
            RuntimeInterestNode interestNode = new RuntimeInterestNode(roads[rolledRoad].checkpoints[0] + Vector3.up * 1f);
            runtimePath.AddInterestNode(interestNode);
            RuntimeInterestNode interestNode2 = new RuntimeInterestNode(roads[rolledRoad].checkpoints.Last() + Vector3.up * 1f);
            runtimePath.AddInterestNode(interestNode2);
            BradleyAPC tank = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", startPosition) as BradleyAPC;
            roads[rolledRoad].isNowOccupied = true;
            tank.Spawn();
            tank.InstallPatrolPath(runtimePath);
            tank.gameObject.AddComponent<RoadBradleyController>();
            RoadBradleyController controller = tank.GetComponent<RoadBradleyController>();
            controller.roadInfo = roads[rolledRoad];
            controller.routeName = routeName;
            controller.profile = profileName;
            if (configValue.disarmEnabled)
            {
                GameObject go = new GameObject();
                go.AddComponent<BradleyDisarmZone>();
                BradleyDisarmZone bradleyDisarm = go.GetComponent<BradleyDisarmZone>();
                go.transform.SetParent(tank.transform, true);
                bradleyDisarm.SetupZone(tank);
            }
            roadBradleys.Add(tank);
            tank.currentPath = new List<Vector3>(roads[rolledRoad].checkpoints);
            tank.currentPathIndex = spawnPoint;
            tank.moveForceMax = configValue.moveForce;
            tank.throttle = configValue.moveSpeed;
            tank.leftThrottle = configValue.moveSpeed;
            tank.rightThrottle = configValue.moveSpeed;
            tank.searchRange = configValue.viewRange;
            tank.viewDistance = configValue.viewRange;
            if (configValue.lootPreset == "")
                tank.maxCratesToSpawn = configValue.crateAmount;
            else
            {
                int crateAmount = Core.Random.Range(config.lootPresets[configValue.lootPreset].minCrates, config.lootPresets[configValue.lootPreset].maxCrates + 1);
                tank.maxCratesToSpawn = crateAmount;
            }
            tank.coaxBurstLength = configValue.coaxBurstLength;
            tank.coaxFireRate = configValue.coaxFireRate;
            tank.coaxAimCone = configValue.coaxAimCone;
            tank.bulletDamage = configValue.coaxBulletDamage;
            tank._health = configValue.health;
            tank._maxHealth = configValue.health;
            tank.globalBroadcast = true;
            if (configValue.tankScale != 1)
            {
                timer.Once(5, () => {
                    if (tank != null && !tank.IsDestroyed)
                    {
                        SphereEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", tank.transform.position) as SphereEntity;
                        sphere.Spawn();
                        tank.SetParent(sphere, true);
                        sphere.LerpRadiusTo(configValue.tankScale, 1000);
                    }
                });
            }
            string markerType = configValue.markerType.ToLower();
            if (markerType != "none")
            {
                BaseEntity marker = GameManager.server.CreateEntity(markerPrefabs[markerType], tank.transform.position);
                marker.SetParent(tank, true);
                marker.Spawn();
                marker.transform.position = startPosition;
            }
            if (config.enableTankInfo)
            {
                VendingMachineMapMarker marker2 = GameManager.server.CreateEntity(markerPrefabs["vending"], tank.transform.position) as VendingMachineMapMarker;
                marker2.SetParent(tank, true);
                marker2.Spawn();
                marker2.transform.position = startPosition;
                marker2.markerShopName = string.Format(config.tankInfo, profileName.ToUpper());
            }
            if (config.spawns[routeName].announceSpawn)
                foreach (var player in BasePlayer.activePlayerList)
                    SendReply(player, Lang("BradleySpawned", player.UserIDString, profileName, PhoneController.PositionToGridCoord(startPosition)));
            tank.SendNetworkUpdate();
        }

        private void TryPurchaseTank(BasePlayer player, string profile, bool forceBuy = false)
        {
            if (!config.purchases.ContainsKey(profile)) return;
            if (!forceBuy)
            {
                string date = DateTime.Now.ToShortDateString();
                limitData.limits.TryAdd(date, new Dictionary<ulong, Dictionary<string, int>>());
                limitData.limits[date].TryAdd(player.userID, new Dictionary<string, int>());
                limitData.limits[date][player.userID].TryAdd(profile, 0);
                if (!permission.UserHasPermission(player.UserIDString, "roadbradley.bypass"))
                {
                    if (purchaseCooldowns.ContainsKey(player.userID) && purchaseCooldowns[player.userID].ContainsKey(profile)) return;
                    if (currentBradleyOwners.ContainsValue(player.userID)) return;
                    if (config.purchases[profile].maxDaily != 0 && limitData.limits[date][player.userID][profile] >= config.purchases[profile].maxDaily) return;
                }
            }
            string targetRoad = "";
            Dictionary<string, float> nearestRoad = new Dictionary<string, float>();
            foreach (var road in roads)
                if (road.Value.isPurchasable && !road.Value.isNowOccupied && road.Value.checkpoints.Any())
                    nearestRoad.Add(road.Key, Vector3.Distance(player.transform.position, road.Value.checkpoints[0]));
            foreach (var road in nearestRoad.OrderBy(x => x.Value))
            {
                targetRoad = road.Key;
                break;
            }
            CuiHelper.DestroyUi(player, "RoadBradleyUI_Main");
            if (targetRoad == "")
            {
                SendReply(player, Lang("NoFreeRoad", player.UserIDString));
                return;
            }
            if (config.maxBradleys > 0)
            {
                int spawnedPurchasedTanks = 0;
                foreach (var apc in roadBradleys)
                {
                    if (apc == null) continue;
                    RoadBradleyController apcCont = apc.GetComponent<RoadBradleyController>();
                    if (apcCont != null && apcCont.ownerId != 0)
                        spawnedPurchasedTanks++;
                }
                if (spawnedPurchasedTanks > config.maxBradleys)
                {
                    SendReply(player, Lang("TooManyBradleys", player.UserIDString));
                    return;
                }
            }
            if (!forceBuy)
            {
                if (!permission.UserHasPermission(player.UserIDString, "roadbradley.bypass") && !TakeResources(player, config.purchases[profile].requiredItems)) return;
                string date = DateTime.Now.ToShortDateString();
                limitData.limits[date][player.userID][profile]++;
                purchaseCooldowns.TryAdd(player.userID, new Dictionary<string, int>());
                purchaseCooldowns[player.userID].TryAdd(profile, 0);
                purchaseCooldowns[player.userID][profile] = config.purchases[profile].cooldown;
            }
            ProfileConfig configValue = config.profiles[profile];
            int spawnPoint = 0;
            if (config.randomizeSpawn)
                spawnPoint = Core.Random.Range(0, roads[targetRoad].checkpoints.Count - 1);
            Vector3 startPosition = roads[targetRoad].checkpoints[spawnPoint] + new Vector3(0, 0.1f, 0);
            BradleyAPC tank = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", startPosition) as BradleyAPC;
            Puts($"Tank '{profile}' purchased by '{player.displayName}' ({player.UserIDString}) has spawned on road '{targetRoad}'!");
            tank.Spawn();
            Interface.Call("OnRoadBradleyPurchased", player, tank, profile, targetRoad);
            roads[targetRoad].isNowOccupied = true;
            tank.gameObject.AddComponent<RoadBradleyController>();
            RoadBradleyController controller = tank.GetComponent<RoadBradleyController>();
            controller.roadInfo = roads[targetRoad];
            controller.profile = profile;
            controller.ownerId = player.userID;
            if (configValue.disarmEnabled)
            {
                GameObject go = new GameObject();
                go.AddComponent<BradleyDisarmZone>();
                BradleyDisarmZone bradleyDisarm = go.GetComponent<BradleyDisarmZone>();
                go.transform.SetParent(tank.transform, true);
                bradleyDisarm.SetupZone(tank);
            }
            roadBradleys.Add(tank);
            currentBradleyOwners.Add(tank, player.userID);
            bradleyTimers.TryAdd(tank, config.purchases[profile].killTime + config.purchases[profile].comingTime);
            tank.currentPath = new List<Vector3>(roads[targetRoad].checkpoints);
            tank.currentPathIndex = spawnPoint;
            tank.moveForceMax = configValue.moveForce;
            tank.throttle = configValue.moveSpeed;
            tank.leftThrottle = configValue.moveSpeed;
            tank.rightThrottle = configValue.moveSpeed;
            tank.searchRange = configValue.viewRange;
            tank.viewDistance = configValue.viewRange;
            if (configValue.lootPreset == "")
                tank.maxCratesToSpawn = configValue.crateAmount;
            else
            {
                int crateAmount = Core.Random.Range(config.lootPresets[configValue.lootPreset].minCrates, config.lootPresets[configValue.lootPreset].maxCrates + 1);
                tank.maxCratesToSpawn = crateAmount;
            }
            tank.coaxBurstLength = configValue.coaxBurstLength;
            tank.coaxFireRate = configValue.coaxFireRate;
            tank.coaxAimCone = configValue.coaxAimCone;
            tank.bulletDamage = configValue.coaxBulletDamage;
            tank._health = configValue.health;
            tank._maxHealth = configValue.health;
            tank.globalBroadcast = true;
            if (configValue.tankScale != 1)
            {
                timer.Once(5, () => {
                    if (tank != null && !tank.IsDestroyed)
                    {
                        SphereEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", tank.transform.position) as SphereEntity;
                        sphere.Spawn();
                        tank.SetParent(sphere, true);
                        sphere.LerpRadiusTo(configValue.tankScale, 1000);
                    }
                });
            }
            string markerType = configValue.markerType.ToLower();
            if (markerType != "none")
            {
                BaseEntity marker = GameManager.server.CreateEntity(markerPrefabs[markerType], tank.transform.position);
                marker.Spawn();
                marker.SetParent(tank, true);
            }
            if (config.enablePurchasedTankInfo)
            {
                VendingMachineMapMarker marker2 = GameManager.server.CreateEntity(markerPrefabs["vending"], startPosition) as VendingMachineMapMarker;
                marker2.Spawn();
                marker2.SetParent(tank, true);
                marker2.markerShopName = string.Format(config.purchasedTankInfo, profile.ToUpper(), player.displayName);
            }
            tank.SendNetworkUpdate();
            SendReply(player, Lang("BradleyPurchased", player.UserIDString, profile, PhoneController.PositionToGridCoord(startPosition), CalculateTime(config.purchases[profile].killTime), CalculateTime(config.purchases[profile].comingTime)));
            if (killTimer == null)
                killTimer = timer.Every(1, () => CheckTimers());
        }

        private void CheckTimers()
        {
            if (!bradleyTimers.Any() && !purchaseCooldowns.Any())
            {
                killTimer.Destroy();
                killTimer = null;
                return;
            }
            foreach (var tank in bradleyTimers.Keys.ToList())
            {
                if (tank == null)
                {
                    bradleyTimers.Remove(tank);
                    continue;
                }
                RoadBradleyController controller = tank.GetComponent<RoadBradleyController>();
                if (controller.ownerId == 0) continue;
                foreach (var fighter in controller.targets)
                {
                    BasePlayer player = BasePlayer.FindByID(fighter);
                    if (player == null) continue;
                    UpdateTimer(player, tank);
                }
                bradleyTimers[tank]--;
                if (bradleyTimers[tank] <= 0)
                {
                    BasePlayer player = BasePlayer.FindByID(controller.ownerId);
                    if (player != null)
                    {
                        SendReply(player, Lang("FightLost", player.UserIDString));
                        CuiHelper.DestroyUi(player, "RoadBradleyUI_Timer");
                    }
                    foreach (var fighter in controller.targets)
                    {
                        if (fighter == controller.ownerId) continue;
                        player = BasePlayer.FindByID(fighter);
                        if (player != null)
                        {
                            SendReply(player, Lang("FightLost", player.UserIDString));
                            CuiHelper.DestroyUi(player, "RoadBradleyUI_Timer");
                        }
                    }
                    Effect.server.Run("assets/prefabs/weapons/beancan grenade/effects/beancan_grenade_explosion.prefab", tank.transform.position);
                    tank.Kill();
                    bradleyTimers.Remove(tank);
                }
            }
            foreach (var timer1 in purchaseCooldowns.ToList())
            {
                foreach (var timer2 in timer1.Value.ToList())
                {
                    purchaseCooldowns[timer1.Key][timer2.Key]--;
                    if (purchaseCooldowns[timer1.Key][timer2.Key] <= 0)
                    {
                        purchaseCooldowns[timer1.Key].Remove(timer2.Key);
                        if (!purchaseCooldowns[timer1.Key].Any())
                            purchaseCooldowns.Remove(timer1.Key);
                    }
                }
            }
        }

        private static string CalculateTime(int time)
        {
            int seconds = time % 60;
            int minutes = (time - seconds) / 60;
            int remaingingMinutes = minutes % 60;
            int hours = (minutes - remaingingMinutes) / 60;
            if (hours == 0)
                return $"{minutes}m {seconds}s";
            else
                return $"{hours}h {remaingingMinutes}m";
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BradleyAPC)
            {
                if (info.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName == "rocket_mlrs") return false;
                BradleyAPC apc = entity as BradleyAPC;
                BasePlayer player = info.InitiatorPlayer;
                if (info == null || player == null) return null;
                RoadBradleyController controller = apc.GetComponent<RoadBradleyController>();
                if (controller == null) return null;
                if (controller.targets.Contains(player.userID))
                {
                    controller.damageDealt.TryAdd(player.userID, 0);
                    controller.damageDealt[player.userID] += info.damageTypes.Total();
                    controller.CheckMLRSSpawn();
                    return null;
                }
                if (controller.ownerId != 0 && config.purchases[controller.profile].ownerDamage)
                {
                    if ((controller.ownerId == player.userID) || (config.useFriends && Friends != null && AreFriends(controller.ownerId, player.userID)) || (config.useClans && Clans != null && Clans.Call<bool>("IsClanMember", controller.ownerId.ToString(), player.userID.ToString())) || (config.useTeams && player.Team != null && player.Team.members.Contains(controller.ownerId)))
                    {
                        controller.targets.Add(player.userID);
                        if (!controller.damageDealt.Any())
                            CheckFirstDamage(apc, controller);
                        controller.damageDealt.TryAdd(player.userID, 0);
                        controller.damageDealt[player.userID] += info.damageTypes.Total();
                        controller.CheckMLRSSpawn();
                    }
                    else
                    {
                        SendReply(player, Lang("CannotDamage", player.UserIDString));
                        return false;
                    }
                }
                else
                {
                    if (!controller.damageDealt.Any())
                        CheckFirstDamage(apc, controller);
                    controller.targets.Add(player.userID);
                    controller.damageDealt.TryAdd(player.userID, 0);
                    controller.damageDealt[player.userID] += info.damageTypes.Total();
                    controller.CheckMLRSSpawn();
                    return null;
                }
            }
            else if (config.callerDamage && info.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName == "maincannonshell")
            {
                float nearest = 10000;
                BradleyAPC apc = null;
                RoadBradleyController controller;
                /*if (entity is BasePlayer)
                {
					foreach (var bradley in roadBradleys)
					{
						float distance = Vector3.Distance(entity.transform.position, bradley.transform.position);
						if (distance < nearest)
						{
							nearest = distance;
							apc = bradley;
						}
					}
					if (apc == null) return null;
					controller = apc.GetComponent<RoadBradleyController>();
                    if (!controller.targets.Contains((entity as BasePlayer).userID))
                        return false;
                    else
                        return null;
                }*/
                if (entity.OwnerID == 0) return null;
                if (entity is BasePlayer) return null;
                foreach (var bradley in roadBradleys)
                {
                    float distance = Vector3.Distance(entity.transform.position, bradley.transform.position);
                    if (distance < nearest)
                    {
                        nearest = distance;
                        apc = bradley;
                    }
                }
                if (apc == null) return null;
                controller = apc.GetComponent<RoadBradleyController>();
                if (controller == null || controller.ownerId == 0) return null;
                if (config.callerTeamCheck)
                {
                    RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindPlayersTeam(entity.OwnerID);
                    if (team != null && team.members.Contains(controller.ownerId)) return null;
                }
                if (controller.targets.Contains(entity.OwnerID)) return null;
                BasePlayer player = BasePlayer.FindByID(controller.ownerId);
                controller.damageAuths++;
                if (controller.damageAuths % config.damageAuthsRemind == 0 && controller.damageAuths < config.damageAuths)
                    SendReply(player, Lang("FightUnauthorizedInfo", player.UserIDString, controller.damageAuths, config.damageAuths));
                if (controller.damageAuths == config.damageAuths)
                {
                    bradleyTimers[apc] = 0;
                    SendReply(player, Lang("FightLostUnauthorized", player.UserIDString, config.damageAuths));
                }
                return false;
            }
            else if (info.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName == "rocket_mlrs")
            {
                if (!roadBradleys.Any()) return null;
                DecayEntity hitEntity = entity as DecayEntity;
                if (hitEntity == null) return null;
                BradleyAPC closest = null;
                float distance = float.MaxValue;
                foreach (var brad in roadBradleys)
                {
                    float bradDistance = Vector3.Distance(entity.transform.position, brad.transform.position);
                    if (bradDistance < distance)
                    {
                        distance = bradDistance;
                        closest = brad;
                    }
                }
                if (distance > 100 && Vector3.Distance(tankDeathInfo.tankDeathLoc, entity.transform.position) < 100)
                {
                    if (hitEntity.OwnerID == tankDeathInfo.tankDeathOwner) return null;
                    BuildingPrivlidge cupboard = hitEntity.GetBuildingPrivilege();
                    if (cupboard == null) return null;
                    if (!cupboard.IsAuthed(tankDeathInfo.tankDeathOwner)) return false;
                    return null;
                }
                else if (distance > 150) return null;
                RoadBradleyController controller = closest.GetComponent<RoadBradleyController>();
                if (entity is BasePlayer && !controller.targets.Contains((entity as BasePlayer).userID)) return false;
                if (controller.ownerId == 0) return null;
                if (hitEntity.OwnerID == controller.ownerId) return null;
                BuildingPrivlidge tc = hitEntity.GetBuildingPrivilege();
                if (tc == null) return null;
                if (!tc.IsAuthed(controller.ownerId)) return false;
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
                if (controller == null) continue;
                controller.targets.Remove(player.userID);
            }
            if (config.enabledBags)
                return OnKilledByBradley(player, info);
            else
                return null;
        }

        private void OnEntityKill(BradleyAPC apc)
        {
            if (apc == null || apc.net == null) return;
            RoadBradleyController controller = apc.GetComponent<RoadBradleyController>();
            if (controller == null) return;
            if (controller.ownerId == 0)
            {
                int waitTime = Core.Random.Range(config.spawns[controller.roadInfo.routeName].respawnTime - config.spawns[controller.roadInfo.routeName].respawnTimeRandomize, config.spawns[controller.roadInfo.routeName].respawnTime + config.spawns[controller.roadInfo.routeName].respawnTimeRandomize);
                timer.Once(waitTime, () => SpawnDefaultRouteTank(controller.roadInfo.routeName));
            }
            controller.roadInfo.isNowOccupied = false;
            SphereEntity sphere = apc.GetParentEntity() as SphereEntity;
            if (sphere != null && !sphere.IsDestroyed)
                sphere.Kill();
            roadBradleys.Remove(apc);
            bradleyTimers.Remove(apc);
            currentBradleyOwners.Remove(apc);
        }

        private void OnEntityKill(HelicopterDebris debris)
        {
            SphereEntity sphere = debris.GetParentEntity() as SphereEntity;
            if (sphere != null && !sphere.IsDestroyed)
                sphere.Kill();
        }

        private void OnEntitySpawned(HelicopterDebris debris)
        {
            if (tankDeathInfo.tankDeathProfile == string.Empty) return;
            if (debris.ShortPrefabName == "servergibs_bradley")
            {
                NextTick(() =>
                {
                    if (debris == null || debris.IsDestroyed) return;
                    if (config.profiles[tankDeathInfo.tankDeathProfile].gibsScale)
                    {
                        float scale = config.profiles[tankDeathInfo.tankDeathProfile].tankScale;
                        if (scale != 1)
                        {
                            Vector3 pos = debris.transform.position;
                            SphereEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", debris.transform.position) as SphereEntity;
                            sphere.Spawn();
                            debris.SetParent(sphere, true);
                            sphere.LerpRadiusTo(scale, 10000);
                        }
                    }
                    debris.InitializeHealth(config.profiles[tankDeathInfo.tankDeathProfile].gibsHealth, config.profiles[tankDeathInfo.tankDeathProfile].gibsHealth);
                    debris.tooHotUntil = UnityEngine.Time.realtimeSinceStartup + config.profiles[tankDeathInfo.tankDeathProfile].gibsTime;
                    debris.SendNetworkUpdate();
                });
            }
        }

        private void OnEntitySpawned(LockedByEntCrate crate)
        {
            if (tankDeathInfo.tankDeathProfile == string.Empty) return;
            if (Vector3.Distance(crate.transform.position, tankDeathInfo.tankDeathLoc) < 10)
            {
                crate.skinID = 12345;
                crate.inventory.capacity = 12;
                if (tankDeathInfo.tankDeathOwner != 0 && config.purchases[tankDeathInfo.tankDeathProfile].ownerLoot)
                    crate.OwnerID = tankDeathInfo.tankDeathOwner;
                if (config.limitToTopDamage && crate.OwnerID == 0 && tankDeathInfo.topDmgUser != 0)
                    crate.OwnerID = tankDeathInfo.topDmgUser;
                if (config.profiles[tankDeathInfo.tankDeathProfile].lootPreset != "")
                {
                    foreach (var item in crate.inventory.itemList.ToList())
                    {
                        item.GetHeldEntity()?.Kill();
                        item.DoRemove();
                    }
                    string preset = config.profiles[tankDeathInfo.tankDeathProfile].lootPreset;
                    foreach (var item in config.lootPresets[preset].lootTable)
                        if (item.alwaysInclude > 0)
                        {
                            string key = $"{item.shortname}_{item.skin}";
                            if (item.maxPerLoot > 0 && tankDeathInfo.crateCount.ContainsKey(key) && tankDeathInfo.crateCount[key] >= item.maxPerLoot) continue;
                            if (item.alwaysInclude > Core.Random.Range(0f, 100f))
                            {
                                CreateItem(item, crate.inventory);
                                tankDeathInfo.crateCount.TryAdd(key, 0);
                                tankDeathInfo.crateCount[key]++;
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
                timer.Once(config.profiles[tankDeathInfo.tankDeathProfile].lockTime, () =>
                {
                    FireBall lockingEnt = crate?.lockingEnt?.GetComponent<FireBall>();
                    if (lockingEnt != null && !lockingEnt.IsDestroyed)
                    {
                        lockingEnt.CancelInvoke(lockingEnt.Extinguish);
                        lockingEnt.Extinguish();
                    }
                    crate?.CancelInvoke(crate.Think);
                    crate?.SetLocked(false);
                });
            }
        }

        private bool CreateItem(ItemConfig item, ItemContainer storage)
        {
            int amount = item.amount;
            if (item.amountRandomize != 0)
                amount = Core.Random.Range(item.amount - item.amountRandomize, item.amount + item.amountRandomize);
            Item itemOutput = ItemManager.CreateByName(item.shortname, amount, item.skin);
            if (itemOutput == null)
            {
                PrintWarning($"Couldn't create item with shortname {item.shortname}. This item doesn't exist!");
                return false;
            }
            itemOutput.name = item.displayName;
            itemOutput.MoveToContainer(storage);
            if (!item.additionalItems.Any()) return true;
            foreach (var additionalItem in item.additionalItems)
            {
                Item additionalItemOutput = ItemManager.CreateByName(additionalItem.shortname, additionalItem.amount, additionalItem.skin);
                additionalItemOutput.name = additionalItem.displayName;
                additionalItemOutput.MoveToContainer(storage);
            }
            return true;
        }

        private object CanLootEntity(BasePlayer player, LockedByEntCrate crate)
        {
            if (crate.OwnerID == 0) return null;
            else if (crate.skinID == 12345)
            {
                if ((crate.OwnerID == player.userID) || (config.useFriends && Friends != null && AreFriends(crate.OwnerID, player.userID)) || (config.useClans && Clans != null && Clans.Call<bool>("IsClanMember", crate.OwnerID.ToString(), player.userID.ToString())) || (config.useTeams && player.Team != null && player.Team.members.Contains(crate.OwnerID))) return null;
                else
                {
                    SendReply(player, Lang("CannotLoot", player.UserIDString));
                    return false;
                }
            }
            return null;
        }

        private void OnEntityDeath(BradleyAPC apc)
        {
            if (apc.net == null) return;
            RoadBradleyController controller = apc.GetComponent<RoadBradleyController>();
            if (controller == null) return;
            if (controller.ownerId == 0)
            {
                Puts($"Bradley from route '{controller.routeName}' has been destroyed!");
                tankDeathInfo.tankDeathOwner = 0;
            }
            else
            {
                Puts($"Bradley purchased by '{controller.ownerId}' ({controller.profile}) has been destroyed!");
                Interface.Call("OnRoadBradleyKilled", apc, controller.ownerId, controller.profile);
                foreach (var playerId in controller.targets)
                {
                    BasePlayer player = BasePlayer.FindByID(playerId);
                    if (player != null)
                        CuiHelper.DestroyUi(player, "RoadBradleyUI_Timer");
                }
                tankDeathInfo.tankDeathOwner = controller.ownerId;
            }
            tankDeathInfo.tankDeathLoc = apc.transform.position;
            tankDeathInfo.tankDeathProfile = controller.profile;
            tankDeathInfo.crateCount.Clear();
            foreach (var damageDealt in controller.damageDealt.OrderByDescending(x => x.Value))
            {
                tankDeathInfo.topDmgUser = damageDealt.Key;
                break;
            }
            if ((!string.IsNullOrEmpty(controller.routeName) && config.spawns[controller.routeName].announceKill) || (controller.ownerId != 0 && config.purchases[controller.profile].announceKill))
            {
                Dictionary<string, int> damageScoreboard = new Dictionary<string, int>();
                foreach (var damageDealt in controller.damageDealt.OrderByDescending(x => x.Value))
                {
                    BasePlayer player = BasePlayer.FindByID(damageDealt.Key);
                    if (player == null) continue;
                    damageScoreboard.TryAdd(player.displayName, Convert.ToInt32(damageDealt.Value));
                }
                List<BasePlayer> broadcasters = new List<BasePlayer>();
                if (controller.ownerId != 0 && config.purchases[controller.profile].announceKillFighters)
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
                foreach (var player in broadcasters)
                {
                    string text = Lang("TankDeathAnnouncement", player.UserIDString, controller.profile.ToUpper());
                    int counter = 0;
                    foreach (var dealt in damageScoreboard)
                    {
                        counter++;
                        text += Lang("TankDeathAnnouncementPlayerFormat", player.UserIDString, counter, dealt.Key, dealt.Value);
                    }
                    SendReply(player, text);
                }
            }
            if (config.profiles[controller.profile].damageReward.Any() && controller.damageDealt.Any())
            {
                float damageSum = 0;
                foreach (var damage in controller.damageDealt)
                    damageSum += damage.Value;
                foreach (var playerId in controller.damageDealt.OrderByDescending(x => x.Value))
                {
                    BasePlayer damagePlayer = BasePlayer.FindByID(playerId.Key);
                    if (damagePlayer == null) continue;
                    float percentage = playerId.Value / damageSum;
                    string rewardItems = Lang("KillReward", damagePlayer.UserIDString, percentage * 100f);
                    foreach (var item in config.profiles[controller.profile].damageReward)
                    {
                        if (item.shortname == "currency")
                        {
                            int amount = Convert.ToInt32(item.amount * percentage);
                            switch (config.moneyPlugin)
                            {
                                case 1:
                                    if (Economics != null)
                                        Economics.Call<double>("Deposit", damagePlayer.userID, (double)amount);
                                    break;
                                case 2:
                                    if (ServerRewards != null)
                                        ServerRewards.Call<int>("AddPoints", damagePlayer.userID, amount);
                                    break;
                                case 3:
                                    if (ShoppyStock != null)
                                        ShoppyStock.Call<int>("AddCurrency", config.currency, damagePlayer.userID, amount);
                                    break;
                                default:
                                    break;
                            }
                            rewardItems += Lang("KillRewardCurrency", damagePlayer.UserIDString, amount);
                        }
                        else
                        {
                            int itemAmount = Convert.ToInt32(item.amount * percentage);
                            if (itemAmount == 0) continue;
                            Item rewardItem = ItemManager.CreateByName(item.shortname, itemAmount, item.skin);
                            rewardItem.name = item.displayName;
                            if (!rewardItem.MoveToContainer(damagePlayer.inventory.containerMain))
                                rewardItem.Drop(damagePlayer.eyes.position, Vector3.zero);
                            string displayName = item.displayName == string.Empty ? rewardItem.info.displayName.english : item.displayName;
                            rewardItems += Lang("KillRewardItem", damagePlayer.UserIDString, displayName, itemAmount);
                        }
                    }
                    SendReply(damagePlayer, rewardItems);
                }
            }
            timer.Once(2, () => tankDeathInfo.tankDeathProfile = string.Empty);
        }

        private object CanBradleyApcTarget(BradleyAPC apc, BasePlayer player)
        {
            if (!roadBradleys.Contains(apc)) return null;
            RoadBradleyController controller = apc.GetComponent<RoadBradleyController>();
            if (controller == null) return null;
            object call = Interface.Call("CanRoadBradleyTarget", apc, player);
            if (call is bool) return call;
            if (controller.targets.Contains(player.userID))
            {
                if (apc.IsVisibleAndCanSee(player.eyes.position))
                {
                    if (player.userID < 10000000) return config.targetNpc;
                    else return null;
                }
                else return false;
            }
            else
            {
                ProfileConfig configValue = config.profiles[controller.profile];
                if (!config.targetSleepers && player.IsSleeping()) return false;
                if (Vector3.Distance(player.transform.position, apc.transform.position) <= configValue.maxTargetDistance || (player.GetActiveItem() != null && configValue.targetItems.Contains(player.GetActiveItem().info.shortname)))
                {
                    if (controller.ownerId != 0 && config.purchases[controller.profile].ownerTarget)
                    {
                        if ((controller.ownerId == player.userID) || (config.useFriends && Friends != null && AreFriends(controller.ownerId, player.userID)) || (config.useClans && Clans != null && Clans.Call<bool>("IsClanMember", controller.ownerId.ToString(), player.userID.ToString())) || (config.useTeams && player.Team != null && player.Team.members.Contains(controller.ownerId)))
                        {
                            if (!controller.targets.Any())
                                CheckFirstDamage(apc, controller);
                            controller.targets.Add(player.userID);
                            return null;
                        }
                        else return false;
                    }
                    else
                    {
                        if (!controller.targets.Any())
                            CheckFirstDamage(apc, controller);
                        controller.targets.Add(player.userID);
                        if (player.userID < 10000000) return config.targetNpc;
                        else return null;
                    }
                }
                else return false;
            }
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            RoadEditorController controller = player.GetComponent<RoadEditorController>();
            if (controller == null) return;
            projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
            projectile.SendNetworkUpdateImmediate();
        }

        private void OnLoseCondition(Item item, ref float amount)
        {
            BasePlayer player = item.GetOwnerPlayer();
            if (player == null) return;
            RoadEditorController controller = player.GetComponent<RoadEditorController>();
            if (controller == null) return;
            amount = 0;
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info == null) return;
            RoadEditorController controller = attacker.GetComponent<RoadEditorController>();
            if (controller == null) return;
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
                            ShowEditUI(attacker);
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
                    ShowEditUI(attacker);
                    SendReply(attacker, Lang("CheckpointAdd", attacker.UserIDString));
                    DisplayRoute(attacker, 2);
                }
            }
            else if (controller.currentMode == "Insert")
            {
                controller.cachedCheckpoints.Insert(controller.currentIndex + 1, hitPosition);
                controller.currentMode = "";
                ShowEditUI(attacker);
                SendReply(attacker, Lang("CheckpointInsert", attacker.UserIDString));
                DisplayRoute(attacker, 2);
            }
            else if (controller.currentMode == "Update")
            {
                controller.cachedCheckpoints[controller.currentIndex] = hitPosition;
                controller.currentMode = "";
                ShowEditUI(attacker);
                SendReply(attacker, Lang("CheckpointUpdate", attacker.UserIDString));
                DisplayRoute(attacker, 2);
            }
        }

        private void OnEntitySpawned(TimedExplosive entity)
        {
            if (entity.ShortPrefabName != "maincannonshell") return;
            List<BradleyAPC> bradleys = new List<BradleyAPC>();
            Vis.Entities(entity.transform.position, 3f, bradleys);
            if (!bradleys.Any()) return;
            RoadBradleyController controller = bradleys[0].GetComponent<RoadBradleyController>();
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

        private object CanRenameBed(BasePlayer player, SleepingBag bed, string bedName)
        {
            if (bed.niceName.Contains("bradley"))
            {
                if (bed.OwnerID != player.userID)
                {
                    SendReply(player, Lang("NotBedOwner", player.UserIDString));
                    return false;
                }
                bagData.bags[player.userID].Remove(bed.net.ID.Value);
                SendReply(player, Lang("RespawnPointRemoved", player.UserIDString));
            }
            if (bedName.ToLower().Contains("bradley"))
            {
                bagData.bags.TryAdd(player.userID, new List<ulong>());
                if (bagData.bags[player.userID].Count > config.maxBags)
                {
                    SendReply(player, Lang("TooManyBags", player.UserIDString));
                    return false;
                }
                if (bed.OwnerID != player.userID)
                {
                    SendReply(player, Lang("NotBedOwner", player.UserIDString));
                    return false;
                }
                bagData.bags[player.userID].Add(bed.net.ID.Value);
                SendReply(player, Lang("RespawnPointSet", player.UserIDString));
            }
            return null;
        }

        private object CanDropActiveItem(BasePlayer player)
        {
            if (player == null || player is NPCPlayer) return null;
            if (player.lastAttacker != null && player.lastAttacker is BradleyAPC) return false;
            return null;
        }

        private object OnKilledByBradley(BasePlayer victim, HitInfo info, bool wasWounded = false)
        {
            if (victim == null || info == null) return null;
            if (victim is NPCPlayer) return null;
            if (!bagData.bags.ContainsKey(victim.userID) || !bagData.bags[victim.userID].Any()) return null;
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
                victim.Heal(5);
                victim.ClientRPCPlayer(null, victim, "StartLoading");
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

        private void CheckFirstDamage(BradleyAPC apc, RoadBradleyController controller)
        {
            if (controller.ownerId == 0 || controller.firstDamage) return;
            bradleyTimers[apc] = config.purchases[controller.profile].killTime;
			controller.firstDamage = true;
        }

        private bool AreFriends(ulong owner, ulong fighter)
        {
            return Friends.Call<bool>("HasFriend", owner, fighter);
        }

        private void TankRouteCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "roadbradley.admin"))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }
            if (args.Length == 1 && args[0].ToLower() == "default")
            {
                List<string> routes = new List<string>();
                foreach (var road in roads)
                    if (road.Value.isDefault)
                        routes.Add(road.Key);
                string routeList = string.Join(", ", routes);
                if (!routes.Any())
                    routeList = "NO ROUTES";
                SendReply(player, Lang("RouteList", player.UserIDString, routeList));
                return;
            }
            else if (args.Length == 1 && args[0].ToLower() == "edited")
            {
                List<string> routes = new List<string>();
                foreach (var road in roads)
                    if (road.Key.Contains("_Edited"))
                        routes.Add(road.Key);
                string routeList = string.Join(", ", routes);
                if (!routes.Any())
                    routeList = "NO ROUTES";
                SendReply(player, Lang("RouteList", player.UserIDString, routeList));
                return;
            }
            else if (args.Length == 1 && args[0].ToLower() == "custom")
            {
                List<string> routes = new List<string>();
                foreach (var road in roadData.roads.Keys)
                    routes.Add(road);
                string routeList = string.Join(", ", routes);
                if (!routes.Any())
                    routeList = "NO ROUTES";
                SendReply(player, Lang("RouteList", player.UserIDString, routeList));
                return;
            }
            else if (args.Length == 1 && args[0].ToLower() == "new")
            {
                StartEditing(player);
                return;
            }
            else if (args.Length == 2 && args[0].ToLower() == "name")
            {
                RoadEditorController controller = player.GetComponent<RoadEditorController>();
                if (controller == null) return;
                controller.roadFileName = args[1];
                SendReply(player, Lang("RoadNameSet", player.UserIDString, args[1]));
                return;
            }
            else if (args.Length == 0 || !roads.ContainsKey(args[0]))
            {
                SendReply(player, Lang("RouteHelpV3", player.UserIDString));
                return;
            }
            StartEditing(player, args[0]);
        }

        private void SpawnTankCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin && !permission.UserHasPermission(user.Id, "roadbradley.spawncommand"))
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
                TryPurchaseTank(spawnUser, profile, true);
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
                SpawnTank(profile, routeOrPlayer);
                user.Reply(Lang("TankSpawned", user.Id));
                Puts($"Spawned tank with profile {profile} on route {routeOrPlayer}.");
            }
        }

        private void StartEditing(BasePlayer player, string path = "")
        {
            if (!routeEditors.Contains(player.userID))
                routeEditors.Add(player.userID);
            Subscribe(nameof(OnWeaponFired));
            Subscribe(nameof(OnPlayerAttack));
            Subscribe(nameof(OnLoseCondition));
            RoadEditorController controller = player.GetComponent<RoadEditorController>();
            if (controller == null)
            {
                player.gameObject.AddComponent<RoadEditorController>();
                controller = player.GetComponent<RoadEditorController>();
            }
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
                Item silencer = ItemManager.CreateByName("weapon.mod.silencer", 1, 0);
                silencer.MoveToContainer(weapon.contents);
            }
            if (path != "")
            {
                controller.cachedCheckpoints = new List<Vector3>(roads[path].checkpoints);
                controller.roadInfo = roads[path];
                controller.roadName = path;
                if (roadData.roads.ContainsKey(path))
                    controller.roadFileName = path;
                if (roads[path].isPurchasable || roads[path].isRandomEvent)
                    NextTick(() => SendReply(player, Lang("RouteEditWarning", player.UserIDString)));
                ShowEditUI(player);
                DisplayRoute(player);
                SendReply(player, Lang("RouteLoaded", player.UserIDString, path));
                SendReply(player, Lang("EditorHint", player.UserIDString));
            }
            else
            {
                controller.roadName = "Untitled";
                ShowEditUI(player);
                SendReply(player, Lang("EditorHint", player.UserIDString));
            }
        }

        private void DisplayRoute(BasePlayer player, float time = 15)
        {
            RoadEditorController controller = player.GetComponent<RoadEditorController>();
            if (controller == null) return;
            List<Vector3> currentRoute = controller.cachedCheckpoints;
            if (player.net.connection.authLevel == 0)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
            }
            Vector3 firstCheckPoint = currentRoute[0];
            int counter1 = 0;
            int counter2 = 0;
            foreach (var checkpoint in currentRoute)
            {
                if (firstCheckPoint != checkpoint)
                {
                    player.SendConsoleCommand("ddraw.line", time, Color.blue, firstCheckPoint, checkpoint);
                    firstCheckPoint = checkpoint;
                }
                if (counter2 % config.checkpointDisplay == 0)
                {
                    player.SendConsoleCommand("ddraw.text", time, Color.green, checkpoint, counter1);
                    counter2 = 0;
                }
                counter1++;
                counter2++;
            }
            if (player.net.connection.authLevel == 0)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }

        private void SaveRoute(BasePlayer player)
        {
            RoadEditorController controller = player.GetComponent<RoadEditorController>();
            if (controller == null) return;
            if (controller.roadFileName == "")
            {
                SendReply(player, Lang("NoFileName", player.UserIDString));
                return;
            }
            roadData.roads.TryAdd(controller.roadFileName, new List<Vector3>());
            roadData.roads[controller.roadFileName] = new List<Vector3>(controller.cachedCheckpoints);
            roads.TryAdd(controller.roadFileName, new RoadInfo() { isDefault = false, checkpoints = new List<Vector3>(controller.cachedCheckpoints) });
            roads[controller.roadFileName].checkpoints = new List<Vector3>(controller.cachedCheckpoints);
            SendReply(player, Lang("RoadSaved", player.UserIDString));
            SaveData();
            FinishEditing(player);
        }

        private void FinishEditing(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "RoadBradleyUI_Edit");
            routeEditors.Remove(player.userID);
            GameObject.Destroy(player.GetComponent<RoadEditorController>());
            if (!routeEditors.Any())
            {
                Unsubscribe(nameof(OnWeaponFired));
                Unsubscribe(nameof(OnPlayerAttack));
                Unsubscribe(nameof(OnLoseCondition));
            }
            SendReply(player, Lang("EditorClosed", player.UserIDString));
        }

        private void BuyTankCommand(BasePlayer player) => OpenPurchaseUI(player);

        private void DebugShowRouteCommand(BasePlayer player)
        {
            if (!player.IsAdmin) return;
            BradleyAPC nearest = null;
            float nearestFloat = float.MaxValue;
            foreach (var brad in roadBradleys)
            {
                float dist = Vector3.Distance(player.transform.position, brad.transform.position);
                if (dist < nearestFloat)
                {
                    nearest = brad;
                    nearestFloat = dist;
                }
            }
            if (nearest == null) return;
            SendReply(player, $"Index: {nearest.currentPathIndex}\nBrake: {nearest.brake}\nBrake Force: {nearest.brakeForce}\nLeft Throttle: {nearest.leftThrottle}\nRight Throttle: {nearest.rightThrottle}\nNext Patrol Time: {nearest.nextPatrolTime}\nRPM Multiplier: {nearest.rpmMultiplier}\nThrottle: {nearest.throttle}\nTicks Since Stopped: {nearest.ticksSinceStopped}\nTurn Force: {nearest.turnForce}");
            List<Vector3> currentRoute = new List<Vector3>(nearest.currentPath.Take(nearest.currentPath.Count / 2));
            if (player.net.connection.authLevel == 0)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
            }
            Vector3 firstCheckPoint = currentRoute[0];
            int counter1 = 0;
            int counter2 = 0;
            foreach (var checkpoint in currentRoute)
            {
                if (firstCheckPoint != checkpoint)
                {
                    player.SendConsoleCommand("ddraw.line", 15, Color.blue, firstCheckPoint, checkpoint);
                    firstCheckPoint = checkpoint;
                }
                if (counter2 % config.checkpointDisplay == 0)
                {
                    player.SendConsoleCommand("ddraw.text", 15, Color.green, checkpoint, counter1);
                    counter2 = 0;
                }
                counter1++;
                counter2++;
            }
            if (player.net.connection.authLevel == 0)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }

        [ConsoleCommand("UI_RoadBradley")]
        private void RoadBradleyConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg.Args[0] == "close")
                CuiHelper.DestroyUi(player, "RoadBradleyUI_Main");
            else if (arg.Args[0] == "close_edit")
                FinishEditing(player);
            else if (arg.Args[0] == "show_path")
                DisplayRoute(player);
            else if (arg.Args[0] == "show_hint")
                SendReply(player, Lang("EditorHint", player.UserIDString));
            else if (arg.Args[0] == "save_path")
                SaveRoute(player);
            else if (arg.Args[0] == "profile")
                OpenPurchaseUI(player, arg.Args[1]);
            else if (arg.Args[0] == "spawn")
                TryPurchaseTank(player, arg.Args[1]);
        }

        private void TankCountCommand(IPlayer user)
        {
            if (!user.IsAdmin) return;
            user.Reply($"Road Bradleys Active: {roadBradleys.Count}");
        }

        private void UpdateTimer(BasePlayer player, BradleyAPC apc)
        {
            if (apc == null)
            {
                CuiHelper.DestroyUi(player, "RoadBradleyUI_Timer");
                return;
            }
            RoadBradleyController controller = apc.GetComponent<RoadBradleyController>();
            if (controller == null)
            {
                CuiHelper.DestroyUi(player, "RoadBradleyUI_Timer");
                return;
            }
            float healthPercentage = apc.health / config.profiles[controller.profile].health * 20;
            CuiElementContainer container = new CuiElementContainer();
            UI_AddCorePanelNoCursor(container, "RoadBradleyUI_Timer", "Hud", "0 0 0 0", "0.5 0", "0.5 0", "0 0", "0 0");
            UI_AddPanel(container, "RoadBradleyUI_Timer", "0.85 0.85 0.85 0.035", "0.5 0", "0.5 0", "-71.5 80", "52.5 100");
            UI_AddPanel(container, "RoadBradleyUI_Timer", "0.091 0.362 0.548 0.9", "0.5 0", "0.5 0", "-71.5 80", $"52.5 {80 + healthPercentage}");
            UI_AddText(container, "RoadBradleyUI_Timer", "0.729 0.694 0.659 1", $"{CalculateTime(bradleyTimers[apc])}", TextAnchor.MiddleCenter, 16, "0.5 0", "0.5 0", "-71.5 80", "52.5 100");
            CuiHelper.DestroyUi(player, "RoadBradleyUI_Timer");
            CuiHelper.AddUi(player, container);
        }

        private void ShowEditUI(BasePlayer player)
        {
            RoadEditorController controller = player.GetComponent<RoadEditorController>();
            if (controller == null) return;
            CuiElementContainer container = new CuiElementContainer();
            UI_AddCorePanelNoCursor(container, "RoadBradleyUI_Edit", "Hud", "0 0 0 0", "1 0.5", "1 0.5", "0 0", "0 0");
            UI_AddPanel(container, "RoadBradleyUI_Edit", "0.144 0.128 0.107 1", "0 0", "0 0", "-175 -86", "-25 100"); //Background Panel
            UI_AddPanel(container, "RoadBradleyUI_Edit", "0.227 0.218 0.206 1", "0 0", "0 0", "-172 77", "-28 97"); //Title Background
            UI_AddTextNoOutline(container, "RoadBradleyUI_Edit", "0.729 0.694 0.659 1", Lang("BradleyEditorTitle", player.UserIDString), TextAnchor.MiddleLeft, 12, "0 0", "0 0", "-168 77", "-28 97");
            UI_AddButton(container, "RoadBradleyUI_Edit", "0.941 0.486 0.302 1", "✖", TextAnchor.MiddleCenter, 15, "0.45 0.237 0.194 1", "UI_RoadBradley close_edit", "0 0", "0 0", "-48 77", "-28 97");
            UI_AddPanel(container, "RoadBradleyUI_Edit", "0.187 0.179 0.172 1", "0 0", "0 0", "-172 34", "-28 74"); //Route Name Background
            UI_AddTextNoOutline(container, "RoadBradleyUI_Edit", "0.733 0.851 0.533 1", controller.roadName, TextAnchor.MiddleCenter, 12, "0 0", "0 0", "-172 51", "-28 74");
            UI_AddPanel(container, "RoadBradleyUI_Edit", "0.227 0.218 0.206 1", "0 0", "0 0", "-170 36", "-30 51"); //Route Title Background
            UI_AddTextNoOutline(container, "RoadBradleyUI_Edit", "0.729 0.694 0.659 1", Lang("CurrentRouteTitle", player.UserIDString), TextAnchor.MiddleCenter, 9, "0 0", "0 0", "-170 36", "-30 51");
            UI_AddButton(container, "RoadBradleyUI_Edit", "0.733 0.851 0.533 1", Lang("ShowPathButton", player.UserIDString), TextAnchor.MiddleCenter, 12, "0.439 0.538 0.261 1", $"UI_RoadBradley show_path", "0 0", "0 0", "-172 11", "-28 31");
            UI_AddButton(container, "RoadBradleyUI_Edit", "0.733 0.851 0.533 1", Lang("ShowHintButton", player.UserIDString), TextAnchor.MiddleCenter, 12, "0.439 0.538 0.261 1", $"UI_RoadBradley show_hint", "0 0", "0 0", "-172 -12", "-28 8");
            UI_AddButton(container, "RoadBradleyUI_Edit", "0.733 0.851 0.533 1", Lang("SaveExitButton", player.UserIDString), TextAnchor.MiddleCenter, 12, "0.439 0.538 0.261 1", $"UI_RoadBradley save_path", "0 0", "0 0", "-172 -35", "-28 -15");
            string color = "0.941 0.486 0.302 1";
            if (controller.roadInfo != null && controller.roadInfo.isDefault)
                color = "0.733 0.851 0.533 1";
            UI_AddTextNoOutline(container, "RoadBradleyUI_Edit", color, Lang("IsDefault", player.UserIDString), TextAnchor.MiddleLeft, 12, "0 0", "0 0", "-170 -52", "-30 -35");
            color = "0.941 0.486 0.302 1";
            if (controller.roadInfo != null && controller.roadInfo.isPurchasable)
                color = "0.733 0.851 0.533 1";
            UI_AddTextNoOutline(container, "RoadBradleyUI_Edit", color, Lang("IsPurchasable", player.UserIDString), TextAnchor.MiddleRight, 12, "0 0", "0 0", "-170 -52", "-30 -35");
            color = "0.941 0.486 0.302 1";
            if (controller.roadInfo != null && controller.roadInfo.isRandomEvent)
                color = "0.733 0.851 0.533 1";
            UI_AddTextNoOutline(container, "RoadBradleyUI_Edit", color, Lang("IsRandomEvent", player.UserIDString), TextAnchor.MiddleLeft, 12, "0 0", "0 0", "-170 -69", "-30 -52");
            color = "0.941 0.486 0.302 1";
            if (controller.roadInfo != null && controller.roadInfo.isNowOccupied)
                color = "0.733 0.851 0.533 1";
            UI_AddTextNoOutline(container, "RoadBradleyUI_Edit", color, Lang("IsOccupied", player.UserIDString), TextAnchor.MiddleRight, 12, "0 0", "0 0", "-170 -69", "-30 -52");
            UI_AddTextNoOutline(container, "RoadBradleyUI_Edit", "0.729 0.694 0.659 1", Lang("CheckpointCount", player.UserIDString, controller.cachedCheckpoints.Count), TextAnchor.MiddleCenter, 12, "0 0", "0 0", "-170 -86", "-30 -69");
            CuiHelper.DestroyUi(player, "RoadBradleyUI_Edit");
            CuiHelper.AddUi(player, container);
        }

        private void OpenPurchaseUI(BasePlayer player, string profile = "")
        {
            if (!config.profiles.Any())
            {
                PrintToChat(player, Lang("NoPurchases", player.UserIDString));
                return;
            }
            CuiElementContainer container = new CuiElementContainer();
            UI_AddCorePanel(container, "RoadBradleyUI_Main", "Hud.Menu", "0.3 0.3 0.3 0.2", "0 0", "1 1", "0 0", "0 0");
            UI_AddPanel(container, "RoadBradleyUI_Main", "0.4 0.4 0.4 0.3", "0 0", "1 1", "0 0", "0 0");
            UI_AddBackgroundPanel(container, "RoadBradleyUI_Main", "0 0 0 0.8", "0 0", "1 1", "0 0", "0 0");
            UI_AddButton(container, "RoadBradleyUI_Main", "0 0 0 0", "", TextAnchor.UpperCenter, 0, "0 0 0 0", "UI_RoadBradley close", "0 0", "1 1", "0 0", "0 0"); //Close Button
            UI_AddPanel(container, "RoadBradleyUI_Main", "0.144 0.128 0.107 1", "0.5 0.5", "0.5 0.5", "-275 -175", "275 175"); //Background Panel
            UI_AddPanel(container, "RoadBradleyUI_Main", "0.227 0.218 0.206 1", "0.5 0.5", "0.5 0.5", "-272 152", "272 172"); //Title Background
            UI_AddImage(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", "0.5 0.5", "0.5 0.5", "-270 154", "-254 170", "assets/icons/target.png");
            UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("BradleyFights", player.UserIDString), TextAnchor.MiddleLeft, 12, "0.5 0.5", "0.5 0.5", "-250 154", "272 170");
            UI_AddButton(container, "RoadBradleyUI_Main", "0.941 0.486 0.302 1", "✖", TextAnchor.MiddleCenter, 15, "0.45 0.237 0.194 1", "UI_RoadBradley close", "0.5 0.5", "0.5 0.5", "252 152", "272 172");

            UI_AddPanel(container, "RoadBradleyUI_Main", "0.227 0.218 0.206 1", "0.5 0.5", "0.5 0.5", "-272 129", "-100 149"); //Tier List Title Background
            UI_AddImage(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", "0.5 0.5", "0.5 0.5", "-270 131", "-254 147", "assets/icons/level.png");
            UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("Levels", player.UserIDString), TextAnchor.MiddleLeft, 12, "0.5 0.5", "0.5 0.5", "-250 131", "-100 147");
            UI_AddPanel(container, "RoadBradleyUI_Main", "0.187 0.179 0.172 1", "0.5 0.5", "0.5 0.5", "-272 -172", "-100 129"); //Tier List Background
            int start = 127;
            foreach (var tank in config.purchases.Keys)
            {
                if (config.purchases[tank].permission != "" && !permission.UserHasPermission(player.UserIDString, config.purchases[tank].permission)) continue;
                if (profile == "")
                    profile = tank;
                if (profile == tank)
                    UI_AddButton(container, "RoadBradleyUI_Main", "0.733 0.851 0.533 1", "   " + Lang($"Tank_{tank}", player.UserIDString), TextAnchor.MiddleLeft, 10, "0.439 0.538 0.261 1", $"UI_RoadBradley profile {tank}", "0.5 0.5", "0.5 0.5", $"-272 {start - 20}", $"-100 {start}");
                else
                    UI_AddButton(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", "   " + Lang($"Tank_{tank}", player.UserIDString), TextAnchor.MiddleLeft, 10, "0.169 0.153 0.136 1", $"UI_RoadBradley profile {tank}", "0.5 0.5", "0.5 0.5", $"-272 {start - 20}", $"-100 {start}");
                start -= 22;
            }

            UI_AddPanel(container, "RoadBradleyUI_Main", "0.227 0.218 0.206 1", "0.5 0.5", "0.5 0.5", "-97 129", "272 149"); //Tank Info Title Background
            UI_AddImage(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", "0.5 0.5", "0.5 0.5", "-95 131", "-79 147", "assets/icons/examine.png");
            UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("BradleyInfo", player.UserIDString), TextAnchor.MiddleLeft, 12, "0.5 0.5", "0.5 0.5", "-75 131", "272 147");

            if (profile != "")
            {
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.252 0.243 0.227 1", "0.5 0.5", "0.5 0.5", "-97 106", "272 126"); //Tank Title Text Background
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.733 0.851 0.533 1", Lang($"Tank_{profile}", player.UserIDString), TextAnchor.MiddleLeft, 10, "0.5 0.5", "0.5 0.5", "-87 106", "180 126");


                UI_AddPanel(container, "RoadBradleyUI_Main", "0.252 0.243 0.227 1", "0.5 0.5", "0.5 0.5", "-97 83", "272 103"); //Info Tab #1 Background
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.187 0.179 0.172 1", "0.5 0.5", "0.5 0.5", "150 83", "272 103"); //Info Tab #1 Text Background
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("Health", player.UserIDString), TextAnchor.MiddleLeft, 10, "0.5 0.5", "0.5 0.5", "-87 83", "150 103");
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", $"{config.profiles[profile].health}", TextAnchor.MiddleCenter, 10, "0.5 0.5", "0.5 0.5", "150 83", "272 103");

                UI_AddPanel(container, "RoadBradleyUI_Main", "0.252 0.243 0.227 1", "0.5 0.5", "0.5 0.5", "-97 60", "272 80"); //Info Tab #2 Background
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.187 0.179 0.172 1", "0.5 0.5", "0.5 0.5", "150 60", "272 80"); //Info Tab #2 Text Background
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("BulletDamage", player.UserIDString), TextAnchor.MiddleLeft, 10, "0.5 0.5", "0.5 0.5", "-87 60", "150 80");
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", $"{config.profiles[profile].coaxBulletDamage}", TextAnchor.MiddleCenter, 10, "0.5 0.5", "0.5 0.5", "150 60", "272 80");

                UI_AddPanel(container, "RoadBradleyUI_Main", "0.252 0.243 0.227 1", "0.5 0.5", "0.5 0.5", "-97 37", "272 57"); //Info Tab #3 Background
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.187 0.179 0.172 1", "0.5 0.5", "0.5 0.5", "150 37", "272 57"); //Info Tab #3 Text Background
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("BurstLength", player.UserIDString), TextAnchor.MiddleLeft, 10, "0.5 0.5", "0.5 0.5", "-87 37", "150 57");
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", $"{config.profiles[profile].coaxBurstLength}", TextAnchor.MiddleCenter, 10, "0.5 0.5", "0.5 0.5", "150 37", "272 57");

                UI_AddPanel(container, "RoadBradleyUI_Main", "0.252 0.243 0.227 1", "0.5 0.5", "0.5 0.5", "-97 14", "272 34"); //Info Tab #4 Background
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.187 0.179 0.172 1", "0.5 0.5", "0.5 0.5", "150 14", "272 34"); //Info Tab #4 Text Background
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("TimeToKill", player.UserIDString), TextAnchor.MiddleLeft, 10, "0.5 0.5", "0.5 0.5", "-87 14", "150 34");
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", $"{CalculateTime(config.purchases[profile].killTime)}", TextAnchor.MiddleCenter, 10, "0.5 0.5", "0.5 0.5", "150 14", "272 34");

                string failedReason = "";
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.252 0.243 0.227 1", "0.5 0.5", "0.5 0.5", "-97 -9", "272 11"); //Info Tab #5 Background
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.187 0.179 0.172 1", "0.5 0.5", "0.5 0.5", "150 -9", "272 11"); //Info Tab #5 Text Background
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("Cooldown", player.UserIDString), TextAnchor.MiddleLeft, 10, "0.5 0.5", "0.5 0.5", "-87 -9", "150 11");
                if (config.purchases[profile].cooldown > 0)
                    UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", $"{CalculateTime(config.purchases[profile].cooldown)}", TextAnchor.MiddleCenter, 10, "0.5 0.5", "0.5 0.5", "150 -9", "272 11");
                else
                    UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("NoCooldown", player.UserIDString), TextAnchor.MiddleCenter, 10, "0.5 0.5", "0.5 0.5", "150 -9", "272 11");

                int playerLimit = 0;
                string date = DateTime.Now.ToShortDateString();
                string color = "0.729 0.694 0.659 1";
                if (config.purchases[profile].maxDaily != 0 && limitData.limits.ContainsKey(date) && limitData.limits[date].ContainsKey(player.userID) && limitData.limits[date][player.userID].ContainsKey(profile))
                {
                    playerLimit = limitData.limits[date][player.userID][profile];
                    if (playerLimit >= config.purchases[profile].maxDaily)
                    {
                        color = "0.941 0.486 0.302 1";
                        failedReason = "limit";
                    }
                }
                string limitFormat = $"{playerLimit}/{config.purchases[profile].maxDaily}";
                if (config.purchases[profile].maxDaily == 0)
                    limitFormat = Lang("Unlimited", player.UserIDString);
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.252 0.243 0.227 1", "0.5 0.5", "0.5 0.5", "-97 -32", "272 -12"); //Info Tab #6 Background
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.187 0.179 0.172 1", "0.5 0.5", "0.5 0.5", "150 -32", "272 -12"); //Info Tab #6 Text Background
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("DailyLimit", player.UserIDString), TextAnchor.MiddleLeft, 10, "0.5 0.5", "0.5 0.5", "-87 -32", "150 -12");
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", color, limitFormat, TextAnchor.MiddleCenter, 10, "0.5 0.5", "0.5 0.5", "150 -32", "272 -12");

                UI_AddPanel(container, "RoadBradleyUI_Main", "0.227 0.218 0.206 1", "0.5 0.5", "0.5 0.5", "-97 -55", "272 -35"); //Requirements Title Background
                UI_AddImage(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", "0.5 0.5", "0.5 0.5", "-95 -53", "-79 -37", "assets/icons/open.png");
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", Lang("Requirements", player.UserIDString), TextAnchor.MiddleLeft, 12, "0.5 0.5", "0.5 0.5", "-75 -53", "272 -37");
                int column = -97;
                int line = -57;
                foreach (var item in config.purchases[profile].requiredItems)
                {
                    UI_AddPanel(container, "RoadBradleyUI_Main", "0.278 0.27 0.256 1", "0.5 0.5", "0.5 0.5", $"{column} {line - 20}", $"{column + 183} {line}");
                    string name = item.displayName == "" ? ItemManager.FindItemDefinition(item.shortname).displayName.english : item.displayName;
                    string finalFormat = $"{name} x{item.amount}";
                    if (item.shortname.ToLower() == "currency")
                        finalFormat = string.Format(name, item.amount);
                    if (PlayerHasItem(player, item.shortname, item.skin, item.amount))
                        UI_AddPanel(container, "RoadBradleyUI_Main", "0.319 0.372 0.219 1", "0.5 0.5", "0.5 0.5", $"{column} {line - 20}", $"{column + 3} {line}");
                    else
                    {
                        UI_AddPanel(container, "RoadBradleyUI_Main", "0.45 0.237 0.194 1", "0.5 0.5", "0.5 0.5", $"{column} {line - 20}", $"{column + 3} {line}");
                        if (failedReason == "")
                            failedReason = "item";
                    }
                    UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", finalFormat, TextAnchor.MiddleLeft, 10, "0.5 0.5", "0.5 0.5", $"{column + 10} {line - 20}", $"{column + 183} {line}");
                    if (column == -97)
                        column += 186;
                    else
                    {
                        line -= 22;
                        column = -97;
                    }
                    if (line < -135) break;
                }
                int cooldown = 0;
                if (purchaseCooldowns.ContainsKey(player.userID) && purchaseCooldowns[player.userID].ContainsKey(profile))
                {
                    cooldown = purchaseCooldowns[player.userID][profile];
                    failedReason = "cooldown";
                }
                if (currentBradleyOwners.ContainsValue(player.userID))
                    failedReason = "spawned";
                UI_AddPanel(container, "RoadBradleyUI_Main", "0.187 0.179 0.172 1", "0.5 0.5", "0.5 0.5", "-97 -173", "272 -145"); //Requirements Title Background
                string readyText = Lang("YouAreReady", player.UserIDString);
                if (failedReason == "limit")
                    readyText = Lang("DailyLimitReached", player.UserIDString);
                else if (failedReason == "item")
                    readyText = Lang("NoAllItems", player.UserIDString);
                else if (failedReason == "cooldown")
                    readyText = Lang("InCooldown", player.UserIDString, CalculateTime(cooldown));
                else if (failedReason == "spawned")
                    readyText = Lang("AlreadySpawned", player.UserIDString);
                UI_AddTextNoOutline(container, "RoadBradleyUI_Main", "0.729 0.694 0.659 1", readyText, TextAnchor.MiddleLeft, 10, "0.5 0.5", "0.5 0.5", "-87 -173", "150 -145");
                if (failedReason == "" || permission.UserHasPermission(player.UserIDString, "roadbradley.bypass"))
                    UI_AddButton(container, "RoadBradleyUI_Main", "0.733 0.851 0.533 1", Lang("Spawn", player.UserIDString), TextAnchor.MiddleCenter, 11, "0.439 0.538 0.261 1", $"UI_RoadBradley spawn {profile}", "0.5 0.5", "0.5 0.5", "150 -173", "272 -145");
                else
                    UI_AddButton(container, "RoadBradleyUI_Main", "0.941 0.486 0.302 1", Lang("Locked", player.UserIDString), TextAnchor.MiddleCenter, 11, "0.45 0.237 0.194 1", "", "0.5 0.5", "0.5 0.5", "150 -173", "272 -145");
            }
            CuiHelper.DestroyUi(player, "RoadBradleyUI_Main");
            CuiHelper.AddUi(player, container);
        }

        private bool PlayerHasItem(BasePlayer player, string shortname, ulong skin, int amount)
        {
            if (shortname.ToLower() == "currency")
            {
                switch (config.moneyPlugin)
                {
                    case 1:
                        if (Economics != null && Economics.Call<double>("Balance", player.userID) >= amount)
                            return true;
                        else
                            return false;
                    case 2:
                        if (ServerRewards != null && ServerRewards.Call<int>("CheckPoints", player.userID) >= amount)
                            return true;
                        else
                            return false;
                    case 3:
                        if (ShoppyStock != null && ShoppyStock.Call<int>("GetCurrencyAmount", config.currency, player.userID) >= amount)
                            return true;
                        else
                            return false;
                    default:
                        return false;
                }
            }
            else
            {
                int currentAmount = 0;
                foreach (var item in Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())))
                {
                    if (item.info.shortname == shortname && item.skin == skin)
                    {
                        currentAmount += item.amount;
                        if (currentAmount >= amount)
                            return true;
                    }
                }
                return false;
            }
        }

        private bool TakeResources(BasePlayer player, List<SubItemConfig> items)
        {
            foreach (var requiredItem in items)
            {
                if (requiredItem.shortname.ToLower() == "currency")
                {
                    switch (config.moneyPlugin)
                    {
                        case 1:
                            if (Economics != null && Economics.Call<double>("Balance", player.userID) >= requiredItem.amount)
                                continue;
                            else
                                return false;
                        case 2:
                            if (ServerRewards != null && ServerRewards.Call<int>("CheckPoints", player.userID) >= requiredItem.amount)
                                continue;
                            else
                                return false;
                        case 3:
                            if (ShoppyStock != null && ShoppyStock.Call<int>("GetCurrencyAmount", config.currency, player.userID) >= requiredItem.amount)
                                continue;
                            else
                                return false;
                        default:
                            return false;
                    }
                }
                bool haveRequired = false;
                int inventoryAmount = 0;
                foreach (var item in Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())))
                {
                    if (item.skin == requiredItem.skin && item.info.shortname == requiredItem.shortname)
                    {
                        inventoryAmount += item.amount;
                        if (inventoryAmount >= requiredItem.amount)
                        {
                            haveRequired = true;
                            break;
                        }
                    }
                }
                if (!haveRequired)
                    return false;
            }
            foreach (var requiredItem in items)
            {
                if (requiredItem.shortname.ToLower() == "currency")
                {
                    switch (config.moneyPlugin)
                    {
                        case 1:
                            if (Economics != null && Economics.Call<bool>("Withdraw", player.userID, (double)requiredItem.amount))
                                continue;
                            else
                                return false;
                        case 2:
                            if (ServerRewards != null && ServerRewards.Call<bool>("TakePoints", player.userID, requiredItem.amount))
                                continue;
                            else
                                return false;
                        case 3:
                            if (ShoppyStock != null && ShoppyStock.Call<bool>("TakeCurrency", config.currency, player.userID, requiredItem.amount))
                                continue;
                            else
                                return false;
                        default:
                            return false;
                    }
                }
                int takenItems = 0;
                foreach (var item in Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())))
                {
                    if (item.skin == requiredItem.skin && item.info.shortname == requiredItem.shortname)
                    {
                        if (takenItems < requiredItem.amount)
                        {
                            if (item.amount > requiredItem.amount - takenItems)
                            {
                                item.amount -= requiredItem.amount - takenItems;
                                item.MarkDirty();
                                break;
                            }
                            if (item.amount <= requiredItem.amount - takenItems)
                            {
                                takenItems += item.amount;
                                item.GetHeldEntity()?.Kill();
                                item.Remove();
                            }
                        }
                        else break;
                    }
                }
            }
            return true;
        }

        private static void UI_AddCorePanel(CuiElementContainer container, string name, string parentName, string color, string anchorMin, string anchorMax, string offsetMin, string offsetMax)
        {
            container.Add(new CuiElement
            {
                Name = name,
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = color,
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax,
                    },
                    new CuiNeedsCursorComponent()
                }
            });
        }

        private static void UI_AddCorePanelNoCursor(CuiElementContainer container, string name, string parentName, string color, string anchorMin, string anchorMax, string offsetMin, string offsetMax)
        {
            container.Add(new CuiElement
            {
                Name = name,
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = color,
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax,
                    }
                }
            });
        }

        private static void UI_AddBackgroundPanel(CuiElementContainer container, string parentName, string color, string anchorMin, string anchorMax, string offsetMin, string offsetMax)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = color,
                        Material = "assets/content/ui/namefontmaterial.mat",
                        Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax,
                    }
                }
            });
        }

        private static void UI_AddPanel(CuiElementContainer container, string parentName, string color, string anchorMin, string anchorMax, string offsetMin, string offsetMax)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = color,
                        Material = "assets/content/ui/namefontmaterial.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax,
                    }
                }
            });
        }

        private static void UI_AddTextNoOutline(CuiElementContainer container, string parentName, string color, string text, TextAnchor textAnchor, int fontSize, string anchorMin, string anchorMax, string offsetMin, string offsetMax)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = color,
                        Text = text,
                        Align = textAnchor,
                        FontSize = fontSize
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax,
                    }
                }
            });
        }

        private static void UI_AddText(CuiElementContainer container, string parentName, string color, string text, TextAnchor textAnchor, int fontSize, string anchorMin, string anchorMax, string offsetMin, string offsetMax)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = color,
                        Text = text,
                        Align = textAnchor,
                        FontSize = fontSize
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax,
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0.091 0.362 0.548 0.9",
                        Distance = "0.5 0.5"
                    }
                }
            });
        }

        private static void UI_AddButton(CuiElementContainer container, string parentName, string textColor, string text, TextAnchor textAnchor, int fontSize, string buttonColor, string command, string anchorMin, string anchorMax, string offsetMin, string offsetMax)
        {
            container.Add(new CuiButton
            {
                Text =
                {
                    Color = textColor,
                    Text = text,
                    Align = textAnchor,
                    FontSize = fontSize
                },
                Button =
                {
                    Color = buttonColor,
                    Material = "assets/content/ui/namefontmaterial.mat",
                    Command = command,
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax,
                }
            }, parentName);
        }

        private static void UI_AddImage(CuiElementContainer container, string parentName, string color, string anchorMin, string anchorMax, string offsetMin, string offsetMax, string path)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = color,
                        Sprite = path
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax,
                    }
                }
            });
        }

        private class BradleyDisarmZone : FacepunchBehaviour
        {
            private Rigidbody rigidbody;
            private SphereCollider sphereCollider;
            private BradleyAPC apc;

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

            public void SetupZone(BradleyAPC bradley)
            {
                apc = bradley;
                transform.position = apc.transform.position;
                transform.SetParent(apc.transform, true);
                sphereCollider.transform.position = apc.transform.position;
                sphereCollider.transform.SetParent(apc.transform, true);
                sphereCollider.radius = _plugin.config.profiles[apc.GetComponent<RoadBradleyController>().profile].disarmRadius;
            }

            private void OnTriggerEnter(Collider other)
            {
                if (!other || !other.gameObject) return;
                RFTimedExplosive explosive = other.GetComponent<RFTimedExplosive>();
                if (explosive == null || explosive.GetFrequency() == -1) return;
                if (_plugin.config.profiles[apc.GetComponent<RoadBradleyController>().profile].disarmType)
                    Invoke(new Action(explosive.Explode), UnityEngine.Random.Range(0f, 0.2f));
                else
                    explosive.ChangeFrequency(1470);
            }
        }

        internal static class BradleyLimitMovement
        {
            internal static bool Prefix(BradleyAPC __instance)
            {
                if (__instance.HasFlag(BaseEntity.Flags.Reserved6))
                    return false;
                else
                    return true;
            }
        }

        private class RoadBradleyController : FacepunchBehaviour
        {
            public Dictionary<ulong, float> damageDealt = new Dictionary<ulong, float>();
            public string profile;
            public string routeName;
            public RoadInfo roadInfo;
            public List<ulong> targets = new List<ulong>();
            public ulong ownerId = 0;
            public int damageAuths = 0;
			public bool firstDamage = false;

            private BradleyAPC apc;
            private readonly List<float> ranMLRS = new List<float>();
            private int lastCheckpoint = 0;
            private int noFighterFoundCount = 0;
            private bool brakesOn = false;
            private int stuckCount = 0;
            private int overallStuckCount = 0;

            private void Awake()
            {
                apc = GetComponent<BradleyAPC>();
                InvokeRepeating(CheckTargets, 1, 1);
                InvokeRepeating(CheckDebris, 3, 3);
                CancelInvoke(apc.FixedUpdate);
                apc.SetFlag(BaseEntity.Flags.Reserved6, true);
            }

            private void CheckTargets()
            {
                if (targets.Any())
                {
                    BasePlayer target = apc.lastAttacker?.ToPlayer();
                    if (target == null || !targets.Contains(target.userID))
                        target = BasePlayer.FindByID(targets[0]);
                    if (target == null)
                    {
                        Brakes(false);
                        return;
                    }
                    if (apc.IsVisibleAndCanSee(target.eyes.position) && Vector3.Distance(apc.transform.position, target.eyes.position) < apc.viewDistance)
                    {
                        Brakes(true);
                        return;
                    }
                    Brakes(false);
                    return;
                }
                Brakes(false);
            }

            private void FixedUpdate()
            {
                if (apc.isClient)
                {
                    return;
                }
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
                Vector3.Distance(apc.transform.position, apc.GetFinalDestination());
                float num = Vector3.Distance(apc.transform.position, apc.GetCurrentPathDestination());
                float num2 = 15f;
                if (num < 20f)
                {
                    float value = Vector3.Dot(apc.PathDirection(apc.currentPathIndex), apc.PathDirection(apc.currentPathIndex + 1));
                    float num3 = Mathf.InverseLerp(2f, 10f, num);
                    float num4 = Mathf.InverseLerp(0.5f, 0.8f, value);
                    num2 = 15f - 14f * ((1f - num4) * (1f - num3));
                }
                if (apc.patrolPath != null)
                {
                    float num5 = num2;
                    foreach (IAIPathSpeedZone pathSpeedZone in apc.patrolPath.SpeedZones)
                    {
                        if (pathSpeedZone.WorldSpaceBounds().Contains(base.transform.position))
                        {
                            num5 = Mathf.Min(num5, pathSpeedZone.GetMaxSpeed());
                        }
                    }
                    apc.currentSpeedZoneLimit = Mathf.Lerp(apc.currentSpeedZoneLimit, num5, UnityEngine.Time.deltaTime);
                    num2 = Mathf.Min(num2, apc.currentSpeedZoneLimit);
                }
                if (apc.PathComplete())
                {
                    num2 = 0f;
                }
                if (ConVar.Global.developer > 1)
                {
                    Debug.Log(string.Concat(new object[]
                    {
                        "velocity:",
                        velocity.magnitude,
                        "max : ",
                        num2
                    }));
                }
                float num6 = apc.throttle;
                apc.leftThrottle = Mathf.Clamp(apc.leftThrottle + num6, -1f, 1f);
                apc.rightThrottle = Mathf.Clamp(apc.rightThrottle + num6, -1f, 1f);
                float t = Mathf.InverseLerp(2f, 1f, velocity.magnitude * Mathf.Abs(Vector3.Dot(velocity.normalized, base.transform.forward)));
                float torqueAmount = Mathf.Lerp(apc.moveForceMax, apc.turnForce, t);
                float num7 = Mathf.InverseLerp(5f, 1.5f, velocity.magnitude * Mathf.Abs(Vector3.Dot(velocity.normalized, base.transform.forward)));
                apc.ScaleSidewaysFriction(1f - num7);
                apc.SetMotorTorque(apc.leftThrottle, false, torqueAmount);
                apc.SetMotorTorque(apc.rightThrottle, true, torqueAmount);
                apc.impactDamager.damageEnabled = (apc.myRigidBody.velocity.magnitude > 2f);
            }

            private void CheckDebris()
            {
                if (!brakesOn && apc.currentPathIndex == lastCheckpoint)
                {
                    if (apc.currentPath.Count == 0)
                    {
                        if (roadInfo != null)
                            apc.currentPath = new List<Vector3>(roadInfo.checkpoints);
                        apc.currentPathIndex = 0;
                        apc.SendNetworkUpdate();
                    }
                    int count = 0;
                    List<BaseEntity> roadEntities = new List<BaseEntity>();
                    Vis.Entities(apc.transform.position, 4, roadEntities);
                    foreach (var entity in roadEntities)
                    {
                        if (entity == null || entity.IsDestroyed) continue;
                        if (entity.net.ID == apc.net.ID) continue;
                        if (_plugin.config.killedEntities.Contains(entity.ShortPrefabName))
                        {
                            Effect.server.Run("assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab", entity.transform.position);
                            entity.Kill();
                            count++;
                        }
                    }
                    if (count == 0)
                        stuckCount++;
                    else
                    {
                        overallStuckCount = 0;
                        stuckCount = 0;
                    }
                    if (stuckCount == 10)
                    {
                        Brakes(true);
                        Invoke(() => Brakes(false), 1);
                    }
                    if (stuckCount == 20)
                    {
                        if (_plugin.config.stuckErrorKill > 0)
                        {
                            overallStuckCount++;
                            if (overallStuckCount >= _plugin.config.stuckErrorKill)
                            {
                                if (_plugin.config.killOnStuck)
                                {
                                    if (_plugin.config.broadcastStuck)
                                    {
                                        foreach (var playerId in damageDealt.Keys)
                                        {
                                            BasePlayer player = BasePlayer.FindByID(playerId);
                                            if (player == null) continue;
                                            _plugin.SendReply(player, _plugin.Lang("TankStuckKill", player.UserIDString));
                                        }
                                    }
                                    _plugin.PrintWarning($"Tank has been stuck for over {_plugin.config.stuckErrorKill} times and has been killed on '{apc.transform.position}'");
                                    Effect.server.Run("assets/prefabs/weapons/beancan grenade/effects/beancan_grenade_explosion.prefab", apc.transform.position);
                                    apc.Kill();
                                }
                                else
                                {
                                    if (_plugin.config.broadcastStuck)
                                    {
                                        foreach (var playerId in damageDealt.Keys)
                                        {
                                            BasePlayer player = BasePlayer.FindByID(playerId);
                                            if (player == null) continue;
                                            _plugin.SendReply(player, _plugin.Lang("TankStuckTeleport", player.UserIDString));
                                        }
                                    }
                                    apc.transform.position = roadInfo.checkpoints[0];
                                    apc.currentPathIndex = 0;
                                    apc.SendNetworkUpdate();
                                    _plugin.PrintWarning($"Tank has been stuck for over {_plugin.config.stuckErrorKill} times and has been teleported to start of the road at '{apc.transform.position}'");
                                }
                            }

                        }
                        stuckCount = 0;
                        _plugin.PrintWarning($"Tank has been stuck for over a minute! Current location: {apc.transform.position}. Sending list of entities nearby:");
                        int entityCount = 0;
                        foreach (var entity in roadEntities)
                        {
                            if (entity.net.ID == apc.net.ID) continue;
                            _plugin.PrintWarning($"Entity ShortPrefabName: {entity.ShortPrefabName}");
                            entityCount++;
                        }
                        if (entityCount == 0)
                            _plugin.PrintWarning($"NO ENTITIES FOUND!");
                        else
                            _plugin.PrintWarning("Its recommended to check these entities and add them to filter, because players could abuse and block bradley!");
                        _plugin.PrintWarning("It can be also a road that bradley cound't reach!");
                    }
                }
                else
                    lastCheckpoint = apc.currentPathIndex;
            }

            private void Brakes(bool enable)
            {
                if (enable)
                {
                    apc.ApplyBrakeTorque(1, true);
                    apc.ApplyBrakeTorque(1, false);
                    brakesOn = true;
                    noFighterFoundCount = 0;
                }
                else if (brakesOn)
                {
                    noFighterFoundCount++;
                    if (noFighterFoundCount < _plugin.config.profiles[profile].loseIntrestTime) return;
                    apc.ApplyBrakeTorque(0, true);
                    apc.ApplyBrakeTorque(0, false);
                    brakesOn = false;
                }
            }

            public void CheckMLRSSpawn()
            {
                if (!_plugin.config.profiles[profile].mlrsEnable) return;
                foreach (var health in _plugin.config.profiles[profile].mlrsHealthSpawns)
                {
                    if (apc._health > health) continue;
                    if (ranMLRS.Contains(health)) continue;
                    ranMLRS.Add(health);
                    foreach (var attacker in targets)
                    {
                        BasePlayer attackerPlayer = BasePlayer.FindByID(attacker);
                        if (attackerPlayer == null) continue;
                        if (Vector3.Distance(attackerPlayer.transform.position, apc.transform.position) > 100) continue;
                        if (_plugin.config.profiles[profile].mlrsSound != "")
                            EffectNetwork.Send(new Effect(_plugin.config.profiles[profile].mlrsSound, attackerPlayer, 0, new Vector3(0, 1.7f, 0), Vector3.forward), attackerPlayer.net.connection);
                        if (_plugin.config.profiles[profile].mlrsMessage)
                            _plugin.SendReply(attackerPlayer, _plugin.Lang("MlrsIncoming", attackerPlayer.UserIDString));
                        for (int i = 0; i < _plugin.config.profiles[profile].mlrsAmount; i++)
                        {
                            Vector3 dropPos = attackerPlayer.transform.position;
                            float randomization = _plugin.config.profiles[profile].mlrsRandom;
                            dropPos.x += Core.Random.Range(-randomization, randomization);
                            dropPos.z += Core.Random.Range(-randomization, randomization);
                            TimedExplosive rocket = GameManager.server.CreateEntity("assets/content/vehicles/mlrs/rocket_mlrs.prefab", new Vector3(dropPos.x, _plugin.config.profiles[profile].mlrsHeight, dropPos.z)) as TimedExplosive;
                            rocket.timerAmountMin = 150;
                            rocket.timerAmountMax = 150;
                            rocket.explosionRadius = _plugin.config.profiles[profile].mlrsExplosionRadius;
                            ServerProjectile projectile = rocket.GetComponent<ServerProjectile>();
                            projectile.gravityModifier = Core.Random.Range(0.1f, 0.2f);
                            projectile.InitializeVelocity(new Vector3(0, -18, 0));
                            foreach (var damageType in rocket.damageTypes)
                            {
                                if (damageType.type == DamageType.Blunt) damageType.amount = _plugin.config.profiles[profile].mlrsBluntDamage;
                                else if (damageType.type == DamageType.Explosion) damageType.amount = _plugin.config.profiles[profile].mlrsExplosionDamage;
                                else if (damageType.type == DamageType.AntiVehicle) damageType.amount = 0;
                            }
                            rocket.Spawn();
                        }
                    }
                }
            }

            private void OnDestroy()
            {
                CancelInvoke(CheckTargets);
                apc = null;
            }
        }
        private class RoadEditorController : FacepunchBehaviour
        {
            public string currentMode = "";
            public int currentIndex = -1;
            public string roadName = "";
            public string roadFileName = "";
            public RoadInfo roadInfo;
            public List<Vector3> cachedCheckpoints = new List<Vector3>();

            private void OnDestroy() => cachedCheckpoints.Clear();
        }

        private void LoadMessages()
        {
            Dictionary<string, string> langFile = new Dictionary<string, string>()
            {
                ["NoPermission"] = "You don't have permission to use this command!",
                ["RouteHelpV3"] = "Command usage: \n<color=#5c81ed>/tankroute <routeName></color> - Opens route editor with previously created/generated route.\nIf it is server-generated road you will start making new road based on generated one.\n<color=#5c81ed>/tankroute new</color> - Opens route editor without any road loaded.\n<color=#5c81ed>/tankroute default</color> - Displays list of available default roads. (Raw road paths from RUST)\n<color=#5c81ed>/tankroute edited</color> - Displays list of available roads edited automatically by RoadBradley plugin for usage.\n<color=#5c81ed>/tankroute custom</color> - Displays list of all user-made routes.",
                ["RouteList"] = "<color=#5c81ed>Available routes:</color> {0}",
                ["RouteLoaded"] = "Route <color=#5c81ed>{0}</color> has been loaded to the editor.",
                ["BradleyFights"] = "BRADLEY FIGHTS",
                ["Levels"] = "TIERS",
                ["BradleyInfo"] = "INFORMATIONS",
                ["Spawn"] = "SPAWN",
                ["Requirements"] = "REQUIREMENTS",
                ["Health"] = "Max Health",
                ["ViewRange"] = "Tank View Range",
                ["FireRate"] = "Turret Fire Rate",
                ["BurstLength"] = "Turret Burst Length",
                ["BulletDamage"] = "Turret Bullet Damage",
                ["DailyLimit"] = "Daily Purchase Limit",
                ["Cooldown"] = "Purchase Cooldown",
                ["Unlimited"] = "UNLIMITED",
                ["YouAreReady"] = "You can purchase and call the bradley!",
                ["Locked"] = "LOCKED",
                ["DailyLimitReached"] = "You've reached daily limit of purchases!",
                ["NoAllItems"] = "You don't have all required items!",
                ["InCooldown"] = "Purchase in cooldown! You need to wait {0}.",
                ["NoPurchases"] = "There is no bradleys available to purchase!",
                ["TimeToKill"] = "Time To Kill",
                ["AlreadySpawned"] = "You've spawned one bradley already! You need to destroy it first!",
                ["CannotDamage"] = "Only the owner, their friends and/or team can damage this bradley!",
                ["CannotLoot"] = "Only the owner, their friends and/or team can collect this bradley loot!",
                ["FightLost"] = "You've lost fight with the bradley!\nYou can try again by <color=#5c81ed>purchasing another one</color>!",
                ["NoFreeRoad"] = "There is no free road on the map!\nWait a moment!",
                ["BradleyPurchased"] = "Your <color=#5c81ed>{0}</color> Bradley has spawned at <color=#5c81ed>{1}</color>!\nYou have <color=#5c81ed>{2}</color> to destroy it, and additional <color=#5c81ed>{3}</color> to came!",
                ["BradleySpawned"] = "An <color=#5c81ed>{0}</color> Bradley has spawned at <color=#5c81ed>{1}</color>!",
                ["NoCooldown"] = "NO COOLDOWN",
                ["KillReward"] = "You've got an reward for damaging <color=#5c81ed>{0}%</color> of Bradley's health:",
                ["KillRewardCurrency"] = "\n <color=#5c81ed>-</color> {0}$",
                ["KillRewardItem"] = "\n <color=#5c81ed>-</color> x{1} {0}",
                ["BradleyEditorTitle"] = "ROUTE EDITOR",
                ["CurrentRouteTitle"] = "CURRENT ROUTE",
                ["ShowPathButton"] = "SHOW CHECKPOINTS",
                ["ShowHintButton"] = "SHOW USAGE HINT",
                ["SaveExitButton"] = "SAVE AND EXIT",
                ["IsDefault"] = "DEFAULT",
                ["IsRandomEvent"] = "RANDOM",
                ["IsPurchasable"] = "PURCHASABLE",
                ["IsOccupied"] = "OCCUPIED",
                ["CheckpointCount"] = "CHECKPOINTS: {0}",
                ["EditorHint"] = "To click on UI - open chat to get the cursor.\nIn order to add/edit/remove use Semi Automatic Rifle. You should get one.\n\n<color=#5c81ed>Shoot</color> - Create checkpoint on hit position.\n<color=#5c81ed>Shoot near checkpoint</color> - update its position.\n<color=#5c81ed>Shoot near checkpoint holding SPRINT</color> - Create checkpoint one index after shoot checkpoint.\n<color=#5c81ed>Shoot near checkpoint holding DUCK</color> - Remove Checkpoint.",
                ["InsertInfo"] = "Shoot, where you want to insert new - <color=#5c81ed>{0}</color> checkpoint!",
                ["CheckpointRemove"] = "Checkpoint <color=#5c81ed>{0}</color> has been removed!",
                ["UpdateInfo"] = "Shoot, where you want to move - <color=#5c81ed>{0}</color> checkpoint!",
                ["CheckpointAdd"] = "New checkpoint has been added!",
                ["CheckpointInsert"] = "You've inserted new checkpoint!",
                ["CheckpointUpdate"] = "You've updated checkpoint!",
                ["RoadNameSet"] = "You've set this custom rote name to <color=#5c81ed>{0}</color>!",
                ["RouteEditWarning"] = "<color=red>Warning! This route is used in plugin right now! \nIt's recommended to edit unused routes! \nWe recommend disabling this route from pool first.",
                ["NoFileName"] = "This road has no name!\nSet your route name with <color=#5c81ed>/tankroute name <routeName></color> command!",
                ["RoadSaved"] = "Road has been saved succesfully!",
                ["EditorClosed"] = "Editor has been closed!",
                ["NotBedOwner"] = "This bed is bradley death respawn, and You are not owner of this bed!\nYou cannot change its name!",
                ["RespawnPointRemoved"] = "Bradley death respawn point removed!",
                ["TooManyBags"] = "You've set too many respawn bags.\nRemove previously created first, in order to create new one!",
                ["RespawnPointSet"] = "New bradley respawn point set!",
                ["RespawnNotFound"] = "Unfortunatelly we've not found respawn point for your bradley fight.\nYou can add <color=#5c81ed>bradley</color> to sleeping bag name to make it spawn for bradley fight!",
                ["Respawned"] = "You've spawned in your bradley fight sleeping bag!",
                ["TankStuckKill"] = "Your Bradley has been stuck for too long and has been <color=#5c81ed>killed</color>!",
                ["TankStuckTeleport"] = "Your Bradley has been stuck for too long and has been <color=#5c81ed>teleported</color>!",
                ["TankDeathAnnouncement"] = "The <color=#5c81ed>{0}</color> Bradley has been killed!\n\nDamage dealt to Bradley:",
                ["TankDeathAnnouncementPlayerFormat"] = "\n{0}. <color=#5c81ed>{1}</color> - {2}",
                ["SpawnHelp"] = "Usage:\n/spawnrbtank <profile> <route> - Spawns road bradley with specified profile on specified route.\n/spawnrbtank <profile> <userId> - Spawns road bradley with specified profile on specified route as purchased by user.",
                ["ProfileNotFound"] = "Profile '{0}' not found.",
                ["RouteNotFound"] = "Route '{0}' not found.",
                ["TankSpawned"] = "If there is no limits on the map, your tank should be succesfully spawned.",
                ["UserIdIncorrect"] = "Id '{0}' is not correct userId.",
                ["PlayerOffline"] = "Player with id '{0}' is offline.",
                ["TankSpawnedUser"] = "If there is no limits on the map, your tank for user '{0}' should be succesfully spawned.",
                ["FightUnauthorizedInfo"] = "Your tank is hitting buildings that are not owned by anyone, who fights right now.\nIf tank will keep shooting, it will despawn and fight will be lost.\nCurrent warning: {0}/{1}",
                ["FightLostUnauthorized"] = "You've lost your fight because tank tried to damage too many entities not owned by anyone, who fought the tank.",
                ["MlrsIncoming"] = "Bradley called MLRS strike! Look up!"
            };
            foreach (var tank in config.profiles.Keys)
                langFile.Add($"Tank_{tank}", $"{tank} Bradley");
            lang.RegisterMessages(langFile, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(config = new PluginConfig()
            {
                obstacleWhitelist = new List<string>()
                {
                    "roadsign",
                    "train_track",
                    "road_tunnel",
                    "doorcloser"
                },
                killedEntities = new List<string>()
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
                roadBlacklist = new Dictionary<string, List<int>>()
                {
                    { "default", new List<int> {
                        0,
                        3
                    }},
                    { "Detroit", new List<int> {
                        1
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
                        requiredItems = new List<SubItemConfig>()
                        {
                            new SubItemConfig()
                            {
                                shortname = "scrap",
                                skin = 0,
                                amount = 100,
                                displayName = "",
                            },
                            new SubItemConfig()
                            {
                                shortname = "metal.fragments",
                                skin = 0,
                                amount = 1500,
                                displayName = "",
                            }
                        }
                    }},
                    { "Hard", new PurchaseConfig() {
                        cooldown = 7200,
                        maxDaily = 1,
                        requiredItems = new List<SubItemConfig>()
                        {
                            new SubItemConfig()
                            {
                                shortname = "scrap",
                                skin = 0,
                                amount = 1000,
                                displayName = "",
                            },
                            new SubItemConfig()
                            {
                                shortname = "currency",
                                skin = 0,
                                amount = 1500,
                                displayName = "{0}$",
                            }
                        }
                    }},
                    { "Extreme", new PurchaseConfig() {
                        permission = "roadbradley.extreme",
                        cooldown = 7200,
                        maxDaily = 1,
                        requiredItems = new List<SubItemConfig>()
                        {
                            new SubItemConfig()
                            {
                                shortname = "scrap",
                                skin = 0,
                                amount = 1000,
                                displayName = "",
                            },
                            new SubItemConfig()
                            {
                                shortname = "currency",
                                skin = 0,
                                amount = 5000,
                                displayName = "{0}$",
                            },
                            new SubItemConfig()
                            {
                                shortname = "metal.fragments",
                                skin = 0,
                                amount = 10000,
                                displayName = "",
                            }
                        }
                    }}
                },
                profiles = new Dictionary<string, ProfileConfig>()
                {
                    { "Normal", new ProfileConfig() {
                        targetItems = new List<string>()
                        {
                            "rocket.launcher",
                            "explosive.timed",
                            "explosive.satchel"
                        },
                        tankScale = 0.6f,
                        viewRange = 50,
                    }},
                    { "Hard", new ProfileConfig() {
                        targetItems = new List<string>()
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
                        damageReward = new List<SubItemConfig>()
                        {
                            new SubItemConfig()
                            {
                                shortname = "currency",
                                amount = 2000,
                                skin = 0,
                                displayName = ""
                            }
                        }
                    }},
                    { "Extreme", new ProfileConfig() {
                        targetItems = new List<string>()
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
                        damageReward = new List<SubItemConfig>()
                        {
                            new SubItemConfig()
                            {
                                shortname = "currency",
                                amount = 2000,
                                skin = 0,
                                displayName = ""
                            },
                            new SubItemConfig()
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

            [JsonProperty("Tank Route - Display every X checkpoint (Command)")]
            public int checkpointDisplay = 1;

            [JsonProperty("Tank Route - Use Asphalt Roads Only")]
            public bool asphaltOnly = false;

            [JsonProperty("Tank Route - Height Offset")]
            public float heightOffset = 1;

            [JsonProperty("Tank Route - Check for Map Obstacles (recommended for custom maps with prefabs on roads)")]
            public bool checkObstacles = false;

            [JsonProperty("Tank Route - Remove Roads With Obstacles From Pool")]
            public bool removeObstacleRoads = false;

            [JsonProperty("Tank Route - Kill/Teleport Bradley after X Stuck Errors (0, to disable)")]
            public int stuckErrorKill = 0;

            [JsonProperty("Tank Route - Stuck Fix Method (true = kill, false = teleport to start)")]
            public bool killOnStuck = false;

            [JsonProperty("Tank Route - Broadcast Stuck To Fighters")]
            public bool broadcastStuck = true;

            [JsonProperty("Tank Route - Randomize Spawn On Road")]
            public bool randomizeSpawn = true;

            [JsonProperty("Tank Route - Remove Timed-Event Roads From Purchasable Pool")]
            public bool removeTimedRoads = false;

            [JsonProperty("Tank Route - Obstacle Keyword Whitelist")]
            public List<string> obstacleWhitelist = new List<string>();

            [JsonProperty("Tank Route - Remove first and last checkpoints of purchased bradleys")]
            public int removePurchasableCheckpointAmount = 3;

            [JsonProperty("Tank Route Editor - Height Offset")]
            public float editorHeightOffset = 1;

            [JsonProperty("Targeting - Target NPC")]
            public bool targetNpc = false;

            [JsonProperty("Loot & Damage Share - Check For Caller Building Damage")]
            public bool callerDamage = true;

            [JsonProperty("Loot & Damage Share - Allow Damage For Caller Team Buildings")]
            public bool callerTeamCheck = true;

            [JsonProperty("Loot & Damage Share - Unowned Damage Entity Reminder")]
            public int damageAuthsRemind = 50;

            [JsonProperty("Loot & Damage Share - Unowned Damage Entity Bradley Kill")]
            public int damageAuths = 200;

            [JsonProperty("Loot & Damage Share - Ignore Ownership Checks")]
            public bool ignoreOwnerships = false;

            [JsonProperty("Loot & Damage Share - Use Friends")]
            public bool useFriends = false;

            [JsonProperty("Loot & Damage Share - Use Clans")]
            public bool useClans = false;

            [JsonProperty("Loot & Damage Share - Use RUST Teams")]
            public bool useTeams = true;

            [JsonProperty("Loot & Damage Share - Limit Server Spawned Bradleys Loot To Top Damage Player")]
            public bool limitToTopDamage = false;

            [JsonProperty("Purchases - Used Purchase System (0 - None, 1 - Economics, 2 - ServerRewards, 3 - ShoppyStock)")]
            public int moneyPlugin = 3;

            [JsonProperty("Purchases - Used Currency (If ShoppyStock Is Used)")]
            public string currency = "rp";

            [JsonProperty("Purchases - Max Purchased Bradleys (0, to disable)")]
            public int maxBradleys = 3;

            [JsonProperty("Tank Target - Target Sleepers")]
            public bool targetSleepers = false;

            [JsonProperty("Tank Info - Display Timed Bradley Info")]
            public bool enableTankInfo = true;

            [JsonProperty("Tank Info - Timed Bradley Info Format")]
            public string tankInfo = "-=BRADLEY=-\nTier: {0}";

            [JsonProperty("Tank Info - Display Purchased Bradley Info")]
            public bool enablePurchasedTankInfo = true;

            [JsonProperty("Tank Info - Purchased Bradley Info Format")]
            public string purchasedTankInfo = "-=BRADLEY=-\nTier: {0}\nOwner: {1}";

            [JsonProperty("Bag Respawns - Enabled")]
            public bool enabledBags = true;

            [JsonProperty("Bag Respawns - Max Bags Per Player")]
            public int maxBags = 4;

            [JsonProperty("Bag Respawns - Max Bed Distance From Player")]
            public float maxBedDistance = 100;

            [JsonProperty("Tank Unstuck - Killed Entity Names")]
            public List<string> killedEntities = new List<string>();

            [JsonProperty("Tank Routes - Spawns Per Maps")]
            public Dictionary<string, List<string>> mapTracks = new Dictionary<string, List<string>>();

            [JsonProperty("Tank Routes - Purchasable Road ID Blacklist Per Map")]
            public Dictionary<string, List<int>> roadBlacklist = new Dictionary<string, List<int>>();

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

        private class ProfileConfig
        {
            [JsonProperty("Tank Target - Max distance to target")]
            public float maxTargetDistance = 10;

            [JsonProperty("Tank Target - Max clothing amount")]
            public float maxTargetClothing = 4;

            [JsonProperty("Tank Target - Targeted items")]
            public List<string> targetItems = new List<string>();

            [JsonProperty("Tank Target - Lose Target After X Seconds")]
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

            [JsonProperty("Loot - Rewards For Damage Dealt")]
            public List<SubItemConfig> damageReward = new List<SubItemConfig>();

            [JsonProperty("Loot - Fire Lock Time (in seconds)")]
            public int lockTime = 60;
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

        private class PurchaseConfig
        {
            [JsonProperty("Purchases - Required Permission (leave blank, to disable)")]
            public string permission = "";

            [JsonProperty("Purchases - Cooldown (in seconds, 0 to disable)")]
            public int cooldown = 0;

            [JsonProperty("Purchases - Max Daily (in seconds, 0 to disable)")]
            public int maxDaily = 3;

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

            [JsonProperty("Purchases - Announce Kill To Fighters Only")]
            public bool announceKillFighters = true;

            [JsonProperty("Purchases - Required Items")]
            public List<SubItemConfig> requiredItems = new List<SubItemConfig>();
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
            public string shortname;

            [JsonProperty("Amount")]
            public int amount;

            [JsonProperty("Amount Randomizer Amount (+ and -)")]
            public int amountRandomize = 0;

            [JsonProperty("Always Include Chance (0-100)")]
            public float alwaysInclude = 0;

            [JsonProperty("Max Always Includes Per Loot (0 to disable)")]
            public int maxPerLoot = 0;

            [JsonProperty("Skin")]
            public ulong skin;

            [JsonProperty("Display Name")]
            public string displayName;

            [JsonProperty("Chance")]
            public int chance;

            [JsonProperty("Additional Items")]
            public List<SubItemConfig> additionalItems = new List<SubItemConfig>();
        }

        private class SubItemConfig
        {
            [JsonProperty("Shortname")]
            public string shortname;

            [JsonProperty("Amount")]
            public int amount;

            [JsonProperty("Skin")]
            public ulong skin;

            [JsonProperty("Display Name")]
            public string displayName;
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
            limitData = Interface.Oxide.DataFileSystem.ReadObject<LimitData>($"{this.Name}/LimitData");
            roadData = Interface.Oxide.DataFileSystem.ReadObject<RoadData>($"{this.Name}/RoadData");
            bagData = Interface.Oxide.DataFileSystem.ReadObject<BagData>($"{this.Name}/BagData");
            timer.Every(Core.Random.Range(500, 700), SaveData);
            if (limitData == null)
            {
                PrintWarning("Data file is corrupted! Generating new data file...");
                Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}/LimitData", new LimitData());
                limitData = Interface.Oxide.DataFileSystem.ReadObject<LimitData>($"{this.Name}/LimitData");
            }
            if (roadData == null)
            {
                PrintWarning("Data file is corrupted! Generating new data file...");
                Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}/RoadData", new RoadData());
                roadData = Interface.Oxide.DataFileSystem.ReadObject<RoadData>($"{this.Name}/RoadData");
            }
            if (bagData == null)
            {
                PrintWarning("Data file is corrupted! Generating new data file...");
                Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}/BagData", new BagData());
                bagData = Interface.Oxide.DataFileSystem.ReadObject<BagData>($"{this.Name}/BagData");
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}/LimitData", limitData);
            Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}/RoadData", roadData);
            Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}/BagData", bagData);
        }
    }
}
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */