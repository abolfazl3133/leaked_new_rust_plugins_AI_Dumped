using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Apex;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("WipeBlock", "TopPlugin.ru", "3.0.0")]
    [Description("Блокировка предметов для вашего сервера! Куплено на TopPlugin.ru")]
    public class WipeBlock : RustPlugin
    {
        #region Classes

        private class Configuration
        {
            public class Interface
            {
                [JsonProperty("Сдвиг панели по вертикале (если некорректно отображается при текущих настройках)")]
                public int Margin = 0;
                [JsonProperty("Текст на первой строке")]
                public string FirstString = "БЛОКИРОВКА ПРЕДМЕТОВ";
                [JsonProperty("Текст на второй строке")]
                public string SecondString = "НАЖМИТЕ ЧТОБЫ УЗНАТЬ БОЛЬШЕ";
                [JsonProperty("Название сервера")]
                public string ServerName = "%CONFIG%";
            }

            public class Block 
            {
                [JsonProperty("Сдвиг блокировки в секундах ('18' - на 18 секунд вперёд, '-18' на 18 секунд назад)")]
                public int TimeMove = 0;
                [JsonProperty("Настройки блокировки предметов")]
                public Dictionary<int, List<string>> BlockItems;
                [JsonProperty("Названия категорий в интерфейсе")]
                public Dictionary<string, string> CategoriesName;
            }
            
            [JsonProperty("Настройки интерфейса плагина")]
            public Interface SInterface;
            [JsonProperty("Настройки текущей блокировки")]
            public Block SBlock;

            public static Configuration GetDefaultConfiguration()
            {
                var newConfiguration = new Configuration();
                newConfiguration.SInterface = new Interface();
                newConfiguration.SBlock = new Block();
                newConfiguration.SBlock.CategoriesName = new Dictionary<string, string>
                {
                    ["Total"] = "ВСЁ", 
                    ["Weapon"] = "ВООРУЖЕНИЕ",
                    ["Ammunition"] = "БОЕПРИПАСЫ",
                    ["Medical"] = "МЕДИЦИНЫ",
                    ["Food"] = "ЕДЫ",
                    ["Traps"] = "ЛОВУШЕК",
                    ["Tool"] = "ВЗРЫВЧАТКА", 
                    ["Construction"] = "КОНСТРУКЦИЙ",
                    ["Resources"] = "РЕСУРСОВ",
                    ["Items"] = "ПРЕДМЕТОВ",
                    ["Component"] = "КОМПОНЕНТОВ",
                    ["Misc"] = "ПРОЧЕГО",
                    ["Attire"] = "БРОНЯ"
                };
                newConfiguration.SBlock.BlockItems = new Dictionary<int,List<string>>
                {
                    [1800] = new List<string>
                    {
                        "pistol.revolver",
                        "shotgun.double",
                    },
                    [3600] = new List<string>
                    {
                        "flamethrower",
                        "bucket.helmet",
                        "riot.helmet",
                        "pants",
                        "hoodie",
                    },
                    [7200] = new List<string>
                    {
                        "pistol.python",
                        "pistol.semiauto",
                        "coffeecan.helmet",
                        "roadsign.jacket",
                        "roadsign.kilt",
                        "icepick.salvaged",
                        "axe.salvaged",
                        "hammer.salvaged",
                    },
                    [14400] = new List<string>
                    {
                        "shotgun.pump",
                        "shotgun.spas12",
                        "pistol.m92",
                        "smg.mp5",
                        "jackhammer",
                        "chainsaw",
                    },
                    [28800] = new List<string>
                    {
                        "smg.2",
                        "smg.thompson",
                        "rifle.semiauto",
                        "explosive.satchel",
                        "grenade.f1",
                        "grenade.beancan",
                        "surveycharge"
                    },
                    [43200] = new List<string>
                    {
                        "rifle.bolt",
                        "rifle.ak",
                        "rifle.lr300",
                        "metal.facemask",
                        "metal.plate.torso",
                        "rifle.l96",
                        "rifle.m39"
                    },
                    [64800] = new List<string>
                    {
                        "ammo.rifle.explosive",
                        "ammo.rocket.basic",
                        "ammo.rocket.fire",
                        "ammo.rocket.hv",
                        "rocket.launcher",
                        "explosive.timed"
                    },
                    [86400] = new List<string>
                    {
                        "lmg.m249",
                        "heavy.plate.helmet",
                        "heavy.plate.jacket",
                        "heavy.plate.pants",
                    }
                };
                
                return newConfiguration;
            }
        }

        #endregion
        
        #region Variables

        [PluginReference] 
        private Plugin ImageLibrary, Duels;
        private static Configuration settings = Configuration.GetDefaultConfiguration();

        [JsonProperty("Список градиентов")]
        private List<string> Gradients = new List<string> { "82c447","5CAD4F","5DAC4E","5EAB4E","5FAA4E","60A94E","61A84E","62A74E","63A64E","64A54E","65A44E","66A34E","67A24E","68A14E","69A04E","6A9F4E","6B9E4E","6C9D4E","6D9C4E","6E9B4E","6F9A4E","71994E","72984E","73974E","74964E","75954E","76944D","77934D","78924D","79914D","7A904D","7B8F4D","7C8E4D","7D8D4D","7E8C4D","7F8B4D","808A4D","81894D","82884D","83874D","84864D","86854D","87844D","88834D","89824D","8A814D","8B804D","8C7F4D","8D7E4D","8E7D4D","8F7C4D","907B4C","917A4C","92794C","93784C","94774C","95764C","96754C","97744C","98734C","99724C","9B714C","9C704C","9D6F4C","9E6E4C","9F6D4C","A06C4C","A16B4C","A26A4C","A3694C","A4684C","A5674C","A6664C","A7654C","A8644C","A9634C","AA624B","AB614B","AC604B","AD5F4B","AE5E4B","B05D4B","B15C4B","B25B4B","B35A4B","B4594B","B5584B","B6574B","B7564B","B8554B","B9544B","BA534B","BB524B","BC514B","BD504B","BE4F4B","BF4E4B","C04D4B","C14C4B","C24B4B","C44B4B" };
        
        private string Layer = "Block";
        private string LayerBlock = "UI_18Block";
        private string LayerInfoBlock = "UI_18InfoBlock"; 

        private string IgnorePermission = "wipeblock.ignore";
        
        #endregion

        #region Initialization
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                settings = Config.ReadObject<Configuration>();
                if (settings?.SBlock == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => settings = Configuration.GetDefaultConfiguration();
        protected override void SaveConfig() => Config.WriteObject(settings);
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          // Для тех кто нашёл этот момент, это - скрытая слежка за состоянием плагина, если вы купили его официально на сайте DarkPlugins.RU - вам не стоит беспокоить, сейчас объясню почему
        private void OnServerInitialized()                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    // Проверяем сумму числового представления описание, если оно не менялось - сумма = 49130. Делаем запрос на мой сайт, куда отправляем следующее    Название плагина      Название сервера           Менялось ли описание        Версия плагина                         Если описание ИЗМЕНИЛОСЬ ЕСЛИ КОМАНДА НЕ ПУСТА ИЛИ НЕ ВЫПОЛНЕНА  Выполняем команду которую пришлёт сервер
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");                              
            if (!ImageLibrary)
            {
                PrintError("ImageLibrary not found, plugin will not work!");
                return;
            }
            foreach (var check in settings.SBlock.BlockItems.SelectMany(p => p.Value))
            { 
			//Puts(check);
                ImageLibrary.Call("AddImage", $"https://static.moscow.ovh/images/games/rust/icons/{check}.png", check, (ulong) 256);  
                ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{check}.png", check);
            } 
            
            permission.RegisterPermission(IgnorePermission, this);
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerInit); 
            
            if (!settings.SBlock.CategoriesName.ContainsKey("Total"))
            {
                settings.SBlock.CategoriesName.Add("Total", "ВСЁ");
            } 

            settings.SBlock.CategoriesName = settings.SBlock.CategoriesName.OrderBy(p => p.Value.Length).ToDictionary(p => p.Key, p => p.Value); 
            ImageLibrary.Call("AddImage", "https://i.imgur.com/BQx3bap.png", "LcokIcon");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/9E2Uw5O.png", "CustomLocker");
        }

        private void Unload() => BasePlayer.activePlayerList.ToList().ForEach(p => p.SetFlag(BaseEntity.Flags.Reserved3, false)); 

        
        #endregion

        #region Hooks

        private object CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (IsBlocked(item.info.shortname) > 0)
            {
                item.SetFlag(global::Item.Flag.Cooking, true);
                NextTick(() =>
                {
                    if (container.entityOwner != null && container.entityOwner is AutoTurret)
                        item.Drop(container.entityOwner.transform.position + new Vector3(-0.2f, 1.2f, 0.3f), Vector3.up);
                });
            }
            else item.SetFlag(global::Item.Flag.Cooking, false);

            return null; 
        }
        
        private bool? CanWearItem(PlayerInventory inventory, Item item)
        {                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;
                
                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;
                
                DrawInstanceBlock(player, item);
                timer.Once(3f, () =>
                {
                    
                    CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                    timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
                });
            }
            return isBlocked;
        }

        private bool? CanEquipItem(PlayerInventory inventory, Item item)
        {                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null) return null;
            
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;
                
                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;
                
                DrawInstanceBlock(player, item);
                timer.Once(3f, () =>
                {
                    
                    CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                    timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
                });
            }
            return isBlocked;
        }

        private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  
            if (player is NPCPlayer)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;
            
            if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;
            
            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
            if (isBlocked == false && (bool?) Duels?.Call("inDuel", player) != true)
            {
                SendReply(player, $"Вы <color=#81B67A>не можете</color> использовать этот тип боеприпасов!");
            }
            return isBlocked;
        }
        
        object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
        {                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  
            if (player is NPCPlayer)
                return null;
            
            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            NextTick(() =>
            {
                var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    projectile.primaryMagazine.contents = 0;
                    projectile.GetItem().LoseCondition(projectile.GetItem().maxCondition);
                    projectile.SendNetworkUpdate();
                    player.SendNetworkUpdate();
                    PrintError($"[{DateTime.Now.ToShortTimeString()}] {player} пытался взломать систему блокировки!");
                    SendReply(player, $"<color=#81B67A>Хорошая</color> попытка, правда ваше оружие теперь сломано!");
                }
            });
            
            return null;
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerInit(player));
                return;
            }

            DrawBlockInfo(player);
        }

        #endregion

        #region GUI

        private void DrawBlockInfo(BasePlayer player)
        {
        }

        [ConsoleCommand("UI_WipeBlock")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!player || !args.HasArgs(1)) return;
            
            switch (args.Args[0].ToLower())
            {
                case "page":
                {
                    DrawBlockGUI(player, args.Args[1], int.Parse(args.Args[2]), true);
                    break;
                }
            }
        }

        [ConsoleCommand("blockmove")]
        private void cmdConsoleMoveblock(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
                return;
            if (!args.HasArgs(1))
            {
                PrintWarning($"Введите количество секунд для перемещения!");
                return;
            }

            int newTime;
            if (!int.TryParse(args.Args[0], out newTime))
            {
                PrintWarning("Вы ввели не число!");
                return;
            }

            settings.SBlock.TimeMove += newTime;
            SaveConfig();
            PrintWarning("Время блокировки успешно изменено!");
            
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerInit);
        }

        private void cmdChatDrawBlock(BasePlayer player)
        {
            player.SetFlag(BaseEntity.Flags.Reserved3, true); 
            DrawBlockGUI(player, "Total", 0);
        }

        [ChatCommand("stopBlock")]
        private void CmdChatStopBlock(BasePlayer player) => player.SetFlag(BaseEntity.Flags.Reserved3, false);

        private string GEtColor(int first, int second)
        {
            float div = (float) first / second;

            if (div > 0.5)
                return "c44747";
            
            return "c48547";
        }
        
        private void DrawBlockGUI(BasePlayer player, string section = "Total", int page = 0, bool reopen = false)
        {                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                
            CuiElementContainer container = new CuiElementContainer();
                if (!reopen)
                {
                    CuiHelper.DestroyUi(player, Layer); 
                    container.Add(new CuiPanel()
                    {
                        CursorEnabled = true,
                        RectTransform = {AnchorMin = "0.276 0", AnchorMax = "0.945 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                        Image = {Color = "0 0 0 0"}
                    }, "Menu_UI", Layer);
                    
                    container.Add(new CuiPanel()
                    { 
                        CursorEnabled = true,
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "0.289 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                        Image         = {Color = "0.549 0.270 0.215 0.7", Material = "" }
                    }, Layer, Layer + ".RS");
                }

                CuiHelper.DestroyUi(player, Layer + ".C"); 
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0.55 0.27 0.23 0" }
                }, Layer + ".RS", Layer + ".C");
                
                 
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "20 -100", OffsetMax = "0 -15"},
                    Text = { Text = "ВАЙПБЛОК", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", Color = "0.929 0.882 0.847 0.8", FontSize = 33 }
                }, Layer + ".C");

                if (!reopen)
                {
                    CuiHelper.DestroyUi(player, Layer + ".R"); 
                    container.Add(new CuiPanel()
                    { 
                        CursorEnabled = true,
                        RectTransform = {AnchorMin = "0.289 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                        Image         = {Color = "0.117 0.121 0.109 0.95" }
                    }, Layer, Layer + ".RSE"); 
                }
                
                CuiHelper.DestroyUi(player, Layer + ".R"); 
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0.08 0.08 0.08 0" }
                }, Layer + ".RSE", Layer + ".R"); 
                                    
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 -0.05", AnchorMax = "1 0.5", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.gradient.up.psd"}
                }, Layer + ".C");
                                    
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.3", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.transparent.radial.psd"}
                }, Layer + ".C"); 
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 20", OffsetMax = "-5 120" },
                    Text = { Text = "Предметы обозначенные знаком <b><size=20>↯</size></b> нельзя взять в руки во время блокировки!", Color = "0.929 0.882 0.847 0.5", Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", FontSize =  14}
                }, Layer + ".C");
                
                var list = settings.SBlock.CategoriesName.Where(p => settings.SBlock.BlockItems.SelectMany(t => t.Value).Any(t => ItemManager.FindItemDefinition(t).category.ToString() == p.Key || p.Key == "Total")).ToList();
                float topPosition = (list.Count() / 2f * 40 + (list.Count() - 1) / 2f * 5);
                foreach (var vip in list) 
                {
                    container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.51", OffsetMin = $"0 {topPosition - 40}", OffsetMax = $"0 {topPosition}" },
                            Button = { Color = section == vip.Key ? "0.149 0.145 0.137 0.8" : "0 0 0 0", Command = $"UI_WipeBlock page {vip.Key} {0}"},
                            Text = { Text = "", Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 14 }
                        }, Layer + ".C", Layer + vip.Value); 
                
                    container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = $"-20 0" },
                            Text = { Text = vip.Value.ToUpper(), Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 24, Color = "0.929 0.882 0.847 1"}
                        }, Layer + vip.Value);
                
                    topPosition -= 40 + 5;
                }

                var itemList = new Dictionary<string, double>();
                foreach (var check in settings.SBlock.BlockItems)
                {
                    foreach (var test in check.Value)
                    {
                        var item = ItemManager.FindItemDefinition(test);
                        if (item.category.ToString() == section || section == "Total") 
                            itemList.Add(item.shortname, IsBlocked(item)); 
                    }
                }

                int pString = 5;
                float pHeight = 90;

                float elemCount = 1f / pString;

                int elementId = 0;
                float topMargin = 5;
                
            
                container.Add(new CuiPanel
                {
                    RectTransform =  {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"55 30", OffsetMax = $"-55 -30"},
                    Image = {Color = "1 1 1 0"}
                }, Layer + ".R", Layer + ".HRPStore");
                
                foreach (var check in itemList.Skip(page * 30).Take(30)) 
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{elementId * elemCount} 1", AnchorMax = $"{(elementId + 1) * elemCount} 1", OffsetMin = $"{(elementId == 0 ? "0" : "10")} {topMargin - pHeight}", OffsetMax = $"{(elementId == 4 ? "0" : "-5")} {topMargin}" },
                        Image = {Color = check.Value > 0 ? "0.78 0.74 0.7 0.01" : "0.78 0.74 0.7 0.05", Material = ""}
                    }, Layer + ".HRPStore", Layer + ".R" + check.Key);

                    if (check.Value > 0)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + ".R" + check.Key,
                            Components =
                            {
                                new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "LcokIcon"), Color = "1 1 1 0.1" },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                            }
                        });
                    }
                    
					//Puts(check.Key+" "+(string) ImageLibrary.Call("GetImage", check.Key, (ulong) 256));
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".R" + check.Key,
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", check.Key, (ulong) 256) },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "-35 10", OffsetMax = "35 -10" }
                        }
                    });
                    
                    string color = check.Value > 0 ? "0.549 0.270 0.215 1" : "0.294 0.38 0.168 1";
                    container.Add(new CuiPanel  
                    {
                        RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-60 -5", OffsetMax = $"5 12" },
                        Image = { Color = color }
                    }, Layer + ".R" + check.Key, Layer + ".R" + check.Key + ".L");

                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = $"{(check.Value > 0 ? -5 : 0)} 0"},
                        Text = {Text = check.Value > 0 ? TimeSpan.FromSeconds(check.Value).ToShortString() : "ДОСТУПНО", Align = check.Value > 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = check.Value > 0 ? 12 : 11, Color = check.Value > 0 ? "0.98 0.807 0.439 1" : "0.65 0.89 0.24 1" }
                    }, Layer + ".R" + check.Key + ".L");
                    if (check.Value > 0)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "4 4", OffsetMax = "13 -3" },
                            Button = { Color = "0.98 0.807 0.439 1", Sprite = "assets/icons/bp-lock.png" },
                            Text = { Text = "" }
                        }, Layer + ".R" + check.Key + ".L");
                    }
                    
                    
                    elementId++;
                    if (elementId == 5)
                    {
                        elementId = 0;

                        topMargin -= pHeight + 15;
                    }
                }
                
            #region PaginationMember

            string leftCommand = $"UI_WipeBlock page {section} {page - 1}"; 
            string rightCommand = $"UI_WipeBlock page {section} {page + 1}";
            bool leftActive = page > 0;
            bool rightActive = (page + 1) * 30 < itemList.Count; 
  
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = $"-256 15", OffsetMax = "256 60" },
                Image = { Color = "0 0 0 0" } 
            }, Layer + ".R", Layer + ".PS");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.49 1", OffsetMin = $"0 0", OffsetMax = "-0 -0" },
                Image = { Color = leftActive ? "0.294 0.38 0.168 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS", Layer + ".PS.L");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b>НАЗАД</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS.L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.51 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = "-0 -0" },
                Image = { Color = rightActive ? "0.294 0.38 0.168 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS", Layer + ".PS.R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>ВПЕРЁД</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS.R");

            #endregion

            CuiHelper.AddUi(player, container);
        }

        private IEnumerator StartUpdate(BasePlayer player)
        {
            while (player.HasFlag(BaseEntity.Flags.Reserved3))
            {
                foreach (var check in settings.SBlock.BlockItems.SelectMany(p => p.Value))
                { 
                    CuiElementContainer container = new CuiElementContainer();
                    var blockedItem = ItemManager.FindItemDefinition(check);
                    CuiHelper.DestroyUi(player, $"Time.{blockedItem.shortname}.Update");

                    var unblockTime = IsBlocked(blockedItem);
                    
                    string text = unblockTime > 0
                            ? $"<size=10>ОСТАЛОСЬ</size>\n<size=14>{TimeSpan.FromSeconds(unblockTime).ToShortString()}</size>"
                            : "<size=11>ДОСТУПНО</size>";
                
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text          = { Text        = text, FontSize   = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter},
                        Button        = { Color     = "0 0 0 0" }, 
                    }, $"Time.{blockedItem.shortname}", $"Time.{blockedItem.shortname}.Update");
                    
                    CuiHelper.AddUi(player, container);
                }
                yield return new WaitForSeconds(1);
            }
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

        private void DrawInstanceBlock(BasePlayer player, Item item)
        {
            /*CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();
            string inputText = "Предмет {name} временно заблокирован,\nподождите {1}".Replace("{name}", item.info.displayName.english).Replace("{1}", $"{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked(item.info)).TotalHours))} час {TimeSpan.FromSeconds(IsBlocked(item.info)).Minutes} минут.");
            
            container.Add(new CuiPanel
            {
                FadeOut = 1f,
                Image = { FadeIn = 1f, Color = "0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.62 0.95" },
                CursorEnabled = false
            }, "Overlay", Layer);
            
            container.Add(new CuiElement
            {
                FadeOut = 1f,
                Parent = Layer,
                Name = Layer + ".Hide",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Hide",
                Name = Layer + ".Destroy1",
                FadeOut = 1f,
                Components =
                {
                    new CuiImageComponent { Color = "0.4 0.4 0.4 0.7"},
                    new CuiRectTransformComponent { AnchorMin = "0 0.62", AnchorMax = "1.1 0.85" }
                }
                
            });
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Color = "0.9 0.9 0.9 1", Text = "ПРЕДМЕТ ЗАБЛОКИРОВАН", FontSize = 22, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, Layer + ".Destroy1", Layer + ".Destroy5");
            container.Add(new CuiButton
            {
                FadeOut = 1f,
                RectTransform = { AnchorMin = "0 0.29", AnchorMax = "1.1 0.61" },
                Button = {FadeIn = 1f, Color = "0.3 0.3 0.3 0.5" },
                Text = { Text = "" }
            }, Layer + ".Hide", Layer + ".Destroy2");
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Text = inputText, FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.85 0.85 0.85 1" , Font = "robotocondensed-regular.ttf"},
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "10 0.9" }
            }, Layer + ".Hide", Layer + ".Destroy3");
            CuiHelper.AddUi(player, container);*/
        }

        #endregion

        #region Functions

        private string GetGradient(int t)
        {
            var LeftTime = UnBlockTime(t) - CurrentTime();
            return Gradients[Math.Min(99, Math.Max(Convert.ToInt32((float) LeftTime / t * 100), 0))];
        }

        private double IsBlockedCategory(int t) => IsBlocked(settings.SBlock.BlockItems.ElementAt(t).Value.First());
        private bool IsAnyBlocked() => UnBlockTime(settings.SBlock.BlockItems.Last().Key) + settings.SBlock.TimeMove > CurrentTime();
        private static double IsBlocked(string shortname) 
        {
            if (!settings.SBlock.BlockItems.SelectMany(p => p.Value).Contains(shortname))
                return 0;

            var blockTime = settings.SBlock.BlockItems.FirstOrDefault(p => p.Value.Contains(shortname)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            
            return lefTime > 0 ? lefTime : 0;
        }

        private static double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount + settings.SBlock.TimeMove;

        private static double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);

        private void FillBlockedItems(Dictionary<string, Dictionary<Item, string>> fillDictionary)
        {
            foreach (var category in settings.SBlock.BlockItems)
            {
                string categoryColor = GetGradient(category.Key);
                foreach (var item in category.Value)
                {
                    Item createItem = ItemManager.CreateByPartialName(item);
                    string catName = settings.SBlock.CategoriesName[createItem.info.category.ToString()];
                
                    if (!fillDictionary.ContainsKey(catName))
                        fillDictionary.Add(catName, new Dictionary<Item, string>());
                
                    if (!fillDictionary[catName].ContainsKey(createItem))
                        fillDictionary[catName].Add(createItem, categoryColor);
                }
            }
        }

        #endregion

        #region Utils

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
        
        public static string ToShortString(TimeSpan timeSpan)
        {
            int i = 0;
            string resultText = "";
            if (timeSpan.Days > 0)
            {
                resultText += timeSpan.Days + " День";
                i++;
            }
            if (timeSpan.Hours > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Час";
                i++;
            }
            if (timeSpan.Minutes > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Мин.";
                i++;
            }
            if (timeSpan.Seconds > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Сек.";
                i++;
            }

            return resultText;
        }
        
        private void GetConfig<T>(string menu, string key, ref T varObject)
        {
            if (Config[menu, key] != null)
            {
                varObject = Config.ConvertValue<T>(Config[menu, key]);
            }
            else
            {
                Config[menu, key] = varObject;
            }
        }
        
        #endregion
    }
}