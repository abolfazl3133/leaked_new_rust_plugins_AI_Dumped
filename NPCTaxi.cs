using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Ext.Chaos.AStar;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Vector3Ex = UnityEngine.Vector3Ex;

namespace Oxide.Plugins
{
    [Info("NPCTaxi", "k1lly0u", "0.1.19")]
    [Description("Allow players to call NPC driven taxi's to drive them around")]
    class NPCTaxi : RustPlugin, IPathFinder
    {
        #region Fields  
        [PluginReference] Plugin ServerRewards, Economics, Friends, Clans;

        private Telephone _telephone;

        private readonly List<GridNode> _roadNodes = new List<GridNode>();

        private static int _scrapItemID;

        private static NPCTaxi Instance;

        private static Oxide.Ext.Chaos.AStar.PathFinder PathFinder;

        private static List<BasePlayer> PathRequests = new List<BasePlayer>();
        private static List<VehicleAI> ActiveVehicles = new List<VehicleAI>();
        private static readonly Hash<ulong, float> Cooldowns = new Hash<ulong, float>();

        private const string SCIENTIST_PREFAB = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab";
        private const string PHONE_PREFAB = "assets/prefabs/voiceaudio/telephone/telephone.deployed.prefab";

        private const string PERMISSION_USE = "npctaxi.use";
        private const string PERMISSION_NOCOST = "npctaxi.nocost";
        private const string PERMISSION_NOCOOLDOWN = "npctaxi.nocooldown";

        private const float MAXIMUM_CALL_DISTANCE_FROM_ROAD = 20;

        private const float WORLD_GRID_SIZE = 2f;

        public bool DebugMode => Configuration.PathfinderDebug;

        private bool DrawSensors => false;

        private PurchaseType _purchaseType;

        private enum PurchaseType { Scrap, ServerRewards, Economics }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Instance = this;

            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_NOCOST, this);
            permission.RegisterPermission(PERMISSION_NOCOOLDOWN, this);

            for (int i = 0; i < Configuration.Costs.Count; i++)
            {
                if (!permission.PermissionExists(Configuration.Costs[i].Permission))
                    permission.RegisterPermission(Configuration.Costs[i].Permission, this);
            }
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(Messages, this);
        
        private void OnServerInitialized()
        {
            _purchaseType = ParseType<PurchaseType>(Configuration.CostType);
            _scrapItemID = ItemManager.FindItemDefinition("scrap").itemid;            

            if (!Configuration.Invincible)
                Unsubscribe(nameof(OnEntityTakeDamage));

            if (!Configuration.UseTelephone)
                Unsubscribe(nameof(OnPhoneDial));
            else SpawnTelephone();

            PathFinder = PathManager.RegisterPathFinder(this, new PathFinderConfig
            {
                NodeEvaluator = EvaluateNode, 
                WorldSize = World.Size, 
                CellSize = WORLD_GRID_SIZE, 
                PathInterval = 10f, 
                BlurRate = 2
            });
        }

        private void OnEntityTakeDamage(BaseVehicleModule baseVehicleModule, HitInfo hitInfo)
        {
            if (baseVehicleModule.GetParentEntity()?.GetComponent<VehicleAI>() != null)
            {
                hitInfo.damageTypes.Clear();
                hitInfo.HitEntity = null;
                hitInfo.HitMaterial = 0;
                hitInfo.PointStart = Vector3.zero;
            }
        }

        private void OnEntityTakeDamage(ScientistNPC scientistNPC, HitInfo hitInfo)
        {
            if (scientistNPC.GetMountedVehicle()?.GetComponent<VehicleAI>() != null)
            {
                hitInfo.damageTypes.Clear();
                hitInfo.HitEntity = null;
                hitInfo.HitMaterial = 0;
                hitInfo.PointStart = Vector3.zero;
            }
        }

        private object OnTurretTarget(AutoTurret autoTurret, ScientistNPC scientistNPC)
        {
            if (ActiveVehicles.Count == 0)
                return null;

            if (scientistNPC?.GetMountedVehicle()?.GetComponent<VehicleAI>() != null)            
                return true;

            return null;
        }

        private void OnLootEntity(BasePlayer player, Rust.Modular.EngineStorage engineStorage)
        {
            if (engineStorage.GetComponentInParent<VehicleAI>() != null)
                NextTick(player.EndLooting);
        }

        private void OnEntityKill(ModularCar modularCar) => UnityEngine.Object.Destroy(modularCar.GetComponent<VehicleAI>());

        private void OnMapMarkerAdd(BasePlayer player, MapNote mapNote)
        {
            VehicleAI vehicleAI = player.GetMountedVehicle()?.GetComponent<VehicleAI>();
            if (vehicleAI != null && vehicleAI.Caller == player && vehicleAI.State == VehicleAI.TaxiState.WaitingForInput)
            {
                int topology = TerrainMeta.TopologyMap.GetTopology(mapNote.worldPosition, MAXIMUM_CALL_DISTANCE_FROM_ROAD);

                if (!ContainsTopology(topology, TerrainTopology.Enum.Road))
                {
                    player.ChatMessage(Message("Error.TooFarAway", player.userID, true));
                    return;
                }

                Vector3 closestNode = FindClosestNodeTo(mapNote.worldPosition);

                vehicleAI.OnMarkerPlaced(closestNode, mapNote);
            }
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (player.IsNpc)
                return null;

            VehicleAI vehicleAI = mountable.VehicleParent()?.GetComponent<VehicleAI>();
            if (vehicleAI != null)
            {
                if (vehicleAI.State == VehicleAI.TaxiState.WaitingForInput)
                {
                    if (player == vehicleAI.Caller)
                    {
                        vehicleAI.OnCallerEnteredVehicle();
                        return null;
                    }

                    if (player.currentTeam != 0UL && player.currentTeam.Equals(vehicleAI.Caller.currentTeam))
                        return null;

                    if (IsFriend(vehicleAI.Caller.userID, player.userID))
                        return null;

                    if (IsClanmate(vehicleAI.Caller.userID, player.userID))
                        return null;
                }

                return false;
            }
            return null;
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            VehicleAI vehicleAI = mountable.VehicleParent()?.GetComponent<VehicleAI>();
            if (vehicleAI != null)
            {
                if (player == vehicleAI.Caller)
                {
                    player.SendConsoleCommand("gametip.hidegametip");
                    player.ChatMessage(Message("Notification.Goodbye", player.userID, true));
                    vehicleAI.CancelService();

                    Cooldowns[player.userID] = Time.time + Configuration.CooldownTime;
                }
            }
        }

        private object OnPhoneDial(PhoneController callerPhone, PhoneController receiverPhone, BasePlayer player)
        {
            if ((receiverPhone.ParentEntity as Telephone) == _telephone)
            {
                receiverPhone.OnDialFailed(Telephone.DialFailReason.RemoteHangUp);

                callerPhone.ServerHangUp();
                callerPhone.ClearCurrentUser();

                RequestTaxi(player, true);
                return true;
            }
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            const string VEHICLE_SWAP_SEAT = "vehicle.swapseats";
            const string PLAYER_SWAP_SEAT = "player.swapseat";

            BasePlayer player = arg.Player();
            if (player != null && (arg.cmd.FullName.Equals(PLAYER_SWAP_SEAT, StringComparison.OrdinalIgnoreCase) || 
                                   arg.cmd.FullName.Equals(VEHICLE_SWAP_SEAT, StringComparison.OrdinalIgnoreCase)))
            {
                if (player.GetMountedVehicle()?.GetComponent<VehicleAI>() != null)
                {
                    player.ChatMessage(Message("Notification.NoSwappingSeats", player.userID, true));
                    return false;
                }
            }
            return null;
        }

        private void Unload()
        {
            if (_telephone != null && !_telephone.IsDestroyed)
            {
                TelephoneManager.DeregisterTelephone(_telephone.Controller);

                _telephone.Kill(BaseNetworkable.DestroyMode.None);
            }

            for (int i = ActiveVehicles.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(ActiveVehicles[i]);
            }

            ActiveVehicles.Clear();
            PathRequests.Clear();
            
            PathManager.Unload(this);

            Instance = null;
            Configuration = null;
            PathFinder = null;
        }
        #endregion

        #region Path Generator Callbacks         
        private void OnPathGenerated(PathCompletedResult pathResult)
        {        
            if (DebugMode)
                Debug.Log("Path Generated");

            List<Vector3> results = pathResult.Results;
            BasePlayer requester = pathResult.Reference as BasePlayer;

            if (requester)
            {
                PathRequests.Remove(requester);

                requester.ChatMessage(Message("Notification.Inbound", requester.userID, true));
                SendMapMarker(requester, results[results.Count - 1], "Taxi Pickup");

                VehicleAI vehicleAI = CreateTaxiVehicle(results[0] + Vector3.up, Quaternion.LookRotation((results[1] - results[0]).normalized));
                vehicleAI.InitializeVehicle(requester, results);

                ActiveVehicles.Add(vehicleAI);

                Cooldowns[requester.userID] = Time.time + Configuration.CooldownTime;

                if (DebugMode)
                    Debug.Log($"Taxi spawned at {results[0]}");
            }
        }

        private void OnPathFailed(PathFailedResult pathResult)
        {
            if (DebugMode)
                Debug.Log("Path Generation Failed");
            
            BasePlayer requester = pathResult.Reference as BasePlayer;

            if (requester)
            {
                requester.ChatMessage(Message($"Error.PathFail.{pathResult.FailType}", requester.userID));
                PathRequests.Remove(requester);
            }
        }
        #endregion

        #region Node Helpers
        private Vector3 FindClosestNodeTo(Vector3 position)
        {
            Vector3 closestNode = position;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < _roadNodes.Count; i++)
            {
                GridNode node = _roadNodes[i];
                float distance = Vector3.Distance(position, node.Position);

                if (distance < closestDistance)
                {
                    closestNode = node.Position;
                    closestDistance = distance;
                }
            }

            return closestNode;
        }

        private Vector3 FindNodeAtDistance(Vector3 position, float range, float threshold)
        {
            Vector3 closestNode = position;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < _roadNodes.Count; i++)
            {
                GridNode node = _roadNodes[i];
                float distance = Vector3.Distance(position, node.Position);

                if (distance.Approximately(range, threshold))
                    return node.Position;

                if (distance < closestDistance)
                {
                    closestNode = node.Position;
                    closestDistance = distance;
                }
            }

            return closestNode;
        }
        #endregion

        #region Map Markers
        private static void SendMapMarker(BasePlayer player, Vector3 position, string text)
        {
            if (player.State.pointsOfInterest == null)
                player.State.pointsOfInterest = Pool.GetList<MapNote>();
            
            MapNote mapNote = Pool.Get<MapNote>();
            mapNote.noteType = 1;
            mapNote.worldPosition = position;
            mapNote.label = text;
            mapNote.colourIndex = 0;
            mapNote.icon = 4;

            player.State.pointsOfInterest.Add(mapNote);

            using (MapNoteList list = Pool.Get<MapNoteList>())
            {
                list.notes = Pool.GetList<MapNote>();

                if (player.ServerCurrentDeathNote != null)
                    list.notes.Add(player.ServerCurrentDeathNote);

                if (player.State.pointsOfInterest != null)
                    list.notes.AddRange(player.State.pointsOfInterest);
                
                player.ClientRPCPlayer<MapNoteList>(null, player, "Client_ReceiveMarkers", list);
                list.notes.Clear();
            }
        }
        #endregion

        #region Telephone
        private void SpawnTelephone()
        {
            _telephone = GameManager.server.CreateEntity(PHONE_PREFAB) as Telephone;
            _telephone.enableSaving = false;
            _telephone.limitNetworking = true;

            UnityEngine.Object.Destroy(_telephone.GetComponent<Collider>());
            UnityEngine.Object.Destroy(_telephone.GetComponent<GroundWatch>());
            UnityEngine.Object.Destroy(_telephone.GetComponent<DestroyOnGroundMissing>());

            _telephone.Spawn();

            NextTick(() =>
            {
                _telephone.Controller.PhoneName = "Taxi Service";
                _telephone.Controller.RequirePower = false;
                _telephone.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            });
        }
        #endregion

        #region Helpers
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

        private int GetFareCost(string playerId)
        {
            int cost = int.MaxValue;

            for (int i = 0; i < Configuration.Costs.Count; i++)
            {
                ConfigData.CostEntry costEntry = Configuration.Costs[i];
                if (permission.UserHasPermission(playerId, costEntry.Permission))                
                    cost = Mathf.Min(cost, costEntry.Cost);                
            }

            return cost;
        }

        private int GetAmount(BasePlayer player)
        {
            switch (_purchaseType)
            {
                case PurchaseType.Scrap:
                    return player.inventory.GetAmount(_scrapItemID);
                case PurchaseType.ServerRewards:
                    return (int)ServerRewards?.Call("CheckPoints", player.userID);
                case PurchaseType.Economics:
                    return (int)((double)Economics?.Call("Balance", player.UserIDString));
            }

            return 0;
        }

        private void SubtractFareCost(BasePlayer player, int fareCost)
        {
            switch (_purchaseType)
            {
                case PurchaseType.Scrap:
                    player.inventory.Take(null, _scrapItemID, fareCost);
                    return;
                case PurchaseType.ServerRewards:
                    ServerRewards?.Call("TakePoints", player.userID, fareCost);
                    return;
                case PurchaseType.Economics:
                    Economics?.Call("Withdraw", player.UserIDString, (double)fareCost);
                    return;
            }
        }

        private void RefundFareCost(BasePlayer player, int refundFare)
        {
            switch (_purchaseType)
            {
                case PurchaseType.Scrap:
                    player.GiveItem(ItemManager.CreateByItemID(_scrapItemID, refundFare), BaseEntity.GiveItemReason.PickedUp);
                    return;
                case PurchaseType.ServerRewards:
                    ServerRewards?.Call("AddPoints", player.userID, refundFare);
                    return;
                case PurchaseType.Economics:
                    Economics?.Call("Deposit", player.UserIDString, (double)refundFare);
                    return;
            }
        }

        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (!Clans)
                return false;

            string playerTag = Clans?.Call<string>("GetClanOf", playerId);
            string friendTag = Clans?.Call<string>("GetClanOf", friendId);

            if (!string.IsNullOrEmpty(playerTag) && playerTag == friendTag)
                return true;

            return false;
        }

        private bool IsFriend(ulong playerID, ulong friendID)
        {
            if (!Friends)
                return false;

            return (bool)Friends?.Call("AreFriends", playerID, friendID);
        }

        private static bool ContainsTopologyAtPoint(TerrainTopology.Enum mask, Vector3 position) => (TerrainMeta.TopologyMap.GetTopology(position) & (int)mask) != 0;
        
        private static bool ContainsTopology(int topology, TerrainTopology.Enum mask) => (topology & (int)mask) != 0;        
        #endregion

        #region Node Evaluator
        public void EvaluateNode(GridNode gridNode, Vector3 position)
        {
            position.y = TerrainMeta.HeightMap.GetHeight(position);

            gridNode.Position = position;

            if (position.y < 0)
            {
                gridNode.C_Cost = 1000;
                gridNode.IsBlocked = true;
                return;
            }
            
            int topologyInRadius = TerrainMeta.TopologyMap.GetTopology(position, gridNode.CellSize * 0.5f);

            if (ContainsTopology(topologyInRadius, TerrainTopology.Enum.Road))
            {
                _roadNodes.Add(gridNode);
                gridNode.C_Cost = 0;
                return;
            }

            if (ContainsTopology(topologyInRadius, TerrainTopology.Enum.Ocean | TerrainTopology.Enum.River | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Cliff | 
                                 TerrainTopology.Enum.Swamp | TerrainTopology.Enum.Decor | TerrainTopology.Enum.Building))
            {
                gridNode.C_Cost = 1000;
                gridNode.IsBlocked = true;
                return;
            }
             
            gridNode.C_Cost = 50;

            if (ContainsTopology(topologyInRadius, TerrainTopology.Enum.Roadside))            
                return;
            
            if (ContainsTopology(topologyInRadius, TerrainTopology.Enum.Monument | TerrainTopology.Enum.Forest | TerrainTopology.Enum.Forestside | TerrainTopology.Enum.Cliffside))
                gridNode.C_Cost = 750;

            float slope = TerrainMeta.HeightMap.GetSlope(position);
            float maxSlope = Configuration.Taxi.Tier == 3 ? 60 : Configuration.Taxi.Tier == 2 ? 50 : 40;
            if (slope > maxSlope)
            {
                gridNode.C_Cost = 1000;
                gridNode.IsBlocked = true;
            }
            else gridNode.C_Cost += Mathf.RoundToInt((slope / maxSlope) * 1000f);
        }

        public void OnGridProcessed() { }
        #endregion

        #region Road Prefabs
        public IEnumerator OnCostMapGenerated()
        {
            Debug.Log("[NPCTaxi] Processing road prefabs...");

            _roadPrefabs = JsonConvert.DeserializeObject<Dictionary<string, List<SerializedVector3>>>(SERIALIZED_ROAD_NODES);

            GameObject[] gameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

            int prefabCount = 0;
            for (int i = 0; i < gameObjects.Length; i++)
            {
                GameObject gameObject = gameObjects[i];
                
                if (gameObject.name.Contains("road_"))
                {
                    string name = Core.Utility.GetFileNameWithoutExtension(gameObject.name);
                    List<SerializedVector3> list = FindPrefab(name);
                    if (list != null)
                    {
                        prefabCount++;

                        Transform tr = gameObject.transform;

                        for (int y = 0; y < list.Count; y++)
                        {
                            SerializedVector3 vector3 = list[y];

                            Vector3 v = tr.TransformPoint(vector3.X, vector3.Y, vector3.Z);

                            GridNode node = PathFinder.FindOrCreateCell(v);

                            node.Position = new Vector3(node.Position.x, v.y, node.Position.z);
                            node.C_Cost = 0;
                            node.IsBlocked = false;
                        }
                        
                        yield return null;
                    }
                }
            }
            Debug.Log($"[NPCTaxi] Processed {prefabCount} road prefabs");
        }

        private Dictionary<string, List<SerializedVector3>> _roadPrefabs = new Dictionary<string, List<SerializedVector3>>();

        private struct SerializedVector3
        {
            public float X, Y, Z;
        }

        private List<SerializedVector3> FindPrefab(string name)
        {
            foreach(KeyValuePair<string, List<SerializedVector3>> kvp in _roadPrefabs)
            {
                if (name.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return null;
        }

        #region Road Node Json
        private const string SERIALIZED_ROAD_NODES = @"{'road_18x15':[{'X':-5.1,'Y':0,'Z':-10},{'X':-3.1,'Y':0,'Z':-10},{'X':-1.1,'Y':0,'Z':-10},{'X':0.9,'Y':0,'Z':-10},{'X':2.9,'Y':0,'Z':-10},{'X':4.9,'Y':0,'Z':-10},{'X':-5.1,'Y':0,'Z':-8},{'X':-3.1,'Y':0,'Z':-8},{'X':-1.1,'Y':0,'Z':-8},{'X':0.9,'Y':0,'Z':-8},{'X':2.9,'Y':0,'Z':-8},{'X':4.9,'Y':0,'Z':-8},{'X':-5.1,'Y':0,'Z':-6},{'X':-3.1,'Y':0,'Z':-6},{'X':-1.1,'Y':0,'Z':-6},{'X':0.9,'Y':0,'Z':-6},{'X':2.9,'Y':0,'Z':-6},{'X':4.9,'Y':0,'Z':-6},{'X':-5.1,'Y':0,'Z':-4},{'X':-3.1,'Y':0,'Z':-4},{'X':-1.1,'Y':0,'Z':-4},{'X':0.9,'Y':0,'Z':-4},{'X':2.9,'Y':0,'Z':-4},{'X':4.9,'Y':0,'Z':-4},{'X':-5.1,'Y':0,'Z':-2},{'X':-3.1,'Y':0,'Z':-2},{'X':-1.1,'Y':0,'Z':-2},{'X':0.9,'Y':0,'Z':-2},{'X':2.9,'Y':0,'Z':-2},{'X':4.9,'Y':0,'Z':-2},{'X':-5.1,'Y':0,'Z':0},{'X':-3.1,'Y':0,'Z':0},{'X':-1.1,'Y':0,'Z':0},{'X':0.9,'Y':0,'Z':0},{'X':2.9,'Y':0,'Z':0},{'X':4.9,'Y':0,'Z':0},{'X':-5.1,'Y':0,'Z':2},{'X':-3.1,'Y':0,'Z':2},{'X':-1.1,'Y':0,'Z':2},{'X':0.9,'Y':0,'Z':2},{'X':2.9,'Y':0,'Z':2},{'X':4.9,'Y':0,'Z':2},{'X':-5.1,'Y':0,'Z':4},{'X':-3.1,'Y':0,'Z':4},{'X':-1.1,'Y':0,'Z':4},{'X':0.9,'Y':0,'Z':4},{'X':2.9,'Y':0,'Z':4},{'X':4.9,'Y':0,'Z':4},{'X':-5.1,'Y':0,'Z':6},{'X':-3.1,'Y':0,'Z':6},{'X':-1.1,'Y':0,'Z':6},{'X':0.9,'Y':0,'Z':6},{'X':2.9,'Y':0,'Z':6},{'X':4.9,'Y':0,'Z':6},{'X':-5.1,'Y':0,'Z':8},{'X':-3.1,'Y':0,'Z':8},{'X':-1.1,'Y':0,'Z':8},{'X':0.9,'Y':0,'Z':8},{'X':2.9,'Y':0,'Z':8},{'X':4.9,'Y':0,'Z':8},{'X':-5.1,'Y':0,'Z':10},{'X':-3.1,'Y':0,'Z':10},{'X':-1.1,'Y':0,'Z':10},{'X':0.9,'Y':0,'Z':10},{'X':2.9,'Y':0,'Z':10},{'X':4.9,'Y':0,'Z':10}],'road_27x15':[{'X':-4.9,'Y':0,'Z':-14},{'X':-2.9,'Y':0,'Z':-14},{'X':-0.9,'Y':0,'Z':-14},{'X':1.1,'Y':0,'Z':-14},{'X':3,'Y':0,'Z':-14},{'X':5.1,'Y':0,'Z':-14},{'X':-4.9,'Y':0,'Z':-12},{'X':-2.9,'Y':0,'Z':-12},{'X':-0.9,'Y':0,'Z':-12},{'X':1.1,'Y':0,'Z':-12},{'X':3.1,'Y':0,'Z':-12},{'X':5.1,'Y':0,'Z':-12},{'X':-4.9,'Y':0,'Z':-10},{'X':-2.9,'Y':0,'Z':-10},{'X':-0.9,'Y':0,'Z':-10},{'X':1.1,'Y':0,'Z':-10},{'X':3.1,'Y':0,'Z':-10},{'X':5.1,'Y':0,'Z':-10},{'X':-4.9,'Y':0,'Z':-8},{'X':-2.9,'Y':0,'Z':-8},{'X':-0.9,'Y':0,'Z':-8},{'X':1.1,'Y':0,'Z':-8},{'X':3.1,'Y':0,'Z':-8},{'X':5.1,'Y':0,'Z':-8},{'X':-4.9,'Y':0,'Z':-6},{'X':-2.9,'Y':0,'Z':-6},{'X':-0.9,'Y':0,'Z':-6},{'X':1.1,'Y':0,'Z':-6},{'X':3.1,'Y':0,'Z':-6},{'X':5.1,'Y':0,'Z':-6},{'X':-4.9,'Y':0,'Z':-4},{'X':-2.9,'Y':0,'Z':-4},{'X':-0.9,'Y':0,'Z':-4},{'X':1.1,'Y':0,'Z':-4},{'X':3.1,'Y':0,'Z':-4},{'X':5.1,'Y':0,'Z':-4},{'X':-4.9,'Y':0,'Z':-2},{'X':-2.9,'Y':0,'Z':-2},{'X':-0.9,'Y':0,'Z':-2},{'X':1.1,'Y':0,'Z':-2},{'X':3.1,'Y':0,'Z':-2},{'X':5.1,'Y':0,'Z':-2},{'X':-4.9,'Y':0,'Z':0},{'X':-2.9,'Y':0,'Z':0},{'X':-0.9,'Y':0,'Z':0},{'X':1.1,'Y':0,'Z':0},{'X':3.1,'Y':0,'Z':0},{'X':5.1,'Y':0,'Z':0},{'X':-4.9,'Y':0,'Z':2},{'X':-2.9,'Y':0,'Z':2},{'X':-0.9,'Y':0,'Z':2},{'X':1.1,'Y':0,'Z':2},{'X':3.1,'Y':0,'Z':2},{'X':5.1,'Y':0,'Z':2},{'X':-4.9,'Y':0,'Z':4},{'X':-2.9,'Y':0,'Z':4},{'X':-0.9,'Y':0,'Z':4},{'X':1.1,'Y':0,'Z':4},{'X':3.1,'Y':0,'Z':4},{'X':5.1,'Y':0,'Z':4},{'X':-4.9,'Y':0,'Z':6},{'X':-2.9,'Y':0,'Z':6},{'X':-0.9,'Y':0,'Z':6},{'X':1.1,'Y':0,'Z':6},{'X':3.1,'Y':0,'Z':6},{'X':5.1,'Y':0,'Z':6},{'X':-4.9,'Y':0,'Z':8},{'X':-2.9,'Y':0,'Z':8},{'X':-0.9,'Y':0,'Z':8},{'X':1.1,'Y':0,'Z':8},{'X':3.1,'Y':0,'Z':8},{'X':5.1,'Y':0,'Z':8},{'X':-4.9,'Y':0,'Z':10},{'X':-2.9,'Y':0,'Z':10},{'X':-0.9,'Y':0,'Z':10},{'X':1.1,'Y':0,'Z':10},{'X':3.1,'Y':0,'Z':10},{'X':5.1,'Y':0,'Z':10},{'X':-4.9,'Y':0,'Z':12},{'X':-2.9,'Y':0,'Z':12},{'X':-0.9,'Y':0,'Z':12},{'X':1.1,'Y':0,'Z':12},{'X':3.1,'Y':0,'Z':12},{'X':5.1,'Y':0,'Z':12},{'X':-4.9,'Y':0,'Z':14},{'X':-2.9,'Y':0,'Z':14},{'X':-0.9,'Y':0,'Z':14},{'X':1.1,'Y':0,'Z':14},{'X':3.1,'Y':0,'Z':14},{'X':5.1,'Y':0,'Z':14}],'road_36x15':[{'X':-5,'Y':0,'Z':-18.9},{'X':-3,'Y':0,'Z':-18.9},{'X':-1,'Y':0,'Z':-18.9},{'X':1,'Y':0,'Z':-18.9},{'X':3,'Y':0,'Z':-18.9},{'X':5,'Y':0,'Z':-18.9},{'X':-5,'Y':0,'Z':-16.9},{'X':-3,'Y':0,'Z':-16.9},{'X':-1,'Y':0,'Z':-16.9},{'X':1,'Y':0,'Z':-16.9},{'X':3,'Y':0,'Z':-16.9},{'X':5,'Y':0,'Z':-16.9},{'X':-5,'Y':0,'Z':-14.9},{'X':-3,'Y':0,'Z':-14.9},{'X':-1,'Y':0,'Z':-14.9},{'X':1,'Y':0,'Z':-14.9},{'X':3,'Y':0,'Z':-14.9},{'X':5,'Y':0,'Z':-14.9},{'X':-5,'Y':0,'Z':-12.9},{'X':-3,'Y':0,'Z':-12.9},{'X':-1,'Y':0,'Z':-12.9},{'X':1,'Y':0,'Z':-12.9},{'X':3,'Y':0,'Z':-12.9},{'X':5,'Y':0,'Z':-12.9},{'X':-5,'Y':0,'Z':-10.9},{'X':-3,'Y':0,'Z':-10.9},{'X':-1,'Y':0,'Z':-10.9},{'X':1,'Y':0,'Z':-10.9},{'X':3,'Y':0,'Z':-10.9},{'X':5,'Y':0,'Z':-10.9},{'X':-5,'Y':0,'Z':-8.9},{'X':-3,'Y':0,'Z':-8.9},{'X':-1,'Y':0,'Z':-8.9},{'X':1,'Y':0,'Z':-8.9},{'X':3,'Y':0,'Z':-8.9},{'X':5,'Y':0,'Z':-8.9},{'X':-5,'Y':0,'Z':-6.9},{'X':-3,'Y':0,'Z':-6.9},{'X':-1,'Y':0,'Z':-6.9},{'X':1,'Y':0,'Z':-6.9},{'X':3,'Y':0,'Z':-6.9},{'X':5,'Y':0,'Z':-6.9},{'X':-5,'Y':0,'Z':-4.9},{'X':-3,'Y':0,'Z':-4.9},{'X':-1,'Y':0,'Z':-4.9},{'X':1,'Y':0,'Z':-4.9},{'X':3,'Y':0,'Z':-4.9},{'X':5,'Y':0,'Z':-4.9},{'X':-5,'Y':0,'Z':-2.9},{'X':-3,'Y':0,'Z':-2.9},{'X':-1,'Y':0,'Z':-2.9},{'X':1,'Y':0,'Z':-2.9},{'X':3,'Y':0,'Z':-2.9},{'X':5,'Y':0,'Z':-2.9},{'X':-5,'Y':0,'Z':-0.9},{'X':-3,'Y':0,'Z':-0.9},{'X':-1,'Y':0,'Z':-0.9},{'X':1,'Y':0,'Z':-0.9},{'X':3,'Y':0,'Z':-0.9},{'X':5,'Y':0,'Z':-0.9},{'X':-5,'Y':0,'Z':1.1},{'X':-3,'Y':0,'Z':1.1},{'X':-1,'Y':0,'Z':1.1},{'X':1,'Y':0,'Z':1.1},{'X':3,'Y':0,'Z':1.1},{'X':5,'Y':0,'Z':1.1},{'X':-5,'Y':0,'Z':3.1},{'X':-3,'Y':0,'Z':3.1},{'X':-1,'Y':0,'Z':3.1},{'X':1,'Y':0,'Z':3.1},{'X':3,'Y':0,'Z':3.1},{'X':5,'Y':0,'Z':3.1},{'X':-5,'Y':0,'Z':5.1},{'X':-3,'Y':0,'Z':5.1},{'X':-1,'Y':0,'Z':5.1},{'X':1,'Y':0,'Z':5.1},{'X':3,'Y':0,'Z':5.1},{'X':5,'Y':0,'Z':5.1},{'X':-5,'Y':0,'Z':7.1},{'X':-3,'Y':0,'Z':7.1},{'X':-1,'Y':0,'Z':7.1},{'X':1,'Y':0,'Z':7.1},{'X':3,'Y':0,'Z':7.1},{'X':5,'Y':0,'Z':7.1},{'X':-5,'Y':0,'Z':9.1},{'X':-3,'Y':0,'Z':9.1},{'X':-1,'Y':0,'Z':9.1},{'X':1,'Y':0,'Z':9.1},{'X':3,'Y':0,'Z':9.1},{'X':5,'Y':0,'Z':9.1},{'X':-5,'Y':0,'Z':11.1},{'X':-3,'Y':0,'Z':11.1},{'X':-1,'Y':0,'Z':11.1},{'X':1,'Y':0,'Z':11.1},{'X':3,'Y':0,'Z':11.1},{'X':5,'Y':0,'Z':11.1},{'X':-5,'Y':0,'Z':13.1},{'X':-3,'Y':0,'Z':13.1},{'X':-1,'Y':0,'Z':13.1},{'X':1,'Y':0,'Z':13.1},{'X':3,'Y':0,'Z':13.1},{'X':5,'Y':0,'Z':13.1},{'X':-5,'Y':0,'Z':15.1},{'X':-3,'Y':0,'Z':15.1},{'X':-1,'Y':0,'Z':15.1},{'X':1,'Y':0,'Z':15.1},{'X':3,'Y':0,'Z':15.1},{'X':5,'Y':0,'Z':15.1},{'X':-5,'Y':0,'Z':17.1},{'X':-3,'Y':0,'Z':17.1},{'X':-1,'Y':0,'Z':17.1},{'X':1,'Y':0,'Z':17.1},{'X':3,'Y':0,'Z':17.1},{'X':5,'Y':0,'Z':17.1}],'road_9x15':[{'X':-4.9,'Y':0,'Z':-5.5},{'X':-2.9,'Y':0,'Z':-5.5},{'X':-0.9,'Y':0,'Z':-5.5},{'X':1.1,'Y':0,'Z':-5.5},{'X':3.1,'Y':0,'Z':-5.5},{'X':5.1,'Y':0,'Z':-5.5},{'X':-4.9,'Y':0,'Z':-3.5},{'X':-2.9,'Y':0,'Z':-3.5},{'X':-0.9,'Y':0,'Z':-3.5},{'X':1.1,'Y':0,'Z':-3.5},{'X':3.1,'Y':0,'Z':-3.5},{'X':5.1,'Y':0,'Z':-3.5},{'X':-4.9,'Y':0,'Z':-1.5},{'X':-2.9,'Y':0,'Z':-1.5},{'X':-0.9,'Y':0,'Z':-1.5},{'X':1.1,'Y':0,'Z':-1.5},{'X':3.1,'Y':0,'Z':-1.5},{'X':5.1,'Y':0,'Z':-1.5},{'X':-4.9,'Y':0,'Z':0.5},{'X':-2.9,'Y':0,'Z':0.5},{'X':-0.9,'Y':0,'Z':0.5},{'X':1.1,'Y':0,'Z':0.5},{'X':3.1,'Y':0,'Z':0.5},{'X':5.1,'Y':0,'Z':0.5},{'X':-4.9,'Y':0,'Z':2.5},{'X':-2.9,'Y':0,'Z':2.5},{'X':-0.9,'Y':0,'Z':2.5},{'X':1.1,'Y':0,'Z':2.5},{'X':3.1,'Y':0,'Z':2.5},{'X':5.1,'Y':0,'Z':2.5},{'X':-4.9,'Y':0,'Z':4.5},{'X':-2.9,'Y':0,'Z':4.5},{'X':-0.9,'Y':0,'Z':4.5},{'X':1.1,'Y':0,'Z':4.5},{'X':3.1,'Y':0,'Z':4.5},{'X':5.1,'Y':0,'Z':4.5}],'road_9x15_end':[{'X':-5,'Y':0,'Z':-9.6},{'X':-3,'Y':0,'Z':-9.6},{'X':-1,'Y':0,'Z':-9.6},{'X':1,'Y':0,'Z':-9.6},{'X':3,'Y':0,'Z':-9.6},{'X':5,'Y':0,'Z':-9.6},{'X':-5,'Y':0,'Z':-7.6},{'X':-3,'Y':0,'Z':-7.6},{'X':-1,'Y':0,'Z':-7.6},{'X':1,'Y':0,'Z':-7.6},{'X':3,'Y':0,'Z':-7.6},{'X':5,'Y':0,'Z':-7.6},{'X':-5,'Y':0,'Z':-5.6},{'X':-3,'Y':0,'Z':-5.6},{'X':-1,'Y':0,'Z':-5.6},{'X':1,'Y':0,'Z':-5.6},{'X':3,'Y':0,'Z':-5.6},{'X':5,'Y':0,'Z':-5.6},{'X':-5,'Y':0,'Z':-3.6},{'X':-3,'Y':0,'Z':-3.6},{'X':-1,'Y':0,'Z':-3.6},{'X':1,'Y':0,'Z':-3.6},{'X':3,'Y':0,'Z':-3.6},{'X':5,'Y':0,'Z':-3.6},{'X':-5,'Y':0,'Z':-1.6},{'X':-3,'Y':0,'Z':-1.6},{'X':-1,'Y':0,'Z':-1.6},{'X':1,'Y':0,'Z':-1.6},{'X':3,'Y':0,'Z':-1.6},{'X':5,'Y':0,'Z':-1.6},{'X':-5,'Y':0,'Z':0.4},{'X':-3,'Y':0,'Z':0.4},{'X':-1,'Y':0,'Z':0.4},{'X':1,'Y':0,'Z':0.4},{'X':3,'Y':0,'Z':0.4},{'X':5,'Y':0,'Z':0.4},{'X':-5,'Y':0,'Z':2.4},{'X':-3,'Y':0,'Z':2.4},{'X':-1,'Y':0,'Z':2.4},{'X':1,'Y':0,'Z':2.4},{'X':3,'Y':0,'Z':2.4},{'X':5,'Y':0,'Z':2.4},{'X':-5,'Y':0,'Z':4.4},{'X':-3,'Y':0,'Z':4.4},{'X':-1,'Y':0,'Z':4.4},{'X':1,'Y':0,'Z':4.4},{'X':3,'Y':0,'Z':4.4},{'X':5,'Y':0,'Z':4.4}],'road_9x15_railway':[{'X':-5,'Y':0,'Z':-5.1},{'X':-3,'Y':0,'Z':-5.1},{'X':-1,'Y':0,'Z':-5.1},{'X':1,'Y':0,'Z':-5.1},{'X':3,'Y':0,'Z':-5.1},{'X':5,'Y':0,'Z':-5.1},{'X':-5,'Y':0,'Z':-3.1},{'X':-3,'Y':0,'Z':-3.1},{'X':-1,'Y':0,'Z':-3.1},{'X':1,'Y':0,'Z':-3.1},{'X':3,'Y':0,'Z':-3.1},{'X':5,'Y':0,'Z':-3.1},{'X':-5,'Y':0,'Z':-1.1},{'X':-3,'Y':0,'Z':-1.1},{'X':-1,'Y':0,'Z':-1.1},{'X':1,'Y':0,'Z':-1.1},{'X':3,'Y':0,'Z':-1.1},{'X':5,'Y':0,'Z':-1.1},{'X':-5,'Y':0,'Z':0.9},{'X':-3,'Y':0,'Z':0.9},{'X':-1,'Y':0,'Z':0.9},{'X':1,'Y':0,'Z':0.9},{'X':3,'Y':0,'Z':0.9},{'X':5,'Y':0,'Z':0.9},{'X':-5,'Y':0,'Z':2.9},{'X':-3,'Y':0,'Z':2.9},{'X':-1,'Y':0,'Z':2.9},{'X':1,'Y':0,'Z':2.9},{'X':3,'Y':0,'Z':2.9},{'X':5,'Y':0,'Z':2.9},{'X':-5,'Y':0,'Z':4.9},{'X':-3,'Y':0,'Z':4.9},{'X':-1,'Y':0,'Z':4.9},{'X':1,'Y':0,'Z':4.9},{'X':3,'Y':0,'Z':4.9},{'X':5,'Y':0,'Z':4.9}],'road_9x18_broken':[{'X':-4.9,'Y':0,'Z':-9.1},{'X':-2.9,'Y':0,'Z':-9.1},{'X':-0.9,'Y':0,'Z':-9.1},{'X':1.1,'Y':0,'Z':-9.1},{'X':3.1,'Y':0,'Z':-9.1},{'X':5.1,'Y':0,'Z':-9.1},{'X':-4.9,'Y':0,'Z':-7.1},{'X':-2.9,'Y':0,'Z':-7.1},{'X':-0.9,'Y':0,'Z':-7.1},{'X':1.1,'Y':0,'Z':-7.1},{'X':3.1,'Y':0,'Z':-7.1},{'X':5.1,'Y':0,'Z':-7.1},{'X':-4.9,'Y':0,'Z':-5.1},{'X':-2.9,'Y':0,'Z':-5.1},{'X':-0.9,'Y':0,'Z':-5.1},{'X':1.1,'Y':0,'Z':-5.1},{'X':3.1,'Y':0,'Z':-5.1},{'X':5.1,'Y':0,'Z':-5.1},{'X':-4.9,'Y':0,'Z':-3.1},{'X':-2.9,'Y':0,'Z':-3.1},{'X':-0.9,'Y':0,'Z':-3.1},{'X':1.1,'Y':0,'Z':-3.1},{'X':3.1,'Y':0,'Z':-3.1},{'X':5.1,'Y':0,'Z':-3.1},{'X':-4.9,'Y':0,'Z':-1.1},{'X':-2.9,'Y':0,'Z':-1.1},{'X':-0.9,'Y':0,'Z':-1.1},{'X':1.1,'Y':0,'Z':-1.1},{'X':3.1,'Y':0,'Z':-1.1},{'X':5.1,'Y':0,'Z':-1.1},{'X':-4.9,'Y':0,'Z':0.9},{'X':-2.9,'Y':0,'Z':0.9},{'X':-0.9,'Y':0,'Z':0.9},{'X':1.1,'Y':0,'Z':0.9},{'X':3.1,'Y':0,'Z':0.9},{'X':5.1,'Y':0,'Z':0.9},{'X':-4.9,'Y':0,'Z':2.9},{'X':-2.9,'Y':0,'Z':2.9},{'X':-0.9,'Y':0,'Z':2.9},{'X':1.1,'Y':0,'Z':2.9},{'X':3.1,'Y':0,'Z':2.9},{'X':5.1,'Y':0,'Z':2.9},{'X':-4.9,'Y':0,'Z':4.9},{'X':-2.9,'Y':0,'Z':4.9},{'X':-0.9,'Y':0,'Z':4.9},{'X':1.1,'Y':0,'Z':4.9},{'X':3.1,'Y':0,'Z':4.9},{'X':5.1,'Y':0,'Z':4.9},{'X':-4.9,'Y':0,'Z':6.9},{'X':-2.9,'Y':0,'Z':6.9},{'X':-0.9,'Y':0,'Z':6.9},{'X':1.1,'Y':0,'Z':6.9},{'X':3.1,'Y':0,'Z':6.9},{'X':5.1,'Y':0,'Z':6.9},{'X':-4.9,'Y':0,'Z':8.9},{'X':-2.9,'Y':0,'Z':8.9},{'X':-0.9,'Y':0,'Z':8.9},{'X':1.1,'Y':0,'Z':8.9},{'X':3.1,'Y':0,'Z':8.9},{'X':5.1,'Y':0,'Z':8.9}],'road_bend_15x15':[{'X':3,'Y':0,'Z':-1},{'X':5,'Y':0,'Z':-1},{'X':7,'Y':0,'Z':-1},{'X':9,'Y':0,'Z':-1},{'X':11,'Y':0,'Z':-1},{'X':13,'Y':0,'Z':-1},{'X':3,'Y':0,'Z':1},{'X':5,'Y':0,'Z':1},{'X':7,'Y':0,'Z':1},{'X':9,'Y':0,'Z':1},{'X':11,'Y':0,'Z':1},{'X':13,'Y':0,'Z':1},{'X':-1,'Y':0,'Z':3},{'X':1,'Y':0,'Z':3},{'X':3,'Y':0,'Z':3},{'X':5,'Y':0,'Z':3},{'X':7,'Y':0,'Z':3},{'X':9,'Y':0,'Z':3},{'X':11,'Y':0,'Z':3},{'X':13,'Y':0,'Z':3},{'X':-1,'Y':0,'Z':5},{'X':1,'Y':0,'Z':5},{'X':3,'Y':0,'Z':5},{'X':5,'Y':0,'Z':5},{'X':7,'Y':0,'Z':5},{'X':9,'Y':0,'Z':5},{'X':11,'Y':0,'Z':5},{'X':-1,'Y':0,'Z':7},{'X':1,'Y':0,'Z':7},{'X':3,'Y':0,'Z':7},{'X':5,'Y':0,'Z':7},{'X':7,'Y':0,'Z':7},{'X':9,'Y':0,'Z':7},{'X':11,'Y':0,'Z':7},{'X':-1,'Y':0,'Z':9},{'X':1,'Y':0,'Z':9},{'X':3,'Y':0,'Z':9},{'X':5,'Y':0,'Z':9},{'X':7,'Y':0,'Z':9},{'X':9,'Y':0,'Z':9},{'X':-1,'Y':0,'Z':11},{'X':1,'Y':0,'Z':11},{'X':3,'Y':0,'Z':11},{'X':5,'Y':0,'Z':11},{'X':7,'Y':0,'Z':11},{'X':-1,'Y':0,'Z':13},{'X':1,'Y':0,'Z':13},{'X':3,'Y':0,'Z':13}],'road_junction_15x15':[{'X':-4,'Y':0,'Z':-8},{'X':-2,'Y':0,'Z':-8},{'X':0,'Y':0,'Z':-8},{'X':2,'Y':0,'Z':-8},{'X':4,'Y':0,'Z':-8},{'X':-4,'Y':0,'Z':-6},{'X':-2,'Y':0,'Z':-6},{'X':0,'Y':0,'Z':-6},{'X':2,'Y':0,'Z':-6},{'X':4,'Y':0,'Z':-6},{'X':-8,'Y':0,'Z':-4},{'X':-6,'Y':0,'Z':-4},{'X':-4,'Y':0,'Z':-4},{'X':-2,'Y':0,'Z':-4},{'X':0,'Y':0,'Z':-4},{'X':2,'Y':0,'Z':-4},{'X':4,'Y':0,'Z':-4},{'X':6,'Y':0,'Z':-4},{'X':8,'Y':0,'Z':-4},{'X':-8,'Y':0,'Z':-2},{'X':-6,'Y':0,'Z':-2},{'X':-4,'Y':0,'Z':-2},{'X':-2,'Y':0,'Z':-2},{'X':0,'Y':0,'Z':-2},{'X':2,'Y':0,'Z':-2},{'X':4,'Y':0,'Z':-2},{'X':6,'Y':0,'Z':-2},{'X':8,'Y':0,'Z':-2},{'X':-8,'Y':0,'Z':0},{'X':-6,'Y':0,'Z':0},{'X':-4,'Y':0,'Z':0},{'X':-2,'Y':0,'Z':0},{'X':0,'Y':0,'Z':0},{'X':2,'Y':0,'Z':0},{'X':4,'Y':0,'Z':0},{'X':6,'Y':0,'Z':0},{'X':8,'Y':0,'Z':0},{'X':-8,'Y':0,'Z':2},{'X':-6,'Y':0,'Z':2},{'X':-4,'Y':0,'Z':2},{'X':-2,'Y':0,'Z':2},{'X':0,'Y':0,'Z':2},{'X':2,'Y':0,'Z':2},{'X':4,'Y':0,'Z':2},{'X':6,'Y':0,'Z':2},{'X':8,'Y':0,'Z':2},{'X':-8,'Y':0,'Z':4},{'X':-6,'Y':0,'Z':4},{'X':-4,'Y':0,'Z':4},{'X':-2,'Y':0,'Z':4},{'X':0,'Y':0,'Z':4},{'X':2,'Y':0,'Z':4},{'X':4,'Y':0,'Z':4},{'X':6,'Y':0,'Z':4},{'X':8,'Y':0,'Z':4},{'X':-4,'Y':0,'Z':6},{'X':-2,'Y':0,'Z':6},{'X':0,'Y':0,'Z':6},{'X':2,'Y':0,'Z':6},{'X':4,'Y':0,'Z':6},{'X':-4,'Y':0,'Z':8},{'X':-2,'Y':0,'Z':8},{'X':0,'Y':0,'Z':8},{'X':2,'Y':0,'Z':8},{'X':4,'Y':0,'Z':8}],'road_junctiont_15x15':[{'X':-4.5,'Y':0,'Z':-8.5},{'X':-2.5,'Y':0,'Z':-8.5},{'X':-0.5,'Y':0,'Z':-8.5},{'X':1.5,'Y':0,'Z':-8.5},{'X':3.5,'Y':0,'Z':-8.5},{'X':-4.5,'Y':0,'Z':-6.5},{'X':-2.5,'Y':0,'Z':-6.5},{'X':-0.5,'Y':0,'Z':-6.5},{'X':1.5,'Y':0,'Z':-6.5},{'X':3.5,'Y':0,'Z':-6.5},{'X':-8.5,'Y':0,'Z':-4.5},{'X':-6.5,'Y':0,'Z':-4.5},{'X':-4.5,'Y':0,'Z':-4.5},{'X':-2.5,'Y':0,'Z':-4.5},{'X':-0.5,'Y':0,'Z':-4.5},{'X':1.5,'Y':0,'Z':-4.5},{'X':3.5,'Y':0,'Z':-4.5},{'X':5.5,'Y':0,'Z':-4.5},{'X':7.5,'Y':0,'Z':-4.5},{'X':-8.5,'Y':0,'Z':-2.5},{'X':-6.5,'Y':0,'Z':-2.5},{'X':-4.5,'Y':0,'Z':-2.5},{'X':-2.5,'Y':0,'Z':-2.5},{'X':-0.5,'Y':0,'Z':-2.5},{'X':1.5,'Y':0,'Z':-2.5},{'X':3.5,'Y':0,'Z':-2.5},{'X':5.5,'Y':0,'Z':-2.5},{'X':7.5,'Y':0,'Z':-2.5},{'X':-8.5,'Y':0,'Z':-0.5},{'X':-6.5,'Y':0,'Z':-0.5},{'X':-4.5,'Y':0,'Z':-0.5},{'X':-2.5,'Y':0,'Z':-0.5},{'X':-0.5,'Y':0,'Z':-0.5},{'X':1.5,'Y':0,'Z':-0.5},{'X':3.5,'Y':0,'Z':-0.5},{'X':5.5,'Y':0,'Z':-0.5},{'X':7.5,'Y':0,'Z':-0.5},{'X':-8.5,'Y':0,'Z':1.5},{'X':-6.5,'Y':0,'Z':1.5},{'X':-4.5,'Y':0,'Z':1.5},{'X':-2.5,'Y':0,'Z':1.5},{'X':-0.5,'Y':0,'Z':1.5},{'X':1.5,'Y':0,'Z':1.5},{'X':3.5,'Y':0,'Z':1.5},{'X':5.5,'Y':0,'Z':1.5},{'X':7.5,'Y':0,'Z':1.5},{'X':-8.5,'Y':0,'Z':3.5},{'X':-6.5,'Y':0,'Z':3.5},{'X':-4.5,'Y':0,'Z':3.5},{'X':-2.5,'Y':0,'Z':3.5},{'X':-0.5,'Y':0,'Z':3.5},{'X':1.5,'Y':0,'Z':3.5},{'X':3.5,'Y':0,'Z':3.5},{'X':5.5,'Y':0,'Z':3.5},{'X':7.5,'Y':0,'Z':3.5},{'X':-8.5,'Y':0,'Z':5.5},{'X':-6.5,'Y':0,'Z':5.5},{'X':-4.5,'Y':0,'Z':5.5},{'X':-2.5,'Y':0,'Z':5.5},{'X':-0.5,'Y':0,'Z':5.5},{'X':1.5,'Y':0,'Z':5.5},{'X':3.5,'Y':0,'Z':5.5},{'X':5.5,'Y':0,'Z':5.5},{'X':7.5,'Y':0,'Z':5.5}],'road_nopavement_18x15':[{'X':-5.1,'Y':0,'Z':-10},{'X':-3,'Y':0,'Z':-10},{'X':-1,'Y':0,'Z':-10},{'X':1,'Y':0,'Z':-10},{'X':3,'Y':0,'Z':-10},{'X':-5,'Y':0,'Z':-8},{'X':-3,'Y':0,'Z':-8},{'X':-1,'Y':0,'Z':-8},{'X':1,'Y':0,'Z':-8},{'X':3,'Y':0,'Z':-8},{'X':-5,'Y':0,'Z':-6},{'X':-3,'Y':0,'Z':-6},{'X':-1,'Y':0,'Z':-6},{'X':1,'Y':0,'Z':-6},{'X':3,'Y':0,'Z':-6},{'X':-5,'Y':0,'Z':-4},{'X':-3,'Y':0,'Z':-4},{'X':-1,'Y':0,'Z':-4},{'X':1,'Y':0,'Z':-4},{'X':3,'Y':0,'Z':-4},{'X':-5,'Y':0,'Z':-2},{'X':-3,'Y':0,'Z':-2},{'X':-1,'Y':0,'Z':-2},{'X':1,'Y':0,'Z':-2},{'X':3,'Y':0,'Z':-2},{'X':-5,'Y':0,'Z':0},{'X':-3,'Y':0,'Z':0},{'X':-1,'Y':0,'Z':0},{'X':1,'Y':0,'Z':0},{'X':3,'Y':0,'Z':0},{'X':-5,'Y':0,'Z':2},{'X':-3,'Y':0,'Z':2},{'X':-1,'Y':0,'Z':2},{'X':1,'Y':0,'Z':2},{'X':3,'Y':0,'Z':2},{'X':-5,'Y':0,'Z':4},{'X':-3,'Y':0,'Z':4},{'X':-1,'Y':0,'Z':4},{'X':1,'Y':0,'Z':4},{'X':3,'Y':0,'Z':4},{'X':-5,'Y':0,'Z':6},{'X':-3,'Y':0,'Z':6},{'X':-1,'Y':0,'Z':6},{'X':1,'Y':0,'Z':6},{'X':3,'Y':0,'Z':6},{'X':-5,'Y':0,'Z':8},{'X':-3,'Y':0,'Z':8},{'X':-1,'Y':0,'Z':8},{'X':1,'Y':0,'Z':8},{'X':3,'Y':0,'Z':8},{'X':-5,'Y':0,'Z':10},{'X':-3,'Y':0,'Z':10},{'X':-1,'Y':0,'Z':10},{'X':1,'Y':0,'Z':10},{'X':3,'Y':0,'Z':10},{'X':5,'Y':0,'Z':-10},{'X':5,'Y':0,'Z':-8},{'X':5,'Y':0,'Z':-6},{'X':5,'Y':0,'Z':-4},{'X':5,'Y':0,'Z':-2},{'X':5,'Y':0,'Z':0},{'X':5,'Y':0,'Z':2},{'X':5,'Y':0,'Z':4},{'X':5,'Y':0,'Z':6},{'X':5,'Y':0,'Z':8},{'X':5,'Y':0,'Z':10}],'road_nopavement_27x15':[{'X':-3,'Y':0,'Z':-13.3},{'X':-1,'Y':0,'Z':-13.3},{'X':1,'Y':0,'Z':-13.3},{'X':3,'Y':0,'Z':-13.3},{'X':5,'Y':0,'Z':-13.3},{'X':-3,'Y':0,'Z':-11.3},{'X':-1,'Y':0,'Z':-11.3},{'X':1,'Y':0,'Z':-11.3},{'X':3,'Y':0,'Z':-11.3},{'X':5,'Y':0,'Z':-11.3},{'X':-3,'Y':0,'Z':-9.3},{'X':-1,'Y':0,'Z':-9.3},{'X':1,'Y':0,'Z':-9.3},{'X':3,'Y':0,'Z':-9.3},{'X':5,'Y':0,'Z':-9.3},{'X':-3,'Y':0,'Z':-7.3},{'X':-1,'Y':0,'Z':-7.3},{'X':1,'Y':0,'Z':-7.3},{'X':3,'Y':0,'Z':-7.3},{'X':5,'Y':0,'Z':-7.3},{'X':-3,'Y':0,'Z':-5.3},{'X':-1,'Y':0,'Z':-5.3},{'X':1,'Y':0,'Z':-5.3},{'X':3,'Y':0,'Z':-5.3},{'X':5,'Y':0,'Z':-5.3},{'X':-3,'Y':0,'Z':-3.3},{'X':-1,'Y':0,'Z':-3.3},{'X':1,'Y':0,'Z':-3.3},{'X':3,'Y':0,'Z':-3.3},{'X':5,'Y':0,'Z':-3.3},{'X':-3,'Y':0,'Z':-1.3},{'X':-1,'Y':0,'Z':-1.3},{'X':1,'Y':0,'Z':-1.3},{'X':3,'Y':0,'Z':-1.3},{'X':5,'Y':0,'Z':-1.3},{'X':-3,'Y':0,'Z':0.7},{'X':-1,'Y':0,'Z':0.7},{'X':1,'Y':0,'Z':0.7},{'X':3,'Y':0,'Z':0.7},{'X':5,'Y':0,'Z':0.7},{'X':-3,'Y':0,'Z':2.7},{'X':-1,'Y':0,'Z':2.7},{'X':1,'Y':0,'Z':2.7},{'X':3,'Y':0,'Z':2.7},{'X':5,'Y':0,'Z':2.7},{'X':-3,'Y':0,'Z':4.7},{'X':-1,'Y':0,'Z':4.7},{'X':1,'Y':0,'Z':4.7},{'X':3,'Y':0,'Z':4.7},{'X':5,'Y':0,'Z':4.7},{'X':-3,'Y':0,'Z':6.7},{'X':-1,'Y':0,'Z':6.7},{'X':1,'Y':0,'Z':6.7},{'X':3,'Y':0,'Z':6.7},{'X':5,'Y':0,'Z':6.7},{'X':-3,'Y':0,'Z':8.7},{'X':-1,'Y':0,'Z':8.7},{'X':1,'Y':0,'Z':8.7},{'X':3,'Y':0,'Z':8.7},{'X':5,'Y':0,'Z':8.7},{'X':-3,'Y':0,'Z':10.7},{'X':-1,'Y':0,'Z':10.7},{'X':1,'Y':0,'Z':10.7},{'X':3,'Y':0,'Z':10.7},{'X':5,'Y':0,'Z':10.7},{'X':-3,'Y':0,'Z':12.7},{'X':-1,'Y':0,'Z':12.7},{'X':1,'Y':0,'Z':12.7},{'X':3,'Y':0,'Z':12.7},{'X':5,'Y':0,'Z':12.7},{'X':-3,'Y':0,'Z':14.7},{'X':-1,'Y':0,'Z':14.7},{'X':1,'Y':0,'Z':14.7},{'X':3,'Y':0,'Z':14.7},{'X':5,'Y':0,'Z':14.7},{'X':-5,'Y':0,'Z':-13.3},{'X':-5,'Y':0,'Z':-11.3},{'X':-5,'Y':0,'Z':-9.3},{'X':-5,'Y':0,'Z':-7.3},{'X':-5,'Y':0,'Z':-5.3},{'X':-5,'Y':0,'Z':-3.3},{'X':-5,'Y':0,'Z':-1.3},{'X':-5,'Y':0,'Z':0.7},{'X':-5,'Y':0,'Z':2.7},{'X':-5,'Y':0,'Z':4.7},{'X':-5,'Y':0,'Z':6.7},{'X':-5,'Y':0,'Z':8.7},{'X':-5,'Y':0,'Z':10.7},{'X':-5,'Y':0,'Z':12.7},{'X':-5,'Y':0,'Z':14.7}],'road_nopavement_36x15':[{'X':-5.1,'Y':0,'Z':-18.9},{'X':-3.1,'Y':0,'Z':-18.9},{'X':-1.1,'Y':0,'Z':-18.9},{'X':0.9,'Y':0,'Z':-18.9},{'X':2.9,'Y':0,'Z':-18.9},{'X':4.9,'Y':0,'Z':-18.9},{'X':-5.1,'Y':0,'Z':-16.9},{'X':-3.1,'Y':0,'Z':-16.9},{'X':-1.1,'Y':0,'Z':-16.9},{'X':0.9,'Y':0,'Z':-16.9},{'X':2.9,'Y':0,'Z':-16.9},{'X':4.9,'Y':0,'Z':-16.9},{'X':-5.1,'Y':0,'Z':-14.9},{'X':-3.1,'Y':0,'Z':-14.9},{'X':-1.1,'Y':0,'Z':-14.9},{'X':0.9,'Y':0,'Z':-14.9},{'X':2.9,'Y':0,'Z':-14.9},{'X':4.9,'Y':0,'Z':-14.9},{'X':-5.1,'Y':0,'Z':-12.9},{'X':-3.1,'Y':0,'Z':-12.9},{'X':-1.1,'Y':0,'Z':-12.9},{'X':0.9,'Y':0,'Z':-12.9},{'X':2.9,'Y':0,'Z':-12.9},{'X':4.9,'Y':0,'Z':-12.9},{'X':-5.1,'Y':0,'Z':-10.9},{'X':-3.1,'Y':0,'Z':-10.9},{'X':-1.1,'Y':0,'Z':-10.9},{'X':0.9,'Y':0,'Z':-10.9},{'X':2.9,'Y':0,'Z':-10.9},{'X':4.9,'Y':0,'Z':-10.9},{'X':-5.1,'Y':0,'Z':-8.9},{'X':-3.1,'Y':0,'Z':-8.9},{'X':-1.1,'Y':0,'Z':-8.9},{'X':0.9,'Y':0,'Z':-8.9},{'X':2.9,'Y':0,'Z':-8.9},{'X':4.9,'Y':0,'Z':-8.9},{'X':-5.1,'Y':0,'Z':-6.9},{'X':-3.1,'Y':0,'Z':-6.9},{'X':-1.1,'Y':0,'Z':-6.9},{'X':0.9,'Y':0,'Z':-6.9},{'X':2.9,'Y':0,'Z':-6.9},{'X':4.9,'Y':0,'Z':-6.9},{'X':-5.1,'Y':0,'Z':-4.9},{'X':-3.1,'Y':0,'Z':-4.9},{'X':-1.1,'Y':0,'Z':-4.9},{'X':0.9,'Y':0,'Z':-4.9},{'X':2.9,'Y':0,'Z':-4.9},{'X':4.9,'Y':0,'Z':-4.9},{'X':-5.1,'Y':0,'Z':-2.9},{'X':-3.1,'Y':0,'Z':-2.9},{'X':-1.1,'Y':0,'Z':-2.9},{'X':0.9,'Y':0,'Z':-2.9},{'X':2.9,'Y':0,'Z':-2.9},{'X':4.9,'Y':0,'Z':-2.9},{'X':-5.1,'Y':0,'Z':-0.9},{'X':-3.1,'Y':0,'Z':-0.9},{'X':-1.1,'Y':0,'Z':-0.9},{'X':0.9,'Y':0,'Z':-0.9},{'X':2.9,'Y':0,'Z':-0.9},{'X':4.9,'Y':0,'Z':-0.9},{'X':-5.1,'Y':0,'Z':1.1},{'X':-3.1,'Y':0,'Z':1.1},{'X':-1.1,'Y':0,'Z':1.1},{'X':0.9,'Y':0,'Z':1.1},{'X':2.9,'Y':0,'Z':1.1},{'X':4.9,'Y':0,'Z':1.1},{'X':-5.1,'Y':0,'Z':3.1},{'X':-3.1,'Y':0,'Z':3.1},{'X':-1.1,'Y':0,'Z':3.1},{'X':0.9,'Y':0,'Z':3.1},{'X':2.9,'Y':0,'Z':3.1},{'X':4.9,'Y':0,'Z':3.1},{'X':-5.1,'Y':0,'Z':5.1},{'X':-3.1,'Y':0,'Z':5.1},{'X':-1.1,'Y':0,'Z':5.1},{'X':0.9,'Y':0,'Z':5.1},{'X':2.9,'Y':0,'Z':5.1},{'X':4.9,'Y':0,'Z':5.1},{'X':-5.1,'Y':0,'Z':7.1},{'X':-3.1,'Y':0,'Z':7.1},{'X':-1.1,'Y':0,'Z':7.1},{'X':0.9,'Y':0,'Z':7.1},{'X':2.9,'Y':0,'Z':7.1},{'X':4.9,'Y':0,'Z':7.1},{'X':-5.1,'Y':0,'Z':9.1},{'X':-3.1,'Y':0,'Z':9.1},{'X':-1.1,'Y':0,'Z':9.1},{'X':0.9,'Y':0,'Z':9.1},{'X':2.9,'Y':0,'Z':9.1},{'X':4.9,'Y':0,'Z':9.1},{'X':-5.1,'Y':0,'Z':11.1},{'X':-3.1,'Y':0,'Z':11.1},{'X':-1.1,'Y':0,'Z':11.1},{'X':0.9,'Y':0,'Z':11.1},{'X':2.9,'Y':0,'Z':11.1},{'X':4.9,'Y':0,'Z':11.1},{'X':-5.1,'Y':0,'Z':13.1},{'X':-3.1,'Y':0,'Z':13.1},{'X':-1.1,'Y':0,'Z':13.1},{'X':0.9,'Y':0,'Z':13.1},{'X':2.9,'Y':0,'Z':13.1},{'X':4.9,'Y':0,'Z':13.1},{'X':-5.1,'Y':0,'Z':15.1},{'X':-3.1,'Y':0,'Z':15.1},{'X':-1.1,'Y':0,'Z':15.1},{'X':0.9,'Y':0,'Z':15.1},{'X':2.9,'Y':0,'Z':15.1},{'X':4.9,'Y':0,'Z':15.1},{'X':-5.1,'Y':0,'Z':17.1},{'X':-3.1,'Y':0,'Z':17.1},{'X':-1.1,'Y':0,'Z':17.1},{'X':0.9,'Y':0,'Z':17.1},{'X':2.9,'Y':0,'Z':17.1},{'X':4.9,'Y':0,'Z':17.1}],'road_nopavement_9x15':[{'X':-5.1,'Y':0,'Z':-5.5},{'X':-3.1,'Y':0,'Z':-5.5},{'X':-1.1,'Y':0,'Z':-5.5},{'X':0.9,'Y':0,'Z':-5.5},{'X':2.9,'Y':0,'Z':-5.5},{'X':4.9,'Y':0,'Z':-5.5},{'X':-5.1,'Y':0,'Z':-3.5},{'X':-3.1,'Y':0,'Z':-3.5},{'X':-1.1,'Y':0,'Z':-3.5},{'X':0.9,'Y':0,'Z':-3.5},{'X':2.9,'Y':0,'Z':-3.5},{'X':4.9,'Y':0,'Z':-3.5},{'X':-5.1,'Y':0,'Z':-1.5},{'X':-3.1,'Y':0,'Z':-1.5},{'X':-1.1,'Y':0,'Z':-1.5},{'X':0.9,'Y':0,'Z':-1.5},{'X':2.9,'Y':0,'Z':-1.5},{'X':4.9,'Y':0,'Z':-1.5},{'X':-5.1,'Y':0,'Z':0.5},{'X':-3.1,'Y':0,'Z':0.5},{'X':-1.1,'Y':0,'Z':0.5},{'X':0.9,'Y':0,'Z':0.5},{'X':2.9,'Y':0,'Z':0.5},{'X':4.9,'Y':0,'Z':0.5},{'X':-5.1,'Y':0,'Z':2.5},{'X':-3.1,'Y':0,'Z':2.5},{'X':-1.1,'Y':0,'Z':2.5},{'X':0.9,'Y':0,'Z':2.5},{'X':2.9,'Y':0,'Z':2.5},{'X':4.9,'Y':0,'Z':2.5},{'X':-5.1,'Y':0,'Z':4.5},{'X':-3.1,'Y':0,'Z':4.5},{'X':-1.1,'Y':0,'Z':4.5},{'X':0.9,'Y':0,'Z':4.5},{'X':2.9,'Y':0,'Z':4.5},{'X':4.9,'Y':0,'Z':4.5}],'road_nopavement_9x15_end':[{'X':-5.1,'Y':0,'Z':-9.6},{'X':-3.1,'Y':0,'Z':-9.6},{'X':-1.1,'Y':0,'Z':-9.6},{'X':0.9,'Y':0,'Z':-9.6},{'X':2.9,'Y':0,'Z':-9.6},{'X':4.9,'Y':0,'Z':-9.6},{'X':-5.1,'Y':0,'Z':-7.6},{'X':-3.1,'Y':0,'Z':-7.6},{'X':-1.1,'Y':0,'Z':-7.6},{'X':0.9,'Y':0,'Z':-7.6},{'X':2.9,'Y':0,'Z':-7.6},{'X':4.9,'Y':0,'Z':-7.6},{'X':-5.1,'Y':0,'Z':-5.6},{'X':-3.1,'Y':0,'Z':-5.6},{'X':-1.1,'Y':0,'Z':-5.6},{'X':0.9,'Y':0,'Z':-5.6},{'X':2.9,'Y':0,'Z':-5.6},{'X':4.9,'Y':0,'Z':-5.6},{'X':-5.1,'Y':0,'Z':-3.6},{'X':-3.1,'Y':0,'Z':-3.6},{'X':-1.1,'Y':0,'Z':-3.6},{'X':0.9,'Y':0,'Z':-3.6},{'X':2.9,'Y':0,'Z':-3.6},{'X':4.9,'Y':0,'Z':-3.6},{'X':-5.1,'Y':0,'Z':-1.6},{'X':-3.1,'Y':0,'Z':-1.6},{'X':-1.1,'Y':0,'Z':-1.6},{'X':0.9,'Y':0,'Z':-1.6},{'X':2.9,'Y':0,'Z':-1.6},{'X':4.9,'Y':0,'Z':-1.6},{'X':-5.1,'Y':0,'Z':0.4},{'X':-3.1,'Y':0,'Z':0.4},{'X':-1.1,'Y':0,'Z':0.4},{'X':0.9,'Y':0,'Z':0.4},{'X':2.9,'Y':0,'Z':0.4},{'X':4.9,'Y':0,'Z':0.4},{'X':-5.1,'Y':0,'Z':2.4},{'X':-3.1,'Y':0,'Z':2.4},{'X':-1.1,'Y':0,'Z':2.4},{'X':0.9,'Y':0,'Z':2.4},{'X':2.9,'Y':0,'Z':2.4},{'X':4.9,'Y':0,'Z':2.4},{'X':-5.1,'Y':0,'Z':4.4},{'X':-3.1,'Y':0,'Z':4.4},{'X':-1.1,'Y':0,'Z':4.4},{'X':0.9,'Y':0,'Z':4.4},{'X':2.9,'Y':0,'Z':4.4},{'X':4.9,'Y':0,'Z':4.4}],'road_nopavement_9x15_railway':[{'X':-5,'Y':0,'Z':-5.5},{'X':-3,'Y':0,'Z':-5.5},{'X':-1,'Y':0,'Z':-5.5},{'X':1,'Y':0,'Z':-5.5},{'X':3,'Y':0,'Z':-5.5},{'X':5,'Y':0,'Z':-5.5},{'X':-5,'Y':0,'Z':-3.5},{'X':-3,'Y':0,'Z':-3.5},{'X':-1,'Y':0,'Z':-3.5},{'X':1,'Y':0,'Z':-3.5},{'X':3,'Y':0,'Z':-3.5},{'X':5,'Y':0,'Z':-3.5},{'X':-5,'Y':0,'Z':-1.5},{'X':-3,'Y':0,'Z':-1.5},{'X':-1,'Y':0,'Z':-1.5},{'X':1,'Y':0,'Z':-1.5},{'X':3,'Y':0,'Z':-1.5},{'X':5,'Y':0,'Z':-1.5},{'X':-5,'Y':0,'Z':0.5},{'X':-3,'Y':0,'Z':0.5},{'X':-1,'Y':0,'Z':0.5},{'X':1,'Y':0,'Z':0.5},{'X':3,'Y':0,'Z':0.5},{'X':5,'Y':0,'Z':0.5},{'X':-5,'Y':0,'Z':2.5},{'X':-3,'Y':0,'Z':2.5},{'X':-1,'Y':0,'Z':2.5},{'X':1,'Y':0,'Z':2.5},{'X':3,'Y':0,'Z':2.5},{'X':5,'Y':0,'Z':2.5},{'X':-5,'Y':0,'Z':4.5},{'X':-3,'Y':0,'Z':4.5},{'X':-1,'Y':0,'Z':4.5},{'X':1,'Y':0,'Z':4.5},{'X':3,'Y':0,'Z':4.5},{'X':5,'Y':0,'Z':4.5}],'road_nopavement_9x36_slope_600':[{'X':-37,'Y':-5.9,'Z':-5},{'X':-35,'Y':-5.8,'Z':-5},{'X':-33,'Y':-5.5,'Z':-5},{'X':-31,'Y':-5.3,'Z':-5},{'X':-29,'Y':-4.8,'Z':-5},{'X':-27,'Y':-4.4,'Z':-5},{'X':-25,'Y':-4,'Z':-5},{'X':-23,'Y':-3.5,'Z':-5},{'X':-21,'Y':-3,'Z':-5},{'X':-19,'Y':-2.6,'Z':-5},{'X':-17,'Y':-2.2,'Z':-5},{'X':-15,'Y':-1.8,'Z':-5},{'X':-13,'Y':-1.4,'Z':-5},{'X':-11,'Y':-0.9,'Z':-5},{'X':-9,'Y':-0.5,'Z':-5},{'X':-7,'Y':-0.1,'Z':-5},{'X':-5,'Y':0,'Z':-5},{'X':-3,'Y':0,'Z':-5},{'X':-1,'Y':0,'Z':-5},{'X':1,'Y':0,'Z':-5},{'X':-37,'Y':-5.9,'Z':-3},{'X':-35,'Y':-5.8,'Z':-3},{'X':-33,'Y':-5.5,'Z':-3},{'X':-31,'Y':-5.3,'Z':-3},{'X':-29,'Y':-4.8,'Z':-3},{'X':-27,'Y':-4.4,'Z':-3},{'X':-25,'Y':-4,'Z':-3},{'X':-23,'Y':-3.5,'Z':-3},{'X':-21,'Y':-3.1,'Z':-3},{'X':-19,'Y':-2.6,'Z':-3},{'X':-17,'Y':-2.2,'Z':-3},{'X':-15,'Y':-1.8,'Z':-3},{'X':-13,'Y':-1.4,'Z':-3},{'X':-11,'Y':-0.9,'Z':-3},{'X':-9,'Y':-0.5,'Z':-3},{'X':-7,'Y':-0.1,'Z':-3},{'X':-5,'Y':0,'Z':-3},{'X':-3,'Y':0,'Z':-3},{'X':-1,'Y':0,'Z':-3},{'X':1,'Y':0,'Z':-3},{'X':-37,'Y':-5.9,'Z':-1},{'X':-35,'Y':-5.8,'Z':-1},{'X':-33,'Y':-5.5,'Z':-1},{'X':-31,'Y':-5.3,'Z':-1},{'X':-29,'Y':-4.8,'Z':-1},{'X':-27,'Y':-4.4,'Z':-1},{'X':-25,'Y':-4,'Z':-1},{'X':-23,'Y':-3.5,'Z':-1},{'X':-21,'Y':-3.1,'Z':-1},{'X':-19,'Y':-2.6,'Z':-1},{'X':-17,'Y':-2.2,'Z':-1},{'X':-15,'Y':-1.8,'Z':-1},{'X':-13,'Y':-1.4,'Z':-1},{'X':-11,'Y':-0.9,'Z':-1},{'X':-9,'Y':-0.5,'Z':-1},{'X':-7,'Y':-0.1,'Z':-1},{'X':-5,'Y':0,'Z':-1},{'X':-3,'Y':0,'Z':-1},{'X':-1,'Y':0,'Z':-1},{'X':1,'Y':0,'Z':-1},{'X':-37,'Y':-5.9,'Z':1},{'X':-35,'Y':-5.8,'Z':1},{'X':-33,'Y':-5.5,'Z':1},{'X':-31,'Y':-5.3,'Z':1},{'X':-29,'Y':-4.8,'Z':1},{'X':-27,'Y':-4.4,'Z':1},{'X':-25,'Y':-4,'Z':1},{'X':-23,'Y':-3.5,'Z':1},{'X':-21,'Y':-3.1,'Z':1},{'X':-19,'Y':-2.6,'Z':1},{'X':-17,'Y':-2.2,'Z':1},{'X':-15,'Y':-1.8,'Z':1},{'X':-13,'Y':-1.4,'Z':1},{'X':-11,'Y':-0.9,'Z':1},{'X':-9,'Y':-0.5,'Z':1},{'X':-7,'Y':-0.1,'Z':1},{'X':-5,'Y':0,'Z':1},{'X':-3,'Y':0,'Z':1},{'X':-1,'Y':0,'Z':1},{'X':1,'Y':0,'Z':1},{'X':-37,'Y':-5.9,'Z':3},{'X':-35,'Y':-5.8,'Z':3},{'X':-33,'Y':-5.5,'Z':3},{'X':-31,'Y':-5.3,'Z':3},{'X':-29,'Y':-4.8,'Z':3},{'X':-27,'Y':-4.4,'Z':3},{'X':-25,'Y':-4,'Z':3},{'X':-23,'Y':-3.5,'Z':3},{'X':-21,'Y':-3.1,'Z':3},{'X':-19,'Y':-2.6,'Z':3},{'X':-17,'Y':-2.2,'Z':3},{'X':-15,'Y':-1.8,'Z':3},{'X':-13,'Y':-1.4,'Z':3},{'X':-11,'Y':-0.9,'Z':3},{'X':-9,'Y':-0.5,'Z':3},{'X':-7,'Y':-0.1,'Z':3},{'X':-5,'Y':0,'Z':3},{'X':-3,'Y':0,'Z':3},{'X':-1,'Y':0,'Z':3},{'X':1,'Y':0,'Z':3},{'X':-37,'Y':-5.9,'Z':5},{'X':-35,'Y':-5.8,'Z':5},{'X':-33,'Y':-5.5,'Z':5},{'X':-31,'Y':-5.3,'Z':5},{'X':-29,'Y':-4.8,'Z':5},{'X':-27,'Y':-4.4,'Z':5},{'X':-25,'Y':-4,'Z':5},{'X':-23,'Y':-3.5,'Z':5},{'X':-21,'Y':-3.1,'Z':5},{'X':-19,'Y':-2.6,'Z':5},{'X':-17,'Y':-2.2,'Z':5},{'X':-15,'Y':-1.8,'Z':5},{'X':-13,'Y':-1.4,'Z':5},{'X':-11,'Y':-0.9,'Z':5},{'X':-9,'Y':-0.5,'Z':5},{'X':-7,'Y':-0.1,'Z':5},{'X':-5,'Y':0,'Z':5},{'X':-3,'Y':0,'Z':5},{'X':-1,'Y':0,'Z':5},{'X':1,'Y':0,'Z':5}],'road_nopavement_bend_15x15':[{'X':3.6,'Y':0,'Z':-0.3},{'X':5.6,'Y':0,'Z':-0.3},{'X':7.6,'Y':0,'Z':-0.3},{'X':9.6,'Y':0,'Z':-0.3},{'X':11.6,'Y':0,'Z':-0.3},{'X':1.6,'Y':0,'Z':1.7},{'X':3.6,'Y':0,'Z':1.7},{'X':5.6,'Y':0,'Z':1.7},{'X':7.6,'Y':0,'Z':1.7},{'X':9.6,'Y':0,'Z':1.7},{'X':11.6,'Y':0,'Z':1.7},{'X':-0.4,'Y':0,'Z':3.7},{'X':1.6,'Y':0,'Z':3.7},{'X':3.6,'Y':0,'Z':3.7},{'X':5.6,'Y':0,'Z':3.7},{'X':7.6,'Y':0,'Z':3.7},{'X':9.6,'Y':0,'Z':3.7},{'X':11.6,'Y':0,'Z':3.7},{'X':-0.4,'Y':0,'Z':5.7},{'X':1.6,'Y':0,'Z':5.7},{'X':3.6,'Y':0,'Z':5.7},{'X':5.6,'Y':0,'Z':5.7},{'X':7.6,'Y':0,'Z':5.7},{'X':9.6,'Y':0,'Z':5.7},{'X':11.6,'Y':0,'Z':5.7},{'X':-0.4,'Y':0,'Z':7.7},{'X':1.6,'Y':0,'Z':7.7},{'X':3.6,'Y':0,'Z':7.7},{'X':5.6,'Y':0,'Z':7.7},{'X':7.6,'Y':0,'Z':7.7},{'X':9.6,'Y':0,'Z':7.7},{'X':-0.4,'Y':0,'Z':9.7},{'X':1.6,'Y':0,'Z':9.7},{'X':3.6,'Y':0,'Z':9.7},{'X':5.6,'Y':0,'Z':9.7},{'X':7.6,'Y':0,'Z':9.7},{'X':-0.4,'Y':0,'Z':11.7},{'X':1.6,'Y':0,'Z':11.7},{'X':3.6,'Y':0,'Z':11.7},{'X':5.6,'Y':0,'Z':11.7}],'road_nopavement_bend_27x27':[{'X':-5.4,'Y':0,'Z':-0.8},{'X':-3.4,'Y':0,'Z':-0.8},{'X':-1.4,'Y':0,'Z':-0.8},{'X':0.6,'Y':0,'Z':-0.8},{'X':2.6,'Y':0,'Z':-0.8},{'X':4.6,'Y':0,'Z':-0.8},{'X':-5.4,'Y':0,'Z':1.2},{'X':-3.4,'Y':0,'Z':1.2},{'X':-1.4,'Y':0,'Z':1.2},{'X':0.6,'Y':0,'Z':1.2},{'X':2.6,'Y':0,'Z':1.2},{'X':4.6,'Y':0,'Z':1.2},{'X':-5.4,'Y':0,'Z':3.2},{'X':-3.4,'Y':0,'Z':3.2},{'X':-1.4,'Y':0,'Z':3.2},{'X':0.6,'Y':0,'Z':3.2},{'X':2.6,'Y':0,'Z':3.2},{'X':4.6,'Y':0,'Z':3.2},{'X':-5.4,'Y':0,'Z':5.2},{'X':-3.4,'Y':0,'Z':5.2},{'X':-1.4,'Y':0,'Z':5.2},{'X':0.6,'Y':0,'Z':5.2},{'X':2.6,'Y':0,'Z':5.2},{'X':4.6,'Y':0,'Z':5.2},{'X':-5.4,'Y':0,'Z':7.2},{'X':-3.4,'Y':0,'Z':7.2},{'X':-1.4,'Y':0,'Z':7.2},{'X':0.6,'Y':0,'Z':7.2},{'X':2.6,'Y':0,'Z':7.2},{'X':-7.4,'Y':0,'Z':9.2},{'X':-5.4,'Y':0,'Z':9.2},{'X':-3.4,'Y':0,'Z':9.2},{'X':-1.4,'Y':0,'Z':9.2},{'X':0.6,'Y':0,'Z':9.2},{'X':2.6,'Y':0,'Z':9.2},{'X':-9.4,'Y':0,'Z':11.2},{'X':-7.4,'Y':0,'Z':11.2},{'X':-5.4,'Y':0,'Z':11.2},{'X':-3.4,'Y':0,'Z':11.2},{'X':-1.4,'Y':0,'Z':11.2},{'X':0.6,'Y':0,'Z':11.2},{'X':2.6,'Y':0,'Z':11.2},{'X':-11.4,'Y':0,'Z':13.2},{'X':-9.4,'Y':0,'Z':13.2},{'X':-7.4,'Y':0,'Z':13.2},{'X':-5.4,'Y':0,'Z':13.2},{'X':-3.4,'Y':0,'Z':13.2},{'X':-1.4,'Y':0,'Z':13.2},{'X':0.6,'Y':0,'Z':13.2},{'X':-13.4,'Y':0,'Z':15.2},{'X':-11.4,'Y':0,'Z':15.2},{'X':-9.4,'Y':0,'Z':15.2},{'X':-7.4,'Y':0,'Z':15.2},{'X':-5.4,'Y':0,'Z':15.2},{'X':-3.4,'Y':0,'Z':15.2},{'X':-1.4,'Y':0,'Z':15.2},{'X':-23.4,'Y':0,'Z':17.2},{'X':-21.4,'Y':0,'Z':17.2},{'X':-19.4,'Y':0,'Z':17.2},{'X':-17.4,'Y':0,'Z':17.2},{'X':-15.4,'Y':0,'Z':17.2},{'X':-13.4,'Y':0,'Z':17.2},{'X':-11.4,'Y':0,'Z':17.2},{'X':-9.4,'Y':0,'Z':17.2},{'X':-7.4,'Y':0,'Z':17.2},{'X':-5.4,'Y':0,'Z':17.2},{'X':-3.4,'Y':0,'Z':17.2},{'X':-1.4,'Y':0,'Z':17.2},{'X':-23.4,'Y':0,'Z':19.2},{'X':-21.4,'Y':0,'Z':19.2},{'X':-19.4,'Y':0,'Z':19.2},{'X':-17.4,'Y':0,'Z':19.2},{'X':-15.4,'Y':0,'Z':19.2},{'X':-13.4,'Y':0,'Z':19.2},{'X':-11.4,'Y':0,'Z':19.2},{'X':-9.4,'Y':0,'Z':19.2},{'X':-7.4,'Y':0,'Z':19.2},{'X':-5.4,'Y':0,'Z':19.2},{'X':-3.4,'Y':0,'Z':19.2},{'X':-23.4,'Y':0,'Z':21.2},{'X':-21.4,'Y':0,'Z':21.2},{'X':-19.4,'Y':0,'Z':21.2},{'X':-17.4,'Y':0,'Z':21.2},{'X':-15.4,'Y':0,'Z':21.2},{'X':-13.4,'Y':0,'Z':21.2},{'X':-11.4,'Y':0,'Z':21.2},{'X':-9.4,'Y':0,'Z':21.2},{'X':-7.4,'Y':0,'Z':21.2},{'X':-5.4,'Y':0,'Z':21.2},{'X':-23.4,'Y':0,'Z':23.2},{'X':-21.4,'Y':0,'Z':23.2},{'X':-19.4,'Y':0,'Z':23.2},{'X':-17.4,'Y':0,'Z':23.2},{'X':-15.4,'Y':0,'Z':23.2},{'X':-13.4,'Y':0,'Z':23.2},{'X':-11.4,'Y':0,'Z':23.2},{'X':-9.4,'Y':0,'Z':23.2},{'X':-7.4,'Y':0,'Z':23.2},{'X':-23.4,'Y':0,'Z':25.2},{'X':-21.4,'Y':0,'Z':25.2},{'X':-19.4,'Y':0,'Z':25.2},{'X':-17.4,'Y':0,'Z':25.2},{'X':-15.4,'Y':0,'Z':25.2},{'X':-13.4,'Y':0,'Z':25.2},{'X':-11.4,'Y':0,'Z':25.2},{'X':-23.4,'Y':0,'Z':27.2},{'X':-21.4,'Y':0,'Z':27.2},{'X':-19.4,'Y':0,'Z':27.2},{'X':-17.4,'Y':0,'Z':27.2}],'road_nopavement_junction_15x15':[{'X':-4.1,'Y':0,'Z':-8},{'X':-2.1,'Y':0,'Z':-8},{'X':-0.1,'Y':0,'Z':-8},{'X':1.9,'Y':0,'Z':-8},{'X':3.9,'Y':0,'Z':-8},{'X':-4.1,'Y':0,'Z':-6},{'X':-2.1,'Y':0,'Z':-6},{'X':-0.1,'Y':0,'Z':-6},{'X':1.9,'Y':0,'Z':-6},{'X':3.9,'Y':0,'Z':-6},{'X':-8.1,'Y':0,'Z':-4},{'X':-6.1,'Y':0,'Z':-4},{'X':-4.1,'Y':0,'Z':-4},{'X':-2.1,'Y':0,'Z':-4},{'X':-0.1,'Y':0,'Z':-4},{'X':1.9,'Y':0,'Z':-4},{'X':3.9,'Y':0,'Z':-4},{'X':5.9,'Y':0,'Z':-4},{'X':7.9,'Y':0,'Z':-4},{'X':-8.1,'Y':0,'Z':-2},{'X':-6.1,'Y':0,'Z':-2},{'X':-4.1,'Y':0,'Z':-2},{'X':-2.1,'Y':0,'Z':-2},{'X':-0.1,'Y':0,'Z':-2},{'X':1.9,'Y':0,'Z':-2},{'X':3.9,'Y':0,'Z':-2},{'X':5.9,'Y':0,'Z':-2},{'X':7.9,'Y':0,'Z':-2},{'X':-8.1,'Y':0,'Z':0},{'X':-6.1,'Y':0,'Z':0},{'X':-4.1,'Y':0,'Z':0},{'X':-2.1,'Y':0,'Z':0},{'X':-0.1,'Y':0,'Z':0},{'X':1.9,'Y':0,'Z':0},{'X':3.9,'Y':0,'Z':0},{'X':5.9,'Y':0,'Z':0},{'X':7.9,'Y':0,'Z':0},{'X':-8.1,'Y':0,'Z':2},{'X':-6.1,'Y':0,'Z':2},{'X':-4.1,'Y':0,'Z':2},{'X':-2.1,'Y':0,'Z':2},{'X':-0.1,'Y':0,'Z':2},{'X':1.9,'Y':0,'Z':2},{'X':3.9,'Y':0,'Z':2},{'X':5.9,'Y':0,'Z':2},{'X':7.9,'Y':0,'Z':2},{'X':-8.1,'Y':0,'Z':4},{'X':-6.1,'Y':0,'Z':4},{'X':-4.1,'Y':0,'Z':4},{'X':-2.1,'Y':0,'Z':4},{'X':-0.1,'Y':0,'Z':4},{'X':1.9,'Y':0,'Z':4},{'X':3.9,'Y':0,'Z':4},{'X':5.9,'Y':0,'Z':4},{'X':7.9,'Y':0,'Z':4},{'X':-4.1,'Y':0,'Z':6},{'X':-2.1,'Y':0,'Z':6},{'X':-0.1,'Y':0,'Z':6},{'X':1.9,'Y':0,'Z':6},{'X':3.9,'Y':0,'Z':6},{'X':-4.1,'Y':0,'Z':8},{'X':-2.1,'Y':0,'Z':8},{'X':-0.1,'Y':0,'Z':8},{'X':1.9,'Y':0,'Z':8},{'X':3.9,'Y':0,'Z':8}],'road_nopavement_junctiont_15x15':[{'X':-4.5,'Y':0,'Z':-8.6},{'X':-2.5,'Y':0,'Z':-8.6},{'X':-0.5,'Y':0,'Z':-8.6},{'X':1.5,'Y':0,'Z':-8.6},{'X':3.5,'Y':0,'Z':-8.6},{'X':-4.5,'Y':0,'Z':-6.6},{'X':-2.5,'Y':0,'Z':-6.6},{'X':-0.5,'Y':0,'Z':-6.6},{'X':1.5,'Y':0,'Z':-6.6},{'X':3.5,'Y':0,'Z':-6.6},{'X':-6.5,'Y':0,'Z':-4.6},{'X':-4.5,'Y':0,'Z':-4.6},{'X':-2.5,'Y':0,'Z':-4.6},{'X':-0.5,'Y':0,'Z':-4.6},{'X':1.5,'Y':0,'Z':-4.6},{'X':3.5,'Y':0,'Z':-4.6},{'X':5.5,'Y':0,'Z':-4.6},{'X':-8.5,'Y':0,'Z':-2.6},{'X':-6.5,'Y':0,'Z':-2.6},{'X':-4.5,'Y':0,'Z':-2.6},{'X':-2.5,'Y':0,'Z':-2.6},{'X':-0.5,'Y':0,'Z':-2.6},{'X':1.5,'Y':0,'Z':-2.6},{'X':3.5,'Y':0,'Z':-2.6},{'X':5.5,'Y':0,'Z':-2.6},{'X':7.5,'Y':0,'Z':-2.6},{'X':-8.5,'Y':0,'Z':-0.6},{'X':-6.5,'Y':0,'Z':-0.6},{'X':-4.5,'Y':0,'Z':-0.6},{'X':-2.5,'Y':0,'Z':-0.6},{'X':-0.5,'Y':0,'Z':-0.6},{'X':1.5,'Y':0,'Z':-0.6},{'X':3.5,'Y':0,'Z':-0.6},{'X':5.5,'Y':0,'Z':-0.6},{'X':7.5,'Y':0,'Z':-0.6},{'X':-8.5,'Y':0,'Z':1.4},{'X':-6.5,'Y':0,'Z':1.4},{'X':-4.5,'Y':0,'Z':1.4},{'X':-2.5,'Y':0,'Z':1.4},{'X':-0.5,'Y':0,'Z':1.4},{'X':1.5,'Y':0,'Z':1.4},{'X':3.5,'Y':0,'Z':1.4},{'X':5.5,'Y':0,'Z':1.4},{'X':7.5,'Y':0,'Z':1.4},{'X':-8.5,'Y':0,'Z':3.4},{'X':-6.5,'Y':0,'Z':3.4},{'X':-4.5,'Y':0,'Z':3.4},{'X':-2.5,'Y':0,'Z':3.4},{'X':-0.5,'Y':0,'Z':3.4},{'X':1.5,'Y':0,'Z':3.4},{'X':3.5,'Y':0,'Z':3.4},{'X':5.5,'Y':0,'Z':3.4},{'X':7.5,'Y':0,'Z':3.4},{'X':-8.5,'Y':0,'Z':5.4},{'X':-6.5,'Y':0,'Z':5.4},{'X':-4.5,'Y':0,'Z':5.4},{'X':-2.5,'Y':0,'Z':5.4},{'X':-0.5,'Y':0,'Z':5.4},{'X':1.5,'Y':0,'Z':5.4},{'X':3.5,'Y':0,'Z':5.4},{'X':5.5,'Y':0,'Z':5.4},{'X':7.5,'Y':0,'Z':5.4}],'road_nopavement_junctiont_large':[{'X':-5,'Y':0,'Z':-14},{'X':-3,'Y':0,'Z':-14},{'X':-1,'Y':0,'Z':-14},{'X':1,'Y':0,'Z':-14},{'X':3,'Y':0,'Z':-14},{'X':5,'Y':0,'Z':-14},{'X':-5,'Y':0,'Z':-12},{'X':-3,'Y':0,'Z':-12},{'X':-1,'Y':0,'Z':-12},{'X':1,'Y':0,'Z':-12},{'X':3,'Y':0,'Z':-12},{'X':5,'Y':0,'Z':-12},{'X':-5,'Y':0,'Z':-10},{'X':-3,'Y':0,'Z':-10},{'X':-1,'Y':0,'Z':-10},{'X':1,'Y':0,'Z':-10},{'X':3,'Y':0,'Z':-10},{'X':5,'Y':0,'Z':-10},{'X':-5,'Y':0,'Z':-8},{'X':-3,'Y':0,'Z':-8},{'X':-1,'Y':0,'Z':-8},{'X':1,'Y':0,'Z':-8},{'X':3,'Y':0,'Z':-8},{'X':5,'Y':0,'Z':-8},{'X':-5,'Y':0,'Z':-6},{'X':-3,'Y':0,'Z':-6},{'X':-1,'Y':0,'Z':-6},{'X':1,'Y':0,'Z':-6},{'X':3,'Y':0,'Z':-6},{'X':5,'Y':0,'Z':-6},{'X':-7,'Y':0,'Z':-4},{'X':-5,'Y':0,'Z':-4},{'X':-3,'Y':0,'Z':-4},{'X':-1,'Y':0,'Z':-4},{'X':1,'Y':0,'Z':-4},{'X':3,'Y':0,'Z':-4},{'X':5,'Y':0,'Z':-4},{'X':7,'Y':0,'Z':-4},{'X':-9,'Y':0,'Z':-2},{'X':-7,'Y':0,'Z':-2},{'X':-5,'Y':0,'Z':-2},{'X':-3,'Y':0,'Z':-2},{'X':-1,'Y':0,'Z':-2},{'X':1,'Y':0,'Z':-2},{'X':3,'Y':0,'Z':-2},{'X':5,'Y':0,'Z':-2},{'X':7,'Y':0,'Z':-2},{'X':9,'Y':0,'Z':-2},{'X':-11,'Y':0,'Z':0},{'X':-9,'Y':0,'Z':0},{'X':-7,'Y':0,'Z':0},{'X':-5,'Y':0,'Z':0},{'X':-3,'Y':0,'Z':0},{'X':-1,'Y':0,'Z':0},{'X':1,'Y':0,'Z':0},{'X':3,'Y':0,'Z':0},{'X':5,'Y':0,'Z':0},{'X':7,'Y':0,'Z':0},{'X':9,'Y':0,'Z':0},{'X':11,'Y':0,'Z':0},{'X':-13,'Y':0,'Z':2},{'X':-11,'Y':0,'Z':2},{'X':-9,'Y':0,'Z':2},{'X':-7,'Y':0,'Z':2},{'X':-5,'Y':0,'Z':2},{'X':-3,'Y':0,'Z':2},{'X':-1,'Y':0,'Z':2},{'X':1,'Y':0,'Z':2},{'X':3,'Y':0,'Z':2},{'X':5,'Y':0,'Z':2},{'X':7,'Y':0,'Z':2},{'X':9,'Y':0,'Z':2},{'X':11,'Y':0,'Z':2},{'X':13,'Y':0,'Z':2},{'X':-23,'Y':0,'Z':4},{'X':-21,'Y':0,'Z':4},{'X':-19,'Y':0,'Z':4},{'X':-17,'Y':0,'Z':4},{'X':-15,'Y':0,'Z':4},{'X':-13,'Y':0,'Z':4},{'X':-11,'Y':0,'Z':4},{'X':-9,'Y':0,'Z':4},{'X':-7,'Y':0,'Z':4},{'X':-5,'Y':0,'Z':4},{'X':-3,'Y':0,'Z':4},{'X':-1,'Y':0,'Z':4},{'X':1,'Y':0,'Z':4},{'X':3,'Y':0,'Z':4},{'X':5,'Y':0,'Z':4},{'X':7,'Y':0,'Z':4},{'X':9,'Y':0,'Z':4},{'X':11,'Y':0,'Z':4},{'X':13,'Y':0,'Z':4},{'X':15,'Y':0,'Z':4},{'X':17,'Y':0,'Z':4},{'X':19,'Y':0,'Z':4},{'X':21,'Y':0,'Z':4},{'X':23,'Y':0,'Z':4},{'X':-23,'Y':0,'Z':6},{'X':-21,'Y':0,'Z':6},{'X':-19,'Y':0,'Z':6},{'X':-17,'Y':0,'Z':6},{'X':-15,'Y':0,'Z':6},{'X':-13,'Y':0,'Z':6},{'X':-11,'Y':0,'Z':6},{'X':-9,'Y':0,'Z':6},{'X':-7,'Y':0,'Z':6},{'X':-5,'Y':0,'Z':6},{'X':-3,'Y':0,'Z':6},{'X':-1,'Y':0,'Z':6},{'X':1,'Y':0,'Z':6},{'X':3,'Y':0,'Z':6},{'X':5,'Y':0,'Z':6},{'X':7,'Y':0,'Z':6},{'X':9,'Y':0,'Z':6},{'X':11,'Y':0,'Z':6},{'X':13,'Y':0,'Z':6},{'X':15,'Y':0,'Z':6},{'X':17,'Y':0,'Z':6},{'X':19,'Y':0,'Z':6},{'X':21,'Y':0,'Z':6},{'X':23,'Y':0,'Z':6},{'X':-23,'Y':0,'Z':8},{'X':-21,'Y':0,'Z':8},{'X':-19,'Y':0,'Z':8},{'X':-17,'Y':0,'Z':8},{'X':-15,'Y':0,'Z':8},{'X':-13,'Y':0,'Z':8},{'X':-11,'Y':0,'Z':8},{'X':-9,'Y':0,'Z':8},{'X':-7,'Y':0,'Z':8},{'X':-5,'Y':0,'Z':8},{'X':-3,'Y':0,'Z':8},{'X':-1,'Y':0,'Z':8},{'X':1,'Y':0,'Z':8},{'X':3,'Y':0,'Z':8},{'X':5,'Y':0,'Z':8},{'X':7,'Y':0,'Z':8},{'X':9,'Y':0,'Z':8},{'X':11,'Y':0,'Z':8},{'X':13,'Y':0,'Z':8},{'X':15,'Y':0,'Z':8},{'X':17,'Y':0,'Z':8},{'X':19,'Y':0,'Z':8},{'X':21,'Y':0,'Z':8},{'X':23,'Y':0,'Z':8},{'X':-23,'Y':0,'Z':10},{'X':-21,'Y':0,'Z':10},{'X':-19,'Y':0,'Z':10},{'X':-17,'Y':0,'Z':10},{'X':-15,'Y':0,'Z':10},{'X':-13,'Y':0,'Z':10},{'X':-11,'Y':0,'Z':10},{'X':-9,'Y':0,'Z':10},{'X':-7,'Y':0,'Z':10},{'X':-5,'Y':0,'Z':10},{'X':-3,'Y':0,'Z':10},{'X':-1,'Y':0,'Z':10},{'X':1,'Y':0,'Z':10},{'X':3,'Y':0,'Z':10},{'X':5,'Y':0,'Z':10},{'X':7,'Y':0,'Z':10},{'X':9,'Y':0,'Z':10},{'X':11,'Y':0,'Z':10},{'X':13,'Y':0,'Z':10},{'X':15,'Y':0,'Z':10},{'X':17,'Y':0,'Z':10},{'X':19,'Y':0,'Z':10},{'X':21,'Y':0,'Z':10},{'X':23,'Y':0,'Z':10},{'X':-23,'Y':0,'Z':12},{'X':-21,'Y':0,'Z':12},{'X':-19,'Y':0,'Z':12},{'X':-17,'Y':0,'Z':12},{'X':-15,'Y':0,'Z':12},{'X':-13,'Y':0,'Z':12},{'X':-11,'Y':0,'Z':12},{'X':-9,'Y':0,'Z':12},{'X':-7,'Y':0,'Z':12},{'X':-5,'Y':0,'Z':12},{'X':-3,'Y':0,'Z':12},{'X':-1,'Y':0,'Z':12},{'X':1,'Y':0,'Z':12},{'X':3,'Y':0,'Z':12},{'X':5,'Y':0,'Z':12},{'X':7,'Y':0,'Z':12},{'X':9,'Y':0,'Z':12},{'X':11,'Y':0,'Z':12},{'X':13,'Y':0,'Z':12},{'X':15,'Y':0,'Z':12},{'X':17,'Y':0,'Z':12},{'X':19,'Y':0,'Z':12},{'X':21,'Y':0,'Z':12},{'X':23,'Y':0,'Z':12},{'X':-23,'Y':0,'Z':14},{'X':-21,'Y':0,'Z':14},{'X':-19,'Y':0,'Z':14},{'X':-17,'Y':0,'Z':14},{'X':-15,'Y':0,'Z':14},{'X':-13,'Y':0,'Z':14},{'X':-11,'Y':0,'Z':14},{'X':-9,'Y':0,'Z':14},{'X':-7,'Y':0,'Z':14},{'X':-5,'Y':0,'Z':14},{'X':-3,'Y':0,'Z':14},{'X':-1,'Y':0,'Z':14},{'X':1,'Y':0,'Z':14},{'X':3,'Y':0,'Z':14},{'X':5,'Y':0,'Z':14},{'X':7,'Y':0,'Z':14},{'X':9,'Y':0,'Z':14},{'X':11,'Y':0,'Z':14},{'X':13,'Y':0,'Z':14},{'X':15,'Y':0,'Z':14},{'X':17,'Y':0,'Z':14},{'X':19,'Y':0,'Z':14},{'X':21,'Y':0,'Z':14},{'X':23,'Y':0,'Z':14}]}";
        #endregion
        #endregion

        #region Component        
        private class VehicleAI : MonoBehaviour
        {
            internal ModularCar Entity { get; private set; }

            internal BasePlayer Caller { get; private set; }

            internal TaxiState State { get; private set; } = TaxiState.TravellingToPickup;

            internal int FareCost { get; private set; }

            private bool HasPaidFare { get; set; }

            internal bool HasDestinationSet { get; private set; }

            private Transform _tr;

            private BasePlayer _driver;

            internal List<Vector3> Path;

            private int _targetIndex = 1;

            private Vector3 _targetNode;

            private Vector3 _intendedDestination;

            private InputState _inputState = new InputState();

            private RaycastHit _raycastHit;

            private float _cornerSpeed = 4f;

            private float _targetSpeed = 20f;

            private float _maximumSpeed = 25f;

            private float _collisionProximity = 0f;

            private float _diagonalAvoidanceDelta = 0f;

            private float _avoidanceDelta = 0f;

            private float _reverseEndTime;

            private float _stuckTime = 0f;

            private float _lastSteerInput = 0f;

            private Sensor[] _sensors;

            private Plane _plane = new Plane();

            private bool shouldDestroyVehicle = true;

            private float NextCornerAngle
            {
                get
                {
                    Vector3 direction = GetNode(_targetIndex + 1) - _targetNode;
                    return Vector3.Angle(new Vector3(direction.x, 0f, direction.z), new Vector3(_tr.forward.x, 0f, _tr.forward.z));
                }
            }

            private const float CORNER_ANGLE_LIMIT = 45f;

            private const float HALF_LENGTH = 3f;

            private bool HasPassedTarget => !_plane.GetSide(_tr.position);

            private bool IsReversing => Entity.GetSpeed() < 0f;

            private bool WantsToReverse => Time.time < _reverseEndTime;

            internal enum TaxiState { TravellingToPickup, WaitingForInput, TravellingToDestination, WaitingForExit, Despawning }

            private void Awake()
            {
                Entity = GetComponent<ModularCar>();
                _tr = Entity.transform;

                enabled = false;

                PopulateSensors();

                InvokeHandler.InvokeRepeating(this, UpdateLights, 1f, 5f);
                InvokeHandler.InvokeRepeating(this, CheckWorkingEngines, 5f, 3f);
            }

            private void OnCollisionEnter(Collision collision)
            {
                ResourceEntity resourceEntity = collision.collider?.gameObject?.ToBaseEntity() as ResourceEntity;
                if (resourceEntity != null)
                    resourceEntity.OnKilled(new HitInfo(Entity, resourceEntity, Rust.DamageType.Collision, 1000f));
            }

            private void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, UpdateLights);
                InvokeHandler.CancelInvoke(this, CheckWorkingEngines);

                ActiveVehicles?.Remove(this);

                DismountAllPlayers();

                if (Path != null)
                    Pool.FreeList(ref Path);

                if (_driver != null && !_driver.IsDestroyed)
                    _driver.Kill();

                if (Caller != null && Caller.IsConnected)
                    Caller.SendConsoleCommand("gametip.hidegametip");

                if (shouldDestroyVehicle)
                {
                    if (Entity != null && !Entity.IsDestroyed)
                    {
                        for (int y = 0; y < Entity.AttachedModuleEntities.Count; y++)
                        {
                            VehicleModuleStorage storage = Entity.AttachedModuleEntities[y] as VehicleModuleStorage;
                            if (storage != null)
                            {
                                ItemContainer container = storage.GetContainer()?.inventory;
                                if (container != null)
                                {
                                    for (int z = container.itemList.Count - 1; z >= 0; z--)
                                    {
                                        Item item = container.itemList[z];
                                        item.RemoveFromContainer();
                                        item.Remove();
                                    }
                                }
                            }
                        }

                        Entity.Kill();
                    }
                }
            }

            #region Initialization
            private void PopulateSensors()
            {
                BoxCollider boxCollider = Entity.hurtTriggerFront.GetComponent<BoxCollider>();

                float x = boxCollider.bounds.extents.x;
                float z = boxCollider.bounds.extents.z * 0.25f;

                _sensors = new Sensor[]
                {   // Sensor Type          Position                                                Angle   Avoidance   Length     
                    new SideSensor(_tr,     boxCollider.center - new Vector3(x, 0f, z + 0.5f),      -90f,   1f),                        // Left Side
                    new DiagonalSensor(_tr, boxCollider.center - new Vector3(x, 0f, z),             -30f,   0.5f,       0.7f),          // Left Diagonal
                    new Sensor(_tr,         boxCollider.center - new Vector3(x, 0f, z),             0f,     1f,         1f),            // Left Forward

                    new Sensor(_tr,         boxCollider.center + new Vector3(x, 0f, -z),            0f,     -1f,        1f),            // Right Forward
                    new DiagonalSensor(_tr, boxCollider.center + new Vector3(x, 0f, -z),            30f,    -0.5f,      0.7f),          // Right Diagonal
                    new SideSensor(_tr,     boxCollider.center + new Vector3(x, 0f, -(z + 0.5f)),   90f,    -1f),                       // Right Side

                    new CenterSensor(_tr,   boxCollider.center + new Vector3(0f, 0f, -z),           0f,                 1f),            // Center Forward
                };
            }
            
            internal void InitializeVehicle(BasePlayer caller, List<Vector3> path)
            {
                Caller = caller;
                Path = path;

                _targetNode = Path[_targetIndex];
                UpdateNodePlane();

                Entity.Invoke(VehicleSetup, 1.5f);

                State = TaxiState.TravellingToPickup;
            }

            private void VehicleSetup()
            {
                StorageContainer fuelSystem = (Entity.GetFuelSystem() as EntityFuelSystem).GetFuelContainer();
                fuelSystem.inventory.AddItem(fuelSystem.allowedItem, 10000, (ulong)0);
                fuelSystem.SetFlag(BaseEntity.Flags.Locked, true);

                Entity.SetHealth(Entity.MaxHealth());

                for (int i = 0; i < Entity.AttachedModuleEntities.Count; i++)
                    Entity.AttachedModuleEntities[i].AdminFixUp(Mathf.Clamp(Configuration.Taxi.Tier, 1, 3));

                Entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                CreateAndMountDriver();
            }

            private void CreateAndMountDriver()
            {
                _driver = GameManager.server.CreateEntity(SCIENTIST_PREFAB, _tr.position, Quaternion.identity) as BasePlayer;
                _driver.enableSaving = false;
                _driver.Spawn();

                _driver.displayName = "Taxi Driver";

                _driver.inventory.Strip();
                _driver.enabled = false;

                for (int i = 0; i < Configuration.Taxi.DriverClothes.Length; i++)
                {
                    ConfigData.TaxiVehicle.KitItem kitItem = Configuration.Taxi.DriverClothes[i];

                    Item item = ItemManager.CreateByName(kitItem.Shortname, 1, kitItem.SkinID);

                    if (item != null)
                        item.MoveToContainer(_driver.inventory.containerWear);
                }

                foreach (BaseVehicle.MountPointInfo allMountPoint in Entity.allMountPoints)
                {
                    if (allMountPoint == null || allMountPoint.mountable == null || !allMountPoint.isDriver)
                        continue;

                    BasePlayer mounted = allMountPoint.mountable.GetMounted();
                    if (mounted != null)
                        continue;

                    allMountPoint.mountable.MountPlayer(_driver);
                    enabled = true;
                    return;
                }

                Debug.LogError("This taxi does not have any driver's seats!");
                Destroy(this);
            }
            #endregion

            #region Update
            private void Update()
            {
                if (!_driver || _driver.IsDead())
                {
                    shouldDestroyVehicle = false;
                    Destroy(this);
                    return;
                }
                
                if (State == TaxiState.WaitingForInput || State == TaxiState.WaitingForExit)
                {
                    if (Entity.CurEngineState == VehicleEngineController<GroundVehicle>.EngineState.On)
                        Entity.engineController.StopEngine();
                    ReduceVelocity();
                    return;
                }

                _inputState.Clear();

                if (Entity.engineController.IsWaterlogged())
                {
                    DismountAllPlayers();

                    _driver.Die(new HitInfo(_driver, _driver, Rust.DamageType.Drowned, 1000f));
                    Entity.Die(new HitInfo(_driver, _driver, Rust.DamageType.Explosion, 1000f));
                    return;
                }

                if (Entity.IsFlipped())
                {
                    _tr.position += Vector3.up;
                    _tr.rotation = Quaternion.Euler(0f, _tr.eulerAngles.y, 0f);
                }

                if (Mathf.Abs(Entity.GetSpeed()) < 1f)
                {
                    _stuckTime += Time.deltaTime;

                    if (_stuckTime > 2f)                    
                        StartReversing();                    
                }
                else _stuckTime = 0f;

                UpdateTargetNode();

                DetectObstacles();

                Vector3 relativeTarget = _tr.InverseTransformPoint(_targetNode);
                
                DoSteering(relativeTarget);
                DoThrottle(relativeTarget);

                Entity.PlayerServerInput(_inputState, _driver);
            }

            private void CheckWorkingEngines()
            {
                if (!_driver)
                    return;

                if (!Entity.HasAnyWorkingEngines())
                {
                    shouldDestroyVehicle = false;
                    Destroy(this);
                }
            }
            #endregion

            #region Nodes
            private Vector3 GetNode(int index)
            {
                if (index > Path.Count - 1)
                    index = index - Path.Count - 1;

                if (index < 0)
                    index = Path.Count + index;

                return Path[index];
            }

            private void UpdateTargetNode()
            {
                float distance = Vector3Ex.Distance2D(_tr.position, _targetNode) - HALF_LENGTH;
                if (distance <= 2f || HasPassedTarget)
                {                    
                    _targetIndex++;

                    if (_targetIndex >= Path.Count)
                    {
                        OnReachedDestination();
                        return;                                          
                    }

                    _targetNode = GetNode(_targetIndex);
                    UpdateNodePlane();

                    if (Instance.DrawSensors)
                    {
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                        {
                            if (player.IsAdmin)
                                player.SendConsoleCommand("ddraw.sphere", 5f, Color.red, _targetNode, 0.5f);
                        }
                    }
                }                
            }

            private void UpdateNodePlane()
            {
                Vector3 previousPoint = GetNode(_targetIndex - 1);
                Vector3 dir = (_targetNode - previousPoint).normalized;

                _plane.Set3Points(_targetNode, _targetNode + Vector3.up, _targetNode + (Quaternion.LookRotation(dir) * Vector3.right));
            }

            #endregion

            #region Locomotion
            private void DoSteering(Vector3 relativeTarget)
            {
                float steer = !Mathf.Approximately(_avoidanceDelta, 0f) ? _avoidanceDelta : relativeTarget.x / relativeTarget.magnitude;

                if (IsReversing)
                    steer *= -1f;

                _inputState.current.buttons |= (int)BUTTON.FIRE_THIRD;

                _inputState.current.mouseDelta.x = ((Entity.GetSteerInput() * -1f) + steer) * 10f;                      
            }

            private void DoThrottle(Vector3 relativeTarget)
            {
                float currentSpeed = Entity.GetSpeed();

                float delta = Mathf.Min(1f, relativeTarget.z < 0 ? 0f : 1f);
                delta = Mathf.Min(delta, Mathf.Approximately(_avoidanceDelta, 0f) ? 1f : Mathf.Clamp01(1f - Mathf.Abs(_avoidanceDelta)));
                delta = Mathf.Min(delta, Mathf.InverseLerp(1f, 0.25f, Mathf.Abs(_targetNode.y - _tr.position.y) / 4f));
                delta = Mathf.Min(delta, 1f - Mathf.Clamp01(NextCornerAngle / CORNER_ANGLE_LIMIT));
                delta = Mathf.Min(delta, _collisionProximity <= 1 ? 1f - _collisionProximity : 1f);

                float speed = Mathf.Lerp(_cornerSpeed, _targetSpeed, delta);

                if (currentSpeed > (speed + _cornerSpeed) || currentSpeed > _maximumSpeed || WantsToReverse)                
                    _inputState.current.buttons |= (int)BUTTON.BACKWARD;                
                else if (currentSpeed < speed)
                    _inputState.current.buttons |= (int)BUTTON.FORWARD;
            }

            private void StartReversing()
            {                
                _reverseEndTime = Time.time + 1.5f;
            }

            private void StopReversing() => _reverseEndTime = 0f;
            #endregion

            #region Obstacle Detection            
            private void DetectObstacles()
            {
                _avoidanceDelta = 0f;
                _diagonalAvoidanceDelta = 0f;

                float _collisionDist = float.MaxValue;

                float speedDelta = Mathf.Clamp01(Entity.GetSpeed() / _maximumSpeed);
                float sensorRange = CalculateSensorRange(speedDelta);
                float sensorCollisionDistance = CalculateCollisionProximityDistance(sensorRange, speedDelta);

                int hits = 0;
                Sensor sensor;

                Physics.queriesHitBackfaces = true;

                for (int i = 0; i < _sensors.Length; i++)
                {
                    sensor = _sensors[i];
                    bool hasHit = sensor.Cast(sensorRange, _targetNode, out _raycastHit, WantsToReverse) && (!_raycastHit.collider?.gameObject?.name?.Contains("Road", CompareOptions.OrdinalIgnoreCase) ?? true);
                    bool isOverWater = sensor.IsOverWater();

                    if (Instance.DrawSensors)
                    {
                        Color color = hasHit ? Color.red : isOverWater ? Color.green : Color.blue;
                        
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                        {
                            if (player.IsAdmin)                            
                                sensor.DrawSensor(player, color); 
                        }
                    }

                    if (hasHit || isOverWater)
                    {                        
                        if (sensor.isCollisionDetector && ((hasHit && _raycastHit.distance < sensorCollisionDistance) || isOverWater))
                        {
                            hits++;
                            _collisionDist = Mathf.Min(_collisionDist, hasHit ? _raycastHit.distance : sensorRange);
                        }

                        if (sensor is CenterSensor)
                        {
                            if (Mathf.Approximately(_avoidanceDelta, 0f))
                            {
                                _avoidanceDelta = Vector3.Angle(_tr.position - _targetNode, _tr.forward) < 0f ? 1f : -1f;
                            }
                        }
                        else if (sensor is DiagonalSensor)
                        {
                            _avoidanceDelta += Mathf.Lerp(0.1f, sensor.avoidanceMultiplier, Mathf.Abs(Vector3.Dot(sensor.WorldForward, _raycastHit.normal)));
                            _diagonalAvoidanceDelta += sensor.avoidanceMultiplier;
                        }
                        else _avoidanceDelta += sensor.avoidanceMultiplier;
                    }
                }

                Physics.queriesHitBackfaces = false;
                _collisionProximity = _collisionDist / sensorCollisionDistance;

                if (hits >= 3)
                {
                    if (!Mathf.Approximately(_diagonalAvoidanceDelta, 0f))
                        _avoidanceDelta = _diagonalAvoidanceDelta < 0f ? -1f : 1f;
                    else if (Mathf.Approximately(_avoidanceDelta, 0f))
                        _avoidanceDelta = UnityEngine.Random.value < 0.5f ? 1f : -1f;

                    if (_collisionProximity < 1f || _collisionProximity <= sensorCollisionDistance)
                    {
                        if (WantsToReverse)
                            StopReversing();
                        else StartReversing();
                    }
                }
            }

            private float CalculateSensorRange(float speedDelta) => Mathf.Lerp(6f, 20f, speedDelta);

            private float CalculateCollisionProximityDistance(float sensorRange, float speedDelta) => Mathf.Lerp(2f, sensorRange, speedDelta);
            #endregion

            #region Destination Pathfinding
            internal void OnMarkerPlaced(Vector3 position, MapNote mapNote)
            {
                if (PathRequests.Contains(Caller))
                {
                    Caller.ChatMessage(Message("Error.PendingCalculation", Caller.userID, true));
                    return;
                }

                InvokeHandler.CancelInvoke(this, KickIdlePlayers);
                InvokeHandler.Invoke(this, KickIdlePlayers, Configuration.MaxWaitTime * 2f);

                _intendedDestination = position;

                if (!PathRequests.Contains(Caller))
                    PathRequests.Add(Caller);

                Caller.SendConsoleCommand("gametip.hidegametip");
                Caller.SendConsoleCommand("gametip.showgametip", Message("GameTip.Calculating", Caller.userID));

                FareCost = 0;
                PathFinder.EvaluateGrid(new PathRequest(_tr.position, position, Caller, Pool.GetList<Vector3>(), OnPathGenerated, OnPathFailed));                
            }

            private void OnPathGenerated(PathCompletedResult pathResult)
            {
                PathRequests.Remove(Caller);

                List<Vector3> results = pathResult.Results;

                if (Caller == null || !Caller.IsConnected)
                {
                    Pool.FreeList(ref results);
                    return;
                }

                if (Path != null)
                    Pool.FreeList(ref Path);                

                Path = results;

                _targetIndex = 0;

                UpdateNodePlane();

                HasDestinationSet = true;

                if (Caller.IPlayer.HasPermission(PERMISSION_NOCOST))
                    FareCost = 0;
                else
                {
                    float distance = 0;
                    for (int i = 1; i < results.Count; i++)
                        distance += Vector3.Distance(Path[i - 1], Path[i]);

                    FareCost = Mathf.RoundToInt((distance / 1000f) * Instance.GetFareCost(Caller.UserIDString));
                }

                Caller.SendConsoleCommand("gametip.hidegametip");
                Caller.SendConsoleCommand("gametip.showgametip", FareCost > 0 ? string.Format(Message("GameTip.Fare.Cost", Caller.userID), FareCost, Message($"FareCost.{Instance._purchaseType}", Caller.userID)) :                                                              Message("GameTip.Fare.Free", Caller.userID));

                InvokeHandler.CancelInvoke(this, KickIdlePlayers);
                InvokeHandler.Invoke(this, KickIdlePlayers, Configuration.MaxWaitTime * 2f);
            }

            private void OnPathFailed(PathFailedResult pathResult)
            {
                PathRequests.Remove(Caller);
                Caller.ChatMessage(Message($"Error.PathFail.{pathResult.FailType}", Caller.userID, true));

                InvokeHandler.CancelInvoke(this, KickIdlePlayers);
                InvokeHandler.Invoke(this, KickIdlePlayers, Configuration.MaxWaitTime * 2f);
            }
            #endregion

            #region State Changes
            private void OnReachedDestination()
            {
                if (State == TaxiState.TravellingToPickup)
                {
                    State = TaxiState.WaitingForInput;

                    _inputState.Clear();
                    Entity.PlayerServerInput(_inputState, _driver);

                    DoHornLoop();

                    InvokeHandler.Invoke(this, CancelByTimelimit, Configuration.MaxWaitTime);
                }
                else if (State == TaxiState.TravellingToDestination)
                {
                    State = TaxiState.WaitingForExit;

                    _inputState.Clear();
                    Entity.PlayerServerInput(_inputState, _driver);

                    Caller.ChatMessage(Message("Notification.ArrivedAtDest", Caller.userID, true));

                    InvokeHandler.Invoke(this, KickIdlePlayers, Configuration.MaxWaitTime);
                }
                else if (State == TaxiState.Despawning)
                {
                    Destroy(this);
                }
            }

            internal void HeadTowardsDestination()
            {
                HasPaidFare = true;
                State = TaxiState.TravellingToDestination;
                InvokeHandler.CancelInvoke(this, KickIdlePlayers);
            }

            private void ReduceVelocity()
            {
                Entity.rigidBody.velocity = Vector3.Lerp(Entity.rigidBody.velocity, Vector3.zero, Time.deltaTime * 4f);
            }
            #endregion

            #region Hornage
            private void DoHornLoop()
            {
                EnableHorn();
                InvokeHandler.Invoke(this, DisableHorn, 0.25f);
                InvokeHandler.Invoke(this, DisableHorn, 1f);
                InvokeHandler.Invoke(this, DisableHorn, 1.25f);
            }

            private void EnableHorn()
            {
                _inputState.Clear();
                _inputState.current.buttons |= (int)BUTTON.FIRE_PRIMARY;
                Entity.PlayerServerInput(_inputState, _driver);
            }

            private void DisableHorn()
            {
                _inputState.Clear();
                Entity.PlayerServerInput(_inputState, _driver);
            }
            #endregion

            #region Lights
            private void UpdateLights()
            {
                if (TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunsetTime || TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunriseTime)
                {
                    if (!Entity.HasFlag(BaseEntity.Flags.Reserved5))
                        Entity.SetFlag(BaseEntity.Flags.Reserved5, true);
                }
                else
                {
                    if (Entity.HasFlag(BaseEntity.Flags.Reserved5))
                        Entity.SetFlag(BaseEntity.Flags.Reserved5, false);
                }
            }
            #endregion

            #region Abortion
            private void CancelByTimelimit()
            {
                Caller.SendConsoleCommand("gametip.hidegametip");
                Caller.ChatMessage(Message("Notification.TimedOut", Caller.userID, true));
                CancelService();
            }

            private void KickIdlePlayers()
            {
                Caller.SendConsoleCommand("gametip.hidegametip");
                Caller.ChatMessage(Message("Notification.Kicked", Caller.userID, true));
                CancelService();
            }

            internal void CancelService()
            {
                DoHornLoop();

                if (HasPaidFare && _targetIndex < Path.Count - 1)
                {
                    int refundFare = FareCost - Mathf.RoundToInt((((float)_targetIndex / (float)Path.Count) * (float)FareCost));

                    if (refundFare > 0)
                        Instance.RefundFareCost(Caller, refundFare);
                }

                Caller = null;

                InvokeHandler.CancelInvoke(this, KickIdlePlayers);

                DismountAllPlayers();

                if (_targetIndex > Path.Count * 0.5f)
                {
                    Path.Reverse();
                    _targetIndex = 1;
                }

                State = TaxiState.Despawning;

                Destroy(this, 30f);
            }
            #endregion

            #region Mount/Dismount
            internal void OnCallerEnteredVehicle()
            {
                Caller.SendConsoleCommand("gametip.hidegametip");
                Caller.SendConsoleCommand("gametip.showgametip", Message("GameTip.SetDestination", Caller.userID));

                InvokeHandler.CancelInvoke(this, CancelByTimelimit);
                InvokeHandler.Invoke(this, KickIdlePlayers, Configuration.MaxWaitTime * 2f);
            }

            private void DismountAllPlayers()
            {
                foreach (BaseVehicle.MountPointInfo mountPointInfo in Entity.allMountPoints)
                {
                    if (mountPointInfo.mountable && !mountPointInfo.isDriver)
                    {
                        mountPointInfo.mountable.DismountAllPlayers();
                    }
                }
            }
            #endregion           
            
            #region Sensors
            private class Sensor
            {
                internal Vector3 localOffset;
                internal Vector3 invertedOffset;

                internal float angle;
                internal float avoidanceMultiplier;
                internal float sensorMultiplier;
                internal bool isCollisionDetector;

                private Transform tr;

                protected Vector3 _sensorEnd;

                internal Vector3 WorldPosition => tr.TransformPoint(localOffset);

                internal Vector3 InvertedPosition => tr.TransformPoint(invertedOffset);
                
                internal Vector3 WorldForward => Quaternion.AngleAxis(angle, tr.up) * tr.forward;

                internal Vector3 InvertedForward => Quaternion.AngleAxis(angle * -1f, tr.up) * -tr.forward;

                internal const int SENSOR_LAYERS = 1 << 0 | 1 << 8 | 1 << 12 | 1 << 13 | 1 << 15 | 1 << 16 | 1 << 21 | 1 << 24 | 1 << 27 | 1 << 30;

                internal const float CAST_RADIUS = 0.35f;

                internal Sensor(Transform tr, Vector3 localOffset, float angle, float avoidanceMultiplier, float sensorMultiplier, bool isCollisionDetector = true)
                {
                    this.tr = tr;

                    this.localOffset = localOffset;
                    this.angle = angle;
                    this.avoidanceMultiplier = avoidanceMultiplier;
                    this.sensorMultiplier = sensorMultiplier;
                    this.isCollisionDetector = isCollisionDetector;

                    invertedOffset = new Vector3(localOffset.x, localOffset.y, localOffset.z * -1f);
                }

                internal virtual bool Cast(float sensorRange, Vector3 nextCheckPoint, out RaycastHit raycastHit, bool invert = false)
                {
                    if (invert)
                    {
                        _sensorEnd = InvertedPosition + (InvertedForward * (sensorRange * sensorMultiplier));
                        return Physics.SphereCast(InvertedPosition, CAST_RADIUS, InvertedForward, out raycastHit, sensorRange * sensorMultiplier, SENSOR_LAYERS);
                    }
                    else
                    {
                        float range = sensorRange * sensorMultiplier;
                        _sensorEnd = WorldPosition + (WorldForward * range);

                        if (_sensorEnd.y < nextCheckPoint.y)
                        {                            
                            float distance = Vector3.Distance(tr.position, nextCheckPoint);
                            float delta = range > distance ? 1f : range / distance;
                            
                            _sensorEnd.y = Mathf.Lerp(WorldPosition.y, nextCheckPoint.y + localOffset.y, delta);

                            return Physics.SphereCast(WorldPosition, CAST_RADIUS, (_sensorEnd - WorldPosition).normalized, out raycastHit, sensorRange * sensorMultiplier, SENSOR_LAYERS);
                        }
                        else
                        {
                            return Physics.SphereCast(WorldPosition, CAST_RADIUS, WorldForward, out raycastHit, sensorRange * sensorMultiplier, SENSOR_LAYERS);
                        }
                    }
                }

                internal bool IsOverWater() => TerrainMeta.HeightMap.GetHeight(_sensorEnd) < -0.7f || ContainsTopologyAtPoint(TerrainTopology.Enum.Ocean | TerrainTopology.Enum.River | TerrainTopology.Enum.Lake, _sensorEnd);
                
                internal void DrawSensor(BasePlayer player, Color color)
                {                    
                    player.SendConsoleCommand("ddraw.line", Time.deltaTime, color, WorldPosition, _sensorEnd);

                    if (!(this is SideSensor))
                    {
                        player.SendConsoleCommand("ddraw.sphere", Time.deltaTime, color, WorldPosition, CAST_RADIUS);
                        player.SendConsoleCommand("ddraw.sphere", Time.deltaTime, color, _sensorEnd, CAST_RADIUS);
                    }
                }
            }

            private class CenterSensor : Sensor
            {
                internal CenterSensor(Transform tr, Vector3 localOffset, float angle, float sensorMultiplier) : base(tr, localOffset, angle, 0f, sensorMultiplier, true) { }
            }

            private class SideSensor : Sensor
            {
                internal SideSensor(Transform tr, Vector3 localOffset, float angle, float avoidanceMultiplier) : base(tr, localOffset, angle, avoidanceMultiplier, 1f, false) { }

                internal override bool Cast(float sensorRange, Vector3 nextCheckPoint, out RaycastHit raycastHit, bool invert = false)
                {
                    _sensorEnd = WorldPosition + (WorldForward * CAST_RADIUS);
                    return Physics.SphereCast(WorldPosition, CAST_RADIUS, WorldForward, out raycastHit, CAST_RADIUS, SENSOR_LAYERS);
                }
            }

            private class DiagonalSensor : Sensor
            {
                internal DiagonalSensor(Transform tr, Vector3 localOffset, float angle, float avoidanceMultiplier, float sensorMultiplier) : base(tr, localOffset, angle, avoidanceMultiplier, sensorMultiplier, false) { }
            }
            #endregion
        }
        #endregion

        #region Vehicle Creator
        public enum ChassisType { TwoModule, ThreeModule, FourModule }

        private readonly string[] _chassisPrefabs = new string[]
        {
            "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab",
            "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab",
            "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab"
        };

        private VehicleAI CreateTaxiVehicle(Vector3 position, Quaternion rotation)
        {
            ModularCar modularCar = GameManager.server.CreateEntity(_chassisPrefabs[Mathf.Clamp(Configuration.Taxi.Chassis, 0, 2)], position, rotation) as ModularCar;
            modularCar.enableSaving = false;
            modularCar.spawnSettings.useSpawnSettings = false;
            modularCar.Spawn();

            for (int i = 0; i < Mathf.Min(Configuration.Taxi.Modules.Count, (int)Configuration.Taxi.Chassis + 2); i++)
            {
                Item item = ItemManager.CreateByName(Configuration.Taxi.Modules[i]);
                if (!modularCar.TryAddModule(item, i))
                    item.Remove(0f);
                //item.MoveToContainer(modularCar.Inventory.ModuleContainer);
            }

            return modularCar.gameObject.AddComponent<VehicleAI>();
        }        
        #endregion

        #region Commands
        [ChatCommand("taxi")]
        private void cmdTaxi(BasePlayer player, string command, string[] args)
        {
            if (Configuration.UseTelephone)
            {
                player.ChatMessage(Message("Notification.TelephoneOnly", player.userID, true));
                return;
            }

            RequestTaxi(player, false);
        }

        private void RequestTaxi(BasePlayer player, bool isTelephone)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                player.ChatMessage(Message("Error.NoPermission", player.userID, true));
                return;
            }
                        
            if (PathRequests.Contains(player) || ActiveVehicles.Any((VehicleAI ai) => ai.Caller == player))
            {
                player.ChatMessage(Message("Error.TaxiInbound", player.userID, true));
                return;
            }

            if (player.GetMountedVehicle()?.GetComponent<VehicleAI>() != null)
            {
                player.ChatMessage(Message("Error.InTaxi", player.userID, true));
                return;
            }

            float cooldown;
            if (Cooldowns.TryGetValue(player.userID, out cooldown))
            {
                if (cooldown > Time.time && !permission.UserHasPermission(player.UserIDString, PERMISSION_NOCOOLDOWN))
                {
                    player.ChatMessage(string.Format(Message("Error.Cooldown", player.userID, true), Mathf.RoundToInt(cooldown - Time.time)));
                    return;
                }
            }

            if (ActiveVehicles.Count >= Configuration.MaximumVehicles || PathFinder.IsCalculating)
            {
                player.ChatMessage(Message("Error.Busy", player.userID, true));
                return;
            }

            if (!isTelephone)
            {
                int topology = TerrainMeta.TopologyMap.GetTopology(player.transform.position, MAXIMUM_CALL_DISTANCE_FROM_ROAD);

                if (!ContainsTopology(topology, TerrainTopology.Enum.Road))
                {
                    player.ChatMessage(Message("Error.RoadTooFar", player.userID, true));
                    return;
                }
            }

            Vector3 spawnPoint = FindNodeAtDistance(player.transform.position, 125, 25);
            if (isTelephone && Vector3.Distance(spawnPoint, player.transform.position) < 1f)
            {
                player.ChatMessage(Message("Error.RoadTooFar", player.userID, true));
                return;
            }

            Vector3 closestNode = FindClosestNodeTo(player.transform.position);

            if (!PathRequests.Contains(player))
                PathRequests.Add(player);

            player.ChatMessage(Message("Notification.Calculating", player.userID, true));
            PathFinder.EvaluateGrid(new PathRequest(spawnPoint, closestNode, player, Pool.GetList<Vector3>(), OnPathGenerated, OnPathFailed));
        }

        [ChatCommand("payfare")]
        private void cmdPayFare(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                player.ChatMessage(Message("Error.NoPermission", player.userID, true));
                return;
            }

            VehicleAI vehicleAI = player.GetMountedVehicle()?.GetComponent<VehicleAI>();
            if (vehicleAI != null)
            {
                if (player == vehicleAI.Caller && vehicleAI.State == VehicleAI.TaxiState.WaitingForInput)
                {
                    if (!vehicleAI.HasDestinationSet)
                    {
                        player.ChatMessage(Message("Error.NoDestination", player.userID, true));
                        return;
                    }

                    player.SendConsoleCommand("gametip.hidegametip");

                    int scrapAmount = GetAmount(player);

                    if (scrapAmount < vehicleAI.FareCost)
                    {
                        player.ChatMessage(string.Format(Message("Error.NotEnoughBalance", player.userID, true), Message($"FareCost.{Instance._purchaseType}", player.userID)));
                    }
                    else
                    {
                        SubtractFareCost(player, vehicleAI.FareCost);
                        player.ChatMessage(Message("Notification.HeadingToDest", player.userID, true));
                        vehicleAI.HeadTowardsDestination();
                    }                    
                }
            }
        }

        [ChatCommand("drawpath")]
        private void DrawPath(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            foreach (VehicleAI vehicleAI in ActiveVehicles)
            {
                for (int i = 0; i < vehicleAI.Path.Count; i++)
                {
                    player.SendConsoleCommand("ddraw.sphere", 10f, Color.blue, vehicleAI.Path[i], 0.5f);
                }
            }
        }

        [ChatCommand("cellinfo")]
        private void CellInfo(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            GridNode node = PathFinder.FindOrCreateCell(player.transform.position);

            SendReply(player, $"X {node.X} Z {node.Z} - Cost = {node.C_Cost} - Blocked? {node.IsBlocked}");
        }

        [ChatCommand("celldebug")]
        private void CellDebug(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            float radius = 10;

            if (args.Length > 0)
                float.TryParse(args[0], out radius);

            List<GridNode> list = Pool.GetList<GridNode>();

            PathFinder.GetNodesInRadius(player.transform.position, radius, ref list);

            for (int i = 0; i < list.Count; i++)
            {
                GridNode gridNode = list[i];

                player.SendConsoleCommand("ddraw.sphere", 10f, Color.blue, gridNode.Position, gridNode.CellSize * 0.5f);
                player.SendConsoleCommand("ddraw.text", 10f, Color.white, gridNode.Position + (Vector3.up * gridNode.CellSize), $"X {gridNode.X} Z {gridNode.Z}\nCost {gridNode.C_Cost}\nBlocked? {gridNode.IsBlocked}");
            }

            Pool.FreeList(ref list);
        }
        #endregion

        #region Config 
        private static ConfigData Configuration;

        private class ConfigData
        {         
            [JsonProperty(PropertyName = "Scrap cost per 1km travelled (permission -> cost)")]
            public List<CostEntry> Costs { get; set; }

            [JsonProperty(PropertyName = "Cost type (Scrap, ServerRewards, Economics)")]
            public string CostType { get; set; }

            [JsonProperty(PropertyName = "Are taxi vehicles invincible?")]
            public bool Invincible { get; set; }

            [JsonProperty(PropertyName = "Maximum allowed taxi's at any given time")]
            public int MaximumVehicles { get; set; }

            [JsonProperty(PropertyName = "Maximum time the taxi will wait for the player to enter before leaving")]
            public int MaxWaitTime { get; set; }

            [JsonProperty(PropertyName = "Amount of time from when the player leaves a taxi until they can call another one (seconds)")]
            public int CooldownTime { get; set; }

            [JsonProperty(PropertyName = "Taxi Vehicle Settings")]
            public TaxiVehicle Taxi { get; set; }

            [JsonProperty(PropertyName = "Setup and allow taxi's to be called via telephones (disables chat command)")]
            public bool UseTelephone { get; set; }

            [JsonProperty(PropertyName = "Pathfinder Debug")]
            public bool PathfinderDebug { get; set; }

            public class TaxiVehicle
            {
                [JsonProperty(PropertyName = "Chassis Type (0 = TwoModule, 1 = ThreeModule, 2 = FourModule)")]
                public int Chassis { get; set; }

                [JsonProperty(PropertyName = "Modules (item shortname)")]
                public List<string> Modules { get; set; }

                [JsonProperty(PropertyName = "Engine component tier (1 - 3)")]
                public int Tier { get; set; }

                [JsonProperty(PropertyName = "Driver clothing items")]
                public KitItem[] DriverClothes { get; set; }

                public class KitItem
                {
                    public string Shortname { get; set; }
                    public ulong SkinID { get; set; }
                }
            }

            public struct CostEntry
            {
                public string Permission { get; set; }
                public int Cost { get; set; }
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
                MaximumVehicles = 2,
                Costs = new List<ConfigData.CostEntry>
                {
                    new ConfigData.CostEntry() { Permission = "npctaxi.use", Cost = 50 },
                    new ConfigData.CostEntry() { Permission = "npctaxi.vip1", Cost = 25 },
                    new ConfigData.CostEntry() { Permission = "npctaxi.vip2", Cost = 10 }
                },
                CostType = "Scrap",
                Invincible = true,
                MaxWaitTime = 45,
                CooldownTime = 60,                
                Taxi = new ConfigData.TaxiVehicle
                {
                    Chassis = 1,
                    Tier = 3,
                    Modules = new List<string>
                    {
                        "vehicle.1mod.cockpit.with.engine",
                        "vehicle.1mod.taxi",
                        "vehicle.1mod.taxi"
                    },
                    DriverClothes = new ConfigData.TaxiVehicle.KitItem[]
                    {
                        new ConfigData.TaxiVehicle.KitItem
                        {
                            Shortname= "pants",
                            SkinID = 0
                        },
                        new ConfigData.TaxiVehicle.KitItem
                        {
                            Shortname = "boots",
                            SkinID = 0
                        },
                        new ConfigData.TaxiVehicle.KitItem
                        {
                            Shortname = "shirt.collared",
                            SkinID = 0
                        },
                        new ConfigData.TaxiVehicle.KitItem
                        {
                            Shortname = "movembermoustache",
                            SkinID = 0
                        }
                    }
                },
                PathfinderDebug = false,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (Configuration.Version < new Core.VersionNumber(0, 1, 1))
                Configuration.Costs = baseConfig.Costs;

            if (Configuration.Version < new Core.VersionNumber(0, 1, 2))
            {
                Configuration.Taxi.Chassis = 1;
                Configuration.Taxi.Tier = 3;
            }

            if (Configuration.Version < new Core.VersionNumber(0, 1, 3))
            {
                Configuration.Taxi.Tier = Mathf.Clamp(Configuration.Taxi.Tier, 1, 3);
                Configuration.CostType = "Scrap";
            }

            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Localization
        public static string Message(string key, ulong playerId = 0U, bool title = false)
        {
            if (title)
                return Instance.lang.GetMessage("Message.Title", Instance, playerId != 0U ? playerId.ToString() : null) + Instance.lang.GetMessage(key, Instance, playerId != 0U ? playerId.ToString() : null);
            return Instance.lang.GetMessage(key, Instance, playerId != 0U ? playerId.ToString() : null);
        }

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Message.Title"] = "[<color=#FFC02E>TAXI</color>] ",
            ["Error.TooFarAway"] = "The chosen destination is too far away from a road or trail",
            ["Error.PathFail.Generic"] = "Path calculation failed",
            ["Error.PathFail.Unreachable"] = "Path calculation failed : Destination is unreachable",
            ["Error.PathFail.SmallerThanGrid"] = "Path calculation failed : Path distance is too small",
            ["Error.PathFail.NotEnoughNodes"] = "Path calculation failed : Not enough nodes to calculate a path",
            ["Error.PendingCalculation"] = "Please wait until your current destination has been calculated",
            ["Error.NoPermission"] = "You do not have permission to use this command",
            ["Error.TaxiInbound"] = "You already have a taxi inbound or ready",
            ["Error.InTaxi"] = "You are already in a taxi",
            ["Error.Cooldown"] = "You must wait another <color=#FFC02E>{0}</color> seconds before calling another taxi",
            ["Error.Busy"] = "All taxi's are currently busy. Try again soon",
            ["Error.RoadTooFar"] = "You must move closer to a road or trail to call a taxi",
            ["Error.NoDestination"] = "You must set a destination first",
            ["Error.NotEnoughBalance"] = "You do not have enough {0} to reach your destination",
            ["Notification.Inbound"] = "A vehicle is on its way. Your map has been marked with the pick up location",
            ["Notification.ArrivedAtDest"] = "We have arrived at your destination!",
            ["Notification.TimedOut"] = "You took too long and your taxi left",
            ["Notification.Kicked"] = "You were kicked for waiting too long",
            ["Notification.Calculating"] = "The driver is calculating their route to you...",
            ["Notification.HeadingToDest"] = "Thanks, heading to your destination now",
            ["Notification.Goodbye"] = "See you next time!",
            ["Notification.TelephoneOnly"] = "You can only call for a taxi via telephones",
            ["Notification.NoSwappingSeats"] = "You can swap seats in a taxi",
            ["GameTip.SetDestination"] = "Set your destination by placing a marker on your map",
            ["GameTip.Fare.Cost"] = "Your trip will cost <color=#FFC02E>{0}</color> {1}. Type <color=#FFC02E>/payfare</color> to proceed",
            ["GameTip.Fare.Free"] = "Your trip is free. Type <color=#FFC02E>/payfare</color> to proceed",
            ["GameTip.Calculating"] = "Calculating route. Please wait...",   
            ["FareCost.Scrap"] = "Scrap",
            ["FareCost.ServerRewards"] = "RP",
            ["FareCost.Economics"] = "Coins",
        };
        #endregion
    }
}
