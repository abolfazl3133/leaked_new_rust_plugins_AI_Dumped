using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("MainMenu", "OxideBro", "1.1.4")]
    public class MainMenu : RustPlugin
    {
        private PluginConfig config;
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за покупку плагина у разработчика OxideBro. Если вы передадите этот плагин сторонним лицам знайте - это лишает вас гарантированных обновлений!");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(1, 1, 2))
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            if (player.IsReceivingSnapshot)
            {
                if (!player.IsConnected) return;
                timer.In(1f, () => OnPlayerConnected(player));
                return;
            }

            if (config.Main.OpenToConnectionFirst)
            {
                if (!PlayersList.ContainsKey(player.userID))
                {
                    CreateMenu(player);
                    PlayersList.Add(player.userID, true);
                    return;
                }
            }
            if (config.Main.OpenToConnection)
                CreateMenu(player);
        }

        class DefaultButton
        {
            [JsonProperty("Позиция AnchorMin")]
            public string AnchorMin;
            [JsonProperty("Позиция AnchorMax")]
            public string AnchorMax;
            [JsonProperty("Текст")]
            public string Text;

            [JsonProperty("Привилегия для отображения (Оставьте поле пустым чтобы отображалось всем)")]
            public string Permission = "";


            [JsonProperty("Настройка изображения")]
            public Images ImagesSetting;

            [JsonProperty("Выполняемая команда (Если это чатовая команда, используйте через слэш)")]
            public string Command;

            [JsonProperty("Цвет кнопки")]
            public string Color;
        }

        public class Images
        {
            [JsonProperty("Ссылка на изображение")]
            public string ImageURL;
            [JsonProperty("Позиция AnchorMin")]
            public string AnchorMin;
            [JsonProperty("Позиция AnchorMax")]
            public string AnchorMax;
            [JsonProperty("Цвет изображения")]
            public string Color;
        }

        class UserAvatar
        {
            [JsonProperty("Позиция AnchorMin")]
            public string AnchorMin;
            [JsonProperty("Позиция AnchorMax")]
            public string AnchorMax;
            [JsonProperty("Прозрачность")]
            public string Color;
        }

        class MainSettings
        {
            [JsonProperty("Показывать при подключении")]
            public bool OpenToConnection = false;

            [JsonProperty("Показывать только при первом подключении")]
            public bool OpenToConnectionFirst = true;

            [JsonProperty("Команды для открытия меню")]
            public List<string> Commands = new List<string>();
        }

        class HomeSettings
        {
            [JsonProperty("Заголовок блока домов игрока")]
            public string Title;
            [JsonProperty("Цвет фона блока")]
            public string ColorHeader;
            [JsonProperty("Позиция AnchorMin")]
            public string AnchorMin;
            [JsonProperty("Позиция AnchorMax")]
            public string AnchorMax;


            [JsonProperty("Шапка: Позиция AnchorMin")]
            public string HeaderAnchorMin;
            [JsonProperty("Шапка: Позиция AnchorMax")]
            public string HeaderAnchorMax;


            [JsonProperty("Настройка иконки")]
            public Images Image;
            [JsonProperty("Текст кнопки сохранения новой точки")]
            public string TextSethome;
            [JsonProperty("Максимальное количество точек домов")]
            public int Limit;
            [JsonProperty("Список привилегий по количеству home (Привилегия: количество)")]
            public Dictionary<string, int> TeleportPrivilages = new Dictionary<string, int>();
            [JsonProperty("Цвет кнопок")]
            public string ColorButtons;
        }

        class FriendSettings
        {
            [JsonProperty("Заголовок блока друзей игрока")]
            public string Title;
            [JsonProperty("Цвет фона блока")]
            public string ColorHeader;

            [JsonProperty("Включить индекатор онлайна")]
            public bool EnabledOnline = false;

            [JsonProperty("Позиция AnchorMin")]
            public string AnchorMin;
            [JsonProperty("Позиция AnchorMax")]
            public string AnchorMax;
            [JsonProperty("Шапка: Позиция AnchorMin")]
            public string HeaderAnchorMin;
            [JsonProperty("Шапка: Позиция AnchorMax")]
            public string HeaderAnchorMax;
            [JsonProperty("Настройка иконки")]
            public Images Image;
            [JsonProperty("Текст кнопки пустой ячейки")]
            public string Text;
            [JsonProperty("Цвет кнопок")]
            public string ColorButtons;

            [JsonProperty("Лимит друзей")]
            public int Limit;
        }

        class OtherSettings
        {
            [JsonProperty("Заголовок блока Дополнительных функций")]
            public string Title;
            [JsonProperty("Цвет фона блока")]
            public string ColorHeader;
            [JsonProperty("Позиция AnchorMin")]
            public string AnchorMin;
            [JsonProperty("Позиция AnchorMax")]
            public string AnchorMax;
            [JsonProperty("Шапка: Позиция AnchorMin")]
            public string HeaderAnchorMin;
            [JsonProperty("Шапка: Позиция AnchorMax")]
            public string HeaderAnchorMax;
            [JsonProperty("Цвет кнопок")]
            public string ColorButtons;
        }

        class PluginConfig
        {
            [JsonProperty("Основные настройки")]
            public MainSettings Main;
            [JsonProperty("Заголовок главной страницы")]
            public string Title = "<b>ТИТЛ-ЗАГОЛОВОК</b> <size=20>ИЛИ</size> <b>НАЗВАНИЕ СЕРВЕРА</b>";
            [JsonProperty("Аватар")]
            public UserAvatar UserAvatar;
            [JsonProperty("Точки дома")]
            public HomeSettings Home;
            [JsonProperty("Друзья")]
            public FriendSettings Friends;
            [JsonProperty("Дополнительные")]
            public OtherSettings Other;
            [JsonProperty("Настройка кнопок")]
            public List<DefaultButton> DefaultButtons = new List<DefaultButton>();
            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();

            [JsonIgnore]
            [JsonProperty("Инициализация сервера ‌‌‌​‌﻿‍")]
            public bool ServerInit = false;
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    Title = "<b>ТИТЛ-ЗАГОЛОВОК</b> <size=20>ИЛИ</size> <b>НАЗВАНИЕ СЕРВЕРА</b>",
                    Main = new MainSettings()
                    {
                        OpenToConnection = true,
                        OpenToConnectionFirst = true,
                        Commands = new List<string>()
                        {
                            "menu",
                            "info"
                        },
                    },
                    Home = new HomeSettings()
                    {
                        AnchorMin = "0.48 0",
                        AnchorMax = "0.73 0.99",
                        ColorButtons = "0 0 0 0.85",
                        ColorHeader = "0 0 0 0.85",
                        HeaderAnchorMin = "0 0.9",
                        HeaderAnchorMax = "1 1",
                        Image = new Images()
                        {
                            AnchorMin = "0.01 0.05",
                            AnchorMax = "0.2 0.9",
                            Color = "1 1 1 1",
                            ImageURL = "https://i.imgur.com/dcbBvDS.png",
                        },
                        TextSethome = "Нажми, чтобы сохранить\n<size=14><b>SETHOME</b></size>",
                        Title = "ВЫБЕРИТЕ СВОЙ ДОМ",
                        TeleportPrivilages = new Dictionary<string, int>()
                        {
                            ["teleportation.vip"] = 3,
                            ["teleportation.elite"] = 5,
                            ["teleportation.promo"] = 7,

                        },
                        Limit = 7
                    },

                    Friends = new FriendSettings()
                    {
                        AnchorMin = "0.74 0.54",
                        AnchorMax = "0.99 0.99",
                        ColorButtons = "0 0 0 0.85",
                        ColorHeader = "0 0 0 0.85",
                        HeaderAnchorMin = "0 0.85",
                        HeaderAnchorMax = "1 1",
                        Image = new Images()
                        {
                            AnchorMin = "0.01 0.15",
                            AnchorMax = "0.16 0.85",
                            Color = "1 1 1 1",
                            ImageURL = "https://i.imgur.com/7heMoLn.png",
                        },
                        Title = "ВЫБЕРИТЕ ДРУГА",
                        Text = "ПУСТО",
                        Limit = 3,
                    },
                    UserAvatar = new UserAvatar()
                    {
                        AnchorMin = "0.001 0.4",
                        AnchorMax = "0.25 0.99",
                        Color = "1 1 1 1"
                    },
                    Other = new OtherSettings()
                    {
                        AnchorMin = "0.74 0.3",
                        AnchorMax = "0.99 0.53",
                        ColorButtons = "0 0 0 0.85",
                        ColorHeader = "0 0 0 0.85",
                        HeaderAnchorMin = "0 0.75",
                        HeaderAnchorMax = "1 1",
                        Title = "Дополнительные функции"
                    },
                    DefaultButtons = new List<DefaultButton>()
                    {
                        new DefaultButton()
                        {
                            AnchorMin = "0.001 0.195",
                            AnchorMax = "0.25 0.38",
                            Command = "/stat",
                            ImagesSetting = new Images(),
                            Color = "0 0 0 0.85",
                            Text = "<size=11>Нажми, чтобы открыть</size>\n<size=30><b>СТАТИСТИКУ</b></size>"
                        },
                        new DefaultButton()
                        {
                            AnchorMin = "0.26 0.8",
                            AnchorMax = "0.47 0.99",
                            Command = "/block",
                            ImagesSetting = new Images(),
                            Color = "0 0 0 0.85",
                            Text = "<size=11>Нажми, чтобы открыть</size>\n<size=27><b>БЛОКИРОВКУ</b></size>"
                        },
                        new DefaultButton()
                        {
                            AnchorMin = "0.26 0.6",
                            AnchorMax = "0.47 0.79",
                            Command = "/kit",
                             ImagesSetting = new Images(),
                            Color = "0 0 0 0.85",
                            Text = "<size=11>Нажми, чтобы открыть</size>\n<size=30><b>НАБОРЫ</b></size>"
                        },
                        new DefaultButton()
                        {
                            AnchorMin = "0.26 0.4",
                            AnchorMax = "0.47 0.59",
                            Command = "/report",
                             ImagesSetting = new Images(),
                            Color = "0 0 0 0.85",
                            Text = "<size=11>Нажми, чтобы отправить</size>\n<size=30><b>ЖАЛОБУ</b></size>"
                        },
                         new DefaultButton()
                         {
                             AnchorMin = "0.26 0.195",
                             AnchorMax = "0.47 0.38",
                             Command = "/cases",
                              ImagesSetting = new Images(),
                             Color = "0 0 0 0.85",
                             Text = "<size=11>Нажми, чтобы открыть</size>\n<size=30><b>КЕЙСЫ</b></size>"
                         },
                         new DefaultButton()
                         {
                             AnchorMin = "0.74 0.1",
                             AnchorMax = "0.82 0.29",
                             Command = "/lot",
                              ImagesSetting = new Images()
                              {
                                  AnchorMin="0.2 0.2", AnchorMax="0.8 0.8",
                                  ImageURL = "https://i.imgur.com/CrPqgAT.png",
                                  Color = "1 1 1 1"
                              },
                             Color = "0 0 0 0.85",
                             Text = ""
                         },


                          new DefaultButton()
                          {
                             AnchorMin = "0.825 0.1",
                             AnchorMax = "0.905 0.29",
                             Command = "/info",
                             ImagesSetting = new Images()
                             {
                                 AnchorMin="0.2 0.2", AnchorMax="0.8 0.8",
                                 ImageURL = "https://i.imgur.com/AAzVQpA.png",
                                 Color = "1 1 1 1"
                             },
                             Color = "0 0 0 0.85",
                             Text = ""
                          },
                           new DefaultButton()
                           {
                              AnchorMin = "0.91 0.1", AnchorMax = "0.99 0.29",
                              Command = "/store",
                              ImagesSetting = new Images()
                              {
                                  AnchorMin="0.2 0.2", AnchorMax="0.8 0.8",
                                  ImageURL = "https://i.imgur.com/YvaDfru.png",
                                  Color = "1 1 1 1"
                              },
                              Color = "0 0 0 0.85",
                              Text = ""
                         },
                    }
                };
            }
        }

        [PluginReference] Plugin ImageLibrary;


        void Loaded()
        {
            LoadData();
            config.Main.Commands.ForEach(c => cmd.AddChatCommand(c, this, cmdOpenMainMenu));
        }

        void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("ImageLibrary not found!");
                return;
            }
            config.ServerInit = true;
            foreach (var buttons in config.DefaultButtons)
            {
                if (!string.IsNullOrEmpty(buttons.ImagesSetting.ImageURL))
                    ImageLibrary?.Call("AddImage", buttons.ImagesSetting.ImageURL, buttons.ImagesSetting.ImageURL);

                if (!string.IsNullOrEmpty(buttons.Permission) && !permission.PermissionExists(buttons.Permission) && buttons.Permission.StartsWith("mainmenu."))
                    permission.RegisterPermission(buttons.Permission, this);

            }
            ImageLibrary?.Call("AddImage", config.Home.Image.ImageURL, config.Home.Image.ImageURL);
            ImageLibrary?.Call("AddImage", config.Friends.Image.ImageURL, config.Friends.Image.ImageURL);
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        public Dictionary<ulong, bool> PlayersList = new Dictionary<ulong, bool>();

        void LoadData()
        {
            try
            {
                PlayersList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, bool>>(Name);
            }
            catch
            {
                PlayersList = new Dictionary<ulong, bool>();
            }
        }

        void SaveData()
        {
            if (PlayersList != null)
                Interface.Oxide.DataFileSystem.WriteObject(Name, PlayersList);
        }

        void Unload()
        {
            config.ServerInit = false;
            BasePlayer.activePlayerList.ToList().ForEach(DestroyUI);
            SaveData();
        }

        int GetFreeHomesCount(BasePlayer player)
        {
            var count = 1;
            foreach (var privilage in config.Home.TeleportPrivilages)
            {
                if (permission.UserHasPermission(player.UserIDString, privilage.Key) && privilage.Value > count)
                    count = privilage.Value;
            }
            return count;
        }

        void DestroyUI(BasePlayer player)
         => CuiHelper.DestroyUi(player, MainLayer);

        private string MainLayer = "MainMenu";

        void CreateMenu(BasePlayer player)
        {
            DestroyUI(player);
            if (!ImageLibrary || !config.ServerInit) return;
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = "Hud",
                Name = MainLayer,
                Components =
                {
                    new CuiImageComponent { Color= "1 1 1 0" },
                    new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },
                    new CuiNeedsCursorComponent{ }
                },
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0.5", Close = MainLayer, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 24 },
            }, MainLayer);


            container.Add(new CuiElement
            {
                Parent = MainLayer,
                Name = MainLayer + "_main",
                Components =
                {
                    new CuiImageComponent { Color="0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin="0.19 0.32", AnchorMax="0.81 0.78" },
                },
            });

            container.Add(new CuiElement
            {
                Parent = MainLayer + "_main",
                Components =
                    {
                        new CuiTextComponent {Text = config.Title,  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 30  },
                        new CuiRectTransformComponent { AnchorMin="0 1", AnchorMax="1 1.3" },

                    },
            });


            container.Add(new CuiElement
            {
                Parent = MainLayer + "_main",
                Components =
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary?.Call("GetImage", player.UserIDString), Color= config.UserAvatar.Color},
                    new CuiRectTransformComponent { AnchorMin= config.UserAvatar.AnchorMin, AnchorMax= config.UserAvatar.AnchorMax },
                },
            });

            int parentName = 0;

            foreach (var button in config.DefaultButtons)
            {
                if (!string.IsNullOrEmpty(button.Permission) && !permission.UserHasPermission(player.UserIDString, button.Permission)) continue;
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = button.AnchorMin, AnchorMax = button.AnchorMax },
                    Button = { Color = button.Color, Command = $"MainMenu_UI command {button.Command}" },
                    Text = { Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 24 },
                }, MainLayer + "_main", MainLayer + parentName);


                if (!string.IsNullOrEmpty(button.Text))
                {
                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + parentName,
                        Components =
                         {
                            new CuiTextComponent {Text = button.Text,  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 11  },
                            new CuiRectTransformComponent { AnchorMin= button.ImagesSetting != null && !string.IsNullOrEmpty(button.ImagesSetting.ImageURL) ? "0.2 0" : "0 0", AnchorMax="1 1" },
                        },
                    });
                }


                if (button.ImagesSetting != null && !string.IsNullOrEmpty(button.ImagesSetting.ImageURL))
                {
                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + parentName,
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary?.Call("GetImage", button.ImagesSetting.ImageURL), Color= button.ImagesSetting.Color},
                            new CuiRectTransformComponent { AnchorMin= button.ImagesSetting.AnchorMin, AnchorMax= button.ImagesSetting.AnchorMax},
                        },
                    });
                }
                parentName++;
            }

            container.Add(new CuiElement
            {
                Parent = MainLayer + "_main",
                Name = MainLayer + "_mainHOME",

                Components =
                {
                    new CuiImageComponent { Color= "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = config.Home.AnchorMin, AnchorMax = config.Home.AnchorMax  },
                },
            });

            container.Add(new CuiElement
            {
                Parent = MainLayer + "_mainHOME",
                Name = MainLayer + "_homeTitle",

                Components =
                {
                    new CuiImageComponent { Color= config.Home.ColorHeader },
                    new CuiRectTransformComponent { AnchorMin = config.Home.HeaderAnchorMin, AnchorMax = config.Home.HeaderAnchorMax },
                },
            });
            container.Add(new CuiElement
            {
                Parent = MainLayer + "_homeTitle",
                Components =
                {
                    new CuiTextComponent {Text =  config.Home.Title,  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 11 },
                    new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },
                },
            });

            container.Add(new CuiElement
            {
                Parent = MainLayer + "_mainHOME",
                Name = MainLayer + "_mainHOMELIST",

                Components =
                {
                    new CuiImageComponent { Color= "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 {double.Parse(config.Home.HeaderAnchorMin.Split(' ')[1]) - 0.02}" },
                },
            });

            var pos = GetPositions(1, 7, 0.01f, 0.01f);
            int hom = 0;
            var homes = GetHomes(player);
            var max = GetFreeHomesCount(player) < config.Home.Limit ? config.Home.Limit : GetFreeHomesCount(player);


            if (homes != null && homes.Count > 0)
            {
                foreach (var home in homes.Take(config.Home.Limit))
                {
                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + "_mainHOMELIST",
                        Name = MainLayer + "_Home" + home.Key,
                        Components =
                {
                    new CuiImageComponent {Color=config.Home.ColorButtons},
                    new CuiRectTransformComponent { AnchorMin = pos[hom].AnchorMin, AnchorMax = pos[hom].AnchorMax  },

                },
                    });

                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + "_Home" + home.Key,
                        Components =
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary?.Call("GetImage", config.Home.Image.ImageURL), Color= config.Home.Image.Color},
                    new CuiRectTransformComponent { AnchorMin= config.Home.Image.AnchorMin, AnchorMax= config.Home.Image.AnchorMax },

                },
                    });

                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + "_Home" + home.Key,
                        Components =
                        {
                            new CuiTextComponent {Text = home.Key,  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 25  },
                            new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },

                        },
                    });

                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + "_Home" + home.Key,
                        Components =
                        {
                            new CuiButtonComponent {Color="1 1 1 0", Command = $"MainMenu_UI command /home {home.Key}" },
                            new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },

                        },
                    });

                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + "_Home" + home.Key,
                        Name = MainLayer + "_Home" + home.Key + "_remove",
                        Components =
                        {
                            new CuiButtonComponent {Color="0.98 0.22 0.37 0", Command = $"MainMenu_UI rshow {home.Key} {MainLayer + "_Home" + home.Key}" },
                            new CuiRectTransformComponent { AnchorMin="0.8 0", AnchorMax="1 1" },

                        },
                    });

                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + "_Home" + home.Key + "_remove",
                        Components =
                        {
                            new CuiTextComponent {Text = "X",  Color="0.98 0.22 0.37 1", Align = TextAnchor.MiddleCenter, FontSize = 25, Font = "robotocondensed-regular.ttf",  },
                            new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },

                        },
                    });


                    hom++;
                }
            }

            for (int i = hom; i < max; i++)
            {
                container.Add(new CuiElement
                {
                    Parent = MainLayer + "_mainHOMELIST",
                    Name = MainLayer + "_Home_" + i,
                    Components =
                {
                    new CuiImageComponent {Color= config.Home.ColorButtons },
                    new CuiRectTransformComponent { AnchorMin = pos[i].AnchorMin, AnchorMax = pos[i].AnchorMax  },

                },
                });

                container.Add(new CuiElement
                {
                    Parent = MainLayer + "_Home_" + i,
                    Components =
                {
                    new CuiTextComponent {Text = config.Home.TextSethome,  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 11  },
                    new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },
                },
                });


                var name = GetGridString(player.transform.position);

                if (homes.ContainsKey(name))
                {
                    var count = homes.Where(p => p.Key.Contains(name)).Count();
                    name = name + $"({count})";
                }

                container.Add(new CuiElement
                {
                    Parent = MainLayer + "_Home_" + i,
                    Components =
                {
                    new CuiButtonComponent {Color="1 1 1 0", Command = $"MainMenu_UI command /sethome {name}" },
                    new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },

                },
                });


            }

            container.Add(new CuiElement
            {
                Parent = MainLayer + "_main",
                Name = MainLayer + "_mainHFriends",

                Components =
                {
                    new CuiImageComponent { Color= "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = config.Friends.AnchorMin, AnchorMax = config.Friends.AnchorMax  },
                },
            });
            container.Add(new CuiElement
            {
                Parent = MainLayer + "_mainHFriends",
                Name = MainLayer + "_mainFRIEND",

                Components =
                {
                    new CuiImageComponent { Color= config.Friends.ColorHeader },
                    new CuiRectTransformComponent { AnchorMin = config.Friends.HeaderAnchorMin, AnchorMax = config.Friends.HeaderAnchorMax  },
                },
            });

            container.Add(new CuiElement
            {
                Parent = MainLayer + "_mainFRIEND",
                Components =
                {
                    new CuiTextComponent {Text = config.Friends.Title,  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 11 },
                    new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },
                },
            });


            container.Add(new CuiElement
            {
                Parent = MainLayer + "_mainHFriends",
                Name = MainLayer + "_mainFRIENDLIST",

                Components =
                {
                    new CuiImageComponent { Color= "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 {double.Parse(config.Friends.HeaderAnchorMin.Split(' ')[1]) - 0.02}"  },
                },
            });

            var friends = GetFriends(player.userID);
            var posF = GetPositions(1, config.Friends.Limit, 0.01f, 0.02f);
            int fr = 0;
            if (friends != null && friends.Count > 0)
            {
                foreach (var friend in friends.Take(config.Friends.Limit))
                {
                    var covFriend = covalence.Players.FindPlayerById(friend.ToString());
                    if (covFriend == null) continue;
                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + "_mainFRIENDLIST",
                        Name = MainLayer + "_Friend" + friend,
                        Components =
                {
                    new CuiImageComponent {Color= config.Friends.ColorButtons },
                    new CuiRectTransformComponent { AnchorMin = posF[fr].AnchorMin, AnchorMax = posF[fr].AnchorMax  },

                },
                    });

                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + "_Friend" + friend,
                        Components =
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary?.Call("GetImage", config.Friends.Image.ImageURL), Color= config.Friends.Image.Color },
                    new CuiRectTransformComponent { AnchorMin= config.Friends.Image.AnchorMin, AnchorMax= config.Friends.Image.AnchorMax },

                },
                    });

                    container.Add(new CuiElement
                    {
                        Name = MainLayer + "_Friend" + friend + covFriend.Id,

                        Parent = MainLayer + "_Friend" + friend,
                        Components =
                        {
                            new CuiTextComponent {Text = $"{covFriend.Name}"  ,  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 15  },
                            new CuiRectTransformComponent { AnchorMin="0.1 0", AnchorMax="1 1" },

                        },
                    });
                    container.Add(new CuiElement
                    {
                        Parent = MainLayer + "_Friend" + friend,
                        Components =
                        {
                            new CuiButtonComponent {Color="1 1 1 0", Command = $"MainMenu_UI command /tpr {covFriend.Id}" },
                            new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },

                        },
                    });

                    if (config.Friends.EnabledOnline)
                        container.Add(new CuiElement
                        {
                            Parent = MainLayer + "_Friend" + friend + covFriend.Id,
                            Components =
                        {
                            new CuiImageComponent {Color= covFriend.IsConnected ?  "0.04 0.93 0.48 1.00" : "0.98 0.22 0.37 1.00" },
                            new CuiRectTransformComponent { AnchorMin = "0.2 0.1", AnchorMax = "0.8 0.15"  },

                        },
                        });

                    fr++;
                }
            }

            for (int i = fr; i < config.Friends.Limit; i++)
            {
                container.Add(new CuiElement
                {
                    Parent = MainLayer + "_mainFRIENDLIST",
                    Name = MainLayer + "_Friend" + i,
                    Components =
                {
                    new CuiImageComponent {Color= config.Friends.ColorButtons },
                    new CuiRectTransformComponent { AnchorMin = posF[i].AnchorMin, AnchorMax = posF[i].AnchorMax  },

                },
                });
                container.Add(new CuiElement
                {
                    Parent = MainLayer + "_Friend" + i,
                    Components =
                    {
                        new CuiTextComponent {Text = "ПУСТО",  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 18  },
                        new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },

                    },
                });
            }
            container.Add(new CuiElement
            {
                Parent = MainLayer + "_main",
                Name = MainLayer + "_mainOther",
                Components =
                {
                    new CuiImageComponent { Color= "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = config.Other.AnchorMin, AnchorMax = config.Other.AnchorMax  },
                },
            });
            container.Add(new CuiElement
            {
                Parent = MainLayer + "_mainOther",
                Name = MainLayer + "_mainOther1",

                Components =
                {
                    new CuiImageComponent { Color= config.Other.ColorHeader },
                    new CuiRectTransformComponent { AnchorMin = config.Other.HeaderAnchorMin, AnchorMax = config.Other.HeaderAnchorMax },
                },
            });
            container.Add(new CuiElement
            {
                Parent = MainLayer + "_mainOther1",
                Components =
                {
                    new CuiTextComponent {Text = config.Other.Title,  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 11 },
                    new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },
                },
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.49 0.685" },
                Button = { Color = config.Other.ColorButtons, Command = "MainMenu_UI command /trade yes" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 24 },
            }, MainLayer + "_mainOther", MainLayer + "_main_Button");


            container.Add(new CuiElement
            {
                Parent = MainLayer + "_main_Button",
                Components =
                {
                    new CuiTextComponent {Text = "Трейд",  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 11  },
                    new CuiRectTransformComponent { AnchorMin="0 0.65", AnchorMax="1 1" },
                },
            });
            container.Add(new CuiElement
            {
                Parent = MainLayer + "_main_Button",
                Components =
                {
                    new CuiTextComponent {Text = "ПРИНЯТЬ",  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 22  },
                    new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 0.8" },
                },
            });

            if (Trade?.Call("PlayerGetActiveTrade", player) != null && (bool)Trade?.Call("PlayerGetActiveTrade", player))
            {
                container.Add(new CuiElement
                {
                    Parent = MainLayer + "_main_Button",

                    Components =
                {
                    new CuiImageComponent { Color= "0.04 0.93 0.48 1.00" },
                    new CuiRectTransformComponent { AnchorMin = "0.1 0.1", AnchorMax = "0.9 0.17"  },

                },
                });
            }
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.51 0", AnchorMax = "1 0.685" },
                Button = { Color = config.Other.ColorButtons, Command = "MainMenu_UI command /tpa" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 24 },
            }, MainLayer + "_mainOther", MainLayer + "_main_Button");


            container.Add(new CuiElement
            {
                Parent = MainLayer + "_main_Button",
                Components =
                {
                    new CuiTextComponent {Text = "Телепорт",  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 11  },
                    new CuiRectTransformComponent { AnchorMin="0 0.65", AnchorMax="1 1" },

                },
            });

            container.Add(new CuiElement
            {
                Parent = MainLayer + "_main_Button",
                Components =
                {
                    new CuiTextComponent {Text = "ПРИНЯТЬ",  Color="1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 22  },
                    new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 0.8" },

                },
            });

            if (Teleportation?.Call("IsActiveQueueTP", player) != null && (bool)Teleportation?.Call("IsActiveQueueTP", player))
            {
                container.Add(new CuiElement
                {
                    Parent = MainLayer + "_main_Button",

                    Components =
                {
                    new CuiImageComponent { Color= "0.04 0.93 0.48 1.00" },
                    new CuiRectTransformComponent {  AnchorMin = "0.1 0.1", AnchorMax = "0.9 0.17"  },

                },
                });
            }
            CuiHelper.AddUi(player, container);
        }

        private string GetGridString(Vector3 position)
        {
            Vector2 adjPosition = new Vector2((World.Size / 2) + position.x, (World.Size / 2) - position.z);
            return $"{NumberToString((int)(adjPosition.x / 150))}{(int)(adjPosition.y / 150)}";
        }

        private string NumberToString(int number)
        {
            bool a = number > 26;
            Char c = (Char)(65 + (a ? number - 26 : number));
            return a ? "A" + c : c.ToString();
        }

        [ConsoleCommand("MainMenu_UI")]
        void cmdMainMenuCommands(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;
            if (!args.HasArgs(1)) return;
            switch (args.Args[0])
            {
                case "command":
                    DestroyUI(player);
                    if (args.FullString.Contains("/"))
                        player.Command("chat.say", args.FullString.Replace("command ", ""));
                    else
                        player.Command(args.FullString.Replace("command ", ""));
                    break;
                case "home":
                    if (!args.HasArgs(3)) return;
                    player.Command("chat.say", $"{args.Args[1]} {args.Args[2]}");
                    timer.Once(0.1f, () => CreateMenu(player));
                    break;
                case "rshow":
                    if (!args.HasArgs(3)) return;

                    var home = args.Args[1];
                    var parent = args.Args[2];
                    CreateRemoveHomse(player, home, parent);
                    break;
            }
        }

        void CreateRemoveHomse(BasePlayer player, string home, string parrent)
        {
            CuiHelper.DestroyUi(player, $"MainMenu_removehome{home}");
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = parrent,
                Name = $"MainMenu_removehome{home}",
                Components =
                {

                    new CuiImageComponent { Color= "0.98 0.22 0.37 0.98", FadeIn = 0.3f  },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                },
            });

            container.Add(new CuiElement
            {
                Parent = $"MainMenu_removehome{home}",
                Components =
                        {
                            new CuiTextComponent {Color="1 1 1 1", Text = "УДАЛИТЬ ?", Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "robotocondensed-regular.ttf", FadeIn = 0.3f },
                            new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="0.6 0.97" },
                            new CuiOutlineComponent{Color = "0 0 0 1", Distance = "-0.3 0.3"}

                        },
            });

            container.Add(new CuiElement
            {
                Parent = $"MainMenu_removehome{home}",
                Name = $"MainMenu_removehome_accept",

                Components =
                        {
                            new CuiButtonComponent {Color="0.99 0.49 0.59 1.00", Command = $"MainMenu_UI home /removehome {home}", FadeIn = 0.3f },
                            new CuiRectTransformComponent { AnchorMin="0.6 0", AnchorMax="0.8 0.97" },

                        },
            });

            container.Add(new CuiElement
            {
                Parent = $"MainMenu_removehome_accept",
                Components =
                        {
                            new CuiTextComponent {Color="1 1 1 1", Text = "✓", Align = TextAnchor.MiddleCenter, FontSize = 22, Font = "robotocondensed-regular.ttf", FadeIn = 0.3f  },
                            new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },

                        },
            });

            container.Add(new CuiElement
            {
                Parent = $"MainMenu_removehome{home}",
                Name = $"MainMenu_removehome_decline",

                Components =
                        {
                            new CuiButtonComponent {Color="0.82 0.35 0.36 1.00", Close = $"MainMenu_removehome{home}", FadeIn = 0.3f },
                            new CuiRectTransformComponent { AnchorMin="0.8 0", AnchorMax="1 0.97" },

                        },
            });

            container.Add(new CuiElement
            {
                Parent = $"MainMenu_removehome_decline",
                Components =
                        {
                            new CuiTextComponent {Color="1 1 1 1", Text = "X", Align = TextAnchor.MiddleCenter, FontSize = 22, Font = "robotocondensed-regular.ttf", FadeIn = 0.3f  },
                            new CuiRectTransformComponent { AnchorMin="0 0", AnchorMax="1 1" },

                        },
            });
            CuiHelper.AddUi(player, container);
        }


        [PluginReference] Plugin Friends, NTeleportation, Teleport, Teleportation, HomesGUI, Trade, MutualPermission;

        public List<ulong> GetFriends(ulong playerid = 0)
        {
            if (MutualPermission)
            {
                var MutualFr = MutualPermission?.Call("GetFriends", playerid) as List<ulong>;
                return MutualFr;
            }
            if (Friends)
            {
                var friends = Friends?.Call("GetFriends", playerid) as ulong[];
                if (friends == null) return new List<ulong>();
                return friends.ToList();
            }

            return new List<ulong>();
        }

        Dictionary<string, Vector3> GetHomes(BasePlayer player)
        {
            var a1 = (Dictionary<string, Vector3>)NTeleportation?.Call("API_GetHomes", player) ?? new Dictionary<string, Vector3>();
            var a2 = (Dictionary<string, Vector3>)Teleport?.Call("ApiGetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a3 = (Dictionary<string, Vector3>)Teleportation?.Call("GetHomes", player.userID) ?? new Dictionary<string, Vector3>();
            var a4 = (Dictionary<string, Vector3>)HomesGUI?.Call("GetPlayerHomes", player.UserIDString) ?? new Dictionary<string, Vector3>();
            return a1.Concat(a2).Concat(a3).Concat(a4).GroupBy(p => p.Key).ToDictionary(p => p.Key, p => p.First().Value);
        }

        void cmdOpenMainMenu(BasePlayer player, string com, string[] args)
        {
            CreateMenu(player);
        }

        class Position
        {
            public float Xmin;
            public float Xmax;
            public float Ymin;
            public float Ymax;

            public string AnchorMin =>
                $"{Math.Round(Xmin, 4).ToString(CultureInfo.InvariantCulture)} {Math.Round(Ymin, 4).ToString(CultureInfo.InvariantCulture)}";
            public string AnchorMax =>
                $"{Math.Round(Xmax, 4).ToString(CultureInfo.InvariantCulture)} {Math.Round(Ymax, 4).ToString(CultureInfo.InvariantCulture)}";

            public override string ToString()
            {
                return "----------\nAmin:{AnchorMin}\nAmax:{AnchorMax}\n----------‌";
            }
        }


        private static List<Position> GetPositions(int colums, int rows, float colPadding = 0, float rowPadding = 0, bool columsFirst = false)
        {
            if (colums == 0)
                throw new ArgumentException("Can't create positions for gui!‌", nameof(colums));
            if (rows == 0)
                throw new ArgumentException("Can't create positions for gui!", nameof(rows));

            List<Position> result = new List<Position>();
            result.Clear();
            var colsDiv = 1f / colums;
            var rowsDiv = 1f / rows;
            var reply = 0;

            if (colPadding == 0) colPadding = colsDiv / 2;
            if (rowPadding == 0) rowPadding = rowsDiv / 2;
            if (!columsFirst)
                for (int j = rows; j >= 1; j--)
                {
                    for (int i = 1; i <= colums; i++)
                    {
                        Position pos = new Position
                        {
                            Xmin = (i - 1) * colsDiv + colPadding / 2f,
                            Xmax = i * colsDiv - colPadding / 2f,
                            Ymin = (j - 1) * rowsDiv + rowPadding / 2f,
                            Ymax = j * rowsDiv - rowPadding / 2f
                        };
                        result.Add(pos);
                    }
                }
            else
                for (int i = 1; i <= colums; i++)
                {
                    for (int j = rows; j >= 1; j--)
                    {
                        Position pos = new Position
                        {
                            Xmin = (i - 1) * colsDiv + colPadding / 2f,
                            Xmax = i * colsDiv - colPadding / 2f,
                            Ymin = (j - 1) * rowsDiv + rowPadding / 2f,
                            Ymax = j * rowsDiv - rowPadding / 2f
                        };
                        result.Add(pos);
                    }
                }
            return result;
        }
    }
}