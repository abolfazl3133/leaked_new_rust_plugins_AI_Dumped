// Reference: EasyAntiCheat.Server

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ConVar;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Object = UnityEngine.Object;
using Pool = Facepunch.Pool;
using Random = UnityEngine.Random;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("Skills", "Mevent", "1.29.1")]
    [Description("Adds a system of skills")]
    public class Skills : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary, Notifications, Notify, UINotify, TimedPermissions;

        private const string Layer = "Com.Mevent.Main";

        private static Skills _instance;

        private readonly Dictionary<BasePlayer, RecoverComponent> _recoverPlayers =
            new Dictionary<BasePlayer, RecoverComponent>();

        private readonly List<uint> _containers = new List<uint>();

        private readonly List<FurnaceController> _furnaceControllers = new List<FurnaceController>();

        private readonly Dictionary<SkillType, Skill> _skillByType = new Dictionary<SkillType, Skill>();

        private enum SkillType
        {
            Wood,
            Stones,
            Metal,
            Sulfur,
            Attack,
            Secure,
            Regeneration,
            Metabolism,
            ComponentsRates,
            StandUpChance,
            CraftSpeed,
            FastOven,
            Kits,
            None,
            Cloth,
            Butcher,
            Scrap
        }

        #endregion

        #region Config

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Разрешение | Permission (example: skills.use)")]
            public readonly string Permission = string.Empty;

            [JsonProperty(PropertyName = "Команда | Command")]
            public readonly string Command = "skills";

            [JsonProperty(PropertyName = "Автоматический вайп? | Automatic wipe?")]
            public readonly bool Wipe = true;

            [JsonProperty(PropertyName = "Work with Notify?")]
            public readonly bool UseNotify = true;

            [JsonProperty(PropertyName = "Экономика | Economy")]
            public readonly EconomyConf Economy = new EconomyConf
            {
                Type = EconomyType.Plugin,
                AddHook = "Deposit",
                BalanceHook = "Balance",
                RemoveHook = "Withdraw",
                Plug = "Economics",
                ShortName = "scrap",
                DisplayName = string.Empty,
                Skin = 0
            };

            [JsonProperty(PropertyName = "Оповещение о максимальном уровне | Maximum level alert")]
            public readonly INotify MaxLevel = new INotify
            {
                Image = "warning",
                Url = "https://i.imgur.com/p3tKXJV.png",
                Delay = 0.9f
            };

            [JsonProperty(PropertyName = "Оповещение о нехватке баланса | Out of balance alert")]
            public readonly INotify NotMoney = new INotify
            {
                Image = "warning",
                Url = "https://i.imgur.com/p3tKXJV.png",
                Delay = 0.9f
            };

            [JsonProperty(PropertyName = "Фон | Background")]
            public readonly IPanel Background = new IPanel
            {
                AnchorMin = "0 0", AnchorMax = "1 1",
                OffsetMin = "0 0", OffsetMax = "0 0",
                Image = string.Empty,
                Color = new IColor("#0D1F4E", 95),
                isRaw = false,
                Sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                Material = "Assets/Icons/IconMaterial.mat"
            };

            [JsonProperty(PropertyName = "Заглавие | Title")]
            public readonly IText Title = new IText
            {
                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                OffsetMin = "-150 300", OffsetMax = "150 360",
                Font = "robotocondensed-bold.ttf",
                Align = TextAnchor.MiddleCenter,
                FontSize = 38,
                Color = new IColor("#FFFFFF", 100)
            };

            [JsonProperty(PropertyName = "Назад | Back")]
            public readonly IText Back = new IText
            {
                AnchorMin = "0 0.5", AnchorMax = "0 0.5",
                OffsetMin = "0 -40", OffsetMax = "65 40",
                Font = "robotocondensed-bold.ttf",
                Align = TextAnchor.MiddleCenter,
                FontSize = 60,
                Color = new IColor("#FFFFFF", 100)
            };

            [JsonProperty(PropertyName = "Вперёд | Next")]
            public readonly IText Next = new IText
            {
                AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                OffsetMin = "-65 -40", OffsetMax = "0 40",
                Font = "robotocondensed-bold.ttf",
                Align = TextAnchor.MiddleCenter,
                FontSize = 60,
                Color = new IColor("#FFFFFF", 100)
            };


            [JsonProperty(PropertyName = "Баланс | Balance")]
            public readonly IText BalanceText = new IText
            {
                AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                OffsetMin = "-150 0", OffsetMax = "150 75",
                Font = "robotocondensed-regular.ttf",
                Align = TextAnchor.MiddleCenter,
                FontSize = 24,
                Color = new IColor("#FFFFFF", 100)
            };

            [JsonProperty(PropertyName = "Включить иконку баланса | Enable Balance Icon")]
            public bool EnableBalanceIcon;

            [JsonProperty(PropertyName = "Иконка Баланса | Balance Icon")]
            public readonly BalanceIcon BalanceIcon = new BalanceIcon
            {
                OffsetMinY = 20,
                OffsetMaxY = 55,
                Length = 35
            };

            [JsonProperty(PropertyName = "Закрыть | Close")]
            public readonly IText Close = new IText
            {
                AnchorMin = "1 1", AnchorMax = "1 1",
                OffsetMin = "-35 -35", OffsetMax = "-5 -5",
                Font = "robotocondensed-bold.ttf",
                Align = TextAnchor.MiddleCenter,
                FontSize = 24,
                Color = new IColor("#FFFFFF", 100)
            };

            [JsonProperty(PropertyName = "Способность | Skill Panel")]
            public readonly SkillPanel SkillPanel = new SkillPanel
            {
                Background = new SIPanel
                {
                    Image = string.Empty,
                    Color = new IColor("#1D3676", 98),
                    isRaw = false,
                    Sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                    Material = "Assets/Icons/IconMaterial.mat",
                    HeightCorrect = 5
                },
                Image = new InterfacePosition
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = "10 10", OffsetMax = "160 160"
                },
                Title = new IText
                {
                    AnchorMin = "0 0", AnchorMax = "1 0",
                    OffsetMin = "165 135", OffsetMax = "-5 160",
                    Align = TextAnchor.MiddleLeft,
                    FontSize = 20,
                    Font = "robotocondensed-bold.ttf",
                    Color = new IColor("#FFFFFF", 100)
                },
                Description = new IText
                {
                    AnchorMin = "0 0", AnchorMax = "1 0",
                    OffsetMin = "165 30", OffsetMax = "-5 130",
                    Align = TextAnchor.UpperLeft,
                    FontSize = 14,
                    Font = "robotocondensed-regular.ttf",
                    Color = new IColor("#FFFFFF", 100)
                },
                Button = new IButton
                {
                    AnchorMin = "1 0", AnchorMax = "1 0",
                    OffsetMin = "-105 10", OffsetMax = "-10 30",
                    AColor = new IColor("#B5FFC9", 100),
                    DColor = new IColor("#B5FFC9", 25),
                    TextUi = new IText
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 0", OffsetMax = "0 0",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 14,
                        Font = "robotocondensed-regular.ttf",
                        Color = new IColor("#1D3676", 100)
                    }
                },
                Cost = new IText
                {
                    AnchorMin = "1 0", AnchorMax = "1 0",
                    OffsetMin = "-150 10", OffsetMax = "-110 30",
                    Align = TextAnchor.MiddleRight,
                    FontSize = 14,
                    Font = "robotocondensed-regular.ttf",
                    Color = new IColor("#FFFFFF", 100)
                },
                AddCost = "$",
                EnableCostImage = false,
                AddCostImage = new IPanel
                {
                    AnchorMin = "1 0", AnchorMax = "1 0",
                    OffsetMin = "-130 10", OffsetMax = "-110 30",
                    Color = new IColor("#FFFFFF", 100),
                    isRaw = true,
                    Image = "https://i.imgur.com/5GdD0cU.png",
                    Sprite = string.Empty,
                    Material = string.Empty
                },
                Stages = new IProgress
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = "160 10", OffsetMax = "160 10",
                    Height = 20,
                    Weidth = 10,
                    Margin = 5,
                    AColor = new IColor("#8C70D6", 100),
                    DColor = new IColor("#8C70D6", 25)
                },
                Count = 6,
                Height = 170,
                Width = 565,
                Margin = 20
            };

            [JsonProperty(PropertyName = "Способности | Skills",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<Skill> Skills = new List<Skill>
            {
                new Skill
                {
                    Type = SkillType.Wood,
                    Image = "https://gspics.org/images/2020/09/02/xz6Fy.png",
                    Title = "Дровосек",
                    Description = "Отвечает за добычу дерева\nИзучив способность, рейт добычи увеличивается на x1",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 133, 0),
                        [2] = new StageConf(25, 164, 0),
                        [3] = new StageConf(40, 200, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Stones,
                    Image = "https://gspics.org/images/2020/09/02/xz9mX.png",
                    Title = "Камнедобыча",
                    Description = "Отвечает за добычу камня\nИзучив способность, рейт добычи увеличивается на x1",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 133, 0),
                        [2] = new StageConf(25, 164, 0),
                        [3] = new StageConf(40, 200, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Metal,
                    Image = "https://gspics.org/images/2020/09/02/xznT3.png",
                    Title = "Чернорабочий",
                    Description =
                        "Отвечает за добычу железной руды\nИзучив способность, рейт добычи увеличивается на x1",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 133, 0),
                        [2] = new StageConf(25, 164, 0),
                        [3] = new StageConf(40, 200, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Sulfur,
                    Image = "https://gspics.org/images/2020/09/02/xz4te.png",
                    Title = "Серодобытчик",
                    Description = "Отвечает за добычу серной руды\nИзучив способность, рейт добычи увеличивается на x1",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 133, 0),
                        [2] = new StageConf(25, 164, 0),
                        [3] = new StageConf(40, 200, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Attack,
                    Image = "https://gspics.org/images/2020/09/02/xzXza.png",
                    Title = "Нападение",
                    Description =
                        "Изменяет величину урона любого оружия\nИзучив способность, Вы увеличите свой урон на 15%",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(20, 5, 0),
                        [2] = new StageConf(35, 10, 0),
                        [3] = new StageConf(50, 15, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Secure,
                    Image = "https://gspics.org/images/2020/09/02/xzAfi.png",
                    Title = "Защита",
                    Description =
                        "Изменяет величину урона любого оружия\nИзучив способность, Вы увеличите свою защиту на 15%",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(20, 5, 0),
                        [2] = new StageConf(35, 10, 0),
                        [3] = new StageConf(50, 15, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Regeneration,
                    Image = "https://gspics.org/images/2020/09/02/xzGrO.png",
                    Title = "Регенерация",
                    Description =
                        "Скоростная регенерация здоровья\nИзучив способность, Вы учеличите скорость регенерации на 90%",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 50, 0),
                        [2] = new StageConf(15, 70, 0),
                        [3] = new StageConf(20, 90, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Metabolism,
                    Image = "https://gspics.org/images/2020/09/02/xzWdI.png",
                    Title = "Метаболизм",
                    Description =
                        "Изменяет восстановление жажды и голода\nИзучив способность, Вы получите восполнение каллорий и гидратации",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 0.5f, 0),
                        [2] = new StageConf(15, 1, 0),
                        [3] = new StageConf(20, 2, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.ComponentsRates,
                    Image = "https://gspics.org/images/2020/09/02/xz15L.png",
                    Title = "Рейты на компоненты",
                    Description =
                        "Изменяет рейты добываемых компонентов\nИзучив способность, найденные компоненты будут x4",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 2, 0),
                        [2] = new StageConf(15, 3, 0),
                        [3] = new StageConf(20, 4, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.StandUpChance,
                    Image = "https://gspics.org/images/2020/09/02/xzTDD.png",
                    Title = "Увеличение шанса встать",
                    Description =
                        "Увеличивает шанс подняться, когда вы ранены\nИзучив способность, шанс увеличивается с стандартных 20% до 50%",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 30, 40),
                        [2] = new StageConf(15, 40, 50),
                        [3] = new StageConf(20, 50, 60)
                    }
                },
                new Skill
                {
                    Type = SkillType.CraftSpeed,
                    Image = "https://i.imgur.com/fAti1Cj.png",
                    Title = "Скорость крафта",
                    Description =
                        "Ускоряет крафт.\nИзучив способность, вы сможете крафтить почти моментально",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 30, 0),
                        [2] = new StageConf(15, 40, 0),
                        [3] = new StageConf(20, 50, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Kits,
                    Image = "https://i.imgur.com/Log7sQR.png",
                    Title = "Ускорение наборов",
                    Description =
                        "Ускоряет задержку между получением наборов",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 30, 0),
                        [2] = new StageConf(15, 40, 0),
                        [3] = new StageConf(20, 50, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.FastOven,
                    Image = "https://i.imgur.com/IifHk5l.png",
                    Title = "Ускорение печей",
                    Description =
                        "Ускорение плавки в печах",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 133, 0),
                        [2] = new StageConf(25, 164, 0),
                        [3] = new StageConf(40, 200, 0)
                    }
                },
                new Skill
                {
                    ID = 0,
                    Type = SkillType.None,
                    Image = "https://i.imgur.com/RqdcAm0.png",
                    Title = "Сортировка",
                    Description =
                        "На каждом этапе открываются новые виды сортировки",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(25, 0, 0, new List<string>(), new List<string>(), new List<string>
                        {
                            "furnacesplitter.use"
                        }),
                        [2] = new StageConf(40, 0, 0, new List<string>(), new List<string>(), new List<string>
                        {
                            "activesort.use"
                        })
                    }
                },
                new Skill
                {
                    ID = 1,
                    Type = SkillType.None,
                    Image = "https://i.imgur.com/S4xulmj.png",
                    Title = "Телепортация",
                    Description =
                        "Вы телепортируетесь быстрее с каждым этапом",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(30, 0, 0, new List<string>(), new List<string>(), new List<string>
                        {
                            "nteleportation.vip"
                        }),
                        [2] = new StageConf(50, 0, 0, new List<string>(), new List<string>(), new List<string>
                        {
                            "nteleportation.premium"
                        }),
                        [2] = new StageConf(80, 0, 0, new List<string>(), new List<string>(), new List<string>
                        {
                            "nteleportation.deluxe"
                        })
                    }
                },
                new Skill
                {
                    Type = SkillType.Cloth,
                    Image = "https://i.imgur.com/5fixMch.png",
                    Title = "Сборщик конопли",
                    Description =
                        "Отвечает за сбор ткани\nИзучив способность, рейт добычи увеличивается на x1",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 133, 0),
                        [2] = new StageConf(25, 164, 0),
                        [3] = new StageConf(40, 200, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Butcher,
                    Image = "https://i.imgur.com/4WlHK7u.png",
                    Title = "Butcher",
                    Description =
                        "Отвечает за добычу животных\nИзучив способность, рейт добычи увеличивается на x1",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 133, 0),
                        [2] = new StageConf(25, 164, 0),
                        [3] = new StageConf(40, 200, 0)
                    }
                },
                new Skill
                {
                    Type = SkillType.Scrap,
                    Image = "https://i.imgur.com/SEvG4EU.png",
                    Title = "Scrap",
                    Description =
                        "Изменяет рейты добычи скрапа\nИзучив способность, найденный скрап будет x4",
                    Stages = new Dictionary<int, StageConf>
                    {
                        [1] = new StageConf(10, 2, 0),
                        [2] = new StageConf(15, 3, 0),
                        [3] = new StageConf(20, 4, 0)
                    }
                }
            };

            [JsonProperty(PropertyName = "Животные | Animals", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> Animals = new List<string>
            {
                "chicken.corpse",
                "boar.corpse",
                "bear.corpse",
                "wolf.corpse",
                "stag.corpse"
            };

            [JsonProperty(PropertyName = "Чёрный список печей | Ovens Black List",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> OvensBlackList = new List<string>
            {
                "entity shortnameprefab 1",
                "entity shortnameprefab 2"
            };

            [JsonProperty(PropertyName = "Чёрный список контейнеров | Containers Black List",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> ContainersBlackList = new List<string>
            {
                "entity prefab name 1",
                "entity prefab name 2"
            };

            [JsonProperty(PropertyName = "Чёрный список для навыка Attack | Blacklist for Attack skill",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> AttackBlackList = new List<string>
            {
                "entity prefab name 1",
                "entity prefab name 2"
            };

            public VersionNumber Version;
        }

        private class INotify
        {
            [JsonProperty(PropertyName = "Ключ изображения | Image Key")]
            public string Image;

            [JsonProperty(PropertyName = "Ссылка на изображение | Image Url")]
            public string Url;

            [JsonProperty(PropertyName = "Время показа | Show Time")]
            public float Delay;
        }

        private enum EconomyType
        {
            Plugin,
            Item
        }

        private class EconomyConf
        {
            [JsonProperty(PropertyName = "Тип (Plugin/Item)")] [JsonConverter(typeof(StringEnumConverter))]
            public EconomyType Type;

            [JsonProperty(PropertyName = "Функция пополнения баланса | Balance add hook")]
            public string AddHook;

            [JsonProperty(PropertyName = "Функция снятия баланса | Balance remove hook")]
            public string RemoveHook;

            [JsonProperty(PropertyName = "Функция показа баланса | Balance show hook")]
            public string BalanceHook;

            [JsonProperty(PropertyName = "Название плагина | Plugin name")]
            public string Plug;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Display Name (пусто - стандартное | empty - default)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            public double ShowBalance(BasePlayer player)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = _instance?.plugins?.Find(Plug);
                        if (plugin == null) return 0;

                        return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.userID)));
                    }
                    case EconomyType.Item:
                    {
                        return ItemCount(player.inventory.AllItems(), ShortName, Skin);
                    }
                    default:
                        return 0;
                }
            }

            public void AddBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = _instance?.plugins?.Find(Plug);
                        if (plugin == null) return;

                        switch (Plug)
                        {
                            case "Economics":
                                plugin.Call(AddHook, player.userID, amount);
                                break;
                            default:
                                plugin.Call(AddHook, player.userID, (int) amount);
                                break;
                        }

                        break;
                    }
                    case EconomyType.Item:
                    {
                        var am = (int) amount;

                        var item = ToItem(am);
                        if (item == null) return;

                        player.GiveItem(item);
                        break;
                    }
                }
            }

            public bool RemoveBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        if (ShowBalance(player) < amount) return false;

                        var plugin = _instance?.plugins.Find(Plug);
                        if (plugin == null) return false;

                        switch (Plug)
                        {
                            case "Economics":
                                plugin.Call(RemoveHook, player.userID, amount);
                                break;
                            default:
                                plugin.Call(RemoveHook, player.userID, (int) amount);
                                break;
                        }

                        return true;
                    }
                    case EconomyType.Item:
                    {
                        var playerItems = player.inventory.AllItems();
                        var am = (int) amount;

                        if (ItemCount(playerItems, ShortName, Skin) < am) return false;

                        Take(playerItems, ShortName, Skin, am);
                        return true;
                    }
                    default:
                        return false;
                }
            }

            private Item ToItem(int amount)
            {
                var item = ItemManager.CreateByName(ShortName, amount, Skin);
                if (item == null)
                {
                    Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

                return item;
            }

            private int ItemCount(Item[] items, string shortname, ulong skin)
            {
                return items.Where(item =>
                        item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
                    .Sum(item => item.amount);
            }

            private void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
            {
                var num1 = 0;
                if (iAmount == 0) return;

                var list = Pool.GetList<Item>();

                foreach (var item in itemList)
                {
                    if (item.info.shortname != shortname ||
                        skinId != 0 && item.skin != skinId || item.isBroken) continue;

                    var num2 = iAmount - num1;
                    if (num2 <= 0) continue;
                    if (item.amount > num2)
                    {
                        item.MarkDirty();
                        item.amount -= num2;
                        num1 += num2;
                        break;
                    }

                    if (item.amount <= num2)
                    {
                        num1 += item.amount;
                        list.Add(item);
                    }

                    if (num1 == iAmount)
                        break;
                }

                foreach (var obj in list)
                    obj.RemoveFromContainer();

                Pool.FreeList(ref list);
            }
        }

        private class Skill
        {
            [JsonProperty(PropertyName = "Включено | Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Разрешение | Permission")]
            public string Permission;

            [JsonProperty(PropertyName = "Тип | Type")] [JsonConverter(typeof(StringEnumConverter))]
            public SkillType Type;

            [JsonProperty(PropertyName = "ID (для/for None)")]
            public int ID;

            [JsonProperty(PropertyName = "Изображение | Image")]
            public string Image;

            [JsonProperty(PropertyName = "Название | Title")]
            public string Title;

            [JsonProperty(PropertyName = "Описание | Description")]
            public string Description;

            [JsonProperty(PropertyName = "Уровни | Levels", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<int, StageConf> Stages;
        }

        private class StageConf
        {
            [JsonProperty(PropertyName = "Стоимость | Cost")]
            public readonly float Cost;

            [JsonProperty(PropertyName =
                "Значение [метаболизм - значение, шанс встать - шанс, для всех остальных процент %] | VALUE")]
            public readonly float Value;

            [JsonProperty(PropertyName = "Значение 2")]
            public readonly float Value2;

            [JsonProperty(PropertyName = "Команды | Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> Commands;

            [JsonProperty(PropertyName = "Группы | Groups", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> Groups;

            [JsonProperty(PropertyName = "Разрешения | Permissions",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> Permissions;

            public StageConf(float cost, float value, float value2)
            {
                Cost = cost;
                Value = value;
                Value2 = value2;
                Commands = new List<string>();
                Groups = new List<string>();
                Permissions = new List<string>();
            }

            [JsonConstructor]
            public StageConf(float cost, float value, float value2, List<string> commands, List<string> groups,
                List<string> permissions)
            {
                Cost = cost;
                Value = value;
                Value2 = value2;
                Commands = commands;
                Groups = groups;
                Permissions = permissions;
            }
        }

        private class BalanceIcon
        {
            [JsonProperty(PropertyName = "Offset Min Y")]
            public float OffsetMinY;

            [JsonProperty(PropertyName = "Offset Max Y")]
            public float OffsetMaxY;

            [JsonProperty(PropertyName = "Длина | Length")]
            public float Length;
        }

        private class SkillPanel
        {
            [JsonProperty(PropertyName = "Фон | Background")]
            public SIPanel Background;

            [JsonProperty(PropertyName = "Изображение | Image")]
            public InterfacePosition Image;

            [JsonProperty(PropertyName = "Заглавие | Title")]
            public IText Title;

            [JsonProperty(PropertyName = "Описание | Description")]
            public IText Description;

            [JsonProperty(PropertyName = "Кнопка | Button")]
            public IButton Button;

            [JsonProperty(PropertyName = "Стоимость | Cost")]
            public IText Cost;

            [JsonProperty(PropertyName = "Приписка к стоимости | Add to cost")]
            public string AddCost;

            [JsonProperty(PropertyName = "Включить пририску к стоимости (изображение) | Enable Add to cost (image)")]
            public bool EnableCostImage;

            [JsonProperty(PropertyName = "Приписка к стоимости (изображение) | Add to cost (image)")]
            public IPanel AddCostImage;

            [JsonProperty(PropertyName = "Уровни | Levels")]
            public IProgress Stages;

            [JsonProperty(PropertyName = "Количество на страницу | Count On Page")]
            public int Count;

            [JsonProperty(PropertyName = "Высота | Height")]
            public float Height;

            [JsonProperty(PropertyName = "Ширина | Width")]
            public float Width;

            [JsonProperty(PropertyName = "Отступ | Margin")]
            public float Margin;

            public void Get(ref CuiElementContainer container, string layer, int page, string parent, int index,
                BasePlayer player, Skill skill,
                float oMinX, float oMinY, float oMaxX, float oMaxY)
            {
                Background?.Get(ref container, parent, parent + $".Skill.{index}", oMinX, oMinY, oMaxX, oMaxY);

                container.Add(new CuiElement
                {
                    Parent = parent + $".Skill.{index}",
                    Components =
                    {
                        new CuiRawImageComponent
                            {Png = _instance?.ImageLibrary?.Call<string>("GetImage", skill.Image)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = Image.AnchorMin, AnchorMax = Image.AnchorMax, OffsetMin = Image.OffsetMin,
                            OffsetMax = Image.OffsetMax
                        }
                    }
                });

                Title?.Get(ref container, parent + $".Skill.{index}", null, skill.Title);

                Description?.Get(ref container, parent + $".Skill.{index}", null, skill.Description);

                var cost = _instance?.GetNextPrice(player.userID, skill) ?? -1;

                Button?.Get(ref container, player, parent + $".Skill.{index}", null,
                    $"UI_Skills upgrade {skill.Type} {page} {layer} {skill.ID}", "", cost > 0);

                if (cost > 0) Cost?.Get(ref container, parent + $".Skill.{index}", null, $"{cost}{AddCost}");

                #region Stages

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = Stages.AnchorMin, AnchorMax = Stages.AnchorMax,
                        OffsetMin = Stages.OffsetMin, OffsetMax = Stages.OffsetMax
                    },
                    Image = {Color = "0 0 0 0"}
                }, parent + $".Skill.{index}", parent + $".Skill.{index}.Stages");

                var skillData = _instance?.GetPlayerData(player.userID)?.Skills
                    ?.FirstOrDefault(x => x.Type == skill.Type);

                var xSwitch = 0f;
                foreach (var i in skill.Stages.Keys)
                {
                    var color = skillData != null && i <= skillData.Stage ? Stages.AColor : Stages.DColor;

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = $"{xSwitch} 0", OffsetMax = $"{xSwitch + Stages.Weidth} {Stages.Height}"
                        },
                        Image = {Color = color.Get()}
                    }, parent + $".Skill.{index}.Stages", parent + $".Skill.{index}.Stages.{i}");

                    xSwitch += Stages.Weidth + Stages.Margin;
                }

                #endregion
            }
        }

        private class IProgress : InterfacePosition
        {
            [JsonProperty(PropertyName = "Высота | Height")]
            public float Height;

            [JsonProperty(PropertyName = "Ширина | Weidth")]
            public float Weidth;

            [JsonProperty(PropertyName = "Отступ | Margin")]
            public float Margin;

            [JsonProperty(PropertyName = "Активный цвет | Active Color")]
            public IColor AColor;

            [JsonProperty(PropertyName = "Не активный цвет | No Active Color")]
            public IColor DColor;
        }

        private class IButton : InterfacePosition
        {
            [JsonProperty(PropertyName = "Цвет | Color")]
            public IColor AColor;

            [JsonProperty(PropertyName = "Не активный цвет | No Active Color")]
            public IColor DColor;

            [JsonProperty(PropertyName = "Настройка текста | Text Setting")]
            public IText TextUi;

            public void Get(ref CuiElementContainer container, BasePlayer player, string parent, string name = null,
                string cmd = "",
                string close = "", bool color = true)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin,
                        OffsetMax = OffsetMax
                    },
                    Image = {Color = color ? AColor.Get() : DColor.Get()}
                }, parent, name);

                TextUi?.Get(ref container, name, null, _instance.Msg(Upgrade, player.UserIDString));

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""},
                    Button =
                    {
                        Command = cmd,
                        Close = close,
                        Color = "0 0 0 0"
                    }
                }, name);
            }
        }

        private class SIPanel
        {
            [JsonProperty(PropertyName = "Изображение | Image")]
            public string Image;

            [JsonProperty(PropertyName = "Цвет | Color")]
            public IColor Color;

            [JsonProperty(PropertyName = "Сохранять цвет изображения? | Save Image Color")]
            public bool isRaw;

            [JsonProperty(PropertyName = "Sprite")]
            public string Sprite;

            [JsonProperty(PropertyName = "Material")]
            public string Material;

            [JsonProperty(PropertyName = "Отклонение по высоте | Height deviation")]
            public float HeightCorrect;

            public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null,
                float oMinX = 0, float oMinY = 0, float oMaxX = 0, float oMaxY = 0)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                if (isRaw)
                    container.Add(new CuiElement
                    {
                        Name = name,
                        Parent = parent,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = !string.IsNullOrEmpty(Image)
                                    ? _instance.ImageLibrary.Call<string>("GetImage", Image)
                                    : null,
                                Color = Color.Get(),
                                Material = Material,
                                Sprite = !string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Icons/rust.png"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                                OffsetMin = $"{oMinX} {oMinY + HeightCorrect}",
                                OffsetMax = $"{oMaxX} {oMaxY + HeightCorrect}"
                            }
                        }
                    });
                else
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                            OffsetMin = $"{oMinX} {oMinY + HeightCorrect}",
                            OffsetMax = $"{oMaxX} {oMaxY + HeightCorrect}"
                        },
                        Image =
                        {
                            Png = !string.IsNullOrEmpty(Image)
                                ? _instance.ImageLibrary.Call<string>("GetImage", Image)
                                : null,
                            Color = Color.Get(),
                            Sprite =
                                !string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Content/UI/UI.Background.Tile.psd",
                            Material = !string.IsNullOrEmpty(Material) ? Material : "Assets/Icons/IconMaterial.mat"
                        }
                    }, parent, name);
            }
        }

        private class InterfacePosition
        {
            public string AnchorMin;

            public string AnchorMax;

            public string OffsetMin;

            public string OffsetMax;
        }

        private class IColor
        {
            [JsonProperty(PropertyName = "HEX")] public string HEX;

            [JsonProperty(PropertyName = "Непрозрачность | Opacity (0 - 100)")]
            public readonly float Alpha;

            public string Get()
            {
                if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

                var str = HEX.Trim('#');
                if (str.Length != 6) throw new Exception(HEX);
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
            }

            public IColor(string hex, float alpha)
            {
                HEX = hex;
                Alpha = alpha;
            }
        }

        private class IPanel : InterfacePosition
        {
            [JsonProperty(PropertyName = "Изображение | Image")]
            public string Image;

            [JsonProperty(PropertyName = "Цвет | Color")]
            public IColor Color;

            [JsonProperty(PropertyName = "Сохранять цвет изображения? | Save Image Color")]
            public bool isRaw;

            [JsonProperty(PropertyName = "Sprite")]
            public string Sprite;

            [JsonProperty(PropertyName = "Material")]
            public string Material;

            public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null,
                bool cursor = false)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                if (isRaw)
                {
                    var element = new CuiElement
                    {
                        Name = name,
                        Parent = parent,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = !string.IsNullOrEmpty(Image)
                                    ? _instance.ImageLibrary.Call<string>("GetImage", Image)
                                    : null,
                                Color = Color.Get(),
                                Material = Material,
                                Sprite = !string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Icons/rust.png"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin,
                                OffsetMax = OffsetMax
                            }
                        }
                    };

                    if (cursor) element.Components.Add(new CuiNeedsCursorComponent());

                    container.Add(element);
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax
                        },
                        Image =
                        {
                            Png = !string.IsNullOrEmpty(Image)
                                ? _instance.ImageLibrary.Call<string>("GetImage", Image)
                                : null,
                            Color = Color.Get(),
                            Sprite =
                                !string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Content/UI/UI.Background.Tile.psd",
                            Material = !string.IsNullOrEmpty(Material) ? Material : "Assets/Icons/IconMaterial.mat"
                        },
                        CursorEnabled = cursor
                    }, parent, name);
                }
            }
        }

        private class IText : InterfacePosition
        {
            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize;

            [JsonProperty(PropertyName = "Font")] public string Font;

            [JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor Align;

            [JsonProperty(PropertyName = "Text Color")]
            public IColor Color;

            public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null,
                string text = "", bool enableIcon = false)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiLabel
                {
                    RectTransform =
                        {AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax},
                    Text =
                    {
                        Text = $"{text}", Align = Align, FontSize = FontSize, Color = Color.Get(),
                        Font = Font
                    }
                }, parent, name);

                if (enableIcon)
                {
                    var length = text.Length * FontSize * 0.225f;

                    container.Add(new CuiElement
                    {
                        Parent = parent,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = _instance.ImageLibrary.Call<string>("GetImage",
                                    _config.SkillPanel.AddCostImage.Image)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = AnchorMin,
                                AnchorMax = AnchorMax,
                                OffsetMin = $"{length} {_config.BalanceIcon.OffsetMinY}",
                                OffsetMax = $"{length + _config.BalanceIcon.Length} {_config.BalanceIcon.OffsetMaxY}"
                            }
                        }
                    });
                }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();

                if (_config.Version < Version)
                    UpdateConfigValues();

                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            if (_config.Version == default(VersionNumber) && _config.Version < new VersionNumber(1, 27, 0))
                _config.Skills.ForEach(skill => skill.Enabled = true);

            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data

        private static PluginData _data;

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            [JsonProperty(PropertyName = "Skills", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<SkillData> Skills = new List<SkillData>();

            [JsonProperty(PropertyName = "Permissions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly HashSet<string> Permissions = new HashSet<string>();

            [JsonProperty(PropertyName = "Groups", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly HashSet<string> Groups = new HashSet<string>();

            public SkillData GetSkill(SkillType type, int id = 0)
            {
                if (type == SkillType.None)
                    foreach (var skillData in Skills.FindAll(x => x.Type == SkillType.None && x.ID == id))
                    {
                        if (skillData.Skill == null)
                            skillData.Skill = _config.Skills.Find(x => x.Type == SkillType.None && x.ID == id);

                        return skillData;
                    }
                else
                    foreach (var data in Skills)
                    {
                        var skill = _instance.FindByType(data.Type);
                        if (skill == null || skill.Type != type) continue;

                        data.Skill = skill;
                        return data;
                    }

                return null;
            }

            public void AddSkill(BasePlayer player, Skill skill, int stage)
            {
                Skills.Add(new SkillData
                {
                    ID = skill.ID,
                    Type = skill.Type,
                    Stage = stage,
                    Skill = skill
                });

                CheckSkill(player, skill, stage);
            }

            public void UpgradeSkill(SkillData data, BasePlayer player, Skill skill, int stage)
            {
                data.Stage = stage;

                CheckSkill(player, skill, stage);
            }

            private void CheckSkill(BasePlayer player, Skill skill, int stage)
            {
                if (skill.Type == SkillType.None)
                {
                    GetCommands(player, skill, stage);

                    GrantPermissions(player, skill, stage);

                    GrantGroups(player, skill, stage);
                }
            }

            private void GetCommands(BasePlayer player, Skill skill, int stage)
            {
                skill.Stages[stage]?.Commands?.ForEach(command =>
                {
                    command = command
                        .Replace("\n", "|")
                        .Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase)
                        .Replace("%username%", player.displayName, StringComparison.OrdinalIgnoreCase);

                    foreach (var check in command.Split('|'))
                        _instance?.Server.Command(check);
                });
            }

            private void GrantPermissions(BasePlayer player, Skill skill, int stage)
            {
                if (skill.Stages.ContainsKey(stage))
                    skill.Stages[stage].Permissions?.ForEach(command =>
                    {
                        Permissions.Add(command);
                        foreach (var check in command.Split('|'))
                            _instance?.permission.GrantUserPermission(player.UserIDString, check, null);
                    });
            }

            private void RevokePermissions(IPlayer player, StageConf stage)
            {
                if (player == null || stage == null) return;

                Permissions?.ToList().ForEach(command =>
                {
                    Permissions.Remove(command);
                    foreach (var check in command.Split('|'))
                        _instance.permission.RevokeUserPermission(player.Id, check);
                });
            }

            private void GrantGroups(BasePlayer player, Skill skill, int stage)
            {
                if (skill.Stages.ContainsKey(stage))
                    skill.Stages[stage].Groups?.ForEach(command =>
                    {
                        Groups.Add(command);
                        foreach (var check in command.Split('|'))
                            if (!_instance.permission.UserHasGroup(player.UserIDString, check))
                                _instance.permission.AddUserGroup(player.UserIDString, check);
                    });
            }

            private void RevokeGroups(IPlayer player, StageConf stage)
            {
                if (player == null || stage == null) return;

                Groups?.ToList().ForEach(command =>
                {
                    Groups.Remove(command);

                    foreach (var check in command.Split('|')) _instance.permission.RemoveUserGroup(player.Id, check);
                });
            }

            public void Revoke(IPlayer player, StageConf stage)
            {
                if (player == null || stage == null) return;

                RevokeGroups(player, stage);

                RevokePermissions(player, stage);

                _instance.TimedPermissions?.Call("OnUserConnected", player);
            }
        }

        private class SkillData
        {
            [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public SkillType Type;

            [JsonProperty(PropertyName = "ID")] public int ID;

            [JsonProperty(PropertyName = "Stage")] public int Stage;

            [JsonIgnore] public Skill Skill;
        }

        private PlayerData GetPlayerData(BasePlayer player)
        {
            return GetPlayerData(player.userID);
        }

        private PlayerData GetPlayerData(ulong user)
        {
            if (!user.IsSteamId()) return null;

            if (!_data.Players.ContainsKey(user))
                _data.Players.Add(user, new PlayerData());

            return _data.Players[user];
        }

        private float GetNextPrice(ulong user, Skill skill)
        {
            var result = -1;

            var data = GetPlayerData(user);
            if (data == null) return result;

            var skillData = data.GetSkill(skill.Type, skill.ID);
            if (skillData == null) return skill.Stages[1].Cost;

            var nextStage = skillData.Stage + 1;
            if (!skill.Stages.ContainsKey(nextStage))
                return result;

            return skill.Stages[nextStage].Cost;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;

            LoadData();

            RegisterIDs();

            RegisterPermissions();

            UnsubscribeHooks();
        }

        private void OnServerInitialized()
        {
            Notifications?.Call("AddImage", _config.MaxLevel.Image, _config.MaxLevel.Url);

            Notifications?.Call("AddImage", _config.NotMoney.Image, _config.NotMoney.Url);

            LoadImages();

            CheckOnDuplicates();

            CheckStages();

            RegisterCommands();

            SubscribeHooks();
        }

        private void OnServerSave()
        {
            timer.In(Random.Range(2f, 7f), SaveData);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Layer);

            _furnaceControllers.ToList().ForEach(component =>
            {
                if (component != null)
                    component.Kill();
            });

            _recoverPlayers.Values.ToList().ForEach(component =>
            {
                if (component != null)
                    component.Kill(true);
            });

            SaveData();

            _instance = null;
            _config = null;
            _data = null;
        }

        private void OnNewSave(string filename)
        {
            if (!_config.Wipe) return;

            WipeData();
        }

        #region Gather

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            OnGather(player, item);
        }

        private void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            OnGather(player, item);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OnGather(player, item, dispenser);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            OnGather(player, item, dispenser);
        }

        #endregion

        #region Damage

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            var attack = 0f;
            var secure = 0f;

            var attacker = info.InitiatorPlayer;
            if (attacker != null)
            {
                var data = GetPlayerData(attacker);
                var dataSkill = data?.GetSkill(SkillType.Attack);
                var skill = dataSkill?.Skill;
                if (skill != null && !_config.AttackBlackList.Contains(entity.ShortPrefabName))
                    attack = skill.Stages[dataSkill.Stage].Value;
            }

            var target = entity as BasePlayer;
            if (target != null)
            {
                var data = GetPlayerData(target);
                var dataSkill = data?.GetSkill(SkillType.Secure);
                var skill = dataSkill?.Skill;
                if (skill != null) secure = skill.Stages[dataSkill.Stage].Value;
            }

            var result = attack - secure;
            if (result != 0)
            {
                result = 1f + result / 100f;

                info.damageTypes.ScaleAll(result);
            }
        }

        #endregion

        #region Regeneration

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player, float delta)
        {
            if (metabolism == null || player == null) return;

            var data = GetPlayerData(player);
            if (data == null) return;

            if (metabolism.pending_health.value > 0.0)
            {
                var regen = data.GetSkill(SkillType.Regeneration);
                var skill = regen?.Skill;
                if (skill != null)
                {
                    var num12 = Mathf.Min(1f * delta, metabolism.pending_health.value);

                    var value = num12 + num12 * (skill.Stages[regen.Stage].Value / 100f);

                    player.Heal(value);
                    if (player.healthFraction == 1.0)
                        metabolism.pending_health.value = 0.0f;
                    else
                        metabolism.pending_health.Subtract(value);
                }
            }

            var met = data.GetSkill(SkillType.Metabolism);
            if (met != null)
            {
                var skill = met.Skill;
                if (skill != null)
                {
                    var value = skill.Stages[met.Stage].Value;
                    player.metabolism.hydration.Add(value);
                    player.metabolism.calories.Add(value);
                }
            }
        }

        #endregion

        #region Loot

        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (player == null || container == null || container.net == null || container.inventory == null ||
                _config.ContainersBlackList.Contains(container.ShortPrefabName)) return;

            var id = container.net.ID;
            if (_containers.Contains(id))
                return;

            var any = false;

            var componentsData = GetPlayerData(player)?.GetSkill(SkillType.ComponentsRates);
            var componentSkill = componentsData?.Skill;
            StageConf componentStage;
            if (componentSkill != null && componentSkill.Stages.TryGetValue(componentsData.Stage, out componentStage))
            {
                container.inventory.itemList.ForEach(item =>
                {
                    if (item != null && item.info.category == ItemCategory.Component)
                        item.amount = (int) (componentStage.Value * item.amount);
                });

                any = true;
            }

            var scrapData = GetPlayerData(player)?.GetSkill(SkillType.Scrap);
            var scrapSkill = scrapData?.Skill;
            StageConf scrapStage;
            if (scrapSkill != null && scrapSkill.Stages.TryGetValue(scrapData.Stage, out scrapStage))
            {
                container.inventory.itemList.ForEach(item =>
                {
                    if (item != null && item.info.shortname == "scrap")
                        item.amount = (int) (scrapStage.Value * item.amount);
                });

                any = true;
            }

            if (any)
                _containers.Add(id);
        }

        private void OnContainerDropItems(ItemContainer container)
        {
            if (container == null) return;

            var entity = container.entityOwner as LootContainer;
            if (entity == null || entity.IsDestroyed ||
                _config.ContainersBlackList.Contains(entity.ShortPrefabName)) return;

            var player = entity.lastAttacker as BasePlayer;
            if (player == null) return;

            var componentsData = GetPlayerData(player)?.GetSkill(SkillType.ComponentsRates);
            var componentSkill = componentsData?.Skill;
            StageConf componentStage;
            if (componentSkill != null && componentSkill.Stages.TryGetValue(componentsData.Stage, out componentStage))
                container.itemList.ForEach(item =>
                {
                    if (item != null && item.info.category == ItemCategory.Component)
                        item.amount = (int) (componentStage.Value * item.amount);
                });

            var scrapData = GetPlayerData(player)?.GetSkill(SkillType.Scrap);
            var scrapSkill = scrapData?.Skill;
            StageConf scrapStage;
            if (scrapSkill != null && scrapSkill.Stages.TryGetValue(scrapData.Stage, out scrapStage))
                container.itemList.ForEach(item =>
                {
                    if (item != null && item.info.shortname == "scrap")
                        item.amount = (int) (scrapStage.Value * item.amount);
                });
        }

        #endregion

        #region Chance Recover

        private object OnPlayerWound(BasePlayer player, HitInfo info)
        {
            if (player == null) return null;

            var skillData = GetPlayerData(player)?.GetSkill(SkillType.StandUpChance);

            var skill = skillData?.Skill;
            StageConf stage;
            if (skill == null || !skill.Stages.TryGetValue(skillData.Stage, out stage) || stage == null) return null;

            var flag = info != null && info.damageTypes.GetMajorityDamageType() == DamageType.Fall;
            if (player.IsCrawling())
            {
                player.woundedByFallDamage |= flag;
                GoToIncapacitated(player, info, stage);
            }
            else
            {
                player.woundedByFallDamage = flag;
                if (flag)
                    GoToIncapacitated(player, info, stage);
                else
                    GoToCrawling(player, info, stage);
            }

            return true;
        }

        private void GoToCrawling(BasePlayer player, HitInfo info, StageConf stage)
        {
            player.health = Random.Range(ConVar.Server.crawlingminimumhealth, ConVar.Server.crawlingmaximumhealth);
            player.metabolism.bleeding.value = 0.0f;
            player.healingWhileCrawling = 0.0f;
            player.WoundedStartSharedCode(info);
            StartWoundedTick(player, 40, 50, stage);
            player.SendNetworkUpdateImmediate();
        }

        private void GoToIncapacitated(BasePlayer player, HitInfo info, StageConf stage)
        {
            if (!player.IsWounded())
                player.WoundedStartSharedCode(info);

            player.health = Random.Range(2f, 6f);
            player.metabolism.bleeding.value = 0.0f;
            player.healingWhileCrawling = 0.0f;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Incapacitated, true);
            player.SetServerFall(true);
            var basePlayer = info?.InitiatorPlayer;
            if (EACServer.playerTracker != null && player.net.connection != null && basePlayer != null &&
                basePlayer.net.connection != null)
                using (TimeWarning.New("playerTracker.LogPlayerDowned"))
                {
                    var client1 = EACServer.GetClient(player.net.connection);
                    var client2 = EACServer.GetClient(basePlayer.net.connection);
                    EACServer.playerTracker.LogPlayerDowned(client1, client2);
                }

            StartWoundedTick(player, 10, 25, stage);
            player.SendNetworkUpdateImmediate();
        }

        private void StartWoundedTick(BasePlayer player, int minTime, int maxTime, StageConf stage)
        {
            player.woundedDuration = Random.Range(minTime, maxTime + 1);
            player.lastWoundedStartTime = Time.realtimeSinceStartup;

            if (_recoverPlayers.ContainsKey(player))
                _recoverPlayers[player]?.Kill();

            player.gameObject.AddComponent<RecoverComponent>()
                .SetParams(stage);
        }

        private void OnPlayerRecovered(BasePlayer player)
        {
            if (player == null) return;

            RecoverComponent recover;
            if (_recoverPlayers.TryGetValue(player, out recover) && recover != null)
                recover.Kill();
        }

        #endregion

        #region Craft

        private void OnItemCraft(ItemCraftTask task, BasePlayer player, Item item)
        {
            if (task == null || player == null || player.inventory.crafting.queue.Count > 0) return;

            CraftingHandle(task, player, item);
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            var player = task?.owner;
            if (task == null || player == null) return;

            if (task.amount == 0 && player.inventory.crafting.queue.Count > 1)
                task = player.inventory.crafting.queue.ElementAt(1);

            CraftingHandle(task, player, item);
        }

        private void CraftingHandle(ItemCraftTask task, BasePlayer player, Item item)
        {
            if (task == null || player == null) return;

            var skillData = GetPlayerData(player)?.GetSkill(SkillType.CraftSpeed);

            var skill = skillData?.Skill;
            if (skill == null) return;

            var rate = 1f - skill.Stages[skillData.Stage].Value / 100f;

            NextTick(() =>
            {
                var currentCraftLevel = task.owner.currentCraftLevel;

                var duration = ItemCrafter.GetScaledDuration(task.blueprint, currentCraftLevel);
                var scaledDuration = duration * rate;

                task.endTime = Time.realtimeSinceStartup + scaledDuration;
                if (task.owner == null)
                    return;

                task.owner.Command("note.craft_start", task.taskUID, scaledDuration,
                    task.amount);

                if (!task.owner.IsAdmin || !Craft.instant)
                    return;
                task.endTime = Time.realtimeSinceStartup + 1f;
            });
        }

        #endregion

        #region Fast Oven

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (oven == null || player == null || _config.OvensBlackList.Exists(x => oven.name.Contains(x)))
                return null;

            var skillData = GetPlayerData(player)?.GetSkill(SkillType.FastOven);

            var skill = skillData?.Skill;
            if (skill == null || !skill.Stages.ContainsKey(skillData.Stage)) return null;

            var speedMultiplier = 0.5f / (skill.Stages[skillData.Stage].Value / 100f);

            var component = oven.GetComponent<FurnaceController>() ?? oven.gameObject.AddComponent<FurnaceController>();
            if (component == null) return null;

            var flag = !oven.IsOn();
            if (flag)
                component.StartCooking(speedMultiplier);
            else
                component.StopCooking();

            return false;
        }

        #endregion

        #region Kits

        private object OnKitCooldown(BasePlayer player, double cooldown)
        {
            var skillData = GetPlayerData(player)?.GetSkill(SkillType.Kits);
            var skill = skillData?.Skill;
            if (skill == null) return null;

            var rate = (double) (1f - skill.Stages[skillData.Stage].Value / 100f);

            return cooldown * rate;
        }

        #endregion

        #endregion

        #region Commands

        private void CmdOpenUi(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (!string.IsNullOrEmpty(_config.Permission) &&
                !permission.UserHasPermission(player.UserIDString, _config.Permission))
            {
                SendNotify(player, NoPermissions, 1);
                return;
            }

            MainUi(player, first: true);
        }

        private void AdminCommands(IPlayer cov, string command, string[] args)
        {
            if (!cov.IsAdmin) return;

            if (args.Length == 0)
            {
                cov.Reply($"Use: /{command} name/steamid");
                return;
            }

            var target = BasePlayer.Find(args[0]);
            if (target == null)
            {
                cov.Reply($"Player {args[0]} not found!");
                return;
            }

            var data = GetPlayerData(target);
            if (data == null) return;

            switch (command)
            {
                case "giveallskills":
                {
                    data.Skills.Clear();
                    _config.Skills.ForEach(skill => data.AddSkill(target, skill, skill.Stages.Keys.Max()));

                    cov.Reply($"Player {args[0]} give all skills!");
                    break;
                }
                case "giveskill":
                {
                    if (args.Length < 3)
                    {
                        cov.Reply($"Use: /{command} name/steamid [SkillType] [Stage] [ID - for None]");
                        return;
                    }

                    SkillType type;
                    int stage;
                    if (!Enum.TryParse(args[1], out type) || !int.TryParse(args[2], out stage))
                    {
                        cov.Reply("Error getting values");
                        return;
                    }

                    int id;
                    if (args.Length > 2)
                        int.TryParse(args[2], out id);

                    var skill = data.GetSkill(type);
                    if (skill != null)
                        skill.Stage = stage;
                    else
                        data.AddSkill(target, FindByType(type), stage);

                    cov.Reply($"Player {args[0]} give skill: {type} (stage: {stage})!");
                    break;
                }

                case "skills.wipe":
                {
                    WipeData();
                    break;
                }
            }
        }

        private void CmdWipe(IPlayer cov, string command, string[] args)
        {
            if (!cov.IsAdmin) return;

            WipeData();
        }

        [ConsoleCommand("UI_Skills")]
        private void CmdConsoleSkills(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "close":
                {
                    CuiHelper.DestroyUi(player, Layer);
                    break;
                }
                case "page":
                {
                    int page;
                    if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out page)) return;

                    MainUi(player, arg.Args[2], page);
                    break;
                }
                case "upgrade":
                {
                    SkillType type;
                    int page, id;
                    if (!arg.HasArgs(5) ||
                        !Enum.TryParse(arg.Args[1], out type) ||
                        !int.TryParse(arg.Args[2], out page) ||
                        !int.TryParse(arg.Args[4], out id)) return;

                    var parent = arg.Args[3];

                    var skill = _config.Skills.Find(x => x.Type == type && x.ID == id);
                    if (skill == null) return;

                    var cost = GetNextPrice(player.userID, skill);
                    if (cost <= 0) return;

                    var data = GetPlayerData(player.userID);
                    if (data == null) return;

                    var nextStage = 1;
                    var dataSkill = data.GetSkill(type, id);
                    if (dataSkill != null)
                    {
                        nextStage = dataSkill.Stage + 1;
                        if (!skill.Stages.ContainsKey(nextStage))
                        {
                            SendNotify(player, _config.MaxLevel.Delay, MaxLevelTitle, MaxLevelDescription,
                                _config.MaxLevel.Image);
                            return;
                        }
                    }

                    if (!_config.Economy.RemoveBalance(player, (int) cost))
                    {
                        SendNotify(player, _config.NotMoney.Delay, NotMoneyTitle, NotMoneyDescription,
                            _config.NotMoney.Image);
                        return;
                    }

                    if (dataSkill != null)
                        data.UpgradeSkill(dataSkill, player, skill, nextStage);
                    else
                        data.AddSkill(player, skill, nextStage);

                    MainUi(player, parent, page);
                    break;
                }
            }
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, string parent = "Overlay", int page = 0, bool first = false)
        {
            if (string.IsNullOrEmpty(parent))
                parent = "Overlay";

            var container = new CuiElementContainer();

            #region Background

            if (first)
            {
                CuiHelper.DestroyUi(player, Layer);

                _config.Background.Get(ref container, parent, Layer, true);

                _config.Title.Get(ref container, Layer, null, Msg(MainTitle, player.UserIDString));
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0"}
            }, Layer, Layer + ".Main");

            var skills = _config.Skills
                .FindAll(skill =>
                    string.IsNullOrEmpty(skill.Permission) || player.IPlayer.HasPermission(skill.Permission))
                .Skip(page * _config.SkillPanel.Count).Take(_config.SkillPanel.Count).ToList();

            var lines = (int) Math.Ceiling(skills.Count / 2f);
            var xMargin = _config.SkillPanel.Margin / 2f;

            var ySwitch = (lines * _config.SkillPanel.Height + (lines - 1) * _config.SkillPanel.Margin) / 2f;

            var i = 1;
            skills.ForEach(skill =>
            {
                var xSwitch = i % 2 != 0 && i == skills.Count && skills.Count != _config.SkillPanel.Count
                    ? -_config.SkillPanel.Width / 2f
                    : i % 2 != 0
                        ? -_config.SkillPanel.Width - xMargin
                        : xMargin;

                _config.SkillPanel.Get(ref container, parent, page, Layer + ".Main", i, player, skill, xSwitch,
                    ySwitch - _config.SkillPanel.Height, xSwitch + _config.SkillPanel.Width, ySwitch);

                if (i % 2 == 0)
                    ySwitch = ySwitch - _config.SkillPanel.Height - _config.SkillPanel.Margin;
                i++;
            });

            #endregion

            #region Pages

            if (_config.Skills.Count > _config.SkillPanel.Count)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            _config.Back.AnchorMin,
                        AnchorMax =
                            _config.Back.AnchorMax,
                        OffsetMin =
                            _config.Back.OffsetMin,
                        OffsetMax =
                            _config.Back.OffsetMax
                    },
                    Text =
                    {
                        Text = "«",
                        Align =
                            _config.Back.Align,
                        FontSize =
                            _config.Back.FontSize,
                        Font =
                            _config.Back.Font,
                        Color =
                            _config.Back.Color.Get()
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = page != 0 ? $"UI_Skills page {page - 1} {parent}" : ""
                    }
                }, Layer + ".Main");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            _config.Next.AnchorMin,
                        AnchorMax =
                            _config.Next.AnchorMax,
                        OffsetMin =
                            _config.Next.OffsetMin,
                        OffsetMax =
                            _config.Next.OffsetMax
                    },
                    Text =
                    {
                        Text = "»",
                        Align =
                            _config.Next.Align,
                        FontSize =
                            _config.Next.FontSize,
                        Font =
                            _config.Next.Font,
                        Color =
                            _config.Next.Color.Get()
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = _config.Skills.Count > (page + 1) * _config.SkillPanel.Count
                            ? $"UI_Skills page {page + 1} {parent}"
                            : ""
                    }
                }, Layer + ".Main");
            }

            #endregion

            #region Balance

            _config.BalanceText.Get(ref container, Layer + ".Main", null,
                Msg(Balance, player.UserIDString, _config.Economy.ShowBalance(player)), _config.EnableBalanceIcon);

            #endregion

            #region Close

            _config.Close.Get(ref container, Layer + ".Main", Layer + ".Close", "✕");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button = {Color = "0 0 0 0", Command = "UI_Skills close"}
            }, Layer + ".Close");

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnCollectiblePickup));
            Unsubscribe(nameof(OnGrowableGathered));
            Unsubscribe(nameof(OnDispenserBonus));
            Unsubscribe(nameof(OnDispenserGather));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnRunPlayerMetabolism));
            Unsubscribe(nameof(OnContainerDropItems));
            Unsubscribe(nameof(OnLootEntity));
            Unsubscribe(nameof(OnPlayerWound));
            Unsubscribe(nameof(OnItemCraft));
            Unsubscribe(nameof(OnItemCraftFinished));
            Unsubscribe(nameof(OnOvenToggle));
            Unsubscribe(nameof(OnKitCooldown));
        }

        private void SubscribeHooks()
        {
            var types = Pool.GetList<SkillType>();

            _config.Skills.ForEach(skill =>
            {
                if (!skill.Enabled || types.Contains(skill.Type)) return;

                switch (skill.Type)
                {
                    case SkillType.Wood:
                    case SkillType.Stones:
                    case SkillType.Metal:
                    case SkillType.Sulfur:
                    case SkillType.Cloth:
                    case SkillType.Butcher:
                    {
                        Subscribe(nameof(OnCollectiblePickup));
                        Subscribe(nameof(OnGrowableGathered));
                        Subscribe(nameof(OnDispenserBonus));
                        Subscribe(nameof(OnDispenserGather));
                        break;
                    }
                    case SkillType.Attack:
                    case SkillType.Secure:
                    {
                        Subscribe(nameof(OnEntityTakeDamage));
                        break;
                    }
                    case SkillType.Regeneration:
                    case SkillType.Metabolism:
                    {
                        Subscribe(nameof(OnRunPlayerMetabolism));
                        break;
                    }
                    case SkillType.ComponentsRates:
                    {
                        Subscribe(nameof(OnContainerDropItems));
                        Subscribe(nameof(OnLootEntity));
                        break;
                    }
                    case SkillType.StandUpChance:
                    {
                        Subscribe(nameof(OnPlayerWound));
                        break;
                    }
                    case SkillType.CraftSpeed:
                    {
                        Subscribe(nameof(OnItemCraft));
                        Subscribe(nameof(OnItemCraftFinished));
                        break;
                    }
                    case SkillType.FastOven:
                    {
                        Subscribe(nameof(OnOvenToggle));
                        break;
                    }
                    case SkillType.Kits:
                    {
                        Subscribe(nameof(OnKitCooldown));
                        break;
                    }
                    default:
                        return;
                }

                types.Add(skill.Type);
            });

            Pool.FreeList(ref types);
        }

        private void RegisterCommands()
        {
            AddCovalenceCommand(_config.Command, nameof(CmdOpenUi));

            AddCovalenceCommand(new[] {"giveallskills", "giveskill"}, nameof(AdminCommands));

            AddCovalenceCommand("skills.wipe", nameof(CmdWipe));
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(_config.Permission, this);

            _config.Skills.ForEach(skill =>
            {
                if (!string.IsNullOrEmpty(skill.Permission) && !permission.PermissionExists(skill.Permission))
                    permission.RegisterPermission(skill.Permission, this);
            });
        }

        private void RegisterIDs()
        {
            _config.Skills.ForEach(skill =>
            {
                if (skill.Type == SkillType.None) return;

                skill.ID = 0;

                if (_skillByType.ContainsKey(skill.Type))
                {
                    PrintError($"DUBLICATE SKILL TYPE: {skill.Type}");
                    return;
                }

                _skillByType.Add(skill.Type, skill);
            });
        }

        private void OnGather(BasePlayer player, Item item, ResourceDispenser dispenser = null)
        {
            if (player == null || item == null) return;

            GetPlayerData(player)?.Skills?.ForEach(dataSkill =>
            {
                var skill = dataSkill?.Skill;
                if (skill == null || !skill.Stages.ContainsKey(dataSkill.Stage)) return;

                var amount = skill.Stages[dataSkill.Stage].Value / 100f;
                switch (skill.Type)
                {
                    case SkillType.Wood:
                    {
                        if (item.info.shortname == "wood") item.amount = (int) (item.amount * amount);
                        break;
                    }
                    case SkillType.Stones:
                    {
                        if (item.info.shortname == "stones") item.amount = (int) (item.amount * amount);
                        break;
                    }
                    case SkillType.Sulfur:
                    {
                        if (item.info.shortname == "sulfur.ore") item.amount = (int) (item.amount * amount);
                        break;
                    }
                    case SkillType.Metal:
                    {
                        if (item.info.shortname == "metal.ore" || item.info.shortname == "hq.metal.ore")
                            item.amount = (int) (item.amount * amount);
                        break;
                    }
                    case SkillType.Cloth:
                    {
                        if (item.info.shortname == "cloth") item.amount = (int) (item.amount * amount);
                        break;
                    }
                    case SkillType.Butcher:
                    {
                        if (dispenser == null) return;

                        var entity = dispenser.GetComponent<BaseEntity>();
                        if (entity == null || !_config.Animals.Contains(entity.ShortPrefabName)) return;

                        item.amount = (int) (item.amount * amount);
                        break;
                    }
                }
            });
        }

        private Skill FindByType(SkillType type)
        {
            Skill skill;
            return _skillByType.TryGetValue(type, out skill) ? skill : null;
        }

        private void CheckOnDuplicates()
        {
            var duplicates = _config.Skills
                .FindAll(x => x.Type == SkillType.None)
                .GroupBy(x => x.ID)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicates.Length > 0)
                PrintError(
                    $"Matching item IDs found (None Type): {string.Join(", ", duplicates.Select(x => x.ToString()))}");
        }

        private void CheckStages()
        {
            foreach (var data in _data.Players.Values)
                data.Skills.ForEach(skillData =>
                {
                    skillData.Skill = FindByType(skillData.Type);

                    var skill = skillData.Skill;
                    if (skill == null) return;

                    if (!skill.Stages.ContainsKey(skillData.Stage))
                        skillData.Stage = skill.Stages.Max(x => x.Key);
                });
        }

        private void LoadImages()
        {
            timer.In(5, () =>
            {
                if (!ImageLibrary)
                {
                    PrintWarning("IMAGE LIBRARY IS NOT INSTALLED");
                }
                else
                {
                    var imagesList = new Dictionary<string, string>();

                    if (!string.IsNullOrEmpty(_config.Background.Image))
                        imagesList.Add(_config.Background.Image, _config.Background.Image);

                    if (!string.IsNullOrEmpty(_config.SkillPanel.Background.Image))
                        imagesList.Add(_config.SkillPanel.Background.Image, _config.SkillPanel.Background.Image);

                    if (!string.IsNullOrEmpty(_config.SkillPanel.AddCostImage.Image))
                        imagesList.Add(_config.SkillPanel.AddCostImage.Image, _config.SkillPanel.AddCostImage.Image);

                    _config.Skills.ForEach(skill =>
                    {
                        if (!string.IsNullOrEmpty(skill.Image) && !imagesList.ContainsKey(skill.Image))
                            imagesList.Add(skill.Image, skill.Image);
                    });

                    ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
                }
            });
        }

        private void WipeData()
        {
            foreach (var check in _data.Players)
                check.Value.Skills.ForEach(skill =>
                {
                    skill.Skill = _config.Skills.Find(x => x.Type == SkillType.None && x.ID == skill.ID);

                    if (skill.Skill == null || skill.Skill.Type != SkillType.None ||
                        !skill.Skill.Stages.ContainsKey(skill.Stage))
                        return;

                    check.Value.Revoke(covalence.Players.FindPlayerById(check.Key.ToString()),
                        skill.Skill.Stages[skill.Stage]);
                });

            _data.Players.Clear();
            SaveData();

            PrintWarning($"{Name} wiped!");
        }

        #endregion

        #region Furnace Controller

        private class FurnaceController : FacepunchBehaviour
        {
            private BaseOven _furnace;

            private void Awake()
            {
                _furnace = GetComponent<BaseOven>();

                _instance?._furnaceControllers.Add(this);
            }

            public void StartCooking(float speedMultiplier)
            {
                if (_furnace == null || _furnace.FindBurnable() == null)
                    return;

                _furnace.inventory.temperature = _furnace.cookingTemperature;
                _furnace.UpdateAttachmentTemperature();

                InvokeRepeating(Cook, speedMultiplier, speedMultiplier);
                _furnace.SetFlag(BaseEntity.Flags.On, true);
            }

            public void StopCooking()
            {
                if (_furnace == null) return;

                _furnace.UpdateAttachmentTemperature();
                if (_furnace.inventory != null)
                {
                    _furnace.inventory.temperature = 15f;
                    _furnace.inventory.itemList.ForEach(item =>
                    {
                        if (item.HasFlag(global::Item.Flag.OnFire))
                        {
                            item.SetFlag(global::Item.Flag.OnFire, false);
                            item.MarkDirty();
                        }
                    });
                }

                CancelInvoke(Cook);
                _furnace.SetFlag(BaseEntity.Flags.On, false);
            }

            private void Cook()
            {
                var burnable = _furnace.FindBurnable();
                if (Interface.CallHook("OnOvenCook", this, burnable) != null)
                    return;

                if (burnable == null)
                {
                    StopCooking();
                }
                else
                {
                    _furnace.inventory.OnCycle(0.5f);
                    var slot = _furnace.GetSlot(BaseEntity.Slot.FireMod);
                    if ((bool) (Object) slot)
                        slot.SendMessage(nameof(Cook), 0.5f, SendMessageOptions.DontRequireReceiver);
                    var component = burnable.info.GetComponent<ItemModBurnable>();
                    burnable.fuel -= (float) (0.5 * (_furnace.cookingTemperature / 200.0));
                    if (!burnable.HasFlag(global::Item.Flag.OnFire))
                    {
                        burnable.SetFlag(global::Item.Flag.OnFire, true);
                        burnable.MarkDirty();
                    }

                    if (burnable.fuel <= 0.0)
                        ConsumeFuel(burnable, component);

                    Interface.CallHook("OnOvenCooked", this, burnable, slot);
                }
            }

            private void ConsumeFuel(Item fuel, ItemModBurnable burnable)
            {
                if (Interface.CallHook("OnFuelConsume", this, fuel, burnable) != null)
                    return;

                if (_furnace.allowByproductCreation && burnable.byproductItem != null &&
                    Random.Range(0.0f, 1f) > (double) burnable.byproductChance)
                {
                    var obj = ItemManager.Create(burnable.byproductItem, burnable.byproductAmount);
                    if (!obj.MoveToContainer(_furnace.inventory))
                    {
                        StopCooking();
                        obj.Drop(_furnace.inventory.dropPosition, _furnace.inventory.dropVelocity);
                    }
                }

                if (fuel.amount <= 1)
                {
                    fuel.Remove();
                }
                else
                {
                    --fuel.amount;
                    fuel.fuel = burnable.fuelAmount;
                    fuel.MarkDirty();
                    Interface.CallHook("OnFuelConsumed", this, fuel, burnable);
                }
            }

            private void OnDestroy()
            {
                CancelInvoke();

                _instance?._furnaceControllers.Remove(this);

                Destroy(this);
            }

            public void Kill()
            {
                DestroyImmediate(this);
            }
        }

        #endregion

        #region Recover Component

        private class RecoverComponent : FacepunchBehaviour
        {
            private BasePlayer player;

            private StageConf Stage;

            private float incapacitatedrecoverchance;
            private float woundedrecoverchance;

            private bool Unload;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();

                _instance._recoverPlayers[player] = this;
            }

            public void SetParams(StageConf stage)
            {
                Stage = stage;
                incapacitatedrecoverchance = stage.Value / 100f;
                woundedrecoverchance = stage.Value2 / 100f;

                Invoke(WoundingTick, 1f);
            }

            public void WoundingTick()
            {
                using (TimeWarning.New(nameof(WoundingTick)))
                {
                    if (player.IsDead())
                        return;
                    if (player.TimeSinceWoundedStarted >= (double) player.woundedDuration)
                    {
                        var num1 = player.IsIncapacitated()
                            ? incapacitatedrecoverchance
                            : (double) woundedrecoverchance;

                        var t = (float) ((player.metabolism.hydration.Fraction() +
                                          (double) player.metabolism.calories.Fraction()) / 2.0);
                        double num2 = Mathf.Lerp(0.0f, ConVar.Server.woundedmaxfoodandwaterbonus, t);
                        if (Random.value < (double) Mathf.Clamp01((float) (num1 + num2)))
                        {
                            player.RecoverFromWounded();
                        }
                        else if (player.woundedByFallDamage)
                        {
                            player.Die();
                        }
                        else
                        {
                            var itemByItemId =
                                player.inventory.containerBelt.FindItemByItemID(ItemManager
                                    .FindItemDefinition("largemedkit").itemid);
                            if (itemByItemId != null)
                            {
                                itemByItemId.UseItem();
                                player.RecoverFromWounded();
                            }
                            else
                            {
                                player.Die();
                            }
                        }
                    }
                    else
                    {
                        if (player.IsSwimming() && player.IsCrawling())
                            _instance.GoToIncapacitated(player, null, Stage);

                        Invoke(WoundingTick, 1f);
                    }
                }
            }

            private void OnDestroy()
            {
                CancelInvoke();

                if (Unload)
                    player.Invoke(player.WoundingTick, 1);

                _instance?._recoverPlayers.Remove(player);
            }

            public void Kill(bool unload = false)
            {
                Unload = unload;

                DestroyImmediate(this);
            }
        }

        #endregion

        #region Lang

        private const string
            NoPermissions = "NoPermissions",
            Upgrade = "Upgrade",
            MainTitle = "Title",
            Balance = "Balance",
            MaxLevelTitle = "MaxLevelTitle",
            MaxLevelDescription = "MaxLevelDescription",
            NotMoneyTitle = "NotMoneyTitle",
            NotMoneyDescription = "NotMoneyDescription",
            Close = "Close";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [MainTitle] = "SKILLS",
                [Upgrade] = "UPGRADE",
                [Balance] = "Balance: {0}$",
                [MaxLevelTitle] = "Warning",
                [MaxLevelDescription] = "You have the maximum level!!!",
                [NotMoneyTitle] = "Warning",
                [NotMoneyDescription] = "Not enough money!",
                [Close] = "✕",
                [NoPermissions] = "You don't have permissions!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [MainTitle] = "СПОСОБНОСТИ",
                [Upgrade] = "УЛУЧШИТЬ",
                [Balance] = "Баланс: {0}$",
                [MaxLevelTitle] = "Предупреждение",
                [MaxLevelDescription] = "У вас максимальный уровень!!!",
                [NotMoneyTitle] = "Предупреждение",
                [NotMoneyDescription] = "Недостаточно денег!",
                [Close] = "✕",
                [NoPermissions] = "У вас нет необходимого разрешения"
            }, this, "ru");
        }

        private void SendNotify(BasePlayer player, float delay, string title, string description, string image)
        {
            if (Notifications)
                Notifications.Call("ShowNotify", player, delay, Msg(title, player.UserIDString),
                    Msg(description, player.UserIDString), image);
            else if (_config.UseNotify && (Notify != null || UINotify != null))
                Interface.Oxide.CallHook("SendNotify", player, 0, Msg(player, description));
            else
                Reply(player, description);
        }

        private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
        {
            if (_config.UseNotify && (Notify != null || UINotify != null))
                Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            SendReply(player, Msg(key, player.UserIDString, obj));
        }

        private string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, userid), obj);
        }

        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        #endregion
    }
}