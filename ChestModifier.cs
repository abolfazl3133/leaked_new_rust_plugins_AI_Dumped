using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ChestModifier", "M&B-Studios", "1.0.0")]
    class ChestModifier : RustPlugin
    {
        #region Classes
        internal class ItemData
        {
            [JsonProperty(Order = 0)]
            public string Shortname;
            [JsonProperty("Skin id", Order = 1)]
            public ulong SkinId;
        }
        internal class Chest
        {
            [JsonProperty(Order = 0)]
            public string Shortname;
            
            [JsonProperty("Skin id", Order = 1)]
            public ulong SkinId;

            [JsonProperty("Item name", Order = 2)] 
            public string displayName;
            
            [JsonProperty(Order = 3)]
            public int Capacity;
            
            [JsonProperty("Blacklisted items", Order = 4)]
            public List<ItemData> BlacklistedItems;
            
            [JsonProperty("Allowed categories (still empty if allowed all)", Order = 5)]
            public List<string> AllowedCategories;
            
            [JsonProperty("Spawn chances", Order = 6)]
            public Dictionary<string, float> SpawnChances;


            public Item Get(int amount = 1)
            {
                var item = ItemManager.CreateByName(Shortname, amount, SkinId);
                if (!string.IsNullOrEmpty(displayName))
                    item.name = displayName;
                return item;
            }
        }
        #endregion

        #region Fields
        private const string Layer = "ui.ChestModifier.bg";
        private const string PERM_GIVE = "chestmodifier.give";
        private const string PERM_HELPMENU = "chestmodifier.helpmenu";
        private readonly List<string> AllCategories = new();

        private readonly Dictionary<string, string> CategoryToSprite = new()
        {
            ["Attire"] = "assets/icons/clothing.png",
            ["Misc"] = "assets/icons/menu_dots.png",
            ["Items"] = "assets/icons/extinguish.png",
            ["Component"] = "assets/icons/loot.png",
            ["Ammunition"] = "assets/icons/ammunition.png",
            ["Construction"] = "assets/icons/construction.png",
            ["Traps"] = "assets/icons/traps.png",
            ["Electrical"] = "assets/icons/electric.png",
            ["Fun"] = "assets/icons/fun.png",
            ["Food"] = "assets/icons/meat.png",
            ["Weapon"] = "assets/icons/weapon.png",
            ["Resources"] = "assets/icons/resource.png",
            ["Tool"] = "assets/icons/tools.png",
            ["Medical"] = "assets/icons/medical.png"
        };
        #endregion

        #region Hooks
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || player == null || !player.userID.IsSteamId())
                return;
            
            if (!player.IPlayer.HasPermission(PERM_HELPMENU))
                return;
            
            if (entity.skinID == 0)
                return;

            var storage = entity as StorageContainer;
            if (storage == null)
                return;
            
            var chest = cfg.Chests.FirstOrDefault(x => x.Value.SkinId == entity.skinID);
            if (chest.Key == null)
                return;

            UI_DrawHelpButton(player, storage.inventory.capacity, chest.Key);
        }

        private void OnLootEntityEnd(BasePlayer player, BaseEntity entity)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer + ".btn");
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            timer.Once(0.2f, () =>
            {
                if (entity == null)
                    return;
                if (entity.TryGetComponent<LootableCorpse>(out var corpse))
                {
                    foreach (var x in cfg.Chests.Where(x => x.Value.SpawnChances.ContainsKey(entity.PrefabName)))
                    {
                        foreach (var y in x.Value.SpawnChances.Where(z => z.Key == entity.PrefabName))
                        {
                            var rndm = UnityEngine.Random.Range(0f, 100.1f);
                            if (y.Value > rndm)
                            {
                                corpse.containers[0].capacity++;
                                x.Value.Get().MoveToContainer(corpse.containers[0]);
                            }
                        }
                    }
                }
                else if (entity.TryGetComponent<StorageContainer>(out var comp))
                {
                    var chest = cfg.Chests.FirstOrDefault(x => x.Value.SkinId == entity.skinID);
                    if (chest.Key == null)
                        return;
                    comp.inventory.capacity = chest.Value.Capacity;
                    comp.SendNetworkUpdate();
                }
            });
        }

        private void OnLootSpawn(LootContainer container)
        {
            if (container == null)
                return;
            
            foreach (var x in cfg.Chests.Where(x => x.Value.SpawnChances.ContainsKey(container.PrefabName)))
            {
                foreach (var y in x.Value.SpawnChances.Where(z => z.Key == container.PrefabName))
                {
                    var rndm = UnityEngine.Random.Range(0f, 100.1f);
                    if (y.Value > rndm)
                    {
                        container.inventory.capacity++;
                        container.inventory.GiveItem(x.Value.Get());
                    }
                }
            }
            
            return;
        }
        
        private void OnServerInitialized()
        {
            PrintWarning(
                $"Support M&B Studios\n Contact Discord: mbstudios");

            permission.RegisterPermission(PERM_GIVE, this);
            permission.RegisterPermission(PERM_HELPMENU, this);

            var categories = new List<string>();
            foreach (var x in ItemManager.itemList)
                if (!categories.Contains(x.category.ToString()))
                    categories.Add(x.category.ToString());

            foreach (var x in cfg.Chests)
            {
                foreach (var y in x.Value.AllowedCategories)
                    if (!categories.Contains(y))
                        PrintError($"Unknown category '{y}' in '{x.Key}'");
            }
        }

        private void Unload()
        {
            foreach (var x in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(x, Layer);
                CuiHelper.DestroyUi(x, Layer + ".btn");
            }
        }
        
        private ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (container == null || item == null)
                return null;
            
            var entity = container.GetEntityOwner();
            if (entity == null)
                return null;
        
            var chest = cfg.Chests.FirstOrDefault(x => x.Value.SkinId == entity.skinID);
        
            if (chest.Key == null)
                return null;
            
            if (!IsAllowedForChest(chest.Key, item))
                return ItemContainer.CanAcceptResult.CannotAccept;
            
            return null;
        }
        #endregion

        #region Methods

        private void SendMessage(BasePlayer player, string message)
        {
            if (player == null)
                PrintWarning(message);
            else
                player.ChatMessage(message);
        }

        private bool IsAllowedForChest(string key, Item item)
        {
            if (!cfg.Chests.TryGetValue(key, out var chest))
                return true;
            
            if (!chest.AllowedCategories.IsNullOrEmpty())
                if (!chest.AllowedCategories.Contains(item.info.category.ToString()))
                    return false;

            if (chest.BlacklistedItems.Any(x => x.SkinId == item.skin && x.Shortname == item.info.shortname))
                return false;

            return true;
        }
        private string ConcatArgs(string[] args, int start)
        {
            StringBuilder sb = new StringBuilder();
            
            for (int i = start; i < args.Length; i++)
            {
                if (i == start)
                    sb.Append(args[i]);
                else
                    sb.Append(" " + args[i]);
            }

            return sb.ToString();
        }
        #endregion

        #region UI

        private void UI_DrawMain(BasePlayer player, string key, int slots)
        {
            var chest = cfg.Chests[key];
            if (chest == null)
                return;

            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.317 0.317 0.317 0.5490196", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "192.26 109.4", OffsetMax = "573.589 367.201" }
            }, "Overlay", Layer);
            
            
            container.Add(new CuiElement
            {
                Name = Layer + ".label",
                Parent = Layer,
                Components = {
                    new CuiTextComponent { Text = chest.displayName.ToUpper(), Font = "robotocondensed-bold.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "0.8078431 0.7803922 0.7411765 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-190.658 95.74", OffsetMax = "190.662 128.9" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.4078432 0.3372549 0.3019608 1", Close = Layer },
                Text = { Text = "✖", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.8156863 0.5568628 0.4745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "161.617 99.907", OffsetMax = "186.383 124.733" }
            }, Layer, Layer + ".close");
            
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-190.658 48.727", OffsetMax = "190.662 95.74" }
            }, Layer, Layer + ".categories");
            
            container.Add(new CuiElement
            {
                Name = Layer + ".categories" + ".label",
                Parent = Layer + ".categories",
                Components = {
                    new CuiTextComponent { Text = "ALLOWED CATEGORIES", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "0.8078431 0.7803922 0.7411765 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-185.532 0.058", OffsetMax = "190.66 23.506" }
                }
            });
            
            float minx = -183.1622f;
            float maxx = -159.4378f;  
            float miny = -23.7244f;
            float maxy = 0f;
            
            var categories = chest.AllowedCategories.IsNullOrEmpty() ? AllCategories : chest.AllowedCategories;
            foreach (var x in chest.AllowedCategories)
            {
                
                container.Add(new CuiButton
                {
                    Button = { Color = "0.5254902 0.5019608 0.4666667 1", Sprite = CategoryToSprite[x] },
                    Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}", OffsetMax =  $"{maxx} {maxy}" }
                }, Layer + ".categories", Layer + $".cat.{x}");
                
                minx += 25.3f;
                maxx += 25.3f;
            }
            
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-190.665 -82.066", OffsetMax = "190.655 48.726" }
            }, Layer, Layer + ".blacklistedItems");
            container.Add(new CuiElement
            {
                Name = Layer + ".blacklistedItems" + ".label",
                Parent = Layer + ".blacklistedItems",
                Components = {
                    new CuiTextComponent { Text = "BLACKLISTED ITEMS", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "0.8078431 0.7803922 0.7411765 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-185.532 41.948", OffsetMax = "190.66 65.396" }
                }
            });
            minx = -181.5822f;
            maxx = -157.8578f;
            miny = 15.7378f;
            maxy = 39.4622f;
            int i = 0;
            foreach (var x in chest.BlacklistedItems)
            {
                var item = ItemManager.FindItemDefinition(x.Shortname);
                if (item == null)
                    continue;
                if (i != 0 && i % 14 == 0)
                {
                    minx = -181.5822f;
                    maxx = -157.8578f;
                }

                
                container.Add(new CuiElement()
                {
                    Name = Layer + ".blacklistedItems" + $".item.{x.Shortname}.{x.SkinId}",
                    Parent = Layer + ".blacklistedItems",
                    Components =
                    {
                        new CuiImageComponent() { ItemId = item.itemid, SkinId = x.SkinId },
                        new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}", OffsetMax =  $"{maxx} {maxy}" }
                    }
                });
                
                minx += 25.32f;
                maxx += 25.32f;
                i++;
            }
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-190.658 -128.905", OffsetMax = "190.662 -82.075" }
            }, Layer, Layer + ".description");

            container.Add(new CuiElement
            {
                Name = Layer + ".description" + ".capacity",
                Parent = Layer + ".description",
                Components = {
                    new CuiTextComponent { Text = $"CAPACITY: {slots}", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.LowerRight, Color = "0.8078431 0.7803922 0.7411765 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "32.022 -23.416", OffsetMax = "186.978 0" }
                }
            });
            
            CuiHelper.AddUi(player, container);
        }
        private void UI_DrawHelpButton(BasePlayer player, int slots, string key)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            
            container.Add(new CuiButton
            {
                Button = { Color = "0.3882353 0.3960785 0.3098039 1", Command = $"cm.openmenu {slots} {key}" },
                Text = { Text = "?", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.8078431 0.8431373 0.5490196 1" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = $"551.218 {176.688 + ((slots / 6) * 61.73)}", OffsetMax = $"571.382 {196.852  + ((slots / 6) * 61.73)}" }
            }, "Overlay", Layer + ".btn");
            
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Commands

        [ConsoleCommand("cm.openmenu")]
        private void cmdOpenMenu(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || arg.Args.IsNullOrEmpty())
                return;
            
            if (!arg.Player().IPlayer.HasPermission(PERM_HELPMENU))
                return;
            
            if (!int.TryParse(arg.Args[0], out var slots))
                return;

            var key = ConcatArgs(arg.Args, 1);
            if (!cfg.Chests.ContainsKey(key))
                return;
            UI_DrawMain(arg.Player(), key, slots);
        }
        
        [ChatCommand("cm.give")]
        private void cmdGive(BasePlayer player, string command, string[] args)
        {
            if (player != null)
                if (!player.IsAdmin && !player.IPlayer.HasPermission(PERM_GIVE))
                    return;

            if (args.IsNullOrEmpty())
            {
                SendMessage(player, "Usage: /cm.give 'chest key' 'player'");
                return;
            }

            if (!cfg.Chests.TryGetValue(args[0], out var chest))
            {
                SendMessage(player, $"Chest '{args[0]}' not found in config");
                return;
            }

            var arg = ConcatArgs(args, 1);

            if (string.IsNullOrEmpty(arg))
            {
                player.GiveItem(chest.Get());
                return;
            }
            
            var target = BasePlayer.activePlayerList.FirstOrDefault(x =>
                x.UserIDString == arg || x.displayName.Contains(arg, CompareOptions.IgnoreCase));
            if (target == null)
            {
                SendMessage(player, $"Player '{arg}' not found");
                return;
            }
            
            target.GiveItem(chest.Get());
        }
        #endregion
        
        #region Config

        private ConfigData cfg;

        public class ConfigData
        {
            [JsonProperty("Chests")] 
            public Dictionary<string, Chest> Chests;
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData()
            {
                Chests = new()
                {
                    ["test"] = new Chest
                    {
                        displayName = "Large test box",
                        Shortname = "box.wooden.large",
                        SkinId = 5,
                        Capacity = 3,
                        BlacklistedItems = new()
                        {
                            new()
                            {
                                Shortname = "rifle.ak",
                                SkinId = 0
                            },
                            new()
                            {
                                Shortname = "sulfur.ore",
                                SkinId = 0
                            },
                        },
                        AllowedCategories = new()
                        {
                            "Attire",
                            "Misc",
                            "Items",
                            "Component",
                            "Ammunition",
                            "Construction",
                            "Traps",
                            "Electrical",
                            "Fun",
                            "Food",
                            "Weapon",
                            "Resources",
                            "Tool",
                            "Medical"
                        },
                        SpawnChances = new()
                        {
                            ["assets/bundled/prefabs/radtown/crate_basic.prefab"] = 5,
                            ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 100,
                            ["assets/prefabs/npc/scientist/scientist_corpse.prefab"] = 100
                        }
                    },
                    ["test1"] = new Chest
                    {
                        displayName = "Small test box",
                        Shortname = "box.wooden",
                        SkinId = 6,
                        Capacity = 35,
                        BlacklistedItems = new()
                        {
                            new()
                            {
                                Shortname = "rifle.lr300",
                                SkinId = 0
                            },
                            new()
                            {
                                Shortname = "metal.ore",
                                SkinId = 0
                            },
                        },
                        AllowedCategories = new()
                        {
                            "Attire",
                            "Misc",
                            "Items",
                            "Component",
                            "Ammunition",
                            "Construction",
                            "Traps",
                            "Weapon",
                            "Resources",
                            "Tool",
                            "Medical"
                        },
                        SpawnChances = new()
                        {
                            ["assets/bundled/prefabs/radtown/crate_basic.prefab"] = 100,
                            ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 5,
                            ["assets/prefabs/npc/scientist/scientist_corpse.prefab"] = 50
                        }
                    }
                }
            };
            SaveConfig(config);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<ConfigData>();
            SaveConfig(cfg);
        }

        private void SaveConfig(object config)
        {
            Config.WriteObject(config, true);
        }
        #endregion
    }
}

