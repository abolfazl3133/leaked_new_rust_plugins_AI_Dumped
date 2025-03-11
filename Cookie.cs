using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Cookie", "Chibubrik", "1.0.0")]
    public class Cookie : RustPlugin
    {
        #region Вар
        private string Layer = "Cookie_UI";
        private string LayerInventory = "Inventory_UI";

        private static Cookie inst;
        [PluginReference] Plugin ImageLibrary;
        private Dictionary<ulong, CookieData> Data;
        #endregion

        #region Класс
        public class CookieSettings
        {
            [JsonProperty("Название предмета или команды")] public string DisplayName;
            [JsonProperty("Короткое название предмета")] public string ShortName;
            [JsonProperty("SkinID предмета")] public ulong SkinID;
            [JsonProperty("Дополнительная команда")] public string Command;
            [JsonProperty("Изображение")] public string Url;
            [JsonProperty("Минимальное количество при выпадени")] public int AmountMin;
            [JsonProperty("Максимальное Количество при выпадени")] public int AmountMax;
            public int GetRandomAmount() => Core.Random.Range(AmountMin, AmountMax);
        }

        private class CookieData
        {
            [JsonProperty("Сколько игрок открыл печенек")] public int Count;
            [JsonProperty("Откат")] public double Time;
            [JsonProperty("Список вещей")] public List<InventoryItem> Inventory = new List<InventoryItem>();
        }

        private class InventoryItem
        {
            [JsonProperty("Название предмета или команды")] public string DisplayName;
            [JsonProperty("Короткое название предмета")] public string ShortName;
            [JsonProperty("SkinID предмета")] public ulong SkinID;
            [JsonProperty("Дополнительная команда")] public string Command;
            [JsonProperty("Изображение")] public string Url;
            [JsonProperty("Количество предметов")] public int Amount;

            public Item GiveItem(BasePlayer player)
            {
                if (!string.IsNullOrEmpty(Command)) inst.Server.Command(Command.Replace("%STEAMID%", player.UserIDString));
                if (!string.IsNullOrEmpty(ShortName))
                {
                    Item item = ItemManager.CreateByPartialName(ShortName, Amount);

                    return item;
                }
                return null;
            }

            public static InventoryItem Generate(CookieSettings check)
            {
                return new InventoryItem
                {
                    DisplayName = check.DisplayName,
                    ShortName = check.ShortName,
                    SkinID = check.SkinID,
                    Command = check.Command,
                    Url = check.Url,
                    Amount = check.GetRandomAmount()
                };
            }
        }
        #endregion

        #region Конфиг
        public Configuration config;
        public class Configuration
        {
            [JsonProperty("Доступно печенек для открытия")] public int Count = 2;
            [JsonProperty("Откат на открытие печеньки")] public double Time = 120;
            [JsonProperty("Список наград")] public List<CookieSettings> settings;
            public static Configuration GetNewCong()
            {
                return new Configuration
                {
                    settings = new List<CookieSettings>
                    {
                        new CookieSettings
                        {
                            DisplayName = "Дерево",
                            ShortName = "wood",
                            SkinID = 0,
                            Command = null,
                            Url = null,
                            AmountMin = 1000,
                            AmountMax = 3000
                        },
                        new CookieSettings
                        {
                            DisplayName = "Вип на 7 дней",
                            ShortName = null,
                            SkinID = 0,
                            Command = "suka bleat %STEAMID%",
                            Url = "https://imgur.com/rfg3RRS.png",
                            AmountMin = 1,
                            AmountMax = 1
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
                config = Config.ReadObject<Configuration>();
                if (config?.settings == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        private void OnServerInitialized()
        {
            inst = this;
            ImageLibrary.Call("AddImage", "https://i.imgur.com/5huYGXR.png", "CookieImage");
            foreach (var check in config.settings)
            {
                ImageLibrary.Call("AddImage", check.Url, check.Url);
            }
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("CookieData/PlayerList"))
            {
                Data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, CookieData>>("CookieData/PlayerList");
            }
            else
            {
                Data = new Dictionary<ulong, CookieData>();
            }
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (!Data.ContainsKey(player.userID))
            {
                Data.Add(player.userID, new CookieData());
            }
            SaveData();
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, LayerInventory);
            }
            SaveData();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SaveData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("CookieData/PlayerList", Data);
        }

        private InventoryItem AddItem(BasePlayer player, CookieSettings check)
        {
            var item = InventoryItem.Generate(check);
            Data[player.userID].Inventory.Add(item);
            return item;
        }
        #endregion

        #region Команды
        [ChatCommand("cookie")]
        private void ChatCookie(BasePlayer player)
        {
            CookieUI(player);
        }

        [ConsoleCommand("cookie")]
        private void ConsoleCookie(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "ui")
                {
                    CookieUI(player);
                } 
                if (args.Args[0] == "inventory")
                {
                    InventoryUI(player);
                }
                if (args.Args[0] == "skip")
                {
                    InventoryUI(player, int.Parse(args.Args[1]));
                }
                if (args.Args[0] == "open")
                {
                    var currentTime = CurrentTime();
                    if (Data[player.userID].Time < currentTime)
                    {
                        Data[player.userID].Count++;
                        if (Data[player.userID].Count <= config.Count)
                        {
                            var item = config.settings.ToList().GetRandom();
                            PrizUI(player, args.Args[1], item);
                        }

                        if (Data[player.userID].Count == config.Count)
                        {
                            Data[player.userID].Time = CurrentTime() + config.Time;
                            Data[player.userID].Count = 0;
                            timer.Once(10f, () => CookieUI(player));
                        }
                    }
                }
                if (args.Args[0] == "take")
                {
                    var item = Data[player.userID].Inventory.ElementAt(int.Parse(args.Args[1]));
                    if (item.ShortName != null)
                    {
                        if (player.inventory.containerMain.itemList.Count >= 24)
                        {
                            player.ChatMessage($"У вас <color=#ee3e61>недостаточно</color> места в основном инвентаре!");
                            return;
                        }
                    }
                    var text = item.Command != null ? $"Вы получили услугу: <color=#ee3e61>{item.DisplayName}</color>" : $"Вы получили предмет: <color=#ee3e61>{item.DisplayName}</color>\nВ размере: <color=#8fde5b>{item.Amount}шт.</color>";
                    SendReply(player, text);
                    item.GiveItem(player)?.MoveToContainer(player.inventory.containerMain);
                    Data[player.userID].Inventory.Remove(item);
                    InventoryUI(player);
                }
            }
        }
        #endregion

        #region Интерфейс
        private void CookieUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();
            int ItemCount = config.settings.Count(), CountItem = 0, Count = 8;
            float Position = 0.5f, Width = 0.07f, Height = 0.115f, Margin = 0.003f, MinHeight = 0.375f;

            if (ItemCount >= Count) Position = 0.5f - Count / 2f * Width - (Count - 1) / 2f * Margin;
            else Position = 0.5f - ItemCount / 2f * Width - (ItemCount - 1) / 2f * Margin;
            ItemCount -= Count;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9" },
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-2 -2", AnchorMax = "2 2", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.1", Close = Layer },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 0.95", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = $"<b><size=35>ПЕЧЕНЬКА</size></b>\n {config.Time} Раз в сутки вы можете открывать {config.Count} печеньки и получать призы", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.94 0.46", AnchorMax = "0.99 0.54", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0", Command = "cookie inventory", Close = Layer },
                Text = { Text = ">", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 45, Font = "robotocondensed-bold.ttf" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.6", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = "<b><size=35>СПИСОК НАГРАД</size></b>\nИз этого списка будет выбираться 1 предмет", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            float width1 = 0.13f, height1 = 0.2f, startxBox1 = 0.306f, startyBox1 = 0.845f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            for (int z = 0; z < 3; z++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = xmin1 + " " + ymin1, AnchorMax = (xmin1 + width1) + " " + (ymin1 + height1 * 1), OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Button = { Color = "1 1 1 0.1", Command = $"cookie open {z}" },
                    Text = { Text = "", Color = "0.1 0.1 0.1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-bold.ttf" }
                }, Layer, $"Button.{z}");
                xmin1 += width1;

                container.Add(new CuiElement
                {
                    Name = $"Imagess.{z}",
                    Parent = $"Button.{z}",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "CookieImage") },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });
            }

            foreach (var check in config.settings)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{Position} {MinHeight}", AnchorMax = $"{Position + Width} {MinHeight + Height}", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1" },
                    Text = { Text = "" }
                }, Layer, "Item");

                var image = check.Command != null ? check.Url : check.ShortName;
                container.Add(new CuiElement
                {
                    Parent = "Item",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });

                var amount = check.AmountMax / 2;
                var count = check.AmountMax != check.AmountMin ? $"~{amount}" : $"{check.AmountMax}";
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0" },
                    Text = { Text = $"{count} ", Color = "1 1 1 0.3", Align = TextAnchor.LowerRight, FontSize = 18, Font = "robotocondensed-bold.ttf" }
                }, "Item");

                CountItem += 1;
                if (CountItem % Count == 0)
                {
                    if (ItemCount > Count)
                    {
                        Position = 0.5f - Count / 2f * Width - (Count - 1) / 2f * Margin;
                        ItemCount -= Count;
                    }
                    else
                    {
                        Position = 0.5f - ItemCount / 2f * Width - (ItemCount - 1) / 2f * Margin;
                    }
                    MinHeight -= ((Margin * 2) + Height);
                }
                else
                {
                    Position += (Width + Margin);
                }
            }

            if (Data[player.userID].Time >= CurrentTime())
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.308 0.648", AnchorMax = "0.695 0.843", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                    Text = { Text = $"ПОДОЖДИТЕ {FormatShortTime(TimeSpan.FromSeconds(Data[player.userID].Time - CurrentTime()))}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 50, Font = "robotocondensed-bold.ttf" }
                }, Layer);
            }

            CuiHelper.AddUi(player, container);
        }

        #region Приз
        private void PrizUI(BasePlayer player, string x, CookieSettings check)
        {
            CuiHelper.DestroyUi(player, $"Imagess.{x}");
            CuiElementContainer container = new CuiElementContainer();
            var items = AddItem(player, check);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0" },
                Text = { Text = "" }
            }, $"Button.{x}", "Layers");

            var image = items.Command != null ? items.Url : items.ShortName;
            container.Add(new CuiElement
            {
                Parent = "Layers",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image), FadeIn = 2f},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = $"{items.Amount}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 50, Font = "robotocondensed-bold.ttf" }
            }, "Layers");

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Инвентарь
        private void InventoryUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, LayerInventory);
            CuiElementContainer container = new CuiElementContainer();
            int ItemCount = Data[player.userID].Inventory.Count(), CountItem = 0, Count = 8;
            float Position = 0.5f, Width = 0.1f, Height = 0.155f, Margin = 0.003f, MinHeight = 0.668f;

            if (ItemCount >= Count) Position = 0.5f - Count / 2f * Width - (Count - 1) / 2f * Margin;
            else Position = 0.5f - ItemCount / 2f * Width - (ItemCount - 1) / 2f * Margin;
            ItemCount -= Count;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9" },
            }, "Overlay", LayerInventory);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-2 -2", AnchorMax = "2 2", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.1", Close = LayerInventory },
                Text = { Text = "" }
            }, LayerInventory);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 0.95", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = $"<b><size=35>ИНВЕНТАРЬ</size></b>\nТут будут хранится предметы, которые вы выбили с печенек!", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, LayerInventory);

            if (page == 0)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.01 0.46", AnchorMax = "0.06 0.54", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0", Command = "cookie ui", Close = LayerInventory },
                    Text = { Text = "<", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 45, Font = "robotocondensed-bold.ttf" }
                }, LayerInventory);
            }

            if (page != 0)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.01 0.46", AnchorMax = "0.06 0.54", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0", Command = $"cookie skip {page - 1}" },
                    Text = { Text = "<", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 45, Font = "robotocondensed-bold.ttf" }
                }, LayerInventory);
            }

            if ((float)Data[player.userID].Inventory.Count() > (page + 1) * 32)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.94 0.46", AnchorMax = "0.99 0.54", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0", Command = $"cookie skip {page + 1}" },
                    Text = { Text = ">", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 45, Font = "robotocondensed-bold.ttf" }
                }, LayerInventory);
            }

            var list = Data[player.userID].Inventory.Skip(page * 32).Take(32);
            foreach (var check in list.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{Position} {MinHeight}", AnchorMax = $"{Position + Width} {MinHeight + Height}", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1" },
                    Text = { Text = "" }
                }, LayerInventory, $"{check.B}");

                var image = check.A.Command != null ? check.A.Url : check.A.ShortName;
                container.Add(new CuiElement
                {
                    Parent = $"{check.B}",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = $"cookie take {check.B + page * 32}" },
                    Text = { Text = $"X{check.A.Amount} ", Color = "1 1 1 0.1", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.LowerRight }
                }, $"{check.B}");

                CountItem += 1;
                if (CountItem % Count == 0)
                {
                    if (ItemCount > Count)
                    {
                        Position = 0.5f - Count / 2f * Width - (Count - 1) / 2f * Margin;
                        ItemCount -= Count;
                    }
                    else
                    {
                        Position = 0.5f - ItemCount / 2f * Width - (ItemCount - 1) / 2f * Margin;
                    }
                    MinHeight -= ((Margin * 2) + Height);
                }
                else
                {
                    Position += (Width + Margin);
                }
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion
        #endregion

        #region Хелпер
        static double CurrentTime() => new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;
        public static string FormatShortTime(TimeSpan time)
        {
            string result = string.Empty;
            result += $"{time.Hours.ToString("00")}:";
            result += $"{time.Minutes.ToString("00")}";
            return result;
        }
        #endregion
    }
}