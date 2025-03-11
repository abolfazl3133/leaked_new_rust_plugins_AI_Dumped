using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using System.Text.RegularExpressions;
using Network;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using UnityEngine;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("Tipster", "FuJiCuRa", "1.4.9", ResourceId = 45)]
    [Description("Player informations and server notifications")]
    internal class Tipster : RustPlugin
    {
        [PluginReference] private Plugin PopupNotifications;
        private bool Changed = false;
        private bool Initialized = false;
        private ulong pluginIcon = 0uL;
        private JsonSerializerSettings jsonsettings;
        private string seperatorText = string.Join("-", new string[25 + 1]);
        private readonly string PlayerDatabaseFile = "Tipster_PlayerDatabase";
        private readonly string CountryDatabase = "Tipster_CountryDatabase";
        private Dictionary<ulong, PlayerCache> Players = new Dictionary<ulong, PlayerCache>();
        private Dictionary<string, object> CountryNames = new Dictionary<string, object>();
        private List<string> DbLanguages = new List<string>();
        private Dictionary<string, object> storedCountryData = new Dictionary<string, object>();
        private System.Random AdvertsLoop = new System.Random();
        private List<int> AdvertLoopChk;
        private List<ulong> wereNewcomers;
        private DateTime Epoch = new DateTime(1970, 1, 1);

        private string[] urlProviders = new string[]
        {
            "http://ip-api.com/line/{0}?fields=countryCode",
            "http://legacy.iphub.info/api.php?showtype=4&ip={0}",
            "http://geoip.nekudo.com/api/{0}",
            "http://ipinfo.io/{0}/country"
        };

        private List<Regex> regexTags = new List<Regex>
            {new Regex(@"<color=.*?>", RegexOptions.Compiled), new Regex(@"<size=.*?>", RegexOptions.Compiled)};

        private List<string> tags = new List<string> {"</color>", "</size>", "<i>", "</i>", "<b>", "</b>"};

        private static Dictionary<string, object> defaultCommands()
        {
            Dictionary<string, object> dp = new Dictionary<string, object>();
            dp.Add("CmdPlayersList", "players");
            dp.Add("CmdPluginsList", "pluginlist");
            dp.Add("CmdAdminsList", "admins");
            dp.Add("CmdPlayersCount", "online");
            dp.Add("CmdServerRules", "rules");
            dp.Add("CmdHelp", "tipster");
            dp.Add("CmdServerHelp", "help");
            dp.Add("CmdStaffInfo", "staffinfo");
            return dp;
        }

        private static Dictionary<string, object> defaultActiveCommands()
        {
            Dictionary<string, object> dp = new Dictionary<string, object>();
            dp.Add("CmdPlayersList", true);
            dp.Add("CmdPluginsList", false);
            dp.Add("CmdAdminsList", false);
            dp.Add("CmdPlayersCount", true);
            dp.Add("CmdServerRules", true);
            dp.Add("CmdHelp", true);
            dp.Add("CmdServerHelp", false);
            dp.Add("CmdStaffInfo", false);
            return dp;
        }

        private Dictionary<string, object> ChatCommands;
        private Dictionary<string, object> ActiveCommands;
        private List<object> hiddenPlugins;
        private List<string> welcomeMessages;
        private List<string> newcomerMessages;
        private List<string> advertMessages;
        private List<string> rulesMessages;
        private List<string> pluginLanguages;
        private List<string> helpMessages;
        private List<string> staffMessages;
        private string Prefix;
        private string prefixColor;
        private string prefixFormat;
        private string pluginSteamID;
        private string textColorHeadlines;
        private string textColorSeparator;
        private string textColorCommandsInOverview;
        private string textColorCommandsDesc;
        private string textColorPluginsName;
        private string textColorPluginsVer;
        private string textColorPlayersList;
        private string textColorAdminsList;
        private string textColorRules;
        private string textColorHelp;
        private string textColorStaffInfo;
        private string textColorDefault;
        private string seperatorChar;
        private int seperatorLength;
        private string helpMessagesPrefix;
        private bool showPrefixJoins;
        private bool showPrefixLeaves;
        private bool showPrefixAdverts;
        private string timeFormat;
        private string dateFormat;
        private int advertsInterval;
        private bool advertsRandomized;
        private bool advertsUsePopUp;
        private float advertsPopUpTime;
        private string advertsPermBypass;
        private bool enableJoinMessage;
        private bool enableLeaveMessage;
        private bool broadcastToConsole;
        private bool broadcastToConsoleAds;
        private string staffInfoPermission;
        private int staffInfoAuthLevel;
        private bool enableAdvertMessages;
        private bool enableWelcomeMessage;
        private bool enableIPCheck;
        private bool hideAdminsJoin;
        private bool joinTreatAdminAsPlayer;
        private bool leaveTreatAdminAsPlayer;
        private bool hideAdminsLeave;
        private bool hideAdminsList;
        private bool enableChatSeparators;
        private int daysCountryIsValid;
        private bool displaySteamAvatar;
        private bool disablePlayerDatabaseFile;
        private bool useNewcomerMsgs;
        private bool countryBlockUse;
        private List<object> countryBlockList = new List<object>();
        private bool countryBlockWhitelist;
        private bool countryBlockExcludeAdmin;
        private string countryBlockPermBypass;
        private bool countryBlockLogConsole;

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            Dictionary<string, object> data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }

            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }

            return value;
        }

        private void LoadVariables()
        {
            Prefix = Convert.ToString(GetConfig("Formatting", "Prefix", "Tipster"));
            prefixColor = Convert.ToString(GetConfig("Formatting", "Prefix color", "#00b7eb"));
            prefixFormat = Convert.ToString(GetConfig("Formatting", "Prefix format", "| <color={0}>{1}</color> |"));
            pluginSteamID = Convert.ToString(GetConfig("Formatting", "Plugin Icon SteamID", "0"));
            textColorHeadlines = Convert.ToString(GetConfig("Formatting", "Command headlines", "#bfc1c2"));
            textColorSeparator = Convert.ToString(GetConfig("Formatting", "Chat separator", "#acacac"));
            textColorCommandsInOverview = Convert.ToString(GetConfig("Formatting", "Commands in overview", "#efcc00"));
            textColorCommandsDesc = Convert.ToString(GetConfig("Formatting", "Commands descriptions", "#ff8651"));
            textColorPluginsName = Convert.ToString(GetConfig("Formatting", "Plugins name", "#ff8651"));
            textColorPluginsVer = Convert.ToString(GetConfig("Formatting", "Plugins version", "#c0c0c0"));
            textColorPlayersList = Convert.ToString(GetConfig("Formatting", "PlayersList namecolors", "#6699cc"));
            textColorAdminsList = Convert.ToString(GetConfig("Formatting", "AdminsList namecolors", "#00a877"));
            textColorRules = Convert.ToString(GetConfig("Formatting", "Textcolor rulesview", "#ff7538"));
            textColorHelp = Convert.ToString(GetConfig("Formatting", "Textcolor help", "#bebebe"));
            textColorStaffInfo = Convert.ToString(GetConfig("Formatting", "Textcolor staff info", "#bebebe"));
            textColorDefault = Convert.ToString(GetConfig("Formatting", "Textcolor default", "#bebebe"));
            helpMessagesPrefix = Convert.ToString(GetConfig("Formatting", "Helpmessages prefix", "<color=#ff7538><size=18>•</size></color> "));
            timeFormat = Convert.ToString(GetConfig("Formatting", "Time Format", "{hour}:{minute}:{second}"));
            dateFormat = Convert.ToString(GetConfig("Formatting", "Date Format", "{day}/{month}/{year}"));
            showPrefixJoins = Convert.ToBoolean(GetConfig("Formatting", "Show prefix for Joins", true));
            showPrefixLeaves = Convert.ToBoolean(GetConfig("Formatting", "Show prefix for Leaves", true));
            showPrefixAdverts = Convert.ToBoolean(GetConfig("Formatting", "Show prefix for Adverts", true));
            enableChatSeparators = Convert.ToBoolean(GetConfig("Formatting", "Show headline separators", true));
            seperatorChar = Convert.ToString(GetConfig("Formatting", "Seperator", "-"));
            seperatorLength = Convert.ToInt32(GetConfig("Formatting", "Seperator length", 25));
            enableAdvertMessages = Convert.ToBoolean(GetConfig("Adverts", "Adverts enabled", true));
            advertsInterval = Convert.ToInt32(GetConfig("Adverts", "Display interval (minutes)", 10));
            advertsRandomized = Convert.ToBoolean(GetConfig("Adverts", "Display order randomized", true));
            advertsUsePopUp = Convert.ToBoolean(GetConfig("Adverts", "Use PopupNotifications", false));
            advertsPopUpTime = Convert.ToSingle(GetConfig("Adverts", "Used Popup time", 10.0));
            broadcastToConsoleAds = Convert.ToBoolean(GetConfig("Adverts", "Broadcast to console", false));
            advertsPermBypass = Convert.ToString(GetConfig("Adverts", "Permission for bypassing adverts", "tipster.bypassadverts"));
            broadcastToConsole = Convert.ToBoolean(GetConfig("General", "Broadcast to console - join/leave", true));
            displaySteamAvatar = Convert.ToBoolean(GetConfig("General", "Display steam avatar of player - join/leave", false));
            enableWelcomeMessage = Convert.ToBoolean(GetConfig("General", "Enable - welcome message", true));
            enableJoinMessage = Convert.ToBoolean(GetConfig("General", "Enable - join messages", true));
            enableLeaveMessage = Convert.ToBoolean(GetConfig("General", "Enable - leave messages", true));
            enableIPCheck = Convert.ToBoolean(GetConfig("General", "Enable - IP checks (needed for countrycode|countryblock)", true));
            hideAdminsJoin = Convert.ToBoolean(GetConfig("General", "Hide admins - Join", false));
            hideAdminsLeave = Convert.ToBoolean(GetConfig("General", "Hide admins - Leave", true));
            hideAdminsList = Convert.ToBoolean(GetConfig("General", "Hide admins - List", true));
            joinTreatAdminAsPlayer = Convert.ToBoolean(GetConfig("General", "Admin join - treat as player", false));
            leaveTreatAdminAsPlayer = Convert.ToBoolean(GetConfig("General", "Admin leave - treat as player", false));
            daysCountryIsValid = Convert.ToInt32(GetConfig("General", "Days how long country is valid", 7));
            staffInfoPermission = Convert.ToString(GetConfig("General", "Staff info - permission", "tipster.staffinfo"));
            staffInfoAuthLevel = Convert.ToInt32(GetConfig("General", "Staff info - authLevel", 1));
            disablePlayerDatabaseFile = Convert.ToBoolean(GetConfig("General", "Do not load&save player database", false));
            useNewcomerMsgs = Convert.ToBoolean(GetConfig("General", "Use Newcomer messages for first join", true));
            ChatCommands = (Dictionary<string, object>) GetConfig("Commands", "Command", defaultCommands());
            foreach (KeyValuePair<string, object> cmd in defaultCommands())
                if (!ChatCommands.ContainsKey(cmd.Key))
                {
                    ChatCommands.Add(cmd.Key, (string) cmd.Value);
                    Config["Commands", "Command"] = ChatCommands;
                    Changed = true;
                }

            ActiveCommands = (Dictionary<string, object>) GetConfig("Commands", "Activation", defaultActiveCommands());
            foreach (KeyValuePair<string, object> cmd in defaultActiveCommands())
                if (!ActiveCommands.ContainsKey(cmd.Key))
                {
                    ActiveCommands.Add(cmd.Key, (bool) cmd.Value);
                    Config["Commands", "Activation"] = ActiveCommands;
                    Changed = true;
                }

            hiddenPlugins = (List<object>) GetConfig("PluginList", "Hidden in overview", new List<object> {"Rust", "Unity Core", "AdminRadar"});
            countryBlockUse = Convert.ToBoolean(GetConfig("CountryBlocker", "Enable blocker", false));
            countryBlockList = (List<object>) GetConfig("CountryBlocker", "Blocked country codes", new List<object> {"CN", "KP"});
            countryBlockWhitelist = Convert.ToBoolean(GetConfig("CountryBlocker", "Use as whitelist", false));
            countryBlockExcludeAdmin = Convert.ToBoolean(GetConfig("CountryBlocker", "Exclude admins", true));
            countryBlockPermBypass = Convert.ToString(GetConfig("CountryBlocker", "Bypass permission", "tipster.bypassblock"));
            countryBlockLogConsole = Convert.ToBoolean(GetConfig("CountryBlocker", "Log kicks to console", true));

            if (!Changed)
                return;

            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private readonly Dictionary<string, string> defaultMessages = new Dictionary<string, string>
        {
            {"Join Message", "<color=#5af>{player.name} <color=#bfc1c2>joined from</color> {player.country}</color>"},
            {"Join Message Unknown", "<color=#5af>{player.name}</color> <color=#bfc1c2>joined from anywhere</color>"},
            {"Join Admin", "<color=#af5>{player.name} <color=#bfc1c2>joined the server</color></color>"},
            {"CountryBlocked", "Your country is blocked on this server"},
            {"Leave Message", "<color=#5af>{player.name}</color> left the server (Reason: {reason})"},
            {"Leave Admin", "<color=#af5>{player.name}</color> left the server"},
            {"No Admins Online", "There are no <color=#00b7eb>Admins</color> currently online"},
            {"CmdPlayersList Description", "List of active players"},
            {"CmdPluginsList Description", "List of plugins running in the server"},
            {"CmdAdminsList Description", "List of active Admins"},
            {"CmdServerRules Description", "Displays server rules"},
            {"CmdServerHelp Description", "Displays server help and informations"},
            {"CmdHelp Description", "Shows this overview"},
            {"CmdPlayersCount Description", "Counts players, sleepers and admins of the server"},
            {"CmdStaffInfo Description", "Shows the server staff special informations"},
            {"Players List Title", "Players List"}, {"Plugins List Title", "Plugins List"},
            {"Admins List Title", "Admins Online"}, {"Server Rules Title", "Server Rules"},
            {"Server Help Title", "Server Informations"}, {"Server StaffInfo Title", "Staff Informations"},
            {"Command Overview", "Available Commands"},
            {
                "Players Count Message",
                "There are <color=#ff7538>{players.active} <color=#bfc1c2>of</color> {server.maxplayers}</color> <color=#bfc1c2>players in the server, and <color=#ff7538>{players.sleepers}</color> sleepers</color>\n<color=#ff7538>{players.joining}</color>  players are joining, <color=#ff7538>{players.queued}</color> are in the queue."
            },
            {"Welcome00", "<size=18>Welcome <color=#add8e6>{player.name}</color></size>"},
            {
                "Welcome01",
                "<color=#ff7538><size=20>•</size></color> Type <color=#ff7538>/tipster</color> for all available commands"
            },
            {
                "Welcome02",
                "<color=#ff7538><size=20>•</size></color> Please respect our server <color=#ff7538>/rules</color>"
            },
            {"Welcome03", "<color=#ff7538><size=20>•</size></color> Have fun and respect other players"},
            {"Newcomer00", "<size=18>Thx for joining and welcome aboard <color=#add8e6>{player.name}</color></size>"},
            {
                "Newcomer01",
                "<color=#ff7538><size=20>•</size></color> Type <color=#ff7538>/tipster</color> for all available commands"
            },
            {
                "Newcomer02",
                "<color=#ff7538><size=20>•</size></color> Please respect our server <color=#ff7538>/rules</color>"
            },
            {"Newcomer03", "<color=#ff7538><size=20>•</size></color> Have fun and respect other players"},
            {"Help00", "Type <color=#ff7538>/tipster</color> for all available commands"},
            {"Help01", "Open this help by <color=#ff7538>/help</color>"},
            {"Help02", "Check the server <color=#ff7538>/rules</color>"},
            {"Help03", "See who's online by typing in <color=#ff7538>/players</color>"},
            {
                "Advert00",
                "<color=#ff7538>Need help?</color> Try calling for the <color=#00ffff>Admins</color> in the chat."
            },
            {"Advert01", "Please avoid any insults and be respectful!"},
            {"Advert02", "Cheating or abusing of game exploits will result in a <color=#ff0000>permanent</color> ban."},
            {"Advert03", "You are playing on: <color=#ff7538>{server.hostname}</color>"},
            {
                "Advert04",
                "There are <color=#ff7538>{players.active}<color=#bfc1c2>/</color>{server.maxplayers} <color=#bfc1c2>players playing in the server, and</color> {players.sleepers}</color> sleepers."
            },
            {"Advert05", "Check the tips with <color=#ff7538>/tipster</color> command."},
            {"Rules00", "Cheating is strictly prohibited!"}, {"Rules01", "Respect all players!"},
            {"Rules02", "Don't spam the chat!"}, {"Staff00", "Special information restricted to you as staff member."},
            {"Staff01", "As staff member you have access to special commands."}
        };

        private Dictionary<string, string> CheckLangFiles(Dictionary<string, string> existingMessages,
            Dictionary<string, string> defaultMessages)
        {
            if (existingMessages == null || existingMessages.Count == 0)
                return new Dictionary<string, string>(defaultMessages);
            foreach (KeyValuePair<string, string> current in existingMessages)
                if (!defaultMessages.ContainsKey(current.Key))
                    defaultMessages.Add(current.Key, current.Value);
            foreach (KeyValuePair<string, string> current in defaultMessages)
                if (!existingMessages.ContainsKey(current.Key))
                    existingMessages.Add(current.Key, current.Value);
            return new Dictionary<string, string>(existingMessages);
        }

        private void Init()
        {
            LoadVariables();
            if (seperatorChar != string.Empty && seperatorChar.Length == 1)
                seperatorText = string.Join(seperatorChar, new string[seperatorLength + 1]);
            pluginIcon = Convert.ToUInt64(pluginSteamID);
            pluginLanguages = lang.GetLanguages(this).ToList();
            Dictionary<string, string> referenceMsgs = new Dictionary<string, string>();
            if (pluginLanguages.Count == 0 || pluginLanguages.Count > 0 && !pluginLanguages.Contains("en"))
            {
                lang.RegisterMessages(new Dictionary<string, string>(defaultMessages), this);
                referenceMsgs = new Dictionary<string, string>(defaultMessages);
                pluginLanguages.Add("en");
            }
            else
            {
                referenceMsgs = lang.GetMessages("en", this);
                lang.RegisterMessages(new Dictionary<string, string>(CheckLangFiles(defaultMessages, referenceMsgs)),
                    this, "en");
            }

            welcomeMessages = new List<string>();
            newcomerMessages = new List<string>();
            advertMessages = new List<string>();
            rulesMessages = new List<string>();
            helpMessages = new List<string>();
            staffMessages = new List<string>();
            foreach (KeyValuePair<string, string> msg in referenceMsgs)
            {
                if (msg.Key.StartsWith("Welcome") && !welcomeMessages.Contains(msg.Key) && msg.Value.Length > 1)
                    welcomeMessages.Add(msg.Key);
                if (msg.Key.StartsWith("Newcomer") && !newcomerMessages.Contains(msg.Key) && msg.Value.Length > 1)
                    newcomerMessages.Add(msg.Key);
                if (msg.Key.StartsWith("Advert") && !advertMessages.Contains(msg.Key) && msg.Value.Length > 1)
                    advertMessages.Add(msg.Key);
                if (msg.Key.StartsWith("Rules") && !rulesMessages.Contains(msg.Key) && msg.Value.Length > 1)
                    rulesMessages.Add(msg.Key);
                if (msg.Key.StartsWith("Help") && !helpMessages.Contains(msg.Key) && msg.Value.Length > 1)
                    helpMessages.Add(msg.Key);
                if (msg.Key.StartsWith("Staff") && !staffMessages.Contains(msg.Key) && msg.Value.Length > 1)
                    staffMessages.Add(msg.Key);
            }

            foreach (string pluginLanguage in pluginLanguages)
                lang.RegisterMessages(
                    new Dictionary<string, string>(
                        CheckLangFiles(lang.GetMessages(pluginLanguage, this), referenceMsgs)), this, pluginLanguage);
            referenceMsgs.Clear();
            referenceMsgs = null;
            permission.RegisterPermission(countryBlockPermBypass, this);
            permission.RegisterPermission(staffInfoPermission, this);
            permission.RegisterPermission(advertsPermBypass, this);
            jsonsettings = new JsonSerializerSettings();
            jsonsettings.Converters.Add(new KeyValuePairConverter());
            foreach (KeyValuePair<string, object> active in ActiveCommands)
            {
                string name = active.Key.ToString();
                if ((bool) active.Value)
                    cmd.AddChatCommand((string) ChatCommands[name], this, name.Replace("Cmd", "Command"));
            }

            CountryNames = new Dictionary<string, object>();
            DbLanguages = new List<string>();
            storedCountryData = Interface.GetMod().DataFileSystem
                .ReadObject<Dictionary<string, object>>(CountryDatabase);
            if (storedCountryData.Count == 0)
            {
                webrequest.Enqueue("http://pastebin.com/raw/Gu3htR11", null, (code, response) =>
                {
                    if (response != null && code == 200)
                    {
                        string result = response.Replace('\n', '\r');
                        string[] lines = result.Split(new char[] {'\r'}, StringSplitOptions.RemoveEmptyEntries);
                        int num_rows = lines.Length;
                        int num_cols = lines[0].Split(',').Length;
                        for (int r = 0; r < num_rows; r++)
                        {
                            string[] line_r = lines[r].Split(',');
                            if (!CountryNames.ContainsKey(line_r[0].ToLower()))
                            {
                                CountryNames.Add(line_r[0].ToLower(), new Dictionary<string, object>());
                                DbLanguages.Add(line_r[0].ToLower());
                            }

                            (CountryNames[line_r[0].ToLower()] as Dictionary<string, object>).Add(line_r[1], line_r[2]);
                        }

                        storedCountryData = new Dictionary<string, object>(CountryNames);
                        Interface.GetMod().DataFileSystem.WriteObject(CountryDatabase, storedCountryData);
                    }
                }, this, RequestMethod.POST, null, 10f);
            }
            else
            {
                CountryNames = new Dictionary<string, object>(storedCountryData);
                foreach (KeyValuePair<string, object> lang in CountryNames) DbLanguages.Add(lang.Key.ToString());
            }
        }

        private bool IsSteamId(ulong id)
        {
            return id > 76561197960265728uL;
        }

        private void OnServerInitialized()
        {
            wereNewcomers = new List<ulong>();
            if (disablePlayerDatabaseFile) Players = new Dictionary<ulong, PlayerCache>();
            else
                Players = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerCache>>(PlayerDatabaseFile);
            foreach (BasePlayer ply in BasePlayer.activePlayerList.ToList())
            {
                if (ply.userID == 0uL || !IsSteamId(ply.userID)) continue;
                PlayerCache obj = null;
                if (!Players.TryGetValue(ply.userID, out obj))
                {
                    if (useNewcomerMsgs && !disablePlayerDatabaseFile && !wereNewcomers.Contains(ply.userID))
                        wereNewcomers.Add(ply.userID);
                    Players.Add(ply.userID, new PlayerCache(ply.net.connection));
                }
                else
                {
                    Players[ply.userID].Update(ply.net.connection);
                }

                Players[ply.userID].Name = ply.displayName;
            }

            if (!disablePlayerDatabaseFile) Interface.Oxide.DataFileSystem.WriteObject(PlayerDatabaseFile, Players);
            Initialized = true;
            if (enableAdvertMessages && advertMessages.Count > 0)
            {
                AdvertLoopChk = new List<int>();
                timer.Every(advertsInterval * 60, SendAdvert);
                Puts(
                    $"Adverts active (Interval: {advertsInterval} min | Console: {broadcastToConsoleAds} | Popup: {advertsUsePopUp})");
            }
        }

        private void Unload()
        {
            if (!disablePlayerDatabaseFile) Interface.Oxide.DataFileSystem.WriteObject(PlayerDatabaseFile, Players);
        }

        private void OnServerSave()
        {
            if (!disablePlayerDatabaseFile) Interface.Oxide.DataFileSystem.WriteObject(PlayerDatabaseFile, Players);
        }

        private class PlayerCache
        {
            public string Ip = string.Empty;
            public string Ctry = string.Empty;
            public string Name = string.Empty;
            public string CCode = string.Empty;
            public int Checked = 0;
            [JsonIgnore] [ProtoIgnore] public string Lang = string.Empty;

            public PlayerCache()
            {
            }

            internal PlayerCache(Connection connect)
            {
                Update(connect);
            }

            internal void Update(Connection connect)
            {
                Name = connect.username;
                string tempIP = connect.ipaddress.Split(':')[0];
                if (tempIP == "127.0.0.1")
                    tempIP = Steamworks.SteamServer.PublicIp.ToString();
                Ip = tempIP;
                Lang = connect.info.GetString("global.language", "en");
            }
        }

        private void MsgPlayer(BasePlayer player, string msg, bool prefix, ulong icon = 0uL)
        {
            Player.Message(player, $"<color={textColorDefault}>{msg}</color>",
                prefix ? string.Format(prefixFormat, prefixColor, Prefix) : null, icon != 0uL ? icon : pluginIcon);
        }

        private string StripTags(string original)
        {
            foreach (string tag in tags) original = original.Replace(tag, "");
            foreach (Regex regexTag in regexTags) original = regexTag.Replace(original, "");
            return original;
        }

        private List<ulong> clientAuthPassed = new List<ulong>();

        private void OnClientAuth(Connection connection)
        {
            if (!Initialized || connection == null || connection.userid == 0uL || !IsSteamId(connection.userid)) return;
            ulong userid = connection.userid;
            if (!clientAuthPassed.Contains(userid)) clientAuthPassed.Add(userid);
            if (!Players.ContainsKey(userid))
            {
                if (useNewcomerMsgs && !disablePlayerDatabaseFile && !wereNewcomers.Contains(userid))
                    wereNewcomers.Add(userid);
                Players.Add(userid, new PlayerCache(connection));
            }
            else
            {
                Players[userid].Update(connection);
            }

            if (!enableIPCheck) return;
            string ip = Players[userid].Ip;
            if (Players[userid].Ctry == string.Empty || Players[userid].Checked == 0 ||
                (int) DateTime.UtcNow.Subtract(Epoch).TotalSeconds - Players[userid].Checked >
                daysCountryIsValid * 86400)
            {
                int index = new System.Random().Next(urlProviders.Length);
                List<int> UrlsDone = new List<int>();
                CheckCCode(userid, ip, index, UrlsDone, 0);
            }
        }

        private void CanClientLogin(Connection connection)
        {
            if (!Initialized || !enableIPCheck || connection == null || connection.userid == 0uL ||
                !IsSteamId(connection.userid)) return;
            ulong userid = connection.userid;
            if (clientAuthPassed.Contains(userid))
            {
                clientAuthPassed.Remove(userid);
                return;
            }

            if (!Players.ContainsKey(userid))
            {
                if (useNewcomerMsgs && !disablePlayerDatabaseFile && !wereNewcomers.Contains(userid))
                    wereNewcomers.Add(userid);
                Players.Add(userid, new PlayerCache(connection));
            }
            else
            {
                Players[userid].Update(connection);
            }

            string ip = Players[userid].Ip;
            if (Players[userid].Ctry == string.Empty || Players[userid].Checked == 0 ||
                (int) DateTime.UtcNow.Subtract(Epoch).TotalSeconds - Players[userid].Checked >
                daysCountryIsValid * 86400)
            {
                int index = new System.Random().Next(urlProviders.Length);
                List<int> UrlsDone = new List<int>();
                CheckCCode(userid, ip, index, UrlsDone, 0);
            }
        }

        private void CheckCCode(ulong userid, string ip, int index, List<int> usedUrl, int run = 0)
        {
            run++;
            bool success = false;
            string url = string.Format(urlProviders[index], ip);
            try
            {
                webrequest.Enqueue(url, null, (code, response) =>
                {
                    if (response != null && code == 200 && !string.IsNullOrEmpty(response) && response != "undefined" ||
                        response != "xx")
                    {
                        string cCode = string.Empty;
                        string country = string.Empty;
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (json["country"] != null) cCode = (string) json["country"]["code"];
                            else if (json["countryCode"] != null) cCode = (string) json["countryCode"];
                        }
                        catch
                        {
                            cCode = Regex.Replace(response, @"\t|\n|\r|\s.*", "");
                        }

                        if (cCode != string.Empty && cCode.Length == 2)
                        {
                            try
                            {
                                country =
                                    (string) ((JObject) CountryNames["en"]).ToObject<Dictionary<string, object>>()[
                                        cCode.ToUpper()];
                            }
                            catch
                            {
                            }

                            if (country != string.Empty)
                            {
                                Players[userid].Ctry = country;
                                Players[userid].CCode = cCode;
                                Players[userid].Checked = (int) DateTime.UtcNow.Subtract(Epoch).TotalSeconds;
                                return;
                            }
                        }
                    }

                    if (run >= urlProviders.Length) return;
                    if (!success)
                    {
                        usedUrl.Add(index);
                        while (usedUrl.Contains(index)) index = new System.Random().Next(urlProviders.Length);
                        CheckCCode(userid, ip, index, usedUrl, run);
                        return;
                    }
                }, this, RequestMethod.GET, null, 4f);
            }
            catch
            {
                if (run >= urlProviders.Length) return;
                usedUrl.Add(index);
                while (usedUrl.Contains(index)) index = new System.Random().Next(urlProviders.Length);
                CheckCCode(userid, ip, index, usedUrl, run);
            }
        }

        private bool IsPrivateIP(string ipAddress)
        {
            string[] split = ipAddress.Split(new[] {"."}, StringSplitOptions.RemoveEmptyEntries);
            int[] ip = new[] {int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]), int.Parse(split[3])};
            return ip[0] == 10 || ip[0] == 127 || ip[0] == 192 && ip[1] == 168 ||
                   ip[0] == 172 && ip[1] >= 16 && ip[1] <= 31;
        }

        private string GetUserLang(ulong userID)
        {
            string plyLang = lang.GetLanguage(userID.ToString());
            if (DbLanguages.Contains(plyLang.Substring(0, 2))) return plyLang.Substring(0, 2);
            return "en";
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!Initialized || player == null || !IsSteamId(player.userID)) return;
            PlayerCache obj;
            if (!Players.TryGetValue(player.userID, out obj))
            {
                if (!disablePlayerDatabaseFile && !wereNewcomers.Contains(player.userID))
                    wereNewcomers.Add(player.userID);
                Players.Add(player.userID, new PlayerCache(player.net.connection));
            }

            if (countryBlockUse && !IsPrivateIP(Players[player.userID].Ip) &&
                !permission.UserHasPermission(player.UserIDString, countryBlockPermBypass))
                if (!countryBlockExcludeAdmin || countryBlockExcludeAdmin &&
                    !(player.IsAdmin || permission.UserHasGroup(player.UserIDString, "admin")))
                    if (enableIPCheck &&
                        (!countryBlockList.Contains(Players[player.userID].CCode) && countryBlockWhitelist ||
                         countryBlockList.Contains(Players[player.userID].CCode) && !countryBlockWhitelist))
                    {
                        NextTick(() =>
                        {
                            if (countryBlockLogConsole)
                                Puts(
                                    $"CountryBlock > Kicking player '{player.displayName}' from '{Players[player.userID].CCode}'");
                            player.Kick(lang.GetMessage("CountryBlocked", this, player.UserIDString));
                        });
                        return;
                    }

            if (enableJoinMessage || enableWelcomeMessage) JoinMessages(player);
            else MsgPlayer(player, string.Join("\n", new string[50 + 1]), false);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!Initialized || player == null || !enableLeaveMessage || player.IsAdmin && hideAdminsLeave ||
                !Players.ContainsKey(player.userID)) return;
            if (player.IsAdmin && !leaveTreatAdminAsPlayer)
            {
                foreach (BasePlayer target in BasePlayer.activePlayerList)
                    MsgPlayer(target,
                        GetNameFormats(lang.GetMessage("Leave Admin", this, target.UserIDString), player, target),
                        showPrefixLeaves, displaySteamAvatar ? player.userID.Get() : 0uL);
                if (broadcastToConsole) Puts(StripTags(GetNameFormats(lang.GetMessage("Leave Admin", this), player)));
                return;
            }

            if (reason.StartsWith("Kicked: "))
            {
                if (countryBlockUse && !countryBlockList.Contains(Players[player.userID].CCode) &&
                    countryBlockWhitelist ||
                    countryBlockList.Contains(Players[player.userID].CCode) && !countryBlockWhitelist) return;
                reason = "Kicked: " + reason.Replace(reason.Split()[0], "").Trim();
            }

            foreach (BasePlayer target in BasePlayer.activePlayerList)
                MsgPlayer(target,
                    GetNameFormats(
                        lang.GetMessage("Leave Message", this, target.UserIDString).Replace("{reason}", reason), player,
                        target), showPrefixLeaves, displaySteamAvatar ? player.userID.Get() : 0uL);
            if (broadcastToConsole)
                Puts(StripTags(GetNameFormats(lang.GetMessage("Leave Message", this).Replace("{reason}", reason),
                    player)));
        }

        private void CommandPlayersList(BasePlayer player, string command, string[] args)
        {
            StringBuilder wT = new StringBuilder();
            wT.AppendLine($"<color={textColorHeadlines}>" +
                          lang.GetMessage("Players List Title", this, player.UserIDString) + "</color>");
            if (enableChatSeparators) wT.AppendLine($"<color={textColorSeparator}>" + seperatorText + "</color>");
            foreach (BasePlayer active in BasePlayer.activePlayerList)
            {
                if (!Players.ContainsKey(active.userID))
                    continue;

                if (wT.ToString().Length > 900)
                {
                    MsgPlayer(player, wT.ToString().TrimEnd(',', ' '), false);
                    wT.Clear();
                }

                if (!(active.IsAdmin && hideAdminsList))
                    wT.Append($"<color={textColorPlayersList}> {active.displayName}</color>, ");
            }

            MsgPlayer(player, wT.ToString().TrimEnd(',', ' '), false);
            wT.Clear();
        }

        private void CommandAdminsList(BasePlayer player, string command, string[] args)
        {
            if (hideAdminsList && !player.IsAdmin)
            {
                MsgPlayer(player, lang.GetMessage("No Admins Online", this, player.UserIDString), false);
            }
            else
            {
                StringBuilder wT = new StringBuilder();
                wT.AppendLine($"<color={textColorHeadlines}>" +
                              lang.GetMessage("Admins List Title", this, player.UserIDString) + "</color>");
                if (enableChatSeparators) wT.AppendLine($"<color={textColorSeparator}>" + seperatorText + "</color>");
                foreach (BasePlayer admin in BasePlayer.activePlayerList)
                {
                    if (!admin.IsAdmin)
                        continue;

                    if (wT.ToString().Length > 900)
                    {
                        MsgPlayer(player, wT.ToString().TrimEnd(',', ' '), false);
                        wT.Clear();
                    }

                    wT.Append($"<color={textColorAdminsList}> {admin.displayName}</color>, ");
                }

                MsgPlayer(player, wT.ToString().TrimEnd(',', ' '), false);
                wT.Clear();
            }
        }

        private void CommandServerRules(BasePlayer player, string command, string[] args)
        {
            if (rulesMessages.Count == 0) return;
            StringBuilder wT = new StringBuilder();
            wT.AppendLine($"<color={textColorHeadlines}>" +
                          lang.GetMessage("Server Rules Title", this, player.UserIDString) + "</color>");
            if (enableChatSeparators) wT.AppendLine($"<color={textColorSeparator}>" + seperatorText + "</color>");
            int count = 1;
            foreach (string line in rulesMessages)
            {
                if (wT.ToString().Length > 900)
                {
                    MsgPlayer(player, wT.ToString().TrimEnd(), false);
                    wT.Clear();
                }

                wT.AppendLine(
                    $"<color={textColorHeadlines}>{count})</color> <color={textColorRules}>{lang.GetMessage(line, this, player.UserIDString)}</color>");
                count++;
            }

            MsgPlayer(player, wT.ToString().TrimEnd(), false);
            wT.Clear();
        }

        private void CommandServerHelp(BasePlayer player, string command, string[] args)
        {
            if (helpMessages.Count == 0) return;
            StringBuilder wT = new StringBuilder();
            wT.AppendLine($"<color={textColorHeadlines}>" +
                          lang.GetMessage("Server Help Title", this, player.UserIDString) + "</color>");
            if (enableChatSeparators) wT.AppendLine($"<color={textColorSeparator}>" + seperatorText + "</color>");
            int count = 1;
            foreach (string line in helpMessages)
            {
                if (wT.ToString().Length > 900)
                {
                    MsgPlayer(player, wT.ToString().TrimEnd(), false);
                    wT.Clear();
                }

                wT.AppendLine(helpMessagesPrefix +
                              $"<color={textColorHelp}>{lang.GetMessage(line, this, player.UserIDString)}</color>");
                count++;
            }

            MsgPlayer(player, wT.ToString().TrimEnd(), false);
            wT.Clear();
        }

        private void CommandStaffInfo(BasePlayer player, string command, string[] args)
        {
            if (staffMessages.Count == 0 || !(player.IsAdmin ||
                                              permission.UserHasPermission(player.UserIDString, staffInfoPermission) ||
                                              player.net.connection.authLevel >= staffInfoAuthLevel)) return;
            StringBuilder wT = new StringBuilder();
            wT.AppendLine($"<color={textColorHeadlines}>" +
                          lang.GetMessage("Server StaffInfo Title", this, player.UserIDString) + "</color>");
            if (enableChatSeparators) wT.AppendLine($"<color={textColorSeparator}>" + seperatorText + "</color>");
            int count = 1;
            foreach (string line in staffMessages)
            {
                if (wT.ToString().Length > 900)
                {
                    MsgPlayer(player, wT.ToString().TrimEnd(), false);
                    wT.Clear();
                }

                wT.AppendLine(
                    $"<color={textColorStaffInfo}>{lang.GetMessage(line, this, player.UserIDString)}</color>");
                count++;
            }

            MsgPlayer(player, wT.ToString().TrimEnd(), false);
            wT.Clear();
        }

        private void CommandPluginsList(BasePlayer player, string command, string[] args)
        {
            StringBuilder wT = new StringBuilder();
            wT.AppendLine($"<color={textColorHeadlines}>" +
                          lang.GetMessage("Plugins List Title", this, player.UserIDString) + "</color>");
            if (enableChatSeparators) wT.AppendLine($"<color={textColorSeparator}>" + seperatorText + "</color>");
            foreach (Plugin p in plugins.GetAll())
            {
                if (wT.ToString().Length > 900)
                {
                    MsgPlayer(player, wT.ToString().TrimEnd(',', ' '), false);
                    wT.Clear();
                }

                if (!hiddenPlugins.Contains(p.Title))
                    wT.Append(string.Format("<color={2}>{0}</color> <color={3}>v{1}</color>", p.Title, p.Version,
                                  textColorPluginsName, textColorPluginsVer) + ", ");
            }

            MsgPlayer(player, wT.ToString().TrimEnd(',', ' '), false);
            wT.Clear();
        }

        private void CommandPlayersCount(BasePlayer player, string command, string[] args)
        {
            MsgPlayer(player, GetNameFormats(lang.GetMessage("Players Count Message", this, player.UserIDString)),
                false);
        }

        private void CommandHelp(BasePlayer player, string command, string[] args)
        {
            StringBuilder wT = new StringBuilder();
            wT.AppendLine($"<color={textColorHeadlines}>" +
                          lang.GetMessage("Command Overview", this, player.UserIDString) + "</color>");
            if (enableChatSeparators) wT.AppendLine($"<color={textColorSeparator}>" + seperatorText + "</color>");
            foreach (KeyValuePair<string, object> cmd in ActiveCommands)
            {
                if (cmd.Key == "CmdStaffInfo" && !(player.IsAdmin ||
                                                   permission.UserHasPermission(player.UserIDString,
                                                       staffInfoPermission) ||
                                                   player.net.connection.authLevel >= staffInfoAuthLevel)) continue;
                if ((bool) cmd.Value)
                    wT.AppendLine($"<color={textColorCommandsInOverview}>/" + (string) ChatCommands[cmd.Key] +
                                  $" </color>: <color={textColorCommandsDesc}>" +
                                  lang.GetMessage(cmd.Key + " Description", this, player.UserIDString) + "</color>");
            }

            MsgPlayer(player, wT.ToString().TrimEnd(',', ' '), false);
            wT.Clear();
        }

        private void JoinMessages(BasePlayer player)
        {
            if (enableJoinMessage && NotHideJoin(player))
            {
                if (player.IsAdmin && !joinTreatAdminAsPlayer)
                {
                    foreach (BasePlayer target in BasePlayer.activePlayerList.Where(t => t != player).ToList())
                        MsgPlayer(target,
                            GetNameFormats(lang.GetMessage("Join Admin", this, target.UserIDString), player, target),
                            showPrefixJoins, displaySteamAvatar ? player.userID.Get() : 0uL);
                    if (broadcastToConsole)
                        Puts(StripTags(GetNameFormats(lang.GetMessage("Join Admin", this), player)));
                }
                else
                {
                    if (enableIPCheck && Players[player.userID].Ctry != string.Empty)
                    {
                        foreach (BasePlayer target in BasePlayer.activePlayerList.Where(t => t != player).ToList())
                            MsgPlayer(target,
                                GetNameFormats(lang.GetMessage("Join Message", this, target.UserIDString), player,
                                    target), showPrefixJoins, displaySteamAvatar ? player.userID.Get() : 0uL);
                        if (broadcastToConsole)
                            Puts(StripTags(GetNameFormats(lang.GetMessage("Join Message", this), player)));
                    }
                    else
                    {
                        foreach (BasePlayer target in BasePlayer.activePlayerList.Where(t => t != player).ToList())
                            MsgPlayer(target,
                                GetNameFormats(lang.GetMessage("Join Message Unknown", this, target.UserIDString),
                                    player, target), showPrefixJoins, displaySteamAvatar ? player.userID.Get() : 0uL);
                        if (broadcastToConsole)
                            Puts(StripTags(GetNameFormats(lang.GetMessage("Join Message Unknown", this), player)));
                    }
                }
            }

            if (enableWelcomeMessage && welcomeMessages.Count > 0)
                ServerMgr.Instance.StartCoroutine(WaitForReady(player));
        }

        private IEnumerator WaitForReady(BasePlayer player)
        {
            yield return new WaitWhile(new Func<bool>(() => player.IsReceivingSnapshot || player.IsSleeping()));
            if (player.IsDead()) yield return null;
            MsgPlayer(player, string.Join("\n", new string[50 + 1]), false);
            if (useNewcomerMsgs && wereNewcomers.Contains(player.userID))
            {
                wereNewcomers.Remove(player.userID);
                NewcomerMessage(player);
            }
            else
            {
                WelcomeMessage(player);
            }

            yield return null;
        }

        private void WelcomeMessage(BasePlayer player)
        {
            if (player == null) return;
            StringBuilder wT = new StringBuilder();
            foreach (string line in welcomeMessages)
            {
                if (wT.ToString().Length > 1000)
                {
                    MsgPlayer(player, wT.ToString().TrimEnd(), false);
                    wT.Clear();
                }

                wT.AppendLine(GetNameFormats(lang.GetMessage(line, this, player.UserIDString), player));
            }

            MsgPlayer(player, wT.ToString().TrimEnd(), false);
            wT.Clear();
        }

        private void NewcomerMessage(BasePlayer player)
        {
            if (player == null) return;
            StringBuilder wT = new StringBuilder();
            foreach (string line in newcomerMessages)
            {
                if (wT.ToString().Length > 1000)
                {
                    MsgPlayer(player, wT.ToString().TrimEnd(), false);
                    wT.Clear();
                }

                wT.AppendLine(GetNameFormats(lang.GetMessage(line, this, player.UserIDString), player));
            }

            MsgPlayer(player, wT.ToString().TrimEnd(), false);
            wT.Clear();
        }

        private void SendAdvert()
        {
            if (AdvertLoopChk.Count == advertMessages.Count) AdvertLoopChk.Clear();
            int index = 0;
            if (advertsRandomized) index = AdvertsLoop.Next(advertMessages.Count);
            else index = AdvertLoopChk.Count;
            while (AdvertLoopChk.Contains(index)) index = AdvertsLoop.Next(advertMessages.Count);
            AdvertLoopChk.Add(index);
            if (broadcastToConsoleAds) Puts(StripTags(GetNameFormats(lang.GetMessage(advertMessages[index], this))));
            List<BasePlayer> players = BasePlayer.activePlayerList
                .Where(p => !permission.UserHasPermission(p.UserIDString, advertsPermBypass)).ToList();
            if (players == null || players.Count == 0) return;
            if (advertsUsePopUp && PopupNotifications)
            {
                if (showPrefixAdverts)
                    foreach (BasePlayer player in players)
                        PopupNotifications.Call("CreatePopupNotification",
                            string.Format(prefixFormat, prefixColor, Prefix) + " " +
                            GetNameFormats(lang.GetMessage(advertMessages[index], this, player.UserIDString)), player,
                            advertsPopUpTime);
                else
                    foreach (BasePlayer player in players)
                        PopupNotifications.Call("CreatePopupNotification",
                            GetNameFormats(lang.GetMessage(advertMessages[index], this, player.UserIDString)), player,
                            advertsPopUpTime);
            }
            else
            {
                foreach (BasePlayer player in players)
                    MsgPlayer(player, GetNameFormats(lang.GetMessage(advertMessages[index], this, player.UserIDString)),
                        showPrefixAdverts);
            }
        }

        private string GetNameFormats(string text, BasePlayer player = null, BasePlayer target = null)
        {
            int Active = BasePlayer.activePlayerList.Count;
            int Sleeping = BasePlayer.sleepingPlayerList.Count;
            DateTime Now = DateTime.Now;
            string time = timeFormat.Replace("{hour}", Pads(Now.Hour.ToString()))
                .Replace("{minute}", Pads(Now.Minute.ToString())).Replace("{second}", Pads(Now.Second.ToString()));
            string date = dateFormat.Replace("{year}", Now.Year.ToString())
                .Replace("{month}", Pads(Now.Month.ToString())).Replace("{day}", Pads(Now.Day.ToString()));
            Dictionary<string, object> Dict = new Dictionary<string, object>
            {
                {"{server.ip}", ConVar.Server.ip}, {"{server.port}", ConVar.Server.port},
                {"{server.hostname}", ConVar.Server.hostname}, {"{server.description}", ConVar.Server.description},
                {"{server.maxplayers}", ConVar.Server.maxplayers}, {"{server.worldsize}", ConVar.Server.worldsize},
                {"{server.seed}", ConVar.Server.seed}, {"{server.level}", ConVar.Server.level},
                {"{localtime.now}", time}, {"{localtime.date}", date}, {"{players.active}", Active},
                {"{players.sleepers}", Sleeping}, {"{players.joining}", ServerMgr.Instance.connectionQueue.Joining},
                {"{players.queued}", ServerMgr.Instance.connectionQueue.Queued},
                {"{players.online}", ServerMgr.Instance.connectionQueue.Joining + Active},
                {"{players.total}", Active + Sleeping}
            };
            if (player != null && Players.ContainsKey(player.userID))
            {
                Dict.Add("{player.name}", Players[player.userID].Name);
                if (target == null) Dict.Add("{player.country}", Players[player.userID].Ctry);
                else
                    try
                    {
                        Dict.Add("{player.country}",
                            (string) ((JObject) CountryNames[GetUserLang(target.userID)])
                            .ToObject<Dictionary<string, object>>()[Players[player.userID].CCode.ToUpper()]);
                    }
                    catch
                    {
                        Dict.Add("{player.country}", Players[player.userID].Ctry);
                    }

                Dict.Add("{player.countrycode}", Players[player.userID].CCode);
                Dict.Add("{player.ip}", Players[player.userID].Ip);
                Dict.Add("{player.uid}", player.UserIDString);
            }

            foreach (KeyValuePair<string, object> kvp in Dict) text = text.Replace(kvp.Key, kvp.Value.ToString());
            return text;
        }

        private bool NotHideJoin(BasePlayer player)
        {
            return !(hideAdminsJoin && player.IsAdmin);
        }

        private string Pads(string target, int number = 2)
        {
            return target.PadLeft(number, '0');
        }
    }
}