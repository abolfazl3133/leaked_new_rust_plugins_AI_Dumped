using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Miner", "XAVIER", "1.0.0")]
    public class Miner : RustPlugin
    {
        #region Variables

        [PluginReference] private Plugin ImageLibrary = null;

        private const string Layer = "UI_MinerMask";
        
        public enum RarityType
        {
            Default = 1,
            Medium  = 2,
            Premium = 3,
            Epic    = 4,
            Hard    = 5,
        }

        public Dictionary<ulong, PlayerInfo> PlayerDictionary = new Dictionary<ulong, PlayerInfo>();

        private static Miner Instance;

        #endregion

        #region Classes


        public class PlayerInfo
        {
            public class ItemPrize
            {
                public string ShortName;

                public int Amount;

                public string Image;

                public string Command;

                public ulong SkinID;
                
                // New
                
                public int CurrentStage;
                
                public int FinishStage;
                
                public string DisplayName;
                
                public RarityType Rarity;
                
                public string UniID = CuiHelper.GetGuid();
                
                //
                

                public string GetDisplay() => ItemManager.FindItemDefinition(ShortName) == null
                    ? DisplayName
                    : ItemManager.FindItemDefinition(ShortName).displayName.english;


                public object GiveTo(BasePlayer player)
                {
                    var itemDef = ItemManager.FindItemDefinition(ShortName);

                    if (itemDef == null)
                    {
                        Instance.Server.Command(Command.Replace($"%STEAMID%", player.UserIDString));
                        return null;
                    }

                    var item = ItemManager.CreateByName(ShortName, Amount, SkinID);

                    return item;
                }
            }
            
            public int TotalGame;


            public int CurrentGame;

            public bool IsGame;

            public List<ItemPrize> CachedItem = new List<ItemPrize>();
            public List<ItemPrize> PrizeList  = new List<ItemPrize>();
        }

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            Instance = this;
            
            try
            {
                PlayerDictionary = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayerInfo>>(Name);
            }
            catch
            {
                PlayerDictionary = new Dictionary<ulong, PlayerInfo>();
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }

            ImageLibrary.Call("AddImage", "https://i.imgur.com/8qD4etV.png", "LoseMiner");
            
            timer.Every(58, () =>
            {
                RunUpdateChecksDateTime();
            });
        }

        void Unload()
        {
            Interface.GetMod().DataFileSystem.WriteObject(Name, PlayerDictionary);

            Instance = null;
            
        }

        void OnServerSave() => Interface.GetMod().DataFileSystem.WriteObject(Name, PlayerDictionary);

        void OnPlayerConnected(BasePlayer player) => PlayerDictionary.TryAdd(player.userID, new PlayerInfo
        {
            TotalGame = 0
        });

        #endregion

        #region Functions

        
        private string GetFormatTime(TimeSpan timespan)
        {
            return string.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds, System.Math.Floor(timespan.TotalHours));
        }
        
        private void RunUpdateChecksDateTime()
        {
            var currentTime = DateTime.Now;

            if (currentTime.ToString("t") == "00:00")
            {
                foreach (var info in PlayerDictionary)
                {
                    info.Value.TotalGame = 0;
                }
                
                LogAction($"[{DateTime.Now.ToString("f")}] Плагин выдал <<всем>> {config.TotalGameLimit} бонусные попытки");
            }
        }

        public void LogAction(string message)
        {
            PrintWarning(message);
            
            LogToFile(Name, message, this);
        }
        
        private static Configuration.ItemPrize GetItem()
        {
            string type  = string.Empty;

            var iterationRarity = 0;

            do
            {
                iterationRarity++;

                var randomType = Instance.config.SettingsRarity.GetRandom();
                if (randomType.ChanceRarity < 1 || randomType.ChanceRarity > 100)
                    continue;
                
                if (UnityEngine.Random.Range(0f, 100f) <= randomType.ChanceRarity)
                    type = randomType.Type.ToString();
                
                
            } while (type == string.Empty && iterationRarity < 1000);

            if (type == string.Empty)
                return null;
            
           //Instance.PrintWarning($"---Плагин нашел раритетность: {type}");
            
            if (type != string.Empty)
            {
                var listAll = Instance.config.PrizeItem.Where(p => p.RarityType.ToString() == type).ToList();
                
                Configuration.ItemPrize item = null;
                var iteration = 0;
                do
                {
                    iteration++;

                    var randomItem = listAll.GetRandom();
                    if (randomItem.Chance < 1 || randomItem.Chance > 100)
                        continue;

                    if (UnityEngine.Random.Range(0f, 100f) <= randomItem.Chance)
                        item = randomItem;
                } while (item == null && iteration < 1000);

                if (item != null)
                {
                    //Instance.PrintWarning($"---Плагин нашел предмет: {item.DisplayName} | {item.RarityType.ToString()}");
                    return item;
                }
            }

            return null;
        }
        
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }
        
        #endregion

        #region Commands

        #region Console

        [ConsoleCommand("UIMinerHandler")]
        void ConsoleCmdMain(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();

            if (args.HasArgs())
            {
                switch (args.Args[0])
                {
                    case "openInventory":
                    {
                        StartInventoryUI(player);
                        
                        break;
                    }

                    case "backUI":
                    {
                        StartUI(player);
                        
                        break;
                    }


                    case "viewInfo":
                    {
                        
                        Effect effect = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, new Vector3(), new Vector3());
                        EffectNetwork.Send(effect, player.Connection);
                        
                        CuiHelper.DestroyUi(player, ".B1");
                        
                        CuiHelper.DestroyUi(player, ".B2");
                        
                        var data = PlayerDictionary[player.userID];


                        int index = int.Parse(args.Args[1]);

                        var currentItem = data.PrizeList.FirstOrDefault(p => p.UniID == args.Args[2]);
                        if (currentItem == null) return;
                        

                        var container = new CuiElementContainer();
                        
                        CuiHelper.DestroyUi(player, ".MainInfo");
                        
                        container.Add(new CuiPanel
                        {
                            RectTransform = {AnchorMin = $"0.4645821 0.6212963", AnchorMax = $"0.76875 0.6675926"},
                            Image         = {Color     = HexToCuiColor("#B6B6B6", 10),},
                        }, Layer, ".MainInfo");
                        
                        CuiHelper.DestroyUi(player, ".MainInfoTittle");
            
                        container.Add(new CuiLabel
                        {
                            Text          = {Text      = $"<b>{currentItem.GetDisplay()}</b>" + $"<size=10>\n<color=#FFFFFF66>Вы собрали {currentItem.CurrentStage}/{currentItem.FinishStage} карт, что-бы получить выбранный предмет вам необходимо собрать все карты!</color></size>", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#EDF1F0")},
                            RectTransform = {AnchorMin = $"0.4713541 0.6212963", AnchorMax = $"0.76875 0.6675926" },
                        }, Layer, ".MainInfoTittle");

                        CuiHelper.AddUi(player, container);

                        if (currentItem.CurrentStage >= currentItem.FinishStage)
                        {
                            container.Clear();
                            
                            
                            container.Add(new CuiButton
                            {
                                RectTransform = {AnchorMin = "0 0.6", AnchorMax = "0.985 0.98"},
                                Button        = {Color     = HexToCuiColor("#1F1F1F", 80), Command = $"UIMinerHandler giveItem {index} {currentItem.UniID}"},
                                Text          = {Text      = "Забрать", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
                            }, Layer + index + ".M", ".B1");
                            
                            container.Add(new CuiButton
                            {
                                RectTransform = {AnchorMin = "0 0", AnchorMax = "0.985 0.3650798"},
                                Button        = {Color     = HexToCuiColor("#1F1F1F", 80), Command = $"UIMinerHandler removeButton"},
                                Text          = {Text      = "Отмена", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
                            }, Layer + index + ".M", ".B2");

                            CuiHelper.AddUi(player, container);
                        }
                        
                        break;
                    }

                    case "giveItem":
                    {
                        var data = PlayerDictionary[player.userID];


                        int index = int.Parse(args.Args[1]);

                        var currentItem = data.PrizeList.FirstOrDefault(p => p.UniID == args.Args[2]);
                        if (currentItem == null) return;
                        
                        if (currentItem.CurrentStage < currentItem.FinishStage) return;

                        var item = currentItem.GiveTo(player);
                        
                        if (item is Item)
                            player.GiveItem(item as Item);

                        if (currentItem.FinishStage <= 1)
                        {
                            data.PrizeList.Remove(currentItem);
                        
                            CuiHelper.DestroyUi(player, Layer + index + ".G");
                            CuiHelper.DestroyUi(player, Layer + index + ".I");
                            CuiHelper.DestroyUi(player, Layer + index + ".A");
                            CuiHelper.DestroyUi(player, Layer + index + ".B");
                        }
                        else
                        {
                            if (currentItem.CurrentStage > currentItem.FinishStage)
                                currentItem.CurrentStage -= currentItem.FinishStage;
                            else if (currentItem.CurrentStage == currentItem.FinishStage)
                            {
                                data.PrizeList.Remove(currentItem);
                        
                                CuiHelper.DestroyUi(player, Layer + index + ".G");
                                CuiHelper.DestroyUi(player, Layer + index + ".I");
                                CuiHelper.DestroyUi(player, Layer + index + ".A");
                                CuiHelper.DestroyUi(player, Layer + index + ".B");
                            }
                        }
                        
                        CuiHelper.DestroyUi(player, ".MainInfoTittle");
                        CuiHelper.DestroyUi(player, ".MainInfo");
                        CuiHelper.DestroyUi(player, ".B1");
                        CuiHelper.DestroyUi(player, ".B2");
                        
                        
                        Effect effect = new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player, 0, new Vector3(), new Vector3());
                        EffectNetwork.Send(effect, player.Connection);
                        
                        
                        break;
                    }

                    case "removeButton":
                    {
                        CuiHelper.DestroyUi(player, ".MainInfoTittle");
                        CuiHelper.DestroyUi(player, ".MainInfo");
                        
                        CuiHelper.DestroyUi(player, ".B1");
                        CuiHelper.DestroyUi(player, ".B2");
                        
                        break;
                    }
                    
                    case "startGame":
                    {
                        var data = PlayerDictionary[player.userID];

                        if (data.TotalGame >= config.TotalGameLimit)
                        {
                            player.ChatMessage($"Вы уже сыграли {config.TotalGameLimit} раза. Приходите завтра!");
                            return;
                        }

                        data.TotalGame++;

                        data.IsGame = true;


                        CuiHelper.DestroyUi(player, ".ButtonRemove");
                        CuiHelper.DestroyUi(player, ".LoseInfo");


                        ViewSquares(player);
                        ViewCurrentGame(player);
                        
                        break;
                    }
                    
                    
                    case "stopGame":
                    {
                        
                        var data = PlayerDictionary[player.userID];

                        if (!data.IsGame) return;
                        
                        if (data.CachedItem.Count <= 0)
                        {
                            player.ChatMessage("Вы еще ничего не выйграли! Попробуйте выйграть хотя-бы 1 предмет, перед тем как завершать игру");
                            
                            return;
                        }

                        data.IsGame = false;

                        data.CurrentGame = 0;

                        foreach (var itemPrize in data.CachedItem)
                        {
                            var findContains = data.PrizeList.FirstOrDefault(p =>
                                p.DisplayName.ToLower() == itemPrize.DisplayName.ToLower());

                            if (findContains != null && findContains.FinishStage > 1)
                            {
                                if (findContains.FinishStage > 1)
                                    findContains.CurrentStage++;
                            }
                            else
                            {
                                data.PrizeList.Add(itemPrize);
                            }
                        }
                        
                        
                        //data.PrizeList.AddRange(data.CachedItem);
                        
                        player.ChatMessage($"Поздравляю! Вы успешно выиграли {data.CachedItem.Count} различных игровых предметов! Они находятся в инвентаре");
                        
                        data.CachedItem = new List<PlayerInfo.ItemPrize>();
                        
                        var container = new CuiElementContainer();
                        
                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1" },
                            Button        = {Color     = "0 0 0 0", Close = Layer},
                            Text          = {Text      = ""}
                        }, ".ButtonClose", ".ButtonRemove");

                        CuiHelper.AddUi(player, container);
                        
                        ViewSquares(player);

                        ViewCurrentGame(player);
                        
                        break;
                    }


                    case "viewItem":
                    {
                        var data = PlayerDictionary[player.userID];
                        
                        if (!data.IsGame) return;


                        int indexUi = int.Parse(args.Args[1]);

                        var container = new CuiElementContainer();
                            

                        float chanceMine = config.ChanceMine[data.CurrentGame];


                        if (UnityEngine.Random.Range(0, 100) < chanceMine)
                        {
                            
                            Effect effect = new Effect("assets/bundled/prefabs/fx/weapons/landmine/landmine_explosion.prefab", player, 0, new Vector3(), new Vector3());
                            EffectNetwork.Send(effect, player.Connection);
                            
                            data.CachedItem = new List<PlayerInfo.ItemPrize>();
                            data.CurrentGame = 0;
                            
                            player.ChatMessage("Упс... Вы наступили на мину, к сожалению вы все проиграли!");
                            
                            container.Add(new CuiPanel
                            {
                                RectTransform = {AnchorMin = $"0 0", AnchorMax = $"0.98 1"},
                                Image         = {FadeIn = 1.0f, Sprite    = "assets/content/ui/ui.background.transparent.linear.psd", Color = HexToCuiColor("#FF8484", 50),},
                            }, Layer + indexUi + ".S");
                            
                            container.Add(new CuiElement
                            {
                                Parent = Layer + indexUi + ".S",
                                Components =
                                {
                                    new CuiRawImageComponent     {Png       = (string) ImageLibrary.Call("GetImage", "trap.landmine" + 512)},
                                    new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"}
                                }
                            });
                            
                            container.Add(new CuiElement
                            {
                                Parent = Layer,
                                Name   = ".LoseInfo",
                                Components =
                                {
                                    new CuiRawImageComponent     {FadeIn    = 1.0f,Png       = (string) ImageLibrary.Call("GetImage", "LoseMiner")},
                                    new CuiRectTransformComponent{AnchorMin = "0.4786459 0.6555555", AnchorMax = "0.6739584 0.7018518"}
                                }
                            });
                            
                            container.Add(new CuiLabel
                            {
                                Text          = {FadeIn    = 1.0f,Text      = $"Вы наступили на мину", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter},
                                RectTransform = {AnchorMin = $"0 0", AnchorMax = $"1 1" },
                            }, ".LoseInfo");

                            data.IsGame = false;
                            
                            container.Add(new CuiButton
                            {
                                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1" },
                                Button        = {Color     = "0 0 0 0", Close = Layer},
                                Text          = {Text      = ""}
                            }, ".ButtonClose", ".ButtonRemove");
                            
                            
                            
                            CuiHelper.AddUi(player, container);

                            ViewCurrentGame(player);
                        }
                        else
                        {
                            
                            Effect effect = new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player, 0, new Vector3(), new Vector3());
                            EffectNetwork.Send(effect, player.Connection);
                            
                            var itemRandom = GetItem();
                            
                            if (itemRandom == null)
                                itemRandom = GetItem();

                            int amount = itemRandom.MinAmount == itemRandom.MaxAmount ? itemRandom.MinAmount : UnityEngine.Random.Range(itemRandom.MinAmount, itemRandom.MaxAmount);
                            
                            data.CachedItem.Add(new PlayerInfo.ItemPrize
                            {
                                ShortName    = itemRandom.ShortName,
                                Command      = itemRandom.Command,
                                Amount       = amount,
                                Image        = itemRandom.URLImage,
                                SkinID       = itemRandom.SkinID,
                                CurrentStage = 1,
                                FinishStage  = itemRandom.StageNeed,
                                DisplayName = itemRandom.DisplayName,
                                Rarity = itemRandom.RarityType,
                            });

                            data.CurrentGame++;

                            CuiHelper.DestroyUi(player, Layer + indexUi + ".SRemoveButton");
                            
                            var colorRarity = config.SettingsRarity.FirstOrDefault(p => p.Type == itemRandom.RarityType)?.ColorRarity;

                            
                            container.Add(new CuiPanel
                            {
                                RectTransform = {AnchorMin = $"0 0", AnchorMax = $"0.98 1"},
                                Image         = {FadeIn = 1.0f, Sprite    = "assets/content/ui/ui.background.transparent.linear.psd", Color = HexToCuiColor(colorRarity, 50),},
                            }, Layer + indexUi + ".S");

                            if (string.IsNullOrEmpty(itemRandom.URLImage))
                            {
                                container.Add(new CuiElement
                                {
                                    Parent = Layer + indexUi + ".S",
                                    Components =
                                    {
                                        new CuiRawImageComponent     {Png       = (string) ImageLibrary.Call("GetImage", itemRandom.ShortName + 512)},
                                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"}
                                    }
                                });
                            }
                            else
                            {
                                container.Add(new CuiElement
                                {
                                    Parent = Layer + indexUi + ".S",
                                    Components =
                                    {   
                                        new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", itemRandom.URLImage)},
                                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"}
                                    }
                                });
                            }
                            
                            container.Add(new CuiLabel
                                {
                                    Text          = {Text      = $"x{amount}", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleRight},
                                    RectTransform = {AnchorMin = $"0 0.01086909", AnchorMax = $"0.9239309 0.2934731" },
                                }, Layer + indexUi + ".S");
                            
                            
                            CuiHelper.AddUi(player, container);
                        }
                        
                        
                        break;
                    }
                }
            }
        }

        #endregion
        
        #region Chat

        [ChatCommand("miner")]
        void ChatCmdMain(BasePlayer player, string command, string[] args)
        {
            if (args.Length <= 0)
            {
                StartUI(player);
                return;
            }
            
            if (!player.IsAdmin) return;

            switch (args[0])
            {
                case "configSetup":
                {
                    int RarityInt = int.Parse(args[1]);
                    
                    
                    var itemList = player.inventory.AllItems();

                    foreach (var item in itemList)
                    {
                        config.PrizeItem.Add(new Configuration.ItemPrize
                        {
                            ShortName = item.info.shortname,
                            MinAmount = item.amount,
                            MaxAmount = item.amount + 2,
                            Chance = UnityEngine.Random.Range(10, 60),
                            Command = "",
                            RarityType = (RarityType)RarityInt
                        });
                    }
                    
                    SaveConfig();
                    
                    player.ChatMessage($"Вы успешно сохранили предметы в раритетность <<{(RarityType)RarityInt}>>");
                    
                    
                    break;
                }

                case "resetGame":
                {
                    var data = PlayerDictionary[player.userID];

                    data.TotalGame = 0;
                    
                    break;
                }
            }
            
        }

        #endregion

        #endregion

        #region UI


        private void StartInventoryUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);

            
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image         = {Color     = "0 0 0 0.9"}
            }, "Overlay", Layer);
            
			
            
            container.Add(new CuiButton
            {
                RectTransform  = {AnchorMin = "0 0", AnchorMax = "1 1" },
                Button         = {Color     = "0 0 0 0.23", Material  = "assets/content/ui/uibackgroundblur.mat"},
                Text = { Text = "" }
            }, Layer);
           
            
            container.Add(new CuiButton
            {
                RectTransform  = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button         = {Sprite    = "assets/content/ui/ui.background.transparent.radial.psd", Color     = HexToCuiColor("#363636", 100)},
                Text           = {Text      = "" }
            }, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = $"0.23125 0.6212963", AnchorMax = $"0.378125 0.6675926"},
                Image         = {Color = HexToCuiColor("#B6B6B6", 15),},
            }, Layer);
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "Ежедневная игра <<Минер>>", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E4DAD1")},
                RectTransform = {AnchorMin = $"0.23125 0.6212963", AnchorMax = $"0.378125 0.6675926" },
            }, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = $"0.23125 0.3324074", AnchorMax = $"0.378125 0.6120387"},
                Image         = {Color = HexToCuiColor("#B6B6B6", 15),},
            }, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = $"0.2364583 0.3851852", AnchorMax = $"0.3729167 0.6027778"},
                Image         = {Color = HexToCuiColor("#B6B6B6", 20),},
            }, Layer);
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "Информация", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E4DAD1")},
                RectTransform = {AnchorMin = $"0.2364583 0.5629629", AnchorMax = $"0.3729167 0.6027778" },
            }, Layer);
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "Тут расписать про инвентарь, что лут попадает сюда, хранится вечно или временн и т.д.", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft, Color =  HexToCuiColor("#E4DAD1", 50)},
                RectTransform = {AnchorMin = $"0.2416667 0.3851852", AnchorMax = $"0.3729167 0.5601864" },
            }, Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = $"0.2364583 0.3416666", AnchorMax = $"0.3729167 0.3759259"},
                Button        = {Command   = "UIMinerHandler backUI", Color = HexToCuiColor("#B6B6B6", 20),},
                Text          = {Text      = "Вернуться обратно", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E4DAD1") }
            }, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = $"0.3833333 0.6212963", AnchorMax = $"0.459375 0.6675926"},
                Image         = {Color = HexToCuiColor("#B6B6B6", 15),},
            }, Layer);
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "Инвентарь", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E4DAD1")},
                RectTransform = {AnchorMin = $"0.3833333 0.6212963", AnchorMax = $"0.459375 0.6675926" },
            }, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = $"0.3833333 0.3324074", AnchorMax = $"0.76875 0.6120387"},
                Image         = {Color = HexToCuiColor("#B6B6B6", 15),},
            }, Layer);
            
            
            CuiHelper.AddUi(player, container);
            
            ViewStorage(player, 1);
        }

        private void ViewStorage(BasePlayer player, int page)
        {
            var data = PlayerDictionary[player.userID];
            
            var SelectList = data.PrizeList.Select((i, t) => new { A = i, B = t - (page - 1) * 38 }).Skip((page - 1) * 38)
                .Take(38).ToList();
            
            CuiElementContainer container = new CuiElementContainer();
            
            CuiHelper.DestroyUi(player, ".Back");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.6927037 0.3416675", AnchorMax = "0.7255161 0.4000027"},
                Button        = {Color     = HexToCuiColor("#B6B6B6", 20),},
                Text          = {Text      = "<",  Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 16 }
            }, Layer, ".Back");
            
            CuiHelper.DestroyUi(player, ".Next");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.730724 0.3416675", AnchorMax = "0.7635363 0.4000027" },
                Button        = {Color     = HexToCuiColor("#B6B6B6", 20)},
                Text          = {Text      = ">",  Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 16 }
            }, Layer, ".Next");


            for (int i = 0; i < 38; i++)
            {
                CuiHelper.DestroyUi(player, Layer + i + ".M");
                
                container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = $"{0.3885417 + i * 0.038 - Math.Floor((double)i / 10) * 10 * 0.038} {0.5444444 - Math.Floor((double) i / 10) * 0.0675}",
                            AnchorMax = $"{0.4213541 + i * 0.038 - Math.Floor((double)i / 10) * 10 * 0.038} {0.6027778 - Math.Floor((double) i / 10) * 0.0675}",
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#B6B6B6", 20),
                        },
                        Text =
                        {
                            Text = $"",
                        }
                    }, Layer, Layer + i + ".M");
                
                
            }


            foreach (var check in SelectList)
            {
                var colorRarity = config.SettingsRarity.FirstOrDefault(p => p.Type == check.A.Rarity)?.ColorRarity;
                
                
                container.Add(new CuiElement
                {   
                    Parent = Layer + check.B + ".M",
                    Name   = Layer + check.B + ".G",
                    Components =
                    {
                        new CuiImageComponent         {Color     = HexToCuiColor(colorRarity, 40),  Sprite = "assets/icons/circle_gradient.png" },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" },
                    }
                });
                


                if (check.A.FinishStage > 1 && check.A.CurrentStage < check.A.FinishStage)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + check.B + ".M",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) plugins.Find("ImageLibrary").Call("GetImage", "blueprintbase") },
                            new CuiRectTransformComponent { AnchorMin = "0.1904761 0.1904767", AnchorMax = "0.8095239 0.8095233"},
                        }
                    });
                }
                
                if (string.IsNullOrEmpty(check.A.Image))
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + check.B + ".M",
                        Name   = Layer + check.B + ".I",
                        Components =
                        {
                            new CuiRawImageComponent     {Png       = (string) ImageLibrary.Call("GetImage", check.A.ShortName + 512)},
                            new CuiRectTransformComponent{AnchorMin = "0.1904761 0.1904767", AnchorMax = "0.8095239 0.8095233"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + check.B + ".M",
                        Name   = Layer + check.B + ".I",
                        Components =
                        {   
                            new CuiRawImageComponent      {Png       = (string) ImageLibrary.Call("GetImage", check.A.Image)},
                            new CuiRectTransformComponent {AnchorMin = "0.1904761 0.1904767", AnchorMax = "0.8095239 0.8095233"}
                        }
                    });
                }
                            
                container.Add(new CuiLabel
                {
                    Text          = {Text      = $"x{check.A.Amount}", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleRight},
                    RectTransform = {AnchorMin = $"0 0.01086909", AnchorMax = $"0.9239309 0.2934731" },
                }, Layer + check.B + ".M",Layer + check.B + ".A");
                

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Button        = {Color     = "0 0 0 0", Command = $"UIMinerHandler viewInfo {check.B} {check.A.UniID}"},
                    Text          = {Text      = "", }
                }, Layer + check.B + ".M", Layer + check.B + ".B");
            }


            CuiHelper.AddUi(player, container);

        }

        private void StartUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);

            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image         = {Color     = "0 0 0 0.9"}
            }, "Overlay", Layer);
            
			
            
            container.Add(new CuiButton
            {
                RectTransform  = {AnchorMin = "0 0", AnchorMax = "1 1" },
                Button         = {Color     = "0 0 0 0.23", Material  = "assets/content/ui/uibackgroundblur.mat"},
                Text = { Text = "" }
            }, Layer);
           
            
            container.Add(new CuiButton
            {
                RectTransform  = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button         = {Sprite    = "assets/content/ui/ui.background.transparent.radial.psd", Color     = HexToCuiColor("#363636", 100)},
                Text           = {Text      = "" }
            }, Layer);
            
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1" },
                Button        = {Color     = "0 0 0 0"},
                Text          = {Text      = ""}
            }, Layer, ".ButtonClose");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1" },
                Button        = {Color     = "0 0 0 0", Close = Layer},
                Text          = {Text      = ""}
            }, ".ButtonClose", ".ButtonRemove");
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = $"0.3265625 0.6555555", AnchorMax = $"0.4734375 0.7018518"},
                Image         = {Color     = HexToCuiColor("#B6B6B6", 15),},
            }, Layer);
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "Ежедневная игра <<Минер>>", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E4DAD1")},
                RectTransform = {AnchorMin = $"0.3265625 0.6555555", AnchorMax = $"0.4734375 0.7018518" },
            }, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = $"0.3265625 0.3555556", AnchorMax = $"0.4734375 0.6462979"},
                Image         = {Color = HexToCuiColor("#B6B6B6", 15),},
            }, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = $"0.3317708 0.4518519", AnchorMax = $"0.4682291 0.637037"},
                Image         = {Color = HexToCuiColor("#B6B6B6", 20),},
            }, Layer);
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "Информация", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E4DAD1")},
                RectTransform = {AnchorMin = $"0.3317708 0.5972222", AnchorMax = $"0.4682291 0.637037" },
            }, Layer);
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "Каждый день Вам будет доступно 3 попытки для игры. Нажимая на поле может быть 2 исхода, первый - вы получите рандомный лут, второй - вы наступите на мину и тем самым потратите одну из попыток.Доверьтесь своей интуиции и получайте ценные награды!", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft, Color =  HexToCuiColor("#E4DAD1", 50)},
                RectTransform = {AnchorMin = $"0.3375 0.4518519", AnchorMax = $"0.4682291 0.5898162" },
            }, Layer);

            var data = PlayerDictionary[player.userID];
            
            DateTime now = DateTime.Now;
                
                
            DateTime midnight = new DateTime(now.Year, now.Month, now.Day + 1, 0, 0, 0); // Устанавливаем полночь следующего дня

            TimeSpan timeLeft = midnight - now;


            string MessageSend = config.TotalGameLimit - data.TotalGame <= 0
                ? $"{GetFormatTime(TimeSpan.FromSeconds(timeLeft.TotalSeconds))}"
                : "Попыток осталось: ";
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = $"0.3317708 0.4083333", AnchorMax = $"0.4682291 0.4425964"},
                Button        = {Color = HexToCuiColor("#84CBFF", 35),},
                Text          = {Text      = MessageSend, Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#84CBFF") }
            }, Layer, ".CurrentInfo");
            
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = $"0.3265625 0.2990741", AnchorMax = $"0.4734375 0.3462963"},
                Button        = {Command   = "UIMinerHandler openInventory",Color = HexToCuiColor("#B6B6B6", 15),},
                Text          = {Text      = "Инвентарь", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E4DAD1") }
            }, Layer, ".CurrentInfo");
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = $"0.4786435 0.2990741", AnchorMax = $"0.6739584 0.6462963"},
                Image         = {Color     = HexToCuiColor("#B6B6B6", 15),},
            }, Layer);

            CuiHelper.AddUi(player, container);
            
            
            ViewCurrentGame(player);

            ViewSquares(player);
        }

        public void ViewSquares(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();


            for (int i = 0; i < 25; i++)
            {
                CuiHelper.DestroyUi(player, Layer + i + ".S");
                
                
                container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = $"{0.4838541 + i * 0.038 - Math.Floor((double)i / 5) * 5 * 0.038} {0.5787037 - Math.Floor((double) i / 5) * 0.067}",
                            AnchorMax = $"{0.5166667 + i * 0.038 - Math.Floor((double)i / 5) * 5 * 0.038} {0.637037 - Math.Floor((double) i / 5) * 0.067}",
                        },
                        Button =
                        {
                            Color     = HexToCuiColor("#B6B6B6", 20)
                        },
                        Text =
                        {
                            Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                        }
                    }, Layer, Layer + i + ".S");
                
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"0 0",
                        AnchorMax = $"1 1",
                    },
                    Button =
                    {
                        Color     = "0 0 0 0",
                        Command   = $"UIMinerHandler viewItem {i}"
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, Layer + i + ".S", Layer + i + ".SRemoveButton");
                
            }
            
            CuiHelper.AddUi(player, container);
            
        }


        private void ViewCurrentGame(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            var data = PlayerDictionary[player.userID];

            CuiHelper.DestroyUi(player, ".InfoGame");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = $"0.4489583 0.4083333", AnchorMax = $"0.4682291 0.4425964"},
                Button        = {Color     = HexToCuiColor("#84CBFF", 35),},
                Text          = {Text      = $"{config.TotalGameLimit - data.TotalGame}", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#84CBFF") }
            }, Layer, ".InfoGame");

            string textInfo = data.IsGame ? "Остановиться и забрать награды" : config.TotalGameLimit - data.TotalGame <= 0 ? "Недоступно" : "Начать игру";

            CuiHelper.DestroyUi(player, ".ButtonGame");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = $"0.3317708 0.3648147", AnchorMax = $"0.4682291 0.3990778"},
                Button        = {Command   = data.IsGame ? "UIMinerHandler stopGame" : "UIMinerHandler startGame", Color = HexToCuiColor("#C8D382", 35),},
                Text          = {Text      = $"{textInfo}", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#CCD68A") }
            }, Layer, ".ButtonGame");


            CuiHelper.AddUi(player, container);

        }
        


        #endregion
        
        #region Configuration


        public Configuration config;

        protected override void LoadDefaultConfig()
        {
            config = Configuration.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();

            Config.WriteObject(config, true);
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        public class Configuration
        {

            #region Classes
            

            public class ItemPrize
            {
                [JsonProperty("ShortName")] 
                public string ShortName;

                [JsonProperty("SkinID")] 
                public ulong SkinID;
            
                [JsonProperty("Command")] 
                public string Command;

                [JsonProperty("Min Amount")] 
                public int MinAmount;
                
                [JsonProperty("Max Amount")] 
                public int MaxAmount;

                [JsonProperty("Chance")] 
                public float Chance;
                
                
                // TODO: New

                [JsonProperty("Need Stage")]
                public int StageNeed;

                [JsonProperty("Название")] 
                public string DisplayName;
                
                //

                [JsonProperty("Image")] 
                public string URLImage;

                [JsonProperty("Rarity ( 1 - Default, 2 - Medium, 3 - Premium, 4 - Epic, 5 - Hard )")]
                public RarityType RarityType;
            }


            public class RaritySetting
            {
                [JsonProperty("Раритетность")] 
                public RarityType Type;
                
                [JsonProperty("Цвет раритетности")] 
                public string ColorRarity;

                [JsonProperty("Шанс выпадения")]
                public int ChanceRarity;
            }
            

            #endregion


            #region Variables


            [JsonProperty("Максимальное количество игр на 1 игрока за 1 день")]
            public int TotalGameLimit;

            [JsonProperty("Настройка раритетности")]
            public List<RaritySetting> SettingsRarity = new List<RaritySetting>();
            
            

            [JsonProperty("Количество выпавших предметов => шанс выпадения мины")]
            public Dictionary<int, float> ChanceMine = new Dictionary<int, float>();
            
            [JsonProperty("Возможные призы")]
            public List<ItemPrize> PrizeItem = new List<ItemPrize>();
            
            

            #endregion
            

            #region Loaded

            public static Configuration DefaultConfig()
            {
                return new Configuration()
                {
                    SettingsRarity = new List<RaritySetting>()
                    {
                        new RaritySetting
                        {
                            Type = RarityType.Default,
                            ColorRarity = "#1986EC",
                            ChanceRarity = 60,
                        },
                        new RaritySetting
                        {
                            Type = RarityType.Medium,
                            ColorRarity = "#19EC47",
                            ChanceRarity = 30,
                        },
                        new RaritySetting
                        {
                            Type = RarityType.Premium,
                            ColorRarity = "#EC7E19",
                            ChanceRarity = 20,
                        },
                        new RaritySetting
                        {
                            Type = RarityType.Epic,
                            ColorRarity = "#EC1919",
                            ChanceRarity = 15,
                        },
                        new RaritySetting
                        {
                            Type = RarityType.Hard,
                            ColorRarity = "#7116E5",
                            ChanceRarity = 2,
                        },
                    },
                    
                    TotalGameLimit = 3,
                        
                    
                    ChanceMine = new Dictionary<int, float>
                    {
                        [0] = 2,
                        [1] = 5,
                        [2] = 10,
                        [3] = 15,
                        [4] = 20,
                        [5] = 25,
                        [6] = 30,
                        [7] = 35,
                        [8] = 40,
                        [9] = 45,
                        [10] = 50,
                        [11] = 55,
                        [12] = 70,
                        [13] = 75,
                        [14] = 80,
                        [15] = 85,
                        [16] = 90,
                        [17] = 95,
                        [18] = 97,
                        [19] = 98,
                        [20] = 99,
                    },
                    PrizeItem = new List<ItemPrize>
                    {
                        new ItemPrize
                        {
                            ShortName = "blueberries",
                            MinAmount = 3,
                            MaxAmount = 9,
                            SkinID = 0,
                            Chance = 50,
                            Command = "",
                            StageNeed = 1,
                            DisplayName = "Черника",
                            URLImage = "",
                            RarityType = RarityType.Epic,
                        },
                        new ItemPrize
                        {
                            ShortName = "mining.quarry",
                            MinAmount = 1,
                            MaxAmount = 1,
                            SkinID = 0,
                            Chance = 50,
                            Command = "",
                            DisplayName = "Карьер",
                            StageNeed = 1,
                            URLImage = "",
                            RarityType = RarityType.Default,
                        },
                        new ItemPrize
                        {
                            ShortName = "explosive.satchel",
                            MinAmount = 1,
                            MaxAmount = 15,
                            SkinID = 0,
                            Chance = 50,
                            Command = "",
                            DisplayName = "C4",
                            StageNeed = 1,
                            URLImage = "",
                            RarityType = RarityType.Default,
                        },
                        new ItemPrize
                        {
                            ShortName = "supply.signal",
                            MinAmount = 1,
                            MaxAmount = 3,
                            SkinID = 0,
                            Chance = 50,
                            DisplayName = "Сигнальная шашка",
                            StageNeed = 1,
                            Command = "",
                            URLImage = "",
                            RarityType = RarityType.Default,
                        }
                    }
                };
            }

            #endregion
        }

        #endregion
    }
}