using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Batia", "Skuli Dropek", "1.0.1")]
    internal class Batia : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary,BroadcastSystem;

        void Loaded()
        {
            ins = this;
        }
        void OnServerInitialized()
        {
            if (ImageLibrary)
            {
                var images = config.bottleSetting.CustomItemsShop.Where(p => !string.IsNullOrEmpty(p.ImageURL));
                foreach (var check in images)
                    ImageLibrary.Call("AddImage", check.ImageURL, check.ImageURL);

                foreach (var check in config.artefactsSettings.ItemsAdded)
                {
                    ImageLibrary.Call("AddImage", check.ImageURL, check.ImageURL);
                }

                ImageLibrary.Call("AddImage", "https://gspics.org/images/2022/01/14/0QKR0I.png", "https://gspics.org/images/2022/06/04/0nWJCJ.png");
                ImageLibrary.Call("AddImage", "https://gspics.org/images/2022/06/04/0nWcGe.png", "https://gspics.org/images/2022/06/04/0nWcGe.png");
                ImageLibrary.Call("AddImage", "https://gspics.org/images/2022/06/04/0nWsOm.png", "https://gspics.org/images/2022/06/04/0nWsOm.png");
                ImageLibrary.Call("AddImage", "https://gspics.org/images/2022/01/16/0QsHPj.png", "https://gspics.org/images/2022/01/16/0QsHPj.png");
            }
            if (config.gameStoresSettings == null)
            {
                config.gameStoresSettings = new GameStoresSettings() { Secret = "", ShopID = "" };
                SaveConfig();
            }

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        public string External = "UI_Batia_External";
        public string Internal = "UI_Batia_Internal";

        void DrawNPCUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, External);
            CuiHelper.DestroyUi(player, Internal);

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0.235 0.227 0.180 0" }
            }, "SubContent_UI", External);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.2" }
            }, External);


            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, External);



            CuiHelper.AddUi(player, container);
            InitializeLayers(player);

            if (com != null && com.EndExpeditions.ContainsKey(player.userID))
                CreateInfoPlayer(player, true);
        }


        void CreateInfoPlayer(BasePlayer player, bool opened)
        {
            if (opened)
            {
                player.Command("UI_Batia givepackage");
            }
            else
            {
                player.Command("UI_Batia givepackage");
            }
        }

        string GetMoodTranslate(FatherComponent.Mood mood)
        {
            switch (mood)
            {
                case FatherComponent.Mood.Neutral:
                    return "СРЕДНЕЕ";

                case FatherComponent.Mood.Kind:
                    return "УЛУЧШЕННОЕ";

                case FatherComponent.Mood.Evil:
                    return "ПЕРЕБОИ ПОСТАВОК";
            }
            return "НЕЙТРАЛЬНАЯ";
        }


        string GetMoodInfoRmation(FatherComponent.Mood mood)
        {
            switch (mood)
            {
                case FatherComponent.Mood.Neutral:
                    return "От поставок в кузницу зависит курс обмена предметов. Курс обмена предметов в кузнице стандартные. Чем лучше снабжение, тем больше предметов вам предложат.";

                case FatherComponent.Mood.Kind:
                    return "От поставок в кузницу зависит курс обмена предметов. Курс обмена предметов в кузнице увеличены.  Чем лучше снабжение, тем больше предметов вам предложат.";


                case FatherComponent.Mood.Evil:
                    return "Перебои с поставками. Курс обмена снижен. Лучше ожидайте более высокие поставки в кузницу.";
            }

            return "От поставок в кузницу зависит курс обмена предметов. Курс обмена предметов в кузнице стандартные. Чем лучше снабжение, тем больше предметов вам предложат.";

        }



        string InitialLayer = "UI_Batia_InitialLayer";
        private void InitializeLayers(BasePlayer player, string SelectMenu = "")
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.15 0", AnchorMax = "1 1", OffsetMin = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, External, InitialLayer);

            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.7 0", AnchorMax = "0.9 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0.24 0.45 0.90 0", Material = "" }
            }, InitialLayer, InitialLayer + ".C");


            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.01 0", AnchorMax = "0.65 0.75", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0.24 0.45 0.90 0", Material = "" }
            }, InitialLayer, InitialLayer + ".R");
            CuiHelper.DestroyUi(player, InitialLayer);
            CuiHelper.AddUi(player, container);
            DrawMenuPoints(player);
        }

        void CreateInfoJson(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, InitialLayer + ".Expedition");

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "-0.39 0", AnchorMax = "1 1" },
                Image = { Color = "0.24 0.45 0.90 0" }
            }, InitialLayer + ".R", InitialLayer + ".Expedition");



            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.1 0.01", AnchorMax = "0.9 0.5" },
                Image = { Color = "0.24 0.45 0.90 0" }
            }, InitialLayer + ".Expedition", InitialLayer + ".ItemsList");


            container.Add(new CuiElement
            {
                Parent = InitialLayer + ".ItemsList",
                Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = $"СПИСОК ПРЕДМЕТОВ И ОЧКИ ЗА НИХ:", Align = TextAnchor.UpperLeft , FontSize = 15},
                            new CuiRectTransformComponent {AnchorMin = "0 1", AnchorMax = "1 1.1"}
                        }
            });


            var pos = GetPositions(10, 5, 0.01f, 0.02f);
            int count = 0;
            var itemsList = config.expeditionSettings.ItemsAdded.OrderBy(p => p.Value.amount).Skip(page * 50).Take(50);

            foreach (var item in itemsList)
            {
                container.Add(new CuiPanel()
                {
                    RectTransform = { AnchorMin = pos[count].AnchorMin, AnchorMax = pos[count].AnchorMax },
                    Image = { Color = "0 0 0 0" }
                }, InitialLayer + ".ItemsList", InitialLayer + item.Key);

                container.Add(new CuiElement
                {
                    Parent = InitialLayer + item.Key,
                    Components =
                            {
                                new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", item.Key), Color = "1 1 1 0.6"},
                                new CuiRectTransformComponent { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.95" }
                            }
                });


                container.Add(new CuiElement
                {
                    Parent = InitialLayer + item.Key,
                    Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = $"{item.Value.amount}", Align = TextAnchor.MiddleCenter , FontSize = 23,Font="robotocondensed-regular.ttf"},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                });


                container.Add(new CuiElement
                {
                    Parent = InitialLayer + item.Key,
                    Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 0.5", Text = $"MAX {item.Value.maxCount}", Align = TextAnchor.LowerCenter , FontSize = 12,Font="robotocondensed-regular.ttf"},
                            new CuiRectTransformComponent {AnchorMin = "0 0.05", AnchorMax = "1 1"}
                        }
                });


                count++;
            }

            container.Add(new CuiElement
            {
                Parent = InitialLayer + ".Expedition",
                Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = $"Здесь вы можете увидеть список предметов которые вы можете обменять, и количество очков, которые они дают. Оператор может отправить 5 разных посылок , в зависимости от количества набранных очков. Длительность доставки посылок около <b>1</b> часа.\n\n<size=14><b>Виды посылок:</b>\n        Потрепанная сумка (<b>от 20 до 20 очков</b>).\n        Слегка порванная сумка (<b>от 20 до 40 очков</b>).\n        Обычная сумка (<b>от 40 до 60 очков</b>).\n        Отличный контейнер (<b>от 60 до 80 очков</b>).\n        Презент от Оператора (<b>от 80 до 100 очков</b>).\n\n*<size=12>Максимально количество очков - 100.</size></size>", Align = TextAnchor.UpperLeft ,Font="robotocondensed-regular.ttf", FontSize = 16},
                            new CuiRectTransformComponent {AnchorMin = "0.1 0", AnchorMax = "1 1"}
                        }
            });

            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0" , Command = page > 0 ? $"UI_Batia expedition {page -1}" : ""
                },
                Text =
                {
                    Text = "◀", FontSize = 55, Align = TextAnchor.LowerRight, Color = page > 0 ? "0.929 0.882 0.847 0.7" : "0.929 0.882 0.847 0.1"
                },
                RectTransform =
                {
                    AnchorMin = $"0 0.2", AnchorMax = $"0.1 0.4"
                }
            }, InitialLayer + ".Expedition");

            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0", Command =  page < 2 ? $"UI_Batia expedition {page +1}" : ""
                },
                Text =
                {
                    Text = "▶", FontSize = 55, Align = TextAnchor.LowerLeft, Color = page < 2  ? "0.929 0.882 0.847 0.7" :  "0.929 0.882 0.847 0.1"
                },
                RectTransform =
                {
                  AnchorMin = $"0.9 0.2", AnchorMax = $"1 0.4"
                }
            }, InitialLayer + ".Expedition");


            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.5 0.6", AnchorMax = "0.89 0.85" },
                Image = { Color = "0.77 0.74 0.71 0" }
            }, InitialLayer + ".Expedition", InitialLayer + ".buttonAccept");

            container.Add(new CuiElement
            {
                Parent = InitialLayer + ".buttonAccept",
                Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = $"Нажми что бы передать предметы на обмен".ToUpper(), Align = TextAnchor.UpperCenter ,Font="robotocondensed-regular.ttf", FontSize = 14},
                            new CuiRectTransformComponent {AnchorMin = "0.1 0", AnchorMax = "0.9 0.95"}
                        }
            });

            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0.41 0.47 0.26 1.00" , Command = $"UI_Batia startLoot", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                Text =
                {
                    Text = "НАЧАТЬ", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.59 0.69 0.42 0.7"
                },
                RectTransform =
                {
                    AnchorMin = $"0.15 0.2", AnchorMax = $"0.9 0.6"
                }
            }, InitialLayer + ".buttonAccept");
            CuiHelper.AddUi(player, container);
            
            //UIList.Add(container.ToJson());
        }

        void CreateInfo(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "-0.39 0", AnchorMax = "1 1" },
                Image = { Color = "0.24 0.45 0.90 0" }
            }, InitialLayer + ".R", InitialLayer + ".Info");

            container.Add(new CuiElement
            {
                Parent = InitialLayer + ".Info",
                Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = $"Это комендант Волтбой, и она может облегчить вашу жизнь в этом мире, если вы конечно заслужите это.", Align = TextAnchor.UpperLeft ,Font="robotocondensed-regular.ttf", FontSize = 16},
                            new CuiRectTransformComponent {AnchorMin = "0.1 0", AnchorMax = "1 0.98"}
                        }
            });

            container.Add(new CuiElement
            {
                Parent = InitialLayer + ".Info",
                Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = $"Волтбой имеет <b>настроение</b>, от которого будет зависеть напрямую количество и качество предметов которые Вы можете получить от него (настроение указанно в верхней части экрана)", Align = TextAnchor.UpperLeft ,Font="robotocondensed-regular.ttf", FontSize = 14},
                            new CuiRectTransformComponent {AnchorMin = "0.1 0", AnchorMax = "1 0.9"}
                        }
            });

            container.Add(new CuiElement
            {
                Parent = InitialLayer + ".Info",
                Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = "<size=18><b>Вкладка 'ПОСЫЛКИ'</b></size> \nВолтбой может забрать ваши предметы, позвонить своим барахольщикам и дать что-то лучше, чем Вы предложили ей.\nКаждый предмет имеет определенное количество очков равноценное его редкости и сложности получения. (чем предмет легче получить, тем меньше очков он дает).\nМаксимальное внесенное количество предметов: <b>10 единиц</b>\nМаксимальное количество получаемых предметов: <b>от 4 до 6 единиц</b>\nТак что не надейтесь получить за свое копье какой-нибудь AK47 или LR300.\nКогда Волтбой закончит с Вашей посылкой, она пришлет ее вам в раздел инвентарь. Появиться соответствующее уведомление на экране.\nКоличество и качество предметов зависит напрямую от настроения коменданта.\nВремя на каждую 'Посылку' рандомное.\nПопросить следующую посылку нельзя пока активна предыдущая.", Align = TextAnchor.UpperLeft ,Font="robotocondensed-regular.ttf", FontSize = 14},
                            new CuiRectTransformComponent {AnchorMin = "0.1 0", AnchorMax = "1 0.78"}
                        }
            });

            container.Add(new CuiElement
            {
                Parent = InitialLayer + ".Info",
                Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = "<size=18><b>Вкладка 'Археотек'</b></size> \nВолтбой с удовольствием примет ваши крышки и обменяет их на что-то ценное.\nПредметы и цены в обменнике меняться рандомно раз в некоторое время.", Align = TextAnchor.UpperLeft ,Font="robotocondensed-regular.ttf", FontSize = 14},
                            new CuiRectTransformComponent {AnchorMin = "0.1 0", AnchorMax = "1 0.3"}
                        }
            });

            CuiHelper.DestroyUi(player, InitialLayer + ".Info");
            CuiHelper.AddUi(player, container);
        }

        void ShowTOPArtefacts(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "-0.39 0", AnchorMax = "1 1" },
                Image = { Color = "0.24 0.45 0.90 0" }
            }, InitialLayer + ".R", InitialLayer + ".Info");

            container.Add(new CuiElement
            {
                Parent = InitialLayer + ".Info",
                Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text =$"Каждый  рестарт сервера, три игрока могут обменять шесть модификаций <b>3 уровня</b> на <b>300 рублей</b> в премиум магазине.\n<b>Виды НЮКА 'КОЛЫ':</b> 'ДИКАЯ', 'КВАНТОВАЯ', 'АПЕЛЬСИНОВАЯ', 'ВИНОГРАДНАЯ', 'ВИШНЕВАЯ', 'ВИКТОРИЯ'\n", Align = TextAnchor.UpperLeft ,Font="robotocondensed-regular.ttf", FontSize = 16},
                            new CuiRectTransformComponent {AnchorMin = "0.1 0", AnchorMax = "0.9 0.99"}
                        }
            });

            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.05 0.32", AnchorMax = "1 0.74" },
                Image = { Color = "0.24 0.45 0.90 0" }
            }, InitialLayer + ".Info", InitialLayer + ".Artefacts.Players");


            var playerPos = GetPositions(3, 1, 0.05f, 0.08f);

            for (int i = 0; i < 3; i++)
            {
                container.Add(new CuiPanel()
                {
                    RectTransform = { AnchorMin = playerPos[i].AnchorMin, AnchorMax = playerPos[i].AnchorMax },
                    Image = { Color = "0 0 0 0" }
                }, InitialLayer + ".Artefacts.Players", InitialLayer + ".Artefacts.Players" + i);

                if (ArtefactsList.Count > i)
                {
                    var playerID = ArtefactsList[i];

                    container.Add(new CuiElement
                    {
                        Parent = InitialLayer + ".Artefacts.Players" + i,
                        Components =
                    {
                        new CuiRawImageComponent {Png            = (string) ImageLibrary?.Call("GetImage", playerID.ToString()) },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 10", OffsetMax = "-10 -10"}
                    }
                    });

                    container.Add(new CuiPanel()
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Image = { Color = "0.235 0.227 0.180 0" }
                    }, InitialLayer + ".Artefacts.Players" + i);

                    container.Add(new CuiElement
                    {
                        Parent = InitialLayer + ".Artefacts.Players" + i,
                        Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = permission.GetUserData(playerID.ToString()).LastSeenNickname.ToUpper(), Align = TextAnchor.MiddleCenter ,Font="robotocondensed-regular.ttf", FontSize = 20},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = InitialLayer + ".Artefacts.Players" + i,
                        Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = $"ПУСТО", Align = TextAnchor.MiddleCenter ,Font="robotocondensed-regular.ttf", FontSize = 20},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                    });

                }
            }



            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.05 0.1", AnchorMax = "1 0.25" },
                Image = { Color = "0.24 0.45 0.90 0" }
            }, InitialLayer + ".Info", InitialLayer + ".Artefacts.Invectory");

            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.05 0.12", AnchorMax = "0.75 1" },
                Image = { Color = "0.24 0.45 0.90 0" }
            }, InitialLayer + ".Artefacts.Invectory", InitialLayer + ".Artefacts.ItemList");



            if (ArtefactsList.Count < config.artefactsSettings.MaxCount && !ArtefactsList.Contains(player.userID))
            {
                var invectoryPos = GetPositions(6, 1, 0.02f, 0.08f);
                int count = 0;

                List<ulong> IgnoreList = new List<ulong>();

                for (int i = 0; i < 6; i++)
                {
                    container.Add(new CuiPanel()
                    {
                        RectTransform = { AnchorMin = invectoryPos[i].AnchorMin, AnchorMax = invectoryPos[i].AnchorMax },
                        Image = { Color = "0.815 0.776 0.741 0.15" }
                    }, InitialLayer + ".Artefacts.ItemList", InitialLayer + ".Artefacts.ItemList" + i);

                    var item = config.artefactsSettings.ItemsAdded[i];

                    foreach (var invItem in player.inventory.AllItems())
                    {
                        if (invItem.info.shortname == item.ShortName && invItem.skin == item.SkinID && !IgnoreList.Contains(item.SkinID))
                        {
                            container.Add(new CuiElement
                            {
                                Parent = InitialLayer + ".Artefacts.ItemList" + count,
                                Components =
                            {
                                new CuiRawImageComponent {Png = (string) ImageLibrary?.Call("GetImage", item.ImageURL) },
                                new CuiRectTransformComponent {AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.95"}
                            }
                            });
                            IgnoreList.Add(item.SkinID);
                            count++;
                        }
                    }
                }


                if (count == 6)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0.41 0.47 0.26 1.00", Command = $"UI_Batia takeArt", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                        Text = { Text = "ОБМЕНЯТЬ", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "0.59 0.69 0.42 0.7" },
                        RectTransform = { AnchorMin = "0.78 0.14", AnchorMax = "0.9 0.97" }
                    }, InitialLayer + ".Artefacts.Invectory");


                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.78 0.14", AnchorMax = "0.9 0.97" },
                        Button = { Command = "", Color = "0.815 0.776 0.741 0.15" },
                        Text = { Text = "ОБМЕНЯТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 16, Color = "0.929 0.882 0.847 0.1" }
                    }, InitialLayer + ".Artefacts.Invectory");
                }
            }
            else if (ArtefactsList.Count >= config.artefactsSettings.MaxCount)
            {
                container.Add(new CuiElement
                {
                    Parent = InitialLayer + ".Artefacts.Invectory",
                    Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = $"ОБМЕН ЗАКОНЧЕН, ЕСТЬ ТРИ ПОБЕДИТЕЛЯ", Align = TextAnchor.MiddleCenter ,Font="robotocondensed-regular.ttf", FontSize = 25},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                });
            }
            else if (ArtefactsList.Contains(player.userID))
            {
                container.Add(new CuiElement
                {
                    Parent = InitialLayer + ".Artefacts.Invectory",
                    Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 1", Text = $"ВЫ ПОЛУЧИЛИ УЖЕ СВОЙ ПРИЗ В ЭТОМ ВАЙПЕ", Align = TextAnchor.MiddleCenter ,Font="robotocondensed-regular.ttf", FontSize = 25},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                });
            }
            CuiHelper.DestroyUi(player, InitialLayer + ".Info");
            CuiHelper.AddUi(player, container);
        }


        bool TakeItems(BasePlayer player)
        {
            List<ulong> IgnoreList = new List<ulong>();
            List<Item> currentList = new List<Item>();
            for (int i = 0; i < 6; i++)
            {
                var item = config.artefactsSettings.ItemsAdded[i];
                foreach (var invItem in player.inventory.AllItems())
                {
                    if (invItem.info.shortname == item.ShortName && invItem.skin == item.SkinID && !IgnoreList.Contains(item.SkinID))
                    {
                        currentList.Add(invItem);
                        IgnoreList.Add(item.SkinID);
                    }
                }
            }
            bool result = false;
            if (currentList.Count == 6)
            {
                result = true;
                foreach (var item in currentList)
                    item.UseItem(1);
            }
            return result;
        }

        void CreateBottleExchange(BasePlayer player, int page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            double amount = (double)Economics.Call("Balance", player.userID);

            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "-0.3 0", AnchorMax = "1.3 1" },
                Image = { Color = "0 0 0 0" }
            }, InitialLayer + ".R", InitialLayer + ".Bottle");

            var itemList = config.bottleSetting.CustomItemsShop.Skip(page * 18);
            if (itemList.Count() > 18)
            {
                container.Add(new CuiButton
                {
                    Button =
                {
                    Command = $"UI_Gold page {page+1} ",
                    Color = "0 0 0 0" ,
                },
                    Text =
                {
                    Text = $"▶", FontSize = 60, Align = TextAnchor.MiddleRight, Color = "0.929 0.882 0.847 0.7"
                },
                    RectTransform =
                {
                    AnchorMin = $"0.89 -0.15",
                    AnchorMax = $"0.99 0"
                }
                }, InitialLayer + ".Bottle");
            }

            int i = 0;
            var pos = GetPositions(3, 5, 0.01f, 0.02f, false);


            foreach (var item in itemList.Take(18))
            {
                container.Add(new CuiPanel()
                {
                    RectTransform = { AnchorMin = pos[i].AnchorMin, AnchorMax = pos[i].AnchorMax },
                    Image = { Color = "0.815 0.776 0.741 0.05", Sprite = "assets/content/ui/ui.background.transparent.radial.psd", Material = "" }
                }, InitialLayer + ".Bottle", InitialLayer + $".Shop.{i}");

                container.Add(new CuiPanel()
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.36 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                    Image = { Color = "0.815 0.776 0.741 0.3", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
                }, InitialLayer + $".Shop.{i}");


                var image = !string.IsNullOrEmpty(item.ImageURL) ? item.ImageURL : item.defaultItem.ShortName + 128;

                container.Add(new CuiElement
                {
                    Parent = InitialLayer + $".Shop.{i}",
                    Components =
                    {
                        new CuiRawImageComponent {Png            = (string) ImageLibrary?.Call("GetImage",image) },
                        new CuiRectTransformComponent {AnchorMin = "0 0.08", AnchorMax = "0.34 0.92", OffsetMin = "10 5", OffsetMax = "-10 -5"}
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.36 0.7", AnchorMax = "1 1" },
                    Text = { Text = item.Title, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 15, Color = "0.929 0.882 0.847 1" }
                }, InitialLayer + $".Shop.{i}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.36 0.3", AnchorMax = "1 0.7" },
                    Text = { Text = $"ПОКУПКА ЗА: {item.NeedGold} RP", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "0.929 0.882 0.847 0.7" }
                }, InitialLayer + $".Shop.{i}");

                var color = amount >= item.NeedGold ? "0.815 0.776 0.741 0.2" : "0.815 0.776 0.741 0.05";

                var Tcolor = amount >= item.NeedGold ? "0.929 0.882 0.847 1" : "0.929 0.882 0.847 0.1";

                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = $"UI_Batia buy {i}", Material = "" },
                    Text = { Text = "ОБМЕНЯТЬ", Color = Tcolor, Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = $"0.35 0.05", AnchorMax = $"0.99 0.25" },
                }, InitialLayer + $".Shop.{i}");
                i++;
            }

            CuiHelper.DestroyUi(player, InitialLayer + ".Bottle");

            CuiHelper.AddUi(player, container);
        }

        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int pos)
        {
            var newItem = container.parent as Item;
            if (newItem == null) return null;

            if (newItem.info.shortname == "wrappedgift")
                return ItemContainer.CanAcceptResult.CannotAccept;
            return null;
        }

        public void DrawMenuPoints(BasePlayer player, MenuPoints choosed = null)
        {
            if (choosed == null)
                choosed = config.menuUISettings.PointsList.ElementAt(0);
            CuiHelper.DestroyUi(player, InitialLayer + ".Bottle");
            CuiHelper.DestroyUi(player, InitialLayer + ".Info");
            CuiHelper.DestroyUi(player, InitialLayer + ".Expedition");

            CuiElementContainer container = new CuiElementContainer();

            float marginTop = -220;
            float originalHeight = 40;
            float freeHeight = 20;
            float padding = 5;

            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0" }
            }, InitialLayer + ".R", InitialLayer + ".MenuInfo");

            container.Add(new CuiElement
            {
                Parent = InitialLayer + ".MenuInfo",
                Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 0.7", Text = choosed.Title.ToUpper(), Align = TextAnchor.LowerCenter , FontSize = 20},
                            new CuiRectTransformComponent {AnchorMin = "0 1", AnchorMax = "1 1.2"}
                        }
            });


            foreach (var point in config.menuUISettings.PointsList)
            {
                CuiHelper.DestroyUi(player, InitialLayer + config.menuUISettings.PointsList.IndexOf(point));

                string color = point == choosed ? "0.929 0.882 0.847 1" : "0.929 0.882 0.847 0.2";
                float elementHeight = point.DisplayName.Length > 0 ? originalHeight : freeHeight;

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"50 {marginTop - elementHeight}", OffsetMax = $"250 {marginTop}" },
                    Button = { Command = point == choosed ? "" : $"UI_Batia menu {config.menuUISettings.PointsList.IndexOf(point)}", Color = "0 0 0 0" },
                    Text = { Text = point.DisplayName, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = color }
                }, "SubMenu_UI", InitialLayer + config.menuUISettings.PointsList.IndexOf(point));

                container.Add(new CuiElement
                {
                    Parent = InitialLayer + config.menuUISettings.PointsList.IndexOf(point),
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage", "white"),
                        
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        },
                    }
                });

                marginTop -= elementHeight + padding;
            }
            CuiHelper.DestroyUi(player, InitialLayer + ".MenuInfo");

            CuiHelper.AddUi(player, container);
            player.SendConsoleCommand(choosed.DrawMethod);
        }

        [ConsoleCommand("UI_Batia")]
        void cmdMenuBatia(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;

            if (!args.HasArgs(1)) return;
            switch (args.Args[0])
            {
                case "menu":
                    int chooseIndex = -1;
                    if (!int.TryParse(args.Args[1], out chooseIndex)) return;

                    var chooseElement = config.menuUISettings.PointsList.ElementAtOrDefault(chooseIndex);
                    if (chooseElement == null) return;
                    DrawMenuPoints(player, chooseElement);
                    break;

                case "bottle":
                    CreateBottleExchange(player);
                    break;
                case "info":
                    CreateInfo(player);
                    break;
                case "artefacts":
                    ShowTOPArtefacts(player);
                    break;

                case "takeArt":
                    if (ArtefactsList.Count >= config.artefactsSettings.MaxCount) return;
                    if (ArtefactsList.Contains(player.userID)) return;
                    if (string.IsNullOrEmpty(config.gameStoresSettings.Secret) || string.IsNullOrEmpty(config.gameStoresSettings.ShopID))
                    {
                        SendReply(player, $"<color=#EDE1D8>Ошибка пополнения, обратитесь к администратору</color>");
                        return;
                    }
                    if (TakeItems(player))
                    {
                        APIChangeUserBalance(player.userID, config.artefactsSettings.Summ, null);
                        SendReply(player, $"<size=12><color=#EDE1D8>Вы успешно обменяли Нюка Колу на {config.artefactsSettings.Summ} рублей баланса в премиум магазине.</color></size>");
                        ArtefactsList.Add(player.userID);
                        ShowTOPArtefacts(player);
                    }
                    else
                        SendReply(player, $"<size=12><color=#EDE1D8>У вас не хватает Нюка Колы для обмена!</color></size>");
                    break;

                case "givepackage":

                    if (!com.EndExpeditions.ContainsKey(player.userID)) 
                    {
                        Puts("123");
                        return;
                    }
                    var component = com.EndExpeditions[player.userID];


                    if (component < 20) return;

                    if (component >= 20 && component < 40)
                        SendPackage(player, 20);

                    if (component >= 40 && component < 60)
                        SendPackage(player, 40);

                    if (component >= 60 && component < 80)
                        SendPackage(player, 60);

                    if (component >= 80 && component < 90)
                        SendPackage(player, 80);

                    if (component >= 90)
                        SendPackage(player, 100);


                    break;

                case "expedition":
                    int page;
                    if (!int.TryParse(args.Args[1], out page)) page = 0;
                    CreateInfoJson(player, page);
                    break;

                case "startLoot":
                    CuiHelper.DestroyUi(player, "Main_UI");
                    ExpeditionExceptionBox box = ExpeditionExceptionBox.Spawn(player);
                    box.StartLoot();
                    break;
                case "sendLoot":
                    player.Command("chat.say /tech");
                    var entityLoot = player.inventory.loot;
                    if (entityLoot == null || entityLoot.entitySource.GetComponent<ExpeditionExceptionBox>() == null) return;
                    if (entityLoot.entitySource.GetComponent<ExpeditionExceptionBox>().PoitsInvectoryCount() < 20) return;
                    entityLoot.entitySource.GetComponent<ExpeditionExceptionBox>().Close();
                    break;

                case "buy":
                    chooseIndex = -1;
                    if (!int.TryParse(args.Args[1], out chooseIndex)) return;
                    var buyItem = config.bottleSetting.CustomItemsShop.ElementAtOrDefault(chooseIndex);
                    if (buyItem == null) return;
                    if ((double)Economics.Call("Balance", player.userID) < buyItem.NeedGold) return;
                    Economics.Call("SetBalance", player.UserIDString,
                        (double) Economics.Call("Balance", player.UserIDString) - buyItem.NeedGold);
                    BMenu.Call("UpdateBalance", player);
                    if (buyItem.Command.Count > 0)
                    {
                        foreach (var command in buyItem.Command)
                        {
                            Server.Command(command.Replace("%STEAMID%", player.UserIDString));
                        }

                        SendReply(player, $"<size=12>Вы приобрели <color=#B1D6F1>{buyItem.Title}</color> за {buyItem.NeedGold} RP.</size>");
                    }
                    if (!string.IsNullOrEmpty(buyItem.defaultItem.ShortName))
                    {
                        var giveItem = ItemManager.CreateByName(buyItem.defaultItem.ShortName, buyItem.defaultItem.MinAmount, buyItem.defaultItem.SkinID);
                        player.GiveItem(giveItem, BaseEntity.GiveItemReason.Generic);
                        SendReply(player, $"<size=12>Вы приобрели <color=#B1D6F1>{buyItem.Title}</color> за {buyItem.NeedGold} RP.</size>");
                    }
                    DrawMenuPoints(player, config.menuUISettings.PointsList[0]);
                    break;

                case "close":
                    if (com != null && com.OpenInterface.Contains(player))
                        com.OpenInterface.Remove(player);
                    CuiHelper.DestroyUi(player, External);
                    CuiHelper.DestroyUi(player, "UI_Batia_External" + ".Mood1" + ".panel");
                    break;
            }
        }

        void APIChangeUserBalance(ulong steam, int balanceChange, Action<int> callback)
        {
            AddMoney(steam, (float)balanceChange, "Batia API", new Action<int>((result) =>
            {
                if (result > 0)
                {
                    LogToFile("logWEB", $"({DateTime.Now.ToShortTimeString()}): Отправлен запрос пользователем {steam} на пополнение баланса в размере: {balanceChange}", this);
                    callback(result);
                    return;
                }
                LogToFile("logError", $"({DateTime.Now.ToShortTimeString()}): Баланс ИГРОКА {steam} не был изменен, ошибка: {result}", this);

                callback(0);
            }));
        }

        public static Batia ins;

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("!");
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
            if (config.PluginVersion < new VersionNumber(1, 0, 1))
            {
                PrintWarning("Config update detected! Updating config values...");
                config.artefactsSettings.ItemsAdded = new List<ArtefactsItems>()
                {
                   new ArtefactsItems() { ShortName = "blood", SkinID = 2248214689, ImageURL = "https://i.imgur.com/sBCgt6B.png"},
                   new ArtefactsItems() { ShortName = "blood", SkinID = 2248217143, ImageURL = "https://i.imgur.com/zrqGMpn.png" },
                   new ArtefactsItems() { ShortName = "blood", SkinID = 2248119385, ImageURL = "https://i.imgur.com/aqM4efr.png" },
                   new ArtefactsItems() { ShortName = "blood", SkinID = 2248218154, ImageURL = "https://i.imgur.com/Fa1AFUe.png" },
                   new ArtefactsItems() { ShortName = "blood", SkinID = 2248215784, ImageURL = "https://i.imgur.com/9s2QJLT.png" },
                   new ArtefactsItems() { ShortName = "blood", SkinID = 2248216305, ImageURL = "https://i.imgur.com/46sJWQU.png" }
                };

                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }


        public class MenuUISettings
        {
            [JsonProperty("Пункты меню")]
            public List<MenuPoints> PointsList = new List<MenuPoints>();

            [JsonProperty("Основная информация")]
            public string MainInformation = "Основная информация о бате:";

            [JsonProperty("Дополнительная информация")]
            public string AdditionalInformation = "Дополнительная информация";
        }

        public class MenuPoints
        {
            [JsonProperty("Название пункта меню в UI")]
            public string DisplayName;

            [JsonProperty("Выполняемая команда")]
            public string DrawMethod;

            [JsonProperty("Титл страницы")]
            public string Title;

        }


        public class GameStoresSettings
        {
            [JsonProperty("Gamestores ShopID")]
            public string ShopID = "";

            [JsonProperty("Gamestores SecretKey")]
            public string Secret = "";

        }

        private void AddMoney(ulong userId, float amount, string mess, Action<int> callback)
        {
            ExecuteApiRequest(new Dictionary<string, string>()
            {
                {"action", "moneys"},
                {"type", "plus"},
                {"steam_id", userId.ToString()},
                {"amount", amount.ToString()},
                {"mess", mess}
            }, callback);
        }

        private void ExecuteApiRequest(Dictionary<string, string> args, Action<int> callback)
        {
            if (string.IsNullOrEmpty(config.gameStoresSettings.Secret) || string.IsNullOrEmpty(config.gameStoresSettings.ShopID))
            {
                callback(0);
                return;
            }
            string url = $"https://gamestores.ru/api?shop_id={config.gameStoresSettings.ShopID}&secret={config.gameStoresSettings.Secret}" +
                    $"{string.Join("", args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
            webrequest.EnqueueGet(url, (i, s) =>
            {
                if (i != 200)
                {
                    PrintError($"Ошибка зачисления, подробнисти в ЛОГ-Файле");
                    LogToFile("DailyBonus", $"Код ошибки: {i}, подробности:\n{s}", this);
                    callback(-1);
                }
                else
                {
                    if (s.Contains("fail"))
                    {
                        callback(-1);
                        return;
                    }
                    callback((int)((JObject)JsonConvert.DeserializeObject(s))["newBalance"]);
                }
            }, this);
        }


        class PluginConfig
        {
            [JsonProperty("Настройка меню")]
            public MenuUISettings menuUISettings;

            [JsonProperty("Настройка геймстор")]
            public GameStoresSettings gameStoresSettings;

            [JsonProperty("Настройка бутылок")]
            public BottleSetting bottleSetting;

            [JsonProperty("Настройка посылок")]
            public Dictionary<int, PackageListSettings> packageSetting;

            [JsonProperty("Настройка экспедиции")]
            public ExpeditionSettings expeditionSettings;


            [JsonProperty("Настройка обмена артефактов")]
            public ArtefactsSettings artefactsSettings;

            [JsonProperty("Configuration Version")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    bottleSetting = new BottleSetting(),
                    expeditionSettings = new ExpeditionSettings(),
                    packageSetting = new Dictionary<int, PackageListSettings>(),
                    PluginVersion = new VersionNumber(),
                    menuUISettings = new MenuUISettings()
                    {
                        PointsList = new List<MenuPoints>()
                        {
                            new MenuPoints()
                            {
                                DisplayName = "ИНФОРМАЦИЯ",
                                Title = "Основная информация"
                            },
                            new MenuPoints()
                            {
                                DisplayName = "ПОДГОН ОТ БАТИ",
                                Title = "ПОДГОН ОТ БАТИ"
                            },
                            new MenuPoints()
                            {
                                DisplayName = "СДАТЬ БУТЫЛКИ",
                                Title = "ОБМЕН БУТЫЛОК"
                            },

                        }
                    }

                };
            }
        }


        public class CustomShopItems
        {
            [JsonProperty("Название предмета в UI")]
            public string Title;

            [JsonProperty("Выполняемая команда (Если это предмет оставь ПУСТЫМ! %STEAMID% - индификатор игрока)")]
            public List<string> Command = new List<string>();

            [JsonProperty("Кастомное изображение предмета (Если у игровой предмет можно не указывать)")]
            public string ImageURL;

            [JsonProperty("Нужное количество золота на покупку данного предмета")]
            public int NeedGold;

            [JsonProperty("Настройка предмета (Если не привилегия трогать не нужно)")]
            public DefaultItem defaultItem;
        }

        public class DefaultItem
        {
            [JsonProperty("Shortname предмета")]
            public string ShortName;
            [JsonProperty("Количество")]
            public int MinAmount;
            [JsonProperty("SkinID предмета")]
            public ulong SkinID;
            [JsonProperty("Имя предмета при создании (Оставте поле постым чтобы использовать стандартное название итема)")]
            public string Name;
            [JsonProperty("Это чертеж")]
            public bool IsBlueprnt;
        }

        public class ItemSettings
        {
            [JsonProperty("ShortName предмета")] public string ShortName;
            [JsonProperty("Количество предмета")] public int Amount;
            [JsonProperty("SkinID предмета")] public ulong SkinID;
            [JsonProperty("Шанс на получение")] public int Chance;
            [JsonProperty("Изображение предмета")] public string image;
            [JsonProperty("Имя предмета(оставить пустым если стандартное)")]
            public string DisplayName;
        }


        internal class ItemsAddSetting
        {
            [JsonProperty("Количество очков за предмет")]
            public float amount;

            [JsonProperty("Максимальное количество предмета")]
            public int maxCount;
        }

        internal class ArtefactsItems
        {
            [JsonProperty("ShortName предмета")] public string ShortName;
            [JsonProperty("SkinID предмета")] public ulong SkinID;
            [JsonProperty("Ссылка на изображение")] public string ImageURL;
        }


        public class BottleSetting
        {
            [JsonProperty("Список предметов на обмен")]
            public List<CustomShopItems> CustomItemsShop = new List<CustomShopItems>();
        }

        public class PackageListSettings
        {
            [JsonProperty("Имя предмета при создании")]
            public string DisplayName = "Батина посылка [Средняя]";
            [JsonProperty("ShortName основного предмета")]
            public string Shortname = "wrappedgift";
            [JsonProperty("SkinID предмета")]
            public ulong SkinID = 2274179009;
        }


        public class ExpeditionSettings
        {
            [JsonProperty("Время через какое игроку будет доставлены предметы с экспедиции (Min)")]
            public int StartTimeMin = 1400;
            [JsonProperty("Время через какое игроку будет доставлены предметы с экспедиции (Max)")]
            public int StartTimeMax = 1800;

            [JsonProperty("Список предметов, которые игрок может положить")]
            public Dictionary<string, ItemsAddSetting> ItemsAdded;
            [JsonProperty("Список предметов, которые может получить игрок")]
            public List<ItemSettings> ItemList;
        }

        public class ArtefactsSettings
        {
            [JsonProperty("Максимальное количество победителей")]
            public int MaxCount = 3;

            [JsonProperty("Сумма пополнения на баланс магазина")]
            public int Summ = 300;
            [JsonProperty("Список нужных артефактов")]
            public List<ArtefactsItems> ItemsAdded = new List<ArtefactsItems>();

        }

        #region Batia
        private List<ItemSettings> Find(int x)
        {
            var num = config.expeditionSettings.ItemList.Where(p => p.Chance >= x - 20 && p.Chance < x).ToList();
            return num;
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || player == null || action != "open" || item.skin == 0) return null;
            var configItem = config.packageSetting.FirstOrDefault(p => p.Value.SkinID == item.skin);
            if (configItem.Value != null)
            {
                var count = UnityEngine.Random.Range(4, 6);
                var list = Find(configItem.Key);
                System.Random random = new System.Random();
                for (int i = list.Count - 1; i >= 1; i--)
                {
                    int j = random.Next(i + 1);
                    var temp = list[j];
                    list[j] = list[i];
                    list[i] = temp;
                }
                item.UseItem(1);
                foreach (var items in list.Take(count))
                {
                    var createItem = ItemManager.CreateByName(items.ShortName, items.Amount, items.SkinID);
                    GiveItem(player, createItem, BaseEntity.GiveItemReason.Generic);
                }
                return false;
            }
            return null;
        }


        private void GiveItem(BasePlayer player, Item item, BaseEntity.GiveItemReason reason = 0)
        {
            if (reason == BaseEntity.GiveItemReason.ResourceHarvested)
                player.stats.Add(string.Format("harvest.{0}", item.info.shortname), item.amount, Stats.Server | Stats.Life);

            int num = item.amount;
            if (!GiveItem(player.inventory, item, null))
            {
                item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());
                return;
            }

            if (string.IsNullOrEmpty(item.name))
            {
                player.Command("note.inv", new object[] { item.info.itemid, num, string.Empty, (int)reason });
                return;
            }

            player.Command("note.inv", new object[] { item.info.itemid, num, item.name, (int)reason });
        }

        private bool GiveItem(PlayerInventory inv, Item item, ItemContainer container = null)
        {
            if (item == null)
                return false;

            int num = -1;
            GetIdealPickupContainer(inv, item, ref container, ref num);
            if (container != null && MoveToContainer(item, container, num, true))
                return true;

            if (MoveToContainer(item, inv.containerMain, -1, true))
                return true;

            if (MoveToContainer(item, inv.containerBelt, -1, true))
                return true;

            return false;
        }
        public static bool CanMoveTo(Item item, ItemContainer newcontainer, int iTargetPos = -1, bool allowStack = true)
        {
            return !item.IsChildContainer(newcontainer) && newcontainer.CanAcceptItem(item, iTargetPos) == global::ItemContainer.CanAcceptResult.CanAccept && iTargetPos < newcontainer.capacity && (item.parent == null || newcontainer != item.parent || iTargetPos != item.position);
        }
        private static bool MoveToContainer(Item itemBase, ItemContainer newcontainer, int iTargetPos = -1, bool allowStack = true)
        {
            bool container;
            Quaternion quaternion;
            using (TimeWarning timeWarning = TimeWarning.New("MoveToContainer", 0))
            {
                var itemContainer = itemBase.parent;
                if (!CanMoveTo(itemBase, newcontainer, iTargetPos, allowStack))
                    container = false;
                else
                    if (iTargetPos >= 0 && newcontainer.SlotTaken(itemBase, iTargetPos))
                {
                    Item slot = newcontainer.GetSlot(iTargetPos);

                    if (allowStack)
                    {
                        int num = slot.MaxStackable();
                        if (slot.CanStack(itemBase))
                        {
                            if (slot.amount < num)
                            {
                                slot.amount += itemBase.amount;
                                slot.MarkDirty();
                                itemBase.RemoveFromWorld();
                                itemBase.RemoveFromContainer();
                                itemBase.Remove(0f);
                                int num1 = slot.amount - num;
                                if (num1 > 0)
                                {
                                    Item item = slot.SplitItem(num1);
                                    if (item != null && !MoveToContainer(item, newcontainer, -1, false) && (itemContainer == null || !MoveToContainer(item, itemContainer, -1, true)))
                                    {
                                        Vector3 vector3 = newcontainer.dropPosition;
                                        Vector3 vector31 = newcontainer.dropVelocity;
                                        quaternion = new Quaternion();
                                        item.Drop(vector3, vector31, quaternion);
                                    }
                                    slot.amount = num;
                                }
                                container = true;
                                return container;
                            }
                            else
                            {
                                container = false;
                                return container;
                            }
                        }
                    }

                    if (itemBase.parent == null)
                        container = false;
                    else
                    {
                        ItemContainer itemContainer1 = itemBase.parent;
                        int num2 = itemBase.position;
                        if (CanMoveTo(slot, itemContainer1, num2, true))
                        {
                            itemBase.RemoveFromContainer();
                            slot.RemoveFromContainer();
                            MoveToContainer(slot, itemContainer1, num2, true);
                            container = MoveToContainer(itemBase, newcontainer, iTargetPos, true);
                        }
                        else
                            container = false;
                    }
                }
                else
                        if (itemBase.parent != newcontainer)
                {
                    if (iTargetPos == -1 & allowStack && itemBase.info.stackable > 1)
                    {
                        var item1 = newcontainer.itemList.Where(x => x != null && x.info.itemid == itemBase.info.itemid && x.skin == itemBase.skin).OrderBy(x => x.amount).FirstOrDefault();
                        if (item1 != null && item1.CanStack(itemBase))
                        {
                            int num3 = item1.MaxStackable();
                            if (item1.amount < num3)
                            {
                                var total = item1.amount + itemBase.amount;
                                if (total <= num3)
                                {
                                    item1.amount += itemBase.amount;
                                    item1.MarkDirty();
                                    itemBase.RemoveFromWorld();
                                    itemBase.RemoveFromContainer();
                                    itemBase.Remove(0f);
                                    container = true;
                                    return container;
                                }
                                else
                                {
                                    item1.amount = item1.MaxStackable();
                                    item1.MarkDirty();
                                    itemBase.amount = total - item1.MaxStackable();
                                    itemBase.MarkDirty();
                                    container = MoveToContainer(itemBase, newcontainer, iTargetPos, allowStack);
                                    return container;
                                }
                            }
                        }
                    }

                    if (newcontainer.maxStackSize > 0 && newcontainer.maxStackSize < itemBase.amount)
                    {
                        Item item2 = itemBase.SplitItem(newcontainer.maxStackSize);
                        if (item2 != null && !MoveToContainer(item2, newcontainer, iTargetPos, false) && (itemContainer == null || !MoveToContainer(item2, itemContainer, -1, true)))
                        {
                            Vector3 vector32 = newcontainer.dropPosition;
                            Vector3 vector33 = newcontainer.dropVelocity;
                            quaternion = new Quaternion();
                            item2.Drop(vector32, vector33, quaternion);
                        }
                        container = true;
                    }
                    else
                        if (newcontainer.CanAccept(itemBase))
                    {
                        itemBase.RemoveFromContainer();
                        itemBase.RemoveFromWorld();
                        itemBase.position = iTargetPos;
                        itemBase.SetParent(newcontainer);
                        container = true;
                    }
                    else
                        container = false;
                }
                else
                            if (iTargetPos < 0 || iTargetPos == itemBase.position || itemBase.parent.SlotTaken(itemBase, iTargetPos))
                    container = false;
                else
                {
                    itemBase.position = iTargetPos;
                    itemBase.MarkDirty();
                    container = true;
                }
            }

            return container;
        }

        private void GetIdealPickupContainer(PlayerInventory inv, Item item, ref ItemContainer container, ref int position)
        {
            if (item.info.stackable > 1)
            {
                if (inv.containerBelt != null && inv.containerBelt.FindItemByItemID(item.info.itemid) != null)
                {
                    container = inv.containerBelt;
                    return;
                }

                if (inv.containerMain != null && inv.containerMain.FindItemByItemID(item.info.itemid) != null)
                {
                    container = inv.containerMain;
                    return;
                }
            }

            if (!item.info.isUsable || item.info.HasFlag(ItemDefinition.Flag.NotStraightToBelt))
                return;

            container = inv.containerBelt;
        }

        [ChatCommand("create")]
        void cmdCreateItem(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            switch (args[0])
            {
                case "posilka":

                    var item = ItemManager.CreateByName(config.packageSetting[int.Parse(args[1])].Shortname, 10, config.packageSetting[int.Parse(args[1])].SkinID);
                    item.name = config.packageSetting[int.Parse(args[1])].DisplayName;

                    player.GiveItem(item);
                    break;
            }
        }

        public BasePlayer newPlayer = null;
        FatherComponent com = null;

        [PluginReference] Plugin RustStore, Inventory, Economics, BMenu;

        void Unload()
        {
            if (newPlayer != null)
            {
                if (newPlayer.GetComponent<FatherComponent>() != null)
                {
                    newPlayer.GetComponent<FatherComponent>().SaveData();
                    UnityEngine.Component.Destroy(newPlayer.GetComponent<FatherComponent>());
                }
                newPlayer.AdminKill();
            }

            var objects = UnityEngine.Object.FindObjectsOfType<ExpeditionExceptionBox>();
            if (objects != null)
                foreach (var component in objects)
                    UnityEngine.Object.Destroy(component);

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, External);
                CuiHelper.DestroyUi(player, "ExpeditionExceptionBox_UI");
                CuiHelper.DestroyUi(player, External + ".Mood1" + ".panel");

            }
        }

        string GetImageInMood()
        {
            switch (com.mood)
            {
                case FatherComponent.Mood.Neutral:
                    return "https://gspics.org/images/2022/06/04/0nWsOm.png";
                case FatherComponent.Mood.Kind:
                    return "https://gspics.org/images/2022/06/04/0nWcGe.png";
                case FatherComponent.Mood.Evil:
                    return "https://gspics.org/images/2022/06/04/0nWJCJ.png";
            }
            return "https://gspics.org/images/2022/01/14/0QKR0I.png";
        }


        bool SendPackage(BasePlayer player, int type)
        {
            if (player == null || !player.IsConnected) return false;
            CuiHelper.DestroyUi(player, External + ".Mood1" + ".panel");

            var pConfig = config.packageSetting.FirstOrDefault().Value;
            var Package = ItemManager.CreateByName(pConfig.Shortname, 1, pConfig.SkinID);
            SendReply(player, "<size=12>Посылка от <color=#EBD4AE>Волт-Тек</color> готова. Загляните в убежище!</size>");
            string sound = "assets/bundled/prefabs/fx/notice/stack.world.fx.prefab";
            BroadcastSystem?.Call("SendCustonNotification", player,"ВОЛТ-ТЕК\n","Посылка от Волт-тек готова. Загляните в убежище!",sound,5f);

            switch (type)
            {
                case 20:
                    pConfig = config.packageSetting[20];
                    Package = ItemManager.CreateByName(pConfig.Shortname, 1, pConfig.SkinID);
                    Package.name = pConfig.DisplayName;
                    Inventory.Call("AddToInv", player, Package.info.shortname, false, false, 1, "https://gspics.org/images/2022/01/18/0QEduu.png", Package.name, Package.skin, "ПОСЫЛКИ");
                    if (com != null)
                        com.EndExpeditions.Remove(player.userID);
                    return true;
                case 40:
                    pConfig = config.packageSetting[40];
                    Package = ItemManager.CreateByName(pConfig.Shortname, 1, pConfig.SkinID);
                    Package.name = pConfig.DisplayName;
                    Inventory.Call("AddToInv", player, Package.info.shortname, false, false, 1, "https://gspics.org/images/2022/01/18/0QEduu.png", Package.name, Package.skin, "ПОСЫЛКИ");
                    if (com != null)
                        com.EndExpeditions.Remove(player.userID);
                    return true;
                case 60:
                    pConfig = config.packageSetting[60];
                    Package = ItemManager.CreateByName(pConfig.Shortname, 1, pConfig.SkinID);
                    Package.name = pConfig.DisplayName;
                    Inventory.Call("AddToInv", player, Package.info.shortname, false, false, 1, "https://gspics.org/images/2022/01/18/0QEduu.png", Package.name, Package.skin, "ПОСЫЛКИ");
                    if (com != null)
                        com.EndExpeditions.Remove(player.userID);
                    return true;
                case 80:
                    pConfig = config.packageSetting[80];
                    Package = ItemManager.CreateByName(pConfig.Shortname, 1, pConfig.SkinID);
                    Package.name = pConfig.DisplayName;
                    Inventory.Call("AddToInv", player, Package.info.shortname, false, false, 1, "https://gspics.org/images/2022/01/18/0QEo9o.png", Package.name, Package.skin, "ПОСЫЛКИ");
                    if (com != null)
                        com.EndExpeditions.Remove(player.userID);
                    return true;
                case 100:
                    pConfig = config.packageSetting[100];
                    Package = ItemManager.CreateByName(pConfig.Shortname, 1, pConfig.SkinID);
                    Package.name = pConfig.DisplayName;
                    Inventory.Call("AddToInv", player, Package.info.shortname, false, false, 1, "https://gspics.org/images/2022/01/18/0QEo9o.png", Package.name, Package.skin, "ПОСЫЛКИ");
                    if (com != null)
                        com.EndExpeditions.Remove(player.userID);
                    return true;
            }
            return false;
        }


        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }

            if (com == null)
                com = player.gameObject.AddComponent<FatherComponent>();

            CuiHelper.DestroyUi(player, External + ".Mood1" + ".panel");
/* 
            if (com != null && com.EndExpeditions.ContainsKey(player.userID))
                CreateInfoPlayer(player, false); */
        }
        public List<ulong> ArtefactsList = new List<ulong>();


        class FatherComponent : FacepunchBehaviour
        {
            public BasePlayer player;
            public BasePlayer target = null;
            public Dictionary<ulong, ExpeditionPlayer> CurrentExpeditions = new Dictionary<ulong, ExpeditionPlayer>();
            public Dictionary<ulong, float> EndExpeditions = new Dictionary<ulong, float>();


            public List<BasePlayer> OpenInterface = new List<BasePlayer>();


            public void SaveData()
            {
                Interface.Oxide.DataFileSystem.WriteObject("Batia/CurrentExpeditions", CurrentExpeditions);
                Interface.Oxide.DataFileSystem.WriteObject("Batia/EndExpeditions", EndExpeditions);
                Interface.Oxide.DataFileSystem.WriteObject("Batia/Artefacts", ins.ArtefactsList);
            }

            public void LoadData()
            {
                try
                {
                    CurrentExpeditions = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ExpeditionPlayer>>("Batia/CurrentExpeditions");
                    EndExpeditions = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("Batia/EndExpeditions");
                    ins.ArtefactsList = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("Batia/Artefacts");

                }
                catch
                {
                    CurrentExpeditions = new Dictionary<ulong, ExpeditionPlayer>();
                    EndExpeditions = new Dictionary<ulong, float>();
                    ins.ArtefactsList = new List<ulong>();
                }
            }

            public Mood mood;

            public class ExpeditionPlayer
            {
                public double EndTime;
                public float Points;
                public bool Ending;
            }

            public void StartExpedition(BasePlayer player, float points)
             => CurrentExpeditions.Add(player.userID, new ExpeditionPlayer() { EndTime = UnityEngine.Random.Range(ins.config.expeditionSettings.StartTimeMin, ins.config.expeditionSettings.StartTimeMax), Points = points });


            void SendMessages(ulong Player)
            {
                var bsPlayer = BasePlayer.FindByID(Player);
                if (bsPlayer == null || !bsPlayer.IsConnected) return;
            }


            void ExpeditionHandler()
            {
                foreach (var player in CurrentExpeditions)
                {
                    player.Value.EndTime--;
                    if (player.Value.EndTime <= 0)
                    {
                        EndExpeditions.Add(player.Key, player.Value.Points);
                        ins.NextTick(() => CurrentExpeditions.Remove(player.Key));

                        if (BasePlayer.FindByID(player.Key) != null)
                            ins.CreateInfoPlayer(BasePlayer.FindByID(player.Key), OpenInterface.Contains(BasePlayer.FindByID(player.Key)));
                        continue;
                    }

                }

                foreach (var bsPLayer in OpenInterface)
                {
                    CuiHelper.DestroyUi(player, "UI_Batia_External" + ".Mood1" + ".panel");
                    if (CurrentExpeditions.ContainsKey(bsPLayer.userID))
                    {

                        if (CurrentExpeditions[bsPLayer.userID].EndTime <= 0)
                        {
                            ins.CreateInfoPlayer(player, true);
                        }
                        else
                            CreateInfoPlayer(bsPLayer, CurrentExpeditions[bsPLayer.userID].EndTime);


                    }


                }
            }

            void CreateInfoPlayer(BasePlayer player, double time)
            {
                CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiElement
                {
                    Name = "UI_Batia_External" + ".Mood1" + ".panel",
                    Parent = "Overlay",
                    Components =
                        {
                            new CuiImageComponent { Color =  "0.235 0.227 0.180 0.6" , Sprite = "assets/content/ui/ui.background.transparent.linear.psd"},
                            new CuiRectTransformComponent {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-240 -81", OffsetMax = "-60 -30"}
                        }
                });



                container.Add(new CuiElement
                {
                    Parent = "UI_Batia_External" + ".Mood1" + ".panel",
                    Name = "UI_Batia_External" + ".Mood1",
                    Components =
                        {
                            new CuiRawImageComponent {Png = (string)ins.ImageLibrary?.Call("GetImage", "https://i.imgur.com/ghyCq0Q.png"), Color = "0.929 0.882 0.847 1"},
                            new CuiRectTransformComponent {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-30 -60", OffsetMax = "50 20"}
                        }
                });



                container.Add(new CuiElement
                {
                    Parent = "UI_Batia_External" + ".Mood1" + ".panel",
                    Components =
                        {
                            new CuiTextComponent { Color = "0.929 0.882 0.847 0.5", Text = $"<color=#EDE1D8>ОЖИДАНИЕ ПОСЫЛКИ</color>", Align = TextAnchor.UpperLeft /*,Font="robotocondensed-regular.ttf"*/},
                            new CuiRectTransformComponent {AnchorMin = "0.03 0", AnchorMax = "1 0.9"}
                        }
                });

                container.Add(new CuiElement
                {
                    Parent = "UI_Batia_External" + ".Mood1" + ".panel",
                    Components =
                        {
                            new CuiTextComponent {Color = "0.929 0.882 0.847 0.4", Text = $"ОСТАЛОСЬ ВРЕМЕНИ: {FormatShortTime(TimeSpan.FromSeconds(time))}", Align = TextAnchor.MiddleLeft ,Font="robotocondensed-regular.ttf", FontSize = 12},
                            new CuiRectTransformComponent {AnchorMin = "0.03 0", AnchorMax = "1 0.8"}
                        }
                });


                CuiHelper.DestroyUi(player, "UI_Batia_External" + ".Mood1" + ".panel");

                CuiHelper.AddUi(player, container);
            }


            public static string FormatShortTime(TimeSpan time)
            {
                if (time.Hours != 0)
                    return time.ToString(@"hh\:mm\:ss");
                if (time.Minutes != 0)
                    return time.ToString(@"mm\:ss");
                if (time.Seconds != 0)
                    return time.ToString(@"mm\:ss");
                return time.ToString(@"hh\:mm\:ss");
            }



            public enum Mood
            {
                Neutral,
                Kind,
                Evil
            }

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                LoadData();
                ChangeInMood();
                InvokeRepeating("ChangeInMood", 7200, 7200);
                InvokeRepeating("ExpeditionHandler", 1, 1);
            }

            void ChangeInMood()
            {
                var random = UnityEngine.Random.Range(-1, 3);
                switch (random)
                {
                    case 0:
                        mood = Mood.Neutral;
                        break;
                    case 1:
                        mood = Mood.Kind;
                        break;
                    case 2:
                        mood = Mood.Evil;
                        break;
                }
            }

            void Puts(string messages) => ins.Puts(messages);
            public void Destroy() => Destroy(this);
        }
        #endregion

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
                return $"----------\nAmin:{AnchorMin}\nAmax:{AnchorMax}\n----------";
            }
        }

        private static List<Position> GetPositions(float colums, int rows, float colPadding = 0, float rowPadding = 0, bool columsFirst = false)
        {
            if (colums == 0)
                throw new ArgumentException("Can't create positions for gui!", nameof(colums));
            if (rows == 0)
                throw new ArgumentException("Can't create positions for gui!", nameof(rows));

            List<Position> result = new List<Position>();
            result.Clear();
            var colsDiv = 1f / colums;
            var rowsDiv = 1f / rows;
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


        #region Expedition

        private static string _(string i)
        {
            return !string.IsNullOrEmpty(i)
                ? new string(i.Select(x =>
                    x >= 'a' && x <= 'z' ? (char)((x - 'a' + 13) % 26 + 'a') :
                    x >= 'A' && x <= 'Z' ? (char)((x - 'A' + 13) % 26 + 'A') : x).ToArray())
                : i;
        }


        public class ExpeditionExceptionBox : MonoBehaviour
        {
            public StorageContainer storage;
            public BasePlayer player;
            public string UIPanel = "ExpeditionExceptionBox_UI";

            public void Init(StorageContainer storage, BasePlayer owner)
            {
                this.storage = storage;
                this.player = owner;
            }
            public static ExpeditionExceptionBox Spawn(BasePlayer player)
            {
                player.EndLooting();
                var storage = SpawnContainer(player);
                var box = storage.gameObject.AddComponent<ExpeditionExceptionBox>();
                box.Init(storage, player);
                return box;
            }

            public static StorageContainer SpawnContainer(BasePlayer player)
            {
                var storage = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", player.transform.position - new Vector3(0, 250f + UnityEngine.Random.Range(-25f, 25f), 0)) as StorageContainer;
                if (storage == null) return null;
                if (!storage) return null;
                storage.panelName = "mailboxcontents";
                storage.SendMessage("SetDeployedBy", player, (SendMessageOptions)1);
                UnityEngine.Object.Destroy(storage.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(storage.GetComponent<GroundWatch>());
                storage.Spawn();
                storage.inventory.capacity = 8;
                return storage;
            }

            private void PlayerStoppedLooting(BasePlayer player)
            {
                if (ins.com != null && ins.com.OpenInterface.Contains(player))
                    ins.com.OpenInterface.Remove(player);

                CuiHelper.DestroyUi(player, "UI_Batia_External" + ".Mood1" + ".panel");
                CuiHelper.DestroyUi(player, UIPanel);
                ReturnPlayerItems();
                Destroy(this);
            }

            public void Close()
            {
                SendItems();
            }

            public void StartLoot()
            {
                player.EndLooting();
                storage.SetFlag(BaseEntity.Flags.Open, true, false);
                player.inventory.loot.StartLootingEntity(storage, false);
                player.inventory.loot.AddContainer(storage.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", storage.panelName);
                storage.DecayTouch();
                storage.SendNetworkUpdate();
                CreateUI();
                InvokeRepeating("UpdatePanels", 1f, 1f);
                InvokeRepeating("UpdateInfo", 1f, 1f);
            }

            bool disabledButton = false;

            void CreateUI()
            {
                CuiHelper.DestroyUi(player, UIPanel);
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "199 35", OffsetMax = "425 97" },
                    Image = { Color = "1 1 1 0" }
                }, "Overlay", UIPanel);

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0.77 0.74 0.71 0.05", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
                }, UIPanel, UIPanel + ".main");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.03 0.15", AnchorMax = "0.97 0.85" },
                    Button = { Command = disabledButton ? "UI_Batia sendLoot" : "", Color = disabledButton ? "0.41 0.47 0.26 1.00" : "0.41 0.47 0.26 0.2" },
                    Text = { Text = $"", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = disabledButton ? "0.59 0.69 0.42 0.7" : "0.59 0.69 0.42 0.2" }
                }, UIPanel + ".main", UIPanel + ".button");



                if (ins.com.CurrentExpeditions.ContainsKey(player.userID))
                {
                    container.Add(new CuiElement
                    {
                        Parent = UIPanel + ".button",

                        Components =
                        {
                            new CuiTextComponent { Text = $"ЕСТЬ АКТИВНЫЙ ОБМЕН", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 20, Color = "0.59 0.69 0.42 0.2"},
                            new CuiRectTransformComponent {AnchorMin = "0 0.1", AnchorMax = "1 1"}
                        }
                    });
                }
                else if (ins.com.EndExpeditions.ContainsKey(player.userID))
                {
                    container.Add(new CuiElement
                    {
                        Parent = UIPanel + ".button",

                        Components =
                        {
                            new CuiTextComponent { Text = $"СНАЧАЛА ЗАБЕРИТЕ ПОСЫЛКУ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 15, Color = "0.59 0.69 0.42 0.2"},
                            new CuiRectTransformComponent {AnchorMin = "0 0.1", AnchorMax = "1 1"}
                        }
                    });
                }
                else
                    container.Add(new CuiElement
                    {
                        Parent = UIPanel + ".button",

                        Components =
                        {
                            new CuiTextComponent { Text = $"ОТПРАВИТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 20, Color = disabledButton ? "0.59 0.69 0.42 0.7" : "0.59 0.69 0.42 0.2"},
                            new CuiRectTransformComponent {AnchorMin = "0 0.1", AnchorMax = "1 1"}
                        }
                    });

                CuiHelper.AddUi(player, container);
                UpdateInfo();
            }

            public void ReturnPlayerItems()
            {
                global::ItemContainer itemContainer = storage.inventory;
                if (itemContainer != null)
                {
                    for (int i = itemContainer.itemList.Count - 1; i >= 0; i--)
                    {
                        global::Item item = itemContainer.itemList[i];
                        player.GiveItem(item, global::BaseEntity.GiveItemReason.Generic);
                    }
                }
            }

            void UpdatePanels()
            {
                if (ins.com.CurrentExpeditions.ContainsKey(player.userID))
                {
                    disabledButton = false;
                    CreateUI();
                    return;

                }

                if (ins.com.EndExpeditions.ContainsKey(player.userID))
                {
                    disabledButton = false;
                    CreateUI();
                    return;

                }

                if (storage.inventory.itemList.Count > 0 && !disabledButton && PoitsInvectoryCount() >= 20)
                {
                    disabledButton = true;
                    CreateUI();
                    return;
                }
                if (storage.inventory.itemList.Count == 0 && disabledButton || PoitsInvectoryCount() < 20)
                {
                    disabledButton = false;
                    CreateUI();
                }
            }

            public float PoitsInvectoryCount()
            {
                float amount = 0;

                for (int i = 0; i < storage.inventory.itemList.Count; i++)
                {
                    var item = storage.inventory.itemList[i];
                    var configItem = ins.config.expeditionSettings.ItemsAdded[item.info.shortname];
                    amount += configItem.amount * item.amount;
                }

                return amount;
            }

            void UpdateInfo()
            {
                CuiHelper.DestroyUi(player, UIPanel + ".text");

                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Parent = UIPanel + ".button",
                    Name = UIPanel + ".text",

                    Components =
                        {
                            new CuiTextComponent { Color = disabledButton? "0.59 0.69 0.42 1" : "0.59 0.69 0.42 0.2", Text = PoitsInvectoryCount() > 0 ?  $"Текущие очки: {PoitsInvectoryCount()}" : "Нету предметов" , Align = TextAnchor.LowerCenter , FontSize = 11},
                            new CuiRectTransformComponent {AnchorMin = "0 0.05", AnchorMax = "1 1"}
                        }
                });
                CuiHelper.AddUi(player, container);
            }


            public void SendItems()
            {
                ins.com.StartExpedition(player, PoitsInvectoryCount());
                storage.inventory.itemList.Clear();
                player.EndLooting();
                Destroy(this);
            }

            public List<Item> GetItems => storage.inventory.itemList.Where(i => i != null).ToList();
            void OnDestroy()
            {
                ReturnPlayerItems();
                storage.Kill();
            }
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot, int amount)
        {
            var player = playerLoot.GetComponent<BasePlayer>();
            if (player == null || playerLoot == null || targetContainer == 0) return null;
            if (item.GetRootContainer() != null && item.GetRootContainer().entityOwner != null && item.GetRootContainer().entityOwner.GetComponent<ExpeditionExceptionBox>() != null)
            {
                var newContainer = playerLoot.FindContainer(targetContainer);
                if (newContainer == null) return null;
                var slot = newContainer.GetSlot(targetSlot);
                if (slot == null) return null;
                return false;
            }
            var container = playerLoot.FindContainer(targetContainer);
            if (container == null || container.entityOwner == null || container.entityOwner.GetComponent<ExpeditionExceptionBox>() == null) return null;
            if (!config.expeditionSettings.ItemsAdded.ContainsKey(item.info.shortname))
                return false;
            else
            {
                var configItem = config.expeditionSettings.ItemsAdded[item.info.shortname];
                var containsItem = container.itemList.Find(p => p.info.shortname == item.info.shortname);
                if (containsItem != null)
                {
                    if (configItem.maxCount == 1) return null;
                    if (containsItem.amount == configItem.maxCount) return false;
                    if ((containsItem.amount + amount) > configItem.maxCount)
                    {
                        var needAmount = configItem.maxCount - containsItem.amount;
                        item.UseItem(needAmount);

                        var newItem = ItemManager.CreateByItemID(item.info.itemid, needAmount);
                        newItem.MoveToContainer(container);
                        return false;
                    }
                }
                if (amount > configItem.maxCount)
                {
                    item.UseItem(configItem.maxCount);
                    var newItem = ItemManager.CreateByItemID(item.info.itemid, configItem.maxCount);
                    newItem.MoveToContainer(container);
                    return false;
                }
            }
            return null;
        }

        #endregion
    }
}