using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System.IO;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using System.Collections;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("UltimateRconPlus", "Amino", "1.0.2")]
    [Description("An addon for UR+")]
    public class UltimateRconPlus : RustPlugin
    {
        #region Config
        public static URPlusConfig _config;
        public class URPlusConfig
        {
            [JsonProperty(PropertyName = "Feedback Settings")]
            public FeedbackSettings FeedbackSettings { get; set; }
            [JsonProperty(PropertyName = "Kill Settings")]
            public KillConfig KillSettings { get; set; }
            public EntitySpawns EntitySpawnSettings { get; set; }
            public UserPermissions UserPermissions { get; set; }
            public ConnectionConfig ConnectionConfig { get; set; } = new ConnectionConfig();
            public static URPlusConfig DefaultConfig()
            {
                return new URPlusConfig()
                {
                    FeedbackSettings = new FeedbackSettings { EnabledFeedback = true, 
                        FeedbackCommands = new List<string> { "feedback", "suggestion" }, 
                        FeedbackOptions = new List<string> { "FEEDBACK", "SUGGESTION", "BUG" },
                        FeedbackUISettings = new FeedbackUISettings { BlurPanel = "0 0 0 .4", BackgroundColor = ".17 .17 .17 1", BackgroundImage = "", SelectedButtonColor = ".93 .56 .15 .5", SubmitButtonColor = ".24 .9 .11 .3", SubmitButtonTextColor = ".24 .9 .11 .4" } },
                    KillSettings = new KillConfig { AnimalKills = true, NpcKills = true, PlayerKills = true, SuicideKills = true },
                    EntitySpawnSettings = new EntitySpawns { BradAPC = true, CargoPlane = true, PatrolHelicopter = true, CargoShip = true, CH47Helicopter = true },
                    UserPermissions = new UserPermissions { UserAddedToGroup = true, UserAddedToPerm = true, UserRemovedFromGroup = true, UserRemovedFromPerm = true },
                    ConnectionConfig = new ConnectionConfig { PlayerConnects = true, PlayerDisconnects = true }
                };
            }
        }
        public class FeedbackSettings
        {
            public bool EnabledFeedback { get; set; }
            public List<string> FeedbackCommands { get; set; }
            public List<string> FeedbackOptions { get; set; }
            public FeedbackUISettings FeedbackUISettings { get; set; }
        }

        public class FeedbackUISettings
        {
            public string BlurPanel { get; set; }
            public string BackgroundColor { get; set; }
            public string BackgroundImage { get; set; }
            public string SelectedButtonColor { get; set; }
            public string SubmitButtonColor { get; set; }
            public string SubmitButtonTextColor { get; set; }
        }

        public class FeedbackOptions
        {
            public string ButtonText { get; set; }
            public string ButtonImage { get; set; }
        }

        public class ConnectionConfig
        {
            public bool PlayerConnects { get; set; }
            public bool PlayerDisconnects { get; set; }
        }

        public class KillConfig
        {
            public bool PlayerKills { get; set; }
            public bool SuicideKills { get; set; }
            public bool AnimalKills { get; set; }
            public bool NpcKills { get; set; }
        }

        public class UserPermissions
        {
            public bool UserAddedToGroup { get; set; }
            public bool UserRemovedFromGroup { get; set; }
            public bool UserAddedToPerm { get; set; }
            public bool UserRemovedFromPerm { get; set; }
        }

        public class EntitySpawns
        {
            public bool PatrolHelicopter { get; set; }
            public bool BradAPC { get; set; }
            public bool CargoPlane { get; set; }
            public bool CargoShip { get; set; }
            public bool CH47Helicopter { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<URPlusConfig>();
                if (_config == null) LoadDefaultConfig();
                if (_config.FeedbackSettings.FeedbackUISettings == null) _config.FeedbackSettings.FeedbackUISettings = new FeedbackUISettings { BlurPanel = "0 0 0 .4", BackgroundColor = ".17 .17 .17 1", BackgroundImage = "", SelectedButtonColor = ".93 .56 .15 .5", SubmitButtonColor = ".24 .9 .11 .3", SubmitButtonTextColor = ".24 .9 .11 .4" };
                if (_config.ConnectionConfig == null) _config.ConnectionConfig = new ConnectionConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }
        protected override void LoadDefaultConfig() => _config = URPlusConfig.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Constructors
        public List<string> _subscribedHooks = new List<string> { "OnPlayerDeath", "OnEntityDeath" };
        public List<PlayerFeedbackInfo> _playerFeedbackInfo = new List<PlayerFeedbackInfo>();

        public class PlayerFeedbackInfo
        {
            public ulong SteamId { get; set; }
            public string Title { get; set; }
            public string Message { get; set; }
            public string Category { get; set; }
        }

        public class FeedbackInfo
        {
            public string SteamID { get; set; }
            public string Category { get; set; }
            public string Title { get; set; }
            public string Message { get; set; }
        }

        public class PlayerDeathInfo
        {
            public string AttackerID { get; set; }
            public string AttackerName { get; set; }
            public bool AttackerIsNPC { get; set; }
            public string VictimID { get; set; }
            public string VictimName { get; set; }
            public bool VictimIsNPC { get; set; }
            public string Weapon { get; set; }
            public float KillDistance { get; set; }
        }

        public class EntitySpawnInfo
        {
            public string EntityName { get; set; }
            public string EntityPosition { get; set; }
        }

        public class UserPermsChanged
        {
            public string UserID { get; set; }
            public bool Added { get; set; }
            public bool IsGroup { get; set; }
            public string Perm { get; set; }
        }
        #endregion

        #region Hooks
        void OnServerInitialized(bool initial)
        {
            RegisterCommands();
            SubscribeHooks(true);
        }

        void OnPluginChanged()
        {
            SaveConfig();
        }

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "URMainPanel");
                    CuiHelper.DestroyUi(player, "URFeedbackPanel");
                }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            Puts("CONNECT:{0}", JsonConvert.SerializeObject(new { SteamId = player.UserIDString }));
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            Puts("DISCONNECT:{0}", JsonConvert.SerializeObject(new { SteamId = player.UserIDString, Reason = reason }));
        }


        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info == null || info.InitiatorPlayer == null) return null;

            if (!_config.KillSettings.PlayerKills && (!player.IsNpc || !info.Initiator.IsNpc)) return null;
            if (!_config.KillSettings.NpcKills && (player.IsNpc || info.Initiator.IsNpc)) return null;
            if (!_config.KillSettings.SuicideKills && player.userID == info.InitiatorPlayer.userID) return null;
            string initiatorUserID = info.InitiatorPlayer.UserIDString;
            string playerUserID = player.UserIDString;
            string weaponName = info.Weapon?.GetItem()?.info?.displayName?.english ?? "Unknown";
            float killDistance = info.ProjectileDistance;

            var playerDeath = new PlayerDeathInfo
            {
                AttackerID = initiatorUserID,
                AttackerName = info.InitiatorPlayer.displayName,
                AttackerIsNPC = info.Initiator.IsNpc,
                VictimID = playerUserID,
                VictimName = player.displayName,
                VictimIsNPC = player.IsNpc,
                Weapon = weaponName,
                KillDistance = killDistance
            };

            Puts("DEATHMESSAGE:{0}", JsonConvert.SerializeObject(playerDeath));

            return null;
        }

        void OnEntityDeath(BaseAnimalNPC animal, HitInfo info)
        {
            if(animal == null || info.InitiatorPlayer == null || _config.KillSettings.AnimalKills) return;
            if (info.Initiator.IsNpc) return;

            string weaponName = info.Weapon?.GetItem()?.info?.displayName?.english ?? "Unknown";
            float killDistance = info.ProjectileDistance;

            var playerDeath = new PlayerDeathInfo
            {
                AttackerID = info.InitiatorPlayer.UserIDString,
                AttackerName = info.InitiatorPlayer.displayName,
                AttackerIsNPC = info.Initiator.IsNpc,
                VictimName = animal.ShortPrefabName,
                Weapon = weaponName,
                KillDistance = killDistance
            };

            Puts("DEATHMESSAGE:{0}", JsonConvert.SerializeObject(playerDeath));
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;
            
            if(entity is PatrolHelicopter && _config.EntitySpawnSettings.PatrolHelicopter) Puts("ENTITYSPAWNED:{0}", JsonConvert.SerializeObject(new EntitySpawnInfo { EntityName = "🚁 **Patrol Helicopter**" }));
            else if(entity is BradleyAPC && _config.EntitySpawnSettings.BradAPC) Puts("ENTITYSPAWNED:{0}", JsonConvert.SerializeObject(new EntitySpawnInfo { EntityName = "💥 **Bradley APC**" }));
            else if(entity is CargoPlane && _config.EntitySpawnSettings.CargoPlane) Puts("ENTITYSPAWNED:{0}", JsonConvert.SerializeObject(new EntitySpawnInfo { EntityName = "✈️ **Cargo Plane**" }));
            else if (entity is CargoShip && _config.EntitySpawnSettings.CargoShip) Puts("ENTITYSPAWNED:{0}", JsonConvert.SerializeObject(new EntitySpawnInfo { EntityName = "🚢 **Cargo Ship**" }));
            else if (entity is CH47Helicopter && _config.EntitySpawnSettings.CH47Helicopter) Puts("ENTITYSPAWNED:{0}", JsonConvert.SerializeObject(new EntitySpawnInfo { EntityName = "🚁 **CH47 Helicopter**" }));
        }

        void OnUserGroupAdded(string id, string groupName)
        {
            Puts("PERMCHANGED:{0}", JsonConvert.SerializeObject(new UserPermsChanged { UserID = id, IsGroup = true, Added = true, Perm = groupName}));
        }
       
        void OnUserGroupRemoved(string id, string groupName)
        {
            Puts("PERMCHANGED:{0}", JsonConvert.SerializeObject(new UserPermsChanged { UserID = id, IsGroup = true, Added = false, Perm = groupName }));
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            Puts("PERMCHANGED:{0}", JsonConvert.SerializeObject(new UserPermsChanged { UserID = id, IsGroup = false, Added = true, Perm = permName }));
        }

        void OnUserPermissionRevoked(string id, string permName)
        {
            Puts("PERMCHANGED:{0}", JsonConvert.SerializeObject(new UserPermsChanged { UserID = id, IsGroup = false, Added = false, Perm = permName }));
        }
        #endregion

        #region Commands
        [ConsoleCommand("ur_main")]
        private void CMDURMain(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            switch (arg.Args[0])
            {
                case "player":
                    if (_config.KillSettings.PlayerKills) _config.KillSettings.PlayerKills = false;
                    else _config.KillSettings.PlayerKills = true;
                    SubscribeHooks(!_config.KillSettings.PlayerKills);
                    break;
                case "npc":
                    if (_config.KillSettings.NpcKills) _config.KillSettings.NpcKills = false;
                    else _config.KillSettings.NpcKills = true;
                    SubscribeHooks(!_config.KillSettings.NpcKills);
                    break;
                case "animals":
                    if (_config.KillSettings.AnimalKills) _config.KillSettings.AnimalKills = false;
                    else _config.KillSettings.AnimalKills = true;
                    SubscribeHooks(!_config.KillSettings.AnimalKills);
                    break;
                case "suicide":
                    if (_config.KillSettings.SuicideKills) _config.KillSettings.SuicideKills = false;
                    else _config.KillSettings.SuicideKills = true;
                    SubscribeHooks(!_config.KillSettings.SuicideKills);
                    break;
                case "patrolheli":
                    if (_config.EntitySpawnSettings.PatrolHelicopter) _config.EntitySpawnSettings.PatrolHelicopter = false;
                    else _config.EntitySpawnSettings.PatrolHelicopter = true;
                    SubscribeHooks(!_config.EntitySpawnSettings.PatrolHelicopter);
                    break;
                case "bradapc":
                    if (_config.EntitySpawnSettings.BradAPC) _config.EntitySpawnSettings.BradAPC = false;
                    else _config.EntitySpawnSettings.BradAPC = true;
                    SubscribeHooks(!_config.EntitySpawnSettings.BradAPC);
                    break;
                case "cargoplane":
                    if (_config.EntitySpawnSettings.CargoPlane) _config.EntitySpawnSettings.CargoPlane = false;
                    else _config.EntitySpawnSettings.CargoPlane = true;
                    SubscribeHooks(!_config.EntitySpawnSettings.CargoPlane);
                    break;
                case "cargoship":
                    if (_config.EntitySpawnSettings.CargoShip) _config.EntitySpawnSettings.CargoShip = false;
                    else _config.EntitySpawnSettings.CargoShip = true;
                    SubscribeHooks(!_config.EntitySpawnSettings.CargoShip);
                    break;
                case "ch47heli":
                    if (_config.EntitySpawnSettings.CH47Helicopter) _config.EntitySpawnSettings.CH47Helicopter = false;
                    else _config.EntitySpawnSettings.CH47Helicopter = true;
                    SubscribeHooks(!_config.EntitySpawnSettings.CH47Helicopter);
                    break;
                case "groupadded":
                    if (_config.UserPermissions.UserAddedToGroup) _config.UserPermissions.UserAddedToGroup = false;
                    else _config.UserPermissions.UserAddedToGroup = true;
                    SubscribeHooks(!_config.UserPermissions.UserAddedToGroup);
                    break;
                case "groupremoved":
                    if (_config.UserPermissions.UserRemovedFromGroup) _config.UserPermissions.UserRemovedFromGroup = false;
                    else _config.UserPermissions.UserRemovedFromGroup = true;
                    SubscribeHooks(!_config.UserPermissions.UserRemovedFromGroup);
                    break;
                case "permadded":
                    if (_config.UserPermissions.UserAddedToPerm) _config.UserPermissions.UserAddedToPerm = false;
                    else _config.UserPermissions.UserAddedToPerm = true;
                    SubscribeHooks(!_config.UserPermissions.UserAddedToPerm);
                    break;
                case "permremoved":
                    if (_config.UserPermissions.UserRemovedFromPerm) _config.UserPermissions.UserRemovedFromPerm = false;
                    else _config.UserPermissions.UserRemovedFromPerm = true;
                    SubscribeHooks(!_config.UserPermissions.UserRemovedFromPerm);
                    break;
                case "connects":
                    if (_config.ConnectionConfig.PlayerConnects) _config.ConnectionConfig.PlayerConnects = false;
                    else _config.ConnectionConfig.PlayerConnects = true;
                    SubscribeHooks(!_config.ConnectionConfig.PlayerConnects);
                    break;
                case "disconnects":
                    if (_config.ConnectionConfig.PlayerDisconnects) _config.ConnectionConfig.PlayerDisconnects = false;
                    else _config.ConnectionConfig.PlayerDisconnects = true;
                    SubscribeHooks(!_config.ConnectionConfig.PlayerDisconnects);
                    break;
            }
            UIPanel(player);
            SaveConfig();        
        }


        [ConsoleCommand("ur_event")]
        private void CMDURUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            switch (arg.Args[0])
            {
                case "close":
                    CuiHelper.DestroyUi(player, "URMainPanel");
                    break;
            }
        }

        [ConsoleCommand("ur_feedback")]
        private void CMDFeedbackUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            switch (arg.Args[0])
            {
                case "close":
                    _playerFeedbackInfo.RemoveAll(x => x.SteamId == player.userID);
                    CuiHelper.DestroyUi(player, "URFeedbackPanel");
                    break;
                case "title":
                    var feedback = _playerFeedbackInfo.FirstOrDefault(x => x.SteamId == player.userID);
                    feedback.Title = arg.Args.Count() > 1 ? String.Join(" ", arg.Args.Skip(1)) : null;
                    FeedbackUIPanel(player);
                    break;
                case "message":
                    var messageFeedback = _playerFeedbackInfo.FirstOrDefault(x => x.SteamId == player.userID);
                    messageFeedback.Message = arg.Args.Count() > 1 ? String.Join(" ", arg.Args.Skip(1)) : null;
                    FeedbackUIPanel(player);
                    break;
                case "category":
                    var feedbackCat = _playerFeedbackInfo.FirstOrDefault(x => x.SteamId == player.userID);
                    feedbackCat.Category = _config.FeedbackSettings.FeedbackOptions[int.Parse(arg.Args[1])];
                    FeedbackUIPanel(player);
                    break;
                case "submit":
                    var feedbackSubmit = _playerFeedbackInfo.FirstOrDefault(x => x.SteamId == player.userID);
                    if(feedbackSubmit.Title == null || feedbackSubmit.Title.Length < 3) UIPopup(player, false, "Title cannot be null or less than 3 characters");
                    else if (feedbackSubmit.Message == null || feedbackSubmit.Message.Length < 15) UIPopup(player, false, "Message cannot be null or less than 15 characters");
                    else
                    {
                        Puts("FEEDBACK:{0}", JsonConvert.SerializeObject(new FeedbackInfo { SteamID = player.UserIDString, Category = feedbackSubmit.Category, Title = feedbackSubmit.Title, Message = feedbackSubmit.Message }));
                        _playerFeedbackInfo.RemoveAll(x => x.SteamId == player.userID);
                        CuiHelper.DestroyUi(player, "URFeedbackPanel");
                    }
                    break;
            }
        }

        private void CMDOpenURUI(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "ultimaterconplus.admin"))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            };

            var container = new CuiElementContainer();
            var mainPanel = container.Add(new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = "0 0",
                AnchorMax = "1 1"
            },
                Image = { Color = _config.FeedbackSettings.FeedbackUISettings.BlurPanel, Material = "assets/content/ui/uibackgroundblur.mat" },
                CursorEnabled = true
            }, "Overlay", "URMainPanel");
            CreatePanel(ref container, ".15 .15", ".85 .85", _config.FeedbackSettings.FeedbackUISettings.BackgroundColor, mainPanel, "secondaryPanel");
            if (_config.FeedbackSettings.FeedbackUISettings.BackgroundImage != string.Empty) CreateImagePanel(ref container, "0 0", "1 1", _config.FeedbackSettings.FeedbackUISettings.BackgroundImage, "secondaryPanel"); 

            CuiHelper.DestroyUi(player, "URMainPanel");
            CuiHelper.AddUi(player, container);
            UIPanel(player);
        }

        private void UIPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var enabledButtonColor = ".33 .82 .16 1";
            var disabledButtonColor = ".92 .13 .13 1";

            var secondaryPanel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "secondaryPanel", "secondaryOverlayPanel");
            var labelPanel = CreateLabel(ref container, "0 .9", "1 1", "0 0 0 .6", "1 1 1 1", "ULTIMATE RCON", 40, TextAnchor.MiddleCenter, secondaryPanel);
            CreateButton(ref container, ".93 0", "1 1", "1 1 1 0", "1 1 1 1", "X", 43, "ur_event close", labelPanel);

            // KILL

            CreateLabel(ref container, ".01 .84", ".25 .89", "0 0 0 .6", "1 1 1 1", "KILL LOGS", 20, TextAnchor.MiddleCenter, secondaryPanel);

            var playerKills = CreateLabel(ref container, ".01 .78", ".25 .83", "0 0 0 .5", "1 1 1 1", " PLAYER", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.KillSettings.PlayerKills? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main player", playerKills);

            var suicideKills = CreateLabel(ref container, ".01 .72", ".25 .77", "0 0 0 .5", "1 1 1 1", " SUICIDE", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.KillSettings.SuicideKills ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main suicide", suicideKills);

            var animalKills = CreateLabel(ref container, ".01 .66", ".25 .71", "0 0 0 .5", "1 1 1 1", " ANIMALS", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.KillSettings.AnimalKills? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main animals", animalKills);

            var npcKills = CreateLabel(ref container, ".01 .60", ".25 .65", "0 0 0 .5", "1 1 1 1", " NPC", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.KillSettings.NpcKills? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main npc", npcKills);

            // EVENT

            CreateLabel(ref container, ".01 .54", ".25 .59", "0 0 0 .6", "1 1 1 1", "EVENT SPAWNS", 20, TextAnchor.MiddleCenter, secondaryPanel);

            var patrolHeli = CreateLabel(ref container, ".01 .48", ".25 .53", "0 0 0 .5", "1 1 1 1", " PATROL HELI", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.EntitySpawnSettings.PatrolHelicopter ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main patrolheli", patrolHeli);

            var bradAPC = CreateLabel(ref container, ".01 .42", ".25 .47", "0 0 0 .5", "1 1 1 1", " BRAD APC", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.EntitySpawnSettings.BradAPC ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main bradapc", bradAPC);

            var cargoPlane = CreateLabel(ref container, ".01 .36", ".25 .41", "0 0 0 .5", "1 1 1 1", " CARGO PLANE", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.EntitySpawnSettings.CargoPlane ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main cargoplane", cargoPlane);

            var cargoShip = CreateLabel(ref container, ".01 .30", ".25 .35", "0 0 0 .5", "1 1 1 1", " CARGO SHIP", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.EntitySpawnSettings.CargoShip ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main cargoship", cargoShip);

            var ch47Heli = CreateLabel(ref container, ".01 .24", ".25 .29", "0 0 0 .5", "1 1 1 1", " CH47 HELI", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.EntitySpawnSettings.CH47Helicopter ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main ch47heli", ch47Heli);

            // PERMS

            CreateLabel(ref container, ".255 .84", ".50 .89", "0 0 0 .6", "1 1 1 1", "PERM CHANGE LOGS", 20, TextAnchor.MiddleCenter, secondaryPanel);

            var groupAdded = CreateLabel(ref container, ".255 .78", ".50 .83", "0 0 0 .5", "1 1 1 1", " GROUP GIVEN", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.UserPermissions.UserAddedToGroup ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main groupadded", groupAdded);

            var groupRemoved = CreateLabel(ref container, ".255 .72", ".50 .77", "0 0 0 .5", "1 1 1 1", " GROUP REMOVED", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.UserPermissions.UserRemovedFromGroup ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main groupremoved", groupRemoved);

            var permAdded = CreateLabel(ref container, ".255 .66", ".50 .71", "0 0 0 .5", "1 1 1 1", " PERM ADDED", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.UserPermissions.UserAddedToPerm ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main permadded", permAdded);

            var permRemoved = CreateLabel(ref container, ".255 .6", ".50 .65", "0 0 0 .5", "1 1 1 1", " PERM REMOVED", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.UserPermissions.UserRemovedFromPerm ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main permremoved", permRemoved);

            // CONNECTION

            CreateLabel(ref container, ".01 .18", ".25 .23", "0 0 0 .6", "1 1 1 1", "CONNECTION", 20, TextAnchor.MiddleCenter, secondaryPanel);

            var connects = CreateLabel(ref container, ".01 .12", ".25 .17", "0 0 0 .5", "1 1 1 1", " JOINS", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.ConnectionConfig.PlayerConnects ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main connects", connects);

            var disconnects = CreateLabel(ref container, ".01 .06", ".25 .11", "0 0 0 .5", "1 1 1 1", " LEAVES", 15, TextAnchor.MiddleLeft, secondaryPanel);
            CreateButton(ref container, ".89 .1", ".975 .82", _config.ConnectionConfig.PlayerDisconnects ? enabledButtonColor : disabledButtonColor, "1 1 1 1", " ", 15, "ur_main disconnects", disconnects);

            CuiHelper.DestroyUi(player, "secondaryOverlayPanel");
            CuiHelper.AddUi(player, container);
        }

        private void CMDOpenFeedbackUI(BasePlayer player, string command, string[] args)
        {
            _playerFeedbackInfo.Add(new PlayerFeedbackInfo { SteamId = player.userID, Message = null, Title = null, Category = _config.FeedbackSettings.FeedbackOptions[0] });

            var container = new CuiElementContainer();
            var mainPanel = container.Add(new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = "0 0",
                AnchorMax = "1 1"
            },
                Image = { Color = _config.FeedbackSettings.FeedbackUISettings.BlurPanel, Material = "assets/content/ui/uibackgroundblur.mat" },
                CursorEnabled = true
            }, "Overlay", "URFeedbackPanel");
            CreatePanel(ref container, ".15 .15", ".85 .85", _config.FeedbackSettings.FeedbackUISettings.BackgroundColor, mainPanel, "feedSecondaryPanel");
            if (_config.FeedbackSettings.FeedbackUISettings.BackgroundImage != string.Empty) CreateImagePanel(ref container, "0 0", "1 1", _config.FeedbackSettings.FeedbackUISettings.BackgroundImage, "feedSecondaryPanel");

            CuiHelper.DestroyUi(player, "URFeedbackPanel");
            CuiHelper.AddUi(player, container);
            FeedbackUIPanel(player);
        }

        private void FeedbackUIPanel(BasePlayer player)
        {
            var feedback = _playerFeedbackInfo.FirstOrDefault(x => x.SteamId == player.userID);

            var container = new CuiElementContainer();

            var secondaryPanel = CreatePanel(ref container, "0 0", ".997 .998", "0 0 0 0", "feedSecondaryPanel", "feedbackSecondaryOverlayPanel");
            var labelPanel = CreateLabel(ref container, "0 .9", "1 1", "0 0 0 .6", "1 1 1 1", "SERVER FEEDBACK", 40, TextAnchor.MiddleCenter, secondaryPanel);
            CreateButton(ref container, ".93 0", "1 1", "1 1 1 0", "1 1 1 1", "X", 43, "ur_feedback close", labelPanel);

            int totalOptions = _config.FeedbackSettings.FeedbackOptions.Count;
            float availableWidth = 0.95f - 0.01f * (totalOptions - 1);
            float buttonWidth = availableWidth / totalOptions;

            var i = 0;
            foreach (var category in _config.FeedbackSettings.FeedbackOptions)
            {
                float left = 0.025f + i * (buttonWidth + 0.01f);
                float right = left + buttonWidth;
                CreateButton(ref container, $"{left:F3} .8", $"{right:F3} .86", feedback.Category == category ? _config.FeedbackSettings.FeedbackUISettings.SelectedButtonColor : "0 0 0 .5", "1 1 1 1", category, 20, $"ur_feedback category {i}", secondaryPanel);
                i++;
            }

            CreatePanel(ref container, ".025 .68", ".975 .79", "0 0 0 .5", secondaryPanel);
            CreateLabel(ref container, ".025 .68", ".975 .79", "0 0 0 0", "1 1 1 .03", "TITLE", 40, TextAnchor.MiddleCenter, secondaryPanel);
            CreateInput(ref container, ".032 .68", ".975 .79", "ur_feedback title", "0 0 0 0", "1 1 1 .8", feedback.Title != null ? feedback.Title : " ", 30, TextAnchor.MiddleLeft, secondaryPanel);
            
            CreatePanel(ref container, ".025 .13", ".975 .67", "0 0 0 .5", secondaryPanel);
            CreateLabel(ref container, ".025 .13", ".975 .67", "0 0 0 0", "1 1 1 .03", "MESSAGE", 80, TextAnchor.MiddleCenter, secondaryPanel);
            CreateInput(ref container, ".032 .13", ".975 .66", "ur_feedback message", "0 0 0 0", "1 1 1 .8", feedback.Message != null ? feedback.Message : " ", 20, TextAnchor.UpperLeft, secondaryPanel);

            CreateButton(ref container, ".025 .04", ".973 .12", _config.FeedbackSettings.FeedbackUISettings.SubmitButtonColor, _config.FeedbackSettings.FeedbackUISettings.SubmitButtonTextColor, "SUBMIT", 35, "ur_feedback submit", secondaryPanel);

            CuiHelper.DestroyUi(player, "feedbackSecondaryOverlayPanel");
            CuiHelper.AddUi(player, container);
        }

        private void UIPopup(BasePlayer player, bool valid, string reason)
        {
            var container = new CuiElementContainer();
            var panelColor = valid ? ".35 1 .23 1" : "1 .22 .22 1";

            var UIPopup = CreatePanel(ref container, ".005 .87", ".2 .98", ".17 .17 .17 1", "URFeedbackPanel", "URUIPopup");
            CreatePanel(ref container, "0 0", ".03 1", panelColor, UIPopup);
            CreateLabel(ref container, ".06 0", "1 1", "0 0 0 0", "1 1 1 1", reason, 15, TextAnchor.MiddleLeft, UIPopup);

            CuiHelper.DestroyUi(player, "URUIPopup");
            CuiHelper.AddUi(player, container);

            timer.Once(2, () => CuiHelper.DestroyUi(player, UIPopup));
        }
        #endregion

        #region Methods
        private void SubscribeHooks(bool unSub = false)
        {
            if(!unSub) {
                if (!_subscribedHooks.Contains("OnPlayerDeath") && (_config.KillSettings.PlayerKills || _config.KillSettings.NpcKills))
                {
                    Subscribe(nameof(OnPlayerDeath));
                    _subscribedHooks.Add("OnPlayerDeath");
                }

                if (!_subscribedHooks.Contains("OnEntityDeath") && _config.KillSettings.AnimalKills)
                {
                    Subscribe(nameof(OnEntityDeath));
                    _subscribedHooks.Add("OnEntityDeath");
                }

                if (!_subscribedHooks.Contains("OnUserGroupAdded") && _config.UserPermissions.UserAddedToGroup)
                {
                    Subscribe(nameof(OnUserGroupAdded));
                    _subscribedHooks.Add("OnUserGroupAdded");
                }

                if (!_subscribedHooks.Contains("OnUserGroupRemoved") && _config.UserPermissions.UserRemovedFromGroup)
                {
                    Subscribe(nameof(OnUserGroupRemoved));
                    _subscribedHooks.Add("OnUserGroupRemoved");
                }

                if (!_subscribedHooks.Contains("OnUserPermissionGranted") && _config.UserPermissions.UserAddedToPerm)
                {
                    Subscribe(nameof(OnUserPermissionGranted));
                    _subscribedHooks.Add("OnUserPermissionGranted");
                }

                if (!_subscribedHooks.Contains("OnUserPermissionRevoked") && _config.UserPermissions.UserRemovedFromPerm)
                {
                    Subscribe(nameof(OnUserPermissionRevoked));
                    _subscribedHooks.Add("OnUserPermissionRevoked");
                }

                if (!_subscribedHooks.Contains("OnPlayerConnected") && _config.ConnectionConfig.PlayerConnects)
                {
                    Subscribe(nameof(OnPlayerConnected));
                    _subscribedHooks.Add("OnPlayerConnected");
                }

                if (!_subscribedHooks.Contains("OnPlayerConnected") && _config.ConnectionConfig.PlayerDisconnects)
                {
                    Subscribe(nameof(OnPlayerConnected));
                    _subscribedHooks.Add("OnPlayerConnected");
                }
            } 
            else
            {
                if (!_config.KillSettings.PlayerKills && !_config.KillSettings.NpcKills)
                {
                    Unsubscribe(nameof(OnPlayerDeath));
                    _subscribedHooks.RemoveAll(x => x == "OnPlayerDeath");
                };

                if (!_config.KillSettings.AnimalKills)
                {
                    Unsubscribe(nameof(OnEntityDeath));
                    _subscribedHooks.RemoveAll(x => x == "OnEntityDeath");
                };

                if (!_config.UserPermissions.UserAddedToGroup)
                {
                    Unsubscribe(nameof(OnUserGroupAdded));
                    _subscribedHooks.RemoveAll(x => x == "OnUserGroupAdded");
                }

                if (!_config.UserPermissions.UserRemovedFromGroup)
                {
                    Unsubscribe(nameof(OnUserGroupRemoved));
                    _subscribedHooks.RemoveAll(x => x == "OnUserGroupRemoved");
                }

                if (!_config.UserPermissions.UserAddedToPerm)
                {
                    Unsubscribe(nameof(OnUserPermissionGranted));
                    _subscribedHooks.RemoveAll(x => x == "OnUserPermissionGranted");
                }

                if (!_config.UserPermissions.UserRemovedFromPerm)
                {
                    Unsubscribe(nameof(OnUserPermissionRevoked));
                    _subscribedHooks.RemoveAll(x => x == "OnUserPermissionRevoked");
                }

                if (!_config.ConnectionConfig.PlayerConnects)
                {
                    Unsubscribe(nameof(OnPlayerConnected));
                    _subscribedHooks.RemoveAll(x => x == "OnPlayerConnected");
                }

                if (!_config.ConnectionConfig.PlayerDisconnects)
                {
                    Unsubscribe(nameof(OnPlayerConnected));
                    _subscribedHooks.RemoveAll(x => x == "OnPlayerConnected");
                }
            }
        }

        private void RegisterCommands()
        {
            cmd.AddChatCommand("urplus", this, CMDOpenURUI);

            if (_config.FeedbackSettings.EnabledFeedback) foreach (var command in _config.FeedbackSettings.FeedbackCommands)
                    cmd.AddChatCommand(command, this, CMDOpenFeedbackUI);

            permission.RegisterPermission("ultimaterconplus.admin", this);
        }
        #endregion

        #region UI Methods
        private static string CreateItemPanel(ref CuiElementContainer container, string anchorMin, string anchorMax, float padding, string color, int itemId, string parent = "Overlay",
           string panelName = null, ulong skinId = 0L)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Image = { Color = color }
            }, parent, panelName);

            container.Add(new CuiElement
            {
                Parent = panel,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{padding} {padding + .004f}",
                        AnchorMax = $"{1 - padding - .004f} {1 - padding - .02f}"
                    },
                    new CuiImageComponent {ItemId = itemId, SkinId = skinId}
                }
            });

            return panel;
        }

        private static string CreateLabel(ref CuiElementContainer container, string anchorMin, string anchorMax, string backgroundColor, string textColor,
            string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay",
            string labelName = null, bool blur = false)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, backgroundColor, parent, blur: blur);
            container.Add(new CuiLabel
            {
                Text =
                {
                    Color = textColor,
                    Text = labelText,
                    Align = alignment,
                    FontSize = fontSize,
                    Font = "robotocondensed-bold.ttf"
                }
            }, panel);
            return panel;
        }

        private static string CreatePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string parent = "Overlay",
            string panelName = null, bool blur = false)
        {
            if (blur)
                return container.Add(new CuiPanel
                {
                    RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                    Image = { Color = panelColor, Material = "assets/content/ui/uibackgroundblur.mat" }
                }, parent, panelName);
            else
                return container.Add(new CuiPanel
                {
                    RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                    Image = { Color = panelColor }
                }, parent, panelName);
        }

        private static void CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelImage, string parent = "Overlay",
        string panelName = null)
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = panelName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax

                    },
                    new CuiRawImageComponent {Url = panelImage},
                }
            });
        }

        private static string CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText,
        int fontSize, string buttonCommand, string parent = "Overlay",
        TextAnchor labelAnchor = TextAnchor.MiddleCenter)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Image = { Color = "0 0 0 0" }
            }, parent);

            container.Add(new CuiButton
            {
                Button = { Color = buttonColor, Command = $"{buttonCommand}" },
                Text = { Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText }
            }, panel);
            return panel;
        }

        private static string CreateInput(ref CuiElementContainer container, string anchorMin, string anchorMax, string command, string backgroundColor, string textColor,
        string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay",
        string labelName = null)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin, AnchorMax = anchorMax
                },
                Image = { Color = backgroundColor }
            }, parent, labelName);

            container.Add(new CuiElement
            {
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = textColor,
                        Text = labelText,
                        Align = alignment,
                        FontSize = fontSize,
                        Font = "robotocondensed-bold.ttf",
                        NeedsKeyboard = true,
                        Command = command
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                Parent = panel
            });

            return panel;
        }
        #endregion
    }
}
