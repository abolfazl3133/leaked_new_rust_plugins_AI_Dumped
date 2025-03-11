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
    [Info("SkillSystem", "Chibubrik", "1.2.0")]
    class SkillSystem : RustPlugin
    {
        #region Вар
        private string Layer = "Skill_UI";
        private string LayersAlert = "Alerts_UI";

        [PluginReference] private Plugin ImageLibrary;
        #endregion

        #region Класс
        public class SkillSettings
        {
            [JsonProperty("Название панельки")] public string Name;
            [JsonProperty("Информация")] public string Info;
            [JsonProperty("Название предмета")] public string DisplayName;
            [JsonProperty("Используемый предмет Glue")] public string ShortName;
            [JsonProperty("SkinId предмета")] public ulong SkinID;
            [JsonProperty("Шанс выпадения листка в %")] public float DropChance;
        }
        
        public class Settings
        {
            [JsonProperty("Название навыка")] public string DisplayName;
            [JsonProperty("Рейтинг увеличения навыка за уровень")] public float Rate;
            [JsonProperty("Сколько нужно листов с информацией, чтобы прокачать навык")] public int Price;
            [JsonProperty("Максимальный уровень прокачки навыка")] public int LevelMax;
            [JsonProperty("Изображение навыка")] public string Url;
            [JsonProperty("Короткое название предмета, на который будет действовать увеличенный рейтинг навыка")] public Dictionary<string, string> ShortName;
        }
        
        private Dictionary<ulong, Dictionary<string, SettingsData>> settingsData;
        public class SettingsData
        {
            [JsonProperty("Навык")] public int Level;
            [JsonProperty("Рейт")] public float Rate = 1;
        }
        #endregion

        #region Конфиг
        public Configuration config;
        public class Configuration
        {
            [JsonProperty("Настройки")] public SkillSettings skill = new SkillSettings();
            [JsonProperty("Список")] public List<Settings> settings;
            [JsonProperty("Список ящиков")] public List<string> container = new List<string>();
            public static Configuration GetNewCong()
            {
                return new Configuration
                {
                    skill = new SkillSettings()
                    {
                        DisplayName = "Листок с информацией",
                        ShortName = "glue",
                        SkinID = 1835496050,
                        DropChance = 100f
                    },
                    settings = new List<Settings>
                    {
                        new Settings()
                        {
                            DisplayName = "Гайд для лесоруба",
                            Rate = 0.1f,
                            Price = 100,
                            LevelMax = 10,
                            Url = "https://imgur.com/4q8snx9.png",
                            ShortName = new Dictionary<string, string>()
                                                         {
                                ["wood"] = "Дерево"
                            }
                        },                
                        new Settings()
                        {
                            DisplayName = "Дневник шахтера",
                            Rate = 0.1f,
                            Price = 100,
                            LevelMax = 10,
                            Url = "https://imgur.com/Lhyflw5.png",
                            ShortName = new Dictionary<string, string>()
                            {
                                ["stones"] = "Камень"
                            }
                        },        
                        new Settings()
                        {
                            DisplayName = "Добыча металла",
                            Rate = 0.1f,
                            Price = 100,
                            LevelMax = 10,
                            Url = "https://imgur.com/xAepxrq.png",
                            ShortName = new Dictionary<string, string>()
                            {
                                ["metal.ore"] = "Металл"
                            }
                        },   
                        new Settings()
                        {
                            DisplayName = "Серные камни",
                            Rate = 0.1f,
                            Price = 100,
                            LevelMax = 10,
                            Url = "https://imgur.com/6xqLfrQ.png",
                            ShortName = new Dictionary<string, string>()
                            {
                                ["sulfur.ore"] = "Сера"
                            }
                        },  
                        new Settings()
                        {
                            DisplayName = "Снятие шкур",
                            Rate = 0.1f,
                            Price = 100,
                            LevelMax = 10,
                            Url = "https://imgur.com/Nla51R6.png",
                            ShortName = new Dictionary<string, string>()
                            {
                                ["cloth"] = "Ткань"
                            }
                        },  
                        new Settings()
                        {
                            DisplayName = "Поиск предметов",
                            Rate = 0.1f,
                            Price = 100,
                            LevelMax = 10,
                            Url = "https://imgur.com/E46dylP.png",
                            ShortName = new Dictionary<string, string>()
                            {
                                ["Поиск"] = "Не трогать"
                            }
                        },
                    },
                    container = new List<string>()
                    {
                        { "crate_elite" },
                        { "supply_drop" },
                        { "crate_normal_2" }
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
                if (config?.container == null) LoadDefaultConfig();
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

        #region Команды
        [ChatCommand("skill")]
        private void CommandSkill(BasePlayer player)
        {
            SkillUI(player);
        }

        [ConsoleCommand("skill")]
        private void Command(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "buy")
                {
                    var check = config.settings.ElementAt(int.Parse(args.Args[1]));
                    var items = player.inventory.GetAmount(ItemManager.FindItemDefinition(config.skill.ShortName).itemid);
                    if (items >= check.Price) {  } else { return; }
                    foreach (var item in check.ShortName)
                    {
                        var skills = AddPlayersData(player.userID, item.Key);
                        if (skills.Level != check.LevelMax)
                        {
                            skills.Rate += check.Rate;
                            skills.Level += 1;
                            AlertUI(player, check.DisplayName);
                            player.inventory.Take(null, ItemManager.FindItemDefinition(config.skill.ShortName).itemid, check.Price);
                            CuiHelper.DestroyUi(player, Layer);
                        }
                        else
                        {
                            SendReply(player, "Вы полностью изучили навык!");
                        }
                    }
                }
                if (args.Args[0] == "give")
                {
                    if (player != null && !player.IsAdmin) return;
                    if (args.Args == null || args.Args.Length < 2)
                    {
                        player.ConsoleMessage("Команда: skill give SteamID количество листков");
                        return;
                    }
                    BasePlayer target = BasePlayer.Find(args.Args[1]);
                    if (target == null)
                    {
                        player.ConsoleMessage($"Игрок {target} не найден");
                        return;
                    }
                    int change;
                    if (!int.TryParse(args.Args[2], out change))
                    {
                        player.ConsoleMessage("Вы не указали кол-во");
                        return;
                    }

                    player.ConsoleMessage($"Игроку {target}, были успешно выданы листки с информацией.\nВ размере: {change}");
                    Item item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(config.skill.ShortName).itemid, change, config.skill.SkinID);
                    item.name = config.skill.DisplayName;
                    player.inventory.GiveItem(item);
                }
            }
        }
        #endregion

        #region Хуки
        private void Loaded()
        {
            settingsData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, SettingsData>>>("SkillSystem/Player");
        }
        
        private SettingsData AddPlayersData(ulong userID, string name)
        {
            if (!settingsData.ContainsKey(userID)) settingsData[userID] = new Dictionary<string, SettingsData>();

            if (!settingsData[userID].ContainsKey(name)) settingsData[userID][name] = new SettingsData();

            return settingsData[userID][name];
        }
        
        private void OnServerInitialized()
        {
            foreach (var check in config.settings)
            {
                ImageLibrary.Call("AddImage", check.Url, check.Url);
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                SaveData();
            }
        }
        
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SaveData();
        }
        
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SkillSystem/Player", settingsData);
        }
        
        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            var playerData = AddPlayersData(player.userID, item.info.shortname);
            item.amount = (int) (item.amount * playerData.Rate);
        }
        
        private void OnDispenserBonus(ResourceDispenser disp, BasePlayer player, Item item)
        {
            var playerData = AddPlayersData(player.userID, item.info.shortname);
            item.amount = (int) (item.amount * playerData.Rate);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity, Item item)
        {
            if (!(entity is LootContainer) || entity.OwnerID != 0)
                return;
            foreach (var check in ((LootContainer) entity).inventory.itemList.Where(p => p.MaxStackable() > 1 && !p.IsBlueprint()))
            {
                var playerData = AddPlayersData(player.userID, "Поиск");
                check.amount = (int) (check.amount * playerData.Rate);
            }
            entity.OwnerID = player.userID;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!(info?.Initiator is BasePlayer) || !(entity is LootContainer)) return;
            var inventory = entity.GetComponent<LootContainer>().inventory;
            foreach (var check in inventory.itemList.Where(p => p.MaxStackable() > 1 && !p.IsBlueprint()))
            {
                var playerData = AddPlayersData(info.InitiatorPlayer.userID, "Поиск");
                check.amount = (int) (check.amount * playerData.Rate);
            }
        }
        
        int GetList(BasePlayer player, string type)
        {
            int amount = 0;
            foreach (var item in player.inventory.FindItemIDs(ItemManager.FindItemDefinition(config.skill.ShortName).itemid))
            {
                if (type == "skill")
                {
                    if (item.info.itemid == ItemManager.FindItemDefinition(config.skill.ShortName).itemid)
                    {
                        amount += item.amount;
                    }
                }
            }
            return amount;
        }

        #region Спавн листочков
        private void OnLootSpawn(LootContainer lootContainer)
        {
            if (lootContainer == null) return;
            if (lootContainer.inventory == null) return;
            if (config.container.Contains(lootContainer.ShortPrefabName))
            {
                if (UnityEngine.Random.Range(0, 100) <= config.skill.DropChance)
                {
                    Item add = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(config.skill.ShortName).itemid, 1, config.skill.SkinID);
                    add.name = config.skill.DisplayName;
                    add.MoveToContainer(lootContainer.inventory);
                }
            }
        }

        object OnItemSplit(Item thisI, int split_Amount)
        {
            Item item = null;
            if (thisI.skin == 0uL) return null;
            if (thisI.skin == config.skill.SkinID)
            {
                thisI.amount -= split_Amount; item = ItemManager.CreateByItemID(thisI.info.itemid, split_Amount, thisI.skin);
                if (item != null)
                {
                    item.amount = split_Amount;
                    item.name = thisI.name;
                    item.OnVirginSpawn();
                    if (thisI.IsBlueprint()) item.blueprintTarget = thisI.blueprintTarget;
                    if (thisI.hasCondition) item.condition = thisI.condition;
                    item.MarkDirty();
                    return item;
                }
            }
            return null;
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item.info.itemid == ItemManager.FindItemDefinition(config.skill.ShortName).itemid && targetItem.info.itemid == ItemManager.FindItemDefinition(config.skill.ShortName).itemid) if (item.skin == config.skill.SkinID || targetItem.skin == config.skill.SkinID) if (targetItem.skin != item.skin) return false;
            return null;
        }
        #endregion

        #endregion

        #region Интерфейс
        private void SkillUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9" },
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-2 -2", AnchorMax = "2 2", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9", Close = Layer },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.28", AnchorMax = "1 0.325", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = $"ЛИСТКОВ С ИНФОРМАЦИЕЙ - {GetList(player, "skill").ToString()}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "robotocondensed-bold.ttf" }
            }, Layer);

            float gap = 0.01f, width = 0.155f, height = 0.4f, startxBox = 0.01f, startyBox = 0.73f - height, xmin = startxBox, ymin = startyBox;
            for (int i = 0; i < config.settings.Count(); i++)
            {
                container.Add(new CuiButton()
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1" },
                    Text = { Text = "" }
                }, Layer, $"{i}");
                xmin += width + gap;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + gap;
                }

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.21", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0" },
                    Text = { Text = $"", Color = HexToUiColor("#FFFFFF5A"), Align = TextAnchor.UpperCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, $"{i}", "Skills");
                
                foreach (var check in config.settings.ElementAt(i).ShortName)
                {
                    var data = AddPlayersData(player.userID, check.Key);
                    var text = config.settings.ElementAt(i).LevelMax <= data.Level ? "ИЗУЧЕНО" : $"УРОВЕНЬ МАСТЕРСТВА {data.Level + "/" + config.settings.ElementAt(i).LevelMax}";
                    var color = config.settings.ElementAt(i).LevelMax <= data.Level ? "1 1 1 1" : $"1 1 1 0.3";
                    var colorbutton = config.settings.ElementAt(i).LevelMax <= data.Level ? "0 0 0 0" : $"0.43 0.81 0.53 0.6";

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0.21", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Button = { Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, $"{i}", "Images");

                    container.Add(new CuiElement
                    {
                        Parent = $"Images",
                        Components =
                        {
                            new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", config.settings.ElementAt(i).Url), Color = color},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "3 5", OffsetMax = "-3 -5"}
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.03 0.1", AnchorMax = "0.4 0.2", OffsetMax = "0 0" },
                        Button = { Color = "1 1 1 0.1" },
                        Text = { Text = $"Цена: {config.settings.ElementAt(i).Price}", Color = HexToUiColor("#FFFFFF5A"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                    }, $"{i}");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.415 0.1", AnchorMax = "0.97 0.2", OffsetMax = "0 0" },
                        Button = { Color = "0.43 0.81 0.53 0.6", Command = $"skill buy {i}" },
                        Text = { Text = "ИЗУЧИТЬ", Color = HexToUiColor("#FFFFFF5A"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-bold.ttf" }
                    }, $"{i}");

                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0.03 0.025", AnchorMax = "0.97 0.09", OffsetMax = "0 0"},
                        Button = {Color = "1 1 1 0.1"},
                        Text = {Text = ""}
                    }, $"{i}", "Progress");
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = $"{(float) data.Level / config.settings.ElementAt(i).LevelMax} 1", OffsetMax = "0 0"},
                        Button = {Color = colorbutton },
                        Text = {Text = ""}
                    }, "Progress");

                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                        Button = {Color = "0 0 0 0"},
                        Text = {Text = text, Color = HexToUiColor("#FFFFFF5A"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf"}
                    }, "Progress");
                }
            }

            CuiHelper.AddUi(player, container);
        }

        private void AlertUI(BasePlayer player, string DisplayName)
        {
            CuiHelper.DestroyUi(player, LayersAlert);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 180", OffsetMax = "400 280" },
                Image = { Color = "0 0 0 0" },
            }, "Hud", LayersAlert);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = $"<color=#FFFFFF9A><size=20><b>НАВЫКИ</b></size>\nВы изучили навык: <b>{DisplayName.ToUpper()}</b></color>\nОткрыть меню <b>скилов</b> можно прописав команду <b>/skill</b>", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, LayersAlert);

            CuiHelper.AddUi(player, container);
            timer.Once(10, () => { CuiHelper.DestroyUi(player, LayersAlert); });
        }
        #endregion

        #region Хелпер
        private static string HexToUiColor(string hex)
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

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }
        #endregion
    }
}