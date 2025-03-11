using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using Network;
using System.Web;

namespace Oxide.Plugins
{
    [Info("ZAdminVideo", "Zwe1sr", "1.0.0")]
    [Description("Позволяет администраторам проигрывать видео для игроков")]
    public class ZAdminVideo : RustPlugin
    {
        #region Fields
        private const string PermissionUse = "adminvideo.use";
        private Dictionary<ulong, string> ActiveVideos = new Dictionary<ulong, string>();
        private Timer videoTimer;
        #endregion

        #region Configuration
        private ConfigData config;

        class ConfigData
        {
            [JsonProperty("Максимальная длительность видео (в секундах)")]
            public float MaxVideoDuration = 300f;

            [JsonProperty("Размер видео")]
            public VideoSize VideoSize = new VideoSize();
            
            [JsonProperty("YouTube API Key")]
            public string YouTubeApiKey = "YOUR_API_KEY_HERE";
        }

        class VideoSize
        {
            [JsonProperty("Ширина (0.0-1.0)")]
            public float Width = 0.8f;
            
            [JsonProperty("Высота (0.0-1.0)")]
            public float Height = 0.6f;
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void LoadConfig()
        {
            config = Config.ReadObject<ConfigData>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            cmd.AddChatCommand("video", this, nameof(CmdVideo));
            cmd.AddChatCommand("stopvideo", this, nameof(CmdStopVideo));
            LoadDefaultMessages();
        }

        void OnServerInitialized(bool initial)
        {
            LoadConfig();
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (ActiveVideos.ContainsKey(player.userID))
            {
                ActiveVideos.Remove(player.userID);
                DestroyUI(player);
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "У вас нет прав на использование этой команды!",
                ["Usage"] = "Использование: /video <URL> [имя_игрока/steamID]",
                ["PlayerNotFound"] = "Игрок не найден!",
                ["InvalidUrl"] = "Неверный URL видео!",
                ["VideoStarted"] = "Видео запущено для игрока {0}",
                ["VideoStopped"] = "Видео остановлено для игрока {0}",
                ["YouTubeError"] = "Ошибка при обработке YouTube видео!"
            }, this, "ru");
        }
        #endregion

        #region Commands
        [Command("video")]
        private void CmdVideo(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                Message(player, "NoPermission");
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "Usage");
                return;
            }

            string url = args[0];
            BasePlayer targetPlayer = null;

            if (args.Length > 1)
            {
                targetPlayer = BasePlayer.Find(args[1]);
                if (targetPlayer == null)
                {
                    Message(player, "PlayerNotFound");
                    return;
                }
            }
            else
            {
                targetPlayer = player;
            }

            if (!IsValidUrl(url))
            {
                Message(player, "InvalidUrl");
                return;
            }

            ShowVideo(targetPlayer, url);
            Message(player, "VideoStarted", targetPlayer.displayName);
        }

        [Command("stopvideo")]
        private void CmdStopVideo(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                Message(player, "NoPermission");
                return;
            }

            BasePlayer targetPlayer = null;

            if (args.Length > 0)
            {
                targetPlayer = BasePlayer.Find(args[0]);
                if (targetPlayer == null)
                {
                    Message(player, "PlayerNotFound");
                    return;
                }
            }
            else
            {
                targetPlayer = player;
            }

            StopVideo(targetPlayer);
            Message(player, "VideoStopped", targetPlayer.displayName);
        }

        private void Message(BasePlayer player, string key, params object[] args)
        {
            string message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
            SendReply(player, message);
        }
        #endregion

        #region Core Methods
        private void ShowVideo(BasePlayer player, string url)
        {
            if (ActiveVideos.ContainsKey(player.userID))
            {
                DestroyUI(player);
            }

            if (IsYouTubeUrl(url))
            {
                ProcessYouTubeUrl(player, url);
                return;
            }

            ShowDirectVideo(player, url);
        }

        private void ProcessYouTubeUrl(BasePlayer player, string url)
        {
            string videoId = ExtractYouTubeVideoId(url);
            if (string.IsNullOrEmpty(videoId))
            {
                Message(player, "InvalidUrl");
                return;
            }

            webrequest.Enqueue(
                $"https://www.googleapis.com/youtube/v3/videos?id={videoId}&part=contentDetails&key={config.YouTubeApiKey}",
                "", (code, response) =>
                {
                    if (code != 200)
                    {
                        Message(player, "YouTubeError");
                        return;
                    }

                    try
                    {
                        var directUrl = $"https://www.youtube.com/embed/{videoId}?autoplay=1&controls=0";
                        ShowDirectVideo(player, directUrl);
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Error processing YouTube video: {ex.Message}");
                        Message(player, "YouTubeError");
                    }
                }, this);
        }

        private void ShowDirectVideo(BasePlayer player, string url)
        {
            ActiveVideos[player.userID] = url;

            var elements = new CuiElementContainer();
            
            elements.Add(new CuiElement
            {
                Parent = "Overlay",
                Components = 
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{(1 - config.VideoSize.Width) / 2} {(1 - config.VideoSize.Height) / 2}",
                        AnchorMax = $"{(1 + config.VideoSize.Width) / 2} {(1 + config.VideoSize.Height) / 2}"
                    },
                    new CuiRawImageComponent
                    {
                        Url = url,
                        Color = "1 1 1 1"
                    }
                },
                Name = $"VideoPlayer_{player.userID}"
            });

            CuiHelper.AddUi(player, elements);

            if (videoTimer != null && !videoTimer.Destroyed)
            {
                videoTimer.Destroy();
            }

            videoTimer = timer.Once(config.MaxVideoDuration, () => StopVideo(player));
        }

        private bool IsYouTubeUrl(string url)
        {
            return url.Contains("youtube.com") || url.Contains("youtu.be");
        }

        private string ExtractYouTubeVideoId(string url)
        {
            try
            {
                if (url.Contains("youtu.be"))
                {
                    return url.Split(new[] { "youtu.be/" }, StringSplitOptions.None)[1].Split('?')[0];
                }
                
                if (url.Contains("youtube.com/watch"))
                {
                    var uri = new Uri(url);
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    return query["v"];
                }
                
                if (url.Contains("youtube.com/embed"))
                {
                    return url.Split(new[] { "youtube.com/embed/" }, StringSplitOptions.None)[1].Split('?')[0];
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        private void StopVideo(BasePlayer player)
        {
            if (ActiveVideos.ContainsKey(player.userID))
            {
                DestroyUI(player);
                ActiveVideos.Remove(player.userID);
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, $"VideoPlayer_{player.userID}");
        }

        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) 
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
        #endregion
    }
} 