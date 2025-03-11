using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("Reports", "Frizen", "1.0.0")]
    public class Reports : RustPlugin
    {
        #region Reference

        [PluginReference] private Plugin ImageLibrary, NoEscape;

        #endregion

        #region Vars

        private static Reports _ins;

        public Dictionary<ulong, PlayerSaveCheckClass> PlayerSaveCheck = new Dictionary<ulong, PlayerSaveCheckClass>();
        public class PlayerSaveCheckClass
        {
            public string Discord;
            public string NickName;

            public ulong ModeratorID;

            public DateTime CheckStart = DateTime.Now;
        }

        public Dictionary<ulong, bool> OpenedModeratorMenu = new Dictionary<ulong, bool>();
        public List<ReportList> LastReport = new List<ReportList>();
        public Dictionary<ulong, PlayerInfo> ReportInformation = new Dictionary<ulong, PlayerInfo>();

        #endregion

        #region Config
        private static Configuration _config;
        public class Configuration
        {
            [JsonProperty("[Discord] Вебхук для новых репортов")]
            public string DiscordWebHook { get; set; }
            [JsonProperty("Пермишн модератора")]
            public string ModeratorPermission { get; set; }
            [JsonProperty("Кд отправки репортов")]
            public double Cooldown { get; set; }
            [JsonProperty("Количество репортов для отправки уведомления и отображения в панели")]
            public int AlertCount { get; set; }
            [JsonProperty("Кастомные имена для оружия")]
            public Dictionary<string, string> CustomNames { get; set; }
            [JsonProperty("Причина блокировки - время в днях")]
            public Dictionary<string, double> BanReasons { get; set; }

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    DiscordWebHook = "",
                    ModeratorPermission = "reports.moderator",
                    Cooldown = 200,
                    AlertCount = 3,
                    CustomNames = new Dictionary<string, string>()
                    {
                        ["rifle.ak"] = "AK-47"
                    },
                    BanReasons = new Dictionary<string, double>()
                    {
                        ["Использование макросов"] = 3600,
                        ["Использование читов"] = 0,
                        ["Игнор проверки"] = 0,
                        ["Отказ"] = 0,
                    }
                };
            }
        }

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

        #region Helpers

        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Players", ReportInformation);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/LastReports", LastReport);
        }

        private void LoadData()
        {
            try
            {
                ReportInformation = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerInfo>>($"{Title}/Players");
                LastReport = Interface.Oxide.DataFileSystem.ReadObject<List<ReportList>>($"{Title}/LastReports");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (ReportInformation == null) ReportInformation = new Dictionary<ulong, PlayerInfo>();
            if (LastReport == null) LastReport = new List<ReportList>();
        }

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion

        #region [Info class]

        public class PlayerInfo
        {
            [JsonProperty("Количество проверок")]
            public int ChecksCount = 0;
            [JsonProperty("Количество репортов")]
            public int ReportCount = 0;
            [JsonProperty("История репортов")]
            public List<string> ReportsHistory = new List<string>();
            [JsonProperty("Выбранные к репорту")]
            public List<ulong> ReportsList = new List<ulong>();
            [JsonProperty("Список смертей")]
            public List<ulong> DeathList = new List<ulong>();
            [JsonProperty("Список убийств")]
            public List<KillsInfo> KillsList = new List<KillsInfo>();
            [JsonProperty("Статус игрока")]
            public Status PlayerStatus = Status.None;
            [JsonProperty("Должность игрока")]
            public Permission PlayerPermission = Permission.Player;
            [JsonProperty("Кд репортов")]
            public double Cooldown = 0;


            [JsonProperty("Список тиммейтов")]
            public List<ulong> Teammates = new List<ulong>();

            public enum Permission
            {
                Player,
                Moderator,
                Administrator
            }

            public enum Status
            {
                None,
                OnCheck,
                AfkCheck,
                ModeratorOnCheck
            }

            public PlayerInfo(BasePlayer player)
            {
                if (player != null)
                {
                    if (player.IsAdmin)
                    {
                        PlayerPermission = Permission.Administrator;
                    }
                    else if (_ins.permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission))
                    {
                        PlayerPermission = Permission.Moderator;
                    }
                }
            }

        }

        public class ReportList
        {
            [JsonProperty("Время репорта")]
            public DateTime Time;
            [JsonProperty("Стим айди зарепорченного")]
            public ulong SteamId;
        }

        public class KillsInfo
        {
            [JsonProperty("Ник убитого")]
            public string KilledName { get; set; }

            [JsonProperty("Айди убитого")]
            public ulong KilledId { get; set; }

            [JsonProperty("Оружие")]
            public string WeaponName { get; set; }

            [JsonProperty("Хитбокс")]
            public string HitBox { get; set; }

            [JsonProperty("Дистанция")]
            public int Distance { get; set; }
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _ins = this;
            if (!permission.PermissionExists(_config.ModeratorPermission, this))
            {
                permission.RegisterPermission(_config.ModeratorPermission, this);
            }
            LoadData();

            Dictionary<string, string> images = new Dictionary<string, string>
            {
                { "Gradient", "https://cdn.discordapp.com/attachments/935163486340804698/1107682180311814174/Rectangle_191.png" },
                { "DiscordNull", "https://cdn.discordapp.com/attachments/1009170439089684500/1187478129812459570/Rectangle_196.png?ex=65970830&is=65849330&hm=3666858b26a7e54896db04ece13bd59a15bfcbdf6f020ac8816f2b32a6929a6d&" },
                { "GradientCheck", "https://cdn.discordapp.com/attachments/1120708904242925689/1182293789197475910/Group_525111267.png?ex=6596a0e5&is=65842be5&hm=e0439709a08439bccaa27a67e2a5c24ef376dc6643ed7398d03095011905aad9&" },
                { "Viniette", "https://i.imgur.com/v4Ix05O.png" },
                { "RemoveBTN", "https://cdn.discordapp.com/attachments/935163486340804698/1107702418273226752/2.png" },
                { "Coursive", "https://cdn.discordapp.com/attachments/935163486340804698/1107689080592007188/Rectangle_196.png" }
            };

            foreach (var image in images)
            {
                ImageLibrary.Call("AddImage", image.Value, image.Key);
            }

            foreach (var item in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(item);
            }
        }

        private void OnUserPermissionGranted(string id, string permName)
        {
            var player = BasePlayer.activePlayerList.FirstOrDefault(x => x.UserIDString == id);

            if (player != null && ReportInformation.ContainsKey(player.userID) && permName == _config.ModeratorPermission)
            {
                ReportInformation[player.userID].PlayerPermission = PlayerInfo.Permission.Moderator;
            }
        }

        private void OnUserPermissionRevoked(string id, string permName)
        {
            var player = BasePlayer.activePlayerList.FirstOrDefault(x => x.UserIDString == id);

            if (player != null && ReportInformation.ContainsKey(player.userID) && permName == _config.ModeratorPermission)
            {
                ReportInformation[player.userID].PlayerPermission = PlayerInfo.Permission.Moderator;
            }
        }

        private void Unload()
        {
            foreach (var item in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(item, "CheckAlert");
            }

            foreach (var item in ReportInformation)
            {
                item.Value.PlayerStatus = PlayerInfo.Status.None;
            }

            PlayerSaveCheck = null;

            SaveData();
            _ins = null;
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            if (!ReportInformation.ContainsKey(player.userID)) ReportInformation.Add(player.userID, new PlayerInfo(player));

            if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
            {
                if (!OpenedModeratorMenu.ContainsKey(player.userID))
                {
                    OpenedModeratorMenu.Add(player.userID, false);
                }
            }
            if (PlayerSaveCheck.ContainsKey(player.userID))
            {
                SendReply(BasePlayer.FindByID(PlayerSaveCheck[player.userID].ModeratorID), $"Игрок - {player.displayName}/{player.userID} вернулся сервер во время проверки");
                DiscordSendMessage($"Игрок - {player.displayName}/{player.userID} вернулся на сервер во время проверки");

            }
        }
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
            {
                if (OpenedModeratorMenu.ContainsKey(player.userID))
                {
                    OpenedModeratorMenu[player.userID] = false;
                }
            }
            if (PlayerSaveCheck.ContainsKey(player.userID))
            {
                SendReply(BasePlayer.FindByID(PlayerSaveCheck[player.userID].ModeratorID), $"Игрок - {player.displayName} покинул сервер во время проверки: {reason}");
                DiscordSendMessage($"Игрок - {player.displayName} покинул сервер во время проверки: {reason}");

            }
            ReportInformation[player.userID].PlayerStatus = PlayerInfo.Status.None;
        }


        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return null;
            var attacker = info.InitiatorPlayer;
            if (attacker.userID == player.userID) return null;
            if (attacker != null)
            {
                KillsInfo killinfo = new KillsInfo
                {
                    Distance = (int)Vector3.Distance(player.transform.position, attacker.transform.position),
                    HitBox = info.boneName,
                    KilledId = player.userID,
                    KilledName = player.displayName,
                    WeaponName = _config.CustomNames.ContainsKey(info.Weapon.GetItem().info.shortname) ? _config.CustomNames[info.Weapon.GetItem().info.shortname] : info.Weapon.GetItem().info.displayName.english
                };
                if (ReportInformation.ContainsKey(player.userID))
                {
                    ReportInformation[player.userID].DeathList.Insert(0, attacker.userID);
                }
                if (ReportInformation.ContainsKey(attacker.userID))
                {
                    ReportInformation[attacker.userID].KillsList.Insert(0, killinfo);
                }
            }
            return null;
        }


        #endregion

        #region Methods

        void DiscordSendMessage(string key, ulong userID = 0, params object[] args)
        {
            if (String.IsNullOrEmpty(_config.DiscordWebHook)) return;

            List<Fields> fields = new List<Fields>
                {
                    new Fields("ReportSystem", key, true),
                };

            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 635133, fields, new Authors("ReportSystem", "https://vk.com/starkow1337", "https://i.imgur.com/ILk3uJc.png", null), new Footer("Author: Frizen[https://vk.com/starkow1337]", "https://i.imgur.com/ILk3uJc.png", null)) });
            Request($"{_config.DiscordWebHook}", newMessage.toJSON());
        }

        #region FancyDiscord
        public class FancyMessage
        {
            public string content { get; set; }
            public bool tts { get; set; }
            public Embeds[] embeds { get; set; }

            public class Embeds
            {
                public string title { get; set; }
                public int color { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Authors author { get; set; }

                public Embeds(string title, int color, List<Fields> fields, Authors author, Footer footer)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                    this.author = author;
                    this.footer = footer;

                }
            }

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON() => JsonConvert.SerializeObject(this);
        }

        public class Footer
        {
            public string text { get; set; }
            public string icon_url { get; set; }
            public string proxy_icon_url { get; set; }
            public Footer(string text, string icon_url, string proxy_icon_url)
            {
                this.text = text;
                this.icon_url = icon_url;
                this.proxy_icon_url = proxy_icon_url;
            }
        }

        public class Authors
        {
            public string name { get; set; }
            public string url { get; set; }
            public string icon_url { get; set; }
            public string proxy_icon_url { get; set; }
            public Authors(string name, string url, string icon_url, string proxy_icon_url)
            {
                this.name = name;
                this.url = url;
                this.icon_url = icon_url;
                this.proxy_icon_url = proxy_icon_url;
            }
        }

        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }

        private void Request(string url, string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            webrequest.Enqueue(url, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning($" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning($"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }
                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex) { }

            }, this, RequestMethod.POST, header);
        }
        #endregion

        private void ReportPlayer(BasePlayer target, string reason)
        {
            ReportInformation[target.userID].ReportCount++;
            ReportInformation[target.userID].ReportsHistory.Insert(0,reason);
            LastReport.Insert(0, new ReportList()
            {
                SteamId = target.userID,
                Time = DateTime.Now
            });

            if (ReportInformation[target.userID].ReportCount >= _config.AlertCount)
            {
                DiscordSendMessage($"Игрок - {target.displayName}/{target.userID} достиг максимальное количество репортов\nСвободный модератор вызовите на проверку!");

                foreach (var item in BasePlayer.activePlayerList.Where(x => ReportInformation[x.userID].PlayerPermission != PlayerInfo.Permission.Player))
                {
                    SendReply(item, $"Игрок - {target.displayName}/{target.userID} достиг максимальное количество репортов\nСвободный модератор вызовите на проверку!");
                }
            }
        }

        public string TimeToString(double time)
        {
            TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
            string s = "";
            if (days > 0) s += $"{days} дн.";
            if (hours > 0) s += $"{hours} ч. ";
            if (minutes > 0) s += $"{minutes} м. ";
            if (seconds > 0) s += $"{seconds} с.";
            else s = s.TrimEnd(' ');
            return s;
        }

        public bool IsRaidBlocked(BasePlayer player)
        {
            if (NoEscape)
                return (bool)NoEscape?.Call("IsRaidBlocked", player);
            else return false;
        }

        private void SoundToast(BasePlayer player, string text, int type)
        {
            Effect effect = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(effect, player.Connection);

            player.Command("gametip.showtoast", type, text);
        }

        private double CurrentTime() => DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        #endregion

        #region Commands

        [ChatCommand("report")]
        private void ChatReport(BasePlayer player)
        {
            if (player == null) return;

            if(PlayerSaveCheck != null)
            {
                foreach (var kvp in PlayerSaveCheck)
                {
                    if (kvp.Value.ModeratorID == player.userID)
                    {

                        ModeratorCheckInfo(player, kvp.Key);
                        return;
                    }
                }
            }

            ReportsUI(player);
        }


        [ConsoleCommand("reports")]
        private void HandleConsoleCommand(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;

            string command = args.Args[0];

            switch (command)
            {
                case "back":

                    ReportsBack(player);
                    break;

                case "stopcheck":

                    if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
                    {
                        if (args.Args.Length > 1)
                        {
                            StopCheck(player, Convert.ToUInt64(args.Args[1]));
                        }
                    }


                    break;

                case "trychecklast":

                    if (args.Args.Length > 1)
                    {
                        string target = args.Args[1];
                        TryCheckLast(player, target);
                    }
                    break; 
                case "trycheck":

                    if (args.Args.Length > 2)
                    {
                        string target = args.Args[1];
                        int index = Convert.ToInt32(args.Args[2]);

                        TryCheck(player, target, index);
                    }
                    break;

                case "add":
                    if (args.Args.Length > 1)
                    {
                        AddTargetHandler(player, Convert.ToUInt64(args.Args[1]));
                    }
                    break;

                case "remove":
                    if (args.Args.Length > 1)
                    {
                        RemoveTargetHandler(player, Convert.ToUInt64(args.Args[1]));
                    }
                    break;
                case "closemenu":

                    if (OpenedModeratorMenu.ContainsKey(player.userID))
                    {
                        OpenedModeratorMenu[player.userID] = false;
                    }

                    break;

                case "sendcheck":

                    if (args.Args.Length > 1)
                    {
                        SendCheck(player, Convert.ToUInt64(args.Args[1]));
                    }

                    break;

                case "cancelcheck":

                    LoadedPlayersModerator(player);

                    break;                
                case "verdict":

                    if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
                    {
                        if (args.Args.Length > 2)
                        {
                            StopCheckVerdict(player, Convert.ToUInt64(args.Args[1]), Convert.ToInt32(args.Args[2]));
                        }
                    }


                    break;
                case "banreasons":

                    if(args.Args.Length > 1)
                    {
                        DrawBanReasons(player, Convert.ToUInt64(args.Args[1]));
                    }

                    break;

                case "moderation":
                    OpenModerationMenu(player);
                    break;

                case "send":
                    SendReportHandler(player, args.Args[1]);
                    break;

                case "search":
                    if (args.Args.Length > 1)
                    {
                        string name = args.Args[1];
                        LoadedPlayers(player, name);
                    }
                    break;

                default:
                    break;
            }
        }

        [ChatCommand("discord")]
        private void SendDiscord(BasePlayer Suspect, string command, string[] args)
        {
            if (!PlayerSaveCheck.ContainsKey(Suspect.userID))
            {
                return;
            }
            string Discord = "";
            foreach (var arg in args)
                Discord += " " + arg;

            PlayerSaveCheck[Suspect.userID].Discord = Discord;

            BasePlayer Moderator = BasePlayer.FindByID(PlayerSaveCheck[Suspect.userID].ModeratorID);


            if (Discord != "")
            {
                var container = new CuiElementContainer();
                CuiHelper.DestroyUi(Moderator, "DiscordImage");
                CuiHelper.DestroyUi(Moderator, "DiscordText");

                container.Add(new CuiElement
                {
                    Name = "DiscordText",
                    Parent = "DiscordPanel",
                    Components = {
                            new CuiTextComponent { Text = $"{PlayerSaveCheck[Suspect.userID].Discord}", Font = "robotocondensed-bold.ttf", FontSize = 7, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                });

                DiscordSendMessage($"Игрок {Suspect.displayName}/{Suspect.userID} предоставил дискорд для проверки: {Discord}");

                CuiHelper.AddUi(Moderator, container);
            }
            
        }

        private void DrawBanReasons(BasePlayer moderator, ulong target)
        {
            var container = new CuiElementContainer();

            double offsetminy = -112.788, offsetmaxy = -93.406, margin = 136.791 - 112.788;
            int i = 0;
            foreach (var item in _config.BanReasons)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = $"{HexToRustFormat("#B6B6B63E")}", Command = $"reports verdict {target} {i}" },
                    Text = { Text = $"{item.Key}", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"37.035 {offsetminy}", OffsetMax = $"166.885 {offsetmaxy}" }
                }, "CheckUI", "ReasonBTN");

                offsetminy -= margin;
                offsetmaxy -= margin;
                i++;
            }

            CuiHelper.AddUi(moderator, container);
        }

        private void StopCheck(BasePlayer moderator, ulong target)
        {
            ReportInformation[target].ChecksCount++;
            ReportInformation[target].PlayerStatus = PlayerInfo.Status.None;

            PlayerSaveCheck.Remove(target);

            var targetPlayer = BasePlayer.FindByID(target) ?? BasePlayer.FindAwakeOrSleeping(target.ToString());

            CuiHelper.DestroyUi(targetPlayer, "CheckAlert");

            ReportInformation[moderator.userID].PlayerStatus = PlayerInfo.Status.None;

            ReportInformation[target].ReportCount = 0;

            ReportInformation[target].ReportsHistory.Clear();

            LastReport.Remove(LastReport.FirstOrDefault(x => x.SteamId == target));

            ReportsUI(moderator);

            DiscordSendMessage($"Модератор - {moderator.displayName}/{moderator.userID} завершил проверку игрока - {targetPlayer.displayName}/{target}\nВердикт:Чист");
        }


        private void StopCheckVerdict(BasePlayer moderator, ulong target, int index)
        {
            ReportInformation[target].ChecksCount++;
            ReportInformation[target].PlayerStatus = PlayerInfo.Status.None;

            PlayerSaveCheck.Remove(target);

            var targetPlayer = BasePlayer.FindByID(target) ?? BasePlayer.FindAwakeOrSleeping(target.ToString());

            CuiHelper.DestroyUi(targetPlayer, "CheckAlert");

            ReportInformation[moderator.userID].PlayerStatus = PlayerInfo.Status.None;

            ReportInformation[target].ReportCount = 0;

            ReportInformation[target].ReportsHistory.Clear();

            rust.RunClientCommand(moderator, $"ban {target} {_config.BanReasons.ElementAt(index).Value}d {_config.BanReasons.ElementAt(index).Key}");

            LastReport.Remove(LastReport.FirstOrDefault(x => x.SteamId == target));

            ReportsUI(moderator);

            DiscordSendMessage($"Модератор - {moderator.displayName}/{moderator.userID} завершил проверку игрока - {targetPlayer.displayName}/{target}\nВердикт:{_config.BanReasons.ElementAt(index).Key}");
        }

        private void SendCheck(BasePlayer moderator, ulong target)
        {
            if (permission.UserHasPermission(moderator.UserIDString, _config.ModeratorPermission) || moderator.IsAdmin)
            {
                if (ReportInformation[moderator.userID].PlayerStatus != PlayerInfo.Status.None)
                {
                    SoundToast(moderator, "У вас уже имеется активная проверка", 1);
                    LoadedPlayersModerator(moderator);
                    return;
                }

                if (ReportInformation[target].PlayerStatus != PlayerInfo.Status.None)
                {
                    SoundToast(moderator, "Игрок уже проверяется", 1);
                    LoadedPlayersModerator(moderator);
                    return;
                }

                if (IsRaidBlocked(BasePlayer.FindByID(target)))
                {
                    SoundToast(moderator, "Игрок находится в рейдблоке", 1);
                    LoadedPlayersModerator(moderator);
                    return;
                }

                ReportInformation[target].PlayerStatus = PlayerInfo.Status.AfkCheck;
                LoadedPlayersModerator(moderator);

                Metods_AFK(moderator, target);

            }

        }

        public Dictionary<ulong, GenericPosition> AFKPositionTry = new Dictionary<ulong, GenericPosition>();
        public Dictionary<ulong, int> AFKCheckedTry = new Dictionary<ulong, int>();

        void Metods_AFK(BasePlayer Moderator, ulong SuspectID)
        {
            IPlayer Suspect = covalence.Players.FindPlayerById(SuspectID.ToString());
            if (!AFKCheckedTry.ContainsKey(SuspectID))
                AFKCheckedTry.Add(SuspectID, 0);
            else AFKCheckedTry[SuspectID] = 0;

            StartAFKCheck(Moderator, Suspect);
        }

        public void StartAFKCheck(BasePlayer Moderator, IPlayer Suspect)
        {
            SoundToast(Moderator, "Проверка на афк - началась", 0);

            ulong SuspectID = ulong.Parse(Suspect.Id);
            var Position = Suspect.Position();

            if (!AFKPositionTry.ContainsKey(SuspectID))
                AFKPositionTry.Add(SuspectID, Position);
            



            int Try = 1;
            double seconds = 0;
            timer.Repeat(5f, 5, () =>
            {
                Position = Suspect.Position();
                if (AFKPositionTry[SuspectID] != Position)
                {
                    AFKCheckedTry[SuspectID]++;
                }

                AFKPositionTry[SuspectID] = Position;
                Try++;
            });
            timer.Repeat(1f, 30, () => 
            {
                if (OpenedModeratorMenu[Moderator.userID])
                {
                    DrawAfkCheck(Moderator, seconds);
                }
                seconds++;
            });
            timer.Once(30f, () =>
            {
                if (AFKCheckedTry[SuspectID] < 3)
                {
                    SoundToast(Moderator, "Игрок - афк, проверка отменяется", 1);
                    ReportInformation[SuspectID].PlayerStatus = PlayerInfo.Status.None;
                    LoadedPlayersModerator(Moderator);
                    CuiHelper.DestroyUi(Moderator, "AfkText");
                    CuiHelper.DestroyUi(Moderator, "AfkLine");
                    CuiHelper.DestroyUi(Moderator, "AfkPanel");
                }
                else
                {
                    BasePlayer SuspectOnline = BasePlayer.FindByID(ulong.Parse(Suspect.Id));

                    ReportInformation[SuspectID].PlayerStatus = PlayerInfo.Status.OnCheck;
                    ReportInformation[Moderator.userID].PlayerStatus = PlayerInfo.Status.None;


                    if (SuspectOnline == null || !SuspectOnline.IsConnected)
                    {
                        SoundToast(Moderator, $"Игрок - {Suspect.Name} покинул сервер во время скрытой проверки на АФК!\nПроверка была снята автоматически", 1);
                        DiscordSendMessage($"Игрок - {Suspect.Name} покинул сервер во время скрытой проверки на АФК!\nПроверка была снята автоматически", 1);
                        if (PlayerSaveCheck.ContainsKey(SuspectID))
                            PlayerSaveCheck.Remove(SuspectID);
                        return;
                    }

                    if (!PlayerSaveCheck.ContainsKey(SuspectOnline.userID))
                    {
                        PlayerSaveCheck.Add(SuspectID, new PlayerSaveCheckClass
                        {
                            Discord = string.Empty,
                            NickName = Suspect.Name,

                            ModeratorID = Moderator.userID,
                        });
                    }
                    else
                    {
                        PlayerSaveCheck.Remove(SuspectOnline.userID);

                        PlayerSaveCheck.Add(SuspectID, new PlayerSaveCheckClass
                        {
                            Discord = string.Empty,
                            NickName = Suspect.Name,

                            ModeratorID = Moderator.userID,
                        });
                    }

                    CuiHelper.DestroyUi(Moderator, "AfkText");
                    CuiHelper.DestroyUi(Moderator, "AfkLine");
                    CuiHelper.DestroyUi(Moderator, "AfkPanel");

                    ModeratorCheckInfo(Moderator, SuspectID);
                    UI_AlertSendPlayer(SuspectOnline);
                    DiscordSendMessage($"Модератор {Moderator.displayName}/{Moderator.userID} вызвал на проверку игрока - {SuspectOnline.displayName}/{SuspectOnline.userID}");
                }  
                if (AFKCheckedTry.ContainsKey(SuspectID))
                    AFKCheckedTry.Remove(SuspectID);
            });
        }

        void UI_AlertSendPlayer(BasePlayer Suspect)
        {
            var PARENT_UI_ALERT_SEND = "CheckAlert";
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(Suspect, PARENT_UI_ALERT_SEND);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.575", AnchorMax = "1 0.8888889" },
                Image = { FadeIn = 0.15f, Color = HexToRustFormat("#21211AF2"), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Overall", PARENT_UI_ALERT_SEND);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.365625 0.2595869", AnchorMax = "0.6463541 0.2772861" },
                Image = { Color = HexToRustFormat("#B4371EFF"), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, PARENT_UI_ALERT_SEND);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.7079645", AnchorMax = "1 1" },
                Text = { Text = "<size=40><b>ВАС ВЫЗВАЛИ НА ПРОВЕРКУ</b></size>", Color = HexToRustFormat("#B4371EFF"), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter }
            }, PARENT_UI_ALERT_SEND);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.2949852", AnchorMax = "1 0.761062" },
                Text = { Text = "<size=18>Вы превысили максимально-допустимое количество жалоб.\nПоэтому,предоставьте ваш Discord, для того чтобы с вами связалась наша модерация!\nВ случае игнорирования данного сообщения - вы получите блокировку! (У вас имеется 2 минуты)</size>", Color = HexToRustFormat("#DAD1C7FF"), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter }
            }, PARENT_UI_ALERT_SEND);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.2300885" },
                Text = { Text = "<size=15>Чтобы предоставить данные для связи,используйте команды:\n/discord\nДалее с вами свяжется модератор</size>", Color = HexToRustFormat("#DAD1C7FF"), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter }
            }, PARENT_UI_ALERT_SEND);

            CuiHelper.AddUi(Suspect, container);
        }

        private void DrawAfkCheck(BasePlayer moderator, double seconds)
        {
            var container = new CuiElementContainer();
            CuiHelper.DestroyUi(moderator, "AfkText");
            CuiHelper.DestroyUi(moderator, "AfkLine");
            CuiHelper.DestroyUi(moderator, "AfkPanel");
            container.Add(new CuiElement
            {
                Parent = "ModerUI",
                Name = "AfkPanel",
                Components =
                {
                    new CuiImageComponent{Color = HexToRustFormat("#B6B6B63E")},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-166.491 110.611", OffsetMax = "166.489 135.209" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = HexToRustFormat("#C8D38259") },
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"{ 1 - (seconds / 30)} 1"}
            }, "AfkPanel", "AfkLine");


            container.Add(new CuiElement
            {
                Parent = "AfkPanel",
                Name = "AfkText",
                Components =
                {
                    new CuiTextComponent{Color = $"{HexToRustFormat("#CCD68AFF")}", Text = $"Проверка на АФК, осталось {30 - seconds}с", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            CuiHelper.AddUi(moderator, container);
        }

        private void ModeratorCheckInfo(BasePlayer moderator, ulong target)
        {
            CuiHelper.DestroyUi(moderator, "ModerBG");

            var container = new CuiElementContainer();

            var targetPlayer = BasePlayer.FindByID(target) ?? BasePlayer.FindAwakeOrSleeping(target.ToString());

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Overlay", "CheckBG");

            container.Add(new CuiElement
            {
                Parent = "CheckBG",
                Name = "BG",
                Components =
                {
                    new CuiRawImageComponent{Color = "1 1 1 1", Png = ImageLibrary.Call<string>("GetImage", "Viniette")},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                    {
                    AnchorMin = "0 0",
                    AnchorMax= "1 1"
                    },
                Text =
                    {
                    Text = "",
                    },
                Button =
                    {
                    Color = "0 0 0 0",
                    Command = "reports closemenu",
                    Close = "CheckBG",
                    }
            }, "BG");

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-166.431 -206.357", OffsetMax = "167.329 164.58" }
            }, "BG", "CheckUI");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = HexToRustFormat("#84CBFF59") },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-166.49 160.784", OffsetMax = "166.88 185.47" }
            }, "CheckUI", "PlayerMoved");

            container.Add(new CuiElement
            {
                Name = "PlayerMovedText",
                Parent = "PlayerMoved",
                Components = {
                    new CuiTextComponent { Text = "Игрок двигался, удачной проверки!", Font = "robotocondensed-bold.ttf", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#80C9FF") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-166.49 142.299", OffsetMax = "33.246 156.897" }
            }, "CheckUI", "SuspectTitlePanel");

            container.Add(new CuiElement
            {
                Name = "SuspectText",
                Parent = "SuspectTitlePanel",
                Components = {
                    new CuiTextComponent { Text = "Подозреваемый", Font = "robotocondensed-bold.ttf", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.65" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-99.871 -7.299", OffsetMax = "99.869 7.298" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "36.92 142.299", OffsetMax = "166.88 156.897" }
            }, "CheckUI", "LastKillsPanelText");

            container.Add(new CuiElement
            {
                Name = "LastKillsText",
                Parent = "LastKillsPanelText",
                Components = {
                    new CuiTextComponent { Text = "Последнии убийства", Font = "robotocondensed-bold.ttf", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.65" },

                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.782 -7.299", OffsetMax = "64.978 7.298" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-166.88 108.427", OffsetMax = "32.621 138.125" }
            }, "CheckUI", "SuspectPanel");

            container.Add(new CuiElement
            {
                Name = "Avatar",
                Parent = "SuspectPanel",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(target.ToString(), 0) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-96.681 -11.779", OffsetMax = "-73.361 11.779" }
                }
            });

            var name = targetPlayer.displayName.Length > 20 ? targetPlayer.displayName.Substring(0, 20) + "..." : targetPlayer.displayName;

            container.Add(new CuiElement
            {
                Name = "Name",
                Parent = "SuspectPanel",
                Components = {
                    new CuiTextComponent { Text = $"{name}", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = $"{HexToRustFormat("#84CBFF")}" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-70.425 -3.251", OffsetMax = "14.225 13.051" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "SteamID",
                Parent = "SuspectPanel",
                Components = {
                    new CuiTextComponent { Text = $"{target}", Font = "robotocondensed-bold.ttf", FontSize = 7, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.9" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-70.425 -11.224", OffsetMax = "14.225 -2.176" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "CheckTime",
                Parent = "SuspectPanel",
                Components = {
                    new CuiTextComponent { Text = $"{TimeToString(DateTime.Now.Subtract(PlayerSaveCheck[target].CheckStart).TotalSeconds)}", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },

                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "73.758 -11.224", OffsetMax = "95.042 8.765" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-166.88 0.969", OffsetMax = "32.62 102.64" }
            }, "CheckUI", "TeamPanel");


            var Team = targetPlayer.Team == null ? ReportInformation[target].Teammates : targetPlayer.Team.members;

            double offsetminy2 = 1.185, offsetmaxy2 = 25.875, margin2 = 1.185 + 26.145;
            foreach (var j in Enumerable.Range(0, 3))
            {
                var item = Team.Where(x => x != target).ElementAtOrDefault(j);

                if (item != 0)
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-97.3 {offsetminy2}", OffsetMax = $"97.282 {offsetmaxy2}" }
                    }, "TeamPanel", "TeamMate");

                    var mate = covalence.Players.FindPlayerById(item.ToString());

                    container.Add(new CuiElement
                    {
                        Name = "Avatar",
                        Parent = "TeamMate",
                        Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.ToString(), 0) },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-96.2 -11.5", OffsetMax = "-73.2 11.5" }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Name = "Name",
                        Parent = "TeamMate",
                        Components = {
                            new CuiTextComponent { Text = $"{mate.Name}", Font = "robotocondensed-bold.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.9" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-71.125 -3.957", OffsetMax = "27.976 12.345" }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Name = "SteamID",
                        Parent = "TeamMate",
                        Components = {
                            new CuiTextComponent { Text = $"{item}", Font = "robotocondensed-bold.ttf", FontSize = 6, Align = TextAnchor.MiddleLeft, Color = $"{HexToRustFormat("#E8E2D9E5")}" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-71.125 -10.924", OffsetMax = "13.525 -1.876" }
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "79.026 -7.834", OffsetMax = "94.026 7.166"
                        },
                        Text =
                        {
                            Text = "",
                        },
                        Button =
                        {
                            Color = $"{HexToRustFormat("#85CCFF5A")}",
                            Command = ""
                        }
                    }, "TeamMate", "BTN");

                    container.Add(new CuiPanel
                    {
                        Image = { Color = $"{HexToRustFormat("#80C9FF")}", Sprite = "assets/icons/facepunch.png" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" }
                    }, "BTN");

                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-97.3 {offsetminy2}", OffsetMax = $"97.282 {offsetmaxy2}" }
                    }, "TeamPanel", "TeamMate");

                    container.Add(new CuiElement
                    {
                        Name = "NullMate",
                        Parent = "TeamMate",
                        Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("DiscordNull", 0) },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Name = "Text",
                        Parent = "TeamMate",
                        Components = {
                            new CuiTextComponent { Text = $"Тимейт отсутствует", Font = "robotocondensed-bold.ttf", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = $"1 1 1 0.65" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });
                }

                offsetminy2 -= margin2;
                offsetmaxy2 -= margin2;
            }

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "36.92 -38.69", OffsetMax = "166.88 138.125" }
            }, "CheckUI", "KillsPanel");

            double offsetminy = 60.52, offsetmaxy = 83.715, margin = 60.52 - 31.933;

            foreach (var i in Enumerable.Range(0, 6))
            {
                var item = ReportInformation[target].KillsList.ElementAtOrDefault(i);

                if (item != null)
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-64.98 {offsetminy}", OffsetMax = $"64.98 {offsetmaxy}" }
                    }, "KillsPanel", "Kill");

                    container.Add(new CuiElement
                    {
                        Name = "Avatar",
                        Parent = "Kill",
                        Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.KilledId.ToString(), 0) },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-61.548 -11.5", OffsetMax = "-38.548 11.5" }
                        }
                    });

                    var killedname = item.KilledName.Length > 15 ? item.KilledName.Substring(0, 15) + "..." : item.KilledName;

                    container.Add(new CuiElement
                    {
                        Name = "KilledName",
                        Parent = "Kill",
                        Components = {
                            new CuiTextComponent { Text = $"{killedname}", Font = "robotocondensed-bold.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },

                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-34.958 -2.286", OffsetMax = "43.712 11.598" }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Name = "KillDescription",
                        Parent = "Kill",
                        Components = {
                            new CuiTextComponent { Text = $"C <color=#84CBFF>{item.WeaponName}</color> в <color=#FF8080>{item.HitBox}</color>", Font = "robotocondensed-bold.ttf", FontSize = 7, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },

                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-34.958 -11.597", OffsetMax = "43.712 0" }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Name = "KillDistance",
                        Parent = "Kill",
                        Components = {
                            new CuiTextComponent { Text = $"{item.Distance}м", Font = "robotocondensed-bold.ttf", FontSize = 7, Align = TextAnchor.MiddleRight, Color = "1 1 1 0.6" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "43.712 -7", OffsetMax = "58.991 8" }
                        }
                    });

                    offsetminy -= margin;
                    offsetmaxy -= margin;
                }

            }

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "36.92 -63.794", OffsetMax = "166.88 -43.786" }
            }, "CheckUI", "DiscordPanel");

            if (PlayerSaveCheck[target].Discord == "")
            {
                container.Add(new CuiElement
                {
                    Name = "DiscordImage",
                    Parent = "DiscordPanel",
                    Components = {
                         new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("DiscordNull", 0) },
                         new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "DiscordText",
                    Parent = "DiscordPanel",
                    Components = {
                            new CuiTextComponent { Text = $"Дискорд не предоставлен!", Font = "robotocondensed-bold.ttf", FontSize = 7, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                });
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "DiscordText",
                    Parent = "DiscordPanel",
                    Components = {
                            new CuiTextComponent { Text = $"{PlayerSaveCheck[target].Discord}", Font = "robotocondensed-bold.ttf", FontSize = 7, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                });
            }

            container.Add(new CuiButton
            {
                Button = { Color = $"{HexToRustFormat("#B6B6B63E")}", Command = $"reports stopcheck {target}" },
                Text = { Text = "Стоп", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "37.034 -89.251", OffsetMax = "100.348 -69.361" }
            }, "CheckUI", "StopBTN");

            container.Add(new CuiButton
            {
                Button = { Color = $"{HexToRustFormat("#B6B6B63E")}", Command = $"reports banreasons {target}" },
                Text = { Text = "Бан", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "103.452 -89.251", OffsetMax = "166.88 -69.361" }
            }, "CheckUI", "BanBtn");

            CuiHelper.AddUi(moderator, container);
        }

        private void TryCheck(BasePlayer moderator, string target, int index)
        {
            if (permission.UserHasPermission(moderator.UserIDString, _config.ModeratorPermission) || moderator.IsAdmin)
            {

                CuiHelper.DestroyUi(moderator, $"CheckPanel{index}");

                var container = new CuiElementContainer();

                var offsets = CalculateOffsets(index);

                float offsetMinY = offsets.Item1;
                float offsetMaxY = offsets.Item2;

                container.Add(new CuiButton
                {
                    RectTransform =
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-166.49 {offsetMinY}", OffsetMax = $"32.991 {offsetMaxY}"
                            },
                    Button =
                            {
                                Color = index % 2 == 0 ? $"{HexToRustFormat("#B6B6B62E")}" : $"{HexToRustFormat("#B6B6B640")}",
                            },
                    Text =
                            {
                                Text = ""
                            }
                }, "ModerUI", $"CheckPanel{index}");

                container.Add(new CuiElement
                {
                    Parent = $"CheckPanel{index}",
                    Name = "Gradient",
                    Components =
                    {
                        new CuiRawImageComponent{ Color = "1 1 1 1", Png = GetImage("GradientCheck")},
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 -0.2" }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"reports sendcheck {target}" },
                    Text = { Text = "    Вызвать", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.8" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-99.74 -15.651", OffsetMax = "0 15.65" }
                }, "Gradient", "Check");

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "reports cancelcheck" },
                    Text = { Text = "Закрыть     ", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = "1 1 1 0.8" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0.001 -15.65", OffsetMax = "99.741 15.651" }
                }, "Gradient", "Close");

                CuiHelper.AddUi(moderator, container);
            }


        }

        private void TryCheckLast(BasePlayer moderator, string target)
        {
            if (permission.UserHasPermission(moderator.UserIDString, _config.ModeratorPermission) || moderator.IsAdmin)
            {

                var container = new CuiElementContainer();

                CuiHelper.DestroyUi(moderator, $"PanelParent{target}");
                container.Add(new CuiPanel
                {
                    Image = { Color = $"0 0 0 0" },
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" }
                }, $"CallBTN{target}", $"PanelParent{target}");

                container.Add(new CuiButton
                {
                    RectTransform =
                                        {
                                            AnchorMin = $"0 0",
                                            AnchorMax = $"1 0",
                                            OffsetMin = "0 15",
                                            OffsetMax = "-2 26"
                                        },
                    Button =
                                        {
                                            Color = $"{HexToRustFormat("#1F1F1FFF")}",
                                            Command = $"reports sendcheck {target}",
                                            Close = $"PanelParent{target}"
                                        },
                    Text =
                            {
                                Text = "Вызвать?",
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 0.7",
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 5
                            }
                }, $"PanelParent{target}", $"CallPanel");

                container.Add(new CuiButton
                {
                    RectTransform =
                                        {
                                            AnchorMin = $"0 0",
                                            AnchorMax = $"1 0",
                                            OffsetMin = "0 0",
                                            OffsetMax = "-2 10"
                                        },
                    Button =
                                        {
                                            Color = $"{HexToRustFormat("#1F1F1FFF")}",
                                            Close = $"PanelParent{target}",
                                        },
                    Text =
                            {
                                Text = "Отмена",
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 0.7",
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 5
                            }
                }, $"PanelParent{target}", $"CancelPanel");

                CuiHelper.AddUi(moderator, container);
            }

        }

        private void ReportsBack(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ModerBG");

            OpenedModeratorMenu[player.userID] = false;

            ReportsUI(player);
        }

        private void AddTargetHandler(BasePlayer player, ulong id)
        {
            if (ReportInformation[player.userID].ReportsList.Count >= 4) return;
            if (!ReportInformation[player.userID].ReportsList.Contains(id))
            {
                ReportInformation[player.userID].ReportsList.Insert(0, id);
            }
            LoadedPlayers(player);
        }

        private void RemoveTargetHandler(BasePlayer player, ulong id)
        {
            if (ReportInformation[player.userID].ReportsList.Contains(id))
            {
                ReportInformation[player.userID].ReportsList.Remove(id);
            }
            LoadedPlayers(player);
        }

        private void OpenModerationMenu(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
            {
                OpenedModeratorMenu[player.userID] = true;

                CuiHelper.DestroyUi(player, "MainLayer3");
                ModerUI(player);
            }
        }

        private void SendReportHandler(BasePlayer player, string reason)
        {

            if (ReportInformation[player.userID].Cooldown > CurrentTime())
            {
                var msg = $"Перед отправкой следующего репорта необходимо подождать {TimeSpan.FromSeconds(ReportInformation[player.userID].Cooldown - CurrentTime()).TotalSeconds}с";
                SoundToast(player, msg, 1);
                return;
            }

            foreach (var item in ReportInformation[player.userID].ReportsList)
            {
                BasePlayer target = BasePlayer.Find(item.ToString());
                ReportPlayer(target, reason);
            }
            ReportInformation[player.userID].ReportsList.Clear();
            ReportInformation[player.userID].Cooldown = CurrentTime() + _config.Cooldown;
            var container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "Text");
            CuiHelper.DestroyUi(player, "Message");
            container.Add(new CuiElement
            {
                Parent = "Parent",
                Name = "Message",
                FadeOut = 0.2f,
                Components =
                {
                    new CuiImageComponent{Color = HexToRustFormat("#C8D38259"), FadeIn = 0.5f},
                    new CuiRectTransformComponent { AnchorMin = "-2.235174E-08 1.019803", AnchorMax = "1 1.111387" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "Message",
                Name = "Text",
                FadeOut = 0.2f,
                Components =
                {
                    new CuiTextComponent{Color = $"{HexToRustFormat("#CCD68AFF")}", Text = "Жалоба на игрока(-ов), успешно отправлена, в ближайшее время она будет рассмотрена", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-bold.ttf", FadeIn = 0.5f},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            CuiHelper.AddUi(player, container);
            timer.Once(5f, () => 
            {
                CuiHelper.DestroyUi(player, "Text");
                CuiHelper.DestroyUi(player, "Message");
            });
            LoadedPlayers(player);
        }

        private void SearchHandler(BasePlayer player, string name)
        {
            LoadedPlayers(player, name);
        }

        #endregion

        #region UI

        private void ModerUI(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Overlay", "ModerBG");

            container.Add(new CuiElement
            {
                Parent = "ModerBG",
                Name = "BG",
                Components =
                {
                    new CuiRawImageComponent{Color = "1 1 1 1", Png = ImageLibrary.Call<string>("GetImage", "Viniette")},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                    {
                    AnchorMin = "0 0",
                    AnchorMax= "1 1"
                    },
                Text =
                    {
                    Text = "",
                    },
                Button =
                    {
                    Color = "0 0 0 0",
                    Command = "reports closemenu",
                    Close = "ModerBG",
                    }
            }, "BG");

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-166.43 -105.828", OffsetMax = "166.548 164.582" }
            }, "BG", "ModerUI");


            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-166.49 72.136", OffsetMax = "129.172 105.236" }
            }, "ModerUI", "Title");

            container.Add(new CuiElement
            {
                Name = "TitleText",
                Parent = "Title",
                Components = {
                    new CuiTextComponent { Text = "Панель модератора", Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = $"{HexToRustFormat("#E8E2D9E5")}" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-147.83 -16.55", OffsetMax = "147.83 16.55" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = $"{HexToRustFormat("#B6B6B63E")}", Command = "reports back" },
                Text = { Text = ""},
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "133.338 72.136", OffsetMax = "166.49 105.236" }
            }, "ModerUI", "BackButton");

            container.Add(new CuiPanel
            {
                Image = { Color = $"1 1 1 0.5", Sprite = "assets/icons/enter.png" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 10", OffsetMax = "-10 -10" }
            }, "BackButton");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "36.805 45.229", OffsetMax = "166.495 66.422" }
            }, "ModerUI", "LastReportsTitlePanel");

            container.Add(new CuiElement
            {
                Name = "LastReportsText",
                Parent = "LastReportsTitlePanel",
                Components = {
                    new CuiTextComponent { Text = "Последние репорты", Font = "robotocondensed-bold.ttf", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.846 -10.597", OffsetMax = "64.844 10.596" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "36.805 -142.205", OffsetMax = "166.495 41.165" }
            }, "ModerUI", "LastReports");

            CuiHelper.DestroyUi(player, "ModerUI");
            CuiHelper.AddUi(player, container);

            LoadedPlayersModerator(player);
        }

        private void ReportsUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MainLayer3");
            CuiHelper.DestroyUi(player, "CheckBG");

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Overlay", "MainLayer3");

            container.Add(new CuiElement
            {
                Parent = "MainLayer3",
                Name = "BG",
                Components =
                {
                    new CuiRawImageComponent{Color = "1 1 1 1", Png = ImageLibrary.Call<string>("GetImage", "Viniette")},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                    {
                    AnchorMin = "0 0",
                    AnchorMax= "1 1"
                    },
                Text =
                    {
                    Text = "",
                    },
                Button =
                    {
                    Color = "0 0 0 0",
                    Close = "MainLayer3",
                    }
            }, "BG");

            container.Add(new CuiPanel
            {
                Image = { Color = $"0 0 0 0" },
                RectTransform = { AnchorMin = "0.3697917 0.3111111", AnchorMax = "0.6302084 0.6851852" }
            }, "BG", "Parent");

            container.Add(new CuiPanel
            {
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "-2.235174E-08 0.8762376", AnchorMax = "1 1" }
            }, "Parent", "Title");

            container.Add(new CuiElement
            {
                Parent = "Title",
                Components =
                        {
                            new CuiTextComponent{Color = $"{HexToRustFormat("#E8E2D9E5")}", Text = "Репорты", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0 0.7698019", AnchorMax = "0.06999984 0.8564357" }
            }, "Parent", "LeftIcon");

            bool isModerator = permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission);

            var sprite = isModerator ? "assets/icons/facepunch.png" : "assets/icons/info.png";

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.9299951 0.7698019", AnchorMax = "0.9999942 0.8564357"
                },
                Text =
                {
                    Text = "",
                },
                Button =
                {
                    Color = $"{HexToRustFormat("#ECCC7A3E")}",
                    Command = isModerator ? "reports moderation" : ""
                }
            }, "Parent", "RightIcon");

            container.Add(new CuiPanel
            {
                Image = { Color = $"{HexToRustFormat("#ECCC7AC0")}", Sprite = sprite },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
            }, "RightIcon");

            container.Add(new CuiPanel
            {
                Image = { Color = $"1 1 1 0.5", Sprite = "assets/icons/web.png" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
            }, "LeftIcon");

            container.Add(new CuiPanel
            {
                Image = { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                RectTransform = { AnchorMin = "0.6100001 0.3044554", AnchorMax = "0.9999942 0.7499995" }
            }, "Parent", "RightPanel");

            container.Add(new CuiElement
            {
                Parent = "Parent",
                Name = "Input",
                Components =
                {
                    new CuiImageComponent { Color = $"{HexToRustFormat("#B6B6B63E")}" },
                    new CuiRectTransformComponent { AnchorMin = "0.07999998 0.7698019", AnchorMax = "0.9199998 0.8564357" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "Input",
                Components =
                        {
                            new CuiInputFieldComponent{Color = $"{HexToRustFormat("#FFFFFF59")}", Text = "Поиск по нику/steamid64", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", Command = "reports search"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "5 0"}
                        }
            });

            CuiHelper.AddUi(player, container);
            LoadedPlayers(player);
        }

        private Tuple<float, float> CalculateOffsets(int index)
        {
            float initialOffsetMinY = 35.442f;
            float initialOffsetMaxY = 66.744f;
            float margin = 35.442f;

            float offsetMinY = initialOffsetMinY - index * margin;
            float offsetMaxY = initialOffsetMaxY - index * margin;

            return Tuple.Create(offsetMinY, offsetMaxY);
        }



        private void LoadedPlayersModerator(BasePlayer player)
        {
            var container = new CuiElementContainer();

            for (int i = 0; i < 6; i++)
            {
                CuiHelper.DestroyUi(player, $"CheckPanel{i}");
                CuiHelper.DestroyUi(player, $"LastReport{i}");
            }

            double offsetminy = 35.442, offsetmaxy = 66.744, margin = 35.442;

            var dict = BasePlayer.activePlayerList
                .Where(z => z.userID != player.userID && ReportInformation[z.userID].ReportCount >= _config.AlertCount).OrderByDescending(x => ReportInformation[x.userID].ReportCount).ToList();

            foreach (var i in Enumerable.Range(0, 6))
            {
                var item = dict.ElementAtOrDefault(i);

                if (item == null) 
                {
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-166.49 {offsetminy}", OffsetMax = $"32.991 {offsetmaxy}"
                        },
                        Button =
                        {
                            Color = i % 2 == 0 ? $"{HexToRustFormat("#B6B6B62E")}" : $"{HexToRustFormat("#B6B6B640")}",
                        },
                        Text =
                        {
                            Text = ""
                        }
                    }, "ModerUI", $"CheckPanel{i}");
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-166.49 {offsetminy}", OffsetMax = $"32.991 {offsetmaxy}"
                        },
                        Button =
                        {
                            Color = i % 2 == 0 ? $"{HexToRustFormat("#B6B6B62E")}" : $"{HexToRustFormat("#B6B6B640")}",
                            Command = $"reports trycheck {item.userID} {i}"
                        },
                        Text =
                        {
                            Text = ""
                        }
                    }, "ModerUI", $"CheckPanel{i}");

                    container.Add(new CuiElement
                    {
                        Name = "Avatar",
                        Parent = $"CheckPanel{i}",
                        Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.UserIDString, 0) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-96.326 -10.902", OffsetMax = "-73.307 12.918" }
                        }
                    });


                    var status = ReportInformation[item.userID].PlayerStatus;

                    container.Add(new CuiElement
                    {
                        Name = "PlayerName",
                        Parent = $"CheckPanel{i}",
                        Components =
                        {
                            new CuiTextComponent{Color = status != PlayerInfo.Status.None ? $"{HexToRustFormat("#84CBFF")}" : $"{HexToRustFormat("#E8E2D9E5")}", Text = $"{item.displayName}", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-bold.ttf"},
                            new CuiRectTransformComponent{ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-69.496 -2.844", OffsetMax = "55.6 15.65" }
                        }
                    });  

                    container.Add(new CuiElement
                    {
                        Name = "ReportCount",
                        Parent = $"CheckPanel{i}",
                        Components =
                        {
                            new CuiTextComponent{Color = $"{HexToRustFormat("#E8E2D9E5")}", Text = status != PlayerInfo.Status.None ? "Игрок на проверке..." : $"{ReportInformation[item.userID].ReportCount} жалобы", Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-bold.ttf"},
                            new CuiRectTransformComponent{ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-69.496 -15.651", OffsetMax = "55.6 2.844" }
                        }
                    });


                }

                offsetminy -= margin;
                offsetmaxy -= margin;
            }

            double offsetminy2 = 60.238, offsetmaxy2 = 83.434, margin2 = 28.456;

            if (LastReport != null)
            {
                HashSet<ulong> takenSteamIds = new HashSet<ulong>();

                foreach (var j in Enumerable.Range(0, 6))
                {
                    var item = LastReport
        .Where(x => BasePlayer.FindByID(x.SteamId) != null && !takenSteamIds.Contains(x.SteamId)).ElementAtOrDefault(j);

                    if (item != null)
                    {
                        container.Add(new CuiPanel
                        {
                            CursorEnabled = false,
                            Image = { Color = "0 0 0 0" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-64.844 {offsetminy2}", OffsetMax = $"64.846 {offsetmaxy2}" }
                        }, "LastReports", $"LastReport{j}");


                        container.Add(new CuiElement
                        {
                            Name = $"Avatar{item.SteamId}",
                            Parent = $"LastReport{j}",
                            Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.SteamId.ToString(), 0) },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-61.816 -11.598", OffsetMax = "-38.316 11.598" }
                            }
                        });

                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1"
                            },
                            Button =
                            {
                                Color = "0 0 0 0",
                                Command = $"reports trychecklast {item.SteamId}"
                            },
                            Text =
                            {
                                Text = ""
                            }
                        }, $"Avatar{item.SteamId}", $"CallBTN{item.SteamId}");

                        var status = ReportInformation[item.SteamId].PlayerStatus;

                        var name = covalence.Players.FindPlayerById(item.SteamId.ToString()).Name;

                        var SubStringName = name.Length > 12 ? name.Substring(0, 12) + "..." : name; 

                        container.Add(new CuiElement
                        {
                            Name = "PlayerName",
                            Parent = $"LastReport{j}",
                            Components =
                            {
                                new CuiTextComponent{Color = status == PlayerInfo.Status.OnCheck ? $"{HexToRustFormat("#84CBFF")}" : $"{HexToRustFormat("#E8E2D9E5")}", Text = $"{SubStringName}", Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-bold.ttf"},
                                new CuiRectTransformComponent{ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-34.649 -2.028", OffsetMax = "50 11.598" }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Name = "ReportCount",
                            Parent = $"LastReport{j}",
                            Components =
                            {
                                new CuiTextComponent{Color = $"{HexToRustFormat("#E8E2D9E5")}", Text = status == PlayerInfo.Status.OnCheck ? "Игрок на проверке..." : $"{ReportInformation[item.SteamId].ReportCount} жалобы", Align = TextAnchor.MiddleLeft, FontSize = 6, Font = "robotocondensed-bold.ttf"},
                                new CuiRectTransformComponent{ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-34.649 -12.413", OffsetMax = "50 1.213"}
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Name = "ReportTime",
                            Parent = $"LastReport{j}",
                            Components =
                            {
                                new CuiTextComponent{Color = $"{HexToRustFormat("#E8E2D9E5")}", Text = $"{TimeToString(DateTime.Now.Subtract(item.Time).TotalSeconds)}", Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-bold.ttf"},
                                new CuiRectTransformComponent{ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "43.561 -9.995", OffsetMax = "64.845 9.995"}
                            }
                        });


                        takenSteamIds.Add(item.SteamId);
                    }

                    offsetminy2 -= margin2;
                    offsetmaxy2 -= margin2;

                }

            }


            CuiHelper.AddUi(player, container);
        }

        private void LoadedPlayers(BasePlayer player, string TargetName = "")
        {
            var container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, $"Reason1");
            CuiHelper.DestroyUi(player, $"Reason2");
            CuiHelper.DestroyUi(player, $"Reason3");

            for (int j = 0; j < 6; j++)
            {
                CuiHelper.DestroyUi(player, $"Panel{j}");
            }
            for (int i = 0; i < 4; i++)
            {
                CuiHelper.DestroyUi(player, $"PanelR{i}");
            }

            double anchormin2 = 0.6336634, anchormax2 = 0.7499995;
            var dict = BasePlayer.activePlayerList
                .Where(z => z.userID != player.userID && (z.displayName.ToLower().Contains(TargetName.ToLower()) || z.userID.ToString().Contains(TargetName)))
                .OrderBy(x => {
                    int index = ReportInformation[player.userID].DeathList.IndexOf(x.userID);
                    return index >= 0 && index <= 5 ? index : int.MaxValue;
                });

            foreach (var i in Enumerable.Range(0, 6))
            {
                var item = dict.ElementAtOrDefault(i);
                if (item != null)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = $"0 {anchormin2}",
                            AnchorMax = $"0.5999997 {anchormax2}"
                        },
                        Button =
                        {
                            Color = i % 2 == 0 ? $"{HexToRustFormat("#B6B6B62E")}" : $"{HexToRustFormat("#B6B6B640")}",
                            Command = $"reports add {item.userID}",
                        },
                        Text =
                        {
                            Text = ""
                        }
                    }, "Parent", $"Panel{i}");

                    if (ReportInformation[player.userID].ReportsList.Contains(item.userID))
                    {
                        container.Add(new CuiElement
                        {
                            Parent = $"Panel{i}",
                            Components =
                        {
                            new CuiRawImageComponent{Color = "1 1 1 1", Png = ImageLibrary.Call<string>("GetImage", "Gradient"), FadeIn = 1f},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-0.2 -0.2"}
                        }
                        });

                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = $"0.9166671 0.3617443",
                                AnchorMax = $"0.9666671 0.6808972"
                            },
                            Button =
                            {
                                Color = "0 0 0 0",
                                Command = $"reports remove {item.userID}",
                            },
                            Text =
                            {
                                Text = "",
                            }
                        }, $"Panel{i}", "RemoveBTN");

                        container.Add(new CuiElement
                        {
                            Parent = $"RemoveBTN",
                            Components =
                        {
                            new CuiRawImageComponent{Color = "1 1 1 1", Png = ImageLibrary.Call<string>("GetImage", "RemoveBTN")},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                        });
                    }

                    container.Add(new CuiElement
                    {
                        Parent = $"Panel{i}",
                        Components =
                        {
                            new CuiRawImageComponent{Color = "1 1 1 1", Png = GetImage(item.UserIDString, 0)},
                            new CuiRectTransformComponent{AnchorMin = "0.01666642 0.1276988", AnchorMax = "0.1333331 0.8936658"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = $"Panel{i}",
                        Components =
                        {
                            new CuiTextComponent{Color = $"{HexToRustFormat("#E8E2D9E5")}", Text = $"{item.displayName}", Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf"},
                            new CuiRectTransformComponent{AnchorMin = "0.1566666 0.4361705", AnchorMax = "0.59 0.989369"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = $"Panel{i}",
                        Components =
                        {
                            new CuiTextComponent{Color = $"1 1 1 0.7", Text = $"{item.userID}", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-bold.ttf"},
                            new CuiRectTransformComponent{AnchorMin = "0.1566666 0.09574071", AnchorMax = "0.8233335 0.5212779"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = $"0 {anchormin2}",
                            AnchorMax = $"0.5999997 {anchormax2}"
                        },
                        Button =
                        {
                            Color = i % 2 == 0 ? $"{HexToRustFormat("#B6B6B62E")}" : $"{HexToRustFormat("#B6B6B640")}",
                            Command = $"",
                        },
                        Text =
                        {
                            Text = ""
                        }
                    }, "Parent", $"Panel{i}");
                }

                anchormin2 -= 0.1287136;
                anchormax2 -= 0.1287136;
            }

            double anchormin = 0.7722343, anchormax = 0.9777927;

            for (int j = 0; j < 4; j++)
            {
                if (j < ReportInformation[player.userID].ReportsList.Count)
                {
                    var item = ReportInformation[player.userID].ReportsList[j];
                    container.Add(new CuiPanel
                    {
                        Image = { Color = $"0 0 0 0" },
                        RectTransform = { AnchorMin = $"0.02644184 {anchormin}", AnchorMax = $"0.9743726 {anchormax}" }
                    }, "RightPanel", $"PanelR{j}");

                    container.Add(new CuiElement
                    {
                        Parent = $"PanelR{j}",
                        Components =
                        {
                            new CuiRawImageComponent{Color = "1 1 1 1", Png = GetImage(item.ToString(), 0)},
                            new CuiRectTransformComponent{AnchorMin = "0.004564997 0.02702122", AnchorMax = "0.1939143 0.972964"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = $"PanelR{j}",
                        Components =
                        {
                            new CuiTextComponent{Color = $"{HexToRustFormat("#E8E2D9E5")}", Text = $"{covalence.Players.FindPlayerById(item.ToString()).Name}", Align = TextAnchor.MiddleLeft, FontSize = 11, Font = "robotocondensed-bold.ttf"},
                            new CuiRectTransformComponent{AnchorMin = "0.2161763 0.4361705", AnchorMax = "0.8112735 1.030762"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = $"PanelR{j}",
                        Components =
                        {
                            new CuiTextComponent{Color = $"1 1 1 0.7", Text = $"{item}", Align = TextAnchor.MiddleLeft, FontSize = 7, Font = "robotocondensed-bold.ttf"},
                            new CuiRectTransformComponent{AnchorMin = "0.2161763 0.0848209", AnchorMax = "0.7842237 0.4631974"}
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform =
                            {
                                AnchorMin = $"0.8625675 0.2266099",
                                AnchorMax = $"0.970767 0.7671477"
                            },
                        Button =
                            {
                                Color = $"{HexToRustFormat("#EC7A7A40")}",
                                Command = $"reports remove {item}",
                            },
                        Text =
                            {
                                Text = "",
                            }
                    }, $"PanelR{j}", $"RemoveBTN{j}");

                    container.Add(new CuiElement
                    {
                        Parent = $"RemoveBTN{j}",
                        Components =
                        {
                            new CuiImageComponent{Color = $"{HexToRustFormat("#E0947A")}", Sprite = "assets/icons/close.png"},
                            new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "3 3", OffsetMax = "-3 -3"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = "RightPanel",
                        Name = $"PanelR{j}",
                        Components =
                        {
                            new CuiRawImageComponent{Color = "1 1 1 1", Png = ImageLibrary.Call<string>("GetImage", "Coursive")},
                            new CuiRectTransformComponent{AnchorMin = $"0.02644184 {anchormin}", AnchorMax = $"0.9743726 {anchormax}"}
                        }
                    });
                }
                anchormin -= 0.2500033;
                anchormax -= 0.2500033;
            }


            container.Add(new CuiButton
            {
                RectTransform =
                        {
                            AnchorMin = $"0.6100001 0.1955446",
                            AnchorMax = $"0.9999942 0.2846536"
                        },
                Button =
                        {
                            Color = ReportInformation[player.userID].ReportsList.Count() > 0 && ReportInformation[player.userID].ReportsList.Count() < 2 ? $"182 182 182 0.25" : "82 182 182 0.15",
                            Command = ReportInformation[player.userID].ReportsList.Count() > 0 && ReportInformation[player.userID].ReportsList.Count() < 2 ? $"reports send Читы" : "",
                        },
                Text =
                        {
                            Text = "Читы",
                            Align = TextAnchor.MiddleCenter,
                            Color = ReportInformation[player.userID].ReportsList.Count() > 0 && ReportInformation[player.userID].ReportsList.Count() < 2 ? "232 226 217 0.8" : "232 226 217 0.4",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14
                        }
            }, "Parent", $"Reason1");

            container.Add(new CuiButton
            {
                RectTransform =
                        {
                            AnchorMin = $"0.6100001 0.09653473",
                            AnchorMax = $"0.9999942 0.1856435"
                        },
                Button =
                        {
                            Color = ReportInformation[player.userID].ReportsList.Count() > 0 && ReportInformation[player.userID].ReportsList.Count() < 2 ? $"182 182 182 0.25" : "82 182 182 0.15",
                            Command = ReportInformation[player.userID].ReportsList.Count() > 0 && ReportInformation[player.userID].ReportsList.Count() < 2 ? $"reports send Макрос" : "",
                        },
                Text =
                        {
                            Text = "Макрос",
                            Align = TextAnchor.MiddleCenter,
                            Color = ReportInformation[player.userID].ReportsList.Count() > 0 && ReportInformation[player.userID].ReportsList.Count() < 2 ? "232 226 217 0.8" : "232 226 217 0.4",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14
                        }
            }, "Parent", $"Reason2");

            container.Add(new CuiButton
            {
                RectTransform =
                        {
                            AnchorMin = $"0.6100001 -0.004950392",
                            AnchorMax = $"0.9999942 0.08415844"
                        },
                Button =
                        {
                            Color = ReportInformation[player.userID].ReportsList.Count() > 0 && ReportInformation[player.userID].ReportsList.Count() != 1 ? $"182 182 182 0.25" : "82 182 182 0.15",
                            Command = ReportInformation[player.userID].ReportsList.Count() > 0 && ReportInformation[player.userID].ReportsList.Count() != 1 ? $"reports send 4+" : "",
                        },
                Text =
                        {
                            Text = "4+",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            Color = ReportInformation[player.userID].ReportsList.Count() > 0 && ReportInformation[player.userID].ReportsList.Count() != 1 ? "232 226 217 0.8" : "232 226 217 0.4",
                            FontSize = 14
                        }
            }, "Parent", $"Reason3");

            CuiHelper.AddUi(player, container);
        }

        #endregion
    }
}
