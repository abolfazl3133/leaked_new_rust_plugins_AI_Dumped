using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("PlayBonus", "Anathar", "0.2.6")]
    [Description("Выдача бонусов за игру")]
    class PlayBonus : RustPlugin
    {
        [PluginReference]
        Plugin RustStore;
        Plugin Economics;
        Plugin ServerRewards;
        
        Random random = new Random();
        public double GetRandomNumber(double minimum, double maximum)
        { 
            return random.NextDouble() * (maximum - minimum) + minimum;
        }
        public double TugrikRate;
        #region Data
        private StoredData DataBase = new StoredData();
        Dictionary<ulong,int> Timers = new Dictionary<ulong, int>();
        public class StoredData {
            public Dictionary<ulong, PlayerBase> PlayerInfo = new Dictionary<ulong, PlayerBase>();
        }
        public class PlayerBase
        {
            public int Time;
            public float Tugrik;
            public bool WaitTugrik;
        }
        
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, DataBase);

        private void LoadData() {
            try {
                DataBase = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(Name);
            } catch (Exception e) {
                DataBase = new StoredData();
            }
        }
        #endregion

        #region Config
        private static ConfigFile config;
        
        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Поддержка Магазина Moscow OVH")]
            public bool OvhStore { get; set; } = false;
            
            [JsonProperty(PropertyName = "Поддержка Магазина GameStore")]
            public bool GameStore { get; set; } = false;
            
            [JsonProperty(PropertyName = "Поддержка Economics")]
            public bool UseEconomics { get; set; } = false;
            
            [JsonProperty(PropertyName = "Поддержка ServerRewards")]
            public bool UseServerRwards { get; set; } = false;
            
            [JsonProperty(PropertyName = "GameStore Id Магазина")]
            public string GSId { get; set; } = "";
            
            [JsonProperty(PropertyName = "GameStore Api Ключь")]
            public string GSApi { get; set; } = "";
            
            [JsonProperty(PropertyName = "Ссылка на ваш магазин")]
            public string StoreUrl { get; set; } = "SuperPuperRust.ru";
            
            [JsonProperty(PropertyName = "Название валюты")]
            public string TugrikName { get; set; } = "Тугрики";
            
            [JsonProperty(PropertyName = "Иконка с балансом AnchorMin")]
            public string AnchorMin { get; set; } = "0 0";
            
            [JsonProperty(PropertyName = "Иконка с балансом AnchorMax")]
            public string AnchorMax { get; set; } = "0.01 0.01";
            
            [JsonProperty(PropertyName = "Иконка с балансом OffsetMin")]
            public string OffsetMin { get; set; } = "300 18";
            
            [JsonProperty(PropertyName = "Иконка с балансом OffsetMax")]
            public string OffsetMax { get; set; } = "420 71";
            
            [JsonProperty(PropertyName = "Цвет заднего фона")]
            public string BgColor { get; set; } = "#00000068";
            
            [JsonProperty(PropertyName = "Цвет Блока")]
            public string LineColor { get; set; } = "#A0A0A063";
            
            [JsonProperty(PropertyName = "Цвет кнопки обменять/Получить")]
            public string GreenButton { get; set; } = "#3488349A";
            
            [JsonProperty(PropertyName = "Цвет кнопки перевести")]
            public string ChangeButton { get; set; } = "#A0A0A09A";
            
            [JsonProperty(PropertyName = "Цвет текста перевести")]
            public string ChangeButtontxt { get; set; } = "#D68A00FF";
            
            [JsonProperty(PropertyName = "Цвет текста №1")]
            public string textcolor1 { get; set; } = "#FFFFFFFF";
            
            [JsonProperty(PropertyName = "Цвет текста №2")]
            public string textcolor2 { get; set; } = "#FF7D00FF";
            
            [JsonProperty(PropertyName = "Время проведённое на сервер для бонуса(В секундах)")]
            public int NeedTime { get; set; } = 3600;
            
            [JsonProperty(PropertyName = "Количество бонусов за проведённое время на сервере")]
            public int HowBonus { get; set; } = 1;
            
            [JsonProperty(PropertyName = "Рандомный курс обмена?")]
            public bool RandomRate { get; set; } = false;
            
            [JsonProperty(PropertyName = "Рандомный курс от")]
            public double RandomRateStart { get; set; } = 0.1;
            
            [JsonProperty(PropertyName = "Рандомный курс до")]
            public double RandomRateEnd { get; set; } = 1.6;
            
            [JsonProperty(PropertyName = "Через какое время изменять курс? (В секундах)")]
            public int RandomTime { get; set; } = 3600;
            
            [JsonProperty(PropertyName = "Статичный курс обмена")]
            public float StaticRate { get; set; } = 1.05f;

        }
        
        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigFile>();
                if (config == null)
                    Regenerate();
            }
            catch
            {
                Regenerate();
            }
        }

        private void Regenerate()
        {
            LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigFile();
            Config.WriteObject(config);
        }

        #endregion

        #region Hoocks

        void Init() 
        { 
            LoadConfig();
        } 
        private void OnServerInitialized() {
            LoadData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                
                if (!DataBase.PlayerInfo.ContainsKey(player.userID))
                {
                    DataBase.PlayerInfo.Add(player.userID, new PlayerBase()
                    {
                        Time = config.NeedTime,
                        Tugrik = 0,
                        WaitTugrik = false
                    });
                    Timers.Add(player.userID,config.NeedTime);
                    SaveData();
                    DrawButton(player);
                    
                }
                else
                {
                    if(!DataBase.PlayerInfo[player.userID].WaitTugrik)
                    Timers.Add(player.userID,DataBase.PlayerInfo[player.userID].Time);
                    DrawButton(player);
                }
                
            }
            timer.Every(60, CheckTimer);
            if (config.RandomRate)
            {
                SetRandomRate();
                timer.Every(config.RandomTime, SetRandomRate);
            }
            else TugrikRate = config.StaticRate;
            
            timer.Every(360, SaveData);
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(1f, () => OnPlayerInit(player));
                return;
            }
            CheckDb(player.userID);

            if (DataBase.PlayerInfo.ContainsKey(player.userID))
            {
                if (!DataBase.PlayerInfo[player.userID].WaitTugrik)
                {
                    if (!Timers.ContainsKey(player.userID))
                        Timers.Add(player.userID, DataBase.PlayerInfo[player.userID].Time);
                }

                DrawButton(player);
            }
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            CheckDb(player.userID);
            if (Timers.ContainsKey(player.userID))
            {
                DataBase.PlayerInfo[player.userID].Time = Timers[player.userID];
                Timers.Remove(player.userID);
            }
        }
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "BonusButton");
                CuiHelper.DestroyUi(player, "BonusUi" + "BackGround");
            }
            SaveData();
        }

        #endregion

        #region OtherShit

        void CheckDb(ulong player)
        {
            var data = new PlayerBase
            {
                Time = config.NeedTime,
                Tugrik = 0,
                WaitTugrik = false
            };
            if(!DataBase.PlayerInfo.ContainsKey(player)){ DataBase.PlayerInfo.Add(player,data); SaveData();}
        }
        
        void SetRandomRate()
        {
            TugrikRate = GetRandomNumber(config.RandomRateStart, config.RandomRateEnd);
        }

        void CheckTimer()
        {
            foreach (var finder in Timers.Keys.ToList())
            {
                Timers[finder] -= 60;
                if (Timers[finder] > config.NeedTime)
                {
                    DataBase.PlayerInfo[finder].Time = DataBase.PlayerInfo[finder].Time += config.NeedTime;
                    CheckTimer();
                    break;
                }

                if (Timers[finder] <= 0)
                {
                    Timers.Remove(finder);
                    BasePlayer player = BasePlayer.FindByID(finder);
                    DrawBonus(player);
                    SendReply(player, $"Вам доступен Бонус,для получения нажмите на зелёную кнопку");
                    DataBase.PlayerInfo[finder].WaitTugrik = true;
                }

            }
        }
        void GetRandom()
        {
            int Get = 0;
            if (Get == {DarkPluginsID})
                {
                PrintError("Error Check");
            }
        
        }
        void DrawBonus(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BonusButton" + "ExButton");
            CuiElementContainer containerButton = new CuiElementContainer();
            containerButton.Add(new CuiButton
                {
                    Button = {Color = HexToRustFormat(config.GreenButton), Command = "GetBonus"},
                    RectTransform = {AnchorMax = "0.98 0.95", AnchorMin = "0.02 0.55"},
                    Text = {Text = "Получить",Color = HexToRustFormat("#FFFFFFFF"),FontSize = 20, Align = TextAnchor.MiddleCenter}
                }, "BonusButton", "BonusButton" + "GetBonus");
            CuiHelper.AddUi(player, containerButton);
        }

        void DrawButton(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BonusButton");
            CuiElementContainer containerButton = new CuiElementContainer();
            containerButton.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = "BonusButton",
                Components =
                {
                    new CuiRawImageComponent()
                    {
                        Color = HexToRustFormat(config.BgColor),
                        Sprite = "assets/content/ui/ui.spashscreen.psd",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = config.AnchorMin,
                        AnchorMax = config.AnchorMax,
                        OffsetMin = config.OffsetMin,
                        OffsetMax = config.OffsetMax
                    }
                }
            });
            if (!DataBase.PlayerInfo[player.userID].WaitTugrik)
            {
                containerButton.Add(new CuiButton
                    {
                        Button = {Color = HexToRustFormat(config.ChangeButton), Command = "PlayBonus"},
                        RectTransform = {AnchorMax = "0.98 0.95", AnchorMin = "0.02 0.55"},
                        Text =
                        {
                            Text = "Перевести", Color = HexToRustFormat(config.ChangeButtontxt), FontSize = 20,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, "BonusButton", "BonusButton" + "ExButton");
            }
            else
            {
                containerButton.Add(new CuiButton
                    {
                        Button = {Color = HexToRustFormat(config.GreenButton), Command = "GetBonus"},
                        RectTransform = {AnchorMax = "0.98 0.95", AnchorMin = "0.02 0.55"},
                        Text = {Text = "Получить",Color = HexToRustFormat("#FFFFFFFF"),FontSize = 20, Align = TextAnchor.MiddleCenter}
                    }, "BonusButton", "BonusButton" + "GetBonus");
            }

            containerButton.Add(new CuiElement
            {
                Parent = "BonusButton",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"{config.TugrikName}:{DataBase.PlayerInfo[player.userID].Tugrik}",
                        Align = TextAnchor.MiddleCenter,
                        Color = HexToRustFormat("#FFFFFFFF"),
                        FontSize = 18
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.02 0.02",
                        AnchorMax = "0.98 0.5"
                    }
                }
            });
            CuiHelper.AddUi(player, containerButton);
        }
        void DrawGui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BonusUi" + "BackGround");
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Parent = "Hud",
                Name = "BonusUi" + "BackGround",
                Components =
                {
                    new CuiNeedsCursorComponent()
                    {
                    },
                    new CuiRawImageComponent()
                    {
                        Color = HexToRustFormat(config.BgColor),
                        Sprite = "assets/content/ui/ui.spashscreen.psd",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0.01 0.01",
                        OffsetMin = "441 300",
                        OffsetMax = "807 550"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "BackGround",
                Name = "BonusUi" + "Header",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = HexToRustFormat(config.LineColor)
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0.88",
                        AnchorMax = "0.905 0.985"
                       
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "Header",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"Обмен валют",
                        Align = TextAnchor.MiddleCenter,
                        Color = HexToRustFormat(config.textcolor1),
                        FontSize = 22
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            container.Add(new CuiButton
                {
                    Button = {Color = HexToRustFormat("#FF00009A"), Close = "BonusUi" + "BackGround"},
                    RectTransform = {AnchorMax = "0.99 0.985", AnchorMin = "0.91 0.88"},
                    Text = {Text = "X", FontSize = 24, Align = TextAnchor.MiddleCenter}
                }, "BonusUi" + "BackGround");
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "BackGround",
                Name = "BonusUi" + "UserName",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = HexToRustFormat(config.LineColor)
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0.77",
                        AnchorMax = "0.99 0.87"
                       
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "UserName",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"Имя клиента:",
                        Align = TextAnchor.MiddleRight,
                        Color = HexToRustFormat(config.textcolor1),
                        FontSize = 16
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0",
                        AnchorMax = "0.37 0.9"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "UserName",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = player.displayName,
                        Align = TextAnchor.MiddleLeft,
                        Color = HexToRustFormat(config.textcolor2),
                        FontSize = 16
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.38 0",
                        AnchorMax = "1 0.9"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "BackGround",
                Name = "BonusUi" + "Balance",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = HexToRustFormat(config.LineColor)
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0.66",
                        AnchorMax = "0.99 0.76"
                       
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "Balance",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"Баланс:",
                        Align = TextAnchor.MiddleRight,
                        Color = HexToRustFormat(config.textcolor1),
                        FontSize = 16
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0",
                        AnchorMax = "0.37 0.9"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "Balance",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = DataBase.PlayerInfo[player.userID].Tugrik.ToString(),
                        Align = TextAnchor.MiddleLeft,
                        Color = HexToRustFormat(config.textcolor2),
                        FontSize = 16
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.38 0",
                        AnchorMax = "1 0.9"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "BackGround",
                Name = "BonusUi" + "Rate",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = HexToRustFormat(config.LineColor)
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0.55",
                        AnchorMax = "0.99 0.65"
                       
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "Rate",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"Текущий курс:",
                        Align = TextAnchor.MiddleRight,
                        Color = HexToRustFormat(config.textcolor1),
                        FontSize = 16
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0",
                        AnchorMax = "0.37 0.9"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "Rate",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = string.Format("{0:0.00}",TugrikRate),
                        Align = TextAnchor.MiddleLeft,
                        Color = HexToRustFormat(config.textcolor2),
                        FontSize = 16
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.38 0",
                        AnchorMax = "1 0.9"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "BackGround",
                Name = "BonusUi" + "ConfirmSum",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = HexToRustFormat(config.LineColor)
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0.44",
                        AnchorMax = "0.99 0.54"
                       
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "ConfirmSum",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"Бонусов на сумму:",
                        Align = TextAnchor.MiddleRight,
                        Color = HexToRustFormat(config.textcolor1),
                        FontSize = 16
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0",
                        AnchorMax = "0.37 0.9"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "ConfirmSum",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = string.Format("{0:0.00}",DataBase.PlayerInfo[player.userID].Tugrik * TugrikRate)+"₽",
                        Align = TextAnchor.MiddleLeft,
                        Color = HexToRustFormat(config.textcolor2),
                        FontSize = 16
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.38 0",
                        AnchorMax = "1 0.9"
                    }
                }
            });
            container.Add(new CuiButton
                {
                    Button = {Color = HexToRustFormat(config.GreenButton), Command = "Exchange"},
                    RectTransform = {AnchorMax = "0.99 0.41", AnchorMin = "0.01 0.15"},
                    Text = {Text = "Обменять", FontSize = 45, Align = TextAnchor.MiddleCenter}
                }, "BonusUi" + "BackGround");
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "BackGround",
                Name = "BonusUi" + "Url",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = HexToRustFormat(config.LineColor)
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0.01",
                        AnchorMax = "0.99 0.13"
                       
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = "BonusUi" + "Url",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = config.StoreUrl,
                        Align = TextAnchor.MiddleCenter,
                        Color = HexToRustFormat(config.textcolor2),
                        FontSize = 22
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01 0",
                        AnchorMax = "1 1"
                    }
                }
            });
            CuiHelper.AddUi(player, container);
        }

        [ChatCommand("PlayBonus")]
        void PlayBonusCmd(BasePlayer player, string command, string[] args)
        {
          DrawGui(player);
        }
        
        [ConsoleCommand("PlayBonus")]
        void PlayBonusConsoleCmd(ConsoleSystem.Arg args)
        {
            BasePlayer pl = args.Player();
            DrawGui(pl);
        }

        [ConsoleCommand("GetBonus")]
        void GetBonusCmd(ConsoleSystem.Arg args)
        {
            BasePlayer pl = args.Player();
            if (!Timers.ContainsKey(pl.userID) && DataBase.PlayerInfo[pl.userID].WaitTugrik)
            {
                DataBase.PlayerInfo[pl.userID].Tugrik += config.HowBonus;
                DataBase.PlayerInfo[pl.userID].Time = DataBase.PlayerInfo[pl.userID].Time = config.NeedTime;
                DataBase.PlayerInfo[pl.userID].WaitTugrik = false;
                SaveData();
                DrawButton(pl);
                Timers.Add(pl.userID,config.NeedTime);
            }

        }

        [ConsoleCommand("Exchange")]
        void ExchangeCmd(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();

            if (DataBase.PlayerInfo[player.userID].Tugrik == 0)
            {
                SendReply(player,"У вас нечего обменивать");
                return;
            } 
            double ChangeTugrik = DataBase.PlayerInfo[player.userID].Tugrik * TugrikRate;

            if (config.UseEconomics)
            {
                Economics.Call("Deposit", player.userID, ChangeTugrik);
                SendReply(player, $"Вы успешно обменяли {config.TugrikName} На игровую валюту");
            }

            if (config.UseServerRwards)
            {
                ServerRewards.Call("AddPoints", player.userID, ChangeTugrik);
                SendReply(player,$"Вы успешно обменяли {config.TugrikName} На игровую валюту");
            }
            
            if (config.OvhStore)
            {
              
                RustStore?.CallHook("APIChangeUserBalance", player.userID, (int)ChangeTugrik, new Action<string>((result) =>
                {    
                    if (result == "SUCCESS")
                    {
                        DataBase.PlayerInfo[player.userID].Tugrik = 0;
                        SaveData();
                        PrintWarning($"Игрок {player.displayName} успешно получил {ChangeTugrik} рублей");
                        DrawGui(player);
                        DrawButton(player);
                        return;
                    }
                    Interface.Oxide.LogDebug($"Баланс не был изменен, ошибка: {result}");
                }));
                
            }
            if (config.GameStore)
            {
                
                string url = $"http://gamestores.ru/api?shop_id={config.GSId}&secret={config.GSApi}&action=moneys&type=plus&steam_id={player.UserIDString}&amount={ChangeTugrik}&mess=Обмен тугриков на рубли";
                webrequest.Enqueue(url, null, (i, s) =>
                {
                    if (i != 200)
                    {
                        PrintError($"Ошибка соединения с сайтом GS!");
                    }
                    else
                    {
                        JObject jObject = JObject.Parse(s);
                        if (jObject["result"].ToString() == "fail")
                        {
                            PrintError($"Ошибка пополнения баланса для {player.displayName}!");
                            PrintError($"Причина: {jObject["message"].ToString()}");
                        }
                        else
                        {
                            DataBase.PlayerInfo[player.userID].Tugrik = 0;
                            SaveData();
                            PrintWarning($"Игрок {player.displayName} успешно получил {ChangeTugrik} рублей");
                            DrawGui(player);
                            DrawButton(player);
                        }

                    }
                }, this, RequestMethod.GET);
            }
           
        }
        
        
   string SimpleColorFormat(string text, bool removeTags = false)
    {
    /*  Simple Color Format ( v3.0 ) by SkinN - Modified by LaserHydra
        Formats simple color tags to game dependant color codes */

    // All patterns
    Regex end = new Regex(@"\<(end?)\>"); // End tags
    Regex clr = new Regex(@"\<(\w+?)\>"); // Names
    Regex hex = new Regex(@"\<#(\w+?)\>"); // Hex codes

    // Replace tags
    text = end.Replace(text, "[/#]");
    text = clr.Replace(text, "[#$1]");
    text = hex.Replace(text, "[#$1]");

    return removeTags ? Formatter.ToPlaintext(text) : covalence.FormatText(text);
    }

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
        
    #endregion
    }
}