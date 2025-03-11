using Oxide.Plugins.BetterChatExtensions;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

#if RUST
using ConVar;
using Facepunch;
using Facepunch.CardGames;
using Facepunch.Math;
using CompanionServer;
#endif

// TODO: Reduce garbage creation
// TODO: Improve string usage by using stringbuilders
// TODO: Add "name" or "identifier" format for third-party plugins to obtain a formatted identifier

namespace Oxide.Plugins
{
    [Info("Better Chat", "LaserHydra", "5.2.14")]
    [Description("Allows to manage chat groups, customize colors and add titles.")]
    internal class BetterChat : CovalencePlugin
    {
        #region Fields

        private static BetterChat _instance;

        private Configuration _config;
        private List<ChatGroup> _chatGroups;
        private Dictionary<Plugin, Func<IPlayer, string>> _thirdPartyTitles = new Dictionary<Plugin, Func<IPlayer, string>>();

        private static readonly string[] _stringReplacements = new string[]
        {
#if RUST || HURTWORLD || UNTURNED
            "<b>", "</b>",
            "<i>", "</i>",
            "</size>",
            "</color>"
#endif
        };

        private static readonly Regex[] _regexReplacements = new Regex[]
        {
            new Regex(@"<voffset=(?:.|\s)*?>", RegexOptions.Compiled),
#if RUST || HURTWORLD || UNTURNED
            new Regex(@"<color=.+?>", RegexOptions.Compiled),
            new Regex(@"<size=.+?>", RegexOptions.Compiled),
#elif REIGNOFKINGS || SEVENDAYSTODIE
            new Regex(@"\[[\w\d]{6}\]", RegexOptions.Compiled),
#elif RUSTLEGACY
            new Regex(@"\[color #[\w\d]{6}\]", RegexOptions.Compiled),
#elif TERRARIA
            new Regex(@"\[c\/[\w\d]{6}:", RegexOptions.Compiled),
#endif
        };

        #endregion

        #region Hooks

        private void Loaded()
        {
            _instance = this;

            LoadData(ref _chatGroups);

            if (_chatGroups.Count == 0)
                _chatGroups.Add(new ChatGroup("default"));

            foreach (ChatGroup group in _chatGroups)
            {
                if (!permission.GroupExists(group.GroupName))
                    permission.CreateGroup(group.GroupName, string.Empty, 0);
            }

            SaveData(_chatGroups);
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (_thirdPartyTitles.ContainsKey(plugin))
                _thirdPartyTitles.Remove(plugin);
        }

#if RUST
        private object OnPlayerChat(BasePlayer bplayer, string message, Chat.ChatChannel chatchannel)
        {
            IPlayer player = bplayer.IPlayer;
#else
        private object OnUserChat(IPlayer player, string message)
        {
#endif
            if (message.Length > _instance._config.MaxMessageLength)
                message = message.Substring(0, _instance._config.MaxMessageLength);

            BetterChatMessage chatMessage = ChatGroup.PrepareMessage(player, message);
            if (chatMessage == null)
                return null;

#if RUST
            BetterChatMessage.CancelOptions result = SendBetterChatMessage(chatMessage, chatchannel);
#else
            BetterChatMessage.CancelOptions result = SendBetterChatMessage(chatMessage);
#endif

            switch (result)
            {
                case BetterChatMessage.CancelOptions.None:
                case BetterChatMessage.CancelOptions.BetterChatAndDefault:
                    return true;
            }

            return null;
        }

        #endregion

        #region Messaging

#if RUST
        private BetterChatMessage.CancelOptions SendBetterChatMessage(BetterChatMessage chatMessage, Chat.ChatChannel chatchannel)
#else
        private BetterChatMessage.CancelOptions SendBetterChatMessage(BetterChatMessage chatMessage)
#endif
        {
            Dictionary<string, object> chatMessageDict = chatMessage.ToDictionary();
#if RUST
            chatMessageDict.Add("ChatChannel", chatchannel);
#endif
            foreach (Plugin plugin in plugins.GetAll())
            {
                object hookResult = plugin.CallHook("OnBetterChat", chatMessageDict);

                if (hookResult is Dictionary<string, object>)
                {
                    try
                    {
                        chatMessageDict = hookResult as Dictionary<string, object>;
                    }
                    catch (Exception e)
                    {
                        PrintError($"Failed to load modified OnBetterChat hook data from plugin '{plugin.Title} ({plugin.Version})':{Environment.NewLine}{e}");
                        continue;
                    }
                }
                else if (hookResult != null)
                    return BetterChatMessage.CancelOptions.BetterChatOnly;
            }

            chatMessage = BetterChatMessage.FromDictionary(chatMessageDict);

            if (chatMessage.CancelOption != BetterChatMessage.CancelOptions.None)
            {
                return chatMessage.CancelOption;
            }

            var output = chatMessage.GetOutput();

#if RUST
            BasePlayer basePlayer = chatMessage.Player.Object as BasePlayer;

            switch (chatchannel)
            {
                case Chat.ChatChannel.Team:
                    RelationshipManager.PlayerTeam team = basePlayer.Team;
                    if (team == null || team.members.Count == 0)
                    {
                        throw new InvalidOperationException("Chat channel is set to Team, however the player is not in a team.");
                    }

                    team.BroadcastTeamChat(basePlayer.userID, chatMessage.Player.Name, chatMessage.Message, chatMessage.UsernameSettings.Color);

                    List<Network.Connection> onlineMemberConnections = team.GetOnlineMemberConnections();
                    if (onlineMemberConnections != null)
                    {
                        ConsoleNetwork.SendClientCommand(onlineMemberConnections, "chat.add", (int) chatchannel, chatMessage.Player.Id, output.Chat);
                    }
                    break;

                case Chat.ChatChannel.Cards:
                    BaseCardGameEntity baseCardGame = basePlayer.GetMountedVehicle() as BaseCardGameEntity;

                    if (baseCardGame == null /* || !cardTable.GameController.PlayerIsInGame(basePlayer) */)
                    {
                       throw new InvalidOperationException("Chat channel is set to Cards, however the player is not in a participating in a card game.");
                    }

                    List<Network.Connection> list = Facepunch.Pool.GetList<Network.Connection>();

                    foreach (CardPlayerData playerData in baseCardGame.GameController.PlayerData)
                    {
                        if (playerData.HasUser)
                        {
                            list.Add(BasePlayer.FindByID(playerData.UserID).net.connection);
                        }
                    }

                    if (list.Count > 0)
                    {
                        ConsoleNetwork.SendClientCommand(list, "chat.add", (int) chatchannel, chatMessage.Player.Id, output.Chat);
                    }

                    Facepunch.Pool.FreeList(ref list);
                    break;

                case Chat.ChatChannel.Local: 
                    float localRange = Chat.localChatRange * Chat.localChatRange;
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        if (chatMessage.BlockedReceivers.Contains(player.UserIDString))
                        {
                            continue;
                        }
                        
                        if ((basePlayer.transform.position - player.transform.position).sqrMagnitude <= localRange)
                        {
                            player.SendConsoleCommand("chat.add", (int)chatchannel, chatMessage.Player.Id, output.Chat);
                        }
                    }

                    break;
                
                default:
                    foreach (BasePlayer p in BasePlayer.activePlayerList.Where(p => !chatMessage.BlockedReceivers.Contains(p.UserIDString)))
                        p.SendConsoleCommand("chat.add", (int) chatchannel, chatMessage.Player.Id, output.Chat);
                    break;
            }
#else
            foreach (IPlayer p in players.Connected.Where(p => !chatMessage.BlockedReceivers.Contains(p.Id)))
                p.Message(output.Chat);
#endif

#if RUST
            Puts($"[{chatchannel}] {output.Console}");

            var chatEntry = new Chat.ChatEntry
            {
                Channel = chatchannel,
                Message = output.Console,
                UserId = chatMessage.Player.Id,
                Username = chatMessage.Player.Name,
                Color = chatMessage.UsernameSettings.Color,
                Time = Epoch.Current
            };

            Chat.Record(chatEntry);
#else
            Puts(output.Console);
#endif

            return chatMessage.CancelOption;
        }

        #endregion

        #region API

        private bool API_AddGroup(string group)
        {
            if (ChatGroup.Find(group) != null)
                return false;

            _chatGroups.Add(new ChatGroup(group));
            SaveData(_chatGroups);

            return true;
        }

        private List<JObject> API_GetAllGroups() => _chatGroups.ConvertAll(JObject.FromObject);

        private List<JObject> API_GetUserGroups(IPlayer player) => ChatGroup.GetUserGroups(player).ConvertAll(JObject.FromObject);

        private bool API_GroupExists(string group) => ChatGroup.Find(group) != null;

        private ChatGroup.Field.SetValueResult? API_SetGroupField(string group, string field, string value) => ChatGroup.Find(group)?.SetField(field, value);

        private Dictionary<string, object> API_GetGroupFields(string group) => ChatGroup.Find(group)?.GetFields() ?? new Dictionary<string, object>();

        private Dictionary<string, object> API_GetMessageData(IPlayer player, string message) => ChatGroup.PrepareMessage(player, message).ToDictionary();

        private string API_GetFormattedUsername(IPlayer player)
        {
            var primary = ChatGroup.GetUserPrimaryGroup(player);

            // Player has no groups - this should never happen
            if (primary == null)
                return player.Name;

            return $"[#{primary.Username.GetUniversalColor()}][+{primary.Username.Size}]{player.Name}[/+][/#]";
        }

        private string API_GetFormattedMessage(IPlayer player, string message, bool console = false)
        {
            var output = ChatGroup.PrepareMessage(player, message).GetOutput();

            return console ? output.Console : output.Chat;
        }

        private BetterChatMessage.CancelOptions API_SendMessage(Dictionary<string, object> betterChatMessageDict, int chatChannel = 0)
        {
#if RUST
            return SendBetterChatMessage(BetterChatMessage.FromDictionary(betterChatMessageDict), (Chat.ChatChannel)chatChannel);
#else
            return SendBetterChatMessage(BetterChatMessage.FromDictionary(betterChatMessageDict));
#endif
        }

        private void API_RegisterThirdPartyTitle(Plugin plugin, Func<IPlayer, string> titleGetter) => _thirdPartyTitles[plugin] = titleGetter;

        #endregion

        #region Commands

        [Command("chat"), Permission("betterchat.admin")]
        private void CmdChat(IPlayer player, string cmd, string[] args)
        {
            cmd = player.LastCommand == CommandType.Console ? cmd : $"/{cmd}";

            if (args.Length == 0)
            {
                player.Reply($"{cmd} group <add|remove|set|list>");
                player.Reply($"{cmd} user <add|remove>");
                return;
            }

            string argsStr = string.Join(" ", args);

            var commands = new Dictionary<string, Action<string[]>>
            {
                ["group add"] = a => {
                    if (a.Length != 1)
                    {
                        player.Reply($"Syntax: {cmd} group add <group>");
                        return;
                    }

                    string groupName = a[0].ToLower();

                    if (ChatGroup.Find(groupName) != null)
                    {
                        player.ReplyLang("Group Already Exists", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    ChatGroup group = new ChatGroup(groupName);

                    _chatGroups.Add(group);

                    if (!permission.GroupExists(group.GroupName))
                        permission.CreateGroup(group.GroupName, string.Empty, 0);

                    SaveData(_chatGroups);

                    player.ReplyLang("Group Added", new KeyValuePair<string, string>("group", groupName));
                },
                ["group remove"] = a => {
                    if (a.Length != 1)
                    {
                        player.Reply($"Syntax: {cmd} group remove <group>");
                        return;
                    }

                    string groupName = a[0].ToLower();
                    ChatGroup group = ChatGroup.Find(groupName);

                    if (group == null)
                    {
                        player.ReplyLang("Group Does Not Exist", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    _chatGroups.Remove(group);
                    SaveData(_chatGroups);

                    player.ReplyLang("Group Removed", new KeyValuePair<string, string>("group", groupName));
                },
                ["group set"] = a => {
                    if (a.Length != 3)
                    {
                        player.Reply($"Syntax: {cmd} group set <group> <field> <value>");
                        player.Reply($"Fields:{Environment.NewLine}{string.Join(", ", ChatGroup.Fields.Select(kvp => $"({kvp.Value.UserFriendyType}) {kvp.Key}").ToArray())}");
                        return;
                    }

                    string groupName = a[0].ToLower();
                    ChatGroup group = ChatGroup.Find(groupName);

                    if (group == null)
                    {
                        player.ReplyLang("Group Does Not Exist", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    string field = a[1];
                    string strValue = a[2];

                    switch (group.SetField(field, strValue))
                    {
                        case ChatGroup.Field.SetValueResult.Success:
                            SaveData(_chatGroups);
                            player.ReplyLang("Group Field Changed", new Dictionary<string, string> { ["group"] = group.GroupName, ["field"] = field, ["value"] = strValue });
                            break;

                        case ChatGroup.Field.SetValueResult.InvalidField:
                            player.ReplyLang("Invalid Field", new KeyValuePair<string, string>("field", field));
                            break;

                        case ChatGroup.Field.SetValueResult.InvalidValue:
                            player.ReplyLang("Invalid Value", new Dictionary<string, string> { ["field"] = field, ["value"] = strValue, ["type"] = ChatGroup.Fields[field].UserFriendyType });
                            break;
                    }
                },
                ["group list"] = a =>
                {
                    player.Reply(string.Join(", ", _chatGroups.Select(g => g.GroupName).ToArray()));
                },
                ["group"] = a => player.Reply($"Syntax: {cmd} group <add|remove|set|list>"),
                ["user add"] = a => {
                    if (a.Length != 2)
                    {
                        player.Reply($"Syntax: {cmd} user add <username|id> <group>");
                        return;
                    }

                    string response;
                    IPlayer targetPlayer = FindPlayer(a[0], out response);

                    if (targetPlayer == null)
                    {
                        player.Reply(response);
                        return;
                    }

                    string groupName = a[1].ToLower();
                    ChatGroup group = ChatGroup.Find(groupName);

                    if (group == null)
                    {
                        player.ReplyLang("Group Does Not Exist", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    if (permission.UserHasGroup(targetPlayer.Id, groupName))
                    {
                        player.ReplyLang("Player Already In Group", new Dictionary<string, string> { ["player"] = targetPlayer.Name, ["group"] = groupName });
                        return;
                    }

                    group.AddUser(targetPlayer);
                    player.ReplyLang("Added To Group", new Dictionary<string, string> { ["player"] = targetPlayer.Name, ["group"] = groupName });
                },
                ["user remove"] = a => {
                    if (a.Length != 2)
                    {
                        player.Reply($"Syntax: {cmd} user remove <username|id> <group>");
                        return;
                    }

                    string response;
                    IPlayer targetPlayer = FindPlayer(a[0], out response);

                    if (targetPlayer == null)
                    {
                        player.Reply(response);
                        return;
                    }

                    string groupName = a[1].ToLower();
                    ChatGroup group = ChatGroup.Find(groupName);

                    if (group == null)
                    {
                        player.ReplyLang("Group Does Not Exist", new KeyValuePair<string, string>("group", groupName));
                        return;
                    }

                    if (!permission.UserHasGroup(targetPlayer.Id, groupName))
                    {
                        player.ReplyLang("Player Not In Group", new Dictionary<string, string> { ["player"] = targetPlayer.Name, ["group"] = groupName });
                        return;
                    }

                    group.RemoveUser(targetPlayer);
                    player.ReplyLang("Removed From Group", new Dictionary<string, string> { ["player"] = targetPlayer.Name, ["group"] = groupName });
                },
                ["user"] = a => player.Reply($"Syntax: {cmd} user <add|remove>"),
                [string.Empty] = a =>
                {
                    player.Reply($"{cmd} group <add|remove|set|list>");
                    player.Reply($"{cmd} user <add|remove>");
                }
            };

            var command = commands.First(c => argsStr.ToLower().StartsWith(c.Key));

            string remainingArgs = argsStr.Remove(0, command.Key.Length);

            command.Value(remainingArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToArray());
        }

        #endregion

        #region Helper and Wrapper Methods

        #region Player Lookup

        private IPlayer FindPlayer(string nameOrID, out string response)
        {
            response = null;

            if (IsConvertableTo<string, ulong>(nameOrID) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                IPlayer result = players.All.ToList().Find((p) => p.Id == nameOrID);

                if (result == null)
                    response = $"Could not find player with ID '{nameOrID}'";

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    response = $"Could not find player with name '{nameOrID}'";
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
                    response = "Multiple matching players found: \n" + string.Join(", ", names);
                    break;
            }

            return null;
        }

        #endregion

        #region Type Conversion

        private bool IsConvertableTo<TSource, TResult>(TSource s)
        {
            TResult result;
            return TryConvert(s, out result);
        }

        private bool TryConvert<TSource, TResult>(TSource s, out TResult c)
        {
            try
            {
                c = (TResult)Convert.ChangeType(s, typeof(TResult));
                return true;
            }
            catch
            {
                c = default(TResult);
                return false;
            }
        }

        #endregion

        #region Data

        private void LoadData<T>(ref T data, string filename = null) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? Name);

        private void SaveData<T>(T data, string filename = null) => Core.Interface.Oxide.DataFileSystem.WriteObject(filename ?? Name, data);

        #endregion

        #region Formatting

        private static string StripRichText(string text)
        {
            foreach (var replacement in _stringReplacements)
                text = text.Replace(replacement, string.Empty);

            foreach (var replacement in _regexReplacements)
                text = replacement.Replace(text, string.Empty);

            return Formatter.ToPlaintext(text);
        }

        #endregion

        #region Message Wrapper

        public static string GetMessage(string key, string id) => _instance.lang.GetMessage(key, _instance, id);

        #endregion

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Group Already Exists"] = "Group '{group}' already exists.",
                ["Group Does Not Exist"] = "Group '{group}' doesn't exist.",
                ["Group Field Changed"] = "Changed {field} to {value} for group '{group}'.",
                ["Group Added"] = "Successfully added group '{group}'.",
                ["Group Removed"] = "Successfully removed group '{group}'.",
                ["Invalid Field"] = "{field} is not a valid field. Type 'chat group set' to list all existing fields.",
                ["Invalid Value"] = "'{value}' is not a correct value for field '{field}'! Should be a '{type}'.",
                ["Player Already In Group"] = "{player} already is in group '{group}'.",
                ["Added To Group"] = "{player} was added to group '{group}'.",
                ["Player Not In Group"] = "{player} is not in group '{group}'.",
                ["Removed From Group"] = "{player} was removed from group '{group}'."
            }, this);
        }

        #endregion

        #region Configuration

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        private class Configuration
        {
            [JsonProperty("Maximal Titles")]
            public int MaxTitles { get; set; } = 3;

            [JsonProperty("Maximal Characters Per Message")]
            public int MaxMessageLength { get; set; } = 128;

            [JsonProperty("Reverse Title Order")]
            public bool ReverseTitleOrder { get; set; } = false;
        }

        #endregion

        #region Group Structures

        public class BetterChatMessage
        {
            public IPlayer Player;
            public string Username;
            public string Message;
            public List<string> Titles;
            public string PrimaryGroup;
            public ChatGroup.UsernameSettings UsernameSettings;
            public ChatGroup.MessageSettings MessageSettings;
            public ChatGroup.FormatSettings FormatSettings;
            public List<string> BlockedReceivers = new List<string>();
            public CancelOptions CancelOption;

            public ChatGroup.FormatSettings GetOutput()
            {
                ChatGroup.FormatSettings output = new ChatGroup.FormatSettings();

                if (Message.Contains("[#") || Message.Contains("[+"))
                    Message = Message.Replace("[", string.Empty).Replace("]", string.Empty);

                if (Username.Contains("[#") || Username.Contains("[+"))
                    Username = Username.Replace("[", string.Empty).Replace("]", string.Empty);

                Dictionary<string, string> replacements = new Dictionary<string, string>
                {
                    ["Title"] = string.Join(" ", Titles.ToArray()),
                    ["Username"] = $"[#{UsernameSettings.GetUniversalColor()}][+{UsernameSettings.Size}]{Username}[/+][/#]",
                    ["Group"] = PrimaryGroup,
                    ["Message"] = $"[#{MessageSettings.GetUniversalColor()}][+{MessageSettings.Size}]{Message}[/+][/#]",
                    ["ID"] = Player.Id,
                    ["Time"] = DateTime.Now.TimeOfDay.ToString(),
                    ["Date"] = DateTime.Now.ToString()
                };

                output.Chat = FormatSettings.Chat;
                output.Console = FormatSettings.Console;

                foreach (var replacement in replacements)
                {
                    output.Console = StripRichText(output.Console.Replace($"{{{replacement.Key}}}", replacement.Value));
                    output.Chat = _instance.covalence.FormatText(output.Chat.Replace($"{{{replacement.Key}}}", replacement.Value));
                }

                if (output.Chat.StartsWith(" "))
                    output.Chat = output.Chat.Remove(0, 1);

                if (output.Console.StartsWith(" "))
                    output.Console = output.Console.Remove(0, 1);

                return output;
            }

            public static BetterChatMessage FromDictionary(Dictionary<string, object> dictionary)
            {
                var usernameSettings = dictionary[nameof(UsernameSettings)] as Dictionary<string, object>;
                var messageSettings = dictionary[nameof(MessageSettings)] as Dictionary<string, object>;
                var formatSettings = dictionary[nameof(FormatSettings)] as Dictionary<string, object>;

                return new BetterChatMessage
                {
                    Player = dictionary[nameof(Player)] as IPlayer,
                    Message = dictionary[nameof(Message)] as string,
                    Username = dictionary[nameof(Username)] as string,
                    Titles = dictionary[nameof(Titles)] as List<string>,
                    PrimaryGroup = dictionary[nameof(PrimaryGroup)] as string,
                    BlockedReceivers = dictionary[nameof(BlockedReceivers)] as List<string>,
                    UsernameSettings = new ChatGroup.UsernameSettings
                    {
                        Color = usernameSettings[nameof(ChatGroup.UsernameSettings.Color)] as string,
                        Size = (int)usernameSettings[nameof(ChatGroup.UsernameSettings.Size)]
                    },
                    MessageSettings = new ChatGroup.MessageSettings
                    {
                        Color = messageSettings[nameof(ChatGroup.MessageSettings.Color)] as string,
                        Size = (int)messageSettings[nameof(ChatGroup.MessageSettings.Size)]
                    },
                    FormatSettings = new ChatGroup.FormatSettings
                    {
                        Chat = formatSettings[nameof(ChatGroup.FormatSettings.Chat)] as string,
                        Console = formatSettings[nameof(ChatGroup.FormatSettings.Console)] as string
                    },
                    CancelOption = (CancelOptions) dictionary[nameof(CancelOption)]
                };
            }

            public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>
            {
                [nameof(Player)] = Player,
                [nameof(Message)] = Message,
                [nameof(Username)] = Username,
                [nameof(Titles)] = Titles,
                [nameof(PrimaryGroup)] = PrimaryGroup,
                [nameof(BlockedReceivers)] = BlockedReceivers,
                [nameof(UsernameSettings)] = new Dictionary<string, object>
                {
                    [nameof(ChatGroup.UsernameSettings.Color)] = UsernameSettings.Color,
                    [nameof(ChatGroup.UsernameSettings.Size)] = UsernameSettings.Size
                },
                [nameof(MessageSettings)] = new Dictionary<string, object>
                {
                    [nameof(ChatGroup.MessageSettings.Color)] = MessageSettings.Color,
                    [nameof(ChatGroup.MessageSettings.Size)] = MessageSettings.Size
                },
                [nameof(FormatSettings)] = new Dictionary<string, object>
                {
                    [nameof(ChatGroup.FormatSettings.Chat)] = FormatSettings.Chat,
                    [nameof(ChatGroup.FormatSettings.Console)] = FormatSettings.Console
                },
                [nameof(CancelOption)] = CancelOption
            };

            public enum CancelOptions
            {
                None = 0,
                BetterChatOnly = 1,
                BetterChatAndDefault = 2
            }
        }

        public class ChatGroup
        {
            private static readonly ChatGroup _fallbackGroup = new ChatGroup("default");

#if RUST
            private static readonly ChatGroup _rustDeveloperGroup = new ChatGroup("rust_developer")
            {
                Priority = 100,
                Title =
                {
                    Text  = "[Rust Developer]",
                    Color = "#ffaa55"
                }
            };
#endif

            public string GroupName;
            public int Priority = 0;

            public TitleSettings Title = new TitleSettings();
            public UsernameSettings Username = new UsernameSettings();
            public MessageSettings Message = new MessageSettings();
            public FormatSettings Format = new FormatSettings();

            public ChatGroup(string name)
            {
                GroupName = name;
                Title = new TitleSettings(name);
            }

            public static readonly Dictionary<string, Field> Fields = new Dictionary<string, Field>(StringComparer.InvariantCultureIgnoreCase)
            {
                ["Priority"] = new Field(g => g.Priority, (g, v) => g.Priority = int.Parse(v), "number"),

                ["Title"] = new Field(g => g.Title.Text, (g, v) => g.Title.Text = v, "text"),
                ["TitleColor"] = new Field(g => g.Title.Color, (g, v) => g.Title.Color = v, "color"),
                ["TitleSize"] = new Field(g => g.Title.Size, (g, v) => g.Title.Size = int.Parse(v), "number"),
                ["TitleHidden"] = new Field(g => g.Title.Hidden, (g, v) => g.Title.Hidden = bool.Parse(v), "true/false"),
                ["TitleHiddenIfNotPrimary"] = new Field(g => g.Title.HiddenIfNotPrimary, (g, v) => g.Title.HiddenIfNotPrimary = bool.Parse(v), "true/false"),

                ["UsernameColor"] = new Field(g => g.Username.Color, (g, v) => g.Username.Color = v, "color"),
                ["UsernameSize"] = new Field(g => g.Username.Size, (g, v) => g.Username.Size = int.Parse(v), "number"),

                ["MessageColor"] = new Field(g => g.Message.Color, (g, v) => g.Message.Color = v, "color"),
                ["MessageSize"] = new Field(g => g.Message.Size, (g, v) => g.Message.Size = int.Parse(v), "number"),

                ["ChatFormat"] = new Field(g => g.Format.Chat, (g, v) => g.Format.Chat = v, "text"),
                ["ConsoleFormat"] = new Field(g => g.Format.Console, (g, v) => g.Format.Console = v, "text")
            };

            public static ChatGroup Find(string name) => _instance._chatGroups.Find(g => g.GroupName == name);

            public static List<ChatGroup> GetUserGroups(IPlayer player)
            {
                string[] oxideGroups = _instance.permission.GetUserGroups(player.Id);
                var groups = _instance._chatGroups.Where(g => oxideGroups.Any(name => g.GroupName.Equals(name, StringComparison.OrdinalIgnoreCase))).ToList();

#if RUST
                BasePlayer bPlayer = BasePlayer.Find(player.Id);

                if (bPlayer.IsValid() && DeveloperList.Contains(bPlayer.userID))
                    groups.Add(_rustDeveloperGroup);
#endif

                return groups;
            }

            public static ChatGroup GetUserPrimaryGroup(IPlayer player)
            {
                List<ChatGroup> groups = GetUserGroups(player);
                ChatGroup primary = null;

                foreach (ChatGroup group in groups)
                    if (primary == null || group.Priority < primary.Priority)
                        primary = group;

                return primary;
            }

            public static BetterChatMessage PrepareMessage(IPlayer player, string message)
            {
                ChatGroup primary = GetUserPrimaryGroup(player);
                List<ChatGroup> groups = GetUserGroups(player);
                var userNamesettings = groups.Any(x => x.GroupName.Contains("vip") || x.GroupName == "mvp") ? new UsernameSettings { Size = 15, Color = "#ff9913" } : primary.Username;

                if (primary == null)
                {
                    _instance.PrintWarning($"{player.Name} ({player.Id}) does not seem to be in any BetterChat group - falling back to internal default group! This should never happen! Please make sure you have a group called 'default'.");
                    primary = _fallbackGroup;
                    groups.Add(primary);
                }

                groups.Sort((a, b) => a.Priority.CompareTo(b.Priority));

                var titles = (from g in groups
                              where !g.Title.Hidden && !(g.Title.HiddenIfNotPrimary && primary != g)
                              select $"[#{g.Title.GetUniversalColor()}][+{g.Title.Size}]{g.Title.Text}[/+][/#]")
                              .ToList();

                titles = titles.GetRange(0, Math.Min(_instance._config.MaxTitles, titles.Count));

                if (_instance._config.ReverseTitleOrder)
                {
                    titles.Reverse();
                }

                foreach (var thirdPartyTitle in _instance._thirdPartyTitles)
                {
                    try
                    {
                        string title = thirdPartyTitle.Value(player);

                        if (!string.IsNullOrEmpty(title))
                            titles.Add(title);
                    }
                    catch (Exception ex)
                    {
                        _instance.PrintError($"Error when trying to get third-party title from plugin '{thirdPartyTitle.Key}'{Environment.NewLine}{ex}");
                    }
                }

                return new BetterChatMessage
                {
                    Player = player,
                    Username = StripRichText(player.Name),
                    Message = StripRichText(message),
                    Titles = titles,
                    PrimaryGroup = primary.GroupName,
                    UsernameSettings = userNamesettings,
                    MessageSettings = primary.Message,
                    FormatSettings = primary.Format
                };
            }

            public void AddUser(IPlayer player) => _instance.permission.AddUserGroup(player.Id, GroupName);

            public void RemoveUser(IPlayer player) => _instance.permission.RemoveUserGroup(player.Id, GroupName);

            public Field.SetValueResult SetField(string field, string value)
            {
                if (!Fields.ContainsKey(field))
                    return Field.SetValueResult.InvalidField;

                try
                {
                    Fields[field].Setter(this, value);
                }
                catch (FormatException)
                {
                    return Field.SetValueResult.InvalidValue;
                }

                return Field.SetValueResult.Success;
            }

            public Dictionary<string, object> GetFields() => Fields.ToDictionary(field => field.Key, field => field.Value.Getter(this));

            public override int GetHashCode() => GroupName.GetHashCode();

            public class TitleSettings
            {
                public string Text = "[Player]";
                public string Color = "#55aaff";
                public int Size = 15;
                public bool Hidden = false;
                public bool HiddenIfNotPrimary = false;

                public string GetUniversalColor() => Color.StartsWith("#") ? Color.Substring(1) : Color;

                public TitleSettings(string groupName)
                {
                    if (groupName != "default" && groupName != null)
                        Text = $"[{groupName}]";
                }

                public TitleSettings()
                {
                }
            }

            public class UsernameSettings
            {
                public string Color = "#55aaff";
                public int Size = 15;

                public string GetUniversalColor() => Color.StartsWith("#") ? Color.Substring(1) : Color;
            }

            public class MessageSettings
            {
                public string Color = "white";
                public int Size = 15;

                public string GetUniversalColor() => Color.StartsWith("#") ? Color.Substring(1) : Color;
            }

            public class FormatSettings
            {
                public string Chat = "{Title} {Username}: {Message}";
                public string Console = "{Title} {Username}: {Message}";
            }

            public class Field
            {
                public Func<ChatGroup, object> Getter { get; }
                public Action<ChatGroup, string> Setter { get; }
                public string UserFriendyType { get; }

                public enum SetValueResult
                {
                    Success,
                    InvalidField,
                    InvalidValue
                }

                public Field(Func<ChatGroup, object> getter, Action<ChatGroup, string> setter, string userFriendyType)
                {
                    Getter = getter;
                    Setter = setter;
                    UserFriendyType = userFriendyType;
                }
            }
        }

        #endregion
    }
}

#region Extension Methods

namespace Oxide.Plugins.BetterChatExtensions
{
    internal static class IPlayerExtensions
    {
        public static void ReplyLang(this IPlayer player, string key, Dictionary<string, string> replacements = null)
        {
            string message = BetterChat.GetMessage(key, player.Id);

            if (replacements != null)
                foreach (var replacement in replacements)
                    message = message.Replace($"{{{replacement.Key}}}", replacement.Value);

            replacements = null;

            player.Reply(message);
        }

        public static void ReplyLang(this IPlayer player, string key, KeyValuePair<string, string> replacement)
        {
            string message = BetterChat.GetMessage(key, player.Id);
            message = message.Replace($"{{{replacement.Key}}}", replacement.Value);

            player.Reply(message);
        }
    }
}

#endregion