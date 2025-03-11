using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("DiscordLinkBot", "Amino & Shady14u", "2.0.8")]
    [Description("Discord link")]
    class DiscordLinkBot : RustPlugin
    {
        #region Config
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

        public class Configuration
        {

            public bool UseUI { get; set; } = true;

            [JsonProperty(PropertyName = "Code Generation Cooldown")]
            public double CodeCooldown { get; set; } = 5;
            [JsonProperty(PropertyName = "Auto open instructions in UI")]
            public bool AutoInstructions { get; set; } = false;

            [JsonProperty(PropertyName = "Command To Link")]
            public List<string> CommandToLink { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Command To Search Links")]
            public List<string> CommandToSearch { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Command To Unlink")]
            public List<string> CommandToUnlink { get; set; } = new List<string>();
            public string LinkedOxideUserGroup { get; set; } = "linked";

            [JsonProperty(PropertyName = "Oxide Group for Discord Boosters")]
            public string DiscordBoosterUserGroup { get; set; } = "Booster";

            [JsonProperty(PropertyName = "Roles to Sync (Steam : Discord)")]
            public Dictionary<string, string> RolesToSync = new Dictionary<string, string>();

            [JsonProperty(PropertyName = "Booster Color")]
            public string BoosterTextColor { get; set; } = ".72 .01 .9 .9";

            [JsonProperty(PropertyName = "Code Color")]
            public string CodeColor { get; set; } = "0 1 0 .9";

            [JsonProperty(PropertyName = "Default Player Avatar Url")]
            public string DefaultAvatarUrl { get; set; } = "https://media.discordapp.net/attachments/1134276798427959427/1134276902903877693/DefaultAvatar.png";

            [JsonProperty(PropertyName = "Discord Invite Url")]
            public string DiscordUrl { get; set; } = "DISCORD.GG/YOUR SITE";

            [JsonProperty(PropertyName = "Header Color")]
            public string HeaderColor { get; set; } = "0 0 0 .8";

            [JsonProperty(PropertyName = "Header Text Color")]
            public string HeaderTextColor { get; set; } = "1 1 1 1";

            [JsonProperty(PropertyName = "Link Button Color")]
            public string LinkButtonColor { get; set; } = "0 0 0 .8";

            [JsonProperty(PropertyName = "Link Button Text Color")]
            public string LinkButtonTextColor { get; set; } = "1 1 1 .5";
            [JsonProperty(PropertyName = "Unlink Button Color")]
            public string UnlinkButtonColor { get; set; } = "0 0 0 .95";
            [JsonProperty(PropertyName = "Unlink Button Text Color")]
            public string UnlinkButtonTextColor { get; set; } = "1 1 1 .5";

            [JsonProperty(PropertyName = "Link Instructions Text Color")]
            public string LinkInstructionsTextColor { get; set; } = "1 1 1 1";
            [JsonProperty(PropertyName = "UI Backgound Blur")]
            public string LinkPanelBlur { get; set; } = "0 0 0 .4";

            [JsonProperty(PropertyName = "Link Panel Color")]
            public string LinkPanelColor { get; set; } = "0 0 0 .9";

            [JsonProperty(PropertyName = "Main Panel Color")]
            public string MainPanelColor { get; set; } = "0 0 0 .9";
            [JsonProperty(PropertyName = "Discord Panel Text Color")]
            public string DiscordTextColor { get; set; } = ".07 .99 .26 .95";

            [JsonProperty(PropertyName = "Steam Panel Text Color")]
            public string SteamTextColor { get; set; } = ".32 .93 1 .95";

            [JsonProperty(PropertyName = "Unlink Image Url")]
            public string UnlinkImageUrl { get; set; } =
            "https://cdn.discordapp.com/attachments/1134276798427959427/1134328620731600917/Unlink.png";

            [JsonProperty(PropertyName = "Search Header Background Color")]
            public string SearchHeaderColor = "0 0 0 .8";

            [JsonProperty(PropertyName = "Search Row Background Color")]
            public string SearchRowTextColor = "1 1 1 1";

            [JsonProperty(PropertyName = "Search Row Alt Background Color")]
            public string SearchRowAltTextColor = ".38 .53 .95 .95";

            public static Configuration DefaultConfig()
            {
                var config = new Configuration();
                config.RolesToSync.Add("OxideGroupName", "DiscordRoleId");
                config.CommandToLink = new List<string> { "link", "auth" };
                config.CommandToSearch = new List<string> { "dl", "search" };
                config.CommandToUnlink = new List<string> { "unlink", "deauth" };
                return config;
            }
        }
        #endregion

        #region Lang
        private static class PluginMessages
        {
            public const string LinkButtonText = "Link Button Text";
            public const string PlayerLinked = "Player Linked";
            public const string CodeInstructions = "Code Instructions";
            public const string CodeCooldown = "Code generation on cooldown";
            public const string Instructions = "Link Instructions";
            public const string AccountLinkedTitle = "Account Linked Title";
            public const string Unlink = "Unlink";
            public const string Booster = "Booster";
            public const string UnlinkMessage = "UnlinkMessage";
            public const string AccountsNotLinked = "NotLinked";
            public const string LinkedPlayers = "LinkedPlayers";
            public const string NoPermission = "You do not have permission to do this";
            public const string NoUserFound = "No User Found";
            public const string HowToLink = "HOW TO LINK";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [PluginMessages.LinkButtonText] = "G E N E R A T E   C O D E",
                [PluginMessages.PlayerLinked] = "Thank you for linking your accounts.",
                [PluginMessages.CodeInstructions] = "Put the code <color=#66c4ff>{0}</color> into our linking channel in discord",
                [PluginMessages.CodeCooldown] = "Code generation on cooldown",
                [PluginMessages.Instructions] = "To link, please join our discord <color=#66c4ff>discord.gg/foundInLang</color> and put the code that you get into the linking channel or the linking bot's DMs.",
                [PluginMessages.AccountLinkedTitle] = "The Following Accounts Are Linked",
                [PluginMessages.Unlink] = "U N L I N K",
                [PluginMessages.Booster] = "Booster",
                [PluginMessages.UnlinkMessage] = "Your accounts have been unlinked",
                [PluginMessages.AccountsNotLinked] = "You do not have any linked accounts",
                [PluginMessages.LinkedPlayers] = "Search Linked Players",
                [PluginMessages.NoPermission] = "You do not have permission to do this",
                [PluginMessages.NoUserFound] = "No User Found",
                [PluginMessages.HowToLink] = "HOW TO LINK"
            }, this);
        }
        private string GetMsg(string key, object userId = null)
        {
            return lang.GetMessage(key, this, userId?.ToString());
        }
        #endregion

        #region Data Handling
        private List<LinkedPlayer> _storedData;

        private void LoadData()
        {
            try
            {
                _storedData = Interface.GetMod().DataFileSystem.ReadObject<List<LinkedPlayer>>("DiscordLink");
            }
            catch (Exception e)
            {
                Puts(e.Message);
                Puts(e.StackTrace);
                _storedData = new List<LinkedPlayer>();
            }
        }

        private void SaveData() =>  Interface.GetMod().DataFileSystem.WriteObject("DiscordLink", _storedData);

        public class LinkedPlayer
        {
            public bool IsBooster { get; set; }
            public string DiscordId { get; set; }
            public string DiscordImage { get; set; }
            public string DiscordName { get; set; }
            public ulong SteamId { get; set; }
            public string SteamName { get; set; }
            public string SteamAvatar { get; set; }
        }
        #endregion

        #region Constructors
        private const string AllowedChars = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
        private readonly Dictionary<ulong, LinkInfo> _lastCodes = new Dictionary<ulong, LinkInfo>();
        private static readonly Random RandomCode = new Random();
        private List<ulong> _openUIs = new List<ulong>();
        private Dictionary<ulong, SearchConstructor> _searchConstructors = new Dictionary<ulong, SearchConstructor>();

        public class PluginPermissions
        {
            public const string Search = "DiscordLinkBot.Search";
        }

        public class SearchConstructor
        {
            public string Filter { get; set; }
            public int Page { get; set; }
            public LinkedPlayer LinkedPlayer { get; set; }
        }

        public class LinkInfo
        {
            public string Code { get; set; }
            public DateTime CreatedAt { get; set; }
        }
        #endregion

        #region Hooks
        void Init()
        {
            LoadData();
            RegisterCommandsAndPermissions();
        }

        void OnServerSave()
        {
            SaveData();
        }

        void OnServerInitialized(bool initial)
        {
            SendRolesToDiscord();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            LogAction("CheckStatus", new { steamId = player.UserIDString });
        }

        void OnUserGroupAdded(string id, string groupName)
        {
            if (!_config.RolesToSync.ContainsKey(groupName)) return;
            var linkedPlayer = _storedData.FirstOrDefault(x => $"{x.SteamId}" == id);
            if (linkedPlayer == null) return;

            LogAction("RoleChanged", new { discordId = linkedPlayer.DiscordId, roleId = _config.RolesToSync[groupName], added = true });
        }

        void OnUserGroupRemoved(string id, string groupName)
        {
            if (!_config.RolesToSync.ContainsKey(groupName)) return;
            var linkedPlayer = _storedData.FirstOrDefault(x => $"{x.SteamId}" == id);
            if (linkedPlayer == null) return;

            LogAction("RoleChanged", new { discordId = linkedPlayer.DiscordId, roleId = _config.RolesToSync[groupName], added = false });
        }

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
                SaveData();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "DLMainLinkPanel");
            }

            _config = null;
        }
        #endregion

        #region Commands
        void LinkCmd(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if(_config.UseUI) _openUIs.Add(player.userID);
            if (_storedData.Any(x => x.SteamId == player.userID))
            {
                if (_config.UseUI) ShowLinkPanel(player);
                else player.ChatMessage(string.Format(GetMsg(PluginMessages.PlayerLinked, player.userID)));
                return;
            }

            if (_config.UseUI)
            {
                ShowLinkPanel(player);
                return;
            }

            var code = GenerateAuthCode(player);
            player.ChatMessage(string.Format(GetMsg(PluginMessages.CodeInstructions, player.userID), code));
        }

        void SearchCmd(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PluginPermissions.Search))
            {
                player.ChatMessage(GetMsg(PluginMessages.NoPermission));
                return;
            }

            ShowSearchPanel(player);
        }

        void UnlinkCmd(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            UnlinkPlayer(player.UserIDString);

            player.ChatMessage("Unlinking...");
        }

        [ConsoleCommand("dl_main")]
        private void CMDDLMain(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            switch (arg.Args[0])
            {
                case "close":
                    CuiHelper.DestroyUi(player, "DLMainLinkPanel");
                    _openUIs.Remove(player.userID);
                    break;
                case "code":
                    if (GetAuthCode(player) != arg.Args[1]) ShowLinkPanel(player);
                    break;
                case "title":
                    if (string.Join(" ", arg.Args.Skip(1)) != _config.DiscordUrl) ShowLinkPanel(player);
                    break;
                case "desc":
                    if (string.Join(" ", arg.Args.Skip(1)) != PluginMessages.Instructions) CreateLinkingInfo(player);
                    break;
                case "unlink":
                    UnlinkPlayer(player.UserIDString);
                    break;
                case "generatecode":
                    var code = GenerateAuthCode(player);
                    if(code != arg.Args[1]) ShowLinkPanel(player);
                    break;
                case "searchclose":
                    _searchConstructors.Remove(player.userID);
                    CuiHelper.DestroyUi(player, "DLMainSearchPanel");
                    break;
                case "searchpage":
                    var searchPlayer = _searchConstructors[player.userID];
                    searchPlayer.Page = int.Parse(arg.Args[1]);
                    GeneratePlayersUI(player);
                    break;
                case "searchfilter":
                    searchPlayer = _searchConstructors[player.userID];
                    if (arg.Args.Length < 2 || !String.IsNullOrEmpty(arg.Args[1]) || arg.Args[1] != " " || !arg.Args[1].Equals("SEARCH", StringComparison.OrdinalIgnoreCase) || String.Join(" ", arg.Args.Skip(1)).Any(x => !char.IsLetterOrDigit(x))) searchPlayer.Filter = String.Join(" ", arg.Args.Skip(1));
                    else searchPlayer.Filter = null;
                    searchPlayer.Page = 0;
                    GeneratePlayersUI(player);
                    break; 
                case "closeplayer":
                    CuiHelper.DestroyUi(player, "DLMainPlayerPanel");
                    ShowSearchPanel(player);
                    break;
                case "playercopy":
                    break;
                case "info":
                    CreateLinkingInfo(player);
                    break;
                case "infoclose":
                    CuiHelper.DestroyUi(player, "DLMainInfoPanel");
                    break;
                case "forceunlink":
                    UnlinkPlayer(arg.Args[1]);
                    CuiHelper.DestroyUi(player, "DLMainPlayerPanel");
                    ShowSearchPanel(player);
                    break;
                case "checkplayer":
                    CuiHelper.DestroyUi(player, "DLMainSearchPanel");
                    searchPlayer = _searchConstructors[player.userID];
                    searchPlayer.LinkedPlayer = _storedData.FirstOrDefault(x => x.SteamId == ulong.Parse(arg.Args[1]));
                    ShowDetailedPlayer(player);
                    break;
            }
        }
        #endregion

        #region Bot Processors
        [ConsoleCommand("discordLink_updatePlayer")]
        private void UpdatePlayer(ConsoleSystem.Arg arg)
        {
            // Command format - SteamId, DiscordId, IsLinked, IsBooster, DiscordProfilePicture, SteamProfilePicture, DiscordName
            if (arg.Player() != null || arg == null || arg.Args.Length < 6) return ;
            ulong steamId = ulong.Parse(arg.Args[0]);

            var playerInData = _storedData.FirstOrDefault(x => x.SteamId == steamId);
            var rustPlayer = BasePlayer.FindByID(steamId);

            if (rustPlayer == null) return;

            if (playerInData == null)
            {
                playerInData = new LinkedPlayer { SteamId = steamId, SteamName = rustPlayer.displayName };
                _storedData.Add(playerInData);
            } else if(playerInData.SteamName != rustPlayer.displayName)
            {
                playerInData.SteamName = rustPlayer.displayName;
                LogAction("NameUpdated", new { discordId = playerInData.DiscordId, steamName = playerInData.SteamName });
            }

            playerInData.DiscordId = arg.Args[1];
            playerInData.IsBooster = arg.Args[3] == "true";
            playerInData.DiscordName = String.Join(" ", arg.Args.Skip(6));
            if (arg.Args.Length >= 5 && arg.Args[4] != "false") playerInData.DiscordImage = arg.Args[4];
            if (arg.Args.Length >= 6 && arg.Args[5] != "false") playerInData.SteamAvatar = arg.Args[5];

            List<string> needToSyncRoles = permission.GetUserGroups(rustPlayer.UserIDString).Where(x => _config.RolesToSync.ContainsKey(x)).ToList();
            foreach (var role in needToSyncRoles)
            {
                LogAction("RoleChanged", new { discordId = playerInData.DiscordId, roleId = _config.RolesToSync[role], added = true });
            }

            if (arg.Args[2] == "true") permission.AddUserGroup(rustPlayer.UserIDString, _config.LinkedOxideUserGroup);
            else permission.RemoveUserGroup(rustPlayer.UserIDString, _config.LinkedOxideUserGroup);

            if (playerInData.IsBooster) permission.AddUserGroup(rustPlayer.UserIDString, _config.DiscordBoosterUserGroup);
            else permission.RemoveUserGroup(rustPlayer.UserIDString, _config.DiscordBoosterUserGroup);

            if (_openUIs.Contains(rustPlayer.userID)) ShowLinkPanel(rustPlayer);
        }

        [ConsoleCommand("discordLink_roleChanged")]
        private void DiscordRoleChanged(ConsoleSystem.Arg arg)
        {
            // Command format - DiscordId, roleId, wasAdded
            if (arg.Player() != null || arg == null || arg.Args.Length < 3) return;
            LogAction("Test", new { discordId = arg.Args[0] });
            var linkedPlayer = _storedData.FirstOrDefault(x => x.DiscordId == arg.Args[0]);
            if (linkedPlayer == null) return;

            var groupName = _config.RolesToSync.FirstOrDefault(x => x.Value == arg.Args[1]).Key;
            if (string.IsNullOrEmpty(groupName)) return;

            var player = BasePlayer.FindByID(linkedPlayer.SteamId);
            
            if (player != null) 
            {
                if (arg.Args[2] == "true") permission.AddUserGroup(player.UserIDString, groupName);
                else permission.RemoveUserGroup(player.UserIDString, groupName);
            }
        }

        [ConsoleCommand("discordLink_unlinkPlayer")]
        private void UnlinkPlayer(ConsoleSystem.Arg arg)
        {
            // Command format - SteamID
            if (arg == null || arg.Args.Length < 1 || arg.Player() != null) return;
            UnlinkPlayer(arg.Args[0], true);
        }

        [ConsoleCommand("discordLink_getRolesToSync")]
        private void SendRolesToSync(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Player() != null) return;
            SendRolesToDiscord();
        }
        
        #endregion

        #region UI
        private void ShowLinkPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var playerLink = _storedData.FirstOrDefault(x => x.SteamId == player.userID);

            container.Add(new CuiPanel()
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                CursorEnabled = true,
                Image = { Color = _config.LinkPanelBlur, Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", "DLMainLinkPanel");

            var mainPanel = CreatePanel(ref container, ".3 .3", ".7 .7", _config.LinkPanelColor, "DLMainLinkPanel");
            var topLabel = CreateInput(ref container, "0 .85", "1 1", "dl_main title", _config.HeaderColor, _config.HeaderTextColor, _config.DiscordUrl, 25, TextAnchor.MiddleCenter, mainPanel);
            CreateButton(ref container, ".93 0", "1 .98", "1 1 1 0", "1 1 1 1", "X", 25, "dl_main close", topLabel);
            CreateButton(ref container, ".87 0", ".92 .98", "1 1 1 0", "1 1 1 1", "?", 25, "dl_main info", topLabel);

            if (playerLink != null)
            {
                CreateLabel(ref container, "0 .73", ".5 .85", "0 0 0 0", _config.DiscordTextColor, $"Discord: {playerLink.DiscordName}", 17, TextAnchor.MiddleCenter, mainPanel);
                CreateImagePanel(ref container, ".085 .12", ".43 .73", playerLink.DiscordImage.Contains(".gif") ? _config.DefaultAvatarUrl : playerLink.DiscordImage, mainPanel);
                CreateLabel(ref container, "0 0", ".5 .12", "0 0 0 0", _config.DiscordTextColor, playerLink.DiscordId, 17, TextAnchor.MiddleCenter, mainPanel);

                CreateLabel(ref container, ".5 .73", "1 .85", "0 0 0 0", _config.SteamTextColor, $"Steam: {playerLink.SteamName}", 17, TextAnchor.MiddleCenter, mainPanel);
                CreateImagePanel(ref container, ".57 .12", ".915 .73", playerLink.SteamAvatar, mainPanel);
                CreateLabel(ref container, ".5 0", "1 .12", "0 0 0 0", _config.SteamTextColor, $"{playerLink.SteamId}", 17, TextAnchor.MiddleCenter, mainPanel);

                var unlinkButton = CreateButton(ref container, "0 -.13", ".997 -.005", _config.UnlinkButtonColor, _config.LinkButtonTextColor, GetMsg(PluginMessages.Unlink, player.userID), 30, "dl_main unlink", mainPanel);
                if(!string.IsNullOrEmpty(_config.UnlinkImageUrl)) CreateImagePanel(ref container, ".005 .05", ".065 .95", _config.UnlinkImageUrl, unlinkButton);
            }
            else
            {
                var code = GetAuthCode(player);
                CreateInput(ref container, ".1 .35", ".9 .75", "dl_main code", "0 0 0 .8", _config.CodeColor, code, 60, TextAnchor.MiddleCenter, mainPanel);
                CreateButton(ref container, ".1 .1", ".9 .32", _config.LinkButtonColor, _config.LinkButtonTextColor, GetMsg(PluginMessages.LinkButtonText, player.userID), 30, $"dl_main generatecode {code}", mainPanel);
            }

            CuiHelper.DestroyUi(player, "DLMainLinkPanel");
            CuiHelper.AddUi(player, container);

            if (_config.AutoInstructions) CreateLinkingInfo(player);
        }

        private void CreateLinkingInfo(BasePlayer player)
        {
            var container = new CuiElementContainer();

            var mainPanel = CreatePanel(ref container, ".71 .3", ".9 .7", _config.LinkPanelColor, "DLMainLinkPanel", "DLMainInfoPanel");
            CreateLabel(ref container, "0 .85", "1 1", _config.HeaderColor, _config.HeaderTextColor, GetMsg(PluginMessages.HowToLink), 25, TextAnchor.MiddleCenter, mainPanel);
            CreateButton(ref container, ".93 .85", ".98 1", "1 1 1 0", "1 1 1 1", "<", 25, "dl_main infoclose", mainPanel);

            CreateLabel(ref container, ".05 .05", ".95 .8", "1 1 1 0", "1 1 1 1", GetMsg(PluginMessages.Instructions), 15, TextAnchor.UpperLeft, mainPanel);

            CuiHelper.DestroyUi(player, "DLMainInfoPanel");
            CuiHelper.AddUi(player, container);
        }

        private void ShowErrUI(BasePlayer player)
        {
            var container = new CuiElementContainer();

            var DLErrorPanel = CreateLabel(ref container, ".3 .23", ".7 .29", ".96 .16 .16 .9", "1 1 1 1", $" {GetMsg(PluginMessages.CodeCooldown)}", 20, TextAnchor.MiddleCenter, "DLMainLinkPanel", "DLError");

            CuiHelper.DestroyUi(player, "DLError");
            CuiHelper.AddUi(player, container);

            timer.Once(2, () => CuiHelper.DestroyUi(player, DLErrorPanel));
        }

        private void ShowSearchPanel(BasePlayer player)
        {
            if (!_searchConstructors.ContainsKey(player.userID)) _searchConstructors.Add(player.userID, new SearchConstructor());
            var container = new CuiElementContainer();

            container.Add(new CuiPanel()
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                CursorEnabled = true,
                Image = { Color = "0 0 0 .2", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", "DLMainSearchPanel");

            CreatePanel(ref container, ".025 .05", ".975 .95", "0 0 0 .85", "DLMainSearchPanel", "DLMainSearchScreen");
            var panelLabel = CreateLabel(ref container, "0 .9", "1 1", "0 0 0 .6", "1 1 1 1", "DISCORD LINK", 40, TextAnchor.MiddleCenter, "DLMainSearchScreen");
            CreateButton(ref container, ".93 0", "1 1", "0 0 0 0", "1 1 1 1", "X", 45, "dl_main searchclose", panelLabel);

            CuiHelper.DestroyUi(player, "DLMainSearchPanel");
            CuiHelper.AddUi(player, container);

            GeneratePlayersUI(player);
        }

        private void GeneratePlayersUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var overlayPanel = CreatePanel(ref container, "0 0", "1 .9", "0 0 0 0", "DLMainSearchScreen", "DLMainSearchScreenOverlay");

            var i = 0;
            var row = 0;
            List<LinkedPlayer> linkedList = new List<LinkedPlayer>(_storedData.Select(link => new LinkedPlayer
            {
                DiscordId = link.DiscordId,
                SteamId = link.SteamId,
                DiscordImage = link.DiscordImage,
                DiscordName = link.DiscordName,
                IsBooster = link.IsBooster,
                SteamAvatar = link.SteamAvatar,
                SteamName = link.SteamName,
            }));

            var theUser = _searchConstructors[player.userID];
            var filter = theUser.Filter;
            if (filter != null) linkedList = linkedList.Where(x => x.DiscordName.Contains(filter, System.Globalization.CompareOptions.IgnoreCase) || x.SteamName.Contains(filter, System.Globalization.CompareOptions.IgnoreCase) || x.SteamId.Equals(filter) || x.DiscordId.Equals(filter)).ToList();

            var maxPage = linkedList.Count() / 22;
            if (theUser.Page > maxPage) theUser.Page = 0;
            if (theUser.Page < 0) theUser.Page = maxPage;

            CreateInput(ref container, "0 -.055", ".2 -.007", $"dl_main searchfilter", "0 0 0 .85", "1 1 1 1", theUser.Filter != null ? theUser.Filter : "SEARCH", 20, TextAnchor.MiddleCenter, "DLMainSearchScreenOverlay");
            CreateLabel(ref container, ".4035 -.055", ".597 -.005", "0 0 0 .85", "1 1 1 1", $"{theUser.Page} / {maxPage}", 20, TextAnchor.MiddleCenter, "DLMainSearchScreenOverlay");
            if(maxPage > 0)
            {
                CreateButton(ref container, ".35 -.055", ".4 -.007", "0 0 0 .85", "1 1 1 1", "<", 20, $"dl_main searchpage {theUser.Page - 1}", "DLMainSearchScreenOverlay");
                CreateButton(ref container, ".6 -.055", ".65 -.007", "0 0 0 .85", "1 1 1 1", ">", 20, $"dl_main searchpage {theUser.Page + 1}", "DLMainSearchScreenOverlay");
            }

            foreach (var link in linkedList.Skip(theUser.Page * 22).Take(22))
            {
                if (i == 11)
                {
                    row = 1;
                    i = 0;
                }

                var linkPanel = CreatePanel(ref container, $"{.003 + (row * .50)} {.91 - (i * .09)}", $"{.495 + (row * .50)} {.99 - (i * .09)}", "0 0 0 .5", overlayPanel);
                CreateImagePanel(ref container, ".005 .05", ".075 .93", link.SteamAvatar, linkPanel);
                CreateLabel(ref container, ".085 .5", ".45 1", "0 0 0 0", _config.SteamTextColor, link.SteamName, 15, TextAnchor.MiddleLeft, linkPanel);
                CreateLabel(ref container, ".085 0", ".45 .5", "0 0 0 0", _config.SteamTextColor, $"{link.SteamId}", 15, TextAnchor.MiddleLeft, linkPanel);

                CreateImagePanel(ref container, ".46 .05", ".53 .93", link.DiscordImage.Contains(".gif") ? _config.DefaultAvatarUrl : link.DiscordImage, linkPanel);
                CreateLabel(ref container, ".54 .5", ".905 1", "0 0 0 0", _config.DiscordTextColor, link.DiscordName, 15, TextAnchor.MiddleLeft, linkPanel);
                CreateLabel(ref container, ".54 0", ".905 .5", "0 0 0 0", _config.DiscordTextColor, $"{link.DiscordId}", 15, TextAnchor.MiddleLeft, linkPanel);

                CreateButton(ref container, ".925 .06", ".991 .9", ".51 .68 .97 .4", "1 1 1 1", "+", 25, $"dl_main checkplayer {link.SteamId}", linkPanel);
                i++;
            }

            CuiHelper.DestroyUi(player, "DLMainSearchScreenOverlay");
            CuiHelper.AddUi(player, container);
        }

        private void ShowDetailedPlayer(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var info = _searchConstructors[player.userID];

            container.Add(new CuiPanel()
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                CursorEnabled = true,
                Image = { Color = "0 0 0 .4", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", "DLMainPlayerPanel");

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 .5", "DLMainPlayerPanel", "DLMainSearchDetail", true);
            CreatePanel(ref container, "0 0", "1 1", "0 0 0 .7", panel);
            var mainPanel = CreatePanel(ref container, ".25 .3", ".75 .7", "0 0 0 .7", panel);
            CreateLabel(ref container, "0 .9", "1 1", "0 0 0 .5", "1 1 1 1", "DISCORD LINK", 25, TextAnchor.MiddleCenter, mainPanel);
            CreateButton(ref container, ".93 .9", "1 1", "0 0 0 0", "1 1 1 1", "X", 25, "dl_main closeplayer", mainPanel);

            CreateLabel(ref container, "0 .8", ".42 .9", "0 0 0 0", "1 1 1 1", info.LinkedPlayer.SteamName, 20, TextAnchor.MiddleCenter, mainPanel);
            CreateImagePanel(ref container, ".05 .1", ".37 .8", info.LinkedPlayer.SteamAvatar, mainPanel);
            CreateInput(ref container, "0 .005", ".42 .1", "dl_main playercopy", "0 0 0 0", "1 1 1 1", $"{info.LinkedPlayer.SteamId}", 20, TextAnchor.MiddleCenter, mainPanel);

            if (info.LinkedPlayer.IsBooster) CreateImagePanel(ref container, ".42 .33", ".58 .68", "https://cdn.discordapp.com/attachments/670451699063980083/1207819837393342524/7791-server-boost.png?ex=65e108e1&is=65ce93e1&hm=d59716f5d7389bcdddce118539fb9cc65bbdf14a07b929a9f967f0fbe3f15df9&", mainPanel);

            CreateButton(ref container, ".39 .1", ".61 .2", "0 0 0 .6", "1 1 1 .8", "UNLINK", 25, $"dl_main forceunlink {info.LinkedPlayer.SteamId}", mainPanel);

            CreateLabel(ref container, ".63 .8", ".95 .9", "0 0 0 0", "1 1 1 1", info.LinkedPlayer.DiscordName, 20, TextAnchor.MiddleCenter, mainPanel);
            CreateImagePanel(ref container, ".63 .1", ".95 .8", info.LinkedPlayer.DiscordImage.Contains(".gif") ? _config.DefaultAvatarUrl : info.LinkedPlayer.DiscordImage, mainPanel);
            CreateInput(ref container, ".63 .005", ".95 .1", "dl_main playercopy", "0 0 0 0", "1 1 1 1", $"{info.LinkedPlayer.DiscordId}", 20, TextAnchor.MiddleCenter, mainPanel);

            CuiHelper.DestroyUi(player, "DLMainSearchDetail");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Methods
        private void SendRolesToDiscord() => LogAction("RolesToSync", new { rolesToSync = _config.RolesToSync.Select(x => x.Value).Distinct().ToList() });

        private void UnlinkPlayer(string playerId, bool fromDiscord = false)
        {
            if (fromDiscord) FinishUnlink(playerId);
            else LogAction("VerifyUnlink", new { SteamID = playerId });
        }

        private void FinishUnlink(string playerId)
        {
            ulong steamId;
            if (!ulong.TryParse(playerId.ToString(), out steamId)) return;

            if (permission.UserHasGroup(playerId, _config.LinkedOxideUserGroup)) permission.RemoveUserGroup(playerId, _config.LinkedOxideUserGroup);
            if (permission.UserHasGroup(playerId, _config.DiscordBoosterUserGroup)) permission.RemoveUserGroup(playerId, _config.DiscordBoosterUserGroup);

            var linkedPlayer = _storedData.FirstOrDefault(x => x.SteamId == steamId);
            if (linkedPlayer != null) _storedData.Remove(linkedPlayer);

            LogAction("Unlinked", new { steamId = playerId });

            var player = BasePlayer.FindByID(steamId);
            if (player == null) return;

            player.ChatMessage(linkedPlayer != null ? GetMsg(PluginMessages.UnlinkMessage, player.userID) : GetMsg(PluginMessages.AccountsNotLinked, player.userID));

            if (_openUIs.Contains(player.userID)) ShowLinkPanel(player);
        }

        private void RegisterCommandsAndPermissions()
        {
            foreach (var chatCmd in _config.CommandToLink)
                cmd.AddChatCommand(chatCmd, this, LinkCmd);

            foreach (var chatCmd in _config.CommandToSearch)
                cmd.AddChatCommand(chatCmd, this, SearchCmd);

            foreach (var chatCmd in _config.CommandToUnlink)
                cmd.AddChatCommand(chatCmd, this, UnlinkCmd);

            permission.RegisterPermission(PluginPermissions.Search, this);
        }

        private string GetAuthCode(BasePlayer player)
        {
            if (_lastCodes.ContainsKey(player.userID))
            {
                return _lastCodes[player.userID].Code;
            } else
            {
                return GenerateAuthCode(player);
            }
        }

        private string GenerateAuthCode(BasePlayer player)
        {
            if (_lastCodes.ContainsKey(player.userID) && _lastCodes[player.userID].CreatedAt.AddSeconds(_config.CodeCooldown) > DateTime.Now)
            {
                if (_openUIs.Contains(player.userID)) ShowErrUI(player);
                return _lastCodes[player.userID].Code;
            }

            var chars = new char[6];
            for (var i = 0; i < 6; i++)
            {
                chars[i] = AllowedChars[RandomCode.Next(AllowedChars.Length)];
            }

            var code = new string(chars);
            _lastCodes[player.userID] = new LinkInfo { Code = code, CreatedAt = DateTime.Now };
            LogAction("Generated", new { userId = $"{player.UserIDString}", player.displayName, code });

            return code;
        }

        private void LogAction(string action, object messageObject) => RCon.Broadcast(RCon.LogType.Generic, $"DiscordLink.{action}||{JsonConvert.SerializeObject(messageObject)}");
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
