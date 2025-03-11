using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;
using Global = Rust.Global;

namespace Oxide.Plugins;

[Info("LSkins", "LAGZYA", "1.7.8")]
public class LSkins : RustPlugin
{
    #region Cfg

    private const bool En = true;

    private class DataPlayer
    {
        [JsonProperty(En ? "List of favorite skins" : "Список избранных скинов")]
        public Dictionary<string, Dictionary<ulong, ItemData.SkinInfo>> _izvSkins =
            new Dictionary<string, Dictionary<ulong, ItemData.SkinInfo>>();

        [JsonProperty(En ? "List of default skins supplied" : "Список поставленных скинов по дефолту")]
        public Dictionary<string, DefaultSkins> _defaultSkins =
            new Dictionary<string, DefaultSkins>();
    }

    private class DefaultSkins
    {
        [JsonProperty(En ? "SkinID" : "СкинАйди")]
        public ulong skinId;

        [JsonProperty(En ? "Information about the skin" : "Информация о скине")]
        public ItemData.SkinInfo SkinInfo;
    }

    private Dictionary<ulong, DataPlayer> _playerData = new Dictionary<ulong, DataPlayer>();
    private static ConfigData Cfg { get; set; }


    private class ConfigData
    {
        internal class MarketSystem
        {
            [JsonProperty(En ? "Enabled(true = yes)" : "Включить?(true = да)")]
            public bool IsEnabled = false;

            [JsonProperty(En
                ? "How to pay?(Scrap, ServerRewards, Economics)"
                : "Чем платить?(Scrap, ServerRewards, Economics)")]
            public string WhoMarket = "Scrap";

            [JsonProperty(En
                ? "The price for opening the interface (If 0, then free)"
                : "Цена за открытие интерфейса(Если 0, то бесплатно)")]
            public int costOpen = 10;

            [JsonProperty(En
                ? "Price per change of one skin (If 0, then free)"
                : "Цена за смену одного скина(Если 0, то бесплатно)")]
            public int costChange = 10;
        }

        internal class InterfaceSetting
        {
            [JsonProperty(En ? "Background color of favorite skins" : "Цвет фона фаворитных скинов")]
            public string favcolor = "#535150";

            [JsonProperty(En ? "Background color of the main panel" : "Цвет фона основной панели")]
            public string mainColor = "#535151";

            [JsonProperty(En ? "The color of the forward arrow" : "Цвет стрелочки вперед")]
            public string pageup = "#5c80ba";

            [JsonProperty(En ? "The color of the back arrow" : "Цвет стрелочки назад")]
            public string pagedown = "#5c80ba";
        }

        [JsonProperty(En
            ? "Add a skin to the cfg automatically?(true = yes)"
            : "Добавить в кфг автоматически скин?(true = да)")]
        public bool IsApproved = false;

        [JsonProperty(En
            ? "Add a hazmats to the cfg automatically?(true = yes)"
            : "Добавить в кфг автоматически хазматы?(true = да)")]
        public bool IsHazmat = false;

        [JsonProperty(En ? "Rights to use the skins system" : "Права для использование системы скинов.")]
        public string canuse = "lskins.use";
        [JsonProperty(En ? "Rights to use the skins approve" : "Права для использование подтвержденных скинов.")]
        public string canuseapprove = "lskins.useapprove";
        [JsonProperty(En ? "List of available commands" : "Список доступных команд")]
        public List<string> Commands = new List<string>();

        [JsonProperty(En
            ? "List of available commands for skins entity"
            : "Список доступных команд для изменения скина ентити")]
        public List<string> EntCommands = new List<string>();

        [JsonProperty(En
            ? "Install the skin automatically on items where there is already a skin?(true = yes)"
            : "Устанавилвать скин автоматически на предметы где уже есть скин?(true = да)")]
        public bool SkinAuto = false;

        [JsonProperty(En ? "Rights to use default skins" : "Права для использование скинов по дефолту")]
        public string canusedefault = "lskins.usedefault";

        [JsonProperty(En ? "Setting up payment for opening" : "Настройка оплаты за открытие")]
        public MarketSystem Market = new MarketSystem();

        [JsonProperty(En ? "Setting interface" : "Настройка интерфейса")]
        public InterfaceSetting Interface = new InterfaceSetting();

        [JsonProperty(En
            ? "Items with these skins cannot be exchanged."
            : "Предметы с этими скинами нельзя будет поменять.")]
        public List<ulong> blackList = new List<ulong>();

        [JsonProperty(En ? "Rights to use unique skins" : "Права для использование уникальных скинов")]
        public Dictionary<string, List<ulong>> useSkins = new Dictionary<string, List<ulong>>();

        public static ConfigData GetNewConf()
        {
            var newConfig = new ConfigData
            {
                Commands = new List<string>()
                {
                    "skins",
                    "skin",
                    "s"
                },
                EntCommands = new List<string>()
                {
                    "se"
                },
                useSkins = new Dictionary<string, List<ulong>>()
                {
                    ["lskins.prem"] = new List<ulong>()
                    {
                        2668297561,
                        2649480126,
                        2599664731,
                    }
                },
                blackList = new List<ulong>()
                {
                    12341234,
                }
            };
            return newConfig;
        }
    }

    class ItemData
    {
        public class SkinInfo
        {
            [JsonProperty(En ? "Enabled skin?(true = yes)" : "Включить скин?(true = да)")]
            public bool IsEnabled = true;

            [JsonProperty(En
                ? "Is this skin from the developers of rust or take it in a workshop?"
                : "Этот скин есть от разработчиков раста или принять в воркшопе??(true = да)")]
            public bool IsApproved = true;

            [JsonProperty(En ? "Name skin" : "Название скина")]
            public string MarketName = "Warhead LR300";
        }

        public Dictionary<ulong, SkinInfo> _skinInfos = new Dictionary<ulong, SkinInfo>();
    }

    protected override void LoadDefaultConfig()
    {
        Cfg = ConfigData.GetNewConf();
    }

    protected override void SaveConfig()
    {
        Config.WriteObject(Cfg);
    }

    protected override void LoadConfig()
    {
        base.LoadConfig();
        try
        {
            Cfg = Config.ReadObject<ConfigData>();
        }
        catch
        {
            LoadDefaultConfig();
        }

        NextTick(SaveConfig);
    }

    #endregion

    #region Interface

    private static string Layer = "UiLSkins";
    private static string Hud = "Hud";
    private static string Overlay = "Overlay";
    private static string _regular = "robotocondensed-regular.ttf";
    private static string _blur = "assets/content/ui/uibackgroundblur.mat";
    private static string _sharp = "assets/content/ui/ui.background.tile.psd";
    private static string _radial = "assets/content/ui/ui.background.transparent.radial.psd";


    private void LoadIzbranoeUI(BasePlayer player, string weapon, ulong itemId, int page, bool isentity = false)
    {
        if (!_playerData.TryGetValue(player.userID, out var weapons)) return;
        CuiHelper.DestroyUi(player, Layer + "-Izbranoe");
        var cont = new CuiElementContainer();
        cont.Add(new CuiPanel()
        {
            Image =
            {
                Color = "0 0 0 0"
            },
            RectTransform =
            {
                AnchorMin = "0.5 0",
                AnchorMax = "0.5 0",
                OffsetMin = $"{201} {111}",
                OffsetMax = $"{571} {231}"
            }
        }, "Overlay", Layer + "-Izbranoe");

        if (!weapons._izvSkins.ContainsKey(weapon)) return;
        var tSkinsList = weapons._izvSkins[weapon];
        foreach (var keyValuePair in Cfg.useSkins.Where(keyValuePair =>
                     !permission.UserHasPermission(player.UserIDString, keyValuePair.Key)))
        {
            keyValuePair.Value.ForEach(p =>
            {
                if (tSkinsList.ContainsKey(p))
                {
                    tSkinsList.Remove(p);
                }
            });
        }

        for (var i = 0; i < 12; i++)
        {
            if (tSkinsList.Count - 1 < i) continue;
            cont.Add(new CuiElement()
            {
                Parent = Layer + "-Izbranoe",
                Components =
                {
                    new CuiImageComponent()
                    {
                        ItemId = weapon.Contains("hazmat") ? ItemManager.FindItemDefinition(_hazmats[tSkinsList.ToList()[i].Key]).itemid : ItemManager.FindItemDefinition(weapon).itemid,
                        SkinId = tSkinsList.ToList()[i].Key
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin =
                            $"0 0.5",
                        AnchorMax =
                            $"0 0.5",
                        OffsetMin =
                            $"{2 + i * 62 - Math.Floor((double)i / 6) * 6 * 62} {5 - Math.Floor((double)i / 6) * 62}",
                        OffsetMax =
                            $"{52 + i * 62 - Math.Floor((double)i / 6) * 6 * 62} {55 - Math.Floor((double)i / 6) * 62}"
                    }
                }
            });
            var type = isentity ? "setskinent" : "setskin";
            cont.Add(new CuiButton()
            {
                Button =
                {
                    Color = "1 1 1 0",
                    Command = $"uiskinmenu {type} {itemId} {tSkinsList.ToList()[i].Key}"
                },
                Text =
                {
                    Text = ""
                },
                RectTransform =
                {
                    AnchorMin =
                        $"0 0.5",
                    AnchorMax =
                        $"0 0.5",
                    OffsetMin =
                        $"{2 + i * 62 - Math.Floor((double)i / 6) * 6 * 62} {5 - Math.Floor((double)i / 6) * 62}",
                    OffsetMax =
                        $"{52 + i * 62 - Math.Floor((double)i / 6) * 6 * 62} {55 - Math.Floor((double)i / 6) * 62}"
                }
            }, Layer + "-Izbranoe", Layer + "-Izbranoe" + i);
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0 1",
                    AnchorMax = "0 1",
                    OffsetMin = $"{-5} {-10}",
                    OffsetMax = $"{10} {5}"
                },
                Button =
                {
                    Command = $"uiskinmenu setizbranoe {weapon} {tSkinsList.ToList()[i].Key} {page} {itemId}",
                    Color = HexToRustFormat("#dead39"),
                    Sprite = "assets/icons/favourite_servers.png",
                },
                Text = { Text = "" }
            }, Layer + "-Izbranoe" + i);
            if (!permission.UserHasPermission(player.UserIDString, Cfg.canusedefault)) continue;
            if (_playerData.TryGetValue(player.userID, out weapons) && weapons._defaultSkins.ContainsKey(weapon) &&
                weapons._defaultSkins[weapon].skinId == tSkinsList.ToList()[i].Key)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{-10} {0}",
                        OffsetMax = $"{0} {10}"
                    },
                    Button =
                    {
                        Command = $"uiskinmenu setdefault {weapon} {tSkinsList.ToList()[i].Key} {page} {itemId}",
                        Color = HexToRustFormat("#369f47"),
                        Sprite = "assets/icons/power.png",
                    },
                    Text = { Text = "" }
                }, Layer + "-Izbranoe" + i);
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{-10} {0}",
                        OffsetMax = $"{0} {10}"
                    },
                    Button =
                    {
                        Command = $"uiskinmenu setdefault {weapon} {tSkinsList.ToList()[i].Key} {page} {itemId}",
                        Color = HexToRustFormat("#fb4f3b"),
                        Sprite = "assets/icons/power.png",
                    },
                    Text = { Text = "" }
                }, Layer + "-Izbranoe" + i);
            }
        }

        CuiHelper.AddUi(player, cont);
    }

    private void LoadWearUI(BasePlayer player)
    {
        CuiHelper.DestroyUi(player, Layer + "-Wear");
        var cont = new CuiElementContainer();
        cont.Add(new CuiPanel()
        {
            Image =
            {
                Color = "0 0 0 0"
            },
            RectTransform =
            {
                AnchorMin = "0.5 0",
                AnchorMax = "0.5 0",
                OffsetMin = $"{-588} {115}",
                OffsetMax = $"{-215} {169}"
            }
        }, "Overlay", Layer + "-Wear");

        for (int i = 0; i < player.inventory.containerWear.capacity - 1; i++)
        {
            var item = player.inventory.containerWear.GetSlot(i);
            if (item == null)
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Wear",
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage($"LSKINGRID({Version})"),
                            Color = "1 1 1 0.05"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"0 0",
                            AnchorMax =
                                $"0 0",
                            OffsetMin = $"{0 + i * 54 - Math.Floor((double)i / 7) * 7 * 54} {0}",
                            OffsetMax = $"{50 + i * 54 - Math.Floor((double)i / 7) * 7 * 54} {50}"
                        }
                    }
                });
                continue;
            }

            var onblacklist = Cfg.blackList.Contains(item.skin);
            var shortname = item.info.shortname.Contains("hazmatsuit") ? "hazmatsuit" : item.info.shortname;
            var skinsExsist = Interface.Oxide.DataFileSystem.ExistsDatafile($"LSkins/Skins/{shortname}");
            if (!onblacklist && skinsExsist)
            {
                cont.Add(new CuiButton()
                {
                    Button =
                    {
                        Color = "0.65 0.65 0.65 0",
                        Command = $"uiskinmenu weaponSelect {item.info.shortname} {item.uid}"
                    },
                    Text =
                    {
                        Text = ""
                    },
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"0 0",
                        OffsetMin = $"{0 + i * 54 - Math.Floor((double)i / 7) * 7 * 54} {0}",
                        OffsetMax = $"{50 + i * 54 - Math.Floor((double)i / 7) * 7 * 54} {50}"
                    }
                }, Layer + "-Wear");
            }
            else
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Wear",
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage($"LSKINGRID({Version})"),
                            Color = "1 1 1 0.05"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"0 0",
                            AnchorMax =
                                $"0 0",
                            OffsetMin = $"{0 + i * 54 - Math.Floor((double)i / 7) * 7 * 54} {0}",
                            OffsetMax = $"{50 + i * 54 - Math.Floor((double)i / 7) * 7 * 54} {50}"
                        }
                    }
                });
            }
        }

        CuiHelper.AddUi(player, cont);
    }

    private void LoadMainUI(BasePlayer player)
    {
        CuiHelper.DestroyUi(player, Layer + "-Main");
        var cont = new CuiElementContainer();
        cont.Add(new CuiPanel()
        {
            Image =
            {
                Color = "0 0 0 0"
            },
            RectTransform =
            {
                AnchorMin = $"{0.5} {0}",
                AnchorMax = $"{0.5} {0}",
                OffsetMin = $"{-200} {85}",
                OffsetMax = $"{180} {337}"
            }
        }, "Overlay", Layer + "-Main");
        for (int i = 0; i < player.inventory.containerMain.capacity; i++)
        {
            var item = player.inventory.containerMain.GetSlot(i);
            if (item == null)
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Main",
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage($"LSKINGRID({Version})"),
                            Color = "1 1 1 0.05"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"0 1",
                            AnchorMax =
                                $"0 1",
                            OffsetMin =
                                $"{0 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {-60 - Math.Floor((double)i / 6) * 63.5}",
                            OffsetMax =
                                $"{60 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {0 - Math.Floor((double)i / 6) * 63.5}"
                        }
                    }
                });
                continue;
            }

            var onblacklist = Cfg.blackList.Contains(item.skin);
            var shortname = item.info.shortname.Contains("hazmatsuit") ? "hazmatsuit" : item.info.shortname;
            var skinsExsist = Interface.Oxide.DataFileSystem.ExistsDatafile($"LSkins/Skins/{shortname}");
            if (!onblacklist && skinsExsist)
            {
                cont.Add(new CuiButton()
                {
                    Button =
                    {
                        Color = "0.65 0.65 0.65 0",
                        Command = $"uiskinmenu weaponSelect {item.info.shortname} {item.uid}"
                    },
                    Text =
                    {
                        Text = ""
                    },
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 1",
                        AnchorMax =
                            $"0 1",
                        OffsetMin =
                            $"{0 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {-60 - Math.Floor((double)i / 6) * 63}",
                        OffsetMax =
                            $"{60 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {0 - Math.Floor((double)i / 6) * 63}"
                    }
                }, Layer + "-Main");
            }
            else
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Main",
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage($"LSKINGRID({Version})"),
                            Color = "1 1 1 0.05"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"0 1",
                            AnchorMax =
                                $"0 1",
                            OffsetMin =
                                $"{0 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {-60 - Math.Floor((double)i / 6) * 63}",
                            OffsetMax =
                                $"{60 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {0 - Math.Floor((double)i / 6) * 63.5}"
                        }
                    }
                });
            }
        }

        CuiHelper.AddUi(player, cont);
    }

    private void LoadBeltUI(BasePlayer player)
    {
        CuiHelper.DestroyUi(player, Layer + "-Belt");
        var cont = new CuiElementContainer();
        cont.Add(new CuiPanel()
        {
            Image =
            {
                Color = "0 0 0 0"
            },
            RectTransform =
            {
                AnchorMin = "0.5 0",
                AnchorMax = "0.5 0",
                OffsetMin = $"{-200} {17}",
                OffsetMax = $"{180} {79}"
            }
        }, "Overlay", Layer + "-Belt");
        for (int i = 0; i < 6; i++)
        {
            var item = player.inventory.containerBelt.GetSlot(i);
            if (item == null)
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Belt",
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage($"LSKINGRID({Version})"),
                            Color = "1 1 1 0.05"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"0 0",
                            AnchorMax =
                                $"0 0",
                            OffsetMin = $"{0 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {0}",
                            OffsetMax = $"{60 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {60}"
                        }
                    }
                });
                continue;
            }

            var onblacklist = Cfg.blackList.Contains(item.skin);
            var shortname = item.info.shortname.Contains("hazmatsuit") ? "hazmatsuit" : item.info.shortname;
            var skinsExsist = Interface.Oxide.DataFileSystem.ExistsDatafile($"LSkins/Skins/{shortname}");
            if (!onblacklist && skinsExsist)
            {
                cont.Add(new CuiButton()
                {
                    Button =
                    {
                        Color = "0.65 0.65 0.65 0",
                        Command = $"uiskinmenu weaponSelect {item.info.shortname} {item.uid}"
                    },
                    Text =
                    {
                        Text = ""
                    },
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"0 0",
                        OffsetMin = $"{0 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {0}",
                        OffsetMax = $"{60 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {60}"
                    }
                }, Layer + "-Belt");
            }
            else
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "-Belt",
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage($"LSKINGRID({Version})"),
                            Color = "1 1 1 0.05"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"0 0",
                            AnchorMax =
                                $"0 0",
                            OffsetMin = $"{0 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {0}",
                            OffsetMax = $"{60 + i * 64 - Math.Floor((double)i / 6) * 6 * 64} {60}"
                        }
                    }
                });
            }
        }

        CuiHelper.AddUi(player, cont);
    }

    [ConsoleCommand("uiskinmenu")]
    private void ConsoleCommand(ConsoleSystem.Arg arg)
    {
        if(arg.Args == null || arg.Args.Length < 1) return;
        var player = arg.Player();
        if (player == null) return;
        var sound1 = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(),
            new Vector3());
        string weapon;
        ulong skinId;
        int page;
        int itemId;
        DataPlayer weapons;
        string uid;
        switch (arg.Args[0])
        {
            case "search":
                EffectNetwork.Send(sound1, player.Connection);
                LoadSkinsUI(player, arg.Args[1], 1, uint.Parse(arg.Args[2]), bool.Parse(arg.Args[3]), arg.Args[4]);
                break;
            case "page":
                EffectNetwork.Send(sound1, player.Connection);
                LoadSkinsUI(player, arg.Args[1], arg.Args[2].ToInt(), uint.Parse(arg.Args[3]), bool.Parse(arg.Args[4]));
                LoadIzbranoeUI(player, arg.Args[1], uint.Parse(arg.Args[3]), arg.Args[2].ToInt(),
                    bool.Parse(arg.Args[4]));
                break;
            case "weaponSelect":
                EffectNetwork.Send(sound1, player.Connection);
                LoadSkinsUI(player, arg.Args[1], 1, uint.Parse(arg.Args[2]));
                LoadIzbranoeUI(player, arg.Args[1], uint.Parse(arg.Args[2]), 1);
                break;
            case "setizbranoe":
                EffectNetwork.Send(sound1, player.Connection);
                if (!_playerData.TryGetValue(player.userID, out weapons)) return;
                weapon = arg.Args[1];
                skinId = ulong.Parse(arg.Args[2]);
                page = arg.Args[3].ToInt();
                itemId = arg.Args[4].ToInt();
                if (weapons._izvSkins.ContainsKey(weapon) && weapons._izvSkins[weapon].ContainsKey(skinId))
                {
                    weapons._izvSkins[weapon].Remove(skinId);
                }
                else if (weapons._izvSkins.ContainsKey(weapon))
                {
                    weapons._izvSkins[weapon].Add(skinId, _loadData[weapon][skinId]);
                }
                else
                {
                    weapons._izvSkins.Add(weapon, new Dictionary<ulong, ItemData.SkinInfo>()
                    {
                        [skinId] = _loadData[weapon][skinId]
                    });
                }

                LoadSkinsUI(player, weapon, page, (uint)itemId);
                LoadIzbranoeUI(player, weapon, (uint)itemId, page);
                break;
            case "setdefault":
                EffectNetwork.Send(sound1, player.Connection);
                if (!_playerData.TryGetValue(player.userID, out weapons)) return;
                weapon = arg.Args[1];
                skinId = ulong.Parse(arg.Args[2]);
                page = arg.Args[3].ToInt();
                itemId = arg.Args[4].ToInt();
                if (weapons._defaultSkins.ContainsKey(weapon) && weapons._defaultSkins[weapon].skinId == skinId)
                {
                    weapons._defaultSkins.Remove(weapon);
                }
                else if (weapons._defaultSkins.ContainsKey(weapon))
                {
                    weapons._defaultSkins[weapon].skinId = skinId;
                    weapons._defaultSkins[weapon].SkinInfo = _loadData[weapon][skinId];
                }
                else
                {
                    weapons._defaultSkins.Add(weapon, new DefaultSkins()
                    {
                        skinId = skinId,
                        SkinInfo = _loadData[weapon][skinId]
                    });
                }

                LoadSkinsUI(player, weapon, page, (uint)itemId);
                LoadIzbranoeUI(player, weapon, (uint)itemId, page);
                break;
            case "setskin":
                EffectNetwork.Send(sound1, player.Connection);
                if (Cfg.Market.IsEnabled && !permission.UserHasPermission(player.UserIDString, Cfg.canuse))
                {
                    if (ChangeSkin(player) == false)
                    {
                        return;
                    }
                }

                uid = arg.Args[1];
                var item = player.inventory.FindItemByUID(new ItemId(ulong.Parse(uid)));
                if (item.info.shortname.Contains("hazmatsuit"))
                {
                    var pos = item.position;
                    var container = item.GetRootContainer();
                    var uids = item.uid;
                    int amount = item.amount;
                    item.DoRemove();
                    item = ItemManager.CreateByName($"{_hazmats[ulong.Parse(arg.Args[2])]}");
                    item.uid = uids;
                    item.MoveToContainer(container, pos);
                    item.amount = amount;
                    item.MarkDirty();
                    player.SendNetworkUpdate();
                    return;
                }

                item.skin = ulong.Parse(arg.Args[2]);
                var hend = item.GetHeldEntity();
                if (hend != null)
                {
                    hend.skinID = ulong.Parse(arg.Args[2]);
                    hend.SendNetworkUpdate();
                }

                item.MarkDirty();
                player.SendNetworkUpdate();
                break;
            case "setskinent":
                EffectNetwork.Send(sound1, player.Connection);
                if (Cfg.Market.IsEnabled && !permission.UserHasPermission(player.UserIDString, Cfg.canuse))
                {
                    if (ChangeSkin(player) == false)
                    {
                        return;
                    }
                }

                uint entid = uint.Parse(arg.Args[1]);
                var ent = BaseEntity.serverEntities.Find(new NetworkableId(entid)) as BaseEntity;
                if (ent != null)
                {
                    ent.skinID = ulong.Parse(arg.Args[2]);
                    ent.SendNetworkUpdate();
                }

                break;
            case "close":
                EffectNetwork.Send(sound1, player.Connection);
                player.EndLooting();
                break;
        }
    }

    private void LoadSkinsUI(BasePlayer player, string weapon, int page, ulong itemId, bool isentity = false,
        string search = null)
    {
        if (weapon.Contains("hazmatsuit"))
            weapon = "hazmatsuit";
        var skinsList = _loadData.ContainsKey(weapon);
        if (!skinsList) return;
        if (!_playerData.TryGetValue(player.userID, out var weapons)) return;
        CuiHelper.DestroyUi(player, Layer + "-Search");
        var cont = new CuiElementContainer();
        cont.Add(new CuiPanel()
        {
            Image =
            {
                ImageType = UnityEngine.UI.Image.Type.Filled,
                Png = "assets/standard assets/effects/imageeffects/textures/noise.png",
                Sprite = _sharp,
                Color = HexToRustFormat("#5351512C"),
                Material = "assets/icons/greyout.mat"
            },
            RectTransform =
            {
                AnchorMin = "0.5 0.5",
                AnchorMax = "0.5 0.5",
                OffsetMin = $"{-400} {-185}",
                OffsetMax = $"{-35} {115}"
            }
        }, Layer, Layer + "-Search");
        cont.Add(new CuiPanel()
        {
            Image =
            {
                ImageType = UnityEngine.UI.Image.Type.Filled,
                Png = "assets/standard assets/effects/imageeffects/textures/noise.png",
                Sprite = _sharp,
                Color = HexToRustFormat("#5351512C"),
                Material = "assets/icons/greyout.mat"
            },
            RectTransform =
            {
                AnchorMin = "1 0.5",
                AnchorMax = "1 0.5",
                OffsetMin = $"{4} {-150}",
                OffsetMax = $"{42} {150}"
            }
        }, Layer + "-Search", Layer + "-Page");
        var tSkinsList = _loadData[weapon].Where(p => permission.UserHasPermission(player.UserIDString, Cfg.canuseapprove) ?  p.Value.IsApproved || !p.Value.IsApproved : !p.Value.IsApproved)
            .ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value);

        if (Cfg.useSkins.Count >= 1)
            foreach (var keyValuePair in Cfg.useSkins.Where(keyValuePair =>
                         !permission.UserHasPermission(player.UserIDString, keyValuePair.Key)))
            {
                keyValuePair.Value.ForEach(p =>
                {
                    if (tSkinsList.ContainsKey(p))
                    {
                        tSkinsList.Remove(p);
                    }
                });
            }
        if (0 < tSkinsList.Count - 12 * page)
        {
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = $"{-10} {5}",
                    OffsetMax = $"{10} {143}"
                },
                Button =
                {
                    Command = $"uiskinmenu page {weapon} {page + 1} {itemId} {isentity}",
                    Color = HexToRustFormat($"{Cfg.Interface.pageup}c3")
                },
                Text =
                {
                    Text = "»", Align = TextAnchor.MiddleCenter,
                    FontSize = (int)(20)
                }
            }, Layer + "-Page");
        }
        else
        {
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = $"{-10} {5}",
                    OffsetMax = $"{10} {143}"
                },
                Button =
                {
                    Command = $"",
                    Color = HexToRustFormat($"{Cfg.Interface.pageup}c3")
                },
                Text =
                {
                    Text = "»", Align = TextAnchor.MiddleCenter,
                    FontSize = (int)(20),
                    Color = "0.65 0.65 0.65 0.65"
                }
            }, Layer + "-Page");
        }

        if (page > 1)
        {
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = $"{-10} {-137}",
                    OffsetMax = $"{10} {-5}"
                },
                Button =
                {
                    Command = $"uiskinmenu page {weapon} {page - 1} {itemId} {isentity}",
                    Color = HexToRustFormat(Cfg.Interface.pagedown)
                },
                Text =
                {
                    Text = "«", Align = TextAnchor.MiddleCenter, FontSize = (int)(20)
                }
            }, Layer + "-Page");
        }
        else
        {
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = $"{-10} {-137}",
                    OffsetMax = $"{10} {-5}"
                },
                Button =
                {
                    Command = $"",
                    Color = HexToRustFormat($"{Cfg.Interface.pagedown}c3")
                },
                Text =
                {
                    Text = "«", Align = TextAnchor.MiddleCenter, FontSize = (int)(20),
                    Color = "0.65 0.65 0.65 0.65"
                }
            }, Layer + "-Page");
        }

        
        
        cont.Add(new CuiElement()
        {
            Parent = Layer + "-Search",
            Components =
            {
                new CuiImageComponent()
                {
                    ImageType = UnityEngine.UI.Image.Type.Filled,
                    Png = "assets/standard assets/effects/imageeffects/textures/noise.png",
                    Sprite = _sharp,
                    Color = "0.25 0.25 0.25 0.3",
                    Material = "assets/icons/greyout.mat"
                },
                new CuiRectTransformComponent()
                {
                    AnchorMin = "0.7 1",
                    AnchorMax = "0.7 1",
                    OffsetMin = "-70 13",
                    OffsetMax = "100 35"
                }
            }
        });
        cont.Add(new CuiElement()
        {
            Parent = Layer + "-Search",
            Components =
            {
                new CuiInputFieldComponent()
                {
                    NeedsKeyboard = false,
                    Text = lang.GetMessage("LABLESEARCH", this, player.UserIDString),
                    Align = TextAnchor.MiddleCenter,
                    Command = $"uiskinmenu search {weapon} {itemId} {isentity} ",
                    Color = "1 1 1 1",
                    FontSize = 12
                },
                new CuiRectTransformComponent()
                {
                    AnchorMin = "0.7 1",
                    AnchorMax = "0.7 1",
                    OffsetMin = "-70 13",
                    OffsetMax = "100 35"
                }
            }
        });
        foreach (var skinItem in tSkinsList
                     .Where(p => search == null
                         ? p.Value != null
                         : p.Value.MarketName.ToLower().Contains(search.ToLower()) ||
                           p.Key.ToString().Contains(search.ToLower())).Where(p => p.Value.IsEnabled)
                     .Select((i, t) => new { A = i, B = t - (page - 1) * 12 }).Skip((page - 1) * 12).Take(12))
        {
            cont.Add(new CuiElement()
            {
                Parent = Layer + "-Search",
                Name = Layer + "-Search" + ".Player" + skinItem.B,
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.35 0.35 0.35 0.65"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0.5",
                        AnchorMax = $"0 0.5",
                        OffsetMin =
                            $"{4 + skinItem.B * 90 - Math.Floor((double)skinItem.B / 4) * 4 * 90} {60 - Math.Floor((double)skinItem.B / 4) * 98}",
                        OffsetMax =
                            $"{90 + skinItem.B * 90 - Math.Floor((double)skinItem.B / 4) * 4 * 90} {143 - Math.Floor((double)skinItem.B / 4) * 98}"
                    }
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = Layer + "-Search" + ".Player" + skinItem.B,
                Components =
                {
                    new CuiImageComponent()
                    {
                        ItemId =  weapon.Contains("hazmat") ? ItemManager.FindItemDefinition(_hazmats[skinItem.A.Key]).itemid : ItemManager.FindItemDefinition(weapon).itemid,
                        SkinId = skinItem.A.Key
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-30 -30",
                        OffsetMax = "30 30"
                    }
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = Layer + "-Search" + ".Player" + skinItem.B,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = skinItem.A.Value.MarketName, Align = TextAnchor.MiddleCenter,
                        Font = _regular,
                        FontSize = 9
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = $"{-40} {-15}",
                        OffsetMax = $"{40} {0}"
                    }
                }
            });
            var type = isentity ? "setskinent" : "setskin";
            cont.Add(new CuiButton()
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = $"{-30} {-30}",
                    OffsetMax = $"{30} {30}"
                },
                Button =
                {
                    Command = $"uiskinmenu {type} {itemId} {skinItem.A.Key}",
                    Color = "0 0 0 0"
                },
                Text = { Text = "" }
            }, Layer + "-Search" + ".Player" + skinItem.B);

            if (weapons!._izvSkins.ContainsKey(weapon) &&
                weapons._izvSkins[weapon].ContainsKey(skinItem.A.Key))
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{1} {-18}",
                        OffsetMax = $"{18} {-1}"
                    },
                    Button =
                    {
                        Command = $"uiskinmenu setizbranoe {weapon} {skinItem.A.Key} {page} {itemId}",
                        Color = HexToRustFormat("#dead39"),
                        Sprite = "assets/icons/favourite_servers.png",
                    },
                    Text = { Text = "" }
                }, Layer + "-Search" + ".Player" + skinItem.B);
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{1} {-18}",
                        OffsetMax = $"{18} {-1}"
                    },
                    Button =
                    {
                        Command = $"uiskinmenu setizbranoe {weapon} {skinItem.A.Key} {page} {itemId}",
                        Color = "1 1 1 1",
                        Sprite = "assets/icons/favourite_servers.png",
                    },
                    Text = { Text = "" }
                }, Layer + "-Search" + ".Player" + skinItem.B);
            }

            if (!permission.UserHasPermission(player.UserIDString, Cfg.canusedefault)) continue;
            if (_playerData.TryGetValue(player.userID, out weapons) && weapons._defaultSkins.ContainsKey(weapon) &&
                weapons._defaultSkins[weapon].skinId == skinItem.A.Key)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{-16} {-16}",
                        OffsetMax = $"{-5} {-5}"
                    },
                    Button =
                    {
                        Command = $"uiskinmenu setdefault {weapon} {skinItem.A.Key} {page} {itemId}",
                        Color = HexToRustFormat("#369f47"),
                        Sprite = "assets/icons/power.png",
                    },
                    Text = { Text = "" }
                }, Layer + "-Search" + ".Player" + skinItem.B);
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{-16} {-16}",
                        OffsetMax = $"{-5} {-5}"
                    },
                    Button =
                    {
                        Command = $"uiskinmenu setdefault {weapon} {skinItem.A.Key} {page} {itemId}",
                        Color = HexToRustFormat("#fb4f3b"),
                        Sprite = "assets/icons/power.png",
                    },
                    Text = { Text = "" }
                }, Layer + "-Search" + ".Player" + skinItem.B);
            }
        }


        CuiHelper.AddUi(player, cont);
    }

    #endregion

    #region Hooks

    private bool ChangeSkin(BasePlayer player)
    {
        if (player == null || player.IsDead()) return false;
        if (Cfg.Market.costChange <= 0) return true;
        switch (Cfg.Market.WhoMarket)
        {
            case "Scrap":
            {
                List<Item> allItems = Facepunch.Pool.GetList<Item>();
                player.inventory.GetAllItems(allItems);
                player.inventory.AddBackpackContentsToList(allItems);
                var scrap = allItems.FirstOrDefault(p =>
                    p.info.shortname == "scrap" && p.skin == 0 && p.amount >= Cfg.Market.costChange);
                                player.inventory.AddBackpackContentsToList(allItems);
                Facepunch.Pool.Free<Item>(ref allItems, false);
                if (scrap == null)
                {
                    SendReply(player, lang.GetMessage("NEEDMORESCRAP", this, player.UserIDString));
                    return false;
                }

                if (scrap.amount == Cfg.Market.costChange)
                {
                    scrap.RemoveFromContainer();
                    scrap.RemoveFromWorld();
                }
                else
                {
                    scrap.amount = (int)(scrap.amount - Cfg.Market.costChange);
                    scrap.MarkDirty();
                }

                return true;
            }
            case "ServerRewards":
            {
                var checkPoint = ServerRewards?.Call("CheckPoints", player.userID);
                if (checkPoint == null)
                {
                    return false;
                }

                if ((int)checkPoint < Cfg.Market.costChange)
                {
                    SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                    return false;
                }
                else
                {
                    ServerRewards?.Call("TakePoints", player.userID, Cfg.Market.costChange);
                }

                return true;
            }
            case "Economics":
            {
                double checkPoint = (double)Economics?.Call("Balance", player.userID)!;

                if (checkPoint <= 0)
                {
                    SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                    return false;
                }

                if (checkPoint < Cfg.Market.costChange)
                {
                    SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                    return false;
                }
                else
                {
                    Puts(checkPoint.ToString());
                    Economics?.Call("Withdraw", player.userID, (double)Cfg.Market.costChange);
                }

                return true;
            }
            default:
                return false;
        }
    }

    private bool OpenPay(BasePlayer player)
    {
        if (player == null || player.IsDead()) return false;
        if (Cfg.Market.costOpen <= 0) return true;
        switch (Cfg.Market.WhoMarket)
        {
            case "Scrap":
            {
                List<Item> allItems = Facepunch.Pool.GetList<Item>();
                player.inventory.GetAllItems(allItems);
                player.inventory.AddBackpackContentsToList(allItems);
                var scrap = allItems.FirstOrDefault(p =>
                    p.info.shortname == "scrap" && p.skin == 0 && p.amount >= Cfg.Market.costOpen);
                
                Facepunch.Pool.Free<Item>(ref allItems, false);
                if (scrap == null)
                {
                    SendReply(player, lang.GetMessage("NEEDMORESCRAP", this, player.UserIDString));
                    return false;
                }

                if (scrap.amount == Cfg.Market.costOpen)
                {
                    scrap.RemoveFromContainer();
                    scrap.RemoveFromWorld();
                }
                else
                {
                    scrap.amount = (int)(scrap.amount - Cfg.Market.costOpen);
                    scrap.MarkDirty();
                }

                return true;
            }
            case "ServerRewards":
            {
                var checkPoint = ServerRewards?.Call("CheckPoints", player.userID);
                if (checkPoint == null)
                {
                    SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                    return false;
                }

                if ((int)checkPoint < Cfg.Market.costOpen)
                {
                    SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                    return false;
                }
                else
                {
                    ServerRewards?.Call("TakePoints", player.userID, Cfg.Market.costOpen);
                }

                return true;
            }
            case "Economics":
            {
                double checkPoint = (double)Economics?.Call("Balance", player.userID)!;
                if (checkPoint <= 0)
                {
                    SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                    return false;
                }

                if (checkPoint < Cfg.Market.costOpen)
                {
                    SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                    return false;
                }
                else
                {
                    Economics?.Call("Withdraw", player.userID, (double)Cfg.Market.costOpen);
                }

                return true;
            }
            default:
                return false;
        }
    }

    private void OnItemAddedToContainer(ItemContainer container, Item item)
    {
        if (container == null || item == null) return;
        if(Cfg == null) return;
        if(permission == null) return;
        string shortname = item.info.shortname;
        if (item.info.shortname.Contains("hazmatsuit"))
            shortname = "hazmatsuit";
        if (!_loadData.ContainsKey(shortname)) return;
        if (item.skin != 0 && !Cfg.SkinAuto) return;
        if (item.skin != 0 && Cfg.blackList.Contains(item.skin)) return;
        var player = item.GetOwnerPlayer();
        if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;
        if (!permission.UserHasPermission(player.UserIDString, Cfg.canusedefault)) return;
        if (container != player.inventory.containerMain && container != player.inventory.containerBelt &&
            container != player.inventory.containerWear) return;
        if (!_playerData.TryGetValue(player.userID, out var playerData)) return;
        if (!playerData._defaultSkins.TryGetValue(shortname, out var skinData)) return;
        if (item.skin == skinData.skinId) return;
        if (shortname.Contains("hazmatsuit"))
        {
            if (item.info.shortname == _hazmats[skinData.skinId]) return;
            var pos = item.position;
            var uids = item.uid;
            var amount = item.amount;
            item.DoRemove();
            item = ItemManager.CreateByName($"{_hazmats[skinData.skinId]}");
            item.uid = uids;
            item.amount = amount;
            item.MoveToContainer(container, pos);
            item.MarkDirty();
            player.SendNetworkUpdate();
            return;
        }

        item.skin = skinData.skinId;
        var held = item.GetHeldEntity();
        if (held == null) return;
        held.skinID = skinData.skinId;
        held.SendNetworkUpdate();
    }

    private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter craft)
    {
        if (craft == null || craft.owner == null) return;
        string shortname = item.info.shortname;
        if (item.info.shortname.Contains("hazmatsuit"))
            shortname = "hazmatsuit";
        if (task.skinID != 0 && !Cfg.SkinAuto) return;
        if (task.skinID != 0 && Cfg.blackList.Contains(ulong.Parse(task.skinID.ToString()))) return;
        if (!permission.UserHasPermission(craft.owner.UserIDString, Cfg.canusedefault)) return;
        if (!this._playerData.TryGetValue(craft.owner.userID, out var playerData)) return;
        if (!playerData._defaultSkins.TryGetValue(shortname, out var skinData)) return;
        if (item.skin == skinData.skinId) return;
        if (shortname.Contains("hazmatsuit"))
        {
            if (item.info.shortname == _hazmats[skinData.skinId]) return;
            var pos = item.position;
            var uids = item.uid;
            var amount = item.amount;
            item.DoRemove();
            item = ItemManager.CreateByName($"{_hazmats[skinData.skinId]}");
            item.uid = uids;
            item.amount = amount;
            craft.owner.GiveItem(item);
            item.MarkDirty();
            return;
        }

        item.skin = skinData.skinId;
        var held = item.GetHeldEntity();
        if (held == null) return;
        held.skinID = skinData.skinId;
        held.SendNetworkUpdate();
    }

    private object OnItemAction(Item item, string action, BasePlayer player)
    {
        if (_openSkins.ContainsKey(player.userID)) return false;
        return null;
    }

    private string _apiKey = "LSKINS-fch42341sad85wkasxksqq";

    private Dictionary<string, Dictionary<ulong, ItemData.SkinInfo>> _loadData =
        new Dictionary<string, Dictionary<ulong, ItemData.SkinInfo>>();

    private void LoadDataSkins()
    {
        foreach (var cfgWeaponSkin in Interface.Oxide.DataFileSystem.GetFiles("LSkins/Skins/"))
        {
            var text = cfgWeaponSkin.Remove(0, cfgWeaponSkin.IndexOf("/Skins/", StringComparison.Ordinal) + 7);
            var text2 = text.Remove(text.IndexOf(".json", StringComparison.Ordinal), text.Length - text.IndexOf(".json", StringComparison.Ordinal));
            var skins =
                Interface.Oxide.DataFileSystem
                    .ReadObject<Dictionary<ulong, ItemData.SkinInfo>>($"LSkins/Skins/{text2}");
            _loadData.Add(text2, skins);
        }
    }

    private void LoadSteamApproved()
    {
        foreach (var inventoryDef in Steamworks.SteamInventory.Definitions)
        {
            string shortname = inventoryDef.GetProperty("itemshortname");
            ulong workshopid;

            if (string.IsNullOrEmpty(shortname))
                continue;

            if (workshopNameToShortname.TryGetValue(shortname, out var value))
                shortname = value;

            if (inventoryDef.Id < 100)
                continue;

            if (!ulong.TryParse(inventoryDef.GetProperty("workshopid"), out workshopid))
                continue;
            
            if (_loadData.ContainsKey(shortname) &&
                _loadData[shortname].ContainsKey(workshopid)) continue;
            if (_loadData.ContainsKey(shortname))
            {
                _loadData[shortname].Add(workshopid, new ItemData.SkinInfo()
                {
                    IsApproved = true,
                    MarketName = 
                        inventoryDef.Name
                });
                Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{shortname}", _loadData[shortname]);
            }
            else
            {
                _loadData.Add(shortname, new Dictionary<ulong, ItemData.SkinInfo>()
                {
                    [workshopid] = new ItemData.SkinInfo()
                    {
                        IsApproved = true,
                        MarketName = inventoryDef.Name
                    }
                });
                Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{shortname}", _loadData[shortname]);
            }
            _skinIdsRust.Add($"{shortname}_{workshopid}");
        }
    }
    private readonly Dictionary<string, string> workshopNameToShortname = new Dictionary<string, string>
    {
        {"longtshirt", "tshirt.long" },
        {"cap", "hat.cap" },
        {"beenie", "hat.beenie" },
        {"boonie", "hat.boonie" },
        {"balaclava", "mask.balaclava" },
        {"pipeshotgun", "shotgun.waterpipe" },
        {"woodstorage", "box.wooden" },
        {"ak47", "rifle.ak" },
        {"bearrug", "rug.bear" },
        {"boltrifle", "rifle.bolt" },
        {"bandana", "mask.bandana" },
        {"hideshirt", "attire.hide.vest" },
        {"snowjacket", "jacket.snow" },
        {"buckethat", "bucket.helmet" },
        {"semiautopistol", "pistol.semiauto" },
        {"burlapgloves", "burlap.gloves" },
        {"roadsignvest", "roadsign.jacket" },
        {"roadsignpants", "roadsign.kilt" },
        {"burlappants", "burlap.trousers" },
        {"collaredshirt", "shirt.collared" },
        {"mp5", "smg.mp5" },
        {"sword", "salvaged.sword" },
        {"workboots", "shoes.boots" },
        {"vagabondjacket", "jacket" },
        {"hideshoes", "attire.hide.boots" },
        {"deerskullmask", "deer.skull.mask" },
        {"minerhat", "hat.miner" },
        {"lr300", "rifle.lr300" },
        {"lr300.item", "rifle.lr300" },
        {"burlap.gloves", "burlap.gloves.new"},
        {"leather.gloves", "burlap.gloves"},
        {"python", "pistol.python" },
        {"m39", "rifle.m39"},
        {"woodendoubledoor", "door.double.hinged.wood"}
    };
    private void LoadApprovedSkin()
    {
        var i = 0;
        foreach (var approvedSkinInfo in Rust.Workshop.Approved.All)
        {
            if (approvedSkinInfo.Value == null || approvedSkinInfo.Value.Skinnable == null ||
                approvedSkinInfo.Value.Marketable == false) continue;
            var item = approvedSkinInfo.Value.Skinnable.ItemName;
            if (item.Contains("lr300")) item = "rifle.lr300";
            if (_loadData.ContainsKey(item) &&
                _loadData[item].ContainsKey(approvedSkinInfo.Value.WorkshopdId)) continue;
            if (_loadData.ContainsKey(item))
            {
                _loadData[item].Add(approvedSkinInfo.Value.WorkshopdId, new ItemData.SkinInfo()
                {
                    IsApproved = true,
                    MarketName = approvedSkinInfo.Value.Name
                });
                Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{item}", _loadData[item]);
            }
            else
            {
                _loadData.Add(item, new Dictionary<ulong, ItemData.SkinInfo>()
                {
                    [approvedSkinInfo.Value.WorkshopdId] = new ItemData.SkinInfo()
                    {
                        IsApproved = true,
                        MarketName = approvedSkinInfo.Value.Name
                    }
                });
                Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{item}", _loadData[item]);
            }

            i++;
            if (_skinIdsRust.Exists(p => p.Contains($"{item}_{approvedSkinInfo.Value.WorkshopdId}"))) continue;
            _skinIdsRust.Add($"{item}_{approvedSkinInfo.Value.WorkshopdId}");
        }
    }

    private WebRequests _requests;

    private Dictionary<ulong, string> _hazmats = new Dictionary<ulong, string>()
    {
        [0] = "hazmatsuit",
        [1] = "hazmatsuit.spacesuit",
        [2] = "hazmatsuit.nomadsuit",
        [3] = "hazmatsuit.arcticsuit",
        [4] = "hazmatsuit.lumberjack",
        [5] = "hazmatsuit.diver"
    };

    private void OnServerInitialized()
    {
        if (!ImageLibrary)
        {
            PrintError("To start, you need to install ImageLibrary!!!!!!!!!!!!!!!!");
            Interface.Oxide.UnloadPlugin("LSkins");
            return;
        }

        _requests = new WebRequests();
        AddCovalenceCommand(Cfg.Commands.ToArray(), nameof(CommandSkins));
        AddCovalenceCommand(Cfg.EntCommands.ToArray(), nameof(CommandEntity));
        UpdateWorkshopShortName();
        ServerConsole.PrintColoured(ConsoleColor.Blue, (object)$"{Name} [{Version}] ", (object)ConsoleColor.Blue,
            (object)"B", (object)ConsoleColor.Cyan, (object)"Y ", (object)ConsoleColor.Green, (object)"L",
            (object)ConsoleColor.Magenta, (object)"A", (object)ConsoleColor.Red, (object)"G",
            (object)ConsoleColor.Yellow, (object)"Z", (object)ConsoleColor.Cyan, (object)"Y",
            (object)ConsoleColor.DarkCyan, (object)"A");
        permission.RegisterPermission(Cfg.canuse, this);
        permission.RegisterPermission(Cfg.canusedefault, this);
        permission.RegisterPermission(Cfg.canuseapprove, this);
        permission.RegisterPermission("lskins.admin", this);
        foreach (var keyValuePair in Cfg.useSkins)
        {
            permission.RegisterPermission(keyValuePair.Key, this);
        }

        AddImage($"https://rustapi.top/cartinki/givecart.php?token={_apiKey}&image=grid.png", $"LSKINGRID({Version})");
        _playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DataPlayer>>("LSkins/PlayerData");
        if (!Interface.Oxide.DataFileSystem.ExistsDatafile("LSkins/Skins/rifle.lr300"))
        {
            var newData = new Dictionary<ulong, ItemData.SkinInfo>();
            newData.Add(0, new ItemData.SkinInfo()
            {
                IsApproved = true,
                MarketName = ItemManager.FindItemDefinition("rifle.lr300").displayName.english
            });
            Interface.Oxide.DataFileSystem.WriteObject("LSkins/Skins/rifle.lr300", newData);
        }

        LoadDataSkins();
        foreach (var basePlayer in BasePlayer.activePlayerList)
        {
            OnPlayerConnected(basePlayer);
        }

        if (Cfg.IsApproved)
        {
            timer.Once(0.5f, () =>
            {
                LoadSkinDefines();
                LoadApprovedSkin();
                LoadSteamApproved();
                Puts($"Load skins: {_skinIdsRust.Count}");
            });
        }

        if (Cfg.IsHazmat)
        {
            var count = 0;
            var newData = new Dictionary<ulong, ItemData.SkinInfo>();
            if (!_loadData.TryGetValue("hazmatsuit", out var value))
            {
                newData.Add(0, new ItemData.SkinInfo()
                {
                    IsApproved = true,
                    MarketName = ItemManager.FindItemDefinition("hazmatsuit").displayName.english
                });
                newData.Add(1, new ItemData.SkinInfo()
                {
                    IsApproved = true,
                    MarketName = ItemManager.FindItemDefinition("hazmatsuit.spacesuit").displayName.english
                });
                newData.Add(2, new ItemData.SkinInfo()
                {
                    IsApproved = true,
                    MarketName = ItemManager.FindItemDefinition("hazmatsuit.nomadsuit").displayName.english
                });
                newData.Add(3, new ItemData.SkinInfo()
                {
                    IsApproved = true,
                    MarketName = ItemManager.FindItemDefinition("hazmatsuit.arcticsuit").displayName.english
                });
                count = 5;
                newData.Add(4, new ItemData.SkinInfo()
                {
                    IsApproved = true,
                    MarketName = ItemManager.FindItemDefinition("hazmatsuit.lumberjack").displayName.english
                });
                count = 6;
                newData.Add(5, new ItemData.SkinInfo()
                {
                    IsApproved = true,
                    MarketName = ItemManager.FindItemDefinition("hazmatsuit.diver").displayName.english
                });
                _loadData.Add("hazmatsuit", newData);
            }
            else
            {
                newData = value;
                if (!newData.ContainsKey(0))
                {
                    newData.Add(0, new ItemData.SkinInfo()
                    {
                        IsApproved = true,
                        MarketName = ItemManager.FindItemDefinition("hazmatsuit").displayName.english
                    });
                    count++;
                }

                if (!newData.ContainsKey(1))
                {
                    newData.Add(1, new ItemData.SkinInfo()
                    {
                        IsApproved = true,
                        MarketName = ItemManager.FindItemDefinition("hazmatsuit.spacesuit").displayName.english
                    });
                    count++;
                }

                if (!newData.ContainsKey(2))
                {
                    newData.Add(2, new ItemData.SkinInfo()
                    {
                        IsApproved = true,
                        MarketName = ItemManager.FindItemDefinition("hazmatsuit.nomadsuit").displayName.english
                    });
                    count++;
                }

                if (!newData.ContainsKey(3))
                {
                    newData.Add(3, new ItemData.SkinInfo()
                    {
                        IsApproved = true,
                        MarketName = ItemManager.FindItemDefinition("hazmatsuit.arcticsuit").displayName.english
                    });
                    count++;
                }

                if (!newData.ContainsKey(4))
                {
                    newData.Add(4, new ItemData.SkinInfo()
                    {
                        IsApproved = true,
                        MarketName = ItemManager.FindItemDefinition("hazmatsuit.lumberjack").displayName.english
                    });
                    count++;
                }

                if (!newData.ContainsKey(5))
                {
                    newData.Add(5, new ItemData.SkinInfo()
                    {
                        IsApproved = true,
                        MarketName = ItemManager.FindItemDefinition("hazmatsuit.diver").displayName.english
                    });

                    count++;
                }
            }

            Interface.Oxide.DataFileSystem.WriteObject("LSkins/Skins/hazmatsuit", newData);
           
            if (count > 0) Puts($"Add Hazmats: " + count);
        }
    }

    private void LoadSkinDefines()
    {
        var items = ItemManager.GetItemDefinitions();
        foreach (var itemDefinition in items.Where(itemDefinition =>
                     itemDefinition != null && itemDefinition.skins2 != null && itemDefinition.skins2.Length != 0))
        {
            if (!_loadData.ContainsKey(itemDefinition.shortname) || !_loadData[itemDefinition.shortname].ContainsKey(0))
            {
                if (_loadData.ContainsKey(itemDefinition.shortname))
                {
                    _loadData[itemDefinition.shortname].Add(0,
                        new ItemData.SkinInfo()
                        {
                            IsApproved = true,
                            MarketName = itemDefinition.displayName.english
                        });
                    Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{itemDefinition.shortname}",
                        _loadData[itemDefinition.shortname]);
                }
                else
                {
                    _loadData.Add(itemDefinition.shortname, new Dictionary<ulong, ItemData.SkinInfo>()
                    {
                        [0] = new ItemData.SkinInfo()
                        {
                            IsApproved = true,
                            MarketName = itemDefinition.displayName.english
                        }
                    });
                    Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{itemDefinition.shortname}",
                        _loadData[itemDefinition.shortname]);
                }

                _skinIdsRust.Add($"{itemDefinition.shortname}_0");
            }

            foreach (var playerItemDefinition in itemDefinition.skins2)
            {
                if (playerItemDefinition == null || itemDefinition.shortname == null) continue;
                var skinId = playerItemDefinition.WorkshopDownload == 0
                    ? ulong.Parse(playerItemDefinition.DefinitionId.ToString())
                    : playerItemDefinition.WorkshopDownload;
                if (_loadData.ContainsKey(itemDefinition.shortname) &&
                    _loadData[itemDefinition.shortname].ContainsKey(skinId)) continue;
                if (_loadData.ContainsKey(itemDefinition.shortname))
                {
                    _loadData[itemDefinition.shortname].Add(skinId,
                        new ItemData.SkinInfo()
                        {
                            IsApproved = true,
                            MarketName = playerItemDefinition.Name
                        });
                    Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{itemDefinition.shortname}",
                        _loadData[itemDefinition.shortname]);
                }
                else
                {
                    _loadData.Add(itemDefinition.shortname, new Dictionary<ulong, ItemData.SkinInfo>()
                    {
                        [skinId] = new ItemData.SkinInfo()
                        {
                            IsApproved = true,
                            MarketName = playerItemDefinition.Name
                        }
                    });
                    Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{itemDefinition.shortname}",
                        _loadData[itemDefinition.shortname]);
                }

                _skinIdsRust.Add(itemDefinition.shortname + "_" + skinId);
            }
        }
    }

    private void OnServerSave()
    {
        Interface.Oxide.DataFileSystem.WriteObject("LSkins/PlayerData", _playerData);
    }

    private void Unload()
    {
        Interface.Oxide.DataFileSystem.WriteObject("LSkins/PlayerData", _playerData);
        foreach (var keyValuePair in _loadData)
        {
            Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{keyValuePair.Key}", keyValuePair.Value);
        }

        foreach (var basePlayer in BasePlayer.activePlayerList.Where(p => _openSkins.ContainsKey(p.userID)))
        {
            basePlayer.EndLooting();
            CuiHelper.DestroyUi(basePlayer, Layer);
            CuiHelper.DestroyUi(basePlayer, Layer + "-Wear");
            CuiHelper.DestroyUi(basePlayer, Layer + "-Main");
            CuiHelper.DestroyUi(basePlayer, Layer + "-Belt");
            CuiHelper.DestroyUi(basePlayer, Layer + "-Izbranoe");
            CuiHelper.DestroyUi(basePlayer, Layer + "LableIzb");
        }
    }

    #region ConsoleCommand

    void AddSkins(BasePlayer player, ulong skinId)
    {
        string weapon;
        string name;
        _requests.Enqueue($"https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
            $"itemcount=1&publishedfileids[0]={skinId}",
            (code, res) =>
            {
                PublishedFileQueryResponse query =
                    JsonConvert.DeserializeObject<PublishedFileQueryResponse>(res, _errorHandling);
                if (query?.response == null ||
                    query.response.publishedfiledetails.Length <= 0) return;
                foreach (var publishedFileQueryDetail in query.response.publishedfiledetails)
                {
                    foreach (var tag in publishedFileQueryDetail.tags)
                    {
                        string adjTag = tag.tag.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "")
                            .Replace(".item", "");

                        if (!_workshopsnames.TryGetValue(adjTag, out var workshopsname)) continue;
                        weapon = workshopsname;
                        name = publishedFileQueryDetail.title;
                        if (!_loadData.ContainsKey(weapon))
                        {
                            _loadData.Add(weapon, new Dictionary<ulong, ItemData.SkinInfo>()
                            {
                                [0] = new ItemData.SkinInfo()
                                {
                                    IsApproved = false,
                                    IsEnabled = true,
                                    MarketName = ItemManager.FindItemDefinition(weapon).displayName.english
                                },
                                [skinId] = new ItemData.SkinInfo()
                                {
                                    IsApproved = false,
                                    IsEnabled = true,
                                    MarketName = name
                                },
                            });

                            PrintWarning($"Add skin(LSkins/Skins/{weapon}.json): {skinId}");
                            if (player != null)
                                PrintToConsole(player,
                                    $"[LSkins] Add skin(Open the file and edit:LSkins/Skins/{weapon}.json): {skinId}");
                            Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{weapon}", _loadData[weapon]);
                        }
                        else if (!_loadData[weapon].ContainsKey(skinId))
                        {
                            _loadData[weapon].Add(skinId, new ItemData.SkinInfo()
                            {
                                IsApproved = false,
                                IsEnabled = true,
                                MarketName = name
                            });
                            PrintWarning($"Add skin(LSkins/Skins/{weapon}.json): {skinId}");
                            if (player != null)
                                PrintToConsole(player,
                                    $"[LSkins] Add skin(Open the file and edit:LSkins/Skins/{weapon}.json): {skinId}");
                            Interface.Oxide.DataFileSystem.WriteObject($"LSkins/Skins/{weapon}", _loadData[weapon]);
                        }

                        return;
                    }
                }
            }, this, RequestMethod.POST);
    }

    private readonly JsonSerializerSettings _errorHandling = new JsonSerializerSettings
        { Error = (se, ev) => { ev.ErrorContext.Handled = true; } };

    [ConsoleCommand("lskins")]
    private void SkinsConsole(ConsoleSystem.Arg arg)
    {
        if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, "lskins.admin")) return;
        if (arg.Args == null || arg.Args.Length < 2) return;
        var type = arg.Args[0];
        if (!ulong.TryParse(arg.Args[1], out var skinId)) return;
        switch (type)
        {
            case "add":
                Puts($"Try add {skinId}");
                AddSkins(arg.Player(), skinId);
                break;
            case "addcollection":
                _requests.Enqueue($"https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/",
                    $"&collectioncount=1&publishedfileids[0]={skinId}",
                    (code, res) =>
                    {
                        CollectionQueryResponse collectionQuery =
                            JsonConvert.DeserializeObject<CollectionQueryResponse>(res);
                        foreach (var responseCollectiondetail in collectionQuery.response.collectiondetails)
                        {
                            foreach (var responseCollectiondetailChild in responseCollectiondetail.children)
                            {
                                rust.RunServerCommand($"lskins add {responseCollectiondetailChild.publishedfileid}");
                            }
                        }
                    }, this, RequestMethod.POST);
                break;
            case "remove":
                if (arg.Args.Length < 2)
                {
                    PrintWarning($"lskins {type} {skinId}");
                    if (arg.Player() != null) PrintToConsole(arg.Player(), $"lskins {type} {skinId}");
                    return;
                }

                var findSkin = _loadData.FirstOrDefault(p => p.Value.ContainsKey(skinId));
                if (findSkin.Value != null)
                {
                    _loadData[findSkin.Key].Remove(skinId);
                    PrintWarning($"Remove skin: {findSkin.Key}_{skinId}");
                    if (arg.Player() != null)
                        PrintToConsole(arg.Player(), $"[LSkins] Remove skin: {findSkin.Key}_{skinId}");
                }

                break;
        }
    }

    #endregion

    private void CommandSkins(IPlayer user, string command, string[] args)
    {
        StartUI(user.Object as BasePlayer, command, args);
    }

    private void CommandEntity(IPlayer user, string command, string[] args)
    {
        var player = user.Object as BasePlayer;
        RaycastHit hit;
        if (!Physics.Raycast(player!.eyes.HeadRay(), out hit, 5) || hit.GetEntity() == null ||
            !(hit.GetEntity() is BaseCombatEntity))
        {
            SendReply(player, lang.GetMessage("ENTITYNOTFOUND", this, player.UserIDString));
            return;
        }

        var entity = hit.GetEntity() as BaseCombatEntity;
        if (entity == null || !entity.pickup.enabled || !player.CanBuild() || entity.pickup.itemTarget == null)
        {
            SendReply(player, lang.GetMessage("ENTITYNOTFOUND", this, player.UserIDString));
            return;
        }

        if (!_loadData.ContainsKey(entity.pickup.itemTarget.shortname))
        {
            SendReply(player, lang.GetMessage("SKINNOTFOUND", this, player.UserIDString));
            return;
        }

        if (!Cfg.Market.IsEnabled)
        {
            if (!permission.UserHasPermission(player.UserIDString, Cfg.canuse))
            {
                SendReply(player, lang.GetMessage("NOPERMUSE", this, player.UserIDString));
                return;
            }
        }
        else
        {
            if (!permission.UserHasPermission(player.UserIDString, Cfg.canuse))
            {
                if (!OpenPay(player)) return;
            }
        }

        CuiHelper.DestroyUi(player, Layer);
        player.EndLooting();
        if (_openSkins.ContainsKey(player.userID)) return;
        timer.Once(0.5f, () =>
        {
            StartLoot(player);
            LoadSkinsUI(player, entity.pickup.itemTarget.shortname, 1, hit.GetEntity().net.ID.Value, true);
            LoadIzbranoeUI(player, entity.pickup.itemTarget.shortname, hit.GetEntity().net.ID.Value, 1, true);
        });
    }

    [ChatCommand("skin")]
    private void StartUI(BasePlayer player, string command, string[] args)
    {
        if (!Cfg.Market.IsEnabled)
        {
            if (!permission.UserHasPermission(player.UserIDString, Cfg.canuse))
            {
                SendReply(player, lang.GetMessage("NOPERMUSE", this, player.UserIDString));
                return;
            }
        }
        else
        {
            if (!permission.UserHasPermission(player.UserIDString, Cfg.canuse))
            {
                if (!OpenPay(player)) return;
            }
        }

        CuiHelper.DestroyUi(player, Layer);
        player.EndLooting();
        if (_openSkins.ContainsKey(player.userID)) return;
        timer.Once(0.5f, () => { StartLoot(player); });
    }

    private object CanLootPlayer(BasePlayer looted, BasePlayer looter)
    {
        if (looter == null) return null;
        if (_openSkins.ContainsKey(looter.userID))
        {
            return true;
        }

        return null;
    }

    private void StartLoot(BasePlayer player)
    {
        if (_openSkins.ContainsKey(player.userID)) return;
        ItemContainer container = new ItemContainer();
        container.entityOwner = player;
        container.isServer = true;
        container.allowedContents = ItemContainer.ContentsType.Generic;
        container.GiveUID();
        _openSkins.Add(player.userID, container);
        container.capacity = 12;
        container.canAcceptItem = (item, i) => false;
        container.playerOwner = player;
        PlayerLootContainer(player, container);
        StartInterface(player);
    }

    private void StartInterface(BasePlayer player)
    {
        if (!_playerData.TryGetValue(player.userID, out DataPlayer dataPlayer)) return;
        CuiHelper.DestroyUi(player, Layer);
        CuiHelper.DestroyUi(player, Layer + "-Izbranoe");
        CuiHelper.DestroyUi(player, Layer + "LableIzb");
        var cont = new CuiElementContainer();
        CuiPanel fon = new CuiPanel()
        {
            RectTransform =
                { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{-225} {-325}", OffsetMax = $"{615} {-1}" },
            CursorEnabled = true,
            Image = { Color = "0 0 0 0" }
        };
        cont.Add(fon, "Overlay", Layer);
        cont.Add(new CuiPanel()
        {
            Image =
            {
                Color = HexToRustFormat(Cfg.Interface.mainColor),
                Material = "assets/content/ui/ui.background.tile.psd",
                ImageType = Image.Type.Tiled
            },
            RectTransform =
            {
                AnchorMin = "0 1",
                AnchorMax = "0 1",
                OffsetMin = $"{7} {-47}",
                OffsetMax = $"{427} {5}"
            }
        }, Layer, Layer + "Lable");
        cont.Add(new CuiElement()
        {
            Parent = Layer + "Lable",
            Components =
            {
                new CuiTextComponent()
                {
                    Text = lang.GetMessage("LABLEMENUTEXT", this, player.UserIDString),
                    Align = TextAnchor.MiddleLeft,
                    FontSize = (int)(24),
                    Color = HexToRustFormat("#beb5ad")
                },
                new CuiRectTransformComponent()
                {
                    AnchorMin = "0 0.5",
                    AnchorMax = "0 0.5",
                    OffsetMin = $"{10} {-30}",
                    OffsetMax = $"{427} {25}"
                }
            }
        });
        cont.Add(new CuiElement()
        {
            Parent = Layer,
            Name = Layer + "-Search",
            Components =
            {
                new CuiTextComponent()
                {
                    Text = lang.GetMessage("INFOCLICKSKIN", this, player.UserIDString),
                    Color = "1 1 1 1",
                    FontSize = (int)(12),
                    Align = TextAnchor.MiddleCenter
                },
                new CuiRectTransformComponent()
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = $"{-400} {-185}",
                    OffsetMax = $"{-35} {115}"
                }
            }
        });
        CuiButton destroyUi = new CuiButton()
        {
            RectTransform =
                { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = $"{5} {-15}", OffsetMax = $"{35} {15}" },
            Button = { Color = "0.75 0.75 0.75 0.65", Command = "uiskinmenu close", Sprite = "assets/icons/close.png" },
            Text = { Text = "", Align = TextAnchor.MiddleCenter }
        };
        cont.Add(destroyUi, Layer + "Lable");
        cont.Add(new CuiPanel()
        {
            Image =
            {
                Color = HexToRustFormat(Cfg.Interface.favcolor),
                Material = "assets/content/ui/ui.background.tile.psd",
                ImageType = Image.Type.Tiled
            },
            RectTransform =
            {
                AnchorMin = "0.5 0",
                AnchorMax = "0.5 0",
                OffsetMin = $"{190} {235}",
                OffsetMax = $"{575} {290}"
            },
        }, "Overlay", Layer + "LableIzb");
        cont.Add(new CuiElement()
        {
            Parent = Layer + "LableIzb",
            Components =
            {
                new CuiTextComponent()
                {
                    Text = lang.GetMessage("FAVSKINSLABLE", this, player.UserIDString),
                    Align = TextAnchor.MiddleCenter,
                    FontSize = (int)(24),
                    Color = HexToRustFormat("#beb5ac")
                },
                new CuiRectTransformComponent()
                {
                    AnchorMin = "0 0.5",
                    AnchorMax = "0 0.5",
                    OffsetMin = $"{20} {-25}",
                    OffsetMax = $"{350} {25}"
                }
            }
        });

        cont.Add(new CuiPanel()
        {
            Image =
            {
                Color = "0 0 0 0"
            },
            RectTransform =
            {
                AnchorMin = "0.5 0",
                AnchorMax = "0.5 0",
                OffsetMin = $"{201} {111}",
                OffsetMax = $"{571} {231}"
            }
        }, "Overlay", Layer + "-Izbranoe");
        CuiHelper.AddUi(player, cont);
        LoadWearUI(player);
        LoadMainUI(player);
        LoadBeltUI(player);
    }

    private Dictionary<ulong, ItemContainer> _openSkins = new Dictionary<ulong, ItemContainer>();

    private static void PlayerLootContainer(BasePlayer player, ItemContainer container)
    {
        player.inventory.loot.Clear();
        player.inventory.loot.PositionChecks = false;
        player.inventory.loot.entitySource = container.entityOwner ?? player;
        player.inventory.loot.itemSource = null;
        player.inventory.loot.MarkDirty();
        player.inventory.loot.AddContainer(container);
        player.inventory.loot.SendImmediate();
        player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic_resizable");
    }

    private void OnPlayerLootEnd(PlayerLoot inventory)
    {
        if (inventory == null) return;
        var player = inventory.GetComponent<BasePlayer>();
        if (player == null || !_openSkins.ContainsKey(player.userID)) return;
        var cont = _openSkins[player.userID];
        var itemc = inventory.containers.Find(p => p.uid == cont.uid);
        if (itemc == null) return;
        itemc.Clear();
        _openSkins.Remove(player.userID);
        itemc.Kill();
        CuiHelper.DestroyUi(player, Layer);
        CuiHelper.DestroyUi(player, Layer + "-Izbranoe");
        CuiHelper.DestroyUi(player, Layer + "LableIzb");
        CuiHelper.DestroyUi(player, Layer + "-Wear");
        CuiHelper.DestroyUi(player, Layer + "-Main");
        CuiHelper.DestroyUi(player, Layer + "-Belt");
    }


    private void OnPlayerConnected(BasePlayer player)
    {
        if (!_playerData.TryGetValue(player.userID, out DataPlayer playerDatas))
            _playerData.Add(player.userID, new DataPlayer());
    }

    #endregion

    #region LoadSkins
    
    private List<string> _skinIdsRust = new List<string>();

    #endregion

    #region lang

    protected override void LoadDefaultMessages()
    {
        var ru = new Dictionary<string, string>()
        {
            ["NOPERMUSE"] = "У вас нет прав на использование команды!",
            ["NEEDMORESCRAP"] = "Не достаточно скрапа в инвентаре!",
            ["NEEDMOREMONEY"] = "Не достаточно средств на счету.",
            ["LABLEMENUTEXT"] = "МЕНЮ СКИНОВ",
            ["LABLESEARCH"] = "Поиск... НАЖМИТЕ ENTER",
            ["INFOCLICKSKIN"] = "Нажмите на предмет,\nна который нужно установить скин",
            ["FAVSKINSLABLE"] = "ИЗБРАННЫЕ СКИНЫ",
            ["ADAPTINTERFACELABLE"] = "АДАПТИРОВАТЬ\n<size=10>РАЗМЕР ИНТЕРФЕЙСА СКИНОВ</size>",
            ["SKINNOTFOUND"] = "Скины на этот предмет не найдены"
        }.ToDictionary(rus => rus.Key, rus => rus.Value);
        var en = new Dictionary<string, string>()
        {
            ["NOPERMUSE"] = "You don't have the rights to use the command!",
            ["NEEDMORESCRAP"] = "Not enough scrap in the inventory!",
            ["NEEDMOREMONEY"] = "Insufficient funds in the account.",
            ["LABLEMENUTEXT"] = "SKIN MENU",
            ["LABLESEARCH"] = "Search... PRESS ENTER",
            ["INFOCLICKSKIN"] = "Click on the item\nyou want to install the skin on",
            ["FAVSKINSLABLE"] = "FAVOURITE SKINS",
            ["ADAPTINTERFACELABLE"] = "ADAPT\n<size=10>THE SIZE OF THE SKINS INTERFACE</size>",
            ["ENTITYNOTFOUND"] = "Entity not found",
            ["SKINNOTFOUND"] = "Skins not found for this item"
        }.ToDictionary(ens => ens.Key, ens => ens.Value);
        lang.RegisterMessages(ru, this, "ru");
        lang.RegisterMessages(en, this, "en");
    }

    #endregion

    #region Help

    [PluginReference] private Plugin ImageLibrary, ServerRewards, Economics;

    public string GetImage(string shortname, ulong skin = 0) =>
        (string)ImageLibrary.Call("GetImage", shortname, skin);

    public bool AddImage(string url, string shortname, ulong skin = 0) =>
        (bool)ImageLibrary.Call("AddImage", url, shortname, skin);

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

    #endregion

    #region JSON Response Classes

    public class PublishedFileQueryResponse
    {
        public FileResponse response { get; set; }
    }

    public class FileResponse
    {
        public int result { get; set; }
        public int resultcount { get; set; }
        public PublishedFileQueryDetail[] publishedfiledetails { get; set; }
    }

    public class PublishedFileQueryDetail
    {
        public string publishedfileid { get; set; }
        public int result { get; set; }
        public string creator { get; set; }
        public int creator_app_id { get; set; }
        public int consumer_app_id { get; set; }
        public string filename { get; set; }
        public int file_size { get; set; }
        public string preview_url { get; set; }
        public string hcontent_preview { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public int time_created { get; set; }
        public int time_updated { get; set; }
        public int visibility { get; set; }
        public int banned { get; set; }
        public string ban_reason { get; set; }
        public int subscriptions { get; set; }
        public int favorited { get; set; }
        public int lifetime_subscriptions { get; set; }
        public int lifetime_favorited { get; set; }
        public int views { get; set; }
        public Tag[] tags { get; set; }

        public class Tag
        {
            public string tag { get; set; }
        }
    }

    public class CollectionQueryResponse
    {
        public CollectionResponse response { get; set; }
    }

    public class CollectionResponse
    {
        public int result { get; set; }
        public int resultcount { get; set; }
        public CollectionDetails[] collectiondetails { get; set; }
    }

    public class CollectionDetails
    {
        public string publishedfileid { get; set; }
        public int result { get; set; }
        public CollectionChild[] children { get; set; }
    }

    public class CollectionChild
    {
        public string publishedfileid { get; set; }
        public int sortorder { get; set; }
        public int filetype { get; set; }
    }

    #endregion

    #region WokshopNames

    private Dictionary<string, string> _workshopsnames = new Dictionary<string, string>()
    {
        { "ak47", "rifle.ak" },
        { "lr300", "rifle.lr300" },
        { "lr300.item", "rifle.lr300" },
        { "m39", "rifle.m39" },
        { "l96", "rifle.l96" },
        { "longtshirt", "tshirt.long" },
        { "cap", "hat.cap" },
        { "beenie", "hat.beenie" },
        { "boonie", "hat.boonie" },
        { "balaclava", "mask.balaclava" },
        { "pipeshotgun", "shotgun.waterpipe" },
        { "woodstorage", "box.wooden" },
        { "bearrug", "rug.bear" },
        { "boltrifle", "rifle.bolt" },
        { "bandana", "mask.bandana" },
        { "hideshirt", "attire.hide.vest" },
        { "snowjacket", "jacket.snow" },
        { "buckethat", "bucket.helmet" },
        { "semiautopistol", "pistol.semiauto" },
        { "roadsignvest", "roadsign.jacket" },
        { "roadsignpants", "roadsign.kilt" },
        { "burlappants", "burlap.trousers" },
        { "collaredshirt", "shirt.collared" },
        { "mp5", "smg.mp5" },
        { "sword", "salvaged.sword" },
        { "workboots", "shoes.boots" },
        { "vagabondjacket", "jacket" },
        { "hideshoes", "attire.hide.boots" },
        { "deerskullmask", "deer.skull.mask" },
        { "minerhat", "hat.miner" },
        { "burlapgloves", "burlap.gloves" },
        { "burlap.gloves", "burlap.gloves" },
        { "leather.gloves", "burlap.gloves" },
        { "python", "pistol.python" },
        { "woodendoubledoor", "door.double.hinged.wood" }
    };

    private void UpdateWorkshopShortName()
    {
        foreach (var itemDefinition in ItemManager.itemList)
        {
            if (itemDefinition.shortname == "ammo.snowballgun") continue;
            var name = itemDefinition.displayName.english.ToLower().Replace("skin", "").Replace(" ", "")
                .Replace("-", "");
            _workshopsnames.TryAdd(name, itemDefinition.shortname);
            _workshopsnames.TryAdd(itemDefinition.shortname, itemDefinition.shortname);
            if (!_workshopsnames.ContainsKey(itemDefinition.shortname.Replace(".", "")))
                _workshopsnames.Add(itemDefinition.shortname.Replace(".", ""), itemDefinition.shortname);
        }
    }

    #endregion
}