
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
        [Info("TPCases", "rustmods.ru", "2.1.7‌‌​‍‍")]
        public class TPCases : RustPlugin
        {
            static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
            static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;

            private string LStorage = "storage";

            [JsonProperty("Изображения плагина")]
            private Dictionary<string, string> PluginImages = new Dictionary<string, string>
            {
                ["0Bag"] = "https://i.imgur.com/kW6iVdn.png",
                ["1Bag"] = "https://i.imgur.com/65eSjiO.png",
                ["2Bag"] = "https://i.imgur.com/nmrGCON.png",
                ["3Bag"] = "https://i.imgur.com/dEeCJst.png"
            };
            [PluginReference] private Plugin ImageLibrary;
            #region Init
            public class ItemsData
            {
                public List<ItemData> CheckItemData = new List<ItemData>();

                public class ItemData
                {
                    public string ShortName { get; set; }
                    public string Url { get; set; }
                    public string Command { get; set; }
                    public ulong SkinID { get; set; }
                    public int AmountDrop { get; set; }

                    public ItemData(string shortname = "", string Url = null, string Command = null, ulong SkinID = 0, int amountdrop = 0)
                    {
                        this.ShortName = shortname;
                        this.Url = Url;
                        this.Command = Command;
                        this.SkinID = SkinID;
                        this.AmountDrop = amountdrop;
                    }
                }
            }


            void Data()
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile("Case/PlayerAmount"))
                {
                    CheckFileDrop = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ItemsData>>("Case/PlayerAmount");
                }
                else
                {
                    CheckFileDrop = new Dictionary<ulong, ItemsData>();
                }
                if (Interface.Oxide.DataFileSystem.ExistsDatafile("Case/PlayerTimeOut"))
                {
                PlayerTimeOut = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DataList>>("Case/PlayerTimeOut");
                }
                else
                {
                PlayerTimeOut = new Dictionary<ulong, DataList>();
                }
            }

            void OnServerInitialized()
            {
            foreach (var check in PluginImages)
                {
                    ImageLibrary.Call("AddImage", check.Value, check.Key);
                }
                foreach (var check in Settings.ListDrop)
                { 
                    ImageLibrary.Call("AddImage", check.Url, check.Url);
                }
                foreach (var check in Settings.ListDropTo)
                { 
                    ImageLibrary.Call("AddImage", check.Url, check.Url);
                }
                foreach (var check in Settings.ListDropFree)
                { 
                    ImageLibrary.Call("AddImage", check.Url, check.Url);
                }
                foreach (var check in Settings.ListDropFoo)
                { 
                    ImageLibrary.Call("AddImage", check.Url, check.Url);
                }
                ImageLibrary.Call("AddImage", "https://i.imgur.com/KccfOc2.png", "fonItems");
                ImageLibrary.Call("AddImage", "https://i.imgur.com/kKhxfm0.png", "backgroundFon");
                ImageLibrary.Call("AddImage", "https://i.imgur.com/ZDeku17.png", "fonCases");
                ImageLibrary.Call("AddImage", "https://i.imgur.com/uKQLLMZ.png", "fonCasesINV");
                ImageLibrary.Call("AddImage", "https://i.imgur.com/4fd4gCh.png", "openCase");
                ImageLibrary.Call("AddImage", "https://imgur.com/EF2INiq.png", "imagecasexp");
                timer.Every(60, OnServerSave);
                Data();
                BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);
                            PrintWarning("\n-----------------------------------------------------------------------------------------\n" +
                "     Загрузка плагина...\n" +
                "     Этот плагин был скачан с канала Discord [Rust Plugin Sliv]\n" +
                "     Этот плагин исправлен Инкубом под заказ [Rust Plugin Sliv]\n" +
                "     DISCORD: https://discord.gg/pFgKw6Dyyq\n" +
                "     Приятного использования!\n" +
                "-----------------------------------------------------------------------------------------");
            }
            void OnPlayerConnected(BasePlayer player)
            {
                if (!CheckFileDrop.ContainsKey(player.userID))
                {
                    ItemsData check = new ItemsData() { CheckItemData = new List<ItemsData.ItemData> { } };
                    CheckFileDrop.Add(player.userID, check);
                }
                if (!PlayerTimeOut.ContainsKey(player.userID))
                {
                    DataList data = new DataList()
                    {
                        foocase = 0,
                        onecase = 0,
                        tocase = 0,
                        freecase = 0,
                        level = 0,
                        xp = 0
                    };
                    PlayerTimeOut.Add(player.userID, data);
                }

                InterfaceXP(player);
            }

            void Unload()
            {
                OnServerSave();
            }
            void OnServerSave()
            {
                Interface.Oxide.DataFileSystem.WriteObject("Case/PlayerAmount", CheckFileDrop);
                Interface.Oxide.DataFileSystem.WriteObject("Case/PlayerTimeOut", PlayerTimeOut);
             }
            public Dictionary<ulong, ItemsData> CheckFileDrop = new Dictionary<ulong, ItemsData>();
            public Dictionary<ulong, bool> EnableUI = new Dictionary<ulong, bool>();

            #endregion

            #region Config

            private ConfigData Settings { get; set; }


            public class Itemss
            {
                [JsonProperty("Предмет из игры(shortname)")] public string ShortName;
                [JsonProperty("Изображение")] public string Url;
                [JsonProperty("Команда")] public string Command;
                [JsonProperty("Скин")] public ulong SkinID;
                [JsonProperty("мин кол-во предмета")] public int MinDrop;
                [JsonProperty("макс кол-во предмета")] public int MaxDrop;
            }

            class ConfigData
            {
                [JsonProperty("Включить поддержку плагина скилл")]
                public bool Enable = true;
                [JsonProperty("Очищать ли склад предметов у игрока после вайпа?")]
                public bool ClearSklad = true;
                [JsonProperty("Описание плагина кейсов")]
                public string Desk = "Ахуенные кейсы, всем желаю преобрести и установить на свой супер пупер ахуенный сервер!";
                [JsonProperty("Очищать ли кулдавн игрока после вайпа?")]
                public bool ClearTime = true;
                [JsonProperty("Сколько выдавать очков xp за добычу ресурсов")]
                public double XpBonusGather = 1;
                [JsonProperty("Сколько выдавать очков xp за убийство животных")]
                public double XpKillAnimal = 3;
                [JsonProperty("Сколько выдавать очков xp за убийство игрока")]
                public double XpKillPlayer = 10;
                [JsonProperty("Сколько выдавать очков xp за убийство игрока в голову")]
                public double XpKillPlayerHead = 15;
                [JsonProperty("Сколько выдавать очков xp за сбитие вертолета")]
                public double XpKillHeli = 50;
                [JsonProperty("Сколько выдавать очков xp за разбитие бочек")]
                public double XpKillBarrel = 5;
                [JsonProperty("Сколько выдавать очков xp за уничтожении танка")]
                public double XpKillBradley = 100;
                [JsonProperty("Сколько давать кулдауна после открытия первого кейса?(в секундах)")]
                public double TimeOne = 150;
                [JsonProperty("Сколько давать кулдауна после открытия второго кейса?(в секундах)")]
                public double TimeTo = 150;
                [JsonProperty("Сколько давать кулдауна после открытия третьего кейса?(в секундах)")]
                public double TimeFree = 150;
                [JsonProperty("Сколько давать кулдауна после открытия четвертого кейса?(В секундах)")]
                public double TimeFoo = 150;
                [JsonProperty("Описание для первого кейса!")]
                public string DescOne = "Это первый кейс, он легкий, и тут конечно же будет мало ресурсов!";
                [JsonProperty("Описание для второй кейса!")]
                public string DescTo = "Это второй кейс, он получше, и тут конечно же будет побольше ресурсов";
                [JsonProperty("Описание для третьего кейса!")]
                public string DescFree = "Это третий кейс, он чем 2 предыдущих, и тут конечно же будет побольше, чем у предыдущих";
                [JsonProperty("Описание для четвертого кейса!")]
                public string DescFoo = "Это четвертый кейс!, он самый жесткий, тут может выпасть все что угодно!";
                [JsonProperty("Настройка предметов для первого кейса")]
                public List<Itemss> ListDrop { get; set; }
                [JsonProperty("Настройка предметов для второго кейса")]
                public List<Itemss> ListDropTo { get; set; }
                [JsonProperty("Настройка предметов для третьего кейса")]
                public List<Itemss> ListDropFree { get; set; }
                [JsonProperty("Настройка предметов для четвертого кейса")]
                public List<Itemss> ListDropFoo { get; set; }


                public static ConfigData GetNewCong()
                {
                    ConfigData newConfig = new ConfigData();

                    newConfig.ListDrop = new List<Itemss>
                {
                    new Itemss()
                    {
                        ShortName = "rifle.ak",
                        Url = null,
                        Command = null,
                        SkinID = 0,
                        MinDrop = 1,
                        MaxDrop = 2,
                    },
                    new Itemss()
                    {
                        ShortName = null,
                        Url = "https://i.imgur.com/KccfOc2.png",
                        Command = "хуита",
                        SkinID = 0,
                        MinDrop = 1,
                        MaxDrop = 2,
                    }
                };
                    newConfig.ListDropTo = new List<Itemss>
                {
                    new Itemss()
                    {
                        ShortName = "rifle.ak",
                        Url = null,
                        Command = null,
                        SkinID = 0,
                        MinDrop = 1,
                        MaxDrop = 2,
                    },
                    new Itemss()
                    {
                        ShortName = "wood",                        
                        Url = null,
                        Command = null,
                        SkinID = 0,
                        MinDrop = 1,
                        MaxDrop = 2,
                    },
                    new Itemss()
                    {
                        ShortName = null,
                        Url = "https://i.imgur.com/KccfOc2.png",
                        Command = "хуита",
                        SkinID = 0,
                        MinDrop = 1,
                        MaxDrop = 2,
                    }
                };
                    newConfig.ListDropFree = new List<Itemss>
                {
                    new Itemss()
                    {
                        ShortName = "rifle.ak",                        
                        Url = null,
                        Command = null,
                        SkinID = 0,
                        MinDrop = 1,
                        MaxDrop = 2,
                    },
                    new Itemss()
                    {
                        ShortName = "wood",                        
                        Url = null,
                        Command = null,
                        SkinID = 0,
                        MinDrop = 1,
                        MaxDrop = 2,
                    }
                };
                    newConfig.ListDropFoo = new List<Itemss>
                {
                    new Itemss()
                    {
                        ShortName = "rifle.ak",                        
                        Url = null,
                        Command = null,
                        SkinID = 0,
                        MinDrop = 1,
                        MaxDrop = 2,
                    },
                    new Itemss()
                    {
                        ShortName = "wood",                        
                        Url = null,
                        Command = null,
                        SkinID = 0,
                        MinDrop = 1,
                        MaxDrop = 2,
                    }
                };
                    return newConfig;
                }
            }

            protected override void LoadConfig()
            {
                base.LoadConfig();
                try
                {
                    Settings = Config.ReadObject<ConfigData>();
                    if (Settings?.ListDrop == null)  LoadDefaultConfig();
                    if (Settings?.ListDropFoo == null) LoadDefaultConfig();
                    if (Settings?.ListDropFree == null) LoadDefaultConfig();
                    if (Settings?.ListDropTo == null) LoadDefaultConfig();
                }
                catch
                {
                    LoadDefaultConfig();
                }

                NextTick(SaveConfig);
            }
            
            void OnNewSave() 
            {
                if (Settings.ClearSklad)
                {
                    CheckFileDrop?.Clear();
                    PrintWarning("Замечен вайп! Очещаем склад игроков!");
                }
                if (Settings.ClearTime)
                {
                    PrintWarning("Замечен вайп! Очищаем кулдавн игроков!");
                    PlayerTimeOut?.Clear();
                }
            }

            protected override void LoadDefaultConfig() => Settings = ConfigData.GetNewCong();
            protected override void SaveConfig() => Config.WriteObject(Settings);

            #endregion

            class DataList
            {
                [JsonProperty("Кулдаун первого кейса")]
                public double onecase { get; set; }
                [JsonProperty("Кулдаун второго кейса!")]
                public double tocase { get; set; }
                [JsonProperty("Кулдаун третьего кейса!")]
                public double freecase { get; set; }
                [JsonProperty("Кулдаун четвертого кейса!")]
                public double foocase { get; set; }
                [JsonProperty("Уровень")]
                public double level { get; set; }
                [JsonProperty("XP игрока")]
                public double xp { get; set; }
            }

        private Dictionary<ulong, DataList> PlayerTimeOut = new Dictionary<ulong, DataList>();

        [ConsoleCommand("openDesc")]
        private void cmdOPenDesc(ConsoleSystem.Arg Args)
        {
            BasePlayer player = Args.Player();
            DescriptionUi(player, Convert.ToInt32(Args.Args[0]));
        }

            [ConsoleCommand("opencase")]
            void giveopencaseitems(ConsoleSystem.Arg args)
            {
                BasePlayer player = args.Player();
                var container = new CuiElementContainer();
                if (player != null && args.HasArgs(1))
                {
                    if (args.Args[0] == "one")
                    {
                        if (CheckFileDrop[player.userID].CheckItemData.Count >= 18)
                        {
                            SendReply(player, "У вас заполнен склад!");
                            return;
                        }
                        var check = PlayerTimeOut[player.userID].onecase - CurrentTime();
                        var timecheck = TimeSpan.FromSeconds(check).ToShortString();

                        if (PlayerTimeOut[player.userID].level < 5) {
                            SendReply(player, "Ваш уровень слишком низок!" + $"\nТребуется уровень 5");
                            return;
                        }

                        if (check > 0)
                        {
                            SendReply(player, "У вас еще откат кейса!" + $"\nПодождите {timecheck}");
                            return;
                        }
                        if (check <= 0)
                        {

                            PlayerTimeOut[player.userID].onecase = 0;
                                var count = Settings.ListDrop.ElementAt(UnityEngine.Random.Range(0, Settings.ListDrop.Count));
                                var kolvo = UnityEngine.Random.Range(count.MinDrop, count.MaxDrop);
                                var add = CheckFileDrop[player.userID].CheckItemData;
                                add.Add(new ItemsData.ItemData
                                {
                                    ShortName = count.ShortName,
                                    Url = count.Url,
                                    Command = count.Command,
                                    SkinID = count.SkinID,
                                    AmountDrop = kolvo,
                                });

                                container.Add(new CuiElement
                                {
                                    Parent = Layer + ".Main" + $"Case{0}",
                                    Components = 
                                    {
                                        new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "openCase"), Color = "1 1 1 1" },
                                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                                    }
                                });
                                CuiHelper.AddUi(player, container);
                                Effect.server.Run( "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab",player.transform.position);
                                if (count.Command != null) {
                                    SendReply(player, $"<color=#e5a779><size=15>Вы успешно открыли кейс</size></color>" + $"\n<color=#b3a1fc><size=15>Вам выпала команда</size></color>");
                                }
                                else {
                                    SendReply(player, $"<color=#e5a779><size=15>Вы успешно открыли кейс</size></color>" + $"\n<color=#b3a1fc><size=15>Вам выпало {ItemManager.FindItemDefinition(count.ShortName).displayName.english} в количестве {kolvo}</size></color>");
                                }
                                PlayerTimeOut[player.userID].onecase += CurrentTime() + Settings.TimeOne;
                            }
                    }
                    else if (args.Args[0] == "too")
                    {
                        if (CheckFileDrop[player.userID].CheckItemData.Count >= 18)
                        {
                        SendReply(player, "У вас заполнен склад!");
                        return;
                        }
                        var check = PlayerTimeOut[player.userID].tocase - CurrentTime();
                        var timechek = TimeSpan.FromSeconds(check).ToShortString();

                        if (PlayerTimeOut[player.userID].level < 10) {
                            SendReply(player, "Ваш уровень слишком низок!" + $"\nТребуется уровень 10");
                            return;
                        }

                        if (check > 0)
                        {
                        SendReply(player, "У вас еще откат кейса!" + $"\nПодождите {timechek}");
                        return;
                        }
                        if (check <= 0)
                        {
                            PlayerTimeOut[player.userID].tocase = 0;
                        var count = Settings.ListDropTo.ElementAt(UnityEngine.Random.Range(0, Settings.ListDropTo.Count));
                        var kolvo = UnityEngine.Random.Range(count.MinDrop, count.MaxDrop);
                        var add = CheckFileDrop[player.userID].CheckItemData;
                        add.Add(new ItemsData.ItemData
                        {
                            ShortName = count.ShortName,
                            Url = count.Url,
                            Command = count.Command,
                            SkinID = count.SkinID,
                            AmountDrop = kolvo,
                        });
                        container.Add(new CuiElement
                        {
                            Parent = Layer + ".Main" + $"Case{1}",
                            Components = 
                            {
                                new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "openCase"), Color = "1 1 1 1" },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                            }
                        });
                        CuiHelper.AddUi(player, container);
                        Effect.server.Run("assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab",player.transform.position);
                        if (count.Command != null) {
                            SendReply(player, $"<color=#e5a779><size=15>Вы успешно открыли кейс</size></color>" + $"\n<color=#b3a1fc><size=15>Вам выпала команда</size></color>");
                        }
                        else {
                            SendReply(player, $"<color=#e5a779><size=15>Вы успешно открыли кейс</size></color>" + $"\n<color=#b3a1fc><size=15>Вам выпало {ItemManager.FindItemDefinition(count.ShortName).displayName.english} в количестве {kolvo}</size></color>");
                        }
                        PlayerTimeOut[player.userID].tocase += CurrentTime() + Settings.TimeTo;
                        }
                    }
                    else if (args.Args[0] == "free")
                    {
                        if (CheckFileDrop[player.userID].CheckItemData.Count >= 18)
                        {
                            SendReply(player, "У вас заполнен склад!");
                            return;
                        }
                        var check = PlayerTimeOut[player.userID].freecase - CurrentTime();
                        var timecheck = TimeSpan.FromSeconds(check).ToShortString();

                        if (PlayerTimeOut[player.userID].level < 15) {
                            SendReply(player, "Ваш уровень слишком низок!" + $"\nТребуется уровень 15");
                            return;
                        }

                        if (check > 0)
                        {
                            SendReply(player, "У вас еще откат кейса!" + $"\nПодождите {timecheck}");
                        }

                        if (check <= 0)
                        {
                            PlayerTimeOut[player.userID].freecase = 0;
                            var count = Settings.ListDropFree.ElementAt(UnityEngine.Random.Range(0, Settings.ListDropFree.Count));
                            var kolvo = UnityEngine.Random.Range(count.MinDrop, count.MaxDrop);
                            var add = CheckFileDrop[player.userID].CheckItemData;
                            add.Add(new ItemsData.ItemData
                            {
                                ShortName = count.ShortName,
                                Url = count.Url,
                                Command = count.Command,
                                SkinID = count.SkinID,
                                AmountDrop = kolvo,
                            });
                            container.Add(new CuiElement
                            {
                                Parent = Layer + ".Main" + $"Case{2}",
                                Components = 
                                {
                                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "openCase"), Color = "1 1 1 1" },
                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                                }
                            });
                            CuiHelper.AddUi(player, container);
                            Effect.server.Run("assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab",player.transform.position);
                            if (count.Command != null) {
                                SendReply(player, $"<color=#e5a779><size=15>Вы успешно открыли кейс</size></color>" + $"\n<color=#b3a1fc><size=15>Вам выпала команда</size></color>");
                            }
                            else {
                                SendReply(player, $"<color=#e5a779><size=15>Вы успешно открыли кейс</size></color>" + $"\n<color=#b3a1fc><size=15>Вам выпало {ItemManager.FindItemDefinition(count.ShortName).displayName.english} в количестве {kolvo}</size></color>");
                            }                            
                            PlayerTimeOut[player.userID].freecase += CurrentTime() + Settings.TimeFree;
                        }
                    }
                    else if (args.Args[0] == "foo")
                    {
                        if (CheckFileDrop[player.userID].CheckItemData.Count >= 18)
                        {
                            SendReply(player, "У вас заполнен склад!");
                            return;
                        }
                        var check = PlayerTimeOut[player.userID].foocase - CurrentTime();
                        var timecheck = TimeSpan.FromSeconds(check).ToShortString();

                        if (PlayerTimeOut[player.userID].level < 20) {
                            SendReply(player, "Ваш уровень слишком низок!" + $"\nТребуется уровень 20");
                            return;
                        }

                        if (check > 0 )
                        {
                            SendReply(player, "У вас еще откат кейса!" + $"\nПодождите {timecheck}");
                            return;
                        }
                        if (check <= 0)
                        {
                            PlayerTimeOut[player.userID].foocase = 0;
                            var count = Settings.ListDropFoo.ElementAt(UnityEngine.Random.Range(0, Settings.ListDropFoo.Count));
                            var kolvo = UnityEngine.Random.Range(count.MinDrop, count.MaxDrop);
                            var add = CheckFileDrop[player.userID].CheckItemData;
                            add.Add(new ItemsData.ItemData
                            {
                                ShortName = count.ShortName,
                                Url = count.Url,
                                Command = count.Command,
                                SkinID = count.SkinID,
                                AmountDrop = kolvo,
                            });
                            container.Add(new CuiElement
                            {
                                Parent = Layer + ".Main" + $"Case{3}",
                                Components = 
                                {
                                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "openCase"), Color = "1 1 1 1" },
                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                                }
                            });
                            CuiHelper.AddUi(player, container);
                        Effect.server.Run("assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab",player.transform.position);
                        if (count.Command != null) {
                            SendReply(player, $"<color=#e5a779><size=15>Вы успешно открыли кейс</size></color>" + $"\n<color=#b3a1fc><size=15>Вам выпала команда</size></color>");
                        }
                        else {
                            SendReply(player, $"<color=#e5a779><size=15>Вы успешно открыли кейс</size></color>" + $"\n<color=#b3a1fc><size=15>Вам выпало {ItemManager.FindItemDefinition(count.ShortName).displayName.english} в количестве {kolvo}</size></color>");
                        }                        
                        PlayerTimeOut[player.userID].foocase += CurrentTime() + Settings.TimeFoo;
                    }
                    }
                }
            }

        const string Layer = "lay";

        [ChatCommand("case")]
        private void DrawUICase(BasePlayer player)
        {
            DrawGui(player);
        }

        [ConsoleCommand("returnCase")]
        private void cmdReturnCase(ConsoleSystem.Arg args)
        {
            BasePlayer player = args?.Player();
            DrawGui(player);
        }

        [ConsoleCommand("openInventory")]
        private void cmdOpenInventory(ConsoleSystem.Arg args)
        {
            BasePlayer player = args?.Player();
            DrawGuiStorage(player);
        }

        private void DrawGuiStorage(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, LStorage);
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, "Overlay", LStorage);

            container.Add(new CuiElement
            {
                Parent = LStorage,
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "backgroundFon") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 1" },
                }
            });

            container.Add(new CuiElement
            {
                Name = LStorage + ".Main",
                Parent = LStorage,
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "fonCasesINV"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "642 362" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = LStorage, Color = "0 0 0 0" },
                Text = { Text = "" }
            }, LStorage + ".Main");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.22 0.718", AnchorMax = "0.317 0.766" },
                Button = { Command = "returnCase", Color = "1 1 1 0" },
                Text = { Text = "" }
            }, LStorage + ".Main");

            var Items = CheckFileDrop[player.userID].CheckItemData.Count;
            for (int i = 0, y = 0, x = 0; i < 18; i++)
            {
                container.Add(new CuiElement
                {
                    Name = LStorage + ".Main" + $"Item{i}",
                    Parent = LStorage + ".Main",
                    Components = 
                    {
                        new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "fonItems") },
                        new CuiRectTransformComponent { AnchorMin = $"{0.221 + (x * 0.095)} {0.542 - (y * 0.168)}", AnchorMax = $"{0.305 + (x * 0.095)} {0.69 - (y * 0.168)}" },
                    }
                });

                if (Items - 1 >= i)
                {
                    var image = CheckFileDrop[player.userID].CheckItemData.ElementAt(i).Url != null ? CheckFileDrop[player.userID].CheckItemData.ElementAt(i).Url : CheckFileDrop[player.userID].CheckItemData.ElementAt(i).ShortName;
                    container.Add(new CuiElement
                    {
                        Parent = LStorage + ".Main" + $"Item{i}",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image) },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 1", OffsetMax = "-5 -1" }
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0 ", AnchorMax = "1 1", OffsetMax = "-5 -1", OffsetMin = "5 1" },
                        Button = { Color = "0 0 0 0", Command = $"takeitem {i}" },
                        Text = { Text = $"{CheckFileDrop[player.userID].CheckItemData.ElementAt(i).AmountDrop.ToString()}" + "шт", Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                    }, LStorage + ".Main" + $"Item{i}");
                }

                x++;
                if (x == 6)
                {
                    x = 0;
                    y++;
                }
            }

            CuiHelper.AddUi(player, container);
        }

        private void DrawGui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, LStorage);
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "backgroundFon") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 1" },
                }
            });

            container.Add(new CuiElement
            {
                Name = Layer + ".Main",
                Parent = Layer,
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "fonCases"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "642 362" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.78 0.805", AnchorMax = "0.795 0.833", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "desc" },
                Text = { Text = "?", Color = "1 1 1 0.7", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = Layer, Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.67 0.705", AnchorMax = "0.765 0.755" },
                Button = { Command = "openInventory", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, Layer + ".Main");

            for (int i = 0; i < 4; i++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = 
                    {
                        AnchorMin = $"{0.232 + (i * 0.138)} 0.235",
                        AnchorMax = $"{0.353 + (i * 0.138)} 0.45"
                    },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Main", Layer + ".Main" + $"Case{i}");

                if (i == 0)
                {
                    var check = PlayerTimeOut[player.userID].onecase - CurrentTime();
                    var timecheck = TimeSpan.FromSeconds(check).ToShortString();

                    if (PlayerTimeOut[player.userID].level < 5) {
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4" },
                            Text = { Text = $"<size=20>Уровень</size>\n<size=14>Требуется 5 уровень</size>", Color = "1 1 1 0.65",FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                        }, Layer + ".Main" + $"Case{i}");
                    }

                    if (check <= 0)
                        PlayerTimeOut[player.userID].onecase = 0;

                    if (PlayerTimeOut[player.userID].onecase > 0)
                    {
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4" },
                            Text = { Text = $"<size=20>ОСТАЛОСЬ</size>\n<size=14>{timecheck}</size>", Color = "1 1 1 0.65",FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                        }, Layer + ".Main" + $"Case{i}");
                    }
                }
                if (i == 1)
                {
                    var check = PlayerTimeOut[player.userID].tocase - CurrentTime();
                    var timecheck = TimeSpan.FromSeconds(check).ToShortString();

                    if (PlayerTimeOut[player.userID].level < 10) {
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4" },
                            Text = { Text = $"<size=20>Уровень</size>\n<size=14>Требуется 10 уровень</size>", Color = "1 1 1 0.65",FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                        }, Layer + ".Main" + $"Case{i}");
                    }

                    if (check <= 0)
                        PlayerTimeOut[player.userID].tocase = 0;

                    if (PlayerTimeOut[player.userID].tocase > 0)
                    {
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4" },
                            Text = { Text = $"<size=20>ОСТАЛОСЬ</size>\n<size=14>{timecheck}</size>", Color = "1 1 1 0.65",FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                        }, Layer + ".Main" + $"Case{i}");
                    }
                }
                if (i == 2)
                {
                    var checkss = PlayerTimeOut[player.userID].freecase - CurrentTime();
                    var timecheck = TimeSpan.FromSeconds(checkss).ToShortString();

                    if (PlayerTimeOut[player.userID].level < 15) {
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4" },
                            Text = { Text = $"<size=20>Уровень</size>\n<size=14>Требуется 15 уровень</size>", Color = "1 1 1 0.65",FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                        }, Layer + ".Main" + $"Case{i}");
                    }

                    if (checkss <= 0)
                        PlayerTimeOut[player.userID].freecase = 0;
                
                    if (PlayerTimeOut[player.userID].freecase > 0)
                    {
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4" },
                            Text = { Text = $"<size=20>ОСТАЛОСЬ</size>\n<size=14>{timecheck}</size>", Color = "1 1 1 0.65",FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                        }, Layer + ".Main" + $"Case{i}");
                    }
                }
                if (i == 3)
                {
                    var checksss = PlayerTimeOut[player.userID].foocase - CurrentTime();
                    var timecheck = TimeSpan.FromSeconds(checksss).ToShortString();

                    if (PlayerTimeOut[player.userID].level < 20) {
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4" },
                            Text = { Text = $"<size=20>Уровень</size>\n<size=14>Требуется 20 уровень</size>", Color = "1 1 1 0.65",FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                        }, Layer + ".Main" + $"Case{i}");
                    }

                    if (checksss <= 0)
                        PlayerTimeOut[player.userID].foocase = 0;

                    if (PlayerTimeOut[player.userID].foocase > 0)
                    {
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4" },
                            Text = { Text = $"<size=20>ОСТАЛОСЬ</size>\n<size=14>{timecheck}</size>", Color = "1 1 1 0.65",FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                        }, Layer + ".Main" + $"Case{i}");
                    }
                }

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Command = $"opencase {(i == 0 ? "one" : i == 1 ? "too" : i == 2 ? "free" : "foo")}" },
                    Text = { Text = "" }
                }, Layer + ".Main" + $"Case{i}");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.045 0.8", AnchorMax = $"0.203 0.96", },
                    Button = { Color = "1 1 1 0", Command = $"openDesc {i}" },
                    Text = { Text = $"" }
                }, Layer + ".Main" + $"Case{i}");
            }

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("desc")]
        void DescUI(ConsoleSystem.Arg args) {
            var player = args.Player();
            CuiHelper.DestroyUi(player, Layer + ".Main" + ".Description");
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = Layer + ".Main" + ".Description",
                Parent = Layer + ".Main",
                Components = {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "fonDescription") },
                    new CuiRectTransformComponent { AnchorMin = $"0.58 0.6", AnchorMax = $"0.8 0.8" },
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.8", AnchorMax = "0.9 1" },
                Text = { Text = $"Описание кейсов", Color = "1 1 1 0.65",FontSize = 14, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, Layer + ".Main" + ".Description");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0", AnchorMax = "1 0.7" },
                Text = { Text = $"{Settings.Desk}", Color = "1 1 1 0.65",FontSize = 12, Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperLeft }
            }, Layer + ".Main" + ".Description");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.9 0.82", AnchorMax = "0.98 0.98" },
                Button = { Close = Layer + ".Main" + ".Description", Color = "1 1 1 0" },
                Text = { Text = "" }
            }, Layer + ".Main" + ".Description");

            CuiHelper.AddUi(player, container);
        }

        private void DescriptionUi(BasePlayer player, int element)
        {
            CuiHelper.DestroyUi(player, Layer + ".Main" + $"Case{element}" + ".Description");
            CuiElementContainer container = new CuiElementContainer();
            int y = element == 0 ? 1 : element == 1 ? 0 : element == 2 ? 0 : 1;
            string Description = element == 0 ? Settings.DescOne : element == 1 ? Settings.DescTo : element == 2 ? Settings.DescFree : Settings.DescFoo;
            string Name = element == 0 ? "Мешок" : element == 1 ? "ДЕРЕВЯННЫЙ ЯЩИК" : element == 2 ? "СУМКА" : "ЗОЛОТАЯ БОЧКА";

            container.Add(new CuiElement
            {
                Name = Layer + ".Main" + $"Case{element}" + ".Description",
                Parent = Layer + ".Main",
                Components = {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "fonDescription") },
                    new CuiRectTransformComponent { AnchorMin = $"{0.078 + (element * 0.21)} {0.512 - (y * 0.12)}", AnchorMax = $"{0.28 + (element * 0.21)} {0.694 - (y * 0.12)}" },
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.8", AnchorMax = "0.9 1" },
                Text = { Text = $"Описание кейса '{Name}'", Color = "1 1 1 0.65",FontSize = 14, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, Layer + ".Main" + $"Case{element}" + ".Description");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0", AnchorMax = "1 0.7" },
                Text = { Text = $"{Description}", Color = "1 1 1 0.65",FontSize = 12, Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperLeft }
            }, Layer + ".Main" + $"Case{element}" + ".Description");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.9 0.82", AnchorMax = "0.98 0.98" },
                Button = { Close = Layer + ".Main" + $"Case{element}" + ".Description", Color = "1 1 1 0" },
                Text = { Text = "" }
            }, Layer + ".Main" + $"Case{element}" + ".Description");

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("enablecase")]
        void Console_EnableCase(ConsoleSystem.Arg args) {
            var player = args.Player();
            var enable = EnableUI[player.userID] == true ? false : true;
            EnableUI[player.userID] = enable;
            InterfaceXP(player);
        }

        void InterfaceXP(BasePlayer player) {
            if (!EnableUI.ContainsKey(player.userID)) {
                EnableUI[player.userID] = true;
            }
            CuiHelper.DestroyUi(player, "Layer_xp");
            var container = new CuiElementContainer();

            var anchor = EnableUI[player.userID] == true ? "-430 16" : "-250 16";
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = anchor, OffsetMax = "-210 42.5" },
                Image = { Color = "0 0 0 0"}
            }, "Overlay", "Layer_xp");

            var text = EnableUI[player.userID] == true ? ">" : "<";
            var anchortext = EnableUI[player.userID] == true ? "0.2 1" : "0.3 1";
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = anchortext, OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "enablecase" },
                Text = { Text = text, Color = "1 1 1 0.7", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf" }
            }, "Layer_xp");

            var anchorblock = EnableUI[player.userID] == true ? "0.15 0" : "0.3 0";
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = anchorblock, AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.15"}
            }, "Layer_xp", "Image");

            var anchorimage = EnableUI[player.userID] == true ? "0.15 1" : "1 1";
            container.Add(new CuiElement
            {
                Parent = "Image",
                Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "imagecasexp"), Color = "1 1 1 0.6" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = anchorimage, OffsetMin = "6 6", OffsetMax = "-6 -6" }
                    }
            });

            if (EnableUI[player.userID]) {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.16 0.15", AnchorMax = $"1 0.85", OffsetMax = "-4 0" },
                    Image = { Color = "0 0 0 0"}
                }, "Image", "Progress");
                
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"{(float)PlayerTimeOut[player.userID].xp / 100} 1", OffsetMax = "0 0" },
                    Image = { Color = "0.96 0.41 0.07 0.9"}
                }, "Progress");

                container.Add(new CuiLabel
                {  
                    RectTransform = { AnchorMin = "0.18 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{PlayerTimeOut[player.userID].xp}%", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, "Image");

                container.Add(new CuiLabel
                {  
                    RectTransform = { AnchorMin = "0.35 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"({PlayerTimeOut[player.userID].level}) Ваш уровень", Color = "1 1 1 0.7", Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-regular.ttf" }
                }, "Image");
            }

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("takeitem")]
        private void skladgive(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            var item = CheckFileDrop[player.userID].CheckItemData.ElementAt(int.Parse(args.Args[0]));
            if (item.Command != null) {
                Server.Command(item.Command.Replace("%STEAMID%", player.UserIDString));
            }
            else {
                var ite = ItemManager.CreateByName(item.ShortName, item.AmountDrop);
                player.GiveItem(ite, BaseEntity.GiveItemReason.PickedUp);
            }
            CheckFileDrop[player.userID].CheckItemData.RemoveAt(int.Parse(args.Args[0]));
            DrawGuiStorage(player);
            SendReply(player, $"<color=#efd9c9>Вы успешно забрали свой приз</color>");
        }

        void ConvertXP(BasePlayer player, double xp) {
            PlayerTimeOut[player.userID].xp += xp;
            if (PlayerTimeOut[player.userID].xp >= 100) {
                PlayerTimeOut[player.userID].level += 1;
                SendReply(player, "Вы успешно апнули уровент!" + $"\nВаш уровень {PlayerTimeOut[player.userID].level}");
                PlayerTimeOut[player.userID].xp = 0;
            }
            InterfaceXP(player);
        }

        #region Функции получения xp
        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            ConvertXP(player, Settings.XpBonusGather);
        }

        public ulong lastDamageName;
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BradleyAPC && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
            if (entity is BaseHelicopter && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
        }

        [Oxide.Core.Plugins.HookMethod("OnEntityDeath")]
        void OnDeadEntity(BaseCombatEntity entity, HitInfo info)
        {
			if (entity==null || info==null) return;
            BasePlayer player = null;

            if (entity is BradleyAPC) {
                player = BasePlayer.FindByID(lastDamageName);
                ConvertXP(player, Settings.XpKillBradley);
            }
            if (entity is BaseHelicopter) {
                player = BasePlayer.FindByID(lastDamageName);
                ConvertXP(player, Settings.XpKillHeli);
            }
            
            EntityDeathFunc(entity, info);
        }

        void EntityDeathFunc(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.Initiator is BaseNpc || info.Initiator is ScientistNPC || info.InitiatorPlayer == null || info.InitiatorPlayer.GetComponent<NPCPlayer>()) return;
            if (info?.InitiatorPlayer == null) return;
            BasePlayer atacker = info.InitiatorPlayer;
            if (atacker as BasePlayer)
            {
                if (entity as BaseAnimalNPC)
                {
                    ConvertXP(atacker, Settings.XpKillAnimal);
                }
                if (entity as BasePlayer)
                {
                    if (atacker.userID == (entity as BasePlayer).userID) return;
                    if (info.isHeadshot) ConvertXP(atacker, Settings.XpKillPlayerHead);
                    else ConvertXP(atacker, Settings.XpKillPlayer);
                }
                if (entity.ShortPrefabName.Contains("barrel"))
                {
                    ConvertXP(atacker, Settings.XpKillBarrel);
                }
            }
        }
        #endregion
        }
    }