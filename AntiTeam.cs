using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("AnitTeam", "Yoshi", "1.0.8")]
    class AntiTeam : CovalencePlugin
    {
        [PluginReference]
        private Plugin Clans, AutoDemoRecord;
        private bool _clansEnabled;
        private bool _demoRecord = false;

        private string permissionName = "AntiTeam.admin";

        private string _banListID;
        private string _serverId;
        private string _orgId;

        private int _groupLimit;
        private string _reportWebhook;
        private string _notesWebhook;
        private string _alertWebhook;
        private ulong _reportColor;
        private bool _ignoreAdmins;
        private string _roleID;
        private string _BanRoleID;
        private string _serverName;

        private AutoBanRules _autoBanConfig;
        public ProximityConfig _proximityConfig;
        private FriendsConfig _friendsConfig;
        private BanConfig _banConfig;
        private ProximityKillConfig _killConfig;
        private AutoDemoRecorder _demoConfig;
        private GroupTempBanConfig _tempBans;

        public static AntiTeam _instance;
        private ProximityTracking _proximityInstance;
        private GameObject _proximityObject;

        private WebClient _client = new WebClient();
        private Dictionary<string, Parameter> alertConfigs = new Dictionary<string, Parameter>();
        public Dictionary<ulong, Dictionary<ulong, ProximitySubInfo>> ProximityData = new Dictionary<ulong, Dictionary<ulong, ProximitySubInfo>>();
        public Dictionary<ulong, Dictionary<ulong, List<string>>> AlertTracking = new Dictionary<ulong, Dictionary<ulong, List<string>>>();
        public Dictionary<ulong, List<ulong>> MarkedAsTeaming = new Dictionary<ulong, List<ulong>>();

        #region Startup
        void OnServerInitialized(bool initial)
        {
            _instance = this;
            if(Interface.Oxide.DataFileSystem.ExistsDatafile("Alerts_Data"))
                AlertTracking = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<ulong, List<string>>>>("Alerts_Data");

            _demoConfig = config.AutoDemoRecorderConfig;

            _groupLimit = config.groupLimit;
            _reportWebhook = config.ReportWebhook;
            _alertWebhook = config.AlertsWebhooks;
            _reportColor = config.ReportColor;
            _tempBans = config.tempBans;

            _autoBanConfig = config.autoBanRules;
            _notesWebhook = config.noteConfig.NotesWebhook;
            _proximityConfig = config.proximityConfig;
            _friendsConfig = config.steamFriendsConfig;
            _banConfig = config.banConfig;
            _killConfig = config.proximityKillConfig;
            _ignoreAdmins = config.AdminBypass;

            _serverName = config.ServerName;
            _roleID = config.RoleID;
            _BanRoleID = config.BanRoleID;
            _banListID = _banConfig.BanlistID;
            if (_proximityConfig.Enabled)
            {
                _proximityObject = new GameObject();
                _proximityInstance = _proximityObject.AddComponent<ProximityTracking>();
                _proximityInstance.InitTracking();

                if(Interface.Oxide.DataFileSystem.ExistsDatafile("Proximity_Data"))
                    ProximityData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<ulong, ProximitySubInfo>>>("Proximity_Data");
            }

            _client.SetFriendsCheck(config.steamFriendsConfig.Enabled);

            #region gross

            if (!string.IsNullOrEmpty(_banConfig.Token))
                _client.SetToken(_banConfig.Token);
            else
            {
                if(_autoBanConfig.BattlemetricsBan)
                {
                    PrintError("Battlemetrics ban is enabled but no token was given, disabling battlemetrics banning");
                    _autoBanConfig.BattlemetricsBan = false;
                }
            }

            if(String.IsNullOrEmpty(_friendsConfig.SteamAPIKey) && _friendsConfig.Enabled)
            {
                PrintError("Check friends is enabled but no api key was given! disabling friends check");
                _friendsConfig.Enabled = false;
            }

            if (!Clans)
            {
                PrintWarning("Clans plugin not found, disabling clans support");
                _clansEnabled = false;
            }
            else
            {
                PrintWarning("Clans plugin found, enabling clans support");
                _clansEnabled = true;
            }

            if(!AutoDemoRecord)
            {
                PrintWarning("Auto Demo Recorder plugin not found, disabling demo support");
            }
            else
            {
                if (_demoConfig.Enabled)
                {
                    PrintWarning("Auto Demo Recorder plugin found, enabling demo support");
                    _demoRecord = true;
                }
            }

            #endregion

            if ((RelationshipManager.maxTeamSize <= _groupLimit) || !_tempBans.Enabled)
                Unsubscribe(nameof(OnPlayerConnected));

            alertConfigs = _autoBanConfig.parameters;
            alertConfigs.Add("SteamFriends", new Parameter(config.steamFriendsConfig.weight, true, 0, false, false, false, false));

            AddCovalenceCommand("prox", nameof(ProximityCommand));
            AddCovalenceCommand("alerts", nameof(AlertsCommand));
            AddCovalenceCommand("friends", nameof(FriendsCommand));
            AddCovalenceCommand("voicelogs", nameof(VoiceLogs));
            permission.RegisterPermission(permissionName, this);

            InitBattlemetrics();
        }

        void Unload()
        {
            _instance = null;
            _proximityInstance.Destroy();
            MonoBehaviour.Destroy(_proximityObject);

            foreach (var obj in activeWatchers.Values)
            {
                if (obj != null)
                    obj.Destroy();
            }
        }

        void OnNewSave(string filename)
        {
            if(Interface.Oxide.DataFileSystem.ExistsDatafile("Proximity_Data"))
                Interface.Oxide.DataFileSystem.WriteObject("Proximity_Data", new Dictionary<ulong, Dictionary<ulong, ProximitySubInfo>>());

            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Alerts_Data"))
                Interface.Oxide.DataFileSystem.WriteObject("Alerts_Data", new Dictionary<ulong, Dictionary<ulong, List<string>>>());
        }
        #endregion

        #region Config

        public Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "ignore admins (true/false)")]
            public bool AdminBypass = true;

            [JsonProperty(PropertyName = "group limit")]
            public int groupLimit = 3;

            [JsonProperty(PropertyName = "The webhook where the teaming alerts will be sent to (leave blank to disable)")]
            public string AlertsWebhooks = "";

            [JsonProperty(PropertyName = @"Server name sent with the alert embed (put ""default"" to use the servers host name, or blank not include any name)")]
            public string ServerName = "default";

            [JsonProperty(PropertyName = "Role ID, the id of the role you want pinged with an alert (leave blank to disable)")]
            public string RoleID = "";

            [JsonProperty(PropertyName = "Role ID, the id of the role you want pinged for the automatic ban alerts")]
            public string BanRoleID = "";

            [JsonProperty(PropertyName = "The webhook where the F7 teaming reports will be sent to (leave blank to disable)")]
            public string ReportWebhook = "";

            [JsonProperty(PropertyName = "Report Color, the color of the teaming report embed")]
            public ulong ReportColor = 0xFF0000;

            [JsonProperty(PropertyName = "When a player gives a note to another player, this feature will log the note contents and player info to discord")]
            public NoteLoggingConfig noteConfig = new NoteLoggingConfig();

            [JsonProperty(PropertyName = "Team kill config options")]
            public ProximityKillConfig proximityKillConfig = new ProximityKillConfig();

            [JsonProperty(PropertyName = "Proximity config options")]
            public ProximityConfig proximityConfig = new ProximityConfig();

            [JsonProperty(PropertyName = "Friends check config options")]
            public FriendsConfig steamFriendsConfig = new FriendsConfig();

            [JsonProperty(PropertyName = "Temp bans for players that are in a group exceeding the limit (requires the team UI to not be limited)")]
            public GroupTempBanConfig tempBans = new GroupTempBanConfig();

            [JsonProperty(PropertyName = "Auto demo recoreder config options")]
            public AutoDemoRecorder AutoDemoRecorderConfig = new AutoDemoRecorder();

            [JsonProperty(PropertyName = "Battlemetric ban options")]
            public BanConfig banConfig = new BanConfig();

            [JsonProperty(PropertyName = "Parameters for auto bans")]
            public AutoBanRules autoBanRules = new AutoBanRules();

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    steamFriendsConfig = new FriendsConfig()
                    {
                        Enabled = true,
                        weight = 0.5f,
                        SteamAPIKey = "",
                        MinimumFriendTime = 0
                    },

                    noteConfig = new NoteLoggingConfig()
                    {
                        Enabled = true,
                        NotesWebhook = ""
                    },

                    AutoDemoRecorderConfig = new AutoDemoRecorder()
                    {
                        demoLength = 2,
                        Enabled = false,
                        DemoWebhook = ""
                    },

                    tempBans = new GroupTempBanConfig()
                    {
                        Enabled = false,
                        BanLength = 10,
                        MinutesInGroup = 3,
                        BanMessage = "Temp banned for violating the group limit",
                        TempBanWebhook = "",
                        TempBanColor = 0x14F8FF
                    },

                    proximityKillConfig = new ProximityKillConfig()
                    {
                        MaximumDistance = 5,
                        MaximumHeight = 3,
                        MinimumDistanceToTarget = 20,
                        MaximumAngle = 140
                    },

                    proximityConfig = new ProximityConfig()
                    {
                        Enabled = true,
                        SaveToFile = true,
                        WipeFileOnWipe = true,
                        TrackKills = true,
                        HasToBeVisible = false,
                        ProximityDistance = 20,
                        ProximityKillTrigger = 3,
                        ProximityKillWeight = 0.3f,
                        ProximityTimeTrigger = 900,
                        ProximityTimeWeight = 0.3f
                    },

                    banConfig = new BanConfig()
                    {
                        Token = "",
                        BanlistID = "",
                        BanReason = "Banned for teaming, banned for {{timeLeft}}",
                        Note = "Automated teaming ban, proof =>"
                    },

                    autoBanRules = new AutoBanRules()
                    {
                        AutoBanWebhook = "",
                        AutoBanColor = 0x000000,
                        AutoBanWeight = 1,
                        BattlemetricsBan = true,
                        NativeBan = false,
                        EnableAutoBan = false,
                        banLength = 7,
                        teamBan = true,
                        parameters = new Dictionary<string, Parameter>
                        {
                            {"Team Raid", new Parameter(0.5f, true, 0x000000, true, true, false, true) },
                            {"Team Kill", new Parameter(0.5f, true, 0xFF0000, true, true, false, true) },
                            {"Code Lock", new Parameter(0.5f, true, 0x30FF46, false, false, false, true) },
                            {"Cupboard", new Parameter(0.1f, true, 0x8A6000, false, false, false, true) },
                            {"Shared Vehicle", new Parameter(0.3f, true, 0x4F008A, false, true, false, true) },
                            {"Turret", new Parameter(0.3f, true, 0xFF9414, false, false, true, true) },
                            {"Revive", new Parameter(0.1f, true, 0x14F8FF, true, true, true, true) },
                            {"Looting", new Parameter(0.2f, true, 0x2914FF, true, true, false, true) },
                            {"Team Accept", new Parameter(0.4f, true, 0x14FF74, false, true, false, true) },
                            {"Relationship Alert", new Parameter(0.2f, true, 0xB026FF, false, true, true, true) },
                            {"Voice Chat", new Parameter(0.1f, true, 0xCDE500, false, true, false, true) },
                            {"Bag", new Parameter(0.2f, true, 0xFF5317, false, true, false, true) }
                        }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                foreach(var alert in Configuration.DefaultConfig().autoBanRules.parameters)
                {
                    if (!config.autoBanRules.parameters.ContainsKey(alert.Key))
                        config.autoBanRules.parameters.Add(alert.Key, alert.Value);
                }

                SaveConfig();
            }
            catch(Exception e)
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        #endregion

        #region Proximity Kills

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (!_proximityConfig.TrackKills)
                return;

            if (info == null || player == null || !player.userID.IsSteamId()) return;
            if (info.InitiatorPlayer == null || info.InitiatorPlayer == player) return;
            if (!info.InitiatorPlayer.userID.IsSteamId()) return;
            var Initiator = info.Initiator as BasePlayer;

            if (!PlayerCheck(player, Initiator))
                return;

            if (IsPlayerFriendly(player.userID, Initiator.userID))
                return;

            _proximityInstance.getProximityDataFiltered(player.userID, Initiator.userID).selfKills++;

            List<BasePlayer> list = Facepunch.Pool.GetList<BasePlayer>();
            BaseNetworkable.GetCloseConnections(player.transform.position, _proximityConfig.ProximityDistance, list);
            foreach (BasePlayer otherPlayer in list)
            {
                if ((otherPlayer == player) || (otherPlayer == Initiator))
                    continue;

                if (IsPlayerFriendly(otherPlayer.userID, Initiator.userID))
                    return;

                if (_proximityConfig.HasToBeVisible && !AntiTeam.IsPlayerVisibleToUs(Initiator, otherPlayer))
                    return;

                _proximityInstance.getProximityDataFiltered(Initiator.userID, otherPlayer.userID).proxKills++;
            }
        }

        #endregion

        #region Alert Methods

            #region Team Kill
        List<ulong> IsOnCoolDown = new List<ulong>();
        void OnEntityTakeDamage(BasePlayer entity, HitInfo info)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Team Kill", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (info.InitiatorPlayer == null)
                return;
            if (!entity.userID.IsSteamId())
                return;
            if (!info.InitiatorPlayer.userID.IsSteamId())
                return;
            if (info.ProjectileID == 0)
                return;

            var player = info.InitiatorPlayer;
            var target = entity;
            if (target == player)
                return;

            List<BasePlayer> list = Facepunch.Pool.GetList<BasePlayer>();
            BaseNetworkable.GetCloseConnections(player.transform.position, RelationshipManager.seendistance, list);
            foreach (var subPlayer in list)
            {
                if (_ignoreAdmins && subPlayer.IsAdmin)
                    continue;
                if (IsPlayerFriendly(player.userID, subPlayer.userID))
                    continue;
                if (target == subPlayer)
                    continue;
                if (subPlayer == player)
                    continue;
                if (!subPlayer.userID.IsSteamId())
                    continue;
                if (subPlayer.IsSleeping())
                    continue;
                if (!subPlayer.IsConnected)
                    continue;
                
                var playerPos = player.transform.position;
                var subPlayerPos = subPlayer.transform.position;
                var targetPlayerPos = target.transform.position;

                Vector3 direction = subPlayer.eyes.HeadForward();
                direction.y = target.eyes.HeadForward().y;
                subPlayerPos.y = targetPlayerPos.y;

                float difference = Vector3.Angle(direction, targetPlayerPos - subPlayerPos);
                float aimDifference = Vector3.Angle(direction, playerPos - subPlayerPos);

                if (aimDifference > _killConfig.MaximumAngle)
                    continue;

                if (difference <= 2)
                {
                    subPlayerPos = subPlayer.transform.position;
                    if (subPlayer.GetActiveItem() == null)
                        continue;
                    if (subPlayer.GetActiveItem().GetHeldEntity() is BaseProjectile)
                    {
                        if (IsOnCoolDown.Contains(subPlayer.userID))
                            continue;

                        if (!IsPlayerVisibleToUs(player, subPlayer))
                            continue;

                        var distance = Vector3.Distance(playerPos, subPlayerPos);
                        var angle = Vector3.Angle(playerPos, subPlayerPos);
                        var playerGun = player.GetActiveItem().info.name;
                        var subPlayerGun = subPlayer.GetActiveItem().info.name;
                        var distanceToTarget = Math.Max(Vector3.Distance(targetPlayerPos, playerPos), Vector3.Distance(targetPlayerPos, subPlayerPos));

                        var YAxis = Math.Sin(angle) * distance;
                        var XAxis = Math.Cos(angle) * distance;

                        if (Math.Abs(YAxis) >= _killConfig.MaximumHeight)
                            continue;
                        if (Math.Abs(XAxis) >= _killConfig.MaximumDistance)
                            continue;
                        if (Math.Abs(distanceToTarget) <= _killConfig.MinimumDistanceToTarget)
                            continue;

                        ProximityPacket packet = new ProximityPacket()
                        {
                            Angle = angle,
                            Distance = distance,
                            playerGun = playerGun,
                            subPlayerGun = subPlayerGun,
                            targetPlayer = target.userID,
                            targetPlayerDistance = distanceToTarget
                        };

                        string alertType = "Team Kill";
                        var players = new List<ulong> { player.userID, subPlayer.userID };

                        string Optinfo = $"{player.displayName} and {subPlayer.displayName} were detected teaming up in PVP, targeted player => {target.displayName} - {target.userID}";
                        AlertPacket alert = new AlertPacket(players, alertType, packet, playerPos.ToString(), Optinfo);

                        var alerts = _instance.getAlertData(players[0], players[1]);

                        _instance.SendToDiscord(alert);
                        if (_instance.alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                            alerts.Add(alertType);

                        if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                        {
                            StartDemoRecording(player, $"Team Kill - {player.userID} {subPlayer.userID}");
                        }

                        var id = subPlayer.userID;
                        IsOnCoolDown.Add(subPlayer.userID);
                        timer.Once(60f, () =>
                        {
                            IsOnCoolDown.Remove(id);
                        });
                    }
                }
            }
        }
        #endregion

            #region Raid Detection

        private List<ulong> justReported = new List<ulong>();
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Team Raid", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (_ignoreAdmins && player.IsAdmin)
                return;
            if (justReported.Contains(player.userID))
                return;

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 30f, LayerMask.GetMask("Default", "Construction")))
            {
                var hitEntity = hit.GetEntity();
                if (hitEntity == null)
                    return;

                var combatEntity = hitEntity.GetComponent<BaseCombatEntity>();
                if (combatEntity != null)
                {
                    if (IsPlayerFriendly(player.userID, combatEntity.OwnerID))
                        return;

                    if (combatEntity.OwnerID == player.userID || !combatEntity.OwnerID.IsSteamId())
                        return;

                    if (activeWatchers.ContainsKey(player.userID))
                    {
                        activeWatchers[player.userID].Reset();
                        return;
                    }

                    activeWatchers.Add(player.userID, new RaidWatch(player, combatEntity.OwnerID));
                }
            }
        }

        void CheckRaiding(int ammoType, BasePlayer player)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Team Raid", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (_ignoreAdmins && player.IsAdmin)
                return;

            if (justReported.Contains(player.userID))
                return;

            if (ammoType == -1321651331)
            {
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 30f, LayerMask.GetMask("Default", "Construction")))
                {
                    var hitEntity = hit.GetEntity();
                    if (hitEntity == null)
                        return;

                    var combatEntity = hitEntity.GetComponent<BaseCombatEntity>();
                    if (combatEntity != null)
                    {
                        if (IsPlayerFriendly(player.userID, combatEntity.OwnerID))
                            return;

                        if (combatEntity.OwnerID == player.userID || !combatEntity.OwnerID.IsSteamId())
                            return;

                        if (activeWatchers.ContainsKey(player.userID))
                        {
                            activeWatchers[player.userID].Reset();
                            return;
                        }

                        activeWatchers.Add(player.userID, new RaidWatch(player, combatEntity.OwnerID));
                    }
                }
            }
        }

        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Team Raid", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (_ignoreAdmins && player.IsAdmin)
                return;

            if (justReported.Contains(player.userID))
                return;

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 30f, LayerMask.GetMask("Default", "Construction")))
            {
                var hitEntity = hit.GetEntity();
                if (hitEntity == null)
                    return;

                var combatEntity = hitEntity.GetComponent<BaseCombatEntity>();
                if (combatEntity != null)
                {
                    if (IsPlayerFriendly(player.userID, combatEntity.OwnerID))
                        return;

                    if (combatEntity.OwnerID == player.userID || !combatEntity.OwnerID.IsSteamId())
                        return;

                    if (activeWatchers.ContainsKey(player.userID))
                    {
                        activeWatchers[player.userID].Reset();
                        return;
                    }

                    activeWatchers.Add(player.userID, new RaidWatch(player, combatEntity.OwnerID));
                }
            }
        }

        private Dictionary<ulong, RaidWatch> activeWatchers = new Dictionary<ulong, RaidWatch>();
        public class RaidWatch
        {
            private BasePlayer player;
            private ulong raider;
            private DateTime startTime;
            private Dictionary<ulong, RaidInfo> raidData = new Dictionary<ulong, RaidInfo>();

            public RaidWatch(BasePlayer playerIn, ulong raiderIn)
            {

                this.startTime = DateTime.Now;
                this.player = playerIn;
                this.raider = raiderIn;
                playerIn.InvokeRepeating(CheckSurroundings, 0, 1);
            }
            private void CheckSurroundings()
            {
                if (player == null || player.IsDead())
                {
                    this.Destroy();
                    return;
                }

                if ((DateTime.Now - startTime).TotalMinutes >= 3)
                {
                    this.Destroy();
                    return;
                }

                List<BasePlayer> list = Facepunch.Pool.GetList<BasePlayer>();
                BaseNetworkable.GetCloseConnections(player.transform.position, RelationshipManager.seendistance, list);
                foreach (BasePlayer otherPlayer in list)
                {
                    if (_instance._ignoreAdmins && otherPlayer.IsAdmin)
                        continue;
                    if (_instance.IsPlayerFriendly(player.userID, otherPlayer.userID))
                        continue;
                    if (_instance.IsPlayerFriendly(raider, otherPlayer.userID))
                        continue;
                    if (otherPlayer == player)
                        continue;
                    if (otherPlayer == null)
                        continue;
                    if (otherPlayer.IsSleeping())
                        continue;
                    if (otherPlayer.IsDead())
                        continue;
                    if (!otherPlayer.IsConnected)
                        continue;
                    if (otherPlayer.userID == raider)
                        continue;

                    var playerPos = player.transform.position;
                    var otherPlayerPos = otherPlayer.transform.position;
                    if (Math.Abs(Vector3.Distance(playerPos, otherPlayerPos)) <= 10)
                    {
                        if (IsPlayerVisibleToUs(player, otherPlayer))
                        {
                            if (!raidData.ContainsKey(otherPlayer.userID))
                                raidData.Add(otherPlayer.userID, new RaidInfo { inProximity = 0, lookingAt = 0 });

                            float angel = Vector3.Angle(player.eyes.HeadRay().direction, otherPlayerPos - playerPos);
                            if (Mathf.Abs(angel) <= 60)
                                raidData[otherPlayer.userID].lookingAt++;
                            else
                            {
                                angel = Vector3.Angle(otherPlayer.eyes.HeadRay().direction, playerPos - otherPlayerPos);
                                if (Mathf.Abs(angel) <= 60)
                                    raidData[otherPlayer.userID].lookingAt++;
                            }

                            raidData[otherPlayer.userID].inProximity++;
                            if (raidData[otherPlayer.userID].inProximity > 30 && raidData[otherPlayer.userID].lookingAt > 10)
                            {
                                var distance = Vector3.Distance(player.transform.position, otherPlayer.transform.position);
                                var angle = Vector3.Angle(playerPos, otherPlayerPos);
                                Item weapon = null;
                                foreach (var item in otherPlayer.inventory.containerBelt.itemList)
                                {
                                    var wcheck = item.GetHeldEntity();
                                    if (wcheck == null)
                                        continue;

                                    var wfcheck = wcheck.GetComponent<BaseProjectile>();
                                    if (wfcheck != null)
                                    {
                                        weapon = item;
                                        break;
                                    }
                                }
                                if (weapon == null)
                                    continue;

                                var subPlayerGun = weapon.info.name;
                                weapon = null;
                                foreach (var item in player.inventory.containerBelt.itemList)
                                {
                                    var wcheck = item.GetHeldEntity();
                                    if (wcheck == null)
                                        continue;

                                    var wfcheck = wcheck.GetComponent<BaseProjectile>();
                                    if (wfcheck != null)
                                    {
                                        weapon = item;
                                        break;
                                    }
                                }

                                if (weapon == null)
                                    continue;
                                var playerGun = weapon.info.name;
                                ProximityPacket killConfig = new ProximityPacket
                                {
                                    Distance = distance,
                                    Angle = angle,
                                    playerGun = playerGun,
                                    subPlayerGun = subPlayerGun,
                                    targetPlayerDistance = 0,
                                    targetPlayer = raider
                                };

                                string alertType = "Team Raid";
                                var players = new List<ulong> { player.userID, otherPlayer.userID };

                                string Optinfo = $"{player.displayName} and {otherPlayer.displayName} were detected raiding together, owner of base being raided => {raider}";
                                AlertPacket alert = new AlertPacket(players, alertType, killConfig, playerPos.ToString(), Optinfo);

                                if (_instance._demoRecord && _instance.alertConfigs[alertType].AutoDemoRecord)
                                {
                                    _instance.StartDemoRecording(player, $"Team Raid - {player.userID} {otherPlayer.userID}");
                                }

                                var alerts = _instance.getAlertData(players[0], players[1]);

                                _instance.SendToDiscord(alert);
                                if (_instance.alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                                    alerts.Add(alertType);

                                _instance.justReported.Add(player.userID);
                                _instance.justReported.Add(otherPlayer.userID);
                                this.Destroy();
                            }
                        }

                    }
                }
            }

            public void Destroy()
            {
                if (player == null)
                    return;

                _instance.activeWatchers.Remove(player.userID);
                player.CancelInvoke(CheckSurroundings);
            }

            public void Reset()
            {
                startTime = DateTime.Now;
            }
        }

        public class RaidInfo
        {
            public int lookingAt;
            public int inProximity;
        }

        #endregion

            #region Voice Chat

        private Dictionary<ulong, Dictionary<ulong, int>> voiceChatData = new Dictionary<ulong, Dictionary<ulong, int>>();
        void OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            if (_ignoreAdmins && player.IsAdmin)
                return;

            Parameter parameter;
            if (alertConfigs.TryGetValue("Voice Chat", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (player.InSafeZone() || player.IsBuildingAuthed())
                return;

            List<BasePlayer> list = Facepunch.Pool.GetList<BasePlayer>();
            BaseNetworkable.GetCloseConnections(player.transform.position, 5, list);
            foreach (BasePlayer otherPlayer in list)
            {
                if (otherPlayer != player && !otherPlayer.isClient && otherPlayer.IsAlive() && !otherPlayer.IsSleeping() && !otherPlayer.limitNetworking)
                {
                    if (IsPlayerFriendly(otherPlayer.userID, player.userID))
                        return;

                    if (otherPlayer.InSafeZone() || otherPlayer.IsBuildingAuthed())
                        return;

                    if (_ignoreAdmins && otherPlayer.IsAdmin)
                        return;

                    if (!IsPlayerVisibleToUs(player, otherPlayer))
                        return;

                    IncreaseVoiceChatDataFiltered(player, otherPlayer);
                }
            }
        }

        public void IncreaseVoiceChatDataFiltered(BasePlayer player, BasePlayer otherPlayer)
        {
            var ent1 = player.userID;
            var ent2 = otherPlayer.userID;

            if (ent2 > ent1)
            {
                var tmp = ent2;
                ent2 = ent1;
                ent1 = tmp;
            }

            Dictionary<ulong, int> proximityData;
            if (voiceChatData.TryGetValue(ent1, out proximityData))
            {
                int time;
                if (!proximityData.TryGetValue(ent2, out time))
                    proximityData.Add(ent2, 1);
                else
                {
                    proximityData[ent2]++;

                    if ((time++ / 10) == 60)
                    {
                        SendVoiceChatAlert(player, otherPlayer);
                        proximityData[ent2] += 10;
                    }
                }
            }
            else
            {
                voiceChatData.Add(ent1, new Dictionary<ulong, int> { { ent2, 1 } });
            }
        }

        public int GetVoiceChatDataFiltered(ulong ent1, ulong ent2)
        {
            if (ent2 > ent1)
            {
                var tmp = ent2;
                ent2 = ent1;
                ent1 = tmp;
            }

            int time = 0;
            Dictionary<ulong, int> proximityData;
            if (voiceChatData.TryGetValue(ent1, out proximityData))
                proximityData.TryGetValue(ent2, out time);

            return time;
        }

        public void SendVoiceChatAlert(BasePlayer player, BasePlayer otherPlayer)
        {
            if (_ignoreAdmins && (player.IsAdmin || otherPlayer.IsAdmin))
                return;

            if (IsPlayerFriendly(player.userID, otherPlayer.userID))
                return;

            string alertType = "Voice Chat";
            List<ulong> players = new List<ulong> { player.userID, otherPlayer.userID };

            string OptInfo = $"{player.displayName} and {otherPlayer.displayName} have talked through voice chat for over 60 seconds";
            AlertPacket alert = new AlertPacket(players, alertType, player.transform.position.ToString(), OptInfo);

            var alerts = getAlertData(players[0], players[1]);

            if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
            {
                StartDemoRecording(player, $"Voice Chat Alert - {player.userID} {otherPlayer.userID}");
            }

            if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                alerts.Add(alertType);
            SendToDiscord(alert);
        }

        #endregion

            #region Bag

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null)
                return;

            var player = plan.GetOwnerPlayer();
            var Entity = go.ToBaseEntity();

            if (Entity == null || player == null)
                return;

            if(Entity is SleepingBag)
                HandleNewBag(player, Entity as SleepingBag, player.userID);
        }

        void CanAssignBed(BasePlayer player, SleepingBag bag, ulong targetPlayerId) => HandleNewBag(player, bag, targetPlayerId);
        void HandleNewBag(BasePlayer player, SleepingBag bag, ulong targetPlayerId)
        {
            var bags = SleepingBag.sleepingBags.Where(x => x.buildingID == bag.buildingID && x != bag).Select(x => x.deployerUserID).Distinct().ToList();
            if (!bags.Contains(targetPlayerId))
                bags.Add(targetPlayerId);

            if (bags.Count() > _groupLimit)
            {
                if (_ignoreAdmins && player.IsAdmin)
                    return;

                if (bags.Count > _groupLimit)
                {
                    var otherPlayer = BasePlayer.FindByID(targetPlayerId);
                    string otherPlayerName = otherPlayer != null ? otherPlayer.displayName : targetPlayerId.ToString();

                    string alertType = "Bag";
                    if (bags.Count - _groupLimit > 1)
                    {
                        var existingCombinations = from item1 in bags
                                                   select new PlayerPair(item1, player.userID);
                        foreach (var pair in existingCombinations)
                        {
                            if (IsPlayerFriendly(pair.player1, pair.player2))
                                continue;

                            var alerts = getAlertData(pair.player1, pair.player2);
                            if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                                alerts.Add(alertType);
                        }

                        if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                        {
                            string players = "";
                            foreach (var user in bags)
                                players += $"{user}, ";
                            players = players.Remove(players.Length - 2, 2);

                            StartDemoRecording(player, $"Bag - {players}");
                        }

                        SendToDiscord(new AlertPacket(bags, alertType, bag.transform.position.ToString(), $"{player.displayName} has bagged {otherPlayerName} in a base that already has {_groupLimit} players bagged"), false);
                        return;
                    }

                    var combinations = from item1 in bags
                                       from item2 in bags
                                       where item1 < item2
                                       select new PlayerPair(item1, item2);

                    foreach (var pair in combinations)
                    {
                        if (IsPlayerFriendly(pair.player1, pair.player2))
                            continue;

                        var alerts = getAlertData(pair.player1, pair.player2);
                        if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                            alerts.Add(alertType);
                    }

                    if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                    {
                        string players = "";
                        foreach (var user in bags)
                            players += $"{user}, ";
                        players = players.Remove(players.Length - 2, 2);

                        StartDemoRecording(player, $"Bag - {players}");
                    }

                    SendToDiscord(new AlertPacket(bags, alertType, bag.transform.position.ToString(), $"{player.displayName} has bagged {otherPlayerName} in a base that already has {_groupLimit} players bagged"));
                }
            }
        }

        #endregion

            #region Code Lock
        void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Code Lock", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (_ignoreAdmins && player.IsAdmin)
                return;
            if (codeLock.code != code)
                return;

            var authlist = codeLock.whitelistPlayers;
            if (!authlist.Contains(player.userID))
                authlist.Add(player.userID);

            if (authlist.Count > _groupLimit)
            {
                string alertType = "Code Lock";
                if (authlist.Count - _groupLimit > 1)
                {
                    var existingCombinations = from item1 in authlist
                                               select new PlayerPair(item1, player.userID);
                    foreach (var pair in existingCombinations)
                    {
                        if (IsPlayerFriendly(pair.player1, pair.player2))
                            continue;

                        var alerts = getAlertData(pair.player1, pair.player2);
                        if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                            alerts.Add(alertType);
                    }

                    if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                    {
                        string players = "";
                        foreach (var user in authlist)
                            players += $"{user}, ";
                        players = players.Remove(players.Length - 2, 2);

                        StartDemoRecording(player, $"Code Lock - {players}");
                    }

                    SendToDiscord(new AlertPacket(authlist, alertType, codeLock.transform.position.ToString(), "codelock code - " + code), false);
                    return;
                }

                var combinations = from item1 in authlist
                                   from item2 in authlist
                                   where item1 < item2
                                   select new PlayerPair(item1, item2);

                foreach (var pair in combinations)
                {
                    if (IsPlayerFriendly(pair.player1, pair.player2))
                        continue;

                    var alerts = getAlertData(pair.player1, pair.player2);
                    if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                        alerts.Add(alertType);
                }

                if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                {
                    string players = "";
                    foreach (var user in authlist)
                        players += $"{user}, ";
                    players = players.Remove(players.Length - 2, 2);

                    StartDemoRecording(player, $"Code Lock - {players}");
                }

                string OptInfo = $"{player.displayName} has authed on a codelock that already has {_groupLimit} players authed, codelock is owned by this account => {codeLock.OwnerID}";
                SendToDiscord(new AlertPacket(authlist, alertType, codeLock.transform.position.ToString(), OptInfo));
            }

            //scan
        }
        #endregion

            #region Cupboard
        void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Cupboard", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (_ignoreAdmins && player.IsAdmin)
                return;

            var authlist = privilege.authorizedPlayers.Select(x => x.userid).ToList();
            if (!authlist.Contains(player.userID))
                authlist.Add(player.userID);

            if (privilege.GetProtectedMinutes() > 0 && authlist.Count() > _groupLimit)
            {
                string alertType = "Cupboard";
                if (authlist.Count - _groupLimit > 1)
                {
                    var existingCombinations = from item1 in authlist
                                               select new PlayerPair(item1, player.userID);
                    foreach (var pair in existingCombinations)
                    {
                        if (IsPlayerFriendly(pair.player1, pair.player2))
                            continue;

                        var alerts = getAlertData(pair.player1, pair.player2);
                        if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                            alerts.Add(alertType);
                    }

                    if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                    {
                        string players = "";
                        foreach (var user in authlist)
                            players += $"{user}, ";
                        players = players.Remove(players.Length - 2, 2);

                        StartDemoRecording(player, $"Cupboard - {players}");
                    }

                    SendToDiscord(new AlertPacket(authlist, alertType, player.transform.position.ToString(), ""), false);
                    return;
                }

                var combinations = from item1 in authlist
                                   from item2 in authlist
                                   where item1 < item2
                                   select new PlayerPair(item1, item2);

                foreach (var pair in combinations)
                {
                    if (IsPlayerFriendly(pair.player1, pair.player2))
                        continue;

                    var alerts = getAlertData(pair.player1, pair.player2);
                    if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                        alerts.Add(alertType);
                }

                if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                {
                    string players = "";
                    foreach (var user in authlist)
                        players += $"{user}, ";
                    players = players.Remove(players.Length - 2, 2);

                    StartDemoRecording(player, $"Cupboard - {players}");
                }

                string OptInfo = $"{player.displayName} has authed on a cupboard that already has {_groupLimit} players authed, cupboard is owned by this account => {privilege.OwnerID}";
                SendToDiscord(new AlertPacket(authlist, alertType, player.transform.position.ToString(), OptInfo));
            }
        }
        #endregion

            #region Shared Vehicle

        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Shared Vehicle", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (_ignoreAdmins && player.IsAdmin)
                return;

            if (entity.GetParentEntity() == null)
                return;

            if (player.InSafeZone())
                return;

            if (!player.userID.IsSteamId())
                return;

            if (entity.GetParentEntity() is BaseVehicle)
            {
                var vehicle = entity.GetParentEntity().GetComponent<BaseVehicle>();
                List<ulong> mounted = new List<ulong>();
                foreach(var mount in vehicle.mountPoints)
                {
                    if(mount != null)
                    {
                        var mountedPlayer = mount.mountable.GetMounted();
                        if (mountedPlayer != null && mountedPlayer.userID.IsSteamId() && (!mountedPlayer.IsAdmin || !_ignoreAdmins))
                            mounted.Add(mountedPlayer.userID);
                    }
                }

                if (!mounted.Contains(player.userID))
                    mounted.Add(player.userID);

                if (mounted.Count <= _groupLimit)
                    return;

                string alertType = "Shared Vehicle";
                AlertPacket alert = new AlertPacket(mounted, alertType, entity.transform.position.ToString(), "");

                var combinations = from item1 in mounted
                                   from item2 in mounted
                                   where item1 < item2
                                   select new PlayerPair(item1, item2);

                foreach (var pair in combinations)
                {
                    if (IsPlayerFriendly(pair.player1, pair.player2))
                        continue;

                    var alerts = getAlertData(pair.player1, pair.player2);
                    if (_instance.alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                        alerts.Add(alertType);
                }

                if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                {
                    string players = "";
                    foreach (var user in mounted)
                        players += $"{user}, ";
                    players = players.Remove(players.Length - 2, 2);

                    StartDemoRecording(player, $"Shared Vehicle - {players}");
                }

                string OptInfo = $"{player.displayName} has mounted in a vehicle that already has {_groupLimit} players mounted";
                SendToDiscord(new AlertPacket(mounted, alertType, entity.transform.position.ToString(), OptInfo), true, VehicleNameToImage(vehicle.ShortPrefabName));
            }
        }

        private string VehicleNameToImage(string shortName)
        {
            if (shortName.Contains("module"))
                return "https://cdn.discordapp.com/attachments/571758018258272268/952758640060792842/car.png";

            switch(shortName)
            {
                case "rowboat":
                    return "https://cdn.discordapp.com/attachments/571758018258272268/952758507868917840/rowboat.png";
                case "rhib":
                    return "https://rustlabs.com/img/screenshots/rhib.png";
                case "snowmobile":
                    return "https://cdn.discordapp.com/attachments/571758018258272268/952758507583725588/snowmobile.png";
                case "minicopter.entity":
                    return "https://cdn.discordapp.com/attachments/571758018258272268/952760817097539615/unknown_13.png";
                case "scraptransporthelicopter":
                    return "https://cdn.discordapp.com/attachments/571758018258272268/952758871049506918/unknown_14.png";
            }

            return "https://cdn.discordapp.com/attachments/571758018258272268/952758640060792842/car.png";
        }

        #endregion

            #region Turrets
        void OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Turret", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (_ignoreAdmins && player.IsAdmin)
                return;

            var authlist = turret.authorizedPlayers.Select(x => x.userid).ToList();
            if (!authlist.Contains(player.userID))
                authlist.Add(player.userID);

            var cupboard = turret.GetBuildingPrivilege();
            if (cupboard != null)
            {
                if (turret.GetBuildingPrivilege().GetProtectedMinutes() == 0)
                    return;
            }
            else
                return;

            if (authlist.Count > _groupLimit)
            {
                string alertType = "Turret";
                if (authlist.Count - _groupLimit > 1)
                {
                    var existingCombinations = from item1 in authlist
                                               select new PlayerPair(item1, player.userID);
                    foreach (var pair in existingCombinations)
                    {
                        if (IsPlayerFriendly(pair.player1, pair.player2))
                            continue;

                        var alerts = getAlertData(pair.player1, pair.player2);
                        if (_instance.alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                            alerts.Add(alertType);
                    }

                    if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                    {
                        string players = "";
                        foreach (var user in authlist)
                            players += $"{user}, ";
                        players = players.Remove(players.Length - 2, 2);

                        StartDemoRecording(player, $"Turret - {players}");
                    }

                    SendToDiscord(new AlertPacket(authlist, alertType, player.transform.position.ToString(), ""), false);
                    return;
                }

                var combinations = from item1 in authlist
                                   from item2 in authlist
                                   where item1 < item2
                                   select new PlayerPair(item1, item2);

                foreach (var pair in combinations)
                {
                    if (IsPlayerFriendly(pair.player1, pair.player2))
                        continue;

                    var alerts = getAlertData(pair.player1, pair.player2);
                    if (_instance.alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                        alerts.Add(alertType);
                }

                if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                {
                    string players = "";
                    foreach (var user in authlist)
                        players += $"{user}, ";
                    players = players.Remove(players.Length - 2, 2);

                    StartDemoRecording(player, $"Turret - {players}");
                }


                string OptInfo = $"{player.displayName} has authed on a turret that already has {_groupLimit} players authed, turret is owned by this account => {turret.OwnerID}";
                SendToDiscord(new AlertPacket(authlist, alertType, turret.transform.position.ToString(), OptInfo));
            }
        }
        #endregion

            #region F7 reports

        List<string> TriggerWords = new List<string>()
        {
            "team",
            "trio",
            "duo",
            "quad",
            "group"
        };

        void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message, string type)
        {
            if (String.IsNullOrEmpty(_reportWebhook))
                return;

            var text = (subject + message).ToLower();
            if (TriggerWords.Where(x => text.Contains(x)).Any())
            {
                TeamingReportPacket packet = new TeamingReportPacket()
                {
                    reported = targetId,
                    reportedBy = reporter.displayName,
                    reportedName = targetName,
                    message = message,
                    subject = subject
                };

                if (_demoRecord)
                {
                    ulong userID;
                    if (ulong.TryParse(targetId, out userID))
                    {
                        var player = BasePlayer.FindByID(userID);

                        if(player != null)
                        StartDemoRecording(player, $"Teaming Report - {targetId}");
                    }
                }

                SendReportToDiscord(packet);
            }
        }
        #endregion

            #region Revive
        //oxide moment (they actually dont have a revive hook)
        void OnPlayerRecovered(BasePlayer player)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Revive", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            List<BasePlayer> list = Facepunch.Pool.GetList<BasePlayer>();
            BaseNetworkable.GetCloseConnections(player.transform.position, 2, list);
            foreach(var reviver in list)
            {
                if (reviver.serverInput.IsDown(BUTTON.USE))
                {
                    OnPlayerRevive(reviver, player);
                }
            }
        }

        void OnPlayerRevive(BasePlayer reviver, BasePlayer player)
        {
            if (IsPlayerFriendly(reviver.userID, player.userID))
                return;

            timer.Once(5f, () =>
            {
                if (player.IsDead())
                    return;

                string alertType = "Revive";
                List<ulong> players = new List<ulong> {player.userID, reviver.userID };

                string OptInfo = $"{player.displayName} has been revived by {reviver.displayName}";
                AlertPacket alert = new AlertPacket(players, alertType, player.transform.position.ToString(), OptInfo);

                var alerts = getAlertData(players[0], players[1]);

                if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                {
                    StartDemoRecording(player, $"Revive - {reviver.userID} {player.userID}");
                }

                if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                    alerts.Add(alertType);
                SendToDiscord(alert);
            });
        }

        #endregion

            #region Looting

        List<ulong> JustReportedLooting = new List<ulong>();
        Dictionary<ulong, ulong> Looting = new Dictionary<ulong, ulong>();
        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Looting", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (entity is PlayerCorpse)
            {
                var target = entity as PlayerCorpse;
                if (_ignoreAdmins && player.IsAdmin)
                    return;
                    
                if (player.InSafeZone())
                    return;
                
                if (!target.playerSteamID.IsSteamId())
                    return;
                    
                if (Looting.ContainsKey(target.net.ID.Value))
                {
                    if (IsPlayerFriendly(player.userID, Looting[target.net.ID.Value]))
                        return;
                    
                    if (Looting[target.net.ID.Value] != player.userID)
                    {
                        if (JustReportedLooting.Contains(target.net.ID.Value))
                            return;
                        
                        string alertType = "Looting";
                        List<ulong> players = new List<ulong> { player.userID, Looting[target.net.ID.Value] };
                        
                        string name = Looting[target.net.ID.Value].ToString();
                        BasePlayer otherPlayer = BasePlayer.FindByID(Looting[target.net.ID.Value]);
                        if (otherPlayer != null)
                            name = otherPlayer.displayName;
                        
                        string OptInfo = $"{player.displayName} and {name} have been detected looting a players body together";
                        AlertPacket alert = new AlertPacket(players, alertType, player.transform.position.ToString(), OptInfo);
                        JustReportedLooting.Add(target.net.ID.Value);

                        var alerts = getAlertData(players[0], players[1]);

                        if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
                        {
                            StartDemoRecording(player, $"Looting - {player.userID} {Looting[target.net.ID.Value]}");
                        }

                        if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                            alerts.Add(alertType);
                        SendToDiscord(alert);

                    }

                    return;
                }

                var netID = target.net.ID.Value;
                Looting.Add(netID, player.userID);
                timer.Once(5f, () =>
                {
                    JustReportedLooting.Remove(netID);
                    Looting.Remove(netID);
                });
            }
        }

        #endregion

            #region Team Accept
        void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Team Accept", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (_ignoreAdmins && player.IsAdmin)
                return;

            var players = team.members.ToList();
            if (!players.Contains(player.userID))
                players.Add(player.userID);
            if (players.Count <= _groupLimit)
                return;

            string alertType = "Team Accept";
            AlertPacket alert = new AlertPacket(players, alertType, player.transform.position.ToString(), "");
            var alerts = getAlertData(player.userID, team.teamLeader);
            if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
            {
                string users = "";
                foreach (var user in players)
                    users += $"{user}, ";
                users = users.Remove(users.Length - 2, 2);

                StartDemoRecording(player, $"Team Accept - {users}");
            }

            if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                alerts.Add(alertType);
            SendToDiscord(alert);

            if ((RelationshipManager.maxTeamSize <= _groupLimit) || !_tempBans.Enabled)
                return;

            timer.Once(_tempBans.MinutesInGroup * 60, ()=>
            {
                if (team == null)
                    return;

                if(team.members.Count > _groupLimit)
                {
                    foreach (var user in team.members)
                        TempBan(user);

                    SendTempBanMessage(new AutoBanPacket(team.members, $"Temp banned for {_tempBans.BanLength} minutes for exceeding the team size"));
                    team.Disband();
                }
            });
        }
        #endregion

            #region Relationship
        void CanSetRelationship(BasePlayer player, BasePlayer otherPlayer, RelationshipManager.RelationshipType type, int weight)
        {
            Parameter parameter;
            if (alertConfigs.TryGetValue("Relationship Alert", out parameter))
            {
                if (!parameter.Enabled)
                    return;
            }
            else
                return;

            if (_ignoreAdmins && (player.IsAdmin || otherPlayer.IsAdmin))
                return;

            if (IsPlayerFriendly(player.userID, otherPlayer.userID))
                return;

            if (type != RelationshipManager.RelationshipType.Friend)
                return;

            string alertType = "Relationship Alert";
            List<ulong> players = new List<ulong> {player.userID, otherPlayer.userID };

            string OptInfo = $"{player.displayName} has set {otherPlayer.displayName} as friendly in their contacts";
            AlertPacket alert = new AlertPacket(players, alertType, player.transform.position.ToString(), OptInfo);

            var alerts = getAlertData(players[0], players[1]);

            if (_demoRecord && alertConfigs[alertType].AutoDemoRecord)
            {
                StartDemoRecording(player, $"Relationship Alert - {player.userID} {otherPlayer.userID}");
            }

            if (alertConfigs[alertType].Stackable || !alerts.Contains(alertType))
                alerts.Add(alertType);
            SendToDiscord(alert);
        }
        #endregion

        #endregion

        #region Note Tracking

        Dictionary<ulong, NoteLog> NoteLogs = new Dictionary<ulong, NoteLog>();
        void OnItemDropped(Item item, BaseEntity entity)
        {
            if (item.info.itemid == 1414245162)
            {
                if (item.GetOwnerPlayer() == null)
                    return;

                if(!NoteLogs.ContainsKey(item.uid.Value))
                    NoteLogs.Add(item.uid.Value, new NoteLog(item.GetOwnerPlayer().UserIDString, UnityEngine.Time.time));
            }
        }

        void OnItemPickup(Item item, BasePlayer player)
        {
            if (item.info.itemid == 1414245162)
            {
                NoteLog note;
                if (NoteLogs.TryGetValue(item.uid.Value, out note))
                {
                    if (player.UserIDString == note.owner)
                        return;

                    if((UnityEngine.Time.time - note.time) <= 120)
                    {
                        _client.GetFriendsList($"https://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key={config.steamFriendsConfig.SteamAPIKey}&steamid={player.UserIDString}", (data) =>
                        {
                            string friendsStatus = "Players steam friends are private";
                            if (data != null)
                            {
                                friendsStatus = "Players are not friends on steam";
                                var friends = data["friendslist"]["friends"] as JArray;
                                foreach (var friend in friends)
                                {
                                    var userID = friend["steamid"].Value<string>();

                                    if (userID == note.owner)
                                    {
                                        var FriendTime = ConvertEpoch(friend["friend_since"].Value<int>());
                                        var days = (DateTime.Now - FriendTime).TotalDays;
                                        friendsStatus = $"Players have been friends for {days} days";
                                    }
                                }
                            }

                            List<EmbedField> Fields = new List<EmbedField>();
                            Fields.Add(new EmbedField { Inline = true, Name = "Note Owner", Value = $"[{note.owner}](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={note.owner}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=false)" });
                            Fields.Add(new EmbedField { Inline = true, Name = "Note Receiver", Value = $"[{player.UserIDString}](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={player.UserIDString}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=false)" });
                            Fields.Add(new EmbedField { Inline = true, Name = "Friends Status", Value = friendsStatus });

                            var Owner = BasePlayer.FindByID(ulong.Parse(note.owner));
                            string ownerName = Owner != null ? Owner.displayName : note.owner;

                            string Title = "Note Logging";
                            Embed embed = new Embed
                            {
                                Title = Title,
                                Footer = new EmbedFooter(DateTime.Now.ToString()),
                                Color = 0x02DB5E,
                                Thumbnail = new EmbedThumbnail("https://cdn.discordapp.com/attachments/571758018258272268/952752869776429086/Note_icon.png"),
                                Fields = Fields
                            };

                            embed.Description = $"{ownerName} has given a note to {player.displayName}";
                            embed.Description += $"\n\n**Note Contents**\n```{item.text}```";

                            DiscordMessage message = new DiscordMessage();

                            message.AddEmbed(embed);
                            var JObject = JsonConvert.SerializeObject(message);
                            _client.SendMessage(_notesWebhook, JObject);
                        });
                    }

                    NoteLogs.Remove(item.uid.Value);
                }
            }
        }

        public struct NoteLog
        {
            public string owner { get; set; }
            public float time { get; set; }
            public NoteLog(string ownerIn, float timeIn)
            {
                owner = ownerIn;
                time = timeIn;
            }
        }

        #endregion

        #region Models
        //main teaming alert
        public class AlertPacket
        {
            public List<ulong> players { get; set; }
            public string AlertType { get; set; }
            public string optionalInfo { get; set; }
            public string location { get; set; }
            public ProximityPacket proximityPacket { get; set; } = null;

            public AlertPacket(List<ulong> playersIn, string AlertTypeIn, string locationIn, string optionalInfoIn)
            {
                players = playersIn;
                AlertType = AlertTypeIn;
                optionalInfo = optionalInfoIn;
                location = locationIn;
            }

            public AlertPacket(List<ulong> playersIn, string AlertTypeIn, ProximityPacket packetIn, string locationIn, string optionalInfoIn)
            {
                players = playersIn;
                AlertType = AlertTypeIn;
                proximityPacket = packetIn;
                location = locationIn;
                optionalInfo = optionalInfoIn;
            }
        }

        //replace with Tuple if oxide every supports it
        public struct PlayerPair
        {
            public ulong player1 { get; set; }
            public ulong player2 { get; set; }
            public PlayerPair(ulong player1In, ulong player2In)
            {
                player1 = player1In;
                player2 = player2In;
            }
        }

        //teaming reports
        public class AutoDemoRecorder
        {
            public bool Enabled { get; set; }
            [JsonProperty("the length of the demo recordings (minutes)")]
            public int demoLength { get; set; }
            [JsonProperty("The webhook where the demo files will be sent to")]
            public string DemoWebhook { get; set; }
        }

        //teaming reports
        public class TeamingReportPacket
        {
            public string reportedName { get; set; }
            public string reported { get; set; }
            public string reportedBy { get; set; }
            public string message { get; set; }
            public string subject { get; set; }
        }

        //Auto ban packet
        public class AutoBanPacket
        {
            public List<ulong> bannedPlayers { get; set; }
            public string notes { get; set; }

            public AutoBanPacket(List<ulong> BannedPlayersIn, string notesIn)
            {
                bannedPlayers = BannedPlayersIn;
                notes = notesIn;
            }
        }

        //proximity data
        public class ProximityPacket
        {
            public float Distance { get; set; }
            public float Angle { get; set; }
            public string playerGun { get; set; }
            public string subPlayerGun { get; set; }
            public float targetPlayerDistance { get; set; }
            public ulong targetPlayer { get; set; }
        }
        
        //auto ban parameters
        public class AutoBanRules
        {
            public bool EnableAutoBan { get; set; }
            public string AutoBanWebhook { get; set; }
            [JsonProperty("Auto ban message embed color")]
            public ulong AutoBanColor { get; set; }
            public float AutoBanWeight { get; set; }
            [JsonProperty("Team ban, when a player in a full team is teaming with another outside player, this will ban the entire team, or just the two players")]
            public bool teamBan { get; set; }
            public int banLength { get; set; }
            public bool NativeBan { get; set; }
            public bool BattlemetricsBan { get; set; }
            public Dictionary<string, Parameter> parameters { get; set; }
        }

        //alert parameters
        public class Parameter
        {
            public float Weight { get; set; }
            public bool Enabled { get; set; }
            public ulong AlertColor { get; set; }
            public bool Stackable { get; set; }
            public bool AutoDemoRecord { get; set; }
            public bool DontShowAlone { get; set; }
            public bool SendDiscordAlert { get; set; }

            public Parameter(float WeightIn, bool EnabledIn, ulong AlertColorIn, bool StackableIn, bool demoRecord, bool DontShowAloneIn, bool SendDiscordAlertIn)
            {
                AutoDemoRecord = demoRecord;
                Weight = WeightIn;
                Enabled = EnabledIn;
                AlertColor = AlertColorIn;
                Stackable = StackableIn;
                DontShowAlone = DontShowAloneIn;
                SendDiscordAlert = SendDiscordAlertIn;
            }
        }

        //Friends Info
        public class FriendsConfig
        {
            public bool Enabled { get; set; }
            [JsonProperty("Minimum Friend Time, the minimum amount of days the users have been friends to count")]
            public int MinimumFriendTime { get; set; }
            public float weight { get; set; }
            public string SteamAPIKey { get; set; }
        }

        //Proximity Config
        public class ProximityConfig
        {
            public bool Enabled { get; set; }
            public bool SaveToFile { get; set; }
            public bool WipeFileOnWipe { get; set; }
            public bool TrackKills { get; set; }
            public bool HasToBeVisible { get; set; }
            public int ProximityDistance { get; set; }
            public float ProximityKillTrigger { get; set; }
            public float ProximityKillWeight { get; set; }
            public float ProximityTimeTrigger { get; set; }
            public float ProximityTimeWeight { get; set; }

        }

        //proximity tracking
        public class ProximitySubInfo
        {
            public float time { get; set; } = 0;
            public int proxKills { get; set; } = 0;
            public int selfKills { get; set; } = 0;
        }

        //proximity tracking
        public class ProximityKillConfig
        {
            public float MaximumDistance { get; set; } 
            public float MaximumHeight { get; set; } 
            public int MinimumDistanceToTarget { get; set; }
            public float MaximumAngle { get; set; }
        }

        //note logging
        public class NoteLoggingConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled { get; set; } = true;
            [JsonProperty("Webhook where the note logs will be sent")]
            public string NotesWebhook { get; set; } = "";
        }

        //temp ban
        public class GroupTempBanConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled { get; set; }
            [JsonProperty("Amount of minutes they must be in a group exceeding the limit to trigger a ban")]
            public int MinutesInGroup { get; set; }
            [JsonProperty("Ban length in minutes")]
            public int BanLength { get; set; }
            [JsonProperty("Ban message")]
            public string BanMessage { get; set; }
            [JsonProperty("Temp ban webhook")]
            public string TempBanWebhook { get; set; }
            [JsonProperty("Temp ban message embed color")]
            public ulong TempBanColor { get; set; }
        }

        //Ban config
        public class BanConfig
        {
            [JsonProperty("your battlemetrics token")]
            public string Token { get; set; }
            [JsonProperty("Ban list id, you can leave this blank and it will use your default ban list")]
            public string BanlistID { get; set; }
            [JsonProperty("Ban reason")]
            public string BanReason { get; set; }
            [JsonProperty("Ban notes, the proof for the ban will be added a line below this text")]
            public string Note { get; set; }
        }

        //Ban config
        public class PublicMessageConfig
        {
            [JsonProperty("Ban list id, you can leave this blank and it will use your default ban list")]
            public string BanlistID { get; set; }
            [JsonProperty("Ban reason")]
            public string BanReason { get; set; }
            [JsonProperty("Ban notes, the proof for the ban will be added a line below this text")]
            public string Note { get; set; }
        }
        #endregion

        #region Helpers

        private void NativeBan(string userID, string reason)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "ban", userID, reason);
        }

        public bool CheckClan(ulong player, string otherPlayer)
        {
            var clanMembers = Clans?.Call<List<string>>("GetClanMembers", player);
            if (clanMembers == null)
                return false;

            return clanMembers.Contains(otherPlayer);
        }

        public void StartDemoRecording(BasePlayer player, string reason)
        {
            if(String.IsNullOrEmpty(config.AutoDemoRecorderConfig.DemoWebhook))
            {
                PrintError("Auto Demo Recording support is enabled but no webhook was provided in the config!");
                return;
            }

            AutoDemoRecord.Call("API_StartRecording4", player, reason, _demoConfig.demoLength, config.AutoDemoRecorderConfig.DemoWebhook);
        }

        public bool IsPlayerFriendly(ulong player, ulong otherPlayer)
        {
            if (_clansEnabled)
            {
                if (CheckClan(player, otherPlayer.ToString()))
                    return true;
            }

            RelationshipManager.PlayerTeam team;
            if (RelationshipManager.ServerInstance.playerToTeam.TryGetValue(player, out team))
            {
                return team.members.Contains(otherPlayer);
            }

            return false;
        }
        
        //make sure team exists before this
        private List<ulong> GetFriends(ulong player)
        {
            List<ulong> friends = new List<ulong>();
            RelationshipManager.PlayerTeam team;
            if (RelationshipManager.ServerInstance.playerToTeam.TryGetValue(player, out team))
            {
                friends.AddRange(team.members);
            }

            if (_clansEnabled)
            {
                var clanMembers = Clans?.Call<List<string>>("GetClanMembers", player);
                if (clanMembers != null)
                {
                    foreach (var member in clanMembers)
                    {
                        ulong userID;
                        if(ulong.TryParse(member, out userID))
                        {
                            if(!friends.Contains(userID))
                                friends.Add(userID);
                        }
                    }
                }
            }

            return friends;
        }

        public static bool IsPlayerVisibleToUs(BasePlayer player, BasePlayer otherPlayer)
        {
            if (otherPlayer == null)
                return false;
            Vector3 vector3 = !player.isMounted ? (!player.IsDucked() ? (!player.IsCrawling() ? player.eyes.worldStandingPosition : player.eyes.worldCrawlingPosition) : player.eyes.worldCrouchedPosition) : player.eyes.worldMountedPosition;
            return (otherPlayer.IsVisible(vector3, otherPlayer.CenterPoint()) || otherPlayer.IsVisible(vector3, otherPlayer.transform.position) || otherPlayer.IsVisible(vector3, otherPlayer.eyes.position)) && (player.IsVisible(otherPlayer.CenterPoint(), vector3) || player.IsVisible(otherPlayer.transform.position, vector3) || player.IsVisible(otherPlayer.eyes.position, vector3));
        }

        private bool PlayerCheck(BasePlayer player, BasePlayer otherPlayer)
        {
            if (player.IsSleeping())
                return false;
            if ((player.IsBuildingAuthed() || otherPlayer.IsBuildingAuthed()) && !IsPlayerVisibleToUs(player, otherPlayer))
                return false;
            if (!player.IsConnected)
                return false;
            if (!otherPlayer.IsConnected)
                return false;

            return true;
        }

        private List<string> getAlertData(ulong ent1, ulong ent2)
        {
            if (ent2 > ent1)
            {
                var tmp = ent2;
                ent2 = ent1;
                ent1 = tmp;
            }

            List<string> info;
            Dictionary<ulong, List<string>> proximityData;
            if (AlertTracking.TryGetValue(ent1, out proximityData))
            {
                if (!proximityData.TryGetValue(ent2, out info))
                {
                    info = new List<string>();
                    proximityData.Add(ent2, info);
                }
            }
            else
            {
                info = new List<string>();
                AlertTracking.Add(ent1, new Dictionary<ulong, List<string>> { { ent2, info } });
            }

            return info;
        }

        private static DateTime ConvertEpoch(int epoch)
        {
            double timestamp = epoch;
            DateTime start = new DateTime(1970, 1, 1, 0, 0, 0, 0); //from start epoch time
            return start.AddSeconds(timestamp); //add the seconds to the start DateTime
        }

        private enum FriendType
        {
            NotFriends,
            AreFriends,
            PrivateProfile,
            UnderMinimum
        }

        private string GetThumbnail(string inputAlert)
        {
            switch(inputAlert)
            {
                case "Team Raid":
                    return "https://rustlabs.com/img/items180/ammo.rocket.basic.png";
                case "Team Kill":
                    return "https://rustlabs.com/img/items180/rifle.ak.png";
                case "Code Lock":
                    return "https://rustlabs.com/img/items180/lock.code.png";
                case "Cupboard":
                    return "https://rustlabs.com/img/items180/cupboard.tool.png";
                case "Turret":
                    return "https://rustlabs.com/img/items180/autoturret.png";
                case "Revive":
                    return "https://rustlabs.com/img/items180/syringe.medical.png";
                case "Looting":
                    return "https://rustlabs.com/img/items180/box.wooden.large.png";
                case "Team Accept":
                    return "https://media.discordapp.net/attachments/571758018258272268/931602306984738896/kindpng_1587071.png?width=694&height=463";
                case "Relationship Alert":
                    return "https://media.discordapp.net/attachments/571758018258272268/931602306984738896/kindpng_1587071.png?width=694&height=463";
                case "Voice Chat":
                    return "https://cdn.discordapp.com/attachments/571758018258272268/952683236398628934/PngItem_350197.png";
                case "Bag":
                    return "https://cdn.discordapp.com/attachments/571758018258272268/952692114649661440";
            }

            return "https://media.discordapp.net/attachments/571758018258272268/931602306984738896/kindpng_1587071.png?width=694&height=463";
        }

        #endregion

        #region BattleMetrics

        void InitBattlemetrics()
        {
            if (!_autoBanConfig.BattlemetricsBan)
                return;

            _client.GetJson($@"servers?filter[search]=""{server.Address}:{server.Port}""&filter[rcon]=true", (serverJson) =>
            {
                var serverInfo = (serverJson["data"] as JArray).FirstOrDefault();
                _serverId = serverInfo["id"].Value<string>();
                _orgId = serverInfo["relationships"]["organization"]["data"]["id"].Value<string>();

                if (String.IsNullOrEmpty(_banListID))
                _client.GetJson($"organizations/{_orgId}", (orgJson) =>
                {
                    _banListID = orgJson["data"]["relationships"]["defaultBanList"]["data"]["id"].Value<string>();
                });

            });
        }

        void Ban(BanPacket packet)
        {
            if (_autoBanConfig.NativeBan)
                NativeBan(packet.steamID, packet.reason);

            if (!_autoBanConfig.BattlemetricsBan)
                return;

            var playerPacket = new JObject()
            {
                new JProperty("data", new JArray()
                {
                    new JObject()
                    {
                        new JProperty("type", "identifier"),
                        new JProperty("attributes", new JObject()
                        {
                            new JProperty("type", "steamID"),
                            new JProperty("identifier", packet.steamID)
                        })
                    }
                })
            };

            var datetime = DateTime.Now.AddDays(_autoBanConfig.banLength);
            var time = datetime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture);
            var convertedPlayerPacket = JsonConvert.SerializeObject(playerPacket);
            _client.GetPlayerInfo(convertedPlayerPacket, (response) => {

                var bmID = (response["data"] as JArray).FirstOrDefault()["relationships"]["player"]["data"]["id"].Value<string>();

                JObject banObj = new JObject()
                {
                    new JProperty("data", new JObject()
                    {
                        new JProperty("type", "ban"),
                        new JProperty("attributes", new JObject()
                        {
                            new JProperty("uid", GenerateUID()),
                            new JProperty("timestamp", DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture)),
                            new JProperty("reason", packet.reason),
                            new JProperty("note", packet.notes),
                            new JProperty("expires", time),
                            new JProperty("identifiers", new JArray(){

                                new JObject()
                                {
                                    new JProperty("type", "steamID"),
                                    new JProperty("identifier", packet.steamID),
                                    new JProperty("manual", true)
                                }
                            }),
                            new JProperty("orgWide", true),
                            new JProperty("nativeEnabled", false),
                            new JProperty("autoAddEnabled", true),

                        }),
                        new JProperty("relationships", new JObject()
                        {
                            new JProperty("banList", new JObject()
                            {
                                new JProperty("data", new JObject()
                                {
                                    new JProperty("type", "banList"),
                                    new JProperty("id", _banListID),
                                })
                            }),
                            new JProperty("server", new JObject()
                            {
                                new JProperty("data", new JObject()
                                {
                                    new JProperty("type", "server"),
                                    new JProperty("id", _serverId),
                                })
                            }),
                            new JProperty("organization", new JObject()
                            {
                                new JProperty("data", new JObject()
                                {
                                    new JProperty("type", "organization"),
                                    new JProperty("id", _orgId),
                                })
                            }),
                            new JProperty("player", new JObject()
                            {
                                new JProperty("data", new JObject()
                                {
                                    new JProperty("type", "player"),
                                    new JProperty("id", bmID)
                                })
                            })
                        }),
                    })
                };
                
                var banPacket = JsonConvert.SerializeObject(banObj);
                _client.SendBan(banPacket);
            });
        }
       
        public class BanPacket
        {
            public string steamID;
            public string reason;
            public string notes;

            public BanPacket(string steamIDIn, string reasonIn, string notesIn)
            {
                steamID = steamIDIn;
                reason = reasonIn;
                notes = notesIn;
            }
        }

        private System.Random random = new System.Random();
        private string GenerateUID()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, 9)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        #endregion

        #region Auto Moderation

        bool CheckTeamScore(ulong ent1, ulong ent2, List<string> alerts, FriendType friend)
        {
            if (!_autoBanConfig.EnableAutoBan)
                return false;

            if (ent2 > ent1)
            {
                var tmp = ent2;
                ent2 = ent1;
                ent1 = tmp;
            }

            string notes = $"{ent1}, {ent2} - ";
            if (friend == FriendType.AreFriends)
                if (!alerts.Contains("SteamFriends"))
                    alerts.Add("SteamFriends");

            float weight = 0;
            foreach (var alert in alerts)
            {
                notes += alert + ", ";
                weight += alertConfigs[alert].Weight;
            }
            notes = notes.Remove(notes.Length - 2, 2);

            var proximity = _proximityInstance.getProximityData(ent1, ent2);
            if (proximity.time >= _proximityConfig.ProximityTimeTrigger)
            {
                notes += $" - Proximity time {proximity.time}";
                weight += _proximityConfig.ProximityTimeWeight;
            }

            if ((proximity.proxKills - proximity.selfKills) >= _proximityConfig.ProximityKillTrigger)
            {
                notes += $" - Proximity kills {proximity.proxKills}, Self kills {proximity.selfKills}";
                weight += _proximityConfig.ProximityKillWeight;
            }
            
            if (weight >= config.autoBanRules.AutoBanWeight)
            {
                List<ulong> teamSize = new List<ulong> {ent1, ent2};

                teamSize.AddRange(GetFriends(ent1));
                teamSize.AddRange(GetFriends(ent2));

                List<ulong> markedPlayers;
                if (MarkedAsTeaming.TryGetValue(ent1, out markedPlayers))
                {
                    if (markedPlayers.Contains(ent2))
                    {
                        teamSize.AddRange(markedPlayers);
                    }
                    else
                    {
                        teamSize.AddRange(markedPlayers);
                        markedPlayers.Add(ent2);
                    }
                }
                else
                    MarkedAsTeaming.Add(ent1, new List<ulong> { ent2});
                
                teamSize = teamSize.Distinct().ToList();
                if (teamSize.Count > _groupLimit)
                {
                    var finalNote = _banConfig.Note + "\n\n" + notes;
                    if (_autoBanConfig.teamBan)
                    {
                        SendAutoBanMessage(new AutoBanPacket(teamSize, notes));

                        foreach (var user in teamSize)
                            Ban(new BanPacket(user.ToString(), _banConfig.BanReason, finalNote));
                    }
                    else
                    {
                        SendAutoBanMessage(new AutoBanPacket(new List<ulong> {ent1, ent2 }, notes));

                        Ban(new BanPacket(ent1.ToString(), _banConfig.BanReason, finalNote));
                        Ban(new BanPacket(ent2.ToString(), _banConfig.BanReason, finalNote));
                    }

                    return true;
                }
            }

            return false;
        }

        public static Dictionary<ulong, DateTime> TempBanList = new Dictionary<ulong, DateTime>();
        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
                return;

            if (TempBanList.ContainsKey(player.userID))
            {
                if (DateTime.Compare(DateTime.Now, TempBanList[player.userID]) > 0)
                {
                    TempBanList.Remove(player.userID);
                }
                else
                {
                    player.Kick($"{_tempBans.BanMessage}. Your ban expires in {Math.Round((TempBanList[player.userID] - DateTime.Now).TotalMinutes)} minutes.");
                }
            }
        }

        private void TempBan(ulong userID)
        {
            TempBanList[userID] = DateTime.Now.AddMinutes(5);

            var player = BasePlayer.FindByID(userID);
            if (player == null)
                return;

            if (player.IsConnected)
                player.Kick($"{_tempBans.BanMessage}. Your ban expires in {Math.Round((TempBanList[player.userID] - DateTime.Now).TotalMinutes)} minutes.");
        }

        #endregion

        #region Commands

            #region Proximity Command

        void ProximityCommand(IPlayer player, string command, string[] args)
        {
            if(!player.IsAdmin && !player.HasPermission(permissionName))
            {
                player.Message("you dont have the proper permissions to run this command!");
                return;
            }

            if(args.Length != 2)
            {
                player.Message("you need to include two inputs of either a steamid, name, or ip");
                return;
            }

            var search1 = FindPlayersOnline(args[0]);
            if(search1.Count > 1)
            {
                player.Message($"multiple players found for input {args[0]} try putting in their full name, or their steam id");
                return;
            }
            if(search1.Count == 0)
            {
                player.Message($"no players were found for input {args[0]}");
                return;
            }

            var search2 = FindPlayersOnline(args[1]);
            if (search2.Count > 1)
            {
                player.Message($"multiple players found for input {args[1]} try putting in their full name, or their steam id");
                return;
            }
            if (search2.Count == 0)
            {
                player.Message($"no players were found for input {args[1]}");
                return;
            }

            var proximity = _proximityInstance.getProximityDataFiltered(search1[0].userID, search2[0].userID);
            if(proximity.time == 0)
            {
                player.Message("no proximity data was found for those two players!");
                return;
            }

            string text = "<size=22><color=#ffa500>Proximity Info</color></size>\n\n";
            text += $"<color=#ffd479><size=18>Proximity Time:</size></color><color=#515151><size=18> {Math.Round((proximity.time / 60), 2)}</size></color><color=#ffd479><size=18> minutes</size></color>\n\n";
            text += $"<color=#ffd479><size=18>Proximity Kills:</size></color><color=#515151><size=18> {proximity.proxKills}</size></color><color=#ffd479><size=18>  -  Self Kills:</size></color><color=#515151><size=18> {proximity.selfKills}</size></color>";
            player.Message(text);
        }

        #endregion

            #region Alerts Command

        void AlertsCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !player.HasPermission(permissionName))
            {
                player.Message("you dont have the proper permissions to run this command!");
                return;
            }

            if (args.Length != 2)
            {
                player.Message("you need to include two inputs of either a steamid, name, or ip");
                return;
            }

            var search1 = FindPlayersOnline(args[0]);
            if (search1.Count > 1)
            {
                player.Message($"multiple players found for input {args[0]} try putting in their full name, or their steam id");
                return;
            }
            if (search1.Count == 0)
            {
                player.Message($"no players were found for input {args[0]}");
                return;
            }

            var search2 = FindPlayersOnline(args[1]);
            if (search2.Count > 1)
            {
                player.Message($"multiple players found for input {args[1]} try putting in their full name, or their steam id");
                return;
            }
            if (search2.Count == 0)
            {
                player.Message($"no players were found for input {args[1]}");
                return;
            }

            var alerts = getAlertData(search1[0].userID, search2[0].userID);
            if (alerts.Count == 0)
            {
                player.Message("no past alerts was found for those two players!");
                return;
            }

            string text = "<size=22><color=#ffa500>Past Alerts</color></size>\n";
            foreach (var alert in alerts)
                text += $"\n<color=#ffd479><size=18>{alert}</size></color>";

            player.Message(text);
        }

        #endregion

            #region Friends Command

        void FriendsCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !player.HasPermission(permissionName))
            {
                player.Message("you dont have the proper permissions to run this command!");
                return;
            }

            if(!config.steamFriendsConfig.Enabled || String.IsNullOrEmpty(config.steamFriendsConfig.SteamAPIKey))
            {
                player.Message("No steam API key was provided in the config, or the server owner has disabled this feature.");
                return;
            }

            if(args.Length == 0)
            {
                player.Message("Please input a single steamID to check if they have any friends on the server, or enter two steamID's to check if they are stream friends");
                return;
            }

            var doubleSearch = args.Length == 2;

            if(args.Length == 1 || doubleSearch)
            {
                var FirstPlayer = FindPlayersOnline(args[0]);
                if(FirstPlayer.Count == 0)
                {
                    player.Message($"no players were found for input {args[0]}");
                    return;
                }

                List<BasePlayer> SecondPlayer = null;
                if (doubleSearch)
                {
                    SecondPlayer = FindPlayersOnline(args[0]);
                    if (SecondPlayer.Count == 0)
                    {
                        player.Message($"no players were found for input {args[0]}");
                        return;
                    }
                }

                _client.GetFriendsList($"https://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key={config.steamFriendsConfig.SteamAPIKey}&steamid={FirstPlayer.FirstOrDefault().UserIDString}", (data) =>
                {
                    if (data != null)
                    {
                        if (doubleSearch)
                            CheckPlayersAreFriends(player, data, SecondPlayer.FirstOrDefault().userID);
                        else
                            CheckFriendsList(player, data);
                    }
                    else
                    {
                        if (doubleSearch)
                        {
                            _client.GetFriendsList($"https://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key={config.steamFriendsConfig.SteamAPIKey}&steamid={SecondPlayer.FirstOrDefault().UserIDString}", (Sdata) =>
                            {
                                if (Sdata != null)
                                    CheckPlayersAreFriends(player, data, FirstPlayer.FirstOrDefault().userID);
                                else
                                    player.Message("Both players have their friends list private");
                            });
                        }
                        else
                            player.Message("The players friends list is private!");
                    }
                });
            }
        }

        void CheckFriendsList(IPlayer player, JObject data)
        {
            int i = 0;
            string text = "<size=22><color=#ffa500>Online Friends</color></size>\n";
            var friends = data["friendslist"]["friends"] as JArray;
            foreach (var friend in friends)
            {
                var userID = friend["steamid"].Value<ulong>();

                //to many calls for LINQ
                foreach (var activePlayer in BasePlayer.allPlayerList)
                {
                    if (activePlayer.userID == userID)
                    {
                        i++;
                        var FriendTime = ConvertEpoch(friend["friend_since"].Value<int>());
                        var days = (DateTime.Now - FriendTime).TotalDays;
                        text += $"\n<color=#ffd479><size=18>{activePlayer.displayName} - {activePlayer.UserIDString}: users have been friends for {days} days</size></color>";
                    }
                }
            }

            if (i == 0)
            {
                player.Message("Player has no steam friends on the server");
            }
        }

        void CheckPlayersAreFriends(IPlayer player, JObject data, ulong otherPlayer)
        {
            string text = "<size=22><color=#ffa500>Online Friends</color></size>\n";
            var friends = data["friendslist"]["friends"] as JArray;
            foreach (var friend in friends)
            {
                var userID = friend["steamid"].Value<ulong>();

                if (userID == otherPlayer)
                {
                    var FriendTime = ConvertEpoch(friend["friend_since"].Value<int>());
                    var days = (DateTime.Now - FriendTime).TotalDays;
                    text += $"\n<color=#ffd479><size=18>Players have been friends for {days} days</size></color>";
                }
            }

            player.Message("Players are not friends");
        }

        private static List<BasePlayer> FindPlayersOnline(string nameOrIdOrIp)
        {
            var players = new List<BasePlayer>();
            if (string.IsNullOrEmpty(nameOrIdOrIp)) return players;
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString.Equals(nameOrIdOrIp))
                    players.Add(activePlayer);
                else if (!string.IsNullOrEmpty(activePlayer.displayName) && activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.IgnoreCase))
                    players.Add(activePlayer);
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Equals(nameOrIdOrIp))
                    players.Add(activePlayer);
            }
            return players;
        }
        #endregion

            #region Voice Chat Command

        void VoiceLogs(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !player.HasPermission(permissionName))
            {
                player.Message("you dont have the proper permissions to run this command!");
                return;
            }

            if (args.Length != 2)
            {
                player.Message("you need to include two inputs of either a steamid, name, or ip");
                return;
            }

            var search1 = FindPlayersOnline(args[0]);
            if (search1.Count > 1)
            {
                player.Message($"multiple players found for input {args[0]} try putting in their full name, or their steam id");
                return;
            }
            if (search1.Count == 0)
            {
                player.Message($"no players were found for input {args[0]}");
                return;
            }

            var search2 = FindPlayersOnline(args[1]);
            if (search2.Count > 1)
            {
                player.Message($"multiple players found for input {args[1]} try putting in their full name, or their steam id");
                return;
            }
            if (search2.Count == 0)
            {
                player.Message($"no players were found for input {args[1]}");
                return;
            }

            var voiceLog = GetVoiceChatDataFiltered(search1[0].userID, search2[0].userID);
            if (voiceLog == 0)
            {
                player.Message("no voice chat logs were found for these two players");
                return;
            }

            string text = "<size=22><color=#ffa500>Voice Logs</color></size>\n\n";
            text += $"<color=#ffd479><size=18>Players have spent</size></color><color=#515151><size=18> {voiceLog / 10}</size></color><color=#ffd479><size=18> seconds talking through voice chat</size></color>\n\n";
            player.Message(text);
        }

        #endregion

        #endregion

        #region Discord

        void SendToDiscord(AlertPacket packet, bool SendToDiscord = true, string ThumbnailOverride = null)
        {
            _client.GetFriendsList($"https://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key={config.steamFriendsConfig.SteamAPIKey}&steamid={packet.players.Last()}", (data) =>
            {
                DateTime FriendTime = DateTime.Now;
                FriendType friendType = FriendType.NotFriends;
                List<EmbedField> Fields = new List<EmbedField>();
                if (data != null)
                {
                    var friends = data["friendslist"]["friends"] as JArray;

                    foreach (var friend in friends)
                    {
                        if (friend["steamid"].Value<ulong>() == packet.players[0])
                        {
                            FriendTime = ConvertEpoch(friend["friend_since"].Value<int>());
                            if ((DateTime.Now - FriendTime).TotalDays >= _friendsConfig.MinimumFriendTime)
                                friendType = FriendType.AreFriends;
                            else
                                friendType = FriendType.UnderMinimum;
                        }
                    }
                }
                else
                    friendType = FriendType.PrivateProfile;
                
                var combinations = from item1 in packet.players
                                   from item2 in packet.players
                                   where item1 < item2
                                   select new PlayerPair(item1, item2);

                string alertType = packet.AlertType;
                List<string> pastAlerts = new List<string>();
                foreach (var pair in combinations)
                {
                    var alerts = getAlertData(pair.player1, pair.player2);

                    pastAlerts.AddRange(alerts);
                    FriendType friendInfo = packet.players.Count == 2 ? friendType : FriendType.NotFriends;
                    if (CheckTeamScore(pair.player1, pair.player2, alerts, friendInfo))
                        break;
                }

                if (!alertConfigs[packet.AlertType].SendDiscordAlert)
                    return;
                if (pastAlerts.Count == 1 && alertConfigs[packet.AlertType].DontShowAlone)
                    return;
                if (!SendToDiscord || String.IsNullOrEmpty(_alertWebhook))
                    return;

                if (packet.players.Count <= _groupLimit)
                {
                    List<ulong> involved = new List<ulong>();
                    foreach (var player in packet.players)
                    {
                        involved.Add(player);
                        involved.AddRange(GetFriends(player));
                    }

                    if (involved.Distinct().Count() <= _groupLimit)
                        return;
                }

                string friendMessage = "Players are not friends";
                switch(friendType)
                {
                    case FriendType.AreFriends:
                        friendMessage = $"Players have been friends since {FriendTime}";
                        break;
                    case FriendType.UnderMinimum:
                        friendMessage = $"Players have not been friends long enough to meet the minimum days, friends since {FriendTime}";
                        break;
                    case FriendType.PrivateProfile:
                        friendMessage = "The scanned profile was private";
                        break;
                }

                List<EmbedField> fields = new List<EmbedField>();
                if (!String.IsNullOrEmpty(packet.optionalInfo))
                {
                    fields.Add(new EmbedField { Inline = false, Name = "Alert Info", Value = packet.optionalInfo });
                }
                
                if(!String.IsNullOrEmpty(packet.location))
                {
                    fields.Add(new EmbedField { Inline = false, Name = "Location", Value = $"teleportpos \"{packet.location}\"" });
                }
                
                fields.Add(new EmbedField {Inline = false, Name = "Friends Status", Value = friendMessage });
                if (packet.proximityPacket != null)
                {
                    var proxPacket = packet.proximityPacket; 
                    fields.Add(new EmbedField { Inline = true, Name = "Player 1", Value = "Gun : " + proxPacket.playerGun });
                    fields.Add(new EmbedField { Inline = true, Name = "Player 2", Value = "Gun : " + proxPacket.subPlayerGun });
                    fields.Add(new EmbedField { Inline = true, Name = "​", Value = "​" });

                    var YAxis = Math.Sin(proxPacket.Angle) * proxPacket.Distance;
                    var XAxis = Math.Cos(proxPacket.Angle) * proxPacket.Distance;
                    fields.Add(new EmbedField { Inline = true, Name = "Distance", Value = $"{proxPacket.Distance} meters" });
                    fields.Add(new EmbedField { Inline = true, Name = "Angle", Value = $"{Math.Round(proxPacket.Angle, 2)} degrees" });
                    fields.Add(new EmbedField { Inline = true, Name = "​Distance to Target", Value = $"{proxPacket.targetPlayerDistance}​ meters" });

                    fields.Add(new EmbedField { Inline = true, Name = "Distance Y Component", Value = $"{Math.Round(YAxis, 2)} meters" });
                    fields.Add(new EmbedField { Inline = true, Name = "Distance X Component", Value = $"{Math.Round(XAxis, 2)} meters" });
                    fields.Add(new EmbedField { Inline = true, Name = "​", Value = "​" });

                    fields.Add(new EmbedField { Inline = false, Name = "Target Player", Value = $"[{proxPacket.targetPlayer}](https://steamcommunity.com/profiles/{proxPacket.targetPlayer})" });
                }
                
                string Alerts = "";
                if(pastAlerts.Count > 1)
                foreach (var alert in pastAlerts)
                {
                    Alerts += alert + ", ";
                }
                
                if (String.IsNullOrEmpty(Alerts))
                    fields.Add(new EmbedField { Inline = false, Name = "Past Alerts", Value = "players have no past alerts" });
                else
                {
                    Alerts = Alerts.Remove(Alerts.Length - 2, 2);
                    fields.Add(new EmbedField { Inline = false, Name = "Past Alerts", Value = Alerts });
                }
                
                var proximityData =
                    _proximityInstance.getProximityDataFiltered(packet.players.Last(), packet.players[0]);
                if (proximityData != null)
                {
                    fields.Add(new EmbedField { Inline = false, Name = "Proximity Kills", Value = $"Time: {proximityData.time} - Proximity Kills: {proximityData.proxKills} - Self Kills: {proximityData.selfKills}" });
                }

                string team1 = "";
                foreach (var user in GetFriends(packet.players[0]))
                {
                    team1 += user + ", ";
                }

                if (String.IsNullOrEmpty(team1))
                    fields.Add(new EmbedField { Inline = false, Name = "Team info for " + packet.players[0], Value = "player does not have a team" });
                else
                {
                    team1 = team1.Remove(team1.Length - 2, 2);
                    fields.Add(new EmbedField { Inline = false, Name = "Team info for " + packet.players[0], Value = team1 });
                }
                
                string team2 = "";
                foreach (var user in GetFriends(packet.players.Last()))
                {
                    team2 += user + ", ";
                }
                if (String.IsNullOrEmpty(team2))
                    fields.Add(new EmbedField { Inline = false, Name = "Team info for " + packet.players.Last(), Value = "player does not have a team" });
                else
                {
                    team2 = team2.Remove(team2.Length - 2, 2);
                    fields.Add(new EmbedField { Inline = false, Name = "Team info for " + packet.players.Last(), Value = team2 });
                }
                
                string Profiles = "";
                foreach (var user in packet.players)
                {
                    Profiles += $"[{user}](https://steamcommunity.com/profiles/{user}) - [BM](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={user}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=false)" + ", ";
                }
                Profiles = Profiles.Remove(Profiles.Length - 2, 2);
                fields.Add(new EmbedField { Inline = false, Name = "Player Profiles", Value = Profiles });

                string thumbnail = ThumbnailOverride == null ? GetThumbnail(packet.AlertType) : ThumbnailOverride;
                Embed embed = new Embed
                {
                    Title = packet.AlertType,
                    Footer = new EmbedFooter(DateTime.Now.ToString()),
                    Fields = fields,
                    Color = alertConfigs[packet.AlertType].AlertColor,
                    Thumbnail = new EmbedThumbnail(thumbnail)
                };
              
                if (!String.IsNullOrEmpty(_serverName))
                {
                    string Description = packet.AlertType;
                    if (_serverName == "default")
                        Description = server.Name;
                    else
                        Description = _serverName;

                    embed.Description = Description;
                }
            
                DiscordMessage message = new DiscordMessage();
                if (!String.IsNullOrEmpty(_roleID))
                    message.AddContent($"<@&{_roleID}>");

                message.AddEmbed(embed);
                var JObject = JsonConvert.SerializeObject(message);
                _client.SendMessage(_alertWebhook, JObject);
            });
        }

        void SendReportToDiscord(TeamingReportPacket packet)
        {
            List<EmbedField> Fields = new List<EmbedField>();
            Fields.Add(new EmbedField { Inline = false, Name = "Subject", Value = packet.subject });
            Fields.Add(new EmbedField { Inline = false, Name = "Message", Value = packet.message });
            Fields.Add(new EmbedField { Inline = false, Name = "Reported", Value = $"{packet.reportedName} was reported by {packet.reportedBy}" });
            Fields.Add(new EmbedField { Inline = false, Name = "Steam Profile", Value = $"[{packet.reported}](https://steamcommunity.com/profiles/{packet.reported})" });
            Fields.Add(new EmbedField { Inline = false, Name = "Battlemetrics Profile", Value = $"[{packet.reported}](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={packet.reported}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=false)" });

            string Title = "F7 Report";
            if (!String.IsNullOrEmpty(_serverName))
            {
                if (_serverName == "default")
                    Title += " - (" + server.Name + ")";
                else
                    Title += _serverName;
            }

            Embed embed = new Embed
            {
                Title = Title,
                Footer = new EmbedFooter(DateTime.Now.ToString()),
                Color = _reportColor,
                Thumbnail = new EmbedThumbnail("https://cdn-icons-png.flaticon.com/512/179/179386.png"),
                Fields = Fields
            };

            DiscordMessage message = new DiscordMessage();
            if (!String.IsNullOrEmpty(_BanRoleID))
                message.AddContent($"<@&{_BanRoleID}>");

            message.AddEmbed(embed);
            var JObject = JsonConvert.SerializeObject(message);
            _client.SendMessage(_alertWebhook, JObject);
        }

        void SendAutoBanMessage(AutoBanPacket packet)
        {
            if (String.IsNullOrEmpty(_autoBanConfig.AutoBanWebhook))
                return;

            List<EmbedField> fields = new List<EmbedField>();
            string playerNames = "";
            foreach (var player in packet.bannedPlayers)
            {
                var basePlayer = BasePlayer.FindByID(player);
                if (basePlayer != null)
                    playerNames += basePlayer.displayName + ", ";
            }
            playerNames = playerNames.Remove(playerNames.Length - 2, 2);
            fields.Add(new EmbedField { Inline = false, Name = "Online Players", Value = playerNames });
            fields.Add(new EmbedField { Inline = false, Name = "Notes", Value = packet.notes });

            string team1 = "";
            foreach (var user in GetFriends(packet.bannedPlayers[0]))
            {
                team1 += user + ", ";
            }

            if (String.IsNullOrEmpty(team1))
                fields.Add(new EmbedField { Inline = false, Name = "Team info for " + packet.bannedPlayers[0], Value = "player does not have a team" });
            else
            {
                team1 = team1.Remove(team1.Length - 2, 2);
                fields.Add(new EmbedField { Inline = false, Name = "Team info for " + packet.bannedPlayers[0], Value = team1 });
            }

            string team2 = "";
            foreach (var user in GetFriends(packet.bannedPlayers.Last()))
            {
                team2 += user + ", ";
            }
            if (String.IsNullOrEmpty(team2))
                fields.Add(new EmbedField { Inline = false, Name = "Team info for " + packet.bannedPlayers.Last(), Value = "player does not have a team" });
            else
            {
                team2 = team2.Remove(team2.Length - 2, 2);
                fields.Add(new EmbedField { Inline = false, Name = "Team info for " + packet.bannedPlayers.Last(), Value = team2 });
            }

            string Profiles = "";
            foreach (var user in packet.bannedPlayers)
            {
                Profiles += $"[{user}](https://steamcommunity.com/profiles/{user}) - [BM](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={user}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=false)" + ", ";
            }
            Profiles = Profiles.Remove(Profiles.Length - 2, 2);
            fields.Add(new EmbedField { Inline = false, Name = "Player Profiles", Value = Profiles });

            string Title = "AutoBan Alert";
            if (!String.IsNullOrEmpty(_serverName))
            {
                if (_serverName == "default")
                    Title += " - (" + server.Name + ")";
                else
                    Title += _serverName;
            }

            Embed embed = new Embed
            {
                Title = Title,
                Footer = new EmbedFooter(DateTime.Now.ToString()),
                Color = _autoBanConfig.AutoBanColor,
                Thumbnail = new EmbedThumbnail("https://cdn-icons-png.flaticon.com/512/179/179386.png"),
                Fields = fields
            };

            DiscordMessage message = new DiscordMessage();
            if (!String.IsNullOrEmpty(_roleID))
                message.AddContent($"<@&{_roleID}>");

            message.AddEmbed(embed);
            var JObject = JsonConvert.SerializeObject(message);
            _client.SendMessage(_autoBanConfig.AutoBanWebhook, JObject);
        }

        void SendTempBanMessage(AutoBanPacket packet)
        {
            if (String.IsNullOrEmpty(_autoBanConfig.AutoBanWebhook))
                return;

            List<EmbedField> fields = new List<EmbedField>();
            string playerNames = "";
            foreach (var player in packet.bannedPlayers)
            {
                var basePlayer = BasePlayer.FindByID(player);
                if (basePlayer != null)
                    playerNames += basePlayer.displayName + ", ";
            }
            playerNames = playerNames.Remove(playerNames.Length - 2, 2);
            fields.Add(new EmbedField { Inline = false, Name = "Banned Players", Value = playerNames });
            fields.Add(new EmbedField { Inline = false, Name = "Notes", Value = packet.notes });

            string steamProfiles = "";
            foreach (var user in packet.bannedPlayers)
            {
                steamProfiles += $"[{user}](https://steamcommunity.com/profiles/{user})" + ", ";
            }
            steamProfiles = steamProfiles.Remove(steamProfiles.Length - 2, 2);
            fields.Add(new EmbedField { Inline = false, Name = "Steam Profiles", Value = steamProfiles });

            string battleMetrics = "";
            foreach (var user in packet.bannedPlayers)
            {
                battleMetrics += $"[{user}](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={user}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=false)" + ", ";
            }
            battleMetrics = battleMetrics.Remove(battleMetrics.Length - 2, 2);
            fields.Add(new EmbedField { Inline = false, Name = "Battlemetrics Profiles", Value = battleMetrics });

            string Title = "Temp Ban Alert";
            if (!String.IsNullOrEmpty(_serverName))
            {
                if (_serverName == "default")
                    Title += " - (" + server.Name + ")";
                else
                    Title += _serverName;
            }

            Embed embed = new Embed
            {
                Title = "Temp Ban Alert",
                Footer = new EmbedFooter(DateTime.Now.ToString()),
                Color = _tempBans.TempBanColor,
                Thumbnail = new EmbedThumbnail("https://cdn-icons-png.flaticon.com/512/179/179386.png"),
                Fields = fields
            };

            DiscordMessage message = new DiscordMessage();
            if (!String.IsNullOrEmpty(_roleID))
                message.AddContent($"<@&{_roleID}>");

            message.AddEmbed(embed);
            var JObject = JsonConvert.SerializeObject(message);
            _client.SendMessage(_tempBans.TempBanWebhook, JObject);
        }

        private class DiscordMessage
        {
            [JsonProperty("username")]
            private string Username { get; set; }

            [JsonProperty("avatar_url")]
            private string AvatarUrl { get; set; }

            [JsonProperty("content")]
            private string Content { get; set; }

            [JsonProperty("embeds")]
            private List<Embed> Embeds { get; }

            public DiscordMessage(string username = null, string avatarUrl = null)
            {
                Username = username;
                AvatarUrl = avatarUrl;
                Embeds = new List<Embed>();
            }

            public DiscordMessage(Embed embed, string username = null, string avatarUrl = null)
            {
                Embeds = new List<Embed> { embed };
                Username = username;
                AvatarUrl = avatarUrl;
            }

            public void AddEmbed(Embed embed)
            {
                if (Embeds.Count >= 10)
                {
                    throw new IndexOutOfRangeException("Only 10 embeds are allowed per message!");
                }

                Embeds.Add(embed);
            }

            public void AddContent(string contentIn)
            {
                Content = contentIn;
            }
        }

        internal class Embed
        {
            [JsonProperty("title")]
            public string Title { get; set; }
            [JsonProperty("description")]
            public string Description { get; set; }
            [JsonProperty("url")]
            public string Url { get; set; }
            [JsonProperty("color")]
            public ulong? Color { get; set; } = 0;
            [JsonProperty("timestamp")]
            public DateTimeOffset? Timestamp { get; set; }
            [JsonProperty("author")]
            public EmbedAuthor Author { get; set; }
            [JsonProperty("footer")]
            public EmbedFooter Footer { get; set; }
            [JsonProperty("thumbnail")]
            public EmbedThumbnail Thumbnail { get; set; }
            [JsonProperty("fields")]
            public List<EmbedField> Fields { get; set; } = new List<EmbedField>();
        }

        internal class EmbedAuthor
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("url")]
            public string Url { get; set; }
            [JsonProperty("iconUrl")]
            public string IconUrl { get; set; }
            [JsonProperty("proxyIconUrl")]
            public string ProxyIconUrl { get; set; }
        }

        internal class EmbedField
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("value")]
            public string Value { get; set; }
            [JsonProperty("inline")]
            public bool Inline { get; set; }
        }

        internal class EmbedThumbnail
        {
            [JsonProperty("url")]
            public string Url { get; set; }
            [JsonProperty("proxyUrl")]
            public string ProxyUrl { get; set; }
            [JsonProperty("height")]
            public int Height { get; set; }
            [JsonProperty("width")]
            public int Width { get; set; }

            public EmbedThumbnail(string urlIn)
            {
                Url = urlIn;
            }
        }

        internal class EmbedFooter
        {
            [JsonProperty("text")]
            public string Text { get; set; }
            [JsonProperty("iconUrl")]
            public string IconUrl { get; set; }
            [JsonProperty("proxyIconUrl")]
            public string ProxyIconUrl { get; set; }

            public EmbedFooter(string TextIn, string IconUrlIn = null, string ProxyIconUrlIn = null)
            {
                Text = TextIn;
                IconUrl = IconUrlIn;
                ProxyIconUrl = ProxyIconUrlIn;
            }
        }
        #endregion

        public class WebClient : RustPlugin
        {
            private bool wantsFriendsCheck;
            private Queue<WebPacket> Queue = new Queue<WebPacket>();
            private string BaseAPI = "https://api.battlemetrics.com/";
            private Dictionary<string, string> _discordheader = new Dictionary<string, string> { { "Content-Type", "application/json" } };
            private Dictionary<string, string> _battlemetricsHeaders;

            public void GetJson(string url, Action<JObject> callBack)
                => TryPushQueue(new WebPacket("GET", callBack, BaseAPI + url, null, _battlemetricsHeaders));
            public void GetFriendsList(string url, Action<JObject> callBack)
            {
                if (!wantsFriendsCheck)
                    callBack.Invoke(null);
                else
                    TryPushQueue(new WebPacket("GET", callBack, url, null, null));
            }
            public void SendBan(string JsonData)
                => TryPushQueue(new WebPacket("POST", null, BaseAPI + "bans", JsonData, _battlemetricsHeaders));
            public void GetPlayerInfo(string JsonData, Action<JObject> callBack)
                => TryPushQueue(new WebPacket("POST", callBack, BaseAPI + "players/match", JsonData, _battlemetricsHeaders));
            public void SendMessage(string url, string JsonData)
                => TryPushQueue(new WebPacket("POST", null, url, JsonData, _discordheader));

            private void TryPushQueue(WebPacket packet = null)
            {
                try
                {
                    if (packet == null)
                    {
                        if (!CanRun())
                            return;

                        if (Queue.Count > 0)
                        {
                            var request = Queue.Dequeue();
                            ServerMgr.Instance.StartCoroutine(SendRequest(request));
                        }
                    }
                    else
                    {
                        if (!CanRun())
                        {
                            Queue.Enqueue(packet);
                            return;
                        }

                        ServerMgr.Instance.StartCoroutine(SendRequest(packet));
                    }
                }
                catch (Exception ex)
                {
                    running = false;
                    TryPushQueue();
                }
            }

            private bool CanRun()
            {
                if ((UnityEngine.Time.time - lastRequest) >= 10)
                    return true;

                if (running)
                    return false;

                return true;
            }

            float lastRequest = 0;
            private bool running = false;
            private IEnumerator SendRequest(WebPacket packet)
            {
                lastRequest = UnityEngine.Time.time;
                running = true;
                var request = new UnityWebRequest(packet.url, packet.method);

                if(packet.data != null)
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(packet.data);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                }
                request.downloadHandler = new DownloadHandlerBuffer();

                if(packet.Headers != null)
                foreach (var header in packet.Headers)
                {
                    request.SetRequestHeader(header.Key, header.Value);
                }
                yield return request.SendWebRequest();

                var code = request.responseCode;
                if (request.error != null || (code != 200 && code != 201 && code != 204))
                {
                    if(request.responseCode != 401)
                        Debug.Log("Error: " + request.error);
                }

                try
                {
                    if (packet.Action != null)
                    {
                        var response = request.downloadHandler.text;
                        var Jobject = JObject.Parse(response);
                        packet.Action.Invoke(Jobject);
                    }
                }
                catch(Exception e)
                {
                    if (packet.Action != null)
                        packet.Action.Invoke(null);
                }

                running = false;
                TryPushQueue();
            }

            public class WebPacket
            {
                public string method;
                public Action<JObject> Action;
                public string url;
                public string data;
                public Dictionary<string, string> Headers;

                public WebPacket(string methodIn, Action<JObject> ActionIn, string urlIn, string dataIn, Dictionary<string, string> HeadersIn)
                {
                    method = methodIn;
                    Action = ActionIn;
                    url = urlIn;
                    data = dataIn;
                    Headers = HeadersIn;
                }
            }

            public void SetFriendsCheck(bool friendsCheck)
            {
                wantsFriendsCheck = friendsCheck;
            }

            public void SetToken(string tokenIn)
            {
                _battlemetricsHeaders = new Dictionary<string, string>()
                {
                    {"Authorization", $"Bearer " + tokenIn},
                    {"Content-Type", "application/json"},
                    {"User-Agent", $"AntiTeam Plugin"}
                };
            }
        }
    }

    //this can be optimized to have basically no impact on performance by patching into the contacts system, send me a DM for that version
    class ProximityTracking : MonoBehaviour
    {
        bool VisibleCheck;
        public void InitTracking()
        {
            InvokeRepeating(nameof(UpdateProximity), 0.0f, 1f);
            VisibleCheck = AntiTeam._instance._proximityConfig.HasToBeVisible;
        }

        private void UpdateProximity()
        {
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
                GetPlayersInProximity(activePlayer);
        }

        public void GetPlayersInProximity(BasePlayer player)
        {
            List<BasePlayer> list = Facepunch.Pool.GetList<BasePlayer>();
            BaseNetworkable.GetCloseConnections(player.transform.position, AntiTeam._instance._proximityConfig.ProximityDistance, list);
            foreach (BasePlayer otherPlayer in list)
            {
                if (otherPlayer != player && !otherPlayer.isClient && otherPlayer.IsAlive() && !otherPlayer.IsSleeping() && !otherPlayer.limitNetworking)
                {
                    if (otherPlayer.userID > player.userID)
                        return;

                    if (VisibleCheck && !AntiTeam.IsPlayerVisibleToUs(player, otherPlayer))
                        return;

                    getProximityData(player.userID, otherPlayer.userID).time += 1;
                }
            }
            Facepunch.Pool.FreeList(ref list);
        }

        public AntiTeam.ProximitySubInfo getProximityData(ulong ent1, ulong ent2)
        {
            AntiTeam.ProximitySubInfo info;
            var ProximityInstance = AntiTeam._instance.ProximityData;
            Dictionary<ulong, AntiTeam.ProximitySubInfo> proximityData;
            if (ProximityInstance.TryGetValue(ent1, out proximityData))
            {
                if (!proximityData.TryGetValue(ent2, out info))
                {
                    info = new AntiTeam.ProximitySubInfo();
                    proximityData.Add(ent2, info);
                }
            }
            else
            {
                info = new AntiTeam.ProximitySubInfo();
                ProximityInstance.Add(ent1, new Dictionary<ulong, AntiTeam.ProximitySubInfo> { { ent2, info } });
            }

            return info;
        }

        public AntiTeam.ProximitySubInfo getProximityDataFiltered(ulong ent1, ulong ent2)
        {
            if (ent2 > ent1)
            {
                var tmp = ent2;
                ent2 = ent1;
                ent1 = tmp;
            }

            AntiTeam.ProximitySubInfo info;
            var ProximityInstance = AntiTeam._instance.ProximityData;
            Dictionary<ulong, AntiTeam.ProximitySubInfo> proximityData;
            if (ProximityInstance.TryGetValue(ent1, out proximityData))
            {
                if (!proximityData.TryGetValue(ent2, out info))
                {
                    info = new AntiTeam.ProximitySubInfo();
                    proximityData.Add(ent2, info);
                }
            }
            else
            {
                info = new AntiTeam.ProximitySubInfo();
                ProximityInstance.Add(ent1, new Dictionary<ulong, AntiTeam.ProximitySubInfo> { { ent2, info } });
            }

            return info;
        }

        public void Destroy()
        {
            CancelInvoke(nameof(UpdateProximity));
        }
    }
}
