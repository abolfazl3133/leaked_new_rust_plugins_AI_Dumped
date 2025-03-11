using System;
using ConVar;
using Newtonsoft.Json;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;
using Oxide.Core.Libraries;
using Oxide.Plugins.AutomatedMessagesMethods;

/*2.1.0
 * Added "Send as game tip" option for replies + ability to preview game tip in /am.edit
 * Added "Send in chat" option (true by default)
 * Fix for inaccurate grid {hacklocation}
 * Added support for Toxic Village when using {hacklocation}
*/

namespace Oxide.Plugins
{
    [Info("AutomatedMessages", "beee", "2.1.0")]
    [Description("Automated chat messages based on triggers or repeating interval.")]
    class AutomatedMessages : RustPlugin
    {
        #region Fields

        private static AutomatedMessages _plugin;
        private PluginConfig _config;
        private List<Timer> _timers;

        public const string PREFIX_SHORT = "am";
        public const string PREFIX_LONG = "automatedmessages";
        public const string PERM_ADMIN = $"{PREFIX_LONG}.admin";

        private Dictionary<Regex, PluginConfig.ConfigAction> _autoReplyPatterns;

        [PluginReference]
        private Plugin PlaceholderAPI;

        #endregion

        #region Load/Unload Hooks

        private void Init()
        {
            UnsubscribeHooks();
        }

        void OnServerInitialized()
        {
            pData = PlayersData.Load();
            ProcessConfig();
            RegisterCommand(_config.ToggleCommand, nameof(TipsToggleChatCMD));
            RegisterPermissions();
            InitTimers();
            InitAutoReply();
            SubscribeHooks();
            RegisterConfigCommands();
            SetUnsetCountries();
            RegisterCommand($"{PREFIX_SHORT}.ui.texteditor", nameof(LongInputCMD));
        }

        void Loaded()
        {
            _plugin = this;
            _timers = new();
        }

        void Unload()
        {
            DestroyTimers();
            SaveData();

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, uimodal);
                CuiHelper.DestroyUi(player, chatpreview);
                CuiHelper.DestroyUi(player, gametippreview);
            }

            _plugin = null;
            _timers = null;
        }

        #endregion

        #region Functions

        void RegisterPermissions()
        {
            permission.RegisterPermission(PERM_ADMIN, this);
        }

        private void InitTimers()
        {
            DestroyTimers();
            _timers = new();

            int count = 0;
            foreach (var message in _config.Actions.FindAll(s => s.IsEnabled() && s.Type == "Timed"))
            {
                Lang_Action defaultLangAction = message.CachedLangActions[_config.DefaultLang];
                if (defaultLangAction.Replies == null || defaultLangAction.Replies.Count == 0)
                    continue;

                if (message.Interval <= 0)
                    continue;

                Timer timer = _plugin.timer.Repeat(
                    message.Interval * 60,
                    0,
                    () =>
                    {
                        ProcessReply(message);
                    }
                );
                _timers.Add(timer);
                count++;
            }
            PrintWarning($"Started {count} chat timer{((count == 1) ? "" : "s")}.");
        }

        private void InitAutoReply()
        {
            _autoReplyPatterns = new();

            int count = 0;
            foreach (var message in _config.Actions.FindAll(s => s.IsEnabled() && s.Type == "AutoReply"))
            {
                Lang_Action defaultLangAction = message.CachedLangActions[_config.DefaultLang];
                if (defaultLangAction.Replies == null || defaultLangAction.Replies.Count == 0)
                    continue;

                if (string.IsNullOrEmpty(message.Target))
                    continue;

                foreach (var set in message.Target.Split('|'))
                {
                    string regexPattern = "";

                    foreach (var keyword in set.Split(','))
                        regexPattern += $"(?=.*{(keyword.StartsWith("!") ? "^!" : "")}\\b{keyword.TrimStart('!').Trim()}\\b)";

                    if (regexPattern == "")
                        continue;

                    regexPattern = $"^{regexPattern}.*$";

                    _autoReplyPatterns.Add(new (regexPattern, RegexOptions.IgnoreCase), message);
                    count++;
                }
            }
            PrintWarning($"Cached {count} auto reply keyword pattern{((count == 1) ? "" : "s")}.");
        }

        List<string> _registeredConfigCommands;
        private void RegisterConfigCommands()
        {
            _registeredConfigCommands = new();
            int count = 0;
            foreach (var message in _config.Actions.FindAll(s => s.IsEnabled() && s.Type == "ChatCommand"))
            {
                Lang_Action defaultLangAction = message.CachedLangActions[_config.DefaultLang];
                if (defaultLangAction.Replies == null || defaultLangAction.Replies.Count == 0)
                    continue;

                if (string.IsNullOrEmpty(message.Target))
                    continue;

                string command = message.Target.Replace("/", "").Trim().ToLower();

                foreach (string com in command.Split(',').Select(s => s.Trim()))
                {
                    if (string.IsNullOrEmpty(com))
                        continue;

                    RegisterCommand(com, nameof(CustomChatCMD));
                    _registeredConfigCommands.Add(com);

                    count++;
                }
            }
            PrintWarning($"Registered {count} chat command{((count == 1) ? "" : "s")}.");
        }

        private void UnregisterCachedCommands()
        {
            if (_registeredConfigCommands == null) return;

            Covalence library = GetLibrary<Covalence>();
            foreach (var command in _registeredConfigCommands)
                library.UnregisterCommand(command, this);

            _registeredConfigCommands.Clear();
        }

        private void RegisterCommand(string command, string callback, string perm = null)
        {
            if (!string.IsNullOrEmpty(command) && !command.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(perm))
                    AddCovalenceCommand(command, callback);
                else
                    AddCovalenceCommand(command, callback, perm);
            }
        }

        private void SetUnsetCountries()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                PlayerInfo pinfo;
                bool isNew = !pData.Players.TryGetValue(player.userID, out pinfo);

                if (isNew)
                    pData.Players.Add(player.userID, pinfo = new PlayerInfo() { TipsActive = true, isNew = true });

                if (string.IsNullOrEmpty(pinfo.Country))
                    SetPlayerCountry(player, pinfo);
            }
        }

        private void DestroyTimers()
        {
            foreach (Timer timer in _timers)
                if (timer != null)
                    timer.Destroy();
        }

        private void ProcessReplies(BasePlayer player, List<string> types, string target = "", bool onlySendToTeam = false)
        {
            if (!player.userID.IsSteamId()) return;

            PlayerInfo pinfo;

            bool isNew = !pData.Players.TryGetValue(player.userID, out pinfo);

            if (isNew)
                pData.Players.Add(player.userID, pinfo = new PlayerInfo() { TipsActive = true, isNew = true });

            foreach (var action in _config.Actions.FindAll(s => s.IsEnabled() && types.Contains(s.Type)))
            {
                Lang_Action defaultLangAction = action.CachedLangActions[_config.DefaultLang];
                if (defaultLangAction.Replies == null || defaultLangAction.Replies.Count == 0)
                    continue;

                if ((action.PlayerCanDisable && !pinfo.TipsActive) || !MessageIsEligible(action, player.UserIDString))
                    continue;

                bool toSend = false;

                if(AMTriggers.TryGetValue(action.Type, out AMTrigger amTrigger))
                {
                    if (amTrigger.RequiresTarget)
                    {
                        if (string.IsNullOrEmpty(target)) break;

                        switch (amTrigger.Key)
                        {
                            case "ChatCommand":
                                if (action.Target.Replace("/", "").ToLower().Split(',').Any(x => x.Trim() == target))
                                    toSend = true;
                                break;
                            default:
                                if(action.Target == target)
                                    toSend = true;
                                break;
                        }
                    }
                    else
                    {
                        switch (amTrigger.Key)
                        {
                            case "NewPlayerJoined":
                                if (pinfo.isNew)
                                    toSend = true;
                                break;
                            case "PlayerConnected":
                                if (pinfo.justJoined)
                                    toSend = true;
                                break;
                            case "PlayerDead":
                                if (pinfo.wasDead && ((target == "" && !action.IsGlobalBroadcast) || (target == "FromEntityDeath" && action.IsGlobalBroadcast)))
                                {
                                    pinfo.wasDead = false;
                                    toSend = true;
                                }
                                break;
                            default:
                                toSend = true;
                                break;
                        }
                    }

                    if (amTrigger.UsesGenericCooldown && action.OnCooldownAll)
                        toSend = false;

                    if (amTrigger.UsesPlayerCooldown && action.OnCooldown != null && action.OnCooldown.Contains(player.userID))
                        toSend = false;
                }

                if (toSend)
                {
                    if (action.IsGlobalBroadcast)
                        ProcessReply(action, player, onlySendToTeam: onlySendToTeam);
                    else
                    {
                        string playerLanguage = "";
                        Lang_Action langAction = null;
                        string reply = string.Empty;
                        if (Lang_TryGetAction(player, action.Type, action.Id, out playerLanguage, out langAction) && langAction.Replies.Count > 0)
                        {
                            if (langAction.ReplyIndex >= langAction.Replies.Count)
                                langAction.ReplyIndex = 0;

                            reply = langAction.Replies[langAction.ReplyIndex];
                            IncrementReplyIndex(ref langAction);
                        }
                        else
                        {
                            langAction = action.CachedLangActions[_config.DefaultLang];

                            if (langAction.ReplyIndex >= langAction.Replies.Count)
                                langAction.ReplyIndex = 0;

                            reply = langAction.Replies[langAction.ReplyIndex];
                            IncrementReplyIndex(ref langAction);
                        }

                        reply = FormatMessage(player, reply);

                        timer.Once(0.5f, () =>
                        {
                            SendMessage(player, reply, action.SendInChat, action.SendAsGameTip);
                        });
                    }

                    switch(action.Type)
                    {
                        case "AutoReply":
                            action.RunCooldownAll(_config.AutoReplyCooldown);
                            break;
                        case "ChatCommand":
                            action.RunCooldownAll(_config.ChatCommandCooldown);
                            break;
                        case "EnteredZone":
                            action.RunCooldownPlayer(player, _config.ZoneManagerCooldown);
                            break;
                        case "LeftZone":
                            action.RunCooldownPlayer(player, _config.ZoneManagerCooldown);
                            break;
                        case "EnteredMonument":
                            action.RunCooldownPlayer(player, _config.MonumentWatcherCooldown);
                            break;
                        case "LeftMonument":
                            action.RunCooldownPlayer(player, _config.MonumentWatcherCooldown);
                            break;
                    }
                }
            }

            pinfo.isNew = false;
            pinfo.justJoined = false;
        }

        private void ProcessReply(PluginConfig.ConfigAction action, BasePlayer triggerOwner = null, Lang_Action defaultLangAction = null, bool onlySendToTeam = false)
        {
            if(defaultLangAction == null)
                defaultLangAction = action.CachedLangActions[_config.DefaultLang];

            if (defaultLangAction.Replies == null || defaultLangAction.Replies.Count == 0)
                return;
            if (triggerOwner != null && triggerOwner.IsAdmin && action.DontTriggerAdmin)
                return;

            List<string> incrementedLangIndex = Facepunch.Pool.Get<List<string>>();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if(onlySendToTeam && triggerOwner.currentTeam != 0 && triggerOwner.currentTeam != player.currentTeam) continue;

                string playerLanguage = "";

                PlayerInfo pinfo;

                if (!pData.Players.TryGetValue(player.userID, out pinfo))
                    pData.Players.Add(player.userID, pinfo = new PlayerInfo() { TipsActive = true, isNew = true });

                if ((action.PlayerCanDisable && !pinfo.TipsActive) || !MessageIsEligible(action, player.UserIDString))
                    continue;

                string reply = string.Empty;

                if (Lang_TryGetAction(player, action.Type, action.Id, out playerLanguage, out Lang_Action playerlangAction) && playerlangAction.Replies.Count > 0)
                {
                    if (playerlangAction.ReplyIndex >= playerlangAction.Replies.Count)
                        playerlangAction.ReplyIndex = 0;

                    reply = playerlangAction.Replies[playerlangAction.ReplyIndex];

                    if (!incrementedLangIndex.Contains(playerLanguage))
                    {
                        IncrementReplyIndex(ref playerlangAction);
                        incrementedLangIndex.Add(playerLanguage);
                    }
                }
                else
                {
                    if (defaultLangAction.ReplyIndex >= defaultLangAction.Replies.Count)
                        defaultLangAction.ReplyIndex = 0;

                    reply = defaultLangAction.Replies[defaultLangAction.ReplyIndex];

                    if (!incrementedLangIndex.Contains(_config.DefaultLang))
                    {
                        IncrementReplyIndex(ref defaultLangAction);
                        incrementedLangIndex.Add(_config.DefaultLang);
                    }
                }

                reply = FormatMessage((triggerOwner == null) ? player : triggerOwner, reply);

                timer.Once(0.5f, () =>
                {
                    SendMessage(player, reply, action.SendInChat, action.SendAsGameTip);
                });
            }

            Facepunch.Pool.FreeUnmanaged<string>(ref incrementedLangIndex);
        }

        private void IncrementReplyIndex(ref Lang_Action lang_Action)
        {
            lang_Action.ReplyIndex++;
            if (lang_Action.ReplyIndex >= lang_Action.Replies.Count || lang_Action.ReplyIndex < 0)
                lang_Action.ReplyIndex = 0;
        }

        List<string> availableVariables = new List<string>() { "{playername}", "{playerid}", "{playercountry}", "{hacklocation}", "{wipetimeremaining}", "{online}", "{sleeping}", "{joining}" };

        private string FormatMessage(BasePlayer player, string message)
        {
            StringBuilder builder = new StringBuilder(message);

            builder.Replace("{playername}", player.displayName)
                .Replace("{playerid}", player.UserIDString)
                .Replace("{online}", $"{BasePlayer.activePlayerList.Count}")
                .Replace("{sleeping}", $"{BasePlayer.sleepingPlayerList.Count}")
                .Replace("{joining}", $"{ServerMgr.Instance.connectionQueue.Joining + ServerMgr.Instance.connectionQueue.Queued}");

            if (message.Contains("{playercountry}"))
            {
                PlayerInfo pinfo;
                bool found = pData.Players.TryGetValue(player.userID, out pinfo);

                if (found && !string.IsNullOrEmpty(pinfo.Country))
                    builder.Replace("{playercountry}", pinfo.Country);
                else
                    builder.Replace("{playercountry}", "");
            }

            if (message.Contains("{wipetimeremaining}") && WipeTimer.serverinstance != null)
            {
                DateTimeOffset nowTime = DateTimeOffset.UtcNow;
                nowTime = nowTime.AddDays((double)WipeTimer.daysToAddTest);
                nowTime = nowTime.AddHours((double)WipeTimer.hoursToAddTest);

                DateTimeOffset wipeTime = WipeTimer.serverinstance.GetWipeTime(nowTime);
                TimeSpan timeSpan = wipeTime - nowTime;

                builder.Replace("{wipetimeremaining}", FormatTime(timeSpan, m: false, s: false));
            }

            if (message.Contains("{hacklocation}"))
            {
                PlayerInfo pinfo;
                bool found = pData.Players.TryGetValue(player.userID, out pinfo);

                if (found && !string.IsNullOrEmpty(pinfo.HackLocation))
                    builder.Replace("{hacklocation}", pinfo.HackLocation);
                else
                    builder.Replace("{hacklocation}", GetGrid(player.transform.position));
            }

            if (PlaceholderAPI)
            {
                PlaceholderAPI?.CallHook("ProcessPlaceholders", player.IPlayer, builder);
            }

            return builder.ToString();
        }
        
        //Credit @Substrata
        string GetGrid(Vector3 pos)
        {
            float worldSize = TerrainMeta.Size.x;
            int cellCount = Mathf.FloorToInt((worldSize * 7) / 1024);
            float cellSize = worldSize / cellCount;
            
            int x = Mathf.FloorToInt((pos.x + (worldSize / 2)) / cellSize);
            int z = Mathf.FloorToInt((pos.z + (worldSize / 2)) / cellSize);
            
            char zChar = (char)('A' + (x % 26));
            string columnLabel = x < 26 ? zChar.ToString() : $"{(char)('A' + x / 26 - 1)}{zChar}";

            int row = cellCount - 1 - z;

            return $"{columnLabel}{row}";
        }

        #endregion

        #region Hooks

        void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnPlayerChat));
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(CanHackCrate));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnUserPermissionGranted));
            Unsubscribe(nameof(OnUserPermissionRevoked));
            Unsubscribe(nameof(OnUserGroupAdded));
            Unsubscribe(nameof(OnUserGroupRemoved));
            Unsubscribe(nameof(OnEnterZone));
            Unsubscribe(nameof(OnExitZone));
            Unsubscribe(nameof(OnPlayerEnteredMonument));
            Unsubscribe(nameof(OnPlayerExitedMonument));
        }

        void SubscribeHooks()
        {
            foreach (var trigger in AMTriggers)
            {
                if (string.IsNullOrEmpty(trigger.Value.Hooks))
                    continue;

                if (_config.Actions.Any(s => s.Type == trigger.Key && s.IsEnabled()))
                    foreach (string hook in trigger.Value.Hooks.Split(','))
                        Subscribe(hook);
            }
        }

        private void OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel chatchannel)
        {
            if (chatchannel != Chat.ChatChannel.Global && chatchannel != Chat.ChatChannel.Team) return;

            foreach (var keywords in _autoReplyPatterns.Where(regex => regex.Key.Match(message).Success))
            {
                NextTick(() =>
                {
                    ProcessReplies(player, new List<string>() { "AutoReply" }, (keywords.Value as PluginConfig.ConfigAction).Target, chatchannel == Chat.ChatChannel.Team && _config.BroadcastToTeamOnly);
                });
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!player.userID.IsSteamId()) return;

            PlayerInfo pinfo;
            if (pData.Players.TryGetValue(player.userID, out pinfo))
            {
                pinfo.justJoined = true;
            }
            else
            {
                pData.Players.Add(player.userID, pinfo = new PlayerInfo() { TipsActive = true, isNew = true, justJoined = !HasActiveNewPlayerJoinedAction });
            }

            if (string.IsNullOrEmpty(pinfo.Country))
                SetPlayerCountry(player, pinfo);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null)
                ProcessReplies(player, new List<string>() { "PlayerDisconnected" });
        }

        private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player != null && player.userID.IsSteamId())
            {
                string Location = GetGrid(crate.transform.position);

                if (OnCargoShip(crate))
                    Location = "Cargo Ship";
                else
                {
                    MonumentRef monument = GetMonumentFromPosition(crate.transform.position);

                    if (monument != null)
                        Location = $"{monument.DisplayName} ({Location})";
                }

                PlayerInfo pinfo;
                if (pData.Players.TryGetValue(player.userID, out pinfo))
                {
                    pinfo.HackLocation = Location;
                }
                else
                {
                    pData.Players.Add(player.userID, pinfo = new PlayerInfo() { TipsActive = true, HackLocation = Location });
                }

                ProcessReplies(player, new List<string>() { "CrateHacked" });
            }
        }

        private void OnEntityDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || !player.userID.IsSteamId())
                return;

            PlayerInfo pinfo;
            if (pData.Players.TryGetValue(player.userID, out pinfo))
            {
                pinfo.wasDead = true;
                ProcessReplies(player, new List<string>() { "PlayerDead" }, "FromEntityDeath");
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player.IsDead() || !player.IsConnected) return;
            ProcessReplies(player, new List<string>() { "NewPlayerJoined", "PlayerConnected", "PlayerDead" });
        }

        private void OnUserPermissionGranted(string id, string permName)
        {
            var player = BasePlayer.FindByID(Convert.ToUInt64(id));
            if (player != null)
                ProcessReplies(player, new List<string>() { "PermissionGranted" }, permName);
        }

        private void OnUserPermissionRevoked(string id, string permName)
        {
            var player = BasePlayer.FindByID(Convert.ToUInt64(id));
            if (player != null)
                ProcessReplies(player, new List<string>() { "PermissionRevoked" }, permName);
        }

        private void OnUserGroupAdded(string userId, string groupName)
        {
            var player = BasePlayer.FindByID(Convert.ToUInt64(userId));
            if (player != null)
                ProcessReplies(player, new List<string>() { "AddedToGroup" }, groupName);
        }

        private void OnUserGroupRemoved(string userId, string groupName)
        {
            var player = BasePlayer.FindByID(Convert.ToUInt64(userId));
            if (player != null)
                ProcessReplies(player, new List<string>() { "RemovedFromGroup" }, groupName);
        }

        private void OnEnterZone(string ZoneID, BasePlayer player)
        {
            if (player != null)
                ProcessReplies(player, new List<string>() { "EnteredZone" }, ZoneID);
        }

        private void OnExitZone(string ZoneID, BasePlayer player)
        {
            if (player != null)
                ProcessReplies(player, new List<string>() { "LeftZone" }, ZoneID);
        }

        private void OnPlayerEnteredMonument(string monumentID, BasePlayer player, string type, string oldMonumentID)
        {
            if (player != null)
                ProcessReplies(player, new List<string>() { "EnteredMonument" }, monumentID);
        }

        private void OnPlayerExitedMonument(string monumentID, BasePlayer player, string type, string reason, string newMonumentID)
        {
            if (player != null)
                ProcessReplies(player, new List<string>() { "LeftMonument" }, monumentID);
        }

        #endregion

        #region Commands

        private void TipsToggleChatCMD(IPlayer user, string command, string[] args)
        {
            var player = user.Object as BasePlayer;
            if (player == null)
                return;

            bool isActive = pData.ToggleTipsActive(player.userID);

            if (isActive)
                SendMessage(player, lang.GetMessage("toggle_enabled", this, player.UserIDString));
            else
                SendMessage(player, lang.GetMessage("toggle_disabled", this, player.UserIDString));
        }

        private void CustomChatCMD(IPlayer user, string command, string[] args)
        {
            var player = user.Object as BasePlayer;
            if (player == null)
                return;

            ProcessReplies(player, new List<string>() { "ChatCommand" }, command);
        }

        #endregion

        #region AMTrigger

        Dictionary<string, AMTrigger> AMTriggers;

        private class AMTrigger
        {
            public string Key { get; set; }
            public string Hooks { get; set; }
            public bool RequiresTarget { get; set; }
            public bool UsesIsGlobalBroadcast { get; set; }
            public bool UsesDontTriggerAdmin { get; set; }
            public bool UsesGenericCooldown { get; set; }
            public bool UsesPlayerCooldown { get; set; }
        }

        private void InitAMTriggers()
        {
            AMTriggers = new();

            AMTriggers.Add("Timed", new AMTrigger() { Key = "Timed", RequiresTarget = false });
            AMTriggers.Add("ChatCommand", new AMTrigger() { Key = "ChatCommand", Hooks = "OnPlayerChat", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true, UsesGenericCooldown = true });
            AMTriggers.Add("AutoReply", new AMTrigger() { Key = "AutoReply", Hooks = "OnPlayerChat", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true, UsesGenericCooldown = true });
            AMTriggers.Add("NewPlayerJoined", new AMTrigger() { Key = "NewPlayerJoined", Hooks = "OnPlayerSleepEnded", RequiresTarget = false, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true });
            AMTriggers.Add("PlayerConnected", new AMTrigger() { Key = "PlayerConnected", Hooks = "OnPlayerSleepEnded", RequiresTarget = false, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true });
            AMTriggers.Add("PlayerDisconnected", new AMTrigger() { Key = "PlayerDisconnected", Hooks = "OnPlayerDisconnected", RequiresTarget = false, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true });
            AMTriggers.Add("PermissionGranted", new AMTrigger() { Key = "PermissionGranted", Hooks = "OnUserPermissionGranted", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true });
            AMTriggers.Add("PermissionRevoked", new AMTrigger() { Key = "PermissionRevoked", Hooks = "OnUserPermissionRevoked", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true });
            AMTriggers.Add("AddedToGroup", new AMTrigger() { Key = "AddedToGroup", Hooks = "OnUserGroupAdded", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true });
            AMTriggers.Add("RemovedFromGroup", new AMTrigger() { Key = "RemovedFromGroup", Hooks = "OnUserGroupRemoved", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true });
            AMTriggers.Add("PlayerDead", new AMTrigger() { Key = "PlayerDead", Hooks = "OnEntityDeath,OnPlayerSleepEnded", RequiresTarget = false, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true });
            AMTriggers.Add("CrateHacked", new AMTrigger() { Key = "CrateHacked", Hooks = "CanHackCrate", RequiresTarget = false, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true });
            AMTriggers.Add("EnteredZone", new AMTrigger() { Key = "EnteredZone", Hooks = "OnEnterZone", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true, UsesPlayerCooldown = true });
            AMTriggers.Add("LeftZone", new AMTrigger() { Key = "LeftZone", Hooks = "OnExitZone", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true, UsesPlayerCooldown = true });
            AMTriggers.Add("EnteredMonument", new AMTrigger() { Key = "EnteredMonument", Hooks = "OnPlayerEnteredMonument", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true, UsesPlayerCooldown = true });
            AMTriggers.Add("LeftMonument", new AMTrigger() { Key = "LeftMonument", Hooks = "OnPlayerExitedMonument", RequiresTarget = true, UsesIsGlobalBroadcast = true, UsesDontTriggerAdmin = true, UsesPlayerCooldown = true });
        }

        #endregion

        #region Config

        bool HasActiveNewPlayerJoinedAction = false;
        void ProcessConfig()
        {
            HasActiveNewPlayerJoinedAction = _config.Actions.Any(s => s.Type == "NewPlayerJoined" && s.IsEnabled());
            Lang_CacheActions();
        }

        private class PluginConfig
        {
            [JsonProperty(Order = 1000)]
            public Oxide.Core.VersionNumber Version;

            [JsonProperty(Order = 1, PropertyName = "Chat Icon (Steam Id)")]
            public string IconSteamId { get; set; }

            [JsonProperty(Order = 2, PropertyName = "Toggle Chat Command")]
            public string ToggleCommand { get; set; }

            [JsonProperty(Order = 3, PropertyName = "AutoReply Cooldown (in seconds)")]
            public int AutoReplyCooldown { get; set; }

            [JsonProperty(Order = 4, PropertyName = "ChatCommand Cooldown (in seconds)")]
            public int ChatCommandCooldown { get; set; }

            [JsonProperty(Order = 5, PropertyName = "ZoneManager Cooldown (in seconds)")]
            public int ZoneManagerCooldown { get; set; }

            [JsonProperty(Order = 6, PropertyName = "MonumentWatcher Cooldown (in seconds)")]
            public int MonumentWatcherCooldown { get; set; }

            [JsonProperty(Order = 7, PropertyName = "Sample Types for Reference (Do Not Edit)")]
            public string SampleTypes { get; set; }

            [JsonProperty(Order = 8, PropertyName = "Replies Server Languages (Creates lang file for each in data/AutomatedMessages/lang)")]
            public List<string> ServerLangs { get; set; }

            [JsonProperty(Order = 9, PropertyName = "Default Server Language")]
            public string DefaultLang { get; set; }

            [JsonProperty(Order = 10, PropertyName = "AutoReply `Broadcast to all` option to broadcast to team only if keywords sent from team chat")]
            public bool BroadcastToTeamOnly { get; set; }
            

            [JsonProperty(Order = 11, PropertyName = "Actions")]
            public List<ConfigAction> Actions { get; set; }

            [JsonProperty("Chat Icon (SteamId)")]
            public ulong _ObsIconSteamId { get; set; }

            [JsonProperty("Messages")]
            public List<ConfigAction> _ObsMessages = null;

            public bool ShouldSerialize_ObsMessages() => _ObsMessages != null;

            internal class ConfigAction
            {
                [JsonProperty("Id")]
                public string Id = GenerateId();

                [JsonProperty(Order = 0, PropertyName = "Enabled")]
                public bool Enabled { get; set; }

                [JsonProperty("Type (Check Sample Types above for Reference)")]
                public string Type = "";

                [JsonProperty("Broadcast to all?")]
                public bool IsGlobalBroadcast { get; set; }

                [JsonProperty("Don't trigger for admins")]
                public bool DontTriggerAdmin { get; set; }

                [JsonProperty("Send in chat")] 
                public bool SendInChat = true;

                [JsonProperty("Send as game tip")]
                public bool SendAsGameTip { get; set; }

                [JsonProperty("Interval between messages in minutes (if Type = Timed)")]
                public int Interval = 0;

                [JsonProperty("Target")]
                public string Target = "";

                [JsonProperty("Permissions")]
                public List<string> Permissions = new();

                [JsonProperty("Groups")]
                public List<string> Groups = new();

                [JsonProperty("Blacklisted Permissions")]
                public List<string> BlacklistedPerms = new();

                [JsonProperty("Blacklisted Groups")]
                public List<string> BlacklistedGroups = new();

                [JsonProperty("Player Can Disable?")]
                public bool PlayerCanDisable { get; set; }

                [JsonIgnore]
                public bool OnCooldownAll = false;

                [JsonIgnore]
                public List<ulong> OnCooldown { get; set; }

                [JsonIgnore]
                public Dictionary<string, Lang_Action> CachedLangActions { get; set; }

                [JsonProperty("Messages (Random if more than one)")]
                public List<string> _ObsMessages = null;

                [JsonProperty("Replies")]
                public List<string> _ObsReplies = null;

                public void RunCooldownAll(int cooldown)
                {
                    if (cooldown <= 0) return;

                    OnCooldownAll = true;
                    _plugin.timer.Once(cooldown, () =>
                    {
                        OnCooldownAll = false;
                    });
                }

                public void RunCooldownPlayer(BasePlayer player, int cooldown)
                {
                    OnCooldown ??= new();
                    if (cooldown <= 0 || OnCooldown.Contains(player.userID)) return;

                    OnCooldown.Add(player.userID);
                    _plugin.timer.Once(cooldown, () =>
                    {
                        OnCooldown.Remove(player.userID);
                    });
                }

                public ConfigAction Clone(bool newGuid = false)
                {
                    return new ConfigAction()
                    {
                        Id = newGuid ? GenerateId() : Id,
                        Enabled = Enabled,
                        Type = Type,
                        IsGlobalBroadcast = IsGlobalBroadcast,
                        DontTriggerAdmin = DontTriggerAdmin,
                        SendInChat = SendInChat,
                        SendAsGameTip = SendAsGameTip,
                        Interval = Interval,
                        Target = Target,
                        Permissions = new (Permissions),
                        Groups = new (Groups),
                        BlacklistedPerms = new (BlacklistedPerms),
                        BlacklistedGroups = new (BlacklistedGroups),
                        PlayerCanDisable = PlayerCanDisable,
                        CachedLangActions = null
                    };
                }
                public bool ShouldSerialize_ObsMessages() => _ObsMessages != null;

                public bool ShouldSerialize_ObsReplies() => _ObsReplies != null;

                public static string GenerateId()
                {
                    ulong timestamp = (ulong)DateTime.UtcNow.Ticks;
                    uint randomPart = (uint)UnityEngine.Random.Range(0, int.MaxValue);
                    return ((timestamp << 32) | randomPart).ToString();
                }

                public bool IsEnabled() => Enabled && (SendInChat || SendAsGameTip) ? true : false;
            }

            public bool ShouldSerialize_ObsIconSteamId() => _ObsIconSteamId != 0;
        }

        private PluginConfig GetDefaultConfig()
        {
            PluginConfig result = new PluginConfig
            {
                IconSteamId = "0",
                Version = Version,
                ToggleCommand = "tips",
                AutoReplyCooldown = 30,
                ChatCommandCooldown = 30,
                ZoneManagerCooldown = 0,
                MonumentWatcherCooldown = 15,
                SampleTypes = string.Join(" | ", AMTriggers.Keys),
                ServerLangs = new List<string>() { "en", "es", "it", "ru", "zh" },
                DefaultLang = lang.GetServerLanguage(),
                BroadcastToTeamOnly = true,
                Actions = new List<PluginConfig.ConfigAction>()
            };

            return result;
        }

        private void CheckForConfigUpdates()
        {
            bool changes = false;

            if (_config == null)
            {
                PluginConfig tmpDefaultConfig = GetDefaultConfig();
                _config = tmpDefaultConfig;
                changes = true;
            }

            string sampleTypes = string.Join(" | ", AMTriggers.Keys);
            if(_config.SampleTypes != sampleTypes)
            {
                _config.SampleTypes = sampleTypes;
                changes = true;
            }

            //1.0.8 update
            if (_config.Version == null || _config.Version < new VersionNumber(1, 0, 8))
            {
                PluginConfig tmpDefaultConfig = GetDefaultConfig();
                _config.ToggleCommand = tmpDefaultConfig.ToggleCommand;
                changes = true;
            }

            //1.0.16 update
            if (_config.Version < new VersionNumber(1, 0, 16))
            {
                PluginConfig tmpDefaultConfig = GetDefaultConfig();
                _config.AutoReplyCooldown = tmpDefaultConfig.AutoReplyCooldown;
                changes = true;
            }

            //2.0.0 update
            if (_config.Version < new VersionNumber(2, 0, 0))
            {

                if (_config._ObsMessages != null)
                    _config.Actions = new List<PluginConfig.ConfigAction>(_config._ObsMessages);
                else
                    _config.Actions = new();

                _config._ObsMessages = null;

                foreach (var action in _config.Actions)
                {
                    if (action._ObsMessages != null)
                        action._ObsReplies = new List<string>(action._ObsMessages);
                    else
                        action._ObsReplies = new();

                    action._ObsMessages = null;
                }

                changes = true;
            }

            if(string.IsNullOrEmpty(_config.DefaultLang) || !AvailableLangs.ContainsKey(_config.DefaultLang))
            {
                _config.DefaultLang = lang.GetServerLanguage();
                changes = true;
            }

            //2.0.6 update
            if (_config.Version < new VersionNumber(2, 0, 6))
            {
                PluginConfig tmpDefaultConfig = GetDefaultConfig();
                _config.ServerLangs = _config.ServerLangs;
                _config.IconSteamId = _config._ObsIconSteamId.ToString();
                _config._ObsIconSteamId = 0;
                _config.ChatCommandCooldown = tmpDefaultConfig.ChatCommandCooldown;
                _config.ZoneManagerCooldown = tmpDefaultConfig.ZoneManagerCooldown;
                _config.MonumentWatcherCooldown = tmpDefaultConfig.MonumentWatcherCooldown;
                _config.ServerLangs = new List<string>() { _config.DefaultLang };

                RepliesLangData = new();

                foreach (string serverLang in _config.ServerLangs)
                {
                    if (!AvailableLangs.ContainsKey(serverLang)) continue;
                    if (RepliesLangData.ContainsKey(serverLang)) continue;

                    Lang_Root langRoot = new();

                    foreach (var amTrigger in AMTriggers)
                        if (!langRoot.Triggers.ContainsKey(amTrigger.Key))
                        {
                            langRoot.Triggers.Add(amTrigger.Key, new());
                            changes = true;
                        }

                    RepliesLangData.Add(serverLang, langRoot);
                }

                foreach (var action in _config.Actions)
                {
                    if (action._ObsReplies != null)
                    {
                        foreach (var langRoot in RepliesLangData)
                        {
                            if(langRoot.Value.Triggers.TryGetValue(action.Type, out Lang_Trigger langTrigger))
                            {
                                langTrigger.Actions ??= new();

                                if(langRoot.Key == _config.DefaultLang)
                                    langTrigger.Actions.Add(action.Id, new() { Replies = action._ObsReplies });
                                else
                                    langTrigger.Actions.Add(action.Id, new());
                            }
                        }

                        action._ObsReplies = null;
                    }
                }

                Lang_SaveRepliesData();

                changes = true;
            }
            else
                Lang_LoadRepliesData();

            if (_config.ServerLangs == null || _config.ServerLangs.Count == 0)
            {
                _config.ServerLangs = new List<string>() { lang.GetServerLanguage() };
                changes = true;
            }

            var langsToRemove = _config.ServerLangs.Where(s => !AvailableLangs.ContainsKey(s));

            if (langsToRemove.Length > 0)
            {
                PrintWarning($"Language code{(langsToRemove.Length > 1 ? "s" : "")} \"{string.Join(", ", langsToRemove)}\" {(langsToRemove.Length > 1 ? "are" : "is")} not available or not in correct code (removed from config), the following are the available options:\n{string.Join(", ", AvailableLangs.Keys)}.");
                for (int i = 0; i < langsToRemove.Length; i++)
                    _config.ServerLangs.Remove(langsToRemove[i]);
                changes = true;
            }

            //2.0.9 update
            if (_config.Version < new VersionNumber(2, 0, 9))
            {
                _config.BroadcastToTeamOnly = true;
                changes = true;
            }

            if (_config.Version != Version)
                changes = true;

            if (changes)
            {
                _config.Version = Version;

                PrintWarning("Config updated.");
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            InitAMTriggers();
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();

            CheckForConfigUpdates();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region Data

        public PlayersData pData;

        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(5f, 10f), SaveData);

        private bool wiped { get; set; }

        private void OnNewSave(string filename) => wiped = true;

        private void SaveData()
        {
            if (pData != null)
                pData.Save();
        }

        public class PlayersData
        {
            public Dictionary<ulong, PlayerInfo> Players = new();

            public static PlayersData Load()
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<PlayersData>($"{nameof(AutomatedMessages)}/PlayerData");
                if (data == null || _plugin.wiped)
                {
                    _plugin.PrintWarning("No player data found! Creating a new data file.");
                    data = new();
                    data.Save();
                }
                return data;
            }

            public void Save()
            {
                Interface.Oxide.DataFileSystem.WriteObject($"{nameof(AutomatedMessages)}/PlayerData", this);
            }

            public bool CheckIfNew(ulong playerid)
            {
                return !Players.ContainsKey(playerid);
            }

            public void EnableTips(ulong playerid, bool active)
            {
                PlayerInfo playerInfo;
                if (!Players.TryGetValue(playerid, out playerInfo))
                    Players.Add(playerid, playerInfo = new PlayerInfo() { TipsActive = active, isNew = true });
                else
                    playerInfo.TipsActive = active;
            }

            public bool ToggleTipsActive(ulong playerid)
            {
                PlayerInfo playerInfo;
                if (!Players.TryGetValue(playerid, out playerInfo))
                    Players.Add(playerid, playerInfo = new PlayerInfo() { TipsActive = true, isNew = true });
                else
                    playerInfo.TipsActive = !playerInfo.TipsActive;

                return playerInfo.TipsActive;
            }

            public bool IsActive(ulong playerid)
            {
                PlayerInfo playerInfo;
                if (!Players.TryGetValue(playerid, out playerInfo))
                {
                    return true;
                }
                else
                {
                    return playerInfo.TipsActive;
                }
            }
        }

        public class PlayerInfo
        {
            public string Country = "";

            public bool TipsActive = true;

            [JsonIgnore]
            public bool wasDead = false;

            [JsonIgnore]
            public bool justJoined = false;

            [JsonIgnore]
            public bool isNew = false;

            [JsonIgnore]
            public string HackLocation = "";
        }

        #endregion

        #region Helpers

        private void SendMessage(BasePlayer player, string message, bool SendInChat = true, bool SendAsGameTip = false)
        {
            if (player == null) return;

            if (SendInChat)
            {
                ulong iconSteamId = 0;
                ulong.TryParse(_config.IconSteamId, out iconSteamId);

                player.SendConsoleCommand("chat.add", 2, iconSteamId, message);
            }

            if (SendAsGameTip)
                player.ShowToast(GameTip.Styles.Blue_Normal, message);
        }

        private bool MessageIsEligible(PluginConfig.ConfigAction configMessage, string userID)
        {
            bool isEligible = false;

            if((configMessage.Permissions == null || configMessage.Permissions.Count == 0) &&
                (configMessage.Groups == null || configMessage.Groups.Count == 0))
                isEligible = true;
            else
            {
                foreach (var perm in configMessage.Permissions)
                    if (permission.UserHasPermission(userID, perm))
                    {
                        isEligible = true;
                        break;
                    }

                foreach (var group in configMessage.Groups)
                    if (permission.UserHasGroup(userID, group))
                    {
                        isEligible = true;
                        break;
                    }
            }

            if (!isEligible) return false;

            if (configMessage.BlacklistedPerms != null && configMessage.BlacklistedPerms.Count > 0)
                foreach (var perm in configMessage.BlacklistedPerms)
                    if (permission.UserHasPermission(userID, perm))
                        return false;

            if (configMessage.BlacklistedGroups != null && configMessage.BlacklistedGroups.Count > 0)
                foreach (var group in configMessage.BlacklistedGroups)
                    if (permission.UserHasGroup(userID, group))
                        return false;

            return true;
        }

        private bool SetPlayerCountry(BasePlayer player, PlayerInfo pinfo)
        {
            if (player == null || !player.IsConnected) return false;

            bool success = false;
            Dictionary<string, object> objects = new();

            string URL = $"https://get.geojs.io/v1/ip/country/{player.net.connection.ipaddress.Split(':')[0]}.json";

            webrequest.Enqueue(URL, null, (code, response) =>
            {
                if (response == null || code != 200)
                    return;

                objects = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);

                if (objects.ContainsKey("name"))
                {
                    pinfo.Country = objects["name"].ToString();
                    success = true;
                }
                else
                {
                    success = false;
                }
            }, this, RequestMethod.GET);

            return success;
        }

        private bool OnCargoShip(BaseEntity entity)
        {
            return entity?.GetComponentInParent<CargoShip>();
        }

        private MonumentRef GetMonumentFromPosition(Vector3 position)
        {
            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                var monumentkey = MonumentsRadius.Keys.ToList().Find(s => monument.name.Contains(s));
                if (monumentkey != null)
                {
                    float distance = Vector3Ex.Distance2D(monument.transform.position, position);
                    if (distance <= MonumentsRadius[monumentkey].Radius)
                        return MonumentsRadius[monumentkey];
                }
            }

            return null;
        }

        private static Dictionary<string, MonumentRef> MonumentsRadius = new Dictionary<string, MonumentRef>()
        {
            {"supermarket", new MonumentRef(){ DisplayName = "Abandoned Supermarket", Radius = 40f }},
            {"gas_station", new MonumentRef(){ DisplayName = "Oxum's Gas Station", Radius = 70f }},
            {"warehouse", new MonumentRef(){ DisplayName = "Mining Outpost", Radius = 44f }},
            {"ferry_terminal", new MonumentRef(){ DisplayName = "Ferry Terminal", Radius = 150f }},
            {"harbor_1", new MonumentRef(){ DisplayName = "Large Harbor", Radius = 250f }},
            {"harbor_2", new MonumentRef(){ DisplayName = "Small Harbor", Radius = 230f }},
            {"sphere_tank", new MonumentRef(){ DisplayName = "Dome", Radius = 100f }},
            {"junkyard", new MonumentRef(){ DisplayName = "Junk Yard", Radius = 180f }},
            {"radtown_small", new MonumentRef(){ DisplayName = "Sewer Branch", Radius = 120f }},
            {"satellite_dish", new MonumentRef(){ DisplayName = "Satellite Dish", Radius = 160f }},
            {"oilrig_1", new MonumentRef(){ DisplayName = "Large Oil Rig", Radius = 80f }},
            {"oilrig_2", new MonumentRef(){ DisplayName = "Small Oil Rig", Radius = 50f }},
            {"trainyard", new MonumentRef(){ DisplayName = "Train Yard", Radius = 225f }},
            {"powerplant", new MonumentRef(){ DisplayName = "Power Plant", Radius = 205f }},
            {"water_treatment_plant", new MonumentRef(){ DisplayName = "Water Treatment Plant", Radius = 230f }},
            {"excavator", new MonumentRef(){ DisplayName = "Giant Excavator Pit", Radius = 240f }},
            {"nuclear_missile_silo", new MonumentRef(){ DisplayName = "Nuclear Missile Silo", Radius = 50f }},
            {"arctic_research_base", new MonumentRef(){ DisplayName = "Arctic Research Base", Radius = 200f }},
            {"airfield", new MonumentRef(){ DisplayName = "Airfield", Radius = 355f }},
            {"launch_site", new MonumentRef(){ DisplayName = "Launch Site", Radius = 535f }},
            {"military_tunnel", new MonumentRef(){ DisplayName = "Military Tunnels", Radius = 265f }},
            {"radtown_1", new MonumentRef(){ DisplayName = "Radtown", Radius = 120f }}
        };

        private class MonumentRef { public string DisplayName { get; set; } public float Radius { get; set; } }

        //copied from ZoneManager
        private static string HEXToRGBA(string hexColor, float alpha = 100f)
        {
            if (hexColor.StartsWith("#"))
                hexColor = hexColor.Substring(1);
            int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha / 100f}";
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"toggle_enabled", "<color=#F3D428>Tips enabled.</color>"},
                {"toggle_disabled", "<color=#fc3a3a>Tips disabled.</color>"},
                {"UI_Edit_Title", "Automated Messages"},
                {"UI_Edit_Cancel", "Cancel"},
                {"UI_Edit_Save", "Save"},
                {"UI_Edit_Back", "Back"},
                {"UI_Edit_Add", "Add"},
                {"UI_Edit_AddNew", "Add new"},
                {"UI_Edit_Duplicate", "Duplicate"},
                {"UI_Edit_Delete", "Delete"},
                {"UI_Edit_Preview", "Preview"},
                {"UI_Edit_Done", "Done"},
                {"UI_Edit_PreviewInChat", "Preview in chat"},
                {"UI_Edit_PreviewGameTip", "Preview game tip"},
                {"UI_Edit_SaveAndExit", "Save & Exit"},
                {"UI_Edit_SaveWarning", "Saving will reset running timers"},
                {"UI_Edit_LanguagesTitle", "Replies Languages:"},
                {"UI_Edit_Triggers", "Triggers"},
                {"UI_Edit_Trigger_Timed", "Timer"},
                {"UI_Edit_Trigger_ChatCommand", "Chat Command"},
                {"UI_Edit_Trigger_AutoReply", "Chat Bot"},
                {"UI_Edit_Trigger_NewPlayerJoined", "New Player Joined"},
                {"UI_Edit_Trigger_PlayerConnected", "Player Connected"},
                {"UI_Edit_Trigger_PlayerDisconnected", "Player Disconnected"},
                {"UI_Edit_Trigger_PermissionGranted", "Permission Granted"},
                {"UI_Edit_Trigger_PermissionRevoked", "Permission Revoked"},
                {"UI_Edit_Trigger_AddedToGroup", "Added to Group"},
                {"UI_Edit_Trigger_RemovedFromGroup", "Removed from Group"},
                {"UI_Edit_Trigger_PlayerDead", "Player Death"},
                {"UI_Edit_Trigger_CrateHacked", "Hacking Crate"},
                {"UI_Edit_Trigger_EnteredZone", "Zone Manager | Entered"},
                {"UI_Edit_Trigger_LeftZone", "Zone Manager | Exited"},
                {"UI_Edit_Trigger_EnteredMonument", "Monument Watcher | Entered"},
                {"UI_Edit_Trigger_LeftMonument", "Monument Watcher | Exited"},
                {"UI_Edit_Actions", "Actions"},
                {"UI_Edit_Actions_Empty", "It's empty here..."},
                {"UI_Edit_Action", "Action"},
                {"UI_Edit_Action_Active", "Active"},
                {"UI_Edit_Action_Replies", "Replies"},
                {"UI_Edit_Action_Permissions", "Permissions"},
                {"UI_Edit_Action_Groups", "Groups"},
                {"UI_Edit_Action_ExcludedPermissions", "Excluded Permissions"},
                {"UI_Edit_Action_ExcludedGroups", "Excluded Groups"},
                {"UI_Edit_Action_SendInChat", "Send in chat"},
                {"UI_Edit_Action_SendAsGameTip", "Send as game tip"},
                {"UI_Edit_Action_CanDisable", "Player can disable using <color=#DFFFAA>/{0}</color> command"},
                {"UI_Edit_Action_ExcludeAdmin", "Don't trigger for Admins  <i>(if broadcast to all enabled)</i>"},
                {"UI_Edit_Action_BroadcastToAll", "Broadcast to all"},
                {"UI_Edit_Action_Target_AutoReply", "Keywords <i>(comma separated)</i>"},
                {"UI_Edit_Action_Target_ChatCommand", "Chat Command <i>(variants comma separated)</i>"},
                {"UI_Edit_Action_Target_PermissionGranted", "Target permission"},
                {"UI_Edit_Action_Target_PermissionRevoked", "Target permission"},
                {"UI_Edit_Action_Target_AddedToGroup", "Target group"},
                {"UI_Edit_Action_Target_RemovedFromGroup", "Target group"},
                {"UI_Edit_Action_Target_EnteredZone", "Zone ID"},
                {"UI_Edit_Action_Target_LeftZone", "Zone ID"},
                {"UI_Edit_Action_Target_EnteredMonument", "Monument ID"},
                {"UI_Edit_Action_Target_LeftMonument", "Monument ID"},
                {"UI_Edit_Reply_TextEditor", "Text Editor"},
                {"UI_Edit_Reply_AvailableVariables", "<i>AVAILABLE VARIABLES</i>\n\n{0}\n\n<size=12><color=#a1a1a1>{1}</color></size>"},
                {"UI_Edit_ChatPreviewMode_Title", "Automated Messages\n<color=gray>Chat Preview Mode</color>"},
                {"UI_Edit_ChatPreviewMode_Exit", "Back to editor"},
                {"UI_Edit_GameTipPreviewMode_Title", "Automated Messages\n<color=gray>Game Tip Preview Mode</color>"},
                {"UI_Edit_GameTipPreviewMode_Exit", "Back to editor"}
            }, this, "en");
        }

        Dictionary<string, string> AvailableLangs = new() { { "af", "Afrikaans" }, { "ar", "Arabic" }, { "ca", "català" }, { "cs", "čeština" }, { "da", "dansk" }, { "de", "Deutsch" }, { "el", "ελληνικά" }, { "en-PT", "Pirate Aaargh!" }, { "en", "English" }, { "es", "español" }, { "fi", "suomi" }, { "fr", "français" }, { "hu", "magyar" }, { "it", "italiano" }, { "ja", "日本語" }, { "ko", "한국어" }, { "nl", "Nederlands" }, { "no", "norsk" }, { "pl", "polski" }, { "pt", "Português" }, { "ro", "românește" }, { "ru", "Русский язык" }, { "sr", "српски" }, { "sv", "svenska" }, { "tr", "Türkçe" }, { "uk", "українська мова" }, { "vi", "Tiếng Việt" }, { "zh", "中文" } };

        Dictionary<string, Lang_Root> RepliesLangData = new();
        private class Lang_Root { public Dictionary<string, Lang_Trigger> Triggers = new(); }
        private class Lang_Trigger { public Dictionary<string, Lang_Action> Actions = new(); }
        private class Lang_Action 
        { 
            public List<string> Replies = new();
            [JsonIgnore]
            public int ReplyIndex { get; set; }

            public Lang_Action Clone()
            {
                return new Lang_Action()
                {
                    Replies = new(Replies)
                };
            }
        }

        private void Lang_LoadRepliesData()
        {
            RepliesLangData = new();

            if (_config.ServerLangs == null || _config.ServerLangs.Count == 0)
            {
                _config.ServerLangs = new() { _config.DefaultLang };
                PrintWarning($"Added default server language \"{_config.DefaultLang}\" to config.");
                SaveConfig();
            }
            else if(!_config.ServerLangs.Contains(_config.DefaultLang))
            {
                _config.ServerLangs.Add(_config.DefaultLang);
                PrintWarning($"Added default server language \"{_config.DefaultLang}\" to config.");
                SaveConfig();
            }

            bool changes = false;

            foreach (string serverLang in _config.ServerLangs)
            {
                if (RepliesLangData.ContainsKey(serverLang)) continue;

                Lang_Root langRoot = new();

                string langPath = $"{Name}/lang/{serverLang}";
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(langPath))
                    langRoot = Interface.Oxide.DataFileSystem.ReadObject<Lang_Root>(langPath);
                else
                    changes = true;
                
                //Add missing triggers
                foreach (var amTrigger in AMTriggers)
                    if (!langRoot.Triggers.ContainsKey(amTrigger.Key))
                    {
                        langRoot.Triggers.Add(amTrigger.Key, new());
                        changes = true;
                    }

                //Remove extra actions
                List<string> actionsToRemove = Facepunch.Pool.Get<List<string>>();

                foreach (var langTrigger in langRoot.Triggers)
                {
                    foreach (var langAction in langTrigger.Value.Actions)
                    {
                        if (!_config.Actions.Any(s => s.Type == langTrigger.Key && s.Id == langAction.Key))
                            actionsToRemove.Add(langAction.Key);
                    }

                    foreach (var actionToRemove in actionsToRemove)
                    {
                        langTrigger.Value.Actions.Remove(actionToRemove);
                        changes = true;
                    }

                    actionsToRemove.Clear();
                }

                Facepunch.Pool.FreeUnmanaged<string>(ref actionsToRemove);

                //Add missing actions
                foreach (var configAction in _config.Actions)
                {
                    foreach (var langTrigger in langRoot.Triggers)
                    {
                        if (configAction.Type != langTrigger.Key) continue;

                        if (!langTrigger.Value.Actions.Any(s => s.Key == configAction.Id))
                        {
                            langTrigger.Value.Actions.Add(configAction.Id, new());
                            changes = true;
                        }
                    }
                }

                RepliesLangData.Add(serverLang, langRoot);
            }

            if (changes)
            {
                Lang_SaveRepliesData();
                PrintWarning("Lang files updated due to missing file or mismatch.");
            }
        }

        private void Lang_SaveRepliesData()
        {
            foreach (var langRoot in RepliesLangData)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/lang/{langRoot.Key}", langRoot.Value);
        }

        private void Lang_CacheActions()
        {
            foreach (var configAction in _config.Actions)
            {
                configAction.CachedLangActions = new();

                foreach (string language in _config.ServerLangs)
                    configAction.CachedLangActions.Add(language, Lang_GetAction(language, configAction.Type, configAction.Id));
            }

            PrintWarning($"Cached replies for \"{string.Join(", ", _config.ServerLangs)}\" language{((_config.ServerLangs.Count == 1) ? "" : "s")}.");
        }

        List<string> DetectedUnsupportedLangs = new();

        private bool Lang_TryGetAction(BasePlayer player, string triggerKey, string actionId, out string language, out Lang_Action lang_Action)
        {
            language = lang.GetLanguage(player.UserIDString);

            if(language != "en-PT")
                language = language.Split("-")[0];

            if (!RepliesLangData.ContainsKey(language))
            {
                lang_Action = null;
                return false;
            }

            lang_Action = Lang_GetAction(language, triggerKey, actionId);
            return true;
        }

        private Lang_Action Lang_GetAction(string language, string triggerKey, string actionId) => RepliesLangData[language].Triggers[triggerKey].Actions[actionId];

        #endregion

        #region UI

        #region Models/Fields

        string Layer = "Overlay";
        string uimodal = $"{PREFIX_LONG}.modal";
        string chatpreview = $"{PREFIX_LONG}.chatpreview";
        string gametippreview = $"{PREFIX_LONG}.gametippreview";

        private class UISession
        {
            public string SelectedRepliesLang { get; set; }
            public List<UITrigger> Triggers { get; set; }
        }

        private class UITrigger
        {
            public AMTrigger AMTrigger { get; set; }
            public List<PluginConfig.ConfigAction> ConfigActions { get; set; }
            public Dictionary<string, Lang_Trigger> LangsTriggers { get; set; }
        }

        Dictionary<ulong, UISession> UISessions = new();

        #endregion

        #region Functions

        private static string FormatTime(double time, bool d = true, bool h = true, bool m = true, bool s = true) => FormatTime(TimeSpan.FromSeconds((float)time), d, h, m, s);

        private static string FormatTime(TimeSpan t, bool d = true, bool h = true, bool m = true, bool s = true)
        {
            List<string> shortForm = new();
            if (d && t.Days > 0)
                shortForm.Add(string.Format("{0} day" + (t.Days > 1 ? "s" : ""), t.Days.ToString()));
            if (h && t.Hours > 0)
                shortForm.Add(string.Format("{0} hour" + (t.Hours > 1 ? "s" : ""), t.Hours.ToString()));
            if (m && t.Minutes > 0)
                shortForm.Add(string.Format("{0} minute" + (t.Minutes > 1 ? "s" : ""), t.Minutes.ToString()));
            if (s && t.Seconds > 0)
                shortForm.Add(string.Format("{0} second" + (t.Seconds > 1 ? "s" : ""), t.Seconds.ToString()));

            return string.Join(", ", shortForm);
        }

        private UISession GetUISession(ulong userID)
        {
            UISession uiSession;
            if (UISessions.TryGetValue(userID, out uiSession))
                return uiSession;
            else
                return null;
        }

        private UITrigger GetUITrigger(UISession uiSession, string triggerKey) => uiSession.Triggers.FirstOrDefault(s => s.AMTrigger.Key == triggerKey);

        private UITrigger GetUITrigger(ulong userID, string triggerKey)
        {
            UISession uiSession = GetUISession(userID);
            if (uiSession != null)
                return GetUITrigger(uiSession, triggerKey);
            else
                return null;
        }

        private PluginConfig.ConfigAction GetUIAction(UITrigger uiTrigger, int actionIndex)
        {
            if (uiTrigger == null || uiTrigger.ConfigActions.Count < actionIndex + 1) return null;

            return uiTrigger.ConfigActions[actionIndex];
        }

        private PluginConfig.ConfigAction GetUIAction(ulong userID, string triggerKey, int actionIndex)
        {
            UITrigger uiTrigger = GetUITrigger(userID, triggerKey);

            if (uiTrigger == null || uiTrigger.ConfigActions.Count < actionIndex + 1) return null;

            return uiTrigger.ConfigActions[actionIndex];
        }

        private void UpdateConfigFromUISession(UISession uiSession)
        {
            _config.Actions = new();

            foreach (var trigger in uiSession.Triggers)
            {
                foreach (var configaction in trigger.ConfigActions)
                {
                    _config.Actions.Add(configaction.Clone());
                }

                foreach (var langRoot in RepliesLangData)
                {
                    langRoot.Value.Triggers[trigger.AMTrigger.Key] = new();
                    foreach (var langaction in trigger.LangsTriggers[langRoot.Key].Actions)
                        langRoot.Value.Triggers[trigger.AMTrigger.Key].Actions.Add(langaction.Key, langaction.Value.Clone());
                }
            }


            SaveConfig();
            Lang_SaveRepliesData();
            ProcessConfig();
            PrintWarning("Config updated");

            UnsubscribeHooks();
            SubscribeHooks();
            InitTimers();
            InitAutoReply();
            UnregisterCachedCommands();
            RegisterConfigCommands();
        }

        #endregion

        private void UI_ShowEditor(BasePlayer player)
        {
            UISession session;

            if (!UISessions.TryGetValue(player.userID, out session))
            {
                session = new();
                session.SelectedRepliesLang = _config.DefaultLang;
                session.Triggers = new();

                foreach (var trigger in AMTriggers)
                {
                    UITrigger uiTrigger = new();
                    uiTrigger.AMTrigger = trigger.Value;
                    uiTrigger.ConfigActions = new();
                    uiTrigger.LangsTriggers = new();

                    foreach (var action in _config.Actions.Where(s => s.Type == trigger.Key))
                        uiTrigger.ConfigActions.Add(action.Clone());

                    foreach (string language in _config.ServerLangs)
                    {
                        uiTrigger.LangsTriggers.Add(language, new());
                        foreach (var action in RepliesLangData[language].Triggers[trigger.Key].Actions)
                            uiTrigger.LangsTriggers[language].Actions.Add(action.Key, action.Value.Clone());
                    }

                    session.Triggers.Add(uiTrigger);
                }

                UISessions.Add(player.userID, session);
            }

            CuiElementContainer cont = new();

            //Parent panel
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = HEXToRGBA("#000000", 80), Material = "assets/content/ui/uibackgroundblur.mat" }
                },
                Layer,
                uimodal,
                uimodal
            );

            //Title
            cont.Add(
                new CuiElement
                {
                    Parent = uimodal,
                    Name = $"{uimodal}.title",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = lang.GetMessage("UI_Edit_Title", this, player.UserIDString).ToUpper(), FontSize = 24, Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 275", OffsetMax = "200 325" }
                    }
                }
            );

            int panelWidth = 1000;
            int panelHeight = 500;

            int saveExitWidth = 100;
            int saveWidth = 70;
            int savesGap = 5;
            int buttonsHeight = 25;

            //Save & Exit button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{(panelWidth / 2) - saveExitWidth} -{(panelHeight / 2) + buttonsHeight + savesGap}", OffsetMax = $"{(panelWidth / 2)} -{(panelHeight / 2) + savesGap}" },
                    Text = { Text = lang.GetMessage("UI_Edit_SaveAndExit", this, player.UserIDString).ToUpper(), FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#90CAF3", 100f) },
                    Button = { Color = HEXToRGBA("#376E92", 95), Command = $"{PREFIX_SHORT}.edit.savecancel saveexit" }
                },
                $"{uimodal}"
            );

            //Save button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{(panelWidth / 2) - saveExitWidth - savesGap - saveWidth} -{(panelHeight / 2) + buttonsHeight + savesGap}", OffsetMax = $"{(panelWidth / 2) - saveExitWidth - savesGap} -{(panelHeight / 2) + savesGap}" },
                    Text = { Text = lang.GetMessage("UI_Edit_Save", this, player.UserIDString).ToUpper(), FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#DFFFAA", 100f) },
                    Button = { Color = HEXToRGBA("#79A62F", 95), Command = $"{PREFIX_SHORT}.edit.savecancel save" }
                },
                $"{uimodal}"
            );

            //Save notice
            cont.Add(
                new CuiElement
                {
                    Parent = $"{uimodal}",
                    Name = $"{uimodal}.savenotice",
                    Components =
                    {
                        new CuiTextComponent() { Color = "1 1 1 1", Text = $"<i>{lang.GetMessage("UI_Edit_SaveWarning", this, player.UserIDString)}</i>".ToUpper(), FontSize = 12, Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{(panelWidth / 2) - saveExitWidth - savesGap * 4 - saveWidth - 300} -{(panelHeight / 2) + buttonsHeight + savesGap}", OffsetMax = $"{(panelWidth / 2) - saveExitWidth - savesGap * 4 - saveWidth} -{(panelHeight / 2) + savesGap}" },
                    }
                }
            );

            //Cancel button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{(panelWidth / 2)} -{(panelHeight / 2) + buttonsHeight + savesGap}", OffsetMax = $"-{(panelWidth / 2) - 75} -{(panelHeight / 2) + savesGap}" },
                    Text = { Text = lang.GetMessage("UI_Edit_Cancel", this, player.UserIDString).ToUpper(), FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#FFC3B9", 100f) },
                    Button = { Color = HEXToRGBA("#CE422B", 95), Command = $"{PREFIX_SHORT}.edit.savecancel cancel" }
                },
                $"{uimodal}"
            );

            CuiHelper.AddUi(player, cont);

            UI_ShowTriggersView(player, session, panelWidth, panelHeight, 30);
        }

        private void UI_ShowTriggersView(BasePlayer player, UISession session = null, int panelWidth = 1000, int panelHeight = 500, int panelInnerMargin = 30)
        {
            if (session == null && !UISessions.TryGetValue(player.userID, out session))
                return;

            CuiElementContainer cont = new();

            //Center panel
            cont.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{panelWidth / 2} -{panelHeight / 2}", OffsetMax = $"{panelWidth / 2} {panelHeight / 2}" },
                    Image = { Color = HEXToRGBA("#1E2020", 95) }
                },
                uimodal,
                $"{uimodal}.triggersview",
                $"{uimodal}.triggersview"
            );

            int titleHeight = 30;

            //Sub-title
            cont.Add(
                new CuiElement
                {
                    Parent = $"{uimodal}.triggersview",
                    Name = $"{uimodal}.triggersview.title",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = lang.GetMessage("UI_Edit_Triggers", this, player.UserIDString).ToUpper(), FontSize = 16, Align = TextAnchor.UpperLeft },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"{panelInnerMargin} -{panelInnerMargin + titleHeight}", OffsetMax = $"-{panelInnerMargin} -{panelInnerMargin}" }
                    }
                }
            );

            //Scrollviewer
            CuiElement scrollViewer = new CuiElement
            {
                Parent = $"{uimodal}.triggersview",
                Name = $"{uimodal}.triggersview.scrollviewer",
                Components =
                {
                    new CuiImageComponent() { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"{panelInnerMargin} {panelInnerMargin}", OffsetMax = $"-{panelInnerMargin - 15} -{panelInnerMargin + titleHeight}" },
                    new CuiNeedsCursorComponent()
                }
            };

            int xStart = 0;
            int yStart = 0;
            int itemSize = 125;
            int itemsGap = 10;
            int scrollviewerWidth = panelWidth - panelInnerMargin * 2;
            int finalContentHeight = 0;
            int minContentHeight = panelHeight - panelInnerMargin * 2 - titleHeight;

            CuiElementContainer scrollContent = new();

            //Triggers list
            foreach (var trigger in session.Triggers)
            {
                if (xStart + itemSize > scrollviewerWidth)
                {
                    xStart = 0;
                    yStart += itemSize + itemsGap;
                }

                int actionCount = trigger.ConfigActions.Count;
                int activeActionCount = trigger.ConfigActions.Where(s => s.IsEnabled()).Length;

                scrollContent.Add(
                    new CuiElement
                    {
                        Parent = $"{uimodal}.triggersview.scrollviewer",
                        Name = $"{uimodal}.triggersview.{trigger.AMTrigger.Key}",
                        Components =
                        {
                            new CuiImageComponent() { Color = HEXToRGBA(activeActionCount > 0 ? "#648135" : "#393C3C", 95) },
                            new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{xStart} -{yStart + itemSize}", OffsetMax = $"{xStart + itemSize} -{yStart}" }
                        }
                    }
                );

                finalContentHeight = yStart + itemSize;
                xStart += itemSize + itemsGap;

                scrollContent.Add(
                    new CuiElement
                    {
                        Parent = $"{uimodal}.triggersview.{trigger.AMTrigger.Key}",
                        Name = $"{uimodal}.triggersview.{trigger.AMTrigger.Key}.text",
                        Components =
                        {
                            new CuiTextComponent() { Color = "1 1 1 1", Text = lang.GetMessage($"UI_Edit_Trigger_{trigger.AMTrigger.Key}", this, player.UserIDString), FontSize = 16, Align = TextAnchor.UpperLeft },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"10 10", OffsetMax = $"-10 -10" }
                        }
                    }
                );

                scrollContent.Add(
                    new CuiElement
                    {
                        Parent = $"{uimodal}.triggersview.{trigger.AMTrigger.Key}",
                        Name = $"{uimodal}.triggersview.{trigger.AMTrigger.Key}.count",
                        Components =
                        {
                            new CuiTextComponent() { Color = "1 1 1 1", Text = $"{actionCount}", FontSize = 18, Align = TextAnchor.LowerLeft },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"10 10", OffsetMax = $"-10 -10" }
                        }
                    }
                );

                scrollContent.Add(
                    new CuiElement
                    {
                        Parent = $"{uimodal}.triggersview.{trigger.AMTrigger.Key}",
                        Name = $"{uimodal}.triggersview.{trigger.AMTrigger.Key}.arrow",
                        Components =
                        {
                            new CuiTextComponent() { Color = "1 1 1 1", Text = "→", FontSize = 18, Align = TextAnchor.LowerRight },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"10 10", OffsetMax = $"-10 -10" }
                        }
                    }
                );

                scrollContent.Add(new CuiElement
                {
                    Name = $"{uimodal}.triggersview.{trigger.AMTrigger.Key}.button",
                    Parent = $"{uimodal}.triggersview.{trigger.AMTrigger.Key}",
                    Components =
                    {
                        new CuiButtonComponent() { Color = "0 0 0 0", Command = $"{PREFIX_SHORT}.ui.cmd GoToActions {trigger.AMTrigger.Key}" },
                        new CuiRectTransformComponent() { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
            }

            //Now add ScrollViewComponent based on final content height
            scrollViewer.Components.Add(
                new CuiScrollViewComponent
                {
                    Vertical = true,
                    Horizontal = false,
                    MovementType = ScrollRect.MovementType.Elastic,
                    Elasticity = 0.3f,
                    Inertia = true,
                    DecelerationRate = 0.5f,
                    ScrollSensitivity = 25f,
                    ContentTransform = new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{(minContentHeight > finalContentHeight ? minContentHeight : finalContentHeight)}", OffsetMax = $"0 0" },
                    VerticalScrollbar = new CuiScrollbar { Invert = false, Size = 5f, TrackColor = HEXToRGBA("#121414", 90), HandleColor = HEXToRGBA("#393C3C", 85), HighlightColor = HEXToRGBA("#393C3C", 95) }
                }
            );

            //Now add scrollviewer and content
            cont.Add(scrollViewer);
            cont.AddRange(scrollContent);

            CuiHelper.AddUi(player, cont);
        }

        private void UI_ShowActionsView(BasePlayer player, string selectedTrigger, UISession session = null, int panelWidth = 1000, int panelHeight = 500, int panelInnerMargin = 30)
        {
            if (session == null && !UISessions.TryGetValue(player.userID, out session))
                return;

            UITrigger uiTrigger = session.Triggers.FirstOrDefault(s => s.AMTrigger.Key == selectedTrigger);

            if (uiTrigger == null)
                return;

            CuiElementContainer cont = new();

            //Center panel
            cont.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{panelWidth / 2} -{panelHeight / 2}", OffsetMax = $"{panelWidth / 2} {panelHeight / 2}" },
                    Image = { Color = HEXToRGBA("#1E2020", 95) }
                },
                uimodal,
                $"{uimodal}.actionsview",
                $"{uimodal}.actionsview"
            );

            int titleHeight = 30;
            int triggersButtonWidth = 75;
            int backButtonWidth = 60;
            int subtitleInnerGap = 1;

            //Back button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{panelInnerMargin} -{panelInnerMargin + 20}", OffsetMax = $"{panelInnerMargin + backButtonWidth} -{panelInnerMargin}" },
                    Text = { Text = lang.GetMessage("UI_Edit_Back", this, player.UserIDString).ToUpper(), FontSize = 12, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#90CAF3", 100f) },
                    Button = { Color = HEXToRGBA("#376E92", 95), Command = $"{PREFIX_SHORT}.ui.cmd GoToTriggers" }
                },
                $"{uimodal}.actionsview"
            );

            //Sub-title
            cont.Add(
                new CuiElement
                {
                    Parent = $"{uimodal}.actionsview",
                    Name = $"{uimodal}.actionsview.title",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = lang.GetMessage($"UI_Edit_Trigger_{uiTrigger.AMTrigger.Key}", this, player.UserIDString).ToUpper(), FontSize = 16, Align = TextAnchor.UpperCenter },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"{panelInnerMargin + triggersButtonWidth + subtitleInnerGap} -{panelInnerMargin + titleHeight}", OffsetMax = $"-{panelInnerMargin + triggersButtonWidth + subtitleInnerGap} -{panelInnerMargin}" }
                    }
                }
            );


            CuiHelper.AddUi(player, cont);

            UI_ShowActionsList(player, uiTrigger, session.SelectedRepliesLang);
        }

        private void UI_ShowActionsList(BasePlayer player, UITrigger uiTrigger, string selectedRepliesLang, int selectedActionIndex = 0, int titleHeight = 30, int panelInnerMargin = 30)
        {
            CuiElementContainer cont = new();

            string parent = $"{uimodal}.actionsview";
            string listPanel = $"{uimodal}.actionsview.list";
            int barInnerMargin = 15;

            //Back panel
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.25 1", OffsetMin = $"{panelInnerMargin} {panelInnerMargin}", OffsetMax = $"0 -{panelInnerMargin + titleHeight}" },
                    Image = { Color = HEXToRGBA("#151717", 90) }
                },
                parent, listPanel, listPanel
            );

            int addBtnWidth = 60;

            //Add button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-{addBtnWidth} -20", OffsetMax = $"0 0" },
                    Text = { Text = lang.GetMessage("UI_Edit_AddNew", this, player.UserIDString).ToUpper(), FontSize = 12, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#90CAF3", 100f) },
                    Button = { Color = HEXToRGBA("#376E92", 95), Command = $"{PREFIX_SHORT}.ui.cmd AddNewAction {uiTrigger.AMTrigger.Key}" }
                },
                listPanel
            );

            int subtitleHeight = 25;

            //Sub-title
            cont.Add(
                new CuiElement
                {
                    Parent = listPanel,
                    Name = $"{listPanel}.title",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = lang.GetMessage("UI_Edit_Actions", this, player.UserIDString).ToUpper(), FontSize = 14, Align = TextAnchor.UpperLeft },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"{barInnerMargin} -{barInnerMargin + subtitleHeight}", OffsetMax = $"-{barInnerMargin} -{barInnerMargin}" }
                    }
                }
            );

            int totalCount = uiTrigger.ConfigActions.Count;
            if (totalCount > 0)
            {
                int itemHeight = 35;
                int gapHeight = 5;
                int minContentHeight = itemHeight * 9 + gapHeight * 8;
                int finalContentHeight = itemHeight * totalCount + gapHeight * (totalCount - 1);

                //Scrollviewer
                cont.Add(
                    new CuiElement
                    {
                        Parent = $"{listPanel}",
                        Name = $"{listPanel}.actionlist",
                        Components =
                        {
                            new CuiImageComponent() { Color = "1 0 1 0" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"{barInnerMargin} {barInnerMargin}", OffsetMax = $"-{barInnerMargin - 10} -{barInnerMargin + subtitleHeight}" },
                            new CuiNeedsCursorComponent(),
                            new CuiScrollViewComponent {
                                Vertical = true, Horizontal = false, MovementType = ScrollRect.MovementType.Elastic, Elasticity = 0.3f, Inertia = true, DecelerationRate = 0.5f, ScrollSensitivity = 25f,
                                ContentTransform = new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{(minContentHeight > finalContentHeight ? minContentHeight : finalContentHeight)}", OffsetMax = $"0 0" },
                                VerticalScrollbar = new CuiScrollbar { Invert = false, Size = 5f, TrackColor = HEXToRGBA("#121414", 90), HandleColor = HEXToRGBA("#262929", 85), HighlightColor = HEXToRGBA("#262929", 95) }
                            }
                        }
                    }
                );

                int actionIndex = 0;
                float usedHeight = 0;

                //Action list
                foreach (PluginConfig.ConfigAction action in uiTrigger.ConfigActions)
                {
                    string bgcolor = HEXToRGBA("#393C3C", 100);

                    if (actionIndex == selectedActionIndex)
                        bgcolor = HEXToRGBA("#5A6060", 100);

                    //Action panel
                    cont.Add(
                        new CuiPanel
                        {
                            CursorEnabled = true,
                            RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{usedHeight + itemHeight}", OffsetMax = $"-15 -{usedHeight}" },
                            Image = { Color = bgcolor }
                        },
                        $"{listPanel}.actionlist",
                        $"{listPanel}.actionlist.{actionIndex}"
                    );

                    string text = $"{lang.GetMessage("UI_Edit_Action", this, player.UserIDString)} {actionIndex + 1}";

                    if (uiTrigger.AMTrigger.RequiresTarget && !string.IsNullOrEmpty(action.Target))
                        text = $"{action.Target}";

                    switch (uiTrigger.AMTrigger.Key)
                    {
                        case "Timed":
                            if (action.Interval > 0)
                                text = $"Every {FormatTime(action.Interval * 60)}";
                            break;
                        case "ChatCommand":
                            if (!string.IsNullOrEmpty(action.Target))
                                text = string.Join(" ", action.Target.Split(',').Where(s => !string.IsNullOrEmpty(s.Trim())).Select(s => "/" + s.Trim()));
                            break;
                    }

                    //Action text
                    cont.Add(
                        new CuiElement
                        {
                            Parent = $"{listPanel}.actionlist.{actionIndex}",
                            Name = $"{listPanel}.actionlist.{actionIndex}.text",
                            Components =
                            {
                                new CuiTextComponent() { Color = HEXToRGBA("#FFFFFF", 100), Text = text, FontSize = 14, Align = TextAnchor.MiddleCenter },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.95 1" }
                            }
                        }
                    );

                    //Action select button
                    cont.Add(
                        new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Button = { Color = "0 0 0 0", Close = "", Command = $"{PREFIX_SHORT}.ui.cmd GoToActionForm {uiTrigger.AMTrigger.Key} {actionIndex}" }
                        },
                        $"{listPanel}.actionlist.{actionIndex}"
                    );

                    usedHeight += itemHeight + gapHeight;

                    actionIndex++;
                }
            }
            else
            {
                cont.Add(
                    new CuiElement
                    {
                        Parent = listPanel,
                        Name = $"{listPanel}.emptylist",
                        Components =
                        {
                            new CuiTextComponent() { Color = "1 1 1 0.8", Text = lang.GetMessage("UI_Edit_Actions_Empty", this, player.UserIDString).ToUpper(), FontSize = 14, Align = TextAnchor.MiddleLeft },
                            new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"{barInnerMargin} -{(barInnerMargin + 35 + titleHeight + 10)}", OffsetMax = $"0 -{barInnerMargin}" }
                        }
                    }
                );
            }

            // CuiHelper.DestroyUi(player, listPanel);
            CuiHelper.AddUi(player, cont);

            if (totalCount > 0)
                UI_ShowSelectedAction(player, uiTrigger, selectedActionIndex, selectedRepliesLang);
            else
            {
                CuiHelper.DestroyUi(player, $"{uimodal}.actionsview.editpanel");
                CuiHelper.DestroyUi(player, $"{uimodal}.langsview");
            }
        }

        private void UI_UpdateSelectedActionInList(BasePlayer player, UITrigger uiTrigger, int actionIndex)
        {
            CuiElementContainer cont = new();

            string text = $"Action {actionIndex + 1}";

            if (uiTrigger.AMTrigger.RequiresTarget && !string.IsNullOrEmpty(uiTrigger.ConfigActions[actionIndex].Target))
                text = $"{uiTrigger.ConfigActions[actionIndex].Target}";

            switch (uiTrigger.AMTrigger.Key)
            {
                case "Timed":
                    if (uiTrigger.ConfigActions[actionIndex].Interval > 0)
                        text = $"Every {FormatTime(uiTrigger.ConfigActions[actionIndex].Interval * 60)}";
                    break;
                case "ChatCommand":
                    if (!string.IsNullOrEmpty(uiTrigger.ConfigActions[actionIndex].Target))
                        text = $"/{uiTrigger.ConfigActions[actionIndex].Target}";
                    break;
            }

            //Action text
            cont.Add(
                new CuiElement
                {
                    Name = $"{uimodal}.actionsview.list.actionlist.{actionIndex}.text",
                    Components = { new CuiTextComponent() { Text = text } },
                    Update = true
                }
            );

            CuiHelper.AddUi(player, cont);
        }

        private void UI_ShowSelectedAction(BasePlayer player, UITrigger uiTrigger, int actionIndex, string selectedRepliesLang, int titleHeight = 30, int panelInnerMargin = 30)
        {
            CuiElementContainer cont = new();

            string parent = $"{uimodal}.actionsview";
            string editPanel = $"{parent}.editpanel";

            int formPanelInnerMargin = 20;

            PluginConfig.ConfigAction configAction = uiTrigger.ConfigActions[actionIndex];

            //Back panel
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0.25 0", AnchorMax = "1 1", OffsetMin = $"{10} {panelInnerMargin}", OffsetMax = $"-{panelInnerMargin} -{panelInnerMargin + titleHeight}" },
                    Image = { Color = HEXToRGBA("#151717", 95) }
                },
                parent, editPanel, editPanel
            );

            int duplicateBtnWidth = 70;
            int deleteBtnWidth = 50;

            //Duplicate button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-{deleteBtnWidth + 5 + duplicateBtnWidth} 0", OffsetMax = $"-{deleteBtnWidth + 5} 20" },
                    Text = { Text = lang.GetMessage("UI_Edit_Duplicate", this, player.UserIDString).ToUpper(), FontSize = 12, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#90CAF3", 100f) },
                    Button = { Color = HEXToRGBA("#376E92", 95), Command = $"{PREFIX_SHORT}.ui.cmd DuplicateAction {uiTrigger.AMTrigger.Key} {actionIndex}" }
                },
                editPanel
            );

            //Delete button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-{deleteBtnWidth} 0", OffsetMax = $"0 20" },
                    Text = { Text = lang.GetMessage("UI_Edit_Delete", this, player.UserIDString).ToUpper(), FontSize = 12, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#FFC3B9", 100f) },
                    Button = { Color = HEXToRGBA("#CE422B", 95), Command = $"{PREFIX_SHORT}.ui.cmd DeleteAction {uiTrigger.AMTrigger.Key} {actionIndex}" }
                },
                editPanel
            );

            //Scrollviewer
            CuiElement scrollViewer = new CuiElement
            {
                Parent = editPanel,
                Name = $"{editPanel}.scrollviewer",
                Components =
                {
                    new CuiImageComponent() { Color = "1 0 1 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 {formPanelInnerMargin}", OffsetMax = $"-5 -{formPanelInnerMargin}" },
                    new CuiNeedsCursorComponent()
                }
            };

            CuiElementContainer scrollContent = new();

            int yStart = 0;
            int fieldsGap = 15;

            //Checkbox
            UI_CheckBox(
                uiTrigger.ConfigActions[actionIndex].Enabled, nameof(PluginConfig.ConfigAction.Enabled), lang.GetMessage("UI_Edit_Action_Active", this, player.UserIDString), $"{PREFIX_SHORT}.ui.cmd ActionCB {uiTrigger.AMTrigger.Key} {actionIndex}", scrollContent, $"{editPanel}.scrollviewer",
                new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"{formPanelInnerMargin} -{yStart + 20}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" },
                "Inactive"
            );
            yStart += 20 + fieldsGap;

            //Replies
            UI_Listbox(
                player, nameof(Lang_Action.Replies), uiTrigger.LangsTriggers[selectedRepliesLang].Actions[configAction.Id].Replies, lang.GetMessage("UI_Edit_Action_Replies", this, player.UserIDString).ToUpper(), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", scrollContent, $"{editPanel}.scrollviewer",
                new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"{formPanelInnerMargin} -{yStart + 145}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }, minItemsInView: 3, previewButton: true
            );
            yStart += 145 + fieldsGap;

            if (uiTrigger.AMTrigger.Key == "Timed")
            {
                //Textbox
                UI_TextBox(
                    uiTrigger.ConfigActions[actionIndex].Interval.ToString(), $"Interval in minutes", $"{PREFIX_SHORT}.ui.texteditor ActionTextbox {uiTrigger.AMTrigger.Key} {actionIndex} {nameof(PluginConfig.ConfigAction.Interval)}", scrollContent, $"{editPanel}.scrollviewer",
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"{formPanelInnerMargin} -{yStart + 30}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }
                );
                yStart += 30 + fieldsGap;
            }

            if (uiTrigger.AMTrigger.RequiresTarget)
            {
                //Textbox
                UI_TextBox(
                    uiTrigger.ConfigActions[actionIndex].Target.ToString(), lang.GetMessage($"UI_Edit_Action_Target_{uiTrigger.AMTrigger.Key}", this, player.UserIDString), $"{PREFIX_SHORT}.ui.texteditor ActionTextbox {uiTrigger.AMTrigger.Key} {actionIndex} {nameof(PluginConfig.ConfigAction.Target)}", scrollContent, $"{editPanel}.scrollviewer",
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"{formPanelInnerMargin} -{yStart + 30}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }
                );
                yStart += 30 + fieldsGap;
            }
            
            //Checkbox
            UI_CheckBox(
                uiTrigger.ConfigActions[actionIndex].SendInChat, nameof(PluginConfig.ConfigAction.SendInChat), string.Format(lang.GetMessage("UI_Edit_Action_SendInChat", this, player.UserIDString), _config.ToggleCommand), $"{PREFIX_SHORT}.ui.cmd ActionCB {uiTrigger.AMTrigger.Key} {actionIndex}", scrollContent, $"{editPanel}.scrollviewer",
                new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0.49 1", OffsetMin = $"{formPanelInnerMargin} -{yStart + 20}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }
            );
            
            //Checkbox
            UI_CheckBox(
                uiTrigger.ConfigActions[actionIndex].SendAsGameTip, nameof(PluginConfig.ConfigAction.SendAsGameTip), string.Format(lang.GetMessage("UI_Edit_Action_SendAsGameTip", this, player.UserIDString), _config.ToggleCommand), $"{PREFIX_SHORT}.ui.cmd ActionCB {uiTrigger.AMTrigger.Key} {actionIndex}", scrollContent, $"{editPanel}.scrollviewer",
                new CuiRectTransformComponent { AnchorMin = "0.51 1", AnchorMax = "1 1", OffsetMin = $"0 -{yStart + 20}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }
            );
            yStart += 20 + fieldsGap;

            //Checkbox
            UI_CheckBox(
                uiTrigger.ConfigActions[actionIndex].PlayerCanDisable, nameof(PluginConfig.ConfigAction.PlayerCanDisable), string.Format(lang.GetMessage("UI_Edit_Action_CanDisable", this, player.UserIDString), _config.ToggleCommand), $"{PREFIX_SHORT}.ui.cmd ActionCB {uiTrigger.AMTrigger.Key} {actionIndex}", scrollContent, $"{editPanel}.scrollviewer",
                new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0.49 1", OffsetMin = $"{formPanelInnerMargin} -{yStart + 20}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }
            );
            //yStart += 20 + fieldsGap;

            if (uiTrigger.AMTrigger.UsesDontTriggerAdmin)
            {
                //Checkbox
                UI_CheckBox(
                    uiTrigger.ConfigActions[actionIndex].DontTriggerAdmin, nameof(PluginConfig.ConfigAction.DontTriggerAdmin), lang.GetMessage("UI_Edit_Action_ExcludeAdmin", this, player.UserIDString), $"{PREFIX_SHORT}.ui.cmd ActionCB {uiTrigger.AMTrigger.Key} {actionIndex}", scrollContent, $"{editPanel}.scrollviewer",
                    new CuiRectTransformComponent { AnchorMin = "0.51 1", AnchorMax = "1 1", OffsetMin = $"0 -{yStart + 20}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }
                );
            }
            yStart += 20 + fieldsGap;

            if (uiTrigger.AMTrigger.UsesIsGlobalBroadcast)
            {
                //Checkbox
                UI_CheckBox(
                    uiTrigger.ConfigActions[actionIndex].IsGlobalBroadcast, nameof(PluginConfig.ConfigAction.IsGlobalBroadcast), lang.GetMessage("UI_Edit_Action_BroadcastToAll", this, player.UserIDString), $"{PREFIX_SHORT}.ui.cmd ActionCB {uiTrigger.AMTrigger.Key} {actionIndex}", scrollContent, $"{editPanel}.scrollviewer",
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0.49 1", OffsetMin = $"{formPanelInnerMargin} -{yStart + 20}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }
                );
                yStart += 20 + fieldsGap;
            }

            //Permissions
            UI_Listbox(
                player, nameof(PluginConfig.ConfigAction.Permissions), uiTrigger.ConfigActions[actionIndex].Permissions, lang.GetMessage("UI_Edit_Action_Permissions", this, player.UserIDString).ToUpper(), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", scrollContent, $"{editPanel}.scrollviewer",
                new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0.49 1", OffsetMin = $"{formPanelInnerMargin} -{yStart + 110}", OffsetMax = $"0 -{yStart}" }
            );
            //yStart += 115 + fieldsGap;

            //Groups
            UI_Listbox(
                player, nameof(PluginConfig.ConfigAction.Groups), uiTrigger.ConfigActions[actionIndex].Groups, lang.GetMessage("UI_Edit_Action_Groups", this, player.UserIDString).ToUpper(), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", scrollContent, $"{editPanel}.scrollviewer",
                new CuiRectTransformComponent { AnchorMin = "0.51 1", AnchorMax = "1 1", OffsetMin = $"0 -{yStart + 110}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }
            );
            yStart += 115 + fieldsGap;

            //Blacklisted Permissions
            UI_Listbox(
                player, nameof(PluginConfig.ConfigAction.BlacklistedPerms), uiTrigger.ConfigActions[actionIndex].BlacklistedPerms, lang.GetMessage("UI_Edit_Action_ExcludedPermissions", this, player.UserIDString).ToUpper(), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", scrollContent, $"{editPanel}.scrollviewer",
                new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0.49 1", OffsetMin = $"{formPanelInnerMargin} -{yStart + 110}", OffsetMax = $"0 -{yStart}" }
            );
            //yStart += 115 + fieldsGap;

            //Blacklisted Groups
            UI_Listbox(
                player, nameof(PluginConfig.ConfigAction.BlacklistedGroups), uiTrigger.ConfigActions[actionIndex].BlacklistedGroups, lang.GetMessage("UI_Edit_Action_ExcludedGroups", this, player.UserIDString).ToUpper(), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", scrollContent, $"{editPanel}.scrollviewer",
                new CuiRectTransformComponent { AnchorMin = "0.51 1", AnchorMax = "1 1", OffsetMin = $"0 -{yStart + 110}", OffsetMax = $"-{formPanelInnerMargin - 5} -{yStart}" }
            );
            yStart += 110 + fieldsGap;

            scrollViewer.Components.Add(
                new CuiScrollViewComponent
                {
                    Vertical = true,
                    Horizontal = false,
                    MovementType = ScrollRect.MovementType.Elastic,
                    Elasticity = 0.3f,
                    Inertia = true,
                    DecelerationRate = 0.5f,
                    ScrollSensitivity = 25f,
                    ContentTransform = new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{yStart}", OffsetMax = $"0 0" },
                    VerticalScrollbar = new CuiScrollbar { Invert = false, Size = 5f, TrackColor = HEXToRGBA("#121414", 90), HandleColor = HEXToRGBA("#262929", 85), HighlightColor = HEXToRGBA("#262929", 95) }
                }
            );

            cont.Add(scrollViewer);
            cont.AddRange(scrollContent);

            CuiHelper.AddUi(player, cont);

            UI_ShowRepliesLanguages(player, UISessions[player.userID].SelectedRepliesLang, uiTrigger, actionIndex);
        }

        private void UI_ShowRepliesLanguages(BasePlayer player, string selectedRepliesLang, UITrigger uiTrigger, int actionIndex, int centerPanelWidth = 1000, int centerPanelHeight = 500)
        {
            CuiElementContainer cont = new();

            string parent = $"{uimodal}";
            string listPanel = $"{uimodal}.langsview";

            int panelWidth = 640 - centerPanelWidth / 2 - 30;
            //Back panel
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{centerPanelWidth / 2 + 15} -{centerPanelHeight / 2}", OffsetMax = $"{centerPanelWidth / 2 + 15 + panelWidth} {centerPanelHeight / 2}" },
                    Image = { Color = "0 0 0 0" }
                },
                parent, listPanel, listPanel
            );

            int titleHeight = 25;
            int gapHeight = 5;

            //Langs Title
            cont.Add(
                new CuiElement
                {
                    Parent = $"{listPanel}",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = $"<i>{lang.GetMessage("UI_Edit_LanguagesTitle", this, player.UserIDString).ToUpper()}</i>", FontSize = 11, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{titleHeight}", OffsetMax = "0 0" }
                    }
                }
            );

            //Scrollviewer
            CuiElement scrollViewer = new CuiElement
            {
                Parent = $"{listPanel}",
                Name = $"{listPanel}.scrollviewer",
                Components =
                {
                    new CuiImageComponent() { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.85 1", OffsetMin = $"0 0", OffsetMax = $"0 -{titleHeight + gapHeight}" },
                    new CuiNeedsCursorComponent()
                }
            };

            int yStart = 0;
            int itemHeight = 20;
            int finalContentHeight = 0;
            int minContentHeight = centerPanelHeight - titleHeight - gapHeight;

            CuiElementContainer scrollContent = new CuiElementContainer();

            //Langs list
            foreach (string language in _config.ServerLangs)
            {
                bool selected = language == selectedRepliesLang;

                //Lang panel
                scrollContent.Add(
                    new CuiPanel
                    {
                        CursorEnabled = true,
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{yStart + itemHeight}", OffsetMax = $"-10 -{yStart}" },
                        Image = { Color = selected ? HEXToRGBA("#79A62F", 80) : HEXToRGBA("#5A6060", 50) }
                    },
                    $"{listPanel}.scrollviewer",
                    $"{listPanel}.scrollviewer.{language}"
                );

                finalContentHeight = yStart + itemHeight;
                yStart += itemHeight + gapHeight;

                //Lang text
                scrollContent.Add(
                    new CuiElement
                    {
                        Parent = $"{listPanel}.scrollviewer.{language}",
                        Components =
                        {
                            new CuiTextComponent() { Color = selected ? "1 1 1 1" : "1 1 1 0.5", Text = $"{AvailableLangs[language].ToUpper()}", FontSize = 10, Align = TextAnchor.MiddleCenter },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.95 1" }
                        }
                    }
                );

                //Lang select button
                scrollContent.Add(
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Close = "", Command = $"{PREFIX_SHORT}.ui.cmd ChangeRepliesLang {language} {uiTrigger.AMTrigger.Key} {actionIndex}" }
                    },
                    $"{listPanel}.scrollviewer.{language}"
                );
            }

            //Now add ScrollViewComponent based on final content height
            scrollViewer.Components.Add(
                new CuiScrollViewComponent
                {
                    Vertical = true, Horizontal = false, MovementType = ScrollRect.MovementType.Elastic, Elasticity = 0.3f, Inertia = true, DecelerationRate = 0.5f, ScrollSensitivity = 30f,
                    ContentTransform = new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{(minContentHeight > finalContentHeight ? minContentHeight : finalContentHeight)}", OffsetMax = $"0 0" },
                    VerticalScrollbar = new CuiScrollbar { Invert = false, Size = 2f, AutoHide = true, TrackColor = HEXToRGBA("#121414", 15), HandleColor = HEXToRGBA("#828282", 15), HighlightColor = HEXToRGBA("#828282", 20), PressedColor = HEXToRGBA("#828282", 15) }
                }
            );

            //Now add scrollviewer and content
            cont.Add(scrollViewer);
            cont.AddRange(scrollContent);

            CuiHelper.AddUi(player, cont);
        }

        private void UI_Listbox(BasePlayer player, string key, List<string> list, string label, string command, CuiElementContainer cont, string parent, CuiRectTransformComponent RectTransform, int minItemsInView = 2, bool previewButton = false)
        {
            string listPanel = $"{uimodal}.list.{key}";

            int listInnerMargin = 10;

            //Back panel
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = RectTransform.AnchorMin, AnchorMax = RectTransform.AnchorMax, OffsetMin = RectTransform.OffsetMin, OffsetMax = RectTransform.OffsetMax },
                    Image = { Color = HEXToRGBA("#0A0C0C", 95) }
                },
                parent, listPanel, listPanel
            );

            int addBtnWidth = 35;

            //Add button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-{addBtnWidth} -17", OffsetMax = $"0 0" },
                    Text = { Text = lang.GetMessage("UI_Edit_Add", this, player.UserIDString).ToUpper(), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#90CAF3", 100f) },
                    Button = { Color = HEXToRGBA("#376E92", 95), Command = command.Replace("%cmdname%", "ActionAddListItem").Replace("%index%", $"{key}") }
                },
                listPanel
            );

            int subtitleHeight = 20;

            //Sub-title
            cont.Add(
                new CuiElement
                {
                    Parent = listPanel,
                    Name = $"{listPanel}.title",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#FFFFFF", 100f), Text = label, FontSize = 13, Align = TextAnchor.UpperLeft },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0.8 1", OffsetMin = $"{listInnerMargin} -{listInnerMargin + subtitleHeight}", OffsetMax = $"-{listInnerMargin} -{listInnerMargin}" }
                    }
                }
            );

            UI_Listbox_Scrollviewer(player, key, listPanel, list, listInnerMargin, subtitleHeight, command, previewButton, minItemsInView, ref cont);
        }

        private void UI_Listbox_Scrollviewer(BasePlayer player, string key, string listPanel, List<string> list, int listInnerMargin, int subtitleHeight, string command, bool previewButton, int minItemsInView, ref CuiElementContainer cont, bool scrollToBottom = false)
        {
            int totalCount = list.Count;
            int scrollCount = totalCount > minItemsInView ? totalCount : minItemsInView;

            float itemHeight = 30;
            float gapHeight = 4;

            string contentAnchorMin = scrollToBottom ? "0 0" : "0 1";
            string contentAnchorMax = scrollToBottom ? "1 0" : "1 1";
            string contentOffsetMin = scrollToBottom ? $"0 0" : $"0 -{scrollCount * itemHeight + (scrollCount - 1) * gapHeight}";
            string contentOffsetMax = scrollToBottom ? $"0 {scrollCount * itemHeight + (scrollCount - 1) * gapHeight}" : $"0 0";

            //Scrollviewer
            cont.Add(
                new CuiElement
                {
                    Parent = listPanel,
                    Name = $"{listPanel}.scrollviewer",
                    DestroyUi = $"{listPanel}.scrollviewer",
                    Components =
                    {
                        new CuiImageComponent() { Color = "1 0 1 0" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"{listInnerMargin} {listInnerMargin}", OffsetMax = $"-{listInnerMargin} -{listInnerMargin + subtitleHeight + gapHeight}" },
                        new CuiNeedsCursorComponent(),
                        new CuiScrollViewComponent
                        {
                            Vertical = true, Horizontal = false, MovementType = ScrollRect.MovementType.Elastic, Elasticity = 0.3f, Inertia = true, DecelerationRate = 0.5f, ScrollSensitivity = 15f,
                            ContentTransform = new CuiRectTransformComponent { AnchorMin = contentAnchorMin, AnchorMax = contentAnchorMax, OffsetMin = contentOffsetMin, OffsetMax = contentOffsetMax },
                            VerticalScrollbar = new CuiScrollbar { Invert = false, Size = 5f, TrackColor = HEXToRGBA("#121414", 90), HandleColor = HEXToRGBA("#262929", 85), HighlightColor = HEXToRGBA("#262929", 95) }
                        }
                    }
                }
            );

            string altColor1 = HEXToRGBA("#393C3C", 95);
            string altColor2 = HEXToRGBA("#393C3C", 60);
            string bgcolor = altColor1;

            int itemIndex = 0;
            float usedHeight = 0;

            foreach (string text in list)
            {
                if (itemIndex % 2 == 0)
                    bgcolor = altColor1;
                else
                    bgcolor = altColor2;

                cont.Add(
                    new CuiPanel
                    {
                        CursorEnabled = true,
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{usedHeight + itemHeight}", OffsetMax = $"-10 -{usedHeight}" },
                        Image = { Color = bgcolor }
                    },
                    $"{listPanel}.scrollviewer", $"{listPanel}.scrollviewer.{itemIndex}"
                );

                cont.Add(
                    new CuiElement
                    {
                        Parent = $"{listPanel}.scrollviewer.{itemIndex}",
                        Name = $"{listPanel}.scrollviewer.{itemIndex}.text",
                        Components =
                        {
                            new CuiTextComponent() { Color = "1 1 1 1", Text = text, FontSize = 12, Align = TextAnchor.UpperLeft },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"10 0", OffsetMax = $"-20 -9" }
                        }
                    }
                );

                cont.Add(
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Close = "", Command = command.Replace("%cmdname%", "ActionSelectListItem").Replace("%index%", $"{key} {itemIndex.ToString()}") }
                    },
                    $"{listPanel}.scrollviewer.{itemIndex}"
                );

                //Delete button
                cont.Add(
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-45 5", OffsetMax = $"-5 -5" },
                        Text = { Text = lang.GetMessage("UI_Edit_Delete", this, player.UserIDString).ToUpper(), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#FFC3B9", 100f) },
                        Button = { Color = HEXToRGBA("#CE422B", 95), Command = command.Replace("%cmdname%", "ActionDeleteListItem").Replace("%index%", $"{key} {itemIndex.ToString()}") }
                    },
                    $"{listPanel}.scrollviewer.{itemIndex}"
                );

                if (previewButton)
                {
                    //Preview button
                    cont.Add(
                        new CuiButton
                        {
                            RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-95 5", OffsetMax = $"-50 -5" },
                            Text = { Text = lang.GetMessage("UI_Edit_Preview", this, player.UserIDString).ToUpper(), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#DFFFAA", 100f) },
                            Button = { Color = HEXToRGBA("#79A62F", 95), Command = command.Replace("%cmdname%", "ActionPreviewReply").Replace("%index%", $"{key} {itemIndex.ToString()}") }
                        },
                        $"{listPanel}.scrollviewer.{itemIndex}"
                    );
                }

                usedHeight += itemHeight + gapHeight;

                itemIndex++;
            }
        }

        private void UI_Listbox_Update(BasePlayer player, string key, string command, List<string> list, CuiElementContainer cont, string label = null, bool scrollToBottom = false, int minItemsInView = 2, bool previewButton = false)
        {
            string listPanel = $"{uimodal}.list.{key}";

            int listInnerMargin = 10;

            int subtitleHeight = 20;

            if (!string.IsNullOrEmpty(label))
            {
                //Sub-title
                cont.Add(
                    new CuiElement
                    {
                        Name = $"{listPanel}.title",
                        Components = { new CuiTextComponent() { Text = label } },
                        Update = true
                    }
                );
            }

            UI_Listbox_Scrollviewer(player, key, listPanel, list, listInnerMargin, subtitleHeight, command, previewButton, minItemsInView, ref cont, scrollToBottom);
        }

        private void UI_CheckBox(bool isChecked, string key, string label, string command, CuiElementContainer cont, string parent, CuiRectTransformComponent RectTransform, string uncheckedText = null)
        {
            string cbName = $"{uimodal}.cb.{key}";

            //Back panel
            cont.Add(new CuiElement { Parent = parent, Name = cbName, DestroyUi = cbName, Components = { new CuiImageComponent() { Color = "1 0 0 0" }, RectTransform } });

            string checkedColor = isChecked ? HEXToRGBA("#648135") : HEXToRGBA("#CE422B");

            int toggleWidth = 40;

            cont.Add(
                new CuiElement
                {
                    Parent = cbName,
                    Name = $"{cbName}.toggle",
                    Components = {
                        new CuiImageComponent() { Color = checkedColor },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = $"0 -10", OffsetMax = $"{toggleWidth} 10" }
                    }
                }
            );

            int handleXAnchor = isChecked ? 1 : 0;
            int handleXOffsetMin = isChecked ? -20 : 2;
            int handleXOffsetMax = isChecked ? -2 : 20;

            cont.Add(
                new CuiElement
                {
                    Parent = $"{cbName}.toggle",
                    Name = $"{cbName}.toggle.handle",
                    Components =
                    {
                        new CuiImageComponent() { Color = HEXToRGBA("#151617") },
                        new CuiRectTransformComponent { AnchorMin = $"{handleXAnchor} 0", AnchorMax = $"{handleXAnchor} 1", OffsetMin = $"{handleXOffsetMin} 2", OffsetMax = $"{handleXOffsetMax} -2" }
                    }
                }
            );

            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Close = "", Command = $"{command} {key}" }
                },
                cbName
            );

            string labelText = !string.IsNullOrEmpty(uncheckedText) && !isChecked ? uncheckedText : label;

            cont.Add(
                new CuiElement
                {
                    Parent = cbName,
                    Name = $"{cbName}.button",
                    Components =
                    {
                        new CuiTextComponent() { Color = "1 1 1 1", Text = labelText, FontSize = 12, Align = TextAnchor.MiddleLeft },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = $"{toggleWidth + 10} -10", OffsetMax = $"0 10" }
                    }
                }
            );
        }

        private void UI_CheckBox_Update(bool isChecked, string key, CuiElementContainer cont, string label = null, string uncheckedText = null)
        {
            string cbName = $"{uimodal}.cb.{key}";

            string checkedColor = isChecked ? HEXToRGBA("#648135") : HEXToRGBA("#CE422B");

            cont.Add(
                new CuiElement
                {
                    Name = $"{cbName}.toggle",
                    Components = { new CuiImageComponent() { Color = checkedColor } },
                    Update = true
                }
            );

            int handleXAnchor = isChecked ? 1 : 0;
            int handleXOffsetMin = isChecked ? -20 : 2;
            int handleXOffsetMax = isChecked ? -2 : 20;

            cont.Add(
                new CuiElement
                {
                    Name = $"{cbName}.toggle.handle",
                    Components = { new CuiRectTransformComponent { AnchorMin = $"{handleXAnchor} 0", AnchorMax = $"{handleXAnchor} 1", OffsetMin = $"{handleXOffsetMin} 2", OffsetMax = $"{handleXOffsetMax} -2" } },
                    Update = true
                }
            );

            string labelText = !string.IsNullOrEmpty(uncheckedText) && !isChecked ? uncheckedText : label;

            if (!string.IsNullOrEmpty(labelText))
            {
                cont.Add(
                    new CuiElement
                    {
                        Name = $"{cbName}.button",
                        Components = { new CuiTextComponent() { Text = labelText } },
                        Update = true
                    }
                );
            }
        }

        private void UI_TextBox(string value, string label, string command, CuiElementContainer cont, string parent, CuiRectTransformComponent RectTransform, string uncheckedText = null)
        {
            string cbName = CuiHelper.GetGuid();

            //Back panel
            cont.Add(new CuiElement { Parent = parent, Name = cbName, Components = { new CuiImageComponent() { Color = "1 0 0 0" }, RectTransform } });

            cont.Add(new CuiElement { Parent = cbName, Name = $"{cbName}.textbox.wrapper", Components = { new CuiImageComponent() { Color = HEXToRGBA("#0A0C0C", 90) }, new CuiRectTransformComponent { AnchorMin = "0.51 0", AnchorMax = "1 1" } } });
            cont.Add(new CuiElement
            {
                Parent = $"{cbName}.textbox.wrapper",
                Name = $"{cbName}.textbox",
                Components =
                {
                    new CuiInputFieldComponent { Align = TextAnchor.MiddleLeft, Command = command, FontSize = 12, Text = value, NeedsKeyboard = true, LineType = InputField.LineType.SingleLine },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
                }
            });

            cont.Add(
                new CuiElement
                {
                    Parent = cbName,
                    Components =
                    {
                        new CuiTextComponent() { Color = "1 1 1 1", Text = label, FontSize = 12, Align = TextAnchor.MiddleLeft },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.49 1" }
                    }
                }
            );
        }

        private void UI_ShowTextEditor(BasePlayer player, string text, string title, string triggerKey, int actionIndex, string listType, int listItemIndex)
        {
            CuiElementContainer cont = new();

            string texteditormodal = $"{uimodal}.replyeditor";

            //Parent panel
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = HEXToRGBA("#000000", 95), Material = "assets/content/ui/uibackgroundblur.mat" }
                },
                uimodal,
                texteditormodal,
                texteditormodal
            );

            //Title
            cont.Add(
                new CuiElement
                {
                    Parent = texteditormodal,
                    Name = $"{texteditormodal}.title",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = title.ToUpper(), FontSize = 24, Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 50", OffsetMax = "200 100" }
                    }
                }
            );

            //center panel
            cont.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300 -25", OffsetMax = "300 25" },
                    Image = { Color = HEXToRGBA("#272B2B", 90) }
                },
                texteditormodal,
                $"{texteditormodal}.panel"
            );

            cont.Add(new CuiElement
            {
                Parent = $"{texteditormodal}.panel",
                Name = $"{texteditormodal}.panel.textbox",
                Components =
                {
                    new CuiInputFieldComponent { Color = HEXToRGBA("#DDDDDD"),  Align = TextAnchor.MiddleLeft,
                        Command = $"{PREFIX_SHORT}.ui.textboxcmd {triggerKey} {actionIndex} {listType} {listItemIndex}",
                        FontSize = 20, Text = text, NeedsKeyboard = true, LineType = InputField.LineType.SingleLine },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 5", OffsetMax = "-20 -5" }
                }
            });

            int doneWidth = 100;
            string closeCommand = $"{PREFIX_SHORT}.ui.cmd ActionRefreshList {triggerKey} {actionIndex} {listType} {listItemIndex}";

            //Done button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-{doneWidth} -{30}", OffsetMax = $"0 -5" },
                    Text = { Text = "DONE", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#90CAF3", 100f) },
                    Button = { Color = HEXToRGBA("#376E92", 95), Close = texteditormodal, Command = closeCommand }
                },
                $"{texteditormodal}.panel"
            );

            CuiHelper.AddUi(player, cont);
        }

        private void UI_ShowMultilineTextEditor(BasePlayer player, string text, string title, string triggerKey, int actionIndex, int listItemIndex)
        {
            CuiElementContainer cont = new();

            string texteditormodal = $"{uimodal}.replyeditor";

            //Parent panel
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = HEXToRGBA("#000000", 60), Material = "assets/content/ui/uibackgroundblur.mat" }
                },
                uimodal,
                texteditormodal,
                texteditormodal
            );

            //Title
            cont.Add(
                new CuiElement
                {
                    Parent = texteditormodal,
                    Name = $"{texteditormodal}.title",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = title.ToUpper(), FontSize = 24, Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 225", OffsetMax = "200 275" }
                    }
                }
            );

            //center panel
            cont.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -200", OffsetMax = "400 200" },
                    Image = { Color = HEXToRGBA("#393C3C", 95) }
                },
                texteditormodal,
                $"{texteditormodal}.panel"
            );

            string tips = string.Format(lang.GetMessage("UI_Edit_Reply_AvailableVariables", this, player.UserIDString), string.Join("\n", availableVariables), "+ Placeholder API Supported");
            //Tips
            cont.Add(
                new CuiElement
                {
                    Parent = $"{texteditormodal}.panel",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = tips, FontSize = 13, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "20 -200", OffsetMax = "200 0" }
                    }
                }
            );

            int doneWidth = 100;

            string closeCommand = $"{PREFIX_SHORT}.ui.cmd ActionRefreshList {triggerKey} {actionIndex} Replies {listItemIndex}";

            //Done button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-{doneWidth} -{30}", OffsetMax = $"0 -5" },
                    Text = { Text = lang.GetMessage("UI_Edit_Done", this, player.UserIDString).ToUpper(), FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#90CAF3", 100f) },
                    Button = { Color = HEXToRGBA("#376E92", 95), Close = texteditormodal, Command = closeCommand }
                },
                $"{texteditormodal}.panel"
            );

            //Preview button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"0 -{30}", OffsetMax = $"{130} -5" },
                    Text = { Text = lang.GetMessage("UI_Edit_PreviewInChat", this, player.UserIDString).ToUpper(), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#DFFFAA", 100f) },
                    Button = { Color = HEXToRGBA("#79A62F", 95), Command = $"{PREFIX_SHORT}.ui.cmd ActionPreviewReply {triggerKey} {actionIndex} Replies {listItemIndex} chat" }
                },
                $"{texteditormodal}.panel"
            );

            //Preview game tip button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{135} -{30}", OffsetMax = $"{270} -5" },
                    Text = { Text = lang.GetMessage("UI_Edit_PreviewGameTip", this, player.UserIDString).ToUpper(), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#DFFFAA", 100f) },
                    Button = { Color = HEXToRGBA("#79A62F", 95), Command = $"{PREFIX_SHORT}.ui.cmd ActionPreviewReply {triggerKey} {actionIndex} Replies {listItemIndex} gametip" }
                },
                $"{texteditormodal}.panel"
            );

            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = $"0 0" },
                    Image = { Color = "1 1 1 0" }
                },
                $"{texteditormodal}.panel",
                $"{texteditormodal}.panel.innerpanel"
            );

            cont.Add(
                new CuiElement
                {
                    Parent = $"{texteditormodal}.panel.innerpanel",
                    Name = $"{texteditormodal}.panel.innerpanel.subtitle1",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = lang.GetMessage("UI_Edit_Preview", this, player.UserIDString).ToUpper(), FontSize = 14, Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "20 -40", OffsetMax = "-20 -20" }
                    }
                }
            );

            cont.Add(
                new CuiElement
                {
                    Parent = $"{texteditormodal}.panel.innerpanel",
                    Name = $"{texteditormodal}.panel.innerpanel.preview",
                    Components =
                    {
                            new CuiImageComponent() { Color = "1 0 1 0" },
                            new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "1 1", OffsetMin = "20 10", OffsetMax = "-20 -45" },
                            new CuiNeedsCursorComponent(),
                            new CuiScrollViewComponent {
                                Vertical = true, Horizontal = false, MovementType = ScrollRect.MovementType.Elastic, Elasticity = 0.3f, Inertia = true, DecelerationRate = 0.5f, ScrollSensitivity = 10f,
                                ContentTransform = new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{500}", OffsetMax = $"0 0" },
                                VerticalScrollbar = new CuiScrollbar { Size = 8f, TrackColor = HEXToRGBA("#121414", 90), HandleColor = HEXToRGBA("#262929", 85), HighlightColor = HEXToRGBA("#262929", 95) }
                            }
                    }
                }
            );

            cont.Add(
                new CuiElement
                {
                    Parent = $"{texteditormodal}.panel.innerpanel.preview",
                    Name = $"{texteditormodal}.panel.innerpanel.preview.text",
                    Components =
                    {
                        new CuiTextComponent() { Color = "1 1 1 1", Text = text.Replace("\t", "".PadLeft(4)), FontSize = 14, Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                }
            );

            cont.Add(
                new CuiElement
                {
                    Parent = $"{texteditormodal}.panel.innerpanel",
                    Name = $"{texteditormodal}.panel.innerpanel.subtitle2",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = lang.GetMessage("UI_Edit_Reply_TextEditor", this, player.UserIDString).ToUpper(), FontSize = 14, Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "20 -30", OffsetMax = "-20 -10" }
                    }
                }
            );

            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5", OffsetMin = "20 20", OffsetMax = "-20 -30" },
                    Image = { Color = HEXToRGBA("#1F2222", 95) }
                },
                $"{texteditormodal}.panel.innerpanel", $"{texteditormodal}.panel.innerpanel.tap"
            );

            var textLines = text.Split('\n');

            int lineHeight = 25;
            int numOfLines = 20;
            int linesGap = 1;

            int totalContentHeight = lineHeight * numOfLines + linesGap * (numOfLines - 1);

            //Scrollviewer
            cont.Add(
                new CuiElement
                {
                    Parent = $"{texteditormodal}.panel.innerpanel.tap",
                    Name = $"{texteditormodal}.panel.innerpanel.tap.scrollviewer",
                    Components =
                    {
                            new CuiImageComponent() { Color = "1 0 1 0" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                            new CuiNeedsCursorComponent(),
                            new CuiScrollViewComponent {
                                Vertical = true, Horizontal = true, MovementType = ScrollRect.MovementType.Elastic, Elasticity = 0.3f, Inertia = true, DecelerationRate = 0.5f, ScrollSensitivity = 10f,
                                ContentTransform = new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"0 -{totalContentHeight}", OffsetMax = $"2000 0" },
                                VerticalScrollbar = new CuiScrollbar { Size = 8f, TrackColor = HEXToRGBA("#121414", 90), HandleColor = HEXToRGBA("#262929", 85), HighlightColor = HEXToRGBA("#262929", 95) },
                                HorizontalScrollbar = new CuiScrollbar { Invert = true, Size = 8f, TrackColor = HEXToRGBA("#121414", 90), HandleColor = HEXToRGBA("#262929", 85), HighlightColor = HEXToRGBA("#262929", 95) }
                            }
                    }
                }
            );

            int yStart = 2;

            for (int i = 0; i < numOfLines; i++)
            {
                cont.Add(
                    new CuiPanel { RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"25 -{yStart + lineHeight}", OffsetMax = $"2000 -{yStart}" }, Image = { Color = HEXToRGBA("#171818", 70) } },
                    $"{texteditormodal}.panel.innerpanel.tap.scrollviewer",
                    $"{texteditormodal}.panel.innerpanel.tap.scrollviewer.line"
                );

                //Line number
                cont.Add(
                    new CuiElement
                    {
                        Parent = $"{texteditormodal}.panel.innerpanel.tap.scrollviewer",
                        Components =
                        {
                            new CuiTextComponent() { Color = HEXToRGBA("#393C3C", 90), Text = (i+1).ToString(), FontSize = 12, Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf" },
                            new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"0 -{yStart + lineHeight}", OffsetMax = $"20 -{yStart}" }
                        }
                    }
                );

                yStart += lineHeight + linesGap;

                string linetext = textLines.Length > i ? textLines[i] : "";

                //Line text
                cont.Add(new CuiElement
                {
                    Parent = $"{texteditormodal}.panel.innerpanel.tap.scrollviewer.line",
                    Components =
                    {
                        new CuiInputFieldComponent { Align = TextAnchor.MiddleLeft, CharsLimit = 600, Command = $"{PREFIX_SHORT}.ui.texteditor ActionReply {triggerKey} {actionIndex} {listItemIndex} {i.ToString()}", FontSize = 14, Text = linetext, NeedsKeyboard = true, LineType = InputField.LineType.SingleLine },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "0 0" }
                    }
                });
            }

            CuiHelper.AddUi(player, cont);
        }

        private void UI_ShowChatPreviewMode(BasePlayer player, int interval = -1)
        {
            CuiElementContainer cont = new();

            //Parent panel
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                },
                Layer,
                chatpreview,
                chatpreview
            );

            //Bg panel 1
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "400 0", OffsetMax = "0 0" },
                    Image = { Color = HEXToRGBA("#000000", 90) }
                },
                chatpreview
            );

            //Bg panel 2
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -100", OffsetMax = "400 0" },
                    Image = { Color = HEXToRGBA("#000000", 90) }
                },
                chatpreview
            );

            //Bg panel 3
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "400 100" },
                    Image = { Color = HEXToRGBA("#000000", 90) }
                },
                chatpreview
            );

            //Title
            cont.Add(
                new CuiElement
                {
                    Parent = chatpreview,
                    Name = $"{chatpreview}.title",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = lang.GetMessage("UI_Edit_ChatPreviewMode_Title", this, player.UserIDString).ToUpper(), FontSize = 24, Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 25", OffsetMax = "200 100" }
                    }
                }
            );

            int exitWidth = 140;

            //Exit button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{exitWidth / 2} -{35}", OffsetMax = $"{exitWidth / 2} 0" },
                    Text = { Text = lang.GetMessage("UI_Edit_ChatPreviewMode_Exit", this, player.UserIDString).ToUpper(), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#90CAF3", 100f) },
                    Button = { Color = HEXToRGBA("#376E92", 95), Close = chatpreview, Command = $"{PREFIX_SHORT}.ui.cmd ExitChatPreview" }
                },
                $"{chatpreview}"
            );

            //Hide modalui
            cont.Add(new CuiElement { Name = uimodal, Components = { new CuiRectTransformComponent() { OffsetMin = "-5000 0", OffsetMax = "-5000 0" } }, Update = true });

            CuiHelper.AddUi(player, cont);

            if (interval >= 0)
            {
                _plugin.timer.Once(interval, () =>
                {
                    cont.Add(new CuiElement { Name = uimodal, Components = { new CuiRectTransformComponent() { OffsetMin = "0 0", OffsetMax = "0 0" } }, Update = true });
                    CuiHelper.AddUi(player, cont);
                });
            }
        }
        
        private void UI_ShowGameTipPreviewMode(BasePlayer player, int interval = -1)
        {
            CuiElementContainer cont = new();

            //Parent panel
            cont.Add(
                new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0.4", AnchorMax = "1 1" },
                    Image = { Color = HEXToRGBA("#000000", 90) }
                },
                Layer,
                gametippreview,
                gametippreview
            );

            //Title
            cont.Add(
                new CuiElement
                {
                    Parent = gametippreview,
                    Name = $"{gametippreview}.title",
                    Components =
                    {
                        new CuiTextComponent() { Color = HEXToRGBA("#DCDCDC", 100f), Text = lang.GetMessage("UI_Edit_GameTipPreviewMode_Title", this, player.UserIDString).ToUpper(), FontSize = 24, Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 25", OffsetMax = "200 100" }
                    }
                }
            );

            int exitWidth = 140;

            //Exit button
            cont.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{exitWidth / 2} -{35}", OffsetMax = $"{exitWidth / 2} 0" },
                    Text = { Text = lang.GetMessage("UI_Edit_GameTipPreviewMode_Exit", this, player.UserIDString).ToUpper(), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = HEXToRGBA("#90CAF3", 100f) },
                    Button = { Color = HEXToRGBA("#376E92", 95), Close = gametippreview, Command = $"{PREFIX_SHORT}.ui.cmd ExitGameTipPreview" }
                },
                $"{gametippreview}"
            );

            //Hide modalui
            cont.Add(new CuiElement { Name = uimodal, Components = { new CuiRectTransformComponent() { OffsetMin = "-5000 0", OffsetMax = "-5000 0" } }, Update = true });

            CuiHelper.AddUi(player, cont);

            if (interval >= 0)
            {
                _plugin.timer.Once(interval, () =>
                {
                    cont.Add(new CuiElement { Name = uimodal, Components = { new CuiRectTransformComponent() { OffsetMin = "0 0", OffsetMax = "0 0" } }, Update = true });
                    CuiHelper.AddUi(player, cont);
                });
            }
        }

        #region Commands

        [ChatCommand($"{PREFIX_SHORT}.edit")]
        void OpenAMEditor(BasePlayer player, string command, string[] args)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            UI_ShowEditor(player);
        }

        [ConsoleCommand($"{PREFIX_SHORT}.edit")]
        void OpenAMEditorConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            int selectedIndex = 0;
            if (arg.HasArgs(2))
            {
                selectedIndex = int.Parse(arg.Args[1]);
            }

            UI_ShowEditor(player);
        }

        [ConsoleCommand($"{PREFIX_SHORT}.edit.savecancel")]
        void AMEditorSaveCancelConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            if (!arg.HasArgs(1))
                return;

            bool shouldSave = false;
            bool clearSession = false;

            switch (arg.Args[0])
            {
                case "save":
                    shouldSave = true;
                    break;
                case "saveexit":
                    shouldSave = true;
                    clearSession = true;
                    CuiHelper.DestroyUi(player, uimodal);
                    break;
                case "cancel":
                    clearSession = true;
                    CuiHelper.DestroyUi(player, uimodal);
                    break;
            }

            if (shouldSave)
            {
                UISession uiSession;

                if (UISessions.TryGetValue(player.userID, out uiSession))
                {
                    UpdateConfigFromUISession(uiSession);
                }
            }

            if (clearSession)
                UISessions.Remove(player.userID);
        }

        [ConsoleCommand($"{PREFIX_SHORT}.ui.cmd")]
        void AMEditorSelectViewConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            //PrintWarning(string.Join(", ", arg.Args));

            if (!arg.HasArgs(1))
                return;

            UITrigger uiTrigger;
            int actionIndex = -1;
            PluginConfig.ConfigAction uiAction;
            int listItemIndex = -1;
            CuiElementContainer cont = new();
            Lang_Action langAction;

            switch (arg.Args[0])
            {
                case "GoToTriggers":
                    CuiHelper.DestroyUi(player, $"{uimodal}.actionsview");
                    CuiHelper.DestroyUi(player, $"{uimodal}.langsview"); 
                    UI_ShowTriggersView(player);
                    break;
                case "GoToActions":
                    if (!arg.HasArgs(2))
                        return;

                    CuiHelper.DestroyUi(player, $"{uimodal}.triggersview");
                    UI_ShowActionsView(player, arg.Args[1]);
                    break;
                case "GoToActionForm":
                    if (!arg.HasArgs(3))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    int selected = int.Parse(arg.Args[2]);

                    for (int i = 0; i < uiTrigger.ConfigActions.Count; i++)
                        if (i != selected)
                            cont.Add(new CuiElement { Name = $"{uimodal}.actionsview.list.actionlist.{i}", Components = { new CuiImageComponent() { Color = HEXToRGBA("#393C3C", 95) } }, Update = true });

                    cont.Add(new CuiElement { Name = $"{uimodal}.actionsview.list.actionlist.{selected}", Components = { new CuiImageComponent() { Color = HEXToRGBA("#5A6060", 95) } }, Update = true });

                    CuiHelper.AddUi(player, cont);

                    UI_ShowSelectedAction(player, uiTrigger, selected, UISessions[player.userID].SelectedRepliesLang);
                    break;
                case "AddNewAction":
                    if (!arg.HasArgs(2))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    uiAction = new();
                    uiAction.Type = uiTrigger.AMTrigger.Key;
                    uiAction.Groups.Add("default");

                    foreach (string language in _config.ServerLangs)
                        uiTrigger.LangsTriggers[language].Actions.Add(uiAction.Id, new());

                    uiTrigger.ConfigActions.Add(uiAction);
                    UI_ShowActionsList(player, uiTrigger, UISessions[player.userID].SelectedRepliesLang, uiTrigger.ConfigActions.Count - 1);
                    break;
                case "DuplicateAction":
                    if (!arg.HasArgs(3))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    actionIndex = int.Parse(arg.Args[2]);

                    uiAction = GetUIAction(uiTrigger, actionIndex);
                    if (uiAction == null)
                        return;

                    PluginConfig.ConfigAction clonedAction = uiAction.Clone(true);

                    foreach (string language in _config.ServerLangs)
                        uiTrigger.LangsTriggers[language].Actions.Add(clonedAction.Id, uiTrigger.LangsTriggers[language].Actions[uiAction.Id].Clone());

                    uiTrigger.ConfigActions.Add(clonedAction);
                    UI_ShowActionsList(player, uiTrigger, UISessions[player.userID].SelectedRepliesLang, uiTrigger.ConfigActions.Count - 1);
                    break;
                case "DeleteAction":
                    if (!arg.HasArgs(3))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    actionIndex = int.Parse(arg.Args[2]);

                    uiAction = GetUIAction(uiTrigger, actionIndex);
                    if (uiAction == null)
                        return;

                    foreach (string language in _config.ServerLangs)
                        uiTrigger.LangsTriggers[language].Actions.Remove(uiAction.Id);

                    uiTrigger.ConfigActions.Remove(uiAction);

                    UI_ShowActionsList(player, uiTrigger, UISessions[player.userID].SelectedRepliesLang);
                    break;
                case "ActionCB":
                    if (!arg.HasArgs(4))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    actionIndex = int.Parse(arg.Args[2]);

                    uiAction = GetUIAction(uiTrigger, actionIndex);
                    if (uiAction == null)
                        return;

                    switch (arg.Args[3])
                    {
                        case "Enabled":
                            uiAction.Enabled = !uiAction.Enabled;

                            UI_CheckBox_Update(uiAction.Enabled, nameof(PluginConfig.ConfigAction.Enabled), cont, "Active", "Inactive");
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "SendInChat":
                            uiAction.SendInChat = !uiAction.SendInChat;

                            UI_CheckBox_Update(uiAction.SendInChat, nameof(PluginConfig.ConfigAction.SendInChat), cont);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "SendAsGameTip":
                            uiAction.SendAsGameTip = !uiAction.SendAsGameTip;

                            UI_CheckBox_Update(uiAction.SendAsGameTip, nameof(PluginConfig.ConfigAction.SendAsGameTip), cont);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "IsGlobalBroadcast":
                            uiAction.IsGlobalBroadcast = !uiAction.IsGlobalBroadcast;

                            UI_CheckBox_Update(uiAction.IsGlobalBroadcast, nameof(PluginConfig.ConfigAction.IsGlobalBroadcast), cont);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "PlayerCanDisable":
                            uiAction.PlayerCanDisable = !uiAction.PlayerCanDisable;

                            UI_CheckBox_Update(uiAction.PlayerCanDisable, nameof(PluginConfig.ConfigAction.PlayerCanDisable), cont);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "DontTriggerAdmin":
                            uiAction.DontTriggerAdmin = !uiAction.DontTriggerAdmin;

                            UI_CheckBox_Update(uiAction.DontTriggerAdmin, nameof(PluginConfig.ConfigAction.DontTriggerAdmin), cont);
                            CuiHelper.AddUi(player, cont);
                            break;
                    }
                    break;
                case "ActionDeleteListItem":
                    if (!arg.HasArgs(5))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    actionIndex = int.Parse(arg.Args[2]);

                    uiAction = GetUIAction(uiTrigger, actionIndex);
                    if (uiAction == null)
                        return;

                    langAction = uiTrigger.LangsTriggers[UISessions[player.userID].SelectedRepliesLang].Actions[uiAction.Id];

                    switch (arg.Args[3])
                    {
                        case "Replies":
                            listItemIndex = int.Parse(arg.Args[4]);
                            uiTrigger.LangsTriggers[UISessions[player.userID].SelectedRepliesLang].Actions[uiAction.Id].Replies.RemoveAt(listItemIndex);

                            UI_Listbox_Update(player, nameof(Lang_Action.Replies), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", langAction.Replies, cont, minItemsInView: 3, previewButton: true);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "Permissions":
                            listItemIndex = int.Parse(arg.Args[4]);
                            uiAction.Permissions.RemoveAt(listItemIndex);

                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.Permissions), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.Permissions, cont);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "Groups":
                            listItemIndex = int.Parse(arg.Args[4]);
                            uiAction.Groups.RemoveAt(listItemIndex);

                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.Groups), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.Groups, cont);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "BlacklistedPerms":
                            listItemIndex = int.Parse(arg.Args[4]);
                            uiAction.BlacklistedPerms.RemoveAt(listItemIndex);

                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.BlacklistedPerms), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.BlacklistedPerms, cont);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "BlacklistedGroups":
                            listItemIndex = int.Parse(arg.Args[4]);
                            uiAction.BlacklistedGroups.RemoveAt(listItemIndex);

                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.BlacklistedGroups), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.BlacklistedGroups, cont);
                            CuiHelper.AddUi(player, cont);
                            break;
                    }
                    break;
                case "ActionAddListItem":
                    if (!arg.HasArgs(4))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    actionIndex = int.Parse(arg.Args[2]);

                    uiAction = GetUIAction(uiTrigger, actionIndex);
                    if (uiAction == null)
                        return;

                    langAction = uiTrigger.LangsTriggers[UISessions[player.userID].SelectedRepliesLang].Actions[uiAction.Id];

                    switch (arg.Args[3])
                    {
                        case "Replies":
                            langAction.Replies.Add("<color=yellow>Tips</color> for writing a reply message:\nThis text is in a new line.\n\tThis text is indented.");

                            UI_Listbox_Update(player, nameof(Lang_Action.Replies), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", langAction.Replies, cont, scrollToBottom: true, minItemsInView: 3, previewButton: true);
                            CuiHelper.AddUi(player, cont);
                            listItemIndex = langAction.Replies.Count - 1;
                            UI_ShowMultilineTextEditor(player, langAction.Replies.Last(), "Reply Editor", uiTrigger.AMTrigger.Key, actionIndex, listItemIndex);
                            break;
                        case "Permissions":
                            uiAction.Permissions.Add("pluginname.permission");

                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.Permissions), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.Permissions, cont, scrollToBottom: true);
                            CuiHelper.AddUi(player, cont);
                            listItemIndex = uiAction.Permissions.Count - 1;
                            UI_ShowTextEditor(player, uiAction.Permissions.Last(), "Permission Name", uiTrigger.AMTrigger.Key, actionIndex, nameof(PluginConfig.ConfigAction.Permissions), listItemIndex);
                            break;
                        case "Groups":
                            uiAction.Groups.Add("");

                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.Groups), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.Groups, cont, scrollToBottom: true);
                            CuiHelper.AddUi(player, cont);
                            listItemIndex = uiAction.Groups.Count - 1;
                            UI_ShowTextEditor(player, uiAction.Groups.Last(), "Group Name", uiTrigger.AMTrigger.Key, actionIndex, nameof(PluginConfig.ConfigAction.Groups), listItemIndex);
                            break;
                        case "BlacklistedPerms":
                            uiAction.BlacklistedPerms.Add("pluginname.permission");

                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.BlacklistedPerms), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.BlacklistedPerms, cont, scrollToBottom: true);
                            CuiHelper.AddUi(player, cont);
                            listItemIndex = uiAction.BlacklistedPerms.Count - 1;
                            UI_ShowTextEditor(player, uiAction.BlacklistedPerms.Last(), "Permission Name", uiTrigger.AMTrigger.Key, actionIndex, nameof(PluginConfig.ConfigAction.BlacklistedPerms), listItemIndex);
                            break;
                        case "BlacklistedGroups":
                            uiAction.BlacklistedGroups.Add("");

                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.BlacklistedGroups), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.BlacklistedGroups, cont, scrollToBottom: true);
                            CuiHelper.AddUi(player, cont);
                            listItemIndex = uiAction.BlacklistedGroups.Count - 1;
                            UI_ShowTextEditor(player, uiAction.BlacklistedGroups.Last(), "Group Name", uiTrigger.AMTrigger.Key, actionIndex, nameof(PluginConfig.ConfigAction.BlacklistedGroups), listItemIndex);
                            break;
                    }
                    break;
                case "ActionSelectListItem":
                    if (!arg.HasArgs(5))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    actionIndex = int.Parse(arg.Args[2]);

                    uiAction = GetUIAction(uiTrigger, actionIndex);
                    if (uiAction == null)
                        return;

                    langAction = uiTrigger.LangsTriggers[UISessions[player.userID].SelectedRepliesLang].Actions[uiAction.Id];

                    switch (arg.Args[3])
                    {
                        case "Replies":
                            listItemIndex = int.Parse(arg.Args[4]);
                            UI_ShowMultilineTextEditor(player, langAction.Replies[listItemIndex], "Reply Editor", uiTrigger.AMTrigger.Key, actionIndex, listItemIndex);
                            break;
                        case "Permissions":
                            listItemIndex = int.Parse(arg.Args[4]);
                            UI_ShowTextEditor(player, uiAction.Permissions[listItemIndex], "Permission Name", uiTrigger.AMTrigger.Key, actionIndex, nameof(PluginConfig.ConfigAction.Permissions), listItemIndex);
                            break;
                        case "Groups":
                            listItemIndex = int.Parse(arg.Args[4]);
                            UI_ShowTextEditor(player, uiAction.Groups[listItemIndex], "Group Name", uiTrigger.AMTrigger.Key, actionIndex, nameof(PluginConfig.ConfigAction.Groups), listItemIndex);
                            break;
                        case "BlacklistedPerms":
                            listItemIndex = int.Parse(arg.Args[4]);
                            UI_ShowTextEditor(player, uiAction.BlacklistedPerms[listItemIndex], "Permission Name", uiTrigger.AMTrigger.Key, actionIndex, nameof(PluginConfig.ConfigAction.BlacklistedPerms), listItemIndex);
                            break;
                        case "BlacklistedGroups":
                            listItemIndex = int.Parse(arg.Args[4]);
                            UI_ShowTextEditor(player, uiAction.BlacklistedGroups[listItemIndex], "Group Name", uiTrigger.AMTrigger.Key, actionIndex, nameof(PluginConfig.ConfigAction.BlacklistedGroups), listItemIndex);
                            break;
                    }
                    break;
                case "ActionRefreshList":
                    if (!arg.HasArgs(5))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    actionIndex = int.Parse(arg.Args[2]);

                    uiAction = GetUIAction(uiTrigger, actionIndex);
                    if (uiAction == null)
                        return;

                    langAction = uiTrigger.LangsTriggers[UISessions[player.userID].SelectedRepliesLang].Actions[uiAction.Id];

                    switch (arg.Args[3])
                    {
                        case "Replies":
                            UI_Listbox_Update(player, nameof(Lang_Action.Replies), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", langAction.Replies, cont, scrollToBottom: true, minItemsInView: 3, previewButton: true);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "Permissions":
                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.Permissions), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.Permissions, cont, scrollToBottom: true);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "Groups":
                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.Groups), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.Groups, cont, scrollToBottom: true);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "BlacklistedPerms":
                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.BlacklistedPerms), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.BlacklistedPerms, cont, scrollToBottom: true);
                            CuiHelper.AddUi(player, cont);
                            break;
                        case "BlacklistedGroups":
                            UI_Listbox_Update(player, nameof(PluginConfig.ConfigAction.BlacklistedGroups), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", uiAction.BlacklistedGroups, cont, scrollToBottom: true);
                            CuiHelper.AddUi(player, cont);
                            break;
                    }
                    break;
                case "ActionPreviewReply":
                    if (!arg.HasArgs(5))
                        return;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[1]);
                    if (uiTrigger == null)
                        return;

                    actionIndex = int.Parse(arg.Args[2]);

                    uiAction = GetUIAction(uiTrigger, actionIndex);
                    if (uiAction == null)
                        return;

                    langAction = uiTrigger.LangsTriggers[UISessions[player.userID].SelectedRepliesLang].Actions[uiAction.Id];

                    switch (arg.Args[3])
                    {
                        case "Replies":
                            listItemIndex = int.Parse(arg.Args[4]);

                            bool isGameTip = false;

                            if (arg.HasArgs(6))
                                isGameTip = arg.Args[5] == "gametip";
                            else if(!uiAction.SendInChat && uiAction.SendAsGameTip)
                                isGameTip = true;

                            if (isGameTip)
                            {
                                UI_ShowGameTipPreviewMode(player);
                                timer.Once(0.5f, () =>
                                {
                                    SendMessage(player, FormatMessage(player, langAction.Replies[listItemIndex]), false, true);
                                });
                            }
                            else
                            {
                                SendMessage(player, FormatMessage(player, langAction.Replies[listItemIndex]));
                                UI_ShowChatPreviewMode(player);
                            }
                            
                            break;
                    }
                    break;
                case "ExitChatPreview":
                    CuiHelper.DestroyUi(player, chatpreview);
                    cont.Add(new CuiElement { Name = uimodal, Components = { new CuiRectTransformComponent() { OffsetMin = "0 0", OffsetMax = "0 0" } }, Update = true });
                    CuiHelper.AddUi(player, cont);
                    break;
                case "ExitGameTipPreview":
                    CuiHelper.DestroyUi(player, gametippreview);
                    cont.Add(new CuiElement { Name = uimodal, Components = { new CuiRectTransformComponent() { OffsetMin = "0 0", OffsetMax = "0 0" } }, Update = true });
                    CuiHelper.AddUi(player, cont);
                    break;
                case "ChangeRepliesLang":
                    if (!arg.HasArgs(4))
                        return;

                    string newLang = arg.Args[1];
                    if(UISessions[player.userID].SelectedRepliesLang == newLang)
                        return;
                    UISessions[player.userID].SelectedRepliesLang = newLang;

                    uiTrigger = GetUITrigger(player.userID, arg.Args[2]);
                    if (uiTrigger == null)
                        return;

                    actionIndex = int.Parse(arg.Args[3]);

                    uiAction = GetUIAction(uiTrigger, actionIndex);
                    if (uiAction == null)
                        return;

                    langAction = uiTrigger.LangsTriggers[UISessions[player.userID].SelectedRepliesLang].Actions[uiAction.Id];

                    UI_ShowRepliesLanguages(player, newLang, uiTrigger, actionIndex);
                    UI_Listbox_Update(player, nameof(Lang_Action.Replies), $"{PREFIX_SHORT}.ui.cmd %cmdname% {uiTrigger.AMTrigger.Key} {actionIndex} %index%", langAction.Replies, cont, scrollToBottom: true, minItemsInView: 3, previewButton: true);
                    CuiHelper.AddUi(player, cont);
                    break;
            }
        }

        [ConsoleCommand($"{PREFIX_SHORT}.ui.textboxcmd")]
        void AMEditorTextboxConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            if (!arg.HasArgs(4))
                return;

            string text = string.Join(" ", arg.Args.Skip(4));

            UITrigger uiTrigger = GetUITrigger(player.userID, arg.Args[0]);
            if (uiTrigger == null)
                return;

            int actionIndex = int.Parse(arg.Args[1]);

            PluginConfig.ConfigAction uiAction = GetUIAction(uiTrigger, actionIndex);
            if (uiAction == null)
                return;

            int listItemIndex = -1;

            switch (arg.Args[2])
            {
                case "Permissions":
                    listItemIndex = int.Parse(arg.Args[3]);
                    uiAction.Permissions[listItemIndex] = text;
                    break;
                case "Groups":
                    listItemIndex = int.Parse(arg.Args[3]);
                    uiAction.Groups[listItemIndex] = text;
                    break;
                case "BlacklistedPerms":
                    listItemIndex = int.Parse(arg.Args[3]);
                    uiAction.BlacklistedPerms[listItemIndex] = text;
                    break;
                case "BlacklistedGroups":
                    listItemIndex = int.Parse(arg.Args[3]);
                    uiAction.BlacklistedGroups[listItemIndex] = text;
                    break;
            }
        }

        private void LongInputCMD(IPlayer user, string command, string[] args)
        {
            var player = user.Object as BasePlayer;
            if (player == null) return;

            UITrigger uiTrigger = GetUITrigger(player.userID, args[1]);
            if (uiTrigger == null)
                return;

            int actionIndex = int.Parse(args[2]);
            PluginConfig.ConfigAction uiAction = GetUIAction(uiTrigger, actionIndex);
            if (uiAction == null)
                return;

            Lang_Action langAction = uiTrigger.LangsTriggers[UISessions[player.userID].SelectedRepliesLang].Actions[uiAction.Id];

            string text = "";

            switch (args[0])
            {
                case "ActionReply":
                    text = string.Join(" ", args.Skip(5));

                    int listItemIndex = int.Parse(args[3]);
                    int lineIndex = int.Parse(args[4]);

                    List<string> lines = langAction.Replies[listItemIndex].Split('\n').ToList();

                    if (lines.Count <= lineIndex)
                    {
                        int diff = (lineIndex + 1) - lines.Count;
                        for (int i = 0; i < diff; i++)
                            lines.Add("");
                    }
                    lines[lineIndex] = text.Replace("\\t", "\t");

                    string replymodal = $"{uimodal}.replyeditor";
                    string finalText = string.Join("\n", lines).TrimEnd(new char[] { '\n', ' ' });

                    langAction.Replies[listItemIndex] = finalText;

                    CuiElementContainer cont = new();
                    cont.Add(new CuiElement { Name = $"{replymodal}.panel.innerpanel.preview.text", Components = { new CuiTextComponent() { Text = finalText.Replace("\t", "".PadLeft(4)) } }, Update = true });
                    CuiHelper.AddUi(player, cont);
                    break;
                case "ActionTextbox":
                    text = string.Join(" ", args.Skip(4));
                    switch (args[3])
                    {
                        case "Interval":
                            int.TryParse(text, out uiAction.Interval);
                            UI_UpdateSelectedActionInList(player, uiTrigger, actionIndex);
                            break;
                        case "Target":
                            uiAction.Target = text;
                            UI_UpdateSelectedActionInList(player, uiTrigger, actionIndex);
                            break;
                    }
                    break;
            }
        }

        #endregion

        #endregion
    }
}

#region Extension Methods

namespace Oxide.Plugins.AutomatedMessagesMethods
{
    public static class ExtensionMethods
    {
        //Some copied from AdminRadar
        public static T ElementAt<T>(this IEnumerable<T> a, int b) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == 0) { return c.Current; } b--; } } return default(T); }
        public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == null || b(c.Current)) { return c.Current; } } } return default(T); }
        public static List<T> ToList<T>(this IEnumerable<T> a, Func<T, bool> b = null) { var c = new List<T>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b == null || b(d.Current)) { c.Add(d.Current); } } } return c; }
        public static string[] ToLower(this IEnumerable<string> a, Func<string, bool> b = null) { var c = new List<string>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b == null || b(d.Current)) { c.Add(d.Current.ToLower()); } } } return c.ToArray(); }
        public static T[] Take<T>(this IList<T> a, int b) { var c = new List<T>(); for (int i = 0; i < a.Count; i++) { if (c.Count == b) { break; } c.Add(a[i]); } return c.ToArray(); }
        public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b) { var c = new List<V>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { c.Add(b(d.Current)); } } return c; }
        public static T[] Where<T>(this IEnumerable<T> a, Func<T, bool> b) { var c = new List<T>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b(d.Current)) { c.Add(d.Current); } } } return c.ToArray(); }
        public static IEnumerable<T> OfType<T>(this IEnumerable<object> a) { foreach (object b in a) { if (b is T) { yield return (T)b; } } }
        public static float Sum<T>(this IEnumerable<T> a, Func<T, float> b) { float c = 0; if (a == null) return c; foreach (T d in a) { if (d == null) continue; c = checked(c + b(d)); } return c; }
        public static int Sum<T>(this IEnumerable<T> a, Func<T, int> b) { int c = 0; if (a == null) return c; foreach (T d in a) { if (d == null) continue; c = checked(c + b(d)); } return c; }
        public static bool Any<TSource>(this IEnumerable<TSource> a, Func<TSource, bool> b) { if (a == null) return false; using (var c = a.GetEnumerator()) { while (c.MoveNext()) if (b(c.Current)) return true; } return false; }
        public static string[] Skip(this string[] a, int b) { if (a.Length == 0 || b >= a.Length) { return Array.Empty<string>(); } int n = a.Length - b; string[] c = new string[n]; Array.Copy(a, b, c, 0, n); return c; }
        public static TSource Last<TSource>(this IList<TSource> a) => a[a.Count - 1];
    }
}

#endregion