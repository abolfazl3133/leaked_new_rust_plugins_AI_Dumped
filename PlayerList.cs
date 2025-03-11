using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.PlayerListExtensionMethods;
using UnityEngine;
using Random = UnityEngine.Random;
   ///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("Player List", "discord.gg/9vyTXsJyKR", "2.0.2")]
    [Description("Adds a list of players to the interface")]
    public class PlayerList : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin
            ImageLibrary = null,
            Notify = null,
            UINotify = null,
            Friends = null,
            Clans = null;

        private static PlayerList _instance;

        private const string Layer = "UI.PlayerList";

        private const string ModalLayer = "UI.PlayerList.Modal";

        private readonly Dictionary<int, ButtonInfo> _buttonById = new Dictionary<int, ButtonInfo>();

        private readonly List<ulong> _openedUi = new List<ulong>();

        #endregion

        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = {"players", "plist"};

            [JsonProperty(PropertyName = "Permission (ex: playerlist.use)")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = "Close UI when reusing a command?")]
            public readonly bool CloseReusing = false;

            [JsonProperty(PropertyName = "Work with Notify?")]
            public bool UseNotify = true;

            [JsonProperty(PropertyName = "Show the player who opened the player list?")]
            public bool ShowSelfPlayer = false;

            [JsonProperty(PropertyName = "Fields", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<FieldInfo> Fields = new List<FieldInfo>
            {
                new FieldInfo(string.Empty, "ClanTag", "Clans", "GetClanOf", new List<string> {"%steamid%"}),
                new FieldInfo(string.Empty, "StatsRating", "Statistics", "GetTop", new List<string> {"%steamid%", "0"}),
                new FieldInfo(string.Empty, "StatsWeapon", "Statistics", "GetFavoriteWeapon",
                    new List<string> {"%steamid%"}),
                new FieldInfo(string.Empty, "StatsKD", "Statistics", "GetStatsValue",
                    new List<string> {"%steamid%", "kd"})
            };

            [JsonProperty(PropertyName = "Buttons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ButtonInfo> Buttons = new List<ButtonInfo>
            {
                new ButtonInfo(string.Empty, "BtnTP", "tpr %steamid%", true, true, true, true, true, true, true,
                    new IColor("#4B68FF", 100)),
                new ButtonInfo(string.Empty, "BtnTrade", "trade %username%", true, true, true, true, true, true, true,
                    new IColor("#4B68FF", 100)),
                new ButtonInfo(string.Empty, "BtnStats", "stats %steamid%", true, true, true, true, true, true, true,
                    new IColor("#4B68FF", 100)),
                new ButtonInfo("playerlist.admin", "BtnKick", "kick %steamid%", true, true, true, true, true, true,
                    true, new IColor("#FF4B4B", 100))
            };

            [JsonProperty(PropertyName = "Profile UI Settings")]
            public ProfileSettings ProfileUI = new ProfileSettings
            {
                Width = 490,
                Height = 275,
                FieldWidth = 135,
                FieldHeight = 50,
                FieldHeightMargin = 10f,
                FieldWidthMargin = 20f,
                FieldsOnString = 2,
                FieldsRightIndent = 180f,
                FieldsUpIndent = -105f,
                ButtonWidth = 105,
                ButtonHeight = 25,
                ButtonHeightMargin = 10f,
                ButtonWidthMargin = 10f,
                ButtonsOnString = 4,
                ButtonsRightIndent = 20,
                ButtonsUpIndent = -230
            };
        }

        private class ProfileSettings
        {
            [JsonProperty(PropertyName = "Width")] public float Width;

            [JsonProperty(PropertyName = "Height")]
            public float Height;

            [JsonProperty(PropertyName = "Field Width")]
            public float FieldWidth;

            [JsonProperty(PropertyName = "Field Height")]
            public float FieldHeight;

            [JsonProperty(PropertyName = "Field Vertical Indent")]
            public float FieldHeightMargin;

            [JsonProperty(PropertyName = "Field Horizontal Indent")]
            public float FieldWidthMargin;

            [JsonProperty(PropertyName = "Fields On String")]
            public float FieldsOnString;

            [JsonProperty(PropertyName = "Fields Indent From Adove")]
            public float FieldsUpIndent;

            [JsonProperty(PropertyName = "Fields Indent Right")]
            public float FieldsRightIndent;

            [JsonProperty(PropertyName = "Button Width")]
            public float ButtonWidth;

            [JsonProperty(PropertyName = "Button Height")]
            public float ButtonHeight;

            [JsonProperty(PropertyName = "Button Vertical Indent")]
            public float ButtonHeightMargin;

            [JsonProperty(PropertyName = "Button Horizontal Indent")]
            public float ButtonWidthMargin;

            [JsonProperty(PropertyName = "Buttons On String")]
            public float ButtonsOnString;

            [JsonProperty(PropertyName = "Buttons Indent From Adove")]
            public float ButtonsUpIndent;

            [JsonProperty(PropertyName = "Buttons Indent Right")]
            public float ButtonsRightIndent;
        }

        private class ButtonInfo
        {
            [JsonProperty(PropertyName = "Permission (ex: playerlist.vip)")]
            public string Permission;

            [JsonProperty(PropertyName = "Lang Key (oxide/lang/**/PlayerList.json)")]
            public string LangKey;

            [JsonProperty(PropertyName = "Command")]
            public string Command;

            [JsonProperty(PropertyName = "Close menu after using")]
            public bool CloseMenu;

            [JsonProperty(PropertyName = "Close profile after using")]
            public bool CloseProfile;

            [JsonProperty(PropertyName = "For admins")]
            public bool Admins;

            [JsonProperty(PropertyName = "For clanmates")]
            public bool Clanmates;

            [JsonProperty(PropertyName = "For friends")]
            public bool Friends;

            [JsonProperty(PropertyName = "For teammates")]
            public bool Teammates;

            [JsonProperty(PropertyName = "For all players")]
            public bool All;

            [JsonProperty(PropertyName = "Color")] public IColor Color;

            public ButtonInfo(string permission, string langKey, string command, bool closeMenu, bool closeProfile,
                bool admins, bool clanmates, bool friends, bool teammates, bool all, IColor color)
            {
                Permission = permission;
                LangKey = langKey;
                Command = command;
                CloseMenu = closeMenu;
                CloseProfile = closeProfile;
                Admins = admins;
                Clanmates = clanmates;
                Friends = friends;
                Teammates = teammates;
                All = all;
                Color = color;
            }

            [JsonIgnore] private int _id = -1;

            [JsonIgnore]
            public int ID
            {
                get
                {
                    while (_id == -1)
                    {
                        var id = Random.Range(0, int.MaxValue);
                        if (_instance._buttonById.ContainsKey(id)) continue;

                        _id = id;
                        _instance._buttonById[_id] = this;
                        break;
                    }

                    return _id;
                }
            }
        }

        private class IColor
        {
            [JsonProperty(PropertyName = "HEX")] public string Hex;

            [JsonProperty(PropertyName = "Opacity (0 - 100)")]
            public readonly float Alpha;

            [JsonIgnore] private string _color;

            public string Get()
            {
                if (string.IsNullOrEmpty(_color))
                {
                    if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

                    var str = Hex.Trim('#');
                    if (str.Length != 6) throw new Exception(Hex);
                    var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                    var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                    var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                    _color = $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
                }

                return _color;
            }

            public IColor(string hex, float alpha)
            {
                Hex = hex;
                Alpha = alpha;
            }
        }

        private class FieldInfo
        {
            [JsonProperty(PropertyName = "Permission (ex: playerlist.vip)")]
            public string Permission;

            [JsonProperty(PropertyName = "Lang Key (oxide/lang/**/PlayerList.json)")]
            public string LangKey;

            [JsonProperty(PropertyName = "Plugin Name")]
            public string PluginName;

            [JsonProperty(PropertyName = "Plugin Hook")]
            public string PluginHook;

            [JsonProperty(PropertyName = "Plugin Params")]
            public List<string> PluginParams;

            [JsonIgnore] private Plugin GetPlugin => _instance.plugins.Find(PluginName);
   // https://modhub.to
  // This is a user submitted Rust Plugin checked and verified by ModHub
 // ModHub is the largest Server Owner Trading Platform Online
// ModHub Official Email: nulled-rust@protonmail.com
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⠿⠿⠿⠿⠿⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⡿⠛⠉⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠛⢿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠙⢿⣿⣿⣿⣿
//⣿⣿⣿⡿⠁⠀⠀⠀⠀⣠⣴⣶⣿⡛⢻⣿⣿⠻⣶⣤⣀⠀⠀⠀⠀⠀⢙⣿⣿⣿
//⣿⣿⣿⠃⠀⠀⠀⣠⣾⣿⣤⠈⢿⣇⣘⣿⣇⣰⣿⣿⣿⣷⣄⢀⣠⣴⣿⣿⣿
//⣿⣿⣿⠀⠀⠀⣼⣿⣿⣿⣿⣷⠾⠛⠋⠉⠉⠛⠛⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡀⠀⢸⣿⣿⣿⣿⣿⠃⠀⠀⠀⠀⠀⠀⠀⣀⣴⠿⠋⢿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣇⠀⠘⣿⣿⣿⣿⠇⠀⠀⣀⣀⣠⣤⠶⠟⠉⠀⠀⠀⠘⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⡄⠀⠹⣿⣿⣿⡀⠀⢸⡏⠉⠁⠀⢠⣄⡀⠀⠀⠀⠀⢿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣄⠀⠙⣿⣿⣇⠀⢸⣇⠀⠀⠀⢿⣿⣿⣿⣶⣶⡄⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣧⠀⢸⣿⣿⡄⠈⢿⡄⠀⠀⠀⠉⠛⠻⢿⣿⣇⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⡿⢀⣾⣿⣿⡇⠀⣨⣿⣶⣄⠀⠀⠀⠀⠀⠸⣿⣸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⢡⣾⣿⣿⡿⠁⠀⣿⣿⣿⣿⣇⠀⠀⠀⠀⠀⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡿⣋⣴⣿⣿⣿⣏⣤⣴⣾⣿⣿⣿⣿⣿⣷⣦⣄⡀⠀⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿https://modhub.to⣿⣿⣿⣿⣷⣿⣿
            public string GetField(BasePlayer target)
            {
                var plugin = GetPlugin;
                if (plugin == null)
                    return string.Empty;

                var obj = new List<object>();

                PluginParams.ForEach(param =>
                {
                    switch (param)
                    {
                        case "%steamid%":
                        {
                            obj.Add(target.userID);
                            break;
                        }
                        case "%steamidstring%":
                        {
                            obj.Add(target.UserIDString);
                            break;
                        }
                        case "%username%":
                        {
                            obj.Add(target.displayName);
                            break;
                        }
                        case "%netid%":
                        {
                            obj.Add(target.net.ID);
                            break;
                        }
                        default:
                        {
                            int value;
                            if (int.TryParse(param, out value))
                                obj.Add(value);
                            else
                                obj.Add(param);
                            break;
                        }
                    }
                });

                var callback = plugin.Call(PluginHook, obj.ToArray());
                var str = callback?.ToString();
                return !string.IsNullOrEmpty(str) ? str : "NONE";
            }

            public FieldInfo(string permission, string langKey, string pluginName, string pluginHook,
                List<string> pluginParams)
            {
                Permission = permission;
                LangKey = langKey;
                PluginName = pluginName;
                PluginHook = pluginHook;
                PluginParams = pluginParams;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _instance = this;

            RegisterPermissions();

            AddCovalenceCommand(_config.Commands, nameof(CmdOpenList));

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            ImageLibrary.Call("AddImage", "https://i.imgur.com/jpGOmkW.png", "totalavatar");
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, ModalLayer);
            }

            _instance = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            GetAvatar(player.userID,
                avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;

            CuiHelper.DestroyUi(player, Layer);

            CuiHelper.DestroyUi(player, ModalLayer);

            OnPlayerDisconnected(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            _openedUi.Remove(player.userID);
        }

        #endregion

        #region Commands

        private void CmdOpenList(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (!string.IsNullOrEmpty(_config.Permission) &&
                !permission.UserHasPermission(player.UserIDString, _config.Permission))
            {
                SendNotify(player, NoPermissions, 1);
                return;
            }

            if (_config.CloseReusing)
            {
                if (_openedUi.Contains(player.userID))
                {
                    CuiHelper.DestroyUi(player, Layer);

                    CuiHelper.DestroyUi(player, ModalLayer);

                    _openedUi.Remove(player.userID);

                    return;
                }

                _openedUi.Add(player.userID);
            }

            MainUi(player, first: true);
        }

        [ConsoleCommand("UI_PlayerList")]
        private void CmdConsolePlayers(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "close":
                {
                    _openedUi.Remove(player.userID);
                    break;
                }

                case "page":
                {
                    int page;
                    if (!arg.HasArgs(2) ||
                        !int.TryParse(arg.Args[1], out page)) return;

                    MainUi(player, page);
                    break;
                }

                case "profile":
                {
                    ulong target;
                    if (!arg.HasArgs(2) ||
                        !ulong.TryParse(arg.Args[1], out target)) return;

                    ProfileUi(player, target);
                    break;
                }

                case "usecmd":
                {
                    ulong target;
                    int btnId;
                    if (!arg.HasArgs(3) ||
                        !ulong.TryParse(arg.Args[1], out target) ||
                        !int.TryParse(arg.Args[2], out btnId)) return;

                    var targetPlayer = BasePlayer.FindByID(target);
                    if (targetPlayer == null) return;

                    ButtonInfo btn;
                    if (!_buttonById.TryGetValue(btnId, out btn))
                        return;

                    var commands = btn.Command;
                    if (string.IsNullOrEmpty(commands)) return;

                    foreach (var splitCmd in commands.Split('|'))
                    {
                        var command = splitCmd
                            .Replace("%steamid%", targetPlayer.UserIDString)
                            .Replace("%steamidstring%", targetPlayer.UserIDString)
                            .Replace("%username%", $"\"{targetPlayer.displayName}\"")
                            .Replace("%netid%", targetPlayer.net.ID.ToString());

                        if (command.Contains("chat.say"))
                        {
                            var args = command.Split(' ');

                            command =
                                $"{args[0]}  \" {string.Join(" ", args.ToList().GetRange(1, args.Length - 1))}\" 0";
                        }

                        player.SendConsoleCommand(command);
                    }

                    if (btn.CloseMenu)
                        _openedUi.Remove(player.userID);

                    break;
                }
            }
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, int page = 0, bool first = false)
        {
            var container = new CuiElementContainer();

            var itemsOnString = 3;
            var lines = 6;
            var totalAmount = itemsOnString * lines;

            #region Background

            if (first)
            {
                CuiHelper.DestroyUi(player, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    CursorEnabled = true
                }, "Overlay", Layer);

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Close = Layer,
                        Command = "UI_PlayerList close"
                    }
                }, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-450 -250",
                        OffsetMax = "450 250"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#0E0E10")
                    }
                }, Layer, Layer + ".Background");
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0 0 0 0"
                }
            }, Layer + ".Background", Layer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = {Color = HexToCuiColor("#161617")}
            }, Layer + ".Main", Layer + ".Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "30 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, TitleMenu),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = HexToCuiColor("#FFFFFF")
                }
            }, Layer + ".Header");

            float xSwitch = -25;
            float width = 25;
            float margin = 5;

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = $"{xSwitch - width} -37.5",
                    OffsetMax = $"{xSwitch} -12.5"
                },
                Text =
                {
                    Text = Msg(player, CloseButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = HexToCuiColor("#FFFFFF")
                },
                Button =
                {
                    Close = Layer,
                    Color = HexToCuiColor("#4B68FF")
                }
            }, Layer + ".Header");

            xSwitch = xSwitch - margin - width;

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = $"{xSwitch - width} -37.5",
                    OffsetMax = $"{xSwitch} -12.5"
                },
                Text =
                {
                    Text = Msg(player, BtnNext),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = HexToCuiColor("#FFFFFF")
                },
                Button =
                {
                    Color = HexToCuiColor("#4B68FF"),
                    Command = BasePlayer.activePlayerList.Count > (page + 1) * lines
                        ? $"UI_PlayerList page {page + 1}"
                        : ""
                }
            }, Layer + ".Header");

            xSwitch = xSwitch - margin - width;

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = $"{xSwitch - width} -37.5",
                    OffsetMax = $"{xSwitch} -12.5"
                },
                Text =
                {
                    Text = Msg(player, BtnBack),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = HexToCuiColor("#FFFFFF")
                },
                Button =
                {
                    Color = HexToCuiColor("#4B68FF"),
                    Command = page != 0 ? $"UI_PlayerList page {page - 1}" : ""
                }
            }, Layer + ".Header");

            #endregion

            #region List

            var itemWidth = 280f;
            var itemHeight = 60f;
            var itemMargin = 10f;

            var ySwitch = -70f;
            var constXSwitch = -(itemsOnString * itemWidth + (itemsOnString - 1) * itemMargin) / 2f;
            xSwitch = constXSwitch;

            var index = 1;


            var players = GetPlayers(player);

            if (players.Count == 0)
            {
                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 0", OffsetMax = "0 -50"
                    },
                    Text =
                    {
                        Text = Msg(player, NotAvailablePlayers),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 20,
                        Color = "1 1 1 0.45"
                    }
                }, Layer + ".Main");
            }
            else
            {
                players.Skip(page * totalAmount)
                    .Take(totalAmount).ForEach(member =>
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                                OffsetMin = $"{xSwitch} {ySwitch - itemHeight}",
                                OffsetMax = $"{xSwitch + itemWidth} {ySwitch}"
                            },
                            Image =
                            {
                                Color = HexToCuiColor("#161617")
                            }
                        }, Layer + ".Main", Layer + $".Member.{member.userID}");

                        container.Add(new CuiElement
                        {
                            Parent = Layer + $".Member.{member.userID}",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = ImageLibrary.Call<string>("GetImage", $"avatar_{member.userID}")
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 0",
                                    OffsetMin = "5 5", OffsetMax = "55 55"
                                }
                            }
                        });

                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0.5", AnchorMax = "1 1",
                                OffsetMin = "60 0", OffsetMax = "0 0"
                            },
                            Text =
                            {
                                Text = $"{GetUserName(member.UserIDString)}",
                                Align = TextAnchor.LowerLeft,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 18,
                                Color = "1 1 1 1"
                            }
                        }, Layer + $".Member.{member.userID}");

                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0.5",
                                OffsetMin = "60 0", OffsetMax = "0 0"
                            },
                            Text =
                            {
                                Text = $"{member.userID}",
                                Align = TextAnchor.UpperLeft,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 14,
                                Color = "1 1 1 0.5"
                            }
                        }, Layer + $".Member.{member.userID}");

                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                                OffsetMin = "-80 -10",
                                OffsetMax = "-10 10"
                            },
                            Text =
                            {
                                Text = Msg(player, BtnProfile),
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 12,
                                Color = "1 1 1 1"
                            },
                            Button =
                            {
                                Color = HexToCuiColor("#4B68FF"),
                                Command = $"UI_PlayerList profile {member.userID}"
                            }
                        }, Layer + $".Member.{member.userID}");

                        if (index % itemsOnString == 0)
                        {
                            xSwitch = constXSwitch;
                            ySwitch = ySwitch - itemHeight - itemMargin;
                        }
                        else
                        {
                            xSwitch += itemWidth + itemMargin;
                        }

                        index++;
                    });
            }

            #endregion
   // https://modhub.to
  // This is a user submitted Rust Plugin checked and verified by ModHub
 // ModHub is the largest Server Owner Trading Platform Online
// ModHub Official Email: nulled-rust@protonmail.com
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⠿⠿⠿⠿⠿⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⡿⠛⠉⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠛⢿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠙⢿⣿⣿⣿⣿
//⣿⣿⣿⡿⠁⠀⠀⠀⠀⣠⣴⣶⣿⡛⢻⣿⣿⠻⣶⣤⣀⠀⠀⠀⠀⠀⢙⣿⣿⣿
//⣿⣿⣿⠃⠀⠀⠀⣠⣾⣿⣤⠈⢿⣇⣘⣿⣇⣰⣿⣿⣿⣷⣄⢀⣠⣴⣿⣿⣿
//⣿⣿⣿⠀⠀⠀⣼⣿⣿⣿⣿⣷⠾⠛⠋⠉⠉⠛⠛⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡀⠀⢸⣿⣿⣿⣿⣿⠃⠀⠀⠀⠀⠀⠀⠀⣀⣴⠿⠋⢿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣇⠀⠘⣿⣿⣿⣿⠇⠀⠀⣀⣀⣠⣤⠶⠟⠉⠀⠀⠀⠘⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⡄⠀⠹⣿⣿⣿⡀⠀⢸⡏⠉⠁⠀⢠⣄⡀⠀⠀⠀⠀⢿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣄⠀⠙⣿⣿⣇⠀⢸⣇⠀⠀⠀⢿⣿⣿⣿⣶⣶⡄⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣧⠀⢸⣿⣿⡄⠈⢿⡄⠀⠀⠀⠉⠛⠻⢿⣿⣇⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⡿⢀⣾⣿⣿⡇⠀⣨⣿⣶⣄⠀⠀⠀⠀⠀⠸⣿⣸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⢡⣾⣿⣿⡿⠁⠀⣿⣿⣿⣿⣇⠀⠀⠀⠀⠀⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡿⣋⣴⣿⣿⣿⣏⣤⣴⣾⣿⣿⣿⣿⣿⣷⣦⣄⡀⠀⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿https://modhub.to⣿⣿⣿⣿⣷⣿⣿
            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void ProfileUi(BasePlayer player, ulong targetId)
        {
            var targetPlayer = BasePlayer.FindByID(targetId);
            if (targetPlayer == null) return;

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0 0 0 0.9",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                },
                CursorEnabled = true
            }, Layer, ModalLayer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button =
                {
                    Color = "0 0 0 0",
                    Close = ModalLayer
                }
            }, ModalLayer);

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = $"-{_config.ProfileUI.Width / 2} -{_config.ProfileUI.Height / 2}",
                    OffsetMax = $"{_config.ProfileUI.Width / 2} {_config.ProfileUI.Height / 2}"
                },
                Image =
                {
                    Color = HexToCuiColor("#0E0E10")
                }
            }, ModalLayer, ModalLayer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = {Color = HexToCuiColor("#161617")}
            }, ModalLayer + ".Main", ModalLayer + ".Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "30 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, TitleProfile),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = HexToCuiColor("#FFFFFF")
                }
            }, ModalLayer + ".Header");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-50 -37.5",
                    OffsetMax = "-25 -12.5"
                },
                Text =
                {
                    Text = Msg(player, CloseButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = HexToCuiColor("#FFFFFF")
                },
                Button =
                {
                    Close = ModalLayer,
                    Color = HexToCuiColor("#4B68FF")
                }
            }, ModalLayer + ".Header");

            #endregion

            #region Avatar

            if (ImageLibrary)
                container.Add(new CuiElement
                {
                    Parent = ModalLayer + ".Main",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = ImageLibrary.Call<string>("GetImage", $"avatar_{targetId}")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = "20 -215", OffsetMax = "165 -70"
                        }
                    }
                });

            #endregion

            #region Name

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "180 -90", OffsetMax = "0 -70"
                },
                Text =
                {
                    Text = $"{targetPlayer.displayName}",
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 16,
                    Color = "1 1 1 1"
                }
            }, ModalLayer + ".Main");

            #endregion

            #region Fields

            var ySwitch = _config.ProfileUI.FieldsUpIndent;
            var constXSwitch = _config.ProfileUI.FieldsRightIndent;
            var xSwitch = constXSwitch;

            var index = 1;

            var fields = GetFieldsForPlayer(player);

            fields.ForEach(field =>
            {
                var info = field.GetField(targetPlayer);
                if (string.IsNullOrEmpty(info)) return;

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{xSwitch} {ySwitch - _config.ProfileUI.FieldHeight}",
                        OffsetMax = $"{xSwitch + _config.ProfileUI.FieldWidth} {ySwitch}"
                    },
                    Image =
                    {
                        Color = "0 0 0 0"
                    }
                }, ModalLayer + ".Main", ModalLayer + $".Field.{index}");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = "0 -20", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = Msg(player, field.LangKey),
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    }
                }, ModalLayer + $".Field.{index}");

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "0 0", OffsetMax = "0 30"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#161617")
                    }
                }, ModalLayer + $".Field.{index}", ModalLayer + $".Field.{index}.Value");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "20 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = $"{info}",
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    }
                }, ModalLayer + $".Field.{index}.Value");

                if (index % _config.ProfileUI.FieldsOnString == 0)
                {
                    if (index != fields.Count)
                    {
                        ySwitch = ySwitch - _config.ProfileUI.FieldHeight - _config.ProfileUI.FieldHeightMargin;
                        xSwitch = constXSwitch;
                    }
                }
                else
                {
                    xSwitch += _config.ProfileUI.FieldWidth + _config.ProfileUI.FieldWidthMargin;
                }

                index++;
            });

            #endregion

            #region Buttons

            ySwitch = _config.ProfileUI.ButtonsUpIndent;
            constXSwitch = _config.ProfileUI.ButtonsRightIndent;
            xSwitch = constXSwitch;

            index = 1;

            var buttons = GetButtonsForPlayer(player, targetPlayer);

            buttons.ForEach(btn =>
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{xSwitch} {ySwitch - _config.ProfileUI.ButtonHeight}",
                        OffsetMax = $"{xSwitch + _config.ProfileUI.ButtonWidth} {ySwitch}"
                    },
                    Text =
                    {
                        Text = Msg(player, btn.LangKey),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = btn.Color.Get(),
                        Command = $"UI_PlayerList usecmd {targetId} {btn.ID}",
                        Close = btn.CloseMenu ? Layer : btn.CloseProfile ? ModalLayer : string.Empty
                    }
                }, ModalLayer + ".Main");

                if (index % _config.ProfileUI.ButtonsOnString == 0)
                {
                    ySwitch = ySwitch - _config.ProfileUI.ButtonHeight - _config.ProfileUI.ButtonHeightMargin;
                    xSwitch = constXSwitch;
                }
                else
                {
                    xSwitch += _config.ProfileUI.ButtonWidth + _config.ProfileUI.ButtonWidthMargin;
                }

                index++;
            });

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, ModalLayer);
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private List<BasePlayer> GetPlayers(BasePlayer player)
        {
            return _config.ShowSelfPlayer ? BasePlayer.activePlayerList.ToList() : BasePlayer.activePlayerList.Where(x => x != player);
        }

        #region Avatar

        private readonly Regex _regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

        private void GetAvatar(ulong userId, Action<string> callback)
        {
            if (callback == null) return;

            webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
            {
                if (code != 200 || response == null)
                    return;

                var avatar = _regex.Match(response).Groups[1].ToString();
                if (string.IsNullOrEmpty(avatar))
                    return;

                callback.Invoke(avatar);
            }, this);
        }

        #endregion
   // https://modhub.to
  // This is a user submitted Rust Plugin checked and verified by ModHub
 // ModHub is the largest Server Owner Trading Platform Online
// ModHub Official Email: nulled-rust@protonmail.com
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⠿⠿⠿⠿⠿⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⡿⠛⠉⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠛⢿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠙⢿⣿⣿⣿⣿
//⣿⣿⣿⡿⠁⠀⠀⠀⠀⣠⣴⣶⣿⡛⢻⣿⣿⠻⣶⣤⣀⠀⠀⠀⠀⠀⢙⣿⣿⣿
//⣿⣿⣿⠃⠀⠀⠀⣠⣾⣿⣤⠈⢿⣇⣘⣿⣇⣰⣿⣿⣿⣷⣄⢀⣠⣴⣿⣿⣿
//⣿⣿⣿⠀⠀⠀⣼⣿⣿⣿⣿⣷⠾⠛⠋⠉⠉⠛⠛⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡀⠀⢸⣿⣿⣿⣿⣿⠃⠀⠀⠀⠀⠀⠀⠀⣀⣴⠿⠋⢿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣇⠀⠘⣿⣿⣿⣿⠇⠀⠀⣀⣀⣠⣤⠶⠟⠉⠀⠀⠀⠘⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⡄⠀⠹⣿⣿⣿⡀⠀⢸⡏⠉⠁⠀⢠⣄⡀⠀⠀⠀⠀⢿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣄⠀⠙⣿⣿⣇⠀⢸⣇⠀⠀⠀⢿⣿⣿⣿⣶⣶⡄⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣧⠀⢸⣿⣿⡄⠈⢿⡄⠀⠀⠀⠉⠛⠻⢿⣿⣇⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⡿⢀⣾⣿⣿⡇⠀⣨⣿⣶⣄⠀⠀⠀⠀⠀⠸⣿⣸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⢡⣾⣿⣿⡿⠁⠀⣿⣿⣿⣿⣇⠀⠀⠀⠀⠀⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡿⣋⣴⣿⣿⣿⣏⣤⣴⣾⣿⣿⣿⣿⣿⣷⣦⣄⡀⠀⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿https://modhub.to⣿⣿⣿⣿⣷⣿⣿
        private void RegisterPermissions()
        {
            if (!string.IsNullOrEmpty(_config.Permission) && !permission.PermissionExists(_config.Permission))
                permission.RegisterPermission(_config.Permission, this);

            _config.Fields.ForEach(field =>
            {
                if (!string.IsNullOrEmpty(field.Permission) && !permission.PermissionExists(field.Permission))
                    permission.RegisterPermission(field.Permission, this);
            });

            _config.Buttons.ForEach(btn =>
            {
                if (!string.IsNullOrEmpty(btn.Permission) && !permission.PermissionExists(btn.Permission))
                    permission.RegisterPermission(btn.Permission, this);
            });
        }

        private List<ButtonInfo> GetButtonsForPlayer(BasePlayer player, BasePlayer target)
        {
            return _config.Buttons.FindAll(button =>
                (string.IsNullOrEmpty(button.Permission) ||
                 permission.UserHasPermission(player.UserIDString, button.Permission)) &&
                (button.All ||
                 (button.Admins && player.IsAdmin) ||
                 (button.Clanmates && IsClanMember(player.userID, target.userID)) ||
                 (button.Friends && IsFriends(player.userID, target.userID)) ||
                 (button.Teammates && IsTeammates(player.userID, target.userID))));
        }

        private List<FieldInfo> GetFieldsForPlayer(BasePlayer player)
        {
            return _config.Fields.FindAll(field =>
                string.IsNullOrEmpty(field.Permission) ||
                permission.UserHasPermission(player.UserIDString, field.Permission));
        }

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
        }

        private bool IsClanMember(ulong playerID, ulong targetID)
        {
            return Convert.ToBoolean(Clans?.Call("HasFriend", playerID, targetID));
        }

        private bool IsFriends(ulong playerID, ulong friendId)
        {
            return Convert.ToBoolean(Friends?.Call("AreFriends", playerID, friendId));
        }

        private static bool IsTeammates(ulong player, ulong friend)
        {
            return RelationshipManager.ServerInstance.FindPlayersTeam(player)?.members?.Contains(friend) == true;
        }

        #region Username

        private readonly Dictionary<string, string> _playerNames = new Dictionary<string, string>();

        private string GetUserName(string member)
        {
            string result;
            if (_playerNames.TryGetValue(member, out result)) return result;

            var player = covalence.Players.FindPlayerById(member);
            if (player == null)
                return "UKNOWN";

            result = player.Name;
            _playerNames[member] = result;
            return result;
        }

        #endregion

        #endregion

        #region Lang

        private const string
            NotAvailablePlayers = "NotAvailablePlayers",
            BtnBack = "BtnBack",
            BtnNext = "BtnNext",
            NoPermissions = "NoPermissions",
            CloseButton = "CloseButton",
            BtnProfile = "BtnProfile",
            TitleProfile = "TitleProfile",
            TitleMenu = "TitleMenu";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [NoPermissions] = "You don't have permissions!",
                [CloseButton] = "✕",
                [TitleMenu] = "Players List",
                [TitleProfile] = "Profile",
                [BtnProfile] = "Profile",
                [BtnBack] = "◀",
                [BtnNext] = "▶",
                [NotAvailablePlayers] = "NO PLAYERS AVAILABLE :(",
                ["ClanTag"] = "Clan",
                ["StatsRating"] = "Rating",
                ["StatsWeapon"] = "Favorite Weapon",
                ["StatsKD"] = "KD",
                ["BtnTP"] = "TP",
                ["BtnTrade"] = "Trade",
                ["BtnStats"] = "Stats",
                ["BtnKick"] = "Kick"
            }, this);
        }

        private string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, userid), obj);
        }

        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            SendReply(player, Msg(player, key, obj));
        }

        private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
        {
            if (_config.UseNotify && (Notify != null || UINotify != null))
                Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion
    }
}

#region Extension Methods

namespace Oxide.Plugins.PlayerListExtensionMethods
{
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
    public static class ExtensionMethods
    {
        internal static Permission p;

        public static bool All<T>(this IList<T> a, Func<T, bool> b)
        {
            for (var i = 0; i < a.Count; i++)
                if (!b(a[i]))
                    return false;
            return true;
        }

        public static int Average(this IList<int> a)
        {
            if (a.Count == 0) return 0;
            var b = 0;
            for (var i = 0; i < a.Count; i++) b += a[i];
            return b / a.Count;
        }

        public static T ElementAt<T>(this IEnumerable<T> a, int b)
        {
            using (var c = a.GetEnumerator())
            {
                while (c.MoveNext())
                {
                    if (b == 0) return c.Current;
                    b--;
                }
            }

            return default(T);
        }

        public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null)
        {
            using (var c = a.GetEnumerator())
            {
                while (c.MoveNext())
                    if (b == null || b(c.Current))
                        return true;
            }

            return false;
        }

        public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null)
        {
            using (var c = a.GetEnumerator())
            {
                while (c.MoveNext())
                    if (b == null || b(c.Current))
                        return c.Current;
            }

            return default(T);
        }

        public static int RemoveAll<T, V>(this IDictionary<T, V> a, Func<T, V, bool> b)
        {
            var c = new List<T>();
            using (var d = a.GetEnumerator())
            {
                while (d.MoveNext())
                    if (b(d.Current.Key, d.Current.Value))
                        c.Add(d.Current.Key);
            }

            c.ForEach(e => a.Remove(e));
            return c.Count;
        }

        public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b)
        {
            var c = new List<V>();
            using (var d = a.GetEnumerator())
            {
                while (d.MoveNext()) c.Add(b(d.Current));
            }

            return c;
        }

        public static List<TResult> Select<T, TResult>(this List<T> source, Func<T, TResult> selector)
        {
            if (source == null || selector == null) return new List<TResult>();

            var r = new List<TResult>(source.Count);
            for (var i = 0; i < source.Count; i++) r.Add(selector(source[i]));

            return r;
        }

        public static string[] Skip(this string[] a, int count)
        {
            if (a.Length == 0) return Array.Empty<string>();
            var c = new string[a.Length - count];
            var n = 0;
            for (var i = 0; i < a.Length; i++)
            {
                if (i < count) continue;
                c[n] = a[i];
                n++;
            }

            return c;
        }

        public static List<T> Skip<T>(this IList<T> source, int count)
        {
            if (count < 0)
                count = 0;

            if (source == null || count > source.Count)
                return new List<T>();

            var result = new List<T>(source.Count - count);
            for (var i = count; i < source.Count; i++)
                result.Add(source[i]);
            return result;
        }

        public static Dictionary<T, V> Skip<T, V>(
            this IDictionary<T, V> source,
            int count)
        {
            var result = new Dictionary<T, V>();
            using (var iterator = source.GetEnumerator())
            {
                for (var i = 0; i < count; i++)
                    if (!iterator.MoveNext())
                        break;

                while (iterator.MoveNext()) result.Add(iterator.Current.Key, iterator.Current.Value);
            }

            return result;
        }

        public static List<T> Take<T>(this IList<T> a, int b)
        {
            var c = new List<T>();
            for (var i = 0; i < a.Count; i++)
            {
                if (c.Count == b) break;
                c.Add(a[i]);
            }

            return c;
        }
   // https://modhub.to
  // This is a user submitted Rust Plugin checked and verified by ModHub
 // ModHub is the largest Server Owner Trading Platform Online
// ModHub Official Email: nulled-rust@protonmail.com
//⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⠿⠿⠿⠿⠿⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣿⡿⠛⠉⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠛⢿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠙⢿⣿⣿⣿⣿
//⣿⣿⣿⡿⠁⠀⠀⠀⠀⣠⣴⣶⣿⡛⢻⣿⣿⠻⣶⣤⣀⠀⠀⠀⠀⠀⢙⣿⣿⣿
//⣿⣿⣿⠃⠀⠀⠀⣠⣾⣿⣤⠈⢿⣇⣘⣿⣇⣰⣿⣿⣿⣷⣄⢀⣠⣴⣿⣿⣿
//⣿⣿⣿⠀⠀⠀⣼⣿⣿⣿⣿⣷⠾⠛⠋⠉⠉⠛⠛⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡀⠀⢸⣿⣿⣿⣿⣿⠃⠀⠀⠀⠀⠀⠀⠀⣀⣴⠿⠋⢿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣇⠀⠘⣿⣿⣿⣿⠇⠀⠀⣀⣀⣠⣤⠶⠟⠉⠀⠀⠀⠘⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⡄⠀⠹⣿⣿⣿⡀⠀⢸⡏⠉⠁⠀⢠⣄⡀⠀⠀⠀⠀⢿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣄⠀⠙⣿⣿⣇⠀⢸⣇⠀⠀⠀⢿⣿⣿⣿⣶⣶⡄⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⣧⠀⢸⣿⣿⡄⠈⢿⡄⠀⠀⠀⠉⠛⠻⢿⣿⣇⢸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿⡿⢀⣾⣿⣿⡇⠀⣨⣿⣶⣄⠀⠀⠀⠀⠀⠸⣿⣸⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⠟⢡⣾⣿⣿⡿⠁⠀⣿⣿⣿⣿⣇⠀⠀⠀⠀⠀⣿⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⡿⣋⣴⣿⣿⣿⣏⣤⣴⣾⣿⣿⣿⣿⣿⣷⣦⣄⡀⠀⣿⣿⣿⣿⣿⣿
//⣿⣿⣿⣿⣿⣿https://modhub.to⣿⣿⣿⣿⣷⣿⣿
        public static Dictionary<T, V> Take<T, V>(this IDictionary<T, V> a, int b)
        {
            var c = new Dictionary<T, V>();
            foreach (var f in a)
            {
                if (c.Count == b) break;
                c.Add(f.Key, f.Value);
            }

            return c;
        }

        public static Dictionary<T, V> ToDictionary<S, T, V>(this IEnumerable<S> a, Func<S, T> b, Func<S, V> c)
        {
            var d = new Dictionary<T, V>();
            using (var e = a.GetEnumerator())
            {
                while (e.MoveNext()) d[b(e.Current)] = c(e.Current);
            }

            return d;
        }

        public static List<T> ToList<T>(this IEnumerable<T> a)
        {
            var b = new List<T>();
            using (var c = a.GetEnumerator())
            {
                while (c.MoveNext()) b.Add(c.Current);
            }

            return b;
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> a)
        {
            return new HashSet<T>(a);
        }

        public static List<T> Where<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            var c = new List<T>();

            using (var d = source.GetEnumerator())
            {
                while (d.MoveNext())
                    if (predicate(d.Current))
                        c.Add(d.Current);
            }

            return c;
        }

        public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity
        {
            var b = new List<T>();
            using (var c = a.GetEnumerator())
            {
                while (c.MoveNext())
                    if (c.Current is T)
                        b.Add(c.Current as T);
            }

            return b;
        }

        public static int Sum<T>(this IList<T> a, Func<T, int> b)
        {
            var c = 0;
            for (var i = 0; i < a.Count; i++)
            {
                var d = b(a[i]);
                if (!float.IsNaN(d)) c += d;
            }

            return c;
        }

        public static int Sum(this IList<int> a)
        {
            var c = 0;
            for (var i = 0; i < a.Count; i++)
            {
                var d = a[i];
                if (!float.IsNaN(d)) c += d;
            }

            return c;
        }

        public static bool HasPermission(this string a, string b)
        {
            if (p == null) p = Interface.Oxide.GetLibrary<Permission>();
            return !string.IsNullOrEmpty(a) && p.UserHasPermission(a, b);
        }

        public static bool HasPermission(this BasePlayer a, string b)
        {
            return a.UserIDString.HasPermission(b);
        }

        public static bool HasPermission(this ulong a, string b)
        {
            return a.ToString().HasPermission(b);
        }

        public static bool IsReallyConnected(this BasePlayer a)
        {
            return a.IsReallyValid() && a.net.connection != null;
        }

        public static bool IsKilled(this BaseNetworkable a)
        {
            return (object) a == null || a.IsDestroyed;
        }

        public static bool IsNull<T>(this T a) where T : class
        {
            return a == null;
        }

        public static bool IsNull(this BasePlayer a)
        {
            return (object) a == null;
        }

        public static bool IsReallyValid(this BaseNetworkable a)
        {
            return !((object) a == null || a.IsDestroyed || a.net == null);
        }

        public static void SafelyKill(this BaseNetworkable a)
        {
            if (a.IsKilled()) return;
            a.Kill();
        }

        public static bool CanCall(this Plugin o)
        {
            return o != null && o.IsLoaded;
        }

        public static bool IsInBounds(this OBB o, Vector3 a)
        {
            return o.ClosestPoint(a) == a;
        }

        public static bool IsHuman(this BasePlayer a)
        {
            return !(a.IsNpc || !a.userID.IsSteamId());
        }

        public static BasePlayer ToPlayer(this IPlayer user)
        {
            return user.Object as BasePlayer;
        }

        public static List<TResult> SelectMany<TSource, TResult>(this List<TSource> source,
            Func<TSource, List<TResult>> selector)
        {
            if (source == null || selector == null)
                return new List<TResult>();

            var result = new List<TResult>(source.Count);
            source.ForEach(i => selector(i).ForEach(j => result.Add(j)));
            return result;
        }

        public static IEnumerable<TResult> SelectMany<TSource, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, IEnumerable<TResult>> selector)
        {
            using (var item = source.GetEnumerator())
            {
                while (item.MoveNext())
                    using (var result = selector(item.Current).GetEnumerator())
                    {
                        while (result.MoveNext()) yield return result.Current;
                    }
            }
        }

        public static int Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, int> selector)
        {
            var sum = 0;

            using (var element = source.GetEnumerator())
            {
                while (element.MoveNext()) sum += selector(element.Current);
            }

            return sum;
        }

        public static double Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, double> selector)
        {
            var sum = 0.0;

            using (var element = source.GetEnumerator())
            {
                while (element.MoveNext()) sum += selector(element.Current);
            }

            return sum;
        }

        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) return false;

            using (var element = source.GetEnumerator())
            {
                while (element.MoveNext())
                    if (predicate(element.Current))
                        return true;
            }

            return false;
        }
    }
}

#endregion Extension Methods