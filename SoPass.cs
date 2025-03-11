using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("SoPass", "https://discord.gg/TrJ7jnS233", "1.2.5")]
    public class SoPass : RustPlugin
    { 
         /*
         * ТИПЫ ЗАДАЧ
         * 1. Добыть 
         * 2. Убить
         * 3. Скрафтить
         * 4. Изучить 
         * 5. Залутать
         * 6. Поставить
         * 7. Починить
         * 8. Собрать с земли
         * 9. Улучшиить постройку(Например Оюъект задачи-foundation, Тогда в оружие или инструмент-дерево,камень,металл или мвк)
         * 10. Использовать карточку доступа
         * 11. Купить в магазине
         */
        #region CFG+DATA

        private Dictionary<ulong, PlayerData> _playerData = new Dictionary<ulong, PlayerData>();

        private ConfigData cfg { get; set; }
        public class Reward
        {
            [JsonProperty("Шортнейм(Шортнейм предмета или название команды или название набора)")]
            public string ShortName = "";

            [JsonProperty("Кол-во")] public int Amount;
            [JsonProperty("Скинайди")] public ulong SkinId;
            [JsonProperty("Команда(Если надо)")] public string command = "";
            [JsonProperty("Использовать набор?")] public bool nabor = false;

            [JsonProperty("Картинка(Если команда или набор)")]
            public string URL = "";

            [JsonProperty(
                "Набор: Список предметов и команд(Если используете набор все параметры кроме \"Картинка\" и \"Шортнейм\" оставить пустыми и поставить использовать набор на true)")]
            public List<Items> itemList;
 
            public class Items
            {
                [JsonProperty("Шортнейм")] public string ShortName = "";
                [JsonProperty("Кол-во")] public int Amount;
                [JsonProperty("Скинайди")] public ulong SkinId;
                [JsonProperty("Команда(Если надо)")] public string command = "";
            }
        }

        private class ConfigData
        {
            [JsonProperty("Включить сохранение даты при сохранение карты")]
            public bool saveOnSavew = true;
            [JsonProperty("Включить настройку показа активных задач")]
            public bool activeOn = true;
            [JsonProperty("Положение активных задач - MIN")] public string OffsetMin = "380 50";
            [JsonProperty("Положение активных задач - MAX")] public string OffsetMax = "620 200";
            [JsonProperty("Текст активной задачи 1")] public string text1 = "<b>{0}: ОСТАЛОСЬ {1}</b>";
            [JsonProperty("Текст активной задачи 2")] public string text2 = "<b>{0}: <color=red>ВЫПОЛНЕНО</color></b>";
            [JsonProperty("Чистить лвл и класс игрока, после вайпа?")]
            public bool newsave = false;    
            [JsonProperty("Включить проход следующих классов?")]
            public bool nextklass = false;
            [JsonProperty("Список задач для классов(\"Название класса\":{ Список задач)}")]
            public Dictionary<string, List<Quest>> _listQuest;
            [JsonProperty("Список классов")] public List<ClassPlayer> _classList;

            internal class ClassPlayer
            {
                [JsonProperty("Название")] public string Name = "";
                [JsonProperty("Картинка")] public string URL = "";
                [JsonProperty("Пермищен")] public string Perm = "";
                [JsonProperty("Описание")] public string Text = "";
            }
 
            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData();
                newConfig._listQuest = new Dictionary<string, List<Quest>>() 
                {
                    ["Солдат"] = new List<Quest>()
                    {
                        new Quest()
                        {
                            DisplayName = "Начальный квест",
                            Lvl = 1,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "stones",
                                    URL = "",
                                    Amount = 1000,
                                    command = "",
                                    nabor = false,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "Набор дерева",
                                    URL = "https://www.pngkey.com/png/full/78-786188_shop-icon-icon-ca-hng.png",
                                    Amount = 0,
                                    command = "",
                                    nabor = true,
                                    itemList = new List<Reward.Items>()
                                    {
                                        new Reward.Items()
                                        {
                                            ShortName = "wood",
                                            Amount = 1000,
                                            command = "",
                                            SkinId = 0
                                        }
                                    } 
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить лук",
                                    amount = 1,
                                    type = 3,
                                    need = "bow.hunting"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить стрелы",
                                    amount = 15,
                                    type = 3,
                                    need = "arrow.wooden"
                                }
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Подняться выше",
                            Lvl = 2,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "metal.fragments",
                                    nabor = false,
                                    Amount = 2500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.refined",
                                    nabor = false,
                                    Amount = 100,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "furnace",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить арбалет",
                                    amount = 1,
                                    type = 3,
                                    need = "crossbow"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить скоростных стрел",
                                    amount = 21,
                                    type = 3,
                                    need = "arrow.hv"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить костеной нож",
                                    amount = 1,
                                    type = 3,
                                    need = "knife.bone"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить медведя",
                                    amount = 1,
                                    type = 2,
                                    need = "bear"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть кожу",
                                    amount = 100,
                                    type = 1,
                                    need = "leather"
                                }
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Ну ты красавчик",
                            Lvl = 3,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "sulfur.ore",
                                    nabor = false,
                                    Amount = 2500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.ore",
                                    nabor = false,
                                    Amount = 3500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "wood",
                                    nabor = false,
                                    Amount = 5000,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить печку",
                                    amount = 1,
                                    type = 3,
                                    need = "crossbow"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Поставить печку",
                                    amount = 1,
                                    type = 6,
                                    need = "arrow.hv"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Изучить железный топор",
                                    amount = 1,
                                    type = 4,
                                    need = "hatchet"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть дерево",
                                    amount = 1,
                                    type = 1,
                                    need = "wood"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Убийственные цели",
                            Lvl = 4,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "syringe.medical",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "scrap",
                                    nabor = false,
                                    Amount = 450,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "lowgradefuel",
                                    nabor = false,
                                    Amount = 250,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить томсон",
                                    amount = 1,
                                    type = 3,
                                    need = "smg.thompson"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Поставить пистолетный патрон",
                                    amount = 100,
                                    type = 6,
                                    need = "ammo.pistol"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Изучить ледоруб",
                                    amount = 1,
                                    type = 4,
                                    need = "icepick.salvaged"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть серы",
                                    amount = 1,
                                    type = 1,
                                    need = "sulfur.ore"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Подготовка к жоскому финалу",
                            Lvl = 5,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "syringe.medical",
                                    nabor = false,
                                    Amount = 15,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "scrap",
                                    nabor = false,
                                    Amount = 1000,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "rifle.bolt",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Поставить верстак 3 уровня",
                                    amount = 1,
                                    type = 6,
                                    need = "workbench3.deployed"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить калаш",
                                    amount = 1,
                                    type = 3,
                                    need = "rifle.ak"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить патроны 5.56",
                                    amount = 100,
                                    type = 3,
                                    need = "ammo.rifle"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Финальная битва",
                            Lvl = 6,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "sulfur.ore",
                                    nabor = false,
                                    Amount = 7000,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.refined",
                                    nabor = false,
                                    Amount = 500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "rifle.l96",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Взорвать танк с помощью С4",
                                    amount = 1,
                                    type = 2,
                                    need = "bradleyapc",
                                    Weapon = "explosive.timed.deployed"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Сбить вертолет с калаша",
                                    amount = 1,
                                    type = 2,
                                    need = "patrolhelicopter",
                                    Weapon = "rifle.ak"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить HeavyNPC",
                                    amount = 10,
                                    type = 2,
                                    need = "heavyscientist"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить игроков с калаша",
                                    amount = 15,
                                    type = 2,
                                    need = "player",
                                    Weapon = "rifle.ak"
                                },
                            }
                        }
                    },
                    ["Фармер"] = new List<Quest>()
                    {
                        new Quest()
                        {
                            DisplayName = "Начальный квест",
                            Lvl = 1,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "stones",
                                    URL = "",
                                    Amount = 1000,
                                    command = "",
                                    nabor = false,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "wood",
                                    URL = "",
                                    Amount = 1000,
                                    command = "",
                                    nabor = false,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить лук",
                                    amount = 1,
                                    type = 3,
                                    need = "bow.hunting"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить стрелы",
                                    amount = 15,
                                    type = 3,
                                    need = "arrow.wooden"
                                }
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Подняться выше",
                            Lvl = 2,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "metal.fragments",
                                    nabor = false,
                                    Amount = 2500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.refined",
                                    nabor = false,
                                    Amount = 100,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "furnace",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить арбалет",
                                    amount = 1,
                                    type = 3,
                                    need = "crossbow"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить скоростных стрел",
                                    amount = 21,
                                    type = 3,
                                    need = "arrow.hv"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить костеной нож",
                                    amount = 1,
                                    type = 3,
                                    need = "knife.bone"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить медведя",
                                    amount = 1,
                                    type = 2,
                                    need = "bear"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть кожу",
                                    amount = 100,
                                    type = 1,
                                    need = "leather"
                                }
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Ну ты красавчик",
                            Lvl = 3,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "sulfur.ore",
                                    nabor = false,
                                    Amount = 2500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.ore",
                                    nabor = false,
                                    Amount = 3500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "wood",
                                    nabor = false,
                                    Amount = 5000,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить печку",
                                    amount = 1,
                                    type = 3,
                                    need = "crossbow"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Поставить печку",
                                    amount = 1,
                                    type = 6,
                                    need = "arrow.hv"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Изучить железный топор",
                                    amount = 1,
                                    type = 4,
                                    need = "hatchet"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть дерево",
                                    amount = 1,
                                    type = 1,
                                    need = "wood"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Убийственные цели",
                            Lvl = 4,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "syringe.medical",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "scrap",
                                    nabor = false,
                                    Amount = 450,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "lowgradefuel",
                                    nabor = false,
                                    Amount = 250,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить томсон",
                                    amount = 1,
                                    type = 3,
                                    need = "smg.thompson"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Поставить пистолетный патрон",
                                    amount = 100,
                                    type = 6,
                                    need = "ammo.pistol"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Изучить ледоруб",
                                    amount = 1,
                                    type = 4,
                                    need = "icepick.salvaged"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть серы",
                                    amount = 1,
                                    type = 1,
                                    need = "sulfur.ore"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Подготовка к жоскому финалу",
                            Lvl = 5,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "syringe.medical",
                                    nabor = false,
                                    Amount = 15,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "scrap",
                                    nabor = false,
                                    Amount = 1000,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "rifle.bolt",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Поставить верстак 3 уровня",
                                    amount = 1,
                                    type = 6,
                                    need = "workbench3.deployed"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить калаш",
                                    amount = 1,
                                    type = 3,
                                    need = "rifle.ak"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить патроны 5.56",
                                    amount = 100,
                                    type = 3,
                                    need = "ammo.rifle"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Финальная битва",
                            Lvl = 6,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "sulfur.ore",
                                    nabor = false,
                                    Amount = 7000,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.refined",
                                    nabor = false,
                                    Amount = 500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "rifle.l96",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Взорвать танк с помощью С4",
                                    amount = 1,
                                    type = 2,
                                    need = "bradleyapc",
                                    Weapon = "explosive.timed.deployed"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Сбить вертолет с калаша",
                                    amount = 1,
                                    type = 2,
                                    need = "patrolhelicopter",
                                    Weapon = "rifle.ak"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить HeavyNPC",
                                    amount = 10,
                                    type = 2,
                                    need = "heavyscientist"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить игроков с калаша",
                                    amount = 15,
                                    type = 2,
                                    need = "player",
                                    Weapon = "rifle.ak"
                                },
                            }
                        }
                    },
                    ["Строитель"] = new List<Quest>()
                    {
                        new Quest()
                        {
                            DisplayName = "Начальный квест",
                            Lvl = 1,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "stones",
                                    URL = "",
                                    Amount = 1000,
                                    command = "",
                                    nabor = false,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "wood",
                                    URL = "",
                                    Amount = 1000,
                                    command = "",
                                    nabor = false,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить лук",
                                    amount = 1,
                                    type = 3,
                                    need = "bow.hunting"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить стрелы",
                                    amount = 15,
                                    type = 3,
                                    need = "arrow.wooden"
                                }
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Подняться выше",
                            Lvl = 2,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "metal.fragments",
                                    nabor = false,
                                    Amount = 2500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.refined",
                                    nabor = false,
                                    Amount = 100,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "furnace",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить арбалет",
                                    amount = 1,
                                    type = 3,
                                    need = "crossbow"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить скоростных стрел",
                                    amount = 21,
                                    type = 3,
                                    need = "arrow.hv"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить костеной нож",
                                    amount = 1,
                                    type = 3,
                                    need = "knife.bone"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить медведя",
                                    amount = 1,
                                    type = 2,
                                    need = "bear"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть кожу",
                                    amount = 100,
                                    type = 1,
                                    need = "leather"
                                }
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Ну ты красавчик",
                            Lvl = 3,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "sulfur.ore",
                                    nabor = false,
                                    Amount = 2500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.ore",
                                    nabor = false,
                                    Amount = 3500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "wood",
                                    nabor = false,
                                    Amount = 5000,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить печку",
                                    amount = 1,
                                    type = 3,
                                    need = "crossbow"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Поставить печку",
                                    amount = 1,
                                    type = 6,
                                    need = "arrow.hv"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Изучить железный топор",
                                    amount = 1,
                                    type = 4,
                                    need = "hatchet"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть дерево",
                                    amount = 1,
                                    type = 1,
                                    need = "wood"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Убийственные цели",
                            Lvl = 4,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "syringe.medical",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "scrap",
                                    nabor = false,
                                    Amount = 450,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "lowgradefuel",
                                    nabor = false,
                                    Amount = 250,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить томсон",
                                    amount = 1,
                                    type = 3,
                                    need = "smg.thompson"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Поставить пистолетный патрон",
                                    amount = 100,
                                    type = 6,
                                    need = "ammo.pistol"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Изучить ледоруб",
                                    amount = 1,
                                    type = 4,
                                    need = "icepick.salvaged"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть серы",
                                    amount = 1,
                                    type = 1,
                                    need = "sulfur.ore"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Подготовка к жоскому финалу",
                            Lvl = 5,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "syringe.medical",
                                    nabor = false,
                                    Amount = 15,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "scrap",
                                    nabor = false,
                                    Amount = 1000,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "rifle.bolt",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Поставить верстак 3 уровня",
                                    amount = 1,
                                    type = 6,
                                    need = "workbench3.deployed"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить калаш",
                                    amount = 1,
                                    type = 3,
                                    need = "rifle.ak"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить патроны 5.56",
                                    amount = 100,
                                    type = 3,
                                    need = "ammo.rifle"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Финальная битва",
                            Lvl = 6,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "sulfur.ore",
                                    nabor = false,
                                    Amount = 7000,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.refined",
                                    nabor = false,
                                    Amount = 500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "rifle.l96",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Взорвать танк с помощью С4",
                                    amount = 1,
                                    type = 2,
                                    need = "bradleyapc",
                                    Weapon = "explosive.timed.deployed"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Сбить вертолет с калаша",
                                    amount = 1,
                                    type = 2,
                                    need = "patrolhelicopter",
                                    Weapon = "rifle.ak"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить HeavyNPC",
                                    amount = 10,
                                    type = 2,
                                    need = "heavyscientist"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить игроков с калаша",
                                    amount = 15,
                                    type = 2,
                                    need = "player",
                                    Weapon = "rifle.ak"
                                },
                            }
                        }
                    },
                    ["Донатер"] = new List<Quest>()
                    {
                        new Quest()
                        {
                            DisplayName = "Начальный квест",
                            Lvl = 1,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "stones",
                                    URL = "",
                                    Amount = 1000,
                                    command = "",
                                    nabor = false,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "wood",
                                    URL = "",
                                    Amount = 1000,
                                    command = "",
                                    nabor = false,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить лук",
                                    amount = 1,
                                    type = 3,
                                    need = "bow.hunting"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить стрелы",
                                    amount = 15,
                                    type = 3,
                                    need = "arrow.wooden"
                                }
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Подняться выше",
                            Lvl = 2,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "metal.fragments",
                                    nabor = false,
                                    Amount = 2500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.refined",
                                    nabor = false,
                                    Amount = 100,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "furnace",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить арбалет",
                                    amount = 1,
                                    type = 3,
                                    need = "crossbow"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить скоростных стрел",
                                    amount = 21,
                                    type = 3,
                                    need = "arrow.hv"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить костеной нож",
                                    amount = 1,
                                    type = 3,
                                    need = "knife.bone"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить медведя",
                                    amount = 1,
                                    type = 2,
                                    need = "bear"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть кожу",
                                    amount = 100,
                                    type = 1,
                                    need = "leather"
                                }
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Ну ты красавчик",
                            Lvl = 3,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "sulfur.ore",
                                    nabor = false,
                                    Amount = 2500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.ore",
                                    nabor = false,
                                    Amount = 3500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "wood",
                                    nabor = false,
                                    Amount = 5000,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить печку",
                                    amount = 1,
                                    type = 3,
                                    need = "crossbow"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Поставить печку",
                                    amount = 1,
                                    type = 6,
                                    need = "arrow.hv"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Изучить железный топор",
                                    amount = 1,
                                    type = 4,
                                    need = "hatchet"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть дерево",
                                    amount = 1,
                                    type = 1,
                                    need = "wood"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Убийственные цели",
                            Lvl = 4,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "syringe.medical",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "scrap",
                                    nabor = false,
                                    Amount = 450,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "lowgradefuel",
                                    nabor = false,
                                    Amount = 250,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить томсон",
                                    amount = 1,
                                    type = 3,
                                    need = "smg.thompson"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Поставить пистолетный патрон",
                                    amount = 100,
                                    type = 6,
                                    need = "ammo.pistol"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Изучить ледоруб",
                                    amount = 1,
                                    type = 4,
                                    need = "icepick.salvaged"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Добыть серы",
                                    amount = 1,
                                    type = 1,
                                    need = "sulfur.ore"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Подготовка к жоскому финалу",
                            Lvl = 5,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "syringe.medical",
                                    nabor = false,
                                    Amount = 15,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "scrap",
                                    nabor = false,
                                    Amount = 1000,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "rifle.bolt",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Поставить верстак 3 уровня",
                                    amount = 1,
                                    type = 6,
                                    need = "workbench3.deployed"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить калаш",
                                    amount = 1,
                                    type = 3,
                                    need = "rifle.ak"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Скрафтить патроны 5.56",
                                    amount = 100,
                                    type = 3,
                                    need = "ammo.rifle"
                                },
                            }
                        },
                        new Quest()
                        {
                            DisplayName = "Финальная битва",
                            Lvl = 6,
                            _listReward = new List<Reward>()
                            {
                                new Reward()
                                {
                                    ShortName = "sulfur.ore",
                                    nabor = false,
                                    Amount = 7000,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "metal.refined",
                                    nabor = false,
                                    Amount = 500,
                                    itemList = new List<Reward.Items>()
                                },
                                new Reward()
                                {
                                    ShortName = "rifle.l96",
                                    nabor = false,
                                    Amount = 1,
                                    itemList = new List<Reward.Items>()
                                }
                            },
                            _listZadach = new List<Zadachi>()
                            {
                                new Zadachi()
                                {
                                    DisplayName = "Взорвать танк с помощью С4",
                                    amount = 1,
                                    type = 2,
                                    need = "bradleyapc",
                                    Weapon = "explosive.timed.deployed"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Сбить вертолет с калаша",
                                    amount = 1,
                                    type = 2,
                                    need = "patrolhelicopter",
                                    Weapon = "rifle.ak"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить HeavyNPC",
                                    amount = 10,
                                    type = 2,
                                    need = "heavyscientist"
                                },
                                new Zadachi()
                                {
                                    DisplayName = "Убить игроков с калаша",
                                    amount = 15,
                                    type = 2,
                                    need = "player",
                                    Weapon = "rifle.ak"
                                },
                            }
                        }
                    }
                };
                newConfig._classList = new List<ClassPlayer>()
                {
                    new ClassPlayer()
                    {
                        Name = "Солдат",
                        Text = "-Ты можешь перестрелять макросника?\n-Решаешь споры 1 на 1?\n-Тогда это твой путь!",
                        URL = "https://i.imgur.com/HAmL1so.png",
                        Perm = "sopass.default"
                    },
                    new ClassPlayer()
                    {
                        Name = "Фармер",
                        Text = "-Ты лютый фармер?\n-Ты боишься стрелять?\n-Тогда это твой выбор!",
                        URL = "https://i.imgur.com/GOk1rqK.png",
                        Perm = "sopass.default"
                    },
                    new ClassPlayer()
                    {
                        Name = "Строитель",
                        Text = "-Любишь строить?\n-Хочешь получать плюшки за это?\n-Тогда тебе сюда!",
                        URL = "https://i.imgur.com/9ouiF2V.png",
                        Perm = "sopass.default"
                    },
                    new ClassPlayer()
                    {
                        Name = "Донатер",
                        Text = "-Только для донатеров!",
                        URL = "https://imgur.com/hLPnK7C.png",
                        Perm = "sopass.default"
                    },
                };
                return newConfig;
            }
        }

        protected override void LoadDefaultConfig()
        {
            cfg = ConfigData.GetNewConf();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(cfg);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        public class Zadachi
        {
            [JsonProperty("Текст задачи")] public string DisplayName = "";

            [JsonProperty(
                "Тип задачи(1-Добыть, 2-Убить, 3-Скрафтить, 4-Изучить,5-Залутать, 6-Поставить,7-Починить,8-Собрать с земли)")]
            public int type = 1;

            [JsonProperty("Задача закончен(Оставлять false)")]
            public bool IsFinished = false;

            [JsonProperty("Объект задачи(Тип Калаш-rifle.ak, Игрок-player")]
            public string need = "";

            [JsonProperty("Кол-во")] public int amount = 0;

            [JsonProperty("Оружие или инструмент(Например задача убить с калаша, тогда сюда rifle.ak)")]
            public string Weapon = "";
        }

        class Quest
        {
            [JsonProperty("Название уровня")] public string DisplayName;
            [JsonProperty("Какой лвл")] public int Lvl;

            [JsonProperty("Список наград(Если используете набор все параметры кроме \"Картинка\" и \"Шортнейм\" оставить пустыми и поставить использовать набор на true))")]
            public List<Reward> _listReward = new List<Reward>();

            [JsonProperty("Список задач")] public List<Zadachi> _listZadach = new List<Zadachi>();
        }

        public class PlayerData
        {
            [JsonProperty("НикНейм")] public string NickName;
            [JsonProperty("Класс")] public string Klass;
            [JsonProperty("Показ активных задач")] public bool ActiveZadachi = false;
            [JsonProperty("Лвл")] public int Lvl;
            [JsonProperty("Список активных заданий")]
            public List<Zadachi> listZadachi; 
            [JsonProperty("Список ревардов")] public List<Reward> ListRewards;
            [JsonProperty("Список классов в которых побывал человек")] public List<string> KlassList = new List<string>();
        } 

        #endregion
        #region ui  

        private static string Layer = "SoPassUI";
        private static string LayerMain = "SoPassUIMAIN";
        private string Hud = "Hud";
        private string Overlay = "Overlay";
        private string regular = "robotocondensed-regular.ttf";
        private static string Sharp = "assets/content/ui/ui.background.tile.psd";
        private static string Blur = "assets/content/ui/uibackgroundblur.mat";
        private string radial = "assets/content/ui/ui.background.transparent.radial.psd";

        private CuiPanel _fon = new CuiPanel()
        {
            RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
            CursorEnabled = true,
            Image = {Color = "0 0 0 0.87", Material = Blur}
        };

        private CuiPanel _mainFon = new CuiPanel()
        {
            RectTransform =
                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-1920 -1080", OffsetMax = "1920 1080"},
            Image = {Color = "0.26977050 0.2312312 0.312312312 0"}
        }; 

        [ChatCommand("pass")]
        private void Start(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var cont = new CuiElementContainer();
            cont.Add(_fon, Overlay, Layer);
            CuiHelper.AddUi(player, cont);
            PlayerData playerData;
            if (_playerData.TryGetValue(player.userID, out playerData))
            {
                if (playerData.Klass != "")
                {
                    if (playerData.Lvl <= cfg._listQuest[playerData.Klass].Count)
                    {
                        LoadZadach(player, 1);
                    }
                    else  if(cfg.nextklass)
                    {
                        NextKlass(player, playerData.Klass);
                    }
                    else
                    {
                        LoadZadach(player, 1);
                    }
                }
                else 
                {
                    StartUI(player, 0);
                }

            }
            else
            {
                StartUI(player, 0);
            }
        }

        private string _layerActive = "SoPassActiveLayer";   
        public void ActiveZadachi(BasePlayer player, PlayerData data)
        {
            if(!cfg.activeOn) return;
            CuiHelper.DestroyUi(player, _layerActive);
            var cont = new CuiElementContainer();
            cont.Add(new CuiPanel()
            {
                Image =
                { 
                    Color = "0 0 0 0.87",
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = cfg.OffsetMin,
                    OffsetMax = cfg.OffsetMax
                }
            }, Hud, _layerActive);
            cont.Add(new CuiElement()
            {
                Parent = _layerActive,
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color  = "0 0 0 0", Material = Blur
                    },
                    new CuiOutlineComponent()
                    {
                        Color = HexToRustFormat("#00fff7"), Distance = "0 2"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0.001",
                        AnchorMax = "0.995 0.979",
                    }
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = _layerActive,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "СПИСОК АКТИВНЫХ ЗАДАЧ",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.01388869 0.848889",
                        AnchorMax = "0.986111 0.9866667"
                    }
                }  
            });
            var text = string.Empty;
            foreach (var zadachi in data.listZadachi)
            {
                if (zadachi.amount > 0) text += string.Format(cfg.text1, zadachi.DisplayName, zadachi.amount) + "\n";
                else text += string.Format(cfg.text2, zadachi.DisplayName) + "\n";
            } 
            cont.Add(new CuiElement()
            {
                Parent = _layerActive,
                Components = 
                {
                    new CuiTextComponent()
                    {
                        Text = text.ToUpper(),
                        Align = TextAnchor.UpperCenter,
                        FontSize = 10,
                        Font = regular
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.03611085 0.06222236",
                        AnchorMax = "0.9694443 0.8000001"
                    }
                }
            });
            CuiHelper.AddUi(player, cont);
        }
        void NextKlass(BasePlayer player, string klass)
        {
            CuiHelper.DestroyUi(player, LayerMain);
            var cont = new CuiElementContainer();
            cont.Add(_mainFon, Layer, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Close = Layer, Color = "0.25 0.25 0.25 0.35"},
                Text = {Text = ""}
            }, LayerMain);
            cont.Add(new CuiElement()
            {
                Parent = LayerMain,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Вы прошли все задания данного класса, хотите ли вы выбрать другой класс?", Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.4211805 0.4925921", AnchorMax = "0.5850695 0.5413575"
                    }
                }
            });
            cont.Add(new CuiButton()
            {
                Button =
                {
                    Color = "0.25 0.25 0.25 0.35", 
                    Command = $"uisopass nextklass {klass}"
                },
                Text =
                {
                    Text = "Да, конечно!", Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0.466493 0.4790123", AnchorMax = "0.5342011 0.491358"
                }
            }, LayerMain);
            CuiHelper.AddUi(player, cont);
        }
        void StartUI(BasePlayer player, int num)
        {
            CuiHelper.DestroyUi(player, LayerMain);
            var cont = new CuiElementContainer();
            cont.Add(_mainFon, Layer, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Close = Layer, Color = "0 0 0 0"},
                Text = {Text = ""}
            }, LayerMain);
            if (num > cfg._classList.Count - 1) num = 0;
            int q1 = num;
            int q2 = q1 + 1;
            int q3 = q1 + 2;
            if (q1 < 0) q1 = cfg._classList.Count - 1;
            if (q1 > cfg._classList.Count - 1) q1 = 0;
            if (q2 < 0)
            {
                if (q3 < 0)
                {
                    q3 = cfg._classList.Count + num + 2;
                    q2 = cfg._classList.Count + num + 1;
                    q1 = cfg._classList.Count + num;
                    if (num == 1 - cfg._classList.Count) num = 1;
                }
                else
                {
                    q2 = cfg._classList.Count - 1;
                    q1 = cfg._classList.Count - 2;
                    q3 = 0;
                }
            }

            if (q3 > cfg._classList.Count - 1) q3 = 0;

            if (q2 > cfg._classList.Count - 1)
            {
                q2 = 0;
                q3 = 1;
            }

            cont.Add(new CuiElement()
            {
                Parent = LayerMain,
                Name = LayerMain + 1,
                Components =
                {
                    new CuiImageComponent() {Color = "0 0 0 0.65", Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiRectTransformComponent() {AnchorMin = "0.4126734 0.4549383", AnchorMax = "0.461979 0.592284"}
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = LayerMain,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "BATTLEPASS - ВЫБЕРИ СВОЙ КЛАСС", Align = TextAnchor.MiddleCenter, FontSize = 25
                    },
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.178819 0.6364198", AnchorMax = "0.8179512 0.6660494"}
                }
            });
            PlayerData playerData;
            if (permission.UserHasPermission(player.UserIDString, cfg._classList[q1].Perm))
            {
                if (_playerData.TryGetValue(player.userID, out playerData))
                {
                    if (playerData.KlassList.Contains(cfg._classList[q1].Name))
                    {
                        cont.Add(new CuiButton()
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                            },
                            Button =
                            {
                                Color = HexToRustFormat("#E103947A"), Command = $""
                            },
                            Text =
                            {
                                Text = "НЕДОСТУПНО", Align = TextAnchor.MiddleCenter, FontSize = 18,
                                Color = "0.64 0.64 0.64 1"
                            }
                        }, LayerMain + 1);  
                    }
                    else
                    {
                        cont.Add(new CuiButton()
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                            },
                            Button =
                            {
                                Color = HexToRustFormat("#E103945A"), Command = $"uisopass check {cfg._classList[q1].Name}"
                            },
                            Text =
                            {
                                Text = cfg._classList[q1].Name.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 18,
                                Color = "0.64 0.64 0.64 1"
                            }
                        }, LayerMain + 1); 
                    }
                }
                else
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                        },
                        Button =
                        {
                            Color = HexToRustFormat("#E103945A"), Command = $"uisopass check {cfg._classList[q1].Name}"
                        },
                        Text =
                        {
                            Text = cfg._classList[q1].Name.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 18,
                            Color = "0.64 0.64 0.64 1"
                        }
                    }, LayerMain + 1); 
                }
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                    },
                    Button =
                    {
                        Color = HexToRustFormat("#E103947A"), Command = $""
                    },
                    Text =
                    {
                        Text = "НЕДОСТУПНО", Align = TextAnchor.MiddleCenter, FontSize = 18,
                        Color = "0.64 0.64 0.64 1"
                    }
                }, LayerMain + 1);
            }

            cont.Add(new CuiElement()
            {
                Parent = LayerMain + 1,
                Components =
                {
                    new CuiImageComponent() {Color = "1 1 1 1", Png = GetImage(cfg._classList[q1].URL)},
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.1478906 0.532584", AnchorMax = "0.8274679 0.9460669"}
                }
            });

            cont.Add(new CuiElement()
            {
                Parent = LayerMain + 1,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = cfg._classList[q1].Text, Align = TextAnchor.MiddleCenter, Font = regular,
                        Color = "0.64 0.64 0.64 0.86"
                    },
                    new CuiRectTransformComponent() {AnchorMin = "0 0.1056177", AnchorMax = "0.995 0.3752807"}
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = LayerMain,
                Name = LayerMain + 2,
                Components =
                {
                    new CuiImageComponent() {Color = "0 0 0 0.65", Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiRectTransformComponent() {AnchorMin = "0.4716969 0.4549383", AnchorMax = "0.520999 0.592284"}
                }
            });
            if (permission.UserHasPermission(player.UserIDString, cfg._classList[q2].Perm))
            {
                if (_playerData.TryGetValue(player.userID, out playerData))
                {
                    if (playerData.KlassList.Contains(cfg._classList[q2].Name))
                    {
                        cont.Add(new CuiButton()
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                            },
                            Button =
                            {
                                Color = HexToRustFormat("#E103947A"), Command = $""
                            },
                            Text =
                            {
                                Text = "НЕДОСТУПНО", Align = TextAnchor.MiddleCenter, FontSize = 18,
                                Color = "0.64 0.64 0.64 1"
                            }
                        }, LayerMain + 2);
                    }
                    else
                    {
                        cont.Add(new CuiButton()
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                            },
                            Button =
                            {
                                Color = HexToRustFormat("#E103945A"), Command = $"uisopass check {cfg._classList[q2].Name}"
                            },
                            Text =
                            {
                                Text = cfg._classList[q2].Name.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 18,
                                Color = "0.64 0.64 0.64 1"
                            }
                        }, LayerMain + 2);  
                    }
                }
                else
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                        },
                        Button =
                        {
                            Color = HexToRustFormat("#E103945A"), Command = $"uisopass check {cfg._classList[q2].Name}"
                        },
                        Text =
                        {
                            Text = cfg._classList[q2].Name.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 18,
                            Color = "0.64 0.64 0.64 1"
                        }
                    }, LayerMain + 2);  
                }
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                    },
                    Button =
                    {
                        Color = HexToRustFormat("#E103947A"), Command = $""
                    },
                    Text =
                    {
                        Text = "НЕДОСТУПНО", Align = TextAnchor.MiddleCenter, FontSize = 18,
                        Color = "0.64 0.64 0.64 1"
                    }
                }, LayerMain + 2);
            }

            cont.Add(new CuiElement()
            {
                Parent = LayerMain + 2,
                Components =
                {
                    new CuiImageComponent() {Color = "1 1 1 1", Png = GetImage(cfg._classList[q2].URL)},
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.1478906 0.532584", AnchorMax = "0.8274679 0.9460669"}
                }
            });

            cont.Add(new CuiElement()
            {
                Parent = LayerMain + 2,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = cfg._classList[q2].Text, Align = TextAnchor.MiddleCenter, Font = regular,
                        Color = "0.64 0.64 0.64 0.86"
                    },
                    new CuiRectTransformComponent() {AnchorMin = "0 0.1056177", AnchorMax = "0.995 0.3752807"}
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = LayerMain,
                Name = LayerMain + 3,
                Components =
                {
                    new CuiImageComponent() {Color = "0 0 0 0.65", Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.5307257 0.4549383", AnchorMax = "0.5800229 0.592284"}
                }
            });
            if (permission.UserHasPermission(player.UserIDString, cfg._classList[q3].Perm))
            {
                if (_playerData.TryGetValue(player.userID, out playerData))
                {
                    if (playerData.KlassList.Contains(cfg._classList[q3].Name))
                    {
                        cont.Add(new CuiButton()
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                            },
                            Button =
                            {
                                Color = HexToRustFormat("#E103947A"), Command = $""
                            },
                            Text =
                            {
                                Text = "НЕДОСТУПНО", Align = TextAnchor.MiddleCenter, FontSize = 18,
                                Color = "0.64 0.64 0.64 1"
                            }
                        }, LayerMain + 3);
                    }
                    else
                    {
                        cont.Add(new CuiButton()
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                            },
                            Button =
                            {
                                Color = HexToRustFormat("#E103945A"), Command = $"uisopass check {cfg._classList[q3].Name}"
                            },
                            Text =
                            {
                                Text = cfg._classList[q3].Name.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 18,
                                Color = "0.64 0.64 0.64 1"
                            }
                        }, LayerMain + 3);
                    }
                }
                else
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                        },
                        Button =
                        {
                            Color = HexToRustFormat("#E103945A"), Command = $"uisopass check {cfg._classList[q3].Name}"
                        },
                        Text =
                        {
                            Text = cfg._classList[q3].Name.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 18,
                            Color = "0.64 0.64 0.64 1"
                        }
                    }, LayerMain + 3);
                }
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0.995 0.09754422"
                    },
                    Button =
                    {
                        Color = HexToRustFormat("#E103947A"), Command = $""
                    },
                    Text =
                    {
                        Text = "НЕДОСТУПНО", Align = TextAnchor.MiddleCenter, FontSize = 18,
                        Color = "0.64 0.64 0.64 1"
                    }
                }, LayerMain + 3);
            }

            cont.Add(new CuiElement()
            {
                Parent = LayerMain + 3,
                Components =
                {
                    new CuiImageComponent() {Color = "1 1 1 1", Png = GetImage(cfg._classList[q3].URL)},
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.1478906 0.532584", AnchorMax = "0.8274679 0.9460669"}
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = LayerMain + 3,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = cfg._classList[q3].Text, Align = TextAnchor.MiddleCenter, Font = regular,
                        Color = "0.64 0.64 0.64 0.86"
                    },
                    new CuiRectTransformComponent() {AnchorMin = "0 0.1056177", AnchorMax = "0.995 0.3752807"}
                }
            });
            if (cfg._classList.Count > 3)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0.4015632 0.4549383", AnchorMax = "0.4093743 0.592284"
                    },
                    Button = {Color = "0.64 0.64 0.64 0", Command = $"UISoPass page-- {num}"},
                    Text = {Text = "<", Align = TextAnchor.MiddleCenter, FontSize = 25}
                }, LayerMain);
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5836824 0.4549383", AnchorMax = "0.5914922 0.592284"
                    },
                    Button = {Color = "0.64 0.64 0.64 0", Command = $"UISoPass page++ {num}"},
                    Text = {Text = ">", Align = TextAnchor.MiddleCenter, FontSize = 25}
                }, LayerMain);
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.4716969 0.4351849", AnchorMax = "0.520999 0.4503129"},
                    Button = {Command = "uiopeninv", Color = "0.64 0.64 0.64 0.35"},
                    Text = {Text = "ОТКРЫТЬ ИНВЕНТАРЬ", Align = TextAnchor.MiddleCenter, Color = "0.64 0.64 0.64 0.66"}
                }, LayerMain);
            }

            CuiHelper.AddUi(player, cont);
        }

        private void LoadZadach(BasePlayer player, int page)
        {
            PlayerData f;
            if (!_playerData.TryGetValue(player.userID, out f)) return;
            var cont = new CuiElementContainer();
            CuiHelper.DestroyUi(player, LayerMain);
            cont.Add(_mainFon, Layer, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Close = Layer, Color = "0 0 0 0"},
                Text = {Text = ""}
            }, LayerMain);
            cont.Add(new CuiElement()
            {
                Parent = LayerMain,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = f.Klass.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 30
                    },
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.4678819 0.6364198", AnchorMax = "0.5279512 0.6660494"}
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = LayerMain,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"Ваш уровень: {f.Lvl}", Align = TextAnchor.MiddleCenter, FontSize = 12
                    },
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.4678819 0.6364198", AnchorMax = "0.5279512 0.6460494"}
                }
            });
            if (page <= cfg._listQuest[f.Klass].Count - 5 * page)
            {
                cont.Add(new CuiButton()
                { 
                    RectTransform = {AnchorMin = "0.6519127 0.3333333", AnchorMax = "0.6666672 0.6666666"},
                    Button = {Command = $"UISoPass page {page + 1}", Color = "0.1 0.312312 0.31231 0"},
                    Text = {Text = ">", Align = TextAnchor.MiddleCenter, FontSize = 30}
                }, LayerMain);
            }
  
            if (page > 1) 
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.3333333 0.3333333", AnchorMax = "0.3480903 0.6666666"},
                    Button = {Command = $"UISoPass page {page - 1}", Color = "0 0 0 0"},
                    Text = {Text = "<", Align = TextAnchor.MiddleCenter, FontSize = 30}
                }, LayerMain);
            }

            var findZadah = cfg._listQuest[f.Klass];
            if (findZadah == null)
            {
                Puts("Проблема в конфиге");
                return;
            }

            foreach (var quest in findZadah.Select((i, t) => new {A = i, B = t - (page - 1) * 5}).Skip((page - 1) * 5)
                .Take(5))
            {
                cont.Add(new CuiElement()
                {
                    Parent = LayerMain,
                    Name = Layer + quest.B,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.12 0.12 0.12 0.64",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{0.3472222} {0.5799382 - Math.Floor((double) quest.B / 1) * 0.058}",
                            AnchorMax = $"{0.6531252} {0.6345679 - Math.Floor((double) quest.B / 1) * 0.058}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + quest.B,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = quest.A.DisplayName.ToUpper(), Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.007815376 0.8079098", AnchorMax = "0.1708284 0.9661027"}
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + quest.B,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = "Уровень: " + quest.A.Lvl, Align = TextAnchor.MiddleCenter, Font = regular,
                            FontSize = 10
                        },
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.007815376 0.6836164", AnchorMax = "0.1708284 0.8022601"}
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + quest.B,
                    Components =
                    {
                        new CuiImageComponent() {Color = "1 1 1 1"},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.007815376 0.668419", AnchorMax = "0.186152 0.6779668"}
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + quest.B,
                    Components =
                    {
                        new CuiImageComponent() {Color = "1 1 1 1"},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.1853143 0.04519862", AnchorMax = "0.1861517 0.9378539"}
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + quest.B,
                    Components =
                    {
                        new CuiImageComponent() {Color = "1 1 1 1"},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.852277 0.04519862", AnchorMax = "0.8530146 0.9378539"}
                    }
                });
                float i = 0;
                foreach (var zadachi in quest.A._listZadach)
                {
                    var find = f.listZadachi.Find(p => p.DisplayName == zadachi.DisplayName);
                    if (find != null && f.Lvl == quest.A.Lvl)
                    {
                        if (find.IsFinished)
                        {
                            cont.Add(new CuiElement()
                            {
                                Parent = Layer + quest.B,
                                Components =
                                {
                                    new CuiTextComponent()
                                    {
                                        Text = "ВЫПОЛНЕНО", Align = TextAnchor.MiddleCenter, FontSize = 12,
                                        Color = HexToRustFormat("#E10394")
                                    },
                                    new CuiRectTransformComponent()
                                    {
                                        AnchorMin = $"0.007815376 {0.485876 - i}",
                                        AnchorMax = $"0.1708284 {0.6327676 - i}"
                                    }
                                }
                            });
                        }
                        else
                        {
                            cont.Add(new CuiElement()
                            {
                                Parent = Layer + quest.B,
                                Components =
                                {
                                    new CuiTextComponent()
                                    {
                                        Text = $"{zadachi.DisplayName.ToUpper()}: {find.amount}",
                                        Align = TextAnchor.MiddleCenter, FontSize = 10,
                                        Color = HexToRustFormat("#ff4d4d8A")
                                    },
                                    new CuiRectTransformComponent()
                                    {
                                        AnchorMin = $"0.007815376 {0.485876 - i}",
                                        AnchorMax = $"0.1708284 {0.6327676 - i}"
                                    }
                                }
                            });
                        }
                    }
                    else
                    {
                        cont.Add(new CuiElement()
                        {
                            Parent = Layer + quest.B,
                            Components =
                            {
                                new CuiTextComponent()
                                {
                                    Text = $"{zadachi.DisplayName.ToUpper()}", Align = TextAnchor.MiddleCenter,
                                    FontSize = 10
                                },
                                new CuiRectTransformComponent()
                                {
                                    AnchorMin = $"0.007815376 {0.485876 - i}", AnchorMax = $"0.1708284 {0.6327676 - i}"
                                }
                            }
                        });
                    }

                    i += 0.0952f;
                }

                i = 0;
                foreach (var zadReward in quest.A._listReward)
                { 
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + quest.B,
                        Components =
                        {
                            new CuiImageComponent() {Color = "0 0 0 0", Material = Blur},
                            new CuiOutlineComponent() {Distance = "0 1", Color = "1 1 1 1"},
                            new CuiRectTransformComponent()
                                {AnchorMin = $"{0.2037455 + i} 0.1807913", AnchorMax = $"{0.2763903 + i} 0.8531086"}
                        }
                    });
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + quest.B,
                        Name = Layer + quest.B + "ok",
                        Components =
                        {
                            new CuiRawImageComponent() {Color = "1 1 1 1", Png = GetImage(zadReward.ShortName)},
                            new CuiRectTransformComponent()
                                {AnchorMin = $"{0.2037455 + i} 0.1807913", AnchorMax = $"{0.2763903 + i} 0.8531086"}
                        }
                    });
                    if (zadReward.Amount > 0)
                    {
                        cont.Add(new CuiElement()
                        {
                            Parent = Layer + quest.B + "ok",
                            Components =
                            {
                                new CuiTextComponent()
                                {
                                    Text = $"x{zadReward.Amount}", Align = TextAnchor.LowerRight, FontSize = 10
                                },
                                new CuiRectTransformComponent() {AnchorMin = $"0 0.05", AnchorMax = $"0.95 1"}
                            }
                        });
                    }

                    i += 0.0752f;
                }

                if (quest.A.Lvl > f.Lvl)
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = $"0.8615204 0.40", AnchorMax = $"0.9886485 0.60"},
                        Button = {Command = "", Color = HexToRustFormat("#E103945A")},
                        Text = {Text = "НЕДОСТУПНО", Color = "0.64 0.64 0.64 0.64", Align = TextAnchor.MiddleCenter}
                    }, Layer + quest.B);
                }
                else if (f.Lvl == quest.A.Lvl && f.listZadachi.Count > 0 && f.listZadachi.All(p => p.IsFinished))
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = $"0.8615204 0.40", AnchorMax = $"0.9886485 0.60"},
                        Button = {Command = $"UISoPass takereward {page}", Color = HexToRustFormat("#66a4908A")},
                        Text =
                        {
                            Text = "ЗАБРАТЬ НАГРАДУ", Color = "0.85 0.85 0.85 1", Align = TextAnchor.MiddleCenter
                        }
                    }, Layer + quest.B, Layer + "ACCEPT");
                }
                else if (f.Lvl == quest.A.Lvl && f.listZadachi.Count > 0 && !f.listZadachi.All(p => p.IsFinished))
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = $"0.8615204 0.40", AnchorMax = $"0.9886485 0.60"},
                        Button = {Command = "", Color = HexToRustFormat("#ff4d4d5A")},
                        Text =
                        {
                            Text = "ВЫПОЛНЯЕТСЯ", Color = "0.64 0.64 0.64 0.64", Align = TextAnchor.MiddleCenter
                        }
                    }, Layer + quest.B, Layer + "ACCEPT");
                }
                else if (f.Lvl == quest.A.Lvl)
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = $"0.8615204 0.40", AnchorMax = $"0.9886485 0.60"},
                        Button =
                        {
                            Command = $"UISoPass start {page}", Color = HexToRustFormat("#66a4909a")
                        },
                        Text = {Text = "ВЗЯТЬ ЗАДАНИЕ", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter}
                    }, Layer + quest.B, Layer + "ACCEPT");
                }
                else
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = $"0.8615204 0.40", AnchorMax = $"0.9886485 0.60"},
                        Button =
                        {
                            Command = $"", Color = HexToRustFormat("#ff4d4d3A")
                        },
                        Text = {Text = "ЗАВЕРШЕНО", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter}
                    }, Layer + quest.B, Layer + "ACCEPT");
                }
            }

            if (cfg.activeOn)
            {
                if (!f.ActiveZadachi)
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = "0.5532937 0.6376544", AnchorMax = "0.602252 0.6518518"},
                        Button = {Command = "UISoPass activeZadachi", Color = "0.64 0.64 0.64 0.35"},
                        Text = {Text = "ВКЛЮЧИТЬ ПОКАЗ АКТИВНЫХ ЗАДАЧ", Align = TextAnchor.MiddleCenter, Color = "0.64 0.64 0.64 0.66", FontSize = 10}
                    }, LayerMain, LayerMain + "ActiveZad");   
                }
                else
                {
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = "0.5532937 0.6376544", AnchorMax = "0.602252 0.6518518"},
                        Button = {Command = "UISoPass activeZadachi ", Color = "0.64 0.64 0.64 0.35"},
                        Text = {Text = "ВЫКЛЮЧИТЬ ПОКАЗ АКТИВНЫХ ЗАДАЧ", Align = TextAnchor.MiddleCenter, Color = "0.64 0.64 0.64 0.66", FontSize = 10}
                    }, LayerMain, LayerMain + "ActiveZad");
                } 
            }
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.6086459 0.6376544", AnchorMax = "0.6526042 0.6518518"},
                Button = {Command = "uiopeninv", Color = "0.64 0.64 0.64 0.35"},
                Text = {Text = "ОТКРЫТЬ ИНВЕНТАРЬ", Align = TextAnchor.MiddleCenter, Color = "0.64 0.64 0.64 0.66"}
            }, LayerMain);
            CuiHelper.AddUi(player, cont);
        }

        [ConsoleCommand("uiopeninv")]
        void OpenInv(ConsoleSystem.Arg arg)
        {
            LoadInv(arg.Player(), 1);
        }


        private void LoadPanelNagrads(BasePlayer player, int page, string klass = "Солдат")
        {
            CuiHelper.DestroyUi(player, LayerMain);
            var cont = new CuiElementContainer();
            cont.Add(_mainFon, Layer, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Close = Layer, Color = "0 0 0 0"},
                Text = {Text = ""}
            }, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.4315972 0.6185169", AnchorMax = "0.569618 0.6654304"},
                Button = {Close = Layer, Color = "0 0 0 0"},
                Text = {Text = $"НАГРАДЫ ДЛЯ КЛАССА {klass.ToUpper()}", Align = TextAnchor.MiddleCenter, FontSize = 25}
            }, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.4979166 0.3694502", AnchorMax = "0.5470486 0.3824074"},
                Button = {Command = $"uisopass class {klass}", Color = HexToRustFormat("#66a4908A")},
                Text = {Text = "ВЫБРАТЬ КЛАСС", Align = TextAnchor.MiddleCenter}
            }, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.4479202 0.3694502", AnchorMax = "0.4970603 0.3824074"},
                Button = {Command = "chat.say /pass", Color = HexToRustFormat("#ff4d4d5A")},
                Text = {Text = "ВЕРНУТЬСЯ К ВЫБОРУ", Align = TextAnchor.MiddleCenter}
            }, LayerMain);
            for (int i = 0; i < 36; i++)
            {
                cont.Add(new CuiElement()
                {
                    Parent = LayerMain,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.25 0.25 0.25 0.64", Material = Blur, Sprite = radial
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.3604166 + i * 0.030 - Math.Floor((double) i / 9) * 9 * 0.030} {0.566358 - Math.Floor((double) i / 9) * 0.05}",
                            AnchorMax =
                                $"{0.3881944 + i * 0.030 - Math.Floor((double) i / 9) * 9 * 0.030} {0.6132715 - Math.Floor((double) i / 9) * 0.05}"
                        }
                    }
                });
            }

            CuiHelper.AddUi(player, cont);
            LoadNagrads(player, page, klass);
        }

        private void LoadNagrads(BasePlayer player, int page, string klass)
        {
            int f;
            var cont = new CuiElementContainer();
            Dictionary<string, int> nameList = new Dictionary<string, int>();
            foreach (var reawrd in from quest in cfg._listQuest[klass] from reawrd in quest._listReward select reawrd)
            {
                if (nameList.ContainsKey(reawrd.ShortName)) nameList[reawrd.ShortName] += reawrd.Amount;
                else nameList.Add(reawrd.ShortName, reawrd.Amount);
            } 

            if (page <= nameList.Count - 36 * page)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.6519127 0.3333333", AnchorMax = "0.6666672 0.6666666"},
                    Button = {Command = $"UISoPass next {page + 1} {klass}", Color = "0.1 0.312312 0.31231 0"},
                    Text = {Text = ">", Align = TextAnchor.MiddleCenter, FontSize = 30}
                }, LayerMain);
            }

            if (page > 1)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.3333333 0.3333333", AnchorMax = "0.3480903 0.6666666"},
                    Button = {Command = $"UISoPass next {page - 1} {klass}", Color = "0 0 0 0"},
                    Text = {Text = "<", Align = TextAnchor.MiddleCenter, FontSize = 30}
                }, LayerMain);
            }

            foreach (var reward in nameList.Select((i, t) => new {A = i, B = t - (page - 1) * 36}).Skip((page - 1) * 36)
                .Take(36))
            {
                cont.Add(new CuiElement()
                {
                    Parent = LayerMain,
                    Name = Layer + reward.B,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Color = "1 1 1 1", Png = GetImage(reward.A.Key)
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.3604166 + reward.B * 0.030 - Math.Floor((double) reward.B / 9) * 9 * 0.030} {0.566358 - Math.Floor((double) reward.B / 9) * 0.05}",
                            AnchorMax =
                                $"{0.3881944 + reward.B * 0.030 - Math.Floor((double) reward.B / 9) * 9 * 0.030} {0.6132715 - Math.Floor((double) reward.B / 9) * 0.05}"
                        }
                    }
                });
                if (reward.A.Value > 0)
                {
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + reward.B,
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"x{reward.A.Value} ", Align = TextAnchor.LowerRight, Font = regular,
                                FontSize = 14,
                                Color = "0.85 0.85 0.85 0.85"
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    });
                }
            }

            CuiHelper.AddUi(player, cont);
        }

        private void LoadInv(BasePlayer player, int page)
        {
            PlayerData f;
            if (!_playerData.TryGetValue(player.userID, out f)) return;
            CuiHelper.DestroyUi(player, LayerMain);
            var cont = new CuiElementContainer();
            cont.Add(_mainFon, Layer, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Close = Layer, Color = "0 0 0 0"},
                Text = {Text = ""}
            }, LayerMain);
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.4788195 0.6283951", AnchorMax = "0.5209987 0.6425911"},
                Button = {Command = "chat.say /pass", Color = "0.64 0.64 0.64 0.35"},
                Text = {Text = "ВЕРНУТЬСЯ", Align = TextAnchor.MiddleCenter, Color = "0.64 0.64 0.64 0.66"}
            }, LayerMain);
            if (page > 1)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.4015673 0.4870409", AnchorMax = "0.4157873 0.5117263"},
                    Button =
                    {
                        Color = "0.64 0.64 0.64 0",
                        Command = $"uisopass nextpage {page - 1}"
                    },
                    Text =
                    {
                        Text = "<", Align = TextAnchor.MiddleCenter, FontSize = 25
                    }
                }, LayerMain, Layer + "NextPage-");
            }

            if (page <= f.ListRewards.Count - 20 * page)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.5868118 0.4870409", AnchorMax = "0.6010293 0.5117263"},
                    Button =
                    {
                        Color = "0.64 0.64 0.64 0",
                        Command = $"uisopass nextpage {page + 1}"
                    },
                    Text = {Text = ">", Align = TextAnchor.MiddleCenter, FontSize = 25}
                }, LayerMain, Layer + "NextPage+");
            }

            foreach (var key in f.ListRewards.Select((i, t) => new {A = i, B = t - (page - 1) * 20})
                .Skip((page - 1) * 20)
                .Take(20))
            {
                cont.Add(new CuiElement()
                {
                    Parent = LayerMain,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.64 0.64 0.64 0", Material = Blur, Sprite = radial
                        },
                        new CuiOutlineComponent() {Color = "1 1 1 1", Distance = "0 1"},
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.4290492 + key.B * 0.030 - Math.Floor((double) key.B / 5) * 5 * 0.030} {0.5478395 - Math.Floor((double) key.B / 5) * 0.05}",
                            AnchorMax =
                                $"{0.4565972 + key.B * 0.030 - Math.Floor((double) key.B / 5) * 5 * 0.030} {0.5935185 - Math.Floor((double) key.B / 5) * 0.05}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = LayerMain,
                    Name = Layer + key.B,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.64 0.64 0.64 0", Material = Blur, Sprite = radial
                        },
                        new CuiOutlineComponent() {Color = "1 1 1 1", Distance = "0 1"},
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.4290492 + key.B * 0.030 - Math.Floor((double) key.B / 5) * 5 * 0.030} {0.5478395 - Math.Floor((double) key.B / 5) * 0.05}",
                            AnchorMax =
                                $"{0.4565972 + key.B * 0.030 - Math.Floor((double) key.B / 5) * 5 * 0.030} {0.5935185 - Math.Floor((double) key.B / 5) * 0.05}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + key.B,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Color = "1 1 1 1", Png = GetImage(key.A.ShortName)
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                });
                if (key.A.Amount > 0)
                {
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + key.B,
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"x{key.A.Amount} ", Align = TextAnchor.LowerRight, Font = regular,
                                FontSize = 14,
                                Color = "0.85 0.85 0.85 0.85"
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    });
                }

                cont.Add(new CuiButton()
                {
                    Button = {Command = $"UISoPass takeinv {page} {key.B}", Color = "0 0 0 0"},
                    Text = {Text = $""},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + key.B);
            }

            CuiHelper.AddUi(player, cont);
        }

        [ConsoleCommand("adminpass")]
        void ConsoleCommand(ConsoleSystem.Arg arg)
        {
            if(arg.Player() != null) return;
            if(arg.Args == null ||arg.Args.Length < 1) return;
            ulong steamId;
            PlayerData playerData;
            switch (arg.Args[0])
            {
                case "remove":
                    if (arg.Args.Length < 2)
                    {
                        Puts("[SYNTAX] /adminpass remove STEAMID");
                        return;
                    }
                    if (!ulong.TryParse(arg.Args[1], out steamId))
                    {
                        Puts("Введите стим айди игрока");
                        return;
                    }
                    if (!_playerData.TryGetValue(steamId, out playerData))
                    {
                        Puts("Игрок не найден в базе");
                        return;
                    }
                    Puts($"Игрок {playerData.NickName} удален из даты");
                    _playerData.Remove(steamId);
                    break;
                case "setlvl":
                    if (arg.Args.Length < 3)
                    {
                        Puts("[SYNTAX] /adminpass setlvl STEAMID LVL");
                        return;
                    }
                    if (!ulong.TryParse(arg.Args[1], out steamId))
                    {
                        Puts("Введите стим айди игрока");
                        return;
                    }

                    if (!_playerData.TryGetValue(steamId, out playerData))
                    {
                        Puts("Игрок не найден в базе");
                        return;
                    }
                    Puts($"Вы изменили лвл игрока {playerData.NickName} на {arg.Args[2]}");
                    playerData.Lvl = arg.Args[2].ToInt();
                    break;
                case "zadachiremove":
                    if (arg.Args.Length < 2)
                    {
                        Puts( "[SYNTAX] /adminpass zadachiremove STEAMID LVL");
                        return;
                    }
                    if (!ulong.TryParse(arg.Args[1], out steamId))
                    {
                        Puts("Введите стим айди игрока");
                        return;
                    }

                    if (!_playerData.TryGetValue(steamId, out playerData))
                    {
                        Puts("Игрок не найден в базе");
                        return;
                    }
                    Puts($"Вы удалили все активные задачи игрока {playerData.NickName}");
                    playerData.listZadachi.Clear();
                    break;
                case "klassclear":
                    if (arg.Args.Length < 2)
                    {
                        Puts( "[SYNTAX] /adminpass zadachiremove STEAMID LVL");
                        return;
                    }
                    if (!ulong.TryParse(arg.Args[1], out steamId))
                    {
                        Puts("Введите стим айди игрока");
                        return;
                    }

                    if (!_playerData.TryGetValue(steamId, out playerData))
                    {
                        Puts("Игрок не найден в базе");
                        return;
                    }
                    Puts($"Вы удалили все пройденные классы игрока {playerData.NickName}");
                    playerData.KlassList.Clear();
                    break;
            }
        }
        [ChatCommand("adminpass")]
        void AdminCommand(BasePlayer player, string command, string[] arg)
        {
            if(!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "sopass.admin")) return;
            if(arg.Length < 1) return;
            ulong steamId;
            PlayerData playerData;
            switch (arg[0])
            {
                case "remove":
                    if (arg.Length < 2)
                    {
                        ReplySend(player, "[SYNTAX] /adminpass remove STEAMID");
                        return;
                    }
                    if (!ulong.TryParse(arg[1], out steamId))
                    {
                        ReplySend(player, "Введите стим айди игрока");
                        return;
                    }
                    if (!_playerData.TryGetValue(steamId, out playerData))
                    {
                        ReplySend(player, "Игрок не найден в базе");
                        return;
                    }
                    ReplySend(player, $"Игрок {playerData.NickName} удален из даты");
                    _playerData.Remove(steamId);
                    break;
                case "setlvl":
                    if (arg.Length < 3)
                    {
                        ReplySend(player, "[SYNTAX] /adminpass setlvl STEAMID LVL");
                        return;
                    }
                    if (!ulong.TryParse(arg[1], out steamId))
                    {
                        ReplySend(player, "Введите стим айди игрока");
                        return;
                    }

                    if (!_playerData.TryGetValue(steamId, out playerData))
                    {
                        ReplySend(player, "Игрок не найден в базе");
                        return;
                    }
                    ReplySend(player, $"Вы изменили лвл игрока {playerData.NickName} на {arg[2]}");
                    playerData.Lvl = arg[2].ToInt();
                    break;
                case "zadachiremove":
                    if (arg.Length < 2)
                    {
                        ReplySend(player, "[SYNTAX] /adminpass zadachiremove STEAMID LVL");
                        return;
                    }
                    if (!ulong.TryParse(arg[1], out steamId))
                    {
                        ReplySend(player, "Введите стим айди игрока");
                        return;
                    }

                    if (!_playerData.TryGetValue(steamId, out playerData))
                    {
                        ReplySend(player, "Игрок не найден в базе");
                        return;
                    }
                    ReplySend(player, $"Вы удалили все активные задачи игрока {playerData.NickName}");
                    playerData.listZadachi.Clear();
                    break;
                case "klassclear":
                    if (arg.Length < 2)
                    {
                        ReplySend(player, "[SYNTAX] /adminpass zadachiremove STEAMID LVL");
                        return;
                    }
                    if (!ulong.TryParse(arg[1], out steamId))
                    {
                        ReplySend(player, "Введите стим айди игрока");
                        return;
                    }

                    if (!_playerData.TryGetValue(steamId, out playerData))
                    {
                        ReplySend(player, "Игрок не найден в базе");
                        return;
                    }
                    ReplySend(player, $"Вы удалили все пройденные классы игрока {playerData.NickName}");
                    playerData.KlassList.Clear();
                    break;    
            }
        }
        [ConsoleCommand("UISoPass")]
        private void SoPassCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            PlayerData f;
            switch (arg.Args[0])
            {
                case "activeZadachi":
                    if (!_playerData.TryGetValue(player.userID, out f)) return;
                    if (!f.ActiveZadachi)
                    {
                        CuiHelper.DestroyUi(player, _layerActive);
                        CuiHelper.DestroyUi(player, LayerMain + "ActiveZad");
                        f.ActiveZadachi = true;
                        var cont = new CuiElementContainer();
                        cont.Add(new CuiButton()   
                        { 
                            RectTransform = {AnchorMin = "0.5532937 0.6376544", AnchorMax = "0.602252 0.6518518"},
                            Button = {Command = "UISoPass activeZadachi ", Color = "0.64 0.64 0.64 0.35"},
                            Text = {Text = "ВЫКЛЮЧИТЬ ПОКАЗ АКТИВНЫХ ЗАДАЧ", Align = TextAnchor.MiddleCenter, Color = "0.64 0.64 0.64 0.66", FontSize = 10}
                        }, LayerMain, LayerMain + "ActiveZad");
                        CuiHelper.AddUi(player, cont);
                        if(f.listZadachi.Count > 0) ActiveZadachi(player, f);
                    }
                    else 
                    {
                        CuiHelper.DestroyUi(player, _layerActive);
                        CuiHelper.DestroyUi(player, LayerMain + "ActiveZad");
                        f.ActiveZadachi = false;
                        var cont = new CuiElementContainer();
                        cont.Add(new CuiButton()
                        {
                            RectTransform = {AnchorMin = "0.5532937 0.6376544", AnchorMax = "0.602252 0.6518518"},
                            Button = {Command = "UISoPass activeZadachi ", Color = "0.64 0.64 0.64 0.35"},
                            Text = {Text = "ВКЛЮЧИТЬ ПОКАЗ АКТИВНЫХ ЗАДАЧ", Align = TextAnchor.MiddleCenter, Color = "0.64 0.64 0.64 0.66", FontSize = 10}
                        }, LayerMain, LayerMain + "ActiveZad");
                        CuiHelper.AddUi(player, cont); 
                    }
                    break;
                case "page++":
                    StartUI(arg.Player(), arg.Args[1].ToInt() + 1);
                    break;
                case "check":
                    LoadPanelNagrads(arg.Player(), 1, arg.Args[1]);
                    break;
                case "next":
                    LoadPanelNagrads(arg.Player(), arg.Args[1].ToInt(), arg.Args[2]);
                    break;
                case "page":
                    LoadZadach(player, arg.Args[1].ToInt());
                    break;
                case "takeinv":
                    if (!_playerData.TryGetValue(player.userID, out f)) return;
                    var t = f.ListRewards[arg.Args[2].ToInt()];
                    f.ListRewards.RemoveAt(arg.Args[2].ToInt());
                    LoadInv(player, arg.Args[1].ToInt());
                    if (t.nabor)
                    {
                        foreach (var itemse in t.itemList)
                        {

                            if (string.IsNullOrEmpty(itemse.command))
                            {
                                var item = ItemManager.CreateByName(itemse.ShortName, itemse.Amount, itemse.SkinId);
                                if (!arg.Player().inventory.GiveItem(item))
                                    item.Drop(player.inventory.containerMain.dropPosition,
                                        player.inventory.containerMain.dropVelocity);
                            }
                            else
                            {
                                rust.RunServerCommand(string.Format(itemse.command, player.userID));
                            }
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(t.command))
                        {
                            var item = ItemManager.CreateByName(t.ShortName, t.Amount, t.SkinId);
                            if (!arg.Player().inventory.GiveItem(item))
                                item.Drop(player.inventory.containerMain.dropPosition,
                                    player.inventory.containerMain.dropVelocity);
                        }
                        else
                        {
                            rust.RunServerCommand(string.Format(t.command, player.userID));
                        }
                        
                    }
                    break;
                case "nextklass":
                    if (!_playerData.TryGetValue(player.userID, out f)) return;
                    f.KlassList.Add(arg.Args[1]);
                    f.Klass = "";
                    f.Lvl = 0;
                    StartUI(player, 0);
                    break;
                case "nextpage":
                    LoadInv(player, arg.Args[1].ToInt());
                    break;
                case "page--":
                    StartUI(arg.Player(), arg.Args[1].ToInt() - 1);
                    break;
                case "class":
                    if (_playerData.TryGetValue(player.userID, out f))
                    {
                        f.Lvl = 1;
                        f.NickName = player.displayName;
                        f.Klass = string.Join(" ", arg.Args.Skip(1).ToArray());
                        f.listZadachi = new List<Zadachi>();
                    }
                    else 
                    {
                        _playerData.Add(player.userID, new PlayerData()
                        { 
                            Lvl = 1,
                            NickName = player.displayName,
                            Klass = string.Join(" ", arg.Args.Skip(1).ToArray()),
                            listZadachi = new List<Zadachi>(),
                            ListRewards = new List<Reward>(),
                            KlassList =  new List<string>(),
                        }); 
                    }
                    LoadZadach(player, 1);
                    break;
                case "start":
                    if (!_playerData.TryGetValue(player.userID, out f)) return;
                    var klass = f.Klass;
                    var findQuest = cfg._listQuest[klass].Find(p => p.Lvl == f.Lvl);
                    foreach (var zadachi in findQuest._listZadach)
                    {
                       f.listZadachi.Add(new Zadachi()
                       {
                           amount = zadachi.amount,
                           DisplayName = zadachi.DisplayName,
                           IsFinished = false,
                           need = zadachi.need,
                           type = zadachi.type,
                           Weapon = zadachi.Weapon
                       });
                    }
                    LoadZadach(player, arg.Args[1].ToInt());
                    ActiveZadachi(player, f);
                    break;
                case "takereward": 
                    if (!_playerData.TryGetValue(player.userID, out f)) return;
                    klass = f.Klass;
                    findQuest = cfg._listQuest[klass].Find(p => p.Lvl == f.Lvl);
                    foreach (var reward in findQuest._listReward)
                    {
                        f.ListRewards.Add(reward);
                    }

                    f.listZadachi.Clear();
                    f.Lvl += 1;
                    LoadZadach(player, arg.Args[1].ToInt());
                    CuiHelper.DestroyUi(player, _layerActive);
                    break;
            }  
        }

        #endregion
        #region Hooks

        class LastAttacker
        {
            public ulong SteamId;
            public string Weapon;
        }
        Dictionary<ulong, LastAttacker> lastdamage = new Dictionary<ulong, LastAttacker>();

        private void OnNewSave(string filename)
        {
            if(!cfg.newsave) return;
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("SoPass")) return;
            var playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("SoPass");
            foreach (var keyValuePair in playerData)
            { 
                keyValuePair.Value.Klass = "";
                keyValuePair.Value.Lvl = 1;
                keyValuePair.Value.listZadachi.Clear();
            }
            Interface.Oxide.DataFileSystem.WriteObject("SoPass", playerData);
            Interface.Oxide.ReloadPlugin("SoPass");
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if(!cfg.activeOn) return;
            timer.Once(1f, () =>
            {
                PlayerData f;
                if (!_playerData.TryGetValue(player.userID, out f)) return;
                if(f.ActiveZadachi && f.listZadachi.Count > 0) ActiveZadachi(player, f);
            });
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BaseHelicopter && info.Initiator is BasePlayer)
            {
                
                var _weapon = info?.Weapon?.GetItem()?.info?.shortname ?? info?.WeaponPrefab?.name ;
                if (!lastdamage.ContainsKey(entity.net.ID.Value))
                {
                    lastdamage.Add(entity.net.ID.Value, new LastAttacker()
                    {  
                        SteamId =  info.InitiatorPlayer.userID,
                        Weapon = _weapon 
                    }); 
                }
                else
                {
                    lastdamage[entity.net.ID.Value].SteamId = info.InitiatorPlayer.userID;
                    lastdamage[entity.net.ID.Value].Weapon = _weapon;
                }
            }
            if (entity is BradleyAPC && info.Initiator is BasePlayer)
            {
                var _weapon = info?.Weapon?.GetItem()?.info?.shortname ?? info?.WeaponPrefab?.name;
                if (!lastdamage.ContainsKey(entity.net.ID.Value))
                {
                    lastdamage.Add(entity.net.ID.Value, new LastAttacker()
                    {
                        SteamId =  info.InitiatorPlayer.userID,
                        Weapon = _weapon
                    });
                }
                else
                {
                    lastdamage[entity.net.ID.Value].SteamId = info.InitiatorPlayer.userID;
                    lastdamage[entity.net.ID.Value].Weapon = _weapon;
                }
            }
        }
        private object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {   
            if (dispenser == null || player == null || item == null) return null;
            var amount = item.amount;
            NextTick(() =>
            {
                PlayerData f;
                if (!_playerData.TryGetValue(player.userID, out f)) return;
                var findZadah = f.listZadachi.FindAll(p => p.type == 1)
                    ?.FindAll(p => p.need.Contains(item.info.shortname));
                if (findZadah.Count < 1) return;
                
                foreach (var zadachi in findZadah)
                {
                    if (player.GetActiveItem() == null)
                        Check(player, zadachi, amount, f);
                    else
                        Check(player, zadachi, amount, f, player.GetActiveItem().info.shortname);
                }
            });
            return null;
        }

        private object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) =>
            OnDispenserGather(dispenser, player, item);

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            PlayerData f;
            if(_islooted.Contains(entity))
            {
                _islooted.Remove(entity);
            }
            if (entity is BradleyAPC || entity is BaseHelicopter)
            {
                LastAttacker attackerid;
                if(!lastdamage.TryGetValue(entity.net.ID.Value, out attackerid)) return;
                if (!_playerData.TryGetValue(attackerid.SteamId, out f)) return;
                var findZadahHeliOrTank = f.listZadachi.FindAll(p => p.type == 2)?.Find(p => p.need.Contains(entity.ShortPrefabName));
                if (findZadahHeliOrTank == null) return;  
                Check(BasePlayer.FindByID(attackerid.SteamId), findZadahHeliOrTank, 1, f, attackerid.Weapon);
                lastdamage.Remove(entity.net.ID.Value);
                return;
            }
            if (entity == null || info == null || !(info.Initiator as BasePlayer)) return;
            if (entity is BuildingBlock)
            {
                var attackerBuild = info.InitiatorPlayer;
                var weapon = info?.Weapon?.GetItem()?.info?.shortname ?? info?.WeaponPrefab?.name;
                if(!attackerBuild.userID.IsSteamId() || attackerBuild.IsNpc) return;
                var build = entity as BuildingBlock;
                if (entity.OwnerID == attackerBuild.userID || IsFriends(entity.OwnerID, attackerBuild.userID)) return;
                if (!_playerData.TryGetValue(attackerBuild.userID, out f)) return;
                var findZadahBuild = f.listZadachi.FindAll(p => p.type == 2)?.FindAll(p => p.need == build.ShortPrefabName+"_"+(int)build.grade);
                if (findZadahBuild.Count >= 1)
                {
                    foreach (var zadachi in findZadahBuild)
                    {
                        Check(attackerBuild, zadachi, 1, f, weapon);
                    }
                    return;
                }
                else 
                {
                    findZadahBuild = f.listZadachi.FindAll(p => p.type == 2)?.FindAll(p => p.need == build.ShortPrefabName);
                    foreach (var zadachi in findZadahBuild)
                    {
                        Check(attackerBuild, zadachi, 1, f, weapon);
                    }
                }
            }
            var _weapon = info?.Weapon?.GetItem()?.info?.shortname ?? info?.WeaponPrefab?.name;
            var attacker = info.InitiatorPlayer;
            if(!attacker.userID.IsSteamId() || attacker.IsNpc) return;
            if (entity is BasePlayer && !entity.ToPlayer().IsNpc && entity.ToPlayer().userID.IsSteamId())
            {
                if (IsFriends(entity.ToPlayer().userID, attacker.userID)) return;
            }
            else
            {
                if (entity.OwnerID == attacker.userID || IsFriends(entity.OwnerID, attacker.userID)) return;
            }
            
            if (!_playerData.TryGetValue(attacker.userID, out f)) return;
            var findZadah = f.listZadachi.FindAll(p => p.type == 2)?.FindAll(p => p.need.Contains(entity.ShortPrefabName));
            if(findZadah.Count < 1) return;
            foreach (var zadachi in findZadah)
            {
                Check(attacker, zadachi, 1, f, _weapon);
            }
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        { 
            PlayerData f;
            if (!_playerData.TryGetValue(task.owner.userID, out f)) return;
            var findZadah = f.listZadachi.FindAll(p => p.type == 3)?.FindAll(p => p.need.Contains(item.info.shortname));
            if(findZadah.Count < 1) return;
            foreach (var zadachi in findZadah)
            {
                Check(task.owner, zadachi, item.amount, f);
            }
        }

        private void OnItemResearch(ResearchTable table, Item item, BasePlayer player)
        {
            PlayerData f;
            if (!_playerData.TryGetValue(player.userID, out f)) return;
            var findZadah = f.listZadachi.FindAll(p => p.type == 4)
                ?.FindAll(p => p.need.Contains(item.info.shortname));
            if(findZadah.Count < 1) return;
            foreach (var zadachi in findZadah)
            {
                Check(player, zadachi, 1, f);
            }
        }

        private List<BaseEntity> _islooted = new List<BaseEntity>();
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {   
            if (entity == null || player == null || _islooted.Contains(entity)) return;
            PlayerData f;
            if (!_playerData.TryGetValue(player.userID, out f)) return;
            var findZadah = f.listZadachi.FindAll(p => p.type == 5)
                ?.FindAll(p => p.need.Contains(entity.ShortPrefabName));
            if(findZadah.Count < 1) return;
            _islooted.Add(entity);
            foreach (var zadachi in findZadah)
            {
                Check(player, zadachi, 1, f);
            }
        } 

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null) return;
            var player = BasePlayer.FindByID(entity.OwnerID);
            if (player == null) return;
            PlayerData f;
            if (!_playerData.TryGetValue(player.userID, out f)) return;
            var findZadah = f.listZadachi.FindAll(p => p.type == 6)
                ?.FindAll(p => p.need.Contains(entity.ShortPrefabName));
            if (findZadah.Count < 1) return;
            foreach (var zadachi in findZadah)
            {
                Check(player, zadachi, 1, f);
            }
        } 

        private void OnItemRepair(BasePlayer player, Item item)
        {
            NextTick(() =>
            { 
                if(player == null || item == null) return;    
                if(item.condition != item.maxCondition) return;
                PlayerData f;
                if (!_playerData.TryGetValue(player.userID, out f)) return;
                var findZadah = f.listZadachi.FindAll(p => p.type == 7)?.FindAll(p => p.need.Contains(item.info.shortname));
                if (findZadah.Count < 1) return;
                foreach (var zadachi in findZadah)
                {
                    Check(player, zadachi, 1, f);
                }
            });
           
        }

        private object OnCollectiblePickup(CollectibleEntity entity, BasePlayer player)
        {
            if (entity == null || player == null) return null;
            var itemList = entity.itemList.ToList();
            NextTick(() =>
            {
                PlayerData f;
                if (!_playerData.TryGetValue(player.userID, out f)) return;
                foreach (ItemAmount itemAmount in itemList)
                {
                    Item item = ItemManager.Create(itemAmount.itemDef, (int) itemAmount.amount);
                    var findZadah = f.listZadachi.FindAll(p => p.type == 8)
                        ?.FindAll(p => p.need.Contains(item.info.shortname));
                    if (findZadah.Count < 1) continue;
                    foreach (var zadachi in findZadah)
                    {
                        if (player.GetActiveItem() == null)
                            Check(player, zadachi, item.amount, f);
                        else
                            Check(player, zadachi, item.amount, f, player.GetActiveItem().info.shortname);
                    }
                    item.Remove();
                }
            }); 
            return null;
        }

        private object OnGrowableGathered(GrowableEntity entity, Item item, BasePlayer player)
        {
            if (entity == null || player == null || item == null) return null;

            NextTick(() =>
            {
                PlayerData f;
                if (!_playerData.TryGetValue(player.userID, out f)) return;
                var findZadah = f.listZadachi.FindAll(p => p.type == 8)
                    ?.FindAll(p => p.need.Contains(item.info.shortname));
                if (findZadah.Count < 1) return;
                foreach (var zadachi in findZadah)
                {
                    if (player.GetActiveItem() == null)
                        Check(player, zadachi, item.amount, f);
                    else
                        Check(player, zadachi, item.amount, f, player.GetActiveItem().info.shortname);
                }
            });
            
            return null;
        }
        private Dictionary<int, string> _gradeList = new Dictionary<int, string>()
        {
            [1] = "дерево",
            [2] = "камень",
            [3] = "металл",
            [4] = "мвк",
        };

        private object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            PlayerData f;
            string res;
            if (!_playerData.TryGetValue(player.userID, out f)) return null;
            if (!_gradeList.TryGetValue((int) grade, out res)) return null;
            var findZadah = f.listZadachi.FindAll(p => p.type == 9)?.FindAll(p => p.need.Contains(entity.ShortPrefabName));
            if (findZadah.Count < 1) return null;
            foreach (var zadachi in findZadah)
            {
                if (zadachi.Weapon != res) continue;
                Check(player, zadachi, 1, f, res);
            }

            return null;
        }     

        private object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            PlayerData f;
            if (cardReader.accessLevel != card.accessLevel) return null;
            if (!_playerData.TryGetValue(player.userID, out f)) return null;
            var findZadah = f.listZadachi.FindAll(p => p.type == 10)
                ?.FindAll(p => p.need.Contains(card.GetItem().info.shortname));
            if (findZadah.Count < 1) return null;
            foreach (var zadachi in findZadah)
            {
                Check(player, zadachi, 1, f);
            }

            return null;
        }

        private object OnBuyVendingItem(VendingMachine machine, BasePlayer player, int sellOrderId, int numberOfTransactions)
        {
            PlayerData f;
            if (!_playerData.TryGetValue(player.userID, out f)) return null;
            var item = ItemManager.FindItemDefinition(machine.sellOrders.sellOrders[sellOrderId].itemToSellID);
            if (item == null) return null;
            var findZadah = f.listZadachi.FindAll(p => p.type == 11)?.FindAll(p => p.need.Contains(item.shortname));
            if (findZadah.Count < 1) return null;
            foreach (var zadachi in findZadah)
            {
                Check(player, zadachi, machine.sellOrders.sellOrders[sellOrderId].itemToSellAmount, f);
            }
            return null;
        } 
        object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            PlayerData f;
            if (!_playerData.TryGetValue(player.userID, out f)) return null;
            var item = projectile.primaryMagazine.ammoType;
            if (item == null) return null;
            var findZadah = f.listZadachi.FindAll(p => p.type == 12)?.FindAll(p => p.need.Contains(item.shortname));
            if (findZadah.Count < 1) return null;
            foreach (var zadachi in findZadah)
            {
                if(projectile.GetAvailableAmmo() >= projectile.primaryMagazine.capacity) Check(player, zadachi, projectile.primaryMagazine.capacity, f, projectile.GetItem()?.info?.shortname);
                else Check(player, zadachi, projectile.GetAvailableAmmo(), f, projectile.GetItem()?.info?.shortname);
            }
            
            return null;
        }
        void OnServerSave()
        { 
            Interface.Oxide.DataFileSystem.WriteObject("SoPass", _playerData);
            Puts("Произошло сохранение даты!");
        }
        private void OnServerInitialized()
        { 
            if(!cfg.saveOnSavew) Unsubscribe("OnServerSave");
            else Subscribe("OnServerSave");
            if(!ImageLibrary)
            {
                PrintError("Для работы нужно установить ImageLibrary!");
                Interface.Oxide.UnloadPlugin("SoPass");
                return;
            }
            foreach (var reward in from q in cfg._listQuest
                from quest in q.Value
                from reward in quest._listReward
                select reward)
            { 
                if(reward.URL.IsNullOrEmpty()) continue;
                AddImage(reward.URL, reward.ShortName);
            } 
 
            permission.RegisterPermission("sopass.admin", this);
            foreach (var classPlayer in cfg._classList)
            {
                AddImage(classPlayer.URL, classPlayer.URL);
                if (!permission.PermissionExists(classPlayer.Perm))
                    permission.RegisterPermission(classPlayer.Perm, this);
            }

            if (Interface.Oxide.DataFileSystem.ExistsDatafile("SoPass"))
                _playerData =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("SoPass");
            if(!cfg.activeOn) return;
            foreach (var basePlayer in BasePlayer.activePlayerList)
                OnPlayerConnected(basePlayer);
        }

        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SoPass", _playerData);
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(basePlayer, Layer);
                if(!cfg.activeOn) continue;
                CuiHelper.DestroyUi(basePlayer, _layerActive);
            }
        }

        #endregion
        #region Mettods

        void Check(BasePlayer player, Zadachi findZadah, int amount, PlayerData data, string weapon = "")
        {
            if (findZadah.IsFinished) return;
            if(!string.IsNullOrEmpty(findZadah.Weapon))
            {
                if (!string.IsNullOrEmpty(weapon))
                {
                    if (!findZadah.Weapon.Contains(weapon)) return;
                }
                else return;
            }
            findZadah.amount -= amount;
            if (findZadah.amount <= 0)
            {
                findZadah.IsFinished = true;
                player.SendConsoleCommand($"note.inv {ItemManager.FindItemDefinition("rifle.ak").itemid} 1 \"ЗАДАЧА\"");
                if (data.listZadachi.All(p => p.IsFinished))
                {
                    ReplySend(player, $"Все задачи выполнены заберите награду /pass");
                }
            }
            ActiveZadachi(player, data);
        }

        private bool IsFriends(ulong owner, ulong player)
        {
            if (SoFriends)
                return (bool) SoFriends.CallHook("IsFriend", player, owner);
            if (Friends)
                return (bool) Friends.CallHook("IsFriend", player, owner);
            return false;
        }

        #endregion
        #region Help

        [PluginReference] private Plugin ImageLibrary;

        public string GetImage(string shortname, ulong skin = 0) =>
            (string) ImageLibrary.Call("GetImage", shortname, skin);

        public bool AddImage(string url, string shortname, ulong skin = 0) =>
            (bool) ImageLibrary.Call("AddImage", url, shortname, skin);

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        private void ReplySend(BasePlayer player, string message) => player.SendConsoleCommand("chat.add 0",
            new object[2]
                {76561199015371818, $"<size=18><color=purple>SoPass</color></size>\n{message}"});

        [PluginReference] private Plugin SoFriends, Friends;

        #endregion
    }
}
