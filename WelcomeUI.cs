using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;

namespace Oxide.Plugins
{
    [Info("Welcome UI", "Mevent, modded by BlackSalami", "1.0.2 - 1.0.0")]
    [Description("Information Panel for Server")]
    class WelcomeUI : RustPlugin
    {
        #region Fields
        [PluginReference] private Plugin ImageLibrary;
        private const string Layer = "WelcomePanelUI";
        private const string MainColor = "0.98 0.544 0.548 1";
        private const string DefaultColor = "0 0.6 1 1";
        #endregion

        #region Work with Data
        private static PluginData _data;
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }
        private class PluginData
        {
            public List<ulong> Users = new List<ulong>();
        }
        #endregion

        #region Config
        private static ConfigData config;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Show panel once?")]
            public bool introduce;
            [JsonProperty(PropertyName = "Logo URL")]
            public LogoSettings logo;
            [JsonProperty(PropertyName = "Close Settings")]
            public CloseSettings close;
            [JsonProperty("Menu Settings")]
            public List<MenuSettings> menu;
        }
        private class LogoSettings
        {
            [JsonProperty(PropertyName = "Enabled?")]
            public bool enabled;
            [JsonProperty(PropertyName = "Logo URL")]
            public string logoUrl;
            [JsonProperty(PropertyName = "Offset Min")]
            public string oMin;
            [JsonProperty(PropertyName = "Offset Max")]
            public string oMax;
        }
        private class MenuSettings
        {
            [JsonProperty(PropertyName = "Menu Icon")]
            public string menuIcon;
            [JsonProperty(PropertyName = "Menu Text")]
            public List<string> description;
        }
        private class CloseSettings
        {
            [JsonProperty(PropertyName = "Display close button only on the last page?")]
            public bool closePage;
            [JsonProperty(PropertyName = "Close BTN Text")]
            public string closeText;
            [JsonProperty(PropertyName = "Offset Min")]
            public string oMin;
            [JsonProperty(PropertyName = "Offset Max")]
            public string oMax;
            [JsonProperty(PropertyName = "Font Size")]
            public int fSize;
        }
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                introduce = true,
                logo = new LogoSettings
                {
                    enabled = true,
                    logoUrl = "https://i.imgur.com/MS31MgK.png",
                    oMin = "-80 5",
                    oMax = "80 35"
                },
                close = new CloseSettings
                {
                    closePage = true,
                    closeText = "I have read everything shown here",
                    oMin = "-180 20",
                    oMax = "180 50",
                    fSize = 16
                },
                menu = new List<MenuSettings>
                {
                    new MenuSettings
                    {
                        menuIcon = "https://i.imgur.com/RcORxrs.png",
                        description = new List<string>
                        {
                            "<b><color=#b0fa66>Welcome to SERVERNAME, please read before playing.</color></b>",
                            "<b><color=#5b86b4>SERVER.LINK/DISCORD  SERVER.LINK/STEAM  DONATE.SERVER.LINK</color></b>\n",
                            "<b><color=#5b86b4>Group Limit</color></b>",
                            "<color=#b0fa66>■</color> Using 3rd party applications to gain an advantage will result in a ban. This includes cheating, scripts and macros.",
                            "<color=#b0fa66>■</color> Spamming chat or being racist will result in either a mute, or ban depending on the duration, and type of content.",
                            "<color=#b0fa66>■</color> Any type of advertising with result in a mute, or ban depending on content.",
                            "<color=#b0fa66>■</color> If caught abusing game exploits, depending on severity will result in ban. This includes getting into places outside of the map, or into rocks ect.",
                            "<color=#b0fa66>■</color> Releasing of personal information of other players (doxxing) will result in a perm ban regardless of where you obtained this information (Includes images set as Display Picture). This will also result in a Discord perm ban.",
                            "<color=#b0fa66>■</color> Impersonating server or staff members will result in being banned, the duration of this is dependant on the type of content and intent by the person.",
                            "<color=#b0fa66>■</color> Please respect all staff, they are here to help."
                        }
                    },
                    new MenuSettings
                    {
                        menuIcon = "https://i.imgur.com/gcTGb2M.png",
                        description = new List<string>
                        {
                            "<b><color=#b0fa66>Welcome to SERVERNAME, please read before playing.</color></b>",
                            "<b><color=#5b86b4>SERVER.LINK/DISCORD  SERVER.LINK/STEAM  DONATE.SERVER.LINK</color></b>\n",
                            "<b><color=#5b86b4>EasyAntiCheat (Facepunch/Rust) Game Bans:</color></b>",
                            "<color=#b0fa66>■</color> Anyone found on our servers evading a game ban will be permanently banned, this includes any future accounts purchased to bypass the original game ban.",
                            "<color=#b0fa66>■</color> Anyone caught playing with a person who is cheating will be banned for 2 weeks for association. Evading this ban by playing on an alternative account will result in being permanently banned.",
                            "<color=#b0fa66>■</color> Anyone caught playing with a person over multiple accounts that are banned for ban evading will be permanently banned (includes being banned for any reasons in our Rules).",
                            "<color=#b0fa66>■</color> We believe in one second chance If you have only received one EAC ban for Rust, if you didn't evade this ban for 90 days on our servers, you can ask an admin for your play eligibility to be reviewed. Only after being reviewed and approved may you start playing on our servers.",
                        }
                    },
                    new MenuSettings
                    {
                        menuIcon = "https://i.imgur.com/JL4LFHV.png",
                        description = new List<string>
                        {
                            "<b><color=#b0fa66>Welcome to SERVERNAME, please read before playing.</color></b>",
                            "<b><color=#5b86b4>SERVER.LINK/DISCORD  SERVER.LINK/STEAM  DONATE.SERVER.LINK</color></b>\n",
                            "<b><color=#5b86b4>Stream Sniping:</color></b>",
                            "<color=#b0fa66>■</color> Stream Sniping of PARTNERED twitch streamers is not allowed. ",
                            "<color=#b0fa66>■</color> Anyone caught sniping a partnered streamer will be punished based on the severity of the offense, up to and including a server ban\n",
                            "<b><color=#5b86b4>Proxy & VPN:</color></b>",
                            "<color=#b0fa66>■</color> We do not allow any type of Proxy or VPN on our servers, unless you have approval from the admin team. Joining the server with a Proxy / VPN will result in a ban, unless approved.",
                            "<color=#b0fa66>■</color> Applying for VPN access doesn't mean you will be approved, and using it to bypass our country filter will result in the application being rejected.",
                        }
                    }
                }
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
                LoadDefaultConfig();
            }

            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("Please setup ImageLibrary plugin!");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
            LoadData();
            if (config.logo.enabled) ImageLibrary.Call("AddImage", config.logo.logoUrl, "WelcomePanelLogo");
            for (int i = 0; i < config.menu.Count; i++)
                ImageLibrary.Call("AddImage", config.menu[i].menuIcon, $"WelcomePanelImage.{i}");
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            if (player.IsReceivingSnapshot || player.IsSleeping())
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }
            if (config.introduce && !_data.Users.Contains(player.userID))
            {
                InitializeUI(player);
                _data.Users.Add(player.userID);
                SaveData();
            }
            else if (!config.introduce)
            {
                InitializeUI(player);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("info")] private void CmdChatInfo(BasePlayer player) => InitializeUI(player);
        [ChatCommand("help")] private void CmdChatHelp(BasePlayer player) => InitializeUI(player);
        [ChatCommand("helpme")] private void CmdChatCommands(BasePlayer player) => InitializeUI(player);
        [ChatCommand("command")] private void CmdChatCommand(BasePlayer player) => InitializeUI(player);
        [ChatCommand("server")] private void CmdChatVip(BasePlayer player) => InitializeUI(player);
        [ChatCommand("veteran")] private void CmdChatVeteran(BasePlayer player) => InitializeUI(player);
        [ChatCommand("voting")] private void CmdChatVoting(BasePlayer player) => InitializeUI(player);
        [ChatCommand("cmd")] private void CmdChatCmd(BasePlayer player) => InitializeUI(player);
        [ChatCommand("komutlar")] private void CmdChatCmds(BasePlayer player) => InitializeUI(player);
        [ChatCommand("dc")] private void CmdChatContact(BasePlayer player) => InitializeUI(player);
        [ChatCommand("rule")] private void CmdChatRule(BasePlayer player) => InitializeUI(player);
        [ChatCommand("rule")] private void CmdChatRules(BasePlayer player) => InitializeUI(player);
        [ChatCommand("discord")] private void CmdChatDiscord(BasePlayer player) => InitializeUI(player);

        [ConsoleCommand("welcomemenu")]
        private void CmdConsole(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null || !args.HasArgs(1)) return;
            switch (args.Args[0].ToLower())
            {
                case "page":
                    {
                        int page = 0;
                        if (!args.HasArgs(2) || !int.TryParse(args.Args[1], out page)) return;
                        InitializeUI(player, page, false);
                    }
                    break;
            }
            return;
        }
        #endregion

        #region Interface
        private void InitializeUI(BasePlayer player, int page = 0, bool fadein = true)
        {
            var container = new CuiElementContainer();
            var list = config.menu[page];
            var yCoord = 2;
            var fade = 0f;
            if (fadein) fade = 1f;
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = fade },
            }, "Overlay", Layer + ".Blur");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-350 -200", OffsetMax = "350 200" },
                Image = { Color = "0 0 0 0.85", FadeIn = fade }
            }, Layer + ".Blur", Layer);
            container.AddRange(CreateOutLine(Layer));
            if (config.logo.enabled)
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "WelcomePanelLogo"), FadeIn = fade },
                        new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = config.logo.oMin, OffsetMax = config.logo.oMax },
                    }
                });
            for (int i = 0; i < config.menu.Count; i++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "-0.12 0.975", AnchorMax = "0 1", OffsetMin = $"-23 {yCoord - 18}", OffsetMax = $"-1 {yCoord}" },
                    Button = { Color = MainColor, Command = $"welcomemenu page {i}", FadeIn = fade },
                    Text = { Text = "", FadeIn = fade }
                }, Layer, Layer + $".Btn.{i}");
                yCoord -= 33;
                container.Add(new CuiElement
                {
                    Parent = Layer + $".Btn.{i}",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"WelcomePanelImage.{i}"), FadeIn = fade },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 3", OffsetMax = "-5 -3" },
                    }
                });
            }
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 0", OffsetMax = "-20 -20" },
                Text = { Text = string.Join("\n", list.description), FontSize = 12, FadeIn = fade }
            }, Layer);
            if (!config.close.closePage || (config.close.closePage && page == config.menu.Count - 1))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = config.close.oMin, OffsetMax = config.close.oMax },
                    Button = { Color = "0 0 0 0.6", Close = Layer + ".Blur" },
                    Text = { Text = config.close.closeText, FontSize = config.close.fSize, Align = TextAnchor.MiddleCenter }
                }, Layer, Layer + ".Btn.Close");
                container.AddRange(CreateOutLine(Layer + ".Btn.Close"));
            }
            CuiHelper.DestroyUi(player, Layer + ".Blur");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Utils
        private CuiElementContainer CreateOutLine(string parent, int size = 2)
        {
            return new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 -{size}" },
                        Image = { Color = MainColor }
                    },
                    parent
                },
                {
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMax = $"0 {size}" },
                        Image = { Color = MainColor }
                    },
                    parent
                },
                {
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"-{size} -{size}", OffsetMax = $"0 {size}" },
                        Image = { Color = MainColor }
                    },
                    parent
                },
                {
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"0 -{size}", OffsetMax = $"{size} {size}" },
                        Image = { Color = MainColor }
                    },
                    parent
                }
            };
        }
        #endregion
    }
}