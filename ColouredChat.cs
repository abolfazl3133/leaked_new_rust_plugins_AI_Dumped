using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using ConVar;
using UnityEngine;
using CompanionServer;
using Pool = Facepunch.Pool;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Coloured Chat", "collect_vood", "2.2.87")]
    [Description("Allows players to change their name & message colour in chat")]
    class ColouredChat : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat, BetterChatMute, ZoneManager;

        #region Fields
        
        private readonly StringBuilder _sharedStringBuilder = new StringBuilder();

        private const string ColourRegex = "^#(?:[0-9a-fA-f]{3}){1,2}$";
        private const string ChatFormat = "{0}: {1}";

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this command." },
                { "NoPermissionSetOthers", "You don't have permission to set other players {0} colours." },
                { "NoPermissionGradient", "You don't have permission to use {0} gradients." },
                { "NoPermissionRandom", "You don't have permission to use random {0} colours." },
                { "NoPermissionRainbow", "You don't have permission to use the rainbow colours." },
                { "IncorrectGradientUsage", "Incorrect usage! To use gradients please use /{0} gradient hexCode1 hexCode2 ...</color>" },
                { "IncorrectGradientUsageArgs", "Incorrect usage! A gradient requires at least two different valid colours!"},
                { "GradientChanged", "{0} gradient changed to {1}!"},
                { "GradientChangedFor", "{0}'s gradient {1} colour changed to {2}!"},
                { "IncorrectUsage", "Incorrect usage! /{0} <colour>\nFor detailed help do /{1}" },
                { "IncorrectSetUsage", "Incorrect set usage! /{0} set <playerIdOrName> <colourOrColourArgument>\nFor a list of colours do /colours" },
                { "PlayerNotFound", "Player {0} was not found." },
                { "InvalidCharacters", "The character '{0}' is not allowed in colours. Please remove it." },
                { "ColourRemoved", "{0} colour removed!" },
                { "ColourRemovedFor", "{0}'s {1} colour was removed!" },
                { "ColourChanged", "{0} colour changed to <color={1}>{1}</color>!" },
                { "ColourChangedFor", "{0}'s {1} colour changed to <color={2}>{2}</color>!" },
                { "ColoursInfo", "You can only use hexcodes, eg '<color=#ffff94>#ffff94</color>'\nTo remove your colour, use 'clear', 'reset' or 'remove'\n\nAvailable Commands: {0}\n\n{1}"},
                { "InvalidColour", "That colour is not valid. Do /colours for more information on valid colours." },
                { "RndColour", "{0} colour was randomized to <color={1}>{1}</color>" },
                { "RndColourFor", "{0} colour of {1} randomized to <color={2}>{2}</color>."},
                { "RainbowColour", "{0} colour was set to rainbow." },
                { "RainbowColourFor", "{0} colour of {1} set to rainbow."},
                { "IncorrectGroupUsage", "Incorrect group usage! /{0} group <groupName> <colourOrColourArgument>\nFor a list of colours do /colours" },
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "У вас нет прав для использования этой команды." },
                { "NoPermissionSetOthers", "У вас нет прав для изменения цвета другим игрокам({0})." },
                { "NoPermissionGradient", "У вас нет разрешения на использование градиентов {0}.." },
                { "NoPermissionRandom", "У вас нет разрешения на использование случайных цветов {0}.." },
                { "NoPermissionRainbow", "У вас нет разрешения на использование цветов радуги.." },
                { "IncorrectGradientUsage", "Неправильное использование! Чтобы использовать градиенты, используйте градиент /{0} hexCode1 hexCode2. ...</color>" },
                { "IncorrectGradientUsageArgs", "Неправильное использование! Для градиента требуется как минимум два разных допустимых цвета.!"},
                { "GradientChanged", "{0} градиент изменен на {1}!"},
                { "GradientChangedFor", "Цвет градиента {1} объекта {0} изменен на {2}."},
                { "IncorrectUsage", "Неправильное использование! /{0} <color>\\nДля получения подробной информации выполните /{1}" },
                { "IncorrectSetUsage", "Неправильное использование набора! /{0} set <playerIdOrName> <colourOrColourArgument>\\nДля списка цветов выполните /colours" },
                { "PlayerNotFound", "Игрок {0} не найден." },
                { "InvalidCharacters", "Символ «{0}» не допускается в цветах. Пожалуйста, удалите его." },
                { "ColourRemoved", "Цвет {0} удален!" },
                { "ColourRemovedFor", "Цвет {1} у пользователя {0} был удален!" },
                { "ColourChanged", "Цвет {0} изменен на <color={1}>{1}</color>!" },
                { "ColourChangedFor", "Цвет {1} объекта {0} изменен на <color={2}>{2}</color>!" },
                { "ColoursInfo", "Вы можете использовать только шестнадцатеричные коды, например, '<color=#ffff94>#ffff94</color>'\\nЧтобы удалить цвет, используйте «очистить», «сбросить» или «удалить».\\n\\nДоступные команды: {0}\\n \\n{1}"},
                { "InvalidColour", "Этот цвет недействителен. Чтобы получить дополнительную информацию о допустимых цветах, выполните команду /colors." },
                { "RndColour", "Цвет {0} был рандомизирован на <color={1}>{1}</color>" },
                { "RndColourFor", "Цвет {0} из {1} рандомизирован на <color={2}>{2}</color>."},
                { "RainbowColour", "Цвет {0} был установлен на радугу." },
                { "RainbowColourFor", "{0} цвет {1} установлен на радугу."},
                { "IncorrectGroupUsage", "Неправильное использование группы! /{0} group <groupName> <colourOrColourArgument>\\nДля списка цветов выполните /colours" },
            }, this);
        }

        #endregion

        #region Config     

        private Configuration _configuration;
        private class Configuration
        {
            //General
            [JsonProperty(PropertyName = "Player Inactivity Data Removal (days)")]
            public int InactivityRemovalTime = 7;
            [JsonProperty(PropertyName = "Block messages of muted players (requires BetterChatMute)")]
            public bool BlockChatMute = true;
            [JsonProperty(PropertyName = "Rainbow Colours")]
            public string[] RainbowColours = { "#ff0000", "#ffa500", "#ffff94", "#008000", "#0000ff", "#4b0082", "#ee82ee" };
            [JsonProperty(PropertyName = "Blocked Characters")]
            public string[] BlockedValues = { "{", "}", "size" };

            //Name
            [JsonProperty(PropertyName = "Name colour commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] NameColourCommands = new string[] { "colour", "color" };
            [JsonProperty(PropertyName = "Name colour commands (Help)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] NameColoursCommands = new string[] { "colours", "colors" };
            [JsonProperty(PropertyName = "Name show colour permission")]
            public string NamePermShow = "colouredchat.name.show";
            [JsonProperty(PropertyName = "Name use permission")]
            public string NamePermUse = "colouredchat.name.use";
            [JsonProperty(PropertyName = "Name use gradient permission")]
            public string NamePermGradient = "colouredchat.name.gradient";
            [JsonProperty(PropertyName = "Name default rainbow name permission")]
            public string NamePermRainbow = "colouredchat.name.rainbow";
            [JsonProperty(PropertyName = "Name bypass restrictions permission")]
            public string NamePermBypass = "colouredchat.name.bypass";
            [JsonProperty(PropertyName = "Name set others colour permission")]
            public string NamePermSetOthers = "colouredchat.name.setothers";
            [JsonProperty(PropertyName = "Name get random colour permission")]
            public string NamePermRandomColour = "colouredchat.name.random";
            [JsonProperty(PropertyName = "Name use blacklist")]
            public bool NameUseBlacklist = true;
            [JsonProperty(PropertyName = "Name blocked colour hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> NameBlockColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Name blocked colours range hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ColourRange> NameBlacklistedRangeColoursHex = new List<ColourRange>
            {
                { new ColourRange("#000000", "#000000") }
            };
            [JsonProperty(PropertyName = "Name use whitelist")]
            public bool NameUseWhitelist = false;
            [JsonProperty(PropertyName = "Name whitelisted colours hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> NameWhitelistedColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Name whitelisted colour range hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ColourRange> NameWhitelistedRangeColoursHex = new List<ColourRange>
            {
                { new ColourRange("#000000", "#FFFFFF") }
            };

            //Message
            [JsonProperty(PropertyName = "Message colour commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] MessageColourCommands = new string[] { "mcolour", "mcolor" };
            [JsonProperty(PropertyName = "Message colour commands (Help)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] MessageColoursCommands = new string[] { "mcolours", "mcolors" };
            [JsonProperty(PropertyName = "Message show colour permission")]
            public string MessagePermShow = "colouredchat.message.show";
            [JsonProperty(PropertyName = "Message use permission")]
            public string MessagePermUse = "colouredchat.message.use";
            [JsonProperty(PropertyName = "Message use gradient permission")]
            public string MessagePermGradient = "colouredchat.message.gradient";
            [JsonProperty(PropertyName = "Message default rainbow name permission")]
            public string MessagePermRainbow = "colouredchat.message.rainbow";
            [JsonProperty(PropertyName = "Message bypass restrictions permission")]
            public string MessagePermBypass = "colouredchat.message.bypass";
            [JsonProperty(PropertyName = "Message set others colour permission")]
            public string MessagePermSetOthers = "colouredchat.message.setothers";
            [JsonProperty(PropertyName = "Message get random colour permission")]
            public string MessagePermRandomColour = "colouredchat.message.random";
            [JsonProperty(PropertyName = "Message use blacklist")]
            public bool MessageUseBlacklist = true;
            [JsonProperty(PropertyName = "Message blocked colours hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> MessageBlockColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Message blocked colour range hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ColourRange> MessageBlacklistedRangeColoursHex = new List<ColourRange>
            {
                { new ColourRange("#000000", "#000000") }
            };
            [JsonProperty(PropertyName = "Message use whitelist")]
            public bool MessageUseWhitelist = false;
            [JsonProperty(PropertyName = "Message whitelisted colours hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> MessageWhitelistedColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Message whitelisted colour range hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ColourRange> MessageWhitelistedRangeColoursHex = new List<ColourRange>
            {
                { new ColourRange("#000000", "#FFFFFF") }
            };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configuration = new Configuration();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configuration = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_configuration);

        #region Colour Range Class

        public class ColourRange
        {
            [JsonProperty(PropertyName = "From")]
            public string _from;
            [JsonProperty(PropertyName = "To")]
            public string _to;

            public ColourRange(string from, string to)
            {
                _from = from;
                _to = to;
            }
        }

        #endregion

        #endregion

        #region Data

        private Dictionary<string, CachePlayerData> cachedData = new Dictionary<string, CachePlayerData>();
        private StoredData storedData;
        private Dictionary<string, PlayerData> allColourData => storedData.AllColourData;

        private class StoredData
        {
            public Dictionary<string, PlayerData> AllColourData { get; private set; } = new Dictionary<string, PlayerData>();
        }

        private class PlayerData
        {
            [JsonProperty("Name Colour")]
            public string NameColour = string.Empty;
            [JsonProperty("Name Gradient Args")]
            public string[] NameGradientArgs = null;

            [JsonProperty("Message Colour")]
            public string MessageColour = string.Empty;
            [JsonProperty("Message Gradient Args")]
            public string[] MessageGradientArgs = null;

            [JsonProperty("Last active")]
            public long LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            public PlayerData()
            {

            }

            public PlayerData(string nameColour = "", string[] nameGradientArgs = null, string messageColour = "", string[] messageGradientArgs = null)
            {
                NameColour = nameColour;
                NameGradientArgs = nameGradientArgs;

                MessageColour = messageColour;
                MessageGradientArgs = messageGradientArgs;
            }

            public PlayerData(bool isGroup)
            {
                LastActive = 0;
            }
        }

        private class CachePlayerData
        {
            public string NameColourGradient;
            public string PrimaryGroup;

            public CachePlayerData(string nameColourGradient = "", string primaryGroup = "")
            {
                NameColourGradient = nameColourGradient;
                PrimaryGroup = primaryGroup;
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void OnServerSave()
        {
            ClearUpData();
            SaveData();
        } 
        private void Unload() => SaveData();

        private void ChangeNameColour(string key, string colour, string[] colourArgs)
        {
            var playerData = new PlayerData(colour, colourArgs);
            if (!allColourData.ContainsKey(key)) allColourData.Add(key, playerData);

            allColourData[key].NameColour = colour;
            allColourData[key].NameGradientArgs = colourArgs;
        }

        private void ChangeMessageColour(string key, string colour, string[] colourArgs)
        {
            var playerData = new PlayerData(string.Empty, null, colour, colourArgs);           
            if (!allColourData.ContainsKey(key)) allColourData.Add(key, playerData);

            allColourData[key].MessageColour = colour;
            allColourData[key].MessageGradientArgs = colourArgs;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            if (_configuration.MessageUseBlacklist && _configuration.MessageUseWhitelist || _configuration.NameUseBlacklist && _configuration.NameUseWhitelist) PrintWarning("You are using both black/- and whitelist! This might cause issues.");

            permission.RegisterPermission(_configuration.NamePermShow, this);
            permission.RegisterPermission(_configuration.MessagePermShow, this);
            permission.RegisterPermission(_configuration.NamePermRainbow, this);
            permission.RegisterPermission(_configuration.MessagePermRainbow, this);
            permission.RegisterPermission(_configuration.NamePermGradient, this);
            permission.RegisterPermission(_configuration.MessagePermGradient, this);
            permission.RegisterPermission(_configuration.NamePermUse, this);
            permission.RegisterPermission(_configuration.MessagePermUse, this);
            permission.RegisterPermission(_configuration.NamePermBypass, this);
            permission.RegisterPermission(_configuration.MessagePermBypass, this);
            permission.RegisterPermission(_configuration.NamePermSetOthers, this);
            permission.RegisterPermission(_configuration.MessagePermSetOthers, this);
            permission.RegisterPermission(_configuration.NamePermRandomColour, this);
            permission.RegisterPermission(_configuration.MessagePermRandomColour, this);

            AddCovalenceCommand(_configuration.NameColourCommands, nameof(cmdNameColour));
            AddCovalenceCommand(_configuration.NameColoursCommands, nameof(cmdNameColours));
            AddCovalenceCommand(_configuration.MessageColourCommands, nameof(cmdMessageColour));
            AddCovalenceCommand(_configuration.MessageColoursCommands, nameof(cmdMessageColours));

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            ClearUpData();
        }

        private void OnUserConnected(IPlayer player)
        {
            if (!allColourData.ContainsKey(player.Id)) return;
            allColourData[player.Id].LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private void OnUserDisconnected(IPlayer player)
        {
            if (!allColourData.ContainsKey(player.Id)) return;
            allColourData[player.Id].LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private void OnUserNameUpdated(string id, string oldName, string newName) => ClearCache(id);

        private void OnUserGroupAdded(string id, string groupName) => ClearCache(id);

        private void OnUserGroupRemoved(string id, string groupName) => ClearCache(id);

        private void OnGroupDeleted(string name) => ClearCache();

        private void OnGroupPermissionGranted(string name, string perm) => ClearCache();

        private void OnGroupPermissionRevoked(string name, string perm) => ClearCache();

        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (BetterChatIns()) 
                return null;
            if (_configuration.BlockChatMute && BetterChatMuteIns()) 
                if (BetterChatMute.Call<bool>("API_IsMuted", player.IPlayer)) 
                    return null;
            if (player == null) 
                return null;
            if (ZoneManagerIns() && ZoneManager.Call<bool>("PlayerHasFlag", player, "nochat")) 
                return false;

            if (Chat.serverlog)
            {
                var objArray = new object[] { ConsoleColor.DarkYellow, null, null, null };
                objArray[1] = string.Concat(new object[] { "[", channel, "] ", player.displayName.EscapeRichText(), ": " });
                objArray[2] = ConsoleColor.DarkGreen;
                objArray[3] = message;
                ServerConsole.PrintColoured(objArray);
            }

            var colouredChatMessage = FromMessage(player.IPlayer, channel, message);
            var colouredChatMessageDict = colouredChatMessage.GetDictionary();

            #region API
            
            foreach (var plugin in plugins.GetAll())
            {
                var obj = plugin.CallHook("OnColouredChat", colouredChatMessageDict);

                if (obj is Dictionary<string, object>)
                {
                    try
                    {
                        colouredChatMessageDict = obj as Dictionary<string, object>;
                    }
                    catch (Exception e)
                    {
                        PrintError($"Failed to load modified OnColouredChat hook data from plugin '{plugin.Title} ({plugin.Version})':{Environment.NewLine}{e}");
                        continue;
                    }
                }
                else if (obj != null) return obj;
            }

            colouredChatMessage = ColouredChatMessage.FromDictionary(colouredChatMessageDict);

            #endregion

            SendMessage(colouredChatMessage);
            return true;
        }

        private Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            if (dict != null)
            {
                var player = dict["Player"] as IPlayer;

                var colouredChatMessage = FromMessage(player, (Chat.ChatChannel)dict["ChatChannel"], 
                    dict["Message"].ToString());

                #region API

                var colouredChatMessageDict = colouredChatMessage.GetDictionary();

                foreach (Plugin plugin in plugins.GetAll())
                {
                    object obj = plugin.CallHook("OnColouredChat", colouredChatMessageDict);

                    if (obj is Dictionary<string, object>)
                    {
                        try
                        {
                            colouredChatMessageDict = obj as Dictionary<string, object>;
                        }
                        catch (Exception e)
                        {
                            PrintError($"Failed to load modified OnColouredChat hook data from plugin '{plugin.Title} ({plugin.Version})':{Environment.NewLine}{e}");
                            continue;
                        }
                    }
                    else if (obj != null)
                    {
                        if (obj is bool)
                        {
                            dict["CancelOption"] = 2;
                        }
                    }
                }

                colouredChatMessage = ColouredChatMessage.FromDictionary(colouredChatMessageDict);

                #endregion

                if (!string.IsNullOrEmpty(colouredChatMessage.Name)) dict["Username"] = colouredChatMessage.Name;
                if (!string.IsNullOrEmpty(colouredChatMessage.Colour)) { ((Dictionary<string, object>)dict["UsernameSettings"])["Color"] = colouredChatMessage.Colour; }
                dict["Message"] = colouredChatMessage.Message;
            }
            return dict;
        }

        #endregion

        #region Commands

        void cmdNameColour(IPlayer player, string cmd, string[] args) => ProcessColourCommand(player, cmd, args);

        void cmdNameColours(IPlayer player, string cmd, string[] args) => ProcessColoursCommand(player, cmd, args);

        void cmdMessageColour(IPlayer player, string cmd, string[] args) => ProcessColourCommand(player, cmd, args, true);

        void cmdMessageColours(IPlayer player, string cmd, string[] args) => ProcessColoursCommand(player, cmd, args, true);
        
        #endregion

        #region Helpers

        private bool BetterChatIns() => (BetterChat != null && BetterChat.IsLoaded);
        private bool BetterChatMuteIns() => (BetterChatMute != null && BetterChatMute.IsLoaded);
        private bool ZoneManagerIns() => (ZoneManager != null && ZoneManager.IsLoaded);
        private bool IsValidColour(string input) => Regex.Match(input, ColourRegex).Success;
        private string GetMessage(string key, IPlayer player, params string[] args) => $"<color=#cdc2b2>{string.Format(lang.GetMessage(key, this, player.Id), args)}</color>";

        //Name
        private bool HasNameShowPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.NamePermShow));
        private bool HasNamePerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.NamePermUse));
        private bool HasNameRainbow(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.NamePermRainbow));
        private bool CanNameGradient(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.NamePermGradient));
        private bool CanNameBypass(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.NamePermBypass));
        private bool CanNameSetOthers(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.NamePermSetOthers));
        private bool CanNameRandomColour(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.NamePermRandomColour));

        //Message
        private bool HasMessageShowPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.MessagePermShow));
        private bool HasMessagePerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.MessagePermUse));
        private bool HasMessageRainbow(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.MessagePermRainbow));
        private bool CanMessageGradient(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.MessagePermGradient));
        private bool CanMessageBypass(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.MessagePermBypass));
        private bool CanMessageSetOthers(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.MessagePermSetOthers));
        private bool CanMessageRandomColour(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, _configuration.MessagePermRandomColour));

        private bool IsValidName(string input, IPlayer iPlayer = null)
        {
            if (iPlayer != null && CanNameBypass(iPlayer)) return true;
            if (_configuration.NameUseBlacklist)
            {
                bool inRange = false;
                foreach (var colourRange in _configuration.NameBlacklistedRangeColoursHex)
                {
                    inRange = IsInHexRange(input, colourRange._from, colourRange._to);
                    if (inRange) break;
                }
                return !_configuration.NameBlockColoursHex.Any(x => (input == x)) && !inRange;
            }
            else if (_configuration.NameUseWhitelist)
            {
                bool inRange = false;
                foreach (var colourRange in _configuration.NameWhitelistedRangeColoursHex)
                {
                    inRange = IsInHexRange(input, colourRange._from, colourRange._to);
                    if (!inRange) break;
                }
                return _configuration.NameWhitelistedColoursHex.Any(x => (input == x)) || inRange;
            }
            return true;
        }

        private bool IsValidMessage(string input, IPlayer iPlayer = null)
        {
            if (iPlayer != null && CanMessageBypass(iPlayer)) return true;
            if (_configuration.MessageUseBlacklist)
            {
                bool inRange = false;
                foreach (var colourRange in _configuration.MessageBlacklistedRangeColoursHex)
                {
                    inRange = IsInHexRange(input, colourRange._from, colourRange._to);
                    if (inRange) 
                        break;
                }
                return !_configuration.MessageBlockColoursHex.Any(x => (input == x)) && !inRange;
            }
            else if (_configuration.MessageUseWhitelist)
            {
                bool inRange = false;
                foreach (var colourRange in _configuration.MessageWhitelistedRangeColoursHex)
                {
                    inRange = IsInHexRange(input, colourRange._from, colourRange._to);
                    if (!inRange)
                        break;
                }
                return _configuration.MessageWhitelistedColoursHex.Any(x => (input == x)) || inRange;
            }
            
            return true;
        }
        
        private void SendMessage(ColouredChatMessage colouredChatMessage)
        {
            var player = colouredChatMessage.Player.Object as BasePlayer;
            if (player == null)
                return; // cannot send if player does not exist

            var formattedLogMessage = _sharedStringBuilder;
            formattedLogMessage.Clear();
            
            switch (colouredChatMessage.ChatChannel)
            {
                case Chat.ChatChannel.Global:
                case Chat.ChatChannel.Server:
                {
                    ConsoleNetwork.BroadcastToAllClients("chat.add2", (int) colouredChatMessage.ChatChannel,
                        player.userID, colouredChatMessage.Message, colouredChatMessage.Name,
                        colouredChatMessage.Colour);
                    
                    formattedLogMessage.Append("[CHAT] ");
                    break;
                }
                case Chat.ChatChannel.Team:
                {
                    if (player.Team == null)
                        return;
                    
                    // Broadcast to rust+ app users
                    player.Team.BroadcastTeamChat(player.userID, player.displayName, colouredChatMessage.Message, colouredChatMessage.Colour);

                    ConsoleNetwork.SendClientCommand(player.Team.GetOnlineMemberConnections(), "chat.add2",
                        (int) colouredChatMessage.ChatChannel, player.userID, colouredChatMessage.Message,
                        colouredChatMessage.Name, colouredChatMessage.Colour);

                    formattedLogMessage.Append("[TEAM CHAT] ");
                    break;
                }
                case Chat.ChatChannel.Cards:
                {
                    if (!player.isMounted)
                        return;
                    
                    var cardTable = player.GetMountedVehicle() as CardTable;
                    if (cardTable == null || !cardTable.GameController.PlayerIsInGame(player))
                        return;
                    
                    var cardTableConnections = Pool.GetList<Network.Connection>();
                    cardTable.GameController.GetConnectionsInGame(cardTableConnections);
                    
                    if (cardTableConnections.Count > 0)
                    {
                        ConsoleNetwork.SendClientCommand(cardTableConnections, "chat.add2",
                            (int) colouredChatMessage.ChatChannel, player.userID, colouredChatMessage.Message,
                            colouredChatMessage.Name, colouredChatMessage.Colour);
                    }
                    Pool.FreeList(ref cardTableConnections);
                    
                    formattedLogMessage.Append("[CARDS CHAT] ");
                    break;
                }
                case Chat.ChatChannel.Local:
                {
                    var num = Chat.localChatRange * Chat.localChatRange;
                    var senderPosition = player.transform.position;
                    
                    var closeByConnections = Pool.GetList<Network.Connection>();
                    foreach (var basePlayer in BasePlayer.activePlayerList)
                    {
                        var sqrMagnitude = (basePlayer.transform.position - senderPosition).sqrMagnitude;
                        if (sqrMagnitude > num)
                            continue;
                        
                        closeByConnections.Add(basePlayer.Connection);
                    }

                    ConsoleNetwork.SendClientCommand(closeByConnections, "chat.add2",
                        (int) colouredChatMessage.ChatChannel, player.userID, colouredChatMessage.Message,
                        colouredChatMessage.Name, colouredChatMessage.Colour);
                    
                    Pool.FreeList(ref closeByConnections);
                    formattedLogMessage.Append("[CHAT] ");
                    break;
                }
            }
            
            // Console logging
            formattedLogMessage.Append(player.displayName.EscapeRichText()).Append('[').Append(player.UserIDString)
                .Append("] : ").Append(colouredChatMessage.Message);
            DebugEx.Log(formattedLogMessage);
            
            // Rcon logging
            var chatEntry = new Chat.ChatEntry
            {
                Channel = colouredChatMessage.ChatChannel,
                Message = colouredChatMessage.Message,
                UserId = player.UserIDString,
                Username = player.displayName,
                Color = colouredChatMessage.Colour,
                Time = Facepunch.Math.Epoch.Current
            };
            Facepunch.RCon.Broadcast(Facepunch.RCon.LogType.Chat, chatEntry);
        }

        private void ProcessColourCommand(IPlayer player, string cmd, string[] args, bool isMessage = false)
        {
            if (args.Length < 1)
            {
                Player.Message(player.Object as BasePlayer, GetMessage("IncorrectUsage", player,
                    isMessage ? _configuration.MessageColourCommands[0] : _configuration.NameColourCommands[0],
                    isMessage ? _configuration.MessageColoursCommands[0] : _configuration.NameColoursCommands[0]),76561198297741077);
                return;
            }
            var colLower = string.Empty;
            if (args[0] == "set")
            {
                if ((!isMessage && !CanNameSetOthers(player)) || (isMessage && !CanMessageSetOthers(player)))
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("NoPermissionSetOthers", player, isMessage ? "message" : "name"),76561198297741077);
                    return;
                }
                if (args.Length < 3)
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("IncorrectSetUsage", player,
                        isMessage ? _configuration.MessageColourCommands[0] : _configuration.NameColourCommands[0]),76561198297741077);
                    return;
                }
                var target = covalence.Players.FindPlayer(args[1]);
                if (target == null)
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("PlayerNotFound", player, args[1]),76561198297741077);
                    return;
                }
                colLower = args[2].ToLower();
                ProcessColour(player, target, colLower, args.Skip(2).ToArray(), isMessage);
            }
            else if (args[0] == "group")
            {
                if (!player.IsAdmin)
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("NoPermission", player),76561198297741077);
                    return;
                }
                if (args.Length < 3)
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("IncorrectGroupUsage", player,
                        isMessage ? _configuration.MessageColourCommands[0] : _configuration.NameColourCommands[0]),76561198297741077);
                    return;
                }
                if (!permission.GroupExists(args[1])) permission.CreateGroup(args[1], string.Empty, 0);
                colLower = args[2].ToLower();
                ProcessColour(player, player, colLower, args.Skip(3).ToArray(), isMessage, args[1]);
            }
            else
            {
                if ((!isMessage && !HasNamePerm(player)) || (isMessage && !HasMessagePerm(player)))
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("NoPermission", player),76561198297741077);
                    return;
                }
                if (args.Length < 1)
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("IncorrectUsage", player,
                        isMessage ? _configuration.MessageColourCommands[0] : _configuration.NameColourCommands[0]),76561198297741077);
                    return;
                }
                colLower = args[0].ToLower();
                ProcessColour(player, player, colLower, args.Skip(1).ToArray(), isMessage);
            }
        }

        private void ProcessColoursCommand(IPlayer player, string cmd, string[] args, bool isMessage = false)
        {
            if ((!isMessage && !HasNamePerm(player)) || (isMessage && !HasMessagePerm(player)))
            {
                Player.Message(player.Object as BasePlayer, GetMessage("NoPermission", player),76561198297741077);
                return;
            }
            var availableCommandsBuilder = _sharedStringBuilder;
            availableCommandsBuilder.Clear();
            
            if (isMessage)
            {
                var commandName = _configuration.MessageColourCommands[0];
                availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" <color=#ff6666>#ff6666</color>");
                if (CanMessageRandomColour(player))
                {
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" random");
                }
                if (CanMessageGradient(player)) 
                {
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" gradient <color=#ff6666>#ff6666</color> <color=#ff6666>#ff6666</color>").AppendLine();
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color> <color=#90ee90>#90ee90</color>");
                }
                if (CanMessageSetOthers(player))
                {
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" set <color=#a8a8a8>playerIdOrName</color> <color=#ff6666>#ff6666</color>");
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" set <color=#a8a8a8>playerIdOrName</color> gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color>");
                }
                if (player.IsAdmin)
                {
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" group <color=#a8a8a8>groupName</color> <color=#ff6666>#ff6666</color>");
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" group <color=#a8a8a8>groupName</color> gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color>");
                }
            }
            else
            {
                var commandName = _configuration.NameColourCommands[0];
                availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" <color=#ff6666>#ff6666</color>");
                if (CanNameRandomColour(player))
                {
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" random");
                }
                if (CanNameGradient(player)) 
                {
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" gradient <color=#ff6666>#ff6666</color> <color=#ff6666>#ff6666</color>").AppendLine();
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color> <color=#90ee90>#90ee90</color>");
                }
                if (CanNameSetOthers(player)) 
                {
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" set <color=#a8a8a8>playerIdOrName</color> <color=#ff6666>#ff6666</color>");
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" set <color=#a8a8a8>playerIdOrName</color> gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color>");
                }
                if (player.IsAdmin)
                {
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" group <color=#a8a8a8>groupName</color> <color=#ff6666>#ff6666</color>");
                    availableCommandsBuilder.AppendLine().Append('/').Append(commandName).Append(" group <color=#a8a8a8>groupName</color> gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color>");
                }
            }

            var availableCommands = availableCommandsBuilder.ToString();
            var additionalInfoBuilder = _sharedStringBuilder;
            additionalInfoBuilder.Clear();
            
            if (isMessage && _configuration.MessageUseWhitelist)
            {
                additionalInfoBuilder.AppendLine("Whitelisted Colours:");
                foreach (var colour in _configuration.MessageWhitelistedColoursHex)
                {
                    additionalInfoBuilder.Append("- <color=").Append(colour).Append(">").Append(colour).AppendLine("</color>");
                }
                foreach (var colourRange in _configuration.MessageWhitelistedRangeColoursHex)
                {
                    additionalInfoBuilder.Append("- From <color=").Append(colourRange._from).Append(">")
                        .Append(colourRange._from).Append("</color> to <color=").Append(colourRange._to).Append(">")
                        .Append(colourRange._to).AppendLine("</color>");
                }
            }
            else if (isMessage && _configuration.MessageUseBlacklist)
            {
                additionalInfoBuilder.AppendLine("Blacklisted Colours:");
                foreach (var colour in _configuration.MessageBlockColoursHex)
                {
                    additionalInfoBuilder.Append("- <color=").Append(colour).Append(">").Append(colour).Append("</color>");
                }
                foreach (var colourRange in _configuration.MessageBlacklistedRangeColoursHex)
                {
                    additionalInfoBuilder.Append("- From <color=").Append(colourRange._from).Append(">")
                        .Append(colourRange._from).Append("</color> to <color=").Append(colourRange._to).Append(">")
                        .Append(colourRange._to).AppendLine("</color>");
                }
            }
            else if (!isMessage && _configuration.NameUseWhitelist)
            {
                additionalInfoBuilder.AppendLine("Whitelisted Colours:");
                foreach (var colour in _configuration.NameWhitelistedColoursHex)
                {
                    additionalInfoBuilder.Append("- <color=").Append(colour).Append(">").Append(colour).AppendLine("</color>");
                }
                foreach (var colourRange in _configuration.NameWhitelistedRangeColoursHex)
                {
                    additionalInfoBuilder.Append("- From <color=").Append(colourRange._from).Append(">")
                        .Append(colourRange._from).Append("</color> to <color=").Append(colourRange._to).Append(">")
                        .Append(colourRange._to).AppendLine("</color>");
                }
            }
            else if (!isMessage && _configuration.NameUseBlacklist)
            {
                additionalInfoBuilder.AppendLine("Blacklisted Colours:");
                foreach (var colour in _configuration.NameBlockColoursHex)
                {
                    additionalInfoBuilder.Append("- <color=").Append(colour).Append(">").Append(colour).AppendLine("</color>");
                }
                foreach (var colourRange in _configuration.NameBlacklistedRangeColoursHex)
                {
                    additionalInfoBuilder.Append("- From <color=").Append(colourRange._from).Append(">")
                        .Append(colourRange._from).Append("</color> to <color=").Append(colourRange._to).Append(">")
                        .Append(colourRange._to).AppendLine("</color>");
                }
            }

            Player.Message(player.Object as BasePlayer, GetMessage("ColoursInfo", player, availableCommands, additionalInfoBuilder.ToString()),76561198297741077);
        }

        private string ProcessColourMessage(string message, string colour) => $"<color={colour}>" + message + "</color>";

        private void ProcessColour(IPlayer player, IPlayer target, string colLower, string[] colours, bool isMessage = false, string groupName = "")
        {
            var isGroup = !string.IsNullOrEmpty(groupName);
            var isCalledOnto = player != target && !isGroup;
            var key = isGroup ? groupName : target.Id;
            
            if (!isGroup && !allColourData.ContainsKey(target.Id)) allColourData.Add(target.Id, new PlayerData());
            else if (isGroup && !allColourData.ContainsKey(groupName)) allColourData.Add(groupName, new PlayerData(true));

            if (colLower == "gradient")
            {
                if ((!isMessage && !CanNameGradient(player)) || (isMessage && !CanMessageGradient(player)))
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("NoPermissionGradient", player, isMessage ? "message" : "name"),76561198297741077);
                    return;
                }

                colours = colours.Where(col =>
                    isMessage
                        ? IsValidMessage(col, player)
                        : IsValidName(col, player) && IsValidColour(col) &&
                          IsInvalidCharacter(col) == null).ToArray();
                if (colours.Length < 2)
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("IncorrectGradientUsageArgs", player,
                        isMessage ? _configuration.MessageColourCommands[0] : _configuration.NameColourCommands[0]),76561198297741077);
                    return;
                }
                string gradientName = ProcessGradient(isMessage ? "Example Message" : target.Name, colours, isMessage, player);
                if (gradientName.Equals(string.Empty))
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("IncorrectGradientUsage", player,
                        isMessage ? _configuration.MessageColourCommands[0] : _configuration.NameColourCommands[0]),76561198297741077);
                    return;
                }
                if (isMessage)
                {
                    allColourData[key].MessageColour = string.Empty;
                    allColourData[key].MessageGradientArgs = colours;
                }
                else
                {
                    allColourData[key].NameColour = string.Empty;
                    allColourData[key].NameGradientArgs = colours;
                    if (!isGroup)
                    {
                        if (!cachedData.ContainsKey(key)) cachedData.Add(key, new CachePlayerData(gradientName, GetPrimaryUserGroup(player.Id)));
                        else cachedData[key].NameColourGradient = gradientName;
                    }
                }
                if (isGroup) ClearCache();

                if (target.IsConnected) Player.Message(target.Object as BasePlayer,GetMessage("GradientChanged", target, GetCorrectLang(isGroup, isMessage, key), gradientName),76561198297741077);
                if (isCalledOnto) Player.Message(player.Object as BasePlayer, GetMessage("GradientChangedFor", player, target.Name, isMessage ? "message" : "name", gradientName),76561198297741077);
                return;
            }
            if (colLower == "reset" || colLower == "clear" || colLower == "remove")
            {
                if (isMessage)
                {
                    allColourData[key].MessageColour = string.Empty;
                    allColourData[key].MessageGradientArgs = null;
                } 
                else
                {
                    allColourData[key].NameColour = string.Empty;
                    allColourData[key].NameGradientArgs = null;
                    if (cachedData.ContainsKey(key)) cachedData.Remove(key);
                }

                if (string.IsNullOrEmpty(allColourData[key].NameColour) &&
                    allColourData[key].NameGradientArgs == null &&
                    string.IsNullOrEmpty(allColourData[key].MessageColour) &&
                    allColourData[key].MessageGradientArgs == null)
                {
                    allColourData.Remove(key);
                }

                if (isGroup)
                {
                    ClearCache();
                }

                if (target.IsConnected) Player.Message(target.Object as BasePlayer,GetMessage("ColourRemoved", target, GetCorrectLang(isGroup, isMessage, key)),76561198297741077);
                if (isCalledOnto) Player.Message(player.Object as BasePlayer, GetMessage("ColourRemovedFor", player, target.Name, isMessage ? "message" : "name"),76561198297741077);
                return;
            }
            if (colLower == "random")
            {
                if (!isMessage && !CanNameRandomColour(player) || isMessage && !CanMessageRandomColour(player))
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("NoPermissionRandom", player, isMessage ? "message" : "name"),76561198297741077);
                    return;
                }
                colLower = GetRndColour();
                if (isMessage) ChangeMessageColour(key, colLower, null);
                else ChangeNameColour(key, colLower, null);
                if (isGroup) ClearCache();

                if (target.IsConnected) Player.Message(target.Object as BasePlayer,GetMessage("RndColour", target, GetCorrectLang(isGroup, isMessage, key), colLower),76561198297741077);
                if (isCalledOnto) Player.Message(player.Object as BasePlayer, GetMessage("RndColourFor", player, isMessage ? "Message" : "Name", target.Name, colLower),76561198297741077);
                return;
            }
            if (colLower == "rainbow")
            {
                if (isMessage && !HasMessageRainbow(player) || !HasNameRainbow(player))
                {
                    Player.Message(player.Object as BasePlayer, GetMessage("NoPermissionRainbow", player),76561198297741077);
                    return;
                }

                if (isMessage) ChangeMessageColour(key, string.Empty, _configuration.RainbowColours);
                else ChangeNameColour(key, string.Empty, _configuration.RainbowColours);
                if (isGroup) ClearCache();

                if (target.IsConnected) Player.Message(target.Object as BasePlayer,GetMessage("RainbowColour", target, GetCorrectLang(isGroup, isMessage, key)),76561198297741077);
                if (isCalledOnto) Player.Message(player.Object as BasePlayer, GetMessage("RainbowColourFor", player, isMessage ? "Message" : "Name", target.Name),76561198297741077);
                return;
            }
            string invalidChar;
            if ((invalidChar = IsInvalidCharacter(colLower)) != null)
            {
                Player.Message(player.Object as BasePlayer, GetMessage("InvalidCharacters", player, invalidChar),76561198297741077);
                return;
            }
            if (!IsValidColour(colLower))
            {
                Player.Message(player.Object as BasePlayer, GetMessage("InvalidColour", player),76561198297741077);
                return;
            }
            if (isMessage ? !IsValidMessage(colLower, player) : !IsValidName(colLower, player))
            {
                Player.Message(player.Object as BasePlayer, GetMessage("InvalidColour", player),76561198297741077);
                return;
            }

            if (isMessage) ChangeMessageColour(key, colLower, null);
            else ChangeNameColour(key, colLower, null);

            if (isCalledOnto) Player.Message(player.Object as BasePlayer, GetMessage("ColourChangedFor", player, target.Name, isMessage ? "message" : "name", colLower),76561198297741077);
            else if (isGroup && target.IsConnected) Player.Message(target.Object as BasePlayer,GetMessage("ColourChangedFor", player, key, isMessage ? "message" : "name", colLower),76561198297741077);
            else if (target.IsConnected) Player.Message(target.Object as BasePlayer,GetMessage("ColourChanged", target, isMessage ? "Message" : "Name", colLower),76561198297741077);
            if (isGroup) ClearCache();
        }

        private string ProcessGradient(string name, string[] colourArgs, bool isMessage = false, IPlayer iPlayer = null)
        {
            var gradientName = _sharedStringBuilder;
            gradientName.Clear();
            
            var colours = Pool.GetList<Color>();
            Color startColour;
            Color endColour;

            var nameLength = name.Length;
            var gradientsSteps = nameLength / (colourArgs.Length - 1);
            if (gradientsSteps <= 1)
            {
                for (var i = 0; i < nameLength; i++)
                {
                    if (i > colourArgs.Length - 1)
                        ColorUtility.TryParseHtmlString(colourArgs[colourArgs.Length - 1], out startColour);
                    else 
                        ColorUtility.TryParseHtmlString(colourArgs[i], out startColour);
                    
                    colours.Add(startColour);
                }
            }
            else
            {
                var gradientIterations = nameLength / gradientsSteps;
                for (var i = 0; i < gradientIterations; i++)
                {
                    if (colours.Count >= nameLength) 
                        continue;
                    if (i > colourArgs.Length - 1) 
                        ColorUtility.TryParseHtmlString(colourArgs[colourArgs.Length - 1], out startColour);
                    else 
                        ColorUtility.TryParseHtmlString(colourArgs[i], out startColour);
                    if (i >= colourArgs.Length - 1) 
                        endColour = startColour;
                    else 
                        ColorUtility.TryParseHtmlString(colourArgs[i + 1], out endColour);
                    GetAndAddGradients(startColour, endColour, gradientsSteps, colours);
                }
                if (colours.Count < nameLength)
                {
                    ColorUtility.TryParseHtmlString(colourArgs[colourArgs.Length - 1], out endColour);
                    while (colours.Count < name.Length) 
                        colours.Add(endColour);
                }
            }
            
            for (var i = 0; i < colours.Count; i++)
            {
                gradientName.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(colours[i])).Append(">")
                    .Append(name[i]).Append("</color>");
            }
            Pool.FreeList(ref colours);
            
            return gradientName.ToString();
        }

        /// <summary>
        /// Gets and adds gradient colours to provided results list
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="steps"></param>
        /// <param name="results"></param>
        private void GetAndAddGradients(Color start, Color end, int steps, List<Color> results)
        {
            var stepR = ((end.r - start.r) / (steps - 1));
            var stepG = ((end.g - start.g) / (steps - 1));
            var stepB = ((end.b - start.b) / (steps - 1));

            for (var i = 0; i < steps; i++)
                results.Add(new Color(start.r + (stepR * i), start.g + (stepG * i), start.b + (stepB * i)));
        }

        private readonly Random _random = new Random();
        private string GetRndColour() => $"#{_random.Next(0x1000000):X6}";

        private string IsInvalidCharacter(string input) => _configuration.BlockedValues.FirstOrDefault(x => input.Contains(x));

        private void ClearUpData()
        {
            if (_configuration.InactivityRemovalTime == 0) 
                return;

            var copy = new Dictionary<string, PlayerData>(allColourData);
            foreach (var colData in copy)
            {
                if (colData.Value.LastActive == 0)
                    continue;
                
                if (colData.Value.LastActive + (_configuration.InactivityRemovalTime * 86400) < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) 
                    allColourData.Remove(colData.Key);
            }
        }

        private void ClearCache()
        {
            var cachedCopy = new Dictionary<string, CachePlayerData>(cachedData);
            foreach (var cache in cachedCopy) cachedData.Remove(cache.Key);
        }

        private void ClearCache(string Id)
        {
            if (cachedData.ContainsKey(Id)) cachedData.Remove(Id);
        }

        private string GetCorrectLang(bool isGroup, bool isMessage, string key) => isGroup
            ?
            isMessage ? $"Group {key} message" : $"Group {key} name"
            : isMessage
                ? "Message"
                : "Name";

        private string GetPrimaryUserGroup(string Id)
        {
            var groups = permission.GetUserGroups(Id);

            var primaryGroup = string.Empty;
            var groupRank = -1;
            foreach (var group in groups)
            {
                if (!allColourData.ContainsKey(group)) continue;
                var currentGroupRank = permission.GetGroupRank(group);
                if (currentGroupRank > groupRank)
                {
                    groupRank = currentGroupRank;
                    primaryGroup = group;
                }
            }
            return primaryGroup;
        }

        private ColouredChatMessage FromMessage(IPlayer player, Chat.ChatChannel channel, string message)
        {
            PlayerData playerData;
            if (!allColourData.TryGetValue(player.Id, out playerData) || playerData == null)
                playerData = new PlayerData();
            
            var colouredNameData = GetColouredName(player, playerData);
            colouredNameData.ChatChannel = channel;
            colouredNameData.Message = GetColouredMessage(player, playerData, message);
            return colouredNameData;
        }

        private ColouredChatMessage GetColouredName(IPlayer player, PlayerData playerData)
        {
            var playerUserName = player.Name;
            var playerColour = player.IsAdmin ? "#af5" : "#5af";
            var playerColourNonModified = playerColour;

            CachePlayerData cachedPlayerData;
            if (!cachedData.TryGetValue(player.Id, out cachedPlayerData) || cachedPlayerData == null)
            {
                var gradientName = string.Empty;
                if (playerData?.NameGradientArgs != null) 
                    gradientName = ProcessGradient(player.Name, playerData.NameGradientArgs, false, player);

                cachedData.Add(player.Id, cachedPlayerData = new CachePlayerData(gradientName, GetPrimaryUserGroup(player.Id)));
            }

            if (HasNameShowPerm(player))
            {
                //Gradient Handling
                if (playerData?.NameGradientArgs != null) 
                    playerUserName = cachedPlayerData.NameColourGradient;
                
                else if (!string.IsNullOrEmpty(playerData?.NameColour)) 
                    playerColour = playerData.NameColour;
                
                else if (playerUserName == player.Name && !string.IsNullOrEmpty(cachedPlayerData.NameColourGradient)) 
                    playerUserName = cachedPlayerData.NameColourGradient;
            }
            
            if (allColourData.ContainsKey(cachedPlayerData.PrimaryGroup))
            {
                var groupData = allColourData[cachedPlayerData.PrimaryGroup];
                if (playerUserName == player.Name && playerColour == playerColourNonModified)
                {
                    if (groupData?.NameGradientArgs != null)
                    {
                        playerUserName = string.IsNullOrEmpty(cachedPlayerData.NameColourGradient)
                            ? cachedPlayerData.NameColourGradient = ProcessGradient(player.Name,
                                groupData.NameGradientArgs, false, player)
                            : cachedPlayerData.NameColourGradient;
                    }
                    else if (!string.IsNullOrEmpty(groupData?.NameColour)) 
                        playerColour = groupData.NameColour;
                }
            }

            return new ColouredChatMessage()
            {
                Player = player, Name = playerUserName,
                Colour = (playerColour == playerColourNonModified && BetterChatIns()) ? string.Empty : playerColour
            };
        }

        private string GetColouredMessage(IPlayer player, PlayerData playerData, string message)
        {
            var playerMessage = message;

            if (HasNameShowPerm(player))
            {
                if (playerData?.MessageGradientArgs != null) 
                    playerMessage = ProcessGradient(message, playerData.MessageGradientArgs, true, player);
                else if (!string.IsNullOrEmpty(playerData?.MessageColour)) 
                    playerMessage = ProcessColourMessage(message, playerData.MessageColour);
            }

            //Group Handling
            var userPrimaryGroup = cachedData[player.Id].PrimaryGroup;
            if (allColourData.ContainsKey(userPrimaryGroup))
            {
                var groupData = allColourData[userPrimaryGroup];
                if (playerMessage == message)
                {
                    if (groupData?.MessageGradientArgs != null) 
                        playerMessage = ProcessGradient(message, groupData.MessageGradientArgs, true, player);
                    else if (!string.IsNullOrEmpty(groupData?.MessageColour)) 
                        playerMessage = ProcessColourMessage(message, groupData.MessageColour);
                }
            }

            return playerMessage;
        }

        private bool IsInHexRange(string hexCode,string rangeHexCode1, string rangeHexCode2)
        {
            Color mainColour;
            ColorUtility.TryParseHtmlString(hexCode, out mainColour);
            Color start;
            ColorUtility.TryParseHtmlString(rangeHexCode1, out start);
            Color end;
            ColorUtility.TryParseHtmlString(rangeHexCode2, out end);

            if ((mainColour.r >= start.r && mainColour.r <= end.r) &&
                (mainColour.g >= start.g && mainColour.g <= end.g) &&
                (mainColour.b >= start.b && mainColour.b <= end.b))
            {
                return true;
            }

            return false;
        }

        #endregion

        #region API

        private string API_GetNameColourHex(IPlayer player)
        {
            var playerData = new PlayerData();
            if (allColourData.ContainsKey(player.Id)) 
                playerData = allColourData[player.Id];

            var colouredData = GetColouredName(player, playerData);
            if (string.IsNullOrEmpty(colouredData.Colour))
            {
                return player.IsAdmin ? "#af5" : "#5af";
            }

            return colouredData.Colour;
        }

        private string API_GetColouredName(IPlayer player)
        {
            var playerData = new PlayerData();
            if (allColourData.ContainsKey(player.Id)) 
                playerData = allColourData[player.Id];

            var colouredData = GetColouredName(player, playerData);
            if (!string.IsNullOrEmpty(colouredData.Colour))
                return $"<color={colouredData.Colour}>{player.Name}</color>";

            return colouredData.Name;
        }

        private string API_GetColouredMessage(IPlayer player, string message)
        {
            var playerData = new PlayerData();
            if (allColourData.ContainsKey(player.Id)) playerData = allColourData[player.Id];

            return GetColouredMessage(player, playerData, message);
        }

        private string API_GetColouredChatMessage(IPlayer iPlayer, Chat.ChatChannel channel,
            string message)
        {
            var colouredChatMessage = FromMessage(iPlayer, channel, message);

            var formattedMessage = colouredChatMessage.GetChatOutput();

            if (BetterChatIns())
            {
                Dictionary<string, object> betterChatMessageData = BetterChat.CallHook("API_GetMessageData", iPlayer, message) as Dictionary<string, object>;

                if (!string.IsNullOrEmpty(colouredChatMessage.Name)) betterChatMessageData["Username"] = colouredChatMessage.Name;
                if (!string.IsNullOrEmpty(colouredChatMessage.Colour)) { ((Dictionary<string, object>)betterChatMessageData["UsernameSettings"])["Color"] = colouredChatMessage.Colour; }

                formattedMessage = BetterChat.CallHook("API_GetFormattedMessageFromDict", betterChatMessageData) as string;
            }

            return formattedMessage;
        }

        public struct ColouredChatMessage
        {
            public IPlayer Player;
            public Chat.ChatChannel ChatChannel;
            public string Name;
            public string Colour;
            public string Message;

            private static readonly Dictionary<string, object> _colouredChatDictionary = new Dictionary<string, object>();
            
            public ColouredChatMessage(IPlayer player, Chat.ChatChannel chatChannel, string name, string colour, string message)
            {
                Player = player;
                ChatChannel = chatChannel;
                Name = name;
                Colour = colour;
                Message = message;
            }

            /// <summary>
            /// Gets coloured chat dictionary
            /// <remarks>Note: this is a shared dictionary instance over all ColouredChatMessage's, do not store random stuff in here!</remarks>
            /// </summary>
            /// <returns></returns>
            public Dictionary<string, object> GetDictionary()
            {
                _colouredChatDictionary[nameof(Player)] = Player;
                _colouredChatDictionary[nameof(ChatChannel)] = ChatChannel;
                _colouredChatDictionary[nameof(Name)] = Name;
                _colouredChatDictionary[nameof(Colour)] = Colour;
                _colouredChatDictionary[nameof(Message)] = Message;
                return _colouredChatDictionary;
            }

            public static ColouredChatMessage FromDictionary(Dictionary<string, object> dict)
            {
                return new ColouredChatMessage()
                {
                    Player = dict[nameof(Player)] as IPlayer,
                    ChatChannel = (Chat.ChatChannel)dict[nameof(ChatChannel)],
                    Name = dict[nameof(Name)] as string,
                    Colour = dict[nameof(Colour)] as string,
                    Message = dict[nameof(Message)] as string,
                };
            }     
            
            public string GetChatOutput()
            {
                return string.Format(ChatFormat, $"<color={Colour}>{Name}</color>", Message);
            }
        }
        
        #endregion

    }
}
