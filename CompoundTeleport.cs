using Facepunch;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("CompoundTeleport", "k1lly0u", "2.0.16")]
    [Description("Allow players to respawn at, or teleport to the Outpost and Bandit Camp")]
    class CompoundTeleport : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin NoEscape;

        private Hash<ulong, Cooldown> cooldowns = new Hash<ulong, Cooldown>();

        private Hash<ulong, Action> pending = new Hash<ulong, Action>();

        private SpawnManager Outpost { get; set; }

        private SpawnManager Bandit { get; set; }

        private const string TELEPORT_PERMISSION = "compoundteleport.tp";
        private const string RESPAWN_PERMISSION = "compoundteleport.respawn";
        private const string ADMIN_PERMISSION = "compoundteleport.admin";

        private const int TARGET_LAYERS = ~(1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);

        private const string RESPAWN_BAG_CMD = "respawn_sleepingbag";
        private const string REMOVE_BAG_CMD = "respawn_sleepingbag_remove";
        private const string RESPAWN_CMD = "respawn";

        private static NetworkableId OUTPOST_ID = new NetworkableId(111);
        private static NetworkableId BANDIT_ID = new NetworkableId(112);
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            lang.RegisterMessages(Messages, this);

            permission.RegisterPermission(TELEPORT_PERMISSION, this);
            permission.RegisterPermission(RESPAWN_PERMISSION, this);
            permission.RegisterPermission(ADMIN_PERMISSION, this);

            foreach (ConfigData.CustomSpawnPoint customSpawnPoint in configData.Custom)
            {
                if (customSpawnPoint.Enabled && customSpawnPoint.CanTeleportTo)
                {
                    string name = customSpawnPoint.Name.Replace(" ", "").ToLower();
                    
                    cmd.AddChatCommand(name, this, 
                        (b, s, a) => cmdTeleportCustom(b, s, new []{customSpawnPoint.Name}));
                    
                    Puts($"Registered teleport command /{name}");
                }
            }

            foreach (KeyValuePair<string, ConfigData.TeleportOptions.VIP> kvp in configData.Teleport.VIPTimes)
                permission.RegisterPermission(kvp.Key, this);

            cmd.AddChatCommand(configData.Teleport.OutpostCommand, this, cmdTeleportOutpost);
            cmd.AddChatCommand(configData.Teleport.BanditCommand, this, cmdTeleportBandit);
            cmd.AddChatCommand(configData.Teleport.CancelCommand, this, cmdTeleportCancel);
        }

        private void OnServerInitialized()
        {
            GenerateSpawnPoints();

            int index = 0;
            foreach (ConfigData.CustomSpawnPoint customSpawnPoint in configData.Custom)
            {
                if (customSpawnPoint.Enabled)
                {
                    customSpawnPoint.Setup(index);
                    index++;
                }
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo) => CancelPendingTeleports(player);
        
        private void OnRespawnInformationGiven(BasePlayer player, List<RespawnInformation.SpawnOptions> list)
        {
            if (!player || player.IsNpc || !player.IsConnected)
                return;

            if (!configData.Respawn.AllowBandit && !configData.Respawn.AllowOutpost)
                return;

            if (configData.Respawn.DisableIfHostile && player.IsHostile())
                return;

            if (!permission.UserHasPermission(player.UserIDString, RESPAWN_PERMISSION) && !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                return;

            if (NoEscape)
            {
                if (configData.NoEscape.RaidBlockedRespawn)
                {
                    bool success = NoEscape.Call<bool>("IsRaidBlocked", player);
                    if (success)
                    {
                        SendReply(player, msg("Error.RaidBlocked", player.userID));
                        return;
                    }
                }

                if (configData.NoEscape.CombatBlockedRespawn)
                {
                    bool success = NoEscape.Call<bool>("IsCombatBlocked", player);
                    if (success)
                    {
                        SendReply(player, msg("Error.CombatBlocked", player.userID));
                        return;
                    }
                }
            }

            Cooldown cooldown;
            cooldowns.TryGetValue(player.userID, out cooldown);

            if (Outpost != null && configData.Respawn.AllowOutpost)
            {
                RespawnInformation.SpawnOptions d = Pool.Get<RespawnInformation.SpawnOptions>();
                d.id = OUTPOST_ID;
                d.name = msg("Name.Outpost", player.userID);
                d.worldPosition = Outpost.Position;
                d.type = RespawnInformation.SpawnOptions.RespawnType.Bed;
                d.unlockSeconds = (cooldown?.respawn ?? 0) != 0 ? Mathf.Clamp((cooldown.respawn - Epoch.Current), 0, int.MaxValue) : 0;
                d.respawnState = RespawnInformation.SpawnOptions.RespawnState.OK;
                list.Add(d);
            }

            if (Bandit != null && configData.Respawn.AllowBandit)
            {
                RespawnInformation.SpawnOptions d = Pool.Get<RespawnInformation.SpawnOptions>();
                d.id = BANDIT_ID;
                d.name = msg("Name.Bandit", player.userID);
                d.worldPosition = Bandit.Position;
                d.type = RespawnInformation.SpawnOptions.RespawnType.Bed;
                d.unlockSeconds = (cooldown?.respawn ?? 0) != 0 ? Mathf.Clamp((cooldown.respawn - Epoch.Current), 0, int.MaxValue) : 0;
                d.respawnState = RespawnInformation.SpawnOptions.RespawnState.OK;
                list.Add(d);
            }
            
            foreach (ConfigData.CustomSpawnPoint customSpawnPoint in configData.Custom)
            {
                if (customSpawnPoint.Enabled && customSpawnPoint.CanRespawnAt)
                {
                    RespawnInformation.SpawnOptions d = Pool.Get<RespawnInformation.SpawnOptions>();
                    d.id = customSpawnPoint.Id;
                    d.name = customSpawnPoint.Name;
                    d.worldPosition = customSpawnPoint.WorldPosition;
                    d.type = RespawnInformation.SpawnOptions.RespawnType.Bed;
                    d.unlockSeconds = (cooldown?.respawn ?? 0) != 0 ? Mathf.Clamp((cooldown.respawn - Epoch.Current), 0, int.MaxValue) : 0;
                    d.respawnState = RespawnInformation.SpawnOptions.RespawnState.OK;
                    list.Add(d);
                }
            }
        }
        
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !player.IsConnected)
                return null;

            if (configData.Respawn.DisableIfHostile && player.IsHostile())
                return null;

            if (player.IsDead() || player.IsSpectating())
            {
                if (arg.cmd.Name == RESPAWN_CMD)
                {
                    if (configData.Respawn.ForceTo == "None")
                        return null;

                    if (configData.Respawn.ForceTo == "Outpost" && Outpost != null)
                    {
                        RespawnPlayerAt(player, Outpost.Get());
                        return false;
                    }

                    if (configData.Respawn.ForceTo == "Bandit" && Bandit != null)
                    {
                        RespawnPlayerAt(player, Bandit.Get());
                        return false;
                    }
                }

                if (arg.cmd.Name == REMOVE_BAG_CMD)
                {
                    NetworkableId num = arg.GetEntityID(0);
                    if (num == OUTPOST_ID || num == BANDIT_ID)
                        return false;

                    if (configData.Custom.Any(x => x.Enabled && x.Id == num))
                        return false;
                }

                if (arg.cmd.Name == RESPAWN_BAG_CMD)
                {
                    NetworkableId num = arg.GetEntityID(0);
                    if (num == OUTPOST_ID && Outpost != null)
                    {
                        RespawnPlayerAt(player, Outpost.Get());
                        return false;
                    }

                    if (num == BANDIT_ID && Bandit != null)
                    {
                        RespawnPlayerAt(player, Bandit.Get());
                        return false;
                    }
                    
                    foreach (ConfigData.CustomSpawnPoint customSpawnPoint in configData.Custom)
                    {
                        if (customSpawnPoint.Enabled && customSpawnPoint.CanRespawnAt && customSpawnPoint.Id == num)
                        {
                            RespawnPlayerAt(player, customSpawnPoint.Manager.Get());
                            return false;
                        }
                    }
                }
            }
            return null;
        }    
        
        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CancelPendingTeleports(player);
        }
        #endregion

        #region Functions
        private void GenerateSpawnPoints()
        {
            if (ConVar.Server.level == "HapisIsland")            
                Outpost = new SpawnManager(new Vector3(-211f, 105f, -432f), 40f, "road", "carpark", "pavement");            
            else
            {
                foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
                {
                    if (monument.name.Contains("/compound.prefab"))                                            
                        Outpost = new SpawnManager(monument.transform.position, 40f, "road", "carpark", "pavement");                    
                    else if (monument.name.Contains("/bandit_town.prefab"))
                        Bandit = new SpawnManager(monument.transform.position, 80f, "walkway");
                }
            }

            if (Outpost == null && Bandit == null)
            {
                PrintError("Unable to find a Outpost or Bandit Camp on this map. Unable to continue...");
                return;
            }

            if (Outpost != null)
                Puts($"Generated {Outpost.Count} spawn points at the Outpost");

            if (Bandit != null)
                Puts($"Generated {Bandit.Count} spawn points at the Bandit Camp");
        }

        private void RespawnPlayerAt(BasePlayer player, Vector3 position)
        {
            Cooldown cooldown;

            if (!cooldowns.TryGetValue(player.userID, out cooldown))
                cooldowns[player.userID] = cooldown = new Cooldown();

            player.RespawnAt(position, Quaternion.identity);
            cooldown.respawn = Epoch.Current + configData.Respawn.Cooldown;
        }

        private bool HasPendingTeleports(BasePlayer player) => pending.ContainsKey(player.userID);

        private void CancelPendingTeleports(BasePlayer player)
        {
            if (player == null)
                return;

            if (HasPendingTeleports(player))
            {
                player.CancelInvoke(pending[player.userID]);
                pending.Remove(player.userID);
            }
        }
        #endregion

        #region Helpers        
        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            int days = dateDifference.Days;
            int hours = dateDifference.Hours + (days * 24);

            int mins = dateDifference.Minutes;
            int secs = dateDifference.Seconds;

            if (hours > 0)
                return $"{hours:00}h:{mins:00}m:{secs:00}s";
            if (mins > 0)
                return $"{mins:00}m:{secs:00}s";
            return $"{secs:00}s";
        }

        private int GetCooldownTime(string playerId)
        {
            int cooldown = configData.Teleport.Cooldown;

            foreach(KeyValuePair<string, ConfigData.TeleportOptions.VIP> kvp in configData.Teleport.VIPTimes)
            {
                if (permission.UserHasPermission(playerId, kvp.Key))
                    cooldown = kvp.Value.Cooldown;
            }

            return cooldown;
        }

        private int GetWaitTime(string playerId)
        {
            int wait = configData.Teleport.Wait;

            foreach (KeyValuePair<string, ConfigData.TeleportOptions.VIP> kvp in configData.Teleport.VIPTimes)
            {
                if (permission.UserHasPermission(playerId, kvp.Key))
                    wait = kvp.Value.Wait;
            }

            return wait;
        }

        private void TeleportToOutpost(BasePlayer player) => MovePosition(player, Outpost.Get());

        private void TeleportToBandit(BasePlayer player) => MovePosition(player, Bandit.Get());

        private void MovePosition(BasePlayer player, Vector3 destination)
        {
            try
            {
                if (player.isMounted)
                    player.GetMounted().DismountPlayer(player, true);

                player.SetParent(null, true, true);

                player.StartSleeping();
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);

                player.EnablePlayerCollider();               
                player.SetServerFall(true);

                player.RemoveFromTriggers();

                player.MovePosition(destination);
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);

                player.UpdateNetworkGroup();
                player.SendNetworkUpdateImmediate();
                player.ClearEntityQueue();
                player.SendFullSnapshot();
            }
            finally
            {
                //player.UpdatePlayerCollider(true);
                //player.UpdatePlayerRigidbody(true);
                player.EnablePlayerCollider();
                //player.AddPlayerRigidbody();
                player.SetServerFall(false);

                Cooldown cooldown;
                if (!cooldowns.TryGetValue(player.userID, out cooldown))
                    cooldowns[player.userID] = cooldown = new Cooldown();

                cooldown.command = Epoch.Current + GetCooldownTime(player.UserIDString);

                pending.Remove(player.userID);
            }            
        }
        #endregion

        #region Spawn Handler
        private class SpawnManager
        {
            private List<Vector3> _spawnPoints;
            private List<Vector3> _availablePoints;

            public int Count { get { return _spawnPoints.Count; } }

            public Vector3 Position { get; private set; }

            public SpawnManager() { }

            public SpawnManager(Vector3 position, float size, params string[] targetNames)
            {
                RaycastHit rayHit;

                _spawnPoints = new List<Vector3>();

                for (int i = 0; i < 150; i++)
                {
                    Vector3 p = position + (Random.insideUnitSphere * size);
                    p.y = position.y + 100f;

                    if (Physics.SphereCast(new Ray(p, Vector3.down), 0.5f, out rayHit, 150, TARGET_LAYERS, QueryTriggerInteraction.Ignore))
                    {                        
                        if (TerrainMeta.WaterMap.GetDepth(rayHit.point) > 0f)
                            continue;
                        
                        if (rayHit.collider is TerrainCollider)
                            _spawnPoints.Add(rayHit.point);
                        else
                        {
                            if (targetNames != null)
                            {
                                if (targetNames.Any(rayHit.collider.name.Contains))                                
                                    _spawnPoints.Add(rayHit.point);                                
                            }
                        }
                    }
                }

                _availablePoints = new List<Vector3>(_spawnPoints);
                
                Position = position;
            }

            public Vector3 Get()
            {
                Vector3 point = _availablePoints.GetRandom();
                _availablePoints.Remove(point);

                if (_availablePoints.Count == 0)
                    _availablePoints = new List<Vector3>(_spawnPoints);

                return point;
            }
        }
        #endregion

        #region Commands
        private void cmdTeleportOutpost(BasePlayer player, string command, string[] args)
        {
            if (!MeetsAllRequirements(player))
                return;

            InvokeTeleport(player, () => TeleportToOutpost(player));

            SendReply(player, string.Format(msg("Notification.TPPending", player.userID), msg("Name.Outpost", player.userID), FormatTime(GetWaitTime(player.UserIDString))));
        }

        private void cmdTeleportBandit(BasePlayer player, string command, string[] args)
        {
            if (!MeetsAllRequirements(player))
                return;

            InvokeTeleport(player, () => TeleportToBandit(player));
            
            SendReply(player, string.Format(msg("Notification.TPPending", player.userID), msg("Name.Bandit", player.userID), FormatTime(GetWaitTime(player.UserIDString))));
        }
        
        private void cmdTeleportCustom(BasePlayer player, string command, string[] args)
        {
            if (configData.Custom.All(x => !x.Enabled))
            {
                SendReply(player, msg("Notification.NoAreas", player.userID));
                return;
            }
            
            if (args == null || args.Length == 0)
            {
                SendReply(player, msg("Notification.NoAreaSpecified", player.userID));
                return;
            }

            string name = string.Join(" ", args);
            
            ConfigData.CustomSpawnPoint customSpawnPoint = null;
            foreach (ConfigData.CustomSpawnPoint spawnPoint in configData.Custom)
            {
                if (spawnPoint.Enabled && spawnPoint.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    customSpawnPoint = spawnPoint;
                    break;
                }
            }

            if (customSpawnPoint == null)
            {
                SendReply(player, string.Format(msg("Notification.NoAreaWithName", player.userID), name));
                return;
            }
            
            if (!MeetsAllRequirements(player))
                return;

            InvokeTeleport(player, () => MovePosition(player, customSpawnPoint.Manager.Get()));

            SendReply(player, string.Format(msg("Notification.TPPending", player.userID), customSpawnPoint.Name, FormatTime(GetWaitTime(player.UserIDString))));
        }

        private void InvokeTeleport(BasePlayer player, Action action)
        {
            player.Invoke(action, GetWaitTime(player.UserIDString));

            pending[player.userID] = action;
        }

        private bool MeetsAllRequirements(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                return true;

            if (!permission.UserHasPermission(player.UserIDString, TELEPORT_PERMISSION))
            {
                SendReply(player, msg("Error.NoPermission", player.userID));
                return false;
            }

            if (HasPendingTeleports(player))
            {
                SendReply(player, msg("Error.TPPending", player.userID));
                return false;
            }

            if (configData.Teleport.Hostile && player.IsHostile())
            {
                SendReply(player, msg("Error.Hostile", player.userID));
                return false;
            }

            if (configData.Teleport.BuildingBlocked && player.IsBuildingBlocked())
            {
                SendReply(player, msg("Error.BuildingBlocked", player.userID));
                return false;
            }

            Cooldown cooldown;
            if (cooldowns.TryGetValue(player.userID, out cooldown))
            {
                int cooldownTime = cooldown.command - Epoch.Current;
                if (cooldownTime > 0)
                {
                    SendReply(player, string.Format(msg("Error.Cooldown", player.userID), FormatTime(cooldownTime)));
                    return false;
                }
            }

            if (IsEscapeBlocked(player))
                return false;

            return true;
        }

        private void cmdTeleportCancel(BasePlayer player, string command, string[] args)
        {
            if (!HasPendingTeleports(player))
            {
                SendReply(player, msg("Notification.NonePending", player.userID));
                return;
            }

            CancelPendingTeleports(player);
            SendReply(player, msg("Notification.Cancelled", player.userID));
        }
        #endregion

        #region Plugin Integration
        private bool IsEscapeBlocked(BasePlayer player)
        {
            if (NoEscape)
            {
                if (configData.NoEscape.RaidBlocked)
                {
                    bool success = NoEscape.Call<bool>("IsRaidBlocked", player);
                    if (success)
                    {
                        SendReply(player, msg("Error.RaidBlocked", player.userID));
                        return true;
                    }
                }

                if (configData.NoEscape.CombatBlocked)
                {
                    bool success = NoEscape.Call<bool>("IsCombatBlocked", player);
                    if (success)
                    {
                        SendReply(player, msg("Error.CombatBlocked", player.userID));
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region Config        
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "NoEscape Options")]
            public NoEscapeOptions NoEscape { get; set; }

            [JsonProperty(PropertyName = "Teleport Options")]
            public TeleportOptions Teleport { get; set; }

            [JsonProperty(PropertyName = "Respawn Options")]
            public RespawnOptions Respawn { get; set; }
            
            [JsonProperty(PropertyName = "Custom Spawn Points")]
            public List<CustomSpawnPoint> Custom { get; set; }

            public class TeleportOptions
            {
                [JsonProperty(PropertyName = "Amount of time to wait before teleporting (seconds)")]
                public int Wait { get; set; }
                
                [JsonProperty(PropertyName = "Cooldown time (seconds)")]
                public int Cooldown { get; set; }

                [JsonProperty(PropertyName = "Block teleport if player is hostile")]
                public bool Hostile { get; set; }

                [JsonProperty(PropertyName = "Block teleport if player is building blocked")]
                public bool BuildingBlocked { get; set; }

                [JsonProperty(PropertyName = "Teleport command name (Outpost)")]
                public string OutpostCommand { get; set; }

                [JsonProperty(PropertyName = "Teleport command name (Bandit Camp)")]
                public string BanditCommand { get; set; }
                
                [JsonProperty(PropertyName = "Teleport cancel command name")]
                public string CancelCommand { get; set; }

                [JsonProperty(PropertyName = "VIP Times")]
                public Dictionary<string, VIP> VIPTimes { get; set; }

                public class VIP
                {
                    [JsonProperty(PropertyName = "Amount of time to wait before teleporting (seconds)")]
                    public int Wait { get; set; }

                    [JsonProperty(PropertyName = "Cooldown time (seconds)")]
                    public int Cooldown { get; set; }
                }
            }

            public class CustomSpawnPoint
            {
                [JsonProperty(PropertyName = "The name of this area")]
                public string Name { get; set; }
                    
                [JsonProperty(PropertyName = "Is this area enabled?")]
                public bool Enabled { get; set; }
                
                [JsonProperty(PropertyName = "Allow teleporting to this area")]
                public bool CanTeleportTo { get; set; }
                
                [JsonProperty(PropertyName = "Allow respawning at this area")]
                public bool CanRespawnAt { get; set; }
                    
                [JsonProperty(PropertyName = "World position")]
                public Vector3 WorldPosition { get; set; }
                    
                [JsonProperty(PropertyName = "Radius of points generated")]
                public float Radius { get; set; }
                   
                [JsonProperty(PropertyName = "Names of potential raycast target valid for points")]
                public string[] RaycastTargets { get; set; }
                
                [JsonIgnore]
                public SpawnManager Manager { get; set; }
                
                [JsonIgnore]
                public NetworkableId Id { get; set; }

                public void Setup(int index)
                {
                    Id = new NetworkableId((ulong)(113 + index));
                    Manager = new SpawnManager(WorldPosition, Radius, RaycastTargets);
                    
                    if (Manager.Count == 0)
                    {
                        Interface.Oxide.LogInfo("[{0}] {1}", "CompoundTeleport", $"Failed to generate spawn points at {Name}");
                        Enabled = false;
                    }
                    else Interface.Oxide.LogInfo("[{0}] {1}", "CompoundTeleport", $"Generated {Manager.Count} spawn points at {Name}");
                }
                    
            }
            
            public class RespawnOptions
            {
                [JsonProperty(PropertyName = "Allow respawning at Outpost")]
                public bool AllowOutpost { get; set; }

                [JsonProperty(PropertyName = "Allow respawning at Bandit Camp")]
                public bool AllowBandit { get; set; }

                [JsonProperty(PropertyName = "Disable respawn options if player is hostile")]
                public bool DisableIfHostile { get; set; }

                [JsonProperty(PropertyName = "Cooldown time (seconds)")]
                public int Cooldown { get; set; }

                [JsonProperty(PropertyName = "Force respawns to (None, Outpost, Bandit)")]
                public string ForceTo { get; set; }
            }

            public class NoEscapeOptions
            {
                [JsonProperty(PropertyName = "Block teleport when raid blocked")]
                public bool RaidBlocked { get; set; }

                [JsonProperty(PropertyName = "Block teleport when combat blocked")]
                public bool CombatBlocked { get; set; }

                [JsonProperty(PropertyName = "Disable respawn options when raid blocked")]
                public bool RaidBlockedRespawn { get; set; }

                [JsonProperty(PropertyName = "Disable respawn options when combat blocked")]
                public bool CombatBlockedRespawn { get; set; }
            }

            public VersionNumber Version { get; set; }
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
                NoEscape = new ConfigData.NoEscapeOptions
                {
                    CombatBlocked = true,
                    RaidBlocked = true,
                    CombatBlockedRespawn = true,
                    RaidBlockedRespawn = true,
                },
                Teleport = new ConfigData.TeleportOptions
                {
                    BuildingBlocked = true,
                    BanditCommand = "bandit",
                    CancelCommand = "canceltp",
                    Cooldown = 300,
                    Hostile = true,
                    OutpostCommand = "outpost",
                    Wait = 10,
                    VIPTimes = new Dictionary<string, ConfigData.TeleportOptions.VIP>
                    {
                        ["compoundteleport.vip1"] = new ConfigData.TeleportOptions.VIP { Cooldown = 150, Wait = 5 },
                        ["compoundteleport.vip2"] = new ConfigData.TeleportOptions.VIP { Cooldown = 30, Wait = 3 },
                    }
                },
                Respawn = new ConfigData.RespawnOptions
                {
                    Cooldown = 300,
                    ForceTo = "None",
                    AllowBandit = true,
                    AllowOutpost = true
                },
                Custom = new List<ConfigData.CustomSpawnPoint>
                {
                    new ConfigData.CustomSpawnPoint
                    {
                        Name = "Example1",
                        Enabled = false,
                        CanTeleportTo = true,
                        CanRespawnAt = true,
                        WorldPosition = new Vector3(0, 0, 0),
                        Radius = 50,
                        RaycastTargets = new string[]{"road", "carpark", "pavement", "walkway"}
                    },
                    new ConfigData.CustomSpawnPoint
                    {
                        Name = "Example2",
                        Enabled = false,
                        CanTeleportTo = true,
                        CanRespawnAt = true,
                        WorldPosition = new Vector3(0, 0, 0),
                        Radius = 50,
                        RaycastTargets = new string[]{"road", "carpark", "pavement", "walkway"}
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(2, 0, 0))
                configData = baseConfig;

            if (configData.Version < new VersionNumber(2, 0, 6))
            {
                configData.NoEscape = baseConfig.NoEscape;
            }

            if (configData.Version < new VersionNumber(2, 0, 7))
                configData.Teleport.VIPTimes = baseConfig.Teleport.VIPTimes;

            if (configData.Version < new VersionNumber(2, 0, 16))
                configData.Custom = baseConfig.Custom;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Data
        private class Cooldown
        {
            public int respawn;
            public int command;
        }
        #endregion

        #region Localization
        private string msg(string key, ulong playerId = 0UL) => lang.GetMessage(key, this, playerId != 0UL ? playerId.ToString() : null);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Name.Outpost"] = "Outpost",
            ["Name.Bandit"] = "Bandit Camp",
            ["Error.NoPermission"] = "You do not have permission to use this command",
            ["Error.TPPending"] = "You already have a teleport pending",
            ["Error.Hostile"] = "You can not teleport whilst you are marked hostile",
            ["Error.Cooldown"] = "You must wait another <color=#ce422b>{0}</color> before teleporting again",
            ["Error.RaidBlocked"] = "You can not teleport while <color=#ce422b>raid blocked</color>!",
            ["Error.CombatBlocked"] = "You can not teleport while <color=#ce422b>combat blocked</color>!",
            ["Error.BuildingBlocked"] = "You can not teleport while <color=#ce422b>building blocked</color>!",
            ["Notification.TPPending"] = "Teleporting to <color=#ce422b>{0}</color> in <color=#ce422b>{1}</color>",
            ["Notification.NonePending"] = "You do not have any teleports pending",
            ["Notification.Cancelled"] = "Cancelled pending teleports",
            ["Notification.NoAreas"] = "No custom spawn areas have been setup",
            ["Notification.NoAreaWithName"] = "No spawn area found with the name : {0}",
            ["Notification.NoAreaSpecified"] = "No area specified"
        };
        #endregion
    }
}
