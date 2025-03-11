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
using Oxide.Core.SQLite.Libraries;
using Oxide.Core.Database;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("DailyCards", "EcoSmile", "1.0.4")]
    class DailyCards : RustPlugin
    {
        static DailyCards ins;
        PluginConfig config;

        [JsonConverter(typeof(StringEnumConverter))]
        public enum CardType { Normal, Rare, Legendary }

        public class PluginConfig
        {
            [JsonProperty(PropertyName = "Steam API key (get one here https://steamcommunity.com/dev/apikey)")]
            public string SteamAPIKey;

            [JsonProperty("Интервал синхронизации базы данных")]
            public float SyncTime;
            [JsonProperty("Количество открытий каждый день")]
            public int DailyOpenCard;
            [JsonProperty("Максимум Легендарных карточек")]
            public int MaxLegendaryCard;
            [JsonProperty("Максимум Редких карточек")]
            public int MaxRareCard;
            [JsonProperty("Откат карточек в минутах")]
            public float CardCooldonw;

            [JsonProperty("Карточки")]
            public List<CardSetting> Cards;
        }

        static System.Random rnd = new System.Random();
        public class CardSetting
        {

            [JsonProperty("Редкость карточки (Normal, Rare, Legendary)")]
            public CardType Type;
            [JsonProperty("Карточка считается Набором? (Будут выданы все предметы из списка)")]
            public bool IsKit;
            [JsonProperty("Карточка считается Кейсом? (Будет выдан случайный предмет из списка)")]
            public bool IsCase;
            [JsonProperty("Картинка для UI (если карточка является кейсом или набором)")]
            public string ImageURL;
            [JsonProperty("Предметы карточки (если Набор и Кейс - false будет выдан первый предмет из списка)")]
            public List<CardItem> CardItems = new List<CardItem>();

            [JsonProperty("Системный ID Карточки. Не менять. При добавлении новой карточки ставить 0")]
            public ulong CardID;


            public List<CardItem> GetItems()
            {
                var itemList = new List<CardItem>();
                if (IsKit)
                {
                    CardItems.ForEach(x => itemList.Add(x.Get()));
                }
                else if (IsCase)
                {
                    itemList.Add(CardItems.GetRandom().Get());
                }
                else
                    itemList.Add(CardItems.FirstOrDefault().Get());

                return itemList;
            }

        }

        public class CardItem
        {
            [JsonProperty("Шортнейм предмета")]
            public string ShortName;
            [JsonProperty("Исполняемая команда (%STEAMID% - ключ для вставки SteamID игрока)")]
            public string Command;
            [JsonProperty("Картинка для UI (если нужно)")]
            public string ImageURL;
            [JsonProperty("Имя предмета для UI (если нужно)")]
            public string UIName;
            [JsonProperty("Кастомное имя предмета")]
            public string CustomName;
            [JsonProperty("Минимальное количество предмета")]
            public int MinAmount;
            [JsonProperty("Максимальное количество предмета")]
            public int MaxAmount;
            [JsonProperty("SkinID предмета")]
            public ulong SkinID;

            public CardItem Get()
            {
                var prize = new CardItem();
                prize.ShortName = ShortName;
                prize.Command = Command;
                prize.ImageURL = ImageURL;
                prize.UIName = UIName;
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
                CardCooldonw = 600,
                SteamAPIKey = "",
                SyncTime = 60,
                DailyOpenCard = 3,
                MaxRareCard = 4,
                MaxLegendaryCard = 3,
                Cards = new List<CardSetting>()
                {
                    new CardSetting()
                    {
                        Type = CardType.Legendary,
                        IsCase = true,
                        IsKit = false,
                        ImageURL = "https://i.imgur.com/ykUR9FN.png",
                        CardItems = new List<CardItem>()
                        {
                            new CardItem()
                            {
                                ShortName = "ammo.rocket.basic",
                                MinAmount = 5,
                                MaxAmount = 10,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "explosive.timed",
                                MinAmount = 2,
                                MaxAmount = 5,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "explosive.satchel",
                                MinAmount = 10,
                                MaxAmount = 25,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "grenade.beancan",
                                MinAmount = 20,
                                MaxAmount = 30,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "explosives",
                                MinAmount = 1000,
                                MaxAmount = 1500,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                        }
                    },
                    new CardSetting()
                    {
                        Type = CardType.Rare,
                        IsCase = true,
                        IsKit = false,
                        ImageURL = "https://i.imgur.com/sN5dOH8.png",
                        CardItems = new List<CardItem>()
                        {
                            new CardItem()
                            {
                                ShortName = "sulfur",
                                MinAmount = 1000,
                                MaxAmount = 2500,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "gunpowder",
                                MinAmount = 500,
                                MaxAmount = 1500,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "explosives",
                                MinAmount = 100,
                                MaxAmount = 500,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "fat.animal",
                                MinAmount = 1000,
                                MaxAmount = 2000,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "lowgradefuel",
                                MinAmount = 500,
                                MaxAmount = 1000,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "metal.refined",
                                MinAmount = 100,
                                MaxAmount = 500,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                        }
                    },
                    new CardSetting()
                    {
                        Type = CardType.Legendary,
                        IsCase = false,
                        IsKit = true,
                        ImageURL = "https://i.imgur.com/pmRwHsG.png",
                        CardItems = new List<CardItem>()
                        {
                            new CardItem()
                            {
                                ShortName = "metal.facemask",
                                MinAmount = 1,
                                MaxAmount = 1,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "metal.plate.torso",
                                MinAmount = 1,
                                MaxAmount = 1,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "roadsign.kilt",
                                MinAmount = 1,
                                MaxAmount = 1,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "tactical.gloves",
                                MinAmount = 1,
                                MaxAmount = 1,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "hoodie",
                                MinAmount = 1,
                                MaxAmount = 1,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "pants",
                                MinAmount = 1,
                                MaxAmount = 1,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "shoes.boots",
                                MinAmount = 1,
                                MaxAmount = 1,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            }
                        }
                    },
                    new CardSetting()
                    {
                        Type = CardType.Rare,
                        IsCase = false,
                        IsKit = true,
                        ImageURL = "https://i.imgur.com/ONzWs7c.png",
                        CardItems = new List<CardItem>()
                        {
                            new CardItem()
                            {
                                ShortName = "stones",
                                MinAmount = 500,
                                MaxAmount = 1000,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "wood",
                                MinAmount = 1000,
                                MaxAmount = 1500,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            },
                            new CardItem()
                            {
                                ShortName = "metal.fragments",
                                MinAmount = 1000,
                                MaxAmount = 1500,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            }
                        }
                    },
                    new CardSetting()
                    {
                        Type = CardType.Normal,
                        IsCase = false,
                        IsKit = false,
                        ImageURL = "",
                        CardItems = new List<CardItem>()
                        {
                            new CardItem()
                            {
                                ShortName = "stones",
                                MinAmount = 1000,
                                MaxAmount = 2000,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            }
                        }
                    },
                    new CardSetting()
                    {
                        Type = CardType.Normal,
                        IsCase = false,
                        IsKit = false,
                        ImageURL = "",
                        CardItems = new List<CardItem>()
                        {
                            new CardItem()
                            {
                                ShortName = "wood",
                                MinAmount = 1000,
                                MaxAmount = 2000,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            }
                        }
                    },
                    new CardSetting()
                    {
                        Type = CardType.Normal,
                        IsCase = false,
                        IsKit = false,
                        ImageURL = "",
                        CardItems = new List<CardItem>()
                        {
                            new CardItem()
                            {
                                ShortName = "sulfur",
                                MinAmount = 1000,
                                MaxAmount = 2000,
                                CustomName = "",
                                UIName = "",
                                ImageURL = "",
                                Command = "",
                            }
                        }
                    }
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

        void CheckConfigValue()
        {
            if (config.Cards.Any(x => x.CardID == 0))
            {
                foreach (var card in config.Cards)
                {
                    if (card.CardID == 0)
                        card.CardID = (ulong)rnd.Next(1111, 2000000);
                }
                SaveConfig();
            }
            if (config.CardCooldonw == 0)
            {
                config.CardCooldonw = 600;
                SaveConfig();
            }
        }

        SQLite sqlLisbrary;
        Connection sqlConnection;

        static string SqlDataFileName = "DailyCardData.db";
        static string SqlTable = "DailyCards";
        static string SqlBase = "playercarddata";

        void LoadConnection()
        {
            sqlLisbrary = Interface.GetMod().GetLibrary<Core.SQLite.Libraries.SQLite>();
            sqlConnection = sqlLisbrary.OpenDb(SqlDataFileName, this);
            if (sqlConnection == null)
            {
                PrintError("Couldn't open the SQLite DailyCard Base.");
                return;
            }

            var sql = $"CREATE TABLE IF NOT EXISTS `{SqlTable}`" +
                $"(`userID` bigint NOT NULL PRIMARY KEY, `CardIDs` text, `Opened` int NOT NULL, `OpenedToDay` text, `DailyCardList` text, `Day` int NOT NULL);";

            sqlLisbrary.Insert(Core.Database.Sql.Builder.Append(sql), sqlConnection, x =>
            {
                var data = DateTime.Parse(resetData.ResetDate);

                if (data <= DateTime.Now)
                    ResetUserBase();
            });
        }
        ResetData resetData;
        public class ResetData
        {
            public string ResetDate;
        }

        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name + "/ResetDate"))
                resetData = Interface.Oxide.DataFileSystem.ReadObject<ResetData>(Name + "/ResetDate");
            else
            {
                resetData = new ResetData();
                resetData.ResetDate = DateTime.Now.ToString();
                Interface.Oxide.DataFileSystem.WriteObject(Name + "/ResetDate", resetData);
            }
        }

        private void OnServerInitialized()
        {
            ins = this;
            LoadData();
            CheckConfigValue();
            LoadConnection();

            foreach (CardSetting img in config.Cards)
            {
                ImageLibrary.Call("AddImage", img.ImageURL, img.ImageURL);
                foreach (var img2 in img.CardItems)
                    ImageLibrary.Call("AddImage", img2.ImageURL, img2.ImageURL);
            }

            ImageLibrary.Call("AddImage", "https://i.imgur.com/mTfLh3i.png", "BackCard");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/tT1LmXT.png", "NormalCard");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/zW0ng9v.png", "RareCard");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/sUaYhWE.png", "LegendCard");
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);

            timer.Every(config.SyncTime, () => SincBase());

            timer.Every(60, ResetUserBase);

        }

        void Unload()
        {
            ins = null;
            rnd = null;

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "MainCardUI.BG");
                UnloadPlayerBase(player);
            }

        }

        void OnServerSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/ResetDate", resetData);
        }

        void OnNewSave()
        {

        }

        void ResetUserBase()
        {
            var date = DateTime.Parse(resetData.ResetDate);
            if (date > DateTime.Now) return;

            resetData.ResetDate = DateTime.Now.AddMinutes(config.CardCooldonw).ToString();
            var sql = $"UPDATE `{SqlTable}` SET OpenedToDay = '', Opened = '', Day = ''";
            sqlLisbrary.Insert(Core.Database.Sql.Builder.Append(sql), sqlConnection, x =>
            {
                foreach (var item in PlayerCard)
                {
                    item.Value.ResetUserBase();
                }
            });
        }

        public class OpenData
        {
            public int Opened = 0;
            public Dictionary<int, string> OpenedToDay = new Dictionary<int, string>();
            public List<string> Cards = new List<string>();
            public List<string> DailyList = new List<string>();
            public int Day = 0;

            public OpenData()
            {
                Opened = 0;
                OpenedToDay = new Dictionary<int, string>();
                Cards = new List<string>();
                DailyList = ins.GeneratedDailyList();
                Day = DateTime.Now.Day;
            }

            public OpenData(int opened, Dictionary<int, string> openToDay, List<string> cards, List<string> dailyList, int day)
            {
                Opened = opened;
                OpenedToDay = openToDay;
                Cards = cards;
                DailyList = dailyList;
                Day = day;

                if (Day != DateTime.Now.Day)
                {
                    Opened = 0;
                    OpenedToDay = new Dictionary<int, string>();
                    DailyList = ins.GeneratedDailyList();
                    Day = DateTime.Now.Day;
                }
            }

            public void ResetUserBase()
            {
                Opened = 0;
                OpenedToDay = new Dictionary<int, string>();
                DailyList = ins.GeneratedDailyList();
            }
        }

        private List<string> GeneratedDailyList()
        {
            var list = new List<string>();

            var lCount = config.MaxLegendaryCard;
            var rCount = config.MaxRareCard;
            var nCount = 12 - lCount - rCount;
            var legendList = config.Cards.Where(x => x.Type == CardType.Legendary).ToList();
            var rareList = config.Cards.Where(x => x.Type == CardType.Rare).ToList();
            var normalList = config.Cards.Where(x => x.Type == CardType.Normal).ToList();
            for (int i = 0; i < lCount; i++)
            {
                list.Add(legendList.GetRandom().CardID.ToString());
            }
            for (int i = 0; i < rCount; i++)
            {
                list.Add(rareList.GetRandom().CardID.ToString());
            }
            for (int i = 0; i < nCount; i++)
            {
                list.Add(normalList.GetRandom().CardID.ToString());
            }

            list.Shuffle((uint)UnityEngine.Random.Range(1, 999));

            return list;
        }

        Dictionary<BasePlayer, OpenData> PlayerCard = new Dictionary<BasePlayer, OpenData>();

        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                timer.In(1f, () => OnPlayerConnected(player));
                return;
            }

            LoadPlayerBase(player);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            UnloadPlayerBase(player);
        }

        void LoadPlayerBase(BasePlayer player)
        {
            sqlLisbrary.Query(Sql.Builder.Append($"SELECT * from {SqlTable} WHERE `userID` = '{player.userID}'"), sqlConnection, callBack =>
            {
                if (callBack != null && callBack.Count > 0)
                {
                    foreach (var data in callBack)
                    {
                        PlayerCard[player] = new OpenData(Convert.ToInt32(data["Opened"]), GetOpenedCards(data["OpenedToDay"] as string), GetCardIDs(data["CardIDs"] as string),
                            GetCardIDs(data["DailyCardList"] as string), Convert.ToInt32(data["Day"]));
                    }
                }
                else
                {
                    PlayerCard[player] = new OpenData();
                }
            });
        }

        void SincBase()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!PlayerCard.ContainsKey(player)) continue;

                var ids = string.Join(",", PlayerCard[player]);
                var opened = PlayerCard[player].Opened;
                var dayList = string.Join(",", PlayerCard[player].DailyList);
                string openToDay = "";
                foreach (var crd in PlayerCard[player].OpenedToDay)
                {
                    openToDay += $"{crd.Key}:{crd.Value},";
                }
                string sql = $"INSERT OR REPLACE INTO `{SqlTable}` VALUES ('{player.userID}', '{ids}', '{opened}', '{openToDay}', '{dayList}', '{PlayerCard[player].Day}');";
                sqlLisbrary.Insert(Core.Database.Sql.Builder.Append(sql), sqlConnection);
            }
        }

        void UnloadPlayerBase(BasePlayer player)
        {
            if (!PlayerCard.ContainsKey(player)) return;

            var ids = string.Join(",", PlayerCard[player].Cards);
            var opened = PlayerCard[player].Opened;
            var dayList = string.Join(",", PlayerCard[player].DailyList);
            string openToDay = "";
            foreach (var crd in PlayerCard[player].OpenedToDay)
            {
                openToDay += $"{crd.Key}:{crd.Value},";
            }
            string sql = $"INSERT OR REPLACE INTO `{SqlTable}` VALUES ('{player.userID}', '{ids}', '{opened}', '{openToDay}', '{dayList}', '{PlayerCard[player].Day}');";
            sqlLisbrary.Insert(Core.Database.Sql.Builder.Append(sql), sqlConnection);
            PlayerCard.Remove(player);
        }

        List<string> GetCardIDs(string ids)
        {
            var idList = new List<string>();
            if (string.IsNullOrEmpty(ids)) return idList;
            var reply = 0;
            var idMass = ids.Split(',');
            foreach (string id in idMass)
            {
                if (config.Cards.Any(x => x.CardID.ToString() == id))
                {
                    idList.Add(id);
                }
            }
            if (reply == 0) { }

            return idList;
        }

        Dictionary<int, string> GetOpenedCards(string cards)
        {
            var dict = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(cards)) return dict;
            var dictString = cards.Split(',');
            foreach (var card in dictString)
            {
                var keyValueString = card.Split(':');
                if (string.IsNullOrEmpty(keyValueString[0])) continue;
                var key = Convert.ToInt32(keyValueString[0]);
                if (string.IsNullOrEmpty(keyValueString[1])) continue;
                var value = keyValueString[1];
                if (config.Cards.Any(x => x.CardID.ToString() == value))
                    dict.Add(key, value);
            }
            return dict;
        }

        [ConsoleCommand("cards")]
        void DrawMainUI_cmd(ConsoleSystem.Arg arg) => DrawMainUI(arg.Player());

        [ChatCommand("cards")]
        void DrawMainUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var lg = lang.GetLanguage(player.UserIDString);

            UI.AddImage(ref container, "Overlay", "MainCardUI.BG", "0.141 0.137 0.109 0.95", "assets/content/ui/ui.background.tiletex.psd", "assets/content/ui/uibackgroundblur-ingamemenu.mat", "0 0", "1 1", "", "");
            UI.AddText(ref container, "MainCardUI.BG", "Header", "0.68 0.63 0.60 1", lg == "ru" ? $"ЕЖЕДНЕВНЫЕ КАРТОЧКИ" : "DAILY CARDS", TextAnchor.MiddleCenter, 38, "0.5 1", "0.5 1", "-250 -70", "250 -10");

            UI.AddButton(ref container, "MainCardUI.BG", "Storage.BTN", "opencardstorage", "", "0.28 0.32 0.17 1", "assets/content/ui/capsule-background.png", "", "0.15 0", "0.15 0", "-64 14", "64 52");
            UI.AddText(ref container, "Storage.BTN", "Storage.BTN.txt", "0.68 0.63 0.60 1", lg == "ru" ? $"СКЛАД" : "STORAGE", TextAnchor.MiddleCenter, 18, "0.5 0.5", "0.5 0.5", "-64 -15", "64 15");

            UI.AddButton(ref container, "MainCardUI.BG", "Close.BTN", "", "MainCardUI.BG", "0.441 0.237 0.209 1", "assets/icons/close.png", "", "1 1", "1 1", "-40 -40", "-14 -14");


            CuiHelper.DestroyUi(player, "MainCardUI.BG");
            CuiHelper.AddUi(player, container);
            DrawCardField(player);
            DrawCurrentOpening(player);
        }

        void DrawCurrentOpening(BasePlayer player)
        {
            var lg = lang.GetLanguage(player.UserIDString);
            var container = new CuiElementContainer();
            var opened = config.DailyOpenCard - PlayerCard[player].Opened;
            UI.AddText(ref container, "MainCardUI.BG", "OependCount", "0.68 0.63 0.60 1", lg == "ru" ? $"ДОСТУПНО ОТКРЫТИЙ: <color=#f44e42>{(opened >= 0 ? opened.ToString() : "0")}</color>" : $"AVAILABLE OPENING: <color=#f44e42>{(opened >= 0 ? opened.ToString() : "0")}</color>", TextAnchor.LowerCenter, 22, "0.5 0", "0.5 0", "-250 55", "250 85");
            if (opened <= 0)
            {
                UI.AddText(ref container, "OependCount", "OependCount.2", "0.68 0.63 0.60 1", lg == "ru" ? $"ОТКРЫТИЯ ЗАКОНЧИЛИСЬ, ПРИХОДИТЕ ЧЕРЕЗ: <color=#713D36FF>{(DateTime.Parse(resetData.ResetDate) - DateTime.Now).ToShortString()}</color>" : $"THE OPENINGS ARE OVER, COME BACK AFTER: <color=#713D36FF>{(DateTime.Parse(resetData.ResetDate) - DateTime.Now).ToShortString()}</color>", TextAnchor.LowerCenter, 16, "0.5 0", "0.5 0", "-250 -25", "250 -5");
                UI.AddText(ref container, "OependCount.2", "OependCount.3", "0.68 0.63 0.60 1", lg == "ru" ? $"<color=#f44e42>ПОЛУЧИТЬ ДОП ОТКРЫТИЯ ИЛИ СБРОСИТЬ ОТКРЫТЫЕ КАРТОЧКИ МОЖНО В МАГАЗИНЕ</color>" : $"<color=#f44e42>ADD MORE OPENINGS OR RESET OPENED CARDS CAN IN THE STORE</color>", TextAnchor.LowerCenter, 14, "0.5 0", "0.5 0", "-350 -25", "350 -5");
            }
            CuiHelper.DestroyUi(player, "OependCount");
            CuiHelper.AddUi(player, container);
        }

        void DrawCardField(BasePlayer player)
        {
            var container = new CuiElementContainer();
            UI.AddImage(ref container, "MainCardUI.BG", "CardField", "1 1 1 0", "", "", "0.5 0.5", "0.5 0.5", "-600 -300", "600 300");

            var sHorisontal = 0.1;
            var sVertical = 0.75;
            var vertical = sVertical;
            var horisontal = sHorisontal;
            int i = 0;
            var lg = lang.GetLanguage(player.UserIDString);
            foreach (var crd in PlayerCard[player].DailyList)
            {
                if (PlayerCard[player].OpenedToDay.ContainsKey(i))
                {
                    UI.AddButton(ref container, "CardField", $"Card.{i}", "", "", "0 0 0 0", "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", "-80 -120", "80 120");
                    var cardid = PlayerCard[player].OpenedToDay[i];
                    var card = config.Cards.FirstOrDefault(x => x.CardID.ToString() == cardid);
                    if (card != null)
                    {
                        var rare = card.Type == CardType.Legendary ? GetItemImage("LegendCard") : card.Type == CardType.Rare ? GetItemImage("RareCard") : GetItemImage("NormalCard");
                        UI.AddRawImage(ref container, $"Card.{i}", "CardImg", rare, "1 1 1 1", "", "", "0.5 0.5", "0.5 0.5", "-80 -120", "80 120");
                        UI.AddImage(ref container, "CardImg", "bluur", "0 0 0 .05", "", "assets/content/ui/uibackgroundblur.mat", "0 0", "1 1", "", "");
                        if (card.IsKit || card.IsCase)
                        {
                            if (!string.IsNullOrEmpty(card.ImageURL))
                            {
                                UI.AddRawImage(ref container, $"CardImg", "Card.item", GetItemImage(card.ImageURL), "1 1 1 1", "", "", "0.5 0.5", "0.5 0.5", "-55 -55", "55 55");
                            }
                        }
                        else
                        {
                            var item = card.CardItems.FirstOrDefault();
                            var def = ItemManager.FindItemDefinition(item.ShortName);
                            UI.AddItemImage(container, $"CardImg", "Card.item", "1 1 1 1", 1f, def.itemid, item.SkinID, "0.5 0.6", "0.5 0.6", "-55 -55", "55 55");
                            //UI.AddRawImage(ref container, $"CardImg", "Card.item", GetItemImage(item.ShortName, item.SkinID), "1 1 1 1", "", "", "0.5 0.6", "0.5 0.6", "-55 -55", "55 55");
                            if (!string.IsNullOrEmpty(item.CustomName))
                                UI.AddText(ref container, $"Card.item", "ItemName", "0.68 0.63 0.60 1", $"{item.CustomName}", TextAnchor.MiddleCenter, 22, "0.5 1", "0.5 1", "-70 5", "70 45");

                            UI.AddText(ref container, $"Card.item", "MaxAmount", "0.68 0.63 0.60 1", $"MAX: x{item.MaxAmount}", TextAnchor.MiddleRight, 12, "1 0", "1 0", "-140 -25", "0 -5", "0 0 0 1", "robotocondensed-regular.ttf", "0.5 0.5");
                            UI.AddText(ref container, $"MaxAmount", "MinAmount", "0.68 0.63 0.60 1", $"MIN: x{item.MinAmount}", TextAnchor.MiddleRight, 12, "1 0", "1 0", "-140 -25", "0 -5", "0 0 0 1", "robotocondensed-regular.ttf", "0.5 0.5");
                        }
                    }
                }
                else
                {
                    UI.AddButton(ref container, "CardField", $"Card.{i}", $"opencard {crd} {i}", "", "0 0 0 0", "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", "-80 -120", "80 120");
                    UI.AddRawImage(ref container, $"Card.{i}", "CardImg", GetItemImage("BackCard"), "1 1 1 1", "", "", "0.5 0.5", "0.5 0.5", "-80 -120", "80 120");
                }

                horisontal += 0.16f;
                if (horisontal > 0.95)
                {
                    horisontal = sHorisontal;
                    vertical -= 0.5f;
                }
                i++;
            }
            UI.AddButton(ref container, "MainCardUI.BG", "Storage.BTN", "opencardstorage", "", "0.28 0.32 0.17 1", "assets/content/ui/capsule-background.png", "", "0.15 0", "0.15 0", "-64 14", "64 52");
            UI.AddText(ref container, "Storage.BTN", "Storage.BTN.txt", "0.68 0.63 0.60 1", lg == "ru" ? $"СКЛАД" : "STORAGE", TextAnchor.MiddleCenter, 18, "0.5 0.5", "0.5 0.5", "-64 -15", "64 15");


            UI.AddText(ref container, "MainCardUI.BG", "Header", "0.68 0.63 0.60 1", lg == "ru" ? $"ЕЖЕДНЕВНЫЕ КАРТОЧКИ" : "DAILY CARDS", TextAnchor.MiddleCenter, 38, "0.5 1", "0.5 1", "-250 -70", "250 -10");

            CuiHelper.DestroyUi(player, "Header");
            CuiHelper.DestroyUi(player, "Storage.BTN");
            CuiHelper.DestroyUi(player, "CardField");
            CuiHelper.AddUi(player, container);

            DrawCurrentOpening(player);
        }

        void DrawStorage(BasePlayer player, int page)
        {
            var container = new CuiElementContainer();
            var sHorisontal = 0.1;
            var sVertical = 0.75;
            var vertical = sVertical;
            var horisontal = sHorisontal;
            var lg = lang.GetLanguage(player.UserIDString);
            UI.AddImage(ref container, "MainCardUI.BG", "CardField", "1 1 1 0", "", "", "0.5 0.5", "0.5 0.5", "-600 -300", "600 300");
            int i = page * 12;
            foreach (var crd in PlayerCard[player].Cards.Skip(12 * page).Take(12))
            {
                var card = config.Cards.FirstOrDefault(x => x.CardID.ToString() == crd);
                if (card != null)
                {
                    UI.AddButton(ref container, "CardField", $"Card.{i}", $"getcard {i} {page}", "", "0 0 0 0", "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", "-80 -120", "80 120");
                    var rare = card.Type == CardType.Legendary ? GetItemImage("LegendCard") : card.Type == CardType.Rare ? GetItemImage("RareCard") : GetItemImage("NormalCard");
                    UI.AddRawImage(ref container, $"Card.{i}", "CardImg", rare, "1 1 1 1", "", "", "0.5 0.5", "0.5 0.5", "-80 -120", "80 120");
                    UI.AddImage(ref container, "CardImg", "bluur", "0 0 0 .1", "", "assets/content/ui/uibackgroundblur.mat", "0 0", "1 1", "", "");
                    if (card.IsKit || card.IsCase)
                    {
                        if (!string.IsNullOrEmpty(card.ImageURL))
                        {
                            UI.AddRawImage(ref container, $"CardImg", "Card.item", GetItemImage(card.ImageURL), "1 1 1 1", "", "", "0.5 0.5", "0.5 0.5", "-55 -55", "55 55");
                        }
                    }
                    else
                    {
                        var item = card.CardItems.FirstOrDefault();
                        var def = ItemManager.FindItemDefinition(item.ShortName);
                        UI.AddItemImage(container, $"CardImg", "Card.item", "1 1 1 1", 1f, def.itemid, item.SkinID, "0.5 0.6", "0.5 0.6", "-55 -55", "55 55");
                        //UI.AddRawImage(ref container, $"CardImg", "Card.item", GetItemImage(item.ShortName, item.SkinID), "1 1 1 1", "", "", "0.5 0.6", "0.5 0.6", "-55 -55", "55 55");
                        if (!string.IsNullOrEmpty(item.CustomName))
                            UI.AddText(ref container, $"Card.item", "ItemName", "0.68 0.63 0.60 1", $"{item.CustomName}", TextAnchor.MiddleCenter, 22, "0.5 1", "0.5 1", "-70 5", "70 45");

                        UI.AddText(ref container, $"Card.item", "MaxAmount", "0.68 0.63 0.60 1", $"MAX: x{item.MaxAmount}", TextAnchor.MiddleRight, 12, "1 0", "1 0", "-140 -25", "0 -5", "0 0 0 1", "robotocondensed-regular.ttf", "0.5 0.5");
                        UI.AddText(ref container, $"MaxAmount", "MinAmount", "0.68 0.63 0.60 1", $"MIN: x{item.MinAmount}", TextAnchor.MiddleRight, 12, "1 0", "1 0", "-140 -25", "0 -5", "0 0 0 1", "robotocondensed-regular.ttf", "0.5 0.5");
                    }
                    if (card.IsCase || card.IsKit)
                        UI.AddButton(ref container, "CardImg", $"CardImfo.Btn.{i}", $"cardinfo {i}", "", card.Type == CardType.Legendary ? "0 0 0 1" : "1 1 1 1", "assets/icons/examine.png", "", $"1 1", $"1 1", "-35 -35", "-5 -5");

                    horisontal += 0.16f;
                    if (horisontal > 0.95)
                    {
                        horisontal = sHorisontal;
                        vertical -= 0.5f;
                    }
                }

                i++;
            }

            if (page > 0)
            {
                UI.AddButton(ref container, "CardField", "BackBtn", $"cardpage {page - 1}", "", "0.28 0.32 0.17 1", "assets/content/ui/capsule-background.png", "", "0 0", "0 0", "0 -30", "70 -5");
                UI.AddText(ref container, $"BackBtn", "Text", "0.68 0.63 0.60 0.50", "◄", TextAnchor.MiddleCenter, 24, "0.5 0.5", "0.5 0.5", "-30 -15", "30 15");
            }
            if (PlayerCard[player].Cards.Skip(page * 12).Count() > 12)
            {
                UI.AddButton(ref container, "CardField", "BackBtn", $"cardpage {page + 1}", "", "0.28 0.32 0.17 1", "assets/content/ui/capsule-background.png", "", "1 0", "1 0", "-70 -30", "0 -5");
                UI.AddText(ref container, $"BackBtn", "Text", "0.68 0.63 0.60 0.50", "►", TextAnchor.MiddleCenter, 24, "0.5 0.5", "0.5 0.5", "-30 -15", "30 15");
            }

            UI.AddButton(ref container, "MainCardUI.BG", "Storage.BTN", "backtocards", "", "0.341 0.137 0.109 0.8", "assets/content/ui/capsule-background.png", "", "0.15 0", "0.15 0", "-64 14", "64 52");
            UI.AddText(ref container, "Storage.BTN", "Storage.BTN.txt", "0.68 0.63 0.60 1", lg == "ru" ? $"НАЗАД" : "BACK", TextAnchor.MiddleCenter, 18, "0.5 0.5", "0.5 0.5", "-64 -15", "64 15");

            UI.AddText(ref container, "MainCardUI.BG", "Header", "0.68 0.63 0.60 1", lg == "ru" ? $"ВАШ СКЛАД" : "STORAGE", TextAnchor.MiddleCenter, 38, "0.5 1", "0.5 1", "-250 -70", "250 -10");

            CuiHelper.DestroyUi(player, "Header");
            CuiHelper.DestroyUi(player, "OependCount");
            CuiHelper.DestroyUi(player, "Storage.BTN");
            CuiHelper.DestroyUi(player, "CardField");
            CuiHelper.AddUi(player, container);
        }

        void DrawCardInfo(BasePlayer player, CardSetting card)
        {
            var container = new CuiElementContainer();
            var lg = lang.GetLanguage(player.UserIDString);
            UI.AddButton(ref container, "MainCardUI.BG", "CardInfo.BG", "", "CardInfo.BG", "0 0 0 0.5", "", "assets/content/ui/uibackgroundblur.mat", "0 0", "1 1", "", "");
            var panelHeight = 32f;
            var multiplauer = (float)Math.Ceiling(card.CardItems.Count / 6f);
            if (multiplauer >= 2f)
                panelHeight = 35f;
            panelHeight *= multiplauer;

            UI.AddImage(ref container, "CardInfo.BG", "CardInfo.Field", "1 1 1 0.3", "", "", "0.5 0.5", "0.5 0.5", $"-210 -{panelHeight}", $"210 {panelHeight}");
            UI.AddText(ref container, "CardInfo.Field", "CardInfo.Hedaer", "0.68 0.63 0.60 1", lg == "ru" ? $"{(card.IsCase ? "СЛУЧАЙНЫЙ ПРЕДМЕТ" : "НАБОР ПРЕДМЕТОВ")}" : $"{(card.IsCase ? "RANDOM ITEM" : "SET OF ITEMS")}", TextAnchor.MiddleCenter, 32, "0.5 1", "0.5 1", "-250 5", "250 55");
            var sHorisontal = 0f;
            var sVertical = 1f;
            var vertical = sVertical;
            var horisontal = sHorisontal;
            float gapH = 0;
            float gapV = 0;

            foreach (var it in card.CardItems)
            {
                if (!string.IsNullOrEmpty(it.ImageURL))
                {
                    UI.AddRawImage(ref container, "CardInfo.Field", "CardInfo.item", $"{(string.IsNullOrEmpty(it.ImageURL) ? GetItemImage(it.ShortName, it.SkinID) : GetItemImage(it.ImageURL))}", "1 1 1 1", "", "", $"{horisontal} {vertical}", $"{horisontal} {vertical}", $"{gapH} -{64 + gapV}", $"{64 + gapH} -{gapV}");
                }
                else
                {
                    var def = ItemManager.FindItemDefinition(it.ShortName);
                    UI.AddItemImage(container, $"CardInfo.Field", "CardInfo.item", "1 1 1 1", 1f, def.itemid, it.SkinID, $"{horisontal} {vertical}", $"{horisontal} {vertical}", $"{gapH} -{64 + gapV}", $"{64 + gapH} -{gapV}");
                }

                if (it.MaxAmount > it.MinAmount)
                    UI.AddText(ref container, "CardInfo.item", "CardInfo.maxmin", "1 1 1 1", $"MIN: x{it.MinAmount}\nMAX: x{it.MaxAmount}", TextAnchor.LowerRight, 10, "0 0", "1 1", "", "", "0 0 0 1", "robotocondensed-regular.ttf", "0.1 0.1");
                else
                    UI.AddText(ref container, "CardInfo.item", "CardInfo.maxmin", "1 1 1 1", $"x{it.MinAmount}", TextAnchor.LowerRight, 10, "0 0", "1 1", "", "", "0 0 0 1", "robotocondensed-regular.ttf", "0.1 0.1");

                gapH += 70;
                if (gapH >= 420)
                {
                    gapH = 0;
                    gapV += 70;
                }

            }

            CuiHelper.DestroyUi(player, "CardInfo.BG");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("cardinfo")]
        void CardInfo(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var index = arg.GetInt(0);
            var cardID = PlayerCard[player].Cards[index];
            var card = config.Cards.FirstOrDefault(x => x.CardID.ToString() == cardID);
            DrawCardInfo(player, card);
        }

        [ConsoleCommand("getcard")]
        void GetCard(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var index = arg.GetInt(0);
            var page = arg.GetInt(1);
            var cardID = PlayerCard[player].Cards[index];
            var card = config.Cards.FirstOrDefault(x => x.CardID.ToString() == cardID);
            PlayerCard[player].Cards.RemoveAt(index);
            var items = card.GetItems();

            if (page > 0 && PlayerCard[player].Cards.Skip(page * 12).Count() <= 0)
            {
                page--;
            }
            DrawStorage(player, page);

            foreach (var it in items)
            {
                if (!string.IsNullOrEmpty(it.Command))
                {
                    string cmd = "‌‌﻿‌‍‌​";
                    cmd = it.Command.Replace("%STEAMID%", player.UserIDString);
                    rust.RunServerCommand(cmd);
                }
                if (!string.IsNullOrEmpty(it.ShortName))
                {
                    var amount = UnityEngine.Random.Range(it.MinAmount, it.MaxAmount + 1);
                    Item item = ItemManager.CreateByName(it.ShortName, amount, it.SkinID);
                    player.GiveItem(item);
                }
            }

        }

        [ConsoleCommand("opencard")]
        void OpenCard_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (PlayerCard[player].Opened >= config.DailyOpenCard) return;
            PlayerCard[player].Opened++;
            var cardID = arg.Args[0];
            var index = arg.GetInt(1);
            var card = config.Cards.FirstOrDefault(x => x.CardID.ToString() == cardID);
            PlayerCard[player].OpenedToDay.Add(index, cardID);
            PlayerCard[player].Cards.Add(cardID);
            DrawCardField(player);
            EffectNetwork.Send(new Effect(card.Type == CardType.Legendary ? "assets/prefabs/misc/halloween/lootbag/effects/gold_open.prefab" : "assets/prefabs/misc/easter/painted eggs/effects/gold_open.prefab", player.transform.position, Vector3.zero), player.net.connection);
        }

        [ConsoleCommand("backtocards")]
        void BackToCards(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DrawCardField(player);
        }

        [ConsoleCommand("opencardstorage")]
        void OpenCardStorage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            DrawStorage(player, 0);
        }

        [ConsoleCommand("cardpage")]
        void CardPage_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var page = arg.GetInt(0);
            DrawStorage(player, page);
        }

        [ConsoleCommand("addopentry")]
        void AddOpenTry(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;
            if (arg.Args == null || arg.Args.Length < 2)
            {
                Puts("Use: addopentry STEAMID count");
                return;
            }
            var userID = arg.GetUInt64(0);
            var count = arg.GetInt(1);
            var player = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == userID);
            PlayerCard[player].Opened -= count;
            var lg = lang.GetLanguage(player.UserIDString);
            var opened = config.DailyOpenCard - PlayerCard[player].Opened;
            SendReply(player, lg == "ru" ? $"Вы получили {count} доп. открытие карточек.\nДоступно открытий: <color=#f44e42>{(opened >= 0 ? opened.ToString() : "0")}</color>\nВведите /cards чтобы открыть карточки." : $"You have received {count} additional card opening.\nAvailable opening: <color=#f44e42>{(opened >= 0 ? opened.ToString() : "0")}</color>\nEnter /cards to open the cards.");
        }

        [ConsoleCommand("resetcard")]
        void ResetCard(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                Puts("Use: resetcard STEAMID");
                return;
            }
            var userID = arg.GetUInt64(0);
            var player = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == userID);

            PlayerCard[player].DailyList = GeneratedDailyList();
            PlayerCard[player].OpenedToDay = new Dictionary<int, string>();

            var lg = lang.GetLanguage(player.UserIDString);
            var opened = config.DailyOpenCard - PlayerCard[player].Opened;
            SendReply(player, lg == "ru" ? $"Вы сбросили открытые за сегодня карточки.\nВведите /cards чтобы открыть карточки." : $"You have discarded the cards you opened today.\nEnter /cards to open the cards.");
        }


        [ConsoleCommand("fullreset")]
        void ResetCardAndOpenings(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                Puts("Use: fullreset STEAMID");
                return;
            }
            var userID = arg.GetUInt64(0);
            var player = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == userID);

            PlayerCard[player].Opened = 0;
            PlayerCard[player].DailyList = GeneratedDailyList();
            PlayerCard[player].OpenedToDay = new Dictionary<int, string>();

            var lg = lang.GetLanguage(player.UserIDString);
            var opened = config.DailyOpenCard - PlayerCard[player].Opened;
            SendReply(player, lg == "ru" ? $"Вы выполнили полный сброс карточек за сегодня.\nДоступно открытий: <color=#f44e42>{(opened >= 0 ? opened.ToString() : "0")}</color>\nВведите /cards чтобы открыть карточки." : $"You have completed a full card reset for today.\nAvailable opening: <color=#f44e42>{(opened >= 0 ? opened.ToString() : "0")}</color>\nEnter /cards to open the cards.");
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

            public static void AddItemImage(CuiElementContainer container, string parrent, string name, string color, float alpha, int ItemId, ulong skinID = 0, string aMin = "0 0", string aMax = "1 1", string oMin = "", string oMax = "")
            {
                container.Add(new CuiElement()
                {
                    Parent = parrent,
                    Name = name,
                    Components =
                        {
                            new CuiImageComponent{Color = color, SkinId = skinID, ItemId = ItemId },
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

        [PluginReference] Plugin ImageLibrary;

        public string GetItemImage(string shortname, ulong skinID = 0, ulong playerid = 0)
        {
            if (skinID > 0)
            {
                if (ImageLibrary.Call<bool>("HasImage", shortname, skinID) == false && ImageLibrary.Call<Dictionary<string, object>>("GetSkinInfo", shortname, skinID) == null)
                {

                    webrequest.Enqueue("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", $"key={config.SteamAPIKey}&itemcount=1&publishedfileids%5B0%5D={skinID}", (code, response) =>
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
    }
}
