using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RadialMenu", "MTriper", "1.2.6")]
    [Description("A customizable quick access panel with all popular and frequently used server commands.")]
    class RadialMenu : RustPlugin
    {
        #region Fields
        [PluginReference]
        private Plugin ImageLibrary, NTeleportation, Teleportation, Friends, Clans;
        QuickMenuConfig config;
        const bool isRus = false;
        HashSet<string> activeUsers = new HashSet<string>();
        #endregion

        #region Config
        class QuickMenuConfig
        {
            [JsonProperty(isRus ? "Команда для открытия радиального меню. Если пусто, сервер будет отслеживать нажатие СКМ [повышенная нагрузка]" : "The command to open the radial menu. If empty, the server will monitor the MMB press [increased load]")]
            public string command = "";
            [JsonProperty(isRus ? "Подсказка о том, как закрыть панель [если используется команда выше]" : "Hint on how to close the panel [if using the command above]")]
            public string closeMessage = isRus ? "Введите команду еще раз или нажмите в свободном месте, чтобы закрыть панель" : "Enter the command again or click in an empty space to close the panel";
            [JsonProperty(isRus ? "Сообщение игроку в чат если у него нет привилегии" : "Message to player in chat if he does not have a privilege")]
            public string needPerm = isRus ? "У вас нет разрешения на использование этой команды!" : "You do not have permission to use this command!";
            [JsonProperty(isRus ? "Изображение с 1-им кольцом меню" : "Image with 1 menu ring")]
            public string imgLVL1 = "https://i.postimg.cc/BnGyG8Cs/1-lvl.png";
            [JsonProperty(isRus ? "Изображение с 2-мя кольцами меню" : "Image with 2 menu rings")]
            public string imgLVL2 = "https://i.postimg.cc/Nj7nGgNX/2-lvl.png";
            [JsonProperty(isRus ? "Изображение с 3-мя кольцами меню" : "Image with 3 menu rings")]
            public string imgLVL3 = "https://i.postimg.cc/VNfVd3TV/3-lvl.png";
            [JsonProperty(isRus ? "Загружать изображения игрокам при подключении?" : "Upload images to players when they connect?")]
            public bool cacheImages = true;
            [JsonProperty(isRus ? "Размер радиального меню" : "Radial menu size")]
            public int menuSize = 250;
            [JsonProperty(isRus ? "Скорость появления радиального меню" : "Speed of displaying the radial menu")]
            public float menuFadeIn = 0.5f;
            [JsonProperty(isRus ? "Размер иконки" : "Icon size")]
            public int sectionSize = 15;
            [JsonProperty(isRus ? "Размер текста" : "Text size")]
            public int textSize = 8;
            [JsonProperty(isRus ? "Скорость появления иконок" : "Speed of displaying icons")]
            public float sectionFadeIn = 0.5f;
            [JsonProperty(isRus ? "Расстояние от центра до иконок 1-го уровня" : "Distance from center to 1st level icons")]
            public float distanceLVL1 = 0.194f;
            [JsonProperty(isRus ? "Расстояние от центра до иконок 2-го уровня" : "Distance from center to 2nd level icons")]
            public float distanceLVL2 = 0.32f;
            [JsonProperty(isRus ? "Расстояние от центра до иконок 3-го уровня" : "Distance from center to 3rd level icons")]
            public float distanceLVL3 = 0.446f;
            [JsonProperty(isRus ? "Разделы" : "Sections", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MenuSectionClass> menuSections = new List<MenuSectionClass>
            {
                new MenuSectionClass
                {
                    name = "Teleport",
                    perm = "_homes",
                    img = "https://i.postimg.cc/tJ4f7dG0/homes.png",
                    separate = 15,
                    subsections = 
                    {
                        new MenuSectionClass 
                        {
                            name = "Home 1",
                            perm = "home_1_all",
                            img = "https://i.postimg.cc/V6RpYGTB/home.png",
                            separate = -15,
                            subsections = 
                            {
                                new MenuSectionClass
                                {
                                    name = "Teleport",
                                    perm = "teleport",
                                    img = "https://i.postimg.cc/FKvT7b0P/teleport.png",
                                    command = "home {0}",
                                    colorInactive = "#57de54"
                                },
                                new MenuSectionClass
                                {
                                    name = "Remove",
                                    perm = "remove",
                                    img = "https://i.postimg.cc/K8k6n5kx/del.png",
                                    command = "home remove {0}",
                                    colorInactive = "#f55757"
                                }
                            }
                        },
                        new MenuSectionClass 
                        {
                            name = "Home 2",
                            perm = "home_2_all",
                            img = "https://i.postimg.cc/V6RpYGTB/home.png",
                            separate = -15,
                            subsections = 
                            {
                                new MenuSectionClass
                                {
                                    name = "Teleport",
                                    perm = "teleport",
                                    img = "https://i.postimg.cc/FKvT7b0P/teleport.png",
                                    command = "home {0}",
                                    colorInactive = "0.34 0.87 0.33 1"
                                },
                                new MenuSectionClass
                                {
                                    name = "Remove",
                                    perm = "remove",
                                    img = "https://i.postimg.cc/K8k6n5kx/del.png",
                                    command = "home remove {0}",
                                    colorInactive = "0.96 0.34 0.34 1"
                                }
                            }
                        },
                        new MenuSectionClass 
                        {
                            name = "Home 3",
                            perm = "home_3_all",
                            img = "https://i.postimg.cc/V6RpYGTB/home.png",
                            separate = -15,
                            subsections = 
                            {
                                new MenuSectionClass
                                {
                                    name = "Teleport",
                                    perm = "teleport",
                                    img = "https://i.postimg.cc/FKvT7b0P/teleport.png",
                                    command = "home {0}",
                                    colorInactive = "0.34 0.87 0.33 1"
                                },
                                new MenuSectionClass
                                {
                                    name = "Remove",
                                    perm = "remove",
                                    img = "https://i.postimg.cc/K8k6n5kx/del.png",
                                    command = "home remove {0}",
                                    colorInactive = "0.96 0.34 0.34 1"
                                }
                            }
                        },
                        new MenuSectionClass 
                        {
                            name = "Add",
                            perm = "home_add",
                            img = "https://i.postimg.cc/8PKGd5ZB/add.png",
                            command = "home add {0}"
                        }
                    }
                },
                new MenuSectionClass
                {
                    name = "Friends",
                    perm = "_teammates",
                    img = "https://i.postimg.cc/rmc72Xhw/friends.png",
                    separate = 15,
                    subsections = 
                    {
                        new MenuSectionClass 
                        {
                            name = "Friend 1",
                            perm = "friend_1_all",
                            img = "https://i.postimg.cc/q72WFMxF/friend.png",
                            separate = 20,
                            subsections = 
                            {
                                new MenuSectionClass
                                {
                                    name = "Teleport",
                                    perm = "teleport",
                                    img = "https://i.postimg.cc/FKvT7b0P/teleport.png",
                                    command = "tpr {0}"
                                },
                                new MenuSectionClass
                                {
                                    name = "Trade",
                                    perm = "trade",
                                    img = "https://i.postimg.cc/BvCMD8gJ/trade.png",
                                    command = "trade {0}"
                                }
                            }
                        },
                        new MenuSectionClass 
                        {
                            name = "Friend 2",
                            perm = "friend_2_all",
                            img = "https://i.postimg.cc/q72WFMxF/friend.png",
                            separate = 20,
                            subsections = 
                            {
                                new MenuSectionClass
                                {
                                    name = "Teleport",
                                    perm = "teleport",
                                    img = "https://i.postimg.cc/FKvT7b0P/teleport.png",
                                    command = "tpr {0}"
                                },
                                new MenuSectionClass
                                {
                                    name = "Trade",
                                    perm = "trade",
                                    img = "https://i.postimg.cc/BvCMD8gJ/trade.png",
                                    command = "trade {0}"
                                }
                            }
                        },
                        new MenuSectionClass 
                        {
                            name = "Friend 3",
                            perm = "friend_3_all",
                            img = "https://i.postimg.cc/q72WFMxF/friend.png",
                            separate = 20,
                            subsections = 
                            {
                                new MenuSectionClass
                                {
                                    name = "Teleport",
                                    perm = "teleport",
                                    img = "https://i.postimg.cc/FKvT7b0P/teleport.png",
                                    command = "tpr {0}"
                                },
                                new MenuSectionClass
                                {
                                    name = "Trade",
                                    perm = "trade",
                                    img = "https://i.postimg.cc/BvCMD8gJ/trade.png",
                                    command = "trade {0}"
                                }
                            }
                        },
                    }
                },
                new MenuSectionClass
                {
                    name = "Remove",
                    perm = "remove",
                    img = "https://i.postimg.cc/XqP2JTgF/remove.png",
                    command = "remove",
                    neededperm = "removertool.normal, removertool.structure, removertool.external",
                    showAll = true
                },
                new MenuSectionClass
                {
                    name = "Recycler",
                    perm = "recycler",
                    img = "https://i.postimg.cc/sxx63V5s/recycler.png",
                    command = "rec"
                },
                new MenuSectionClass
                {
                    name = "Skins",
                    perm = "skins",
                    img = "https://i.postimg.cc/026W3y8C/skins.png",
                    command = "skins",
                    neededperm = "skins.use",
                    onlyPerms = true
                },
                new MenuSectionClass
                {
                    name = "Craft",
                    perm = "craft",
                    img = "https://i.postimg.cc/FsR5Kvhf/craft.png",
                    command = "craft",
                    neededperm = "craft.use",
                    showAll = true
                },
                new MenuSectionClass
                {
                    name = "Turrets",
                    perm = "turrets",
                    img = "https://i.postimg.cc/prmZgstd/turrets.png",
                    separate = -15,
                    subsections = 
                    {
                        new MenuSectionClass
                        {
                            name = "Turret on/off",
                            perm = "one",
                            img = "https://i.postimg.cc/j5dvhsgP/turret-1.png",
                            command = "turret"
                        },
                        new MenuSectionClass
                        {
                            name = "Turrets on",
                            perm = "all_on",
                            img = "https://i.postimg.cc/mDXjX0Jq/turret-all.png",
                            command = "turret.tc on",
                            colorInactive = "0.34 0.87 0.33 1"
                        },
                        new MenuSectionClass
                        {
                            name = "Turrets off",
                            perm = "all_off",
                            img = "https://i.postimg.cc/mDXjX0Jq/turret-all.png",
                            command = "turret.tc off",
                            colorInactive = "0.96 0.34 0.34 1"
                        },
                        new MenuSectionClass
                        {
                            name = "SAMs on",
                            perm = "sams_on",
                            img = "https://i.postimg.cc/nVSS0Dtz/sams.png",
                            command = "sam.tc on",
                            colorInactive = "0.34 0.87 0.33 1"
                        },
                        new MenuSectionClass
                        {
                            name = "SAMs off",
                            perm = "sams_off",
                            img = "https://i.postimg.cc/nVSS0Dtz/sams.png",
                            command = "sam.tc off",
                            colorInactive = "0.96 0.34 0.34 1"
                        }
                    }
                },
                new MenuSectionClass
                {
                    name = "Notify",
                    perm = "notify_all",
                    img = "https://i.postimg.cc/TYSSYWkS/notify.png",
                    separate = -15,
                    subsections = 
                    {
                        new MenuSectionClass
                        {
                            name = "Teleport",
                            perm = "tp",
                            img = "https://i.postimg.cc/FKvT7b0P/teleport.png",
                            separate = -15,
                            subsections = 
                            {
                                new MenuSectionClass
                                {
                                    name = "Accept",
                                    perm = "accept",
                                    img = "https://i.postimg.cc/J44mBJ5z/accept.png",
                                    command = "tpa",
                                    colorInactive = "0.34 0.87 0.33 1"
                                },
                                new MenuSectionClass
                                {
                                    name = "Cancel",
                                    perm = "cancel",
                                    img = "https://i.postimg.cc/85SgkrCD/cancel.png",
                                    command = "tpc",
                                    colorInactive = "0.96 0.34 0.34 1"
                                }
                            }
                        },
                        new MenuSectionClass
                        {
                            name = "Trade",
                            perm = "trade",
                            img = "https://i.postimg.cc/BvCMD8gJ/trade.png",
                            separate = -15,
                            subsections = 
                            {
                                new MenuSectionClass
                                {
                                    name = "Accept",
                                    perm = "accept",
                                    img = "https://i.postimg.cc/J44mBJ5z/accept.png",
                                    command = "trade accept",
                                    colorInactive = "0.34 0.87 0.33 1"
                                },
                                new MenuSectionClass
                                {
                                    name = "Cancel",
                                    perm = "cancel",
                                    img = "https://i.postimg.cc/85SgkrCD/cancel.png",
                                    command = "trade cancel",
                                    colorInactive = "0.96 0.34 0.34 1"
                                }
                            }
                        }
                    }
                }
            };
        }

        class MenuSectionClass
        {
            [JsonProperty(isRus ? "Название раздела" : "Section name")]
            public string name = "";
            [JsonProperty(isRus ? "Включить раздел?" : "Enable this section?")]
            public bool isEnable = true;
            [JsonProperty(isRus ? "Permission раздела [обязательно]" : "Section permission [required]")]
            public string perm = "";
            [JsonProperty(isRus ? "Иконка раздела" : "Section icon")]
            public string img = "";
            [JsonProperty(isRus ? "Залить иконку цветом ниже?" : "Fill icon with color below?")]
            public bool isFill = true;
            [JsonProperty(isRus ? "Цвет, когда раздел неактивен [HEX или Unity RGBA]" : "Inactive section color [HEX or Unity RGBA]")]
            public string colorInactive = "#fade54";
            [JsonProperty(isRus ? "Цвет, когда раздел активен [HEX или Unity RGBA]" : "Active section color [HEX or Unity RGBA]")]
            public string colorActive = "#e3e3e3";
            [JsonProperty(isRus ? "Команда от имени игрока [если пусто, то используются подразделы ниже]" : "Command executed on behalf of the player [if empty, the subsections below are used]")]
            public string command = "";
            [JsonProperty(isRus ? "Тип выполняемой команды [true - чат, false - консоль]" : "Command type [true - chat, false - console]")]
            public bool isChat = true;
            [JsonProperty(isRus ? "Permissions для команды выше [через запятую. необязательно]. Проверяет есть ли у игрока такие permissions." : "Permissions for above command [comma separated. optional]. Checks if the player has such a permissions.")]
            public string neededperm = "";
            [JsonProperty(isRus ? "Показывать этот раздел только тем, у кого есть есть привилегии выше?" : "Show this section only to those with permissions above?")]
            public bool onlyPerms = false;
            [JsonProperty(isRus ? "Показывать этот раздел даже тем, у кого нет привилегий?" : "Show this section even to those who don't have permissions?")]
            public bool showAll = false;
            [JsonProperty(isRus ? "Расположение подразделов [n - число]: 0 - авто; n - с начала; -n - от активного элемента." : "Subsections layout [n - number]: 0 - auto; n - from the beginning; -n - from an active element")]
            public int separate = 0;
            [JsonProperty(isRus ? "Подразделы" : "Subsections", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MenuSectionClass> subsections = new List<MenuSectionClass>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new QuickMenuConfig();
            SaveConfig();
        }

        void ReadConfig()
        {
            config = Config.ReadObject<QuickMenuConfig>();
            SaveConfig(); 
        }
        protected override void SaveConfig() => Config.WriteObject(config, true);

        IEnumerator CheckConfig()
        {
            if (config.command != "") 
            {
                Unsubscribe("OnPlayerInput");
                Unsubscribe("OnItemSplit");
                cmd.AddChatCommand(config.command, this, "Command");
            }

            if (string.IsNullOrEmpty(config.imgLVL1) || string.IsNullOrEmpty(config.imgLVL2) || string.IsNullOrEmpty(config.imgLVL3))
            {
                PrintError("One of the radial menu images is not set!");
                NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                yield break;
            }

            for (int i = config.menuSections.Count - 1; i > -1; i--)
            {
                if (!config.menuSections[i].isEnable)
                {
                    config.menuSections.RemoveAt(i);
                    continue;
                }

                yield return CoroutineEx.waitForFixedUpdate;

                if (config.menuSections[i].perm == "")
                {
                    PrintError($"One of the sections ({config.menuSections[i].name}) has no permission set.");
                    NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                    yield break;
                }

                if (config.menuSections[i].perm == "_homes")
                {
                    if ((NTeleportation == null || !NTeleportation.IsLoaded) & (Teleportation == null || !Teleportation.IsLoaded)) PrintError("Supported teleport plugin not found!");
                }

                if (config.menuSections[i].perm == "_friends")
                {
                    if (Friends == null || !Friends.IsLoaded || Friends.Author != "Wulf") PrintError("Supported plugin 'Friends' not found!");  
                }

                if (config.menuSections[i].perm == "_clans")
                {
                    if (Clans == null || !Clans.IsLoaded || Clans.Author != "k1lly0u") PrintError("Supported plugin 'Clans' not found!");
                }

                if (config.menuSections.Where(x => x.perm == config.menuSections[i].perm).Count() > 1) 
                {  
                    PrintError($"Several sections have the same permission - '{config.menuSections[i].perm}'.");
                    NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                    yield break;
                }

                config.menuSections[i].colorInactive = ToUnityRGBA(config.menuSections[i].colorInactive);
                config.menuSections[i].colorActive = ToUnityRGBA(config.menuSections[i].colorActive); 
                permission.RegisterPermission($"radialmenu.{config.menuSections[i].perm}", this);
                ImageLibrary.Call("AddImage", config.menuSections[i].img, $"RadialMenu.{config.menuSections[i].perm}");

                if (config.menuSections[i].command == "" && config.menuSections[i].subsections.IsNullOrEmpty())
                {
                    PrintWarning($"The '{config.menuSections[i].name}' section is missing a command and subsections.");
                }

                for (int j = config.menuSections[i].subsections.Count - 1; j > -1; j--)
                {
                    if (!config.menuSections[i].subsections[j].isEnable)
                    {
                        config.menuSections[i].subsections.RemoveAt(j);
                        continue; 
                    }

                    if (config.menuSections[i].subsections[j].perm == "")
                    {
                        PrintError($"One of the subsections ({config.menuSections[i].subsections[j].name}) has no permission set.");
                        NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                        yield break;
                    }

                    if (config.menuSections[i].subsections.Where(x => x.perm == config.menuSections[i].subsections[j].perm).Count() > 1) 
                    {  
                        PrintError($"Several subsections have the same permission - '{config.menuSections[i].subsections[j].perm}'.");
                        NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                        yield break;
                    }

                    config.menuSections[i].subsections[j].colorInactive = ToUnityRGBA(config.menuSections[i].subsections[j].colorInactive);
                    config.menuSections[i].subsections[j].colorActive = ToUnityRGBA(config.menuSections[i].subsections[j].colorActive); 
                    if (!config.menuSections[i].perm.Contains("_all")) permission.RegisterPermission($"radialmenu.{config.menuSections[i].perm}.{config.menuSections[i].subsections[j].perm}", this);
                    ImageLibrary.Call("AddImage", config.menuSections[i].subsections[j].img, $"RadialMenu.{config.menuSections[i].perm}.{config.menuSections[i].subsections[j].perm}");            

                    if (config.menuSections[i].subsections[j].command == "" && config.menuSections[i].subsections[j].subsections.IsNullOrEmpty())
                    {
                        PrintWarning($"The '{config.menuSections[i].subsections[j].name}' section is missing a command and subsections.");
                    }

                    for (int k = config.menuSections[i].subsections[j].subsections.Count - 1; k > -1; k--)
                    {
                        if (!config.menuSections[i].subsections[j].subsections[k].isEnable)
                        {
                            config.menuSections[i].subsections[j].subsections.RemoveAt(k);
                            continue;
                        }

                        if (!config.menuSections[i].subsections[j].subsections[k].subsections.IsNullOrEmpty())
                        {
                            PrintError($"Only 2 levels of attachments are allowed - {config.menuSections[i].perm} => {config.menuSections[i].subsections[j].perm} => {config.menuSections[i].subsections[j].subsections[k].perm} ---> {config.menuSections[i].subsections[j].subsections[k].subsections[0].perm}");
                            NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                            yield break;
                        }

                        if (config.menuSections[i].subsections[j].subsections[k].perm == "")
                        {
                            PrintError($"One of the subsections ({config.menuSections[i].subsections[j].subsections[k].name}) has no permission set.");
                            NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                            yield break;
                        }

                        if (config.menuSections[i].subsections[j].subsections.Where(x => x.perm == config.menuSections[i].subsections[j].subsections[k].perm).Count() > 1) 
                        {  
                            PrintError($"Several subsections have the same permission - '{config.menuSections[i].subsections[j].subsections[k].perm}'.");
                            NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                            yield break;
                        }

                        config.menuSections[i].subsections[j].subsections[k].colorInactive = ToUnityRGBA(config.menuSections[i].subsections[j].subsections[k].colorInactive);
                        config.menuSections[i].subsections[j].subsections[k].colorActive = ToUnityRGBA(config.menuSections[i].subsections[j].subsections[k].colorActive); 
                        if (!config.menuSections[i].perm.Contains("_all") && !config.menuSections[i].subsections[j].perm.Contains("_all")) permission.RegisterPermission($"radialmenu.{config.menuSections[i].perm}.{config.menuSections[i].subsections[j].perm}.{config.menuSections[i].subsections[j].subsections[k].perm}", this);
                        ImageLibrary.Call("AddImage", config.menuSections[i].subsections[j].subsections[k].img, $"RadialMenu.{config.menuSections[i].perm}.{config.menuSections[i].subsections[j].perm}.{config.menuSections[i].subsections[j].subsections[k].perm}");

                        if (config.menuSections[i].subsections[j].subsections[k].command == "")
                        {
                            PrintWarning($"The '{config.menuSections[i].subsections[j].subsections[k].name}' section is missing a command.");
                        }
                    }
                }
            }
        }
        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("The \"ImageLibrary\" plugin is missing!");
                NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                return;
            }

            ReadConfig();
            ServerMgr.Instance.StartCoroutine(CheckConfig());

            ImageLibrary.Call("AddImage", config.imgLVL1, "RadialMenu.LVL1");
            ImageLibrary.Call("AddImage", config.imgLVL2, "RadialMenu.LVL2");
            ImageLibrary.Call("AddImage", config.imgLVL3, "RadialMenu.LVL3");

            if (!config.cacheImages) Unsubscribe("OnPlayerConnected");

            foreach (var item in config.menuSections) ImageLibrary.Call("AddImage", item.img, $"RadialMenu.{item.perm}");  
        }

        void Unload() 
        {
            ServerMgr.Instance.StopCoroutine(CheckConfig());
            foreach (BasePlayer player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, "RadialMenu.MainPanel");
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            ImageLibrary.Call("SendImage", player, "RadialMenu.LVL1");
            ImageLibrary.Call("SendImage", player, "RadialMenu.LVL2");
            ImageLibrary.Call("SendImage", player, "RadialMenu.LVL3");
        }

        void OnPlayerDisconnected(BasePlayer player) => activeUsers.Remove(player.UserIDString);

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || !input.WasJustReleased(BUTTON.FIRE_THIRD) || !player.CanInteract() || player.inventory.loot.IsLooting()) return;            

            if (activeUsers.Contains(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, "RadialMenu.MainPanel");
                activeUsers.Remove(player.UserIDString);
                return;
            }
            else activeUsers.Add(player.UserIDString);
            OpenPanel(player);
        }

        /*object OnInventoryNetworkUpdate(PlayerInventory inventory, ItemContainer container, ProtoBuf.UpdateItemContainer updateItemContainer, PlayerInventory.Type type, bool broadcast)
        {
            if (container?.playerOwner != null && type != PlayerInventory.Type.Wear)
            {
                try
                {
                    CuiHelper.DestroyUi(container.playerOwner, "RadialMenu.MainPanel");
                    activeUsers.Remove(container.playerOwner.UserIDString);
                }
                catch{}                
            }
            
            return null;
        }*/

        Item OnItemSplit(Item item, int amount)
        {
            BasePlayer player = item.GetOwnerPlayer();
            if (player != null)
            {
                CuiHelper.DestroyUi(player, "RadialMenu.MainPanel");
                activeUsers.Remove(player.UserIDString);
            }            
            return null;
        }
        #endregion

        #region Commands
        [ConsoleCommand("distributionCommand")]
        void DistributionCommand(ConsoleSystem.Arg args)
        {
            BasePlayer player = args?.Player();
            if (player == null) return;

            if (args.Args[0] == "close")
            {
                CuiHelper.DestroyUi(player, "RadialMenu.MainPanel");
                activeUsers.Remove(player.UserIDString);
                return;
            }

            string[] sections = args.Args[0].Split('.');
            int index = Array.FindIndex(sections, x => x.Contains("_all"));
            MenuSectionClass neededSection = config.menuSections.Find(x => x.perm == sections[0]);

            switch (index)
            {
                case 0:
                    if (!permission.UserHasPermission(player.UserIDString, $"RadialMenu.{sections[0]}"))
                    {
                        SendReply(player, config.needPerm);
                        OpenPanel(player, sections.Take(sections.Length - 1).ToArray());
                        return;
                    }
                    break;
                case 1:
                    if (!permission.UserHasPermission(player.UserIDString, $"RadialMenu.{sections[0]}.{sections[1]}"))
                    {
                        SendReply(player, config.needPerm);
                        OpenPanel(player, sections.Take(sections.Length - 1).ToArray());
                        return;
                    }
                    break;
                default:
                    if (!permission.UserHasPermission(player.UserIDString, $"RadialMenu.{args.Args[0]}"))
                    {
                        SendReply(player, config.needPerm);
                        OpenPanel(player, sections.Take(sections.Length - 1).ToArray());
                        return;
                    }
                    break;
            }

            for (int i = 1; i < sections.Length; i++) neededSection = neededSection.subsections.Find(x => x.perm == sections[i]);

            if (!UserHasNeededPerm(player.UserIDString, neededSection.neededperm))
            {
                SendReply(player, config.needPerm);
                OpenPanel(player, sections.Take(sections.Length - 1).ToArray());
                return;
            }

            if (args.Args.ElementAtOrDefault(1) == "chat") // Пример: _friends.friend_1.teleport chat tpr *name*
            {
                player.Command($"chat.say \"/{string.Join(" ", args.Args.Skip(2)).Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace("%username%", player.displayName, StringComparison.OrdinalIgnoreCase)}\"");
                activeUsers.Remove(player.UserIDString);
                CuiHelper.DestroyUi(player, "RadialMenu.MainPanel");               
                return;
            }
            else if (args.Args.ElementAtOrDefault(1) == "console")
            {
                switch (args.Args[2])
                {
                    case "spawn":
                        if (args.Args.Length == 3) { player.ConsoleMessage("No entity name provided"); break; }
                        RaycastHit hitInfo;
                        if (Physics.Raycast(player.eyes.BodyRay().origin, player.eyes.HeadRay().direction, out hitInfo, 100, LayerMask.GetMask("Terrain"), QueryTriggerInteraction.Ignore))
                            ConVar.Entity.svspawn(args.Args[3], hitInfo.point, -player.eyes.MovementForward());
                        break;
                    case "spawnat":
                        if (args.Args.Length <= 4) { player.ConsoleMessage("Not enough arguments"); break; }
                        Vector3 position = args.Args[4]?.ToVector3() ?? Vector3.zero;
                        Vector3 rotation = args.Args.ElementAtOrDefault(5)?.ToVector3() ?? player.eyes.MovementForward();
                        ConVar.Entity.svspawn(args.Args[3], position, rotation);
                        break;
                    case "spawnhere":
                        if (args.Args.Length == 3) { player.ConsoleMessage("No entity name provided"); break; }
                        Vector3 position2 = player.eyes.HeadRay().GetPoint(float.Parse(args.Args.ElementAtOrDefault(4) ?? "0"));
                        Vector3 rotation2 = args.Args.ElementAtOrDefault(5)?.ToVector3() ?? player.eyes.HeadRay().direction;
                        ConVar.Entity.svspawn(args.Args[3], position2, rotation2); 
                        break;
                    case "spawnitem":
                        if (args.Args.Length == 3) { player.ConsoleMessage("No entity name provided"); break; }
                        RaycastHit hitInfo2;
                        if (Physics.Raycast(player.eyes.BodyRay().origin, player.eyes.HeadRay().direction, out hitInfo2, 100, LayerMask.GetMask("Terrain"), QueryTriggerInteraction.Ignore))
                            ConVar.Entity.svspawnitem(args.Args[3], hitInfo2.point);
                        break;
                    default:
                        player.Command(args.Args[2], args.Args.Skip(3).Select(x => x.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace("%username%", player.displayName, StringComparison.OrdinalIgnoreCase)));
                        break;
                }

                activeUsers.Remove(player.UserIDString);
                CuiHelper.DestroyUi(player, "RadialMenu.MainPanel");               
                return;
            }
            
            if (!neededSection.subsections.IsNullOrEmpty()) OpenPanel(player, sections);
        }

        void Command(BasePlayer player)
        {
            if (player != null && player.CanInteract()) 
            {
                if (activeUsers.Contains(player.UserIDString))
                {
                    CuiHelper.DestroyUi(player, "RadialMenu.MainPanel");
                    activeUsers.Remove(player.UserIDString);
                    return;
                }
                else activeUsers.Add(player.UserIDString);

                OpenPanel(player);
            }
        }
        #endregion

        #region Functions
        HashSet<MenuSectionClass> GetValidSections(BasePlayer player, string[] lvls)
        {
            HashSet<MenuSectionClass> sections = new HashSet<MenuSectionClass>();

            foreach (var lvl1 in config.menuSections)
            {
                MenuSectionClass section1 = new MenuSectionClass();
                
                if (!lvl1.showAll) 
                {
                    if (permission.UserHasPermission(player.UserIDString, $"RadialMenu.{lvl1.perm}"))
                    {
                        if (lvl1.onlyPerms && !UserHasNeededPerm(player.UserIDString, lvl1.neededperm)) continue;
                
                        section1.neededperm = lvl1.neededperm;
                        section1.name = lvl1.name;
                        section1.perm = lvl1.perm;
                        section1.command = lvl1.command;
                        section1.isChat = lvl1.isChat;
                        section1.isFill = lvl1.isFill;
                        section1.separate = lvl1.separate;
                        section1.colorActive = lvl1.colorActive;
                        section1.colorInactive = lvl1.colorInactive;
                    }
                    else continue;
                } 
                else
                {
                    section1.neededperm = lvl1.neededperm;
                    section1.name = lvl1.name;
                    section1.perm = lvl1.perm;
                    section1.command = lvl1.command;
                    section1.isChat = lvl1.isChat;
                    section1.isFill = lvl1.isFill;
                    section1.separate = lvl1.separate;
                    section1.colorActive = lvl1.colorActive;
                    section1.colorInactive = lvl1.colorInactive;
                }

                if (lvls.IsNullOrEmpty() || lvl1.perm != lvls[0]) 
                {
                    sections.Add(section1);
                    continue;
                }

                foreach (var lvl2 in lvl1.subsections)
                {
                    MenuSectionClass section2 = new MenuSectionClass();

                    if (!lvl2.showAll)
                    {                 
                        if (lvl1.perm.Contains("_all") || permission.UserHasPermission(player.UserIDString, $"RadialMenu.{lvl1.perm}.{lvl2.perm}"))
                        {  
                            if (lvl2.onlyPerms && !UserHasNeededPerm(player.UserIDString, lvl2.neededperm)) continue;
                            
                            section2.neededperm = lvl2.neededperm;
                            section2.name = lvl2.name;
                            section2.perm = lvl2.perm;
                            section2.command = lvl2.command;
                            section2.isChat = lvl2.isChat;
                            section2.isFill = lvl2.isFill;
                            section2.separate = lvl2.separate;
                            section2.colorActive = lvl2.colorActive;
                            section2.colorInactive = lvl2.colorInactive;
                        }
                        else continue;
                    }
                    else 
                    {
                        section2.neededperm = lvl2.neededperm;
                        section2.name = lvl2.name;
                        section2.perm = lvl2.perm;
                        section2.command = lvl2.command;
                        section2.isChat = lvl2.isChat;
                        section2.isFill = lvl2.isFill;
                        section2.separate = lvl2.separate;
                        section2.colorActive = lvl2.colorActive;
                        section2.colorInactive = lvl2.colorInactive;
                    }

                    if (lvls.IsNullOrEmpty() || lvls.Length != 2 || lvl2.perm != lvls[1]) 
                    {
                        section1.subsections.Add(section2);
                        continue;
                    }

                    foreach (var lvl3 in lvl2.subsections)
                    {
                        MenuSectionClass section3 = new MenuSectionClass();

                        if (!lvl3.showAll)
                        {
                            if (lvl1.perm.Contains("_all") || lvl2.perm.Contains("_all") || permission.UserHasPermission(player.UserIDString, $"RadialMenu.{lvl1.perm}.{lvl2.perm}.{lvl3.perm}"))
                            {
                                if (lvl3.onlyPerms && !UserHasNeededPerm(player.UserIDString, lvl3.neededperm)) continue;
                                
                                section3.neededperm = lvl3.neededperm;
                                section3.name = lvl3.name;
                                section3.perm = lvl3.perm;
                                section3.command = lvl3.command;
                                section3.isChat = lvl3.isChat;
                                section3.isFill = lvl3.isFill;
                                section3.colorActive = lvl3.colorActive;
                                section3.colorInactive = lvl3.colorInactive;
                            }
                            else continue;
                        }
                        else 
                        {
                            section3.neededperm = lvl3.neededperm;
                            section3.name = lvl3.name;
                            section3.perm = lvl3.perm;
                            section3.command = lvl3.command;
                            section3.isChat = lvl3.isChat;
                            section3.isFill = lvl3.isFill;
                            section3.colorActive = lvl3.colorActive;
                            section3.colorInactive = lvl3.colorInactive;
                        }
                        section2.subsections.Add(section3);
                    }
                    if (!section1.subsections.Contains(section2)) section1.subsections.Add(section2);
                }                
                sections.Add(section1);
            }
            return sections;
        }
        
        void AdditionalFunctionality(BasePlayer player, ref HashSet<MenuSectionClass> validSections, string[] lvls)
        {
            if (lvls.IsNullOrEmpty()) return;

            switch (lvls[0])
            {
                case "_homes":
                case "_homes_all":
                    MenuSectionClass homes = validSections.First(x => x.perm.StartsWith("_homes"));
                    Dictionary<string, Vector3> teleportHomes = new Dictionary<string, Vector3>();

                    if (NTeleportation != null && NTeleportation.IsLoaded) teleportHomes = NTeleportation?.Call("API_GetHomes", player) as Dictionary<string, Vector3>;
                    else if (Teleportation != null && Teleportation.IsLoaded && Teleportation.Author == "OxideBro") teleportHomes = Teleportation?.Call("GetHomes", player.userID) as Dictionary<string, Vector3>;
                    else 
                    {
                        PrintWarning("Supported teleport plugin not found!");
                        homes.subsections.Clear();
                        return;
                    }

                    HashSet<string> delHomes = new HashSet<string>();
                    int totalHomesCount = homes.subsections.Where(x => x.perm.Contains("home_")).Count();
                    int passedHomes = 0;

                    for (int i = 0; i < homes.subsections.Count; i++)
                    {
                        if (!homes.subsections[i].perm.Contains("home_")) continue;

                        if (homes.subsections[i].perm == "home_add") 
                        {
                            string grid = PhoneController.PositionToGridCoord(player.transform.position);
                            int c = 1 + teleportHomes.Keys.Count(x => x.Contains(grid));
                            homes.subsections[i].command = string.Format(homes.subsections[i].command, string.Format("{0}h{1}", grid, c));
                            continue;
                        }

                        if (!teleportHomes.IsNullOrEmpty() && passedHomes < totalHomesCount && passedHomes < teleportHomes.Count)
                        {
                            homes.subsections[i].name = teleportHomes.ElementAt(passedHomes).Key;

                            foreach (var item in homes.subsections[i].subsections)
                            {
                                if (item.command.Contains("{0}")) item.command = string.Format(item.command, homes.subsections[i].name);
                            }

                            passedHomes++;
                        }
                        else delHomes.Add(homes.subsections[i].perm);
                    }

                    foreach (var id in delHomes) homes.subsections.RemoveAll(x => x.perm == id);
                    break;
                case "_clans":
                case "_clans_all":
                case "_friends":
                case "_friends_all":
                case "_teammates":
                case "_teammates_all":
                    MenuSectionClass friends = new MenuSectionClass();
                    List<ulong> friendsList = new List<ulong>();

                    if (lvls[0].StartsWith("_friends"))
                    {
                        friends = validSections.First(x => x.perm.StartsWith("_friends"));

                        if (Friends == null || !Friends.IsLoaded || Friends.Author != "Wulf") 
                        {
                            PrintWarning("Supported plugin 'Friends' not found!");
                            friends.subsections.Clear();
                            return;
                        }
                        
                        friendsList = Friends?.Call<ulong[]>("GetFriends", player.userID).ToList();
                    }
                    else if (lvls[0].StartsWith("_teammates"))
                    {
                        friends = validSections.First(x => x.perm.StartsWith("_teammates"));
                        friendsList = player.Team?.members.ToList();
                    }
                    else if (lvls[0].StartsWith("_clans"))
                    {
                        friends = validSections.First(x => x.perm.StartsWith("_clans"));

                        if (Clans == null || !Clans.IsLoaded || Clans.Author != "k1lly0u") 
                        {
                            PrintWarning("Supported plugin 'Clans' not found!");
                            friends.subsections.Clear();
                            return;
                        }

                        friendsList = Clans?.Call<List<string>>("GetClanMembers", player.UserIDString).Select(ulong.Parse).ToList();
                    }

                    HashSet<string> delFriends = new HashSet<string>();
                    int totalFriendsCount = friends.subsections.Where(x => x.perm.Contains("friend_")).Count();
                    int passedFriends = 0;

                    friendsList?.Remove(player.userID);

                    for (int i = 0; i < friends.subsections.Count; i++)
                    {
                        if (!friends.subsections[i].perm.Contains("friend_")) continue;

                        if (!friendsList.IsNullOrEmpty() && passedFriends < totalFriendsCount && passedFriends < friendsList.Count)
                        {
                            friends.subsections[i].name = BasePlayer.FindAwakeOrSleeping(friendsList.ElementAt(passedFriends).ToString()).displayName;
                            
                            foreach (var item in friends.subsections[i].subsections)
                            {
                                if (item.command.Contains("{0}")) item.command = string.Format(item.command, friends.subsections[i].name);
                            }
                            passedFriends++;
                        }
                        else delFriends.Add(friends.subsections[i].perm);
                    }

                    foreach (var id in delFriends) friends.subsections.RemoveAll(x => x.perm == id);
                    break;
            }
        }
       
        bool UserHasNeededPerm(string playerId, string str)
        {
            string[] perms = str.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!perms.IsNullOrEmpty())
            {
                foreach (var item in perms)
                {
                    if (!permission.UserHasPermission(playerId, item)) return false;
                }
            }
            return true;
        }
       
        string ToUnityRGBA(string hexColor)
        {
            Color color;
            if(ColorUtility.TryParseHtmlString(hexColor, out color)) return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
            else return hexColor;
        }
        #endregion

        #region Interface
        void OpenPanel(BasePlayer player, params string[] lvls)
        {
            HashSet<MenuSectionClass> validSections = GetValidSections(player, lvls);
            if (validSections.IsNullOrEmpty()) return;
            AdditionalFunctionality(player, ref validSections, lvls);
            var container = new CuiElementContainer();
            string png, color1, color2;
            float menuFadeIn = 0f, section1FadeIn = 0f, section2FadeIn = 0f, section3FadeIn = 0f;
            Dictionary<string, float> section1Radians = new Dictionary<string, float>();

            if (lvls.IsNullOrEmpty()) 
            {
                png = (string) ImageLibrary.Call("GetImage", "RadialMenu.LVL1"); 
                menuFadeIn = config.menuFadeIn;
                section1FadeIn = config.sectionFadeIn; 
            }
            else if (lvls.Length == 1)
            {
                if (validSections.First(x => x.perm == lvls[0]).subsections.IsNullOrEmpty()) png = (string) ImageLibrary.Call("GetImage", "RadialMenu.LVL1");
                else
                {
                    png = (string) ImageLibrary.Call("GetImage", "RadialMenu.LVL2");
                    section2FadeIn = config.sectionFadeIn;
                }
            }
            else 
            {
                png = (string) ImageLibrary.Call("GetImage", "RadialMenu.LVL3");
                section3FadeIn = config.sectionFadeIn;
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{config.menuSize} -{config.menuSize}", OffsetMax = $"{config.menuSize} {config.menuSize}" },
                Image = { Color = "0 0 0 0" },
                CursorEnabled = true
            }, "Hud", "RadialMenu.MainPanel", "RadialMenu.MainPanel");

            if (config.command != "")
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "-1 -1", AnchorMax = "2 2" },
                    Button = { Color = "0 0 0 0", Command = "distributionCommand close" },
                    Text = { Text = "" }
                }, "RadialMenu.MainPanel");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.46", AnchorMax = "0.5 0.46", OffsetMin = $"-{config.menuSize} -{config.menuSize}", OffsetMax = $"{config.menuSize} {config.menuSize}" },
                    Text = { Text = config.closeMessage, Color = "1 1 1 0.8", FontSize = 12, Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf" }
                }, "RadialMenu.MainPanel");
            }

            container.Add(new CuiElement
            {
                Name = "RadialMenu.RadialMenu",
                Parent = "RadialMenu.MainPanel",
                Components = 
                {
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiRawImageComponent { Png = png, FadeIn = menuFadeIn }
                }
            });

            for (int i = 0; i < validSections.Count; i++)
            {
                float rad = 1.57f - i * 6.28f / validSections.Count;
                float x = 0.5f + config.distanceLVL1 * Mathf.Cos(rad);
                float y = 0.5f + config.distanceLVL1 * Mathf.Sin(rad);

                section1Radians.Add(validSections.ElementAt(i).perm, rad);

                if (!lvls.IsNullOrEmpty() && lvls[0] == validSections.ElementAt(i).perm && !validSections.ElementAt(i).subsections.IsNullOrEmpty()) color1 = validSections.ElementAt(i).colorActive;
                else color1 = validSections.ElementAt(i).colorInactive;

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{x} {y}", AnchorMax = $"{x} {y}", OffsetMin = $"-{config.sectionSize} -{config.sectionSize}", OffsetMax = $"{config.sectionSize} {config.sectionSize}" },
                    Button = { Color = "1 1 1 0", Command = validSections.ElementAt(i).command == "" ? $"distributionCommand {validSections.ElementAt(i).perm}" : string.Format("distributionCommand {0} {1} {2}", validSections.ElementAt(i).perm, validSections.ElementAt(i).isChat ? "chat" : "console", validSections.ElementAt(i).command), FadeIn = section1FadeIn }
                }, "RadialMenu.MainPanel", $"RadialMenu.LVL1.{i}");

                if (!validSections.ElementAt(i).isFill)
                {
                    container.Add(new CuiElement
                    {
                        Parent = $"RadialMenu.LVL1.{i}",
                        Components = 
                        {
                            new CuiRectTransformComponent { AnchorMin = "0.1 0.2", AnchorMax = "0.9 1" },
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"RadialMenu.{validSections.ElementAt(i).perm}"), FadeIn = section1FadeIn }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiPanel 
                    {
                        RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.9 1" },
                        Image = { Png = (string) ImageLibrary.Call("GetImage", $"RadialMenu.{validSections.ElementAt(i).perm}"), Color = color1, FadeIn = section1FadeIn }
                    }, $"RadialMenu.LVL1.{i}");
                }

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "-0.1 -0.15", AnchorMax = "1.1 0.15" },
                    Text = { Text = validSections.ElementAt(i).name, Color = color1, FontSize = config.textSize, FadeIn = section1FadeIn, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, $"RadialMenu.LVL1.{i}");
            }

            MenuSectionClass validSections2 = new MenuSectionClass();
            Dictionary<string, float> section2Radians = new Dictionary<string, float>();

            if (!lvls.IsNullOrEmpty()) 
            {
                validSections2 = validSections.FirstOrDefault(x => x.perm == lvls[0]);
                float a = 0;

                if (validSections2.separate < 0)
                {
                    if (validSections2.subsections.Count % 2 == 0) a = section1Radians[lvls[0]] + (3.14f * validSections2.subsections.Count - 3.14f) / Math.Abs(validSections2.separate);
                    else 
                    {
                        if (section1Radians[lvls[0]] >= 0) a = section1Radians[lvls[0]] + validSections2.subsections.Count / 2 * (6.28f / Math.Abs(validSections2.separate));
                        else a = section1Radians[lvls[0]] - validSections2.subsections.Count / -2 * (6.28f / Math.Abs(validSections2.separate));
                    }
                }

                for (int i = 0; i < validSections2.subsections.Count; i++)
                {
                    float rad = 0;

                    if (validSections2.separate == 0) rad = 1.57f - i * 6.28f / validSections2.subsections.Count;
                    else if (validSections2.separate > 0) rad = 1.57f - i * 6.28f / validSections2.separate;
                    else if (validSections2.separate < 0) rad = a - (i * 6.28f / Math.Abs(validSections2.separate));

                    float x = 0.5f + config.distanceLVL2 * Mathf.Cos(rad);
                    float y = 0.5f + config.distanceLVL2 * Mathf.Sin(rad);

                    section2Radians.Add(validSections2.subsections[i].perm, rad);
                    if (!lvls.IsNullOrEmpty() && lvls.Length > 1 && lvls[1] == validSections2.subsections[i].perm) color2 = validSections2.subsections[i].colorActive;
                    else color2 = validSections2.subsections[i].colorInactive;

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{x} {y}", AnchorMax = $"{x} {y}", OffsetMin = $"-{config.sectionSize} -{config.sectionSize}", OffsetMax = $"{config.sectionSize} {config.sectionSize}" },
                        Button = { Color = "1 1 1 0", Command = validSections2.subsections[i].command == "" ? $"distributionCommand {validSections2.perm}.{validSections2.subsections[i].perm}" : string.Format("distributionCommand {0}.{1} {2} {3}", validSections2.perm, validSections2.subsections[i].perm, validSections2.subsections[i].isChat ? "chat" : "console", validSections2.subsections[i].command), FadeIn = section2FadeIn }
                    }, "RadialMenu.MainPanel", $"RadialMenu.LVL2.{i}");

                    if (!validSections2.subsections[i].isFill)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = $"RadialMenu.LVL2.{i}",
                            Components = 
                            {
                                new CuiRectTransformComponent { AnchorMin = "0.1 0.2", AnchorMax = "0.9 1" },
                                new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"RadialMenu.{validSections2.perm}.{validSections2.subsections[i].perm}"), FadeIn = section2FadeIn }
                            }
                        });
                    }
                    else
                    {
                        container.Add(new CuiPanel 
                        {
                            RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.9 1" },
                            Image = { Png = (string) ImageLibrary.Call("GetImage", $"RadialMenu.{validSections2.perm}.{validSections2.subsections[i].perm}"), Color = color2, FadeIn = section2FadeIn }
                        }, $"RadialMenu.LVL2.{i}");
                    }

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "-0.1 -0.15", AnchorMax = "1.1 0.15" },
                        Text = { Text = validSections2.subsections[i].name, Color = color2, FadeIn = section2FadeIn, FontSize = config.textSize, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                    }, $"RadialMenu.LVL2.{i}");
                }
            }

            if (!lvls.IsNullOrEmpty() && lvls.Length > 1)
            {
                MenuSectionClass validSections3 = validSections2.subsections.First(x => x.perm == lvls[1]);
                float a = 0;

                if (validSections3.separate < 0)
                {
                    if (validSections3.subsections.Count % 2 == 0) a = section2Radians[lvls[1]] + (3.14f * validSections3.subsections.Count - 3.14f) / Math.Abs(validSections3.separate);
                    else 
                    {
                        if (section2Radians[lvls[1]] >= 0) a = section2Radians[lvls[1]] + validSections3.subsections.Count / 2 * (6.28f / Math.Abs(validSections3.separate));
                        else a = section2Radians[lvls[1]] + validSections3.subsections.Count / 2 * (6.28f / Math.Abs(validSections3.separate));
                    }
                }

                for (int i = 0; i < validSections3.subsections.Count; i++)
                {
                    float rad = 0;

                    if (validSections3.separate == 0) rad = 1.57f - i * 6.28f / validSections3.subsections.Count;
                    else if (validSections3.separate > 0) rad = 1.57f - i * 6.28f / validSections3.separate;
                    else if (validSections3.separate < 0) rad = a - (i * 6.28f / Math.Abs(validSections3.separate));

                    float x = 0.5f + config.distanceLVL3 * Mathf.Cos(rad);
                    float y = 0.5f + config.distanceLVL3 * Mathf.Sin(rad);

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{x} {y}", AnchorMax = $"{x} {y}", OffsetMin = $"-{config.sectionSize} -{config.sectionSize}", OffsetMax = $"{config.sectionSize} {config.sectionSize}" },
                        Button = { Color = "1 1 1 0", Command = validSections3.subsections[i].command == "" ? $"distributionCommand {validSections2.perm}.{validSections3.perm}.{validSections3.subsections[i].perm}" : string.Format("distributionCommand {0}.{1}.{2} {3} {4}", validSections2.perm, validSections3.perm, validSections3.subsections[i].perm, validSections3.subsections[i].isChat ? "chat" : "console", validSections3.subsections[i].command), FadeIn = section3FadeIn }
                    }, "RadialMenu.MainPanel", $"RadialMenu.LVL3.{i}");

                    if (!validSections3.subsections[i].isFill)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = $"RadialMenu.LVL3.{i}",
                            Components = 
                            {
                                new CuiRectTransformComponent { AnchorMin = "0.1 0.2", AnchorMax = "0.9 1" },
                                new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"RadialMenu.{validSections2.perm}.{validSections3.perm}.{validSections3.subsections[i].perm}"), FadeIn = section3FadeIn }
                            }
                        });
                    }
                    else
                    {
                        container.Add(new CuiPanel 
                        {
                            RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.9 1" },
                            Image = { Png = (string) ImageLibrary.Call("GetImage", $"RadialMenu.{validSections2.perm}.{validSections3.perm}.{validSections3.subsections[i].perm}"), Color = validSections3.subsections[i].colorInactive, FadeIn = section3FadeIn }
                        }, $"RadialMenu.LVL3.{i}");
                    }

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "-0.1 -0.15", AnchorMax = "1.1 0.15" },
                        Text = { Text = validSections3.subsections[i].name, Color = validSections3.subsections[i].colorInactive, FadeIn = section3FadeIn, FontSize = config.textSize, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                    }, $"RadialMenu.LVL3.{i}");
                }       
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}