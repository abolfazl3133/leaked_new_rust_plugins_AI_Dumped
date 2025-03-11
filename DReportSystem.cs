using Microsoft.VisualBasic;
using System;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using CompanionServer;

namespace Oxide.Plugins
{
    [Info("DReportSystem", "Drop Dead", "1.0.33")]
    public class DReportSystem : RustPlugin
    {
        #region Variables

        [PluginReference] private Plugin ImageLibrary;
        private static DReportSystem _ins;
        public Dictionary<ulong, player> Players = new Dictionary<ulong, player>();
        public Dictionary<ulong, ulong> Check = new Dictionary<ulong, ulong>();
        string Layer = "DReportSystem.Main";
        string Layer2 = "DReportSystem.Notify";
        string Layer3 = "DReportSystem.PlayerInfo";
        bool debug = false;

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Repeated Report", "You have already sent a report to this player!" },
                { "New Report", "{hostname and connect}A new player report has been received!\nName of the complaining player: {0}, his SteamID: {1}\nSuspect: {2}, his SteamID: {3}\n\nReport title: {4}, report text: {5 }" },
                { "Report Command", "To send a complaint about a player press the F7 key" },
                { "No Access", "You do not have access to this command" },
                { "Player Not Found", "Player not found. Maybe he went out" },
                { "Player On Check", "The player is already being on check!" },
                { "You On Check", "You are undergoing player check! To start a new one, end the current" },
                { "Player Check Notify", "<size=16>WARNING!</size>\nYou are suspected of using third-party software\nSubmit your Discord to the moderator with the /discord command, you have 5 minutes" },
                { "Moderator Check Notify", "<size=16>WARNING!</size>\nYou are checking the player\nWait for the player to submit his Discord and get to work\nTo complete the check, write /check and follow the further instructions, if a player needs to be banned, use /ban after that" },
                { "Send Discord Help", "To send your Discord to a moderator use: /discord\nBe sure to follow the instructions, otherwise the moderator will not see your discord!" },
                { "Send Discord Error", "You have not entered your Discord correctly. Follow the instructions for the /discord command" },
                { "Moderator Leave", "The moderator who checked you came out. Verification completed automatically without a verdict" },
                { "Player Send Discord", "WARNING! The player you are checking sent you their Discord: {0}" },
                { "Discord Double", "The player's discord is also duplicated in your console (F1)" },
                { "Verdict Help", "To close player check enter /check cancel \"Verdict\", be sure to use quotes!" },
                { "Verdict Error", "You have passed the verdict incorrectly. Follow the instructions for the /check command" },
                { "Success Vedict", "You have successfully completed the check and delivered your verdict\nDon't forget to ban the player if he is found to be an offender :)" },
                { "SteamID", "SteamID of player {0}: {1}" },

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Repeated Report", "Вы уже отправляли репорт на этого игрока!" },
                { "New Report", "Поступила новая жалоба на игрока!\nНик игрока отправившего жалобу: {0}, его SteamID: {1}\nПодозреваемый: {2}, его SteamID: {3}\n\nЗаголовок жалобы: {4}, текст жалобы: {5}" },
                { "Report Command", "Чтобы отправить жалобу на игрока нажмите клавишу F7" },
                { "No Access", "У вас нет доступа к этой команде" },
                { "Player Not Found", "Игрок не найден. Возможно он вышел" },
                { "Player On Check", "Игрок уже находится на проверке!" },
                { "You On Check", "Вы находитесь на проверке игрока! Чтобы начать новую, завершите текущую" },
                { "Player Check Notify", "<size=16>ВНИМАНИЕ!</size>\nВы подозреваетесь в использовании постороннего ПО\nПредоставьте свой Discord модератору командой /discord, у вас есть 5 минут" },
                { "Moderator Check Notify", "<size=16>ВНИМАНИЕ!</size>\nВы находитесь на проверке игрока\nОжидайте пока игрок предоставит свой Discord и приступайте к работе\nЧтобы завершить проверку пропишите /check и следуйте дальнейшим инструкциям, если игрока требуется забанить - используйте /ban после этого" },
                { "Send Discord Help", "Чтобы отправить свой Discord модератору, используйте: /discord\nОбязательно следуйте инструкции, иначе модератор не увидит ваш дискорд!" },
                { "Send Discord Error", "Вы не правильно ввели свой Discord. Следуйте инструкциям по команде /discord" },
                { "Moderator Leave", "Модератор который вас проверял вышел. Проверка завершена автоматически без вынесения вердикта" },
                { "Player Send Discord", "ВНИМАНИЕ! Игрок, которого вы проверяете отправил вам свой Discord: {0}" },
                { "Discord Double", "Discord игрока так же продублирован вам в консоль (F1)" },
                { "Verdict Help", "Чтобы заврешить проверку игрока введите /check cancel \"Вердикт\", обязательно используйте кавычки!" },
                { "Verdict Error", "Вы не правильно вынесли вердикт. Следуйте инструкциям по команде /check" },
                { "Success Vedict", "Вы успешно завершили проверку и вынесли вердикт\nНе забудьте забанить игрока, если он оказался нарушителем :)" },
                { "SteamID", "SteamID игрока {0}: {1}" },
            }, this, "ru");
        }

        private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);

        #endregion

        #region Data

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Players", Players);
        }

        private void LoadData()
        {
            try
            {
                Players = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, player>>($"{Title}/Players");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (Players == null) Players = new Dictionary<ulong, player>();
        }

        #endregion

        #region Config

        public class report
        {
            [JsonProperty("Никнейм")]
            public string name;
            [JsonProperty("SteamID")]
            public ulong steamid;
            [JsonProperty("Заголовок")]
            public string subject;
            [JsonProperty("Сообщение")]
            public string message;
        }

        public class player
        {
            [JsonProperty("Никнейм")]
            public string name;
            [JsonProperty("Количество репортов")]
            public int reportscount;
            [JsonProperty("Список репортов на игрока")]
            public Dictionary<ulong, List<report>> Reports = new Dictionary<ulong, List<report>>();
        }

        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Настройки плагина")]
            public PSettings pluginsettings = new PSettings();
            [JsonProperty("Настройки интерфейса")]
            public PInterface plugininterface = new PInterface();
            [JsonProperty("Настройки доступа")]
            public Settings access = new Settings();
             [JsonProperty("Настройки оповещений")]
            public AlertSettings alerts = new AlertSettings();

            public class PSettings
            {
                [JsonProperty("Включить оповещение при вводе команды /report?")]
                public bool alert = true;
            }

            public class PInterface
            {
                [JsonProperty("Сортировать игроков? (если true, то вначале списка будут игроки отсортированные по убыванию количества репортов)")]
                public bool sortplayers = false;
                [JsonProperty("Английский интерфейс плагина")]
                public bool english = false;
                [JsonProperty("IP сервера (БЕЗ ПОРТА)")]
                public string ip = "Вставьте сюда IP адресс сервера без порта, пример - 192.168.1.1";
            }

            public class Settings
            {
                [JsonProperty("Пермишн для доступа к команде /reports (должен начинатся с dreportsystem.)")]
                public string perm = "dreportsystem.moderator";
                [JsonProperty("Пермишн для доступа к очистке репортов игрока (должен начинатся с dreportsystem.)")]
                public string clearperm = "dreportsystem.clear";
            }

            public class AlertSettings
            {
                [JsonProperty("[Chat] Включить оповещения о новых репортах модераторам в чат ?")]
                public bool chatalert = true;
                [JsonProperty("[Rust+] Включить оповещения о новых репортах модераторам через Rust+ ?")]
                public bool rustplusalert = true;
                [JsonProperty("[VK] Включить оповещения о новых репортах модераторам в беседу VK (не будет работать если хостинг машина находится в Украине) ?")]
                public bool vkalert = true;
                [JsonProperty("[VK] Токен приложения ВК (лучше использовать отдельную страницу для получения токена)")]
                public string token = "Если не собираетесь использовать оповещения в беседу VK, не изменяйте это значение";
                [JsonProperty("[VK] ID чата ВК")]
                public string chatid = "Если не собираетесь использовать оповещения в беседу VK, не изменяйте это значение";
                [JsonProperty("[Discord] Использовать оповещения о новых репортах в отдельный канал Discord ?")]
                public bool discordalert = true;
                [JsonProperty("[Discord] Webhook (см. инструкцию по настройке оповещений Discord)")]
                public string webhook = "Если не собираетесь использовать оповещения в канал Discord, не изменяйте это значение";
                [JsonProperty("[Discord] Webhook для логов действий модераторов")]
                public string logwebhook = "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение";
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg);
        }

        async void OnServerInitialized()
        {
            _ins = this;
            if (!plugins.Exists("ImageLibrary"))
            {
                PrintError("ImageLibrary not found. Install it and reload plugin!");
                return;
            }
            if (cfg.plugininterface.ip == "Вставьте сюда IP адресс сервера без порта, пример - 192.168.1.1" || String.IsNullOrEmpty(cfg.plugininterface.ip))
            {
                PrintWarning("[RU] Вы не указали IP сервера в конфигурации. Выгружаем плагин..");
                PrintWarning("[ENG] You didn't specify the server IP in the configuration. Unloading the plugin..");
                Interface.Oxide.UnloadPlugin(Title);
            }
            if (cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") Request.Init();
            LoadData();
            ImageLibrary.Call("AddImage", "https://i.imgur.com/n0OUC31.png", "closeicon");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/XSTJVnB.png", "backicon");
            if (!permission.PermissionExists(cfg.access.perm)) permission.RegisterPermission(cfg.access.perm, this);
            if (!permission.PermissionExists(cfg.access.clearperm)) permission.RegisterPermission(cfg.access.clearperm, this);
            if (BasePlayer.activePlayerList.Count > 0) foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);
        }

        void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList) { CuiHelper.DestroyUi(player, Layer); CuiHelper.DestroyUi(player, Layer2); CuiHelper.DestroyUi(player, Layer3); }
            if (Check != null) Check.Clear();
            if (cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") Request.Dispose();
            _ins = null;
        }

        void OnServerSave()
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

            if (player == null) return;
            if (!Players.ContainsKey(player.userID))
            {
                Players.Add(player.userID, new player
                {
                    name = player.displayName,
                    reportscount = 0
                });
            }
            if (Players.ContainsKey(player.userID) && Players[player.userID].name != player.displayName) Players[player.userID].name = player.displayName;
        }

        private void OnPlayerReported(BasePlayer player, string targetName, string targetId, string subject, string message, string type)
        {
            if (player == null)
                return;

            var suspect = BasePlayer.FindByID(ulong.Parse(targetId));
            if (suspect == null || player == null || player.IsNpc || suspect.IsNpc) return;

            SendReport(player, suspect, subject, message);
        }

        #endregion

        #region Methods

        #region API

        [HookMethod("API_SendReport")]
        public void API_SendReport(BasePlayer player, BasePlayer suspect, string subject, string message)
        {
            if (player == null || suspect == null || subject == null || message == null) return;
            SendReport(player, suspect, subject, message);
        }

        #endregion

        #region RustPlus

        void RustPlusAlert(ulong userid, string reportplayername, string suspectname, ulong reportplayerid, ulong suspectid, string subject, string message)
        {
            if (!cfg.plugininterface.english) NotificationList.SendNotificationTo(userid, NotificationChannel.SmartAlarm, "Report System", $"Игрок {reportplayername}/{reportplayerid} отправил жалобу на игрока {suspectname}/{suspectid}. Заголовок жалобы: {subject}, текст жалобы: {message}", new Dictionary<string, string>());
            else NotificationList.SendNotificationTo(userid, NotificationChannel.SmartAlarm, "Report System", $"Player {reportplayername}/{reportplayerid} sent a report to the player {suspectname}/{suspectid}. Report subject: {subject}, report message: {message}", new Dictionary<string, string>());
        }

        #endregion

        #region VK

        private void SendVKChatMessage(string message) 
        {
            var apiver = "v=5.92";
            var randomid = UnityEngine.Random.Range(Int32.MinValue, Int32.MaxValue);
            webrequest.Enqueue("https://api.vk.com/method/messages.send?chat_id="
             + cfg.alerts.chatid + "&message=" + URLEncode(message) + "&"+apiver+ "&random_id="
              + randomid +"&access_token=" + cfg.alerts.token, null, (code, response) => GetCallback(code, response, "Сообщение в беседу"), this);    
        }  

        private string URLEncode(string input)
        {
            if (input.Contains("#")) input = input.Replace("#", "%23");
            if (input.Contains("$")) input = input.Replace("$", "%24");
            if (input.Contains("+")) input = input.Replace("+", "%2B");
            if (input.Contains("/")) input = input.Replace("/", "%2F");
            if (input.Contains(":")) input = input.Replace(":", "%3A");
            if (input.Contains(";")) input = input.Replace(";", "%3B");
            if (input.Contains("?")) input = input.Replace("?", "%3F");
            if (input.Contains("@")) input = input.Replace("@", "%40");
            return input;
        }

        void GetCallback(int code, string response, string type, BasePlayer player = null)
        {
            if (!response.Contains("error")) { Puts($"{type} отправлен(о): {response}"); if (type == "Код подтверждения" && player != null) return; }
            else
            {
                if (type == "Код подтверждения")
                {
                    if (response.Contains("Can't send messages for users without permission") && player != null) return;
                    else PrintWarning("errorconfcode", $"Ошибка отправки кода подтверждения. Ответ сервера ВК: {response}");
                }
                else
                {
                    PrintWarning("Errors", $"{type} не отправлен(о). Ошибка: " + response);
                }
            }
        }

        #endregion

        #region Discord

        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings();

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json"
        };

        public class FancyMessage
        {
            [JsonProperty( "content" )] private string Content { get; set; }

            [JsonProperty( "tts" )] private bool TextToSpeech { get; set; }

            [JsonProperty( "embeds" )] private EmbedBuilder[] Embeds { get; set; }

            public FancyMessage WithContent( string value )
            {
                Content = value;
                return this;
            }

            public FancyMessage AsTTS( bool value )
            {
                TextToSpeech = value;
                return this;
            }

            public FancyMessage SetEmbed( EmbedBuilder value )
            {
                Embeds = new[]
                {
                    value
                };
                return this;
            }

            public string GetContent()
            {
                return Content;
            }

            public bool IsTTS()
            {
                return TextToSpeech;
            }

            public EmbedBuilder GetEmbed()
            {
                return Embeds[0];
            }

            public string ToJson()
            {
                return JsonConvert.SerializeObject( this, _ins._jsonSettings );
            }
        }

        public class EmbedBuilder
        {
            public EmbedBuilder()
            {
                Fields = new List<Field>();
            }

            [JsonProperty( "title" )] private string Title { get; set; }

            [JsonProperty( "color" )] private int Color { get; set; }

            [JsonProperty( "fields" )] private List<Field> Fields { get; }

            [JsonProperty( "description" )] private string Description { get; set; }

            public EmbedBuilder WithTitle( string title )
            {
                Title = title;
                return this;
            }

            public EmbedBuilder WithDescription( string description )
            {
                Description = description;
                return this;
            }

            public EmbedBuilder SetColor( int color )
            {
                Color = color;
                return this;
            }

            public EmbedBuilder SetColor( string color )
            {
                Color = ParseColor( color );
                return this;
            }

            public EmbedBuilder AddInlineField( string name, object value )
            {
                Fields.Add( new Field( name, value, true ) );
                return this;
            }

            public EmbedBuilder AddField( string name, object value )
            {
                Fields.Add( new Field( name, value, false ) );
                return this;
            }

            public EmbedBuilder AddField( Field field )
            {
                Fields.Add( field );
                return this;
            }

            public EmbedBuilder AddFields( Field[] fields )
            {
                for ( var i = 0; i < fields.Length; i++ )
                {
                    Fields.Add( fields[i] );
                }

                return this;
            }

            public int GetColor()
            {
                return Color;
            }

            public string GetTitle()
            {
                return Title;
            }

            public Field[] GetFields()
            {
                return Fields.ToArray();
            }

            private int ParseColor( string input )
            {
                int color;
                if ( !int.TryParse( input, out color ) )
                {
                    color = 3329330;
                }

                return color;
            }

            public class Field
            {
                [JsonProperty( "inline" )]
                public bool Inline;

                [JsonProperty( "name" )]
                public string Name;

                [JsonProperty( "value" )]
                public object Value;

                public Field( string name, object value, bool inline )
                {
                    Name = name;
                    Value = value;
                    Inline = inline;
                }

                public Field() { }
            }
        }

        private abstract class Response
        {
            public int Code { get; set; }
            public string Message { get; set; }
        }

        private class BaseResponse : Response
        {
            public bool IsRatelimit => Code == 429;
            public bool IsOk => ( Code == 200 ) | ( Code == 204 );
            public bool IsBad => !IsRatelimit && !IsOk;

            public RateLimitResponse GetRateLimit()
            {
                return Message.Length == 0 ? null : JsonConvert.DeserializeObject<RateLimitResponse>( Message );
            }
        }

        private class Request
        {
            private static bool _rateLimited;
            private static bool _busy;
            private static Queue<Request> _requestQueue;
            private readonly string _payload;
            private readonly Plugin _plugin;
            private readonly Action<BaseResponse> _response;
            private readonly string _url;

            public static void Init()
            {
                _requestQueue = new Queue<Request>();
            }

            private Request( string url, FancyMessage message, Action<BaseResponse> response = null, Plugin plugin = null )
            {
                _url = url;
                _payload = message.ToJson();
                _response = response;
                _plugin = plugin;
            }

            private Request( string url, FancyMessage message, Plugin plugin = null )
            {
                _url = url;
                _payload = message.ToJson();
                _plugin = plugin;
            }

            private static void SendNextRequest()
            {
                if ( _requestQueue.Count == 0 )
                {
                    return;
                }

                Request request = _requestQueue.Dequeue();
                request.Send();
            }

            private static void EnqueueRequest( Request request )
            {
                _requestQueue.Enqueue( request );
            }


            private void Send()
            {
                if ( _busy )
                {
                    EnqueueRequest( this );
                    return;
                }

                _busy = true;

                _ins.webrequest.Enqueue( _url, _payload, ( code, rawResponse ) =>
                {
                    var response = new BaseResponse
                    {
                        Message = rawResponse,
                        Code = code
                    };

                    if ( response.IsRatelimit )
                    {
                        RateLimitResponse rateLimit = response.GetRateLimit();
                        if ( rateLimit != null )
                        {
                            EnqueueRequest( this );
                            OnRateLimit( rateLimit.RetryAfter );
                        }
                    }
                    else if ( response.IsBad )
                    {
                        _ins.PrintWarning( "Failed! Discord responded with code: {0}. Plugin: {1}\n{2}", code, _plugin != null ? _plugin.Name : "Unknown Plugin", response.Message );
                    }
                    else
                    {
                        try
                        {
                            _response?.Invoke( response );
                        }
                        catch ( Exception ex )
                        {
                            Interface.Oxide.LogException( "[DiscordMessages] Request callback raised an exception!", ex );
                        }
                    }

                    _busy = false;
                    SendNextRequest();
                }, _ins, Core.Libraries.RequestMethod.POST, _ins._headers );
            }

            private static void OnRateLimit( int retryAfter )
            {
                if ( _rateLimited )
                {
                    return;
                }

                _rateLimited = true;
                _ins.timer.In( retryAfter / 1000, OnRateLimitEnd );
            }

            private static void OnRateLimitEnd()
            {
                _rateLimited = false;
                SendNextRequest();
            }

            public static void Send( string url, FancyMessage message, Plugin plugin = null )
            {
                new Request( url, message, plugin ).Send();
            }

            public static void Send( string url, FancyMessage message, Action<BaseResponse> callback, Plugin plugin = null )
            {
                new Request( url, message, callback, plugin ).Send();
            }

            public static void Dispose()
            {
                _requestQueue = null;
                _rateLimited = false;
                _busy = false;
            }
        }

        private class RateLimitResponse : BaseResponse
        {
            [JsonProperty( "retry_after" )] public int RetryAfter { get; set; }
        }

        async void DMessage(string sendername, string reportedname, ulong senderid, ulong reportedid, string title, string message)
        {
            if (cfg.alerts.discordalert && cfg.alerts.webhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение")
            {
                if (!cfg.plugininterface.english)
                {
                    string webhook = cfg.alerts.webhook;
                    string res = @"{
                    ""username"": ""Report System"",
                    ""avatar_url"": ""https://i.imgur.com/5L3JYro.png"",
                    ""embeds"": [
                        {
                            ""title"": ""{title}"",
                            ""description"": ""Отправивший жалобу: **[{0}](http://steamcommunity.com/profiles/{senderurl})**, Подозреваемый: **[{1}](http://steamcommunity.com/profiles/{reportedurl})**\nЗаголовок жалобы: **{2}**\nТекст жалобы: **{3}**\n\nСервер: **{server}**\nАдресс для подключения: `connect {ip}:{port}`"",
                            ""color"": 16726082,
                            ""url"": ""https://oxide-russia.ru/resources/1343/"",
                            ""footer"":
                            {
                                ""text"": ""DReportSystem v{ver} • by Drop Dead"",
                                ""icon_url"": ""https://t3.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=http://oxide-russia.ru&size=16""
                            }
                        }
                    ]
                    }".Replace("{ver}", Version.ToString()).Replace("{title}", cfg.plugininterface.english ? "New report received!" : "Получена новая жалоба!").Replace("{0}", sendername).Replace("{senderurl}", senderid.ToString()).Replace("{1}", reportedname).Replace("{reportedurl}", reportedid.ToString()).Replace("{2}", title).Replace("{3}", message).Replace("{server}", ConVar.Server.hostname).Replace("{ip}", cfg.plugininterface.ip).Replace("{port}", ConVar.Server.port.ToString());
                    webrequest.Enqueue(webhook, res, (code, response) => 
                    {
                        Puts($"{code}");
                        Puts($"{response}");
                    }, this, Core.Libraries.RequestMethod.POST, new Dictionary<string, string>(){{"Content-Type", "application/json"}}, 5000);
                }
                else
                {
                    string webhook = cfg.alerts.webhook;
                    string res = @"{
                    ""username"": ""Report System"",
                    ""avatar_url"": ""https://i.imgur.com/5L3JYro.png"",
                    ""embeds"": [
                        {
                            ""title"": ""{title}"",
                            ""description"": ""Report Sender: **[{0}](http://steamcommunity.com/profiles/{senderurl})**, Suspect: **[{1}](http://steamcommunity.com/profiles/{reportedurl})**\nReport title: **{2}**\nReport message: **{3}**\n\nServer: **{server}**\nConnection address: `connect {ip}:{port}`"",
                            ""color"": 16726082,
                            ""url"": ""https://oxide-russia.ru/resources/1343/"",
                            ""footer"":
                            {
                                ""text"": ""DReportSystem v{ver} • by Drop Dead"",
                                ""icon_url"": ""https://t3.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=http://oxide-russia.ru&size=16""
                            }
                        }
                    ]
                    }".Replace("{ver}", Version.ToString()).Replace("{title}", cfg.plugininterface.english ? "New report received!" : "Получена новая жалоба!").Replace("{0}", sendername).Replace("{senderurl}", senderid.ToString()).Replace("{1}", reportedname).Replace("{reportedurl}", reportedid.ToString()).Replace("{2}", title).Replace("{3}", message).Replace("{server}", ConVar.Server.hostname).Replace("{ip}", cfg.plugininterface.ip).Replace("{port}", ConVar.Server.port.ToString());
                    webrequest.Enqueue(webhook, res, (code, response) => 
                    {
                        Puts($"{code}");
                        Puts($"{response}");
                    }, this, Core.Libraries.RequestMethod.POST, new Dictionary<string, string>(){{"Content-Type", "application/json"}}, 5000);
                }
            }
            else return;
        }

        /*void DiscordMessage(string message)
        {
            if (cfg.alerts.webhook == "Если не собираетесь использовать оповещения в канал Discord, не изменяйте это значение" || string.IsNullOrEmpty(cfg.alerts.webhook)) return;
            //DiscordMessages?.Call("API_SendTextMessage", cfg.alerts.webhook, message); 
            Request.Send(cfg.alerts.webhook, new FancyMessage().AsTTS(false).WithContent(message), null);
            //DMessage("report", message);
        }*/
        void DiscordLog(string message)
        {
            if (cfg.alerts.logwebhook == "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение" || string.IsNullOrEmpty(cfg.alerts.logwebhook)) return;
            //DiscordMessages?.Call("API_SendTextMessage", cfg.alerts.logwebhook, message); 
            Request.Send(cfg.alerts.logwebhook, new FancyMessage().AsTTS(false).WithContent(message), null);
            //DMessage("log", message);
        }

        #endregion

        void Log(string text)
        {
            LogToFile("Checks", text, this, true);
        }

        async void SendReport(BasePlayer player, BasePlayer suspect, string subject, string message)
        {
            if (debug) PrintError($"API works! {player.displayName} reported {player.displayName} for {subject}, {message}");
            if (player == null || suspect == null || player.IsNpc || suspect.IsNpc || player == suspect) return;
            if (!Players.ContainsKey(suspect.userID) || !Players.ContainsKey(player.userID)) return;

            if (Players[suspect.userID].Reports.ContainsKey(player.userID))
            {
                player.ChatMessage(GetMsg("Repeated Report", player.UserIDString));
                return;
            }

            Players[suspect.userID].reportscount++;
            List<report> value;
            var List = Players[suspect.userID].Reports.TryGetValue(player.userID, out value);
            if (List == false)
            {
                Players[suspect.userID].Reports.Add(player.userID, new List<report> { new report { name = player.displayName, steamid = player.userID, subject = subject, message = message } });
            }
            else
            {
                Players[suspect.userID].Reports[player.userID].Add(new report { name = player.displayName, steamid = player.userID, subject = subject, message = message });
            }

            if (!cfg.plugininterface.english) Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\nИгрок {player.displayName}/{player.userID} отправил жалобу на игрока: {suspect.displayName}/{suspect.userID}. Заголовок жалобы: {subject.Replace("[cheat] ", string.Empty).Replace("[spam] ", string.Empty).Replace("[name] ", string.Empty).Replace("[abusive] ", string.Empty)}, текст: {message}");
            else Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\nPlayer {player.displayName}/{player.userID} filed a complaint against player: {suspect.displayName}/{suspect.userID}. Complaint title: {subject.Replace("[cheat] ", string.Empty).Replace("[spam] ", string.Empty).Replace("[name] ", string.Empty).Replace("[abusive] ", string.Empty)}, text: {message}");
            foreach (var players in BasePlayer.activePlayerList.Where(x => permission.UserHasPermission(x.UserIDString, cfg.access.perm)))
            {
                if (cfg.alerts.rustplusalert) RustPlusAlert(players.userID, player.displayName, suspect.displayName, player.userID, suspect.userID, subject.Replace("[cheat] ", string.Empty).Replace("[spam] ", string.Empty).Replace("[name] ", string.Empty).Replace("[abusive] ", string.Empty), message);
                if (cfg.alerts.chatalert) players.ChatMessage(GetMsg("New Report", players.UserIDString).Replace("{hostname and connect}", "").Replace("{0}", $"{player.displayName}").Replace("{1}", $"{player.userID}").Replace("{2}", $"{suspect.displayName}").Replace("{3}", $"{suspect.userID}").Replace("{4}", $"{subject.Replace("[cheat] ", string.Empty).Replace("[spam] ", string.Empty).Replace("[name] ", string.Empty).Replace("[abusive] ", string.Empty)}").Replace("{5}", $"{message}"));
            }

            string text = $"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}";
            string discordtext = $"{ConVar.Server.hostname}\n``connect {cfg.plugininterface.ip}:{ConVar.Server.port}``";
            if (cfg.alerts.vkalert && cfg.alerts.token != "Если не собираетесь использовать оповещения в беседу VK, не изменяйте это значение" 
            && cfg.alerts.chatid != "Если не собираетесь использовать оповещения в беседу VK, не изменяйте это значение") SendVKChatMessage(GetMsg("New Report").Replace("{hostname and connect}", text).Replace("{0}", $"{player.displayName}").Replace("{1}", $"{player.userID}").Replace("{2}", $"{suspect.displayName}").Replace("{3}", $"{suspect.userID}").Replace("{4}", $"{subject.Replace("[cheat] ", string.Empty).Replace("[spam] ", string.Empty).Replace("[name] ", string.Empty).Replace("[abusive] ", string.Empty)}").Replace("{5}", $"{message}"));
            if (cfg.alerts.discordalert) //DiscordMessage(GetMsg("New Report").Replace("{hostname and connect}", discordtext).Replace("{0}", $"``{player.displayName}``").Replace("{1}", $"``{player.userID}``").Replace("{2}", $"``{suspect.displayName}``").Replace("{3}", $"``{suspect.userID}``").Replace("{4}", $"``{subject.Replace("[cheat] ", string.Empty).Replace("[spam] ", string.Empty).Replace("[name] ", string.Empty).Replace("[abusive] ", string.Empty)}``").Replace("{5}", $"``{message}``"));
                DMessage(player.displayName, suspect.displayName, player.userID, suspect.userID, subject.Replace("[cheat] ", string.Empty).Replace("[spam] ", string.Empty).Replace("[name] ", string.Empty).Replace("[abusive] ", string.Empty), message);
        }

        bool IsOnline(ulong id)
        {
            var suspect = BasePlayer.FindByID(id);
            if (suspect != null) return true;
            else return false;
        }

        void DeletePlayerFromDict(ulong id, string name)
        {
            if (!Players.ContainsKey(id)) return;

            Players.Remove(id);
            Players.Add(id, new player
            {
                name = name,
                reportscount = 0
            });
        }

        void AlarmCheckSuspect(BasePlayer suspect)
        {
            if (suspect == null) return;
            if (!Check.ContainsKey(suspect.userID)) return;
            DrawNotify(suspect, GetMsg("Player Check Notify", suspect.UserIDString));
        }

        void AlarmCheckModerator(BasePlayer moderator)
        {
            if (moderator == null) return;
            if (!Check.ContainsValue(moderator.userID)) return;
            DrawNotify(moderator, GetMsg("Moderator Check Notify", moderator.UserIDString));
        }

        void CancelCheck(BasePlayer suspect, BasePlayer moderator)
        {

            if (Check.ContainsKey(suspect.userID)) Check.Remove(suspect.userID);
            DeletePlayerFromDict(suspect.userID, suspect.displayName);
            CuiHelper.DestroyUi(suspect, Layer2);

            if (moderator != null)
            {
                if (Check.ContainsKey(moderator.userID)) Check.Remove(moderator.userID);
                CuiHelper.DestroyUi(moderator, Layer2);
            }
        }

        string CheckText(int chars, string text)
        {
            if (text.Length >= chars) return text.Substring(0, chars - 2) + "..";
            return text;
        }

        #endregion

        #region Commands


        [ChatCommand("check")]
        private void CheckCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, cfg.access.perm)) return;
            if (!Check.ContainsValue(player.userID)) return;
            if (args.Length < 1)
            {
                player.ChatMessage(GetMsg("Verdict Help", player.UserIDString));
                return;
            }
            if (args.Length > 2)
            {
                player.ChatMessage(GetMsg("Verdict Error", player.UserIDString));
                return;
            }

            if (args[0] == "cancel")
            {
                if (args.Length < 2)
                {
                    player.ChatMessage(GetMsg("Verdict Error", player.UserIDString));
                    return;
                }
                player.ChatMessage(GetMsg("Success Vedict", player.UserIDString));

                if (!cfg.plugininterface.english) Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\n{player.displayName}/{player.userID} закончил проверку игрока и вынес свой вердикт: {args[1]}");
                else Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\n{player.displayName}/{player.userID} finished checking the player and delivered his verdict: {args[1]}");

                if (!cfg.plugininterface.english && cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") 
                DiscordLog($"{ConVar.Server.hostname}:\n``connect {cfg.plugininterface.ip}:{ConVar.Server.port}``\n\n``{player.displayName}/{player.userID}`` закончил проверку игрока и вынес свой вердикт: ``{args[1]}``");
                if (cfg.plugininterface.english && cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") 
                DiscordLog($"{ConVar.Server.hostname}:\n``connect {cfg.plugininterface.ip}:{ConVar.Server.port}``\n\n``{player.displayName}/{player.userID}`` finished checking the player and delivered his verdict: ``{args[1]}``");
                
                foreach (var item in Check.ToArray())
                {
                    if (item.Value == player.userID)
                    {
                        var suspect = BasePlayer.FindByID(item.Key);
                        if (suspect == null)
                        {
                            Check.Remove(suspect.userID);
                            return;
                        }
                        CancelCheck(suspect, player);
                    }
                }
                CuiHelper.DestroyUi(player, Layer2);
            }
        }

        [ChatCommand("discord")]
        private void SendDiscordCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!Check.ContainsKey(player.userID)) return;
            if (args.Length < 1)
            {
                player.ChatMessage(GetMsg("Send Discord Help", player.UserIDString));
                return;
            }
            if (args.Length > 1)
            {
                player.ChatMessage(GetMsg("Send Discord Error", player.UserIDString));
                return;
            }

            var moderator = BasePlayer.FindByID(Check[player.userID]);
            if (moderator == null)
            {
                player.ChatMessage(GetMsg("Moderator Leave", player.UserIDString));
                CancelCheck(player, null);
                return;
            }
            moderator.ChatMessage(GetMsg("Player Send Discord", moderator.UserIDString).Replace("{0}", args[0].Replace("\"", string.Empty)) + "\n" + GetMsg("Discord Double"));
            moderator.SendConsoleCommand($"echo {GetMsg("Player Send Discord", moderator.UserIDString).Replace("{0}", args[0].Replace("\"", string.Empty))}");

            if (!cfg.plugininterface.english) Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\n{player.displayName}/{player.userID} отправил свой Discord модератору {moderator.displayName}/{moderator.userID}");
            else Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\n{player.displayName}/{player.userID} sent Discord to a moderator {moderator.displayName}/{moderator.userID}");

            if (!cfg.plugininterface.english && cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") 
                DiscordLog($"{ConVar.Server.hostname}:\n``connect {cfg.plugininterface.ip}:{ConVar.Server.port}``\n\n``{player.displayName}/{player.userID}`` отправил свой Discord модератору ``{moderator.displayName}/{moderator.userID}``");
            if (cfg.plugininterface.english && cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") 
                DiscordLog($"{ConVar.Server.hostname}:\n``connect {cfg.plugininterface.ip}:{ConVar.Server.port}``\n\n``{player.displayName}/{player.userID}`` sent Discord to a moderator ``{moderator.displayName}/{moderator.userID}``");
        }

        [ChatCommand("report")]
        private void ReportCommand(BasePlayer player)
        {
            if (player == null) return;
            if (cfg.pluginsettings.alert) player.ChatMessage(GetMsg("Report Command", player.UserIDString));
        }

        [ChatCommand("reports")]
        private void ReportsCommand(BasePlayer player)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, cfg.access.perm))
            {
                player.ChatMessage(GetMsg("No Access", player.UserIDString));
                return;
            }
            DrawUI(player, 0);
        }

        [ConsoleCommand("drspage")]
        private void ChangePage(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (debug) Puts($"{player.displayName} calling method \"drspage\", page = {args.Args[0]}, {int.Parse(args.Args[0]) * 8 <= Players.Count}");
            if (player != null && args.HasArgs(1))
            {
                var page = int.Parse(args.Args[0]);
                if (page * 8 <= Players.Count)
                {
                    DrawUI(player, page);
                }
            }
        }

        [ConsoleCommand("drstest")]
        private void DRSTEST(ConsoleSystem.Arg args)
        {
            if (debug == false) return;
            DMessage("Test bot", "Test bot 2", 76561198155015818, 76561198155015819, "Test title", "Test message");
            //DiscordLog($"{ConVar.Server.hostname}:\n``connect {ConVar.Server.ip}:{ConVar.Server.port}``\n\n``Test/76561111111`` отправил свой Discord модератору ``Test/76561111111``");
            //DiscordMessage($"{ConVar.Server.hostname}:\n``connect {ConVar.Server.ip}:{ConVar.Server.port}``\n\nПоступила новая жалоба на игрока!\nНик игрока отправившего жалобу: ``test``, его SteamID: ``76561111111``\nПодозреваемый: ``baseplayer``, его SteamID: ``76561111111``\n\nЗаголовок жалобы: ``test``, текст жалобы: ``message``");
        }

        [ConsoleCommand("drspages")]
        private void ChangePages(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (debug) Puts($"{player.displayName} calling method \"drspage\", page = {args.Args[1]}, {int.Parse(args.Args[1]) * 8 <= Players[ulong.Parse(args.Args[0])].reportscount}, steamid = {args.Args[0]}");
            if (player != null && args.HasArgs(2))
            {
                var page = int.Parse(args.Args[1]);
                var steamid = ulong.Parse(args.Args[0]);
                if (page * 8 <= Players[steamid].reportscount)
                {
                    DrawPlayerInfo(player, steamid, page);
                }
            }
        }

        [ConsoleCommand("drsclear")]
        private void DeletePlayer(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, cfg.access.perm) || !args.HasArgs(2)) return;
            var id = ulong.Parse(args.Args[1]);
            var name = args.Args[0].Replace("\"", string.Empty);
            DeletePlayerFromDict(id, name);
            CuiHelper.DestroyUi(player, Layer);
            DrawUI(player, 0);

            if (!cfg.plugininterface.english) Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\n{player.displayName}/{player.userID} очистил количество репортов игроку {name}/{id}");
            else Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\n{player.displayName}/{player.userID} cleared the number of reports to the player {name}/{id}");

            if (!cfg.plugininterface.english && cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") 
                DiscordLog($"{ConVar.Server.hostname}:\n``connect {cfg.plugininterface.ip}:{ConVar.Server.port}``\n\n``{player.displayName}/{player.userID}`` очистил количество репортов игроку ``{name}/{id}``");
            if (cfg.plugininterface.english && cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") 
                DiscordLog($"{ConVar.Server.hostname}:\n``connect {cfg.plugininterface.ip}:{ConVar.Server.port}``\n\n``{player.displayName}/{player.userID}`` cleared the number of reports to the player ``{name}/{id}``");
        }

        [ConsoleCommand("drscheck")]
        private void CheckPlayer(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, cfg.access.perm) || !args.HasArgs(1)) return;
            var id = ulong.Parse(args.Args[0]);
            if (id == player.userID) return;
            var suspect = BasePlayer.FindByID(id);
            if (suspect == null)
            {
                player.ChatMessage(GetMsg("Player Not Found", player.UserIDString));
                return;
            }
            if (Check.ContainsValue(player.userID))
            {
                player.ChatMessage(GetMsg("You On Check", player.UserIDString));
                return;
            }
            if (Check.ContainsKey(suspect.userID))
            {
                player.ChatMessage(GetMsg("Player On Check", player.UserIDString));
                return;
            }

            Check.Add(suspect.userID, player.userID);
            AlarmCheckSuspect(suspect);
            AlarmCheckModerator(player);

            if (!cfg.plugininterface.english) Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\n{player.displayName}/{player.userID} начал проверку игрока {suspect.displayName}/{suspect.userID}");
            else Log($"{ConVar.Server.hostname}:\nconnect {cfg.plugininterface.ip}:{ConVar.Server.port}\n\n{player.displayName}/{player.userID} started checking the player {suspect.displayName}/{suspect.userID}");
            
            if (!cfg.plugininterface.english && cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") 
                DiscordLog($"{ConVar.Server.hostname}:\n``connect {cfg.plugininterface.ip}:{ConVar.Server.port}``\n\n``{player.displayName}/{player.userID}`` начал проверку игрока ``{suspect.displayName}/{suspect.userID}``");
            if (cfg.plugininterface.english && cfg.alerts.discordalert && cfg.alerts.logwebhook != "Если не собираетесь использовать логи в канал Discord, не изменяйте это значение") 
                DiscordLog($"{ConVar.Server.hostname}:\n``connect {cfg.plugininterface.ip}:{ConVar.Server.port}``\n\n``{player.displayName}/{player.userID}`` started checking the player ``{suspect.displayName}/{suspect.userID}``");
        }

        [ConsoleCommand("drsbackui")]
        private void UIBack(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, cfg.access.perm)) return;
            DrawUI(player, 0);
        }

        [ConsoleCommand("drsplayerinfo")]
        private void OpenPlayerInfo(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, cfg.access.perm) || !args.HasArgs(1)) return;
            if (Players[ulong.Parse(args.Args[0])].reportscount < 1)
            {
                player.ChatMessage(GetMsg("SteamID", player.UserIDString).Replace("{0}", $"{Players[ulong.Parse(args.Args[0])].name}").Replace("{1}", args.Args[0]));
                player.SendConsoleCommand($"echo {GetMsg("SteamID", player.UserIDString).Replace("{0}", $"{Players[ulong.Parse(args.Args[0])].name}").Replace("{1}", args.Args[0])}");
                return;
            }
            else DrawPlayerInfo(player, ulong.Parse(args.Args[0]), 0);
        }

        #endregion

        #region UI

        private void DrawUI(BasePlayer player, int page)
        {
            if (cfg.plugininterface.sortplayers) Players = Players.OrderByDescending(pair => pair.Value.reportscount).ToDictionary(pair => pair.Key, pair => pair.Value);
            if (debug) Puts($"DrawUI: {player.displayName}, {page}");
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true,
            }, "Overlay", Layer);
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#181B18DB"), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1",}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".MainContainer",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#9C95956D") },
                    new CuiRectTransformComponent {AnchorMin = "0.265625 0.08888885", AnchorMax = "0.734375 0.8222222"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".MainContainer",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#9C95952E"), Material = "assets/content/ui/uibackgroundblur.mat" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".MainContainer",
                Name = Layer + ".Top",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#484848AD")},
                    new CuiRectTransformComponent {AnchorMin = "0 0.8787879", AnchorMax = "1 1",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".MainContainer",
                Name = Layer + ".Bottom",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#484848AD")},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0.999 0.1212172",}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Top",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = cfg.plugininterface.english ? "ALL REPORTS" : "ВСЕ ЖАЛОБЫ", Align = TextAnchor.UpperLeft, FontSize = 18, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.05333334 0.1979178", AnchorMax = "0.3711112 0.7187503"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Top",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#9A9A9AFF"), Text = cfg.plugininterface.english ? "WHO WILL WE CHECK TODAY? • CLICK ON THE PLAYER'S AVATAR TO SEE THE LIST OF REPORTS" : "КОГО БУДЕМ ПРОВЕРЯТЬ СЕГОДНЯ? • НАЖМИТЕ НА АВАТАР ИГРОКА ЧТОБЫ УВИДЕТЬ СПИСОК ЖАЛОБ", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.05444445 0.1979178", AnchorMax = "0.9988889 0.4270831"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Bottom",
                Name = Layer + ".Bottom" + ".CloseButton",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#FF0000B8")},
                    new CuiRectTransformComponent {AnchorMin = "0.8188888 0.4374651", AnchorMax = "0.9466667 0.6978856",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Bottom" + ".CloseButton",
                Components =
                {
                    new CuiRawImageComponent {Color = "1 1 1 0.6", Png = (string) ImageLibrary.CallHook("GetImage", "closeicon")},
                    new CuiRectTransformComponent {AnchorMin = "0.02608692 0.08000004", AnchorMax = "0.2086954 0.9200005"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Bottom" + ".CloseButton",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = cfg.plugininterface.english ? "CLOSE" : "ЗАКРЫТЬ", Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.1217395 0.08000004", AnchorMax = "1 0.8799995"}
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Close = Layer, Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer + ".Bottom" + ".CloseButton");


            container.Add(new CuiElement
            {
                Parent = Layer + ".Bottom",
                Name = Layer + ".Bottom" + ".Pages",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#FFFFFF32")},
                    new CuiRectTransformComponent {AnchorMin = "0.03777974 0.3958336", AnchorMax = "0.1933326 0.7395837",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Bottom" + ".Pages",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = $"{page + 1}/{(int)Math.Ceiling(Convert.ToDecimal(Players.Count) / 8)}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Bottom" + ".Pages",
                Name = Layer + ".Bottom" + ".Pages" + ".Left",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#74FF008A")},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0.2357184 0.9696969",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Bottom" + ".Pages" + ".Left",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = "<", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".Bottom" + ".Pages",
                Name = Layer + ".Bottom" + ".Pages" + ".Right",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#74FF008A")},
                    new CuiRectTransformComponent {AnchorMin = "0.7500126 0", AnchorMax = "0.9857317 0.9696969",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Bottom" + ".Pages" + ".Right",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = ">", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            if (page + 1 != 1)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"drspage {page - 1}", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, Layer + ".Bottom" + ".Pages" + ".Left");
            }
            if (debug) Puts($"{Players.Count / 8 != page}");
            if (Players.Count / 8 != page)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"drspage {page + 1}", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, Layer + ".Bottom" + ".Pages" + ".Right");
            }

            const double startAnMinX = 0.03333334;
            const double startAnMaxX = 0.4888889;
            const double startAnMinY = 0.6641415;
            const double startAnMaxY = 0.7904055;
            double anMinX = startAnMinX;
            double anMaxX = startAnMaxX;
            double anMinY = startAnMinY;
            double anMaxY = startAnMaxY;

            Dictionary<ulong, player> dict = Players.Skip(8 * page).Take(8).ToDictionary(pair => pair.Key, pair => pair.Value);
            for (int i = 0; i < dict.Count(); i++)
            {
                var suspectid = dict.Keys.ToList()[i];
                var suspectvalue = dict.Values.ToList()[i];
                if (suspectvalue == null || suspectvalue.name == null || suspectvalue.Reports == null) continue;

                if ((i != 0) && (i % 2 == 0))
                {
                    anMinX = startAnMinX;
                    anMaxX = startAnMaxX;
                    anMinY -= 0.151512;
                    anMaxY -= 0.151512;
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".MainContainer",
                    Name = Layer + $".{suspectid}",
                    Components =
                        {
                            new CuiImageComponent {Color = HexToRustFormat("#484848AD")},
                            new CuiRectTransformComponent {
                                AnchorMin = $"{anMinX} {anMinY}",
                                AnchorMax = $"{anMaxX} {anMaxY}" }
                        }
                });
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{suspectid}",
                    Name = Layer + $".{suspectid}" + ".Avatar",
                    Components =
                        {
                            new CuiRawImageComponent {Png = (string)ImageLibrary.Call("GetImage", $"{suspectid}") },
                            new CuiRectTransformComponent {AnchorMin = "0.009756097 0.03999959", AnchorMax = "0.2341463 0.9599901"}
                        }
                });
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Close = Layer, Command = $"drsplayerinfo {suspectid}", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, Layer + $".{suspectid}" + ".Avatar");
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{suspectid}",
                    Components =
                        {
                            new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = $"{(IsOnline(suspectid) ? "<color=#3ee332>•</color>" : "<color=#e33232>•</color>")} {CheckText(22, suspectvalue.name.ToUpper())}", Align = TextAnchor.UpperLeft, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                            new CuiRectTransformComponent {AnchorMin = "0.2634146 0.6599931", AnchorMax = "1 0.9199904"}
                        }
                });
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{suspectid}",
                    Components =
                        {
                            new CuiTextComponent { Color = HexToRustFormat("#9A9A9AFF"), Text = cfg.plugininterface.english ? $"NUMBER OF REPORTS: {suspectvalue.reportscount}" : $"КОЛИЧЕСТВО ЖАЛОБ: {suspectvalue.reportscount}", Align = TextAnchor.UpperLeft, FontSize = 10, Font = "RobotoCondensed-bold.ttf" },
                            new CuiRectTransformComponent {AnchorMin = "0.2634146 0.05999869", AnchorMax = "1 0.659993"}
                        }
                });

                if (!permission.UserHasPermission(player.UserIDString, cfg.access.clearperm))
                {
                    if (IsOnline(suspectid))
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + $".{suspectid}",
                            Name = Layer + $".{suspectid}" + ".Check",
                            Components =
                                {
                                    new CuiImageComponent {Color = HexToRustFormat("#145C89FF")},
                                    new CuiRectTransformComponent {AnchorMin = "0.7487805 0.02999946", AnchorMax = "0.9926829 0.249997",}
                                }
                        });
                        container.Add(new CuiElement
                        {
                            Parent = Layer + $".{suspectid}" + ".Check",
                            Components =
                                {
                                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = cfg.plugininterface.english ? "CHECK" : "ПРОВЕРИТЬ", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "RobotoCondensed-bold.ttf" },
                                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                                }
                        });
                        if (suspectid != player.userID)
                        {
                            container.Add(new CuiButton
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                                Button = { Command = $"drscheck {suspectid}", Color = "0 0 0 0" },
                                Text = { Text = "" }
                            }, Layer + $".{suspectid}" + ".Check");
                        }
                    }
                }
                else
                {
                    if (IsOnline(suspectid))
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + $".{suspectid}",
                            Name = Layer + $".{suspectid}" + ".Check",
                            Components =
                                {
                                    new CuiImageComponent {Color = HexToRustFormat("#145C89FF")},
                                    new CuiRectTransformComponent {AnchorMin = "0.4926832 0.02999946", AnchorMax = "0.7365856 0.249997",}
                                }
                        });
                        container.Add(new CuiElement
                        {
                            Parent = Layer + $".{suspectid}" + ".Check",
                            Components =
                                {
                                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = cfg.plugininterface.english ? "CHECK" : "ПРОВЕРИТЬ", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "RobotoCondensed-bold.ttf" },
                                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                                }
                        });
                        if (suspectid != player.userID)
                        {
                            container.Add(new CuiButton
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                                Button = { Close = Layer, Command = $"drscheck {suspectid}", Color = "0 0 0 0" },
                                Text = { Text = "" }
                            }, Layer + $".{suspectid}" + ".Check");
                        }
                    }

                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{suspectid}",
                        Name = Layer + $".{suspectid}" + ".Remove",
                        Components =
                        {
                            new CuiImageComponent {Color = HexToRustFormat("#993D3DFF")},
                            new CuiRectTransformComponent {AnchorMin = "0.7487805 0.02999946", AnchorMax = "0.9926829 0.249997",}
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{suspectid}" + ".Remove",
                        Components =
                        {
                            new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = cfg.plugininterface.english ? "CLEAR" : "ОЧИСТИТЬ", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "RobotoCondensed-bold.ttf" },
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                    });
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Close = Layer, Command = $"drsclear \"{suspectvalue.name}\" {suspectid}", Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, Layer + $".{suspectid}" + ".Remove");
                }

                anMinX += 0.47777796;
                anMaxX += 0.47777796;
            }

            CuiHelper.AddUi(player, container);
        }

        private void DrawPlayerInfo(BasePlayer player, ulong steamid, int pages)
        {
            CuiHelper.DestroyUi(player, Layer3);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true,
            }, "Overlay", Layer3);
            container.Add(new CuiElement
            {
                Parent = Layer3,
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#181B18DB"), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1",}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer3,
                Name = Layer3 + ".MainContainer",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#9C95956D") },
                    new CuiRectTransformComponent {AnchorMin = "0.265625 0.08888885", AnchorMax = "0.734375 0.8222222"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".MainContainer",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#9C95952E"), Material = "assets/content/ui/uibackgroundblur.mat" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".MainContainer",
                Name = Layer3 + ".Top",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#484848AD")},
                    new CuiRectTransformComponent {AnchorMin = "0 0.8787879", AnchorMax = "1 1",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".MainContainer",
                Name = Layer3 + ".Bottom",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#484848AD")},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0.999 0.1212172",}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Top",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = $"{Players[steamid].name.ToUpper()} • {steamid}", Align = TextAnchor.UpperLeft, FontSize = 18, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.05333334 0.1979178", AnchorMax = "0.8911111 0.7187503"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Top",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#9A9A9AFF"), Text = cfg.plugininterface.english ? "LIST OF REPORTS ABOUT THIS PLAYER" : "СПИСОК ЖАЛОБ НА ЭТОГО ИГРОКА", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.05444445 0.1979178", AnchorMax = "0.9988889 0.4270831"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Bottom",
                Name = Layer3 + ".Bottom" + ".CloseButton",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#FF0000B8")},
                    new CuiRectTransformComponent {AnchorMin = "0.8188888 0.4374651", AnchorMax = "0.9466667 0.6978856",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Bottom" + ".CloseButton",
                Components =
                {
                    new CuiRawImageComponent {Color = "1 1 1 0.6", Png = (string) ImageLibrary.CallHook("GetImage", "backicon")},
                    new CuiRectTransformComponent {AnchorMin = "0.02608692 0.08000004", AnchorMax = "0.2086954 0.9200005"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Bottom" + ".CloseButton",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = cfg.plugininterface.english ? "BACK" : "НАЗАД", Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.1217395 0.08000004", AnchorMax = "1 0.8799995"}
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Close = Layer3, Command = "drsbackui", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer3 + ".Bottom" + ".CloseButton");


            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Bottom",
                Name = Layer3 + ".Bottom" + ".Pages",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#FFFFFF32")},
                    new CuiRectTransformComponent {AnchorMin = "0.03777974 0.3958336", AnchorMax = "0.1933326 0.7395837",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Bottom" + ".Pages",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = $"{pages + 1}/{(int)Math.Ceiling(Convert.ToDecimal(Players[steamid].reportscount) / 8)}", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Bottom" + ".Pages",
                Name = Layer3 + ".Bottom" + ".Pages" + ".Left",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#74FF008A")},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0.2357184 0.9696969",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Bottom" + ".Pages" + ".Left",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = "<", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Bottom" + ".Pages",
                Name = Layer3 + ".Bottom" + ".Pages" + ".Right",
                Components =
                {
                    new CuiImageComponent {Color = HexToRustFormat("#74FF008A")},
                    new CuiRectTransformComponent {AnchorMin = "0.7500126 0", AnchorMax = "0.9857317 0.9696969",}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer3 + ".Bottom" + ".Pages" + ".Right",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = ">", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            if (pages + 1 != 1)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"drspages {steamid} {pages - 1}", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, Layer3 + ".Bottom" + ".Pages" + ".Left");
            }
            if (Players[steamid].reportscount / 8 == pages + 1)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"drspages {steamid} {pages + 1}", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, Layer3 + ".Bottom" + ".Pages" + ".Right");
            }

            const double startAnMinX = 0.03333334;
            const double startAnMaxX = 0.4888889;
            const double startAnMinY = 0.6641415;
            const double startAnMaxY = 0.7904055;
            double anMinX = startAnMinX;
            double anMaxX = startAnMaxX;
            double anMinY = startAnMinY;
            double anMaxY = startAnMaxY;

            List<report> list = Players[steamid].Reports.SelectMany(x => x.Value).Skip(8 * pages).Take(8).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var reportedplayerid = list[i].steamid;
                var reportedplayername = list[i].name;

                if ((i != 0) && (i % 2 == 0))
                {
                    anMinX = startAnMinX;
                    anMaxX = startAnMaxX;
                    anMinY -= 0.151512;
                    anMaxY -= 0.151512;
                }

                container.Add(new CuiElement
                {
                    Parent = Layer3 + ".MainContainer",
                    Name = Layer3 + $".{i}",
                    Components =
                            {
                                new CuiImageComponent {Color = HexToRustFormat("#484848AD")},
                                new CuiRectTransformComponent {
                                    AnchorMin = $"{anMinX} {anMinY}",
                                    AnchorMax = $"{anMaxX} {anMaxY}" }
                            }
                });
                container.Add(new CuiElement
                {
                    Parent = Layer3 + $".{i}",
                    Components =
                            {
                                new CuiRawImageComponent {Png = (string)ImageLibrary.Call("GetImage", $"{reportedplayerid}") },
                                new CuiRectTransformComponent {AnchorMin = "0.009756097 0.03999959", AnchorMax = "0.2341463 0.9599901"}
                            }
                });
                container.Add(new CuiElement
                {
                    Parent = Layer3 + $".{i}",
                    Components =
                            {
                                new CuiTextComponent { Color = HexToRustFormat("#DDDDDDFF"), Text = $"{(IsOnline(reportedplayerid) ? "<color=#3ee332>•</color>" : "<color=#e33232>•</color>")} {reportedplayername.ToUpper()}", Align = TextAnchor.UpperLeft, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                                new CuiRectTransformComponent {AnchorMin = "0.2634146 0.6599931", AnchorMax = "1 0.9199904"}
                            }
                });
                container.Add(new CuiElement
                {
                    Parent = Layer3 + $".{i}",
                    Components =
                            {
                                new CuiTextComponent { Color = HexToRustFormat("#9A9A9AFF"), Text = cfg.plugininterface.english ? $"SUBJECT: {CheckText(24, list[i].subject.ToUpper())}\nMESSAGE: {CheckText(38, list[i].message.ToUpper())}" : $"ЗАГОЛОВОК: {CheckText(24, list[i].subject.ToUpper())}\nТЕКСТ ЖАЛОБЫ: {CheckText(38, list[i].message.ToUpper())}", Align = TextAnchor.UpperLeft, FontSize = 10, Font = "RobotoCondensed-bold.ttf" },
                                new CuiRectTransformComponent {AnchorMin = "0.2634146 0.05999869", AnchorMax = "1 0.659993"}
                            }
                });

                anMinX += 0.47777796;
                anMaxX += 0.47777796;
            }

            CuiHelper.AddUi(player, container);
        }

        private void DrawNotify(BasePlayer player, string text)
        {
            CuiHelper.DestroyUi(player, Layer2);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.75" },
                RectTransform = { AnchorMin = "0.5 0.755548", AnchorMax = "0.5 0.755548", OffsetMin = "-640 -60", OffsetMax = "640 60" },
                CursorEnabled = false,
            }, "Overlay", Layer2);

            container.Add(new CuiElement
            {
                Parent = Layer2,
                Components =
                {
                    new CuiTextComponent { Text = text, Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Helpers

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
    }
}