using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Report", "FreePlugins", "1.0.0")]
    public class Report : RustPlugin
    {
		#region Configuration
		
		private Configuration config;
		private class Configuration
		{
			[JsonProperty("Основания для жалобы", Order = 0)]
			public List <string> Reason = new List <string>();			
			
			[JsonProperty("Кнопки с основанием - ширина кнопки", Order = 1)]
			public float ReasonSizeX;	
			[JsonProperty("Кнопки с основанием - отступ между кнопками", Order = 2)]
			public float ReasonSepX;	
			[JsonProperty("VK - AccessToken для отправки сообщений", Order = 3)]
			public  string AccessToken="";
			[JsonProperty("VK - ID чата сервера, пользователя или групповой беседы. Для отправки нескольким укажите id через запятую", Order = 4)]
			public  string VKServerID="";
			[JsonProperty("Оповещать администрацию если количество жалоб больше чем:", Order = 5)]
			public  int reportCount=0;
			
			public static Configuration GetNewConfiguration(){
				Configuration newConfig = new Configuration();
				newConfig.Reason = new List <string>(){"Макросы", "Игра+", "Читерство"};	
				newConfig.AccessToken = "";
				newConfig.ReasonSizeX=150f;
				newConfig.ReasonSepX=20f;
				newConfig.reportCount=0;
				newConfig.VKServerID="";
				return newConfig;
			}
		}
		
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
				if (config?.Reason == null) LoadDefaultConfig();
			}
			catch
			{
				LoadDefaultConfig();
			}
			NextTick(SaveConfig);
		}
		protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
		protected override void SaveConfig() => Config.WriteObject(config);
		#endregion
		
        #region Fields
        
        public static Report Instance = null;
        public string Layer = "UI_Report";
        //private readonly string[] config.Reason = {"Макросы", "Игра+", "Читерство"};

        #endregion
        
        #region Commands

        [ChatCommand("reportmenu")]
        void ReportMenu(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "report.admin"))
            {
                SendReply(player, "Недостаточно прав!");
                return;
            }

            var targetList = new List<BasePlayer>();

            foreach (var target in BasePlayer.activePlayerList)
            {
                if(storedData.players[target.userID].reportInfo.Count > config.reportCount) targetList.Add(target);
            }

            if (targetList.Count == 0)
            { 
                SendReply(player, "На сервере отсутствуют зарепорченные игроки!");
                return;
            }

            var message = "";
            
            foreach (var target in targetList)
            {
                var targetData = storedData.players[player.userID];
                if(!string.IsNullOrEmpty(message)) message += $"\nЖалобы на игрока: {target.displayName}. Количество жалоб: {targetData.reportInfo.Count}";
                else message += $"Жалобы на игрока: {target.displayName}. Количество жалоб: {targetData.reportInfo.Count}";
                
                foreach (var targetReports in targetData.reportInfo) message += $"\nПричина: {targetReports.reason}. Пожаловался: {targetReports.displayName}";
            }
            
            SendReply(player, message);
        }

        private bool AlreadyReported(ulong playerID, ulong targetID)
        {
            var searchPlayer = storedData.players[targetID].reportInfo.Find(x => x.userID == playerID);
            if(searchPlayer != null && searchPlayer.userID == playerID) return true;
            return false;
        }
        
        // ReSharper disable once UnusedMember.Local
        private void ChatCmdReport(BasePlayer player, string command, string[] args)
        {
            CuiHelper.DestroyUi(player, Layer);

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image =
                {
                    FadeIn = 0.2f,
                    Sprite = "assets/content/ui/ui.background.transparent.radial.psd",
                    Color = "0 0 0 1"
                }
            }, "Overlay", Layer);
            container.Add(new CuiPanel
            {
                Image =
                {
                    FadeIn = 0.2f,
                    Color = "0.2 0.2 0.17 0.7",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                }
            }, Layer);

            container.Add(new CuiLabel
            {
                Text = { Text = "РЕПОРТЫ", Align = TextAnchor.UpperCenter, FontSize = 40, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.3 1", AnchorMax = "0.7 1", OffsetMin = "0 -120", OffsetMax = "0 -56.6" }
            }, Layer);
            container.Add(new CuiLabel
            {
                Text = { Text = "Найдите в поиске игрока и оставьте на него жалобу.", Align = TextAnchor.UpperCenter, FontSize = 18, Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -120", OffsetMax = "0 -98" }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    GetImageComponent("https://static.moscow.ovh/images/games/rust//plugins/ultimate_ui/exit.png","Rep_exit_img"),
                    new CuiRectTransformComponent {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-73.9 20", OffsetMax = "-28.6 80"},
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent {Color = "0.33 0.87 0.59 0.6"},
                    new CuiRectTransformComponent {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-291.3 22.6", OffsetMax = "-108 25.2"}
                }
            });
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0",
                    Close = Layer
                },
                Text = { Text = "Покинуть страницу", Align = TextAnchor.UpperCenter, FontSize = 18 },
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-291.3 22.6", OffsetMax = "-108 49.2" },
            }, Layer);
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0",
                    Close = Layer
                },
                Text = { Text = "" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    GetImageComponent("https://static.moscow.ovh/images/games/rust//plugins/ultimate_ui/report/back.png","Rep_back_img"),
                    new CuiRectTransformComponent {AnchorMin = "0.5 0.42", AnchorMax = "0.5 0.42", OffsetMin = "-369.4 -195.3", OffsetMax = "-325.4 195.3"},
                }
            });
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "report.backpage"
                },
                Text = { Text = "" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-369.4 -195.3", OffsetMax = "-325.4 195.3" },
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    GetImageComponent("https://static.moscow.ovh/images/games/rust//plugins/ultimate_ui/report/next.png","Rep_next_img"),
                    new CuiRectTransformComponent {AnchorMin = "0.5 0.42", AnchorMax = "0.5 0.42", OffsetMin = "325.4 -195.3", OffsetMax = "369.4 195.3"}
                }
            });
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "report.nextpage"
                },
                Text = { Text = "" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "325.4 -195.3", OffsetMax = "369.4 195.3" }
            }, Layer);

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0.6"},
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-295.6 -179.3", OffsetMax = "295.6 -156"
                }
            }, Layer, Layer + ".InputTarget");
            container.Add(new CuiElement
            {
                Parent = Layer + ".InputTarget",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = "1 1 1 1",
                        CharsLimit = 32,
                        Align = TextAnchor.MiddleLeft,
                        Command = "report.inputtarget"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "4 0"}
                }
            });

            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0.33 0.87 0.59 0.6",
                    Command = "report.inputtarget find"
                },
                Text = { Text = "Найти игрока", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-100 0", OffsetMax = "0 0" }
            }, Layer + ".InputTarget");

            var posX = -(config.ReasonSizeX * config.Reason.Count + config.ReasonSepX * (config.Reason.Count - 1)) / 2f;
            for (var i = 0; i < config.Reason.Count; i++)
            {
                container.Add(new CuiButton
                {
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"report.setreason {i}"
                    },
                    Text = { Text = config.Reason[i], Align = TextAnchor.UpperCenter, FontSize = 18 },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = $"{posX} 22.6", OffsetMax = $"{posX + config.ReasonSizeX} 49.2" },
                }, Layer, Layer + $".Reason{i}");

                posX += config.ReasonSizeX + config.ReasonSepX;
            }

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent {Color = "0.87 0.33 0.33 0.6"},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "21 22.6", OffsetMax = $"{config.ReasonSizeX + 21} 25.2"}
                }
            });
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "report.sendreport"
                },
                Text = { Text = "Отправить", Align = TextAnchor.UpperCenter, FontSize = 18 },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "21 22.6", OffsetMax = $"{config.ReasonSizeX + 21} 49.2" }
            }, Layer);

            var data = _playersMenu[player] = new MenuData { Players = BasePlayer.activePlayerList.OrderBy(p => p.displayName).ToArray() };
            ShowPlayers(player, container, data);

            CuiHelper.AddUi(player, container);
        }
        
        // ReSharper disable once UnusedMember.Local
        private void ConsoleCmdBackPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            MenuData data;
            if (player == null || !_playersMenu.TryGetValue(player, out data)) return;
            var page = data.Page == 0 ? (int)Math.Ceiling(data.Players.Length / (float)50) - 1 : data.Page - 1;
            if (page == data.Page) return;
            data.Page = page;
            var container = new CuiElementContainer();
            ShowPlayers(player, container, data);
            CuiHelper.AddUi(player, container);
        }
        
        // ReSharper disable once UnusedMember.Local
        private void ConsoleCmdNextPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            MenuData data;
            if (player == null || !_playersMenu.TryGetValue(player, out data)) return;
            var page = data.Page == (int)Math.Ceiling(data.Players.Length / (float)50) - 1 ? 0 : data.Page + 1;
            if (page == data.Page) return;
            data.Page = page;
            var container = new CuiElementContainer();
            ShowPlayers(player, container, data);
            CuiHelper.AddUi(player, container);
        }

        // ReSharper disable once UnusedMember.Local
        private void ConsoleCmdSetTarget(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            MenuData data;
            if (player == null || !_playersMenu.TryGetValue(player, out data)) return;
            var id = arg.GetInt(0, -1);
            if (id < 0 || id >= data.Players.Length) return;
            data.Target = data.Players[id];
            CuiHelper.DestroyUi(player, Layer + $".Player{id}.Line");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0.33 0.87 0.59 0.6" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMax = "0 2.6" }
            }, Layer + $".Player{id}", Layer + $".Player{id}.Line");
            CuiHelper.AddUi(player, container);
        }

        private void ShowPlayers(BasePlayer player, CuiElementContainer container, MenuData data)
        {
            CuiHelper.DestroyUi(player, Layer + ".Players");
            if (data.Players == null) return;
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0.5 0.71", AnchorMax = "0.5 0.71", OffsetMin = "-316.8 -416.7", OffsetMax = "316.8 0"}
            }, Layer, Layer + ".Players");

            const int maxPlayersPage = 50;
            var count = data.Page * maxPlayersPage + Math.Min(maxPlayersPage, data.Players.Length - data.Page * maxPlayersPage);
            const float sizeX = 119.3f;
            const float sizeY = 33.3f;
            const float sep = 9.3f;
            var i = 0;
            var posX = 0f;
            var posY = 0f;
            for (var i2 = data.Page * maxPlayersPage; i2 < count; i2++)
            {
                var name = data.Players[i2].displayName;
                name = ClearString(name, true);
                if (name.Length > 14) name = name.Substring(0, 14) + "..";
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0.6", Command = $"report.settarget {i2}"},
                    Text = { Text = name, FontSize = 16, Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{posX} {posY - sizeY}", OffsetMax = $"{posX + sizeX} {posY}" }
                }, Layer + ".Players", Layer + $".Player{i}");
                if (++i % 5 == 0)
                {
                    posX = 0f;
                    posY -= sizeY + sep;
                }
                else posX += sizeX + sep;
                if (i >= maxPlayersPage) break;
            }
        }

        private static readonly char[] CacheString = new char[256];
        public static string ClearString(string str, bool removeHtml = true) // v1.1
        {
            var l = 0;
            for (var i = 0; i < str.Length; i++, l++)
            {
                var c = str[i];
                if (c < ' ') CacheString[l] = ' ';
                else
                {
                    switch (c)
                    {
                        case '\x5c': // \
                        {
                            if (!removeHtml) CacheString[l] = c;
                            else
                            {
                                CacheString[l] = c;
                                CacheString[++l] = '\x200B';
                            }
                            break;
                        }
                        case '"':
                        {
                            CacheString[l] = '\x27'; // '
                            CacheString[l] = '\x27';
                            break;
                        }
                        case '<':
                        {
                            if (!removeHtml) CacheString[l] = c;
                            else
                            {
                                CacheString[l] = c;
                                CacheString[++l] = '\x200B';
                            }
                            break;
                        }
                        default: CacheString[l] = c; break;
                    }
                }
            }
            return new string(CacheString, 0, l);
        }

        private class MenuData
        {
            public string Name;
            public BasePlayer Target;
            public BasePlayer[] Players;
            public int Page;
            public int Reason;
        }
        private readonly Dictionary<BasePlayer, MenuData> _playersMenu = new Dictionary<BasePlayer, MenuData>();

        // ReSharper disable once UnusedMember.Local
        private void ConsoleCmdInputTarget(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            MenuData data;
            if (player == null || !_playersMenu.TryGetValue(player, out data)) return;
            if (arg.GetString(0) != "find")
            {
                data.Name = arg.GetString(0, null);
                return;
            }
            if (!_playersMenu.TryGetValue(player, out data)) return;
            data.Target = null;
            data.Page = 0;
            if (string.IsNullOrEmpty(data.Name)) data.Players = BasePlayer.activePlayerList.OrderBy(p => p.displayName).ToArray();
            else
            {
                BasePlayer target;
                List<BasePlayer> players;
                data.Players = !FindPlayerByName(data.Name, out target, out players) && players.Count == 0
                    ? null
                    : players.OrderBy(p => p.displayName).ToArray();
            }
            var container = new CuiElementContainer();
            ShowPlayers(player, container, data);
            CuiHelper.AddUi(player, container);
        }

        // ReSharper disable once UnusedMember.Local
        private void ConsoleCmdSetReason(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            MenuData data;
            var id = arg.GetInt(0, -1);
            if (player == null || !_playersMenu.TryGetValue(player, out data) || id < 0 || id > config.Reason.Count) return;
            CuiHelper.DestroyUi(player, Layer + $".Reason{data.Reason}.Line");
            data.Reason = id;
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = Layer + $".Reason{id}.Line",
                Parent = Layer + $".Reason{id}",
                Components =
                {
                    new CuiImageComponent {Color = "0.33 0.87 0.59 0.6"},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 2.6"}
                }
            });
            CuiHelper.AddUi(player, container);
        }

        // ReSharper disable once UnusedMember.Local
        private void ConsoleCmdSendReport(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            MenuData data;
            if (player == null || !_playersMenu.TryGetValue(player, out data) || data.Target == null || data.Reason == -1) return;
            CuiHelper.DestroyUi(player, Layer);
            if (AlreadyReported(player.userID, data.Target.userID))
            {
                SendReply(player, "Вы уже отправляли жалобу на этого игрока.");
                return;
            }
            SendReply(player, "Ваша жалоба успешно принята и отправлена модераторам.");
            storedData.players[data.Target.userID].reportInfo.Add(new ReportInfo
            {
                displayName = player.displayName,
                reason = config.Reason[data.Reason],
                userID = player.userID,
            });
            SendReport(data.Target);
        }

        #endregion

        #region Methods

        void SendVK(string msg)
        {
            webrequest.EnqueuePost("https://api.vk.com/method/messages.send", $"random_id={UnityEngine.Random.Range(0, int.MaxValue)}" + "&peer_ids=" + config.VKServerID + "&message=" + msg + "&v=5.107&access_token=" + config.AccessToken + "", PostCallback, null);
			Puts("Report sent to VK");
        }
        
        void PostCallback(int code, string response)
        {
            
        }

        void SendReport(BasePlayer target)
        {             
            if (storedData.players[target.userID].reportInfo.Count < config.reportCount) return;
		
            var message = string.Format("Уважаемая модерация сервера {0}<br> Просьба проверить игрока {1}[{2}].<br> Количество репортов - {3}<br>", ConVar.Server.hostname, target.displayName, target.userID, storedData.players[target.userID].reportInfo.Count);
            for (var i = 0; i < storedData.players[target.userID].reportInfo.Count; i++)
            {
                var report = storedData.players[target.userID].reportInfo.ElementAt(i);
                
                message += string.Format("Пожаловался - {0} <br> Основная причина - {1}<br>",
                    report.displayName, report.reason);
            }

            message = message.Replace("{", " ");
            message = message.Replace("}", " ");
            SendVK(message); 
        }

        #endregion
        
        #region Hooks
        
        object OnBanSystemBan(ulong steam, ulong owner, string reason, uint banTime, ulong initiator)
        {
            BasePlayer playerBanned = null;
            BasePlayer initiatorBan = null;

            var message = $"{ConVar.Server.hostname}<br>Информация о бане игрока - ";
            TimeSpan time = new TimeSpan();
            var timeString = "бессрочно";
            var initiatorName = "CONSOLE";

            if (PlayerHelper.Find(steam.ToString(), out playerBanned))
            {
                if (banTime != 0)
                {
                    time = TimeSpan.FromSeconds(banTime);
                    timeString = $"{(int) time.TotalDays:0}д, {time.Hours:0}ч, {time.Minutes:0}м, {time.Seconds:00}с";
                }

                if (PlayerHelper.Find(initiator.ToString(), out initiatorBan))
                {
                    initiatorName = initiatorBan.displayName;
                }

                if (initiator == 0U)
                {
                    initiatorName = "CONSOLE";
                }
                
                message +=
                    $"{playerBanned.displayName}[{playerBanned.UserIDString}]<br>Время бана: {timeString}<br>Причина бана: {reason}<br> Забанил:{initiatorName}[{initiator}]";

                SendVK(message);
            }
            else
            {
                if (banTime != 0)
                {
                    time = TimeSpan.FromSeconds(banTime);
                    timeString = $"{(int) time.TotalDays:0}д, {time.Hours:0}ч, {time.Minutes:0}м, {time.Seconds:00}с";
                }

                if (PlayerHelper.Find(initiator.ToString(), out initiatorBan))
                {
                    initiatorName = initiatorBan.displayName;
                }

                message +=
                    $"Ник не найден[{steam}]<br>Время бана: {timeString}<br>Причина бана: {reason}<br>Забанил: {initiatorName}[{initiator}]";

                SendVK(message);
            }

            return null;
        }

        void OnChatPlusMute(ulong initiator, ulong steam, string reason, uint seconds)
        {

            BasePlayer playerBanned = null;
            BasePlayer initiatorBan = null;

            if (seconds >= 1 && seconds < 3600) return;
            
            var message = $"{ConVar.Server.hostname}<br>Информация о муте игрока - ";
            TimeSpan time = new TimeSpan();
            var timeString = "бессрочно";
            var initiatorName = "CONSOLE";

            

            if (PlayerHelper.Find(steam.ToString(), out playerBanned))
            {
                if (seconds != 0)
                {
                    time = TimeSpan.FromSeconds(seconds);
                    timeString = $"{(int) time.TotalDays:0}д, {time.Hours:0}ч, {time.Minutes:0}м, {time.Seconds:00}с";
                }

                if (PlayerHelper.Find(initiator.ToString(), out initiatorBan))
                {
                    initiatorName = initiatorBan.displayName;
                }


                message +=
                    $"{playerBanned.displayName}[{playerBanned.UserIDString}]<br>Время мута:{timeString}<br>Причина мута: {reason}<br>Замутил:{initiatorName}[{initiator}]";

                SendVK(message);
            }
            else
            {
                if (seconds != 0)
                {
                    time = TimeSpan.FromSeconds(seconds);
                    timeString = $"{(int) time.TotalDays:0}д, {time.Hours:0}ч, {time.Minutes:0}м, {time.Seconds:00}с";
                }

                if (PlayerHelper.Find(initiator.ToString(), out initiatorBan))
                {
                    initiatorName = initiatorBan.displayName;
                }

                message +=
                    $"Ник не найден[{steam}]<br>Время мута:{timeString}<br>Причина мута: {reason}<br>Замутил:{initiatorName}[{initiator}]";

                SendVK(message);
            }
        }

        void OnBanSystemUnban(ulong steam, ulong initiator)
        {
            string Name = "";
            var message = string.Empty;
            
            if (initiator == 0)
            {
                Name = "CONSOLE";
            }
            else
            {
                BasePlayer player = BasePlayer.FindByID(initiator);

                Name = player.displayName;
            }
            
            BasePlayer target = BasePlayer.Find(steam.ToString());

            if (target == null)
            {
                target = BasePlayer.FindSleeping(steam);
            }

            if (target != null)
            {
                message = $"Администратор {Name} разбанил -> {target.displayName}[{target.userID}]";
            }
            else
            {
                message = $"Администратор {Name} разбанил -> {steam}";
            }
            
            SendVK(message);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);
            SaveData();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if(!storedData.players.ContainsKey(player.userID)) storedData.players.Add(player.userID, new ReportData());
        }

        [HookMethod("OnCheckedPlayer")]
        void OnCheckedPlayer(BasePlayer player, BasePlayer target)
        {
            var targetData = storedData.players[target.userID];
            
            var msg = $"{player.displayName} вызвал на проверку игрока с ником {target.displayName}[{target.userID}]. <br> Количество жалоб на данного игрока - {targetData.reportInfo.Count}. <br> Сервер - {ConVar.Server.hostname}";
            targetData.reportInfo.Clear(); 
            
            SendVK(msg);
        }

        void OnServerInitialized()
        { 
            LoadData(); 
            permission.RegisterPermission("report.admin", this);
            Instance = this;

            foreach (var p in BasePlayer.activePlayerList) OnPlayerConnected(p);

            cmd.AddChatCommand("report", this, "ChatCmdReport");
            cmd.AddConsoleCommand("report.inputtarget", this, "ConsoleCmdInputTarget");
            cmd.AddConsoleCommand("report.backpage", this, "ConsoleCmdBackPage");
            cmd.AddConsoleCommand("report.nextpage", this, "ConsoleCmdNextPage");
            cmd.AddConsoleCommand("report.settarget", this, "ConsoleCmdSetTarget");
            cmd.AddConsoleCommand("report.setreason", this, "ConsoleCmdSetReason");
            cmd.AddConsoleCommand("report.sendreport", this, "ConsoleCmdSendReport");

            AddImage("https://static.moscow.ovh/images/games/rust//plugins/ultimate_ui/report/back.png","Rep_back_img");
            AddImage("https://static.moscow.ovh/images/games/rust//plugins/ultimate_ui/report/next.png","Rep_next_img");
            AddImage("https://static.moscow.ovh/images/games/rust//plugins/ultimate_ui/exit.png","Rep_exit_img");
        }

        #endregion

        #region Data

        class StoredData
        {
            public Dictionary<ulong, ReportData> players = new Dictionary<ulong, ReportData>();
        }

        class ReportData
        {
            [JsonProperty("ri")] public List<ReportInfo> reportInfo = new List<ReportInfo>();
        }

        class ReportInfo
        {
            public string displayName = "";
            public ulong userID = 0U;
            public string reason = "";
        }
        
        void SaveData()
        {
            ReportD.WriteObject(storedData);
        }

        void LoadData()
        {
            ReportD = Interface.Oxide.DataFileSystem.GetFile("Report/reports");
            try
            {
                storedData = ReportD.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }

        StoredData storedData;
        private DynamicConfigFile ReportD;

        #endregion

        #region Helper

        public static bool FindPlayerByName(string findString, out BasePlayer player, out List<BasePlayer> players)
        {
            players = new List<BasePlayer>();
            player = null;
            var matches = new List<BasePlayer>();
            foreach (var target in BasePlayer.activePlayerList)
            {
                if (string.Equals(target.displayName, findString, StringComparison.InvariantCultureIgnoreCase))
                {
                    matches.Clear();
                    matches.Add(target);
                    break;
                }
                if (target.displayName.IndexOf(findString, StringComparison.InvariantCultureIgnoreCase) != -1) matches.Add(target);
            }
            if (matches.Count == 0) return false;
            player = matches[0];
            players = matches;
            if (matches.Count == 1) return true;
            return false;
        }

        private static class PlayerHelper
        {
            private static bool FindPlayerPredicate(BasePlayer player, string nameOrUserId)
            {
                return player.displayName.IndexOf(nameOrUserId, StringComparison.OrdinalIgnoreCase) != -1 ||
                       player.UserIDString == nameOrUserId;
            }

            public static bool Find(string nameOrUserId, out BasePlayer target)
            {
                nameOrUserId = nameOrUserId.ToLower();
                foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
                {
                    if (FindPlayerPredicate(activePlayer, nameOrUserId))
                    {
                        target = activePlayer;
                        return true;
                    }
                }

                foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
                {
                    if (FindPlayerPredicate(sleepingPlayer, nameOrUserId))
                    {
                        target = sleepingPlayer;
                        return true;
                    }
                }

                target = null;
                return false;
            }

        }

        #endregion

		public CuiRawImageComponent GetAvatarImageComponent(ulong user_id, string color = "1.0 1.0 1.0 1.0"){
			
			if (plugins.Find("ImageLoader")) return plugins.Find("ImageLoader").Call("BuildAvatarImageComponent",user_id) as CuiRawImageComponent;
			if (plugins.Find("ImageLibrary")) {
				return new CuiRawImageComponent { Png = (string)plugins.Find("ImageLibrary").Call("GetImage", user_id.ToString()), Color = color, Sprite = "assets/content/textures/generic/fulltransparent.tga" };
			}
			return new CuiRawImageComponent {Url = "https://image.flaticon.com/icons/png/512/37/37943.png", Color = color, Sprite = "assets/content/textures/generic/fulltransparent.tga"};
		}
		public CuiRawImageComponent GetImageComponent(string url, string shortName="", string color = "1.0 1.0 1.0 1.0"){
			
			if (plugins.Find("ImageLoader")) return plugins.Find("ImageLoader").Call("BuildImageComponent",url) as CuiRawImageComponent;
			if (plugins.Find("ImageLibrary")) {
				if (!string.IsNullOrEmpty(shortName)) url = shortName;
				//Puts($"{url}: "+ (string)plugins.Find("ImageLibrary").Call("GetImage", url));
				return new CuiRawImageComponent { Png = (string)plugins.Find("ImageLibrary").Call("GetImage", url), Color = color, Sprite = "assets/content/textures/generic/fulltransparent.tga"};
			}
			return new CuiRawImageComponent {Url = url, Color = color, Sprite = "assets/content/textures/generic/fulltransparent.tga"};
		}
		public CuiRawImageComponent GetItemImageComponent(string shortName){
			string itemUrl = shortName;
			if (plugins.Find("ImageLoader")) {itemUrl = $"https://static.moscow.ovh/images/games/rust/icons/{shortName}.png";}
            return GetImageComponent(itemUrl, shortName);
		}
		public bool AddImage(string url,string shortName=""){
			if (plugins.Find("ImageLoader")){				
				plugins.Find("ImageLoader").Call("CheckCachedOrCache", url);
				return true;
			}else
			if (plugins.Find("ImageLibrary")){
				if (string.IsNullOrEmpty(shortName)) shortName=url;
				plugins.Find("ImageLibrary").Call("AddImage", url, shortName);
				//Puts($"Add Image {shortName}");
				return true;
			}	
			return false;		
		}   
    }
} 