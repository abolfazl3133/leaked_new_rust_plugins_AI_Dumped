using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using Newtonsoft.Json.Converters;
using Facepunch;
using VLB;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("TopSystem", "EcoSmile", "1.0.2")]
    class TopSystem : RustPlugin
    {
        static TopSystem ins;
        PluginConfig config;

        public class PluginConfig
        { 
            [JsonProperty("Логотип сервера (если не нужно можно использовать просто название Например: My ServerName)")]
            public string ServerLogo = "https://i.imgur.com/XrerwR9.png";

            [JsonProperty("Название категорий (ru)")]
            public Dictionary<TopType, string> topNameRu = new Dictionary<TopType, string>()
            {
                [TopType.PvPKills] = "Топ убийств в ПВП",
                [TopType.PvEKills] = "Топ убийств в ПВE",
                [TopType.Sulfur] = "Топ по добыче Серы",
                [TopType.EnemyDestroy] = "Топ уничтожения Военной Техники",
                [TopType.CupboardDestroy] = "Топ Рейдеров",
                [TopType.CrateOpen] = "Топ Лутер",
                [TopType.Online] = "Топ по времении в сети",
                [TopType.AllResource] = "Топ по добыче всех ресурсов"
            };

            [JsonProperty("Название категорий (eng)")]
            public Dictionary<TopType, string> topNameEng = new Dictionary<TopType, string>()
            {
                [TopType.PvPKills] = "Top kills in PVP",
                [TopType.PvEKills] = "Top kills in PVE",
                [TopType.Sulfur] = "Top Sulfur Mining",
                [TopType.EnemyDestroy] = "Top destruction of Military Vehicle",
                [TopType.CupboardDestroy] = "Top Raiders",
                [TopType.CrateOpen] = "Top Looter",
                [TopType.Online] = "Top online time",
                [TopType.AllResource] = "Top all resources"
            };

            [JsonProperty("Награды за достижение ТОПа (Категория, Позиция - Призы)")]
            public Dictionary<TopType, Dictionary<int, List<PrizeItem>>> WeekplyPrizes;

        }

        public class PrizeItem
        {
            [JsonProperty("Шортнейм предмета")]
            public string ShortName;
            [JsonProperty("Исполняемая команда (%STEAMID% - ключ для вставки SteamID игрока)")]
            public string Command;
            [JsonProperty("Кастомное имя предмета")]
            public string CustomName;
            [JsonProperty("Минимальное количество предмета")]
            public int MinAmount;
            [JsonProperty("Максимальное количество предмета")]
            public int MaxAmount;
            [JsonProperty("SkinID предмета")]
            public ulong SkinID;
            [JsonProperty("Описание полученного предмета (%amount% - место вставки количества)")]
            public string Description;

            public PrizeItem Get()
            {
                var prize = new PrizeItem();
                prize.ShortName = ShortName;
                prize.Command = Command;
                prize.Description = Description;
                prize.CustomName = CustomName;
                var amount = UnityEngine.Random.Range(MinAmount, MaxAmount);
                prize.MinAmount = amount;
                prize.MaxAmount = amount;
                prize.SkinID = SkinID;
                return prize;
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                WeekplyPrizes = new Dictionary<TopType, Dictionary<int, List<PrizeItem>>>()
                {
                    [TopType.Sulfur] = new Dictionary<int, List<PrizeItem>>()
                    {
                        [1] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "",
                                Command = "addgroup %STEAMID% premium 7d",
                                Description = "Премиум на 7 дней",
                                SkinID = 0,
                                CustomName = "",
                            },
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 1000,
                                MaxAmount = 1500
                            }
                        },
                        [2] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 1000,
                                MaxAmount = 1500
                            }
                        },
                        [3] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 500,
                                MaxAmount = 1000
                            }
                        },
                    },
                    [TopType.CupboardDestroy] = new Dictionary<int, List<PrizeItem>>()
                    {
                        [1] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "",
                                Command = "addgroup %STEAMID% premium 7d",
                                Description = "Премиум на 7 дней",
                                SkinID = 0,
                                CustomName = "",
                            },
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 1000,
                                MaxAmount = 1500
                            }
                        },
                        [2] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 1000,
                                MaxAmount = 1500
                            }
                        },
                        [3] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 500,
                                MaxAmount = 1000
                            }
                        },
                    },
                    [TopType.PvPKills] = new Dictionary<int, List<PrizeItem>>()
                    {
                        [1] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "",
                                Command = "addgroup %STEAMID% premium 7d",
                                Description = "Премиум на 7 дней",
                                SkinID = 0,
                                CustomName = "",
                            },
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 1000,
                                MaxAmount = 1500
                            }
                        },
                        [2] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 1000,
                                MaxAmount = 1500
                            }
                        },
                        [3] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 500,
                                MaxAmount = 1000
                            }
                        },
                    },
                    [TopType.PvEKills] = new Dictionary<int, List<PrizeItem>>()
                    {
                        [1] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "",
                                Command = "addgroup %STEAMID% premium 7d",
                                Description = "Премиум на 7 дней",
                                SkinID = 0,
                                CustomName = "",
                            },
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 1000,
                                MaxAmount = 1500
                            }
                        },
                        [2] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 1000,
                                MaxAmount = 1500
                            }
                        },
                        [3] = new List<PrizeItem>()
                        {
                            new PrizeItem()
                            {
                                ShortName = "scrap",
                                Command = "",
                                Description = "Металлолом %amount% шт.",
                                SkinID = 0,
                                CustomName = "",
                                MinAmount = 500,
                                MaxAmount = 1000
                            }
                        },
                    },
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        public class PlayerData
        {
            public string DisplayName;
            public ulong userID;
            public float Value;
        }

        Dictionary<TopType, List<PlayerData>> topData = new Dictionary<TopType, List<PlayerData>>();
        Dictionary<ulong, List<ulong>> radiData = new Dictionary<ulong, List<ulong>>();

        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name + "/RadiData"))
                radiData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<ulong>>>(Name + "/RadiData");
            else
                Interface.Oxide.DataFileSystem.WriteObject(Name + "/RadiData", radiData = new Dictionary<ulong, List<ulong>>());

            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name + "/WeeklyResults"))
            {
                weekleyResult = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<PrizeItem>>>(Name + "/WeeklyResults");
            }
            else
                Interface.Oxide.DataFileSystem.WriteObject(Name + "/WeeklyResults", weekleyResult = new Dictionary<ulong, List<PrizeItem>>());

            foreach (TopType top in Enum.GetValues(typeof(TopType)))
            {
                var topBase = new List<PlayerData>();
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name + $"/{top}_Data"))
                    topBase = Interface.Oxide.DataFileSystem.ReadObject<List<PlayerData>>(Name + $"/{top}_Data");
                else
                    Interface.Oxide.DataFileSystem.WriteObject(Name + $"/{top}_Data", topBase = new List<PlayerData>());
                topData[top] = topBase;
            }

        }

        void SaveData()
        {
            foreach (var top in topData)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name + $"/{top.Key}_Data", top.Value);
            }
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/WeeklyResults", weekleyResult);
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum TopType { PvPKills, PvEKills, Sulfur, EnemyDestroy, CupboardDestroy, CrateOpen, Online, AllResource }

        private void OnServerInitialized()
        {
            ins = this;

            LoadData();

            WipeResults();

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            ImageLibrary.Call("AddImage", "https://i.imgur.com/fMy9GnT.png", "noavatar");
            if (config.ServerLogo.StartsWith("http"))
                ImageLibrary.Call("AddImage", config.ServerLogo, config.ServerLogo);

            counter = ServerMgr.Instance.StartCoroutine(OnlineCounter());
        }
        Coroutine counter;
        void Unload()
        {
            if (counter != null)
                ServerMgr.Instance.StopCoroutine(counter);

            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, "MainTop.BG");

            SaveData();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            if (player.IsReceivingSnapshot)
            {
                timer.In(1f, () => OnPlayerConnected(player));
                return;
            }
            foreach (TopType top in Enum.GetValues(typeof(TopType)))
            {
                if (topData[top].Any(x => x.userID == player.userID))
                {
                    var data = topData[top].FirstOrDefault(x => x.userID == player.userID);
                    if (data.DisplayName != player.displayName)
                        data.DisplayName = player.displayName;
                    continue;
                }
                else
                    topData[top].Add(new PlayerData() { DisplayName = player.displayName, userID = player.userID, Value = 0 });
            }

        }

        void OnServerSave()
        {
            SaveData();
        }

        bool isWipe = false;

        void OnNewSave()
        {
            isWipe = true;
        }

        Dictionary<ulong, List<PrizeItem>> weekleyResult = new Dictionary<ulong, List<PrizeItem>>();

        void WipeResults()
        {
            if (!isWipe) return; 

            Interface.Oxide.DataFileSystem.WriteObject(Name + "/WeekleyBase", weekleyResult = new Dictionary<ulong, List<PrizeItem>>());
            LogToFile("WeeklyResults", "~~~~~~~~~~~~~~~~~~~~~~~NEW WEEK~~~~~~~~~~~~~~~~~~~~~~~", this);
            foreach (TopType top in Enum.GetValues(typeof(TopType)))
            {
                if (!config.WeekplyPrizes.ContainsKey(top)) continue;
                var topBase = new List<PlayerData>();
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name + $"/{top}_Data"))
                    topBase = Interface.Oxide.DataFileSystem.ReadObject<List<PlayerData>>(Name + $"/{top}_Data");
                if (topBase.Count == 0) continue;
                var sortedBase = topBase.OrderByDescending(x => x.Value);
                int pos = 0;
                string logMsg = $"Category: {top}\n";
                foreach (var data in sortedBase.Take(3))
                {
                    pos++;
                    if (!weekleyResult.ContainsKey(data.userID))
                    {
                        logMsg += $"{pos}. {data.userID}\nPrizes:\n";
                        weekleyResult[data.userID] = new List<PrizeItem>();
                    }

                    foreach (PrizeItem it in config.WeekplyPrizes[top][pos])
                    {
                        var item = it.Get();
                        weekleyResult[data.userID].Add(item);
                        logMsg += $"{item.Description}\n";
                    }
                }
                LogToFile("WeeklyResults", logMsg, this);
            }

            Interface.Oxide.DataFileSystem.WriteObject(Name + "/WeekleyBase", weekleyResult);

            foreach (TopType top in Enum.GetValues(typeof(TopType)))
                Interface.Oxide.DataFileSystem.WriteObject(Name + $"/{top}_Data", topData[top] = new List<PlayerData>());

        }


        private float GetUserRating(ulong player, string topstring)
        {
            var top = (TopType)Enum.Parse(typeof(TopType), topstring);
            var value = topData[top].FirstOrDefault(x => x.userID == player).Value;
            return value;
        }

        private float GetUserRating(BasePlayer player, string topstring)
        {
            var top = (TopType)Enum.Parse(typeof(TopType), topstring);
            var value = topData[top].FirstOrDefault(x => x.userID == player.userID).Value;
            return value;
        }

        [ConsoleCommand("top")]
        void DrawMainTop(ConsoleSystem.Arg arg)
        {
            DrawMainTop(arg.Player());
        }

        [ChatCommand("top")]
        void DrawMainTop(BasePlayer player)
        {
            var lan = lang.GetLanguage(player.UserIDString);
            var container = new CuiElementContainer();
            UI.AddImage(ref container, "Overlay", "MainTop.BG", "0.141 0.137 0.109 0.9", "assets/content/ui/ui.background.tiletex.psd", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0 0", "1 1", "", "");
            UI.AddImage(ref container, "MainTop.BG", "MainTop", "0 0 0 0", "", "", "0 0", "1 1", "", "");
            if (!config.ServerLogo.StartsWith("http"))
                UI.AddText(ref container, "MainTop", "ServerName", "1 1 1 1", $"{config.ServerLogo}", TextAnchor.MiddleCenter, 32, "0.5 1", "0.5 1", "-200 -65", "200 -10", "0 0 0 0.5", "robotocondensed-regular.ttf");
            else
                UI.AddRawImage(ref container, "MainTop", "ServerLogo", GetItemImage(config.ServerLogo), "1 1 1 1", "", "", "0.5 1", "0.5 1", "-90 -85", "90 -5");
            UI.AddRawImage(ref container, "MainTop", "Player.Avatar", GetItemImage(player.UserIDString), "1 1 1 1", "", "", "0.5 0.725", "0.5 0.725", "-100 -100", "100 100");
            UI.AddText(ref container, "Player.Avatar", "Player.Name", "1 1 1 1", $"{player.displayName}", TextAnchor.MiddleCenter, 22, "0.5 0", "0.5 0", "-200 -35", "200 0", "0 0 0 0.5", "robotocondensed-regular.ttf");
            var startH = 0.2f;
            var startV = 0.5f;
            var reply = 0;
            var horisontal = startH;
            var vertical = startV;
            foreach (TopType cat in Enum.GetValues(typeof(TopType)))
            {
                var playerData = topData[cat].FirstOrDefault(x => x.userID == player.userID);
                UI.AddText(ref container, "MainTop", $"{cat}", "0.68 0.63 0.60 0.50", lan == "ru" ? $"{config.topNameRu[cat]}" : $"{config.topNameEng[cat]}", TextAnchor.MiddleCenter, 16, $"{horisontal} {vertical}", $"{horisontal} {vertical}", "-100 -15", "100 25", "0 0 0 0.5", "robotocondensed-regular.ttf");
                UI.AddImage(ref container, "MainTop", $"{cat}.line", "1 1 1 1", "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", "-100 -16", "100 -15");
                UI.AddText(ref container, $"{cat}.line", $"value", "0.68 0.63 0.60 0.90", $"{(cat == TopType.Online ? FormatTime(TimeSpan.FromSeconds(playerData.Value)) : $"{playerData.Value}")}", TextAnchor.MiddleCenter, 18, "0.5 0", "0.5 0", "-100 -45", "100 -5");
                horisontal += 0.2f;
                if (horisontal > 0.9f)
                {
                    horisontal = startH;
                    vertical -= 0.2f;
                }
            }
            if (reply == 0)
            { }
            UI.AddButton(ref container, "MainTop", "closeBtn", "", "MainTop.BG", "1 1 1 0.5", "assets/icons/close.png", "", "1 1", "1 1", "-45 -45", "-15 -15");
            UI.AddButton(ref container, "MainTop", "ToGlobal.btn", "alltop", "", "0.68 0.63 0.60 0.90", "assets/content/ui/capsule-background.png", "", "0.5 0", "0.5 0", "-65 15", "65 60");
            UI.AddText(ref container, "ToGlobal.btn", "ToGlobal.btn.txt", "0.68 0.63 0.60 0.90", lan == "ru" ? "ОБЩАЯ СТАТИСТИКА" : "GENERAL STATISTICS", TextAnchor.MiddleCenter, 16, "0.5 0.5", "0.5 0.5", "-65 -20", "65 20");
            if (weekleyResult.ContainsKey(player.userID))
            {
                UI.AddButton(ref container, "MainTop", "WeekplyPrize.btn", "getweeklyreward", "MainTop.BG", "0.54 0.71 0.24 0.80", "assets/content/ui/capsule-background.png", "", "0.5 0", "0.5 0", "-200 15", "-70 60");
                UI.AddText(ref container, "WeekplyPrize.btn", "WeekplyPrize.btn.txt", "0.68 0.63 0.60 0.90", lan == "ru" ? "ЗАБРАТЬ НАГРАДУ" : "GET THE REWARD", TextAnchor.MiddleCenter, 16, "0.5 0.5", "0.5 0.5", "-65 -20", "65 20");
            }
            CuiHelper.DestroyUi(player, "MainTop.BG");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("alltop")]
        void DrawAllto(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var lan = lang.GetLanguage(player.UserIDString);
            var container = new CuiElementContainer();
            UI.AddImage(ref container, "MainTop.BG", "MainTop", "0 0 0 0", "", "", "0 0", "1 1", "", "");
            UI.AddButton(ref container, "MainTop", "closeBtn", "", "MainTop.BG", "1 1 1 0.5", "assets/icons/close.png", "", "1 1", "1 1", "-45 -45", "-15 -15");
            UI.AddText(ref container, "MainTop", "TopHeader", "0.68 0.63 0.60 0.90", lan == "ru" ? "ТОП КАТЕГОРИИ:" : "TOP CATEGORY:", TextAnchor.MiddleLeft, 22, "0 1", "0 1", "5 -35", "205 -5", "0 0 0 0.5", "robotocondensed-regular.ttf");
            var startH = 0.01f;
            var startV = 0.925f;
            var horisontal = startH;
            var vertical = startV;
            int page = 0;
            foreach (TopType cat in Enum.GetValues(typeof(TopType)))
            {
                UI.AddButton(ref container, "MainTop", "Category.Btn", $"opencategory {page}", "", "1 1 1 0.0", "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", "0 -10", "350 10");
                UI.AddText(ref container, "Category.Btn", $"{cat}", page == 0 ? "0.68 0.63 0.60 0.90" : "0.68 0.63 0.60 0.50", lan == "ru" ? $"• {config.topNameRu[cat]}" : $"• {config.topNameEng[cat]}", TextAnchor.MiddleLeft, 16, $"0 0.5", $"0 0.5", "0 -10", "350 10", "0 0 0 0.5", "robotocondensed-regular.ttf");
                vertical -= 0.03f;
                page++;
            }
            CuiHelper.DestroyUi(player, "MainTop");
            CuiHelper.AddUi(player, container);
            DrawTopInfo(player);
        }

        [ConsoleCommand("opencategory")]
        void OpenCategory(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var lan = lang.GetLanguage(player.UserIDString);
            int page = arg.GetInt(0);
            var container = new CuiElementContainer();
            UI.AddImage(ref container, "MainTop.BG", "MainTop", "0 0 0 0", "", "", "0 0", "1 1", "", "");
            UI.AddButton(ref container, "MainTop", "closeBtn", "", "MainTop.BG", "1 1 1 0.5", "assets/icons/close.png", "", "1 1", "1 1", "-45 -45", "-15 -15");
            UI.AddText(ref container, "MainTop", "TopHeader", "0.68 0.63 0.60 0.90", lan == "ru" ? "ТОП КАТЕГОРИИ:" : "TOP CATEGORY:", TextAnchor.MiddleLeft, 22, "0 1", "0 1", "5 -35", "205 -5", "0 0 0 0.5", "robotocondensed-regular.ttf");
            var startH = 0.01f;
            var startV = 0.925f;
            var horisontal = startH;
            var vertical = startV;
            int index = 0;
            foreach (TopType cat in Enum.GetValues(typeof(TopType)))
            {
                UI.AddButton(ref container, "MainTop", "Category.Btn", $"opencategory {index}", "", "1 1 1 0.0", "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", "0 -10", "350 10");
                UI.AddText(ref container, "Category.Btn", $"{cat}", index == page ? "0.68 0.63 0.60 0.90" : "0.68 0.63 0.60 0.50", lan == "ru" ? $"• {config.topNameRu[cat]}" : $"• {config.topNameEng[cat]}", TextAnchor.MiddleLeft, 16, $"0 0.5", $"0 0.5", "0 -10", "350 10", "0 0 0 0.5", "robotocondensed-regular.ttf");
                vertical -= 0.03f;
                index++;
            }
            CuiHelper.DestroyUi(player, "MainTop");
            CuiHelper.AddUi(player, container);
            DrawTopInfo(player, page);
        }

        void DrawTopInfo(BasePlayer player, int page = 0, ulong playerid = 0)
        {
            var container = new CuiElementContainer();
            TopType cat = (TopType)page;
            string lan = "‌‌﻿‌‍‌​";
            lan = lang.GetLanguage(player.UserIDString);
            UI.AddText(ref container, "MainTop", "HeaderTop", "0.68 0.63 0.60 0.90", lan == "ru" ? $"{config.topNameRu[cat].ToUpper()}" : $"{config.topNameEng[cat].ToUpper()}", TextAnchor.MiddleCenter, 32, "0.5 1", "0.5 1", "-350 -50", "350 0");
            var data = topData[cat].OrderByDescending(x => x.Value).ToList();
            var startH = 0.15f;
            var startV = 0.35f;
            var horisontal = startH;
            var vertical = startV;
            int index = 4;

            UI.AddImage(ref container, "MainTop", "Player.BG.Gold", "0.68 0.63 0.60 0.5", "", "", $"0.5 0.8", "0.5 0.8", "-75 -75", "75 75", "1.00 1.00 0.00 1.00", "1 1");
            UI.AddRawImage(ref container, "Player.BG.Gold", "Player.Avatar", data.Count > 0 ? GetItemImage(data[0].userID.ToString()) : GetItemImage("noavatar"), "1 1 1 1", "", "", "0.5 0.5", "0.5 0.5", "-75 -75", "75 75");
            UI.AddText(ref container, $"Player.Avatar", "PlayerName", "0.68 0.63 0.60 0.90", data.Count > 0 ? $"{string.Join("", data[0].DisplayName.Take(13))}\n({(cat == TopType.Online ? FormatTime(TimeSpan.FromSeconds(data[0].Value)) : $"{data[0].Value}")})" : "", TextAnchor.UpperCenter, 20, "0.5 0", "0.5 0", "-75 -55", "75 -5");
            UI.AddText(ref container, $"Player.Avatar", "PlayerRank", "1.00 1.00 0.00 1.00", "#1 ", TextAnchor.LowerRight, 22, "0 0", "1 1", "", "");

            UI.AddImage(ref container, "MainTop", "Player.BG.Silver", "0.68 0.63 0.60 0.5", "", "", $"0.35 0.65", "0.35 0.65", "-65 -65", "65 65", "0.75 0.75 0.75 1.00", "1 1");
            UI.AddRawImage(ref container, "Player.BG.Silver", "Player.Avatar", data.Count > 1 ? GetItemImage(data[1].userID.ToString()) : GetItemImage("noavatar"), "1 1 1 1", "", "", "0.5 0.5", "0.5 0.5", "-65 -65", "65 65");
            UI.AddText(ref container, $"Player.Avatar", "PlayerName", "0.68 0.63 0.60 0.90", data.Count > 1 ? $"{string.Join("", data[1].DisplayName.Take(13))}\n({(cat == TopType.Online ? FormatTime(TimeSpan.FromSeconds(data[1].Value)) : $"{data[1].Value}")})" : "", TextAnchor.UpperCenter, 18, "0.5 0", "0.5 0", "-75 -55", "75 -5");
            UI.AddText(ref container, $"Player.Avatar", "PlayerRank", "0.75 0.75 0.75 1.00", "#2 ", TextAnchor.LowerRight, 20, "0 0", "1 1", "", "");

            UI.AddImage(ref container, "MainTop", "Player.BG.Bronze", "0.68 0.63 0.60 0.5", "", "", $"0.65 0.635", "0.65 0.635", "-55 -55", "55 55", "0.50 0.25 0.00 1.00", "1 1");
            UI.AddRawImage(ref container, "Player.BG.Bronze", "Player.Avatar", data.Count > 2 ? GetItemImage(data[2].userID.ToString()) : GetItemImage("noavatar"), "1 1 1 1", "", "", "0.5 0.5", "0.5 0.5", "-55 -55", "55 55");
            UI.AddText(ref container, $"Player.Avatar", "PlayerName", "0.68 0.63 0.60 0.90", data.Count > 2 ? $"{string.Join("", data[2].DisplayName.Take(13))}\n({(cat == TopType.Online ? FormatTime(TimeSpan.FromSeconds(data[2].Value)) : $"{data[2].Value}")})" : "", TextAnchor.UpperCenter, 16, "0.5 0", "0.5 0", "-75 -55", "75 -5");
            UI.AddText(ref container, $"Player.Avatar", "PlayerRank", "0.50 0.25 0.00 1.00", "#3 ", TextAnchor.LowerRight, 18, "0 0", "1 1", "", "");

            for (int i = 3; i < 10; i++)
            {
                UI.AddImage(ref container, "MainTop", "Player.BG", "0.68 0.63 0.60 0.5", "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", "-35 -35", "35 35");
                UI.AddRawImage(ref container, "Player.BG", "Player.Avatar", data.Count > i ? GetItemImage(data[i].userID.ToString()) : GetItemImage("noavatar"), "1 1 1 1", "", "", "0.5 0.5", "0.5 0.5", "-35 -35", "35 35");
                UI.AddText(ref container, $"Player.Avatar", "PlayerName", "0.68 0.63 0.60 0.90", data.Count > i ? $"{string.Join("", data[i].DisplayName.Take(13))}\n({(cat == TopType.Online ? FormatTime(TimeSpan.FromSeconds(data[i].Value)) : $"{data[i].Value}")})" : "", TextAnchor.UpperCenter, 14, "0.5 0", "0.5 0", "-75 -55", "75 -5");
                UI.AddText(ref container, $"Player.Avatar", "PlayerRank", "0.75 0.75 0.75 1.00", $"#{index} ", TextAnchor.LowerRight, 20, "0 0", "1 1", "", "");
                index++;
                horisontal += 0.12f;
            }

            UI.AddButton(ref container, "MainTop", "ToGlobal.btn", "top", "", "0.68 0.63 0.60 0.90", "assets/content/ui/capsule-background.png", "", "0.5 0", "0.5 0", "-65 15", "65 60");
            UI.AddText(ref container, "ToGlobal.btn", "ToGlobal.btn.txt", "0.68 0.63 0.60 0.90", lan == "ru" ? "ЛИЧНАЯ СТАТИСТИКА" : "PERSONAL STATISTICS", TextAnchor.MiddleCenter, 16, "0.5 0.5", "0.5 0.5", "-65 -20", "65 20");
            if (weekleyResult.ContainsKey(player.userID))
            {
                UI.AddButton(ref container, "MainTop", "WeekplyPrize.btn", "getweeklyreward", "MainTop.BG", "0.54 0.71 0.24 0.80", "assets/content/ui/capsule-background.png", "", "0.5 0", "0.5 0", "-200 15", "-70 60");
                UI.AddText(ref container, "WeekplyPrize.btn", "WeekplyPrize.btn.txt", "0.68 0.63 0.60 0.90", lan == "ru" ? "ЗАБРАТЬ НАГРАДУ" : "GET THE REWARD", TextAnchor.MiddleCenter, 16, "0.5 0.5", "0.5 0.5", "-65 -20", "65 20");
            }
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("getweeklyreward")]
        void GetweeklyRewad(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!weekleyResult.ContainsKey(player.userID)) return;

            string msg = "Вы получили:\n";

            foreach (var data in weekleyResult[player.userID])
            {
                if (!string.IsNullOrEmpty(data.Command))
                {
                    string cmd = data.Command;
                    rust.RunServerCommand(cmd.Replace("%STEAMID%", player.UserIDString));
                }
                var amount = UnityEngine.Random.Range(data.MinAmount, data.MaxAmount);
                if (!string.IsNullOrEmpty(data.ShortName))
                {
                    var item = ItemManager.CreateByName(data.ShortName, amount, data.SkinID);
                    if (!string.IsNullOrEmpty(data.CustomName))
                        item.name = data.CustomName;
                    player.GiveItem(item);
                }
                msg += $"{data.Description.Replace("%amount%", amount.ToString())}\n";
            }
            SendReply(player, msg);
            weekleyResult.Remove(player.userID);

            LogToFile("WeeklyResults", $"Игрок {player.displayName} [{player.userID}] успешно получил все призы и был удален из базы.", this);
        }

        List<LootContainer> handledContainers = new List<LootContainer>();

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == null) return;
            var container = entity?.GetComponent<LootContainer>();
            if (container == null) return;
            if (handledContainers.Contains(container)) return;
            handledContainers.Add(container);

            AddPlayerPoint(player, TopType.CrateOpen, 1);
        }

        public class PlayerDamage
        {
            public BasePlayer player;
            public float Damage;
        }

        private Dictionary<ulong, List<PlayerDamage>> LastHit = new Dictionary<ulong, List<PlayerDamage>>();

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info.Initiator is BasePlayer)
            {
                if (entity is BaseHelicopter || entity is BradleyAPC || entity is CH47Helicopter)
                {
                    var player = info.InitiatorPlayer;
                    if (player == null) return;
                    if (!LastHit.ContainsKey(entity.net.ID.Value))
                        LastHit[entity.net.ID.Value] = new List<PlayerDamage>();


                    var data = LastHit[entity.net.ID.Value].FirstOrDefault(x => x.player == player);
                    if (data == null)
                    {
                        LastHit[entity.net.ID.Value].Add(new PlayerDamage() { Damage = 0, player = player });
                        data = LastHit[entity.net.ID.Value].FirstOrDefault(x => x.player == player);
                    }
                    var damage = (int)Math.Round(info.damageTypes.Total(), 0, MidpointRounding.AwayFromZero);
                    data.Damage += damage;
                }
            }
        }

        public class KillData
        {
            public DateTime killdate;
            public ulong UserID;
        }

        Dictionary<BasePlayer, List<KillData>> killData = new Dictionary<BasePlayer, List<KillData>>();

        void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim == null || info == null) return;
            BasePlayer victimBP = victim.ToPlayer();
            BasePlayer attacker = info.InitiatorPlayer;
            if (victim.ShortPrefabName == "cupboard.tool.deployed" && attacker != null && !IsNPC(attacker))
            {
                var cupOwnerID = victim.OwnerID;
                if (cupOwnerID == attacker.userID) return;
                if (radiData.ContainsKey(attacker.userID) && radiData[attacker.userID].Contains(cupOwnerID)) return;
                if (!radiData.ContainsKey(attacker.userID))
                    radiData[attacker.userID] = new List<ulong>();
                radiData[attacker.userID].Add(cupOwnerID);
                AddPlayerPoint(attacker, TopType.CupboardDestroy, 1);
            }

            if (victim is BaseHelicopter || victim is BradleyAPC || victim is CH47Helicopter)
            {
                if (LastHit.ContainsKey(victim.net.ID.Value))
                {
                    var damageData = LastHit[victim.net.ID.Value].OrderByDescending(x => x.Damage).ToList();
                    if (victim is BaseHelicopter)
                        AddPlayerPoint(damageData[0].player, TopType.EnemyDestroy, 1);
                    if (victim is BradleyAPC)
                        AddPlayerPoint(damageData[0].player, TopType.EnemyDestroy, 1);
                    if (victim is CH47Helicopter)
                        AddPlayerPoint(damageData[0].player, TopType.EnemyDestroy, 1);
                }
            }

            if (attacker != null && !IsNPC(attacker) && victimBP != null)
            {
                if (victimBP.userID.IsSteamId())
                {
                    if (attacker == victimBP) return;
                    if (!killData.ContainsKey(attacker))
                    {
                        killData[attacker] = new List<KillData>();
                        killData[attacker].Add(new KillData() { killdate = DateTime.Now.AddMinutes(5), UserID = victimBP.userID });
                        AddPlayerPoint(attacker, TopType.PvPKills, 1);
                        return;
                    }
                    else
                    {
                        var data = killData[attacker];
                        var killDataVictum = data.FirstOrDefault(x => x.UserID == victimBP.userID);
                        if (killDataVictum == null)
                        {
                            killData[attacker].Add(new KillData() { killdate = DateTime.Now.AddMinutes(5), UserID = victimBP.userID });
                            AddPlayerPoint(attacker, TopType.PvPKills, 1);
                        }
                        else if (killDataVictum.killdate < DateTime.Now)
                        {
                            killDataVictum.killdate = DateTime.Now.AddMinutes(10);
                            AddPlayerPoint(attacker, TopType.PvPKills, 1);
                        }
                    }
                }
                else
                    AddPlayerPoint(attacker, TopType.PvEKills, 1);

            }
            return;
        }

        object OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            switch (item.info.shortname)
            {
                case "sulfur.ore":
                    AddPlayerPoint(player, TopType.Sulfur, 1 * item.amount);
                    break;
                default:
                    AddPlayerPoint(player, TopType.AllResource, 1 * item.amount);
                    break;
            }
            return null;
        }

        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            switch (item.info.shortname)
            {
                case "sulfur.ore":
                    AddPlayerPoint(player, TopType.Sulfur, 1 * item.amount);
                    break;
                default:
                    AddPlayerPoint(player, TopType.AllResource, 1 * item.amount);
                    break;
            }

            return null;
        }

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) => OnDispenserGather(dispenser, player, item);

        void AddPlayerPoint(BasePlayer player, TopType top, float point)
        {
            var data = topData[top].FirstOrDefault(x => x.userID == player.userID);
            if (data != null)
            {
                data.Value += point;
            }
            else
                topData[top].Add(new PlayerData() { DisplayName = player.displayName, userID = player.userID, Value = point });
        }

        void AddPlayerPoint(ulong userID, TopType top, float point)
        {
            var data = topData[top].FirstOrDefault(x => x.userID == userID);
            if (data != null)
            {
                data.Value += point;
            }
        }

        private bool IsNPC(BasePlayer player)
        {
            if (player == null) return false;
            if (player is NPCPlayer)
                return true;
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L))
                return true;
            return false;
        }

        IEnumerator OnlineCounter()
        {
            while (true)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    AddPlayerPoint(player, TopType.Online, 1);

                    yield return null;
                }
                yield return new WaitForSeconds(1f);
            }
        }

        public static string FormatTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result += $"{Format(time.Days, "д.", "д.", "д.")} ";

            if (time.Hours != 0)
                result += $"{Format(time.Hours, "ч.", "ч.", "ч.")} ";

            if (time.Minutes != 0)
                result += $"{Format(time.Minutes, "м.", "м.", "м.")} ";

            if (time.Seconds != 0)
                result += $"{Format(time.Seconds, "с.", "с.", "с.")} ";

            return result;
        }

        private static string Format(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units} {form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units} {form2}";

            return $"{units} {form3}";
        }
        [PluginReference] Plugin ImageLibrary;

        public string GetItemImage(string shortname, ulong skinID = 0)
        {
            if (skinID > 0)
            {
                if (ImageLibrary.Call<bool>("HasImage", shortname, skinID) == false && ImageLibrary.Call<Dictionary<string, object>>("GetSkinInfo", shortname, skinID) == null)
                {

                    webrequest.Enqueue("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", $"key=349F5903E6EDAD3D615652E2B8AF4527&itemcount=1&publishedfileids%5B0%5D={skinID}", (code, response) =>
                    {
                        if (code != 200 || response == null)
                        {
                            PrintError($"Image failed to download! Code HTTP error: {code} - Image Name: {shortname} - Image skinID: {skinID} - Response: {response}");
                            return;
                        }


                        SteampoweredResult sr = JsonConvert.DeserializeObject<SteampoweredResult>(response);
                        if (sr == null || !(sr is SteampoweredResult) || sr.response.result == 0 || sr.response.resultcount == 0)
                        {
                            PrintError($"Image failed to download! Error: Parse JSON response - Image Name: {shortname} - Image skinID: {skinID} - Response: {response}");
                            return;
                        }

                        foreach (SteampoweredResult.Response.PublishedFiled publishedfiled in sr.response.publishedfiledetails)
                        {
                            ImageLibrary.Call("AddImage", publishedfiled.preview_url, shortname, skinID);
                        }

                    }, this, RequestMethod.POST);

                    return ImageLibrary.Call<string>("GetImage", "LOADING");

                }
            }

            return ImageLibrary.Call<string>("GetImage", shortname, skinID);
        }

        private class SteampoweredResult
        {
            public Response response;
            public class Response
            {
                [JsonProperty("result")]
                public int result;

                [JsonProperty("resultcount")]
                public int resultcount;

                [JsonProperty("publishedfiledetails")]
                public List<PublishedFiled> publishedfiledetails;
                public class PublishedFiled
                {
                    [JsonProperty("publishedfileid")]
                    public ulong publishedfileid;

                    [JsonProperty("result")]
                    public int result;

                    [JsonProperty("creator")]
                    public string creator;

                    [JsonProperty("creator_app_id")]
                    public int creator_app_id;

                    [JsonProperty("consumer_app_id")]
                    public int consumer_app_id;

                    [JsonProperty("filename")]
                    public string filename;

                    [JsonProperty("file_size")]
                    public int file_size;

                    [JsonProperty("preview_url")]
                    public string preview_url;

                    [JsonProperty("hcontent_preview")]
                    public string hcontent_preview;

                    [JsonProperty("title")]
                    public string title;

                    [JsonProperty("description")]
                    public string description;

                    [JsonProperty("time_created")]
                    public int time_created;

                    [JsonProperty("time_updated")]
                    public int time_updated;

                    [JsonProperty("visibility")]
                    public int visibility;

                    [JsonProperty("banned")]
                    public int banned;

                    [JsonProperty("ban_reason")]
                    public string ban_reason;

                    [JsonProperty("subscriptions")]
                    public int subscriptions;

                    [JsonProperty("favorited")]
                    public int favorited;

                    [JsonProperty("lifetime_subscriptions")]
                    public int lifetime_subscriptions;

                    [JsonProperty("lifetime_favorited")]
                    public int lifetime_favorited;

                    [JsonProperty("views")]
                    public int views;

                    [JsonProperty("tags")]
                    public List<Tag> tags;
                    public class Tag
                    {
                        [JsonProperty("tag")]
                        public string tag;
                    }
                }
            }
        }
        public static class UI
        {
            public static void AddImage(ref CuiElementContainer container, string parrent, string name, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {

                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{Color = color, Sprite = sprite, Material = mat},
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{ Color = color, Sprite = sprite, Material = mat },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{ Color = color, Sprite = sprite },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{ Color = color },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiImageComponent{Color = color, Material = mat},
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiImageComponent{Color = color, Sprite = sprite},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiImageComponent{Color = color},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

            }

            public static void AddRawImage(ref CuiElementContainer container, string parrent, string name, string png, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax)
            {
                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Sprite = sprite, Material = mat, Png = png},
                        new CuiNeedsCursorComponent{ },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Material = mat, Png = png},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Sprite = sprite, Png = png},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiRawImageComponent{Color = color, Png = png},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddText(ref CuiElementContainer container, string parrent, string name, string color, string text, TextAnchor align, int size, string aMin, string aMax, string oMin, string oMax, string outColor = "0 0 0 0.5", string font = "robotocondensed-bold.ttf", string dist = "1 1", float FadeIN = 0f, float FadeOut = 0f)
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Name = name,
                    FadeOut = FadeOut,
                    Components =
                    {
                        new CuiTextComponent{Color = color,Text = text, Align = align, FontSize = size, Font = font, FadeIn = FadeIN},
                        new CuiOutlineComponent{Color = outColor, Distance = dist},
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                });

            }

            public static void AddButton(ref CuiElementContainer container, string parrent, string name, string cmd, string close, string color, string sprite, string mat, string aMin, string aMax, string oMin, string oMax, string outline = "", string dist = "")
            {
                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite, Material = mat, },
                            new CuiNeedsCursorComponent{ },
                            new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                        }
                    });

                if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat) && !string.IsNullOrEmpty(outline))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                        {
                            new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite, Material = mat, },
                            new CuiOutlineComponent{Color = outline, Distance = dist},
                            new CuiRectTransformComponent{ AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
                        }
                    });

                if (string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, Material = mat, },
                        new CuiNeedsCursorComponent{ },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (!string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, Sprite = sprite},
                        new CuiNeedsCursorComponent{ },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });

                if (string.IsNullOrEmpty(sprite) && string.IsNullOrEmpty(mat))
                    container.Add(new CuiElement()
                    {
                        Parent = parrent,
                        Name = name,
                        Components =
                    {
                        new CuiButtonComponent{Command = cmd, Color = color, Close = close, },
                        new CuiNeedsCursorComponent{ },
                        new CuiRectTransformComponent{AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                    }
                    });
            }

            public static void AddInputField(ref CuiElementContainer container, string parrent, string name, string cmd, TextAnchor align, int size, int charLimit)
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Name = name,
                    Components =
                        {
                            new CuiInputFieldComponent{ Align = align, FontSize = size, Command = cmd, Font = "permanentmarker.ttf", CharsLimit = charLimit },
                            new CuiRectTransformComponent{  AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                });
            }
        }
    }
}
