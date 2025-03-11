using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SoInfo", "rustmods.ru", "1.0.2")]
    public class SoInfo : RustPlugin
    {
        private ConfigData cfg { get; set; }

        private class ConfigData
        {
            [JsonProperty("Стартовая страница")] public string Name = "Капсулы";
            [JsonProperty("Цвет линий")] public string Color = "#E10394";
            [JsonProperty("Цвет линий(Открытой вкладки)")] public string ColorOpen = "#e64544";
            [JsonProperty("Включить открытие при заходе?")]
            public bool coonect = false;
            [JsonProperty("Список кнопок")]
            public Dictionary<string, TextInfo> _itemList = new Dictionary<string, TextInfo>();
            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData();
                newConfig._itemList = new Dictionary<string, TextInfo>()
                {
                    ["Капсулы"] = new TextInfo()
                    {
                        Lable = "КАПСУЛЫ",
                        Text = new List<string>
                        {
                            "Бла бла бла бла бла", 
                        },
                        UnderLable = "Информация по капсулам"
                    },
                    ["SoFriends"] = new TextInfo()
                    {
                        Lable = "СИСТЕМА ДРУЗЕЙ",
                        Text = new List<string>
                        {
                            "Бла бла бла бла бла", 
                        },
                        UnderLable = "Информация по SoFriends"
                    },
                    ["SoReport"] = new TextInfo()
                    {
                        Lable = "РЕПОРТ",
                        Text = new List<string>
                        {
                            "Бла бла бла бла бла", 
                        },
                        UnderLable = "Информация по SoReport"
                    },
                    ["SoPass"] = new TextInfo()
                    {
                        Lable = "БАТЛ ПАСС",
                        Text = new List<string>
                        {
                            "Бла бла бла бла бла", 
                        },
                        UnderLable = "Информация по SoPass"
                    }, 
                    ["SoKits"] = new TextInfo()
                    {
                        Lable = "КИТЫ",
                        Text = new List<string>
                        {
                            "Бла бла бла бла бла", 
                        },
                        UnderLable = "Информация по SoKits"
                    },
                    ["MicorPanel"] = new TextInfo()
                    {
                        Lable = "Микропанель",
                        Text = new List<string>
                        {
                            "Бла бла бла бла бла", 
                        },
                        UnderLable = "Информация по SoPass"
                    },   
                    ["SoInfo"] = new TextInfo()
                    {
                        Lable = "Информация",
                        Text = new List<string>
                        {
                            "Бла бла бла бла бла", 
                        },
                        UnderLable = "Информация по SoKits"
                    },
                    ["SoCraftSystem"] = new TextInfo()
                    {
                        Lable = "Крафт Система",
                        Text = new List<string>
                        {
                            "Бла бла бла бла бла", 
                        },
                        UnderLable = "Информация по SoPass"
                    }, 
                    ["SoTeleport"] = new TextInfo()
                    {
                        Lable = "Телепорь",
                        Text = new List<string>
                        {
                            "Бла бла бла бла бла", 
                        },
                        UnderLable = "Информация по SoKits"
                    },
                };
                return newConfig;
            }
        }

        protected override void LoadDefaultConfig()
        {
            cfg = ConfigData.GetNewConf();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(cfg);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        class TextInfo
        {
            public string Lable;
            public string UnderLable;
            public List<string> Text;
        }
        private static string Layer = "UiSoInfo";
        private static string LayerMain = "UiSoInfoMain";
        private string Hud = "Hud";
        private string Overlay = "Overlay";
        private string regular = "robotocondensed-regular.ttf";
        private static string Sharp = "assets/content/ui/ui.background.tile.psd";
        private static string Blur = "assets/content/ui/uibackgroundblur.mat";
        public static string radial = "assets/content/ui/ui.background.transparent.radial.psd";

        private CuiPanel _fon = new CuiPanel()
        {
            RectTransform =
            {
                AnchorMin = "0 0", 
                AnchorMax = "1 1"
            },
            CursorEnabled = true,
            Image =
            {
                Color = HexToRustFormat("#2f2f26f1"),
                Material = radial,
            }
        };

        private CuiPanel _mainFon = new CuiPanel()
        {
            RectTransform = 
            {
                AnchorMin = "0.5 0.5", 
                AnchorMax = "0.5 0.5", 
                OffsetMin = "-1920 -1080",
                OffsetMax = "1920 1080"
            },
            Image =
            {
                Color = "0 0.2312312 0.312312312 0"
            }
        };

        private CuiPanel _mainFon2 = new CuiPanel()
        {
            RectTransform =
            {
                AnchorMin = "0.3333333 0.3333333", 
                AnchorMax = "0.6666667 0.6666667"
            },
            Image =
            {
                Color = "0 0.2312312 0.312312312 0"
            }
        };

        [ChatCommand("info")]
        private void StartUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var cont = new CuiElementContainer();
            cont.Add(_fon, Overlay, Layer);
            cont.Add(_mainFon, Layer, Layer + "off");
            cont.Add(_mainFon2, Layer + "off", LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.8281241 0.02222228",
                    AnchorMax = "0.9765615 0.08055702"
                },
                Button =
                {
                    Close = Layer, 
                    Color = "0 0 0 0"
                },
                Text =
                {
                    Text = "Покинуть меню", 
                    Align = TextAnchor.MiddleCenter, FontSize = 20
                }
            }, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.8296865 0.03055555", 
                    AnchorMax = "0.978124 0.03296291"
                },
                Button = {
                    
                    Close = Layer, 
                    Color = HexToRustFormat(cfg.Color)
                    
                },
                Text =
                {
                    Text = "", 
                    Align = TextAnchor.MiddleCenter
                }
            }, LayerMain);
            float i = 0;
            foreach (var key in cfg._itemList)
            {
                cont.Add(new CuiButton()
                {
                    Button =
                    {
                        Command = $"uisoinfo {key.Key}", Color = "0 0 0 0"
                    },
                    Text =
                    {
                        Text = $"{key.Key.ToUpper()}", Align = TextAnchor.MiddleCenter, FontSize = 18
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.07864577 {0.7212963 - i}", AnchorMax = $"0.1802083 {0.749074 - i}"
                    }
                }, LayerMain);
                cont.Add(new CuiButton()
                {
                    Button = 
                        {Command = $"uisoinfo {key.Key}", Color = HexToRustFormat(cfg.Color)},
                    Text =
                    {
                        Text = $""
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.07864577 {0.7138891 - i}", AnchorMax = $"0.1802083 {0.7175925 - i}"
                    }
                }, LayerMain, $"Poloska + {key.Key}");
                i += 0.0546f;
            }
            cont.Add(new CuiPanel()
            {
                RectTransform =
                {
                    AnchorMin = "0.2401047 0.1416666", AnchorMax = "0.8145837 0.9666666"
                },
                Image =
                {
                    Color = "0 0 0 0"
                }
            }, LayerMain, LayerMain + "Info");
            TextInfo text;
            if (!cfg._itemList.TryGetValue(cfg.Name, out text)) return;
            string mes = text.Text.Aggregate(String.Empty, (current, m) => current + $"\n{m}");
            cont.Add(new CuiButton()
            {
                Button = {Command = $"uisoinfo {cfg.Name}", Color = HexToRustFormat(cfg.ColorOpen)},
                Text =
                {
                    Text = $""
                },
                RectTransform =
                {
                    AnchorMin = $"0 0", AnchorMax = $"1 1"
                }
            }, $"Poloska + {cfg.Name}", "Open");
            cont.Add(new CuiPanel()
            {
                RectTransform =
                {
                    AnchorMin = "0.2083334 0.1416666", AnchorMax = "0.7828124 0.9666666"
                },
                Image =
                { 
                    Color = "0 0 0 0"
                }
            }, LayerMain, LayerMain + "Info");

            cont.Add(new CuiElement()
            {
                Parent = LayerMain + "Info",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = text.Lable.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 35
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3327288 0.875926", AnchorMax = "0.6854033 0.9472503"
                    }
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = LayerMain + "Info",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = text.UnderLable, Align = TextAnchor.MiddleCenter, FontSize = 14, Font = regular
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3345421 0.8268534", AnchorMax = "0.6817771 0.8851866"
                    }
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = LayerMain + "Info",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = mes, Align = TextAnchor.MiddleCenter, FontSize = 18, Font = regular
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.00543952 0.05723912", AnchorMax = "1 0.7968574"
                    }
                } 
            });
            CuiHelper.AddUi(player, cont);
        }

        [ConsoleCommand("uisoinfo")]
        private void LoadButtonInfo(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            CuiHelper.DestroyUi(player, LayerMain + "Info");
            CuiHelper.DestroyUi(player, "Open");
            var key = String.Join(" ", arg.Args.ToArray());
            TextInfo text;
            if (!cfg._itemList.TryGetValue(key, out text)) return;
            string mes = text.Text.Aggregate(String.Empty, (current, m) => current + $"\n{m}");
            var cont = new CuiElementContainer();
            cont.Add(new CuiButton()
            {
                Button =
                {
                    Command = $"uisoinfo {key}", 
                    Color = HexToRustFormat(cfg.ColorOpen)
                },
                Text =
                {
                    Text = $""
                },
                RectTransform =
                {
                    AnchorMin = $"0 0", AnchorMax = $"1 1"
                }
            }, $"Poloska + {key}", "Open");
            cont.Add(new CuiPanel()
            {
                RectTransform =
                {
                    AnchorMin = "0.2083334 0.1416666", AnchorMax = "0.7828124 0.9666666"
                },
                Image =
                { 
                    Color = "0 0 0 0"
                }
            }, LayerMain, LayerMain + "Info");

            cont.Add(new CuiElement()
            {
                Parent = LayerMain + "Info",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = text.Lable.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 35
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3327288 0.875926", AnchorMax = "0.6854033 0.9472503"
                    }
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = LayerMain + "Info",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = text.UnderLable, Align = TextAnchor.MiddleCenter, FontSize = 14, Font = regular
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3345421 0.8268534", AnchorMax = "0.6817771 0.8851866"
                    }
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = LayerMain + "Info",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = mes, Align = TextAnchor.MiddleCenter, FontSize = 18, Font = regular
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.00543952 0.05723912", AnchorMax = "1 0.7968574"
                    }
                } 
            });
            CuiHelper.AddUi(player, cont);
            Effect Sound1 = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(Sound1, player.Connection);
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if (cfg.coonect) NextTick(() => StartUi(player));
        }
        private void Unload() 
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(basePlayer, Layer);
            }
        }
        #region Help

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion
    }
}
