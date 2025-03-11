using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using UnityEngine; 
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("TopChat", "TopPlugin.ru", "1.0.3")]
    public class TopChat : RustPlugin
    {

        [PluginReference] Plugin IQRankSystem;
        #region Classes

        private class Prefix
        {
            [JsonProperty("Наименование префикса")]
            public string Name;
            [JsonProperty("Цвет префикса")]
            public string Color;
            [JsonProperty("Размер префикса")]
            public string Size;

            [JsonProperty("Скобки префикса")]
            public string Hooks;
        }

        private class Chatter
        {
            [JsonProperty("Подсказки")]
            public bool Tips;
            [JsonProperty("Звуки личных сообщений")]
            public bool Sound;
            [JsonProperty("Звук сообщений в чате")]
            public bool Censor;
            [JsonProperty("Сообщения в чат")] 
            public bool Chat = true;
            [JsonProperty("Сообщения в ПМ")] 
            public bool PM = true;
        }

        private class Name
        {
            public string Color;
        }
        
        private class Settings
        {
            [JsonProperty("Настройки текущего префикса")]
            public Prefix Prefixes;
            [JsonProperty("Настройки чата")]
            public Chatter Chatters;
            [JsonProperty("Настройки имени")]
            public Name Names;

            [JsonProperty("Список игнорируемых игроков")] 
            public Dictionary<ulong, string> IgnoreList = new Dictionary<ulong, string>();
            [JsonProperty("Последнее личное сообщение для"), JsonIgnore] 
            public BasePlayer ReplyTarget = null;

            public double UMT; 
            
            public Settings() {}
            public static Settings Generate()
            {
                return new Settings
                {
                    Prefixes = new Prefix
                    {
                        Name = "-",
                        Hooks = "-", 
                        Size = "<size=14>",
                        Color = "<color=#c692de>"
                    },
                    Chatters = new Chatter
                    {
                        Censor = false,
                        Sound = true, 
                        Tips = true
                    },
                    Names = new Name
                    {
                        Color = "<color=#c692de>"
                    }
                }; 
            } 
        }

        private class DataBase
        {
            public Dictionary<ulong, Settings> Settingses = new Dictionary<ulong, Settings>();

            public static DataBase LoadData() => Interface.Oxide.DataFileSystem.ExistsDatafile("TopChat") ? Interface.Oxide.DataFileSystem.ReadObject<DataBase>("TopChat") : new DataBase();

            public void SaveData()
            {
                Interface.Oxide.DataFileSystem.WriteObject("TopChat", this);
                Interface.Oxide.DataFileSystem.WriteObject($"Logs", MessagesLogs);
            } 
        }

        private class Configuration
        {
            [JsonProperty("Список доступных префиксов")]
            public Dictionary<string, string> Prefixes = new Dictionary<string, string>();
            [JsonProperty("Список доступных цветов")]
            public Dictionary<string, string> Colors = new Dictionary<string, string>();
            [JsonProperty("Список доступных размеров")]
            public Dictionary<string, string> Sizes = new Dictionary<string, string>();
            [JsonProperty("Список доступных типов префикса")]
            public Dictionary<string, string> Types = new Dictionary<string, string>();
            [JsonProperty("Список цензуры и исключений")]
            public Dictionary<string, List<string>> Censures = new Dictionary<string, List<string>>(); 
            [JsonProperty("Список доступных сообщений")]
            public List<string> Broadcaster = new List<string>();
            [JsonProperty("Интервал отправки сообщений")]
            public int BroadcastInterval = 300;
            [JsonProperty("Оповещать о подключении с префиксом")]
            public bool WelcomePrefix = true;

            [JsonProperty("SteamID отправителя в чат")] 
            public ulong ImageID = 76561199039326412;
            
            public static Configuration LoadDefaultConfiguration()
            {
                return new Configuration
                {
                    WelcomePrefix = true,
                    BroadcastInterval = 300,
                    Broadcaster = new List<string>
                    {
                        "Хочешь узнать о сервере побольше?\n<size=10>Жми на <color=#eb7d6a>саплю</color> в левом верхнем углу.</size>",
						"Группа ВК - <color=#eb7d6a>vk.com/blyadrust</color>",
						"Наш магазин - <color=#eb7d6a>blyadrust.ru</color>\n<size=10>Действуют скидки до <color=#eb7d6a>40%</color> на некоторые товары.</size>",
						"Увидел читера или нарушителей?\nОтправляй жалобу в <color=#eb7d6a>/report</color>",
						"Дискорд - <color=#eb7d6a>discord.gg/KUYpVop</color>",
						"Наш сайт - <color=#eb7d6a>blyadrust.ru</color>\n<size=10>Не забывай крутить <color=#eb7d6a>ежедневный</color> кейс!</size>"
                    },
                    Prefixes = new Dictionary<string, string>
                    {
						["Chat.Default"] = "НЕТ:-",
						["kits.secret2"] = "o:o",
						["Chat.Kaban1"] = "シ:シ",
						["Chat.Kaban2"] = "₪:₪",
						["Chat.Kaban4"] = "ВЕПРЬ:ВЕПРЬ",
						["Chat.Joker1"] = "¥:¥",
						["Chat.Joker2"] = "®:®",
						["Chat.Joker3"] = "〄:〄",
						["Chat.Joker4"] = "JOKER:JOKER",
						["Chat.Masnik1"] = "ס:ס",
						["Chat.Masnik2"] = "√:√",
						["Chat.Masnik3"] = "〤:〤",
						["Chat.Masnik4"] = "マ:マ",
						["Chat.Masnik5"] = "МЯСНИК:МЯСНИК",
						["Chat.Smert1"] = "ה:ה",
						["Chat.Smert2"] = "©:©",
						["Chat.Smert3"] = "〰:〰",
						["Chat.Smert4"] = "†:†",
						["Chat.Smert5"] = "€:€",
						["Chat.Smert6"] = "DEATH:DEATH",
						["Chat.Store1"] = "ש:ש",
						["Chat.Store2"] = "‡:‡",
						["Chat.Store3"] = "◊:◊",
						["Chat.Store4"] = "Ѫ:Ѫ",
						["Chat.Store5"] = "〶:〶"
                    },
                    Colors = new Dictionary<string, string>
                    {
						["Chat.Default"]= "СТАНДАРТНЫЙ:<color=#eb7d6a>",
						["Chat.White"]= "БЕЛЫЙ:<color=#d4d8de>",
						["Chat.Korange"]= "ОРАНЖЕВЕНЬКИЙ:<color=#ec9a49>",
						["Chat.Kcal"]= "КОРИЧНЕВЫЙ:<color=#b18755>",
						["Chat.Jgreen"]= "ЗЕЛЁНЫЙ:<color=#ADFF2F>",
						["Chat.Jpink"]= "СВ.РОЗОВЫЙ:<color=#e999c4>",
						["Chat.Jpaleturquoise"]= "БИРЮЗОВЫЙ:<color=#52d398>",
						["Chat.Mred"]= "КРАСНЫЙ:<color=#e25252>",
						["Chat.Mrosybrowm"]= "SALMON:<color=#FA8072>",
						["Chat.Mmistyrose"]= "KHAKI:<color=#f0e68c>",
						["Chat.Mdarkred"]= "ТЁМНО-КРАСНЫЙ:<color=#a86464>",
						["Chat.Sgray"]= "СЕРЫЙ:<color=#696969>",
						["Chat.Sskyblue"]= "НЕБЕСН.СИНИЙ:<color=#B1D6F1>",
						["Chat.Sdarkslategrey"]= "ТЁМНО-СЕР.ШИФЕР:<color=#FDD9B5>",
						["Chat.Sasdsd"]= "РОЗОВО-КОРИЧ.:<color=#bc8f8f>",
						["Chat.Sdarkgreen"]= "ТЁМНО-ЗЕЛЁНЫЙ:<color=#57a078>",
						["Chat.StoreColor1"]= "ЛИЛОВЫЙ:<color=#DB7093>",
						["Chat.StoreColor2"]= "СВЕТЛО-ГОЛУБОЙ:<color=#E0FFFF>",
						["Chat.StoreColor3"]= "ТЁМНО-ОЛИВКОВЫЙ:<color=#6B8E23>",
						["Chat.StoreColor4"]= "ЗОЛОТОЙ:<color=#FFD700>",
						["Chat.StoreColor5"]= "ТЁМНАЯ-ОРХИДЕЯ:<color=#9932CC>"
                    },
                    Sizes = new Dictionary<string, string>
                    {
                        ["Chat.Default"] = "СТАНДАРТНЫЙ:<size=14>",
                        ["Chat.Big"] = "БОЛЬШОЙ:<size=16>"
                    },
                    Types = new Dictionary<string, string>
                    {
                        ["Chat.Default"] = "НЕТ:-",
                        ["Chat.Hooks"] = "СКОБКИ:[]",
                        ["Chat.Limitter"] = "ПОЛОСА:|"
                    },
					Censures = new Dictionary<string,List<string>>{
						["бля"] = new List<string>{},
						["аху"] = new List<string>{},
						["впиз"] = new List<string>{},
						["въеб"] = new List<string>{},
						["выбля"] = new List<string>{},
						["выеб"] = new List<string>{},
						["выёб"] = new List<string>{},
						["гнид"] = new List<string>{},
						["гонд"] = new List<string>{},
						["доеб"] = new List<string>{},
						["долбо"] = new List<string>{},
						["дроч"] = new List<string>{},
						["ёб"] = new List<string>{},
						["елд"] = new List<string>{},
						["заеб"] = new List<string>{},
						["заёб"] = new List<string>{},
						["залуп"] = new List<string>{},
						["захуя"] = new List<string>{},
						["заяб"] = new List<string>{},
						["злоеб"] = new List<string>{},
						["ипа"] = new List<string>{},
						["лох"] = new List<string>{},
						["лошар"] = new List<string>{},
						["манд"] = new List<string>{"мандар"},
						["мля"] = new List<string>{},
						["мраз"] = new List<string>{},
						["муд"] = new List<string>{"мудр"},
						["наеб"] = new List<string>{},
						["наёб"] = new List<string>{},
						["напизд"] = new List<string>{},
						["нах"] = new List<string>{"наха","нахо","нахл"},
						["нех"] = new List<string>{"нехо","нехв", "неха"},
						["нии"] = new List<string>{},
						["обоср"] = new List<string>{}
					}
                };
            }
        }
        
        #endregion

        #region Variables

        private static TopChat _;
        private static DataBase Handler;
        private static Configuration Settingses;
        
        private static List<ulong> AntiSpamFilter = new List<ulong>();

        #endregion

        #region Initialization

        private void Unload(){
			Handler.SaveData();
			BasePlayer.activePlayerList.ToList().ForEach((player) =>
            {
				CuiHelper.DestroyUi(player, SettingsLayer);
				//CuiHelper.DestroyUi(player, SettingsLayer + ".WINDOW");
				CuiHelper.DestroyUi(player, SettingsLayer + ".INNER");
			});
		}
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settingses = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }

            SaveConfig();
        } 

        protected override void LoadDefaultConfig() => Settingses = Configuration.LoadDefaultConfiguration();
        protected override void SaveConfig()        => Config.WriteObject(Settingses); 
        
        private void OnServerInitialized()
        {
            _ = this;
            Handler = DataBase.LoadData();
             
            BasePlayer.activePlayerList.ToList().ForEach((player) =>
            {
                if (!Handler.Settingses.ContainsKey(player.userID))
                    Handler.Settingses.Add(player.userID, Settings.Generate());
            });  
            
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Logs"))
                MessagesLogs = Interface.Oxide.DataFileSystem.ReadObject<HashSet<Message>>($"Logs");
            if (MessagesLogs.Count > 1000)
            {
                MessagesLogs.Clear();
                PrintError("Data messages was cleared!"); 
            } 

            permission.RegisterPermission("topchat.mute", this);
              
            Settingses.Colors.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});
            Settingses.Types.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});
            Settingses.Sizes.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});
            Settingses.Prefixes.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});

            timer.Every(Settingses.BroadcastInterval, () =>
            {
                var message = Settingses.Broadcaster.GetRandom();
                foreach (var check in BasePlayer.activePlayerList.ToList())
                {
                    var settings = Handler.Settingses[check.userID];
                    if (!settings.Chatters.Tips)
                        continue;

                    check.SendConsoleCommand("chat.add", 0, Settingses.ImageID, message);
                    check.SendConsoleCommand($"echo [<color=white>ЧАТ</color>] {message}");
                }
            }).Callback();
            timer.Every(10, AntiSpamFilter.Clear);
            timer.Every(60, Handler.SaveData);
            timer.Every(300, () => BasePlayer.activePlayerList.ToList().ForEach(p => FetchStatus(p)));    
        }
 
        #endregion

        #region Commands

        [ConsoleCommand("UI_Chat")]
        private void CmdConsoleHandler(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!player || !arg.HasArgs(1)) return;

            switch (arg.Args[0].ToLower())
            {
                case "chatpm":
                {
                    Handler.Settingses[player.userID].Chatters.PM = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player,true); 
                    break;
                }
                case "chatglobal":
                {
                    Handler.Settingses[player.userID].Chatters.Chat = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player,true); 
                    break;
                }
                case "censor":
                {
                    Handler.Settingses[player.userID].Chatters.Censor = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player,true); 
                    break;
                }
                case "sound":
                {
                    Handler.Settingses[player.userID].Chatters.Sound = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player,true); 
                    break;
                }
                case "tips":
                {
                    Handler.Settingses[player.userID].Chatters.Tips = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player,true); 
                    break;
                }
                case "name_color": 
                {
                    var nameColor = Settingses.Colors.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Names.Color = nameColor.Value.Split(':')[1];
                    InitializeInterface(player,true); 
                    break;
                }
                case "prefix_color":
                {
                    var nameColor = Settingses.Colors.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Prefixes.Color = nameColor.Value.Split(':')[1];
                    InitializeInterface(player,true); 
                    break;
                }
                case "prefix_size":
                {
                    var nameColor = Settingses.Sizes.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Prefixes.Size = nameColor.Value.Split(':')[1];
                    InitializeInterface(player,true); 
                    break;
                }
                case "hook_type":
                {
                    var nameColor = Settingses.Types.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Prefixes.Hooks = nameColor.Value.Split(':')[1];
                    InitializeInterface(player,true); 
                    break;
                }
                case "prefix":
                {
                    var nameColor = Settingses.Prefixes.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Prefixes.Name = nameColor.Value.Split(':')[1];
                    InitializeInterface(player,true); 
                    break;
                }
            }
        }

        #endregion

        #region Hooks
  
        private class Response
        {
            [JsonProperty("country")]
            public string Country { get; set; }
        }
        
        void OnCorpse(BasePlayer player, BaseCorpse corpse)
        {
            if (!Handler.Settingses.ContainsKey(player.userID)) return;

            var obj = corpse.GetComponent<PlayerCorpse>();
            if (obj == null) return;
            obj._playerName = PrepareNick(player);
        }

        private void FetchStatus(BasePlayer player)
        {
            if (!Handler.Settingses.ContainsKey(player.userID))
            {
                OnPlayerConnected(player);
                FetchStatus(player);
                return;
            }
            
            var settings = Handler.Settingses[player.userID];            
            if (Settingses.Prefixes.All(p => !p.Value.Contains(settings.Prefixes.Name)) || !permission.UserHasPermission(player.UserIDString, Settingses.Prefixes.FirstOrDefault(p => p.Value.Contains(settings.Prefixes.Name)).Key))
            {
                settings.Prefixes.Name = Settingses.Prefixes.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 				
            }
            if (Settingses.Colors.All(p => !p.Value.Contains(settings.Prefixes.Color)) || !permission.UserHasPermission(player.UserIDString, Settingses.Colors.FirstOrDefault(p => p.Value.Contains(settings.Prefixes.Color)).Key))
            {
                settings.Prefixes.Color = Settingses.Colors.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 
            }
            if (Settingses.Types.All(p => !p.Value.Contains(settings.Prefixes.Hooks)) || !permission.UserHasPermission(player.UserIDString, Settingses.Types.FirstOrDefault(p => p.Value.Contains(settings.Prefixes.Hooks)).Key))
            {
                 settings.Prefixes.Hooks = Settingses.Types.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 
            } 
            if (Settingses.Sizes.All(p => !p.Value.Contains(settings.Prefixes.Size)) || !permission.UserHasPermission(player.UserIDString, Settingses.Sizes.FirstOrDefault(p => p.Value.Contains(settings.Prefixes.Size)).Key))
            {
                settings.Prefixes.Size = Settingses.Sizes.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 
            }
            if (Settingses.Colors.All(p => !p.Value.Contains(settings.Names.Color)) || !permission.UserHasPermission(player.UserIDString, Settingses.Colors.FirstOrDefault(p => p.Value.Contains(settings.Names.Color)).Key))
            { 
                settings.Names.Color = Settingses.Colors.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 
            } 
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (!Handler.Settingses.ContainsKey(player.userID))
                Handler.Settingses.Add(player.userID, Settings.Generate());
            
            FetchStatus(player); 
/*
			foreach (var check in BasePlayer.activePlayerList.ToList())
			{ 
				check.SendConsoleCommand("chat.add", 0, player.userID, $"<size=12>Игрок {_.PrepareNickForConnect(player)} присоединился!</size>");
			}*/
        }

        private static HashSet<Message> MessagesLogs = new HashSet<Message>();
        
        #region Helpers

        private void Broadcast(string text)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!Handler.Settingses.ContainsKey(player.userID))
                    continue;
                    
                var targetSettings = Handler.Settingses[player.userID];
                if (!targetSettings.Chatters.Tips) continue;
                
                SafeMessage(player, text, Settingses.ImageID);
            }
        }

        [ChatCommand("mute")]
        void ChatMute(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "topchat.mute"))
            {
                SendReply(player, "<size=12>У вас нет доступа к команде /mute</size>");
                return;
            }

            if (args.Length < 3)
            {
                SendReply(player, "<size=12>Пример использования:\n/mute <color=#ee3e61>[имя или steamid]</color> <color=#ee3e61>[время мута (в секундах)]</color> <color=#ee3e61>[причина мута]</color></size>");
                return;
            }

            var target = FindBasePlayer(args[0]);
            if (target == null)
            {
                SendReply(player, $"<size=12>Игрок не найден!</size>");
                return;
            }

            MutePlayer(target, player.displayName, int.Parse(args[1]), args[2]);
        }

        [ChatCommand("unmute")]
        void ChatUnMute(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "topchat.mute"))
            {
                SendReply(player, "<size=12>У вас нет доступа к команде /unmute</size>");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "<size=12>Пример использования:\n/unmute <color=#ee3e61>[имя или steamid]</color></size>");
                return;
            }

            var target = FindBasePlayer(args[0]);
            if (target == null)
            {
                SendReply(player, $"<size=12>Игрок не найден!</size>");
                return;
            }

            MutePlayer(player, target.displayName, 0, "");
        }

        private void MutePlayer(BasePlayer player, string initiatorName, int time, string reason)
        {
            var settings = Handler.Settingses[player.userID];
            
            if (time == 0)
            {
                Broadcast($"<color=#90e095>{initiatorName}</color> разблокировал чат игроку <color=#e68585>{player.displayName}</color>");
                Handler.Settingses[player.userID].UMT = 0;
            }
            else 
            {
                Broadcast($"<color=#90e095>{initiatorName}</color> выдал мут игроку <color=#e68585>{player.displayName}</color>\n" +
                          $"  <size=12><color=#e3e3e3>Причина: {reason} [{TimeSpan.FromSeconds(time).ToShortString()}]</color></size>");
                Handler.Settingses[player.userID].UMT = Time() + time;
            }
        }

        BasePlayer FindBasePlayer(string nameOrUserId)
        {
            nameOrUserId = nameOrUserId.ToLower();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrUserId) || player.UserIDString == nameOrUserId) return player;
            }
            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrUserId) || player.UserIDString == nameOrUserId) return player;
            }
            return default(BasePlayer);
        }

        private void SendPrivateMessage(BasePlayer initiator, BasePlayer target, string message)
            => SendPrivateMessage(initiator.userID, target.userID, message);

        private void SendPrivateMessage(ulong initiatorReadyId, ulong targetReadyId, string message)
        {
            BasePlayer initiator = BasePlayer.FindByID(initiatorReadyId);
            BasePlayer target    = BasePlayer.FindByID(targetReadyId);

            string targetReadyName                        = BasePlayer.FindByID(targetReadyId)?.displayName ?? "UNKNOWN";
            string initiatorReadyName                     = BasePlayer.FindByID(initiatorReadyId)?.displayName ?? "UNKNOWN";
            if (initiatorReadyId == 76561199039326412) initiatorReadyName = "ADMIN";

            if (target == null || !target.IsConnected)
            {
                SafeMessage(initiator, "Игрок не находится на сервере!");
                return;
            }

            var targetSettings = Handler.Settingses[targetReadyId];
            if (!targetSettings.IgnoreList.ContainsKey(initiatorReadyId))
            {
                if (!targetSettings.Chatters.PM)
                {
                    initiator.ChatMessage("У игрока отключены приватные сообщения");
                    return;
                }
                if (targetSettings.Chatters.Sound)
                {
                    Effect effect = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", target, 0, new Vector3(), new Vector3());
                    EffectNetwork.Send(effect, target.Connection);
                }

                if (initiator != null && initiator.IsConnected)
                {
                    targetSettings.ReplyTarget = initiator;
                }

                string prepareName = PrepareNick(new BasePlayer {userID = initiatorReadyId, displayName = initiatorReadyName});
                SafeMessage(target, $"<size=14>Личное сообщение от {prepareName}</size>\n<size=12>{message}</size>", initiatorReadyId);
            }
 
            string prepareTargetName = PrepareNick(new BasePlayer {userID = targetReadyId, displayName = targetReadyName});
            SafeMessage(initiator, $"<size=14>Личное сообщение для {prepareTargetName}</size>\n<size=12>{message}</size>", targetReadyId);
            AddMessage(initiatorReadyName, initiatorReadyId, message, targetReadyName, targetReadyId);
            
            DebugEx.Log((object) "[CHAT] " + initiatorReadyName + $" [{initiatorReadyId}] : > {target.displayName} [{target.userID}] :" + message); 
        }
		
        public class Message
        {
            [JsonProperty("id")] public string Id = CuiHelper.GetGuid();
            
            [JsonProperty("displayName")] public string DisplayName;
            [JsonProperty("userId")]      public string UserID;

            [JsonProperty("targetDisplayName")] public string TargetDisplayName;
            [JsonProperty("targetUserId")]      public string TargetUserId;

            [JsonProperty("text")]   public string Text;
            [JsonProperty("isTeam")] public bool   IsTeam;
            [JsonProperty("time")]   public string Time;
        }
		
        private void AddMessage(BasePlayer player, string message, bool team = false) => AddMessage(player.displayName, player.userID, message, team: team);

        private void AddMessage(string displayName, ulong userId, string message, string targetName = "", ulong targetId = 0UL, bool team = false)
        {
            var time       = DateTime.Now;
            var resultTime = $"{time.Hour}:{time.Minute}:{time.Second}";

            var chat = new Message
            {
                DisplayName       = displayName,
                Text              = message,
                TargetDisplayName = targetName,
                TargetUserId      = targetId.ToString(),
                Time              = resultTime,
                UserID            = userId.ToString(),
                IsTeam            = team
            };

            MessagesLogs.Add(chat);

            string logFormat = $"{displayName} [{targetId}]: {message}";
            if (team)
            {
                logFormat = "TEAM: " + logFormat;
            }
            else if (targetId != 0)
            {
                logFormat = $"{displayName} [{userId}] -> {targetName}[{targetId}]: {message}";
            }

            LogToFile($"{(!team ? targetId == 0 ? "Chat" : "PM" : "Team")}", logFormat, this);
        }

        #endregion

        #region Utils

        private static void LogInfo(string text)
        {
            //if (Settings) 
            //{
               // _.PrintWarning(text);
            //}
        }
        private static void UnloadPlugin() => _.NextTick(() => Interface.Oxide.UnloadPlugin("TopChat"));
      

        private static double Time() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private static void SafeMessage(BasePlayer player, string text, ulong avatarId = 0)
        {
            if (player == null || !player.IsConnected) return;

            player.SendConsoleCommand("chat.add", 0, avatarId, text);
            player.SendConsoleCommand($"echo <color=white>[ЧАТ]</color> {text}");
        }

        #endregion
 
        private object OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (IQRankSystem) return null;
            // TODO: HotFix
            while (message.Contains("size"))
                message = message.Replace("size", "");

            if (channel == ConVar.Chat.ChatChannel.Global)
            {
                if (AntiSpamFilter.Contains(player.userID) && !player.IsAdmin)
                {
                    player.ChatMessage("Вы не можете писать чаще, чем раз в <color=#fd7d6b>10</color> секунд!\n<size=12>Старайтесь писать всё в одном сообщении.</size>");
                    return false;
                }

                if (Handler.Settingses[player.userID].UMT > Time())
                {
                    player.ChatMessage("Ссори, у вас мут!");
                    return false;
                }

                string realMessage = PrepareMessage(player, message);
                string censureMessage = PrepareMessage(player, Censure(message));
                
                AntiSpamFilter.Add(player.userID); 
                foreach (var check in BasePlayer.activePlayerList.ToList())
                {
                    var settings = Handler.Settingses[check.userID];
                    if (settings.IgnoreList.ContainsKey(player.userID) || !settings.Chatters.Chat)
                        continue;
                    
                    string prepareMessagge = settings.Chatters.Censor ? censureMessage : realMessage; 
                    check.SendConsoleCommand("chat.add", 0, player.userID, prepareMessagge);
                    check.SendConsoleCommand($"echo [<color=white>ЧАТ</color>] {prepareMessagge}");
                }
            
                DebugEx.Log((object) ("[CHAT] " + player.displayName + $" [{player.UserIDString}] : " + message));
                LogChat(player.displayName + $" [{player.UserIDString}]: " + message);
                AddMessage(player, message);
                return false;
            }
            else
            {
                foreach (var check in player.Team.members)
                {
                    var settings = Handler.Settingses[player.userID]; 
                    if (settings.IgnoreList.ContainsKey(player.userID))
                        continue; 

                    var target = BasePlayer.FindByID(check);
                    if (target == null || !target.IsConnected) continue;
                    
                    string prepareMessagge = PrepareMessage(player, message, true);
                    target.SendConsoleCommand("chat.add", 0, player.userID, prepareMessagge);
                    target.SendConsoleCommand($"echo [<color=white>TEAM</color>] {prepareMessagge}");
                }
            
                DebugEx.Log((object) ("[CHAT] " + player.displayName + $" [{player.UserIDString}] : " + "TEAM> " + message));
                LogChat(player.displayName + $" [{player.UserIDString}]: " + message);
                AddMessage(player, message, true); 
                return false;
            }
            return null;
        }

        #endregion

        #region Commands

        [ChatCommand("chat")]
        private void CmdChatCommandSecret(BasePlayer player, string command, string[] args) => InitializeInterface(player);
        
        [ChatCommand("ignore")]
        private void CmdChatPersonalIgnore(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                player.ChatMessage($"Вы <color=orange>неправильно</color> используете команду!\n<size=12>/ignore <игрок> - отправить сообщение");
                return;
            }

            var targetSearch = args[0].ToLower();
            
            var target = BasePlayer.activePlayerList.ToList().FirstOrDefault(p => p.displayName.ToLower().Contains(targetSearch) || p.UserIDString == targetSearch);
            if (target == null || !target.IsConnected)
            {
                player.ChatMessage($"Игрок не найден!");
                return;
            }

            var settings = Handler.Settingses[player.userID];
            if (settings.IgnoreList.ContainsKey(target.userID))
            {
                player.ChatMessage($"Вы больше <color=orange>не игнорируете</color> этого игрока!");
                settings.IgnoreList.Remove(target.userID);
                return;
            }
            else
            {
                player.ChatMessage($"Теперь вы <color=orange>игнорируете</color> этого игрока!");
                settings.IgnoreList.Add(target.userID, target.displayName);
                return;
            }
        }

        [ChatCommand("r")]
        private void CmdChatPersonalReply(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                player.ChatMessage($"Вы <color=orange>неправильно</color> используете команду!\n<size=12>/r <сообщение> - отправить сообщение</size>");
                return;
            }

            var message = "";

            for (var i = 0; i < args.Length; i++)
                message += $"{args[i]} ";

            var target = Handler.Settingses[player.userID].ReplyTarget;
            if (target == null || !target.IsConnected)
            {
                player.ChatMessage($"Игрок не найден!");
                return;
            }

            SendPrivateMessage(player, target, message); 
        }
        
        [ChatCommand("pm")]
        private void CmdChatPersonalMessage(BasePlayer player, string command, string[] args)
        {
            if (args.Length <= 1)
            {
                player.ChatMessage($"Вы <color=orange>неправильно</color> используете команду!\n<size=12>/pm <имя> <сообщение> - отправить сообщение</size>");
                return;
            }

            var targetSearch = args[0].ToLower();
            var message      = "";

            for (var i = 1; i < args.Length; i++)
                message += $"{args[i]} ";

            var target = BasePlayer.activePlayerList.ToList().FirstOrDefault(p => p.displayName.ToLower().Contains(targetSearch));
            if (target == null || !target.IsConnected)
            {
                player.ChatMessage($"Игрок не найден!");
                return;
            }
            
            SendPrivateMessage(player, target, message);
        }
 
        #endregion

        #region Interface 
		[ConsoleCommand("UI_RM_Handler")]
        private void CmdConsoleRustMenu(ConsoleSystem.Arg args)
        {
			var player = args.Player();
            if (player == null || !args.HasArgs(1)) return;

            switch (args.Args[0].ToLower())
            {
                case "close":
                { 
					CuiHelper.DestroyUi(player, SettingsLayer);
					//CuiHelper.DestroyUi(player, SettingsLayer + ".WINDOW");
					CuiHelper.DestroyUi(player, SettingsLayer + ".INNER");

                    break;
				}
			}
		
		}
        private string SettingsLayer = "UI_SettingsLayer"; 
        private void InitializeInterface(BasePlayer player, bool reopen = false)
        {
            FetchStatus(player); 
			
            var settings = Handler.Settingses[player.userID];			 
			 
            CuiElementContainer container = new CuiElementContainer();
            if (!reopen)
            {
                CuiHelper.DestroyUi(player, SettingsLayer);
            
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                    Image         = {Color = "0 0 0 0" }
                }, "Overlay", SettingsLayer);
							
				container.Add(new CuiElement
				{
					Name = SettingsLayer + ".WINDOW_FRAME",
					Parent = SettingsLayer,
					Components =
					{
						new CuiImageComponent { Color = "0.235 0.227 0.180 0.90" },	
						new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-350 -350", OffsetMax = "350 350" }					
					}
				});

				container.Add(new CuiElement
				{
					Name = SettingsLayer + ".WINDOW",
					Parent = SettingsLayer + ".WINDOW_FRAME",
					Components =
					{
						new CuiImageComponent { Color = "0 0 0 0.87" },	
						new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"}					
					}
				});
			}
			CuiHelper.DestroyUi(player, SettingsLayer + ".INNER");
            
			container.Add(new CuiPanel()
			{ 
				CursorEnabled = false,
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
				Image         = {Color = "0 0 0 0.0" }
			}, SettingsLayer + ".WINDOW", SettingsLayer + ".INNER");
            
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"28 -90", OffsetMax = $"500 -20"} ,
                    Text = { Text = "НАСТРОЙКИ НИКА", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 40, Color = "0.93 0.89 0.85 1"}
                }, SettingsLayer + ".INNER");  
            
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"30 -130", OffsetMax = $"500 -80"} , 
                    Text = { Text = $"ТВОЙ НИК СЕЙЧАС: {PrepareNick(player)}", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
                }, SettingsLayer + ".INNER");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 125" },
                Image = { Color = "0.29411 0.27450 0.254901 1" }
            },  SettingsLayer + ".WINDOW", SettingsLayer + ".Info");//
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"28 -100", OffsetMax = $"-30 -5"},
                Text = { Text = "ИНФОРМАЦИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 40, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".Info");

            string helpText = "<b>Подсказки</b> - сообщения сервера с подсказками (которые отправляются в чат каждые ~3 минуты)\n<b>Глобальный чат</b> - отключает общий чат, но информационные сообщения от плагинов и раста остаются\n<b>Личные сообщения</b> - отключает/включает возможность писать вам в личные сообщения";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0", OffsetMax = "0 -60" },
                Text = { Text = helpText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "0.67 0.63 0.596"}
            }, SettingsLayer + ".Info");

            #region Switch layer Цвет
            
            var currentStatus = Settingses.Colors.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Names.Color);
            List<KeyValuePair<string, string>> possibleStatuses = Settingses.Colors.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
 
            var colorIndex = possibleStatuses.IndexOf(currentStatus);
            string leftCommand = $"UI_Chat name_color {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            string rightCommand = $"UI_Chat name_color {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            bool leftActive = colorIndex > 0;
            bool rightActive = colorIndex < possibleStatuses.Count - 1;
            
            string guid = CuiHelper.GetGuid(); 
            container.Add(new CuiLabel 
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -200", OffsetMax = $"-10 -130" },
                Text = { Text = $"ЦВЕТ НИКА<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".INNER", guid);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1"}
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel 
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".WINDOW");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".WINDOW");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 

            #region Switch layer Префикс
            
            currentStatus = Settingses.Prefixes.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Prefixes.Name);
            possibleStatuses = Settingses.Prefixes.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
            bool canChange = currentStatus.Value != "НЕТ:-";
            
            colorIndex = possibleStatuses.IndexOf(currentStatus);
            leftCommand = $"UI_Chat prefix {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            rightCommand = $"UI_Chat prefix {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            leftActive = colorIndex > 0;
            rightActive = colorIndex < possibleStatuses.Count - 1;
            
            guid = CuiHelper.GetGuid(); 
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -220", OffsetMax = $"-10 -165" },
                Text = { Text = $"ПРЕФИКС<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".INNER", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1"} 
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".WINDOW");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".WINDOW");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 

            #region Switch layer ЦветПрефикс
            
            
            currentStatus = Settingses.Colors.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Prefixes.Color);
            possibleStatuses = Settingses.Colors.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
 
            colorIndex = possibleStatuses.IndexOf(currentStatus);
            leftCommand = $"UI_Chat prefix_color {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            rightCommand = $"UI_Chat prefix_color {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            leftActive = colorIndex > 0 && canChange;
            rightActive = colorIndex < possibleStatuses.Count - 1 && canChange;
            
            guid = CuiHelper.GetGuid(); 
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -250", OffsetMax = $"-10 -200" },
                Text = { Text = $"ЦВЕТ ПРЕФИКСА<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".INNER", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1"}
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".WINDOW");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".WINDOW");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 

            #region Switch layer Размер префикса
            
            
            currentStatus = Settingses.Sizes.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Prefixes.Size);
            possibleStatuses = Settingses.Sizes.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
 
            colorIndex = possibleStatuses.IndexOf(currentStatus);
            leftCommand = $"UI_Chat prefix_size {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            rightCommand = $"UI_Chat prefix_size {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            leftActive = colorIndex > 0 && canChange;
            rightActive = colorIndex < possibleStatuses.Count - 1 && canChange;
            
            guid = CuiHelper.GetGuid(); 
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -290", OffsetMax = $"-10 -235" },
                Text = { Text = $"РАЗМЕР ПРЕФИКСА<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".INNER", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1"}
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".WINDOW");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".WINDOW");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 

            #region Switch layer Вид префикса
            
            
            currentStatus = Settingses.Types.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Prefixes.Hooks);
            possibleStatuses = Settingses.Types.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
 
            colorIndex = possibleStatuses.IndexOf(currentStatus);
            leftCommand = $"UI_Chat hook_type {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            rightCommand = $"UI_Chat hook_type {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            leftActive = colorIndex > 0 && canChange;
            rightActive = colorIndex < possibleStatuses.Count - 1 && canChange;
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -390", OffsetMax = $"-10 -270" },
                Text = { Text = $"ВИД ПРЕФИКСА<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".INNER", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1"}
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".WINDOW");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".WINDOW");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 
            
            
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"28 -500", OffsetMax = $"500 -320"} ,
                Text = { Text = "НАСТРОЙКИ ЧАТА", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 40, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".INNER");  
            

            #region Bool layer Подсказки
            
             
            var switchStatus = settings.Chatters.Tips;

            var mainCommand = $"UI_Chat tips {!switchStatus}"; 
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -500", OffsetMax = $"-10 -390" },
                Text = { Text = "ПОДСКАЗКИ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".INNER", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5"}
            }, guid, guid + ".P");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");
            
            container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = mainCommand },
                    Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
                }, guid + ".P"); 

            #endregion 
            
            #region Bool layer Звуки
            
             
            switchStatus = settings.Chatters.Sound;

            mainCommand = $"UI_Chat sound {!switchStatus}";  
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -700", OffsetMax = $"-10 -425" },
                    Text = { Text = "ЗВУК ЛИЧНЫХ СООБЩ.", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
                }, SettingsLayer + ".INNER", guid);

            container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                    Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5"}
                }, guid, guid + ".P");
            
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                    Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
                }, guid + ".P");
            
            container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = mainCommand },
                    Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
                }, guid + ".P"); 

            #endregion 
            
            
            #region Bool layer Вид префикса
            
             
            switchStatus = settings.Chatters.Censor;

            mainCommand = $"UI_Chat censor {!switchStatus}";  
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -700", OffsetMax = $"-10 -460" },
                    Text = { Text = "ЦЕНЗУРА", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
                }, SettingsLayer + ".INNER", guid);

            container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                    Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5"}
                }, guid, guid + ".P");
            
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                    Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
                }, guid + ".P");
            
            container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = mainCommand },
                    Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
                }, guid + ".P"); 

            #endregion 
            
            #region Bool layer Вид префикса
            
             
            switchStatus = settings.Chatters.Chat;

            mainCommand = $"UI_Chat chatglobal {!switchStatus}";  
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -700", OffsetMax = $"-10 -495" },
                Text = { Text = "ГЛОБАЛЬНЫЙ ЧАТ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".INNER", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5"}
            }, guid, guid + ".P");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = mainCommand },
                Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".P"); 

            #endregion 
            
            #region Bool layer Вид префикса
            
             
            switchStatus = settings.Chatters.PM;

            mainCommand = $"UI_Chat chatpm {!switchStatus}";  
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -700", OffsetMax = $"-10 -530" },
                Text = { Text = "ЛИЧНЫЕ СООБЩЕНИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".INNER", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5"}
            }, guid, guid + ".P");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = mainCommand },
                Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".P"); 

            #endregion 
			
			container.Add(new CuiButton 
			{
				RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-35 -35", OffsetMax = "-10 -10" },
				Button = { Color = "0.929 0.882 0.847 0.6", Command = "UI_RM_Handler close", Sprite = "assets/icons/close.png", Close = SettingsLayer }, 
				Text = { Text = "" }
			}, SettingsLayer + ".WINDOW");
			
            CuiHelper.AddUi(player, container);	
        }
 
        #endregion

        #region Utils

        private void LogChat(string message) => LogToFile("Chat", "[" + DateTime.Now.ToShortTimeString() + "]" + message, this);
        private void LogPM(string message) => LogToFile("PM", "[" + DateTime.Now.ToShortTimeString() + "]" + message, this);

        
        
        private string PrepareNickForConnect(BasePlayer player)
        {
            var settings = Handler.Settingses[player.userID];

            string prefixPrepare = "continue";
            switch (settings.Prefixes.Hooks) 
            {
                case "[]": prefixPrepare = $"[{settings.Prefixes.Color}{settings.Prefixes.Name}</color>]";
                    break;
                case "-": prefixPrepare = $"{settings.Prefixes.Color}{settings.Prefixes.Name}</color>";
                    break;
                case "|": prefixPrepare = $"{settings.Prefixes.Color}{settings.Prefixes.Name}</color> |";
                    break;
            }

            if (settings.Prefixes.Name == "-")
                prefixPrepare = "";
            
            string format = "continue";
            switch (settings.Prefixes.Hooks)
            {
                case "[]": format = $"{(Settingses.WelcomePrefix ? prefixPrepare : "")} {settings.Names.Color}{player.displayName}</color>";
                    break;
                case "-": format = $"{(Settingses.WelcomePrefix ? prefixPrepare : "")} {settings.Names.Color}{player.displayName}</color>";
                    break;
                case "|": format = $"{(Settingses.WelcomePrefix ? prefixPrepare : "")} {settings.Names.Color}{player.displayName}</color>";
                    break;
            }
            
            return format; 
        }
        private string PrepareNick(BasePlayer player)
        {
            var settings = Handler.Settingses[player.userID];

            string prefixPrepare = "continue";
            switch (settings.Prefixes.Hooks) 
            {
                case "[]": prefixPrepare = $"[{settings.Prefixes.Color}{settings.Prefixes.Name}</color>]";
                    break;
                case "-": prefixPrepare = $"{settings.Prefixes.Color}{settings.Prefixes.Name}</color>";
                    break;
                case "|": prefixPrepare = $"{settings.Prefixes.Color}{settings.Prefixes.Name}</color> |";
                    break;
            }

            if (settings.Prefixes.Name == "-")
                prefixPrepare = "";

            string name = player.displayName.Length > 26 ? player.displayName.Substring(0, 26) : player.displayName;

            string format = "continue";
            switch (settings.Prefixes.Hooks)
            {
                case "[]": format = $"{prefixPrepare} {settings.Names.Color}{name}</color>";
                    break;
                case "-": format = $"{prefixPrepare} {settings.Names.Color}{name}</color>";
                    break;
                case "|": format = $"{prefixPrepare} {settings.Names.Color}{name}</color>";
                    break;
            }
            
            return format; 
        }

        private string Censure(string message)
        {
            foreach (var mat in Settingses.Censures)
            {
                if (message.ToLower().Contains(mat.Key))
                {
                    bool shouldReplace = true;
                    foreach (var ist in mat.Value)
                    {
                        if (message.Contains(ist))
                        {
                            shouldReplace = false;
                            break;
                        }
                    }

                    if (shouldReplace) message = message.Replace(mat.Key, "***", StringComparison.CurrentCultureIgnoreCase);
                }
            }

            return message; 
        }
        
        private string PrepareMessage(BasePlayer player, string message, bool team = false) 
        {
            var settings = Handler.Settingses[player.userID];

            string prefixPrepare = "continue";
            switch (settings.Prefixes.Hooks)
            {
                case "[]": prefixPrepare = $"{settings.Prefixes.Size}[{settings.Prefixes.Color}{settings.Prefixes.Name}</color>]</size>";
                    break;
                case "-": prefixPrepare = $"{settings.Prefixes.Size}{settings.Prefixes.Color}{settings.Prefixes.Name}</color></size>";
                    break;
                case "|": prefixPrepare = $"{settings.Prefixes.Size}{settings.Prefixes.Color}{settings.Prefixes.Name}</color></size> |";
                    break;
            }

            if (settings.Prefixes.Name == "-")
                prefixPrepare = "";
            
            string format = "continue";
            switch (settings.Prefixes.Hooks)
            {
                case "[]": format = $"{prefixPrepare} {settings.Names.Color}{player.displayName}</color>: {message}";
                    break;
                case "-": format = $"{prefixPrepare} {settings.Names.Color}{player.displayName}</color>: {message}";
                    break;
                case "|": format = $"{prefixPrepare} {settings.Names.Color}{player.displayName}</color>: {message}";
                    break;
            }

            if (team) format = "<color=#ADFF2F>[TEAM]</color> " + format;
            
            return format;
        }

        #endregion
    }
}