using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections;
using System.IO;

namespace Oxide.Plugins
{
    [Info("HudController", "Amino", "1.0.3")]
    [Description("An advanced hud UI")]
    public class HudController : RustPlugin
    {
        [PluginReference] Plugin ServerRewards, Economics, ImageLibrary;

        #region Config
        public class Configuration
        {
            public string ServerName = "Random Server Name";
            [JsonProperty(PropertyName = "Currency (ServerRewards, Economics)")]
            public string Currency { get; set; } = "ServerRewards";
            public string INFO { get; set; } = "Do not change the Name of the normal events!";
            public List<EventsClass> Events { get; set; } = new List<EventsClass>();
            public List<ExternalHookClass> ExternalHooks { get; set; } = new List<ExternalHookClass>();
            public List<SetCommands> Commands { get; set; } = new List<SetCommands>();
            public UIPositions UIPositions { get; set; } = new UIPositions();
            public static Configuration DefaultConfig()
            {
                return new Configuration()
                {
                    Events = new List<EventsClass>()
                    {
                        new EventsClass()
                        {
                            Enabled = true,
                            Name = "Patrol",
                            RunningIcon = "https://i.ibb.co/tmVxkk8/Heli-Active1.png",
                            NotRunningIcon = "https://i.ibb.co/h78TSz3/Heli-Inactive1.png"
                        },
                        new EventsClass()
                        {
                            Enabled = true,
                            Name = "Cargo",
                            RunningIcon = "https://i.ibb.co/h8NRcbK/Cargo-Active1.png",
                            NotRunningIcon = "https://i.ibb.co/JKcwkYS/Cargo-Inactive1.png"
                        },
                        new EventsClass()
                        {
                            Enabled = true,
                            Name = "Bradley",
                            RunningIcon = "https://i.ibb.co/Q6nMNdG/Bradley-Active1.png",
                            NotRunningIcon = "https://i.ibb.co/mrpQGFh/Bradley-Inactive1.png"
                        },
                        new EventsClass()
                        {
                            Enabled = true,
                            Name = "CH47",
                            NotRunningIcon = "https://i.ibb.co/w6NsNsw/Chinook-Inactive1.png",
                            RunningIcon = "https://i.ibb.co/SK8FZJS/Chinook-Active1.png"
                        }
                    },
                    ExternalHooks = new List<ExternalHookClass>()
                    {
                        new ExternalHookClass()
                        {
                            Enabled = true,
                            ExternalHookStart = "OnConvoyStart",
                            ExternalHookEnd = "OnConvoyStop",
                            RunningIcon = "https://i.ibb.co/nBxqhWz/Convoy-Active.png",
                            NotRunningIcon = "https://i.ibb.co/kmPh9cb/Convoy-Inactive.png",
                            HookName = "Convoy"
                        }
                    },
                    Commands = new List<SetCommands>()
                    {
                        new SetCommands()
                        {
                            CommandName = "INFO",
                            Command = "chat.say /info"
                        },
                        new SetCommands()
                        {
                            CommandName = "SHOP",
                            Command = "chat.say /shop"
                        },
                        new SetCommands()
                        {
                            CommandName = "KITS",
                            Command = "chat.say /kits"
                        },
                    }
                };
            }
        }

        private static Configuration _config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();

                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region UI Class
        public class UIPositions
        {
            public Dictionary<string, UIParts> UIPartsDict { get; set; } = new Dictionary<string, UIParts>();

            public UIPositions()
            {
                UIPartsDict.Add("BackgroundPanel", new UIParts { Anchors = { XMin = .007, YMin = .99, XMax = .007, YMax = .99 }, Offsets = { UseOffsets = true, XMax = 250, YMin = -30 }, PanelSettings = { Color = "0 0 0 0", Parent = "Overlay" }, IsEditable = false, Section = "Main", AlwaysDisplay = true });
                UIPartsDict.Add("OpenCloseButton", new UIParts { Anchors = { XMin = 0, YMin = -.06, XMax = .13, YMax = 1.06 }, PanelSettings = { Color = "0 0 0 .6", Parent = "BackgroundPanel", FadeIn = .5f }, CommandSettings = { Command = "hc_main close", IsButton = true }, IsEditable = false, Section = "Main", AlwaysDisplay = true, Image = "https://i.ibb.co/Rb4RLTP/HomeIcon.png" });
                UIPartsDict.Add("NextSectionButton", new UIParts { Anchors = { XMin = 0, YMin = -.5, XMax = .13, YMax = -.16 }, PanelSettings = { Color = "0 0 0 .6", Parent = "BackgroundPanel", FadeIn = .5f }, CommandSettings = { Command = "hc_main extend", IsButton = true }, IsEditable = false, Section = "Main", DisplayWithMoreThanOneSection = false, Image = "https://i.ibb.co/Sx7fqYb/DropIcon.png" });
                UIPartsDict.Add("PanelIconBar", new UIParts { Anchors = { XMin = .14, YMin = 0, XMax = 1, YMax = 1 }, PanelSettings = { Color = "0 0 0 .6", Parent = "BackgroundPanel", FadeIn = 1f }, IsEditable = false, Section = "Main" });
                UIPartsDict.Add("PanelIconLayout", new UIParts { Anchors = { XMin = .01, YMin = .05, XMax = .135, YMax = .92 }, PanelSettings = { Color = "0 0 0 0", Parent = "PanelIconBar", FadeIn = 1f }, IsEditable = true, Section = "Main", ListSettings = { ButtonSpacing = .135, Vertical = false, IsList = true }, ActuallyLoad = false });
                
                UIPartsDict.Add("CommandsPanel1", new UIParts { Anchors = { XMin = 0, YMin = -2, XMax = .13, YMax = -.15 }, PanelSettings = { Color = "0 0 0 .6", Parent = "BackgroundPanel", FadeIn = .5f }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("CommandsPanel2", new UIParts { Anchors = { XMin = .14, YMin = -2, XMax = 1, YMax = -.15 }, PanelSettings = { Color = "0 0 0 .6", Parent = "BackgroundPanel", FadeIn = 1f }, IsEditable = false, Section = "CommandsDropdown" });

                UIPartsDict.Add("PlayersPanel", new UIParts { Anchors = { XMin = .1, YMin = .66, XMax = .3, YMax = .98 }, Text = { Text = "{onlinePlayers}", Alignment = TextAnchor.MiddleLeft }, PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("PlayersPanelIcon", new UIParts { Anchors = { XMin = .01, YMin = .7, XMax = .08, YMax = .95 }, Image = "https://i.ibb.co/jv5L2r7/Players-Online.png", PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("PlayersJoiningPanel", new UIParts { Anchors = { XMin = .1, YMin = .33, XMax = .3, YMax = .64 }, Text = { Text = "{joiningPlayers}", Alignment = TextAnchor.MiddleLeft }, PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("PlayersJoiningPanelIcon", new UIParts { Anchors = { XMin = .01, YMin = .38, XMax = .08, YMax = .6 }, Image = "https://i.ibb.co/t3FwGZM/Players-Joining.png", PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("PlayersQueuedPanel", new UIParts { Anchors = { XMin = .1, YMin = .04, XMax = .3, YMax = .3 }, Text = { Text = "{queuedPlayers}", Alignment = TextAnchor.MiddleLeft }, PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("PlayersQueuedPanelIcon", new UIParts { Anchors = { XMin = .01, YMin = 0, XMax = .08, YMax = .33 }, Image = "https://i.ibb.co/QbzYKHw/Players-Queued.png", PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("ServerNamePanel", new UIParts { Anchors = { XMin = .3, YMin = .04, XMax = 1, YMax = .3 }, Text = { Text = "{configServerName}", Alignment = TextAnchor.MiddleLeft }, PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("ServerTimePanel", new UIParts { Anchors = { XMin = .3, YMin = .33, XMax = .66, YMax = .66 }, Text = { Text = "{serverTime}", Alignment = TextAnchor.MiddleLeft }, PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" }); 
                UIPartsDict.Add("PlayerGridPanel", new UIParts { Anchors = { XMin = .66, YMin = .33, XMax = 1, YMax = .66 }, Text = { Text = "{playerGrid}", Alignment = TextAnchor.MiddleLeft }, PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("PlayerCurrencyPanel", new UIParts { Anchors = { XMin = .3, YMin = .66, XMax = 1, YMax = .98 }, Text = { Text = "{playerCurrency}", Alignment = TextAnchor.MiddleLeft }, PanelSettings = { Color = "0 0 0 0", Parent = "CommandsPanel2" }, IsEditable = false, Section = "CommandsDropdown" });

                UIPartsDict.Add("LastSectionButton", new UIParts { Anchors = { XMin = 0, YMin = -2.43, XMax = .13, YMax = -2.09 }, PanelSettings = { Color = "0 0 0 .6", Parent = "BackgroundPanel", FadeIn = .5f }, CommandSettings = { Command = "hc_main extend", IsButton = true }, IsEditable = false, Section = "CommandsDropdown", Image = "https://i.ibb.co/rcY7NZy/ColICon.png" });
                UIPartsDict.Add("CommandsSectionButton", new UIParts { Anchors = { XMin = .14, YMin = -2.43, XMax = 1, YMax = -2.09 }, PanelSettings = { Color = "0 0 0 .6", Parent = "BackgroundPanel", FadeIn = .5f }, Text = { Text = "COMMANDS", Size = 10 }, CommandSettings = { Command = "hc_main commands", IsButton = true }, IsEditable = false, Section = "CommandsDropdown" });
                UIPartsDict.Add("CommandsDropdown", new UIParts { Anchors = { XMin = .14, YMin = -3.25, XMax = 1, YMax = -2.52 }, PanelSettings = { Color = "0 0 0 .6", Parent = "BackgroundPanel", FadeIn = .5f }, IsEditable = false, Section = "CommandsList" });
                UIPartsDict.Add("CommandsPanelLayout", new UIParts { Anchors = { XMin = .01, YMin = .1, XMax = .99, YMax = .96 }, Text = { Size = 12 }, PanelSettings = { Color = "0 0 0 .3", Parent = "CommandsDropdown", FadeIn = 1f }, IsEditable = true, Section = "CommandsList", ListSettings = { ButtonSpacing = .04, Vertical = true, IsList = true }, CommandSettings = { Command = "hc_main command {0}", IsButton = true }, ActuallyLoad = false });

            }

            public UIPositions Clone()
            {
                UIPositions clonedPositions = new UIPositions();
                clonedPositions.UIPartsDict.Clear();

                foreach (var entry in UIPartsDict)
                {
                    clonedPositions.UIPartsDict.Add(entry.Key, entry.Value.Clone());
                }

                return clonedPositions;
            }

            public List<string> GetAllUIPartNames()
            {
                return UIPartsDict.Keys.ToList();
            }
        }

        public class UIParts
        {
            public bool IsEditable = true;
            public bool ActuallyLoad = true;
            public string Image { get; set; } = null;
            public string Section = String.Empty;
            public bool DisplayWithMoreThanOneSection = true;
            public bool AlwaysDisplay = false;
            public CommandSettings CommandSettings = new CommandSettings();
            public PanelSettings PanelSettings = new PanelSettings();
            public ListSettings ListSettings = new ListSettings();
            public UIAnchors Anchors = new UIAnchors();
            public UIOffsets Offsets = new UIOffsets();
            public UIText Text = new UIText();

            public UIParts Clone()
            {
                UIParts clone = new UIParts();

                clone.ActuallyLoad = this.ActuallyLoad;
                clone.IsEditable = this.IsEditable;
                clone.Image = this.Image;
                clone.Section = this.Section;
                clone.DisplayWithMoreThanOneSection = this.DisplayWithMoreThanOneSection;
                clone.AlwaysDisplay = this.AlwaysDisplay;
                clone.CommandSettings = this.CommandSettings.Clone();
                clone.PanelSettings = this.PanelSettings.Clone();
                clone.ListSettings = this.ListSettings.Clone();
                clone.Anchors = this.Anchors.Clone();
                clone.Offsets = this.Offsets.Clone();
                clone.Text = this.Text.Clone();

                return clone;
            }
        }

        public class CommandSettings
        {
            public bool IsButton = false;
            public string Command = string.Empty;
            public CommandSettings Clone()
            {
                return (CommandSettings)this.MemberwiseClone();
            }
        }

        public class PanelSettings
        {
            public string Color { get; set; } = "0 0 0 .1";
            public bool Blur { get; set; } = false;
            public float FadeIn { get; set; } = 0f;
            public string Parent = "Overlay";
            public PanelSettings Clone()
            {
                PanelSettings clone = new PanelSettings();

                clone.Color = this.Color;
                clone.Blur = this.Blur;
                clone.FadeIn = this.FadeIn;
                clone.Parent = this.Parent;

                return clone;
            }
        }

        public class ListSettings
        {
            public bool Vertical = true;
            public double ButtonSpacing = .15;
            public bool IsList = false;
            public ListSettings Clone()
            {
                return (ListSettings)this.MemberwiseClone();
            }
        }

        public class UIAnchors
        {
            public double XMin { get; set; } = 0;
            public double XMax { get; set; } = 0;
            public double YMin { get; set; } = 0;
            public double YMax { get; set; } = 0;
            public UIAnchors Clone()
            {
                return (UIAnchors)this.MemberwiseClone();
            }
        }

        public class UIOffsets
        {
            public bool UseOffsets = false;
            public double XMin { get; set; }
            public double XMax { get; set; }
            public double YMin { get; set; }
            public double YMax { get; set; }
            public UIOffsets Clone()
            {
                return (UIOffsets)this.MemberwiseClone();
            }
        }

        public class UIText
        {
            public string Text { get; set; } = String.Empty;
            public string Color { get; set; } = "1 1 1 1";
            public int Size { get; set; } = 15;
            public string Font = "robotocondensed-bold.ttf";
            public TextAnchor Alignment { get; set; } = TextAnchor.MiddleCenter;
            public UIText Clone()
            {
                return (UIText)this.MemberwiseClone();
            }
        }
        #endregion

        #region Class' & Constructors
        public Dictionary<ulong, EditUIPositions> _editPeople = new Dictionary<ulong, EditUIPositions>();
        public Dictionary<ulong, PlayerOptions> _playerOptions = new Dictionary<ulong, PlayerOptions>();
        public Dictionary<string, bool> _events = new Dictionary<string, bool>();
        public Dictionary<string, bool> _oldEvents = new Dictionary<string, bool>();
        public ServerInfoParsed _serverInfo = new ServerInfoParsed();
        System.Random rnd = new System.Random();
        public double lastUpdated = 0;

        public class SetCommands
        {
            public string CommandName { get; set; } = string.Empty;
            public string Command { get; set; } = string.Empty;
            public string CommandImage { get; set; } = string.Empty;
        }

        public class PlayerOptions
        {
            public List<string> neededPanels = new List<string>() { "Main" };
            public List<string> lastOpened = new List<string>() { "Main" };
        }

        public class ServerInfoParsed
        {
            public string Hostname { get; set; }
            public int MaxPlayers { get; set; }
            public int Players { get; set; }
            public int JoiningPlayers { get; set; }
            public int QueuedPlayers { get; set; }
            public string Time { get; set; }
        }

        public class EditUIPositions
        {
            public string ActiveUI { get; set; }
            public string ActiveUIPage { get; set; }
            public int EditPage { get; set; }
            public UIAnchors EditPanelPosition = new UIAnchors { XMin = .01, YMin = .02, XMax = .3, YMax = .7 };
            public UIPositions UIPositions = new UIPositions();
        }

        public class EventsClass
        {
            public bool Enabled { get; set; } = false;
            public string Name { get; set; } = string.Empty;
            public string RunningIcon { get; set; } = string.Empty;
            public string NotRunningIcon { get; set; } = string.Empty;
        }

        public class ExternalHookClass
        {
            public bool Enabled { get; set; } = false;
            public string HookName { get; set; } = string.Empty;
            public string ExternalHookStart { get; set; } = string.Empty;
            public string ExternalHookEnd { get; set; } = string.Empty;
            public string RunningIcon { get; set; } = string.Empty;
            public string NotRunningIcon { get; set; } = string.Empty;
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["RPPlaceholder"] = "RP: {0}",
                ["EconomicsPlaceholder"] = "${0}",
            }, this, "en");
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Hooks
        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    foreach (var UIPart in _config.UIPositions.UIPartsDict)
                    {
                        if (UIPart.Value.PanelSettings.Parent.Equals("Overlay", StringComparison.OrdinalIgnoreCase)) CuiHelper.DestroyUi(player, UIPart.Key);
                        if (UIPart.Value.PanelSettings.Parent.Equals("Hud", StringComparison.OrdinalIgnoreCase)) CuiHelper.DestroyUi(player, UIPart.Key);
                    }

                    if (_editPeople.ContainsKey(player.userID))
                    {
                        foreach (var panel in _editPeople[player.userID].UIPositions.UIPartsDict.Where(x => x.Value.PanelSettings.Parent == "Overlay" || x.Value.PanelSettings.Parent == "Hud")) CuiHelper.DestroyUi(player, panel.Key);
                        CuiHelper.DestroyUi(player, "HCEditBG");
                        _editPeople.Remove(player.userID);
                    }

                    CuiHelper.DestroyUi(player, "HCEditBG");
                }

            _config = null;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if(!_playerOptions.ContainsKey(player.userID)) _playerOptions.Add(player.userID, new PlayerOptions());
            ServerMgr.Instance.StartCoroutine(CreateMainUI(player, false));
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null || !player.userID.Get().IsSteamId()) return;
            if (_playerOptions.ContainsKey(player.userID)) _playerOptions.Remove(player.userID);
        }

        void OnServerInitialized(bool initial)
        {
            RegisterCommandsAndPermissions();
            ImportImages();

            foreach (var evnt in _config.Events) if(evnt.Enabled) _events.Add(evnt.Name, false);
            foreach (var extHook in _config.ExternalHooks) if(extHook.Enabled) _events.Add(extHook.HookName, false);

            foreach (var player in BasePlayer.activePlayerList)
            {
                _playerOptions.Add(player.userID, new PlayerOptions());
                ServerMgr.Instance.StartCoroutine(CreateMainUI(player, false));
            }

            WriteNewHooks();
            StartUpdateServerInfo();
            CheckAllEntities();
            StartUpdateUI();
        }

        void OnEntitySpawned(BaseCombatEntity entity)
        {
            if (entity == null) return;

            if (entity is PatrolHelicopter && _events.ContainsKey("Patrol")) _events["Patrol"] = true;
            else if (entity is BradleyAPC && _events.ContainsKey("Bradley")) _events["Bradley"] = true;
            else if (entity is CH47Helicopter && _events.ContainsKey("CH47")) _events["CH47"] = true;
            else if (entity is CargoShip && _events.ContainsKey("Cargo")) _events["Cargo"] = true;
        }

        void OnEntityKill(BaseCombatEntity entity)
        {
            if (entity == null) return;

            if (entity is PatrolHelicopter && _events.ContainsKey("Patrol")) _events["Patrol"] = false;
            else if (entity is BradleyAPC && _events.ContainsKey("Bradley")) _events["Bradley"] = false; 
            else if (entity is CH47Helicopter && _events.ContainsKey("CH47")) _events["CH47"] = false;
            else if (entity is CargoShip && _events.ContainsKey("Cargo")) _events["Cargo"] = false; 

        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            if (entity is PatrolHelicopter && _events.ContainsKey("Patrol")) _events["Patrol"] = false;
            else if (entity is BradleyAPC && _events.ContainsKey("Bradley")) _events["Bradley"] = false;
            else if (entity is CH47Helicopter && _events.ContainsKey("CH47")) _events["CH47"] = false;
            else if (entity is CargoShip && _events.ContainsKey("Cargo")) _events["Cargo"] = false;
        }
        #endregion

        #region Methods
        void StartUpdateUI() => timer.Every(3, UpdateAllUI);
        void StartUpdateServerInfo() => timer.Every(30, GetServerInfo);

        void UpdateAllUI()
        {
            var currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (!_oldEvents.SequenceEqual(_events) || currentTime - 30 > lastUpdated)
            {
                GetServerInfo();

                foreach (var player in BasePlayer.activePlayerList)
                {
                    ServerMgr.Instance.StartCoroutine(CreateMainUI(player, false));
                }

                lastUpdated = currentTime;
                _oldEvents = new Dictionary<string, bool>(_events);
            }
        }

        private void CheckAllEntities()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity == null || entity.IsDestroyed) continue;
                ServerMgr.Instance.StartCoroutine(CheckEntity(entity));
            }
        }

        IEnumerator CheckEntity(BaseNetworkable entity)
        {
            if (entity is PatrolHelicopter && _events.ContainsKey("Patrol")) _events["Patrol"] = true;
            else if (entity is BradleyAPC && _events.ContainsKey("Bradley")) _events["Bradley"] = true;
            else if (entity is CH47Helicopter && _events.ContainsKey("CH47")) _events["CH47"] = true;
            else if (entity is CargoShip && _events.ContainsKey("Cargo")) _events["Cargo"] = true;

            yield return null;
        }

        string GetReplacedText(BasePlayer player, string text = null)
        {
            if (text == null) text = string.Empty;

            var replacements = new Dictionary<string, string>
            {
                { "{serverName}", $"{_serverInfo.Hostname} " },
                { "{configServerName}", $"{_config.ServerName} " },
                { "{onlinePlayers}", $"{_serverInfo.Players}" },
                { "{joiningPlayers}", $"{_serverInfo.JoiningPlayers}" },
                { "{queuedPlayers}", $"{_serverInfo.QueuedPlayers}" },
                { "{totalPlayers}", $"{_serverInfo.QueuedPlayers + _serverInfo.JoiningPlayers + _serverInfo.Players}" },
                { "{playerGrid}", "getGrid" },
                { "{serverTime}", $"{_serverInfo.Time}" },
                { "{playerCurrency}", $"getCurrency" },
                { "{maxPlayers}", $"{_serverInfo.MaxPlayers}" }
            };

            foreach (var placeholder in replacements)
            {
                if (text.Contains(placeholder.Key))
                {
                    var theValue = placeholder.Value;
                    if (placeholder.Value == "getGrid") theValue = GatherGrid(player);
                    else if (placeholder.Value == "getCurrency") theValue = GetPlayerCurrency(player);
                    text = text.Replace(placeholder.Key, theValue);
                }
            }

            return text;
        }

        private void GetServerInfo()
        {
            var serverInfo = ConVar.Admin.ServerInfo();
            var timeParts = serverInfo.GameTime.Split(' ')[1].Split(':');

            var theHour = int.Parse(timeParts[0]);
            var timeInfo = theHour >= 12 ? $"{(theHour > 12 ? theHour - 12 : theHour)}:{timeParts[1]}PM" : $"{theHour}:{timeParts[1]}AM";
            _serverInfo = new ServerInfoParsed { Hostname = serverInfo.Hostname, MaxPlayers = serverInfo.MaxPlayers, Players = serverInfo.Players, JoiningPlayers = serverInfo.Joining, QueuedPlayers = serverInfo.Queued, Time = $"{timeInfo}" };
        }

        string GatherGrid(BasePlayer player)
        {
            var worldSize = ConVar.Server.worldsize;
            var position = player.transform.position;

            char gridLetter = 'A';
            var xPos = Mathf.Floor((position.x + (worldSize / 2)) / 146.3f) % 26;
            var zPos = (Mathf.Floor(worldSize / 146.3f)) - Mathf.Floor((position.z + (worldSize / 2)) / 146.3f) - 1;
            gridLetter = (char)(gridLetter + xPos);
            return $"{gridLetter}{zPos}";
        }

        string GetPlayerCurrency(BasePlayer player)
        {
            var points = " ";

            if (_config.Currency.Equals("serverrewards", StringComparison.OrdinalIgnoreCase))
            {
                var checkPoints = ServerRewards?.Call("CheckPoints", (ulong)player.userID);
                points = Lang("RPPlaceholder", player.UserIDString, checkPoints == null ? "N/A" : $"{checkPoints}");
            } else if (_config.Currency.Equals("economics", StringComparison.OrdinalIgnoreCase))
            {
                var checkPoints = Economics?.Call("Balance", (ulong)player.userID);
                points = Lang("EconomicsPlaceholder", player.UserIDString, checkPoints == null ? "N/A" : $"{checkPoints}");
            }

            return points;
        }

        private void OnCustomHookCalled(string hookName, bool isStart) => _events[hookName] = isStart;

        private void WriteNewHooks()
        {
            var sourcePath = $"{Interface.Oxide.PluginDirectory}{Path.DirectorySeparatorChar}HudController.cs";
            var sourceFile = File.ReadAllText(sourcePath);
            int regionStart = sourceFile.LastIndexOf("#region AddedPluginHooks"), regionEnd = sourceFile.LastIndexOf("#endregion");

            if (regionStart == -1 || regionEnd == -1) return;

            var newCode = "#region AddedPluginHooks\n";

            foreach (var hook in _config.ExternalHooks)
            {
                if (!hook.Enabled) continue;
                newCode += $"private void {hook.ExternalHookStart}()=>OnCustomHookCalled(\"{hook.HookName}\", true);";
                newCode += $"private void {hook.ExternalHookEnd}()=>OnCustomHookCalled(\"{hook.HookName}\", false);";
            }

            newCode += "\n#endregion";
            var oldCode = sourceFile.Substring(regionStart, regionEnd - regionStart + 10);

            if (oldCode.Equals(newCode)) return;

            File.WriteAllText(sourcePath, sourceFile.Replace(oldCode, newCode));
        }

        PlayerOptions GetOrLoadPlayerOptions(BasePlayer player)
        {
            if(!_playerOptions.ContainsKey(player.userID)) _playerOptions.Add(player.userID, new PlayerOptions());

            return _playerOptions[player.userID];
        }

        EditUIPositions GetOrLoadEditPositions(BasePlayer player, bool loadNew = false)
        {
            if (!loadNew && _editPeople.ContainsKey(player.userID))
            {
                return _editPeople[player.userID];
            }
            else
            {
                EditUIPositions editPos = new EditUIPositions
                {
                    UIPositions = _config.UIPositions.Clone(),
                    ActiveUI = _config.UIPositions.UIPartsDict.First().Key
                };

                _editPeople[player.userID] = editPos;

                return editPos;
            }
        }

        void CheckIfPanelsInclude(PlayerOptions playerOptions, string section)
        {
            playerOptions.lastOpened = playerOptions.neededPanels.Select(x => x).ToList();

            if (playerOptions.neededPanels.Contains(section))
            {
                if(section.Equals("CommandsDropdown", StringComparison.OrdinalIgnoreCase)) playerOptions.neededPanels.Remove("CommandsList");
                playerOptions.neededPanels.Remove(section);
            }
            else playerOptions.neededPanels.Add(section);
        }

        private void RegisterNewImage(string imageName, string imageUrl)
        {
            ImageLibrary?.Call("AddImage", imageUrl, imageName, 0UL, null);
        }

        void RegisterCommandsAndPermissions()
        {
            permission.RegisterPermission("hudcontroller.admin", this);
            cmd.AddChatCommand("hudedit", this, UIOpenEditMenu);
        }

        private string GetImage(string imageName)
        {
            if (ImageLibrary == null)
            {
                PrintError("Could not load images due to no Image Library");
                return null;
            }

            return ImageLibrary?.Call<string>("GetImage", "UI" + imageName, 0UL, false);
        }

        void ImportImages()
        {
            Dictionary<string, string> images = new Dictionary<string, string> {
                { "UILeftArrow", "https://i.ibb.co/pLsjbtx/Left-Arrow.png" },
                { "UIRightArrow", "https://i.ibb.co/mCRYNQG/Right-Arrow.png" },
                { "UIUpArrow", "https://i.ibb.co/HdvBXCz/UpArrow.png" },
                { "UIDownArrow", "https://i.ibb.co/0GGnFNB/Down-Arrow.png" }
            };

            foreach (var item in _config.UIPositions.UIPartsDict.Where(x => !string.IsNullOrEmpty(x.Value.Image))) images.Add($"UI{item.Key}", item.Value.Image);

            foreach (var image in _config.Events)
            {
                images.Add($"UIActive{image.Name}", image.RunningIcon);
                images.Add($"UIInactive{image.Name}", image.NotRunningIcon);
            }

            foreach (var image in _config.ExternalHooks)
            {
                images.Add($"UIActive{image.HookName}", image.RunningIcon);
                images.Add($"UIInactive{image.HookName}", image.NotRunningIcon);
            }

            ImageLibrary?.Call("ImportImageList", "HudController", images, 0UL, true, null);
        }
        #endregion

        #region Commands
        [ConsoleCommand("hc_main")]
        private void CMDHudMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            switch (arg.Args[0])
            {
                case "extend":
                    var playerOptions = GetOrLoadPlayerOptions(player);
                    CheckIfPanelsInclude(playerOptions, "CommandsDropdown");
                    playerOptions.lastOpened = playerOptions.neededPanels.Select(x => x).ToList();
                    break;
                case "close":
                    playerOptions = GetOrLoadPlayerOptions(player);
                    playerOptions.lastOpened = playerOptions.neededPanels.Select(x => x).ToList();
                    if (playerOptions.neededPanels.Count > 0) playerOptions.neededPanels = new List<string>();
                    else playerOptions.neededPanels = new List<string>() { "Main" };
                    break;
                case "commands":
                    playerOptions = GetOrLoadPlayerOptions(player);
                    CheckIfPanelsInclude(playerOptions, "CommandsList");
                    playerOptions.lastOpened = playerOptions.neededPanels.Select(x => x).ToList();
                    break;
                case "command":
                    player.SendConsoleCommand(String.Join(" ", arg.Args.Skip(1)));
                    break;
            }

            if (arg.Args[0] != "command") ServerMgr.Instance.StartCoroutine(CreateMainUI(player, false));
        }

        [ConsoleCommand("hc_edit")]
        private void CMDHudEdit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var editUIPositions = GetOrLoadEditPositions(player);
            UIParts uiParts = editUIPositions.UIPositions.UIPartsDict[editUIPositions.ActiveUI];

            if (arg.Args == null || arg.Args.Count() == 0)
            {
                foreach (var panel in editUIPositions.UIPositions.UIPartsDict.Where(x => x.Value.PanelSettings.Parent == "Overlay" || x.Value.PanelSettings.Parent == "Hud")) CuiHelper.DestroyUi(player, panel.Key);
                CuiHelper.DestroyUi(player, "HCEditBG");
                _editPeople.Remove(player.userID);
                return;
            }

            switch (arg.Args[0])
            {
                case "selectparent":
                    CuiHelper.DestroyUi(player, editUIPositions.ActiveUI);

                    uiParts.PanelSettings.Parent = string.Join(" ", arg.Args.Skip(1));
                    if (uiParts.PanelSettings.Parent == "Overlay") uiParts.Section = "Main";
                    else if (uiParts.PanelSettings.Parent == "Hud") uiParts.Section = "Main";
                    else uiParts.Section = editUIPositions.UIPositions.UIPartsDict[uiParts.PanelSettings.Parent].Section;

                    UICreateEditPanelProperties(player, editUIPositions);
                    break;
                case "editpanel":
                    switch (arg.Args[1])
                    {
                        case "up":
                            editUIPositions.EditPanelPosition.YMax = .93;
                            editUIPositions.EditPanelPosition.YMin = .25;
                            break;
                        case "down":
                            editUIPositions.EditPanelPosition.YMax = .7;
                            editUIPositions.EditPanelPosition.YMin = .02;
                            break;
                        case "left":
                            editUIPositions.EditPanelPosition.XMax = .3;
                            editUIPositions.EditPanelPosition.XMin = .01;
                            break;
                        case "right":
                            editUIPositions.EditPanelPosition.XMax = .99;
                            editUIPositions.EditPanelPosition.XMin = .7;
                            break;
                    }
                    UICreateEditMenu(player, editUIPositions);
                    break;
                case "editpage":
                    editUIPositions.EditPage = int.Parse(arg.Args[1]);
                    break;
                case "addpanel":
                    String b = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    String random = "";

                    for (int i = 0; i < 6; i++)
                    {
                        int a = rnd.Next(26);
                        random += b.ElementAt(a);
                    }

                    editUIPositions.ActiveUI = random;
                    editUIPositions.UIPositions.UIPartsDict.Add(random, new UIParts { Anchors = { XMax = .1, XMin = 0, YMin = 0, YMax = .1 }, Section = "Main", PanelSettings = { Parent = "Hud"} });
                    UICreateEditMenu(player, editUIPositions);
                    break;
                case "deletepanel":
                    if (uiParts.PanelSettings.Parent == "Overlay") CuiHelper.DestroyUi(player, editUIPositions.ActiveUI);
                    if (uiParts.PanelSettings.Parent == "Hud") CuiHelper.DestroyUi(player, editUIPositions.ActiveUI);

                    editUIPositions.UIPositions.UIPartsDict.Remove(editUIPositions.ActiveUI);
                    editUIPositions.ActiveUI = editUIPositions.UIPositions.UIPartsDict.First().Key;
                    UICreateEditMenu(player, editUIPositions);
                    break;
                case "save":
                    _config.UIPositions.UIPartsDict = editUIPositions.UIPositions.Clone().UIPartsDict;
                    SaveConfig();

                    if (_editPeople.ContainsKey(player.userID)) _editPeople.Remove(player.userID);

                    CuiHelper.DestroyUi(player, "HCEditBG");
                    break;
                case "panelinfo":
                    break;
                case "close":
                    foreach (var panel in editUIPositions.UIPositions.UIPartsDict.Where(x => x.Value.PanelSettings.Parent == "Overlay" || x.Value.PanelSettings.Parent == "Hud")) CuiHelper.DestroyUi(player, panel.Key);
                    CuiHelper.DestroyUi(player, "HCEditBG");
                    _editPeople.Remove(player.userID);
                    break;
                case "panelcolor":
                    uiParts.PanelSettings.Color = string.Join(" ", arg.Args.Skip(1));
                    UICreateEditPanel(player, editUIPositions);
                    break;
                case "panelblur":
                    if (uiParts.PanelSettings.Blur) uiParts.PanelSettings.Blur = false;
                    else uiParts.PanelSettings.Blur = true;
                    UICreateEditPanel(player, editUIPositions);
                    break;
                case "image":
                    if (arg.Args.Length >= 2 && arg.Args[1] == "img")
                    {
                        uiParts.Image = arg.Args.Length < 3 ? null : String.Join(" ", arg.Args.Skip(2));
                        if (uiParts.Image != null) RegisterNewImage($"UI{editUIPositions.ActiveUI}", uiParts.Image);
                    }

                    UICreateEditImage(player, editUIPositions);
                    break;
                case "text":
                    if (arg.Args.Length >= 2)
                    {
                        switch (arg.Args[1])
                        {
                            case "text":
                                if (arg.Args.Length >= 2) uiParts.Text.Text = arg.Args.Length < 2 ? null : String.Join(" ", arg.Args.Skip(2));
                                break;
                            case "size":
                                uiParts.Text.Size = arg.Args.Length < 3 ? 15 : int.Parse(arg.Args[2]);
                                break;
                            case "color":
                                uiParts.Text.Color = arg.Args.Length < 3 ? null : String.Join(" ", arg.Args.Skip(2));
                                break;
                            case "font":
                                uiParts.Text.Font = String.Join(" ", arg.Args.Skip(2));
                                break;
                            case "anchor":
                                switch (arg.Args[2])
                                {
                                    case "topleft":
                                        uiParts.Text.Alignment = TextAnchor.UpperLeft;
                                        break;
                                    case "topcenter":
                                        uiParts.Text.Alignment = TextAnchor.UpperCenter;
                                        break;
                                    case "topright":
                                        uiParts.Text.Alignment = TextAnchor.UpperRight;
                                        break;
                                    case "middleleft":
                                        uiParts.Text.Alignment = TextAnchor.MiddleLeft;
                                        break;
                                    case "middlecenter":
                                        uiParts.Text.Alignment = TextAnchor.MiddleCenter;
                                        break;
                                    case "middleright":
                                        uiParts.Text.Alignment = TextAnchor.MiddleRight;
                                        break;
                                    case "bottomleft":
                                        uiParts.Text.Alignment = TextAnchor.LowerLeft;
                                        break;
                                    case "bottomcenter":
                                        uiParts.Text.Alignment = TextAnchor.LowerCenter;
                                        break;
                                    case "bottomright":
                                        uiParts.Text.Alignment = TextAnchor.LowerRight;
                                        break;
                                }
                                break;
                        }

                        UICreateEditText(player, editUIPositions);
                    }
                    break;
                case "points":
                    if (arg.Args[1] == "offset")
                    {
                        switch (arg.Args[2])
                        {
                            case "ymax":
                                if (double.TryParse(arg.Args[3], out double yMax)) uiParts.Offsets.YMax = yMax;
                                break;
                            case "ymin":
                                if (double.TryParse(arg.Args[3], out double yMin)) uiParts.Offsets.YMin = yMin;
                                break;
                            case "xmin":
                                if (double.TryParse(arg.Args[3], out double xMin)) uiParts.Offsets.XMin = xMin;
                                break;
                            case "xmax":
                                if (double.TryParse(arg.Args[3], out double xMax)) uiParts.Offsets.XMax = xMax;
                                break;
                        }
                    }
                    else
                    {
                        switch (arg.Args[2])
                        {
                            case "ymax":
                                if (double.TryParse(arg.Args[3], out double yMax)) uiParts.Anchors.YMax = yMax;
                                break;
                            case "ymin":
                                if (double.TryParse(arg.Args[3], out double yMin)) uiParts.Anchors.YMin = yMin;
                                break;
                            case "xmin":
                                if (double.TryParse(arg.Args[3], out double xMin)) uiParts.Anchors.XMin = xMin;
                                break;
                            case "xmax":
                                if (double.TryParse(arg.Args[3], out double xMax)) uiParts.Anchors.XMax = xMax;
                                break;
                        }
                    }

                    UICreateEditPanel(player, editUIPositions);
                    break;
                case "space":
                    if (double.TryParse(arg.Args[1], out double space)) uiParts.ListSettings.ButtonSpacing = space;
                    break;
                case "spacing":
                    switch (arg.Args[1])
                    {
                        case "up":
                            uiParts.ListSettings.ButtonSpacing += .005;
                            break;
                        case "down":
                            uiParts.ListSettings.ButtonSpacing -= .005;
                            break;
                    }
                    UICreateEditImage(player, editUIPositions);
                    break;
                case "moveall":
                     switch (arg.Args[1])
                     {
                         case "up":
                            uiParts.Anchors.YMax += .01;
                            uiParts.Anchors.YMin += .01;
                            break;
                         case "down":
                            uiParts.Anchors.YMax -= .01;
                            uiParts.Anchors.YMin -= .01; 
                            break;
                         case "left":
                            uiParts.Anchors.XMax -= .01;
                            uiParts.Anchors.XMin -= .01; 
                            break;
                         case "right":
                            uiParts.Anchors.XMax += .01;
                            uiParts.Anchors.XMin += .01;
                            break;
                     }
                    UICreateEditPanel(player, editUIPositions);
                    break;
                case "layout":
                    if (uiParts.ListSettings.Vertical) uiParts.ListSettings.Vertical = false;
                    else uiParts.ListSettings.Vertical = true;

                    if (uiParts.ListSettings.Vertical)
                    {
                        uiParts.Anchors.XMin = 0;
                        uiParts.Anchors.XMax = 1;
                        uiParts.Anchors.YMin = .9;
                        uiParts.Anchors.YMax = 1;
                    }
                    else
                    {
                        uiParts.Anchors.XMin = .01;
                        uiParts.Anchors.XMax = .135;
                        uiParts.Anchors.YMin = .05;
                        uiParts.Anchors.YMax = .92;
                    }
                    UICreateEditImage(player, editUIPositions);
                    break;
                case "select":
                    editUIPositions.ActiveUI = string.Join(" ", arg.Args.Skip(1));
                    UICreateEditMenu(player, editUIPositions);
                    break;
                case "useoffsets":
                    if (!uiParts.Offsets.UseOffsets) uiParts.Offsets.UseOffsets = true;
                    else uiParts.Offsets.UseOffsets = false;
                    UICreateEditPanel(player, editUIPositions);
                    break;
                case "selectpanel":
                    editUIPositions.ActiveUI = string.Join(" ", arg.Args.Skip(1));

                    var uiNames = _config.UIPositions.GetAllUIPartNames();
                    for (int i = 0; i < _config.UIPositions.UIPartsDict.Keys.Count; i++)
                    {
                        if (uiNames[i] == editUIPositions.ActiveUI) editUIPositions.EditPage = i / 10;
                    }
                    break;
                case "panelname":
                    /* var partsList = editUIPositions.UIPositions.UIPartsDict.ToList();

                    var theEntry = partsList.FindIndex(x => x.Key == editUIPositions.ActiveUI);
                    if (theEntry == -1) return;

                    partsList[theEntry] = new KeyValuePair<string, UIParts> { Key = String.Join(" ", arg.Args.Skip(1)), Value = uiParts.Clone() }; */
                    break;
                case "smallclose":
                    CuiHelper.DestroyUi(player, "WCSmallEditPanel");
                    break;
                case "smallimgclose":
                    CuiHelper.DestroyUi(player, "WCSmallEditPanel");
                    break;
                default:
                    foreach (var panel in editUIPositions.UIPositions.UIPartsDict.Where(x => x.Value.PanelSettings.Parent == "Overlay" || x.Value.PanelSettings.Parent == "Hud")) CuiHelper.DestroyUi(player, panel.Key);
                    CuiHelper.DestroyUi(player, "HCEditBG");
                    _editPeople.Remove(player.userID);
                    break;
            }

                if (arg.Args[0] == "image" && arg.Args.Length > 2) timer.Once(.5f, () => ServerMgr.Instance.StartCoroutine(CreateMainUI(player, true)));
                else ServerMgr.Instance.StartCoroutine(CreateMainUI(player, true));
        }

        private void UIOpenEditMenu(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "hudcontroller.admin"))
            {
                SendReply(player, "You do not have the permission 'hudcontroller.admin' to use this command!");
                return;
            }

            var editPos = GetOrLoadEditPositions(player, true);
            UICreateEditMenu(player, editPos);
        }
        #endregion

        #region UI
        IEnumerator CreateMainUI(BasePlayer player, bool isEdit = false)
        {
            var container = new CuiElementContainer();

            List<string> activeOverlayUI = new List<string>();
            Dictionary<string, UIParts> UIList = new Dictionary<string, UIParts>();
            if (isEdit || _editPeople.ContainsKey(player.userID)) UIList = GetOrLoadEditPositions(player).UIPositions.UIPartsDict;
            else UIList = _config.UIPositions.UIPartsDict;
            var playerOptions = GetOrLoadPlayerOptions(player);

            foreach (var UIPart in UIList)
            {
                PanelSettings panelSettings = UIPart.Value.PanelSettings.Clone();
                if (UIPart.Value.PanelSettings.Parent.Equals("Overlay", StringComparison.OrdinalIgnoreCase)) activeOverlayUI.Add(UIPart.Key);
                if (UIPart.Value.PanelSettings.Parent.Equals("Hud", StringComparison.OrdinalIgnoreCase)) activeOverlayUI.Add(UIPart.Key);

                if (!UIPart.Value.ActuallyLoad || !UIPart.Value.DisplayWithMoreThanOneSection && playerOptions.neededPanels.Count > 1 || (!UIPart.Value.AlwaysDisplay && !playerOptions.neededPanels.Contains(UIPart.Value.Section))) continue;
                if (playerOptions.lastOpened.Contains(UIPart.Value.Section)) panelSettings.FadeIn = 0f;

                var UIParts = UIPart.Value.Clone();
                if (!string.IsNullOrEmpty(UIParts.Text.Text)) UIParts.Text.Text = GetReplacedText(player, UIParts.Text.Text);

                if (UIPart.Key == "CommandsDropdown")
                {
                    var currentDepth = (-1 * UIParts.Anchors.YMin) - (-1 * UIParts.Anchors.YMax);
                    var panelDepth = -1 * (currentDepth * _config.Commands.Count);
                    var clonedAnchors = UIParts.Anchors.Clone();
                    clonedAnchors.YMin = panelDepth + clonedAnchors.YMax;

                    CreateSuperPanel(ref container, clonedAnchors, UIParts.Offsets, UIParts.Text, panelSettings, UIParts.Image == null ? null : GetImage(UIPart.Key), UIPart.Key, false, UIPart.Value.CommandSettings);
                } else if(UIPart.Key != "PanelIconLayout" && UIPart.Key != "CommandsPanelLayout") CreateSuperPanel(ref container, UIParts.Anchors, UIParts.Offsets, UIParts.Text, panelSettings, UIParts.Image == null ? null : GetImage(UIPart.Key), UIPart.Key, false, UIPart.Value.CommandSettings);
            
                if (UIPart.Key == "PanelIconBar")
                {
                    int i = 0;
                    foreach (var theEvent in _events)
                    {
                        var panelParts = UIList["PanelIconLayout"].Clone();

                        var buttonSpace = i * panelParts.ListSettings.ButtonSpacing;
                        var tempAnchors = new UIAnchors()
                        {
                            XMin = !panelParts.ListSettings.Vertical ? panelParts.Anchors.XMin + buttonSpace : panelParts.Anchors.XMin,
                            XMax = !panelParts.ListSettings.Vertical ? panelParts.Anchors.XMax + buttonSpace : panelParts.Anchors.XMax,
                            YMin = panelParts.ListSettings.Vertical ? panelParts.Anchors.YMin - buttonSpace : panelParts.Anchors.YMin,
                            YMax = panelParts.ListSettings.Vertical ? panelParts.Anchors.YMax - buttonSpace : panelParts.Anchors.YMax
                        };

                        CreateSuperPanel(ref container, tempAnchors, panelParts.Offsets, panelParts.Text, panelParts.PanelSettings, GetImage($"{(theEvent.Value ? "Active" : "Inactive")}{theEvent.Key}"), null, false, panelParts.CommandSettings);
                        i++;
                    }
                }
                else if (UIPart.Key == "CommandsDropdown")
                {
                    int i = 0;
                    foreach (var command in _config.Commands)
                    {
                        var panelParts = UIList["CommandsPanelLayout"].Clone();
                        var panelDepth = (panelParts.Anchors.YMax - panelParts.Anchors.YMin) / _config.Commands.Count;
                        var topPosition = panelParts.Anchors.YMax - (i * panelDepth);

                        var buttonSpace = i * panelParts.ListSettings.ButtonSpacing;
                        var tempAnchors = new UIAnchors()
                        {
                            XMin = !panelParts.ListSettings.Vertical ? panelParts.Anchors.XMin + buttonSpace : panelParts.Anchors.XMin,
                            XMax = !panelParts.ListSettings.Vertical ? panelParts.Anchors.XMax + buttonSpace : panelParts.Anchors.XMax,
                            YMin = panelParts.ListSettings.Vertical ? topPosition - panelDepth - buttonSpace : panelParts.Anchors.YMin,
                            YMax = panelParts.ListSettings.Vertical ? topPosition - buttonSpace : panelParts.Anchors.YMax
                        };

                        var tempText = panelParts.Text.Clone();
                        tempText.Text = command.CommandName;
                        var tempCommand = panelParts.CommandSettings.Clone();
                        tempCommand.Command = tempCommand.Command.Replace("{0}", command.Command);

                        CreateSuperPanel(ref container, tempAnchors, panelParts.Offsets, tempText, panelParts.PanelSettings, UIParts.Image == null ? null : GetImage(UIPart.Key), null, false, tempCommand);
                        i++;
                    }
                }
            }

            foreach (var ui in activeOverlayUI) CuiHelper.DestroyUi(player, ui);
            CuiHelper.AddUi(player, container);

            yield return null;
        }
        #endregion

        #region Edit UI
        private void UICreateEditMenu(BasePlayer player, EditUIPositions editPos)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "Overlay", "HCEditBG", true, true);
            CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", panel);

            var panelAnchors = editPos.EditPanelPosition;
            CreatePanel(ref container, $"{panelAnchors.XMin} {panelAnchors.YMin}", $"{panelAnchors.XMax} {panelAnchors.YMax}", ".17 .17 .17 1", panel, "HCEditMain");
            var addPanelPanel = CreatePanel(ref container, "0 1.003", ".997 1.07", ".17 .17 .17 1", "HCEditMain");

            CreateButton(ref container, $".02 .14", ".08 .82", "1 0.25 0.14 .4", "1 1 1 1", "X", 14, "hc_edit close", addPanelPanel);
            CreateButton(ref container, $".09 .14", ".29 .82", "0.27 1 0.25 .4", "1 1 1 1", "SAVE EDITS", 10, "hc_edit save", addPanelPanel);
            CreateButton(ref container, $".3 .14", ".5 .82", "0.21 0.63 1 .4", "1 1 1 1", "ADD NEW PANEL", 10, "hc_edit addpanel", addPanelPanel);
            CreateButton(ref container, ".51 .14", ".7 .82", "1 0.18 0.18 .4", "1 1 1 1", "DELETE PANEL", 10, "hc_edit deletepanel", addPanelPanel);

            CreateImageButton(ref container, ".71 .14", ".77 .82", "1 1 1 .1", $"hc_edit editpanel left", GetImage("LeftArrow"), addPanelPanel);
            CreateImageButton(ref container, ".78 .14", ".84 .82", "1 1 1 .1", $"hc_edit editpanel right", GetImage("RightArrow"), addPanelPanel);
            CreateImageButton(ref container, ".85 .14", ".91 .82", "1 1 1 .1", $"hc_edit editpanel up", GetImage("UpArrow"), addPanelPanel);
            CreateImageButton(ref container, ".92 .14", ".98 .82", "1 1 1 .1", $"hc_edit editpanel down", GetImage("DownArrow"), addPanelPanel);
            CuiHelper.DestroyUi(player, "HCEditBG");
            CuiHelper.AddUi(player, container);

            UICreateEditPanelProperties(player, editPos);
            ServerMgr.Instance.StartCoroutine(CreateMainUI(player, true));
        }

        void UICreateEditPanelProperties(BasePlayer player, EditUIPositions editPos)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "HCEditMain", "HCEditMainOverlay");

            CreatePanel(ref container, ".02 .7", ".975 .98", "1 1 1 .1", panel, "HCEditPanel");
            CreatePanel(ref container, ".02 .45", ".975 .69", "1 1 1 .1", panel, "HCEditText");
            CreatePanel(ref container, ".02 .3", ".975 .44", "1 1 1 .1", panel, "HCEditImage");
            CreatePanel(ref container, ".02 .018", ".975 .29", "1 1 1 .1", panel, "HCEditPanelSelect");

            CuiHelper.DestroyUi(player, "HCEditMainOverlay");
            CuiHelper.AddUi(player, container);

            UICreateEditPanel(player, editPos);
            UICreateEditText(player, editPos);
            UICreateEditImage(player, editPos);
            UICreateEditPanelSelect(player, editPos);
        }

        void UICreateEditPanelSelect(BasePlayer player, EditUIPositions editPos)
        {
            var container = new CuiElementContainer();

            List<string> uiPartNames = editPos.UIPositions.Clone().GetAllUIPartNames();
            int panelCount = editPos.UIPositions.UIPartsDict.Count;

            double buttonSize = -.326;
            CreateLabel(ref container, ".01 .87", ".495 .98", "1 1 1 .1", "1 1 1 1", "SELECTED UI", 10, TextAnchor.MiddleCenter, "HCEditPanelSelect");
            AddScrollView(ref container, "0 0", ".495 .85", $"{1 + (panelCount < 6 ? -1 : Math.Ceiling((double)panelCount / 2) * buttonSize)}", "1 1 1 0", "HCEditPanelSelect", "HCEditPanelSelectOverlay");

            CreateLabel(ref container, ".505 .87", ".99 .98", "1 1 1 .1", "1 1 1 1", "PARENT UI", 10, TextAnchor.MiddleCenter, "HCEditPanelSelect");
            AddScrollView(ref container, ".505 0", ".99 .85", $"{1 + (panelCount < 6 ? -1 : Math.Ceiling((double)panelCount / 2) * buttonSize)}", "1 1 1 0", "HCEditPanelSelect", "HCScrollPickParent");

            CuiHelper.DestroyUi(player, "HCEditPanelSelectOverlay");
            CuiHelper.AddUi(player, container);

            int panelRow = -1;

            for (int i = 0; i < panelCount; i++)
            {
                if (i % 2 == 0) panelRow++;
                ServerMgr.Instance.StartCoroutine(UIAddPanel(player, "HCEditPanelSelectOverlay", panelCount, i, panelRow, editPos, uiPartNames));
            }

            int ii = 0;
            panelRow = -1;
            List<string> panels = new List<string>{ "Overlay", "Hud" };
            panels.AddRange(editPos.UIPositions.Clone().GetAllUIPartNames());

            foreach (var panelInfo in panels)
            {
                if (ii % 2 == 0) panelRow++;
                ServerMgr.Instance.StartCoroutine(UIAddParent(player, "HCScrollPickParent", panelCount, ii, panelRow, editPos, panelInfo));
                ii++;
            }
        }

        IEnumerator UIAddPanel(BasePlayer player, string panel, double panelCount, int i, double row, EditUIPositions editPos, List<string> uiPartNames)
        {
            var container = new CuiElementContainer();

            double buttonSize = -.326;

            var panelDepth = 0 - (buttonSize * Math.Ceiling((double)panelCount / 2));
            var space = panelCount < 6 ? .02 : .02 / panelDepth;
            var rowDepth = panelCount < 6 ? (-1 * buttonSize) / 1 : (-1 * buttonSize) / panelDepth;

            var topHeight = 1 + (rowDepth - ((row + 1) * rowDepth));

            var spc = .49;
            CreateButton(ref container, $"{.005 + (i % 2 * spc)} {topHeight - rowDepth + space}", $"{.48 + (i % 2 * spc)} {topHeight}", editPos.ActiveUI.Equals(uiPartNames[i]) ? "0.08 0.77 1 .2" : "1 1 1 .1", "1 1 1 1", uiPartNames[i], 11, $"hc_edit select {uiPartNames[i]}", panel);

            CuiHelper.AddUi(player, container);

            yield return null;
        }

        void UICreateEditImage(BasePlayer player, EditUIPositions editPos)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "HCEditImage", "HCEditImageOverlay");
            var uiPart = editPos.UIPositions.UIPartsDict[editPos.ActiveUI];

            double endNumber = uiPart.ListSettings.IsList ? .498 : .988;
            CreateLabel(ref container, ".007 .75", $"{endNumber} .94", "1 1 1 .1", "1 1 1 1", "IMAGE URL", 10, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".007 .04", $"{endNumber} .71", "hc_edit image img", "1 1 1 .1", "1 1 1 1", uiPart.Image, 12, TextAnchor.MiddleCenter, panel);

            if (uiPart.ListSettings.IsList)
            {
                CreateLabel(ref container, ".506 .75", $".988 .94", "1 1 1 .1", "1 1 1 1", "LIST SETTINGS", 10, TextAnchor.MiddleCenter, panel);
                CreateButton(ref container, ".506 .04", $".746 .69", uiPart.ListSettings.Vertical ? "0.35 0.53 1 .3" : "0.18 0.77 1 .3", "1 1 1 1", uiPart.ListSettings.Vertical ? "Currently\nVertical" : "Currently\nHorizontal", 12, $"hc_edit layout {editPos.ActiveUI}", panel);

                CreateButton(ref container, ".754 .04", ".8 .69", "1 1 1 .1", "1 1 1 1", "<", 12, "hc_edit spacing down", panel);
                CreateButton(ref container, ".934 .04", ".983 .69", "1 1 1 .1", "1 1 1 1", ">", 12, "hc_edit spacing up", panel);
                CreateInput(ref container, ".809 .04", ".927 .71", $"hc_edit space", "1 1 1 .1", "1 1 1 1", $"{uiPart.ListSettings.ButtonSpacing}", 12, TextAnchor.MiddleCenter, panel);
            }

            CuiHelper.DestroyUi(player, "HCEditImageOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UICreateEditText(BasePlayer player, EditUIPositions editPos)
        {
            var container = new CuiElementContainer();
            var uiPart = editPos.UIPositions.UIPartsDict[editPos.ActiveUI];
            var c1 = "1 1 1 .1";
            var c2 = ".01 .6 .99 .5";

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "HCEditText", "HCEditTextOverlay");

            CreateLabel(ref container, ".007 .87", ".988 .97", "1 1 1 .1", "1 1 1 1", "TEXT", 10, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".007 .65", ".988 .85", "hc_edit text text", "1 1 1 .1", "1 1 1 1", uiPart.Text.Text, 12, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".006 .53", ".248 .63", "1 1 1 .1", "1 1 1 1", "FONT", 10, TextAnchor.MiddleCenter, panel);
            CreateLabel(ref container, ".256 .53", ".498 .63", "1 1 1 .1", "1 1 1 1", "SIZE", 10, TextAnchor.MiddleCenter, panel);
            CreateLabel(ref container, ".506 .53", ".748 .63", "1 1 1 .1", "1 1 1 1", "COLOR", 10, TextAnchor.MiddleCenter, panel);
            CreateLabel(ref container, ".756 .53", ".988 .63", "1 1 1 .1", "1 1 1 1", "ANCHOR", 10, TextAnchor.MiddleCenter, panel);

            CreateButton(ref container, ".756 .37", ".824 .5", uiPart.Text.Alignment == TextAnchor.UpperLeft ? c2 : c1, "1 1 1 1", "o", 10, "hc_edit text anchor topleft", panel);
            CreateButton(ref container, ".834 .37", ".9025 .5", uiPart.Text.Alignment == TextAnchor.UpperCenter ? c2 : c1, "1 1 1 1", "o", 10, "hc_edit text anchor topcenter", panel);
            CreateButton(ref container, ".9125 .37", ".985 .5", uiPart.Text.Alignment == TextAnchor.UpperRight ? c2 : c1, "1 1 1 1", "o", 10, "hc_edit text anchor topright", panel);

            CreateButton(ref container, ".756 .19", ".824 .345", uiPart.Text.Alignment == TextAnchor.MiddleLeft ? c2 : c1, "1 1 1 1", "o", 10, "hc_edit text anchor middleleft", panel);
            CreateButton(ref container, ".834 .19", ".9025 .345", uiPart.Text.Alignment == TextAnchor.MiddleCenter ? c2 : c1, "1 1 1 1", "o", 10, "hc_edit text anchor middlecenter", panel);
            CreateButton(ref container, ".9125 .19", ".985 .345", uiPart.Text.Alignment == TextAnchor.MiddleRight ? c2 : c1, "1 1 1 1", "o", 10, "hc_edit text anchor middleright", panel);

            CreateButton(ref container, ".756 .02", ".824 .162", uiPart.Text.Alignment == TextAnchor.LowerLeft ? c2 : c1, "1 1 1 1", "o", 10, "hc_edit text anchor bottomleft", panel);
            CreateButton(ref container, ".834 .02", ".9025 .162", uiPart.Text.Alignment == TextAnchor.LowerCenter ? c2 : c1, "1 1 1 1", "o", 10, "hc_edit text anchor bottomcenter", panel);
            CreateButton(ref container, ".9125 .02", ".985 .162", uiPart.Text.Alignment == TextAnchor.LowerRight ? c2 : c1, "1 1 1 1", "o", 10, "hc_edit text anchor bottomright", panel);

            CreateInput(ref container, ".506 .02", ".748 .5", "hc_edit text color", "1 1 1 .1", "1 1 1 1", uiPart.Text.Color, 10, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".256 .02", ".498 .5", "hc_edit text size", "1 1 1 .1", "1 1 1 1", $"{uiPart.Text.Size}", 10, TextAnchor.MiddleCenter, panel);

            CreateButton(ref container, ".006 .395", ".244 .5", uiPart.Text.Font == "droidsansmono.ttf" ? c2 : c1, "1 1 1 1", "FONT 1", 10, "hc_edit text font droidsansmono.ttf", panel);
            CreateButton(ref container, ".006 .27", ".244 .37", uiPart.Text.Font == "permanentmarker.ttf" ? c2 : c1, "1 1 1 1", "FONT 2", 10, "hc_edit text font permanentmarker.ttf", panel);
            CreateButton(ref container, ".006 .15", ".244 .25", uiPart.Text.Font == "robotocondensed-bold.ttf" ? c2 : c1, "1 1 1 1", "FONT 3", 10, "hc_edit text font robotocondensed-bold.ttf", panel);
            CreateButton(ref container, ".006 .02", ".244 .13", uiPart.Text.Font == "robotocondensed-regular.ttf" ? c2 : c1, "1 1 1 1", "FONT 4", 10, "hc_edit text font robotocondensed-regular.ttf", panel);

            CuiHelper.DestroyUi(player, "HCEditTextOverlay");
            CuiHelper.AddUi(player, container);
        }

        private void UICreateEditPanel(BasePlayer player, EditUIPositions editPos)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "HCEditPanel", "HCEditPanelOverlay");

            var uiPart = editPos.UIPositions.UIPartsDict[editPos.ActiveUI];
            var uiAnchors = uiPart.Anchors;
            var uiOffsets = uiPart.Offsets;

            var enableDisablePanel = CreatePanel(ref container, ".75 .82", ".995 .98", "1 1 1 .1", panel);

            CreateLabel(ref container, ".25 .87", ".495 .98", "1 1 1 .1", "1 1 1 1", "PANEL NAME", 10, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".25 .64", ".495 .85", "hc_edit panelname", "1 1 1 .1", "1 1 1 1", editPos.ActiveUI, 10, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".505 .87", ".74 .98", "1 1 1 .1", "1 1 1 1", "PANEL COLOR", 10, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".505 .64", ".74 .85", "hc_edit panelcolor", "1 1 1 .1", "1 1 1 1", uiPart.PanelSettings.Color, 10, TextAnchor.MiddleCenter, panel);

            CreateButton(ref container, ".04 .12", ".95 .85", uiPart.PanelSettings.Blur ? "0.32 1 0.18 .5" : "1 0.18 0.18 .5", "1 1 1 1", "BLUR", 15, "hc_edit panelblur", enableDisablePanel);

            var directionPanel = CreatePanel(ref container, ".75 .64", ".995 .8", "1 1 1 .1", panel);
            CreateImageButton(ref container, ".03 .1", ".225 .85", "1 1 1 .15", $"hc_edit moveall up", GetImage("UpArrow"), directionPanel);
            CreateImageButton(ref container, ".255 .1", ".475 .85", "1 1 1 .15", $"hc_edit moveall down", GetImage("DownArrow"), directionPanel);
            CreateImageButton(ref container, ".51 .1", ".725 .85", "1 1 1 .15", $"hc_edit moveall left", GetImage("LeftArrow"), directionPanel);
            CreateImageButton(ref container, ".755 .1", ".96 .85", "1 1 1 .15", $"hc_edit moveall right", GetImage("RightArrow"), directionPanel);

            // Anchors
            CreateLabel(ref container, ".005 .49", ".495 .62", "1 1 1 .1", "1 1 1 1", "ANCHORS", 10, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".005 .35", ".1175 .47", "1 1 1 .1", "1 1 1 1", "TOP", 9, TextAnchor.MiddleCenter, panel);
            CreateImageButton(ref container, ".005 .02", ".05625 .16", "1 1 1 .1", $"hc_edit points anchors ymax {uiAnchors.YMax + .01}", GetImage("UpArrow"), panel);
            CreateImageButton(ref container, ".06625 .02", ".1175 .16", "1 1 1 .1", $"hc_edit points anchors ymax {uiAnchors.YMax - .01}", GetImage("DownArrow"), panel);
            CreateInput(ref container, ".005 .185", ".1175 .33", "hc_edit points anchors ymax", "1 1 1 .1", "1 1 1 1", $"{uiAnchors.YMax}", 11, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".1275 .35", ".24 .47", "1 1 1 .1", "1 1 1 1", "BTM", 9, TextAnchor.MiddleCenter, panel);
            CreateImageButton(ref container, ".1275 .02", ".17875 .16", "1 1 1 .1", $"hc_edit points anchors ymin {uiAnchors.YMin + .01}", GetImage("UpArrow"), panel);
            CreateImageButton(ref container, ".18875 .02", ".24 .16", "1 1 1 .1", $"hc_edit points anchors ymin {uiAnchors.YMin - .01}", GetImage("DownArrow"), panel);
            CreateInput(ref container, ".1275 .185", ".24 .33", "hc_edit points anchors ymin", "1 1 1 .1", "1 1 1 1", $"{uiAnchors.YMin}", 11, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".25 .35", ".3625 .47", "1 1 1 .1", "1 1 1 1", "LEFT", 9, TextAnchor.MiddleCenter, panel);
            CreateImageButton(ref container, ".25 .02", ".30125 .16", "1 1 1 .1", $"hc_edit points anchors xmin {uiAnchors.XMin - .01}", GetImage("LeftArrow"), panel);
            CreateImageButton(ref container, ".31125 .02", ".3625 .16", "1 1 1 .1", $"hc_edit points anchors xmin {uiAnchors.XMin + .01}", GetImage("RightArrow"), panel);
            CreateInput(ref container, ".25 .185", ".3625 .33", "hc_edit points anchors xmin", "1 1 1 .1", "1 1 1 1", $"{uiAnchors.XMin}", 11, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".3725 .35", ".495 .47", "1 1 1 .1", "1 1 1 1", "RIGHT", 9, TextAnchor.MiddleCenter, panel);
            CreateImageButton(ref container, ".3725 .02", ".42375 .16", "1 1 1 .1", $"hc_edit points anchors xmax {uiAnchors.XMax - .01}", GetImage("LeftArrow"), panel);
            CreateImageButton(ref container, ".43375 .02", ".495 .16", "1 1 1 .1", $"hc_edit points anchors xmax {uiAnchors.XMax + .01}", GetImage("RightArrow"), panel);
            CreateInput(ref container, ".3725 .185", ".495 .33", "hc_edit points anchors xmax", "1 1 1 .1", "1 1 1 1", $"{uiAnchors.XMax}", 11, TextAnchor.MiddleCenter, panel);

            // Offsets
            CreateLabel(ref container, ".505 .49", ".808 .62", "1 1 1 .15", "1 1 1 1", "OFFSETS", 10, TextAnchor.MiddleCenter, panel);
            CreateButton(ref container, ".817 .49", ".9915 .6125", uiPart.Offsets.UseOffsets ? "0.32 1 0.18 .5" : "1 0.18 0.18 .5", "1 1 1 1", "USE OFFSETS", 11, "hc_edit useoffsets", panel);

            CreateLabel(ref container, ".505 .35", ".6175 .47", "1 1 1 .15", "1 1 1 1", "TOP", 9, TextAnchor.MiddleCenter, panel);
            CreateImageButton(ref container, ".505 .02", ".55625 .16", "1 1 1 .15", $"hc_edit points offset ymax {uiOffsets.YMax + 1}", GetImage("UpArrow"), panel);
            CreateImageButton(ref container, ".56625 .02", ".6175 .16", "1 1 1 .15", $"hc_edit points offset ymax {uiOffsets.YMax - 1}", GetImage("DownArrow"), panel);
            CreateInput(ref container, ".505 .185", ".6175 .33", "hc_edit points offset ymax", "1 1 1 .15", "1 1 1 1", $"{uiOffsets.YMax}", 11, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".6275 .35", ".74 .47", "1 1 1 .15", "1 1 1 1", "BTM", 9, TextAnchor.MiddleCenter, panel);
            CreateImageButton(ref container, ".6275 .02", ".67875 .16", "1 1 1 .15", $"hc_edit points offset ymin {uiOffsets.YMin + 1}", GetImage("UpArrow"), panel);
            CreateImageButton(ref container, ".68875 .02", ".74 .16", "1 1 1 .15", $"hc_edit points offset ymin {uiOffsets.YMin - 1}", GetImage("DownArrow"), panel);
            CreateInput(ref container, ".6275 .185", ".74 .33", "hc_edit points offset ymin", "1 1 1 .15", "1 1 1 1", $"{uiOffsets.YMin}", 11, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".75 .35", ".8625 .47", "1 1 1 .15", "1 1 1 1", "LEFT", 9, TextAnchor.MiddleCenter, panel);
            CreateImageButton(ref container, ".75 .02", ".80125 .16", "1 1 1 .15", $"hc_edit points offset xmin {uiOffsets.XMin - 1}", GetImage("LeftArrow"), panel);
            CreateImageButton(ref container, ".81125 .02", ".8625 .16", "1 1 1 .15", $"hc_edit points offset xmin {uiOffsets.XMin + 1}", GetImage("RightArrow"), panel);
            CreateInput(ref container, ".75 .185", ".8625 .33", "hc_edit points offset xmin", "1 1 1 .15", "1 1 1 1", $"{uiOffsets.XMin}", 11, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".8725 .35", ".995 .47", "1 1 1 .15", "1 1 1 1", "RIGHT", 9, TextAnchor.MiddleCenter, panel);
            CreateImageButton(ref container, ".8725 .02", ".92375 .16", "1 1 1 .15", $"hc_edit points offset xmax {uiOffsets.XMax - 1}", GetImage("LeftArrow"), panel);
            CreateImageButton(ref container, ".93375 .02", ".995 .16", "1 1 1 .15", $"hc_edit points offset xmax {uiOffsets.XMax + 1}", GetImage("RightArrow"), panel);
            CreateInput(ref container, ".8725 .185", ".995 .33", "hc_edit points offset xmax", "1 1 1 .15", "1 1 1 1", $"{uiOffsets.XMax}", 11, TextAnchor.MiddleCenter, panel);


            CuiHelper.DestroyUi(player, "HCEditPanelOverlay");
            CuiHelper.AddUi(player, container);

        }

        IEnumerator UIAddParent(BasePlayer player, string panel, double panelCount, int i, double row, EditUIPositions editPos, string parentName)
        {
            var container = new CuiElementContainer();

            double buttonSize = -.326;

            var panelDepth = 0 - (buttonSize * Math.Ceiling((double)panelCount / 2));
            var space = panelCount < 6 ? .02 : .02 / panelDepth;
            var rowDepth = panelCount < 6 ? (-1 * buttonSize) / 1 : (-1 * buttonSize) / panelDepth;

            var topHeight = 1 + (rowDepth - ((row + 1) * rowDepth));

            var spc = .49;
            CreateButton(ref container, $"{.005 + (i % 2 * spc)} {topHeight - rowDepth + space}", $"{.48 + (i % 2 * spc)} {topHeight}", editPos.UIPositions.UIPartsDict[editPos.ActiveUI].PanelSettings.Parent.Equals(parentName) ? "0.08 0.77 1 .2" : "1 1 1 .1", "1 1 1 1", parentName, 8, $"hc_edit selectparent {parentName}", panel);

            CuiHelper.AddUi(player, container);

            yield return null;
        }
        #endregion

        #region UI Methods
        private static string CreateSuperPanel(ref CuiElementContainer container, UIAnchors anchors, UIOffsets offsets, UIText text, PanelSettings panelSettings, string panelImage = null, string panelName = null, bool mainPanel = false, CommandSettings commandSettings = null)
        {
            CuiPanel panel = new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = $"{anchors.XMin} {anchors.YMin}",
                    AnchorMax = $"{anchors.XMax} {anchors.YMax}"
                },
                Image = { Color = panelSettings.Color }
            };

            if (offsets.UseOffsets)
            {
                panel.RectTransform.OffsetMin = $"{offsets.XMin} {offsets.YMin}";
                panel.RectTransform.OffsetMax = $"{offsets.XMax} {offsets.YMax}";
            }

            if (panelSettings.Blur) panel.Image.Material = "assets/content/ui/uibackgroundblur.mat";
            if (mainPanel) panel.CursorEnabled = true;
            if(panelSettings.FadeIn > 0f) panel.Image.FadeIn = panelSettings.FadeIn;

            string thePanel = container.Add(panel, panelSettings.Parent, panelName);

            if (panelImage != null)
            {
                container.Add(new CuiElement
                {
                    Parent = thePanel,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                        new CuiRawImageComponent {Png = panelImage},
                    }
                });
            }

            if (text != null && !string.IsNullOrEmpty(text.Text))
            {
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Color = text.Color,
                        Text = text.Text,
                        Align = text.Alignment,
                        FontSize = text.Size,
                        Font = text.Font
                    }
                }, thePanel);
            }

            if (commandSettings != null && commandSettings.IsButton) container.Add(new CuiButton
            {
                Button = { Command = $"{commandSettings.Command}", Color = "0 0 0 0" }
            }, thePanel);

            return thePanel;
        }

        static public void AddScrollView(ref CuiElementContainer container, string anchorMin, string anchorMax, string offSetMin, string color, string parent = "Overlay", string panelName = null, bool horizontal = false)
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parent,
                Components = {
                        new CuiImageComponent {
                            FadeIn = 0.2f,
                            Color = color
                        },
                        new CuiScrollViewComponent {
                            Horizontal = false,
                            Vertical = true,
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                            Elasticity = 0.25f,
                            Inertia = true,
                            DecelerationRate = 0.3f,
                            ContentTransform = new CuiRectTransform()
                            {
                                AnchorMin = $"0 {(horizontal ? "0" : offSetMin)}",
                                AnchorMax = $"{(horizontal ? offSetMin : "1")} 1",
                            },
                            ScrollSensitivity = 10.0f,
                            VerticalScrollbar = new CuiScrollbar {
                                Invert = false,
                                AutoHide = false,
                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                HandleColor = "1 1 1 .2",
                                HighlightColor = "1 1 1 .2",
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",
                                TrackColor = "1 1 1 .05",
                                Size = 3,
                                PressedColor = "1 1 1 .2"
                            },
                            HorizontalScrollbar = new CuiScrollbar {
                                Invert = true,
                                AutoHide = false,
                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                HandleColor = "1 1 1 .2",
                                HighlightColor = "1 1 1 .2",
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",
                                TrackColor = "1 1 1 .05",
                                Size = 3,
                                PressedColor = "1 1 1 .2"
                            }
                        },
                        new CuiRectTransformComponent {AnchorMin = anchorMin, AnchorMax = anchorMax}
                    }
            });
        }

        private static string CreateLabel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, panelColor, parent, panelName);
            container.Add(new CuiLabel
            {
                Text =
                {
                    Color = textColor,
                    Text = labelText == null ? " " : labelText,
                    Align = alignment,
                    FontSize = fontSize,
                    Font = "robotocondensed-bold.ttf"
                }
            }, panel);
            return panel;
        }

        private static string CreatePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string parent = "Overlay", string panelName = null, bool blur = false, bool isMainPanel = false, string offsetMin = null, string offsetMax = null, bool fadeIn = false, float fadeInTime = 1)
        {
            CuiPanel panel = new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                Image = { Color = panelColor }
            };

            if (offsetMax != null) panel.RectTransform.OffsetMax = offsetMax;
            if (offsetMax != null) panel.RectTransform.OffsetMin = offsetMin;
            if (fadeIn) panel.Image.FadeIn = fadeInTime;

            if (blur) panel.Image.Material = "assets/content/ui/uibackgroundblur.mat";
            if (isMainPanel) panel.CursorEnabled = true;
            return container.Add(panel, parent, panelName);
        }

        private static void CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelImage, string parent = "Overlay", string panelName = null, bool isUrl = false)
        {
            var panel = new CuiElement
            {
                Parent = parent,
                Name = panelName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    }
                }
            };

            if (isUrl) panel.Components.Add(new CuiRawImageComponent { Url = panelImage });
            else panel.Components.Add(new CuiRawImageComponent { Png = panelImage });

            container.Add(panel);
        }

        private static void CreateImageButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string buttonCommand, string panelImage, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, buttonColor, parent, panelName);
            CreateImagePanel(ref container, ".2 .2", ".8 .8", panelImage, panel);

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"{buttonCommand}" }
            }, panel);
        }

        private static string CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText, int fontSize, string buttonCommand, string parent = "Overlay", TextAnchor labelAnchor = TextAnchor.MiddleCenter)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, "0 0 0 0", parent);

            container.Add(new CuiButton
            {
                Button = { Color = buttonColor, Command = $"{buttonCommand}" },
                Text = { Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText },
            }, panel);
            return panel;
        }

        private static string CreateInput(ref CuiElementContainer container, string anchorMin, string anchorMax, string command, string backgroundColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string labelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, backgroundColor, parent, labelName);

            container.Add(new CuiElement
            {
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = textColor,
                        Text = labelText == null ? " " : labelText,
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

        #region AddedPluginHooks
        #endregion
    }
}
