using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Kits", "Mevent", "1.0.32")]
    public class Kits : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary, CopyPaste, Notify, UINotify, NoEscape;

        private static Kits _instance;

        private const string Layer = "UI.Kits";

        private const string InfoLayer = "UI.Kits.Info";

        private const string EditingLayer = "UI.Kits.Editing";

        private const string ModalLayer = "UI.Kits.Modal";

        private readonly Dictionary<BasePlayer, List<Kit>> _openGUI = new Dictionary<BasePlayer, List<Kit>>();

        private readonly Dictionary<BasePlayer, Dictionary<string, object>> _kitEditing =
            new Dictionary<BasePlayer, Dictionary<string, object>>();

        private readonly Dictionary<BasePlayer, Dictionary<string, object>> _itemEditing =
            new Dictionary<BasePlayer, Dictionary<string, object>>();

        private readonly Dictionary<string, List<string>> _itemsCategories =
            new Dictionary<string, List<string>>();

        private const string PermAdmin = "Kits.admin";

        private int _lastKitID;

        private string _colorOne;
        private string _colorTwo;
        private string _colorWhite;
        private string _colorThree;
        private string _colorFour;
        private string _colorRed;

        #endregion

        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Автовайп")]
            public bool AutoWipe;

            [JsonProperty(PropertyName = "Стандартный Цвет Набора")]
            public readonly string KitColor = "#A0A935";

            [JsonProperty(PropertyName = "Работать с Notify?")]
            public readonly bool UseNotify = true;

            [JsonProperty(PropertyName = "Работать с NoEscape? (Raid/Combat block)")]
            public bool UseNoEscape;

            [JsonProperty(PropertyName = "Whitelist для NoEscape")]
            public readonly List<string> NoEscapeWhiteList = new List<string>
            {
                "kit name 1",
                "kit name 2"
            };

            [JsonProperty(PropertyName = "Команды", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly string[] Commands = {"kit", "kits"};

            [JsonProperty(PropertyName = "Economy")]
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

            [JsonProperty(PropertyName = "Настройка Редкости", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<RarityColor> RarityColors = new List<RarityColor>
            {
                new RarityColor(40, "#A0A935")
            };

            [JsonProperty(PropertyName = "Авто наборы", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> AutoKits = new List<string>
            {
                "autokit", "autokit_vip", "autokit_premium"
            };

            [JsonProperty(PropertyName = "Получение автонабора 1 раз?")]
            public bool OnceAutoKit;

            [JsonProperty(PropertyName = "Разрешить включать/выключать автокиты?")]
            public readonly bool UseChangeAutoKit = false;

            [JsonProperty(PropertyName = "Разрешение для включения/выключения автокитов")]
            public readonly string ChangeAutoKitPermission = "kits.changeautokit";

            [JsonProperty(PropertyName = "Logs")] public readonly LogInfo Logs = new LogInfo
            {
                Console = true,
                File = true
            };

            [JsonProperty(PropertyName = "Цвет 1")]
            public readonly string ColorOne = "#161617";

            [JsonProperty(PropertyName = "Цвет 2")]
            public readonly string ColorTwo = "#0E0E10";

            [JsonProperty(PropertyName = "Цвет 3")]
            public readonly string ColorThree = "#4B68FF";

            [JsonProperty(PropertyName = "Цвет 4")]
            public readonly string ColorFour = "#303030";

            [JsonProperty(PropertyName = "Цвет Red")]
            public readonly string ColorRed = "#FF4B4B";

            [JsonProperty(PropertyName = "Цвет White")]
            public readonly string ColorWhite = "#FFFFFF";

            [JsonProperty(PropertyName = "Показывать номер?")]
            public readonly bool ShowNumber = true;

            [JsonProperty(PropertyName = "Показывать оповещение об отсутвии прав?")]
            public readonly bool ShowNoPermDescription = true;

            [JsonProperty(PropertyName = "Показывать все наборы?")]
            public bool ShowAllKits;

            [JsonProperty(PropertyName = "CopyPaste Parameters",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> CopyPasteParameters = new List<string>
            {
                "deployables", "true", "inventories", "true"
            };

            [JsonProperty(PropertyName = "Блокировка в Building Block?")]
            public bool BlockBuilding;

            [JsonProperty(PropertyName = "NPC Наборы", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<string, NpcKitsData> NpcKits = new Dictionary<string, NpcKitsData>
            {
                ["1234567"] = new NpcKitsData
                {
                    Description = "Free Kits",
                    Kits = new List<string>
                    {
                        "kit_one",
                        "kit_two"
                    }
                },
                ["7654321"] = new NpcKitsData
                {
                    Description = "VIPs Kits",
                    Kits = new List<string>
                    {
                        "kit_three",
                        "kit_four"
                    }
                }
            };

            [JsonProperty(PropertyName = "Описание")]
            public readonly MenuDescription Description = new MenuDescription
            {
                AnchorMin = "0 0", AnchorMax = "1 0",
                OffsetMin = "0 -55", OffsetMax = "0 -5",
                Enabled = true,
                Color = new IColor("#0E0E10", 100),
                FontSize = 18,
                Font = "robotocondensed-bold.ttf",
                Align = TextAnchor.MiddleCenter,
                TextColor = new IColor("#FFFFFF", 100),
                Description = string.Empty
            };

            [JsonProperty(PropertyName = "Информация о наборе")]
            public readonly DescriptionSettings InfoKitDescription = new DescriptionSettings
            {
                AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                OffsetMin = "-125 -55", OffsetMax = "125 -5",
                Enabled = true,
                Color = new IColor("#0E0E10", 100),
                FontSize = 18,
                Font = "robotocondensed-bold.ttf",
                Align = TextAnchor.MiddleCenter,
                TextColor = new IColor("#FFFFFF", 100)
            };

            [JsonProperty(PropertyName = "Интерфейс")]
            public readonly UserInterface UI = new UserInterface
            {
                Height = 455,
                Width = 640,
                KitHeight = 165,
                KitWidth = 135f,
                Margin = 10f,
                KitsOnString = 4,
                Strings = 2,
                YIndent = -100f,
                DisplayName = new DisplayNameSettings
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-45 -75",
                    OffsetMax = "45 0",
                    Enabled = true
                },
                Image = new InterfacePosition
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-32 -75", OffsetMax = "32 -11"
                },
                KitAvailable = new InterfacePosition
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -100", OffsetMax = "0 -75"
                },
                KitAmount = new KitAmountSettings
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-125",
                    OffsetMax = "-120",
                    Width = 115
                },
                KitCooldown = new InterfacePosition
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-32.5 -125", OffsetMax = "32.5 -105"
                },
                KitSale = new InterfacePosition
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-32.5 -115", OffsetMax = "32.5 -95"
                },
                KitAmountCooldown = new InterfacePosition
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -120",
                    OffsetMax = "0 -95"
                },
                NoPermission = new InterfacePosition
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -100", OffsetMax = "0 -75"
                }
            };

            [JsonProperty(PropertyName = "Steampowered API ключ")]
            public readonly string SteamWebApiKey =
                "!!! You can get it HERE > https://steamcommunity.com/dev/apikey < and you need to insert HERE !!!";

            public VersionNumber Version;
        }

        private enum EconomyType
        {
            Plugin,
            Item
        }

        private class EconomyConf
        {
            [JsonProperty(PropertyName = "Type (Plugin/Item)")] [JsonConverter(typeof(StringEnumConverter))]
            public EconomyType Type;

            [JsonProperty(PropertyName = "Plugin name")]
            public string Plug;

            [JsonProperty(PropertyName = "Balance add hook")]
            public string AddHook;

            [JsonProperty(PropertyName = "Balance remove hook")]
            public string RemoveHook;

            [JsonProperty(PropertyName = "Balance show hook")]
            public string BalanceHook;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Display Name (empty - default)")]
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
                            case "BankSystem":
                            case "ServerRewards":
                                plugin.Call(AddHook, player.userID, (int) amount);
                                break;
                            default:
                                plugin.Call(AddHook, player.userID, amount);
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
                            case "BankSystem":
                            case "ServerRewards":
                                plugin.Call(RemoveHook, player.userID, (int) amount);
                                break;
                            default:
                                plugin.Call(RemoveHook, player.userID, amount);
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

            private static int ItemCount(Item[] items, string shortname, ulong skin)
            {
                return items.Where(item =>
                        item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
                    .Sum(item => item.amount);
            }

            private static void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
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
        }

        private class UserInterface
        {
            [JsonProperty(PropertyName = "Высота")]
            public float Height;

            [JsonProperty(PropertyName = "Ширина")]
            public float Width;

            [JsonProperty(PropertyName = "Высота набора")]
            public float KitHeight;

            [JsonProperty(PropertyName = "Ширина набора")]
            public float KitWidth;

            [JsonProperty(PropertyName = "Отступ")]
            public float Margin;

            [JsonProperty(PropertyName = "Кол-во наборов на строке")]
            public int KitsOnString;

            [JsonProperty(PropertyName = "Кол-во строк")]
            public int Strings;

            [JsonProperty(PropertyName = "Отступ по вертикали")]
            public float YIndent;

            [JsonProperty(PropertyName = "Настройка Display Name")]
            public DisplayNameSettings DisplayName;

            [JsonProperty(PropertyName = "Настройка изображения")]
            public InterfacePosition Image;

            [JsonProperty(PropertyName = "Настройка доступности набора")]
            public InterfacePosition KitAvailable;

            [JsonProperty(PropertyName = "Настройка количества набора")]
            public KitAmountSettings KitAmount;

            [JsonProperty(PropertyName = "Настройка КД набора")]
            public InterfacePosition KitCooldown;

            [JsonProperty(PropertyName = "Настройка продажи")]
            public InterfacePosition KitSale;

            [JsonProperty(PropertyName = "Настройка КД набора (с количеством)")]
            public InterfacePosition KitAmountCooldown;

            [JsonProperty(PropertyName = "Настройка отсутствия прав")]
            public InterfacePosition NoPermission;
        }

        private class DisplayNameSettings : InterfacePosition
        {
            [JsonProperty(PropertyName = "Включено")]
            public bool Enabled;
        }

        private class KitAmountSettings : InterfacePosition
        {
            [JsonProperty(PropertyName = "Ширина")]
            public float Width;
        }

        private class NpcKitsData
        {
            [JsonProperty(PropertyName = "Описание")]
            public string Description;

            [JsonProperty(PropertyName = "Наборы", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Kits;
        }

        private class InterfacePosition
        {
            public string AnchorMin;

            public string AnchorMax;

            public string OffsetMin;

            public string OffsetMax;
        }

        private class DescriptionSettings : InterfacePosition
        {
            [JsonProperty(PropertyName = "Включено")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Цвет фона")]
            public IColor Color;

            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize;

            [JsonProperty(PropertyName = "Font")] public string Font;

            [JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor Align;

            [JsonProperty(PropertyName = "Text Color")]
            public IColor TextColor;

            public void Get(ref CuiElementContainer container, string parent, string name = null,
                string description = null)
            {
                if (!Enabled || string.IsNullOrEmpty(description)) return;

                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = AnchorMin, AnchorMax = AnchorMax,
                        OffsetMin = OffsetMin, OffsetMax = OffsetMax
                    },
                    Image = {Color = Color.Get()}
                }, parent, name);

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text =
                    {
                        Text = $"{description}",
                        Align = Align,
                        Font = Font,
                        FontSize = FontSize,
                        Color = TextColor.Get()
                    }
                }, name);
            }
        }

        private class MenuDescription : DescriptionSettings
        {
            [JsonProperty(PropertyName = "Описание")]
            public string Description;
        }

        private class IColor
        {
            [JsonProperty(PropertyName = "HEX")] public string HEX;

            [JsonProperty(PropertyName = "Непрозрачность (0 - 100)")]
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

        private class LogInfo
        {
            [JsonProperty(PropertyName = "To Console")]
            public bool Console;

            [JsonProperty(PropertyName = "To File")]
            public bool File;
        }

        private class RarityColor
        {
            [JsonProperty(PropertyName = "Шанс")] public readonly int Chance;

            [JsonProperty(PropertyName = "Цвет")] public readonly string Color;

            public RarityColor(int chance, string color)
            {
                Chance = chance;
                Color = color;
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

            var baseConfig = new Configuration();

            if (_config.Version < new VersionNumber(1, 0, 25))
                _config.UI.KitSale = baseConfig.UI.KitSale;

            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data

        private PluginData _data;
        private Dictionary<ulong, Dictionary<string, KitData>> _playerData;

        private List<ulong> _disablesAutoKits = new List<ulong>();

        private void SaveData()
        {
            SaveKits();

            SaveUsers();

            SaveDisabledAutoKits();
        }

        private void SaveKits()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Kits", _data);
        }

        private void SaveUsers()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Data", _playerData);
        }

        private void SaveDisabledAutoKits()
        {
            if (!_config.UseChangeAutoKit) return;

            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/DisabledAutoKits", _disablesAutoKits);
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/Kits");

                _playerData =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, KitData>>>(
                        $"{Name}/Data");

                if (_config.UseChangeAutoKit)
                    _disablesAutoKits =
                        Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>(
                            $"{Name}/DisabledAutoKits");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
            if (_playerData == null) _playerData = new Dictionary<ulong, Dictionary<string, KitData>>();
            if (_disablesAutoKits == null) _disablesAutoKits = new List<ulong>();
        }


        private class PluginData
        {
            [JsonProperty(PropertyName = "Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<Kit> Kits = new List<Kit>();
        }

        private KitData GetPlayerData(ulong userID, string name)
        {
            if (!_playerData.ContainsKey(userID))
                _playerData[userID] = new Dictionary<string, KitData>();

            if (!_playerData[userID].ContainsKey(name))
                _playerData[userID][name] = new KitData();

            return _playerData[userID][name];
        }

        private class Kit
        {
            [JsonIgnore] public int ID;

            [JsonProperty(PropertyName = "Name")] public string Name;

            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Color")] public string Color;

            [JsonProperty(PropertyName = "Permission")]
            public string Permission;

            [JsonProperty(PropertyName = "Description")]
            public string Description;

            [JsonProperty(PropertyName = "Image")] public string Image;

            [JsonProperty(PropertyName = "Hide")] public bool Hide;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;

            [JsonProperty(PropertyName = "Cooldown")]
            public double Cooldown;

            [JsonProperty(PropertyName = "Wipe Block")]
            public double CooldownAfterWipe;

            [JsonProperty(PropertyName = "Building")]
            public string Building;

            [JsonProperty(PropertyName = "Enable sale")]
            public bool Sale;

            [JsonProperty(PropertyName = "Selling price")]
            public int Price;

            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<KitItem> Items;

            public void Get(BasePlayer player)
            {
                Items?.ForEach(item => item?.Get(player));
            }
        }

        private enum KitItemType
        {
            Item,
            Command
        }

        private class KitItem
        {
            [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public KitItemType Type;

            [JsonProperty(PropertyName = "Command")]
            public string Command;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;

            [JsonProperty(PropertyName = "Blueprint")]
            public int Blueprint;

            [JsonProperty(PropertyName = "SkinID")]
            public ulong SkinID;

            [JsonProperty(PropertyName = "Container")]
            public string Container;

            [JsonProperty(PropertyName = "Condition")]
            public float Condition;

            [JsonProperty(PropertyName = "Chance")]
            public int Chance;

            [JsonProperty(PropertyName = "Position", DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue(-1)]
            public int Position;

            [JsonProperty(PropertyName = "Image")] public string Image;

            [JsonProperty(PropertyName = "Weapon")]
            public Weapon Weapon;

            [JsonProperty(PropertyName = "Content", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ItemContent> Content;

            public void Get(BasePlayer player)
            {
                if (Chance < 100 && Random.Range(0, 100) > Chance) return;

                switch (Type)
                {
                    case KitItemType.Item:
                    {
                        GiveItem(player, BuildItem(),
                            Container == "belt" ? player.inventory.containerBelt :
                            Container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
                        break;
                    }
                    case KitItemType.Command:
                    {
                        ToCommand(player);
                        break;
                    }
                }
            }

            private void ToCommand(BasePlayer player)
            {
                var command = Command.Replace("\n", "|")
                    .Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace(
                        "%username%",
                        player.displayName, StringComparison.OrdinalIgnoreCase);

                foreach (var check in command.Split('|')) _instance?.Server.Command(check);
            }

            private static void GiveItem(BasePlayer player, Item item, ItemContainer cont = null)
            {
                if (item == null) return;
                var inv = player.inventory;

                var moved = item.MoveToContainer(cont, item.position) || item.MoveToContainer(cont) ||
                            item.MoveToContainer(inv.containerMain);
                if (!moved)
                {
                    if (cont == inv.containerBelt)
                        moved = item.MoveToContainer(inv.containerWear);
                    if (cont == inv.containerWear)
                        moved = item.MoveToContainer(inv.containerBelt);
                }

                if (!moved)
                    item.Drop(player.GetCenter(), player.GetDropVelocity());
            }

            private Item BuildItem()
            {
                var item = ItemManager.CreateByName(ShortName, Amount > 1 ? Amount : 1, SkinID);
                item.condition = Condition;

                item.position = Position;

                if (Blueprint != 0)
                    item.blueprintTarget = Blueprint;

                if (Weapon != null)
                {
                    var heldEntity = item.GetHeldEntity();
                    if (heldEntity != null)
                    {
                        heldEntity.skinID = SkinID;

                        var baseProjectile = heldEntity as BaseProjectile;
                        if (baseProjectile != null && !string.IsNullOrEmpty(Weapon.ammoType))
                        {
                            baseProjectile.primaryMagazine.contents = Weapon.ammoAmount;
                            baseProjectile.primaryMagazine.ammoType =
                                ItemManager.FindItemDefinition(Weapon.ammoType);
                        }

                        heldEntity.SendNetworkUpdate();
                    }
                }

                Content?.ForEach(cont =>
                {
                    var newCont = ItemManager.CreateByName(cont.ShortName, cont.Amount);
                    newCont.condition = cont.Condition;
                    newCont.MoveToContainer(item.contents);
                });

                return item;
            }

            public static KitItem FromOld(ItemData item, string container)
            {
                var newItem = new KitItem
                {
                    Content =
                        item.Contents?.Select(x =>
                                new ItemContent {ShortName = x.Shortname, Condition = x.Condition, Amount = x.Amount})
                            .ToList() ?? new List<ItemContent>(),
                    Weapon = new Weapon {ammoAmount = item.Ammo, ammoType = item.Ammotype},
                    Container = container,
                    SkinID = item.Skin,
                    Command = string.Empty,
                    Chance = 100,
                    Blueprint = string.IsNullOrEmpty(item.BlueprintShortname) ? 0 : 1,
                    Condition = item.Condition,
                    Amount = item.Amount,
                    ShortName = item.Shortname,
                    Type = KitItemType.Item,
                    Position = item.Position
                };

                return newItem;
            }
        }

        private class Weapon
        {
            public string ammoType;

            public int ammoAmount;
        }

        private class ItemContent
        {
            public string ShortName;

            public float Condition;

            public int Amount;
        }

        private class KitData
        {
            public int Amount;

            public double Cooldown;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;

            LoadData();
        }

        private void OnServerInitialized()
        {
            LoadImages();

            #region Colors

            _colorOne = HexToCuiColor(_config.ColorOne);
            _colorTwo = HexToCuiColor(_config.ColorTwo);
            _colorWhite = HexToCuiColor(_config.ColorWhite);
            _colorThree = HexToCuiColor(_config.ColorThree);
            _colorFour = HexToCuiColor(_config.ColorFour);
            _colorRed = HexToCuiColor(_config.ColorRed);

            #endregion

            #region Set IDs

            _data.Kits.ForEach(kit =>
            {
                kit.ID = _lastKitID;
                ++_lastKitID;

                if (!string.IsNullOrEmpty(kit.Permission) && !permission.PermissionExists(kit.Permission))
                    permission.RegisterPermission(kit.Permission, this);
            });

            #endregion

            permission.RegisterPermission(PermAdmin, this);

            if (!permission.PermissionExists(_config.ChangeAutoKitPermission))
                permission.RegisterPermission(_config.ChangeAutoKitPermission, this);

            FixItemsPositions();

            FillCategories();

            AddCovalenceCommand(_config.Commands, nameof(CmdOpenKits));

            timer.Every(1, HandleUi);
        }

        private void OnServerSave()
        {
            timer.In(Random.Range(2, 7), SaveData);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, InfoLayer);
                CuiHelper.DestroyUi(player, EditingLayer);
                CuiHelper.DestroyUi(player, ModalLayer);
            }

            SaveUsers();

            _instance = null;
        }

        private void OnNewSave(string filename)
        {
            if (!_config.AutoWipe) return;

            LoadData();

            _playerData.Clear();

            SaveUsers();
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || _config.UseChangeAutoKit && _disablesAutoKits.Contains(player.userID)) return;

            var kits = GetAutoKits(player);
            if (kits.Count == 0)
                return;

            player.inventory.Strip();

            if (_config.OnceAutoKit)
                kits.LastOrDefault()?.Get(player);
            else
                kits.ForEach(kit => kit.Get(player));
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            _openGUI.Remove(player);
            _kitEditing.Remove(player);
            _itemEditing.Remove(player);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;

            CuiHelper.DestroyUi(player, Layer);

            OnPlayerDisconnected(player);
        }

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (npc == null || player == null || !_config.NpcKits.ContainsKey(npc.UserIDString)) return;

            MainUi(player, npc.userID, first: true);
        }

        #endregion

        #region Commands

        private void CmdOpenKits(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (args.Length == 0)
            {
                MainUi(player, first: true);
                return;
            }

            switch (args[0])
            {
                case "help":
                {
                    Reply(player, KitsHelp, command);
                    break;
                }
                case "list":
                {
                    Reply(player, KitsList,
                        string.Join(", ", GetAvailableKits(player).Select(x => $"'{x.DisplayName}'")));
                    break;
                }
                case "remove":
                {
                    if (!IsAdmin(player)) return;

                    var name = string.Join(" ", args.Skip(1));
                    if (string.IsNullOrEmpty(name))
                    {
                        SendNotify(player, KitNotFound, 1, name);
                        return;
                    }

                    var kit = GetAvailableKits(player)?.Find(x => x.DisplayName == name);
                    if (kit == null)
                    {
                        SendNotify(player, KitNotFound, 1, name);
                        return;
                    }

                    _data.Kits.Remove(kit);
                    SaveKits();

                    SendNotify(player, KitRemoved, 0, name);
                    break;
                }

                case "autokit":
                {
                    if (!_config.UseChangeAutoKit) return;

                    if (!string.IsNullOrEmpty(_config.ChangeAutoKitPermission) &&
                        !cov.HasPermission(_config.ChangeAutoKitPermission))
                    {
                        ErrorUi(player, Msg(player, NoPermission));
                        return;
                    }

                    bool enabled;
                    if (_disablesAutoKits.Contains(player.userID))
                    {
                        _disablesAutoKits.Remove(player.userID);

                        enabled = true;
                    }
                    else
                    {
                        _disablesAutoKits.Add(player.userID);

                        enabled = false;
                    }

                    if (enabled)
                        SendNotify(player, ChangeAutoKitOn, 0);
                    else
                        SendNotify(player, ChangeAutoKitOff, 1);
                    break;
                }

                default:
                {
                    var name = string.Join(" ", args);
                    if (string.IsNullOrEmpty(name))
                    {
                        SendNotify(player, KitNotFound, 1, name);
                        return;
                    }

                    var kit = GetAvailableKits(player, checkAmount: false)
                        .Find(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase) ||
                                   string.Equals(x.DisplayName, name, StringComparison.InvariantCultureIgnoreCase));
                    if (kit == null)
                    {
                        SendNotify(player, KitNotFound, 1, name);
                        return;
                    }

                    GiveKit(player, kit);
                    break;
                }
            }
        }

        [ConsoleCommand("UI_Kits")]
        private void CmdKitsConsole(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "close":
                {
                    _openGUI.Remove(player);

                    StopEditing(player);
                    break;
                }

                case "stopedit":
                {
                    StopEditing(player);
                    break;
                }

                case "main":
                {
                    var targetId = 0UL;
                    if (arg.HasArgs(2))
                        ulong.TryParse(arg.Args[1], out targetId);

                    var page = 0;
                    if (arg.HasArgs(3))
                        int.TryParse(arg.Args[2], out page);

                    var showAll = false;
                    if (arg.HasArgs(4))
                        bool.TryParse(arg.Args[3], out showAll);

                    MainUi(player, targetId, page, showAll);
                    break;
                }

                case "infokit":
                {
                    int kitId;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out kitId)) return;

                    var kit = _data.Kits.Find(x => x.ID == kitId);
                    if (kit == null) return;

                    StopEditing(player);

                    InfoKitUi(player, kit);
                    break;
                }

                case "givekit":
                {
                    int kitId;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out kitId)) return;

                    var kit = _data.Kits.Find(x => x.ID == kitId);
                    if (kit == null) return;

                    GiveKit(player, kit);
                    break;
                }

                case "editkit":
                {
                    if (!IsAdmin(player)) return;

                    bool creating;
                    if (!arg.HasArgs(2) || !bool.TryParse(arg.Args[1], out creating)) return;

                    var kitId = -1;
                    if (arg.HasArgs(3))
                        int.TryParse(arg.Args[2], out kitId);

                    if (arg.HasArgs(4) && (!arg.HasArgs(5) || string.IsNullOrEmpty(arg.Args[4])))
                        return;

                    if (arg.HasArgs(5))
                    {
                        var key = arg.Args[3];
                        var value = arg.Args[4];

                        if (_kitEditing.ContainsKey(player) && _kitEditing[player].ContainsKey(key))
                        {
                            object newValue = null;

                            switch (key)
                            {
                                case "Hide":
                                case "AutoKit":
                                case "Sale":
                                {
                                    bool result;
                                    if (value == "delete")
                                        newValue = default(bool);
                                    else if (bool.TryParse(value, out result)) newValue = result;
                                    break;
                                }
                                case "Amount":
                                case "Price":
                                {
                                    int result;
                                    if (value == "delete")
                                        newValue = default(int);
                                    else if (int.TryParse(value, out result))
                                        newValue = result;
                                    break;
                                }
                                case "Cooldown":
                                case "CooldownAfterWipe":
                                {
                                    double result;
                                    if (value == "delete")
                                        newValue = default(double);
                                    else if (double.TryParse(value, out result))
                                        newValue = result;
                                    break;
                                }
                                case "DisplayName":
                                {
                                    newValue = string.Join(" ", arg.Args.Skip(4));
                                    break;
                                }
                                default:
                                {
                                    newValue = value == "delete" ? string.Empty : value;
                                    break;
                                }
                            }

                            _kitEditing[player][key] = newValue;
                        }
                    }

                    EditingKitUi(player, creating, kitId);
                    break;
                }

                case "takeitem":
                {
                    if (!IsAdmin(player)) return;

                    int kitId, slot;
                    if (!arg.HasArgs(5) ||
                        !_itemEditing.ContainsKey(player) ||
                        !int.TryParse(arg.Args[2], out kitId) ||
                        !int.TryParse(arg.Args[3], out slot))
                        return;

                    var container = arg.Args[1];

                    _itemEditing[player]["ShortName"] = arg.Args[4];
                    _itemEditing[player]["SkinID"] = 0UL;

                    EditingItemUi(player, kitId, slot, container);
                    break;
                }

                case "selectitem":
                {
                    if (!IsAdmin(player)) return;

                    int kitId, slot;
                    if (!arg.HasArgs(4) ||
                        !_itemEditing.ContainsKey(player) ||
                        !int.TryParse(arg.Args[2], out kitId) ||
                        !int.TryParse(arg.Args[3], out slot))
                        return;

                    var container = arg.Args[1];

                    var selectedCategory = string.Empty;
                    if (arg.HasArgs(5))
                        selectedCategory = arg.Args[4];

                    var page = 0;
                    if (arg.HasArgs(6))
                        int.TryParse(arg.Args[5], out page);

                    var input = string.Empty;
                    if (arg.HasArgs(7))
                        input = string.Join(" ", arg.Args.Skip(6));

                    SelectItem(player, kitId, slot, container, selectedCategory, page, input);
                    break;
                }

                case "startedititem":
                {
                    if (!IsAdmin(player)) return;

                    int kitId, slot;
                    if (!arg.HasArgs(4) ||
                        !int.TryParse(arg.Args[2], out kitId) ||
                        !int.TryParse(arg.Args[3], out slot)) return;

                    var container = arg.Args[1];

                    EditingItemUi(player, kitId, slot, container, true);
                    break;
                }

                case "edititem":
                {
                    if (!IsAdmin(player)) return;

                    int kitId, slot;
                    if (!arg.HasArgs(6) ||
                        !int.TryParse(arg.Args[2], out kitId) ||
                        !int.TryParse(arg.Args[3], out slot)) return;

                    var container = arg.Args[1];

                    var key = arg.Args[4];
                    var value = arg.Args[5];

                    if (_itemEditing.ContainsKey(player) && _itemEditing[player].ContainsKey(key))
                    {
                        object newValue = null;

                        switch (key)
                        {
                            case "Type":
                            {
                                KitItemType type;
                                if (value == "delete")
                                    newValue = default(KitItemType);
                                else if (Enum.TryParse(value, out type))
                                    newValue = type;
                                break;
                            }
                            case "Command":
                            {
                                if (value == "delete")
                                    newValue = string.Empty;
                                else
                                    newValue = string.Join(" ", arg.Args.Skip(5));
                                break;
                            }
                            case "ShortName":
                            {
                                if (value == "delete")
                                {
                                    newValue = string.Empty;
                                }
                                else
                                {
                                    newValue = value;
                                    _itemEditing[player]["SkinID"] = 0;
                                }

                                break;
                            }
                            case "SkinID":
                            {
                                ulong result;
                                if (value == "delete")
                                    newValue = default(ulong);
                                else if (ulong.TryParse(value, out result))
                                    newValue = result;
                                break;
                            }
                            case "Amount":
                            case "Blueprint":
                            case "Chance":
                            {
                                int result;
                                if (value == "delete")
                                    newValue = default(int);
                                else if (int.TryParse(value, out result))
                                    newValue = result;
                                break;
                            }
                        }

                        _itemEditing[player][key] = newValue;
                    }

                    EditingItemUi(player, kitId, slot, container);
                    break;
                }

                case "saveitem":
                {
                    if (!IsAdmin(player)) return;

                    int kitId, slot;
                    if (!arg.HasArgs(4) ||
                        !int.TryParse(arg.Args[1], out kitId) ||
                        !int.TryParse(arg.Args[2], out slot)) return;

                    var container = arg.Args[3];
                    if (string.IsNullOrEmpty(container)) return;

                    var editing = _itemEditing[player];
                    if (editing == null) return;

                    var kit = _data.Kits.Find(x => x.ID == kitId);
                    if (kit == null) return;

                    var item = kit.Items.Find(x => x.Container == container && x.Position == slot);
                    var hasItem = item != null;
                    var newItem = item == null || editing["ShortName"].ToString() != item.ShortName;

                    if (item == null)
                        item = new KitItem();

                    item.Type = (KitItemType) editing["Type"];
                    item.Command = editing["Command"].ToString();
                    item.Container = editing["Container"].ToString();
                    item.ShortName = editing["ShortName"].ToString();
                    item.Amount = (int) editing["Amount"];
                    item.Blueprint = (int) editing["Blueprint"];
                    item.Chance = (int) editing["Chance"];
                    item.SkinID = (ulong) editing["SkinID"];
                    item.Position = (int) editing["Position"];

                    if (newItem)
                    {
                        var info = ItemManager.FindItemDefinition(item.ShortName);
                        if (info != null)
                            item.Condition = info.condition.max;
                    }

                    if (!hasItem) kit.Items.Add(item);

                    StopEditing(player);

                    SaveKits();

                    InfoKitUi(player, kit);
                    break;
                }

                case "removeitem":
                {
                    if (!IsAdmin(player)) return;

                    int kitId, slot;
                    if (!arg.HasArgs(4) ||
                        !int.TryParse(arg.Args[1], out kitId) ||
                        !int.TryParse(arg.Args[2], out slot)) return;

                    var editing = _itemEditing[player];
                    if (editing == null) return;

                    var kit = _data.Kits.Find(x => x.ID == kitId);
                    if (kit == null) return;

                    var item = kit.Items.Find(x => x.Container == arg.Args[3] && x.Position == slot);
                    if (item != null)
                        kit.Items.Remove(item);

                    StopEditing(player);

                    SaveKits();

                    InfoKitUi(player, kit);

                    break;
                }

                case "savekit":
                {
                    if (!IsAdmin(player)) return;

                    bool creating;
                    int kitId;
                    if (!arg.HasArgs(3) || !bool.TryParse(arg.Args[1], out creating) ||
                        !int.TryParse(arg.Args[2], out kitId)) return;

                    var editing = _kitEditing[player];
                    if (editing == null) return;

                    Kit kit;
                    if (creating)
                    {
                        kit = new Kit
                        {
                            ID = ++_lastKitID,
                            Name = (string) editing["Name"],
                            DisplayName = (string) editing["DisplayName"],
                            Color = (string) editing["Color"],
                            Permission = (string) editing["Permission"],
                            Image = (string) editing["Image"],
                            Hide = Convert.ToBoolean(editing["Hide"]),
                            Amount = Convert.ToInt32(editing["Amount"]),
                            Cooldown = Convert.ToDouble(editing["Cooldown"]),
                            CooldownAfterWipe = Convert.ToDouble(editing["CooldownAfterWipe"]),
                            Sale = Convert.ToBoolean(editing["Sale"]),
                            Price = Convert.ToInt32(editing["Price"]),
                            Items = new List<KitItem>()
                        };
                        _data.Kits.Add(kit);
                    }
                    else
                    {
                        kit = _data.Kits.Find(x => x.ID == kitId);
                        if (kit == null) return;

                        kit.Name = (string) editing["Name"];
                        kit.DisplayName = (string) editing["DisplayName"];
                        kit.Color = (string) editing["Color"];
                        kit.Permission = (string) editing["Permission"];
                        kit.Image = (string) editing["Image"];
                        kit.Hide = Convert.ToBoolean(editing["Hide"]);
                        kit.Amount = Convert.ToInt32(editing["Amount"]);
                        kit.Cooldown = Convert.ToDouble(editing["Cooldown"]);
                        kit.CooldownAfterWipe = Convert.ToDouble(editing["CooldownAfterWipe"]);
                        kit.Sale = Convert.ToBoolean(editing["Sale"]);
                        kit.Price = Convert.ToInt32(editing["Price"]);
                    }

                    var autoKit = Convert.ToBoolean(editing["AutoKit"]);
                    if (autoKit)
                    {
                        if (!_config.AutoKits.Contains(kit.Name))
                        {
                            _config.AutoKits.Add(kit.Name);
                            SaveConfig();
                        }
                    }
                    else
                    {
                        _config.AutoKits.Remove(kit.Name);
                        SaveConfig();
                    }

                    StopEditing(player);

                    if (!string.IsNullOrEmpty(kit.Permission) && !permission.PermissionExists(kit.Permission))
                        permission.RegisterPermission(kit.Permission, this);

                    if (!string.IsNullOrEmpty(kit.Image))
                        ImageLibrary?.Call("AddImage", kit.Image, kit.Image);

                    SaveKits();

                    MainUi(player);
                    break;
                }

                case "removekit":
                {
                    if (!IsAdmin(player)) return;

                    int kitId;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out kitId)) return;

                    _data.Kits.RemoveAll(x => x.ID == kitId);

                    SaveKits();

                    MainUi(player);
                    break;
                }

                case "frominv":
                {
                    if (!IsAdmin(player)) return;

                    int kitId;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out kitId)) return;

                    var kit = _data.Kits.Find(x => x.ID == kitId);
                    if (kit == null) return;

                    var kitItems = GetPlayerItems(player);
                    if (kitItems == null) return;

                    kit.Items = kitItems;

                    SaveKits();

                    InfoKitUi(player, kit);
                    break;
                }
            }
        }

        [ConsoleCommand("kits.resetkits")]
        private void CmdKitsReset(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            _data.Kits.Clear();
            _playerData.Clear();

            SaveData();

            SendReply(arg, "Plugin successfully reset");
        }

        [ConsoleCommand("kits.resetdata")]
        private void CmdKitsResetPlayers(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            _playerData.Clear();

            SaveUsers();

            SendReply(arg, "Players successfully reset");
        }

        [ConsoleCommand("kits.give")]
        private void CmdKitsGive(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            if (!arg.HasArgs(2))
            {
                SendReply(arg, $"Error syntax! Use: {arg.cmd.FullName} [name/steamid] [kitname]");
                return;
            }

            var target = BasePlayer.Find(arg.Args[0]);

            if (target == null)
            {
                SendReply(arg, $"Player '{arg.Args[0]}' not found!");
                return;
            }

            var kit = _data.Kits.Find(x => x.Name == arg.Args[1]);
            if (kit == null)
            {
                SendReply(arg, $"Kit '{arg.Args[1]}' not found!");
                return;
            }

            kit.Items.ForEach(item => item.Get(target));

            SendReply(arg, $"Player '{arg.Args[0]}' successfully received a kit '{arg.Args[1]}'");

            Interface.CallHook("OnKitRedeemed", target, kit.Name);
            Log(target, kit.Name);
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, ulong targetId = 0, int page = 0, bool showAll = false,
            bool first = false)
        {
            #region Fields

            var totalAmount = _config.UI.KitsOnString * _config.UI.Strings;

            var constSwitch = -(_config.UI.KitsOnString * _config.UI.KitWidth +
                                (_config.UI.KitsOnString - 1) * _config.UI.Margin) / 2f;

            var xSwicth = constSwitch;
            var ySwitch = _config.UI.YIndent;

            var allKits = GetAvailableKits(player, targetId.ToString(), showAll);
            var kitsList = allKits.Skip(page * totalAmount).Take(totalAmount).ToList();

            _openGUI[player] = kitsList;

            #endregion

            var container = new CuiElementContainer();

            #region Background

            if (first)
            {
                CuiHelper.DestroyUi(player, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    CursorEnabled = true
                }, "Overlay", Layer);

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Close = Layer,
                        Command = "UI_Kits close"
                    }
                }, Layer);
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = $"-{_config.UI.Width / 2f} -{_config.UI.Height / 2f}",
                    OffsetMax = $"{_config.UI.Width / 2f} {_config.UI.Height / 2f}"
                },
                Image =
                {
                    Color = _colorTwo
                }
            }, Layer, Layer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = {Color = _colorOne}
            }, Layer + ".Main", Layer + ".Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "10 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, MainTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = _colorWhite
                }
            }, Layer + ".Header");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-35 -37.5",
                    OffsetMax = "-10 -12.5"
                },
                Text =
                {
                    Text = Msg(player, Close),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = _colorWhite
                },
                Button =
                {
                    Close = Layer,
                    Color = _colorThree,
                    Command = "UI_Kits close"
                }
            }, Layer + ".Header");

            if (IsAdmin(player))
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-140 -37.5",
                        OffsetMax = "-45 -12.5"
                    },
                    Text =
                    {
                        Text = Msg(player, CreateKit),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = _colorWhite
                    },
                    Button =
                    {
                        Color = _colorTwo,
                        Command = "UI_Kits editkit True"
                    }
                }, Layer + ".Header");

            #endregion

            #region Second Header

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "10 -85",
                    OffsetMax = "110 -60"
                },
                Text =
                {
                    Text = Msg(player, ListKits),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = _colorWhite
                }
            }, Layer + ".Main");

            #region Checkbox

            if (IsAdmin(player))
                CheckBoxUi(ref container,
                    Layer + ".Main",
                    Layer + ".ShowAll",
                    "0 1", "0 1",
                    "90 -77.5",
                    "100 -67.5",
                    showAll,
                    $"UI_Kits main {targetId} 0 {!showAll}",
                    Msg(player, ShowAll)
                );

            #endregion

            #region Pages

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-132.5 -82.5",
                    OffsetMax = "-72.5 -60"
                },
                Text =
                {
                    Text = Msg(player, Back),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = _colorWhite
                },
                Button =
                {
                    Color = _colorOne,
                    Command = page != 0 ? $"UI_Kits main {targetId} {page - 1} {showAll}" : ""
                }
            }, Layer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-70 -82.5",
                    OffsetMax = "-10 -60"
                },
                Text =
                {
                    Text = Msg(player, Next),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = _colorWhite
                },
                Button =
                {
                    Color = _colorThree,
                    Command = allKits.Count > (page + 1) * totalAmount
                        ? $"UI_Kits main {targetId} {page + 1} {showAll}"
                        : ""
                }
            }, Layer + ".Main");

            #endregion

            #endregion

            #region Kits

            if (allKits.Count == 0)
                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 25", OffsetMax = "0 -85"
                    },
                    Text =
                    {
                        Text = Msg(player, NotAvailableKits),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 20,
                        Color = "1 1 1 0.45"
                    }
                }, Layer + ".Main");
            else
                for (var i = 0; i < kitsList.Count; i++)
                {
                    var kit = kitsList[i];

                    var number = page * totalAmount + i + 1;

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = $"{xSwicth} {ySwitch - _config.UI.KitHeight}",
                            OffsetMax = $"{xSwicth + _config.UI.KitWidth} {ySwitch}"
                        },
                        Image =
                        {
                            Color = "0 0 0 0"
                        }
                    }, Layer + ".Main", Layer + $".Kit.{kit.ID}.Main");

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "0 30", OffsetMax = "0 0"
                        },
                        Image =
                        {
                            Color = _colorOne
                        }
                    }, Layer + $".Kit.{kit.ID}.Main", Layer + $".Kit.{kit.ID}.Main.Background");

                    #region Image

                    if (!string.IsNullOrEmpty(kit.Image))
                        container.Add(new CuiElement
                        {
                            Parent = Layer + $".Kit.{kit.ID}.Main",
                            Components =
                            {
                                new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", kit.Image)},
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = _config.UI.Image.AnchorMin,
                                    AnchorMax = _config.UI.Image.AnchorMax,
                                    OffsetMin = _config.UI.Image.OffsetMin,
                                    OffsetMax = _config.UI.Image.OffsetMax
                                }
                            }
                        });

                    #endregion

                    #region Name

                    if (_config.ShowNumber)
                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                                OffsetMin = "-45 -75",
                                OffsetMax = "45 0"
                            },
                            Text =
                            {
                                Text = $"#{number}",
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 60,
                                Color = _colorFour
                            }
                        }, Layer + $".Kit.{kit.ID}.Main");

                    if (_config.UI.DisplayName.Enabled)
                        container.Add(new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = _config.UI.DisplayName.AnchorMin,
                                AnchorMax = _config.UI.DisplayName.AnchorMax,
                                OffsetMin = _config.UI.DisplayName.OffsetMin,
                                OffsetMax = _config.UI.DisplayName.OffsetMax
                            },
                            Text =
                            {
                                Text = $"{kit.DisplayName}",
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 16,
                                Color = "1 1 1 1"
                            }
                        }, Layer + $".Kit.{kit.ID}.Main");

                    #endregion

                    #region Line

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "0 0", OffsetMax = "0 2"
                        },
                        Image = {Color = HexToCuiColor(kit.Color)}
                    }, Layer + $".Kit.{kit.ID}.Main.Background");

                    #endregion

                    #region Give Kit

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"0 -{_config.UI.KitHeight}",
                            OffsetMax = $"{_config.UI.KitWidth - 30} -{_config.UI.KitHeight - 25}"
                        },
                        Text =
                        {
                            Text = Msg(player, KitTake),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = _colorOne,
                            Command = $"UI_Kits givekit {kit.ID}"
                        }
                    }, Layer + $".Kit.{kit.ID}.Main");

                    #endregion

                    #region Info

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{_config.UI.KitWidth - 25} -{_config.UI.KitHeight}",
                            OffsetMax = $"{_config.UI.KitWidth} -{_config.UI.KitHeight - 25}"
                        },
                        Text =
                        {
                            Text = Msg(player, KitInfo),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 18,
                            Color = "1 1 1 1"
                        },
                        Button =
                        {
                            Color = _colorOne,
                            Command = $"UI_Kits infokit {kit.ID}"
                        }
                    }, Layer + $".Kit.{kit.ID}.Main");

                    #endregion

                    RefreshKitUi(ref container, player, kit);

                    if ((i + 1) % _config.UI.KitsOnString == 0)
                    {
                        xSwicth = constSwitch;
                        ySwitch = ySwitch - _config.UI.KitHeight - _config.UI.Margin;
                    }
                    else
                    {
                        xSwicth += _config.UI.Margin + _config.UI.KitWidth;
                    }
                }

            #endregion

            #region Description

            NpcKitsData npcKit;
            var description = targetId == 0
                ? _config.Description.Description
                : _config.NpcKits.TryGetValue(targetId.ToString(), out npcKit)
                    ? npcKit.Description
                    : string.Empty;

            _config.Description.Get(ref container, Layer + ".Main", null, description);

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void InfoKitUi(BasePlayer player, Kit kit)
        {
            var container = new CuiElementContainer();

            #region Fields

            var Size = 70f;
            var Margin = 5f;

            var ySwitch = -125f;
            var amountOnString = 6;
            var constSwitch = -(amountOnString * Size + (amountOnString - 1) * Margin) / 2f;

            var total = 0;

            #endregion

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = HexToCuiColor(_config.ColorTwo, 98)
                }
            }, "Overlay", InfoLayer);

            #endregion

            #region Header

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "112.5 -140", OffsetMax = "222.5 -115"
                },
                Text =
                {
                    Text = Msg(player, ComeBack),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _colorThree,
                    Command = "UI_Kits stopedit",
                    Close = InfoLayer
                }
            }, InfoLayer);

            #region Change Button

            if (IsAdmin(player))
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-12.5 -140", OffsetMax = "102.5 -115"
                    },
                    Image = {Color = "0 0 0 0"}
                }, InfoLayer, InfoLayer + ".Btn.Change");

                CreateOutLine(ref container, InfoLayer + ".Btn.Change", _colorThree, 1);

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text =
                    {
                        Text = Msg(player, Edit),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_Kits editkit {false} {kit.ID}",
                        Close = InfoLayer
                    }
                }, InfoLayer + ".Btn.Change");
            }

            #endregion

            #endregion

            #region Main

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = $"{constSwitch} {ySwitch - 15f}", OffsetMax = $"0 {ySwitch}"
                },
                Text =
                {
                    Text = Msg(player, ContainerMain),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, InfoLayer);

            ySwitch -= 20f;

            var xSwitch = constSwitch;

            var kitItems = kit.Items.FindAll(item => item.Container == "main");

            kitItems.Sort((x, y) => y.Chance.CompareTo(x.Chance));

            for (var slot = 0; slot < amountOnString * 4; slot++)
            {
                var kitItem = kitItems.Find(x => x.Position == slot); //kitItems.Count > slot ? kitItems[slot] : null;

                InfoItemUi(ref container, player,
                    slot,
                    $"{xSwitch} {ySwitch - Size}",
                    $"{xSwitch + Size} {ySwitch}",
                    kit,
                    kitItem,
                    total,
                    "main");

                if ((slot + 1) % amountOnString == 0)
                {
                    xSwitch = constSwitch;
                    ySwitch = ySwitch - Size - Margin;
                }
                else
                {
                    xSwitch += Size + Margin;
                }

                total++;
            }

            #endregion

            #region Wear

            ySwitch -= 5f;

            amountOnString = 7;

            constSwitch = -(amountOnString * Size + (amountOnString - 1) * Margin) / 2f;

            xSwitch = constSwitch;

            kitItems = kit.Items.FindAll(item => item.Container == "wear");

            kitItems.Sort((x, y) => y.Chance.CompareTo(x.Chance));

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = $"{constSwitch} {ySwitch - 15f}", OffsetMax = $"0 {ySwitch}"
                },
                Text =
                {
                    Text = Msg(player, ContainerWear),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, InfoLayer);

            ySwitch -= 20f;

            for (var slot = 0; slot < amountOnString; slot++)
            {
                var kitItem = kitItems.Find(x => x.Position == slot); //kitItems.Count > slot ? kitItems[slot] : null;

                InfoItemUi(ref container, player,
                    slot,
                    $"{xSwitch} {ySwitch - Size}",
                    $"{xSwitch + Size} {ySwitch}",
                    kit,
                    kitItem,
                    total,
                    "wear");

                if ((slot + 1) % amountOnString == 0)
                {
                    xSwitch = constSwitch;
                    ySwitch = ySwitch - Size - Margin;
                }
                else
                {
                    xSwitch += Size + Margin;
                }

                total++;
            }

            #endregion

            #region Belt

            ySwitch -= 5f;

            amountOnString = 6;

            constSwitch = -(amountOnString * Size + (amountOnString - 1) * Margin) / 2f;

            xSwitch = constSwitch;

            kitItems = kit.Items.FindAll(item => item.Container == "belt");

            kitItems.Sort((x, y) => y.Chance.CompareTo(x.Chance));

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = $"{constSwitch} {ySwitch - 15f}", OffsetMax = $"0 {ySwitch}"
                },
                Text =
                {
                    Text = Msg(player, ContainerBelt),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 1"
                }
            }, InfoLayer);

            ySwitch -= 20f;

            for (var slot = 0; slot < amountOnString; slot++)
            {
                var kitItem = kitItems.Find(x => x.Position == slot); //kitItems.Count > slot ? kitItems[slot] : null;

                InfoItemUi(ref container, player,
                    slot,
                    $"{xSwitch} {ySwitch - Size}",
                    $"{xSwitch + Size} {ySwitch}",
                    kit,
                    kitItem,
                    total,
                    "belt");

                if ((slot + 1) % amountOnString == 0)
                {
                    xSwitch = constSwitch;
                    ySwitch = ySwitch - Size - Margin;
                }
                else
                {
                    xSwitch += Size + Margin;
                }

                total++;
            }

            #endregion

            #region Description

            _config.InfoKitDescription.Get(ref container, InfoLayer, null, kit.Description);

            #endregion

            CuiHelper.DestroyUi(player, InfoLayer);
            CuiHelper.AddUi(player, container);
        }

        private void EditingKitUi(BasePlayer player, bool creating, int kitId = -1)
        {
            #region Dictionary

            if (!_kitEditing.ContainsKey(player))
            {
                if (kitId != -1)
                {
                    var kit = _data.Kits.Find(x => x.ID == kitId);
                    if (kit == null) return;

                    _kitEditing.Add(player, new Dictionary<string, object>
                    {
                        ["Name"] = kit.Name,
                        ["DisplayName"] = kit.DisplayName,
                        ["Color"] = kit.Color,
                        ["Permission"] = kit.Permission,
                        ["Description"] = kit.Description,
                        ["Image"] = kit.Image,
                        ["Hide"] = kit.Hide,
                        ["Amount"] = kit.Amount,
                        ["Cooldown"] = kit.Cooldown,
                        ["CooldownAfterWipe"] = kit.CooldownAfterWipe,
                        ["Sale"] = kit.Sale,
                        ["Price"] = kit.Price,
                        ["AutoKit"] = _config.AutoKits.Contains(kit.Name)
                    });
                }
                else
                {
                    _kitEditing.Add(player, new Dictionary<string, object>
                    {
                        ["Name"] = CuiHelper.GetGuid(),
                        ["DisplayName"] = "My Kit",
                        ["Color"] = _config.KitColor,
                        ["Permission"] = $"{Name}.default",
                        ["Description"] = string.Empty,
                        ["Image"] = string.Empty,
                        ["Hide"] = true,
                        ["Amount"] = 0,
                        ["Cooldown"] = 0.0,
                        ["CooldownAfterWipe"] = 0,
                        ["Sale"] = 0,
                        ["Price"] = 0,
                        ["AutoKit"] = false
                    });
                }
            }

            #endregion

            var container = new CuiElementContainer();

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = HexToCuiColor(_config.ColorTwo, 98)
                }
            }, "Overlay", EditingLayer);

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-260 -230",
                    OffsetMax = "260 255"
                },
                Image =
                {
                    Color = _colorTwo
                }
            }, EditingLayer, EditingLayer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = {Color = _colorOne}
            }, EditingLayer + ".Main", EditingLayer + ".Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "10 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, CreateOrEditKit),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = _colorWhite
                }
            }, EditingLayer + ".Header");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-35 -37.5",
                    OffsetMax = "-10 -12.5"
                },
                Text =
                {
                    Text = Msg(player, Close),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = _colorWhite
                },
                Button =
                {
                    Close = EditingLayer,
                    Color = _colorThree,
                    Command = "UI_Kits close"
                }
            }, EditingLayer + ".Header");

            if (IsAdmin(player))
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-140 -37.5",
                        OffsetMax = "-45 -12.5"
                    },
                    Text =
                    {
                        Text = Msg(player, MainMenu),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _colorTwo,
                        Command = $"{_config.Commands.GetRandom()}",
                        Close = EditingLayer
                    }
                }, EditingLayer + ".Header");

            #endregion

            #region Fields

            var ySwitch = -60f;
            var Height = 60f;
            var Width = 225f;
            var xMargin = 35f;
            var yMargin = 10f;

            var i = 1;
            foreach (var obj in _kitEditing[player]
                         .Where(x => x.Key != "Hide" && x.Key != "AutoKit" && x.Key != "Sale"))
            {
                var xSwitch = i % 2 == 0 ? xMargin / 2f : -Width - xMargin / 2f;

                EditFieldUi(player, ref container, EditingLayer + ".Main", EditingLayer + $".Editing.{i}",
                    $"{xSwitch} {ySwitch - Height}",
                    $"{xSwitch + Width} {ySwitch}",
                    $"UI_Kits editkit {creating} {kitId} {obj.Key} ",
                    obj);

                if (i % 2 == 0) ySwitch = ySwitch - Height - yMargin;

                i++;
            }

            #region Hide

            var hide = !(_kitEditing[player]["Hide"] is bool && (bool) _kitEditing[player]["Hide"]);

            CheckBoxUi(ref container, EditingLayer + ".Main", EditingLayer + ".Editing.Hide", "0.5 1", "0.5 1",
                $"{-Width - xMargin / 2f} {ySwitch - 10}",
                $"{-Width - xMargin / 2f + 10} {ySwitch}",
                hide,
                $"UI_Kits editkit {creating} {kitId} Hide {hide}",
                Msg(player, EnableKit)
            );

            #endregion

            #region Auto Kit

            var autoKit = _kitEditing[player]["AutoKit"] is bool && (bool) _kitEditing[player]["AutoKit"];

            CheckBoxUi(ref container, EditingLayer + ".Main", EditingLayer + ".Editing.AutoKit", "0.5 1", "0.5 1",
                $"{-Width - xMargin / 2f + 80} {ySwitch - 10}",
                $"{-Width - xMargin / 2f + 90} {ySwitch}",
                autoKit,
                $"UI_Kits editkit {creating} {kitId} AutoKit {!autoKit}",
                Msg(player, AutoKit)
            );

            #endregion

            #region Sale

            var sale = _kitEditing[player]["Sale"] is bool && (bool) _kitEditing[player]["Sale"];

            CheckBoxUi(ref container, EditingLayer + ".Main", EditingLayer + ".Editing.Sale", "0.5 1", "0.5 1",
                $"{-Width - xMargin / 2f + 160} {ySwitch - 10}",
                $"{-Width - xMargin / 2f + 170} {ySwitch}",
                sale,
                $"UI_Kits editkit {creating} {kitId} Sale {!sale}",
                Msg(player, EnabledSale)
            );

            #endregion

            #endregion

            ySwitch -= 35f;

            #region Buttons

            #region Save Kit

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = $"15 {ySwitch - 25}",
                    OffsetMax = $"115 {ySwitch}"
                },
                Text =
                {
                    Text = Msg(player, SaveKit),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _colorThree,
                    Command = $"UI_Kits savekit {creating} {kitId}",
                    Close = EditingLayer
                }
            }, EditingLayer + ".Main");

            #endregion

            #region Add From Inventory

            if (!creating)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"-100 {ySwitch - 25}",
                        OffsetMax = $"100 {ySwitch}"
                    },
                    Text =
                    {
                        Text = Msg(player, CopyItems),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#50965F"),
                        Command = $"UI_Kits frominv {kitId}",
                        Close = EditingLayer
                    }
                }, EditingLayer + ".Main");

            #endregion

            #region Remove Kit

            if (!creating)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = $"-115 {ySwitch - 25}",
                        OffsetMax = $"-15 {ySwitch}"
                    },
                    Text =
                    {
                        Text = Msg(player, RemoveKit),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _colorRed,
                        Command = $"UI_Kits removekit {kitId}",
                        Close = EditingLayer
                    }
                }, EditingLayer + ".Main");

            #endregion

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, EditingLayer);
            CuiHelper.AddUi(player, container);
        }

        private void EditingItemUi(BasePlayer player, int kitId, int slot, string itemContainer, bool First = false)
        {
            var container = new CuiElementContainer();

            #region Dictionary

            if (!_itemEditing.ContainsKey(player))
            {
                var kit = _data.Kits.Find(x => x.ID == kitId);
                if (kit == null) return;

                var item = kit.Items.Find(x => x.Container == itemContainer && x.Position == slot);
                if (item != null)
                    _itemEditing.Add(player, new Dictionary<string, object>
                    {
                        ["Type"] = item.Type,
                        ["Command"] = item.Command,
                        ["Container"] = item.Container,
                        ["ShortName"] = item.ShortName,
                        ["Amount"] = item.Amount,
                        ["Blueprint"] = item.Blueprint,
                        ["SkinID"] = item.SkinID,
                        ["Chance"] = item.Chance,
                        ["Position"] = item.Position
                    });
                else
                    _itemEditing.Add(player, new Dictionary<string, object>
                    {
                        ["Type"] = KitItemType.Item,
                        ["Container"] = itemContainer,
                        ["Command"] = string.Empty,
                        ["ShortName"] = string.Empty,
                        ["Amount"] = 1,
                        ["Blueprint"] = 0,
                        ["SkinID"] = 0UL,
                        ["Chance"] = 100,
                        ["Position"] = slot
                    });
            }

            #endregion

            var edit = _itemEditing[player];

            #region Background

            if (First)
            {
                CuiHelper.DestroyUi(player, EditingLayer);

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Image = {Color = HexToCuiColor(_config.ColorOne, 80)},
                    CursorEnabled = true
                }, "Overlay", EditingLayer);
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-260 -240",
                    OffsetMax = "260 250"
                },
                Image =
                {
                    Color = _colorTwo
                }
            }, EditingLayer, EditingLayer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = {Color = _colorOne}
            }, EditingLayer + ".Main", EditingLayer + ".Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "10 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, EditingTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = _colorWhite
                }
            }, EditingLayer + ".Header");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-35 -37.5",
                    OffsetMax = "-10 -12.5"
                },
                Text =
                {
                    Text = Msg(player, Close),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = _colorWhite
                },
                Button =
                {
                    Close = EditingLayer,
                    Color = _colorThree,
                    Command = $"UI_Kits infokit {kitId}"
                }
            }, EditingLayer + ".Header");

            #endregion

            #region Type

            var type = edit["Type"] as KitItemType? ?? KitItemType.Item;

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "10 -110",
                    OffsetMax = "115 -80"
                },
                Text =
                {
                    Text = Msg(player, ItemName),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = HexToCuiColor(_config.ColorThree, type == KitItemType.Item ? 100 : 50),
                    Command = $"UI_Kits edititem {itemContainer} {kitId} {slot} Type {KitItemType.Item}"
                }
            }, EditingLayer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "135 -110",
                    OffsetMax = "240 -80"
                },
                Text =
                {
                    Text = Msg(player, CmdName),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = HexToCuiColor(_config.ColorThree, type == KitItemType.Command ? 100 : 50),
                    Command = $"UI_Kits edititem {itemContainer} {kitId} {slot} Type {KitItemType.Command}"
                }
            }, EditingLayer + ".Main");

            #endregion

            #region Command

            EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-240 -110",
                "0 -60",
                $"UI_Kits edititem {itemContainer} {kitId} {slot} Command ",
                new KeyValuePair<string, object>("Command", edit["Command"]));

            #endregion

            #region Item

            var shortName = (string) edit["ShortName"];

            #region Image

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-240 -265", OffsetMax = "-105 -130"
                },
                Image = {Color = _colorOne}
            }, EditingLayer + ".Main", EditingLayer + ".Image");

            if (!string.IsNullOrEmpty(shortName) && ImageLibrary)
                container.Add(new CuiElement
                {
                    Parent = EditingLayer + ".Image",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = GetItemImage(shortName, (ulong) edit["SkinID"])
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "10 10", OffsetMax = "-10 -10"
                        }
                    }
                });

            #endregion

            #region ShortName

            EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-85 -190",
                "140 -130",
                $"UI_Kits edititem {itemContainer} {kitId} {slot} ShortName ",
                new KeyValuePair<string, object>("ShortName", edit["ShortName"]));

            #endregion

            #region Select Item

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-85 -265",
                    OffsetMax = "55 -235"
                },
                Text =
                {
                    Text = Msg(player, BtnSelect),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _colorThree,
                    Command = $"UI_Kits selectitem {itemContainer} {kitId} {slot}"
                }
            }, EditingLayer + ".Main");

            #endregion

            #region Blueprint

            var bp = edit["Blueprint"] as int? ?? 0;
            CheckBoxUi(ref container,
                EditingLayer + ".Main",
                CuiHelper.GetGuid(),
                "0.5 1", "0.5 1",
                "65 -255",
                "75 -245",
                bp == 1,
                $"UI_Kits edititem {itemContainer} {kitId} {slot} Blueprint {(bp == 0 ? 1 : 0)}",
                Msg(player, BluePrint)
            );

            #endregion

            #region Amount

            EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-240 -345",
                "-7.5 -285",
                $"UI_Kits edititem {itemContainer} {kitId} {slot} Amount ",
                new KeyValuePair<string, object>("Amount", edit["Amount"]));

            #endregion

            #region Chance

            EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "7.5 -345",
                "240 -285",
                $"UI_Kits edititem {itemContainer} {kitId} {slot} Chance ",
                new KeyValuePair<string, object>("Chance", edit["Chance"]));

            #endregion

            #region Skin

            EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
                "-240 -425",
                "240 -365",
                $"UI_Kits edititem {itemContainer} {kitId} {slot} SkinID ",
                new KeyValuePair<string, object>("SkinID", edit["SkinID"]));

            #endregion

            #endregion

            #region Save Button

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "-90 10",
                    OffsetMax = $"{(slot == -1 ? 90 : 55)} 40"
                },
                Text =
                {
                    Text = Msg(player, BtnSave),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _colorThree,
                    Command = $"UI_Kits saveitem {kitId} {slot} {itemContainer}",
                    Close = EditingLayer
                }
            }, EditingLayer + ".Main");

            #endregion

            #region Save Button

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "60 10",
                    OffsetMax = "90 40"
                },
                Text =
                {
                    Text = Msg(player, RemoveItem),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _colorRed,
                    Command = $"UI_Kits removeitem {kitId} {slot} {itemContainer}",
                    Close = EditingLayer
                }
            }, EditingLayer + ".Main");

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, EditingLayer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void SelectItem(BasePlayer player, int kitId, int slot, string itemContainer,
            string selectedCategory = "", int page = 0, string input = "")
        {
            if (string.IsNullOrEmpty(selectedCategory)) selectedCategory = _itemsCategories.FirstOrDefault().Key;

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button =
                {
                    Close = ModalLayer,
                    Color = HexToCuiColor(_config.ColorOne, 80)
                }
            }, "Overlay", ModalLayer);

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-260 -270",
                    OffsetMax = "260 280"
                },
                Image =
                {
                    Color = _colorTwo
                }
            }, ModalLayer, ModalLayer + ".Main");

            #region Categories

            var amountOnString = 4;
            var Width = 120f;
            var Height = 25f;
            var xMargin = 5f;
            var yMargin = 5f;

            var constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
            var xSwitch = constSwitch;
            var ySwitch = -15f;

            var i = 1;
            foreach (var category in _itemsCategories)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"{xSwitch} {ySwitch - Height}",
                        OffsetMax = $"{xSwitch + Width} {ySwitch}"
                    },
                    Text =
                    {
                        Text = $"{category.Key}",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = selectedCategory == category.Key
                            ? _colorThree
                            : _colorOne,
                        Command = $"UI_Kits selectitem {itemContainer} {kitId} {slot}  {category.Key}"
                    }
                }, ModalLayer + ".Main");

                if (i % amountOnString == 0)
                {
                    ySwitch = ySwitch - Height - yMargin;
                    xSwitch = constSwitch;
                }
                else
                {
                    xSwitch += xMargin + Width;
                }

                i++;
            }

            #endregion

            #region Items

            amountOnString = 5;

            var strings = 4;
            var totalAmount = amountOnString * strings;

            ySwitch = ySwitch - yMargin - Height - 10f;

            Width = 85f;
            Height = 85f;
            xMargin = 15f;
            yMargin = 5f;

            constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
            xSwitch = constSwitch;

            i = 1;

            var canSearch = !string.IsNullOrEmpty(input) && input.Length > 2;

            var temp = canSearch
                ? _itemsCategories
                    .SelectMany(x => x.Value)
                    .Where(x => x.StartsWith(input) || x.Contains(input) || x.EndsWith(input)).ToList()
                : _itemsCategories[selectedCategory];

            var itemsAmount = temp.Count;
            var items = temp.Skip(page * totalAmount).Take(totalAmount).ToList();

            items.ForEach(item =>
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"{xSwitch} {ySwitch - Height}",
                        OffsetMax = $"{xSwitch + Width} {ySwitch}"
                    },
                    Image = {Color = _colorOne}
                }, ModalLayer + ".Main", ModalLayer + $".Item.{item}");

                if (ImageLibrary)
                    container.Add(new CuiElement
                    {
                        Parent = ModalLayer + $".Item.{item}",
                        Components =
                        {
                            new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", item)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "5 5", OffsetMax = "-5 -5"
                            }
                        }
                    });

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_Kits takeitem {itemContainer} {kitId} {slot} {item}",
                        Close = ModalLayer
                    }
                }, ModalLayer + $".Item.{item}");

                if (i % amountOnString == 0)
                {
                    xSwitch = constSwitch;
                    ySwitch = ySwitch - yMargin - Height;
                }
                else
                {
                    xSwitch += xMargin + Width;
                }

                i++;
            });

            #endregion

            #region Search

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                    OffsetMin = "-90 10", OffsetMax = "90 35"
                },
                Image = {Color = _colorThree}
            }, ModalLayer + ".Main", ModalLayer + ".Search");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "10 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = canSearch ? $"{input}" : Msg(player, ItemSearch),
                    Align = canSearch ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = canSearch ? "1 1 1 0.8" : "1 1 1 1"
                }
            }, ModalLayer + ".Search");

            container.Add(new CuiElement
            {
                Parent = ModalLayer + ".Search",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft,
                        Command = $"UI_Kits selectitem {itemContainer} {kitId} {slot} {selectedCategory} 0 ",
                        Color = "1 1 1 0.95",
                        CharsLimit = 150
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "10 0", OffsetMax = "0 0"
                    }
                }
            });

            #endregion

            #region Pages

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = "10 10",
                    OffsetMax = "80 35"
                },
                Text =
                {
                    Text = Msg(player, Back),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _colorOne,
                    Command = page != 0
                        ? $"UI_Kits selectitem {itemContainer} {kitId} {slot} {selectedCategory} {page - 1} {input}"
                        : ""
                }
            }, ModalLayer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 0", AnchorMax = "1 0",
                    OffsetMin = "-80 10",
                    OffsetMax = "-10 35"
                },
                Text =
                {
                    Text = Msg(player, Next),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Color = _colorThree,
                    Command = itemsAmount > (page + 1) * totalAmount
                        ? $"UI_Kits selectitem {itemContainer} {kitId} {slot} {selectedCategory} {page + 1} {input}"
                        : ""
                }
            }, ModalLayer + ".Main");

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, ModalLayer);
            CuiHelper.AddUi(player, container);
        }

        private void ErrorUi(BasePlayer player, string msg)
        {
            var container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Image = {Color = HexToCuiColor(_config.ColorTwo, 98)},
                        CursorEnabled = true
                    },
                    "Overlay", ModalLayer
                },
                {
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-127.5 -75",
                            OffsetMax = "127.5 140"
                        },
                        Image = {Color = _colorRed}
                    },
                    ModalLayer, ModalLayer + ".Main"
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -165", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = "XXX",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 120,
                            Color = _colorWhite
                        }
                    },
                    ModalLayer + ".Main"
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -175", OffsetMax = "0 -155"
                        },
                        Text =
                        {
                            Text = $"{msg}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = _colorWhite
                        }
                    },
                    ModalLayer + ".Main"
                },
                {
                    new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 30"
                        },
                        Text =
                        {
                            Text = Msg(player, BtnClose),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = _colorWhite
                        },
                        Button = {Color = HexToCuiColor("#CD3838"), Close = ModalLayer}
                    },
                    ModalLayer + ".Main"
                }
            };

            CuiHelper.DestroyUi(player, ModalLayer);
            CuiHelper.AddUi(player, container);
        }

        private void EditFieldUi(BasePlayer player, ref CuiElementContainer container,
            string parent,
            string name,
            string oMin,
            string oMax,
            string command,
            KeyValuePair<string, object> obj)
        {
            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = $"{oMin}",
                    OffsetMax = $"{oMax}"
                },
                Image =
                {
                    Color = "0 0 0 0"
                }
            }, parent, name);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -20", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = $"{obj.Key}",
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 1"
                }
            }, name);

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "0 0", OffsetMax = "0 -20"
                },
                Image = {Color = "0 0 0 0"}
            }, name, $"{name}.Value");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "10 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = $"{obj.Value}",
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = "1 1 1 0.15"
                }
            }, $"{name}.Value");

            CreateOutLine(ref container, $"{name}.Value", _colorOne);

            container.Add(new CuiElement
            {
                Parent = $"{name}.Value",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Command = $"{command}",
                        Color = "1 1 1 0.99",
                        CharsLimit = 150
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "10 0", OffsetMax = "0 0"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-30 -40", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, EditRemoveField),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 14,
                    Color = _colorOne
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"{command}delete"
                }
            }, $"{name}.Value");
        }

        private void CheckBoxUi(ref CuiElementContainer container, string parent, string name, string aMin, string aMax,
            string oMin, string oMax, bool enabled,
            string command, string text)
        {
            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = aMin, AnchorMax = aMax,
                    OffsetMin = oMin,
                    OffsetMax = oMax
                },
                Image = {Color = "0 0 0 0"}
            }, parent, name);

            CreateOutLine(ref container, name, _colorThree, 1);

            if (enabled)
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    },
                    Image = {Color = _colorThree}
                }, name);


            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = ""},
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"{command}"
                }
            }, name);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                    OffsetMin = "5 -10",
                    OffsetMax = "100 10"
                },
                Text =
                {
                    Text = $"{text}",
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = _colorWhite
                }
            }, name);
        }

        private void InfoItemUi(ref CuiElementContainer container, BasePlayer player, int slot, string oMin,
            string oMax, Kit kit,
            KitItem kitItem, int total, string itemContainer)
        {
            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = $"{oMin}",
                    OffsetMax = $"{oMax}"
                },
                Image =
                {
                    Color = _colorOne
                }
            }, InfoLayer, InfoLayer + $".Item.{total}");

            if (kitItem != null)
            {
                if (ImageLibrary)
                    container.Add(new CuiElement
                    {
                        Parent = InfoLayer + $".Item.{total}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = !string.IsNullOrEmpty(kitItem.Image)
                                    ? ImageLibrary.Call<string>("GetImage", kitItem.Image)
                                    : GetItemImage(kitItem.ShortName, kitItem.SkinID)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "10 10", OffsetMax = "-10 -10"
                            }
                        }
                    });

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "2.5 3.5", OffsetMax = "-2.5 -2.5"
                    },
                    Text =
                    {
                        Text = $"x{kitItem.Amount}",
                        Align = TextAnchor.LowerRight,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    }
                }, InfoLayer + $".Item.{total}");

                var color = _config.RarityColors.Find(x => x.Chance == kitItem.Chance);
                if (color != null)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "0 0", OffsetMax = "0 2"
                        },
                        Image =
                        {
                            Color = HexToCuiColor(color.Color)
                        }
                    }, InfoLayer + $".Item.{total}");

                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "2.5 2.5", OffsetMax = "-2.5 -2.5"
                        },
                        Text =
                        {
                            Text = $"{kitItem.Chance}%",
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Color = "1 1 1 1"
                        }
                    }, InfoLayer + $".Item.{total}");
                }
            }

            if (IsAdmin(player))
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command =
                            $"UI_Kits startedititem {itemContainer} {kit.ID} {slot}",
                        Close = InfoLayer
                    }
                }, InfoLayer + $".Item.{total}");
        }

        private void RefreshKitUi(ref CuiElementContainer container, BasePlayer player, Kit kit)
        {
            var playerData = GetPlayerData(player.userID, kit.Name);
            if (playerData == null) return;

            CuiHelper.DestroyUi(player, Layer + $".Kit.{kit.ID}");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0"}
            }, Layer + $".Kit.{kit.ID}.Main", Layer + $".Kit.{kit.ID}");

            if (_config.ShowAllKits && _config.ShowNoPermDescription && !string.IsNullOrEmpty(kit.Permission) &&
                !permission.UserHasPermission(player.UserIDString, kit.Permission))
            {
                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = _config.UI.NoPermission.AnchorMin, AnchorMax = _config.UI.NoPermission.AnchorMax,
                        OffsetMin = _config.UI.NoPermission.OffsetMin, OffsetMax = _config.UI.NoPermission.OffsetMax
                    },
                    Text =
                    {
                        Text = Msg(player, NoPermissionDescription),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = _colorFour
                    }
                }, Layer + $".Kit.{kit.ID}");
                return;
            }

            if (kit.Cooldown > 0 && playerData.Cooldown - 1 < GetCurrentTime() || kit.Cooldown == 0)
            {
                if (kit.Sale)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = _config.UI.KitSale.AnchorMin, AnchorMax = _config.UI.KitSale.AnchorMax,
                            OffsetMin = _config.UI.KitSale.OffsetMin, OffsetMax = _config.UI.KitSale.OffsetMax
                        },
                        Image = {Color = HexToCuiColor(kit.Color)}
                    }, Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Sale");

                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Text =
                        {
                            Text = Msg(player, PriceFormat, kit.Price),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        }
                    }, Layer + $".Kit.{kit.ID}.Sale");
                }
                else
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = _config.UI.KitAvailable.AnchorMin,
                            AnchorMax = _config.UI.KitAvailable.AnchorMax,
                            OffsetMin = _config.UI.KitAvailable.OffsetMin,
                            OffsetMax = _config.UI.KitAvailable.OffsetMax
                        },
                        Text =
                        {
                            Text = Msg(player, KitAvailableTitle),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Color = _colorFour
                        }
                    }, Layer + $".Kit.{kit.ID}");
                }
            }
            else
            {
                var time = TimeSpan.FromSeconds(playerData.Cooldown - GetCurrentTime());

                if (kit.Amount > 0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = _config.UI.KitAmountCooldown.AnchorMin,
                            AnchorMax = _config.UI.KitAmountCooldown.AnchorMax,
                            OffsetMin = _config.UI.KitAmountCooldown.OffsetMin,
                            OffsetMax = _config.UI.KitAmountCooldown.OffsetMax
                        },
                        Text =
                        {
                            Text = $"{FormatShortTime(time)}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        }
                    }, Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Cooldown");
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = _config.UI.KitCooldown.AnchorMin, AnchorMax = _config.UI.KitCooldown.AnchorMax,
                            OffsetMin = _config.UI.KitCooldown.OffsetMin, OffsetMax = _config.UI.KitCooldown.OffsetMax
                        },
                        Image = {Color = HexToCuiColor(kit.Color)}
                    }, Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Cooldown");

                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Text =
                        {
                            Text = $"{FormatShortTime(time)}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Color = "1 1 1 1"
                        }
                    }, Layer + $".Kit.{kit.ID}.Cooldown");
                }
            }

            if (kit.Amount > 0)
            {
                var amount = Mathf.Min(kit.Amount, 9);

                var hasAmount = kit.Amount > 9 ? 9 * playerData.Amount / kit.Amount : playerData.Amount;

                var width = amount == 1
                    ? _config.UI.KitAmount.Width
                    : _config.UI.KitAmount.Width / amount * 0.9f;

                var margin = (_config.UI.KitAmount.Width - width * amount) / (amount - 1);

                var xSwitch = -(_config.UI.KitAmount.Width / 2f);

                for (var i = 0; i < amount; i++)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = _config.UI.KitAmount.AnchorMin,
                            AnchorMax = _config.UI.KitAmount.AnchorMax,
                            OffsetMin = $"{xSwitch} {_config.UI.KitAmount.OffsetMin}",
                            OffsetMax = $"{xSwitch + width} {_config.UI.KitAmount.OffsetMax}"
                        },
                        Image =
                        {
                            Color = i < hasAmount ? HexToCuiColor(kit.Color) : _colorTwo
                        }
                    }, Layer + $".Kit.{kit.ID}");

                    xSwitch += width + margin;
                }
            }
        }

        #endregion

        #region Kit Helpers

        private void GiveKit(BasePlayer player, Kit kit, bool force = false)
        {
            if (player == null || kit == null) return;

            if (Interface.Oxide.CallHook("canRedeemKit", player) != null)
                return;

            if (!force && !string.IsNullOrEmpty(kit.Permission) &&
                !permission.UserHasPermission(player.UserIDString, kit.Permission))
            {
                ErrorUi(player, Msg(player, NoPermission));
                return;
            }

            if (!force && _config.BlockBuilding && !player.CanBuild())
            {
                ErrorUi(player, Msg(player, BBlocked));
                return;
            }

            var currentTime = GetCurrentTime();

            if (!force && kit.CooldownAfterWipe > 0)
            {
                var leftTime = UnBlockTime(kit.CooldownAfterWipe) - currentTime;
                if (leftTime > 0)
                {
                    ErrorUi(player,
                        Msg(player, KitCooldown,
                            FormatShortTime(TimeSpan.FromSeconds(leftTime))));

                    return;
                }
            }

            if (!force && _config.UseNoEscape && !_config.NoEscapeWhiteList.Contains(kit.Name))
            {
                if (RaidBlocked(player))
                {
                    ErrorUi(player, Msg(player, NoEscapeRaidBlocked));
                    return;
                }

                if (CombatBlocked(player))
                {
                    ErrorUi(player, Msg(player, NoEscapeCombatBlocked));
                    return;
                }
            }

            var playerData = GetPlayerData(player.userID, kit.Name);

            if (!force && kit.Amount > 0 && playerData.Amount >= kit.Amount)
            {
                ErrorUi(player, Msg(player, KitLimit));
                return;
            }

            if (!force && kit.Cooldown > 0)
                if (playerData.Cooldown > currentTime)
                {
                    ErrorUi(player,
                        Msg(player, KitCooldown,
                            FormatShortTime(TimeSpan.FromSeconds(playerData.Cooldown - currentTime))));
                    return;
                }

            if (!force && kit.Sale && !_config.Economy.RemoveBalance(player, kit.Price))
            {
                SendNotify(player, NotMoney, 1);
                return;
            }

            if (CopyPaste && !string.IsNullOrEmpty(kit.Building))
            {
                var success = CopyPaste?.Call("TryPasteFromSteamId", player.userID, kit.Building,
                    _config.CopyPasteParameters.ToArray());
                if (success is string)
                {
                    SendNotify(player, BuildError, 1);
                    return;
                }
            }

            var beltcount = kit.Items.Count(i => i.Container == "belt");
            var wearcount = kit.Items.Count(i => i.Container == "wear");
            var maincount = kit.Items.Count(i => i.Container == "main");
            var totalcount = beltcount + wearcount + maincount;
            if (player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count < beltcount ||
                player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count < wearcount ||
                player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count < maincount)
                if (totalcount > player.inventory.containerMain.capacity -
                    player.inventory.containerMain.itemList.Count)
                {
                    ErrorUi(player, Msg(player, NotEnoughSpace));
                    return;
                }

            kit.Items.ForEach(item => item?.Get(player));

            if (!force && kit.Amount > 0) playerData.Amount += 1;

            if (!force && kit.Cooldown > 0)
                playerData.Cooldown = GetCurrentTime() + GetCooldown(kit.Cooldown, player);

            SendNotify(player, KitClaimed, 0, kit.DisplayName);

            _openGUI.Remove(player);

            CuiHelper.DestroyUi(player, Layer);

            Interface.CallHook("OnKitRedeemed", player, kit.Name);

            Log(player, kit.Name);
        }

        private double GetCooldown(double cooldown, BasePlayer player)
        {
            var cd = Interface.Oxide.CallHook("OnKitCooldown", player, cooldown) as double?;
            if (cd != null) return (double) cd;

            return cooldown;
        }

        private List<KitItem> GetPlayerItems(BasePlayer player)
        {
            var kititems = new List<KitItem>();

            player.inventory.containerWear.itemList.ForEach(item =>
            {
                if (item == null || item.IsLocked()) return;
                kititems.Add(ItemToKit(item, "wear"));
            });

            player.inventory.containerMain.itemList.ForEach(item =>
            {
                if (item == null || item.IsLocked()) return;
                kititems.Add(ItemToKit(item, "main"));
            });

            player.inventory.containerBelt.itemList.ForEach(item =>
            {
                if (item == null || item.IsLocked()) return;
                kititems.Add(ItemToKit(item, "belt"));
            });

            return kititems;
        }

        private KitItem ItemToKit(Item item, string container)
        {
            var kitem = new KitItem
            {
                Amount = item.amount,
                Container = container,
                SkinID = item.skin,
                Blueprint = item.blueprintTarget,
                ShortName = item.info.shortname,
                Condition = item.condition,
                Weapon = null,
                Content = null,
                Chance = 100,
                Command = string.Empty,
                Position = item.position
            };

            if (item.info.category == ItemCategory.Weapon)
            {
                var weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                    kitem.Weapon = new Weapon
                    {
                        ammoType = weapon.primaryMagazine.ammoType.shortname,
                        ammoAmount = weapon.primaryMagazine.contents
                    };
            }

            if (item.contents != null)
                kitem.Content = item.contents.itemList.Select(cont => new ItemContent
                {
                    Amount = cont.amount,
                    Condition = cont.condition,
                    ShortName = cont.info.shortname
                }).ToList();

            return kitem;
        }

        #endregion

        #region Utils

        private bool RaidBlocked(BasePlayer player)
        {
            return Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player) ?? false);
        }

        private bool CombatBlocked(BasePlayer player)
        {
            return Convert.ToBoolean(NoEscape?.Call("IsCombatBlocked", player) ?? false);
        }

        private void StopEditing(BasePlayer player)
        {
            _itemEditing.Remove(player);
            _kitEditing.Remove(player);
        }

        private void FillCategories()
        {
            ItemManager.itemList.ForEach(item =>
            {
                var itemCategory = item.category.ToString();

                if (_itemsCategories.ContainsKey(itemCategory))
                {
                    if (!_itemsCategories[itemCategory].Contains(item.shortname))
                        _itemsCategories[itemCategory].Add(item.shortname);
                }
                else
                {
                    _itemsCategories.Add(itemCategory, new List<string> {item.shortname});
                }
            });
        }

        private void HandleUi()
        {
            var toRemove = Pool.GetList<BasePlayer>();

            foreach (var check in _openGUI)
            {
                var player = check.Key;
                if (player == null || !player.IsConnected)
                {
                    toRemove.Add(player);
                    continue;
                }

                var container = new CuiElementContainer();

                check.Value.ForEach(kit => RefreshKitUi(ref container, player, kit));

                CuiHelper.AddUi(player, container);
            }

            toRemove.ForEach(x => _openGUI.Remove(x));
            Pool.FreeList(ref toRemove);
        }

        private void FixItemsPositions()
        {
            _data.Kits.ForEach(kit =>
            {
                var positions = new Dictionary<string, int>
                {
                    ["belt"] = 0,
                    ["main"] = 0,
                    ["wear"] = 0
                };

                kit.Items.ForEach(item =>
                {
                    if (positions.ContainsKey(item.Container) && item.Position == -1)
                    {
                        item.Position = positions[item.Container];

                        positions[item.Container] += 1;
                    }
                });
            });

            SaveKits();
        }

        private void LoadImages()
        {
            if (!ImageLibrary)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();

                var itemIcons = new List<KeyValuePair<string, ulong>>();

                _data.Kits.ForEach(kit =>
                {
                    if (!string.IsNullOrEmpty(kit.Image)
                        && !imagesList.ContainsKey(kit.Image))
                        imagesList.Add(kit.Image, kit.Image);

                    kit.Items.ForEach(item =>
                    {
                        if (!string.IsNullOrEmpty(item.Image) && !imagesList.ContainsKey(item.Image))
                            imagesList.Add(item.Image, item.Image);

                        itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.SkinID));
                    });
                });

                itemIcons.AddRange(_itemsCategories.SelectMany(x => x.Value)
                    .Select(item => new KeyValuePair<string, ulong>(item, 0)));

                if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }
        }

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
        }

        private static string FormatShortTime(TimeSpan time)
        {
            return time.ToShortString();
        }

        private static void CreateOutLine(ref CuiElementContainer container, string parent, string color,
            float size = 2)
        {
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{size} 0",
                        OffsetMax = $"-{size} {size}"
                    },
                    Image = {Color = color}
                },
                parent);
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{size} -{size}",
                        OffsetMax = $"-{size} 0"
                    },
                    Image = {Color = color}
                },
                parent);
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0 1",
                        OffsetMin = "0 0",
                        OffsetMax = $"{size} 0"
                    },
                    Image = {Color = color}
                },
                parent);
            container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"-{size} 0",
                        OffsetMax = "0 0"
                    },
                    Image = {Color = color}
                },
                parent);
        }

        private List<Kit> GetAvailableKits(BasePlayer player, string targetId = "0", bool showAll = false,
            bool checkAmount = true)
        {
            return IsAdmin(player) && showAll
                ? _data.Kits
                : _data.Kits.FindAll(x =>
                    !x.Hide &&
                    (targetId == "0" || _config.NpcKits.ContainsKey(targetId) &&
                        _config.NpcKits[targetId].Kits.Contains(x.Name)) &&
                    (!checkAmount || x.Amount == 0 || x.Amount > 0 &&
                        GetPlayerData(player.userID, x.Name).Amount < x.Amount) &&
                    (_config.ShowAllKits || string.IsNullOrEmpty(x.Permission) ||
                     permission.UserHasPermission(player.UserIDString, x.Permission)));
        }

        private List<Kit> GetAutoKits(BasePlayer player)
        {
            return _data.Kits
                .FindAll(kit => kit.Name == "autokit" || _config.AutoKits.Contains(kit.Name) &&
                    (string.IsNullOrEmpty(kit.Permission) ||
                     permission.UserHasPermission(
                         player.UserIDString, kit.Permission)));
        }

        private double UnBlockTime(double amount)
        {
            return TimeSpan.FromTicks(SaveRestore.SaveCreatedTime.ToUniversalTime().Ticks).TotalSeconds + amount;
        }

        private static double GetCurrentTime()
        {
            return TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalSeconds;
        }

        private bool IsAdmin(BasePlayer player)
        {
            return player != null && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermAdmin));
        }

        #region Images

        #region SteampoweredAPI

        private class SteampoweredResult
        {
            public Response response;

            public class Response
            {
                [JsonProperty("result")] public int result;

                [JsonProperty("resultcount")] public int resultcount;

                [JsonProperty("publishedfiledetails")] public List<PublishedFiled> publishedfiledetails;

                public class PublishedFiled
                {
                    [JsonProperty("publishedfileid")] public ulong publishedfileid;

                    [JsonProperty("result")] public int result;

                    [JsonProperty("creator")] public string creator;

                    [JsonProperty("creator_app_id")] public int creator_app_id;

                    [JsonProperty("consumer_app_id")] public int consumer_app_id;

                    [JsonProperty("filename")] public string filename;

                    [JsonProperty("file_size")] public int file_size;

                    [JsonProperty("preview_url")] public string preview_url;

                    [JsonProperty("hcontent_preview")] public string hcontent_preview;

                    [JsonProperty("title")] public string title;

                    [JsonProperty("description")] public string description;

                    [JsonProperty("time_created")] public int time_created;

                    [JsonProperty("time_updated")] public int time_updated;

                    [JsonProperty("visibility")] public int visibility;

                    [JsonProperty("banned")] public int banned;

                    [JsonProperty("ban_reason")] public string ban_reason;

                    [JsonProperty("subscriptions")] public int subscriptions;

                    [JsonProperty("favorited")] public int favorited;

                    [JsonProperty("lifetime_subscriptions")]
                    public int lifetime_subscriptions;

                    [JsonProperty("lifetime_favorited")] public int lifetime_favorited;

                    [JsonProperty("views")] public int views;

                    [JsonProperty("tags")] public List<Tag> tags;

                    public class Tag
                    {
                        [JsonProperty("tag")] public string tag;
                    }
                }
            }
        }

        #endregion

        private string GetItemImage(string shortname, ulong skinID = 0)
        {
            if (skinID > 0)
                if (ImageLibrary.Call<bool>("HasImage", shortname, skinID) == false &&
                    ImageLibrary.Call<Dictionary<string, object>>("GetSkinInfo", shortname, skinID) == null)
                {
                    if (string.IsNullOrEmpty(_config.SteamWebApiKey) || _config.SteamWebApiKey.Length != 32)
                    {
                        PrintError("Steam Web API key not set! Check the configuration!");
                        return ImageLibrary.Call<string>("GetImage", shortname);
                    }

                    webrequest.Enqueue("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                        $"key={_config.SteamWebApiKey}&itemcount=1&publishedfileids%5B0%5D={skinID}",
                        (code, response) =>
                        {
                            if (code != 200 || response == null)
                            {
                                PrintError(
                                    $"Image failed to download! Code HTTP error: {code} - Image Name: {shortname} - Image skinID: {skinID} - Response: {response}");
                                return;
                            }

                            var sr = JsonConvert.DeserializeObject<SteampoweredResult>(response);
                            if (sr == null || sr.response.result == 0 || sr.response.resultcount == 0)
                            {
                                PrintError(
                                    $"Image failed to download! Error: Parse JSON response - Image Name: {shortname} - Image skinID: {skinID} - Response: {response}");
                                return;
                            }

                            foreach (var publishedfiled in sr.response.publishedfiledetails)
                                ImageLibrary.Call("AddImage", publishedfiled.preview_url, shortname, skinID);
                        }, this, RequestMethod.POST);

                    return ImageLibrary.Call<string>("GetImage", "LOADING");
                }

            return ImageLibrary.Call<string>("GetImage", shortname, skinID);
        }

        #endregion

        #endregion

        #region Log

        private void Log(BasePlayer player, string kitname)
        {
            if (player == null) return;

            var text = $"{player.displayName}[{player.UserIDString}] - Received Kit: {kitname}";

            if (_config.Logs.Console)
                Puts(text);

            if (_config.Logs.Console)
                LogToFile(Name, $"[{DateTime.Now}] {text}", this);
        }

        #endregion

        #region Lang

        private const string
            EditRemoveField = "EditRemoveField",
            ChangeAutoKitOn = "ChangeAutoKitOn",
            ChangeAutoKitOff = "ChangeAutoKitOff",
            NoEscapeCombatBlocked = "NoEscapeCombatBlocked",
            NoEscapeRaidBlocked = "NoEscapeRaidBlocked",
            NotMoney = "NotMoney",
            PriceFormat = "PriceFormat",
            KitExist = "KitExist",
            KitNotExist = "KitNotExist",
            KitRemoved = "KitRemoved",
            AccessDenied = "AccessDenied",
            KitLimit = "KitLimit",
            KitCooldown = "KitCooldown",
            KitCreate = "KitCreate",
            KitClaimed = "KitClaimed",
            NotEnoughSpace = "NotEnoughtSpace",
            NotifyTitle = "NotifyTitle",
            Close = "Close",
            MainTitle = "MainTitle",
            Back = "Back",
            Next = "Next",
            NotAvailableKits = "NoAvailabeKits",
            CreateKit = "CreateKit",
            ListKits = "ListKits",
            ShowAll = "ShowAll",
            KitInfo = "KitInfo",
            KitTake = "KitGet",
            ComeBack = "ComeBack",
            Edit = "Edit",
            ContainerMain = "ContainerMain",
            ContainerWear = "ContaineWear",
            ContainerBelt = "ContainerBelt",
            CreateOrEditKit = "CreateOrEditKit",
            MainMenu = "MainMenu",
            EnableKit = "EnableKit",
            AutoKit = "AutoKit",
            EnabledSale = "EnabledSale",
            SaveKit = "SaveKit",
            CopyItems = "CopyItems",
            RemoveKit = "RemoveKit",
            EditingTitle = "EditingTitle",
            ItemName = "ItemName",
            CmdName = "CmdName",
            BtnSelect = "BtnSelect",
            BluePrint = "BluePrint",
            BtnSave = "BtnSave",
            ItemSearch = "ItemSearch",
            BtnClose = "BtnClose",
            KitAvailableTitle = "KitAvailable",
            KitsList = "KitsList",
            KitsHelp = "KitsHelp",
            KitNotFound = "KitNotFound",
            RemoveItem = "RemoveItem",
            NoPermission = "NoPermission",
            BuildError = "BuildError",
            BBlocked = "BuildingBlocked",
            NoPermissionDescription = "NoPermissionDescription";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [KitExist] = "Kit with the same name already exist",
                [KitCreate] = "You have created a new kit - {0}",
                [KitNotExist] = "This kit doesn't exist",
                [KitRemoved] = "Kit {0} was removed",
                [AccessDenied] = "Access denied",
                [KitLimit] = "Usage limite reached",
                [KitCooldown] = "You will be able to use this kit after: {0}",
                [NotEnoughSpace] = "Can't redeem kit. Not enought space",
                [KitClaimed] = "You have claimed kit - {0}",
                [NotifyTitle] = "KITS",
                [Close] = "✕",
                [MainTitle] = "Kits",
                [Back] = "Back",
                [Next] = "Next",
                [NotAvailableKits] = "NO KITS AVAILABLE FOR YOU :(",
                [CreateKit] = "Create Kit",
                [ListKits] = "List of kits",
                [ShowAll] = "Show all",
                [KitInfo] = "i",
                [KitTake] = "Take",
                [ComeBack] = "Come back",
                [Edit] = "Edit",
                [ContainerMain] = "Main",
                [ContainerWear] = "Wear",
                [ContainerBelt] = "Belt",
                [CreateOrEditKit] = "Create/Edit Kit",
                [MainMenu] = "Main menu",
                [EnableKit] = "Enable kit",
                [AutoKit] = "Auto kit",
                [EnabledSale] = "Enable sale",
                [SaveKit] = "Save kit",
                [CopyItems] = "Copy items from inventory",
                [RemoveKit] = "Remove kit",
                [EditingTitle] = "Item editing",
                [ItemName] = "Item",
                [CmdName] = "Command",
                [BtnSelect] = "Select",
                [BluePrint] = "Blueprint",
                [BtnSave] = "Save",
                [ItemSearch] = "Item search",
                [BtnClose] = "CLOSE",
                [KitAvailableTitle] = "KIT AVAILABLE\nTO RECEIVE",
                [KitsList] = "List of kits: {0}",
                [KitsHelp] =
                    "KITS HELP\n- /{0} help - get help with kits\n- /{0} list - get a list of available kits\n- /{0} [name] - get the kit",
                [KitNotFound] = "Kit '{0}' not found",
                [RemoveItem] = "✕",
                [NoPermission] = "You don't have permission to get this kit",
                [BuildError] = "Can't place the building here",
                [BBlocked] = "Cannot do that while building blocked.",
                [NoPermissionDescription] = "PURCHASE THIS KIT AT\nwhiteout.gamestores.app",
                [PriceFormat] = "{0}$",
                [NotMoney] = "You don't have enough money!",
                [NoEscapeRaidBlocked] = "You cannot take this kit when you are raid blocked",
                [NoEscapeCombatBlocked] = "You cannot take this kit when you are combat blocked",
                [ChangeAutoKitOn] = "You have enabled autokits",
                [ChangeAutoKitOff] = "You have disabled autokits",
                [EditRemoveField] = "✕"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [KitExist] = "Набор с похожим названием уже существует",
                [KitCreate] = "Вы создали новый набор - {0}",
                [KitNotExist] = "Набор не найден",
                [KitRemoved] = "Набор {0} удалён",
                [AccessDenied] = "Доступ запрещён",
                [KitLimit] = "Достигнут лимит использования",
                [KitCooldown] = "Вы сможете использовать этот набор после: {0}",
                [NotEnoughSpace] = "Невозможно получить набор. Не достаточно места в инвентаре",
                [KitClaimed] = "Вы получили набор - {0}",
                [NotifyTitle] = "KITS",
                [Close] = "✕",
                [MainTitle] = "Наборы",
                [Back] = "Назад",
                [Next] = "Вперёд",
                [NotAvailableKits] = "ДЛЯ ВАС НЕТ ДОСТУПНЫХ НАБОРОВ :(",
                [CreateKit] = "Создать набор",
                [ListKits] = "Список наборов",
                [ShowAll] = "Показать все",
                [KitInfo] = "i",
                [KitTake] = "Получить",
                [ComeBack] = "Назад",
                [Edit] = "Редактировать",
                [ContainerMain] = "Основной",
                [ContainerWear] = "Одежда",
                [ContainerBelt] = "Пояс",
                [CreateOrEditKit] = "Создать/Изменить Набор",
                [MainMenu] = "Основное меню",
                [EnableKit] = "Включить",
                [AutoKit] = "Автокит",
                [EnabledSale] = "Включить продажу",
                [SaveKit] = "Сохранить набор",
                [CopyItems] = "Копировать предметы из инвентаря",
                [RemoveKit] = "Удалить набор",
                [EditingTitle] = "Редактирование предмета",
                [ItemName] = "Item",
                [CmdName] = "Command",
                [BtnSelect] = "Выбрать",
                [BluePrint] = "Blueprint",
                [BtnSave] = "Сохранить",
                [ItemSearch] = "Поиск предмета",
                [BtnClose] = "ЗАКРЫТЬ",
                [KitAvailableTitle] = "НАБОР ДОСТУПЕН\nДЛЯ ПОЛУЧЕНИЯ",
                [KitsList] = "Список наборов: {0}",
                [KitsHelp] =
                    "ИНФОРМАЦИЯ О НАБОРАх\n- /{0} help - получить информацию о наборах\n- /{0} list - получить список доступных наборов\n- /{0} [name] - получить набор",
                [KitNotFound] = "Набор '{0}' не найден",
                [RemoveItem] = "✕",
                [NoPermission] = "У вас нет прав на получение этого набора",
                [BuildError] = "Здесть невозможность установить строение",
                [BBlocked] = "Получение набора в Building Block запрещено!",
                [NoPermissionDescription] = "КУПИТЕ ЭТОТ НАБОР НА\nwhiteout.gamestores.app",
                [PriceFormat] = "{0}$",
                [NotMoney] = "У вас недостаточно денег!",
                [NoEscapeRaidBlocked] = "У вас блокировка рейда! Вы не можете взять этот набор",
                [NoEscapeCombatBlocked] = "У вас блокировка боя! Вы не можете взять этот набор",
                [ChangeAutoKitOn] = "Вы включили автокиты",
                [ChangeAutoKitOff] = "Вы выключили автокиты",
                [EditRemoveField] = "✕"
            }, this, "ru");
        }

        private string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, userid), obj);
        }

        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            SendReply(player, Msg(key, player.UserIDString, obj));
        }

        private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
        {
            if (_config.UseNotify && (Notify != null || UINotify != null))
                Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion

        #region API

        private void GetKitNames(List<string> list)
        {
            list.AddRange(GetAllKits());
        }

        private string[] GetAllKits()
        {
            return _data.Kits.Select(kit => kit.Name).ToArray();
        }

        private object GetKitInfo(string name)
        {
            return GetKitObject(name);
        }

        private string[] GetKitContents(string name)
        {
            var kit = _data.Kits.Find(x => x.Name == name);
            if (kit == null) return null;

            var items = new List<string>();
            foreach (var item in kit.Items)
            {
                var itemstring = $"{item.ShortName}_{item.Amount}";
                if (item.Content.Count > 0)
                    itemstring = item.Content.Aggregate(itemstring, (current, mod) => current + $"_{mod.ShortName}");

                items.Add(itemstring);
            }

            return items.ToArray();
        }

        private double GetKitCooldown(string name)
        {
            return _data.Kits.Find(x => x.Name == name)?.Cooldown ?? 0;
        }

        private double PlayerKitCooldown(ulong ID, string name)
        {
            return GetPlayerData(ID, name).Cooldown;
        }

        private int KitMax(string name)
        {
            return _data.Kits.Find(x => x.Name == name)?.Amount ?? 0;
        }

        private double PlayerKitMax(ulong ID, string name)
        {
            return GetPlayerData(ID, name)?.Amount ?? 0;
        }

        private string KitImage(string name)
        {
            return _data.Kits.Find(x => x.Name == name)?.Image ?? string.Empty;
        }

        private string GetKitImage(string name)
        {
            return KitImage(name);
        }

        private string GetKitDescription(string name)
        {
            return _data.Kits.Find(x => x.Name == name)?.Description ?? string.Empty;
        }

        private int GetKitMaxUses(string name)
        {
            return _data.Kits.Find(x => x.Name == name)?.Amount ?? 0;
        }

        private int GetPlayerKitUses(ulong userId, string name)
        {
            Dictionary<string, KitData> kits;
            if (!_playerData.TryGetValue(userId, out kits)) return 0;

            KitData data;
            return kits.TryGetValue(name, out data) ? data.Amount : 0;
        }

        private void SetPlayerKitUses(ulong userId, string name, int amount)
        {
            Dictionary<string, KitData> kits;
            if (!_playerData.TryGetValue(userId, out kits)) return;

            KitData data;
            if (kits.TryGetValue(name, out data))
                data.Amount = amount;
        }

        private double GetPlayerKitCooldown(ulong userId, string name)
        {
            var data = GetPlayerData(userId, name);
            if (data == null) return 0;

            return Mathf.Max((float) (data.Cooldown - GetCurrentTime()), 0f);
        }

        private void SetPlayerCooldown(ulong userId, string name, int amount)
        {
            var data = GetPlayerData(userId, name);
            if (data == null) return;

            data.Cooldown = GetCurrentTime() + GetCooldown(amount, BasePlayer.FindByID(userId));
        }

        private void GiveKit(BasePlayer player, string name)
        {
            GiveKit(player, _data.Kits.Find(x => x.Name == name), true);
        }

        private bool isKit(string name)
        {
            return IsKit(name);
        }

        private bool IsKit(string name)
        {
            return _data.Kits.Exists(x => x.Name == name);
        }

        private JObject GetKitObject(string name)
        {
            var kit = _data.Kits.Find(x => x.Name == name);
            if (kit == null) return null;

            return new JObject
            {
                ["name"] = kit.Name,
                ["displayname"] = kit.DisplayName,
                ["color"] = kit.Color,
                ["permission"] = kit.Permission,
                ["image"] = kit.Image,
                ["hide"] = kit.Hide,
                ["amount"] = kit.Amount,
                ["cooldown"] = kit.Cooldown,
                ["items"] = new JArray(kit.Items.Select(itemEntry => new JObject
                {
                    ["type"] = itemEntry.Type.ToString(),
                    ["command"] = itemEntry.Command,
                    ["shortname"] = itemEntry.ShortName,
                    ["itemid"] = ItemManager.FindItemDefinition(itemEntry.ShortName)?.itemid ?? 0,
                    ["amount"] = itemEntry.Amount,
                    ["blueprint"] = itemEntry.Blueprint,
                    ["skinid"] = itemEntry.SkinID,
                    ["container"] = itemEntry.Container,
                    ["condition"] = itemEntry.Condition,
                    ["chance"] = itemEntry.Chance,
                    ["mods"] = new JArray(itemEntry.Content?.Select(x =>
                        ItemManager.FindItemDefinition(x.ShortName).itemid) ?? new List<int>())
                }))
            };
        }

        #endregion

        #region Convert

        [ConsoleCommand("kits.convert")]
        private void OldKitsConvert(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            OldData oldKits = null;

            try
            {
                oldKits = Interface.Oxide.DataFileSystem.ReadObject<OldData>("Kits/kits_data");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            var amount = 0;

            oldKits?._kits.ToList().ForEach(oldKit =>
            {
                var kit = new Kit
                {
                    ID = ++_lastKitID,
                    Name = oldKit.Value.Name,
                    DisplayName = oldKit.Value.Name,
                    Permission = oldKit.Value.RequiredPermission,
                    Amount = oldKit.Value.MaximumUses,
                    Cooldown = oldKit.Value.Cooldown,
                    Description = oldKit.Value.Description,
                    Hide = oldKit.Value.IsHidden,
                    Building = oldKit.Value.CopyPasteFile,
                    Image = oldKit.Value.KitImage,
                    Color = _config.KitColor,
                    Items = new List<KitItem>()
                };

                foreach (var item in oldKit.Value.MainItems)
                    kit.Items.Add(KitItem.FromOld(item, "main"));

                foreach (var item in oldKit.Value.WearItems)
                    kit.Items.Add(KitItem.FromOld(item, "wear"));

                foreach (var item in oldKit.Value.BeltItems)
                    kit.Items.Add(KitItem.FromOld(item, "belt"));

                _data.Kits.Add(kit);

                amount++;
            });

            Puts($"{amount} kits was converted!");

            SaveKits();
        }

        private class OldData
        {
            [JsonProperty] public readonly Dictionary<string, OldKitsData> _kits =
                new Dictionary<string, OldKitsData>(StringComparer.OrdinalIgnoreCase);
        }

        private class OldKitsData
        {
            public string Name;
            public string Description;
            public string RequiredPermission;

            public int MaximumUses;
            public int RequiredAuth;
            public int Cooldown;
            public int Cost;

            public bool IsHidden;

            public string CopyPasteFile;
            public string KitImage;

            public ItemData[] MainItems;
            public ItemData[] WearItems;
            public ItemData[] BeltItems;
        }

        private class ItemData
        {
            public string Shortname;

            public ulong Skin;

            public int Amount;

            public float Condition;

            public float MaxCondition;

            public int Ammo;

            public string Ammotype;

            public int Position;

            public int Frequency;

            public string BlueprintShortname;

            public ItemData[] Contents;
        }

        #endregion
    }
}