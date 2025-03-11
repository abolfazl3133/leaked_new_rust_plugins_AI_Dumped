using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("SendVideo", "ArtiOM", "1.2.0")]
    [Description("A simple plugin that allows you to easily send an mp4 recording to yourself, a selected player or all players on the server.")]
    internal class SendVideo : RustPlugin
    {
        private DataFileSystem dataFile = new DataFileSystem($"{Interface.Oxide.DataDirectory}");
        private class StoredVideo
        {
            public Dictionary<string, string> MP4_Links = new Dictionary<string, string>();

            public StoredVideo()
            {

            }

            public string GetLink(string key)
            {
                return MP4_Links[key];
            } 
        }
        private StoredVideo storedVideo = new StoredVideo();
        //Dodać rozdzielność uprawnien do każdej z osobna (all/to/me)
        //Uprawnienie usePerm pozwala na używanie tylko nagrań z listy
        #region Config
        private static ConfigData config;

        private class PermissionSettings
        {
            [JsonProperty(PropertyName = "Name of permission for admins")]
            public string AdminPerm = "sendvideo.admin";
            
            [JsonProperty(PropertyName = "Name of permission for players who can use this plugin")]
            public string UsePerm = "sendvideo.use";
            
            [JsonProperty(PropertyName = "Name of permission for players who never get video from this plugin")]
            public string BypassPerm = "sendvideo.bypass";
        }
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Permission Settings")]
            public PermissionSettings permissionSettings = new PermissionSettings();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                permissionSettings = new PermissionSettings()
            };
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion


        void Init()
        {
            if (!permission.PermissionExists(config.permissionSettings.AdminPerm))
            {
                permission.RegisterPermission(config.permissionSettings.AdminPerm, this);
                permission.RegisterPermission(config.permissionSettings.UsePerm, this);
                permission.RegisterPermission(config.permissionSettings.BypassPerm, this);

                //Dodajemy uprawniwnia admina automatycznie do grupy domyślnej dla adminów
                permission.GrantGroupPermission("admin", config.permissionSettings.AdminPerm, this);
            }

            if (dataFile.ExistsDatafile("MP4_Videos"))
            {
                storedVideo = dataFile.ReadObject<StoredVideo>("MP4_Videos");
            }
            else
            {
                Puts("New file MP4_Videos created in [oxide/data/MP4_Videos.json]");
                dataFile.WriteObject<StoredVideo>("MP4_Videos", storedVideo);
            }
        }

        private void Unload()
        {
            dataFile.WriteObject<StoredVideo>("MP4_Videos", storedVideo);
        }

        [ConsoleCommand("sendvideo")]
        void sendVideoConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer play = arg.Player();
            if(play == null)
            {
                if (arg.Args.IsNullOrEmpty())
                {
                    Puts("Use: sendvideo [all] [link]");
                    Puts("Use: sendvideo to [PlayerName/ID] [link]");
                    Puts("Use: sendvideo list");
                    Puts("Use: sendvideo add [VideoName] [link]");
                    Puts("Use: sendvideo remove [VideoName]");

                    return;
                }

                string[] args = arg.Args;

                if (args.Length == 1)
                {
                    if (args[0] != "list")
                    {
                        Puts("Use: sendvideo [all] [link]");
                        Puts("Use: sendvideo to [PlayerName/ID] [link]");
                        Puts("Use: sendvideo list");
                        Puts("Use: sendvideo add [VideoName] [link]");
                        Puts("Use: sendvideo remove [VideoName]");

                        return;
                    }
                }

                switch (args[0])
                {
                    case "to":
                        {
                            var toPlayer = BasePlayer.Find(args[1]);
                            if (toPlayer != null)
                            {
                                if (!IsMP4(args[2]))
                                {
                                    if (args[2].Contains("http"))
                                    {
                                        Puts("Link to video is wrong!");
                                        return;
                                    }
                                    else if (storedVideo.GetLink(args[2]) == null)
                                    {
                                        Puts("Video name is wrong!");
                                        return;
                                    }
                                    else
                                    {
                                        args[2] = storedVideo.GetLink(args[2]);
                                    }
                                }

                                if (!permission.UserHasPermission(toPlayer.UserIDString, config.permissionSettings.BypassPerm))
                                {
                                    toPlayer.Command("client.playvideo", args[2]);
                                    Puts($"You send video to player {toPlayer.displayName}.");
                                }
                                else
                                {
                                    Puts($"You can't send video to player {toPlayer.displayName}.");
                                }
                            }
                            else
                            {
                                Puts("Wrong player name!");
                            }
                        }
                        break;

                    case "all":
                        {
                            if (!IsMP4(args[1]))
                            {
                                if (args[1].Contains("http"))
                                {
                                    Puts("Link to video is wrong!");
                                    return;
                                }
                                else if (storedVideo.GetLink(args[1]) == null)
                                {
                                    Puts("Video name is wrong!");
                                    return;
                                }
                                else
                                {
                                    args[1] = storedVideo.GetLink(args[1]);
                                }
                            }

                            foreach (BasePlayer pla in BasePlayer.activePlayerList)
                            {
                                if (permission.UserHasPermission(pla.UserIDString, config.permissionSettings.BypassPerm))
                                    continue;

                                pla.Command("client.playvideo", args[1]);
                            }

                            Puts($"You send video to all players.");
                        }
                        break;

                    case "add":
                        {
                            if (args.Length == 2 && !IsMP4(args[2]))
                            {
                                Puts("Link to video is wrong!");
                                Puts("Use: /sendvideo add [VideoName] [link]");
                                return;
                            }

                            storedVideo.MP4_Links.Add(args[1], args[2]);
                            Puts($"Link added to list, named as {args[1]}");
                        }
                        break;

                    case "remove":
                        {
                            if (storedVideo.MP4_Links.Remove(args[1]))
                            {
                                Puts($"Link removed from list, named as {args[1]}");
                            }
                            else
                            {
                                Puts("Video name is wrong!");
                                Puts("Use: /sendvideo remove [VideoName]");
                            }
                        }
                        break;

                    case "list":
                        {
                            if(storedVideo.MP4_Links.Count < 1)
                            {
                                Puts($"List is empty!");
                                break;
                            }

                            Puts($"List of saved videos:");
                            Puts($"{"Video Name".PadRight(15)}| Link To MP4");

                            string list = "\n";

                            foreach (KeyValuePair<string, string> vid in storedVideo.MP4_Links)
                            {
                                list += $"{vid.Key.PadRight(15)}| {vid.Value}\n";
                            }

                            Puts(list);
                        }
                        break;

                    default:
                        {
                            Puts("Use: sendvideo [all] [link]");
                            Puts("Use: sendvideo to [PlayerName/ID] [link]");
                            Puts("Use: sendvideo list");
                            Puts("Use: sendvideo add [VideoName] [link]");
                            Puts("Use: sendvideo remove [VideoName]");
                        }
                        break;
                }
            }
            else
            {
                sendVideoChatCommand(play, "sendvideo", arg.Args);
            }
        }

        [ChatCommand("sendvideo")]
        void sendVideoChatCommand(BasePlayer player, string command, string[] args)
        {
            //Jeżeli gracz nie posiada admina lub nie ma przypisanego uprawnienia nie może używać tej komendy
            if (!permission.UserHasPermission(player.UserIDString, config.permissionSettings.AdminPerm)
                && !permission.UserHasPermission(player.UserIDString, config.permissionSettings.UsePerm))
                return;
            //Jeżeli gracz nie podał żadnych argumentów
            if (args.IsNullOrEmpty())
            {
                player.ChatMessage("Use: /sendvideo [me/all] [link]");
                player.ChatMessage("Use: /sendvideo to [PlayerName/ID] [link]");
                player.ChatMessage("Use: /sendvideo list");

                if(permission.UserHasPermission(player.UserIDString, config.permissionSettings.AdminPerm))
                {
                    player.ChatMessage("\nUse: /sendvideo add [VideoName] [link]");
                    player.ChatMessage("Use: /sendvideo remove [VideoName]");
                }
                return;
            }

            if (args.Length == 1)
            {
                if (args[0] != "list" && args[0] != "close")
                {
                    player.ChatMessage("Use: /sendvideo [me/all] [link]");
                    player.ChatMessage("Use: /sendvideo to [PlayerName/ID] [link]");
                    player.ChatMessage("Use: /sendvideo list");

                    if (permission.UserHasPermission(player.UserIDString, config.permissionSettings.AdminPerm))
                    {
                        player.ChatMessage("\nUse: /sendvideo add [VideoName] [link]");
                        player.ChatMessage("Use: /sendvideo remove [VideoName]");
                    }
                    return;
                }
            }

            switch (args[0])
            {
                case "me":
                    {
                        if (!IsMP4(args[1]))
                        {
                            if (args[1].Contains("http"))
                            {
                                player.ChatMessage("Link to video is wrong!");
                                return;
                            }
                            else if (storedVideo.GetLink(args[1]) == null)
                            {
                                player.ChatMessage("Video name is wrong!");
                                return;
                            }
                            else
                            {
                                args[1] = storedVideo.GetLink(args[1]);
                            }
                        }

                        player.Command("client.playvideo", args[1]);
                    }
                    break;

                case "to":
                    {
                        var toPlayer = BasePlayer.Find(args[1]);
                        if ( toPlayer != null )
                        {

                            if (!IsMP4(args[2]))
                            {
                                if (args[2].Contains("http"))
                                {
                                    player.ChatMessage("Link to video is wrong!");
                                    return;
                                }
                                else if (storedVideo.GetLink(args[2]) == null)
                                {
                                    player.ChatMessage("Video name is wrong!");
                                    return;
                                }
                                else
                                {
                                    args[2] = storedVideo.GetLink(args[2]);
                                }
                            }

                            //Jeżeli gracz posiada uprawnienie do omijania wysyłanych filmów i nie wysyła ich admin, nie otrzyma on nagrania.
                            if (!permission.UserHasPermission(toPlayer.UserIDString, config.permissionSettings.BypassPerm))
                            {
                                toPlayer.Command("client.playvideo", args[2]);
                                player.ChatMessage($"You send video to player {toPlayer.displayName}.");
                            }
                            else
                            {
                                player.ChatMessage($"You can't send video to player {toPlayer.displayName}.");
                            }
                        }
                        else
                        {
                            player.ChatMessage("Wrong player name!");
                        }
                    }
                    break;

                case "all":
                    {
                        if (!IsMP4(args[1]))
                        {
                            if (args[1].Contains("http"))
                            {
                                player.ChatMessage("Link to video is wrong!");
                                return;
                            }
                            else if (storedVideo.GetLink(args[1]) == null) 
                            {
                                player.ChatMessage("Video name is wrong!");
                                return;
                            }
                            else
                            {
                                args[1] = storedVideo.GetLink(args[1]);
                            }
                        }

                        foreach(BasePlayer pla in BasePlayer.activePlayerList)
                        {
                            //Pomija graczy z permisja pomijania filmów.
                            if (permission.UserHasPermission(pla.UserIDString, config.permissionSettings.BypassPerm))
                                continue;

                            if (player == pla)
                                continue;

                            pla.Command("client.playvideo", args[1]);
                        }
                        player.ChatMessage($"You send video to all players.");
                    }
                    break;

                case "add":
                    {
                        if (!permission.UserHasPermission(player.UserIDString, config.permissionSettings.AdminPerm))
                            return;

                        if (args.Length <= 3 && !IsMP4(args[2]))
                        {
                            player.ChatMessage("Link to video is wrong!");
                            player.ChatMessage("Use: /sendvideo add [VideoName] [link]");
                            return;
                        }

                        storedVideo.MP4_Links.Add(args[1], args[2]);
                        player.ChatMessage($"Link added to list, named as {args[1]}");
                    }
                    break;

                case "remove":
                    {
                        if (!permission.UserHasPermission(player.UserIDString, config.permissionSettings.AdminPerm))
                            return;

                        if (storedVideo.MP4_Links.Remove(args[1]))
                        {
                            player.ChatMessage($"Link removed from list, named as {args[1]}");
                        }
                        else
                        {
                            player.ChatMessage("Video name is wrong!");
                            player.ChatMessage("Use: /sendvideo remove [VideoName]");
                        }
                    }
                    break;

                case "list":
                    {
                        if (storedVideo.MP4_Links.Count < 1)
                        {
                            player.ChatMessage($"List is empty!");
                            break;
                        }

                        player.ChatMessage($"List send to console(F1)!");
                        player.ConsoleMessage($"List of saved videos: \n{"Video Name ".PadRight(15)}| Link to mp4");
                        string list = string.Empty;

                        foreach (KeyValuePair<string, string> vid in storedVideo.MP4_Links )
                        {
                            list += $"{vid.Key.PadRight(15)}| {vid.Value}\n";
                        }

                        player.ConsoleMessage(list);
                    }
                    break;

                default:
                    {
                        player.ChatMessage("Use: /sendvideo [me/all] [link]");
                        player.ChatMessage("Use: /sendvideo to [PlayerName/ID] [link]");
                        player.ChatMessage("Use: /sendvideo list");

                        if (permission.UserHasPermission(player.UserIDString, config.permissionSettings.AdminPerm))
                        {
                            player.ChatMessage("\nUse: /sendvideo add [VideoName] [link]");
                            player.ChatMessage("Use: /sendvideo remove [VideoName]");
                        }
                    }
                    break;
            }
        }

        [ConsoleCommand("sv.close")]
        void closeVideoConsoleCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            
            if(player != null)
            {
                player.Command("client.playvideo", "https://megawrzuta.pl/files/f199a5d44228f79bb633e27238ddd94e.mp4");
            }
        }

        #region Local Functions
        private bool IsMP4(string link)
        {
            if (link.Contains(".mp4"))
                return true;

            return false;
        }
        #endregion
    }
}
