using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust.Workshop;
using Steamworks.ServerList;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Kits", "https://discord.gg/TrJ7jnS233", "1.0.2")]
    public class Kits : RustPlugin
    {


        #region Class && Data

        public class Kit
        {
            public string Name;
            public string DisplayName;
            public string DisplayNamePermission;
            public string CustomImage;
            public double Cooldown;
            public bool Hide;
            public string Permission;
            public List<KitItem> Items;
            public int UniversalNumber;
        }
        public class KitItem
        {
            public string ShortName;
            public int Amount;
            public int Blueprint;
            public ulong SkinID;
            public string Container;
            public float Condition;
            public Weapon Weapon;
            public List<ItemContent> Content;
        }
        public class Weapon
        {
            public string ammoType;
            public int ammoAmount;
        }
        public class ItemContent
        {
            public string ShortName;
            public float Condition;
            public int Amount;
        }

        public class KitsCooldown
        {
            public int Number;
            public double CoolDown;
        }


        public class CategoryKit
        {
            [JsonProperty("Название категории")]
            public string DisplayName;

            [JsonProperty("Картинка")]
            public string Image;

            [JsonProperty("Лист с китами")]
            public List<Kit> KitList = new List<Kit>();
        }


        public List<CategoryKit> KitLists = new List<CategoryKit>();

        public Dictionary<ulong, List<KitsCooldown>> CooldownData = new Dictionary<ulong, List<KitsCooldown>>();
        public List<ulong> OPENGUI = new List<ulong>();

        #endregion

        public string Layer = "UI_KitsLayer";
        [PluginReference] private Plugin ImageLibrary;

        public Dictionary<string, string> ImageDictionary = new Dictionary<string, string>()
        {
            ["osnova_kits"] = "https://i.imgur.com/E4jGqDa.png",
            ["close_kits"] = "https://i.imgur.com/WphI2cN.png",
            ["kitgive"] = "https://i.imgur.com/Qi1kUek.png",
            ["perexodyes"] = "https://i.imgur.com/0fjIVMR.png",
            ["perexodno"] = "https://i.imgur.com/Ngpty3c.png",
            ["namekits"] = "https://i.imgur.com/08cbFa9.png",
            ["kitcooldown"] = "https://i.imgur.com/DLmQCle.png",
            ["kitgivebutton"] = "https://i.imgur.com/R9xGnar.png",
            ["back_kits"] = "https://i.imgur.com/PGJxnr8.png",
            ["nedostupno"] = "https://i.imgur.com/EnR2Ism.png",
            ["netmesta"] = "https://i.imgur.com/0nbuFIT.png",
        };





        void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://devplugins.ru/\n" +
            "     VK - https://vk.com/dev.plugin\n" +
            "     Discord - https://discord.gg/eHXBY8hyUJ\n" +
            "-----------------------------");
            try
            {
                KitLists = Interface.GetMod().DataFileSystem.ReadObject<List<CategoryKit>>("HKits/KitList");
                CooldownData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<KitsCooldown>>>("HKits/CooldownPlayer");
            }
            catch
            {
                CooldownData = new Dictionary<ulong, List<KitsCooldown>>();
                KitLists = new List<CategoryKit>();
            }



            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();


                KitLists.ForEach(kit =>
                {
                    if (!ImageLibrary.Call<bool>("HasImage", kit.Image))
                    {
                        if (!string.IsNullOrEmpty(kit.Image) && !imagesList.ContainsKey(kit.Image))
                        {
                            imagesList.Add(kit.Image, kit.Image);
                        }
                    }
                    kit.KitList.ForEach(img =>
                    {
                        if (!permission.PermissionExists(img.Permission, this))
                        {
                            permission.RegisterPermission(img.Permission, this);
                        }
                        if (!ImageLibrary.Call<bool>("HasImage", img.CustomImage) && !string.IsNullOrEmpty(img.CustomImage) && !imagesList.ContainsKey(img.CustomImage))
                            imagesList.Add(img.CustomImage, img.CustomImage);

                    });
                });
                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);

            }

            ImageDictionary.ToList().ForEach(img =>
            {
                ImageLibrary?.Call("AddImage", img.Value, img.Key);
            });
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(basePlayer);
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, LayerTest);
            }
        }



        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("HKits/KitList", KitLists);
            Interface.Oxide.DataFileSystem.WriteObject("HKits/CooldownPlayer", CooldownData);
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, LayerTest);
            }
        }


        void OnPlayerConnected(BasePlayer player)
        {
            if (!CooldownData.ContainsKey(player.userID))
            {
                CooldownData.Add(player.userID, new List<KitsCooldown>());
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (OPENGUI.Contains(player.userID))
                OPENGUI.Remove(player.userID);
        }

        #region GiveItem

        private List<KitItem> GetItemPlayer(BasePlayer player)
        {
            List<KitItem> kititems = new List<KitItem>();
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemAddToKit(item, "wear");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemAddToKit(item, "main");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemAddToKit(item, "belt");
                    kititems.Add(iteminfo);
                }
            }
            return kititems;
        }

        private KitItem ItemAddToKit(Item item, string container)
        {
            KitItem kitem = new KitItem();
            kitem.Amount = item.amount;
            kitem.Container = container;
            kitem.SkinID = item.skin;
            kitem.Blueprint = item.blueprintTarget;
            kitem.ShortName = item.info.shortname;
            kitem.Condition = item.condition;
            kitem.Weapon = null;
            kitem.Content = null;
            if (item.info.category == ItemCategory.Weapon)
            {
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    kitem.Weapon = new Weapon();
                    kitem.Weapon.ammoType = weapon.primaryMagazine.ammoType.shortname;
                    kitem.Weapon.ammoAmount = weapon.primaryMagazine.contents;
                }
            }
            if (item.contents != null)
            {
                kitem.Content = new List<ItemContent>();
                foreach (var cont in item.contents.itemList)
                {
                    kitem.Content.Add(new ItemContent()
                    {
                        Amount = cont.amount,
                        Condition = cont.condition,
                        ShortName = cont.info.shortname
                    }
                    );
                }
            }
            return kitem;
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            foreach (var value in KitLists.Select(p => p.KitList))
            {
                foreach (var key in value)
                {
                    if (key.Name.ToLower() == "autokit")
                    {
                        player.inventory.Strip();
                        GiveItems(player, key);
                    }
                }
            }
        }


        private void GiveItems(BasePlayer player, Kit kit)
        {
            foreach (var kitem in kit.Items)
            {
                GiveItem(player, BuildItem(kitem.ShortName, kitem.Amount, kitem.SkinID, kitem.Condition, kitem.Blueprint, kitem.Weapon, kitem.Content), kitem.Container == "belt" ? player.inventory.containerBelt : kitem.Container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
            }
        }

        private void GiveItem(BasePlayer player, Item item, ItemContainer cont = null)
        {
            if (item == null) return;
            var iventory = player.inventory;
            var moved = item.MoveToContainer(cont) || item.MoveToContainer(iventory.containerMain);
            if (!moved)
            {
                if (cont == iventory.containerBelt) moved = item.MoveToContainer(iventory.containerWear);
                if (cont == iventory.containerWear) moved = item.MoveToContainer(iventory.containerBelt);
            }

            if (!moved) item.Drop(player.GetCenter(), player.GetDropVelocity());
        }

        private Item BuildItem(string ShortName, int Amount, ulong SkinID, float Condition, int blueprintTarget, Weapon weapon, List<ItemContent> Content)
        {
            Item item = ItemManager.CreateByName(ShortName, Amount, SkinID);
            item.condition = Condition;
            if (blueprintTarget != 0)
            {
                item.blueprintTarget = blueprintTarget;
            }
            if (weapon != null)
            {
                var getheld = item.GetHeldEntity() as BaseProjectile;
                if (getheld != null)
                {
                    getheld.primaryMagazine.contents = weapon.ammoAmount;
                    getheld.primaryMagazine.ammoType = ItemManager.FindItemDefinition(weapon.ammoType);
                }
            }
            if (Content != null)
            {
                foreach (var cont in Content)
                {
                    Item conts = ItemManager.CreateByName(cont.ShortName, cont.Amount);
                    conts.condition = cont.Condition;
                    conts.MoveToContainer(item.contents);
                }
            }
            return item;
        }

        #endregion



        [ChatCommand("kit")]
        void KitFunc(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                if (OPENGUI.Contains(player.userID))
                    return;

                OpenKitMenu(player);
                OPENGUI.Add(player.userID);
                return;
            }
            if (args.Length > 0)
            {
                if (args[0] == "create")
                {
                    if (player.IsAdmin)
                    {
                        if (args.Length < 2)
                        {
                            player.ChatMessage("Ошибка: /kit create <NAME>");
                            return;
                        }
                        CreateCategory(player, args[1]);
                    }
                }
                if (args[0] == "add")
                {
                    if (!player.IsAdmin) return;
                    if (args.Length < 3)
                    {
                        player.ChatMessage("Ошибка: /kit add <NAME> <CATEGORY>");
                        return;
                    }
                    AddKits(player, args[1], args[2], "kits.default");
                }
            }
        }


        void CreateCategory(BasePlayer player, string category)
        {
            if (KitLists.FirstOrDefault(p => p.DisplayName == category) == null)
            {
                string Image = "https://i.imgur.com/EbpKAtY.png";
                KitLists.Add(new CategoryKit
                {
                    DisplayName = category,
                    Image = Image,
                    KitList = new List<Kit>()
                });
                player.ChatMessage($"Вы успешно создали категорию с названием - {category}");
                ImageLibrary?.Call("AddImage", Image, Image);
            }
            else
            {
                player.ChatMessage($"Категория {category} уже существует!");
            }
        }

        [ConsoleCommand("give.kit")]
        void GivToKit(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (!args.HasArgs())
            {
                player.ChatMessage("Произошла ошибка #1 при выдаче! Отпишитесь администрации!");
                return;
            }
            GiveKitCategory(player, args.Args[0], args.Args[1]);
        }

        public string LayerNotif = "UI_KitsNotif";

        void NotifUIGive(BasePlayer player, string nameImage)
        {
            CuiHelper.DestroyUi(player, LayerNotif);
            var container = new CuiElementContainer();
            var Panel = container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.8171875 0.9055555", AnchorMax = "0.9822917 0.9694445" },
                CursorEnabled = false,
            }, "Overlay", LayerNotif);
            container.Add(new CuiElement
            {
                FadeOut = 0.3f,
                Parent = LayerNotif,
                Components =
                {
                    new CuiRawImageComponent
                        {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage", nameImage)},
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            CuiHelper.AddUi(player, container);
            timer.Once(5, () =>
            {
                if (player != null)
                {
                    CuiHelper.DestroyUi(player, LayerNotif);
                }
            });
        }

        object GiveKit(BasePlayer player, string nameKit)
        {
            var find = KitLists.Find(p => p.KitList.Find(x => x.Name == nameKit) != null);
            if (find != null)
            {
                var kit = find.KitList.Find(p => p.Name == nameKit);
                if (kit == null) return false;
                int beltcount = kit.Items.Where(i => i.Container == "belt").Count();
                int wearcount = kit.Items.Where(i => i.Container == "wear").Count();
                int maincount = kit.Items.Where(i => i.Container == "main").Count();
                int totalcount = beltcount + wearcount + maincount;
                if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount) if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                    {
                        return false;
                    }
                GiveItems(player, kit);
                return true;
            }
            return false;
        }


        void GiveKitCategory(BasePlayer player, string category, string kitname)
        {
            var findCategoryKit = KitLists.FirstOrDefault(p => p.DisplayName.ToLower() == category.ToLower());
            if (findCategoryKit == null)
            {
                player.ChatMessage("Произошла ошибка #2 при выдаче! Отпишитесь администрации!");
                return;
            }

            var find = findCategoryKit.KitList.FirstOrDefault(p => p.Name.ToLower() == kitname.ToLower());
            if (find != null)
            {
                var cooldown = CooldownData[player.userID].FirstOrDefault(p => p.Number == find.UniversalNumber);
                if (cooldown != null)
                {
                    var time = cooldown.CoolDown - CurrentTime();
                    if (time > 0)
                    {
                        player.ChatMessage($"Ошибка!\nПодождите: {TimeExtensions.FormatShortTime(TimeSpan.FromSeconds(time))} Дабы взять КИТ!");
                        return;
                    }
                    if (time <= 0)
                    {
                        CooldownData[player.userID].Remove(cooldown);
                    }
                    int beltcount = find.Items.Where(i => i.Container == "belt").Count();
                    int wearcount = find.Items.Where(i => i.Container == "wear").Count();
                    int maincount = find.Items.Where(i => i.Container == "main").Count();
                    int totalcount = beltcount + wearcount + maincount;
                    if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount) if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                        {
                            NotifUIGive(player, "netmesta");
                            return;
                        }
                    GiveItems(player, find);
                    CooldownData[player.userID].Add(new KitsCooldown
                    {
                        Number = find.UniversalNumber,
                        CoolDown = CurrentTime() + find.Cooldown
                    });

                    Interface.Oxide.DataFileSystem.WriteObject("HKits/CooldownPlayer", CooldownData);

                    NotifUIGive(player, "kitgive");
                    EffectNetwork.Send(new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player, 0, Vector3.up, Vector3.zero)
                    {
                        scale = UnityEngine.Random.Range(0f, 1f)
                    }
                    );
                }
                else
                {
                    int beltcount = find.Items.Where(i => i.Container == "belt").Count();
                    int wearcount = find.Items.Where(i => i.Container == "wear").Count();
                    int maincount = find.Items.Where(i => i.Container == "main").Count();
                    int totalcount = beltcount + wearcount + maincount;
                    if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount) if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                        {
                            if (player.SecondsSinceAttacked > 15)
                            {
                                NotifUIGive(player, "netmesta");
                                player.lastAttackedTime = UnityEngine.Time.time;
                            }

                            return;
                        }
                    GiveItems(player, find);
                    CooldownData[player.userID].Add(new KitsCooldown
                    {
                        Number = find.UniversalNumber,
                        CoolDown = CurrentTime() + find.Cooldown
                    });
                    NotifUIGive(player, "kitgive");
                    EffectNetwork.Send(new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player, 0, Vector3.up, Vector3.zero)
                    {
                        scale = UnityEngine.Random.Range(0f, 1f)
                    }
                    );
                }
            }
        }


        private static class TimeExtensions
        {
            public static string FormatShortTime(TimeSpan time)
            {
                string result = string.Empty;
                if (time.Days != 0) result += $"{time.Days} д. ";
                if (time.Hours != 0) result += $"{time.Hours} ч. ";
                if (time.Minutes != 0) result += $"{time.Minutes} м. ";
                if (time.Seconds != 0) result += $"{time.Seconds} с. ";
                return result;
            }
            private static string Format(int units, string form1, string form2, string form3)
            {
                var tmp = units % 10;
                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9) return $"{units} {form1}";
                if (tmp >= 2 && tmp <= 4) return $"{units} {form2}";
                return $"{units} {form3}";
            }
        }

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }



        void AddKits(BasePlayer player, string name, string category, string permissions)
        {
            var find = KitLists.FirstOrDefault(p => p.DisplayName == category);
            if (find == null)
            {
                player.ChatMessage($"Категория {category} не найдена!");
                return;
            }
            if (!permission.PermissionExists(permissions, this))
            {
                permission.RegisterPermission(permissions, this);
            }
            find.KitList.Add(new Kit
            {
                Name = name,
                DisplayName = name,
                DisplayNamePermission = "DIAMOND",
                Cooldown = 600,
                Hide = false,
                Permission = permissions,
                CustomImage = "https://i.imgur.com/EbpKAtY.png",
                Items = GetItemPlayer(player),
                UniversalNumber = UnityEngine.Random.Range(0000, 9999)
            });
            player.ChatMessage($"В категорию {category} был добавлен кит с названием {name}");
        }


        List<Kit> GetKitPlayer(BasePlayer player, List<Kit> kitsList)
        {
            return kitsList.Where(p => !p.Hide && permission.UserHasPermission(player.UserIDString, p.Permission)).ToList();
        }


        [ConsoleCommand("close.kit")]
        void CloseKit(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (OPENGUI.Contains(player.userID))
            {
                CuiHelper.DestroyUi(player, Layer);
                OPENGUI.Remove(player.userID);
            }
        }


        public string LayerTest = "UI_LayerTesting";

        [ConsoleCommand("kits.open")]
        void OpenKitCategory(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (!args.HasArgs()) return;
            var find = KitLists.FirstOrDefault(p => p.DisplayName == args.Args[0]);
            if (find != null)
            {
                OpenCategoryKit(player, find);
            }
        }

        [ConsoleCommand("back.kits")]
        void BackKitCategory(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            OpenKitMenu(player);
        }

        private string GetFormatTime(TimeSpan timespan)
        {
            return string.Format(timespan.TotalHours >= 1 ? "{2:00}ч {0:00}м {1:00}с" : "{0:00}м {1:00} с", timespan.Minutes, timespan.Seconds, System.Math.Floor(timespan.TotalHours));
        }


        void OpenCategoryKit(BasePlayer player, CategoryKit kit)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            var Panel = container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true,
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".CloseBack",
                Components =
                {
                    new CuiRawImageComponent
                        {Png = (string) ImageLibrary?.Call("GetImage", "back_kits")},
                    new CuiRectTransformComponent {AnchorMin = "0.4796878 0.2351852", AnchorMax = "0.5208334 0.3092591"},
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Command = "back.kits" },
                Text = { Text = "" }
            }, Layer + ".CloseBack");


            var list = GetKitPlayer(player, kit.KitList);
            var page = list.Count;
            var height = 25f;
            var width = 165f;
            var margin = 15f;
            var switchs = -(width * page + (page - 1) * margin) / 2f;

            foreach (var check in list.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                {
                    AnchorMin =
                        $"0.5 0.65",
                    AnchorMax =
                        $"0.5 0.65",
                    OffsetMin =
                        $"{switchs} -212",
                    OffsetMax =
                        $"{switchs + width} -3"
                },
                    Button =
                {
                    Color = "0 0 0 0",
                    Command = $""
                },
                    Text =
                {
                    Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                }
                }, Layer, Layer + $".{check.B}.ListItemKitCategory");
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItemKitCategory",
                    Name = Layer + $".{check.B}.ImgList",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage", "osnova_kits")},
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItemKitCategory",
                    Name = Layer + $".{check.B}.ImgListNamePermission",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage", "namekits")},
                        new CuiRectTransformComponent
                            {AnchorMin = "0.266127 0.9460317", AnchorMax = "0.7217703 1.053968"}
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItemKitCategory",
                    Name = Layer + $".{check.B}.NamePermission",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{check.A.DisplayNamePermission}", Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", Color = "0.972549 0.9764706 1 1", FontSize = 12,
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0.266127 0.9460317", AnchorMax = "0.7217703 1.053968"}
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItemKitCategory",
                    Name = Layer + $".{check.B}.ImgKitCategory",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage", check.A.CustomImage)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0.2580646 0.4603175", AnchorMax = "0.7379035 0.8412699"}
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItemKitCategory",
                    Name = Layer + $".{check.B}.TxtNameKit",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{check.A.DisplayName}", Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", Color = "0.972549 0.9764706 1 1", FontSize = 12,
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0.3142858", AnchorMax = "1 0.3777778"}
                    }
                });

                /*if (!permission.UserHasPermission(player.UserIDString, check.A.Permission))
                {
                    container.Add(new CuiElement
                    {
                        FadeOut = 0.3f,
                        Parent = Layer + $".{check.B}.ListItemKitCategory",
                        Name = Layer + $".{check.B}.TxtDostup",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "Недоступно", Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-regular.ttf", Color = "0.4745098 0.4862745 0.5607843 1", FontSize = 10,
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0.2476192", AnchorMax = "1 0.3111112"}
                        }
                    });
                    container.Add(new CuiElement
                    {
                        FadeOut = 0.3f,
                        Parent = Layer + $".{check.B}.ListItemKitCategory",
                        Name = Layer + $".{check.B}.ImgListNetDostup",
                        Components =
                        {
                            new CuiRawImageComponent
                                {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage", "nedostupno")},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.125 0.05079359", AnchorMax = "0.8790323 0.2031745"}
                        }
                    });
                    CuiHelper.AddUi(player, container);
                    continue;
                }*/

                string color = null;
                string text = null;
                string times = null;
                string image = null;
                var cooldown = CooldownData[player.userID].FirstOrDefault(p => p.Number == check.A.UniversalNumber);
                if (cooldown != null)
                {
                    double time = cooldown.CoolDown - CurrentTime();
                    if (time > 0)
                    {
                        image = "kitcooldown";
                        color = "0.4745098 0.4862745 0.5607843 1";
                        text = "Недоступно";
                        times = $"{GetFormatTime(TimeSpan.FromSeconds(time))}";
                    }
                    else
                    {
                        if (!permission.UserHasPermission(player.UserIDString, check.A.Permission))
                        {
                            image = "nedostupno";
                            color = "0.4745098 0.4862745 0.5607843 1";
                            text = "Недоступно";
                        }
                        else
                        {
                            image = "kitgivebutton";
                            color = "0.3921569 0.7490196 0.2705882 1";
                            text = "Доступно";
                        }
                    }
                }
                else
                {
                    if (!permission.UserHasPermission(player.UserIDString, check.A.Permission))
                    {
                        image = "nedostupno";
                        color = "0.4745098 0.4862745 0.5607843 1";
                        text = "Недоступно";
                    }
                    else
                    {
                        image = "kitgivebutton";
                        color = "0.3921569 0.7490196 0.2705882 1";
                        text = "Доступно";
                    }
                }

                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItemKitCategory",
                    Name = Layer + $".{check.B}.TxtDostup",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text, Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", Color = color, FontSize = 10,
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0.2476192", AnchorMax = "1 0.3111112"}
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItemKitCategory",
                    Name = Layer + $".{check.B}.ImgListButtonImage",
                    Components =
                        {
                            new CuiRawImageComponent
                                {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage", image)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.125 0.05079359", AnchorMax = "0.8790323 0.2031745"}
                        }
                });

                if (!string.IsNullOrEmpty(times))
                {
                    container.Add(new CuiElement
                    {
                        FadeOut = 0.3f,
                        Parent = Layer + $".{check.B}.ListItemKitCategory",
                        Name = Layer + $".{check.B}.TxtCoolDown",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"{times} м", Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-regular.ttf", Color = "0.4745098 0.4862745 0.5607843 1", FontSize = 10,
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0.4435487 0.09841274", AnchorMax = "0.6451586 0.1619046"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Command = $"give.kit {kit.DisplayName} {check.A.Name}" },
                        Text = { Text = "" }
                    }, Layer + $".{check.B}.ImgListButtonImage");
                }
                switchs += width + margin;
            }

            CuiHelper.AddUi(player, container);
        }


        #region UI


        List<CategoryKit> GetCategory(BasePlayer player)
        {
            List<CategoryKit> categoryKits = new List<CategoryKit>();
            foreach (var value in KitLists)
            {
                foreach (var kit in value.KitList)
                {
                    if (permission.UserHasPermission(player.UserIDString, kit.Permission) && !categoryKits.Contains(value))
                    {
                        categoryKits.Add(value);
                    }
                }
            }
            return categoryKits;
        }


        void OpenKitMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            var Panel = container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true,
            }, "Overlay", Layer);
            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Close",
                Components =
                {
                    new CuiRawImageComponent
                        {Png = (string) ImageLibrary?.Call("GetImage", "close_kits")},
                    new CuiRectTransformComponent {AnchorMin = "0.4796878 0.2351852", AnchorMax = "0.5208334 0.3092591"},
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Command = "close.kit" },
                Text = { Text = "" }
            }, Layer + ".Close");
            var category = GetCategory(player);
            var page = category.Count;
            var height = 25f;
            var width = 165f;
            var margin = 15f;
            var switchs = -(width * page + (page - 1) * margin) / 2f;
            foreach (var check in category.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                {
                    AnchorMin =
                        $"0.5 0.65",
                    AnchorMax =
                        $"0.5 0.65",
                    OffsetMin =
                        $"{switchs} -212",
                    OffsetMax =
                        $"{switchs + width} -3"
                },
                    Button =
                {
                    Color = "0 0 0 0",
                    Command = $""
                },
                    Text =
                {
                    Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                }
                }, Layer, Layer + $".{check.B}.ListItem");
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItem",
                    Name = Layer + $".{check.B}.ImgList",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage", "osnova_kits")},
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItem",
                    Name = Layer + $".{check.B}.ImgList",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage", check.A.Image)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0.2580646 0.4603175", AnchorMax = "0.7379035 0.8412699"}
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItem",
                    Name = Layer + $".{check.B}.TxtNameCategory",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{check.A.DisplayName}", Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", Color = "0.972549 0.9764706 1 1", FontSize = 12,
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0.3142858", AnchorMax = "1 0.3777778"}
                    }
                });
                List<Kit> listKit = GetKitPlayer(player, check.A.KitList);
                string color = listKit.Count > 0 ? "0.3921569 0.7490196 0.2705882 1" : "0.4745098 0.4862745 0.5607843 1";
                string text = listKit.Count > 0 ? "Есть доступные" : "Нет доступных";
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ListItem",
                    Name = Layer + $".{check.B}.TxtDostup",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text, Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", Color = color, FontSize = 10,
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0.2476192", AnchorMax = "1 0.3111112"}
                    }
                });
                if (listKit.Count > 0)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{check.B}.ListItem",
                        Name = Layer + $".Perexod.{check.B}",
                        Components =
                        {
                            new CuiRawImageComponent
                                {Png = (string) ImageLibrary?.Call("GetImage", "perexodyes")},
                            new CuiRectTransformComponent {AnchorMin = "0.125 0.05079359", AnchorMax = "0.8790323 0.2031745"},
                        }
                    });
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Command = $"kits.open {check.A.DisplayName}" },
                        Text = { Text = "" }
                    }, Layer + $".Perexod.{check.B}");
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".{check.B}.ListItem",
                        Name = Layer + $".Perexod.{check.B}",
                        Components =
                        {
                            new CuiRawImageComponent
                                {Png = (string) ImageLibrary?.Call("GetImage", "perexodno")},
                            new CuiRectTransformComponent {AnchorMin = "0.125 0.05079359", AnchorMax = "0.8790323 0.2031745"},
                        }
                    });
                }
                switchs += width + margin;
            }
            CuiHelper.AddUi(player, container);
        }





        #endregion

    }
}