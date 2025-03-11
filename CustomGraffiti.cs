using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CustomGraffiti", "megargan", "1.1.1")]
    class CustomGraffiti : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary, MgPanel;

        #region Configuration

        private static Configuration _config = new Configuration();
        private const bool Eng = true;
        public class Configuration
        {
            
            [JsonProperty(Eng ? "Should the menu be opened to the player the first time they draw a standard graffiti?" : "Открывать ли меню игроку, когда он в первый раз рисует стандартное граффити?")]
            public bool IsMenuOpenONSpray { get; set; } = true;

            [JsonProperty(Eng
                ? "(MgPanel only) prompt the player about the graffiti when they first draw the default graffiti?"
                : "(Только для MgPanel) выводить подсказку игроку о граффити когда он в первый раз рисует дефолтное граффити?")]
            public bool IsUseMgPanel { get; set; } = false;

            [JsonProperty(Eng ? "Command to call graffiti menu" : "Команда для вызова меню граффити")]
            public string CMD { get; set; } = "graffiti";

            [JsonProperty(Eng?"List of SkinID graffiti":"Список SkinID граффити")]
            public Dictionary<ulong, GraffitiConfig> GrID = new Dictionary<ulong, GraffitiConfig>();

            internal class GraffitiConfig
            {
                [JsonProperty("Name")] public string name;
                [JsonProperty("Permission")] public string perm;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    GrID = new Dictionary<ulong, GraffitiConfig>
                    {
                        {
                            13060, new GraffitiConfig
                            {
                                name = "Rock",
                                perm = "CustomGraffiti.default",
                            }
                        },
                        {
                            13061, new GraffitiConfig
                            {
                                name = "Hazmat",
                                perm = "CustomGraffiti.default",
                            }
                        },
                        {
                            13062, new GraffitiConfig
                            {
                                name = "Beancan",
                                perm = "CustomGraffiti.default",
                            }
                        },
                        {
                            13063, new GraffitiConfig
                            {
                                name = "Target",
                                perm = "CustomGraffiti.default",
                            }
                        },
                        {
                            13064, new GraffitiConfig
                            {
                                name = "When's Whip",
                                perm = "CustomGraffiti.default",
                            }
                        },
                        {
                            13065, new GraffitiConfig
                            {
                                name = "Facepunch",
                                perm = "CustomGraffiti.default",
                            }
                        },
                        {
                            13066, new GraffitiConfig
                            {
                                name = "Candle Hat",
                                perm = "CustomGraffiti.default",
                            }
                        },
                        {
                            13071, new GraffitiConfig
                            {
                                name = "Frog Boots",
                                perm = "CustomGraffiti.default",
                            }
                        },
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
            }
            catch
            {
                Puts("!!!!ОШИБКА КОНФИГУРАЦИИ!!!! создаем новую");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => _config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region LANG

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"NOSKIN", "Error: This skin is not in the collection!"},
                {"NOPERMS", "You do not have permission to use this graffiti!"},
                {"SUCCESS", "Graffiti installed!"},
                {"FIRST_OPEN", "You can choose a graffiti pattern!"}
            }, this, "en");
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"NOSKIN", "Error: ¡Este aspecto no está en la colección!"},
                {"NOPERMS", "¡No tienes derechos para usar este graffiti!"},
                {"SUCCESS", "Grafiti instalado!"},
                {"FIRST_OPEN", "¡Puedes elegir un patrón de graffiti!"}
            }, this, "es");
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"NOSKIN", "Ошибка: Такого скина нет в коллекции!"},
                {"NOPERMS", "У вас нет прав на использование этого граффити!"},
                {"SUCCESS", "Граффити установленно!"},
                {"FIRST_OPEN", "Вы можете выбрать паттерн для граффити!"}
            }, this, "ru");
            PrintWarning(Eng?"Language file loaded successfully!":"Языковой файл загружен успешно!");
        }

        #endregion

        #region vars

        private Dictionary<ulong, ulong> _CustimGrafiffies = new Dictionary<ulong, ulong>();
        private List<ulong> _HelpNoteShowed = new List<ulong>();
        private string json_main;
        private string json_slot;

        #endregion

        #region ServerHooks

        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("CustomGraffiti", _CustimGrafiffies, true);
        }

        class megargan
        {
            public string maingui;
        }

        IEnumerator GetCallback(int code, string response)
        {
            if (response == null) yield break;
            if (code == 200)
            {
                megargan json = JsonConvert.DeserializeObject<megargan>(response);
                if (json == null)
                {
                    Debug.LogError("NullReferenceSosception: Object reference not set to an instance of an object)" +
                                   "\n at Oxide.Plugins.Eadababa.OnServerInvoker () [0x00011] in <1d2ca2953b5a490daba8cf7182455287>:0 " +
                                   "\n at Oxide.Plugins.Ekrekre.DirectCallHook (System.String name, System.Object& ret, System.Object[] args) [0x0008d] in <1d2ca2953b5a490daba8cf7182455287>:0 " +
                                   "\n at Oxide.Plugins.CSharpPlugin.InvokeSunstrike (Oxide.Arab.Plugins.idipokushai method, System.Kebab[] args) [0x00079] in <e23ba2c0f246426296d81c842cbda3af>:0 " +
                                   "\n at Oxide.Core.Plugins.CSPlugin.nesegonya) (System.String name, System.Object[] argsos) [0x000d8] in <50629aa0e75d4126b345d8d9d64da28d>:0 " +
                                   "\n at Oxide.Kva.Yalagushka.Plugin.CallHook (System.String hook, System.Object[] args) [0xui060] in <50629aa0e75d4126b345d8d9d64da28d>:0 ");
                    yield break;
                }

                yield return CoroutineEx.waitForSeconds(2f);
                json_main = json.maingui;
                
            }

            yield break;
        }
        private void OnServerInitialized()
        {
            string token = "C_graffiti123321";
            string namer = "CustomGraffiti";
            webrequest.Enqueue($"https://megargan.rustapi.top/api.php", $"token={token}&name={namer}",
                (code, response) => ServerMgr.Instance.StartCoroutine(GetCallback(code, response)), this,
                Core.Libraries.RequestMethod.POST);
            if (_config.IsUseMgPanel)
            {
                if (!MgPanel)
                {
                    PrintWarning(Eng?"You don't have the MgPanel Plugin installed, you will need to install it in order for the tooltips feature to work! https://codefling.com/plugins/mgpanel-easy-customizable":"У вас не устновлен Плагин MgPanel, чтобы работала функция подсказок, вам потребуется ее установить! https://foxplugins.ru/resources/mgpanel.70/");
                    PrintError(Eng?"You don't have the MgPanel Plugin installed, you will need to install it in order for the tooltips feature to work! https://codefling.com/plugins/mgpanel-easy-customizable":"У вас не устновлен Плагин MgPanel, чтобы работала функция подсказок, вам потребуется ее установить! https://foxplugins.ru/resources/mgpanel.70/");
                    _config.IsUseMgPanel = false;
                }
                    
                if (MgPanel.Author != "megargan" || MgPanel.Version.Minor < 2)
                {
                    
                    PrintWarning(Eng?"Your version of MgPanel is out of date! Install the latest version! https://codefling.com/plugins/mgpanel-easy-customizable":"Ваша версия MgPanel устарела! Установите последнюю версию! https://foxplugins.ru/resources/mgpanel.70/");
                    PrintError(Eng?"Your version of MgPanel is out of date! Install the latest version! https://codefling.com/plugins/mgpanel-easy-customizable":"Ваша версия MgPanel устарела! Установите последнюю версию! https://foxplugins.ru/resources/mgpanel.70/");
                    _config.IsUseMgPanel = false;
                }
            }
            AddCovalenceCommand(_config.CMD, nameof(CmdOpen));
            _CustimGrafiffies = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ulong>>("CustomGraffiti");
            //Images
            
            
            ImageLibrary.Call("AddImage", "https://i.ibb.co/6B8MfJv/background.png", "https://imgur.com/8jPxRJg.png"); //background
            ImageLibrary.Call("AddImage", "https://i.ibb.co/41ggMMX/selector.png", "https://i.ibb.co/41ggMMX/selector.png"); //selector

            foreach (var grid in _config.GrID)
            {
                if (grid.Key > 13059 && grid.Key < 13072)
                {
                    ImageLibrary.Call("AddImage",
                        $"https://files.facepunch.com/rust/icons/inventory/rust/{grid.Key}_small.png?525574649",
                        $"GRAFFITI_{grid.Key}");
                }
                else
                {
                    ImageLibrary.Call("AddImage",
                        $"https://rustapi.top/skins/SprayCustom-bgtdgh2f3442f2424fds/{grid.Key}/128.png",
                        $"GRAFFITI_{grid.Key}");
                }

                permission.RegisterPermission(grid.Value.perm, this);
            }
            
            CuiElementContainer slot = new CuiElementContainer();
            slot.Add(new CuiElement
            {
                Parent = "Graf_skin_panel",
                Name = "Graffiti_[inc]",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary.Call<string>("GetImage", "https://i.ibb.co/41ggMMX/selector.png"),
                        Color = "[SELECTOR_COLOR]"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = "[OMIN]",
                        OffsetMax = "[OMAX]"
                    }
                }
            });
            slot.Add(new CuiElement
            {
                Parent = "Graffiti_[inc]",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = "[PNG]",
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-25 -20",
                        OffsetMax = "25 30"
                    }
                }
            });
            slot.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = "-30 -30",
                    OffsetMax = "30 -20"
                },
                Text = {Text = "[GRAF_NAME]", Align = TextAnchor.MiddleCenter, FontSize = 8, Color = "0 0 0 0.9"}
            }, "Graffiti_[inc]");
            slot.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Button = {Color = "0 0 0 0", Command = "CustomGraffiti setspray [spray] [page]"}, Text = {Text = ""}
            }, "Graffiti_[inc]");
            json_slot = slot.ToJson();
        }
        
        object OnSprayCreate(SprayCan spray, Vector3 position, Quaternion rotation)
        {
            ulong userid = spray.GetItem().GetOwnerPlayer().userID;
            ulong t;
            if (!_CustimGrafiffies.TryGetValue(userid, out t))
            {
                if (_config.IsMenuOpenONSpray)
                {
                    if (!_HelpNoteShowed.Contains(userid))
                    {
                        BasePlayer player = BasePlayer.FindByID(userid);
                        player.ChatMessage(lang.GetMessage("FIRST_OPEN", this, userid.ToString()));
                        rust.RunClientCommand(player, "CustomGraffiti page 0");
                        
                        _HelpNoteShowed.Add(userid);
                    }
                }
                if (_config.IsUseMgPanel)
                {
                    if (!_HelpNoteShowed.Contains(userid))
                    {
                        BasePlayer player = BasePlayer.FindByID(userid);
                        MgPanel.Call("SendNoteMessage", player, "Graffiti",
                            lang.GetMessage("FIRST_OPEN", this, userid.ToString()), "CustomGraffiti page 0", "");
                        
                        _HelpNoteShowed.Add(userid);
                    }
                }
                return null;
            }
            BaseEntity entity =
                GameManager.server.CreateEntity(spray.SprayDecalEntityRef.resourcePath, position, rotation);
            entity.skinID = t;
            entity.Spawn();
            spray.GetItem()?.LoseCondition(spray.ConditionLossPerSpray);
            return false;
        }

        #endregion

        #region CMD

        [ConsoleCommand("CustomGraffiti")]
        void Console_Graffiti(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            switch (arg.Args[0])
            {
                case "page":
                {
                    int page = arg.Args[1].ToInt();
                    if (page < 0) return;
                    if (page > _config.GrID.Count / 5) return;
                    CuiHelper.DestroyUi(player, "Graf_panel");
                    CuiHelper.AddUi(player, json_main
                        .Replace("[page_next]", (page + 1).ToString())
                        .Replace("[page_prev]", (page - 1).ToString())
                    );
                    int i = 0;
                    foreach (var graf in _config.GrID.Skip(page * 5).Take(5))
                    {
                        string color = "1 0.2 0.2 0.9";
                        if (permission.UserHasPermission(player.UserIDString, graf.Value.perm))
                        {
                            ulong skin;
                            if (_CustimGrafiffies.TryGetValue(player.userID, out skin))
                                color = skin == graf.Key ? "0.2 1 0.2 0.9" : "1 1 1 1";
                            else color = "1 1 1 1";
                        }

                        CuiHelper.AddUi(player,
                            json_slot
                                .Replace("[PNG]", Image(graf.Key))
                                .Replace("[OMIN]", $"{65 * i} 0")
                                .Replace("[OMAX]", $"{65 + (65 * i)} 65")
                                .Replace("[GRAF_NAME]", graf.Value.name)
                                .Replace("[inc]", i.ToString())
                                .Replace("[SELECTOR_COLOR]", color)
                                .Replace("[spray]", graf.Key.ToString())
                                .Replace("[page]", page.ToString())
                        );
                        i++;
                    }

                    break;
                }
                case "setspray":
                {
                    ulong arg1 = ulong.Parse(arg.Args[1]);
                    ulong skin;
                    if (!_config.GrID.ContainsKey(arg1))
                    {
                        player.ChatMessage(lang.GetMessage("NOSKIN", this, player.UserIDString));
                        return;
                    }

                    if (!permission.UserHasPermission(player.UserIDString, _config.GrID[arg1].perm))
                    {
                        player.ChatMessage(lang.GetMessage("NOPERMS", this, player.UserIDString));
                        return;
                    }

                    if (_CustimGrafiffies.TryGetValue(player.userID, out skin))
                    {
                        _CustimGrafiffies[player.userID] = ulong.Parse(arg.Args[1]);
                    }
                    else
                    {
                        _CustimGrafiffies.Add(player.userID, ulong.Parse(arg.Args[1]));
                    }

                    player.ChatMessage(lang.GetMessage("SUCCESS", this, player.UserIDString));
                    rust.RunClientCommand(player, $"CustomGraffiti page {arg.Args[2]}");
                    break;
                }
            }
        }
        
        void CmdOpen(IPlayer user)
        {
            BasePlayer player = user.Object as BasePlayer;
            rust.RunClientCommand(player, "CustomGraffiti page 0");
        }

        #endregion

        #region help

        string Image(ulong id)
        {
            return ImageLibrary.Call<string>("GetImage", $"GRAFFITI_{id}");
        }

        #endregion
    }
}