using System.Runtime.CompilerServices;
using System.Security;
using System.Data.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections;
using Facepunch;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Rust;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("BCraftSystem", "King", "1.0.0")]
    public class BCraftSystem : RustPlugin
    {
        #region [Vars]

        private static BCraftSystem plugin;
        private const string Layer = "BCraftSystem.Layer";

        #endregion

        #region [Config]
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                config.PluginVersion = Version;
                if (Version == new VersionNumber(1, 0, 0))
                {
                    //
                }

                PrintWarning("Config checked completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class CraftConf
        {
            [JsonProperty(PropertyName = "Название")]
            public string Title;

            [JsonProperty(PropertyName = "ID")] 
            public int ID;

            [JsonProperty(PropertyName = "Shortname")]
            public string ShortName;

            [JsonProperty(PropertyName = "Количество")]
            public int Amount;

            [JsonProperty(PropertyName = "Скин")] 
            public ulong SkinID;

            [JsonProperty(PropertyName = "Тип (Item/Vehicle/Recycler)")]
            public CraftType Type;

            [JsonProperty(PropertyName = "Префаб")]
            public string Prefab;

            [JsonProperty(PropertyName = "Уровень верстака")]
            public WorkbenchLevel Level;

            [JsonProperty(PropertyName = "Установка на землю")]
            public bool Ground;

            [JsonProperty(PropertyName = "Установка на строение")]
            public bool Structure;

            [JsonProperty(PropertyName = "Предметы для крафта", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ItemForCraft> Items;

            [JsonIgnore]
            public int craftId
            {
                get
                {
                    while (ID == 0)
                    {
                        var val = Random.Range(int.MinValue, int.MaxValue);
                        if (plugin._craftsById.ContainsKey(val)) continue;

                        ID = val;
                    }

                    return ID;
                }
            }

            public Item ToItem()
            {
                var newItem = ItemManager.CreateByName(ShortName, 1, SkinID);
                if (newItem == null) return null;

                return newItem;
            }

            public void Give(BasePlayer player)
            {
                if (player == null) return;

                var item = ToItem();
                if (item == null) return;

                player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            }

            public void Spawn(BasePlayer player, Vector3 pos, Quaternion rot)
            {
                switch (Type)
                {
                    case CraftType.Vehicle:
                        {
                            var entity = GameManager.server.CreateEntity(Prefab, pos,Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - 90, 0));
                            if (entity == null) return;

                            entity.skinID = SkinID;
                            entity.OwnerID = player.userID;
                            entity.Spawn();
                            break;
                        }
                    default:
                        {
                            var entity = GameManager.server.CreateEntity(Prefab, pos, rot);
                            if (entity == null) return;

                            entity.skinID = SkinID;
                            entity.OwnerID = player.userID;
                            entity.Spawn();
                            break;
                        }
                }
            }
        }

        private readonly Dictionary<int, CraftConf> _craftsById = new Dictionary<int, CraftConf>();

        private enum WorkbenchLevel
        {
            None = 0,
            One = 1,
            Two = 2,
            Three = 3
        }

        private enum CraftType
        {
            Vehicle,
            Item,
            Recycler
        }

        private class ItemForCraft
        {
            [JsonProperty(PropertyName = "Изображение")]
            public string Image;

            [JsonProperty(PropertyName = "Shortname")]
            public string ShortName;

            [JsonProperty(PropertyName = "Количество")]
            public int Amount;

            [JsonProperty(PropertyName = "Скин")] 
            public ulong SkinID;
        }

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Настройки крафта")]
            public List<CraftConf> Crafts;

            [JsonProperty("Config version")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    Crafts = new List<CraftConf>
                    {
                        new CraftConf
                        {
                            Title = "Миникоптер",
                            ShortName = "electric.flasherlight",
                            Amount = 1,
                            SkinID = 2080145158,
                            Type = CraftType.Vehicle,
                            Prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                            Level = WorkbenchLevel.None,
                            Ground = true,
                            Structure = false,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "gears",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "roadsigns",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "metal.fragments",
                                    Amount = 2000,
                                    SkinID = 0,
                                },
                            },
                        },
                        new CraftConf
                        {
                            Title = "Мини лодка",
                            ShortName = "coffin.storage",
                            Amount = 1,
                            SkinID = 2080150023,
                            Type = CraftType.Vehicle,
                            Prefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                            Level = WorkbenchLevel.None,
                            Ground = false,
                            Structure = false,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "gears",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "roadsigns",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "metal.fragments",
                                    Amount = 2000,
                                    SkinID = 0,
                                },
                            },
                        },
                        new CraftConf
                        {
                            Title = "Мега лодка",
                            ShortName = "electric.sirenlight",
                            Amount = 1,
                            SkinID = 2080150770,
                            Type = CraftType.Vehicle,
                            Prefab = "assets/content/vehicles/boats/rhib/rhib.prefab",
                            Level = WorkbenchLevel.None,
                            Ground = false,
                            Structure = false,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "gears",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "roadsigns",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "metal.fragments",
                                    Amount = 2000,
                                    SkinID = 0,
                                },
                            },
                        },
                        new CraftConf
                        {
                            Title = "Мега коптер",
                            ShortName = "electric.sirenlight",
                            Amount = 1,
                            SkinID = 2080154394,
                            Type = CraftType.Vehicle,
                            Prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                            Level = WorkbenchLevel.None,
                            Ground = false,
                            Structure = false,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "gears",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "roadsigns",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "metal.fragments",
                                    Amount = 2000,
                                    SkinID = 0,
                                },
                            },
                        },
                        new CraftConf
                        {
                            Title = "Снегоход",
                            ShortName = "electric.sirenlight",
                            Amount = 1,
                            SkinID = 2747934628,
                            Type = CraftType.Vehicle,
                            Prefab = "assets/content/vehicles/snowmobiles/snowmobile.prefab",
                            Level = WorkbenchLevel.None,
                            Ground = false,
                            Structure = false,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "gears",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "roadsigns",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "metal.fragments",
                                    Amount = 2000,
                                    SkinID = 0,
                                },
                            },
                        },
                        new CraftConf
                        {
                            Title = "Переработчик",
                            ShortName = "research.table",
                            Amount = 1,
                            SkinID = 2186833264,
                            Type = CraftType.Recycler,
                            Prefab = "assets/bundled/prefabs/static/recycler_static.prefab",
                            Level = WorkbenchLevel.None,
                            Ground = false,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "gears",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "roadsigns",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "metal.fragments",
                                    Amount = 2000,
                                    SkinID = 0,
                                },
                            },
                        },
                        new CraftConf
                        {
                            Title = "LR 300",
                            ShortName = "rifle.lr300",
                            Amount = 1,
                            SkinID = 0,
                            Type = CraftType.Item,
                            Prefab = "",
                            Level = WorkbenchLevel.None,
                            Ground = false,
                            Structure = true,
                            Items = new List<ItemForCraft>
                            {
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "gears",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "roadsigns",
                                    Amount = 5,
                                    SkinID = 0,
                                },
                                new ItemForCraft
                                {
                                    Image = string.Empty,
                                    ShortName = "metal.fragments",
                                    Amount = 2000,
                                    SkinID = 0,
                                },
                            },
                        },
                    },
                    PluginVersion = new VersionNumber()
                };
            }
        }
        #endregion

        private void OnServerInitialized()
        {
            plugin = this;

            config.Crafts.ForEach(craft => _craftsById[craft.craftId] = craft);

            cmd.AddChatCommand("craft", this, "MainUi");
        }

        private void Unload()
        {
            plugin = null;
        }

        private void OnEntityBuilt(Planner held, GameObject go)
        {
            if (held == null || go == null) return;

            var player = held.GetOwnerPlayer();
            if (player == null) return;

            var entity = go.ToBaseEntity();
            if (entity == null || entity.skinID == 0) return;

            var craft = config.Crafts.Find(x => (x.Type == CraftType.Vehicle || x.Type == CraftType.Recycler) && x.SkinID == entity.skinID);
            if (craft == null) return;

            var transform = entity.transform;

            var itemName = craft.Title;

            NextTick(() =>
            {
                if (entity != null)
                    entity.Kill();
            });

            RaycastHit rHit;
            if (Physics.Raycast(transform.position + new Vector3(0, 0.1f, 0), Vector3.down, out rHit, 4f,LayerMask.GetMask("Construction")) && rHit.GetEntity() != null)
            {
                if (!craft.Structure)
                {
                    player.ChatMessage($"{itemName} нельзя установить на строениях!");
                    GiveCraft(player, craft);
                    return;
                }
            }
            else
            {
                if (!craft.Ground)
                {
                    player.ChatMessage($"{itemName} нельзя установить на землю!");
                    GiveCraft(player, craft);
                    return;
                }
            }

            craft.Spawn(player, transform.position, transform.rotation);
        }

        #region [Ui]

        private void MainUi(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Material = "assets/content/ui/uibackgroundblur.mat", Color = "0 0 0 0.77" }
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Close = Layer }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0.36 0.33 0.28 0.3", Material = "assets/icons/greyout.mat", Close = Layer }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-344 -50", OffsetMax = "347 260" },
                Image = { Color = "0.3773585 0.3755785 0.3755785 0.3407843", Material = "assets/icons/greyout.mat" }
            }, Layer, Layer + ".Main");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.99", },
                Text = { Text = "Список предметов для крафта", Align = TextAnchor.UpperCenter, Font = "robotocondensed-bold.ttf", FontSize = 24, Color = "1 1 1 0.65" }
            }, Layer + ".Main");

            foreach (var check in config.Crafts.Select((y, t) => new { A = y, B = t }).Take(10))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{0.018 + check.B * 0.196 - Math.Floor((float) check.B / 5) * 5 * 0.196} {0.47 - Math.Floor((float) check.B/ 5) * 0.44}",
                                        AnchorMax = $"{0.197 + check.B * 0.196 - Math.Floor((float) check.B / 5) * 5 * 0.196} {0.873 - Math.Floor((float) check.B / 5) * 0.44}", },
                    Image = { Color = "0.09 0.09 0.09 0.45", Material = "assets/icons/greyout.mat" }
                }, Layer + ".Main", Layer + ".Main" + $".Craft({check.B})");

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Main" + $".Craft({check.B})",
                    Components =
                    {
                        new CuiImageComponent { ItemId = FindItemID(check.A.ShortName), SkinId = check.A.SkinID },
                        new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-36 -84", OffsetMax = "36 -12" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.32" },
                    Text = { Text = $"{check.A.Title}", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "1 1 1 1" }
                }, Layer + ".Main" + $".Craft({check.B})");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Command = $"UI_BCrafts craftUi {check.A.ID}" },
                    Text = { Text = "" }
                }, Layer + ".Main" + $".Craft({check.B})");
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-344 -255", OffsetMax = "347 -55" },
                Image = { Color = "0.3773585 0.3755785 0.3755785 0.3407843", Material = "assets/icons/greyout.mat" }
            }, Layer, Layer + ".CraftPanel");

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void CraftUi(BasePlayer player, int ID)
        {
            var craft = config.Crafts.FirstOrDefault(p => p.ID == ID);
            if (craft == null) return;

            var allItems = player.inventory.AllItems();
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0" },
            }, Layer + ".CraftPanel", Layer + ".CraftPanel" + ".Layer" );

            int itemsPage = 0;
            var width = 130f;
            var margin = -25f;
            var notItem = false;
            var maxAmount = 7;
            var items = craft.Items.Skip(itemsPage * maxAmount).Take(maxAmount).ToList();
            var xSwitch = - (items.Count * width + (items.Count - 1) * margin) / 2f;

            foreach (var check in items.Select((i, t) => new {A = i, B = t}))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.5 1", AnchorMax = $"0.5 1", OffsetMin = $"{xSwitch} -140", OffsetMax = $"{xSwitch + width} -15" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".CraftPanel" + ".Layer", Layer + ".CraftPanel" + ".Layer" + $".Craft({check.B})");

                container.Add(new CuiElement
                {
                    Parent = Layer + ".CraftPanel" + ".Layer" + $".Craft({check.B})",
                    Components =
                    {
                        new CuiImageComponent { ItemId = FindItemID(check.A.ShortName), SkinId = check.A.SkinID },
                        new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-44 -78", OffsetMax = "44 14" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.95 0.55" },
                    Text = { Text = $"{ItemManager.FindItemDefinition(check.A.ShortName)?.displayName.translated}", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 1" }
                }, Layer + ".CraftPanel" + ".Layer" + $".Craft({check.B})");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.95 0.14" },
                    Text = { Text = $"{check.A.Amount} шт", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 1" }
                }, Layer + ".CraftPanel" + ".Layer" + $".Craft({check.B})");

                var hasAmount = HasAmount(allItems, check.A.ShortName, check.A.SkinID, check.A.Amount);

                if (!hasAmount)
                    notItem = true;
    
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-40 20", OffsetMax = "40 22" },
                    Image = { Color = hasAmount ? "0.00 0.84 0.47 1.00" : "0.80 0.27 0.20 1.00", Material = "assets/icons/greyout.mat" }
                }, Layer + ".CraftPanel" + ".Layer" + $".Craft({check.B})");

                xSwitch += width + margin;
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-64 -195", OffsetMax = $"64 -150" },
                Text = { Text = "КРАФТ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 12, Color = notItem ? "1 1 1 0.7" : "1 1 1 1" },
                Button = { Color = notItem ? "0 0 0 0.40" : "0.00 0.84 0.47 0.65", Material = "assets/icons/greyout.mat", Command = notItem ? "" : $"UI_BCrafts trycraft {ID}" }
            }, Layer + ".CraftPanel" + ".Layer" );

            CuiHelper.DestroyUi(player, Layer + ".CraftPanel" + ".Layer");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region [ConsoleCommand]

        [ConsoleCommand("UI_BCrafts")]
        private void CmdConsoleCrafts(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "craftUi":
                {
                    int ID;
                    int.TryParse(arg.Args[1], out ID);

                    CraftUi(player, ID);
                    break;
                }
                case "trycraft":
                {
                    int ID; 
                    int.TryParse(arg.Args[1], out ID);

                    var craft = config.Crafts.FirstOrDefault(p => p.ID == ID);
                    if (craft == null) return;

                    if (!HasWorkbench(player, craft.Level))
                    {
                        player.ChatMessage("Не достаточный уровень верстака для крафта!");
                        return;
                    }

                    var allItems = player.inventory.AllItems();

                    if (craft.Items.Exists(item => !HasAmount(allItems, item.ShortName, item.SkinID, item.Amount)))
                    {
                        player.ChatMessage("Недостаточно ресурсов");
                        return;
                    }

                    ServerMgr.Instance.StartCoroutine(TakeAndGiveCraftItems(player, allItems, craft));

                    player.ChatMessage($"Вы успешно скрафтили <color=green>{craft.Title}</color>");

                    CraftUi(player, ID);
                    break;
                }
            }
        }

        #endregion

        #region [Func]

        private static bool HasAmount(Item[] items, string shortname, ulong skin, int amount)
        {
            return ItemCount(items, shortname, skin) >= amount;
        }

        private static int ItemCount(Item[] items, string shortname, ulong skin)
        {
            return Array.FindAll(items, item =>
                    item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
                .Sum(item => item.amount);
        }

        private static bool HasWorkbench(BasePlayer player, WorkbenchLevel level)
        {
            return level == WorkbenchLevel.Three ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3)
                : level == WorkbenchLevel.Two ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3) ||
                                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench2)
                : level == WorkbenchLevel.One ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3) ||
                                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench2) ||
                                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench1)
                : level == WorkbenchLevel.None;
        }

		private Dictionary<string, int> _itemIds = new Dictionary<string, int>();

		private int FindItemID(string shortName)
		{
			int val;
			if (_itemIds.TryGetValue(shortName, out val))
				return val;

			var definition = ItemManager.FindItemDefinition(shortName);
			if (definition == null) return 0;

			val = definition.itemid;
			_itemIds[shortName] = val;
			return val;
		}

        private IEnumerator TakeAndGiveCraftItems(BasePlayer player, Item[] allItems, CraftConf craft)
        {
            craft.Items.ForEach(item => Take(allItems, item.ShortName, item.SkinID, item.Amount));

            for (var i = 0; i < craft.Amount; i++)
            {
                GiveCraft(player, craft);

                yield return CoroutineEx.waitForFixedUpdate;
            }
        }

        private static void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
        {
            var num1 = 0;
            if (iAmount == 0) return;

            var list = Pool.GetList<Item>();

            foreach (var item in itemList)
            {
                if (item.info.shortname != shortname ||
                    (skinId != 0 && item.skin != skinId) || item.isBroken) continue;

                var num2 = iAmount - num1;
                if (num2 <= 0) continue;
                if (item.amount > num2)
                {
                    item.MarkDirty();
                    item.amount -= num2;
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

        private void GiveCraft(BasePlayer player, CraftConf cfg)
        {
            switch (cfg.Type)
            {
                default:
                {
                    cfg.Give(player);
                    break;
                }
            }
        }

        #endregion
    }
}