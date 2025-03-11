using System;
using System.Collections.Generic;
using System.Drawing;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Teleport Menu", "Orange", "1.1.1")]
    [Description("https://rustworkshop.space/resources/teleport-menu.99/")]
    public class TeleportMenu : RustPlugin
    {
        #region Vars

        private const string elemMain = "teleportMenu.main";
        private static Dictionary<ulong, List<PlayerEntry>> friends = new Dictionary<ulong, List<PlayerEntry>>();
        private static List<ulong> uiOpened = new List<ulong>();
        [PluginReference] private Plugin NTeleportation;

        private class ButtonEntry
        {
            public string name;
            public string url;
            public string command;
        }

        private class PlayerEntry
        {
            public string name;
            public ulong id;
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            cmd.AddChatCommand(config.command, this, nameof(cmdControlChat));
            cmd.AddConsoleCommand(config.command, this, nameof(cmdControlConsole));
        }

        private void Loaded()
        {
            AddImage(config.teleportUrl, config.teleportUrl);
            AddImage(config.acceptUrl, config.acceptUrl);
            AddImage(config.cancelUrl, config.cancelUrl);
            AddImage(config.sethomeUrl, config.sethomeUrl);
            AddImage(config.removeHomeUrl, config.removeHomeUrl);
            AddImage(config.homeUrl, config.homeUrl);
        }

        private void OnServerInitialized()
        {
            if (config.cacheTime > 0)
            {
                timer.Every(config.cacheTime, () => {friends.Clear();});
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (uiOpened.Contains(player.userID))
            {
                uiOpened.Remove(player.userID);
            }
        }

        #endregion

        #region Commands

        private void cmdControlConsole(ConsoleSystem.Arg arg)
        {
            cmdControlChat(arg.Player(), string.Empty, arg.Args);
        }

        private void cmdControlChat(BasePlayer player, string command, string[] args)
        {
            var action = args?.Length > 0 ? args[0] : "true";

            switch (action)
            {
                default:
                    if (uiOpened.Contains(player.userID) == true)
                    {
                        uiOpened.Remove(player.userID);
                        CuiHelper.DestroyUi(player, elemMain);
                        return;
                    }
                    
                    OpenUI(player);
                    return;
                
                case "tp_player":
                    NTeleportation?.Call("CommandTeleportRequest", player.IPlayer, "tpr", new string[] {args[1]});
                    break;
                
                case "tp_accept":
                    NTeleportation?.Call("CommandTeleportAccept", player.IPlayer, "tpa", new string[]{});
                    break;
                
                case "tp_cancel":
                    NTeleportation?.Call("CommandTeleportCancel", player.IPlayer, "tpc", new string[]{});
                    break;
                
                case "tp_home":
                    NTeleportation?.Call("CommandHome", player.IPlayer, "home", new string[] {args[1]});
                    break;
                
                case "tp_sethome":
                    NTeleportation?.Call("CommandSetHome", player.IPlayer, "sethome", new string[] {args[1]});
                    break;
                
                case "tp_removehome":
                    NTeleportation?.Call("CommandRemoveHome", player.IPlayer, "removehome", new string[] {args[1]});
                    break;
            }
            
            if (uiOpened.Contains(player.userID) == true)
            {
                uiOpened.Remove(player.userID);
                CuiHelper.DestroyUi(player, elemMain);
            }
        }

        #endregion

        #region Core

        private void OpenUI(BasePlayer player)
        {
            var buttons = new List<ButtonEntry>(); 
            
            if (HaveAvailableHomes(player))
            {
                var pos = GetGrid(player.transform.position, false);
                buttons.Add(new ButtonEntry
                {
                    name = GetMessage("Save Home", player.UserIDString),
                    command = $"{config.command} tp_sethome {pos}",
                    url = config.sethomeUrl
                });
            }
            
            var friendList = GetFriends(player);
            if (friendList.Count > 0)
            {
                foreach (var friend in friendList)
                {
                    var friendName = friend.name;
                    if (friendName.Length > 10)
                    {
                        friendName = friendName.Substring(0, 10);
                    }
                    
                    buttons.Add(new ButtonEntry
                    {
                        command = $"{config.command} tp_player {friend.id}",
                        name = $"{friendName}",
                        url = config.teleportUrl
                    });
                }
            }

            var homes = GetHomes(player);
            if (homes.Count > 0)
            {
                foreach (var homeName in homes)
                {
                    buttons.Add(new ButtonEntry
                    {
                        name = homeName,
                        command = $"{config.command} tp_home {homeName}",
                        url = config.homeUrl
                    });     
                }
            }
            
            if (HavePendingRequest(player))
            {
                buttons.Add(new ButtonEntry
                {
                    name = GetMessage("Accept Teleport", player.UserIDString),
                    command = $"{config.command} tp_accept",
                    url = config.acceptUrl
                });
                
                buttons.Add(new ButtonEntry
                {
                    name = GetMessage("Cancel Teleport", player.UserIDString),
                    command = $"{config.command} tp_cancel",
                    url = config.cancelUrl
                });
            }
            
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = elemMain,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 0",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5"
                    },
                    new CuiNeedsCursorComponent()
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = elemMain,
                Components =
                {
                    new CuiButtonComponent
                    {
                        //Close = elemMain,
                        Command = config.command,
                        Color = "1 1 1 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-1000 -1000",
                        OffsetMax = "1000 1000"
                    }
                }
            });
            
            var r = buttons.Count * 10 + config.buttonRadius;
            var c = (double) buttons.Count / 2;
            
            for (var i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                var pos = i / c * Math.PI;
                var x = r * Math.Sin(pos);
                var y = r * Math.Cos(pos);

                container.Add(new CuiElement
                {
                    Name = $"{elemMain} {i}",
                    Parent = elemMain,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = "1 1 1 1",
                            Png = GetImage(button.url)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{x - config.buttonSize} {y - config.buttonSize}", 
                            AnchorMax = $"{x + config.buttonSize} {y + config.buttonSize}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = $"{elemMain} {i}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = config.textColor.StartsWith("#") ? GenerateRgba(config.textColor) : config.textColor,
                            Text = $"<size={config.textSize}>{button.name}</size>",
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiOutlineComponent
                        {
                            Color = config.outlineColor.StartsWith("#") ? GenerateRgba(config.outlineColor) : config.outlineColor,
                            Distance = config.outlineDistance
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", 
                            AnchorMax = "1 1",
                        }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Parent = $"{elemMain} {i}",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = "1 1 1 0",
                            Command = button.command,
                            //Close = elemMain
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", 
                            AnchorMax = "1 1"
                        }
                    }
                });

                if (button.url == config.homeUrl)
                {
                    container.Add(new CuiElement
                    {
                        Parent = $"{elemMain} {i}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Color = "1 1 1 1",
                                Png = GetImage(config.removeHomeUrl)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.85 0.85", 
                                AnchorMax = "0.85 0.85",
                                OffsetMin = "-10 -10",
                                OffsetMax = "10 10"
                            }
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Parent = $"{elemMain} {i}",
                        Components =
                        {
                            new CuiButtonComponent
                            {
                                Color = "1 1 1 0",
                                Command = $"{config.command} tp_removehome {button.name}",
                                //Close = elemMain
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.85 0.85", 
                                AnchorMax = "0.85 0.85",
                                OffsetMin = "-10 -10",
                                OffsetMax = "10 10"
                            }
                        }
                    });
                }
            }

            CuiHelper.DestroyUi(player, elemMain);
            CuiHelper.AddUi(player, container);
            
            if (uiOpened.Contains(player.userID) == false)
            {
                uiOpened.Add(player.userID);
            }
        }

        private List<PlayerEntry> GetFriends(BasePlayer player)
        {
            var playerID = player.userID;
            var list = new List<PlayerEntry>();

            if (config.cacheTime > 0)
            {
                if (friends.TryGetValue(playerID, out list))
                {
                    return list;
                }
            }

            list = new List<PlayerEntry>();
            var obj = GetFriends(playerID);
            if (obj != null)
            {
                foreach (var value in obj)
                {
                    var data = permission.GetUserData(value.ToString());
                    var displayName = data.LastSeenNickname;
                    if (displayName == "Unnamed")
                    {
                        var target = BasePlayer.FindByID(value) ?? BasePlayer.FindSleeping(value);
                        if (target != null)
                        {
                            displayName = target.displayName;
                        }
                    }
                    
                    list.Add(new PlayerEntry
                    {
                        name = displayName,
                        id = value,
                    });
                }
            }

            if (config.cacheTime > 0)
            {
                friends.Add(playerID, list);
            }

            return list;
        }
        
        private static ulong[] GetFriends(ulong playerID)
        {
            var flag = Interface.CallHook("GetFriends", playerID);
            if (flag == null)
            {
                return new ulong[]{};
            }
            
            if (flag is ulong[])
            {
                return (ulong[]) flag;
            }

            return new ulong[]{};
        }

        private string GetGrid(Vector3 position, bool addVector) 
            // Credit: Jake_Rich
        {
            var roundedPos = new Vector2(World.Size / 2 + position.x, World.Size / 2 - position.z);
            var grid = $"{NumberToLetter((int)(roundedPos.x / 150))}{(int)(roundedPos.y / 150)}";
            
            if (addVector)
            {
                grid += $" {position.ToString().Replace(",", "")}";
            }
            
            return grid;
        }

        private string NumberToLetter(int num) 
            // Credit: Jake_Rich
        {
            var num2 = Mathf.FloorToInt((float)(num / 26));
            var num3 = num % 26;
            var text = string.Empty;
            if (num2 > 0)
            {
                for (var i = 0; i < num2; i++)
                {
                    text += Convert.ToChar(65 + i);
                }
            } 
      
            return text + Convert.ToChar(65 + num3);
        }

        private string GenerateRgba(string backgroundColor)
        {
            var color = ColorTranslator.FromHtml(backgroundColor);
            int r = Convert.ToInt16(color.R);
            int g = Convert.ToInt16(color.G);
            int b = Convert.ToInt16(color.B);
            return $"{r} {g} {b} 1";
        }

        #endregion
        
        #region Configuration 1.1.2

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Command")]
            public string command;

            [JsonProperty(PropertyName = "Text color")]
            public string textColor;

            [JsonProperty(PropertyName = "Text size")]
            public int textSize;

            [JsonProperty(PropertyName = "Friends cache time")]
            public int cacheTime;

            [JsonProperty(PropertyName = "Button radius")]
            public int buttonRadius;

            [JsonProperty(PropertyName = "Button size")]
            public int buttonSize;
            
            [JsonProperty(PropertyName = "Teleport icon url")]
            public string teleportUrl;

            [JsonProperty(PropertyName = "Accept icon url")]
            public string acceptUrl;
            
            [JsonProperty(PropertyName = "Cancel icon url")]
            public string cancelUrl;
            
            [JsonProperty(PropertyName = "Sethome icon url")]
            public string sethomeUrl;
            
            [JsonProperty(PropertyName = "Home icon url")]
            public string homeUrl;
            
            [JsonProperty(PropertyName = "Home remove icon url")]
            public string removeHomeUrl;

            [JsonProperty(PropertyName = "Outline color")]
            public string outlineColor;

            [JsonProperty(PropertyName = "Outline distance")]
            public string outlineDistance = "1.0 -1.0";
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                buttonRadius = 75,
                textSize = 14,
                cacheTime = 500,
                textColor = "1 1 1 1",
                buttonSize = 35,
                command = "tp.menu",
                acceptUrl = "https://i.imgur.com/vGTqWKV.png",
                cancelUrl = "https://i.imgur.com/wAF3A5K.png",
                homeUrl = "https://i.imgur.com/0iEyYyP.png",
                teleportUrl = "https://i.imgur.com/WMHRamO.png",
                sethomeUrl = "https://i.imgur.com/YWaEHW7.png",
                removeHomeUrl = "https://i.imgur.com/wAF3A5K.png",
                outlineColor = "#00ffff"
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                
                timer.Every(10f, () =>
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                });
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
        
        #region Localization 1.1.1
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Accept Teleport", "Accept TP"},
                {"Cancel Teleport", "Cancel TP"},
                {"Save Home", "Set new home"},
            }, this);
        }
        
        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        #endregion
        
        #region Image Library 1.0.1

        [PluginReference] private Plugin ImageLibrary;

        private void AddImage(string name, string url)
        {
            if (ImageLibrary == null || ImageLibrary?.IsLoaded == false)
            {
                timer.Once(3f, () => { AddImage(name, url); });
                return;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
            {
                return;
            }

            ImageLibrary.CallHook("AddImage", url, name, (ulong) 0);
        }

        private string GetImage(string name)
        {
            return ImageLibrary?.Call<string>("GetImage", name);
        }

        #endregion

        #region Teleportation 1.0.0

        private static bool HavePendingRequest(BasePlayer player)
        {
            var flag = Interface.CallHook("API_HavePendingRequest", player);
            if (flag == null)
            {
                return false;
            }

            if (flag is bool)
            {
                return (bool) flag;
            }

            return false;
        }

        private static bool HaveAvailableHomes(BasePlayer player)
        {
            var flag = Interface.CallHook("API_HaveAvailableHomes", player);
            if (flag == null)
            {
                return false;
            }
            
            if (flag is bool)
            {
                return (bool) flag;
            }

            return false;
        }

        private static List<string> GetHomes(BasePlayer player)
        {
            var flag = Interface.CallHook("API_GetHomes", player);
            if (flag == null)
            {
                return new List<string>();
            }
            
            if (flag is List<string>)
            {
                return (List<string>) flag;
            }

            return new List<string>();
        }

        #endregion
    }
}