// WelcomePanel v4.3.6
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using Network;
using System;
using VLB;

namespace Oxide.Plugins
{
    [Info("WelcomePanel", "David", "4.3.6")]
    public class WelcomePanel : RustPlugin
    {   

        #region Dependencies / Api

        [PluginReference] Plugin ImageLibrary, WPKits, WPSocialLinks, WPVipRanks, WPWipeCycle, VoteMap, Shop, ServerRewards, Economics, WipeCountdown;

        private string Img(string url)
        {
            if (ImageLibrary != null)
            {
                if (!(bool)ImageLibrary.Call("HasImage", url))
                    return url;
                else
                    return (string)ImageLibrary?.Call("GetImage", url);
            }
            else
                return url;
        }

        #endregion        

        #region Hooks

        void OnServerInitialized()
        {
            if (config == null)
            {
                NextTick(() => {
                    PrintWarning("Configuration file is not valid, this is usually user error. Please before asking developer for help make sure you validate your json file at https://jsonformatter.curiousconcept.com/");
                    PrintWarning("Unloading WelcomePanel now...");
                    Interface.Oxide.UnloadPlugin("WelcomePanel");
                });
                return;
            }

            if (_canRunApi()) {______________De(); return;}
            
            if (!CheckConfig())
            {
                NextTick(() => {
                    PrintWarning("Unloading WelcomePanel now...");
                    Interface.Oxide.UnloadPlugin("WelcomePanel");
                });
                return;
            }

            if (config.main.onceWipe)
                LoadData();

            RegisterCommands();
            DownloadImages();
            BuildUi();

            /* Dev(); */
        }

        void OnPlayerConnected(BasePlayer player)
        {   
            if (_canRunApi()) {_OnPlayerConnected(player); return;}

            if (config.main.open)
            {
                if (config.main.onceWipe)
                {
                    if (playerData.Contains(player.userID))
                        return;

                    playerData.Add(player.userID);
                }

                OpenMain(player);
                OpenTab(player, config.main.openAt);
            }
        }

        void OnPlayerDisconnected(BasePlayer _player)
        {
            if (_canRunApi() && _player.IsAdmin) _DestroyInputTracker(_player);
        }

        void Unload()
        {   
            if (_canRunApi()) {_Unload(); return;}

            if (config.main.onceWipe)
                SaveData();

            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, $"{Name}_background");
        }

        void OnNewSave()
        {
            LoadData();
            playerData.Clear();
            SaveData();
        }

        #endregion

        #region Functions

        void OpenMain(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, $"{Name}_background");

            if (config.main.tags)
                CuiHelper.AddUi(player, ReplaceTags(player, $"{_main}"));
            else
                CuiHelper.AddUi(player, _main);

            CuiHelper.AddUi(player, _buttons);
            Interface.CallHook("OnWelcomePanelBaseOpen", player);
        }

        void OpenTab(BasePlayer player, int tab)
        {
            CuiHelper.DestroyUi(player, $"{Name}_focus");
            CuiHelper.DestroyUi(player, "wp_content");
            CuiHelper.AddUi(player, _contentPanel);

            if (config.tabs[tab - 1].addon != null && config.tabs[tab - 1].addon != "")
            {
                OpenAddon(player, config.tabs[tab - 1].addon);
                Interface.CallHook("OnWelcomePanelPageOpen", player, tab - 1, 0, config.tabs[tab - 1].addon);
            }
            else
            {   
                if (config.tabs[tab - 1].scrollHeight > 0)
                {   
                    string sv = @"
                    [
                        {
                            ""name"": ""wp_content"",
                            ""parent"":""wp_content"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.RawImage"",
                                    ""sprite"": ""assets/content/effects/crossbreed/fx gradient skewed.png"",
                                    ""color"": ""0 0 0 0"",
                                    ""fadeIn"": 2.5
                                },
                                {
                                    ""type"":""UnityEngine.UI.ScrollView"",
                                    ""contentTransform"": {
                                        ""anchormin"": ""0 1"",
                                        ""anchormax"": ""1 1"",
                                        ""offsetmin"": ""0 -%h%"",
                                        ""offsetmax"": ""0 0""
                                    },
                                    ""vertical"": true,
                                    ""horizontal"": false,
                                    ""movementType"": ""Elastic"",
                                    ""elasticity"": 0.25,
                                    ""inertia"": true,
                                    ""decelerationRate"": 0.3,
                                    ""scrollSensitivity"": 24.0,
                                    ""maskSoftness"": ""0 100"",
                                    ""verticalScrollbar"": {
                                        ""size"": 10,
                                        ""autoHide"": true,
                                        ""trackSprite"": ""assets/content/ui/ui.background.tile.psd"",
                                        ""trackColor"": ""%c2%"",
                                        ""handleColor"": ""%c1%"",
                                        ""highlightColor"": ""%c1%"",
                                        ""pressedColor"": ""%c1%""

                                    }
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0 0"",
                                    ""anchormax"": ""0 0"",
                                    ""offsetmax"": ""0 0""
                                },
                                {
                                    ""type"":""NeedsCursor""
                                }
                            ]
                        }
                    ]";
                    sv = sv.Replace("%h%", config.tabs[tab - 1].scrollHeight.ToString())
                        .Replace("%c1%", (config.main.scrollColor == null || config.main.scrollColor == "" ? "0.69 0.69 0.69 0.1" : config.main.scrollColor))
                        .Replace("%c2%", (config.main.scrollTrackColor == null || config.main.scrollTrackColor == "" ? "0.69 0.69 0.69 0.04" : config.main.scrollTrackColor));
                    CuiHelper.AddUi(player, sv);
                    CuiHelper.AddUi(player, ReplaceTags(player, $"{tabs[tab][0]}"));
                    const string up = @" [ { ""name"": ""wp_content"", ""update"": true, ""components"": [ { ""type"":""RectTransform"", ""anchormin"": ""0 0"", ""anchormax"": ""1 1"" } ] } ]";
                    timer.Once(0.07f, () => { CuiHelper.AddUi(player, up); }); 
                }
                else 
                {
                    CuiHelper.AddUi(player, ReplaceTags(player, $"{tabs[tab][0]}"));
                }

                Interface.CallHook("OnWelcomePanelPageOpen", player, tab - 1, 0, null);
            }

            tab--;
            var btnFocus = new CuiElementContainer();
            Create.Panel(ref btnFocus, $"{Name}_focus", $"{Name}_offset_container", config.main.selectedColor, config.buttons[tab].anchorMin, config.buttons[tab].anchorMax, false, config.baseUi["side"].fade, config.baseUi["side"].fade, config.buttons[tab].mat, "0 0", "0 0");
            Create.Text(ref btnFocus, $"{Name}_focus_text", $"{Name}_focus", "1 1 1 1", config.tabs[tab].name, 12, "0 0", "1 1", config.buttons[tab].align, config.buttons[tab].font, config.baseUi["side"].fade, config.baseUi["side"].fade, "0 0 0 0", "0 0");
            if (config.tabs[tab].icon != null && config.tabs[tab].icon != "")
                Create.Image(ref btnFocus, $"{Name}_focus_img", $"{Name}_focus", Img(config.tabs[tab].icon), "0 0", "1 1", config.baseUi["side"].fade, config.baseUi["side"].fade);

            CuiHelper.AddUi(player, btnFocus);

            if (tabs[tab + 1].Count > 1)
            {
                CuiHelper.DestroyUi(player, $"{Name}_next_button");
                CuiHelper.AddUi(player, $"{_nextButton}".Replace("%tab%", $"{tab + 1}").Replace("%page%", "1"));
            }
        }

        void OpenPage(BasePlayer player, int tab, int page)
        {
            CuiHelper.DestroyUi(player, "wp_content");
            CuiHelper.DestroyUi(player, $"{Name}_text");
            CuiHelper.DestroyUi(player, $"{Name}_next_button");
            CuiHelper.DestroyUi(player, $"{Name}_back_button");

            CuiHelper.AddUi(player, _contentPanel);
            CuiHelper.AddUi(player, ReplaceTags(player, $"{tabs[tab][page]}"));
            Interface.CallHook("OnWelcomePanelPageOpen", player, tab, page, null);

            if (tabs[tab].Count > page + 1)
            {
                CuiHelper.DestroyUi(player, $"{Name}_next_button");
                CuiHelper.AddUi(player, $"{_nextButton}".Replace("%tab%", $"{tab}").Replace("%page%", $"{page + 1}"));
            }

            if (page > 0)
            {
                CuiHelper.DestroyUi(player, $"{Name}_back_button");
                CuiHelper.AddUi(player, $"{_backButton}".Replace("%tab%", $"{tab}").Replace("%page%", $"{page - 1}"));
            }
        }

        void OpenAddon(BasePlayer player, string addon)
        {
            switch (addon.ToLower())
            {
                case "kits":
                    if (WPKits == null)
                    {
                        PrintWarning("Kits addon is not installed or not loaded...");
                        CuiHelper.AddUi(player, $"{_addonWarning}".Replace("%replace%", "Kits addon is not installed or not loaded..."));
                    }
                    else
                        WPKits.Call("ShowKits_Page1_API", player);
                    break;

                case "sociallinks":
                    if (WPSocialLinks == null)
                    {
                        PrintWarning("SocialLinks addon is not installed or not loaded...");
                        CuiHelper.AddUi(player, $"{_addonWarning}".Replace("%replace%", "SocialLinks addon is not installed or not loaded..."));
                    }
                    else
                        WPSocialLinks.Call("ShowLinks_API", player);
                    break;

                case "vipranks":
                    if (WPVipRanks == null)
                    {
                        PrintWarning("VipRanks addon is not installed or not loaded...");
                        CuiHelper.AddUi(player, $"{_addonWarning}".Replace("%replace%", "VipRanks addon is not installed or not loaded..."));
                    }
                    else
                        WPVipRanks.Call("ShowVipRanks_API", player);
                    break;

                case "wipecycle":
                    if (WPWipeCycle == null)
                    {
                        PrintWarning("WipeCycle addon is not installed or not loaded...");
                        CuiHelper.AddUi(player, $"{_addonWarning}".Replace("%replace%", "WipeCycle addon is not installed or not loaded..."));
                    }
                    else
                        WPWipeCycle.Call("ShowWipeCycle_API", player);
                    break;

                case "votemap":
                    if (VoteMap == null)
                    {
                        PrintWarning("VoteMap addon is not installed or not loaded...");
                        CuiHelper.AddUi(player, $"{_addonWarning}".Replace("%replace%", "VoteMap is not installed or not loaded..."));
                    }
                    else
                        VoteMap.Call("ContentCui", player, 1, 0, true);
                    break;

                case "shop":
                    if (Shop == null)
                    {
                        PrintWarning("Shop is not installed or not loaded...");
                        CuiHelper.AddUi(player, $"{_addonWarning}".Replace("%replace%", "Shop is not installed or not loaded..."));
                    }
                    else
                        Shop.Call("ShowShop_API", player);
                    break;

                default:
                    break;
            }
        }

        string ReplaceTags(BasePlayer player, string _text)
        {
            if (!config.main.tags)
                return _text;

            string text = _text;

            if (text.Contains("{playername}"))
            {
                string playerName = player.displayName;
                text = text.Replace("{playername}", playerName);
            }
            if (text.Contains("{pvp/pve}"))
            {
                bool pve = ConVar.Server.pve;
                if (pve)
                    text = text.Replace("{pvp/pve}", "PVE");
                else
                    text = text.Replace("{pvp/pve}", "PVP");
            }
            if (text.Contains("{maxplayers}"))
            {
                string max = $"{(int)ConVar.Server.maxplayers}";
                text = text.Replace("{maxplayers}", max);
            }
            if (text.Contains("{online}"))
            {
                string online = $"{(int)BasePlayer.activePlayerList.Count()}";
                text = text.Replace("{online}", online);
            }
            if (text.Contains("{sleeping}"))
            {
                string sleeping = $"{(int)BasePlayer.sleepingPlayerList.Count()}";
                text = text.Replace("{sleeping}", sleeping);
            }
            if (text.Contains("{joining}"))
            {
                string joining = $"{(int)ServerMgr.Instance.connectionQueue.Joining}";
                text = text.Replace("{joining}", joining);
            }
            if (text.Contains("{queued}"))
            {
                string queued = $"{(int)ServerMgr.Instance.connectionQueue.Queued}";
                text = text.Replace("{queued}", queued);
            }
            if (text.Contains("{worldsize}"))
            {
                string worldsize = $"{(int)ConVar.Server.worldsize}";
                text = text.Replace("{worldsize}", worldsize);
            }
            if (text.Contains("{hostname}"))
            {
                string hostname = ConVar.Server.hostname;
                text = text.Replace("{hostname}", hostname);
            }
            if (WipeCountdown != null)
            {
                if (text.Contains("{wipecountdown}"))
                {
                    string wipe = Convert.ToString(WipeCountdown.CallHook("GetCountdownFormated_API"));
                    text = text.Replace("{wipecountdown}", wipe);
                }
            }
            if (Economics != null)
            {
                if (text.Contains("{economics}"))
                {
                    string playersBalance = $"{Economics.Call<double>("Balance", player.UserIDString)}";
                    text = text.Replace("{economics}", playersBalance);
                }
            }
            if (ServerRewards != null)
            {
                if (text.Contains("{rp}"))
                {
                    string playersRP = $"{ServerRewards?.Call<int>("CheckPoints", player.userID)}";
                    text = text.Replace("{rp}", playersRP);
                }
            }
            return text;
        }


        #endregion

        #region Commands

        void RegisterCommands()
        {
            cmd.AddChatCommand(config.main.cmd, this, "ChatCommand");

            foreach (string command in config.main.addCmds.Keys)
                cmd.AddChatCommand(command, this, "ChatCommand");
        }

        void ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (command.ToLower() == config.main.cmd)
            {
                OpenMain(player);
                OpenTab(player, config.main.openAt);
            }

            if (config.main.addCmds.ContainsKey(command.ToLower()))
            {
                OpenMain(player);
                OpenTab(player, config.main.addCmds[command.ToLower()]);
            }
        }

        [ConsoleCommand("welcomepanellite_tab")]
        private void welcomepanellite_tab(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null) return;
            var args = arg.Args;
            if (args.Length != 1) return;

            int tab = int.Parse(args[0]);
            OpenTab(player, tab);
        }

        [ConsoleCommand("welcomepanellite_page")]
        private void welcomepanellite_page(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null) return;
            var args = arg.Args;
            if (args.Length != 2) return;

            int tab = int.Parse(args[0]);
            int page = int.Parse(args[1]);
            OpenPage(player, tab, page);
        }

        #endregion

        #region Build

        CuiElementContainer _main;
        CuiElementContainer _buttons;
        CuiElementContainer _nextButton;
        CuiElementContainer _backButton;
        CuiElementContainer _contentPanel;
        string _scrollview;
        Dictionary<int, List<CuiElementContainer>> tabs = new Dictionary<int, List<CuiElementContainer>>();

        CuiElementContainer _addonWarning;

        void BuildUi()
        {

            string logo = config.main.logo;
            if (config.main.logo == null || config.main.logo == "")
                logo = config.baseUi["logo"].image;

            string title = config.main.title;
            if (config.main.title == null || config.main.title == "")
                logo = config.baseUi["title"].text;

            var ui = new CuiElementContainer();
            Create.Panel(ref ui, $"{Name}_background", "Overlay", config.baseUi["background"].color, config.baseUi["background"].anchorMin, config.baseUi["background"].anchorMax, true, config.baseUi["background"].fade, config.baseUi["background"].fade, config.baseUi["background"].mat, config.baseUi["background"].offsetMin, config.baseUi["background"].offsetMax, config.main.blockInput);
            if (config.baseUi["background"].image != null && config.baseUi["background"].image != "")
                Create.Image(ref ui, $"{Name}_panel_img", $"{Name}_background", Img(config.baseUi["background"].image), "0 0", "1 1", config.baseUi["background"].fade, config.baseUi["background"].fade);
            Create.Panel(ref ui, $"{Name}_offset_container", $"{Name}_background", config.baseUi["offset_container"].color, config.baseUi["offset_container"].anchorMin, config.baseUi["offset_container"].anchorMax, false, config.baseUi["offset_container"].fade, config.baseUi["offset_container"].fade, config.baseUi["offset_container"].mat, config.baseUi["offset_container"].offsetMin, config.baseUi["offset_container"].offsetMax);
            if (config.baseUi["offset_container"].image != null && config.baseUi["offset_container"].image != "")
                Create.Image(ref ui, $"{Name}_panel_img", $"{Name}_offset_container", Img(config.baseUi["offset_container"].image), "0 0", "1 1", config.baseUi["offset_container"].fade, config.baseUi["offset_container"].fade);
            Create.Panel(ref ui, $"{Name}_main", $"{Name}_offset_container", config.baseUi["main"].color, config.baseUi["main"].anchorMin, config.baseUi["main"].anchorMax, false, config.baseUi["main"].fade, config.baseUi["main"].fade, config.baseUi["main"].mat, config.baseUi["main"].offsetMin, config.baseUi["main"].offsetMax);
            if (config.baseUi["main"].image != null && config.baseUi["main"].image != "")
                Create.Image(ref ui, $"{Name}_panel_img", $"{Name}_main", Img(config.baseUi["main"].image), "0 0", "1 1", config.baseUi["main"].fade, config.baseUi["main"].fade);
            Create.Panel(ref ui, $"{Name}_side", $"{Name}_offset_container", config.baseUi["side"].color, config.baseUi["side"].anchorMin, config.baseUi["side"].anchorMax, false, config.baseUi["side"].fade, config.baseUi["side"].fade, config.baseUi["side"].mat, config.baseUi["side"].offsetMin, config.baseUi["side"].offsetMax);
            if (config.baseUi["side"].image != null && config.baseUi["side"].image != "")
                Create.Image(ref ui, $"{Name}_panel_img", $"{Name}_side", Img(config.baseUi["side"].image), "0 0", "1 1", config.baseUi["side"].fade, config.baseUi["side"].fade);

            if (logo != null && logo != "")
            {
                Create.Panel(ref ui, $"{Name}_logo", $"{Name}_offset_container", config.baseUi["logo"].color, config.baseUi["logo"].anchorMin, config.baseUi["logo"].anchorMax, false, config.baseUi["logo"].fade, config.baseUi["logo"].fade, config.baseUi["logo"].mat, config.baseUi["logo"].offsetMin, config.baseUi["logo"].offsetMax);
                Create.Image(ref ui, $"{Name}_logo_img", $"{Name}_logo", Img(logo), "0 0", "1 1", config.baseUi["side"].fade, config.baseUi["side"].fade);
            }

            Create.Panel(ref ui, $"{Name}_title", $"{Name}_offset_container", config.baseUi["title"].color, config.baseUi["title"].anchorMin, config.baseUi["title"].anchorMax, false, config.baseUi["title"].fade, config.baseUi["title"].fade, config.baseUi["title"].mat, config.baseUi["title"].offsetMin, config.baseUi["title"].offsetMax);
               
            if (title != null && title != "")
            {
                Create.Text(ref ui, $"{Name}_title_text", $"{Name}_title", "1 1 1 1", title, 12, "0 0", "1 1", config.baseUi["title"].align, config.baseUi["title"].font, config.baseUi["title"].fade, config.baseUi["title"].fade, "0 0 0 0", "0 0");
            }

            if (config.baseUi["title"].image != null && config.baseUi["title"].image != "")
                Create.Image(ref ui, $"{Name}_title_img", $"{Name}_title", Img(config.baseUi["title"].image), "0 0", "1 1", config.baseUi["title"].fade, config.baseUi["title"].fade);

            Create.Button(ref ui, $"{Name}_closebtn", $"{Name}_offset_container", config.baseUi["close_button"].color, config.baseUi["close_button"].text, 12, config.baseUi["close_button"].anchorMin, config.baseUi["close_button"].anchorMax, "", $"{Name}_background", "1 1 1 1", config.baseUi["close_button"].fade, config.baseUi["close_button"].align, config.baseUi["close_button"].font, config.baseUi["close_button"].mat);
            if (config.baseUi["close_button"].image != null && config.baseUi["close_button"].image != "")
                Create.Image(ref ui, $"{Name}_panel_img", $"{Name}_closebtn", Img(config.baseUi["close_button"].image), "0 0", "1 1", config.baseUi["close_button"].fade, config.baseUi["close_button"].fade);

            _main = ui;

            var btns = new CuiElementContainer();
            int count = 0;

            foreach (var btn in config.buttons)
            {
                if (count >= config.tabs.Count() || count >= config.buttons.Count())
                    break;

                Create.Button(ref btns, $"{Name}_btn{count}", $"{Name}_offset_container", btn.color, config.tabs[count].name, 12, btn.anchorMin, btn.anchorMax, $"welcomepanellite_tab {count + 1}", "", "1 1 1 1", config.baseUi["side"].fade, btn.align, btn.font, btn.mat);
                Create.Image(ref btns, $"btn_img", $"{Name}_btn{count}", Img(config.tabs[count].icon), "0 0", "1 1", config.baseUi["side"].fade, 0);
                count++;
            }

            _buttons = btns;

            int tabCount = 1;
            foreach (var tab in config.tabs)
            {
                var pages = new List<CuiElementContainer>();

                foreach (var page in tab.text)
                {
                    string _text = "";

                    foreach (var line in page)
                        _text += line + "\n";

                    var textUi = new CuiElementContainer();

                    if (tab.image != null && tab.image != "")
                    {
                        //Create.Panel(ref textUi, $"{Name}_content_imagepanel", "wp_content", "0 0 0 0", "0 0", "1 1", false, config.baseUi["content"].fade, config.baseUi["content"].fade, "assets/icons/iconmaterial.mat");
                        Create.Image(ref textUi, $"{Name}_tab_img", "wp_content", Img(tab.image), "0 0", "1 1", config.baseUi["content"].fade, config.baseUi["content"].fade);
                    }
                    Create.Text(ref textUi, $"{Name}_text", "wp_content", tab.color, _text, tab.size, "0.02 0.02", "0.98 0.98", tab.align, tab.font, config.baseUi["content"].fade, config.baseUi["content"].fade, tab.outlineColor, $"{tab.outline} {tab.outline}");
                    pages.Add(textUi);
                }

                tabs.Add(tabCount, pages);
                tabCount++;
            }

            var content = new CuiElementContainer();
            Create.Panel(ref content, "wp_content", $"{Name}_offset_container", config.baseUi["content"].color, config.baseUi["content"].anchorMin, config.baseUi["content"].anchorMax, false, config.baseUi["content"].fade, config.baseUi["content"].fade, config.baseUi["content"].mat, config.baseUi["content"].offsetMin, config.baseUi["content"].offsetMax);
            if (config.baseUi["content"].image != null && config.baseUi["content"].image != "")
                Create.Image(ref content, $"{Name}_panel_img", "wp_content", Img(config.baseUi["content"].image), "0 0", "1 1", config.baseUi["content"].fade, config.baseUi["content"].fade);

            _contentPanel = content;

            var nextButton = new CuiElementContainer();
            Create.Button(ref nextButton, $"{Name}_next_button", "wp_content", config.baseUi["next_button"].color, config.baseUi["next_button"].text, 12, config.baseUi["next_button"].anchorMin, config.baseUi["next_button"].anchorMax, $"welcomepanellite_page %tab% %page%", "", "1 1 1 1", config.baseUi["next_button"].fade, config.baseUi["next_button"].align, config.baseUi["next_button"].font, config.baseUi["next_button"].mat);
            if (config.baseUi["next_button"].image != null && config.baseUi["next_button"].image != "")
                Create.Image(ref nextButton, $"{Name}_panel_img", $"{Name}_next_button", Img(config.baseUi["next_button"].image), "0 0", "1 1", config.baseUi["next_button"].fade, config.baseUi["next_button"].fade);

            _nextButton = nextButton;

            var backButton = new CuiElementContainer();
            Create.Button(ref backButton, $"{Name}_back_button", "wp_content", config.baseUi["back_button"].color, config.baseUi["back_button"].text, 12, config.baseUi["back_button"].anchorMin, config.baseUi["back_button"].anchorMax, $"welcomepanellite_page %tab% %page%", "", "1 1 1 1", config.baseUi["back_button"].fade, config.baseUi["back_button"].align, config.baseUi["back_button"].font, config.baseUi["back_button"].mat);
            if (config.baseUi["back_button"].image != null && config.baseUi["back_button"].image != "")
                Create.Image(ref backButton, $"{Name}_panel_img", $"{Name}_back_button", Img(config.baseUi["back_button"].image), "0 0", "1 1", config.baseUi["back_button"].fade, config.baseUi["back_button"].fade);

            _backButton = backButton;

            var addonWarning = new CuiElementContainer();
            Create.Text(ref addonWarning, $"{Name}_warning", "wp_content", "1 1 1 1", "%replace%", 15, "0.02 0.02", "0.98 0.98", TextAnchor.MiddleCenter, "robotocondensed-bold.ttf", config.baseUi["content"].fade, config.baseUi["content"].fade, "0 0 0 1", $"1 1");
            _addonWarning = addonWarning;
        }


        void DownloadImages()
        {   
            if (ImageLibrary == null)
            {
                Puts("ImageLibrary not found.");
                return;
            }

            if (config.main.logo != null && config.main.logo != "")
                ImageLibrary.Call("AddImage", config.main.logo, config.main.logo);

            foreach (var tab in config.tabs)
            {
                if (tab.icon != null || tab.icon != "")
                {
                    if (tab.icon.StartsWith("http") || tab.icon.StartsWith("www"))
                        ImageLibrary.Call("AddImage", tab.icon, tab.icon);
                }

                if (tab.image != null || tab.image != "")
                {
                    if (tab.image.StartsWith("http") || tab.image.StartsWith("www"))
                        ImageLibrary.Call("AddImage", tab.image, tab.image);
                }
            }

            foreach (string component in config.baseUi.Keys)
            {
                if (config.baseUi[component].image != null && config.baseUi[component].image != "")
                {
                    if (config.baseUi[component].image.StartsWith("http") || config.baseUi[component].image.StartsWith("www"))
                        ImageLibrary.Call("AddImage", config.baseUi[component].image, config.baseUi[component].image);
                }
            }
        }

        bool CheckConfig()
        {
            string[] required_panels = new string[] {
                "background", "offset_container",
                "main", "side", "content", "title",
                "logo", "close_button", "next_button",
                "back_button",
            };

            foreach (string component in required_panels)
            {
                if (!config.baseUi.ContainsKey(component))
                {
                    NextTick(() => {
                        PrintWarning($"Build failed! Missing \"{component}\" panel.");
                    });
                    return false;
                }
            }

            if (config.main.openAt > config.tabs.Count)
            {
                NextTick(() => {
                    PrintWarning($"Build failed! Panel is set to open at {config.main.openAt}. tab but config contains only {config.tabs.Count} tabs.");
                });
                return false;
            }

            if (config.tabs.Count() > config.buttons.Count())
            {
                NextTick(() => {
                    PrintWarning($"Warning! Your config contains {config.tabs.Count()} tabs while your template supports only {config.buttons.Count()} buttons. Plugin will load but some tabs will not be displayed.");
                });
            }

            return true;
        }

        #endregion

        #region Cui

        public class Create
        {
            public static void Panel(ref CuiElementContainer container, string name, string parent, string color, string anchorMinx, string anchorMax, bool cursorOn = false, float fade = 0f, float fadeOut = 0f, string material = "", string offsetMin = "", string offsetMax = "", bool keyboard = false)
            {
                container.Add(new CuiPanel
                {

                    Image = { Color = color, Material = material, FadeIn = fade },
                    RectTransform = { AnchorMin = anchorMinx, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax },
                    FadeOut = 0f,
                    CursorEnabled = cursorOn,
                    KeyboardEnabled = keyboard,

                },
                parent,
                name);
            }

            public static void Image(ref CuiElementContainer container, string name, string parent, string image, string anchorMinx, string anchorMax, float fade = 0f, float fadeOut = 0f, string offsetMin = "", string offsetMax = "")
            {
                if (image.StartsWith("http") || image.StartsWith("www"))
                {
                    container.Add(new CuiElement
                    {
                        Name = name,
                        Parent = parent,
                        FadeOut = 0f,
                        Components =
                        {
                            new CuiRawImageComponent { Url = image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = fade},
                            new CuiRectTransformComponent { AnchorMin = anchorMinx, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = parent,
                        Components =
                        {
                            new CuiRawImageComponent { Png = image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = fade},
                            new CuiRectTransformComponent { AnchorMin = anchorMinx, AnchorMax = anchorMax }
                        }
                    });
                }
            }

            public static void Text(ref CuiElementContainer container, string name, string parent, string color, string text, int size, string anchorMinx, string anchorMax, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", float fade = 0f, float fadeOut = 0f, string _outlineColor = "0 0 0 0", string _outlineScale = "0 0")
            {
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text,
                            FontSize = size,
                            Font = font,
                            Align = align,
                            Color = color,
                            FadeIn = fade,
                        },

                        new CuiOutlineComponent
                        {

                            Color = _outlineColor,
                            Distance = _outlineScale

                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = anchorMinx,
                             AnchorMax = anchorMax
                        }
                    },
                    FadeOut = 0f
                });
            }

            public static void Button(ref CuiElementContainer container, string name, string parent, string color, string text, int size, string anchorMinx, string anchorMax, string command = "", string _close = "", string textColor = "0.843 0.816 0.78 1", float fade = 1f, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", string material = "assets/content/ui/uibackgroundblur-ingamemenu.mat")
            {
                container.Add(new CuiButton
                {
                    Button = { Close = _close, Command = command, Color = color, Material = material, FadeIn = fade },
                    RectTransform = { AnchorMin = anchorMinx, AnchorMax = anchorMax },
                    Text = { Text = text, FontSize = size, Align = align, Color = textColor, Font = font, FadeIn = fade }
                },
                parent,
                name);
            }
        }

        #endregion

        #region Player Data

        private void SaveData()
        {
            if (playerData != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerData", playerData);
        }

        private List<ulong> playerData;

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/PlayerData"))
            {
                playerData = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>($"{Name}/PlayerData");
            }
            else
            {
                playerData = new List<ulong>();
                SaveData();
            }
        }

        #endregion

        #region Config

        private Configuration config;

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
            }
            catch
            {
                //
            }

            if (config.apiAccess == null)
            {
                config.apiAccess = new Configuration.APIAccess
                {
                    key = null,
                    token = null
                };
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.CreateConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {
            [JsonProperty(PropertyName = "Main Settings")]
            public Main main { get; set; }

            public class Main
            {
                [JsonProperty("Open when player joins")]
                public bool open { get; set; }

                [JsonProperty("Open once per wipe")]
                public bool onceWipe { get; set; }

                [JsonProperty("Open at tab")]
                public int openAt { get; set; }

                [JsonProperty("Block movement when browsing panel")]
                public bool blockInput { get; set; }

                [JsonProperty("Enable text tags")]
                public bool tags { get; set; }

                [JsonProperty("Server Title")]
                public string title { get; set; }

                [JsonProperty("Server Logo")]
                public string logo { get; set; }

                [JsonProperty("Scroll Bar Color")]
                public string scrollColor { get; set; }

                [JsonProperty("Scroll Track Color")]
                public string scrollTrackColor { get; set; }

                [JsonProperty("Selected button color")]
                public string selectedColor { get; set; }

                [JsonProperty("Base Command")]
                public string cmd { get; set; }

                [JsonProperty("Additional Commands")]
                public Dictionary<string, int> addCmds { get; set; }

            }

            [JsonProperty(PropertyName = "Text Tabs")]
            public List<Tab> tabs { get; set; }

            public class Tab
            {
                [JsonProperty("Name")]
                public string name { get; set; }

                [JsonProperty("Icon")]
                public string icon { get; set; }

                [JsonProperty("Font Size")]
                public int size { get; set; }

                [JsonProperty("Font Color")]
                public string color { get; set; }

                [JsonProperty("Font Outline Color")]
                public string outlineColor { get; set; }

                [JsonProperty("Font Outline Thickness")]
                public string outline { get; set; }

                [JsonProperty("Font")]
                public string font { get; set; }

                [JsonProperty("Text Background Image")]
                public string image { get; set; }

                [JsonProperty("Text Alignment")]
                public TextAnchor align { get; set; }

                [JsonProperty("ScrollView Height")]
                public int scrollHeight { get; set; }

                [JsonProperty("Text Lines")]
                public List<string[]> text { get; set; }

                [JsonProperty("Addon (plugin name)")]
                public string addon { get; set; }

            }

            [JsonProperty(PropertyName = "Base Ui Elements")]
            public Dictionary<string, BaseUi> baseUi { get; set; }

            public class BaseUi
            {
                [JsonProperty("Color")]
                public string color { get; set; }

                [JsonProperty("Material")]
                public string mat { get; set; }

                [JsonProperty("Image")]
                public string image { get; set; }

                [JsonProperty("Anchor Min")]
                public string anchorMin { get; set; }

                [JsonProperty("Anchor Max")]
                public string anchorMax { get; set; }

                [JsonProperty("Offset Min")]
                public string offsetMin { get; set; }

                [JsonProperty("Offset Max")]
                public string offsetMax { get; set; }

                [JsonProperty("Fade")]
                public float fade { get; set; }

                [JsonProperty("Text (not for panels)")]
                public string text { get; set; }

                [JsonProperty("Text Alignment (not for panels)")]
                public TextAnchor align { get; set; }

                [JsonProperty("Text Font (not for panels)")]
                public string font { get; set; }

            }

            [JsonProperty(PropertyName = "Tab Buttons")]
            public List<TabButtons> buttons { get; set; }

            public class TabButtons
            {
                [JsonProperty("Color")]
                public string color { get; set; }

                [JsonProperty("Text Alignment")]
                public TextAnchor align { get; set; }

                [JsonProperty("Text Font")]
                public string font { get; set; }

                [JsonProperty("Anchor Min")]
                public string anchorMin { get; set; }

                [JsonProperty("Anchor Max")]
                public string anchorMax { get; set; }

                [JsonProperty("Material")]
                public string mat { get; set; }

            }

            [JsonProperty(PropertyName = "Build API (optional)")]
            public APIAccess apiAccess { get; set; }

            public class APIAccess
            {
                [JsonProperty("Key")]
                public string key { get; set; }

                [JsonProperty("Token")]
                public string token { get; set; }
            }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {
                    main = new WelcomePanel.Configuration.Main
                    {
                        open = true,
                        onceWipe = false,
                        openAt = 1,
                        blockInput = true,
                        tags = true,
                        scrollColor = "1 1 1 0.08",
                        scrollTrackColor = "1 1 1 0.02",
                        selectedColor = "0.161 0.384 0.569 1",
                        title = "<size=60>RUSTSERVER.NET</size>",
                        logo = "https://rustplugins.net/products/welcomepanellite/1/logo.png",
                        cmd = "info",
                        addCmds = new Dictionary<string, int>()
                        {
                            {"rules", 2},
                            {"help", 3}
                        }
                    },
                    tabs = new List<WelcomePanel.Configuration.Tab>
                    {
                        new WelcomePanel.Configuration.Tab
                        {
                            name = "HOME",
                            icon = "https://rustplugins.net/products/welcomepanellite/1/home_button.png",
                            size = 12,
                            color = "1 1 1 1",
                            outlineColor = "0 0 0 1",
                            outline = "0.5",
                            font = "robotocondensed-bold.ttf",
                            image = "",
                            align = TextAnchor.MiddleCenter,
                            text = new List<string[]>{
                                new string[]
                                {
                                    "<size=45><color=#4A95CC>RUSTSERVERNAME</color> #4 </size> ",
                                    "<size=25>WIPE SCHEDULE <color=#83b8c7>WEEKLY</color> @ <color=#83b8c7>4:00PM</color> (CET)</size>",
                                    "<size=25>RATES <color=#83b8c7>2x GATHER</color> | <color=#83b8c7>1.5x LOOT</color></size> ",
                                    "<size=25>GROUP LIMIT <color=#83b8c7>MAX 5</color></size>",
                                    "<size=25>MAPSIZE <color=#83b8c7>3500</color></size> ",
                                    "\n",
                                    "\n",
                                    "<size=15>Server is located in EU. Blueprints are wiped monthly. Feel free to browse our infomation panel to find out more about the server. If you have more questions, please join our discord and we will happy to help you.</size>",
                                    "\n",
                                    "<size=15><color=#83b8c7>\n This is demo page for Welcome Panel, you can find more examples by checking other tabs.</color></size>"
                                }
                            },
                            addon = "",
                        },
                        new WelcomePanel.Configuration.Tab
                        {
                            name = " RULES",
                            icon = "https://rustplugins.net/products/welcomepanellite/1/rules_button.png",
                            size = 12,
                            color = "1 1 1 1",
                            outlineColor = "0 0 0 1",
                            outline = "0.5",
                            font = "robotocondensed-regular.ttf",
                            image = "",
                            align = TextAnchor.MiddleCenter,
                            text = new List<string[]>{
                                new string[]
                                {
                                    "<size=45><color=#4A95CC>Text Alignment</color></size>",
                                    "",
                                    "<size=18>You can set various text alignments inside config file.</size>",
                                    "<size=18>There is 9 available settings, each one is defined by number (0 to 8)</size>",
                                    "",
                                    "<size=17>UpperLeft - <color=#4A95CC>0</color></size>\n<size=17>UpperCenter - <color=#4A95CC>1</color></size>\n<size=17>UpperRight - <color=#4A95CC>2</color></size>",
                                    "<size=17>MiddleLeft - <color=#4A95CC>3</color></size>\n<size=17>MiddleCenter - <color=#4A95CC>4</color></size>\n<size=17>MiddleRight - <color=#4A95CC>5</color></size>",
                                    "<size=17>LowerLeft - <color=#4A95CC>6</color></size>\n<size=17>LowerCenter - <color=#4A95CC>7</color></size>\n<size=17>LowerRight - <color=#4A95CC>8</color></size>",
                                    "",
                                    ""
                                }
                            },
                            addon = "",
                        },
                        new WelcomePanel.Configuration.Tab
                        {
                            name = "   WIPE CYCLE",
                            icon = "https://rustplugins.net/products/welcomepanellite/1/wipe_button.png",
                            size = 12,
                            color = "1 1 1 1",
                            outlineColor = "0 0 0 1",
                            outline = "0.5",
                            font = "robotocondensed-regular.ttf",
                            image = "https://rustplugins.net/products/welcomepanellite/1/richtext.png",
                            align = TextAnchor.MiddleLeft,
                            text = new List<string[]>{
                                new string[]
                                {
                                    "<size=45><color=#4A95CC><b>Text Style</b></color></size>",
                                    "Text in Rust plugins can be styled with standard rich text tags, it's similar to HTML. Every tag must be closed at the end of text line, otherwise none of the tags will work and they will be shown as regular text. If you ever encounter this issue, please do not reach out to support for help and double check your text lines instead.",
                                    "",
                                    "  Available Tags",
                                    "\n\n\n\n\n\n\n\n\n",
                                    "  Available Fonts <size=9>(fonts are applied to whole page)</size>\n",
                                    "    <b>robotocondensed-bold.ttf</b>",
                                    "    <b>robotocondensed-regular.ttf</b>",
                                    "    <b>permanentmarker.ttf</b>",
                                    "    <b>droidsansmono.ttf</b>"
                                }
                            },
                            addon = "",
                        },
                        new WelcomePanel.Configuration.Tab
                        {
                            name = "  SUPPORT",
                            icon = "https://rustplugins.net/products/welcomepanellite/1/admin_button.png",
                            size = 12,
                            color = "1 1 1 1",
                            outlineColor = "0 0 0 1",
                            outline = "0.5",
                            font = "robotocondensed-regular.ttf",
                            image = "https://rustplugins.net/products/welcomepanellite/1/enableaddons.png",
                            align = TextAnchor.UpperLeft,
                            text = new List<string[]>{
                                new string[]
                                {
                                    "<size=45><color=#4A95CC><b>How to enable addons</b></color></size>",
                                    "Each tab has addon option right under text line. Simply put addon name in there.",
                                    "You can find list of addon names in plugin description."
                                }
                            },
                            addon = "",
                        },
                        new WelcomePanel.Configuration.Tab
                        {
                            name = "KITS",
                            icon = "https://rustplugins.net/products/welcomepanellite/1/kits_button.png",
                            size = 12,
                            color = "1 1 1 1",
                            outlineColor = "0 0 0 1",
                            outline = "0.5",
                            font = "robotocondensed-regular.ttf",
                            image = "",
                            align = TextAnchor.MiddleLeft,
                            text = new List<string[]>{
                                new string[]
                                {
                                    "This text won't be displayed because",
                                    "addon is assigned to this tab."
                                }
                            },
                            addon = "kits",
                        },
                        new WelcomePanel.Configuration.Tab
                        {
                            name = "   COMMANDS",
                            icon = "https://rustplugins.net/products/welcomepanellite/1/star_button.png",
                            size = 12,
                            color = "1 1 1 1",
                            outlineColor = "0 0 0 1",
                            outline = "0.5",
                            font = "robotocondensed-regular.ttf",
                            image = "https://rustplugins.net/products/welcomepanellite/1/multiplepages.png",
                            align = TextAnchor.UpperLeft,
                            text = new List<string[]>{
                                new string[]
                                {
                                    "<size=45><color=#4A95CC><b>How to add multiple pages</b></color></size>",
                                    "You can add unlimited amount of pages, check image bellow or config file for example."
                                },
                                new string[]
                                {
                                    "<size=45><color=#4A95CC><b>This is page number 2</b></color></size>",
                                    "You can add unlimited amount of pages, check image bellow or config file for example."
                                },
                                new string[]
                                {
                                    "<size=45><color=#4A95CC><b>This is page number 3</b></color></size>",
                                    "You can add unlimited amount of pages, check image bellow or config file for example."
                                }
                            },
                            addon = "",
                        },
                        new WelcomePanel.Configuration.Tab
                        {
                            name = " DISCORD",
                            icon = "https://rustplugins.net/products/welcomepanellite/1/discord.png",
                            size = 12,
                            color = "1 1 1 1",
                            outlineColor = "0 0 0 1",
                            outline = "1",
                            font = "robotocondensed-regular.ttf",
                            image = "",
                            align = TextAnchor.MiddleCenter,
                            text = new List<string[]>{
                                new string[]
                                {
                                    "<size=25>discord.gg/<color=#c14229><b><size=22>RUSTPLUGINS</size></b></color></size>",
                                    "\n\n\n",
                                    "Full documentation at <b>docs.rustplugins.net/welcomepanel</b>"
                                }
                            },
                            addon = "",
                        },
                    },
                    baseUi = new Dictionary<string, WelcomePanel.Configuration.BaseUi>
                    {
                        {"background", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "0 0 0 0.8",
                                mat = "assets/content/ui/uibackgroundblur.mat",
                                image = "",
                                anchorMin = "0 0",
                                anchorMax = "1 1",
                                offsetMin = "0 0",
                                offsetMax = "0 0",
                                fade = 0.2f,
                                text = null,
                                align = TextAnchor.MiddleCenter,
                                font = null
                            }
                        },
                        {"offset_container", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "0 0 0 0",
                                mat = "assets/icons/iconmaterial.mat",
                                image = "",
                                anchorMin = "0.5 0.5",
                                anchorMax = "0.5 0.5",
                                offsetMin = "-680 -360",
                                offsetMax = "680 360",
                                fade = 0.2f,
                                text = null,
                                align = TextAnchor.MiddleCenter,
                                font = null
                            }
                        },
                        {"main", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "0.70 0.67 0.65 0.07",
                                mat = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                                image = "",
                                anchorMin = "0.315 0.175",
                                anchorMax = "0.80 0.748",
                                offsetMin = "0 0",
                                offsetMax = "0 0",
                                fade = 0.2f,
                                text = null,
                                align = TextAnchor.MiddleCenter,
                                font = null
                            }
                        },
                        {"side", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "0.70 0.67 0.65 0.07",
                                mat = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                                image = "",
                                anchorMin = "0.187 0.175",
                                anchorMax = "0.308 0.748",
                                offsetMin = "0 0",
                                offsetMax = "0 0",
                                fade = 0.2f,
                                text = null,
                                align = TextAnchor.MiddleCenter,
                                font = null
                            }
                        },
                        {"content", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "0 0 0 0.0",
                                mat = "assets/icons/iconmaterial.mat",
                                image = "",
                                anchorMin = "0.32 0.182",
                                anchorMax = "0.795 0.740",
                                offsetMin = "0 0",
                                offsetMax = "0 0",
                                fade = 0.0f,
                                text = null,
                                align = TextAnchor.MiddleCenter,
                                font = null
                            }
                        },
                        {"title", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "0 0 0 0",
                                mat = "assets/icons/iconmaterial.mat",
                                image = "",
                                anchorMin = "0.185 0.745",
                                anchorMax = "0.9 0.85",
                                offsetMin = "0 0",
                                offsetMax = "0 0",
                                fade = 0.2f,
                                text = null,
                                align = TextAnchor.MiddleLeft,
                                font = "robotocondensed-bold.ttf"
                            }
                        },
                        {"logo", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "0 0 0 0",
                                mat = "assets/icons/iconmaterial.mat",
                                image = null,
                                anchorMin = "0.507 0.760",
                                anchorMax = "0.545 0.833",
                                offsetMin = "0 0",
                                offsetMax = "0 0",
                                fade = 0.2f,
                                text = null,
                                align = TextAnchor.MiddleCenter,
                                font = null,
                            }
                        },
                        {"close_button", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "0.757 0.259 0.161 1.00",
                                mat = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                                image = "",
                                anchorMin = "0.73 0.125",
                                anchorMax = "0.80 0.16",
                                offsetMin = "0 0",
                                offsetMax = "0 0",
                                fade = 0.2f,
                                text = "✘ CLOSE",
                                align = TextAnchor.MiddleCenter,
                                font = "robotocondensed-bold.ttf",
                            }
                        },
                        {"next_button", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "1 1 1 0.30",
                                mat = "assets/icons/iconmaterial.mat",
                                image = "https://rustplugins.net/products/welcomepanellite/1/next.png",
                                anchorMin = "0.953 0.013",
                                anchorMax = "0.995 0.06",
                                offsetMin = "0 0",
                                offsetMax = "0 0",
                                fade = 0.0f,
                                text = "",
                                align = TextAnchor.MiddleCenter,
                                font = "robotocondensed-bold.ttf",
                            }
                        },
                        {"back_button", new WelcomePanel.Configuration.BaseUi
                            {
                                color = "1 1 1 0.30",
                                mat = "assets/icons/iconmaterial.mat",
                                image = "https://rustplugins.net/products/welcomepanellite/1/back.png",
                                anchorMin = "0.902 0.013",
                                anchorMax = "0.943 0.06",
                                offsetMin = "0 0",
                                offsetMax = "0 0",
                                fade = 0.0f,
                                text = "",
                                align = TextAnchor.MiddleCenter,
                                font = "robotocondensed-bold.ttf",
                            }
                        }
                    },
                    buttons = new List<WelcomePanel.Configuration.TabButtons>
                    {
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.700",
                            anchorMax = "0.3095 0.748",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.650",
                            anchorMax = "0.3095 0.697",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.600",
                            anchorMax = "0.3095 0.647",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.550",
                            anchorMax = "0.3095 0.597",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.500",
                            anchorMax = "0.3095 0.547",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.450",
                            anchorMax = "0.3095 0.497",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.400",
                            anchorMax = "0.3095 0.447",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.350",
                            anchorMax = "0.3095 0.397",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.300",
                            anchorMax = "0.3095 0.347",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                        new WelcomePanel.Configuration.TabButtons
                        {
                            color = "0 0 0 0",
                            align = TextAnchor.MiddleCenter,
                            font = "robotocondensed-bold.ttf",
                            anchorMin = "0.1845 0.250",
                            anchorMax = "0.3095 0.297",
                            mat = "assets/icons/iconmaterial.mat"
                        },
                    },
                    apiAccess = new WelcomePanel.Configuration.APIAccess
                    {
                        key = null,
                        token = null,
                    }
                };
            }
        }
        #endregion

        #region Build API

        private List<string>_N0=new List<string>();private string __IG;private string _____Y3;private string[]___zu;private string[]____cd;private string ______6M;private List<string[]>_______Ri=new List<string[]>();private Dictionary<string,int[]>________Cw=new Dictionary<string,int[]>(StringComparer.InvariantCultureIgnoreCase);private List<bool>_________Dp;private bool __________o1=false;bool _canRunApi(){return config.apiAccess!=null&&config.apiAccess.key!=null;}private void ______________De(){____________IK();LoadData();webrequest.Enqueue($"https://wp.rustplugins.io/api/v2/welcomepanel/build",null,(________________________________________________tE,_________________________________________________jb)=>{if(________________________________________________tE==401){Puts("Wrong license key!");return;}if(________________________________________________tE!=200){Puts("Connection error, trying again in 60 seconds...");timer.Once(60f,()=>______________De());return;}if(________________________________________________tE==200){if(_________________________________________________jb==null){Puts("Config is empty.");return;}___________________________________________________GG(_________________________________________________jb,true);}},this,RequestMethod.GET,new Dictionary<string,string>{{"Authorization",$"Bearer {config.apiAccess.key}"}},200f);}private void _Unload(){try{foreach(var ____________________________________________________________Bo in BasePlayer.activePlayerList){CuiHelper.DestroyUi(____________________________________________________________Bo,"background");if(____________________________________________________________Bo.IsAdmin){CuiHelper.DestroyUi(____________________________________________________________Bo,"wpanel.overlay");_DestroyInputTracker(____________________________________________________________Bo);}}if(_________Dp[0])SaveData();}catch{}}private void _OnPlayerConnected(BasePlayer ____________________________________________________________Bo){if(!__________o1)return;if(_________Dp[1]){if(_________Dp[0]){if(playerData.Contains(____________________________________________________________Bo.userID))return;playerData.Add(____________________________________________________________Bo.userID);_______________________________________________________________bh(____________________________________________________________Bo);____________________________srPage(____________________________________________________________Bo,0,0,true);return;}_______________________________________________________________bh(____________________________________________________________Bo);____________________________srPage(____________________________________________________________Bo,0,0,true);}}void _DestroyInputTracker(BasePlayer player){var script=player.gameObject.GetComponent<InputTracker>();UnityEngine.Object.Destroy(script);CuiHelper.DestroyUi(player,"wp.edit.toolbar");}void ____________IK(){string[]list={"https://tools.rustplugins.io/position-icon.png","https://tools.rustplugins.io/color-icon.png","https://tools.rustplugins.io/images-icon.png","https://tools.rustplugins.io/move-icon.png","https://tools.rustplugins.io/offsets-icon.png","https://tools.rustplugins.io/resize-icon.png","https://tools.rustplugins.io/exit.png","https://tools.rustplugins.io/welcomepanel-key.png"};foreach(string url in list){if(!(bool)ImageLibrary.Call("HasImage",url))ImageLibrary.Call("AddImage",url,url);}}void ___________________________________________________GG(string data,bool init=false){if(data==null)return;string ___________GfTemp=data;var __________________________________________________Gx=___________GfTemp.Split(new string[]{"/*=*/"},StringSplitOptions.None);______________________________________________________dW($"{__________________________________________________Gx[3]}");__IG="["+_____________________________________________________OA(__________________________________________________Gx[0])+"]";_____Y3="["+_____________________________________________________OA(__________________________________________________Gx[1])+"]";___zu=_____________________________________________________OA(__________________________________________________Gx[5]).Split(new string[]{"?*?"},StringSplitOptions.None);____cd=_____________________________________________________OA(__________________________________________________Gx[6]).Split(new string[]{"?*?"},StringSplitOptions.None);______6M=_____________________________________________________OA(__________________________________________________Gx[7]);for(int i=8;i<__________________________________________________Gx.Count();i++){string[]_____________________________________________________________x3=_____________________________________________________OA(__________________________________________________Gx[i]).Split(new string[]{"/*nextpage*/"},StringSplitOptions.None);_______Ri.Add(_____________________________________________________________x3);}JObject acc=JObject.Parse($"{__________________________________________________Gx[2]}");int i_=0;_________Dp=new List<bool>();foreach(var o in acc){if(i_!=0)_________Dp.Add(Convert.ToBoolean(o.Value));i_++;}foreach(var c in acc["commands"])if(!________Cw.ContainsKey(c["command"].ToString()))________Cw.Add(c["command"].ToString(),new int[]{Convert.ToInt32(c["tab"])-1,Convert.ToInt32(c["page"])-1});if(init){__________________________________________________________WA();_______________HK();}int countr=0;try{for(int i=0;i<_______Ri.Count;i++){for(int _i=0;_i<_______Ri[i].Length;i++){foreach(string __________________________________________________________BH in _N0){if(_______Ri[i][_i].Contains("{"+__________________________________________________________BH+"}"))_______Ri[i][_i]=_______Ri[i][_i].Replace("{"+__________________________________________________________BH+"}"," ");}}}}catch{}if(init)Puts("Configuration loaded.");else Puts("New build downloaded.");__________o1=true;}private void __________________________________________________________WA(){foreach(string ___________________________________________________________Fr in ________Cw.Keys){if(___________________________________________________________Fr.Contains(" "))continue;cmd.AddChatCommand(___________________________________________________________Fr,this,"_____________6E");}}void _____________6E(BasePlayer ____________________________________________________________Bo,string command,string[]args){if(________Cw.ContainsKey(command)){_______________________________________________________________bh(____________________________________________________________Bo);____________________________srPage(____________________________________________________________Bo,________Cw[command][0],________Cw[command][1],true);}}private void _______________HK(){if(!_________Dp[2])return;for(var i=1;i<_______Ri.Count;i++){if(!permission.PermissionExists("welcomepanel.tab."+i))permission.RegisterPermission("welcomepanel.tab."+i,this);}}private void ______________________________________________________dW(string imagedata){if(ImageLibrary==null){Puts("ImageLibrary not found!");return;}var images=imagedata.Split(new string[]{"?*?"},StringSplitOptions.None);foreach(string __________________________________________________________BH in images){if(!__________________________________________________________BH.StartsWith("http"))continue;bool saved=(bool)ImageLibrary.Call("HasImage",__________________________________________________________BH);if(!saved)ImageLibrary.Call("AddImage",__________________________________________________________BH,__________________________________________________________BH);_N0.Add(__________________________________________________________BH);}}private string _____________________________________________________OA(string ui){string _ui=ui;foreach(string __________________________________________________________BH in _N0){if(!(bool)ImageLibrary.Call("HasImage",__________________________________________________________BH))continue;string find=$"\"url\": \"{__________________________________________________________BH}\",";string put=$"\"png\": \"{(string)ImageLibrary.Call("GetImage",__________________________________________________________BH)}\",";_ui=_ui.Replace(find,put);}return _ui;}private string __________________________________________________________________nb(BasePlayer ____________________________________________________________Bo,string _text){string text=_text;if(text.Contains("{playername}")){string playerName=____________________________________________________________Bo.displayName;text=text.Replace("{playername}",playerName);}if(text.Contains("{maxplayers}")){string max=$"{(int)ConVar.Server.maxplayers}";text=text.Replace("{maxplayers}",max);}if(text.Contains("{online}")){string online=$"{(int)BasePlayer.activePlayerList.Count()}";text=text.Replace("{online}",online);}if(text.Contains("{sleeping}")){string sleeping=$"{(int)BasePlayer.sleepingPlayerList.Count()}";text=text.Replace("{sleeping}",sleeping);}if(text.Contains("{joining}")){string joining=$"{(int)ServerMgr.Instance.connectionQueue.Joining}";text=text.Replace("{joining}",joining);}if(text.Contains("{queued}")){string queued=$"{(int)ServerMgr.Instance.connectionQueue.Queued}";text=text.Replace("{queued}",queued);}if(text.Contains("{worldsize}")){string worldsize=$"{(int)ConVar.Server.worldsize}";text=text.Replace("{worldsize}",worldsize);}if(text.Contains("{hostname}")){string hostname=ConVar.Server.hostname;text=text.Replace("{hostname}",hostname);}if(WipeCountdown!=null){if(text.Contains("{wipecountdown}")){string wipe=Convert.ToString(WipeCountdown.CallHook("GetCountdownFormated_API"));text=text.Replace("{wipecountdown}",wipe);}}if(Economics!=null){if(text.Contains("{economics}")){string playersBalance=$"{Economics.Call<double>("Balance",____________________________________________________________Bo.UserIDString)}";text=text.Replace("{economics}",playersBalance);}}if(ServerRewards!=null){if(text.Contains("{rp}")){string playersRP=$"{ServerRewards?.Call<int>("CheckPoints",____________________________________________________________Bo.userID)}";text=text.Replace("{rp}",playersRP);}}return text;}private void _______________________________________________________________bh(BasePlayer ____________________________________________________________Bo){if(!__________o1){Puts("WelcomePanel didn't finish loading yet.");return;}CuiHelper.DestroyUi(____________________________________________________________Bo,"background");CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo{connection=____________________________________________________________Bo.net.connection},null,"AddUI",__________________________________________________________________nb(____________________________________________________________Bo,__IG));CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo{connection=____________________________________________________________Bo.net.connection},null,"AddUI",_____Y3);Interface.CallHook("OnWelcomePanelBaseOpen",____________________________________________________________Bo);}private void ____________________________srPage(BasePlayer ____________________________________________________________Bo,int ______________________________________________________________________Is,int _______________________________________________________________________d7,bool tabSwap=false){if(!__________o1){Puts("WelcomePanel didn't finish loading yet.");return;}CuiHelper.DestroyUi(____________________________________________________________Bo,"wp_content");if(tabSwap){CuiHelper.DestroyUi(____________________________________________________________Bo,"btn_highlight");if(___zu.Length>______________________________________________________________________Is){CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo{connection=____________________________________________________________Bo.net.connection},null,"AddUI","["+___zu[______________________________________________________________________Is]+"]");}}CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo{connection=____________________________________________________________Bo.net.connection},null,"AddUI","["+______6M+"]");if(_________Dp[2]&&!permission.UserHasPermission(____________________________________________________________Bo.UserIDString,"welcomepanel.tab."+(______________________________________________________________________Is+1))){CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo{connection=____________________________________________________________Bo.net.connection},null,"AddUI","[{\"name\": \"text_\",\"parent\": \"wp_content\",\"components\": [{\"type\": \"UnityEngine.UI.Text\",\"text\": \"You don't have permission to view this tab.\",\"fontSize\": 14,\"align\": \"MiddleCenter\",\"color\": \"1 1 1 1\"}]}]");return;}string p=@"\{%\w+%\}";var m=Regex.Matches(_______Ri[______________________________________________________________________Is][_______________________________________________________________________d7],p);string a="";if(m.Count>0){a=m[0].Value.Replace("{%","").Replace("%}","");CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo{connection=____________________________________________________________Bo.net.connection},null,"AddUI","["+__________________________________________________________________nb(____________________________________________________________Bo,_______Ri[______________________________________________________________________Is][_______________________________________________________________________d7].Replace(m[0].Value,""))+"]");}else{CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo{connection=____________________________________________________________Bo.net.connection},null,"AddUI","["+__________________________________________________________________nb(____________________________________________________________Bo,_______Ri[______________________________________________________________________Is][_______________________________________________________________________d7])+"]");}Interface.CallHook("OnWelcomePanelPageOpen",____________________________________________________________Bo,______________________________________________________________________Is,_______________________________________________________________________d7,a);if(_______________________________________________________________________d7!=0){CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo{connection=____________________________________________________________Bo.net.connection},null,"AddUI","["+____cd[0].Replace("{pagechangecommand}",$"welcomepanel_page {______________________________________________________________________Is} {_______________________________________________________________________d7-1}")+"]");}if(_______Ri[______________________________________________________________________Is].Count()-1>_______________________________________________________________________d7){if(String.IsNullOrEmpty(_______Ri[______________________________________________________________________Is][_______________________________________________________________________d7+1]))return;CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo{connection=____________________________________________________________Bo.net.connection},null,"AddUI","["+____cd[1].Replace("{pagechangecommand}",$"welcomepanel_page {______________________________________________________________________Is} {_______________________________________________________________________d7+1}")+"]");}}[ConsoleCommand("welcomepanel_tab")]private void welcomepanel_tab(ConsoleSystem.Arg arg){var ____________________________________________________________Bo=arg?.Player();var args=arg.Args;if(____________________________________________________________Bo==null)return;if(args.Length<1)return;____________________________srPage(____________________________________________________________Bo,Convert.ToInt32(args[0]),0,true);}[ConsoleCommand("welcomepanel_page")]private void welcomepanel_page(ConsoleSystem.Arg arg){var ____________________________________________________________Bo=arg?.Player();var args=arg.Args;if(____________________________________________________________Bo==null)return;if(args.Length<2)return;____________________________srPage(____________________________________________________________Bo,Convert.ToInt32(args[0]),Convert.ToInt32(args[1]));}[ConsoleCommand("welcomepanel_close")]private void welcomepanel_close(ConsoleSystem.Arg arg){var ____________________________________________________________Bo=arg?.Player();if(____________________________________________________________Bo==null)return;if(____________________________________________________________Bo.IsAdmin){var script=____________________________________________________________Bo.gameObject.GetComponent<InputTracker>();if(script!=null){return;}}CuiHelper.DestroyUi(____________________________________________________________Bo,"background");CuiHelper.DestroyUi(____________________________________________________________Bo,"content");Interface.CallHook("OnWelcomePanelClose",____________________________________________________________Bo);}[ConsoleCommand("wpreload")]private void reloadcmd(ConsoleSystem.Arg arg){var ____________________________________________________________Bo=arg?.Player();var args=arg.Args;if(____________________________________________________________Bo!=null){if(!____________________________________________________________Bo.IsAdmin)return;_N0=new List<string>();___zu=null;____cd=null;_______Ri=new List<string[]>();________Cw=new Dictionary<string,int[]>();_________Dp=null;______________De();timer.Once(1.5f,()=>{_______________________________________________________________bh(____________________________________________________________Bo);____________________________srPage(____________________________________________________________Bo,0,0,true);});}else{_N0=new List<string>();___zu=null;____cd=null;_______Ri=new List<string[]>();________Cw=new Dictionary<string,int[]>();_________Dp=null;______________De();if(args.Length>0){var player=BasePlayer.FindByID(Convert.ToUInt64(args[0]));if(player!=null){________________c5(player);timer.Once(1.5f,()=>{CuiHelper.DestroyUi(player,"modal.overlay");_______________________________________________________________bh(player);____________________________srPage(player,0,0,true);});}}}}[ConsoleCommand("wp.wip")]private void wpwip(ConsoleSystem.Arg arg){var ____________________________________________________________Bo=arg?.Player();if(____________________________________________________________Bo!=null){if(!____________________________________________________________Bo.IsAdmin)return;ShowNotice(____________________________________________________________Bo,"Oh no...","This feature is still in development.");}}void ________________c5(BasePlayer player){var c=new CuiElementContainer();c.Add(new CuiPanel{Image={Color="0.70 0.67 0.65 0.3",Material="assets/content/ui/uibackgroundblur.mat",FadeIn=0.3f},RectTransform={AnchorMin="0 0",AnchorMax="1 1"},FadeOut=0f,CursorEnabled=true,KeyboardEnabled=true},"Overlay","modal.overlay");c.Add(new CuiElement{Parent="modal.overlay",Components={new CuiImageComponent{Material="assets/icons/iconmaterial.mat",Sprite="assets/content/ui/ui.background.transparent.radial.psd",Color="0 0 0 0.93",FadeIn=0.3f},new CuiRectTransformComponent{AnchorMin="0 0",AnchorMax="1 1"}},FadeOut=0f});c.Add(new CuiElement{Parent="modal.overlay",Name="modal.loading.text",Components={new CuiTextComponent{Text="LOADING...",FontSize=45,Font="robotocondensed-bold.ttf",Align=TextAnchor.MiddleCenter,Color="1 1 1 0.5",FadeIn=0.5f,},new CuiRectTransformComponent{AnchorMin="0 0",AnchorMax="1 1"}},FadeOut=0f});CuiHelper.DestroyUi(player,"modal.overlay");CuiHelper.AddUi(player,c);}void __________________5U(BasePlayer player,string section=null){if(string.IsNullOrEmpty(config.apiAccess.key)){ShowNotice(player,"MISSING LICENSE KEY","Please make sure you have a valid license key in your config file.");return;}if(string.IsNullOrEmpty(config.apiAccess.token)){ShowNotice(player,"MISSING ACCESS TOKEN","Please make sure you have a valid access token in your config file.");return;}if(section!=null){}________________c5(player);webrequest.Enqueue("https://wp.rustplugins.io/v2/welcomepanel/get/positions/"+config.apiAccess.token,null,(________________________________________________tE,_________________________________________________jb)=>{if(________________________________________________tE==401){config.apiAccess.token=null;string update=@"[{""name"":""modal.loading.text"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""WRONG ACCESS TOKEN""},]}]";var u=new CuiElementContainer();u.Add(new CuiButton{Button={Close="modal.overlay",Color="0 0 0 0.5",Material="assets/icons/iconmaterial.mat",FadeIn=0.3f},RectTransform={AnchorMin="0.48 0.4",AnchorMax="0.52 0.45"},Text={Text="CLOSE",FontSize=13,Align=TextAnchor.MiddleCenter,Color="1 1 1 0.7",FadeIn=0.3f}},"modal.overlay","modal..closebtn");CuiHelper.AddUi(player,update);CuiHelper.AddUi(player,u);return;}if(________________________________________________tE==200){CuiHelper.DestroyUi(player,"modal.overlay");CuiHelper.DestroyUi(player,"wpanel.overlay");CuiHelper.AddUi(player,"["+_________________________________________________jb+"]");string update=@"[{""name"":""wpanel.positionbtn.icon"",""update"":true,""components"":[{""type"":""UnityEngine.UI.RawImage"",""png"":""URL""},]}]".Replace("URL",(string)ImageLibrary.Call("GetImage","https://tools.rustplugins.io/position-icon.png"));CuiHelper.AddUi(player,update);update=@"[{""name"":""wpanel.colorsbtn.icon"",""update"":true,""components"":[{""type"":""UnityEngine.UI.RawImage"",""png"":""URL""},]}]".Replace("URL",(string)ImageLibrary.Call("GetImage","https://tools.rustplugins.io/color-icon.png"));CuiHelper.AddUi(player,update);update=@"[{""name"":""wpanel.imagesbtn.icon"",""update"":true,""components"":[{""type"":""UnityEngine.UI.RawImage"",""png"":""URL""},]}]".Replace("URL",(string)ImageLibrary.Call("GetImage","https://tools.rustplugins.io/images-icon.png"));CuiHelper.AddUi(player,update);update=@"[{""name"":""wpanel.footer.icon"",""update"":true,""components"":[{""type"":""UnityEngine.UI.RawImage"",""png"":""URL""},]}]".Replace("URL",(string)ImageLibrary.Call("GetImage","https://tools.rustplugins.io/welcomepanel-key.png"));CuiHelper.AddUi(player,update);return;}ShowNotice(player,"CONNECTION ERROR","There was an error while trying to connect to the server. If you sure this is not issue on your side, please create support ticket on our discord.");return;},this,RequestMethod.GET,new Dictionary<string,string>{{"Authorization",$"Bearer {config.apiAccess.key}"}},200f);}void ShowNotice(BasePlayer player,string title,string message,string[]destroy=null){var c=new CuiElementContainer();c.Add(new CuiPanel{Image={Color="0.70 0.67 0.65 0.4",Material="assets/content/ui/uibackgroundblur.mat",FadeIn=0.3f},RectTransform={AnchorMin="0 0",AnchorMax="1 1"},CursorEnabled=true,KeyboardEnabled=true},"Overlay","modal.notice.overlay");c.Add(new CuiElement{Parent="modal.notice.overlay",Components={new CuiImageComponent{Material="assets/icons/iconmaterial.mat",Sprite="assets/content/ui/ui.background.transparent.radial.psd",Color="0 0 0 0.93",FadeIn=0.3f},new CuiRectTransformComponent{AnchorMin="0 0",AnchorMax="1 1"}}});c.Add(new CuiPanel{Image={Color="0.70 0.67 0.65 0",Material="assets/icons/iconmaterial.mat",FadeIn=0.3f},RectTransform={AnchorMin="0.5 0.5",AnchorMax="0.5 0.5",OffsetMin="-200 -150",OffsetMax="200 150"},},"modal.notice.overlay","modal.notice.offset");c.Add(new CuiButton{Button={Close="modal.notice.overlay",Color="0 0 0 0.4",Material="assets/icons/iconmaterial.mat",FadeIn=0.3f},RectTransform={AnchorMin="0.42 0.2",AnchorMax="0.58 0.3"},Text={Text="C L O S E",FontSize=13,Align=TextAnchor.MiddleCenter,Color="1 1 1 0.7",FadeIn=0.3f}},"modal.notice.offset","modal.closebtn");c.Add(new CuiElement{Parent="modal.notice.offset",Components={new CuiTextComponent{Text=title,FontSize=32,Font="robotocondensed-bold.ttf",Align=TextAnchor.UpperCenter,Color="1 1 1 0.7",FadeIn=0.3f,},new CuiRectTransformComponent{AnchorMin="0 0.6",AnchorMax="1 0.74"}},FadeOut=0f});c.Add(new CuiElement{Parent="modal.notice.offset",Components={new CuiTextComponent{Text=message,FontSize=15,Font="robotocondensed-regular.ttf",Align=TextAnchor.UpperCenter,Color="1 1 1 1",FadeIn=0.3f,},new CuiRectTransformComponent{AnchorMin="0 0.3",AnchorMax="1 0.60"}},FadeOut=0f});if(destroy!=null){foreach(string _destroy in destroy){CuiHelper.DestroyUi(player,_destroy);}}CuiHelper.DestroyUi(player,"modal.notice.overlay");CuiHelper.AddUi(player,c);}bool grid=false;[ConsoleCommand("grid")]private void helpgrid(ConsoleSystem.Arg arg){var player=arg?.Player();if(arg.Player()==null)return;CuiHelper.DestroyUi(player,"wp.helpgrid");if(grid){grid=false;return;}var g=new CuiElementContainer();g.Add(new CuiPanel{Image={Color="0 0 0 0.7",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin="0 0",AnchorMax="1 1"},},"Overlay","wp.helpgrid");for(var i=0.5;i<10;i++){Puts(i.ToString());g.Add(new CuiPanel{Image={Color="1 1 1 0.17",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin=$"0.{i*10} 0",AnchorMax=$"0.{i*10} 1",OffsetMin="-1 0",OffsetMax="1 0"},},"wp.helpgrid","wp.helpgrid.vertical.thin");}for(var i=0.5;i<10.6;i++){Puts(i.ToString());g.Add(new CuiPanel{Image={Color="1 1 1 0.17",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin=$"0 0.{i*10}",AnchorMax=$"1 0.{i*10}",OffsetMin="0 -1",OffsetMax="0 1"},},"wp.helpgrid","wp.helpgrid.horizontal.thin");}for(var i=1;i<10;i++){g.Add(new CuiPanel{Image={Color="0.12 0.51 0.67 1",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin=$"0 0.{i}",AnchorMax=$"1 0.{i}",OffsetMin="0 -1",OffsetMax="0 1"},},"wp.helpgrid","wp.helpgrid.horizontal.thick");}for(var i=1;i<10;i++){g.Add(new CuiPanel{Image={Color="0.12 0.51 0.67 1",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin=$"0.{i} 0",AnchorMax=$"0.{i} 1",OffsetMin="-1 0",OffsetMax="1 0"},},"wp.helpgrid","wp.helpgrid.vertical.thick");}grid=true;CuiHelper.AddUi(player,g);}[ConsoleCommand("wpedit")]private void wpstartediting(ConsoleSystem.Arg arg){var player=arg?.Player();var args=arg.Args;if(arg.Player()==null)return;if(args.Length!=10){ShowNotice(player,"SOMETHING WENT WRONG","Probably missplaced arguments...");return;}if(!player.IsAdmin){ShowNotice(player,"NOT ALLOWED","Only server admins can access this feature.");return;}CuiHelper.DestroyUi(player,"wpanel.overlay");var script=player.gameObject.GetOrAddComponent<InputTracker>();if(script==null){ShowNotice(player,"INPUT BEHAVIOR ERROR","InputTracker component was not added. Please contact plugin developer.");return;}else{script.StartEditing(args);}}[ConsoleCommand("wp.modal.input")]private void modalinput(ConsoleSystem.Arg arg){var player=arg?.Player();var args=arg.Args;if(args.Length<1){string _json=@"[	
						{
							""name"": ""modal.enterbutton"",
							""update"": true,
							""components"":
							[
								{
									""type"":""UnityEngine.UI.Button"",
									""color"": ""0 0 0 0.3"",
                                    ""command"": ""_""
								},
							]
						},]";CuiHelper.AddUi(player,_json);return;}config.apiAccess.token=args[0];string json=@"[	
						{
							""name"": ""modal.enterbutton"",
							""update"": true,
							""components"":
							[
								{
									""type"":""UnityEngine.UI.Button"",
									""color"": ""0.48 0.67 0.12 1"",
                                    ""command"": ""wp.modal.enter""
								},
							]
						},]";CuiHelper.AddUi(player,json);}[ConsoleCommand("wp.modal.enter")]private void modalenter(ConsoleSystem.Arg arg){var player=arg?.Player();CuiHelper.DestroyUi(player,"modal.overlay");var c=new CuiElementContainer();c.Add(new CuiPanel{Image={Color="0.70 0.67 0.65 0.3",Material="assets/content/ui/uibackgroundblur.mat",FadeIn=0.3f},RectTransform={AnchorMin="0 0",AnchorMax="1 1"},FadeOut=0f,CursorEnabled=true,KeyboardEnabled=true},"Overlay","modal.overlay");c.Add(new CuiElement{Parent="modal.overlay",Components={new CuiImageComponent{Material="assets/icons/iconmaterial.mat",Sprite="assets/content/ui/ui.background.transparent.radial.psd",Color="0 0 0 0.93",FadeIn=0.3f},new CuiRectTransformComponent{AnchorMin="0 0",AnchorMax="1 1"}},FadeOut=0f});c.Add(new CuiElement{Parent="modal.overlay",Name="modal.loading.text",Components={new CuiTextComponent{Text="LOADING...",FontSize=45,Font="robotocondensed-bold.ttf",Align=TextAnchor.MiddleCenter,Color="1 1 1 0.5",FadeIn=0.5f,},new CuiRectTransformComponent{AnchorMin="0 0",AnchorMax="1 1"}},FadeOut=0f});CuiHelper.DestroyUi(player,"modal.overlay");CuiHelper.AddUi(player,c);webrequest.Enqueue("https://wp.rustplugins.io/v2/welcomepanel/get/positions/"+config.apiAccess.token,null,(________________________________________________tE,_________________________________________________jb)=>{if(________________________________________________tE==401){config.apiAccess.token=null;string update=@"[{""name"":""modal.loading.text"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""WRONG ACCESS TOKEN""},]}]";var u=new CuiElementContainer();u.Add(new CuiButton{Button={Close="modal.overlay",Color="0 0 0 0.5",Material="assets/icons/iconmaterial.mat",FadeIn=0.3f},RectTransform={AnchorMin="0.48 0.4",AnchorMax="0.52 0.45"},Text={Text="CLOSE",FontSize=13,Align=TextAnchor.MiddleCenter,Color="1 1 1 0.7",FadeIn=0.3f}},"modal.overlay","modal..closebtn");CuiHelper.AddUi(player,update);CuiHelper.AddUi(player,u);return;}if(________________________________________________tE==200){CuiHelper.DestroyUi(player,"modal.loading.text");CuiHelper.DestroyUi(player,"wpanel.overlay");CuiHelper.AddUi(player,"["+_________________________________________________jb+"]");return;}},this,RequestMethod.GET,new Dictionary<string,string>{{"Authorization",$"Bearer {config.apiAccess.key}"}},200f);}[ConsoleCommand("wpedit.controls")]private void wpeditcontrols(ConsoleSystem.Arg arg){var player=arg?.Player();if(player==null)return;if(!player.IsAdmin){ShowNotice(player,"NOT ALLOWED","Only server admins can access this feature.");return;}var args=arg.Args;if(args.Length<1)return;if(args[0]=="hidetooltip"){CuiHelper.DestroyUi(player,"wp.tooltip");return;}var script=player.gameObject.GetComponent<InputTracker>();if(script==null)return;if(args[0]=="exit"){script.______________________________iK();UnityEngine.Object.Destroy(script);return;}if(args[0]=="move"){script._____________________________________ne();script.____________________________________ad();string update=@"[{""name"":""wp.edit.movebtn"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.09 0.37 0.48 1""},]}]";CuiHelper.AddUi(player,update);script._______________________________________rE=true;}if(args[0]=="resize"){script._____________________________________ne();script.____________________________________ad();string update=@"[{""name"":""wp.edit.resizebtn"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.09 0.37 0.48 1""},]}]";CuiHelper.AddUi(player,update);script.________________________________________Ok=true;}if(args[0]=="offsets"){if(script.__________________________________________ox!=null){script.___________________________________Bg();return;}else{script._____________________________________ne();script.____________________________________ad();}string update=@"[{""name"":""wp.edit.offsetbtn"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.09 0.37 0.48 1""},]}]";CuiHelper.AddUi(player,update);script.__________________________________________ox="left";script.ShowTooltip($"Selected side: <b>{script.__________________________________________ox.ToUpper()}</b>");}if(args[0]=="outline"){script.______________________________________dg(!script._________________________________________aP);}if(args[0]=="save"){script.__________________________________pa();}}[ChatCommand("edit")]private void wp_edit(BasePlayer player){if(player==null)return;if(!player.IsAdmin){ShowNotice(player,"NOT ALLOWED","Only server admins can access this feature.");return;}if(string.IsNullOrEmpty(config.apiAccess.key)){ShowNotice(player,"MISSING LICENSE KEY","Please make sure you have a valid license key in your config file.");return;}if(string.IsNullOrEmpty(config.apiAccess.token)){ShowNotice(player,"MISSING ACCESS TOKEN","Please make sure you have a valid access token in your config file.");return;}__________________5U(player);}static WelcomePanel plugin;private void Init()=>plugin=this;private class InputTracker:FacepunchBehaviour{BasePlayer player;BaseMountable tempChair;public bool _______________________________________rE=false;public bool ________________________________________Ok=false;public bool _________________________________________aP=false;public string __________________________________________ox=null;public bool ___________________________________________0C=false;float ____________________________________________HK=0.001f;string _____________________________________________7u=null;float ___________________fy=0.008f;float ____________________Ga=0.1f;float _____________________He=0.09f;float ______________________wp=0.85f;float _______________________Yg=0f;float ________________________eE=0f;float _________________________Dh=0f;float __________________________O5=0f;float ______________________________________________BC=-0.055f;float _______________________________________________u3=0.0f;void Awake()=>player=GetComponent<BasePlayer>();void Update(){if(_____________________________________________7u==null)return;if(player.serverInput.IsDown(BUTTON.FORWARD)){if(_______________________________________rE)Move("up");if(________________________________________Ok)Resize("height",true);if(__________________________________________ox!=null)_________________xK(__________________________________________ox,true);________________________________EE(_____________________________________________7u);}if(player.serverInput.IsDown(BUTTON.BACKWARD)){if(_______________________________________rE)Move("down");if(________________________________________Ok)Resize("height",false);if(__________________________________________ox!=null)_________________xK(__________________________________________ox,false);________________________________EE(_____________________________________________7u);}if(player.serverInput.IsDown(BUTTON.LEFT)){if(_______________________________________rE)Move("left");if(________________________________________Ok)Resize("width",false);if(__________________________________________ox!=null)_________________xK(__________________________________________ox,false);________________________________EE(_____________________________________________7u);}if(player.serverInput.IsDown(BUTTON.RIGHT)){if(_______________________________________rE)Move("right");if(________________________________________Ok)Resize("width",true);if(__________________________________________ox!=null)_________________xK(__________________________________________ox,true);________________________________EE(_____________________________________________7u);}if(player.serverInput.IsDown(BUTTON.FIRE_THIRD)){if(IsInvoking(nameof(_____________________________p5))==false){InvokeRepeating(nameof(_____________________________p5),0.02f,0.02f);}}if(player.serverInput.IsDown(BUTTON.SPRINT)){if(_______________________________________rE||________________________________________Ok){if(____________________________________________HK>0.01)return;____________________________________________HK+=0.00015f;}ShowTooltip($"Increment value set to <b>'{Math.Round(____________________________________________HK,4)}'</b>");}if(player.serverInput.IsDown(BUTTON.DUCK)){if(_______________________________________rE||________________________________________Ok){if(____________________________________________HK<0.00015)return;____________________________________________HK-=0.00015f;}ShowTooltip($"Increment value set to <b>'{Math.Round(____________________________________________HK,4)}'</b>");}}void Resize(string dimension,bool inc){if(dimension=="width"){if(inc){_____________________He+=____________________________________________HK;}else{_____________________He-=____________________________________________HK;}}if(dimension=="height"){if(inc){______________________wp+=____________________________________________HK;}else{______________________wp-=____________________________________________HK;}}ShowTooltip($"aMin: '{Math.Round(___________________fy,2)} {Math.Round(____________________Ga,2)}' | aMax: '{Math.Round(_____________________He,2)} {Math.Round(______________________wp,2)}'");}void Move(string direction){switch(direction){case"up":____________________Ga+=____________________________________________HK;______________________wp+=____________________________________________HK;break;case"down":____________________Ga-=____________________________________________HK;______________________wp-=____________________________________________HK;break;case"left":___________________fy-=____________________________________________HK;_____________________He-=____________________________________________HK;break;case"right":___________________fy+=____________________________________________HK;_____________________He+=____________________________________________HK;break;}ShowTooltip($"aMin: '{Math.Round(___________________fy,2)} {Math.Round(____________________Ga,2)}' | aMax: '{Math.Round(_____________________He,2)} {Math.Round(______________________wp,2)}'");}void _________________xK(string side,bool inc){plugin.Puts($"{side} {inc}");if(side=="left"){if(inc){_______________________Yg+=1;}else{_______________________Yg-=1;}ShowTooltip($"Left side offset: <b>'{_______________________Yg}'</b>");}if(side=="bottom"){if(inc){________________________eE+=1;}else{________________________eE-=1;}ShowTooltip($"Bottom side offset: <b>'{________________________eE}'</b>");}if(side=="right"){if(inc){_________________________Dh+=1;}else{_________________________Dh-=1;}ShowTooltip($"Right side offset: <b>'{_________________________Dh}'</b>");}if(side=="top"){if(inc){__________________________O5+=1;}else{__________________________O5-=1;}ShowTooltip($"Top side offset: <b>'{__________________________O5}'</b>");}}public void StartEditing(string[]args){if(args[0]=="pos"){_______________________________3l();_____________________________________________7u=args[1];if(_____________________________________________7u.Contains("%")){_____________________________________________7u=_____________________________________________7u.Replace("%"," ");}___________________fy=Convert.ToSingle(args[2]);____________________Ga=Convert.ToSingle(args[3]);_____________________He=Convert.ToSingle(args[4]);______________________wp=Convert.ToSingle(args[5]);_______________________Yg=Convert.ToSingle(args[6]);________________________eE=Convert.ToSingle(args[7]);_________________________Dh=Convert.ToSingle(args[8]);__________________________O5=Convert.ToSingle(args[9]);___________________________sr();____________________________Td();player.SendConsoleCommand("wpedit.controls move");}}void ___________________________sr(){CuiHelper.DestroyUi(player,"background");CuiHelper.AddUi(player,plugin.__IG.Replace("NeedsKeyboard","NeedsCursor"));CuiHelper.AddUi(player,plugin._____Y3);plugin.____________________________srPage(player,0,0,true);}void ____________________________Td(){var d=new CuiElementContainer();d.Add(new CuiPanel{Image={Color="0.12 0.12 0.12 1",Material="assets/content/ui/uibackgroundblur.mat"},RectTransform={AnchorMin="0.5 0.14",AnchorMax="0.5 0.20",OffsetMin="-270 0",OffsetMax="270 0"},},"Overlay","wp.edit.toolbar");d.Add(new CuiButton{Button={Command="wpedit.controls move",Color="0.19 0.19 0.19 1",Material="assets/content/ui/uibackgroundblur.mat"},RectTransform={AnchorMin="0.008 0.15",AnchorMax="0.167 0.85"},Text={Text="   MOVE",FontSize=13,Align=TextAnchor.MiddleCenter,Color="1 1 1 0.6"}},"wp.edit.toolbar","wp.edit.movebtn");d.Add(new CuiElement{Name="wp.edit.movebtn.icon",Parent="wp.edit.movebtn",Components={new CuiRawImageComponent{Png=(string)plugin.ImageLibrary.Call("GetImage","https://tools.rustplugins.io/move-icon.png"),Sprite="assets/content/textures/generic/fulltransparent.tga"},new CuiRectTransformComponent{AnchorMin="0.1 0.3",AnchorMax="0.3 0.7"}}});d.Add(new CuiButton{Button={Command="wpedit.controls resize",Color="0.19 0.19 0.19 1",Material="assets/content/ui/uibackgroundblur.mat"},RectTransform={AnchorMin="0.175 0.15",AnchorMax="0.334 0.85"},Text={Text="     RESIZE",FontSize=13,Align=TextAnchor.MiddleCenter,Color="1 1 1 0.6"}},"wp.edit.toolbar","wp.edit.resizebtn");d.Add(new CuiElement{Name="wp.edit.resizebtn.icon",Parent="wp.edit.resizebtn",Components={new CuiRawImageComponent{Png=(string)plugin.ImageLibrary.Call("GetImage","https://tools.rustplugins.io/resize-icon.png"),Sprite="assets/content/textures/generic/fulltransparent.tga"},new CuiRectTransformComponent{AnchorMin="0.1 0.3",AnchorMax="0.3 0.7"}}});d.Add(new CuiButton{Button={Command="wpedit.controls offsets",Color="0.19 0.19 0.19 1",Material="assets/content/ui/uibackgroundblur.mat"},RectTransform={AnchorMin="0.342 0.15",AnchorMax="0.501 0.85"},Text={Text="       OFFSETS",FontSize=13,Align=TextAnchor.MiddleCenter,Color="1 1 1 0.6"}},"wp.edit.toolbar","wp.edit.offsetbtn");d.Add(new CuiElement{Name="wp.edit.offsetbtn.icon",Parent="wp.edit.offsetbtn",Components={new CuiRawImageComponent{Png=(string)plugin.ImageLibrary.Call("GetImage","https://tools.rustplugins.io/offsets-icon.png"),Sprite="assets/content/textures/generic/fulltransparent.tga"},new CuiRectTransformComponent{AnchorMin="0.1 0.27",AnchorMax="0.3 0.73"}}});d.Add(new CuiButton{Button={Command="wpedit.controls exit",Color="0.83 0.29 0.21 1",Material="assets/content/ui/uibackgroundblur.mat"},RectTransform={AnchorMin="0.93 0.15",AnchorMax="0.992 0.85"},Text={Text="",FontSize=13,Align=TextAnchor.MiddleCenter,Color="1 1 1 0.6"}},"wp.edit.toolbar","wp.edit.exit");d.Add(new CuiElement{Parent="wp.edit.exit",Components={new CuiImageComponent{Material="assets/icons/iconmaterial.mat",Sprite="assets/icons/exit.png",Color="1 0.43 0.34 1"},new CuiRectTransformComponent{AnchorMin="0.15 0.15",AnchorMax="0.85 0.85"}},FadeOut=0f});d.Add(new CuiButton{Button={Command="wpedit.controls save",Color="0.44 0.6 0.14 1",Material="assets/content/ui/uibackgroundblur.mat"},RectTransform={AnchorMin="0.83 0.15",AnchorMax="0.922 0.85"},Text={Text="SAVE",FontSize=13,Align=TextAnchor.MiddleCenter,Color="0.76 0.99 0.33 1"}},"wp.edit.toolbar","wp.edit.save");d.Add(new CuiElement{Parent="wp.edit.toolbar",Components={new CuiTextComponent{Text="SHOW OUTLINE",FontSize=11,Font="robotocondensed-regular.ttf",Align=TextAnchor.MiddleLeft,Color="1 1 1 0.5"},new CuiRectTransformComponent{AnchorMin="0.55 0",AnchorMax="0.8 0.98"}},FadeOut=0f});d.Add(new CuiPanel{Image={Color="0.19 0.19 0.19 1",Material="assets/content/ui/uibackgroundblur.mat"},RectTransform={AnchorMin="0.69 0.25",AnchorMax="0.77 0.75"},},"wp.edit.toolbar","wp.edit.switch.bg");d.Add(new CuiPanel{Image={Color="0.59 0.59 0.59 1",Material="assets/content/ui/uibackgroundblur.mat"},RectTransform={AnchorMin="0.55 0.15",AnchorMax="0.9 0.8"},},"wp.edit.switch.bg","wp.edit.switch.inner");d.Add(new CuiButton{Button={Command="wpedit.controls outline",Color="0 0 0 0",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin="0.53 0.1",AnchorMax="0.77 0.9"},},"wp.edit.toolbar","wp.edit.switch.clickable");CuiHelper.DestroyUi(player,"wp.edit.toolbar");CuiHelper.AddUi(player,d);CuiHelper.DestroyUi(player,"wp.edit.toolbar");CuiHelper.AddUi(player,d);___________________________________________0C=false;InvokeRepeating(nameof(_____________________________p5),0.01f,0.01f);}void _____________________________p5(){if(!___________________________________________0C){if(_______________________________________________u3>=0.15f){CancelInvoke(nameof(_____________________________p5));___________________________________________0C=true;return;}______________________________________________BC+=0.01f;_______________________________________________u3+=0.01f;string u="[{\"name\":\"wp.edit.toolbar\",\"update\":true,\"components\":[{\"type\":\"RectTransform\",\"anchormin\":\"0.5 BOTTOM\",\"anchormax\":\"0.5 TOP\"}]}]";CuiHelper.AddUi(player,u.Replace("BOTTOM",______________________________________________BC.ToString()).Replace("TOP",_______________________________________________u3.ToString()));}else{if(_______________________________________________u3<=0.0f){CancelInvoke(nameof(_____________________________p5));___________________________________________0C=false;return;}______________________________________________BC-=0.01f;_______________________________________________u3-=0.01f;string u="[{\"name\":\"wp.edit.toolbar\",\"update\":true,\"components\":[{\"type\":\"RectTransform\",\"anchormin\":\"0.5 BOTTOM\",\"anchormax\":\"0.5 TOP\"}]}]";CuiHelper.AddUi(player,u.Replace("BOTTOM",______________________________________________BC.ToString()).Replace("TOP",_______________________________________________u3.ToString()));}}public void ______________________________iK(){_______________________________________rE=false;________________________________________Ok=false;___________________________________________0C=false;__________________________________________ox=null;_________________________________zq();CuiHelper.DestroyUi(player,"wp.edit.toolbar");CuiHelper.DestroyUi(player,"background");plugin.__________________5U(player);}void _______________________________3l(){var position=player.transform.position;var standingChair=GameManager.server.CreateEntity("assets/prefabs/vehicle/seats/standingdriver.prefab",player.transform.position,player.eyes.bodyRotation,true)as BaseMountable;standingChair.transform.SetPositionAndRotation(position,player.eyes.bodyRotation);standingChair.Spawn();standingChair.EnableGlobalBroadcast(true);player.EnsureDismounted();player.SetMounted(standingChair);
standingChair.MountPlayer(player);standingChair.transform.position=position;standingChair.SendNetworkUpdate();tempChair=standingChair;}void ________________________________EE(string name){string json=@"[	
						{
							""name"": ""%NAME%"",
							""update"": true,
							""components"":
							[
								{
									""type"":""RectTransform"",
									""anchormin"": ""LEFT BOTTOM"",
                                    ""anchormax"": ""RIGHT TOP"",
                                    ""offsetmin"": ""O_L O_B"",
                                    ""offsetmax"": ""O_R O_T""
								},
							]
						},]";json=json.Replace("%NAME%",name).Replace("LEFT",___________________fy.ToString()).Replace("BOTTOM",____________________Ga.ToString()).Replace("RIGHT",_____________________He.ToString()).Replace("TOP",______________________wp.ToString()).Replace("O_L",_______________________Yg.ToString()).Replace("O_B",________________________eE.ToString()).Replace("O_R",_________________________Dh.ToString()).Replace("O_T",__________________________O5.ToString());CuiHelper.AddUi(player,json);}void _________________________________zq(){if(tempChair==null)return;player.DismountObject();player.EnsureDismounted();tempChair.Kill();tempChair=null;CuiHelper.DestroyUi(player,"background");}public void __________________________________pa(){string update=@"[{""name"":""wpedit.controls save"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Button"",""command"":"" ""},]}]";CuiHelper.AddUi(player,update);ShowTooltip("Saving changes...");Dictionary<string,string>data=new Dictionary<string,string>{{"name",_____________________________________________7u},{"anchorMin",$"{Math.Round(___________________fy,3)} {Math.Round(____________________Ga,3)}"},{"anchorMax",$"{Math.Round(_____________________He,3)} {Math.Round(______________________wp,3)}"},{"offsetMin",$"{Math.Round(_______________________Yg,3)} {Math.Round(________________________eE,3)}"},{"offsetMax",$"{Math.Round(_________________________Dh,3)} {Math.Round(__________________________O5,3)}"}};string body=string.Join("&",data.Select(kv=>$"{kv.Key}={kv.Value}"));plugin.webrequest.Enqueue("https://wp.rustplugins.io/v2/welcomepanel/post/change/"+plugin.config.apiAccess.token,body,(________________________________________________tE,_________________________________________________jb)=>{update=@"[{""name"":""wpedit.controls save"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Button"",""command"":""wpedit.controls save""},]}]";CuiHelper.AddUi(player,update);if(________________________________________________tE==429){ShowTooltip("Too many requests.","0.83 0.29 0.21 1");return;}if(________________________________________________tE==401){ShowTooltip("Error while saving.","0.83 0.29 0.21 1");return;}if(________________________________________________tE==200){ShowTooltip("Changes successfuly saved.","0.34 0.48 0.08 1");plugin.___________________________________________________GG(_________________________________________________jb.ToString());return;}ShowTooltip("Error while saving.","0.83 0.29 0.21 1");return;},plugin,RequestMethod.POST,new Dictionary<string,string>{{"Authorization",$"Bearer {plugin.config.apiAccess.key}"}},200f);}public void ___________________________________Bg(){if(__________________________________________ox==null)return;string[]order={"left","bottom","right","top"};int index=Array.IndexOf(order,__________________________________________ox);if(index+1>=order.Length){__________________________________________ox=order[0];ShowTooltip($"Selected side: <b>{__________________________________________ox.ToUpper()}</b>");}else{__________________________________________ox=order[index+1];ShowTooltip($"Selected side: <b>{__________________________________________ox.ToUpper()}</b>");}}public void ____________________________________ad(){_______________________________________rE=false;________________________________________Ok=false;__________________________________________ox=null;}public void _____________________________________ne(){if(_______________________________________rE){string update=@"[{""name"":""wp.edit.movebtn"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.19 0.19 0.19 1""},]}]";CuiHelper.AddUi(player,update);}if(________________________________________Ok){string update=@"[{""name"":""wp.edit.resizebtn"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.19 0.19 0.19 1""},]}]";CuiHelper.AddUi(player,update);}if(__________________________________________ox!=null){string update=@"[{""name"":""wp.edit.offsetbtn"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.19 0.19 0.19 1""},]}]";CuiHelper.AddUi(player,update);}}public void ______________________________________dg(bool show){CuiHelper.DestroyUi(player,"wp.edit.outline.left");CuiHelper.DestroyUi(player,"wp.edit.outline.right");CuiHelper.DestroyUi(player,"wp.edit.outline.bottom");CuiHelper.DestroyUi(player,"wp.edit.outline.top");if(!show){_________________________________________aP=false;string update=@"[{""name"":""wp.edit.switch.bg"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0.19 0.19 0.19 1""},]}]";CuiHelper.AddUi(player,update);update=@"[{""name"":""wp.edit.switch.inner"",""update"":true,""components"":[{""type"":""RectTransform"",""anchormin"":""0.55 0.15"",""anchormax"":""0.9 0.8""},]}]";CuiHelper.AddUi(player,update);return;}string _update=@"[{""name"":""wp.edit.switch.bg"",""update"":true,""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0.07 0.3 0.39 1""},]}]";CuiHelper.AddUi(player,_update);_update=@"[{""name"":""wp.edit.switch.inner"",""update"":true,""components"":[{""type"":""RectTransform"",""anchormin"":""0.1 0.15"",""anchormax"":""0.45 0.8""},]}]";CuiHelper.AddUi(player,_update);_________________________________________aP=true;var _outline=new CuiElementContainer();_outline.Add(new CuiPanel{Image={Color="0.12 0.67 0.17 1",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin="0 0",AnchorMax="0 1",OffsetMin="0 0",OffsetMax="3 0"},},_____________________________________________7u,"wp.edit.outline.left");_outline.Add(new CuiPanel{Image={Color="0.12 0.67 0.17 1",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin="1 0",AnchorMax="1 1",OffsetMin="-3 0",OffsetMax="0 0"},},_____________________________________________7u,"wp.edit.outline.right");_outline.Add(new CuiPanel{Image={Color="0.12 0.67 0.17 1",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin="0 0",AnchorMax="1 0",OffsetMin="0 0",OffsetMax="0 3"},},_____________________________________________7u,"wp.edit.outline.bottom");_outline.Add(new CuiPanel{Image={Color="0.12 0.67 0.17 1",Material="assets/icons/iconmaterial.mat"},RectTransform={AnchorMin="0 1",AnchorMax="1 1",OffsetMin="0 -3",OffsetMax="0 0"},},_____________________________________________7u,"wp.edit.outline.top");CuiHelper.AddUi(player,_outline);}public void ShowTooltip(string text="",string color="0.12 0.12 0.12 0.6"){var c=new CuiElementContainer();c.Add(new CuiPanel{Image={Color=color,Material="assets/content/ui/uibackgroundblur.mat"},RectTransform={AnchorMin="0.33 -0.8",AnchorMax="0.67 -0.2"},FadeOut=0f,CursorEnabled=true},"wp.edit.toolbar","wp.tooltip");c.Add(new CuiElement{Name="wp.tooltip.text",Parent="wp.tooltip",Components={new CuiTextComponent{Text=text,FontSize=11,Font="robotocondensed-regular.ttf",Align=TextAnchor.MiddleCenter,Color="1 1 1 1"},new CuiRectTransformComponent{AnchorMin="0 0",AnchorMax="1 1"},new CuiCountdownComponent{EndTime=2,StartTime=0,Command="wpedit.controls hidetooltip"}},FadeOut=0f});CuiHelper.DestroyUi(player,"wp.tooltip");CuiHelper.AddUi(player,c);}}

        #endregion
    }
}